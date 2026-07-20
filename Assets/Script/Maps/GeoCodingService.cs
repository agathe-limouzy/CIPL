using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

[Serializable]
public class NominatimResult
{
    public string lat;
    public string lon;
    public string display_name;
}

public class GeoCodingService : MonoBehaviour

{

   

    // ── Avant ──

 

    // ── Après ──
    private static GeoCodingService _instance;

    /// Point d'accès global : renvoie n'importe quelle instance vivante
    /// (chaque MapController a la sienne, on ne détruit plus les doublons)
    public static GeoCodingService Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<GeoCodingService>();
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // API Adresse - gouvernement français (gratuit, sans clé, adresses FR)
    private const string ADRESSE_GOV_URL = "https://api-adresse.data.gouv.fr/search/";

    // Nominatim en fallback si l'adresse n'est pas trouvée
    private const string NOMINATIM_URL = "https://nominatim.openstreetmap.org/search";

    public IEnumerator GeocodeAddress(string address, Action<double, double> onSuccess, Action<string> onError)
    {
        // Tente d'abord l'API gouvernementale française
        bool found = false;

        yield return TryAdresseGouv(address,
            onSuccess: (lat, lon) => { found = true; onSuccess?.Invoke(lat, lon); },
            onError: _ => { /* silencieux, on essaie le fallback */ }
        );

        if (found) yield break;

        // Fallback : Nominatim
        Debug.Log("[GeoCoding] API gouv échouée, fallback Nominatim...");
        yield return TryNominatim(address, onSuccess, onError);
    }

    // ── API adresse.data.gouv.fr ──────────────────────────────────────────────

    private IEnumerator TryAdresseGouv(string address, Action<double, double> onSuccess, Action<string> onError)
    {
        string encodedAddress = UnityWebRequest.EscapeURL(address);
        string url = $"{ADRESSE_GOV_URL}?q={encodedAddress}&limit=1";

        Debug.Log($"[GeoCoding] API Gouv → {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 8;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error);
                yield break;
            }

            // Parse GeoJSON { features: [ { geometry: { coordinates: [lon, lat] }, properties: { label } } ] }
            GeoJsonResponse response = JsonUtility.FromJson<GeoJsonResponse>(request.downloadHandler.text);

            if (response?.features == null || response.features.Length == 0)
            {
                onError?.Invoke("Adresse introuvable");
                yield break;
            }

            var coords = response.features[0].geometry.coordinates;
            double lon = coords[0];
            double lat = coords[1];

            Debug.Log($"[GeoCoding] Gouv → lat={lat}, lon={lon} ({response.features[0].properties.label})");
            onSuccess?.Invoke(lat, lon);
        }
    }

    // ── Nominatim fallback ────────────────────────────────────────────────────

    private IEnumerator TryNominatim(string address, Action<double, double> onSuccess, Action<string> onError)
    {
        string encodedAddress = UnityWebRequest.EscapeURL(address);
        string url = $"{NOMINATIM_URL}?q={encodedAddress}&format=json&limit=1";

        Debug.Log($"[GeoCoding] Nominatim → {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("User-Agent", "CIPLApp/1.0 contact:agathelimouzy@gmail.com");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Service indisponible. Vérifiez votre connexion.");
                yield break;
            }

            string wrappedJson = "{\"results\":" + request.downloadHandler.text + "}";
            NominatimResponse response = JsonUtility.FromJson<NominatimResponse>(wrappedJson);

            if (response?.results == null || response.results.Length == 0)
            {
                onError?.Invoke("Adresse introuvable. Vérifiez le format (ex: 12 rue de la Paix, Paris)");
                yield break;
            }

            double lat = double.Parse(response.results[0].lat, System.Globalization.CultureInfo.InvariantCulture);
            double lon = double.Parse(response.results[0].lon, System.Globalization.CultureInfo.InvariantCulture);
            Debug.Log($"[GeoCoding] Nominatim → lat={lat}, lon={lon}");
            onSuccess?.Invoke(lat, lon);
        }
    }

    // ── Modèles JSON ──────────────────────────────────────────────────────────

    [Serializable]
    private class NominatimResponse
    {
        public NominatimResult[] results;
    }

    [Serializable]
    private class GeoJsonResponse
    {
        public GeoJsonFeature[] features;
    }

    [Serializable]
    private class GeoJsonFeature
    {
        public GeoJsonGeometry geometry;
        public GeoJsonProperties properties;
    }

    [Serializable]
    private class GeoJsonGeometry
    {
        public double[] coordinates; // [lon, lat]
    }

    [Serializable]
    private class GeoJsonProperties
    {
        public string label;
    }
}