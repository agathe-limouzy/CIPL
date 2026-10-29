using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// La carte satellite d'une fiche bâtiment. Étape 16b (décisions des 24 et 28/10) : Google Maps (Maps Static
/// API), demandée par la fonction serveur « cartes », qui garde la clé de CIPL ; le token Mapbox de chaque PC
/// a disparu. Sur le cloud seulement. Les images ne sont gardées que le temps de la session (conditions de
/// Google) : MapController ne recharge pas une adresse déjà affichée, MapThumbnailService a son cache.
public class TileLoader : MonoBehaviour
{
    /// Les zooms des prefabs datent de Mapbox (tuiles de 512 px) ; Google compte en tuiles de 256 px :
    /// un cran de plus pour le même cadrage.
    public const int DecalageZoom = 1;

    [Header("Paramètres carte")]
    [Range(10, 18)] public int zoomLevel = 17;
    public int imageWidth = 512;
    public int imageHeight = 512;

    [Header("UI")]
    public RawImage targetImage;

    public IEnumerator LoadMapAt(double centerLat, double centerLon, double pinLat, double pinLon)
    {
        if (!SessionCloud.Active) { Debug.LogWarning("[Cartes] Hors cloud : pas de carte (la clé Google est sur le serveur)."); yield break; }

        var t = Image(centerLat, centerLon, zoomLevel + DecalageZoom, imageWidth, imageHeight, pinLat, pinLon);
        yield return new WaitUntil(() => t.IsCompleted);
        var (tex, refus) = t.Result;
        if (tex == null)
        {
            Debug.LogWarning("[Cartes] " + refus);
            UndoToast.Instance?.ShowInfo("Carte indisponible. " + refus.Split('\n')[0]);
            yield break;
        }
        if (targetImage != null)
        {
            targetImage.texture = tex;
            targetImage.color = Color.white;
        }
    }

    /// Le corps envoyé à la fonction « cartes » (mêmes noms que dans Serveur/fonctions/cartes.ts).
    [Serializable] public class DemandeCarte
    {
        public string entreprise;
        public double lat, lon, epingle_lat, epingle_lon;
        public int zoom, largeur, hauteur;
    }

    [Serializable] class RefusCarte { public string refus, erreur; public long code; }

    /// L'image demandée au serveur, ou le motif en clair (suivi du détail technique).
    public static async Task<(Texture2D tex, string refus)> Image(double lat, double lon, int zoom, int largeur, int hauteur,
                                                                  double epingleLat, double epingleLon)
    {
        var d = new DemandeCarte { entreprise = SessionCloud.Entreprise?.id, lat = lat, lon = lon, zoom = zoom,
                                   largeur = largeur, hauteur = hauteur, epingle_lat = epingleLat, epingle_lon = epingleLon };
        var (rep, octets) = await CloudClient.FonctionBrute(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(),
                                                            "cartes", JsonUtility.ToJson(d));
        if (rep.Ok && octets != null)
        {
            var tex = new Texture2D(2, 2) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (tex.LoadImage(octets)) return (tex, null);
            UnityEngine.Object.Destroy(tex);
        }
        // Un refus arrive en JSON : avec un statut 200 (clé absente) ou une erreur (accès, Google).
        string corps = rep.Ok ? (octets == null ? "" : Encoding.UTF8.GetString(octets)) : rep.Corps;
        RefusCarte r = null;
        try { r = JsonUtility.FromJson<RefusCarte>(corps); } catch (Exception) { /* pas du JSON */ }
        return (null, Motif(rep.Code, r?.refus, r?.code ?? 0, r?.erreur ?? rep.Erreur));
    }

    /// Étapes 16 et 16b : la cause en clair d'une carte refusée, puis le détail technique.
    public static string Motif(long code, string refus, long codeGoogle, string erreur)
    {
        string e = erreur ?? "";
        string clair =
              code == 0 ? "Serveur de CIPL injoignable : vérifiez la connexion internet."
            : refus == "config" ? "Les cartes ne sont pas encore réglées sur le serveur (Supabase › Edge Functions › Secrets : GOOGLE_MAPS_KEY)."
            : refus == "acces" ? "Cette entreprise ne vous est pas ouverte."
            : refus == "google" && (codeGoogle == 429 || e.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0)
                ? "Limite de cartes du jour atteinte : réessayez demain."
            : refus == "google" && codeGoogle == 403 ? "Google refuse la carte : la clé de CIPL est refusée, ou l'API Maps Static n'est pas activée."
            : refus == "google" && codeGoogle >= 500 ? "Google Maps est indisponible pour le moment : réessayez plus tard."
            : code == 404 ? "La fonction « cartes » n'est pas déployée sur le serveur."
            : "Le serveur n'a pas pu fournir la carte.";
        string detail = $"HTTP {code}" + (string.IsNullOrEmpty(refus) ? "" : " · " + refus) + (codeGoogle > 0 ? $" {codeGoogle}" : "")
                      + (e == "" ? "" : " " + (e.Length > 200 ? e.Substring(0, 200) + "…" : e));
        return clair + "\nDétail technique : " + detail;
    }
}
