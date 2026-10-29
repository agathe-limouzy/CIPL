using System.Net.Mail;
using System.Net.Sockets;
using NUnit.Framework;

/// Étape 16 : boutons « Tester » des intégrations, et les refus traduits en clair.
public class IntegrationsTests
{
    static string Smtp(System.Exception e) => EmailService.Clair(e, "stmp.gmail.com", 587, "brut");

    [Test]
    public void Un_serveur_SMTP_mal_ecrit_est_dit_introuvable()
    {
        // Le cas du 26/10 (build 1.0.3) : « stmp » au lieu de « smtp ».
        var m = Smtp(new SmtpException("Message could not be sent.", new SocketException((int)SocketError.HostNotFound)));
        StringAssert.StartsWith("Serveur « stmp.gmail.com » introuvable", m);
        StringAssert.EndsWith("\nDétail technique : brut", m);
    }

    [Test]
    public void Les_refus_SMTP_courants_disent_quoi_verifier()
    {
        StringAssert.Contains("mot de passe d'application",
            Smtp(new SmtpException((SmtpStatusCode)535, "5.7.8 Username and Password not accepted.")));
        StringAssert.Contains("mot de passe d'application",
            Smtp(new SmtpException((SmtpStatusCode)535, "5.7.3 Authentication unsuccessful")));
        StringAssert.StartsWith("Microsoft refuse l'envoi par mot de passe",   // étape 16a : Outlook.com et M365 sans SMTP
            Smtp(new SmtpException((SmtpStatusCode)535, "5.7.139 Authentication unsuccessful, SmtpClientAuthentication is disabled for the Tenant.")));
        StringAssert.Contains("ne répond pas sur le port 587",
            Smtp(new SmtpException("Message could not be sent.", new SocketException((int)SocketError.TimedOut))));
        Assert.That(Smtp(new SmtpException(SmtpStatusCode.MailboxBusy, "occupé")), Is.EqualTo("brut"), "inconnu : le message technique seul");
    }

    [Test]
    public void Le_test_SMTP_verifie_ce_qui_est_tape()
    {
        var s = new SmtpConfig { host = "smtp.gmail.com", port = 587, fromEmail = "a@b.fr" };
        Assert.That(EmailService.CeQuiManque(s, "secret"), Is.Null);
        StringAssert.Contains("mot de passe", EmailService.CeQuiManque(s, ""), "champ vidé : pas le mot de passe enregistré");
        s.host = " ";
        StringAssert.Contains("serveur SMTP", EmailService.CeQuiManque(s, "secret"));
    }

    [Test]
    public void Les_refus_de_Pennylane_et_des_cartes_sont_traduits()
    {
        StringAssert.StartsWith("Clé refusée", PennylaneClient.Clair(401));
        StringAssert.Contains("customer_invoices", PennylaneClient.Clair(403));
        StringAssert.Contains("connexion internet", PennylaneClient.Clair(0));
        Assert.That(PennylaneClient.Clair(422), Is.Null, "le corps de réponse dit déjà le champ refusé");

        // Étape 16b : les cartes de Google, par la fonction serveur « cartes ».
        StringAssert.Contains("connexion internet", TileLoader.Motif(0, null, 0, "Cannot resolve destination host"));
        StringAssert.Contains("GOOGLE_MAPS_KEY", TileLoader.Motif(200, "config", 0, null));
        StringAssert.StartsWith("Limite de cartes du jour",
            TileLoader.Motif(424, "google", 403, "You have exceeded your daily request quota for this API."));
        StringAssert.Contains("Maps Static", TileLoader.Motif(424, "google", 403, "This API project is not authorized to use this API."));
        StringAssert.Contains("pas déployée", TileLoader.Motif(404, null, 0, "Function not found"));
        StringAssert.Contains("\nDétail technique : HTTP 424 · google 403", TileLoader.Motif(424, "google", 403, "refus"));
    }
}
