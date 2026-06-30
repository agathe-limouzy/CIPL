using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PLUPanelUI : MonoBehaviour
{
    [Header("Infos zone")]
    public TextMeshProUGUI txtZone;         // "UA"
    public TextMeshProUGUI txtTypeLabel;    // "Zone Urbaine — constructible"
    public TextMeshProUGUI txtDescription;  // libelong
    public TextMeshProUGUI txtCommune;      // "Lyon (69001)"
    public TextMeshProUGUI txtDate;         // "Approuvé le 01/01/2021"
    public Image zoneColorBadge;            // Cercle coloré selon le type

    [Header("Commerce")]
    public Transform commerceListParent;    // Parent du ScrollView ou VLG
    public GameObject commerceRowPrefab;    // Prefab : Image(couleur) + TxtNom + TxtStatus

    [Header("Boutons")]
    public Button btnReglement;             // Ouvre le PDF règlement
    public Button btnGeoportail;            // Ouvre Géoportail Urbanisme
    public Button btnRefresh;              // Relancer la requête

    [Header("États")]
    public GameObject loadingPanel;
    public GameObject contentPanel;
    public GameObject errorPanel;
    public TextMeshProUGUI txtError;

    [Header("Avertissement")]
    public TextMeshProUGUI txtDisclaimer;   // "Données indicatives — vérifier le règlement"

    // ── Internes ──────────────────────────────────────────────────────────────

    private double _lat, _lon;
    private PLUZoneInfo _currentInfo;

    // ── API publique ──────────────────────────────────────────────────────────

    public void Initialize(double lat, double lon)
    {
        _lat = lat;
        _lon = lon;

        btnRefresh?.onClick.RemoveAllListeners();
        btnRefresh?.onClick.AddListener(LoadZone);

        ShowLoading();
        LoadZone();
    }

    // ── Chargement ────────────────────────────────────────────────────────────

    private void LoadZone()
    {
        ShowLoading();
        PLUService.Instance.GetZoneInfo(_lat, _lon,
            onSuccess: DisplayInfo,
            onError: ShowError);
    }

    // ── Affichage ─────────────────────────────────────────────────────────────

    private void DisplayInfo(PLUZoneInfo info)
    {
        _currentInfo = info;
        ShowContent();

        // Infos zone
        if (txtZone != null)
            txtZone.text = info.libelle ?? "—";

        if (txtTypeLabel != null)
            txtTypeLabel.text = info.TypeLabel;

        if (txtDescription != null)
            txtDescription.text = string.IsNullOrEmpty(info.libelong) ? "—" : info.libelong;

        if (txtCommune != null)
            txtCommune.text = (!string.IsNullOrEmpty(info.nomcom))
                ? $"{info.nomcom} ({info.insee})"
                : "—";

        if (txtDate != null)
            txtDate.text = (!string.IsNullOrEmpty(info.datappro))
                ? $"Approuvé le {info.datappro}"
                : "Date inconnue";

        // Badge couleur
        if (zoneColorBadge != null &&
            ColorUtility.TryParseHtmlString(info.TypeColor, out var zoneCol))
            zoneColorBadge.color = zoneCol;

        // Liste commerce
        BuildCommerceList(info.CommercesIndicatifs);

        // Disclaimer
        if (txtDisclaimer != null)
            txtDisclaimer.text =
                "⚠ Données indicatives basées sur les conventions PLU habituelles.\n" +
                "Vérifiez toujours le règlement officiel de la commune.";

        // Bouton règlement PDF
        if (btnReglement != null)
        {
            btnReglement.gameObject.SetActive(info.HasReglement);
            btnReglement.onClick.RemoveAllListeners();
            if (info.HasReglement)
                btnReglement.onClick.AddListener(() => Application.OpenURL(info.urlfic));
        }

        // Bouton Géoportail
        if (btnGeoportail != null)
        {
            string geoUrl = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "https://www.geoportail-urbanisme.gouv.fr/map/#zoom=17&lat={0}&lon={1}",
                _lat, _lon);

            btnGeoportail.onClick.RemoveAllListeners();
            btnGeoportail.onClick.AddListener(() => Application.OpenURL(geoUrl));
        }
    }

    private void BuildCommerceList(List<CommerceEntry> entries)
    {
        if (commerceListParent == null || commerceRowPrefab == null) return;

        foreach (Transform child in commerceListParent)
            Destroy(child.gameObject);

        foreach (var entry in entries)
        {
            var row = Instantiate(commerceRowPrefab, commerceListParent);

            var txts = row.GetComponentsInChildren<TextMeshProUGUI>();
            if (txts.Length >= 1) txts[0].text = entry.Label;
            if (txts.Length >= 2)
            {
                txts[1].text = $"{entry.StatusIcon} {entry.StatusLabel}";
                if (ColorUtility.TryParseHtmlString(entry.StatusColor, out var col))
                    txts[1].color = col;
            }

            var imgs = row.GetComponentsInChildren<Image>();
            // imgs[0] = background de la row (si existe), imgs[1] = rond coloré
            // On cible la dernière image comme badge couleur
            if (imgs.Length > 0 &&
                ColorUtility.TryParseHtmlString(entry.StatusColor, out var imgCol))
                imgs[imgs.Length - 1].color = imgCol;
        }

        // Rebuild immédiat sur toute la hiérarchie vers le haut
        StartCoroutine(RebuildAfterFrame());
    }

    private IEnumerator RebuildAfterFrame()
    {
        // Attend la fin du frame pour que les Destroy soient effectifs
        yield return new WaitForEndOfFrame();

        // Rebuild de bas en haut
        var rt = commerceListParent.GetComponent<RectTransform>();
        if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        var contentRt = contentPanel?.GetComponent<RectTransform>();
        if (contentRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

        // Remonte jusqu'au ScrollRect pour forcer le recalcul global
        Transform t = transform;
        while (t != null)
        {
            var trt = t.GetComponent<RectTransform>();
            if (trt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(trt);
            if (t.GetComponent<ScrollRect>() != null) break;
            t = t.parent;
        }

        // Notifie le ScrollAutoResize si présent
        var scrollAutoResizes = GetComponentsInParent<ScrollAutoResize>(true);
        foreach (var sar in scrollAutoResizes)
            sar.SetDirty();
    }
    // ── États ─────────────────────────────────────────────────────────────────

    private void ShowLoading()
    {
        loadingPanel?.SetActive(true);
        contentPanel?.SetActive(false);
        errorPanel?.SetActive(false);
    }

    private void ShowContent()
    {
        loadingPanel?.SetActive(false);
        contentPanel?.SetActive(true);
        errorPanel?.SetActive(false);
    }

    private void ShowError(string message)
    {
        loadingPanel?.SetActive(false);
        contentPanel?.SetActive(false);
        errorPanel?.SetActive(true);
        if (txtError != null) txtError.text = message;
    }
}