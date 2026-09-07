using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Visionneuse zoom + déplacement pour l'aperçu de facture :
/// molette = zoom (centré sur le curseur), glisser = déplacer quand on est zoomé.
/// Boutons −/+/Ajuster via ZoomBy()/Fit(). L'image (sheet) est centrée dans le
/// viewport masqué ; sa taille = taille « ajustée » × zoom.
[RequireComponent(typeof(RectTransform))]
public class FacturePreviewViewer : MonoBehaviour, IScrollHandler, IDragHandler
{
    RectTransform _viewport;   // zone masquée (fond gris)
    RectTransform _sheet;      // image A4 (RawImage)
    float _aspect;             // largeur / hauteur de l'image
    float _zoom = 1f;          // 1 = ajusté (page entière visible)
    Vector2 _pan;              // décalage courant (px locaux)

    public const float Min = 1f, Max = 6f;
    public float Zoom => _zoom;

    public void Init(RectTransform viewport, RectTransform sheet)
    {
        _viewport = viewport; _sheet = sheet;
    }

    // Nouvelle image : on repart en vue ajustée.
    public void SetAspect(float aspect)
    {
        _aspect = aspect > 0 ? aspect : 1f;
        _zoom = 1f; _pan = Vector2.zero;
        Apply();
    }

    public void Fit() { _zoom = 1f; _pan = Vector2.zero; Apply(); }

    // Zoom autour du centre (boutons +/−).
    public void ZoomBy(float factor)
    {
        _zoom = Mathf.Clamp(_zoom * factor, Min, Max);
        Apply();
    }

    // Molette : zoom centré sur le curseur.
    public void OnScroll(PointerEventData e)
    {
        if (_aspect <= 0 || _viewport == null) return;
        float old = _zoom;
        float step = 1f + Mathf.Clamp(e.scrollDelta.y, -3f, 3f) * 0.12f;
        _zoom = Mathf.Clamp(_zoom * step, Min, Max);
        if (Mathf.Approximately(_zoom, old)) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, e.position, e.enterEventCamera, out var lp))
            _pan = lp - (lp - _pan) * (_zoom / old);
        Apply();
    }

    // Glisser : déplacer l'image (uniquement quand on est zoomé).
    public void OnDrag(PointerEventData e)
    {
        if (_zoom <= Min + 0.001f) return;
        float sf = 1f;
        var cv = _viewport != null ? _viewport.GetComponentInParent<Canvas>() : null;
        if (cv != null && cv.scaleFactor > 0f) sf = cv.scaleFactor;
        _pan += e.delta / sf;
        Apply();
    }

    void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) Apply(); }

    void Apply()
    {
        if (_sheet == null || _viewport == null || _aspect <= 0f) return;
        Vector2 vp = _viewport.rect.size;
        if (vp.x <= 0f || vp.y <= 0f) return;

        // Taille « ajustée » (page entière visible) puis multipliée par le zoom.
        float fitW, fitH;
        if (vp.x / vp.y > _aspect) { fitH = vp.y; fitW = vp.y * _aspect; }
        else { fitW = vp.x; fitH = vp.x / _aspect; }
        float w = fitW * _zoom, h = fitH * _zoom;
        _sheet.sizeDelta = new Vector2(w, h);

        // Empêche de sortir complètement l'image du cadre.
        float maxX = Mathf.Max(0f, (w - vp.x) * 0.5f);
        float maxY = Mathf.Max(0f, (h - vp.y) * 0.5f);
        _pan.x = Mathf.Clamp(_pan.x, -maxX, maxX);
        _pan.y = Mathf.Clamp(_pan.y, -maxY, maxY);
        _sheet.anchoredPosition = _pan;
    }
}
