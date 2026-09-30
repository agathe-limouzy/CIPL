using System.Collections.Generic;
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
