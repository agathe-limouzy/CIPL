using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Écran « Information Facture — Révision du dépôt de garantie ».
/// Facture le COMPLÉMENT de dépôt (nouveau dépôt − dépôt déjà versé) quand le loyer
/// a été indexé. Sans TVA. « Sauvegarder et envoyer » produit le PDF et avance le
/// numéro — il NE modifie PAS le montant du dépôt ni la date de révision (ça reste
/// au bouton « Révision dépôt de garantie » de la fiche). Aucun envoi réel ici.
public class FactureDepotPanel : MonoBehaviour
{
    public static FactureDepotPanel Instance { get; private set; }

    static readonly Color CoVert = UITheme.Primaire, CoVertL = UITheme.PrimaireClair;
    static readonly Color CoBleu = Hex("#2C3E5E"), CoBleuL = Hex("#DEE3EB");
    static readonly Color CoDepot = Hex("#2A6F82"), CoDepotL = Hex("#E2EFF2");
    // Couleurs de cartes. `CoContenu` et `CoReglement` portent le même rôle — donc la
    // même teinte — dans les quatre panneaux ; seule la carte du type change de couleur.
    static readonly Color CoContenu   = Hex("#5F5E5A"), CoContenuL   = Hex("#E9E6DE");
    static readonly Color CoReglement = Hex("#854F0B"), CoReglementL = Hex("#F6E6C8");

    // Source unique : FactureNumerotation (les quatre panneaux dupliquaient ces listes).
    static List<string> NumFmtLabels => FactureNumerotation.Labels;
    static List<string> NumFmtIds => FactureNumerotation.Ids;

    LocatairePrefab _fiche; Locataire _loc; Batiment _bat;

    TMP_Text _titre, _entetePreview, _modeInfo, _previewHint, _tNouveau, _tAncien, _tComplement, _perLine;
    TMP_InputField _nom, _adresse, _siret, _date, _echeance, _numeroId, _refInterne, _sommePhrase, _nbPeriodes, _ancien, _emailEnvoi;
    TMP_InputField _depotRappel, _depotDu, _depotRembourse, _depotEquilibre;
    TMP_InputField _texteTvaDebit;
    TMP_InputField _emailObjet, _emailCorps;
    bool _envoiEnCours;   // empêche un second clic de produire un second envoi
    TMP_Text _numeroPrefixe;
    UIDropdown _ribDD, _enteteDD, _numeroFormatDD;
    Toggle _ttcToggle, _retard;
    UIDropdown _mentionTva;
    string _autoSomme;

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

    /// `ligne` = la ligne du suivi cliquée : « depot-initial » ouvre la facture du dépôt
    /// initial (rien de déjà versé), toute autre valeur la révision.
    public static void OpenDepot(LocatairePrefab fiche, FactureEtat ligne = null)
    {
        if (fiche == null) return;
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("FactureDepotPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<FactureDepotPanel>();
        }
        Instance._initial = ligne?.key == FacturationSuivi.CleDepotInitial;
        Instance.OpenFor(fiche);
    }

    // Dépôt initial (demandé à l'initialisation du dépôt) : on facture tout le dépôt,
    // rien n'est déjà versé, au format d'une facture simple (comme une refacturation) ;
    // sinon, le complément d'une révision, au format régularisation avec l'explication.
    bool _initial;
    readonly List<GameObject> _explication = new List<GameObject>();   // bloc explicatif (révision seulement)

    void OpenFor(LocatairePrefab fiche)
    {
        _fiche = fiche;
        _loc = fiche.GetLocataire();
        _bat = fiche.batimentPrefabOrigin != null ? fiche.batimentPrefabOrigin.getBatiment() : null;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ResetPreview();
        LoadIntoUI();
    }

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
        var back = UIFactory.Button(header.transform, "←  Retour", UITheme.Carte, UITheme.TextePrincipal, 42, UITheme.Role.Bouton);
        UIFactory.Border(back.gameObject); UIFactory.LE(back.gameObject, prefW: 140, flexW: 0);
        back.onClick.AddListener(Close);
        _titre = UIFactory.Text(header.transform, "Information Facture — Révision du dépôt", UITheme.Role.Page, UITheme.TextePrincipal, true);
        UIFactory.LE(_titre.gameObject, flexW: 1);

        var main = UIFactory.HBox(col.transform, 16, false, "Main");
        UIFactory.LE(main.gameObject, flexH: 1);
        main.childForceExpandHeight = true;

        var left = UIFactory.VBox(main.transform, 12, 0, 0, 0, 0, "Left");
        UIFactory.LE(left.gameObject, flexW: 42);
        left.childForceExpandHeight = false;

        var content = MakeScroll(left.transform);

        var d = UIFactory.Section(content, "Destinataire", CoVert, CoVertL);
        _nom = Labeled(d, "Nom");
        UIFactory.Text(d.transform, "Adresse", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _adresse = UIFactory.Input(d.transform, "Adresse (plusieurs lignes possibles)", 88, true);
        _siret = Labeled(d, "SIRET");

        // L'écran suit l'ordre du DOCUMENT IMPRIMÉ : en-tête, contenu, pied de
        // règlement. Dans chaque carte, le champ qui change à chaque facture précède
        // celui qui ne bouge qu'une fois par an (RIB, format du numéro). Même ordre
        // dans les quatre panneaux : seule la carte du type varie.

        // ── 1. En-tête du document ──
        var e = UIFactory.Section(content, "En-tête du document", CoBleu, CoBleuL);
        _date = Labeled(e, "Date");
        _date.onValueChanged.AddListener(_ => RefreshNumero());
        _refInterne = Labeled(e, "Texte / N° interne (optionnel, en rouge au-dessus du n°)");
        _refInterne.onValueChanged.AddListener(_ => RefreshEntetePreview());
        UIFactory.Text(e.transform, "Format du n° de facture", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _numeroFormatDD = UIDropdown.Create(e.transform, NumFmtLabels, NumFmtIds, 1, _ => RefreshNumero());
        BuildNumeroRow(e);

        // ── 2. Contenu ──
        var c = UIFactory.Section(content, "Contenu", CoContenu, CoContenuL);
        UIFactory.Text(c.transform, "Texte de présentation (entête / paragraphe)", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _enteteDD = BuildEnteteDropdown(c.transform);
        UIFactory.Text(c.transform, "Aperçu (variables remplacées) :", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var pv = UIFactory.Panel("Preview", c.transform, Color.white);
        UIFactory.Border(pv.gameObject);
        var pvv = pv.gameObject.AddComponent<VerticalLayoutGroup>();
        // Explicite : le defaut d'Unity est TRUE, et un groupe qui « veut s'etendre »
        // propage un flexibleHeight jusqu'en haut de la hierarchie — c'est ce qui
        // creusait 104 px de blanc dans la section Loyer.
        pvv.childForceExpandHeight = false;
        pvv.padding = new RectOffset(12, 12, 10, 10); pvv.childControlWidth = true; pvv.childControlHeight = true; pvv.childForceExpandWidth = true;
        pv.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _entetePreview = UIFactory.Text(pvv.transform, "—", UITheme.Role.Donnee, UITheme.TextePrincipal);

        // ── Dépôt de garantie (contenu propre au type) ──
        var g = UIFactory.Section(content, "Dépôt de garantie", CoDepot, CoDepotL);
        _ttcToggle = UIFactory.Toggle(g.transform, "Locataire soumis à la TVA (loyer TTC)", true);
        _ttcToggle.onValueChanged.AddListener(_ => RefreshTotaux());
        _perLine = UIFactory.Text(g.transform, "—", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _nbPeriodes = Labeled(g, "Nombre de périodes de loyer");
        _nbPeriodes.contentType = TMP_InputField.ContentType.IntegerNumber;
        _nbPeriodes.onValueChanged.AddListener(_ => RefreshTotaux());
        _ancien = Labeled(g, "Dépôt de garantie déjà versé (€)");
        _ancien.contentType = TMP_InputField.ContentType.DecimalNumber;
        _ancien.onValueChanged.AddListener(_ => RefreshTotaux());
        _tNouveau    = MontRow(g, "Nouveau dépôt de garantie");
        _tAncien     = MontRow(g, "Dépôt déjà versé");
        _tComplement = MontRow(g, "Complément à régler");

        // La mention s'imprime sous ces totaux, donc sa case vit ici — même règle que
        // sur les trois autres panneaux. Le tableau du dépôt reste sans ligne TVA ni
        // T.T.C. (`masquerTva`) : c'est la mention qui devient disponible, pas la TVA.
        _mentionTva = MentionTva.Creer(g.transform, out _texteTvaDebit);

        // Phrases du bloc explicatif imprimé sous le titre du document. Elles
        // appartiennent à CETTE facture, pas à l'entreprise — d'où leur place ici
        // plutôt que dans les Réglages. Trois variantes et non un texte unique :
        // la phrase imprimée dépend du sens de la dette, et ce choix-là reste au code.
        // (Masquées pour le dépôt initial : sa facture est au format simple, sans bloc.)
        int debutExplication = g.transform.childCount;
        UIFactory.Text(g.transform,
            "Explication imprimée sur le document — variables : {depot.termes} · {depot.nb} · "
            + "{depot.base} · {depot.montant}, plus les {loc.*} et {bat.*}. « / » ouvre la liste. "
            + "HTML simple accepté (<b>gras</b>).",
            UITheme.Role.Aide, UITheme.TexteSecondaire);

        UIFactory.Text(g.transform, "Rappel du montant requis", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _depotRappel = UIFactory.Input(g.transform, "Je vous rappelle qu'il doit correspondre à…", 70, true);
        SlashAutocomplete.Attach(_depotRappel);

        UIFactory.Text(g.transform, "Le locataire nous doit un complément", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _depotDu = UIFactory.Input(g.transform, "…vous nous devez…", 70, true);
        SlashAutocomplete.Attach(_depotDu);

        UIFactory.Text(g.transform, "Nous devons un remboursement au locataire", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _depotRembourse = UIFactory.Input(g.transform, "…nous vous devons…", 70, true);
        SlashAutocomplete.Attach(_depotRembourse);

        UIFactory.Text(g.transform, "Dépôt déjà au bon montant (rien à régler)", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _depotEquilibre = UIFactory.Input(g.transform, "…aucun ajustement n'est nécessaire.", 70, true);
        SlashAutocomplete.Attach(_depotEquilibre);
        for (int i = debutExplication; i < g.transform.childCount; i++) _explication.Add(g.transform.GetChild(i).gameObject);

        // ── 3. Règlement (pied du document) ──
        // L'échéance, la phrase qu'elle alimente et le RIB sont voisins : le lien se
        // voit, d'où la mention « (défaut de la phrase de règlement) » retirée du
        // libellé de l'échéance — elle ne servait qu'à compenser l'éloignement.
        var p = UIFactory.Section(content, "Règlement", CoReglement, CoReglementL);
        _echeance = Labeled(p, "Date d'échéance");
        _echeance.onValueChanged.AddListener(_ => RefreshSommeDefault());
        _sommePhrase = Labeled(p, "Phrase de règlement (bas de facture, ex. « Valeur en votre aimable règlement »)");
        SlashAutocomplete.Attach(_sommePhrase);
        UIFactory.Text(p.transform, "RIB CIPL", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _ribDD = BuildRibDropdown(p.transform);
        _retard = UIFactory.Toggle(p.transform, "Ajouter la phrase de retard / pénalités de paiement", true);

        var o = UIFactory.Section(content, "Options & envoi", CoVert, CoVertL);
        _modeInfo = UIFactory.Text(o.transform, "", UITheme.Role.Donnee, UITheme.TexteSecondaire);

        // Message d'accompagnement. Il appartient bien à l'envoi, donc à cette carte.
        UIFactory.Text(o.transform, "Objet du message", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _emailObjet = UIFactory.Input(o.transform, EmailService.ObjetDefaut);
        SlashAutocomplete.Attach(_emailObjet);
        UIFactory.Text(o.transform, "Corps du message", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _emailCorps = UIFactory.Input(o.transform, EmailService.CorpsDefaut, 110, true);
        SlashAutocomplete.Attach(_emailCorps);
        UIFactory.Text(o.transform,
            "« / » ouvre la liste des variables. La facture est jointe en PDF automatiquement.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        _emailEnvoi = Labeled(o, "Email d'envoi");
        UIFactory.Text(o.transform,
            "Note : cette facture ne modifie pas le montant du dépôt (voir le bouton « Révision dépôt de garantie »). Rien n'est émis pour l'instant.",
            UITheme.Role.Aide, UITheme.Alerte);

        var gen = UIFactory.Button(left.transform, "Générer facture", UITheme.Primaire, Color.white, 46, UITheme.Role.Bouton);
        UIFactory.LE(gen.gameObject, minH: 56, flexH: 0);
        gen.onClick.AddListener(GenererApercu);

        var right = UIFactory.VBox(main.transform, 12, 0, 0, 0, 0, "Right");
        UIFactory.LE(right.gameObject, flexW: 58);
        right.childForceExpandHeight = false;
        UIFactory.Text(right.transform, "Facture générée", UITheme.Role.Section, UITheme.TextePrincipal, true);

        var backdrop = UIFactory.Panel("PvBackdrop", right.transform, Hex("#D2D0CB"));
        UIFactory.Border(backdrop.gameObject);
        UIFactory.LE(backdrop.gameObject, flexH: 1);
        var backRT = (RectTransform)backdrop.transform;
        backRT.pivot = new Vector2(.5f, .5f);
        backdrop.gameObject.AddComponent<RectMask2D>();

        _previewHint = UIFactory.Text(backdrop.transform,
            "Clique sur « Générer facture »\npour afficher l'aperçu ici.", UITheme.Role.Aide, UITheme.TexteSecondaire, false,
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

        var save = UIFactory.Button(right.transform, "Sauvegarder et envoyer", Hex("#854F0B"), Color.white, 46, UITheme.Role.Bouton);
        UIFactory.LE(save.gameObject, minH: 56, flexH: 0);
        save.onClick.AddListener(SauvegarderEtEnvoyer);
    }

    // ── Chargement ─────────────────────────────────────────────────────────────

    void LoadIntoUI()
    {
        if (_loc == null) return;
        var f = _loc.factureDepot;

        _titre.text = $"{(_initial ? "Dépôt de garantie" : "Révision du dépôt")} · {(_loc.Name ?? "")}";
        foreach (var go in _explication) go.SetActive(!_initial);

        _nom.text     = !string.IsNullOrEmpty(f?.destNom)     ? f.destNom     : (_loc.Name ?? "");
        _adresse.text = !string.IsNullOrEmpty(f?.destAdresse) ? f.destAdresse : (_loc.adresseLocataire ?? "");
        _siret.text   = !string.IsNullOrEmpty(f?.destSiret)   ? f.destSiret   : (_loc.siretNumber ?? "");

        _ribDD.SetOptions(
            R.ribs.Select(r => string.IsNullOrWhiteSpace(r.name) ? "(RIB)" : r.name).ToList(),
            R.ribs.Select(r => r.id).ToList(), f?.ribId);
        _enteteDD.SetOptions(
            R.entetes.Select(e => string.IsNullOrWhiteSpace(e.nom) ? "(entête)" : e.nom).ToList(),
            R.entetes.Select(e => e.id).ToList(), ReglageService.EnteteChoisi(f?.enteteId, "Depot"));

        DateTime now = DateTime.Today;
        _date.text = f != null && DateTime.TryParse(f.dateISO, out var dd)
            ? dd.ToString("dd/MM/yyyy") : now.ToString("dd/MM/yyyy");
        _echeance.text = f != null && DateTime.TryParse(f.dateEcheanceISO, out var de)
            ? de.ToString("dd/MM/yyyy")
            : (TryDate(_date.text, out var dbase) ? dbase.AddDays(30) : now.AddDays(30)).ToString("dd/MM/yyyy");
        _autoSomme = DefaultSomme();
        _sommePhrase.text = !string.IsNullOrEmpty(f?.sommePhrase) ? f.sommePhrase : _autoSomme;

        // Pré-remplies avec le texte d'usine quand la facture n'a rien : l'utilisatrice
        // doit voir la phrase réellement imprimée et la retoucher sur place, pas
        // deviner ce qu'un champ vide produira.
        _depotRappel.text    = ExplicationDepot.Ou(f?.depotRappel,    ExplicationDepot.RappelDefaut);
        _depotDu.text        = ExplicationDepot.Ou(f?.depotDu,        ExplicationDepot.DuDefaut);
        _depotRembourse.text = ExplicationDepot.Ou(f?.depotRembourse, ExplicationDepot.RembourseDefaut);
        _depotEquilibre.text = ExplicationDepot.Ou(f?.depotEquilibre, ExplicationDepot.EquilibreDefaut);

        string fmt = f != null && !string.IsNullOrEmpty(f.numeroFormat) ? f.numeroFormat : "AMN";
        _numeroFormatDD.SetOptions(NumFmtLabels, NumFmtIds, fmt);
        _refInterne.text = f?.refInterne ?? "";

        // Dépôt : base TTC/HT, ancien montant, nb de périodes (dérivé du dépôt actuel).
        _ttcToggle.isOn = _loc.depotSurTTC;
        _ancien.text = (_initial ? 0f : _loc.depotDeGarantie).ToString("0.00", CultureInfo.InvariantCulture);
        float per = CurrentPeriode();
        int nb = per > 0f ? Mathf.Max(1, Mathf.RoundToInt(_loc.depotDeGarantie / per)) : 1;
        // Le nombre mémorisé est celui de la dernière facture de révision. Le dépôt
        // initial, lui, facture le dépôt tel qu'initialisé : on le déduit du montant.
        if (!_initial && f != null && f.moisPeriode > 0) nb = f.moisPeriode;
        _nbPeriodes.text = nb.ToString();

        _numeroId.text = f != null && !string.IsNullOrEmpty(f.numeroId) ? f.numeroId : "";
        RefreshNumero();

        MentionTva.Charger(_mentionTva, _texteTvaDebit, f);
        _retard.isOn = f?.ajouterRetard ?? true;
        _emailEnvoi.text = !string.IsNullOrEmpty(f?.emailDest) ? f.emailDest : (_loc.emailLocataire ?? "");
        _emailObjet.text = FacturePdfService.Texte(f?.emailObjet, EmailService.ObjetDefaut);
        _emailCorps.text = FacturePdfService.Texte(f?.emailCorps, EmailService.CorpsDefaut);
        _modeInfo.text = R.modeEnvoi == ModeEnvoi.Pennylane
            ? "Mode global : Pennylane (e-facture Factur-X)."
            : "Mode global : Email direct (SMTP).";

        RefreshTotaux();
        RefreshEntetePreview();
    }

    // ── Calculs dépôt ──────────────────────────────────────────────────────────

    float CurrentPeriode()
    {
        float ht = _loc.loyerAnnuel / Mathf.Max(1, LoyerSummaryUI.NbPeriodes(_loc.periodiciteLoyer));
        return (_ttcToggle != null && _ttcToggle.isOn) ? ht * 1.2f : ht;
    }

    float Nouveau()
    {
        int nb = 0; int.TryParse((_nbPeriodes.text ?? "").Trim(), out nb);
        return CurrentPeriode() * Mathf.Max(0, nb);
    }

    void RefreshTotaux()
    {
        float per = CurrentPeriode();
        _perLine.text = $"Loyer : {per:0.00} € / période {(_ttcToggle != null && _ttcToggle.isOn ? "TTC" : "HT")}";
        float nouveau = Cents(Nouveau()), ancien = Cents(ParseF(_ancien.text));
        float complement = Cents(nouveau - ancien);
        _tNouveau.text    = $"{nouveau:N2} €";
        _tAncien.text     = $"{ancien:N2} €";
        _tComplement.text = $"{complement:N2} €";

        // Nouveau dépôt inférieur à l'ancien : c'est un remboursement au locataire,
        // donc un avoir — pas une facture de complément.
        if (complement < 0f)
            _tComplement.text = $"{complement:N2} €  ⚠ remboursement (avoir)";

        RefreshSommeDefault();   // le solde vient de changer : la phrase doit suivre son signe
        RefreshEntetePreview();
    }

    /// Arrondi au centime — évite les dérives de `float` sur les montants.
    static float Cents(float v) => Mathf.Round(v * 100f) / 100f;

    // ── Numéro / phrase / aperçu entête ────────────────────────────────────────

    /// Phrase proposée par défaut. Elle suit le SIGNE du solde : sinon le champ
    /// afficherait « SOMME À NOUS RÉGLER » pendant que le PDF imprimerait
    /// « SOMME QUI VOUS SERA REMBOURSÉE » — l'écran mentirait sur ce qui est émis.
    /// `FactureEmission.PhraseSomme` reste le filet de sécurité à la génération.
    string DefaultSomme()
    {
        if (Nouveau() - ParseF(_ancien.text) < -0.005f) return "SOMME QUI VOUS SERA REMBOURSÉE";

        DateTime ech = TryDate(_echeance != null ? _echeance.text : "", out var ed) ? ed : DateTime.Today;
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

    static string NumeroPrefixe(string fmt, DateTime d) => FactureNumerotation.Prefixe(fmt, d);

    string ComposedNumero()
    {
        string pfx = _numeroPrefixe != null ? _numeroPrefixe.text : "";
        string id = _numeroId != null ? (_numeroId.text ?? "").Trim() : "";
        return pfx + id;
    }

    void RefreshEntetePreview()
    {
        if (_entetePreview == null) return;
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        _entetePreview.text = ent == null || string.IsNullOrEmpty(ent.texte)
            ? "—" : FactureVarResolver.Resolve(ent.texte, _loc, _bat, BuildContext());
    }

    FactureContext BuildContext()
    {
        float c = Nouveau() - ParseF(_ancien.text);
        DateTime d = TryDate(_date.text, out var dd) ? dd : DateTime.Today;
        return new FactureContext
        {
            date = d,
            periode = "dépôt de garantie",
            numero = ComposedNumero(),
            loyerHT = c, tva = 0f, ttc = c,
        };
    }

    // ── Données PDF (réutilise le rendu régularisation, sans page 2 ni TVA) ─────

    FacturePdfService.RegulData BuildData()
    {
        var ctx = BuildContext();
        var rib = ReglageService.GetRib(_ribDD?.SelectedId);
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        string entResolved = ent != null ? FactureVarResolver.Resolve(ent.texte, _loc, _bat, ctx) : "";
        var foot = (R.basDePage ?? "").Replace("\r", "").Split('\n');
        float nouveau = Nouveau(), ancien = ParseF(_ancien.text), complement = nouveau - ancien;
        int.TryParse((_nbPeriodes.text ?? "").Trim(), out int nbTermes);

        return new FacturePdfService.RegulData
        {
            clientNom = _nom.text,
            clientAdresseHtml = FacturePdfService.AdresseHtml(_adresse.text),
            clientSiret = _siret.text,
            refInterne = _refInterne.text,
            dateStr = ctx.date.ToString("d MMMM yyyy", FacturePdfService.FrCulture),
            numero = ComposedNumero(),
            subtitle = _initial ? "Dépôt de garantie" : "Révision du dépôt de garantie",
            bodyHtml = FacturePdfService.BodyHtml(entResolved),
            // Explication du calcul : indice de référence → nouvel indice → nouveau
            // loyer, puis la règle des N termes qui donne le dépôt.
            explicationHtml = ExplicationDepot.Html(_loc, Mathf.Max(1, nbTermes), complement, ctx.date, _bat, _loc.factureDepot),
            charges = new List<FacturePdfService.RegulLigne>(),   // pas de page 2
            totalCharges = nouveau, provisions = ancien, soldeHT = complement,
            tva = 0f, ttc = complement,
            labelTotal = _initial ? "Dépôt de garantie" : "Nouveau dépôt de garantie",
            labelProvisions = "Dépôt de garantie déjà versé",
            // « Complément à régler » serait faux quand c'est nous qui remboursons.
            labelSolde = FactureEmission.LibelleSolde(complement, _initial ? "Montant à régler" : "Complément à régler"),
            masquerTva = true,
            tvaDebit = MentionTva.Imprimee(_mentionTva), retard = _retard.isOn,
            texteTvaDebit = FactureVarResolver.Resolve(MentionTva.Phrase(_mentionTva, _texteTvaDebit), _loc, _bat, ctx),
            // Résolue APRÈS PhraseSomme : celle-ci peut substituer sa propre phrase
            // selon le signe du complément, et cette phrase-là doit être résolue aussi.
            sommePhrase = FactureVarResolver.Resolve(
                FactureEmission.PhraseSomme(_sommePhrase.text, complement), _loc, _bat, ctx),
            ribTitulaire = rib?.titulaire, ribDomiciliation = rib?.domiciliation,
            ribNum = rib?.rib, ribIban = rib?.iban, ribBic = rib?.bic,
            legal = R.phraseRetard,
            foot1 = foot.Length > 0 ? foot[0] : "",
            foot2 = foot.Length > 1 ? foot[1] : "",
        };
    }

    string FactureDir() => DossiersDonnees.DossierFactures(
        _fiche.batimentPrefabOrigin.getName(), _loc.Name);

    // Dépôt initial : facture au format SIMPLE, celui d'une refacturation (demande du
    // 30/09) — une ligne « Dépôt de garantie », sans TVA : un dépôt n'y est pas soumis.
    FacturePdfService.Data BuildDataInitial()
    {
        var ctx = BuildContext();
        var rib = ReglageService.GetRib(_ribDD?.SelectedId);
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        string entResolved = ent != null ? FactureVarResolver.Resolve(ent.texte, _loc, _bat, ctx) : "";
        var foot = (R.basDePage ?? "").Replace("\r", "").Split('\n');
        float montant = Cents(Nouveau());
        int.TryParse((_nbPeriodes.text ?? "").Trim(), out int nb);
        string baseLoyer = _ttcToggle != null && _ttcToggle.isOn ? "TTC" : "HT";

        return new FacturePdfService.Data
        {
            clientNom = _nom.text,
            clientAdresseHtml = FacturePdfService.AdresseHtml(_adresse.text),
            clientSiret = _siret.text,
            refInterne = _refInterne.text,
            ligneLabel = $"Dépôt de garantie — {nb} terme{(nb > 1 ? "s" : "")} de loyer {baseLoyer}",
            dateStr = ctx.date.ToString("d MMMM yyyy", FacturePdfService.FrCulture),
            numero = ComposedNumero(),
            subtitle = "Dépôt de garantie",
            bodyHtml = FacturePdfService.BodyHtml(entResolved),
            totalPeriode = montant, provision = 0f, totalHT = montant, tva = 0f, ttc = montant,
            tvaLibelle = "TVA — dépôt de garantie non soumis",
            tvaDebit = MentionTva.Imprimee(_mentionTva), retard = _retard.isOn,
            sommePhrase = FactureVarResolver.Resolve(_sommePhrase.text, _loc, _bat, ctx),
            texteTvaDebit = FactureVarResolver.Resolve(MentionTva.Phrase(_mentionTva, _texteTvaDebit), _loc, _bat, ctx),
            ribTitulaire = rib?.titulaire, ribDomiciliation = rib?.domiciliation,
            ribNum = rib?.rib, ribIban = rib?.iban, ribBic = rib?.bic,
            legal = R.phraseRetard,
            foot1 = foot.Length > 0 ? foot[0] : "",
            foot2 = foot.Length > 1 ? foot[1] : "",
        };
    }

    // Le document à émettre, quel que soit son format : révision (gabarit régularisation,
    // avec l'explication du calcul) ou dépôt initial (facture simple). Le protocole —
    // numéro, correction, envoi, suivi — est le même pour les deux.
    sealed class Document
    {
        public string Sujet;
        public float Montant;
        public Func<string> LireNumero;
        public Action<string> PoserNumero;
        public Func<string, string> Pdf, Apercu;   // chemin → erreur, ou null si réussi
    }

    Document DocumentAEmettre()
    {
        if (_initial)
        {
            var d = BuildDataInitial();
            return new Document
            {
                Sujet = d.subtitle, Montant = d.ttc,
                LireNumero = () => d.numero, PoserNumero = n => d.numero = n,
                Pdf = p => FacturePdfService.GeneratePdf(d, p, out var e) ? null : e,
                Apercu = p => FacturePdfService.GeneratePreviewPng(d, p, out var e) ? null : e,
            };
        }
        var r = BuildData();
        return new Document
        {
            Sujet = r.subtitle, Montant = r.soldeHT,
            LireNumero = () => r.numero, PoserNumero = n => r.numero = n,
            Pdf = p => FacturePdfService.GenerateRegulPdf(r, p, out var e) ? null : e,
            Apercu = p => FacturePdfService.GenerateRegulPreviewPng(r, p, out var e) ? null : e,
        };
    }

    void GenererApercu()
    {
        SaveFromUI();
        string png = Path.Combine(FactureDir(), "apercu_depot.png");
        string err = DocumentAEmettre().Apercu(png);
        if (err == null) ShowPreview(png);
        else UndoToast.Instance?.ShowInfo("Échec de l'aperçu : " + err);
    }

    void SauvegarderEtEnvoyer()
    {
        SaveFromUI();
        var d = DocumentAEmettre();

        // Complément négatif = le nouveau dépôt est inférieur à l'ancien, donc c'est
        // NOUS qui devons. Le document est émis quand même (comptablement, c'est un
        // avoir : il consomme un numéro comme une facture), mais il est confirmé —
        // inverser le sens d'une somme ne doit pas tenir à un clic.
        if (d.Montant < -0.005f && ConfirmDialog.Instance != null)
        {
            ConfirmDialog.Instance.Show(
                "Remboursement au locataire",
                "Le nouveau dépôt est inférieur à l'ancien : ce document constate "
                + (-d.Montant).ToString("N2", FacturePdfService.FrCulture)
                + " € dus AU locataire, et non réclamés. "
                + "Il consommera un numéro comme une facture.",
                () => Emettre(d), "Émettre");
            return;
        }

        Emettre(d);
    }

    /// Génère le PDF et met le suivi à jour. Séparé de `SauvegarderEtEnvoyer` pour
    /// pouvoir être repris après une confirmation (voir le cas du remboursement).
    void Emettre(Document d)
    {
        // L'envoi est asynchrone : sans cette garde, un double clic partirait deux fois.
        if (_envoiEnCours) { UndoToast.Instance?.ShowInfo("Un envoi est déjà en cours."); return; }

        string dir = FactureDir();
        int year = (TryDate(_date.text, out var dt) ? dt : DateTime.Today).Year;

        // Clé de suivi calculée AVANT la génération du PDF : sans elle, un second
        // clic consommait une nouvelle séquence et écrasait le PDF déjà émis.
        int revYear = DateTime.TryParse(_loc.dateRevisionDepotISO, out var rv) ? rv.Year
            : (TryDate(_date.text, out var dtr) ? dtr.Year : DateTime.Today.Year);
        string key = _initial ? FacturationSuivi.CleDepotInitial : $"depot-{revYear}";

        // Déjà émise → version « corrigée(X) » : même numéro, aucune nouvelle
        // séquence consommée, et le PDF d'origine est conservé.
        var emission = FactureEmission.Preparer(_loc, key, d.LireNumero());
        d.PoserNumero(emission.NumeroFacture);
        bool correction = emission.Correction;

        string fname = Sanitize($"{(_initial ? "DepotGarantie" : "RevisionDepot")}-{_nom.text}-{year}{emission.SuffixeFichier}") + ".pdf";
        string pdf = Path.Combine(dir, fname);

        string err = d.Pdf(pdf);
        if (err != null)
        {
            UndoToast.Instance?.ShowInfo("Échec génération PDF : " + err);
            return;
        }

        string png = Path.Combine(dir, "apercu_depot.png");
        if (d.Apercu(png) == null) ShowPreview(png);

        // Pas d'envoi demandé : on enregistre, rien ne part. Le bouton s'appelant
        // « Sauvegarder et envoyer », il faut le dire, sinon on attend un mail en vain.
        if (R.modeEnvoi != ModeEnvoi.Email)
        {
            Finaliser(d, key, emission, correction, pdf, false,
                "  Aucun email envoyé : la case « Envoyer par email » est décochée (Options & envoi).");
            return;
        }

        // Refus AVANT la confirmation si quelque chose manque.
        string dest = (_emailEnvoi.text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dest))
        {
            UndoToast.Instance?.ShowInfo("Aucune adresse email pour ce locataire. "
                + "Rien n'a été envoyé ; le PDF est enregistré.");
            return;
        }

        string manque = EmailService.CeQuiManque();
        if (manque != null) { UndoToast.Instance?.ShowInfo(manque + " Le PDF est enregistré."); return; }

        var ctx = BuildContext();
        string objet = FactureVarResolver.Resolve(
            FacturePdfService.Texte(_emailObjet.text, EmailService.ObjetDefaut), _loc, _bat, ctx);
        string corps = FactureVarResolver.Resolve(
            FacturePdfService.Texte(_emailCorps.text, EmailService.CorpsDefaut), _loc, _bat, ctx);

        // Sans confirmation disponible, rien ne part.
        if (ConfirmDialog.Instance == null)
        {
            UndoToast.Instance?.ShowInfo("Confirmation indisponible : rien n'a été envoyé. "
                + "Le PDF est enregistré.");
            return;
        }

        ConfirmDialog.Instance.Show(
            "Envoyer la facture de dépôt par email ?",
            $"À : {dest}\nObjet : {objet}\nPièce jointe : {Path.GetFileName(pdf)}\n\n"
            + "Le message part immédiatement et ne pourra pas être rappelé.",
            () => StartCoroutine(EnvoyerPuisFinaliser(d, key, emission, correction, pdf, dest, objet, corps)),
            "Envoyer");
    }

    /// Envoie, attend, et n'enregistre QUE si le message est parti.
    System.Collections.IEnumerator EnvoyerPuisFinaliser(
        Document d, string key, FactureEmission.Decision emission,
        bool correction, string pdf, string dest, string objet, string corps)
    {
        _envoiEnCours = true;
        UndoToast.Instance?.ShowInfo($"Envoi en cours vers {dest}…");

        var envoi = EmailService.Envoyer(dest, objet, corps, new[] { pdf });
        while (!envoi.Termine) yield return null;

        _envoiEnCours = false;

        if (!envoi.Succes)
        {
            UndoToast.Instance?.ShowInfo("Envoi échoué — " + envoi.Erreur
                + " Le PDF est enregistré, la facture n'est PAS marquée envoyée : tu peux réessayer.");
            yield break;
        }

        Finaliser(d, key, emission, correction, pdf, true, $" et envoyée à {dest}");
    }

    /// Enregistrement du suivi, commun aux deux chemins (sans envoi, ou après succès).
    void Finaliser(Document d, string key, FactureEmission.Decision emission,
                   bool correction, string pdf, bool envoye, string suffixeMessage = "")
    {
        // Le dépôt de la fiche n'est PAS modifié par l'émission.
        string message = FactureEmission.Enregistrer(_loc, key, "Depot", emission,
            d.Sujet, _loc.factureDepot?.dateEcheanceISO, pdf, d.Montant, _ribDD?.SelectedId,
            _loc.factureDepot, _initial ? "Facture de dépôt de garantie" : "Facture de révision du dépôt", envoye,
            _initial ? "Facture de dépôt de garantie enregistrée"
                     : "Facture de révision du dépôt enregistrée (le montant du dépôt n'a pas été modifié)");

        _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        LocataireSuiviInline.RefreshFor(_fiche);   // Suivi à jour tout de suite

        if (!correction)
        {
            if (_numeroId != null) _numeroId.text = "";
            RefreshNumero();
        }

        UndoToast.Instance?.ShowInfo(message + suffixeMessage);
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

    // ── Sauvegarde ─────────────────────────────────────────────────────────────

    void SaveFromUI()
    {
        if (_loc == null) return;
        var f = _loc.factureDepot ?? new FactureInfo();
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
        MentionTva.Enregistrer(_mentionTva, _texteTvaDebit, f);
        f.ajouterRetard = _retard.isOn;
        f.emailDest = _emailEnvoi.text;
        f.emailObjet = _emailObjet.text;
        f.emailCorps = _emailCorps.text;
        f.refInterne = _refInterne.text;
        f.depotRappel    = _depotRappel.text;
        f.depotDu        = _depotDu.text;
        f.depotRembourse = _depotRembourse.text;
        f.depotEquilibre = _depotEquilibre.text;
        int.TryParse((_nbPeriodes.text ?? "").Trim(), out int nb);
        f.moisPeriode = Mathf.Max(0, nb);
        f.loyerMontant = Nouveau();
        f.provisionMontant = ParseF(_ancien.text);
        f.objet = "Révision du dépôt de garantie";
        f.saved = true;
        _loc.factureDepot = f;

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
        UIFactory.Text(body.transform, "N° de facture  =  format  +  ID locataire", UITheme.Role.Aide, UITheme.TexteSecondaire);
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
        _numeroPrefixe = UIFactory.Text(pfx.transform, "—", UITheme.Role.Libelle, UITheme.TextePrincipal, true);

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
        var b = UIFactory.Button(parent, label, UITheme.Carte, UITheme.TextePrincipal, 36, UITheme.Role.Action, false);
        UIFactory.Border(b.gameObject);
        UIFactory.LE(b.gameObject, prefW: w, flexW: 0, minH: 36);
        b.onClick.AddListener(onClick);
    }

    TMP_InputField Labeled(VerticalLayoutGroup body, string label)
    {
        UIFactory.Text(body.transform, label, UITheme.Role.Donnee, UITheme.TexteSecondaire);
        return UIFactory.Input(body.transform, label);
    }

    TMP_Text MontRow(VerticalLayoutGroup body, string label)
    {
        var h = UIFactory.HBox(body.transform, 8, false, "Row");
        UIFactory.LE(h.gameObject, minH: 24);
        var l = UIFactory.Text(h.transform, label, UITheme.Role.Donnee, UITheme.TexteSecondaire);
        UIFactory.LE(l.gameObject, flexW: 1);
        return UIFactory.Text(h.transform, "—", UITheme.Role.Donnee, UITheme.TextePrincipal, true, TextAlignmentOptions.Right);
    }

    static string Sanitize(string s) => DossiersDonnees.NomFichier(s);

    // Passe par SaisieNumerique : « . » et « , » y sont interchangeables et les
    // espaces de milliers acceptes (cette copie locale ne gerait que la virgule).
    static float ParseF(string s) => SaisieNumerique.Parse(s);

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static bool TryDate(string s, out DateTime d) => SaisieDate.TryParse(s, out d);
}
