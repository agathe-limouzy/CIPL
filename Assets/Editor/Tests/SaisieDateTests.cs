using System;
using NUnit.Framework;

/// Les deux helpers extraits des quatre panneaux de facture : lecture d'une date
/// saisie et préfixe calendaire du numéro de facture.
public class SaisieDateTests
{
    [TestCase("31/12/2026", 2026, 12, 31)]
    [TestCase("1/3/2026", 2026, 3, 1)]
    [TestCase("01/03/26", 2026, 3, 1)]
    [TestCase("  15/06/2026  ", 2026, 6, 15)]
    public void Les_formats_saisis_dans_les_formulaires_sont_lus(string saisie, int an, int mois, int jour)
    {
        Assert.That(SaisieDate.TryParse(saisie, out var d), Is.True);
        Assert.That(d, Is.EqualTo(new DateTime(an, mois, jour)));
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("pas une date")]
    [TestCase("2026-12-31")]   // format ISO : c'est du stockage, pas de la saisie
    [TestCase("31/13/2026")]   // mois inexistant
    public void Une_saisie_illisible_est_refusee(string saisie)
    {
        // Refuser plutôt que retomber en silence sur une date fausse : c'est ce
        // repli muet qui avait empêché des factures de passer impayées.
        Assert.That(SaisieDate.TryParse(saisie, out _), Is.False);
    }

    [Test]
    public void La_lecture_ne_depend_pas_de_la_culture_de_la_machine()
    {
        // 03/04 doit toujours être le 3 avril, jamais le 4 mars.
        Assert.That(SaisieDate.TryParse("03/04/2026", out var d), Is.True);
        Assert.That(d.Month, Is.EqualTo(4));
        Assert.That(d.Day, Is.EqualTo(3));
    }

    [Test]
    public void Le_prefixe_de_numero_suit_le_format_choisi()
    {
        var d = new DateTime(2026, 9, 5);
        Assert.That(FactureNumerotation.Prefixe("AN", d), Is.EqualTo("2026/"));
        Assert.That(FactureNumerotation.Prefixe("AMN", d), Is.EqualTo("2026/09"));
        Assert.That(FactureNumerotation.Prefixe("AJMN", d), Is.EqualTo("2026/0509"));
    }

    [Test]
    public void Un_format_inconnu_retombe_sur_annee_mois()
    {
        Assert.That(FactureNumerotation.Prefixe("???", new DateTime(2026, 9, 5)), Is.EqualTo("2026/09"));
    }
}
