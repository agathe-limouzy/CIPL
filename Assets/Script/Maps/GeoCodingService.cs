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

    /// Revue 17 (D7b-05, lot D) : ce que dit la réponse d'api-adresse.
    public enum Verdict { Accepte, Imprecise, Introuvable, Injoignable }

    /// Revue 17 (D7b-05, lot D) : tout refus d'api-adresse passait pour une panne et partait sur Nominatim, qui posait
    /// la carte et le PLU sur une rue homonyme d'ailleurs : « Adresse imprécise » ne s'affichait jamais. Seul un
    /// service injoignable (ou une réponse illisible) mène au repli ; une adresse imprécise ou introuvable arrête.
    public static Verdict Decision(bool joint, bool coordonnees, float score)
        => !joint ? Verdict.Injoignable
         : !coordonnees ? Verdict.Introuvable
         : score > 0f && score < 0.5f ? Verdict.Imprecise
         : Verdict.Accepte;

    /// Retour de BW17 (14/11) : l'adresse tapée porte un code postal (5 chiffres) ou le nom de la ville trouvée. Sans eux,
    /// api-adresse répond quand même avec 0,98 de confiance (« 12 rue de la Paix » → Cherbourg) : la carte et le PLU seraient
    /// posés dans une autre ville sans rien dire. Accents, majuscules et tirets ne comptent pas. Pur : testé (LotDCartesTests).
    public static bool VillePresente(string saisie, string ville)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(saisie ?? "", @"(?<!\d)\d{5}(?!\d)")) return true;
        string v = Simple(ville);
        return v.Length > 0 && (" " + Simple(saisie) + " ").Contains(" " + v + " ");
    }

    static string Simple(string t)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in (t ?? "").Normalize(System.Text.NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    /// Retour de BW17 (14/11) : jusqu'à 5 adresses pour le début tapé (api-adresse, autocomplete=1), pour la liste sous le
    /// champ (AdresseAutocomplete). Liste vide si le service ne répond pas : la saisie continue, Entrée cherche le texte tapé.
    /// `pres` (retour de BW17) : la zone à privilégier (lat, lon) ; sans elle, l'annuaire proposait « 12 La Rue » de communes au
    /// hasard pour « 12 rue de la ».
    /// `villeRepli` (décision du 14/11) : sans zone connue (premier bâtiment de l'entreprise), la ville du lieu d'émission ; sa
    /// position est demandée une fois par session et par ville (type=municipality).
    public static IEnumerator Suggestions(string debut, (double lat, double lon)? pres, string villeRepli, Action<System.Collections.Generic.List<string>> ok)
    {
        var liste = new System.Collections.Generic.List<string>();
        string ville = VilleDeRepli(villeRepli);
        if (!pres.HasValue && ville != null)
        {
            if (!_villes.ContainsKey(ville))
            {
                _villes[ville] = null;   // une seule demande, même si elle échoue
                using (var rv = UnityWebRequest.Get($"{ADRESSE_GOV_URL}?q={UnityWebRequest.EscapeURL(ville)}&type=municipality&limit=1"))
                {
                    rv.timeout = 6;
                    yield return rv.SendWebRequest();
                    GeoJsonResponse repv = null;
                    if (rv.result == UnityWebRequest.Result.Success)
                        try { repv = JsonUtility.FromJson<GeoJsonResponse>(rv.downloadHandler.text); } catch (Exception) { }
                    var c = repv?.features != null && repv.features.Length > 0 ? repv.features[0]?.geometry?.coordinates : null;
                    if (c != null && c.Length >= 2) _villes[ville] = (c[1], c[0]);
                }
            }
            pres = _villes[ville];
        }
        using (var r = UnityWebRequest.Get(UrlSuggestions(debut, pres)))
        {
            r.timeout = 6;
            yield return r.SendWebRequest();
            if (r.result == UnityWebRequest.Result.Success)
            {
                GeoJsonResponse rep = null;
                try { rep = JsonUtility.FromJson<GeoJsonResponse>(r.downloadHandler.text); } catch (Exception) { }
                if (rep?.features != null)
                    foreach (var f in rep.features)
                        if (!string.IsNullOrEmpty(f?.properties?.label)) liste.Add(f.properties.label);
            }
        }
        ok?.Invoke(liste);
    }

    /// L'adresse de la liste de suggestions, avec la zone à privilégier si elle est connue. Pur : testé (LotDCartesTests).
    static readonly System.Collections.Generic.Dictionary<string, (double lat, double lon)?> _villes =
        new System.Collections.Generic.Dictionary<string, (double lat, double lon)?>();

    /// La ville de repli lisible dans le lieu d'émission, ou null : vide, ou lieu d'usine (ville de la société familiale, jamais
    /// celle du client). Pur : testé (LotDCartesTests).
    public static string VilleDeRepli(string lieuEmission)
    {
        string v = (lieuEmission ?? "").Trim();
        return v.Length == 0 || string.Equals(v, FacturePdfService.LieuDefaut, StringComparison.OrdinalIgnoreCase) ? null : v;
    }

    public static string UrlSuggestions(string debut, (double lat, double lon)? pres)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string url = $"{ADRESSE_GOV_URL}?q={UnityWebRequest.EscapeURL(debut ?? "")}&limit=5&autocomplete=1";
        return pres.HasValue ? url + "&lat=" + pres.Value.lat.ToString("0.######", inv) + "&lon=" + pres.Value.lon.ToString("0.######", inv) : url;
    }

    public static string Message(Verdict v)
        => v == Verdict.Imprecise ? "Adresse imprécise : précisez la ville ou le code postal."
         : v == Verdict.Introuvable ? "Adresse introuvable : vérifiez le format (ex : 12 rue de la Paix, Paris)."
         : v == Verdict.Injoignable ? "Service d'adresses injoignable : vérifiez la connexion, puis réessayez."
         : null;

    public IEnumerator GeocodeAddress(string address, Action<double, double> onSuccess, Action<string> onError)
    {
        // Tente d'abord l'API gouvernementale française
        Verdict v = Verdict.Injoignable;
        yield return TryAdresseGouv(address, onSuccess, r => v = r);

        if (v == Verdict.Accepte) yield break;
        if (v != Verdict.Injoignable) { onError?.Invoke(Message(v)); yield break; }

        // Repli : Nominatim, seulement si api-adresse est injoignable
        Debug.Log("[GeoCoding] API gouv injoignable, repli Nominatim...");
        yield return TryNominatim(address, onSuccess, onError);
    }

    // ── API adresse.data.gouv.fr ──────────────────────────────────────────────

    private IEnumerator TryAdresseGouv(string address, Action<double, double> onSuccess, Action<Verdict> onVerdict)
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
                Debug.LogWarning($"[GeoCoding] API Gouv injoignable : {request.error}");
                onVerdict?.Invoke(Verdict.Injoignable);
                yield break;
            }

            // Parse GeoJSON { features: [ { geometry: { coordinates: [lon, lat] }, properties: { label } } ] }
            // Revue 16c (D7b-14) : une réponse inattendue levait dans la coroutine (bouton de recherche figé) ; un résultat trop
            // incertain (score < 0,5 : ville absente…) posait la carte ailleurs sans prévenir.
            GeoJsonResponse response = null;
            try { response = JsonUtility.FromJson<GeoJsonResponse>(request.downloadHandler.text); } catch (Exception) { }
            var feature = response?.features != null && response.features.Length > 0 ? response.features[0] : null;
            var coords = feature?.geometry?.coordinates;
            var verdict = Decision(response != null, coords != null && coords.Length >= 2, feature?.properties?.score ?? 0f);
            // Retour de BW17 (14/11) : sans ville ni code postal, api-adresse choisit une ville au hasard avec une forte confiance.
            if (verdict == Verdict.Accepte && !VillePresente(address, feature.properties?.city)) verdict = Verdict.Imprecise;
            onVerdict?.Invoke(verdict);
            if (verdict != Verdict.Accepte) yield break;
            double lon = coords[0];
            double lat = coords[1];

            Debug.Log($"[GeoCoding] Gouv → lat={lat}, lon={lon} ({feature.properties?.label})");
            onSuccess?.Invoke(lat, lon);
        }
    }

    // ── Nominatim fallback ────────────────────────────────────────────────────

    private IEnumerator TryNominatim(string address, Action<double, double> onSuccess, Action<string> onError)
    {
        string encodedAddress = UnityWebRequest.EscapeURL(address);
        // Revue 17 (D7b-05, lot D) : en France seulement (le premier résultat pouvait être à l'étranger).
        string url = $"{NOMINATIM_URL}?q={encodedAddress}&format=json&limit=1&countrycodes=fr";

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

            // Revue 17 (D7b-05, lot D) : une réponse inattendue levait dans la coroutine (recherche figée).
            string wrappedJson = "{\"results\":" + request.downloadHandler.text + "}";
            NominatimResponse response = null;
            try { response = JsonUtility.FromJson<NominatimResponse>(wrappedJson); } catch (Exception) { }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var flottant = System.Globalization.NumberStyles.Float;
            double lat = 0, lon = 0;
            if (response?.results == null || response.results.Length == 0
                || !double.TryParse(response.results[0].lat, flottant, inv, out lat)
                || !double.TryParse(response.results[0].lon, flottant, inv, out lon))
            {
                onError?.Invoke("Adresse introuvable. Vérifiez le format (ex.\u00A0: 12 rue de la Paix, Paris)");
                yield break;
            }

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
        public float score;   // 0 à 1 : confiance d'api-adresse (revue 16c, D7b-14)
        public string city, postcode;   // retour de BW17 (14/11) : la ville trouvée, comparée à l'adresse tapée
    }
}