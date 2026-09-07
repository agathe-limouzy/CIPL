using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Menu déroulant léger construit par code (bouton + popup de choix), sans la
/// complexité du template TMP_Dropdown. Réutilisable dans les menus de facturation.
public class UIDropdown : MonoBehaviour
{
    TMP_Text _label;
    List<string> _labels = new List<string>();
    List<string> _ids = new List<string>();
    int _index = -1;
    Action<int> _onChange;

    public int Index => _index;
    public string SelectedId => _index >= 0 && _index < _ids.Count ? _ids[_index] : null;

    public static UIDropdown Create(Transform parent, List<string> labels, List<string> ids,
        int initial, Action<int> onChange, float height = 44)
    {
        var img = UIFactory.Panel("Dropdown", parent, Color.white);
        UIFactory.Border(img.gameObject, UITheme.Bordure);
        UIFactory.LE(img.gameObject, minH: height, prefH: height);

        var dd = img.gameObject.AddComponent<UIDropdown>();
        dd._labels = labels ?? new List<string>();
        dd._ids = ids ?? new List<string>();
        dd._onChange = onChange;

        var row = UIFactory.HBox(img.transform, 8, false, "Row");
        UIFactory.Stretch((RectTransform)row.transform, 12, 4, 12, 4);
        dd._label = UIFactory.Text(row.transform, "", 18, UITheme.TextePrincipal);
        UIFactory.LE(dd._label.gameObject, flexW: 1);
        UIFactory.Text(row.transform, "v", 16, UITheme.TexteSecondaire);   // indicateur (police-sûr)

        var btn = img.gameObject.AddComponent<Button>();
        btn.onClick.AddListener(dd.OpenPopup);

        dd.SetIndex(initial, false);
        return dd;
    }

    /// Remplace les options (et re-sélectionne `selectId` si présent, sinon la 1re).
    public void SetOptions(List<string> labels, List<string> ids, string selectId)
    {
        _labels = labels ?? new List<string>();
        _ids = ids ?? new List<string>();
        int i = !string.IsNullOrEmpty(selectId) ? _ids.IndexOf(selectId) : 0;
        SetIndex(i < 0 ? 0 : i, false);
    }

    public void SetIndex(int i, bool notify)
    {
        _index = i;
        if (_label != null) _label.text = (i >= 0 && i < _labels.Count) ? _labels[i] : "—";
        if (notify) _onChange?.Invoke(i);
    }

    void OpenPopup()
    {
        if (_labels.Count == 0) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var root = (RectTransform)canvas.rootCanvas.transform;

        // Scrim transparent plein écran : clic en dehors = fermer.
        var scrim = UIFactory.Rect("DDScrim", root);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        var sBtn = scrim.gameObject.AddComponent<Button>();
        scrim.SetAsLastSibling();
        sBtn.onClick.AddListener(() => Destroy(scrim.gameObject));

        // Liste : VLG directement sur le fond + CSF vertical.
        var listBg = UIFactory.Panel("DDList", scrim, Color.white);
        UIFactory.Border(listBg.gameObject);
        var vlg = listBg.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2; vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var csf = listBg.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var myRT = (RectTransform)transform;
        var corners = new Vector3[4]; myRT.GetWorldCorners(corners);   // 0=BL 1=TL 2=TR 3=BR
        var lrt = (RectTransform)listBg.transform;
        lrt.anchorMin = lrt.anchorMax = Vector2.zero;                  // ancrage coin bas-gauche de l'écran
        lrt.pivot = new Vector2(0, 1);                                 // pivot haut-gauche (déroule vers le bas)
        lrt.sizeDelta = new Vector2(myRT.rect.width, 0);
        lrt.anchoredPosition = new Vector2(corners[0].x, corners[0].y);

        for (int i = 0; i < _labels.Count; i++)
        {
            int idx = i;
            bool on = idx == _index;
            var b = UIFactory.Button(vlg.transform, _labels[i], on ? UITheme.PrimaireClair : Color.white,
                UITheme.TextePrincipal, 36, 16, false);
            b.onClick.AddListener(() => { SetIndex(idx, true); Destroy(scrim.gameObject); });
        }
    }
}
