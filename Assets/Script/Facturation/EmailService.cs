using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;

/// Envoi d'emails par SMTP.
///
/// C'est le premier code du projet capable de faire partir quelque chose vers
/// l'extérieur. Deux garanties portées ici, et qui ne doivent pas être contournées
/// par les appelants :
///
///   1. **Rien ne part sans destinataire explicite.** Ce service n'a aucune adresse
///      par défaut, aucun repli sur la fiche du locataire, aucune liste. On lui dit
///      à qui écrire, ou il refuse.
///   2. **Le résultat dit la vérité.** `Succes` n'est vrai que si le serveur a
///      accepté le message. Un échec revient avec son motif, jamais en silence — le
///      suivi de facturation ne doit pouvoir écrire « envoyé » que sur cette base.
///
/// Le mot de passe vient du fichier secrets (hors JSON, hors backup, hors dépôt) et
/// n'est jamais journalisé ni recopié dans un message d'erreur.
///
/// L'envoi tourne sur un thread de fond : un serveur injoignable gèlerait l'éditeur
/// jusqu'au timeout. L'appelant surveille `Termine` depuis une coroutine, puis lit
/// `Succes` / `Erreur` sur le thread principal pour toucher l'UI.
public static class EmailService
{
    /// État d'un envoi en cours. Rempli par le thread de fond, lu par l'appelant.
    public class Envoi
    {
        volatile bool _termine;
        volatile bool _succes;
        string _erreur;

        public bool Termine => _termine;
        public bool Succes => _succes;
        public string Erreur => _erreur ?? "";

        internal void Reussi() { _succes = true; _termine = true; }

        internal void Echoue(string motif)
        {
            _erreur = motif;
            _succes = false;
            _termine = true;

            // Un échec d'envoi n'était visible que dans un message éphémère : s'il
            // passait inaperçu, plus rien n'en gardait trace. La console, elle, reste.
            // Le motif ne contient jamais le mot de passe (voir `Expedier`).
            UnityEngine.Debug.LogError("[EmailService] " + motif);
        }
    }

    /// Vérifie que les réglages permettent un envoi. Renvoie null si tout est en
    /// place, sinon ce qui manque — formulé pour l'utilisatrice, pas pour un log.
    public static string CeQuiManque()
    {
        var s = ReglageService.Current?.smtp;
        if (s == null) return "Les réglages SMTP sont introuvables.";

        if (string.IsNullOrWhiteSpace(s.host))      return "Le serveur SMTP n'est pas renseigné (Réglages → Connexion & envoi).";
        if (s.port <= 0)                            return "Le port SMTP est invalide.";
        if (string.IsNullOrWhiteSpace(s.fromEmail)) return "L'adresse d'expédition n'est pas renseignée.";

        // `System.Net.Mail` ne sait faire que STARTTLS, jamais le SSL implicite du
        // port 465 : l'envoi y échouerait par expiration de délai, sans motif lisible.
        // Toutes les messageries courantes proposent 587, on le dit franchement plutôt
        // que de laisser chercher.
        if (s.port == 465)
            return "Le port 465 (SSL implicite) n'est pas géré. Utilise le port 587, "
                 + "proposé par toutes les messageries courantes (Gmail, Outlook, OVH, Free, Orange).";
        if (string.IsNullOrWhiteSpace(ReglageService.GetSmtpPassword()))
            return "Le mot de passe SMTP n'est pas enregistré (Réglages → Connexion & envoi).";

        return null;
    }

    /// Lance un envoi en tâche de fond. `destinataire` est obligatoire : aucune
    /// adresse n'est devinée. `piecesJointes` accepte des chemins de fichiers
    /// existants ; un chemin manquant fait échouer l'envoi plutôt que d'expédier un
    /// message amputé de sa facture.
    ///
    /// Renvoie toujours un `Envoi` : en cas de réglage incomplet il revient déjà
    /// terminé et en échec, ce qui évite à l'appelant un second chemin de code.
    public static Envoi Envoyer(string destinataire, string objet, string corps,
                                IEnumerable<string> piecesJointes = null)
    {
        var envoi = new Envoi();

        if (string.IsNullOrWhiteSpace(destinataire))
        {
            envoi.Echoue("Aucun destinataire : rien n'a été envoyé.");
            return envoi;
        }

        string manque = CeQuiManque();
        if (manque != null) { envoi.Echoue(manque); return envoi; }

        // Copie des réglages AVANT de quitter le thread principal : `ReglageService`
        // touche à Unity, qui n'est pas accessible depuis un thread de fond.
        var s = ReglageService.Current.smtp;
        string host = s.host, fromEmail = s.fromEmail, fromName = s.fromName;
        int port = s.port;
        bool ssl = s.useStartTls;
        string motDePasse = ReglageService.GetSmtpPassword();

        // L'identifiant peut différer de l'adresse d'expédition — c'est le cas chez
        // la plupart des fournisseurs dès qu'on envoie depuis un alias.
        string identifiant = string.IsNullOrWhiteSpace(s.username) ? fromEmail : s.username.Trim();

        var fichiers = new List<string>();
        if (piecesJointes != null)
            foreach (var p in piecesJointes)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                if (!File.Exists(p))
                {
                    envoi.Echoue($"Pièce jointe introuvable : {Path.GetFileName(p)}. Rien n'a été envoyé.");
                    return envoi;
                }
                fichiers.Add(p);
            }

        var t = new Thread(() => Expedier(envoi, host, port, ssl, fromEmail, fromName,
                                          identifiant, motDePasse, destinataire, objet, corps, fichiers))
        {
            IsBackground = true   // ne retient pas l'éditeur à la fermeture
        };
        t.Start();

        return envoi;
    }

    static void Expedier(Envoi envoi, string host, int port, bool ssl,
                         string fromEmail, string fromName,
                         string identifiant, string motDePasse,
                         string destinataire, string objet, string corps,
                         List<string> fichiers)
    {
        var pieces = new List<Attachment>();
        try
        {
            using (var message = new MailMessage())
            using (var client = new SmtpClient(host, port))
            {
                message.From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(fromName) ? fromEmail : fromName);
                message.To.Add(new MailAddress(destinataire));
                message.Subject = objet ?? "";
                message.Body = corps ?? "";
                message.IsBodyHtml = false;

                foreach (var f in fichiers)
                {
                    var a = new Attachment(f);
                    pieces.Add(a);
                    message.Attachments.Add(a);
                }

                client.EnableSsl = ssl;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(identifiant, motDePasse);
                client.Timeout = 30000;   // 30 s : au-delà, on rend la main avec un motif

                client.Send(message);
            }
            envoi.Reussi();
        }
        catch (SmtpFailedRecipientException e)
        {
            envoi.Echoue($"Le serveur a refusé le destinataire « {destinataire} » : {e.Message}");
        }
        catch (SmtpException e)
        {
            // `StatusCode` distingue un refus d'authentification d'un serveur
            // injoignable — c'est la première question qu'on se pose en cas d'échec.
            envoi.Echoue($"Envoi refusé par le serveur ({e.StatusCode}) : {e.Message}");
        }
        catch (Exception e)
        {
            // Jamais le mot de passe dans le message : il transiterait vers la console
            // et les journaux. `e.Message` de System.Net.Mail ne le contient pas.
            envoi.Echoue($"Échec de l'envoi : {e.Message}");
        }
        finally
        {
            foreach (var a in pieces) { try { a.Dispose(); } catch { /* best effort */ } }
        }
    }

    // ── Message d'accompagnement d'une facture ─────────────────────────────────
    //
    // Modèle par défaut, repris sur la facture (`FactureInfo.emailObjet` / `emailCorps`)
    // et modifiable panneau par panneau. Les variables sont celles du menu « / » :
    // elles sont résolues par le panneau avant l'envoi, comme les textes du document.

    public const string ObjetDefaut = "{societe.nom} — facture {numero}";

    public const string CorpsDefaut =
        "Bonjour,\n\n"
        + "Veuillez trouver ci-joint la facture {numero} concernant {periode}.\n\n"
        + "Cordialement,\n"
        + "{societe.nom}";

    /// Objet et corps du message de test. Volontairement explicites : s'il arrive
    /// dans une vraie boîte, il doit se lire comme un test et pas comme une facture.
    public const string TestObjet = "CIPL — test d'envoi";

    public static string TestCorps() =>
        "Ceci est un message de test envoyé depuis CIPL pour vérifier les réglages SMTP.\n"
        + "Aucune facture n'y est jointe et aucun locataire ne l'a reçu.\n\n"
        + $"Envoyé le {DateTime.Now:dd/MM/yyyy à HH:mm}.";
}
