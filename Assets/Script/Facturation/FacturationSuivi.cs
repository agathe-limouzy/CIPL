using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Suivi des états de facturation d'un locataire.
/// Planifie les factures attendues d'une année (loyers selon la périodicité,
/// régularisation si provision, révision du dépôt, refacturations) et calcule
/// l'état de chacune : À venir → À faire → Envoyé → Impayé / Payé.
public static class FacturationSuivi
{
    // Cloture / Franchise / HorsBail : statuts transitoires (jamais stockés), lignes
    // grisées et non actionnables — voir EstGrisee.
    // Partiel : affichage seulement (EtatAffiche), une facture due réglée en partie.
    // ARembourser (retour BN17, 04/11) : affichage seulement, nous devons de l'argent au locataire (trop-perçu, avoir).
    public enum Etat { AVenir, AFaire, AttenteEnvoi, Envoye, Impaye, Paye, Cloture, Franchise, HorsBail, Partiel, ARembourser }

    /// Ligne sans facture attendue : période reprise, en franchise ou avant le bail.
    public static bool EstGrisee(Etat e) => e == Etat.Cloture || e == Etat.Franchise || e == Etat.HorsBail;

    /// Clé de la ligne du dépôt de garantie initial (demandé à l'initialisation du dépôt).
    public const string CleDepotInitial = "depot-initial";

    // Formats d'échéance réellement écrits par l'app (ISO). On parse en culture
    // INVARIANTE : `DateTime.TryParse` en culture courante pouvait échouer selon la
    // machine, et un échec faisait silencieusement retomber EtatDe sur « Envoyé »,
    // si bien qu'une facture ne passait JAMAIS Impayé.
    static readonly string[] FormatsEcheance =
        { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy-MM-ddTHH:mm:ss" };

    /// Parse une échéance ISO sans dépendre de la culture courante.
    public static bool TryEcheance(string iso, out DateTime d)
    {
        d = default;
        if (string.IsNullOrWhiteSpace(iso)) return false;
        iso = iso.Trim();
        return DateTime.TryParseExact(iso, FormatsEcheance,
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out d)
            || DateTime.TryParse(iso,
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out d);
    }

    // Le calendrier du loyer, en un seul endroit — le « 15 jours » était écrit ici,
    // dans FacturationAlertes, et une troisième fois en dur dans Lead (22 = 15 + 7).
    // Depuis le 01/11, les délais se règlent : Réglages › Facturation › Calendrier des factures pour l'entreprise
    // (ReglageData), ou sur la fiche du locataire s'il a les siens (Locataire.delaisPropres).
    public const int RappelAvantEnvoiJours    = 7;  // « encore 1 semaine » : rappel de préparer la facture

    /// Jours avant l'échéance où le loyer doit partir (15 par défaut).
    public static int EnvoiAvant(Locataire loc)
        => loc != null && loc.delaisPropres ? loc.envoiAvantJours : ReglageService.Current.envoiAvantJours;
    /// Jours après l'échéance sans règlement où la facture passe « Impayé » (15 par défaut).
    public static int ImpayeApres(Locataire loc)
        => loc != null && loc.delaisPropres ? loc.impayeApresJours : ReglageService.Current.impayeApresJours;
    /// Échéance des autres factures (charges, régularisation, dépôt) : jours après la date (30 par défaut).
    public static int EcheanceAutres(Locataire loc)
        => loc != null && loc.delaisPropres ? loc.echeanceAutresJours : ReglageService.Current.echeanceAutresJours;
    /// Un délai saisi : vide ou illisible = la valeur par défaut, sinon borné à un an.
    public static int Jours(string saisie, int defaut)
        => int.TryParse((saisie ?? "").Trim(), out int v) ? Mathf.Clamp(v, 0, 365) : defaut;

    static readonly string[] MoisNoms =
    { "Janvier","Février","Mars","Avril","Mai","Juin","Juillet","Août","Septembre","Octobre","Novembre","Décembre" };

    // Délai (jours avant l'échéance) à partir duquel une ligne passe « À faire ».
    // Loyer = la date d'envoi (J-15), plus une semaine pour préparer la facture ;
    // Régul / Dépôt = à la date elle-même (pas d'anticipation dans le suivi ;
    // l'anticipation « à préparer » est portée par FacturationAlertes).
    public static int Lead(string type, Locataire loc = null)
    {
        switch (type)
        {
            case "Loyer": return EnvoiAvant(loc) + RappelAvantEnvoiJours;   // 22 j par défaut
            default: return 0;         // Régul / Dépôt / Refac : à la date d'échéance
        }
    }

    // ── Planification des lignes d'une année ────────────────────────────────────

    public static List<FactureEtat> Lignes(Locataire loc, int year)
    {
        var res = new List<FactureEtat>();
        if (loc == null) return res;
        var stored = loc.facturesEtat ?? new List<FactureEtat>();

        // Loyers : une ligne par PÉRIODE de l'année (suit la périodicité du loyer :
        // mensuel → 12 lignes, trimestriel → 4, etc.).
        // Une période entièrement avant le premier bail, après le départ du locataire, ou
        // dans la franchise, n'attend aucune facture : grisée (« Hors bail » /
        // « Franchise »), sauf statut stocké. La fin du bail seule n'arrête rien.
        // La franchise ne porte que sur le loyer : un locataire qui verse des provisions
        // reçoit quand même sa facture (loyer à 0, provisions dues).
        bool avecBail = Loyers.DebutPremierBail(loc, out var debutBail);
        bool avecFranchise = TryEcheance(loc.debutFacturationISO, out var finFranchise) && !Loyers.AppelleProvisions(loc);
        bool avecSortie = TryEcheance(loc.dateSortieISO, out var sortie);
        int n = NbPeriodes(loc.periodiciteLoyer);
        for (int p = 1; p <= n; p++)
        {
            var ech = EcheanceLoyer(loc, year, p);
            var ligne = Fusion(stored, $"loyer-{year}-P{p}", "Loyer",
                $"Loyer {PeriodeLibelle(loc.periodiciteLoyer, p, year)}", ech, 0f);
            if (string.IsNullOrEmpty(ligne.statut))
            {
                PeriodeBornes(loc, year, p, out var debutPeriode, out var finPeriode);
                if (avecBail && finPeriode < debutBail.Date) ligne.statut = "HorsBail";
                else if (avecSortie && debutPeriode > sortie.Date) ligne.statut = "HorsBail";
                else if (avecFranchise && finPeriode < finFranchise.Date) ligne.statut = "Franchise";
            }
            res.Add(ligne);
        }

        // Régularisation des charges de l'année (si provision) — faite en N+1, à la
        // date de chaque liste : la générale, puis chaque liste spécifique datée.
        // Seulement pour une année où le locataire était dans les lieux (bail).
        if (loc.provisionPourCharges && Loyers.AnneeDansLeBail(loc, year))
            foreach (var listeId in ListesCharges.DuLocataire(loc))
            {
                DateTime ech = TryEcheance(ListesCharges.DateRegul(loc, listeId), out var dr)
                    ? new DateTime(year + 1, dr.Month, Mathf.Min(dr.Day, DateTime.DaysInMonth(year + 1, dr.Month)))
                    : new DateTime(year + 1, 1, 31);
                res.Add(Fusion(stored, ListesCharges.Cle(listeId, year), "Regul",
                    ListesCharges.Libelle(listeId, year), ech, 0f));
            }

        // Révision du dépôt de garantie (si la date limite tombe cette année). Plus
        // après le départ — le dépôt sera rendu —, sauf si elle a déjà été touchée.
        if (loc.depotDeGarantie > 0f && TryEcheance(loc.dateRevisionDepotISO, out var dd) && dd.Year == year
            && (!(avecSortie && dd.Date > sortie.Date)
                || stored.Any(x => x.key == $"depot-{year}" && !string.IsNullOrEmpty(x.statut))))
            res.Add(Fusion(stored, $"depot-{year}", "Depot",
                "Révision du dépôt de garantie", dd, 0f));

        // Décompte de sortie (restitution du dépôt), à la date limite de restitution.
        if (loc.depotDeGarantie > 0f && DepartLocataire.EcheanceRestitution(loc, out var er) && er.Year == year)
            res.Add(Fusion(stored, DepartLocataire.CleDecompte, "Depot",
                "Décompte de sortie (restitution du dépôt)", er, 0f));

        // Refacturations enregistrées de l'année (créées à la demande), et dépôt de
        // garantie initial (créé à l'initialisation du dépôt).
        foreach (var r in stored)
        {
            if (r.type != "Refac" && r.key != CleDepotInitial) continue;
            int ry = TryEcheance(r.echeanceISO, out var re) ? re.Year : year;
            if (ry != year || res.Any(x => x.key == r.key)) continue;
            // Une refacturation de plusieurs charges est UNE facture (même PDF) : une
            // seule ligne, au total de ses charges (retour du 01/10). Les lignes par
            // charge restent stockées : la régul et les paiements en ont besoin.
            var meme = r.type == "Refac" && !string.IsNullOrEmpty(r.pdfPath)
                ? res.FirstOrDefault(x => x.type == "Refac" && x.pdfPath == r.pdfPath) : null;
            if (meme != null) meme.montant += r.montant;
            else res.Add(CopieDe(r));
        }

        // Factures DÉJÀ ÉMISES de l'année qui ne sont plus planifiées : régularisation (liste supprimée,
        // date ou provision retirée, locataire limité à une autre liste), loyer d'une ancienne périodicité,
        // révision du dépôt dont « Réviser » a déplacé l'année (revue 16c, D4-05)… une facture partie ne
        // disparaît pas du suivi pour autant. Les refacturations ont leur propre bloc ci-dessus.
        foreach (var r in stored)
        {
            if (r.type == "Refac" || res.Any(x => x.key == r.key)) continue;
            int annee = r.type == "Regul" ? (ListesCharges.LireCle(r.key, out int a, out _) ? a : -1)
                      : TryEcheance(r.echeanceISO, out var re) ? re.Year : -1;
            if (annee != year) continue;
            var e = EtatDe(r, loc);
            if (e == Etat.AttenteEnvoi || e == Etat.Envoye || e == Etat.Impaye || e == Etat.Paye)
                res.Add(CopieDe(r));
        }

        // Reprise de passif : toute période d'échéance ≤ date de reprise et sans
        // statut actif stocké est « Clôturée » (historique, non suivie).
        if (TryEcheance(loc.repriseFacturationISO, out var reprise))
            foreach (var l in res)
                if (string.IsNullOrEmpty(l.statut)
                    && TryEcheance(l.echeanceISO, out var e) && e.Date <= reprise.Date)
                    l.statut = "Cloture";

        return res.OrderBy(x => TryEcheance(x.echeanceISO, out var e) ? e : DateTime.MaxValue).ToList();
    }

    /// Lignes montrées dans le suivi de la fiche. Tant que le locataire n'est pas
    /// complet (parcours Général → Bail → Loyer → Dépôt), rien n'est planifié : un
    /// suivi rempli de loyers « À faire » n'aurait aucun sens avant que le loyer
    /// existe. Seules restent les factures réellement touchées (émises, payées,
    /// forcées) — celles d'un locataire en cours de réinitialisation, par exemple.
    public static List<FactureEtat> LignesAffichees(Locataire loc, int year)
    {
        var lignes = Lignes(loc, year);
        if (loc == null || ParcoursLocataire.Prochaine(loc) == ParcoursLocataire.Etape.Termine) return lignes;
        var stored = loc.facturesEtat ?? new List<FactureEtat>();
        return lignes.Where(l => stored.Any(s => s.key == l.key && !string.IsNullOrEmpty(s.statut))).ToList();
    }

    // Ligne planifiée : reprend l'enregistrement s'il existe, sinon une ligne « vierge ».
    static FactureEtat Fusion(List<FactureEtat> stored, string key, string type, string libelle, DateTime ech, float montant)
    {
        var rec = stored.FirstOrDefault(x => x.key == key);
        if (rec != null)
        {
            // On garde l'état/envoi stocké et on rafraîchit le libellé (la périodicité
            // a pu changer). Le suffixe « corrigée(X) » est réappliqué depuis le compteur.
            var c = CopieDe(rec);
            // Facture ÉMISE (Envoyé / En attente d'envoi / Impayé / Payé) : on garde SON
            // échéance réelle (celle imprimée sur la facture) → l'état (Impayé à
            // échéance+15) est cohérent avec la colonne « Créances » (qui lit le stocké).
            // Ligne planifiée non émise : on aligne l'échéance sur la période courante.
            bool emise = rec.statut == "Envoye" || rec.statut == "AttenteEnvoi"
                      || rec.statut == "Impaye" || rec.statut == "Paye";
            // Revue 17 (D4-04) : une facture émise garde SON libellé, le document imprimé fait foi (après un changement de
            // périodicité, « Loyer Mars 2026 » devenait « Loyer 1er trimestre 2026 ») ; une ligne non émise suit la période.
            c.libelle = emise && !string.IsNullOrEmpty(rec.libelle) ? rec.libelle
                      : libelle + (rec.corrections > 0 ? $" — corrigée({rec.corrections})" : "");
            if (!emise || string.IsNullOrEmpty(rec.echeanceISO))
                c.echeanceISO = ech.ToString("yyyy-MM-dd");
            return c;
        }
        return new FactureEtat { key = key, type = type, libelle = libelle,
            echeanceISO = ech.ToString("yyyy-MM-dd"), statut = "", montant = montant };
    }

    static FactureEtat CopieDe(FactureEtat r) => new FactureEtat
    {
        key = r.key, type = r.type, libelle = r.libelle, echeanceISO = r.echeanceISO,
        statut = r.statut, numero = r.numero, pdfPath = r.pdfPath, dateEnvoiISO = r.dateEnvoiISO,
        montant = r.montant, tauxTva = r.tauxTva, loyerHT = r.loyerHT, ribId = r.ribId, ribNom = r.ribNom, corrections = r.corrections,
        dernierRappelISO = r.dernierRappelISO,
        // Revue 17 (D6-07) : les provisions imprimées (la correction les relit sur la ligne affichée, cette copie).
        provisionsConnues = r.provisionsConnues, provision = r.provision, periodicite = r.periodicite,
        provisionsListes = r.provisionsListes != null ? new List<MontantListe>(r.provisionsListes) : new List<MontantListe>(),
        versements = r.versements != null ? new List<Versement>(r.versements) : new List<Versement>(),
        // Revue 16c (D5-03) : sans eux, la pastille de la plateforme (FactureElectronique.Pastille) ne s'affichait jamais.
        plateformeDonnees = r.plateformeDonnees, plateformeId = r.plateformeId, plateformeEtat = r.plateformeEtat,
        plateformeErreur = r.plateformeErreur, encaisseeDeclaree = r.encaisseeDeclaree,
        encaisseeMontant = r.encaisseeMontant, encaisseeDateISO = r.encaisseeDateISO,
        // Transfert (01/11, BG7) : oubliés ici, la pastille « transmise par SCI X » ne s'affichait jamais (la ligne affichée
        // est cette copie, pas la ligne stockée). Le test Suivi_la_copie_affichee_porte_tous_les_champs veille.
        plateformeOrigine = r.plateformeOrigine, plateformeOrigineId = r.plateformeOrigineId
    };

    // ── État d'une ligne ────────────────────────────────────────────────────────

    public static Etat EtatDe(FactureEtat f, Locataire loc = null)
    {
        string s = f.statut ?? "";
        DateTime today = DateTime.Today;
        bool hasEch = TryEcheance(f.echeanceISO, out var ech);

        // Une facture ÉMISE sans échéance exploitable ne peut pas être évaluée :
        // elle resterait « Envoyé » indéfiniment et sortirait des créances. On le
        // signale au lieu de le taire (l'écriture garantit désormais une échéance,
        // ce cas ne concerne donc que d'anciens enregistrements).
        if (!hasEch && (s == "Envoye" || s == "AttenteEnvoi"))
            Debug.LogWarning($"[FacturationSuivi] Ligne émise sans échéance exploitable (key='{f.key}', " +
                             $"echeanceISO='{f.echeanceISO}') : passage en Impayé impossible.");

        if (s == "Paye") return Etat.Paye;
        if (s == "Impaye") return Etat.Impaye;
        if (s == "Cloture") return Etat.Cloture;   // période reprise (historique)
        if (s == "Franchise") return Etat.Franchise;
        if (s == "HorsBail") return Etat.HorsBail;

        // En attente d'envoi : le PDF existe, rien n'est parti. Cet état ne se
        // périme pas — il ne devient « Envoyé » que par un envoi réel ou un forçage
        // manuel, et surtout PAS « Impayé » : on ne peut pas reprocher un impayé à
        // qui n'a jamais reçu sa facture. L'oubli est rattrapé par l'alerte, qui
        // reste allumée tant que la facture n'est pas partie (voir DejaTraite).
        //
        // SAUF sans PDF : « en attente d'envoi » n'aurait alors rien à envoyer. La
        // ligne dormirait en ambre, absente des récapitulatifs comme des alertes — un
        // piège silencieux, constaté sur une fiche réelle. Elle retombe donc dans le
        // cycle normal (À faire / À venir) : il y a bien une facture à produire.
        if (s == "AttenteEnvoi" && !string.IsNullOrEmpty(f.pdfPath)) return Etat.AttenteEnvoi;
        if (s == "AttenteEnvoi")
            return hasEch && today >= ech.AddDays(-Lead(f.type, loc)) ? Etat.AFaire : Etat.AVenir;

        if (s == "Envoye")
        {
            // Envoyé → Impayé N j après l'échéance (ImpayeApres) si non réglé. Jamais pour un avoir
            // (montant négatif) : c'est nous qui devons, il n'y a rien à réclamer — il
            // reste « Envoyé » jusqu'au remboursement, marqué « Payé » à la main.
            // ponytail: ligne par ligne — une régul regroupée aux parts de signes mêlés
            // garderait sa part négative « Envoyé » ; juger sur MemeFacture si ça arrive.
            // Retour BN17 (04/11) : ni une facture réglée au-delà de son montant (trop-perçu à rembourser).
            if (hasEch && today >= ech.AddDays(ImpayeApres(loc)) && f.montant >= 0f && !RegleeAuDela(f, loc))
                return Etat.Impaye;
            return Etat.Envoye;
        }

        if (s == "AFaire") return Etat.AFaire;

        // Pas d'enregistrement : À faire quand on approche l'échéance, sinon À venir.
        if (hasEch && today >= ech.AddDays(-Lead(f.type, loc)))
            return Etat.AFaire;
        return Etat.AVenir;
    }

    public static string EtatLibelle(Etat e)
    {
        switch (e)
        {
            case Etat.AVenir: return "À venir";
            case Etat.AFaire: return "À faire";
            case Etat.AttenteEnvoi: return "En attente d'envoi";
            case Etat.Envoye: return "Envoyé";
            case Etat.Impaye: return "Impayé";
            case Etat.Paye:   return "Payé";
            case Etat.Cloture: return "Clôturé";
            case Etat.Franchise: return "Franchise";
            case Etat.HorsBail: return "Hors bail";
            case Etat.Partiel: return "Partiel";
            case Etat.ARembourser: return "À rembourser";
        }
        return "";
    }

    // ── Paiements partiels (08/10) ──────────────────────────────────────────────
    // Les versements d'une facture vivent sur sa ligne « porteuse » : la première de
    // MemeFacture (une régul regroupée a plusieurs lignes pour un seul PDF), sinon la
    // ligne elle-même. Tout réglé → la facture passe « Payé » (StatutApresVersements).

    /// Somme des versements portés par une ligne.
    public static float Paye(FactureEtat f) => f?.versements?.Sum(v => v.montant) ?? 0f;

    /// Ligne stockée qui porte les versements de la facture `key`.
    public static FactureEtat Porteuse(Locataire loc, string key)
    {
        var meme = MemeFacture(loc, key);
        return meme.Count > 0 ? meme[0] : loc?.facturesEtat?.FirstOrDefault(x => x.key == key);
    }

    /// Montant de la facture (toutes ses lignes) et ce qui en a été versé.
    public static void Solde(Locataire loc, string key, out float total, out float paye)
    {
        var meme = MemeFacture(loc, key);
        var rec = Porteuse(loc, key);
        total = meme.Count > 0 ? meme.Sum(x => x.montant) : rec?.montant ?? 0f;
        paye = Paye(rec);
    }

    /// État affiché : « Partiel » dès qu'une facture due (envoyée ou impayée) est réglée en
    /// partie (retour du 08/10). L'état réel reste `EtatDe` : une partielle échue compte
    /// toujours dans les impayés (Créances, filtre, rappel d'échéance).
    public static Etat EtatAffiche(Locataire loc, FactureEtat f)
    {
        var e = EtatDe(f, loc);
        if (e != Etat.Envoye && e != Etat.Impaye && e != Etat.Paye) return e;
        // Retour BN17 (décisions du 04/11) : nous devons de l'argent au locataire (trop-perçu, avoir pas encore remboursé).
        if (ARembourser(loc, f.key) > 0f) return Etat.ARembourser;
        if (e == Etat.Paye) return e;
        Solde(loc, f.key, out _, out float paye);
        return paye > 0f ? Etat.Partiel : e;
    }

    /// Statut après une saisie de versements : tout réglé → « Payé » ; une facture payée
    /// dont on retire un versement redevient « Envoyé » (puis Impayé si l'échéance est
    /// passée) ; sinon inchangé — un « Payé » mis à la main sans versement reste payé.
    public static string StatutApresVersements(string statut, float total, float payeAvant, float payeApres)
    {
        // Retour BN17 (04/11) : réglée au-delà de son montant : le trop-perçu est à rembourser avant « Payé ».
        if (total > 0f && payeApres > total + 0.005f) return statut == "Paye" ? "Envoye" : statut;
        if (total > 0f && payeApres >= total - 0.005f) return "Paye";
        if (statut == "Paye" && payeApres < payeAvant - 0.005f) return "Envoye";
        return statut;
    }

    /// Contrôle d'un nouveau versement : message d'erreur, ou null s'il est accepté.
    public static string VerifierVersement(float montant, float reste)
    {
        if (montant <= 0f) return "Montant du versement : un nombre supérieur à 0.";
        if (montant > reste + 0.005f)
            return $"Le versement dépasse le reste à payer ({reste.ToString("#,##0.00", FacturePdfService.FrCulture)} €).";
        return null;
    }

    // ── Écriture ────────────────────────────────────────────────────────────────

    /// Enregistre une facture générée. `envoyeReellement` = le document est
    /// effectivement parti (email accepté par le serveur) ; sinon la ligne reste
    /// « En attente d'envoi », PDF prêt mais rien d'expédié.
    public static void MarquerEnvoye(Locataire loc, string key, string type, string libelle,
        string echeanceISO, string numero, string pdfPath, float montant, string ribId, string ribNom,
        bool envoyeReellement)
    {
        if (loc == null || string.IsNullOrEmpty(key)) return;
        if (loc.facturesEtat == null) loc.facturesEtat = new List<FactureEtat>();
        var rec = loc.facturesEtat.FirstOrDefault(x => x.key == key);
        if (rec == null) { rec = new FactureEtat { key = key }; loc.facturesEtat.Add(rec); }
        // Une ligne émise DOIT porter une échéance exploitable : sans elle, elle ne
        // pourrait jamais passer Impayé et disparaîtrait des créances.
        if (!TryEcheance(echeanceISO, out _))
        {
            Debug.LogWarning($"[FacturationSuivi] Échéance absente ou illisible pour '{key}' " +
                             $"('{echeanceISO}') — repli sur la date du jour pour garder la ligne suivie.");
            echeanceISO = DateTime.Today.ToString("yyyy-MM-dd");
        }

        rec.type = type; rec.libelle = libelle; rec.echeanceISO = echeanceISO;
        rec.numero = numero; rec.montant = montant;
        // Chemin RELATIF a la racine : un chemin absolu cassait des que la
        // sauvegarde changeait de dossier, de disque ou de machine (les PDF
        // suivaient le deplacement, pas les chemins memorises).
        rec.pdfPath = DossiersDonnees.VersRelatif(pdfPath);
        rec.ribId = ribId; rec.ribNom = ribNom;
        rec.dateEnvoiISO = DateTime.Today.ToString("yyyy-MM-dd");

        // Émission neuve ou remplacement d'une facture jamais partie : il n'y a aucune
        // correction à annoncer. Sans cette remise à zéro, le libellé gardait un
        // « — corrigée(5) » réappliqué par Fusion depuis le compteur, alors que la
        // nouvelle version est la seule que le locataire verra jamais.
        // (Une vraie correction passe par MarquerCorrige, qui incrémente le compteur.)
        rec.corrections = 0;

        // Une ligne « en attente d'envoi » sans PDF n'a rien à envoyer : elle serait
        // ambre à l'écran, muette dans les récapitulatifs, et ne partirait jamais.
        // On le signale plutôt que de l'écrire en silence.
        if (string.IsNullOrEmpty(rec.pdfPath))
            Debug.LogWarning($"[FacturationSuivi] Facture enregistrée sans PDF (key='{key}') : "
                             + "la ligne ne pourra pas être envoyée.");

        // « Envoyé » veut dire envoyé. Le statut suit le FAIT, plus le calendrier :
        // une facture dont le PDF est généré mais que rien n'a fait partir reste « En
        // attente d'envoi », aussi longtemps qu'il le faut.
        //
        // Avant, le statut se déduisait de la date (préparée à plus de 15 j → « en
        // attente », sinon « envoyée ») et `EtatDe` basculait ensuite tout seul à
        // J-15. Le suivi affichait donc « Envoyé » pour des factures qui n'étaient
        // jamais parties — y compris quand l'envoi email n'était même pas activé.
        // L'utilisatrice garde la main : le menu de la pastille permet de forcer
        // « Envoyé » quand la facture est partie autrement (Pennylane, courrier).
        rec.statut = envoyeReellement ? "Envoye" : "AttenteEnvoi";
    }

    // Vrai si la ligne `key` correspond à une facture DÉJÀ ÉMISE (PDF généré, numéro
    // consommé) → une nouvelle génération sera traitée comme une correction (diagramme).
    //
    // Les quatre états « émise » sont les mêmes que dans `DejaTraite`. Ne tester que
    // Envoyé/Impayé laissait passer deux cas vérifiés en exécution :
    //   · un loyer préparé plus de 15 j avant l'échéance est « En attente d'envoi » —
    //     c'est le cas NORMAL du panneau Loyer — et un second clic consommait alors une
    //     nouvelle séquence tout en écrasant le PDF déjà généré ;
    //   · une facture marquée « Payé » puis re-générée repartait sur un numéro neuf,
    //     donc deux numéros pour une seule période.
    // Le paiement ne « dé-émet » pas une facture : un numéro consommé le reste.
    public static bool EstDejaEmise(Locataire loc, string key, out FactureEtat rec)
    {
        rec = loc?.facturesEtat?.FirstOrDefault(x => x.key == key);
        if (rec == null) return false;
        var e = EtatDe(rec, loc);
        bool emise = e == Etat.AttenteEnvoi || e == Etat.Envoye
                  || e == Etat.Impaye || e == Etat.Paye;
        return emise && !string.IsNullOrEmpty(rec.pdfPath);
    }

    /// Chemin exploitable du PDF d'une ligne, cherché sous les noms ACTUELS du bâtiment et
    /// du locataire (un renommage déplace le dossier sans réécrire `pdfPath`), à défaut le
    /// chemin stocké (relatif aujourd'hui, absolu pour les enregistrements antérieurs).
    /// À utiliser PARTOUT plutôt que `f.pdfPath` brut — celui-ci n'est plus ouvrable tel quel.
    public static string CheminPdf(FactureEtat f, string nomBatiment, string nomLocataire)
        => f == null ? null : DossiersDonnees.PdfFacture(nomBatiment, nomLocataire, f.pdfPath);

    // Correction d'une facture déjà émise : incrémente le compteur, suffixe le libellé
    // « corrigée(X) », met à jour le PDF/montant. Conserve le numéro ET le statut : un
    // loyer corrigé avant sa date d'envoi reste « En attente d'envoi » (le passer à
    // « Envoyé » afficherait un envoi qui n'a pas eu lieu), et une facture payée reste
    // payée. Seule une ligne sans statut devient « Envoyé ».
    // Renvoie X (le nouveau nombre de corrections).
    public static int MarquerCorrige(Locataire loc, string key, string libelleBase, string pdfPath, float montant,
                                     string echeanceISO = null, string ribId = null, string ribNom = null)
    {
        var rec = loc?.facturesEtat?.FirstOrDefault(x => x.key == key);
        if (rec == null) return 0;
        rec.corrections = Mathf.Max(0, rec.corrections) + 1;

        string bas = libelleBase;
        if (string.IsNullOrEmpty(bas))
        {
            bas = rec.libelle ?? "";
            int i = bas.IndexOf(" — corrigée(", StringComparison.Ordinal);
            if (i >= 0) bas = bas.Substring(0, i);
        }
        rec.libelle = bas + $" — corrigée({rec.corrections})";
        rec.pdfPath = DossiersDonnees.VersRelatif(pdfPath);
        if (montant != 0f) rec.montant = montant;   // 0 = non fourni ; négatif = avoir
        rec.dateEnvoiISO = DateTime.Today.ToString("yyyy-MM-dd");
        // Revue 16c (D4-03) : l'échéance et le RIB imprimés sur la version corrigée suivent la ligne (« Impayé »
        // calculé sur la bonne date, Créances sur le bon compte).
        if (!string.IsNullOrEmpty(echeanceISO)) rec.echeanceISO = echeanceISO;
        if (ribId != null) { rec.ribId = ribId; rec.ribNom = ribNom; }
        // Corriger ne fait rien partir : une ligne sans statut est prête, pas envoyée.
        if (string.IsNullOrEmpty(rec.statut)) rec.statut = "AttenteEnvoi";
        return rec.corrections;
    }

    /// Revue 17 (D4-03, décision du 04/11) : une facture payée corrigée à un autre montant. À la hausse, l'écart redevient dû
    /// (« Envoyé », affichée « Partiel » avec ce qui a été réglé : Créances, rappel) ; à la baisse, le trop-perçu est à
    /// rembourser (« Envoyé », affichée « À rembourser », décision révisée du 04/11, BN17) : « Remboursé… », puis « Payé ».
    /// Au même montant (correction de forme), rien ne change. Appelée par les
    /// formulaires une fois TOUTES les lignes de la facture corrigées, avec le total d'avant la correction.
    public static void ApresCorrection(Locataire loc, string key, float totalAvant)
    {
        var porteuse = Porteuse(loc, key);
        if (porteuse == null || totalAvant <= 0.005f) return;
        Solde(loc, key, out float total, out float paye);
        if (total <= 0.005f || Math.Abs(total - totalAvant) <= 0.005f) return;
        var meme = MemeFacture(loc, key);
        if (meme.Count == 0) meme.Add(porteuse);
        if (porteuse.statut == "Paye")
        {
            // « Payé » à la main, sans versement ou avec un acompte seulement (retour BN9) : ce qui était dû avant la correction
            // a été réglé ; le reste est noté en versement pour que le reste dû, ou le trop-perçu, se lise.
            if (paye < totalAvant - 0.005f)
            {
                (porteuse.versements ??= new List<Versement>()).Add(new Versement { dateISO = DateTime.Today.ToString("yyyy-MM-dd"), montant = totalAvant - paye });
                paye = totalAvant;
            }
            // Retour BN17 (décision du 04/11) : un écart dans un sens ou dans l'autre rouvre la facture : à la hausse le reste
            // est dû, à la baisse le trop-perçu est à rembourser avant qu'elle repasse « Payé ».
            if (Math.Abs(paye - total) > 0.005f) foreach (var l in meme) l.statut = "Envoye";
        }
        // Retour BN9 : réglée en partie (« Partiel »), puis corrigée à ce qui a été versé : elle est payée ; au-dessous, le
        // trop-perçu est à rembourser (BN17) : « Envoyé », jamais « Impayé ».
        else if ((porteuse.statut == "Envoye" || porteuse.statut == "Impaye") && paye > 0.005f)
        {
            if (Math.Abs(paye - total) <= 0.005f) foreach (var l in meme) l.statut = "Paye";
            else if (paye > total) foreach (var l in meme) l.statut = "Envoye";
        }
    }

    /// Retour BN17 (décisions du 04/11) : ce que nous devons rendre au locataire sur la facture `key` : le trop-perçu d'une
    /// facture payée puis corrigée à la baisse, ou ce qui reste d'un avoir (facture négative) pas encore remboursé. 0 sinon.
    /// Un avoir marqué « Payé » à la main est remboursé (comme avant).
    public static float ARembourser(Locataire loc, string key)
    {
        var porteuse = Porteuse(loc, key);
        if (porteuse == null) return 0f;
        Solde(loc, key, out float total, out float paye);
        if (total < 0f && porteuse.statut == "Paye") return 0f;
        float r = paye - total;
        return r > 0.005f ? r : 0f;
    }

    // Retour BN17 : réglée au-delà de son montant (payée, puis corrigée à la baisse) : c'est nous qui devons, rien à réclamer.
    static bool RegleeAuDela(FactureEtat f, Locataire loc)
    {
        if (loc == null) return f.montant > 0f && Paye(f) > f.montant + 0.005f;
        Solde(loc, f.key, out float total, out float paye);
        return total > 0f && paye > total + 0.005f;
    }

    /// Retour BN17 (décisions du 04/11) : « Remboursé… » : un versement négatif, daté, de ce que nous rendons au locataire ;
    /// tout rendu → la facture passe « Payé » (toutes ses lignes, et les charges qu'elle couvre). Message de refus, ou null.
    public static string EnregistrerRemboursement(Locataire loc, FactureEtat ligne, float montant, DateTime date, Batiment bat = null)
    {
        var rec = Porteuse(loc, ligne?.key);
        if (rec == null) return "Facture introuvable.";
        float du = ARembourser(loc, ligne.key);
        if (montant <= 0f) return "Montant du remboursement : un nombre supérieur à 0.";
        if (montant > du + 0.005f)
            return $"Le remboursement dépasse ce qui est à rendre ({du.ToString("#,##0.00", FacturePdfService.FrCulture)} €).";
        (rec.versements ??= new List<Versement>()).Add(new Versement { dateISO = date.ToString("yyyy-MM-dd"), montant = -montant });
        rec.versements = rec.versements.OrderBy(x => x.dateISO).ToList();
        if (ARembourser(loc, ligne.key) <= 0.005f && rec.statut != "Paye") SetStatut(loc, ligne, "Paye", bat);
        return null;
    }

    // Mémorise la date d'un rappel d'échéance envoyé (sur l'enregistrement stocké,
    // pas la copie affichée). Sans effet si la ligne n'a pas d'enregistrement.
    public static void MarquerRappel(Locataire loc, string key)
    {
        var rec = loc?.facturesEtat?.FirstOrDefault(x => x.key == key);
        if (rec != null) rec.dernierRappelISO = DateTime.Today.ToString("yyyy-MM-dd");
    }

    // Force manuellement l'état d'une ligne (statut = "" pour revenir à l'auto À venir/À faire).
    /// Change le statut d'une ligne de suivi et **répercute sur les charges** qu'elle
    /// couvre : marquer une régularisation ou une refacturation « payé » encaisse ses
    /// charges, l'inverse les remet en attente.
    ///
    /// `bat` est optionnel pour ne casser aucun appel, mais sans lui la répercussion
    /// n'a pas lieu — les charges vivent sur le bâtiment. Les deux vues du suivi le
    /// passent. La règle est ici et non chez elles : dupliquée, elle finirait par
    /// n'exister que dans une seule (c'est l'histoire de H2).
    public static void SetStatut(Locataire loc, FactureEtat ligne, string statut, Batiment bat = null)
    {
        if (loc == null || ligne == null) return;
        if (loc.facturesEtat == null) loc.facturesEtat = new List<FactureEtat>();
        var rec = loc.facturesEtat.FirstOrDefault(x => x.key == ligne.key);
        if (rec == null)
        {
            rec = new FactureEtat { key = ligne.key, type = ligne.type, libelle = ligne.libelle,
                echeanceISO = ligne.echeanceISO, montant = ligne.montant };
            loc.facturesEtat.Add(rec);
        }
        // Une régularisation regroupant plusieurs listes est UNE facture portée par
        // plusieurs lignes : elles changent d'état ensemble.
        foreach (var r in MemeFacture(loc, rec.key).DefaultIfEmpty(rec))
        {
            r.statut = statut ?? "";
            if (statut == "Envoye" && string.IsNullOrEmpty(r.dateEnvoiISO))
                r.dateEnvoiISO = DateTime.Today.ToString("yyyy-MM-dd");
            RepercuterSurCharges(loc, bat, r, EtatDe(r, loc));
        }
    }

    /// Une facture portée par plusieurs lignes (même PDF : régul regroupée, refacturation
    /// de plusieurs charges) couvre des lignes jamais émises, ou toutes celles d'UNE
    /// facture existante (sa correction, éventuellement élargie). Mélanger deux factures
    /// existantes referait payer ce qui l'est déjà ; en corriger une en retirant une de
    /// ses lignes laisserait celle-ci pointer sur l'ancien document.
    /// Vrai si c'est possible : `cle` = la ligne qui porte la facture. Faux : `oubliees`
    /// = les lignes retirées, ou null si deux factures existantes sont mélangées.
    /// Une facture déjà payée ne reçoit plus de ligne (revue 16c, D4-12 ; décision du 31/10) : `cle` est sa ligne
    /// porteuse, `cles` ce que l'on veut y mettre ; vrai si l'une des clés n'y est pas encore et que la facture est payée.
    public static bool ElargitUneFacturePayee(Locataire loc, string cle, IList<string> cles)
    {
        if (cle == null || !EstDejaEmise(loc, cle, out var rec) || EtatDe(rec, loc) != Etat.Paye) return false;
        return cles.Any(k => !EstDejaEmise(loc, k, out _));
    }

    public static bool Regroupable(Locataire loc, IList<string> cles, out string cle, out List<FactureEtat> oubliees)
    {
        cle = cles.Count > 0 ? cles[0] : null;
        oubliees = new List<FactureEtat>();
        var emises = new List<FactureEtat>();
        foreach (var k in cles) if (EstDejaEmise(loc, k, out var r)) emises.Add(r);
        if (emises.Count == 0) return true;   // facture neuve
        if (emises.Select(r => r.pdfPath).Distinct().Count() > 1) { oubliees = null; return false; }
        cle = emises[0].key;                  // la facture existante : c'est une correction
        oubliees = MemeFacture(loc, cle).Where(r => !cles.Contains(r.key)).ToList();
        return oubliees.Count == 0;
    }

    /// Factures déjà émises du locataire, pour « Avoir sur la facture n° … » : une
    /// entrée par numéro (une facture regroupée a plusieurs lignes), hors avoirs, la plus
    /// récente d'abord. Premier choix : aucune (id vide).
    public static void FacturesOrigine(Locataire loc, out List<string> labels, out List<string> ids)
    {
        labels = new List<string> { "— (aucune)" };
        ids = new List<string> { "" };
        if (loc?.facturesEtat == null) return;
        var parNumero = loc.facturesEtat
            .Where(r => !string.IsNullOrEmpty(r.numero) && EstDejaEmise(loc, r.key, out _))
            .GroupBy(r => r.numero)
            .Where(g => g.Sum(r => r.montant) > 0f)
            .OrderByDescending(g => g.Max(r => r.dateEnvoiISO ?? ""));
        foreach (var g in parNumero)
        {
            int n = g.Count();
            labels.Add($"{g.Key} · {g.First().libelle}{(n > 1 ? $" (+{n - 1})" : "")}");
            ids.Add(g.Key);
        }
    }

    /// Lignes portant la même facture que la ligne `key` (même PDF) — plusieurs pour
    /// une régularisation regroupée, une seule sinon. Vide si la ligne n'a pas de PDF.
    public static List<FactureEtat> MemeFacture(Locataire loc, string key)
    {
        var rec = loc?.facturesEtat?.FirstOrDefault(x => x.key == key);
        if (rec == null || string.IsNullOrEmpty(rec.pdfPath)) return new List<FactureEtat>();
        return loc.facturesEtat.Where(x => x.pdfPath == rec.pdfPath).ToList();
    }

    /// Les charges couvertes par cette facture suivent son sort.
    ///
    /// Payée → le virement est arrivé, elles sont encaissées. Tout autre état → la
    /// facture est partie mais pas réglée : elles repassent « en attente », ce qui
    /// permet de corriger une validation faite par erreur sans rien perdre.
    ///
    /// Seule la part de CE locataire change : les autres locataires d'une charge
    /// partagée ont leur propre facture et leur propre paiement. Sa mise sur facture
    /// n'est jamais effacée : la charge ne redevient pas sélectionnable pour lui.
    static void RepercuterSurCharges(Locataire loc, Batiment bat, FactureEtat ligne, Etat etat)
    {
        if (bat?.charges == null || loc == null || ligne == null || string.IsNullOrEmpty(ligne.key)) return;

        bool payee = etat == Etat.Paye;

        // Refacturation : la clé porte l'id de la charge, le lien est direct.
        if (ligne.key.StartsWith("refac-"))
        {
            string chargeId = ligne.key.Substring("refac-".Length);
            bat.charges.FirstOrDefault(x => x != null && x.id == chargeId)?.MarquerPayee(loc.id, payee);
            return;
        }

        // Régularisation : la clé porte l'année. On ne touche qu'aux charges déjà
        // portées sur une facture de ce locataire — une charge qu'il s'est vu
        // refacturer à part a sa propre ligne, et n'est pas concernée ici.
        if (ListesCharges.LireCle(ligne.key, out int annee, out string listeId))
        {
            foreach (var c in bat.charges)
            {
                if (c == null || TryYear(c.dateISO) != annee || !ListesCharges.DeLaListe(c, listeId)) continue;
                if (loc.facturesEtat?.Any(x => x.key == "refac-" + c.id) == true) continue;
                c.MarquerPayee(loc.id, payee);
            }
        }
    }

    /// Année d'une date ISO, 0 si illisible.
    static int TryYear(string iso) => DateTime.TryParse(iso, out var d) ? d.Year : 0;

    // ── Périodes / libellés (alignés sur FactureLoyerPanel) ─────────────────────

    public static int NbPeriodes(Periodicite p) => LoyerSummaryUI.NbPeriodes(p);

    /// Une créance = une facture due (Envoyé ou Impayé) d'un locataire d'un bâtiment.
    public struct Due { public BatimentPrefab bp; public Locataire loc; public FactureEtat rec; public Etat etat; }

    /// Toutes les factures dues (en attente + impayées) de tous les bâtiments donnés.
    public static List<Due> Dues(IEnumerable<BatimentPrefab> bps)
    {
        var res = new List<Due>();
        if (bps == null) return res;
        foreach (var bp in bps)
        {
            if (bp == null || bp.listLocataire == null) continue;
            foreach (var loc in bp.listLocataire)
                foreach (var r in DuesDe(loc))
                    res.Add(new Due { bp = bp, loc = loc, rec = r, etat = EtatDe(r, loc) });
        }
        return res;
    }

    /// Factures dues (Envoyé ou Impayé) d'un locataire. Une facture portée par plusieurs
    /// lignes (même PDF : refacturation de plusieurs charges, régul regroupée) est UNE
    /// créance, à son total. Ce sont des copies : on ne fait que les lire.
    /// Montant = le RESTE à payer (08/10) : versements déduits — créances, décompte de
    /// sortie et archivage comptent ce qui est encore dû.
    public static List<FactureEtat> DuesDe(Locataire loc)
    {
        var res = new List<FactureEtat>();
        if (loc?.facturesEtat == null) return res;
        foreach (var r in loc.facturesEtat)
        {
            var e = EtatDe(r, loc);
            if (e != Etat.Envoye && e != Etat.Impaye) continue;
            int i = string.IsNullOrEmpty(r.pdfPath) ? -1 : res.FindIndex(x => x.pdfPath == r.pdfPath);
            if (i >= 0) res[i].montant += r.montant;
            else res.Add(CopieDe(r));
        }
        foreach (var d in res) d.montant -= Paye(d);   // la copie de la porteuse a ses versements
        return res;
    }

    /// Vrai si la ligne `key` a déjà été traitée (envoyée / impayée / payée) →
    /// l'alerte « à faire » correspondante peut s'éteindre.
    ///
    /// « En attente d'envoi » n'est PAS traité : le PDF est prêt, mais la facture
    /// n'est pas partie — c'est même le moment où le rappel est le plus utile. Éteindre
    /// l'alerte là aurait rendu l'oubli silencieux, et c'est bien ce qui se passait.
    ///
    /// À ne pas confondre avec `EstDejaEmise`, qui protège du double numéro : pour
    /// elle, « en attente d'envoi » compte bel et bien comme émise (le PDF existe, le
    /// numéro est consommé). Deux questions différentes, deux réponses différentes.
    public static bool DejaTraite(Locataire loc, string key)
    {
        var rec = loc?.facturesEtat?.FirstOrDefault(x => x.key == key);
        if (rec == null) return false;
        return rec.statut == "Envoye" || rec.statut == "Impaye" || rec.statut == "Paye";
    }

    // Index de période (1..N) d'un mois, selon la périodicité (inverse de MoisEcheance).
    public static int PeriodeIndex(Periodicite p, int month)
    {
        switch (p)
        {
            case Periodicite.trimestriel: return (month - 1) / 3 + 1;
            case Periodicite.BiAnnuel:    return (month - 1) / 6 + 1;
            case Periodicite.Annuel:      return 1;
            default:                      return month; // mensuel
        }
    }

    /// Échéance d'une période de loyer : le jour où le loyer est demandé
    /// (`jourDemandeLoyer`, « le X »), posé sur le mois d'échéance de la période.
    /// Le jour est borné à la longueur réelle du mois — « le 31 » en février tombe
    /// le 28 ou le 29, et non le 3 mars.
    ///
    /// Publique parce que le panneau de facture doit proposer EXACTEMENT cette date :
    /// il proposait « date de facture + 30 jours », une échéance que le suivi ne
    /// reconnaissait pas, alors que la règle vivait déjà ici.
    public static DateTime EcheanceLoyer(Locataire loc, int year, int periode)
    {
        int jour = loc != null && loc.jourDemandeLoyer > 0 ? loc.jourDemandeLoyer : 1;
        int mois = MoisEcheance(loc, periode);
        return new DateTime(year, mois, Mathf.Clamp(jour, 1, DateTime.DaysInMonth(year, mois)));
    }

    /// Premier et dernier jour d'une période de loyer : du 1er de son mois d'échéance à
    /// la veille de la période suivante (les mois cochés peuvent être irréguliers).
    public static void PeriodeBornes(Locataire loc, int year, int periode, out DateTime debut, out DateTime fin)
    {
        int n = NbPeriodes(loc != null ? loc.periodiciteLoyer : Periodicite.mensuel);
        debut = new DateTime(year, MoisEcheance(loc, periode), 1);
        var suivant = periode < n
            ? new DateTime(year, MoisEcheance(loc, periode + 1), 1)
            : new DateTime(year + 1, MoisEcheance(loc, 1), 1);
        // Mois cochés incomplets (moins de mois que de périodes) : période standard.
        if (suivant <= debut) suivant = debut.AddMonths(12 / n);
        fin = suivant.AddDays(-1);
    }

    /// Ligne du dépôt de garantie initial, posée à l'initialisation du dépôt :
    /// statut « Envoye » = demandé mais pas reçu (une créance) ; statut vide = pas
    /// encore demandé (« À faire », la facture se génère depuis le suivi).
    public static void AjouterDepotInitial(Locataire loc, float montant, string statut, DateTime echeance)
    {
        if (loc == null) return;
        loc.facturesEtat ??= new List<FactureEtat>();
        var rec = loc.facturesEtat.FirstOrDefault(x => x.key == CleDepotInitial);
        if (rec == null) { rec = new FactureEtat { key = CleDepotInitial }; loc.facturesEtat.Add(rec); }
        rec.type = "Depot";
        rec.libelle = "Dépôt de garantie";
        rec.echeanceISO = echeance.ToString("yyyy-MM-dd");
        rec.montant = montant;
        rec.statut = statut ?? "";
        if (rec.statut == "Envoye") rec.dateEnvoiISO = DateTime.Today.ToString("yyyy-MM-dd");
    }

    /// Échéance attendue par les modalités actuelles pour une ligne de loyer déjà
    /// préparée, quand elle diffère de celle imprimée — sinon null.
    ///
    /// Le cas vécu : on prépare le loyer d'octobre à échéance du 8, puis on change le
    /// jour de demande pour le 7. La facture garde le 8, et c'est volontaire : son PDF
    /// porte cette date. Mais l'écart n'était visible nulle part, et la date d'envoi
    /// calculée dessus paraissait fausse. On le signale donc, avec le geste qui le
    /// corrige : refaire la facture.
    ///
    /// Sans objet pour une facture partie (sa date est gravée chez le locataire) ni
    /// pour une ligne sans statut, que `Fusion` réaligne déjà toute seule.
    public static DateTime? EcheanceAttendue(Locataire loc, FactureEtat f)
    {
        if (loc == null || f == null || f.type != "Loyer" || f.statut != "AttenteEnvoi") return null;
        if (string.IsNullOrEmpty(f.key) || !TryEcheance(f.echeanceISO, out var actuelle)) return null;

        // Clé « loyer-{année}-P{période} ».
        var parts = f.key.Split('-');
        if (parts.Length < 3 || !int.TryParse(parts[1], out int annee)) return null;
        if (!int.TryParse(parts[2].TrimStart('P', 'p'), out int periode)) return null;

        var attendue = EcheanceLoyer(loc, annee, periode);
        return attendue.Date == actuelle.Date ? (DateTime?)null : attendue;
    }

    /// Date à laquelle une facture doit partir, ou null si son type n'en a pas.
    ///
    /// Seul le loyer est attendu à date fixe (échéance − délai d'envoi, `EnvoiAvant`) : une régularisation,
    /// une refacturation ou une révision de dépôt s'envoient quand on les fait.
    /// Sert au suivi, aux alertes, à l'envoi groupé du lancement — et au panneau
    /// Loyer, qui refuse d'expédier avant cette date.
    public static DateTime? DateEnvoi(string type, string echeanceISO, Locataire loc = null)
    {
        if (type != "Loyer") return null;
        if (!TryEcheance(echeanceISO, out var ech)) return null;
        return ech.AddDays(-EnvoiAvant(loc)).Date;
    }

    /// Vrai si la facture peut partir aujourd'hui : soit son type n'a pas de date
    /// d'envoi, soit cette date est atteinte (ou dépassée — un retard doit partir).
    public static bool PeutPartir(string type, string echeanceISO, DateTime aujourdhui, Locataire loc = null)
    {
        var d = DateEnvoi(type, echeanceISO, loc);
        return !d.HasValue || aujourdhui.Date >= d.Value;
    }

    /// Mois d'échéance d'une période, pour CE locataire : les mois qu'on a cochés
    /// dans la révision de loyer (`moisFacturationLoyer`) s'ils existent, sinon le
    /// calendrier standard de la périodicité.
    ///
    /// Les mois cochés étaient respectés par les alertes et ignorés par le suivi :
    /// sur un bail trimestriel facturé en février, l'alerte annonçait le 05/02 et le
    /// tableau affichait le 05/01 — deux dates pour un même loyer.
    /// Sans objet en mensuel (les douze mois sont facturés de toute façon).
    public static int MoisEcheance(Locataire loc, int periode)
    {
        var p = loc != null ? loc.periodiciteLoyer : Periodicite.mensuel;
        var choisis = MoisChoisis(loc);
        if (choisis != null)
            return choisis[Mathf.Clamp(periode - 1, 0, choisis.Count - 1)];
        return MoisEcheance(p, periode);
    }

    /// Les mois de facturation cochés, triés et filtrés — ou null s'il n'y a rien à
    /// suivre (bail mensuel, liste vide, valeurs hors 1-12).
    static List<int> MoisChoisis(Locataire loc)
    {
        if (loc == null || loc.periodiciteLoyer == Periodicite.mensuel) return null;
        if (loc.moisFacturationLoyer == null || loc.moisFacturationLoyer.Count == 0) return null;
        var tries = loc.moisFacturationLoyer.Where(m => m >= 1 && m <= 12).Distinct().OrderBy(m => m).ToList();
        return tries.Count > 0 ? tries : null;
    }

    /// Numéro de période correspondant à un mois, pour CE locataire — l'inverse de
    /// `MoisEcheance`, donc il doit suivre les mêmes mois cochés.
    public static int PeriodeIndex(Locataire loc, int month)
    {
        var choisis = MoisChoisis(loc);
        if (choisis != null)
        {
            int i = choisis.IndexOf(month);
            if (i >= 0) return i + 1;
        }
        return PeriodeIndex(loc != null ? loc.periodiciteLoyer : Periodicite.mensuel, month);
    }

    static int MoisEcheance(Periodicite p, int periode)
    {
        switch (p)
        {
            case Periodicite.trimestriel: return Mathf.Clamp((periode - 1) * 3 + 1, 1, 12);
            case Periodicite.BiAnnuel:    return Mathf.Clamp((periode - 1) * 6 + 1, 1, 12);
            case Periodicite.Annuel:      return 1;
            default:                      return Mathf.Clamp(periode, 1, 12); // mensuel
        }
    }

    static string PeriodeLibelle(Periodicite p, int periode, int year)
    {
        switch (p)
        {
            case Periodicite.trimestriel: return $"{Ord(periode)} trimestre {year}";
            case Periodicite.BiAnnuel:    return $"{Ord(periode)} semestre {year}";
            case Periodicite.Annuel:      return $"année {year}";
            default:                      return $"{MoisNoms[Mathf.Clamp(periode, 1, 12) - 1]} {year}";
        }
    }

    static string Ord(int n) => n == 1 ? "1er" : $"{n}e";

    // ── Helpers publics (sélecteur de reprise de facturation) ───────────────────

    // Échéance (date) d'une période donnée.
    public static DateTime EcheancePeriode(Periodicite p, int periode, int year, int jour)
    {
        int mois = MoisEcheance(p, periode);
        int j = Mathf.Clamp(jour > 0 ? jour : 1, 1, DateTime.DaysInMonth(year, mois));
        return new DateTime(year, mois, j);
    }

    // Libellé lisible d'une période (« 1er trimestre 2026 », « Janvier 2026 »…).
    public static string LibellePeriode(Periodicite p, int periode, int year) => PeriodeLibelle(p, periode, year);
}
