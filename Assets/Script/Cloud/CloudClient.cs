using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// Appels REST au projet Supabase : comptes (/auth/v1), fonctions SQL et tables (/rest/v1), fonctions
/// serveur (/functions/v1) et stockage. Toute l'application passe par ici (SessionCloud, EcritureCloud,
/// DocumentsCloud…). Voir CONCEPTION_MULTIUTILISATEUR.md.
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
        public long expires_in;          // secondes de validité à la réception
        public Utilisateur user;

        // Revue 17 (D1-10) : l'expiration est calculée à la réception avec l'horloge de CE PC (maintenant + expires_in),
        // plus avec expires_at (horloge du serveur) : un PC en retard de 5 min présentait un jeton expiré à chaque heure.
        // Sans réception (session reprise hors ligne) : valeur minimale, le jeton est renouvelé au premier appel.
        [NonSerialized] public DateTime ExpireLe;
        public bool Valide => !string.IsNullOrEmpty(access_token) && user != null && !string.IsNullOrEmpty(user.id);
    }

    /// Réponse de prendre_verrou : qui détient la fiche, et si c'est nous.
    [Serializable] public class Verrou { public bool obtenu; public string nom, motif, pris_le, expire_le; }

    public class Reponse { public bool Ok; public long Code; public string Corps = ""; public string Erreur = ""; }

    [Serializable] class Identifiants { public string email, password; }
    [Serializable] class Renouvellement { public string refresh_token; }
    [Serializable] class ErreurApi { public string msg, message, error_description, error; }

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

    /// Une chaîne en JSON (revue 16c, D3-14) : guillemets, barres obliques inverses et caractères de contrôle
    /// (tabulation d'un copier-coller…) échappés. Avant, trois copies n'échappaient que les deux premiers : un
    /// nom collé depuis un tableur donnait un JSON invalide et un refus illisible.
    public static string Q(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s ?? "")
        {
            if (c == '"') sb.Append("\\\"");
            else if (c == '\\') sb.Append("\\\\");
            else if (c == '\n') sb.Append("\\n");
            else if (c == '\r') sb.Append("\\r");
            else if (c == '\t') sb.Append("\\t");
            else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }

    /// Fonction SQL (POST /rest/v1/rpc/<nom>), exécutée avec les droits de l'utilisateur du jeton.
    public static Task<Reponse> Fonction(string url, string cle, string jeton, string nom, object parametres) =>
        Envoyer("POST", url + "/rest/v1/rpc/" + nom, cle, jeton, JsonUtility.ToJson(parametres));

    /// Étape 9c2 : fonction serveur (Edge Function, POST /functions/v1/<nom>), avec le jeton de l'utilisateur.
    public static Task<Reponse> FonctionServeur(string url, string cle, string jeton, string nom, string corpsJson) =>
        Envoyer("POST", url + "/functions/v1/" + nom, cle, jeton, corpsJson, fonction: true);

    /// Étape 16b : fonction serveur qui répond par un fichier (l'image d'une carte). Les octets sont
    /// rendus aussi quand elle répond un refus en JSON avec un statut 200.
    /// Revue 17 (D1-17, lot D) : `fonction: true`, comme FonctionServeur : un délai dépassé (carte lente, envoi Super PDP
    /// de plus de 120 s) n'est pas une panne du cloud, CIPL ne passe pas « Hors ligne ».
    public static Task<(Reponse rep, byte[] octets)> FonctionBrute(string url, string cle, string jeton, string nom, string corpsJson) =>
        EnvoyerBrut("POST", url + "/functions/v1/" + nom, cle, jeton, Encoding.UTF8.GetBytes(corpsJson), fonction: true);

    /// Même chose avec des paramètres déjà en JSON (quand l'un d'eux est un objet JSON de CIPL).
    public static Task<Reponse> FonctionJson(string url, string cle, string jeton, string nom, string corpsJson) =>
        Envoyer("POST", url + "/rest/v1/rpc/" + nom, cle, jeton, corpsJson);

    /// Lecture (GET) ou écriture (PATCH…) d'une table. `filtre` au format PostgREST :
    /// "id=eq.volteo&version=eq.3". La réponse est toujours un tableau JSON.
    public static Task<Reponse> Table(string methode, string url, string cle, string jeton, string table,
                                      string filtre, string corpsJson = null) =>
        Envoyer(methode, url + "/rest/v1/" + table + (string.IsNullOrEmpty(filtre) ? "" : "?" + filtre),
                cle, jeton, corpsJson, "return=representation");

    // ── Revue 17 (D2-06, lot D) : lire une table en entier, page après page ────
    // Une lecture seule est plafonnée par le serveur (« Max rows », 1 000 lignes par défaut sur Supabase), sans le dire :
    // au-delà, la liste des documents était tronquée, des suppressions étaient défaites et des documents n'arrivaient jamais.

    const int PageTable = 1000;

    /// Toutes les lignes d'une table. Même `filtre` que Table("GET", …) ; il DOIT porter un `order=` total (clé unique),
    /// sans quoi deux pages peuvent se recouvrir ou sauter une ligne. L'offset avance du nombre de lignes RÉELLEMENT
    /// reçues (le plafond du serveur peut être plus bas que la page demandée) ; la lecture s'arrête sur une page vide.
    /// La lecture n'est pas instantanée, même avec un ordre total : une ligne insérée par un collègue entre deux pages,
    /// rangée avant la fin, fait revenir une ligne déjà lue (doublon). L'APPELANT DÉDOUBLE PAR CLÉ. Une ligne supprimée
    /// entre deux pages pleines peut en faire sauter une (jamais pour documents : aucune ligne n'y est supprimée).
    /// Rend Ok avec Corps = un seul tableau JSON. Une page en échec ou illisible : la réponse est en échec, JAMAIS une
    /// liste partielle (elle ferait croire que le serveur n'a plus des documents, ou qu'un collègue a supprimé des fiches).
    public static async Task<Reponse> TableTout(string url, string cle, string jeton, string table, string filtre)
    {
        var pages = new List<string>();
        int offset = 0;
        // ponytail: 10 000 pages au plus (un serveur qui ignorerait l'offset tournerait sans fin) ; au-delà, échec.
        for (int page = 0; page < 10000; page++)
        {
            var r = await Table("GET", url, cle, jeton, table, filtre + "&limit=" + PageTable + "&offset=" + offset);
            if (!r.Ok) return r;
            int n = CompterElements(r.Corps);
            if (n < 0) return new Reponse { Ok = false, Code = r.Code, Corps = r.Corps, Erreur = "liste illisible (réponse inattendue du serveur)" };
            if (n == 0)
            {
                string tout = FusionnerPages(pages);
                return tout != null ? new Reponse { Ok = true, Code = r.Code, Corps = tout }
                                    : new Reponse { Ok = false, Code = r.Code, Erreur = "liste illisible (réponse inattendue du serveur)" };
            }
            pages.Add(r.Corps);
            offset += n;
        }
        return new Reponse { Ok = false, Code = 200, Erreur = "liste trop longue (lecture abandonnée)" };
    }

    /// Nombre d'éléments d'un tableau JSON, sans le décoder en objets (les chaînes et leurs échappements sont respectés :
    /// une virgule ou un crochet dans un nom de fichier ne compte pas) ; -1 si le texte n'est pas un tableau JSON entier.
    public static int CompterElements(string json)
    {
        if (json == null) return -1;
        int i = 0, fin = json.Length;
        while (i < fin && char.IsWhiteSpace(json[i])) i++;
        if (i >= fin || json[i] != '[') return -1;
        int profondeur = 0, virgules = 0;
        bool chaine = false, contenu = false;
        for (; i < fin; i++)
        {
            char c = json[i];
            if (chaine)
            {
                if (c == '\\') i++;            // le caractère échappé (« \" », « \\ ») ne ferme rien
                else if (c == '"') chaine = false;
                continue;
            }
            if (c == '"') { chaine = true; contenu = true; }
            else if (c == '[' || c == '{') { if (profondeur++ > 0) contenu = true; }
            else if (c == ']' || c == '}')
            {
                if (--profondeur > 0) continue;
                for (i++; i < fin; i++) if (!char.IsWhiteSpace(json[i])) return -1;   // rien après le tableau
                return c == ']' ? virgules + (contenu ? 1 : 0) : -1;
            }
            else if (c == ',') { if (profondeur == 1) virgules++; }
            else if (!char.IsWhiteSpace(c)) contenu = true;
        }
        return -1;   // tableau ou chaîne jamais fermé (réponse coupée)
    }

    /// Plusieurs tableaux JSON en un seul, dans l'ordre ; null si une page n'est pas un tableau (jamais de liste partielle).
    public static string FusionnerPages(IEnumerable<string> pages)
    {
        var sb = new StringBuilder("[");
        foreach (var p in pages)
        {
            string t = (p ?? "").Trim();
            if (t.Length < 2 || t[0] != '[' || t[t.Length - 1] != ']') return null;
            t = t.Substring(1, t.Length - 2).Trim();
            if (t.Length == 0) continue;
            if (sb.Length > 1) sb.Append(',');
            sb.Append(t);
        }
        return sb.Append(']').ToString();
    }

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
    /// `timeout` (BH3, 02/11) : avant d'ouvrir une entreprise, 5 s suffisent pour savoir si le réseau répond.
    public static Task<Reponse> Sonder(string url, string cle, int timeout = 15) => Envoyer("GET", url + "/auth/v1/health", cle, null, null, timeout: timeout);

    /// « Se déconnecter » (revue 16c, D3-13) : la session est aussi close côté serveur ; résultat ignoré.
    /// Revue 17 (D1-03) : `scope=local`, la session de CE PC seulement ; sans lui, Supabase fermait celles de tous les PC
    /// de la personne, qui ne l'apprenaient que par des « HTTP 401 » en chaîne.
    public static Task<Reponse> Deconnecter(string url, string cle, string jeton) => Envoyer("POST", url + "/auth/v1/logout?scope=local", cle, jeton, null);

    static Reponse SansReseau() => new Reponse { Ok = false, Code = 0, Erreur = "Serveur injoignable (hors ligne simulé)" };

    /// `fonction` (Revue 17, D1-17, lot D) : même drapeau que dans Envoyer (D1-12 de la 16c), il manquait ici.
    static async Task<(Reponse rep, byte[] octets)> EnvoyerBrut(string methode, string adresse, string cle, string jeton, byte[] corps,
                                                                bool fonction = false)
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
            HorsLigne.Constater(rep.Ok, rep.Code, fonction);
            return (rep, rep.Ok ? r.downloadHandler?.data : null);
        }
    }

    /// `fonction` (revue 16c, D1-12) : une fonction serveur ; un délai dépassé (code 0) n'est pas une panne du cloud.
    static async Task<Reponse> Envoyer(string methode, string adresse, string cle, string jeton, string corps,
                                       string prefer = null, bool fonction = false, int timeout = 15)
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
            r.timeout = timeout;

            var fin = new TaskCompletionSource<bool>();
            r.SendWebRequest().completed += _ => fin.TrySetResult(true);
            await fin.Task;

            var rep = new Reponse { Code = r.responseCode, Corps = r.downloadHandler?.text ?? "" };
            rep.Ok = r.result == UnityWebRequest.Result.Success;
            if (!rep.Ok) rep.Erreur = LireErreur(r.responseCode, rep.Corps, r.error);
            HorsLigne.Constater(rep.Ok, rep.Code, fonction);   // étape 8 ; D1-12 : une fonction lente ne coupe pas CIPL
            if (!rep.Ok) AbonnementCloud.Constater(rep.Erreur);   // étape 9c1 : expiré pendant la session
            return rep;
        }
    }

    // ── Lecture des réponses (sans réseau : testées en EditMode) ───────────────

    public static Session LireSession(string json)
    {
        try
        {
            var s = JsonUtility.FromJson<Session>(json);
            if (s == null || !s.Valide) return null;
            s.ExpireLe = DateTime.Now.AddSeconds(s.expires_in);   // Revue 17 (D1-10)
            return s;
        }
        catch { return null; }
    }

    public static Verrou LireVerrou(string json)
    {
        try { return JsonUtility.FromJson<Verrou>(json); }
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
