using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class BatimentManager : MonoBehaviour
{
    public static BatimentManager Instance { get; private set; }

    [Header("Prefab")]
    public GameObject batimentPrefab;
    public Transform batimentsContainer;
    public Transform batimentsContainerPanel;
    // Liste en mémoire
    private List<Batiment> _batiments = new List<Batiment>();
    public List<Batiment> Batiments => _batiments;

    public List<BatimentPrefab> BatimentPrefab;
    public MenuManager menuManager;

    // Dossier de sauvegarde — un JSON par bâtiment
    private string SaveFolder =>
    Path.Combine(SaveLocationService.GetSaveRoot(), "batiments");

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Taille d'affichage choisie dans les réglages. En tout premier : la poser
        // après la construction des écrans les obligerait à se recalculer.
        UIZoom.Appliquer();

        BackupService.RunStartupBackup();
        PhotoService.PurgerCorbeille();   // photos supprimées il y a plus de 7 jours
        LoadAll();

        // Migration des dossiers GUID → nommage par nom. Après LoadAll, qui fournit les
        // noms ; elle valide tout son plan avant de déplacer quoi que ce soit, et ne
        // marque l'opération faite que si elle a entièrement réussi.
        if (MigrationDossiers.Migrer(_batiments, out string rapportMigration))
        {
            // Les chemins de photos ont pu être réécrits en relatif : il faut les
            // persister, sinon la réécriture serait refaite à chaque démarrage.
            if (!MigrationDossiers.DejaFaite || rapportMigration.StartsWith("Migration réussie"))
                SaveAll();
        }
        else
        {
            ConfirmDialog.Erreur("Migration des dossiers annulée — voir la console.");
        }

        // L'application démarre sur le menu général (Home)
        menuManager.OpenGeneralMenu();

        // Factures prêtes dont la date d'envoi est atteinte : on propose de les
        // envoyer maintenant. Après OpenGeneralMenu, pour que la fenêtre s'affiche
        // par-dessus l'écran d'accueil et non l'inverse. Ne demande rien s'il n'y a
        // rien à envoyer, si l'email n'est pas configuré, ou si c'est déjà reporté
        // pour aujourd'hui.
        FactureEnvoiAuto.ProposerAuDemarrage(BatimentPrefab);

        // Résumés d'entreprise (annuaire de l'État) rafraîchis pour tous les
        // locataires. En coroutine : les appels réseau s'enchaînent en arrière-plan,
        // l'écran reste utilisable, et rien n'est sauvegardé si rien n'a changé.
        StartCoroutine(PappersSync.Tout(BatimentPrefab));
    }

    // Racine utilisée par le dernier LoadAll. La sauvegarde de fermeture ne doit pas
    // écrire ailleurs si l'emplacement a changé entre-temps (disque débranché, bascule
    // d'entreprise), sinon elle dupliquerait les données au mauvais endroit.
    private string _racineChargement;

    private void OnApplicationQuit() => SauvegardeDeFermeture();

    /// Filet de sécurité à la fermeture : réécrit sur le disque l'état en mémoire.
    /// Utile surtout quand une écriture précédente avait échoué (fichier verrouillé par
    /// un antivirus, lecteur réseau momentanément absent) : la mémoire portait alors la
    /// modification, pas le fichier.
    public void SauvegardeDeFermeture()
    {
        if (_batiments == null || _batiments.Count == 0) return;

        string racine = SaveLocationService.GetSaveRoot();
        if (!string.IsNullOrEmpty(_racineChargement)
            && !string.Equals(racine, _racineChargement, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[BatimentManager] Sauvegarde de fermeture ignorée : l'emplacement a changé " +
                             $"depuis le chargement ({_racineChargement} → {racine}).");
            return;
        }

        // Copie : voir SaveAll — SaveBatiment modifie `_batiments` pendant l'itération.
        int echecs = 0;
        foreach (var b in _batiments.ToArray())
            if (!SaveBatiment(b)) echecs++;

        if (echecs > 0)
            Debug.LogError($"[BatimentManager] Sauvegarde de fermeture : {echecs} bâtiment(s) NON écrit(s).");
        else
            Debug.Log($"[BatimentManager] Sauvegarde de fermeture : {_batiments.Count} bâtiment(s) écrit(s).");
    }

    public void ReloadFromDisk()
    {
        // Détruire les prefabs existants
        foreach (var prefab in BatimentPrefab)
        {
            menuManager.RemoveTabAndBuilding(prefab);
            Destroy(prefab.gameObject);
        }
        BatimentPrefab.Clear();
        _batiments.Clear();
        // Le cache photo est indexe par chemin RELATIF : deux entreprises ayant un
        // batiment de meme nom partagent la meme cle, et la fiche afficherait la
        // photo de l'entreprise precedente.
        PhotoService.ViderCache();

        LoadAll();
    }

    // ── Ajout ────────────────────────────────────────────────────────────────

    public BatimentPrefab AddBatiment(bool needToModify)
    {

        var data = new Batiment();
        var prefab = SpawnPrefabInPanel(data, menuManager.ContentPrefab.transform, needToModify);
        BatimentPrefab.Add(prefab);
        // Persistance immédiate : sans ça, un bâtiment tout juste créé n'existait
        // qu'en mémoire et disparaissait à la fermeture de l'app.
        // (SaveBatiment ajoute lui-même `data` à _batiments, d'où l'absence d'Add ici.)
        SaveBatiment(data);
        return prefab;
      
        
    }

    // ── Suppression ──────────────────────────────────────────────────────────

    // Dossiers de bâtiments supprimés, en attente d'une éventuelle annulation.
    // Clé = id du bâtiment, valeur = son emplacement en corbeille.
    static readonly Dictionary<string, string> _dossiersEnCorbeille = new Dictionary<string, string>();

    static string DossierCorbeilleBatiments =>
        Path.Combine(SaveLocationService.GetSaveRoot(), "corbeille_batiments");

    public void DeleteBatiment(string id)
    {
        // Nom relevé AVANT le retrait de la liste : c'est lui qui donne le dossier.
        string nom = _batiments.Find(b => b.id == id)?.Name;
        _batiments.RemoveAll(b => b.id == id);
        var tab = BatimentPrefab.Find(t => t.getID() == id);
        BatimentPrefab.RemoveAll(b => b.getID() == id);
        if (tab != null)
        {
            menuManager.RemoveTabAndBuilding(tab);
            Destroy(tab.gameObject);
        }

        // Supprime le fichier JSON
        string path = GetFilePath(id);
        if (File.Exists(path))
            File.Delete(path);

        // Le dossier du bâtiment (photos, factures, charges) est DÉPLACÉ en corbeille,
        // pas laissé en place : un dossier orphelin faisait ensuite échouer tout
        // renommage vers ce nom (« un dossier X existe déjà »), alors qu'aucun bâtiment
        // de ce nom n'apparaissait dans l'application — cause invisible depuis l'app.
        // Il n'est JAMAIS purgé automatiquement : il contient des pièces comptables.
        string enCorbeille = DeplacerDossierEnCorbeille(nom);
        if (!string.IsNullOrEmpty(enCorbeille)) _dossiersEnCorbeille[id] = enCorbeille;

        // Le cache photo est indexé par chemin relatif : sans ce vidage, un bâtiment
        // recréé sous le même nom afficherait les photos de celui qu'on vient d'effacer.
        PhotoService.ViderCache();

        Debug.Log($"[BatimentManager] Bâtiment '{id}' supprimé");
    }

    /// Déplace le dossier disque d'un bâtiment vers la corbeille. Renvoie l'emplacement
    /// atteint, ou null si le bâtiment n'avait pas encore de dossier. Un échec est
    /// signalé plutôt que taire : la suppression logique, elle, a déjà eu lieu.
    static string DeplacerDossierEnCorbeille(string nomBatiment)
    {
        if (string.IsNullOrWhiteSpace(nomBatiment)) return null;
        try
        {
            string source = DossiersDonnees.DossierBatiment(nomBatiment);
            if (!Directory.Exists(source)) return null;

            Directory.CreateDirectory(DossierCorbeilleBatiments);
            string dest = Path.Combine(DossierCorbeilleBatiments,
                Guid.NewGuid().ToString("N") + "_" + new DirectoryInfo(source).Name);
            Directory.Move(source, dest);
            return dest;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BatimentManager] Dossier de « {nomBatiment} » non déplacé en " +
                           $"corbeille : {e.Message} — il pourra bloquer un futur renommage.");
            return null;
        }
    }

    /// Remet le dossier d'un bâtiment restauré à son emplacement. Sans effet si la
    /// suppression n'avait pas de dossier, ou si un dossier occupe déjà la place
    /// (on ne l'écrase pas : mieux vaut un dossier en corbeille qu'un écrasement).
    static void RestaurerDossierDepuisCorbeille(Batiment data)
    {
        if (data == null || !_dossiersEnCorbeille.TryGetValue(data.id, out string corbeille)) return;
        _dossiersEnCorbeille.Remove(data.id);

        try
        {
            string destination = DossiersDonnees.DossierBatiment(data.Name);
            if (!Directory.Exists(corbeille) || Directory.Exists(destination)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            Directory.Move(corbeille, destination);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BatimentManager] Dossier de « {data.Name} » non restauré depuis la " +
                           $"corbeille : {e.Message} — photos et factures sont dans {corbeille}.");
        }
    }

    // ── Restauration (annuler une suppression) ───────────────────────────────

    public void RestoreBatiment(string json)
    {
        var data = JsonUtility.FromJson<Batiment>(json);
        if (data == null) return;

        _batiments.Add(data);
        RestaurerDossierDepuisCorbeille(data);   // photos et factures reviennent avec lui
        SaveBatiment(data);   // réécrit le fichier JSON supprimé

        var prefab = SpawnPrefabInPanel(data, batimentsContainerPanel, false);
        BatimentPrefab.Add(prefab);
        menuManager.CreateTab(prefab);
        menuManager.OnSelect(prefab);
        prefab.RefreshBatimentTabAlert();

        Debug.Log($"[BatimentManager] Bâtiment '{data.Name}' restauré");
    }

    // ── Sauvegarde — 1 JSON par bâtiment ─────────────────────────────────────

    /// Renvoie true si l'écriture disque a réussi. Le retour peut être ignoré par
    /// les appelants existants, mais permet d'afficher un échec au lieu de laisser
    /// croire à une sauvegarde qui n'a pas eu lieu.
    public bool SaveBatiment(Batiment data)
    {
        // Synchronise la liste en mémoire : les prefabs travaillent sur des
        // copies (clone JSON) — sans ça, Batiments reste périmée et le menu
        // home (objectifs, stats) ne voit pas les changements.
        int idx = _batiments.FindIndex(b => b.id == data.id);
        if (idx >= 0) _batiments[idx] = data;
        else _batiments.Add(data);

        Directory.CreateDirectory(SaveFolder);
        string json = JsonUtility.ToJson(data, prettyPrint: true);

        // Écriture atomique : une coupure pendant l'écriture ne peut plus laisser
        // un JSON tronqué (qui empêchait ensuite l'app de démarrer, cf. LoadAll).
        if (!AtomicFile.WriteAllText(GetFilePath(data.id), json, out string erreur))
        {
            Debug.LogError($"[BatimentManager] ÉCHEC de sauvegarde de '{data.Name}' — {erreur}. " +
                           "Les modifications ne sont PAS enregistrées sur le disque.");
            return false;
        }

        Debug.Log($"[BatimentManager] Sauvegardé → {GetFilePath(data.id)}");
        return true;
    }

    public void SaveAll()
    {
        // Itération sur une COPIE : SaveBatiment réaffecte `_batiments[idx]`, or
        // l'indexeur de List<T> incrémente sa version interne et invalide donc
        // l'énumérateur en cours (InvalidOperationException dès le 2e élément).
        foreach (var b in _batiments.ToArray())
            SaveBatiment(b);
    }

    // ── Chargement ───────────────────────────────────────────────────────────

    // Cession prévue arrivée à son jour (retour du 06/10) : le dossier suit le nouveau nom,
    // comme un renommage par « Modifier ». Nom pris ou dossier bloqué : on réessaie au
    // prochain lancement. ponytail: vérifiée au lancement seulement ; une application
    // restée ouverte passé le jour de la cession attend le lancement suivant.
    static bool EffectuerCession(Batiment bat, Locataire loc)
    {
        var p = Cessions.Due(loc, DateTime.Today);
        if (p == null) return false;
        bool pris = !DossiersDonnees.MemeDossier(loc.Name, p.nom)
            && bat.locataireDuBatiment.Exists(l => l != null && l.id != loc.id && DossiersDonnees.MemeDossier(l.Name, p.nom));
        string err = pris ? "nom déjà pris dans le bâtiment" : null;
        if (pris || !DossiersDonnees.RenommerLocataire(bat.Name, loc.Name, p.nom, out err))
        {
            Debug.LogWarning($"[Cession] « {loc.Name} » → « {p.nom} » reportée au prochain lancement : {err}");
            return false;
        }
        return Cessions.Effectuer(loc, DateTime.Today);
    }

    public void LoadAll()
    {
        _batiments.Clear();

        // Vide les prefabs existants
        foreach (Transform child in batimentsContainer)
            Destroy(child.gameObject);

        if (!Directory.Exists(SaveFolder))
        {
            Debug.Log("[BatimentManager] Aucun dossier de sauvegarde trouvé");
            return;
        }

        Debug.Log(SaveFolder);
        _racineChargement = SaveLocationService.GetSaveRoot();   // référence pour la sauvegarde de fermeture
        string[] files = Directory.GetFiles(SaveFolder, "*.json");
        var illisibles = new List<string>();

        foreach (string file in files)
        {
            // Chaque fichier est isolé : un JSON tronqué (coupure pendant une
            // écriture) ne doit PAS interrompre la boucle. Sans ce try, l'exception
            // remontait hors de Start() et le menu général n'était jamais ouvert :
            // l'application restait bloquée sur un écran vide, sans message.
            try
            {
                string json = File.ReadAllText(file);
                Batiment data = JsonUtility.FromJson<Batiment>(json);

                if (data != null)
                {
                    // Charges au format « facturée pour tous » → par locataire, d'après
                    // le suivi. Avant la création de la fiche : elle travaille sur une copie.
                    bool reconstitue = ChargeBatiment.Reconstituer(data);
                    // Loyer à paliers : le loyer courant suit le palier du jour. Cession
                    // prévue dont le jour est arrivé : la fiche change de titulaire.
                    bool cede = false;
                    if (data.locataireDuBatiment != null)
                        foreach (var loc in data.locataireDuBatiment)
                        {
                            Loyers.Actualiser(loc, DateTime.Today);
                            cede |= EffectuerCession(data, loc);
                        }
                    _batiments.Add(data);
                    if (reconstitue || cede) SaveBatiment(data);
                    var prefab = SpawnPrefabInPanel(data, batimentsContainerPanel, false);
                    BatimentPrefab.Add(prefab);
                    menuManager.CreateTab(prefab);
                    prefab.RefreshBatimentTabAlert();
                }
                else
                {
                    illisibles.Add(Path.GetFileName(file));
                }
            }
            catch (Exception e)
            {
                illisibles.Add(Path.GetFileName(file));
                Debug.LogError($"[BatimentManager] Fichier illisible ignoré : {Path.GetFileName(file)} — {e.Message}");
            }
        }

        if (BatimentPrefab.Count > 0)
            menuManager.OnSelect(BatimentPrefab[0]);
        Debug.Log($"[BatimentManager] {_batiments.Count} bâtiment(s) chargé(s)");

        // Les fichiers illisibles sont signalés explicitement : un bâtiment qui
        // disparaît en silence est pire qu'une erreur affichée.
        if (illisibles.Count > 0)
            Debug.LogError($"[BatimentManager] {illisibles.Count} fichier(s) non chargé(s) : {string.Join(", ", illisibles)}. " +
                           "Une sauvegarde de secours existe peut-être dans le dossier Backups.");
    }


    // ── Utilitaires ──────────────────────────────────────────────────────────



    // Change la visibilité de SpawnPrefab et ajoute un paramètre parent
    public BatimentPrefab SpawnPrefabInPanel(Batiment data, Transform parent, bool needToModify)
    {
        var go = Instantiate(batimentPrefab, parent);

        // 1. Désactive le CSF AVANT qu'il ne recalcule la taille
        var csf = go.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;

        // 2. Force le stretch pour remplir le parent
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // 3. Si ContentPanel a un VLG, flexibleHeight=1 garantit qu'il prend tout l'espace
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;

        var prefab = go.GetComponent<BatimentPrefab>();
        prefab.InitializeBatiment(data, needToModify);
        return prefab;
    }



    private string GetFilePath(string id) =>
        Path.Combine(SaveFolder, $"batiment_{id}.json");

}