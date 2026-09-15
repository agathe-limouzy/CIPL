using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Vue résumé d'un bâtiment : carte d'identité (vignette, pilules, infos clés)
/// + liste des locataires + bande financière (loyers, cash flow, rendement).
public class BatimentSummaryView : MonoBehaviour
{
    [Header("Carte bâtiment")]
    public RawImage vignetteCarte;     // miroir de la carte Mapbox de la fiche
    public TMP_Text txtNom;
    public TMP_Text txtAdresse;
    public TMP_Text txtPiluleSurface;
    public TMP_Text txtPiluleLots;
    public TMP_Text txtPiluleParking;
    public TMP_Text txtAcquis;
    public TMP_Text txtTerrain;
    public TMP_Text txtTravaux;
    public TMP_Text txtObjectifs;
    public TMP_Text txtLoyerAnnuel;    // Loyers /an (cellule verte de la grille)
    public TMP_Text txtInvesti;        // Investi total (achats + travaux)
    public TMP_Text txtRendement;      // Rendement net %
    public TMP_Text txtCadastre;       // Référence cadastrale
    public TMP_Text txtPiluleEtat;     // pilule d'état (vacance)
    public Button btnFicheComplete;

    [Header("Locataires")]
    public TMP_Text txtTitreLocataires;
    public Button btnAjouterLocataire;
    public Transform rowContainer;     // rempli par BatimentPrefab.RebuildLocataireRows

    [Header("Bande financière")]
    public TMP_Text txtFinances;
    public Button btnDetailsFinanciers;

    private BatimentPrefab _bp;

    // Tableau de bord ajouté par code : carte « À traiter » (par bâtiment) + KPI agrandis.
    private Transform _aTraiterCard, _aTraiterRows;
    private TMP_Text _aTraiterTitre;
    private bool _kpiEnlarged;

    // Tuiles KPI crème (remplacent la grille plate InfoGrid) + ligne d'infos secondaires.
    private bool _kpiBuilt;
    private TMP_Text _tLoyer, _tInvesti, _tRend, _tCash, _secInfo, _tDepense, _tGagne;

    // « À traiter » + « Locataires » sur une même ligne (2 colonnes).
    private bool _twoColBuilt;
    private GameObject _countBadge;
    private TMP_Text _countTxt;
    static readonly Color NotesAccent = HexC("#5C6E85"), NotesClair = HexC("#E7ECF2");
    static readonly Color IdentityAccent = HexC("#0F6E56"), IdentityClair = HexC("#D6ECE3");
    bool _locBandBuilt;
    static Color HexC(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    public void Init(BatimentPrefab bp)
    {
        _bp = bp;

        btnFicheComplete.onClick.RemoveAllListeners();
        btnFicheComplete.onClick.AddListener(() => _bp.ShowFiche());

        if (btnDetailsFinanciers != null)
        {
            btnDetailsFinanciers.onClick.RemoveAllListeners();
            btnDetailsFinanciers.onClick.AddListener(() => _bp.ShowFiche());
        }

        if (btnAjouterLocataire != null)
        {
            btnAjouterLocataire.onClick.RemoveAllListeners();
            btnAjouterLocataire.onClick.AddListener(() =>
            {
                _bp.ShowLocataireView();
                var prefab = _bp.Addlocataire(true);
                _bp.menulocataire.CreateTab(prefab);
                _bp.menulocataire.OnSelect(prefab, true);
            });
        }

        // La vignette suit la carte de la fiche (même texture)
        if (_bp.mapController != null)
        {
            _bp.mapController.AdressUpdate.RemoveListener(RefreshVignette);
            _bp.mapController.AdressUpdate.AddListener(RefreshVignette);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_bp == null) return;
        var bat = _bp.getBatiment();
        if (bat == null) return;

        EnsureKpiTiles();

        Set(txtNom, string.IsNullOrEmpty(bat.Name) ? "Sans nom" : bat.Name);
        Set(txtAdresse, string.IsNullOrEmpty(bat.adressBatiment) ? "Adresse non renseignée" : bat.adressBatiment);

        // Pilules
        Set(txtPiluleSurface, $"{bat.tailleBatiment:F0} m²");
        int nbLots = bat.locataireDuBatiment.Count;
        Set(txtPiluleLots, nbLots > 1 ? $"{nbLots} lots" : $"{nbLots} lot");
        Set(txtPiluleParking, LabelParking(bat.parkingEtat));

        // Grille d'infos
        Set(txtAcquis, DateAcquisition(bat));
        Set(txtTerrain, bat.tailleTerrain > 0 ? $"{bat.tailleTerrain:F0} m²" : "—");
        Set(txtTravaux, ResumeTravaux(bat));
        Set(txtObjectifs, ResumeObjectifs(bat));

        // Loyer /an mis en avant + pilule état
        float loyerTotal = _bp.GetLoyerTotal();
        Set(txtLoyerAnnuel, loyerTotal > 0 ? $"{loyerTotal:N0} € / an" : "—");
        if (txtLoyerAnnuel != null)
            txtLoyerAnnuel.color = loyerTotal > 0 ? Col("#0F6E56") : Col("#5F5E5A");
        Set(txtPiluleEtat, EtatPilule(bat, out bool occupe));
        if (txtPiluleEtat != null)
        {
            ColorUtility.TryParseHtmlString(occupe ? "#E1F5EE" : "#FAEEDA", out var bgc);
            ColorUtility.TryParseHtmlString(occupe ? "#085041" : "#633806", out var txc);
            var bg = txtPiluleEtat.transform.parent.GetComponent<Image>();
            if (bg != null) bg.color = bgc;
            txtPiluleEtat.color = txc;
            var etatIcon = txtPiluleEtat.transform.parent.Find("Icon");
            if (etatIcon != null)
            {
                var ei = etatIcon.GetComponent<Image>();
                if (ei != null) ei.color = txc;
            }
        }
        Set(txtTitreLocataires,
            $"Locataires   <size=70%><color=#5F5E5A>{nbLots} lot{(nbLots > 1 ? "s" : "")} · {loyerTotal:N0} € / an</color></size>");

        // Bande financière : loyers · cash flow · rendement
        CalculFinances(bat, loyerTotal, out float cashFlowMois, out float rendementNet, out bool aDesCredits, out float investTotal);

        // Tuiles KPI (crème ambré, gros chiffres) + ligne d'infos secondaires.
        if (_tLoyer != null)
        {
            _tLoyer.text = loyerTotal > 0 ? $"{loyerTotal:N0} €" : "—";
            _tLoyer.color = loyerTotal > 0 ? Col("#0F6E56") : Col("#5F5E5A");
            _tInvesti.text = investTotal > 0 ? $"{investTotal:N0} €" : "—";

            // Dépensé/an = service de la dette annuel (loyers − cash-flow annuel).
            // Total gagné = cash-flow net cumulé depuis l'acquisition.
            float cashFlowAn = cashFlowMois * 12f;
            float depenseAn = loyerTotal - cashFlowAn;
            float annees = AnneesDepuisAcquisition(bat);
            float gagneTotal = cashFlowAn * annees;
            if (_tDepense != null)
            {
                _tDepense.text = depenseAn > 0.5f ? $"{depenseAn:N0} €" : (loyerTotal > 0 ? "0 €" : "—");
                _tDepense.color = Col("#D85A30");
            }
            if (_tGagne != null)
            {
                _tGagne.text = annees > 0f ? $"{(gagneTotal >= 0 ? "+" : "")}{gagneTotal:N0} €" : "—";
                _tGagne.color = gagneTotal >= 0 ? Col("#0F6E56") : Col("#D85A30");
            }

            _tRend.text = rendementNet != 0 ? $"{rendementNet:F1} %" : "—";
            _tRend.color = ColSign(rendementNet);
            _tCash.text = (aDesCredits || cashFlowMois != 0)
                ? $"{(cashFlowMois >= 0 ? "+" : "")}{cashFlowMois:N0} €" : "—";
            _tCash.color = cashFlowMois >= 0 ? Col("#0F6E56") : Col("#D85A30");
            _secInfo.text =
                $"Acquis {DateAcquisition(bat)}    ·    Terrain {(bat.tailleTerrain > 0 ? $"{bat.tailleTerrain:F0} m²" : "—")}"
                + $"    ·    Cadastre {(string.IsNullOrWhiteSpace(bat.cadastral) ? "—" : bat.cadastral)}"
                + $"    ·    Travaux {ResumeTravaux(bat)}    ·    Objectifs {ResumeObjectifs(bat)}";
        }

        // Grille : investi · rendement · cadastre
        Set(txtInvesti, investTotal > 0 ? $"{investTotal:N0} €" : "—");
        Set(txtRendement, rendementNet != 0 ? $"{rendementNet:F1} %" : "—");
        if (txtRendement != null)
            txtRendement.color = ColSign(rendementNet);
        Set(txtCadastre, string.IsNullOrWhiteSpace(bat.cadastral) ? "—" : bat.cadastral);
        string signe = cashFlowMois >= 0 ? "+" : "";
        string couleurCF = cashFlowMois >= 0 ? "#0F6E56" : "#D85A30";
        string finances = $"Loyers  <b>{loyerTotal:N0} € / an</b>";
        if (aDesCredits || cashFlowMois != 0)
            finances += $"      Cash flow  <b><color={couleurCF}>{signe}{cashFlowMois:N0} € / mois</color></b>";
        if (rendementNet > 0)
            finances += $"      Rendement  <b>{rendementNet:F1} %</b>";
        Set(txtFinances, finances);

        EnlargeKpisOnce();
        EnsureATraiterCard();
        EnsureTwoColRow();
        EnsureLocatairesBand();
        RefreshATraiter();

        RefreshVignette();
    }

    // ── KPI agrandis (l'utilisatrice : « les informations sont trop petites ») ──
    // Autosize borné (comme le ResumeBox) : les gros montants restent lisibles sans déborder.
    void EnlargeKpisOnce()
    {
        if (_kpiEnlarged) return;
        _kpiEnlarged = true;
        BigValue(txtLoyerAnnuel); BigValue(txtInvesti); BigValue(txtRendement);

        // Carte de présentation agrandie (nom + vignette + hauteur de carte).
        if (txtNom != null && !txtNom.enableAutoSizing) txtNom.fontSize = 30;
        if (txtAdresse != null && !txtAdresse.enableAutoSizing && txtAdresse.fontSize < 17) txtAdresse.fontSize = 17;
        if (vignetteCarte != null)
        {
            var vle = vignetteCarte.GetComponent<LayoutElement>();
            if (vle != null) { vle.minWidth = 235; vle.preferredWidth = 235; vle.minHeight = 150; vle.preferredHeight = 150; }
            // La hauteur de la carte est pilotée par son LayoutElement (fixe) → l'agrandir
            // pour laisser respirer la vignette agrandie.
            var carte = vignetteCarte.transform.parent != null ? vignetteCarte.transform.parent.parent : null;
            var cle = carte != null ? carte.GetComponent<LayoutElement>() : null;
            if (cle != null && cle.preferredHeight < 215) { cle.minHeight = 215; cle.preferredHeight = 215; }
        }
    }

    static void BigValue(TMP_Text t)
    {
        if (t == null) return;
        t.enableAutoSizing = true; t.fontSizeMin = 18; t.fontSizeMax = 30;
    }

    // ── Tuiles KPI crème (remplacent la grille plate InfoGrid) ──────────────────
    void EnsureKpiTiles()
    {
        if (_kpiBuilt || vignetteCarte == null) return;
        var carte = vignetteCarte.transform.parent != null ? vignetteCarte.transform.parent.parent : null;
        if (carte == null) return;
        _kpiBuilt = true;

        // Grille plate d'origine masquée. La ligne d'infos secondaires reste DANS la
        // carte identité ; les tuiles KPI deviennent une RANGÉE à part (sur le beige),
        // comme la maquette — sinon elles s'écrasent dans la hauteur de la carte.
        var infoGrid = carte.Find("InfoGrid");
        int idx = infoGrid != null ? infoGrid.GetSiblingIndex() : carte.childCount;
        if (infoGrid != null) infoGrid.gameObject.SetActive(false);

        _secInfo = UIFactory.Text(carte, "", 13, UITheme.TexteSecondaire);
        _secInfo.transform.SetSiblingIndex(idx);

        // Rangée de tuiles KPI, insérée dans VueResume juste après la carte identité.
        var row = UIFactory.HBox(transform, 10, false, "KpiRow");
        row.childControlWidth = true; row.childForceExpandWidth = true;
        row.childControlHeight = true; row.childForceExpandHeight = true;
        row.childAlignment = TextAnchor.MiddleLeft;
        UIFactory.LE(row.gameObject, minH: 82, prefH: 82, flexH: 0);
        _tLoyer = KpiTile(row.transform, "Loyers / an");
        _tDepense = KpiTile(row.transform, "Dépensé / an");
        _tInvesti = KpiTile(row.transform, "Investi");
        _tGagne = KpiTile(row.transform, "Total gagné");
        _tRend = KpiTile(row.transform, "Rendement net");
        _tCash = KpiTile(row.transform, "Cash flow / mois");
        row.transform.SetSiblingIndex(1);   // CarteBatiment(0) → KpiRow(1)

        // Bande financière du bas devenue redondante avec la tuile « Cash flow ».
        transform.Find("BandeFinanciere")?.gameObject.SetActive(false);
    }

    TMP_Text KpiTile(Transform parent, string label)
    {
        // Fond clair net (crème blanche) + cadre ambre franc → la tuile se détache
        // du beige de la page (avant : crème #FBF5E8 quasi invisible sur le fond).
        // Root = liseré ambre ARRONDI (comme la carte « À traiter ») + fond crème
        // inséré de 4 px à gauche → le liseré épouse les coins arrondis du cadre.
        var tile = UIFactory.Panel("Kpi", parent, Col("#BA7517"));
        var brd = UIFactory.Border(tile.gameObject, Col("#C9A96A"));
        brd.effectDistance = new Vector2(1.5f, -1.5f);
        UIFactory.LE(tile.gameObject, flexW: 1, minW: 0, minH: 66, prefH: 66);
        var bg = UIFactory.Panel("CardBg", tile.transform, UITheme.Carte);
        bg.raycastTarget = false;
        bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // pas dans le VLG
        var bgrt = (RectTransform)bg.transform;
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
        bgrt.offsetMin = new Vector2(4, 0); bgrt.offsetMax = Vector2.zero;  // liseré 4 px à gauche
        bg.transform.SetSiblingIndex(0);
        var v = tile.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 12, 8, 8); v.spacing = 3;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true;
        v.childAlignment = TextAnchor.MiddleLeft;
        var l = UIFactory.Text(v.transform, label, 13, Col("#8A5E14"));
        l.enableWordWrapping = false; l.overflowMode = TextOverflowModes.Ellipsis;
        var val = UIFactory.Text(v.transform, "—", 25, UITheme.TextePrincipal, true);
        val.enableAutoSizing = true; val.fontSizeMin = 18; val.fontSizeMax = 27;
        val.enableWordWrapping = false; val.overflowMode = TextOverflowModes.Ellipsis;
        return val;
    }

    // ── Carte « À traiter » propre au bâtiment (fait de la fiche résumé un vrai
    //    tableau de bord : ce qui demande l'attention ici, d'un coup d'œil) ──────
    void EnsureATraiterCard()
    {
        if (_aTraiterCard != null || _bp == null) return;

        // Même visuel que la carte « À traiter » du menu : root = liseré gris-bleu
        // (RoundedRect) + « CardBg » crème inséré (liseré de 8 px à gauche).
        var card = UIFactory.Panel("CarteATraiter", transform, NotesAccent);
        UIFactory.Border(card.gameObject);
        _aTraiterCard = card.transform;
        // Après la carte identité (0) et la rangée KPI (1).
        _aTraiterCard.SetSiblingIndex(2);
        var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
        // Mêmes marges que le menu : la bande de titre « flotte » (marge crème
        // au-dessus + à droite ; à gauche = liseré 8 px + gouttière → 28).
        vlg.padding = new RectOffset(28, 16, 10, 12); vlg.spacing = 4;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Fond crème inséré (comme « CardBg » du menu) : stretch, liseré de 8 px à
        // gauche, ignoré par le VLG. Premier enfant → rendu derrière bande + lignes.
        var bg = UIFactory.Panel("CardBg", _aTraiterCard, UITheme.Carte);
        bg.raycastTarget = false;
        bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var bgrt = (RectTransform)bg.transform;
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
        bgrt.offsetMin = new Vector2(8, 0); bgrt.offsetMax = Vector2.zero;
        bg.transform.SetSiblingIndex(0);

        // Bande de titre (famille « notes » gris-bleu, 58 / 22 — comme les en-têtes app).
        var band = UIFactory.Panel("TitleBand", _aTraiterCard, NotesClair);
        UIFactory.LE(band.gameObject, minH: 58, prefH: 58, flexH: 0);
        var bh = band.gameObject.AddComponent<HorizontalLayoutGroup>();
        bh.padding = new RectOffset(16, 16, 4, 4); bh.spacing = 8;
        bh.childControlWidth = true; bh.childControlHeight = true;
        bh.childForceExpandWidth = false; bh.childForceExpandHeight = false;   // sinon le badge s'étire (ovale)
        bh.childAlignment = TextAnchor.MiddleLeft;
        // Icône (comme le menu : « sec_target », gris-bleu). Nommée « Icon » sous
        // « TitleBand » → AppIconResizer l'agrandit à 34 comme partout.
        var ic = UIFactory.Rect("Icon", band.transform);
        var ii = ic.gameObject.AddComponent<Image>();
        var icSp = FindSprite("sec_target");
        if (icSp != null) ii.sprite = icSp;
        ii.color = NotesAccent; ii.preserveAspect = true; ii.raycastTarget = false;
        UIFactory.LE(ic.gameObject, prefW: 22, minW: 22, prefH: 22, minH: 22, flexW: 0);

        _aTraiterTitre = UIFactory.Text(band.transform, "À traiter", 22, NotesAccent, true);
        UIFactory.LE(_aTraiterTitre.gameObject, flexW: 1);

        // Badge compteur rouge (pastille 26×20, exactement comme le « NbBadge » du menu).
        var badge = UIFactory.Panel("Count", band.transform, UITheme.Alerte);
        UIFactory.LE(badge.gameObject, minW: 26, prefW: 26, minH: 20, prefH: 20, flexW: 0, flexH: 0);
        var cbh = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
        cbh.childAlignment = TextAnchor.MiddleCenter;
        cbh.childControlWidth = true; cbh.childControlHeight = true;
        cbh.childForceExpandWidth = false; cbh.childForceExpandHeight = false;
        _countTxt = UIFactory.Text(badge.transform, "", 13, Color.white, true);
        _countTxt.alignment = TextAlignmentOptions.Center;
        _countBadge = badge.gameObject;

        // Marges outer déjà fournies par le VLG racine (28/16) → pas de double indent.
        var rows = UIFactory.VBox(_aTraiterCard, 4, 0, 0, 2, 4, "Rows");
        _aTraiterRows = rows.transform;
    }

    // « À traiter » (gauche) + « Locataires » (droite) sur une même rangée.
    void EnsureTwoColRow()
    {
        if (_twoColBuilt || _aTraiterCard == null) return;
        var loc = transform.Find("CarteLocataires");
        if (loc == null) return;
        _twoColBuilt = true;

        var row = UIFactory.HBox(transform, 12, false, "RowTraiterLoc");
        // childControlHeight=false : chaque colonne se dimensionne via SON ContentSizeFitter
        // (sinon conflit CSF ↔ layout → cartes désalignées). UpperLeft = tops alignés.
        row.childControlWidth = true; row.childForceExpandWidth = false;
        row.childControlHeight = false; row.childForceExpandHeight = false;
        row.childAlignment = TextAnchor.UpperLeft;
        row.transform.SetSiblingIndex(2);   // après la carte identité (0) et les KPI (1)

        _aTraiterCard.SetParent(row.transform, false);
        var leA = _aTraiterCard.GetComponent<LayoutElement>() ?? _aTraiterCard.gameObject.AddComponent<LayoutElement>();
        leA.flexibleWidth = 1f; leA.minWidth = 280;

        loc.SetParent(row.transform, false);
        var csfL = loc.GetComponent<ContentSizeFitter>() ?? loc.gameObject.AddComponent<ContentSizeFitter>();
        csfL.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var leL = loc.GetComponent<LayoutElement>() ?? loc.gameObject.AddComponent<LayoutElement>();
        leL.flexibleWidth = 1f; leL.minWidth = 280;
    }

    void RefreshATraiter()
    {
        if (_aTraiterCard == null || _bp == null) return;

        var alertes = HomeAlertCollector.Collect(new[] { _bp });
        // « Seulement si utile » : agréger les alertes n'a de sens qu'à partir de
        // 2 locataires. À 0 ou 1 locataire la carte ferait doublon avec la fiche du
        // locataire (ou n'aurait rien à agréger) → toujours masquée, même s'il existe
        // une alerte propre au bâtiment.
        int nommes = _bp.listLocataire?.Count(l => l != null && !string.IsNullOrEmpty(l.Name)) ?? 0;
        bool utile = alertes.Count > 0 && nommes >= 2;

        _aTraiterCard.gameObject.SetActive(utile);
        if (utile)
        {
            foreach (Transform c in _aTraiterRows) Destroy(c.gameObject);
            if (_countTxt != null) _countTxt.text = alertes.Count.ToString();
            if (_countBadge != null) _countBadge.SetActive(alertes.Count > 0);
            ATraiterHeader();
            foreach (var a in alertes) ATraiterRow(a);
        }
        AjusteHauteurRangee();
    }

    // Le HBox « RowTraiterLoc » a childControlHeight=false (pour ne pas casser le CSF
    // des deux cartes) → il NE MESURE PAS leur hauteur et reste à ~100 px, si bien que
    // la carte « À traiter » (plus haute) déborde et recouvre la rangée KPI au-dessus.
    // Correctif : on force la hauteur de la rangée = la plus haute des cartes actives.
    // Donne au titre « Locataires » une bande de fond (comme « À traiter »), mais en
    // vert (famille identité = locataires) au lieu du gris-bleu (notes).
    void EnsureLocatairesBand()
    {
        if (_locBandBuilt || txtTitreLocataires == null) return;
        var rowEntete = txtTitreLocataires.transform.parent as RectTransform;   // RowEntete
        if (rowEntete == null) return;
        _locBandBuilt = true;

        var img = rowEntete.GetComponent<Image>() ?? rowEntete.gameObject.AddComponent<Image>();
        img.color = IdentityClair;
        // Reprend le sprite arrondi de la bande « À traiter » pour un rendu identique.
        var atBand = _aTraiterCard != null ? _aTraiterCard.Find("TitleBand") : null;
        var atImg = atBand != null ? atBand.GetComponent<Image>() : null;
        if (atImg != null && atImg.sprite != null) { img.sprite = atImg.sprite; img.type = Image.Type.Sliced; }

        var le = rowEntete.GetComponent<LayoutElement>() ?? rowEntete.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 58; le.preferredHeight = 58; le.flexibleHeight = 0f;
        var hlg = rowEntete.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.padding = new RectOffset(16, 16, 4, 4);
            hlg.childAlignment = TextAnchor.MiddleLeft;
        }
        var icon = rowEntete.Find("Icon");
        if (icon != null) { var ii = icon.GetComponent<Image>(); if (ii != null) ii.color = IdentityAccent; }
    }

    void AjusteHauteurRangee()
    {
        var rowRT = (_aTraiterCard != null ? _aTraiterCard.parent : null) as RectTransform;
        if (rowRT == null || rowRT.name != "RowTraiterLoc") return;
        float h = 0f;
        foreach (Transform c in rowRT)
        {
            if (!c.gameObject.activeSelf) continue;
            var crt = c as RectTransform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(crt);
            h = Mathf.Max(h, LayoutUtility.GetPreferredHeight(crt));
        }
        if (h <= 0f) return;
        var le = rowRT.GetComponent<LayoutElement>() ?? rowRT.gameObject.AddComponent<LayoutElement>();
        le.minHeight = h; le.preferredHeight = h; le.flexibleHeight = 0f;
    }

    // En-têtes de colonnes reproduits À L'IDENTIQUE du « ColHeader » du menu :
    // padding (4,8), spacing 10 ; spacer 4 · Type 170 gauche · Description flex ·
    // Locataire 170 droite ; police 21 (colonne « Bâtiment » retirée : un seul bâtiment).
    void ATraiterHeader()
    {
        var hb = UIFactory.HBox(_aTraiterRows, 10, false, "ColHeader");
        hb.padding = new RectOffset(4, 8, 0, 0);
        hb.childControlWidth = true; hb.childForceExpandWidth = false;
        hb.childControlHeight = true; hb.childForceExpandHeight = false;
        hb.childAlignment = TextAnchor.MiddleLeft;
        UIFactory.LE(hb.gameObject, minH: 24, prefH: 24, flexH: 0);

        var sp = UIFactory.Rect("Col", hb.transform);                 // aligne avec la pastille
        UIFactory.LE(sp.gameObject, minW: 4, prefW: 4, flexW: 0);
        HeaderCol(hb.transform, "Type", 170, TextAlignmentOptions.Left);
        HeaderCol(hb.transform, "Description", 0, TextAlignmentOptions.Left);   // flex
        HeaderCol(hb.transform, "Locataire", 170, TextAlignmentOptions.Right);
    }

    void HeaderCol(Transform parent, string label, float w, TextAlignmentOptions align)
    {
        var t = UIFactory.Text(parent, label, 21, UITheme.TexteSecondaire, false, align);
        t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        if (w > 0) UIFactory.LE(t.gameObject, minW: w, prefW: w, flexW: 0);
        else UIFactory.LE(t.gameObject, flexW: 1, minW: 0);
    }

    static Sprite FindSprite(string spriteName)
    {
        foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            if (s != null && s.name == spriteName) return s;
        return null;
    }

    void ATraiterRow(HomeAlert a)
    {
        // Visuel IDENTIQUE au « À traiter » du menu : on réutilise le même prefab de ligne.
        var prefab = GeneralMenuPanel.Instance != null ? GeneralMenuPanel.Instance.alertRowPrefab : null;
        if (prefab != null)
        {
            var go = Instantiate(prefab, _aTraiterRows);
            var ui = go.GetComponent<HomeAlertRowUI>();
            if (ui != null)
            {
                ui.Setup(a, _ => _bp?.ShowLocataireView());
                if (ui.txtBatiment != null) ui.txtBatiment.gameObject.SetActive(false);   // colonne Bâtiment inutile
            }
            return;
        }

        // Repli (si le prefab n'est pas disponible) : ligne compacte maison.
        var row = UIFactory.HBox(_aTraiterRows, 10, false, "Row");
        UIFactory.LE(row.gameObject, minH: 34);
        row.childControlWidth = true; row.childForceExpandWidth = false;
        row.childAlignment = TextAnchor.MiddleLeft;

        // Liseré d'urgence.
        var dot = UIFactory.Panel("Dot", row.transform, a.pastille);
        UIFactory.LE(dot.gameObject, minW: 5, prefW: 5, flexW: 0, minH: 22, prefH: 22);

        // Pastille « Type » (fond pastel + texte foncé, comme dans « À traiter » du menu).
        var pill = UIFactory.Panel("Type", row.transform, a.typeBg);
        var ph = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        ph.padding = new RectOffset(10, 10, 2, 2); ph.childAlignment = TextAnchor.MiddleCenter;
        ph.childControlWidth = true; ph.childControlHeight = true; ph.childForceExpandWidth = false;
        UIFactory.LE(pill.gameObject, minW: 92, prefW: 92, flexW: 0, minH: 26, prefH: 26);
        var pt = UIFactory.Text(pill.transform, a.typeTodo ?? "", 13, a.typeTexte, true);
        pt.enableWordWrapping = false; pt.overflowMode = TextOverflowModes.Ellipsis;

        // Rappel (détail) + locataire.
        var rap = UIFactory.Text(row.transform, a.nomRappel ?? "", 15, UITheme.TextePrincipal);
        rap.enableWordWrapping = false; rap.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(rap.gameObject, flexW: 1, minW: 60);
        if (!string.IsNullOrEmpty(a.nomLocataire))
        {
            var loc = UIFactory.Text(row.transform, a.nomLocataire, 14, UITheme.TexteSecondaire,
                false, TextAlignmentOptions.Right);
            loc.enableWordWrapping = false; loc.overflowMode = TextOverflowModes.Ellipsis;
            UIFactory.LE(loc.gameObject, prefW: 140, minW: 60, flexW: 0);
        }
    }

    private void RefreshVignette()
    {
        if (vignetteCarte == null || _bp == null || _bp.mapController == null) return;
        var mapImg = _bp.mapController.mapImage;
        if (mapImg != null && mapImg.texture != null)
        {
            vignetteCarte.texture = mapImg.texture;
            vignetteCarte.color = Color.white;
        }
    }

    // ── Calculs ───────────────────────────────────────────────────────────────

    private static void CalculFinances(Batiment bat, float loyerAnnuel,
        out float cashFlowMois, out float rendementNet, out bool aDesCredits, out float investTotal)
    {
        investTotal = 0f;
        float mensualites = 0f;
        aDesCredits = false;

        if (bat.historiquesAchat != null)
            foreach (var a in bat.historiquesAchat)
            {
                investTotal += a.prixAchat + a.fraisNotaire + a.fraisAgence;
                if (a.emprunt && a.dureeMois > 0)
                {
                    mensualites += RentabiliteCalculator.Mensualite(
                        a.montantEmprunte, a.tauxInteretAnnuel, a.dureeMois);
                    aDesCredits = true;
                }
            }

        if (bat.travaux != null)
            foreach (var t in bat.travaux)
            {
                investTotal += t.coutTotal;
                if (t.emprunt && t.dureeMois > 0)
                {
                    mensualites += RentabiliteCalculator.Mensualite(
                        t.montantEmprunte, t.tauxInteretAnnuel, t.dureeMois);
                    aDesCredits = true;
                }
            }

        float cashFlowAnnuel = loyerAnnuel - mensualites * 12f;
        cashFlowMois = cashFlowAnnuel / 12f;
        rendementNet = investTotal > 0 ? cashFlowAnnuel / investTotal * 100f : 0f;
    }

    // Années (fractionnaires) écoulées depuis l'acquisition (date manuelle, sinon
    // achat le plus ancien). Sert au calcul du « Total gagné » cumulé.
    static float AnneesDepuisAcquisition(Batiment bat)
    {
        DateTime acq;
        if (!DateTime.TryParse(bat.dateAcquisitionISO, out acq))
        {
            acq = DateTime.Today;
            if (bat.historiquesAchat != null)
                foreach (var a in bat.historiquesAchat)
                    if (DateTime.TryParse(a.dateAchat, out var d) && d < acq) acq = d;
        }
        return Mathf.Max(0f, (float)(DateTime.Today - acq).TotalDays / 365.25f);
    }

    public static string DateAcquisition(Batiment bat)
    {
        // Date saisie manuellement prioritaire, sinon date d'achat la plus ancienne.
        if (DateTime.TryParse(bat.dateAcquisitionISO, out var manuelle))
            return manuelle.ToString("dd/MM/yyyy");
        if (bat.historiquesAchat == null || bat.historiquesAchat.Count == 0) return "—";
        DateTime plusAncienne = DateTime.MaxValue;
        foreach (var a in bat.historiquesAchat)
            if (DateTime.TryParse(a.dateAchat, out var d) && d < plusAncienne)
                plusAncienne = d;
        return plusAncienne == DateTime.MaxValue ? "—" : plusAncienne.ToString("dd/MM/yyyy");
    }

    private static string ResumeTravaux(Batiment bat)
    {
        if (bat.travaux == null || bat.travaux.Count == 0)
            return bat.travauxEnCours ? "En cours" : "Aucun";

        // Le plus récent avec description
        var dernier = bat.travaux[bat.travaux.Count - 1];
        string desc = string.IsNullOrEmpty(dernier.description) ? "Travaux" : dernier.description;
        if (desc.Length > 24) desc = desc.Substring(0, 24) + "…";

        if (DateTime.TryParse(dernier.dateDebutTravaux, out var debut) && dernier.dureeMois > 0)
        {
            var fin = debut.AddMonths(dernier.dureeMois);
            if (fin > DateTime.Now)
                return $"{desc} — fin {fin:MM/yyyy}";
        }
        return bat.travauxEnCours ? $"{desc} (en cours)" : $"{bat.travaux.Count} enregistrés";
    }

    private static string ResumeObjectifs(Batiment bat)
    {
        var items = bat.objectifs?.items;
        int aFaire = 0, obligatoires = 0;
        if (items != null)
            foreach (var o in items)
            {
                if (o.status == Objective.ObjectiveStatus.Fait) continue;
                if (o.status == Objective.ObjectiveStatus.Obligatoire) obligatoires++;
                else aFaire++;
            }

        if (aFaire == 0 && obligatoires == 0) return "Aucun en cours";
        string s = $"{aFaire} à faire";
        if (obligatoires > 0)
            s += $" · <color=#D85A30>{obligatoires} obligatoire{(obligatoires > 1 ? "s" : "")}</color>";
        return s;
    }

    private static string EtatPilule(Batiment bat, out bool occupe)
    {
        occupe = false;
        var locs = bat.locataireDuBatiment;
        int total = locs?.Count ?? 0;
        if (total == 0) return "Vacant";                       // aucun lot loué

        int vacant = 0;
        foreach (var l in locs)
            if (string.IsNullOrEmpty(l.Name)) vacant++;

        if (vacant >= total) return "Vacant";                  // tous les lots vides
        if (vacant > 0) return vacant > 1 ? $"{vacant} lots vacants" : "1 lot vacant";

        occupe = true;                                          // tous occupés
        return "Occupé";
    }

    private static string LabelParking(ParkingState p) => p switch
    {
        ParkingState.ParkingPrive => "Parking privé",
        ParkingState.ParkingEnCopropiete => "Parking copro",
        _ => "Sans parking"
    };

    private static void Set(TMP_Text t, string v) { if (t != null) t.text = v; }

    private static Color Col(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    // Vert si positif, terracotta si négatif, gris si nul.
    private static Color ColSign(float v) => v > 0 ? Col("#0F6E56") : v < 0 ? Col("#D85A30") : Col("#5F5E5A");
}
