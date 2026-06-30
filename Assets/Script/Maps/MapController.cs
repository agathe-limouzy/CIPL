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
        searchButton.onClick.AddListener(OnSearchClicked);
        addressInput.onSubmit.AddListener(_ => OnSearchClicked());
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


    public void SetAdress(string newAdress)
    {
    
        addressInput.text = newAdress;
        adressOutput.text = newAdress;
        adressOutput.gameObject.SetActive(true);
        addressInput.gameObject.SetActive(false);
        searchButton.gameObject.SetActive(false);
        statusText.gameObject.SetActive(true); // visible pendant le chargement
        _pendingAddress = newAdress;
        StartCoroutine(SearchAddress(newAdress)); // passe l'adresse directement, pas de DelayedSearch
   
}

  

    public void ModifyAdress()
    {
        adressOutput.gameObject.SetActive(false);
        addressInput.gameObject.SetActive(true);
        searchButton.gameObject.SetActive(true);
        statusText.gameObject.SetActive(true);
    }

    public string GetAdress()
    {
        adressOutput.text = addressInput.text;
        adressOutput.gameObject.SetActive(true);
        addressInput.gameObject.SetActive(false);
        searchButton.gameObject.SetActive(false);
        statusText.gameObject.SetActive(false);
        OnSearchClicked();
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

    public void PanByPixels(Vector2 pixelDelta)
    {
        if (_isLoading) return;

        double lonPerPixel = 360.0 / (256.0 * System.Math.Pow(2, _currentZoom));
        double latPerPixel = lonPerPixel * System.Math.Cos(_centerLat * System.Math.PI / 180.0);

        _centerLon -= pixelDelta.x * lonPerPixel;
        _centerLat += pixelDelta.y * latPerPixel;

        _centerLat = System.Math.Max(-85.0, System.Math.Min(85.0, _centerLat));
        _centerLon = ((_centerLon + 180) % 360 + 360) % 360 - 180;

        ReloadMap();
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
        }

        if (searchButton.gameObject.activeSelf)
            searchButton.interactable = true;
    }
    

    public double getLat()
    {
        return _pinLat;
    }
    public double getLon()
    {
        return _pinLon;
    }

    private IEnumerator OnAddressFound()
    {
        _isLoading = true;
        _tileLoader.zoomLevel = _currentZoom;
        statusText.text = "Chargement...";

        // Centre bouge, pin reste fixe
        yield return StartCoroutine(_tileLoader.LoadMapAt(_centerLat, _centerLon, _pinLat, _pinLon));

        if (showDebugCoords)
            statusText.text = $" lat={_pinLat:F6}  lon={_pinLon:F6}  zoom={_currentZoom}";
        else
            statusText.text = " Carte chargée";
        AdressUpdate.Invoke();
        searchButton.interactable = true;
        _isLoading = false;
    }
}