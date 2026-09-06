using System.IO;
using UnityEngine;

/// Opérations de sauvegarde/chargement partagées (emplacement + charger une save).
/// S'appuie sur SaveLocationService et le plugin StandaloneFileBrowser (SFB).
public static class SaveIO
{
    /// Ouvre le sélecteur de dossier natif. Renvoie false si annulé/indisponible.
    public static bool PickFolder(string title, out string path)
    {
        path = null;
#if UNITY_STANDALONE || UNITY_EDITOR
        var paths = SFB.StandaloneFileBrowser.OpenFolderPanel(title, "", false);
        if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            path = paths[0];
            return true;
        }
#endif
        return false;
    }

    /// Change l'emplacement de sauvegarde ET déplace les données actuelles dedans.
    public static bool ChangeLocation()
    {
        if (!PickFolder("Choisir l'emplacement de sauvegarde", out var dir)) return false;
        string ancien = SaveLocationService.GetSaveRoot();
        string nouveau = SaveLocationService.SetSaveRoot(dir);
        SaveLocationService.MigrateData(ancien, nouveau);
        Reload();
        return true;
    }

    /// Charge une sauvegarde existante depuis un dossier (SANS déplacer l'actuelle).
    /// `dir` doit contenir (ou recevoir) le sous-dossier CIPL_Saves.
    public static bool LoadSave(out int nbBatiments)
    {
        nbBatiments = 0;
        if (!PickFolder("Charger une sauvegarde — choisir le dossier", out var dir)) return false;
        SaveLocationService.SetSaveRoot(dir);   // pointe sur <dir>/CIPL_Saves
        Reload();
        nbBatiments = CountBatiments();
        return true;
    }

    /// Revient à l'emplacement par défaut et recharge.
    public static void ResetToDefault()
    {
        SaveLocationService.ResetToDefault();
        Reload();
    }

    /// Ouvre le dossier de sauvegarde dans l'explorateur système.
    public static void OpenFolder()
    {
        string path = SaveLocationService.GetSaveRoot();
        if (!Directory.Exists(path)) return;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        System.Diagnostics.Process.Start("explorer.exe", path.Replace('/', '\\'));
#else
        Application.OpenURL(path);
#endif
    }

    public static int CountBatiments()
    {
        string bat = Path.Combine(SaveLocationService.GetSaveRoot(), "batiments");
        return Directory.Exists(bat) ? Directory.GetFiles(bat, "*.json").Length : 0;
    }

    static void Reload()
    {
        ReglageService.Load();
        if (BatimentManager.Instance != null)
            BatimentManager.Instance.ReloadFromDisk();
    }
}
