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
    public void Le_bandeau_dit_de_quand_datent_les_donnees()
    {
        Assert.That(HorsLigne.Texte(new DateTime(2026, 10, 16, 14, 41, 0)),
                    Is.EqualTo("Hors ligne — lecture seule. Données à jour au 16/10 à 14 h 41"));
        Assert.That(HorsLigne.Texte(new DateTime(2026, 10, 9, 9, 5, 0)), Does.EndWith("au 09/10 à 9 h 05"));
    }
}
