using System.Collections;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mettre sur le Content RectTransform de N'IMPORTE QUEL ScrollRect
  public class ScrollAutoResize : MonoBehaviour
{
    [Header("Parents à rebuilder aussi (du plus proche au plus loin)")]
      [SerializeField]
    private RectTransform[]
parentContentRects;

    [Header("Optionnel : suivi de texte (commentaire)")]
    [SerializeField] private TMP_Text trackedText;
    [SerializeField] private TMP_InputField trackedInput;
    [Header("Hauteur minimale du champ inputField tracké")]
    [SerializeField] private float trackedInputMinHeight = 80f;

    private RectTransform _rt;
    private bool _dirty;
    private string _lastText;

    private void Awake() => _rt =
GetComponent<RectTransform>();

    // --- Déclencheurs ---

    // Appelé manuellement depuis LocatairePrefab (Modify, Save, Pappers...)
      public void SetDirty() => _dirty = true;

    // Automatique quand un prefab enfant est spawné / détruit
    private void OnTransformChildrenChanged() => _dirty = true;

    // Automatique quand le texte change (commentaire)
    private void LateUpdate()
    {
        if (trackedText != null || trackedInput != null)
        {
            string current = (trackedText != null &&
trackedText.gameObject.activeSelf)
                ? trackedText.text
                : trackedInput?.text ?? string.Empty;

            if (current != _lastText)
            {
                _lastText = current;
                _dirty = true;
            }
        }

        if (!_dirty) return;
        _dirty = false;
        StartCoroutine(Rebuild());
    }

    // --- Rebuild ---

    private IEnumerator Rebuild()
    {
        yield return new WaitForEndOfFrame();

        // Force une hauteur minimale sur l'inputField tracké si vide
        if (trackedInput != null && trackedInput.gameObject.activeSelf)
        {
            var rt = trackedInput.GetComponent<RectTransform>();
            if (rt.sizeDelta.y < trackedInputMinHeight)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, trackedInputMinHeight);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);

        foreach (var parent in parentContentRects)
            if (parent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
    }

    // Scroll vers le bas après injection Pappers
    public void ScrollToBottom(ScrollRect scrollRect)
    {
        StartCoroutine(DoScrollToBottom(scrollRect));
    }

    private IEnumerator DoScrollToBottom(ScrollRect scrollRect)
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}