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
    static readonly Color CoAmbre = Hex("#854F0B"), CoAmbreL = Hex("#F6E6C8");
    static readonly Color CoTaupe = Hex("#5F5E5A"), CoTaupeL = Hex("#E9E6DE");

    static readonly string[] MoisNoms =
    { "Janvier","Février","Mars","Avril","Mai","Juin","Juillet","Août","Septembre","Octobre","Novembre","Décembre" };

    static readonly List<string> NumFmtLabels = new List<string> { "Année / Numéro", "Année / Mois-Numéro", "Année / JourMois-Numéro" };
    static readonly List<string> NumFmtIds = new List<string> { "AN", "AMN", "AJMN" };

    LocatairePrefab _fiche; Locataire _loc; Batiment _bat;
    FactureEtat _correctionTarget;   // ligne du suivi à corriger (ouverture via « Corriger »)

    TMP_Text _titre, _entetePreview, _modeInfo, _mTotalHT, _mTVA, _mTTC, _numeroPrefixe;
    TMP_InputField _nom, _adresse, _siret, _date, _echeance, _numeroId, _annee, _refInterne, _sommePhrase, _loyer, _provision, _emailEnvoi;
    UIDropdown _ribDD, _enteteDD, _numeroFormatDD, _periodeDD;
    string _autoSomme;   // dernière phrase de règlement auto (suivie tant que non personnalisée)
    Toggle _tvaDebit, _retard, _envoiEmail, _mensuel;

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

    // `correctionTarget` != null : ouverture via « Corriger » sur une facture déjà
    // émise → on force la période sur la ligne visée et la sauvegarde crée une
    // version « corrigée(X) » (même numéro).
    public static void OpenLoyer(LocatairePrefab fiche, FactureEtat correctionTarget)
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
        Instance.OpenFor(fiche, correctionTarget);
    }

    void OpenFor(LocatairePrefab fiche, FactureEtat correctionTarget = null)
    {
        _fiche = fiche;
        _loc = fiche.GetLocataire();
        _bat = fiche.batimentPrefabOrigin != null ? fiche.batimentPrefabOrigin.getBatiment() : null;
        _correctionTarget = correctionTarget;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ResetPreview();   // pas d'aperçu du locataire précédent
        LoadIntoUI();
        ApplyCorrectionTarget();
    }

    // Force la période/année du formulaire sur la ligne à corriger (clé « loyer-{Y}-P{P} »).
    void ApplyCorrectionTarget()
    {
        _titre.text = _correctionTarget != null
            ? "Information Facture — Loyer (correction)"
            : "Information Facture — Loyer";
        if (_correctionTarget == null || string.IsNullOrEmpty(_correctionTarget.key)) return;

        var parts = _correctionTarget.key.Split('-');   // ["loyer","2026","P3"]
        if (parts.Length >= 3 && int.TryParse(parts[1], out int y))
        {
            _annee.text = y.ToString();
            string p = parts[2].TrimStart('P', 'p');
            var (plabels, pids) = PeriodeOptions(_loc.periodiciteLoyer);
            _periodeDD.SetOptions(plabels, pids, p);
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
        var back = UIFactory.Button(header.transform, "←  Retour", UITheme.Carte, UITheme.TextePrincipal, 42, 18);
        UIFactory.Border(back.gameObject); UIFactory.LE(back.gameObject, prefW: 140, flexW: 0);
        back.onClick.AddListener(Close);
        _titre = UIFactory.Text(header.transform, "Information Facture — Loyer", 26, UITheme.TextePrincipal, true);
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

        // Texte libre affiché en rouge avec le n° (ex. « N° Interne Magasin 001048 »).
        _refInterne = Labeled(f, "Texte / N° interne (optionnel, en rouge au-dessus du n°)");
        _refInterne.onValueChanged.AddListener(_ => RefreshEntetePreview());

        // Période facturée : mois / trimestre / semestre / année selon la périodicité.
        UIFactory.Text(f.transform, "Période facturée", 16, UITheme.TexteSecondaire);
        var perRow = UIFactory.HBox(f.transform, 8, false, "PeriodeRow");
        UIFactory.LE(perRow.gameObject, minH: 46);
        _periodeDD = UIDropdown.Create(perRow.transform, new List<string>(MoisNoms),
            Enumerable.Range(1, 12).Select(i => i.ToString()).ToList(), 0, _ => RefreshEntetePreview());
        UIFactory.LE(_periodeDD.gameObject, flexW: 1, minH: 46);
        _annee = UIFactory.Input(perRow.transform, "Année");
        _annee.contentType = TMP_InputField.ContentType.IntegerNumber;
        _annee.onValueChanged.AddListener(_ => RefreshEntetePreview());
        UIFactory.LE(_annee.gameObject, prefW: 110, flexW: 0, minH: 46);

        UIFactory.Text(f.transform, "Texte de présentation (entête / paragraphe)", 16, UITheme.TexteSecondaire);
        _enteteDD = BuildEnteteDropdown(f.transform);
        UIFactory.Text(f.transform, "Aperçu (variables remplacées) :", 14, UITheme.TexteSecondaire);
        var pv = UIFactory.Panel("Preview", f.transform, Color.white);
        UIFactory.Border(pv.gameObject);
        var pvv = pv.gameObject.AddComponent<VerticalLayoutGroup>();
        pvv.padding = new RectOffset(12, 12, 10, 10); pvv.childControlWidth = true; pvv.childControlHeight = true; pvv.childForceExpandWidth = true;
        pv.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _entetePreview = UIFactory.Text(pvv.transform, "—", 15, UITheme.TextePrincipal);

        // ── Montants ──
        var mo = UIFactory.Section(content, "Montants", CoTaupe, CoTaupeL);
        _loyer = Labeled(mo, "Loyer HT (période)");
        _loyer.contentType = TMP_InputField.ContentType.DecimalNumber;
        _loyer.onValueChanged.AddListener(_ => { RefreshMontants(); RefreshEntetePreview(); });
        _provision = Labeled(mo, "Provision pour charges");
        _provision.contentType = TMP_InputField.ContentType.DecimalNumber;
        _provision.onValueChanged.AddListener(_ => { RefreshMontants(); RefreshEntetePreview(); });
        _mTotalHT = MontRow(mo, "Total HT");
        _mTVA     = MontRow(mo, "TVA 20 %");
        _mTTC     = MontRow(mo, "Total TTC");

        // ── Options / envoi ──
        var o = UIFactory.Section(content, "Options & envoi", CoVert, CoVertL);
        _tvaDebit = UIFactory.Toggle(o.transform, "Ajouter la mention « TVA payée sur les débits »", true);
        _retard = UIFactory.Toggle(o.transform, "Ajouter la phrase de retard / pénalités de paiement", true);
        // Option (hors mensuel) : ligne « montant mensuel à régler » = loyer de la période ÷ nb de mois.
        _mensuel = UIFactory.Toggle(o.transform, "Ajouter « le montant mensuel à régler » (loyer de la période ÷ nb de mois)", true);
        _mensuel.onValueChanged.AddListener(_ => RefreshMontants());
        _modeInfo = UIFactory.Text(o.transform, "", 15, UITheme.TexteSecondaire);
        _envoiEmail = UIFactory.Toggle(o.transform, "Envoyer par email (au lieu de Pennylane)", false);
        _emailEnvoi = Labeled(o, "Email d'envoi");
        UIFactory.Text(o.transform,
            "Note : l'envoi réel (Pennylane / email) sera activé après validation — rien n'est émis pour l'instant.",
            14, UITheme.Alerte);

        // Bouton « Générer facture » sous le formulaire (rend l'aperçu à droite).
        var gen = UIFactory.Button(left.transform, "Générer facture", UITheme.Primaire, Color.white, 46, 20);
        UIFactory.LE(gen.gameObject, minH: 56, flexH: 0);
        gen.onClick.AddListener(GenererApercu);

        // ── Colonne droite : aperçu de la facture rendue ──
        var right = UIFactory.VBox(main.transform, 12, 0, 0, 0, 0, "Right");
        UIFactory.LE(right.gameObject, flexW: 58);
        right.childForceExpandHeight = false;

        UIFactory.Text(right.transform, "Facture générée", 22, UITheme.TextePrincipal, true);

        // Fond gris (type visionneuse PDF) qui occupe la hauteur restante ; la feuille
        // (image A4) est centrée dedans, avec zoom (molette / boutons) et déplacement.
        var backdrop = UIFactory.Panel("PvBackdrop", right.transform, Hex("#D2D0CB"));
        UIFactory.Border(backdrop.gameObject);
        UIFactory.LE(backdrop.gameObject, flexH: 1);
        var backRT = (RectTransform)backdrop.transform;
        backRT.pivot = new Vector2(.5f, .5f);
        backdrop.gameObject.AddComponent<RectMask2D>();     // masque l'image qui déborde

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

        // Bouton « Sauvegarder et envoyer » sous l'aperçu.
        var save = UIFactory.Button(right.transform, "Sauvegarder et envoyer", Hex("#854F0B"), Color.white, 46, 20);
        UIFactory.LE(save.gameObject, minH: 56, flexH: 0);
        save.onClick.AddListener(SauvegarderEtEnvoyer);
    }

    // Construit les données de la facture (au visuel réel) depuis l'UI courante.
    FacturePdfService.Data BuildData()
    {
        var ctx = BuildContext();
        var rib = ReglageService.GetRib(_ribDD?.SelectedId);
        var ent = ReglageService.GetEntete(_enteteDD?.SelectedId);
        float loyer = ParseF(_loyer.text), prov = ParseF(_provision.text);
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
            tvaDebit = _tvaDebit.isOn, retard = _retard.isOn,
            afficherMensuel = afficheMensuel, montantMensuel = afficheMensuel ? ttc / moisParPeriode : 0f,
            sommePhrase = _sommePhrase.text,
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
        SaveFromUI();
        var fl = _loc.factureLoyer;
        string key = $"loyer-{fl.anneePeriode}-P{fl.moisPeriode}";

        // Facture déjà émise pour cette période → on refait une version « corrigée(X) »
        // (même numéro, sans consommer de nouvelle séquence).
        bool correction = FacturationSuivi.EstDejaEmise(_loc, key, out var recExist);
        int x = correction ? recExist.corrections + 1 : 0;

        var d = BuildData();
        if (correction && !string.IsNullOrEmpty(recExist.numero))
            d.numero = recExist.numero + $" corrigée({x})";

        string dir = FactureDir();
        string fname = Sanitize($"Loyer-{_nom.text}-{d.subtitle}{(correction ? $"-corrigee{x}" : "")}") + ".pdf";
        string pdf = Path.Combine(dir, fname);

        if (!FacturePdfService.GeneratePdf(d, pdf, out string err))
        {
            UndoToast.Instance?.ShowInfo("Échec génération PDF : " + err);
            return;
        }

        // Aperçu à jour dans l'appli à partir du même rendu.
        string png = Path.Combine(dir, "apercu.png");
        if (FacturePdfService.GeneratePreviewPng(d, png, out _)) ShowPreview(png);

        if (correction)
        {
            // Met à jour la ligne existante (lien PDF + libellé « corrigée(X) »),
            // conserve le numéro et le statut. Pas de nouvelle séquence consommée.
            FacturationSuivi.MarquerCorrige(_loc, key, d.subtitle, pdf, d.ttc);
            _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
            LocataireSuiviInline.RefreshFor(_fiche);   // Suivi à jour tout de suite
            _correctionTarget = null;
            _titre.text = "Information Facture — Loyer";
            UndoToast.Instance?.ShowInfo($"Facture corrigée ({x}) enregistrée (PDF). Envoi réel non activé.");
            return;
        }

        // Suivi : la ligne du loyer de cette période passe « Envoyé » (ou « En attente
        // d'envoi » si préparée plus de 15 j avant l'échéance — décidé dans MarquerEnvoye).
        var ribS = ReglageService.GetRib(_ribDD?.SelectedId);
        string ribNom = ribS != null ? (!string.IsNullOrWhiteSpace(ribS.name) ? ribS.name : ribS.titulaire) : "";
        FacturationSuivi.MarquerEnvoye(_loc, key, "Loyer",
            d.subtitle, fl.dateEcheanceISO, d.numero, pdf, d.ttc, _ribDD?.SelectedId, ribNom);

        // N° consommé → on avance la séquence (unique par locataire) et on oublie
        // l'ID mémorisé pour reproposer la nouvelle séquence à la prochaine ouverture.
        _loc.factureSeq = Mathf.Max(1, _loc.factureSeq) + 1;
        if (_loc.factureLoyer != null) _loc.factureLoyer.numeroId = "";
        _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        LocataireSuiviInline.RefreshFor(_fiche);   // Suivi à jour tout de suite
        if (_numeroId != null) _numeroId.text = "";
        RefreshNumero();

        UndoToast.Instance?.ShowInfo("Facture enregistrée (PDF). Envoi réel non activé — rien n'a été émis.");
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

    static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = (s ?? "").Replace(c, '-');
        return s;
    }

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
        UIFactory.Text(body.transform, "N° de facture  =  format  +  ID locataire", 16, UITheme.TexteSecondaire);
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
        _numeroPrefixe = UIFactory.Text(pfx.transform, "—", 19, UITheme.TextePrincipal, true);

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
        // Échéance : mémorisée, sinon proposée à +30 jours de la date de facture.
        _echeance.text = f != null && DateTime.TryParse(f.dateEcheanceISO, out var de)
            ? de.ToString("dd/MM/yyyy")
            : (TryDate(_date.text, out var dbase) ? dbase.AddDays(30) : now.AddDays(30)).ToString("dd/MM/yyyy");
        // Phrase de règlement : mémorisée si personnalisée, sinon défaut basé sur l'échéance.
        _autoSomme = DefaultSomme();
        _sommePhrase.text = !string.IsNullOrEmpty(f?.sommePhrase) ? f.sommePhrase : _autoSomme;

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

        // Montants pré-remplis (ou repris s'ils ont été saisis/mémorisés).
        int n = LoyerSummaryUI.NbPeriodes(_loc.periodiciteLoyer);
        float loyerCalc = _loc.loyerAnnuel / n;
        float provCalc = _loc.provisionPourCharges ? _loc.provisionPourChargeValue : 0f;
        _loyer.text = (f != null && f.saved ? f.loyerMontant : loyerCalc).ToString("0.00", CultureInfo.InvariantCulture);
        _provision.text = (f != null && f.saved ? f.provisionMontant : provCalc).ToString("0.00", CultureInfo.InvariantCulture);

        // N° de facture : l'ID locataire mémorisé est repris ; le préfixe format est
        // toujours recalculé. RefreshNumero() remplit l'ID avec la séquence s'il est vide.
        _numeroId.text = f != null && !string.IsNullOrEmpty(f.numeroId) ? f.numeroId : "";
        RefreshNumero();

        _tvaDebit.isOn = f?.tvaDebit ?? true;
        _retard.isOn = f?.ajouterRetard ?? true;
        // Ligne « montant mensuel » : seulement pour un loyer non mensuel (trim / semestre / an).
        _mensuel.isOn = f?.ajouterMensuel ?? true;
        _mensuel.gameObject.SetActive(_loc.periodiciteLoyer != Periodicite.mensuel);
        _envoiEmail.isOn = f?.envoiEmail ?? false;
        _emailEnvoi.text = !string.IsNullOrEmpty(f?.emailDest) ? f.emailDest : (_loc.emailLocataire ?? "");
        _modeInfo.text = R.modeEnvoi == ModeEnvoi.Pennylane
            ? "Mode global : Pennylane (e-facture Factur-X)."
            : "Mode global : Email direct (SMTP).";

        RefreshMontants();
        RefreshEntetePreview();
    }

    void RefreshMontants()
    {
        float loyer = ParseF(_loyer.text), prov = ParseF(_provision.text);
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
        float loyer = ParseF(_loyer.text), prov = ParseF(_provision.text);
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
    static int DefaultPeriodeIndex(Periodicite p)
    {
        int m = DateTime.Today.Month;
        switch (p)
        {
            case Periodicite.mensuel:     return m;                 // mois courant
            case Periodicite.trimestriel: return (m - 1) / 3 + 1;   // trimestre courant (sept → 3)
            case Periodicite.BiAnnuel:    return m <= 6 ? 1 : 2;    // semestre courant
            default:                      return 1;                 // annuel
        }
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
        DateTime ech = TryDate(_echeance.text, out var ed) ? ed : DateTime.Today;
        return "SOMME À NOUS RÉGLER LE " + ech.ToString("d MMMM yyyy", FacturePdfService.FrCulture);
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
    static string NumeroPrefixe(string fmt, DateTime d)
    {
        switch (fmt)
        {
            case "AN": return $"{d.Year}/";
            case "AJMN": return $"{d.Year}/{d.Day:D2}{d.Month:D2}";
            default: return $"{d.Year}/{d.Month:D2}"; // AMN
        }
    }

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
        float loyer = ParseF(_loyer.text), prov = ParseF(_provision.text);
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
        if (_tvaDebit.isOn) sb.AppendLine("« la TVA est payée sur les débits »");
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

        UIFactory.Text(v.transform, titre, 22, UITheme.TextePrincipal, true);
        var body = MakeScroll(v.transform);
        UIFactory.LE(((Transform)body.parent.parent).gameObject, flexH: 1);
        UIFactory.Text(body, texte, 15, UITheme.TextePrincipal);

        var close = UIFactory.Button(v.transform, "Fermer", UITheme.Primaire, Color.white, 44, 18);
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
        f.tvaDebit = _tvaDebit.isOn;
        f.ajouterRetard = _retard.isOn;
        f.ajouterMensuel = _mensuel.isOn;
        f.envoiEmail = _envoiEmail.isOn;
        f.emailDest = _emailEnvoi.text;
        f.loyerMontant = ParseF(_loyer.text);
        f.provisionMontant = ParseF(_provision.text);
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
        UIFactory.Text(body.transform, label, 16, UITheme.TexteSecondaire);
        return UIFactory.Input(body.transform, label);
    }

    void ZoomBtn(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float w = 44)
    {
        var b = UIFactory.Button(parent, label, UITheme.Carte, UITheme.TextePrincipal, 36, 18, false);
        UIFactory.Border(b.gameObject);
        UIFactory.LE(b.gameObject, prefW: w, flexW: 0, minH: 36);
        b.onClick.AddListener(onClick);
    }

    TMP_Text MontRow(VerticalLayoutGroup body, string label)
    {
        var h = UIFactory.HBox(body.transform, 8, false, "Row");
        UIFactory.LE(h.gameObject, minH: 24);
        var l = UIFactory.Text(h.transform, label, 16, UITheme.TexteSecondaire);
        UIFactory.LE(l.gameObject, flexW: 1);
        return UIFactory.Text(h.transform, "—", 16, UITheme.TextePrincipal, true, TextAlignmentOptions.Right);
    }

    // Passe par SaisieNumerique : « . » et « , » y sont interchangeables et les
    // espaces de milliers acceptes (cette copie locale ne gerait que la virgule).
    static float ParseF(string s) => SaisieNumerique.Parse(s);

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static bool TryDate(string s, out DateTime d) =>
        DateTime.TryParseExact((s ?? "").Trim(),
            new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
}
