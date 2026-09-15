using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Fabrique d'éléments d'UI stylés au thème CIPL, construits 100 % par code.
/// Utilisée par tout le module Facturation (Réglage, Info Facture, etc.).
public static class UIFactory
{
    // ── Sprite coins arrondis (généré une fois, 9-slice radius 24) ─────────────
    private static Sprite _rounded;
    public static Sprite Rounded()
    {
        if (_rounded != null) return _rounded;
        const int r = 24, s = r * 2 + 6;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float cx = Mathf.Clamp(x, r, s - 1 - r);
                float cy = Mathf.Clamp(y, r, s - 1 - r);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        _rounded = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(.5f, .5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _rounded;
    }

    // ── Primitives ─────────────────────────────────────────────────────────────

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static void Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }

    /// Place un menu déroulant SOUS l'ancre, ou AU-DESSUS s'il manque de place en bas
    /// (ex. dernière ligne d'un tableau). `popup` doit être enfant d'un scrim plein
    /// écran (canvas overlay), ancré en bas-gauche (0,0) ; on gère son pivot et sa
    /// position. Le popup est reconstruit pour mesurer sa hauteur réelle.
    public static void PlacePopup(RectTransform popup, RectTransform anchor, float margin = 6f)
    {
        if (popup == null || anchor == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(popup);
        float h = popup.rect.height;
        var c = new Vector3[4]; anchor.GetWorldCorners(c);   // 0=bas-gauche · 1=haut-gauche
        bool enBas = (c[0].y - h) >= margin;                 // assez de place sous l'ancre ?
        if (enBas)
        {
            popup.pivot = new Vector2(0f, 1f);               // haut-gauche du menu…
            popup.anchoredPosition = new Vector2(c[0].x, c[0].y);   // …au bas-gauche de l'ancre
        }
        else
        {
            popup.pivot = new Vector2(0f, 0f);               // bas-gauche du menu…
            popup.anchoredPosition = new Vector2(c[1].x, c[1].y);   // …au haut-gauche de l'ancre
        }
    }

    public static Image Panel(string name, Transform parent, Color color, bool rounded = true)
    {
        var rt = Rect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        if (rounded) { img.sprite = Rounded(); img.type = Image.Type.Sliced; }
        return img;
    }

    public static Outline Border(GameObject go, Color? color = null)
    {
        var o = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        o.effectColor = color ?? UITheme.Bordure;
        o.effectDistance = new Vector2(1, -1);
        return o;
    }

    public static LayoutElement LE(GameObject go, float minH = -1, float prefH = -1,
        float minW = -1, float prefW = -1, float flexH = -1, float flexW = -1)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (minH >= 0) le.minHeight = minH;
        if (prefH >= 0) le.preferredHeight = prefH;
        if (minW >= 0) le.minWidth = minW;
        if (prefW >= 0) le.preferredWidth = prefW;
        if (flexH >= 0) le.flexibleHeight = flexH;
        if (flexW >= 0) le.flexibleWidth = flexW;
        return le;
    }

    // ── Layout groups ──────────────────────────────────────────────────────────

    public static VerticalLayoutGroup VBox(Transform parent, float spacing = 10,
        int padL = 0, int padR = 0, int padT = 0, int padB = 0, string name = "VBox")
    {
        var rt = Rect(name, parent);
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing; v.padding = new RectOffset(padL, padR, padT, padB);
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperLeft;
        return v;
    }

    public static HorizontalLayoutGroup HBox(Transform parent, float spacing = 8,
        bool expandChildW = false, string name = "HBox")
    {
        var rt = Rect(name, parent);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = expandChildW; h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        return h;
    }

    // ── Texte ──────────────────────────────────────────────────────────────────

    public static TextMeshProUGUI Text(Transform parent, string text, float size,
        Color? color = null, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var rt = Rect("Text", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.enableAutoSizing = false;
        t.color = color ?? UITheme.TextePrincipal;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    // ── Bouton ─────────────────────────────────────────────────────────────────

    public static Button Button(Transform parent, string label, Color bg, Color fg,
        float height = 40, float fontSize = 18, bool bold = true)
    {
        var img = Panel("Button", parent, bg);
        var btn = img.gameObject.AddComponent<Button>();
        LE(img.gameObject, minH: height, prefH: height);
        var t = Text(img.transform, label, fontSize, fg, bold, TextAlignmentOptions.Center);
        Stretch((RectTransform)t.transform, 10, 0, 10, 0);
        var colors = btn.colors; colors.fadeDuration = 0.08f;
        colors.highlightedColor = new Color(1, 1, 1, 0.92f);
        colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        btn.colors = colors;
        return btn;
    }

    // ── Champ de saisie (TMP_InputField) ───────────────────────────────────────

    public static TMP_InputField Input(Transform parent, string placeholder,
        float height = 40, bool multiline = false, float fontSize = 20)
    {
        var img = Panel("Input", parent, Color.white);
        Border(img.gameObject, UITheme.Bordure);
        var input = img.gameObject.AddComponent<TMP_InputField>();
        LE(img.gameObject, minH: height, prefH: height);

        var area = Rect("Text Area", img.transform);
        area.gameObject.AddComponent<RectMask2D>();
        Stretch(area, 12, 7, 12, 7);

        var ph = Text(area, placeholder, fontSize, UITheme.TexteSecondaire, false,
            multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left);
        Stretch((RectTransform)ph.transform);
        var txt = Text(area, "", fontSize, UITheme.TextePrincipal, false,
            multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left);
        Stretch((RectTransform)txt.transform);

        input.textViewport = area;
        input.textComponent = txt;
        input.placeholder = ph;
        input.fontAsset = txt.font;
        input.pointSize = fontSize;
        input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline
                                   : TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.interactable = true;
        input.readOnly = false;

        // Ré-initialise le champ : les références (textComponent, textViewport,
        // placeholder) sont assignées APRÈS l'AddComponent, or l'OnEnable du
        // TMP_InputField a déjà tourné avec des refs nulles → sans ce toggle, la
        // saisie clavier peut ne pas s'enregistrer sur un champ construit par code.
        if (input.gameObject.activeInHierarchy)
        {
            input.enabled = false;
            input.enabled = true;
        }
        return input;
    }

    // ── Toggle ─────────────────────────────────────────────────────────────────

    public static Toggle Toggle(Transform parent, string label, bool on = false, float box = 24)
    {
        var row = HBox(parent, 8, false, "Toggle").gameObject;
        LE(row, minH: box + 4);
        var toggle = row.AddComponent<Toggle>();
        toggle.isOn = on;

        var bg = Panel("Box", row.transform, Color.white);
        Border(bg.gameObject);
        LE(bg.gameObject, minW: box, prefW: box, minH: box, prefH: box);

        var check = Panel("Check", bg.transform, UITheme.Primaire);
        Stretch((RectTransform)check.transform, 4, 4, 4, 4);
        toggle.graphic = check;
        toggle.targetGraphic = bg;

        if (!string.IsNullOrEmpty(label))
        {
            var t = Text(row.transform, label, 18, UITheme.TextePrincipal, false, TextAlignmentOptions.Left);
            LE(t.gameObject, flexW: 1);
        }
        return toggle;
    }

    // ── Carte / Section ────────────────────────────────────────────────────────

    /// Carte crème arrondie avec bordure. Renvoie le VBox interne (corps).
    public static VerticalLayoutGroup Card(Transform parent, float pad = 14, float spacing = 10)
    {
        var bg = Panel("Card", parent, UITheme.Carte);
        Border(bg.gameObject);
        var v = bg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing; v.padding = new RectOffset((int)pad, (int)pad, (int)pad, (int)pad);
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperLeft;
        return v;
    }

    /// Section titrée (bande de titre colorée + corps). Renvoie le VBox du corps.
    public static VerticalLayoutGroup Section(Transform parent, string titre, Color accent,
        Color accentClair)
    {
        var card = Panel("Section", parent, UITheme.Carte);
        Border(card.gameObject);
        var outer = card.gameObject.AddComponent<VerticalLayoutGroup>();
        outer.spacing = 0; outer.padding = new RectOffset(0, 0, 0, 0);
        outer.childControlWidth = true; outer.childControlHeight = true;
        outer.childForceExpandWidth = true; outer.childForceExpandHeight = false;
        outer.childAlignment = TextAnchor.UpperLeft;

        // Bande de titre (hauteur fixe)
        var band = Panel("TitleBand", card.transform, accentClair);
        LE(band.gameObject, minH: 42, prefH: 42);
        var tt = Text(band.transform, titre, 22, accent, true, TextAlignmentOptions.Left);
        var trt = (RectTransform)tt.transform;
        trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 1);
        trt.offsetMin = new Vector2(16, 0); trt.offsetMax = new Vector2(-16, 0);

        // Corps
        var body = VBox(card.transform, 10, 14, 14, 12, 14, "SectionBody");
        return body;
    }
}
