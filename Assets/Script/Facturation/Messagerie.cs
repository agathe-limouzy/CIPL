using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// Étape 16a (décisions des 25 et 27/10) : les factures, relances et invitations partent de l'adresse
/// du client sans mot de passe d'application. Gmail et Google Workspace : l'API Gmail ; Outlook,
/// Hotmail et Microsoft 365 : Microsoft Graph ; les autres messageries : SMTP (EmailService), serveur
/// rempli d'après l'adresse. L'admin relie l'adresse une fois, dans la fenêtre de Google ou de
/// Microsoft ; l'autorisation reste sur le serveur (Serveur/fonctions/messagerie.ts), chaque envoi
/// demande un jeton d'une heure qui ne permet que d'envoyer. Sur le cloud seulement.
public static class Messagerie
{
    public const string Google = "google", Microsoft = "microsoft";
    public const string ScopeGoogle = "openid email https://www.googleapis.com/auth/gmail.send";
    public const string ScopeMicrosoft = "offline_access openid email https://graph.microsoft.com/Mail.Send";   // le même dans messagerie.ts

    public static bool Relie(SmtpConfig s) => s != null && (s.fournisseur == Google || s.fournisseur == Microsoft);
    public static string Nom(string fournisseur) => fournisseur == Google ? "Google" : fournisseur == Microsoft ? "Microsoft" : "";

    // ── Quelle messagerie, d'après l'adresse ───────────────────────────────────

    /// `fournisseur` vide : SMTP. `serveur` : le serveur SMTP quand on le connaît (aussi pour Google et
    /// Microsoft, si l'admin préfère le SMTP). `nom` : la messagerie, pour l'écran. `inconnu` : domaine
    /// propre dont la messagerie ne se devine pas (filtre anti-spam devant, petit hébergeur) : le client
    /// choisit Google, Microsoft ou le SMTP (28/10). On ne cherche pas si l'adresse est un compte Google
    /// ou Microsoft (aucun moyen officiel). Constaté le 28/10 : un compte Microsoft créé sur un Gmail
    /// envoie, mais l'e-mail arrive dans les indésirables ; d'où le bouton conseillé en vert et
    /// l'avertissement sur l'autre (ReglagePanel). Un compte vraiment sans boîte : l'e-mail de test le dit (`Clair`).
    public class Choix { public string fournisseur = "", nom = "", serveur = ""; public bool inconnu; }

    static Choix C(string f, string nom, string serveur) => new Choix { fournisseur = f, nom = nom, serveur = serveur };

    static readonly Dictionary<string, Choix> Domaines = new Dictionary<string, Choix>
    {
        ["gmail.com"] = C(Google, "Gmail", "smtp.gmail.com"), ["googlemail.com"] = C(Google, "Gmail", "smtp.gmail.com"),
        ["outlook.com"] = C(Microsoft, "Outlook", "smtp-mail.outlook.com"), ["outlook.fr"] = C(Microsoft, "Outlook", "smtp-mail.outlook.com"),
        ["hotmail.com"] = C(Microsoft, "Hotmail", "smtp-mail.outlook.com"), ["hotmail.fr"] = C(Microsoft, "Hotmail", "smtp-mail.outlook.com"),
        ["live.com"] = C(Microsoft, "Outlook", "smtp-mail.outlook.com"), ["live.fr"] = C(Microsoft, "Outlook", "smtp-mail.outlook.com"),
        ["msn.com"] = C(Microsoft, "Outlook", "smtp-mail.outlook.com"),
        ["orange.fr"] = C("", "Orange", "smtp.orange.fr"), ["wanadoo.fr"] = C("", "Orange", "smtp.orange.fr"),
        ["free.fr"] = C("", "Free", "smtp.free.fr"),
        ["sfr.fr"] = C("", "SFR", "smtp.sfr.fr"), ["neuf.fr"] = C("", "SFR", "smtp.sfr.fr"),
        ["laposte.net"] = C("", "La Poste", "smtp.laposte.net"),
        ["icloud.com"] = C("", "iCloud", "smtp.mail.me.com"), ["me.com"] = C("", "iCloud", "smtp.mail.me.com"),
        ["yahoo.fr"] = C("", "Yahoo", "smtp.mail.yahoo.com"), ["yahoo.com"] = C("", "Yahoo", "smtp.mail.yahoo.com"),
        ["gmx.fr"] = C("", "GMX", "mail.gmx.com"), ["gmx.com"] = C("", "GMX", "mail.gmx.com"),
    };

    /// Le domaine propre d'un client (contact@sci-jbc.fr) : d'après le service qui reçoit son courrier.
    static readonly (string fin, Choix choix)[] ParMx =
    {
        ("google.com", C(Google, "Google Workspace", "smtp.gmail.com")), ("googlemail.com", C(Google, "Google Workspace", "smtp.gmail.com")),
        ("mail.protection.outlook.com", C(Microsoft, "Microsoft 365", "smtp.office365.com")),
        ("ovh.net", C("", "OVH", "ssl0.ovh.net")),
        ("ionos.fr", C("", "IONOS", "smtp.ionos.fr")), ("ionos.com", C("", "IONOS", "smtp.ionos.fr")),
        ("gandi.net", C("", "Gandi", "mail.gandi.net")),
        ("orange.fr", C("", "Orange", "smtp.orange.fr")), ("free.fr", C("", "Free", "smtp.free.fr")),
    };

    public static string Domaine(string adresse)
    {
        string a = (adresse ?? "").Trim();
        int i = a.LastIndexOf('@');
        return i < 0 ? "" : a.Substring(i + 1).TrimEnd('.').ToLowerInvariant();
    }

    /// Null : domaine inconnu, il faut lire les serveurs de courrier du domaine (`DevinerParMx`).
    public static Choix Deviner(string adresse) => Domaines.TryGetValue(Domaine(adresse), out var c) ? c : null;

    /// Le premier serveur de courrier reconnu ; sinon inconnu (Google, Microsoft ou SMTP au choix).
    public static Choix DevinerParMx(IEnumerable<string> mx)
    {
        foreach (var m in mx ?? Enumerable.Empty<string>())
        {
            string h = (m ?? "").Trim().TrimEnd('.').ToLowerInvariant();
            foreach (var (fin, choix) in ParMx)
                if (h == fin || h.EndsWith("." + fin)) return choix;
        }
        return new Choix { inconnu = true };
    }

    [Serializable] class Dns { public Enregistrement[] Answer; }
    [Serializable] class Enregistrement { public int type; public string data; }

    /// Réponse du DNS de Google (« 10 aspmx.l.google.com. ») → les serveurs de courrier (type 15).
    public static List<string> LireMx(string json)
    {
        try
        {
            return (JsonUtility.FromJson<Dns>(json ?? "")?.Answer ?? new Enregistrement[0])
                .Where(e => e.type == 15 && !string.IsNullOrWhiteSpace(e.data))
                .Select(e => e.data.Trim().Split(' ').Last()).ToList();
        }
        catch (Exception) { return new List<string>(); }
    }

    public static async Task<Choix> DevinerEnLigne(string adresse)
    {
        var c = Deviner(adresse);
        if (c != null) return c;
        string d = Domaine(adresse);
        if (d == "") return new Choix();
        var (code, corps, _) = await Requete("GET", "https://dns.google/resolve?type=MX&name=" + Uri.EscapeDataString(d), null, null);
        return DevinerParMx(code == 200 ? LireMx(corps) : null);
    }

    // ── Relier l'adresse (fenêtre de Google ou de Microsoft) ───────────────────

    public static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static string Aleatoire()
    {
        var b = new byte[32];
        using (var g = RandomNumberGenerator.Create()) g.GetBytes(b);
        return B64Url(b);
    }

    /// PKCE (RFC 7636) : le défi est l'empreinte SHA-256 du vérificateur, que seul ce PC connaît.
    public static string Defi(string verificateur)
    {
        using var s = SHA256.Create();
        return B64Url(s.ComputeHash(Encoding.ASCII.GetBytes(verificateur)));
    }

    /// Le retour de la fenêtre, sur ce PC. Google demande 127.0.0.1 ; Microsoft, « localhost » (déclaré tel quel).
    public static string Redirection(string fournisseur, int port) =>
        fournisseur == Google ? $"http://127.0.0.1:{port}/" : $"http://localhost:{port}/";

    public static string UrlAutorisation(string fournisseur, string clientId, string redirection, string defi, string etat, string adresse)
    {
        string E(string s) => Uri.EscapeDataString(s ?? "");
        string commun = $"client_id={E(clientId)}&redirect_uri={E(redirection)}&response_type=code"
                      + $"&code_challenge={E(defi)}&code_challenge_method=S256&state={E(etat)}"
                      + (string.IsNullOrWhiteSpace(adresse) ? "" : "&login_hint=" + E(adresse.Trim()));
        return fournisseur == Google
            ? "https://accounts.google.com/o/oauth2/v2/auth?" + commun + "&scope=" + E(ScopeGoogle) + "&access_type=offline&prompt=consent"
            : "https://login.microsoftonline.com/common/oauth2/v2.0/authorize?" + commun + "&scope=" + E(ScopeMicrosoft)
              + "&response_mode=query&prompt=select_account";
    }

    /// « GET /?code=…&state=… HTTP/1.1 » → les paramètres.
    public static Dictionary<string, string> Parametres(string ligne)
    {
        var p = new Dictionary<string, string>();
        var morceaux = (ligne ?? "").Split(' ');
        int q = morceaux.Length < 2 ? -1 : morceaux[1].IndexOf('?');
        if (q < 0) return p;
        foreach (var kv in morceaux[1].Substring(q + 1).Split('&'))
        {
            int e = kv.IndexOf('=');
            if (e > 0) p[Uri.UnescapeDataString(kv.Substring(0, e))] = Uri.UnescapeDataString(kv.Substring(e + 1).Replace('+', ' '));
        }
        return p;
    }

    const string PageRetour = "<!doctype html><meta charset=utf-8><title>CIPL</title>"
        + "<body style=\"font-family:sans-serif;margin:3em\"><h2>C'est fait</h2>"
        + "<p>Vous pouvez fermer cet onglet et revenir dans CIPL.</p></body>";

    /// Attend le retour de la fenêtre. Un navigateur ouvre parfois une connexion sans rien demander
    /// (préchargement, icône) : on attend celle qui porte « code » ou « error ».
    static async Task<Dictionary<string, string>> Attendre(List<TcpListener> ecoutes, TimeSpan delai)
    {
        var limite = Task.Delay(delai);
        var attentes = ecoutes.ToDictionary(l => l, l => l.AcceptTcpClientAsync());
        while (true)
        {
            var fini = await Task.WhenAny(attentes.Values.Cast<Task>().Append(limite));
            if (fini == limite) return null;
            var ecoute = attentes.First(kv => kv.Value == fini).Key;
            var client = attentes[ecoute].Result;
            attentes[ecoute] = ecoute.AcceptTcpClientAsync();
            using (client)
            {
                var flux = client.GetStream();
                var lecture = new StreamReader(flux, Encoding.ASCII).ReadLineAsync();
                if (await Task.WhenAny(lecture, Task.Delay(5000)) != lecture) continue;
                var p = Parametres(lecture.Result);
                if (!p.ContainsKey("code") && !p.ContainsKey("error")) continue;
                byte[] page = Encoding.UTF8.GetBytes(PageRetour);
                byte[] entete = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n"
                                                        + $"Content-Length: {page.Length}\r\nConnection: close\r\n\r\n");
                await flux.WriteAsync(entete, 0, entete.Length);
                await flux.WriteAsync(page, 0, page.Length);
                return p;
            }
        }
    }

    /// L'admin relie l'adresse : la fenêtre s'ouvre dans le navigateur, le code revient sur ce PC, le
    /// serveur l'échange contre l'autorisation et la garde. Renvoie l'adresse reliée, ou le motif.
    public static async Task<(string adresse, string refus)> Relier(string fournisseur, string adresse)
    {
        string nom = Nom(fournisseur);
        var etat = await Etat();
        string motif = Motif(etat, fournisseur);
        if (motif != null) return (null, motif);
        string clientId = fournisseur == Google ? etat.google : etat.microsoft;
        if (string.IsNullOrEmpty(clientId)) return (null, Motif(new Reponse { refus = "config" }, fournisseur));

        var ecoutes = new List<TcpListener> { new TcpListener(IPAddress.Loopback, 0) };
        ecoutes[0].Start();
        int port = ((IPEndPoint)ecoutes[0].LocalEndpoint).Port;
        try { var v6 = new TcpListener(IPAddress.IPv6Loopback, port); v6.Start(); ecoutes.Add(v6); }
        catch (Exception) { /* pas d'IPv6 sur ce PC : « localhost » passe par 127.0.0.1 */ }
        try
        {
            string verificateur = Aleatoire(), cle = Aleatoire(), redirection = Redirection(fournisseur, port);
            Application.OpenURL(UrlAutorisation(fournisseur, clientId, redirection, Defi(verificateur), cle, adresse));
            var p = await Attendre(ecoutes, TimeSpan.FromMinutes(5));
            if (p == null) return (null, $"La fenêtre de {nom} n'a pas répondu en 5 minutes : recommencez « Se connecter avec {nom} ».");
            if (p.TryGetValue("error", out var e))
                return (null, RefusFenetre(fournisseur, e, p.TryGetValue("error_description", out var d) ? d : ""));
            if (!p.TryGetValue("state", out var s) || s != cle || !p.TryGetValue("code", out var code))
                return (null, "Réponse inattendue de la fenêtre de connexion : recommencez.");
            var r = await Appeler(new Demande { action = "relier", fournisseur = fournisseur, code = code, verifier = verificateur, redirect = redirection });
            _jeton = null;
            return r.ok ? (r.adresse, null) : (null, Motif(r, fournisseur));
        }
        finally { foreach (var l in ecoutes) l.Stop(); }
    }

    /// Le refus rapporté par la fenêtre (« error=access_denied »…). Constaté le 28/10 avec @cipl.fr : tant
    /// que CIPL n'a pas d'éditeur vérifié (étape 17), un compte Microsoft 365 qui n'est pas administrateur
    /// tombe sur « Approbation administrateur requise ».
    public static string RefusFenetre(string fournisseur, string erreur, string description)
    {
        string nom = Nom(fournisseur);
        string refus = erreur == "access_denied" ? $"Autorisation refusée dans la fenêtre de {nom} : rien n'est relié."
            : $"{nom} a refusé la connexion : {erreur} {description}".Trim();
        return fournisseur != Microsoft ? refus
            : refus + "\nSi Microsoft a demandé « Approbation administrateur requise » (adresse professionnelle Microsoft 365), "
                    + "l'administrateur de votre Microsoft 365 doit d'abord autoriser CIPL ; recommencez ensuite avec votre adresse.";
    }

    public static async Task<string> Delier()
    {
        var r = await Appeler(new Demande { action = "delier" });
        _jeton = null;
        return r.ok ? null : Motif(r);
    }

    // ── La fonction serveur « messagerie » ─────────────────────────────────────

    [Serializable] class Demande { public string action, entreprise, fournisseur, code, verifier, redirect; }
    [Serializable] public class Reponse
    {
        public bool ok;
        public string refus, erreur, fournisseur, adresse, google, microsoft, access_token;
        public int expire_dans;
    }

    public static Task<Reponse> Etat() => Appeler(new Demande { action = "etat" });

    static async Task<Reponse> Appeler(Demande d)
    {
        if (!SessionCloud.Active || SessionCloud.Entreprise == null) return new Reponse { refus = "cloud" };
        d.entreprise = SessionCloud.Entreprise.id;
        var r = await CloudClient.FonctionServeur(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "messagerie", JsonUtility.ToJson(d));
        Reponse rep = null;
        try { rep = JsonUtility.FromJson<Reponse>(r.Corps); } catch (Exception) { /* pas du JSON : r.Erreur dit pourquoi */ }
        if (rep != null && (rep.ok || !string.IsNullOrEmpty(rep.refus))) return rep;
        return new Reponse { refus = "serveur", erreur = r.Ok ? "réponse inattendue" : r.Erreur };
    }

    /// Null si la fonction a répondu oui, sinon le motif pour l'écran.
    public static string Motif(Reponse r, string fournisseur = null)
    {
        string nom = Nom(string.IsNullOrEmpty(r?.fournisseur) ? fournisseur : r.fournisseur);
        string detail = string.IsNullOrEmpty(r?.erreur) ? "" : "\nDétail technique : " + r.erreur;
        switch (r?.refus)
        {
            case null: case "": return r == null ? "Pas de réponse du serveur de CIPL." : null;
            case "cloud": return "Relier une adresse demande d'être connecté(e) au cloud de CIPL.";
            case "acces": return "Cette entreprise ne vous est pas ouverte.";
            case "admin": return "Seul un administrateur peut relier ou délier l'adresse d'envoi.";
            case "config": return $"La connexion avec {nom} n'est pas encore réglée sur le serveur (Supabase › Edge Functions › Secrets).";
            case "echange": return $"{nom} n'a pas donné d'autorisation durable : recommencez « Se connecter avec {nom} »." + detail;
            case "non_relie": return "Aucune adresse d'envoi n'est reliée : l'admin doit la relier (Réglages → Intégrations → E-mail).";
            case "retire": return $"L'autorisation de {nom} a expiré ou a été retirée : l'admin doit relier de nouveau l'adresse (Réglages → Intégrations → E-mail).";
            case "jeton": return $"{nom} refuse de renouveler l'autorisation." + detail;
            default: return "La fonction « messagerie » du serveur n'a pas pu répondre." + (detail == "" ? "\nDétail technique : " + r.refus : detail);
        }
    }

    // ── Envoyer ─────────────────────────────────────────────────────────────────

    static string _jeton, _jetonEntreprise;
    static DateTime _jetonFin;

    /// Un jeton d'une heure, gardé en mémoire tant qu'il vaut (deux minutes de marge).
    static async Task<(string jeton, string refus)> Jeton()
    {
        string e = SessionCloud.Entreprise?.id;
        if (_jeton != null && _jetonEntreprise == e && DateTime.UtcNow < _jetonFin) return (_jeton, null);
        var r = await Appeler(new Demande { action = "jeton" });
        if (!r.ok) return (null, Motif(r));
        _jeton = r.access_token;
        _jetonEntreprise = e;
        _jetonFin = DateTime.UtcNow.AddSeconds(Math.Max(60, r.expire_dans - 120));
        return (_jeton, null);
    }

    /// Mêmes garanties que le SMTP (EmailService) : `Succes` seulement si Google ou Microsoft a
    /// accepté le message ; une pièce jointe illisible fait tout échouer.
    internal static async void Envoyer(EmailService.Envoi envoi, string fournisseur, string de, string nomDe,
                                       string a, string objet, string corps, List<string> fichiers)
    {
        try
        {
            var pieces = fichiers.Select(f => (Path.GetFileName(f), File.ReadAllBytes(f))).ToList();
            var (jeton, refus) = await Jeton();
            if (refus != null) { envoi.Echoue(refus); return; }
            var (code, rep, erreur) = fournisseur == Google
                ? await Requete("POST", "https://gmail.googleapis.com/gmail/v1/users/me/messages/send", jeton,
                                "{\"raw\":\"" + B64Url(Encoding.UTF8.GetBytes(Mime(de, nomDe, a, objet, corps, pieces))) + "\"}")
                : await Requete("POST", "https://graph.microsoft.com/v1.0/me/sendMail", jeton, CorpsGraph(a, objet, corps, pieces));
            if (code >= 200 && code < 300) { envoi.Reussi(); return; }
            if (code == 401) _jeton = null;
            envoi.Echoue(Clair(fournisseur, code, rep, erreur));
        }
        catch (Exception e) { envoi.Echoue($"Échec de l'envoi par {Nom(fournisseur)} : {e.Message}"); }
    }

    static async Task<(long code, string corps, string erreur)> Requete(string methode, string url, string jeton, string json)
    {
        using var r = new UnityWebRequest(url, methode) { downloadHandler = new DownloadHandlerBuffer() };
        if (json != null)
        {
            r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            r.SetRequestHeader("Content-Type", "application/json");
        }
        if (jeton != null) r.SetRequestHeader("Authorization", "Bearer " + jeton);
        r.timeout = 60;
        var fin = new TaskCompletionSource<bool>();
        r.SendWebRequest().completed += _ => fin.TrySetResult(true);
        await fin.Task;
        return (r.responseCode, r.downloadHandler?.text ?? "", r.error);
    }

    /// La cause en clair d'un refus de Gmail ou de Microsoft Graph, puis le détail technique.
    public static string Clair(string fournisseur, long code, string corps, string erreurReseau = null)
    {
        string nom = Nom(fournisseur), c = corps ?? "";
        string clair =
              code == 0 ? $"{nom} injoignable : vérifiez la connexion internet."
            // Le compte existe mais sans boîte chez ce fournisseur (par exemple un compte Microsoft créé sur un Gmail).
            : c.Contains("MailboxNotEnabledForRESTAPI") || c.Contains("soft-deleted")
                ? "Microsoft n'a pas de boîte e-mail pour cette adresse (elle sert peut-être seulement d'identifiant Microsoft) : "
                  + "« Se déconnecter », puis reliez-la avec Google, ou réglez-la en SMTP (Réglages → Intégrations → E-mail)."
            : c.Contains("Mail service not enabled")
                ? "Google n'a pas de boîte Gmail pour cette adresse (elle sert peut-être seulement d'identifiant Google) : "
                  + "« Se déconnecter », puis reliez-la avec Microsoft, ou réglez-la en SMTP (Réglages → Intégrations → E-mail)."
            : code == 401 ? $"L'autorisation de {nom} n'est plus valable : l'admin doit relier de nouveau l'adresse (Réglages → Intégrations → E-mail)."
            : c.Contains("dailyLimitExceeded") || c.Contains("Daily user sending limit") ? "Limite d'envois du jour atteinte chez Google : réessayez demain."
            : code == 429 || c.Contains("rateLimitExceeded") ? $"Trop d'envois d'un coup chez {nom} : réessayez dans une minute."
            : code == 403 ? $"{nom} refuse l'envoi : l'autorisation a été retirée, ou bloquée par l'administrateur de la messagerie. L'admin doit relier de nouveau l'adresse."
            : code == 400 && (c.Contains("Invalid To header") || c.Contains("ErrorInvalidRecipients")) ? "Adresse du destinataire refusée : vérifiez-la."
            : code >= 500 ? $"{nom} est indisponible pour le moment : réessayez plus tard."
            : $"{nom} a refusé l'envoi.";
        string detail = code == 0 ? erreurReseau ?? "" : c.Length > 300 ? c.Substring(0, 300) + "…" : c;
        return (clair + $"\nDétail technique : HTTP {code} {detail}").TrimEnd();
    }

    // ── Les messages (testés sans réseau) ──────────────────────────────────────

    /// Le message au format MIME, que Gmail envoie tel quel : texte et pièces en base64.
    public static string Mime(string de, string nomDe, string a, string objet, string corps,
                              IList<(string nom, byte[] octets)> pieces, string frontiere = null)
    {
        frontiere ??= "cipl_" + Guid.NewGuid().ToString("N");
        var b = new StringBuilder()
            .Append("From: ").Append(Adresse(nomDe, de)).Append("\r\n")
            .Append("To: ").Append(Ligne(a)).Append("\r\n")
            .Append("Subject: ").Append(Mot(objet)).Append("\r\n")
            .Append("MIME-Version: 1.0\r\n")
            .Append("Content-Type: multipart/mixed; boundary=\"").Append(frontiere).Append("\"\r\n\r\n");
        Partie(b, frontiere, "text/plain; charset=UTF-8", null, Encoding.UTF8.GetBytes(corps ?? ""));
        foreach (var (nom, octets) in pieces ?? new List<(string nom, byte[] octets)>())
            Partie(b, frontiere, $"{TypeMime(nom)}; name=\"{Mot(nom)}\"", $"attachment; filename=\"{Mot(nom)}\"", octets);
        return b.Append("--").Append(frontiere).Append("--\r\n").ToString();
    }

    static void Partie(StringBuilder b, string frontiere, string type, string disposition, byte[] octets)
    {
        b.Append("--").Append(frontiere).Append("\r\nContent-Type: ").Append(type).Append("\r\n");
        if (disposition != null) b.Append("Content-Disposition: ").Append(disposition).Append("\r\n");
        b.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        string s = Convert.ToBase64String(octets ?? new byte[0]);
        for (int i = 0; i < s.Length; i += 76) b.Append(s, i, Math.Min(76, s.Length - i)).Append("\r\n");
    }

    /// Un en-tête sur une seule ligne (un retour à la ligne ajouterait un en-tête).
    static string Ligne(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();

    /// Tel quel en ASCII, sinon encodé (RFC 2047) : les accents de l'objet et du nom passent partout.
    static string Mot(string s)
    {
        s = Ligne(s);
        return s.All(c => c >= 32 && c < 127) ? s : "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(s)) + "?=";
    }

    static string Adresse(string nom, string mail)
    {
        nom = Ligne(nom);
        mail = Ligne(mail);
        if (nom == "") return mail;
        return (nom.All(c => c >= 32 && c < 127) ? "\"" + nom.Replace("\"", "") + "\"" : Mot(nom)) + " <" + mail + ">";
    }

    static string TypeMime(string nom) =>
        (nom ?? "").EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream";

    /// Le corps de Microsoft Graph (sendMail). Le nom d'expédition est celui du compte Microsoft.
    /// ponytail: pièces jointes dans le corps, 3 Mo chacune au plus (une facture en fait 100 Ko).
    public static string CorpsGraph(string a, string objet, string corps, IList<(string nom, byte[] octets)> pieces)
    {
        var b = new StringBuilder("{\"message\":{\"subject\":").Append(J(Ligne(objet)))
            .Append(",\"body\":{\"contentType\":\"Text\",\"content\":").Append(J(corps))
            .Append("},\"toRecipients\":[{\"emailAddress\":{\"address\":").Append(J(Ligne(a))).Append("}}]");
        if (pieces != null && pieces.Count > 0)
        {
            b.Append(",\"attachments\":[");
            for (int i = 0; i < pieces.Count; i++)
                b.Append(i == 0 ? "" : ",")
                 .Append("{\"@odata.type\":\"#microsoft.graph.fileAttachment\",\"name\":").Append(J(pieces[i].nom))
                 .Append(",\"contentType\":").Append(J(TypeMime(pieces[i].nom)))
                 .Append(",\"contentBytes\":\"").Append(Convert.ToBase64String(pieces[i].octets ?? new byte[0])).Append("\"}");
            b.Append("]");
        }
        return b.Append("},\"saveToSentItems\":true}").ToString();
    }

    static string J(string s)
    {
        var b = new StringBuilder("\"");
        foreach (char c in s ?? "")
            switch (c)
            {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\n': b.Append("\\n"); break;
                case '\r': b.Append("\\r"); break;
                case '\t': b.Append("\\t"); break;
                default: if (c < 32) b.Append("\\u").Append(((int)c).ToString("x4")); else b.Append(c); break;
            }
        return b.Append('"').ToString();
    }
}
