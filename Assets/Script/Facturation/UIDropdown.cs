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
        dd._label = UIFactory.Text(row.transform, "", UITheme.Role.Libelle, UITheme.TextePrincipal);
        dd._label.enableWordWrapping = false; dd._label.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        UIFactory.LE(dd._label.gameObject, flexW: 1);
        UIFactory.Text(row.transform, "v", UITheme.Role.Donnee, UITheme.TexteSecondaire);   // indicateur (police-sûr)

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

        // Liste à hauteur bornée, qui DÉFILE : sans ça, une longue liste (reprise sur 4
        // ans en mensuel, fins de palier sur un bail de 9 ans) sortait de l'écran et ses
        // derniers choix étaient inaccessibles.
        const float HauteurLigne = 36f, Espace = 2f, Marge = 4f;
        float hauteurMax = Mathf.Min(360f, root.rect.height * 0.6f);
        float hauteurContenu = _labels.Count * (HauteurLigne + Espace) - Espace + 2 * Marge;

        var myRT = (RectTransform)transform;
        var listBg = UIFactory.Panel("DDList", scrim, Color.white);
        UIFactory.Border(listBg.gameObject);
        var lrt = (RectTransform)listBg.transform;
        lrt.anchorMin = lrt.anchorMax = Vector2.zero;                  // ancrage coin bas-gauche de l'écran
        lrt.sizeDelta = new Vector2(myRT.rect.width, Mathf.Min(hauteurContenu, hauteurMax));

        var viewport = UIFactory.Rect("Viewport", listBg.transform);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = UIFactory.Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = Espace; vlg.padding = new RectOffset((int)Marge, (int)Marge, (int)Marge, (int)Marge);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var defil = listBg.gameObject.AddComponent<ScrollRect>();
        defil.viewport = viewport; defil.content = content;
        defil.horizontal = false; defil.movementType = ScrollRect.MovementType.Clamped;
        defil.scrollSensitivity = 30f;

        for (int i = 0; i < _labels.Count; i++)
        {
            int idx = i;
            bool on = idx == _index;
            var b = UIFactory.Button(vlg.transform, _labels[i], on ? UITheme.PrimaireClair : Color.white,
                UITheme.TextePrincipal, HauteurLigne, UITheme.Role.Donnee, false);
            b.onClick.AddListener(() => { SetIndex(idx, true); Destroy(scrim.gameObject); });
        }

        // Sous le champ, ou au-dessus s'il manque de place en bas.
        UIFactory.PlacePopup(lrt, myRT, 6f);

        // Le choix en cours est visible dès l'ouverture, même en bas d'une longue liste.
        if (hauteurContenu > hauteurMax && _index > 0)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);   // une fois, à l'ouverture
            float haut = Marge + _index * (HauteurLigne + Espace);
            defil.verticalNormalizedPosition = Mathf.Clamp01(1f - haut / (hauteurContenu - hauteurMax));
        }
    }
}
