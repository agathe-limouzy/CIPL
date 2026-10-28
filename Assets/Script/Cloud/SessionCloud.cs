using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// Étape 12 (décisions du 23/10) : « Mot de passe oublié » à la connexion et « Changer mon mot de
/// passe » dans le menu Compte : un code envoyé par Supabase, puis le nouveau mot de passe.
public static class RegleMotDePasse
{
    public const int Minimum = 8;

    // Les zones en cause d'un refus (retour du 25/10 : elles passent en rouge dans la fenêtre).
    public const string ChampEmail = "email", ChampCode = "code", ChampMdp = "mdp", ChampConfirmation = "confirmation";
    static readonly string[] Aucun = new string[0];

    /// Null si le mot de passe convient, sinon le motif.
    public static string Refus(string mdp, string confirmation) => Refus(mdp, confirmation, out _);

    public static string Refus(string mdp, string confirmation, out string[] champs)
    {
        champs = Aucun;
        if ((mdp ?? "").Length < Minimum) { champs = new[] { ChampMdp }; return $"Le mot de passe doit faire au moins {Minimum} caractères."; }
        if (mdp != confirmation) { champs = new[] { ChampMdp, ChampConfirmation }; return "Les deux mots de passe ne sont pas identiques."; }
        return null;
    }

    public static string RefusAdresse(string email) => RefusAdresse(email, out _);

    public static string RefusAdresse(string email, out string[] champs)
    {
        bool faute = string.IsNullOrWhiteSpace(email) || !email.Contains("@");
        champs = faute ? new[] { ChampEmail } : Aucun;
        return faute ? "Saisissez l'adresse e-mail de votre compte." : null;
    }

    /// Le code de l'e-mail sans espaces ni tirets (« 123 456 » → « 123456 »).
    public static string Code(string saisie) => new string((saisie ?? "").Where(char.IsDigit).ToArray());

    /// Le code et le nouveau mot de passe : null si complets, sinon ce qu'il manque. Supabase envoie
    /// 6 chiffres par défaut (réglable jusqu'à 10).
    public static string RefusCode(string code, string mdp, string confirmation) => RefusCode(code, mdp, confirmation, out _);

    public static string RefusCode(string code, string mdp, string confirmation, out string[] champs)
    {
        int n = Code(code).Length;
        if (n < 6 || n > 10) { champs = new[] { ChampCode }; return "Saisissez le code à 6 chiffres reçu par e-mail."; }
        return Refus(mdp, confirmation, out champs);
    }

    /// Zones en cause d'un code refusé par Supabase (incorrect ou expiré), ou d'un mot de passe refusé.
    public static string[] ChampsCode(long code) => code >= 400 && code < 500 && code != 429 ? new[] { ChampCode } : Aucun;
    public static string[] ChampsChangement(long code) => code == 422 ? new[] { ChampMdp, ChampConfirmation } : Aucun;

    // Les refus de Supabase, en clair.

    /// 429 : un code demandé il y a moins d'une minute (« … after 42 seconds »), ou le plafond d'e-mails
    /// par heure du service d'envoi de Supabase (« email rate limit exceeded »).
    public static string MotifEnvoi(long code, string erreur)
    {
        if (code != 429) return "Envoi du code impossible : " + erreur;
        return (erreur ?? "").IndexOf("seconds", StringComparison.OrdinalIgnoreCase) >= 0
            ? "Un code vient déjà de partir : attendez une minute avant d'en demander un autre."
            : "Trop d'e-mails envoyés pour l'instant : réessayez dans une heure.";
    }

    public static string MotifCode(long code, string erreur) => code >= 400 && code < 500 && code != 429
        ? "Ce code est incorrect ou a expiré : vérifiez-le, ou demandez-en un nouveau."
        : "Vérification du code impossible : " + erreur;

    public static string MotifChangement(long code, string erreur)
    {
        string e = erreur ?? "";
        if (e.IndexOf("different", StringComparison.OrdinalIgnoreCase) >= 0) return "Choisissez un mot de passe différent de l'ancien.";
        if (code == 422) return "Mot de passe refusé : " + e;
        return "Changement du mot de passe impossible : " + e;
    }
}

/// Une entreprise du cloud, telle que la renvoie mes_entreprises().
[Serializable]
public class EntrepriseCloud
{
    public string id, nom, type, role;
    public int nb_batiments;
    public string mode_documents;   // étape 6b : « cipl » (chaque PC reçoit sa copie) ou « drive » (Drive partagé)
    public string siret;            // 27/10 : affiché dans Réglages › Entreprise (vide : serveur pas encore à jour, ou particulier)
    public bool EstAdmin => role == "admin";
    public bool ModeDrive => mode_documents == "drive";
}

/// Qui est connecté, sur quelle entreprise. Le jeton de renouvellement est gardé
/// chiffré par Windows (Dpapi) dans persistentDataPath si « Rester connectée » est coché.
/// Étape 2 : tout est en lecture seule (l'écriture dans le cloud arrive à l'étape 4).
public static class SessionCloud
{
    public static string Url => ReglageService.GetCloudUrl();
    public static string Cle => ReglageService.GetCloudCle();
    public static bool Configure => !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(Cle);

    public static CloudClient.Session Session { get; private set; }
    public static EntrepriseCloud Entreprise { get; set; }
    public static bool Active => Session != null;
    public static bool LectureSeule => false;   // étape 4a : on écrit dans le cloud (EcritureCloud)

    static bool _memoriser;
    /// L'éditeur et le build partagent persistentDataPath : avec un seul fichier, se connecter dans
    /// l'un changeait le compte de l'autre (retour du 19/10). L'éditeur garde donc le sien.
    /// ponytail: distinction pour le développement seulement ; l'application vendue n'a que cipl_session.dat.
    static string Fichier => Path.Combine(Application.persistentDataPath, Application.isEditor ? "cipl_session_editeur.dat" : "cipl_session.dat");

    public const string Incorrect = "E-mail ou mot de passe incorrect.";

    /// Null si la connexion a réussi, sinon le motif à afficher.
    public static async Task<string> SeConnecter(string email, string motDePasse, bool memoriser)
    {
        var r = await CloudClient.SeConnecter(Url, Cle, (email ?? "").Trim(), motDePasse ?? "");
        if (!r.Ok)
            return r.Code == 400 ? Incorrect : "Connexion impossible : " + r.Erreur;
        var s = CloudClient.LireSession(r.Corps);
        if (s == null) return "Réponse du serveur illisible.";
        Session = s;
        _memoriser = memoriser;
        if (memoriser) Memoriser(); else EffacerFichier();
        return null;
    }

    /// Étape 9b, « J'ai reçu une invitation » : crée le compte ; s'il existe déjà (membre d'une
    /// autre entreprise), se connecte avec son mot de passe. Null si connecté, sinon le motif.
    public static async Task<string> Rejoindre(string email, string motDePasse)
    {
        var r = await CloudClient.Inscrire(Url, Cle, (email ?? "").Trim(), motDePasse ?? "");
        var s = r.Ok ? CloudClient.LireSession(r.Corps) : null;
        if (s != null)
        {
            Session = s;
            _memoriser = true;
            Memoriser();
            return null;
        }
        if (r.Ok) return "Le compte est créé, mais Supabase attend une confirmation par e-mail : " +
                         "décochez « Confirm email » dans Supabase (voir Serveur/etape9b_supabase.sql).";
        if (r.Code == 400 || r.Code == 422) return await SeConnecter(email, motDePasse, true);   // compte déjà là
        return "Création du compte impossible : " + r.Erreur;
    }

    /// Reprend la connexion gardée sur ce PC. Faux s'il n'y en a pas ou si elle n'est plus valable.
    public static async Task<bool> Restaurer()
    {
        string garde = JetonGarde();
        if (garde == null) return false;
        if (garde == "") { EffacerFichier(); return false; }

        var r = await CloudClient.Renouveler(Url, Cle, garde);
        var s = r.Ok ? CloudClient.LireSession(r.Corps) : null;
        if (s == null)
        {
            // Jeton refusé par le serveur (4xx) : il ne vaut plus rien. Réseau qui ne répond pas
            // (code 0, 5xx) : on le garde pour la prochaine fois (retour du 14/10).
            if (r.Ok || (r.Code >= 400 && r.Code < 500)) EffacerFichier();
            Debug.LogWarning("[Cloud] Reprise de la connexion impossible : " + (r.Ok ? "réponse illisible" : r.Erreur));
            return false;
        }
        Session = s;
        _memoriser = true;
        Memoriser();   // le serveur remplace le jeton de renouvellement à chaque usage
        return true;
    }

    /// Étape 8b : pas de réseau au démarrage. La connexion gardée est reprise telle quelle : son
    /// jeton de renouvellement servira au retour du réseau (Jeton le renouvelle au premier appel).
    /// L'utilisateur vient de la copie locale. Faux sans connexion gardée sur ce PC.
    public static bool RestaurerHorsLigne(CloudClient.Utilisateur u)
    {
        if (u == null || string.IsNullOrEmpty(u.id)) return false;
        string garde = JetonGarde();
        if (string.IsNullOrEmpty(garde)) return false;
        Session = new CloudClient.Session { access_token = "", refresh_token = garde, expires_at = 0, user = u };
        _memoriser = true;
        return true;
    }

    /// Jeton valable pour un appel : renouvelé s'il expire dans moins de 5 minutes.
    public static async Task<string> Jeton()
    {
        if (Session == null) return null;
        if (Session.ExpireLe - DateTime.Now > TimeSpan.FromMinutes(5)) return Session.access_token;
        var r = await CloudClient.Renouveler(Url, Cle, Session.refresh_token);
        var s = r.Ok ? CloudClient.LireSession(r.Corps) : null;
        if (s != null)
        {
            Session = s;
            if (_memoriser) Memoriser();
        }
        return Session.access_token;
    }

    // ── Étape 12 : mot de passe ────────────────────────────────────────────────

    static CloudClient.Session _recuperation;   // code vérifié, en attente du nouveau mot de passe
    static string _recuperationEmail;

    /// Envoie le code. Null s'il est parti — ou si l'adresse n'a pas de compte : Supabase ne le dit pas.
    public static async Task<string> DemanderCode(string email)
    {
        _recuperation = null;   // un nouveau code remplace l'ancien
        var r = await CloudClient.DemanderCode(Url, Cle, (email ?? "").Trim());
        return r.Ok ? null : RegleMotDePasse.MotifEnvoi(r.Code, r.Erreur);
    }

    /// Le code de l'e-mail, puis le nouveau mot de passe. Null si changé : la session est alors celle
    /// du compte (décision du 23/10 : connecté(e) directement), gardée sur ce PC — sauf depuis le menu
    /// Compte, où la connexion garde le choix fait à l'ouverture.
    public static async Task<(string motif, string[] champs)> ChangerMotDePasse(string email, string code, string motDePasse)
    {
        email = (email ?? "").Trim();
        // Un code ne sert qu'une fois : vérifié, sa session attend un mot de passe accepté (refusé s'il
        // est identique à l'ancien), sans redemander de code.
        if (_recuperation == null || _recuperationEmail != email.ToLowerInvariant() || _recuperation.ExpireLe < DateTime.Now)
        {
            var v = await CloudClient.VerifierCode(Url, Cle, email, RegleMotDePasse.Code(code));
            var s = v.Ok ? CloudClient.LireSession(v.Corps) : null;
            if (s == null) return v.Ok ? ("Réponse du serveur illisible.", null)
                                       : (RegleMotDePasse.MotifCode(v.Code, v.Erreur), RegleMotDePasse.ChampsCode(v.Code));
            _recuperation = s;
            _recuperationEmail = email.ToLowerInvariant();
        }
        var r = await CloudClient.ChangerMotDePasse(Url, Cle, _recuperation.access_token, motDePasse ?? "");
        if (!r.Ok) return (RegleMotDePasse.MotifChangement(r.Code, r.Erreur), RegleMotDePasse.ChampsChangement(r.Code));

        bool garder = Session == null || _memoriser;
        Session = _recuperation;
        _recuperation = null;
        _memoriser = garder;
        if (garder) Memoriser();
        return (null, null);
    }

    // ── Étape 13 : créer mon compte ────────────────────────────────────────────

    static CloudClient.Session _inscription;   // code vérifié, compte pas encore prêt
    static string _inscriptionEmail;
    static bool _inscriptionCreee;

    /// Retour du 25/10 : l'adresse appartient-elle déjà à un groupe ? Demandé avant l'envoi du code, sans
    /// être connecté (le serveur ne dit que oui ou non). Pas de réponse : faux, `inscrire` refusera après le code.
    public static async Task<bool> AdresseDejaMembre(string email)
    {
        var r = await CloudClient.FonctionJson(Url, Cle, null, "adresse_deja_membre", RegleInscription.CorpsAdresse(email));
        return r.Ok && r.Corps.Trim() == "true";
    }

    /// Retour du 25/10 (AO8) : la société est-elle déjà sur CIPL ? Même principe qu'AdresseDejaMembre.
    public static async Task<bool> SiretDejaSurCipl(string siret)
    {
        var r = await CloudClient.FonctionJson(Url, Cle, null, "siret_deja_sur_cipl", RegleInscription.CorpsSiret(siret));
        return r.Ok && r.Corps.Trim() == "true";
    }

    public static async Task<string> DemanderCodeInscription(string email)
    {
        _inscription = null;
        _inscriptionCreee = false;
        var r = await CloudClient.DemanderCodeInscription(Url, Cle, (email ?? "").Trim());
        return r.Ok ? null : RegleMotDePasse.MotifEnvoi(r.Code, r.Erreur);
    }

    /// Le code, puis le groupe et l'entreprise (fonction inscrire), puis le mot de passe. Null si le
    /// compte est prêt : la session est ouverte et gardée sur ce PC. Après un refus, on reprend où l'on
    /// en était sans nouveau code (un code ne sert qu'une fois) ; une adresse déjà membre d'un groupe
    /// est refusée sans que son mot de passe change.
    public static async Task<(string motif, string[] champs)> Inscrire(string email, string code, string motDePasse,
                                              string type, string nom, string siret, string monNom, ReglageData reglages)
    {
        email = (email ?? "").Trim();
        if (_inscription == null || _inscriptionEmail != email.ToLowerInvariant() || _inscription.ExpireLe < DateTime.Now)
        {
            var v = await CloudClient.VerifierCode(Url, Cle, email, RegleMotDePasse.Code(code), "email");
            var s = v.Ok ? CloudClient.LireSession(v.Corps) : null;
            if (s == null) return v.Ok ? ("Réponse du serveur illisible.", null)
                                       : (RegleMotDePasse.MotifCode(v.Code, v.Erreur), RegleMotDePasse.ChampsCode(v.Code));
            _inscription = s;
            _inscriptionEmail = email.ToLowerInvariant();
            _inscriptionCreee = false;
        }
        if (!_inscriptionCreee)
        {
            var r = await CloudClient.FonctionJson(Url, Cle, _inscription.access_token, "inscrire",
                RegleInscription.Corps(type, nom, siret, monNom, JsonUtility.ToJson(reglages)));
            var res = r.Ok ? JsonUtility.FromJson<RegleInscription.Resultat>(r.Corps) : null;
            if (res?.ok != true)
            {
                if (res?.refus == "deja_membre") _inscription = null;   // compte existant : sa session n'est pas gardée
                return r.Ok ? (RegleInscription.Motif(res), RegleInscription.Champs(res)) : ("Création du compte impossible : " + r.Erreur, null);
            }
            _inscriptionCreee = true;
        }
        var p = await CloudClient.ChangerMotDePasse(Url, Cle, _inscription.access_token, motDePasse ?? "");
        if (!p.Ok) return (RegleMotDePasse.MotifChangement(p.Code, p.Erreur), RegleMotDePasse.ChampsChangement(p.Code));

        Session = _inscription;
        _inscription = null;
        _inscriptionCreee = false;
        _memoriser = true;
        Memoriser();
        return (null, null);
    }

    // ── Étape 13b : ce que CIPL retient pour la personne ───────────────────────

    public const string PremiersPasMasques = nameof(CloudClient.Metadonnees.cipl_premiers_pas_masques),
                        VisiteVue = nameof(CloudClient.Metadonnees.cipl_visite_vue);

    public static CloudClient.Metadonnees Retenu => Session?.user?.user_metadata ?? new CloudClient.Metadonnees();

    /// Retient un choix (PremiersPasMasques, VisiteVue) dans le compte de la personne : il vaut sur tous
    /// ses PC. Gardé pour cette session même si le serveur ne répond pas.
    /// `valeur` faux : le choix est défait (retour du 25/10 : « Masquer » n'avait pas de retour).
    public static async void Retenir(string nom, bool valeur = true)
    {
        var u = Session?.user;
        if (u == null) return;
        if (u.user_metadata == null) u.user_metadata = new CloudClient.Metadonnees();
        if (nom == PremiersPasMasques) u.user_metadata.cipl_premiers_pas_masques = valeur;
        else if (nom == VisiteVue) u.user_metadata.cipl_visite_vue = valeur;
        var r = await CloudClient.RetenirMetadonnee(Url, Cle, await Jeton(), nom, valeur);
        if (!r.Ok) Debug.LogWarning($"[Cloud] « {nom} » non retenu dans le compte : {r.Erreur}");
    }

    /// Se déconnecter : oublie la session et efface le jeton gardé sur ce PC.
    public static void Oublier()
    {
        Session = null;
        Entreprise = null;
        _memoriser = false;
        EffacerFichier();
    }

    /// Étape 15b : sur Mac, le jeton est dans le trousseau, au nom du fichier qu'il aurait sous Windows.
    static string Compte => Path.GetFileNameWithoutExtension(Fichier);

    /// Le jeton gardé sur cet ordinateur : null s'il n'y en a pas, "" s'il est illisible (autre compte,
    /// autre PC). Windows : chiffré par Dpapi dans `Fichier` ; Mac : dans le trousseau.
    static string JetonGarde()
    {
        if (Plateforme.Mac) return Trousseau.Lire(Compte);
        if (!File.Exists(Fichier)) return null;
        byte[] clair;
        try { clair = Dpapi.Deproteger(File.ReadAllBytes(Fichier)); }
        catch (Exception) { clair = null; }
        return clair == null ? "" : Encoding.UTF8.GetString(clair);
    }

    static void Memoriser()
    {
        if (Plateforme.Mac)
        {
            if (!Trousseau.Ecrire(Compte, Session.refresh_token ?? "")) Debug.LogWarning("[SessionCloud] Trousseau indisponible : connexion non gardée.");
            return;
        }
        var chiffre = Dpapi.Proteger(Encoding.UTF8.GetBytes(Session.refresh_token ?? ""));
        if (chiffre == null) { Debug.LogWarning("[SessionCloud] Chiffrement Windows indisponible : connexion non gardée."); return; }
        try { File.WriteAllBytes(Fichier, chiffre); }
        catch (Exception e) { Debug.LogWarning("[SessionCloud] Connexion non gardée : " + e.Message); }
    }

    static void EffacerFichier()
    {
        if (Plateforme.Mac) { Trousseau.Effacer(Compte); return; }
        try { if (File.Exists(Fichier)) File.Delete(Fichier); }
        catch (Exception e) { Debug.LogWarning("[SessionCloud] " + e.Message); }
    }
}
