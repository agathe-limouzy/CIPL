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
/// « Générer la facture » = mémorise + aperçu (le PDF au visuel réel + l'envoi Pennylane/
/// email viennent ensuite — RIEN n'est émis pour l'instant).
public class FactureLoyerPanel : MonoBehaviour
{
    public static FactureLoyerPanel Instance { get; private set; }

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

    TMP_Text _titre, _entetePreview, _modeInfo, _mTotalHT, _mTVA, _mTTC, _numeroPrefixe;
    TMP_InputField _nom, _adresse, _siret, _date, _echeance, _numeroId, _annee, _refInterne, _sommePhrase, _loyer, _provision, _emailEnvoi;
    TMP_InputField _texteTvaDebit, _texteMensuel;
    TMP_Text _provisionLabel;                              // provision générale
    Transform _provListesBox;                              // une provision par liste spécifique
    readonly List<(string id, TMP_InputField inp)> _provListes = new List<(string, TMP_InputField)>();
    TMP_InputField _emailObjet, _emailCorps;
    bool _envoiEnCours;   // empêche un second clic de produire un second envoi
    UIDropdown _ribDD, _enteteDD, _numeroFormatDD, _periodeDD;
    string _autoSomme;   // dernière phrase de règlement auto (suivie tant que non personnalisée)
    string _autoEcheance;// dernière échéance proposée (suivie tant qu'elle n'est pas saisie à la main)
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
        Instance.OpenFor(fiche, ligneCiblee);
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
    void AppliquerLigneCiblee()
    {
        // « Correction » ne vaut que pour une facture RÉELLEMENT partie : sur une
        // facture seulement préparée, on la remplace. La ligne, elle, est transmise
        // dans les deux cas — c'est elle qui désigne la période à ouvrir.
        bool partie = _ligneCiblee != null
            && (_ligneCiblee.statut == "Envoye" || _ligneCiblee.statut == "Impaye"
             || _ligneCiblee.statut == "Paye");
        _titre.text = partie
            ? "Information Facture — Loyer (correction)"
            : "Information Facture — Loyer";
        if (_ligneCiblee == null || string.IsNullOrEmpty(_ligneCiblee.key)) return;

        var parts = _ligneCiblee.key.Split('-');   // ["loyer","2026","P3"]
        if (parts.Length >= 3 && int.TryParse(parts[1], out int y))
        {
            _annee.text = y.ToString();
            string p = parts[2].TrimStart('P', 'p');
            var (plabels, pids) = PeriodeOptions(_loc.periodiciteLoyer);
            _periodeDD.SetOptions(plabels, pids, p);
            // La période vient de changer : l'échéance et la phrase de règlement en
            // dépendent, et le bouton annonce la date d'envoi qui en découle. Sans ce
            // rappel, le panneau gardait l'échéance de la période précédente.
            RefreshEcheanceDefault();
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
            _ => { RefreshEntetePreview(); RefreshEcheanceDefault(); });
        UIFactory.LE(_periodeDD.gameObject, flexW: 1, minH: 46);
        _annee = UIFactory.Input(perRow.transform, "Année");
        _annee.contentType = TMP_InputField.ContentType.IntegerNumber;
        _annee.onValueChanged.AddListener(_ => { RefreshEntetePreview(); RefreshEcheanceDefault(); });
        UIFactory.LE(_annee.gameObject, prefW: 110, flexW: 0, minH: 46);

        _loyer = Labeled(mo, "Loyer HT (période)");
        _loyer.contentType = TMP_InputField.ContentType.DecimalNumber;
        _loyer.onValueChanged.AddListener(_ => { RefreshMontants(); RefreshEntetePreview(); });
        // Provision des charges générales ; les listes spécifiques ont chacune leur
        // ligne juste en dessous (ProvListes), imprimée sous son nom.
        _provisionLabel = UIFactory.Text(mo.transform, "Provision pour charges", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _provision = UIFactory.Input(mo.transform, "Provision pour charges");
        _provision.contentType = TMP_InputField.ContentType.DecimalNumber;
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

        // Ligne « montant mensuel » : seulement pour un loyer non mensuel.
        _mensuel = UIFactory.Toggle(mo.transform, "Ajouter « le montant mensuel à régler » (loyer de la période ÷ nb de mois)", true);
        _mensuel.onValueChanged.AddListener(_ => RefreshMontants());
        _texteMensuel = UIFactory.Input(mo.transform, FacturePdfService.MensuelDefaut, 46, true);
        SlashAutocomplete.Attach(_texteMensuel);
        UIFactory.Text(mo.transform,
            "Dans cette phrase, {montant} porte le montant mensuel calculé. « / » ouvre la liste des variables.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);

        // ── 3. Règlement (pied du document) ──
        // L'échéance, la phrase qu'elle alimente et le RIB sont voisins : le lien se
        // voit, d'où la mention « (défaut de la phrase de règlement) » retirée du
        // libellé de l'échéance — elle ne servait qu'à compenser l'éloignement.
        var p = UIFactory.Section(content, "Règlement", CoReglement, CoReglementL);
        _echeance = Labeled(p, "Date d'échéance");
        _echeance.onValueChanged.AddListener(_ => { RefreshSommeDefault(); RefreshBoutonSauver(); });
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
        UIFactory.Text(o.transform,
            "« / » ouvre la liste des variables. La facture est jointe en PDF automatiquement.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        _emailEnvoi = Labeled(o, "Email d'envoi");
        UIFactory.Text(o.transform,
            "Mode Pennylane : la facture est déposée sur Pennylane SANS être émise — à finaliser et transmettre depuis Pennylane.",
            UITheme.Role.Aide, UITheme.Alerte);

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
        _btnSauver.onClick.AddListener(SauvegarderEtEnvoyer);
    }

    // Construit les données de la facture (au visuel réel) depuis l'UI courante.
    FacturePdfService.Data BuildData()
    {
        var ctx = BuildContext();
        var rib = ReglageService.GetRib(_ribDD?.SelectedId);
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        float loyer = ParseF(_loyer.text), prov = ProvisionSaisie();
        float totalHT = loyer + prov, tva = totalHT * .2f, ttc = totalHT * 1.2f;
        // Montant mensuel = TTC de la période ÷ nombre de mois de la période (hors mensuel).
        int moisParPeriode = Mathf.Max(1, 12 / LoyerSummaryUI.NbPeriodes(_loc.periodiciteLoyer));
        bool afficheMensuel = _mensuel != null && _mensuel.isOn && _loc.periodiciteLoyer != Periodicite.mensuel;
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
            numero = ComposedNumero(),
            subtitle = $"Loyer {ctx.periode}",
            bodyHtml = FacturePdfService.BodyHtml(entResolved),
            totalPeriode = loyer, provision = prov, totalHT = totalHT, tva = tva, ttc = ttc,
            lignesProvision = LignesProvision(),
            tvaDebit = MentionTva.Imprimee(_mentionTva), retard = _retard.isOn,
            afficherMensuel = afficheMensuel, montantMensuel = afficheMensuel ? ttc / moisParPeriode : 0f,
            // Les textes libres passent par le même résolveur que l'entête : sans ça,
            // le menu « / » proposerait d'insérer {loc.nom}… qui s'imprimerait tel quel
            // sur un document envoyé au client.
            texteTvaDebit = FactureVarResolver.Resolve(MentionTva.Phrase(_mentionTva, _texteTvaDebit), _loc, _bat, ctx),
            texteMensuel = FactureVarResolver.Resolve(_texteMensuel.text, _loc, _bat, ctx),
            sommePhrase = FactureVarResolver.Resolve(_sommePhrase.text, _loc, _bat, ctx),
            ribTitulaire = rib?.titulaire, ribDomiciliation = rib?.domiciliation,
            ribNum = rib?.rib, ribIban = rib?.iban, ribBic = rib?.bic,
            legal = R.phraseRetard,
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
        SaveFromUI();
        var d = BuildData();
        string png = Path.Combine(FactureDir(), "apercu.png");
        if (FacturePdfService.GeneratePreviewPng(d, png, out string err)) ShowPreview(png);
        else UndoToast.Instance?.ShowInfo("Échec de l'aperçu : " + err);
    }

    // « Sauvegarder et envoyer » : mémorise + génère le PDF au visuel réel (local),
    // consomme le numéro (séquence +1) et rafraîchit l'aperçu. AUCUN envoi réel n'est
    // effectué ici (Pennylane / email seront activés après validation).
    void SauvegarderEtEnvoyer()
    {
        // L'envoi est asynchrone : sans cette garde, un double clic partirait deux fois.
        if (_envoiEnCours) { UndoToast.Instance?.ShowInfo("Un envoi est déjà en cours."); return; }

        SaveFromUI();
        var fl = _loc.factureLoyer;
        string key = $"loyer-{fl.anneePeriode}-P{fl.moisPeriode}";

        // Facture déjà émise pour cette période → on refait une version « corrigée(X) »
        // (même numéro, sans consommer de nouvelle séquence).
        var d = BuildData();
        var emission = FactureEmission.Preparer(_loc, key, d.numero);
        d.numero = emission.NumeroFacture;
        bool correction = emission.Correction;

        string dir = FactureDir();
        string fname = Sanitize($"Loyer-{_nom.text}-{d.subtitle}{emission.SuffixeFichier}") + ".pdf";
        string pdf = Path.Combine(dir, fname);

        if (!FacturePdfService.GeneratePdf(d, pdf, out string err))
        {
            UndoToast.Instance?.ShowInfo("Échec génération PDF : " + err);
            return;
        }

        // Aperçu à jour dans l'appli à partir du même rendu.
        string png = Path.Combine(dir, "apercu.png");
        if (FacturePdfService.GeneratePreviewPng(d, png, out _)) ShowPreview(png);

        // Avant la date d'envoi, RIEN ne part : la facture est préparée et attendra
        // son jour, où elle sera proposée au lancement de l'application. Un loyer
        // expédié un mois trop tôt ne se rappelle pas, et le calendrier existe
        // précisément pour ça.
        var dateEnvoi = FacturationSuivi.DateEnvoi("Loyer", fl.dateEcheanceISO);
        if (dateEnvoi.HasValue && DateTime.Today < dateEnvoi.Value)
        {
            PreparerPourEnvoi(d, key, emission, correction, pdf, dateEnvoi.Value);
            return;
        }

        // Mode Pennylane : dépôt de notre PDF, NON émis (voir PennylaneClient).
        if (R.modeEnvoi == ModeEnvoi.Pennylane)
        {
            StartCoroutine(DeposerPuisFinaliser(d, key, emission, correction, pdf));
            return;
        }

        // Envoi demandé. On refuse AVANT d'ouvrir la confirmation si quelque chose
        // manque : mieux vaut un message net qu'une boîte de dialogue qui promet un
        // départ impossible.
        string dest = (_emailEnvoi.text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dest))
        {
            UndoToast.Instance?.ShowInfo("Aucune adresse email pour ce locataire. "
                + "La facture n'a pas été envoyée ; le PDF est enregistré.");
            return;
        }

        string manque = EmailService.CeQuiManque();
        if (manque != null) { UndoToast.Instance?.ShowInfo(manque + " Le PDF est enregistré."); return; }

        var ctx = BuildContext();
        string objet = FactureVarResolver.Resolve(
            FacturePdfService.Texte(_emailObjet.text, EmailService.ObjetDefaut), _loc, _bat, ctx);
        string corps = FactureVarResolver.Resolve(
            FacturePdfService.Texte(_emailCorps.text, EmailService.CorpsDefaut), _loc, _bat, ctx);

        // Sans boîte de confirmation disponible, on n'envoie PAS : un départ non
        // confirmé vaut moins qu'un PDF enregistré qu'on renverra plus tard.
        if (ConfirmDialog.Instance == null)
        {
            UndoToast.Instance?.ShowInfo("Confirmation indisponible : rien n'a été envoyé. "
                + "Le PDF est enregistré.");
            return;
        }

        // Un email ne se rappelle pas : on montre exactement ce qui va partir.
        ConfirmDialog.Instance.Show(
            "Envoyer la facture par email ?",
            $"À : {dest}\nObjet : {objet}\nPièce jointe : {Path.GetFileName(pdf)}\n\n"
            + "Le message part immédiatement et ne pourra pas être rappelé.",
            () => StartCoroutine(EnvoyerPuisFinaliser(d, key, emission, correction, pdf, dest, objet, corps)),
            "Envoyer");
    }

    /// Facture préparée avant sa date d'envoi : le PDF est généré, la ligne passe « en
    /// attente d'envoi », et le départ se fera le jour dit. Le panneau l'annonce — une
    /// facture qu'on croit partie et qui dort est pire qu'un envoi manuel.
    void PreparerPourEnvoi(FacturePdfService.Data d, string key, FactureEmission.Decision emission,
                           bool correction, string pdf, DateTime dateEnvoi)
    {
        TryDate(_echeance.text, out var ech);
        string quand = dateEnvoi.ToString("dddd d MMMM yyyy", FacturePdfService.FrCulture);
        string detail =
            $"Cette facture sera envoyée le {quand}, soit "
            + $"{FacturationSuivi.EnvoiAvantJours} jours avant son échéance du {ech:dd/MM/yyyy}.\n\n"
            + "Elle est enregistrée dès maintenant et reste « en attente d'envoi ». "
            + "L'application vous la proposera au lancement, le jour venu.\n\n"
            + "Pour l'envoyer tout de suite malgré tout, avancez la date d'échéance ou "
            + "utilisez le bouton « Corriger » depuis le suivi le jour de l'envoi.";

        void Enregistrer() => Finaliser(d, key, emission, correction, pdf, false,
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
        bool correction, string pdf, string dest, string objet, string corps)
    {
        _envoiEnCours = true;   // un second clic produirait un second envoi
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

    /// Même ordre que l'email : le suivi n'est écrit (et le numéro consommé) qu'après
    /// un dépôt réussi. La ligne reste « en attente d'envoi » : rien n'est émis.
    System.Collections.IEnumerator DeposerPuisFinaliser(
        FacturePdfService.Data d, string key, FactureEmission.Decision emission, bool correction, string pdf)
    {
        _envoiEnCours = true;
        UndoToast.Instance?.ShowInfo("Dépôt sur Pennylane en cours…");

        var depot = new PennylaneClient.Depot();
        yield return PennylaneClient.Deposer(_loc.factureLoyer, d, pdf, depot);
        _envoiEnCours = false;

        if (!depot.Succes)
        {
            UndoToast.Instance?.ShowInfo(depot.Erreur + " Le PDF est enregistré, rien n'est consommé : tu peux réessayer.");
            yield break;
        }

        Finaliser(d, key, emission, correction, pdf, false,
            $"  Déposée sur Pennylane{(depot.FacturX ? " en Factur-X" : "")}, NON émise : "
            + "à finaliser et transmettre depuis Pennylane.");
    }

    /// Enregistrement du suivi, commun aux deux chemins (sans envoi, ou après un
    /// envoi réussi). Correction ou première émission : la règle vit dans
    /// FactureEmission, plus dans chacun des quatre panneaux.
    void Finaliser(FacturePdfService.Data d, string key, FactureEmission.Decision emission,
                   bool correction, string pdf, bool envoye, string suffixeMessage = "")
    {
        var fl = _loc.factureLoyer;

        string message = FactureEmission.Enregistrer(_loc, key, "Loyer", emission,
            d.subtitle, fl.dateEcheanceISO, pdf, d.ttc, _ribDD?.SelectedId,
            _loc.factureLoyer, "Facture", envoye);

        _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        LocataireSuiviInline.RefreshFor(_fiche);   // Suivi à jour tout de suite

        if (correction)
        {
            _ligneCiblee = null;
            _titre.text = "Information Facture — Loyer";
        }
        else
        {
            if (_numeroId != null) _numeroId.text = "";
            RefreshNumero();
        }

        UndoToast.Instance?.ShowInfo(message + suffixeMessage);
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
        catch (Exception e) { UndoToast.Instance?.ShowInfo("Aperçu illisible : " + e.Message); }
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
        UIFactory.Text(body.transform, "N° de facture  =  format  +  ID locataire", UITheme.Role.Aide, UITheme.TexteSecondaire);
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

    // ── Chargement ─────────────────────────────────────────────────────────────

    void LoadIntoUI()
    {
        if (_loc == null) return;
        var f = _loc.factureLoyer;   // peut être null (jamais rempli)

        _titre.text = $"Information Facture — Loyer · {(_loc.Name ?? "")}";

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
        _date.text = f != null && DateTime.TryParse(f.dateISO, out var dd)
            ? dd.ToString("dd/MM/yyyy") : now.ToString("dd/MM/yyyy");
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

        // Période facturée : options selon la périodicité + sélection & année mémorisées.
        var (plabels, pids) = PeriodeOptions(_loc.periodiciteLoyer);
        int periodeSel = f != null && f.moisPeriode > 0 ? f.moisPeriode : DefaultPeriodeIndex(_loc.periodiciteLoyer);
        _periodeDD.SetOptions(plabels, pids, periodeSel.ToString());
        _annee.text = (f != null && f.anneePeriode > 0 ? f.anneePeriode : DateTime.Today.Year).ToString();

        // Échéance : mémorisée si la facture en portait une, sinon le jour de demande
        // du loyer sur la période choisie — la même règle que le suivi, via
        // FacturationSuivi.EcheanceLoyer. Elle se recalcule ensuite à chaque
        // changement de période ou d'année, tant qu'elle n'a pas été saisie à la main.
        _autoEcheance = DefaultEcheance();
        _echeance.text = _autoEcheance;
        // Phrase de règlement : mémorisée si personnalisée, sinon défaut basé sur l'échéance.
        _autoSomme = DefaultSomme();
        _sommePhrase.text = !string.IsNullOrEmpty(f?.sommePhrase) ? f.sommePhrase : _autoSomme;

        // Montants pré-remplis (ou repris s'ils ont été saisis/mémorisés).
        int n = LoyerSummaryUI.NbPeriodes(_loc.periodiciteLoyer);
        float loyerCalc = _loc.loyerAnnuel / n;
        float provCalc = ListesCharges.Provision(_loc, "");   // charges générales ; les listes suivent
        _loyer.text = (f != null && f.saved ? f.loyerMontant : loyerCalc).ToString("0.00", CultureInfo.InvariantCulture);
        _provision.text = (f != null && f.saved ? f.provisionMontant : provCalc).ToString("0.00", CultureInfo.InvariantCulture);
        ChargerProvisionsListes(f);

        // N° de facture : l'ID locataire mémorisé est repris ; le préfixe format est
        // toujours recalculé. RefreshNumero() remplit l'ID avec la séquence s'il est vide.
        _numeroId.text = f != null && !string.IsNullOrEmpty(f.numeroId) ? f.numeroId : "";
        RefreshNumero();

        MentionTva.Charger(_mentionTva, _texteTvaDebit, f);
        _retard.isOn = f?.ajouterRetard ?? true;
        // Ligne « montant mensuel » : seulement pour un loyer non mensuel (trim / semestre / an).
        _mensuel.isOn = f?.ajouterMensuel ?? true;
        _mensuel.gameObject.SetActive(_loc.periodiciteLoyer != Periodicite.mensuel);

        // Pré-remplis avec le texte d'usine : l'utilisatrice doit voir la phrase
        // réellement imprimée, pas un champ vide dont il faut deviner l'effet.
        _texteMensuel.text  = FacturePdfService.Texte(f?.texteMensuel,  FacturePdfService.MensuelDefaut);
        // La phrase suit sa case : inutile de la montrer si la ligne ne s'imprime pas.
        _texteMensuel.gameObject.SetActive(_loc.periodiciteLoyer != Periodicite.mensuel);
        _emailEnvoi.text = !string.IsNullOrEmpty(f?.emailDest) ? f.emailDest : (_loc.emailLocataire ?? "");
        _emailObjet.text = FacturePdfService.Texte(f?.emailObjet, EmailService.ObjetDefaut);
        _emailCorps.text = FacturePdfService.Texte(f?.emailCorps, EmailService.CorpsDefaut);
        _modeInfo.text = R.modeEnvoi == ModeEnvoi.Pennylane
            ? "Mode global : Pennylane (e-facture Factur-X)."
            : "Mode global : Email direct (SMTP).";

        RefreshMontants();
        RefreshEntetePreview();
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
        if (f != null && f.saved && f.provisionsListes != null)
            foreach (var m in f.provisionsListes)
                if (m.montant > 0f && !ids.Contains(m.listeId) && ListesCharges.Effective(m.listeId) != "") ids.Add(m.listeId);

        foreach (var id in ids)
        {
            var memo = f != null && f.saved ? f.provisionsListes?.Find(m => m.listeId == id) : null;
            UIFactory.Text(_provListesBox, ListesCharges.LibelleProvision(id), UITheme.Role.Donnee, UITheme.TexteSecondaire);
            var inp = UIFactory.Input(_provListesBox, ListesCharges.LibelleProvision(id));
            inp.contentType = TMP_InputField.ContentType.DecimalNumber;
            inp.text = (memo != null ? memo.montant : ListesCharges.Provision(_loc, id)).ToString("0.00", CultureInfo.InvariantCulture);
            inp.onValueChanged.AddListener(_ => { RefreshMontants(); RefreshEntetePreview(); });
            _provListes.Add((id, inp));
        }
    }

    /// Total des provisions saisies (générale + listes) : ce qui entre dans le HT.
    float ProvisionSaisie() => ParseF(_provision.text) + _provListes.Sum(x => ParseF(x.inp.text));

    /// Lignes imprimées : une par liste. Null tant qu'aucune liste spécifique
    /// n'existe — la facture garde alors sa ligne unique « Provision pour charges ».
    List<KeyValuePair<string, float>> LignesProvision()
    {
        if (ListesCharges.Specifiques().Count == 0) return null;
        var res = new List<KeyValuePair<string, float>>
            { new KeyValuePair<string, float>(ListesCharges.LibelleProvision(""), ParseF(_provision.text)) };
        foreach (var (id, inp) in _provListes)
            res.Add(new KeyValuePair<string, float>(ListesCharges.LibelleProvision(id), ParseF(inp.text)));
        return res;
    }

    void RefreshMontants()
    {
        float loyer = ParseF(_loyer.text), prov = ProvisionSaisie();
        float totalHT = loyer + prov, tva = totalHT * .2f, ttc = totalHT * 1.2f;
        _mTotalHT.text = $"{totalHT:N2} €";
        _mTVA.text     = $"{tva:N2} €";
        _mTTC.text     = $"{ttc:N2} €";
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
            numero = ComposedNumero(),
            loyerHT = totalHT,
            tva = totalHT * .2f,
            ttc = totalHT * 1.2f,
        };
    }

    // ── Période facturée ───────────────────────────────────────────────────────

    // Options du menu « Période » selon la périodicité du loyer.
    static (List<string> labels, List<string> ids) PeriodeOptions(Periodicite p)
    {
        switch (p)
        {
            case Periodicite.trimestriel:
                return (new List<string> { "1er Trimestre", "2e Trimestre", "3e Trimestre", "4e Trimestre" },
                        new List<string> { "1", "2", "3", "4" });
            case Periodicite.BiAnnuel:
                return (new List<string> { "1er Semestre", "2e Semestre" }, new List<string> { "1", "2" });
            case Periodicite.Annuel:
                return (new List<string> { "Année complète" }, new List<string> { "1" });
            default: // mensuel
                return (new List<string>(MoisNoms), Enumerable.Range(1, 12).Select(i => i.ToString()).ToList());
        }
    }

    // Période proposée par défaut = la période EN COURS (d'après la date du jour),
    // pas systématiquement la 1re. Évite de facturer le « 1er trimestre » par erreur.
    /// Période en cours = la dernière dont le mois d'échéance est déjà atteint.
    /// Passe par FacturationSuivi.MoisEcheance, donc suit les mois de facturation
    /// cochés : sur un trimestriel facturé en février/mai/août/novembre, le calcul
    /// « (mois − 1) / 3 + 1 » proposait la mauvaise période.
    int DefaultPeriodeIndex(Periodicite p)
    {
        int m = DateTime.Today.Month;
        int n = LoyerSummaryUI.NbPeriodes(p);
        int enCours = 1;
        for (int per = 1; per <= n; per++)
            if (FacturationSuivi.MoisEcheance(_loc, per) <= m) enCours = per;
        return enCours;
    }

    int PeriodeAnnee()
    {
        if (int.TryParse((_annee != null ? _annee.text : "").Trim(), out int y) && y > 0) return y;
        return DateTime.Today.Year;
    }

    // Libellé de la période, ex. « 1er Trimestre 2023 », « Janvier 2023 », « Année 2023 ».
    string PeriodeLibelle()
    {
        int idx = 1; int.TryParse(_periodeDD?.SelectedId, out idx); if (idx < 1) idx = 1;
        int year = PeriodeAnnee();
        Periodicite p = _loc != null ? _loc.periodiciteLoyer : Periodicite.mensuel;
        switch (p)
        {
            case Periodicite.trimestriel: return $"{Ord(idx)} Trimestre {year}";
            case Periodicite.BiAnnuel:    return $"{Ord(idx)} Semestre {year}";
            case Periodicite.Annuel:      return $"Année {year}";
            default:                      return $"{MoisNoms[Mathf.Clamp(idx, 1, 12) - 1]} {year}";
        }
    }

    static string Ord(int n) => n == 1 ? "1er" : $"{n}e";

    // Phrase de règlement par défaut, dérivée de l'échéance.
    string DefaultSomme()
    {
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
    }

    // Date d'envoi de la facture en cours de saisie, d'après l'échéance affichée.
    DateTime? DateEnvoiSaisie()
        => TryDate(_echeance != null ? _echeance.text : "", out var ech)
            ? FacturationSuivi.DateEnvoi("Loyer", ech.ToString("yyyy-MM-dd"))
            : null;

    // Le bouton dit ce qu'il va faire. Il promettait un envoi immédiat même à un mois
    // de la date d'envoi — on cliquait alors « et envoyer » en croyant préparer, et le
    // locataire recevait sa facture bien trop tôt, sans retour possible.
    void RefreshBoutonSauver()
    {
        if (_btnSauver == null) return;
        var t = _btnSauver.GetComponentInChildren<TMP_Text>(true);
        if (t == null) return;
        var envoi = DateEnvoiSaisie();
        bool enAvance = envoi.HasValue && DateTime.Today < envoi.Value;
        t.text = enAvance
            ? $"Enregistrer pour l'envoi du {envoi.Value:dd/MM}"
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

    // Préfixe « format possible » selon le format et la date (l'ID vient après).
    static string NumeroPrefixe(string fmt, DateTime d) => FactureNumerotation.Prefixe(fmt, d);

    // N° complet = préfixe format + ID locataire saisi.
    string ComposedNumero()
    {
        string pfx = _numeroPrefixe != null ? _numeroPrefixe.text : "";
        string id = _numeroId != null ? (_numeroId.text ?? "").Trim() : "";
        return pfx + id;
    }

    // ── Aperçu (texte — le PDF au visuel réel viendra ensuite) ─────────────────

    void ShowApercu()
    {
        var ctx = BuildContext();
        var rib = ReglageService.GetRib(_ribDD?.SelectedId);
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        float loyer = ParseF(_loyer.text), prov = ProvisionSaisie();
        float totalHT = loyer + prov, tva = totalHT * .2f, ttc = totalHT * 1.2f;

        var sb = new System.Text.StringBuilder();
        string numAff = ComposedNumero();
        sb.AppendLine($"<b>FACTURE {(!string.IsNullOrWhiteSpace(numAff) ? "n° " + numAff : "")} — {ctx.periode}</b>");
        sb.AppendLine($"Date : {ctx.date:dd/MM/yyyy}");
        sb.AppendLine();
        sb.AppendLine($"<b>Destinataire</b> : {_nom.text}");
        sb.AppendLine($"{_adresse.text}");
        if (!string.IsNullOrWhiteSpace(_siret.text)) sb.AppendLine($"SIRET : {_siret.text}");
        sb.AppendLine();
        if (ent != null && !string.IsNullOrEmpty(ent.texte))
        {
            sb.AppendLine(FactureVarResolver.Resolve(ent.texte, _loc, _bat, ctx));
            sb.AppendLine();
        }
        sb.AppendLine($"Loyer HT (période) : {loyer:N2} €");
        if (prov > 0f) sb.AppendLine($"Provision charges : {prov:N2} €");
        sb.AppendLine($"Total HT : {totalHT:N2} €");
        sb.AppendLine($"TVA 20 % : {tva:N2} €");
        sb.AppendLine($"<b>Total TTC : {ttc:N2} €</b>");
        if (MentionTva.Imprimee(_mentionTva))
            sb.AppendLine("« " + FactureVarResolver.Resolve(
                MentionTva.Phrase(_mentionTva, _texteTvaDebit), _loc, _bat, BuildContext()) + " »");
        sb.AppendLine();
        if (rib != null)
        {
            sb.AppendLine("<b>Règlement</b>");
            sb.AppendLine($"{rib.titulaire} · {rib.domiciliation}");
            sb.AppendLine($"IBAN {rib.iban} · BIC {rib.bic}");
            sb.AppendLine();
        }
        if (_retard.isOn) sb.AppendLine(R.phraseRetard);

        Modal("Aperçu de la facture (loyer)", sb.ToString());
    }

    void Modal(string titre, string texte)
    {
        var scrim = UIFactory.Rect("ApercuScrim", transform);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
        scrim.SetAsLastSibling();

        var cardImg = UIFactory.Panel("ApercuCard", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(760, 820);
        var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10; v.padding = new RectOffset(20, 20, 18, 18);
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        UIFactory.Text(v.transform, titre, UITheme.Role.Section, UITheme.TextePrincipal, true);
        var body = MakeScroll(v.transform);
        UIFactory.LE(((Transform)body.parent.parent).gameObject, flexH: 1);
        UIFactory.Text(body, texte, UITheme.Role.Donnee, UITheme.TextePrincipal);

        var close = UIFactory.Button(v.transform, "Fermer", UITheme.Primaire, Color.white, 44, UITheme.Role.Bouton);
        close.onClick.AddListener(() => Destroy(scrim.gameObject));
    }

    // ── Sauvegarde (mémorisé sur le locataire) ─────────────────────────────────

    void SaveFromUI()
    {
        if (_loc == null) return;
        var f = _loc.factureLoyer ?? new FactureInfo();
        // Destinataire mémorisé SUR LA FACTURE (peut différer du locataire — on n'écrase pas la fiche).
        f.destNom = _nom.text;
        f.destAdresse = _adresse.text;
        f.destSiret = _siret.text;
        f.ribId = _ribDD?.SelectedId;
        f.enteteId = _enteteDD?.SelectedId;
        f.dateISO = TryDate(_date.text, out var d) ? d.ToString("yyyy-MM-dd") : "";
        f.dateEcheanceISO = TryDate(_echeance.text, out var de) ? de.ToString("yyyy-MM-dd") : "";
        f.sommePhrase = _sommePhrase.text;
        f.numeroFormat = _numeroFormatDD?.SelectedId ?? "AMN";
        f.numeroId = (_numeroId.text ?? "").Trim();
        f.numero = ComposedNumero();
        MentionTva.Enregistrer(_mentionTva, _texteTvaDebit, f);
        f.ajouterRetard = _retard.isOn;
        f.ajouterMensuel = _mensuel.isOn;
        f.texteMensuel = _texteMensuel.text;
        f.emailDest = _emailEnvoi.text;
        f.emailObjet = _emailObjet.text;
        f.emailCorps = _emailCorps.text;
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
