using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// TVA sur les loyers (chantier du 02/11) : la règle en un endroit (Tva), et ce qui la lit.
public class TvaTests
{
    [Test]
    public void La_regle_entreprise_puis_bail_puis_facture()
    {
        var r = new ReglageData();   // soumis, 20 % : les entreprises existantes ne changent pas
        Assert.That(Tva.Soumis(new Locataire(), r), Is.True);
        Assert.That(Tva.Taux(new Locataire(), null, r), Is.EqualTo(20f));
        Assert.That(Tva.Taux(new Locataire { tvaLoyer = "non" }, null, r), Is.EqualTo(0f), "bail exonéré");
        Assert.That(Tva.Taux(new Locataire { tvaLoyer = "oui", tauxTvaLoyer = 10f }, null, r), Is.EqualTo(10f), "le taux du bail");
        Assert.That(Tva.Taux(new Locataire { tauxTvaLoyer = 10f }, new FactureInfo { tauxTva = 5.5f }, r), Is.EqualTo(5.5f), "la facture d'abord");
        var exoneree = new ReglageData { loyersSoumisTva = false, tauxTva = 8.5f };
        Assert.That(Tva.Taux(new Locataire(), null, exoneree), Is.EqualTo(0f), "entreprise sans option");
        Assert.That(Tva.Taux(new Locataire { tvaLoyer = "oui" }, null, exoneree), Is.EqualTo(8.5f), "le bail déroge, au taux de l'entreprise");
        Assert.That(Tva.Taux(new Locataire { tauxTvaLoyer = 10f }, null, exoneree), Is.EqualTo(10f), "BK4 : un bail réglé à la main (son taux) reste soumis quand l'entreprise ne l'est plus");
        Assert.That(Tva.Soumis(new Locataire { tvaLoyer = "non", tauxTvaLoyer = 10f }, r), Is.False, "« non » l'emporte");
        Assert.That(Tva.Taux(null, null, exoneree), Is.EqualTo(0f));
    }

    [Test]
    public void Les_montants_et_les_libelles()
    {
        Assert.That(Tva.Montant(1000f, 20f), Is.EqualTo(200f));
        Assert.That(Tva.Ttc(1000f, 5.5f), Is.EqualTo(1055f));
        Assert.That(Tva.Ttc(1000f, 0f), Is.EqualTo(1000f), "exonéré : TTC = total");
        Assert.That(Tva.Ht(1200f, 20f), Is.EqualTo(1000f));
        Assert.That(Tva.Ht(1000f, 0f), Is.EqualTo(1000f));
        Assert.That(Tva.Libelle(20f), Is.EqualTo("TVA 20%"));
        Assert.That(Tva.Libelle(5.5f), Is.EqualTo("TVA 5,5%"));
        Assert.That(Tva.Mention(new ReglageData()), Is.EqualTo(Tva.ExonerationDefaut));
        Assert.That(Tva.Mention(new ReglageData { mentionTvaExoneration = "Exonéré" }), Is.EqualTo("Exonéré"));
        Assert.That(new FactureEtat().tauxTva, Is.EqualTo(20f), "une ligne d'avant le chantier se relit à 20 %");
    }

    [Test]
    public void Les_parts_et_les_relectures_suivent_le_taux()
    {
        var listes = new List<string> { "generale", "tf" };
        var parts = FactureRegulPanel.MontantsParListe(listes, id => id == "tf" ? 800f : 2000f, id => id == "tf" ? 300f : 0f, 2, 1000f, 0f);
        Assert.That(parts["tf"], Is.EqualTo(200f).Within(0.01f), "exonéré : 800 − 600, sans TVA");
        Assert.That(FactureRefacPanel.PartsTtc(new[] { 100f, 50f }, 150f, 0f), Is.EqualTo(new[] { 100f, 50f }));
        Assert.That(FactureRefacPanel.PartsTtc(new[] { 100f, 50f }, 180f), Is.EqualTo(new[] { 120f, 60f }), "20 % par défaut");

        var loc = new Locataire();
        var rec = new FactureEtat { key = "loyer-2026-P1", type = "Loyer", montant = 1000f, tauxTva = 0f, pdfPath = "x.pdf", libelle = "Loyer janvier 2026" };
        loc.facturesEtat.Add(rec);
        Assert.That(FactureEnvoiAuto.Contexte(loc, rec).loyerHT, Is.EqualTo(1000f).Within(0.01f), "e-mail d'un bail exonéré : HT = TTC");
        rec.tauxTva = 20f;
        Assert.That(FactureEnvoiAuto.Contexte(loc, rec).loyerHT, Is.EqualTo(833.33f).Within(0.01f));
    }

    [Test]
    public void Les_ecrans_passent_par_la_regle()
    {
        string src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));
        foreach (var p in new[] { "Facturation/FactureLoyerPanel.cs", "Facturation/FactureRegulPanel.cs", "Facturation/FactureRefacPanel.cs" })
        {
            string s = src(p);
            StringAssert.Contains("TauxFacture()", s, p);
            StringAssert.DoesNotContain("* .2f", s, p + " : plus de 20 % en dur");
            StringAssert.DoesNotContain("* 1.2f", s, p + " : plus de 20 % en dur");
            StringAssert.DoesNotContain("/ 1.2f", s, p + " : plus de 20 % en dur");
        }
        StringAssert.Contains("Tva.Taux(loc)", src("RevisionLoyer/LoyerSummaryUI.cs"), "carte Loyer");
        StringAssert.Contains("bail exonéré de TVA", src("Facturation/FactureElectronique.cs"), "ligne d'envoi");
        StringAssert.Contains("Tva.Ht(ttc, rec.tauxTva)", src("Facturation/FactureEnvoiAuto.cs"), "e-mails");
        StringAssert.Contains("Tva.Ttc(ht, Tva.Taux(_loc))", src("Facturation/FactureDepotPanel.cs"), "dépôt sur TTC");
        StringAssert.Contains("RefreshTvaVisibility", src("RevisionLoyer/RevisionPanel.cs"), "Gestion du loyer");
        StringAssert.Contains("BuildTva(facturation)", src("Facturation/ReglagePanel.cs"), "Réglages");
    }

    // Retours BK du 02/11 : la case « Assujetti à la TVA » de la fiche est retirée (la plateforme suit le bail et le SIRET),
    // Gestion du loyer a une case + taux, et masquer le taux ne cache plus la section « Loyer facturé ».
    [Test]
    public void La_plateforme_suit_le_bail_et_le_siret()
    {
        string src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));
        StringAssert.DoesNotContain("assujettiTva", src("Locataire/Locataire.cs"));
        StringAssert.DoesNotContain("AssujettiTva", src("Locataire/LocatairePrefab.cs"));
        StringAssert.Contains("Active && Tva.Soumis(loc)", src("Facturation/FactureElectronique.cs"), "Concerne et SansSiret");
        StringAssert.Contains("_tvaToggle", src("RevisionLoyer/RevisionPanel.cs"), "une case, pas un menu");
        StringAssert.Contains("if (_tvaTouche) _loc.tvaLoyer", src("RevisionLoyer/RevisionPanel.cs"), "BK4 : le bail garde son choix dès qu'on y a touché");
        foreach (var p in new[] { "Facturation/FactureLoyerPanel.cs", "Facturation/FactureRegulPanel.cs", "Facturation/FactureRefacPanel.cs" })
        {
            StringAssert.DoesNotContain("_tauxTva.transform.parent", src(p), p + " : cachait toute la section");
            StringAssert.Contains("UIFactory.MontrerAvecLibelle(_tauxTva.gameObject", src(p), p);
        }
        var section = new GameObject("section");
        new GameObject("libellé").transform.SetParent(section.transform);
        var champ = new GameObject("champ"); champ.transform.SetParent(section.transform);
        UIFactory.MontrerAvecLibelle(champ, false);
        Assert.That(section.activeSelf && !champ.activeSelf && !section.transform.GetChild(0).gameObject.activeSelf, "le champ et son libellé seuls");
        UIFactory.MontrerAvecLibelle(champ, true);
        Assert.That(champ.activeSelf && section.transform.GetChild(0).gameObject.activeSelf);
        UnityEngine.Object.DestroyImmediate(section);
    }

    // BK (02/11) : les totaux du formulaire suivent le taux : sans TVA, une seule ligne « Total » ; sinon « TVA 5,5% ».
    [Test]
    public void Les_totaux_du_formulaire_suivent_le_taux()
    {
        string src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));
        foreach (var p in new[] { "Facturation/FactureLoyerPanel.cs", "Facturation/FactureRegulPanel.cs", "Facturation/FactureRefacPanel.cs" })
        {
            StringAssert.Contains("UIFactory.LigneMontant(_", src(p), p);
            StringAssert.DoesNotContain("\"exonéré\"", src(p), p + " : plus de parenthèse à côté du montant");
        }
        StringAssert.DoesNotContain("\"TVA 20 % (retenues soumises)\"", src("Facturation/FactureDepotPanel.cs"), "dépôt au taux du bail");
        var rangee = new GameObject("Row");
        var lib = new GameObject("lib").AddComponent<TMPro.TextMeshProUGUI>(); lib.transform.SetParent(rangee.transform);
        var val = new GameObject("val").AddComponent<TMPro.TextMeshProUGUI>(); val.transform.SetParent(rangee.transform);
        UIFactory.LigneMontant(val, true, "TVA 5,5%");
        Assert.That(lib.text, Is.EqualTo("TVA 5,5%"));
        UIFactory.LigneMontant(val, false);
        Assert.That(rangee.activeSelf, Is.False, "la rangée entière, libellé compris");
        UnityEngine.Object.DestroyImmediate(rangee);
    }
}
