using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Écran « Information Facture — Loyer » (construit par code), conforme au design :
/// Destinataire (Nom/Adresse/Siret) · RIB CIPL (menu déroulant) · Date · Loyer &
/// Provision (pré-remplis, éditables) · N° de facture · Texte de présentation
/// (menu déroulant) · cases TVA débit / Retard de paiement · email d'envoi.
/// Pré-rempli depuis le locataire + Réglages, mémorisé par locataire (loc.factureLoyer).
/// « Générer la facture » = mémorise + aperçu (le PDF au visuel réel + l'envoi par e-mail ou par la
/// plateforme viennent ensuite — RIEN n'est émis pour l'instant).
public class FactureLoyerPanel : MonoBehaviour
{
    // Revue 16c (D6-06) : les montants en écriture française (« 1 234,56 »), comme le PDF et Créances, quelle que
    // soit la langue de Windows (la culture courante donnait « 1,234.56 » dans les textes et les e-mails).
    static string N2(float v) => v.ToString("N2", FacturePdfService.FrCulture);

    public static FactureLoyerPanel Instance { get; private set; }

    /// Étape 13b-3 (retour du 25/10) : pendant le guide « Une première facture », le bouton émet sans
    /// envoyer — ni e-mail ni plateforme — et la facture attend dans le suivi. Un e-mail mal réglé ne
    /// bloque plus le tutoriel, et rien ne part pendant qu'on apprend.
    public static bool SansEnvoi { get; private set; }

    public static void ModeSansEnvoi(bool oui)
    {
        SansEnvoi = oui;
        if (Instance != null) Instance.RefreshBoutonSauver();
    }

    static readonly Color CoVert = UITheme.Primaire, CoVertL = UITheme.PrimaireClair;
    static readonly Color CoBleu = Hex("#2C3E5E"), CoBleuL = Hex("#DEE3EB");
    // Couleurs de cartes. `CoContenu` et `CoReglement` portent le même rôle — donc la
    // même teinte — dans les quatre panneaux ; seule la carte du type change de couleur.
    static readonly Color CoContenu   = Hex("#5F5E5A"), CoContenuL   = Hex("#E9E6DE");
    static readonly Color CoReglement = Hex("#854F0B"), CoReglementL = Hex("#F6E6C8");
    static readonly Color CoLoyer     = Hex("#5B3E7A"), CoLoyerL     = Hex("#E9E0F2");

    static readonly string[] MoisNoms =
    { "Janvier","Février","Mars","Avril","Mai","Juin","Juillet","Août","Septembre","Octobre","Novembre","Décembre" };

    // Source unique : FactureNumerotation (les quatre panneaux dupliquaient ces listes).
    static List<string> NumFmtLabels => FactureNumerotation.Labels;
    static List<string> NumFmtIds => FactureNumerotation.Ids;

    LocatairePrefab _fiche; Locataire _loc; Batiment _bat;
    FactureEtat _ligneCiblee;   // ligne du suivi cliquée : elle désigne la période à ouvrir

    TMP_Text _titre, _entetePreview, _modeInfo, _mTotalHT, _mTVA, _mTTC, _numeroPrefixe, _numeroAide;
    TMP_InputField _nom, _adresse, _siret, _date, _echeance, _numeroId, _annee, _refInterne, _sommePhrase, _loyer, _provision, _emailEnvoi;
    TMP_InputField _texteTvaDebit, _texteMensuel, _tauxTva;
    TMP_Text _provisionLabel;                              // provision générale
    TMP_Text _aideMensuel;                                 // l'aide sous la phrase « montant mensuel » (D6-24)
    Transform _provListesBox;                              // une provision par liste spécifique
    readonly List<(string id, TMP_InputField inp)> _provListes = new List<(string, TMP_InputField)>();
    TMP_InputField _emailObjet, _emailCorps;
    TMP_Text _aideEmail;
    // Étape 18b : les champs de l'e-mail sont ceux de la « copie » (loyer qui part aussi par la plateforme, avant
    // la bascule) ou de la notification (après) ; sinon ceux de l'e-mail de facture. Revue 17 (D5-10, lot 13) : ils suivent le
    // SIRET et le taux tapés (RefreshPlateforme) ; `_chargement` : LoadIntoUI écrit ces champs, l'écouteur attend la fin.
    bool _modeNotif, _modeCopie, _chargement;
    bool _envoiEnCours;   // empêche un second clic de produire un second envoi
    string _numeroEmis;   // le numéro décidé par FactureEmission.Preparer pendant l'émission (revue 16c, D6-03)
    string NumeroDocument() => _numeroEmis ?? ComposedNumero();
    string _idGarde;      // Revue 17 (2c, décision du 06/11) : l'ID tapé, mis de côté pendant qu'un numéro repris occupe la ligne
    UIDropdown _ribDD, _enteteDD, _numeroFormatDD, _periodeDD;
    int _anneeOptions;   // Revue 17 (avenant de périodicité, lot 15) : l'année dont le plan remplit le menu « Période facturée »
    string _autoSomme;   // dernière phrase de règlement auto (suivie tant que non personnalisée)
    string _autoEcheance;// dernière échéance proposée (suivie tant qu'elle n'est pas saisie à la main)
    string _autoLoyer;   // dernier loyer proposé pour la période (suivi tant qu'il n'est pas saisi à la main)
    string _autoTaux;    // Revue 17 (D5-08, lot 10) : dernier taux de TVA proposé (suivi tant qu'il n'est pas saisi à la main)
    // Revue 17 (avenant de périodicité, lot 16) : dernière provision proposée par liste ("" : la générale), suivie de même.
    readonly Dictionary<string, string> _autoProvisions = new Dictionary<string, string>();
    // Revue 17 (D7a-03) : l'acheteur d'office (fiche ou destinataire mémorisé), celui affiché d'office, et s'il vient d'une cession.
    string[] _acheteurFiche, _acheteurAuto;
    bool _acheteurCession;
    TMP_Text _noteCession;   // retour BN12 (04/11) : la ligne sous « Destinataire »
    Toggle _retard, _mensuel;
    UIDropdown _mentionTva;
    Button _btnSauver;   // libellé variable : « … et envoyer » ou « … pour l'envoi du JJ/MM »

    // Aperçu de la facture rendue (image à droite du formulaire).
    RawImage _previewImg;
    FacturePreviewViewer _viewer;
    TMP_Text _previewHint;
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

    public static void OpenLoyer(LocatairePrefab fiche) => OpenLoyer(fiche, null);

    // `ligneCiblee` : la ligne du suivi sur laquelle on a cliqué, quel que soit le
    // bouton (Générer / Refaire / Corriger). Elle sert à OUVRIR LA BONNE PÉRIODE —
    // sans elle, le panneau retombait sur la dernière période éditée, et on refaisait
    // avril en croyant refaire mars.
    //
    // Ce que devient la facture (nouvelle, remplacée ou corrigée) ne se décide PAS
    // ici : FactureEmission.Preparer tranche d'après le statut réel de la ligne.
    public static void OpenLoyer(LocatairePrefab fiche, FactureEtat ligneCiblee)
    {
        if (fiche == null) return;
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("FactureLoyerPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<FactureLoyerPanel>();
        }
        var p = Instance;   // sur le cloud : le locataire est réservé tant que le panneau est ouvert (étape 4b)
        EcritureCloud.Reserver(EcritureCloud.Cibles(fiche, false), "facturation", () => p.OpenFor(fiche, ligneCiblee),
            () => p != null && p.gameObject.activeSelf && p._fiche == fiche, () => { if (p != null) p.FermerDeForce(); });
    }

    void OpenFor(LocatairePrefab fiche, FactureEtat ligneCiblee = null)
    {
        _fiche = fiche;
        _loc = fiche.GetLocataire();
        _bat = fiche.batimentPrefabOrigin != null ? fiche.batimentPrefabOrigin.getBatiment() : null;
        _ligneCiblee = ligneCiblee;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ResetPreview();   // pas d'aperçu du locataire précédent
        LoadIntoUI();
        AppliquerLigneCiblee();
    }

    // Force la période/année du formulaire sur la ligne cliquée (clé « loyer-{Y}-P{P} »).
    // Revue 17 (D6-04, décision du 05/11) : le titre « (correction) » n'est plus posé ici (seulement depuis une ligne du suivi, et
    // sans le nom du locataire, D10-14) : RefreshBoutonSauver le pose d'après la période affichée, appelé par RefreshEcheanceDefault.
    void AppliquerLigneCiblee()
    {
        if (_ligneCiblee == null || string.IsNullOrEmpty(_ligneCiblee.key)) return;

        var parts = _ligneCiblee.key.Split('-');   // ["loyer","2026","P3"]
        if (parts.Length >= 3 && int.TryParse(parts[1], out int y))
        {
            string p = parts[2].TrimStart('P', 'p');
            // Revue 17 (avenant de périodicité, lot 15) : les options sont celles du plan de l'année de la CLÉ (avant : celles de la
            // périodicité actuelle, quelle que soit l'année). Une ligne hors du plan (« Loyer Mai 2026 », clé P5, émise en mensuel
            // sur une fiche passée au trimestriel sans date d'effet) n'a pas d'option : le menu retombait sur la première, et
            // « Corriger » ouvrait le 1er trimestre à sa place. Refusée au centre, le formulaire ne s'ouvre pas sur une autre période.
            if (!int.TryParse(p, out int rang) || rang < 1 || rang > FacturationSuivi.Plan(_loc, y).Count)
            {
                ConfirmDialog.Erreur($"« {_ligneCiblee.libelle} » a été émis avec une périodicité que la fiche n'a plus (changée sans date "
                    + "d'effet) : il ne se corrige pas depuis ce formulaire. Pour l'annuler, faites un avoir (Refacturation, « Avoir sur la facture n° … »).");
                Close();
                return;
            }
            PoserPeriodes(y, rang);   // avant l'année : son écouteur trouve le menu déjà rempli pour elle
            _annee.text = y.ToString();
            // La période vient de changer : l'échéance et la phrase de règlement en
            // dépendent, et le bouton annonce la date d'envoi qui en découle. Sans ce
            // rappel, le panneau gardait l'échéance de la période précédente.
            RefreshEcheanceDefault();
            RefreshTauxDefault();   // Revue 17 (D5-08, lot 10) : le taux imprimé sur la ligne cliquée (facture partie)
            RefreshNumero(); RefreshMontants(); RefreshEntetePreview();
        }
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
        _titre = UIFactory.Text(header.transform, "Information Facture — Loyer", UITheme.Role.Page, UITheme.TextePrincipal, true);
        UIFactory.LE(_titre.gameObject, flexW: 1);

        // Corps : deux colonnes — formulaire à gauche, aperçu de la facture à droite.
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
        UIFactory.Text(d.transform, "Adresse", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _adresse = UIFactory.Input(d.transform, "Adresse (plusieurs lignes possibles)", 88, true);
        _siret = Labeled(d, "SIRET");
        _siret.onValueChanged.AddListener(_ => RefreshPlateforme());   // Revue 17 (D5-10, lot 13) : le SIRET tapé décide aussitôt de la plateforme
        // Retour BN12 (04/11) : la période est facturée au titulaire d'une cession : dit ici, tant que c'est vrai (le message du
        // bas restait affiché en revenant à une période de la fiche).
        _noteCession = UIFactory.Text(d.transform, "", UITheme.Role.Aide, UITheme.AttentionTexte);
        _noteCession.gameObject.SetActive(false);

        // L'écran suit l'ordre du DOCUMENT IMPRIMÉ : en-tête, contenu, pied de
        // règlement. Dans chaque carte, le champ qui change à chaque facture précède
        // celui qui ne bouge qu'une fois par an (RIB, format du numéro). Même ordre
        // dans les quatre panneaux : seule la carte du type varie.

        // ── 1. En-tête du document ──
        var e = UIFactory.Section(content, "En-tête du document", CoBleu, CoBleuL);
        _date = Labeled(e, "Date");
        _date.onValueChanged.AddListener(_ => RefreshNumero());
        // Texte libre affiché en rouge avec le n° (ex. « N° Interne Magasin 001048 »).
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

        // ── Loyer facturé (contenu propre au type) ──
        // La période rejoint les montants : elle s'imprime dans le corps du document,
        // pas dans l'en-tête, et c'est elle qui détermine le loyer de la ligne.
        var mo = UIFactory.Section(content, "Loyer facturé", CoLoyer, CoLoyerL);
        // Période facturée : mois / trimestre / semestre / année selon la périodicité.
        UIFactory.Text(mo.transform, "Période facturée", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var perRow = UIFactory.HBox(mo.transform, 8, false, "PeriodeRow");
        UIFactory.LE(perRow.gameObject, minH: 46);
        _periodeDD = UIDropdown.Create(perRow.transform, new List<string>(MoisNoms),
            Enumerable.Range(1, 12).Select(i => i.ToString()).ToList(), 0,
            _ => { RefreshEntetePreview(); RefreshEcheanceDefault(); RefreshNumero(); RefreshTauxDefault(); });   // Revue 17 (2c) : le numéro repris suit la période ; (D5-08, lot 10) le taux aussi
        UIFactory.LE(_periodeDD.gameObject, flexW: 1, minH: 46);
        _annee = UIFactory.Input(perRow.transform, "Année");
        _annee.contentType = TMP_InputField.ContentType.IntegerNumber;
        _annee.characterLimit = 4;
        // Revue 17 (avenant de périodicité, lot 15) : les périodes proposées sont celles du plan de l'année saisie (RefreshPeriodes, d'abord).
        _annee.onValueChanged.AddListener(_ => { RefreshPeriodes(); RefreshEntetePreview(); RefreshEcheanceDefault(); RefreshNumero(); RefreshTauxDefault(); });
        UIFactory.LE(_annee.gameObject, prefW: 110, flexW: 0, minH: 46);

        _loyer = Labeled(mo, "Loyer HT (période)");
        SaisieNumerique.Montant(_loyer);   // retour BN2 (04/11) : la virgule aussi
        _loyer.onValueChanged.AddListener(_ => { RefreshMontants(); RefreshEntetePreview(); });
        // Provision des charges générales ; les listes spécifiques ont chacune leur
        // ligne juste en dessous (ProvListes), imprimée sous son nom.
        _provisionLabel = UIFactory.Text(mo.transform, "Provision pour charges", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _provision = UIFactory.Input(mo.transform, "Provision pour charges");
        SaisieNumerique.Montant(_provision);   // retour BN2 (04/11) : la virgule aussi
        _provision.onValueChanged.AddListener(_ => { RefreshMontants(); RefreshEntetePreview(); });
        _provListesBox = UIFactory.VBox(mo.transform, 6, 0, 0, 0, 0, "ProvListes").transform;
        _mTotalHT = MontRow(mo, "Total HT");
        _mTVA     = MontRow(mo, "TVA 20 %");
        _mTTC     = MontRow(mo, "Total TTC");

        // Les deux lignes optionnelles qui s'impriment JUSTE SOUS ces totaux. Elles
        // étaient rangées dans « Options & envoi », donc loin de ce qu'elles
        // commandent — impossible de deviner où décocher « Suite à votre demande… ».
        // La case et sa formulation vont ensemble, là où la ligne apparaît.
        _mentionTva = MentionTva.Creer(mo.transform, out _texteTvaDebit);
        _tauxTva = Labeled(mo, "Taux de TVA (%)");   // 02/11 : celui du bail ou de l'entreprise, modifiable pour cette facture
        _tauxTva.onValueChanged.AddListener(_ => { RefreshMontants(); RefreshPlateforme(); });   // Revue 17 (D5-08, lot 10 ; D5-10, lot 13) : la plateforme suit le taux du document

        // Ligne « montant mensuel » : seulement pour un loyer non mensuel.
        _mensuel = UIFactory.Toggle(mo.transform, "Ajouter « le montant mensuel à régler » (loyer de la période ÷ nb de mois)", true);
        _mensuel.onValueChanged.AddListener(_ => RefreshMontants());
        _texteMensuel = UIFactory.Input(mo.transform, FacturePdfService.MensuelDefaut, 46, true);
        SlashAutocomplete.Attach(_texteMensuel, "Ligne montant mensuel");   // Revue 17 (D6-08) : {montant} n'est proposé que dans cette phrase
        _aideMensuel = UIFactory.Text(mo.transform,   // Revue 17 (D6-24, coutures) : gardée, pour la cacher avec sa phrase (RefreshMensuel)
            "Dans cette phrase, {montant} porte le montant mensuel calculé. « / » ouvre la liste des variables.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);

        // ── 3. Règlement (pied du document) ──
        // L'échéance, la phrase qu'elle alimente et le RIB sont voisins : le lien se
        // voit, d'où la mention « (défaut de la phrase de règlement) » retirée du
        // libellé de l'échéance — elle ne servait qu'à compenser l'éloignement.
        var p = UIFactory.Section(content, "Règlement", CoReglement, CoReglementL);
        _echeance = Labeled(p, "Date d'échéance");
        // Coutures (10/11) : l'aperçu de l'entête suit l'échéance ({echeance} y gardait l'ancienne date), et la ligne « Envoi » aussi
        // (elle juge le jour où le loyer partira : DepartSaisi).
        _echeance.onValueChanged.AddListener(_ => { RefreshSommeDefault(); RefreshBoutonSauver(); RefreshEntetePreview(); RefreshModeEnvoi(); });
        _sommePhrase = Labeled(p, "Phrase de règlement (bas de facture, ex. « Valeur en votre aimable règlement »)");
        SlashAutocomplete.Attach(_sommePhrase);
        UIFactory.Text(p.transform, "RIB CIPL", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _ribDD = BuildRibDropdown(p.transform);

        _retard = UIFactory.Toggle(p.transform, "Ajouter la phrase de retard / pénalités de paiement", true);
        UIFactory.Text(p.transform,
            "Le texte de la phrase de retard et le bas de page se modifient dans Réglages — "
            + "ils sont imprimés à l'identique sur tous les documents.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);

        // ── Options / envoi ──
        // Ne reste ici que ce qui concerne l'ENVOI. Tout ce qui commande une ligne
        // imprimée vit dans la carte où cette ligne apparaît.
        var o = UIFactory.Section(content, "Options & envoi", CoVert, CoVertL);
        _modeInfo = UIFactory.Text(o.transform, "", UITheme.Role.Donnee, UITheme.TexteSecondaire);

        // Message d'accompagnement. Il appartient bien à l'envoi, donc à cette carte.
        UIFactory.Text(o.transform, "Objet du message", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _emailObjet = UIFactory.Input(o.transform, EmailService.ObjetDefaut);
        SlashAutocomplete.Attach(_emailObjet);
        UIFactory.Text(o.transform, "Corps du message", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _emailCorps = UIFactory.Input(o.transform, EmailService.CorpsDefaut, 110, true);
        SlashAutocomplete.Attach(_emailCorps);
        _aideEmail = UIFactory.Text(o.transform,
            "« / » ouvre la liste des variables. La facture est jointe en PDF automatiquement.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        _emailEnvoi = Labeled(o, "Email d'envoi");

        // Bouton « Générer facture » sous le formulaire (rend l'aperçu à droite).
        var gen = UIFactory.Button(left.transform, "Générer facture", UITheme.Primaire, Color.white, 46, UITheme.Role.Bouton);
        UIFactory.LE(gen.gameObject, minH: 56, flexH: 0);
        gen.onClick.AddListener(GenererApercu);

        // ── Colonne droite : aperçu de la facture rendue ──
        var right = UIFactory.VBox(main.transform, 12, 0, 0, 0, 0, "Right");
        UIFactory.LE(right.gameObject, flexW: 58);
        right.childForceExpandHeight = false;

        UIFactory.Text(right.transform, "Facture générée", UITheme.Role.Section, UITheme.TextePrincipal, true);

        // Fond gris (type visionneuse PDF) qui occupe la hauteur restante ; la feuille
        // (image A4) est centrée dedans, avec zoom (molette / boutons) et déplacement.
        var backdrop = UIFactory.Panel("PvBackdrop", right.transform, Hex("#D2D0CB"));
        UIFactory.Border(backdrop.gameObject);
        UIFactory.LE(backdrop.gameObject, flexH: 1);
        var backRT = (RectTransform)backdrop.transform;
        backRT.pivot = new Vector2(.5f, .5f);
        backdrop.gameObject.AddComponent<RectMask2D>();     // masque l'image qui déborde

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
        _previewImg.color = new Color(1, 1, 1, 0);          // invisible tant qu'aucune image
        _previewImg.raycastTarget = false;                  // les gestes vont au fond (visionneuse)

        // Visionneuse (zoom molette + déplacement) portée par le fond.
        _viewer = backdrop.gameObject.AddComponent<FacturePreviewViewer>();
        _viewer.Init(backRT, sheet);

        // Barre de zoom (coin haut-droit du cadre) : −  ⟳  +
        var zbar = UIFactory.HBox(backdrop.transform, 6, false, "ZoomBar");
        var zbarRT = (RectTransform)zbar.transform;
        zbarRT.anchorMin = zbarRT.anchorMax = new Vector2(1, 1); zbarRT.pivot = new Vector2(1, 1);
        zbarRT.anchoredPosition = new Vector2(-10, -10);
        ZoomBtn(zbar.transform, "−", () => _viewer.ZoomBy(1f / 1.25f));
        ZoomBtn(zbar.transform, "Ajuster", () => _viewer.Fit(), 84);
        ZoomBtn(zbar.transform, "+", () => _viewer.ZoomBy(1.25f));

        // Bouton principal. Son libellé suit la date d'échéance : avant la date
        // d'envoi, il annonce le jour du départ au lieu de promettre un envoi immédiat.
        _btnSauver = UIFactory.Button(right.transform, "Sauvegarder et envoyer", Hex("#854F0B"), Color.white, 46, UITheme.Role.Bouton);
        UIFactory.LE(_btnSauver.gameObject, minH: 56, flexH: 0);
        // Revue 17 (D5-17, coutures) : un taux de TVA inhabituel tapé pour cette facture (« 2 » pour « 20 »), autre que celui du bail ou
        // de la facture corrigée, se confirme avant l'émission ; « Annuler » laisse le formulaire ouvert avec la saisie.
        _btnSauver.onClick.AddListener(() => Tva.ConfirmerTaux(TauxFacture(), Tva.TauxPropose(_loc, null, Corrigee()), SauvegarderEtEnvoyer));
    }

    static float Cents(float v) => Tva.Cents(v);   // Revue 17 (D5-03) : décimal, « au plus loin », comme le XML

    // Construit les données de la facture (au visuel réel) depuis l'UI courante.
    FacturePdfService.Data BuildData()
    {
        var ctx = BuildContext();
        var rib = ReglageService.GetRib(_ribDD?.SelectedId);
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        // Revue 16c (D5-16) : au centime ligne par ligne, et TTC = HT + TVA arrondie : le PDF et le XML du Factur-X disent pareil.
        float loyer = Cents(ParseF(_loyer.text)), prov = Cents(ProvisionSaisie());
        float taux = TauxFacture();
        float totalHT = loyer + prov, tva = Tva.Montant(totalHT, taux), ttc = totalHT + tva;
        // Montant mensuel = TTC de la période ÷ nombre de mois de la période (hors mensuel).
        // Revue 17 (avenant de périodicité, lot 15) : les mois de la PÉRIODE facturée (MoisFactures), pas ceux de la périodicité de la
        // fiche : un loyer de mai, mensuel avant un passage au trimestriel, n'a pas de ligne « mensuel » ; « mai à juin » se divise par 2.
        int moisParPeriode = MoisFactures(_loc, PeriodeAnnee(), PeriodeChoisie());
        bool afficheMensuel = _mensuel != null && _mensuel.isOn && moisParPeriode > 1;
        string entResolved = ent != null ? FactureVarResolver.Resolve(ent.texte, _loc, _bat, ctx) : "";
        var foot = (R.basDePage ?? "").Replace("\r", "").Split('\n');
        string dateStr = ctx.date.ToString("d MMMM yyyy", FacturePdfService.FrCulture);

        return new FacturePdfService.Data
        {
            clientNom = _nom.text,
            clientAdresseHtml = FacturePdfService.AdresseHtml(_adresse.text),
            clientSiret = _siret.text,
            refInterne = _refInterne.text,
            dateStr = dateStr,
            numero = NumeroDocument(),
            subtitle = $"Loyer {ctx.periode}",
            bodyHtml = FacturePdfService.BodyHtml(entResolved),
            ligneLabel = LigneLoyer(loyer),
            totalPeriode = loyer, provision = prov, totalHT = totalHT, tva = tva, ttc = ttc,
            masquerTva = taux <= 0f, tvaLibelle = Tva.Libelle(taux),   // 02/11 : exonéré = total seul
            lignesProvision = LignesProvision(),
            tvaDebit = taux <= 0f || MentionTva.Imprimee(_mentionTva), retard = _retard.isOn,
            afficherMensuel = afficheMensuel, montantMensuel = afficheMensuel ? ttc / moisParPeriode : 0f,
            // Les textes libres passent par le même résolveur que l'entête : sans ça,
            // le menu « / » proposerait d'insérer {loc.nom}… qui s'imprimerait tel quel
            // sur un document envoyé au client.
            texteTvaDebit = taux <= 0f ? Tva.Mention(R) : FactureVarResolver.Resolve(MentionTva.Phrase(_mentionTva, _texteTvaDebit), _loc, _bat, ctx),
            texteMensuel = FactureVarResolver.Resolve(_texteMensuel.text, _loc, _bat, ctx),
            // Revue 17 (D6-11, coutures) : un loyer à total négatif est titré « AVOIR » : une phrase « À NOUS RÉGLER » laissée dessus
            // devient la phrase de remboursement, comme dans les trois autres formulaires (FactureEmission.PhraseSomme).
            sommePhrase = FactureVarResolver.Resolve(FactureEmission.PhraseSomme(_sommePhrase.text, ttc), _loc, _bat, ctx),
            ribTitulaire = rib?.titulaire, ribDomiciliation = rib?.domiciliation,
            ribNum = rib?.rib, ribIban = rib?.iban, ribBic = rib?.bic,
            legal = R.phraseRetard, escompte = R.phraseEscompte,
            foot1 = foot.Length > 0 ? foot[0] : "",
            foot2 = foot.Length > 1 ? foot[1] : "",
        };
    }

    string FactureDir() => DossiersDonnees.DossierFactures(
        _fiche.batimentPrefabOrigin.getName(), _loc.Name);

    // « Générer facture » : mémorise les champs et rend l'aperçu (image) à droite.
    // Ne consomme PAS de numéro, n'émet rien — juste la visualisation, ré-cliquable.
    void GenererApercu()
    {
        // Revue 17 (D6-05) : une date vide ou illisible est refusée au centre, le formulaire reste ouvert avec la saisie. Avant,
        // SaveFromUI l'enregistrait vide : facture datée du jour, échéance perdue (le loyer partait tout de suite).
        if (SaisieDate.RefusFacture(_date.text, _echeance.text) is string refusDate) { ConfirmDialog.Erreur(refusDate); return; }
        SaveFromUI();
        var d = BuildData();
        string png = DossiersDonnees.Apercu("apercu.png");
        if (FacturePdfService.GeneratePreviewPng(d, png, out string err)) ShowPreview(png);
        else ConfirmDialog.Erreur("Échec de l'aperçu : " + err);
    }

    // « Sauvegarder et envoyer » : mémorise + génère le PDF au visuel réel (local),
    // consomme le numéro (séquence +1) et rafraîchit l'aperçu. AUCUN envoi réel n'est
    // effectué ici (l'envoi se décide plus bas).
    void SauvegarderEtEnvoyer()
    {
        // L'envoi est asynchrone : sans cette garde, un double clic partirait deux fois.
        if (_envoiEnCours) { UndoToast.Instance?.ShowInfo("Un envoi est déjà en cours."); return; }
        if (SaisieDate.RefusFacture(_date.text, _echeance.text) is string refusDate) { ConfirmDialog.Erreur(refusDate); return; }   // Revue 17 (D6-05)

        SaveFromUI();
        var fl = _loc.factureLoyer;
        string key = CleLoyer();   // Revue 17 (2c) : la clé de la ligne « N° de facture » (période et année que SaveFromUI vient d'enregistrer)

        // Facture déjà émise pour cette période → on refait une version « corrigée(X) »
        // (même numéro, sans consommer de nouvelle séquence).
        // Revue 16c (D6-03) : le numéro décidé AVANT de construire le document : l'entête, la phrase de règlement et
        // l'e-mail ({numero}) portaient le numéro proposé, pas celui de la facture (correction, réécriture).
        var emission = FactureEmission.Preparer(_loc, key, ComposedNumero(), PeriodiciteChoisie().ToString());   // Revue 17 (D4-04 ; lot 15 : la périodicité de la période)
        if (emission.Refus != null) { ConfirmDialog.Erreur(emission.Refus); return; }   // D4-04 : numéro déjà pris
        _numeroEmis = emission.NumeroFacture;
        var d = BuildData();
        bool correction = emission.Correction;

        // Revue 17 (D6-19, coutures ; décision du 09/11) : un total nul est refusé AVANT tout : le PDF, la branche « premiers pas » et
        // l'envoi différé. Avant : après la génération du PDF et la branche « premiers pas » (un loyer de 0 € y prenait un numéro).
        // Revue 16c (D5-16) puis BI5 (02/11) : une ligne négative part en quantité −1, un TOTAL négatif part en avoir (381), tous
        // deux dans FactureEn16931 ; un total nul n'est ni une facture ni un avoir : rien à facturer, plateforme ou non (BI8 : sans
        // TVA, la plateforme ne le concernait pas et un loyer de 0 € partait par e-mail).
        if (FactureEmission.RefusTotalNul(d.ttc, d.subtitle) is string refusNul) { ConfirmDialog.Erreur(refusNul); return; }
        // Revue 17 (D6-15, coutures) : sans RIB, le relevé d'identité bancaire de la facture serait vide : refusé avant le PDF, « premiers
        // pas » compris (la facture émise sans envoi part plus tard). Pas sur le bâtiment d'exemple : rien n'en part jamais.
        bool exemple = BatimentExemple.Est(_bat);
        if (!exemple && FactureEmission.RefusSansRib(ReglageService.GetRib(_ribDD?.SelectedId)) is string refusRib) { ConfirmDialog.Erreur(refusRib); return; }

        // Revue 17 (D5-07, lot 11) : la plateforme se décide UNE fois, ici, et suit jusqu'à Finaliser : si l'état de Super PDP change pendant
        // la confirmation (lecture au lancement, Délier), le refus et la facture électronique voyaient deux états différents.
        bool parPlateforme = FactureElectronique.Concerne(_loc, d.tva, SiretSaisi());   // lu dès l'envoi différé (lot 6)
        // Premiers pas, bâtiment d'exemple : émise, pas envoyée (même chemin que l'envoi différé) : les refus de l'envoi, ci-dessous, ne
        // la concernent pas (comme avant).
        bool sansEnvoi = SansEnvoi || exemple;

        var dateEnvoi = FacturationSuivi.DateEnvoi("Loyer", fl.dateEcheanceISO, _loc);
        // Revue 17 (D5-05, lot 11) : les refus passent AVANT l'envoi différé (plus bas) : un avoir sans facture à citer ou une facture qui
        // doit partir par la plateforme et ne le peut pas, préparés à l'avance, étaient enregistrés et partaient à leur date.
        // Coutures (reste du lot 11) : et AVANT la génération du PDF : un refus ne laisse plus sur le disque un fichier « corrigée(X) »
        // qui n'a jamais été émis.
        // Revue 17 (D5-02) : la correction d'une facture transmise ne part pas en avoir (la plateforme garderait les deux). Lot 11 (D5-07,
        // D5-05, décision du 05/11) : hors plateforme, la correction d'une facture transmise est refusée ici (plus un message après l'e-mail),
        // et, dès la bascule, une facture qui doit partir par la plateforme aussi ; la date qui compte est celle du départ (envoi différé).
        DateTime depart = !correction && dateEnvoi > DateTime.Today ? dateEnvoi.Value : DateTime.Today;
        string refusCorrection = sansEnvoi ? null
                               : FactureElectronique.RefusCorrection(_loc.facturesEtat?.Find(x => x.key == key), d.ttc, d.subtitle, parPlateforme,
                                     FactureElectronique.RaisonHorsPlateforme(_loc, d.tva, SiretSaisi()))
                              ?? FactureElectronique.RefusObligation(_loc, d.tva, SiretSaisi(), d.subtitle, parPlateforme, depart);
        if (refusCorrection != null) { ConfirmDialog.Erreur(refusCorrection); return; }
        // BI8 (02/11) : un avoir cite les factures qu'il corrige (BR-FR-CO-05) : la version précédente, sinon les loyers de l'année
        // (leurs provisions couvrent ce qui est rendu).
        var citees = parPlateforme && d.ttc < -0.004f
            ? FactureElectronique.ReferencesAvoir(_loc, _loc.facturesEtat?.Find(x => x.key == key), FactureElectronique.AnneeDeCle(key)) : null;
        // « Premiers pas » et bâtiment d'exemple compris (coutures) : l'avoir électronique serait construit quand même, sans rien à citer
        // (avant, il s'y citait lui-même : sa ligne venait d'être enregistrée).
        if (citees != null && citees.Count == 0)
        {
            // Revue 17 (D5-14, coutures) : la raison suit la règle : un loyer ENVOYÉ au locataire (avant : « émis », faux pour un loyer en attente d'envoi).
            ConfirmDialog.Erreur(FactureElectronique.RefusAvoir(d.subtitle,
                $"Aucun loyer {FactureElectronique.AnneeDeCle(key)} envoyé au locataire à citer (un loyer en attente d'envoi, ou refusé par Super PDP, ne compte pas) : "
                + "corrigez plutôt une facture existante depuis le suivi (« Corriger »)."));
            return;
        }
        // Revue 17 (D5-15, coutures) : par la plateforme, le PDF de l'avoir nomme les factures que son XML cite (« Avoir sur la facture
        // n° … », mêmes références que Finaliser). Avant : le XML citait, le PDF du même Factur-X ne nommait rien.
        if (citees != null) d.factureOrigine = FactureElectronique.NumerosCites(citees);

        string dir = FactureDir();
        string fname = Sanitize($"Loyer-{_nom.text}-{d.subtitle}{emission.SuffixeFichier}") + ".pdf";
        string pdf = DossiersDonnees.CheminFacture(dir, fname);

        if (!FacturePdfService.GeneratePdf(d, pdf, out string err))
        {
            ConfirmDialog.Erreur("Échec génération PDF : " + err);
            return;
        }

        // Aperçu à jour dans l'appli à partir du même rendu.
        string png = DossiersDonnees.Apercu("apercu.png");
        if (FacturePdfService.GeneratePreviewPng(d, png, out _)) ShowPreview(png);

        if (sansEnvoi)
        {
            Finaliser(d, key, emission, correction, pdf, false, parPlateforme, exemple
                ? "  Bâtiment d'exemple : rien n'est envoyé."
                : "  Émise sans envoi (premiers pas) : elle attend dans le suivi de facturation.");
            return;
        }

        // Avant la date d'envoi, RIEN ne part : la facture est préparée et attendra
        // son jour, où elle sera proposée au lancement de l'application. Un loyer
        // expédié un mois trop tôt ne se rappelle pas, et le calendrier existe
        // précisément pour ça.
        // Revue 16c (D4-03) : la correction d'une facture déjà partie s'envoie tout de suite ; la fenêtre « elle partira
        // le … » promettait un envoi que l'envoi automatique ne ferait jamais (la ligne reste « Envoyé »).
        if (!correction && dateEnvoi.HasValue && DateTime.Today < dateEnvoi.Value)
        {
            // Revue 17 (D4-01, lot 6 ; D4-N3) : sans adresse, la facture dormirait « en attente d'envoi » sans jamais partir ni
            // prévenir (le cessionnaire d'une cession prévue n'en a pas toujours). Refusée ici, comme l'envoi immédiat ; par la
            // plateforme seule, elle part sans e-mail (même règle que « Transmettre »).
            if (string.IsNullOrWhiteSpace(_emailEnvoi.text) && !(parPlateforme && FactureElectronique.NotificationSeule))
            {
                ConfirmDialog.Erreur($"Aucune adresse email pour ce locataire : la facture ne pourrait pas partir le {dateEnvoi.Value:dd/MM/yyyy}. "
                    + "Elle n'est pas enregistrée : renseignez « Email d'envoi », puis recommencez. Le PDF est généré.");
                return;
            }
            PreparerPourEnvoi(d, key, emission, correction, pdf, dateEnvoi.Value, parPlateforme);
            return;
        }

        // Étape 18b : dès la bascule (1/9/2027), un loyer qui part par la plateforme n'est plus envoyé en PDF ; le
        // locataire reçoit un e-mail de notification une fois Super PDP d'accord (FactureElectronique.Notifier).
        // Une transmission ne se rappelle pas : elle se confirme, comme un e-mail. Une correction d'une facture
        // déjà transmise part aussi par la plateforme, comme facture rectificative (Finaliser, 31/10).
        if (parPlateforme && FactureElectronique.NotificationSeule)
        {
            // Revue 17 (D5-10, lot 13) : la confirmation est partagée avec la régularisation et la refacturation.
            FactureElectronique.ConfirmerTransmission(_emailEnvoi.text,
                () => Finaliser(d, key, emission, correction, pdf, true, parPlateforme, "  Transmise par la plateforme agréée."));
            return;
        }

        // Envoi demandé. On refuse AVANT d'ouvrir la confirmation si quelque chose
        // manque : mieux vaut un message net qu'une boîte de dialogue qui promet un
        // départ impossible.
        string dest = (_emailEnvoi.text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dest))
        {
            ConfirmDialog.Erreur("Aucune adresse email pour ce locataire. "
                + "La facture n'a pas été envoyée ; le PDF est enregistré.");
            return;
        }

        string manque = EmailService.CeQuiManque();
        if (manque != null) { ConfirmDialog.Erreur(manque + " Le PDF est enregistré."); return; }

        var ctx = BuildContext();
        // Le texte part tel qu'affiché (e-mail de facture, ou « copie » si elle part aussi par la plateforme) s'il est du vrai mode ;
        // sinon le texte du vrai mode (ouvert en « copie », mais plus de plateforme à l'envoi, ou l'inverse). Revue 17 (D5-10, lot 13) :
        // la règle « copieFausse », dans les deux sens, partagée avec la régularisation et la refacturation (EmailService.TexteEnvoi).
        var (eo, ec) = EmailService.TexteEnvoi(_loc.factureLoyer, parPlateforme, !_modeNotif && _modeCopie == parPlateforme, _emailObjet.text, _emailCorps.text);
        string objet = FactureVarResolver.Resolve(eo, _loc, _bat, ctx);
        string corps = FactureVarResolver.Resolve(ec, _loc, _bat, ctx);

        // Sans boîte de confirmation disponible, on n'envoie PAS : un départ non
        // confirmé vaut moins qu'un PDF enregistré qu'on renverra plus tard.
        if (ConfirmDialog.Instance == null)
        {
            ConfirmDialog.Erreur("Confirmation indisponible : rien n'a été envoyé. "
                + "Le PDF est enregistré.");
            return;
        }

        // Un email ne se rappelle pas : on montre exactement ce qui va partir.
        ConfirmDialog.Instance.Show(
            "Envoyer la facture par email ?",
            $"À : {dest}\nObjet : {objet}\nPièce jointe : {Path.GetFileName(pdf)}\n\n"
            + "Le message part immédiatement et ne pourra pas être rappelé.",
            () => StartCoroutine(EnvoyerPuisFinaliser(d, key, emission, correction, pdf, parPlateforme, dest, objet, corps)),
            "Envoyer");
    }

    /// Facture préparée avant sa date d'envoi : le PDF est généré, la ligne passe « en
    /// attente d'envoi », et le départ se fera le jour dit. Le panneau l'annonce — une
    /// facture qu'on croit partie et qui dort est pire qu'un envoi manuel.
    void PreparerPourEnvoi(FacturePdfService.Data d, string key, FactureEmission.Decision emission,
                           bool correction, string pdf, DateTime dateEnvoi, bool parPlateforme)
    {
        TryDate(_echeance.text, out var ech);
        string quand = dateEnvoi.ToString("dddd d MMMM yyyy", FacturePdfService.FrCulture);
        string detail =
            $"Cette facture sera envoyée le {quand}, soit "
            + $"{FacturationSuivi.EnvoiAvant(_loc)} jours avant son échéance du {ech:dd/MM/yyyy}.\n\n"
            + "Elle est enregistrée dès maintenant et reste « en attente d'envoi ». "
            + "L'application vous la proposera au lancement, le jour venu.\n\n"
            + "Pour l'envoyer tout de suite malgré tout, avancez la date d'échéance ou "
            + "utilisez le bouton « Refaire » depuis le suivi le jour de l'envoi.";   // coutures (D4-10) : le bouton d'une facture pas encore partie

        void Enregistrer() => Finaliser(d, key, emission, correction, pdf, false, parPlateforme,
            $"  Elle partira le {dateEnvoi:dd/MM/yyyy}.");

        if (ConfirmDialog.Instance != null)
            ConfirmDialog.Instance.Show("Facture prête pour l'envoi différé", detail,
                Enregistrer, "Enregistrer");
        else
            Enregistrer();
    }

    /// Envoie, attend le résultat, et n'enregistre QUE si le message est parti.
    ///
    /// L'ordre n'est pas négociable : `FactureEmission.Enregistrer` écrit « Envoyé »
    /// dans le suivi et consomme le numéro. L'appeler avant de savoir ferait dire au
    /// suivi plus qu'il ne sait — c'est le défaut H3, corrigé en septembre.
    ///
    /// Conséquence voulue d'un échec : rien n'est consommé, donc un nouvel essai
    /// reprend le même numéro et réécrit le même PDF.
    System.Collections.IEnumerator EnvoyerPuisFinaliser(
        FacturePdfService.Data d, string key, FactureEmission.Decision emission,
        bool correction, string pdf, bool parPlateforme, string dest, string objet, string corps)
    {
        // Un second clic produirait un second envoi. Revue 17 (D4-07) : compté : rechargement et fermeture de CIPL attendent la fin.
        FactureEmission.NoterEnvoi(ref _envoiEnCours, true);
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
        Finaliser(d, key, emission, correction, pdf, true, parPlateforme, $" et envoyée à {dest}", dest);
        if (_fermerApresEnvoi) Close();   // Revue 17 (D4-07, coutures) : la fermeture demandée pendant l'envoi, une fois la facture enregistrée
    }

    /// Enregistrement du suivi, commun aux deux chemins (sans envoi, ou après un
    /// envoi réussi). Correction ou première émission : la règle vit dans
    /// FactureEmission, plus dans chacun des quatre panneaux.
    /// `dest` (Revue 17, D4-01, lot 6) : l'adresse à laquelle l'e-mail est réellement parti ; sinon celle du formulaire.
    /// `parPlateforme` (Revue 17, D5-07, lot 11) : décidé par SauvegarderEtEnvoyer, le même pour le refus, l'e-mail et la facture électronique.
    void Finaliser(FacturePdfService.Data d, string key, FactureEmission.Decision emission,
                   bool correction, string pdf, bool envoye, bool parPlateforme, string suffixeMessage = "", string dest = null)
    {
        var fl = _loc.factureLoyer;
        // Étape 18b : correction d'une facture déjà transmise → facture rectificative, qui cite la dernière transmise
        // (lue avant que l'enregistrement ne remplace la ligne).
        var avant = _loc.facturesEtat?.Find(x => x.key == key);
        var (precedentNumero, precedentDate) = FactureEn16931.Remplacee(avant);
        // Revue 17 (D5-14 / D5-15, coutures) : ce qu'un avoir cite se lit AVANT l'enregistrement, comme la facture remplacée (BI8 : la
        // version précédente, sinon les loyers de l'année) : après, la ligne de cet avoir existe, ou vient de passer « Envoyé », et il se
        // citait lui-même, alors que son PDF, écrit avant, ne la nomme pas.
        var citees = FactureElectronique.ReferencesAvoir(_loc, avant, FactureElectronique.AnneeDeCle(key));

        FacturationSuivi.Solde(_loc, key, out float totalAvantCorrection, out _);   // Revue 17 (D4-03) : avant la correction
        string message = FactureEmission.Enregistrer(_loc, key, "Loyer", emission,
            d.subtitle, fl.dateEcheanceISO, pdf, d.ttc, _ribDD?.SelectedId,
            _loc.factureLoyer, "Facture", envoye);
        FacturationSuivi.ApresCorrection(_loc, key, totalAvantCorrection);   // Revue 17 (D4-03) : payée, l'écart redevient dû
        // Loyer HT facturé, mémorisé sur la ligne : un départ saisi après coup repère
        // ainsi une période facturée en entier (DepartLocataire.LoyerACorriger).
        var ligne = _loc.facturesEtat?.Find(x => x.key == key);
        if (ligne != null) ligne.loyerHT = d.totalPeriode;
        if (ligne != null) ligne.tauxTva = d.masquerTva ? 0f : TauxFacture();   // 02/11 : le taux de l'époque (relecture du HT, e-mails)
        if (ligne != null)   // Revue 17 (D6-07) : les provisions imprimées, pour la correction de CETTE facture
        {
            // Revue 17 (D4-04 ; avenant de périodicité, lot 15) : la périodicité de la PÉRIODE émise (celle de sa tranche dans le plan),
            // pas celle de la fiche : mai émis après la saisie d'un passage au trimestriel au 1er juillet reste « mensuel ».
            ligne.periodicite = (DepartLocataire.LirePeriode(key, out int ka, out int kp) ? FacturationSuivi.PeriodiciteDe(_loc, ka, kp) : _loc.periodiciteLoyer).ToString();
            ligne.provisionsConnues = true;
            // Lot 16 : au centime, telles qu'imprimées ligne à ligne : la régularisation les additionne (Loyers.ProvisionsAppelees).
            ligne.provision = Cents(ParseF(_provision.text));
            ligne.provisionsListes = _provListes.Select(x => new MontantListe { listeId = x.id, montant = Cents(ParseF(x.inp.text)) }).ToList();
        }
        // Revue 17 (D4-01, décision du 05/11, lot 6) : pour qui cette facture est préparée : le titulaire de la période (cédant, futur
        // cessionnaire ou fiche) et l'adresse réellement envoyée, sinon celle du formulaire. Lus par l'envoi différé, le rappel et la
        // notification ; un loyer préparé au nom d'un autre que le titulaire (cession saisie après) est retenu (HorsTitulaire).
        FacturationSuivi.PoserAcheteur(_loc, key, Cessions.Titulaire(_loc, ligne), dest ?? _emailEnvoi.text);

        // Étape 18b : la facture électronique est gardée sur la ligne ; elle part à la plateforme dès que la
        // ligne est « Envoyé » (tout de suite ci-dessous, ou le jour de l'envoi différé). Hors plateforme, Preparer
        // reçoit null : une ancienne version jamais transmise ne repartira pas (revue 16c, D5-01). Revue 17 (D5-07, lot 11) : la
        // correction hors plateforme d'une facture transmise est refusée avant l'envoi (RefusCorrection), plus prévenue ici après coup.
        if (ligne != null)
            FactureElectronique.Preparer(ligne, !parPlateforme ? null : FactureEn16931.Json(new FactureEn16931.Entree
            {
                numero = d.numero, dateISO = BuildContext().date.ToString("yyyy-MM-dd"), echeanceISO = fl.dateEcheanceISO,
                vendeurNom = R.entrepriseNom, vendeurSiren = SocieteLocataire.Siren(SessionCloud.Entreprise?.siret),
                acheteurNom = d.clientNom, acheteurSiren = SocieteLocataire.Siren(FactureElectronique.SiretAcheteur(_loc, SiretSaisi())),
                acheteurSiret = FactureElectronique.Siret14(FactureElectronique.SiretAcheteur(_loc, SiretSaisi())), acheteurAdresse = _adresse.text,
                lignes = FactureEn16931.Lignes(d), mention = d.tvaDebit ? d.texteTvaDebit : null, penalites = d.legal, escompte = d.escompte,
                tauxTva = TauxFacture(),
                precedentNumero = precedentNumero, precedentDateISO = precedentDate,
                references = citees,
            }));

        _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        FactureElectronique.Rattraper(_fiche.batimentPrefabOrigin, _loc);
        LocataireSuiviInline.RefreshFor(_fiche);   // Suivi à jour tout de suite

        // Revue 17 (2c, décision du 06/11) : la ligne « N° de facture » reprend le numéro de la période, correction comprise (la
        // suivante sera « corrigée(2) » ; avant, rien n'était rafraîchi après une correction), et l'aperçu avec elle.
        if (correction) _ligneCiblee = null;
        else if (_numeroId != null) _numeroId.text = "";
        RefreshNumero();
        // Revue 17 (D6-04) : la période affichée vient de partir, ou reste une correction : titre et bouton le disent (avant : le
        // titre repassait sans « (correction) » après une correction, et un second clic corrigeait encore).
        RefreshBoutonSauver();
        // Revue 17 (D5-08, lot 10) : le taux proposé repart de la facture telle qu'émise : un taux tapé pour cette correction ne suit pas
        // une autre période choisie ensuite (RefreshTauxDefault le reprend comme une proposition).
        ChargerTva(fl);

        UndoToast.Instance?.ShowInfo(message + suffixeMessage);
    }

    /// La provision proposée : celle de la fiche, sauf pour la correction d'une facture déjà partie, qui garde son
    /// montant imprimé (revue 16c, D6-04). Revue 17 (lot 16) : `provisionFiche` est celle de la fiche ramenée à la périodicité de
    /// la période affichée, et `ligneCiblee` la facture partie de cette période (DefaultProvision).
    /// Décision du 15/11 : pour une facture émise avant D6-07 (provision imprimée inconnue), `deduite` est la provision retrouvée à
    /// partir de son montant imprimé (FactureLoyerPanel.ProvisionDeduite) ; avant, la provision mémorisée par la DERNIÈRE utilisation
    /// du formulaire, d'une autre période (210 € au lieu de 0 sur le loyer de mai de sephora : la correction partait à 1 252 €).
    public static float ProvisionProposee(FactureInfo f, float provisionFiche, FactureEtat ligneCiblee, float? deduite = null)
    {
        // Sans locataire (D4-05) : « Envoyé » ou « Impayé », la facture est partie dans les deux cas.
        var e = ligneCiblee != null ? FacturationSuivi.EtatDe(ligneCiblee, null) : FacturationSuivi.Etat.AFaire;
        bool partie = e == FacturationSuivi.Etat.Envoye || e == FacturationSuivi.Etat.Impaye || e == FacturationSuivi.Etat.Paye;
        if (partie && ligneCiblee.provisionsConnues) return ligneCiblee.provision;   // Revue 17 (D6-07) : celle de CETTE facture
        if (partie && deduite.HasValue) return deduite.Value;
        return f != null && f.saved && partie ? f.provisionMontant : provisionFiche;
    }

    /// Décision du 15/11 : la provision générale d'une facture de loyer émise sans provision connue (avant D6-07), retrouvée à partir de
    /// ce qu'elle a imprimé : son HT (montant TTC ÷ (1 + taux)) moins le loyer de sa période et les provisions des listes. Jamais négative.
    /// ponytail: suppose que le loyer de la période n'a pas été retouché à la main sur la facture d'origine ; le champ reste modifiable.
    public static float ProvisionDeduite(FactureEtat ligne, float loyerPeriode, float provisionsListes)
    {
        if (ligne == null) return 0f;
        double ht = ligne.montant / (1.0 + Math.Max(0f, ligne.tauxTva) / 100.0);
        return (float)Math.Max(0.0, Math.Round(ht - loyerPeriode - provisionsListes, 2, MidpointRounding.AwayFromZero));
    }

    // Charge l'image rendue dans le RawImage d'aperçu (ratio ajusté à l'image).
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

    static string Sanitize(string s) => DossiersDonnees.NomFichier(s);

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

    // Ligne du n° de facture : [ préfixe format (auto, lecture) ] [ ID locataire (saisie) ].
    // Le n° complet = préfixe + ID. Le préfixe suit le format choisi + la date ; l'ID est
    // le numéro de facturation propre au locataire, que l'utilisatrice remplit librement.
    void BuildNumeroRow(VerticalLayoutGroup body)
    {
        _numeroAide = UIFactory.Text(body.transform, "N° de facture  =  format  +  ID locataire", UITheme.Role.Aide, UITheme.TexteSecondaire);
        var row = UIFactory.HBox(body.transform, 8, false, "NumRow");
        UIFactory.LE(row.gameObject, minH: 46);

        // Préfixe (« format possible ») — encadré, en lecture seule.
        var pfx = UIFactory.Panel("NumPrefixe", row.transform, UITheme.Fond);
        UIFactory.Border(pfx.gameObject, UITheme.Bordure);
        var pl = pfx.gameObject.AddComponent<HorizontalLayoutGroup>();
        pl.padding = new RectOffset(14, 14, 0, 0);
        pl.childControlWidth = true; pl.childControlHeight = true;
        pl.childForceExpandWidth = false; pl.childForceExpandHeight = true;
        pl.childAlignment = TextAnchor.MiddleCenter;
        UIFactory.LE(pfx.gameObject, minH: 46, minW: 96, flexW: 0);
        _numeroPrefixe = UIFactory.Text(pfx.transform, "—", UITheme.Role.Libelle, UITheme.TextePrincipal, true);

        // ID locataire — champ de saisie (numéro de facturation du locataire).
        _numeroId = UIFactory.Input(row.transform, "ID locataire (ex. 001)");
        UIFactory.LE(_numeroId.gameObject, minH: 46, flexW: 1);
        // Revue 17 (2c, décision du 06/11) : un ID retapé après une émission abandonnée (« Annuler » à l'envoi…) : l'entête et
        // l'aperçu le suivent (avant : le numéro décidé par cette émission, alors que la ligne et le PDF portaient le nouveau).
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

    // ── Chargement ─────────────────────────────────────────────────────────────

    void LoadIntoUI()
    {
        if (_loc == null) return;
        var f = _loc.factureLoyer;   // peut être null (jamais rempli)
        _chargement = true;   // Revue 17 (D5-10, lot 13) : le SIRET et le taux posés ici ne déclenchent pas RefreshPlateforme, appelé en dernier
        _acheteurFiche = null;   // Revue 17 (D7a-03) : posé plus bas, une fois la période connue
        // (le titre, avec le nom du locataire, suit la période : RefreshBoutonSauver, en fin de chargement ; Revue 17, D6-04)

        // Destinataire : on reprend la valeur mémorisée sur la facture si elle est
        // renseignée, sinon on retombe sur celle du locataire (préremplissage).
        // → un champ dest laissé vide (ex. SIRET jamais saisi) réaffiche la donnée
        //   du locataire au lieu de rester vide.
        _nom.text     = !string.IsNullOrEmpty(f?.destNom)     ? f.destNom     : (_loc.Name ?? "");
        _adresse.text = !string.IsNullOrEmpty(f?.destAdresse) ? f.destAdresse : (_loc.adresseLocataire ?? "");
        _siret.text   = !string.IsNullOrEmpty(f?.destSiret)   ? f.destSiret   : (_loc.siretNumber ?? "");

        // RIB / entête (dropdowns) : options rafraîchies + sélection mémorisée.
        _ribDD.SetOptions(
            R.ribs.Select(r => string.IsNullOrWhiteSpace(r.name) ? "(RIB)" : r.name).ToList(),
            R.ribs.Select(r => r.id).ToList(), f?.ribId);
        _enteteDD.SetOptions(
            R.entetes.Select(e => string.IsNullOrWhiteSpace(e.nom) ? "(entête)" : e.nom).ToList(),
            R.entetes.Select(e => e.id).ToList(), ReglageService.EnteteChoisi(f?.enteteId, "Loyer"));

        DateTime now = DateTime.Today;
        _numeroEmis = null;
        _date.text = now.ToString("dd/MM/yyyy");   // revue 16c, D6-02 : aujourd'hui, pas la date de la facture précédente
        // L'échéance — et la phrase de règlement qu'elle alimente — sont posées PLUS
        // BAS, après le sélecteur de période : le loyer est dû au jour de demande de
        // la période facturée, donc la période doit être connue d'abord.
        // NOTE : `f.dateEcheanceISO` n'est PAS lu ici. C'est le réglage du type de
        // facture, donc partagé par toutes les périodes : une échéance saisie une fois
        // (par exemple le 21/10) se réimposait ensuite à chaque nouvelle période, en
        // écrasant le jour de demande du locataire. L'échéance appartient à la facture
        // d'UNE période — DefaultEcheance va la chercher sur sa ligne de suivi, ou la
        // calcule. Le champ reste écrit à la sauvegarde pour les anciens réglages.

        // Format du n° de facture (dropdown) : mémorisé, défaut AMN (Année/Mois-Numéro).
        string fmt = f != null && !string.IsNullOrEmpty(f.numeroFormat) ? f.numeroFormat : "AMN";
        _numeroFormatDD.SetOptions(NumFmtLabels, NumFmtIds, fmt);

        // Texte libre / n° interne (rouge sur la facture).
        _refInterne.text = f?.refInterne ?? "";

        // Période facturée : options selon la périodicité.
        // Revue 17 (D6-04, décision du 05/11) : la période à envoyer (PeriodeAOuvrir), et non plus la dernière enregistrée, en
        // général déjà partie : « Sauvegarder et envoyer » en envoyait une correction sans le dire. Sans période facturable, la
        // mémorisée, comme avant. Une ligne du suivi ou une alerte passée garde la priorité (AppliquerLigneCiblee, ensuite).
        // Revue 17 (avenant de périodicité, lot 15) : les options sont les périodes du PLAN de l'année ouverte (FacturationSuivi.Plan) :
        // l'année d'un changement daté propose ses mois puis ses trimestres. Une période mémorisée hors de ces options (« 11 »,
        // novembre d'un ancien mensuel) ne compte plus : le menu retombait sur la première option (janvier, le 1er trimestre).
        var aOuvrir = PeriodeAOuvrir(_loc, DateTime.Today);
        int anneeSel = aOuvrir?.annee ?? (f != null && f.anneePeriode >= 1000 && f.anneePeriode <= 9998 ? f.anneePeriode : DateTime.Today.Year);
        bool memorisee = f != null && f.moisPeriode > 0 && f.moisPeriode <= FacturationSuivi.Plan(_loc, anneeSel).Count;
        int periodeSel = aOuvrir?.periode ?? (memorisee ? f.moisPeriode : DefaultPeriodeIndex(anneeSel));
        PoserPeriodes(anneeSel, periodeSel);   // avant l'année : son écouteur trouve le menu déjà rempli pour elle
        _annee.text = anneeSel.ToString();
        // Revue 17 (D7a-03) : l'acheteur suit la période (RefreshTitulaire) ; l'e-mail du destinataire est posé ici, avec lui.
        _emailEnvoi.text = !string.IsNullOrEmpty(f?.emailDest) ? f.emailDest : (_loc.emailLocataire ?? "");
        _acheteurFiche = _acheteurAuto = new[] { _nom.text, _siret.text, _adresse.text, _emailEnvoi.text };
        _acheteurCession = false;
        RefreshTitulaire();

        // Échéance : mémorisée si la facture en portait une, sinon le jour de demande
        // du loyer sur la période choisie — la même règle que le suivi, via
        // FacturationSuivi.EcheanceLoyer. Elle se recalcule ensuite à chaque
        // changement de période ou d'année, tant qu'elle n'a pas été saisie à la main.
        _autoEcheance = DefaultEcheance();
        _echeance.text = _autoEcheance;
        // Phrase de règlement : mémorisée si personnalisée, sinon défaut basé sur l'échéance.
        _autoSomme = DefaultSomme();
        // Revue 17 (06/11) : une phrase d'usine mémorisée (ancienne date) cède la place à celle de cette facture.
        _sommePhrase.text = string.IsNullOrEmpty(f?.sommePhrase) || FactureEmission.EstPhraseUsine(f.sommePhrase) ? _autoSomme : f.sommePhrase;

        // Loyer : celui de la période choisie (palier, révision, franchise), et non
        // celui de la dernière facture — il restait sinon à l'ancien montant après une
        // révision ou un changement de palier. Recalculé avec la période (RefreshLoyerDefault).
        _autoLoyer = DefaultLoyer();
        _loyer.text = _autoLoyer;
        // Revue 16c (D6-04) : la provision suit la fiche (comme le loyer suit la période) ; celle mémorisée sur la
        // dernière facture ne vaut que pour la correction d'une facture déjà partie (son montant imprimé).
        // Revue 17 (avenant de périodicité, lot 16) : celle de la fiche ramenée à la périodicité de la PÉRIODE affichée
        // (DefaultProvision : charges générales, les listes suivent), puis recalculée avec la période (RefreshProvisionDefault).
        _autoProvisions.Clear();
        _autoProvisions[""] = DefaultProvision("");
        _provision.text = _autoProvisions[""];
        ChargerProvisionsListes(f);

        // N° de facture : l'ID locataire mémorisé est repris ; le préfixe format est
        // toujours recalculé. RefreshNumero() remplit l'ID avec la séquence s'il est vide.
        _idGarde = null;   // Revue 17 (2c) : rien de mis de côté d'une ouverture précédente
        _numeroId.text = f != null && !string.IsNullOrEmpty(f.numeroId) ? f.numeroId : "";
        RefreshNumero();

        MentionTva.Charger(_mentionTva, _texteTvaDebit, f);
        ChargerTva(f);   // 02/11
        _retard.isOn = f?.ajouterRetard ?? true;
        // Ligne « montant mensuel » : seulement pour un loyer non mensuel (trim / semestre / an).
        _mensuel.isOn = f?.ajouterMensuel ?? true;

        // Pré-remplis avec le texte d'usine : l'utilisatrice doit voir la phrase
        // réellement imprimée, pas un champ vide dont il faut deviner l'effet.
        _texteMensuel.text  = FacturePdfService.Texte(f?.texteMensuel,  FacturePdfService.MensuelDefaut);
        // La phrase suit sa case : inutile de la montrer si la ligne ne s'imprime pas (RefreshMensuel : la période affichée).
        RefreshMensuel();
        // (l'e-mail du destinataire est posé avec l'acheteur, après la période : Revue 17, D7a-03)
        // Revue 17 (D5-10, lot 13) : la partie « plateforme » (case retard forcée, mode et champs de l'e-mail, ligne « Envoi ») est posée en
        // dernier, une fois le SIRET et le taux connus, par la même méthode qui suit ensuite ce qui est tapé.
        _chargement = false;
        RefreshPlateforme(chargement: true);

        RefreshMontants();
        RefreshEntetePreview();
        RefreshBoutonSauver();   // Revue 17 (D6-04) : titre et bouton (une échéance au même texte ne déclenche pas onValueChanged)
    }

    /// Provisions par liste de charges : la générale dans `_provision` (masquée si le
    /// locataire n'en relève pas), puis une ligne par liste spécifique provisionnée.
    /// Mémorisées si la facture a été enregistrée, sinon reprises de la fiche.
    void ChargerProvisionsListes(FactureInfo f)
    {
        bool avecListes = ListesCharges.Specifiques().Count > 0;
        bool generale = ListesCharges.ConcerneParListe(_loc, "");
        _provisionLabel.text = ListesCharges.LibelleProvision("");
        _provisionLabel.gameObject.SetActive(generale);
        _provision.gameObject.SetActive(generale);
        if (!generale) _provision.text = "0.00";

        _provListes.Clear();
        foreach (Transform c in _provListesBox) Destroy(c.gameObject);
        if (!avecListes) return;

        var ids = ListesCharges.Provisionnees(_loc);
        // Revue 17 (D6-07) : comme la provision générale : la fiche, sauf pour la correction d'une facture partie, qui reprend
        // les montants de SA ligne (avant : ceux de la dernière facture du type, même hors correction).
        var etat = _ligneCiblee != null ? FacturationSuivi.EtatDe(_ligneCiblee, _loc) : FacturationSuivi.Etat.AFaire;
        bool partie = etat == FacturationSuivi.Etat.Envoye || etat == FacturationSuivi.Etat.Impaye || etat == FacturationSuivi.Etat.Paye;
        var memos = !partie ? null : _ligneCiblee.provisionsConnues ? _ligneCiblee.provisionsListes : f != null && f.saved ? f.provisionsListes : null;
        if (memos != null)
            foreach (var m in memos)
                if (m.montant > 0f && !ids.Contains(m.listeId) && ListesCharges.Effective(m.listeId) != "") ids.Add(m.listeId);

        foreach (var id in ids)
        {
            UIFactory.Text(_provListesBox, ListesCharges.LibelleProvision(id), UITheme.Role.Donnee, UITheme.TexteSecondaire);
            var inp = UIFactory.Input(_provListesBox, ListesCharges.LibelleProvision(id));
            SaisieNumerique.Montant(inp);   // retour BN2 (04/11) : la virgule aussi
            // Revue 17 (lot 16) : proposée pour la période affichée (DefaultProvision), et suivie avec elle comme la générale.
            _autoProvisions[id] = DefaultProvision(id);
            inp.text = _autoProvisions[id];
            inp.onValueChanged.AddListener(_ => { RefreshMontants(); RefreshEntetePreview(); });
            _provListes.Add((id, inp));
        }
    }

    /// Revue 17 (avenant de périodicité, lot 16) : la provision proposée pour la période AFFICHÉE et une liste ("" : la générale) :
    /// celle de la fiche ramenée à la périodicité de la période (Loyers.ProvisionPeriode : après un passage au trimestriel à 300 €,
    /// mai, encore mensuel, propose 100 € ; avant, 300 €), sauf pour la correction d'une facture partie, qui garde son montant
    /// imprimé (D6-07 : la ligne de la période affichée, Corrigee, comme le taux). La dernière facture du type (ligne émise avant
    /// D6-07) ne vaut que pour une période de la même périodicité.
    string DefaultProvision(string listeId)
    {
        if (listeId == "" && !ListesCharges.ConcerneParListe(_loc, "")) return "0.00";   // pas de provision générale : champ masqué, à zéro
        var f = _loc.factureLoyer;
        if (f != null && FacturationSuivi.PeriodiciteDe(_loc, f.anneePeriode, f.moisPeriode) != PeriodiciteChoisie()) f = null;
        float fiche = Loyers.ProvisionPeriode(_loc, PeriodeAnnee(), PeriodeChoisie(), listeId);
        var corrigee = Corrigee();   // finitions de la vague 3 : un état posé à la main, sans PDF, n'en est pas une (Tva.FacturePartie) : provision de la période
        float v;
        bool inconnue = corrigee != null && !corrigee.provisionsConnues;   // décision du 15/11 : émise avant D6-07
        if (listeId == "")
        {
            float? deduite = null;
            if (inconnue)
                deduite = ProvisionDeduite(corrigee, Loyers.MontantPeriode(_loc, PeriodeAnnee(), PeriodeChoisie()),
                    ListesCharges.Specifiques().Sum(l => Loyers.ProvisionPeriode(_loc, PeriodeAnnee(), PeriodeChoisie(), l.id)));
            v = ProvisionProposee(f, fiche, corrigee, deduite);
        }
        else
        {
            // Décision du 15/11 : sans provision connue sur la facture, celle de la fiche (pas celle mémorisée d'une autre période).
            var memos = corrigee == null || inconnue ? null : corrigee.provisionsListes;
            var memo = memos?.Find(m => m.listeId == listeId);
            v = memo != null ? memo.montant : fiche;
        }
        return v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// Revue 17 (avenant de périodicité, lot 16) : même règle que le loyer et l'échéance : la provision (générale et par liste) suit
    /// la période tant qu'elle n'a pas été retouchée à la main. Avant, elle était lue une fois à l'ouverture : un mois de l'ancienne
    /// périodicité recevait la provision du trimestre, et la facture neuve d'une autre période gardait celle de la ligne cliquée.
    void RefreshProvisionDefault()
    {
        if (_loc == null || _provision == null || _chargement) return;   // au chargement : posée par LoadIntoUI, une fois la période connue
        var champs = new List<(string id, TMP_InputField inp)> { ("", _provision) };
        champs.AddRange(_provListes);
        foreach (var (id, inp) in champs)
        {
            if (inp == null) continue;
            string def = DefaultProvision(id);
            if (_autoProvisions.TryGetValue(id, out string auto) && inp.text == auto) inp.text = def;   // déclenche RefreshMontants
            _autoProvisions[id] = def;
        }
    }

    /// Total des provisions saisies (générale + listes) : ce qui entre dans le HT.
    /// Revue 17 (D5-13 ← D5-N8, lot 16) : chaque ligne arrondie au centime, une à une, comme le XML de la plateforme
    /// (FactureEn16931) : la somme imprimée est la somme des lignes (avant : 10,005 + 10,005 = 20,01 € au PDF, 20,02 € au XML).
    float ProvisionSaisie() => Cents(ParseF(_provision.text)) + _provListes.Sum(x => Cents(ParseF(x.inp.text)));

    /// Lignes imprimées : une par liste. Null tant qu'aucune liste spécifique
    /// n'existe — la facture garde alors sa ligne unique « Provision pour charges ».
    List<KeyValuePair<string, float>> LignesProvision()
    {
        if (ListesCharges.Specifiques().Count == 0) return null;
        var res = new List<KeyValuePair<string, float>>
            { new KeyValuePair<string, float>(ListesCharges.LibelleProvision(""), Cents(ParseF(_provision.text))) };
        foreach (var (id, inp) in _provListes)
            res.Add(new KeyValuePair<string, float>(ListesCharges.LibelleProvision(id), Cents(ParseF(inp.text))));
        return res;
    }

    void RefreshMontants()
    {
        // Revue 16c (D5-16) : au centime ligne par ligne, et TTC = HT + TVA arrondie : le PDF et le XML du Factur-X disent pareil.
        float loyer = Cents(ParseF(_loyer.text)), prov = Cents(ProvisionSaisie());
        float taux = TauxFacture();
        float totalHT = loyer + prov, tva = Tva.Montant(totalHT, taux), ttc = totalHT + tva;
        _mTotalHT.text = $"{N2(totalHT)} €";
        _mTVA.text     = $"{N2(tva)} €";
        _mTTC.text     = $"{N2(ttc)} €";
        // BK (02/11) : sans TVA, une seule ligne « Total » ; sinon la ligne TVA porte le taux de la facture.
        UIFactory.LigneMontant(_mTotalHT, taux > 0f);
        UIFactory.LigneMontant(_mTVA, taux > 0f, Tva.Libelle(taux));
        UIFactory.LigneMontant(_mTTC, true, taux > 0f ? "Total TTC" : "Total");
        RefreshSommeDefault();   // Revue 17 (D6-11, coutures) : le total vient de changer : la phrase de règlement proposée suit son signe
    }

    void RefreshEntetePreview()
    {
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        _entetePreview.text = ent == null || string.IsNullOrEmpty(ent.texte)
            ? "—" : FactureVarResolver.Resolve(ent.texte, _loc, _bat, BuildContext());
    }

    FactureContext BuildContext()
    {
        float loyer = ParseF(_loyer.text), prov = ProvisionSaisie();
        float totalHT = loyer + prov;
        DateTime d = TryDate(_date.text, out var dd) ? dd : DateTime.Today;
        return new FactureContext
        {
            date = d,
            periode = PeriodeLibelle(),
            numero = NumeroDocument(),
            loyerHT = totalHT,
            tva = Tva.Montant(totalHT, TauxFacture()),
            ttc = Tva.Ttc(totalHT, TauxFacture()),
            // Revue 17 (D6-08) : {echeance} remplie, comme à l'envoi au lancement (avant : vide dans l'e-mail et le PDF).
            echeance = TryDate(_echeance != null ? _echeance.text : "", out var e) ? e.ToString("dd/MM/yyyy") : "",
            periodicite = PeriodiciteChoisie().ToString(),   // Revue 17 (lot 15) : {loc.periodicite} imprime celle de la période facturée
        };
    }

    // ── Période facturée ───────────────────────────────────────────────────────

    // Options du menu « Période » : les périodes de l'année.
    /// Revue 17 (avenant de périodicité, lot 15) : celles du PLAN de l'année (FacturationSuivi.Plan), l'identifiant étant le rang
    /// dans l'année, le « n » de la clé « loyer-{année}-P{n} ». Avant : les numéros de la périodicité de la fiche, quelle que soit
    /// l'année : après un passage du mensuel au trimestriel au 1er juillet, « 3e Trimestre » désignait P3, la facture de mars.
    /// Sans avenant de périodicité : les mêmes options qu'avant (« 1er Trimestre »…, les douze mois, « Année complète »).
    public static (List<string> labels, List<string> ids) PeriodeOptions(Locataire loc, int annee)
    {
        var plan = FacturationSuivi.Plan(loc, annee);
        return (plan.Select(p => Libelle(p, false)).ToList(), Enumerable.Range(1, plan.Count).Select(i => i.ToString()).ToList());
    }

    // Libellé d'une période à la façon du formulaire : « 3e Trimestre », « Mars », « Année complète » dans le menu ; avec l'année
    // sur la facture (« 3e Trimestre 2026 », « Année 2026 »). Coupée par un avenant de périodicité : « 2e Trimestre 2026 (mai à juin) ».
    static string Libelle(FacturationSuivi.PeriodeLoyer p, bool avecAnnee)
    {
        string annee = avecAnnee ? " " + p.debutNaturel.Year : "", s;
        switch (p.periodicite)
        {
            case Periodicite.trimestriel: s = $"{Ord(p.numero)} Trimestre{annee}"; break;
            case Periodicite.BiAnnuel:    s = $"{Ord(p.numero)} Semestre{annee}"; break;
            case Periodicite.Annuel:      s = avecAnnee ? $"Année{annee}" : "Année complète"; break;
            default:                      s = MoisNoms[Mathf.Clamp(p.numero, 1, 12) - 1] + annee; break;
        }
        return p.Coupee ? $"{s} ({FacturationSuivi.MoisFactures(p)})" : s;
    }

    /// Revue 17 (avenant de périodicité, lot 15) : le libellé imprimé d'une période (« Loyer … », objet, {periode}) : celui de la
    /// période du plan, dans SA périodicité (« Mars 2026 » pour P3 et « 3e Trimestre 2026 » pour P7 l'année du changement).
    public static string LibellePeriode(Locataire loc, int annee, int periode) => Libelle(FacturationSuivi.Periode(loc, annee, periode), true);

    /// Revue 17 (avenant de périodicité, lot 15) : le nombre de mois d'une période, diviseur de la ligne « montant mensuel » :
    /// 12 ÷ le nombre de périodes de SA périodicité, comme avant ; coupée par un avenant de périodicité, ses mois facturés.
    public static int MoisFactures(Locataire loc, int annee, int periode)
    {
        var p = FacturationSuivi.Periode(loc, annee, periode);
        return p.Coupee ? p.Mois : Mathf.Max(1, 12 / LoyerSummaryUI.NbPeriodes(p.periodicite));
    }

    /// Revue 17 (avenant de périodicité, lot 15) : l'année change : la période gardée est celle qui contient le mois de début de
    /// la période choisie (mars 2026 → 1er trimestre 2027 ; sans avenant de périodicité : le même numéro, comme avant).
    /// Revue 17 (avenant de périodicité, finitions) : d'abord le même rang, quand la période de ce rang dans la nouvelle année a la
    /// même périodicité et commence le même mois : sans avenant de périodicité, le numéro ne change donc JAMAIS. La recherche par
    /// mois seule faisait passer P3 ou P4 à P2 sur une vieille fiche aux mois cochés incomplets (trimestriel, février et mai seuls :
    /// P2, P3 et P4 commencent toutes en mai).
    public static int PeriodeGardee(Locataire loc, int anneeAvant, int periode, int annee)
    {
        var avant = FacturationSuivi.Periode(loc, anneeAvant, periode);
        if (periode >= 1 && periode <= FacturationSuivi.Plan(loc, annee).Count)
        {
            var meme = FacturationSuivi.Periode(loc, annee, periode);
            if (meme.periodicite == avant.periodicite && meme.debut.Month == avant.debut.Month) return periode;
        }
        return Mathf.Max(1, FacturationSuivi.PeriodeIndex(loc, annee, avant.debut.Month));
    }

    // Remplit le menu avec le plan de l'année, sur la période voulue.
    void PoserPeriodes(int annee, int periode)
    {
        var (plabels, pids) = PeriodeOptions(_loc, annee);
        _periodeDD.SetOptions(plabels, pids, periode.ToString());
        _anneeOptions = annee;
    }

    /// Revue 17 (avenant de périodicité, lot 15) : l'année saisie change : le menu propose les périodes de SON plan. Seulement pour
    /// une année complète (quatre chiffres) : reconstruit à chaque frappe (« 2 », « 20 », « 202 »), il perdait la période choisie.
    void RefreshPeriodes()
    {
        if (_loc == null || _periodeDD == null || _anneeOptions <= 0) return;
        string t = (_annee.text ?? "").Trim();
        if (t.Length != 4 || !int.TryParse(t, out int annee) || annee < 1000 || annee > 9998 || annee == _anneeOptions) return;
        PoserPeriodes(annee, PeriodeGardee(_loc, _anneeOptions, PeriodeChoisie(), annee));
    }

    /// Revue 17 (avenant de périodicité, lot 15) : la périodicité de la PÉRIODE affichée (celle de sa tranche dans le plan), pas
    /// celle de la fiche : c'est elle que l'émission compare (FactureEmission.Preparer, D4-04), garde sur la ligne et imprime.
    Periodicite PeriodiciteChoisie() => FacturationSuivi.PeriodiciteDe(_loc, PeriodeAnnee(), PeriodeChoisie());

    /// Revue 17 (avenant de périodicité, lot 15) : la case « montant mensuel » et sa phrase suivent la PÉRIODE affichée (avant : la
    /// périodicité de la fiche, lue une fois à l'ouverture) : cachées pour un mois, montrées pour un trimestre.
    void RefreshMensuel()
    {
        if (_loc == null || _mensuel == null || _texteMensuel == null) return;
        bool plusieursMois = MoisFactures(_loc, PeriodeAnnee(), PeriodeChoisie()) > 1;
        _mensuel.gameObject.SetActive(plusieursMois);
        _texteMensuel.gameObject.SetActive(plusieursMois);
        // Revue 17 (D6-24, coutures) : l'aide de la phrase suit la même règle (avant : affichée seule sous le taux de TVA pour un mois).
        if (_aideMensuel != null) _aideMensuel.gameObject.SetActive(plusieursMois);
    }

    // Période proposée par défaut = la période EN COURS (d'après la date du jour),
    // pas systématiquement la 1re. Évite de facturer le « 1er trimestre » par erreur.
    /// Période en cours = la dernière dont le mois d'échéance est déjà atteint.
    /// Suit les mois de facturation cochés : sur un trimestriel facturé en
    /// février/mai/août/novembre, le calcul « (mois − 1) / 3 + 1 » proposait la mauvaise période.
    /// Revue 17 (avenant de périodicité, lot 15) : parmi les périodes du plan de l'année ouverte (leur mois de début).
    int DefaultPeriodeIndex(int annee)
    {
        int m = DateTime.Today.Month;
        var plan = FacturationSuivi.Plan(_loc, annee);
        int enCours = 1;
        for (int per = 1; per <= plan.Count; per++)
            if (plan[per - 1].debut.Month <= m) enCours = per;
        return enCours;
    }

    /// Revue 17 (D6-04, décision du 05/11) : la période que « Facturer le loyer » ouvre sans ligne du suivi (bouton de la fiche,
    /// premiers pas) : à partir de celle en cours, la première dont le loyer n'est pas parti (une facture préparée, en attente
    /// d'envoi, est proposée), cette année puis la suivante. Mêmes règles que l'alerte du loyer : période facturable (ni hors bail
    /// ni en franchise), après la reprise, pas déjà traitée. Rien à envoyer (locataire parti, tout envoyé) : la dernière période
    /// facturable jusqu'à aujourd'hui ; aucune : null (la période mémorisée). Le 16/11, novembre pas parti : novembre, même si
    /// l'alerte affichée au-dessus du bouton parle déjà de décembre (elle suit la prochaine échéance).
    public static (int annee, int periode)? PeriodeAOuvrir(Locataire loc, DateTime auj)
    {
        if (loc == null) return null;
        auj = auj.Date;
        bool avecReprise = FacturationSuivi.TryEcheance(loc.repriseFacturationISO, out var reprise);
        (int, int)? derniere = null;
        // Revue 17 (avenant de périodicité, lot 14) : les périodes du PLAN de chaque année (FacturationSuivi.Plan), pas les n numéros
        // de la périodicité actuelle : après un passage du mensuel au trimestriel au 1er juillet, le 3e trimestre (P7) est proposé.
        for (int y = auj.Year - 1; y <= auj.Year + 1; y++)   // l'an passé : la période à cheval (novembre à janvier), un départ
            for (int p = 1, n = FacturationSuivi.Plan(loc, y).Count; p <= n; p++)
            {
                if (!Loyers.PeriodeFacturable(loc, y, p)
                    || avecReprise && FacturationSuivi.EcheanceLoyer(loc, y, p) <= reprise.Date) continue;
                FacturationSuivi.PeriodeBornes(loc, y, p, out var debut, out var fin);
                if (debut <= auj) derniere = (y, p);
                if (fin >= auj && !FacturationSuivi.DejaTraite(loc, $"loyer-{y}-P{p}")) return (y, p);
            }
        return derniere;
    }

    int PeriodeAnnee()
    {
        // Revue 17 (lot 15) : jusqu'à 9998, la dernière année dont les périodes se calculent (le libellé lit maintenant le plan).
        if (int.TryParse((_annee != null ? _annee.text : "").Trim(), out int y) && y > 0 && y <= 9998) return y;
        return DateTime.Today.Year;
    }

    // Libellé de la période, ex. « 1er Trimestre 2023 », « Janvier 2023 », « Année 2023 ».
    // Revue 17 (avenant de périodicité, lot 15) : dans la périodicité de la PÉRIODE (LibellePeriode), pas celle de la fiche.
    string PeriodeLibelle() => LibellePeriode(_loc, PeriodeAnnee(), PeriodeChoisie());

    static string Ord(int n) => n == 1 ? "1er" : $"{n}e";

    // Phrase de règlement par défaut, dérivée de l'échéance. Revue 17 (D6-11, coutures) : elle suit le signe du total, comme dans la
    // régularisation et le dépôt : l'écran ne propose plus « SOMME À NOUS RÉGLER » pour un loyer qui sera un avoir.
    string DefaultSomme()
    {
        if (_loyer != null && _provision != null && Cents(ParseF(_loyer.text)) + ProvisionSaisie() < -0.005f) return "SOMME QUI VOUS SERA REMBOURSÉE";

        DateTime ech = TryDate(_echeance != null ? _echeance.text : "", out var ed) ? ed : DateTime.Today;
        return "SOMME À NOUS RÉGLER LE " + ech.ToString("d MMMM yyyy", FacturePdfService.FrCulture);
    }

    // Échéance proposée : le jour où le loyer est demandé, sur la période facturée.
    // Une seule règle, partagée avec le suivi (FacturationSuivi.EcheanceLoyer) : le
    // panneau proposait « date de facture + 30 jours », une date que le suivi ne
    // reconnaissait pas — la facture et sa ligne de suivi n'avaient pas la même échéance.
    string DefaultEcheance()
    {
        int periode = 1;
        if (_periodeDD != null) int.TryParse(_periodeDD.SelectedId, out periode);
        if (periode < 1) periode = 1;
        int annee = PeriodeAnnee();

        // Une facture RÉELLEMENT PARTIE porte sa propre échéance : celle imprimée sur
        // le PDF qu'a reçu le locataire. On ne la recalcule pas.
        //
        // Une facture seulement préparée, elle, se recalcule : rien n'a été imprimé
        // pour personne. C'est ce qui rattrape les lignes polluées par l'échéance
        // mémorisée — un « Loyer Mars 2026 » qu'on retrouvait à échéance du 17 octobre.
        var ligne = _loc?.facturesEtat?.FirstOrDefault(x => x.key == $"loyer-{annee}-P{periode}");
        bool partie = ligne != null && (ligne.statut == "Envoye" || ligne.statut == "Impaye"
                                     || ligne.statut == "Paye");
        if (partie && FacturationSuivi.TryEcheance(ligne.echeanceISO, out var dech))
            return dech.ToString("dd/MM/yyyy");

        return FacturationSuivi.EcheanceLoyer(_loc, annee, periode).ToString("dd/MM/yyyy");
    }

    // L'échéance suit la période tant qu'elle n'a pas été saisie à la main — même
    // règle que la phrase de règlement, qu'elle alimente à son tour.
    void RefreshEcheanceDefault()
    {
        if (_loc == null || _echeance == null) return;
        string def = DefaultEcheance();
        if (_echeance.text == _autoEcheance) _echeance.text = def;   // déclenche RefreshSommeDefault
        _autoEcheance = def;
        RefreshBoutonSauver();
        RefreshLoyerDefault();
        RefreshProvisionDefault();   // Revue 17 (lot 16) : la provision proposée suit la période, comme le loyer
        RefreshTitulaire();   // Revue 17 (D7a-03)
        RefreshMensuel();     // Revue 17 (lot 15) : la ligne « montant mensuel » suit la période affichée
    }

    // Revue 17 (D7a-03) : l'acheteur d'office suit la période choisie (Cessions.TitulaireDe) : le cessionnaire d'une cession
    // prévue qui commence au plus tard le premier jour de la période (avant, le loyer de décembre préparé en novembre partait
    // au cédant), le cédant pour une période d'avant une cession faite. Un champ retouché à la main reste tel quel.
    // Retour BN12 (04/11) : le SIRET de l'acheteur pour la plateforme : un cessionnaire sans SIRET n'en a pas (avant, le champ
    // vide retombait sur celui de la fiche, donc du cédant, dans la facture électronique).
    string SiretSaisi() => SiretPourPlateforme(_acheteurCession, _siret.text);
    public static string SiretPourPlateforme(bool cession, string saisi) => cession && string.IsNullOrWhiteSpace(saisi) ? "(aucun)" : saisi;

    // La ligne « Envoi : … » suit l'acheteur affiché : son SIRET décide de la plateforme.
    void RefreshModeEnvoi()
    {
        if (_modeInfo == null || _loc == null || _siret == null) return;
        // Revue 17 (D5-08, lot 10) : au taux du document (une correction gardée taxée d'un bail devenu exonéré repart par la plateforme).
        // Coutures (reste du lot 11) : au jour où le loyer partira (DepartSaisi), le jour que juge la garde de l'émission. Avant : au jour
        // même ; en août 2027, un loyer à envoyer en septembre annonçait « e-mail » et l'émission le refusait.
        _modeInfo.text = FactureElectronique.LibelleEnvoi(_loc, TauxFacture(), SiretSaisi(), FactureElectronique.NotificationSeule, DepartSaisi());   // partagé (02/11)
        _modeInfo.color = FactureElectronique.SansSiret(_loc, TauxFacture(), SiretSaisi()) ? UITheme.Alerte : UITheme.TexteSecondaire;
    }

    /// Revue 17 (D5-10, lot 13) : la partie « plateforme » du formulaire suit le SIRET et le taux tapés, aussitôt : case retard (forcée pour
    /// la plateforme ou un professionnel, D6-03), mode de l'e-mail (facture, « copie », notification) et ses champs, ligne « Envoi ». Un texte
    /// retouché reste dans la paire de son mode (EmailService.Garder) avant que les champs passent au texte du nouveau mode. Avant : décidés
    /// une fois au chargement (un SIRET tapé faisait partir la facture par la plateforme avec l'e-mail de facture, ligne « Envoi » orange).
    /// `chargement` : fin de LoadIntoUI, les champs sont posés quoi qu'il en soit, sans rien garder (le texte à l'écran est encore l'ancien).
    void RefreshPlateforme(bool chargement = false)
    {
        if (_loc == null || _retard == null || (_chargement && !chargement)) return;
        var f = _loc.factureLoyer;
        bool plateforme = FactureElectronique.Concerne(_loc, TauxFacture(), SiretSaisi());   // Revue 17 (D5-08, lot 10) : au taux du document
        bool obligatoire = plateforme || FactureElectronique.Professionnel(_loc, SiretSaisi());   // Revue 17 (D6-03) : professionnel
        if (obligatoire) _retard.isOn = true;   // plateforme : phrase de retard obligatoire (note PMD, règle BR-FR-05 ; décision du 30/10)
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

    void RefreshTitulaire()
    {
        if (_loc == null || _acheteurFiche == null) return;
        FacturationSuivi.PeriodeBornes(_loc, PeriodeAnnee(), PeriodeChoisie(), out var debut, out _);
        var t = Cessions.TitulaireDe(_loc, debut);
        // Revue 17 (D4-01, lot 6) : la correction d'une facture d'un autre titulaire reprend l'adresse gardée sur sa ligne (compta@…),
        // pas l'e-mail de la fiche du cédant ; une facture du titulaire de la fiche suit l'adresse actuelle (décision du 05/11).
        if (t != null && Cessions.AcheteurDe(_loc, _loc.facturesEtat?.Find(x => x.key == CleLoyer())) is string[] a && a[0] == t[0]) t = a;
        var voulu = t ?? _acheteurFiche;
        var champs = new[] { _nom, _siret, _adresse, _emailEnvoi };
        for (int i = 0; i < champs.Length; i++)
            if (champs[i] != null && champs[i].text == (_acheteurAuto[i] ?? "")) champs[i].text = voulu[i] ?? "";
        if (_noteCession != null)
        {
            _noteCession.text = t != null ? $"Cession du bail : cette période est facturée à « {t[0]} », titulaire du bail à son début." : "";
            _noteCession.gameObject.SetActive(t != null);
        }
        _acheteurAuto = voulu;
        _acheteurCession = t != null;
        RefreshPlateforme();   // le SIRET affiché décide de la plateforme (lot 13 : case retard et e-mail aussi, une fois _acheteurCession posé)
    }

    // Loyer HT proposé pour la période et l'année choisies (voir Loyers.MontantPeriode).
    string DefaultLoyer()
        => Loyers.MontantPeriode(_loc, PeriodeAnnee(), PeriodeChoisie()).ToString("0.00", CultureInfo.InvariantCulture);

    int PeriodeChoisie()
    {
        int periode = 1;
        if (_periodeDD != null) int.TryParse(_periodeDD.SelectedId, out periode);
        return periode < 1 ? 1 : periode;
    }

    /// Revue 17 (2c, décision du 06/11) : la ligne du suivi de la période affichée, la même pour la ligne « N° de facture », le
    /// titre et l'émission : le numéro affiché est celui qui part.
    string CleLoyer() => $"loyer-{PeriodeAnnee()}-P{PeriodeChoisie()}";

    // Ligne du loyer : « Loyer — franchise » quand la franchise l'annule (seules les
    // provisions sont dues), sinon null = libellé par défaut du gabarit.
    string LigneLoyer(float loyer)
        => loyer == 0f && Loyers.EnFranchise(_loc, PeriodeAnnee(), PeriodeChoisie()) ? "Loyer — franchise" : null;

    // Même règle que l'échéance : le loyer suit la période tant qu'il n'a pas été saisi à la main.
    void RefreshLoyerDefault()
    {
        if (_loc == null || _loyer == null) return;
        string def = DefaultLoyer();
        if (_loyer.text == _autoLoyer) _loyer.text = def;   // déclenche RefreshMontants
        _autoLoyer = def;
    }

    // Date d'envoi de la facture en cours de saisie, d'après l'échéance affichée.
    DateTime? DateEnvoiSaisie()
        => TryDate(_echeance != null ? _echeance.text : "", out var ech)
            ? FacturationSuivi.DateEnvoi("Loyer", ech.ToString("yyyy-MM-dd"), _loc)
            : null;

    /// Coutures (reste du lot 11) : le jour où le loyer affiché partira, celui que juge la garde de l'émission (RefusObligation) : sa date
    /// d'envoi si elle est encore à venir (envoi différé), sinon aujourd'hui ; une correction part tout de suite (D4-03).
    DateTime DepartSaisi()
    {
        var envoi = DateEnvoiSaisie();
        return envoi > DateTime.Today && !FactureEmission.SeraUneCorrection(_loc, CleLoyer(), PeriodiciteChoisie().ToString()) ? envoi.Value : DateTime.Today;
    }

    // Le bouton dit ce qu'il va faire. Il promettait un envoi immédiat même à un mois
    // de la date d'envoi — on cliquait alors « et envoyer » en croyant préparer, et le
    // locataire recevait sa facture bien trop tôt, sans retour possible.
    // Revue 17 (D6-04, décision du 05/11) : une période déjà partie s'annonce, quel que soit le chemin (bouton de la fiche, alerte,
    // ligne du suivi, période choisie à la main) : titre « (correction) » et bouton « Corriger et envoyer ». La règle est celle de
    // FactureEmission.Preparer (SeraUneCorrection) : avant, « (correction) » ne venait que d'une ligne du suivi, et le bouton
    // suivait le statut seul (une période forcée « Payé » sans PDF s'annonçait partie, alors que Preparer en émet une neuve).
    // Le titre garde le nom du locataire (D10-14) ; il est posé avant la garde du bouton.
    void RefreshBoutonSauver()
    {
        // Revue 17 (2c) : avec la périodicité, comme la ligne « N° de facture » (RefreshNumero) : un refus D4-04 n'est pas annoncé « (correction) ».
        // Lot 15 (avenant de périodicité) : celle de la période affichée (mars, émis en mensuel, se corrige après un passage daté au trimestriel).
        bool correction = FactureEmission.SeraUneCorrection(_loc, CleLoyer(), PeriodiciteChoisie().ToString());
        if (_titre != null) _titre.text = $"Information Facture — Loyer{(correction ? " (correction)" : "")} · {_loc?.Name ?? ""}";
        if (_btnSauver == null) return;
        var t = _btnSauver.GetComponentInChildren<TMP_Text>(true);
        if (t == null) return;
        var envoi = DateEnvoiSaisie();
        // Une facture déjà partie se corrige et repart tout de suite (D4-03) : pas d'« envoi du … » à annoncer.
        bool enAvance = envoi.HasValue && DateTime.Today < envoi.Value && !correction;
        t.text = SansEnvoi || BatimentExemple.Est(_bat) ? "Émettre (sans envoyer)"
            : enAvance ? $"Enregistrer pour l'envoi du {envoi.Value:dd/MM}"
            : correction ? "Corriger et envoyer"
            : "Sauvegarder et envoyer";
    }

    // La phrase suit l'échéance tant qu'elle n'a pas été personnalisée.
    void RefreshSommeDefault()
    {
        string def = DefaultSomme();
        if (_sommePhrase != null && _sommePhrase.text == _autoSomme) _sommePhrase.text = def;
        _autoSomme = def;
    }

    // Rafraîchit le préfixe (format + date) et pré-remplit l'ID avec la séquence
    // du locataire tant que l'utilisatrice n'a rien saisi.
    // Revue 17 (2c, décision du 06/11) : la période a déjà son numéro (facture préparée, remise en automatique ou partie) : la
    // ligne montre, grisé, celui que l'émission imprimera (« 2026/11003 », « 2026/11003 corrigée(1) »), et l'ID n'est plus modifiable.
    // Un numéro décidé par une émission finie ou abandonnée ne vaut plus : l'aperçu et l'entête suivent la ligne.
    void RefreshNumero()
    {
        if (_loc == null) return;
        _numeroEmis = null;
        DateTime d = TryDate(_date != null ? _date.text : "", out var dd) ? dd : DateTime.Today;
        string fmt = _numeroFormatDD != null ? _numeroFormatDD.SelectedId : "AMN";
        string repris = FactureEmission.NumeroRepris(_loc, CleLoyer(), PeriodiciteChoisie().ToString());
        if (_numeroPrefixe != null && _numeroId != null)
            FactureEmission.AfficherNumero(_numeroAide, _numeroPrefixe, _numeroId, repris, NumeroPrefixe(fmt, d), _loc.factureSeq,
                                           "N° de facture — repris : cette période a déjà ce numéro", ref _idGarde);
        RefreshEntetePreview();
    }

    // Préfixe « format possible » selon le format et la date (l'ID vient après).
    static string NumeroPrefixe(string fmt, DateTime d) => FactureNumerotation.Prefixe(fmt, d);

    // N° complet = préfixe format + ID locataire saisi. Revue 17 (2c) : numéro repris, la boîte porte tout le numéro et l'ID est vide.
    string ComposedNumero()
    {
        string pfx = _numeroPrefixe != null ? _numeroPrefixe.text : "";
        string id = _numeroId != null ? (_numeroId.text ?? "").Trim() : "";
        return pfx + id;
    }

    // ── Sauvegarde (mémorisé sur le locataire) ─────────────────────────────────

    void SaveFromUI()
    {
        if (_loc == null) return;
        var f = _loc.factureLoyer ?? new FactureInfo();
        // Destinataire mémorisé SUR LA FACTURE (peut différer du locataire — on n'écrase pas la fiche).
        // Revue 17 (D7a-03) : sauf l'acheteur venu d'une cession (celui d'une autre période) : il ne devient pas celui du type.
        if (!_acheteurCession) { f.destNom = _nom.text; f.destAdresse = _adresse.text; f.destSiret = _siret.text; }
        f.ribId = _ribDD?.SelectedId;
        f.enteteId = _enteteDD?.SelectedId;
        f.dateISO = TryDate(_date.text, out var d) ? d.ToString("yyyy-MM-dd") : "";
        f.dateEcheanceISO = TryDate(_echeance.text, out var de) ? de.ToString("yyyy-MM-dd") : "";
        f.sommePhrase = FactureEmission.PhraseAMemoriser(_sommePhrase.text);   // Revue 17 (06/11) : la phrase d'usine suit l'échéance
        f.numeroFormat = _numeroFormatDD?.SelectedId ?? "AMN";
        f.numeroId = (_numeroId.text ?? "").Trim();
        f.numero = ComposedNumero();
        if (Corrigee() == null) MentionTva.Enregistrer(_mentionTva, _texteTvaDebit, f);   // décision du 10/11 : la mention choisie pendant une correction ne vaut que pour elle, comme le taux
        if (Corrigee() == null) f.tauxTva = TauxAMemoriser();   // 02/11 ; Revue 17 (D5-08, lot 10) : le taux tapé pendant une correction ne vaut que pour elle
        f.ajouterRetard = _retard.isOn;
        f.ajouterMensuel = _mensuel.isOn;
        f.texteMensuel = _texteMensuel.text;
        if (!_acheteurCession) f.emailDest = _emailEnvoi.text;   // Revue 17 (D7a-03)
        else
        {
            // Revue 17 (D4-01, lot 6 ; D4-N3) : l'acheteur d'une autre période : ce qui est tapé (nom, SIRET, adresse, e-mail) va dans
            // la cession prévue, pas dans le réglage du type. Avant, l'e-mail tapé pour le cessionnaire n'était gardé nulle part :
            // l'envoi différé partait sans adresse. Pour un cédant (cession faite), rien n'est reporté : l'adresse tapée reste sur la
            // ligne (Finaliser, PoserAcheteur) et l'historique n'est pas réécrit (relecture du lot 6).
            FacturationSuivi.PeriodeBornes(_loc, PeriodeAnnee(), PeriodeChoisie(), out var debut, out _);
            Cessions.Reporter(_loc, debut, _nom.text, _siret.text, _adresse.text, _emailEnvoi.text);
        }
        // Gardé sur la facture seulement s'il diffère du modèle des Réglages, dans la paire du mode affiché :
        // e-mail de facture, « copie » (par la plateforme, avant la bascule) ou notification (après). Revue 17 (D5-10, lot 13) : règle partagée.
        EmailService.Garder(f, _modeCopie || _modeNotif, _modeNotif, _emailObjet.text, _emailCorps.text);
        f.loyerMontant = ParseF(_loyer.text);
        f.provisionMontant = ParseF(_provision.text);
        f.provisionsListes = _provListes.Select(x => new MontantListe { listeId = x.id, montant = ParseF(x.inp.text) }).ToList();
        f.refInterne = _refInterne.text;
        int.TryParse(_periodeDD?.SelectedId, out int pidx);
        f.moisPeriode = pidx > 0 ? pidx : 1;
        f.anneePeriode = PeriodeAnnee();
        f.objet = $"Loyer {PeriodeLibelle()}";
        f.saved = true;
        _loc.factureLoyer = f;

        _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        UndoToast.Instance?.ShowInfo("Informations de facture enregistrées");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────


    /// TVA de cette facture (02/11) : le taux tapé dans le formulaire, à défaut celui proposé (Tva.TauxPropose : la facture corrigée,
    /// sinon le bail ou l'entreprise). Revue 17 (D5-08, lot 10) : un 0 tapé compte (une facture taxée peut se corriger exonérée), et
    /// plus de 0 forcé par le bail du jour (la correction d'une facture taxée d'un bail devenu exonéré partait sans TVA).
    float TauxFacture()
        => _tauxTva != null && !string.IsNullOrWhiteSpace(_tauxTva.text) && SaisieNumerique.TryParse(_tauxTva.text, out float t) && t >= 0f
           ? t : Tva.TauxPropose(_loc, null, Corrigee());

    /// Le taux tapé n'est mémorisé que s'il diffère de celui du bail ou de l'entreprise (sinon il suivrait un changement de réglage).
    float TauxAMemoriser() => Tva.Soumis(_loc) && SaisieNumerique.TryParse(_tauxTva.text, out float t) && t > 0f
                              && Mathf.Abs(t - Tva.Taux(_loc)) > 0.001f ? t : 0f;

    /// Revue 17 (D5-08, lot 10) : la facture PARTIE que le formulaire corrige (la ligne de la période affichée, comme le numéro repris :
    /// CleLoyer, donc suivie à chaque changement de période), sinon null : son taux imprimé est proposé, et le taux tapé ne vaut que
    /// pour elle (SaveFromUI ne le mémorise pas sur la facture du type).
    FactureEtat Corrigee()
    {
        if (_loc?.facturesEtat == null) return null;
        string cle = CleLoyer();
        var ligne = _loc.facturesEtat.Find(x => x.key == cle);
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

    /// Revue 17 (D5-08, lot 10) : la facture visée vient de changer (période, année) : le taux proposé la suit, tant qu'il n'a pas été
    /// saisi à la main (comme le loyer et l'échéance). Sinon, la facture NEUVE d'une autre période partait au taux de la ligne cliquée.
    void RefreshTauxDefault() { if (_loc != null && _tauxTva != null && _tauxTva.text == _autoTaux) ChargerTva(_loc.factureLoyer); }

    TMP_InputField Labeled(VerticalLayoutGroup body, string label)
    {
        UIFactory.Text(body.transform, label, UITheme.Role.Donnee, UITheme.TexteSecondaire);
        return UIFactory.Input(body.transform, label);
    }

    void ZoomBtn(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float w = 44)
    {
        var b = UIFactory.Button(parent, label, UITheme.Carte, UITheme.TextePrincipal, 36, UITheme.Role.Action, false);
        UIFactory.Border(b.gameObject);
        UIFactory.LE(b.gameObject, prefW: w, flexW: 0, minH: 36);
        b.onClick.AddListener(onClick);
    }

    TMP_Text MontRow(VerticalLayoutGroup body, string label)
    {
        var h = UIFactory.HBox(body.transform, 8, false, "Row");
        UIFactory.LE(h.gameObject, minH: 24);
        var l = UIFactory.Text(h.transform, label, UITheme.Role.Donnee, UITheme.TexteSecondaire);
        UIFactory.LE(l.gameObject, flexW: 1);
        return UIFactory.Text(h.transform, "—", UITheme.Role.Donnee, UITheme.TextePrincipal, true, TextAlignmentOptions.Right);
    }

    // Passe par SaisieNumerique : « . » et « , » y sont interchangeables et les
    // espaces de milliers acceptes (cette copie locale ne gerait que la virgule).
    static float ParseF(string s) => SaisieNumerique.Parse(s);

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static bool TryDate(string s, out DateTime d) => SaisieDate.TryParse(s, out d);
}
