using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class NestedScrollRect : MonoBehaviour,
IBeginDragHandler, IDragHandler, IEndDragHandler,
IScrollHandler
{
    private ScrollRect _self;
    private ScrollRect _parentScroll;

    private void Awake()
    {
        _self = GetComponent<ScrollRect>();

        // Trouve le ScrollRect parent le plus proche
        var t = transform.parent;
        while (t != null)
        {
            _parentScroll = t.GetComponent<ScrollRect>();
            if (_parentScroll != null) break;
            t = t.parent;
        }
    }

    public void OnBeginDrag(PointerEventData e) =>_parentScroll?.OnBeginDrag(e);
    public void OnEndDrag(PointerEventData e) =>_parentScroll?.OnEndDrag(e);

    public void OnDrag(PointerEventData e)
    {
        bool atTop = _self.verticalNormalizedPosition >= 1f;
        bool atBottom = _self.verticalNormalizedPosition <= 0f;
        bool goingUp = e.delta.y < 0f;
        bool goingDown = e.delta.y > 0f;

        if ((atTop && goingUp) || (atBottom && goingDown))
            _parentScroll?.OnDrag(e);
    }

    public void OnScroll(PointerEventData e)
    {
        bool atTop = _self.verticalNormalizedPosition >= 1f;
        bool atBottom = _self.verticalNormalizedPosition <= 0f;
        bool goingUp = e.scrollDelta.y < 0f;
        bool goingDown = e.scrollDelta.y > 0f;

        if ((atTop && goingUp) || (atBottom && goingDown))
            _parentScroll?.OnScroll(e);
    }
}