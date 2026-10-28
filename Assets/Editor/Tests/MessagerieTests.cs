using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// Étape 16a : envoi depuis l'adresse du client par Google ou Microsoft, sinon SMTP rempli.
public class MessagerieTests
{
    static readonly List<(string nom, byte[] octets)> Pdf =
        new List<(string nom, byte[] octets)> { ("Facture_2026-10.pdf", Encoding.ASCII.GetBytes("%PDF-1.4 essai")) };

    [Test]
    public void La_messagerie_se_devine_d_apres_l_adresse()
    {
        Assert.That(Messagerie.Deviner("sci.jbc@gmail.com").fournisseur, Is.EqualTo(Messagerie.Google));
        Assert.That(Messagerie.Deviner("x@hotmail.fr").fournisseur, Is.EqualTo(Messagerie.Microsoft));
        var orange = Messagerie.Deviner("contact@orange.fr");
        Assert.That(orange.fournisseur + "|" + orange.serveur, Is.EqualTo("|smtp.orange.fr"));
        Assert.That(Messagerie.Deviner("contact@sci-jbc.fr"), Is.Null, "domaine propre : lire ses serveurs de courrier");
        Assert.That(Messagerie.Domaine(" Contact@SCI-JBC.fr "), Is.EqualTo("sci-jbc.fr"));
    }

    [Test]
    public void Un_domaine_propre_se_devine_d_apres_ses_serveurs_de_courrier()
    {
        var mx = Messagerie.LireMx("{\"Status\":0,\"Answer\":[{\"name\":\"sci-jbc.fr.\",\"type\":15,\"TTL\":300,\"data\":\"1 smtp.google.com.\"},"
                                   + "{\"name\":\"sci-jbc.fr.\",\"type\":5,\"data\":\"autre.\"}]}");
        Assert.That(mx, Is.EqualTo(new[] { "smtp.google.com." }));
        Assert.That(Messagerie.DevinerParMx(mx).nom, Is.EqualTo("Google Workspace"));
        Assert.That(Messagerie.DevinerParMx(new[] { "sci-jbc-fr.mail.protection.outlook.com" }).fournisseur, Is.EqualTo(Messagerie.Microsoft));
        Assert.That(Messagerie.DevinerParMx(new[] { "mx1.mail.ovh.net." }).serveur, Is.EqualTo("ssl0.ovh.net"));
        var inconnu = Messagerie.DevinerParMx(new[] { "mail.exemple.fr" });
        Assert.That(inconnu.fournisseur + inconnu.serveur, Is.Empty, "inconnu : SMTP, serveur à saisir");
        Assert.That(inconnu.inconnu && !Messagerie.DevinerParMx(mx).inconnu && !Messagerie.Deviner("x@orange.fr").inconnu, Is.True,
                    "inconnu seulement : Google, Microsoft et SMTP au choix");
        Assert.That(Messagerie.LireMx("pas du json"), Is.Empty);
    }

    [Test]
    public void La_fenetre_de_connexion_demande_seulement_l_envoi()
    {
        // RFC 7636, annexe B.
        Assert.That(Messagerie.Defi("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"), Is.EqualTo("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"));

        string g = Messagerie.UrlAutorisation(Messagerie.Google, "id", Messagerie.Redirection(Messagerie.Google, 5000), "defi", "etat", "a@gmail.com");
        StringAssert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", g);
        StringAssert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A5000%2F", g);
        StringAssert.Contains("gmail.send", g);
        StringAssert.Contains("access_type=offline&prompt=consent", g);
        StringAssert.Contains("code_challenge_method=S256", g);
        StringAssert.DoesNotContain("gmail.readonly", g);

        string m = Messagerie.UrlAutorisation(Messagerie.Microsoft, "id", Messagerie.Redirection(Messagerie.Microsoft, 5000), "defi", "etat", null);
        StringAssert.StartsWith("https://login.microsoftonline.com/common/oauth2/v2.0/authorize?", m);
        StringAssert.Contains("redirect_uri=http%3A%2F%2Flocalhost%3A5000%2F", m);
        StringAssert.Contains("Mail.Send", m);
        StringAssert.DoesNotContain("login_hint", m);
    }

    [Test]
    public void Le_retour_de_la_fenetre_se_lit()
    {
        var p = Messagerie.Parametres("GET /?state=abc&code=4%2F0Ab-x&scope=email HTTP/1.1");
        Assert.That(p["code"] + "|" + p["state"], Is.EqualTo("4/0Ab-x|abc"));
        Assert.That(Messagerie.Parametres("GET /favicon.ico HTTP/1.1"), Is.Empty);
        Assert.That(Messagerie.Parametres(null), Is.Empty);
    }

    [Test]
    public void Le_message_Gmail_porte_le_pdf_et_les_accents()
    {
        string m = Messagerie.Mime("a@gmail.com", "SCI JBC", "locataire@exemple.fr", "Facture d'octobre — loyer",
                                   "Bonjour,\nVoici la facture.", Pdf, "FRONTIERE");
        StringAssert.Contains("From: \"SCI JBC\" <a@gmail.com>\r\n", m);
        StringAssert.Contains("Subject: =?UTF-8?B?", m);
        StringAssert.Contains("boundary=\"FRONTIERE\"", m);
        StringAssert.Contains("Content-Type: application/pdf; name=\"Facture_2026-10.pdf\"", m);
        StringAssert.Contains(System.Convert.ToBase64String(Pdf[0].octets), m);
        StringAssert.EndsWith("--FRONTIERE--\r\n", m);

        // Un retour à la ligne dans l'objet n'ajoute pas d'en-tête (copie cachée…).
        string piege = Messagerie.Mime("a@gmail.com", "", "x@y.fr", "Loyer\r\nBcc: espion@exemple.fr", "", null, "F");
        StringAssert.DoesNotContain("\r\nBcc:", piege);
    }

    [Test]
    public void Le_corps_Microsoft_est_un_json_valide()
    {
        string c = Messagerie.CorpsGraph("locataire@exemple.fr", "Facture \"octobre\"", "Bonjour,\nMerci.", Pdf);
        StringAssert.Contains("\"@odata.type\":\"#microsoft.graph.fileAttachment\"", c);
        StringAssert.Contains("\"subject\":\"Facture \\\"octobre\\\"\"", c);
        StringAssert.Contains("\"content\":\"Bonjour,\\nMerci.\"", c);
        StringAssert.Contains("\"saveToSentItems\":true", c);
        StringAssert.DoesNotContain("attachments", Messagerie.CorpsGraph("x@y.fr", "o", "c", new List<(string nom, byte[] octets)>()));
    }

    [Test]
    public void Les_refus_de_Google_et_Microsoft_sont_traduits()
    {
        StringAssert.Contains("relier de nouveau", Messagerie.Clair(Messagerie.Google, 401, "{}"));
        StringAssert.StartsWith("Limite d'envois du jour", Messagerie.Clair(Messagerie.Google, 403, "{\"reason\":\"dailyLimitExceeded\"}"));
        // Le compte existe mais sans boîte chez ce fournisseur (compte Microsoft créé sur un Gmail…).
        string sansBoite = Messagerie.Clair(Messagerie.Microsoft, 404, "{\"error\":{\"code\":\"MailboxNotEnabledForRESTAPI\"}}");
        StringAssert.StartsWith("Microsoft n'a pas de boîte", sansBoite);
        StringAssert.Contains("reliez-la avec Google", sansBoite);
        StringAssert.StartsWith("Google n'a pas de boîte Gmail",
            Messagerie.Clair(Messagerie.Google, 400, "{\"error\":{\"code\":400,\"message\":\"Mail service not enabled\"}}"));
        StringAssert.Contains("administrateur de votre Microsoft 365", Messagerie.RefusFenetre(Messagerie.Microsoft, "access_denied", ""));
        StringAssert.DoesNotContain("administrateur", Messagerie.RefusFenetre(Messagerie.Google, "access_denied", ""));
        StringAssert.Contains("injoignable", Messagerie.Clair(Messagerie.Microsoft, 0, "", "Cannot resolve destination host"));
        StringAssert.Contains("\nDétail technique : HTTP 500", Messagerie.Clair(Messagerie.Google, 500, "panne"));

        Assert.That(Messagerie.Motif(new Messagerie.Reponse { ok = true }), Is.Null);
        StringAssert.Contains("relier de nouveau", Messagerie.Motif(new Messagerie.Reponse { refus = "retire", fournisseur = Messagerie.Microsoft }));
        StringAssert.Contains("Secrets", Messagerie.Motif(new Messagerie.Reponse { refus = "config" }, Messagerie.Google));
    }

    [Test]
    public void La_fonction_serveur_parle_comme_CIPL()
    {
        string ts = File.ReadAllText(Path.Combine(Application.dataPath, "..", "Serveur", "fonctions", "messagerie.ts"));
        StringAssert.Contains($"\"{Messagerie.ScopeMicrosoft}\"", ts, "mêmes droits demandés qu'à la connexion");
        foreach (var action in new[] { "etat", "relier", "jeton", "delier" })
            StringAssert.Contains($"action === \"{action}\"", ts);
        // Chaque refus de la fonction a son message dans CIPL (pas « La fonction n'a pas pu répondre »).
        foreach (var refus in new[] { "acces", "admin", "config", "echange", "non_relie", "retire", "jeton" })
        {
            StringAssert.Contains($"\"{refus}\"", ts);
            StringAssert.DoesNotStartWith("La fonction « messagerie »", Messagerie.Motif(new Messagerie.Reponse { refus = refus }, Messagerie.Google));
        }
    }

    [Test]
    public void Une_adresse_reliee_n_a_besoin_ni_de_serveur_ni_de_mot_de_passe()
    {
        var s = new SmtpConfig { host = "", port = 587, fromEmail = "sci.jbc@gmail.com", fournisseur = Messagerie.Google };
        Assert.That(EmailService.CeQuiManque(s, ""), Is.Null);
        s.fromEmail = "";
        StringAssert.Contains("adresse d'expédition", EmailService.CeQuiManque(s, ""));
        s.fournisseur = "";
        StringAssert.Contains("serveur SMTP", EmailService.CeQuiManque(s, ""), "non reliée : le SMTP comme avant");
    }
}
