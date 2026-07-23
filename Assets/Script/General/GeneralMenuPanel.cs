using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GeneralMenuPanel : MonoBehaviour
{
    public static GeneralMenuPanel Instance { get; private set; }

    [Header("Références")]
    public MenuManager menuManager;
    public BatimentManager batimentManager;

    [Header("Résumé global")]
    public TMP_Text txtNbBatiments;      // "3 bâtiments · 9 locataires" (en-tête)
    public TMP_Text txtLoyerTotal;       // KPI loyers perçus
    public TMP_Text txtInvesti;          // KPI investi (achats + travaux)
    public Button btnNouveauBatiment;

    [Header("À traiter")]
    public GameObject alertesSection;
    public Transform alertesContainer;
    public GameObject alertRowPrefab;
    public TMP_Text txtNbAlertes;
    public GameObject txtAucuneAlerte;
    public Button btnToutVoirAlertes;    // ouvre la section objectifs (optionnel)
    public int maxAlertesAffichees = 3;

    [Header("Calcul rapide")]
    public QuickCalcInline quickCalc;

    [Header("Bâtiments — adaptatif")]
    public int seuilListe = 10;                 // ≤ seuil → cartes ; > seuil → liste
    public TMP_InputField rechercheInput;
    public TMP_Dropdown triDropdown;            // 0=alertes, 1=loyer↓, 2=nom
    public GameObject listHeader;               // en-tête de colonnes (visible en mode liste)
    public Transform buildingsGrid;             // parent des cartes
    public GameObject buildingCardPrefab;
    public GameObject buildingsListPanel;       // conteneur liste (avec en-tête colonnes)
    public Transform buildingsListContainer;    // parent des lignes
    public GameObject buildingRowPrefab;

    [Header("Outils")]
    public Button btnPLU;
    public Button btnObjectifs;
    public Button btnSauvegardes;
    public GameObject objectifsPanel;
    public GameObject sauvegardesPanel;

    [Header("Sections")]
    public GlobalObjectivesSection objectivesSection;

    // ── Internes ──────────────────────────────────────────────────────────────

    private readonly List<GameObject> _cards = new();
    private readonly List<GameObject> _rows = new();
    private readonly List<GameObject> _alertRows = new();
    private string _recherche = "";
    private int _tri = 0;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        btnPLU?.onClick.AddListener(() => PLUOverlayPanel.Instance.OpenFreeSearch());
        btnObjectifs?.onClick.AddListener(() => TogglePanel(objectifsPanel));
        btnSauvegardes?.onClick.AddListener(() => TogglePanel(sauvegardesPanel));

        btnNouveauBatiment?.onClick.AddListener(() =>
        {
            var prefab = batimentManager.AddBatiment(true);
            menuManager.CreateTab(prefab);
            menuManager.OnSelect(prefab, true);
        });

        if (rechercheInput != null)
            rechercheInput.onValueChanged.AddListener(r => { _recherche = r; RebuildBuildings(); });
        if (triDropdown != null)
            triDropdown.onValueChanged.AddListener(t => { _tri = t; RebuildBuildings(); });
    }

    // ── API publique ──────────────────────────────────────────────────────────

    public void Show()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide() => gameObject.SetActive(false);

    public void Refresh()
    {
        UpdateStats();
        BuildAlertes();
        RebuildBuildings();
        // Section objectifs détaillée : masquée par défaut (les objectifs
        // vivent dans « À traiter ») — on ne la rafraîchit que si visible.
        if (objectivesSection != null && objectivesSection.gameObject.activeInHierarchy)
            objectivesSection.Refresh();
    }

    // ── Stats globales ────────────────────────────────────────────────────────

    private void UpdateStats()
    {
        int nbBat = batimentManager.Batiments.Count;
        int nbLoc = 0;
        float loyerTotal = 0f, investi = 0f;

        foreach (var prefab in batimentManager.BatimentPrefab)
        {
            nbLoc += prefab.listLocataire.Count;
            loyerTotal += prefab.GetLoyerTotal();

            var data = prefab.getBatiment();
            if (data.historiquesAchat != null)
                foreach (var a in data.historiquesAchat)
                    investi += a.prixAchat + a.fraisNotaire + a.fraisAgence;
            if (data.travaux != null)
                foreach (var t in data.travaux)
                    investi += t.coutTotal;
        }

        if (txtNbBatiments != null)
            txtNbBatiments.text = $"{nbBat} bâtiment{(nbBat > 1 ? "s" : "")} · {nbLoc} locataire{(nbLoc > 1 ? "s" : "")}";
        if (txtLoyerTotal != null) txtLoyerTotal.text = $"{loyerTotal:N0} €";
        if (txtInvesti != null) txtInvesti.text = FormatInvesti(investi);
    }

    private static string FormatInvesti(float v)
        => v >= 1_000_000f ? $"{v / 1_000_000f:0.0#} M€" : $"{v:N0} €";

    // ── Zone « À traiter » ────────────────────────────────────────────────────

    private void BuildAlertes()
    {
        foreach (var r in _alertRows) Destroy(r);
        _alertRows.Clear();

        var alertes = HomeAlertCollector.Collect(batimentManager.BatimentPrefab);

        if (txtNbAlertes != null) txtNbAlertes.text = alertes.Count.ToString();
        txtAucuneAlerte?.SetActive(alertes.Count == 0);
        alertesSection?.SetActive(alertes.Count > 0);

        // Tant que le prefab/conteneur ne sont pas câblés, on n'instancie rien.
        if (alertRowPrefab == null || alertesContainer == null) return;

        int n = Mathf.Min(alertes.Count, maxAlertesAffichees);
        for (int i = 0; i < n; i++)
        {
            var go = Instantiate(alertRowPrefab, alertesContainer);
            go.GetComponent<HomeAlertRowUI>().Setup(alertes[i], OuvrirAlerte);
            _alertRows.Add(go);
        }

        if (btnToutVoirAlertes != null)
            btnToutVoirAlertes.gameObject.SetActive(alertes.Count > maxAlertesAffichees);
    }

    private void OuvrirAlerte(HomeAlert alerte)
    {
        Hide();
        menuManager.OnSelect(alerte.batiment);
        if (alerte.locataire != null &&
            alerte.batiment.dictionnairelocataire.TryGetValue(alerte.locataire, out var locPrefab))
        {
            alerte.batiment.ShowLocataireView();
            alerte.batiment.menulocataire.OnSelect(locPrefab);
        }
        else
        {
            alerte.batiment.ShowSummary();
        }
    }

    // ── Bâtiments (adaptatif cartes / liste) ──────────────────────────────────

    private bool _containerReady;

    // Prépare le conteneur unique : ContentSizeFitter pour le scroll,
    // et supprime l'ancien panneau liste séparé.
    private void EnsureBuildingsContainer()
    {
        if (_containerReady || buildingsGrid == null) return;
        _containerReady = true;

        var csf = buildingsGrid.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = buildingsGrid.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollView = buildingsGrid.parent != null ? buildingsGrid.parent.parent : null;
        var section = scrollView != null ? scrollView.parent : null;
        if (section != null)
        {
            var old = section.Find("BuildingsList");
            if (old != null) Destroy(old.gameObject);
        }
        buildingsListPanel = null;
        buildingsListContainer = null;
    }

    private void RebuildBuildings()
    {
        if (buildingsGrid == null) return;
        EnsureBuildingsContainer();

        _cards.Clear();
        _rows.Clear();
        var aDetruire = new List<GameObject>();
        foreach (Transform c in buildingsGrid) aDetruire.Add(c.gameObject);
        foreach (var g in aDetruire) Destroy(g);

        var liste = FiltrerTrier(batimentManager.BatimentPrefab);
        bool modeListe = batimentManager.BatimentPrefab.Count > seuilListe;

        // En-tête de colonnes visible uniquement en mode liste
        listHeader?.SetActive(modeListe);

        // Un seul GridLayoutGroup, reconfiguré selon le mode
        var grid = buildingsGrid.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            float largeur = (buildingsGrid as RectTransform).rect.width;
            grid.padding = new RectOffset(6, 6, 6, 6);
            if (modeListe)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 1;
                grid.cellSize = new Vector2(largeur - 12f, 40f);
                grid.spacing = new Vector2(0f, 5f);
            }
            else
            {
                grid.constraint = GridLayoutGroup.Constraint.Flexible;
                grid.cellSize = new Vector2(210f, 150f);
                grid.spacing = new Vector2(10f, 10f);
            }
        }

        if (modeListe && buildingRowPrefab != null)
        {
            foreach (var bp in liste)
            {
                var go = Instantiate(buildingRowPrefab, buildingsGrid);
                go.GetComponent<BuildingRowUI>().Setup(bp, OnBuildingClicked);
                _rows.Add(go);
            }
        }
        else if (buildingCardPrefab != null)
        {
            foreach (var bp in liste)
            {
                var go = Instantiate(buildingCardPrefab, buildingsGrid);
                go.GetComponent<BuildingCard>().Setup(bp, OnBuildingClicked);
                _cards.Add(go);
            }
        }
    }

    private List<BatimentPrefab> FiltrerTrier(IEnumerable<BatimentPrefab> source)
    {
        var liste = source.Where(bp =>
        {
            if (string.IsNullOrEmpty(_recherche)) return true;
            string r = _recherche.ToLowerInvariant();
            var data = bp.getBatiment();
            return (data.Name ?? "").ToLowerInvariant().Contains(r)
                || (data.adressBatiment ?? "").ToLowerInvariant().Contains(r);
        });

        switch (_tri)
        {
            case 1: // loyer décroissant
                liste = liste.OrderByDescending(bp => bp.GetLoyerTotal());
                break;
            case 2: // nom A→Z
                liste = liste.OrderBy(bp => bp.getName());
                break;
            default: // alertes (plus urgent en premier), puis nom
                liste = liste
                    .OrderBy(bp => BatimentEtatHelper.Priorite(BatimentEtatHelper.GetEtat(bp)))
                    .ThenBy(bp => bp.getName());
                break;
        }
        return liste.ToList();
    }

    private void OnBuildingClicked(BatimentPrefab batiment)
    {
        Hide();
        menuManager.OnSelect(batiment);
        batiment.ShowSummary();
    }

    // ── Outils ────────────────────────────────────────────────────────────────

    private void TogglePanel(GameObject panel)
    {
        if (panel == null) return;
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }
}
