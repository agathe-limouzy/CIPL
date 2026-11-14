using UnityEngine;
using UnityEngine.EventSystems;

// ─── À attacher sur le GameObject qui porte le RawImage (la carte) ───
[RequireComponent(typeof(UnityEngine.UI.RawImage))]
public class MapInteractable : MonoBehaviour,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IScrollHandler,
    IPointerClickHandler
{
    [Header("Référence")]
    public MapController mapController;

    [Header("Sensibilité")]
    public float dragSensitivity = 1.0f;

    private bool _wasDragged = false;
    private Vector2 _debutGlisser;   // point de départ du geste, dans le repère de la carte

    public void OnPointerDown(PointerEventData eventData)
    {
        _wasDragged = false;
    }

    // Revue 17 (D7b-20, lot D) : chaque mouvement du glisser relançait une image Google (facturée), et les mouvements
    // faits pendant un chargement étaient perdus. Pendant le geste, seule la texture glisse dans son cadre (uvRect :
    // gratuit, sans déborder sur l'adresse : la carte est placée par un groupe de mise en page) ; au relâchement,
    // UNE image est demandée pour le déplacement cumulé.
    public void OnBeginDrag(PointerEventData eventData)
    {
        _wasDragged = true;
        _debutGlisser = PointLocal(eventData.pressPosition, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        var rt = (RectTransform)transform;
        if (rt.rect.width <= 0 || rt.rect.height <= 0) return;
        Vector2 doigt = PointLocal(eventData.position, eventData) - _debutGlisser;
        // La texture suit le doigt : doigt vers la droite → la fenêtre de texture part vers la gauche.
        var decalage = new Vector2(-doigt.x / rt.rect.width, -doigt.y / rt.rect.height) * dragSensitivity;
        GetComponent<UnityEngine.UI.RawImage>().uvRect = new Rect(decalage, Vector2.one);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Décalage de texture → pixels de l'image Google (ancienne − nouvelle position, y vers le haut : la convention
        // de PanByPixels). L'image décalée reste à l'écran jusqu'à la nouvelle (MapController.RemettreImage).
        var tl = mapController.GetComponent<TileLoader>();
        Vector2 uv = GetComponent<UnityEngine.UI.RawImage>().uvRect.position;
        mapController.PanByPixels(new Vector2(uv.x * (tl != null ? tl.imageWidth : 512),
                                              uv.y * (tl != null ? tl.imageHeight : 512)));
    }

    Vector2 PointLocal(Vector2 ecran, PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, ecran, eventData.pressEventCamera, out var local);
        return local;
    }

    // Revue 17 (D7b-19, lot D, décision du 11/11) : la molette zoomait (une image Google par cran) et bloquait le
    // défilement de la fiche sous le pointeur. La molette seule fait défiler la fiche ; Ctrl + molette zoome ;
    // les boutons + et − restent.
    public void OnScroll(PointerEventData eventData)
    {
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (eventData.scrollDelta.y > 0) mapController.ZoomIn();
            else if (eventData.scrollDelta.y < 0) mapController.ZoomOut();
        }
        else if (transform.parent != null)
            ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.scrollHandler);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Ouvre Google Maps seulement si c'est un vrai clic (pas un drag)
        if (!_wasDragged)
            mapController.OpenInGoogleMaps();
    }
}