using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, thème 2c, lots 14 à 16 (REVUE_17/PLAN_2C.md) : l'avenant de périodicité, changer la périodicité du loyer à une date
/// d'effet, en cours de bail. Lot 14 : l'historique daté (Locataire.historiquePeriodicites) et le plan des périodes d'une année
/// (FacturationSuivi.Plan). Lot 15 : la date d'effet demandée et refusée dans Gestion du loyer (Loyers.RefusAvenantPeriodicite),
/// et le formulaire du loyer sur le plan. Lot 16 : les provisions converties avec la périodicité (Loyers.ProvisionConvertie), celles
/// qu'une période appelle (ProvisionPeriode) et que la régularisation déduit (ProvisionsAppelees), les parts par liste (D6-16,
/// D5-N8). Règles pures d'abord, texte du code à défaut.
public class AvenantPeriodiciteTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));

    // ── Lot 14 · historique daté et plan des périodes ──

    /// 12 000 € HT par an, loyer demandé le 5, bail depuis 2024, fiche complète (les alertes parlent). `avant` : les périodicités
    /// d'avant un avenant, chacune jusqu'à sa fin incluse.
    static Locataire Bail(Periodicite p, params (string fin, Periodicite periodicite)[] avant)
    {
        var loc = new Locataire { id = "AP14", Name = "Locataire AP", typeRevision = TypeRevision.Aucune, loyerAnnuel = 12000f, loyerDepart = 12000f,
            periodiciteLoyer = p, jourDemandeLoyer = 5, dateDebutBailISO = "2024-01-01", dateFinBailISO = "2033-12-31", depotInitialise = true,
            delaisPropres = true, envoiAvantJours = 15, impayeApresJours = 15, facturesEtat = new List<FactureEtat>() };
        foreach (var (fin, periodicite) in avant)
            loc.historiquePeriodicites.Add(new PeriodiciteAnterieure { finISO = fin, periodicite = periodicite });
        return loc;
    }

    static FactureEtat Emis(string key, string libelle, string echeance, string periodicite, float ht, string statut = "Paye")
        => new FactureEtat { key = key, type = "Loyer", statut = statut, libelle = libelle, numero = "AP/" + key.Substring(key.LastIndexOf('P')),
                             pdfPath = key + ".pdf", echeanceISO = echeance, montant = ht * 1.2f, loyerHT = ht, periodicite = periodicite };

    static List<FactureEtat> LignesLoyer(Locataire loc, int annee) => FacturationSuivi.Lignes(loc, annee).Where(l => l.type == "Loyer").ToList();

    // Coutures (ménage) : Loyers.PeriodesFacturees est retirée (le code ne la lisait plus) : les périodes du plan qui reçoivent une facture.
    static int PeriodesFacturees(Locataire loc, int annee)
        => Enumerable.Range(1, FacturationSuivi.Plan(loc, annee).Count).Count(p => Loyers.PeriodeFacturable(loc, annee, p));

    static string Bornes(FacturationSuivi.PeriodeLoyer p) => $"{p.debut:yyyy-MM-dd}>{p.fin:yyyy-MM-dd}";

    /// Le scénario du plan : mensuel, puis trimestriel à compter du 1er juillet 2026. 2026 garde ses six mois (P1 à P6) et continue
    /// par les 3e et 4e trimestres (P7, P8) ; 2025 reste mensuel, 2027 est trimestriel. Avant : quatre lignes en 2026, « P3 »
    /// désignait à la fois mars et le 3e trimestre, et juillet à décembre n'avaient aucune ligne.
    [Test]
    public void Mensuel_puis_trimestriel_au_1er_juillet()
    {
        var loc = Bail(Periodicite.trimestriel, ("2026-06-30", Periodicite.mensuel));
        var plan = FacturationSuivi.Plan(loc, 2026);
        Assert.That(plan, Has.Count.EqualTo(8), "janvier à juin, puis les 3e et 4e trimestres");
        Assert.That(Bornes(plan[2]), Is.EqualTo("2026-03-01>2026-03-31"), "P3 reste mars");
        Assert.That(plan[2].periodicite, Is.EqualTo(Periodicite.mensuel));
        Assert.That(Bornes(plan[6]), Is.EqualTo("2026-07-01>2026-09-30"), "P7 = 3e trimestre");
        Assert.That((plan[6].periodicite, plan[6].numero, plan[6].Coupee), Is.EqualTo((Periodicite.trimestriel, 3, false)));
        Assert.That(Bornes(plan[7]), Is.EqualTo("2026-10-01>2026-12-31"));
        Assert.That(FacturationSuivi.Plan(loc, 2025), Has.Count.EqualTo(12), "l'année d'avant reste mensuelle");
        Assert.That(FacturationSuivi.Plan(loc, 2027), Has.Count.EqualTo(4), "l'année d'après est trimestrielle");

        loc.facturesEtat.Add(Emis("loyer-2026-P3", "Loyer Mars 2026", "2026-03-05", "mensuel", 1000f));
        var lignes = LignesLoyer(loc, 2026);
        Assert.That(lignes.Select(l => l.key), Is.EqualTo(Enumerable.Range(1, 8).Select(n => $"loyer-2026-P{n}")), "les clés ne changent pas de format");
        Assert.That(lignes[2].libelle, Is.EqualTo("Loyer Mars 2026"));
        Assert.That(lignes[2].statut, Is.EqualTo("Paye"), "« loyer-2026-P3 » reste mars, émis");
        Assert.That(lignes[5].libelle, Is.EqualTo("Loyer Juin 2026"));
        Assert.That((lignes[6].libelle, lignes[6].echeanceISO), Is.EqualTo(("Loyer 3e trimestre 2026", "2026-07-05")), "« loyer-2026-P7 » = 3e trimestre");
        Assert.That((lignes[7].libelle, lignes[7].echeanceISO), Is.EqualTo(("Loyer 4e trimestre 2026", "2026-10-05")));
        Assert.That(lignes[6].statut, Is.Empty, "à facturer");

        Assert.That(FactureEmission.Preparer(loc, "loyer-2026-P7", "AP/7", "trimestriel").Refus, Is.Null, "le 3e trimestre se facture : P7 est libre");
        Assert.That(Loyers.MontantPeriode(loc, 2026, 7), Is.EqualTo(3000f).Within(0.01f));
        Assert.That(Loyers.MontantPeriode(loc, 2026, 3), Is.EqualTo(1000f).Within(0.01f), "mars reste un mois");
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 7), Is.EqualTo(new DateTime(2026, 7, 5)));
        Assert.That(FacturationSuivi.PeriodiciteDe(loc, 2026, 3), Is.EqualTo(Periodicite.mensuel));
        Assert.That(FacturationSuivi.PeriodiciteDe(loc, 2026, 7), Is.EqualTo(Periodicite.trimestriel));
        Assert.That(FacturationSuivi.PeriodiciteDe(loc, 2025, 11), Is.EqualTo(Periodicite.mensuel));
        Assert.That(FacturationSuivi.LibellePeriode(loc, 2026, 7), Is.EqualTo("3e trimestre 2026"));
        Assert.That(FacturationSuivi.LibellePeriode(loc, 2026, 3), Is.EqualTo("Mars 2026"));
        Assert.That(FacturationSuivi.PeriodeIndex(loc, 2026, 10), Is.EqualTo(8), "octobre 2026 : la 8e période de l'année");
        Assert.That(FacturationSuivi.PeriodeIndex(loc, 2026, 3), Is.EqualTo(3));
        Assert.That(FacturationSuivi.PeriodeIndex(loc, 2027, 10), Is.EqualTo(4));
        Assert.That(FacturationSuivi.PeriodeIndex(loc, 10), Is.EqualTo(4), "la surcharge sans année reste dans la périodicité actuelle");
        Assert.That(PeriodesFacturees(loc, 2026), Is.EqualTo(8));
        Assert.That(Loyers.EstDebutPeriode(loc, new DateTime(2026, 5, 1)), Is.True, "mai 2026 est encore un début de période (mensuel)");
        Assert.That(Loyers.EstDebutPeriode(loc, new DateTime(2026, 8, 1)), Is.False, "août 2026 est dans le 3e trimestre");
        Assert.That(LoyerHistoryService.LoyerPourAnnee(loc, 2026, null), Is.EqualTo(12000f).Within(0.01f), "6 × 1 000 + 2 × 3 000");
    }

    /// Sens inverse : les années d'avant restent trimestrielles. Avant, chaque année affichée était replanifiée en mensuel :
    /// « Mai … Décembre · À faire » alors que les trimestres étaient facturés.
    [Test]
    public void Trimestriel_puis_mensuel_ne_propose_pas_les_mois_couverts()
    {
        var loc = Bail(Periodicite.mensuel, ("2026-12-31", Periodicite.trimestriel));
        var avant = LignesLoyer(loc, 2025);
        Assert.That(avant.Select(l => l.libelle), Is.EqualTo(new[] { "Loyer 1er trimestre 2025", "Loyer 2e trimestre 2025", "Loyer 3e trimestre 2025", "Loyer 4e trimestre 2025" }));
        Assert.That(avant.Any(l => l.libelle.Contains("Mai 2025")), Is.False, "aucun « Mai 2025 »");
        Assert.That(LignesLoyer(loc, 2027), Has.Count.EqualTo(12));
        Assert.That(LignesLoyer(loc, 2027)[0].libelle, Is.EqualTo("Loyer Janvier 2027"));

        // En cours d'année, au 1er avril 2026 : le 1er trimestre (P1), puis avril à décembre (P2 à P10) ; ni février ni mars.
        var avril = Bail(Periodicite.mensuel, ("2026-03-31", Periodicite.trimestriel));
        var libelles = LignesLoyer(avril, 2026).Select(l => l.libelle).ToList();
        Assert.That(libelles, Has.Count.EqualTo(10));
        Assert.That(libelles[0], Is.EqualTo("Loyer 1er trimestre 2026"));
        Assert.That(libelles[1], Is.EqualTo("Loyer Avril 2026"));
        Assert.That(libelles[9], Is.EqualTo("Loyer Décembre 2026"));
        Assert.That(libelles.Any(l => l.Contains("Février") || l.Contains("Mars")), Is.False, "les mois du trimestre facturé ne sont jamais proposés");
        Assert.That(Loyers.MontantPeriode(avril, 2026, 1), Is.EqualTo(3000f).Within(0.01f));
        Assert.That(Loyers.MontantPeriode(avril, 2026, 2), Is.EqualTo(1000f).Within(0.01f));
    }

    /// Décision du 05/11 : la période coupée par l'avenant est au prorata des MOIS (2 mois sur 3 d'un trimestre à 3 000 € =
    /// 2 000 €, pas 2 010,99 € au jour), pour que l'année du changement fasse exactement le loyer annuel.
    [Test]
    public void La_periode_a_cheval_est_au_prorata_des_mois()
    {
        var loc = Bail(Periodicite.trimestriel, ("2026-04-30", Periodicite.mensuel));   // trimestriel à compter du 01/05/2026
        var plan = FacturationSuivi.Plan(loc, 2026);
        Assert.That(plan, Has.Count.EqualTo(7), "janvier à avril, 2e trimestre coupé, 3e et 4e trimestres");
        var p5 = plan[4];
        Assert.That(Bornes(p5), Is.EqualTo("2026-05-01>2026-06-30"), "facturé du 01/05 au 30/06");
        Assert.That((p5.periodicite, p5.numero, p5.Coupee, p5.Mois), Is.EqualTo((Periodicite.trimestriel, 2, true, 2)));
        Assert.That(p5.debutNaturel, Is.EqualTo(new DateTime(2026, 4, 1)));
        FacturationSuivi.PeriodeBornes(loc, 2026, 5, out var debut, out var fin);
        Assert.That((debut, fin), Is.EqualTo((new DateTime(2026, 5, 1), new DateTime(2026, 6, 30))));
        Assert.That(Loyers.MontantPeriode(loc, 2026, 5), Is.EqualTo(2000f).Within(0.005f), "2 mois sur 3, pas 61 jours sur 91");
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 5), Is.EqualTo(new DateTime(2026, 5, 5)), "due au jour de demande de mai");
        Assert.That(LignesLoyer(loc, 2026)[4].libelle, Is.EqualTo("Loyer 2e trimestre 2026 (mai à juin)"));
        Assert.That(Enumerable.Range(1, 7).Sum(p => Loyers.MontantPeriode(loc, 2026, p)), Is.EqualTo(12000f).Within(0.01f), "l'année fait le loyer annuel");
        Assert.That(LoyerHistoryService.LoyerPourAnnee(loc, 2026, null), Is.EqualTo(12000f).Within(0.01f));
        Assert.That(PeriodesFacturees(loc, 2026), Is.EqualTo(7), "la période coupée reçoit sa facture (provision entière)");

        // Un départ dans la période coupée reste au jour, à l'intérieur des mois facturés : 31 jours sur 61 de 2 000 €.
        loc.dateSortieISO = "2026-05-31";
        Assert.That(Loyers.MontantPeriode(loc, 2026, 5), Is.EqualTo(2000f * 31f / 61f).Within(0.01f));

        // Sens inverse, mensuel à compter du 01/05/2026 : le 2e trimestre ne garde qu'avril, 1 000 €.
        var inverse = Bail(Periodicite.mensuel, ("2026-04-30", Periodicite.trimestriel));
        Assert.That(FacturationSuivi.Plan(inverse, 2026), Has.Count.EqualTo(10), "1er trimestre, avril, puis mai à décembre");
        Assert.That(FacturationSuivi.LibellePeriode(inverse, 2026, 2), Is.EqualTo("2e trimestre 2026 (avril)"));
        Assert.That(Loyers.MontantPeriode(inverse, 2026, 2), Is.EqualTo(1000f).Within(0.005f));
        Assert.That(LoyerHistoryService.LoyerPourAnnee(inverse, 2026, null), Is.EqualTo(12000f).Within(0.01f));
    }

    /// Mensuel puis trimestriel au 1er juillet, mars et avril payés. Le 20/09/2026, la prochaine échéance est le 05/10 (4e
    /// trimestre, P8). Avant : le numéro était pris dans la périodicité actuelle (« P4 »), avril payé éteignait l'alerte.
    [Test]
    public void L_alerte_suit_le_trimestre()
    {
        var loc = Bail(Periodicite.trimestriel, ("2026-06-30", Periodicite.mensuel));
        loc.facturesEtat.Add(Emis("loyer-2026-P3", "Loyer Mars 2026", "2026-03-05", "mensuel", 1000f));
        loc.facturesEtat.Add(Emis("loyer-2026-P4", "Loyer Avril 2026", "2026-04-05", "mensuel", 1000f));
        var alerte = FacturationAlertes.Pour(loc, new DateTime(2026, 9, 20)).Single(a => a.type == FacturationAlertes.AlerteType.Loyer);
        Assert.That(alerte.cle, Is.EqualTo("loyer-2026-P8"), "l'alerte parle du 4e trimestre");
        StringAssert.Contains($"échéance le {new DateTime(2026, 10, 5):dd/MM}", alerte.message);
        Assert.That(FacturationSuivi.DejaTraite(loc, "loyer-2026-P8"), Is.False);
    }

    /// « Facturer le loyer » (FactureLoyerPanel.PeriodeAOuvrir) parcourt le plan : le 20/09/2026, le 3e trimestre (P7) n'est pas
    /// parti. Avant : P3 et P4 lus comme les 3e et 4e trimestres, déjà payés (mars, avril) : janvier 2027 était proposé.
    [Test]
    public void Facturer_le_loyer_ouvre_la_periode_du_plan()
    {
        var loc = Bail(Periodicite.trimestriel, ("2026-06-30", Periodicite.mensuel));
        loc.facturesEtat.Add(Emis("loyer-2026-P3", "Loyer Mars 2026", "2026-03-05", "mensuel", 1000f));
        loc.facturesEtat.Add(Emis("loyer-2026-P4", "Loyer Avril 2026", "2026-04-05", "mensuel", 1000f));
        Assert.That(FactureLoyerPanel.PeriodeAOuvrir(loc, new DateTime(2026, 9, 20)), Is.EqualTo((2026, 7)));
        loc.facturesEtat.Add(Emis("loyer-2026-P7", "Loyer 3e trimestre 2026", "2026-07-05", "trimestriel", 3000f));
        Assert.That(FactureLoyerPanel.PeriodeAOuvrir(loc, new DateTime(2026, 9, 20)), Is.EqualTo((2026, 8)), "3e trimestre parti : le 4e");
    }

    /// Un départ saisi après le changement : le loyer de mars (P3, 1 000 €) est comparé à mars. Avant : au 3e trimestre
    /// (3 000 €) : « à corriger » à tort, et « Annuler le départ » refusé.
    [Test]
    public void Le_depart_compare_mars_a_mars()
    {
        var loc = Bail(Periodicite.trimestriel, ("2026-06-30", Periodicite.mensuel));
        var mars = Emis("loyer-2026-P3", "Loyer Mars 2026", "2026-03-05", "mensuel", 1000f);
        loc.facturesEtat.Add(mars);
        loc.dateSortieISO = "2026-11-30";
        Assert.That(DepartLocataire.LoyerACorriger(loc, mars), Is.False, "1 000 € facturés, 1 000 € dus pour mars");
        Assert.That(DepartLocataire.PeutAnnuler(loc), Is.Null, "rien n'a été facturé au prorata du départ");
        Assert.That(DepartLocataire.PeriodeApresDepart(loc, mars), Is.False);
        Assert.That(FacturationSuivi.FactureToujoursJuste(loc, mars), Is.True, "sa périodicité est celle de sa période dans le plan");

        // Départ fin février : mars commence après. Avant, « mensuel » n'étant plus la périodicité de la fiche, la ligne n'était pas lue.
        loc.dateSortieISO = "2026-02-28";
        Assert.That(DepartLocataire.PeriodeApresDepart(loc, mars), Is.True, "mars est après un départ au 28/02");
        Assert.That(DepartLocataire.LoyerACorriger(loc, mars), Is.True, "à retirer du suivi");
    }

    /// Le titulaire d'une ligne (cession) se lit sur les bornes du plan : mars et le 3e trimestre (P7, du 01/07) sont au cédant
    /// d'une cession du 01/08/2026, le 4e trimestre (P8) au titulaire de la fiche. Avant : mars n'était pas lu (autre
    /// périodicité que la fiche), et « P7 » tombait en décembre.
    [Test]
    public void Le_titulaire_d_une_ligne_se_lit_sur_le_plan()
    {
        var loc = Bail(Periodicite.trimestriel, ("2026-06-30", Periodicite.mensuel));
        loc.cessions.Add(new CessionBail { dateISO = "2026-08-01", ancienNom = "SARL Cédant", ancienEmail = "cedant@exemple.fr" });
        Assert.That(Cessions.TitulaireLigne(loc, Emis("loyer-2026-P3", "Loyer Mars 2026", "2026-03-05", "mensuel", 1000f))?[0], Is.EqualTo("SARL Cédant"));
        Assert.That(Cessions.TitulaireLigne(loc, Emis("loyer-2026-P7", "Loyer 3e trimestre 2026", "2026-07-05", "trimestriel", 3000f))?[0], Is.EqualTo("SARL Cédant"));
        Assert.That(Cessions.TitulaireLigne(loc, Emis("loyer-2026-P8", "Loyer 4e trimestre 2026", "2026-10-05", "trimestriel", 3000f)), Is.Null, "la fiche");
    }

    /// L'échéance attendue d'un loyer préparé suit le plan : novembre préparé en mensuel avant un changement au 1er janvier 2027
    /// n'est pas « à refaire ». Fiche changée sans date (périodicité de la ligne ≠ celle de la période) : rien à comparer.
    [Test]
    public void Un_loyer_prepare_dans_l_ancienne_periodicite_n_est_pas_a_refaire()
    {
        var nov = Emis("loyer-2026-P11", "Loyer Novembre 2026", "2026-11-05", "mensuel", 1000f, "AttenteEnvoi");
        var date = Bail(Periodicite.trimestriel, ("2026-12-31", Periodicite.mensuel));
        Assert.That(FacturationSuivi.EcheanceAttendue(date, nov), Is.Null, "changement daté : P11 reste novembre, échéance le 05/11");
        date.jourDemandeLoyer = 7;
        Assert.That(FacturationSuivi.EcheanceAttendue(date, nov), Is.EqualTo(new DateTime(2026, 11, 7)), "un vrai écart reste signalé");

        var sansDate = Bail(Periodicite.trimestriel);
        Assert.That(FacturationSuivi.EcheanceAttendue(sansDate, nov), Is.Null, "changée sans date : « P11 » n'est pas une période trimestrielle");
    }

    /// Loyers.EnregistrerAvenantPeriodicite, sur le modèle de l'avenant de loyer : la périodicité et les mois cochés en place
    /// passent dans l'historique jusqu'à la veille du mois d'effet ; un avenant antidaté recoupe ; une tranche vide est ignorée.
    [Test]
    public void L_avenant_range_la_periodicite_en_place_jusqu_a_la_veille()
    {
        var loc = Bail(Periodicite.mensuel);
        Loyers.EnregistrerAvenantPeriodicite(loc, new DateTime(2026, 7, 5));   // le jour de demande du loyer de juillet
        loc.periodiciteLoyer = Periodicite.trimestriel;                        // ce que l'écran écrit ensuite
        Assert.That(loc.historiquePeriodicites.Select(h => (h.finISO, h.periodicite)), Is.EqualTo(new[] { ("2026-06-30", Periodicite.mensuel) }));
        Assert.That(FacturationSuivi.Plan(loc, 2026), Has.Count.EqualTo(8));

        // Antidaté au 05/05/2026, semestriel : la tranche mensuelle est recoupée au 30/04, le trimestriel (jamais en vigueur) disparaît.
        Loyers.EnregistrerAvenantPeriodicite(loc, new DateTime(2026, 5, 5));
        loc.periodiciteLoyer = Periodicite.BiAnnuel;
        Assert.That(loc.historiquePeriodicites.Select(h => (h.finISO, h.periodicite)), Is.EqualTo(new[] { ("2026-04-30", Periodicite.mensuel) }));
        Assert.That(FacturationSuivi.LibellePeriode(loc, 2026, 5), Is.EqualTo("1er semestre 2026 (mai à juin)"));
        Assert.That(Loyers.MontantPeriode(loc, 2026, 5), Is.EqualTo(2000f).Within(0.005f), "2 mois sur 6 d'un semestre à 6 000 €");

        // Un avenant de plus, au 05/01/2027, annuel : une deuxième tranche.
        Loyers.EnregistrerAvenantPeriodicite(loc, new DateTime(2027, 1, 5));
        loc.periodiciteLoyer = Periodicite.Annuel;
        Assert.That(loc.historiquePeriodicites.Select(h => (h.finISO, h.periodicite)),
            Is.EqualTo(new[] { ("2026-04-30", Periodicite.mensuel), ("2026-12-31", Periodicite.BiAnnuel) }));
        Assert.That(FacturationSuivi.Plan(loc, 2026), Has.Count.EqualTo(6), "4 mois, le semestre coupé, le 2e semestre");
        Assert.That(FacturationSuivi.Plan(loc, 2027), Has.Count.EqualTo(1));

        // Les mois cochés d'avant sont gardés (une copie) ; le calendrier d'avant reste le leur.
        var coches = Bail(Periodicite.trimestriel);
        coches.moisFacturationLoyer = new List<int> { 2, 5, 8, 11 };
        Loyers.EnregistrerAvenantPeriodicite(coches, new DateTime(2027, 2, 5));
        Assert.That(coches.historiquePeriodicites[0].mois, Is.EqualTo(new[] { 2, 5, 8, 11 }));
        Assert.That(coches.historiquePeriodicites[0].mois, Is.Not.SameAs(coches.moisFacturationLoyer));
        coches.periodiciteLoyer = Periodicite.mensuel; coches.moisFacturationLoyer = new List<int>();
        Assert.That(FacturationSuivi.EcheanceLoyer(coches, 2026, 2), Is.EqualTo(new DateTime(2026, 5, 5)), "2026 : toujours février, mai, août, novembre");
        Assert.That(FacturationSuivi.Plan(coches, 2027)[0].debut, Is.EqualTo(new DateTime(2027, 2, 1)), "janvier 2027 est dans le trimestre de novembre 2026");

        // Bail pas encore commencé à la date d'effet : rien avant, tranche vide ignorée (la périodicité vaut pour tout le bail).
        var neuf = Bail(Periodicite.mensuel);
        neuf.dateDebutBailISO = "2026-09-01";
        Loyers.EnregistrerAvenantPeriodicite(neuf, new DateTime(2026, 9, 1));
        Assert.That(neuf.historiquePeriodicites, Is.Empty);

        // Finitions (FIN-1) : le même bail, par un autre chemin : avenant au 05/01/2027, puis antidaté au 05/09/2026, premier mois du
        // bail. La tranche recoupée finirait le 31/08, avant le bail : elle ne reste pas, le plan est celui de l'avenant direct.
        var antidate = Bail(Periodicite.mensuel);
        antidate.dateDebutBailISO = "2026-09-01";
        Loyers.EnregistrerAvenantPeriodicite(antidate, new DateTime(2027, 1, 5));
        antidate.periodiciteLoyer = Periodicite.trimestriel;
        Assert.That(antidate.historiquePeriodicites.Select(h => h.finISO), Is.EqualTo(new[] { "2026-12-31" }));
        Loyers.EnregistrerAvenantPeriodicite(antidate, new DateTime(2026, 9, 5));
        Assert.That(antidate.historiquePeriodicites, Is.Empty, "aucune tranche d'avant le bail");
        Assert.That(FacturationSuivi.Plan(antidate, 2026), Has.Count.EqualTo(4), "trimestriel pour tout le bail, comme l'avenant direct");
    }

    /// Toutes les fiches d'aujourd'hui (sans historique) : mêmes périodes, mêmes bornes, mêmes clés, mêmes montants, mêmes libellés.
    [Test]
    public void Sans_historique_rien_ne_change()
    {
        var loc = Bail(Periodicite.trimestriel);
        var plan = FacturationSuivi.Plan(loc, 2026);
        Assert.That(plan.Select(p => Bornes(p)), Is.EqualTo(new[] { "2026-01-01>2026-03-31", "2026-04-01>2026-06-30", "2026-07-01>2026-09-30", "2026-10-01>2026-12-31" }));
        Assert.That(plan.All(p => !p.Coupee && p.Part == 1.0 && p.periodicite == Periodicite.trimestriel), Is.True);
        Assert.That(plan.Select(p => p.numero), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        var lignes = LignesLoyer(loc, 2026);
        Assert.That(lignes.Select(l => l.key), Is.EqualTo(new[] { "loyer-2026-P1", "loyer-2026-P2", "loyer-2026-P3", "loyer-2026-P4" }));
        Assert.That(lignes.Select(l => l.libelle), Is.EqualTo(new[] { "Loyer 1er trimestre 2026", "Loyer 2e trimestre 2026", "Loyer 3e trimestre 2026", "Loyer 4e trimestre 2026" }));
        Assert.That(lignes.Select(l => l.echeanceISO), Is.EqualTo(new[] { "2026-01-05", "2026-04-05", "2026-07-05", "2026-10-05" }));
        for (int p = 1; p <= 4; p++) Assert.That(Loyers.MontantPeriode(loc, 2026, p), Is.EqualTo(3000f), "période " + p);
        Assert.That(PeriodesFacturees(loc, 2026), Is.EqualTo(4));
        Assert.That(FacturationSuivi.PeriodeIndex(loc, 2026, 8), Is.EqualTo(3));
        Assert.That(FacturationSuivi.PeriodiciteDe(loc, 2026, 3), Is.EqualTo(Periodicite.trimestriel));
        Assert.That(FacturationSuivi.LibellePeriode(loc, 2026, 3), Is.EqualTo("3e trimestre 2026"));

        // Une vieille clé hors plan (« P11 » d'un ancien mensuel) : le calcul d'avant, dans la périodicité actuelle.
        FacturationSuivi.PeriodeBornes(loc, 2026, 11, out var d11, out var f11);
        Assert.That((d11, f11), Is.EqualTo((new DateTime(2026, 12, 1), new DateTime(2026, 12, 31))));
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2026, 11), Is.EqualTo(new DateTime(2026, 12, 5)));

        // Mois cochés (février, mai, août, novembre) : la 4e période passe l'année, comme avant.
        loc.moisFacturationLoyer = new List<int> { 2, 5, 8, 11 };
        Assert.That(FacturationSuivi.Plan(loc, 2026).Select(p => Bornes(p)),
            Is.EqualTo(new[] { "2026-02-01>2026-04-30", "2026-05-01>2026-07-31", "2026-08-01>2026-10-31", "2026-11-01>2027-01-31" }));
        Assert.That(FacturationSuivi.PeriodeIndex(loc, 2026, 8), Is.EqualTo(3));
        Assert.That(Loyers.EstDebutPeriode(loc, new DateTime(2026, 5, 1)), Is.True);
        Assert.That(Loyers.EstDebutPeriode(loc, new DateTime(2026, 4, 1)), Is.False);
        Assert.That(Loyers.EstDebutPeriode(loc, new DateTime(2026, 5, 2)), Is.False);

        // Les autres périodicités : n périodes, mêmes libellés.
        var mois = LignesLoyer(Bail(Periodicite.mensuel), 2026).Select(l => l.libelle).ToList();
        Assert.That(mois, Has.Count.EqualTo(12));
        Assert.That((mois[0], mois[11]), Is.EqualTo(("Loyer Janvier 2026", "Loyer Décembre 2026")));
        Assert.That(LignesLoyer(Bail(Periodicite.BiAnnuel), 2026).Select(l => l.libelle), Is.EqualTo(new[] { "Loyer 1er semestre 2026", "Loyer 2e semestre 2026" }));
        Assert.That(LignesLoyer(Bail(Periodicite.Annuel), 2026).Select(l => l.libelle), Is.EqualTo(new[] { "Loyer année 2026" }));
        Assert.That(Loyers.MontantPeriode(Bail(Periodicite.mensuel), 2026, 2), Is.EqualTo(1000f));

        // Une fiche d'avant le modèle 10 (JSON sans le champ), ou un historique absent : le même plan.
        var relue = JsonUtility.FromJson<Locataire>("{\"periodiciteLoyer\":1,\"jourDemandeLoyer\":5}");
        Assert.That(FacturationSuivi.Plan(relue, 2026), Has.Count.EqualTo(4));
        relue.historiquePeriodicites = null;
        Assert.That(FacturationSuivi.Plan(relue, 2026), Has.Count.EqualTo(4));
        Assert.That(FacturationSuivi.EcheanceLoyer(relue, 2026, 3), Is.EqualTo(new DateTime(2026, 7, 5)));
    }

    /// Le champ nouveau part avec la fiche (JsonUtility : [Serializable] sur PeriodiciteAnterieure) et le modèle des données monte.
    [Test]
    public void L_historique_est_enregistre_avec_la_fiche_modele_10()
    {
        var loc = Bail(Periodicite.trimestriel, ("2026-06-30", Periodicite.mensuel));
        loc.historiquePeriodicites[0].mois = new List<int> { 3 };
        var relu = JsonUtility.FromJson<Locataire>(JsonUtility.ToJson(loc));
        Assert.That(relu.historiquePeriodicites, Has.Count.EqualTo(1));
        Assert.That((relu.historiquePeriodicites[0].finISO, relu.historiquePeriodicites[0].periodicite), Is.EqualTo(("2026-06-30", Periodicite.mensuel)));
        Assert.That(relu.historiquePeriodicites[0].mois, Is.EqualTo(new[] { 3 }));
        Assert.That(FacturationSuivi.Plan(relu, 2026), Has.Count.EqualTo(8));
        Assert.That(DonneesCloud.Modele, Is.GreaterThanOrEqualTo(10), "un champ enregistré de plus : modèle 10 (empreinte : Revue17Tests.D2_01)");
    }

    /// Le refus D4-04 reste le filet des fiches changées sans date ; son conseil « au 1er janvier » était faux.
    [Test]
    public void Le_refus_de_periodicite_renvoie_a_la_date_d_effet()
    {
        var loc = Bail(Periodicite.trimestriel);   // passée au trimestriel sans date
        loc.facturesEtat.Add(Emis("loyer-2026-P3", "Loyer Mars 2026", "2026-03-05", "mensuel", 1000f, "Envoye"));
        string refus = FactureEmission.Preparer(loc, "loyer-2026-P3", "AP/9", "trimestriel").Refus;
        StringAssert.Contains("ancienne périodicité", refus);
        StringAssert.Contains("saisissez la nouvelle périodicité avec sa date d'effet dans « Gestion du loyer »", refus);
        StringAssert.DoesNotContain("1er janvier", refus);
    }

    // ── Lot 15 · la date d'effet dans Gestion du loyer, et le formulaire du loyer sur le plan ──

    static string J(int annee, int mois, int jour) => new DateTime(annee, mois, jour).ToString("dd/MM/yyyy");

    static string Corps(string src, string debut)
    {
        int i = src.IndexOf(debut, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), debut);
        return src.Substring(i, src.IndexOf("\n    }", i, StringComparison.Ordinal) - i);
    }

    /// Décision du 05/11 : la date d'effet est le jour de demande du loyer d'un mois, après la dernière période facturée. Le 2e
    /// trimestre 2026 émis : le 05/05/2026 est refusé avec la fin de la période et la première date possible ; le 05/07 passe ;
    /// le 15/07 n'est pas un jour de demande.
    [Test]
    public void Avenant_refuse_dans_une_periode_deja_facturee()
    {
        var loc = Bail(Periodicite.trimestriel);
        loc.facturesEtat.Add(Emis("loyer-2026-P2", "Loyer 2e Trimestre 2026", "2026-04-05", "trimestriel", 3000f));
        Assert.That(Loyers.DateAvenantRequise(loc), Is.True);
        Assert.That(Loyers.FactureJusquAu(loc, out var ligne), Is.EqualTo(new DateTime(2026, 6, 30)));
        Assert.That(ligne.key, Is.EqualTo("loyer-2026-P2"));

        string refus = Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 5, 5), 5);
        StringAssert.Contains($"déjà facturé jusqu'au {J(2026, 6, 30)} (« Loyer 2e Trimestre 2026 »)", refus);
        StringAssert.Contains($"peut commencer le {J(2026, 7, 5)} ou après", refus);
        StringAssert.DoesNotContain("bouton « Auto »", refus, "une facture de CIPL ne se libère pas");
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 6, 5), 5), Is.Not.Null, "juin est dans le trimestre facturé");
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 7, 5), 5), Is.Null);
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2027, 2, 5), 5), Is.Null, "n'importe quel mois après");

        string jour = Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 7, 15), 5);
        StringAssert.Contains("le jour où le loyer est demandé (le 5 du mois)", jour);
        StringAssert.Contains($"saisissez le {J(2026, 7, 5)}", jour);
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 7, 1), 5), Is.Not.Null, "le 1er n'est pas le jour de demande");
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 7, 1), 1), Is.Null, "« Loyer demandé le 1er »");
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 7, 1), 0), Is.Null, "jour de demande non saisi : le 1er");
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 9, 30), 31), Is.Null, "le 31 en septembre : le 30");
        Assert.That(Loyers.PremiereDateAvenant(loc, 5), Is.EqualTo(new DateTime(2026, 7, 5)), "proposée : le jour de demande du mois qui suit");
        Assert.That(Loyers.PremiereDateAvenant(loc, 31), Is.EqualTo(new DateTime(2026, 7, 31)));
    }

    /// Rien de facturé, pas de reprise, périodicité jamais changée : pas de date, « Modifier » vaut pour tout le bail, comme avant.
    [Test]
    public void Sans_loyer_touche_pas_de_date()
    {
        var loc = Bail(Periodicite.trimestriel);
        loc.facturesEtat.Add(new FactureEtat { key = "loyer-2026-P1", type = "Loyer", libelle = "Loyer 1er trimestre 2026", statut = "" });   // jamais touchée
        loc.facturesEtat.Add(new FactureEtat { key = "regul-2025", type = "Regul", statut = "Paye", numero = "R1", pdfPath = "r.pdf" });       // pas un loyer
        Assert.That(Loyers.DateAvenantRequise(loc), Is.False);
        Assert.That(Loyers.FactureJusquAu(loc, out _), Is.Null);
        Assert.That(Loyers.PremiereDateAvenant(loc, 5), Is.Null);
        Assert.That(Loyers.DateAvenantRequise(null), Is.False);
    }

    /// Ce qui compte comme facturé : l'état STOCKÉ (« Payé » mis à la main sans facture, en attente d'envoi) et une facture de
    /// CIPL remise en automatique (numéro et PDF, statut vide). Un état mis à la main se libère : le refus dit comment.
    [Test]
    public void Un_etat_pose_a_la_main_et_une_facture_remise_comptent()
    {
        var loc = Bail(Periodicite.mensuel);
        loc.facturesEtat.Add(new FactureEtat { key = "loyer-2026-P5", type = "Loyer", libelle = "Loyer Mai 2026", statut = "Paye", echeanceISO = "2026-05-05" });
        string refus = Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 5, 5), 5);
        StringAssert.Contains($"jusqu'au {J(2026, 5, 31)} (« Loyer Mai 2026 »)", refus);
        StringAssert.Contains("rallumez son bouton « Auto » dans le suivi", refus);
        Assert.That(Loyers.PremiereDateAvenant(loc, 5), Is.EqualTo(new DateTime(2026, 6, 5)));

        var remise = Bail(Periodicite.mensuel);
        remise.facturesEtat.Add(Emis("loyer-2026-P3", "Loyer Mars 2026", "2026-03-05", "mensuel", 1000f, ""));   // numéro et PDF, statut vide
        Assert.That(Loyers.FactureJusquAu(remise, out _), Is.EqualTo(new DateTime(2026, 3, 31)));
        Assert.That(Loyers.RefusAvenantPeriodicite(remise, new DateTime(2026, 3, 5), 5), Is.Not.Null);
        Assert.That(Loyers.RefusAvenantPeriodicite(remise, new DateTime(2026, 4, 5), 5), Is.Null);

        // Un loyer préparé à l'avance (janvier 2027, en attente d'envoi) repousse la première date : il garde son mois.
        remise.facturesEtat.Add(Emis("loyer-2027-P1", "Loyer Janvier 2027", "2027-01-05", "mensuel", 1000f, "AttenteEnvoi"));
        Assert.That(Loyers.PremiereDateAvenant(remise, 5), Is.EqualTo(new DateTime(2027, 2, 5)));
        string prepare = Loyers.RefusAvenantPeriodicite(remise, new DateTime(2027, 1, 5), 5);
        StringAssert.Contains("« Loyer Janvier 2027 »", prepare);
        // Finitions (FIN-2) : il n'est pas parti : le refus dit quoi comprendre (avant : seulement « déjà facturé jusqu'au 31/01/2027 »).
        StringAssert.Contains($"peut commencer le {J(2027, 2, 5)} ou après. Ce loyer est déjà préparé : il garde sa période, la nouvelle périodicité commence après lui.", prepare);
        StringAssert.DoesNotContain("bouton « Auto »", prepare, "une facture de CIPL ne se libère pas par le bouton");
        StringAssert.DoesNotContain("déjà préparé", refus, "un état mis à la main : son propre conseil, inchangé");
        remise.facturesEtat[1].statut = "AFaire";   // préparé puis bloqué « À faire » (bouton « Auto » éteint) : pareil
        StringAssert.Contains("Ce loyer est déjà préparé", Loyers.RefusAvenantPeriodicite(remise, new DateTime(2027, 1, 5), 5));
        remise.facturesEtat[1].statut = "Envoye";   // parti : « déjà facturé » suffit
        StringAssert.DoesNotContain("déjà préparé", Loyers.RefusAvenantPeriodicite(remise, new DateTime(2027, 1, 5), 5));
    }

    /// Une reprise de facturation compte comme facturée (ses périodes l'ont été hors de CIPL) : celle de la fiche, ou celle
    /// choisie à l'écran au même « Modifier » ("" : « Aucune »).
    [Test]
    public void La_reprise_de_facturation_compte_comme_facturee()
    {
        var loc = Bail(Periodicite.trimestriel);
        loc.repriseFacturationISO = "2026-04-05";   // dernière période facturée hors de CIPL : le 2e trimestre 2026
        Assert.That(Loyers.DateAvenantRequise(loc), Is.True);
        Assert.That(Loyers.FactureJusquAu(loc, out var ligne), Is.EqualTo(new DateTime(2026, 6, 30)));
        Assert.That(ligne, Is.Null, "la reprise, pas une ligne");
        string refus = Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 6, 5), 5);
        StringAssert.Contains($"jusqu'au {J(2026, 6, 30)} (reprise de facturation)", refus);
        StringAssert.Contains(J(2026, 7, 5), refus);
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 7, 5), 5), Is.Null);
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 7, 5), 5, "2026-07-05"), Is.Not.Null, "reprise choisie à l'écran : le 3e trimestre");
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 6, 5), 5, ""), Is.Null, "« Aucune » à l'écran : plus rien de facturé");

        // Finitions (FIN-7) : la date PROPOSÉE lit la même reprise que le refus : celle choisie à l'écran. Avant : celle de la fiche
        // (le 05/07), que « Modifier » refusait dès qu'une reprise plus tardive était choisie au même écran.
        Assert.That(Loyers.PremiereDateAvenant(loc, 5), Is.EqualTo(new DateTime(2026, 7, 5)), "sans reprise à l'écran : celle de la fiche");
        Assert.That(Loyers.PremiereDateAvenant(loc, 5, null, "2026-07-05"), Is.EqualTo(new DateTime(2026, 10, 5)), "reprise choisie : le 3e trimestre");
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 10, 5), 5, "2026-07-05"), Is.Null, "la date proposée passe le refus");
        Assert.That(Loyers.PremiereDateAvenant(loc, 5, new DateTime(2026, 11, 15), "2026-07-05"), Is.EqualTo(new DateTime(2026, 12, 5)), "l'avenant de loyer compte toujours");
        Assert.That(Loyers.PremiereDateAvenant(loc, 5, null, ""), Is.Null, "« Aucune » à l'écran et rien d'autre de facturé : pas de proposition");
        string rp = Src("RevisionLoyer/RevisionPanel.cs"), proposer = Corps(rp, "void RefreshAvenantPeriodicite()");
        StringAssert.Contains("string reprise = RepriseChoisie();", proposer);
        StringAssert.Contains("al : (DateTime?)null, reprise);", proposer, "la date proposée");
        StringAssert.Contains("Loyers.FactureJusquAu(_loc, out var ligne, reprise)", proposer, "la ligne d'aide");
        StringAssert.Contains("new List<string> { \"\" }, 0, _ => RefreshAvenantPeriodicite());", rp, "le menu de reprise rafraîchit la proposition");

        // Mois cochés (février, mai, août, novembre) : la reprise du 05/11/2025 couvre jusqu'au 31/01/2026.
        loc.moisFacturationLoyer = new List<int> { 2, 5, 8, 11 };
        loc.repriseFacturationISO = "2025-11-05";
        Assert.That(Loyers.FactureJusquAu(loc, out _), Is.EqualTo(new DateTime(2026, 1, 31)));
    }

    /// Finitions (FIN-4) : une reprise enregistrée dont le mois appartient à une période commencée l'année d'avant (mois cochés à
    /// cheval sur janvier, avec un historique de périodicité) se retrouve dans le plan de l'année d'avant. Avant : « 2026-P0 »,
    /// absent du menu, qui retombait sur « Aucune » : un simple « Modifier » effaçait la reprise sans le dire.
    [Test]
    public void La_reprise_enregistree_se_retrouve_dans_le_plan_de_l_annee_d_avant()
    {
        // Mensuel jusqu'à fin 2024, puis trimestriel en février, mai, août, novembre : janvier 2026 est dans le trimestre de novembre 2025.
        var loc = Bail(Periodicite.trimestriel, ("2024-12-31", Periodicite.mensuel));
        loc.moisFacturationLoyer = new List<int> { 2, 5, 8, 11 };
        Assert.That(FacturationSuivi.PeriodeIndex(loc, 2026, 1), Is.Zero, "aucune période de 2026 ne contient janvier");
        Assert.That(RevisionPanel.PeriodeReprise(loc, new DateTime(2026, 1, 5)), Is.EqualTo((2025, 5)), "le trimestre de novembre 2025, 5e période de 2025");
        Assert.That(RevisionPanel.PeriodeReprise(loc, new DateTime(2026, 2, 5)), Is.EqualTo((2026, 1)), "dans son année : comme avant");
        Assert.That(RevisionPanel.PeriodeReprise(loc, new DateTime(2024, 3, 5)), Is.EqualTo((2024, 3)), "mars 2024, encore mensuel");
        // « Modifier » réécrit l'échéance de la même période : rien n'est perdu, la reprise couvre toujours jusqu'au 31/01/2026.
        Assert.That(FacturationSuivi.EcheanceLoyer(loc, 2025, 5), Is.EqualTo(new DateTime(2025, 11, 5)));
        loc.repriseFacturationISO = "2026-01-05";
        Assert.That(Loyers.FactureJusquAu(loc, out _), Is.EqualTo(new DateTime(2026, 1, 31)));
        loc.repriseFacturationISO = "2025-11-05";
        Assert.That(Loyers.FactureJusquAu(loc, out _), Is.EqualTo(new DateTime(2026, 1, 31)));
        // Dans aucun plan (date hors des années lisibles) : rang 0, la reprise enregistrée est gardée telle quelle.
        Assert.That(RevisionPanel.PeriodeReprise(loc, new DateTime(1, 6, 5)).periode, Is.Zero);

        // Sans historique : le rang dans la périodicité actuelle, jamais 0, comme avant.
        var sans = Bail(Periodicite.trimestriel);
        sans.moisFacturationLoyer = new List<int> { 2, 5, 8, 11 };
        Assert.That(RevisionPanel.PeriodeReprise(sans, new DateTime(2026, 1, 5)), Is.EqualTo((2026, FacturationSuivi.PeriodeIndex(sans, 1))));
        Assert.That(RevisionPanel.PeriodeReprise(sans, new DateTime(2026, 8, 5)), Is.EqualTo((2026, 3)));

        string rp = Src("RevisionLoyer/RevisionPanel.cs"), options = Corps(rp, "void RefreshRepriseOptions()");
        StringAssert.Contains("sel = perEnr > 0 ? $\"{anEnr}-P{perEnr}\" : RepriseGardee;", options, "introuvable : une option gardée, pas « Aucune »");
        StringAssert.Contains("FacturationSuivi.LibellePeriode(_loc, anEnr, perEnr)", options);
        // L'option gardée n'est pas « {année}-P{n} » : RepriseChoisie rend null, et EcrireModalites ne réécrit pas la reprise.
        StringAssert.Contains("const string RepriseGardee = \"gardee\";", rp);
        StringAssert.Contains("if (RepriseChoisie() is string reprise) _loc.repriseFacturationISO = reprise;", Corps(rp, "void EcrireModalites()"));
    }

    /// Fiche changée sans date avant cette version : mars, émis en mensuel sous « P3 », n'est pas le 3e trimestre. La ligne qui
    /// porte sa périodicité (D4-04) se lit sur elle-même : première date le 05/04, pas le 05/10.
    [Test]
    public void Une_ligne_emise_sous_une_autre_periodicite_se_lit_sur_elle_meme()
    {
        var loc = Bail(Periodicite.trimestriel);
        var mars = Emis("loyer-2026-P3", "Loyer Mars 2026", "2026-03-05", "mensuel", 1000f);
        loc.facturesEtat.Add(mars);
        Assert.That(Loyers.FactureJusquAu(loc, out _), Is.EqualTo(new DateTime(2026, 3, 31)));
        Assert.That(Loyers.PremiereDateAvenant(loc, 5), Is.EqualTo(new DateTime(2026, 4, 5)));
        mars.periodicite = "";   // émise avant le 04/11 : rien ne dit sa périodicité, elle se lit dans le plan (limite connue)
        Assert.That(Loyers.FactureJusquAu(loc, out _), Is.EqualTo(new DateTime(2026, 9, 30)));
    }

    /// La date proposée suit l'avenant de loyer saisi à côté : son mois s'il commence le 1er, sinon le mois suivant (la
    /// périodicité change par mois entiers), jamais avant la première date possible.
    [Test]
    public void La_date_proposee_suit_l_avenant_de_loyer()
    {
        var loc = Bail(Periodicite.mensuel);
        loc.facturesEtat.Add(Emis("loyer-2026-P6", "Loyer Juin 2026", "2026-06-05", "mensuel", 1000f));
        Assert.That(Loyers.PremiereDateAvenant(loc, 5), Is.EqualTo(new DateTime(2026, 7, 5)));
        Assert.That(Loyers.PremiereDateAvenant(loc, 5, new DateTime(2026, 7, 15)), Is.EqualTo(new DateTime(2026, 8, 5)), "en cours de mois : le mois suivant");
        Assert.That(Loyers.PremiereDateAvenant(loc, 5, new DateTime(2026, 8, 1)), Is.EqualTo(new DateTime(2026, 8, 5)), "le 1er : ce mois-là");
        Assert.That(Loyers.PremiereDateAvenant(loc, 5, new DateTime(2026, 3, 1)), Is.EqualTo(new DateTime(2026, 7, 5)), "pas avant la dernière période facturée");
    }

    /// Une périodicité déjà changée à une date fige les périodes d'avant : un nouveau changement demande aussi sa date, proposée
    /// au début des conditions en place ; sans loyer facturé, une date plus tôt reste permise (l'historique est recoupé).
    [Test]
    public void Une_periodicite_deja_changee_demande_aussi_la_date()
    {
        var loc = Bail(Periodicite.trimestriel, ("2026-06-30", Periodicite.mensuel));
        Assert.That(Loyers.DateAvenantRequise(loc), Is.True);
        Assert.That(Loyers.PremiereDateAvenant(loc, 5), Is.EqualTo(new DateTime(2026, 7, 5)));
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, new DateTime(2026, 3, 5), 5), Is.Null);
    }

    /// Le bloc de date se montre quand les mois EFFECTIFS changent, pas la liste enregistrée : une fiche trimestrielle sans mois
    /// enregistré s'ouvre avec janvier, avril, juillet et octobre cochés (CocherMoisStandard) : ce n'est pas un changement.
    [Test]
    public void Le_calendrier_change_se_juge_sur_les_mois_effectifs()
    {
        var aucun = new List<int>();
        Assert.That(RevisionPanel.CalendrierChange(Periodicite.trimestriel, aucun, Periodicite.trimestriel, new[] { 1, 4, 7, 10 }), Is.False);
        Assert.That(RevisionPanel.CalendrierChange(Periodicite.trimestriel, null, Periodicite.trimestriel, new[] { 10, 7, 4, 1 }), Is.False);
        Assert.That(RevisionPanel.CalendrierChange(Periodicite.trimestriel, new[] { 1, 4, 7, 10 }, Periodicite.trimestriel, new[] { 2, 5, 8, 11 }), Is.True);
        Assert.That(RevisionPanel.CalendrierChange(Periodicite.mensuel, new[] { 3 }, Periodicite.mensuel, aucun), Is.False, "en mensuel les mois cochés sont ignorés");
        Assert.That(RevisionPanel.CalendrierChange(Periodicite.mensuel, aucun, Periodicite.trimestriel, new[] { 1, 4, 7, 10 }), Is.True);
        Assert.That(RevisionPanel.MoisEffectifs(Periodicite.BiAnnuel, aucun), Is.EqualTo(new[] { 1, 7 }));
        Assert.That(RevisionPanel.MoisEffectifs(Periodicite.mensuel, new[] { 3 }), Is.Empty);
    }

    /// Le parcours entier, règles pures : janvier à juin émis en mensuel, « trimestriel à compter du 05/07/2026 ». Les loyers émis
    /// gardent leur clé et leur mois ; le formulaire du loyer propose les périodes du plan de l'année saisie, libellées dans LEUR
    /// périodicité ; mars se corrige encore (la périodicité passée à l'émission est celle de la période, pas celle de la fiche).
    [Test]
    public void Apres_l_avenant_le_formulaire_du_loyer_suit_le_plan()
    {
        var loc = Bail(Periodicite.mensuel);
        loc.facturesEtat.Add(Emis("loyer-2026-P3", "Loyer Mars 2026", "2026-03-05", "mensuel", 1000f));
        loc.facturesEtat.Add(Emis("loyer-2026-P6", "Loyer Juin 2026", "2026-06-05", "mensuel", 1000f));
        var effet = new DateTime(2026, 7, 5);
        Assert.That(Loyers.RefusAvenantPeriodicite(loc, effet, 5), Is.Null);
        Loyers.EnregistrerAvenantPeriodicite(loc, effet);                 // ce que « Modifier » fait, dans cet ordre
        loc.periodiciteLoyer = Periodicite.trimestriel;
        loc.moisFacturationLoyer = new List<int> { 1, 4, 7, 10 };

        var (labels, ids) = FactureLoyerPanel.PeriodeOptions(loc, 2026);
        Assert.That(labels, Is.EqualTo(new[] { "Janvier", "Février", "Mars", "Avril", "Mai", "Juin", "3e Trimestre", "4e Trimestre" }));
        Assert.That(ids, Is.EqualTo(new[] { "1", "2", "3", "4", "5", "6", "7", "8" }), "l'identifiant est le rang de la clé « loyer-2026-P{n} »");
        Assert.That(FactureLoyerPanel.PeriodeOptions(loc, 2025).labels, Has.Count.EqualTo(12), "l'année d'avant reste mensuelle");
        Assert.That(FactureLoyerPanel.PeriodeOptions(loc, 2027).labels, Is.EqualTo(new[] { "1er Trimestre", "2e Trimestre", "3e Trimestre", "4e Trimestre" }));

        Assert.That(FactureLoyerPanel.LibellePeriode(loc, 2026, 3), Is.EqualTo("Mars 2026"));
        Assert.That(FactureLoyerPanel.LibellePeriode(loc, 2026, 7), Is.EqualTo("3e Trimestre 2026"), "avant : « Juillet 2026 » ou le libellé de P3");
        Assert.That(FactureLoyerPanel.MoisFactures(loc, 2026, 3), Is.EqualTo(1), "mars : pas de ligne « montant mensuel »");
        Assert.That(FactureLoyerPanel.MoisFactures(loc, 2026, 7), Is.EqualTo(3));

        // Mars (P3), émis en mensuel : sa période est toujours mensuelle dans le plan, il se corrige. Avec la périodicité de la
        // fiche (avant), l'émission le refusait (D4-04) ou en faisait une rectificative du 3e trimestre.
        string periodiciteMars = FacturationSuivi.PeriodiciteDe(loc, 2026, 3).ToString();
        Assert.That(periodiciteMars, Is.EqualTo("mensuel"));
        Assert.That(FactureEmission.SeraUneCorrection(loc, "loyer-2026-P3", periodiciteMars), Is.True);
        Assert.That(FactureEmission.Preparer(loc, "loyer-2026-P3", "AP/9", loc.periodiciteLoyer.ToString()).Refus, Is.Not.Null, "ce que le formulaire passait avant");
        Assert.That(FactureEmission.Preparer(loc, "loyer-2026-P7", "AP/7", FacturationSuivi.PeriodiciteDe(loc, 2026, 7).ToString()).Refus, Is.Null);

        // L'année saisie change : la période est gardée par son mois de début.
        Assert.That(FactureLoyerPanel.PeriodeGardee(loc, 2026, 3, 2027), Is.EqualTo(1), "mars 2026 → 1er trimestre 2027");
        Assert.That(FactureLoyerPanel.PeriodeGardee(loc, 2027, 4, 2026), Is.EqualTo(8), "4e trimestre 2027 → 4e trimestre 2026 (P8)");
        Assert.That(FactureLoyerPanel.PeriodeGardee(loc, 2026, 3, 2025), Is.EqualTo(3), "mars → mars");
        Assert.That(FactureLoyerPanel.PeriodeGardee(Bail(Periodicite.trimestriel), 2026, 3, 2027), Is.EqualTo(3), "sans avenant : le même numéro");
        // Finitions (FIN-3) : sans avenant, le numéro ne change JAMAIS, même sur une vieille fiche aux mois cochés incomplets
        // (février et mai seuls : P2, P3 et P4 commencent toutes en mai ; la recherche par mois rendait P2).
        var incomplet = Bail(Periodicite.trimestriel);
        incomplet.moisFacturationLoyer = new List<int> { 2, 5 };
        Assert.That(FactureLoyerPanel.PeriodeGardee(incomplet, 2026, 3, 2027), Is.EqualTo(3));
        Assert.That(FactureLoyerPanel.PeriodeGardee(incomplet, 2026, 4, 2027), Is.EqualTo(4));
        Assert.That(FactureLoyerPanel.PeriodeGardee(incomplet, 2027, 2, 2026), Is.EqualTo(2));

        // Un nouveau changement : le 3e trimestre n'est pas facturé, la date suit toujours juin.
        Assert.That(Loyers.PremiereDateAvenant(loc, 5), Is.EqualTo(new DateTime(2026, 7, 5)));
    }

    /// Sans avenant de périodicité, le menu « Période facturée » et les libellés imprimés sont ceux d'avant ; une période coupée
    /// dit ses mois, et sa ligne « montant mensuel » divise par ses mois facturés.
    [Test]
    public void Les_options_de_periode_sont_celles_d_avant_sans_avenant()
    {
        var trimestres = FactureLoyerPanel.PeriodeOptions(Bail(Periodicite.trimestriel), 2026);
        Assert.That(trimestres.labels, Is.EqualTo(new[] { "1er Trimestre", "2e Trimestre", "3e Trimestre", "4e Trimestre" }));
        Assert.That(trimestres.ids, Is.EqualTo(new[] { "1", "2", "3", "4" }));
        Assert.That(FactureLoyerPanel.PeriodeOptions(Bail(Periodicite.BiAnnuel), 2026).labels, Is.EqualTo(new[] { "1er Semestre", "2e Semestre" }));
        Assert.That(FactureLoyerPanel.PeriodeOptions(Bail(Periodicite.Annuel), 2026).labels, Is.EqualTo(new[] { "Année complète" }));
        var mois = FactureLoyerPanel.PeriodeOptions(Bail(Periodicite.mensuel), 2026);
        Assert.That(mois.labels, Has.Count.EqualTo(12));
        Assert.That((mois.labels[0], mois.labels[11], mois.ids[11]), Is.EqualTo(("Janvier", "Décembre", "12")));

        Assert.That(FactureLoyerPanel.LibellePeriode(Bail(Periodicite.trimestriel), 2026, 3), Is.EqualTo("3e Trimestre 2026"));
        Assert.That(FactureLoyerPanel.LibellePeriode(Bail(Periodicite.BiAnnuel), 2026, 2), Is.EqualTo("2e Semestre 2026"));
        Assert.That(FactureLoyerPanel.LibellePeriode(Bail(Periodicite.Annuel), 2026, 1), Is.EqualTo("Année 2026"));
        Assert.That(FactureLoyerPanel.LibellePeriode(Bail(Periodicite.mensuel), 2026, 11), Is.EqualTo("Novembre 2026"));
        Assert.That(FactureLoyerPanel.MoisFactures(Bail(Periodicite.trimestriel), 2026, 2), Is.EqualTo(3));
        Assert.That(FactureLoyerPanel.MoisFactures(Bail(Periodicite.BiAnnuel), 2026, 1), Is.EqualTo(6));
        Assert.That(FactureLoyerPanel.MoisFactures(Bail(Periodicite.Annuel), 2026, 1), Is.EqualTo(12));
        Assert.That(FactureLoyerPanel.MoisFactures(Bail(Periodicite.mensuel), 2026, 5), Is.EqualTo(1));

        var coupe = Bail(Periodicite.trimestriel, ("2026-04-30", Periodicite.mensuel));   // trimestriel à compter du 05/05/2026
        Assert.That(FactureLoyerPanel.PeriodeOptions(coupe, 2026).labels,
            Is.EqualTo(new[] { "Janvier", "Février", "Mars", "Avril", "2e Trimestre (mai à juin)", "3e Trimestre", "4e Trimestre" }));
        Assert.That(FactureLoyerPanel.LibellePeriode(coupe, 2026, 5), Is.EqualTo("2e Trimestre 2026 (mai à juin)"));
        Assert.That(FactureLoyerPanel.MoisFactures(coupe, 2026, 5), Is.EqualTo(2), "2 000 € ÷ 2 mois");
    }

    /// {loc.periodicite} imprime la périodicité de la période facturée quand le contexte la connaît (formulaire du loyer, loyer
    /// envoyé au lancement : celle gardée sur la ligne) ; sinon celle de la fiche, comme avant.
    [Test]
    public void La_variable_periodicite_suit_la_periode_facturee()
    {
        var loc = Bail(Periodicite.trimestriel, ("2026-06-30", Periodicite.mensuel));
        Assert.That(FactureVarResolver.Resolve("{loc.periodicite}", loc, null, new FactureContext { periodicite = "mensuel" }), Is.EqualTo("mensuel"));
        Assert.That(FactureVarResolver.Resolve("{loc.periodicite}", loc, null, new FactureContext()), Is.EqualTo("trimestriel"));
        var mai = Emis("loyer-2026-P5", "Loyer Mai 2026", "2026-05-05", "mensuel", 1000f, "AttenteEnvoi");
        loc.facturesEtat.Add(mai);
        Assert.That(FactureEnvoiAuto.Contexte(loc, mai).periodicite, Is.EqualTo("mensuel"));
        StringAssert.Contains("periodicite = PeriodiciteChoisie().ToString(),", Corps(Src("Facturation/FactureLoyerPanel.cs"), "FactureContext BuildContext()"));

        // Finitions (PROV-VAR) : {loc.provision} suit aussi la période facturée : fiche convertie à 300 € par trimestre, mai encore
        // mensuel : 100 €. Avant : 300,00 €, à côté d'une ligne de provision à 100 €. (Charges générales seules : les listes
        // spécifiques dépendent des Réglages ouverts ; chacune est convertie de la même façon.)
        Provisions(loc, 300f);
        Assert.That(FactureVarResolver.Resolve("{loc.provision}", loc, null, new FactureContext { periodicite = "mensuel" }), Is.EqualTo("100,00 €"));
        Assert.That(FactureVarResolver.Resolve("{loc.provision}", loc, null, FactureEnvoiAuto.Contexte(loc, mai)), Is.EqualTo("100,00 €"), "le loyer de mai envoyé au lancement");
        Assert.That(FactureVarResolver.Resolve("{loc.provision}", loc, null, new FactureContext { periodicite = "trimestriel" }), Is.EqualTo("300,00 €"));
        Assert.That(FactureVarResolver.Resolve("{loc.provision}", loc, null, new FactureContext()), Is.EqualTo("300,00 €"), "périodicité inconnue : la fiche, comme avant");
        // Sans historique (fiche passée au trimestriel sans date avant le lot 14, loyer de mai gardé « mensuel ») : comme avant.
        var sansDate = Provisions(Bail(Periodicite.trimestriel), 300f);
        Assert.That(FactureVarResolver.Resolve("{loc.provision}", sansDate, null, new FactureContext { periodicite = "mensuel" }), Is.EqualTo("300,00 €"), "sans historique : jamais convertie");
        StringAssert.Contains("foreach (var l in ListesCharges.Specifiques())", Corps(Src("Facturation/FactureVarResolver.cs"), "static float ProvisionFacturee("), "liste par liste, comme le formulaire");
        loc.provisionPourCharges = false;
        Assert.That(FactureVarResolver.Resolve("{loc.provision}", loc, null, new FactureContext { periodicite = "mensuel" }), Is.EqualTo("—"));
    }

    /// Gestion du loyer : la date d'effet est refusée AVANT toute écriture, l'avenant est rangé avant que les modalités ne soient
    /// remplacées, et la reprise de facturation propose les périodes du plan.
    [Test]
    public void Gestion_du_loyer_demande_la_date_et_refuse_avant_d_ecrire()
    {
        string rp = Src("RevisionLoyer/RevisionPanel.cs");
        StringAssert.Contains("BlocCaseDate(content, \"AvenantPeriodiciteBlock\"", rp, "le même bloc que l'avenant de loyer");
        StringAssert.Contains("\"Nouvelle périodicité à compter du\"", rp);
        StringAssert.Contains("if (DateIllisible(_avenantPerDate)) return", Corps(rp, "string DatesIllisibles()"), "date incomplète : refusée avec les autres");

        string valider = Corps(rp, "void ValiderInitialisation()");
        int refus = valider.IndexOf("Loyers.RefusAvenantPeriodicite(_loc, effetPeriodicite.Value, JourSaisi(), RepriseChoisie())", StringComparison.Ordinal);
        int avenantLoyer = valider.IndexOf("Loyers.EnregistrerAvenant(_loc, Iso(avenant));", StringComparison.Ordinal);
        int avenant = valider.IndexOf("Loyers.EnregistrerAvenantPeriodicite(_loc, effetPeriodicite.Value);", StringComparison.Ordinal);
        int ecrire = valider.IndexOf("\n        EcrireModalites();", StringComparison.Ordinal);
        Assert.That(refus, Is.GreaterThan(0), "le refus de la date d'effet");
        Assert.That(refus, Is.LessThan(avenantLoyer), "avant EnregistrerAvenant, qui écrit déjà l'historique des loyers");
        Assert.That(avenant, Is.GreaterThan(avenantLoyer).And.LessThan(ecrire), "l'ancienne périodicité est rangée avant EcrireModalites");
        StringAssert.Contains("if (AvenantPeriodiciteDemande())", valider);
        // Finitions (FIN-8) : réinitialisation par indice (la fenêtre se rouvre sur l'indice) : la date d'effet est dite aussi.
        // Reprise (FIN-8-R1) : et avant de passer aux paliers (PaliersPanel ne dit ensuite que « Paliers enregistrés »).
        const string toast = "if (effetPeriodicite.HasValue) UndoToast.Instance?.ShowInfo(\"Modalités du loyer enregistrées.\" + infoPeriodicite);";
        int paliers = valider.IndexOf("PaliersPanel.Open(_loc, _onSaved, this);", StringComparison.Ordinal);
        Assert.That(valider.IndexOf(toast, StringComparison.Ordinal), Is.GreaterThan(ecrire).And.LessThan(paliers), "le toast avant de passer aux paliers");
        int toastIndice = valider.LastIndexOf(toast, StringComparison.Ordinal);
        Assert.That(toastIndice, Is.GreaterThan(paliers), "le toast de la branche qui rouvre sur l'indice");
        Assert.That(toastIndice, Is.LessThan(valider.IndexOf("Open(_loc, _onSaved, Volet.Indice);", StringComparison.Ordinal)));
        StringAssert.Contains("CalendrierChange(_loc.periodiciteLoyer, _loc.moisFacturationLoyer, (Periodicite)periodiciteDropdown.value,", rp, "les mois effectifs");
        StringAssert.Contains("&& Loyers.DateAvenantRequise(_loc);", rp, "seulement si un loyer est déjà facturé");

        string reprise = Corps(rp, "void RefreshRepriseOptions()");
        StringAssert.Contains("Loyers.DateAvenantRequise(_loc) ? _loc : new Locataire { periodiciteLoyer = (Periodicite)periodiciteDropdown.value }", reprise);
        StringAssert.Contains("FacturationSuivi.Plan(fiche, y)", reprise, "les options « {année}-P{n} » sont les périodes du plan");
        // Finitions (FIN-4) : la recherche est passée dans PeriodeReprise (règle pure, essayée plus bas), qui regarde aussi l'année d'avant.
        StringAssert.Contains("var (anEnr, perEnr) = PeriodeReprise(_loc, rd);", reprise);
        StringAssert.Contains("FacturationSuivi.PeriodeIndex(loc, reprise.Year, reprise.Month)", Corps(rp, "public static (int annee, int periode) PeriodeReprise("),
            "la reprise enregistrée se retrouve par son rang dans le plan");
        StringAssert.Contains("if (RepriseChoisie() is string reprise) _loc.repriseFacturationISO = reprise;", Corps(rp, "void EcrireModalites()"));
    }

    /// Le formulaire du loyer : la ligne du suivi ouvre les options du plan de SON année, une ligne hors du plan est refusée (le
    /// menu retombait sur la première période), l'année ne reconstruit le menu que complète, et l'émission garde la périodicité
    /// de la période.
    [Test]
    public void Le_formulaire_du_loyer_lit_le_plan_de_l_annee()
    {
        string loyer = Src("Facturation/FactureLoyerPanel.cs");
        string ciblee = Corps(loyer, "void AppliquerLigneCiblee()");
        StringAssert.Contains("rang > FacturationSuivi.Plan(_loc, y).Count", ciblee, "ligne hors du plan : refusée, pas la première option");
        StringAssert.Contains("ConfirmDialog.Erreur(", ciblee);
        StringAssert.Contains("PoserPeriodes(y, rang);", ciblee, "les options du plan de l'année lue dans la clé");
        StringAssert.DoesNotContain("PeriodeOptions(_loc.periodiciteLoyer)", loyer, "plus les options de la périodicité de la fiche");

        string annee = Corps(loyer, "void RefreshPeriodes()");
        StringAssert.Contains("t.Length != 4", annee, "pas de reconstruction pendant la frappe de l'année");
        StringAssert.Contains("PeriodeGardee(_loc, _anneeOptions, PeriodeChoisie(), annee)", annee, "la période gardée par son mois de début");
        StringAssert.Contains("_ => { RefreshPeriodes(); RefreshEntetePreview(); RefreshEcheanceDefault(); RefreshNumero(); RefreshTauxDefault(); }", loyer);

        string charger = Corps(loyer, "void LoadIntoUI()");
        StringAssert.Contains("f.moisPeriode <= FacturationSuivi.Plan(_loc, anneeSel).Count", charger, "période mémorisée hors des options : la période en cours");
        StringAssert.Contains("DefaultPeriodeIndex(anneeSel)", charger);

        StringAssert.Contains("FactureEmission.Preparer(_loc, key, ComposedNumero(), PeriodiciteChoisie().ToString())", loyer);
        StringAssert.Contains("FacturationSuivi.PeriodiciteDe(_loc, ka, kp)", Corps(loyer, "void Finaliser("), "la ligne garde la périodicité de SA période");
        StringAssert.Contains("Periodicite PeriodiciteChoisie() => FacturationSuivi.PeriodiciteDe(_loc, PeriodeAnnee(), PeriodeChoisie());", loyer);
        StringAssert.Contains("bool afficheMensuel = _mensuel != null && _mensuel.isOn && moisParPeriode > 1;", loyer, "la ligne « mensuel » suit la période");
        StringAssert.DoesNotContain("_loc.periodiciteLoyer != Periodicite.mensuel", loyer);
    }

    // ── Lot 16 · provisions converties avec la périodicité, provisions appelées, parts par liste ──

    /// Le bail des essais avec des provisions, par période de la périodicité ACTUELLE : la générale, et la liste « tf ».
    static Locataire Provisions(Locataire loc, float generale, float tf = 0f)
    {
        loc.provisionPourCharges = true;
        loc.provisionPourChargeValue = generale;
        if (tf > 0f) loc.regulListes.Add(new RegulListe { listeId = "tf", dateISO = "2027-03-01", provision = tf });
        return loc;
    }

    /// Décision du 05/11 : la provision, saisie « par période », est convertie d'office quand la périodicité change dans Gestion du
    /// loyer (100 € par mois → 300 € par trimestre), d'après la valeur ENREGISTRÉE, sauf champ retouché. Avant : gardée telle
    /// quelle, soit 100 € par trimestre : un tiers des provisions de l'année, vu seulement à la régularisation.
    [Test]
    public void Changer_de_periodicite_convertit_la_provision()
    {
        Assert.That(Loyers.ProvisionConvertie(100f, Periodicite.mensuel, Periodicite.trimestriel), Is.EqualTo(300f));
        Assert.That(Loyers.ProvisionConvertie(300f, Periodicite.trimestriel, Periodicite.mensuel), Is.EqualTo(100f));
        Assert.That(Loyers.ProvisionConvertie(100f, Periodicite.mensuel, Periodicite.BiAnnuel), Is.EqualTo(600f));
        Assert.That(Loyers.ProvisionConvertie(100f, Periodicite.mensuel, Periodicite.Annuel), Is.EqualTo(1200f));
        Assert.That(Loyers.ProvisionConvertie(100f, Periodicite.trimestriel, Periodicite.mensuel), Is.EqualTo(33.33f).Within(0.001f), "au centime");
        Assert.That(Loyers.ProvisionConvertie(33.333f, Periodicite.mensuel, Periodicite.mensuel), Is.EqualTo(33.333f), "même périodicité : le montant tel quel");
        // Un aller-retour sur le champ affiché dériverait : 100 € par trimestre → 33,33 € → 99,99 €. D'où la valeur enregistrée.
        Assert.That(Loyers.ProvisionConvertie(33.33f, Periodicite.mensuel, Periodicite.trimestriel), Is.EqualTo(99.99f).Within(0.001f));

        string rp = Src("RevisionLoyer/RevisionPanel.cs");
        StringAssert.Contains("ConvertirProvisions();", Corps(rp, "void OnPeriodiciteChanged()"), "à chaque changement de périodicité à l'écran");
        string convertir = Corps(rp, "void ConvertirProvisions()");
        StringAssert.Contains("Loyers.ProvisionConvertie(_loc.provisionPourChargeValue, _loc.periodiciteLoyer, choisie)", convertir, "la valeur et la périodicité enregistrées");
        StringAssert.Contains("foreach (var (id, _, prov) in _listesChamps)", convertir, "chaque liste aussi");
        StringAssert.Contains("ListesCharges.De(_loc, id)?.provision", convertir);
        StringAssert.Contains("champ.text == auto", Corps(rp, "bool SuivreProvision("), "un champ retouché à la main n'est pas converti");
        // Finitions (FIN-5) : le champ à côté de la case, caché dès qu'une liste de charges existe, suit toujours la conversion : il
        // n'allume le toast que s'il est à l'écran (RefreshRegulVisibility montre ou cache ProvContainer).
        StringAssert.Contains("_loc.periodiciteLoyer, choisie).ToString()) && ProvContainer().activeSelf;", convertir);
        StringAssert.Contains("ProvContainer().SetActive(toggleProvisions.isOn && !avecListes);", Corps(rp, "void RefreshRegulVisibility()"));
        // L'écouteur de l'ouverture précédente se déclenche quand la périodicité est posée : il ne doit rien convertir.
        string ouvrir = Corps(rp, "void OuvrirLibre(");
        int vide = ouvrir.IndexOf("_provAuto.Clear();", StringComparison.Ordinal);
        Assert.That(vide, Is.GreaterThan(0));
        Assert.That(vide, Is.LessThan(ouvrir.IndexOf("periodiciteDropdown.value = (int)loc.periodiciteLoyer;", StringComparison.Ordinal)));
        StringAssert.Contains("_provAuto[ProvCase] = provisionValue.text;", ouvrir);
    }

    /// Correction 5 de la vérification : la fiche convertie (300 € par trimestre), une période de l'ancienne périodicité appelle la
    /// provision ramenée à la sienne (mai, mensuel : 100 €) ; une période coupée par l'avenant appelle la provision entière.
    [Test]
    public void Une_periode_appelle_la_provision_de_sa_periodicite()
    {
        // Mensuel à 100 € (10 € de taxe foncière), trimestriel à compter du 1er juillet 2026 : la fiche porte 300 € et 30 €.
        var loc = Provisions(Bail(Periodicite.trimestriel, ("2026-06-30", Periodicite.mensuel)), 300f, 30f);
        Assert.That(Loyers.ProvisionPeriode(loc, 2026, 5, ""), Is.EqualTo(100f), "mai, encore mensuel : 100 €, pas 300 €");
        Assert.That(Loyers.ProvisionPeriode(loc, 2026, 7, ""), Is.EqualTo(300f), "3e trimestre");
        Assert.That(Loyers.ProvisionPeriode(loc, 2025, 11, ""), Is.EqualTo(100f), "l'année d'avant reste mensuelle");
        Assert.That(Loyers.ProvisionPeriode(loc, 2027, 2, ""), Is.EqualTo(300f));
        Assert.That(Loyers.ProvisionPeriode(loc, 2026, 5, "tf"), Is.EqualTo(10f), "chaque liste aussi");
        Assert.That(Loyers.ProvisionPeriode(loc, 2026, 8, "tf"), Is.EqualTo(30f));

        // Trimestriel à compter du 1er mai : le 2e trimestre ne facture que mai et juin, mais appelle la provision du trimestre.
        var coupe = Provisions(Bail(Periodicite.trimestriel, ("2026-04-30", Periodicite.mensuel)), 300f);
        Assert.That(FacturationSuivi.Plan(coupe, 2026)[4].Coupee, Is.True);
        Assert.That(Loyers.ProvisionPeriode(coupe, 2026, 5, ""), Is.EqualTo(300f), "entière, comme une arrivée ou un départ");
        Assert.That(Loyers.ProvisionPeriode(coupe, 2026, 4, ""), Is.EqualTo(100f), "avril reste un mois");

        // Sans avenant de périodicité : la provision de la fiche, telle quelle.
        var sans = Provisions(Bail(Periodicite.trimestriel), 123.456f);
        Assert.That(Loyers.ProvisionPeriode(sans, 2026, 2, ""), Is.EqualTo(123.456f));
        Assert.That(Loyers.ProvisionPeriode(sans, 2026, 2, "tf"), Is.Zero, "liste sans provision");
        Assert.That(Loyers.ProvisionPeriode(null, 2026, 1, ""), Is.Zero);
    }

    /// Le scénario du plan : 100 € par mois, trimestriel au 1er juillet 2026, fiche convertie à 300 €. La régularisation 2026
    /// déduit ce qui a été appelé, période par période : 6 × 100 + 2 × 300 = 1 200 €. Avant : 300 × 8 périodes = 2 400 €.
    [Test]
    public void La_regul_de_l_annee_du_changement_compte_les_provisions_appelees()
    {
        var loc = Provisions(Bail(Periodicite.trimestriel, ("2026-06-30", Periodicite.mensuel)), 300f, 30f);
        // Aucune provision imprimée connue (loyers émis avant D6-07) : la fiche ramenée à la périodicité de chaque période.
        Assert.That(Loyers.ProvisionsAppelees(loc, 2026, ""), Is.EqualTo(1200f));
        Assert.That(ListesCharges.Provision(loc, "") * PeriodesFacturees(loc, 2026), Is.EqualTo(2400f), "l'ancien calcul");
        Assert.That(Loyers.ProvisionsAppelees(loc, 2026, "tf"), Is.EqualTo(120f), "6 × 10 + 2 × 30");
        Assert.That(Loyers.ProvisionsAppelees(loc, 2025, ""), Is.EqualTo(1200f), "12 mois à 100 €");
        Assert.That(Loyers.ProvisionsAppelees(loc, 2027, ""), Is.EqualTo(1200f), "4 trimestres à 300 €");

        // Le montant imprimé sur un loyer fait foi quand il est connu (D6-07) : janvier à 90 € (12 € de taxe foncière), le 3e
        // trimestre à 330 € (sans taxe foncière). Février, émis avant (provisions inconnues), retombe sur la fiche ramenée.
        var janvier = Emis("loyer-2026-P1", "Loyer Janvier 2026", "2026-01-05", "mensuel", 1000f);
        janvier.provisionsConnues = true; janvier.provision = 90f;
        janvier.provisionsListes = new List<MontantListe> { new MontantListe { listeId = "tf", montant = 12f } };
        var t3 = Emis("loyer-2026-P7", "Loyer 3e trimestre 2026", "2026-07-05", "trimestriel", 3000f);
        t3.provisionsConnues = true; t3.provision = 330f;
        var fevrier = Emis("loyer-2026-P2", "Loyer Février 2026", "2026-02-05", "mensuel", 1000f);
        fevrier.provision = 999f;   // sans provisionsConnues : pas lu
        loc.facturesEtat.AddRange(new[] { janvier, t3, fevrier });
        Assert.That(Loyers.ProvisionsAppelees(loc, 2026, ""), Is.EqualTo(1220f), "90 + 5 × 100 + 330 + 300");
        Assert.That(Loyers.ProvisionsAppelees(loc, 2026, "tf"), Is.EqualTo(92f), "12 + 5 × 10 + 0 + 30");

        // Départ le 15/08/2026 : le 4e trimestre n'est pas facturé, il n'appelle rien.
        loc.dateSortieISO = "2026-08-15";
        Assert.That(Loyers.ProvisionsAppelees(loc, 2026, ""), Is.EqualTo(920f));
        Assert.That(Loyers.ProvisionsAppelees(null, 2026, ""), Is.Zero);

        // Le formulaire de régularisation propose cette somme, et son libellé le dit.
        string regul = Src("Facturation/FactureRegulPanel.cs");
        StringAssert.Contains("=> ListesSel().Sum(id => Loyers.ProvisionsAppelees(_loc, SelectedYear(), id));", regul);
        StringAssert.DoesNotContain("Loyers.PeriodesFacturees(", regul, "plus provision de la fiche × nombre de périodes");
        StringAssert.Contains("\"Provisions déjà versées (appelées avec les loyers de l'année)\"", regul);
    }

    /// Sans avenant ni provision imprimée connue : l'ancien calcul, provision de la fiche × périodes facturées (départ compris).
    /// Et sans avenant aussi (D6-N5), une provision changée en cours d'année n'est plus recomptée sur toute l'année.
    [Test]
    public void Sans_avenant_les_provisions_appelees_suivent_les_periodes_facturees()
    {
        var loc = Provisions(Bail(Periodicite.trimestriel), 250.5f, 40f);
        Assert.That(Loyers.ProvisionsAppelees(loc, 2026, ""), Is.EqualTo(1002f));
        Assert.That(Loyers.ProvisionsAppelees(loc, 2026, "tf"), Is.EqualTo(160f));
        loc.dateSortieISO = "2026-05-15";   // départ : 1er et 2e trimestres
        Assert.That(Loyers.ProvisionsAppelees(loc, 2026, ""), Is.EqualTo(ListesCharges.Provision(loc, "") * PeriodesFacturees(loc, 2026)));
        Assert.That(Loyers.ProvisionsAppelees(loc, 2027, ""), Is.Zero);

        // Le 1er trimestre a été appelé à 200 € (imprimé), puis la fiche est passée à 250,50 € : 200 + 250,50, pas 2 × 250,50.
        var t1 = Emis("loyer-2026-P1", "Loyer 1er trimestre 2026", "2026-01-05", "trimestriel", 3000f);
        t1.provisionsConnues = true; t1.provision = 200f;
        loc.facturesEtat.Add(t1);
        Assert.That(Loyers.ProvisionsAppelees(loc, 2026, ""), Is.EqualTo(450.5f));
    }

    /// D6-16 : dans une régularisation regroupée, chaque liste déduit sa part de la provision SAISIE dans le formulaire, au prorata
    /// de ce qui a été appelé pour elle. Avant : sa provision de fiche, quelle que soit la saisie ; la première liste absorbait
    /// tout l'écart (deux lignes fausses dans le suivi et Créances, au total juste).
    [Test]
    public void Les_parts_par_liste_repartissent_la_provision_saisie()
    {
        // Appelé dans l'année : 900 € (générale) et 300 € (taxe foncière) ; le champ est corrigé à 900 € au lieu de 1 200 €.
        Assert.That(FactureRegulPanel.PartProvision(900f, 300f, 1200f), Is.EqualTo(225f));
        Assert.That(FactureRegulPanel.PartProvision(900f, 900f, 1200f), Is.EqualTo(675f));
        Assert.That(FactureRegulPanel.PartProvision(1200f, 300f, 1200f), Is.EqualTo(300f), "saisie laissée telle quelle : ce qui a été appelé");
        Assert.That(FactureRegulPanel.PartProvision(100f, 1f, 3f), Is.EqualTo(33.33f).Within(0.001f), "au centime");
        Assert.That(FactureRegulPanel.PartProvision(500f, 0f, 0f), Is.Zero, "rien d'appelé : la première liste porte tout");

        var listes = new List<string> { "generale", "tf" };
        Func<string, float> charges = id => id == "tf" ? 800f : 2000f, appelee = id => id == "tf" ? 300f : 900f;
        float ttcTotal = Tva.Ttc(2800f - 900f, 20f);
        var parts = FactureRegulPanel.MontantsParListe(listes, charges, id => FactureRegulPanel.PartProvision(900f, appelee(id), 1200f), 1, ttcTotal);
        Assert.That(parts["tf"], Is.EqualTo(690f).Within(0.01f), "800 − 225 = 575 HT ; avant : 800 − 300 = 500 HT, 600 TTC");
        Assert.That(parts["generale"], Is.EqualTo(1590f).Within(0.01f), "2 000 − 675 = 1 325 HT ; avant, tout l'écart : 1 680 TTC");

        string regul = Src("Facturation/FactureRegulPanel.cs");
        string instance = Corps(regul, "Dictionary<string, float> MontantsParListe(List<string> listes, int year, float ttcTotal)");
        StringAssert.Contains("float saisie = ParseF(_provisions.text);", instance, "la provision saisie dans le formulaire");
        StringAssert.Contains("Loyers.ProvisionsAppelees(_loc, year, id)", instance);
        StringAssert.Contains("PartProvision(saisie, appelees[id], total)", instance);
    }

    /// D5-13 ← D5-N8 : la part TTC d'une liste passe par Tva (décimal, « au plus loin »), comme tout le reste : 1 234,45 € à 10 %
    /// font 123,45 € de TVA (avant, en float : 123,44 €). Et les lignes de provision d'un loyer sont arrondies une à une, comme le
    /// XML de la plateforme : la somme imprimée est la somme des lignes.
    [Test]
    public void Les_arrondis_des_parts_et_des_provisions_suivent_le_chemin_commun()
    {
        var listes = new List<string> { "generale", "tf" };
        var parts = FactureRegulPanel.MontantsParListe(listes, id => id == "tf" ? 1234.45f : 0f, id => 0f, 1, 5000f, 10f);
        Assert.That(parts["tf"], Is.EqualTo(1234.45f + 123.45f).Within(0.004f));
        Assert.That(parts["tf"], Is.EqualTo(Tva.Ttc(1234.45f, 10f)).Within(0.001f));
        Assert.That(parts["generale"] + parts["tf"], Is.EqualTo(5000f).Within(0.01f), "la somme des lignes redonne le total");

        // Deux provisions de 10,005 € : 10,01 € + 10,01 € = 20,02 €, comme le XML (avant : la somme arrondie, 20,01 € au PDF).
        Assert.That(Tva.Cents(10.005f) + Tva.Cents(10.005f), Is.EqualTo(20.02f).Within(0.001f));
        Assert.That(Tva.Cents(10.005f + 10.005f), Is.EqualTo(20.01f).Within(0.001f), "l'ancien calcul");
        string loyer = Src("Facturation/FactureLoyerPanel.cs");
        StringAssert.Contains("float ProvisionSaisie() => Cents(ParseF(_provision.text)) + _provListes.Sum(x => Cents(ParseF(x.inp.text)));", loyer);
        string lignes = Corps(loyer, "List<KeyValuePair<string, float>> LignesProvision()");
        StringAssert.Contains("Cents(ParseF(_provision.text))", lignes);
        StringAssert.Contains("Cents(ParseF(inp.text))", lignes);
        StringAssert.Contains("ligne.provision = Cents(ParseF(_provision.text));", loyer, "la provision gardée sur la ligne est celle imprimée");
    }

    /// Le formulaire du loyer : la provision proposée (générale et par liste) est celle de la période AFFICHÉE, et elle se
    /// rafraîchit avec la période tant qu'elle n'a pas été retouchée à la main. Avant : lue une fois à l'ouverture, dans la
    /// périodicité de la fiche (mai recevait 300 € après la conversion).
    [Test]
    public void La_provision_du_formulaire_du_loyer_suit_la_periode()
    {
        string loyer = Src("Facturation/FactureLoyerPanel.cs");
        string defaut = Corps(loyer, "string DefaultProvision(string listeId)");
        StringAssert.Contains("Loyers.ProvisionPeriode(_loc, PeriodeAnnee(), PeriodeChoisie(), listeId)", defaut, "la fiche ramenée à la périodicité de la période");
        StringAssert.Contains("var corrigee = Corrigee();", defaut, "la facture partie de la période affichée");
        StringAssert.Contains("ProvisionProposee(f, fiche, corrigee)", defaut, "sa correction garde le montant imprimé (D6-07)");
        // Finitions (FIN-6) : un état « Payé », « Envoyé » ou « Impayé » posé à la main (pas de PDF) n'est pas une facture à corriger
        // (Tva.FacturePartie, lue par Corrigee) : la période reçoit la provision de la fiche (charges générales et listes), et le taux du bail.
        StringAssert.Contains("return Tva.FacturePartie(ligne) ? ligne : null;", Corps(loyer, "FactureEtat Corrigee()"));
        Assert.That(Tva.FacturePartie(new FactureEtat { key = "loyer-2026-P1", type = "Loyer", statut = "Paye" }), Is.False, "la ligne posée à la main est écartée");
        Assert.That(FactureLoyerPanel.ProvisionProposee(new FactureInfo { saved = true, provisionMontant = 80f }, 100f, null), Is.EqualTo(100f), "écartée : la fiche, pas la dernière facture");
        Assert.That(FactureLoyerPanel.ProvisionProposee(new FactureInfo { saved = true, provisionMontant = 80f }, 100f, new FactureEtat { key = "loyer-2026-P1", statut = "Paye" }), Is.EqualTo(80f), "ce que la ligne donnait avant");
        StringAssert.Contains("RefreshProvisionDefault();", Corps(loyer, "void RefreshEcheanceDefault()"), "rafraîchie avec la période et l'année");
        StringAssert.Contains("inp.text == auto) inp.text = def;", Corps(loyer, "void RefreshProvisionDefault()"), "sauf champ retouché à la main");
        string charger = Corps(loyer, "void LoadIntoUI()");
        StringAssert.Contains("_autoProvisions[\"\"] = DefaultProvision(\"\");", charger);
        StringAssert.DoesNotContain("ListesCharges.Provision(_loc, \"\")", charger, "plus la provision de la fiche lue une fois");
        StringAssert.Contains("_autoProvisions[id] = DefaultProvision(id);", Corps(loyer, "void ChargerProvisionsListes(FactureInfo f)"));
    }
}
