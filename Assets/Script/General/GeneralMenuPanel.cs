using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GeneralMenuPanel : MonoBehaviour
{
    public static GeneralMenuPanel Instance { get; private set; }

    [Header("Références")]
    public MenuManager menuManager;
    public BatimentManager batimentManager;

    [Header("Bâtiments")]
    public Transform buildingsGrid;          // parent des cards
    public GameObject buildingCardPrefab;    // prefab card bâtiment

    [Header("Outils")]
    public Button btnPLU;

    [Header("Résumé global")]
    public TMP_Text txtNbBatiments;
    public TMP_Text txtNbLocataires;
    public TMP_Text txtLoyerTotal;
  
    [Header("Sections")]
    public GlobalObjectivesSection objectivesSection;
    // RevisionAlertsSection supprimée — intégrée dans BuildingCard

 

    // ── Internes ──────────────────────────────────────────────────────────────

    private readonly List<GameObject> _cards = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        btnPLU?.onClick.AddListener(() => PLUOverlayPanel.Instance.OpenFreeSearch());
    }

    // ── API publique ──────────────────────────────────────────────────────────

    public void Show()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>Reconstruit les cards et les stats.</summary>
    public void Refresh()
    {
      
        BuildBuildingCards();
        UpdateStats();
        objectivesSection?.Refresh();
    }

    // ── Cards bâtiments ───────────────────────────────────────────────────────

    private void BuildBuildingCards()
    {
        // Vide les anciennes cards
        foreach (var c in _cards) Destroy(c);
        _cards.Clear();

        foreach (var prefab in batimentManager.BatimentPrefab)
        {
            var go = Instantiate(buildingCardPrefab, buildingsGrid);
            var card = go.GetComponent<BuildingCard>();
            card.Setup(prefab, OnCardClicked);
            _cards.Add(go);
        }
    }

    private void OnCardClicked(BatimentPrefab batiment)
    {
        Hide();
        menuManager.OnSelect(batiment);
    }

    // ── Stats globales ────────────────────────────────────────────────────────

    private void UpdateStats()
    {
        int nbBat = batimentManager.Batiments.Count;
        int nbLoc = 0;
        float loyerTotal = 0f;

        foreach (var prefab in batimentManager.BatimentPrefab)
        {
            nbLoc += prefab.listLocataire.Count;
            loyerTotal += prefab.GetLoyerTotal();
        }

        if (txtNbBatiments != null) txtNbBatiments.text = $"{nbBat} bâtiment{(nbBat > 1 ? "s" : "")}";
        if (txtNbLocataires != null) txtNbLocataires.text = $"{nbLoc} locataire{(nbLoc > 1 ? "s" : "")}";
        if (txtLoyerTotal != null) txtLoyerTotal.text = $"{loyerTotal:F2} € / an";
    }
}