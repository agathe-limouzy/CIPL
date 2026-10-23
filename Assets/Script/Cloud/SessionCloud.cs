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

    /// Null si le mot de passe convient, sinon le motif.
    public static string Refus(string mdp, string confirmation)
    {
        if ((mdp ?? "").Length < Minimum) return $"Le mot de passe doit faire au moins {Minimum} caractères.";
        if (mdp != confirmation) return "Les deux mots de passe ne sont pas identiques.";
        return null;
    }

    public static string RefusAdresse(string email) =>
        string.IsNullOrWhiteSpace(email) || !email.Contains("@") ? "Saisissez l'adresse e-mail de votre compte." : null;

    /// Le code de l'e-mail sans espaces ni tirets (« 123 456 » → « 123456 »).
    public static string Code(string saisie) => new string((saisie ?? "").Where(char.IsDigit).ToArray());

    /// Le code et le nouveau mot de passe : null si complets, sinon ce qu'il manque. Supabase envoie
    /// 6 chiffres par défaut (réglable jusqu'à 10).
    public static string RefusCode(string code, string mdp, string confirmation)
    {
        int n = Code(code).Length;
        if (n < 6 || n > 10) return "Saisissez le code à 6 chiffres reçu par e-mail.";
        return Refus(mdp, confirmation);
    }

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

    /// Null si la connexion a réussi, sinon le motif à afficher.
    public static async Task<string> SeConnecter(string email, string motDePasse, bool memoriser)
    {
        var r = await CloudClient.SeConnecter(Url, Cle, (email ?? "").Trim(), motDePasse ?? "");
        if (!r.Ok)
            return r.Code == 400 ? "E-mail ou mot de passe incorrect." : "Connexion impossible : " + r.Erreur;
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
        if (!File.Exists(Fichier)) return false;
        byte[] clair;
        try { clair = Dpapi.Deproteger(File.ReadAllBytes(Fichier)); }
        catch (Exception) { clair = null; }
        if (clair == null) { EffacerFichier(); return false; }

        var r = await CloudClient.Renouveler(Url, Cle, Encoding.UTF8.GetString(clair));
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
        if (u == null || string.IsNullOrEmpty(u.id) || !File.Exists(Fichier)) return false;
        byte[] clair;
        try { clair = Dpapi.Deproteger(File.ReadAllBytes(Fichier)); }
        catch (Exception) { clair = null; }
        if (clair == null) return false;
        Session = new CloudClient.Session { access_token = "", refresh_token = Encoding.UTF8.GetString(clair), expires_at = 0, user = u };
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
    public static async Task<string> ChangerMotDePasse(string email, string code, string motDePasse)
    {
        email = (email ?? "").Trim();
        // Un code ne sert qu'une fois : vérifié, sa session attend un mot de passe accepté (refusé s'il
        // est identique à l'ancien), sans redemander de code.
        if (_recuperation == null || _recuperationEmail != email.ToLowerInvariant() || _recuperation.ExpireLe < DateTime.Now)
        {
            var v = await CloudClient.VerifierCode(Url, Cle, email, RegleMotDePasse.Code(code));
            var s = v.Ok ? CloudClient.LireSession(v.Corps) : null;
            if (s == null) return v.Ok ? "Réponse du serveur illisible." : RegleMotDePasse.MotifCode(v.Code, v.Erreur);
            _recuperation = s;
            _recuperationEmail = email.ToLowerInvariant();
        }
        var r = await CloudClient.ChangerMotDePasse(Url, Cle, _recuperation.access_token, motDePasse ?? "");
        if (!r.Ok) return RegleMotDePasse.MotifChangement(r.Code, r.Erreur);

        bool garder = Session == null || _memoriser;
        Session = _recuperation;
        _recuperation = null;
        _memoriser = garder;
        if (garder) Memoriser();
        return null;
    }

    /// Se déconnecter : oublie la session et efface le jeton gardé sur ce PC.
    public static void Oublier()
    {
        Session = null;
        Entreprise = null;
        _memoriser = false;
        EffacerFichier();
    }

    static void Memoriser()
    {
        var chiffre = Dpapi.Proteger(Encoding.UTF8.GetBytes(Session.refresh_token ?? ""));
        if (chiffre == null) { Debug.LogWarning("[SessionCloud] Chiffrement Windows indisponible : connexion non gardée."); return; }
        try { File.WriteAllBytes(Fichier, chiffre); }
        catch (Exception e) { Debug.LogWarning("[SessionCloud] Connexion non gardée : " + e.Message); }
    }

    static void EffacerFichier()
    {
        try { if (File.Exists(Fichier)) File.Delete(Fichier); }
        catch (Exception e) { Debug.LogWarning("[SessionCloud] " + e.Message); }
    }
}
