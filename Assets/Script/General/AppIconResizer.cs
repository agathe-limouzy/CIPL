using UnityEngine;
using UnityEngine.UI;

/// Uniformise la taille des icônes de section sur TOUTE l'app.
///
/// Les bandeaux de titre des sections (scène « titre », ou « TitleBand » créés au
/// code) contiennent une icône nommée « Icon ». Ce composant, posé sur le Canvas
/// racine, re-scanne périodiquement l'UI et force ces icônes à `iconSize` — ce qui
/// couvre aussi les écrans/panneaux instanciés dynamiquement (fiches, pop-ups…).
///
/// Idempotent : n'agit que sur les icônes pas encore à la bonne taille (pas de
/// reflow inutile).
public class AppIconResizer : MonoBehaviour
{
    [Tooltip("Taille cible des icônes de section (px).")]
    public float iconSize = 34f;

    [Tooltip("Intervalle de re-scan (secondes).")]
    public float interval = 0.5f;

    // Noms de bandeaux de titre reconnus.
    static readonly string[] BandNames = { "titre", "TitleBand", "Titre", "Header" };

    float _timer;

    // Revue 17 (D9-02, lot E) : comme AppSectionColors, le passage périodique ne visite que les objets affichés (31 ms toutes
    // les 0,5 s pour SCI (JBC), 0,34 s pour 300 locataires, mesuré le 16/11) ; les fiches sont traitées à leur construction.
    static AppIconResizer _instance;
    static readonly System.Collections.Generic.List<RectTransform> _tampon = new System.Collections.Generic.List<RectTransform>();

    void OnEnable() { _instance = this; Parcourir(transform, true); }

    void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer < interval) return;
        _timer = 0f;
        Parcourir(transform, false);
    }

    /// Met les icônes de tout ce qui est sous `racine` à la taille, objets cachés compris (fiche qui vient d'être construite).
    public static void Styler(Transform racine) { if (racine != null && _instance != null) _instance.Parcourir(racine, true); }

    void Parcourir(Transform racine, bool inactifs)
    {
        racine.GetComponentsInChildren(inactifs, _tampon);
        for (int i = 0; i < _tampon.Count; i++)
        {
            var rt = _tampon[i];
            if (rt.name != "Icon") continue;

            var parent = rt.parent;
            if (parent == null || !IsBand(parent.name)) continue;

            if (Mathf.Abs(rt.sizeDelta.x - iconSize) < 0.5f &&
                Mathf.Abs(rt.sizeDelta.y - iconSize) < 0.5f) continue;   // déjà à la bonne taille

            var le = rt.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = iconSize; le.preferredWidth = iconSize;
            le.minHeight = iconSize; le.preferredHeight = iconSize;
            rt.sizeDelta = new Vector2(iconSize, iconSize);
        }
        _tampon.Clear();
    }

    static bool IsBand(string n)
    {
        for (int i = 0; i < BandNames.Length; i++)
            if (n == BandNames[i]) return true;
        return false;
    }
}
