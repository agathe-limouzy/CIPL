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

    // ── Résiliation triennale ────────────────────────────────────────────────────

    [Test]
    public void Les_echeances_triennales_respectent_la_periode_ferme()
    {
        var debut = new DateTime(2020, 6, 1);
        Assert.That(Locataire.EcheancesTriennales(debut, 9, 0),
            Is.EqualTo(new[] { new DateTime(2023, 5, 31), new DateTime(2026, 5, 31) }), "3/6/9 : la fin (9 ans) relève du renouvellement");
        Assert.That(Locataire.EcheancesTriennales(debut, 9, 6), Is.EqualTo(new[] { new DateTime(2026, 5, 31) }), "6 ans fermes");
        Assert.That(Locataire.EcheancesTriennales(debut, 9, 9), Is.Empty, "9 ans fermes : aucune sortie avant la fin");
        Assert.That(Locataire.EcheancesTriennales(debut, 10, 9), Is.EqualTo(new[] { new DateTime(2029, 5, 31) }), "10 ans dont 9 fermes");
    }

    /// L'alerte s'ouvre 3 mois avant la date limite du congé (6 mois avant l'échéance)
    /// et se ferme à cette date.
    [Test]
    public void L_alerte_de_resiliation_couvre_le_delai_de_conge()
    {
        var loc = new Locataire { Name = "Sephora", typeDeBail = BailType.BailCommercial369,
                                  dateDebutBailISO = "2020-06-01", dateFinBailISO = "2029-05-31" };
        Assert.That(Locataire.ResiliationProche(loc, new DateTime(2022, 8, 1), out _, out _), Is.False, "trop tôt");
        Assert.That(Locataire.ResiliationProche(loc, new DateTime(2022, 9, 15), out var echeance, out var limite), Is.True);
        Assert.That(echeance, Is.EqualTo(new DateTime(2023, 5, 31)));
        Assert.That(limite, Is.EqualTo(new DateTime(2022, 11, 30)), "congé 6 mois avant");
        Assert.That(Locataire.ResiliationProche(loc, new DateTime(2022, 11, 30), out _, out _), Is.True, "dernier jour");
        Assert.That(Locataire.ResiliationProche(loc, new DateTime(2022, 12, 1), out _, out _), Is.False, "délai passé");

        loc.anneesFermes = 6;
        Assert.That(Locataire.ResiliationProche(loc, new DateTime(2022, 9, 15), out _, out _), Is.False, "période ferme");
        loc.typeDeBail = BailType.BailProfessionnel;
        loc.anneesFermes = 0;
        Assert.That(Locataire.ResiliationProche(loc, new DateTime(2022, 9, 15), out _, out _), Is.False, "pas un bail commercial");
    }

    // ── Fin de bail : renouvellement, congé, tacite prolongation ────────────────

    static Locataire BailQuiFinitLe(string finISO, BailType type = BailType.BailCommercial369)
        => new Locataire { Name = "Sephora", typeDeBail = type, dateDebutBailISO = "2024-05-01", dateFinBailISO = finISO };

    /// 9 mois avant la fin : 3 mois avant la limite du congé (6 mois avant la fin).
    [Test]
    public void Le_renouvellement_s_annonce_neuf_mois_avant_la_fin()
    {
        var loc = BailQuiFinitLe("2027-06-30");
        Assert.That(Locataire.RenouvellementProche(loc, new DateTime(2026, 9, 29), out _), Is.False, "9 mois et 1 jour avant");
        Assert.That(Locataire.RenouvellementProche(loc, new DateTime(2026, 9, 30), out _), Is.True, "9 mois avant");
        Assert.That(Locataire.RenouvellementProche(loc, new DateTime(2027, 7, 5), out int jours), Is.True, "expiré");
        Assert.That(jours, Is.LessThan(0));
    }

    [Test]
    public void Le_conge_pour_la_fin_du_bail_se_donne_six_mois_avant()
    {
        var loc = BailQuiFinitLe("2027-06-30");
        Assert.That(Locataire.CongeFinDeBailPossible(loc, new DateTime(2026, 10, 15), out var fin, out var limite), Is.True);
        Assert.That(fin, Is.EqualTo(new DateTime(2027, 6, 30)));
        Assert.That(limite, Is.EqualTo(new DateTime(2026, 12, 30)));
        Assert.That(Locataire.CongeFinDeBailPossible(loc, new DateTime(2026, 12, 31), out _, out _), Is.False, "délai passé");
        Assert.That(Locataire.CongeFinDeBailPossible(BailQuiFinitLe("2027-06-30", BailType.BailProfessionnel),
            new DateTime(2026, 10, 15), out _, out _), Is.False, "pas un bail commercial");
    }

    /// Tacite prolongation : 6 mois de préavis, pour le dernier jour d'un trimestre civil.
    [Test]
    public void En_tacite_prolongation_la_sortie_tombe_en_fin_de_trimestre()
    {
        Assert.That(Locataire.SortieTaciteAuPlusTot(new DateTime(2026, 9, 28)), Is.EqualTo(new DateTime(2027, 3, 31)));
        Assert.That(Locataire.SortieTaciteAuPlusTot(new DateTime(2026, 10, 1)), Is.EqualTo(new DateTime(2027, 6, 30)));
        Assert.That(Locataire.SortieTaciteAuPlusTot(new DateTime(2026, 12, 31)), Is.EqualTo(new DateTime(2027, 6, 30)),
            "30/06 tombe pile à 6 mois");
    }

    [Test]
    public void Le_texte_de_fin_de_bail_donne_les_dates_qui_comptent()
    {
        var loc = BailQuiFinitLe("2027-06-30");
        Assert.That(Locataire.TexteFinDeBail(loc, new DateTime(2026, 10, 15)),
            Is.EqualTo("À renouveler — fin le 30/06/2027, congé jusqu'au 30/12/2026"));
        Assert.That(Locataire.TexteFinDeBail(loc, new DateTime(2027, 2, 1)), Is.EqualTo("À renouveler — fin le 30/06/2027"),
            "délai de congé passé");
        Assert.That(Locataire.TexteFinDeBail(loc, new DateTime(2027, 7, 5)), Does.StartWith("Bail expiré — congé possible à tout moment")
            .And.EndsWith("31/03/2028"));
        Assert.That(Locataire.TexteFinDeBail(BailQuiFinitLe("2027-06-30", BailType.BailCivil), new DateTime(2027, 7, 5)),
            Is.EqualTo("Bail expiré — à renouveler"), "hors bail commercial : pas de règle de congé");
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
