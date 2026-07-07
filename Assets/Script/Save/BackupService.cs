using System;
using System.IO;
using UnityEngine;

public static class BackupService
{
    private const string BACKUP_FOLDER = "Backups";
    private const string DATE_FORMAT = "yyyy-MM-dd_HH-mm-ss";
    private const int RETENTION_DAYS = 30;

    /// À appeler une fois au démarrage de l'app
    public static void RunStartupBackup()
    {
        try
        {
            string saveRoot = SaveLocationService.GetSaveRoot();
            string dataFolder = Path.Combine(saveRoot, "batiments");
            string backupRoot = Path.Combine(saveRoot, BACKUP_FOLDER);

            Directory.CreateDirectory(backupRoot);

            // ── 1. Copie du dossier avec date + heure d'ouverture ─────────────
            if (Directory.Exists(dataFolder) &&
                Directory.GetFiles(dataFolder, "*.json").Length > 0)
            {
                string stamp = DateTime.Now.ToString(DATE_FORMAT);
                string backupDest = Path.Combine(backupRoot, $"batiments_{stamp}");

                CopyDirectory(dataFolder, backupDest);
                Debug.Log($"[Backup] Sauvegarde créée : {backupDest}");
            }

            // ── 2. Purge des backups de plus de 30 jours ──────────────────────
            PurgeOldBackups(backupRoot);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Backup] Erreur : {e.Message}");
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));

        foreach (string dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private static void PurgeOldBackups(string backupRoot)
    {
        DateTime limite = DateTime.Now.AddDays(-RETENTION_DAYS);

        foreach (string dir in Directory.GetDirectories(backupRoot))
        {
            string nom = Path.GetFileName(dir);

            // On parse la date depuis le nom du dossier "batiments_2026-07-03_21-42-15"
            int idx = nom.IndexOf('_');
            if (idx < 0) continue;

            string datePart = nom.Substring(idx + 1);
            if (DateTime.TryParseExact(datePart, DATE_FORMAT,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime dateBackup))
            {
                if (dateBackup < limite)
                {
                    Directory.Delete(dir, recursive: true);
                    Debug.Log($"[Backup] Backup supprimé (>{RETENTION_DAYS}j) : {nom}");
                }
            }
        }
    }
}