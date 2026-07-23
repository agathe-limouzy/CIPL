using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

/// Charge des vignettes de carte (Mapbox static) indépendamment des prefabs
/// bâtiment — pour le menu home, où les cartes des fiches ne sont pas actives.
/// Cache par adresse : une seule requête par adresse et par session.
public static class MapThumbnailService
{
    private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
    private static readonly HashSet<string> _enCours = new HashSet<string>();

    public static void Charge(MonoBehaviour runner, string adresse, Action<Texture2D> onReady)
    {
        if (string.IsNullOrWhiteSpace(adresse) || runner == null) return;

        if (_cache.TryGetValue(adresse, out var tex))
        {
            onReady?.Invoke(tex);
            return;
        }
        if (_enCours.Contains(adresse)) return;   // déjà en chargement

        runner.StartCoroutine(Fetch(adresse, onReady));
    }

    private static IEnumerator Fetch(string adresse, Action<Texture2D> onReady)
    {
        _enCours.Add(adresse);
        try
        {
            // 1 — Géocodage (enumerator pur, tourne sur le runner)
            double lat = 0, lon = 0;
            bool ok = false;
            var geo = GeoCodingService.Instance;
            if (geo == null) yield break;
            yield return geo.GeocodeAddress(adresse,
                (la, lo) => { lat = la; lon = lo; ok = true; },
                _ => { });
            if (!ok) yield break;

            // 2 — Token + style depuis n'importe quel TileLoader du projet
            string token = null;
            var style = TileLoader.MapboxStyle.satellite_streets_v12;
            foreach (var tl in Resources.FindObjectsOfTypeAll<TileLoader>())
                if (!string.IsNullOrEmpty(tl.mapboxAccessToken)
                    && tl.mapboxAccessToken != "VOTRE-TOKEN-MAPBOX-ICI")
                { token = tl.mapboxAccessToken; style = tl.mapStyle; break; }
            if (token == null) yield break;

            // 3 — Image statique Mapbox (petite : vignette)
            string styleName = style.ToString().Replace('_', '-');
            string url = string.Format(CultureInfo.InvariantCulture,
                "https://api.mapbox.com/styles/v1/mapbox/{0}/static/pin-s+ff0000({1},{2})/{1},{2},16/300x200?access_token={3}",
                styleName, lon, lat, token);

            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                req.SetRequestHeader("User-Agent", "CIPLApp/1.0");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;

                var tex = DownloadHandlerTexture.GetContent(req);
                if (tex == null) yield break;
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                _cache[adresse] = tex;
                onReady?.Invoke(tex);
            }
        }
        finally
        {
            _enCours.Remove(adresse);
        }
    }
}
