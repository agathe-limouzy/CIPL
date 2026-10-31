using System;
using System.Collections.Generic;
using NUnit.Framework;

/// Le catalogue des variables et le résolveur doivent rester d'accord.
///
/// Le menu « / » propose `FactureVariables.All` dans les textes de facture. Si une
/// entrée du catalogue n'est pas résolue par `FactureVarResolver`, l'utilisatrice
/// l'insère en confiance et le jeton `{loc.nom}` s'imprime **tel quel sur un document
/// envoyé au client**. C'est précisément ce que ces tests interdisent.
///
/// Deux groupes sont résolus ailleurs et sont donc exclus, pas oubliés :
///   · « Explication dépôt »      → `ExplicationDepot.Phrase`
///   · « Ligne montant mensuel »  → `FacturePdfService.BuildHtml`
public class FactureVariablesTests
{
    static readonly string[] GroupesResolusAilleurs = { "Explication dépôt", "Ligne montant mensuel" };

    static Locataire Loc() => new Locataire
    {
        Name = "SARL DEMO",
        adresseLocataire = "12 rue de Rivoli",
        siretNumber = "111 222 333 00044",
        codeCompatable = "C0042",
        emailLocataire = "demo@example.invalid",
        telephoneLocataire = "0100000000",
        lotBatiment = 1,
        tailleLot = 120f,
        dateDebutBailISO = "2020-01-01",
        dateFinBailISO = "2029-12-31",
        dateDebutPremierBailISO = "2020-01-01",
        depotDeGarantie = 12000f,
        loyerAnnuel = 48000f,
        provisionPourCharges = true,
        provisionPourChargeValue = 500f
    };

    static Batiment Bat() => new Batiment
    {
        Name = "Immeuble Rivoli",
        adressBatiment = "12 rue de Rivoli, 75004 Paris",
        tailleBatiment = 800f,
        tailleTerrain = 950f,
        cadastral = "AB-0042",
        dateAcquisitionISO = "2015-06-30"
    };

    static FactureContext Ctx() => new FactureContext
    {
        date = new DateTime(2026, 9, 21),
        periode = "3e trimestre 2026",
        numero = "2026/09001",
        loyerHT = 48894.58f,
        tva = 10978.92f,
        ttc = 65873.50f
    };

    [Test]
    public void Chaque_variable_proposee_par_le_menu_est_reellement_resolue()
    {
        var oublis = new List<string>();

        foreach (var v in FactureVariables.All)
        {
            if (Array.IndexOf(GroupesResolusAilleurs, v.groupe) >= 0) continue;

            string sortie = FactureVarResolver.Resolve(v.token, Loc(), Bat(), Ctx());
            if (sortie == v.token) oublis.Add($"{v.token} ({v.label})");
        }

        Assert.That(oublis, Is.Empty,
            "ces variables sont proposées par le menu « / » mais ressortent telles quelles, "
            + "donc elles s'imprimeraient sur la facture : " + string.Join(", ", oublis));
    }

    [Test]
    public void Une_phrase_de_facture_ressort_sans_aucun_jeton()
    {
        // Le cas d'usage réel : une phrase de règlement écrite avec le menu « / ».
        string phrase = FactureVarResolver.Resolve(
            "{loc.nom} — {periode} — facture {numero} — {societe.nom}", Loc(), Bat(), Ctx());

        Assert.That(phrase, Does.Contain("SARL DEMO"));
        Assert.That(phrase, Does.Contain("3e trimestre 2026"));
        Assert.That(phrase, Does.Contain("2026/09001"));
        Assert.That(phrase, Does.Not.Contain("{"), "aucun jeton ne doit survivre à la résolution");
    }

    [Test]
    public void La_societe_est_celle_de_l_entreprise_ouverte()
    {
        // 30/10 : une SCI par entreprise. Avant, « GROUPE CIPL » sortait sur les factures et les e-mails de toutes.
        var sci = new EntrepriseCloud { nom = "SCI (JBC) cloud", siret = "55203253400054" };

        Assert.That(FactureVarResolver.Societe("SCI (JBC)", sci),
                    Is.EqualTo(("SCI (JBC)", "552 032 534 00054", "FR 27 552 032 534")), "le nom des Réglages passe avant celui du cloud");
        Assert.That(FactureVarResolver.Societe("  ", sci).nom, Is.EqualTo("SCI (JBC) cloud"), "Réglages sans nom : celui du cloud");
        Assert.That(FactureVarResolver.Societe("SCI (JBC)", new EntrepriseCloud { nom = "x", type = "particulier" }),
                    Is.EqualTo(("SCI (JBC)", "", "")), "sans SIRET : vide, jamais celui de GROUPE CIPL");
        Assert.That(FactureVarResolver.Societe("SCI (JBC)", null), Is.EqualTo(("SCI (JBC)", "", "")), "mode local, pas de SIRET connu");
        Assert.That(FactureVarResolver.Societe(null, null),
                    Is.EqualTo((FactureVarResolver.SocieteNom, FactureVarResolver.SocieteSiret, FactureVarResolver.SocieteTva)),
                    "rien de réglé : les constantes d'avant");
        Assert.That(FactureVarResolver.Societe("GROUPE CIPL", new EntrepriseCloud { siret = "71722088300044" }),
                    Is.EqualTo((FactureVarResolver.SocieteNom, FactureVarResolver.SocieteSiret, FactureVarResolver.SocieteTva)),
                    "calculés, le SIRET et la TVA de GROUPE CIPL s'écrivent comme les constantes");
    }

    [Test]
    public void Un_texte_vide_ou_sans_variable_traverse_sans_dommage()
    {
        Assert.That(FactureVarResolver.Resolve("", Loc(), Bat(), Ctx()), Is.Empty);
        Assert.That(FactureVarResolver.Resolve(null, Loc(), Bat(), Ctx()), Is.Empty);
        Assert.That(FactureVarResolver.Resolve("Valeur en votre aimable règlement", Loc(), Bat(), Ctx()),
                    Is.EqualTo("Valeur en votre aimable règlement"));
    }

    [Test]
    public void Les_variables_a_portee_limitee_sont_annoncees_comme_telles()
    {
        // Elles ne sont PAS résolues par le résolveur général — c'est voulu. Leur
        // groupe doit donc dire où elles fonctionnent, sinon le menu ment.
        foreach (var v in FactureVariables.All)
        {
            if (Array.IndexOf(GroupesResolusAilleurs, v.groupe) < 0) continue;

            Assert.That(FactureVarResolver.Resolve(v.token, Loc(), Bat(), Ctx()), Is.EqualTo(v.token),
                        $"{v.token} est censée être résolue ailleurs qu'au résolveur général");
            Assert.That(v.groupe, Is.Not.Null.And.Not.Empty);
        }
    }
}
