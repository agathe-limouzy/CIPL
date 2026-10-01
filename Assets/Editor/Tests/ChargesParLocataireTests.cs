using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// L'état « facturée / payée » d'une charge est tenu PAR LOCATAIRE. Il était commun :
/// une charge partagée facturée à l'un sortait de la régularisation des autres, dont
/// la quote-part était perdue (constaté sur la taxe foncière 2025 de rivoli).
public class ChargesParLocataireTests
{
    static ChargeBatiment Charge(string id = "c1", string date = "2025-11-20")
        => new ChargeBatiment { id = id, nom = "Taxe foncière", cout = 13800, dateISO = date, tousLocataires = true };

    static Locataire Loc(string id, params FactureEtat[] lignes)
        => new Locataire { id = id, Name = id, provisionPourCharges = true, facturesEtat = new List<FactureEtat>(lignes) };

    static FactureEtat Ligne(string key, string statut) => new FactureEtat { key = key, statut = statut };

    [Test]
    public void Refacturer_a_un_locataire_ne_retire_la_charge_que_pour_lui()
    {
        var c = Charge();
        c.MarquerFacturee("A", "2026-09-29");

        Assert.That(c.AFacturerPour("A"), Is.False, "sortie de SA régularisation");
        Assert.That(c.AFacturerPour("B"), Is.True, "les autres gardent leur quote-part");
    }

    [Test]
    public void Une_charge_reglee_a_la_main_n_est_plus_proposee_a_personne()
    {
        var c = Charge();
        c.paye = true;

        Assert.That(c.AFacturerPour("A"), Is.False);
        Assert.That(c.AFacturerPour("B"), Is.False);
        Assert.That(c.EstPayee);
    }

    [Test]
    public void Le_paiement_d_une_regularisation_ne_touche_que_ce_locataire()
    {
        var c = Charge();
        var a = Loc("A", Ligne("regul-2025", "Envoye"));
        var b = Loc("B", Ligne("regul-2025", "Envoye"));
        var bat = new Batiment { charges = { c }, locataireDuBatiment = { a, b } };
        c.MarquerFacturee("A", "2026-01-31");
        c.MarquerFacturee("B", "2026-01-31");

        FacturationSuivi.SetStatut(a, a.facturesEtat[0], "Paye", bat);

        Assert.That(c.FacturationDe("A").paye, Is.True);
        Assert.That(c.FacturationDe("B").paye, Is.False);
        Assert.That(c.EstPayee, Is.False);
        Assert.That(c.Etat, Does.StartWith("En attente de paiement"));

        FacturationSuivi.SetStatut(b, b.facturesEtat[0], "Paye", bat);
        Assert.That(c.EstPayee);
    }

    [Test]
    public void Une_charge_refacturee_a_part_ne_suit_pas_le_paiement_de_la_regularisation()
    {
        var taxe = Charge("taxe");
        var toiture = Charge("toiture");
        var a = Loc("A", Ligne("regul-2025", "Envoye"), Ligne("refac-toiture", "Envoye"));
        var bat = new Batiment { charges = { taxe, toiture }, locataireDuBatiment = { a } };
        toiture.MarquerFacturee("A", "2025-12-01");   // refacturée seule
        taxe.MarquerFacturee("A", "2026-01-31");      // dans la régularisation

        FacturationSuivi.SetStatut(a, a.facturesEtat[0], "Paye", bat);

        Assert.That(taxe.FacturationDe("A").paye, Is.True);
        Assert.That(toiture.FacturationDe("A").paye, Is.False, "sa propre facture n'est pas payée");

        FacturationSuivi.SetStatut(a, a.facturesEtat[1], "Paye", bat);
        Assert.That(toiture.FacturationDe("A").paye, Is.True);
    }

    // ── Régularisation refaite ou corrigée ────────────────────────────────────

    static FactureEtat Emise(string key, string statut = "Envoye")
        => new FactureEtat { key = key, statut = statut, pdfPath = "Batiment/rivoli/A/Facture/f.pdf" };

    [Test]
    public void Une_regularisation_corrigee_reprend_les_charges_qu_elle_couvre()
    {
        var taxe = Charge("taxe");
        var toiture = Charge("toiture");
        var ancienne = Charge("ancienne", "2024-06-01");
        var a = Loc("A", Emise("regul-2025"), Emise("refac-toiture"));
        var bat = new Batiment { charges = { taxe, toiture, ancienne }, locataireDuBatiment = { a, Loc("B") } };
        taxe.MarquerFacturee("A", "2026-01-31");      // portée par la régul 2025
        toiture.MarquerFacturee("A", "2025-12-01");   // refacturée à part

        var liste = FactureRegulPanel.ChargesDeRegul(bat, a, 2025);

        Assert.That(liste, Is.EquivalentTo(new[] { taxe }), "ni la charge refacturée à part, ni une autre année");
        Assert.That(FactureRegulPanel.ChargesDeRegul(bat, bat.locataireDuBatiment[1], 2025),
                    Is.EquivalentTo(new[] { taxe, toiture }), "B garde ses quotes-parts");
    }

    [Test]
    public void Une_charge_refacturee_deduite_des_provisions_s_affiche_sur_la_regul()
    {
        // Demande du 01/10 : eau 800 € HT (960 TTC) refacturée et payée à part. Case
        // « déduite des provisions » cochée : la régul la montre et la déduit aussitôt
        // — le solde ne change pas. Décochée : hors des provisions, absente de la régul.
        var eau = Charge("eau");
        var taxe = Charge("taxe");
        var refac = Emise("refac-eau"); refac.montant = 960f;
        var a = Loc("A", refac);
        var bat = new Batiment { charges = { eau, taxe }, locataireDuBatiment = { a } };
        eau.MarquerFacturee("A", "2025-12-01");
        Assert.That(FactureRegulPanel.ChargesRefacturees(bat, a, 2025), Is.Empty, "case décochée : hors des provisions");

        eau.FacturationDe("A").deduitProvisions = true;
        var deja = FactureRegulPanel.ChargesRefacturees(bat, a, 2025);
        Assert.That(deja.Count, Is.EqualTo(1));
        Assert.That(deja[0].charge, Is.SameAs(eau));
        Assert.That(deja[0].ht, Is.EqualTo(800f), "le HT facturé");
        Assert.That(FactureRegulPanel.ChargesDeRegul(bat, a, 2025), Is.EquivalentTo(new[] { taxe }), "pas refacturée deux fois");
        Assert.That(FactureRegulPanel.ChargesRefacturees(bat, Loc("B"), 2025), Is.Empty, "rien refacturé à B");

        var html = FacturePdfService.BuildRegulHtml(new FacturePdfService.RegulData
            { totalCharges = 2000f, provisions = 3000f, dejaRefacture = 800f, soldeHT = -1800f });
        Assert.That(html, Does.Contain(FacturePdfService.LibelleDejaRefacture));
    }

    [Test]
    public void Plusieurs_charges_partent_sur_une_seule_refacturation()
    {
        // Demande du 01/10 : plusieurs charges sur UNE facture, une ligne de suivi chacune.
        var eau = Charge("eau");
        var taxe = Charge("taxe");
        var toiture = Charge("toiture");
        var a = Loc("A");
        Assert.That(FactureRefacPanel.Regroupement(a, new[] { eau, taxe }, out string cle), Is.Null, "facture neuve");
        Assert.That(cle, Is.EqualTo("refac-eau"));

        // Facture émise pour eau + taxe (même PDF) : sa correction garde ses deux charges.
        a.facturesEtat.Add(Emise("refac-eau"));
        a.facturesEtat.Add(Emise("refac-taxe"));
        Assert.That(FactureRefacPanel.Regroupement(a, new[] { eau }, out _), Does.Contain("couvre aussi"));
        Assert.That(FactureRefacPanel.Regroupement(a, new[] { toiture, eau, taxe }, out cle), Is.Null, "correction élargie");
        Assert.That(cle, Is.EqualTo("refac-eau"), "la facture existante porte la correction");

        // Deux factures existantes ne se mélangent pas.
        var autre = Emise("refac-toiture");
        autre.pdfPath = "Batiment/rivoli/A/Facture/g.pdf";
        a.facturesEtat.Add(autre);
        Assert.That(FactureRefacPanel.Regroupement(a, new[] { eau, taxe, toiture }, out _), Does.Contain("factures différentes"));

        // TTC par charge : la somme des lignes redonne le total de la facture.
        float total = 200.06f * 1.2f;
        var parts = FactureRefacPanel.PartsTtc(new List<float> { 100.03f, 100.03f }, total);
        Assert.That(parts[1], Is.EqualTo(120.04f));
        Assert.That(parts[0] + parts[1], Is.EqualTo(total).Within(0.001f));

        // Suivi : UNE ligne pour la facture, au total de ses charges (retour du 01/10).
        const string lib = "Refacturation : Jardin 2025 et Ménage 2025";
        var b = Loc("B",
            new FactureEtat { key = "refac-j", type = "Refac", libelle = lib, statut = "Envoye", montant = 240f, pdfPath = "p.pdf", echeanceISO = "2026-10-12" },
            new FactureEtat { key = "refac-m", type = "Refac", libelle = lib, statut = "Envoye", montant = 120f, pdfPath = "p.pdf", echeanceISO = "2026-10-12" });
        var refacs = FacturationSuivi.Lignes(b, 2026).Where(x => x.type == "Refac").ToList();
        Assert.That(refacs.Count, Is.EqualTo(1));
        Assert.That(refacs[0].montant, Is.EqualTo(360f));
        Assert.That(b.facturesEtat[0].montant, Is.EqualTo(240f), "le stockage par charge n'est pas touché");

        Assert.That(FactureRefacPanel.LibelleFacture(new[] { "Jardin 2025", "Ménage 2025" }, false), Is.EqualTo(lib));
        Assert.That(FactureRefacPanel.LibelleFacture(new[] { "Entretien des espaces verts 2025", "Nettoyage des vitres 2025", "Ménage 2025" }, false),
                    Is.EqualTo("Refacturation : 3 charges"), "trop long pour la colonne du suivi");
        Assert.That(FactureRefacPanel.LibelleFacture(new[] { "Avoir eau 2025" }, true), Is.EqualTo("Avoir : Avoir eau 2025"));
    }

    [Test]
    public void Un_avoir_sur_une_charge_se_presente_comme_un_avoir()
    {
        // Demande du 01/10 : charge négative (avoir fournisseur) refacturée au locataire.
        var c = Charge("avoir");
        c.cout = -500f;
        c.ratios = new List<ChargeRatio> { new ChargeRatio("A", 1f), new ChargeRatio("B", 3f) };
        Assert.That(ListesCharges.QuotePart(c, Loc("A"), null), Is.EqualTo(-125f), "quote-part négative");

        // PDF : titre AVOIR, ligne négative imprimée, phrase de remboursement.
        var html = FacturePdfService.BuildHtml(new FacturePdfService.Data
        {
            ligneLabel = "Eau", totalPeriode = 800f,
            lignesProvision = new List<KeyValuePair<string, float>> { new KeyValuePair<string, float>("Avoir eau", -1000f) },
            totalHT = -200f, tva = -40f, ttc = -240f,
        });
        Assert.That(html, Does.Contain("AVOIR :"));
        Assert.That(html, Does.Contain("Avoir eau"), "une ligne négative ne doit pas disparaître du tableau");
        Assert.That(FacturePdfService.BuildHtml(new FacturePdfService.Data { ttc = 240f }), Does.Contain("FACTURE :"));
        Assert.That(FactureEmission.PhraseSomme("SOMME À NOUS RÉGLER LE 1 mai 2026", -200f),
                    Is.EqualTo("SOMME QUI VOUS SERA REMBOURSÉE"));

        // Suivi : un avoir envoyé ne devient jamais « Impayé » (c'est nous qui devons).
        var ligne = new FactureEtat { key = "refac-avoir", type = "Refac", statut = "Envoye", montant = -240f,
                                      pdfPath = "f.pdf", echeanceISO = "2020-01-01" };
        Assert.That(FacturationSuivi.EtatDe(ligne), Is.EqualTo(FacturationSuivi.Etat.Envoye));
        ligne.montant = 240f;
        Assert.That(FacturationSuivi.EtatDe(ligne), Is.EqualTo(FacturationSuivi.Etat.Impaye));
    }

    [Test]
    public void Un_avoir_cite_sa_facture_d_origine_et_la_regul_qui_rembourse_est_un_avoir()
    {
        // Demande du 01/10 : régul négative intitulée AVOIR ; « Avoir sur la facture n° ».
        var avoir = FacturePdfService.BuildHtml(new FacturePdfService.Data { ttc = -600f, factureOrigine = "2026/09001" });
        Assert.That(avoir, Does.Contain("Avoir sur la facture n° 2026/09001"));
        var facture = FacturePdfService.BuildHtml(new FacturePdfService.Data { ttc = 600f, factureOrigine = "2026/09001" });
        Assert.That(facture, Does.Not.Contain("Avoir sur la facture"), "jamais sur une facture");

        var regul = FacturePdfService.BuildRegulHtml(new FacturePdfService.RegulData { soldeHT = -1800f, factureOrigine = "2026/01005" });
        Assert.That(regul, Does.Contain("AVOIR :"));
        Assert.That(regul, Does.Contain("Avoir sur la facture n° 2026/01005"));
        Assert.That(FacturePdfService.BuildRegulHtml(new FacturePdfService.RegulData { soldeHT = 200f }), Does.Contain("FACTURE :"));

        // Factures proposées : une par numéro (régul regroupée = 2 lignes), sans les avoirs.
        var a = Loc("A",
            new FactureEtat { key = "regul-2025", numero = "2026/01005", statut = "Paye", montant = 300f, pdfPath = "r.pdf", libelle = "Régul 2025", dateEnvoiISO = "2026-01-31" },
            new FactureEtat { key = "regul-2025-TF", numero = "2026/01005", statut = "Paye", montant = 100f, pdfPath = "r.pdf", libelle = "Régul TF 2025", dateEnvoiISO = "2026-01-31" },
            new FactureEtat { key = "refac-x", numero = "2026/09001", statut = "Envoye", montant = 960f, pdfPath = "x.pdf", libelle = "Refacturation : Eau", dateEnvoiISO = "2026-09-01" },
            new FactureEtat { key = "refac-y", numero = "2026/09002", statut = "Envoye", montant = -600f, pdfPath = "y.pdf", libelle = "Avoir : Eau", dateEnvoiISO = "2026-09-02" });
        FacturationSuivi.FacturesOrigine(a, out var labels, out var ids);
        Assert.That(ids, Is.EqualTo(new[] { "", "2026/09001", "2026/01005" }), "aucune, puis la plus récente d'abord");
        Assert.That(labels[2], Does.Contain("(+1)"), "la régul regroupée apparaît une fois");
    }

    [Test]
    public void Une_regularisation_preparee_puis_refaite_garde_ses_charges()
    {
        var taxe = Charge();
        var a = Loc("A", Emise("regul-2025", "AttenteEnvoi"));
        var bat = new Batiment { charges = { taxe }, locataireDuBatiment = { a } };
        taxe.MarquerFacturee("A", "2026-01-31");

        Assert.That(FactureRegulPanel.ChargesDeRegul(bat, a, 2025), Is.EquivalentTo(new[] { taxe }));
    }

    [Test]
    public void Une_charge_ajoutee_apres_coup_entre_dans_la_correction()
    {
        var taxe = Charge("taxe");
        var nouvelle = Charge("nouvelle", "2025-12-15");
        var a = Loc("A", Emise("regul-2025"));
        var bat = new Batiment { charges = { taxe, nouvelle }, locataireDuBatiment = { a } };
        taxe.MarquerFacturee("A", "2026-01-31");

        Assert.That(FactureRegulPanel.ChargesDeRegul(bat, a, 2025), Is.EquivalentTo(new[] { taxe, nouvelle }));
    }

    [Test]
    public void Sans_regularisation_emise_une_charge_deja_facturee_n_y_revient_pas()
    {
        var taxe = Charge();
        var a = Loc("A");   // aucune régul 2025 émise
        var bat = new Batiment { charges = { taxe }, locataireDuBatiment = { a } };
        taxe.MarquerFacturee("A", "2026-01-31");

        Assert.That(FactureRegulPanel.ChargesDeRegul(bat, a, 2025), Is.Empty);
    }

    // ── Reconstitution de l'ancien format ─────────────────────────────────────

    [Test]
    public void La_taxe_regularisee_pour_Sephora_redevient_due_par_les_autres()
    {
        var c = Charge();
        c.factureeISO = "2026-09-21";
        c.paye = true;   // venait du paiement de la régularisation de Sephora
        var bat = new Batiment
        {
            charges = { c },
            locataireDuBatiment = { Loc("Sephora", Ligne("regul-2025", "Paye")), Loc("Starbucks"), Loc("Orange") },
        };

        Assert.That(ChargeBatiment.Reconstituer(bat), Is.True);

        Assert.That(c.FacturationDe("Sephora")?.paye, Is.True);
        Assert.That(c.FacturationDe("Sephora").dateISO, Is.EqualTo("2026-09-21"));
        Assert.That(c.AFacturerPour("Starbucks"), Is.True);
        Assert.That(c.AFacturerPour("Orange"), Is.True);
        Assert.That(c.paye, Is.False);
        Assert.That(c.factureeISO, Is.Null);
        Assert.That(ChargeBatiment.Reconstituer(bat), Is.False, "une seule fois");
    }

    [Test]
    public void Une_refacturation_ancienne_est_attribuee_a_son_locataire()
    {
        var c = new ChargeBatiment { id = "c9", dateISO = "2026-03-01", tousLocataires = false,
                                     locatairesConcernes = { "A", "B" }, factureeISO = "2026-03-10" };
        var bat = new Batiment
        {
            charges = { c },
            locataireDuBatiment = { Loc("A", Ligne("refac-c9", "AttenteEnvoi")), Loc("B") },
        };

        ChargeBatiment.Reconstituer(bat);

        Assert.That(c.EstFactureePour("A") && !c.FacturationDe("A").paye);
        Assert.That(c.AFacturerPour("B"));
    }

    [Test]
    public void Sans_ligne_de_suivi_la_charge_reste_reglee_pour_tous()
    {
        var c = Charge();
        c.factureeISO = "2026-09-21";
        var bat = new Batiment { charges = { c }, locataireDuBatiment = { Loc("A"), Loc("B") } };

        ChargeBatiment.Reconstituer(bat);

        Assert.That(c.AFacturerPour("A") || c.AFacturerPour("B"), Is.False, "rien n'est refacturé au hasard");
        Assert.That(c.factureeISO, Is.Null);
    }

    [Test]
    public void L_etat_par_locataire_survit_au_json()
    {
        var c = Charge();
        c.MarquerFacturee("A", "2026-09-29");
        c.MarquerPayee("A", true);

        var relu = JsonUtility.FromJson<ChargeBatiment>(JsonUtility.ToJson(c));

        Assert.That(relu.FacturationDe("A")?.paye, Is.True);
        Assert.That(relu.AFacturerPour("B"));
    }
}
