using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;

public class TileLoader : MonoBehaviour
{
    [Header("Mapbox")]
    // ⚠ Ce champ est sérialisé dans le prefab, donc COMMITÉ dans le dépôt : n'y mettez
    // jamais un vrai token. Le token réel se saisit dans Réglages et vit dans
    // <SaveRoot>/pennylane_secrets.dat, hors repo et hors backup.
    public string mapboxAccessToken = "VOTRE-TOKEN-MAPBOX-ICI";

    public const string TOKEN_PLACEHOLDER = "VOTRE-TOKEN-MAPBOX-ICI";

    /// Token à utiliser : celui des Réglages (hors repo) en priorité, sinon le champ
    /// de l'inspecteur — conservé uniquement comme dépannage local.
    public string TokenEffectif
    {
        get
        {
            string secret = ReglageService.GetMapboxToken();
            if (!string.IsNullOrWhiteSpace(secret)) return secret;
            return mapboxAccessToken == TOKEN_PLACEHOLDER ? "" : mapboxAccessToken;
        }
    }

    /// Vrai si un token exploitable est disponible.
    public bool ATokenValide => !string.IsNullOrWhiteSpace(TokenEffectif);

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

        // Sans token, la requête partirait avec « access_token= » vide et Mapbox
        // répondrait un 401 « Not Authorized » incompréhensible. On sort avant, avec
        // un message qui dit quoi faire.
        if (!ATokenValide)
        {
            Debug.LogError("[Mapbox] Aucun token configuré — carte non chargée. " +
                           "Saisissez-le dans Réglages → Intégrations → Cartes (Mapbox).");
            UndoToast.Instance?.ShowInfo("Carte indisponible : renseignez le token Mapbox dans Réglages → Intégrations.");
            yield break;
        }

        string url = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "https://api.mapbox.com/styles/v1/mapbox/{0}/static/{1}/{2},{3},{4}/{5}x{6}?access_token={7}",
            styleName,
            pin,
            centerLon, centerLat,  // ← Mapbox : lon avant lat
            zoomLevel,
            imageWidth, imageHeight,
            TokenEffectif
        );

        // On ne journalise JAMAIS l'URL complète : elle contient le token d'accès.
        Debug.Log($"[Mapbox] Requête style={styleName} zoom={zoomLevel} {imageWidth}x{imageHeight}");

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            req.SetRequestHeader("User-Agent", "CIPLApp/1.0");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Mapbox] Erreur {req.responseCode}: {req.error}");
                Debug.LogError($"[Mapbox] Body: {req.downloadHandler.text}");
                string clair = Clair(req.responseCode);
                if (clair != null) UndoToast.Instance?.ShowInfo("Carte indisponible. " + clair);
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

    /// Étape 16 : la cause en clair d'un refus de Mapbox, ou null.
    public static string Clair(long code) =>
          code == 0 ? "Mapbox injoignable : vérifiez la connexion internet."
        : code == 401 ? "Jeton refusé par Mapbox : recopiez-le en entier (il commence par pk.) depuis account.mapbox.com → Tokens."
        : code == 403 ? "Jeton reconnu mais interdit ici : retirez sa restriction d'adresse (URL) sur account.mapbox.com, ou prenez le « Default public token »."
        : code == 429 ? "Trop de cartes demandées à Mapbox : réessayez dans une minute."
        : code >= 500 ? "Mapbox est indisponible pour le moment : réessayez plus tard."
        : null;

    /// Étape 16, bouton « Tester le jeton » : une carte de 1 × 1 pixel, demandée comme les vraies.
    public static IEnumerator Tester(string jeton, System.Action<bool, string> fin)
    {
        if (string.IsNullOrWhiteSpace(jeton)) { fin(false, "Collez d'abord le token Mapbox."); yield break; }
        using var req = UnityWebRequest.Get("https://api.mapbox.com/styles/v1/mapbox/streets-v12/static/2.35,48.85,10/1x1?access_token="
                                            + UnityWebRequest.EscapeURL(jeton.Trim()));
        req.SetRequestHeader("User-Agent", "CIPLApp/1.0");
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success) fin(true, "Jeton valide : Mapbox a renvoyé une carte.");
        else fin(false, (Clair(req.responseCode) ?? "Mapbox a refusé la carte.") + $"\nDétail technique : HTTP {req.responseCode} {req.error}");
    }

}