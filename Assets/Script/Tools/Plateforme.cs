using System;
using UnityEngine;

/// Étape 15b (version Mac, décisions du 26/10) : ce qui diffère entre Windows et macOS, à un seul endroit.
public static class Plateforme
{
    public static bool Mac =>
        Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;

    /// Adresse d'un fichier pour Application.OpenURL ou un navigateur. Windows : inchangée depuis
    /// toujours (« file:///C:/… », sans échappement : les accents passent tels quels). Mac : le chemin
    /// commence déjà par « / », l'adresse doit être échappée (« file:///Users/…%20… »).
    public static string UrlFichier(string chemin, bool mac) =>
        mac ? "file://" + string.Join("/", Array.ConvertAll(chemin.Split('/'), Uri.EscapeDataString))
            : "file:///" + chemin.Replace('\\', '/');

    public static string UrlFichier(string chemin) => UrlFichier(chemin, Mac);

    /// Ouvre un dossier dans l'Explorateur (Windows) ou le Finder (Mac).
    public static void OuvrirDossier(string chemin)
    {
        if (Mac) Application.OpenURL(UrlFichier(chemin, true));
        else System.Diagnostics.Process.Start("explorer.exe", chemin.Replace('/', '\\'));
    }
}
