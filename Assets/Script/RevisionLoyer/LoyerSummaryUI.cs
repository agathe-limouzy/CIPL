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
        float periodeHT = annuel / n + provision;
        string labelP = LabelPeriode(loc.periodiciteLoyer);

        Set(txtLoyerAnnuelHT, $"{annuel:N2} €");
        Set(txtLoyerAnnuelTTC, $"{annuel * TVA:N2} €");
        Set(txtLoyerM2, taille > 0 ? $"{annuel / taille:N2} €/m²" : "—");
        Set(txtLoyerPrecedent, loc.loyerAnnuelPrecedent > 0f
            ? $"{loc.loyerAnnuelPrecedent:N2} €"
            : "—");   // nul tant qu'aucune révision n'a eu lieu
        Set(txtProvisions, loc.provisionPourCharges
            ? $"{provision:N2} € / {labelP}" : "Aucune");
        Set(txtLoyerPeriodeHT, $"{periodeHT:N2} € / {labelP}");
        Set(txtLoyerPeriodeTTC, $"{periodeHT * TVA:N2} € / {labelP}");

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