using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{


    public GameObject ContentItem;
    public GameObject ContentPrefab;
    public Button addTabButton;
    public GameObject tabItemPrefab;

    private PrefabBatLoc _activePrefab;
    public bool IsMenuBatiment = true;
    BatimentPrefab batimentPrefabOwned;

    public Dictionary<PrefabBatLoc, MenuItem> dictionnaryMenu= new Dictionary<PrefabBatLoc, MenuItem>();


    // Start is called before the first frame update

    [Header("Menu Général")]
    public Button btnHome;
    public GeneralMenuPanel generalMenuPanel;
    public GameObject contentArea;   // le panel qui affiche les bâtiments

    // Dans Start() ou Init()


    public void OpenGeneralMenu()
    {
        // Désélectionne tous les tabs visuellement
        foreach (var tab in dictionnaryMenu)
        {
            var tabItem = tab.Value.GetComponent<MenuItem>();
            tabItem.SetActive(false);
        }

        // Cache la zone de contenu bâtiment
        ContentPrefab.SetActive(false);

        // Affiche le menu général
        GeneralMenuPanel.Instance.Show();
    }


    void Start()
    {
        if (IsMenuBatiment)
        {
            addTabButton.onClick.AddListener(OnAddTabBatiment);
            btnHome.onClick.AddListener(OpenGeneralMenu);
        }
        else
        {
            batimentPrefabOwned = GetComponentInParent<BatimentPrefab>();
            addTabButton.onClick.AddListener(OnAddTabLocataire);
        }
    }

  

    private void OnAddTabBatiment()
    {
      

        var prefab = BatimentManager.Instance.AddBatiment(true);
        CreateTab(prefab);
        OnSelect(prefab, true);

    }
    private void OnAddTabLocataire()
    {


        var prefab = batimentPrefabOwned.Addlocataire(true);
        CreateTab(prefab);
        OnSelect(prefab, true);

    }
   


    public void CreateTab(PrefabBatLoc prefabBatLoc)
    {

        var go = Instantiate(tabItemPrefab, ContentItem.transform);
        var tab = go.GetComponent<MenuItem>();

        // Place le bouton "+" toujours en dernier
        go.transform.SetSiblingIndex(ContentItem.transform.childCount - 2);

        tab.Setup(prefabBatLoc, this);
        dictionnaryMenu.Add(prefabBatLoc, tab);

    }

    public void OnSelect(PrefabBatLoc prefabSelected, bool needToModify = false)
    {
        if (IsMenuBatiment)
        {
            // Cache le menu général si visible
            GeneralMenuPanel.Instance?.Hide();

            // Remet le contenu visible
            ContentPrefab.SetActive(true);
        }
        _activePrefab = prefabSelected;

        // Met à jour le style de tous les onglets
        foreach (var tab in dictionnaryMenu)
            tab.Value.SetActive(tab.Value._BatLocLinked == _activePrefab);

        // Affiche le prefab correspondant
        ShowBatimentPrefab(prefabSelected, needToModify);

    }

    private void ShowBatimentPrefab(PrefabBatLoc prefabBatLoc, bool needToModify)
    {
        // Cache tous les prefabs du pool
        foreach (var prefabBatiment in dictionnaryMenu.Keys)
            prefabBatiment.gameObject.SetActive(false);

        // Récupère ou crée le prefab dans le pool
      
        foreach(var key in dictionnaryMenu.Keys)
        {
            if(key== prefabBatLoc)
            {
                key.gameObject.SetActive(true);
                _activePrefab = key;
            }
        }


    }


    public void RemoveTabAndBuilding(PrefabBatLoc batiment)
    {
        Debug.Log("RemoveTab");
        // Supprime l'onglet UI
        PrefabBatLoc tab = null;
        foreach(var key in dictionnaryMenu.Keys)
        {
            if (key == batiment)
            {
                tab = key;
            }
        }
        if (tab != null)
        {
            Debug.Log("RemoveTab2");
            Destroy(dictionnaryMenu[tab].gameObject);
            dictionnaryMenu.Remove(tab);
           
        }

       

      

        // Sélectionne un autre onglet si possible
        if (dictionnaryMenu.Count > 0)
        {
            OnSelect(dictionnaryMenu.First().Key);
            Debug.Log("RemoveTab4");
        }
        else
            _activePrefab = null;
    }


    public void UpdateTabLabel(PrefabBatLoc batiment, string newLabel)
    {

        MenuItem tab = null;
        foreach (var key in dictionnaryMenu.Keys)
        {
            if (key == batiment)
            {
                tab = dictionnaryMenu[key];
            }
        }
        tab?.UpdateLabel(TruncateLabel(newLabel));
    }

    private string TruncateLabel(string label) =>
    label.Length > 20 ? label.Substring(0, 20) + "..." : label;

}
