using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class PLUService : MonoBehaviour
{
    public static PLUService Instance { get; private set; }

    private const string IGN_API = "https://apicarto.ign.fr/api/gpu/zone-urba";

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Récupère les informations PLU pour des coordonnées GPS.
    /// onSuccess → premier résultat trouvé
    /// onError   → message d'erreur lisible
    /// </summary>
    public void GetZoneInfo(double lat, double lon,
        Action<PLUZoneInfo> onSuccess,
        Action<string> onError = null)
    {
        StartCoroutine(FetchZone(lat, lon, onSuccess, onError));
    }

    private IEnumerator FetchZone(double lat, double lon,
        Action<PLUZoneInfo> onSuccess,
        Action<string> onError)
    {
        string geom = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{{\"type\":\"Point\",\"coordinates\":[{0},{1}]}}",
            lon, lat);

        string url = $"{IGN_API}?geom={UnityWebRequest.EscapeURL(geom)}";
        Debug.Log($"[PLU] Requête → {url}");

        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Accept", "application/json");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string err = $"Erreur réseau ({req.responseCode}) : {req.error}";
            Debug.LogError($"[PLU] {err}");
            onError?.Invoke(err);
            yield break;
        }

        FeatureCollection wrapper;
        try
        {
            wrapper = JsonUtility.FromJson<FeatureCollection>(req.downloadHandler.text);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PLU] Parse JSON échoué : {e.Message}");
            onError?.Invoke("Impossible de lire la réponse du serveur.");
            yield break;
        }

        if (wrapper == null || wrapper.features == null || wrapper.features.Length == 0)
        {
            onError?.Invoke("Aucune zone PLU trouvée pour cette adresse.\nVérifiez les coordonnées GPS du bâtiment.");
            yield break;
        }

        Debug.Log($"[PLU] Zone trouvée : {wrapper.features[0].properties.libelle} ({wrapper.features[0].properties.typezone})");
        onSuccess?.Invoke(wrapper.features[0].properties);
    }

    // ── Wrappers JsonUtility ──────────────────────────────────────────────────

    [Serializable]
    private class FeatureCollection
    {
        public Feature[] features;
    }

    [Serializable]
    private class Feature
    {
        public PLUZoneInfo properties;
    }
}