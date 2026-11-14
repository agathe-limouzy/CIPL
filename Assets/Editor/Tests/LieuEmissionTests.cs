using NUnit.Framework;
using UnityEngine;

/// Lieu d'émission (« {lieu}, le {date} ») : un réglage d'entreprise, imprimé à
/// l'identique sur la facture, la régularisation et la quittance.
public class LieuEmissionTests
{
    string _avant;

    // En mémoire seulement : rien n'est enregistré dans reglage.json.
    [SetUp] public void Garder() => _avant = ReglageService.Current.lieuEmission;
    [TearDown] public void Rendre() => ReglageService.Current.lieuEmission = _avant;

    static string[] Documents() => new[]
    {
        FacturePdfService.BuildHtml(new FacturePdfService.Data()),
        FacturePdfService.BuildRegulHtml(new FacturePdfService.RegulData()),
        FacturePdfService.BuildQuittanceHtml(new FacturePdfService.QuittanceData()),
    };

    [Test]
    public void Le_lieu_des_reglages_est_imprime_sur_les_trois_documents()
    {
        ReglageService.Current.lieuEmission = "Toulouse";

        foreach (var html in Documents())
        {
            Assert.That(html, Does.Contain("Toulouse, le"));
            Assert.That(html, Does.Not.Contain("{{LIEU}}"));
            Assert.That(html, Does.Not.Contain("St Marcel Paulel, le"));
        }
    }

    // Revue 17 (D3-01 reste, 09/11) : sans ville, la date seule — jamais la ville de CIPL sur le document d'un client.
    [Test]
    public void Un_lieu_vide_imprime_la_date_seule()
    {
        ReglageService.Current.lieuEmission = "  ";

        foreach (var html in Documents())
        {
            Assert.That(html, Does.Contain(">Le&nbsp;"));
            Assert.That(html, Does.Not.Contain("St Marcel Paulel"));
            Assert.That(html, Does.Not.Contain("{{LIEU"));
        }
    }

    [Test]
    public void Des_reglages_d_avant_gardent_St_Marcel_Paulel()
    {
        var r = JsonUtility.FromJson<ReglageData>("{\"phraseRetard\":\"x\"}");
        Assert.That(r.lieuEmission, Is.EqualTo("St Marcel Paulel"));
    }
}
