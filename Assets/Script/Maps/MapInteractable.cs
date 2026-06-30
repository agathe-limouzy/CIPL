using UnityEngine;
using UnityEngine.EventSystems;

// ─── À attacher sur le GameObject qui porte le RawImage (la carte) ───
[RequireComponent(typeof(UnityEngine.UI.RawImage))]
public class MapInteractable : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IDragHandler,
    IScrollHandler,
    IPointerClickHandler
{
    [Header("Référence")]
    public MapController mapController;

    [Header("Sensibilité")]
    public float dragSensitivity = 1.0f;

    private bool _wasDragged = false;
    private Vector2 _lastDragPos;

    public void OnPointerDown(PointerEventData eventData)
    {
        _wasDragged = false;
        _lastDragPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        _wasDragged = true;
        Vector2 delta = _lastDragPos - eventData.position;
        _lastDragPos = eventData.position;
        mapController.PanByPixels(delta * dragSensitivity);
    }

    public void OnPointerUp(PointerEventData eventData) { }

    public void OnScroll(PointerEventData eventData)
    {
        if (eventData.scrollDelta.y > 0)
            mapController.ZoomIn();
        else
            mapController.ZoomOut();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Ouvre Google Maps seulement si c'est un vrai clic (pas un drag)
        if (!_wasDragged)
            mapController.OpenInGoogleMaps();
    }
}