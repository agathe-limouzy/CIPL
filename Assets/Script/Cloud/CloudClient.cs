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
    [Serializable] public class Utilisateur { public string id; public string email; }

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

    public static Task<Reponse> Renouveler(string url, string cle, string refreshToken) =>
        Envoyer("POST", url + "/auth/v1/token?grant_type=refresh_token", cle, null,
                JsonUtility.ToJson(new Renouvellement { refresh_token = refreshToken }));

    /// Fonction SQL (POST /rest/v1/rpc/<nom>), exécutée avec les droits de l'utilisateur du jeton.
    public static Task<Reponse> Fonction(string url, string cle, string jeton, string nom, object parametres) =>
        Envoyer("POST", url + "/rest/v1/rpc/" + nom, cle, jeton, JsonUtility.ToJson(parametres));

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

    static async Task<Reponse> Envoyer(string methode, string adresse, string cle, string jeton, string corps,
                                       string prefer = null)
    {
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
