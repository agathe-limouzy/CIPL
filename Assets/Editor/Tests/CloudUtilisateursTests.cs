using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

/// Étape 9b : codes d'invitation, licences, refus du serveur et formulaire « J'ai reçu une invitation ».
public class CloudUtilisateursTests
{
    [Test]
    public void Le_code_d_invitation_se_lit_sans_ambiguite_et_le_serveur_n_en_garde_que_l_empreinte()
    {
        for (int i = 0; i < 50; i++)
        {
            string code = UtilisateursCloud.NouveauCode();
            Assert.That(Regex.IsMatch(code, "^[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$"), Is.True, code + " : ni 0, O, 1 ni I");
        }
        // Saisi en minuscules, avec ou sans tiret : le même code.
        Assert.That(UtilisateursCloud.Normaliser(" k7f2-9qxd "), Is.EqualTo("K7F29QXD"));
        Assert.That(UtilisateursCloud.Empreinte("k7f2 9qxd"), Is.EqualTo(UtilisateursCloud.Empreinte("K7F2-9QXD")));
        // Même calcul que le serveur : SHA-256 en hexadécimal minuscule (vecteur connu de « ABC »).
        Assert.That(UtilisateursCloud.Empreinte("abc"), Is.EqualTo("b5d4045c3f466fa91fe2cc6abe79232a1a57cdf104f7a26e716e0a1e2789df78"));
    }

    [Test]
    public void Licences_et_refus_du_serveur_en_clair()
    {
        Assert.That(UtilisateursCloud.Licences(2, 3), Is.EqualTo("2 / 3 licences"));
        Assert.That(UtilisateursCloud.Motif(new UtilisateursCloud.Resultat { refus = "licences", utilisees = 3, max = 3 }),
                    Is.EqualTo("3 licences sur 3 utilisées. Retirez quelqu'un ou augmentez l'abonnement."));
        Assert.That(UtilisateursCloud.Motif(new UtilisateursCloud.Resultat { refus = "dernier_admin" }),
                    Is.EqualTo("Il faut au moins un administrateur."));
        StringAssert.Contains("expiré", UtilisateursCloud.Motif(new UtilisateursCloud.Resultat { refus = "expire" }));
        Assert.That(UtilisateursCloud.Motif(new UtilisateursCloud.Resultat { refus = "erreur", erreur = "Serveur injoignable" }),
                    Is.EqualTo("Serveur injoignable"));
    }

    /// Licences par entreprise (décision du 23/10) : un compteur et des cases par entreprise, et un
    /// refus qui nomme l'entreprise pleine.
    [Test]
    public void Licences_par_entreprise_compteur_cases_et_refus()
    {
        var demo = new UtilisateursCloud.Entreprise { nom = "Démo CIPL", licences_max = 3, licences_utilisees = 2 };
        var rivoli = new UtilisateursCloud.Entreprise { nom = "SCI Rivoli", licences_max = 1, licences_utilisees = 1 };
        Assert.That(UtilisateursCloud.PastilleLicences(demo), Is.EqualTo("Démo CIPL · 2 / 3 licences"));
        Assert.That(UtilisateursCloud.PastilleLicences(rivoli), Is.EqualTo("SCI Rivoli · 1 / 1 licence · pleine"));
        Assert.That(UtilisateursCloud.CaseEntreprise(demo, dejaDedans: false), Is.EqualTo("Démo CIPL · 1 licence libre"));
        Assert.That(UtilisateursCloud.CaseEntreprise(rivoli, dejaDedans: false), Is.EqualTo("SCI Rivoli · aucune licence libre"));
        Assert.That(UtilisateursCloud.CaseEntreprise(rivoli, dejaDedans: true), Is.EqualTo("SCI Rivoli · sa licence"));
        Assert.That(UtilisateursCloud.Motif(new UtilisateursCloud.Resultat { refus = "licences", nom = "SCI Rivoli", utilisees = 1, max = 1 }),
                    Is.EqualTo("SCI Rivoli : 1 licence sur 1 utilisée. Ajoutez une licence à SCI Rivoli (Réglages › Abonnement) ou retirez-y quelqu'un."));
    }

    [Test]
    public void Le_formulaire_d_invitation_dit_ce_qui_manque()
    {
        Assert.That(UtilisateursCloud.VerifierRejoindre("", "K7F2-9QXD", "motdepasse", "motdepasse"), Does.Contain("adresse e-mail"));
        Assert.That(UtilisateursCloud.VerifierRejoindre("a@b.fr", "K7F2", "motdepasse", "motdepasse"), Does.Contain("8 caractères"));
        Assert.That(UtilisateursCloud.VerifierRejoindre("a@b.fr", "K7F2-9QXD", "court", "court"), Does.Contain("au moins 8"));
        Assert.That(UtilisateursCloud.VerifierRejoindre("a@b.fr", "K7F2-9QXD", "motdepasse", "autre123"), Does.Contain("identiques"));
        Assert.That(UtilisateursCloud.VerifierRejoindre("a@b.fr", "k7f2 9qxd", "motdepasse", "motdepasse"), Is.Null);
    }

    /// Retour du 19/10 : « Retirer » envoyait aussi p_role (vide), et Supabase ne trouvait pas
    /// retirer_membre (404). Chaque appel doit envoyer exactement les paramètres de sa fonction SQL.
    [Test]
    public void Chaque_appel_envoie_exactement_les_parametres_de_sa_fonction_SQL()
    {
        string sql = File.ReadAllText(Path.Combine("Serveur", "etape9b_supabase.sql"));
        var envoyes = new List<(string fonction, string json)>();
        var avant = UtilisateursCloud.Appeler;
        UtilisateursCloud.Appeler = (f, p) =>
        {
            envoyes.Add((f, JsonUtility.ToJson(p)));
            return Task.FromResult(new UtilisateursCloud.Resultat { ok = true });
        };
        try
        {
            UtilisateursCloud.Inviter("a@b.fr", "utilisateur", new[] { "e" }, "K7F29QXD");
            UtilisateursCloud.AnnulerInvitation("i");
            UtilisateursCloud.RenvoyerInvitation("i", "K7F29QXD");
            UtilisateursCloud.ChangerRole("u", "admin");
            UtilisateursCloud.ChangerAcces("u", new[] { "e" });
            UtilisateursCloud.Retirer("u");
            UtilisateursCloud.Accepter("K7F29QXD");
        }
        finally { UtilisateursCloud.Appeler = avant; }

        Assert.That(envoyes.Count, Is.EqualTo(7));
        foreach (var (f, json) in envoyes)
        {
            var sig = Regex.Match(sql, @"function public\." + f + @"\(([^)]*)\)");
            Assert.That(sig.Success, Is.True, f + " absente du SQL");
            var attendus = sig.Groups[1].Value.Split(',').Select(p => p.Trim().Split(' ')[0]).OrderBy(x => x);
            var cles = Regex.Matches(json, "\"(p_\\w+)\":").Cast<Match>().Select(m => m.Groups[1].Value).OrderBy(x => x);
            Assert.That(cles, Is.EqualTo(attendus), f);
        }
    }

    [Test]
    public void L_email_d_invitation_donne_le_code_et_la_marche_a_suivre()
    {
        string corps = UtilisateursCloud.CorpsEmail("Agathe", "Démo CIPL", "K7F2-9QXD", new DateTime(2026, 10, 26));
        StringAssert.Contains("Agathe vous invite à rejoindre Démo CIPL", corps);
        StringAssert.Contains("K7F2-9QXD", corps);
        StringAssert.Contains("26/10/2026", corps);
        StringAssert.Contains("J'ai reçu une invitation", corps);
        Assert.That(UtilisateursCloud.ObjetEmail("Démo CIPL"), Is.EqualTo("Invitation à rejoindre Démo CIPL sur CIPL"));
    }
}
