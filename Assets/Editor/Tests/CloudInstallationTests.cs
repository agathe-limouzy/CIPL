using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;

/// Étape 15 : versions, mise à jour proposée ou obligatoire, installeur et e-mail d'invitation.
public class CloudInstallationTests
{
    static RegleVersion.Fichier F(string version, string minimale = "1.0.0") =>
        new RegleVersion.Fichier { version = version, minimale = minimale };

    [Test]
    public void Les_versions_se_comparent_nombre_par_nombre()
    {
        Assert.That(RegleVersion.Comparer("1.0.10", "1.0.9"), Is.GreaterThan(0), "10 vient après 9");
        Assert.That(RegleVersion.Comparer("1.2", "1.2.0"), Is.EqualTo(0));
        Assert.That(RegleVersion.Comparer("1.9.9", "2.0.0"), Is.LessThan(0));
        Assert.That(RegleVersion.Valide("1.0.1") && RegleVersion.Valide("1.0"), Is.True);
        Assert.That(RegleVersion.Valide("1.0b") || RegleVersion.Valide("") || RegleVersion.Valide(null), Is.False);
    }

    [Test]
    public void Une_version_plus_recente_est_proposee_et_obligatoire_sous_la_minimale()
    {
        Assert.That(RegleVersion.Lire("1.0.0", F("1.0.0")), Is.EqualTo(RegleVersion.Etat.AJour));
        Assert.That(RegleVersion.Lire("1.0.2", F("1.0.1")), Is.EqualTo(RegleVersion.Etat.AJour), "plus récente que la publiée (build d'essai)");
        Assert.That(RegleVersion.Lire("1.0.0", F("1.0.1")), Is.EqualTo(RegleVersion.Etat.Proposee));
        Assert.That(RegleVersion.Lire("1.0.0", F("1.2.0", "1.1.0")), Is.EqualTo(RegleVersion.Etat.Obligatoire));
        Assert.That(RegleVersion.Lire("1.0.0", F("1.0.1", "1.1.0")), Is.EqualTo(RegleVersion.Etat.Proposee),
                    "minimale pas encore publiée : obliger ne mènerait qu'à une mise à jour sans fin");
        Assert.That(RegleVersion.Lire("1.0.0", null), Is.EqualTo(RegleVersion.Etat.AJour), "sans connexion : rien");
        Assert.That(RegleVersion.Lire("1.0.0", F("bientôt")), Is.EqualTo(RegleVersion.Etat.AJour));
    }

    [Test]
    public void Le_fichier_de_version_ecrit_se_relit()
    {
        var f = RegleVersion.Analyser(RegleVersion.Ecrire(new RegleVersion.Fichier
            { version = "1.0.1", minimale = "1.0.0", windows = MiseAJour.Publiee + MiseAJour.Installeur, mac = "" }));
        Assert.That(f.version + " " + f.minimale, Is.EqualTo("1.0.1 1.0.0"));
        Assert.That(f.windows, Does.EndWith("/releases/latest/download/CIPL-installation.exe"));
        Assert.That(RegleVersion.Analyser("pas du json"), Is.Null);
        Assert.That(RegleVersion.Analyser(""), Is.Null);
    }

    /// Retour du 26/10 : Windows avait bloqué l'installeur sans rien afficher.
    [Test]
    public void Une_mise_a_jour_non_installee_se_voit_au_lancement_suivant()
    {
        Assert.That(RegleVersion.MiseAJourEchouee("1.0.1", "1.0.0"), Is.True, "toujours l'ancienne version");
        Assert.That(RegleVersion.MiseAJourEchouee("1.0.1", "1.0.1"), Is.False, "installée");
        Assert.That(RegleVersion.MiseAJourEchouee("1.0.1", "1.0.2"), Is.False, "une plus récente depuis");
        Assert.That(RegleVersion.MiseAJourEchouee("", "1.0.0"), Is.False, "fichier vide : rien à dire");
        StringAssert.Contains("version 1.0.1 n'a pas pu s'installer : votre version 1.0.0 fonctionne toujours", RegleVersion.TexteEchec("1.0.1", "1.0.0"));
    }

    /// Dépôt privé jusqu'à la sortie (26/10) : les essais lisent un dossier du PC à la place de GitHub.
    [Test]
    public void Le_fichier_d_essai_donne_un_dossier_ou_une_adresse()
    {
        Assert.That(RegleVersion.Source(@"C:\CIPL_Git\Build\Installation"), Is.EqualTo("file:///C:/CIPL_Git/Build/Installation/"));
        Assert.That(RegleVersion.Source("\"C:\\CIPL_Git\\Build\\Installation\\\"\r\n"), Is.EqualTo("file:///C:/CIPL_Git/Build/Installation/"), "guillemets, barre et retour à la ligne");
        Assert.That(RegleVersion.Source("https://exemple.fr/cipl"), Is.EqualTo("https://exemple.fr/cipl/"));
        Assert.That(RegleVersion.Source("  "), Is.Null, "fichier vide : GitHub");
        Assert.That(RegleVersion.Source(null), Is.Null);
    }

    [Test]
    public void La_version_du_projet_est_publiable_et_l_identite_ne_bouge_plus()
    {
        string v = PlayerSettings.bundleVersion;
        Assert.That(RegleVersion.Valide(v), Is.True, v);
        Assert.That(RegleVersion.Comparer(v, ConstruireInstalleur.VersionMinimale), Is.GreaterThanOrEqualTo(0));
        // Décision du 26/10 : changer la société ou le produit déplacerait le dossier caché et les réglages
        // de chaque PC (connexion gardée, copie locale). Le nom visible, lui, peut changer.
        Assert.That(PlayerSettings.companyName + "/" + PlayerSettings.productName, Is.EqualTo("CIPL/CIPL"));
    }

    /// L'installeur et CIPL parlent des mêmes fichiers.
    [Test]
    public void L_installeur_correspond_a_la_mise_a_jour()
    {
        string iss = File.ReadAllText(Path.Combine("Installeur", "CIPL.iss"));
        StringAssert.Contains("OutputBaseFilename=" + Path.GetFileNameWithoutExtension(MiseAJour.Installeur), iss);
        StringAssert.Contains($"#define Exe \"{MiseAJour.Exe}\"", iss);
        StringAssert.Contains("PrivilegesRequired=lowest", iss);                        // mise à jour sans autorisation
        StringAssert.Contains("AppId={{8F3C2A71-5D4E-4B9A-A6C1-2E7D9F04B3C8}", iss);   // ne jamais le changer
        StringAssert.DoesNotContain("skipifsilent", iss);                               // CIPL se rouvre après une mise à jour
        StringAssert.EndsWith(MiseAJour.Installeur, MiseAJour.LienWindows);
    }

    /// Retour du 26/10 (AS8) : « GeneralFailure : Message could not be sent. » ne disait rien ; la cause
    /// intérieure, ajoutée au message, a montré le serveur mal orthographié (« stmp.gmail.com »).
    [Test]
    public void L_erreur_d_envoi_dit_sa_cause()
    {
        var e = new System.Net.Mail.SmtpException("Message could not be sent.",
                                                  new System.Net.Sockets.SocketException(11001));
        StringAssert.StartsWith(" — SocketException : ", EmailService.Cause(e));
        Assert.That(EmailService.Cause(new InvalidOperationException("seule")), Is.Empty);
    }

    // ── Étape 15b : la version Mac ─────────────────────────────────────────────

    [Test]
    public void L_adresse_d_un_fichier_reste_celle_de_Windows_et_s_echappe_sur_Mac()
    {
        Assert.That(Plateforme.UrlFichier(@"C:\CIPL\Démo CIPL\f.pdf", false), Is.EqualTo("file:///C:/CIPL/Démo CIPL/f.pdf"), "Windows inchangé");
        Assert.That(Plateforme.UrlFichier("/Users/agathe/Documents/CIPL/Démo CIPL/f.pdf", true),
                    Is.EqualTo("file:///Users/agathe/Documents/CIPL/D%C3%A9mo%20CIPL/f.pdf"));
    }

    [Test]
    public void Sur_Mac_les_PDF_viennent_de_Chrome_ou_d_Edge()
    {
        var mac = FacturePdfService.Navigateurs(true, "/Users/agathe");
        Assert.That(mac, Has.Some.EqualTo("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"));
        Assert.That(mac, Has.Some.EqualTo("/Users/agathe/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"));
        Assert.That(FacturePdfService.Navigateurs(false, @"C:\Users\agathe"), Has.All.EndsWith("msedge.exe"), "Windows inchangé");
    }

    [Test]
    public void Le_trousseau_du_Mac_garde_la_connexion_sous_le_service_CIPL()
    {
        Assert.That(Trousseau.ArgsEcrire("cipl_session", "jeton"),
                    Is.EqualTo(new[] { "add-generic-password", "-U", "-a", "cipl_session", "-s", "CIPL", "-w", "jeton" }));
        Assert.That(Trousseau.ArgsLire("cipl_session"), Is.EqualTo(new[] { "find-generic-password", "-a", "cipl_session", "-s", "CIPL", "-w" }));
        Assert.That(Trousseau.ArgsEffacer("cipl_session"), Is.EqualTo(new[] { "delete-generic-password", "-a", "cipl_session", "-s", "CIPL" }));
    }

    [Test]
    public void Sur_Mac_rien_n_est_propose_tant_que_la_version_Mac_n_est_pas_publiee()
    {
        var f = new RegleVersion.Fichier { version = "1.0.6", minimale = "1.0.0", mac = "" };
        Assert.That(RegleVersion.Lire("1.0.5", f), Is.EqualTo(RegleVersion.Etat.Proposee), "Windows");
        Assert.That(RegleVersion.Lire("1.0.5", f, mac: true), Is.EqualTo(RegleVersion.Etat.AJour));
        f.mac = MiseAJour.Publiee + MiseAJour.ZipMac;
        Assert.That(RegleVersion.Lire("1.0.5", f, mac: true), Is.EqualTo(RegleVersion.Etat.Proposee));
    }

    [Test]
    public void Les_Drives_du_Mac_sont_trouves_dans_CloudStorage()
    {
        string nuage = "/Users/agathe/Library/CloudStorage";
        var r = DossierEntreprise.Racines(_ => null, new[] { nuage + "/OneDrive-Personnel", nuage + "/GoogleDrive-agathe@cipl.fr" }, new string[0]);
        Assert.That(r, Has.Some.EqualTo(nuage + "/OneDrive-Personnel"));
        Assert.That(r, Has.Some.EqualTo(Path.Combine(nuage + "/GoogleDrive-agathe@cipl.fr", "Mon Drive")));
    }

    [Test]
    public void Le_zip_Mac_garde_le_droit_d_executer_l_application()
    {
        string dossier = Path.Combine(Path.GetTempPath(), "cipl_zip_essai");
        if (Directory.Exists(dossier)) Directory.Delete(dossier, true);
        string app = Path.Combine(dossier, "Test.app");
        Directory.CreateDirectory(Path.Combine(app, "Contents", "MacOS"));
        File.WriteAllText(Path.Combine(app, "Contents", "MacOS", "Test"), "programme");
        File.WriteAllText(Path.Combine(app, "Contents", "Info.plist"), "<plist/>");
        string zip = Path.Combine(dossier, "Test.zip");
        try
        {
            ConstruireInstalleur.ZipperApp(app, zip);
            using (var z = System.IO.Compression.ZipFile.OpenRead(zip))
            {
                Assert.That((uint)z.GetEntry("Test.app/Contents/MacOS/Test").ExternalAttributes >> 16, Is.EqualTo(0x81EDu), "rwxr-xr-x");
                Assert.That((uint)z.GetEntry("Test.app/Contents/Info.plist").ExternalAttributes >> 16, Is.EqualTo(0x81A4u), "rw-r--r--");
            }
            // « Fait sous Unix » : la 1re fiche du répertoire central (position lue à la fin du zip).
            var b = File.ReadAllBytes(zip);
            int fin = b.Length - 22, p = (int)BitConverter.ToUInt32(b, fin + 16);
            Assert.That(BitConverter.ToUInt32(b, fin), Is.EqualTo(0x06054B50u));
            Assert.That(b[p + 5], Is.EqualTo(3));
        }
        finally { Directory.Delete(dossier, true); }
    }

    [Test]
    public void L_identifiant_de_l_application_Mac_ne_bouge_plus()
    {
        Assert.That(PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone), Is.EqualTo("fr.cipl.cipl"));
        string iss = File.ReadAllText(Path.Combine("Installeur", "CIPL.iss"));
        StringAssert.DoesNotContain("URL Protocol", iss, "liens cipl:// retirés le 26/10");
        StringAssert.Contains("Flags: deletekey", iss);
    }

    [Test]
    public void L_email_d_invitation_donne_le_lien_d_installation()
    {
        string corps = UtilisateursCloud.CorpsEmail("Agathe", "Démo CIPL", "K7F2-9QXD", new DateTime(2026, 10, 26));
        StringAssert.Contains(MiseAJour.LienWindows, corps);
        StringAssert.Contains("Exécuter quand même", corps);
        StringAssert.DoesNotContain("demandez le lien", corps);
        StringAssert.DoesNotContain("Sur Mac", corps, "pas de lien Mac tant que la version Mac n'est pas publiée");
    }
}
