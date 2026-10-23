using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

/// Étape 8b : la dernière copie reçue du serveur, gardée sur ce PC pour démarrer sans connexion
/// (décision du 09/10 : lecture seule sur la dernière copie). Dans persistentDataPath, jamais dans
/// le dossier des documents : en mode Drive, elle serait recopiée chez tout le monde. C'est l'état
/// du serveur tel que l'application le connaît (EcritureCloud), versions comprises.
public static class CopieLocale
{
    [Serializable] class Ligne { public string cle, json, batiment; public int version, ordre; }

    [Serializable] class Contenu
    {
        public EntrepriseCloud entreprise;
        public CloudClient.Utilisateur utilisateur;
        public string a_jour_le;
        public Ligne[] lignes;
    }

    // ── Sans disque (testé en EditMode) ────────────────────────────────────────

    public static string Ecrire(EntrepriseCloud e, CloudClient.Utilisateur u, DateTime aJour, IDictionary<string, EcritureCloud.Etat> etats) =>
        JsonUtility.ToJson(new Contenu
        {
            entreprise = e, utilisateur = u,
            a_jour_le = aJour.ToString("o", CultureInfo.InvariantCulture),
            lignes = etats.Select(kv => new Ligne { cle = kv.Key, json = kv.Value.Json, batiment = kv.Value.BatimentId,
                                                    version = kv.Value.Version, ordre = kv.Value.Ordre }).ToArray(),
        });

    /// Faux si la copie est illisible ou incomplète : on ne l'ouvre pas.
    public static bool Lire(string texte, out EntrepriseCloud e, out CloudClient.Utilisateur u, out DateTime aJour,
                            out Dictionary<string, EcritureCloud.Etat> etats)
    {
        e = null; u = null; aJour = default; etats = null;
        Contenu c;
        try { c = JsonUtility.FromJson<Contenu>(texte); }
        catch (Exception) { return false; }
        if (c?.entreprise == null || string.IsNullOrEmpty(c.entreprise.id) || c.lignes == null) return false;
        e = c.entreprise; u = c.utilisateur;
        DateTime.TryParse(c.a_jour_le, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out aJour);
        etats = c.lignes.Where(l => l != null && !string.IsNullOrEmpty(l.cle)).GroupBy(l => l.cle)
                 .ToDictionary(g => g.Key, g => new EcritureCloud.Etat { Json = g.First().json, Version = g.First().version,
                                                                         BatimentId = g.First().batiment, Ordre = g.First().ordre });
        return true;
    }

    /// Les bâtiments de la copie, chacun avec ses locataires dans l'ordre (comme EcritureCloud.Lire).
    public static List<Batiment> Recomposer(IDictionary<string, EcritureCloud.Etat> etats)
    {
        var res = new List<Batiment>();
        foreach (var kv in etats.Where(kv => kv.Key.StartsWith("b:")))
        {
            var b = JsonUtility.FromJson<Batiment>(kv.Value.Json);
            if (b == null) continue;
            string id = kv.Key.Substring(2);
            b.locataireDuBatiment = etats.Where(x => x.Key.StartsWith("l:") && x.Value.BatimentId == id)
                .OrderBy(x => x.Value.Ordre)
                .Select(x => JsonUtility.FromJson<Locataire>(x.Value.Json)).Where(l => l != null).ToList();
            res.Add(b);
        }
        return res;
    }

    // ── Disque ─────────────────────────────────────────────────────────────────

    static string Chemin(string entrepriseId) => Path.Combine(Application.persistentDataPath, $"cipl_copie_{entrepriseId}.json");

    const string CleDerniere = "cipl_derniere_entreprise";

    /// L'entreprise ouverte en dernier sur ce PC : c'est elle qu'on ouvre sans connexion.
    public static string Derniere => PlayerPrefs.GetString(CleDerniere, "");

    public static void Retenir(string entrepriseId)
    {
        PlayerPrefs.SetString(CleDerniere, entrepriseId ?? "");
        PlayerPrefs.Save();
    }

    public static void Garder(EntrepriseCloud e, CloudClient.Utilisateur u, DateTime aJour, IDictionary<string, EcritureCloud.Etat> etats)
    {
        if (e == null || etats == null || etats.Count == 0) return;
        if (!AtomicFile.WriteAllText(Chemin(e.id), Ecrire(e, u, aJour, etats), out string erreur))
            Debug.LogWarning("[Copie] Copie locale non écrite : " + erreur);
    }

    public static bool Charger(string entrepriseId, out EntrepriseCloud e, out CloudClient.Utilisateur u, out DateTime aJour,
                               out Dictionary<string, EcritureCloud.Etat> etats)
    {
        e = null; u = null; aJour = default; etats = null;
        string chemin = Chemin(entrepriseId);
        if (!File.Exists(chemin)) return false;
        try { return Lire(File.ReadAllText(chemin), out e, out u, out aJour, out etats); }
        catch (Exception ex) { Debug.LogWarning("[Copie] Copie locale illisible : " + ex.Message); return false; }
    }

    /// « Se déconnecter » : plus rien de lisible sur ce PC sans connexion.
    public static void Effacer()
    {
        try
        {
            foreach (var f in Directory.GetFiles(Application.persistentDataPath, "cipl_copie_*.json")) File.Delete(f);
        }
        catch (Exception ex) { Debug.LogWarning("[Copie] Effacement des copies locales : " + ex.Message); }
        Retenir("");
    }
}
