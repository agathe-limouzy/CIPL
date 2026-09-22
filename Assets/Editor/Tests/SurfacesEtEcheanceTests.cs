using System;
using System.Collections.Generic;
using NUnit.Framework;

/// Deux règles corrigées le 22/09, l'une et l'autre signalées par l'usage.
///
/// 1. La surface du bâtiment est la donnée SOURCE. Elle valait la somme des lots dès
///    deux locataires : ajouter un locataire de 1500 m² à un bâtiment de 1250 m² en
///    faisait un bâtiment de 2750 m², alors que c'est le lot du premier — hérité par
///    défaut, faute d'avoir pu être saisi — qui devait se réduire d'autant.
/// 2. L'échéance d'un loyer est le jour où il est demandé (« le X » de la période).
///    Le panneau de facture proposait « date de facture + 30 jours », une date que le
///    suivi ne reconnaissait pas : la facture et sa ligne n'avaient pas la même échéance.
public class SurfacesEtEcheanceTests
{
    static Locataire Lot(float taille) => new Locataire { tailleLot = taille };

    // ── Surfaces ────────────────────────────────────────────────────────────────

    /// Le scénario exact rapporté : un bâtiment, un premier locataire qui n'a pas pu
    /// saisir sa surface, puis un second qui saisit la sienne.
    [Test]
    public void Le_lot_non_defini_se_reduit_quand_un_autre_locataire_prend_sa_part()
    {
        var premier = Lot(0f);                 // jamais saisi → suit le bâtiment
        var lots = new List<Locataire> { premier };

        // Seul, il occupe tout le bâtiment.
        Assert.That(BatimentPrefab.TailleLotDisponible(1250f, lots, premier), Is.EqualTo(1250f));

        // Un second locataire saisit 500 m² : le premier se réduit, et le bâtiment NE
        // BOUGE PAS (c'est lui la donnée source).
        lots.Add(Lot(500f));
        Assert.That(BatimentPrefab.TailleLotDisponible(1250f, lots, premier), Is.EqualTo(750f));
        Assert.That(BatimentPrefab.TailleLotDepassement(1250f, lots), Is.EqualTo(0f));
    }

    [Test]
    public void Une_surface_saisie_n_est_jamais_recalculee()
    {
        var saisi = Lot(300f);
        var lots = new List<Locataire> { saisi, Lot(0f) };
        Assert.That(BatimentPrefab.TailleLotDisponible(1250f, lots, saisi), Is.EqualTo(300f),
            "un lot saisi doit rester exactement à sa valeur");
    }

    [Test]
    public void Le_reste_se_partage_entre_les_lots_non_definis()
    {
        var a = Lot(0f); var b = Lot(0f);
        var lots = new List<Locataire> { a, b, Lot(400f) };
        // 1000 − 400 = 600, partagés entre les deux lots non définis.
        Assert.That(BatimentPrefab.TailleLotDisponible(1000f, lots, a), Is.EqualTo(300f));
        Assert.That(BatimentPrefab.TailleLotDisponible(1000f, lots, b), Is.EqualTo(300f));
    }

    /// Dépassement : la saisie est respectée (on ne corrige pas les chiffres de
    /// l'utilisatrice), le reste tombe à zéro et l'écart est signalé.
    [Test]
    public void Un_depassement_est_mesure_sans_rendre_le_reste_negatif()
    {
        var reste = Lot(0f);
        var lots = new List<Locataire> { reste, Lot(1500f) };
        Assert.That(BatimentPrefab.TailleLotDisponible(1250f, lots, reste), Is.EqualTo(0f),
            "une surface disponible ne peut pas être négative");
        Assert.That(BatimentPrefab.TailleLotDepassement(1250f, lots), Is.EqualTo(250f));
    }

    [Test]
    public void Un_batiment_sans_locataire_garde_sa_surface()
    {
        Assert.That(BatimentPrefab.TailleLotDisponible(800f, new List<Locataire>(), null), Is.EqualTo(800f));
        Assert.That(BatimentPrefab.TailleLotDisponible(800f, null, null), Is.EqualTo(800f));
        Assert.That(BatimentPrefab.TailleLotDepassement(800f, null), Is.EqualTo(0f));
    }

    // ── Échéance du loyer ───────────────────────────────────────────────────────

    static Locataire Bail(int jour, Periodicite p)
        => new Locataire { jourDemandeLoyer = jour, periodiciteLoyer = p };

    [Test]
    public void L_echeance_tombe_le_jour_ou_le_loyer_est_demande()
    {
        var loc = Bail(5, Periodicite.mensuel);
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 3), Is.EqualTo(new DateTime(2026, 3, 5)));
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 10), Is.EqualTo(new DateTime(2026, 10, 5)));
    }

    [Test]
    public void Sans_jour_renseigne_l_echeance_tombe_le_premier()
    {
        Assert.That(FacturationSuivi.EcheanceLoyer(Bail(0, Periodicite.mensuel), 2026, 4),
            Is.EqualTo(new DateTime(2026, 4, 1)));
    }

    /// « Le 31 » n'existe pas tous les mois : l'échéance doit rester dans le mois de
    /// la période, sinon un loyer de février serait dû le 3 mars.
    [Test]
    public void Le_jour_est_borne_a_la_longueur_du_mois()
    {
        var loc = Bail(31, Periodicite.mensuel);
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 2), Is.EqualTo(new DateTime(2026, 2, 28)));
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2024, 2), Is.EqualTo(new DateTime(2024, 2, 29)),
            "2024 est bissextile");
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 4), Is.EqualTo(new DateTime(2026, 4, 30)));
    }

    [Test]
    public void La_periodicite_decide_du_mois_de_l_echeance()
    {
        Assert.That(FacturationSuivi.EcheanceLoyer(Bail(15, Periodicite.trimestriel), 2026, 3),
            Is.EqualTo(new DateTime(2026, 7, 15)), "3e trimestre → juillet");
        Assert.That(FacturationSuivi.EcheanceLoyer(Bail(15, Periodicite.BiAnnuel), 2026, 2),
            Is.EqualTo(new DateTime(2026, 7, 15)), "2e semestre → juillet");
        Assert.That(FacturationSuivi.EcheanceLoyer(Bail(15, Periodicite.Annuel), 2026, 1),
            Is.EqualTo(new DateTime(2026, 1, 15)), "annuel → janvier");
    }

    /// Les mois cochés dans la révision de loyer (« mois facturés ») commandent les
    /// échéances. Ils étaient respectés par les alertes et ignorés par le suivi :
    /// un trimestriel facturé en février voyait l'alerte annoncer le 05/02 et le
    /// tableau afficher le 05/01 — deux dates pour un même loyer.
    [Test]
    public void Les_mois_coches_commandent_les_echeances()
    {
        var loc = Bail(5, Periodicite.trimestriel);
        loc.moisFacturationLoyer = new List<int> { 2, 5, 8, 11 };

        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 1), Is.EqualTo(new DateTime(2026, 2, 5)));
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 3), Is.EqualTo(new DateTime(2026, 8, 5)));

        // PeriodeIndex est l'inverse : il doit désigner la même période, sinon la clé
        // « loyer-2026-P… » des alertes ne correspond plus à la ligne du suivi.
        Assert.That(FacturationSuivi.PeriodeIndex(loc, 8), Is.EqualTo(3));
    }

    [Test]
    public void Les_mois_coches_sont_tries_et_filtres()
    {
        var loc = Bail(1, Periodicite.trimestriel);
        loc.moisFacturationLoyer = new List<int> { 11, 2, 99, 2, 5, 0 };   // désordre, doublon, hors bornes
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 1), Is.EqualTo(new DateTime(2026, 2, 1)));
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 2), Is.EqualTo(new DateTime(2026, 5, 1)));
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 3), Is.EqualTo(new DateTime(2026, 11, 1)));
    }

    [Test]
    public void Sans_mois_coches_le_calendrier_standard_s_applique()
    {
        var loc = Bail(5, Periodicite.trimestriel);
        loc.moisFacturationLoyer = new List<int>();
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 2), Is.EqualTo(new DateTime(2026, 4, 5)),
            "2e trimestre du calendrier standard → avril");
    }

    /// Un bail mensuel facture les douze mois : cocher des mois n'a pas de sens, et
    /// ne doit surtout pas amputer le calendrier.
    [Test]
    public void Un_bail_mensuel_ignore_les_mois_coches()
    {
        var loc = Bail(5, Periodicite.mensuel);
        loc.moisFacturationLoyer = new List<int> { 2, 5 };
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 9), Is.EqualTo(new DateTime(2026, 9, 5)));
        Assert.That(FacturationSuivi.Lignes(loc, 2026).FindAll(l => l.type == "Loyer"), Has.Count.EqualTo(12));
    }

    /// La règle du suivi et celle du panneau de facture doivent être la MÊME : c'est
    /// tout l'objet de l'extraction. Si les lignes du suivi cessaient de passer par
    /// EcheanceLoyer, ce test le verrait.
    [Test]
    public void Les_lignes_du_suivi_portent_l_echeance_calculee()
    {
        var loc = Bail(5, Periodicite.mensuel);
        var lignes = FacturationSuivi.Lignes(loc, 2026);
        var mars = lignes.Find(l => l.key == "loyer-2026-P3");
        Assert.That(mars, Is.Not.Null, "la ligne de mars doit exister");
        Assert.That(mars.echeanceISO, Is.EqualTo("2026-03-05"));
    }
}
