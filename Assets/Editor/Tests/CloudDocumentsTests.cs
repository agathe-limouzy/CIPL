using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using A = SyncDocuments.Action;

/// Étape 6a : ce qu'un passage de synchronisation des documents fait, cas par cas.
public class CloudDocumentsTests
{
    const string F = "Batiment/rivoli/Volteo/Facture/F-2026-001.pdf";

    static Dictionary<string, string> D(params (string c, string e)[] l)
    {
        var d = new Dictionary<string, string>();
        foreach (var (c, e) in l) d[c] = e;
        return d;
    }

    static Dictionary<string, SyncDocuments.Distant> S(string empreinte, bool supprime = false) =>
        new Dictionary<string, SyncDocuments.Distant> { { F, new SyncDocuments.Distant { Empreinte = empreinte, Supprime = supprime } } };

    static List<(A, string)> Plan(Dictionary<string, string> local, Dictionary<string, string> manifeste,
                                  Dictionary<string, SyncDocuments.Distant> serveur, bool telecharger = true) =>
        SyncDocuments.Plan(local, manifeste, serveur, telecharger);

    static readonly Dictionary<string, SyncDocuments.Distant> Vide = new Dictionary<string, SyncDocuments.Distant>();

    [Test]
    public void Seuls_les_dossiers_des_batiments_et_le_logo_sont_partages()
    {
        Assert.That(SyncDocuments.Partage(F), Is.True);
        Assert.That(SyncDocuments.Partage("Logo/logo.png"), Is.True);
        Assert.That(SyncDocuments.Partage("Backups/2026-10-14/batiment_1.json"), Is.False);
        Assert.That(SyncDocuments.Partage("batiments/batiment_1.json"), Is.False);
        Assert.That(SyncDocuments.Partage("corbeille_documents/2026-10-14/" + F), Is.False);
        Assert.That(SyncDocuments.Partage("Batiment/rivoli/Volteo/Bail/~$bail.docx"), Is.False, "fichier temporaire d'Office");
        Assert.That(SyncDocuments.Partage(F + ".cipl-recu"), Is.False, "réception en cours");
    }

    [Test]
    public void L_empreinte_est_le_sha256_du_contenu()
    {
        Assert.That(SyncDocuments.Empreinte(Encoding.UTF8.GetBytes("abc")),
                    Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
    }

    [Test]
    public void Un_document_cree_ici_part_et_un_document_identique_est_juste_note()
    {
        Assert.That(Plan(D((F, "e1")), D(), Vide), Is.EqualTo(new[] { (A.Envoyer, F) }), "facture émise ici");
        Assert.That(Plan(D((F, "e1")), D(), S("e1")), Is.EqualTo(new[] { (A.Noter, F) }), "déjà au cloud (import, Drive)");
        Assert.That(Plan(D((F, "e1")), D((F, "e1")), S("e1")), Is.Empty, "rien n'a bougé");
    }

    [Test]
    public void Ce_qu_un_collegue_ajoute_modifie_ou_supprime_arrive_ici()
    {
        Assert.That(Plan(D(), D(), S("e1")), Is.EqualTo(new[] { (A.Telecharger, F) }), "ajouté par Marie");
        Assert.That(Plan(D((F, "e1")), D((F, "e1")), S("e2")), Is.EqualTo(new[] { (A.Telecharger, F) }), "modifié par Marie");
        Assert.That(Plan(D((F, "e1")), D((F, "e1")), S("e1", supprime: true)), Is.EqualTo(new[] { (A.Corbeille, F) }), "supprimé par Marie");
    }

    [Test]
    public void Ce_que_je_modifie_ou_supprime_part_et_l_emporte()
    {
        Assert.That(Plan(D((F, "e2")), D((F, "e1")), S("e1")), Is.EqualTo(new[] { (A.Envoyer, F) }), "modifié ici");
        Assert.That(Plan(D(), D((F, "e1")), S("e1")), Is.EqualTo(new[] { (A.SupprimerServeur, F) }), "supprimé ici");
        Assert.That(Plan(D((F, "e2")), D((F, "e1")), S("e1", supprime: true)), Is.EqualTo(new[] { (A.Envoyer, F) }),
                    "modifié ici pendant que Marie le supprimait : il revient");
    }

    // ── Étape 6b : le dossier partagé sur ce PC ────────────────────────────────

    [Test]
    public void On_cherche_dans_OneDrive_et_Google_Drive_sans_doublon()
    {
        var env = new Dictionary<string, string>
        {
            { "OneDriveCommercial", @"C:\Users\paul\OneDrive - SCI Rivoli" },
            { "OneDrive", @"C:\Users\paul\OneDrive - SCI Rivoli\" },   // même dossier, avec une barre finale
        };
        var racines = DossierEntreprise.Racines(v => env.TryGetValue(v, out var p) ? p : null,
            new[] { @"C:\Users\paul\Documents", @"C:\Users\paul\OneDrive", @"C:\Users\paul\Google Drive" },
            new[] { @"G:\" });
        Assert.That(racines, Is.EquivalentTo(new[]
        {
            @"C:\Users\paul\OneDrive - SCI Rivoli", @"C:\Users\paul\OneDrive", @"C:\Users\paul\Google Drive",
            @"G:\Mon Drive", @"G:\My Drive", @"G:\Drives partagés", @"G:\Shared drives",
        }));
    }

    [Test]
    public void Le_repere_de_l_entreprise_designe_son_dossier_et_un_autre_est_refuse()
    {
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cipl_test_drive_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            string rivoli = System.IO.Path.Combine(tmp, "Societe", "CIPL", "SCI Rivoli");
            System.IO.Directory.CreateDirectory(rivoli);
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(tmp, "Vide"));
            ImportLocal.EcrireRepere(rivoli, new ImportLocal.Repere { id = "e1", nom = "SCI Rivoli" }, out _);

            Assert.That(DossierEntreprise.Chercher("e1", new[] { tmp }), Is.EqualTo(rivoli));
            Assert.That(DossierEntreprise.Chercher("e2", new[] { tmp }), Is.Null);
            Assert.That(DossierEntreprise.Refus(rivoli, "e1", "SCI Rivoli"), Is.Null);
            StringAssert.Contains("« SCI Rivoli », pas de « SCI Autre »", DossierEntreprise.Refus(rivoli, "e2", "SCI Autre"));
            StringAssert.Contains("absent", DossierEntreprise.Refus(System.IO.Path.Combine(tmp, "Vide"), "e1", "SCI Rivoli"));
        }
        finally { if (System.IO.Directory.Exists(tmp)) System.IO.Directory.Delete(tmp, true); }
    }

    [Test]
    public void Les_Drives_sont_fouilles_ensemble_niveau_par_niveau()
    {
        // Retour du 15/10 : le repère à 2 niveaux dans le 3e Drive attendait la fouille complète des deux premiers.
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cipl_test_niveaux_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            string profond = System.IO.Path.Combine(tmp, "OneDrive", "a", "b", "Demo");
            string proche = System.IO.Path.Combine(tmp, "MonDrive", "Demo");
            System.IO.Directory.CreateDirectory(profond);
            System.IO.Directory.CreateDirectory(proche);
            ImportLocal.EcrireRepere(profond, new ImportLocal.Repere { id = "e1", nom = "Démo" }, out _);
            ImportLocal.EcrireRepere(proche, new ImportLocal.Repere { id = "e1", nom = "Démo" }, out _);

            var racines = new[] { System.IO.Path.Combine(tmp, "OneDrive"), System.IO.Path.Combine(tmp, "MonDrive") };
            Assert.That(DossierEntreprise.Chercher("e1", racines), Is.EqualTo(proche));
        }
        finally { if (System.IO.Directory.Exists(tmp)) System.IO.Directory.Delete(tmp, true); }
    }

    [Test]
    public void Le_dossier_designe_recoit_un_sous_dossier_sauf_s_il_est_deja_celui_de_l_entreprise()
    {
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cipl_test_cible_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            string demo = System.IO.Path.Combine(tmp, "Démo CIPL");
            System.IO.Directory.CreateDirectory(demo);
            ImportLocal.EcrireRepere(demo, new ImportLocal.Repere { id = "e1", nom = "Démo CIPL" }, out _);

            Assert.That(DossierEntreprise.Cible(tmp, "e1", "Démo CIPL"), Is.EqualTo(demo));     // parent → sous-dossier
            Assert.That(DossierEntreprise.Cible(demo, "e1", "Démo CIPL"), Is.EqualTo(demo));    // déjà le sien
            Assert.That(DossierEntreprise.Cible(demo, "e2", "Autre"), Is.EqualTo(System.IO.Path.Combine(demo, "Autre")));
            Assert.That(DossierEntreprise.Meme(demo + "\\", demo.ToUpperInvariant()), Is.True);
            Assert.That(DossierEntreprise.Meme(demo, tmp), Is.False);
        }
        finally { if (System.IO.Directory.Exists(tmp)) System.IO.Directory.Delete(tmp, true); }
    }

    [Test]
    public void Passer_au_Drive_recopie_documents_et_reglages_sans_rien_ecraser()
    {
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cipl_test_recopie_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            string a = System.IO.Path.Combine(tmp, "a"), b = System.IO.Path.Combine(tmp, "b");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(a, "Batiment", "rivoli", "Facture"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(a, "Batiment", "rivoli", "Facture", "F-001.pdf"), "pdf");
            System.IO.File.WriteAllText(System.IO.Path.Combine(a, "reglage.json"), "{}");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(a, "Backups"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(a, "Backups", "vieux.json"), "{}");

            Assert.That(DossierEntreprise.Recopier(a, b), Is.EqualTo(1));
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(b, "Batiment", "rivoli", "Facture", "F-001.pdf")), Is.True);
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(b, "reglage.json")), Is.True);
            Assert.That(System.IO.Directory.Exists(System.IO.Path.Combine(b, "Backups")), Is.False, "les sauvegardes ne suivent pas");
            Assert.That(DossierEntreprise.Recopier(a, b), Is.EqualTo(0), "rien n'est écrasé");
        }
        finally { if (System.IO.Directory.Exists(tmp)) System.IO.Directory.Delete(tmp, true); }
    }

    [Test]
    public void Un_document_se_repere_par_son_chemin_dans_le_dossier_de_l_entreprise()
    {
        Assert.That(DocumentsCloud.Relatif(@"C:\Docs\SCI Rivoli\", @"C:\Docs\SCI Rivoli\Batiment\rivoli\Bail\bail.pdf"),
                    Is.EqualTo("Batiment/rivoli/Bail/bail.pdf"));
        Assert.That(DocumentsCloud.Relatif(@"C:\Docs\SCI Rivoli", @"C:\Docs\Autre\bail.pdf"), Is.Null);
    }

    [Test]
    public void Une_copie_jamais_synchronisee_cede_au_cloud_et_rien_ne_descend_en_mode_drive()
    {
        Assert.That(Plan(D((F, "vieux")), D(), S("e1")), Is.EqualTo(new[] { (A.Telecharger, F) }),
                    "copie d'ici inconnue du manifeste : le cloud fait foi (elle part en corbeille)");
        Assert.That(Plan(D(), D(), S("e1"), telecharger: false), Is.Empty, "Drive : il recopie lui-même");
        Assert.That(Plan(D((F, "e1")), D((F, "e1")), S("e1", supprime: true), telecharger: false), Is.Empty);
        Assert.That(Plan(D(), D((F, "e1")), S("e1"), telecharger: false), Is.EqualTo(new[] { (A.SupprimerServeur, F) }),
                    "une suppression faite ici part quand même");
    }
}
