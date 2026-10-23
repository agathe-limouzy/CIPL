using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// Non-régression de M5, G1, G2, G4 et de la corbeille photos : tout ce qui touche
/// au disque. Chaque test s'exécute dans une racine de sauvegarde TEMPORAIRE — la
/// sauvegarde réelle de l'utilisatrice n'est jamais lue ni écrite.
public class DonneesDisqueTests
{
    string _racineReelle;
    string _bac;

    [SetUp]
    public void Isoler()
    {
        _racineReelle = SaveLocationService.GetSaveRoot();
        _bac = Path.Combine(Path.GetTempPath(), "cipl_tests_" + Guid.NewGuid().ToString("N"));
        SaveLocationService.UseRoot(Path.Combine(_bac, "CIPL_Saves"));
    }

    [TearDown]
    public void Restaurer()
    {
        SaveLocationService.UseRoot(_racineReelle);   // priorité absolue : rendre la vraie racine
        try { if (Directory.Exists(_bac)) Directory.Delete(_bac, true); } catch { /* bac temporaire */ }
    }

    static void Fichier(string chemin, string contenu)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(chemin));
        File.WriteAllText(chemin, contenu);
    }

    // ── Nommage des fichiers et dossiers (G3, G4) ───────────────────────────

    [Test]
    public void Un_nom_de_fichier_ne_contient_aucun_caractere_interdit()
    {
        string propre = DossiersDonnees.NomFichier("Loyer:Dupont/2026*T3?\"<>|");
        foreach (char c in Path.GetInvalidFileNameChars())
            Assert.That(propre.IndexOf(c), Is.LessThan(0), $"caractère interdit « {c} » resté");
    }

    [Test]
    public void Un_nom_valide_n_est_pas_modifie()
    {
        Assert.That(DossiersDonnees.NomFichier("Loyer-Dupont-2026"), Is.EqualTo("Loyer-Dupont-2026"));
        Assert.That(DossiersDonnees.NomFichier(null), Is.EqualTo(""));
    }

    [Test]
    public void Factures_et_charges_sont_rangees_sous_le_NOM_du_batiment()
    {
        // Elles atterrissaient dans des dossiers nommés par GUID, invisibles pour qui
        // ouvre le dossier lisible du bâtiment. Rangement du 23/10 : ce qui concerne le bâtiment
        // dans « _Infos du bâtiment », les factures du locataire par sorte.
        string bat = DossiersDonnees.DossierBatiment("Immeuble Rivoli");
        Assert.That(DossiersDonnees.DossierCharges("Immeuble Rivoli"), Is.EqualTo(Path.Combine(bat, "_Infos du bâtiment", "Factures de charges")));
        Assert.That(DossiersDonnees.DossierPhotos("Immeuble Rivoli"), Is.EqualTo(Path.Combine(bat, "_Infos du bâtiment", "Photos")));
        Assert.That(DossiersDonnees.DossierObjectifs("Immeuble Rivoli", null), Is.EqualTo(Path.Combine(bat, "_Infos du bâtiment", "Objectifs")));
        Assert.That(DossiersDonnees.DossierObjectifs("Immeuble Rivoli", "Dupont"), Is.EqualTo(Path.Combine(bat, "Dupont", "Objectifs")));

        string factures = DossiersDonnees.DossierFactures("Immeuble Rivoli", "Dupont");
        Assert.That(factures, Is.EqualTo(Path.Combine(bat, "Dupont", "Factures")));
        Assert.That(DossiersDonnees.CheminFacture(factures, "Loyer-Dupont-Octobre 2026.pdf"),
                    Is.EqualTo(Path.Combine(factures, "Loyers", "Loyer-Dupont-Octobre 2026.pdf")));
        foreach (var (f, sorte) in new[]
        {
            ("RegularisationdeCharge-Dupont-2025.pdf", "Régularisations"), ("Refacturation-eau-Dupont-10-2026.pdf", "Refacturations"),
            ("Avoir-eau-Dupont-10-2026.pdf", "Avoirs"), ("DepotGarantie-Dupont-2026.pdf", "Dépôt de garantie"),
            ("RevisionDepot-Dupont-2027.pdf", "Dépôt de garantie"), ("DecompteSortie-Dupont-2028.pdf", "Dépôt de garantie"),
            ("Quittance-Dupont-Octobre 2026.pdf", "Quittances"), ("autre.pdf", ""),
        })
            Assert.That(DossiersDonnees.SousDossierFacture(f), Is.EqualTo(sorte), f);
    }

    [Test]
    public void Un_document_range_a_l_ancienne_a_sa_nouvelle_place()
    {
        foreach (var (avant, apres) in new[]
        {
            ("Batiment/rivoli/Photos/facade.jpg", "Batiment/rivoli/_Infos du bâtiment/Photos/facade.jpg"),
            ("Batiment/rivoli/Objectifs/devis.pdf", "Batiment/rivoli/_Infos du bâtiment/Objectifs/devis.pdf"),
            ("Batiment/rivoli/Charge/eau.pdf", "Batiment/rivoli/_Infos du bâtiment/Factures de charges/eau.pdf"),
            ("Batiment/rivoli/Sephora/Facture/Loyer-Sephora-Loyer 3e Trimestre 2026-corrigee1.pdf",
             "Batiment/rivoli/Sephora/Factures/Loyers/Loyer-Sephora-Loyer 3e Trimestre 2026-corrigee1.pdf"),
            ("Batiment/rivoli/Sephora/Facture/autre.pdf", "Batiment/rivoli/Sephora/Factures/autre.pdf"),
            ("Batiment/1eecbbbc-e00f-4dbf-b451-0acf0a6b83c9/e6971e84-be55-4de8-8f44-b9a140975381/Facture/Loyer-x.pdf",
             "Anciens documents/1eecbbbc-e00f-4dbf-b451-0acf0a6b83c9/e6971e84-be55-4de8-8f44-b9a140975381/Facture/Loyer-x.pdf"),
            // déjà rangés, ou hors du rangement : inchangés
            ("Batiment/rivoli/Sephora/Bail/bail.pdf", "Batiment/rivoli/Sephora/Bail/bail.pdf"),
            ("Batiment/rivoli/Sephora/Objectifs/devis.pdf", "Batiment/rivoli/Sephora/Objectifs/devis.pdf"),
            ("Logo/logo.png", "Logo/logo.png"),
        })
        {
            Assert.That(DossiersDonnees.Nouveau(avant), Is.EqualTo(apres), avant);
            Assert.That(DossiersDonnees.Nouveau(apres), Is.EqualTo(apres), "rangé une seule fois : " + apres);
        }
        Assert.That(DossiersDonnees.Nouveau("Batiment/rivoli/Sephora/Facture/apercu_regul.png"), Is.Null, "un aperçu n'est pas un document");
        Assert.That(DossiersDonnees.Nouveau("Batiment/2c5459f5-7108-4995-9c1d-438714d1cefc/8f0aa55a-e71a-459f-bc77-40394e8ada2f/Facture/apercu.png"),
                    Is.Null, "même dans un dossier à identifiant");
    }

    [Test]
    public void Le_rangement_deplace_les_fichiers_et_les_chemins_enregistres_suivent()
    {
        string racine = SaveLocationService.GetSaveRoot();
        string R(string c) => Path.Combine(racine, c.Replace('/', Path.DirectorySeparatorChar));
        Fichier(R("Batiment/rivoli/Photos/facade.jpg"), "photo");
        Fichier(R("Batiment/rivoli/Sephora/Facture/Loyer-Sephora-Octobre 2026.pdf"), "loyer");
        Fichier(R("Batiment/rivoli/Sephora/Facture/apercu.png"), "aperçu");
        Fichier(R("Batiment/rivoli/Sephora/Bail/bail.pdf"), "bail");
        Fichier(R("Batiment/rivoli/Charge/eau.pdf"), "ancienne");
        Fichier(R("Batiment/rivoli/_Infos du bâtiment/Factures de charges/eau.pdf"), "nouvelle");
        // Pas encore rangé (PC ouvert hors ligne) : le chemin enregistré mène à l'ancien fichier.
        Assert.That(File.ReadAllText(DossiersDonnees.VersAbsolu("Batiment/rivoli/Photos/facade.jpg")), Is.EqualTo("photo"));

        var faits = DocumentsCloud.RangerIci(racine);

        Assert.That(faits, Has.Count.EqualTo(4));
        Assert.That(faits["Batiment/rivoli/Sephora/Facture/apercu.png"], Is.Null, "aperçu retiré");
        Assert.That(File.ReadAllText(R("Batiment/rivoli/Sephora/Factures/Loyers/Loyer-Sephora-Octobre 2026.pdf")), Is.EqualTo("loyer"));
        Assert.That(File.ReadAllText(R("Batiment/rivoli/_Infos du bâtiment/Factures de charges/eau (2).pdf")), Is.EqualTo("ancienne"), "rien n'est écrasé");
        Assert.That(File.Exists(R("Batiment/rivoli/Sephora/Bail/bail.pdf")), Is.True);
        Assert.That(Directory.Exists(R("Batiment/rivoli/Sephora/Facture")) || Directory.Exists(R("Batiment/rivoli/Photos")), Is.False, "dossiers vidés retirés");
        // Les fiches ne sont pas réécrites : l'ancien chemin enregistré mène au nouvel emplacement.
        Assert.That(File.ReadAllText(DossiersDonnees.VersAbsolu("Batiment/rivoli/Photos/facade.jpg")), Is.EqualTo("photo"));
        Assert.That(File.ReadAllText(DossiersDonnees.VersAbsolu("Batiment/rivoli/Sephora/Facture/Loyer-Sephora-Octobre 2026.pdf")), Is.EqualTo("loyer"));
        Assert.That(DocumentsCloud.RangerIci(racine), Is.Empty, "une seule fois");
    }

    // ── Chemins relatifs (G1) ───────────────────────────────────────────────

    [Test]
    public void Un_chemin_sous_la_racine_fait_l_aller_retour()
    {
        string absolu = Path.Combine(DossiersDonnees.DossierPhotos("Rivoli"), "facade.jpg");
        string relatif = DossiersDonnees.VersRelatif(absolu);

        Assert.That(Path.IsPathRooted(relatif), Is.False);
        Assert.That(Path.GetFullPath(DossiersDonnees.VersAbsolu(relatif)),
                    Is.EqualTo(Path.GetFullPath(absolu)).IgnoreCase);
    }

    [Test]
    public void Un_chemin_absolu_herite_est_renvoye_tel_quel()
    {
        Assert.That(DossiersDonnees.VersAbsolu(@"D:\Ancien\photo.jpg"), Is.EqualTo(@"D:\Ancien\photo.jpg"));
    }

    // ── Renommage : les PDF de factures et de charges suivent ───────────────

    [Test]
    public void Apres_un_renommage_les_PDF_de_factures_et_de_charges_se_retrouvent()
    {
        // Un renommage déplace le dossier sans réécrire les fiches (sur le cloud, chaque locataire
        // a son verrou) : le PDF se cherche sous les noms actuels.
        string loyer = DossiersDonnees.CheminFacture(DossiersDonnees.DossierFactures("Rivoli", "Sephora"), "Loyer-Sephora-Octobre 2026.pdf");
        string eau = Path.Combine(DossiersDonnees.DossierCharges("Rivoli"), "eau.pdf");
        Fichier(loyer, "loyer"); Fichier(eau, "eau");
        var rec = new FactureEtat { pdfPath = DossiersDonnees.VersRelatif(loyer) };
        string charge = DossiersDonnees.VersRelatif(eau);

        Assert.That(DossiersDonnees.RenommerBatiment("Rivoli", "Rivoli Neuf", out string err), Is.True, err);
        Assert.That(DossiersDonnees.RenommerLocataire("Rivoli Neuf", "Sephora", "Sephora Paris", out err), Is.True, err);

        Assert.That(File.Exists(DossiersDonnees.VersAbsolu(rec.pdfPath)), Is.False, "le chemin enregistré ne mène plus au fichier");
        Assert.That(File.ReadAllText(FacturationSuivi.CheminPdf(rec, "Rivoli Neuf", "Sephora Paris")), Is.EqualTo("loyer"));
        Assert.That(File.ReadAllText(DossiersDonnees.PdfCharge("Rivoli Neuf", charge)), Is.EqualTo("eau"));
        // Enregistré avant le rangement du 23/10 (« Facture », sans sorte), puis renommé.
        Assert.That(File.ReadAllText(DossiersDonnees.PdfFacture("Rivoli Neuf", "Sephora Paris",
            "Batiment/Rivoli/Sephora/Facture/Loyer-Sephora-Octobre 2026.pdf")), Is.EqualTo("loyer"));

        // Seul le chemin enregistré mène au fichier (dossier à identifiant rangé dans « Anciens documents ») : il sert.
        const string bat = "1eecbbbc-e00f-4dbf-b451-0acf0a6b83c9", loc = "e6971e84-be55-4de8-8f44-b9a140975381";
        Fichier(Path.Combine(SaveLocationService.GetSaveRoot(), "Anciens documents", bat, loc, "Facture", "Loyer-x.pdf"), "ancien");
        Assert.That(File.ReadAllText(DossiersDonnees.PdfFacture("Rivoli Neuf", "Sephora Paris",
            $"Batiment/{bat}/{loc}/Facture/Loyer-x.pdf")), Is.EqualTo("ancien"));

        // Absent partout (cloud, Drive pas encore recopié) : le chemin sous les noms actuels.
        Assert.That(DossiersDonnees.PdfFacture("Rivoli Neuf", "Sephora Paris", "Batiment/Rivoli/Sephora/Factures/Loyers/Loyer-Sephora-Novembre 2026.pdf"),
                    Is.EqualTo(DossiersDonnees.CheminFacture(DossiersDonnees.DossierFactures("Rivoli Neuf", "Sephora Paris"), "Loyer-Sephora-Novembre 2026.pdf")));
    }

    // ── M5 : migration d'entreprise ─────────────────────────────────────────

    [Test]
    public void Migrer_vers_un_dossier_vide_copie_tout_y_compris_secrets_et_photos()
    {
        string src = Path.Combine(_bac, "A"), dst = Path.Combine(_bac, "Vide");
        Fichier(Path.Combine(src, "reglage.json"), "{}");
        Fichier(Path.Combine(src, "pennylane_secrets.dat"), "CLE-FACTICE");
        Fichier(Path.Combine(src, "photos", "p1.jpg"), "IMAGE");
        Directory.CreateDirectory(dst);

        Assert.That(SaveLocationService.MigrateData(src, dst, out string err), Is.True, err);
        Assert.That(File.Exists(Path.Combine(dst, "reglage.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(dst, "pennylane_secrets.dat")), Is.True, "les secrets doivent suivre");
        Assert.That(File.Exists(Path.Combine(dst, "photos", "p1.jpg")), Is.True, "les photos doivent suivre");
    }

    [Test]
    public void Migrer_vers_un_dossier_deja_peuple_est_refuse_sans_rien_ecraser()
    {
        string src = Path.Combine(_bac, "A"), dst = Path.Combine(_bac, "B");
        Fichier(Path.Combine(src, "reglage.json"), "{\"entreprise\":\"A\"}");
        Fichier(Path.Combine(src, "pennylane_secrets.dat"), "CLE-A");
        const string reglageB = "{\"entreprise\":\"B\"}";
        Fichier(Path.Combine(dst, "reglage.json"), reglageB);

        // Le refus journalise volontairement une erreur : on la declare, sinon NUnit
        // fait echouer le test sur ce Debug.LogError.
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Migration annul"));

        Assert.That(SaveLocationService.MigrateData(src, dst, out string err), Is.False);
        Assert.That(err, Does.Contain("annulée"));
        Assert.That(File.ReadAllText(Path.Combine(dst, "reglage.json")), Is.EqualTo(reglageB),
                    "les réglages de l'autre entreprise doivent être intacts");
        Assert.That(File.Exists(Path.Combine(dst, "pennylane_secrets.dat")), Is.False,
                    "aucune copie partielle avant le refus");
    }

    // ── Corbeille photos : suppression annulable ────────────────────────────

    [Test]
    public void Supprimer_une_photo_la_met_en_corbeille_et_l_annulation_la_remet()
    {
        var bat = new Batiment { Name = "Rivoli" };
        string abs = Path.Combine(DossiersDonnees.DossierPhotos(bat.Name), "cour.jpg");
        Fichier(abs, "IMAGE-COUR");
        string stocke = DossiersDonnees.VersRelatif(abs);
        bat.photos.Add("Batiment/Rivoli/Photos/facade.jpg");
        bat.photos.Add(stocke);
        bat.coverPhoto = stocke;

        var suppr = PhotoService.Supprimer(bat, stocke);

        Assert.That(suppr, Is.Not.Null);
        Assert.That(bat.photos, Does.Not.Contain(stocke));
        Assert.That(bat.coverPhoto, Is.Empty, "la couverture doit être libérée");
        Assert.That(File.Exists(abs), Is.False, "le fichier quitte le dossier du bâtiment");
        Assert.That(File.Exists(suppr.cheminCorbeille), Is.True, "mais il n'est PAS détruit");

        Assert.That(PhotoService.Restaurer(bat, suppr), Is.True);
        Assert.That(File.Exists(abs), Is.True);
        Assert.That(File.ReadAllText(abs), Is.EqualTo("IMAGE-COUR"));
        Assert.That(bat.photos[1], Is.EqualTo(stocke), "remise à sa position d'origine");
        Assert.That(bat.coverPhoto, Is.EqualTo(stocke), "couverture rétablie");
    }

    [Test]
    public void La_purge_supprime_les_vieilles_photos_et_garde_les_recentes()
    {
        string corbeille = Path.Combine(SaveLocationService.GetSaveRoot(), "corbeille_photos");
        string vieux = Path.Combine(corbeille, "vieux.jpg"), recent = Path.Combine(corbeille, "recent.jpg");
        Fichier(vieux, "x"); Fichier(recent, "x");
        File.SetLastWriteTime(vieux, DateTime.Now.AddDays(-30));

        PhotoService.PurgerCorbeille();

        Assert.That(File.Exists(vieux), Is.False);
        Assert.That(File.Exists(recent), Is.True);
    }

    [Test]
    public void Supprimer_une_photo_dont_le_fichier_manque_ne_leve_pas()
    {
        var bat = new Batiment { Name = "Rivoli" };
        bat.photos.Add("Batiment/Rivoli/Photos/fantome.jpg");

        var suppr = PhotoService.Supprimer(bat, "Batiment/Rivoli/Photos/fantome.jpg");

        Assert.That(suppr, Is.Not.Null);
        Assert.That(suppr.cheminCorbeille, Is.Empty);
        Assert.That(bat.photos, Is.Empty);
    }

    // ── G2 : le cache photo ─────────────────────────────────────────────────

    [Test]
    public void Vider_le_cache_force_le_rechargement_depuis_le_disque()
    {
        // Sans ce vidage, deux entreprises ayant un bâtiment de même nom avec un
        // fichier de même nom partagent la clé : la seconde affichait la photo de
        // la première.
        string abs = Path.Combine(DossiersDonnees.DossierPhotos("Rivoli"), "facade.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(abs));
        File.WriteAllBytes(abs, new Texture2D(4, 4).EncodeToPNG());
        string stocke = DossiersDonnees.VersRelatif(abs);

        var t1 = PhotoService.Charger(stocke);
        Assert.That(t1, Is.Not.Null);
        int id1 = t1.GetInstanceID();
        Assert.That(PhotoService.Charger(stocke).GetInstanceID(), Is.EqualTo(id1), "cache actif");

        PhotoService.ViderCache();

        var t2 = PhotoService.Charger(stocke);
        Assert.That(t2, Is.Not.Null);
        Assert.That(t2.GetInstanceID(), Is.Not.EqualTo(id1), "rechargée, pas servie par le cache");
    }
}
