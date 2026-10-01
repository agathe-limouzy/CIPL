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

    // ── Loyer d'une date, montant d'une période ───────────────────────────────

    /// Loyer annuel HT en vigueur à cette date : avant un avenant, le loyer de
    /// l'historique ; ensuite le palier qui la contient (le premier avant le début, le
    /// dernier après la fin — tacite prolongation), sinon le loyer courant.
    public static float LoyerAnnuelA(Locataire loc, DateTime date)
    {
        if (loc == null) return 0f;
        if (Date(loc.debutConditionsISO, out var avenant) && date.Date < avenant.Date && loc.historiqueLoyers != null)
            foreach (var h in loc.historiqueLoyers)
                if ((!Date(h.debutISO, out var hd) || hd.Date <= date.Date) && Date(h.finISO, out var hf) && date.Date <= hf.Date)
                    return h.loyer;
        if (loc.typeRevision != TypeRevision.Paliers || loc.paliers == null || loc.paliers.Count == 0)
            return loc.loyerAnnuel;
        var tries = loc.paliers.OrderBy(Fin).ToList();
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
    public static float MontantPeriode(Locataire loc, int year, int periode)
    {
        if (loc == null) return 0f;
        FacturationSuivi.PeriodeBornes(loc, year, periode, out var debut, out var fin);
        var d0 = DebutFacturation(loc);
        if (d0 < debut) d0 = debut;
        var d1 = FinFacturation(loc);
        if (d1 > fin) d1 = fin;
        if (d0 > d1) return 0f;
        int n = LoyerSummaryUI.NbPeriodes(loc.periodiciteLoyer);
        double somme = 0;
        for (var d = d0; d <= d1; d = d.AddDays(1)) somme += LoyerAnnuelA(loc, d);
        return (float)Math.Round(somme / n / ((fin - debut).TotalDays + 1), 2);
    }

    /// Vrai si au moins un jour de la période est facturable.
    public static bool PeriodeFacturable(Locataire loc, int year, int periode)
    {
        FacturationSuivi.PeriodeBornes(loc, year, periode, out var debut, out var fin);
        return fin >= DebutFacturation(loc) && debut <= FinFacturation(loc);
    }

    /// Vrai si la date est le 1er jour d'une période de facturation du locataire.
    public static bool EstDebutPeriode(Locataire loc, DateTime date)
    {
        if (date.Day != 1) return false;
        int n = LoyerSummaryUI.NbPeriodes(loc.periodiciteLoyer);
        for (int p = 1; p <= n; p++)
            if (FacturationSuivi.MoisEcheance(loc, p) == date.Month) return true;
        return false;
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
