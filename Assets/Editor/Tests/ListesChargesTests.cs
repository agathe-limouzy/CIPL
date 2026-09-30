using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// Listes de charges : la générale (comme avant) et des listes spécifiques des
/// Réglages, chacune régularisée à sa date avec sa propre provision.
public class ListesChargesTests
{
    List<ListeCharges> _avant;
    string _nomGeneraleAvant;

    // Les listes vivent dans les réglages de l'entreprise active : on les remplace en
    // mémoire le temps du test, rien n'est enregistré.
    [SetUp] public void Garder()
    {
        _avant = ReglageService.Current.listesCharges;
        _nomGeneraleAvant = ReglageService.Current.nomListeGenerale;
        ReglageService.Current.listesCharges = new List<ListeCharges> { new ListeCharges { id = "TF", nom = "Taxe foncière" } };
        ReglageService.Current.nomListeGenerale = ListesCharges.NomGeneraleDefaut;
    }
    [TearDown] public void Rendre()
    {
        ReglageService.Current.listesCharges = _avant;
        ReglageService.Current.nomListeGenerale = _nomGeneraleAvant;
    }

    [Test]
    public void La_liste_generale_se_renomme_partout()
    {
        ReglageService.Current.nomListeGenerale = "Charges communes";

        Assert.That(ListesCharges.Nom(""), Is.EqualTo("Charges communes"));
        Assert.That(ListesCharges.LibelleProvision(""), Is.EqualTo("Provision pour charges — Charges communes"));
        Assert.That(ListesCharges.Libelle(new List<string> { "", "TF" }, 2025), Does.Contain("Charges communes, Taxe foncière"));
        Assert.That(ListesCharges.Nom("SUPPRIMEE"), Is.EqualTo("Charges communes"), "liste supprimée : retombe sur la générale");

        ReglageService.Current.nomListeGenerale = "  ";
        Assert.That(ListesCharges.NomGenerale, Is.EqualTo(ListesCharges.NomGeneraleDefaut), "vide = nom d'usine");
    }

    [Test]
    public void Des_reglages_d_avant_ont_le_nom_d_usine()
        => Assert.That(UnityEngine.JsonUtility.FromJson<ReglageData>("{\"phraseRetard\":\"x\"}").nomListeGenerale,
                       Is.EqualTo(ListesCharges.NomGeneraleDefaut));

    static Locataire Loc(string dateTF = "2026-11-15", float provTF = 50f) => new Locataire
    {
        id = "A", Name = "A", provisionPourCharges = true, provisionPourChargeValue = 100f,
        dateRegularisationChargeISO = "2026-01-31", periodiciteLoyer = Periodicite.mensuel,
        regulListes = { new RegulListe { listeId = "TF", dateISO = dateTF, provision = provTF } },
    };

    static ChargeBatiment Charge(string id, string listeId)
        => new ChargeBatiment { id = id, nom = id, cout = 1200, dateISO = "2025-06-01", tousLocataires = true, listeId = listeId };

    [Test]
    public void Les_cles_de_suivi_restent_lisibles()
    {
        Assert.That(ListesCharges.Cle("", 2025), Is.EqualTo("regul-2025"), "générale : inchangée");
        Assert.That(ListesCharges.Cle("TF", 2025), Is.EqualTo("regul-2025-TF"));

        Assert.That(ListesCharges.LireCle("regul-2025-TF", out int a, out string l) && a == 2025 && l == "TF");
        Assert.That(ListesCharges.LireCle("regul-2025", out a, out l) && a == 2025 && l == "");
        Assert.That(FactureRegulPanel.AnneeDeCle("regul-2025-TF"), Is.EqualTo(2025));
        Assert.That(ListesCharges.LireCle("loyer-2026-P3", out _, out _), Is.False);
    }

    [Test]
    public void Une_ligne_de_regularisation_par_liste_datee()
    {
        var lignes = FacturationSuivi.Lignes(Loc(), 2025).Where(x => x.type == "Regul").ToList();

        Assert.That(lignes.Select(x => x.key), Is.EquivalentTo(new[] { "regul-2025", "regul-2025-TF" }));
        var tf = lignes.Single(x => x.key == "regul-2025-TF");
        Assert.That(tf.echeanceISO, Is.EqualTo("2026-11-15"), "année suivante, à la date de la liste");
        Assert.That(tf.libelle, Does.Contain("Taxe foncière"));
        Assert.That(lignes.Single(x => x.key == "regul-2025").libelle, Is.EqualTo("Régularisation des charges 2025"));
    }

    [Test]
    public void Sans_date_une_liste_n_a_pas_de_regularisation()
    {
        var lignes = FacturationSuivi.Lignes(Loc(dateTF: ""), 2025).Where(x => x.type == "Regul");
        Assert.That(lignes.Select(x => x.key), Is.EquivalentTo(new[] { "regul-2025" }));
    }

    [Test]
    public void Chaque_regularisation_ne_prend_que_les_charges_de_sa_liste()
    {
        var loc = Loc();
        var gen = Charge("entretien", "");
        var tf = Charge("taxe", "TF");
        var orpheline = Charge("ancienne-liste", "SUPPRIMEE");
        var bat = new Batiment { charges = { gen, tf, orpheline }, locataireDuBatiment = { loc } };

        Assert.That(FactureRegulPanel.ChargesDeRegul(bat, loc, 2025, ""), Is.EquivalentTo(new[] { gen, orpheline }),
                    "une charge d'une liste supprimée retombe dans la générale");
        Assert.That(FactureRegulPanel.ChargesDeRegul(bat, loc, 2025, "TF"), Is.EquivalentTo(new[] { tf }));
    }

    [Test]
    public void La_provision_se_compte_par_liste_et_au_total()
    {
        var loc = Loc();
        Assert.That(ListesCharges.Provision(loc, ""), Is.EqualTo(100f));
        Assert.That(ListesCharges.Provision(loc, "TF"), Is.EqualTo(50f));
        Assert.That(ListesCharges.ProvisionTotale(loc), Is.EqualTo(150f), "appelée avec le loyer");

        loc.provisionPourCharges = false;
        Assert.That(ListesCharges.ProvisionTotale(loc), Is.Zero);
    }

    // ── Locataire limité à une liste ──────────────────────────────────────────

    [Test]
    public void Un_locataire_limite_a_une_liste_n_a_que_celle_ci()
    {
        var loc = Loc();
        loc.listesConcernees = new List<string> { "TF" };

        Assert.That(ListesCharges.ConcerneParListe(loc, "TF"));
        Assert.That(ListesCharges.ConcerneParListe(loc, ""), Is.False);
        Assert.That(ListesCharges.DuLocataire(loc), Is.EqualTo(new[] { "TF" }));
        Assert.That(ListesCharges.ProvisionTotale(loc), Is.EqualTo(50f), "sa provision générale ne compte plus");
        Assert.That(FacturationSuivi.Lignes(loc, 2025).Where(x => x.type == "Regul").Select(x => x.key),
                    Is.EquivalentTo(new[] { "regul-2025-TF" }));
    }

    [Test]
    public void Un_locataire_peut_relever_de_deux_listes_sur_trois()
    {
        ReglageService.Current.listesCharges.Add(new ListeCharges { id = "ASC", nom = "Ascenseur" });
        var loc = Loc();
        loc.regulListes.Add(new RegulListe { listeId = "ASC", dateISO = "2026-03-01", provision = 30 });
        loc.listesConcernees = new List<string> { "", "TF" };   // générale + taxe foncière, pas l'ascenseur

        Assert.That(ListesCharges.ConcerneParListe(loc, ""));
        Assert.That(ListesCharges.ConcerneParListe(loc, "TF"));
        Assert.That(ListesCharges.ConcerneParListe(loc, "ASC"), Is.False);
        Assert.That(ListesCharges.DuLocataire(loc), Is.EqualTo(new[] { "", "TF" }));
        Assert.That(ListesCharges.ProvisionTotale(loc), Is.EqualTo(150f), "100 + 50, l'ascenseur ne compte pas");
    }

    // ── Retirer une liste à un locataire qui y a des charges ─────────────────

    [Test]
    public void Une_charge_non_payee_de_la_liste_bloque_son_retrait()
    {
        var loc = Loc();
        var taxe = Charge("taxe", "TF");
        var entretien = Charge("entretien", "");
        var bat = new Batiment { charges = { taxe, entretien }, locataireDuBatiment = { loc } };

        Assert.That(ListesCharges.ChargesDeListe(bat, loc, "TF", true), Is.EqualTo(new[] { taxe }), "jamais régularisée");

        taxe.MarquerFacturee("A", "2026-11-15");
        Assert.That(ListesCharges.ChargesDeListe(bat, loc, "TF", true), Is.EqualTo(new[] { taxe }), "régularisée, pas encore payée");

        taxe.MarquerPayee("A", true);
        Assert.That(ListesCharges.ChargesDeListe(bat, loc, "TF", true), Is.Empty, "payée : plus de blocage…");
        Assert.That(ListesCharges.ChargesDeListe(bat, loc, "TF", false), Is.EqualTo(new[] { taxe }), "…mais un avertissement");
    }

    [Test]
    public void Seules_les_charges_qui_le_concernent_vraiment_comptent()
    {
        var loc = Loc();
        loc.dateDebutBailISO = "2025-01-01";
        var avantBail = Charge("taxe-2024", "TF");
        avantBail.dateISO = "2024-11-20";
        var pasDesigne = Charge("toiture", "TF");
        pasDesigne.tousLocataires = false;
        pasDesigne.locatairesConcernes.Add("B");
        var reglee = Charge("reglee", "TF");
        reglee.paye = true;   // réglée pour tous
        var bat = new Batiment { charges = { avantBail, pasDesigne, reglee }, locataireDuBatiment = { loc } };

        Assert.That(ListesCharges.ChargesDeListe(bat, loc, "TF", true), Is.Empty);
        Assert.That(ListesCharges.ChargesDeListe(bat, loc, "TF", false), Is.EqualTo(new[] { reglee }));
    }

    // ── Supprimer une liste dans les Réglages ────────────────────────────────

    [Test]
    public void Une_liste_ne_se_supprime_pas_tant_qu_un_locataire_y_doit_une_charge()
    {
        var a = Loc();
        var sansProvision = new Locataire { id = "B", Name = "B" };
        var taxe = Charge("taxe", "TF");
        var bat = new Batiment { Name = "rivoli", charges = { taxe }, locataireDuBatiment = { a, sansProvision } };

        var dus = ListesCharges.Debiteurs(new[] { bat }, "TF");
        Assert.That(dus.Select(d => d.loc), Is.EqualTo(new[] { a }), "B, sans provision, ne bloque pas");

        taxe.MarquerFacturee("A", "2026-11-15");
        taxe.MarquerPayee("A", true);
        Assert.That(ListesCharges.Debiteurs(new[] { bat }, "TF"), Is.Empty, "payée : suppression possible");
    }

    [Test]
    public void Supprimer_une_liste_bascule_ses_charges_dans_la_generale_sans_les_perdre()
    {
        var a = Loc();
        a.listesConcernees = new List<string> { "TF" };
        var taxe = Charge("taxe", "TF");
        taxe.MarquerFacturee("A", "2026-11-15");
        taxe.MarquerPayee("A", true);
        var entretien = Charge("entretien", "");
        var bat = new Batiment { charges = { taxe, entretien }, locataireDuBatiment = { a } };

        int n = ListesCharges.BasculerVersGenerale(bat, "TF");

        Assert.That(n, Is.EqualTo(1));
        Assert.That(bat.charges, Is.EquivalentTo(new[] { taxe, entretien }), "rien ne disparaît de l'historique");
        Assert.That(taxe.listeId, Is.EqualTo(""));
        Assert.That(taxe.FacturationDe("A").paye, Is.True, "son état par locataire est conservé");
        Assert.That(a.regulListes.Exists(r => r.listeId == "TF"), Is.False, "date et provision de la liste retirées");
        Assert.That(a.listesConcernees, Is.EqualTo(new[] { "" }), "limité à la liste supprimée → à la générale");
    }

    [Test]
    public void Une_charge_d_une_autre_liste_ne_le_concerne_pas_meme_pour_tous()
    {
        var loc = Loc();
        loc.listesConcernees = new List<string> { "TF" };
        var entretien = Charge("entretien", "");   // « tous les locataires », liste générale
        var bat = new Batiment { charges = { entretien }, locataireDuBatiment = { loc } };

        Assert.That(ListesCharges.Concerne(entretien, loc), Is.False);
        Assert.That(FactureRegulPanel.ChargesDeRegul(bat, loc, 2025, ""), Is.Empty);
    }

    [Test]
    public void La_quote_part_se_repartit_entre_les_seuls_locataires_concernes()
    {
        var a = Loc();
        var b = new Locataire { id = "B", Name = "B", provisionPourCharges = true,
                                listesConcernees = new List<string> { "" } };   // générale seulement
        var taxe = Charge("taxe", "TF");
        taxe.ratios.Add(new ChargeRatio("A", 100));
        taxe.ratios.Add(new ChargeRatio("B", 100));   // réparti avant que B soit limité
        var bat = new Batiment { charges = { taxe }, locataireDuBatiment = { a, b } };

        Assert.That(ListesCharges.QuotePart(taxe, a, bat), Is.EqualTo(1200f), "B ne compte plus : A porte tout");
        Assert.That(ListesCharges.QuotePart(taxe, b, bat), Is.Zero);
    }

    [Test]
    public void Sans_provision_les_listes_ne_s_appliquent_pas()
    {
        var loc = Loc();
        loc.listesConcernees = new List<string> { "TF" };
        loc.provisionPourCharges = false;   // choix masqué dans « Gestion du loyer »

        Assert.That(ListesCharges.ConcerneParListe(loc, ""), "il relève de toutes les charges, comme avant les listes");
        Assert.That(ListesCharges.Concerne(Charge("entretien", ""), loc));
    }

    [Test]
    public void Une_restriction_vers_une_liste_supprimee_retombe_sur_la_generale()
    {
        var loc = Loc();
        loc.listesConcernees = new List<string> { "SUPPRIMEE" };

        Assert.That(ListesCharges.ConcerneParListe(loc, ""), Is.True);
        Assert.That(ListesCharges.ConcerneParListe(loc, "TF"), Is.False);
    }

    // ── Facture de loyer : une ligne par provision ────────────────────────────

    [Test]
    public void La_facture_de_loyer_imprime_chaque_provision_sous_son_nom()
    {
        var d = new FacturePdfService.Data
        {
            totalPeriode = 1000, provision = 150,
            lignesProvision = new List<KeyValuePair<string, float>>
            {
                new KeyValuePair<string, float>(ListesCharges.LibelleProvision(""), 100),
                new KeyValuePair<string, float>(ListesCharges.LibelleProvision("TF"), 50),
            },
        };
        string html = FacturePdfService.BuildHtml(d);

        Assert.That(html, Does.Contain("Provision pour charges — Charges générales"));
        Assert.That(html, Does.Contain("Provision pour charges — Taxe foncière"));
    }

    [Test]
    public void Sans_liste_specifique_la_ligne_de_provision_ne_change_pas()
    {
        ReglageService.Current.listesCharges = new List<ListeCharges>();
        string html = FacturePdfService.BuildHtml(new FacturePdfService.Data { totalPeriode = 1000, provision = 100 });

        Assert.That(ListesCharges.LibelleProvision(""), Is.EqualTo("Provision pour charges"));
        Assert.That(html, Does.Contain("<td>Provision pour charges</td>"));
    }

    // ── Régularisation regroupée ──────────────────────────────────────────────

    [Test]
    public void Le_libelle_d_une_regularisation_regroupee_nomme_ses_listes()
    {
        Assert.That(ListesCharges.Libelle(new List<string> { "" }, 2025), Is.EqualTo("Régularisation des charges 2025"));
        Assert.That(ListesCharges.Libelle(new List<string> { "", "TF" }, 2025),
                    Is.EqualTo("Régularisation des charges 2025 — Charges générales, Taxe foncière"));
    }

    [Test]
    public void Les_lignes_d_une_meme_facture_sont_payees_ensemble()
    {
        var loc = Loc();
        string pdf = "Batiment/rivoli/A/Facture/Regul-2025.pdf";
        loc.facturesEtat.Add(new FactureEtat { key = "regul-2025", type = "Regul", statut = "Envoye", pdfPath = pdf, montant = 300 });
        loc.facturesEtat.Add(new FactureEtat { key = "regul-2025-TF", type = "Regul", statut = "Envoye", pdfPath = pdf, montant = 120 });
        var gen = Charge("entretien", "");
        var tf = Charge("taxe", "TF");
        gen.MarquerFacturee("A", "2026-01-31");
        tf.MarquerFacturee("A", "2026-01-31");
        var bat = new Batiment { charges = { gen, tf }, locataireDuBatiment = { loc } };

        Assert.That(FacturationSuivi.MemeFacture(loc, "regul-2025-TF").Count, Is.EqualTo(2));

        FacturationSuivi.SetStatut(loc, loc.facturesEtat[1], "Paye", bat);

        Assert.That(loc.facturesEtat.All(x => x.statut == "Paye"), "une seule facture, un seul paiement");
        Assert.That(gen.FacturationDe("A").paye && tf.FacturationDe("A").paye);
    }

    // ── Garde-fous du regroupement ────────────────────────────────────────────

    static FactureEtat Emise(string key, string pdf)
        => new FactureEtat { key = key, type = "Regul", statut = "Envoye", pdfPath = pdf, libelle = key };

    [Test]
    public void Des_listes_sans_facture_forment_une_facture_neuve()
    {
        Assert.That(FactureRegulPanel.Regroupement(Loc(), new List<string> { "", "TF" }, 2025, out string cle), Is.Null);
        Assert.That(cle, Is.EqualTo("regul-2025"));
    }

    [Test]
    public void Ajouter_une_liste_a_une_facture_existante_la_corrige()
    {
        var loc = Loc();
        loc.facturesEtat.Add(Emise("regul-2025-TF", "tf.pdf"));   // taxe foncière déjà facturée seule

        Assert.That(FactureRegulPanel.Regroupement(loc, new List<string> { "", "TF" }, 2025, out string cle), Is.Null);
        Assert.That(cle, Is.EqualTo("regul-2025-TF"), "rangée sous la facture existante : c'est une correction, pas un nouveau numéro");
    }

    [Test]
    public void Deux_factures_existantes_ne_se_melangent_pas()
    {
        var loc = Loc();
        loc.facturesEtat.Add(Emise("regul-2025", "gen.pdf"));
        loc.facturesEtat.Add(Emise("regul-2025-TF", "tf.pdf"));

        Assert.That(FactureRegulPanel.Regroupement(loc, new List<string> { "", "TF" }, 2025, out _),
                    Does.Contain("factures différentes"), "sinon les charges de taxe foncière seraient refacturées");
    }

    [Test]
    public void Corriger_une_facture_regroupee_garde_toutes_ses_listes()
    {
        var loc = Loc();
        loc.facturesEtat.Add(Emise("regul-2025", "groupe.pdf"));
        loc.facturesEtat.Add(Emise("regul-2025-TF", "groupe.pdf"));

        Assert.That(FactureRegulPanel.Regroupement(loc, new List<string> { "" }, 2025, out _), Does.Contain("couvre aussi"));
        Assert.That(FactureRegulPanel.Regroupement(loc, new List<string> { "", "TF" }, 2025, out _), Is.Null);
        Assert.That(FactureRegulPanel.Regroupement(loc, new List<string>(), 2025, out _), Does.Contain("au moins une"));
    }

    [Test]
    public void Une_regularisation_emise_reste_au_suivi_meme_si_sa_liste_n_est_plus_datee()
    {
        var loc = Loc(dateTF: "");   // plus de date pour la taxe foncière…
        loc.facturesEtat.Add(Emise("regul-2025-TF", "tf.pdf"));   // …mais sa facture 2025 est partie

        var cles = FacturationSuivi.Lignes(loc, 2025).Select(x => x.key).ToList();

        Assert.That(cles, Does.Contain("regul-2025-TF"));
        Assert.That(cles.Count(k => k == "regul-2025-TF"), Is.EqualTo(1));
    }

    [Test]
    public void Payer_la_regularisation_d_une_liste_ne_touche_que_ses_charges()
    {
        var loc = Loc();
        loc.facturesEtat.Add(new FactureEtat { key = "regul-2025-TF", statut = "Envoye" });
        var gen = Charge("entretien", "");
        var tf = Charge("taxe", "TF");
        gen.MarquerFacturee("A", "2026-01-31");
        tf.MarquerFacturee("A", "2026-11-15");
        var bat = new Batiment { charges = { gen, tf }, locataireDuBatiment = { loc } };

        FacturationSuivi.SetStatut(loc, loc.facturesEtat[0], "Paye", bat);

        Assert.That(tf.FacturationDe("A").paye, Is.True);
        Assert.That(gen.FacturationDe("A").paye, Is.False);
    }
}
