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
    /// Revue 17 (D2-01) : numéro du modèle des données de cette version de CIPL. Il part avec chaque écriture (`p_modele`) et
    /// le serveur refuse qu'une version plus ancienne réécrive une fiche enregistrée par une plus récente : `JsonUtility` y
    /// effacerait les champs qu'elle ne connaît pas. À MONTER à chaque champ ajouté ou retiré dans Batiment, Locataire,
    /// ReglageData ou ce qu'ils contiennent (le test D2_01 de Revue17Tests le rappelle par une empreinte des champs).
    public const int Modele = 11;  // 2 : ChargeBatiment.ponctuelle ; 3 : Batiment.lots ; 4 : ReglageData.phraseEscompte (04/11) ;
                                   // 5 : AchatFinancement.fraisNotaireAuto (Revue 17, D7b-02) ;
                                   // 6 : FactureEtat.provisionsConnues / provision / provisionsListes (Revue 17, D6-07) ;
                                   // 7 : FactureEtat.periodicite (Revue 17, D4-04) ;
                                   // 8 : FactureEtat.destinataire / acheteur (Revue 17, D4-01, lot 6) ; retireApresEnvoi / factureCitee
                                   //     (lot 6b, décisions du 09/11, même modèle) ;
                                   // 9 : FactureEtat.encaisseeAAnnulerId (Revue 17, D5-06, lot 9) ;
                                   // 10 : Locataire.historiquePeriodicites (Revue 17, avenant de périodicité, lot 14) ;
                                   // 11 : ReglageData.mentionTvaParDefaut ; FactureEtat.payeLeISO (coutures)

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
        // Revue 17 (D2-06, lot D) : une lecture en plusieurs pages peut rendre deux fois la même ligne (insertion d'un
        // collègue pendant la lecture) : on garde la première par id, sans quoi l'export écrirait une fiche en double.
        // Un id vide n'est jamais pris pour un doublon (chaque ligne sans id reste).
        var vus = new HashSet<string>();
        var parBatiment = locs.Where(l => l?.data != null && (string.IsNullOrEmpty(l.data.id) || vus.Add("l:" + l.data.id)))
                              .GroupBy(l => l.batiment_id)
                              .ToDictionary(g => g.Key, g => g.OrderBy(l => l.ordre).Select(l => l.data).ToList());
        var resultat = new List<Batiment>();
        foreach (var ligne in bats)
        {
            if (ligne?.data == null || (!string.IsNullOrEmpty(ligne.id) && !vus.Add("b:" + ligne.id))) continue;
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
        "[{\"entreprise_id\":" + CloudClient.Q(entrepriseId) + ",\"id\":" + CloudClient.Q(id) + ",\"data\":" + jsonBatiment + "}]";

    public static string CorpsLocataires(string entrepriseId, string batimentId, IList<string> ids, IList<string> jsons)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < jsons.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"entreprise_id\":").Append(CloudClient.Q(entrepriseId))
              .Append(",\"id\":").Append(CloudClient.Q(ids[i]))
              .Append(",\"batiment_id\":").Append(CloudClient.Q(batimentId))
              .Append(",\"ordre\":").Append(i)
              .Append(",\"data\":").Append(jsons[i]).Append('}');
        }
        return sb.Append(']').ToString();
    }

    // Revue 17 (D1-16, lot D) : la copie privée « Chaine » (guillemets et barres obliques seulement) est remplacée par
    // CloudClient.Q, qui échappe aussi les caractères de contrôle (D3-14 de la 16c l'avait manquée).

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
        // Revue 17 (D2-06, lot D) : lecture page après page (l'export passe par ici), dans l'ordre de la clé (`id`, qui ne
        // change jamais : aucune ligne ne glisse d'une page à l'autre) ; Recomposer remet les locataires dans leur « ordre ».
        var rb = await CloudClient.TableTout(SessionCloud.Url, SessionCloud.Cle, jeton, "batiments",
                                             $"entreprise_id=eq.{entrepriseId}&select=id,data&order=id.asc");
        if (!rb.Ok) return (null, rb.Erreur);
        var rl = await CloudClient.TableTout(SessionCloud.Url, SessionCloud.Cle, jeton, "locataires",
                                             $"entreprise_id=eq.{entrepriseId}&select=batiment_id,ordre,data&order=id.asc");
        if (!rl.Ok) return (null, rl.Erreur);
        try { return (Recomposer(rb.Corps, rl.Corps), null); }
        catch (Exception e) { return (null, "Données illisibles : " + e.Message); }
    }
}
