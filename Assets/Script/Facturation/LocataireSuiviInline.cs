using System;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Suivi de facturation d'UN locataire, intégré en bandeau pleine largeur dans la
/// fiche (option A). Reprend la logique de FacturationSuiviPanel (sélecteur d'année,
/// tableau des factures avec état À venir/À faire/Envoyé/Impayé/Payé + actions) mais
/// sans le décor plein écran : la GameObject devient une carte titrée.
public class LocataireSuiviInline : MonoBehaviour
{
    LocatairePrefab _fiche; Locataire _loc;
    int _year;
    Transform _tableBox, _yearRow;
    bool _built;

    static readonly Color Accent = Hex("#9A5B2E");
    static readonly Color AccentClair = Hex("#F3E7D9");
    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    Transform _extBody, _extTitre;

    public void Setup(LocatairePrefab fiche) => Setup(fiche, null, null);

    // Coquille externe : `extBody` = Content (tableau) ; `extTitre` = bandeau de
    // titre cloné (sélecteur d'année ajouté à droite). Si null → chrome interne.
    public void Setup(LocatairePrefab fiche, Transform extBody, Transform extTitre)
    {
        _fiche = fiche;
        _extBody = extBody; _extTitre = extTitre;
        _loc = fiche != null ? fiche.GetLocataire() : null;
        if (_loc == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);
        if (!_built) Build();
        _year = DateTime.Today.Year;
        RebuildYears();
        RebuildTable();
    }

    // Rafraîchit le tableau sans reconstruire le décor (après génération/modif d'une
    // facture, pour que le Suivi se mette à jour tout de suite).
    public void Refresh()
    {
        if (!_built || _loc == null) return;
        RebuildYears();
        RebuildTable();
    }

    // Rafraîchit le Suivi de la fiche concernée (appelé par les panneaux de facture).
    public static void RefreshFor(LocatairePrefab fiche)
    {
        if (fiche == null) return;
        foreach (var s in Resources.FindObjectsOfTypeAll<LocataireSuiviInline>())
            if (s != null && s._fiche == fiche) s.Refresh();
    }

    // ── Construction (la GameObject devient la carte) ───────────────────────────

    void Build()
    {
        _built = true;

        // Mode coquille externe (clone de section de scène) : on ne crée pas le
        // décor, on ajoute juste le sélecteur d'année dans le bandeau et le tableau
        // dans le corps fournis.
        if (_extBody != null)
        {
            if (_extTitre != null)
            {
                // Le HLG du bandeau cloné étire ses enfants en hauteur → les boutons
                // d'année rempliraient les 58px. On désactive l'étirement (les enfants
                // gardent leur hauteur, centrée verticalement).
                var hlg = _extTitre.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (hlg != null) { hlg.childForceExpandHeight = false; hlg.childControlHeight = true; hlg.childAlignment = TextAnchor.MiddleLeft; }
                var sp = UIFactory.Rect("Spacer", _extTitre);
                UIFactory.LE(sp.gameObject, flexW: 1);
                UIFactory.Text(_extTitre, "Année", 14, UITheme.TexteSecondaire);
                _yearRow = _extTitre;
            }
            var rows0 = UIFactory.VBox(_extBody, 6, 14, 14, 12, 14, "Rows");
            _tableBox = rows0.transform;
            return;
        }

        var img = gameObject.AddComponent<Image>();
        img.color = UITheme.Carte; img.sprite = UIFactory.Rounded(); img.type = Image.Type.Sliced;
        UIFactory.Border(gameObject);
        var v = gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(0, 0, 0, 0); v.spacing = 0;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        // Bande de titre : titre à gauche, sélecteur d'année à droite.
        var band = UIFactory.Panel("TitleBand", transform, AccentClair);
        UIFactory.LE(band.gameObject, minH: 44, prefH: 44, flexH: 0);
        var bh = band.gameObject.AddComponent<HorizontalLayoutGroup>();
        bh.padding = new RectOffset(16, 16, 4, 4); bh.spacing = 8;
        bh.childControlWidth = true; bh.childControlHeight = true;
        bh.childForceExpandWidth = false; bh.childForceExpandHeight = true;
        bh.childAlignment = TextAnchor.MiddleLeft;
        UIFactory.Text(bh.transform, "Suivi de facturation", 26, Accent, true);
        var spacer = UIFactory.Rect("Spacer", bh.transform);
        UIFactory.LE(spacer.gameObject, flexW: 1);
        UIFactory.Text(bh.transform, "Année", 14, UITheme.TexteSecondaire);
        _yearRow = bh.transform;   // les boutons année sont ajoutés à la suite (à droite)

        // Corps : le tableau.
        var body = UIFactory.VBox(transform, 8, 14, 14, 12, 14, "Body");
        var tbl = UIFactory.VBox(body.transform, 6, 0, 0, 0, 0, "Rows");
        _tableBox = tbl.transform;
    }

    void RebuildYears()
    {
        foreach (Transform c in _yearRow) if (c.GetComponent<Button>() != null) Destroy(c.gameObject);
        // Chip modèle = un bouton de filtre des « améliorations » (même sprite/forme).
        var src = (_fiche != null && _fiche.objectivesManager != null) ? _fiche.objectivesManager.filterAllButton : null;
        int cur = DateTime.Today.Year;
        for (int y = cur - 3; y <= cur + 1; y++)
        {
            int yy = y;
            bool on = y == _year;
            Button b;
            GameObject chip = null;
            if (src != null)
            {
                var go = Instantiate(src.gameObject, _yearRow);
                go.name = "Year" + y;
                go.SetActive(true);
                chip = go;
                b = go.GetComponent<Button>();
                var lbl = go.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) { lbl.text = y.ToString(); lbl.color = on ? Color.white : Hex("#2C2C2A"); }
                var im = go.GetComponent<Image>();
                if (im != null) im.color = on ? Hex("#A9741C") : Hex("#E6E3DA");
                var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                le.minWidth = 62; le.preferredWidth = 62; le.flexibleWidth = 0;
                le.minHeight = 34; le.preferredHeight = 34; le.flexibleHeight = 0;
                var rt = (RectTransform)go.transform; rt.localScale = Vector3.one; rt.sizeDelta = new Vector2(62, 34);
            }
            else
            {
                b = UIFactory.Button(_yearRow, y.ToString(), on ? Hex("#A9741C") : Hex("#E6E3DA"),
                    on ? Color.white : Hex("#2C2C2A"), 34, 16, false);
                UIFactory.LE(b.gameObject, prefW: 62, minW: 62, flexW: 0, minH: 34, prefH: 34, flexH: 0);
                chip = b.gameObject;
            }
            if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(() => { _year = yy; RebuildYears(); RebuildTable(); }); }
            // Pastille si l'année contient une facturation à faire / en attente / impayée.
            var pc = YearAlerteColor(yy);
            if (chip != null && pc.HasValue) AjouteYearPastille(chip, pc.Value);
        }
    }

    // Rouge si un impayé, ambre s'il y a du « à faire » / « en attente d'envoi »,
    // sinon rien (année sans action). Pastille posée en haut-droite du chip.
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
        if (impaye) return Hex("#D85A30");
        if (autre) return Hex("#A9741C");
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

    // ── Tableau ─────────────────────────────────────────────────────────────────

    const float WEch = 130, WMontant = 140, WEtat = 132, WActions = 280;

    void RebuildTable()
    {
        if (_tableBox == null || _loc == null) return;
        foreach (Transform c in _tableBox) Destroy(c.gameObject);

        var lignes = FacturationSuivi.Lignes(_loc, _year);

        var card = UIFactory.Panel("Card", _tableBox, UITheme.Carte);
        UIFactory.Border(card.gameObject);
        var cv = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.spacing = 0; cv.padding = new RectOffset(0, 0, 0, 0);
        cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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

    void FactCell(Transform row, string text, Color color, bool bold)
    {
        var t = UIFactory.Text(row, text, 14, color, bold, TextAlignmentOptions.Left);
        t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(t.gameObject, flexW: 1, minW: 200);
    }

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
        // Période clôturée (reprise) : pastille statique, non actionnable.
        if (etat == FacturationSuivi.Etat.Cloture) { pill.interactable = false; return; }
        var lgn = l;
        pill.onClick.AddListener(() => OpenStatutMenu(lgn, (RectTransform)pill.transform));
    }

    void ActionsCell(Transform row, FactureEtat l, FacturationSuivi.Etat etat, float w)
    {
        var act = UIFactory.HBox(row, 6, false, "Actions");
        UIFactory.LE(act.gameObject, prefW: w, minW: w, flexW: 0);
        act.childAlignment = TextAnchor.MiddleLeft; act.childForceExpandWidth = false;
        // Période clôturée (reprise) : aucune action (historique).
        if (etat == FacturationSuivi.Etat.Cloture) return;
        string pdfAbs = FacturationSuivi.CheminPdf(l);
        bool genere = !string.IsNullOrEmpty(pdfAbs) && File.Exists(pdfAbs);
        if (genere)
        {
            MiniBtn(act.transform, "PDF", () => Application.OpenURL("file:///" + pdfAbs.Replace("\\", "/")));
            // Facture déjà émise → « Corriger » (crée une version corrigée, même numéro) ;
            // sinon « Refaire » (regénère). Correction câblée pour le loyer.
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
        // L'état « Payé » peut être forcé à la main sur une ligne JAMAIS émise : sans le
        // contrôle numéro + PDF, on éditait une quittance (un reçu) pour une facture
        // qui n'existe pas.
        bool reellementEmise = !string.IsNullOrEmpty(l.numero) && !string.IsNullOrEmpty(l.pdfPath);
        if (etat == FacturationSuivi.Etat.Paye && l.type == "Loyer"
            && !Locataire.EstBailCommercial(_loc.typeDeBail)
            && reellementEmise)
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
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    void MiniBtn(Transform parent, string label, Action onClick)
    {
        var b = UIFactory.Button(parent, label, UITheme.Carte, UITheme.TextePrincipal, 30, 13, false);
        UIFactory.Border(b.gameObject); UIFactory.LE(b.gameObject, prefW: 74, flexW: 0, minH: 30);
        b.onClick.AddListener(() => onClick());
    }

    static string Montant(float v) => v > 0f ? v.ToString("#,##0.00", Fr) + " €" : "—";
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
            default:                           return Hex("#E1F5EE");
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
            default:                           return Hex("#0F6E56");
        }
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
