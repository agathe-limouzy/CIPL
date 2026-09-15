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
    public Sprite creanceIcon;           // icône de la bande « Créances » (sec_coins)

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

    // Rangée de tuiles KPI GLOBALES (agrégées sur tous les bâtiments), en haut du menu.
    private Transform _kpiGlobaux;
    private TMP_Text _gLoyer, _gDepense, _gInvesti, _gGagne, _gRend, _gCash;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        btnPLU?.onClick.AddListener(() => PLUOverlayPanel.Instance.OpenFreeSearch());
        btnObjectifs?.onClick.AddListener(() => TogglePanel(objectifsPanel));

        // Le bouton « Sauvegardes » / « Charger une save » est masqué : le multi-
        // entreprise (bouton « Entreprises » → « Ouvrir un dossier existant ») le
        // remplace. On le désactive APRÈS les bootstraps (AfterSceneLoad) qui le
        // clonent pour créer « Réglage » et « Entreprises » — donc sans les casser.
        if (btnSauvegardes != null)
            btnSauvegardes.gameObject.SetActive(false);

        btnNouveauBatiment?.onClick.AddListener(() =>
        {
            var prefab = batimentManager.AddBatiment(true);
            menuManager.CreateTab(prefab);
            menuManager.OnSelect(prefab, true);
        });

        if (rechercheInput != null)
            rechercheInput.onValueChanged.AddListener(r => { _recherche = r; RebuildBuildings(); });
        if (triDropdown != null)
        {
            triDropdown.onValueChanged.AddListener(t => { _tri = t; RebuildBuildings(); });
            // Le contrôle « Tri » et sa liste déroulante passaient DERRIÈRE le Scroll
            // View des bâtiments (dessiné après). Un Canvas trié les remonte au-dessus.
            // NB : pas de « ?? » ici — GetComponent d'un composant Unity absent renvoie
            // un faux-null qui n'active pas le fallback ; on teste avec == null (surcharge Unity).
            var cv = triDropdown.GetComponent<Canvas>();
            if (cv == null) cv = triDropdown.gameObject.AddComponent<Canvas>();
            cv.overrideSorting = true; cv.sortingOrder = 100;
            if (triDropdown.GetComponent<GraphicRaycaster>() == null)
                triDropdown.gameObject.AddComponent<GraphicRaycaster>();
            // La liste instanciée (clone du Template) reçoit un Canvas encore au-dessus.
            if (triDropdown.template != null)
            {
                var tcv = triDropdown.template.GetComponent<Canvas>();
                if (tcv == null) tcv = triDropdown.template.gameObject.AddComponent<Canvas>();
                tcv.overrideSorting = true; tcv.sortingOrder = 200;
                if (triDropdown.template.GetComponent<GraphicRaycaster>() == null)
                    triDropdown.template.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        EnsureHomeReorg();
    }

    // ── Réaménagement du menu (À traiter | Créances en colonnes, calcul rapide en bouton) ──

    private FacturationHomeSection _creancesSection;
    private Transform _calculRapide, _calcHome;
    private GameObject _calcScrim;
    private bool _reorgDone;

    private void EnsureHomeReorg()
    {
        if (_reorgDone) return;
        var panel = (RectTransform)transform.Find("Panel");
        if (panel == null) return;
        _reorgDone = true;

        // 1) Masquer le bandeau KPI du haut (Loyers perçus / Investi).
        foreach (Transform c in panel)
            if (c.name.StartsWith("Section Resum")) { c.gameObject.SetActive(false); break; }

        // 2) Ligne « À traiter / Calcul rapide » → deux colonnes « À traiter | Créances ».
        var row = (RectTransform)panel.Find("RowTraiterCalcul");
        if (row != null)
        {
            _calculRapide = row.Find("Calcul Rapide");
            if (_calculRapide != null) _calculRapide.gameObject.SetActive(false);

            var go = new GameObject("Section Creances", typeof(RectTransform));
            go.transform.SetParent(row, false);
            _creancesSection = go.AddComponent<FacturationHomeSection>();
            _creancesSection.Icon = creanceIcon;
            var cle = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            cle.flexibleWidth = 1; cle.minWidth = 300;
        }

        // 3) Répartition de la hauteur (le Panel a un ContentSizeFitter : il ne s'étire
        //    pas, on calcule donc nous-mêmes pour combler la place disponible).
        LayoutHeights();

        // 4) Bouton « Calcul rapide » dans la barre d'outils du bas.
        var ro = panel.Find("RowOutils");
        if (ro != null && btnPLU != null)
        {
            var b = Instantiate(btnPLU, ro);
            b.name = "BtnCalculRapide";
            var lbl = b.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) { lbl.text = "Calcul rapide"; lbl.color = IdentityAccent; }
            // Teinte verte claire (comme les autres boutons du bas), texte vert foncé.
            var img = b.GetComponent<Image>();
            if (img != null) img.color = IdentityClair;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(ToggleCalculRapide);
        }

        // 5) Uniformise les en-têtes de section (bande colorée + titre ~22, couleur d'accent).
        NormalizeHeaders();
    }

    // Colorimétrie « groupée par fonction » (menu principal, choix utilisatrice) :
    // Bâtiments → vert (identité), Créances → ambre (argent), À traiter → gris-bleu (notes).
    static readonly Color IdentityAccent = HexC("#0F6E56");
    static readonly Color IdentityClair  = HexC("#D6ECE3");
    static readonly Color NotesAccent    = HexC("#5C6E85");
    static readonly Color NotesClair     = HexC("#E7ECF2");
    static Color HexC(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // Harmonise « À traiter » et « Bâtiments » (objets de scène) avec « Créances » :
    // même bande colorée + même taille de titre, couleurs par famille de fonction.
    private void NormalizeHeaders()
    {
        var panel = transform.Find("Panel") as RectTransform;
        if (panel == null) return;
        Sprite rounded = null;

        // À traiter → gris-bleu (notes).
        var atHeader = panel.Find("RowTraiterCalcul/A Traiter/Header");
        if (atHeader != null)
        {
            var img = atHeader.GetComponent<Image>(); if (img != null) rounded = img.sprite;
            var le = atHeader.GetComponent<LayoutElement>() ?? atHeader.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 58; le.preferredHeight = 58;
            StyleHeader(atHeader, NotesClair, NotesAccent);
            // Liseré (Image racine de la carte) = accent gris-bleu (il restait en ambre).
            var atCard = atHeader.parent != null ? atHeader.parent.GetComponent<Image>() : null;
            if (atCard != null) atCard.color = NotesAccent;
        }

        // Bâtiments → vert (identité).
        var bTitle = panel.Find("Section Batiment/TitleRow");
        if (bTitle != null)
        {
            var img = bTitle.GetComponent<Image>() ?? bTitle.gameObject.AddComponent<Image>();
            if (rounded != null) { img.sprite = rounded; img.type = Image.Type.Sliced; }
            var le = bTitle.GetComponent<LayoutElement>() ?? bTitle.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 58; le.preferredHeight = 58;
            var hlg = bTitle.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null && hlg.padding.left < 10) hlg.padding = new RectOffset(12, 12, 4, 4);
            StyleHeader(bTitle, IdentityClair, IdentityAccent);
        }
    }

    // Applique à un bandeau d'en-tête : fond = teinte claire, icône + titre = accent,
    // hauteur 58 + titre 22 (uniforme avec la fiche locataire).
    private static void StyleHeader(Transform header, Color band, Color accent)
    {
        var img = header.GetComponent<Image>();
        if (img != null) img.color = band;
        foreach (Transform ch in header)
        {
            var ii = ch.GetComponent<Image>();
            if (ii != null) { ii.color = accent; break; }   // icône (1er enfant Image)
        }
        var t = header.GetComponentInChildren<TMP_Text>(true);
        if (t != null) { t.enableAutoSizing = false; t.fontSize = 22; t.color = accent; }
    }

    // Mise en page statique (pas de mesure du parent : elle oscillait car le parent est
    // lui-même dimensionné au contenu, ce qui créait une boucle de rétroaction).
    private const float BatimentH = 380f;        // en-tête + barre + cartes (agrandi ×2)
    private const float ScrollBatimentH = 300f;  // zone des cartes de bâtiment
    private const float RowTraiterH = 455f;      // hauteur des deux colonnes = leur contenu (11 alertes)

    private void LayoutHeights()
    {
        var panel = transform.Find("Panel") as RectTransform;
        if (panel == null) return;

        // Le bandeau KPI est ré-activé par UpdateStats() : on le remasque à chaque passage.
        foreach (Transform c in panel)
            if (c.name.StartsWith("Section Resum") && c.gameObject.activeSelf) c.gameObject.SetActive(false);

        void Set(Transform t, float hh)
        {
            if (t == null) return;
            var le = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = hh; le.preferredHeight = hh; le.flexibleHeight = 0;
        }

        // Bâtiments : réduit son Scroll View à une rangée (il faisait 300 = 2 rangées).
        var sb = panel.Find("Section Batiment");
        if (sb != null)
        {
            var sv = sb.Find("Scroll View");
            if (sv != null) Set(sv, ScrollBatimentH);
        }

        // « À traiter » : sa liste d'alertes remplit la colonne (texte jusqu'en bas).
        var scrollAlertes = panel.Find("RowTraiterCalcul/A Traiter/ScrollAlertes");
        if (scrollAlertes != null)
        {
            var le = scrollAlertes.GetComponent<LayoutElement>() ?? scrollAlertes.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 150f; le.preferredHeight = 150f; le.flexibleHeight = 1f;
        }

        Set(panel.Find("RowTraiterCalcul"), RowTraiterH);
        Set(sb, BatimentH);
    }

    // Affiche / masque le « Calcul rapide » en superposition centrée.
    private void ToggleCalculRapide()
    {
        if (_calculRapide == null) return;
        if (_calcScrim != null) { CloseCalculRapide(); return; }

        var root = transform.root;
        var scrim = new GameObject("CalcScrim", typeof(RectTransform), typeof(Image), typeof(Button));
        var srt = (RectTransform)scrim.transform;
        srt.SetParent(root, false);
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
        scrim.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);
        scrim.GetComponent<Button>().onClick.AddListener(CloseCalculRapide);
        srt.SetAsLastSibling();
        _calcScrim = scrim;

        _calcHome = _calculRapide.parent;
        _calculRapide.SetParent(srt, false);
        var crt = (RectTransform)_calculRapide;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(.5f, .5f);
        crt.anchoredPosition = Vector2.zero;
        _calculRapide.gameObject.SetActive(true);
    }

    private void CloseCalculRapide()
    {
        if (_calculRapide != null && _calcHome != null)
        {
            _calculRapide.SetParent(_calcHome, false);
            _calculRapide.gameObject.SetActive(false);
        }
        if (_calcScrim != null) Destroy(_calcScrim);
        _calcScrim = null;
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
        AjusteOutilsDansHeader();
        EnsureKpiGlobaux();
        RefreshKpiGlobaux();
        BuildAlertes();
        RebuildBuildings();
        EnsureHomeReorg();
        LayoutHeights();
        NormalizeHeaders();
        _creancesSection?.Setup(batimentManager.BatimentPrefab);
        // Section objectifs détaillée : masquée par défaut (les objectifs
        // vivent dans « À traiter ») — on ne la rafraîchit que si visible.
        if (objectivesSection != null && objectivesSection.gameObject.activeInHierarchy)
            objectivesSection.Refresh();
    }

    // ── Barre d'outils déplacée dans le header ─────────────────────────────────

    // Déplace « RowOutils » (Consulter le PLU / Entreprises / Réglage / Calcul rapide…)
    // depuis le bas du menu vers la barre de titre (à droite), boutons compacts.
    private void AjusteOutilsDansHeader()
    {
        var panel = transform.Find("Panel");
        if (panel == null) return;
        var header = panel.Find("MenuHeader");
        var ro = panel.Find("RowOutils");
        if (header == null || ro == null) return;

        if (ro.parent != header)
        {
            ro.SetParent(header, false);
            ro.SetAsLastSibling();                                   // à droite du header
            var rohlg = ro.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (rohlg != null) { rohlg.childForceExpandWidth = false; rohlg.childControlWidth = true; rohlg.spacing = 6; rohlg.childAlignment = TextAnchor.MiddleRight; }
            var role = ro.GetComponent<LayoutElement>() ?? ro.gameObject.AddComponent<LayoutElement>();
            role.flexibleWidth = 0; role.minWidth = 0;
            // Le bloc titre prend l'espace restant → pousse la barre d'outils à droite.
            var title = header.Find("TitleBlock");
            if (title != null) { var tle = title.GetComponent<LayoutElement>() ?? title.gameObject.AddComponent<LayoutElement>(); tle.flexibleWidth = 1; }
        }

        // Boutons compacts (chaque Refresh, pour rattraper ceux ajoutés au runtime).
        foreach (Transform b in ro)
        {
            if (!b.gameObject.activeSelf || b.GetComponent<UnityEngine.UI.Button>() == null) continue;
            var le = b.GetComponent<LayoutElement>() ?? b.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 170; le.preferredWidth = 170; le.flexibleWidth = 0;
            le.minHeight = 36; le.preferredHeight = 36; le.flexibleHeight = 0;
        }
    }

    // ── Tuiles KPI globales (patrimoine) ───────────────────────────────────────

    private void EnsureKpiGlobaux()
    {
        if (_kpiGlobaux != null) return;
        var panel = transform.Find("Panel");
        if (panel == null) return;
        var row = UIFactory.HBox(panel, 10, false, "KpiGlobaux");
        row.childControlWidth = true; row.childForceExpandWidth = true;
        row.childControlHeight = true; row.childForceExpandHeight = true;
        row.childAlignment = TextAnchor.MiddleLeft;
        UIFactory.LE(row.gameObject, minH: 82, prefH: 82, flexH: 0);
        _gLoyer   = BatimentSummaryView.KpiTile(row.transform, "Loyers / an");
        _gDepense = BatimentSummaryView.KpiTile(row.transform, "Dépensé / an");
        _gInvesti = BatimentSummaryView.KpiTile(row.transform, "Investi");
        _gGagne   = BatimentSummaryView.KpiTile(row.transform, "Total gagné");
        _gRend    = BatimentSummaryView.KpiTile(row.transform, "Rendement net");
        _gCash    = BatimentSummaryView.KpiTile(row.transform, "Cash flow / mois");
        row.transform.SetSiblingIndex(1);   // juste après le header du menu
        _kpiGlobaux = row.transform;
    }

    private void RefreshKpiGlobaux()
    {
        if (_gLoyer == null || batimentManager == null) return;
        float loyers = 0f, mensAn = 0f, chargesAn = 0f, investi = 0f, gagne = 0f;
        foreach (var bp in batimentManager.BatimentPrefab)
        {
            if (bp == null) continue;
            BatimentSummaryView.ComposantesFinancieres(bp.getBatiment(),
                out float lo, out float me, out float ch, out float inv, out float ga);
            loyers += lo; mensAn += me; chargesAn += ch; investi += inv; gagne += ga;
        }
        float cashMois = (loyers - mensAn) / 12f;
        float rend = investi > 0 ? (loyers - chargesAn) / investi * 100f : 0f;

        _gLoyer.text = loyers > 0 ? $"{loyers:N0} €" : "—";
        _gLoyer.color = loyers > 0 ? HexC("#0F6E56") : HexC("#5F5E5A");
        _gDepense.text = mensAn > 0.5f ? $"{mensAn:N0} €" : "0 €";
        _gDepense.color = HexC("#D85A30");
        _gInvesti.text = investi > 0 ? $"{investi:N0} €" : "—";
        _gGagne.text = $"{(gagne >= 0 ? "+" : "")}{gagne:N0} €";
        _gGagne.color = gagne >= 0 ? HexC("#0F6E56") : HexC("#D85A30");
        _gRend.text = rend != 0 ? $"{rend:F1} %" : "—";
        _gRend.color = rend > 0 ? HexC("#0F6E56") : rend < 0 ? HexC("#D85A30") : HexC("#5F5E5A");
        _gCash.text = $"{(cashMois >= 0 ? "+" : "")}{cashMois:N0} €";
        _gCash.color = cashMois >= 0 ? HexC("#0F6E56") : HexC("#D85A30");
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
            default: // alertes : le plus d'alertes d'abord (toutes : révision, dépôt,
                     // régul, fin de bail, objectifs, facturation…), puis sévérité, puis nom.
                liste = liste
                    .OrderByDescending(bp => HomeAlertCollector.Collect(new[] { bp }).Count)
                    .ThenBy(bp => BatimentEtatHelper.Priorite(BatimentEtatHelper.GetEtat(bp)))
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
