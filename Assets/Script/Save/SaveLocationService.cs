using System.IO;
using UnityEngine;

public static class SaveLocationService
{
    private const string PREF_KEY = "save_root_path";
    private const string FOLDER_NAME = "CIPL_Saves";

    /// Racine choisie par l'utilisateur (ou persistentDataPath par défaut)
    public static string GetSaveRoot()
    {
        string custom = PlayerPrefs.GetString(PREF_KEY, "");

        if (!string.IsNullOrEmpty(custom) && Directory.Exists(custom))
            return custom;

        // Fallback : emplacement par défaut Unity
        return GetDefaultRoot();
    }

    public static string GetDefaultRoot()
    {
        string def = Path.Combine(Application.persistentDataPath, FOLDER_NAME);
        Directory.CreateDirectory(def);
        return def;
    }

    /// L'utilisateur choisit un emplacement → on crée le dossier CIPL_Saves dedans
    public static string SetSaveRoot(string chosenDirectory)
    {
        string root = Path.Combine(chosenDirectory, FOLDER_NAME);
        Directory.CreateDirectory(root);

        PlayerPrefs.SetString(PREF_KEY, root);
        PlayerPrefs.Save();
        return root;
    }

    /// Déplace les JSON existants vers le nouvel emplacement
    public static void MigrateData(string oldRoot, string newRoot)
    {
        if (oldRoot == newRoot || !Directory.Exists(oldRoot)) return;

        foreach (string dir in Directory.GetDirectories(oldRoot, "*", SearchOption.AllDirectories))
        {
            string dest = dir.Replace(oldRoot, newRoot);
            Directory.CreateDirectory(dest);
        }

        foreach (string file in Directory.GetFiles(oldRoot, "*.json", SearchOption.AllDirectories))
        {
            string dest = file.Replace(oldRoot, newRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(file, dest, overwrite: true);
        }
    }
}