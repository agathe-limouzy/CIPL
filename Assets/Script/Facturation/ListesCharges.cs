using System;
using System.Collections.Generic;
using System.Linq;

/// Une liste de charges définie dans les Réglages (ex. « Taxe foncière »), régularisée
/// à part : chaque locataire à provision a pour elle sa propre date et sa propre provision.
[Serializable]
public class ListeCharges
{
    public string id = Guid.NewGuid().ToString("N");   // sans tiret : il entre dans les clés de suivi
    public string nom;
}

/// Date de régularisation et provision d'un locataire pour UNE liste spécifique.
/// La liste générale garde les champs historiques du locataire
/// (`dateRegularisationChargeISO`, `provisionPourChargeValue`).
[Serializable]
public class RegulListe
{
    public string listeId;
    public string dateISO;    // "yyyy-MM-dd" ; vide = pas de régularisation pour cette liste
    public float provision;   // par période de loyer, comme la provision générale
}

/// Montant d'une liste de charges mémorisé sur une facture (provision du loyer).
[Serializable]
public class MontantListe
{
    public string listeId;
    public float montant;
}

/// Listes de charges. La liste GÉNÉRALE (id vide) existe toujours ; les autres
/// s'ajoutent dans les Réglages. Sans liste spécifique, tout se comporte comme avant.
/// Une charge dont la liste a été supprimée retombe dans la générale.
public static class ListesCharges
{
    public const string NomGeneraleDefaut = "Charges générales";

    /// Nom de la liste générale : renommable dans les Réglages (jamais supprimable).
    public static string NomGenerale
        => string.IsNullOrWhiteSpace(R?.nomListeGenerale) ? NomGeneraleDefaut : R.nomListeGenerale.Trim();

    static ReglageData R => ReglageService.Current;

    public static List<ListeCharges> Specifiques() => R?.listesCharges ?? new List<ListeCharges>();

    /// Id réellement en vigueur : "" pour la générale, ou pour une liste supprimée.
    public static string Effective(string listeId)
        => !string.IsNullOrEmpty(listeId) && Specifiques().Exists(l => l.id == listeId) ? listeId : "";

    public static string Nom(string listeId)
        => Specifiques().Find(l => l.id == Effective(listeId))?.nom ?? NomGenerale;

    public static bool DeLaListe(ChargeBatiment c, string listeId)
        => c != null && Effective(c.listeId) == Effective(listeId);

    // ── Locataire ─────────────────────────────────────────────────────────────

    /// Le locataire est-il concerné par cette liste ? Sans restriction (liste vide) :
    /// par toutes. Une restriction vers une liste supprimée retombe sur la générale.
    /// Sans provision, les listes ne s'appliquent pas (leur choix est masqué) : il
    /// relève de toutes les charges, comme avant les listes.
    public static bool ConcerneParListe(Locataire loc, string listeId)
    {
        if (loc == null || !loc.provisionPourCharges) return true;
        var choix = loc.listesConcernees;
        if (choix == null || choix.Count == 0) return true;
        string eff = Effective(listeId);
        return choix.Exists(x => Effective(x) == eff);
    }

    /// La charge concerne-t-elle ce locataire : désigné (ou « tous »), ET concerné par
    /// la liste de la charge. Seule source de cette règle pour la refacturation, la
    /// régularisation et la fiche charge.
    public static bool Concerne(ChargeBatiment c, Locataire loc)
        => c != null && loc != null
           && (c.tousLocataires || (c.locatairesConcernes != null && c.locatairesConcernes.Contains(loc.id)))
           && ConcerneParListe(loc, c.listeId);

    /// Quote-part du locataire = coût × sa part / somme des parts. Les parts des
    /// locataires du bâtiment qui ne sont plus concernés par la liste de la charge ne
    /// comptent pas : la charge se répartit entre ceux qui la paient. Sans ratios, la
    /// charge revient entière à son seul locataire.
    public static float QuotePart(ChargeBatiment c, Locataire loc, Batiment bat)
    {
        if (c == null || loc == null) return 0f;
        if (c.ratios == null || c.ratios.Count == 0) return c.cout;
        float sum = 0f, mine = -1f;
        foreach (var r in c.ratios)
        {
            if (r == null) continue;
            var l = bat?.locataireDuBatiment?.Find(x => x.id == r.locataireId);
            if (l != null && !ConcerneParListe(l, c.listeId)) continue;
            sum += r.part;
            if (r.locataireId == loc.id) mine = r.part;
        }
        return mine < 0f || sum <= 0f ? 0f : c.cout * mine / sum;
    }

    /// Charges d'une liste qui concernent vraiment ce locataire : désigné (ou « tous »),
    /// quote-part non nulle, datées à partir du début de son bail. Avec
    /// `nonPayeesSeulement`, seulement celles qu'il doit encore — non régularisées, ou
    /// régularisées mais pas encore encaissées. Sert à refuser de lui retirer une liste
    /// tant qu'il y doit quelque chose.
    public static List<ChargeBatiment> ChargesDeListe(Batiment bat, Locataire loc, string listeId, bool nonPayeesSeulement)
    {
        var res = new List<ChargeBatiment>();
        if (bat?.charges == null || loc == null) return res;
        bool debutConnu = DateTime.TryParse(loc.dateDebutBailISO, out var debut);
        foreach (var c in bat.charges)
        {
            if (c == null || !DeLaListe(c, listeId)) continue;
            bool designe = c.tousLocataires || (c.locatairesConcernes != null && c.locatairesConcernes.Contains(loc.id));
            if (!designe || QuotePart(c, loc, bat) <= 0.005f) continue;
            if (debutConnu && DateTime.TryParse(c.dateISO, out var d) && d.Date < debut.Date) continue;
            bool payee = c.paye || c.FacturationDe(loc.id)?.paye == true;
            if (nonPayeesSeulement && payee) continue;
            res.Add(c);
        }
        return res;
    }

    /// Locataires à provision qui doivent encore des charges de cette liste, bâtiment
    /// par bâtiment. Non vide = la liste ne peut pas être supprimée : ses charges
    /// changeraient de régularisation en cours de route. (Sans provision, rien ne se
    /// perd : la charge reste refacturable depuis la générale.)
    public static List<(Batiment bat, Locataire loc, List<ChargeBatiment> dues)> Debiteurs(IEnumerable<Batiment> bats, string listeId)
    {
        var res = new List<(Batiment, Locataire, List<ChargeBatiment>)>();
        foreach (var bat in bats ?? Enumerable.Empty<Batiment>())
            foreach (var loc in bat?.locataireDuBatiment ?? new List<Locataire>())
            {
                if (loc == null || !loc.provisionPourCharges) continue;
                var dues = ChargesDeListe(bat, loc, listeId, true);
                if (dues.Count > 0) res.Add((bat, loc, dues));
            }
        return res;
    }

    /// Suppression d'une liste : ses charges passent dans la GÉNÉRALE — elles restent
    /// dans l'historique, avec leur état par locataire. Les dates et provisions
    /// saisies pour elle sont retirées ; un locataire limité à elle l'est désormais à
    /// la générale. Renvoie le nombre de charges basculées.
    public static int BasculerVersGenerale(Batiment bat, string listeId)
    {
        if (bat == null || string.IsNullOrEmpty(listeId)) return 0;
        int n = 0;
        foreach (var c in bat.charges ?? new List<ChargeBatiment>())
            if (c != null && c.listeId == listeId) { c.listeId = ""; n++; }
        foreach (var loc in bat.locataireDuBatiment ?? new List<Locataire>())
        {
            loc.regulListes?.RemoveAll(r => r == null || r.listeId == listeId);
            if (loc.listesConcernees != null && loc.listesConcernees.Contains(listeId))
                loc.listesConcernees = loc.listesConcernees.Select(x => x == listeId ? "" : x).Distinct().ToList();
        }
        return n;
    }

    public static RegulListe De(Locataire loc, string listeId)
        => loc?.regulListes?.Find(x => x != null && x.listeId == listeId);

    public static string DateRegul(Locataire loc, string listeId)
        => string.IsNullOrEmpty(listeId) ? loc?.dateRegularisationChargeISO : De(loc, listeId)?.dateISO;

    /// Provision par période de loyer pour une liste (0 sans provision).
    public static float Provision(Locataire loc, string listeId)
    {
        if (loc == null || !loc.provisionPourCharges || !ConcerneParListe(loc, listeId)) return 0f;
        return string.IsNullOrEmpty(listeId) ? loc.provisionPourChargeValue : (De(loc, listeId)?.provision ?? 0f);
    }

    /// Total appelé avec le loyer : générale + listes spécifiques encore définies.
    public static float ProvisionTotale(Locataire loc)
        => Provision(loc, "") + Specifiques().Sum(l => Provision(loc, l.id));

    /// Listes régularisées pour ce locataire : la générale, plus chaque liste
    /// spécifique pour laquelle il a une date — parmi celles qui le concernent.
    public static List<string> DuLocataire(Locataire loc)
    {
        var res = new List<string>();
        if (ConcerneParListe(loc, "")) res.Add("");
        foreach (var l in Specifiques())
            if (ConcerneParListe(loc, l.id) && !string.IsNullOrEmpty(De(loc, l.id)?.dateISO)) res.Add(l.id);
        return res;
    }

    /// Libellé d'une régularisation : inchangé pour la générale.
    public static string Libelle(string listeId, int annee)
        => $"Régularisation des charges {annee}" + (Effective(listeId) == "" ? "" : $" — {Nom(listeId)}");

    /// Libellé d'une régularisation regroupant plusieurs listes (une seule facture).
    public static string Libelle(IList<string> listes, int annee)
    {
        if (listes == null || listes.Count == 0) return Libelle("", annee);
        if (listes.Count == 1) return Libelle(listes[0], annee);
        return $"Régularisation des charges {annee} — " + string.Join(", ", listes.Select(Nom));
    }

    /// Libellé d'une ligne de provision sur la facture de loyer : inchangé tant
    /// qu'aucune liste spécifique n'existe, sinon suivi du nom de la liste.
    public static string LibelleProvision(string listeId)
        => Specifiques().Count == 0 ? "Provision pour charges" : $"Provision pour charges — {Nom(listeId)}";

    /// Listes spécifiques pour lesquelles ce locataire verse une provision.
    public static List<string> Provisionnees(Locataire loc)
        => Specifiques().Where(l => Provision(loc, l.id) > 0f).Select(l => l.id).ToList();

    // ── Clés de suivi ─────────────────────────────────────────────────────────
    // Générale : « regul-2025 » (inchangé, l'historique reste lisible).
    // Spécifique : « regul-2025-<id> ».

    public static string Cle(string listeId, int annee)
        => string.IsNullOrEmpty(listeId) ? $"regul-{annee}" : $"regul-{annee}-{listeId}";

    public static bool LireCle(string key, out int annee, out string listeId)
    {
        annee = 0; listeId = "";
        const string p = "regul-";
        if (string.IsNullOrEmpty(key) || !key.StartsWith(p)) return false;
        string reste = key.Substring(p.Length);
        int tiret = reste.IndexOf('-');
        if (tiret >= 0) { listeId = reste.Substring(tiret + 1); reste = reste.Substring(0, tiret); }
        return int.TryParse(reste, out annee);
    }
}
