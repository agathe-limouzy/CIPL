using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Gestion des photos d'un bâtiment : sélection (StandaloneFileBrowser),
/// copie dans le dossier de sauvegarde (Photos/&lt;id&gt;), chargement en
/// Texture2D (avec cache) et suppression. Les fichiers copiés sont ainsi
/// inclus dans les backups et insensibles aux déplacements des originaux.
public static class PhotoService
{
    static readonly string[] Exts = { "jpg", "jpeg", "png" };
    static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

    /// Ouvre le sélecteur de fichiers (multi-sélection), copie les images
    /// choisies dans le dossier du bâtiment et ajoute les chemins à
    /// batiment.photos. Retourne le nombre de photos réellement ajoutées.
    public static int AjouterPhotos(Batiment batiment)
    {
        if (batiment == null) return 0;
#if UNITY_STANDALONE || UNITY_EDITOR
        var paths = SFB.StandaloneFileBrowser.OpenFilePanel(
            "Choisir des photos", "",
            new[] { new SFB.ExtensionFilter("Images", Exts) },
            true);   // multi-sélection

        if (paths == null || paths.Length == 0) return 0;

        string dossier = DossierPhotos(batiment);
        Directory.CreateDirectory(dossier);

        int n = 0;
        foreach (var src in paths)
        {
            if (string.IsNullOrEmpty(src) || !File.Exists(src)) continue;
            try
            {
                string dest = CheminUnique(dossier, Path.GetFileName(src));
                File.Copy(src, dest, false);
                batiment.photos.Add(dest);
                n++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Photo] Copie échouée ({src}) : {e.Message}");
            }
        }
        return n;
#else
        return 0;
#endif
    }

    /// Retire une photo : de la liste, du cache, du disque, et de la couverture.
    public static void Supprimer(Batiment batiment, string chemin)
    {
        if (batiment == null || string.IsNullOrEmpty(chemin)) return;
        batiment.photos.Remove(chemin);
        if (batiment.coverPhoto == chemin) batiment.coverPhoto = "";
        _cache.Remove(chemin);
        try { if (File.Exists(chemin)) File.Delete(chemin); } catch { }
    }

    /// Charge une image du disque en Texture2D (mise en cache). Null si absente.
    public static Texture2D Charger(string chemin)
    {
        if (string.IsNullOrEmpty(chemin) || !File.Exists(chemin)) return null;
        if (_cache.TryGetValue(chemin, out var cached) && cached != null) return cached;
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(chemin)))
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                _cache[chemin] = tex;
                return tex;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Photo] Chargement échoué ({chemin}) : {e.Message}");
        }
        return null;
    }

    static string DossierPhotos(Batiment b) =>
        Path.Combine(SaveLocationService.GetSaveRoot(), "Photos", b.id);

    static string CheminUnique(string dossier, string nom)
    {
        string dest = Path.Combine(dossier, nom);
        string bn = Path.GetFileNameWithoutExtension(nom);
        string ext = Path.GetExtension(nom);
        int i = 1;
        while (File.Exists(dest))
            dest = Path.Combine(dossier, $"{bn}_{i++}{ext}");
        return dest;
    }
}
