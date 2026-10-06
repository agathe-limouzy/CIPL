using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Écran Réglage (facturation), construit 100 % par code.
/// Sections : Connexion Pennylane + mode d'envoi (Pennylane / Email + SMTP),
/// RIB (ajout/modif/suppr), Entêtes (modèles de texte), textes fixes, sauvegarde.
public class ReglagePanel : MonoBehaviour
{
    public static ReglagePanel Instance { get; private set; }

    // Couleurs de sections (accent, tint clair)
    static readonly Color CoVert = UITheme.Primaire, CoVertL = UITheme.PrimaireClair;
    static readonly Color CoBleu = Hex("#2C3E5E"), CoBleuL = Hex("#DEE3EB");
    static readonly Color CoAmbre = Hex("#854F0B"), CoAmbreL = Hex("#F6E6C8");
    static readonly Color CoTaupe = Hex("#5F5E5A"), CoTaupeL = Hex("#E9E6DE");
    static readonly Color CoPetrole = Hex("#16697A"), CoPetroleL = Hex("#D7E9EC");

    // Références UI
    TMP_InputField _apiKey, _smtpHost, _smtpPort, _smtpFromEmail, _smtpFromName, _smtpPwd, _smtpUser;
    TMP_InputField _mapboxToken;
    TMP_InputField _phraseRetard, _basDePage, _entrepriseNom, _lieuEmission;
    TMP_InputField _tvaDebits, _tvaEncaissements;
    Toggle _modePennylane;
    GameObject _smtpCard;
    Button _testEmail;
    TMP_Text _testInfo;
    Transform _ribList, _enteteList;
    TMP_Text _savePath;
    RawImage _logoPreview;
    AspectRatioFitter _logoAspect;
    TMP_Text _logoInfo;

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

    // ── Ouverture / fermeture ──────────────────────────────────────────────────

    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        LoadIntoUI();
    }

    public void Close() => gameObject.SetActive(false);

    /// Point d'entrée robuste : crée le panneau à la volée si besoin, puis l'ouvre.
    public static void OpenPanel()
    {
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("ReglagePanel", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<ReglagePanel>();
        }
        Instance.Open();
    }

    // ── Construction ───────────────────────────────────────────────────────────

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
        var back = UIFactory.Button(header.transform, "←  Retour", UITheme.Carte, UITheme.TextePrincipal, 42, UITheme.Role.Bouton);
        UIFactory.Border(back.gameObject);
        UIFactory.LE(back.gameObject, prefW: 140, flexW: 0);
        back.onClick.AddListener(Close);
        var title = UIFactory.Text(header.transform, "Réglage", UITheme.Role.Page, UITheme.TextePrincipal, true);
        UIFactory.LE(title.gameObject, flexW: 1);

        // Scroll
        var content = MakeScroll(col.transform);

        BuildAffichage(content);
        BuildConnexion(content);
        BuildLogo(content);
        BuildRibs(content);
        BuildEntetes(content);
        BuildListesCharges(content);
        BuildTextes(content);
        BuildSauvegarde(content);

        // Footer
        var footer = UIFactory.HBox(col.transform, 12, false, "Footer");
        UIFactory.LE(footer.gameObject, minH: 56);
        var save = UIFactory.Button(footer.transform, "Enregistrer", UITheme.Primaire, Color.white, 46, UITheme.Role.Bouton);
        UIFactory.LE(save.gameObject, flexW: 1);
        save.onClick.AddListener(SaveFromUI);
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

        var content = UIFactory.VBox(viewport, 16, 2, 8, 2, 12, "Content");
        var crt = (RectTransform)content.transform;
        crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(.5f, 1);
        crt.offsetMin = new Vector2(0, 0); crt.offsetMax = Vector2.zero;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        sr.viewport = viewport; sr.content = crt;
        return content.transform;
    }

    // ── Section Connexion + mode d'envoi ───────────────────────────────────────

    void BuildConnexion(Transform parent)
    {
        var body = UIFactory.Section(parent, "Connexion & envoi", CoVert, CoVertL);

        UIFactory.Text(body.transform, "Nom de l'entreprise", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _entrepriseNom = UIFactory.Input(body.transform, "GROUPE CIPL");

        UIFactory.Text(body.transform, "Clé API Pennylane", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _apiKey = UIFactory.Input(body.transform, "Collez votre clé API…");
        _apiKey.contentType = TMP_InputField.ContentType.Password;

        // Token Mapbox : saisi ici plutôt que dans le champ public de TileLoader, qui
        // était sérialisé en clair dans Maps.prefab et donc commité dans le dépôt.
        UIFactory.Text(body.transform, "Token Mapbox (cartes)", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _mapboxToken = UIFactory.Input(body.transform, "Collez votre token Mapbox…");
        _mapboxToken.contentType = TMP_InputField.ContentType.Password;

        _modePennylane = UIFactory.Toggle(body.transform, "Envoyer via Pennylane (e-facture Factur-X)", true);

        // Cette case ne gouverne QUE l'envoi des factures. Les réglages SMTP restent
        // visibles dans les deux modes : les relances d'impayé partent par email même
        // en Pennylane, donc les masquer les rendait inconfigurables.
        UIFactory.Text(body.transform,
            "Cette case choisit comment partent les FACTURES. Les relances d'impayé passent "
            + "par email dans tous les cas : les réglages SMTP ci-dessous servent donc toujours. "
            + "(Pennylane : loyers seulement pour l'instant, déposés sans être émis.)",
            UITheme.Role.Aide, UITheme.TexteSecondaire);

        // Sous-carte SMTP (mode Email)
        var smtpBody = UIFactory.Card(body.transform);
        _smtpCard = smtpBody.gameObject; // la carte SMTP (le VLG est porté par la carte)
        UIFactory.Text(smtpBody.transform, "Envoi par email (SMTP)", UITheme.Role.Libelle, UITheme.TextePrincipal, true);
        UIFactory.Text(smtpBody.transform,
            "Sert aux relances d'impayé dans tous les cas, et à l'envoi des factures si le mode "
            + "email est choisi ci-dessus. N'importe quelle messagerie convient, avec le port 587 :",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        UIFactory.Text(smtpBody.transform,
            "Gmail : smtp.gmail.com  ·  Outlook / Microsoft 365 : smtp.office365.com  ·  "
            + "OVH : ssl0.ovh.net  ·  Free : smtp.free.fr  ·  Orange : smtp.orange.fr",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        UIFactory.Text(smtpBody.transform,
            "Attention : Gmail et Outlook exigent un mot de passe d'application (créé dans les réglages de "
            + "sécurité du compte), pas le mot de passe habituel. Sur Microsoft 365, SMTP AUTH est "
            + "souvent à activer côté administrateur. Le port 465 n'est pas géré : utilise 587.",
            UITheme.Role.Aide, UITheme.Alerte);

        _smtpHost = LabeledInput(smtpBody.transform, "Serveur SMTP", "smtp.office365.com");
        _smtpPort = LabeledInput(smtpBody.transform, "Port", "587");
        _smtpPort.contentType = TMP_InputField.ContentType.IntegerNumber;
        _smtpFromEmail = LabeledInput(smtpBody.transform, "Adresse d'expédition", "contact@cipl.fr");
        _smtpFromName = LabeledInput(smtpBody.transform, "Nom d'expédition", "GROUPE CIPL");
        _smtpUser = LabeledInput(smtpBody.transform,
            "Identifiant SMTP (si différent de l'adresse d'expédition)", "laisser vide si identique");
        _smtpPwd = LabeledInput(smtpBody.transform, "Mot de passe (d'application)", "••••••");
        _smtpPwd.contentType = TMP_InputField.ContentType.Password;

        // Test d'envoi. Il écrit à l'adresse d'EXPÉDITION, jamais à un locataire :
        // c'est le seul moyen de valider serveur, port, mot de passe et STARTTLS
        // sans qu'un vrai destinataire puisse recevoir quoi que ce soit.
        _testEmail = UIFactory.Button(smtpBody.transform, "Envoyer un email de test",
                                      UITheme.Carte, UITheme.TextePrincipal, 42, UITheme.Role.Bouton);
        UIFactory.Border(_testEmail.gameObject);
        _testEmail.onClick.AddListener(TesterEnvoi);

        _testInfo = UIFactory.Text(smtpBody.transform,
            "Le test part vers l'adresse d'expédition ci-dessus. Enregistre les réglages avant.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
    }

    // ── Test d'envoi SMTP ──────────────────────────────────────────────────────

    void TesterEnvoi()
    {
        string manque = EmailService.CeQuiManque();
        if (manque != null) { _testInfo.text = manque; _testInfo.color = UITheme.Alerte; return; }

        string destinataire = ReglageService.Current.smtp.fromEmail;

        _testEmail.interactable = false;
        _testInfo.color = UITheme.TexteSecondaire;
        _testInfo.text = $"Envoi en cours vers {destinataire}…";

        var envoi = EmailService.Envoyer(destinataire, EmailService.TestObjet, EmailService.TestCorps());
        StartCoroutine(AttendreEnvoi(envoi, destinataire));
    }

    /// L'envoi tourne sur un thread de fond ; l'UI ne peut être touchée que depuis
    /// le thread principal, d'où cette attente en coroutine plutôt qu'un callback.
    System.Collections.IEnumerator AttendreEnvoi(EmailService.Envoi envoi, string destinataire)
    {
        while (!envoi.Termine) yield return null;

        _testEmail.interactable = true;

        if (envoi.Succes)
        {
            _testInfo.color = UITheme.Primaire;
            _testInfo.text = $"Envoyé à {destinataire}. Vérifie la réception — si le message n'arrive pas, "
                           + "regarde les indésirables avant de conclure à un échec.";
            UndoToast.Instance?.ShowInfo("Email de test envoyé.");
        }
        else
        {
            _testInfo.color = UITheme.Alerte;
            _testInfo.text = envoi.Erreur;
            ConfirmDialog.Erreur("Échec du test d'envoi — détail sous le bouton.");
        }
    }

    TMP_InputField LabeledInput(Transform parent, string label, string placeholder)
    {
        UIFactory.Text(parent, label, UITheme.Role.Libelle, UITheme.TexteSecondaire);
        return UIFactory.Input(parent, placeholder);
    }

    // ── Section Logo de la facture ─────────────────────────────────────────────

    void BuildLogo(Transform parent)
    {
        var body = UIFactory.Section(parent, "Logo de la facture", CoPetrole, CoPetroleL);
        UIFactory.Text(body.transform,
            "Le logo apparaît en haut de la facture (remplace le logo CIPL par défaut).",
            UITheme.Role.Aide, UITheme.TexteSecondaire);

        var previewBg = UIFactory.Panel("LogoPreview", body.transform, Color.white);
        UIFactory.Border(previewBg.gameObject);
        UIFactory.LE(previewBg.gameObject, minH: 130, prefH: 130);
        var imgGO = new GameObject("Img", typeof(RectTransform));
        imgGO.transform.SetParent(previewBg.transform, false);
        _logoPreview = imgGO.AddComponent<RawImage>();
        _logoPreview.raycastTarget = false;
        var irt = (RectTransform)imgGO.transform;
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(.5f, .5f);
        _logoAspect = imgGO.AddComponent<AspectRatioFitter>();
        _logoAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        _logoInfo = UIFactory.Text(body.transform, "", UITheme.Role.Donnee, UITheme.TexteSecondaire);

        var row = UIFactory.HBox(body.transform, 8, false, "LogoBtns");
        var pick = UIFactory.Button(row.transform, "Choisir un logo…", CoPetroleL, CoPetrole, 40, UITheme.Role.Bouton);
        UIFactory.LE(pick.gameObject, flexW: 1);
        pick.onClick.AddListener(ChoisirLogo);
        var reset = UIFactory.Button(row.transform, "Logo CIPL par défaut", UITheme.Carte, UITheme.TexteSecondaire, 40, UITheme.Role.Action);
        UIFactory.Border(reset.gameObject); UIFactory.LE(reset.gameObject, prefW: 240, flexW: 0);
        reset.onClick.AddListener(() => { R.logoPath = ""; ReglageService.Save(); RefreshLogo(); });

        RefreshLogo();
    }

    void ChoisirLogo()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        var paths = SFB.StandaloneFileBrowser.OpenFilePanel(
            "Choisir un logo", "",
            new[] { new SFB.ExtensionFilter("Images", "png", "jpg", "jpeg") }, false);
        if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            try
            {
                string dossier = Path.Combine(SaveLocationService.GetSaveRoot(), "Logo");
                Directory.CreateDirectory(dossier);
                string dest = Path.Combine(dossier, "logo" + Path.GetExtension(paths[0]));
                File.Copy(paths[0], dest, true);
                R.logoPath = dest;
                ReglageService.Save();
                RefreshLogo();
            }
            catch (Exception e) { ConfirmDialog.Erreur("Logo : " + e.Message); }
        }
#endif
    }

    void RefreshLogo()
    {
        string path = !string.IsNullOrEmpty(R.logoPath) && File.Exists(R.logoPath)
            ? R.logoPath : Path.Combine(Application.streamingAssetsPath, "logo_cipl.png");
        var tex = PhotoService.Charger(path);
        if (_logoPreview != null)
        {
            _logoPreview.texture = tex;
            _logoPreview.color = tex != null ? Color.white : new Color(1, 1, 1, 0);
        }
        if (_logoAspect != null && tex != null && tex.height > 0)
            _logoAspect.aspectRatio = (float)tex.width / tex.height;
        if (_logoInfo != null)
            _logoInfo.text = !string.IsNullOrEmpty(R.logoPath)
                ? "Logo personnalisé : " + Path.GetFileName(R.logoPath)
                : "Logo CIPL par défaut.";
    }

    // ── Section RIB ────────────────────────────────────────────────────────────

    void BuildRibs(Transform parent)
    {
        var body = UIFactory.Section(parent, "RIB (comptes émetteurs)", CoBleu, CoBleuL);
        var listGO = UIFactory.VBox(body.transform, 8, 0, 0, 0, 0, "RibList");
        _ribList = listGO.transform;
        var add = UIFactory.Button(body.transform, "+  Ajouter un RIB", CoBleuL, CoBleu, 40, UITheme.Role.Bouton);
        add.onClick.AddListener(() => OpenRibForm(null));
        RebuildRibList();
    }

    void RebuildRibList()
    {
        foreach (Transform c in _ribList) Destroy(c.gameObject);
        if (R.ribs.Count == 0)
            UIFactory.Text(_ribList, "Aucun RIB. Cliquez sur « Ajouter un RIB ».", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        foreach (var rib in R.ribs)
        {
            var row = UIFactory.HBox(_ribList, 8, false, "RibRow");
            var rowBg = row.gameObject.AddComponent<Image>();
            rowBg.color = UITheme.Fond; rowBg.sprite = UIFactory.Rounded(); rowBg.type = Image.Type.Sliced;
            UIFactory.LE(row.gameObject, minH: 52);
            var pad = UIFactory.VBox(row.transform, 1, 10, 10, 6, 6, "info");
            UIFactory.LE(pad.gameObject, flexW: 1);
            string ribNom = string.IsNullOrWhiteSpace(rib.name) ? "(RIB sans nom)" : rib.name;
            UIFactory.Text(pad.transform, ribNom, UITheme.Role.Libelle, UITheme.TextePrincipal, true);
            UIFactory.Text(pad.transform, string.IsNullOrEmpty(rib.iban) ? "—" : rib.iban, UITheme.Role.Donnee, UITheme.TexteSecondaire);
            var rib2 = rib;
            var edit = UIFactory.Button(row.transform, "Modifier", UITheme.Carte, UITheme.TextePrincipal, 34, UITheme.Role.Action);
            UIFactory.Border(edit.gameObject); UIFactory.LE(edit.gameObject, prefW: 90, flexW: 0);
            edit.onClick.AddListener(() => OpenRibForm(rib2));
            var del = UIFactory.Button(row.transform, "Supprimer", UITheme.AlerteClair, UITheme.AlerteTexte, 34, UITheme.Role.Action);
            UIFactory.LE(del.gameObject, prefW: 100, flexW: 0);
            del.onClick.AddListener(() => ConfirmDialog.Instance?.Show(
                "Voulez-vous supprimer ce RIB ?", rib2.name,
                () => { R.ribs.Remove(rib2); ReglageService.Save(); RebuildRibList(); }, "Supprimer"));
        }
    }

    void OpenRibForm(RibData existing)
    {
        var rib = existing ?? new RibData();
        Modal(existing == null ? "Nouveau RIB" : "Modifier le RIB", body =>
        {
            var fName = ModalField(body, "Nom du RIB (titre affiché dans la liste)", rib.name, "BNP Mazamet");
            var fTit = ModalField(body, "Titulaire", rib.titulaire, "Groupe CIPL");
            var fDom = ModalField(body, "Domiciliation", rib.domiciliation, "BNP PARIBAS Mazamet (00747)");
            var fRib = ModalField(body, "RIB", rib.rib, "30004 00747 00021009833 38");
            var fIban = ModalField(body, "IBAN", rib.iban, "FR76 3000 4007 4700 0210 0983 338");
            var fBic = ModalField(body, "BIC", rib.bic, "BNPAFRPPALB");
            return () =>
            {
                rib.name = fName.text; rib.titulaire = fTit.text; rib.domiciliation = fDom.text;
                rib.rib = fRib.text; rib.iban = fIban.text; rib.bic = fBic.text;
                if (existing == null) R.ribs.Add(rib);
                ReglageService.Save(); RebuildRibList();
            };
        });
    }

    // ── Section Entêtes ────────────────────────────────────────────────────────

    void BuildEntetes(Transform parent)
    {
        var body = UIFactory.Section(parent, "Entêtes (modèles de texte)", CoAmbre, CoAmbreL);
        var listGO = UIFactory.VBox(body.transform, 8, 0, 0, 0, 0, "EnteteList");
        _enteteList = listGO.transform;
        var add = UIFactory.Button(body.transform, "+  Ajouter un entête", CoAmbreL, CoAmbre, 40, UITheme.Role.Bouton);
        add.onClick.AddListener(() => OpenEnteteForm(null));
        RebuildEnteteList();
    }

    void RebuildEnteteList()
    {
        foreach (Transform c in _enteteList) Destroy(c.gameObject);
        if (R.entetes.Count == 0)
            UIFactory.Text(_enteteList, "Aucun entête. Cliquez sur « Ajouter un entête ».", UITheme.Role.Aide, UITheme.TexteSecondaire);
        foreach (var ent in R.entetes)
        {
            var row = UIFactory.HBox(_enteteList, 8, false, "EnteteRow");
            var rowBg = row.gameObject.AddComponent<Image>();
            rowBg.color = UITheme.Fond; rowBg.sprite = UIFactory.Rounded(); rowBg.type = Image.Type.Sliced;
            UIFactory.LE(row.gameObject, minH: 52);
            var pad = UIFactory.VBox(row.transform, 1, 10, 10, 6, 6, "info");
            UIFactory.LE(pad.gameObject, flexW: 1);
            string entNom = string.IsNullOrWhiteSpace(ent.nom) ? "(entête sans nom)" : ent.nom;
            UIFactory.Text(pad.transform, entNom, UITheme.Role.Libelle, UITheme.TextePrincipal, true);
            var preview = (ent.texte ?? "").Replace("\n", " ");
            if (preview.Length > 70) preview = preview.Substring(0, 70) + "…";
            UIFactory.Text(pad.transform, preview, UITheme.Role.Donnee, UITheme.TexteSecondaire);
            var ent2 = ent;
            var edit = UIFactory.Button(row.transform, "Modifier", UITheme.Carte, UITheme.TextePrincipal, 34, UITheme.Role.Action);
            UIFactory.Border(edit.gameObject); UIFactory.LE(edit.gameObject, prefW: 90, flexW: 0);
            edit.onClick.AddListener(() => OpenEnteteForm(ent2));
            var del = UIFactory.Button(row.transform, "Supprimer", UITheme.AlerteClair, UITheme.AlerteTexte, 34, UITheme.Role.Action);
            UIFactory.LE(del.gameObject, prefW: 100, flexW: 0);
            del.onClick.AddListener(() => ConfirmDialog.Instance?.Show(
                "Voulez-vous supprimer cet entête ?", ent2.nom,
                () => { R.entetes.Remove(ent2); ReglageService.Save(); RebuildEnteteList(); }, "Supprimer"));
        }
    }

    void OpenEnteteForm(EnteteData existing)
    {
        var ent = existing ?? new EnteteData();
        Modal(existing == null ? "Nouvel entête" : "Modifier l'entête", body =>
        {
            var fNom = ModalField(body, "Nom du modèle", ent.nom, "Bail trimestriel…");
            UIFactory.Text(body.transform, "Texte  —  tape « / » pour insérer une variable", UITheme.Role.Libelle, UITheme.TexteSecondaire);
            var fTexte = UIFactory.Input(body.transform, "Conformément au bail…  (tape / pour une variable)", 120, true);
            fTexte.text = ent.texte ?? "";
            SlashAutocomplete.Attach(fTexte);
            return () =>
            {
                ent.nom = fNom.text; ent.texte = fTexte.text;
                if (existing == null) R.entetes.Add(ent);
                ReglageService.Save(); RebuildEnteteList();
            };
        });
    }

    // ── Section Listes de charges ──────────────────────────────────────────────

    Transform _listesList;

    void BuildListesCharges(Transform parent)
    {
        var body = UIFactory.Section(parent, "Listes de charges", CoTaupe, CoTaupeL);
        UIFactory.Text(body.transform,
            "Par défaut, toutes les charges forment une seule liste, régularisée à une date. "
            + "Une liste ajoutée ici (ex. « Taxe foncière ») est régularisée à part : on la choisit "
            + "en créant la charge, et chaque locataire à provision a pour elle sa propre date et sa propre provision.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        var listGO = UIFactory.VBox(body.transform, 8, 0, 0, 0, 0, "ListesChargesList");
        _listesList = listGO.transform;
        var add = UIFactory.Button(body.transform, "+  Ajouter une liste de charges", CoTaupeL, CoTaupe, 40, UITheme.Role.Bouton);
        add.onClick.AddListener(() => OpenListeForm(null));
        RebuildListesCharges();
    }

    void RebuildListesCharges()
    {
        foreach (Transform c in _listesList) Destroy(c.gameObject);

        // Liste générale : toujours présente — renommable, jamais supprimable.
        LigneListe(ListesCharges.NomGenerale + "  (liste par défaut)",
            () => Modal("Renommer la liste par défaut", body =>
            {
                var fNom = ModalField(body, "Nom de la liste", ListesCharges.NomGenerale, ListesCharges.NomGeneraleDefaut);
                return () =>
                {
                    R.nomListeGenerale = string.IsNullOrWhiteSpace(fNom.text) ? ListesCharges.NomGeneraleDefaut : fNom.text.Trim();
                    ReglageService.Save(); RebuildListesCharges();
                };
            }),
            null);

        foreach (var l in R.listesCharges)
        {
            var l2 = l;
            LigneListe(string.IsNullOrWhiteSpace(l.nom) ? "(sans nom)" : l.nom,
                () => OpenListeForm(l2),
                () => SupprimerListe(l2));
        }
    }

    /// Refusée tant qu'un locataire à provision doit une charge de cette liste ; sinon
    /// avertissement, puis ses charges basculent dans la générale (l'historique reste).
    void SupprimerListe(ListeCharges liste)
    {
        if (ConfirmDialog.Instance == null) return;
        var bps = (BatimentManager.Instance?.BatimentPrefab ?? new List<BatimentPrefab>()).Where(bp => bp != null).ToList();
        var bats = bps.Select(bp => bp.getBatiment()).Where(b => b != null).ToList();

        var debiteurs = ListesCharges.Debiteurs(bats, liste.id);
        if (debiteurs.Count > 0)
        {
            ConfirmDialog.Instance.Show($"Impossible de supprimer « {liste.nom} »",
                "Ces locataires doivent encore des charges de cette liste :\n"
                + string.Join("\n", debiteurs.Take(8).Select(d => $"• {d.loc.Name} ({d.bat.Name}) : {string.Join(", ", d.dues.Select(c => c.nom))}"))
                + (debiteurs.Count > 8 ? "\n• …" : "")
                + "\n\nFaites la régularisation de cette liste et attendez son paiement avant de la supprimer.",
                () => { }, "Compris");
            return;
        }

        int nb = bats.Sum(b => b.charges?.Count(c => c != null && c.listeId == liste.id) ?? 0);
        ConfirmDialog.Instance.Show($"Supprimer la liste « {liste.nom} » ?",
            (nb > 0 ? $"Ses {nb} charge(s) passent dans « {ListesCharges.NomGenerale} » : elles restent dans l'historique des charges.\n\n"
                    : "Aucune charge n'est rangée dans cette liste.\n\n")
            + "Les dates et provisions saisies pour elle chez les locataires sont retirées.",
            () =>
            {
                foreach (var b in bats)
                {
                    ListesCharges.BasculerVersGenerale(b, liste.id);
                    BatimentManager.Instance.SaveBatiment(b);
                }
                R.listesCharges.Remove(liste);
                ReglageService.Save();
                RebuildListesCharges();
            }, "Supprimer");
    }

    /// Une ligne de la liste : nom, « Renommer », et « Supprimer » si `supprimer`
    /// n'est pas null. Marges intérieures, et boutons à la largeur de leur libellé :
    /// à largeur fixe, « Renommer » passait sur deux lignes à cette taille de texte.
    void LigneListe(string nom, Action renommer, Action supprimer)
    {
        var row = UIFactory.HBox(_listesList, 8, false, "ListeRow");
        row.padding = new RectOffset(14, 8, 6, 6);
        var bg = row.gameObject.AddComponent<Image>();
        bg.color = UITheme.Fond; bg.sprite = UIFactory.Rounded(); bg.type = Image.Type.Sliced;
        UIFactory.LE(row.gameObject, minH: 46);

        var t = UIFactory.Text(row.transform, nom, UITheme.Role.Libelle, UITheme.TextePrincipal, true);
        UIFactory.LE(t.gameObject, flexW: 1);

        var edit = UIFactory.Button(row.transform, "Renommer", UITheme.Carte, UITheme.TextePrincipal, 34, UITheme.Role.Action);
        UIFactory.Border(edit.gameObject);
        UIFactory.LargeurDuTexte(edit);
        edit.onClick.AddListener(() => renommer());

        if (supprimer == null) return;
        var del = UIFactory.Button(row.transform, "Supprimer", UITheme.AlerteClair, UITheme.AlerteTexte, 34, UITheme.Role.Action);
        UIFactory.LargeurDuTexte(del);
        del.onClick.AddListener(() => supprimer());
    }

    void OpenListeForm(ListeCharges existing)
    {
        var liste = existing ?? new ListeCharges();
        Modal(existing == null ? "Nouvelle liste de charges" : "Renommer la liste", body =>
        {
            var fNom = ModalField(body, "Nom de la liste", liste.nom, "Taxe foncière…");
            return () =>
            {
                if (string.IsNullOrWhiteSpace(fNom.text)) return;
                liste.nom = fNom.text.Trim();
                if (existing == null) R.listesCharges.Add(liste);
                ReglageService.Save(); RebuildListesCharges();
            };
        });
    }

    // ── Section Textes fixes ───────────────────────────────────────────────────

    void BuildTextes(Transform parent)
    {
        var body = UIFactory.Section(parent, "Textes fixes", CoTaupe, CoTaupeL);
        UIFactory.Text(body.transform, "Lieu d'émission (imprimé « …, le 29 septembre 2026 » sur tous les documents)",
            UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _lieuEmission = UIFactory.Input(body.transform, FacturePdfService.LieuDefaut);
        UIFactory.Text(body.transform, "Phrase de retard / pénalités", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _phraseRetard = UIFactory.Input(body.transform, "En cas de retard…", 90, true);
        UIFactory.Text(body.transform, "Bas de page (mentions société)", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _basDePage = UIFactory.Input(body.transform, "SAS au capital…", 90, true);

        // Phrases de BASE : une facture peut les remplacer pour elle seule, dans son
        // panneau. Changer la base ici touche toutes celles qui ne l'ont pas fait.
        UIFactory.Text(body.transform, "Mention « TVA payée sur les débits »", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _tvaDebits = UIFactory.Input(body.transform, FacturePdfService.TvaDebitDefaut, 46, true);
        SlashAutocomplete.Attach(_tvaDebits);
        UIFactory.Text(body.transform, "Mention « TVA payée sur les encaissements »", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _tvaEncaissements = UIFactory.Input(body.transform, FacturePdfService.TvaEncaissementsDefaut, 46, true);
        SlashAutocomplete.Attach(_tvaEncaissements);

        // La phrase de retard et le bas de page s'impriment à l'identique sur les
        // quatre types de facture. Les phrases de l'explication du dépôt, elles, se
        // règlent dans le panneau de la facture de dépôt — elles appartiennent au
        // document, pas à l'entreprise.
        UIFactory.Text(body.transform,
            "Les mentions TVA sont les phrases de base : chaque facture choisit la sienne "
            + "et peut la reformuler pour elle seule. Les textes propres à un type de facture "
            + "(explication du dépôt de garantie…) se modifient dans le panneau de la facture concernée.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
    }

    // ── Section Affichage ──────────────────────────────────────────────────────

    // Les boutons de taille sont reconstruits à chaque changement pour que le
    // palier courant reste visible : c'est le seul retour que l'écran donne.
    Transform _taillesRow;

    void BuildAffichage(Transform parent)
    {
        var body = UIFactory.Section(parent, "Taille de l'affichage", CoPetrole, CoPetroleL);
        UIFactory.Text(body.transform,
            "Agrandit ou réduit toute l'interface : texte, champs et boutons ensemble.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);

        _taillesRow = UIFactory.HBox(body.transform, 8, false, "Tailles").transform;
        RefreshTailles();
    }

    void RefreshTailles()
    {
        if (_taillesRow == null) return;
        foreach (Transform t in _taillesRow) Destroy(t.gameObject);

        float courant = UIZoom.Facteur;
        foreach (float p in UIZoom.Paliers)
        {
            bool actif = Mathf.Abs(p - courant) < 0.01f;
            var b = UIFactory.Button(_taillesRow,
                Mathf.RoundToInt(p * 100f) + " %",
                actif ? CoPetrole : UITheme.Carte,
                actif ? Color.white : UITheme.TextePrincipal,
                40, UITheme.Role.Bouton);
            if (!actif) UIFactory.Border(b.gameObject);
            UIFactory.LE(b.gameObject, flexW: 1);

            float valeur = p;   // capture par copie, sinon tous les boutons posent le dernier palier
            b.onClick.AddListener(() => { UIZoom.Facteur = valeur; RefreshTailles(); });
        }
    }

    // ── Section Sauvegarde ─────────────────────────────────────────────────────

    void BuildSauvegarde(Transform parent)
    {
        var body = UIFactory.Section(parent, "Emplacement de sauvegarde", CoPetrole, CoPetroleL);
        _savePath = UIFactory.Text(body.transform, "", UITheme.Role.Donnee, UITheme.TexteSecondaire);

        var row = UIFactory.HBox(body.transform, 8, false, "SaveBtns");
        var change = UIFactory.Button(row.transform, "Changer l'emplacement", CoPetroleL, CoPetrole, 40, UITheme.Role.Bouton);
        UIFactory.LE(change.gameObject, flexW: 1);
        change.onClick.AddListener(() => FermetureGuard.ConfirmerPerteSaisies(
            "Changer d'emplacement", () => { if (SaveIO.ChangeLocation()) LoadIntoUI(); }));

        var open = UIFactory.Button(row.transform, "Ouvrir le dossier", UITheme.Carte, UITheme.TextePrincipal, 40, UITheme.Role.Action);
        UIFactory.Border(open.gameObject); UIFactory.LE(open.gameObject, prefW: 160, flexW: 0);
        open.onClick.AddListener(SaveIO.OpenFolder);

        var reset = UIFactory.Button(body.transform, "Réinitialiser (emplacement par défaut)", UITheme.Carte, UITheme.TexteSecondaire, 36, UITheme.Role.Action);
        UIFactory.Border(reset.gameObject);
        reset.onClick.AddListener(() => FermetureGuard.ConfirmerPerteSaisies(
            "Revenir à l'emplacement par défaut", () => { SaveIO.ResetToDefault(); LoadIntoUI(); }));
    }

    // ── Chargement / sauvegarde des valeurs ────────────────────────────────────

    void LoadIntoUI()
    {
        _entrepriseNom.text = R.entrepriseNom ?? "";
        _apiKey.text = ReglageService.GetApiKey();
        if (_mapboxToken != null) _mapboxToken.text = ReglageService.GetMapboxToken();
        _modePennylane.isOn = R.modeEnvoi == ModeEnvoi.Pennylane;
        // La carte SMTP reste visible quel que soit le mode : les relances d'impayé
        // partent par email même quand les factures passent par Pennylane.
        _smtpHost.text = R.smtp.host;
        _smtpPort.text = R.smtp.port.ToString();
        _smtpFromEmail.text = R.smtp.fromEmail;
        _smtpFromName.text = R.smtp.fromName;
        _smtpUser.text = R.smtp.username;
        _smtpPwd.text = ReglageService.GetSmtpPassword();
        _lieuEmission.text = FacturePdfService.Texte(R.lieuEmission, FacturePdfService.LieuDefaut);
        _phraseRetard.text = R.phraseRetard;
        _basDePage.text = R.basDePage;
        _tvaDebits.text = MentionTva.Base(R, MentionTva.Debits);
        _tvaEncaissements.text = MentionTva.Base(R, MentionTva.Encaissements);
        if (_logoPreview != null) RefreshLogo();
        RebuildRibList();
        RebuildEnteteList();
        RebuildListesCharges();
        if (_savePath != null)
        {
            int nb = SaveIO.CountBatiments();
            string type = SaveLocationService.IsCustom() ? "personnalisé" : "défaut";
            _savePath.text = $"Emplacement actuel ({type}) · {nb} bâtiment(s) :\n{SaveLocationService.GetSaveRoot()}";
        }
    }

    void SaveFromUI()
    {
        R.entrepriseNom = _entrepriseNom.text.Trim();
        EntrepriseService.Register(R.entrepriseNom, SaveLocationService.GetSaveRoot());
        ReglageService.SetApiKey(_apiKey.text.Trim());
        if (_mapboxToken != null) ReglageService.SetMapboxToken(_mapboxToken.text.Trim());
        R.modeEnvoi = _modePennylane.isOn ? ModeEnvoi.Pennylane : ModeEnvoi.Email;
        R.smtp.host = _smtpHost.text.Trim();
        int.TryParse(_smtpPort.text.Trim(), out int port); R.smtp.port = port == 0 ? 587 : port;
        R.smtp.fromEmail = _smtpFromEmail.text.Trim();
        R.smtp.fromName = _smtpFromName.text.Trim();
        R.smtp.username = _smtpUser.text.Trim();
        ReglageService.SetSmtpPassword(_smtpPwd.text);
        R.lieuEmission = _lieuEmission.text.Trim();
        R.phraseRetard = _phraseRetard.text;
        R.basDePage = _basDePage.text;
        R.mentionTvaDebits = _tvaDebits.text;
        R.mentionTvaEncaissements = _tvaEncaissements.text;
        ReglageService.Save();
        UndoToast.Instance?.ShowInfo("Réglages enregistrés");
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // ── Petit système de modal générique ───────────────────────────────────────

    /// Ouvre une modale centrée. `builder` reçoit le corps (VBox) et renvoie
    /// l'action à exécuter sur « Valider ».
    void Modal(string titre, Func<VerticalLayoutGroup, Action> builder)
    {
        var scrim = UIFactory.Rect("ModalScrim", transform.parent);
        UIFactory.Stretch(scrim);
        var scrimImg = scrim.gameObject.AddComponent<Image>();
        scrimImg.color = new Color(0, 0, 0, 0.45f);
        scrim.SetAsLastSibling();

        var cardImg = UIFactory.Panel("ModalCard", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(600, 100);
        var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10; v.padding = new RectOffset(18, 18, 16, 16);
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperLeft;
        var vcsf = cardImg.gameObject.AddComponent<ContentSizeFitter>();
        vcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        vcsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        UIFactory.Text(v.transform, titre, UITheme.Role.Section, UITheme.TextePrincipal, true);
        var onValidate = builder(v);

        var actions = UIFactory.HBox(v.transform, 10, false, "Actions");
        UIFactory.LE(actions.gameObject, minH: 46, prefH: 46);
        var cancel = UIFactory.Button(actions.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 44, UITheme.Role.Bouton);
        UIFactory.Border(cancel.gameObject); UIFactory.LE(cancel.gameObject, flexW: 1);
        cancel.onClick.AddListener(() => Destroy(scrim.gameObject));
        var ok = UIFactory.Button(actions.transform, "Valider", UITheme.Primaire, Color.white, 44, UITheme.Role.Bouton);
        UIFactory.LE(ok.gameObject, flexW: 1);
        ok.onClick.AddListener(() => { onValidate?.Invoke(); Destroy(scrim.gameObject); });
    }

    TMP_InputField ModalField(VerticalLayoutGroup body, string label, string value, string placeholder)
    {
        UIFactory.Text(body.transform, label, UITheme.Role.Libelle, UITheme.TexteSecondaire);
        var f = UIFactory.Input(body.transform, placeholder);
        f.text = value ?? "";
        return f;
    }
}
