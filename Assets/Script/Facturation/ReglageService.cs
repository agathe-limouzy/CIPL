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
