using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(TMP_InputField))]
public class InputFieldScrollFix : MonoBehaviour, IScrollHandler
{
    private ScrollRect _parentScrollRect;
    private TMP_InputField _inputField;

    private void Awake()
    {
        _parentScrollRect = GetComponentInParent<ScrollRect>();
        _inputField = GetComponent<TMP_InputField>();
    }

    public void OnScroll(PointerEventData eventData)
    {
        // Si le champ n'est pas actif/focus → scroll le parent
        if (!_inputField.isFocused && _parentScrollRect != null)
            _parentScrollRect.OnScroll(eventData);
    }
}