using UnityEngine;
using UnityEngine.UI;

/// Taille de l'interface : un seul réglage qui agrandit ou réduit tout
/// proportionnellement — texte, champs, marges, boutons.
///
/// Le `CanvasScaler` du projet est en `ScaleWithScreenSize`, mode dans lequel
/// `scaleFactor` est purement et simplement ignoré. Le levier est la résolution
/// de référence : la diviser par 1,25 revient à dessiner l'interface comme si
/// l'écran était plus petit, donc à tout grossir de 25 %.
///
/// Pourquoi pas un réglage de police seule : la mesure du 23/09 a montré que
/// 140 hauteurs sont écrites en dur dans des `LayoutElement`. Grossir le texte
/// sans grossir les champs les ferait déborder. Le zoom global n'a pas ce défaut
/// puisque tout change ensemble.
public static class UIZoom
{
    const string Cle = "CIPL_ZoomUI";

    public const float Min = 0.8f;
    // À 175 %, un écran de 1920 px n'offre plus que 1097 unités de large : c'est la
    // largeur que chaque écran doit tenir sans déborder.
    public const float Max = 1.75f;

    /// Les paliers proposés dans le réglage.
    public static readonly float[] Paliers = { 0.9f, 1f, 1.1f, 1.25f, 1.5f, 1.75f };

    // Résolution de référence d'origine, lue une seule fois. La relire à chaque
    // application la ferait dériver, puisqu'on écrit dessus.
    static Vector2 _origine;
    static bool _origineLue;

    public static float Facteur
    {
        get => Borner(PlayerPrefs.GetFloat(Cle, 1f));
        set
        {
            PlayerPrefs.SetFloat(Cle, Borner(value));
            PlayerPrefs.Save();
            Appliquer();
        }
    }

    public static float Borner(float f)
    {
        if (float.IsNaN(f) || f <= 0f) return 1f;
        return Mathf.Clamp(f, Min, Max);
    }

    /// Résolution à poser sur le scaler pour un facteur donné.
    public static Vector2 Reference(Vector2 origine, float facteur)
        => origine / Borner(facteur);

    /// Applique le facteur enregistré à tous les CanvasScaler de la scène.
    public static void Appliquer()
    {
        var scalers = Object.FindObjectsByType<CanvasScaler>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (scalers.Length == 0) return;

        if (!_origineLue)
        {
            _origine = scalers[0].referenceResolution;
            _origineLue = true;
        }

        foreach (var cs in scalers)
        {
            if (cs.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            cs.referenceResolution = Reference(_origine, Facteur);
        }
    }
}
