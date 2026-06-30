using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PLUOverlayPanel : MonoBehaviour
{
    public static PLUOverlayPanel Instance { get; private set; }

    [Header("Structure")]
    public GameObject overlayRoot;       // Le panel entier (SetActive)
   // public Button btnBackground;         // Fond semi-transparent cliquable
    public Button btnFermer;

    [Header("Adresse")]
    public TMP_InputField inputAdresse;
    public Button btnRechercher;         // Visible seulement en mode libre
    public TMP_Text txtModeLabel;        // ex: "Bâtiment : 6 route d'Agde"

    [Header("Infos zone")]
    public TMP_Text txtZone;
    public TMP_Text txtTypeLabel;
    public TMP_Text txtDescription;
    public TMP_Text txtCommune;
    public TMP_Text txtDate;
    public Image zoneColorBadge;

    [Header("Commerce")]
    public Transform commerceListParent;
    public GameObject commerceRowPrefab;
    public TMP_Text txtDisclaimer;

    [Header("Boutons accès")]
    public Button btnReglement;
    public Button btnGeoportail;

    [Header("États")]
    public GameObject loadingPanel;
    public GameObject contentPanel;
    public GameObject errorPanel;
    public TMP_Text txtError;
    public Button btnRefresh;

    // ── Internes ──────────────────────────────────────────────────────────────

    private enum Mode { Batiment, Libre }
    private Mode _mode;
    private string _lastAddress;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        btnFermer?.onClick.AddListener(Close);
       // btnBackground?.onClick.AddListener(Close);
        btnRechercher?.onClick.AddListener(OnRechercher);
        btnRefresh?.onClick.AddListener(OnRefresh);

        overlayRoot.SetActive(false);
    }

    // ── API publique ──────────────────────────────────────────────────────────

    /// <summary>
    /// Ouverture depuis un bâtiment.
    /// L'adresse est pré-remplie et non éditable.
    /// </summary>
    public void OpenWithBatiment(string adresse)
    {
        _mode = Mode.Batiment;
        _lastAddress = adresse;

        inputAdresse.text = adresse;
        inputAdresse.interactable = false;
        btnRechercher.gameObject.SetActive(false);

        overlayRoot.SetActive(true);
        ShowLoading();
        StartCoroutine(RechercherPLU(adresse));
    }

    /// <summary>
    /// Ouverture depuis le menu général.
    /// L'utilisateur saisit une adresse libre.
    /// </summary>
    public void OpenFreeSearch()
    {
        _mode = Mode.Libre;

        inputAdresse.text = "";
        inputAdresse.interactable = true;
        btnRechercher.gameObject.SetActive(true);

        overlayRoot.SetActive(true);
        ShowEmpty();
    }

    public void Close()
    {
        overlayRoot.SetActive(false);
    }

    // ── Recherche ─────────────────────────────────────────────────────────────

    private void OnRechercher()
    {
        string adresse = inputAdresse.text.Trim();
        if (string.IsNullOrEmpty(adresse))
        {
            ShowError("Veuillez saisir une adresse.");
            return;
        }
        _lastAddress = adresse;
        ShowLoading();
        StartCoroutine(RechercherPLU(adresse));
    }

    private void OnRefresh()
    {
        if (string.IsNullOrEmpty(_lastAddress)) return;
        ShowLoading();
        StartCoroutine(RechercherPLU(_lastAddress));
    }

    private IEnumerator RechercherPLU(string adresse)
    {
        // 1 — Géocodage
        double lat = 0, lon = 0;
        bool geocodeOk = false;
        string geocodeError = "";

        yield return StartCoroutine(
            GeoCodingService.Instance.GeocodeAddress(adresse,
                (la, lo) => { lat = la; lon = lo; geocodeOk = true; },
                err => { geocodeError = err; }));

        if (!geocodeOk)
        {
            ShowError($"Adresse introuvable : {geocodeError}");
            yield break;
        }

        // 2 — PLU
        bool pluOk = false;
        PLUZoneInfo zoneInfo = null;
        string pluError = "";

        PLUService.Instance.GetZoneInfo(lat, lon,
            info => { zoneInfo = info; pluOk = true; },
            err => { pluError = err; });

        // Attend la réponse PLU (callback asynchrone via coroutine interne)
        float timeout = 10f;
        float elapsed = 0f;
        while (!pluOk && string.IsNullOrEmpty(pluError) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!pluOk)
        {
            ShowError(string.IsNullOrEmpty(pluError)
                ? "Délai dépassé — réessayez."
                : pluError);
            yield break;
        }

        DisplayInfo(zoneInfo, lat, lon);
    }

    // ── Affichage ─────────────────────────────────────────────────────────────

    private void DisplayInfo(PLUZoneInfo info, double lat, double lon)
    {
        ShowContent();

        if (txtZone != null) txtZone.text = info.libelle ?? "—";
        if (txtTypeLabel != null) txtTypeLabel.text = info.TypeLabel;
        if (txtDescription != null)
            txtDescription.text = string.IsNullOrEmpty(info.libelong) ? "—" : info.libelong;
        if (txtCommune != null)
            txtCommune.text = !string.IsNullOrEmpty(info.nomcom)
                ? $"{info.nomcom} ({info.insee})" : "—";
        if (txtDate != null)
            txtDate.text = !string.IsNullOrEmpty(info.datappro)
                ? $"Approuvé le {info.datappro}" : "Date inconnue";

        if (zoneColorBadge != null &&
            ColorUtility.TryParseHtmlString(info.TypeColor, out var col))
            zoneColorBadge.color = col;

        BuildCommerceList(info.CommercesIndicatifs);

        if (txtDisclaimer != null)
            txtDisclaimer.text =
                "⚠ Données indicatives — vérifiez le règlement officiel de la commune.";

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
                lat, lon);
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
            var txts = row.GetComponentsInChildren<TMP_Text>();

            if (txts.Length >= 1) txts[0].text = entry.Label;
            if (txts.Length >= 2)
            {
                txts[1].text = $"{entry.StatusIcon} {entry.StatusLabel}";
                if (ColorUtility.TryParseHtmlString(entry.StatusColor, out var col))
                    txts[1].color = col;
            }

            var imgs = row.GetComponentsInChildren<Image>();
            if (imgs.Length > 0 &&
                ColorUtility.TryParseHtmlString(entry.StatusColor, out var imgCol))
                imgs[imgs.Length - 1].color = imgCol;
        }

        StartCoroutine(RebuildLayout());
    }

    private IEnumerator RebuildLayout()
    {
        yield return new WaitForEndOfFrame();
        var rt = commerceListParent.GetComponent<RectTransform>();
        if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        var contentRt = contentPanel?.GetComponent<RectTransform>();
        if (contentRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
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

    private void ShowEmpty()
    {
        loadingPanel?.SetActive(false);
        contentPanel?.SetActive(false);
        errorPanel?.SetActive(false);
    }
}