using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// Appels REST au projet Supabase : comptes (/auth/v1), fonctions SQL et tables (/rest/v1).
/// Étape 1 (essai technique) : seule la fenêtre CIPL › Essai cloud l'appelle ; rien dans
/// l'application ne passe encore par le cloud. Voir CONCEPTION_MULTIUTILISATEUR.md.
public static class CloudClient
{
    [Serializable] public class Utilisateur { public string id; public string email; public Metadonnees user_metadata; }

    /// Étape 13b : ce que CIPL retient pour la personne, dans son compte Supabase (donc sur tous ses
    /// PC, décision du 25/10) : carte « Premiers pas » masquée, visite guidée déjà vue.
    [Serializable] public class Metadonnees { public bool cipl_premiers_pas_masques, cipl_visite_vue; }

    /// Réponse de /auth/v1/token : le jeton (« badge ») valable environ 1 h, et de quoi le renouveler.
    [Serializable] public class Session
    {
        public string access_token;
        public string refresh_token;
        public long expires_at;          // secondes Unix
        public Utilisateur user;

        public DateTime ExpireLe => DateTimeOffset.FromUnixTimeSeconds(expires_at).LocalDateTime;
        public bool Valide => !string.IsNullOrEmpty(access_token) && user != null && !string.IsNullOrEmpty(user.id);
    }

    /// Réponse de essai_prendre_verrou : qui détient la fiche, et si c'est nous.
    [Serializable] public class Verrou { public bool obtenu; public string nom, motif, pris_le, expire_le; }

    /// Ligne de la table essai_fiches (essai technique seulement).
    [Serializable] public class FicheEssai { public string id; public double loyer; public string maj_par; public int version; }

    public class Reponse { public bool Ok; public long Code; public string Corps = ""; public string Erreur = ""; }

    [Serializable] class Identifiants { public string email, password; }
    [Serializable] class Renouvellement { public string refresh_token; }
    [Serializable] class ErreurApi { public string msg, message, error_description, error; }
    [Serializable] class Fiches { public FicheEssai[] items; }

    // ── Appels ─────────────────────────────────────────────────────────────────

    public static Task<Reponse> SeConnecter(string url, string cle, string email, string motDePasse) =>
        Envoyer("POST", url + "/auth/v1/token?grant_type=password", cle, null,
                JsonUtility.ToJson(new Identifiants { email = email, password = motDePasse }));

    /// Étape 9b : création du compte d'un invité. « Confirm email » décoché dans Supabase : la
    /// réponse porte directement la session (le code d'invitation a prouvé la boîte mail).
    public static Task<Reponse> Inscrire(string url, string cle, string email, string motDePasse) =>
        Envoyer("POST", url + "/auth/v1/signup", cle, null,
                JsonUtility.ToJson(new Identifiants { email = email, password = motDePasse }));

    // Étape 12 : mot de passe oublié. Le modèle « Reset Password » de Supabase montre le code ({{ .Token }}).
    [Serializable] class Adresse { public string email; }
    [Serializable] class CodeRecupere { public string type, email, token; }
    [Serializable] class NouveauMotDePasse { public string password; }
    [Serializable] class DemandeInscription { public string email; public bool create_user; }

    public static string CorpsDemande(string email) => JsonUtility.ToJson(new Adresse { email = email });
    public static string CorpsDemandeInscription(string email) => JsonUtility.ToJson(new DemandeInscription { email = email, create_user = true });
    /// `type` : « recovery » (mot de passe oublié), « email » (code d'inscription, étape 13).
    public static string CorpsCode(string email, string code, string type = "recovery") =>
        JsonUtility.ToJson(new CodeRecupere { type = type, email = email, token = code });

    /// Étape 13 : code d'inscription (modèles « Confirm signup » et « Magic Link » de Supabase,
    /// {{ .Token }}) ; le compte est créé s'il n'existe pas, sans mot de passe tant que le code n'est pas saisi.
    public static Task<Reponse> DemanderCodeInscription(string url, string cle, string email) =>
        Envoyer("POST", url + "/auth/v1/otp", cle, null, CorpsDemandeInscription(email));

    /// Envoie un code de récupération ; même réponse que le compte existe ou non.
    public static Task<Reponse> DemanderCode(string url, string cle, string email) =>
        Envoyer("POST", url + "/auth/v1/recover", cle, null, CorpsDemande(email));

    /// Le code contre une session : il prouve la boîte mail.
    public static Task<Reponse> VerifierCode(string url, string cle, string email, string code, string type = "recovery") =>
        Envoyer("POST", url + "/auth/v1/verify", cle, null, CorpsCode(email, code, type));

    public static Task<Reponse> ChangerMotDePasse(string url, string cle, string jeton, string motDePasse) =>
        Envoyer("PUT", url + "/auth/v1/user", cle, jeton, JsonUtility.ToJson(new NouveauMotDePasse { password = motDePasse }));

    /// Étape 13b : pose un choix (`nom` = champ de Metadonnees) à vrai dans le compte ; Supabase
    /// fusionne avec ce qui y est déjà.
    public static string CorpsMetadonnee(string nom, bool valeur = true) => "{\"data\":{\"" + nom + "\":" + (valeur ? "true" : "false") + "}}";

    public static Task<Reponse> RetenirMetadonnee(string url, string cle, string jeton, string nom, bool valeur = true) =>
        Envoyer("PUT", url + "/auth/v1/user", cle, jeton, CorpsMetadonnee(nom, valeur));

    public static Task<Reponse> Renouveler(string url, string cle, string refreshToken) =>
        Envoyer("POST", url + "/auth/v1/token?grant_type=refresh_token", cle, null,
                JsonUtility.ToJson(new Renouvellement { refresh_token = refreshToken }));

    /// Fonction SQL (POST /rest/v1/rpc/<nom>), exécutée avec les droits de l'utilisateur du jeton.
    public static Task<Reponse> Fonction(string url, string cle, string jeton, string nom, object parametres) =>
        Envoyer("POST", url + "/rest/v1/rpc/" + nom, cle, jeton, JsonUtility.ToJson(parametres));

    /// Étape 9c2 : fonction serveur (Edge Function, POST /functions/v1/<nom>), avec le jeton de l'utilisateur.
    public static Task<Reponse> FonctionServeur(string url, string cle, string jeton, string nom, string corpsJson) =>
        Envoyer("POST", url + "/functions/v1/" + nom, cle, jeton, corpsJson);

    /// Étape 16b : fonction serveur qui répond par un fichier (l'image d'une carte). Les octets sont
    /// rendus aussi quand elle répond un refus en JSON avec un statut 200.
    public static Task<(Reponse rep, byte[] octets)> FonctionBrute(string url, string cle, string jeton, string nom, string corpsJson) =>
        EnvoyerBrut("POST", url + "/functions/v1/" + nom, cle, jeton, Encoding.UTF8.GetBytes(corpsJson));

    /// Même chose avec des paramètres déjà en JSON (quand l'un d'eux est un objet JSON de CIPL).
    public static Task<Reponse> FonctionJson(string url, string cle, string jeton, string nom, string corpsJson) =>
        Envoyer("POST", url + "/rest/v1/rpc/" + nom, cle, jeton, corpsJson);

    /// Lecture (GET) ou écriture (PATCH…) d'une table. `filtre` au format PostgREST :
    /// "id=eq.volteo&version=eq.3". La réponse est toujours un tableau JSON.
    public static Task<Reponse> Table(string methode, string url, string cle, string jeton, string table,
                                      string filtre, string corpsJson = null) =>
        Envoyer(methode, url + "/rest/v1/" + table + (string.IsNullOrEmpty(filtre) ? "" : "?" + filtre),
                cle, jeton, corpsJson, "return=representation");

    /// Insère des lignes (tableau JSON) ; une ligne déjà présente (même clé) est laissée
    /// telle quelle. Un import interrompu peut ainsi être relancé sans doublon.
    public static Task<Reponse> Inserer(string url, string cle, string jeton, string table, string corpsJson) =>
        Envoyer("POST", url + "/rest/v1/" + table, cle, jeton, corpsJson, "resolution=ignore-duplicates,return=minimal");

    // ── Étape 6 : documents (stockage Supabase, compartiment privé) ────────────

    /// Dépose un fichier. Déjà présent : ce n'est pas une erreur (la clé est l'empreinte du
    /// contenu, un même contenu n'est envoyé qu'une fois).
    public static async Task<Reponse> DeposerFichier(string url, string cle, string jeton, string compartiment, string objet, byte[] contenu)
    {
        var (rep, _) = await EnvoyerBrut("POST", url + "/storage/v1/object/" + compartiment + "/" + objet, cle, jeton, contenu);
        if (!rep.Ok && (rep.Code == 409 || rep.Corps.Contains("Duplicate") || rep.Corps.Contains("already exists"))) rep.Ok = true;
        return rep;
    }

    /// Lit un fichier ; contenu null si l'appel échoue (motif dans `erreur`).
    public static async Task<(byte[] contenu, string erreur)> LireFichier(string url, string cle, string jeton, string compartiment, string objet)
    {
        var (rep, octets) = await EnvoyerBrut("GET", url + "/storage/v1/object/authenticated/" + compartiment + "/" + objet, cle, jeton, null);
        return rep.Ok ? (octets, null) : (null, rep.Erreur);
    }

    /// Étape 8 : un appel léger pour savoir si le serveur répond (« Réessayer », toutes les 30 s).
    public static Task<Reponse> Sonder(string url, string cle) => Envoyer("GET", url + "/auth/v1/health", cle, null, null);

    static Reponse SansReseau() => new Reponse { Ok = false, Code = 0, Erreur = "Serveur injoignable (hors ligne simulé)" };

    static async Task<(Reponse rep, byte[] octets)> EnvoyerBrut(string methode, string adresse, string cle, string jeton, byte[] corps)
    {
        if (HorsLigne.Simulee) { HorsLigne.Constater(false, 0); return (SansReseau(), null); }
        using (var r = new UnityWebRequest(adresse, methode) { downloadHandler = new DownloadHandlerBuffer() })
        {
            if (corps != null)
            {
                r.uploadHandler = new UploadHandlerRaw(corps);
                r.SetRequestHeader("Content-Type", "application/octet-stream");
            }
            r.SetRequestHeader("apikey", cle);
            if (!string.IsNullOrEmpty(jeton)) r.SetRequestHeader("Authorization", "Bearer " + jeton);
            r.timeout = 120;   // une photo peut peser plusieurs Mo

            var fin = new TaskCompletionSource<bool>();
            r.SendWebRequest().completed += _ => fin.TrySetResult(true);
            await fin.Task;

            var rep = new Reponse { Code = r.responseCode, Ok = r.result == UnityWebRequest.Result.Success };
            if (!rep.Ok) { rep.Corps = r.downloadHandler?.text ?? ""; rep.Erreur = LireErreur(r.responseCode, rep.Corps, r.error); }
            HorsLigne.Constater(rep.Ok, rep.Code);
            return (rep, rep.Ok ? r.downloadHandler?.data : null);
        }
    }

    static async Task<Reponse> Envoyer(string methode, string adresse, string cle, string jeton, string corps,
                                       string prefer = null)
    {
        if (HorsLigne.Simulee) { HorsLigne.Constater(false, 0); return SansReseau(); }
        using (var r = new UnityWebRequest(adresse, methode) { downloadHandler = new DownloadHandlerBuffer() })
        {
            if (corps != null)
            {
                r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(corps));
                r.SetRequestHeader("Content-Type", "application/json");
            }
            r.SetRequestHeader("apikey", cle);
            if (!string.IsNullOrEmpty(jeton)) r.SetRequestHeader("Authorization", "Bearer " + jeton);
            if (prefer != null) r.SetRequestHeader("Prefer", prefer);
            r.timeout = 15;

            var fin = new TaskCompletionSource<bool>();
            r.SendWebRequest().completed += _ => fin.TrySetResult(true);
            await fin.Task;

            var rep = new Reponse { Code = r.responseCode, Corps = r.downloadHandler?.text ?? "" };
            rep.Ok = r.result == UnityWebRequest.Result.Success;
            if (!rep.Ok) rep.Erreur = LireErreur(r.responseCode, rep.Corps, r.error);
            HorsLigne.Constater(rep.Ok, rep.Code);   // étape 8
            if (!rep.Ok) AbonnementCloud.Constater(rep.Erreur);   // étape 9c1 : expiré pendant la session
            return rep;
        }
    }

    // ── Lecture des réponses (sans réseau : testées en EditMode) ───────────────

    public static Session LireSession(string json)
    {
        try { var s = JsonUtility.FromJson<Session>(json); return s != null && s.Valide ? s : null; }
        catch { return null; }
    }

    public static Verrou LireVerrou(string json)
    {
        try { return JsonUtility.FromJson<Verrou>(json); }
        catch { return null; }
    }

    /// PostgREST renvoie un tableau ; JsonUtility ne lit qu'un objet, d'où l'enveloppe.
    public static FicheEssai[] LireFiches(string jsonTableau)
    {
        try { return JsonUtility.FromJson<Fiches>("{\"items\":" + jsonTableau + "}")?.items; }
        catch { return null; }
    }

    /// Message lisible d'un refus. Selon le service, Supabase met le motif dans « msg »,
    /// « message » ou « error_description » ; code 0 = pas de réponse du tout.
    public static string LireErreur(long code, string corps, string erreurReseau = null)
    {
        if (code == 0)
            return "Serveur injoignable" + (string.IsNullOrEmpty(erreurReseau) ? "" : $" ({erreurReseau})");

        string detail = "";
        try
        {
            var e = JsonUtility.FromJson<ErreurApi>(corps ?? "");
            if (e != null) detail = Premier(e.msg, e.error_description, e.message, e.error);
        }
        catch { /* corps non JSON (page d'erreur HTML…) : on l'affiche tel quel ci-dessous */ }

        if (string.IsNullOrEmpty(detail))
            detail = corps != null && corps.Length > 200 ? corps.Substring(0, 200) + "…" : corps ?? "";
        return $"HTTP {code} {detail}".Trim();
    }

    /// Adresse de l'API du projet. On colle facilement celle du tableau de bord
    /// (https://supabase.com/dashboard/project/<id>/…) : on en tire https://<id>.supabase.co.
    public static string AdresseProjet(string saisie)
    {
        string s = (saisie ?? "").Trim().TrimEnd('/');
        var m = System.Text.RegularExpressions.Regex.Match(s, @"supabase\.com/dashboard/project/([a-z0-9]+)");
        return m.Success ? $"https://{m.Groups[1].Value}.supabase.co" : s;
    }

    /// Date ISO 8601 du serveur (« 2026-10-12T12:32:05.123+00:00 ») en heure locale.
    public static DateTime? Date(string iso) =>
        DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.LocalDateTime
            : (DateTime?)null;

    static string Premier(params string[] valeurs)
    {
        foreach (var v in valeurs) if (!string.IsNullOrEmpty(v)) return v;
        return "";
    }
}
