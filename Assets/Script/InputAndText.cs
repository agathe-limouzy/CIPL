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


    private void Awake()
    {
        _scrollAutoResizes = GetComponentsInParent<ScrollAutoResize>(true);
    }

    public void Start()
    {
        inputModify.onValueChanged.AddListener(_ => CopyValue());
    }

    public void Modify()
    {
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
    }

    public void ApplyValue(string text)
    {
        inputModify.text = text;
        textSaved.text = text;
    }

    public void SetPlaceholder(string text)
    {
        var ph = inputModify.placeholder?.GetComponent<TMP_Text>();
        textSaved.text = text;
        if (ph != null) ph.text = text;
    }

}
