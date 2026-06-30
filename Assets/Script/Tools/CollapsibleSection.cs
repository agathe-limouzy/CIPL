using UnityEngine;
using UnityEngine.UI;

public class CollapsibleSection : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject content;
    [SerializeField] private Toggle toggleButton;  // ← Toggle au lieu de Button
    [SerializeField] private RectTransform arrowIcon;

    [Header("État initial")]
    [SerializeField] private bool startOpen = true;

    private ScrollAutoResize[] _scrollAutoResizes;

    public bool IsOpen => toggleButton != null ? toggleButton.isOn : false;

    private void Awake()
    {
        _scrollAutoResizes = GetComponentsInParent<ScrollAutoResize>(true);

        if (toggleButton != null)
        {
            toggleButton.isOn = startOpen;
            toggleButton.onValueChanged.AddListener(OnToggleChanged);
        }
    }

    private void Start()
    {
        ApplyState(startOpen, notify: false);
    }

    // ── API publique ──────────────────────────────────────────────────────────

    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);
    public void SetOpen(bool open)
    {
        // Met à jour le Toggle sans déclencher onValueChanged en double
        if (toggleButton != null)
        {
            toggleButton.onValueChanged.RemoveListener(OnToggleChanged);
            toggleButton.isOn = open;
            toggleButton.onValueChanged.AddListener(OnToggleChanged);
        }
        ApplyState(open, notify: true);
    }

    // ── Interne ───────────────────────────────────────────────────────────────

    private void OnToggleChanged(bool isOn) => ApplyState(isOn, notify: true);

    private void ApplyState(bool open, bool notify)
    {
        content.SetActive(open);

        if (arrowIcon != null)
            arrowIcon.localRotation = Quaternion.Euler(0f, 0f, open ? 0f : -90f);

        if (notify)
        {
            ForceRebuildLayout();
            foreach (var sar in _scrollAutoResizes)
                sar.SetDirty();
        }
    }

    // Rebuild immédiat de toute la hiérarchie jusqu'au ScrollRect
    private void ForceRebuildLayout()
    {
        Transform t = transform;
        while (t != null)
        {
            var rt = t.GetComponent<RectTransform>();
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            if (t.GetComponent<ScrollRect>() != null) break;
            t = t.parent;
        }
    }
}