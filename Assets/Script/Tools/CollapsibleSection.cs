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

    private bool _wired;

    private void Awake()
    {
        EnsureInit();

        if (toggleButton != null)
        {
            toggleButton.isOn = startOpen;
            if (!_wired) { toggleButton.onValueChanged.AddListener(OnToggleChanged); _wired = true; }
        }
    }

    // Awake ne tourne pas si l'objet est instancié sous un parent inactif :
    // on initialise à la demande.
    private void EnsureInit()
    {
        if (_scrollAutoResizes == null)
            _scrollAutoResizes = GetComponentsInParent<ScrollAutoResize>(true);
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
        if (content != null) content.SetActive(open);

        if (arrowIcon != null)
            arrowIcon.localRotation = Quaternion.Euler(0f, 0f, open ? 0f : -90f);

        if (notify)
        {
            ForceRebuildLayout();
            EnsureInit();
            if (_scrollAutoResizes != null)
                foreach (var sar in _scrollAutoResizes)
                    sar?.SetDirty();
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