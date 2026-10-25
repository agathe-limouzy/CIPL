using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Écrans de l'étape 2 (maquettes validées le 12/10) : connexion, choix de l'entreprise,
/// import des entreprises de ce PC, menu compte, pastille « lecture seule ».
/// Construits en code comme VersementsPanel ; chaque écran rend la main par une Task.
public static class EcransCloud
{
    // ── Fondations ─────────────────────────────────────────────────────────────

    /// Voile plein écran + carte centrée. `opaque` : cache l'application derrière (connexion).
    static (RectTransform voile, VerticalLayoutGroup carte) Fenetre(Canvas canvas, string titre, float largeur, bool opaque, Color? entete = null)
    {
        var voile = UIFactory.Rect("CloudVoile", canvas.rootCanvas.transform);
        UIFactory.Stretch(voile);
        voile.gameObject.AddComponent<Image>().color = opaque ? UITheme.Fond : new Color(0, 0, 0, 0.45f);
        voile.SetAsLastSibling();

        var cardImg = UIFactory.Panel("CloudCarte", voile, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(largeur, 100);
        var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        cardImg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        DepartPanel.Entete(v.transform, titre, entete ?? UITheme.Primaire);
        return (voile, v);
    }

    static Button Bouton(Transform parent, string texte, bool principal)
    {
        var b = principal
            ? UIFactory.Button(parent, texte, UITheme.Primaire, Color.white, 40, UITheme.Role.Bouton)
            : UIFactory.Button(parent, texte, UITheme.Carte, UITheme.TextePrincipal, 40, UITheme.Role.Bouton, false);
        if (!principal) UIFactory.Border(b.gameObject);
        UIFactory.LargeurDuTexte(b);
        return b;
    }

    static HorizontalLayoutGroup Pied(Transform carte)
    {
        var pied = UIFactory.HBox(carte, 8, false, "Pied");
        pied.padding = new RectOffset(18, 18, 6, 16);
        UIFactory.LE(UIFactory.Rect("Espace", pied.transform).gameObject, flexW: 1);
        return pied;
    }

    // ── 1. Connexion ───────────────────────────────────────────────────────────

    /// Rend la main une fois connecté (SessionCloud.Session renseignée).
    public static Task Connexion(Canvas canvas)
    {
        var fin = new TaskCompletionSource<bool>();
        AfficherConnexion(canvas, fin);
        return fin.Task;
    }

    /// Les champs d'une fenêtre, par nom (RegleMotDePasse.Champ…, RegleInscription.Champ…). Retour du
    /// 25/10 : la zone en cause d'un refus passe en rouge (bordure et libellé), et redevient normale
    /// dès qu'on la modifie. Sert aussi à la fenêtre du RIB (ReglagePanel, retour du 25/10).
    internal class Zones
    {
        readonly Dictionary<string, TMP_InputField> _champs = new Dictionary<string, TMP_InputField>();

        public void Ajouter(string nom, TMP_InputField champ)
        {
            _champs[nom] = champ;
            champ.onValueChanged.AddListener(_ => Colorer(champ, false));
        }

        /// Montre en rouge ces zones (et seulement elles) ; `noms` null ou vide : aucune.
        public void Montrer(IEnumerable<string> noms)
        {
            foreach (var c in _champs.Values) Colorer(c, false);
            foreach (var n in noms ?? Enumerable.Empty<string>())
                if (_champs.TryGetValue(n, out var c)) Colorer(c, true);
        }

        static void Colorer(TMP_InputField champ, bool faute)
        {
            if (champ == null) return;
            if (champ.TryGetComponent(out Outline bord))
            {
                bord.effectColor = faute ? UITheme.Alerte : UITheme.Bordure;
                bord.effectDistance = faute ? new Vector2(2, -2) : new Vector2(1, -1);
            }
            // Le libellé : le texte juste au-dessus du champ (DepartPanel.Champ, ReglagePanel.ModalField).
            int i = champ.transform.GetSiblingIndex();
            var libelle = i > 0 ? champ.transform.parent.GetChild(i - 1).GetComponent<TMP_Text>() : null;
            if (libelle != null) libelle.color = faute ? UITheme.Alerte : UITheme.TexteSecondaire;
        }
    }

    /// Un refus : les zones en cause en rouge, puis le message au centre.
    static void Refuser(Zones zones, string motif, IEnumerable<string> champs)
    {
        zones.Montrer(champs);
        ConfirmDialog.Erreur(motif);
    }

    /// Un lien (texte bleu souligné, à gauche).
    static Button Lien(Transform parent, string texte)
    {
        var b = UIFactory.Button(parent, texte, UITheme.Carte, UITheme.Primaire, 26, UITheme.Role.Aide, false);
        var t = b.GetComponentInChildren<TMP_Text>(true);
        t.alignment = TextAlignmentOptions.Left;
        t.fontStyle = FontStyles.Underline;
        return b;
    }

    static void AfficherConnexion(Canvas canvas, TaskCompletionSource<bool> fin, string emailAvant = null)
    {
        var (voile, carte) = Fenetre(canvas, "CIPL — Connexion", 540, true);
        var corps = UIFactory.VBox(carte.transform, 12, 18, 18, 16, 6, "Corps");

        var email = DepartPanel.Champ(corps.transform, "E-mail", "vous@entreprise.fr", 1f);
        email.contentType = TMP_InputField.ContentType.EmailAddress;
        email.text = emailAvant ?? "";
        var mdp = DepartPanel.Champ(corps.transform, "Mot de passe", "", 1f);
        mdp.contentType = TMP_InputField.ContentType.Password;
        mdp.ForceLabelUpdate();
        var garder = UIFactory.Toggle(corps.transform, "Rester connecté(e) sur ce PC", true);
        // Étape 12 : l'adresse saisie passe à la fenêtre du code, et en revient.
        Lien(corps.transform, "Mot de passe oublié ?").onClick.AddListener(() =>
        {
            UnityEngine.Object.Destroy(voile.gameObject);
            AfficherMotDePasse(canvas, email.text, false, e => AfficherConnexion(canvas, fin, e), () => fin.TrySetResult(true));
        });

        var pied = Pied(carte.transform);
        // Étape 9b : un collègue invité crée ici son compte avec le code reçu par e-mail.
        var invite = Bouton(pied.transform, "J'ai reçu une invitation", false);
        invite.transform.SetAsFirstSibling();
        invite.onClick.AddListener(() => { UnityEngine.Object.Destroy(voile.gameObject); AfficherInvitation(canvas, fin); });
        // Étape 13 : un nouveau client crée lui-même son compte (l'adresse saisie passe et revient).
        var creer = Bouton(pied.transform, "Créer mon compte", false);
        creer.transform.SetSiblingIndex(1);
        creer.onClick.AddListener(() =>
        {
            UnityEngine.Object.Destroy(voile.gameObject);
            AfficherInscription(canvas, email.text, e => AfficherConnexion(canvas, fin, e), () => fin.TrySetResult(true));
        });
        var connecter = Bouton(pied.transform, "Se connecter", true);
        var zones = new Zones();
        zones.Ajouter(RegleMotDePasse.ChampEmail, email);
        zones.Ajouter(RegleMotDePasse.ChampMdp, mdp);

        bool enCours = false;
        async void Essayer()
        {
            if (enCours) return;
            if (string.IsNullOrWhiteSpace(email.text) || string.IsNullOrEmpty(mdp.text))
            {
                Refuser(zones, "Saisissez votre e-mail et votre mot de passe.",
                        new[] { (RegleMotDePasse.ChampEmail, email.text), (RegleMotDePasse.ChampMdp, mdp.text) }
                            .Where(x => string.IsNullOrWhiteSpace(x.Item2)).Select(x => x.Item1));
                return;
            }
            enCours = true; connecter.interactable = false;
            string refus = await SessionCloud.SeConnecter(email.text, mdp.text, garder.isOn);
            mdp.text = "";   // jamais gardé : à ressaisir en cas d'échec
            enCours = false; connecter.interactable = true;
            if (refus != null)
            {
                Refuser(zones, refus, refus == SessionCloud.Incorrect ? new[] { RegleMotDePasse.ChampEmail, RegleMotDePasse.ChampMdp } : null);
                return;
            }
            UnityEngine.Object.Destroy(voile.gameObject);
            fin.TrySetResult(true);
        }
        connecter.onClick.AddListener(Essayer);
        mdp.onSubmit.AddListener(_ => Essayer());
        email.Select();
    }

    /// Étape 9b (maquette validée le 19/10) : l'invité saisit l'adresse invitée, le code de l'e-mail
    /// et son mot de passe. Le compte est créé (ou, s'il existe, ouvert), puis l'invitation acceptée.
    static void AfficherInvitation(Canvas canvas, TaskCompletionSource<bool> fin)
    {
        var (voile, carte) = Fenetre(canvas, "CIPL — J'ai reçu une invitation", 440, true);
        var corps = UIFactory.VBox(carte.transform, 12, 18, 18, 16, 6, "Corps");
        UIFactory.Text(corps.transform, "Saisissez l'adresse qui a reçu l'invitation et le code de l'e-mail, " +
                       "puis choisissez votre mot de passe.", UITheme.Role.Aide, UITheme.TexteSecondaire);
        var email = DepartPanel.Champ(corps.transform, "E-mail", "vous@entreprise.fr", 1f);
        email.contentType = TMP_InputField.ContentType.EmailAddress;
        var code = DepartPanel.Champ(corps.transform, "Code d'invitation", "K7F2-9QXD", 1f);
        var mdp = DepartPanel.Champ(corps.transform, "Mot de passe (8 caractères minimum)", "", 1f);
        mdp.contentType = TMP_InputField.ContentType.Password;
        mdp.ForceLabelUpdate();
        var mdp2 = DepartPanel.Champ(corps.transform, "Confirmer le mot de passe", "", 1f);
        mdp2.contentType = TMP_InputField.ContentType.Password;
        mdp2.ForceLabelUpdate();

        var pied = Pied(carte.transform);
        var deja = Bouton(pied.transform, "J'ai déjà un compte", false);
        deja.transform.SetAsFirstSibling();
        deja.onClick.AddListener(() => { UnityEngine.Object.Destroy(voile.gameObject); AfficherConnexion(canvas, fin); });
        var rejoindre = Bouton(pied.transform, "Rejoindre", true);
        var zones = new Zones();
        zones.Ajouter(RegleMotDePasse.ChampEmail, email);
        zones.Ajouter(RegleMotDePasse.ChampCode, code);
        zones.Ajouter(RegleMotDePasse.ChampMdp, mdp);
        zones.Ajouter(RegleMotDePasse.ChampConfirmation, mdp2);

        bool enCours = false;
        async void Essayer()
        {
            if (enCours) return;
            string manque = UtilisateursCloud.VerifierRejoindre(email.text, code.text, mdp.text, mdp2.text, out var champs);
            if (manque != null) { Refuser(zones, manque, champs); return; }
            enCours = true; rejoindre.interactable = false;
            try
            {
                // Déjà connecté (code refusé au premier essai) : on ne recrée pas le compte.
                string refus = SessionCloud.Active ? null : await SessionCloud.Rejoindre(email.text, mdp.text);
                if (refus != null) { Refuser(zones, refus, null); return; }
                var res = await UtilisateursCloud.Accepter(code.text);
                if (res?.ok != true)
                {
                    Refuser(zones, UtilisateursCloud.Motif(res),
                            res?.refus == "code" || res?.refus == "expire" ? new[] { RegleMotDePasse.ChampCode } : null);
                    return;
                }
                mdp.text = mdp2.text = "";
                UnityEngine.Object.Destroy(voile.gameObject);
                UndoToast.Instance?.ShowInfo($"Bienvenue dans {res.nom}.");
                fin.TrySetResult(true);
            }
            finally { enCours = false; if (rejoindre != null) rejoindre.interactable = true; }
        }
        rejoindre.onClick.AddListener(Essayer);
        email.Select();
    }

    /// Étape 12 (maquette validée le 23/10) : l'adresse, un code par e-mail, puis le code et le nouveau
    /// mot de passe dans la même fenêtre. `connecte` : depuis le menu Compte (adresse du compte, non
    /// modifiable, fenêtre par-dessus l'application). `retour(adresse)` : « Retour » ; `reussi` : le mot
    /// de passe est changé et la session ouverte. Un refus laisse la fenêtre telle quelle.
    static void AfficherMotDePasse(Canvas canvas, string emailDepart, bool connecte, Action<string> retour, Action reussi)
    {
        var (voile, carte) = Fenetre(canvas, connecte ? "CIPL — Changer mon mot de passe" : "CIPL — Mot de passe oublié", 440, !connecte);
        var corps = UIFactory.VBox(carte.transform, 12, 18, 18, 16, 6, "Corps");
        var aide = UIFactory.Text(corps.transform, connecte ? "Un code vous sera envoyé à l'adresse de votre compte."
                                                            : "Saisissez l'adresse de votre compte : un code vous sera envoyé par e-mail.",
                                  UITheme.Role.Aide, UITheme.TexteSecondaire);
        var email = DepartPanel.Champ(corps.transform, "E-mail", "vous@entreprise.fr", 1f);
        email.contentType = TMP_InputField.ContentType.EmailAddress;
        email.text = emailDepart ?? "";
        email.interactable = !connecte;

        // Second temps, montré une fois le code parti.
        var code = DepartPanel.Champ(corps.transform, "Code reçu par e-mail", "123456", 1f);
        code.contentType = TMP_InputField.ContentType.IntegerNumber;
        var mdp = DepartPanel.Champ(corps.transform, $"Nouveau mot de passe ({RegleMotDePasse.Minimum} caractères minimum)", "", 1f);
        mdp.contentType = TMP_InputField.ContentType.Password;
        mdp.ForceLabelUpdate();
        var mdp2 = DepartPanel.Champ(corps.transform, "Confirmer le mot de passe", "", 1f);
        mdp2.contentType = TMP_InputField.ContentType.Password;
        mdp2.ForceLabelUpdate();
        var renvoyer = Lien(corps.transform, "Renvoyer le code");
        var second = new[] { code.transform.parent.gameObject, mdp.transform.parent.gameObject, mdp2.transform.parent.gameObject, renvoyer.gameObject };
        foreach (var g in second) g.SetActive(false);

        var pied = Pied(carte.transform);
        var retourBtn = Bouton(pied.transform, "Retour", false);
        retourBtn.transform.SetAsFirstSibling();
        retourBtn.onClick.AddListener(() => { UnityEngine.Object.Destroy(voile.gameObject); retour?.Invoke(email.text); });
        var principal = Bouton(pied.transform, "Envoyer le code", true);
        var libelle = principal.GetComponentInChildren<TMP_Text>(true);
        var zones = new Zones();
        zones.Ajouter(RegleMotDePasse.ChampEmail, email);
        zones.Ajouter(RegleMotDePasse.ChampCode, code);
        zones.Ajouter(RegleMotDePasse.ChampMdp, mdp);
        zones.Ajouter(RegleMotDePasse.ChampConfirmation, mdp2);

        bool envoye = false, enCours = false;
        async Task<bool> Envoyer()
        {
            string refus = RegleMotDePasse.RefusAdresse(email.text, out var champs);
            if (refus == null) { champs = null; refus = await SessionCloud.DemanderCode(email.text); }
            if (refus != null) { Refuser(zones, refus, champs); return false; }
            zones.Montrer(null);
            aide.text = $"Si un compte existe pour {email.text.Trim()}, un code vient de partir. Il est valable 1 heure.";
            return true;
        }
        async void Valider()
        {
            if (enCours) return;
            enCours = true; principal.interactable = false;
            try
            {
                if (!envoye)
                {
                    if (!await Envoyer()) return;
                    envoye = true;
                    email.interactable = false;
                    foreach (var g in second) g.SetActive(true);
                    libelle.text = "Changer le mot de passe";
                    UIFactory.LargeurDuTexte(principal);
                    code.Select();
                    return;
                }
                string refus = RegleMotDePasse.RefusCode(code.text, mdp.text, mdp2.text, out var champs);
                if (refus == null) (refus, champs) = await SessionCloud.ChangerMotDePasse(email.text, code.text, mdp.text);
                if (refus != null) { Refuser(zones, refus, champs); return; }
                mdp.text = mdp2.text = "";
                UnityEngine.Object.Destroy(voile.gameObject);
                UndoToast.Instance?.ShowInfo("Mot de passe changé.");
                reussi?.Invoke();
            }
            finally { enCours = false; if (principal != null) principal.interactable = true; }
        }
        principal.onClick.AddListener(Valider);
        renvoyer.onClick.AddListener(async () =>
        {
            if (enCours) return;
            enCours = true;
            try { if (await Envoyer()) UndoToast.Instance?.ShowInfo("Un nouveau code vient de partir."); }
            finally { enCours = false; }
        });
        mdp2.onSubmit.AddListener(_ => Valider());
        if (connecte || !string.IsNullOrWhiteSpace(email.text)) principal.Select(); else email.Select();
    }

    /// Étape 13 (maquette validée le 23/10) : « Créer mon compte ». Société (l'annuaire remplit le nom au
    /// 14e chiffre du SIRET) ou particulier (« Patrimoine de … »), l'adresse et le mot de passe, puis le
    /// code reçu par e-mail dans la même fenêtre. Compte prêt : connecté(e), essai de 14 jours, la
    /// nouvelle entreprise s'ouvre (vide). Un refus laisse la fenêtre telle quelle.
    static void AfficherInscription(Canvas canvas, string emailDepart, Action<string> retour, Action reussi)
    {
        var (voile, carte) = Fenetre(canvas, "CIPL — Créer mon compte", 480, true);
        var corps = UIFactory.VBox(carte.transform, 10, 18, 18, 14, 6, "Corps");

        UIFactory.Text(corps.transform, "Vous gérez des biens en tant que", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var choix = UIFactory.HBox(corps.transform, 6, false, "Type");
        Button Choix(string texte)
        {
            var b = UIFactory.Button(choix.transform, texte, UITheme.Carte, UITheme.TextePrincipal, 36, UITheme.Role.Bouton, false);
            UIFactory.Border(b.gameObject);
            UIFactory.LE(b.gameObject, flexW: 1);
            return b;
        }
        var btnSociete = Choix("Société");
        var btnParticulier = Choix("Particulier");

        var siret = DepartPanel.Champ(corps.transform, "SIRET (14 chiffres)", "123 456 789 00012", 1f);
        var infoAnnuaire = UIFactory.Text(corps.transform, "", UITheme.Role.Aide, UITheme.TexteSecondaire);
        var ligneNom = UIFactory.HBox(corps.transform, 10, false, "Prénom et nom");
        var prenom = DepartPanel.Champ(ligneNom.transform, "Prénom", "Jean", 1f);
        var nom = DepartPanel.Champ(ligneNom.transform, "Nom", "Martin", 1f);
        var adresse = DepartPanel.Champ(corps.transform, "Adresse", "4 rue des Lilas, 31000 Toulouse", 1f);
        var entreprise = DepartPanel.Champ(corps.transform, "Nom de l'entreprise", "SCI Rivoli", 1f);
        var monNom = DepartPanel.Champ(corps.transform, "Votre nom (affiché à vos collègues)", "Marie Dupont", 1f);
        var email = DepartPanel.Champ(corps.transform, "E-mail", "vous@entreprise.fr", 1f);
        email.contentType = TMP_InputField.ContentType.EmailAddress;
        email.text = emailDepart ?? "";
        var ligneMdp = UIFactory.HBox(corps.transform, 10, false, "Mot de passe");
        var mdp = DepartPanel.Champ(ligneMdp.transform, $"Mot de passe ({RegleMotDePasse.Minimum} caractères min.)", "", 1f);
        mdp.contentType = TMP_InputField.ContentType.Password;
        mdp.ForceLabelUpdate();
        var mdp2 = DepartPanel.Champ(ligneMdp.transform, "Confirmer", "", 1f);
        mdp2.contentType = TMP_InputField.ContentType.Password;
        mdp2.ForceLabelUpdate();
        var aide = UIFactory.Text(corps.transform, "Essai gratuit de 14 jours, sans carte.", UITheme.Role.Aide, UITheme.TexteSecondaire);
        var code = DepartPanel.Champ(corps.transform, "Code reçu par e-mail", "123456", 1f);
        code.contentType = TMP_InputField.ContentType.IntegerNumber;
        var renvoyer = Lien(corps.transform, "Renvoyer le code");
        var second = new[] { code.transform.parent.gameObject, renvoyer.gameObject };
        foreach (var g in second) g.SetActive(false);

        // Nom de l'entreprise proposé (raison sociale de l'annuaire, ou « Patrimoine de … »), tant
        // qu'il n'a pas été changé à la main.
        string nomAuto = "";
        void Proposer(string n) { if (string.IsNullOrWhiteSpace(entreprise.text) || entreprise.text == nomAuto) entreprise.text = nomAuto = n ?? ""; }

        bool societe = true;
        void Type(bool s)
        {
            societe = s;
            foreach (var g in new[] { siret.transform.parent.gameObject, infoAnnuaire.gameObject, monNom.transform.parent.gameObject }) g.SetActive(s);
            foreach (var g in new[] { ligneNom.gameObject, adresse.transform.parent.gameObject }) g.SetActive(!s);
            foreach (var (b, on) in new[] { (btnSociete, s), (btnParticulier, !s) })
            {
                b.image.color = on ? UITheme.Primaire : UITheme.Carte;
                b.GetComponentInChildren<TMP_Text>(true).color = on ? Color.white : UITheme.TextePrincipal;
            }
            if (!s) Proposer(RegleInscription.NomParticulier(prenom.text, nom.text));
        }
        btnSociete.onClick.AddListener(() => Type(true));
        btnParticulier.onClick.AddListener(() => Type(false));
        prenom.onValueChanged.AddListener(_ => Proposer(RegleInscription.NomParticulier(prenom.text, nom.text)));
        nom.onValueChanged.AddListener(_ => Proposer(RegleInscription.NomParticulier(prenom.text, nom.text)));

        // Étape 10 : aux 14 chiffres, l'annuaire des entreprises ; forme et siège affichés, nom proposé.
        AnnuaireEntreprise annuaire = null;
        string cherche = null;
        async void Rechercher(string chiffres)
        {
            cherche = chiffres;
            annuaire = null;
            infoAnnuaire.color = UITheme.TexteSecondaire;
            infoAnnuaire.text = "Recherche dans l'annuaire des entreprises…";
            var (data, _, introuvable) = await PapperService.Chercher(chiffres);
            if (infoAnnuaire == null || cherche != chiffres) return;   // fenêtre fermée, ou autre SIRET saisi depuis
            string refus = PappersResume.Refus(chiffres, data, introuvable);
            if (refus != null) { infoAnnuaire.color = UITheme.Alerte; infoAnnuaire.text = refus; return; }
            if (data == null) { infoAnnuaire.text = "Annuaire des entreprises injoignable : le SIRET n'a pas pu être vérifié."; return; }
            annuaire = data;
            infoAnnuaire.text = PappersResume.Description(data);
            Proposer(data.nom_complet);
        }
        siret.onValueChanged.AddListener(v =>
        {
            string chiffres = new string((v ?? "").Where(char.IsDigit).ToArray());
            if (chiffres.Length == 14 && chiffres != cherche) Rechercher(chiffres);
            else if (chiffres.Length != 14) { cherche = null; annuaire = null; infoAnnuaire.text = ""; }
        });
        Type(true);

        var pied = Pied(carte.transform);
        var retourBtn = Bouton(pied.transform, "Retour", false);
        retourBtn.transform.SetAsFirstSibling();
        retourBtn.onClick.AddListener(() => { UnityEngine.Object.Destroy(voile.gameObject); retour?.Invoke(email.text); });
        var principal = Bouton(pied.transform, "Recevoir le code", true);
        var libelle = principal.GetComponentInChildren<TMP_Text>(true);
        var zones = new Zones();
        foreach (var (n, c) in new[]
        {
            (RegleInscription.ChampSiret, siret), (RegleInscription.ChampPrenom, prenom), (RegleInscription.ChampNom, nom),
            (RegleInscription.ChampAdresse, adresse), (RegleInscription.ChampEntreprise, entreprise), (RegleInscription.ChampMonNom, monNom),
            (RegleMotDePasse.ChampEmail, email), (RegleMotDePasse.ChampMdp, mdp), (RegleMotDePasse.ChampConfirmation, mdp2),
            (RegleMotDePasse.ChampCode, code),
        })
            zones.Ajouter(n, c);

        string Manque(out string[] champs) => RegleInscription.Refus(societe, siret.text, entreprise.text, prenom.text, nom.text,
                                                                     adresse.text, monNom.text, email.text, mdp.text, mdp2.text, out champs);
        async Task<bool> Envoyer()
        {
            string refus = Manque(out var champs);
            if (refus == null && societe)   // société fermée ou introuvable : refusée avant d'envoyer un code
            {
                var (data, _, introuvable) = await PapperService.Chercher(new string(siret.text.Where(char.IsDigit).ToArray()));
                refus = PappersResume.Refus(siret.text, data, introuvable);
                if (refus == null && data != null) annuaire = data;
                if (refus == null && await SessionCloud.SiretDejaSurCipl(siret.text))   // AO8 : avant le code
                    refus = RegleInscription.Motif(new RegleInscription.Resultat { refus = "deja_sur_cipl" });
                champs = new[] { RegleInscription.ChampSiret };
            }
            // Retour du 25/10 : une adresse déjà membre d'un groupe est refusée avant l'envoi du code.
            if (refus == null && await SessionCloud.AdresseDejaMembre(email.text))
            {
                refus = RegleInscription.Motif(new RegleInscription.Resultat { refus = "deja_membre" });
                champs = new[] { RegleMotDePasse.ChampEmail };
            }
            if (refus == null) { champs = null; refus = await SessionCloud.DemanderCodeInscription(email.text); }
            if (refus != null) { Refuser(zones, refus, champs); return false; }
            zones.Montrer(null);
            aide.text = $"Un code vient de partir à {email.text.Trim()}. Il est valable 1 heure.";
            return true;
        }

        bool envoye = false, enCours = false;
        async void Valider()
        {
            if (enCours) return;
            enCours = true; principal.interactable = false;
            try
            {
                if (!envoye)
                {
                    if (!await Envoyer()) return;
                    envoye = true;
                    email.interactable = false;   // le code vaut pour cette adresse
                    foreach (var g in second) g.SetActive(true);
                    libelle.text = "Créer mon compte";
                    UIFactory.LargeurDuTexte(principal);
                    code.Select();
                    return;
                }
                string qui = societe ? monNom.text.Trim() : $"{prenom.text.Trim()} {nom.text.Trim()}";
                string refus = Manque(out var champs) ?? RegleMotDePasse.RefusCode(code.text, mdp.text, mdp2.text, out champs);
                if (refus == null)
                    (refus, champs) = await SessionCloud.Inscrire(email.text, code.text, mdp.text, societe ? "societe" : "particulier",
                        entreprise.text.Trim(), societe ? siret.text : "", qui,
                        RegleInscription.Reglages(societe, entreprise.text, siret.text, annuaire, qui, adresse.text, email.text));
                if (refus != null) { Refuser(zones, refus, champs); return; }
                mdp.text = mdp2.text = "";
                UnityEngine.Object.Destroy(voile.gameObject);
                UndoToast.Instance?.ShowInfo($"Bienvenue sur CIPL : « {entreprise.text.Trim()} » est créée, essai gratuit de 14 jours.");
                reussi?.Invoke();
            }
            finally { enCours = false; if (principal != null) principal.interactable = true; }
        }
        principal.onClick.AddListener(Valider);
        renvoyer.onClick.AddListener(async () =>
        {
            if (enCours) return;
            enCours = true;
            try { if (await Envoyer()) UndoToast.Instance?.ShowInfo("Un nouveau code vient de partir."); }
            finally { enCours = false; }
        });
        siret.Select();
    }

    // ── 2. Choisir l'entreprise ────────────────────────────────────────────────

    /// L'entreprise choisie, ou null si « Se déconnecter ».
    public static Task<EntrepriseCloud> Choisir(Canvas canvas, IList<EntrepriseCloud> liste, string connecte)
    {
        var fin = new TaskCompletionSource<EntrepriseCloud>();
        var (voile, carte) = Fenetre(canvas, "Choisir l'entreprise", 460, true);
        var corps = UIFactory.VBox(carte.transform, 8, 18, 18, 14, 6, "Corps");
        UIFactory.Text(corps.transform, "Connecté(e) : " + connecte, UITheme.Role.Aide, UITheme.TexteSecondaire);

        if (liste.Count == 0)
            UIFactory.Text(corps.transform, "Vous n'avez accès à aucune entreprise sur le cloud. " +
                           "Demandez une invitation à son administrateur.", UITheme.Role.Donnee, UITheme.TextePrincipal);

        foreach (var e in liste)
        {
            var choix = e;
            var b = UIFactory.Button(corps.transform, "", UITheme.Carte, UITheme.TextePrincipal, 44, UITheme.Role.Donnee, false);
            UIFactory.Border(b.gameObject);
            var t = b.GetComponentInChildren<TMP_Text>(true);
            t.alignment = TextAlignmentOptions.Left;
            t.text = $"{e.nom}   <color=#5F5E5A>{(e.EstAdmin ? "Admin" : "Utilisateur")} · {e.nb_batiments} bâtiment(s) ›</color>";
            b.onClick.AddListener(() => { UnityEngine.Object.Destroy(voile.gameObject); fin.TrySetResult(choix); });
        }

        var pied = UIFactory.HBox(carte.transform, 8, false, "Pied");
        pied.padding = new RectOffset(18, 18, 6, 16);
        var deco = Bouton(pied.transform, "Se déconnecter", false);
        deco.onClick.AddListener(() => { UnityEngine.Object.Destroy(voile.gameObject); fin.TrySetResult(null); });
        return fin.Task;
    }

    // ── 3. Import des entreprises de ce PC ─────────────────────────────────────

    /// `importer(source, etat, progression)` fait l'envoi et renvoie null ou le motif d'échec.
    /// Rend la main après l'import, ou sur « Plus tard ».
    public static Task Import(Canvas canvas, IList<(ImportLocal.Source source, ImportLocal.Etat etat)> sources,
                              Func<ImportLocal.Source, ImportLocal.Etat, Action<string>, Task<string>> importer)
    {
        var fin = new TaskCompletionSource<bool>();
        var (voile, carte) = Fenetre(canvas, "Bienvenue sur CIPL en ligne", 520, true);
        var corps = UIFactory.VBox(carte.transform, 10, 18, 18, 14, 6, "Corps");

        bool reprise = sources.Any(s => s.etat == ImportLocal.Etat.AReprendre);
        UIFactory.Text(corps.transform, reprise
            ? "Un import n'est pas allé au bout. Reprenez-le : ce qui est déjà envoyé n'est pas doublé."
            : "Vous n'avez encore aucune entreprise sur le cloud. Ces entreprises ont été trouvées sur ce PC :",
            UITheme.Role.Donnee, UITheme.TextePrincipal);

        var cases = new List<(Toggle t, ImportLocal.Source s, ImportLocal.Etat e)>();
        foreach (var (s, e) in sources)
        {
            string detail = $"{s.Nom} — {s.NbBatiments} bâtiment(s), {s.NbLocataires} locataire(s)";
            if (e == ImportLocal.Etat.AReprendre) detail += " (import à reprendre)";
            if (e == ImportLocal.Etat.DejaSurLeCloud) detail += " (déjà sur le cloud : demandez une invitation)";
            var t = UIFactory.Toggle(corps.transform, detail, e == ImportLocal.Etat.AReprendre);
            t.interactable = e != ImportLocal.Etat.DejaSurLeCloud;
            cases.Add((t, s, e));
        }

        UIFactory.Text(corps.transform, "Chaque entreprise cochée est créée sur le cloud sous le même nom, avec ses bâtiments " +
                       "et locataires. Vos fichiers sur ce PC ne sont que lus (seul un petit fichier repère est ajouté). " +
                       "Les documents (PDF, photos) restent sur ce PC.", UITheme.Role.Aide, UITheme.TexteSecondaire);
        var etat = UIFactory.Text(corps.transform, "", UITheme.Role.Donnee, UITheme.Primaire, true);

        var pied = Pied(carte.transform);
        var plusTard = Bouton(pied.transform, "Plus tard", false);
        var importerBtn = Bouton(pied.transform, "Importer", true);
        plusTard.onClick.AddListener(() => { UnityEngine.Object.Destroy(voile.gameObject); fin.TrySetResult(false); });

        importerBtn.onClick.AddListener(async () =>
        {
            var choisies = cases.Where(c => c.t.isOn && c.t.interactable).ToList();
            if (choisies.Count == 0) { ConfirmDialog.Erreur("Cochez au moins une entreprise à importer."); return; }
            importerBtn.interactable = plusTard.interactable = false;
            foreach (var c in choisies)
            {
                string refus = await importer(c.s, c.e, msg => etat.text = msg);
                if (refus != null)
                {
                    ConfirmDialog.Erreur($"Import de « {c.s.Nom} » interrompu : {refus}\nRelancez CIPL pour le reprendre.");
                    break;
                }
            }
            UnityEngine.Object.Destroy(voile.gameObject);
            fin.TrySetResult(true);
        });
        return fin.Task;
    }

    // ── Nom affiché (étape 4a) ─────────────────────────────────────────────────

    /// « Votre nom (affiché à vos collègues) » ; renvoie le nom saisi, ou "" si l'on passe.
    public static Task<string> DemanderNom(Canvas canvas)
    {
        var fin = new TaskCompletionSource<string>();
        var (voile, carte) = Fenetre(canvas, "Votre nom", 420, false);
        var corps = UIFactory.VBox(carte.transform, 10, 18, 18, 14, 6, "Corps");
        var nom = DepartPanel.Champ(corps.transform, "Votre nom (affiché à vos collègues)", "Marie Dupont", 1f);
        UIFactory.Text(corps.transform, "Il apparaît dans « En cours de modification par… ». Sans nom, c'est votre e-mail qui s'affiche.",
                       UITheme.Role.Aide, UITheme.TexteSecondaire);
        var pied = Pied(carte.transform);
        var passer = Bouton(pied.transform, "Plus tard", false);
        var ok = Bouton(pied.transform, "Enregistrer", true);
        passer.onClick.AddListener(() => { UnityEngine.Object.Destroy(voile.gameObject); fin.TrySetResult(""); });
        ok.onClick.AddListener(() =>
        {
            if (string.IsNullOrWhiteSpace(nom.text)) { ConfirmDialog.Erreur("Saisissez votre nom, ou choisissez « Plus tard »."); return; }
            UnityEngine.Object.Destroy(voile.gameObject);
            fin.TrySetResult(nom.text.Trim());
        });
        nom.Select();
        return fin.Task;
    }

    // ── Drive partagé introuvable sur ce PC (étape 6b, maquette validée le 14/10) ─

    public enum ChoixDossier { Rechercher, Choisir, Continuer }

    /// `refus` : motif du dernier « Choisir le dossier… » refusé, affiché en rouge (null : rien).
    public static Task<ChoixDossier> DossierIntrouvable(Canvas canvas, string nomEntreprise, string refus)
    {
        var fin = new TaskCompletionSource<ChoixDossier>();
        var (voile, carte) = Fenetre(canvas, $"Documents de {nomEntreprise}", 520, false);
        var corps = UIFactory.VBox(carte.transform, 8, 18, 18, 14, 6, "Corps");
        UIFactory.Text(corps.transform, "L'admin range les documents dans un Drive partagé, mais CIPL ne le trouve pas sur cet ordinateur.",
                       UITheme.Role.Donnee, UITheme.TextePrincipal);
        UIFactory.Text(corps.transform, "Ouvrez ce Drive dans votre navigateur et cliquez sur « Synchroniser », puis « Rechercher à nouveau ».",
                       UITheme.Role.Aide, UITheme.TexteSecondaire);
        if (!string.IsNullOrEmpty(refus))
            UIFactory.Text(corps.transform, refus, UITheme.Role.Aide, new Color32(0xA3, 0x2D, 0x2D, 0xFF));
        var pied = Pied(carte.transform);
        var continuer = Bouton(pied.transform, "Continuer sans le dossier", false);
        var choisir = Bouton(pied.transform, "Choisir le dossier…", false);
        var rechercher = Bouton(pied.transform, "Rechercher à nouveau", true);
        void Repondre(ChoixDossier c) { UnityEngine.Object.Destroy(voile.gameObject); fin.TrySetResult(c); }
        continuer.onClick.AddListener(() => Repondre(ChoixDossier.Continuer));
        choisir.onClick.AddListener(() => Repondre(ChoixDossier.Choisir));
        rechercher.onClick.AddListener(() => Repondre(ChoixDossier.Rechercher));
        return fin.Task;
    }

    // ── « Toujours là ? » (étape 4b, maquette 3 du 09/10) ──────────────────────

    /// Modale ambre ; le gardien des verrous réécrit le texte chaque seconde (compte à rebours).
    public static (GameObject voile, TMP_Text texte) ToujoursLa(Canvas canvas, Action continuer, Action liberer)
    {
        var (voile, carte) = Fenetre(canvas, "Toujours là ?", 480, false, new Color32(0xBA, 0x75, 0x17, 0xFF));
        var corps = UIFactory.VBox(carte.transform, 10, 18, 18, 14, 6, "Corps");
        var texte = UIFactory.Text(corps.transform, "", UITheme.Role.Donnee, UITheme.TextePrincipal);
        var pied = Pied(carte.transform);
        var lib = Bouton(pied.transform, "Annuler et libérer", false);
        var cont = Bouton(pied.transform, "Continuer", true);
        lib.onClick.AddListener(() => { UnityEngine.Object.Destroy(voile.gameObject); liberer(); });
        cont.onClick.AddListener(() => { UnityEngine.Object.Destroy(voile.gameObject); continuer(); });
        return (voile.gameObject, texte);
    }

    /// « Vous modifiez Volteo sans activité depuis 15 min. … Sans réponse dans 1:45, … »
    public static string TexteToujoursLa(IList<string> fiches, string restant)
    {
        bool une = fiches.Count <= 1;
        return $"Vous modifiez {string.Join(", ", fiches)} sans activité depuis {(int)RegleVerrou.Inactivite.TotalMinutes} min. "
             + $"Les autres ne peuvent pas modifier {(une ? "cette fiche" : "ces fiches")}. "
             + $"Sans réponse dans {restant}, vos saisies non enregistrées seront annulées et "
             + (une ? "la fiche libérée." : "les fiches libérées.");
    }

    // ── 4. Menu compte et pastille ─────────────────────────────────────────────

    public static void MenuCompte(Canvas canvas, Action changer, Action deconnecter)
    {
        var (voile, carte) = Fenetre(canvas, "Compte", 380, false);
        var corps = UIFactory.VBox(carte.transform, 6, 18, 18, 14, 6, "Corps");
        UIFactory.Text(corps.transform, SessionCloud.Session?.user?.email ?? "Non connecté(e)", UITheme.Role.Donnee, UITheme.TextePrincipal, true);
        UIFactory.Text(corps.transform, SessionCloud.Entreprise != null
            ? $"{SessionCloud.Entreprise.nom} · {(SessionCloud.Entreprise.EstAdmin ? "Admin" : "Utilisateur")}" : "",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        // Étape 12 : la même fenêtre que « Mot de passe oublié », sur l'adresse du compte.
        Lien(corps.transform, "Changer mon mot de passe").onClick.AddListener(() =>
        {
            UnityEngine.Object.Destroy(voile.gameObject);
            AfficherMotDePasse(canvas, SessionCloud.Session?.user?.email, true, null, null);
        });
        // Étape 13b.
        Lien(corps.transform, "Revoir la visite guidée").onClick.AddListener(() =>
        {
            UnityEngine.Object.Destroy(voile.gameObject);
            VisiteGuidee.Lancer();
        });
        // Retour du 25/10 : la carte masquée revient d'ici (sinon, plus moyen de la retrouver).
        if (SessionCloud.Retenu.cipl_premiers_pas_masques)
            Lien(corps.transform, "Réafficher les premiers pas").onClick.AddListener(() =>
            {
                UnityEngine.Object.Destroy(voile.gameObject);
                SessionCloud.Retenir(SessionCloud.PremiersPasMasques, false);
                GeneralMenuPanel.Instance?.Refresh();
            });

        var pied = UIFactory.HBox(carte.transform, 8, false, "Pied");
        pied.padding = new RectOffset(18, 18, 6, 16);
        var fermer = Bouton(pied.transform, "Fermer", false);
        UIFactory.LE(UIFactory.Rect("Espace", pied.transform).gameObject, flexW: 1);
        var chg = Bouton(pied.transform, "Changer d'entreprise", false);
        var deco = Bouton(pied.transform, "Se déconnecter", true);
        Action fermerVoile = () => UnityEngine.Object.Destroy(voile.gameObject);
        fermer.onClick.AddListener(() => fermerVoile());
        chg.onClick.AddListener(() => { fermerVoile(); changer(); });
        deco.onClick.AddListener(() => { fermerVoile(); deconnecter(); });
    }

    static GameObject _pastille;

    /// Pastille discrète en bas à gauche, tant que CIPL est en lecture seule sur le cloud.
    public static void Pastille(Canvas canvas, string texte)
    {
        if (_pastille != null) UnityEngine.Object.Destroy(_pastille);
        if (string.IsNullOrEmpty(texte)) return;
        var fond = UIFactory.Panel("CloudPastille", canvas.rootCanvas.transform, new Color32(0xFA, 0xEE, 0xDA, 0xF0));
        fond.raycastTarget = false;
        var rt = (RectTransform)fond.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(12, 12);
        var h = fond.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 12, 6, 6);
        h.childControlWidth = true; h.childControlHeight = true;
        var csf = fond.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var t = UIFactory.Text(fond.transform, texte, UITheme.Role.Aide, new Color32(0x63, 0x38, 0x06, 0xFF), true);
        t.enableWordWrapping = false;
        _pastille = fond.gameObject;
    }
}
