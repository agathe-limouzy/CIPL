using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Attacher sur le même GameObject que le TMP_InputField
[RequireComponent(typeof(TMP_InputField))]
public class InputFieldMinHeight : MonoBehaviour, ILayoutElement
{
    [SerializeField] public float minHeight = 80f;

    private TMP_InputField _input;

    private void Awake() => _input = GetComponent<TMP_InputField>();

    public void CalculateLayoutInputHorizontal() { }
    public void CalculateLayoutInputVertical() { }

    public float minWidth => -1;
    public float preferredWidth => -1;
    public float flexibleWidth => -1;
    public float flexibleHeight => -1;

    // Hauteur minimale garantie même si le champ est vide
    float ILayoutElement.minHeight => minHeight;

    // Hauteur préférée = max(minHeight, hauteur réelle du texte)
    float ILayoutElement.preferredHeight =>
        Mathf.Max(minHeight, _input != null
            ? _input.textComponent.preferredHeight + 10f
            : minHeight);

    // Priorité plus haute que TMP (0) et LayoutElement (1)
    // → ContentSizeFitter utilise ces valeurs en priorité
    public int layoutPriority => 2;
}