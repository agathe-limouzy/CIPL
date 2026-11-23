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
        // Coins de l'ancre dans le repère du parent du menu, mesurés depuis son coin bas-gauche
        // (l'ancrage du menu). Les coins « monde » sont en pixels d'écran : les poser tels quels
        // ne tombait juste qu'au zoom 100 % ; à 175 % le menu partait en haut à droite (07/10).
        var parent = (RectTransform)popup.parent;
        Vector2 basGauche = (Vector2)parent.InverseTransformPoint(c[0]) - parent.rect.min;
        Vector2 hautGauche = (Vector2)parent.InverseTransformPoint(c[1]) - parent.rect.min;
        bool enBas = (basGauche.y - h) >= margin;            // assez de place sous l'ancre ?
        if (enBas)
        {
            popup.pivot = new Vector2(0f, 1f);               // haut-gauche du menu…
            popup.anchoredPosition = basGauche;              // …au bas-gauche de l'ancre
        }
        else
        {
            popup.pivot = new Vector2(0f, 0f);               // bas-gauche du menu…
            popup.anchoredPosition = hautGauche;             // …au haut-gauche de l'ancre
        }
        // Revue 17 (B-19, lot F) : ramené dans l'écran. À 125 % et plus, un menu ouvert près du bord droit en sortait (menu de la
        // pastille du suivi : x 1 773..2 010 pour 1 920 ; filtre des Créances à 150 %). Le pivot est à gauche dans les deux cas.
        var pos = popup.anchoredPosition;
        pos.x = DansEcran(pos.x, popup.rect.width, parent.rect.width, margin);
        if (!enBas) pos.y = DansEcran(pos.y, h, parent.rect.height, margin);
        popup.anchoredPosition = pos;
    }

    /// Le bord gauche (ou bas) d'un menu de `taille`, ramené dans un écran de `ecran` avec `marge` ; un menu plus grand que l'écran
    /// commence à la marge. Pure, testée.
    public static float DansEcran(float position, float taille, float ecran, float marge) =>
        Mathf.Max(marge, Mathf.Min(position, ecran - marge - taille));

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
        t.parseCtrlCharacters = false;   // « \r » d'un chemin Windows : voir ConfirmDialog
        t.text = text; t.fontSize = size; t.enableAutoSizing = false;
        t.color = color ?? UITheme.TextePrincipal;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    // ── Bouton ─────────────────────────────────────────────────────────────────

    public static Button Button(Transform parent, string label, Color bg, Color fg,
        float height = 40, float fontSize = UITheme.Role.Bouton, bool bold = true)
    {
        var img = Panel("Button", parent, bg);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;   // sous un parent inactif, Awake ne le fait pas : `image` restait nul
        LE(img.gameObject, minH: height, prefH: height);
        var t = Text(img.transform, label, fontSize, fg, bold, TextAlignmentOptions.Center);
        Stretch((RectTransform)t.transform, 10, 0, 10, 0);
        var colors = btn.colors; colors.fadeDuration = 0.08f;
        // Revue 17 (D10-08, lot F) : survol visible (avant : blanc à 92 % d'opacité, invisible sur un fond crème ou blanc, options des
        // menus comprises) ; un bouton désactivé s'éteint nettement.
        colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.6f);
        btn.colors = colors;
        return btn;
    }

    /// Bouton dont la largeur suit son libellé (marges de 10 px) : il ne coupe
    /// jamais un mot, quelle que soit la taille de son rôle. Une largeur fixe
    /// calibrée pour 13 pt faisait passer « Générer » sur deux lignes à 15.
    public const float HauteurBandeau = 52f;

    /// Revue visuelle du 16/11 (V-01 à V-04 ; décision du 16/11) : le bandeau de titre commun (plein, 52 de haut, titre blanc de 24, à
    /// la couleur de la famille), posé en tête d'une carte déjà mise en page : il prend toute la largeur, la carte garde ses marges.
    /// `fermer` : un « × » à droite du titre. Les messages au centre (ConfirmDialog) n'en ont pas, exprès.
    public static TMP_Text Bandeau(RectTransform carte, string titre, Color fond, System.Action fermer = null)
    {
        var t = DepartPanel.Entete(carte, titre, fond);
        var bandeau = (RectTransform)t.transform.parent;
        bandeau.GetComponent<LayoutElement>().ignoreLayout = true;
        bandeau.anchorMin = new Vector2(0f, 1f); bandeau.anchorMax = new Vector2(1f, 1f); bandeau.pivot = new Vector2(.5f, 1f);
        bandeau.offsetMin = new Vector2(0f, -HauteurBandeau); bandeau.offsetMax = Vector2.zero;
        bandeau.SetAsFirstSibling();
        var groupe = carte.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (groupe != null) groupe.padding = new RectOffset(groupe.padding.left, groupe.padding.right, (int)HauteurBandeau + 12, groupe.padding.bottom);
        if (fermer != null)
        {
            var x = Button(bandeau, "×", new Color(1f, 1f, 1f, 0f), Color.white, 40, UITheme.Role.Page, false);
            LE(x.gameObject, minW: 40, prefW: 40, flexW: 0);
            x.onClick.AddListener(() => fermer());
        }
        return t;
    }

    /// Revue visuelle du 16/11 (V-05 à V-08 ; décisions du 16/11) : le bas d'une fenêtre : l'abandon puis l'action, compacts à droite,
    /// 40 de haut. `action` nul : le seul bouton d'abandon.
    public static (Button abandon, Button action) PiedFenetre(Transform rangee, string abandon, string action, Color couleur)
    {
        LE(Rect("Espace", rangee).gameObject, flexW: 1);
        var a = Button(rangee, abandon, UITheme.Carte, UITheme.TextePrincipal, 40, UITheme.Role.Bouton, false);
        Border(a.gameObject); LargeurDuTexte(a);
        Button b = null;
        if (action != null) { b = Button(rangee, action, couleur, Color.white, 40, UITheme.Role.Bouton); LargeurDuTexte(b); }
        return (a, b);
    }

    /// Place `x` juste avant son frère `y`. `x.SetSiblingIndex(y.GetSiblingIndex())` seul se trompe quand `x` est déjà avant
    /// `y` : le retirer décale `y` d'un cran, et `x` se retrouve APRÈS lui. Vu le 17/11 : après « Sauvegarder », « Transférer » et
    /// « Départ du locataire », replacés à chaque initialisation de la fiche, passaient derrière « Supprimer ».
    public static void PlacerAvant(Transform x, Transform y)
    {
        int i = y.GetSiblingIndex();
        x.SetSiblingIndex(x.GetSiblingIndex() < i ? i - 1 : i);
    }

    public static void LargeurDuTexte(Button b)
    {
        var h = b.GetComponent<HorizontalLayoutGroup>() ?? b.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(10, 10, 0, 0);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        var le = b.GetComponent<LayoutElement>();
        if (le != null) { le.minWidth = -1; le.preferredWidth = -1; }
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
        {
            t.enableWordWrapping = false;
            // Revue 17 (B-15, lot F) : le bouton ne descend plus sous la largeur de son libellé.
            if (t.GetComponent<LargeurMinTexte>() == null) t.gameObject.AddComponent<LargeurMinTexte>();
        }
    }

    // ── Champ de saisie (TMP_InputField) ───────────────────────────────────────

    public static TMP_InputField Input(Transform parent, string placeholder,
        float height = 40, bool multiline = false, float fontSize = UITheme.Role.Valeur)
    {
        var img = Panel("Input", parent, Color.white);
        Border(img.gameObject, UITheme.BordureChamp);
        var input = img.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = img;   // idem Button : sinon nul sous un parent inactif
        LE(img.gameObject, minH: height, prefH: height);

        var area = Rect("Text Area", img.transform);
        area.gameObject.AddComponent<RectMask2D>();
        Stretch(area, 12, 7, 12, 7);

        var ph = Text(area, placeholder, fontSize, UITheme.TexteExemple, false,
            multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left);
        ph.fontStyle = FontStyles.Italic;
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
        // Un champ à plusieurs lignes garde la molette pour lui : la page ne défilait plus dès que la
        // souris passait dessus (constaté le 30/10 dans Facturer). Le relais la rend à la page.
        if (multiline) img.gameObject.AddComponent<InputFieldScrollFix>();
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
            var t = Text(row.transform, label, UITheme.Role.Libelle, UITheme.TextePrincipal, false, TextAlignmentOptions.Left);
            t.raycastTarget = true;   // Revue 17 (D10-07, lot F) : le libellé coche aussi (avant : seul le carré de 24 px réagissait)
            LE(t.gameObject, flexW: 1);
        }
        return toggle;
    }

    /// Revue 17 (D10-18, lot F ; décision du 16/11) : un choix exclusif en bouton-pastille (comme « Société / Particulier ») : vert
    /// quand il est choisi. C'est un Toggle : à mettre dans un ToggleGroup avec ses voisins.
    public static Toggle TogglePastille(Transform parent, string label, bool on)
    {
        var img = Panel("Choix", parent, UITheme.Carte);
        Border(img.gameObject);
        LE(img.gameObject, minH: 36, prefH: 36, flexW: 1);
        var t = Text(img.transform, label, UITheme.Role.Bouton, UITheme.TextePrincipal, false, TextAlignmentOptions.Center);
        Stretch((RectTransform)t.transform, 10, 0, 10, 0);
        var toggle = img.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = img;
        toggle.transition = Selectable.Transition.None;
        void Peindre(bool choisi)
        {
            img.color = choisi ? UITheme.Primaire : UITheme.Carte;
            t.color = choisi ? Color.white : UITheme.TextePrincipal;
        }
        toggle.isOn = on;
        Peindre(on);
        toggle.onValueChanged.AddListener(Peindre);
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
        // Revue 17 (D10-11, lot F) : rôle Section (24) dans une bande de 52, sur une ligne avec « … » (avant : 22 pt, hors échelle,
        // dans une bande de 42 d'où un titre long sur deux lignes sortait).
        LE(band.gameObject, minH: 52, prefH: 52);
        var tt = Text(band.transform, titre, UITheme.Role.Section, accent, true, TextAlignmentOptions.Left);
        tt.enableWordWrapping = false; tt.overflowMode = TextOverflowModes.Ellipsis;
        var trt = (RectTransform)tt.transform;
        trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 1);
        trt.offsetMin = new Vector2(16, 0); trt.offsetMax = new Vector2(-16, 0);

        // Corps
        var body = VBox(card.transform, 10, 14, 14, 12, 14, "SectionBody");
        return body;
    }

    /// Montre ou cache un champ créé juste après son libellé (motif « Labeled » des panneaux : texte puis champ dans le même
    /// parent). Ne touche pas au parent : masquer `transform.parent` cachait toute la section « Loyer facturé » (vu en BK2, 02/11).
    public static void MontrerAvecLibelle(GameObject champ, bool visible)
    {
        champ.SetActive(visible);
        int i = champ.transform.GetSiblingIndex();
        if (i > 0) champ.transform.parent.GetChild(i - 1).gameObject.SetActive(visible);
    }

    /// Ligne « libellé … valeur » des totaux (motif MontRow des panneaux : une rangée avec le libellé puis la valeur) : la montre
    /// ou la cache, et change son libellé si on en donne un (« TVA 5,5% » au taux de la facture, « Total » seul sans TVA ; BK, 02/11).
    public static void LigneMontant(TMP_Text valeur, bool visible, string libelle = null)
    {
        var rangee = valeur.transform.parent;
        rangee.gameObject.SetActive(visible);
        if (libelle != null) rangee.GetChild(0).GetComponent<TMP_Text>().text = libelle;
    }

    /// Revue visuelle (V-18, décision du 16/11) : le chevron des menus déroulants bâtis par code, dessiné une fois. Avant,
    /// l'indicateur était la lettre « v » (la police n'a pas de « ▾ ») ; les menus des prefabs avaient déjà un chevron.
    static Sprite _chevron;
    public static Image Chevron(Transform parent, float taille = 14)
    {
        if (_chevron == null)
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            Vector2 gauche = new Vector2(6, 21), bas = new Vector2(15.5f, 11), droite = new Vector2(25, 21);
            float Distance(Vector2 pt, Vector2 a, Vector2 b)
            {
                float k = Mathf.Clamp01(Vector2.Dot(pt - a, b - a) / (b - a).sqrMagnitude);
                return Vector2.Distance(pt, a + k * (b - a));
            }
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    var pt = new Vector2(x, y);
                    float d = Mathf.Min(Distance(pt, gauche, bas), Distance(pt, bas, droite));
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(2f - d) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            _chevron = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(.5f, .5f));
        }
        var img = Rect("Chevron", parent).gameObject.AddComponent<Image>();
        img.sprite = _chevron; img.color = UITheme.TexteSecondaire; img.raycastTarget = false;
        LE(img.gameObject, minW: taille, prefW: taille, minH: taille, prefH: taille, flexW: 0);
        return img;
    }

    /// Revue visuelle (V-19, décision du 16/11) : une case à cocher d'un prefab (un carré gris) prend l'allure des cases bâties
    /// par code (`Toggle` plus haut) : rond blanc bordé, pastille verte quand elle est cochée.
    public static void CaseRonde(Toggle t)
    {
        if (!(t.targetGraphic is Image fond) || !(t.graphic is Image coche)) return;
        fond.sprite = Rounded(); fond.type = Image.Type.Sliced; fond.color = Color.white;
        if (fond.GetComponent<Outline>() == null) Border(fond.gameObject);
        coche.sprite = Rounded(); coche.type = Image.Type.Sliced; coche.color = UITheme.Primaire;
        Stretch((RectTransform)coche.transform, 4, 4, 4, 4);
    }
}

/// Revue visuelle (V-17, décision du 16/11) : une date en trois cases « JJ / MM / AAAA », comme dans les fiches, pour les
/// formulaires bâtis par code (factures, charge, révision du dépôt). `text` se lit et s'écrit « jj/mm/aaaa », comme le champ
/// unique qu'elle remplace : les contrôles (`SaisieDate`) et les appelants ne changent pas.
public class ChampDate
{
    public readonly TMP_InputField jour, mois, annee;
    public readonly UnityEngine.Events.UnityEvent<string> onValueChanged = new UnityEngine.Events.UnityEvent<string>();
    bool _muet;   // `text = …` remplit trois cases : un seul signal, à la fin

    public ChampDate(Transform parent)
    {
        var rangee = UIFactory.HBox(parent, 6, false, "Date").transform;
        UIFactory.LE(rangee.gameObject, minH: 40, prefH: 40);
        jour = Case(rangee, "JJ", 2, 52);
        UIFactory.Text(rangee, "/", UITheme.Role.Valeur, UITheme.TexteSecondaire);
        mois = Case(rangee, "MM", 2, 52);
        UIFactory.Text(rangee, "/", UITheme.Role.Valeur, UITheme.TexteSecondaire);
        annee = Case(rangee, "AAAA", 4, 76);
        Suivre(jour, mois); Suivre(mois, annee); Suivre(annee, null);
    }

    /// Le libellé gris au-dessus, puis les trois cases.
    public static ChampDate Avec(Transform parent, string libelle, float taille = UITheme.Role.Libelle)
    {
        UIFactory.Text(parent, libelle, taille, UITheme.TexteSecondaire);
        return new ChampDate(parent);
    }

    /// Dans une rangée : une colonne « libellé au-dessus, trois cases » (même motif que `DepartPanel.Champ`), préremplie si `valeur`.
    /// Revue visuelle du 16/11 : remplace le clone du champ de la fiche (libellé dans la case) dans « Départ », la cession,
    /// « Nouvel objectif » et « Paiement partiel ».
    public static ChampDate Colonne(Transform rangee, string libelle, System.DateTime? valeur = null)
    {
        var col = UIFactory.VBox(rangee, 4, name: libelle);
        UIFactory.LE(col.gameObject, flexW: 1);
        var d = Avec(col.transform, libelle);
        if (valeur.HasValue) d.text = valeur.Value.ToString("dd/MM/yyyy");
        return d;
    }

    /// Les trois cases sont vides (« pas de date », autre chose qu'une date mal tapée).
    public bool Vide() => text == "";

    /// La date des trois cases, si elle est complète et existe (mêmes noms que `DateInputController`, le champ des fiches).
    public bool LireDate(out System.DateTime date) => SaisieDate.TryParse(text, out date) && date.Year >= 1900;

    public string text
    {
        get => Composer(jour.text, mois.text, annee.text);
        set
        {
            string avant = text;
            var p = (value ?? "").Trim().Replace('-', '/').Replace('.', '/').Split('/');
            _muet = true;
            jour.text = p[0]; mois.text = p.Length > 1 ? p[1] : ""; annee.text = p.Length > 2 ? p[2] : "";
            _muet = false;
            if (text != avant) onValueChanged.Invoke(text);
        }
    }

    /// « 5 », « 3 », « 2027 » → « 5/3/2027 » ; trois cases vides → « » (pas de date, autre chose qu'une date mal tapée).
    public static string Composer(string j, string m, string a)
        => string.IsNullOrWhiteSpace(j + m + a) ? "" : $"{j.Trim()}/{m.Trim()}/{a.Trim()}";

    static TMP_InputField Case(Transform rangee, string exemple, int chiffres, float largeur)
    {
        var c = UIFactory.Input(rangee, exemple);
        c.characterLimit = chiffres;
        c.contentType = TMP_InputField.ContentType.IntegerNumber;
        UIFactory.LE(c.gameObject, minW: largeur, prefW: largeur, flexW: 0);
        UIFactory.Stretch(c.textViewport, 4, 7, 4, 7);   // « AAAA » en italique ne tient pas avec la marge de 12 d'un champ large
        c.textComponent.alignment = TextAlignmentOptions.Center;
        ((TMP_Text)c.placeholder).alignment = TextAlignmentOptions.Center;
        return c;
    }

    // Une case complète passe à la suivante (« 14 » → le mois), seulement quand on tape : `text = …` ne déplace pas le curseur.
    void Suivre(TMP_InputField c, TMP_InputField suivante)
        => c.onValueChanged.AddListener(v =>
        {
            if (_muet) return;
            onValueChanged.Invoke(text);
            if (suivante != null && c.isFocused && v.Length >= c.characterLimit) { suivante.Select(); suivante.ActivateInputField(); }
        });
}
