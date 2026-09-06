using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Écran « Charges » d'un bâtiment (à côté d'Achat / Travaux), construit 100 % par
/// code. Historique des charges + ajout/modif/suppr. Chaque charge : nom, coût,
/// date, locataire(s) concerné(s) (Tous ou sélection + répartition par ratio),
/// PDF de la facture (copié dans le dossier de sauvegarde), statut impayé/payé.
public class ChargePanel : MonoBehaviour
{
    public static ChargePanel Instance { get; private set; }

    static readonly Color Accent = Hex("#7A5AA6");      // violet — charges
    static readonly Color AccentClair = Hex("#ECE4F5");

    BatimentPrefab _bp;
    Transform _listContent;
    TMP_Text _titre;

    // ── État du formulaire en cours ────────────────────────────────────────────
    ChargeBatiment _edit;
    bool _isNew;
    Toggle _fTous;
    readonly Dictionary<string, Toggle> _fLoc = new Dictionary<string, Toggle>();
    readonly Dictionary<string, TMP_InputField> _fRatio = new Dictionary<string, TMP_InputField>();
    Transform _locBox, _ratioBox;
    string _fType = "Surface";
    string _fPdf;
    TMP_Text _fPdfLabel;
    Button _chipSurface, _chipEgal, _chipManuel;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        UIFactory.Stretch(rt);
        Build();
        gameObject.SetActive(false);
    }

    // ── Entrée publique ────────────────────────────────────────────────────────

    public static void OpenList(BatimentPrefab bp)
    {
        if (bp == null) return;
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("ChargePanel", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<ChargePanel>();
        }
        Instance._bp = bp;
        Instance.gameObject.SetActive(true);
        Instance.transform.SetAsLastSibling();
        Instance.RefreshTitre();
        Instance.RebuildList();
    }

    public void Close() => gameObject.SetActive(false);

    Batiment Bat => _bp != null ? _bp.getBatiment() : null;

    void RefreshTitre()
    {
        if (_titre == null) return;
        string nom = _bp != null ? _bp.getName() : "";
        _titre.text = string.IsNullOrEmpty(nom) ? "Charges" : $"Charges — {nom}";
    }

    // ── Construction de l'écran ────────────────────────────────────────────────

    void Build()
    {
        var bg = gameObject.AddComponent<Image>();
        bg.color = UITheme.Fond;

        var col = UIFactory.VBox(transform, 12, 24, 24, 14, 16, "Col");
        UIFactory.Stretch((RectTransform)col.transform);
        col.childForceExpandHeight = false;

        // Header
        var header = UIFactory.HBox(col.transform, 12, false, "Header");
        UIFactory.LE(header.gameObject, minH: 52);
        var back = UIFactory.Button(header.transform, "←  Retour", UITheme.Carte, UITheme.TextePrincipal, 42, 18);
        UIFactory.Border(back.gameObject);
        UIFactory.LE(back.gameObject, prefW: 140, flexW: 0);
        back.onClick.AddListener(Close);
        _titre = UIFactory.Text(header.transform, "Charges", 26, UITheme.TextePrincipal, true);
        UIFactory.LE(_titre.gameObject, flexW: 1);

        var add = UIFactory.Button(header.transform, "+  Ajouter une charge", Accent, Color.white, 42, 17);
        UIFactory.LE(add.gameObject, prefW: 240, flexW: 0);
        add.onClick.AddListener(() => OpenChargeForm(null));

        // Scroll liste
        _listContent = MakeScroll(col.transform);
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

        var content = UIFactory.VBox(viewport, 10, 2, 8, 2, 12, "Content");
        var crt = (RectTransform)content.transform;
        crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(.5f, 1);
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        sr.viewport = viewport; sr.content = crt;
        return content.transform;
    }

    // ── Liste des charges ──────────────────────────────────────────────────────

    void RebuildList()
    {
        if (_listContent == null || Bat == null) return;
        foreach (Transform c in _listContent) Destroy(c.gameObject);

        var charges = Bat.charges ?? new List<ChargeBatiment>();
        if (charges.Count == 0)
        {
            UIFactory.Text(_listContent, "Aucune charge. Cliquez sur « Ajouter une charge ».",
                17, UITheme.TexteSecondaire);
            return;
        }

        foreach (var ch in charges.OrderByDescending(c => c.dateISO ?? ""))
        {
            var ch2 = ch;
            var rowBg = UIFactory.Panel("ChargeRow", _listContent, UITheme.Carte);
            UIFactory.Border(rowBg.gameObject);
            var row = rowBg.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10; row.padding = new RectOffset(14, 14, 10, 10);
            row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;

            // Bloc infos
            var info = UIFactory.VBox(rowBg.transform, 2, 0, 0, 0, 0, "info");
            UIFactory.LE(info.gameObject, flexW: 1);
            string nom = string.IsNullOrWhiteSpace(ch.nom) ? "(charge sans nom)" : ch.nom;
            UIFactory.Text(info.transform, nom, 18, UITheme.TextePrincipal, true);
            string date = string.IsNullOrEmpty(ch.dateISO) ? "—"
                : (DateTime.TryParse(ch.dateISO, out var d) ? d.ToString("dd/MM/yyyy") : ch.dateISO);
            UIFactory.Text(info.transform, $"{date}  ·  {QuiConcerne(ch)}", 15, UITheme.TexteSecondaire);

            // Coût
            var cout = UIFactory.Text(rowBg.transform, $"{ch.cout:N2} €", 18, UITheme.TextePrincipal, true,
                TextAlignmentOptions.Right);
            UIFactory.LE(cout.gameObject, prefW: 130, flexW: 0);

            // Badge statut
            var badge = UIFactory.Panel("Badge", rowBg.transform, ch.paye ? UITheme.PrimaireClair : UITheme.AlerteClair);
            UIFactory.LE(badge.gameObject, prefW: 90, minH: 30, flexW: 0);
            var bTxt = UIFactory.Text(badge.transform, ch.paye ? "Payé" : "Impayé", 14,
                ch.paye ? UITheme.Primaire : UITheme.AlerteTexte, true, TextAlignmentOptions.Center);
            UIFactory.Stretch((RectTransform)bTxt.transform, 8, 0, 8, 0);

            // PDF (si présent)
            if (!string.IsNullOrEmpty(ch.pdfPath))
            {
                var pdf = UIFactory.Button(rowBg.transform, "PDF", UITheme.Carte, UITheme.TextePrincipal, 32, 14);
                UIFactory.Border(pdf.gameObject); UIFactory.LE(pdf.gameObject, prefW: 64, flexW: 0);
                pdf.interactable = File.Exists(ch.pdfPath);
                pdf.onClick.AddListener(() => { if (File.Exists(ch2.pdfPath)) Application.OpenURL("file:///" + ch2.pdfPath.Replace('\\', '/')); });
            }

            var edit = UIFactory.Button(rowBg.transform, "Modifier", UITheme.Carte, UITheme.TextePrincipal, 32, 14);
            UIFactory.Border(edit.gameObject); UIFactory.LE(edit.gameObject, prefW: 90, flexW: 0);
            edit.onClick.AddListener(() => OpenChargeForm(ch2));

            var del = UIFactory.Button(rowBg.transform, "Supprimer", UITheme.AlerteClair, UITheme.AlerteTexte, 32, 14);
            UIFactory.LE(del.gameObject, prefW: 100, flexW: 0);
            del.onClick.AddListener(() => ConfirmDialog.Instance?.Show(
                "Supprimer la charge", $"Supprimer « {nom} » ?",
                () =>
                {
                    Bat.charges.RemoveAll(x => x.id == ch2.id);
                    Persist();
                    UndoToast.Instance?.Show("Charge supprimée", () => { Bat.charges.Add(ch2); Persist(); });
                }, "Supprimer"));
        }
    }

    string QuiConcerne(ChargeBatiment ch)
    {
        if (ch.tousLocataires) return "Tous les locataires";
        var noms = (ch.locatairesConcernes ?? new List<string>())
            .Select(id => _bp.listLocataire.FirstOrDefault(l => l.id == id))
            .Where(l => l != null).Select(l => string.IsNullOrEmpty(l.Name) ? "?" : l.Name).ToList();
        if (noms.Count == 0) return "Aucun locataire";
        return string.Join(" · ", noms);
    }

    // ── Formulaire d'ajout / modification (modale) ─────────────────────────────

    void OpenChargeForm(ChargeBatiment existing)
    {
        _isNew = existing == null;
        _edit = existing != null ? Clone(existing) : new ChargeBatiment { dateISO = DateTime.Today.ToString("yyyy-MM-dd") };
        _fType = string.IsNullOrEmpty(_edit.typeRatio) ? "Surface" : _edit.typeRatio;
        _fPdf = _edit.pdfPath;
        _fLoc.Clear(); _fRatio.Clear();

        var scrim = UIFactory.Rect("ChargeScrim", transform);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        scrim.SetAsLastSibling();

        // Carte avec scroll interne (hauteur bornée)
        var cardImg = UIFactory.Panel("ChargeCard", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(620, 640);
        var cv = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.spacing = 10; cv.padding = new RectOffset(18, 18, 16, 16);
        cv.childControlWidth = true; cv.childControlHeight = true;
        cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;

        UIFactory.Text(cv.transform, _isNew ? "Nouvelle charge" : "Modifier la charge", 22, UITheme.TextePrincipal, true);

        var body = MakeScroll(cv.transform);   // corps défilable
        UIFactory.LE(((Transform)body.parent.parent).gameObject, flexH: 1);

        var fNom = LabeledInput(body, "Nom de la charge", "Taxe foncière 2026…", _edit.nom);
        var fCout = LabeledInput(body, "Coût (€)", "0", _edit.cout > 0 ? _edit.cout.ToString(CultureInfo.InvariantCulture) : "");
        fCout.contentType = TMP_InputField.ContentType.DecimalNumber;
        var fDate = LabeledInput(body, "Date", "JJ/MM/AAAA",
            DateTime.TryParse(_edit.dateISO, out var de) ? de.ToString("dd/MM/yyyy") : "");

        // Locataires concernés
        UIFactory.Text(body, "Locataire(s) concerné(s)", 17, UITheme.TexteSecondaire);
        _fTous = UIFactory.Toggle(body, "Tous les locataires", _edit.tousLocataires);
        _fTous.onValueChanged.AddListener(_ => { RefreshLocBox(); RebuildRatio(); });

        var locWrap = UIFactory.VBox(body, 4, 0, 0, 0, 0, "LocBox");
        _locBox = locWrap.transform;

        // Répartition (ratios)
        var ratioWrap = UIFactory.VBox(body, 6, 0, 0, 0, 0, "RatioBox");
        _ratioBox = ratioWrap.transform;

        // PDF
        UIFactory.Text(body, "PDF de la facture", 17, UITheme.TexteSecondaire);
        var pdfRow = UIFactory.HBox(body, 8, false, "PdfRow");
        var pick = UIFactory.Button(pdfRow.transform, "Choisir un PDF…", AccentClair, Accent, 38, 15);
        UIFactory.LE(pick.gameObject, prefW: 180, flexW: 0);
        pick.onClick.AddListener(ChoisirPdf);
        _fPdfLabel = UIFactory.Text(pdfRow.transform, PdfLabel(), 15, UITheme.TexteSecondaire);
        UIFactory.LE(_fPdfLabel.gameObject, flexW: 1);
        var clearPdf = UIFactory.Button(pdfRow.transform, "Retirer", UITheme.Carte, UITheme.TexteSecondaire, 34, 14);
        UIFactory.Border(clearPdf.gameObject); UIFactory.LE(clearPdf.gameObject, prefW: 90, flexW: 0);
        clearPdf.onClick.AddListener(() => { _fPdf = null; if (_fPdfLabel != null) _fPdfLabel.text = PdfLabel(); });

        // Statut
        var fPaye = UIFactory.Toggle(body, "Payé (sinon : impayé)", _edit.paye);

        RefreshLocBox();
        RebuildRatio();

        // Actions
        var actions = UIFactory.HBox(cv.transform, 10, false, "Actions");
        UIFactory.LE(actions.gameObject, minH: 46, prefH: 46);
        var cancel = UIFactory.Button(actions.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 44, 18);
        UIFactory.Border(cancel.gameObject); UIFactory.LE(cancel.gameObject, flexW: 1);
        cancel.onClick.AddListener(() => Destroy(scrim.gameObject));
        var ok = UIFactory.Button(actions.transform, "Enregistrer", Accent, Color.white, 44, 18);
        UIFactory.LE(ok.gameObject, flexW: 1);
        ok.onClick.AddListener(() =>
        {
            SaveForm(fNom.text, fCout.text, fDate.text, fPaye.isOn);
            Destroy(scrim.gameObject);
        });
    }

    // Cases à cocher par locataire (visibles seulement si « Tous » décoché).
    void RefreshLocBox()
    {
        foreach (Transform c in _locBox) Destroy(c.gameObject);
        _fLoc.Clear();
        bool show = !_fTous.isOn;
        _locBox.gameObject.SetActive(show);
        if (!show) return;

        foreach (var l in _bp.listLocataire)
        {
            string nom = string.IsNullOrEmpty(l.Name) ? $"Lot {l.lotBatiment}" : l.Name;
            bool on = _edit.locatairesConcernes != null && _edit.locatairesConcernes.Contains(l.id);
            var t = UIFactory.Toggle(_locBox, nom, on);
            t.onValueChanged.AddListener(_ => RebuildRatio());
            _fLoc[l.id] = t;
        }
    }

    // Locataires réellement concernés selon l'état courant du formulaire.
    List<Locataire> Concerned()
    {
        if (_fTous.isOn) return _bp.listLocataire.ToList();
        return _bp.listLocataire.Where(l => _fLoc.TryGetValue(l.id, out var t) && t.isOn).ToList();
    }

    // Section « répartition » : visible seulement si > 1 locataire concerné.
    void RebuildRatio()
    {
        foreach (Transform c in _ratioBox) Destroy(c.gameObject);
        _fRatio.Clear();
        var concerned = Concerned();
        bool show = concerned.Count > 1;
        _ratioBox.gameObject.SetActive(show);
        if (!show) return;

        UIFactory.Text(_ratioBox, "Répartition entre locataires", 17, UITheme.TexteSecondaire);

        // Choix du type de ratio (3 chips)
        var chips = UIFactory.HBox(_ratioBox, 6, false, "TypeChips");
        _chipSurface = TypeChip(chips.transform, "Surface");
        _chipEgal = TypeChip(chips.transform, "Égalité");
        _chipManuel = TypeChip(chips.transform, "Manuel");
        StyleChips();

        // Une ligne par locataire concerné : nom + poids (part)
        foreach (var l in concerned)
        {
            var row = UIFactory.HBox(_ratioBox, 8, false, "RatioRow");
            UIFactory.LE(row.gameObject, minH: 40);
            var nom = UIFactory.Text(row.transform, string.IsNullOrEmpty(l.Name) ? $"Lot {l.lotBatiment}" : l.Name,
                16, UITheme.TextePrincipal);
            UIFactory.LE(nom.gameObject, flexW: 1);
            var inp = UIFactory.Input(row.transform, "0", 38);
            inp.contentType = TMP_InputField.ContentType.DecimalNumber;
            UIFactory.LE(inp.gameObject, prefW: 120, flexW: 0);
            inp.text = DefaultPart(l).ToString("0.##", CultureInfo.InvariantCulture);
            _fRatio[l.id] = inp;
        }
    }

    Button TypeChip(Transform parent, string type)
    {
        var b = UIFactory.Button(parent, type, UITheme.Carte, UITheme.TextePrincipal, 34, 15, false);
        UIFactory.Border(b.gameObject); UIFactory.LE(b.gameObject, prefW: 120, flexW: 0);
        b.onClick.AddListener(() => { _fType = type; StyleChips(); RefillRatios(); });
        return b;
    }

    void StyleChips()
    {
        StyleChip(_chipSurface, _fType == "Surface");
        StyleChip(_chipEgal, _fType == "Égalité");
        StyleChip(_chipManuel, _fType == "Manuel");
    }

    static void StyleChip(Button b, bool active)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>(); if (img != null) img.color = active ? Accent : UITheme.Carte;
        var t = b.GetComponentInChildren<TMP_Text>(true); if (t != null) t.color = active ? Color.white : UITheme.TextePrincipal;
    }

    // Recalcule les poids affichés selon le type choisi (sauf « Manuel » : on garde).
    void RefillRatios()
    {
        if (_fType == "Manuel") return;
        foreach (var l in Concerned())
            if (_fRatio.TryGetValue(l.id, out var inp))
                inp.text = DefaultPart(l).ToString("0.##", CultureInfo.InvariantCulture);
    }

    float DefaultPart(Locataire l)
    {
        switch (_fType)
        {
            case "Surface": return l.tailleLot > 0f ? l.tailleLot : 1f;
            case "Égalité": return 1f;
            default: // Manuel : reprend la valeur existante si dispo, sinon 1
                var r = _edit.ratios?.FirstOrDefault(x => x.locataireId == l.id);
                return r != null && r.part > 0f ? r.part : 1f;
        }
    }

    // ── PDF ────────────────────────────────────────────────────────────────────

    void ChoisirPdf()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        var paths = SFB.StandaloneFileBrowser.OpenFilePanel(
            "Choisir la facture de charge", "",
            new[] { new SFB.ExtensionFilter("Documents", "pdf", "jpg", "jpeg", "png") }, false);
        if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            _fPdf = paths[0];
            if (_fPdfLabel != null) _fPdfLabel.text = PdfLabel();
        }
#endif
    }

    string PdfLabel() => string.IsNullOrEmpty(_fPdf) ? "Aucun fichier" : Path.GetFileName(_fPdf);

    // ── Sauvegarde du formulaire ───────────────────────────────────────────────

    void SaveForm(string nom, string coutTxt, string dateTxt, bool paye)
    {
        _edit.nom = nom?.Trim();
        _edit.cout = ParseFloat(coutTxt);
        _edit.dateISO = DateTime.TryParse(dateTxt, out var dt) ? dt.ToString("yyyy-MM-dd") : _edit.dateISO;
        _edit.paye = paye;

        _edit.tousLocataires = _fTous.isOn;
        var concerned = Concerned();
        _edit.locatairesConcernes = _fTous.isOn ? new List<string>() : concerned.Select(l => l.id).ToList();
        _edit.typeRatio = _fType;
        _edit.ratios = new List<ChargeRatio>();
        if (concerned.Count > 1)
            foreach (var l in concerned)
                _edit.ratios.Add(new ChargeRatio(l.id,
                    _fRatio.TryGetValue(l.id, out var inp) ? ParseFloat(inp.text) : 0f));

        // Copie du PDF dans le dossier de sauvegarde (comme le bail / les photos).
        if (!string.IsNullOrEmpty(_fPdf) && _fPdf != _edit.pdfPath && File.Exists(_fPdf))
            _edit.pdfPath = CopyPdf(_fPdf);
        else if (string.IsNullOrEmpty(_fPdf))
            _edit.pdfPath = "";

        var list = Bat.charges;
        int idx = list.FindIndex(c => c.id == _edit.id);
        if (idx >= 0) list[idx] = _edit; else list.Add(_edit);

        Persist();
    }

    string CopyPdf(string src)
    {
        string dossier = Path.Combine(SaveLocationService.GetSaveRoot(), "Batiment", _bp.getID(), "Charge");
        Directory.CreateDirectory(dossier);
        string bn = Path.GetFileNameWithoutExtension(src), ext = Path.GetExtension(src);
        string dest = Path.Combine(dossier, bn + ext);
        int i = 1;
        while (File.Exists(dest)) dest = Path.Combine(dossier, $"{bn}_{i++}{ext}");
        File.Copy(src, dest, false);
        return dest;
    }

    void Persist()
    {
        if (BatimentManager.Instance != null && Bat != null)
            BatimentManager.Instance.SaveBatiment(Bat);
        RebuildList();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    TMP_InputField LabeledInput(Transform parent, string label, string placeholder, string value)
    {
        UIFactory.Text(parent, label, 17, UITheme.TexteSecondaire);
        var f = UIFactory.Input(parent, placeholder);
        f.text = value ?? "";
        return f;
    }

    static float ParseFloat(string s)
    {
        float.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
        return v;
    }

    static ChargeBatiment Clone(ChargeBatiment c) => JsonUtility.FromJson<ChargeBatiment>(JsonUtility.ToJson(c));
    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
