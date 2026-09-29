using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// Mention TVA : le choix (aucune / débits / encaissements) et ses deux niveaux de
/// texte — la base dans les Réglages, le remplacement éventuel sur la facture.
///
/// Ce qui doit tenir : une facture enregistrée avant le menu sort comme avant, une
/// phrase de base modifiée dans les Réglages atteint toutes les factures qui ne
/// l'ont pas remplacée, et un vrai remplacement n'est jamais écrasé par la base.
public class MentionTvaTests
{
    static ReglageData Reglages(string debits = null, string encaissements = null)
    {
        var r = new ReglageData();
        if (debits != null) r.mentionTvaDebits = debits;
        if (encaissements != null) r.mentionTvaEncaissements = encaissements;
        return r;
    }

    // ── Le choix ────────────────────────────────────────────────────────────

    [Test]
    public void Une_facture_d_avant_le_menu_garde_sa_mention()
    {
        // JSON écrit par l'ancien panneau : pas de clé `tvaEncaissements`.
        var cochee = JsonUtility.FromJson<FactureInfo>("{\"tvaDebit\":true,\"texteTvaDebit\":\"la TVA est payée sur les débits\"}");
        var decochee = JsonUtility.FromJson<FactureInfo>("{\"tvaDebit\":false}");

        Assert.That(MentionTva.Lire(cochee), Is.EqualTo(MentionTva.Debits));
        Assert.That(MentionTva.Lire(decochee), Is.EqualTo(MentionTva.Aucune));
        Assert.That(MentionTva.Lire(null), Is.EqualTo(MentionTva.Debits), "facture jamais réglée : valeur d'usine");
    }

    [Test]
    public void Les_trois_choix_survivent_au_JSON()
    {
        foreach (int choix in new[] { MentionTva.Aucune, MentionTva.Debits, MentionTva.Encaissements })
        {
            var f = new FactureInfo();
            MentionTva.Ecrire(f, choix);
            var relu = JsonUtility.FromJson<FactureInfo>(JsonUtility.ToJson(f));
            Assert.That(MentionTva.Lire(relu), Is.EqualTo(choix), MentionTva.Libelles[choix]);
        }
    }

    // ── La phrase de base (Réglages) ────────────────────────────────────────

    [Test]
    public void Des_reglages_d_avant_ont_les_phrases_d_usine()
    {
        var r = JsonUtility.FromJson<ReglageData>("{\"phraseRetard\":\"x\"}");

        Assert.That(MentionTva.Base(r, MentionTva.Debits), Is.EqualTo(FacturePdfService.TvaDebitDefaut));
        Assert.That(MentionTva.Base(r, MentionTva.Encaissements), Is.EqualTo(FacturePdfService.TvaEncaissementsDefaut));
    }

    [Test]
    public void Une_base_videe_retombe_sur_le_texte_d_usine()
    {
        var r = Reglages(debits: "  ", encaissements: "");

        Assert.That(MentionTva.Base(r, MentionTva.Debits), Is.EqualTo(FacturePdfService.TvaDebitDefaut));
        Assert.That(MentionTva.Base(r, MentionTva.Encaissements), Is.EqualTo(FacturePdfService.TvaEncaissementsDefaut));
    }

    // ── Base ou remplacement ────────────────────────────────────────────────

    [Test]
    public void Sans_remplacement_la_facture_suit_les_reglages()
    {
        var r = Reglages(debits: "Option pour le paiement de la taxe d'après les débits");
        var f = new FactureInfo { tvaDebit = true, texteTvaDebit = "" };

        Assert.That(MentionTva.PhraseFacture(r, f), Is.EqualTo("Option pour le paiement de la taxe d'après les débits"));
    }

    [Test]
    public void Le_texte_d_usine_enregistre_par_l_ancien_panneau_n_est_pas_un_remplacement()
    {
        // L'ancien panneau enregistrait la phrase pré-remplie telle quelle : toutes les
        // factures existantes portent le texte d'usine. Elles doivent suivre la base.
        var r = Reglages(debits: "Option pour le paiement de la taxe d'après les débits");
        var f = new FactureInfo { tvaDebit = true, texteTvaDebit = FacturePdfService.TvaDebitDefaut };

        Assert.That(MentionTva.PhraseFacture(r, f), Is.EqualTo("Option pour le paiement de la taxe d'après les débits"));
    }

    [Test]
    public void Un_vrai_remplacement_l_emporte_sur_la_base()
    {
        var r = Reglages(debits: "Option pour le paiement de la taxe d'après les débits");
        var f = new FactureInfo { tvaDebit = true, texteTvaDebit = "TVA acquittée sur les débits" };

        Assert.That(MentionTva.PhraseFacture(r, f), Is.EqualTo("TVA acquittée sur les débits"));
    }

    [Test]
    public void Une_phrase_egale_a_la_base_n_est_pas_gardee_comme_remplacement()
    {
        // Sinon, la retoucher ensuite dans les Réglages ne toucherait plus cette facture.
        var r = Reglages(encaissements: "TVA sur les encaissements");

        Assert.That(MentionTva.Surcharge(r, MentionTva.Encaissements, "TVA sur les encaissements"), Is.False);
        Assert.That(MentionTva.Surcharge(r, MentionTva.Encaissements, ""), Is.False);
        Assert.That(MentionTva.Surcharge(r, MentionTva.Encaissements, "Autre phrase"), Is.True);
    }

    [Test]
    public void Les_encaissements_prennent_leur_propre_base()
    {
        var r = Reglages();
        var f = new FactureInfo();
        MentionTva.Ecrire(f, MentionTva.Encaissements);

        Assert.That(MentionTva.PhraseFacture(r, f), Is.EqualTo(FacturePdfService.TvaEncaissementsDefaut));
    }

    // ── Héritage ────────────────────────────────────────────────────────────

    [Test]
    public void Un_nouveau_locataire_herite_du_type_de_mention()
    {
        var precedent = new Locataire { Name = "Premier", creationISO = "2026-01-01 10:00:00" };
        precedent.factureLoyer = new FactureInfo();
        MentionTva.Ecrire(precedent.factureLoyer, MentionTva.Encaissements);

        var neuf = new Locataire { Name = "Second" };
        HeritageFacture.Appliquer(neuf, new List<Locataire> { precedent });

        Assert.That(MentionTva.Lire(neuf.factureLoyer), Is.EqualTo(MentionTva.Encaissements));
    }
}
