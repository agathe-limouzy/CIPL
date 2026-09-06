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

    public void Init(LocatairePrefab locatairePrefab)
    {
        _locatairePrefab = locatairePrefab;

        btnOuvrirRevision.onClick.RemoveAllListeners();
        btnOuvrirRevision.onClick.AddListener(() =>
            RevisionPanel.Instance.Open(
                _locatairePrefab.GetLocataire(),
                onSaved: () => _locatairePrefab.OnRevisionSaved()));
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
        bool aCharges = loc.provisionPourCharges && provision > 0f;
        Set(_htHors, $"{perHT:N2} € / {labelP}");
        Set(_ttcHors, $"{perHT * TVA:N2} € / {labelP}");
        Set(txtLoyerPeriodeHT, $"{perHTCharge:N2} € / {labelP}");
        Set(txtLoyerPeriodeTTC, $"{perHTCharge * TVA:N2} € / {labelP}");
        if (_rowAvecCharge != null) _rowAvecCharge.SetActive(aCharges);

        // Récap lecture seule (demande / mois facturés / révision / régularisation)
        EnsureRecapBuilt();
        UpdateRecap(loc);

        // Badge + bouton Réviser plus voyants si révision dépassée
        bool due = EstRevisionDue(loc);
        if (badgeRevision != null)
            badgeRevision.SetActive(due);
        if (btnOuvrirRevision != null)
        {
            var img = btnOuvrirRevision.GetComponent<Image>();
            if (img != null) img.color = due ? Col("#D85A30") : Col("#8E3B5A");
            var lbl = btnOuvrirRevision.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.color = due ? Color.white : Col("#F6E5EB");
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
        UIFactory.Text(v.transform, "Facturation du loyer", 15, UITheme.Primaire, true);

        _valDemande  = Row(v.transform, "Loyer demandé le");
        _valMois     = Row(v.transform, "Mois facturés");
        _valRevision = Row(v.transform, "Prochaine révision");
        _valRegul    = Row(v.transform, "Régularisation charges", out _rowRegul);
    }

    private TMP_Text Row(Transform parent, string label) => Row(parent, label, out _);

    private TMP_Text Row(Transform parent, string label, out GameObject rowGO)
    {
        var h = UIFactory.HBox(parent, 8, false, "Row");
        UIFactory.LE(h.gameObject, minH: 20);
        rowGO = h.gameObject;
        var l = UIFactory.Text(h.transform, label, 14, UITheme.TexteSecondaire);
        UIFactory.LE(l.gameObject, flexW: 1);
        return UIFactory.Text(h.transform, "—", 14, UITheme.TextePrincipal, true, TextAlignmentOptions.Right);
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

    private static void Set(TMP_Text t, string v) { if (t != null) t.text = v; }
}