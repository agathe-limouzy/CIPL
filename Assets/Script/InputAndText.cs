using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class InputAndText : MonoBehaviour
{


    public TMP_Text textSaved;
    public TMP_InputField inputModify;

    private ScrollAutoResize[] _scrollAutoResizes;
    private TMP_Text _quantite;


    private void Awake()
    {
        EnsureInit();
    }

    // Awake ne tourne pas si l'objet est instancié sous un parent inactif :
    // on initialise à la demande.
    private void EnsureInit()
    {
        if (_scrollAutoResizes == null)
            _scrollAutoResizes = GetComponentsInParent<ScrollAutoResize>(true);
        if (_quantite == null)
        {
            var q = transform.Find("quantité");
            if (q != null) _quantite = q.GetComponent<TMP_Text>();
        }
    }

    // Masque l'unité (« quantité ») si elle est vide ou déjà contenue dans la valeur
    // (évite les doublons « 6 789 € € »). Sinon la garde (%, m²…).
    private void UpdateUnit()
    {
        if (_quantite == null) return;
        string unit = _quantite.text != null ? _quantite.text.Trim() : "";
        string val = textSaved != null ? textSaved.text : "";
        bool show = unit.Length > 0 && (val == null || !val.Contains(unit));
        _quantite.gameObject.SetActive(show);
    }

    public void Start()
    {
        inputModify.onValueChanged.AddListener(_ => CopyValue());
    }

    public void Modify()
    {
        EnsureInit();
        inputModify.gameObject.SetActive(true);
        textSaved.gameObject.SetActive(false);

        ForceRebuildLayout();
        foreach (var sar in _scrollAutoResizes)
            sar.SetDirty();
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


    public void ShowSaveElement()
    {
        EnsureInit();
        inputModify.gameObject.SetActive(false);
        textSaved.gameObject.SetActive(true);
        ForceRebuildLayout();
        foreach (var sar in _scrollAutoResizes)
            sar.SetDirty();

    }

    public void ApplySave(string text)
    {
       ApplyValue(text);
        ShowSaveElement();
    }

    public string GetNewSave()
    {

        ShowSaveElement();
        return GetValue();
    }
    public string GetValue()
    {
       
       CopyValue();
        return inputModify.text;
    }

    private  void CopyValue()
    {
        textSaved.text = inputModify.text;
        EnsureInit();
        UpdateUnit();
    }

    public void ApplyValue(string text)
    {
        inputModify.text = text;
        textSaved.text = text;
        EnsureInit();
        UpdateUnit();
    }

    public void SetPlaceholder(string text)
    {
        var ph = inputModify.placeholder?.GetComponent<TMP_Text>();
        textSaved.text = text;
        if (ph != null) ph.text = text;
    }

}
