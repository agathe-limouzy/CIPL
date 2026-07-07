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
        BackupService.RunStartupBackup();
        LoadAll();
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

        LoadAll();
    }

    // ── Ajout ────────────────────────────────────────────────────────────────

    public BatimentPrefab AddBatiment(bool needToModify)
    {

        var data = new Batiment();
       var prefab= SpawnPrefabInPanel(data, menuManager.ContentPrefab.transform, needToModify);
        _batiments.Add(data);
        BatimentPrefab.Add(prefab);
        //SaveBatiment(data.getBatiment());
        return prefab;
      
        
    }

    // ── Suppression ──────────────────────────────────────────────────────────

    public void DeleteBatiment(string id)
    {
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

        Debug.Log($"[BatimentManager] Bâtiment '{id}' supprimé");
    }

    // ── Sauvegarde — 1 JSON par bâtiment ─────────────────────────────────────

    public void SaveBatiment(Batiment data)
    {
        Directory.CreateDirectory(SaveFolder);
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(GetFilePath(data.id), json);
        Debug.Log($"[BatimentManager] Sauvegardé → {GetFilePath(data.id)}"+ json);
    }

    public void SaveAll()
    {
        foreach (var b in _batiments)
            SaveBatiment(b);
    }

    // ── Chargement ───────────────────────────────────────────────────────────

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
        string[] files = Directory.GetFiles(SaveFolder, "*.json");
        foreach (string file in files)
        {
            string json = File.ReadAllText(file);
            Batiment data = JsonUtility.FromJson<Batiment>(json);

            if (data != null)
            {
                _batiments.Add(data);
                var prefab= SpawnPrefabInPanel(data, batimentsContainerPanel,false);
                BatimentPrefab.Add(prefab);
                menuManager.CreateTab(prefab);
            }
        }
        if (BatimentPrefab.Count > 0)
            menuManager.OnSelect(BatimentPrefab[0]);
        Debug.Log($"[BatimentManager] {_batiments.Count} bâtiment(s) chargé(s)");
    }

    public Batiment LoadBatiment(string id)
    {
        string path = GetFilePath(id);
        if (!File.Exists(path)) return null;

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<Batiment>(json);
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

    public string GetSaveFolder() => SaveFolder;
}