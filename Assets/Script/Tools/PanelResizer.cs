using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PanelResizer : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("Panneaux")]
    public RectTransform panelTop;
    public RectTransform panelBottom;

    [Header("Contenu scroll du bas (assigner ContentLocataire)")]
    [SerializeField] private RectTransform bottomScrollContent;

    [Header("Limites (px)")]
    public float minTop = 150f;
    public float minBottom = 150f;

    private RectTransform _parentRt;
    private VerticalLayoutGroup _parentVLG;
    private ContentSizeFitter _csfTop;
    private ContentSizeFitter _csfBottom;
    private ContentSizeFitter _csfScrollContent;

    private float _topHeight;
    private float _bottomHeight;
    private float _bottomScrollContentOtherH; // hauteur fixe = titre + tab bar

    // Enfants directs de panelTop à étirer
    private readonly List<RectTransform> _topDirectChildren = new List<RectTransform>();


    [Header("Curseur")]
    public Texture2D resizeCursor;
    public Vector2 cursorHotspot = new Vector2(16, 16);

    private bool _hovering;
    private bool _dragging;

    public void OnBeginDrag(PointerEventData eventData) { _dragging = true; UpdateCursor(); }
    public void OnEndDrag(PointerEventData eventData) { _dragging = false; UpdateCursor(); }
    public void OnPointerEnter(PointerEventData e) { _hovering = true; UpdateCursor(); }
    public void OnPointerExit(PointerEventData e) { _hovering = false; UpdateCursor(); }

    private void UpdateCursor()
    {
        if (_hovering || _dragging)
            Cursor.SetCursor(resizeCursor, cursorHotspot, CursorMode.Auto);
        else
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void OnDisable()
    {
        if (_hovering || _dragging)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        _hovering = _dragging = false;
    }
    private void Awake()
    {
        var parent = transform.parent;
        _parentRt = parent.GetComponent<RectTransform>();
        _parentVLG = parent.GetComponent<VerticalLayoutGroup>();
        _csfTop = panelTop.GetComponent<ContentSizeFitter>();
        _csfBottom = panelBottom.GetComponent<ContentSizeFitter>();

        if (bottomScrollContent != null)
            _csfScrollContent = bottomScrollContent.GetComponent<ContentSizeFitter>();
    }
 
    private bool _initialized = false;

    private void LateUpdate()
    {
        if (_initialized) return;
        if (_parentRt.rect.height <= 1f) return; // attend que le parent ait une taille valide

        _initialized = true;
        InitLayout();
    }

    private void InitLayout()
    {
        if (_csfTop != null) _csfTop.enabled = false;
        if (_csfBottom != null) _csfBottom.enabled = false;
        if (_csfScrollContent != null) _csfScrollContent.enabled = false;
        if (_parentVLG != null) _parentVLG.childControlHeight = false;

        Canvas.ForceUpdateCanvases();

        float resizerH = GetComponent<RectTransform>().rect.height;
        float containerH = _parentRt.rect.height;

        _topHeight = Mathf.Max(minTop, panelTop.rect.height);
        _bottomHeight = Mathf.Max(minBottom, containerH - resizerH - _topHeight);

        ApplySizes();
    }


  

    private void ApplySizes()
    {
        panelTop.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _topHeight);
        panelBottom.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _bottomHeight);

        // Enfants directs de panelTop
        _topDirectChildren.Clear();
        foreach (Transform child in panelTop)
        {
            var rt = child as RectTransform;
            if (rt == null) continue;
            var csf = child.GetComponent<ContentSizeFitter>();
            if (csf != null) csf.enabled = false;
            _topDirectChildren.Add(rt);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _topHeight);
        }

        // ContentLocataire
        if (bottomScrollContent != null)
        {
            if (_csfScrollContent != null) _csfScrollContent.enabled = false;

            Canvas.ForceUpdateCanvases();

            float fixedH = 0f;
            foreach (Transform child in panelBottom)
            {
                var rt = child as RectTransform;
                if (rt == null || rt == bottomScrollContent) continue;
                fixedH += rt.rect.height;
            }
            _bottomScrollContentOtherH = fixedH;

            bottomScrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(0f, _bottomHeight - fixedH));
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelBottom);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_parentRt);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_csfTop != null) _csfTop.enabled = false;
        if (_csfBottom != null) _csfBottom.enabled = false;
        if (_csfScrollContent != null) _csfScrollContent.enabled = false;
        if (_parentVLG != null) _parentVLG.childControlHeight = false;

        Canvas.ForceUpdateCanvases();

        float resizerH = GetComponent<RectTransform>().rect.height;
        _topHeight = panelTop.rect.height;
        _bottomHeight = _parentRt.rect.height - resizerH - _topHeight; // remplit exactement

        // Applique immédiatement pour que les panels remplissent le conteneur
        panelBottom.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _bottomHeight);
        if (bottomScrollContent != null)
        {
            _bottomScrollContentOtherH = _bottomHeight - bottomScrollContent.rect.height;
            bottomScrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(0f, _bottomHeight - _bottomScrollContentOtherH));
        }

        // panelTop : désactive les CSF des enfants directs
        _topDirectChildren.Clear();
        foreach (Transform child in panelTop)
        {
            var rt = child as RectTransform;
            if (rt == null) continue;
            var csf = child.GetComponent<ContentSizeFitter>();
            if (csf != null) csf.enabled = false;
            _topDirectChildren.Add(rt);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelBottom);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_parentRt);
    }


   




    

    public void OnDrag(PointerEventData eventData)
    {
        _topHeight -= eventData.delta.y;
        _bottomHeight += eventData.delta.y;

        if (_topHeight < minTop) { _bottomHeight -= minTop - _topHeight; _topHeight = minTop; }
        if (_bottomHeight < minBottom) { _topHeight -= minBottom - _bottomHeight; _bottomHeight = minBottom; }

        ApplySizes();
    }

  
}