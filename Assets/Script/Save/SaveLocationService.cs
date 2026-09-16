using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class SaveLocationService
{
    private const string PREF_KEY = "save_root_path";
    private const string FOLDER_NAME = "CIPL_Saves";

    /// Racine choisie par l'utilisateur (ou persistentDataPath par défaut)
    // Évite de répéter l'alerte à chaque appel (GetSaveRoot est très sollicité).
    private static string _racineManquanteSignalee;

    public static string GetSaveRoot()
    {
        string custom = PlayerPrefs.GetString(PREF_KEY, "");

        if (!string.IsNullOrEmpty(custom) && Directory.Exists(custom))
            return custom;

        // Une racine enregistrée mais INTROUVABLE (disque externe débranché, dossier
        // renommé ou déplacé) faisait retomber l'app sur l'emplacement par défaut sans
        // rien dire : elle démarrait « vide » et les sauvegardes suivantes partaient
        // ailleurs que les données réelles.
        if (!string.IsNullOrEmpty(custom) && custom != _racineManquanteSignalee)
        {
            _racineManquanteSignalee = custom;
            Debug.LogError($"[SaveLocationService] Emplacement de sauvegarde introuvable : {custom}. " +
                           "Repli sur l'emplacement par défaut — vos données ne sont PAS perdues, " +
                           "mais rebranchez le disque ou rouvrez l'entreprise avant de saisir quoi que ce soit.");
            UndoToast.Instance?.ShowInfo("Emplacement de sauvegarde introuvable — repli sur le dossier par défaut.");
        }

        // Fallback : emplacement par défaut Unity
        return GetDefaultRoot();
    }

    /// Vrai si l'utilisateur a défini un emplacement personnalisé (existant).
    public static bool IsCustom()
    {
        string custom = PlayerPrefs.GetString(PREF_KEY, "");
        return !string.IsNullOrEmpty(custom) && Directory.Exists(custom);
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

    /// Active directement une racine FINALE connue (déjà « …/CIPL_Saves »), sans
    /// ré-ajouter le sous-dossier. Utilisé pour basculer entre entreprises.
    public static void UseRoot(string finalRoot)
    {
        if (string.IsNullOrEmpty(finalRoot)) return;
        Directory.CreateDirectory(finalRoot);
        PlayerPrefs.SetString(PREF_KEY, finalRoot);
        PlayerPrefs.Save();
    }

    /// Revient à l'emplacement par défaut (persistentDataPath).
    public static void ResetToDefault()
    {
        PlayerPrefs.DeleteKey(PREF_KEY);
        PlayerPrefs.Save();
    }

    /// Copie les données de `oldRoot` vers `newRoot`.
    /// Renvoie false (et renseigne `erreur`) sans rien copier si la destination
    /// contient déjà des données : l'appelant NE DOIT PAS considérer le déplacement
    /// comme fait dans ce cas.
    public static bool MigrateData(string oldRoot, string newRoot, out string erreur)
    {
        erreur = null;
        if (string.IsNullOrEmpty(oldRoot) || string.IsNullOrEmpty(newRoot)) return true;
        if (string.Equals(oldRoot, newRoot, StringComparison.OrdinalIgnoreCase)) return true;
        if (!Directory.Exists(oldRoot)) return true;

        try
        {
            // TOUS les fichiers, pas seulement les *.json : le fichier de secrets
            // (clé API Pennylane) et les photos restaient sinon dans l'ancien dossier,
            // alors que celui-ci est ensuite retiré de la liste des entreprises.
            string[] fichiers = Directory.GetFiles(oldRoot, "*", SearchOption.AllDirectories);

            // Pré-analyse : refuser d'écraser des données déjà présentes. Migrer vers
            // un dossier contenant déjà une entreprise écrasait son reglage.json
            // (nom fixe → collision garantie) sans le moindre avertissement.
            var conflits = fichiers
                .Select(f => CheminRelatif(oldRoot, f))
                .Where(rel => File.Exists(Path.Combine(newRoot, rel)))
                .ToList();

            if (conflits.Count > 0)
            {
                erreur = $"La destination contient déjà {conflits.Count} fichier(s) de sauvegarde " +
                         $"({string.Join(", ", conflits.Take(3))}{(conflits.Count > 3 ? "…" : "")}). " +
                         "Migration annulée pour ne pas écraser une autre entreprise — choisissez un dossier vide.";
                Debug.LogError("[SaveLocationService] " + erreur);
                return false;
            }

            foreach (string f in fichiers)
            {
                string dest = Path.Combine(newRoot, CheminRelatif(oldRoot, f));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(f, dest);
            }
            return true;
        }
        catch (Exception e)
        {
            erreur = e.Message;
            Debug.LogError($"[SaveLocationService] Migration interrompue : {e.Message}");
            return false;
        }
    }

    /// Chemin d'un fichier relativement à une racine.
    /// `string.Replace` était sensible à la casse alors que les chemins Windows ne le
    /// sont pas : une simple différence de casse laissait le chemin inchangé, et on
    /// copiait le fichier sur lui-même.
    private static string CheminRelatif(string racine, string chemin)
    {
        string r = Path.GetFullPath(racine)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string c = Path.GetFullPath(chemin);

        return c.StartsWith(r, StringComparison.OrdinalIgnoreCase)
            ? c.Substring(r.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : Path.GetFileName(chemin);
    }
}