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
    public enum Etat { AVenir, AFaire, AttenteEnvoi, Envoye, Impaye, Paye, Cloture }

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

    public const int ImpayeApresEcheanceJours = 15; // Impayé auto 15 j après l'échéance si non réglé
    public const int EnvoiAvantJours          = 15; // loyer préparé tôt → envoyé auto 15 j avant l'échéance

    static readonly string[] MoisNoms =
    { "Janvier","Février","Mars","Avril","Mai","Juin","Juillet","Août","Septembre","Octobre","Novembre","Décembre" };

    // Délai (jours avant l'échéance) à partir duquel une ligne passe « À faire ».
    // Diagramme : Loyer = 15 j avant l'envoi + 1 semaine = 22 j avant l'échéance ;
    // Régul / Dépôt = à la date elle-même (pas d'anticipation dans le suivi ;
    // l'anticipation « à préparer » est portée par FacturationAlertes).
    public static int Lead(string type)
    {
        switch (type)
        {
            case "Loyer": return 22;
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
        int n = NbPeriodes(loc.periodiciteLoyer);
        int jour = loc.jourDemandeLoyer > 0 ? loc.jourDemandeLoyer : 1;
        for (int p = 1; p <= n; p++)
        {
            int mois = MoisEcheance(loc.periodiciteLoyer, p);
            var ech = new DateTime(year, mois, Mathf.Clamp(jour, 1, DateTime.DaysInMonth(year, mois)));
            res.Add(Fusion(stored, $"loyer-{year}-P{p}", "Loyer",
                $"Loyer {PeriodeLibelle(loc.periodiciteLoyer, p, year)}", ech, 0f));
        }

        // Régularisation des charges de l'année (si provision) — faite en janvier N+1.
        if (loc.provisionPourCharges)
        {
            DateTime ech = TryEcheance(loc.dateRegularisationChargeISO, out var dr)
                ? new DateTime(year + 1, dr.Month, Mathf.Min(dr.Day, DateTime.DaysInMonth(year + 1, dr.Month)))
                : new DateTime(year + 1, 1, 31);
            res.Add(Fusion(stored, $"regul-{year}", "Regul",
                $"Régularisation des charges {year}", ech, 0f));
        }

        // Révision du dépôt de garantie (si la date limite tombe cette année).
        if (loc.depotDeGarantie > 0f && TryEcheance(loc.dateRevisionDepotISO, out var dd) && dd.Year == year)
            res.Add(Fusion(stored, $"depot-{year}", "Depot",
                "Révision du dépôt de garantie", dd, 0f));

        // Refacturations enregistrées de l'année (créées à la demande).
        foreach (var r in stored)
        {
            if (r.type != "Refac") continue;
            int ry = TryEcheance(r.echeanceISO, out var re) ? re.Year : year;
            if (ry == year && !res.Any(x => x.key == r.key)) res.Add(CopieDe(r));
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

    // Ligne planifiée : reprend l'enregistrement s'il existe, sinon une ligne « vierge ».
    static FactureEtat Fusion(List<FactureEtat> stored, string key, string type, string libelle, DateTime ech, float montant)
    {
        var rec = stored.FirstOrDefault(x => x.key == key);
        if (rec != null)
        {
            // On garde l'état/envoi stocké et on rafraîchit le libellé (la périodicité
            // a pu changer). Le suffixe « corrigée(X) » est réappliqué depuis le compteur.
            var c = CopieDe(rec);
            c.libelle = libelle + (rec.corrections > 0 ? $" — corrigée({rec.corrections})" : "");
            // Facture ÉMISE (Envoyé / En attente d'envoi / Impayé / Payé) : on garde SON
            // échéance réelle (celle imprimée sur la facture) → l'état (Impayé à
            // échéance+15) est cohérent avec la colonne « Créances » (qui lit le stocké).
            // Ligne planifiée non émise : on aligne l'échéance sur la période courante.
            bool emise = rec.statut == "Envoye" || rec.statut == "AttenteEnvoi"
                      || rec.statut == "Impaye" || rec.statut == "Paye";
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
        montant = r.montant, ribId = r.ribId, ribNom = r.ribNom, corrections = r.corrections,
        dernierRappelISO = r.dernierRappelISO
    };

    // ── État d'une ligne ────────────────────────────────────────────────────────

    public static Etat EtatDe(FactureEtat f)
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

        if (s == "Envoye" || s == "AttenteEnvoi")
        {
            // En attente d'envoi : loyer préparé tôt, envoyé (état) auto 15 j avant l'échéance.
            if (s == "AttenteEnvoi" && hasEch && today < ech.AddDays(-EnvoiAvantJours))
                return Etat.AttenteEnvoi;
            // Envoyé → Impayé 15 j après l'échéance si non réglé.
            if (hasEch && today >= ech.AddDays(ImpayeApresEcheanceJours))
                return Etat.Impaye;
            return Etat.Envoye;
        }

        if (s == "AFaire") return Etat.AFaire;

        // Pas d'enregistrement : À faire quand on approche l'échéance, sinon À venir.
        if (hasEch && today >= ech.AddDays(-Lead(f.type)))
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
        }
        return "";
    }

    // ── Écriture ────────────────────────────────────────────────────────────────

    // Passe la ligne « Envoyé » (à la génération d'une facture).
    public static void MarquerEnvoye(Locataire loc, string key, string type, string libelle,
        string echeanceISO, string numero, string pdfPath, float montant, string ribId, string ribNom)
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

        // Diagramme : un loyer préparé plus de 15 j avant l'échéance passe « En
        // attente d'envoi » (envoyé auto — état seulement — à J-15) ; sinon « Envoyé ».
        bool loyerTot = type == "Loyer" && TryEcheance(echeanceISO, out var ech)
                        && DateTime.Today < ech.AddDays(-EnvoiAvantJours);
        rec.statut = loyerTot ? "AttenteEnvoi" : "Envoye";
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
        var e = EtatDe(rec);
        bool emise = e == Etat.AttenteEnvoi || e == Etat.Envoye
                  || e == Etat.Impaye || e == Etat.Paye;
        return emise && !string.IsNullOrEmpty(rec.pdfPath);
    }

    /// Chemin exploitable du PDF d'une ligne : résout le chemin stocké, relatif
    /// aujourd'hui, absolu pour les enregistrements antérieurs. À utiliser PARTOUT
    /// plutôt que `f.pdfPath` brut — celui-ci n'est plus ouvrable tel quel.
    public static string CheminPdf(FactureEtat f)
        => f == null ? null : DossiersDonnees.VersAbsolu(f.pdfPath);

    // Correction d'une facture déjà émise : incrémente le compteur, suffixe le libellé
    // « corrigée(X) », met à jour le PDF/montant. Conserve le numéro ET le statut : un
    // loyer corrigé avant sa date d'envoi reste « En attente d'envoi » (le passer à
    // « Envoyé » afficherait un envoi qui n'a pas eu lieu), et une facture payée reste
    // payée. Seule une ligne sans statut devient « Envoyé ».
    // Renvoie X (le nouveau nombre de corrections).
    public static int MarquerCorrige(Locataire loc, string key, string libelleBase, string pdfPath, float montant)
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
        if (montant > 0f) rec.montant = montant;
        rec.dateEnvoiISO = DateTime.Today.ToString("yyyy-MM-dd");
        if (string.IsNullOrEmpty(rec.statut)) rec.statut = "Envoye";
        return rec.corrections;
    }

    // Mémorise la date d'un rappel d'échéance envoyé (sur l'enregistrement stocké,
    // pas la copie affichée). Sans effet si la ligne n'a pas d'enregistrement.
    public static void MarquerRappel(Locataire loc, string key)
    {
        var rec = loc?.facturesEtat?.FirstOrDefault(x => x.key == key);
        if (rec != null) rec.dernierRappelISO = DateTime.Today.ToString("yyyy-MM-dd");
    }

    // Force manuellement l'état d'une ligne (statut = "" pour revenir à l'auto À venir/À faire).
    public static void SetStatut(Locataire loc, FactureEtat ligne, string statut)
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
        rec.statut = statut ?? "";
        if (statut == "Envoye" && string.IsNullOrEmpty(rec.dateEnvoiISO))
            rec.dateEnvoiISO = DateTime.Today.ToString("yyyy-MM-dd");
    }

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
            {
                if (loc?.facturesEtat == null) continue;
                foreach (var r in loc.facturesEtat)
                {
                    var e = EtatDe(r);
                    if (e == Etat.Envoye || e == Etat.Impaye)
                        res.Add(new Due { bp = bp, loc = loc, rec = r, etat = e });
                }
            }
        }
        return res;
    }

    // Vrai si la ligne `key` a déjà été traitée (envoyée / impayée / payée) → l'alerte
    // « à faire » correspondante peut s'éteindre.
    public static bool DejaTraite(Locataire loc, string key)
    {
        var rec = loc?.facturesEtat?.FirstOrDefault(x => x.key == key);
        if (rec == null) return false;
        return rec.statut == "Envoye" || rec.statut == "AttenteEnvoi"
            || rec.statut == "Impaye" || rec.statut == "Paye";
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
