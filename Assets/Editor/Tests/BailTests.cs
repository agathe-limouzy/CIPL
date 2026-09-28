using System;
using NUnit.Framework;

/// Les règles du bail (28/09) : durée, années fermes, date de fin, anciens types.
public class BailTests
{
    [Test]
    public void La_fin_est_la_veille_de_l_anniversaire()
    {
        Assert.That(Locataire.FinDeBail(new DateTime(2020, 6, 1), 9), Is.EqualTo(new DateTime(2029, 5, 31)));
        Assert.That(Locataire.FinDeBail(new DateTime(2024, 1, 1), 3), Is.EqualTo(new DateTime(2026, 12, 31)));
    }

    /// « La période ferme c'est un temps à l'intérieur de la durée d'un bail défini » :
    /// facultative, jamais plus longue que le bail.
    [Test]
    public void La_periode_ferme_est_comprise_dans_la_duree()
    {
        Assert.That(Locataire.VerifierBail(9, 10), Is.Not.Null, "10 ans fermes sur un bail de 9 ans");
        Assert.That(Locataire.VerifierBail(10, 10), Is.Null, "10 ans, entièrement fermes");
        Assert.That(Locataire.VerifierBail(9, 6), Is.Null, "9 ans dont 6 fermes");
        Assert.That(Locataire.VerifierBail(9, 0), Is.Null, "sans période ferme");
        Assert.That(Locataire.VerifierBail(0, 0), Is.Null, "durée libre (civil, convention précaire)");
        Assert.That(Locataire.VerifierBail(9, -1), Is.Not.Null, "négatif");
    }

    [Test]
    public void La_periode_ferme_n_existe_que_pour_le_bail_commercial()
    {
        Assert.That(Locataire.PeriodeFermePossible(BailType.BailCommercial369), Is.True);
        Assert.That(Locataire.PeriodeFermePossible(BailType.BailCommercialFerme), Is.True, "ancien « ferme »");
        Assert.That(Locataire.PeriodeFermePossible(BailType.BailDerogatoire), Is.False);
        Assert.That(Locataire.PeriodeFermePossible(BailType.BailProfessionnel), Is.False);
    }

    [Test]
    public void La_duree_se_deduit_des_dates_quand_elle_n_est_pas_saisie()
    {
        var l = new Locataire { typeDeBail = BailType.BailCommercial369, dateDebutBailISO = "2020-06-01", dateFinBailISO = "2029-05-31" };
        Assert.That(l.DureeBail(), Is.EqualTo(9));
        l.dateFinBailISO = "2030-05-31";
        Assert.That(l.DureeBail(), Is.EqualTo(10));
        l.dateFinBailISO = "2025-09-15";
        Assert.That(l.DureeBail(), Is.EqualTo(9), "bail repris en cours (5,3 ans entre ses dates) : durée du type");
        l.dureeBailAns = 12;
        Assert.That(l.DureeBail(), Is.EqualTo(12), "la durée saisie prime");
    }

    [Test]
    public void Les_anciens_types_restent_lisibles()
    {
        Assert.That(Locataire.Normaliser(BailType.Bail9ans), Is.EqualTo(BailType.BailCommercial369));
        Assert.That(Locataire.Normaliser(BailType.Bail10ans), Is.EqualTo(BailType.BailCommercial369));
        Assert.That(Locataire.Normaliser(BailType.BailCommercialFerme), Is.EqualTo(BailType.BailCommercial369));
        Assert.That(Locataire.TypesProposes, Has.No.Member(BailType.Bail9ans));
        Assert.That(Locataire.TypesProposes, Has.No.Member(BailType.Bail10ans));
        Assert.That(Locataire.TypesProposes, Has.No.Member(BailType.BailCommercialFerme), "la période ferme n'est pas un type");
        Assert.That(new Locataire { typeDeBail = BailType.BailCommercialFerme }.AnneesFermes(), Is.EqualTo(9),
            "l'ancien « 9 ans ferme » garde ses 9 ans fermes");
        Assert.That(Locataire.EstBailCommercial(BailType.BailCommercialFerme), Is.True, "facturation inchangée");
        // Les fichiers stockent le numéro : il ne doit jamais bouger.
        Assert.That((int)BailType.BailCommercial369, Is.EqualTo(3));
        Assert.That((int)BailType.BailCommercialFerme, Is.EqualTo(4));
        Assert.That((int)BailType.BailCivil, Is.EqualTo(10));
    }

    [Test]
    public void Un_nouveau_locataire_part_en_3_6_9()
        => Assert.That(new Locataire().typeDeBail, Is.EqualTo(BailType.BailCommercial369));

    /// Le défaut ne vaut que pour une fiche neuve : un fichier existant garde son type,
    /// et les nouveaux champs y valent 0 (durée déduite, voir DureeBail).
    [Test]
    public void Un_fichier_existant_garde_son_type()
    {
        var l = UnityEngine.JsonUtility.FromJson<Locataire>("{\"typeDeBail\":0,\"dateDebutBailISO\":\"\"}");
        Assert.That(l.typeDeBail, Is.EqualTo(BailType.BailAContruction));
        Assert.That(l.dureeBailAns, Is.EqualTo(0));
        Assert.That(l.anneesFermes, Is.EqualTo(0));
    }

    /// Le champ cloné porte SON titre et SON unité, même quand le modèle les range
    /// autrement (« Taille Batiment » : titre au nom du champ, unité un niveau plus bas).
    /// Le premier essai affichait « Taille Batiment : … m² » dans la section Bail.
    [Test]
    public void Un_champ_clone_porte_son_titre_et_son_unite()
    {
        var fiche = UnityEngine.Object.Instantiate(
            UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefab/LocatairePrefab.prefab"));
        try
        {
            var modele = fiche.GetComponent<LocatairePrefab>().tailleLotTxt;
            var clone = LocataireFacturationFields.Clone(modele, fiche.transform, "Durée", "9", "ans");
            var textes = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(
                clone.GetComponentsInChildren<TMPro.TMP_Text>(true), t => t.text));
            Assert.That(textes, Has.Member("Durée"));
            Assert.That(textes, Has.Member("ans"));
            Assert.That(textes, Has.None.Contains("Taille Batiment"));
            Assert.That(textes, Has.No.Member("m²"));
        }
        finally { UnityEngine.Object.DestroyImmediate(fiche); }
    }

    // ── Documents du bail (bail et avenants) ────────────────────────────────────

    [Test]
    public void Le_bail_est_range_dans_le_dossier_du_locataire()
    {
        string dossier = DossiersDonnees.DossierBail("rivoli", "Sephora");
        Assert.That(dossier, Does.StartWith(DossiersDonnees.DossierLocataire("rivoli", "Sephora")));
        Assert.That(System.IO.Path.GetFileName(dossier), Is.EqualTo("Bail"));
        Assert.That(DossiersDonnees.CheminDocumentBail("rivoli", "Sephora", "bail.pdf"),
            Is.EqualTo(System.IO.Path.Combine(dossier, "bail.pdf")), "nom de fichier → dossier du locataire");
        Assert.That(DossiersDonnees.CheminDocumentBail("rivoli", "Sephora", @"C:\ancien\bail.pdf"),
            Is.EqualTo(@"C:\ancien\bail.pdf"), "ancien format : chemin complet gardé");
    }

    [Test]
    public void Un_document_copie_n_ecrase_jamais_un_autre()
    {
        string racine = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cipl_test_bail_" + Guid.NewGuid().ToString("N"));
        try
        {
            string source = System.IO.Path.Combine(racine, "source", "bail.pdf");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(source));
            System.IO.File.WriteAllText(source, "v1");
            string dossier = System.IO.Path.Combine(racine, "Bail");

            Assert.That(DossiersDonnees.CopierSansEcraser(source, dossier), Is.EqualTo("bail.pdf"));
            System.IO.File.WriteAllText(source, "v2");
            Assert.That(DossiersDonnees.CopierSansEcraser(source, dossier), Is.EqualTo("bail (2).pdf"), "même nom : copie à côté");
            Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(dossier, "bail.pdf")), Is.EqualTo("v1"), "l'original est intact");
            Assert.That(DossiersDonnees.CopierSansEcraser(System.IO.Path.Combine(dossier, "bail.pdf"), dossier), Is.EqualTo("bail.pdf"),
                "un fichier déjà rangé n'est pas recopié");
            Assert.That(System.IO.Directory.GetFiles(dossier), Has.Length.EqualTo(2));
        }
        finally { if (System.IO.Directory.Exists(racine)) System.IO.Directory.Delete(racine, true); }
    }

    [Test]
    public void Un_ancien_locataire_n_a_aucun_avenant()
    {
        var l = UnityEngine.JsonUtility.FromJson<Locataire>("{\"cheminBail\":\"\"}");
        Assert.That(l.avenants, Is.Not.Null.And.Empty);
    }

    [Test]
    public void Chaque_type_propose_a_un_libelle_en_francais()
    {
        foreach (var t in Locataire.TypesProposes)
            Assert.That(Locataire.LibelleBail(t), Does.StartWith("Bail").Or.StartWith("Convention"), t.ToString());
    }
}
