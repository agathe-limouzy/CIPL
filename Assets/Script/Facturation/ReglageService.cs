using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// Charge / sauvegarde les réglages globaux (facturation).
/// - Les réglages "normaux" vont dans <SaveRoot>/reglage.json.
/// - Les SECRETS (clé API Pennylane, mot de passe SMTP) vont dans un fichier
///   séparé <SaveRoot>/pennylane_secrets.dat (format clé=valeur), qui n'est PAS
///   du JSON → ignoré par les backups (qui ne copient que les *.json) et jamais
///   commité dans le repo.
public static class ReglageService
{
    private static ReglageData _current;

    public static ReglageData Current
    {
        get { if (_current == null) Load(); return _current; }
    }

    private static string FilePath =>
        Path.Combine(SaveLocationService.GetSaveRoot(), "reglage.json");

    private static string SecretsPath =>
        Path.Combine(SaveLocationService.GetSaveRoot(), "pennylane_secrets.dat");

    // Secrets de l'APPLICATION, communs à toutes les entreprises. La clé Pennylane et
    // le mot de passe SMTP appartiennent à une entreprise donnée ; le token Mapbox, lui,
    // est une clé de l'app : le ranger par entreprise obligerait à le ressaisir à chaque
    // fois qu'on en ouvre une autre.
    private static string AppSecretsPath =>
        Path.Combine(Application.persistentDataPath, "cipl_app_secrets.dat");

    // ── Réglages (JSON) ────────────────────────────────────────────────────────

    // Vrai si reglage.json existe mais n'a pas pu être exploité. Dans ce cas les
    // réglages en mémoire sont VIDES et ne représentent pas le fichier : toute
    // écriture détruirait définitivement l'original. Save() refuse donc d'écrire.
    private static bool _chargementEchoue;

    public static bool ChargementEchoue => _chargementEchoue;

    public static void Load()
    {
        _chargementEchoue = false;

        var res = AtomicFile.ReadAllText(FilePath, out string json);

        // Fichier absent = premier lancement. C'est normal, pas une anomalie.
        if (res == AtomicFile.Lecture.Absent) { _current = new ReglageData(); return; }

        if (res == AtomicFile.Lecture.Ok)
        {
            try
            {
                var data = JsonUtility.FromJson<ReglageData>(json);
                if (data != null) { _current = data; return; }
            }
            catch { /* JSON invalide → on tente la sauvegarde de secours ci-dessous */ }
        }

        // Fichier présent mais inexploitable → repli sur le .bak laissé par la
        // dernière écriture atomique réussie.
        if (AtomicFile.ReadAllText(AtomicFile.CheminBak(FilePath), out string bak) == AtomicFile.Lecture.Ok)
        {
            try
            {
                var data = JsonUtility.FromJson<ReglageData>(bak);
                if (data != null)
                {
                    _current = data;
                    Debug.LogWarning("[ReglageService] reglage.json illisible — réglages restaurés depuis la sauvegarde .bak.");
                    return;
                }
            }
            catch { /* .bak lui aussi inexploitable */ }
        }

        // Rien de récupérable. On repart d'un objet vide pour que l'app reste
        // utilisable, MAIS on verrouille l'écriture pour ne pas détruire l'original.
        _current = new ReglageData();
        _chargementEchoue = true;
        Debug.LogError(
            $"[ReglageService] {FilePath} est présent mais illisible, et aucune sauvegarde .bak n'est exploitable. " +
            "Les réglages affichés sont VIDES et l'enregistrement est désactivé pour ne pas écraser le fichier d'origine. " +
            "Mettez ce fichier de côté (renommez-le) puis relancez l'app pour repartir de réglages neufs.");
    }

    /// Renvoie true si l'enregistrement a réellement eu lieu.
    public static bool Save()
    {
        if (_chargementEchoue)
        {
            Debug.LogError("[ReglageService] Enregistrement refusé : les réglages n'ont pas pu être lus au démarrage. " +
                           "Écrire maintenant écraserait définitivement le fichier d'origine.");
            return false;
        }

        if (!AtomicFile.WriteAllText(FilePath, JsonUtility.ToJson(Current, true), out string erreur))
        {
            Debug.LogError($"[ReglageService] ÉCHEC d'enregistrement des réglages — {erreur}. Rien n'a été écrit sur le disque.");
            return false;
        }

        Debug.Log($"[ReglageService] Réglages sauvegardés → {FilePath}");
        return true;
    }

    // ── Lookup par ID (référencés depuis les locataires) ───────────────────────

    public static RibData GetRib(string id) =>
        string.IsNullOrEmpty(id) ? null : Current.ribs.Find(r => r.id == id);

    public static EnteteData GetEntete(string id) =>
        string.IsNullOrEmpty(id) ? null : Current.entetes.Find(e => e.id == id);

    // Entête à sélectionner : la mémorisée si présente, sinon celle qui correspond
    // au type de facture (association auto par mot-clé du nom).
    public static string EnteteChoisi(string stored, string type)
        => !string.IsNullOrEmpty(stored) ? stored : EnteteDefautId(type);

    // Entête suggérée par défaut pour un type ("Loyer"/"Regul"/"Refac"/"Depot") :
    // 1re entête dont le NOM contient un mot-clé du type, sinon la 1re de la liste.
    public static string EnteteDefautId(string type)
    {
        var ents = Current.entetes;
        if (ents == null || ents.Count == 0) return null;
        var kw = MotsClesEntete(type);
        foreach (var e in ents)
        {
            string nom = (e.nom ?? "").ToLowerInvariant();
            foreach (var k in kw)
                if (nom.Contains(k)) return e.id;
        }
        return ents[0].id;
    }

    static string[] MotsClesEntete(string type)
    {
        switch (type)
        {
            case "Loyer": return new[] { "loyer" };
            case "Regul": return new[] { "régularisation", "regularisation", "charges", "régul", "regul" };
            case "Refac": return new[] { "refacturation", "refac" };
            case "Depot": return new[] { "dépôt", "depot", "garantie" };
            default:      return new string[0];
        }
    }

    // ── Secrets (hors JSON / hors backup / hors repo) ──────────────────────────

    public static string GetApiKey() => ReadSecret("pennylane_api_key");
    public static bool SetApiKey(string v) => WriteSecret("pennylane_api_key", v);

    public static string GetSmtpPassword() => ReadSecret("smtp_password");
    public static bool SetSmtpPassword(string v) => WriteSecret("smtp_password", v);

    // Token Mapbox : stocké hors du champ public de TileLoader, qui était sérialisé
    // EN CLAIR dans Maps.prefab et donc commité dans le dépôt.
    // Rangé au niveau APPLICATION (pas par entreprise) : c'est une clé de l'app, la
    // mettre dans le fichier de l'entreprise obligerait à la ressaisir à chaque bascule.
    public static string GetMapboxToken() => ReadSecretFrom(AppSecretsPath, "mapbox_token");
    public static bool SetMapboxToken(string v) => WriteSecretTo(AppSecretsPath, "mapbox_token", v);

    private static string ReadSecret(string key) => ReadSecretFrom(SecretsPath, key);

    private static string ReadSecretFrom(string chemin, string key)
    {
        try
        {
            if (!File.Exists(chemin)) return "";   // pas encore de secret : normal
            foreach (var line in File.ReadAllLines(chemin))
            {
                int i = line.IndexOf('=');
                if (i > 0 && line.Substring(0, i) == key)
                    return line.Substring(i + 1);
            }
        }
        catch (System.Exception e)
        {
            // Sans ce log, un fichier de secrets verrouillé ou corrompu renvoyait "" :
            // l'app se comportait comme si aucune clé n'était configurée, sans le dire.
            Debug.LogError($"[ReglageService] Lecture du secret « {key} » impossible ({e.Message}) — " +
                           "l'app se comportera comme si la valeur n'était pas renseignée.");
        }
        return "";
    }

    private static bool WriteSecret(string key, string value) => WriteSecretTo(SecretsPath, key, value);

    private static bool WriteSecretTo(string chemin, string key, string value)
    {
        var dict = new Dictionary<string, string>();

        // Relecture des secrets déjà présents. Si le fichier existe mais qu'on ne
        // parvient PAS à le lire, on abandonne : réécrire avec un dictionnaire
        // incomplet effacerait les autres secrets (enregistrer le mot de passe SMTP
        // aurait supprimé la clé API Pennylane).
        if (File.Exists(chemin))
        {
            try
            {
                foreach (var line in File.ReadAllLines(chemin))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) dict[line.Substring(0, i)] = line.Substring(i + 1);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ReglageService] Secrets illisibles ({e.Message}) — écriture annulée " +
                               "pour ne pas effacer les autres secrets enregistrés.");
                return false;
            }
        }

        dict[key] = value ?? "";

        var sb = new StringBuilder();
        foreach (var kv in dict) sb.AppendLine(kv.Key + "=" + kv.Value);

        if (!AtomicFile.WriteAllText(chemin, sb.ToString(), out string erreur))
        {
            Debug.LogError($"[ReglageService] ÉCHEC d'enregistrement des secrets — {erreur}");
            return false;
        }
        return true;
    }
}
