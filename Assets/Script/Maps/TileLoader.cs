using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;

public class TileLoader : MonoBehaviour
{
    [Header("Mapbox")]
    public string mapboxAccessToken = "VOTRE-TOKEN-MAPBOX-ICI";

    [Header("Style")]
    public MapboxStyle mapStyle = MapboxStyle.satellite_streets_v12;

    [Header("Paramètres carte")]
    [Range(10, 18)] public int zoomLevel = 17;
    public int imageWidth = 512;
    public int imageHeight = 512;

    [Header("UI")]
    public RawImage targetImage;

    public enum MapboxStyle
    {
        satellite_v9,           // Satellite pur
        satellite_streets_v12,  // Satellite + rues
        streets_v12,            // Carte classique
        light_v11,              // Thème clair
        dark_v11,               // Thème sombre
        outdoors_v12,           // Outdoor/randonnée
        navigation_day_v1       // Navigation jour
    }

    public IEnumerator LoadMapAt(double centerLat, double centerLon, double pinLat, double pinLon)
    {
        // Mapbox : longitude AVANT latitude, tirets dans le nom du style
        string styleName = mapStyle.ToString().Replace('_', '-');
        // Retire le dernier "-vXX" pour reconstruire correctement
        // ex: satellite_streets_v12 → satellite-streets-v12

        // Pin format Mapbox : pin-s+COULEUR(lon,lat)
        string pin = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "pin-l+ff0000({0},{1})",
            pinLon, pinLat
        );

        string url = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "https://api.mapbox.com/styles/v1/mapbox/{0}/static/{1}/{2},{3},{4}/{5}x{6}?access_token={7}",
            styleName,
            pin,
            centerLon, centerLat,  // ← Mapbox : lon avant lat
            zoomLevel,
            imageWidth, imageHeight,
            mapboxAccessToken
        );

        Debug.Log($"[Mapbox] URL → {url}");

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            req.SetRequestHeader("User-Agent", "CIPLApp/1.0");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Mapbox] Erreur {req.responseCode}: {req.error}");
                Debug.LogError($"[Mapbox] Body: {req.downloadHandler.text}");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null)
            {
                Debug.LogError("[Mapbox] Texture null !");
                yield break;
            }

            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            if (targetImage != null)
            {
                targetImage.texture = tex;
                targetImage.color = Color.white;
            }

            Debug.Log($"[Mapbox] Carte chargée : {tex.width}x{tex.height}");
        }
    }

    public void HidePin()
    {
        if (targetImage != null)
            targetImage.texture = null;
    }
}