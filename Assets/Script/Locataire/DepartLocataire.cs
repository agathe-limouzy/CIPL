using System;
using System.Collections.Generic;
using System.Linq;

/// Départ d'un locataire — règles décidées avec l'utilisatrice le 01/10 (voir
/// FACTURATION_CIPL_PENNYLANE.md §16.5 « Départ du locataire ») :
/// • décompte de sortie : retenues + créances reprises − dépôt, en UN document (négatif
///   = avoir, on rembourse ; positif = facture), dans le délai de restitution ;
/// • régul de sortie à la date habituelle, charges au prorata de présence
///   (ListesCharges.QuotePartAuProrata), rien retenu sur le dépôt pour elle ;
/// • une fois tout soldé, la fiche peut être archivée.
public static class DepartLocataire
{
    /// Clé de suivi du décompte de sortie (une seule par locataire).
    public const string CleDecompte = "depot-sortie";
    public const int DelaiRestitutionDefautMois = 2;
    public const int DepartProcheJours = 30;
    public const int RestitutionAttentionJours = 15;

    public static bool Sortie(Locataire loc, out DateTime d)
    {
        bool ok = FacturationSuivi.TryEcheance(loc?.dateSortieISO, out d);
        d = d.Date;
        return ok;
    }

    public static int DelaiMois(Locataire loc)
        => loc != null && loc.delaiRestitutionMois > 0 ? loc.delaiRestitutionMois : DelaiRestitutionDefautMois;

    /// Date limite de restitution du dépôt : dernier jour de location + délai.
    public static bool EcheanceRestitution(Locataire loc, out DateTime d)
    {
        d = default;
        if (!Sortie(loc, out var s)) return false;
        d = s.AddMonths(DelaiMois(loc));
        return true;
    }

    /// Le décompte, prêt à imprimer : le dépôt rendu (négatif), une ligne par retenue
    /// (HT ; TVA 20 % si sa case est cochée — sans par défaut : une indemnité de remise
    /// en état n'y est généralement pas soumise) et par créance reprise (son montant
    /// facturé). `ttc` = ce que le document réclame : négatif = AVOIR, on rembourse.
    /// Dépôt 3 000, retenue 500, loyer impayé 1 783,52 → −716,48.
    /// Un dépôt jamais versé (ligne « depot-initial » encore due) ou un complément de
    /// révision impayé est lui-même une créance reprise : il s'annule de lui-même.
    public class Decompte
    {
        public List<KeyValuePair<string, float>> lignes = new List<KeyValuePair<string, float>>();
        public float retenuesHT, creances, totalHT, tva, ttc;
    }

    public static Decompte Calculer(float depot, IEnumerable<RetenueSortie> retenues, IEnumerable<FactureEtat> creances)
    {
        var d = new Decompte();
        double tva = 0;
        foreach (var r in retenues ?? Enumerable.Empty<RetenueSortie>())
        {
            if (r == null || Math.Abs(r.ht) < 0.005f) continue;
            d.lignes.Add(new KeyValuePair<string, float>(
                "Retenue — " + (string.IsNullOrWhiteSpace(r.libelle) ? "remise en état" : r.libelle.Trim())
                + (r.tva ? " (HT, TVA 20 %)" : ""), Cents(r.ht)));
            d.retenuesHT += Cents(r.ht);
            if (r.tva) tva += r.ht * 0.2;
        }
        foreach (var c in creances ?? Enumerable.Empty<FactureEtat>())
        {
            if (c == null) continue;
            d.lignes.Add(new KeyValuePair<string, float>(
                (string.IsNullOrEmpty(c.numero) ? "" : $"Facture n° {c.numero} — ") + c.libelle, Cents(c.montant)));
            d.creances += Cents(c.montant);
        }
        d.retenuesHT = Cents(d.retenuesHT);
        d.creances = Cents(d.creances);
        d.totalHT = Cents(d.retenuesHT + d.creances - depot);
        d.tva = Cents((float)tva);
        d.ttc = Cents(d.totalHT + d.tva);
        return d;
    }

    /// Créances proposées au décompte : les factures dues (une ligne par facture, hors
    /// le décompte lui-même), plus — pour corriger un décompte déjà émis — celles qu'il
    /// a reprises (`dejaReprises`), passées « Payé » depuis : sinon elles disparaîtraient
    /// de la correction.
    public static List<FactureEtat> CreancesProposees(Locataire loc, IEnumerable<string> dejaReprises = null)
    {
        var res = FacturationSuivi.DuesDe(loc).Where(r => r.key != CleDecompte).ToList();
        foreach (var key in dejaReprises ?? Enumerable.Empty<string>())
        {
            if (res.Any(x => x.key == key)) continue;
            var rec = loc?.facturesEtat?.FirstOrDefault(x => x.key == key);
            if (rec == null) continue;
            var meme = FacturationSuivi.MemeFacture(loc, key);
            res.Add(new FactureEtat { key = rec.key, type = rec.type, libelle = rec.libelle, numero = rec.numero,
                                      pdfPath = rec.pdfPath, echeanceISO = rec.echeanceISO, statut = rec.statut,
                                      montant = meme.Count > 0 ? meme.Sum(x => x.montant) : rec.montant });
        }
        return res;
    }

    /// Le décompte est parti — ou il n'y a aucun dépôt à rendre.
    public static bool DecompteFait(Locataire loc)
        => loc != null && (loc.depotDeGarantie <= 0f || FacturationSuivi.DejaTraite(loc, CleDecompte));

    /// Les régul de l'année du départ sont faites (une par liste), ou sans objet.
    public static bool RegulsDeSortieFaites(Locataire loc)
    {
        if (loc == null || !loc.provisionPourCharges || !Sortie(loc, out var s)) return true;
        return ListesCharges.DuLocataire(loc).All(id => FacturationSuivi.DejaTraite(loc, ListesCharges.Cle(id, s.Year)));
    }

    /// Parti, derniers loyers facturés, dépôt rendu, régul de sortie faite, plus aucune
    /// facture due (un avoir non remboursé compte : on doit encore quelque chose) →
    /// « Archiver » proposé.
    public static bool PretAArchiver(Locataire loc)
        => loc != null && !loc.archive && RaisonsNonArchivable(loc).Count == 0;

    /// Ce qui empêche encore d'archiver, en clair, pour le bandeau (retour du 05/10 :
    /// « mettre la raison, facture X pas payée par exemple »). Vide = archivable.
    /// Seule source de vérité de PretAArchiver.
    public static List<string> RaisonsNonArchivable(Locataire loc)
    {
        var r = new List<string>();
        if (!Sortie(loc, out var s)) { r.Add("aucun départ saisi"); return r; }
        if (!loc.EstParti) r.Add($"le locataire n'est pas encore parti (dernier jour le {s:dd/MM/yyyy})");

        foreach (var l in FacturationSuivi.Lignes(loc, s.Year))
        {
            if (l.type != "Loyer") continue;
            var e = FacturationSuivi.EtatDe(l);
            if (LoyerACorriger(loc, l)) r.Add($"« {l.libelle} » à corriger au prorata");
            else if (e == FacturationSuivi.Etat.AFaire || e == FacturationSuivi.Etat.AVenir) r.Add($"« {l.libelle} » à facturer");
            else if (e == FacturationSuivi.Etat.AttenteEnvoi) r.Add($"« {l.libelle} » à envoyer");
        }

        if (!DecompteFait(loc))
            r.Add(FacturationSuivi.EstDejaEmise(loc, CleDecompte, out _) ? "décompte de sortie à envoyer" : "décompte de sortie à faire");

        if (!RegulsDeSortieFaites(loc))
            foreach (var l in FacturationSuivi.Lignes(loc, s.Year).Where(x => x.type == "Regul" && !FacturationSuivi.DejaTraite(loc, x.key)))
                r.Add(FacturationSuivi.TryEcheance(l.echeanceISO, out var ech) && ech.Date > DateTime.Today
                    ? $"« {l.libelle} » à faire à partir du {ech:dd/MM/yyyy}" : $"« {l.libelle} » à faire");

        foreach (var d in FacturationSuivi.DuesDe(loc))
        {
            string quoi = $"{(string.IsNullOrEmpty(d.numero) ? "" : $"n° {d.numero} ")}« {d.libelle} »";
            string montant = Math.Abs(d.montant).ToString("N2", FacturePdfService.FrCulture) + " €";
            r.Add(d.montant < 0f
                ? $"avoir {quoi} à rembourser ({montant}), puis à passer « Payé »"
                : $"facture {quoi} non payée ({montant})");
        }
        return r;
    }


    /// Le départ approche (30 j) : état des lieux et décompte à prévoir.
    public static bool DepartProche(Locataire loc, DateTime aujourdhui)
        => Sortie(loc, out var s) && aujourdhui.Date <= s && aujourdhui.Date >= s.AddDays(-DepartProcheJours);

    /// Loyer émis pour plus que ce qui est dû depuis le départ (facture faite avant
    /// que le départ soit saisi) → « Montant à corriger (départ) » dans le suivi.
    /// Avec le HT mémorisé : comparé au montant de la période. Sans (anciennes lignes) :
    /// seule une période entièrement après le départ est repérée.
    public static bool LoyerACorriger(Locataire loc, FactureEtat l)
    {
        if (l == null || l.type != "Loyer" || !Sortie(loc, out _)) return false;
        if (!FacturationSuivi.EstDejaEmise(loc, l.key, out _)) return false;
        if (!LirePeriode(l.key, out int annee, out int periode)) return false;
        float du = Loyers.MontantPeriode(loc, annee, periode);
        if (l.loyerHT > 0f) return l.loyerHT > du + 0.01f;
        return du <= 0f && l.montant > 0.005f;
    }

    // ── Parcours de départ (maquettes validées le 02/10) ───────────────────────
    // Bandeau de six étapes, en tête de fiche, du départ saisi à l'archivage. L'état se
    // déduit de la fiche : rien n'est stocké. Pas de verrou entre étapes (on peut faire
    // l'état des lieux avant le dernier loyer) ; « Continuer » va à la première faisable.

    public enum Etape { Depart, DernierLoyer, EtatDesLieux, Decompte, Regul, Archiver }
    public enum Statut { Faite, AFaire, EnAttente }   // EnAttente : pas encore faisable (date)
    public static readonly string[] Titres =
        { "Départ", "Dernier loyer", "État des lieux", "Décompte de sortie", "Régul de sortie", "Archiver" };

    /// « Non, pas d'état des lieux », ou date ET PDF (décision du 02/10).
    public static bool EtatDesLieuxFait(Locataire loc)
        => loc != null && (loc.sansEtatDesLieux
           || (FacturationSuivi.TryEcheance(loc.dateEtatDesLieuxISO, out _) && !string.IsNullOrEmpty(loc.etatDesLieux)));

    /// Statut d'une étape. `quand` = date à partir de laquelle une étape en attente
    /// devient faisable (null si elle ne dépend pas d'une date).
    public static Statut StatutDe(Locataire loc, Etape e, DateTime aujourdhui, out DateTime? quand)
    {
        quand = null;
        if (!Sortie(loc, out var s)) return e == Etape.Depart ? Statut.AFaire : Statut.EnAttente;
        switch (e)
        {
            case Etape.Depart: return Statut.Faite;

            case Etape.DernierLoyer:
            {
                var loyers = FacturationSuivi.Lignes(loc, s.Year).Where(l => l.type == "Loyer").ToList();
                if (loyers.Any(l => LoyerACorriger(loc, l))) return Statut.AFaire;
                bool faisable = false;
                foreach (var l in loyers)
                {
                    var et = FacturationSuivi.EtatDe(l);
                    if (et == FacturationSuivi.Etat.AFaire || et == FacturationSuivi.Etat.AttenteEnvoi) faisable = true;
                    else if (et == FacturationSuivi.Etat.AVenir && FacturationSuivi.TryEcheance(l.echeanceISO, out var ech))
                    {
                        var d = ech.AddDays(-FacturationSuivi.Lead("Loyer"));
                        if (quand == null || d < quand) quand = d;
                    }
                }
                return faisable ? Statut.AFaire : quand != null ? Statut.EnAttente : Statut.Faite;
            }

            case Etape.EtatDesLieux: return EtatDesLieuxFait(loc) ? Statut.Faite : Statut.AFaire;

            case Etape.Decompte:
                if (DecompteFait(loc)) return Statut.Faite;
                if (aujourdhui.Date <= s) { quand = s.AddDays(1); return Statut.EnAttente; }
                return Statut.AFaire;

            case Etape.Regul:
            {
                if (RegulsDeSortieFaites(loc)) return Statut.Faite;
                foreach (var l in FacturationSuivi.Lignes(loc, s.Year).Where(l => l.type == "Regul"))
                {
                    var et = FacturationSuivi.EtatDe(l);
                    if (et == FacturationSuivi.Etat.AFaire || et == FacturationSuivi.Etat.AttenteEnvoi) return Statut.AFaire;
                    if (et == FacturationSuivi.Etat.AVenir && FacturationSuivi.TryEcheance(l.echeanceISO, out var ech)
                        && (quand == null || ech < quand)) quand = ech;
                }
                return Statut.EnAttente;
            }

            default:   // Archiver
                return loc.archive ? Statut.Faite : PretAArchiver(loc) ? Statut.AFaire : Statut.EnAttente;
        }
    }

    /// Première étape faisable maintenant, ou null (tout est fait, ou tout attend une date).
    public static Etape? Prochaine(Locataire loc, DateTime aujourdhui)
    {
        foreach (Etape e in Enum.GetValues(typeof(Etape)))
            if (StatutDe(loc, e, aujourdhui, out _) == Statut.AFaire) return e;
        return null;
    }

    /// Phrase du bandeau pour l'étape en cours — ou, si rien n'est faisable, ce qu'on attend.
    public static string Consigne(Locataire loc, DateTime aujourdhui)
    {
        var p = Prochaine(loc, aujourdhui);
        if (p == null)
        {
            foreach (Etape e in Enum.GetValues(typeof(Etape)))
                if (StatutDe(loc, e, aujourdhui, out var q) == Statut.EnAttente)
                    return e == Etape.Archiver
                        ? "Pas encore archivable : " + string.Join(" · ", RaisonsNonArchivable(loc)) + "."
                        : e == Etape.Regul
                        ? $"En attente de la régularisation de sortie{(q != null ? $" : elle se fera le {q:dd/MM/yyyy}" : "")}, avec les charges au prorata."
                        : $"En attente : {Titres[(int)e].ToLowerInvariant()}{(q != null ? $" à partir du {q:dd/MM/yyyy}" : "")}.";
            return "Départ terminé : le locataire est archivé.";
        }
        Sortie(loc, out var s);
        switch (p.Value)
        {
            case Etape.DernierLoyer:
                return LoyerACorrigerLibelle(loc, s) ?? "Étape 2 — Dernier loyer : facturez la dernière période, au prorata jusqu'au départ.";
            case Etape.EtatDesLieux:
                return FacturationSuivi.TryEcheance(loc.dateEtatDesLieuxISO, out var d)
                    ? $"Étape 3 — État des lieux : joignez le PDF de l'état des lieux du {d:dd/MM/yyyy}."
                    : "Étape 3 — État des lieux : saisissez sa date et joignez le PDF (ou choisissez « pas d'état des lieux »).";
            case Etape.Decompte:
                return EcheanceRestitution(loc, out var r) && loc.depotDeGarantie > 0f
                    ? $"Étape 4 — Décompte de sortie : restituez le dépôt avant le {r:dd/MM/yyyy}."
                    : "Étape 4 — Décompte de sortie.";
            case Etape.Regul: return "Étape 5 — Régularisation de sortie : les charges de l'année sont à régulariser, au prorata.";
            case Etape.Archiver: return "Tout est soldé : dépôt rendu, loyers et régul réglés. Vous pouvez archiver le locataire.";
            default: return "Étape 1 — Départ : saisissez le dernier jour de location.";
        }
    }

    static string LoyerACorrigerLibelle(Locataire loc, DateTime s)
    {
        var l = FacturationSuivi.Lignes(loc, s.Year).FirstOrDefault(x => LoyerACorriger(loc, x));
        return l == null ? null : $"Étape 2 — Dernier loyer : « {l.libelle} » a été émis en entier, corrigez-le au prorata.";
    }

    /// Ligne du suivi à ouvrir pour l'étape « Dernier loyer » : celle à corriger, sinon
    /// la première encore à facturer de l'année du départ. Null si rien.
    public static FactureEtat LigneDernierLoyer(Locataire loc)
    {
        if (!Sortie(loc, out var s)) return null;
        var loyers = FacturationSuivi.Lignes(loc, s.Year).Where(l => l.type == "Loyer").ToList();
        return loyers.FirstOrDefault(l => LoyerACorriger(loc, l))
            ?? loyers.FirstOrDefault(l =>
            {
                var et = FacturationSuivi.EtatDe(l);
                return et == FacturationSuivi.Etat.AFaire || et == FacturationSuivi.Etat.AttenteEnvoi || et == FacturationSuivi.Etat.AVenir;
            });
    }

    /// Ligne du suivi de la régul de sortie (la première pas encore traitée), ou null.
    public static FactureEtat LigneRegulSortie(Locataire loc)
        => !Sortie(loc, out var s) ? null
         : FacturationSuivi.Lignes(loc, s.Year).FirstOrDefault(l => l.type == "Regul" && !FacturationSuivi.DejaTraite(loc, l.key));

    /// Prépare le locataire qui reprend le lot sans cession (décision du 06/10) : même lot,
    /// même surface, mêmes listes de charges, bail au lendemain du départ. Il reprend la
    /// part de l'ancien sur les charges de son année d'arrivée et des suivantes — sans
    /// elle, il ne paierait rien ; le partage au prorata des jours fait le reste
    /// (ListesCharges.QuotePart compte le lot une fois).
    public static void PreparerRemplacant(Locataire ancien, Locataire nouveau, Batiment bat)
    {
        if (ancien == null || nouveau == null || !Sortie(ancien, out var s)) return;
        var debut = s.AddDays(1);
        nouveau.lotBatiment = ancien.lotBatiment;
        nouveau.tailleLot = ancien.tailleLot;
        nouveau.listesConcernees = new List<string>(ancien.listesConcernees ?? new List<string>());
        nouveau.dateDebutBailISO = debut.ToString("yyyy-MM-dd");
        nouveau.dateDebutPremierBailISO = nouveau.dateDebutBailISO;
        if (bat?.charges == null) return;
        foreach (var c in bat.charges)
        {
            if (c == null || !FacturationSuivi.TryEcheance(c.dateISO, out var dc) || dc.Year < debut.Year) continue;
            var r = c.ratios?.Find(x => x != null && x.locataireId == ancien.id);
            if (r != null && !c.ratios.Exists(x => x != null && x.locataireId == nouveau.id))
                c.ratios.Add(new ChargeRatio(nouveau.id, r.part));
            if (!c.tousLocataires && c.locatairesConcernes != null && c.locatairesConcernes.Contains(ancien.id)
                && !c.locatairesConcernes.Contains(nouveau.id))
                c.locatairesConcernes.Add(nouveau.id);
        }
    }

    /// Annuler le départ : impossible une fois le décompte émis (il faudrait d'abord le
    /// traiter). Message de refus, ou null.
    public static string PeutAnnuler(Locataire loc)
        => FacturationSuivi.EstDejaEmise(loc, CleDecompte, out _)
            ? "Le décompte de sortie est déjà émis : traitez-le d'abord (avoir) avant d'annuler le départ."
            : null;

    /// « loyer-2026-P2 » → 2026, 2.
    public static bool LirePeriode(string key, out int annee, out int periode)
    {
        annee = periode = 0;
        var p = (key ?? "").Split('-');
        return p.Length == 3 && p[0] == "loyer" && int.TryParse(p[1], out annee)
               && p[2].StartsWith("P") && int.TryParse(p[2].Substring(1), out periode);
    }

    static float Cents(float v) => (float)Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// Une retenue du décompte de sortie (mémorisée sur la facture : FactureInfo.retenuesSortie).
[Serializable]
public class RetenueSortie
{
    public string libelle;
    public float ht;
    public bool tva;
}
