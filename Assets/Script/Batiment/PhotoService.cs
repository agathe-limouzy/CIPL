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

        string dossier = DossiersDonnees.DossierPhotos(batiment.Name);
        Directory.CreateDirectory(dossier);

        int n = 0;
        foreach (var src in paths)
        {
            if (string.IsNullOrEmpty(src) || !File.Exists(src)) continue;
            try
            {
                string dest = CheminUnique(dossier, Path.GetFileName(src));
                File.Copy(src, dest, false);
                // On stocke un chemin RELATIF à la racine de sauvegarde. Un chemin
                // absolu cassait dès que la sauvegarde changeait de disque ou de
                // machine, alors même que le fichier avait suivi le déplacement.
                batiment.photos.Add(DossiersDonnees.VersRelatif(dest));
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

    /// Photo retirée, avec de quoi revenir en arrière (voir `Supprimer` / `Restaurer`).
    public class PhotoSupprimee
    {
        public string chemin;           // chemin STOCKÉ, tel qu'il figurait dans batiment.photos
        public int index;               // position d'origine dans la liste
        public bool etaitCouverture;    // la photo servait de couverture
        public string cheminCorbeille;  // fichier déplacé (absolu) ; vide si le fichier manquait déjà
    }

    /// Vide le cache de textures. À appeler chaque fois que la racine de sauvegarde
    /// change (bascule d'entreprise, changement d'emplacement) : le cache est indexé
    /// par chemin RELATIF, donc deux entreprises ayant un bâtiment de même nom avec un
    /// fichier de même nom partagent la même clé — la seconde recevait la photo de la
    /// première. Les textures sont détruites explicitement : Unity ne les ramasse pas.
    public static void ViderCache()
    {
        foreach (var tex in _cache.Values)
        {
            if (tex == null) continue;
            // `Destroy` est refusé hors mode Play (« Destroy may not be called from
            // edit mode ») : la texture survivrait et une erreur rouge s'afficherait.
            if (Application.isPlaying) Object.Destroy(tex);
            else Object.DestroyImmediate(tex);
        }
        _cache.Clear();
    }

    /// Corbeille des photos, à la racine de la sauvegarde. Les fichiers y patientent
    /// quelques jours au lieu d'être détruits : c'est ce qui rend l'annulation possible.
    static string DossierCorbeille =>
        Path.Combine(SaveLocationService.GetSaveRoot(), "corbeille_photos");

    /// Retire une photo : de la liste, du cache, de la couverture et du dossier du
    /// bâtiment — mais le fichier est DÉPLACÉ en corbeille, pas détruit.
    ///
    /// Toutes les autres suppressions de l'app (achat, travaux, charge, fiche) passent
    /// par une confirmation puis une annulation ; la photo était la seule à être
    /// définitive dès le premier clic, sans même une corbeille système pour rattraper.
    ///
    /// Renvoie de quoi restaurer, ou null si rien n'a été retiré.
    public static PhotoSupprimee Supprimer(Batiment batiment, string chemin)
    {
        if (batiment == null || string.IsNullOrEmpty(chemin)) return null;

        int index = batiment.photos.IndexOf(chemin);
        bool couverture = batiment.coverPhoto == chemin;

        batiment.photos.Remove(chemin);
        if (couverture) batiment.coverPhoto = "";
        _cache.Remove(chemin);

        // Le chemin stocké peut être relatif (nouveau format) ou absolu (ancien) :
        // on résout avant de toucher au disque.
        string absolu = DossiersDonnees.VersAbsolu(chemin);
        string enCorbeille = "";
        // Un échec ici laissait le fichier sur le disque alors que l'UI annonçait
        // la suppression : on le signale au lieu de le taire.
        try
        {
            if (File.Exists(absolu))
            {
                Directory.CreateDirectory(DossierCorbeille);
                enCorbeille = Path.Combine(DossierCorbeille,
                    System.Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(absolu));
                File.Move(absolu, enCorbeille);
            }
        }
        catch (System.Exception e)
        {
            enCorbeille = "";
            Debug.LogError($"[PhotoService] Photo non retirée du disque ({absolu}) : {e.Message}");
        }

        return new PhotoSupprimee
        {
            chemin = chemin,
            index = index,
            etaitCouverture = couverture,
            cheminCorbeille = enCorbeille
        };
    }

    /// Annule une suppression : remet le fichier en place et la photo dans la liste, à
    /// sa position d'origine. Renvoie false si le fichier n'a pas pu être remis — dans
    /// ce cas l'appelant ne doit pas faire croire à une restauration réussie.
    public static bool Restaurer(Batiment batiment, PhotoSupprimee suppr)
    {
        if (batiment == null || suppr == null) return false;

        string chemin = suppr.chemin;
        if (!string.IsNullOrEmpty(suppr.cheminCorbeille))
        {
            try
            {
                if (!File.Exists(suppr.cheminCorbeille)) return false;

                string absolu = DossiersDonnees.VersAbsolu(chemin);
                Directory.CreateDirectory(Path.GetDirectoryName(absolu));
                // Une photo ajoutée entre-temps a pu reprendre le nom libéré : on ne
                // l'écrase pas, on restaure sous un nom libre et c'est celui-là qu'on
                // réinscrit dans la liste.
                if (File.Exists(absolu))
                {
                    absolu = CheminUnique(Path.GetDirectoryName(absolu), Path.GetFileName(absolu));
                    chemin = DossiersDonnees.VersRelatif(absolu);
                }
                File.Move(suppr.cheminCorbeille, absolu);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PhotoService] Restauration impossible ({suppr.cheminCorbeille}) : {e.Message}");
                return false;
            }
        }

        if (!batiment.photos.Contains(chemin))
        {
            int i = suppr.index < 0
                ? batiment.photos.Count
                : Mathf.Clamp(suppr.index, 0, batiment.photos.Count);
            batiment.photos.Insert(i, chemin);
        }
        if (suppr.etaitCouverture) batiment.coverPhoto = chemin;
        return true;
    }

    /// Vide la corbeille des photos de plus de `jours` jours (appelée au démarrage).
    /// Sans cette purge, les photos supprimées s'accumuleraient indéfiniment dans la
    /// sauvegarde — et seraient recopiées à chaque migration d'entreprise.
    public static void PurgerCorbeille(int jours = 7)
    {
        try
        {
            string dossier = DossierCorbeille;
            if (!Directory.Exists(dossier)) return;

            var limite = System.DateTime.Now.AddDays(-jours);
            foreach (string f in Directory.GetFiles(dossier))
            {
                try { if (File.GetLastWriteTime(f) < limite) File.Delete(f); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[PhotoService] Corbeille : « {Path.GetFileName(f)} » non purgé ({e.Message}).");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PhotoService] Purge de la corbeille photos impossible : {e.Message}");
        }
    }

    /// Charge une image du disque en Texture2D (mise en cache). Null si absente.
    /// Accepte indifféremment un chemin relatif (nouveau format) ou absolu (ancien).
    public static Texture2D Charger(string chemin)
    {
        if (string.IsNullOrEmpty(chemin)) return null;
        if (_cache.TryGetValue(chemin, out var cached) && cached != null) return cached;

        string absolu = DossiersDonnees.VersAbsolu(chemin);
        if (!File.Exists(absolu)) return null;
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(absolu)))
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

    // L'ancien DossierPhotos (« Photos/<id> », rangé à part et nommé par GUID) a été
    // remplacé par DossiersDonnees.DossierPhotos, qui place les photos DANS le dossier
    // du bâtiment, nommé par son nom. La migration des dossiers existants est prise en
    // charge séparément.

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
