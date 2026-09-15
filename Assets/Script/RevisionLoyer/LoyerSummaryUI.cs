using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoyerSummaryUI : MonoBehaviour
{
    [Header("Affichage")]
    public TMP_Text txtLoyerAnnuelHT;
    public TMP_Text txtLoyerAnnuelTTC;
    public TMP_Text txtLoyerM2;
    public TMP_Text txtLoyerPrecedent;    // loyer avant la dernière révision
    public TMP_Text txtProvisions;
    public TMP_Text txtLoyerPeriodeHT;
    public TMP_Text txtLoyerPeriodeTTC;

    [Header("Révision")]
    public Button btnOuvrirRevision;
    public GameObject badgeRevision;      // pastille rouge

    private const float TVA = 1.2f;

    private LocatairePrefab _locatairePrefab;
    private Button _btnModalites;          // 2ᵉ bouton (gestion du loyer / facturation)

    public void Init(LocatairePrefab locatairePrefab)
    {
        _locatairePrefab = locatairePrefab;

        btnOuvrirRevision.onClick.RemoveAllListeners();
        btnOuvrirRevision.onClick.AddListener(() =>
            RevisionPanel.Instance.Open(
                _locatairePrefab.GetLocataire(),
                onSaved: () => _locatairePrefab.OnRevisionSaved()));

        EnsureModalitesButton();
    }

    // Deuxième bouton dans la bande de titre « Loyer » : ouvre le même pop-up sur
    // le volet « Gestion du loyer » (périodicité, jour, mois, provisions, régul.).
    // Clone du bouton « Réviser » pour un visuel identique, en teinte secondaire.
    private void EnsureModalitesButton()
    {
        if (_btnModalites != null || btnOuvrirRevision == null) return;

        var clone = Instantiate(btnOuvrirRevision.gameObject, btnOuvrirRevision.transform.parent);
        clone.name = "BtnModalites";
        clone.transform.SetSiblingIndex(btnOuvrirRevision.transform.GetSiblingIndex()); // avant « Réviser »
        _btnModalites = clone.GetComponent<Button>();

        var lbl = _btnModalites.GetComponentInChildren<TMP_Text>(true);
        if (lbl != null) { lbl.text = "Modalités"; lbl.color = Col("#A9741C"); }
        // Bouton secondaire CONTRASTÉ : fond crème + liseré ambre (sinon invisible :
        // l'ancien fond #F4E7CD = la couleur de la bande de titre).
        var img = _btnModalites.GetComponent<Image>();
        if (img != null) img.color = UITheme.Carte;       // crème, ressort sur la bande ambre-clair
        UIFactory.Border(_btnModalites.gameObject, Col("#A9741C"));   // liseré ambre → bouton défini

        _btnModalites.onClick.RemoveAllListeners();
        _btnModalites.onClick.AddListener(() =>
            RevisionPanel.Instance.Open(
                _locatairePrefab.GetLocataire(),
                onSaved: () => _locatairePrefab.OnRevisionSaved(),
                RevisionPanel.Volet.Modalites));
    }

    public void Refresh(Locataire loc)
    {
        if (loc == null) return;

        float annuel = loc.loyerAnnuel;
        float taille = loc.tailleLot;
        float provision = loc.provisionPourCharges ? loc.provisionPourChargeValue : 0f;

        int n = NbPeriodes(loc.periodiciteLoyer);
        float perHT = annuel / n;                  // loyer par période, hors charges
        float perHTCharge = perHT + provision;     // charges comprises
        string labelP = LabelPeriode(loc.periodiciteLoyer);

        Set(txtLoyerAnnuelHT, $"{annuel:N2} €");
        Set(txtLoyerAnnuelTTC, $"{annuel * TVA:N2} €");
        Set(txtLoyerM2, taille > 0 ? $"{annuel / taille:N2} €/m²" : "—");
        Set(txtLoyerPrecedent, loc.loyerAnnuelPrecedent > 0f
            ? $"{loc.loyerAnnuelPrecedent:N2} €"
            : "—");   // nul tant qu'aucune révision n'a eu lieu
        Set(txtProvisions, loc.provisionPourCharges
            ? $"{provision:N2} € / {labelP}" : "Aucune");

        // 4 lignes de loyer par période : HT/TTC hors charges (toujours) +
        // HT/TTC charges comprises (masquées s'il n'y a pas de provision).
        EnsureLoyerLinesBuilt();
        ClearUnits();   // les valeurs incluent déjà « € » → on masque l'unité du prefab (évite « €€ »)
        bool aCharges = loc.provisionPourCharges && provision > 0f;
        Set(_htHors, $"{perHT:N2} € / {labelP}");
        Set(_ttcHors, $"{perHT * TVA:N2} € / {labelP}");
        Set(txtLoyerPeriodeHT, $"{perHTCharge:N2} € / {labelP}");
        Set(txtLoyerPeriodeTTC, $"{perHTCharge * TVA:N2} € / {labelP}");
        if (_rowAvecCharge != null) _rowAvecCharge.SetActive(aCharges);

        // Récap lecture seule (demande / mois facturés / révision / régularisation)
        EnsureRecapBuilt();
        UpdateRecap(loc);

        // Layout compact (annuel en ligne + mini-tableau par période) ; remplace les encadrés.
        BuildCompact();
        if (_cHT != null)
        {
            _cHT.text = $"{annuel:N2} €";
            _cTTC.text = $"{annuel * TVA:N2} €";
            _cPrec.text = loc.loyerAnnuelPrecedent > 0f ? $"{loc.loyerAnnuelPrecedent:N2} €" : "—";
            _cM2.text = taille > 0 ? $"{annuel / taille:N2} €/m²" : "—";
            _cProv.text = loc.provisionPourCharges
                ? $"Provision pour charges : {provision:N2} € / {labelP}"
                : "Provision pour charges : aucune";
            _hHt.text = $"{perHT:N2} €";
            _hTtc.text = $"{perHT * TVA:N2} €";
            _aHt.text = $"{perHTCharge:N2} €";
            _aTtc.text = $"{perHTCharge * TVA:N2} €";
            if (_perLabel != null) _perLabel.text = $"Loyer par période ({labelP})";
        }

        // Badge + bouton Réviser plus voyants si révision dépassée
        bool due = EstRevisionDue(loc);
        if (badgeRevision != null)
            badgeRevision.SetActive(due);
        if (btnOuvrirRevision != null)
        {
            var img = btnOuvrirRevision.GetComponent<Image>();
            if (img != null) img.color = due ? Col("#D85A30") : Col("#A9741C");
            var lbl = btnOuvrirRevision.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.color = due ? Color.white : Col("#FBF5E8");
        }
    }

    // ── Lignes de loyer HT/TTC hors et avec charges ───────────────────────────
    // Les deux cartes-valeurs « Loyer Actuel HT/TTC » du prefab deviennent
    // « charges comprises » ; on clone leur Row pour créer « hors charges »
    // au-dessus. La Row « charges comprises » est masquée sans provision.

    private GameObject _rowAvecCharge;
    private TMP_Text _htHors, _ttcHors;
    private bool _loyerLinesBuilt;

    private void EnsureLoyerLinesBuilt()
    {
        if (_loyerLinesBuilt) return;
        if (txtLoyerPeriodeHT == null) return;
        _loyerLinesBuilt = true;

        var htField = txtLoyerPeriodeHT.transform.parent.parent;   // « Loyer Actuel HT »
        var row = htField.parent;                                  // Row [HT | TTC]
        _rowAvecCharge = row.gameObject;

        SetTitle(htField, "Loyer HT (charges comprises) :");
        if (row.childCount > 1) SetTitle(row.GetChild(1), "Loyer TTC (charges comprises) :");

        // Clone de la Row → version « hors charges », insérée juste au-dessus.
        var clone = Instantiate(row.gameObject, row.parent);
        clone.name = "RowLoyerHorsCharge";
        clone.transform.SetSiblingIndex(row.GetSiblingIndex());

        var cHT = clone.transform.GetChild(0);
        SetTitle(cHT, "Loyer HT (hors charges) :");
        _htHors = ValueText(cHT);
        if (clone.transform.childCount > 1)
        {
            var cTTC = clone.transform.GetChild(1);
            SetTitle(cTTC, "Loyer TTC (hors charges) :");
            _ttcHors = ValueText(cTTC);
        }
    }

    private static void SetTitle(Transform field, string txt)
    {
        var t = field.Find("title")?.GetComponent<TMP_Text>();
        if (t != null) t.text = txt;
    }

    private static TMP_Text ValueText(Transform field)
    {
        var vl = field.Find("ValueLine");
        return vl != null && vl.childCount > 0 ? vl.GetChild(0).GetComponent<TMP_Text>() : null;
    }

    // ── Récap facturation (lecture seule, construit par code) ─────────────────
    // Affiché dans la carte Loyer, au-dessus du bouton « Réviser ». Les valeurs
    // sont saisies dans le pop-up « Révision du loyer » (voir RevisionPanel).

    private GameObject _recapGO, _rowRegul;
    private TMP_Text _valDemande, _valMois, _valRevision, _valRegul;

    private static readonly string[] MoisNoms =
    {
        "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
        "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"
    };
    private static string MoisNom(int m) => (m >= 1 && m <= 12) ? MoisNoms[m - 1] : m.ToString();

    private void EnsureRecapBuilt()
    {
        if (_recapGO != null || btnOuvrirRevision == null) return;

        // Corps de la carte Loyer : section « Loyer » → enfant « Content » (VLG).
        // (Le bouton Révision est dans la bande de titre horizontale, pas ici.)
        var titre = btnOuvrirRevision.transform.parent;
        var section = titre != null ? titre.parent : null;
        var content = section != null ? section.Find("Content") : null;
        if (content == null) return;

        var v = UIFactory.VBox(content, spacing: 3, padL: 2, padR: 12, name: "RecapFacturation");
        _recapGO = v.gameObject;
        var csf = _recapGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Filet de séparation + titre du bloc.
        var sep = UIFactory.Panel("Sep", v.transform, UITheme.Bordure, false);
        UIFactory.LE(sep.gameObject, minH: 1, prefH: 1);
        UIFactory.Text(v.transform, "Modalités du loyer", 17, Col("#A9741C"), true);

        _valDemande  = Row(v.transform, "Loyer demandé le");
        _valMois     = Row(v.transform, "Mois facturés");
        _valRevision = Row(v.transform, "Prochaine révision");
        _valRegul    = Row(v.transform, "Régularisation charges", out _rowRegul);
    }

    private TMP_Text Row(Transform parent, string label) => Row(parent, label, out _);

    private TMP_Text Row(Transform parent, string label, out GameObject rowGO)
    {
        var h = UIFactory.HBox(parent, 8, false, "Row");
        UIFactory.LE(h.gameObject, minH: 24);
        rowGO = h.gameObject;
        var l = UIFactory.Text(h.transform, label, 17, UITheme.TexteSecondaire);
        UIFactory.LE(l.gameObject, flexW: 1);
        return UIFactory.Text(h.transform, "—", 17, UITheme.TextePrincipal, true, TextAlignmentOptions.Right);
    }

    private void UpdateRecap(Locataire loc)
    {
        if (_recapGO == null) return;

        _valDemande.text = loc.jourDemandeLoyer > 0
            ? $"le {loc.jourDemandeLoyer} du mois" : "—";

        if (loc.periodiciteLoyer == Periodicite.mensuel)
            _valMois.text = "Tous les mois";
        else if (loc.moisFacturationLoyer != null && loc.moisFacturationLoyer.Count > 0)
            _valMois.text = string.Join(" · ",
                loc.moisFacturationLoyer.OrderBy(m => m).Select(MoisNom));
        else
            _valMois.text = "à définir";

        bool initialise = !string.IsNullOrEmpty(loc.indiceImmoAuDepart)
                          && loc.indiceImmoAuDepart != "—";
        _valRevision.text = initialise ? loc.MoisDeRevision.ToString("dd/MM/yyyy") : "—";
        _valRevision.color = EstRevisionDue(loc) ? Col("#D85A30") : UITheme.TextePrincipal;

        // Régularisation : uniquement en cas de provision pour charges.
        _rowRegul.SetActive(loc.provisionPourCharges);
        if (loc.provisionPourCharges)
            _valRegul.text = System.DateTime.TryParse(loc.dateRegularisationChargeISO, out var dr)
                ? dr.ToString("dd/MM/yyyy") : "—";
    }

    private static Color Col(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    public static bool EstRevisionDue(Locataire loc)
    {
        bool initialise = !string.IsNullOrEmpty(loc.indiceImmoAuDepart)
                          && loc.indiceImmoAuDepart != "—";
        return initialise && System.DateTime.Now >= loc.MoisDeRevision;
    }

    public static int NbPeriodes(Periodicite p) => p switch
    {
        Periodicite.mensuel => 12,
        Periodicite.trimestriel => 4,
        Periodicite.BiAnnuel => 2,
        _ => 1
    };

    public static string LabelPeriode(Periodicite p) => p switch
    {
        Periodicite.mensuel => "mois",
        Periodicite.trimestriel => "trimestre",
        Periodicite.BiAnnuel => "semestre",
        _ => "an"
    };

    // ── Layout compact (option validée : annuel en ligne + mini-tableau période) ─
    private bool _compactBuilt;
    private TMP_Text _cHT, _cTTC, _cPrec, _cM2, _cProv, _hHt, _hTtc, _aHt, _aTtc, _perLabel;

    private void BuildCompact()
    {
        if (_compactBuilt || btnOuvrirRevision == null) return;
        var section = btnOuvrirRevision.transform.parent.parent;   // section « Loyer »
        var content = section.Find("Content");
        if (content == null) return;
        _compactBuilt = true;

        // Badge « Révision due » → « Révision à faire » (élargi pour ne pas tronquer).
        if (badgeRevision != null)
        {
            var bt = badgeRevision.GetComponentInChildren<TMP_Text>(true);
            if (bt != null) { bt.text = "Révision à faire"; bt.enableWordWrapping = false; bt.overflowMode = TextOverflowModes.Overflow; bt.fontSize = 12; }
            var ble = badgeRevision.GetComponent<LayoutElement>() ?? badgeRevision.AddComponent<LayoutElement>();
            ble.minWidth = 130; ble.preferredWidth = 130;
        }

        // Masque tous les encadrés d'origine (on garde le récap facturation).
        var recap = _recapGO != null ? _recapGO.transform : content.Find("RecapFacturation");
        foreach (Transform c in content)
            if (c != recap) c.gameObject.SetActive(false);

        // Bloc compact en tête.
        var box = UIFactory.VBox(content, 10, 2, 12, 2, 4, "LoyerCompact");
        box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        box.transform.SetSiblingIndex(0);

        var L = TextAlignmentOptions.Left;

        // Loyer annuel : stats réparties sur la largeur ; HT/TTC en avant (gros/gras),
        // le tout dans une tuile à fond léger pour mettre en valeur.
        UIFactory.Text(box.transform, "Loyer annuel", 17, Col("#A9741C"), true);
        var annualCard = UIFactory.Panel("AnnualCard", box.transform, Col("#FBF5E8"));
        UIFactory.Border(annualCard.gameObject);
        var annual = annualCard.gameObject.AddComponent<HorizontalLayoutGroup>();
        annual.padding = new RectOffset(14, 14, 10, 10); annual.spacing = 12;
        annual.childControlWidth = true; annual.childForceExpandWidth = true;
        annual.childControlHeight = true; annual.childForceExpandHeight = false;
        annual.childAlignment = TextAnchor.LowerLeft;
        _cHT   = Stat(annual.transform, "HT", 24, UITheme.TextePrincipal);
        _cTTC  = Stat(annual.transform, "TTC", 24, UITheme.TextePrincipal);
        _cPrec = Stat(annual.transform, "Précédent", 17, UITheme.TexteSecondaire);
        _cM2   = Stat(annual.transform, "€ / m²", 17, UITheme.TexteSecondaire);

        // Provision.
        _cProv = UIFactory.Text(box.transform, "—", 17, UITheme.TextePrincipal);

        // Deux colonnes : mini-tableau par période (gauche, resserré) | Facturation du loyer (droite).
        var R = TextAlignmentOptions.Right;
        var cols = UIFactory.HBox(box.transform, 16, false, "PeriodeEtFacturation");
        cols.childControlWidth = true; cols.childForceExpandWidth = false;
        cols.childControlHeight = true; cols.childForceExpandHeight = false;
        cols.childAlignment = TextAnchor.UpperLeft;

        // Colonne gauche : label + mini-tableau 2×2 (montants alignés à droite).
        var leftCol = UIFactory.VBox(cols.transform, 4, 0, 0, 0, 0, "ColPeriode");
        leftCol.childControlWidth = true; leftCol.childForceExpandWidth = true;
        UIFactory.LE(leftCol.gameObject, flexW: 1, minW: 250);
        _perLabel = UIFactory.Text(leftCol.transform, "Loyer par période", 17, Col("#A9741C"), true);
        var card = MakeTable(leftCol.transform, "PerTable");
        var head = PerRow(card, Col("#FBF5E8"), 32);
        PerCell(head, "", 52, 17, false, L); PerCell(head, "Hors ch.", 0, 17, false, R); PerCell(head, "Ch. compr.", 0, 17, false, R);
        var r1 = PerRow(card, Col("#FCF8EE"), 34);
        PerCell(r1, "HT", 52, 17, false, L); _hHt = PerCell(r1, "—", 0, 18, true, R); _aHt = PerCell(r1, "—", 0, 18, true, R);
        var r2 = PerRow(card, Col("#F5EBD5"), 34);
        PerCell(r2, "TTC", 52, 17, false, L); _hTtc = PerCell(r2, "—", 0, 18, true, R); _aTtc = PerCell(r2, "—", 0, 18, true, R);

        // Colonne droite : « Facturation du loyer » (récap réparenté depuis Content).
        if (_recapGO != null)
        {
            _recapGO.transform.SetParent(cols.transform, false);
            var rle = _recapGO.GetComponent<LayoutElement>() ?? _recapGO.AddComponent<LayoutElement>();
            rle.flexibleWidth = 1.05f; rle.minWidth = 200;
            var sep = _recapGO.transform.Find("Sep");
            if (sep != null) sep.gameObject.SetActive(false);
        }
    }

    // Stat « libellé discret + valeur » répartie (flexW=1).
    private TMP_Text Stat(Transform parent, string label, float size, Color valColor)
    {
        var col = UIFactory.VBox(parent, 1, 0, 0, 0, 0, "Stat");
        col.childForceExpandWidth = true; col.childAlignment = TextAnchor.LowerLeft;
        UIFactory.LE(col.gameObject, flexW: 1, minW: 0);
        UIFactory.Text(col.transform, label, 17, UITheme.TexteSecondaire);
        return UIFactory.Text(col.transform, "—", size, valColor, true);
    }

    // Carte-tableau arrondie (renvoie le conteneur des lignes).
    private Transform MakeTable(Transform parent, string name)
    {
        var card = UIFactory.Panel(name, parent, UITheme.Carte);
        UIFactory.Border(card.gameObject);
        var cv = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.spacing = 0; cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return card.transform;
    }

    private Transform PerRow(Transform parent, Color bg, float h)
    {
        var p = UIFactory.Panel("PRow", parent, bg);
        var hl = p.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(12, 12, 0, 0); hl.spacing = 8;
        hl.childControlWidth = true; hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
        hl.childAlignment = TextAnchor.MiddleLeft;
        p.gameObject.AddComponent<LayoutElement>().minHeight = h;
        return p.transform;
    }

    private TMP_Text PerCell(Transform row, string text, float w, float size, bool bold, TextAlignmentOptions align)
    {
        var t = UIFactory.Text(row, text, size, bold ? UITheme.TextePrincipal : UITheme.TexteSecondaire, bold, align);
        t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        if (w > 0) UIFactory.LE(t.gameObject, prefW: w, minW: w, flexW: 0);
        else UIFactory.LE(t.gameObject, flexW: 1, minW: 0);
        return t;
    }

    private static void Set(TMP_Text t, string v) { if (t != null) t.text = v; }

    // ── Masquage de l'unité « € » du prefab (les valeurs incluent déjà €) ─────
    private bool _unitsCleared;
    private void ClearUnits()
    {
        if (_unitsCleared) return;
        _unitsCleared = true;
        ClearUnit(txtLoyerAnnuelHT); ClearUnit(txtLoyerAnnuelTTC); ClearUnit(txtLoyerM2);
        ClearUnit(txtLoyerPrecedent); ClearUnit(txtProvisions);
        ClearUnit(txtLoyerPeriodeHT); ClearUnit(txtLoyerPeriodeTTC);
        ClearUnit(_htHors); ClearUnit(_ttcHors);
    }

    private static void ClearUnit(TMP_Text value)
    {
        if (value == null) return;
        // Champ = value → ValueLine → field ; l'unité est « quantité » dans le field.
        Transform field = value.transform.parent != null ? value.transform.parent.parent : null;
        Transform q = field != null ? field.Find("quantité") : null;
        if (q == null && value.transform.parent != null) q = value.transform.parent.Find("quantité");
        if (q != null) { var t = q.GetComponent<TMP_Text>(); if (t != null) t.text = ""; }
    }
}