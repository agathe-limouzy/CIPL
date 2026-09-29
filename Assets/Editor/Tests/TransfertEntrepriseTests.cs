using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// Transfert entre entreprises, sur deux racines TEMPORAIRES : la sauvegarde réelle
/// n'est jamais lue ni écrite (le service ne suit pas la racine active).
public class TransfertEntrepriseTests
{
    string _bac, _orig, _dest;

    [SetUp]
    public void Creer()
    {
        _bac = Path.Combine(Path.GetTempPath(), "cipl_transfert_" + Guid.NewGuid().ToString("N"));
        _orig = Path.Combine(_bac, "Demo", "CIPL_Saves");
        _dest = Path.Combine(_bac, "Cipl", "CIPL_Saves");
        Directory.CreateDirectory(_orig);
        Directory.CreateDirectory(_dest);
    }

    [TearDown]
    public void Nettoyer()
    {
        try { if (Directory.Exists(_bac)) Directory.Delete(_bac, true); } catch { /* bac temporaire */ }
    }

    // ── Outils ────────────────────────────────────────────────────────────────

    static Locataire Loc(string nom, string id) => new Locataire { Name = nom, id = id };

    static Batiment Bat(string nom, string id, params Locataire[] locs)
        => new Batiment { Name = nom, id = id, locataireDuBatiment = locs.ToList() };

    static void Ecrire(string racine, Batiment b)
    {
        Directory.CreateDirectory(Path.Combine(racine, "batiments"));
        File.WriteAllText(Path.Combine(racine, "batiments", $"batiment_{b.id}.json"), JsonUtility.ToJson(b, true));
    }

    static List<Batiment> Lire(string racine)
        => Directory.GetFiles(Path.Combine(racine, "batiments"), "*.json")
                    .Select(f => JsonUtility.FromJson<Batiment>(File.ReadAllText(f))).ToList();

    static void Fichier(string racine, string relatif)
    {
        string c = Path.Combine(racine, relatif);
        Directory.CreateDirectory(Path.GetDirectoryName(c));
        File.WriteAllText(c, relatif);
    }

    static bool Existe(string racine, string relatif) => File.Exists(Path.Combine(racine, relatif));

    string Sauvegarde() => Directory.GetDirectories(Path.Combine(_dest, "corbeille_batiments"))
                                    .Single(d => d.Contains("avant-transfert"));

    // ── Bâtiment ──────────────────────────────────────────────────────────────

    [Test]
    public void Un_batiment_absent_arrive_complet_avec_ses_fichiers()
    {
        var a = Loc("Sephora", "A1");
        a.facturesEtat.Add(new FactureEtat { key = "loyer-2026-P1", pdfPath = "Batiment/Rivoli/Sephora/Facture/f1.pdf" });
        var src = Bat("Rivoli", "O", a, Loc("Starbucks", "B1"));
        src.photos.Add("Batiment/Rivoli/Photos/facade.jpg");
        Ecrire(_orig, src);
        Fichier(_orig, "Batiment/Rivoli/Photos/facade.jpg");
        Fichier(_orig, "Batiment/Rivoli/Sephora/Facture/f1.pdf");
        Fichier(_orig, "Batiment/Rivoli/Starbucks/Bail/bail.pdf");

        var r = TransfertEntreprise.TransfererBatiment(src, _orig, _dest);

        Assert.That(r.Succes, r.Erreur);
        Assert.That(r.BatimentExistant, Is.False);
        var b = Lire(_dest).Single();
        Assert.That(b.id, Is.EqualTo("O"));
        Assert.That(b.locataireDuBatiment.Select(l => l.Name), Is.EquivalentTo(new[] { "Sephora", "Starbucks" }));
        // Chemins relatifs inchangés et fichiers présents à ces chemins.
        Assert.That(Existe(_dest, b.photos[0]));
        Assert.That(Existe(_dest, b.locataireDuBatiment[0].facturesEtat[0].pdfPath));
        Assert.That(Existe(_dest, "Batiment/Rivoli/Starbucks/Bail/bail.pdf"));
        // L'origine n'est pas touchée : c'est l'appelant qui la retire après succès.
        Assert.That(Existe(_orig, "batiments/batiment_O.json"));
        Assert.That(Existe(_orig, "Batiment/Rivoli/Photos/facade.jpg"));
    }

    [Test]
    public void Un_batiment_existant_est_mis_a_jour_sans_perdre_ce_qui_n_existe_que_la_bas()
    {
        // Destination : même bâtiment (casse différente), un homonyme et un locataire propre.
        var dest = Bat("rivoli", "D", Loc("Sephora", "A2"), Loc("Orange", "C"));
        dest.adressBatiment = "ancienne adresse";
        dest.locataireDuBatiment[0].loyerAnnuel = 1;
        dest.charges.Add(new ChargeBatiment { id = "ch1", nom = "Taxe", ratios = { new ChargeRatio("A2", 1) } });
        dest.photos.Add("Batiment/rivoli/Photos/ancienne.jpg");
        Ecrire(_dest, dest);
        Fichier(_dest, "Batiment/rivoli/Sephora/Facture/ancienne.pdf");
        Fichier(_dest, "Batiment/rivoli/Orange/Facture/orange.pdf");

        var src = Bat("Rivoli", "O", Loc("Sephora", "A1"), Loc("Starbucks", "B1"));
        src.adressBatiment = "nouvelle adresse";
        src.locataireDuBatiment[0].loyerAnnuel = 999;
        src.charges.Add(new ChargeBatiment
        {
            id = "ch2", nom = "Toiture", tousLocataires = false,
            locatairesConcernes = { "A1", "B1" }, ratios = { new ChargeRatio("A1", 1), new ChargeRatio("B1", 1) },
        });
        Fichier(_orig, "Batiment/Rivoli/Sephora/Facture/nouvelle.pdf");

        var r = TransfertEntreprise.TransfererBatiment(src, _orig, _dest);

        Assert.That(r.Succes, r.Erreur);
        Assert.That(r.BatimentExistant && r.LocatairesMisAJour == 1 && r.LocatairesAjoutes == 1);
        var b = Lire(_dest).Single();
        Assert.That(b.id, Is.EqualTo("D"), "l'identité de destination est conservée");
        Assert.That(b.adressBatiment, Is.EqualTo("nouvelle adresse"));

        var sephora = b.locataireDuBatiment.Single(l => l.Name == "Sephora");
        Assert.That(sephora.id, Is.EqualTo("A2"), "l'homonyme garde son id de destination");
        Assert.That(sephora.loyerAnnuel, Is.EqualTo(999f));
        Assert.That(b.locataireDuBatiment.Select(l => l.Name), Is.EquivalentTo(new[] { "Sephora", "Orange", "Starbucks" }));

        // Charges : fusion par id, locataires remappés vers les ids de destination.
        var ch2 = b.charges.Single(c => c.id == "ch2");
        Assert.That(b.charges.Select(c => c.id), Is.EquivalentTo(new[] { "ch1", "ch2" }));
        Assert.That(ch2.locatairesConcernes, Is.EquivalentTo(new[] { "A2", "B1" }));
        Assert.That(ch2.ratios.Select(x => x.locataireId), Is.EquivalentTo(new[] { "A2", "B1" }));
        Assert.That(b.photos, Does.Contain("Batiment/rivoli/Photos/ancienne.jpg"));

        // Dossier du locataire transféré remplacé en entier ; celui d'Orange intact.
        Assert.That(Existe(_dest, "Batiment/rivoli/Sephora/Facture/nouvelle.pdf"));
        Assert.That(Existe(_dest, "Batiment/rivoli/Sephora/Facture/ancienne.pdf"), Is.False);
        Assert.That(Existe(_dest, "Batiment/rivoli/Orange/Facture/orange.pdf"));

        // L'ancienne version est gardée en corbeille (dossier + JSON).
        string sauv = Sauvegarde();
        Assert.That(File.Exists(Path.Combine(sauv, "Sephora", "Facture", "ancienne.pdf")));
        Assert.That(File.Exists(Path.Combine(sauv, "batiment_D.json")));
    }

    [Test]
    public void Collision_d_id_sans_homonyme_donne_un_nouvel_id()
    {
        Ecrire(_dest, Bat("Autre", "X"));
        var src = Bat("Rivoli", "X", Loc("Sephora", "A1"));

        var r = TransfertEntreprise.TransfererBatiment(src, _orig, _dest);

        Assert.That(r.Succes, r.Erreur);
        var bats = Lire(_dest);
        Assert.That(bats.Count, Is.EqualTo(2));
        Assert.That(bats.Single(b => b.Name == "Rivoli").id, Is.Not.EqualTo("X"));
        Assert.That(bats.Single(b => b.Name == "Autre").id, Is.EqualTo("X"));
    }

    [Test]
    public void Un_chemin_absolu_herite_devient_relatif()
    {
        var a = Loc("Sephora", "A1");
        a.facturesEtat.Add(new FactureEtat { pdfPath = Path.Combine(_orig, "Batiment", "Rivoli", "Sephora", "Facture", "f.pdf") });
        a.cheminBail = Path.Combine(_orig, "Batiment", "Rivoli", "Sephora", "Bail", "bail.pdf");
        a.avenants.Add(@"D:\ailleurs\avenant.pdf");
        var src = Bat("Rivoli", "O", a);
        src.photos.Add(Path.Combine(_orig, "Batiment", "Rivoli", "Photos", "f.jpg"));

        var r = TransfertEntreprise.TransfererBatiment(src, _orig, _dest);

        Assert.That(r.Succes, r.Erreur);
        var b = Lire(_dest).Single();
        var l = b.locataireDuBatiment[0];
        Assert.That(b.photos[0], Is.EqualTo("Batiment/Rivoli/Photos/f.jpg"));
        Assert.That(l.facturesEtat[0].pdfPath, Is.EqualTo("Batiment/Rivoli/Sephora/Facture/f.pdf"));
        Assert.That(l.cheminBail, Is.EqualTo("bail.pdf"), "le bail se relit par son nom dans le dossier Bail");
        Assert.That(l.avenants[0], Is.EqualTo(@"D:\ailleurs\avenant.pdf"), "hors racine : laissé tel quel");
        Assert.That(src.photos[0], Does.StartWith(_orig), "l'origine n'est pas modifiée");
    }

    // ── Locataire ─────────────────────────────────────────────────────────────

    [Test]
    public void Un_locataire_vers_une_entreprise_sans_son_batiment_cree_le_batiment_complet()
    {
        var src = Bat("Rivoli", "O", Loc("Sephora", "A1"), Loc("Starbucks", "B1"));
        src.charges.Add(new ChargeBatiment { id = "ch", nom = "Taxe" });
        Fichier(_orig, "Batiment/Rivoli/Photos/facade.jpg");
        Fichier(_orig, "Batiment/Rivoli/Charge/taxe.pdf");
        Fichier(_orig, "Batiment/Rivoli/Sephora/Facture/f.pdf");
        Fichier(_orig, "Batiment/Rivoli/Starbucks/Facture/s.pdf");

        var r = TransfertEntreprise.TransfererLocataire(src, src.locataireDuBatiment[0], _orig, _dest);

        Assert.That(r.Succes, r.Erreur);
        var b = Lire(_dest).Single();
        Assert.That(b.locataireDuBatiment.Select(l => l.Name), Is.EqualTo(new[] { "Sephora" }));
        Assert.That(b.charges.Count, Is.EqualTo(1), "bâtiment complet : ses charges suivent");
        Assert.That(Existe(_dest, "Batiment/Rivoli/Photos/facade.jpg"));
        Assert.That(Existe(_dest, "Batiment/Rivoli/Charge/taxe.pdf"));
        Assert.That(Existe(_dest, "Batiment/Rivoli/Sephora/Facture/f.pdf"));
        Assert.That(Directory.Exists(Path.Combine(_dest, "Batiment", "Rivoli", "Starbucks")), Is.False,
                    "le dossier d'un autre locataire ne part pas");
        Assert.That(src.locataireDuBatiment.Count, Is.EqualTo(2), "l'origine n'est pas modifiée");
    }

    [Test]
    public void Un_locataire_homonyme_remplace_son_dossier_en_entier()
    {
        var dest = Bat("Rivoli", "D", Loc("Sephora", "A2"), Loc("Orange", "C"));
        dest.adressBatiment = "adresse de destination";
        Ecrire(_dest, dest);
        Fichier(_dest, "Batiment/Rivoli/Sephora/Facture/ancienne.pdf");
        Fichier(_dest, "Batiment/Rivoli/Photos/dest.jpg");

        var src = Bat("Rivoli", "O", Loc("Sephora", "A1"), Loc("Starbucks", "B1"));
        src.adressBatiment = "adresse d'origine";
        src.locataireDuBatiment[0].loyerAnnuel = 999;
        Fichier(_orig, "Batiment/Rivoli/Sephora/Facture/nouvelle.pdf");
        Fichier(_orig, "Batiment/Rivoli/Photos/orig.jpg");

        var r = TransfertEntreprise.TransfererLocataire(src, src.locataireDuBatiment[0], _orig, _dest);

        Assert.That(r.Succes, r.Erreur);
        Assert.That(r.LocatairesMisAJour, Is.EqualTo(1));
        var b = Lire(_dest).Single();
        Assert.That(b.adressBatiment, Is.EqualTo("adresse de destination"), "le bâtiment de destination garde ses infos");
        var s = b.locataireDuBatiment.Single(l => l.Name == "Sephora");
        Assert.That(s.id, Is.EqualTo("A2"));
        Assert.That(s.loyerAnnuel, Is.EqualTo(999f));
        Assert.That(b.locataireDuBatiment.Count, Is.EqualTo(2), "Starbucks ne part pas, Orange reste");

        Assert.That(Existe(_dest, "Batiment/Rivoli/Sephora/Facture/nouvelle.pdf"));
        Assert.That(Existe(_dest, "Batiment/Rivoli/Sephora/Facture/ancienne.pdf"), Is.False);
        Assert.That(Existe(_dest, "Batiment/Rivoli/Photos/orig.jpg"), Is.False, "seul le dossier du locataire est copié");
        Assert.That(File.Exists(Path.Combine(Sauvegarde(), "Sephora", "Facture", "ancienne.pdf")));
    }

    [Test]
    public void Un_nouveau_locataire_est_ajoute_au_batiment_existant()
    {
        Ecrire(_dest, Bat("Rivoli", "D", Loc("Orange", "A1")));   // même id que le transféré
        var src = Bat("Rivoli", "O", Loc("Sephora", "A1"));

        var r = TransfertEntreprise.TransfererLocataire(src, src.locataireDuBatiment[0], _orig, _dest);

        Assert.That(r.Succes, r.Erreur);
        var locs = Lire(_dest).Single().locataireDuBatiment;
        Assert.That(locs.Select(l => l.Name), Is.EquivalentTo(new[] { "Orange", "Sephora" }));
        Assert.That(locs.Select(l => l.id).Distinct().Count(), Is.EqualTo(2), "collision d'id résolue");
    }

    // ── Sécurité ──────────────────────────────────────────────────────────────

    [Test]
    public void Une_destination_introuvable_ne_touche_a_rien()
    {
        var src = Bat("Rivoli", "O", Loc("Sephora", "A1"));
        Ecrire(_orig, src);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[Transfert\]"));

        var r = TransfertEntreprise.TransfererBatiment(src, _orig, Path.Combine(_bac, "absente"));

        Assert.That(r.Succes, Is.False);
        Assert.That(r.Erreur, Does.Contain("introuvable"));
        Assert.That(Existe(_orig, "batiments/batiment_O.json"));
    }

    [Test]
    public void L_apercu_annonce_la_fusion_sans_rien_ecrire()
    {
        Ecrire(_dest, Bat("Rivoli", "D", Loc("Sephora", "A2")));
        var src = Bat("RIVOLI", "O", Loc("sephora", "A1"), Loc("Starbucks", "B1"));

        var a = TransfertEntreprise.Apercu(src, null, _dest);

        Assert.That(a.BatimentExistant && a.LocatairesMisAJour == 1 && a.LocatairesAjoutes == 1);
        Assert.That(Directory.Exists(Path.Combine(_dest, "corbeille_batiments")), Is.False);
    }

    [Test]
    public void La_mise_en_corbeille_deplace_le_dossier()
    {
        Fichier(_orig, "Batiment/Rivoli/Sephora/Facture/f.pdf");
        string dossier = Path.Combine(_orig, "Batiment", "Rivoli", "Sephora");

        string ou = TransfertEntreprise.MettreEnCorbeille(dossier, _orig);

        Assert.That(Directory.Exists(dossier), Is.False);
        Assert.That(File.Exists(Path.Combine(ou, "Facture", "f.pdf")));
        Assert.That(ou, Does.Contain("corbeille_batiments"));
    }
}
