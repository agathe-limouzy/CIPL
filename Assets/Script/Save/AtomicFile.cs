using System;
using System.IO;
using UnityEngine;

/// Écriture de fichier atomique.
///
/// Pourquoi : un `File.WriteAllText` direct sur le fichier final laisse une
/// fenêtre pendant laquelle le fichier est tronqué. Une coupure de courant ou un
/// crash à cet instant détruit la sauvegarde. C'est la cause la plus probable
/// d'un `batiment_*.json` ou d'un `reglage.json` illisible.
///
/// Principe : on écrit d'abord dans un `.tmp`, puis on le bascule sur le fichier
/// final en une opération de système de fichiers. L'ancienne version est conservée
/// en `.bak`, ce qui donne un filet de récupération même si le remplacement échoue.
public static class AtomicFile
{
    /// Écrit `contenu` dans `chemin` de façon atomique.
    /// Renvoie false et renseigne `erreur` en cas d'échec — l'appelant DOIT le
    /// remonter à l'utilisatrice plutôt que d'afficher « sauvegardé ».
    public static bool WriteAllText(string chemin, string contenu, out string erreur)
    {
        erreur = null;
        string tmp = chemin + ".tmp";

        try
        {
            string dossier = Path.GetDirectoryName(chemin);
            if (!string.IsNullOrEmpty(dossier)) Directory.CreateDirectory(dossier);

            File.WriteAllText(tmp, contenu);

            if (File.Exists(chemin))
            {
                // File.Replace bascule le .tmp sur la cible et archive l'ancienne
                // version en .bak, en une seule opération.
                File.Replace(tmp, chemin, chemin + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tmp, chemin);   // première écriture : rien à remplacer
            }
            return true;
        }
        catch (Exception e)
        {
            erreur = e.Message;
            // Le .tmp ne doit pas rester traîner : il serait ramassé par les
            // scans de dossier et pourrait être pris pour une sauvegarde valide.
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
            Debug.LogError($"[AtomicFile] Échec d'écriture de {chemin} — {e.Message}");
            return false;
        }
    }

    /// Lit un fichier texte en distinguant les trois cas, au lieu de les confondre :
    /// - fichier absent      → `Absent`, contenu null (cas normal au premier lancement) ;
    /// - fichier lisible     → `Ok` ;
    /// - fichier illisible   → `Illisible` (anomalie : NE PAS écraser derrière).
    public enum Lecture { Ok, Absent, Illisible }

    public static Lecture ReadAllText(string chemin, out string contenu)
    {
        contenu = null;
        try
        {
            if (!File.Exists(chemin)) return Lecture.Absent;
            contenu = File.ReadAllText(chemin);
            return Lecture.Ok;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AtomicFile] Lecture impossible : {chemin} — {e.Message}");
            return Lecture.Illisible;
        }
    }

    /// Chemin de la sauvegarde de secours laissée par la dernière écriture réussie.
    public static string CheminBak(string chemin) => chemin + ".bak";
}
