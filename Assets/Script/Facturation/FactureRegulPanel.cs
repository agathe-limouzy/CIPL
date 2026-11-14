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
/// la compare aux provisions déjà versées (celles appelées avec les loyers de l'année) et facture
/// la différence (TVA 20 %). L'aperçu réel est rendu à droite (image), et
/// « Sauvegarder et envoyer » produit le PDF, passe les charges concernées en
/// « payé » et avance le numéro. AUCUN envoi réel ici.
public class FactureRegulPanel : MonoBehaviour
{
    // Revue 16c (D6-06) : les montants en écriture française (« 1 234,56 »), comme le PDF et Créances, quelle que
    // soit la langue de Windows (la culture courante donnait « 1,234.56 » dans les textes et les e-mails).
    static string N2(float v) => v.ToString("N2", FacturePdfService.FrCulture);

    public static FactureRegulPanel Instance { get; private set; }

    static readonly Color CoVert = UITheme.Primaire, CoVertL = UITheme.PrimaireClair;
    static readonly Color CoBleu = Hex("#2C3E5E"), CoBleuL = Hex("#DEE3EB");
    static readonly Color CoViolet = Hex("#5B3E7A"), CoVioletL = Hex("#E9E0F2");
    // Couleurs de cartes. `CoContenu` et `CoReglement` portent le même rôle — donc la
    // même teinte — dans les quatre panneaux ; seule la carte du type change de couleur.
    static readonly Color CoContenu   = Hex("#5F5E5A"), CoContenuL   = Hex("#E9E6DE");
    static readonly Color CoReglement = Hex("#854F0B"), CoReglementL = Hex("#F6E6C8");

    // Source unique : FactureNumerotation (les quatre panneaux dupliquaient ces listes).
    static List<string> NumFmtLabels => FactureNumerotation.Labels;
    static List<string> NumFmtIds => FactureNumerotation.Ids;

    LocatairePrefab _fiche; Locataire _loc; Batiment _bat;

    TMP_Text _titre, _entetePreview, _modeInfo, _previewHint;
    Button _btnSauver;   // décision du 11/11 : « Corriger et envoyer » pendant une correction
    TMP_Text _tCharges, _tProvisions, _tDeja, _tSolde, _tTVA, _tTTC;
    TMP_InputField _nom, _adresse, _siret, _date, _echeance, _numeroId, _refInterne, _sommePhrase, _provisions, _emailEnvoi;
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
    string _autoSomme;   // dernière phrase de règlement auto (suivie tant que non personnalisée)
    string _autoProv;    // dernière provision auto (suit l'année tant que non saisie à la main)
    UIDropdown _ribDD, _enteteDD, _numeroFormatDD, _anneeDD;
    // Listes de charges régularisées ensemble (une case par liste du locataire) : une
    // seule facture couvre toutes celles cochées.
    GameObject _listeLabel;
    Transform _listesBox;
    readonly List<(string id, Toggle t)> _listesCases = new List<(string, Toggle)>();
    FactureEtat _ligneCiblee;   // ligne du suivi cliquée : elle désigne l'année à ouvrir
    string _autoTaux;           // Revue 17 (D5-08, lot 10) : dernier taux de TVA proposé (suivi tant qu'il n'est pas saisi à la main)
    bool _acheteurAutre;        // Revue 17 (D4-01, lot 6) : correction d'une facture d'un autre titulaire (cédant) : son acheteur, pas le réglage du type
    Toggle _retard;
    UIDropdown _mentionTva;
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

    public static void OpenRegul(LocatairePrefab fiche) => OpenRegul(fiche, null);

    // `ligneCiblee` : la ligne du suivi sur laquelle on a cliqué (clé « regul-2025 »).
    // Elle impose l'ANNÉE régularisée — sans elle, le panneau retombait sur l'année
    // mémorisée ou la première disponible, et on refaisait 2024 en croyant faire 2025.
    public static void OpenRegul(LocatairePrefab fiche, FactureEtat ligneCiblee)
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
        // Sur le cloud : le bâtiment (ses charges) ET le locataire sont réservés tant que le
        // panneau est ouvert (étape 4b, décision du 13/10).
        var p = Instance;
        EcritureCloud.Reserver(EcritureCloud.Cibles(fiche, true), "régularisation", () => p.OpenFor(fiche, ligneCiblee),
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
        _titre = UIFactory.Text(header.transform, "Information Facture — Régularisation des charges", UITheme.Role.Page, UITheme.TextePrincipal, true);
        UIFactory.LE(_titre.gameObject, flexW: 1);

        var main = UIFactory.HBox(col.transform, 16, false, "Main");
        UIFactory.LE(main.gameObject, flexH: 1);
        main.childForceExpandHeight = true;

        var left = UIFactory.VBox(main.transform, 12, 0, 0, 0, 0, "Left");
        UIFactory.LE(left.gameObject, flexW: 42);
        left.childForceExpandHeight = false;

        var content = MakeScroll(left.transform);

        // L'écran suit l'ordre du DOCUMENT IMPRIMÉ : en-tête, contenu, pied de
        // règlement. Le RIB était le premier champ alors qu'il s'imprime en dernier,
        // et le « N° interne » venait après le numéro alors qu'il s'imprime au-dessus.
        // Dans chaque carte, le champ qui change à chaque facture (date, montants)
        // précède celui qui ne bouge qu'une fois par an (RIB, format du numéro).
        // Même ordre attendu dans les quatre panneaux : seule la carte du type varie.

        // ── Destinataire ──
        var d = UIFactory.Section(content, "Destinataire", CoVert, CoVertL);
        _nom = Labeled(d, "Nom");
        UIFactory.Text(d.transform, "Adresse", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _adresse = UIFactory.Input(d.transform, "Adresse (plusieurs lignes possibles)", 88, true);
        _siret = Labeled(d, "SIRET");
        _siret.onValueChanged.AddListener(_ => RefreshPlateforme());   // Revue 17 (D5-10, lot 13) : le SIRET tapé décide aussitôt de la plateforme

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

        // ── Régularisation (contenu propre au type) ──
        var g = UIFactory.Section(content, "Régularisation des charges", CoViolet, CoVioletL);
        // Liste de charges : visible seulement si des listes spécifiques existent
        // (Réglages) — sinon tout se passe comme avant, sur la liste générale.
        _listeLabel = UIFactory.Text(g.transform, "Listes de charges à régulariser (une seule facture pour toutes celles cochées)",
            UITheme.Role.Donnee, UITheme.TexteSecondaire).gameObject;
        _listesBox = UIFactory.VBox(g.transform, 4, 0, 0, 0, 0, "ListesBox").transform;
        UIFactory.Text(g.transform, "Année à régulariser (charges impayées)", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _anneeDD = UIDropdown.Create(g.transform, new List<string> { "—" }, new List<string> { DateTime.Today.Year.ToString() }, 0, _ => { RefreshProvisionsAuto(); RefreshCharges(); RefreshNumero(); RefreshTauxDefault(); });   // Revue 17 (D5-08, lot 10) : le taux suit la facture visée
        UIFactory.Text(g.transform, "Charges concernées (quote-part du locataire) :", UITheme.Role.Aide, UITheme.TexteSecondaire);
        var cbox = UIFactory.VBox(g.transform, 4, 0, 0, 0, 0, "ChargesBox");
        _chargesBox = cbox.transform;
        // Revue 17 (lot 16, D6-N5) : le libellé dit ce qui est proposé : les provisions appelées, période par période (ProvisionsAuto).
        _provisions = Labeled(g, "Provisions déjà versées (appelées avec les loyers de l'année)");
        SaisieNumerique.Montant(_provisions);   // retour BN2 (04/11) : la virgule aussi
        _provisions.onValueChanged.AddListener(_ => { RefreshTotaux(); RefreshEntetePreview(); });
        _tCharges    = MontRow(g, "Total des charges (quote-part)");
        _tProvisions = MontRow(g, "Provisions déjà versées");
        _tDeja       = MontRow(g, FacturePdfService.LibelleDejaRefacture);
        _tSolde      = MontRow(g, "Solde HT");
        _tTVA        = MontRow(g, "TVA 20 %");
        _tTTC        = MontRow(g, "Total TTC");
        // Pas de « Avoir sur la facture n° » ici (retiré le 05/10) : une régul qui rembourse
        // solde les provisions de l'année, elle ne corrige pas une facture précise.

        // La ligne « la TVA est payée sur les débits » s'imprime juste sous ces
        // totaux : sa case vit donc ici, pas dans la carte d'envoi où on ne pensait
        // pas à la chercher. Même règle que sur le panneau Loyer.
        _mentionTva = MentionTva.Creer(g.transform, out _texteTvaDebit);
        _tauxTva = Labeled(g, "Taux de TVA (%)");   // 02/11
        _tauxTva.onValueChanged.AddListener(_ => { RefreshCharges(); RefreshPlateforme(); });   // Revue 17 (D5-08, lot 10 ; D5-10, lot 13) : la plateforme suit le taux du document

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

        // ── Options / envoi ──
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

        // ── Colonne droite : aperçu ──
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
        var f = _loc.factureRegul;
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
            R.entetes.Select(e => e.id).ToList(), ReglageService.EnteteChoisi(f?.enteteId, "Regul"));

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

        ChargerListes(f);
        ChargerAnnees(f);

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

        RefreshCharges();
        RefreshEntetePreview();
    }

    // ── Charges / calculs ──────────────────────────────────────────────────────

    int SelectedYear()
    {
        int.TryParse(_anneeDD?.SelectedId, out int y);
        return y > 0 ? y : DateTime.Today.Year - 1;
    }

    /// Années proposées pour la liste choisie, et provisions correspondantes.
    /// `f` = réglage mémorisé (au chargement) ; null quand on change de liste.
    void ChargerAnnees(FactureInfo f)
    {
        // Années disponibles (charges impayées de cette liste concernant ce locataire).
        var years = YearsAvailable();
        if (years.Count == 0) years.Add(DateTime.Today.Year - 1);

        // Année désignée par la ligne cliquée. On la REMET dans la liste si besoin :
        // `YearsAvailable` écarte les charges déjà facturées, donc l'année d'une régul
        // qu'on veut refaire en avait justement disparu.
        int cible = AnneeCiblee();
        if (cible > 0 && !years.Contains(cible)) { years.Add(cible); years.Sort(); years.Reverse(); }

        string selYear = cible > 0 ? cible.ToString()
            : (f != null && f.anneePeriode > 0 && years.Contains(f.anneePeriode) ? f.anneePeriode.ToString() : years[0].ToString());
        _anneeDD.SetOptions(years.Select(y => y.ToString()).ToList(), years.Select(y => y.ToString()).ToList(), selYear);

        // Provisions : mémorisées si saisies POUR CES MÊMES LISTES ET CETTE ANNÉE (celles
        // d'une année pleine ne valent pas pour l'année du départ), sinon les provisions
        // appelées pour ces listes avec les loyers de l'année (ProvisionsAuto).
        var sel = ListesSel();
        bool memo = f != null && f.saved && f.listesRegul != null && f.anneePeriode == SelectedYear()
                    && f.listesRegul.Select(ListesCharges.Effective).OrderBy(x => x).SequenceEqual(sel.OrderBy(x => x));
        _autoProv = ProvisionsAuto().ToString("0.00", CultureInfo.InvariantCulture);
        _provisions.text = memo ? f.provisionMontant.ToString("0.00", CultureInfo.InvariantCulture) : _autoProv;
    }

    // L'année change : la provision proposée suit, sauf si elle a été saisie à la main.
    void RefreshProvisionsAuto()
    {
        string def = ProvisionsAuto().ToString("0.00", CultureInfo.InvariantCulture);
        if (_provisions != null && _provisions.text == _autoProv) _provisions.text = def;
        _autoProv = def;
    }

    /// Une case par liste du locataire. Cochées : celles de la facture cliquée dans le
    /// suivi (toutes ses listes si elle en regroupait plusieurs), sinon la dernière
    /// sélection, sinon la première liste.
    void ChargerListes(FactureInfo f)
    {
        var listes = ListesCharges.DuLocataire(_loc);
        List<string> sel;
        if (_ligneCiblee != null) sel = ListesDeLaFacture(_ligneCiblee);
        else if (f != null && f.listesRegul != null && f.listesRegul.Count > 0)
            sel = f.listesRegul.Select(ListesCharges.Effective).Where(listes.Contains).Distinct().ToList();
        else sel = new List<string>();
        if (sel.Count == 0 && listes.Count > 0) sel.Add(listes[0]);
        foreach (var id in sel) if (!listes.Contains(id)) listes.Add(id);   // la facture cliquée fait foi

        _listesCases.Clear();
        foreach (Transform c in _listesBox) Destroy(c.gameObject);
        foreach (var id in listes)
        {
            var t = UIFactory.Toggle(_listesBox, ListesCharges.Nom(id), sel.Contains(id));
            t.onValueChanged.AddListener(_ => { ChargerAnnees(null); RefreshCharges(); RefreshNumero(); RefreshTauxDefault(); });   // Revue 17 (2c) : RefreshNumero rafraîchit aussi l'entête ; (D5-08, lot 10) le taux suit la facture visée
            _listesCases.Add((id, t));
        }
        bool avecListes = ListesCharges.Specifiques().Count > 0;
        if (avecListes && listes.Count == 0)
            UIFactory.Text(_listesBox, "Aucune liste à régulariser pour ce locataire : renseigne ses dates "
                + "de régularisation dans « Gestion du loyer ».", UITheme.Role.Aide, UITheme.Alerte);
        _listeLabel.SetActive(avecListes);
        _listesBox.gameObject.SetActive(avecListes);
    }

    /// Listes cochées, dans l'ordre (générale d'abord). Sans liste spécifique : la générale.
    List<string> ListesSel()
    {
        // Aucune case : sans liste spécifique, c'est la générale ; avec, c'est que le
        // locataire n'a aucune liste à régulariser — surtout pas la générale par défaut.
        if (_listesCases.Count == 0)
            return ListesCharges.Specifiques().Count == 0 ? new List<string> { "" } : new List<string>();
        return _listesCases.Where(x => x.t != null && x.t.isOn).Select(x => x.id).ToList();
    }

    /// Une facture regroupée couvre des listes SANS facture pour cette année, ou
    /// toutes celles d'UNE facture existante (sa correction, éventuellement élargie).
    /// Mélanger deux factures existantes referait payer des charges déjà facturées ;
    /// en corriger une en décochant une de ses listes laisserait cette liste pointer
    /// sur l'ancien document. Renvoie null si c'est possible — `cle` est alors la
    /// ligne sous laquelle ranger la facture — sinon la raison du refus.
    public static string Regroupement(Locataire loc, IList<string> listes, int year, out string cle)
    {
        cle = null;
        if (listes == null || listes.Count == 0) return "Coche au moins une liste de charges à régulariser.";
        var cles = listes.Select(id => ListesCharges.Cle(id, year)).ToList();
        if (FacturationSuivi.Regroupable(loc, cles, out cle, out var oubliees))
            // Revue 16c (D4-12, décision du 31/10) : une facture payée ne reçoit plus de liste.
            return FacturationSuivi.ElargitUneFacturePayee(loc, cle, cles)
                ? "Cette facture est déjà payée : elle ne peut plus recevoir de liste de charges. Décochez celles déjà facturées et faites une nouvelle facture pour les listes ajoutées."
                : null;
        if (oubliees == null)
            return $"Ces listes ont déjà des factures différentes pour {year} : corrige chacune depuis le suivi, "
                 + "ou décoche celles déjà facturées.";
        return "Cette facture couvre aussi : " + string.Join(", ", oubliees.Select(r => r.libelle))
             + ". Garde ces listes cochées pour la corriger.";
    }

    /// Listes couvertes par la facture d'une ligne du suivi : une régularisation
    /// regroupée porte le même PDF sur la ligne de chacune de ses listes.
    List<string> ListesDeLaFacture(FactureEtat ligne)
    {
        var res = new List<string>();
        foreach (var rec in FacturationSuivi.MemeFacture(_loc, ligne.key))
            if (ListesCharges.LireCle(rec.key, out _, out string id)) res.Add(ListesCharges.Effective(id));
        if (res.Count == 0 && ListesCharges.LireCle(ligne.key, out _, out string seul)) res.Add(ListesCharges.Effective(seul));
        return res.Distinct().ToList();
    }

    /// Charges de toutes les listes cochées (chaque liste avec sa propre règle).
    List<ChargeBatiment> ChargesFor(int year)
        => ListesSel().SelectMany(id => ChargesDeRegul(_bat, _loc, year, id)).Distinct().ToList();

    /// Charges de l'année `year` à porter sur la régularisation de ce locataire :
    /// celles encore à lui facturer, PLUS celles que sa régularisation de cette année
    /// couvre déjà — une régularisation refaite ou corrigée doit les reprendre, sinon
    /// elle sortirait à 0 €. Une charge qu'il s'est vu refacturer à part a sa propre
    /// facture et n'y revient jamais ; les autres locataires ne sont pas concernés.
    public static List<ChargeBatiment> ChargesDeRegul(Batiment bat, Locataire loc, int year, string listeId = "")
    {
        var res = new List<ChargeBatiment>();
        if (bat?.charges == null || loc == null) return res;
        listeId = ListesCharges.Effective(listeId);
        // Revue 17 (2c, relecture du 07/11) : aussi une régularisation préparée puis bloquée (bouton « Auto » éteint), ou remise.
        bool regulEmise = FacturationSuivi.AFacture(loc, ListesCharges.Cle(listeId, year), out _);
        foreach (var c in bat.charges)
        {
            // Revue 17 (D6-09) : une charge « Réglée pour tous » que sa régularisation émise couvre déjà y reste à la correction,
            // comme en refacturation (avant : elle en sortait) ; encore à facturer, AFacturerPour l'écarte.
            if (c == null) continue;
            if (!ListesCharges.DeLaListe(c, listeId)) continue;   // une autre liste a sa propre régularisation
            if (!ListesCharges.Concerne(c, loc) || TryYear(c.dateISO) != year) continue;

            bool couverte = regulEmise && c.EstFactureePour(loc.id)
                            && loc.facturesEtat?.Any(x => x.key == "refac-" + c.id) != true;
            if (c.AFacturerPour(loc.id) || couverte) res.Add(c);
        }
        return res;
    }

    /// Charges de l'année refacturées à part à ce locataire « déduites des provisions »
    /// (case de la refacturation, facture émise), avec le HT facturé. La régularisation
    /// les affiche et les déduit aussitôt comme déjà réglées : le solde n'en dépend pas
    /// (demande du 01/10). Sans la case, la refacturation reste hors des provisions.
    public static List<(ChargeBatiment charge, float ht)> ChargesRefacturees(Batiment bat, Locataire loc, int year, string listeId = "")
    {
        var res = new List<(ChargeBatiment, float)>();
        if (bat?.charges == null || loc == null) return res;
        listeId = ListesCharges.Effective(listeId);
        foreach (var c in bat.charges)
        {
            if (c == null || c.FacturationDe(loc.id)?.deduitProvisions != true) continue;
            if (!ListesCharges.DeLaListe(c, listeId)) continue;
            if (!ListesCharges.Concerne(c, loc) || TryYear(c.dateISO) != year) continue;
            if (!FacturationSuivi.EstDejaEmise(loc, "refac-" + c.id, out var rec)) continue;
            // Le suivi garde le TTC ; le HT se relit au taux de l'époque (02/11).
            res.Add((c, rec.montant != 0f ? Tva.Ht(rec.montant, rec.tauxTva) : ListesCharges.QuotePart(c, loc, bat)));
        }
        return res;
    }

    /// Charges refacturées « déduites des provisions » des listes cochées.
    List<(ChargeBatiment charge, float ht)> RefacFor(int year)
        => ListesSel().SelectMany(id => ChargesRefacturees(_bat, _loc, year, id)).Distinct().ToList();

    int AnneeCiblee() => AnneeDeCle(_ligneCiblee?.key);

    /// Année portée par une clé de suivi de régularisation (« regul-2025 » ou
    /// « regul-2025-&lt;liste&gt; » → 2025), ou 0 si la clé est d'un autre type ou illisible.
    public static int AnneeDeCle(string key)
        => ListesCharges.LireCle(key, out int y, out _) ? y : 0;

    List<int> YearsAvailable()
    {
        var set = new SortedSet<int>();
        if (_bat?.charges != null)
            foreach (var c in _bat.charges)
            {
                if (!c.AFacturerPour(_loc.id)) continue;   // déjà facturée à ce locataire (ou réglée)
                if (!ListesSel().Any(id => ListesCharges.DeLaListe(c, id))) continue;
                if (!ListesCharges.Concerne(c, _loc)) continue;
                int y = TryYear(c.dateISO); if (y > 0) set.Add(y);
            }
        return set.Reverse().ToList();   // plus récent d'abord
    }

    // Quote-part du locataire sur une charge = coût × part_locataire / somme(parts),
    // au prorata de ses jours de présence dans l'année (arrivée, départ — 01/10).
    // Si un seul locataire concerné (ratios vides) → 100 % du coût.
    float QuotePart(ChargeBatiment c) => ListesCharges.QuotePartAuProrata(c, _loc, _bat);

    // Reconstruit la liste des charges + recalcule les totaux.
    void RefreshCharges()
    {
        foreach (Transform t in _chargesBox) Destroy(t.gameObject);
        var charges = ChargesFor(SelectedYear());
        if (charges.Count == 0)
            UIFactory.Text(_chargesBox, "Aucune charge impayée pour cette année.", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        foreach (var c in charges) LigneCharge(c, QuotePart(c), "", UITheme.TextePrincipal);
        foreach (var (c, ht) in RefacFor(SelectedYear()))
            LigneCharge(c, ht, "  ·  déjà refacturée", UITheme.TexteSecondaire);
        RefreshTotaux();
    }

    void LigneCharge(ChargeBatiment c, float montant, string mention, Color couleur)
    {
        var row = UIFactory.HBox(_chargesBox, 8, false, "ChRow");
        UIFactory.LE(row.gameObject, minH: 24);
        string dstr = DateTime.TryParse(c.dateISO, out var cd) ? cd.ToString("dd/MM/yyyy") : "";
        var l = UIFactory.Text(row.transform, $"{c.nom}  ·  {dstr}{mention}", UITheme.Role.Donnee, couleur);
        UIFactory.LE(l.gameObject, flexW: 1);
        UIFactory.Text(row.transform, $"{N2(montant)} €", UITheme.Role.Donnee, couleur, true, TextAlignmentOptions.Right);
    }

    void RefreshTotaux()
    {
        float totalCharges = ChargesFor(SelectedYear()).Sum(QuotePart);
        float provisions = ParseF(_provisions.text);
        // Charges refacturées : comptées dans le total ET déduites — le solde ne bouge pas.
        float deja = RefacFor(SelectedYear()).Sum(x => x.ht);
        _tDeja.text = $"{N2(deja)} €";
        _tDeja.transform.parent.gameObject.SetActive(deja > 0.005f);

        // TVA et TTC dérivés du HT DÉJÀ ARRONDI, et TTC = HT + TVA.
        // Avant, `tva = solde*.2f` et `ttc = solde*1.2f` étaient calculés
        // indépendamment : après arrondi à l'affichage, HT + TVA pouvait ne pas
        // redonner le TTC imprimé (écart d'un centime sur la facture).
        float solde = Cents(totalCharges - provisions);
        float taux = TauxFacture();   // 02/11 : le taux du bail (0 si exonéré)
        float tva = Tva.Montant(solde, taux);
        float ttc = solde + tva;

        _tCharges.text    = $"{N2(totalCharges + deja)} €";
        _tProvisions.text = $"{N2(provisions)} €";
        _tSolde.text      = $"{N2(solde)} €";
        _tTVA.text        = $"{N2(tva)} €";
        _tTTC.text        = $"{N2(ttc)} €";
        // BK (02/11) : sans TVA, le solde est le total et la ligne TVA disparaît ; sinon elle porte le taux de la facture.
        UIFactory.LigneMontant(_tSolde, true, taux > 0f ? "Solde HT" : "Solde");
        UIFactory.LigneMontant(_tTVA, taux > 0f, Tva.Libelle(taux));
        UIFactory.LigneMontant(_tTTC, taux > 0f, "Total TTC");

        // Provisions supérieures aux charges : il s'agit d'un trop-perçu, donc
        // d'un AVOIR — pas d'une facture. On le signale au lieu de laisser passer
        // une facture à montant négatif.
        if (solde < 0f)
            _tSolde.text = $"{N2(solde)} €  — trop-perçu (avoir)";   // pas de « ⚠ » : absent de la police

        RefreshSommeDefault();   // le solde vient de changer : la phrase doit suivre son signe
    }

    /// Arrondi au centime — évite les dérives de `float` sur les montants.
    static float Cents(float v) => Tva.Cents(v);   // Revue 17 (D5-03) : décimal, « au plus loin », comme le XML

    // Provisions appelées : une par période facturée de l'année (départ, arrivée,
    // franchise sans provisions → moins de périodes).
    // Revue 17 (avenant de périodicité, lot 16 ; D6-N5) : celles RÉELLEMENT appelées, période par période (Loyers.ProvisionsAppelees :
    // le montant imprimé sur chaque loyer quand il est connu, sinon celui de la fiche ramené à la périodicité de la période).
    // Avant : provision de la fiche × périodes facturées, faux l'année d'un changement de périodicité (300 × 8 = 2 400 € pour
    // 1 200 € appelés) ou d'un changement de provision.
    float ProvisionsAuto()
        => ListesSel().Sum(id => Loyers.ProvisionsAppelees(_loc, SelectedYear(), id));

    // ── Numéro / aperçu entête ─────────────────────────────────────────────────

    /// Phrase proposée par défaut. Elle suit le SIGNE du solde : sinon le champ
    /// afficherait « SOMME À NOUS RÉGLER » pendant que le PDF imprimerait
    /// « SOMME QUI VOUS SERA REMBOURSÉE » — l'écran mentirait sur ce qui est émis.
    /// `FactureEmission.PhraseSomme` reste le filet de sécurité à la génération.
    string DefaultSomme()
    {
        if (ChargesFor(SelectedYear()).Sum(QuotePart) - ParseF(_provisions.text) < -0.005f) return "SOMME QUI VOUS SERA REMBOURSÉE";

        DateTime ech = TryDate(_echeance != null ? _echeance.text : "", out var ed) ? ed : DateTime.Today;
        return "SOMME À NOUS RÉGLER LE " + ech.ToString("d MMMM yyyy", FacturePdfService.FrCulture);
    }

    void RefreshSommeDefault()
    {
        string def = DefaultSomme();
        if (_sommePhrase != null && _sommePhrase.text == _autoSomme) _sommePhrase.text = def;
        _autoSomme = def;
    }

    // Revue 17 (2c, décision du 06/11) : la facture des listes cochées a déjà son numéro : la ligne montre, grisé, celui que
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
        // suit l'année et les listes cochées. Avant : rien ne disait qu'une « corrigée(X) » allait partir.
        if (_titre != null) _titre.text = $"Régularisation des charges{(FactureEmission.SeraUneCorrection(_loc, CleFacture()) ? " (correction)" : "")} · {_loc.Name ?? ""}";
        FactureEmission.NommerBouton(_btnSauver, FactureEmission.SeraUneCorrection(_loc, CleFacture()));   // décision du 11/11
        RefreshEntetePreview();
    }

    /// Revue 17 (2c) : la ligne qui portera la facture, comme à l'émission (Regroupement) ; null si les listes cochées sont refusées.
    string CleFacture() => Regroupement(_loc, ListesSel(), SelectedYear(), out string cle) == null ? cle : null;

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
        int year = SelectedYear();
        float totalCharges = ChargesFor(year).Sum(QuotePart);
        float provisions = ParseF(_provisions.text);
        float solde = totalCharges - provisions;
        DateTime d = TryDate(_date.text, out var dd) ? dd : DateTime.Today;
        return new FactureContext
        {
            date = d,
            periode = $"charges {year}",
            numero = NumeroDocument(),
            loyerHT = solde,
            tva = Tva.Montant(solde, TauxFacture()),
            ttc = Tva.Ttc(solde, TauxFacture()),
            // Revue 17 (D6-08) : {echeance} remplie, comme à l'envoi au lancement (avant : vide dans l'e-mail et le PDF).
            echeance = TryDate(_echeance != null ? _echeance.text : "", out var e) ? e.ToString("dd/MM/yyyy") : "",
        };
    }

    // ── Données PDF ────────────────────────────────────────────────────────────

    /// Revue 17 (D6-22, coutures) : le titre de la page 2 (détail des charges). Lot 0 = « non renseigné » : l'adresse seule
    /// (avant : « LOT N°0 » imprimé sur le document envoyé).
    public static string TitreDetail(string adresse, int lot) => lot > 0 ? $"{adresse}, LOT N°{lot}" : adresse;

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
        float chargesRegul = lignes.Sum(l => l.quotePart);

        // Charges déjà refacturées : listées et déduites, le solde ne change pas.
        var refac = RefacFor(year);
        lignes.AddRange(refac.Select(x => new FacturePdfService.RegulLigne
        {
            nom = x.charge.nom + " (déjà refacturée)",
            dateStr = DateTime.TryParse(x.charge.dateISO, out var cd) ? cd.ToString("dd/MM/yyyy") : "",
            coutTotal = x.charge.cout,
            quotePart = x.ht,
            pj = string.IsNullOrEmpty(x.charge.pdfPath) ? "" : Path.GetFileName(x.charge.pdfPath),
        }));
        float deja = refac.Sum(x => x.ht);

        float totalCharges = chargesRegul + deja;
        float totalARepartir = lignes.Sum(l => l.coutTotal);
        float provisions = ParseF(_provisions.text);
        float solde = chargesRegul - provisions;
        string adr = (_bat != null ? _bat.adressBatiment : "") ?? "";
        string detailTitre = TitreDetail(adr, _loc.lotBatiment);

        return new FacturePdfService.RegulData
        {
            clientNom = _nom.text,
            clientAdresseHtml = FacturePdfService.AdresseHtml(_adresse.text),
            clientSiret = _siret.text,
            refInterne = _refInterne.text,
            dateStr = dateStr,
            numero = NumeroDocument(),
            subtitle = ListesCharges.Libelle(ListesSel(), year),
            bodyHtml = FacturePdfService.BodyHtml(entResolved),
            charges = lignes,
            totalCharges = totalCharges,
            provisions = provisions,
            dejaRefacture = deja,
            soldeHT = solde,
            tva = Tva.Montant(solde, TauxFacture()),
            ttc = Tva.Ttc(solde, TauxFacture()),
            detailTitre = detailTitre,
            locataireNom = _nom.text,
            surfaceImmeuble = _bat != null ? _bat.tailleBatiment : 0f,
            totalARepartir = totalARepartir,
            masquerTva = TauxFacture() <= 0f, tvaLibelle = Tva.Libelle(TauxFacture()),   // 02/11 : exonéré = solde seul
            tvaDebit = TauxFacture() <= 0f || MentionTva.Imprimee(_mentionTva),
            retard = _retard.isOn,
            // « SOMME À NOUS RÉGLER » devient « SOMME QUI VOUS SERA REMBOURSÉE »
            // quand le solde est négatif. Libellé du total inversé de même.
            // Résolue APRÈS PhraseSomme : celle-ci peut substituer sa propre phrase
            // selon le signe du solde, et cette phrase-là doit être résolue aussi.
            sommePhrase = FactureVarResolver.Resolve(
                FactureEmission.PhraseSomme(_sommePhrase.text, solde), _loc, _bat, ctx),
            texteTvaDebit = TauxFacture() <= 0f ? Tva.Mention(R) : FactureVarResolver.Resolve(MentionTva.Phrase(_mentionTva, _texteTvaDebit), _loc, _bat, ctx),
            labelSolde = FactureEmission.LibelleSolde(solde, TauxFacture() <= 0f ? "Solde" : "Solde H.T."),
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
        // SaveFromUI l'enregistrait vide : facture datée du jour, échéance du jour dans le suivi.
        if (SaisieDate.RefusFacture(_date.text, _echeance.text) is string refusDate) { ConfirmDialog.Erreur(refusDate); return; }
        SaveFromUI();
        var d = BuildData();
        string png = DossiersDonnees.Apercu("apercu_regul.png");
        // Hauteur 2 pages : la facture (p.1) + le détail des charges (p.2).
        if (FacturePdfService.GenerateRegulPreviewPng(d, png, out string err, 794, 2246)) ShowPreview(png);
        else ConfirmDialog.Erreur("Échec de l'aperçu : " + err);
    }

    void SauvegarderEtEnvoyer()
    {
        if (SaisieDate.RefusFacture(_date.text, _echeance.text) is string refusDate) { ConfirmDialog.Erreur(refusDate); return; }   // Revue 17 (D6-05)
        // Refus AVANT tout (PDF, confirmation) si les listes cochées ne peuvent pas
        // former une seule facture.
        // Une facture regroupée se range sous UNE ligne, `key` (celle de la facture existante s'il y en a une) ; les autres
        // reçoivent le même numéro et le même PDF.
        string refus = Regroupement(_loc, ListesSel(), SelectedYear(), out string key);
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }

        SaveFromUI();
        // Revue 16c (D6-03) : le numéro décidé AVANT de construire le document (entête, phrase, e-mail : {numero}).
        // Déjà émise pour cette année → version « corrigée(X) » : même numéro, aucune nouvelle séquence consommée, PDF d'origine conservé.
        var emission = FactureEmission.Preparer(_loc, key, ComposedNumero());
        if (emission.Refus != null) { ConfirmDialog.Erreur(emission.Refus); return; }   // D4-04 : numéro déjà pris
        _numeroEmis = emission.NumeroFacture;
        var d = BuildData();
        // Revue 17 (D6-19, coutures ; décision du 09/11) : un total nul est refusé, comme pour le loyer : rien à facturer (avant : émis, il
        // prenait un numéro). Revue 17 (D6-15) : sans RIB, le relevé d'identité bancaire serait vide. Les deux avant le PDF.
        if (FactureEmission.RefusTotalNul(d.ttc, d.subtitle) is string refusNul) { ConfirmDialog.Erreur(refusNul); return; }
        if (FactureEmission.RefusSansRib(ReglageService.GetRib(_ribDD?.SelectedId)) is string refusRib) { ConfirmDialog.Erreur(refusRib); return; }

        // Un solde négatif (provisions > charges) est un AVOIR, pas une facture :
        // l'émettre produirait une facture à HT/TVA/TTC négatifs, non conforme.
        // Solde négatif = les provisions dépassent les charges réelles, donc c'est
        // NOUS qui devons. Le document est émis quand même (comptablement un avoir :
        // il consomme un numéro comme une facture), mais il est confirmé — inverser
        // le sens d'une somme ne doit pas tenir à un clic.
        if (d.ttc < -0.005f && ConfirmDialog.Instance != null)
        {
            ConfirmDialog.Instance.Show(
                "Remboursement au locataire",
                "Les provisions versées dépassent les charges réelles : ce document constate "
                + (-d.ttc).ToString("N2", FacturePdfService.FrCulture)
                + " € dus AU locataire, et non réclamés. "
                + "Il consommera un numéro comme une facture.",
                () => Emettre(d, key, emission), "Émettre");
            return;
        }

        Emettre(d, key, emission);
    }

    /// Génère le PDF et met le suivi à jour. Séparé de `SauvegarderEtEnvoyer` pour
    /// pouvoir être repris après une confirmation (voir le cas du remboursement).
    /// Revue 17 (D4-09, coutures) : la ligne (`key`) et la décision de SauvegarderEtEnvoyer suivent jusqu'ici, comme pour le loyer et
    /// la refacturation. Avant : un second Regroupement et un second Preparer, dont le refus n'était pas lu.
    void Emettre(FacturePdfService.RegulData d, string key, FactureEmission.Decision emission)
    {
        // L'envoi est asynchrone : sans cette garde, un double clic partirait deux fois.
        if (_envoiEnCours) { UndoToast.Instance?.ShowInfo("Un envoi est déjà en cours."); return; }

        int year = SelectedYear();
        string dir = FactureDir();
        var listes = ListesSel();
        bool correction = emission.Correction;
        // Revue 17 (D5-07, lot 11) : la plateforme se décide UNE fois, ici, et suit jusqu'à Finaliser (avant : Concerne relu après l'e-mail).
        bool parPlateforme = FactureElectronique.Concerne(_loc, d.tva, _siret.text);
        // Revue 17 (D5-02) : la correction d'une facture transmise ne part pas en avoir (la plateforme garderait les deux). Lot 11 (D5-07,
        // D5-05, décision du 05/11) : hors plateforme, la correction d'une facture transmise est refusée ici, au centre, avec la raison
        // (avant : elle partait par e-mail sans un mot) ; dès la bascule, une facture qui doit partir par la plateforme aussi.
        string refus = FactureElectronique.RefusCorrection(_loc.facturesEtat?.Find(x => x.key == key), d.ttc, d.subtitle, parPlateforme,
                    FactureElectronique.RaisonHorsPlateforme(_loc, d.tva, _siret.text))
             ?? FactureElectronique.RefusObligation(_loc, d.tva, _siret.text, d.subtitle, parPlateforme, DateTime.Today);
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }
        // BI8 (02/11) : un avoir cite les factures qu'il corrige (BR-FR-CO-05) : la version précédente, sinon les loyers de l'année.
        var citees = parPlateforme && d.ttc < -0.004f ? FactureElectronique.ReferencesAvoir(_loc, _loc.facturesEtat?.Find(x => x.key == key), year) : null;
        if (citees != null && citees.Count == 0)
        {
            // Revue 17 (D5-14, coutures) : la raison suit la règle : un loyer ENVOYÉ au locataire (avant : « émis », faux pour un loyer en attente d'envoi).
            ConfirmDialog.Erreur(FactureElectronique.RefusAvoir(d.subtitle,
                $"Aucun loyer {year} envoyé au locataire à citer (un loyer en attente d'envoi, ou refusé par Super PDP, ne compte pas) : "
                + "envoyez d'abord un loyer de l'année, ou rendez ce trop-perçu sur le prochain loyer."));
            return;
        }
        // Revue 17 (D5-15, coutures) : par la plateforme, le PDF de l'avoir nomme les factures que son XML cite (mêmes références que
        // Finaliser). Hors plateforme, rien n'est imprimé, comme depuis le 05/10 (la régularisation solde les provisions de l'année).
        if (citees != null) d.factureOrigine = FactureElectronique.NumerosCites(citees);

        string liste = listes.Count == 1 && listes[0] == "" ? "" : "-" + string.Join("+", listes.Select(ListesCharges.Nom));
        string fname = Sanitize($"RegularisationdeCharge-{_nom.text}-{year}{liste}{emission.SuffixeFichier}") + ".pdf";
        string pdf = DossiersDonnees.CheminFacture(dir, fname);

        if (!FacturePdfService.GenerateRegulPdf(d, pdf, out string err))
        {
            ConfirmDialog.Erreur("Échec génération PDF : " + err);
            return;
        }

        string png = DossiersDonnees.Apercu("apercu_regul.png");
        if (FacturePdfService.GenerateRegulPreviewPng(d, png, out _, 794, 2246)) ShowPreview(png);

        // Revue 17 (D5-10 / D5-26, lot 13) : dès la bascule (1/9/2027), comme le loyer : plus de PDF par e-mail, la facture part par Super PDP
        // après confirmation, puis la notification (FactureElectronique.Notifier). Avant : l'e-mail de facture avec le PDF, puis la notification.
        // Pour un avoir, cette confirmation suit celle du « Remboursement au locataire ».
        if (parPlateforme && FactureElectronique.NotificationSeule)
        {
            FactureElectronique.ConfirmerTransmission(_emailEnvoi.text,
                () => Finaliser(d, year, key, emission, correction, pdf, true, parPlateforme, "  Transmise par la plateforme agréée."));
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
        var (eo, ec) = EmailService.TexteEnvoi(_loc.factureRegul, parPlateforme, !_modeNotif && _modeCopie == parPlateforme, _emailObjet.text, _emailCorps.text);
        string objet = FactureVarResolver.Resolve(eo, _loc, _bat, ctx);
        string corps = FactureVarResolver.Resolve(ec, _loc, _bat, ctx);

        // Sans confirmation disponible, rien ne part.
        if (ConfirmDialog.Instance == null)
        {
            ConfirmDialog.Erreur("Confirmation indisponible : rien n'a été envoyé. "
                + "Le PDF est enregistré.");
            return;
        }

        ConfirmDialog.Instance.Show(
            "Envoyer la régularisation par email ?",
            $"À : {dest}\nObjet : {objet}\nPièce jointe : {Path.GetFileName(pdf)}\n\n"
            + "Le message part immédiatement et ne pourra pas être rappelé.",
            () => StartCoroutine(EnvoyerPuisFinaliser(d, year, key, emission, correction, pdf, parPlateforme, dest, objet, corps)),
            "Envoyer");
    }

    /// Envoie, attend, et n'enregistre QUE si le message est parti. Un échec laisse
    /// le numéro disponible et les charges NON payées : tant que la facture n'est pas
    /// partie, rien de ce qu'elle emporte ne doit être considéré comme acquis.
    System.Collections.IEnumerator EnvoyerPuisFinaliser(
        FacturePdfService.RegulData d, int year, string key, FactureEmission.Decision emission,
        bool correction, string pdf, bool parPlateforme, string dest, string objet, string corps)
    {
        FactureEmission.NoterEnvoi(ref _envoiEnCours, true);   // Revue 17 (D4-07) : compté : rechargement et fermeture de CIPL attendent la fin
        UndoToast.Instance?.ShowInfo($"Envoi en cours vers {dest}…");

        var envoi = EmailService.Envoyer(dest, objet, corps, new[] { pdf });
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
        Finaliser(d, year, key, emission, correction, pdf, true, parPlateforme, $" et envoyée à {dest}", dest);
        if (_fermerApresEnvoi) Close();   // Revue 17 (D4-07, coutures) : la fermeture demandée pendant l'envoi, une fois la facture enregistrée
    }

    /// Enregistrement du suivi, commun aux deux chemins (sans envoi, ou après succès).
    /// `dest` (Revue 17, D4-01, lot 6) : l'adresse à laquelle l'e-mail est réellement parti ; sinon celle du formulaire.
    /// `parPlateforme` (Revue 17, D5-07, lot 11) : décidé par Emettre, le même pour le refus, l'e-mail et la facture électronique.
    void Finaliser(FacturePdfService.RegulData d, int year, string key,
                   FactureEmission.Decision emission, bool correction, string pdf, bool envoye, bool parPlateforme,
                   string suffixeMessage = "", string dest = null)
    {
        // Les charges régularisées passent « en attente de paiement », PAS « payé » :
        // la facture vient de partir, le virement n'est pas arrivé. Elles sortent du
        // choix (pour ne pas être régularisées deux fois) sans prétendre être encaissées.
        // La liste de la ligne `key` en tête : c'est elle qui porte la facture.
        var listes = ListesSel();
        if (ListesCharges.LireCle(key, out _, out string principale))
        {
            principale = ListesCharges.Effective(principale);
            listes.Remove(principale);
            listes.Insert(0, principale);
        }
        var montants = MontantsParListe(listes, year, d.ttc);   // avant de marquer les charges
        string aujourdhui = DateTime.Today.ToString("yyyy-MM-dd");
        foreach (var c in ChargesFor(year)) c.MarquerFacturee(_loc.id, aujourdhui);

        var avant = _loc.facturesEtat?.Find(x => x.key == key);   // lue avant l'enregistrement : une correction transmise la cite
        // Revue 17 (D5-14 / D5-15, coutures) : ce qu'un avoir cite se lit AVANT l'enregistrement aussi (BI8 : la version précédente, sinon
        // les loyers de l'année) : après, une version jamais partie vient de passer « Envoyé » et l'avoir se citait lui-même, pas son PDF.
        var citees = FactureElectronique.ReferencesAvoir(_loc, avant, year);
        FacturationSuivi.Solde(_loc, key, out float totalAvantCorrection, out _);   // Revue 17 (D4-03) : avant la correction
        string message = FactureEmission.Enregistrer(_loc, key, "Regul", emission,
            d.subtitle, _loc.factureRegul?.dateEcheanceISO, pdf, montants[listes[0]], _ribDD?.SelectedId,
            _loc.factureRegul, "Régularisation", envoye,
            "Régularisation enregistrée (PDF) · charges en attente de paiement");

        // Les autres listes de la MÊME facture : même numéro, même PDF, chacune sa part
        // du montant (la somme des lignes redonne le total de la facture). Le numéro
        // n'est consommé qu'une fois, par la première liste ci-dessus.
        // Revue 17 (D4-06, coutures) : une seule règle, partagée avec la refacturation : ces lignes reçoivent aussi l'échéance et le RIB
        // d'une correction, et une liste ajoutée à une correction le numéro de base (FactureEmission.EnregistrerLiee).
        for (int i = 1; i < listes.Count; i++)
            FactureEmission.EnregistrerLiee(_loc, ListesCharges.Cle(listes[i], year), "Regul", emission, ListesCharges.Libelle(listes[i], year),
                _loc.factureRegul?.dateEcheanceISO, pdf, montants[listes[i]], _ribDD?.SelectedId, envoye);
        FacturationSuivi.ApresCorrection(_loc, key, totalAvantCorrection);   // Revue 17 (D4-03) : payée, l'écart redevient dû

        foreach (var l in FacturationSuivi.MemeFacture(_loc, key)) l.tauxTva = d.masquerTva ? 0f : TauxFacture();   // 02/11 : taux de l'époque
        // Revue 17 (D4-01, lot 6) : pour qui la facture est préparée (la fiche, ou le cédant d'une facture corrigée) et l'adresse réellement
        // envoyée, sinon celle du formulaire ; lus par le rappel et la notification.
        FacturationSuivi.PoserAcheteur(_loc, key, _acheteurAutre ? _ligneCiblee?.acheteur : null, dest ?? _emailEnvoi.text);
        // Décision du 02/11 (BI5) : la facture électronique sur la ligne principale, comme le loyer ; un total négatif part en
        // avoir (FactureEn16931). Hors plateforme (sans SIRET, SCI non reliée…) : rien, la copie PDF part par e-mail comme avant
        // (une facture transmise, elle, a été refusée avant l'envoi : lot 11).
        FactureElectronique.Preparer(_loc.facturesEtat?.Find(x => x.key == key), avant,
            parPlateforme, new FactureEn16931.Entree
            {
                numero = d.numero, dateISO = BuildContext().date.ToString("yyyy-MM-dd"), echeanceISO = _loc.factureRegul?.dateEcheanceISO,
                acheteurNom = d.clientNom, acheteurSiren = SocieteLocataire.Siren(FactureElectronique.SiretAcheteur(_loc, _siret.text)),
                acheteurSiret = FactureElectronique.Siret14(FactureElectronique.SiretAcheteur(_loc, _siret.text)), acheteurAdresse = _adresse.text,
                lignes = FactureEn16931.LignesRegul(d), mention = d.tvaDebit ? d.texteTvaDebit : null, penalites = d.legal, escompte = d.escompte,
                tauxTva = TauxFacture(),
                references = citees,
            });
        _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();   // persiste locataire + charges
        LocataireSuiviInline.RefreshFor(_fiche);   // Suivi à jour tout de suite
        FactureElectronique.Rattraper(_fiche.batimentPrefabOrigin, _loc);

        // Revue 17 (2c, décision du 06/11) : la ligne « N° de facture » reprend le numéro de la facture, correction comprise (la
        // suivante sera « corrigée(2) »), et l'aperçu avec elle (avant : il gardait le numéro émis).
        if (!correction)
        {
            if (_numeroId != null) _numeroId.text = "";
            RefreshCharges();   // les charges régularisées disparaissent (désormais payées)
        }
        RefreshNumero();
        ChargerTva(_loc.factureRegul);   // Revue 17 (D5-08, lot 10) : le taux proposé repart de la facture telle qu'émise (voir le loyer)

        UndoToast.Instance?.ShowInfo(message + suffixeMessage);
    }

    /// Part de chaque liste dans le TTC de la facture : ses charges moins sa provision.
    /// La première liste porte le reste, pour que la somme redonne exactement le total.
    /// Revue 17 (lot 16 ; D6-16, D6-N5) : chaque liste déduit SA part de la provision SAISIE dans le formulaire (PartProvision), au
    /// prorata des provisions appelées pour elle dans l'année (Loyers.ProvisionsAppelees). Avant : sa provision de fiche × périodes
    /// facturées, quelle que soit la saisie : la première liste absorbait tout l'écart (lignes fausses dans le suivi et Créances,
    /// au total juste). La forme statique reçoit donc la part déjà calculée, pour une seule « période ».
    Dictionary<string, float> MontantsParListe(List<string> listes, int year, float ttcTotal)
    {
        float saisie = ParseF(_provisions.text);
        var appelees = listes.Distinct().ToDictionary(id => id, id => Loyers.ProvisionsAppelees(_loc, year, id));
        float total = appelees.Values.Sum();
        return MontantsParListe(listes, id => ChargesDeRegul(_bat, _loc, year, id).Sum(QuotePart), id => PartProvision(saisie, appelees[id], total),
                                1, ttcTotal, TauxFacture());
    }

    /// Revue 17 (D6-16) : la part d'une liste dans la provision saisie : au prorata de ce qui a été appelé pour elle (`appelee`)
    /// sur ce qui l'a été pour toutes les listes de la facture (`total`), au centime. Rien d'appelé : 0 (la première liste porte tout).
    public static float PartProvision(float saisie, float appelee, float total)
    {
        decimal t = Tva.Centimes(total);
        return t == 0m ? 0f : (float)Math.Round(Tva.Centimes(saisie) * Tva.Centimes(appelee) / t, 2, MidpointRounding.AwayFromZero);
    }

    /// La part de chaque liste dans une régularisation regroupée (la première absorbe l'écart : la somme des lignes redonne le
    /// TTC). Revue 16c (D4-08, D6-11) : les provisions déduites comptent les périodes FACTURÉES de l'année (arrivée, départ),
    /// comme le total du formulaire, et plus celles d'une année pleine.
    /// Revue 17 (D5-13 ← D5-N8, lot 16) : la part TTC de chaque liste passe par Tva.Ttc, le chemin commun (décimal, « au plus
    /// loin »). Avant, en float : 1 234,45 € à 10 % donnait 123,44 € de TVA ici et 123,45 € partout ailleurs.
    public static Dictionary<string, float> MontantsParListe(List<string> listes, Func<string, float> chargesDe,
                                                             Func<string, float> provisionDe, int periodesFacturees, float ttcTotal, float taux = 20f)
    {
        var res = new Dictionary<string, float>();
        float autres = 0f;
        for (int i = 1; i < listes.Count; i++)
        {
            float solde = Cents(chargesDe(listes[i]) - provisionDe(listes[i]) * periodesFacturees);
            float ttc = Tva.Ttc(solde, taux);
            res[listes[i]] = ttc;
            autres += ttc;
        }
        res[listes[0]] = ttcTotal - autres;
        return res;
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

    // ── Sauvegarde (mémorisé sur le locataire) ─────────────────────────────────

    void SaveFromUI()
    {
        if (_loc == null) return;
        var f = _loc.factureRegul ?? new FactureInfo();
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
        f.provisionMontant = ParseF(_provisions.text);
        f.anneePeriode = SelectedYear();
        f.listesRegul = ListesSel();
        f.objet =ListesCharges.Libelle(ListesSel(), SelectedYear());
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
    /// plus de 0 forcé par le bail du jour (la correction d'une facture taxée d'un bail devenu exonéré partait sans TVA).
    float TauxFacture()
        => _tauxTva != null && !string.IsNullOrWhiteSpace(_tauxTva.text) && SaisieNumerique.TryParse(_tauxTva.text, out float t) && t >= 0f
           ? t : Tva.TauxPropose(_loc, null, Corrigee());

    /// Le taux tapé n'est mémorisé que s'il diffère de celui du bail ou de l'entreprise (sinon il suivrait un changement de réglage).
    float TauxAMemoriser() => Tva.Soumis(_loc) && SaisieNumerique.TryParse(_tauxTva.text, out float t) && t > 0f
                              && Mathf.Abs(t - Tva.Taux(_loc)) > 0.001f ? t : 0f;

    /// Revue 17 (D5-08, lot 10) : la facture PARTIE que le formulaire corrige (la ligne qui portera la facture, comme le numéro repris :
    /// CleFacture, donc suivie à chaque changement d'année ou de liste), sinon null : son taux imprimé est proposé, et le taux tapé ne
    /// vaut que pour elle (SaveFromUI ne le mémorise pas sur la facture du type).
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

    /// Revue 17 (D5-08, lot 10) : la facture visée vient de changer (année, listes) : le taux proposé la suit, tant qu'il n'a pas été
    /// saisi à la main. Sinon, une facture NEUVE faite après une correction sans fermer le formulaire gardait le taux de l'époque.
    void RefreshTauxDefault() { if (_loc != null && _tauxTva != null && _tauxTva.text == _autoTaux) ChargerTva(_loc.factureRegul); }

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
        var f = _loc.factureRegul;
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

    static int TryYear(string iso) => DateTime.TryParse(iso, out var d) ? d.Year : 0;

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static bool TryDate(string s, out DateTime d) => SaisieDate.TryParse(s, out d);
}
