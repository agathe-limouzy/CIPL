using UnityEngine;
using UnityEngine.UI;

/// Revue 17 (D10-01 / D3-N6 / B-18, lot F) : une fenêtre dont la carte grandit avec son contenu (VerticalLayoutGroup +
/// ContentSizeFitter) n'avait pas de hauteur maximale : au-delà de l'écran (« Choisir l'entreprise » avec onze SCI, inscription
/// après le code, paliers d'un bail de neuf ans, à 150-175 %), son pied et ses boutons sortaient de l'écran. Posé sur la carte,
/// ce composant met son corps (l'enfant « Corps » ou « Body ») dans une zone qui défile, plafonnée à la hauteur de l'écran
/// moins 40, en-tête et pied restant fixes. Une fenêtre qui tient garde exactement sa taille.
public class PlafondFenetre : MonoBehaviour
{
    const float Marge = 40f, Minimum = 120f;

    RectTransform _carte, _vue, _corps;
    LayoutElement _hauteur;
    ScrollRect _defil;
    float _posee = -1f;

    public static void Poser(RectTransform carte)
    {
        if (carte != null && carte.GetComponent<PlafondFenetre>() == null) carte.gameObject.AddComponent<PlafondFenetre>()._carte = carte;
    }

    /// BZ7 (22/11) : fenêtre de prefab dont le pied (ses boutons, chemin `pied` sous la carte) est rangé dans le corps : il en est
    /// sorti pour rester fixe sous la zone qui défile (« Ajouter un achat » en mode Emprunt dépassait le bas de l'écran).
    public static void Poser(RectTransform carte, string pied)
    {
        var p = carte != null ? carte.Find(pied) : null;
        if (p != null)
        {
            // Le fond crème (coins arrondis en bas) était porté par le corps : sorti du corps, le pied se dessinait hors cadre (vu le
            // 22/11). Il reprend ce fond, et le corps, qui défile au-dessus, devient un rectangle plein. Pas le fond de la carte
            // entière : ses coins, moins arrondis, dépassaient du bandeau du titre.
            var fondCorps = p.parent != null ? p.parent.GetComponent<Image>() : null;
            p.SetParent(carte, false);
            if (fondCorps != null && p.GetComponent<Graphic>() == null)
            {
                var fond = p.gameObject.AddComponent<Image>();
                fond.sprite = fondCorps.sprite; fond.type = fondCorps.type; fond.color = fondCorps.color;
                fond.pixelsPerUnitMultiplier = fondCorps.pixelsPerUnitMultiplier;
                fondCorps.sprite = null;
            }
        }
        Poser(carte);
    }

    /// La hauteur donnée au corps : la sienne, plafonnée à ce que l'écran laisse une fois le reste de la carte compté. Pure, testée.
    public static float Hauteur(float contenu, float ecran, float autres) => Mathf.Min(contenu, Mathf.Max(Minimum, ecran - Marge - autres));

    // La fenêtre est construite dans l'image où elle s'ouvre : à sa première LateUpdate (avant le dessin), son corps est là.
    void LateUpdate()
    {
        if (_carte == null) { enabled = false; return; }
        var voile = _carte.parent as RectTransform;
        if (voile == null) return;
        // B-15 : une carte plus étroite que son pied (trois boutons à la largeur de leur texte) s'élargit, dans l'écran.
        float largeur = Mathf.Min(LayoutUtility.GetMinWidth(_carte), voile.rect.width - Marge);
        if (largeur > _carte.rect.width + 0.5f) _carte.sizeDelta = new Vector2(largeur, _carte.sizeDelta.y);
        if (_vue == null && !Envelopper()) { enabled = false; return; }

        var groupe = _carte.GetComponent<VerticalLayoutGroup>();
        float autres = groupe != null ? groupe.padding.vertical : 0f;
        int enfants = 0;
        foreach (RectTransform c in _carte)
        {
            if (!c.gameObject.activeSelf) continue;
            var le = c.GetComponent<LayoutElement>();
            if (le != null && le.ignoreLayout) continue;
            enfants++;
            if (c != _vue) autres += LayoutUtility.GetPreferredHeight(c);
        }
        if (groupe != null) autres += groupe.spacing * Mathf.Max(0, enfants - 1);

        float contenu = LayoutUtility.GetPreferredHeight(_corps);
        float h = Hauteur(contenu, voile.rect.height, autres);
        bool defile = contenu > h + 0.5f;
        if (_defil.enabled != defile)
        {
            _defil.enabled = defile;
            if (!defile) _corps.anchoredPosition = Vector2.zero;
        }
        if (Mathf.Abs(h - _posee) < 0.5f) return;
        _posee = h;
        _hauteur.minHeight = _hauteur.preferredHeight = h;   // la carte (ContentSizeFitter) suit
    }

    bool Envelopper()
    {
        _corps = (_carte.Find("Corps") ?? _carte.Find("Body")) as RectTransform;
        if (_corps == null) return false;
        _vue = UIFactory.Rect("Defil", _carte);
        _vue.SetSiblingIndex(_corps.GetSiblingIndex());
        _vue.gameObject.AddComponent<RectMask2D>();
        _vue.gameObject.AddComponent<Image>().color = Color.clear;   // capte la molette dans les vides
        _hauteur = _vue.gameObject.AddComponent<LayoutElement>();
        _hauteur.flexibleHeight = 0f;

        _corps.SetParent(_vue, false);
        _corps.anchorMin = new Vector2(0f, 1f); _corps.anchorMax = new Vector2(1f, 1f); _corps.pivot = new Vector2(0.5f, 1f);
        _corps.offsetMin = new Vector2(0f, _corps.offsetMin.y); _corps.offsetMax = new Vector2(0f, _corps.offsetMax.y);
        _corps.anchoredPosition = Vector2.zero;
        var fit = _corps.GetComponent<ContentSizeFitter>() ?? _corps.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _defil = _vue.gameObject.AddComponent<ScrollRect>();
        _defil.viewport = _vue; _defil.content = _corps;
        _defil.horizontal = false; _defil.vertical = true;
        _defil.movementType = ScrollRect.MovementType.Clamped;
        _defil.scrollSensitivity = 30f;
        _defil.enabled = false;
        return true;
    }
}
