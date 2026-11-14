using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Events;

public class MapController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField addressInput;
    public TMP_Text adressOutput;
    public Button searchButton;
    public TMP_Text statusText;
    public RawImage mapImage;

    [Header("Debug (optionnel)")]
    public bool showDebugCoords = true;

    [Header("Interaction")]
    [Range(1, 20)] public int minZoom = 10;
    [Range(1, 20)] public int maxZoom = 18;

    private GeoCodingService _geocodingService;
    private TileLoader _tileLoader;
    private int _currentZoom;
    private bool _isLoading = false;

    // Remplace _lastLat, _lastLon par deux paires :
    private double _centerLat, _centerLon;  // bouge avec le drag
    private double _pinLat, _pinLon;  // reste sur l'adresse d'origine

    public UnityEvent AdressUpdate = new UnityEvent();

    private void Awake()
    {
        _geocodingService = GetComponent<GeoCodingService>();
        _tileLoader = GetComponent<TileLoader>();
        _tileLoader.targetImage = mapImage;
        _currentZoom = _tileLoader.zoomLevel; // ← hérite du zoom initial du TileLoader
        AdressUpdate= new UnityEvent();
    }

    private void Start()
    {
        AjouterBoutonsZoom();   // retour de BW6 (13/11)
        searchButton.onClick.AddListener(OnSearchClicked);
        addressInput.onSubmit.AddListener(_ => OnSearchClicked());
        AdresseAutocomplete.Attach(addressInput, _ => OnSearchClicked(), Zone,   // retour de BW17 (14/11) : adresses proposées à la frappe
            () => ReglageService.Current?.lieuEmission);   // premier bâtiment de l'entreprise : la ville du lieu d'émission
        CadreAdresseExtensible();
    }

    // ─── Recherche ─────────────────────────────────────────────────────────
    private void OnSearchClicked()
    {
        string address = addressInput.text.Trim();
        if (string.IsNullOrEmpty(address)) return;
        StartCoroutine(SearchAddress(address));
    }

    // ─── API publique (BatimentPrefab) ─────────────────────────────────────

    private string _pendingAddress;

    private void Update()
    {
        // Tente le chargement dès que l'objet est actif et prêt
        if (!string.IsNullOrEmpty(_pendingAddress) && !_isLoading && gameObject.activeInHierarchy)
        {
            string addr = _pendingAddress;
            _pendingAddress = null;
            StartCoroutine(SearchAddress(addr));
        }
    }


    // Adresse dont la carte est déjà à l'écran : la rouvrir ne doit rien retélécharger.
    private string _adresseAffichee;
    // Revue 17 (D9-19, lot D) : adresse dont la recherche est partie (géocodage ou image en cours). GetAdress puis
    // SetAdress (« Sauvegarder ») lançaient sinon deux recherches en parallèle pour une adresse changée.
    private string _adresseEnCours;

    // Revue 17 (D9-19, lot D) : une fiche masquée arrête ses coroutines ; sans cette remise, la carte restait « en
    // chargement » (plus de zoom ni de glisser) et l'adresse « en recherche » ne repartait plus.
    private void OnDisable()
    {
        _isLoading = false;
        _adresseEnCours = null;
        RemettreImage();
    }

    public void SetAdress(string newAdress)
    {
        addressInput.text = newAdress;
        adressOutput.text = newAdress;
        adressOutput.gameObject.SetActive(true);
        addressInput.gameObject.SetActive(false);
        searchButton.gameObject.SetActive(false);

        // Carte déjà chargée pour cette adresse : rien à refaire. Chaque ouverture de
        // fiche relançait sinon un géocodage ET un téléchargement de carte pour
        // une adresse inchangée — le vrai coût de l'ouverture, mesuré en réseau et non
        // en code (le basculement d'onglet, lui, prend 0 ms).
        string a = (newAdress ?? "").Trim();
        if (a == _adresseAffichee)
        {
            _pendingAddress = null;
            statusText.gameObject.SetActive(false);
            return;
        }

        statusText.gameObject.SetActive(true); // visible pendant le chargement
        if (a == _adresseEnCours) return;      // Revue 17 (D9-19, lot D) : déjà en recherche (GetAdress), pas une seconde fois

        // UNE seule demande : `Update` la lance dès que l'objet est actif. Poser la
        // demande ET lancer la coroutine ici faisait partir la recherche deux fois —
        // deux géocodages et deux tuiles pour la même adresse, visibles dans la console.
        _pendingAddress = newAdress;
    }

  

    public void ModifyAdress()
    {
        adressOutput.gameObject.SetActive(false);
        addressInput.gameObject.SetActive(true);
        searchButton.gameObject.SetActive(true);
        statusText.gameObject.SetActive(true);
    }

    /// Revue 17 (D9-19, lot D) : l'adresse saisie, sans effet de bord (le PLU la lit sans relancer la carte).
    public string Adresse => addressInput.text;

    public string GetAdress()
    {
        adressOutput.text = addressInput.text;
        adressOutput.gameObject.SetActive(true);
        addressInput.gameObject.SetActive(false);
        searchButton.gameObject.SetActive(false);
        statusText.gameObject.SetActive(false);
        // Revue 17 (D9-19, lot D) : chaque « Sauvegarder » relançait un géocodage et une image Google (facturée),
        // même adresse inchangée. La recherche ne part que si l'adresse a changé, et une seule fois.
        string a = addressInput.text.Trim();
        if (a != _adresseAffichee && a != _adresseEnCours) OnSearchClicked();
        return addressInput.text;
    }

    // ─── Interaction (appelée par MapInteractable) ──────────────────────────

    public void ZoomIn()
    {
        if (_currentZoom >= maxZoom || _isLoading) return;
        _currentZoom++;
        ReloadMap();
    }

    public void ZoomOut()
    {
        if (_currentZoom <= minZoom || _isLoading) return;
        _currentZoom--;
        ReloadMap();
    }

    // Revue 17 (D7b-20, lot D) : appelé une seule fois par geste, au relâchement (MapInteractable). Refusé (image en
    // cours, pas encore d'adresse) : l'image décalée par le glisser revient en place.
    public void PanByPixels(Vector2 pixelDelta)
    {
        if (_isLoading || (_pinLat == 0 && _pinLon == 0)) { RemettreImage(); return; }
        (_centerLat, _centerLon) = Deplacer(_centerLat, _centerLon, pixelDelta, _currentZoom);
        ReloadMap();
    }

    // Revue 17 (D7b-15, lot D) : le glisser horizontal partait à l'envers du doigt (_centerLon -= …) alors que le
    // vertical le suivait. pixelDelta = ancienne − nouvelle position du doigt, en pixels d'image, y vers le haut :
    // doigt vers la droite → le centre part vers l'ouest ; doigt vers le haut → vers le sud. La carte suit le doigt.
    public static (double lat, double lon) Deplacer(double lat, double lon, Vector2 pixelDelta, int zoom)
    {
        // Tuiles de 256 px : le zoom de Google (étape 16b), un cran au-dessus de celui des prefabs.
        double lonPerPixel = 360.0 / (256.0 * System.Math.Pow(2, zoom + TileLoader.DecalageZoom));
        double latPerPixel = lonPerPixel * System.Math.Cos(lat * System.Math.PI / 180.0);

        lon += pixelDelta.x * lonPerPixel;
        lat += pixelDelta.y * latPerPixel;

        lat = System.Math.Max(-85.0, System.Math.Min(85.0, lat));
        lon = ((lon + 180) % 360 + 360) % 360 - 180;
        return (lat, lon);
    }

    // Retour de BW6 (13/11) : boutons + et − en haut à droite de la carte. Aucun n'existait (le zoom n'avait que Ctrl + molette,
    // invisible) ; un clic sur eux ne passe pas au clic de la carte (Google Maps), le Button le prend.
    /// Retour de BW17 (14/11) : la zone où chercher les adresses proposées : le repère de cette carte, sinon celui d'un autre
    /// bâtiment de l'entreprise (bâtiment neuf, sans carte) ; null si aucune carte n'est connue (la liste prend alors la ville du
    /// lieu d'émission des Réglages, décision du 14/11).
    (double lat, double lon)? Zone()
    {
        if (_pinLat != 0 || _pinLon != 0) return (_pinLat, _pinLon);
        foreach (var autre in FindObjectsOfType<MapController>(true))
            if (autre != this && (autre._pinLat != 0 || autre._pinLon != 0)) return (autre._pinLat, autre._pinLon);
        return null;
    }

    /// Retour de BW17 (14/11) : le bloc « Adresse du Bâtiment » (Adress) grandit avec sa colonne (Presentation) : sa hauteur
    /// était fixe, un message de plusieurs lignes sous le champ faisait déborder la colonne, et son titre chevauchait Cadastre.
    void CadreAdresseExtensible()
    {
        var colonne = statusText != null ? statusText.transform.parent : null;
        var bloc = colonne != null ? colonne.parent : null;
        var h = bloc != null ? bloc.GetComponent<HorizontalLayoutGroup>() : null;
        if (h == null) return;
        h.childControlHeight = true;
        h.childForceExpandHeight = false;
        // Le parent dimensionne : les ajusteurs de taille de la colonne et du bloc se battraient avec lui.
        foreach (var f in new[] { colonne.GetComponent<ContentSizeFitter>(), bloc.GetComponent<ContentSizeFitter>() })
            if (f != null) f.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    void AjouterBoutonsZoom()
    {
        if (mapImage == null || mapImage.transform.Find("ZoomCarte") != null) return;
        var boite = UIFactory.Rect("ZoomCarte", mapImage.transform);
        boite.anchorMin = boite.anchorMax = boite.pivot = new Vector2(1f, 1f);
        boite.anchoredPosition = new Vector2(-6f, -6f);
        boite.sizeDelta = new Vector2(30f, 64f);
        var v = boite.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 4f;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var plus = UIFactory.Button(boite, "+", UITheme.Carte, UITheme.TextePrincipal, 30);
        var moins = UIFactory.Button(boite, "−", UITheme.Carte, UITheme.TextePrincipal, 30);
        UIFactory.Border(plus.gameObject);
        UIFactory.Border(moins.gameObject);
        plus.onClick.AddListener(ZoomIn);
        moins.onClick.AddListener(ZoomOut);
    }

    // Le glisser décale la texture dans son cadre (uvRect) ; remise en place quand la nouvelle image arrive.
    private void RemettreImage()
    {
        if (mapImage != null) mapImage.uvRect = new Rect(0, 0, 1, 1);
    }
    

    public void OpenInGoogleMaps()
    {
        string address = addressInput.text.Trim();
        if (string.IsNullOrEmpty(address)) return;
        string encoded = UnityEngine.Networking.UnityWebRequest.EscapeURL(address);
        Application.OpenURL("https://www.google.com/maps/search/" + encoded);
    }
    // ─── Coroutines internes ────────────────────────────────────────────────

    private void ReloadMap()
    {
        if (_pinLat == 0 && _pinLon == 0) return;
        StartCoroutine(OnAddressFound());
    }

    private IEnumerator SearchAddress(string address)
    {
        address = (address ?? "").Trim();
        _adresseEnCours = address;   // Revue 17 (D9-19, lot D)
        if (searchButton.gameObject.activeSelf)
            searchButton.interactable = false;
        statusText.text = "Recherche...";

        double resultLat = 0, resultLon = 0;
        bool success = false;

        // Géocodage
        yield return StartCoroutine(_geocodingService.GeocodeAddress(
            address,
            onSuccess: (lat, lon) => { resultLat = lat; resultLon = lon; success = true; },
            onError: (err) => { statusText.text = $" {err}"; }
        ));

        // Chargement carte (séquentiel, plus dans un callback)
        if (success)
        {
            _pinLat = resultLat; _pinLon = resultLon;
            _centerLat = resultLat; _centerLon = resultLon;
            _currentZoom = _tileLoader.zoomLevel;
            yield return StartCoroutine(OnAddressFound()); // ← yield propre
            // Revue 17 (D7b-18, lot D) : marquée affichée seulement si l'image est posée (hors cloud, quota, panne
            // Google : rouvrir la fiche retente au lieu de garder un cadre vide toute la session). Hors cloud, retenter ne
            // sert à rien (pas de clé) : l'adresse est marquée quand même, sinon chaque « Sauvegarder » regéocodait (D9-19).
            if (_tileLoader.ImagePosee || !SessionCloud.Active) _adresseAffichee = address;   // la carte correspond à cette adresse
        }
        if (_adresseEnCours == address) _adresseEnCours = null;

        if (searchButton.gameObject.activeSelf)
            searchButton.interactable = true;
    }
    


    private IEnumerator OnAddressFound()
    {
        _isLoading = true;
        _tileLoader.zoomLevel = _currentZoom;
        statusText.text = "Chargement...";

        // Centre bouge, pin reste fixe
        yield return StartCoroutine(_tileLoader.LoadMapAt(_centerLat, _centerLon, _pinLat, _pinLon));
        RemettreImage();

        // Revue 17 (D7b-18, lot D) : « Carte chargée » s'affichait aussi quand aucune image n'était posée.
        if (!_tileLoader.ImagePosee)
            statusText.text = " Carte indisponible";
        else if (showDebugCoords)
            statusText.text = $" lat={_pinLat:F6}  lon={_pinLon:F6}  zoom={_currentZoom}";
        else
            statusText.text = " Carte chargée";
        AdressUpdate.Invoke();
        searchButton.interactable = true;
        _isLoading = false;
    }
}