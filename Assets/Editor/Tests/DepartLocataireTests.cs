using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Départ d'un locataire (décisions du 01/10) : décompte de sortie, délai de
/// restitution, régul au prorata de présence, loyer à corriger, archivage, alertes.
/// Règles dans `DepartLocataire`, `ListesCharges.QuotePartAuProrata`,
/// `Loyers.PeriodesFacturees` et `FacturationAlertes` ; ces tests les verrouillent.
public class DepartLocataireTests
{
    static string Iso(DateTime d) => d.ToString("yyyy-MM-dd");

    // Loyer 12 000 €/an HT au trimestre, bail au 01/01/2026, parcours terminé.
    static Locataire Bail(string sortie = null) => new Locataire
    {
        id = "L", Name = "Test",
        typeRevision = TypeRevision.Aucune,
        loyerAnnuel = 12000f, loyerDepart = 12000f,
        periodiciteLoyer = Periodicite.trimestriel,
        dateDebutBailISO = "2026-01-01", dateFinBailISO = "2034-12-31",
        depotInitialise = true,
        dateSortieISO = sortie,
    };

    static FactureEtat Emise(string key, string statut, float montant, float loyerHT = 0f)
        => new FactureEtat { key = key, type = key.StartsWith("loyer") ? "Loyer" : "Depot", statut = statut,
                             pdfPath = key + ".pdf", montant = montant, loyerHT = loyerHT,
                             echeanceISO = "2026-04-01" };

    static bool A(Locataire loc, FacturationAlertes.AlerteType t)
        => FacturationAlertes.Pour(loc).Any(a => a.type == t);

    // ── Données ────────────────────────────────────────────────────────────────

    [Test]
    public void Les_champs_de_sortie_survivent_a_un_aller_retour_json()
    {
        var loc = Bail("2026-05-15");
        loc.dateEtatDesLieuxISO = "2026-05-15";
        loc.etatDesLieux = "EDL-sortie.pdf";
        loc.delaiRestitutionMois = 3;
        loc.archive = true;
        loc.facturesEtat.Add(Emise("loyer-2026-P2", "Envoye", 3600f, 1483.52f));

        var relu = JsonUtility.FromJson<Locataire>(JsonUtility.ToJson(loc));

        Assert.That(relu.dateEtatDesLieuxISO, Is.EqualTo("2026-05-15"));
        Assert.That(relu.etatDesLieux, Is.EqualTo("EDL-sortie.pdf"));
        Assert.That(relu.delaiRestitutionMois, Is.EqualTo(3));
        Assert.That(relu.archive, Is.True);
        Assert.That(relu.facturesEtat[0].loyerHT, Is.EqualTo(1483.52f));
    }

    // ── Décompte de sortie ─────────────────────────────────────────────────────

    [Test]
    public void Le_decompte_deduit_retenues_et_creances_du_depot()
    {
        // Dépôt 3 000, retenue 500 sans TVA, loyer du T2 impayé 1 783,52 → avoir 716,48.
        var porte = new RetenueSortie { libelle = "Réparation porte", ht = 500f };
        var t2 = new FactureEtat { key = "loyer-2026-P2", numero = "2026/05001", libelle = "Loyer 2e trimestre 2026", montant = 1783.52f };
        var d = DepartLocataire.Calculer(3000f, new[] { porte }, new[] { t2 });
        Assert.That(d.ttc, Is.EqualTo(-716.48f).Within(0.001f));
        Assert.That(d.tva, Is.Zero);
        Assert.That(d.lignes.Select(l => l.Key), Is.EqualTo(new[]
            { "Retenue — Réparation porte", "Facture n° 2026/05001 — Loyer 2e trimestre 2026" }));

        // Avec TVA, la retenue compte TTC : 500 HT + 100 de TVA.
        porte.tva = true;
        d = DepartLocataire.Calculer(3000f, new[] { porte }, new[] { t2 });
        Assert.That(d.tva, Is.EqualTo(100f));
        Assert.That(d.ttc, Is.EqualTo(-616.48f).Within(0.001f));
        Assert.That(d.totalHT + d.tva, Is.EqualTo(d.ttc).Within(0.001f), "HT + TVA = TTC");

        // Retenues supérieures au dépôt : c'est une facture. Une retenue vide est ignorée.
        d = DepartLocataire.Calculer(1000f, new[] { new RetenueSortie { ht = 1500f }, new RetenueSortie() }, null);
        Assert.That(d.ttc, Is.EqualTo(500f));
        Assert.That(d.lignes, Has.Count.EqualTo(1));
    }

    [Test]
    public void Sans_TVA_le_tableau_n_a_qu_une_ligne_Total()
    {
        // Demande du 02/10 : dépôt de garantie, ou décompte sans retenue soumise → ni HT ni
        // TVA, un seul « Total ». (La mention, elle, reste un choix de la facture.)
        var d = new FacturePdfService.Data { totalHT = -3100f, tva = 0f, ttc = -3100f, masquerTva = true };
        string html = FacturePdfService.BuildHtml(d);
        // (Pas de recherche de « TVA » seul : le logo en base64 pourrait le contenir.)
        Assert.That(html, Does.Not.Contain("<td>TVA"));
        Assert.That(html, Does.Not.Contain("Total H.T."));
        Assert.That(html, Does.Not.Contain("Total T.T.C."));
        Assert.That(html, Does.Contain("<td>Total</td>"));

        // Témoin : sans l'option, les trois lignes habituelles (loyer, refacturation…).
        d.masquerTva = false;
        html = FacturePdfService.BuildHtml(d);
        Assert.That(html, Does.Contain("Total H.T."));
        Assert.That(html, Does.Contain("TVA 20%"));
        Assert.That(html, Does.Contain("Total T.T.C."));
    }

    [Test]
    public void Une_correction_du_decompte_retrouve_les_creances_deja_soldees()
    {
        var loc = Bail("2026-05-15");
        var a = Emise("refac-a", "Paye", 100f); a.pdfPath = "refac.pdf";
        var b = Emise("refac-b", "Paye", 50f);  b.pdfPath = "refac.pdf";
        loc.facturesEtat.AddRange(new[] { a, b, Emise("loyer-2026-P1", "Impaye", 3600f) });
        Assert.That(DepartLocataire.CreancesProposees(loc).Select(c => c.key), Is.EqualTo(new[] { "loyer-2026-P1" }));
        var c2 = DepartLocataire.CreancesProposees(loc, new[] { "refac-a" });
        Assert.That(c2, Has.Count.EqualTo(2));
        Assert.That(c2.Single(c => c.key == "refac-a").montant, Is.EqualTo(150f), "facture à deux lignes : son total");
    }

    [Test]
    public void Le_suivi_porte_le_decompte_a_la_date_limite_de_restitution()
    {
        var loc = Bail("2026-05-15");
        loc.depotDeGarantie = 3000f;
        loc.dateRevisionDepotISO = "2026-12-01";   // après le départ : plus planifiée
        var l = FacturationSuivi.Lignes(loc, 2026);
        var dec = l.Single(x => x.key == DepartLocataire.CleDecompte);
        Assert.That(dec.type, Is.EqualTo("Depot"));
        Assert.That(dec.echeanceISO, Is.EqualTo("2026-07-15"));
        Assert.That(l.Any(x => x.key == "depot-2026"), Is.False);
        Assert.That(FacturationSuivi.Lignes(Bail(), 2026).Any(x => x.key == DepartLocataire.CleDecompte), Is.False);
    }

    [Test]
    public void Le_delai_de_restitution_part_du_dernier_jour_de_location()
    {
        var loc = Bail("2026-05-15");
        Assert.That(DepartLocataire.EcheanceRestitution(loc, out var d), Is.True);
        Assert.That(Iso(d), Is.EqualTo("2026-07-15"), "2 mois par défaut");
        loc.delaiRestitutionMois = 3;
        DepartLocataire.EcheanceRestitution(loc, out d);
        Assert.That(Iso(d), Is.EqualTo("2026-08-15"));
        Assert.That(DepartLocataire.EcheanceRestitution(Bail(), out _), Is.False, "sans départ, pas de délai");
    }

    [Test]
    public void Les_creances_proposees_sont_une_ligne_par_facture_hors_payees()
    {
        var loc = Bail("2026-05-15");
        var a = Emise("refac-a", "Envoye", 100f); a.pdfPath = "refac.pdf";
        var b = Emise("refac-b", "Envoye", 50f);  b.pdfPath = "refac.pdf";
        loc.facturesEtat.AddRange(new[] { a, b, Emise("loyer-2026-P1", "Paye", 3600f),
                                          Emise(DepartLocataire.CleDecompte, "Envoye", -700f) });
        var c = DepartLocataire.CreancesProposees(loc);
        Assert.That(c, Has.Count.EqualTo(1), "payée et décompte exclus, refacturation fusionnée");
        Assert.That(c[0].montant, Is.EqualTo(150f));
        Assert.That(loc.facturesEtat.First(x => x.key == "refac-a").montant, Is.EqualTo(100f), "le stocké n'est pas touché");
    }

    // ── Régul au prorata de présence ───────────────────────────────────────────

    [Test]
    public void La_quote_part_de_regul_suit_les_jours_de_presence()
    {
        var eau = new ChargeBatiment { id = "eau", cout = 1000f, dateISO = "2026-10-01", tousLocataires = true };
        // Départ le 15/05/2026 : 135 jours sur 365.
        Assert.That(ListesCharges.QuotePartAuProrata(eau, Bail("2026-05-15"), null), Is.EqualTo(369.86f).Within(0.001f));
        // Arrivée le 01/09/2026 (premier bail) : 122 jours, charge de mars comprise.
        var arrive = Bail(); arrive.dateDebutBailISO = "2026-09-01";
        eau.dateISO = "2026-03-01";
        Assert.That(ListesCharges.QuotePartAuProrata(eau, arrive, null), Is.EqualTo(334.25f).Within(0.001f));
        // Présent toute l'année : quote-part entière.
        Assert.That(ListesCharges.QuotePartAuProrata(eau, Bail(), null), Is.EqualTo(1000f));
    }

    [Test]
    public void Un_lot_qui_change_d_occupant_compte_une_fois_dans_le_partage()
    {
        // Décision du 06/10 : 2 lots à parts égales ; lot 2 occupé par B jusqu'au 15/05,
        // puis par C dès le 16/05. Taxe foncière 2026 : 1 200 €.
        Locataire Loc(string id, int lot, string debut, string sortie = null)
        {
            var l = Bail(sortie); l.id = id; l.Name = id; l.lotBatiment = lot; l.dateDebutBailISO = debut;
            return l;
        }
        var a = Loc("A", 1, "2026-01-01");
        var b = Loc("B", 2, "2026-01-01", "2026-05-15");
        var c = Loc("C", 2, "2026-05-16");
        var taxe = new ChargeBatiment { id = "tf", cout = 1200f, dateISO = "2026-10-01", tousLocataires = true,
            ratios = { new ChargeRatio("A", 50f), new ChargeRatio("B", 50f), new ChargeRatio("C", 50f) } };
        var bat = new Batiment { locataireDuBatiment = { a, b, c }, charges = { taxe } };

        Assert.That(ListesCharges.QuotePartAuProrata(taxe, a, bat), Is.EqualTo(600f).Within(0.01f), "A paie sa part entière, pas 400");
        Assert.That(ListesCharges.QuotePartAuProrata(taxe, b, bat), Is.EqualTo(221.92f).Within(0.01f), "600 × 135/365");
        Assert.That(ListesCharges.QuotePartAuProrata(taxe, c, bat), Is.EqualTo(378.08f).Within(0.01f), "600 × 230/365");

        // Lot non renseigné (0) : chacun compte, comme avant.
        b.lotBatiment = 0; c.lotBatiment = 0;
        Assert.That(ListesCharges.QuotePart(taxe, a, bat), Is.EqualTo(400f).Within(0.01f));
    }

    [Test]
    public void Le_remplacant_reprend_le_lot_et_la_part_de_l_ancien()
    {
        // Décision du 06/10 : B part le 15/05, « un nouveau locataire reprend le lot ».
        var b = Bail("2026-05-15"); b.id = "B"; b.lotBatiment = 2; b.tailleLot = 120f; b.listesConcernees = new List<string> { "" };
        var c = new Locataire { id = "C" };
        var taxe = new ChargeBatiment { id = "tf", cout = 1200f, dateISO = "2026-10-01", tousLocataires = false,
            locatairesConcernes = { "B" }, ratios = { new ChargeRatio("B", 50f) } };
        var vieille = new ChargeBatiment { id = "eau25", cout = 300f, dateISO = "2025-06-30", ratios = { new ChargeRatio("B", 50f) } };
        var bat = new Batiment { locataireDuBatiment = { b, c }, charges = { taxe, vieille } };

        DepartLocataire.PreparerRemplacant(b, c, bat);
        Assert.That(c.lotBatiment, Is.EqualTo(2));
        Assert.That(c.tailleLot, Is.EqualTo(120f));
        Assert.That(c.dateDebutBailISO, Is.EqualTo("2026-05-16"), "le lendemain du départ");
        Assert.That(taxe.ratios.Exists(r => r.locataireId == "C" && r.part == 50f), Is.True, "même part que B");
        Assert.That(taxe.locatairesConcernes, Does.Contain("C"));
        Assert.That(vieille.ratios.Exists(r => r.locataireId == "C"), Is.False, "pas les charges d'avant son arrivée");
        // Et le partage au prorata en découle : B 221,92, C 378,08 (lot compté une fois).
        Assert.That(ListesCharges.QuotePartAuProrata(taxe, c, bat), Is.EqualTo(1200f * 230f / 365f).Within(0.01f));
        // Fiche du remplaçant supprimée : sa part reste dans la charge mais ne compte plus.
        bat.locataireDuBatiment.Remove(c);
        Assert.That(ListesCharges.QuotePart(taxe, b, bat), Is.EqualTo(1200f));
    }

    [Test]
    public void Une_cession_garde_le_bail_et_trace_l_ancien_titulaire()
    {
        // Décision du 06/10 : cession = même fiche, nouvelles coordonnées, historique.
        var loc = Bail(); loc.Name = "SARL Ancien"; loc.siretNumber = "111"; loc.emailLocataire = "a@x.fr"; loc.depotDeGarantie = 3600f;
        loc.factureLoyer = new FactureInfo { destNom = "SARL Ancien", emailDest = "a@x.fr" };
        var d = new DateTime(2026, 6, 1);

        Assert.That(Cessions.Verifier(loc, d, ""), Is.Not.Null, "nom obligatoire");
        Assert.That(Cessions.Verifier(loc, new DateTime(2025, 12, 31), "SARL Nouveau"), Is.Not.Null, "pas avant le bail");
        loc.dateSortieISO = "2026-05-15";
        Assert.That(Cessions.Verifier(loc, d, "SARL Nouveau"), Is.Not.Null, "pas après le départ");
        loc.dateSortieISO = null;
        Assert.That(Cessions.Verifier(loc, d, "SARL Nouveau"), Is.Null);

        Cessions.Appliquer(loc, d, "SARL Nouveau", "222", "1 rue Neuve", "n@x.fr");
        Assert.That(loc.Name, Is.EqualTo("SARL Nouveau"));
        Assert.That(loc.depotDeGarantie, Is.EqualTo(3600f), "le dépôt reste tel quel");
        Assert.That(loc.factureLoyer.destNom, Is.Empty, "la prochaine facture part au cessionnaire");
        Assert.That(loc.factureLoyer.emailDest, Is.Empty);
        Assert.That(Cessions.Texte(loc), Is.EqualTo("cédé par SARL Ancien le 01/06/2026"));

        var relu = JsonUtility.FromJson<Locataire>(JsonUtility.ToJson(loc));
        Assert.That(relu.cessions, Has.Count.EqualTo(1));
        Assert.That(relu.cessions[0].ancienSiret, Is.EqualTo("111"));
    }

    [Test]
    public void La_refacturation_propose_le_prorata_si_le_locataire_n_a_pas_ete_la_toute_l_annee()
    {
        // Décision du 01/10 : au prorata ou en entier (les locataires s'arrangent entre eux).
        var parti = Bail("2026-06-30");
        var taxe = new ChargeBatiment { id = "tf", cout = 1200f, dateISO = "2026-10-01" };
        var bat = new Batiment { locataireDuBatiment = { parti }, charges = { taxe } };
        Assert.That(ListesCharges.PresencePartielle(parti, taxe), Is.True);
        Assert.That(ListesCharges.PresencePartielle(Bail(), taxe), Is.False, "là toute l'année : pas de choix à faire");
        Assert.That(ListesCharges.QuotePart(taxe, parti, bat), Is.EqualTo(1200f), "en entier");
        Assert.That(ListesCharges.QuotePartAuProrata(taxe, parti, bat), Is.EqualTo(595.07f).Within(0.001f), "1 200 × 181/365");
    }

    [Test]
    public void Une_cession_future_attend_son_jour()
    {
        // Retour du 06/10 : cession datée après aujourd'hui → le cédant reste jusqu'à cette date.
        var loc = Bail(); loc.Name = "SARL Ancien";
        var aujourdhui = new DateTime(2026, 10, 6);

        Assert.That(Cessions.Enregistrer(loc, new DateTime(2026, 11, 1), "SARL Nouveau", "222", "1 rue Neuve", "n@x.fr", aujourdhui), Is.False);
        Assert.That(loc.Name, Is.EqualTo("SARL Ancien"), "rien ne change avant le jour de la cession");
        Assert.That(Cessions.Texte(loc), Is.EqualTo("cession prévue le 01/11/2026 à SARL Nouveau"));
        var relu = JsonUtility.FromJson<Locataire>(JsonUtility.ToJson(loc));
        Assert.That(Cessions.Prevue(relu)?.nom, Is.EqualTo("SARL Nouveau"), "la cession prévue est enregistrée");

        Assert.That(Cessions.Effectuer(loc, new DateTime(2026, 10, 31)), Is.False, "la veille : rien");
        Assert.That(Cessions.Effectuer(loc, new DateTime(2026, 11, 3)), Is.True, "lancement le jour même ou après");
        Assert.That(loc.Name, Is.EqualTo("SARL Nouveau"));
        Assert.That(loc.cessions[0].dateISO, Is.EqualTo("2026-11-01"), "datée du jour de la cession, pas du lancement");
        Assert.That(Cessions.Prevue(loc), Is.Null);
        Assert.That(Cessions.Prevue(JsonUtility.FromJson<Locataire>(JsonUtility.ToJson(loc))), Is.Null, "objet vide relu = pas de cession prévue");

        // Date du jour : tout de suite.
        Assert.That(Cessions.Enregistrer(loc, aujourdhui, "SARL Troisième", "", "", "", aujourdhui), Is.True);
        Assert.That(loc.Name, Is.EqualTo("SARL Troisième"));
    }

    [Test]
    public void La_regul_deduit_une_provision_par_periode_facturee()
    {
        Assert.That(Loyers.PeriodesFacturees(Bail(), 2026), Is.EqualTo(4));
        Assert.That(Loyers.PeriodesFacturees(Bail("2026-05-15"), 2026), Is.EqualTo(2), "T1 et T2 (partiel)");
        Assert.That(Loyers.PeriodesFacturees(Bail("2026-05-15"), 2027), Is.EqualTo(0));
        var arrive = Bail(); arrive.dateDebutBailISO = "2026-09-01";
        Assert.That(Loyers.PeriodesFacturees(arrive, 2026), Is.EqualTo(2), "T3 (partiel) et T4");
    }

    // ── Dernière facture ───────────────────────────────────────────────────────

    [Test]
    public void Un_loyer_emis_en_entier_avant_la_saisie_du_depart_est_a_corriger()
    {
        var loc = Bail("2026-05-15");
        // T2 dû : 3 000 × 45/91 = 1 483,52 HT.
        Assert.That(Loyers.MontantPeriode(loc, 2026, 2), Is.EqualTo(1483.52f).Within(0.001f));
        var plein = Emise("loyer-2026-P2", "Envoye", 3600f, 3000f);
        loc.facturesEtat.Add(plein);
        Assert.That(DepartLocataire.LoyerACorriger(loc, plein), Is.True);
        plein.loyerHT = 1483.52f;
        Assert.That(DepartLocataire.LoyerACorriger(loc, plein), Is.False, "déjà au prorata");

        // Ancienne ligne sans HT : seule une période entièrement après le départ est repérée.
        var t3 = Emise("loyer-2026-P3", "Envoye", 3600f);
        var t2 = Emise("loyer-2026-P2", "Envoye", 3600f);
        loc.facturesEtat = new List<FactureEtat> { t2, t3 };
        Assert.That(DepartLocataire.LoyerACorriger(loc, t3), Is.True);
        Assert.That(DepartLocataire.LoyerACorriger(loc, t2), Is.False);
        // Non émise : rien à corriger.
        Assert.That(DepartLocataire.LoyerACorriger(loc, new FactureEtat { key = "loyer-2026-P3", type = "Loyer" }), Is.False);
    }

    // ── Archivage ──────────────────────────────────────────────────────────────

    [Test]
    public void Archiver_est_propose_quand_tout_est_solde()
    {
        var loc = Bail("2026-05-15");   // parti (date passée)
        loc.facturesEtat.Add(Emise("loyer-2026-P1", "Paye", 3600f));
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.False, "dernier loyer (T2, au prorata) pas encore facturé");

        loc.facturesEtat.Add(Emise("loyer-2026-P2", "Impaye", 1780.22f));
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.False, "créance restante");

        loc.facturesEtat[1].statut = "Paye";
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.True, "sans dépôt ni provision");

        loc.depotDeGarantie = 3000f;
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.False, "dépôt pas encore rendu");
        loc.facturesEtat.Add(Emise(DepartLocataire.CleDecompte, "Paye", -716.48f));
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.True);

        loc.provisionPourCharges = true;
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.False, "régul de sortie 2026 à faire");
        loc.facturesEtat.Add(Emise("regul-2026", "Paye", 120f));
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.True);

        loc.archive = true;
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.False, "déjà archivé");
    }

    // ── Parcours de départ (bandeau, 02/10) ────────────────────────────────────

    static DepartLocataire.Statut St(Locataire loc, DepartLocataire.Etape e)
        => DepartLocataire.StatutDe(loc, e, DateTime.Today, out _);

    [Test]
    public void Sans_depart_seule_la_premiere_etape_est_a_faire()
    {
        var loc = Bail();
        Assert.That(St(loc, DepartLocataire.Etape.Depart), Is.EqualTo(DepartLocataire.Statut.AFaire));
        Assert.That(St(loc, DepartLocataire.Etape.Decompte), Is.EqualTo(DepartLocataire.Statut.EnAttente));
        Assert.That(DepartLocataire.Prochaine(loc, DateTime.Today), Is.EqualTo(DepartLocataire.Etape.Depart));
    }

    [Test]
    public void Les_etapes_du_depart_avancent_avec_la_fiche()
    {
        var loc = Bail("2026-05-15");   // parti
        loc.depotDeGarantie = 3000f;
        loc.provisionPourCharges = true;
        loc.dateRegularisationChargeISO = "2027-03-31";
        loc.facturesEtat.Add(Emise("loyer-2026-P1", "Paye", 3600f));

        Assert.That(St(loc, DepartLocataire.Etape.Depart), Is.EqualTo(DepartLocataire.Statut.Faite));
        Assert.That(St(loc, DepartLocataire.Etape.DernierLoyer), Is.EqualTo(DepartLocataire.Statut.AFaire), "T2 au prorata à facturer");
        Assert.That(DepartLocataire.Prochaine(loc, DateTime.Today), Is.EqualTo(DepartLocataire.Etape.DernierLoyer));
        Assert.That(DepartLocataire.LigneDernierLoyer(loc).key, Is.EqualTo("loyer-2026-P2"));
        loc.facturesEtat.Add(Emise("loyer-2026-P2", "Paye", 1780.22f, 1483.52f));
        Assert.That(St(loc, DepartLocataire.Etape.DernierLoyer), Is.EqualTo(DepartLocataire.Statut.Faite));

        // État des lieux : date seule ne suffit pas ; date + PDF, ou « pas d'état des lieux ».
        Assert.That(St(loc, DepartLocataire.Etape.EtatDesLieux), Is.EqualTo(DepartLocataire.Statut.AFaire));
        loc.dateEtatDesLieuxISO = "2026-05-15";
        Assert.That(St(loc, DepartLocataire.Etape.EtatDesLieux), Is.EqualTo(DepartLocataire.Statut.AFaire));
        loc.etatDesLieux = "EDL.pdf";
        Assert.That(St(loc, DepartLocataire.Etape.EtatDesLieux), Is.EqualTo(DepartLocataire.Statut.Faite));
        loc.dateEtatDesLieuxISO = ""; loc.etatDesLieux = ""; loc.sansEtatDesLieux = true;
        Assert.That(St(loc, DepartLocataire.Etape.EtatDesLieux), Is.EqualTo(DepartLocataire.Statut.Faite));

        Assert.That(St(loc, DepartLocataire.Etape.Decompte), Is.EqualTo(DepartLocataire.Statut.AFaire));
        Assert.That(DepartLocataire.PeutAnnuler(loc), Is.Null);
        loc.facturesEtat.Add(Emise(DepartLocataire.CleDecompte, "Paye", -2800f));
        Assert.That(St(loc, DepartLocataire.Etape.Decompte), Is.EqualTo(DepartLocataire.Statut.Faite));
        Assert.That(DepartLocataire.PeutAnnuler(loc), Is.Not.Null, "décompte émis : on n'annule plus");

        // Régul de sortie : à sa date habituelle, rien de faisable avant.
        Assert.That(DepartLocataire.StatutDe(loc, DepartLocataire.Etape.Regul, DateTime.Today, out var quand),
                    Is.EqualTo(DepartLocataire.Statut.EnAttente));
        Assert.That(quand, Is.EqualTo(new DateTime(2027, 3, 31)));
        Assert.That(DepartLocataire.Prochaine(loc, DateTime.Today), Is.Null);
        Assert.That(DepartLocataire.Consigne(loc, DateTime.Today), Does.Contain("31/03/2027"));
        Assert.That(St(loc, DepartLocataire.Etape.Archiver), Is.EqualTo(DepartLocataire.Statut.EnAttente));

        loc.facturesEtat.Add(Emise("regul-2026", "Paye", 120f));
        Assert.That(St(loc, DepartLocataire.Etape.Regul), Is.EqualTo(DepartLocataire.Statut.Faite));
        Assert.That(DepartLocataire.Prochaine(loc, DateTime.Today), Is.EqualTo(DepartLocataire.Etape.Archiver));
        loc.archive = true;
        Assert.That(St(loc, DepartLocataire.Etape.Archiver), Is.EqualTo(DepartLocataire.Statut.Faite));
    }

    [Test]
    public void Le_bandeau_dit_pourquoi_on_ne_peut_pas_encore_archiver()
    {
        // Retour du 05/10 : « mettre la raison, facture X pas payée par exemple ».
        var loc = Bail("2026-05-15");
        loc.depotDeGarantie = 3000f;
        loc.sansEtatDesLieux = true;   // les autres étapes faites : il ne reste que l'archivage
        loc.facturesEtat.Add(Emise("loyer-2026-P1", "Paye", 3600f));
        var t2 = Emise("loyer-2026-P2", "Impaye", 2140.22f, 1483.52f); t2.numero = "2026/04802"; t2.libelle = "Loyer 2e trimestre 2026";
        var dec = Emise(DepartLocataire.CleDecompte, "Envoye", -1259.78f); dec.numero = "2026/10003"; dec.libelle = "Décompte de sortie";
        loc.facturesEtat.Add(t2); loc.facturesEtat.Add(dec);

        var r = DepartLocataire.RaisonsNonArchivable(loc);
        string Eur(float v) => v.ToString("N2", FacturePdfService.FrCulture) + " €";   // séparateur de milliers insécable
        Assert.That(r, Has.Some.Contains($"facture n° 2026/04802 « Loyer 2e trimestre 2026 » non payée ({Eur(2140.22f)})"));
        Assert.That(r, Has.Some.Contains($"avoir n° 2026/10003 « Décompte de sortie » à rembourser ({Eur(1259.78f)})"));
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.False);
        Assert.That(DepartLocataire.Consigne(loc, DateTime.Today), Does.StartWith("Pas encore archivable : ").And.Contain("2026/04802"));

        t2.statut = "Paye"; dec.statut = "Paye";
        Assert.That(DepartLocataire.RaisonsNonArchivable(loc), Is.Empty);
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.True);
    }

    // ── Alertes ────────────────────────────────────────────────────────────────

    [Test]
    public void Le_depart_proche_est_signale_30_jours_avant()
    {
        var loc = Bail(Iso(DateTime.Today.AddDays(10)));
        Assert.That(A(loc, FacturationAlertes.AlerteType.Depart), Is.True);
        loc.dateSortieISO = Iso(DateTime.Today.AddDays(40));
        Assert.That(A(loc, FacturationAlertes.AlerteType.Depart), Is.False);
    }

    [Test]
    public void La_restitution_du_depot_est_rappelee_puis_urgente()
    {
        var t = DateTime.Today;
        var loc = Bail(Iso(t.AddMonths(-2).AddDays(10)));   // délai dans 10 jours
        loc.depotDeGarantie = 3000f;
        var a = FacturationAlertes.Pour(loc).Single(x => x.type == FacturationAlertes.AlerteType.Restitution);
        Assert.That(a.niveau, Is.EqualTo(FacturationAlertes.Niveau.Attention));

        loc.dateSortieISO = Iso(t.AddMonths(-2).AddDays(-5));   // délai dépassé
        a = FacturationAlertes.Pour(loc).Single(x => x.type == FacturationAlertes.AlerteType.Restitution);
        Assert.That(a.niveau, Is.EqualTo(FacturationAlertes.Niveau.Urgent));

        loc.facturesEtat.Add(Emise(DepartLocataire.CleDecompte, "Envoye", -500f));
        Assert.That(A(loc, FacturationAlertes.AlerteType.Restitution), Is.False, "décompte envoyé");
    }

    [Test]
    public void Plus_de_revision_du_depot_apres_le_depart_ni_d_alerte_une_fois_archive()
    {
        var t = DateTime.Today;
        var loc = Bail(Iso(t.AddDays(-10)));
        loc.depotDeGarantie = 3000f;
        loc.dateRevisionDepotISO = Iso(t.AddDays(-1));
        Assert.That(A(loc, FacturationAlertes.AlerteType.Depot), Is.False);
        loc.dateSortieISO = null;
        Assert.That(A(loc, FacturationAlertes.AlerteType.Depot), Is.True, "témoin : sans départ, l'alerte existe");

        loc.archive = true;
        Assert.That(FacturationAlertes.Pour(loc), Is.Empty);
    }
}
