using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Formulaire d'ajout / modification d'une charge de bâtiment (modale, construite
/// par code). La LISTE (historique) est rendue par InvestissementListPanel, au même
/// visuel qu'Achat / Travaux ; ce composant ne fournit que le formulaire.
/// Champs : nom, coût, date, locataire(s) concerné(s) (Tous ou sélection +
/// répartition par ratio), PDF de la facture, statut payé/impayé.
public class ChargePanel : MonoBehaviour
{
    public static ChargePanel Instance { get; private set; }

    static readonly Color Accent = Hex("#7A5AA6");      // violet — charges
    static readonly Color AccentClair = Hex("#ECE4F5");

    BatimentPrefab _bp;
    Action _onSaved;

    // ── État du formulaire en cours ────────────────────────────────────────────
    ChargeBatiment _edit;
    bool _isNew;
    Toggle _fTous;
    readonly Dictionary<string, Toggle> _fLoc = new Dictionary<string, Toggle>();
    readonly Dictionary<string, TMP_InputField> _fRatio = new Dictionary<string, TMP_InputField>();
    readonly List<TMP_InputField> _manualEditable = new List<TMP_InputField>();
    TMP_InputField _manualLast;      // dernier locataire (Manuel) = 1 − somme des autres
    Transform _locBox, _ratioBox;
    string _fType = "Surface";
    string _fPdf;
    TMP_Text _fPdfLabel;
    Button _chipSurface, _chipEgal, _chipManuel;

    Batiment Bat => _bp != null ? _bp.getBatiment() : null;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        gameObject.SetActive(false);
    }

    // ── Entrée publique ────────────────────────────────────────────────────────

    /// Ouvre le formulaire (ajout si existing == null). `onSaved` est invoqué après
    /// écriture de la charge dans le bâtiment (le caller persiste + rafraîchit).
    public static void OpenForm(BatimentPrefab bp, ChargeBatiment existing, Action onSaved)
    {
        if (bp == null) return;
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("ChargeFormHost", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<ChargePanel>();
        }
        Instance._bp = bp;
        Instance._onSaved = onSaved;
        Instance.OpenChargeForm(existing);
    }

    // ── Formulaire d'ajout / modification (modale) ─────────────────────────────

    void OpenChargeForm(ChargeBatiment existing)
    {
        _isNew = existing == null;
        _edit = existing != null ? Clone(existing) : new ChargeBatiment { dateISO = DateTime.Today.ToString("yyyy-MM-dd") };
        _fType = string.IsNullOrEmpty(_edit.typeRatio) ? "Surface" : _edit.typeRatio;
        _fPdf = DossiersDonnees.VersAbsolu(_edit.pdfPath);
        _fLoc.Clear(); _fRatio.Clear();

        var root = (FindObjectOfType<Canvas>()?.rootCanvas.transform) ?? transform;
        var scrim = UIFactory.Rect("ChargeScrim", root);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        scrim.SetAsLastSibling();

        // Carte avec scroll interne (hauteur bornée)
        var cardImg = UIFactory.Panel("ChargeCard", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(660, 860);
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

    List<Locataire> Concerned()
    {
        if (_fTous.isOn) return _bp.listLocataire.ToList();
        return _bp.listLocataire.Where(l => _fLoc.TryGetValue(l.id, out var t) && t.isOn).ToList();
    }

    // Section « répartition » : visible seulement si > 1 locataire concerné.
    // Surface  → surface de chaque lot (lecture seule).
    // Égalité  → 1/N (lecture seule).
    // Manuel   → fractions modifiables ; le dernier locataire = 1 − somme des autres.
    void RebuildRatio()
    {
        foreach (Transform c in _ratioBox) Destroy(c.gameObject);
        _fRatio.Clear(); _manualEditable.Clear(); _manualLast = null;
        var concerned = Concerned();
        bool show = concerned.Count > 1;
        _ratioBox.gameObject.SetActive(show);
        if (!show) return;

        UIFactory.Text(_ratioBox, "Répartition entre locataires", 17, UITheme.TexteSecondaire);

        var chips = UIFactory.HBox(_ratioBox, 6, false, "TypeChips");
        _chipSurface = TypeChip(chips.transform, "Surface");
        _chipEgal = TypeChip(chips.transform, "Égalité");
        _chipManuel = TypeChip(chips.transform, "Manuel");
        StyleChips();

        int n = concerned.Count;
        var manualInit = InitialManual(concerned);

        for (int i = 0; i < n; i++)
        {
            var l = concerned[i];
            var row = UIFactory.HBox(_ratioBox, 8, false, "RatioRow");
            UIFactory.LE(row.gameObject, minH: 40);
            var nom = UIFactory.Text(row.transform,
                string.IsNullOrEmpty(l.Name) ? $"Lot {l.lotBatiment}" : l.Name, 16, UITheme.TextePrincipal);
            UIFactory.LE(nom.gameObject, flexW: 1);

            if (_fType == "Manuel")
            {
                bool isLast = i == n - 1;
                var inp = UIFactory.Input(row.transform, "0", 38);
                inp.contentType = TMP_InputField.ContentType.DecimalNumber;
                UIFactory.LE(inp.gameObject, prefW: 120, flexW: 0);
                inp.text = manualInit[i].ToString("0.###", CultureInfo.InvariantCulture);
                inp.interactable = !isLast;         // dernier = reste (auto)
                _fRatio[l.id] = inp;
                if (isLast) _manualLast = inp;
                else { _manualEditable.Add(inp); inp.onValueChanged.AddListener(_ => RecomputeManualLast()); }
            }
            else
            {
                string txt = _fType == "Surface"
                    ? $"{(l.tailleLot > 0f ? l.tailleLot : 0f):0.##} m²"
                    : $"1/{n}";
                var val = UIFactory.Text(row.transform, txt, 16, UITheme.TextePrincipal, true,
                    TextAlignmentOptions.Right);
                UIFactory.LE(val.gameObject, prefW: 120, flexW: 0);
            }
        }
        if (_fType == "Manuel") RecomputeManualLast();
    }

    // Le dernier champ (Manuel) absorbe le reste pour que le total fasse 1.
    void RecomputeManualLast()
    {
        if (_manualLast == null) return;
        float sum = 0f;
        foreach (var inp in _manualEditable) sum += ParseFloat(inp.text);
        float rest = Mathf.Clamp(1f - sum, 0f, 1f);
        _manualLast.SetTextWithoutNotify(rest.ToString("0.###", CultureInfo.InvariantCulture));
    }

    // Valeurs initiales Manuel (fractions sommant à 1) : reprend les ratios existants
    // normalisés, sinon répartit à parts égales.
    List<float> InitialManual(List<Locataire> concerned)
    {
        int n = concerned.Count;
        var res = new List<float>();
        float total = 0f;
        var existing = new List<float>();
        foreach (var l in concerned)
        {
            var r = _edit.ratios?.FirstOrDefault(x => x.locataireId == l.id);
            float v = r != null ? r.part : 0f;
            existing.Add(v); total += v;
        }
        if (total > 0f) for (int i = 0; i < n; i++) res.Add(existing[i] / total);
        else for (int i = 0; i < n; i++) res.Add(1f / n);
        return res;
    }

    Button TypeChip(Transform parent, string type)
    {
        var b = UIFactory.Button(parent, type, UITheme.Carte, UITheme.TextePrincipal, 34, 15, false);
        UIFactory.Border(b.gameObject); UIFactory.LE(b.gameObject, prefW: 120, flexW: 0);
        b.onClick.AddListener(() => { _fType = type; RebuildRatio(); });
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
        _edit.dateISO = TryParseFr(dateTxt, out var dt) ? dt.ToString("yyyy-MM-dd") : _edit.dateISO;
        _edit.paye = paye;

        _edit.tousLocataires = _fTous.isOn;
        var concerned = Concerned();
        _edit.locatairesConcernes = _fTous.isOn ? new List<string>() : concerned.Select(l => l.id).ToList();
        _edit.typeRatio = _fType;
        _edit.ratios = new List<ChargeRatio>();
        if (concerned.Count > 1)
            foreach (var l in concerned)
            {
                float part;
                if (_fType == "Surface") part = l.tailleLot > 0f ? l.tailleLot : 1f;
                else if (_fType == "Égalité") part = 1f;
                else part = _fRatio.TryGetValue(l.id, out var inp) ? ParseFloat(inp.text) : 0f; // Manuel (dernier = reste déjà calculé)
                _edit.ratios.Add(new ChargeRatio(l.id, part));
            }

        // Copie du PDF dans le dossier de sauvegarde (comme le bail / les photos).
        // Comparaison entre chemins ABSOLUS : _edit.pdfPath est desormais relatif,
        // le comparer tel quel aurait recopie le PDF a chaque enregistrement.
        if (!string.IsNullOrEmpty(_fPdf) && _fPdf != DossiersDonnees.VersAbsolu(_edit.pdfPath)
            && File.Exists(_fPdf))
            _edit.pdfPath = DossiersDonnees.VersRelatif(CopyPdf(_fPdf));
        else if (string.IsNullOrEmpty(_fPdf))
            _edit.pdfPath = "";

        if (Bat.charges == null) Bat.charges = new List<ChargeBatiment>();
        int idx = Bat.charges.FindIndex(c => c.id == _edit.id);
        if (idx >= 0) Bat.charges[idx] = _edit; else Bat.charges.Add(_edit);

        _onSaved?.Invoke();
    }

    string CopyPdf(string src)
    {
        // Dossier du batiment par son NOM (via DossiersDonnees), comme les factures
        // et les photos : les justificatifs atterrissaient dans un dossier nomme par
        // GUID, invisible pour qui ouvre le dossier lisible du batiment.
        string dossier = DossiersDonnees.DossierCharges(_bp.getName());
        Directory.CreateDirectory(dossier);
        string bn = Path.GetFileNameWithoutExtension(src), ext = Path.GetExtension(src);
        string dest = Path.Combine(dossier, bn + ext);
        int i = 1;
        while (File.Exists(dest)) dest = Path.Combine(dossier, $"{bn}_{i++}{ext}");
        File.Copy(src, dest, false);
        return dest;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

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

    TMP_InputField LabeledInput(Transform parent, string label, string placeholder, string value)
    {
        UIFactory.Text(parent, label, 17, UITheme.TexteSecondaire);
        var f = UIFactory.Input(parent, placeholder);
        f.text = value ?? "";
        return f;
    }

    // Passe par SaisieNumerique : « . » et « , » y sont interchangeables et les
    // espaces de milliers acceptes (cette copie locale ne gerait que la virgule).
    static float ParseFloat(string s) => SaisieNumerique.Parse(s);

    static ChargeBatiment Clone(ChargeBatiment c) => JsonUtility.FromJson<ChargeBatiment>(JsonUtility.ToJson(c));
    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // Parse une date saisie au format français JJ/MM/AAAA (sans ambiguïté mois/jour).
    static bool TryParseFr(string s, out DateTime d) =>
        DateTime.TryParseExact((s ?? "").Trim(),
            new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
}
