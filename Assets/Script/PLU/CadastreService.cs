using System;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Localise une parcelle cadastrale (commune + section + numéro) et renvoie
/// un point (lat / lon) au centre de la parcelle, prêt à alimenter le service PLU.
///
/// Enchaînement :
///   1. commune (nom ou code postal)  → code INSEE   (api-adresse, type municipality)
///   2. INSEE + section + numéro       → géométrie    (apicarto IGN cadastre/parcelle)
///   3. géométrie                      → centroïde    (moyenne des sommets)
/// </summary>
public class CadastreService : MonoBehaviour
{
    private static CadastreService _instance;

    /// Point d'accès global — comme GeoCodingService, on ne détruit pas les doublons.
    public static CadastreService Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<CadastreService>();
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // API adresse gouv — résolution de commune (renvoie le code INSEE dans citycode)
    private const string COMMUNE_URL = "https://api-adresse.data.gouv.fr/search/";

    // API Carto IGN — module cadastre
    private const string CADASTRE_URL = "https://apicarto.ign.fr/api/cadastre/parcelle";

    // ── API publique ────────────────────────────────────────────────────────────

    /// <summary>
    /// Localise une parcelle et renvoie son centre (lat, lon).
    /// Enumerator pur : tourne sur le MonoBehaviour appelant (comme GeoCodingService).
    /// </summary>
    public IEnumerator LocateParcelle(string communeQuery, string section, string numero,
        Action<double, double> onSuccess, Action<string> onError)
    {
        // 1 — Commune → INSEE
        string insee = null;
        yield return ResolveCommuneInsee(communeQuery,
            code => insee = code,
            _ => { /* silencieux : on gère l'échec juste après */ });

        if (string.IsNullOrEmpty(insee))
        {
            onError?.Invoke($"Commune introuvable : « {communeQuery} ».");
            yield break;
        }

        // 2 — Normalisation (conventions PCI : section sur 2 car., numéro sur 4 chiffres)
        string sec = NormalizeSection(section);
        string num = NormalizeNumero(numero);

        if (sec.Length == 0 || num.Length == 0)
        {
            onError?.Invoke("Section et numéro de parcelle requis.");
            yield break;
        }

        // 3 — Requête cadastre
        string url = $"{CADASTRE_URL}?code_insee={insee}" +
                     $"&section={UnityWebRequest.EscapeURL(sec)}" +
                     $"&numero={UnityWebRequest.EscapeURL(num)}";
        Debug.Log($"[Cadastre] Requête → {url}");

        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Accept", "application/json");
            req.timeout = 12;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Erreur réseau cadastre ({req.responseCode}). Réessayez.");
                yield break;
            }

            if (!TryComputeCentroid(req.downloadHandler.text, out double lat, out double lon))
            {
                onError?.Invoke($"Parcelle introuvable : section {sec}, n° {num} à « {communeQuery} ».\n" +
                                "Vérifiez la section et le numéro.");
                yield break;
            }

            Debug.Log($"[Cadastre] Parcelle localisée → lat={lat}, lon={lon}");
            onSuccess?.Invoke(lat, lon);
        }
    }

    // ── Commune → code INSEE ──────────────────────────────────────────────────────

    private IEnumerator ResolveCommuneInsee(string query, Action<string> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(query)) { onError?.Invoke("vide"); yield break; }

        string url = $"{COMMUNE_URL}?q={UnityWebRequest.EscapeURL(query.Trim())}&type=municipality&limit=1";
        Debug.Log($"[Cadastre] Commune → {url}");

        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 8;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(req.error);
                yield break;
            }

            CommuneResponse resp;
            try { resp = JsonUtility.FromJson<CommuneResponse>(req.downloadHandler.text); }
            catch { onError?.Invoke("parse"); yield break; }

            if (resp?.features == null || resp.features.Length == 0 ||
                resp.features[0].properties == null ||
                string.IsNullOrEmpty(resp.features[0].properties.citycode))
            {
                onError?.Invoke("introuvable");
                yield break;
            }

            onSuccess?.Invoke(resp.features[0].properties.citycode);
        }
    }

    /// <summary>
    /// Nom de la commune à partir d'une adresse complète (api-adresse →
    /// properties.city). Sert à pré-remplir la commune en mode cadastre quand on
    /// ouvre le PLU depuis un bâtiment (l'adresse ne contient pas forcément la ville).
    /// </summary>
    public IEnumerator ResolveCityFromAddress(string address, Action<string> onCity)
    {
        if (string.IsNullOrWhiteSpace(address)) yield break;

        string url = $"{COMMUNE_URL}?q={UnityWebRequest.EscapeURL(address.Trim())}&limit=1";
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 8;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            CommuneResponse resp;
            try { resp = JsonUtility.FromJson<CommuneResponse>(req.downloadHandler.text); }
            catch { yield break; }

            if (resp?.features != null && resp.features.Length > 0 &&
                resp.features[0].properties != null &&
                !string.IsNullOrEmpty(resp.features[0].properties.city))
                onCity?.Invoke(resp.features[0].properties.city);
        }
    }

    // ── Centroïde depuis le GeoJSON ───────────────────────────────────────────────
    // JsonUtility ne sait pas désérialiser les tableaux de coordonnées imbriqués
    // (MultiPolygon = number[][][][]). On extrait le bloc "coordinates" de la
    // première feature par comptage de crochets, puis on moyenne tous les sommets.
    private bool TryComputeCentroid(string json, out double lat, out double lon)
    {
        lat = 0; lon = 0;
        if (string.IsNullOrEmpty(json)) return false;

        int ci = json.IndexOf("\"coordinates\"", StringComparison.Ordinal);
        if (ci < 0) return false;

        int start = json.IndexOf('[', ci);
        if (start < 0) return false;

        int depth = 0, end = -1;
        for (int i = start; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']')
            {
                depth--;
                if (depth == 0) { end = i; break; }
            }
        }
        if (end < 0) return false;

        string block = json.Substring(start, end - start + 1);
        var matches = Regex.Matches(block, @"-?\d+\.\d+");
        if (matches.Count < 2) return false;

        double sumLon = 0, sumLat = 0;
        int n = 0;
        for (int i = 0; i + 1 < matches.Count; i += 2)
        {
            double a = double.Parse(matches[i].Value, CultureInfo.InvariantCulture);
            double b = double.Parse(matches[i + 1].Value, CultureInfo.InvariantCulture);
            sumLon += a;
            sumLat += b;
            n++;
        }
        if (n == 0) return false;

        lon = sumLon / n;
        lat = sumLat / n;
        return true;
    }

    // ── Normalisation ─────────────────────────────────────────────────────────────

    private string NormalizeSection(string s)
    {
        s = (s ?? "").Trim().ToUpperInvariant();
        s = Regex.Replace(s, @"[^0-9A-Z]", "");
        if (s.Length == 1) s = "0" + s;   // section toujours sur 2 caractères (ex. "A" → "0A")
        return s;
    }

    private string NormalizeNumero(string s)
    {
        s = Regex.Replace(s ?? "", @"\D", "");   // ne garde que les chiffres
        if (s.Length == 0) return "";
        return s.PadLeft(4, '0');                 // numéro sur 4 chiffres (ex. "142" → "0142")
    }

    // ── Modèles JSON ──────────────────────────────────────────────────────────────

    [Serializable] private class CommuneResponse { public CommuneFeature[] features; }
    [Serializable] private class CommuneFeature { public CommuneProps properties; }
    [Serializable] private class CommuneProps { public string citycode; public string city; }
}
