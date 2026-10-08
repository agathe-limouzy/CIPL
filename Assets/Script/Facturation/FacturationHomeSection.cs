using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Section « Créances » intégrée au menu principal (tous bâtiments) : KPIs
/// (en attente / impayé / total dû), récap « qui doit combien », filtres empilés
/// (banque / locataire / état) et tableau des factures dues (Envoyé + Impayé),
/// avec pastille de banque colorée. Clic sur une ligne → détail du locataire.
public class FacturationHomeSection : MonoBehaviour
{
    public Sprite Icon;                                            // icône de la bande (assignée par GeneralMenuPanel)
    readonly List<BatimentPrefab> _bps = new List<BatimentPrefab>();
    readonly HashSet<string> _collapsed = new HashSet<string>();   // locataires repliés (tout déplié par défaut)
    Transform _tableBox, _body;
    TMP_Text _tAtt, _tImp, _tTot;
    UIDropdown _bankDD, _locDD, _etatDD;

    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public void Setup(IEnumerable<BatimentPrefab> bps)
    {
        _bps.Clear();
        if (bps != null) _bps.AddRange(bps.Where(b => b != null));
        if (_tableBox == null) Build();
        RefreshFilters();
        Rebuild();
    }

    // ── Construction (cette GameObject devient la carte) ────────────────────────

    void Build()
    {
        // Fond accent bleu arrondi = liseré sur le côté GAUCHE (comme les autres sections).
        // La hauteur et la largeur de la carte sont posées par GeneralMenuPanel,
        // qui la place à côté ou en dessous de « À traiter » (voir LargeurMin).
        var frame = gameObject.AddComponent<Image>();
        frame.color = Hex("#A9741C"); frame.sprite = UIFactory.Rounded(); frame.type = Image.Type.Sliced; // ambre (argent)
        var outer = gameObject.AddComponent<VerticalLayoutGroup>();
        outer.padding = new RectOffset(Lisere, 0, 0, 0); outer.spacing = 0;   // bleu visible uniquement à gauche (comme À traiter)
        outer.childControlWidth = true; outer.childControlHeight = true;
        outer.childForceExpandWidth = true; outer.childForceExpandHeight = true;

        // Corps crème arrondi (contient tout le contenu) — remplit le fond bleu.
        var cardBg = UIFactory.Panel("CardBg", transform, UITheme.Carte);
        UIFactory.LE(cardBg.gameObject, flexH: 1);
        var v = cardBg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(MargeCorps, MargeCorps, 10, 12); v.spacing = 8;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        _body = cardBg.transform;

        // En-tête accentué : icône + titre (les filtres passent en dessous).
        var headBar = UIFactory.Panel("HeadBar", _body, Hex("#F4E7CD"));
        UIFactory.LE(headBar.gameObject, minH: 58, prefH: 58, flexH: 0);   // uniforme (comme locataire)
        var head = headBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        head.padding = new RectOffset(12, 12, 2, 2); head.spacing = 8;
        head.childControlWidth = true; head.childControlHeight = true;
        head.childForceExpandWidth = false; head.childForceExpandHeight = true;
        head.childAlignment = TextAnchor.MiddleLeft;
        if (Icon != null)
        {
            var ic = UIFactory.Rect("Icon", head.transform);
            var ii = ic.gameObject.AddComponent<Image>();
            ii.sprite = Icon; ii.color = Hex("#A9741C"); ii.preserveAspect = true; ii.raycastTarget = false;
            UIFactory.LE(ic.gameObject, prefW: 22, minW: 22, prefH: 22, minH: 22, flexW: 0);
        }
        var creTitle = UIFactory.Text(head.transform, "Créances", UITheme.Role.Section, Hex("#A9741C"), true);
        creTitle.enableAutoSizing = false;

        // KPIs (3 tuiles avec fond : petit libellé + grand montant).
        var tiles = UIFactory.HBox(_body, 10, false, "KPIs");
        UIFactory.LE(tiles.gameObject, minH: 68, prefH: 68, flexH: 0);
        tiles.childForceExpandWidth = true; tiles.childControlWidth = true;
        tiles.childAlignment = TextAnchor.MiddleLeft;
        _tAtt = Kpi(tiles.transform, "En attente de paiement", Hex("#E6F1FB"), Hex("#85B7EB"), Hex("#185FA5"));
        _tImp = Kpi(tiles.transform, "Impayé", Hex("#FCEBEB"), Hex("#F09595"), Hex("#A32D2D"));
        _tTot = Kpi(tiles.transform, "Total dû", Hex("#F1EFE8"), UITheme.Bordure, UITheme.TextePrincipal);

        // Filtres en ligne (largeur au contenu).
        var filt = UIFactory.HBox(_body, 8, false, "Filtres");
        UIFactory.LE(filt.gameObject, minH: 36, prefH: 36, flexH: 0);
        filt.childForceExpandWidth = false; filt.childControlWidth = true; filt.childControlHeight = true;
        filt.childAlignment = TextAnchor.MiddleLeft;
        // Sans retour à la ligne : serré, il s'empilait lettre par lettre.
        UIFactory.Text(filt.transform, "Filtrer :", UITheme.Role.Mention, UITheme.TexteSecondaire).enableWordWrapping = false;
        _bankDD = UIDropdown.Create(filt.transform, new List<string> { "Toutes les banques" }, new List<string> { "" }, 0, _ => Rebuild(), 34);
        UIFactory.LE(_bankDD.gameObject, prefW: 210, minW: 210, flexW: 0, minH: 34, prefH: 34);
        _locDD = UIDropdown.Create(filt.transform, new List<string> { "Tous les locataires" }, new List<string> { "" }, 0, _ => Rebuild(), 34);
        UIFactory.LE(_locDD.gameObject, prefW: 210, minW: 210, flexW: 0, minH: 34, prefH: 34);
        _etatDD = UIDropdown.Create(filt.transform,
            new List<string> { "En attente + impayé", "Impayés", "En attente" },
            new List<string> { "", "impaye", "attente" }, 0, _ => Rebuild(), 34);
        UIFactory.LE(_etatDD.gameObject, prefW: 210, minW: 210, flexW: 0, minH: 34, prefH: 34);

        // Tableau borné (scroll interne).
        var content = MakeScroll(_body);
        var box = UIFactory.VBox(content, 0, 0, 0, 0, 0, "Rows");
        _tableBox = box.transform;
    }

    // Tuile KPI avec fond : libellé discret + montant en grand.
    TMP_Text Kpi(Transform parent, string label, Color bg, Color border, Color txt)
    {
        var t = UIFactory.Panel("Kpi", parent, bg);
        UIFactory.Border(t.gameObject, border);
        UIFactory.LE(t.gameObject, flexW: 1, minW: 120);
        var vv = t.gameObject.AddComponent<VerticalLayoutGroup>();
        // Explicite : le defaut d'Unity est TRUE, et un groupe qui « veut s'etendre »
        // propage un flexibleHeight jusqu'en haut de la hierarchie — c'est ce qui
        // creusait 104 px de blanc dans la section Loyer.
        vv.childForceExpandHeight = false;
        vv.padding = new RectOffset(14, 14, 8, 8); vv.spacing = 2;
        vv.childControlWidth = true; vv.childControlHeight = true;
        vv.childForceExpandWidth = true; vv.childAlignment = TextAnchor.UpperLeft;
        UIFactory.Text(vv.transform, label, UITheme.Role.EnTete, txt);
        return UIFactory.Text(vv.transform, "—", UITheme.Role.ChiffreCle, txt, true);
    }

    // ── Données ────────────────────────────────────────────────────────────────

    List<FacturationSuivi.Due> Filtered()
    {
        string bank = _bankDD?.SelectedId, locId = _locDD?.SelectedId, et = _etatDD?.SelectedId;
        return FacturationSuivi.Dues(_bps).Where(e =>
            (string.IsNullOrEmpty(bank) || (e.rec.ribNom ?? "") == bank) &&
            (string.IsNullOrEmpty(locId) || e.loc.id == locId) &&
            (et == "impaye" ? e.etat == FacturationSuivi.Etat.Impaye
                : et == "attente" ? e.etat == FacturationSuivi.Etat.Envoye : true))
            .OrderByDescending(e => e.etat == FacturationSuivi.Etat.Impaye)
            .ThenBy(e => LocNom(e.loc)).ThenBy(e => Ech(e.rec.echeanceISO))
            .ToList();
    }

    void RefreshFilters()
    {
        var all = FacturationSuivi.Dues(_bps);

        var banks = all.Select(e => e.rec.ribNom).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        var kl = new List<string> { "Toutes les banques" }; var ki = new List<string> { "" };
        foreach (var b in banks) { kl.Add(b); ki.Add(b); }
        _bankDD.SetOptions(kl, ki, "");

        var locs = all.Select(e => e.loc).Where(l => l != null).Distinct().ToList();
        var ll = new List<string> { "Tous les locataires" }; var li = new List<string> { "" };
        foreach (var l in locs) { ll.Add(LocNom(l)); li.Add(l.id); }
        _locDD.SetOptions(ll, li, "");
    }

    void Rebuild()
    {
        var entries = Filtered();
        float att = entries.Where(e => e.etat == FacturationSuivi.Etat.Envoye).Sum(e => e.rec.montant);
        float imp = entries.Where(e => e.etat == FacturationSuivi.Etat.Impaye).Sum(e => e.rec.montant);
        _tAtt.text = Montant(att); _tImp.text = Montant(imp); _tTot.text = Montant(att + imp);

        foreach (Transform c in _tableBox) Destroy(c.gameObject);

        var head = Row(_tableBox, Hex("#EDEBE3"), 30, null);
        Cell(head, "", WCar, false, UITheme.TexteSecondaire, true);
        CellFlex(head, "Locataire / facture", UITheme.TexteSecondaire, true);
        Cell(head, "Échéance", WEch, false, UITheme.TexteSecondaire, true);
        Cell(head, "Montant TTC", WMont, true, UITheme.TexteSecondaire, true);
        Cell(head, "", WGap, false, UITheme.TexteSecondaire, false);
        Cell(head, "Banque (RIB)", WBank, false, UITheme.TexteSecondaire, true);
        Cell(head, "État", WEtat, false, UITheme.TexteSecondaire, true);

        if (entries.Count == 0)
        {
            var empty = Row(_tableBox, UITheme.Carte, 40, null);
            var t = UIFactory.Text(empty, "Aucune facture due (rien en attente ni impayé).", UITheme.Role.Aide, UITheme.TexteSecondaire);
            UIFactory.LE(t.gameObject, flexW: 1);
            return;
        }

        // Groupé par locataire : une ligne récap (dépliable) puis le détail des factures.
        var groupes = entries.GroupBy(e => e.loc)
            .Select(g => new
            {
                loc = g.Key,
                bp = g.First().bp,
                lignes = g.OrderByDescending(e => e.etat == FacturationSuivi.Etat.Impaye)
                          .ThenBy(e => Ech(e.rec.echeanceISO)).ToList(),
                total = g.Sum(e => e.rec.montant),
                imp = g.Count(e => e.etat == FacturationSuivi.Etat.Impaye)
            })
            .OrderByDescending(g => g.imp > 0).ThenByDescending(g => g.total)
            .ToList();

        foreach (var grp in groupes)
        {
            string locId = grp.loc?.id ?? "";
            bool ouvert = !_collapsed.Contains(locId);

            // Ligne récap.
            var rec = Row(_tableBox, Hex("#E6F1FB"), 34, () => ToggleGroupe(locId));
            CaretCell(rec, ouvert, Hex("#185FA5"));
            CellFlex(rec, LocNom(grp.loc) + "  —  " + BatNom(grp.bp), UITheme.TextePrincipal, true);
            Cell(rec, "", WEch, false, UITheme.TexteSecondaire, false);
            Cell(rec, Montant(grp.total), WMont, true, Hex("#A32D2D"), true);
            Cell(rec, "", WGap, false, UITheme.TexteSecondaire, false);
            Cell(rec, "", WBank, false, UITheme.TexteSecondaire, false);
            if (grp.imp > 0) PillCell(rec, grp.imp + " impayé", Hex("#FCEBEB"), Hex("#A32D2D"), WEtat);
            else PillCell(rec, "À jour", Hex("#E1F5EE"), Hex("#0F6E56"), WEtat);

            if (!ouvert) continue;

            // Détail des factures du locataire.
            int i = 0;
            foreach (var e in grp.lignes)
            {
                var ent = e;
                var row = Row(_tableBox, (i++ % 2 == 0) ? UITheme.Carte : Hex("#F6F4EC"), 32, () => OpenDetail(ent));
                Cell(row, "", WCar, false, UITheme.TexteSecondaire, false);
                CellFlex(row, "    " + e.rec.libelle, UITheme.TextePrincipal, false);
                Cell(row, EchStr(e.rec.echeanceISO), WEch, false, UITheme.TexteSecondaire, false);
                Cell(row, Montant(e.rec.montant), WMont, true, UITheme.TextePrincipal, false);
                Cell(row, "", WGap, false, UITheme.TexteSecondaire, false);
                CellBank(row, e.rec.ribNom, WBank);
                Pill(row, FacturationSuivi.EtatAffiche(e.loc, e.rec), WEtat);   // « Partiel » (08/10) ; montant = le reste
            }
        }
    }

    void ToggleGroupe(string locId)
    {
        if (!_collapsed.Remove(locId)) _collapsed.Add(locId);
        Rebuild();
    }

    void OpenDetail(FacturationSuivi.Due e)
    {
        if (e.bp == null || e.loc == null) return;
        if (!e.bp.dictionnairelocataire.TryGetValue(e.loc, out var fiche) || fiche == null) return;
        var gm = GeneralMenuPanel.Instance;
        if (gm != null && gm.menuManager != null) { gm.Hide(); gm.menuManager.OnSelect(e.bp); }
        e.bp.ShowLocataireView();
        if (e.bp.menulocataire != null) e.bp.menulocataire.OnSelect(fiche);
        // Directement sur le suivi de la fiche, à l'année de la facture (celle où le
        // suivi la range) — plutôt que la vue plein écran ouverte par-dessus.
        int annee = FacturationSuivi.TryEcheance(e.rec.echeanceISO, out var ech) ? ech.Year : 0;
        LocataireSuiviInline.MontrerPour(fiche, annee);
    }

    // ── Helpers UI ──────────────────────────────────────────────────────────────

    const float WCar = 22, WEch = 92, WMont = 108, WGap = 26, WBank = 150, WEtat = 84;
    const float MinLibelle = 110;                        // colonne libre « Locataire / facture »
    const int Lisere = 8, MargeCorps = 14, MargeLigne = 12, EspaceLigne = 10;

    /// Largeur en dessous de laquelle le tableau sort de sa carte : colonnes fixes,
    /// colonne libre à son minimum, espacements (7 cellules) et marges. Tirée des
    /// mêmes constantes que les cellules, elle suit si une colonne change. La
    /// carte l'annonçait à 300 alors qu'il en faut 712 : le menu la serrait donc à
    /// côté de « À traiter » jusqu'à la faire sortir de l'écran (zoom 175 %).
    public static float LargeurMin =>
        Lisere + 2 * MargeCorps + 2 * MargeLigne + 6 * EspaceLigne
        + WCar + MinLibelle + WEch + WMont + WGap + WBank + WEtat;

    Transform Row(Transform parent, Color bg, float minH, Action onClick)
    {
        var p = UIFactory.Panel("Row", parent, bg);
        var hl = p.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(MargeLigne, MargeLigne, 0, 0); hl.spacing = EspaceLigne;
        hl.childControlWidth = true; hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
        hl.childAlignment = TextAnchor.MiddleLeft;
        p.gameObject.AddComponent<LayoutElement>().minHeight = minH;
        if (onClick != null) { var b = p.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => onClick()); }
        return p.transform;
    }

    // Les cellules portent des données (libellé de facture, échéance, montant) :
    // rôle « secondaire ». La légende est réservée aux pastilles d'état.
    void Cell(Transform row, string text, float w, bool right, Color color, bool bold)
    {
        var t = UIFactory.Text(row, text, UITheme.Role.Donnee, color, bold, right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
        t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(t.gameObject, prefW: w, minW: w, flexW: 0);
    }

    void CellFlex(Transform row, string text, Color color, bool bold)
    {
        var t = UIFactory.Text(row, text, UITheme.Role.Donnee, color, bold, TextAlignmentOptions.Left);
        t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(t.gameObject, flexW: 1, minW: MinLibelle);
    }

    // Caret triangle (sprite généré) : pointe à droite si replié, vers le bas si déplié.
    static Sprite _tri;
    static Sprite Tri()
    {
        if (_tri != null) return _tri;
        int s = 24;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        float cy = (s - 1) / 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float t = x / (float)(s - 1);          // 0 gauche → 1 droite
                float half = (1f - t) * (s / 2f);      // large à gauche, pointe à droite
                px[y * s + x] = Mathf.Abs(y - cy) <= half ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        _tri = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(.5f, .5f));
        return _tri;
    }

    void CaretCell(Transform row, bool open, Color color)
    {
        var cell = UIFactory.HBox(row, 0, false, "CaretCell");
        UIFactory.LE(cell.gameObject, prefW: WCar, minW: WCar, flexW: 0);
        cell.childAlignment = TextAnchor.MiddleCenter;
        cell.childControlWidth = true; cell.childControlHeight = true;
        cell.childForceExpandWidth = false; cell.childForceExpandHeight = false;
        var go = UIFactory.Rect("Tri", cell.transform);
        var img = go.gameObject.AddComponent<Image>(); img.sprite = Tri(); img.color = color; img.raycastTarget = false;
        UIFactory.LE(go.gameObject, prefW: 9, minW: 9, prefH: 9, minH: 9, flexW: 0, flexH: 0);
        ((RectTransform)go.transform).localRotation = Quaternion.Euler(0, 0, open ? -90f : 0f);
    }

    // Cellule banque : pastille de couleur + nom du RIB.
    void CellBank(Transform row, string bank, float w)
    {
        var cell = UIFactory.HBox(row, 6, false, "BankCell");
        UIFactory.LE(cell.gameObject, prefW: w, minW: w, flexW: 0);
        cell.childAlignment = TextAnchor.MiddleLeft;
        cell.childControlWidth = true; cell.childControlHeight = true; cell.childForceExpandWidth = false;
        bool none = string.IsNullOrWhiteSpace(bank);
        var dot = UIFactory.Panel("Dot", cell.transform, none ? UITheme.Bordure : BankColor(bank));
        UIFactory.LE(dot.gameObject, prefW: 9, minW: 9, prefH: 9, minH: 9, flexW: 0);
        var t = UIFactory.Text(cell.transform, none ? "—" : bank, UITheme.Role.Donnee, UITheme.TexteSecondaire, false);
        t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(t.gameObject, flexW: 1, minW: 0);
    }

    void Pill(Transform row, FacturationSuivi.Etat etat, float w)
        => PillCell(row, FacturationSuivi.EtatLibelle(etat), EtatBg(etat), EtatFg(etat), w);

    void PillCell(Transform row, string text, Color bg, Color fg, float w)
    {
        var cell = UIFactory.HBox(row, 0, false, "EtatCell");
        UIFactory.LE(cell.gameObject, prefW: w, minW: w, flexW: 0);
        cell.childAlignment = TextAnchor.MiddleLeft; cell.childForceExpandWidth = false;
        var pill = UIFactory.Panel("Pill", cell.transform, bg);
        var pimg = pill.GetComponent<Image>(); if (pimg != null) pimg.sprite = null;   // rectangle plat
        var ph = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        // Explicite : le defaut d'Unity est TRUE, et un groupe qui « veut s'etendre »
        // propage un flexibleHeight jusqu'en haut de la hierarchie — c'est ce qui
        // creusait 104 px de blanc dans la section Loyer.
        ph.childForceExpandHeight = false;
        ph.padding = new RectOffset(8, 8, 2, 2); ph.childControlWidth = true; ph.childControlHeight = true;
        ph.childAlignment = TextAnchor.MiddleCenter;
        var csf = pill.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var t = UIFactory.Text(pill.transform, text, UITheme.Role.Pastille, fg, true);
        t.raycastTarget = false;
    }

    Transform MakeScroll(Transform parent)
    {
        var srGO = UIFactory.Rect("Scroll", parent);
        var sr = srGO.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 28;
        sr.movementType = ScrollRect.MovementType.Clamped;
        UIFactory.LE(srGO.gameObject, flexH: 1);
        var viewport = UIFactory.Rect("Viewport", srGO);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var vb = UIFactory.VBox(viewport, 0, 0, 0, 0, 0, "Content");
        var crt = (RectTransform)vb.transform;
        crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(.5f, 1);
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        var csf = vb.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sr.viewport = viewport; sr.content = crt;
        return vb.transform;
    }

    static string BatNom(BatimentPrefab bp)
    { string n = bp != null ? bp.getName() : ""; return string.IsNullOrWhiteSpace(n) ? "(sans nom)" : n; }
    static string LocNom(Locataire l)
        => l == null ? "" : (string.IsNullOrEmpty(l.Name) ? $"Lot {l.lotBatiment}" : l.Name);
    static string Montant(float v) => v.ToString("#,##0.00", Fr) + " €";
    static string EchStr(string iso) => DateTime.TryParse(iso, out var d) ? d.ToString("dd/MM/yyyy") : "—";
    static DateTime Ech(string iso) => DateTime.TryParse(iso, out var d) ? d : DateTime.MaxValue;

    static Color EtatBg(FacturationSuivi.Etat e) => e == FacturationSuivi.Etat.Impaye ? Hex("#FCEBEB")
        : e == FacturationSuivi.Etat.Partiel ? Hex("#FFE8D6") : Hex("#E6F1FB");
    static Color EtatFg(FacturationSuivi.Etat e) => e == FacturationSuivi.Etat.Impaye ? Hex("#A32D2D")
        : e == FacturationSuivi.Etat.Partiel ? Hex("#B4520A") : Hex("#185FA5");

    // Couleur de pastille par banque (banques connues, sinon dérivée du nom).
    static Color BankColor(string bank)
    {
        if (string.IsNullOrWhiteSpace(bank)) return UITheme.Bordure;
        string b = bank.ToLowerInvariant();
        if (b.Contains("bnp")) return Hex("#185FA5");
        if (b.Contains("agricole") || b.Contains("crédit agri") || b.Contains("credit agri")) return Hex("#2E8B57");
        if (b.Contains("épargne") || b.Contains("epargne") || b.Contains("caisse")) return Hex("#C0392B");
        if (b.Contains("lcl")) return Hex("#0E76A8");
        if (b.Contains("société") || b.Contains("societe") || b.Contains("sg ")) return Hex("#2C3E50");
        if (b.Contains("postale") || b.Contains("poste")) return Hex("#D4A017");
        var pal = new[] { "#185FA5", "#2E8B57", "#8E44AD", "#D35400", "#16A085", "#C0392B" };
        int h = 0; foreach (char c in bank) h = (h * 31 + c) & 0x7fffffff;
        return Hex(pal[h % pal.Length]);
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
