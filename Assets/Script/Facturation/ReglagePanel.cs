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
    TMP_Text _reglePar;   // étape 9a : « Réglé par l'administrateur », à la place des champs d'admin
    readonly List<GameObject> _reserveAdmin = new List<GameObject>();

    /// Admin, ou hors cloud : Pennylane, mode d'envoi et SMTP se modifient ici (étape 9a).
    static bool Admin => !SessionCloud.Active || SessionCloud.Entreprise == null || SessionCloud.Entreprise.EstAdmin;

    /// Réglages relus sur le serveur pendant que la fenêtre est ouverte (refus d'enregistrement).
    public void Rafraichir() => LoadIntoUI();

    SectionUtilisateurs _utilisateurs;   // étape 9b
    SectionAbonnement _abonnement;       // étape 9c1
    SectionExport _export;               // étape 11

    // ── Onglets (étape 9c1, maquette validée le 19/10) ─────────────────────────
    public const string OEntreprise = "Entreprise", OFacturation = "Facturation", OUtilisateurs = "Utilisateurs",
                        OAbonnement = "Abonnement", ODocuments = "Documents", OIntegrations = "Intégrations";
    readonly Dictionary<string, GameObject> _pages = new Dictionary<string, GameObject>();
    Transform _barreOnglets;
    GameObject _footer;
    string _onglet = OEntreprise;

    /// Un utilisateur du cloud ne voit que Entreprise (lecture), Facturation et Documents
    /// (décision du 11/10) ; hors cloud, ni Utilisateurs ni Abonnement.
    public static string[] Onglets(bool cloud, bool admin) =>
        !cloud ? new[] { OEntreprise, OFacturation, ODocuments, OIntegrations }
      : admin ? new[] { OEntreprise, OFacturation, OUtilisateurs, OAbonnement, ODocuments, OIntegrations }
      : new[] { OEntreprise, OFacturation, ODocuments };

    /// Bandeau « Voir l'abonnement » : Réglages ouvert sur cet onglet.
    public static void OuvrirOnglet(string onglet)
    {
        OpenPanel();
        Instance?.Montrer(onglet);
    }

    public void Montrer(string onglet)
    {
        _onglet = onglet;
        ConstruireOnglets();
        // Retour du 23/10 : les licences changées dans Abonnement n'apparaissaient pas dans
        // Utilisateurs (lu à l'ouverture de Réglages seulement). Chaque onglet se relit à l'affichage.
        if (onglet == OUtilisateurs) _utilisateurs?.Rafraichir();
        else if (onglet == OAbonnement) _abonnement?.Rafraichir();
        else if (onglet == ODocuments) _export?.Rafraichir();
        if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;   // chaque onglet s'ouvre en haut
    }

    ScrollRect _scroll;

    void ConstruireOnglets()
    {
        var visibles = Onglets(SessionCloud.Active, Admin);
        if (!visibles.Contains(_onglet)) _onglet = visibles[0];
        foreach (Transform t in _barreOnglets) Destroy(t.gameObject);
        foreach (var o in visibles)
        {
            bool actif = o == _onglet;
            var b = UIFactory.Button(_barreOnglets, o, actif ? UITheme.Primaire : UITheme.Carte,
                                     actif ? Color.white : UITheme.TextePrincipal, 38, UITheme.Role.Bouton, actif);
            if (!actif) UIFactory.Border(b.gameObject);
            UIFactory.LargeurDuTexte(b);
            string nom = o;   // capture par copie
            b.onClick.AddListener(() => Montrer(nom));
        }
        foreach (var kv in _pages) kv.Value.SetActive(kv.Key == _onglet);
        _footer.SetActive(_onglet != OUtilisateurs && _onglet != OAbonnement);   // rien à y enregistrer
    }

    Transform Page(Transform content, string onglet)
    {
        var p = UIFactory.VBox(content, 16, 0, 0, 0, 0, onglet);
        _pages[onglet] = p.gameObject;
        return p.transform;
    }

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

        // Onglets (étape 9c1), construits à chaque ouverture : ils dépendent du rôle
        _barreOnglets = UIFactory.HBox(col.transform, 8, false, "Onglets").transform;
        UIFactory.LE(_barreOnglets.gameObject, minH: 44);

        // Scroll : une page par onglet
        var content = MakeScroll(col.transform);

        BuildEntreprise(Page(content, OEntreprise));

        var facturation = Page(content, OFacturation);
        BuildLogo(facturation);
        BuildRibs(facturation);
        BuildEntetes(facturation);
        BuildListesCharges(facturation);
        BuildTextes(facturation);

        _utilisateurs = new SectionUtilisateurs(this, Page(content, OUtilisateurs));   // étape 9b
        _abonnement = new SectionAbonnement(this, Page(content, OAbonnement));

        var documents = Page(content, ODocuments);   // ce qui se règle sur cet ordinateur
        BuildDocuments(documents);   // sur le cloud (étape 6b), à la place de l'emplacement de sauvegarde
        BuildSauvegarde(documents);
        _export = new SectionExport(documents);   // étape 11 : « Exporter toutes les données »
        BuildAffichage(documents);
        BuildCartes(documents);

        BuildConnexion(Page(content, OIntegrations));

        // Footer
        var footer = UIFactory.HBox(col.transform, 12, false, "Footer");
        _footer = footer.gameObject;
        UIFactory.LE(footer.gameObject, minH: 56);
        var save = UIFactory.Button(footer.transform, "Enregistrer", UITheme.Primaire, Color.white, 46, UITheme.Role.Bouton);
        UIFactory.LE(save.gameObject, flexW: 1);
        save.onClick.AddListener(SaveFromUI);
    }

    Transform MakeScroll(Transform parent)
    {
        var srGO = UIFactory.Rect("Scroll", parent);
        var sr = srGO.gameObject.AddComponent<ScrollRect>();
        _scroll = sr;
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

    // ── Onglet Entreprise : nom (lecture seule pour un utilisateur du cloud) ────

    void BuildEntreprise(Transform parent)
    {
        var body = UIFactory.Section(parent, "Entreprise", CoVert, CoVertL);
        UIFactory.Text(body.transform, "Nom de l'entreprise", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _entrepriseNom = UIFactory.Input(body.transform, "GROUPE CIPL");

        // Étape 9a : pour un utilisateur (non admin) sur le cloud, l'envoi des factures est
        // « Réglé par l'administrateur » (décision du 19/10) ; l'onglet Intégrations lui est caché.
        _reglePar = UIFactory.Text(body.transform, "", UITheme.Role.Aide, UITheme.TexteSecondaire);
        _reglePar.gameObject.SetActive(false);
    }

    // ── Cartes : token Mapbox, propre à chaque ordinateur ──────────────────────

    void BuildCartes(Transform parent)
    {
        var body = UIFactory.Section(parent, "Cartes", CoPetrole, CoPetroleL);
        // Token Mapbox : saisi ici plutôt que dans le champ public de TileLoader, qui
        // était sérialisé en clair dans Maps.prefab et donc commité dans le dépôt.
        UIFactory.Text(body.transform, "Token Mapbox (sur cet ordinateur)", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _mapboxToken = UIFactory.Input(body.transform, "Collez votre token Mapbox…");
        _mapboxToken.contentType = TMP_InputField.ContentType.Password;
    }

    // ── Onglet Intégrations : Pennylane, mode d'envoi, SMTP (admin) ────────────

    void BuildConnexion(Transform parent)
    {
        var body = UIFactory.Section(parent, "Connexion & envoi", CoVert, CoVertL);

        _reserveAdmin.Add(UIFactory.Text(body.transform, "Clé API Pennylane", UITheme.Role.Libelle, UITheme.TexteSecondaire).gameObject);
        _apiKey = UIFactory.Input(body.transform, "Collez votre clé API…");
        _apiKey.contentType = TMP_InputField.ContentType.Password;
        _reserveAdmin.Add(_apiKey.gameObject);

        _modePennylane = UIFactory.Toggle(body.transform, "Envoyer via Pennylane (e-facture Factur-X)", true);
        _reserveAdmin.Add(_modePennylane.gameObject);

        // Cette case ne gouverne QUE l'envoi des factures. Les réglages SMTP restent
        // visibles dans les deux modes : les relances d'impayé partent par email même
        // en Pennylane, donc les masquer les rendait inconfigurables.
        _reserveAdmin.Add(UIFactory.Text(body.transform,
            "Cette case choisit comment partent les FACTURES. Les relances d'impayé passent "
            + "par email dans tous les cas : les réglages SMTP ci-dessous servent donc toujours. "
            + "(Pennylane : loyers seulement pour l'instant, déposés sans être émis.)",
            UITheme.Role.Aide, UITheme.TexteSecondaire).gameObject);

        // Sous-carte SMTP (mode Email)
        var smtpBody = UIFactory.Card(body.transform);
        _smtpCard = smtpBody.gameObject; // la carte SMTP (le VLG est porté par la carte)
        _reserveAdmin.Add(_smtpCard);
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
            "Le logo apparaît en haut des factures. Sans logo, le haut de la facture reste vide.",
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
        // Retour du 25/10 : un client n'a pas à prendre le logo de CIPL ; « Retirer » = aucun logo.
        var reset = UIFactory.Button(row.transform, "Retirer le logo", UITheme.Carte, UITheme.TexteSecondaire, 40, UITheme.Role.Action);
        UIFactory.Border(reset.gameObject); UIFactory.LE(reset.gameObject, prefW: 240, flexW: 0);
        reset.onClick.AddListener(() => { R.logoPath = ""; R.sansLogo = true; ReglageService.Save(); RefreshLogo(); });
        _reserveAdmin.Add(row.gameObject);   // étape 9b : logo réservé à l'admin (décision du 11/10)

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
                R.logoPath = DossiersDonnees.VersRelatif(dest);   // relatif : juste sur chaque PC du Drive partagé (étape 6b)
                ReglageService.Save();
                RefreshLogo();
            }
            catch (Exception e) { ConfirmDialog.Erreur("Logo : " + e.Message); }
        }
#endif
    }

    void RefreshLogo()
    {
        string logo = DossiersDonnees.VersAbsolu(R.logoPath);
        bool perso = !string.IsNullOrEmpty(logo) && File.Exists(logo);
        string path = perso ? logo : Path.Combine(Application.streamingAssetsPath, "logo_cipl.png");
        var tex = perso || !R.sansLogo ? PhotoService.Charger(path) : null;
        if (_logoPreview != null)
        {
            _logoPreview.texture = tex;
            _logoPreview.color = tex != null ? Color.white : new Color(1, 1, 1, 0);
        }
        if (_logoAspect != null && tex != null && tex.height > 0)
            _logoAspect.aspectRatio = (float)tex.width / tex.height;
        if (_logoInfo != null)
            _logoInfo.text = !string.IsNullOrEmpty(R.logoPath) ? "Logo personnalisé : " + Path.GetFileName(R.logoPath)
                           : R.sansLogo ? "Aucun logo : cliquez sur « Choisir un logo… »."
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
        _reserveAdmin.Add(add.gameObject);   // étape 9b : RIB réservés à l'admin (décision du 11/10)
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
            if (!Admin) continue;   // étape 9b : lecture seule pour un utilisateur
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

    /// Retour du 25/10 : saisie contrôlée (RegleRib) ; un refus rouvre la fenêtre telle que remplie,
    /// la zone en cause en rouge. Un n° de RIB laissé vide se remplit d'après l'IBAN.
    void OpenRibForm(RibData existing, RibData saisie = null, string[] fautes = null)
    {
        var rib = existing ?? new RibData();
        var v = saisie ?? rib;
        Modal(existing == null ? "Nouveau RIB" : "Modifier le RIB", body =>
        {
            var fName = ModalField(body, "Nom du RIB (titre affiché dans la liste)", v.name, "BNP Mazamet");
            var fTit = ModalField(body, "Titulaire", v.titulaire, "Groupe CIPL");
            var fDom = ModalField(body, "Domiciliation", v.domiciliation, "BNP PARIBAS Mazamet (00747)");
            var fRib = ModalField(body, "RIB", v.rib, "30004 00747 00021009833 38");
            var fIban = ModalField(body, "IBAN", v.iban, "FR76 3000 4007 4700 0210 0983 338");
            var fBic = ModalField(body, "BIC", v.bic, "BNPAFRPPALB");
            var zones = new EcransCloud.Zones();
            zones.Ajouter(RegleRib.ChampTitulaire, fTit); zones.Ajouter(RegleRib.ChampRib, fRib);
            zones.Ajouter(RegleRib.ChampIban, fIban); zones.Ajouter(RegleRib.ChampBic, fBic);
            zones.Montrer(fautes);
            return () =>
            {
                var lu = new RibData { name = fName.text, titulaire = fTit.text, domiciliation = fDom.text, rib = fRib.text, iban = fIban.text, bic = fBic.text };
                if (string.IsNullOrWhiteSpace(lu.rib)) lu.rib = RegleRib.RibDepuisIban(lu.iban) ?? "";
                string refus = RegleRib.Refus(lu.titulaire, lu.rib, lu.iban, lu.bic, out var champs);
                if (refus != null) { ConfirmDialog.Erreur(refus, () => OpenRibForm(existing, lu, champs)); return; }
                rib.name = lu.name; rib.titulaire = lu.titulaire; rib.domiciliation = lu.domiciliation;
                rib.rib = lu.rib; rib.iban = lu.iban; rib.bic = lu.bic;
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
            // Sur le cloud : refusé si un bâtiment est en cours de modification (étape 4b).
            () => EcritureCloud.SiAucunBatimentPris(bats, () =>
            {
                foreach (var b in bats)
                {
                    ListesCharges.BasculerVersGenerale(b, liste.id);
                    BatimentManager.Instance.SaveBatiment(b);
                }
                R.listesCharges.Remove(liste);
                ReglageService.Save();
                RebuildListesCharges();
            }), "Supprimer");
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

        // Supprimer une liste touche tous les bâtiments : admin seulement (étape 9b, décision du 11/10).
        if (supprimer == null || !Admin) return;
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
        _sectionSauvegarde = body.transform.parent.gameObject;
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

    // ── Section Documents (étape 6b, maquette validée le 14/10) ───────────────

    Transform _docsBody;
    GameObject _sectionDocuments, _sectionSauvegarde;

    void BuildDocuments(Transform parent)
    {
        var body = UIFactory.Section(parent, "Documents", CoPetrole, CoPetroleL);
        _docsBody = body.transform;
        _sectionDocuments = body.transform.parent.gameObject;
    }

    void RefreshDocuments()
    {
        if (_docsBody == null) return;
        foreach (Transform c in _docsBody) Destroy(c.gameObject);
        var e = SessionCloud.Entreprise;
        if (e == null) return;
        bool admin = e.EstAdmin, drive = e.ModeDrive;

        // Rangement : choix de l'admin, en lecture pour les autres.
        UIFactory.Text(_docsBody, "Rangement des documents", UITheme.Role.Donnee, UITheme.TextePrincipal, true);
        var modes = UIFactory.HBox(_docsBody, 8, false, "Modes");
        PuceDoc(modes.transform, "Dans CIPL", !drive, admin && drive ? () => Agir(DemarrageCloud.PasserEnCipl) : (Action)null);
        PuceDoc(modes.transform, "Drive partagé", drive, admin && !drive ? () => Agir(DemarrageCloud.PasserEnDrive) : (Action)null);
        UIFactory.LE(UIFactory.Rect("Espace", modes.transform).gameObject, flexW: 1);
        UIFactory.Text(_docsBody, drive
            ? "Les documents sont dans un dossier OneDrive, Google Drive ou SharePoint que vous partagez déjà. CIPL y écrit, le Drive les recopie chez chacun. Le cloud de CIPL en garde la copie de référence."
            : "Chaque ordinateur reçoit sa copie des documents depuis le cloud de CIPL.", UITheme.Role.Aide, UITheme.TexteSecondaire);
        if (drive && admin) BoutonDoc(UIFactory.HBox(_docsBody, 8, false, "Partage").transform, "Changer le dossier partagé…", () => Agir(DemarrageCloud.PasserEnDrive));
        if (!admin) UIFactory.Text(_docsBody, "Seul un administrateur peut changer le rangement.", UITheme.Role.Aide, UITheme.TexteSecondaire);

        // Sur cet ordinateur.
        UIFactory.Text(_docsBody, "Sur cet ordinateur", UITheme.Role.Donnee, UITheme.TextePrincipal, true);
        string type = DemarrageCloud.TypeDossier(e.id);
        bool horsDrive = drive && type == DossierEntreprise.HorsDrive;
        string etat = !drive ? (DossierEntreprise.Meme(SaveLocationService.GetSaveRoot(), DemarrageCloud.DossierLocal(e)) ? "Dossier de CIPL" : "Choisi à la main")
                    : type == DossierEntreprise.DriveAuto ? "Trouvé automatiquement"
                    : type == DossierEntreprise.DriveMain ? "Choisi à la main"
                    : "Hors du Drive : ce PC reçoit lui-même les documents";
        var ligneEtat = UIFactory.HBox(_docsBody, 8, false, "Etat");
        ObjectifsTableau.Pastille(ligneEtat.transform, etat,
            horsDrive ? new Color32(0xFA, 0xEE, 0xDA, 0xFF) : new Color32(0xEA, 0xF3, 0xDE, 0xFF),
            horsDrive ? new Color32(0x63, 0x38, 0x06, 0xFF) : new Color32(0x27, 0x50, 0x0A, 0xFF));
        UIFactory.LE(UIFactory.Rect("Espace", ligneEtat.transform).gameObject, flexW: 1);
        UIFactory.Text(_docsBody, SaveLocationService.GetSaveRoot(), UITheme.Role.Aide, UITheme.TexteSecondaire);
        if (drive) UIFactory.Text(_docsBody, "Chaque ordinateur range le Drive à sa façon : CIPL retrouve ce dossier tout seul.",
                                  UITheme.Role.Aide, UITheme.TexteSecondaire);
        var boutons = UIFactory.HBox(_docsBody, 8, false, "Boutons");
        if (drive)
        {
            BoutonDoc(boutons.transform, "Rechercher à nouveau", () => Agir(DemarrageCloud.RechercherDossier));
            BoutonDoc(boutons.transform, "Choisir à la main…", () => Agir(DemarrageCloud.ChoisirDossierPartage));
        }
        else BoutonDoc(boutons.transform, "Changer de dossier…", () => Agir(DemarrageCloud.ChangerDossierLocal));
        BoutonDoc(boutons.transform, "Ouvrir le dossier", SaveIO.OpenFolder);
        UIFactory.LE(UIFactory.Rect("Espace", boutons.transform).gameObject, flexW: 1);
    }

    /// Choix du mode : pleine couleur s'il est retenu ; cliquable seulement pour l'admin.
    void PuceDoc(Transform parent, string texte, bool choisi, Action clic)
    {
        var b = UIFactory.Button(parent, texte, choisi ? CoPetrole : UITheme.Carte, choisi ? Color.white : UITheme.TextePrincipal,
                                 34, UITheme.Role.Action, choisi);
        if (!choisi) UIFactory.Border(b.gameObject);
        UIFactory.LargeurDuTexte(b);
        if (clic != null) b.onClick.AddListener(() => clic());
    }

    void BoutonDoc(Transform parent, string texte, Action clic)
    {
        var b = UIFactory.Button(parent, texte, UITheme.Carte, UITheme.TextePrincipal, 38, UITheme.Role.Action, false);
        UIFactory.Border(b.gameObject);
        UIFactory.LargeurDuTexte(b);
        b.onClick.AddListener(() => clic());
    }

    /// Un changement de dossier relit l'entreprise : Réglages se ferme, ses valeurs venant de l'ancien dossier.
    void Agir(Action action) =>
        FermetureGuard.ConfirmerPerteSaisies("Changer le dossier des documents", () => { Close(); action(); });

    // ── Chargement / sauvegarde des valeurs ────────────────────────────────────

    void LoadIntoUI()
    {
        // Sur le cloud, la section Documents remplace l'emplacement de sauvegarde (étape 6b).
        if (_sectionDocuments != null) _sectionDocuments.SetActive(SessionCloud.Active);
        if (_sectionSauvegarde != null) _sectionSauvegarde.SetActive(!SessionCloud.Active);
        RefreshDocuments();
        _utilisateurs?.Rafraichir();
        _abonnement?.Rafraichir();
        _export?.Rafraichir();
        ConstruireOnglets();
        bool admin = Admin;
        foreach (var go in _reserveAdmin) if (go != null) go.SetActive(admin);
        _entrepriseNom.interactable = admin;   // étape 9c1 : onglet Entreprise en lecture pour un utilisateur
        _reglePar.gameObject.SetActive(!admin);
        _reglePar.text = "Envoi des factures : " + (R.modeEnvoi == ModeEnvoi.Pennylane ? "via Pennylane"
                         : $"par e-mail ({(string.IsNullOrEmpty(R.smtp.fromEmail) ? "adresse non réglée" : R.smtp.fromEmail)})")
                         + ". Réglé par l'administrateur.";
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
        if (_mapboxToken != null) ReglageService.SetMapboxToken(_mapboxToken.text.Trim());
        if (Admin)   // étape 9a : envoi et secrets réglés par l'admin seulement
        {
            ReglageService.SetApiKey(_apiKey.text.Trim());
            R.modeEnvoi = _modePennylane.isOn ? ModeEnvoi.Pennylane : ModeEnvoi.Email;
            R.smtp.host = _smtpHost.text.Trim();
            int.TryParse(_smtpPort.text.Trim(), out int port); R.smtp.port = port == 0 ? 587 : port;
            R.smtp.fromEmail = _smtpFromEmail.text.Trim();
            R.smtp.fromName = _smtpFromName.text.Trim();
            R.smtp.username = _smtpUser.text.Trim();
            ReglageService.SetSmtpPassword(_smtpPwd.text);
        }
        R.lieuEmission = _lieuEmission.text.Trim();
        R.phraseRetard = _phraseRetard.text;
        R.basDePage = _basDePage.text;
        R.mentionTvaDebits = _tvaDebits.text;
        R.mentionTvaEncaissements = _tvaEncaissements.text;
        if (ReglageService.Save()) UndoToast.Instance?.ShowInfo("Réglages enregistrés");   // refusé : le motif est déjà affiché
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // ── Petit système de modal générique ───────────────────────────────────────

    /// Ouvre une modale centrée. `builder` reçoit le corps (VBox) et renvoie
    /// l'action à exécuter sur « Valider ».
    internal void Modal(string titre, Func<VerticalLayoutGroup, Action> builder, string valider = "Valider")
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
        var ok = UIFactory.Button(actions.transform, valider, UITheme.Primaire, Color.white, 44, UITheme.Role.Bouton);
        UIFactory.LE(ok.gameObject, flexW: 1);
        ok.onClick.AddListener(() => { onValidate?.Invoke(); Destroy(scrim.gameObject); });
    }

    internal TMP_InputField ModalField(VerticalLayoutGroup body, string label, string value, string placeholder)
    {
        UIFactory.Text(body.transform, label, UITheme.Role.Libelle, UITheme.TexteSecondaire);
        var f = UIFactory.Input(body.transform, placeholder);
        f.text = value ?? "";
        return f;
    }
}
