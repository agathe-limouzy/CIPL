using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

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
