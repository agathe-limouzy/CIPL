using System;
using System.Collections.Generic;
using System.Linq;

/// Le loyer dans le temps : paliers, franchise, montant d'une période de facturation.
/// Règles en un seul endroit — le suivi, les alertes, la facture et la rentabilité
/// passent tous par ici.
///
/// Un palier commence toujours le 1er jour d'une période de facturation (décision du
/// 30/09) : une période n'est donc jamais à cheval sur deux paliers. Seules la première
/// et la dernière période peuvent être partielles — bail ou franchise qui démarre en
/// cours de période, départ du locataire en cours de période — et sont alors
/// proratisées au jour. La fin du bail seule n'arrête rien : sans départ saisi, le
/// loyer continue (tacite prolongation), au dernier palier.
public static class Loyers
{
    /// Revue 16c (D7b-03) : la date de révision proposée : celle enregistrée, sinon un an après le début du bail (sinon
    /// aujourd'hui) — « aujourd'hui » rendait la révision due dès l'initialisation.
    public static DateTime PremiereRevisionParDefaut(Locataire loc)
    {
        if (DateTime.TryParse(loc?.moisDeRevisionISO, out var d)) return d;
        return FacturationSuivi.TryEcheance(loc?.dateDebutBailISO, out var debut) ? debut.AddYears(1) : DateTime.Today;
    }

    static bool Date(string iso, out DateTime d) => FacturationSuivi.TryEcheance(iso, out d);
    static DateTime Fin(PalierLoyer p) => Date(p?.finISO, out var f) ? f : DateTime.MaxValue;
    static string Iso(DateTime d) => d.ToString("yyyy-MM-dd");

    // ── Début de la facturation ────────────────────────────────────────────────

    /// Premier jour couvert par les paliers : le début du bail en cours, franchise
    /// COMPRISE — le 1er palier contient la franchise, qui reste gratuite (décision du
    /// 30/09 : « 01/09 → 31/12 · 4 mois, dont 3 mois payés »). Jamais avant un avenant :
    /// les paliers décrivent les conditions en vigueur depuis la renégociation.
    public static bool DebutPaliers(Locataire loc, out DateTime debut)
    {
        if (!Date(loc?.dateDebutBailISO, out debut)) return false;
        if (Date(loc.debutConditionsISO, out var avenant) && avenant > debut) debut = avenant;
        return true;
    }

    /// Premier jour facturé : la franchise, et jamais avant le tout premier bail.
    /// MinValue si aucune date n'est connue (tout est facturable, comme avant).
    /// N'utilise pas LoyerHistoryService.PremierBail : il renvoie la date du jour
    /// quand rien n'est saisi, ce qui masquerait tout le passé.
    public static DateTime DebutFacturation(Locataire loc)
    {
        var debut = DateTime.MinValue;
        if (DebutPremierBail(loc, out var p)) debut = p;
        if (Date(loc?.debutFacturationISO, out var f) && f > debut) debut = f;
        return debut.Date;
    }

    /// Dernier jour facturé : le jour du départ du locataire. MaxValue s'il reste — le
    /// loyer continue alors après la fin du bail (décision du 30/09).
    public static DateTime FinFacturation(Locataire loc)
        => Date(loc?.dateSortieISO, out var s) ? s.Date : DateTime.MaxValue;

    /// Vrai si le locataire était dans les lieux au moins un jour de l'année civile :
    /// du tout premier bail à son départ. Pas de régularisation des charges d'une année
    /// où il n'était pas là (retour du 30/09 : un bail de 2026 affichait des régul
    /// depuis 2023). Sans dates connues, toutes les années comptent (comme avant).
    public static bool AnneeDansLeBail(Locataire loc, int annee)
    {
        if (DebutPremierBail(loc, out var debut) && annee < debut.Year) return false;
        if (Date(loc?.dateSortieISO, out var sortie) && annee > sortie.Year) return false;
        return true;
    }

    /// Premier jour du bail (le tout premier, conservé à travers les renouvellements).
    public static bool DebutPremierBail(Locataire loc, out DateTime d)
        => Date(loc?.dateDebutPremierBailISO, out d) || Date(loc?.dateDebutBailISO, out d);

    /// Revue 17 (retour BL4, 04/11) : dans les lieux ce jour-là, du premier bail au départ, comme FractionPresence (sans
    /// date de bail : oui).
    public static bool PresentLe(Locataire loc, DateTime jour)
        => (!DebutPremierBail(loc, out var debut) || debut.Date <= jour.Date) && jour.Date <= FinFacturation(loc);

    // ── Loyer d'une date, montant d'une période ───────────────────────────────

    /// Loyer annuel HT en vigueur à cette date : avant un avenant, le loyer de
    /// l'historique ; ensuite le palier qui la contient (le premier avant le début, le
    /// dernier après la fin — tacite prolongation), sinon le loyer courant.
    public static float LoyerAnnuelA(Locataire loc, DateTime date) => LoyerAnnuelA(loc, date, PaliersTries(loc));

    // Revue 17 (D7b-16, lot D) : les paliers triés par fin (null : pas de paliers en vigueur), une fois par MontantPeriode et non
    // plus une fois par jour (tri et lecture des dates, 365 fois par locataire et par année du tableau de rentabilité).
    static List<PalierLoyer> PaliersTries(Locataire loc)
        => loc != null && loc.typeRevision == TypeRevision.Paliers && loc.paliers != null && loc.paliers.Count > 0
            ? loc.paliers.OrderBy(Fin).ToList() : null;

    static float LoyerAnnuelA(Locataire loc, DateTime date, List<PalierLoyer> tries)
    {
        if (loc == null) return 0f;
        if (Date(loc.debutConditionsISO, out var avenant) && date.Date < avenant.Date && loc.historiqueLoyers != null)
            foreach (var h in loc.historiqueLoyers)
                if ((!Date(h.debutISO, out var hd) || hd.Date <= date.Date) && Date(h.finISO, out var hf) && date.Date <= hf.Date)
                    return h.loyer;
        if (tries == null) return loc.loyerAnnuel;
        return tries[IndexEnVigueur(tries, date)].loyer;
    }

    /// Index (dans la liste triée) du palier en vigueur à cette date.
    public static int IndexEnVigueur(List<PalierLoyer> tries, DateTime date)
    {
        int i = 0;
        for (int k = 0; k < tries.Count; k++)
            if (Date(tries[k].debutISO, out var d) && d.Date <= date.Date) i = k;
        return i;
    }

    /// Premier palier qui commence après cette date, ou null.
    public static PalierLoyer Prochain(Locataire loc, DateTime date)
        => loc?.paliers?.OrderBy(Fin).FirstOrDefault(p => Date(p.debutISO, out var d) && d.Date > date.Date);

    /// Montant HT du loyer d'une période = loyer annuel / nombre de périodes, au jour
    /// près : chaque jour facturé compte au loyer en vigueur ce jour-là. D'où le
    /// prorata quand la facturation commence (bail, franchise) ou s'arrête (départ) en
    /// cours de période, et quand un avenant change le loyer en cours de période. Une
    /// période pleine à un seul loyer vaut exactement loyer / n. 0 si la période est
    /// entièrement non facturable (avant le bail, en franchise, après le départ).
    /// Au jour et non au mois (choix de l'utilisatrice, 01/10 : plus juste) — un mois
    /// de franchise sur un trimestre laisse donc 59/90 et non 2/3.
    /// Revue 17 (avenant de périodicité, lot 14) : la période est celle du PLAN de l'année (FacturationSuivi.Periode) : ses bornes
    /// facturées et SA périodicité, pas celle d'aujourd'hui. Coupée par un avenant de périodicité, elle est au prorata des MOIS
    /// (décision du 05/11 : 2 mois sur 3 d'un trimestre à 3 000 € = 2 000 €), pour que l'année du changement fasse exactement le
    /// loyer annuel ; départ, franchise et avenant de loyer restent au jour, à l'intérieur des mois facturés.
    public static float MontantPeriode(Locataire loc, int year, int periode)
    {
        if (loc == null) return 0f;
        var pp = FacturationSuivi.Periode(loc, year, periode);
        DateTime debut = pp.debut, fin = pp.fin;
        var d0 = DebutFacturation(loc);
        if (d0 < debut) d0 = debut;
        var d1 = FinFacturation(loc);
        if (d1 > fin) d1 = fin;
        if (d0 > d1) return 0f;
        int n = LoyerSummaryUI.NbPeriodes(pp.periodicite);
        double somme = 0;
        var tries = PaliersTries(loc);   // Revue 17 (D7b-16, lot D) : triés une fois pour la période
        for (var d = d0; d <= d1; d = d.AddDays(1)) somme += LoyerAnnuelA(loc, d, tries);
        return (float)Math.Round(somme / n / ((fin - debut).TotalDays + 1) * pp.Part, 2);   // Part = 1 pour une période entière
    }

    /// Vrai si une facture est attendue pour la période : au moins un jour de loyer
    /// facturable — ou, pendant la franchise, des provisions à appeler. La franchise
    /// porte sur le loyer seul, les provisions restent dues (retour du 01/10).
    public static bool PeriodeFacturable(Locataire loc, int year, int periode)
    {
        FacturationSuivi.PeriodeBornes(loc, year, periode, out var debut, out var fin);
        var depuis = !AppelleProvisions(loc) ? DebutFacturation(loc)
                   : DebutPremierBail(loc, out var p) ? p.Date : DateTime.MinValue;
        return fin >= depuis && debut <= FinFacturation(loc);
    }

    /// Vrai si la franchise couvre le début de la période (loyer nul ou partiel).
    public static bool EnFranchise(Locataire loc, int year, int periode)
    {
        if (!Date(loc?.debutFacturationISO, out var f)) return false;
        FacturationSuivi.PeriodeBornes(loc, year, periode, out var debut, out _);
        return debut < f.Date;
    }

    /// Le locataire verse des provisions pour charges : elles sont facturées même
    /// pendant la franchise.
    public static bool AppelleProvisions(Locataire loc) => ListesCharges.ProvisionTotale(loc) > 0f;

    // ── Provisions pour charges et périodicité (Revue 17, avenant de périodicité, lot 16) ──

    /// La provision se saisit « par période de loyer » : d'une périodicité à l'autre elle garde la même somme par an (100 € par
    /// mois → 300 € par trimestre → 600 € par semestre), au centime. Même périodicité : le montant tel quel.
    public static float ProvisionConvertie(float montant, Periodicite de, Periodicite vers)
        => de == vers ? montant
         : (float)Math.Round(Tva.Centimes(montant) * LoyerSummaryUI.NbPeriodes(de) / LoyerSummaryUI.NbPeriodes(vers), 2, MidpointRounding.AwayFromZero);

    /// La provision qu'une période de loyer appelle pour une liste de charges ("" : la générale), d'après la fiche : celle de la
    /// fiche, qui vaut par période de la périodicité ACTUELLE, ramenée à la périodicité de la PÉRIODE (celle de sa tranche dans le
    /// plan) : après un passage au trimestriel à 300 €, mai, encore mensuel, appelle 100 €. Une période coupée par l'avenant appelle
    /// la provision ENTIÈRE de sa périodicité, comme une arrivée ou un départ (la régularisation corrige). Sans avenant de
    /// périodicité : la provision de la fiche, comme avant.
    public static float ProvisionPeriode(Locataire loc, int annee, int periode, string listeId)
        => loc == null ? 0f
         : ProvisionConvertie(ListesCharges.Provision(loc, listeId), loc.periodiciteLoyer, FacturationSuivi.PeriodiciteDe(loc, annee, periode));

    /// Les provisions RÉELLEMENT appelées pour une liste ("" : la générale) sur une année : la somme, pour chaque période du plan
    /// qui reçoit une facture (PeriodeFacturable), du montant imprimé sur son loyer quand il est connu (FactureEtat.provisionsConnues,
    /// D6-07), sinon de celui de la fiche ramené à la périodicité de la période (ProvisionPeriode). C'est ce que la régularisation
    /// déduit (D6-N5). Avant : provision de la fiche × nombre de périodes, soit 300 × 8 = 2 400 € l'année d'un passage du mensuel
    /// (100 €) au trimestriel (300 €) au 1er juillet, pour 1 200 € appelés ; et une provision changée en cours d'année était
    /// recomptée sur toute l'année.
    /// ponytail: un loyer émis hors du plan (fiche changée sans date d'effet avant le lot 14) n'est pas compté ; le montant
    /// proposé reste modifiable dans le formulaire.
    public static float ProvisionsAppelees(Locataire loc, int annee, string listeId)
    {
        if (loc == null) return 0f;
        decimal total = 0m;
        for (int p = 1, n = FacturationSuivi.Plan(loc, annee).Count; p <= n; p++)
        {
            if (!PeriodeFacturable(loc, annee, p)) continue;
            string cle = $"loyer-{annee}-P{p}";
            var ligne = loc.facturesEtat?.Find(x => x != null && x.key == cle);
            total += Tva.Centimes(ligne != null && ligne.provisionsConnues ? ProvisionImprimee(ligne, listeId) : ProvisionPeriode(loc, annee, p, listeId));
        }
        return (float)total;
    }

    // La provision imprimée sur un loyer pour une liste : la générale dans `provision`, les autres dans `provisionsListes`.
    static float ProvisionImprimee(FactureEtat ligne, string listeId)
        => string.IsNullOrEmpty(listeId) ? ligne.provision
         : ligne.provisionsListes?.Find(m => m != null && m.listeId == listeId)?.montant ?? 0f;

    /// Vrai si la date est le 1er jour d'une période de facturation du locataire.
    /// Revue 17 (avenant de périodicité, lot 14) : une période du plan de cette année-là (la périodicité qui valait alors).
    public static bool EstDebutPeriode(Locataire loc, DateTime date)
        => date.Day == 1 && FacturationSuivi.Plan(loc, date.Year).Any(p => p.debut == date.Date);

    /// Revue 17 (D7b-17, lot D) : avec cette périodicité et ces mois (et la date d'effet d'un avenant de périodicité ; null sans),
    /// une fin de palier autre que la fin du bail ne serait plus la veille d'un début de période : la période serait à cheval sur
    /// deux paliers (prorata au jour) et le formulaire du palier ne retrouverait plus sa fin. Calculé sur une copie : la fiche ne
    /// change pas.
    public static bool PaliersDesalignesApres(Locataire loc, Periodicite periodicite, List<int> mois, DateTime? effet)
    {
        if (loc?.paliers == null || loc.paliers.Count == 0) return false;
        var copie = UnityEngine.JsonUtility.FromJson<Locataire>(UnityEngine.JsonUtility.ToJson(loc));
        if (effet.HasValue) EnregistrerAvenantPeriodicite(copie, effet.Value);
        copie.periodiciteLoyer = periodicite;
        copie.moisFacturationLoyer = new List<int>(mois ?? new List<int>());
        var finBail = Date(copie.dateFinBailISO, out var fb) ? fb.Date : DateTime.MaxValue;
        return copie.paliers.Any(p => Date(p?.finISO, out var f) && f.Year < 9999 && f.Date != finBail
                                      && !EstDebutPeriode(copie, f.Date.AddDays(1)));
    }

    // ── Paliers ────────────────────────────────────────────────────────────────

    /// Trie par date de fin et recalcule les débuts : le premier au début des paliers,
    /// chacun des suivants au lendemain de la fin du précédent. Modifier une fin décale
    /// donc le palier suivant, et supprimer un palier donne son début au suivant.
    public static void Normaliser(List<PalierLoyer> paliers, DateTime debut)
    {
        paliers.Sort((a, b) => Fin(a).CompareTo(Fin(b)));
        var d = debut.Date;
        foreach (var p in paliers)
        {
            p.debutISO = Iso(d);
            if (Date(p.finISO, out var f)) d = f.Date.AddDays(1);
        }
    }

    /// Ajoute (index = -1) ou modifie le palier `index` de la liste triée. Renvoie le
    /// message à afficher, ou null si c'est fait (liste normalisée).
    public static string PoserPalier(Locataire loc, List<PalierLoyer> liste, int index, float loyer, DateTime fin)
    {
        if (!DebutPaliers(loc, out var debut)) return "Renseignez d'abord la date de début du bail.";
        if (!Date(loc.dateFinBailISO, out var finBail)) return "Renseignez d'abord la date de fin du bail.";
        if (loyer <= 0f) return "Le loyer du palier doit être supérieur à zéro.";
        Normaliser(liste, debut);
        fin = fin.Date;
        finBail = finBail.Date;

        DateTime du;
        if (index < 0)
        {
            du = liste.Count > 0 ? Fin(liste[liste.Count - 1]).Date.AddDays(1) : debut.Date;
            if (du > finBail) return "Les paliers couvrent déjà tout le bail : raccourcissez le dernier pour en ajouter un.";
        }
        else if (!Date(liste[index].debutISO, out du)) return "Palier illisible.";

        if (fin < du) return $"La fin doit tomber après le début du palier ({du:dd/MM/yyyy}).";
        if (fin > finBail) return $"La fin ne peut pas dépasser la fin du bail ({finBail:dd/MM/yyyy}).";
        if (fin != finBail && !EstDebutPeriode(loc, fin.AddDays(1)))
            return "Un palier se termine la veille d'un début de période de facturation "
                 + $"(ex. le {PremiereFinPossible(loc, du):dd/MM/yyyy}), ou à la fin du bail.";
        if (index >= 0 && index + 1 < liste.Count && fin >= Fin(liste[index + 1]).Date)
            return "Le palier suivant finirait avant de commencer : modifiez-le ou supprimez-le d'abord.";

        if (index < 0) liste.Add(new PalierLoyer { loyer = loyer, finISO = Iso(fin) });
        else { liste[index].loyer = loyer; liste[index].finISO = Iso(fin); }
        Normaliser(liste, debut);
        return null;
    }

    /// Fins possibles d'un palier qui commence le `du` : la veille de chaque début de
    /// période de facturation jusqu'à la fin du bail, puis la fin du bail elle-même.
    /// C'est la liste proposée à l'écran : on ne choisit que des dates valides.
    public static List<DateTime> FinsPossibles(Locataire loc, DateTime du)
    {
        var res = new List<DateTime>();
        if (loc == null || !Date(loc.dateFinBailISO, out var finBail)) return res;
        finBail = finBail.Date; du = du.Date;
        // Un palier tout entier dans la franchise ne ferait rien payer : non proposé.
        var facture = DebutFacturation(loc);
        for (var m = new DateTime(du.Year, du.Month, 1).AddMonths(1); m <= finBail; m = m.AddMonths(1))
        {
            var fin = m.AddDays(-1);
            if (EstDebutPeriode(loc, m) && fin >= du && fin >= facture) res.Add(fin);
        }
        if (finBail >= du && !res.Contains(finBail)) res.Add(finBail);
        return res;
    }

    /// Durée d'un palier, et ce qui en est payé quand la franchise en mange le début :
    /// « 4 mois, dont 3 mois payés ». Sans franchise dans le palier, la durée seule.
    public static string DureeLibelle(Locataire loc, DateTime du, DateTime fin)
    {
        string total = DureeTexte(du, fin);
        var facture = DebutFacturation(loc);
        if (facture <= du.Date || facture > fin.Date) return total;
        string paye = DureeTexte(facture, fin);
        return $"{total}, dont {paye} {(paye == "1 an" ? "payé" : "payés")}";
    }

    // Première fin valide à partir de `du` (veille d'un début de période), pour l'exemple
    // donné dans le message d'erreur.
    static DateTime PremiereFinPossible(Locataire loc, DateTime du)
    {
        var d = new DateTime(du.Year, du.Month, 1).AddMonths(1);
        for (int k = 0; k < 24 && !EstDebutPeriode(loc, d); k++) d = d.AddMonths(1);
        return d.AddDays(-1);
    }

    /// Les paliers couvrent-ils le bail sans trou, du début des paliers à la fin du bail ?
    public static bool Couvrent(Locataire loc, List<PalierLoyer> liste, out string msg)
    {
        msg = null;
        if (liste == null || liste.Count == 0) { msg = "Ajoutez au moins un palier."; return false; }
        if (!DebutPaliers(loc, out var debut)) { msg = "Renseignez la date de début du bail."; return false; }
        if (!Date(loc.dateFinBailISO, out var finBail)) { msg = "Renseignez la date de fin du bail."; return false; }

        var d = debut.Date;
        foreach (var p in liste.OrderBy(Fin))
        {
            if (!Date(p.debutISO, out var pd) || pd.Date != d)
            { msg = $"Les paliers doivent se suivre sans trou à partir du {d:dd/MM/yyyy}."; return false; }
            if (!Date(p.finISO, out var pf) || pf.Date < pd.Date)
            { msg = "Un palier a une date de fin invalide."; return false; }
            d = pf.Date.AddDays(1);
        }
        var derniere = d.AddDays(-1);
        if (derniere < finBail.Date)
        { msg = $"Les paliers s'arrêtent le {derniere:dd/MM/yyyy} : le bail court jusqu'au {finBail:dd/MM/yyyy}."; return false; }
        if (derniere > finBail.Date)
        { msg = $"Les paliers dépassent la fin du bail ({finBail:dd/MM/yyyy})."; return false; }
        return true;
    }

    /// Paliers : le loyer courant suit le palier du jour, le précédent celui d'avant.
    /// Appelé au chargement et à la validation des paliers, pour que tous les lecteurs
    /// de `loyerAnnuel` (fiche, rentabilité, dépôt) restent justes sans le savoir.
    public static void Actualiser(Locataire loc, DateTime aujourdhui)
    {
        if (loc == null || loc.typeRevision != TypeRevision.Paliers || loc.paliers == null || loc.paliers.Count == 0) return;
        var tries = loc.paliers.OrderBy(Fin).ToList();
        int i = IndexEnVigueur(tries, aujourdhui);
        loc.loyerAnnuel = LoyerAnnuelA(loc, aujourdhui);   // l'historique si l'avenant est à venir
        float avant = loc.historiqueLoyers != null && loc.historiqueLoyers.Count > 0
            ? loc.historiqueLoyers.OrderBy(Fin).Last().loyer : 0f;
        loc.loyerAnnuelPrecedent = i > 0 ? tries[i - 1].loyer : avant;
    }

    /// Avenant (renégociation) à la date `effet` : le loyer en vigueur jusqu'à la veille
    /// passe dans l'historique, et les conditions actuelles ne valent plus qu'à partir
    /// de `effet`. À appeler AVANT d'écrire les nouvelles conditions (on lit ici le
    /// type, les paliers et le loyer encore en place).
    public static void EnregistrerAvenant(Locataire loc, DateTime effet)
    {
        effet = effet.Date;
        var veille = effet.AddDays(-1);
        loc.historiqueLoyers ??= new List<PalierLoyer>();

        // Un historique qui débordait après l'avenant (avenant antidaté) est recoupé.
        loc.historiqueLoyers.RemoveAll(h => Date(h.debutISO, out var hd) && hd.Date >= effet);
        foreach (var h in loc.historiqueLoyers)
            if (Fin(h).Date > veille) h.finISO = Iso(veille);
        var finHisto = loc.historiqueLoyers.Count > 0 ? loc.historiqueLoyers.Max(h => Fin(h).Date) : DateTime.MinValue;
        var apresHisto = finHisto == DateTime.MinValue ? DateTime.MinValue : finHisto.AddDays(1);

        if (loc.typeRevision == TypeRevision.Paliers && loc.paliers != null && loc.paliers.Count > 0)
        {
            var tries = loc.paliers.OrderBy(Fin).ToList();
            foreach (var p in tries)
            {
                if (!Date(p.debutISO, out var pd)) continue;
                var du = pd.Date < apresHisto ? apresHisto : pd.Date;
                var au = Fin(p).Date > veille ? veille : Fin(p).Date;
                if (du <= au) loc.historiqueLoyers.Add(new PalierLoyer { debutISO = Iso(du), finISO = Iso(au), loyer = p.loyer });
            }
            // Tacite prolongation : le dernier palier a continué après sa fin.
            var dernier = tries[tries.Count - 1];
            if (Fin(dernier) < veille)
            {
                var suite = Fin(dernier).Date.AddDays(1);
                if (suite < apresHisto) suite = apresHisto;
                loc.historiqueLoyers.Add(new PalierLoyer { debutISO = Iso(suite), finISO = Iso(veille), loyer = dernier.loyer });
            }
        }
        else if (loc.loyerAnnuel > 0f && apresHisto <= veille)
            loc.historiqueLoyers.Add(new PalierLoyer
            {
                debutISO = apresHisto == DateTime.MinValue ? "" : Iso(apresHisto),
                finISO = Iso(veille),
                loyer = loc.loyerAnnuel,
            });

        loc.debutConditionsISO = Iso(effet);
    }

    /// Revue 17 (avenant de périodicité, lot 14) : avenant de périodicité à la date `effet`, le jour de demande du loyer d'un mois
    /// (décision du 05/11) : la nouvelle périodicité vaut à partir du 1er de ce mois. La périodicité et les mois cochés en vigueur
    /// jusqu'à la veille passent dans l'historique (Locataire.historiquePeriodicites), comme le loyer dans EnregistrerAvenant : un
    /// historique qui débordait (avenant antidaté) est recoupé, une tranche vide (bail pas encore commencé, conditions actuelles
    /// entièrement après la date) est ignorée. À appeler AVANT d'écrire la nouvelle périodicité et les nouveaux mois cochés (on
    /// lit ici ceux encore en place), et APRÈS les refus de l'écran : une date d'effet ne passe jamais avant une période déjà
    /// facturée, sinon des rangs de clés déjà utilisés changeraient de sens (FacturationSuivi.Plan).
    public static void EnregistrerAvenantPeriodicite(Locataire loc, DateTime effet)
    {
        if (loc == null) return;
        var veille = new DateTime(effet.Year, effet.Month, 1).AddDays(-1);
        DateTime FinDe(PeriodiciteAnterieure t) => Date(t.finISO, out var f) ? f.Date : DateTime.MaxValue;

        var garde = new List<PeriodiciteAnterieure>();
        bool couverte = false;   // la veille tombe déjà dans une tranche de l'historique
        foreach (var h in (loc.historiquePeriodicites ?? new List<PeriodiciteAnterieure>())
                          .Where(x => x != null && Date(x.finISO, out _)).OrderBy(x => FinDe(x)).ToList())
        {
            if (couverte) break;                                                   // tranche entièrement après l'avenant : retirée
            if (FinDe(h) >= veille) { h.finISO = Iso(veille); couverte = true; }   // avenant antidaté : tranche recoupée
            garde.Add(h);
        }
        // Les conditions encore en place valaient depuis la fin de la dernière tranche (ou depuis toujours) jusqu'à la veille.
        // Tranche vide : la veille est déjà couverte (elles commençaient après), ou le bail n'avait pas commencé.
        bool avantLeBail = DebutPremierBail(loc, out var bail) && bail.Date > veille;
        // Revue 17 (avenant de périodicité, finitions) : les tranches gardées finissent toutes au plus tard la veille : si le bail
        // commence après, aucune n'a jamais valu. Avant, la tranche recoupée d'un avenant antidaté jusqu'au premier mois du bail
        // restait, et le plan n'était pas celui d'un avenant saisi directement à cette date.
        if (avantLeBail) garde.Clear();
        if (!couverte && !avantLeBail)
            garde.Add(new PeriodiciteAnterieure
            {
                finISO = Iso(veille),
                periodicite = loc.periodiciteLoyer,
                mois = new List<int>(loc.moisFacturationLoyer ?? new List<int>()),
            });
        loc.historiquePeriodicites = garde;
    }

    // ── Avenant de périodicité : la date d'effet (Revue 17, lot 15 ; décision du 05/11) ──

    /// Jusqu'où le loyer est déjà facturé : la fin de la dernière période de loyer touchée, et sa ligne (null : la reprise de
    /// facturation). Touchée = une facture de CIPL (numéro et PDF, même remise en automatique) ou un état posé (« Payé » mis à la
    /// main, en attente d'envoi, bloquée « À faire ») : l'état STOCKÉ, pas l'état calculé, que le menu de la pastille peut défaire.
    /// La reprise de facturation compte : ses périodes ont été facturées hors de CIPL (`repriseISO` : celle choisie à l'écran,
    /// "" = aucune ; null = celle de la fiche). La période se lit dans le plan en place, AVANT le changement ; une ligne émise sous
    /// une autre périodicité que celle de sa période (fiche changée sans date, D4-04) se lit sur elle-même : sa périodicité, à
    /// partir du mois de son échéance (mars émis en mensuel n'est pas le 3e trimestre). Null : rien n'est facturé.
    public static DateTime? FactureJusquAu(Locataire loc, out FactureEtat ligne, string repriseISO = null)
    {
        ligne = null;
        if (loc == null) return null;
        DateTime? fin = null;
        foreach (var l in loc.facturesEtat ?? new List<FactureEtat>())
        {
            if (l == null || !DepartLocataire.LirePeriode(l.key, out int annee, out int periode) || annee < 2 || annee > 9998) continue;   // « loyer-2026-P3 »
            if (string.IsNullOrEmpty(l.statut) && (string.IsNullOrEmpty(l.numero) || string.IsNullOrEmpty(l.pdfPath))) continue;
            DateTime f;
            // ponytail: le mois de l'échéance imprimée tient lieu de début de période ; une échéance retapée dans un autre mois
            // décale d'autant (cas des seules fiches changées sans date). À lire sur le libellé si cela arrive.
            if (!string.IsNullOrEmpty(l.periodicite) && l.periodicite != FacturationSuivi.PeriodiciteDe(loc, annee, periode).ToString()
                && Enum.TryParse(l.periodicite, out Periodicite sienne) && Date(l.echeanceISO, out var ech))
                f = new DateTime(ech.Year, ech.Month, 1).AddMonths(12 / LoyerSummaryUI.NbPeriodes(sienne)).AddDays(-1);
            else FacturationSuivi.PeriodeBornes(loc, annee, periode, out _, out f);
            if (fin == null || f > fin.Value) { fin = f; ligne = l; }
        }
        if (Date(repriseISO ?? loc.repriseFacturationISO, out var reprise))
            for (int y = reprise.Year; y >= reprise.Year - 1; y--)   // l'an d'avant : une période à cheval sur le 1er janvier
                foreach (var p in FacturationSuivi.Plan(loc, y))
                    if (p.debut <= reprise.Date && reprise.Date <= p.fin && (fin == null || p.fin > fin.Value)) { fin = p.fin; ligne = null; }
        return fin;
    }

    /// La date d'effet d'un changement de périodicité est demandée : un loyer est déjà facturé (FactureJusquAu), ou la périodicité
    /// a déjà changé à une date (les périodes d'avant sont figées). Sinon le changement vaut pour tout le bail, sans date, comme avant.
    public static bool DateAvenantRequise(Locataire loc)
        => FactureJusquAu(loc, out _) != null || FinHistoriquePeriodicites(loc) != null;

    static DateTime? FinHistoriquePeriodicites(Locataire loc)
    {
        DateTime? fin = null;
        foreach (var h in loc?.historiquePeriodicites ?? new List<PeriodiciteAnterieure>())
            if (h != null && Date(h.finISO, out var f) && (fin == null || f.Date > fin.Value)) fin = f.Date;
        return fin;
    }

    // Le jour de demande du loyer dans le mois de `mois` (« Loyer demandé le 5 du mois » → le 5 ; le 31 en février : le 28 ou le 29).
    static DateTime JourDemande(DateTime mois, int jour)
        => new DateTime(mois.Year, mois.Month, Math.Min(Math.Max(jour, 1), DateTime.DaysInMonth(mois.Year, mois.Month)));

    /// La date d'effet proposée (décision du 05/11) : le jour de demande du loyer (`jour`) du mois qui suit la dernière période
    /// facturée (à défaut, la dernière périodicité d'avant). `avenantLoyer` : la date d'un avenant de loyer saisi en même temps :
    /// son mois s'il commence le 1er, sinon le mois suivant (la périodicité change par mois entiers), quand c'est plus tard.
    /// Null : pas de date à demander (DateAvenantRequise faux).
    /// Revue 17 (avenant de périodicité, finitions) : `repriseISO` : la reprise choisie à l'écran (voir FactureJusquAu), comme le
    /// refus. Avant, la date proposée lisait la reprise enregistrée : une reprise plus tardive choisie au même écran la faisait refuser.
    public static DateTime? PremiereDateAvenant(Locataire loc, int jour, DateTime? avenantLoyer = null, string repriseISO = null)
    {
        var fin = FactureJusquAu(loc, out _, repriseISO) ?? FinHistoriquePeriodicites(loc);
        if (fin == null) return null;
        var d = JourDemande(fin.Value.AddDays(1), jour);
        if (avenantLoyer is DateTime a)
        {
            var da = JourDemande(a.Day == 1 ? a : a.AddMonths(1), jour);
            if (da > d) d = da;
        }
        return d;
    }

    /// Refus d'une date d'effet (décision du 05/11), ou null. Son mois vient après la dernière période déjà facturée (aucun mois
    /// facturé n'est reproposé, et aucun rang de clé « loyer-{année}-P{n} » déjà utilisé ne change de sens : FacturationSuivi.Plan),
    /// et elle est le jour de demande du loyer de ce mois (`jour` : celui de l'écran, qui va être enregistré). `repriseISO` : la
    /// reprise choisie à l'écran (voir FactureJusquAu). À appeler AVANT toute écriture (EnregistrerAvenantPeriodicite compris).
    public static string RefusAvenantPeriodicite(Locataire loc, DateTime effet, int jour, string repriseISO = null)
    {
        effet = effet.Date;
        if (FactureJusquAu(loc, out var ligne, repriseISO) is DateTime fin && new DateTime(effet.Year, effet.Month, 1) <= fin)
        {
            bool aLaMain = ligne != null && (string.IsNullOrEmpty(ligne.numero) || string.IsNullOrEmpty(ligne.pdfPath));
            // Revue 17 (avenant de périodicité, finitions) : un loyer seulement PRÉPARÉ (numéro et PDF, pas encore parti : en attente
            // d'envoi, bloqué « À faire » ou remis en automatique) repousse aussi la date : « déjà facturé » surprenait, le refus dit
            // quoi comprendre. Pas un loyer parti puis retiré du suivi (retireApresEnvoi) : lui a été reçu.
            bool prepare = ligne != null && !aLaMain && !ligne.retireApresEnvoi
                           && (string.IsNullOrEmpty(ligne.statut) || ligne.statut == "AttenteEnvoi" || ligne.statut == "AFaire");
            return $"Le loyer est déjà facturé jusqu'au {fin:dd/MM/yyyy} ({(ligne != null ? $"« {ligne.libelle} »" : "reprise de facturation")}) : "
                 + $"la nouvelle périodicité peut commencer le {JourDemande(fin.AddDays(1), jour):dd/MM/yyyy} ou après."
                 + (aLaMain ? " Cet état a été mis à la main : pour libérer la ligne, rallumez son bouton « Auto » dans le suivi." : "")
                 + (prepare ? " Ce loyer est déjà préparé : il garde sa période, la nouvelle périodicité commence après lui." : "");
        }
        var attendu = JourDemande(effet, jour);
        return effet == attendu ? null
             : $"La nouvelle périodicité commence le jour où le loyer est demandé (le {(jour <= 1 ? "1er" : jour.ToString())} du mois) : "
             + $"saisissez le {attendu:dd/MM/yyyy}.";
    }

    // ── Loyer selon le chiffre d'affaires ─────────────────────────────────────

    /// Part de l'année civile où le locataire était dans les lieux (du premier bail au
    /// départ), de 0 à 1. Sert à ramener le CA d'une année incomplète à une année pleine.
    public static double FractionPresence(Locataire loc, int annee)
    {
        var janvier = new DateTime(annee, 1, 1);
        var decembre = new DateTime(annee, 12, 31);
        var debut = DebutPremierBail(loc, out var b) && b.Date > janvier ? b.Date : janvier;
        var fin = FinFacturation(loc) < decembre ? FinFacturation(loc) : decembre;
        if (fin < debut) return 0;
        return ((fin - debut).TotalDays + 1) / ((decembre - janvier).TotalDays + 1);
    }

    /// Nouveau loyer annuel HT selon le CA (décisions du 01/10) : % × CA HT de l'année
    /// civile, ramené à une année pleine au prorata des jours de présence, puis borné
    /// au minimum et au maximum révisés. `brut` = le loyer avant bornage (affichage).
    /// 0 si le locataire n'était pas là cette année-là.
    public static float LoyerSelonCA(Locataire loc, int annee, float caHT, float min, float max, out float brut)
    {
        double f = FractionPresence(loc, annee);
        brut = f <= 0 ? 0f : (float)Math.Round(loc.pourcentageCA / 100.0 * caHT / f, 2);
        if (f <= 0) return 0f;
        return (float)Math.Round(Math.Min(Math.Max(brut, min), max), 2);
    }

    /// CA HT déclaré pour l'année, ou null s'il n'a pas été saisi.
    public static float? CA(Locataire loc, int annee)
        => loc?.chiffresAffaires?.FirstOrDefault(c => c.annee == annee)?.montantHT;

    /// Enregistre (ou remplace) le CA HT d'une année.
    public static void PoserCA(Locataire loc, int annee, float montantHT)
    {
        loc.chiffresAffaires ??= new List<ChiffreAffairesAnnuel>();
        var c = loc.chiffresAffaires.FirstOrDefault(x => x.annee == annee);
        if (c == null) loc.chiffresAffaires.Add(new ChiffreAffairesAnnuel { annee = annee, montantHT = montantHT });
        else c.montantHT = montantHT;
    }

    /// « 2 ans 3 mois », « 6 mois », « 45 jours » — durée de `debut` à `fin` inclus.
    public static string DureeTexte(DateTime debut, DateTime fin)
    {
        var apres = fin.Date.AddDays(1);
        int mois = (apres.Year - debut.Year) * 12 + apres.Month - debut.Month;
        if (debut.Date.AddMonths(mois) > apres) mois--;
        if (mois <= 0) return $"{(apres - debut.Date).Days} jours";
        int ans = mois / 12, reste = mois % 12;
        string a = ans > 0 ? $"{ans} an{(ans > 1 ? "s" : "")}" : "";
        string m = reste > 0 ? $"{reste} mois" : "";
        return string.Join(" ", new[] { a, m }.Where(s => s.Length > 0));
    }
}
