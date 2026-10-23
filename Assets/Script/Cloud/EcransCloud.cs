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
        var (voile, carte) = Fenetre(canvas, "CIPL — Connexion", 420, true);
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
        var connecter = Bouton(pied.transform, "Se connecter", true);

        bool enCours = false;
        async void Essayer()
        {
            if (enCours) return;
            if (string.IsNullOrWhiteSpace(email.text) || string.IsNullOrEmpty(mdp.text))
            { ConfirmDialog.Erreur("Saisissez votre e-mail et votre mot de passe."); return; }
            enCours = true; connecter.interactable = false;
            string refus = await SessionCloud.SeConnecter(email.text, mdp.text, garder.isOn);
            mdp.text = "";   // jamais gardé : à ressaisir en cas d'échec
            enCours = false; connecter.interactable = true;
            if (refus != null) { ConfirmDialog.Erreur(refus); return; }
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

        bool enCours = false;
        async void Essayer()
        {
            if (enCours) return;
            string manque = UtilisateursCloud.VerifierRejoindre(email.text, code.text, mdp.text, mdp2.text);
            if (manque != null) { ConfirmDialog.Erreur(manque); return; }
            enCours = true; rejoindre.interactable = false;
            try
            {
                // Déjà connecté (code refusé au premier essai) : on ne recrée pas le compte.
                string refus = SessionCloud.Active ? null : await SessionCloud.Rejoindre(email.text, mdp.text);
                if (refus != null) { ConfirmDialog.Erreur(refus); return; }
                var res = await UtilisateursCloud.Accepter(code.text);
                if (res?.ok != true) { ConfirmDialog.Erreur(UtilisateursCloud.Motif(res)); return; }
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

        bool envoye = false, enCours = false;
        async Task<bool> Envoyer()
        {
            string refus = RegleMotDePasse.RefusAdresse(email.text) ?? await SessionCloud.DemanderCode(email.text);
            if (refus != null) { ConfirmDialog.Erreur(refus); return false; }
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
                string refus = RegleMotDePasse.RefusCode(code.text, mdp.text, mdp2.text)
                               ?? await SessionCloud.ChangerMotDePasse(email.text, code.text, mdp.text);
                if (refus != null) { ConfirmDialog.Erreur(refus); return; }
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
