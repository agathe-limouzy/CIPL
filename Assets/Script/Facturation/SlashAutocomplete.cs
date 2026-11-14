using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Menu « slash » façon terminal pour un TMP_InputField : quand on tape « / »
/// (en début de mot), un panneau apparaît avec la liste des variables, filtrée
/// au fil de la frappe. Sélection : clic, ou Tab/Entrée sur l'élément surligné
/// (↑/↓ pour naviguer, Échap pour fermer).
public class SlashAutocomplete : MonoBehaviour
{
    TMP_InputField input;
    RectTransform popup;
    Transform box;
    readonly List<Button> items = new();
    List<FactureVariable> filtered = new();
    int slashIndex = -1, caretIndex = -1, highlight = 0;
    string[] groupesEnPlus;

    /// `groupesEnPlus` (revue 17, D6-08) : les groupes à portée limitée que ce champ remplace (FactureVariables.GroupesLimites).
    public static SlashAutocomplete Attach(TMP_InputField field, params string[] groupesEnPlus)
    {
        var go = new GameObject("SlashAutocomplete", typeof(RectTransform));
        go.transform.SetParent(field.transform, false);
        var sa = go.AddComponent<SlashAutocomplete>();
        sa.input = field;
        sa.groupesEnPlus = groupesEnPlus;
        sa.BuildPopup();
        field.onValueChanged.AddListener(sa.OnChanged);
        return sa;
    }

    Canvas rootCanvas;
    RectTransform inputRT;

    void BuildPopup()
    {
        inputRT = (RectTransform)input.transform;
        rootCanvas = input.GetComponentInParent<Canvas>();
        Transform parent = rootCanvas != null ? rootCanvas.rootCanvas.transform : input.transform;

        var img = UIFactory.Panel("SlashPopup", parent, UITheme.Carte);
        UIFactory.Border(img.gameObject);
        popup = (RectTransform)img.transform;
        popup.anchorMin = popup.anchorMax = new Vector2(0, 0);
        popup.pivot = new Vector2(0, 1);
        popup.sizeDelta = new Vector2(260, 0);

        var vlg = img.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4, 4, 4, 4); vlg.spacing = 2;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var csf = img.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        box = img.transform;
        img.gameObject.SetActive(false);
    }

    // Revue 17 (D6-26, coutures) : sous le champ, ou au-dessus s'il manque de place en bas (UIFactory.PlacePopup, comme les
    // menus déroulants) ; avant, toujours dessous : pour un champ en bas de l'écran, la liste sortait de la fenêtre.
    void Position()
    {
        popup.sizeDelta = new Vector2(inputRT.rect.width, popup.sizeDelta.y);
        UIFactory.PlacePopup(popup, inputRT);
    }

    void OnDestroy()
    {
        if (popup != null) Destroy(popup.gameObject);
    }

    // Revue 17 (D6-14) : le menu vit sous le canevas racine, hors du formulaire ; fermer le formulaire le masque aussi
    // (avant, il restait affiché sur la fiche, et un clic modifiait un champ caché).
    void OnDisable() => Hide();

    void OnChanged(string text)
    {
        int caret = Mathf.Clamp(input.stringPosition, 0, text.Length);
        int slash = -1;
        for (int i = caret - 1; i >= 0; i--)
        {
            char c = text[i];
            if (c == '/') { slash = i; break; }
            if (char.IsWhiteSpace(c)) break;
        }
        bool ok = slash >= 0 && (slash == 0 || char.IsWhiteSpace(text[slash - 1]));
        if (!ok) { Hide(); return; }

        slashIndex = slash; caretIndex = caret;
        ShowFiltered(text.Substring(slash + 1, caret - slash - 1));
    }

    /// Revue 17 (D6-08) : les variables que le menu propose pour `query` (libellé ou jeton) dans un champ qui nomme
    /// `groupesEnPlus` : celles d'un groupe à portée limitée que le champ ne nomme pas n'y sont pas remplacées, elles n'y
    /// sont donc pas proposées (avant : tout le catalogue partout, {depot.montant} partait tel quel dans un e-mail).
    public static List<FactureVariable> Proposees(string query, params string[] groupesEnPlus)
    {
        string q = (query ?? "").ToLowerInvariant();
        var r = new List<FactureVariable>();
        foreach (var v in FactureVariables.All)
            if ((System.Array.IndexOf(FactureVariables.GroupesLimites, v.groupe) < 0
                 || (groupesEnPlus != null && System.Array.IndexOf(groupesEnPlus, v.groupe) >= 0))
                && (q.Length == 0 || v.label.ToLowerInvariant().Contains(q) || v.token.ToLowerInvariant().Contains(q)))
                r.Add(v);
        return r;
    }

    void ShowFiltered(string query)
    {
        filtered = Proposees(query, groupesEnPlus);
        if (filtered.Count == 0) { Hide(); return; }

        // D6-26 : masquées avant d'être détruites (en fin d'image) : PlacePopup mesure la liste tout de suite, sans les anciennes lignes.
        foreach (Transform c in box) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        items.Clear();

        int max = Mathf.Min(filtered.Count, 8);
        for (int i = 0; i < max; i++)
        {
            var v = filtered[i];
            var b = UIFactory.Button(box, "", UITheme.Carte, UITheme.TextePrincipal, 30, UITheme.Role.Donnee, false);
            var t = b.GetComponentInChildren<TMP_Text>();
            t.richText = true;
            t.text = v.label + "   <size=85%><color=#8A877E>" + v.token + "</color></size>";
            t.alignment = TextAlignmentOptions.Left;
            int idx = i;
            b.onClick.AddListener(() => Select(idx));
            items.Add(b);
        }

        highlight = 0;
        popup.gameObject.SetActive(true);
        popup.SetAsLastSibling();
        Position();
        UpdateHighlight();
    }

    void UpdateHighlight()
    {
        for (int i = 0; i < items.Count; i++)
        {
            var img = items[i].GetComponent<Image>();
            if (img != null) img.color = i == highlight ? UITheme.PrimaireClair : UITheme.Carte;
        }
    }

    void Select(int i)
    {
        if (i < 0 || i >= filtered.Count) return;
        string token = filtered[i].token;
        string text = input.text;
        caretIndex = Mathf.Clamp(caretIndex, 0, text.Length);
        slashIndex = Mathf.Clamp(slashIndex, 0, caretIndex);

        input.text = text.Substring(0, slashIndex) + token + text.Substring(caretIndex);
        input.stringPosition = slashIndex + token.Length;
        Hide();
        input.ActivateInputField();
    }

    void Hide()
    {
        if (popup != null) popup.gameObject.SetActive(false);
    }

    void OnGUI()
    {
        if (popup == null || !popup.gameObject.activeSelf) return;
        var e = Event.current;
        if (e.type != EventType.KeyDown) return;

        switch (e.keyCode)
        {
            case KeyCode.Escape: Hide(); e.Use(); break;
            case KeyCode.DownArrow:
                highlight = Mathf.Min(highlight + 1, items.Count - 1); UpdateHighlight(); e.Use(); break;
            case KeyCode.UpArrow:
                highlight = Mathf.Max(highlight - 1, 0); UpdateHighlight(); e.Use(); break;
            case KeyCode.Tab:
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                Select(highlight); e.Use(); break;
        }
    }
}
