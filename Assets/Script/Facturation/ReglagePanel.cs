using System;
using System.Collections;
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
    TMP_InputField _apiKey, _smtpHost, _smtpPort, _smtpFromEmail, _smtpFromName, _smtpPwd;
    TMP_InputField _phraseRetard, _basDePage;
    Toggle _modePennylane;
    GameObject _smtpCard;
    Transform _ribList, _enteteList;
    TMP_Text _savePath;

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
        var back = UIFactory.Button(header.transform, "←  Retour", UITheme.Carte, UITheme.TextePrincipal, 42, 18);
        UIFactory.Border(back.gameObject);
        UIFactory.LE(back.gameObject, prefW: 140, flexW: 0);
        back.onClick.AddListener(Close);
        var title = UIFactory.Text(header.transform, "Réglage", 26, UITheme.TextePrincipal, true);
        UIFactory.LE(title.gameObject, flexW: 1);

        // Scroll
        var content = MakeScroll(col.transform);

        BuildConnexion(content);
        BuildRibs(content);
        BuildEntetes(content);
        BuildTextes(content);
        BuildSauvegarde(content);

        // Footer
        var footer = UIFactory.HBox(col.transform, 12, false, "Footer");
        UIFactory.LE(footer.gameObject, minH: 56);
        var save = UIFactory.Button(footer.transform, "Enregistrer", UITheme.Primaire, Color.white, 46, 20);
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

        UIFactory.Text(body.transform, "Clé API Pennylane", 17, UITheme.TexteSecondaire);
        _apiKey = UIFactory.Input(body.transform, "Collez votre clé API…");
        _apiKey.contentType = TMP_InputField.ContentType.Password;

        _modePennylane = UIFactory.Toggle(body.transform, "Envoyer via Pennylane (e-facture Factur-X)", true);
        _modePennylane.onValueChanged.AddListener(on =>
        {
            if (_smtpCard != null) _smtpCard.SetActive(!on);
        });

        // Sous-carte SMTP (mode Email)
        var smtpBody = UIFactory.Card(body.transform);
        _smtpCard = smtpBody.gameObject; // la carte SMTP (le VLG est porté par la carte)
        UIFactory.Text(smtpBody.transform, "Envoi par email direct (SMTP)", 19, UITheme.TextePrincipal, true);
        UIFactory.Text(smtpBody.transform,
            "Le PDF est envoyé au locataire à l'adresse de la facture. Outlook / Microsoft 365 : "
            + "smtp.office365.com : 587. ⚠ SMTP AUTH souvent à activer côté admin M365.",
            15, UITheme.TexteSecondaire);

        _smtpHost = LabeledInput(smtpBody.transform, "Serveur SMTP", "smtp.office365.com");
        _smtpPort = LabeledInput(smtpBody.transform, "Port", "587");
        _smtpPort.contentType = TMP_InputField.ContentType.IntegerNumber;
        _smtpFromEmail = LabeledInput(smtpBody.transform, "Adresse d'expédition", "contact@cipl.fr");
        _smtpFromName = LabeledInput(smtpBody.transform, "Nom d'expédition", "GROUPE CIPL");
        _smtpPwd = LabeledInput(smtpBody.transform, "Mot de passe (d'application)", "••••••");
        _smtpPwd.contentType = TMP_InputField.ContentType.Password;
    }

    TMP_InputField LabeledInput(Transform parent, string label, string placeholder)
    {
        UIFactory.Text(parent, label, 17, UITheme.TexteSecondaire);
        return UIFactory.Input(parent, placeholder);
    }

    // ── Section RIB ────────────────────────────────────────────────────────────

    void BuildRibs(Transform parent)
    {
        var body = UIFactory.Section(parent, "RIB (comptes émetteurs)", CoBleu, CoBleuL);
        var listGO = UIFactory.VBox(body.transform, 8, 0, 0, 0, 0, "RibList");
        _ribList = listGO.transform;
        var add = UIFactory.Button(body.transform, "+  Ajouter un RIB", CoBleuL, CoBleu, 40, 18);
        add.onClick.AddListener(() => OpenRibForm(null));
        RebuildRibList();
    }

    void RebuildRibList()
    {
        foreach (Transform c in _ribList) Destroy(c.gameObject);
        if (R.ribs.Count == 0)
            UIFactory.Text(_ribList, "Aucun RIB. Cliquez sur « Ajouter un RIB ».", 16, UITheme.TexteSecondaire);
        foreach (var rib in R.ribs)
        {
            var row = UIFactory.HBox(_ribList, 8, false, "RibRow");
            var rowBg = row.gameObject.AddComponent<Image>();
            rowBg.color = UITheme.Fond; rowBg.sprite = UIFactory.Rounded(); rowBg.type = Image.Type.Sliced;
            UIFactory.LE(row.gameObject, minH: 52);
            var pad = UIFactory.VBox(row.transform, 1, 10, 10, 6, 6, "info");
            UIFactory.LE(pad.gameObject, flexW: 1);
            string ribNom = string.IsNullOrWhiteSpace(rib.name) ? "(RIB sans nom)" : rib.name;
            UIFactory.Text(pad.transform, ribNom, 18, UITheme.TextePrincipal, true);
            UIFactory.Text(pad.transform, string.IsNullOrEmpty(rib.iban) ? "—" : rib.iban, 15, UITheme.TexteSecondaire);
            var rib2 = rib;
            var edit = UIFactory.Button(row.transform, "Modifier", UITheme.Carte, UITheme.TextePrincipal, 34, 15);
            UIFactory.Border(edit.gameObject); UIFactory.LE(edit.gameObject, prefW: 90, flexW: 0);
            edit.onClick.AddListener(() => OpenRibForm(rib2));
            var del = UIFactory.Button(row.transform, "Supprimer", UITheme.AlerteClair, UITheme.AlerteTexte, 34, 15);
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
        var add = UIFactory.Button(body.transform, "+  Ajouter un entête", CoAmbreL, CoAmbre, 40, 18);
        add.onClick.AddListener(() => OpenEnteteForm(null));
        RebuildEnteteList();
    }

    void RebuildEnteteList()
    {
        foreach (Transform c in _enteteList) Destroy(c.gameObject);
        if (R.entetes.Count == 0)
            UIFactory.Text(_enteteList, "Aucun entête. Cliquez sur « Ajouter un entête ».", 16, UITheme.TexteSecondaire);
        foreach (var ent in R.entetes)
        {
            var row = UIFactory.HBox(_enteteList, 8, false, "EnteteRow");
            var rowBg = row.gameObject.AddComponent<Image>();
            rowBg.color = UITheme.Fond; rowBg.sprite = UIFactory.Rounded(); rowBg.type = Image.Type.Sliced;
            UIFactory.LE(row.gameObject, minH: 52);
            var pad = UIFactory.VBox(row.transform, 1, 10, 10, 6, 6, "info");
            UIFactory.LE(pad.gameObject, flexW: 1);
            string entNom = string.IsNullOrWhiteSpace(ent.nom) ? "(entête sans nom)" : ent.nom;
            UIFactory.Text(pad.transform, entNom, 18, UITheme.TextePrincipal, true);
            var preview = (ent.texte ?? "").Replace("\n", " ");
            if (preview.Length > 70) preview = preview.Substring(0, 70) + "…";
            UIFactory.Text(pad.transform, preview, 15, UITheme.TexteSecondaire);
            var ent2 = ent;
            var edit = UIFactory.Button(row.transform, "Modifier", UITheme.Carte, UITheme.TextePrincipal, 34, 15);
            UIFactory.Border(edit.gameObject); UIFactory.LE(edit.gameObject, prefW: 90, flexW: 0);
            edit.onClick.AddListener(() => OpenEnteteForm(ent2));
            var del = UIFactory.Button(row.transform, "Supprimer", UITheme.AlerteClair, UITheme.AlerteTexte, 34, 15);
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
            UIFactory.Text(body.transform, "Texte  —  tape « / » pour insérer une variable", 17, UITheme.TexteSecondaire);
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

    // ── Section Textes fixes ───────────────────────────────────────────────────

    void BuildTextes(Transform parent)
    {
        var body = UIFactory.Section(parent, "Textes fixes", CoTaupe, CoTaupeL);
        UIFactory.Text(body.transform, "Phrase de retard / pénalités", 17, UITheme.TexteSecondaire);
        _phraseRetard = UIFactory.Input(body.transform, "En cas de retard…", 90, true);
        UIFactory.Text(body.transform, "Bas de page (mentions société)", 17, UITheme.TexteSecondaire);
        _basDePage = UIFactory.Input(body.transform, "SAS au capital…", 90, true);
    }

    // ── Section Sauvegarde ─────────────────────────────────────────────────────

    void BuildSauvegarde(Transform parent)
    {
        var body = UIFactory.Section(parent, "Emplacement de sauvegarde", CoPetrole, CoPetroleL);
        _savePath = UIFactory.Text(body.transform, "", 16, UITheme.TexteSecondaire);

        var row = UIFactory.HBox(body.transform, 8, false, "SaveBtns");
        var change = UIFactory.Button(row.transform, "Changer l'emplacement", CoPetroleL, CoPetrole, 40, 16);
        UIFactory.LE(change.gameObject, flexW: 1);
        change.onClick.AddListener(() => { if (SaveIO.ChangeLocation()) LoadIntoUI(); });

        var open = UIFactory.Button(row.transform, "Ouvrir le dossier", UITheme.Carte, UITheme.TextePrincipal, 40, 16);
        UIFactory.Border(open.gameObject); UIFactory.LE(open.gameObject, prefW: 160, flexW: 0);
        open.onClick.AddListener(SaveIO.OpenFolder);

        var reset = UIFactory.Button(body.transform, "Réinitialiser (emplacement par défaut)", UITheme.Carte, UITheme.TexteSecondaire, 36, 15);
        UIFactory.Border(reset.gameObject);
        reset.onClick.AddListener(() => { SaveIO.ResetToDefault(); LoadIntoUI(); });
    }

    // ── Chargement / sauvegarde des valeurs ────────────────────────────────────

    void LoadIntoUI()
    {
        _apiKey.text = ReglageService.GetApiKey();
        _modePennylane.isOn = R.modeEnvoi == ModeEnvoi.Pennylane;
        if (_smtpCard != null) _smtpCard.SetActive(R.modeEnvoi == ModeEnvoi.Email);
        _smtpHost.text = R.smtp.host;
        _smtpPort.text = R.smtp.port.ToString();
        _smtpFromEmail.text = R.smtp.fromEmail;
        _smtpFromName.text = R.smtp.fromName;
        _smtpPwd.text = ReglageService.GetSmtpPassword();
        _phraseRetard.text = R.phraseRetard;
        _basDePage.text = R.basDePage;
        RebuildRibList();
        RebuildEnteteList();
        if (_savePath != null)
        {
            int nb = SaveIO.CountBatiments();
            string type = SaveLocationService.IsCustom() ? "personnalisé" : "défaut";
            _savePath.text = $"Emplacement actuel ({type}) · {nb} bâtiment(s) :\n{SaveLocationService.GetSaveRoot()}";
        }
    }

    void SaveFromUI()
    {
        ReglageService.SetApiKey(_apiKey.text.Trim());
        R.modeEnvoi = _modePennylane.isOn ? ModeEnvoi.Pennylane : ModeEnvoi.Email;
        R.smtp.host = _smtpHost.text.Trim();
        int.TryParse(_smtpPort.text.Trim(), out int port); R.smtp.port = port == 0 ? 587 : port;
        R.smtp.fromEmail = _smtpFromEmail.text.Trim();
        R.smtp.fromName = _smtpFromName.text.Trim();
        ReglageService.SetSmtpPassword(_smtpPwd.text);
        R.phraseRetard = _phraseRetard.text;
        R.basDePage = _basDePage.text;
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

        UIFactory.Text(v.transform, titre, 22, UITheme.TextePrincipal, true);
        var onValidate = builder(v);

        var actions = UIFactory.HBox(v.transform, 10, false, "Actions");
        UIFactory.LE(actions.gameObject, minH: 46, prefH: 46);
        var cancel = UIFactory.Button(actions.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 44, 18);
        UIFactory.Border(cancel.gameObject); UIFactory.LE(cancel.gameObject, flexW: 1);
        cancel.onClick.AddListener(() => Destroy(scrim.gameObject));
        var ok = UIFactory.Button(actions.transform, "Valider", UITheme.Primaire, Color.white, 44, 18);
        UIFactory.LE(ok.gameObject, flexW: 1);
        ok.onClick.AddListener(() => { onValidate?.Invoke(); Destroy(scrim.gameObject); });
    }

    TMP_InputField ModalField(VerticalLayoutGroup body, string label, string value, string placeholder)
    {
        UIFactory.Text(body.transform, label, 17, UITheme.TexteSecondaire);
        var f = UIFactory.Input(body.transform, placeholder);
        f.text = value ?? "";
        return f;
    }
}
