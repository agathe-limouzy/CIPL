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
    public bool EstAdmin => role == "admin";
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
    static string Fichier => Path.Combine(Application.persistentDataPath, "cipl_session.dat");

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
        if (s == null) { EffacerFichier(); return false; }
        Session = s;
        _memoriser = true;
        Memoriser();   // le serveur remplace le jeton de renouvellement à chaque usage
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
