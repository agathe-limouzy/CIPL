using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Vue globale des créances (menu principal, TOUS bâtiments) : synthèse
/// (en attente / impayé / total dû), récap « qui doit combien » par locataire,
/// filtres (bâtiment / banque / locataire / état) et tableau des factures dues
/// (Envoyé + Impayé) avec bâtiment, locataire et banque par ligne.
/// Clic sur une ligne → ouvre le détail du locataire.
public class FacturationGlobalPanel : MonoBehaviour
{
    public static FacturationGlobalPanel Instance { get; private set; }

    readonly List<BatimentPrefab> _bps = new List<BatimentPrefab>();
    Transform _tilesBox, _chipsBox, _tableBox;
    UIDropdown _batDD, _bankDD, _locDD, _etatDD;

    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    struct Entry { public BatimentPrefab bp; public Locataire loc; public FactureEtat rec; public FacturationSuivi.Etat etat; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        UIFactory.Stretch(rt);
        Build();
        gameObject.SetActive(false);
    }

    public static void Open(IEnumerable<BatimentPrefab> bps)
    {
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("FacturationGlobalPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<FacturationGlobalPanel>();
        }
        Instance.OpenFor(bps);
    }

    void OpenFor(IEnumerable<BatimentPrefab> bps)
    {
        _bps.Clear();
        if (bps != null) _bps.AddRange(bps.Where(b => b != null));
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshFilters();
        RebuildAll();
    }

    public void Close() => gameObject.SetActive(false);

    // ── Construction ───────────────────────────────────────────────────────────

    void Build()
    {
        gameObject.AddComponent<Image>().color = UITheme.Fond;

        var col = UIFactory.VBox(transform, 12, 24, 24, 14, 16, "Col");
        UIFactory.Stretch((RectTransform)col.transform);
        col.childForceExpandHeight = false;

        var header = UIFactory.HBox(col.transform, 12, false, "Header");
        UIFactory.LE(header.gameObject, minH: 52);
        var back = UIFactory.Button(header.transform, "←  Retour", UITheme.Carte, UITheme.TextePrincipal, 42, 18);
        UIFactory.Border(back.gameObject); UIFactory.LE(back.gameObject, prefW: 140, flexW: 0);
        back.onClick.AddListener(Close);
        UIFactory.Text(header.transform, "Créances — suivi de facturation", 26, UITheme.TextePrincipal, true);

        var tiles = UIFactory.HBox(col.transform, 12, false, "Tiles");
        UIFactory.LE(tiles.gameObject, minH: 64);
        tiles.childForceExpandWidth = true;
        _tilesBox = tiles.transform;

        UIFactory.Text(col.transform, "Qui doit combien", 14, UITheme.TexteSecondaire);
        var chips = UIFactory.HBox(col.transform, 8, false, "Chips");
        chips.childForceExpandWidth = false; chips.childAlignment = TextAnchor.MiddleLeft;
        chips.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _chipsBox = chips.transform;

        var filt = UIFactory.HBox(col.transform, 10, false, "Filters");
        UIFactory.LE(filt.gameObject, minH: 44);
        filt.childForceExpandWidth = false; filt.childAlignment = TextAnchor.MiddleLeft;
        UIFactory.Text(filt.transform, "Filtrer :", 14, UITheme.TexteSecondaire);
        _batDD = UIDropdown.Create(filt.transform, new List<string> { "Tous les bâtiments" }, new List<string> { "" }, 0, _ => RebuildAll());
        UIFactory.LE(_batDD.gameObject, prefW: 190, flexW: 0);
        _bankDD = UIDropdown.Create(filt.transform, new List<string> { "Toutes les banques" }, new List<string> { "" }, 0, _ => RebuildAll());
        UIFactory.LE(_bankDD.gameObject, prefW: 220, flexW: 0);
        _locDD = UIDropdown.Create(filt.transform, new List<string> { "Tous les locataires" }, new List<string> { "" }, 0, _ => RebuildAll());
        UIFactory.LE(_locDD.gameObject, prefW: 200, flexW: 0);
        _etatDD = UIDropdown.Create(filt.transform,
            new List<string> { "En attente + impayé", "Impayé seulement", "En attente seulement" },
            new List<string> { "", "impaye", "attente" }, 0, _ => RebuildAll());
        UIFactory.LE(_etatDD.gameObject, prefW: 190, flexW: 0);

        var content = MakeScroll(col.transform);
        var box = UIFactory.VBox(content, 6, 0, 0, 0, 0, "Rows");
        _tableBox = box.transform;
    }

    // ── Données ────────────────────────────────────────────────────────────────

    List<Entry> AllEntries()
    {
        var res = new List<Entry>();
        foreach (var bp in _bps)
        {
            if (bp == null || bp.listLocataire == null) continue;
            foreach (var loc in bp.listLocataire)
            {
                if (loc?.facturesEtat == null) continue;
                foreach (var r in loc.facturesEtat)
                {
                    var e = FacturationSuivi.EtatDe(r);
                    if (e == FacturationSuivi.Etat.Envoye || e == FacturationSuivi.Etat.Impaye)
                        res.Add(new Entry { bp = bp, loc = loc, rec = r, etat = e });
                }
            }
        }
        return res;
    }

    List<Entry> Filtered()
    {
        string bat = _batDD?.SelectedId, bank = _bankDD?.SelectedId, locId = _locDD?.SelectedId, et = _etatDD?.SelectedId;
        return AllEntries().Where(e =>
            (string.IsNullOrEmpty(bat) || e.bp.getID() == bat) &&
            (string.IsNullOrEmpty(bank) || (e.rec.ribNom ?? "") == bank) &&
            (string.IsNullOrEmpty(locId) || e.loc.id == locId) &&
            (et == "impaye" ? e.etat == FacturationSuivi.Etat.Impaye
                : et == "attente" ? e.etat == FacturationSuivi.Etat.Envoye : true))
            .OrderBy(e => BatNom(e.bp)).ThenBy(e => e.loc.Name).ThenBy(e => Ech(e.rec.echeanceISO))
            .ToList();
    }

    void RefreshFilters()
    {
        var all = AllEntries();

        var bl = new List<string> { "Tous les bâtiments" }; var bi = new List<string> { "" };
        foreach (var bp in _bps.Distinct()) { bl.Add(BatNom(bp)); bi.Add(bp.getID()); }
        _batDD.SetOptions(bl, bi, "");

        var banks = all.Select(e => e.rec.ribNom).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        var kl = new List<string> { "Toutes les banques" }; var ki = new List<string> { "" };
        foreach (var b in banks) { kl.Add(b); ki.Add(b); }
        _bankDD.SetOptions(kl, ki, "");

        var locs = all.Select(e => e.loc).Distinct().ToList();
        var ll = new List<string> { "Tous les locataires" }; var li = new List<string> { "" };
        foreach (var l in locs) { ll.Add(LocNom(l)); li.Add(l.id); }
        _locDD.SetOptions(ll, li, "");
    }

    void RebuildAll()
    {
        var entries = Filtered();
        RebuildTiles(entries);
        RebuildChips(entries);
        RebuildTable(entries);
    }

    // ── Tuiles ──────────────────────────────────────────────────────────────────

    void RebuildTiles(List<Entry> entries)
    {
        foreach (Transform c in _tilesBox) Destroy(c.gameObject);
        float attente = entries.Where(e => e.etat == FacturationSuivi.Etat.Envoye).Sum(e => e.rec.montant);
        float impaye = entries.Where(e => e.etat == FacturationSuivi.Etat.Impaye).Sum(e => e.rec.montant);
        Tile("En attente de paiement", attente, Hex("#185FA5"));
        Tile("Impayé", impaye, Hex("#A32D2D"));
        Tile("Total dû", attente + impaye, UITheme.TextePrincipal);
    }

    void Tile(string label, float val, Color valColor)
    {
        var t = UIFactory.Panel("Tile", _tilesBox, UITheme.Carte);
        UIFactory.Border(t.gameObject); UIFactory.LE(t.gameObject, flexW: 1);
        var v = t.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 10, 10); v.spacing = 2;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true;
        UIFactory.Text(v.transform, label, 13, UITheme.TexteSecondaire);
        UIFactory.Text(v.transform, Montant(val), 22, valColor, true);
    }

    // ── Chips « qui doit combien » ──────────────────────────────────────────────

    void RebuildChips(List<Entry> entries)
    {
        foreach (Transform c in _chipsBox) Destroy(c.gameObject);
        var parLoc = entries.GroupBy(e => e.loc)
            .Select(g => new { loc = g.Key, montant = g.Sum(e => e.rec.montant) })
            .OrderByDescending(x => x.montant);
        foreach (var x in parLoc)
        {
            var chip = UIFactory.Panel("Chip", _chipsBox, UITheme.Carte);
            UIFactory.Border(chip.gameObject);
            var h = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(11, 11, 7, 7); h.spacing = 5;
            h.childControlWidth = true; h.childControlHeight = true;
            chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIFactory.Text(h.transform, LocNom(x.loc) + " —", 13, UITheme.TextePrincipal, true);
            UIFactory.Text(h.transform, Montant(x.montant), 13, Hex("#A32D2D"), true);
        }
    }

    // ── Tableau ─────────────────────────────────────────────────────────────────

    const float WBat = 150, WLoc = 150, WEch = 100, WMontant = 120, WBanque = 175, WEtat = 100;

    void RebuildTable(List<Entry> entries)
    {
        foreach (Transform c in _tableBox) Destroy(c.gameObject);

        var card = UIFactory.Panel("Card", _tableBox, UITheme.Carte);
        UIFactory.Border(card.gameObject);
        var cv = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.spacing = 0; cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var head = Row(cv.transform, Hex("#EDEBE3"), 36, null);
        FixTxt(head, "Bâtiment", WBat, false, UITheme.TexteSecondaire, true);
        FixTxt(head, "Locataire", WLoc, false, UITheme.TexteSecondaire, true);
        FlexTxt(head, "Facture", UITheme.TexteSecondaire, true);
        FixTxt(head, "Échéance", WEch, false, UITheme.TexteSecondaire, true);
        FixTxt(head, "Montant TTC", WMontant, true, UITheme.TexteSecondaire, true);
        FixTxt(head, "Banque (RIB)", WBanque, false, UITheme.TexteSecondaire, true);
        FixTxt(head, "État", WEtat, false, UITheme.TexteSecondaire, true);

        if (entries.Count == 0)
        {
            var empty = Row(cv.transform, UITheme.Carte, 44, null);
            var t = UIFactory.Text(empty, "Aucune facture due (rien en attente ni impayé).", 14, UITheme.TexteSecondaire);
            UIFactory.LE(t.gameObject, flexW: 1);
            return;
        }

        int i = 0;
        foreach (var e in entries)
        {
            var ent = e;
            var row = Row(cv.transform, (i++ % 2 == 0) ? UITheme.Carte : Hex("#F6F4EC"), 46, () => OpenDetail(ent));
            FixTxt(row, BatNom(e.bp), WBat, false, UITheme.TexteSecondaire, false);
            FixTxt(row, LocNom(e.loc), WLoc, false, UITheme.TextePrincipal, true);
            FlexTxt(row, e.rec.libelle, UITheme.TextePrincipal, false);
            FixTxt(row, EchStr(e.rec.echeanceISO), WEch, false, UITheme.TexteSecondaire, false);
            FixTxt(row, Montant(e.rec.montant), WMontant, true, UITheme.TextePrincipal, false);
            FixTxt(row, string.IsNullOrWhiteSpace(e.rec.ribNom) ? "—" : e.rec.ribNom, WBanque, false, UITheme.TexteSecondaire, false);
            PillCell(row, e.etat, WEtat);
        }
    }

    void OpenDetail(Entry e)
    {
        if (e.bp == null || e.loc == null) return;
        if (!e.bp.dictionnairelocataire.TryGetValue(e.loc, out var fiche) || fiche == null) return;
        Close();
        var gm = GeneralMenuPanel.Instance;
        if (gm != null && gm.menuManager != null) { gm.Hide(); gm.menuManager.OnSelect(e.bp); }
        e.bp.ShowLocataireView();
        if (e.bp.menulocataire != null) e.bp.menulocataire.OnSelect(fiche);
        FacturationSuiviPanel.Open(fiche);
    }

    // ── Helpers UI ──────────────────────────────────────────────────────────────

    Transform Row(Transform parent, Color bg, float minH, Action onClick)
    {
        var p = UIFactory.Panel("Row", parent, bg);
        var hl = p.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(16, 16, 0, 0); hl.spacing = 10;
        hl.childControlWidth = true; hl.childControlHeight = true;
        hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
        hl.childAlignment = TextAnchor.MiddleLeft;
        p.gameObject.AddComponent<LayoutElement>().minHeight = minH;
        if (onClick != null) { var b = p.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => onClick()); }
        return p.transform;
    }

    void FixTxt(Transform row, string text, float w, bool right, Color color, bool bold)
    {
        var t = UIFactory.Text(row, text, 14, color, bold, right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
        t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(t.gameObject, prefW: w, minW: w, flexW: 0);
    }

    void FlexTxt(Transform row, string text, Color color, bool bold)
    {
        var t = UIFactory.Text(row, text, 14, color, bold, TextAlignmentOptions.Left);
        t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(t.gameObject, flexW: 1, minW: 150);
    }

    void PillCell(Transform row, FacturationSuivi.Etat etat, float w)
    {
        var cell = UIFactory.HBox(row, 0, false, "EtatCell");
        UIFactory.LE(cell.gameObject, prefW: w, minW: w, flexW: 0);
        cell.childAlignment = TextAnchor.MiddleLeft; cell.childForceExpandWidth = false;
        var pill = UIFactory.Panel("Pill", cell.transform, EtatBg(etat));
        UIFactory.Border(pill.gameObject, EtatFg(etat));
        var ph = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        ph.padding = new RectOffset(10, 10, 3, 3); ph.childControlWidth = true; ph.childControlHeight = true;
        ph.childAlignment = TextAnchor.MiddleCenter;
        pill.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var t = UIFactory.Text(pill.transform, FacturationSuivi.EtatLibelle(etat), 13, EtatFg(etat), true);
        t.raycastTarget = false;
    }

    Transform MakeScroll(Transform parent)
    {
        var srGO = UIFactory.Rect("Scroll", parent);
        var sr = srGO.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 32;
        sr.movementType = ScrollRect.MovementType.Clamped;
        UIFactory.LE(srGO.gameObject, flexH: 1);
        var viewport = UIFactory.Rect("Viewport", srGO);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var vb = UIFactory.VBox(viewport, 16, 2, 8, 2, 12, "Content");
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
    {
        string n = bp != null ? bp.getName() : "";
        return string.IsNullOrWhiteSpace(n) ? "(sans nom)" : n;
    }
    static string LocNom(Locataire l)
        => l == null ? "" : (string.IsNullOrEmpty(l.Name) ? $"Lot {l.lotBatiment}" : l.Name);
    static string Montant(float v) => v.ToString("#,##0.00", Fr) + " €";
    static string EchStr(string iso) => DateTime.TryParse(iso, out var d) ? d.ToString("dd/MM/yyyy") : "—";
    static DateTime Ech(string iso) => DateTime.TryParse(iso, out var d) ? d : DateTime.MaxValue;

    static Color EtatBg(FacturationSuivi.Etat e) => e == FacturationSuivi.Etat.Impaye ? Hex("#FCEBEB") : Hex("#E6F1FB");
    static Color EtatFg(FacturationSuivi.Etat e) => e == FacturationSuivi.Etat.Impaye ? Hex("#A32D2D") : Hex("#185FA5");
    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
