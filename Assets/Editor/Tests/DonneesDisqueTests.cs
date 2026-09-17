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
        // ouvre le dossier lisible du bâtiment.
        Assert.That(DossiersDonnees.DossierFactures("Immeuble Rivoli", "Dupont"),
                    Does.Contain("Immeuble Rivoli").And.Contain("Dupont"));
        Assert.That(DossiersDonnees.DossierCharges("Immeuble Rivoli"),
                    Does.Contain("Immeuble Rivoli"));
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
