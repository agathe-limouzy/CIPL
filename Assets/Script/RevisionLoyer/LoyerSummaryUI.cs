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

        // Badge rouge si révision dépassée
        if (badgeRevision != null)
            badgeRevision.SetActive(EstRevisionDue(loc));
    }

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