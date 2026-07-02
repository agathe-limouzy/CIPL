using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BatimentPrefab : PrefabBatLoc
{
    // Caracterristque 
    public InputAndText nameOfTheBuiding;
    public InputAndText tailleBatimentText;
    public InputAndText tailleTerrainText;

    [Header("Rentabilité")]
    public RentabiliteGlobaleController rentabiliteGlobale;

    public MapController mapController;
    public ObjectivesManager objectivesManager;
    public TMP_Dropdown ParkingDropdown;



    public Button btnPLU;

    private Batiment batiment;

    [Header("Loyer total")]
    public TMP_Text txtLoyerTotalAnnuel;   // Affiché seulement si > 1 locataire
    public GameObject loyerTotalContainer; // Le GameObject parent (label + valeur)


    [Header("Locataire")]
    public List<Locataire> listLocataire = new List<Locataire>();
    public Dictionary<Locataire, LocatairePrefab> dictionnairelocataire = new Dictionary<Locataire, LocatairePrefab>();
    public GameObject locatairePrefab;
    public GameObject locataireContent;
    public MenuManager menulocataire;

    [Header("Sections repliables")]
    public CollapsibleSection[] sections; // assigner dans l'Inspector
    private bool[] _sectionStateSnapshot;

    [Header("Navigation")]
    public Button btnRetourMenu;

    // Méthodes utilitaires pour les cards et les stats
    public Batiment GetBatimentData()
{
    return BatimentManager.Instance.Batiments.Find(b => b.id == batiment.id);
}

public float GetLoyerTotal()
{
    float total = 0f;
    foreach (var loc in listLocataire)
        total += loc.loyerAnnuel;
    return total;
}
public float GetTailleBatiment() => batiment.tailleBatiment;
    public void RefreshTailleBatiment()
    {
        if (listLocataire.Count > 1)
        {
            // Batiment = somme, non éditable
            float total = listLocataire.Sum(l => l.tailleLot);
            batiment.tailleBatiment = total;
            tailleBatimentText.ApplySave(total.ToString());
        }
        else if (listLocataire.Count == 1)
        {
            // Batiment éditable, locataire = miroir
            var loc = listLocataire[0];
            loc.tailleLot = batiment.tailleBatiment;
            if (dictionnairelocataire.TryGetValue(loc, out var locPrefab))
                locPrefab.RefreshTailleLot(batiment.tailleBatiment);
        }
    }

    public void RefreshLoyerTotal()
    {
        if (loyerTotalContainer == null) return;

        bool multiLocataire = listLocataire.Count > 1;
        loyerTotalContainer.SetActive(multiLocataire);

        if (multiLocataire && txtLoyerTotalAnnuel != null)
        {
            float total = GetLoyerTotal();
            txtLoyerTotalAnnuel.text = $"{total:F2} € / an";
        }
        rentabiliteGlobale?.Refresh();
    }

    public override void InitializeBatiment(Batiment newBatiment, bool NeedToModify)
    {
        foreach (var locPrefab in dictionnairelocataire.Values)
        {
            menulocataire.RemoveTabAndBuilding(locPrefab);
            Destroy(locPrefab.gameObject);
        }
        listLocataire.Clear();
        dictionnairelocataire.Clear();
        ParkingDropdown.ClearOptions();

        // ── Listeners — RemoveAllListeners AVANT d'ajouter ───────────────────
        btnPLU.onClick.RemoveAllListeners();
        btnPLU.onClick.AddListener(() =>
            PLUOverlayPanel.Instance.OpenWithBatiment(mapController.GetAdress()));

        btnRetourMenu.onClick.RemoveAllListeners();
        btnRetourMenu.onClick.AddListener(() =>
            BatimentManager.Instance.menuManager.OpenGeneralMenu());
      



        var options = System.Enum.GetNames(typeof(ParkingState))
            .Select(name => new TMP_Dropdown.OptionData(name))
            .ToList();

        ParkingDropdown.AddOptions(options);
        save.onClick.RemoveAllListeners();
        objectivesManager.AddNeObjectif.RemoveAllListeners();
        modifyBatiment.onClick.RemoveAllListeners();
        Delete.onClick.RemoveAllListeners();
        objectivesManager.AddNeObjectif.AddListener(() => updateListObjectif());
        save.onClick.AddListener(() => SaveBatiment());
        modifyBatiment.onClick.AddListener(() => Modify());
       

        if (!NeedToModify)
        {
       
            string json = JsonUtility.ToJson(newBatiment);
            batiment = JsonUtility.FromJson<Batiment>(json);
            if (batiment.historiquesAchat == null)
                batiment.historiquesAchat = new List<AchatFinancement>();
            if (batiment.travaux == null)
                batiment.travaux = new List<TravauxFinancement>();
            rentabiliteGlobale?.Init(this);
            mapController.SetAdress(batiment.adressBatiment);
            tailleBatimentText.ApplySave ( batiment.tailleBatiment.ToString());
            tailleTerrainText.ApplySave(batiment.tailleTerrain.ToString());
            ParkingDropdown.value = (int)batiment.parkingEtat;
            ParkingDropdown.interactable = false;
            
            nameOfTheBuiding.ApplySave(batiment.Name.ToString());
            objectivesManager.LoadObjectives(batiment.objectifs);
            save.gameObject.SetActive(false);
            modifyBatiment.gameObject.SetActive(true);
            Delete.gameObject.SetActive(true);
            
            for(int i =0; i<batiment.locataireDuBatiment.Count; i++)
            {
                listLocataire.Add(batiment.locataireDuBatiment[i]);
              var locatPrefab= SpawnPrefabLocataireInPanel(batiment.locataireDuBatiment[i], locataireContent.transform, false);
                dictionnairelocataire.Add(batiment.locataireDuBatiment[i], locatPrefab);
                menulocataire.CreateTab(locatPrefab);

            }
            if (listLocataire.Count > 0)
                menulocataire.OnSelect(dictionnairelocataire.First().Value);
            RefreshTailleBatiment();
            RefreshLoyerTotal();
        }
        else
        {

            string json = JsonUtility.ToJson(newBatiment);
            batiment = JsonUtility.FromJson<Batiment>(json);
            Modify();
        }

        Delete.onClick.AddListener(() => BatimentManager.Instance.DeleteBatiment(batiment.id));
    }



    private void updateListObjectif()
    {
        batiment.objectifs = objectivesManager.GetAllTheObjectif();
        BatimentManager.Instance.SaveBatiment(batiment);
    }


    public LocatairePrefab SpawnPrefabLocataireInPanel(Locataire data, Transform parent, bool needToModify)
    {
        var go = Instantiate(locatairePrefab, parent);
        var prefab = go.GetComponent<LocatairePrefab>();
        prefab.batimentPrefabOrigin = this;
        // Câbler automatiquement le ScrollAutoResize
        prefab.locataireScrollContent =locataireContent.GetComponent<ScrollAutoResize>();
        prefab.InitializeLocataire(data, needToModify);
        return prefab;
    }


    public LocatairePrefab Addlocataire(bool needToModify)
    {

        var data = new Locataire();
        var prefab = SpawnPrefabLocataireInPanel(data, menulocataire.ContentPrefab.transform, needToModify);
        listLocataire.Add(data);
        dictionnairelocataire.Add(data, prefab);
        batiment.locataireDuBatiment.Add(data);

        RefreshTailleBatiment();
        RefreshLoyerTotal(); // ← ajouter
        return prefab;


    }

    public void DeleteLocataire(string id)
    {
        var locataire = listLocataire.Find(b => b.id == id);
        listLocataire.RemoveAll(b => b.id == id);
        var tab = dictionnairelocataire[locataire];
        dictionnairelocataire.Remove(locataire);
        if (tab != null)
        {
            menulocataire.RemoveTabAndBuilding(tab);
            Destroy(tab.gameObject);
        }
      
        batiment.locataireDuBatiment.Remove(locataire);
        RefreshTailleBatiment();
        RefreshLoyerTotal(); // ← ajouter
        BatimentManager.Instance.SaveBatiment(batiment);
    }


    public void SaveAfterModifyToDoListLocataire()
    {
        RefreshTailleBatiment();
        RefreshLoyerTotal();
        BatimentManager.Instance.SaveBatiment(batiment);
    }

    public override void Modify()
    {
        nameOfTheBuiding.Modify();
        if (listLocataire.Count <= 1)
            tailleBatimentText.Modify(); // éditable seulement si 0 ou 1 locataire
                                         // si > 1 : reste en lecture seule (somme calculée)
        tailleTerrainText.Modify();
        mapController.ModifyAdress();
        ParkingDropdown.interactable = true;
        save.gameObject.SetActive(true);
        modifyBatiment.gameObject.SetActive(false);
        Delete.gameObject.SetActive(true);
        _sectionStateSnapshot = new bool[sections.Length];
        for (int i = 0; i < sections.Length; i++)
            _sectionStateSnapshot[i] = sections[i].IsOpen;
        foreach (var s in sections) s.Open();
    }


    public override void SaveBatiment()
    {
    
        batiment.Name = nameOfTheBuiding.GetNewSave();
        if (listLocataire.Count > 1)
        {
            // Calculé automatiquement, déjà mis à jour par RefreshTailleBatiment
            float total = listLocataire.Sum(l => l.tailleLot);
            batiment.tailleBatiment = total;
            tailleBatimentText.ApplySave(total.ToString());
        }
        else
        {
            SaveCorrectlyFloat(ref batiment.tailleBatiment, tailleBatimentText.GetNewSave());
            // Propage au locataire unique si besoin
            if (listLocataire.Count == 1)
            {
                listLocataire[0].tailleLot = batiment.tailleBatiment;
                if (dictionnairelocataire.TryGetValue(listLocataire[0], out var locPrefab))
                    locPrefab.RefreshTailleLot(batiment.tailleBatiment);
            }
        }
        SaveCorrectlyFloat(ref batiment.tailleTerrain, tailleTerrainText.GetNewSave());
        Debug.Log(batiment.tailleBatiment);
        batiment.adressBatiment = mapController.GetAdress();
        batiment.parkingEtat = (ParkingState)ParkingDropdown.value;
        ParkingDropdown.interactable = false;
        BatimentManager.Instance.menuManager.UpdateTabLabel(this, batiment.Name);
        BatimentManager.Instance.SaveBatiment(batiment);
        save.gameObject.SetActive(false);
        modifyBatiment.gameObject.SetActive(true);
        Delete.gameObject.SetActive(true);
        rentabiliteGlobale?.Refresh();
        if (_sectionStateSnapshot != null)
            for (int i = 0; i < sections.Length; i++)
                sections[i].SetOpen(_sectionStateSnapshot[i]);
        InitializeBatiment(batiment, false);
    }
   public override string getName()
    {
        return batiment.Name;
    }
    public override  string getID()
    {
        return batiment.id;
    }

    public Batiment getBatiment()
    {
        return batiment;
    }

   


}
