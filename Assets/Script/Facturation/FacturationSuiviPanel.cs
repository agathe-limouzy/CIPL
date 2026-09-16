using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Tableau de suivi de facturation d'UN locataire : sélecteur d'année + liste des
/// factures (loyers, régularisation, refac, dépôt) avec leur état (À venir / À faire /
/// Envoyé / Impayé / Payé), et les actions (Générer, PDF, Refaire, changer l'état).
public class FacturationSuiviPanel : MonoBehaviour
{
    public static FacturationSuiviPanel Instance { get; private set; }

    LocatairePrefab _fiche; Locataire _loc;
    int _year;
    Transform _tableBox, _yearRow;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        UIFactory.Stretch(rt);
        Build();
        gameObject.SetActive(false);
    }

    public static void Open(LocatairePrefab fiche)
    {
        if (fiche == null) return;
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("FacturationSuiviPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<FacturationSuiviPanel>();
        }
        Instance.OpenFor(fiche);
    }

    void OpenFor(LocatairePrefab fiche)
    {
        _fiche = fiche;
        _loc = fiche.GetLocataire();
        _year = DateTime.Today.Year;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RebuildYears();
        RebuildTable();
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
        UIFactory.Text(header.transform, "Suivi de facturation", 26, UITheme.TextePrincipal, true);

        var yr = UIFactory.HBox(col.transform, 6, false, "Years");
        UIFactory.LE(yr.gameObject, minH: 40);
        UIFactory.Text(yr.transform, "Année", 15, UITheme.TexteSecondaire);
        _yearRow = yr.transform;

        var content = MakeScroll(col.transform);
        var box = UIFactory.VBox(content, 6, 0, 0, 0, 0, "Rows");
        _tableBox = box.transform;
    }

    void RebuildYears()
    {
        foreach (Transform c in _yearRow) if (c.GetComponent<Button>() != null) Destroy(c.gameObject);
        int cur = DateTime.Today.Year;
        for (int y = cur - 3; y <= cur + 1; y++)
        {
            int yy = y;
            bool on = y == _year;
            var b = UIFactory.Button(_yearRow, y.ToString(), on ? UITheme.Primaire : UITheme.Carte,
                on ? Color.white : UITheme.TextePrincipal, 34, 15, false);
            UIFactory.Border(b.gameObject); UIFactory.LE(b.gameObject, prefW: 74, flexW: 0);
            b.onClick.AddListener(() => { _year = yy; RebuildYears(); RebuildTable(); });
            // Pastille si l'année contient une facturation à faire / en attente / impayée.
            var pc = YearAlerteColor(yy);
            if (pc.HasValue) AjouteYearPastille(b.gameObject, pc.Value);
        }
    }

    Color? YearAlerteColor(int y)
    {
        if (_loc == null) return null;
        bool impaye = false, autre = false;
        foreach (var l in FacturationSuivi.Lignes(_loc, y))
        {
            var e = FacturationSuivi.EtatDe(l);
            if (e == FacturationSuivi.Etat.Impaye) impaye = true;
            else if (e == FacturationSuivi.Etat.AFaire || e == FacturationSuivi.Etat.AttenteEnvoi) autre = true;
        }
        Color c;
        if (impaye) { ColorUtility.TryParseHtmlString("#D85A30", out c); return c; }
        if (autre)  { ColorUtility.TryParseHtmlString("#A9741C", out c); return c; }
        return null;
    }

    static void AjouteYearPastille(GameObject chip, Color c)
    {
        var p = UIFactory.Panel("Pastille", chip.transform, c);
        p.raycastTarget = false;
        var rt = (RectTransform)p.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(10f, 10f);
        rt.anchoredPosition = new Vector2(-2f, -2f);
    }

    // Largeurs fixes (px) des colonnes — identiques en-tête/lignes → alignement parfait.
    // La colonne « Facture » prend le reste (flexible).
    const float WEch = 130, WMontant = 140, WEtat = 132, WActions = 280;

    void RebuildTable()
    {
        if (_tableBox == null || _loc == null) return;
        foreach (Transform c in _tableBox) Destroy(c.gameObject);

        var lignes = FacturationSuivi.Lignes(_loc, _year);

        // Carte englobante (bord arrondi) contenant l'en-tête + les lignes.
        var card = UIFactory.Panel("Card", _tableBox, UITheme.Carte);
        UIFactory.Border(card.gameObject);
        var cv = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.spacing = 0; cv.padding = new RectOffset(0, 0, 0, 0);
        cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // En-tête.
        var head = Row(cv.transform, Hex("#EDEBE3"), 36);
        FactCell(head, "Facture", UITheme.TexteSecondaire, true);
        FixCell(head, "Échéance", WEch, false, UITheme.TexteSecondaire, true);
        FixCell(head, "Montant TTC", WMontant, true, UITheme.TexteSecondaire, true);
        FixCell(head, "État", WEtat, false, UITheme.TexteSecondaire, true);
        FixCell(head, "Actions", WActions, false, UITheme.TexteSecondaire, true);

        if (lignes.Count == 0)
        {
            var empty = Row(cv.transform, UITheme.Carte, 44);
            var t = UIFactory.Text(empty, "Aucune facture pour cette année.", 14, UITheme.TexteSecondaire);
            UIFactory.LE(t.gameObject, flexW: 1);
            return;
        }

        int i = 0;
        foreach (var l in lignes)
        {
            var etat = FacturationSuivi.EtatDe(l);
            bool clot = etat == FacturationSuivi.Etat.Cloture;   // période reprise (historique) → grisée
            Color cMain = clot ? Hex("#A8A7A1") : UITheme.TextePrincipal;
            Color cSec = clot ? Hex("#B7B6B0") : UITheme.TexteSecondaire;
            var row = Row(cv.transform, clot ? Hex("#F0EEE8") : ((i % 2 == 0) ? UITheme.Carte : Hex("#F6F4EC")), 46);
            i++;
            FactCell(row, l.libelle, cMain, false);
            FixCell(row, Ech(l.echeanceISO), WEch, false, cSec, false);
            FixCell(row, Montant(l.montant), WMontant, true, cMain, false);
            PillCell(row, l, etat, WEtat);
            ActionsCell(row, l, etat, WActions);
        }
    }

    // Une ligne : fond `bg`, hauteur mini `minH`, colonnes centrées verticalement.
    Transform Row(Transform parent, Color bg, float minH)
    {
        var p = UIFactory.Panel("Row", parent, bg);
        var hl = p.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(16, 16, 0, 0); hl.spacing = 10;
        hl.childControlWidth = true; hl.childControlHeight = true;
        hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
        hl.childAlignment = TextAnchor.MiddleLeft;
        p.gameObject.AddComponent<LayoutElement>().minHeight = minH;
        return p.transform;
    }

    // Colonne « Facture » : flexible (prend le reste), minimum 200 px.
    void FactCell(Transform row, string text, Color color, bool bold)
    {
        var t = UIFactory.Text(row, text, 14, color, bold, TextAlignmentOptions.Left);
        t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(t.gameObject, flexW: 1, minW: 200);
    }

    // Colonne à largeur fixe (identique en-tête et lignes).
    void FixCell(Transform row, string text, float w, bool right, Color color, bool bold)
    {
        var t = UIFactory.Text(row, text, 14, color, bold,
            right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
        UIFactory.LE(t.gameObject, prefW: w, minW: w, flexW: 0);
    }

    void PillCell(Transform row, FactureEtat l, FacturationSuivi.Etat etat, float w)
    {
        var cell = UIFactory.HBox(row, 0, false, "EtatCell");
        UIFactory.LE(cell.gameObject, prefW: w, minW: w, flexW: 0);
        cell.childAlignment = TextAnchor.MiddleLeft; cell.childForceExpandWidth = false;
        var pill = UIFactory.Button(cell.transform, FacturationSuivi.EtatLibelle(etat), EtatBg(etat), EtatFg(etat), 28, 13, false);
        UIFactory.Border(pill.gameObject, EtatFg(etat));
        UIFactory.LE(pill.gameObject, prefW: 96, flexW: 0, minH: 28);
        if (etat == FacturationSuivi.Etat.Cloture) { pill.interactable = false; return; }
        var lgn = l;
        pill.onClick.AddListener(() => OpenStatutMenu(lgn, (RectTransform)pill.transform));
    }

    void ActionsCell(Transform row, FactureEtat l, FacturationSuivi.Etat etat, float w)
    {
        var act = UIFactory.HBox(row, 6, false, "Actions");
        UIFactory.LE(act.gameObject, prefW: w, minW: w, flexW: 0);
        act.childAlignment = TextAnchor.MiddleLeft; act.childForceExpandWidth = false;
        if (etat == FacturationSuivi.Etat.Cloture) return;   // période reprise : aucune action
        string pdfAbs = FacturationSuivi.CheminPdf(l);
        bool genere = !string.IsNullOrEmpty(pdfAbs) && File.Exists(pdfAbs);
        if (genere)
        {
            MiniBtn(act.transform, "PDF", () => Application.OpenURL("file:///" + pdfAbs.Replace("\\", "/")));
            bool corrigeable = l.type == "Loyer"
                && (etat == FacturationSuivi.Etat.Envoye || etat == FacturationSuivi.Etat.Impaye);
            MiniBtn(act.transform, corrigeable ? "Corriger" : "Refaire",
                () => OuvrirGeneration(l.type, corrigeable ? l : null));
        }
        else MiniBtn(act.transform, "Générer", () => OuvrirGeneration(l.type, null));

        // Facture impayée → option « rappel d'échéance » (brouillon email manuel).
        if (etat == FacturationSuivi.Etat.Impaye)
            MiniBtn(act.transform, "Rappel", () => FactureRappelService.Demander(_loc, l, () =>
            {
                _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
                RebuildTable();
            }));

        // Loyer payé sur bail NON commercial → possibilité d'émettre la quittance de loyer.
        if (etat == FacturationSuivi.Etat.Paye && l.type == "Loyer"
            && !Locataire.EstBailCommercial(_loc.typeDeBail))
            MiniBtn(act.transform, "Quittance", () => FactureQuittanceService.Emettre(_fiche, _loc, l));
    }

    // ── Menu de changement d'état ───────────────────────────────────────────────

    void OpenStatutMenu(FactureEtat ligne, RectTransform anchor)
    {
        var root = (FindObjectOfType<Canvas>()?.rootCanvas.transform) ?? transform;
        var scrim = UIFactory.Rect("StatutScrim", root);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        var sBtn = scrim.gameObject.AddComponent<Button>();
        scrim.SetAsLastSibling();
        sBtn.onClick.AddListener(() => Destroy(scrim.gameObject));

        var listBg = UIFactory.Panel("StatutList", scrim, UITheme.Carte);
        UIFactory.Border(listBg.gameObject);
        var vlg = listBg.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2; vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true;
        listBg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var lrt = (RectTransform)listBg.transform;
        lrt.anchorMin = lrt.anchorMax = Vector2.zero;
        lrt.sizeDelta = new Vector2(190, 0);

        Action<string> set = statut =>
        {
            FacturationSuivi.SetStatut(_loc, ligne, statut);
            _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
            Destroy(scrim.gameObject);
            RebuildTable();
        };
        MenuItem(vlg.transform, "Payé", () => set("Paye"));
        MenuItem(vlg.transform, "Impayé", () => set("Impaye"));
        MenuItem(vlg.transform, "Envoyé", () => set("Envoye"));
        MenuItem(vlg.transform, "À faire", () => set("AFaire"));
        MenuItem(vlg.transform, "Automatique", () => set(""));

        // Placement intelligent : sous l'ancre, ou au-dessus s'il manque de place en bas.
        UIFactory.PlacePopup(lrt, anchor);
    }

    void MenuItem(Transform parent, string label, Action onClick)
    {
        var b = UIFactory.Button(parent, label, UITheme.Carte, UITheme.TextePrincipal, 34, 15, false);
        b.onClick.AddListener(() => onClick());
    }

    void OuvrirGeneration(string type, FactureEtat correctionTarget = null)
    {
        switch (type)
        {
            case "Loyer": FactureLoyerPanel.OpenLoyer(_fiche, correctionTarget); break;
            case "Regul": FactureRegulPanel.OpenRegul(_fiche); break;
            case "Refac": FactureRefacPanel.OpenRefac(_fiche); break;
            case "Depot": FactureDepotPanel.OpenDepot(_fiche); break;
        }
        Close();
    }

    // ── Helpers UI ──────────────────────────────────────────────────────────────

    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    static string Montant(float v) => v > 0f ? v.ToString("#,##0.00", Fr) + " €" : "—";

    void MiniBtn(Transform parent, string label, Action onClick)
    {
        var b = UIFactory.Button(parent, label, UITheme.Carte, UITheme.TextePrincipal, 30, 13, false);
        UIFactory.Border(b.gameObject); UIFactory.LE(b.gameObject, prefW: 74, flexW: 0, minH: 30);
        b.onClick.AddListener(() => onClick());
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

    static string Ech(string iso) => DateTime.TryParse(iso, out var d) ? d.ToString("dd/MM/yyyy") : "—";

    static Color EtatBg(FacturationSuivi.Etat e)
    {
        switch (e)
        {
            case FacturationSuivi.Etat.AVenir: return Hex("#F1EFE8");
            case FacturationSuivi.Etat.AFaire: return Hex("#FAEEDA");
            case FacturationSuivi.Etat.AttenteEnvoi: return Hex("#EDE8F6");
            case FacturationSuivi.Etat.Envoye: return Hex("#E6F1FB");
            case FacturationSuivi.Etat.Impaye: return Hex("#FCEBEB");
            case FacturationSuivi.Etat.Cloture: return Hex("#ECEAE3");
            default:                           return Hex("#E1F5EE"); // Payé
        }
    }

    static Color EtatFg(FacturationSuivi.Etat e)
    {
        switch (e)
        {
            case FacturationSuivi.Etat.AVenir: return Hex("#888780");
            case FacturationSuivi.Etat.AFaire: return Hex("#854F0B");
            case FacturationSuivi.Etat.AttenteEnvoi: return Hex("#6A5AA0");
            case FacturationSuivi.Etat.Envoye: return Hex("#185FA5");
            case FacturationSuivi.Etat.Impaye: return Hex("#A32D2D");
            case FacturationSuivi.Etat.Cloture: return Hex("#9B9A94");
            default:                           return Hex("#0F6E56"); // Payé
        }
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
