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

    // Revue 17 (D10-03, lot F ; décision du 16/11) : tant que la taille n'a jamais été réglée, elle suit l'écran (largeur et mise à
    // l'échelle de Windows) au lieu de 100 % partout (sur un portable de 1366 px, les pastilles faisaient 8,5 px). Calculée une
    // fois par session ; dès qu'un palier est choisi dans les Réglages, il l'emporte.
    static float? _parDefaut;

    /// Le palier le plus proche de ce que l'écran demande : 1920 px à 96 ppp → 100 % ; 1920 px à 144 ppp (Windows à 150 %) → 150 % ;
    /// 1366 px → 150 % ; 2560 px → 90 %. Pure, testée.
    public static float PalierPourEcran(float ppp, int largeur)
    {
        if (largeur <= 0) return 1f;
        float voulu = (ppp > 0f ? ppp : 96f) / 96f * 1920f / largeur;
        float meilleur = Paliers[0];
        foreach (float p in Paliers)
            if (Mathf.Abs(p - voulu) < Mathf.Abs(meilleur - voulu)) meilleur = p;
        return Borner(meilleur);
    }

    // Décision du 22/11 : dans les Réglages, un clic sur une taille la montre en aperçu, sans l'enregistrer ; « Enregistrer » la garde
    // (Garder), fermer sans enregistrer rend l'ancienne (FinApercu). Avant : le clic l'enregistrait, à côté du bouton « Enregistrer ».
    static float? _apercu;

    public static float Facteur
    {
        get => _apercu ?? (PlayerPrefs.HasKey(Cle) ? Borner(PlayerPrefs.GetFloat(Cle, 1f))
                                                   : (_parDefaut ?? (_parDefaut = PalierPourEcran(Screen.dpi, Screen.width))).Value);
        set
        {
            _apercu = null;
            PlayerPrefs.SetFloat(Cle, Borner(value));
            PlayerPrefs.Save();
            Appliquer();
        }
    }

    public static void Apercu(float facteur) { _apercu = Borner(facteur); Appliquer(); }
    public static void Garder() { if (_apercu.HasValue) Facteur = _apercu.Value; }
    public static void FinApercu() { if (!_apercu.HasValue) return; _apercu = null; Appliquer(); }

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
