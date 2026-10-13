using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// Bâtiments et locataires sur le cloud. Le JSON est celui de CIPL (JsonUtility), coupé en
/// deux niveaux : le bâtiment SANS ses locataires, et une ligne par locataire. C'est ce qui
/// permettra de verrouiller et d'enregistrer un locataire sans toucher aux autres (étape 4).
public static class DonneesCloud
{
    // ── Découpage et recomposition (sans réseau : testés en EditMode) ──────────

    /// JSON du bâtiment sans locataires + JSON de chaque locataire, dans l'ordre des onglets.
    /// Ne modifie pas `b` : on travaille sur une copie.
    public static void Decouper(Batiment b, out string jsonBatiment, out List<string> jsonLocataires)
    {
        var copie = JsonUtility.FromJson<Batiment>(JsonUtility.ToJson(b));
        var locs = copie.locataireDuBatiment ?? new List<Locataire>();
        copie.locataireDuBatiment = new List<Locataire>();
        jsonBatiment = JsonUtility.ToJson(copie);
        jsonLocataires = locs.Where(l => l != null).Select(l => JsonUtility.ToJson(l)).ToList();
    }

    [Serializable] class LigneBatiment { public string id; public Batiment data; }
    [Serializable] class LigneLocataire { public string batiment_id; public int ordre; public Locataire data; }
    [Serializable] class LignesBatiments { public LigneBatiment[] items; }
    [Serializable] class LignesLocataires { public LigneLocataire[] items; }
    [Serializable] class LignesEntreprises { public EntrepriseCloud[] items; }

    /// Reconstruit les bâtiments à partir des réponses de /batiments et /locataires (tableaux JSON).
    public static List<Batiment> Recomposer(string jsonBatiments, string jsonLocataires)
    {
        var bats = JsonUtility.FromJson<LignesBatiments>("{\"items\":" + jsonBatiments + "}")?.items ?? new LigneBatiment[0];
        var locs = JsonUtility.FromJson<LignesLocataires>("{\"items\":" + jsonLocataires + "}")?.items ?? new LigneLocataire[0];
        var parBatiment = locs.Where(l => l?.data != null).GroupBy(l => l.batiment_id)
                              .ToDictionary(g => g.Key, g => g.OrderBy(l => l.ordre).Select(l => l.data).ToList());
        var resultat = new List<Batiment>();
        foreach (var ligne in bats)
        {
            if (ligne?.data == null) continue;
            ligne.data.locataireDuBatiment = parBatiment.TryGetValue(ligne.id, out var l) ? l : new List<Locataire>();
            resultat.Add(ligne.data);
        }
        return resultat;
    }

    public static List<EntrepriseCloud> LireEntreprises(string jsonTableau)
    {
        try { return (JsonUtility.FromJson<LignesEntreprises>("{\"items\":" + jsonTableau + "}")?.items ?? new EntrepriseCloud[0]).ToList(); }
        catch { return null; }
    }

    /// Corps d'insertion : le JSON de CIPL est posé tel quel dans « data » (objet, pas chaîne).
    public static string CorpsBatiment(string entrepriseId, string id, string jsonBatiment) =>
        "[{\"entreprise_id\":" + Chaine(entrepriseId) + ",\"id\":" + Chaine(id) + ",\"data\":" + jsonBatiment + "}]";

    public static string CorpsLocataires(string entrepriseId, string batimentId, IList<string> ids, IList<string> jsons)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < jsons.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"entreprise_id\":").Append(Chaine(entrepriseId))
              .Append(",\"id\":").Append(Chaine(ids[i]))
              .Append(",\"batiment_id\":").Append(Chaine(batimentId))
              .Append(",\"ordre\":").Append(i)
              .Append(",\"data\":").Append(jsons[i]).Append('}');
        }
        return sb.Append(']').ToString();
    }

    static string Chaine(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // ── Réseau ─────────────────────────────────────────────────────────────────

    /// Null + motif si l'appel échoue.
    public static async Task<(List<EntrepriseCloud> liste, string erreur)> MesEntreprises()
    {
        var r = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "mes_entreprises", new Vide());
        if (!r.Ok) return (null, r.Erreur);
        var l = LireEntreprises(r.Corps);
        return l == null ? (null, "Liste des entreprises illisible.") : (l, null);
    }

    [Serializable] class Vide { }
    [Serializable] class ParamNom { public string p_nom; }

    /// Crée l'entreprise (et mon groupe si besoin) pour un import ; renvoie son id.
    public static async Task<(string id, string erreur)> CreerPourImport(string nom)
    {
        var r = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(),
                                           "creer_entreprise_import", new ParamNom { p_nom = nom });
        if (!r.Ok) return (null, r.Erreur);
        string id = r.Corps.Trim().Trim('"');
        return string.IsNullOrEmpty(id) ? (null, "Réponse de création illisible.") : (id, null);
    }

    /// Envoie un bâtiment et ses locataires. Déjà présents = laissés tels quels (reprise possible).
    public static async Task<string> Envoyer(string entrepriseId, Batiment b)
    {
        Decouper(b, out string jsonBat, out var jsonLocs);
        string jeton = await SessionCloud.Jeton();
        var r = await CloudClient.Inserer(SessionCloud.Url, SessionCloud.Cle, jeton, "batiments", CorpsBatiment(entrepriseId, b.id, jsonBat));
        if (!r.Ok) return $"« {b.Name} » : {r.Erreur}";
        if (jsonLocs.Count == 0) return null;
        var ids = b.locataireDuBatiment.Where(l => l != null).Select(l => l.id).ToList();
        r = await CloudClient.Inserer(SessionCloud.Url, SessionCloud.Cle, jeton, "locataires", CorpsLocataires(entrepriseId, b.id, ids, jsonLocs));
        return r.Ok ? null : $"Locataires de « {b.Name} » : {r.Erreur}";
    }

    /// Tous les bâtiments de l'entreprise, avec leurs locataires.
    public static async Task<(List<Batiment> batiments, string erreur)> Charger(string entrepriseId)
    {
        string jeton = await SessionCloud.Jeton();
        var rb = await CloudClient.Table("GET", SessionCloud.Url, SessionCloud.Cle, jeton, "batiments",
                                         $"entreprise_id=eq.{entrepriseId}&select=id,data");
        if (!rb.Ok) return (null, rb.Erreur);
        var rl = await CloudClient.Table("GET", SessionCloud.Url, SessionCloud.Cle, jeton, "locataires",
                                         $"entreprise_id=eq.{entrepriseId}&select=batiment_id,ordre,data&order=ordre.asc");
        if (!rl.Ok) return (null, rl.Erreur);
        try { return (Recomposer(rb.Corps, rl.Corps), null); }
        catch (Exception e) { return (null, "Données illisibles : " + e.Message); }
    }
}
