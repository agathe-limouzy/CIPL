using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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

    [Header("Navigation tabs")]
    public ScrollRect tabScrollRect;      // ScrollRect horizontal de la barre d'onglets (optionnel)
    public TMP_InputField searchField;    // Champ de recherche pour filtrer les onglets (optionnel)

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

        if (searchField != null)
            searchField.onValueChanged.AddListener(FilterTabs);
    }

    // ── Filtre de recherche ──────────────────────────────────────────────────
    private void FilterTabs(string recherche)
    {
        string r = recherche.Trim().ToLowerInvariant();

        foreach (var pair in dictionnaryMenu)
        {
            bool visible = string.IsNullOrEmpty(r)
                || pair.Key.getName().ToLowerInvariant().Contains(r);
            pair.Value.gameObject.SetActive(visible);
        }
    }

    // ── Auto-scroll vers l'onglet actif ──────────────────────────────────────
    private void ScrollToTab(MenuItem tab)
    {
        if (tabScrollRect == null || tab == null) return;
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(ScrollToTabRoutine(tab));
    }

    private IEnumerator ScrollToTabRoutine(MenuItem tab)
    {
        yield return null;   // attendre le layout
        if (tab == null) yield break;
        Canvas.ForceUpdateCanvases();

        RectTransform content  = tabScrollRect.content;
        RectTransform viewport = tabScrollRect.viewport;
        RectTransform tabRect  = tab.transform as RectTransform;

        float contentWidth  = content.rect.width;
        float viewportWidth = viewport.rect.width;
        if (contentWidth <= viewportWidth) yield break;   // tout tient, rien à faire

        // Position du centre de l'onglet dans le content
        float tabCenter = Mathf.Abs(tabRect.anchoredPosition.x) + tabRect.rect.width * 0.5f;

        // Normalise pour centrer l'onglet dans le viewport
        float target = (tabCenter - viewportWidth * 0.5f) / (contentWidth - viewportWidth);
        tabScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(target);
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

        // Si le bouton "+" est encore dans le content, le garder en dernier
        if (addTabButton != null && addTabButton.transform.parent == ContentItem.transform)
            addTabButton.transform.SetAsLastSibling();

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

        // Centre l'onglet sélectionné dans la barre scrollable
        if (dictionnaryMenu.TryGetValue(prefabSelected, out var selectedTab))
            ScrollToTab(selectedTab);

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
