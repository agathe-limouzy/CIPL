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

    // ── Réglages (JSON) ────────────────────────────────────────────────────────

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                _current = JsonUtility.FromJson<ReglageData>(File.ReadAllText(FilePath)) ?? new ReglageData();
            else
                _current = new ReglageData();
        }
        catch
        {
            _current = new ReglageData();
        }
    }

    public static void Save()
    {
        Directory.CreateDirectory(SaveLocationService.GetSaveRoot());
        File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
        Debug.Log($"[ReglageService] Réglages sauvegardés → {FilePath}");
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
    public static void SetApiKey(string v) => WriteSecret("pennylane_api_key", v);

    public static string GetSmtpPassword() => ReadSecret("smtp_password");
    public static void SetSmtpPassword(string v) => WriteSecret("smtp_password", v);

    private static string ReadSecret(string key)
    {
        try
        {
            if (!File.Exists(SecretsPath)) return "";
            foreach (var line in File.ReadAllLines(SecretsPath))
            {
                int i = line.IndexOf('=');
                if (i > 0 && line.Substring(0, i) == key)
                    return line.Substring(i + 1);
            }
        }
        catch { }
        return "";
    }

    private static void WriteSecret(string key, string value)
    {
        var dict = new Dictionary<string, string>();
        try
        {
            if (File.Exists(SecretsPath))
                foreach (var line in File.ReadAllLines(SecretsPath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) dict[line.Substring(0, i)] = line.Substring(i + 1);
                }
        }
        catch { }

        dict[key] = value ?? "";

        var sb = new StringBuilder();
        foreach (var kv in dict) sb.AppendLine(kv.Key + "=" + kv.Value);

        Directory.CreateDirectory(SaveLocationService.GetSaveRoot());
        File.WriteAllText(SecretsPath, sb.ToString());
    }
}
