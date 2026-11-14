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
    // Revue 16c (D6-06) : les montants en écriture française (« 1 234,56 »), comme le PDF et Créances, quelle que
    // soit la langue de Windows (la culture courante donnait « 1,234.56 » dans les textes et les e-mails).
    static string N2(float v) => v.ToString("N2", FacturePdfService.FrCulture);

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
    Button _btnSauver;   // décision du 11/11 : « Corriger et envoyer » pendant une correction
    TMP_InputField _nom, _adresse, _siret, _date, _echeance, _numeroId, _refInterne, _sommePhrase, _emailEnvoi;
    // Charges proposées : une case et un montant HT chacune ; les cochées partent
    // ensemble sur UNE facture (une ligne par charge).
    Transform _chargesBox;
    readonly List<(ChargeBatiment c, Toggle t, TMP_InputField inp)> _lignes = new List<(ChargeBatiment, Toggle, TMP_InputField)>();
    TMP_InputField _texteTvaDebit, _tauxTva;
    TMP_InputField _emailObjet, _emailCorps;
    TMP_Text _aideEmail;
    // Revue 17 (D5-10, lot 13) : comme le loyer, les champs de l'e-mail sont ceux de la « copie » (facture qui part aussi par la plateforme,
    // avant la bascule) ou de la notification (après), sinon ceux de l'e-mail de facture ; ils suivent le SIRET et le taux tapés
    // (RefreshPlateforme) ; `_chargement` : LoadIntoUI écrit ces champs, l'écouteur attend la fin.
    bool _modeNotif, _modeCopie, _chargement;
    bool _envoiEnCours;   // empêche un second clic de produire un second envoi
    string _numeroEmis;   // le numéro décidé par FactureEmission.Preparer pendant l'émission (revue 16c, D6-03)
    string NumeroDocument() => _numeroEmis ?? ComposedNumero();
    string _idGarde;      // Revue 17 (2c, décision du 06/11) : l'ID tapé, mis de côté pendant qu'un numéro repris occupe la ligne
    string _autoEcheance; // échéance proposée, suivie tant qu'elle n'est pas saisie à la main (revue 16c, D6-02)

    void SuivreEcheance()
    {
        if (_echeance == null || _echeance.text != _autoEcheance || !TryDate(_date.text, out var d)) return;
        _autoEcheance = d.AddDays(FacturationSuivi.EcheanceAutres(_loc)).ToString("dd/MM/yyyy");
        _echeance.text = _autoEcheance;
    }
    TMP_Text _numeroPrefixe, _numeroAide;
    UIDropdown _ribDD, _enteteDD, _numeroFormatDD, _origineDD;
    GameObject _origineBox;   // « Avoir sur la facture n° » : visible sur un avoir seulement
    FactureEtat _ligneCiblee;   // ligne du suivi cliquée : elle désigne la facture (ses charges) à ouvrir
    string _autoTaux;           // Revue 17 (D5-08, lot 10) : dernier taux de TVA proposé (suivi tant qu'il n'est pas saisi à la main)
    bool _acheteurAutre;        // Revue 17 (D4-01, lot 6) : correction d'une facture d'un autre titulaire (cédant) : son acheteur, pas le réglage du type
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
        // Sur le cloud : le bâtiment (ses charges) ET le locataire sont réservés tant que le
        // panneau est ouvert (étape 4b, décision du 13/10).
        var p = Instance;
        EcritureCloud.Reserver(EcritureCloud.Cibles(fiche, true), "refacturation", () => p.OpenFor(fiche, ligneCiblee),
            () => p != null && p.gameObject.activeSelf && p._fiche == fiche, () => { if (p != null) p.FermerDeForce(); });
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
    void Retour() { if (!FactureEmission.RefuseFermeture(_envoiEnCours)) Close(); }   // revue 16c, D4-02
    // Revue 17 (D4-07, coutures) : fermeture venue du cloud (fiche libérée par un admin, reprise après une coupure, rechargement) : pendant
    // l'envoi, elle attend sa fin (EnvoyerPuisFinaliser). Avant : Close() arrêtait l'envoi entre l'e-mail et l'enregistrement.
    bool _fermerApresEnvoi;
    void FermerDeForce() { if (FactureEmission.RefuseFermeture(_envoiEnCours)) _fermerApresEnvoi = true; else Close(); }   // même message que « Retour »
    void OnDisable() { FactureEmission.NoterEnvoi(ref _envoiEnCours, false); _fermerApresEnvoi = false; }   // fermé pendant un envoi : pas bloqué ensuite
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
        back.onClick.AddListener(Retour);
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
        _siret.onValueChanged.AddListener(_ => RefreshPlateforme());   // Revue 17 (D5-10, lot 13) : le SIRET tapé décide aussitôt de la plateforme

        // L'écran suit l'ordre du DOCUMENT IMPRIMÉ : en-tête, contenu, pied de
        // règlement. Dans chaque carte, le champ qui change à chaque facture précède
        // celui qui ne bouge qu'une fois par an (RIB, format du numéro). Même ordre
        // dans les quatre panneaux : seule la carte du type varie.

        // ── 1. En-tête du document ──
        var e = UIFactory.Section(content, "En-tête du document", CoBleu, CoBleuL);
        _date = Labeled(e, "Date");
        _date.onValueChanged.AddListener(_ => { RefreshNumero(); SuivreEcheance(); });
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
        _tauxTva = Labeled(g, "Taux de TVA (%)");   // 02/11
        _tauxTva.onValueChanged.AddListener(_ => { RefreshTotaux(); RefreshPlateforme(); });   // Revue 17 (D5-08, lot 10 ; D5-10, lot 13) : la plateforme suit le taux du document

        // ── 3. Règlement (pied du document) ──
        // L'échéance, la phrase qu'elle alimente et le RIB sont voisins : le lien se
        // voit, d'où la mention « (défaut de la phrase de règlement) » retirée du
        // libellé de l'échéance — elle ne servait qu'à compenser l'éloignement.
        var p = UIFactory.Section(content, "Règlement", CoReglement, CoReglementL);
        _echeance = Labeled(p, "Date d'échéance");
        _echeance.onValueChanged.AddListener(_ => { RefreshSommeDefault(); RefreshEntetePreview(); });   // coutures (10/11) : l'aperçu de l'entête suit l'échéance ({echeance})
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
        _aideEmail = UIFactory.Text(o.transform, EmailService.Aide(false, false), UITheme.Role.Aide, UITheme.TexteSecondaire);   // lot 13 : suit le mode
        _emailEnvoi = Labeled(o, "Email d'envoi");

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
        _btnSauver = save;
        // Revue 17 (D5-17, coutures) : un taux de TVA inhabituel tapé pour cette facture, autre que celui du bail ou de la facture
        // corrigée, se confirme avant l'émission (même règle que le loyer : Tva.ConfirmerTaux).
        save.onClick.AddListener(() => Tva.ConfirmerTaux(TauxFacture(), Tva.TauxPropose(_loc, null, Corrigee()), SauvegarderEtEnvoyer));
    }

    // ── Chargement ─────────────────────────────────────────────────────────────

    void LoadIntoUI()
    {
        if (_loc == null) return;
        var f = _loc.factureRefac;
        _chargement = true;   // Revue 17 (D5-10, lot 13) : le SIRET et le taux posés ici ne déclenchent pas RefreshPlateforme, appelé en dernier
        // (le titre, avec le nom du locataire, est posé par RefreshNumero, plus bas : Revue 17, D4-10)

        _nom.text     = !string.IsNullOrEmpty(f?.destNom)     ? f.destNom     : (_loc.Name ?? "");
        _adresse.text = !string.IsNullOrEmpty(f?.destAdresse) ? f.destAdresse : (_loc.adresseLocataire ?? "");
        _siret.text   = !string.IsNullOrEmpty(f?.destSiret)   ? f.destSiret   : (_loc.siretNumber ?? "");
        // Revue 17 (D4-01, lot 6) : la correction d'une facture d'un autre titulaire (un cédant) reprend son acheteur et son adresse
        // d'envoi, gardés sur la ligne et sa cession ; le réglage du type et la fiche sont passés au cessionnaire.
        var acheteur = Cessions.AcheteurDe(_loc, _ligneCiblee);
        _acheteurAutre = acheteur != null;
        if (_acheteurAutre) { _nom.text = acheteur[0] ?? ""; _siret.text = acheteur[1] ?? ""; _adresse.text = acheteur[2] ?? ""; }

        _ribDD.SetOptions(
            R.ribs.Select(r => string.IsNullOrWhiteSpace(r.name) ? "(RIB)" : r.name).ToList(),
            R.ribs.Select(r => r.id).ToList(), f?.ribId);
        _enteteDD.SetOptions(
            R.entetes.Select(e => string.IsNullOrWhiteSpace(e.nom) ? "(entête)" : e.nom).ToList(),
            R.entetes.Select(e => e.id).ToList(), ReglageService.EnteteChoisi(f?.enteteId, "Refac"));

        DateTime now = DateTime.Today;
        // Revue 16c (D6-02) : la date d'aujourd'hui et une échéance à 30 jours qui suit la date tant qu'elle n'est pas
        // saisie à la main — et non celles de la facture précédente du même type (PDF daté de février en juin, « Impayé »
        // dès l'envoi).
        _numeroEmis = null;
        _date.text = now.ToString("dd/MM/yyyy");
        _autoEcheance = now.AddDays(FacturationSuivi.EcheanceAutres(_loc)).ToString("dd/MM/yyyy");
        _echeance.text = _autoEcheance;
        _autoSomme = DefaultSomme();
        // Revue 17 (06/11) : une phrase d'usine mémorisée (ancienne date) cède la place à celle de cette facture.
        _sommePhrase.text = string.IsNullOrEmpty(f?.sommePhrase) || FactureEmission.EstPhraseUsine(f.sommePhrase) ? _autoSomme : f.sommePhrase;

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

        _idGarde = null;   // Revue 17 (2c) : rien de mis de côté d'une ouverture précédente
        _numeroId.text = f != null && !string.IsNullOrEmpty(f.numeroId) ? f.numeroId : "";
        RefreshNumero();

        MentionTva.Charger(_mentionTva, _texteTvaDebit, f);
        ChargerTva(f);   // 02/11
        _retard.isOn = f?.ajouterRetard ?? true;
        _emailEnvoi.text = _acheteurAutre ? (acheteur[3] ?? "") : !string.IsNullOrEmpty(f?.emailDest) ? f.emailDest : (_loc.emailLocataire ?? "");   // lot 6
        // Revue 17 (D5-10, lot 13) : la partie « plateforme » (case retard forcée, mode et champs de l'e-mail, ligne « Envoi ») est posée en
        // dernier, une fois le SIRET et le taux connus, par la même méthode qui suit ensuite ce qui est tapé (comme le loyer).
        _chargement = false;
        RefreshPlateforme(chargement: true);

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
            SaisieNumerique.Montant(inp);   // retour BN2 (04/11) : la virgule aussi
            inp.text = MontantPropose(c).ToString("0.00", CultureInfo.InvariantCulture);
            t.onValueChanged.AddListener(_ => { RefreshDeduit(); RefreshTotaux(); RefreshNumero(); RefreshTauxDefault(); });   // Revue 17 (2c) : RefreshNumero rafraîchit aussi l'entête ; (D5-08, lot 10) le taux suit la facture visée
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
        => FacturationSuivi.AFacture(_loc, "refac-" + c.id, out var rec) && rec.montant != 0f   // Revue 17 (2c) : bloquée comprise
           ? Tva.Ht(rec.montant, rec.tauxTva) : Part(c);   // 02/11 : au taux de l'époque

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
        var cles = charges.Select(c => "refac-" + c.id).ToList();
        if (FacturationSuivi.Regroupable(loc, cles, out cle, out var oubliees))
            // Revue 16c (D4-12, décision du 31/10) : une facture payée ne reçoit plus de charge.
            return FacturationSuivi.ElargitUneFacturePayee(loc, cle, cles)
                ? "Cette facture est déjà payée : elle ne peut plus recevoir de charge. Décochez celles déjà facturées et faites une nouvelle facture pour les charges ajoutées."
                : null;
        if (oubliees == null)
            return "Ces charges sont déjà sur des factures différentes : corrige chacune depuis le suivi, "
                 + "ou décoche celles déjà refacturées.";
        // Les lignes portent le libellé de la facture entière : on compte les charges.
        return $"Cette facture couvre aussi {oubliees.Count} autre(s) charge(s) : garde-les toutes cochées pour la corriger.";
    }

    /// TTC de chaque charge sur la facture (20 %, au centime) ; la première porte l'écart
    /// d'arrondi pour que la somme des lignes du suivi redonne le total de la facture.
    public static List<float> PartsTtc(IList<float> hts, float ttcTotal, float taux = 20f)
    {
        var res = hts.Select(h => Tva.Ttc(h, taux)).ToList();   // 02/11 : au taux du bail
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

    // ── Justificatifs (Revue 17, D6-20, coutures ; décision du 09/11) ──────────
    // La facture imprime « Justificatif joint : … » : l'e-mail joint maintenant le PDF de chaque charge cochée, depuis le formulaire,
    // à l'envoi au lancement et avec la notification d'après la bascule. Avant : la facture seule, dans tous les cas.

    /// Les justificatifs d'une refacturation : le PDF de chaque charge qui en a un, sous le nom actuel du bâtiment (chemin sur ce poste).
    public static List<string> Justificatifs(string nomBatiment, IEnumerable<ChargeBatiment> charges)
        => (charges ?? Enumerable.Empty<ChargeBatiment>()).Where(c => !string.IsNullOrEmpty(c?.pdfPath))
               .Select(c => DossiersDonnees.PdfCharge(nomBatiment, c.pdfPath)).ToList();

    /// Les pièces de l'e-mail : la facture (`pdfFacture` ; vide : la notification n'a pas de PDF de facture), puis les justificatifs
    /// quand la case « Joindre le justificatif » est cochée (`joindre`). Un justificatif introuvable sur ce poste ne bloque pas
    /// l'envoi (une pièce absente ferait refuser tout l'e-mail) : il est rendu dans `absents`, par son nom, et l'appelant le dit.
    /// `existe` : injecté pour que la règle se teste sans disque.
    public static List<string> PiecesJointes(string pdfFacture, IEnumerable<string> justificatifs, bool joindre, out List<string> absents,
                                             Func<string, bool> existe = null)
    {
        var pieces = new List<string>();
        absents = new List<string>();
        if (!string.IsNullOrWhiteSpace(pdfFacture)) pieces.Add(pdfFacture);
        if (!joindre) return pieces;
        if (existe == null) existe = File.Exists;
        foreach (var j in justificatifs ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(j) || pieces.Contains(j)) continue;
            if (existe(j)) pieces.Add(j);
            else absents.Add(Path.GetFileName(j));
        }
        return pieces;
    }

    /// Les pièces d'une ligne du suivi (envoi au lancement, notification) : pour une refacturation, ses charges se retrouvent par les
    /// clés « refac-&lt;id&gt; » de la facture (même PDF), et la case par le réglage du type (FactureInfo.joindrePj, enregistré à
    /// l'émission). Tout autre type : la facture seule.
    /// ponytail: la case est celle de la DERNIÈRE refacturation enregistrée pour ce locataire ; la garder sur la ligne si deux
    /// refacturations préparées à l'avance doivent partir avec des choix différents.
    public static List<string> PiecesDeLigne(Locataire loc, FactureEtat rec, Batiment bat, string pdfFacture, out List<string> absents)
    {
        var charges = rec?.type != "Refac" || bat?.charges == null ? null
            : FacturationSuivi.MemeFacture(loc, rec.key).DefaultIfEmpty(rec)
                  .Select(r => bat.charges.FirstOrDefault(c => c != null && c.id == ChargeDeCle(r.key)));
        return PiecesJointes(pdfFacture, Justificatifs(bat?.Name, charges), loc?.factureRefac?.joindrePj ?? false, out absents);
    }

    /// Ce qui est dit quand un justificatif n'a pas pu être joint ; null : rien à dire.
    public static string AbsentsDits(List<string> absents)
        => absents == null || absents.Count == 0 ? null
         : absents.Count == 1 ? $"Justificatif introuvable sur ce poste, non joint : {absents[0]}."
         : $"Justificatifs introuvables sur ce poste, non joints : {string.Join(", ", absents)}.";

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
        _tHT.text  = $"{N2(ht)} €";
        float taux = TauxFacture();   // 02/11 : le taux du bail (0 si exonéré)
        _tTVA.text = $"{N2(Tva.Montant(ht, taux))} €";
        _tTTC.text = $"{N2(Tva.Ttc(ht, taux))} €" + (ht < -0.005f ? "  — avoir (remboursement au locataire)" : "");   // pas de « ⚠ » : absent de la police
        // BK (02/11) : sans TVA, une seule ligne « Total » ; sinon la ligne TVA porte le taux de la facture.
        UIFactory.LigneMontant(_tHT, taux > 0f);
        UIFactory.LigneMontant(_tTVA, taux > 0f, Tva.Libelle(taux));
        UIFactory.LigneMontant(_tTTC, true, taux > 0f ? "Total TTC" : "Total");
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

    // Revue 17 (2c, décision du 06/11) : la facture des charges cochées a déjà son numéro : la ligne montre, grisé, celui que
    // l'émission imprimera (le même, ou « … corrigée(X) » si elle est partie), et l'ID n'est plus modifiable.
    // Un numéro décidé par une émission finie ou abandonnée ne vaut plus : l'aperçu et l'entête suivent la ligne.
    void RefreshNumero()
    {
        if (_loc == null) return;
        _numeroEmis = null;
        DateTime d = TryDate(_date != null ? _date.text : "", out var dd) ? dd : DateTime.Today;
        string fmt = _numeroFormatDD != null ? _numeroFormatDD.SelectedId : "AMN";
        string repris = FactureEmission.NumeroRepris(_loc, CleFacture());
        if (_numeroPrefixe != null && _numeroId != null)
            FactureEmission.AfficherNumero(_numeroAide, _numeroPrefixe, _numeroId, repris, NumeroPrefixe(fmt, d), _loc.factureSeq,
                                           "N° de facture — repris : cette facture a déjà ce numéro", ref _idGarde);
        // Revue 17 (D4-10, coutures) : le titre annonce la correction, comme le loyer (la règle de l'émission : SeraUneCorrection), et
        // suit les charges cochées. Avant : rien ne disait qu'une « corrigée(X) » allait partir.
        if (_titre != null) _titre.text = $"Refacturation{(FactureEmission.SeraUneCorrection(_loc, CleFacture()) ? " (correction)" : "")} · {_loc.Name ?? ""}";
        FactureEmission.NommerBouton(_btnSauver, FactureEmission.SeraUneCorrection(_loc, CleFacture()));   // décision du 11/11
        RefreshEntetePreview();
    }

    /// Revue 17 (2c) : la ligne qui portera la facture, comme à l'émission (Regroupement) ; null si les charges cochées sont refusées.
    string CleFacture() => Regroupement(_loc, Selection().Select(x => x.c).ToList(), out string cle) == null ? cle : null;

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
            numero = NumeroDocument(),
            loyerHT = ht,
            tva = Tva.Montant(ht, TauxFacture()),
            ttc = Tva.Ttc(ht, TauxFacture()),
            // Revue 17 (D6-08) : {echeance} remplie, comme à l'envoi au lancement (avant : vide dans l'e-mail et le PDF).
            echeance = TryDate(_echeance != null ? _echeance.text : "", out var e) ? e.ToString("dd/MM/yyyy") : "",
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
        // Revue 17 (D6-20, coutures) : la ligne ne nomme que ce qui partira avec l'e-mail : case cochée, PDF présent sur ce poste.
        var pjs = PiecesJointes(null, Justificatifs(_bat?.Name, sel.Select(x => x.c)), _pj.isOn, out _).Select(x => Path.GetFileName(x)).ToList();
        if (pjs.Count > 0)
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
            numero = NumeroDocument(),
            subtitle = $"{(ht < -0.005f ? "Avoir sur refacturation" : "Refacturation")} : {Noms(sel)}",
            bodyHtml = body,
            provision = 0f, totalHT = ht, tva = Tva.Montant(ht, TauxFacture()), ttc = Tva.Ttc(ht, TauxFacture()),
            masquerTva = TauxFacture() <= 0f, tvaLibelle = Tva.Libelle(TauxFacture()),   // 02/11 : exonéré = total seul
            tvaDebit = TauxFacture() <= 0f || MentionTva.Imprimee(_mentionTva), retard = _retard.isOn,
            // Résolue comme l'entête : le menu « / » propose des variables, elles
            // doivent donc être remplacées et non imprimées telles quelles.
            // Filet de sécurité : une phrase « À NOUS RÉGLER » laissée sur un avoir est
            // remplacée par la phrase de remboursement (même règle que la régul).
            sommePhrase = FactureVarResolver.Resolve(FactureEmission.PhraseSomme(_sommePhrase.text, ht), _loc, _bat, ctx),
            texteTvaDebit = TauxFacture() <= 0f ? Tva.Mention(R) : FactureVarResolver.Resolve(MentionTva.Phrase(_mentionTva, _texteTvaDebit), _loc, _bat, ctx),
            ribTitulaire = rib?.titulaire, ribDomiciliation = rib?.domiciliation,
            ribNum = rib?.rib, ribIban = rib?.iban, ribBic = rib?.bic,
            legal = R.phraseRetard, escompte = R.phraseEscompte,
            foot1 = foot.Length > 0 ? foot[0] : "",
            foot2 = foot.Length > 1 ? foot[1] : "",
        };
    }

    string FactureDir() => DossiersDonnees.DossierFactures(
        _fiche.batimentPrefabOrigin.getName(), _loc.Name);

    void GenererApercu()
    {
        // Revue 17 (D6-05) : une date vide ou illisible est refusée au centre, le formulaire reste ouvert avec la saisie. Avant,
        // SaveFromUI l'enregistrait vide : facture et nom du PDF au jour, échéance du jour dans le suivi.
        if (SaisieDate.RefusFacture(_date.text, _echeance.text) is string refusDate) { ConfirmDialog.Erreur(refusDate); return; }
        SaveFromUI();
        var d = BuildData();
        string png = DossiersDonnees.Apercu("apercu_refac.png");
        if (FacturePdfService.GeneratePreviewPng(d, png, out string err)) ShowPreview(png);
        else ConfirmDialog.Erreur("Échec de l'aperçu : " + err);
    }

    void SauvegarderEtEnvoyer()
    {
        // L'envoi est asynchrone : sans cette garde, un double clic partirait deux fois.
        if (_envoiEnCours) { UndoToast.Instance?.ShowInfo("Un envoi est déjà en cours."); return; }
        if (SaisieDate.RefusFacture(_date.text, _echeance.text) is string refusDate) { ConfirmDialog.Erreur(refusDate); return; }   // Revue 17 (D6-05)

        SaveFromUI();
        var sel = Selection();
        // Une facture neuve, ou la correction d'UNE facture existante avec toutes ses
        // charges — `key` est la ligne qui porte la facture.
        string refus = Regroupement(_loc, sel.Select(x => x.c).ToList(), out string key);
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }
        // Déjà refacturée → version « corrigée(X) » : même numéro, aucune nouvelle séquence consommée, PDF d'origine
        // conservé. Décidé AVANT de construire le document (revue 16c, D6-03 : {numero} de l'entête et de l'e-mail).
        var emission = FactureEmission.Preparer(_loc, key, ComposedNumero());
        if (emission.Refus != null) { ConfirmDialog.Erreur(emission.Refus); return; }   // D4-04 : numéro déjà pris
        _numeroEmis = emission.NumeroFacture;
        var d = BuildData();
        // Revue 17 (D6-19, coutures ; décision du 09/11) : un total nul est refusé, comme pour le loyer : rien à facturer (avant : émis, il
        // prenait un numéro). Revue 17 (D6-15) : sans RIB, le relevé d'identité bancaire serait vide. Les deux avant le PDF.
        if (FactureEmission.RefusTotalNul(d.ttc, d.subtitle) is string refusNul) { ConfirmDialog.Erreur(refusNul); return; }
        if (FactureEmission.RefusSansRib(ReglageService.GetRib(_ribDD?.SelectedId)) is string refusRib) { ConfirmDialog.Erreur(refusRib); return; }
        string dir = FactureDir();
        DateTime dt = TryDate(_date.text, out var dd) ? dd : DateTime.Today;
        bool correction = emission.Correction;
        // BI8 (02/11) : un avoir cite les factures qu'il corrige (BR-FR-CO-05) : la version précédente, sinon la facture d'origine
        // choisie et les loyers de l'année de la charge (leurs provisions la couvrent).
        int anneeCharge = FactureElectronique.Annee(sel.Count > 0 ? sel[0].c.dateISO : null);
        // Revue 17 (D5-07, lot 11) : la plateforme se décide UNE fois, ici, et suit jusqu'à Finaliser (avant : Concerne relu après l'e-mail).
        bool parPlateforme = FactureElectronique.Concerne(_loc, d.tva, _siret.text);
        // Revue 17 (D5-02) : la correction d'une facture transmise ne part pas en avoir (la plateforme garderait les deux). Lot 11 (D5-07,
        // D5-05, décision du 05/11) : hors plateforme, la correction d'une facture transmise est refusée ici, au centre, avec la raison
        // (avant : elle partait par e-mail sans un mot) ; dès la bascule, une facture qui doit partir par la plateforme aussi.
        refus = FactureElectronique.RefusCorrection(_loc.facturesEtat?.Find(x => x.key == key), d.ttc, d.subtitle, parPlateforme,
                    FactureElectronique.RaisonHorsPlateforme(_loc, d.tva, _siret.text))
             ?? FactureElectronique.RefusObligation(_loc, d.tva, _siret.text, d.subtitle, parPlateforme, DateTime.Today);
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }
        if (parPlateforme && d.ttc < -0.004f
            && FactureElectronique.ReferencesAvoir(_loc, _loc.facturesEtat?.Find(x => x.key == key), anneeCharge, d.factureOrigine).Count == 0)
        {
            // Revue 17 (D5-14, coutures) : la raison suit la règle : une facture ENVOYÉE au locataire (avant : « émettez », faux pour un loyer en attente d'envoi).
            ConfirmDialog.Erreur(FactureElectronique.RefusAvoir(d.subtitle,
                $"Choisissez une facture d'origine déjà envoyée (« Avoir sur la facture n° »), ou envoyez d'abord un loyer {anneeCharge}."));
            return;
        }

        string quoi = sel.Count == 1 ? sel[0].c.nom : $"{sel.Count} charges";
        string type = d.ttc < -0.005f ? "Avoir" : "Refacturation";
        // Le numéro dans le nom : deux refacturations du même mois (« 2 charges ») avaient le même PDF (D4-01).
        string fname = Sanitize($"{type}-{emission.NumeroFichier}-{quoi}-{_nom.text}-{dt:MM-yyyy}{emission.SuffixeFichier}") + ".pdf";
        string pdf = DossiersDonnees.CheminFacture(dir, fname);

        if (!FacturePdfService.GeneratePdf(d, pdf, out string err))
        {
            ConfirmDialog.Erreur("Échec génération PDF : " + err);
            return;
        }

        string png = DossiersDonnees.Apercu("apercu_refac.png");
        if (FacturePdfService.GeneratePreviewPng(d, png, out _)) ShowPreview(png);

        // Revue 17 (D5-10 / D5-26, lot 13) : dès la bascule (1/9/2027), comme le loyer : plus de PDF par e-mail, la facture part par Super PDP
        // après confirmation, puis la notification (FactureElectronique.Notifier). Avant : l'e-mail de facture avec le PDF, puis la notification.
        if (parPlateforme && FactureElectronique.NotificationSeule)
        {
            FactureElectronique.ConfirmerTransmission(_emailEnvoi.text,
                () => Finaliser(d, sel, key, emission, correction, pdf, true, parPlateforme, "  Transmise par la plateforme agréée."));
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
        // Revue 17 (D5-10, lot 13) : par la plateforme, l'e-mail est la « copie pour information » (avant : l'e-mail de facture, deux originaux
        // du même numéro chez le locataire) ; le texte affiché s'il est du vrai mode, sinon celui du vrai mode (EmailService.TexteEnvoi).
        var (eo, ec) = EmailService.TexteEnvoi(_loc.factureRefac, parPlateforme, !_modeNotif && _modeCopie == parPlateforme, _emailObjet.text, _emailCorps.text);
        string objet = FactureVarResolver.Resolve(eo, _loc, _bat, ctx);
        string corps = FactureVarResolver.Resolve(ec, _loc, _bat, ctx);

        // Sans confirmation disponible, rien ne part.
        if (ConfirmDialog.Instance == null)
        {
            ConfirmDialog.Erreur("Confirmation indisponible : rien n'a été envoyé. "
                + "Le PDF est enregistré.");
            return;
        }

        // Revue 17 (D6-20, coutures ; décision du 09/11) : les justificatifs des charges cochées partent avec la facture. La confirmation
        // nomme toutes les pièces, et dit celle qui manque sur ce poste, avant l'envoi : elle ne part pas, l'envoi n'est pas bloqué.
        var pieces = PiecesJointes(pdf, Justificatifs(_bat?.Name, sel.Select(x => x.c)), _pj.isOn, out var absents);
        string manquants = AbsentsDits(absents);
        string noms = string.Join(", ", pieces.Select(x => Path.GetFileName(x)));
        ConfirmDialog.Instance.Show(
            "Envoyer la refacturation par email ?",
            $"À : {dest}\nObjet : {objet}\n{(pieces.Count > 1 ? "Pièces jointes" : "Pièce jointe")} : {noms}\n"
            + (manquants != null ? manquants + "\n" : "") + "\n"
            + "Le message part immédiatement et ne pourra pas être rappelé.",
            () => StartCoroutine(EnvoyerPuisFinaliser(d, sel, key, emission, correction, pdf, parPlateforme, dest, objet, corps, pieces)),
            "Envoyer");
    }

    /// Envoie, attend, et n'enregistre QUE si le message est parti. Un échec laisse
    /// le numéro disponible et la charge NON payée : tant que la facture n'est pas
    /// partie, rien de ce qu'elle emporte ne doit être considéré comme acquis.
    System.Collections.IEnumerator EnvoyerPuisFinaliser(
        FacturePdfService.Data d, List<(ChargeBatiment c, float ht)> sel, string key,
        FactureEmission.Decision emission, bool correction, string pdf, bool parPlateforme,
        string dest, string objet, string corps, List<string> pieces)
    {
        FactureEmission.NoterEnvoi(ref _envoiEnCours, true);   // Revue 17 (D4-07) : compté : rechargement et fermeture de CIPL attendent la fin
        UndoToast.Instance?.ShowInfo($"Envoi en cours vers {dest}…");

        var envoi = EmailService.Envoyer(dest, objet, corps, pieces);   // Revue 17 (D6-20, coutures) : la facture et les justificatifs présents
        while (!envoi.Termine) yield return null;

        FactureEmission.NoterEnvoi(ref _envoiEnCours, false);

        if (!envoi.Succes)
        {
            ConfirmDialog.Erreur("Envoi échoué — " + envoi.Erreur
                + " Le PDF est enregistré, la facture n'est PAS marquée envoyée : tu peux réessayer.");
            if (_fermerApresEnvoi) Close();   // D4-07 : la fermeture demandée pendant l'envoi
            yield break;
        }

        if (FactureEmission.FichePerdue(_bat, dest)) { Close(); yield break; }   // Revue 17 (D4-07, coutures) : fiche vidée pendant l'envoi : dit au centre, rien n'est écrit
        Finaliser(d, sel, key, emission, correction, pdf, true, parPlateforme, $" et envoyée à {dest}", dest);
        if (_fermerApresEnvoi) Close();   // Revue 17 (D4-07, coutures) : la fermeture demandée pendant l'envoi, une fois la facture enregistrée
    }

    /// Enregistrement du suivi, commun aux deux chemins (sans envoi, ou après succès).
    /// `dest` (Revue 17, D4-01, lot 6) : l'adresse à laquelle l'e-mail est réellement parti ; sinon celle du formulaire.
    /// `parPlateforme` (Revue 17, D5-07, lot 11) : décidé par SauvegarderEtEnvoyer, le même pour le refus, l'e-mail et la facture électronique.
    void Finaliser(FacturePdfService.Data d, List<(ChargeBatiment c, float ht)> sel, string key,
                   FactureEmission.Decision emission, bool correction, string pdf, bool envoye, bool parPlateforme,
                   string suffixeMessage = "", string dest = null)
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
        var parts = PartsTtc(sel.Select(x => x.ht).ToList(), d.ttc, TauxFacture());
        bool avoir = d.ttc < -0.005f;
        // Même libellé sur chaque ligne : le suivi n'en affiche qu'une par facture.
        string libelle = LibelleFacture(sel.Select(x => x.c.nom).ToList(), avoir);
        var avant = _loc.facturesEtat?.Find(x => x.key == key);   // lue avant l'enregistrement : une correction transmise la cite
        // Revue 17 (D5-14, coutures) : ce qu'un avoir cite se lit AVANT l'enregistrement aussi (BI8) : après, une version jamais partie
        // vient de passer « Envoyé » et l'avoir se citait lui-même.
        var citees = FactureElectronique.ReferencesAvoir(_loc, avant, FactureElectronique.Annee(sel.Count > 0 ? sel[0].c.dateISO : null), d.factureOrigine);
        FacturationSuivi.Solde(_loc, key, out float totalAvantCorrection, out _);   // Revue 17 (D4-03) : avant la correction
        string message = FactureEmission.Enregistrer(_loc, key, "Refac", emission,
            libelle, _loc.factureRefac?.dateEcheanceISO, pdf, parts[0], _ribDD?.SelectedId,
            _loc.factureRefac, avoir ? "Avoir" : "Refacturation", envoye,
            avoir ? "Avoir enregistré (PDF) · à rembourser au locataire"
            : sel.Count > 1 ? "Refacturation enregistrée (PDF) · charges en attente de paiement"
                            : "Refacturation enregistrée (PDF) · charge en attente de paiement");
        // Revue 17 (D4-06, coutures) : une seule règle, partagée avec la régularisation : les autres charges reçoivent aussi l'échéance
        // et le RIB d'une correction, et une charge ajoutée à une correction le numéro de base (FactureEmission.EnregistrerLiee).
        for (int i = 1; i < sel.Count; i++)
            FactureEmission.EnregistrerLiee(_loc, "refac-" + sel[i].c.id, "Refac", emission, libelle, _loc.factureRefac?.dateEcheanceISO, pdf,
                parts[i], _ribDD?.SelectedId, envoye);
        FacturationSuivi.ApresCorrection(_loc, key, totalAvantCorrection);   // Revue 17 (D4-03) : payée, l'écart redevient dû

        foreach (var l in FacturationSuivi.MemeFacture(_loc, key)) l.tauxTva = d.masquerTva ? 0f : TauxFacture();   // 02/11 : taux de l'époque
        // Revue 17 (D4-01, lot 6) : pour qui la facture est préparée (la fiche, ou le cédant d'une facture corrigée) et l'adresse réellement
        // envoyée, sinon celle du formulaire ; lus par le rappel et la notification.
        FacturationSuivi.PoserAcheteur(_loc, key, _acheteurAutre ? _ligneCiblee?.acheteur : null, dest ?? _emailEnvoi.text);
        // Revue 17 (lot 6b, décision du 09/11) : l'avoir garde la facture qu'il cite (« Avoir sur la facture n° ») : son « À rembourser »
        // ne compte que ce qui a été versé sur elle (FacturationSuivi.ARembourser).
        FacturationSuivi.PoserFactureCitee(_loc, key, d.factureOrigine);
        // Décision du 02/11 (BI5) : la facture électronique sur la ligne principale, comme le loyer ; un total négatif part en
        // avoir (FactureEn16931). Hors plateforme (sans SIRET, SCI non reliée…) : rien, la copie PDF part par e-mail comme avant
        // (une facture transmise, elle, a été refusée avant l'envoi : lot 11).
        FactureElectronique.Preparer(_loc.facturesEtat?.Find(x => x.key == key), avant,
            parPlateforme, new FactureEn16931.Entree
            {
                numero = d.numero, dateISO = BuildContext().date.ToString("yyyy-MM-dd"), echeanceISO = _loc.factureRefac?.dateEcheanceISO,
                acheteurNom = d.clientNom, acheteurSiren = SocieteLocataire.Siren(FactureElectronique.SiretAcheteur(_loc, _siret.text)),
                acheteurSiret = FactureElectronique.Siret14(FactureElectronique.SiretAcheteur(_loc, _siret.text)), acheteurAdresse = _adresse.text,
                lignes = FactureEn16931.Lignes(d), mention = d.tvaDebit ? d.texteTvaDebit : null, penalites = d.legal, escompte = d.escompte,
                tauxTva = TauxFacture(),
                references = citees,
            });
        _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        LocataireSuiviInline.RefreshFor(_fiche);   // Suivi à jour tout de suite
        FactureElectronique.Rattraper(_fiche.batimentPrefabOrigin, _loc);

        // Revue 17 (2c, décision du 06/11) : la ligne « N° de facture » reprend le numéro de la facture, correction comprise (la
        // suivante sera « corrigée(2) »), et l'aperçu avec elle (avant : il gardait le numéro émis).
        if (!correction)
        {
            if (_numeroId != null) _numeroId.text = "";
            LoadIntoUI();   // recharge la liste (la charge payée disparaît)
        }
        RefreshNumero();
        ChargerTva(_loc.factureRefac);   // Revue 17 (D5-08, lot 10) : le taux proposé repart de la facture telle qu'émise (voir le loyer)

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
        // Revue 17 (D4-01, lot 6) : l'acheteur d'une facture d'un autre titulaire (cédant) ne devient pas celui du type.
        if (!_acheteurAutre) { f.destNom = _nom.text; f.destAdresse = _adresse.text; f.destSiret = _siret.text; }
        f.ribId = _ribDD?.SelectedId;
        f.enteteId = _enteteDD?.SelectedId;
        f.dateISO = TryDate(_date.text, out var dt) ? dt.ToString("yyyy-MM-dd") : "";
        f.dateEcheanceISO = TryDate(_echeance.text, out var de) ? de.ToString("yyyy-MM-dd") : "";
        f.sommePhrase = FactureEmission.PhraseAMemoriser(_sommePhrase.text);   // Revue 17 (06/11) : la phrase d'usine suit l'échéance
        f.numeroFormat = _numeroFormatDD?.SelectedId ?? "AMN";
        f.numeroId = (_numeroId.text ?? "").Trim();
        f.numero = ComposedNumero();
        if (Corrigee() == null) MentionTva.Enregistrer(_mentionTva, _texteTvaDebit, f);   // décision du 10/11 : la mention choisie pendant une correction ne vaut que pour elle, comme le taux
        if (Corrigee() == null) f.tauxTva = TauxAMemoriser();   // 02/11 ; Revue 17 (D5-08, lot 10) : le taux tapé pendant une correction ne vaut que pour elle
        f.ajouterRetard = _retard.isOn;
        if (!_acheteurAutre) f.emailDest = _emailEnvoi.text;   // lot 6
        // Revue 17 (D5-10, lot 13) : dans la paire du mode affiché (facture, « copie », notification) ; vide : suit le modèle des Réglages.
        EmailService.Garder(f, _modeCopie || _modeNotif, _modeNotif, _emailObjet.text, _emailCorps.text);
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
        _numeroAide = UIFactory.Text(body.transform, "N° de facture  =  format  +  ID locataire", UITheme.Role.Aide, UITheme.TexteSecondaire);
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
        // Revue 17 (2c, décision du 06/11) : un ID retapé après une émission abandonnée : l'entête et l'aperçu le suivent.
        _numeroId.onValueChanged.AddListener(_ => { _numeroEmis = null; RefreshEntetePreview(); });
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
        // 30/10 : une surface transparente capte la molette aussi dans les vides entre les sections (sinon elle se perdait sur le fond).
        if (viewport.GetComponent<Graphic>() == null) viewport.gameObject.AddComponent<Image>().color = Color.clear;
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


    /// TVA de cette facture (02/11) : le taux tapé dans le formulaire, à défaut celui proposé (Tva.TauxPropose : la facture corrigée,
    /// sinon le bail ou l'entreprise). Revue 17 (D5-08, lot 10) : un 0 tapé compte (une facture taxée peut se corriger exonérée), et
    /// plus de 0 forcé par le bail du jour (la correction d'une facture taxée d'un bail devenu exonéré partait sans TVA ; le TTC
    /// de « Refaire » était recalculé au taux du jour sur un HT relu au taux de l'époque).
    float TauxFacture()
        => _tauxTva != null && !string.IsNullOrWhiteSpace(_tauxTva.text) && SaisieNumerique.TryParse(_tauxTva.text, out float t) && t >= 0f
           ? t : Tva.TauxPropose(_loc, null, Corrigee());

    /// Le taux tapé n'est mémorisé que s'il diffère de celui du bail ou de l'entreprise (sinon il suivrait un changement de réglage).
    float TauxAMemoriser() => Tva.Soumis(_loc) && SaisieNumerique.TryParse(_tauxTva.text, out float t) && t > 0f
                              && Mathf.Abs(t - Tva.Taux(_loc)) > 0.001f ? t : 0f;

    /// Revue 17 (D5-08, lot 10) : la facture PARTIE que le formulaire corrige (la ligne qui portera la facture, comme le numéro repris :
    /// CleFacture, donc suivie à chaque charge cochée ou décochée), sinon null : son taux imprimé est proposé, et le taux tapé ne vaut
    /// que pour elle (SaveFromUI ne le mémorise pas sur la facture du type).
    FactureEtat Corrigee()
    {
        if (_loc?.facturesEtat == null) return null;
        string cle = CleFacture();
        var ligne = cle == null ? null : _loc.facturesEtat.Find(x => x.key == cle);
        return Tva.FacturePartie(ligne) ? ligne : null;
    }

    /// Le taux proposé est celui du document (Tva.TauxPropose). Correction d'une facture partie : son taux imprimé, champ montré même à
    /// 0 (modifiable vers ou depuis 0) ; facture neuve d'un bail exonéré : le menu « Mention TVA » et le taux disparaissent (la mention
    /// d'exonération s'imprime d'office). Revue 17 (D5-08, lot 10) : avant, le taux du jour, et le champ caché si le bail était devenu exonéré.
    void ChargerTva(FactureInfo f)
    {
        var corrigee = Corrigee();
        bool soumis = corrigee != null || Tva.Soumis(_loc);
        if (_tauxTva != null)
        {
            UIFactory.MontrerAvecLibelle(_tauxTva.gameObject, soumis);   // 02/11 (BK2) : le champ et son libellé, pas la section entière
            _autoTaux = soumis ? Tva.TauxPropose(_loc, f, corrigee).ToString("0.##", FacturePdfService.FrCulture) : "";
            _tauxTva.text = _autoTaux;
        }
        if (_mentionTva != null) UIFactory.MontrerAvecLibelle(_mentionTva.gameObject, soumis);
        if (_texteTvaDebit != null && _mentionTva != null) _texteTvaDebit.transform.parent.gameObject.SetActive(soumis && MentionTva.Imprimee(_mentionTva));
    }

    /// Revue 17 (D5-08, lot 10) : la facture visée vient de changer (charges cochées) : le taux proposé la suit, tant qu'il n'a pas été
    /// saisi à la main. Sinon, une facture NEUVE faite après une correction sans fermer le formulaire gardait le taux de l'époque.
    void RefreshTauxDefault() { if (_loc != null && _tauxTva != null && _tauxTva.text == _autoTaux) ChargerTva(_loc.factureRefac); }

    /// La ligne « Envoi : … » suit le taux du document et le SIRET affiché (Revue 17, D5-08, lot 10 ; avant : posée une fois au chargement).
    /// Lot 13 (D5-10) : après la bascule, elle annonce la notification, comme le loyer.
    void RefreshModeEnvoi()
    {
        if (_modeInfo == null || _loc == null || _siret == null) return;
        _modeInfo.text = FactureElectronique.LibelleEnvoi(_loc, TauxFacture(), _siret.text, FactureElectronique.NotificationSeule);
        _modeInfo.color = FactureElectronique.SansSiret(_loc, TauxFacture(), _siret.text) ? UITheme.Alerte : UITheme.TexteSecondaire;
    }

    /// Revue 17 (D5-10, lot 13) : la partie « plateforme » du formulaire suit le SIRET et le taux tapés, aussitôt, comme le loyer : case retard
    /// (forcée pour la plateforme ou un professionnel, D6-03), mode de l'e-mail (facture, « copie », notification) et ses champs, ligne « Envoi ».
    /// Un texte retouché reste dans la paire de son mode (EmailService.Garder) avant que les champs passent au texte du nouveau mode.
    /// `chargement` : fin de LoadIntoUI, les champs sont posés quoi qu'il en soit, sans rien garder (le texte à l'écran est encore l'ancien).
    void RefreshPlateforme(bool chargement = false)
    {
        if (_loc == null || _retard == null || (_chargement && !chargement)) return;
        var f = _loc.factureRefac;
        bool plateforme = FactureElectronique.Concerne(_loc, TauxFacture(), _siret.text);   // Revue 17 (D5-08, lot 10) : au taux du document
        bool obligatoire = plateforme || FactureElectronique.Professionnel(_loc, _siret.text);   // Revue 17 (D6-03) : professionnel
        if (obligatoire) _retard.isOn = true;   // plateforme : phrase de retard obligatoire (note PMD, règle BR-FR-05 ; décision du 02/11, BI5)
        _retard.interactable = !obligatoire;
        bool notif = plateforme && FactureElectronique.NotificationSeule, copie = plateforme && !notif;
        if (chargement || notif != _modeNotif || copie != _modeCopie)
        {
            if (!chargement) EmailService.Garder(f, _modeCopie || _modeNotif, _modeNotif, _emailObjet.text, _emailCorps.text);
            _modeNotif = notif; _modeCopie = copie;
            (_emailObjet.text, _emailCorps.text) = EmailService.Modeles(f, plateforme, notif);
            _aideEmail.text = EmailService.Aide(plateforme, notif);
        }
        RefreshModeEnvoi();
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
