using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Écran « Information Facture — Régularisation des charges ».
/// Récapitule la quote-part du locataire sur les charges IMPAYÉES d'une année,
/// la compare aux provisions déjà versées (provision × nb de périodes) et facture
/// la différence (TVA 20 %). L'aperçu réel est rendu à droite (image), et
/// « Sauvegarder et envoyer » produit le PDF, passe les charges concernées en
/// « payé » et avance le numéro. AUCUN envoi réel (Pennylane/email) ici.
public class FactureRegulPanel : MonoBehaviour
{
    public static FactureRegulPanel Instance { get; private set; }

    static readonly Color CoVert = UITheme.Primaire, CoVertL = UITheme.PrimaireClair;
    static readonly Color CoBleu = Hex("#2C3E5E"), CoBleuL = Hex("#DEE3EB");
    static readonly Color CoViolet = Hex("#5B3E7A"), CoVioletL = Hex("#E9E0F2");
    static readonly Color CoTaupe = Hex("#5F5E5A"), CoTaupeL = Hex("#E9E6DE");

    static readonly List<string> NumFmtLabels = new List<string> { "Année / Numéro", "Année / Mois-Numéro", "Année / JourMois-Numéro" };
    static readonly List<string> NumFmtIds = new List<string> { "AN", "AMN", "AJMN" };

    LocatairePrefab _fiche; Locataire _loc; Batiment _bat;

    TMP_Text _titre, _entetePreview, _modeInfo, _previewHint;
    TMP_Text _tCharges, _tProvisions, _tSolde, _tTVA, _tTTC;
    TMP_InputField _nom, _adresse, _siret, _date, _echeance, _numeroId, _refInterne, _sommePhrase, _provisions, _emailEnvoi;
    TMP_Text _numeroPrefixe;
    string _autoSomme;   // dernière phrase de règlement auto (suivie tant que non personnalisée)
    UIDropdown _ribDD, _enteteDD, _numeroFormatDD, _anneeDD;
    Toggle _tvaDebit, _retard, _envoiEmail;
    Transform _chargesBox;

    RawImage _previewImg;
    FacturePreviewViewer _viewer;
    Texture2D _previewTex;

    ReglageData R => ReglageService.Current;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        UIFactory.Stretch(rt);
        Build();
        gameObject.SetActive(false);
    }

    public static void OpenRegul(LocatairePrefab fiche)
    {
        if (fiche == null) return;
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("FactureRegulPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<FactureRegulPanel>();
        }
        Instance.OpenFor(fiche);
    }

    void OpenFor(LocatairePrefab fiche)
    {
        _fiche = fiche;
        _loc = fiche.GetLocataire();
        _bat = fiche.batimentPrefabOrigin != null ? fiche.batimentPrefabOrigin.getBatiment() : null;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ResetPreview();   // pas d'aperçu du locataire précédent
        LoadIntoUI();
    }

    // Remet l'aperçu à zéro (évite d'afficher la facture d'un autre locataire).
    void ResetPreview()
    {
        if (_previewImg != null) { _previewImg.texture = null; _previewImg.color = new Color(1, 1, 1, 0); }
        if (_previewTex != null) { Destroy(_previewTex); _previewTex = null; }
        if (_previewHint != null) _previewHint.gameObject.SetActive(true);
        if (_viewer != null) _viewer.Fit();
    }

    public void Close() => gameObject.SetActive(false);
    void OnDestroy() { if (_previewTex != null) Destroy(_previewTex); }

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
        _titre = UIFactory.Text(header.transform, "Information Facture — Régularisation des charges", 26, UITheme.TextePrincipal, true);
        UIFactory.LE(_titre.gameObject, flexW: 1);

        var main = UIFactory.HBox(col.transform, 16, false, "Main");
        UIFactory.LE(main.gameObject, flexH: 1);
        main.childForceExpandHeight = true;

        var left = UIFactory.VBox(main.transform, 12, 0, 0, 0, 0, "Left");
        UIFactory.LE(left.gameObject, flexW: 42);
        left.childForceExpandHeight = false;

        var content = MakeScroll(left.transform);

        // ── Destinataire ──
        var d = UIFactory.Section(content, "Destinataire", CoVert, CoVertL);
        _nom = Labeled(d, "Nom");
        UIFactory.Text(d.transform, "Adresse", 16, UITheme.TexteSecondaire);
        _adresse = UIFactory.Input(d.transform, "Adresse (plusieurs lignes possibles)", 88, true);
        _siret = Labeled(d, "SIRET");

        // ── Facture ──
        var f = UIFactory.Section(content, "Facture", CoBleu, CoBleuL);
        UIFactory.Text(f.transform, "RIB CIPL", 16, UITheme.TexteSecondaire);
        _ribDD = BuildRibDropdown(f.transform);
        _date = Labeled(f, "Date");
        _date.onValueChanged.AddListener(_ => RefreshNumero());
        _echeance = Labeled(f, "Date d'échéance (défaut de la phrase de règlement)");
        _echeance.onValueChanged.AddListener(_ => RefreshSommeDefault());
        _sommePhrase = Labeled(f, "Phrase de règlement (bas de facture, ex. « Valeur en votre aimable règlement »)");
        UIFactory.Text(f.transform, "Format du n° de facture", 16, UITheme.TexteSecondaire);
        _numeroFormatDD = UIDropdown.Create(f.transform, NumFmtLabels, NumFmtIds, 1, _ => RefreshNumero());
        BuildNumeroRow(f);
        _refInterne = Labeled(f, "Texte / N° interne (optionnel, en rouge au-dessus du n°)");
        _refInterne.onValueChanged.AddListener(_ => RefreshEntetePreview());
        UIFactory.Text(f.transform, "Texte de présentation (entête / paragraphe)", 16, UITheme.TexteSecondaire);
        _enteteDD = BuildEnteteDropdown(f.transform);
        UIFactory.Text(f.transform, "Aperçu (variables remplacées) :", 14, UITheme.TexteSecondaire);
        var pv = UIFactory.Panel("Preview", f.transform, Color.white);
        UIFactory.Border(pv.gameObject);
        var pvv = pv.gameObject.AddComponent<VerticalLayoutGroup>();
        pvv.padding = new RectOffset(12, 12, 10, 10); pvv.childControlWidth = true; pvv.childControlHeight = true; pvv.childForceExpandWidth = true;
        pv.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _entetePreview = UIFactory.Text(pvv.transform, "—", 15, UITheme.TextePrincipal);

        // ── Régularisation ──
        var g = UIFactory.Section(content, "Régularisation des charges", CoViolet, CoVioletL);
        UIFactory.Text(g.transform, "Année à régulariser (charges impayées)", 16, UITheme.TexteSecondaire);
        _anneeDD = UIDropdown.Create(g.transform, new List<string> { "—" }, new List<string> { DateTime.Today.Year.ToString() }, 0, _ => RefreshCharges());
        UIFactory.Text(g.transform, "Charges concernées (quote-part du locataire) :", 15, UITheme.TexteSecondaire);
        var cbox = UIFactory.VBox(g.transform, 4, 0, 0, 0, 0, "ChargesBox");
        _chargesBox = cbox.transform;
        _provisions = Labeled(g, "Provisions déjà versées (provision × nb périodes)");
        _provisions.contentType = TMP_InputField.ContentType.DecimalNumber;
        _provisions.onValueChanged.AddListener(_ => { RefreshTotaux(); RefreshEntetePreview(); });
        _tCharges    = MontRow(g, "Total des charges (quote-part)");
        _tProvisions = MontRow(g, "Provisions déjà versées");
        _tSolde      = MontRow(g, "Solde HT");
        _tTVA        = MontRow(g, "TVA 20 %");
        _tTTC        = MontRow(g, "Total TTC");

        // ── Options / envoi ──
        var o = UIFactory.Section(content, "Options & envoi", CoVert, CoVertL);
        _tvaDebit = UIFactory.Toggle(o.transform, "Ajouter la mention « TVA payée sur les débits »", true);
        _retard = UIFactory.Toggle(o.transform, "Ajouter la phrase de retard / pénalités de paiement", true);
        _modeInfo = UIFactory.Text(o.transform, "", 15, UITheme.TexteSecondaire);
        _envoiEmail = UIFactory.Toggle(o.transform, "Envoyer par email (au lieu de Pennylane)", false);
        _emailEnvoi = Labeled(o, "Email d'envoi");
        UIFactory.Text(o.transform,
            "Note : l'envoi réel (Pennylane / email) sera activé après validation — rien n'est émis pour l'instant.",
            14, UITheme.Alerte);

        var gen = UIFactory.Button(left.transform, "Générer facture", UITheme.Primaire, Color.white, 46, 20);
        UIFactory.LE(gen.gameObject, minH: 56, flexH: 0);
        gen.onClick.AddListener(GenererApercu);

        // ── Colonne droite : aperçu ──
        var right = UIFactory.VBox(main.transform, 12, 0, 0, 0, 0, "Right");
        UIFactory.LE(right.gameObject, flexW: 58);
        right.childForceExpandHeight = false;
        UIFactory.Text(right.transform, "Facture générée", 22, UITheme.TextePrincipal, true);

        var backdrop = UIFactory.Panel("PvBackdrop", right.transform, Hex("#D2D0CB"));
        UIFactory.Border(backdrop.gameObject);
        UIFactory.LE(backdrop.gameObject, flexH: 1);
        var backRT = (RectTransform)backdrop.transform;
        backRT.pivot = new Vector2(.5f, .5f);
        backdrop.gameObject.AddComponent<RectMask2D>();

        _previewHint = UIFactory.Text(backdrop.transform,
            "Clique sur « Générer facture »\npour afficher l'aperçu ici.", 16, UITheme.TexteSecondaire, false,
            TextAlignmentOptions.Center);
        _previewHint.raycastTarget = false;
        var hintRT = (RectTransform)_previewHint.transform;
        hintRT.anchorMin = new Vector2(.1f, .4f); hintRT.anchorMax = new Vector2(.9f, .6f);
        hintRT.offsetMin = Vector2.zero; hintRT.offsetMax = Vector2.zero;

        var sheet = UIFactory.Rect("Sheet", backdrop.transform);
        sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(.5f, .5f);
        _previewImg = sheet.gameObject.AddComponent<RawImage>();
        _previewImg.color = new Color(1, 1, 1, 0);
        _previewImg.raycastTarget = false;
        _viewer = backdrop.gameObject.AddComponent<FacturePreviewViewer>();
        _viewer.Init(backRT, sheet);

        var zbar = UIFactory.HBox(backdrop.transform, 6, false, "ZoomBar");
        var zbarRT = (RectTransform)zbar.transform;
        zbarRT.anchorMin = zbarRT.anchorMax = new Vector2(1, 1); zbarRT.pivot = new Vector2(1, 1);
        zbarRT.anchoredPosition = new Vector2(-10, -10);
        ZoomBtn(zbar.transform, "−", () => _viewer.ZoomBy(1f / 1.25f));
        ZoomBtn(zbar.transform, "Ajuster", () => _viewer.Fit(), 84);
        ZoomBtn(zbar.transform, "+", () => _viewer.ZoomBy(1.25f));

        var save = UIFactory.Button(right.transform, "Sauvegarder et envoyer", Hex("#854F0B"), Color.white, 46, 20);
        UIFactory.LE(save.gameObject, minH: 56, flexH: 0);
        save.onClick.AddListener(SauvegarderEtEnvoyer);
    }

    // ── Chargement ─────────────────────────────────────────────────────────────

    void LoadIntoUI()
    {
        if (_loc == null) return;
        var f = _loc.factureRegul;

        _titre.text = $"Régularisation des charges · {(_loc.Name ?? "")}";

        _nom.text     = !string.IsNullOrEmpty(f?.destNom)     ? f.destNom     : (_loc.Name ?? "");
        _adresse.text = !string.IsNullOrEmpty(f?.destAdresse) ? f.destAdresse : (_loc.adresseLocataire ?? "");
        _siret.text   = !string.IsNullOrEmpty(f?.destSiret)   ? f.destSiret   : (_loc.siretNumber ?? "");

        _ribDD.SetOptions(
            R.ribs.Select(r => string.IsNullOrWhiteSpace(r.name) ? "(RIB)" : r.name).ToList(),
            R.ribs.Select(r => r.id).ToList(), f?.ribId);
        _enteteDD.SetOptions(
            R.entetes.Select(e => string.IsNullOrWhiteSpace(e.nom) ? "(entête)" : e.nom).ToList(),
            R.entetes.Select(e => e.id).ToList(), ReglageService.EnteteChoisi(f?.enteteId, "Regul"));

        DateTime now = DateTime.Today;
        _date.text = f != null && DateTime.TryParse(f.dateISO, out var dd)
            ? dd.ToString("dd/MM/yyyy") : now.ToString("dd/MM/yyyy");
        _echeance.text = f != null && DateTime.TryParse(f.dateEcheanceISO, out var de)
            ? de.ToString("dd/MM/yyyy")
            : (TryDate(_date.text, out var dbase) ? dbase.AddDays(30) : now.AddDays(30)).ToString("dd/MM/yyyy");
        _autoSomme = DefaultSomme();
        _sommePhrase.text = !string.IsNullOrEmpty(f?.sommePhrase) ? f.sommePhrase : _autoSomme;

        string fmt = f != null && !string.IsNullOrEmpty(f.numeroFormat) ? f.numeroFormat : "AMN";
        _numeroFormatDD.SetOptions(NumFmtLabels, NumFmtIds, fmt);

        _refInterne.text = f?.refInterne ?? "";

        // Années disponibles (charges impayées concernant ce locataire).
        var years = YearsAvailable();
        if (years.Count == 0) years.Add(DateTime.Today.Year - 1);
        string selYear = f != null && f.anneePeriode > 0 ? f.anneePeriode.ToString() : years[0].ToString();
        _anneeDD.SetOptions(years.Select(y => y.ToString()).ToList(), years.Select(y => y.ToString()).ToList(), selYear);

        // Provisions : mémorisées si saisies, sinon provision × nb de périodes.
        _provisions.text = (f != null && f.saved ? f.provisionMontant : ProvisionsAuto())
            .ToString("0.00", CultureInfo.InvariantCulture);

        _numeroId.text = f != null && !string.IsNullOrEmpty(f.numeroId) ? f.numeroId : "";
        RefreshNumero();

        _tvaDebit.isOn = f?.tvaDebit ?? true;
        _retard.isOn = f?.ajouterRetard ?? true;
        _envoiEmail.isOn = f?.envoiEmail ?? false;
        _emailEnvoi.text = !string.IsNullOrEmpty(f?.emailDest) ? f.emailDest : (_loc.emailLocataire ?? "");
        _modeInfo.text = R.modeEnvoi == ModeEnvoi.Pennylane
            ? "Mode global : Pennylane (e-facture Factur-X)."
            : "Mode global : Email direct (SMTP).";

        RefreshCharges();
        RefreshEntetePreview();
    }

    // ── Charges / calculs ──────────────────────────────────────────────────────

    int SelectedYear()
    {
        int.TryParse(_anneeDD?.SelectedId, out int y);
        return y > 0 ? y : DateTime.Today.Year - 1;
    }

    // Charges IMPAYÉES de l'année `year` concernant ce locataire.
    List<ChargeBatiment> ChargesFor(int year)
    {
        var res = new List<ChargeBatiment>();
        if (_bat?.charges == null) return res;
        foreach (var c in _bat.charges)
        {
            if (c.paye) continue;
            bool concerne = c.tousLocataires || (c.locatairesConcernes != null && c.locatairesConcernes.Contains(_loc.id));
            if (!concerne) continue;
            if (TryYear(c.dateISO) != year) continue;
            res.Add(c);
        }
        return res;
    }

    List<int> YearsAvailable()
    {
        var set = new SortedSet<int>();
        if (_bat?.charges != null)
            foreach (var c in _bat.charges)
            {
                if (c.paye) continue;
                bool concerne = c.tousLocataires || (c.locatairesConcernes != null && c.locatairesConcernes.Contains(_loc.id));
                if (!concerne) continue;
                int y = TryYear(c.dateISO); if (y > 0) set.Add(y);
            }
        return set.Reverse().ToList();   // plus récent d'abord
    }

    // Quote-part du locataire sur une charge = coût × part_locataire / somme(parts).
    // Si un seul locataire concerné (ratios vides) → 100 % du coût.
    float QuotePart(ChargeBatiment c)
    {
        if (c.ratios == null || c.ratios.Count == 0) return c.cout;
        float sum = 0f; foreach (var r in c.ratios) sum += r.part;
        var mine = c.ratios.FirstOrDefault(r => r.locataireId == _loc.id);
        if (mine == null || sum <= 0f) return 0f;
        return c.cout * mine.part / sum;
    }

    // Reconstruit la liste des charges + recalcule les totaux.
    void RefreshCharges()
    {
        foreach (Transform t in _chargesBox) Destroy(t.gameObject);
        var charges = ChargesFor(SelectedYear());
        if (charges.Count == 0)
        {
            UIFactory.Text(_chargesBox, "Aucune charge impayée pour cette année.", 15, UITheme.TexteSecondaire);
        }
        else
        {
            foreach (var c in charges)
            {
                var row = UIFactory.HBox(_chargesBox, 8, false, "ChRow");
                UIFactory.LE(row.gameObject, minH: 24);
                string dstr = DateTime.TryParse(c.dateISO, out var cd) ? cd.ToString("dd/MM/yyyy") : "";
                var l = UIFactory.Text(row.transform, $"{c.nom}  ·  {dstr}", 15, UITheme.TextePrincipal);
                UIFactory.LE(l.gameObject, flexW: 1);
                UIFactory.Text(row.transform, $"{QuotePart(c):N2} €", 15, UITheme.TextePrincipal, true, TextAlignmentOptions.Right);
            }
        }
        RefreshTotaux();
    }

    void RefreshTotaux()
    {
        float totalCharges = ChargesFor(SelectedYear()).Sum(QuotePart);
        float provisions = ParseF(_provisions.text);
        float solde = totalCharges - provisions;
        float tva = solde * .2f, ttc = solde * 1.2f;
        _tCharges.text    = $"{totalCharges:N2} €";
        _tProvisions.text = $"{provisions:N2} €";
        _tSolde.text      = $"{solde:N2} €";
        _tTVA.text        = $"{tva:N2} €";
        _tTTC.text        = $"{ttc:N2} €";
    }

    float ProvisionsAuto()
        => (_loc.provisionPourCharges ? _loc.provisionPourChargeValue : 0f)
           * LoyerSummaryUI.NbPeriodes(_loc.periodiciteLoyer);

    // ── Numéro / aperçu entête ─────────────────────────────────────────────────

    string DefaultSomme()
    {
        DateTime ech = TryDate(_echeance.text, out var ed) ? ed : DateTime.Today;
        return "SOMME À NOUS RÉGLER LE " + ech.ToString("d MMMM yyyy", FacturePdfService.FrCulture);
    }

    void RefreshSommeDefault()
    {
        string def = DefaultSomme();
        if (_sommePhrase != null && _sommePhrase.text == _autoSomme) _sommePhrase.text = def;
        _autoSomme = def;
    }

    void RefreshNumero()
    {
        if (_loc == null) return;
        DateTime d = TryDate(_date != null ? _date.text : "", out var dd) ? dd : DateTime.Today;
        string fmt = _numeroFormatDD != null ? _numeroFormatDD.SelectedId : "AMN";
        if (_numeroPrefixe != null) _numeroPrefixe.text = NumeroPrefixe(fmt, d);
        if (_numeroId != null && string.IsNullOrWhiteSpace(_numeroId.text))
            _numeroId.text = Mathf.Max(1, _loc.factureSeq).ToString("D3");
        RefreshEntetePreview();
    }

    static string NumeroPrefixe(string fmt, DateTime d)
    {
        switch (fmt)
        {
            case "AN": return $"{d.Year}/";
            case "AJMN": return $"{d.Year}/{d.Day:D2}{d.Month:D2}";
            default: return $"{d.Year}/{d.Month:D2}";
        }
    }

    string ComposedNumero()
    {
        string pfx = _numeroPrefixe != null ? _numeroPrefixe.text : "";
        string id = _numeroId != null ? (_numeroId.text ?? "").Trim() : "";
        return pfx + id;
    }

    void RefreshEntetePreview()
    {
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        _entetePreview.text = ent == null || string.IsNullOrEmpty(ent.texte)
            ? "—" : FactureVarResolver.Resolve(ent.texte, _loc, _bat, BuildContext());
    }

    FactureContext BuildContext()
    {
        int year = SelectedYear();
        float totalCharges = ChargesFor(year).Sum(QuotePart);
        float provisions = ParseF(_provisions.text);
        float solde = totalCharges - provisions;
        DateTime d = TryDate(_date.text, out var dd) ? dd : DateTime.Today;
        return new FactureContext
        {
            date = d,
            periode = $"charges {year}",
            numero = ComposedNumero(),
            loyerHT = solde,
            tva = solde * .2f,
            ttc = solde * 1.2f,
        };
    }

    // ── Données PDF ────────────────────────────────────────────────────────────

    FacturePdfService.RegulData BuildData()
    {
        var ctx = BuildContext();
        int year = SelectedYear();
        var rib = ReglageService.GetRib(_ribDD?.SelectedId);
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        string entResolved = ent != null ? FactureVarResolver.Resolve(ent.texte, _loc, _bat, ctx) : "";
        var foot = (R.basDePage ?? "").Replace("\r", "").Split('\n');
        string dateStr = ctx.date.ToString("d MMMM yyyy", FacturePdfService.FrCulture);

        var lignes = ChargesFor(year).Select(c => new FacturePdfService.RegulLigne
        {
            nom = c.nom,
            dateStr = DateTime.TryParse(c.dateISO, out var cd) ? cd.ToString("dd/MM/yyyy") : "",
            coutTotal = c.cout,
            quotePart = QuotePart(c),
            pj = string.IsNullOrEmpty(c.pdfPath) ? "" : Path.GetFileName(c.pdfPath),
        }).ToList();

        float totalCharges = lignes.Sum(l => l.quotePart);
        float totalARepartir = lignes.Sum(l => l.coutTotal);
        float provisions = ParseF(_provisions.text);
        float solde = totalCharges - provisions;
        string adr = (_bat != null ? _bat.adressBatiment : "") ?? "";
        string detailTitre = $"{adr}, LOT N°{_loc.lotBatiment}";

        return new FacturePdfService.RegulData
        {
            clientNom = _nom.text,
            clientAdresseHtml = FacturePdfService.AdresseHtml(_adresse.text),
            clientSiret = _siret.text,
            refInterne = _refInterne.text,
            dateStr = dateStr,
            numero = ComposedNumero(),
            subtitle = $"Régularisation des charges {year}",
            bodyHtml = FacturePdfService.BodyHtml(entResolved),
            charges = lignes,
            totalCharges = totalCharges,
            provisions = provisions,
            soldeHT = solde,
            tva = solde * .2f,
            ttc = solde * 1.2f,
            detailTitre = detailTitre,
            locataireNom = _nom.text,
            surfaceImmeuble = _bat != null ? _bat.tailleBatiment : 0f,
            totalARepartir = totalARepartir,
            tvaDebit = _tvaDebit.isOn,
            retard = _retard.isOn,
            sommePhrase = _sommePhrase.text,
            ribTitulaire = rib?.titulaire, ribDomiciliation = rib?.domiciliation,
            ribNum = rib?.rib, ribIban = rib?.iban, ribBic = rib?.bic,
            legal = R.phraseRetard,
            foot1 = foot.Length > 0 ? foot[0] : "",
            foot2 = foot.Length > 1 ? foot[1] : "",
        };
    }

    string FactureDir() => Path.Combine(SaveLocationService.GetSaveRoot(), "Batiment",
        _fiche.batimentPrefabOrigin.getID(), _loc.id, "Facture");

    void GenererApercu()
    {
        SaveFromUI(markPaid: false);
        var d = BuildData();
        string png = Path.Combine(FactureDir(), "apercu_regul.png");
        // Hauteur 2 pages : la facture (p.1) + le détail des charges (p.2).
        if (FacturePdfService.GenerateRegulPreviewPng(d, png, out string err, 794, 2246)) ShowPreview(png);
        else UndoToast.Instance?.ShowInfo("Échec de l'aperçu : " + err);
    }

    void SauvegarderEtEnvoyer()
    {
        SaveFromUI(markPaid: false);
        var d = BuildData();
        int year = SelectedYear();
        string dir = FactureDir();
        string fname = Sanitize($"RegularisationdeCharge-{_nom.text}-{year}") + ".pdf";
        string pdf = Path.Combine(dir, fname);

        if (!FacturePdfService.GenerateRegulPdf(d, pdf, out string err))
        {
            UndoToast.Instance?.ShowInfo("Échec génération PDF : " + err);
            return;
        }

        string png = Path.Combine(dir, "apercu_regul.png");
        if (FacturePdfService.GenerateRegulPreviewPng(d, png, out _, 794, 2246)) ShowPreview(png);

        // Les charges régularisées passent en « payé ».
        foreach (var c in ChargesFor(year)) c.paye = true;

        // Suivi : la ligne de régularisation de l'année passe « Envoyé ».
        var ribS = ReglageService.GetRib(_ribDD?.SelectedId);
        string ribNom = ribS != null ? (!string.IsNullOrWhiteSpace(ribS.name) ? ribS.name : ribS.titulaire) : "";
        FacturationSuivi.MarquerEnvoye(_loc, $"regul-{year}", "Regul",
            d.subtitle, _loc.factureRegul?.dateEcheanceISO, d.numero, pdf, d.ttc, _ribDD?.SelectedId, ribNom);

        // N° consommé → séquence +1 ; on oublie l'ID mémorisé.
        _loc.factureSeq = Mathf.Max(1, _loc.factureSeq) + 1;
        if (_loc.factureRegul != null) _loc.factureRegul.numeroId = "";
        _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();   // persiste locataire + charges
        LocataireSuiviInline.RefreshFor(_fiche);   // Suivi à jour tout de suite
        if (_numeroId != null) _numeroId.text = "";
        RefreshNumero();
        RefreshCharges();   // les charges régularisées disparaissent (désormais payées)

        UndoToast.Instance?.ShowInfo("Régularisation enregistrée (PDF) · charges passées en payé. Envoi réel non activé.");
    }

    void ShowPreview(string pngPath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(pngPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);
            tex.Apply(false, false);
            if (_previewTex != null) Destroy(_previewTex);
            _previewTex = tex;
            _previewImg.texture = tex;
            _previewImg.color = Color.white;
            if (_viewer != null && tex.height > 0) _viewer.SetAspect((float)tex.width / tex.height);
            if (_previewHint != null) _previewHint.gameObject.SetActive(false);
        }
        catch (Exception e) { UndoToast.Instance?.ShowInfo("Aperçu illisible : " + e.Message); }
    }

    // ── Sauvegarde (mémorisé sur le locataire) ─────────────────────────────────

    void SaveFromUI(bool markPaid)
    {
        if (_loc == null) return;
        var f = _loc.factureRegul ?? new FactureInfo();
        f.destNom = _nom.text;
        f.destAdresse = _adresse.text;
        f.destSiret = _siret.text;
        f.ribId = _ribDD?.SelectedId;
        f.enteteId = _enteteDD?.SelectedId;
        f.dateISO = TryDate(_date.text, out var dt) ? dt.ToString("yyyy-MM-dd") : "";
        f.dateEcheanceISO = TryDate(_echeance.text, out var de) ? de.ToString("yyyy-MM-dd") : "";
        f.sommePhrase = _sommePhrase.text;
        f.numeroFormat = _numeroFormatDD?.SelectedId ?? "AMN";
        f.numeroId = (_numeroId.text ?? "").Trim();
        f.numero = ComposedNumero();
        f.tvaDebit = _tvaDebit.isOn;
        f.ajouterRetard = _retard.isOn;
        f.envoiEmail = _envoiEmail.isOn;
        f.emailDest = _emailEnvoi.text;
        f.refInterne = _refInterne.text;
        f.provisionMontant = ParseF(_provisions.text);
        f.anneePeriode = SelectedYear();
        f.objet = $"Régularisation des charges {SelectedYear()}";
        f.saved = true;
        _loc.factureRegul = f;

        _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
    }

    // ── Construction UI (helpers) ──────────────────────────────────────────────

    UIDropdown BuildRibDropdown(Transform parent)
    {
        var labels = R.ribs.Select(r => string.IsNullOrWhiteSpace(r.name) ? "(RIB)" : r.name).ToList();
        var ids = R.ribs.Select(r => r.id).ToList();
        if (labels.Count == 0) { labels.Add("Aucun RIB — voir Réglage"); ids.Add(null); }
        return UIDropdown.Create(parent, labels, ids, 0, _ => { });
    }

    UIDropdown BuildEnteteDropdown(Transform parent)
    {
        var labels = R.entetes.Select(e => string.IsNullOrWhiteSpace(e.nom) ? "(entête)" : e.nom).ToList();
        var ids = R.entetes.Select(e => e.id).ToList();
        if (labels.Count == 0) { labels.Add("Aucun entête — voir Réglage"); ids.Add(null); }
        return UIDropdown.Create(parent, labels, ids, 0, _ => RefreshEntetePreview());
    }

    void BuildNumeroRow(VerticalLayoutGroup body)
    {
        UIFactory.Text(body.transform, "N° de facture  =  format  +  ID locataire", 16, UITheme.TexteSecondaire);
        var row = UIFactory.HBox(body.transform, 8, false, "NumRow");
        UIFactory.LE(row.gameObject, minH: 46);

        var pfx = UIFactory.Panel("NumPrefixe", row.transform, UITheme.Fond);
        UIFactory.Border(pfx.gameObject, UITheme.Bordure);
        var pl = pfx.gameObject.AddComponent<HorizontalLayoutGroup>();
        pl.padding = new RectOffset(14, 14, 0, 0);
        pl.childControlWidth = true; pl.childControlHeight = true;
        pl.childForceExpandWidth = false; pl.childForceExpandHeight = true;
        pl.childAlignment = TextAnchor.MiddleCenter;
        UIFactory.LE(pfx.gameObject, minH: 46, minW: 96, flexW: 0);
        _numeroPrefixe = UIFactory.Text(pfx.transform, "—", 19, UITheme.TextePrincipal, true);

        _numeroId = UIFactory.Input(row.transform, "ID locataire (ex. 001)");
        UIFactory.LE(_numeroId.gameObject, minH: 46, flexW: 1);
        _numeroId.onValueChanged.AddListener(_ => RefreshEntetePreview());
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

    void ZoomBtn(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float w = 44)
    {
        var b = UIFactory.Button(parent, label, UITheme.Carte, UITheme.TextePrincipal, 36, 18, false);
        UIFactory.Border(b.gameObject);
        UIFactory.LE(b.gameObject, prefW: w, flexW: 0, minH: 36);
        b.onClick.AddListener(onClick);
    }

    TMP_InputField Labeled(VerticalLayoutGroup body, string label)
    {
        UIFactory.Text(body.transform, label, 16, UITheme.TexteSecondaire);
        return UIFactory.Input(body.transform, label);
    }

    TMP_Text MontRow(VerticalLayoutGroup body, string label)
    {
        var h = UIFactory.HBox(body.transform, 8, false, "Row");
        UIFactory.LE(h.gameObject, minH: 24);
        var l = UIFactory.Text(h.transform, label, 16, UITheme.TexteSecondaire);
        UIFactory.LE(l.gameObject, flexW: 1);
        return UIFactory.Text(h.transform, "—", 16, UITheme.TextePrincipal, true, TextAlignmentOptions.Right);
    }

    static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = (s ?? "").Replace(c, '-');
        return s;
    }

    static float ParseF(string s)
    {
        float.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
        return v;
    }

    static int TryYear(string iso) => DateTime.TryParse(iso, out var d) ? d.Year : 0;

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static bool TryDate(string s, out DateTime d) =>
        DateTime.TryParseExact((s ?? "").Trim(),
            new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
}
