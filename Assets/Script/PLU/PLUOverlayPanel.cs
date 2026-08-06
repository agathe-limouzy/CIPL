using System;
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
    public Button btnFermer;

    [Header("Mode de recherche")]
    public Button btnModeAdresse;        // onglet « Adresse »
    public Button btnModeCadastre;       // onglet « Cadastre »
    public GameObject groupeAdresse;     // conteneur : champ adresse + bouton
    public GameObject groupeCadastre;    // conteneur : commune / section / numéro + bouton

    [Header("Recherche adresse")]
    public TMP_InputField inputAdresse;
    public Button btnRechercher;         // visible seulement en mode libre
    public TMP_Text txtModeLabel;        // ex: "Bâtiment : 6 route d'Agde"

    [Header("Recherche cadastre")]
    public TMP_InputField inputCommune;  // nom ou code postal
    public TMP_InputField inputSection;  // ex: AB
    public TMP_InputField inputNumero;   // ex: 0142
    public Button btnRechercherCadastre;

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

    private enum SearchMode { Adresse, Cadastre }
    private SearchMode _searchMode = SearchMode.Adresse;

    // Dernière recherche lancée — rejouée par le bouton « Réessayer ».
    private Func<IEnumerator> _lastSearch;

    // Cache des résultats par mode : chaque mode conserve son dernier zonage,
    // restauré quand on rebascule dessus (vide s'il n'a pas encore été cherché).
    private PLUZoneInfo _cacheAdr, _cacheCad;
    private double _cLatAdr, _cLonAdr, _cLatCad, _cLonCad;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        btnFermer?.onClick.AddListener(Close);
        btnRechercher?.onClick.AddListener(OnRechercher);
        btnRechercherCadastre?.onClick.AddListener(OnRechercherCadastre);
        btnRefresh?.onClick.AddListener(OnRefresh);

        btnModeAdresse?.onClick.AddListener(() => SetSearchMode(SearchMode.Adresse));
        btnModeCadastre?.onClick.AddListener(() => SetSearchMode(SearchMode.Cadastre));

        overlayRoot.SetActive(false);
    }

    private IEnumerator RebuildPanelAtOpen()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        var rt = overlayRoot.transform as RectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        var contentRt = contentPanel != null ? contentPanel.GetComponent<RectTransform>() : null;
        if (contentRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

        Canvas.ForceUpdateCanvases();
    }

    // ── API publique ──────────────────────────────────────────────────────────

    /// <summary>
    /// Ouverture depuis un bâtiment : adresse pré-remplie et verrouillée.
    /// L'utilisateur peut basculer sur le mode Cadastre (commune pré-remplie).
    /// </summary>
    public void OpenWithBatiment(string adresse, string cadastral = null)
    {
        _mode = Mode.Batiment;
        _cacheAdr = null; _cacheCad = null;

        inputAdresse.text = adresse;
        inputAdresse.interactable = false;
        if (btnRechercher != null) btnRechercher.gameObject.SetActive(false);

        // Pré-remplit le mode cadastre : commune géocodée depuis l'adresse, section
        // et numéro extraits de la référence cadastrale stockée sur le bâtiment.
        if (inputCommune != null)
        {
            inputCommune.text = BuildingRowUI.Ville(adresse);   // estimation immédiate
            // Lancée sur CadastreService (toujours actif) : le panneau n'est pas
            // encore activé à ce stade, StartCoroutine dessus échouerait.
            if (CadastreService.Instance != null && !string.IsNullOrWhiteSpace(adresse))
                CadastreService.Instance.StartCoroutine(
                    CadastreService.Instance.ResolveCityFromAddress(adresse, city =>
                    {
                        if (this != null && inputCommune != null && !string.IsNullOrEmpty(city))
                            inputCommune.text = city;
                    }));
        }
        ParseCadastral(cadastral, out var sec, out var num);
        if (inputSection != null) inputSection.text = sec;
        if (inputNumero != null) inputNumero.text = num;

        if (txtModeLabel != null) txtModeLabel.text = $"Bâtiment : {adresse}";

        SetSearchMode(SearchMode.Adresse);

        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();
        StartCoroutine(RebuildPanelAtOpen());

        _lastSearch = () => RechercherPLU(adresse);
        ShowLoading();
        StartCoroutine(_lastSearch());
    }

    /// <summary>
    /// Ouverture depuis le menu général : saisie libre (adresse ou cadastre).
    /// </summary>
    public void OpenFreeSearch()
    {
        _mode = Mode.Libre;
        _cacheAdr = null; _cacheCad = null;

        inputAdresse.text = "";
        inputAdresse.interactable = true;
        if (btnRechercher != null) btnRechercher.gameObject.SetActive(true);

        if (inputCommune != null) inputCommune.text = "";
        if (inputSection != null) inputSection.text = "";
        if (inputNumero != null) inputNumero.text = "";

        if (txtModeLabel != null) txtModeLabel.text = "";

        SetSearchMode(SearchMode.Adresse);

        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();
        StartCoroutine(RebuildPanelAtOpen());

        _lastSearch = null;
        ShowEmpty();
    }

    public void Close()
    {
        overlayRoot.SetActive(false);
    }

    // ── Bascule de mode ─────────────────────────────────────────────────────────

    private void SetSearchMode(SearchMode mode)
    {
        _searchMode = mode;

        if (groupeAdresse != null) groupeAdresse.SetActive(mode == SearchMode.Adresse);
        if (groupeCadastre != null) groupeCadastre.SetActive(mode == SearchMode.Cadastre);

        StyleToggle(btnModeAdresse, mode == SearchMode.Adresse);
        StyleToggle(btnModeCadastre, mode == SearchMode.Cadastre);

        // Chaque mode affiche SES propres résultats : restaure le dernier zonage
        // trouvé dans le mode ciblé, ou reste vide s'il n'a pas encore été cherché
        // (les résultats d'une adresse ne débordent pas sur le cadastre).
        var cached = mode == SearchMode.Adresse ? _cacheAdr : _cacheCad;
        if (cached != null)
            DisplayInfo(cached,
                mode == SearchMode.Adresse ? _cLatAdr : _cLatCad,
                mode == SearchMode.Adresse ? _cLonAdr : _cLonCad);
        else
            ShowEmpty();

        if (isActiveAndEnabled) StartCoroutine(RebuildPanelAtOpen());
    }

    private static void StyleToggle(Button b, bool active)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null) img.color = active ? Hex("#534AB7") : Hex("#FCFBF8");
        var txt = b.GetComponentInChildren<TMP_Text>(true);
        if (txt != null) txt.color = active ? Hex("#EEEDFE") : Hex("#3C3489");
    }

    private static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString(h, out var c);
        return c;
    }

    /// Extrait section (ex. "AB", "0A") et numéro (ex. "142") d'une référence
    /// cadastrale saisie librement : "AB 142", "AB142", "0A-42", "AB/0142"…
    private static void ParseCadastral(string reference, out string section, out string numero)
    {
        section = ""; numero = "";
        if (string.IsNullOrWhiteSpace(reference)) return;

        var m = System.Text.RegularExpressions.Regex.Match(
            reference.Trim().ToUpperInvariant(),
            @"([0-9]?[A-Z]{1,2})\s*[-/ ]?\s*([0-9]{1,4})");
        if (m.Success)
        {
            section = m.Groups[1].Value;
            numero = m.Groups[2].Value;
        }
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
        _lastSearch = () => RechercherPLU(adresse);
        ShowLoading();
        StartCoroutine(_lastSearch());
    }

    private void OnRechercherCadastre()
    {
        string commune = inputCommune != null ? inputCommune.text.Trim() : "";
        string section = inputSection != null ? inputSection.text.Trim() : "";
        string numero = inputNumero != null ? inputNumero.text.Trim() : "";

        if (string.IsNullOrEmpty(commune))
        {
            ShowError("Indiquez la commune (nom ou code postal).");
            return;
        }
        if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(numero))
        {
            ShowError("Indiquez la section et le numéro de parcelle.");
            return;
        }
        if (CadastreService.Instance == null)
        {
            ShowError("Service cadastre indisponible.");
            return;
        }

        _lastSearch = () => RechercherParCadastre(commune, section, numero);
        ShowLoading();
        StartCoroutine(_lastSearch());
    }

    private void OnRefresh()
    {
        if (_lastSearch == null) return;
        ShowLoading();
        StartCoroutine(_lastSearch());
    }

    private IEnumerator RechercherPLU(string adresse)
    {
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

        yield return RechercherPLUByPoint(lat, lon);
    }

    private IEnumerator RechercherParCadastre(string commune, string section, string numero)
    {
        double lat = 0, lon = 0;
        bool ok = false;
        string error = "";

        yield return CadastreService.Instance.LocateParcelle(commune, section, numero,
            (la, lo) => { lat = la; lon = lo; ok = true; },
            err => { error = err; });

        if (!ok)
        {
            ShowError(string.IsNullOrEmpty(error) ? "Parcelle introuvable." : error);
            yield break;
        }

        yield return RechercherPLUByPoint(lat, lon);
    }

    /// Étape commune aux deux recherches : point (lat/lon) → zonage PLU.
    private IEnumerator RechercherPLUByPoint(double lat, double lon)
    {
        bool pluOk = false;
        PLUZoneInfo zoneInfo = null;
        string pluError = "";

        PLUService.Instance.GetZoneInfo(lat, lon,
            info => { zoneInfo = info; pluOk = true; },
            err => { pluError = err; });

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

        // Mémorise le résultat pour le mode courant (restauré à la bascule).
        if (_searchMode == SearchMode.Adresse) { _cacheAdr = zoneInfo; _cLatAdr = lat; _cLonAdr = lon; }
        else { _cacheCad = zoneInfo; _cLatCad = lat; _cLonCad = lon; }

        DisplayInfo(zoneInfo, lat, lon);
    }

    // ── Affichage ─────────────────────────────────────────────────────────────

    private void DisplayInfo(PLUZoneInfo info, double lat, double lon)
    {
        ShowContent();

        if (txtZone != null) txtZone.text = string.IsNullOrWhiteSpace(info.libelle) ? "—" : info.libelle.Trim();
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
                "Données indicatives — vérifiez le règlement officiel de la commune.";

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
                txts[1].text = entry.StatusLabel;
                if (ColorUtility.TryParseHtmlString(entry.StatusTextColor, out var tcol))
                    txts[1].color = tcol;
            }

            // Dernière image = fond de la pastille de statut.
            var imgs = row.GetComponentsInChildren<Image>();
            if (imgs.Length > 0 &&
                ColorUtility.TryParseHtmlString(entry.StatusBgColor, out var bcol))
                imgs[imgs.Length - 1].color = bcol;
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
