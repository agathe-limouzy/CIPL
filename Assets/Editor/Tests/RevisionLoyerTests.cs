using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Refonte des révisions (30/09) : type de révision, paliers, franchise, dépôt initial.
/// Règles dans `Loyers` et `FacturationSuivi` ; ces tests les verrouillent.
public class RevisionLoyerTests
{
    static DateTime D(string iso) => DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    // Bail trimestriel 01/01/2026 → 31/12/2034, périodes civiles (janv., avr., juil., oct.).
    static Locataire Bail(TypeRevision type = TypeRevision.Paliers, float loyer = 12000f)
        => new Locataire
        {
            Name = "Test",
            typeRevision = type,
            loyerAnnuel = loyer,
            loyerDepart = loyer,
            periodiciteLoyer = Periodicite.trimestriel,
            dateDebutBailISO = "2026-01-01",
            dateFinBailISO = "2034-12-31",
        };

    // Deux paliers : 12 000 € jusqu'au 31/12/2027, puis 13 000 € jusqu'à la fin du bail.
    static Locataire AvecDeuxPaliers()
    {
        var loc = Bail();
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, -1, 12000f, D("2027-12-31")), Is.Null);
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, -1, 13000f, D("2034-12-31")), Is.Null);
        return loc;
    }

    // ── Données ────────────────────────────────────────────────────────────────

    [Test]
    public void Les_nouveaux_champs_survivent_a_un_aller_retour_json()
    {
        // Leçon C4 : une classe sans [Serializable] est ignorée en silence par JsonUtility.
        var loc = AvecDeuxPaliers();
        loc.debutFacturationISO = "2026-04-01";
        loc.depotInitialise = true;

        string json = JsonUtility.ToJson(loc);
        var relu = JsonUtility.FromJson<Locataire>(json);

        Assert.That(json, Does.Contain("\"paliers\""));
        Assert.That(relu.typeRevision, Is.EqualTo(TypeRevision.Paliers));
        Assert.That(relu.paliers, Has.Count.EqualTo(2));
        Assert.That(relu.paliers[1].loyer, Is.EqualTo(13000f));
        Assert.That(relu.paliers[1].debutISO, Is.EqualTo("2028-01-01"));
        Assert.That(relu.debutFacturationISO, Is.EqualTo("2026-04-01"));
        Assert.That(relu.depotInitialise, Is.True);
    }

    [Test]
    public void Une_fiche_ancienne_avec_indice_reste_initialisee_en_indice()
    {
        var loc = new Locataire { indiceImmoAuDepart = "125.50  (2020-T2)" };
        Assert.That(loc.typeRevision, Is.EqualTo(TypeRevision.Indice));
        Assert.That(loc.LoyerInitialise, Is.True);
        Assert.That(loc.RevisionIndiceSuivie, Is.True);
        Assert.That(new Locataire().LoyerInitialise, Is.False);
    }

    [Test]
    public void Sans_revision_ni_paliers_aucune_alerte_de_revision()
    {
        // Un indice resté sur la fiche et une date dépassée ne doivent plus rien déclencher.
        var aucune = Bail(TypeRevision.Aucune);
        aucune.indiceImmoAuDepart = "125.50  (2020-T2)";
        aucune.moisDeRevisionISO = "2020-01-01";
        var paliers = AvecDeuxPaliers();
        paliers.indiceImmoAuDepart = "125.50  (2020-T2)";
        paliers.moisDeRevisionISO = "2020-01-01";

        Assert.That(aucune.LoyerInitialise, Is.True);
        Assert.That(LoyerSummaryUI.EstRevisionDue(aucune), Is.False);
        Assert.That(LoyerSummaryUI.EstRevisionDue(paliers), Is.False);
    }

    [Test]
    public void Le_depot_d_une_fiche_ancienne_compte_comme_initialise()
    {
        Assert.That(new Locataire { depotDeGarantie = 3000f }.DepotInitialise, Is.True);
        Assert.That(new Locataire().DepotInitialise, Is.False);
    }

    // ── Paliers ────────────────────────────────────────────────────────────────

    [Test]
    public void Des_paliers_qui_couvrent_le_bail_initialisent_le_loyer()
    {
        var loc = AvecDeuxPaliers();
        Assert.That(Loyers.Couvrent(loc, loc.paliers, out var msg), Is.True, msg);
        Assert.That(loc.LoyerInitialise, Is.True);
    }

    [Test]
    public void On_ne_valide_pas_tant_que_les_paliers_ne_couvrent_pas_le_bail()
    {
        var loc = Bail();
        Loyers.PoserPalier(loc, loc.paliers, -1, 12000f, D("2027-12-31"));
        Assert.That(Loyers.Couvrent(loc, loc.paliers, out var msg), Is.False);
        Assert.That(msg, Does.Contain("31/12/2034"));
        Assert.That(loc.LoyerInitialise, Is.False);
    }

    [Test]
    public void Un_palier_ne_peut_pas_finir_en_milieu_de_periode()
    {
        // Trimestriel : le 01/02 n'ouvre pas de période, un palier ne peut donc pas finir le 31/01.
        var loc = Bail();
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, -1, 12000f, D("2027-01-31")), Is.Not.Null);
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, -1, 12000f, D("2027-02-15")), Is.Not.Null);
        Assert.That(loc.paliers, Is.Empty);
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, -1, 12000f, D("2027-03-31")), Is.Null);
    }

    [Test]
    public void Un_palier_ne_depasse_pas_la_fin_du_bail()
    {
        var loc = Bail();
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, -1, 12000f, D("2035-03-31")), Is.Not.Null);
    }

    [Test]
    public void La_liste_des_fins_de_palier_ne_propose_que_des_dates_valides()
    {
        // Cas vécu : bail au 01/09/2026, franchise jusqu'au 01/10/2026, trimestriel
        // (janv., avr., juil., oct.). Le 1er palier part du DÉBUT DU BAIL et contient la
        // franchise ; le 30/09/2026 n'est pas proposé (palier tout entier gratuit), la
        // 1re fin est le 31/12/2026 : « 4 mois, dont 3 mois payés ».
        var loc = Bail();
        loc.dateDebutBailISO = "2026-09-01";
        loc.dateFinBailISO = "2035-08-31";
        loc.debutFacturationISO = "2026-10-01";
        Assert.That(Loyers.DebutPaliers(loc, out var du), Is.True);
        Assert.That(du, Is.EqualTo(D("2026-09-01")));
        var fins = Loyers.FinsPossibles(loc, du);

        Assert.That(fins[0], Is.EqualTo(D("2026-12-31")));
        Assert.That(Loyers.DureeLibelle(loc, du, fins[0]), Is.EqualTo("4 mois, dont 3 mois payés"));
        Assert.That(fins, Has.Member(D("2027-09-30")));
        Assert.That(fins, Has.No.Member(D("2027-08-31")));   // pas une fin de trimestre
        Assert.That(fins[fins.Count - 1], Is.EqualTo(D("2035-08-31")));   // la fin du bail, toujours proposée
        // Chaque date proposée est acceptée telle quelle.
        foreach (var fin in fins)
            Assert.That(Loyers.PoserPalier(loc, new List<PalierLoyer>(), -1, 12000f, fin), Is.Null, fin.ToString("dd/MM/yyyy"));
    }

    [Test]
    public void Le_palier_suivant_demarre_le_lendemain_de_la_fin_du_precedent()
    {
        var loc = AvecDeuxPaliers();
        Assert.That(loc.paliers[0].debutISO, Is.EqualTo("2026-01-01"));
        Assert.That(loc.paliers[1].debutISO, Is.EqualTo("2028-01-01"));

        // Modifier la fin du premier décale le début du second.
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, 0, 12000f, D("2028-06-30")), Is.Null);
        Assert.That(loc.paliers[1].debutISO, Is.EqualTo("2028-07-01"));
    }

    [Test]
    public void Une_fin_qui_ecraserait_le_palier_suivant_est_refusee()
    {
        var loc = AvecDeuxPaliers();
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, 0, 12000f, D("2034-12-31")), Is.Not.Null);
        Assert.That(loc.paliers[0].finISO, Is.EqualTo("2027-12-31"));
    }

    [Test]
    public void Supprimer_un_palier_donne_son_debut_au_suivant()
    {
        var loc = AvecDeuxPaliers();
        loc.paliers.RemoveAt(0);
        Loyers.Normaliser(loc.paliers, D("2026-01-01"));
        Assert.That(loc.paliers[0].debutISO, Is.EqualTo("2026-01-01"));
        Assert.That(Loyers.Couvrent(loc, loc.paliers, out _), Is.True);
    }

    [Test]
    public void Avec_une_franchise_le_premier_palier_part_du_debut_du_bail_et_la_franchise_reste_gratuite()
    {
        var loc = Bail();
        loc.debutFacturationISO = "2026-07-01";
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, -1, 12000f, D("2034-12-31")), Is.Null);
        Assert.That(loc.paliers[0].debutISO, Is.EqualTo("2026-01-01"));
        Assert.That(Loyers.Couvrent(loc, loc.paliers, out _), Is.True);
        Assert.That(Loyers.MontantPeriode(loc, 2026, 2), Is.EqualTo(0f));      // en franchise
        Assert.That(Loyers.MontantPeriode(loc, 2026, 3), Is.EqualTo(3000f));   // facturé

        // Changer la franchise après coup ne déplace pas les paliers : rien à réinitialiser.
        loc.debutFacturationISO = "2026-10-01";
        Assert.That(loc.LoyerInitialise, Is.True);
    }

    [Test]
    public void Le_loyer_courant_suit_le_palier_du_jour()
    {
        var loc = AvecDeuxPaliers();
        Loyers.Actualiser(loc, D("2029-05-10"));
        Assert.That(loc.loyerAnnuel, Is.EqualTo(13000f));
        Assert.That(loc.loyerAnnuelPrecedent, Is.EqualTo(12000f));

        Loyers.Actualiser(loc, D("2026-05-10"));
        Assert.That(loc.loyerAnnuel, Is.EqualTo(12000f));
        Assert.That(loc.loyerAnnuelPrecedent, Is.EqualTo(0f));
    }

    // ── Montant d'une période ──────────────────────────────────────────────────

    [Test]
    public void Une_periode_est_facturee_au_palier_en_vigueur()
    {
        var loc = AvecDeuxPaliers();
        Assert.That(Loyers.MontantPeriode(loc, 2027, 4), Is.EqualTo(3000f));
        Assert.That(Loyers.MontantPeriode(loc, 2028, 1), Is.EqualTo(3250f));
    }

    [Test]
    public void Une_periode_en_franchise_ne_coute_rien()
    {
        var loc = Bail(TypeRevision.Aucune);
        loc.debutFacturationISO = "2026-04-01";
        Assert.That(Loyers.MontantPeriode(loc, 2026, 1), Is.EqualTo(0f));
        Assert.That(Loyers.MontantPeriode(loc, 2026, 2), Is.EqualTo(3000f));
        Assert.That(Loyers.PeriodeFacturable(loc, 2026, 1), Is.False);
    }

    [Test]
    public void Un_mois_de_franchise_sur_un_trimestre_est_proratise_au_jour()
    {
        // Choix du 01/10 : au jour, plus juste qu'au mois — 59 jours facturés sur 90.
        var loc = Bail(TypeRevision.Aucune);
        loc.debutFacturationISO = "2026-02-01";
        Assert.That(Loyers.MontantPeriode(loc, 2026, 1), Is.EqualTo(1966.67f));
    }

    [Test]
    public void Une_premiere_periode_partielle_est_proratisee_au_jour()
    {
        // Bail au 15/02/2026, 1er trimestre de 90 jours dont 45 facturés.
        var loc = Bail(TypeRevision.Aucune);
        loc.dateDebutBailISO = "2026-02-15";
        Assert.That(Loyers.MontantPeriode(loc, 2026, 1), Is.EqualTo(1500f));
        Assert.That(Loyers.MontantPeriode(loc, 2026, 2), Is.EqualTo(3000f));
    }

    [Test]
    public void Une_periode_mensuelle_pleine_vaut_un_douzieme()
    {
        var loc = Bail(TypeRevision.Aucune);
        loc.periodiciteLoyer = Periodicite.mensuel;
        Assert.That(Loyers.MontantPeriode(loc, 2026, 2), Is.EqualTo(1000f));
    }

    [Test]
    public void La_derniere_periode_de_l_annee_finit_le_31_decembre()
    {
        var loc = Bail();
        FacturationSuivi.PeriodeBornes(loc, 2026, 4, out var debut, out var fin);
        Assert.That(debut, Is.EqualTo(D("2026-10-01")));
        Assert.That(fin, Is.EqualTo(D("2026-12-31")));
    }

    [Test]
    public void Le_loyer_annuel_de_rentabilite_tient_compte_de_la_franchise()
    {
        var loc = Bail(TypeRevision.Aucune);
        loc.debutFacturationISO = "2026-04-01";
        Assert.That(LoyerHistoryService.LoyerPourAnnee(loc, 2026, null), Is.EqualTo(9000f));
        Assert.That(LoyerHistoryService.LoyerPourAnnee(loc, 2027, null), Is.EqualTo(12000f));
    }

    // ── Suivi ──────────────────────────────────────────────────────────────────

    static FacturationSuivi.Etat EtatLoyer(Locataire loc, int year, int p)
        => FacturationSuivi.EtatDe(FacturationSuivi.Lignes(loc, year).Find(x => x.key == $"loyer-{year}-P{p}"));

    [Test]
    public void Les_periodes_en_franchise_sont_grisees_Franchise()
    {
        var loc = Bail(TypeRevision.Aucune);
        loc.debutFacturationISO = "2026-07-01";
        Assert.That(EtatLoyer(loc, 2026, 1), Is.EqualTo(FacturationSuivi.Etat.Franchise));
        Assert.That(EtatLoyer(loc, 2026, 2), Is.EqualTo(FacturationSuivi.Etat.Franchise));
        Assert.That(FacturationSuivi.EstGrisee(EtatLoyer(loc, 2026, 3)), Is.False);
    }

    [Test]
    public void En_franchise_les_provisions_restent_a_facturer()
    {
        // Retour du 01/10 : la franchise porte sur le loyer, pas sur les provisions.
        var loc = Bail(TypeRevision.Aucune);
        loc.debutFacturationISO = "2026-07-01";
        loc.provisionPourCharges = true;
        loc.provisionPourChargeValue = 300f;
        Assert.That(Loyers.MontantPeriode(loc, 2026, 1), Is.EqualTo(0f), "pas de loyer");
        Assert.That(Loyers.PeriodeFacturable(loc, 2026, 1), Is.True, "mais une facture de provisions");
        Assert.That(FacturationSuivi.EstGrisee(EtatLoyer(loc, 2026, 1)), Is.False);
        Assert.That(Loyers.EnFranchise(loc, 2026, 1), Is.True, "ligne « Loyer — franchise »");
        Assert.That(Loyers.EnFranchise(loc, 2026, 3), Is.False);

        // Avant le bail, rien — provisions ou pas.
        loc.dateDebutBailISO = "2026-04-01";
        Assert.That(Loyers.PeriodeFacturable(loc, 2026, 1), Is.False);
        Assert.That(EtatLoyer(loc, 2026, 1), Is.EqualTo(FacturationSuivi.Etat.HorsBail));
    }

    [Test]
    public void Les_periodes_avant_le_bail_sont_grisees_Hors_bail()
    {
        var loc = Bail(TypeRevision.Aucune);
        loc.dateDebutBailISO = "2026-04-01";
        Assert.That(EtatLoyer(loc, 2026, 1), Is.EqualTo(FacturationSuivi.Etat.HorsBail));
        Assert.That(FacturationSuivi.EstGrisee(EtatLoyer(loc, 2026, 2)), Is.False);
    }

    [Test]
    public void Un_depot_demande_non_recu_est_une_creance_envoyee()
    {
        var loc = Bail();
        FacturationSuivi.AjouterDepotInitial(loc, 3000f, "Envoye", DateTime.Today);
        var ligne = FacturationSuivi.Lignes(loc, DateTime.Today.Year).Find(x => x.key == FacturationSuivi.CleDepotInitial);

        Assert.That(ligne, Is.Not.Null);
        Assert.That(ligne.montant, Is.EqualTo(3000f));
        Assert.That(FacturationSuivi.EtatDe(ligne), Is.EqualTo(FacturationSuivi.Etat.Envoye));
    }

    [Test]
    public void Un_depot_pas_encore_demande_est_a_faire()
    {
        var loc = Bail();
        FacturationSuivi.AjouterDepotInitial(loc, 3000f, "", DateTime.Today);
        var ligne = FacturationSuivi.Lignes(loc, DateTime.Today.Year).Find(x => x.key == FacturationSuivi.CleDepotInitial);
        Assert.That(FacturationSuivi.EtatDe(ligne), Is.EqualTo(FacturationSuivi.Etat.AFaire));
    }

    // ── Loyer selon le chiffre d'affaires (01/10) ─────────────────────────────

    static Locataire SelonCA()
    {
        var loc = Bail(TypeRevision.ChiffreAffaires);   // bail au 01/01/2026, 12 000 € la 1re année
        loc.pourcentageCA = 8f;
        loc.loyerMinBase = 10000f;
        loc.loyerMaxBase = 20000f;
        loc.indiceImmoAuDepart = "130.00  (2025-T2)";
        return loc;
    }

    [Test]
    public void Le_loyer_selon_le_CA_est_borne_par_le_min_et_le_max()
    {
        var loc = SelonCA();
        Assert.That(Loyers.LoyerSelonCA(loc, 2026, 150000f, 10200f, 20400f, out float brut), Is.EqualTo(12000f), "8 % de 150 000");
        Assert.That(brut, Is.EqualTo(12000f));
        Assert.That(Loyers.LoyerSelonCA(loc, 2026, 100000f, 10200f, 20400f, out brut), Is.EqualTo(10200f), "8 000 sous le minimum");
        Assert.That(brut, Is.EqualTo(8000f));
        Assert.That(Loyers.LoyerSelonCA(loc, 2026, 400000f, 10200f, 20400f, out _), Is.EqualTo(20400f), "32 000 au-dessus du maximum");
        Assert.That(loc.LoyerInitialise, Is.True);
        Assert.That(loc.RevisionIndiceSuivie, Is.True, "révision annuelle suivie (alertes, bouton Réviser)");

        loc.loyerMaxBase = 5000f;   // max sous le min : pas initialisable
        Assert.That(loc.LoyerInitialise, Is.False);
    }

    [Test]
    public void Le_CA_d_une_annee_incomplete_est_ramene_a_l_annee()
    {
        var loc = SelonCA();
        loc.dateDebutBailISO = "2026-07-01";
        Loyers.LoyerSelonCA(loc, 2026, 75000f, 10000f, 20000f, out float brut);   // 184 jours sur 365
        Assert.That(brut, Is.EqualTo((float)Math.Round(0.08 * 75000 / (184.0 / 365), 2)));
        Assert.That(Loyers.LoyerSelonCA(loc, 2025, 75000f, 10000f, 20000f, out _), Is.EqualTo(0f), "pas dans les lieux en 2025");
    }

    [Test]
    public void Une_revision_selon_le_CA_garde_l_ancien_loyer_avant_sa_date()
    {
        var loc = SelonCA();
        Loyers.EnregistrerAvenant(loc, D("2027-01-01"));   // ce que fait la révision
        loc.loyerAnnuel = 14000f;
        Assert.That(Loyers.MontantPeriode(loc, 2026, 4), Is.EqualTo(3000f), "1re année : le loyer de départ");
        Assert.That(Loyers.MontantPeriode(loc, 2027, 1), Is.EqualTo(3500f), "après révision : 14 000 / 4");

        Loyers.PoserCA(loc, 2026, 175000f);
        Loyers.PoserCA(loc, 2026, 180000f);   // corrigé : remplacé, pas doublé
        var relu = JsonUtility.FromJson<Locataire>(JsonUtility.ToJson(loc));
        Assert.That(relu.chiffresAffaires.Count, Is.EqualTo(1));
        Assert.That(Loyers.CA(relu, 2026), Is.EqualTo(180000f));
        Assert.That(relu.typeRevision, Is.EqualTo(TypeRevision.ChiffreAffaires));
        Assert.That(relu.loyerMaxBase, Is.EqualTo(20000f));
    }

    // ── Départ, tacite prolongation, avenant ──────────────────────────────────

    [Test]
    public void Un_depart_proratise_la_derniere_periode_et_arrete_la_facturation()
    {
        // Départ le 14/02/2027 : 45 jours facturés sur les 90 du 1er trimestre.
        var loc = Bail(TypeRevision.Aucune);
        loc.dateSortieISO = "2027-02-14";
        Assert.That(Loyers.MontantPeriode(loc, 2027, 1), Is.EqualTo(1500f));
        Assert.That(Loyers.MontantPeriode(loc, 2027, 2), Is.EqualTo(0f));
        Assert.That(EtatLoyer(loc, 2027, 2), Is.EqualTo(FacturationSuivi.Etat.HorsBail));
        Assert.That(Loyers.PeriodeFacturable(loc, 2027, 2), Is.False);
    }

    [Test]
    public void Sans_depart_le_loyer_continue_apres_la_fin_du_bail()
    {
        // Tacite prolongation : le bail finit le 31/12/2034, le locataire reste.
        var aucune = Bail(TypeRevision.Aucune);
        Assert.That(Loyers.MontantPeriode(aucune, 2035, 1), Is.EqualTo(3000f));
        Assert.That(FacturationSuivi.EstGrisee(EtatLoyer(aucune, 2035, 1)), Is.False);

        var paliers = AvecDeuxPaliers();   // au dernier palier
        Assert.That(Loyers.MontantPeriode(paliers, 2035, 1), Is.EqualTo(3250f));
    }

    [Test]
    public void Un_avenant_en_cours_de_periode_est_proratise_entre_les_deux_loyers()
    {
        // 12 000 € jusqu'au 14/02/2027, 15 000 € ensuite : 45 jours de chaque sur 90.
        var loc = Bail(TypeRevision.Aucune);
        Loyers.EnregistrerAvenant(loc, D("2027-02-15"));
        loc.loyerAnnuel = 15000f;

        Assert.That(Loyers.MontantPeriode(loc, 2026, 4), Is.EqualTo(3000f));
        Assert.That(Loyers.MontantPeriode(loc, 2027, 1), Is.EqualTo(3375f));
        Assert.That(Loyers.MontantPeriode(loc, 2027, 2), Is.EqualTo(3750f));
    }

    [Test]
    public void Apres_un_avenant_les_paliers_repartent_de_sa_date()
    {
        var loc = AvecDeuxPaliers();
        Loyers.EnregistrerAvenant(loc, D("2029-05-01"));
        Assert.That(Loyers.DebutPaliers(loc, out var debut), Is.True);
        Assert.That(debut, Is.EqualTo(D("2029-05-01")));
        Assert.That(loc.LoyerInitialise, Is.False, "les anciens paliers ne partent pas de l'avenant");

        loc.paliers = new List<PalierLoyer>();
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, -1, 14000f, D("2034-12-31")), Is.Null);
        Assert.That(loc.LoyerInitialise, Is.True);

        // L'ancien loyer reste dû avant l'avenant, au palier de l'époque.
        Assert.That(Loyers.MontantPeriode(loc, 2027, 1), Is.EqualTo(3000f));
        Assert.That(Loyers.MontantPeriode(loc, 2028, 3), Is.EqualTo(3250f));
        // Trimestre de l'avenant : avril à 13 000 €, mai-juin à 14 000 €.
        float attendu = (float)Math.Round((13000.0 * 30 + 14000.0 * 61) / 91 / 4, 2);
        Assert.That(Loyers.MontantPeriode(loc, 2029, 2), Is.EqualTo(attendu));
        Assert.That(Loyers.MontantPeriode(loc, 2029, 3), Is.EqualTo(3500f));
    }

    [Test]
    public void Le_loyer_annuel_de_rentabilite_s_arrete_au_depart()
    {
        var loc = Bail(TypeRevision.Aucune);
        loc.dateSortieISO = "2027-06-30";
        Assert.That(LoyerHistoryService.LoyerPourAnnee(loc, 2027, null), Is.EqualTo(6000f));
        Assert.That(LoyerHistoryService.LoyerPourAnnee(loc, 2028, null), Is.EqualTo(0f));
    }

    [Test]
    public void Un_locataire_parti_n_a_plus_d_alerte_de_revision()
    {
        var loc = new Locataire { indiceImmoAuDepart = "125.50  (2020-T2)", moisDeRevisionISO = "2020-01-01" };
        Assert.That(LoyerSummaryUI.EstRevisionDue(loc), Is.True, "pré-requis");
        loc.dateSortieISO = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
        Assert.That(LoyerSummaryUI.EstRevisionDue(loc), Is.False);
    }

    [Test]
    public void L_historique_d_avenant_et_le_depart_survivent_au_json()
    {
        var loc = Bail(TypeRevision.Aucune);
        Loyers.EnregistrerAvenant(loc, D("2027-02-15"));
        loc.dateSortieISO = "2030-06-30";
        var relu = JsonUtility.FromJson<Locataire>(JsonUtility.ToJson(loc));
        Assert.That(relu.debutConditionsISO, Is.EqualTo("2027-02-15"));
        Assert.That(relu.historiqueLoyers, Has.Count.EqualTo(1));
        Assert.That(relu.historiqueLoyers[0].finISO, Is.EqualTo("2027-02-14"));
        Assert.That(relu.dateSortieISO, Is.EqualTo("2030-06-30"));
    }

    // ── Parcours de création : Général → Bail → Loyer → Dépôt ─────────────────

    [Test]
    public void Le_parcours_suit_l_ordre_general_bail_loyer_depot()
    {
        var loc = new Locataire();
        Assert.That(ParcoursLocataire.Prochaine(loc), Is.EqualTo(ParcoursLocataire.Etape.General));

        loc.Name = "Dupont";
        Assert.That(ParcoursLocataire.Prochaine(loc), Is.EqualTo(ParcoursLocataire.Etape.Bail));

        loc.dateDebutBailISO = "2026-01-01";
        loc.dateFinBailISO = "2034-12-31";
        Assert.That(ParcoursLocataire.Prochaine(loc), Is.EqualTo(ParcoursLocataire.Etape.Loyer));

        loc.typeRevision = TypeRevision.Aucune;   // loyer initialisé sans révision
        Assert.That(ParcoursLocataire.Prochaine(loc), Is.EqualTo(ParcoursLocataire.Etape.Depot));

        loc.depotInitialise = true;
        Assert.That(ParcoursLocataire.Prochaine(loc), Is.EqualTo(ParcoursLocataire.Etape.Termine));
    }

    [Test]
    public void Un_bail_jamais_saisi_ne_compte_pas_comme_rempli()
    {
        // Date par défaut du contrôle de saisie, enregistrée telle quelle autrefois.
        var loc = new Locataire { Name = "Dupont", dateDebutBailISO = "0001-01-01", dateFinBailISO = "0001-01-01" };
        Assert.That(ParcoursLocataire.BailRempli(loc), Is.False);
        loc.dateDebutBailISO = "2026-01-01"; loc.dateFinBailISO = "2025-12-31";   // fin avant le début
        Assert.That(ParcoursLocataire.BailRempli(loc), Is.False);
    }

    [Test]
    public void On_ne_saute_pas_d_etape()
    {
        var loc = new Locataire { Name = "Dupont" };   // bail pas encore saisi
        Assert.That(ParcoursLocataire.Bloque(loc, ParcoursLocataire.Etape.Loyer), Does.StartWith("Étape 2"));
        Assert.That(ParcoursLocataire.Bloque(loc, ParcoursLocataire.Etape.Depot), Does.StartWith("Étape 2"));

        loc.dateDebutBailISO = "2026-01-01"; loc.dateFinBailISO = "2034-12-31";
        Assert.That(ParcoursLocataire.Bloque(loc, ParcoursLocataire.Etape.Loyer), Is.Null);
        Assert.That(ParcoursLocataire.Bloque(loc, ParcoursLocataire.Etape.Depot), Does.StartWith("Étape 3"));
    }

    [Test]
    public void Pas_de_facture_avant_la_fin_du_parcours()
    {
        var loc = Bail(TypeRevision.Aucune);   // nommé, bail et loyer faits, dépôt pas encore
        Assert.That(ParcoursLocataire.FacturationBloquee(loc), Does.Contain("Étape 4"));
        loc.depotInitialise = true;
        Assert.That(ParcoursLocataire.FacturationBloquee(loc), Is.Null);
    }

    [Test]
    public void Le_suivi_d_un_locataire_incomplet_ne_planifie_rien()
    {
        // Nommé, bail et loyer faits, dépôt pas encore : aucune ligne planifiée…
        var loc = Bail(TypeRevision.Aucune);
        Assert.That(FacturationSuivi.LignesAffichees(loc, 2026), Is.Empty);

        // … mais une facture réellement touchée reste visible.
        loc.facturesEtat.Add(new FactureEtat { key = "loyer-2026-P2", type = "Loyer", statut = "Paye", echeanceISO = "2026-04-01" });
        Assert.That(FacturationSuivi.LignesAffichees(loc, 2026).Select(l => l.key), Is.EquivalentTo(new[] { "loyer-2026-P2" }));

        // Locataire complet : tout le calendrier.
        loc.depotInitialise = true;
        Assert.That(FacturationSuivi.LignesAffichees(loc, 2026).FindAll(l => l.type == "Loyer"), Has.Count.EqualTo(4));
    }

    [Test]
    public void Pas_de_regul_des_charges_d_une_annee_d_avant_le_bail()
    {
        // Retour du 30/09 : un bail de 2026 affichait des régularisations depuis 2023.
        var loc = Bail(TypeRevision.Aucune);   // bail au 01/01/2026
        loc.depotInitialise = true;
        loc.provisionPourCharges = true;
        loc.dateRegularisationChargeISO = "2026-11-11";
        Assert.That(FacturationSuivi.Lignes(loc, 2025).Any(l => l.type == "Regul"), Is.False);
        Assert.That(FacturationSuivi.Lignes(loc, 2026).Any(l => l.type == "Regul"), Is.True);

        // Après le départ, plus de régul non plus.
        loc.dateSortieISO = "2027-06-30";
        Assert.That(FacturationSuivi.Lignes(loc, 2027).Any(l => l.type == "Regul"), Is.True);
        Assert.That(FacturationSuivi.Lignes(loc, 2028).Any(l => l.type == "Regul"), Is.False);
    }

    [Test]
    public void Pas_d_alerte_de_regul_pour_des_charges_d_avant_le_bail()
    {
        // Bail commencé au 1er janvier de cette année ; une échéance de régul déjà passée
        // porterait sur les charges de l'an dernier : le locataire n'était pas là.
        int an = DateTime.Today.Year;
        var loc = Bail(TypeRevision.Aucune);
        loc.dateDebutBailISO = $"{an}-01-01";
        loc.dateFinBailISO = $"{an + 8}-12-31";
        loc.depotInitialise = true;
        loc.provisionPourCharges = true;
        loc.dateRegularisationChargeISO = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
        Assert.That(FacturationAlertes.Pour(loc).Any(a => a.type == FacturationAlertes.AlerteType.Regul), Is.False);
    }

    [Test]
    public void La_duree_d_un_palier_se_lit_en_annees_et_mois()
    {
        Assert.That(Loyers.DureeTexte(D("2026-01-01"), D("2027-12-31")), Is.EqualTo("2 ans"));
        Assert.That(Loyers.DureeTexte(D("2026-01-01"), D("2027-06-30")), Is.EqualTo("1 an 6 mois"));
    }
}
