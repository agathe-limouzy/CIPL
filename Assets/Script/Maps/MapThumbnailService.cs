using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Charge des vignettes de carte indépendamment des prefabs bâtiment — pour le menu home, où les cartes
/// des fiches ne sont pas actives. Étape 16b : l'image vient de Google par la fonction serveur « cartes »
/// (TileLoader.Image), sur le cloud seulement. Cache par adresse : une seule requête par adresse et par
/// session (les conditions de Google ne permettent pas de garder l'image au-delà).
public static class MapThumbnailService
{
    private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
    private static readonly HashSet<string> _enCours = new HashSet<string>();

    // Zoom de la vignette, compté comme ceux des prefabs (Mapbox) : TileLoader.DecalageZoom de plus pour Google.
    const int ZoomVignette = 16;

    public static void Charge(MonoBehaviour runner, string adresse, Action<Texture2D> onReady)
    {
        if (string.IsNullOrWhiteSpace(adresse) || runner == null || !SessionCloud.Active) return;

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

            // 2 — Image satellite (petite : vignette), sans message : l'accueil reste lisible sans elle.
            var t = TileLoader.Image(lat, lon, ZoomVignette + TileLoader.DecalageZoom, 300, 200, lat, lon);
            yield return new WaitUntil(() => t.IsCompleted);
            var (tex, refus) = t.Result;
            if (tex == null) { Debug.LogWarning("[Cartes] Vignette : " + refus); yield break; }
            _cache[adresse] = tex;
            onReady?.Invoke(tex);
        }
        finally
        {
            _enCours.Remove(adresse);
        }
    }
}
