using System;
using NUnit.Framework;

/// Étape 8 : ce qui fait passer CIPL hors ligne, et ce que dit le bandeau (décisions du 09/10 et du 16/10).
public class CloudHorsLigneTests
{
    [Test]
    public void Seule_l_absence_de_reponse_du_serveur_fait_passer_hors_ligne()
    {
        Assert.That(HorsLigne.Panne(false, 0), Is.True, "pas de réponse, délai dépassé");
        Assert.That(HorsLigne.Panne(false, 503), Is.True, "serveur indisponible");
        Assert.That(HorsLigne.Panne(false, 504), Is.True);
        Assert.That(HorsLigne.Panne(false, 401), Is.False, "un refus prouve que la connexion est là");
        Assert.That(HorsLigne.Panne(false, 409), Is.False);
        Assert.That(HorsLigne.Panne(false, 500), Is.False, "erreur du serveur, pas une coupure");
        Assert.That(HorsLigne.Panne(true, 200), Is.False);
    }

    [Test]
    public void La_copie_locale_garde_l_etat_du_serveur_et_recompose_les_batiments()
    {
        // Étape 8b : démarrer sans connexion sur la dernière copie, versions comprises.
        var b = new Batiment { id = "b1", Name = "rivoli" };
        b.locataireDuBatiment = new System.Collections.Generic.List<Locataire>
            { new Locataire { id = "l1", Name = "Sephora" }, new Locataire { id = "l2", Name = "Volteo" } };
        var etats = new System.Collections.Generic.Dictionary<string, EcritureCloud.Etat>();
        EcritureCloud.Memoriser(etats, b, 7, new System.Collections.Generic.Dictionary<string, int> { { "l1", 3 }, { "l2", 5 } });

        var e = new EntrepriseCloud { id = "e1", nom = "Démo CIPL", role = "admin", mode_documents = "cipl" };
        var u = new CloudClient.Utilisateur { id = "u1", email = "a@b.fr" };
        var quand = new DateTime(2026, 10, 16, 14, 41, 0, DateTimeKind.Local);
        string texte = CopieLocale.Ecrire(e, u, quand, etats);

        Assert.That(CopieLocale.Lire(texte, out var e2, out var u2, out var quand2, out var etats2), Is.True);
        Assert.That(e2.id, Is.EqualTo("e1")); Assert.That(e2.EstAdmin, Is.True);
        Assert.That(u2.email, Is.EqualTo("a@b.fr"));
        Assert.That(quand2, Is.EqualTo(quand));
        Assert.That(etats2["b:b1"].Version, Is.EqualTo(7));
        Assert.That(etats2["l:l2"].Version, Is.EqualTo(5));

        var bats = CopieLocale.Recomposer(etats2);
        Assert.That(bats.Count, Is.EqualTo(1));
        Assert.That(bats[0].Name, Is.EqualTo("rivoli"));
        Assert.That(bats[0].locataireDuBatiment.ConvertAll(l => l.Name), Is.EqualTo(new[] { "Sephora", "Volteo" }), "dans l'ordre");

        Assert.That(CopieLocale.Lire("pas du json", out _, out _, out _, out _), Is.False, "copie illisible : on ne l'ouvre pas");
        Assert.That(CopieLocale.Lire("{}", out _, out _, out _, out _), Is.False);
    }

    [Test]
    public void Le_bandeau_dit_de_quand_datent_les_donnees()
    {
        Assert.That(HorsLigne.Texte(new DateTime(2026, 10, 16, 14, 41, 0)),
                    Is.EqualTo("Hors ligne — lecture seule. Données à jour au 16/10 à 14 h 41"));
        Assert.That(HorsLigne.Texte(new DateTime(2026, 10, 9, 9, 5, 0)), Does.EndWith("au 09/10 à 9 h 05"));
    }
}
