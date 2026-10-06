using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Écran « Information Facture — Refacturation de charges ».
/// Refacture une ou plusieurs charges du bâtiment au locataire (sa quote-part) sur UNE
/// facture : une ligne par charge dans le tableau, TVA 20 %, justificatifs cités si
/// coché. Dans le suivi, chaque charge garde sa ligne `refac-<id>`, toutes portant le
/// même numéro et le même PDF (comme la régul regroupée : `FacturationSuivi.MemeFacture`).
/// « Sauvegarder et envoyer » produit le PDF, passe les charges « en attente de
/// paiement » et avance le numéro.
public class FactureRefacPanel : MonoBehaviour
{
    public static FactureRefacPanel Instance { get; private set; }

    static readonly Color CoVert = UITheme.Primaire, CoVertL = UITheme.PrimaireClair;
    static readonly Color CoBleu = Hex("#2C3E5E"), CoBleuL = Hex("#DEE3EB");
    // Couleurs de cartes. `CoContenu` et `CoReglement` portent le même rôle — donc la
    // même teinte — dans les quatre panneaux ; seule la carte du type change de couleur.
    // (`CoCharge` s'appelait `CoAmbre` alors qu'il est violet : nom corrigé, l'ambre
    // désigne désormais le règlement dans les quatre fichiers.)
    static readonly Color CoCharge    = Hex("#7A5AA6"), CoChargeL    = Hex("#ECE4F5");   // violet — charges
    static readonly Color CoContenu   = Hex("#5F5E5A"), CoContenuL   = Hex("#E9E6DE");
    static readonly Color CoReglement = Hex("#854F0B"), CoReglementL = Hex("#F6E6C8");

    // Source unique : FactureNumerotation (les quatre panneaux dupliquaient ces listes).
    static List<string> NumFmtLabels => FactureNumerotation.Labels;
    static List<string> NumFmtIds => FactureNumerotation.Ids;

    LocatairePrefab _fiche; Locataire _loc; Batiment _bat;

    TMP_Text _titre, _entetePreview, _modeInfo, _previewHint, _tHT, _tTVA, _tTTC;
    TMP_InputField _nom, _adresse, _siret, _date, _echeance, _numeroId, _refInterne, _sommePhrase, _emailEnvoi;
    // Charges proposées : une case et un montant HT chacune ; les cochées partent
    // ensemble sur UNE facture (une ligne par charge).
    Transform _chargesBox;
    readonly List<(ChargeBatiment c, Toggle t, TMP_InputField inp)> _lignes = new List<(ChargeBatiment, Toggle, TMP_InputField)>();
    TMP_InputField _texteTvaDebit;
    TMP_InputField _emailObjet, _emailCorps;
    bool _envoiEnCours;   // empêche un second clic de produire un second envoi
    TMP_Text _numeroPrefixe;
    UIDropdown _ribDD, _enteteDD, _numeroFormatDD, _origineDD;
    GameObject _origineBox;   // « Avoir sur la facture n° » : visible sur un avoir seulement
    FactureEtat _ligneCiblee;   // ligne du suivi cliquée : elle désigne la facture (ses charges) à ouvrir
    Toggle _retard, _pj, _deduit, _prorata;
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

    public static void OpenRefac(LocatairePrefab fiche) => OpenRefac(fiche, null);

    // `ligneCiblee` : la ligne du suivi sur laquelle on a cliqué (clé « refac-<id> »).
    // Elle impose la CHARGE refacturée — sans elle, le panneau retombait sur la charge
    // mémorisée ou la première impayée, donc sur une autre charge que celle cliquée.
    public static void OpenRefac(LocatairePrefab fiche, FactureEtat ligneCiblee)
    {
        if (fiche == null) return;
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("FactureRefacPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<FactureRefacPanel>();
        }
        Instance.OpenFor(fiche, ligneCiblee);
    }

    void OpenFor(LocatairePrefab fiche, FactureEtat ligneCiblee = null)
    {
        _fiche = fiche;
        _ligneCiblee = ligneCiblee;
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
        _titre = UIFactory.Text(header.transform, "Information Facture — Refacturation", UITheme.Role.Page, UITheme.TextePrincipal, true);
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

        // ── Charge à refacturer (contenu propre au type) ──
        var g = UIFactory.Section(content, "Charges à refacturer", CoCharge, CoChargeL);
        UIFactory.Text(g.transform, "Cochez une ou plusieurs charges : elles partent sur la même facture. "
            + "Montant HT = quote-part du locataire (modifiable).", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        // Arrivée ou départ en cours d'année (décision du 01/10) : au prorata des jours de
        // présence, ou en entier quand les locataires s'arrangent entre eux. Visible
        // seulement si une charge proposée tombe dans une année incomplète.
        _prorata = UIFactory.Toggle(g.transform,
            "Au prorata de sa présence dans l'année (arrivée ou départ en cours d'année) — décochez pour refacturer en entier", true);
        _prorata.onValueChanged.AddListener(_ =>
        {
            foreach (var l in _lignes) l.inp.text = Part(l.c).ToString("0.00", CultureInfo.InvariantCulture);
        });
        _chargesBox = UIFactory.VBox(g.transform, 4, 0, 0, 0, 0, "ChargesBox").transform;
        _tHT  = MontRow(g, "Total HT");
        _tTVA = MontRow(g, "TVA 20 %");
        _tTTC = MontRow(g, "Total TTC");
        _origineBox = UIFactory.VBox(g.transform, 4, 0, 0, 0, 0, "OrigineBox").gameObject;
        UIFactory.Text(_origineBox.transform, "Avoir sur la facture n° (imprimé sous le titre de l'avoir)", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _origineDD = UIDropdown.Create(_origineBox.transform, new List<string> { "— (aucune)" }, new List<string> { "" }, 0, _ => { });
        _pj = UIFactory.Toggle(g.transform, "Joindre le justificatif de la charge (PJ)", true);
        // Locataire à provisions : la charge entre dans sa régularisation (affichée et
        // déduite comme déjà réglée) ou reste hors des provisions (case décochée).
        _deduit = UIFactory.Toggle(g.transform, "Déduite des provisions (figurera sur la régularisation, déjà réglée)", false);

        // La ligne « la TVA est payée sur les débits » s'imprime juste sous ces
        // totaux : sa case vit donc ici, pas dans la carte d'envoi où on ne pensait
        // pas à la chercher. Même règle que sur le panneau Loyer.
        _mentionTva = MentionTva.Creer(g.transform, out _texteTvaDebit);

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

        // Ne reste ici que ce qui concerne l'ENVOI.
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
            "Note : l'envoi réel (Pennylane / email) sera activé après validation — rien n'est émis pour l'instant.",
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
        var f = _loc.factureRefac;

        _titre.text = $"Refacturation · {(_loc.Name ?? "")}";

        _nom.text     = !string.IsNullOrEmpty(f?.destNom)     ? f.destNom     : (_loc.Name ?? "");
        _adresse.text = !string.IsNullOrEmpty(f?.destAdresse) ? f.destAdresse : (_loc.adresseLocataire ?? "");
        _siret.text   = !string.IsNullOrEmpty(f?.destSiret)   ? f.destSiret   : (_loc.siretNumber ?? "");

        _ribDD.SetOptions(
            R.ribs.Select(r => string.IsNullOrWhiteSpace(r.name) ? "(RIB)" : r.name).ToList(),
            R.ribs.Select(r => r.id).ToList(), f?.ribId);
        _enteteDD.SetOptions(
            R.entetes.Select(e => string.IsNullOrWhiteSpace(e.nom) ? "(entête)" : e.nom).ToList(),
            R.entetes.Select(e => e.id).ToList(), ReglageService.EnteteChoisi(f?.enteteId, "Refac"));

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

        ChargerCharges();
        FacturationSuivi.FacturesOrigine(_loc, out var origLabels, out var origIds);
        _origineDD.SetOptions(origLabels, origIds, "");
        _pj.isOn = f?.joindrePj ?? true;
        // Facture rouverte : la case reprend le choix fait à son émission.
        _deduit.SetIsOnWithoutNotify(Selection().Any(x => x.c.FacturationDe(_loc.id)?.deduitProvisions == true));
        RefreshDeduit();

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

    // ── Charges / calculs ──────────────────────────────────────────────────────

    string ChargeLabel(ChargeBatiment c)
    {
        string dstr = DateTime.TryParse(c.dateISO, out var cd) ? cd.ToString("dd/MM/yyyy") : "";
        return string.IsNullOrEmpty(dstr) ? c.nom : $"{c.nom}  ·  {dstr}";
    }

    /// Une ligne par charge proposée : case + montant HT. Cochées d'office : toutes les
    /// charges de la facture cliquée dans le suivi (sa correction) ; sinon aucune.
    void ChargerCharges()
    {
        _lignes.Clear();
        foreach (Transform t in _chargesBox) Destroy(t.gameObject);
        _prorata.SetIsOnWithoutNotify(true);

        var charges = ImpayeesCharges();
        _prorata.gameObject.SetActive(charges.Any(c => ListesCharges.PresencePartielle(_loc, c)));
        // Les charges de la facture cliquée sont REMISES dans la liste : déjà
        // refacturées, `ImpayeesCharges` les avait justement écartées.
        var cochees = ChargesDeLaFacture(_ligneCiblee);
        for (int i = cochees.Count - 1; i >= 0; i--)
            if (!charges.Contains(cochees[i])) charges.Insert(0, cochees[i]);

        if (charges.Count == 0)
        {
            UIFactory.Text(_chargesBox, "Aucune charge à refacturer pour ce locataire.", UITheme.Role.Donnee, UITheme.TexteSecondaire);
            return;
        }
        foreach (var c in charges)
        {
            var row = UIFactory.HBox(_chargesBox, 8, false, "ChRow");
            UIFactory.LE(row.gameObject, minH: 34);
            var t = UIFactory.Toggle(row.transform, ChargeLabel(c), cochees.Contains(c));
            UIFactory.LE(t.gameObject, flexW: 1);
            var inp = UIFactory.Input(row.transform, "HT");
            UIFactory.LE(inp.gameObject, minW: 120, prefW: 120, flexW: 0);
            inp.contentType = TMP_InputField.ContentType.DecimalNumber;
            inp.text = MontantPropose(c).ToString("0.00", CultureInfo.InvariantCulture);
            t.onValueChanged.AddListener(_ => { RefreshDeduit(); RefreshTotaux(); RefreshEntetePreview(); });
            inp.onValueChanged.AddListener(_ => { RefreshTotaux(); RefreshEntetePreview(); });
            _lignes.Add((c, t, inp));
        }
    }

    /// Charges d'une facture du suivi : toutes les lignes `refac-<id>` de même PDF.
    List<ChargeBatiment> ChargesDeLaFacture(FactureEtat ligne)
    {
        var res = new List<ChargeBatiment>();
        if (ligne == null || _bat?.charges == null) return res;
        var cles = FacturationSuivi.MemeFacture(_loc, ligne.key).Select(r => r.key).DefaultIfEmpty(ligne.key);
        foreach (var k in cles)
        {
            string id = ChargeDeCle(k);
            var c = _bat.charges.FirstOrDefault(x => x != null && x.id == id);
            if (c != null && !res.Contains(c)) res.Add(c);   // charge supprimée : ignorée
        }
        return res;
    }

    /// Montant proposé : la part déjà facturée (HT) pour une charge d'une facture
    /// rouverte, sinon la quote-part du locataire (Part).
    float MontantPropose(ChargeBatiment c)
        => FacturationSuivi.EstDejaEmise(_loc, "refac-" + c.id, out var rec) && rec.montant != 0f
           ? Mathf.Round(rec.montant / 1.2f * 100f) / 100f : Part(c);

    /// Quote-part, au prorata de sa présence dans l'année si la case est cochée. Changer
    /// la case recalcule toutes les lignes avec elle, facture rouverte comprise.
    float Part(ChargeBatiment c)
        => _prorata.isOn ? ListesCharges.QuotePartAuProrata(c, _loc, _bat) : QuotePart(c);

    /// Charges cochées et leur montant HT, dans l'ordre de la liste.
    List<(ChargeBatiment c, float ht)> Selection()
        => _lignes.Where(l => l.t != null && l.t.isOn).Select(l => (l.c, ParseF(l.inp.text))).ToList();

    string Noms(List<(ChargeBatiment c, float ht)> sel)
        => sel.Count == 0 ? "Charge" : Liste(sel.Select(x => x.c.nom).ToList());

    /// Une facture couvre des charges jamais refacturées, ou toutes celles d'UNE facture
    /// existante (sa correction, éventuellement élargie) — même règle que la régul
    /// regroupée. Renvoie null si c'est possible (`cle` = ligne qui porte la facture),
    /// sinon la raison du refus.
    public static string Regroupement(Locataire loc, IList<ChargeBatiment> charges, out string cle)
    {
        cle = null;
        if (charges == null || charges.Count == 0) return "Coche au moins une charge à refacturer.";
        if (FacturationSuivi.Regroupable(loc, charges.Select(c => "refac-" + c.id).ToList(), out cle, out var oubliees)) return null;
        if (oubliees == null)
            return "Ces charges sont déjà sur des factures différentes : corrige chacune depuis le suivi, "
                 + "ou décoche celles déjà refacturées.";
        // Les lignes portent le libellé de la facture entière : on compte les charges.
        return $"Cette facture couvre aussi {oubliees.Count} autre(s) charge(s) : garde-les toutes cochées pour la corriger.";
    }

    /// TTC de chaque charge sur la facture (20 %, au centime) ; la première porte l'écart
    /// d'arrondi pour que la somme des lignes du suivi redonne le total de la facture.
    public static List<float> PartsTtc(IList<float> hts, float ttcTotal)
    {
        var res = hts.Select(h => Mathf.Round(h * 1.2f * 100f) / 100f).ToList();
        if (res.Count > 0) res[0] = ttcTotal - res.Skip(1).Sum();
        return res;
    }

    /// « Jardin 2025 et Ménage 2025 », « A, B et C ».
    static string Liste(IList<string> noms)
        => noms.Count <= 1 ? (noms.Count == 1 ? noms[0] : "")
         : string.Join(", ", noms.Take(noms.Count - 1)) + " et " + noms[noms.Count - 1];

    // ponytail: seuil en caractères, pas en pixels — à ajuster si la colonne du suivi change.
    const int LongueurMaxNoms = 40;

    /// Libellé de la facture dans le suivi, où elle tient sur UNE ligne : « Refacturation :
    /// Jardin 2025 et Ménage 2025 », ou « Refacturation : 3 charges » si les noms ne
    /// tiennent pas dans la colonne (retour du 01/10). « Avoir : … » pour un avoir.
    public static string LibelleFacture(IList<string> noms, bool avoir)
    {
        string liste = Liste(noms);
        if (noms.Count > 1 && liste.Length > LongueurMaxNoms) liste = $"{noms.Count} charges";
        return $"{(avoir ? "Avoir" : "Refacturation")} : {liste}";
    }

    /// Identifiant de charge porté par une clé de suivi de refacturation
    /// (« refac-&lt;guid&gt; » → le guid), ou null.
    ///
    /// Découpage par préfixe, jamais par `Split('-')` : un identifiant de charge est
    /// un GUID, il contient lui-même des tirets — un Split rendrait son premier
    /// fragment, donc une charge introuvable.
    public static string ChargeDeCle(string key)
    {
        const string prefixe = "refac-";
        return !string.IsNullOrEmpty(key) && key.StartsWith(prefixe)
            ? key.Substring(prefixe.Length) : null;
    }

    List<ChargeBatiment> ImpayeesCharges()
    {
        var res = new List<ChargeBatiment>();
        if (_bat?.charges == null) return res;
        foreach (var c in _bat.charges)
        {
            if (!c.AFacturerPour(_loc.id)) continue;   // déjà facturée À CE LOCATAIRE (ou réglée) : plus proposée
            if (ListesCharges.Concerne(c, _loc)) res.Add(c);   // désigné ET concerné par la liste de la charge
        }
        return res;
    }

    float QuotePart(ChargeBatiment c) => ListesCharges.QuotePart(c, _loc, _bat);

    // Le locataire verse une provision pour la liste de cette charge.
    bool AProvision(ChargeBatiment c) => ListesCharges.Provision(_loc, ListesCharges.Effective(c.listeId)) > 0f;

    // Case « déduite des provisions » : visible si une charge cochée relève d'une liste
    // provisionnée. Elle ne s'applique qu'à celles-là.
    void RefreshDeduit() => _deduit.gameObject.SetActive(Selection().Any(x => AProvision(x.c)));

    void RefreshTotaux()
    {
        float ht = Selection().Sum(x => x.ht);
        _tHT.text  = $"{ht:N2} €";
        _tTVA.text = $"{ht * .2f:N2} €";
        _tTTC.text = $"{ht * 1.2f:N2} €" + (ht < -0.005f ? "  — avoir (remboursement au locataire)" : "");   // pas de « ⚠ » : absent de la police
        _origineBox.SetActive(ht < -0.005f);
        RefreshSommeDefault();   // la phrase suit le signe du total
    }

    // Total négatif (avoir d'une charge) : c'est nous qui remboursons.
    bool EstAvoir() => Selection().Sum(x => x.ht) < -0.005f;

    // ── Numéro / phrase / aperçu entête ────────────────────────────────────────

    string DefaultSomme()
    {
        if (_lignes.Count > 0 && EstAvoir()) return "SOMME QUI VOUS SERA REMBOURSÉE";
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
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        _entetePreview.text = ent == null || string.IsNullOrEmpty(ent.texte)
            ? "—" : FactureVarResolver.Resolve(ent.texte, _loc, _bat, BuildContext());
    }

    FactureContext BuildContext()
    {
        var sel = Selection();
        float ht = sel.Sum(x => x.ht);
        DateTime d = TryDate(_date.text, out var dd) ? dd : DateTime.Today;
        return new FactureContext
        {
            date = d,
            periode = sel.Count > 0 ? Noms(sel) : "refacturation",
            numero = ComposedNumero(),
            loyerHT = ht,
            tva = ht * .2f,
            ttc = ht * 1.2f,
        };
    }

    // ── Données PDF ────────────────────────────────────────────────────────────

    FacturePdfService.Data BuildData()
    {
        var ctx = BuildContext();
        var rib = ReglageService.GetRib(_ribDD?.SelectedId);
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        var sel = Selection();
        float ht = sel.Sum(x => x.ht);
        string entResolved = ent != null ? FactureVarResolver.Resolve(ent.texte, _loc, _bat, ctx) : "";
        string body = FacturePdfService.BodyHtml(entResolved);
        var pjs = sel.Where(x => !string.IsNullOrEmpty(x.c.pdfPath)).Select(x => Path.GetFileName(x.c.pdfPath)).ToList();
        if (_pj.isOn && pjs.Count > 0)
            body += $"<p>{(pjs.Count > 1 ? "Justificatifs joints" : "Justificatif joint")} : {string.Join(", ", pjs)}</p>";
        var foot = (R.basDePage ?? "").Replace("\r", "").Split('\n');

        return new FacturePdfService.Data
        {
            clientNom = _nom.text,
            clientAdresseHtml = FacturePdfService.AdresseHtml(_adresse.text),
            clientSiret = _siret.text,
            refInterne = _refInterne.text,
            factureOrigine = ht < -0.005f ? _origineDD?.SelectedId : null,
            // Une ligne par charge : la première dans la ligne principale du gabarit, les
            // suivantes dans ses lignes additionnelles (celles des provisions du loyer).
            ligneLabel = sel.Count > 0 ? sel[0].c.nom : "Charge",
            totalPeriode = sel.Count > 0 ? sel[0].ht : 0f,
            lignesProvision = sel.Skip(1).Select(x => new KeyValuePair<string, float>(x.c.nom, x.ht)).ToList(),
            dateStr = ctx.date.ToString("d MMMM yyyy", FacturePdfService.FrCulture),
            numero = ComposedNumero(),
            subtitle = $"{(ht < -0.005f ? "Avoir sur refacturation" : "Refacturation")} : {Noms(sel)}",
            bodyHtml = body,
            provision = 0f, totalHT = ht, tva = ht * .2f, ttc = ht * 1.2f,
            tvaDebit = MentionTva.Imprimee(_mentionTva), retard = _retard.isOn,
            // Résolue comme l'entête : le menu « / » propose des variables, elles
            // doivent donc être remplacées et non imprimées telles quelles.
            // Filet de sécurité : une phrase « À NOUS RÉGLER » laissée sur un avoir est
            // remplacée par la phrase de remboursement (même règle que la régul).
            sommePhrase = FactureVarResolver.Resolve(FactureEmission.PhraseSomme(_sommePhrase.text, ht), _loc, _bat, ctx),
            texteTvaDebit = FactureVarResolver.Resolve(MentionTva.Phrase(_mentionTva, _texteTvaDebit), _loc, _bat, ctx),
            ribTitulaire = rib?.titulaire, ribDomiciliation = rib?.domiciliation,
            ribNum = rib?.rib, ribIban = rib?.iban, ribBic = rib?.bic,
            legal = R.phraseRetard,
            foot1 = foot.Length > 0 ? foot[0] : "",
            foot2 = foot.Length > 1 ? foot[1] : "",
        };
    }

    string FactureDir() => DossiersDonnees.DossierFactures(
        _fiche.batimentPrefabOrigin.getName(), _loc.Name);

    void GenererApercu()
    {
        SaveFromUI();
        var d = BuildData();
        string png = Path.Combine(FactureDir(), "apercu_refac.png");
        if (FacturePdfService.GeneratePreviewPng(d, png, out string err)) ShowPreview(png);
        else ConfirmDialog.Erreur("Échec de l'aperçu : " + err);
    }

    void SauvegarderEtEnvoyer()
    {
        // L'envoi est asynchrone : sans cette garde, un double clic partirait deux fois.
        if (_envoiEnCours) { UndoToast.Instance?.ShowInfo("Un envoi est déjà en cours."); return; }

        SaveFromUI();
        var sel = Selection();
        // Une facture neuve, ou la correction d'UNE facture existante avec toutes ses
        // charges — `key` est la ligne qui porte la facture.
        string refus = Regroupement(_loc, sel.Select(x => x.c).ToList(), out string key);
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }
        var d = BuildData();
        string dir = FactureDir();
        DateTime dt = TryDate(_date.text, out var dd) ? dd : DateTime.Today;

        // Déjà refacturée → version « corrigée(X) » : même numéro, aucune nouvelle
        // séquence consommée, PDF d'origine conservé.
        var emission = FactureEmission.Preparer(_loc, key, d.numero);
        d.numero = emission.NumeroFacture;
        bool correction = emission.Correction;

        string quoi = sel.Count == 1 ? sel[0].c.nom : $"{sel.Count} charges";
        string type = d.ttc < -0.005f ? "Avoir" : "Refacturation";
        string fname = Sanitize($"{type}-{quoi}-{_nom.text}-{dt:MM-yyyy}{emission.SuffixeFichier}") + ".pdf";
        string pdf = Path.Combine(dir, fname);

        if (!FacturePdfService.GeneratePdf(d, pdf, out string err))
        {
            ConfirmDialog.Erreur("Échec génération PDF : " + err);
            return;
        }

        string png = Path.Combine(dir, "apercu_refac.png");
        if (FacturePdfService.GeneratePreviewPng(d, png, out _)) ShowPreview(png);

        // Pas d'envoi demandé : on enregistre, rien ne part. Le bouton s'appelant
        // « Sauvegarder et envoyer », il faut le dire, sinon on attend un mail en vain.
        if (R.modeEnvoi != ModeEnvoi.Email)
        {
            Finaliser(d, sel, key, emission, correction, pdf, false,
                "  Aucun email envoyé : la case « Envoyer par email » est décochée (Options & envoi).");
            return;
        }

        // Refus AVANT la confirmation si quelque chose manque.
        string dest = (_emailEnvoi.text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dest))
        {
            ConfirmDialog.Erreur("Aucune adresse email pour ce locataire. "
                + "Rien n'a été envoyé ; le PDF est enregistré.");
            return;
        }

        string manque = EmailService.CeQuiManque();
        if (manque != null) { ConfirmDialog.Erreur(manque + " Le PDF est enregistré."); return; }

        var ctx = BuildContext();
        string objet = FactureVarResolver.Resolve(
            FacturePdfService.Texte(_emailObjet.text, EmailService.ObjetDefaut), _loc, _bat, ctx);
        string corps = FactureVarResolver.Resolve(
            FacturePdfService.Texte(_emailCorps.text, EmailService.CorpsDefaut), _loc, _bat, ctx);

        // Sans confirmation disponible, rien ne part.
        if (ConfirmDialog.Instance == null)
        {
            ConfirmDialog.Erreur("Confirmation indisponible : rien n'a été envoyé. "
                + "Le PDF est enregistré.");
            return;
        }

        ConfirmDialog.Instance.Show(
            "Envoyer la refacturation par email ?",
            $"À : {dest}\nObjet : {objet}\nPièce jointe : {Path.GetFileName(pdf)}\n\n"
            + "Le message part immédiatement et ne pourra pas être rappelé.",
            () => StartCoroutine(EnvoyerPuisFinaliser(d, sel, key, emission, correction, pdf, dest, objet, corps)),
            "Envoyer");
    }

    /// Envoie, attend, et n'enregistre QUE si le message est parti. Un échec laisse
    /// le numéro disponible et la charge NON payée : tant que la facture n'est pas
    /// partie, rien de ce qu'elle emporte ne doit être considéré comme acquis.
    System.Collections.IEnumerator EnvoyerPuisFinaliser(
        FacturePdfService.Data d, List<(ChargeBatiment c, float ht)> sel, string key,
        FactureEmission.Decision emission, bool correction, string pdf,
        string dest, string objet, string corps)
    {
        _envoiEnCours = true;
        UndoToast.Instance?.ShowInfo($"Envoi en cours vers {dest}…");

        var envoi = EmailService.Envoyer(dest, objet, corps, new[] { pdf });
        while (!envoi.Termine) yield return null;

        _envoiEnCours = false;

        if (!envoi.Succes)
        {
            ConfirmDialog.Erreur("Envoi échoué — " + envoi.Erreur
                + " Le PDF est enregistré, la facture n'est PAS marquée envoyée : tu peux réessayer.");
            yield break;
        }

        Finaliser(d, sel, key, emission, correction, pdf, true, $" et envoyée à {dest}");
    }

    /// Enregistrement du suivi, commun aux deux chemins (sans envoi, ou après succès).
    void Finaliser(FacturePdfService.Data d, List<(ChargeBatiment c, float ht)> sel, string key,
                   FactureEmission.Decision emission, bool correction, string pdf, bool envoye,
                   string suffixeMessage = "")
    {
        // Les charges refacturées passent « en attente de paiement » POUR CE LOCATAIRE,
        // pas « payé » : le virement n'est pas arrivé. Elles sortent de son choix — et
        // donc de sa régularisation — sans toucher la part des autres locataires.
        string aujourdhui = DateTime.Today.ToString("yyyy-MM-dd");
        bool deduit = _deduit.gameObject.activeSelf && _deduit.isOn;
        foreach (var (c, _) in sel)
        {
            c.MarquerFacturee(_loc.id, aujourdhui);
            c.FacturationDe(_loc.id).deduitProvisions = deduit && AProvision(c);
        }

        // Une ligne de suivi par charge, toutes sur la même facture (même numéro, même
        // PDF) : la ligne `key` d'abord, c'est elle qui consomme le numéro.
        sel = sel.OrderBy(x => "refac-" + x.c.id == key ? 0 : 1).ToList();
        var parts = PartsTtc(sel.Select(x => x.ht).ToList(), d.ttc);
        bool avoir = d.ttc < -0.005f;
        // Même libellé sur chaque ligne : le suivi n'en affiche qu'une par facture.
        string libelle = LibelleFacture(sel.Select(x => x.c.nom).ToList(), avoir);
        string message = FactureEmission.Enregistrer(_loc, key, "Refac", emission,
            libelle, _loc.factureRefac?.dateEcheanceISO, pdf, parts[0], _ribDD?.SelectedId,
            _loc.factureRefac, avoir ? "Avoir" : "Refacturation", envoye,
            avoir ? "Avoir enregistré (PDF) · à rembourser au locataire"
            : sel.Count > 1 ? "Refacturation enregistrée (PDF) · charges en attente de paiement"
                            : "Refacturation enregistrée (PDF) · charge en attente de paiement");
        for (int i = 1; i < sel.Count; i++)
        {
            string k = "refac-" + sel[i].c.id;
            if (correction && FacturationSuivi.EstDejaEmise(_loc, k, out _))
                FacturationSuivi.MarquerCorrige(_loc, k, libelle, pdf, parts[i]);
            else
                FacturationSuivi.MarquerEnvoye(_loc, k, "Refac", libelle, _loc.factureRefac?.dateEcheanceISO,
                    d.numero, pdf, parts[i], _ribDD?.SelectedId, FactureEmission.RibNom(_ribDD?.SelectedId), envoye);
        }

        _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        LocataireSuiviInline.RefreshFor(_fiche);   // Suivi à jour tout de suite

        if (!correction)
        {
            if (_numeroId != null) _numeroId.text = "";
            RefreshNumero();
            LoadIntoUI();   // recharge la liste (la charge payée disparaît)
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
        catch (Exception e) { ConfirmDialog.Erreur("Aperçu illisible : " + e.Message); }
    }

    // ── Sauvegarde ─────────────────────────────────────────────────────────────

    void SaveFromUI()
    {
        if (_loc == null) return;
        var f = _loc.factureRefac ?? new FactureInfo();
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
        f.joindrePj = _pj.isOn;
        f.objet = $"Refacturation {Noms(Selection())}";
        f.saved = true;
        _loc.factureRefac = f;

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
