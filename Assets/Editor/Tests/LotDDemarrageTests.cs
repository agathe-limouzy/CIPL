using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot D « données et cloud » (11/11/2026), groupe « démarrage » : D2-03 (fermeture), D2-04, D2-09, D2-12, D2-13, D2-19,
/// D2-20, D2-21, D2-24, D8-11. D8-17 est dans CloudInstallationTests. Règle pure d'abord, texte du code à défaut.
public class LotDDemarrageTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));

    /// Le corps d'une méthode, de `debut` à sa fermeture (motif valable en LF comme en CRLF).
    static string Corps(string src, string debut)
    {
        int i = src.IndexOf(debut, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), debut);
        return src.Substring(i, src.IndexOf("\n    }", i, StringComparison.Ordinal) - i);
    }

    static int Fois(string src, string fragment) => src.Split(new[] { fragment }, StringSplitOptions.None).Length - 1;

    static int Place(string src, string fragment)
    {
        int i = src.IndexOf(fragment, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), fragment);
        return i;
    }

    static string Temp(string nom)
    {
        string d = Path.Combine(Path.GetTempPath(), "cipl_lotd_demarrage_" + nom);
        if (Directory.Exists(d)) Directory.Delete(d, true);
        Directory.CreateDirectory(d);
        return d;
    }

    // ── D2-19 : deux entreprises de même nom partageaient le même dossier local ──

    [Test]
    public void D2_19_deux_entreprises_de_meme_nom_n_ont_pas_le_meme_dossier_local()
    {
        var reperes = new Dictionary<string, ImportLocal.Repere>();
        Func<string, ImportLocal.Repere> lire = d => reperes.TryGetValue(d, out var r) ? r : null;
        string parent = Path.Combine("Documents", "CIPL"), nom = "Patrimoine de Marie Dupont";
        string un = Path.Combine(parent, nom), deux = Path.Combine(parent, nom + " (2)"), trois = Path.Combine(parent, nom + " (3)");

        Assert.That(DossierEntreprise.DossierLibre(parent, nom, "b", lire), Is.EqualTo(un), "pas de dossier, ou dossier sans repère (d'avant la correction)");
        reperes[un] = new ImportLocal.Repere { id = "b" };
        Assert.That(DossierEntreprise.DossierLibre(parent, nom, "b", lire), Is.EqualTo(un), "le repère de la même entreprise : c'est le sien");
        reperes[un] = new ImportLocal.Repere { id = "a" };
        Assert.That(DossierEntreprise.DossierLibre(parent, nom, "b", lire), Is.EqualTo(deux), "le repère d'une autre entreprise : « (2) »");
        reperes[deux] = new ImportLocal.Repere { id = "c" };
        Assert.That(DossierEntreprise.DossierLibre(parent, nom, "b", lire), Is.EqualTo(trois), "« (2) » pris par une troisième : « (3) »");
        reperes[deux] = new ImportLocal.Repere { id = "b" };
        Assert.That(DossierEntreprise.DossierLibre(parent, nom, "b", lire), Is.EqualTo(deux), "« (2) » est déjà le sien : on y reste");
    }

    /// Le repère déposé dans le dossier local d'un PC dit à qui est le dossier, mais ne le fait pas passer pour le dossier partagé
    /// (Documents est souvent dans OneDrive : la recherche du Drive le trouverait), et il ne part pas au cloud.
    [Test]
    public void D2_19_le_repere_d_un_dossier_local_n_est_ni_le_drive_partage_ni_un_document()
    {
        string tmp = Temp("repere");
        try
        {
            string local = Path.Combine(tmp, "OneDrive", "Documents", "CIPL", "SCI Rivoli"), partage = Path.Combine(tmp, "Drive", "Commun", "SCI Rivoli");
            Directory.CreateDirectory(local);
            Directory.CreateDirectory(partage);
            Assert.That(ImportLocal.EcrireRepere(local, new ImportLocal.Repere { id = "e1", nom = "SCI Rivoli", complet = true, local = true }, out _), Is.True);
            Assert.That(ImportLocal.LireRepere(local).local, Is.True, "le champ se relit");
            Assert.That(DossierEntreprise.Chercher("e1", new[] { tmp }), Is.Null, "la copie de ce PC n'est pas le Drive partagé");
            StringAssert.Contains("pas le dossier partagé", DossierEntreprise.Refus(local, "e1", "SCI Rivoli"), "choisi à la main : refusé aussi");
            StringAssert.Contains("« SCI Rivoli », pas de « SCI Autre »", DossierEntreprise.Refus(local, "e2", "SCI Autre"));

            ImportLocal.EcrireRepere(partage, new ImportLocal.Repere { id = "e1", nom = "SCI Rivoli", complet = true }, out _);
            Assert.That(DossierEntreprise.Chercher("e1", new[] { tmp }), Is.EqualTo(partage), "le vrai dossier partagé est toujours trouvé");
            Assert.That(DossierEntreprise.Refus(partage, "e1", "SCI Rivoli"), Is.Null);
            Assert.That(DossierEntreprise.DossierLibre(Path.GetDirectoryName(local), "SCI Rivoli", "e2", ImportLocal.LireRepere),
                        Is.EqualTo(Path.Combine(Path.GetDirectoryName(local), "SCI Rivoli (2)")), "sur le disque : le dossier marqué n'est pas repris par une homonyme");
        }
        finally { Directory.Delete(tmp, true); }

        Assert.That(SyncDocuments.Partage(ImportLocal.NomRepere), Is.False, "à la racine du dossier : jamais envoyé au cloud");
        string src = Src("Cloud/DemarrageCloud.cs");
        string depot = Corps(src, "static void DeposerRepere(EntrepriseCloud e, string dossier)");
        StringAssert.Contains("if (r != null)", depot, "un repère déjà là (import, Drive partagé, autre entreprise) n'est jamais remplacé");
        StringAssert.Contains("local = true", depot);
        StringAssert.Contains("DeposerRepere(e, dossier);", Corps(src, "static async Task<(string dossier, bool telecharger)> DossierDocuments(EntrepriseCloud e)"),
                              "à la création du dossier, et à l'ouverture s'il manque");
        StringAssert.Contains("DossierEntreprise.DossierLibre(", Corps(src, "public static string DossierLocal(EntrepriseCloud e)"));
        string docs = Corps(src, "static async Task<(string dossier, bool telecharger)> DossierDocuments(EntrepriseCloud e)");
        Assert.That(Place(docs, "if (autre != null && autre.id != e.id) { refus = "), Is.LessThan(Place(docs, "Retenir(e.id, local, DossierEntreprise.HorsDrive);")),
                    "mode Drive, « Continuer sans le dossier » : pas le dossier local d'une homonyme");
    }

    // ── D2-12 : le dossier retenu sur ce PC manque : on demande, on ne le remplace plus en silence ──

    [Test]
    public void D2_12_un_dossier_retenu_introuvable_est_demande_pas_remplace()
    {
        const string d = @"D:\CIPL\SCI Rivoli";
        Assert.That(DossierEntreprise.DecisionLocal("", false, ""), Is.EqualTo(DossierEntreprise.Local.Nouveau), "jamais ouverte sur ce PC");
        Assert.That(DossierEntreprise.DecisionLocal(d, true, ""), Is.EqualTo(DossierEntreprise.Local.Garder));
        Assert.That(DossierEntreprise.DecisionLocal(d, false, ""), Is.EqualTo(DossierEntreprise.Local.Demander), "disque externe pas monté");
        Assert.That(DossierEntreprise.DecisionLocal(d, false, DossierEntreprise.HorsDrive), Is.EqualTo(DossierEntreprise.Local.Demander));
        Assert.That(DossierEntreprise.DecisionLocal(d, true, DossierEntreprise.HorsDrive), Is.EqualTo(DossierEntreprise.Local.Garder));
        Assert.That(DossierEntreprise.DecisionLocal(d, true, DossierEntreprise.DriveAuto), Is.EqualTo(DossierEntreprise.Local.Nouveau), "retour « Dans CIPL » : on quitte le Drive");
        Assert.That(DossierEntreprise.DecisionLocal(d, false, DossierEntreprise.DriveMain), Is.EqualTo(DossierEntreprise.Local.Nouveau));

        string dossier = Corps(Src("Cloud/DemarrageCloud.cs"), "static async Task<(string dossier, bool telecharger)> DossierDocuments(EntrepriseCloud e)");
        Assert.That(Place(dossier, "await EcransCloud.DossierLocalIntrouvable(Canvas, e.nom, dossier, essai)"), Is.LessThan(Place(dossier, "Retenir(e.id, dossier, \"\");")),
                    "le nouveau dossier n'est retenu qu'après la réponse");
        string ecran = Corps(Src("Cloud/EcransCloud.cs"), "public static Task<bool> DossierLocalIntrouvable(");
        StringAssert.Contains("$\"Le dossier des documents ({chemin}) est introuvable.\"", ecran);
        StringAssert.Contains("\"Utiliser Documents\\\\CIPL\"", ecran);
        StringAssert.Contains("\"Réessayer\"", ecran);
        // Retour de BW18 (14/11) : après un « Réessayer » sans effet, la fenêtre le dit.
        Assert.That(EcransCloud.TexteToujoursIntrouvable(null), Is.Null, "premier affichage : pas de ligne");
        Assert.That(EcransCloud.TexteToujoursIntrouvable(new DateTime(2026, 11, 14, 18, 42, 5)), Is.EqualTo("Toujours introuvable (essai à 18:42:05)."));
        StringAssert.Contains("if (reessayer) essai = DateTime.Now;", dossier);
    }

    // ── D2-13 : tout refus des Réglages rend la copie du serveur à ce PC ──

    [Test]
    public void D2_13_un_refus_des_reglages_rend_toujours_la_copie_du_serveur()
    {
        Assert.That(ReglagesCloud.Motif("admin", null), Is.EqualTo("réservé à un administrateur"), "jamais le mot brut");
        Assert.That(ReglagesCloud.Motif("erreur", "HTTP 500"), Is.EqualTo("HTTP 500"));
        Assert.That(ReglagesCloud.Motif("inconnu", ""), Is.EqualTo("inconnu"));
        Assert.That(ReglagesCloud.Motif(null, null), Is.EqualTo("réponse illisible du serveur"), "jamais un message qui finit par « : »");
        Assert.That(ReglagesCloud.TexteNonEnregistres("HTTP 500."), Is.EqualTo("Réglages non enregistrés sur le serveur : HTTP 500. Vos modifications " +
                    "ne sont pas gardées : les Réglages du serveur sont repris dès que la fenêtre Réglages est fermée."));

        string envoi = Corps(Src("Cloud/ReglagesCloud.cs"), "static async Task EnvoyerApres(");
        Assert.That(Fois(envoi, "ARelire(null);"), Is.EqualTo(3), "« reseau », tout autre refus, et une exception");
        Assert.That(Fois(envoi, "RelireMaintenant(null)"), Is.EqualTo(3), "« version », « application » et « admin » relisent au OK");
    }

    // ── D2-21 : retiré ou déconnecté, le PC ne garde ni les Réglages ni les secrets de l'entreprise ──

    [Test]
    public void D2_21_le_retrait_et_la_deconnexion_effacent_reglages_et_secrets_du_PC()
    {
        string pc = Temp("reglages");
        try
        {
            Assert.That(ReglageService.FichiersDe(pc, "e1").Select(f => Path.GetFileName(f)), Is.EqualTo(new[]
                { "cipl_reglages_e1.json", "cipl_reglages_e1.json.bak", "cipl_secrets_e1.dat", "cipl_secrets_e1.dat.bak" }));
            Assert.That(ReglageService.EntreprisesCopiees(new[] { Path.Combine(pc, "cipl_reglages_a.json"), "cipl_secrets_a.dat.bak", "cipl_secrets_b.dat",
                                                                   "cipl_app_secrets.dat", "cipl_copie_c.json", "cipl_session.dat" }),
                        Is.EqualTo(new List<string> { "a", "b" }), "ni l'adresse du cloud, ni la copie hors ligne, ni la session");

            string[] gardes = { "cipl_app_secrets.dat", "cipl_copie_e1.json", "reglage.json", "pennylane_secrets.dat" };   // les deux derniers : mode sans cloud
            foreach (var f in ReglageService.FichiersDe(pc, "e1").Concat(ReglageService.FichiersDe(pc, "e2")).Concat(gardes.Select(g => Path.Combine(pc, g))))
                File.WriteAllText(f, "x");

            ReglageService.EffacerCopie(pc, "e1");   // retiré de e1 par un admin
            Assert.That(ReglageService.FichiersDe(pc, "e1").Any(File.Exists), Is.False, "Réglages, secrets et leurs .bak");
            Assert.That(ReglageService.FichiersDe(pc, "e2").All(File.Exists), Is.True, "les autres entreprises restent");

            ReglageService.EffacerCopie(pc, null);   // « Se déconnecter » : toutes
            Assert.That(ReglageService.FichiersDe(pc, "e2").Any(File.Exists), Is.False);
            Assert.That(gardes.All(g => File.Exists(Path.Combine(pc, g))), Is.True, "jamais les Réglages du mode sans cloud ni l'adresse du cloud");
        }
        finally { Directory.Delete(pc, true); }

        string src = Src("Cloud/DemarrageCloud.cs");
        StringAssert.Contains("ReglageService.EffacerCopie(e.id);", Corps(src, "public static async void Exclu()"), "seulement cette entreprise");
        StringAssert.Contains("ReglageService.EffacerCopie();", Corps(src, "public static async void SeDeconnecter()"), "toutes");
        StringAssert.Contains("if (lu == null) ARelire(null);", Corps(Src("Cloud/ReglagesCloud.cs"), "public static async Task Synchroniser(EntrepriseCloud e)"),
                              "copie effacée et serveur illisible à l'ouverture : on réessaie");
    }

    // ── D8-11 : l'installeur téléchargé est contrôlé avant d'être lancé, et effacé ensuite ──

    [Test]
    public void D8_11_un_fichier_qui_n_est_pas_l_installeur_publie_est_refuse()
    {
        byte[] installeur = Encoding.UTF8.GetBytes("MZ installeur de CIPL 1.0.7"), page = Encoding.UTF8.GetBytes("<html>Portail Wi-Fi</html>");
        string attendue = RegleVersion.Empreinte(new MemoryStream(installeur));
        Assert.That(attendue, Is.EqualTo(SyncDocuments.Empreinte(installeur)), "SHA-256 en hexadécimal, comme les documents");
        Assert.That(RegleVersion.InstalleurValide(RegleVersion.Empreinte(new MemoryStream(installeur)), attendue), Is.True, "le bon fichier");
        Assert.That(RegleVersion.InstalleurValide(attendue, " " + attendue.ToUpperInvariant() + "\n"), Is.True, "casse et espaces de version.json");
        Assert.That(RegleVersion.InstalleurValide(RegleVersion.Empreinte(new MemoryStream(page)), attendue), Is.False, "une page HTML à la place");
        Assert.That(RegleVersion.InstalleurValide(null, attendue), Is.False, "fichier illisible");

        var publie = RegleVersion.Analyser(RegleVersion.Ecrire(new RegleVersion.Fichier { version = "1.0.7", minimale = "1.0.0", sha256 = attendue }));
        Assert.That(publie.sha256, Is.EqualTo(attendue), "le champ s'écrit et se relit");
        var ancien = RegleVersion.Analyser("{\"version\":\"1.0.6\",\"minimale\":\"1.0.0\",\"windows\":\"\",\"mac\":\"\"}");
        Assert.That(RegleVersion.InstalleurValide(RegleVersion.Empreinte(new MemoryStream(page)), ancien.sha256), Is.True,
                    "version.json publié avant ce contrôle : accepté, comme avant");
        Assert.That(RegleVersion.TexteInstalleurRefuse, Is.EqualTo("Le fichier téléchargé n'est pas l'installeur attendu. Réessayez plus tard."));

        // Les installeurs téléchargés (un par version) sont effacés au lancement suivant : le motif ne prend qu'eux.
        string tmp = Temp("installeurs");
        try
        {
            foreach (var f in new[] { RegleVersion.NomInstalleur("1.0.6"), RegleVersion.NomInstalleur("1.0.7"), MiseAJour.Installeur, "autre.exe" })
                File.WriteAllText(Path.Combine(tmp, f), "x");
            Assert.That(Directory.GetFiles(tmp, RegleVersion.MotifInstalleurs).Select(f => Path.GetFileName(f)).OrderBy(n => n),
                        Is.EqualTo(new[] { "CIPL-installation-1.0.6.exe", "CIPL-installation-1.0.7.exe" }));
        }
        finally { Directory.Delete(tmp, true); }

        string maj = File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Cloud", "MiseAJour.cs"));
        string telecharger = Corps(maj, "IEnumerator Telecharger(Action<string> afficher)");
        Assert.That(Place(telecharger, "if (!InstalleurAttendu(fichier, Derniere.sha256))"), Is.LessThan(Place(telecharger, "Process.Start(")),
                    "contrôlé avant d'être lancé");
        StringAssert.Contains("Directory.GetFiles(Path.GetTempPath(), RegleVersion.MotifInstalleurs)", Corps(maj, "IEnumerator Bilan()"));
        StringAssert.Contains("if (File.GetLastWriteTimeUtc(f) > DateTime.UtcNow.AddDays(-1)) continue;", Corps(maj, "IEnumerator Bilan()"),
                              "l'installeur qu'un autre CIPL ouvert va lancer n'est pas effacé");
        Assert.That(Place(telecharger, "ConfirmDialog.Erreur(RegleVersion.TexteInstalleurRefuse);"),
                    Is.LessThan(Place(telecharger, "if (_fenetre != null) StartCoroutine(Lire(_canvas, fenetre: false));")), "empreinte relue après un refus");
        string construire = File.ReadAllText(Path.Combine(Application.dataPath, "Editor", "ConstruireInstalleur.cs"));
        Assert.That(Place(construire, "p.WaitForExit();"), Is.LessThan(Place(construire, "sha256 = RegleVersion.Empreinte(f);")),
                    "l'empreinte est celle de l'installeur fini");
        StringAssert.Contains("sha256 = sha256,", construire, "écrite dans version.json");
    }

    // ── D2-03 (part fermeture) : une dernière passe d'envoi des documents avant de quitter ──

    [Test]
    public void D2_03_la_fermeture_fait_une_derniere_passe_des_documents()
    {
        Assert.That(FermetureGuard.DernierePasse(true, false), Is.True, "une entreprise ouverte sur le cloud, en ligne");
        Assert.That(FermetureGuard.DernierePasse(false, false), Is.False, "pas d'entreprise ouverte : on quitte tout de suite");
        Assert.That(FermetureGuard.DernierePasse(true, true), Is.False, "hors ligne : rien ne partirait");

        string src = Src("General/FermetureGuard.cs");
        string quitter = Corps(src, "public static void QuitterSansGarde()");
        StringAssert.DoesNotContain("Application.Quit()", quitter, "plus de sortie directe");
        Assert.That(Fois(quitter, "QuitterApresDocuments();"), Is.EqualTo(2), "rien en vol, et après l'attente de l'enregistrement ou de l'envoi");
        Assert.That(Place(quitter, "{ _quitterConfirme = false; return; }"), Is.LessThan(quitter.LastIndexOf("QuitterApresDocuments();", StringComparison.Ordinal)),
                    "D8-01, D8-16 : un échec ou un refus pendant l'attente garde CIPL ouvert, avant toute passe");
        string passe = Corps(src, "static async void QuitterApresDocuments()");
        StringAssert.Contains("ShowInfo(\"Envoi des documents…\")", passe);
        Assert.That(Place(passe, "await Task.Yield();"), Is.LessThan(Place(passe, "DocumentsCloud.Terminer(Application.Quit);")),
                    "jamais Application.Quit pendant wantsToQuit");
        string garde = Corps(src, "private static bool AutoriserFermeture()");
        StringAssert.Contains("if (!DernierePasse()) return true;", garde, "la croix de Windows et Alt+F4 aussi");
    }

    // ── D2-04, D2-09, D2-20 : rien n'est arrêté avant d'avoir une entreprise à ouvrir ; les envois finissent d'abord ──

    [Test]
    public void D2_04_D2_09_D2_20_l_ancienne_entreprise_ne_s_arrete_qu_a_l_ouverture_de_la_nouvelle()
    {
        string src = Src("Cloud/DemarrageCloud.cs");
        string changer = Corps(src, "public static async void ChangerEntreprise()");
        StringAssert.DoesNotContain("DirectCloud.Arreter()", changer, "D2-04 : sans réseau, on reste sur l'entreprise ouverte, synchronisée");
        StringAssert.DoesNotContain("FermerEcrans", changer);
        Assert.That(Place(changer, "if (_choix) return;"), Is.LessThan(Place(changer, "await OuvrirUneEntreprise(forcerChoix: true);")),
                    "un retrait pendant le choix n'ouvre pas une seconde fenêtre de choix");
        StringAssert.Contains("finally { _choix = false; }", changer);
        string recharger = Corps(src, "public static async void Recharger()");
        Assert.That(Place(recharger, "if (_ouvertures > 0) return;"), Is.LessThan(Place(recharger, "await Ouvrir(e);")),
                    "D2-09 : pas d'ouverture de l'entreprise d'avant pendant qu'une autre attend la fin d'un envoi");

        string ouvrir = Corps(src, "static async Task Ouvrir(EntrepriseCloud e)");
        int attendre = Place(ouvrir, "await FinDesEnvois();"), fermer = Place(ouvrir, "EcritureCloud.FermerEcrans();"),
            arreter = Place(ouvrir, "DirectCloud.Arreter();"), oublier = Place(ouvrir, "if (SessionCloud.Entreprise?.id != e.id) EcritureCloud.Oublier();"),
            poser = Place(ouvrir, "SessionCloud.Entreprise = e;"), vider = Place(ouvrir, "_bm.Vider();");
        Assert.That(attendre, Is.LessThan(fermer), "D2-09 : l'envoi de factures en cours finit avant que ses fiches ne soient fermées");
        Assert.That(fermer, Is.LessThan(poser), "les verrous sont rendus à l'entreprise d'avant");
        Assert.That(arreter, Is.LessThan(poser), "D2-04 : le direct de l'ancienne s'arrête ici");
        Assert.That(oublier, Is.GreaterThan(arreter).And.LessThan(poser), "D2-20 : l'état connu du serveur est vidé avant de poser la nouvelle");
        Assert.That(poser, Is.LessThan(vider));
        Assert.That(Place(ouvrir, "_ouvertures++;"), Is.LessThan(attendre));
        StringAssert.Contains("finally { _ouvertures--;", ouvrir);

        foreach (var sortie in new[] { "public static async void Exclu()", "public static async void SeDeconnecter()" })
        {
            string corps = Corps(src, sortie);
            Assert.That(Place(corps, "await SortieApresLesEnvois()"), Is.LessThan(Place(corps, "EcritureCloud.FermerEcrans();")), sortie);
        }
        StringAssert.Contains("for (int i = 0; i < 120 && EcritureCloud.EnvoiEnCours; i++) await Task.Delay(500);", Corps(src, "static async Task FinDesEnvois()"));
    }

    // ── D2-24 : la liste de charges ne quitte les Réglages que si chaque bâtiment est écrit ──

    [Test]
    public void D2_24_la_liste_de_charges_reste_si_un_batiment_n_est_pas_enregistre()
    {
        string src = Src("Facturation/ReglagePanel.cs");
        int attendre = Place(src, "await EcritureCloud.Attendre();   // la liste ne quitte les Réglages qu'une fois les bâtiments écrits");
        int aJour = src.IndexOf("bats.Any(x => !EcritureCloud.AJour(x))", attendre, StringComparison.Ordinal);
        int retirer = src.IndexOf("R.listesCharges.Remove(liste);", attendre, StringComparison.Ordinal);
        Assert.That(aJour, Is.GreaterThan(attendre).And.LessThan(retirer), "après l'attente, avant de retirer la liste");
        string refus = src.Substring(aJour, retirer - aJour);
        StringAssert.Contains("ConfirmDialog.Erreur(", refus, "refus au centre");
        StringAssert.Contains("rien n'est modifié : réessayez.", refus);
        StringAssert.Contains("return;", refus, "et sortie : la liste reste");
    }
}
