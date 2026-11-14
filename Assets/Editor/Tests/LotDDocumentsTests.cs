using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using A = SyncDocuments.Action;

/// Revue 17, lot D (11/11/2026), groupe « documents » : D2-02, D2-03, D2-06, D2-07, D2-11, D2-14, D2-15, D2-23, D2-26
/// (REVUE_17/D2.md), D1-16 et D1-17 (REVUE_17/D1.md).
public class LotDDocumentsTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));

    static Dictionary<string, string> D(params (string c, string e)[] l)
    {
        var d = new Dictionary<string, string>();
        foreach (var (c, e) in l) d[c] = e;
        return d;
    }

    static SyncDocuments.Distant Vivant(string e) => new SyncDocuments.Distant { Empreinte = e };
    static SyncDocuments.Distant Supprime(string e) => new SyncDocuments.Distant { Empreinte = e, Supprime = true };

    // ── D2-06 : la table est lue en entier, page après page ────────────────────

    [Test]
    public void D2_06_les_elements_d_un_tableau_JSON_se_comptent_sans_le_decoder()
    {
        Assert.That(CloudClient.CompterElements("[]"), Is.EqualTo(0));
        Assert.That(CloudClient.CompterElements(" [ ] \n"), Is.EqualTo(0));
        Assert.That(CloudClient.CompterElements("[{\"chemin\":\"a\"}]"), Is.EqualTo(1));
        Assert.That(CloudClient.CompterElements("[{\"chemin\":\"a\",\"taille\":3},{\"chemin\":\"b\",\"taille\":4}]"), Is.EqualTo(2),
                    "les virgules d'un objet ne comptent pas");
        Assert.That(CloudClient.CompterElements("[{\"chemin\":\"Batiment/a, b [1] {x}.pdf\"},{\"chemin\":\"c\"}]"), Is.EqualTo(2),
                    "virgule, crochet et accolade dans un nom de fichier");
        Assert.That(CloudClient.CompterElements("[{\"chemin\":\"a\\\"],[\\\\\"},{\"chemin\":\"b\"}]"), Is.EqualTo(2),
                    "guillemet et barre oblique échappés : la chaîne ne se ferme qu'à son vrai guillemet");
        Assert.That(CloudClient.CompterElements("[{\"data\":{\"lots\":[1,2,3],\"x\":{\"y\":[]}}},{\"data\":{}}]"), Is.EqualTo(2),
                    "tableaux et objets imbriqués (fiches)");
        Assert.That(CloudClient.CompterElements("[1,2,3]"), Is.EqualTo(3));
        foreach (var illisible in new[] { null, "", "{\"message\":\"erreur\"}", "[{\"chemin\":\"a\"}", "[{\"chemin\":\"a", "[] x", "<html>" })
            Assert.That(CloudClient.CompterElements(illisible), Is.EqualTo(-1), illisible ?? "null");
    }

    [Test]
    public void D2_06_les_pages_fusionnent_en_un_seul_tableau_et_jamais_en_liste_partielle()
    {
        string tout = CloudClient.FusionnerPages(new[] { "[{\"a\":1},{\"a\":2}]", " [{\"a\":3}] ", "[]" });
        Assert.That(tout, Is.EqualTo("[{\"a\":1},{\"a\":2},{\"a\":3}]"));
        Assert.That(CloudClient.CompterElements(tout), Is.EqualTo(3));
        Assert.That(CloudClient.FusionnerPages(new string[0]), Is.EqualTo("[]"), "table vide");
        Assert.That(CloudClient.FusionnerPages(new[] { "[]", "[]" }), Is.EqualTo("[]"));
        Assert.That(CloudClient.FusionnerPages(new[] { "[{\"a\":1}]", "{\"message\":\"erreur\"}" }), Is.Null,
                    "une page illisible : pas de liste du tout (une liste partielle ferait croire que le serveur n'a plus des documents)");
    }

    [Test]
    public void D2_06_les_trois_lectures_des_documents_passent_par_TableTout_dans_l_ordre_de_la_cle()
    {
        string client = Src("Cloud/CloudClient.cs");
        StringAssert.Contains("offset += n;", client, "l'offset avance du nombre de lignes reçues, pas de la page demandée");
        StringAssert.Contains("if (!r.Ok) return r;", client, "une page en échec : la réponse est en échec");
        foreach (var f in new[] { "Cloud/DocumentsCloud.cs", "Cloud/ExportCloud.cs", "Cloud/TransfertCloud.cs" })
        {
            string src = Src(f);
            StringAssert.Contains("CloudClient.TableTout(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), \"documents\"", src, f);
            StringAssert.Contains("&order=chemin.asc\"", src, f + " : ordre total, la clé de la table est (entreprise_id, chemin)");
            StringAssert.DoesNotContain("plafonnée à 1 000 lignes", src, f);
            StringAssert.DoesNotContain("supprime_le=is.null", src, f + " : les lignes supprimées sont écartées ici, pas au serveur (pages stables)");
        }
        StringAssert.Contains("primary key (entreprise_id, chemin)", File.ReadAllText(Path.Combine("Serveur", "etape6_supabase.sql")));
    }

    [Test]
    public void D2_06_une_ligne_rendue_deux_fois_par_la_lecture_en_pages_n_est_gardee_qu_une_fois()
    {
        // Un collègue insère une fiche entre deux pages : l'ancienne dernière ligne revient en tête de la page suivante.
        var b = new Batiment { Name = "rivoli" };
        b.locataireDuBatiment = new List<Locataire> { new Locataire { Name = "Volteo" }, new Locataire { Name = "Sephora" } };
        DonneesCloud.Decouper(b, out string jsonBat, out var jsonLocs);
        string ligneBat = "{\"id\":\"" + b.id + "\",\"data\":" + jsonBat + "}";
        string Loc(int i) => "{\"batiment_id\":\"" + b.id + "\",\"ordre\":" + i + ",\"data\":" + jsonLocs[i] + "}";
        var r = DonneesCloud.Recomposer("[" + ligneBat + "," + ligneBat + "]", "[" + Loc(0) + "," + Loc(1) + "," + Loc(1) + "]");
        Assert.That(r.Count, Is.EqualTo(1), "un seul bâtiment");
        Assert.That(r[0].locataireDuBatiment.Select(l => l.Name), Is.EqualTo(new[] { "Volteo", "Sephora" }), "Sephora une seule fois");
        StringAssert.Contains("L'APPELANT DÉDOUBLE PAR CLÉ", Src("Cloud/CloudClient.cs"));
    }

    // ── D2-02 : retraits voulus et fusible ─────────────────────────────────────

    static List<(A, string)> Renommage38()
    {
        var plan = new List<(A, string)>();
        for (int i = 0; i < 38; i++) plan.Add((A.SupprimerServeur, $"Batiment/Résidence Tilleul/Volteo/Factures/Loyers/F-{i:00}.pdf"));
        for (int i = 0; i < 38; i++) plan.Add((A.Indexer, $"Batiment/Résidence Les Tilleuls/Volteo/Factures/Loyers/F-{i:00}.pdf"));
        return plan;
    }

    [Test]
    public void D2_02_un_renommage_de_tout_le_dossier_n_est_pas_bloque_par_le_fusible()
    {
        var retraits = new[] { SyncDocuments.PrefixeRetrait("Batiment/Résidence Tilleul") };
        Assert.That(retraits[0], Is.EqualTo("Batiment/Résidence Tilleul/"));
        Assert.That(SyncDocuments.SuppressionsAControler(Renommage38(), retraits), Is.EqualTo(0), "40 connus, 38 suppressions sous le retrait annoncé");
        Assert.That(SyncDocuments.SuppressionsSuspectes(0, 2), Is.False);
        Assert.That(SyncDocuments.SousRetrait("Batiment/Résidence TILLEUL/Volteo/Bail/bail.pdf", retraits), Is.True, "sans tenir compte de la casse, comme le disque");
    }

    [Test]
    public void D2_02_un_dossier_disparu_sans_annonce_reste_bloque()
    {
        Assert.That(SyncDocuments.SuppressionsAControler(Renommage38(), new string[0]), Is.EqualTo(38));
        Assert.That(SyncDocuments.SuppressionsAControler(Renommage38(), null), Is.EqualTo(38));
        Assert.That(SyncDocuments.SuppressionsSuspectes(38, 40), Is.True, "le fusible du 01/11 saute comme avant");
        Assert.That(SyncDocuments.SuppressionsAControler(Renommage38(), new[] { "Batiment/Résidence/", "Batiment/Autre/" }), Is.EqualTo(38),
                    "le retrait d'un autre dossier, même au nom voisin, ne couvre pas celui-ci");

        // Un retrait voulu (30 fichiers) et, en même temps, 8 des 10 autres fichiers disparus sans annonce : jugés entre eux, refusés.
        var plan = new List<(A, string)>();
        for (int i = 0; i < 30; i++) plan.Add((A.SupprimerServeur, $"Batiment/rivoli/F-{i:00}.pdf"));
        for (int i = 0; i < 8; i++) plan.Add((A.SupprimerServeur, $"Batiment/haussmann/F-{i:00}.pdf"));
        int aControler = SyncDocuments.SuppressionsAControler(plan, new[] { "Batiment/rivoli/" });
        Assert.That(aControler, Is.EqualTo(8));
        Assert.That(SyncDocuments.SuppressionsSuspectes(aControler, 10), Is.True);
    }

    [Test]
    public void D2_02_un_retrait_est_toujours_un_sous_dossier_partage_jamais_Batiment_entier()
    {
        Assert.That(SyncDocuments.PrefixeRetrait("Batiment/rivoli/Volteo/"), Is.EqualTo("Batiment/rivoli/Volteo/"));
        foreach (var refuse in new[] { null, "", "Batiment", "Batiment/", "Logo", "corbeille_documents/2026-11-11", "Backups/x", "Batiment/../x", "Batiment//x" })
            Assert.That(SyncDocuments.PrefixeRetrait(refuse), Is.Null, refuse ?? "null");
        Assert.That(SyncDocuments.SousRetrait("Batiment/rivoli/a.pdf", new string[0]), Is.False);
    }

    [Test]
    public void D2_02_un_renommage_n_annonce_son_retrait_qu_apres_le_deplacement_reussi()
    {
        // Un déplacement raté (PDF ouvert) ne doit laisser aucun retrait : la garde du 01/11 resterait affaiblie pour ce dossier.
        string src = Src("Save/DossiersDonnees.cs");
        var annonces = Regex.Matches(src, @"Directory\.Move\(source, destination\);\s*DocumentsCloud\.RetraitVoulu\(source\);");
        Assert.That(annonces.Count, Is.EqualTo(2), "RenommerBatiment et RenommerLocataire");
        Assert.That(Regex.Matches(src, @"DocumentsCloud\.RetraitVoulu\(").Count, Is.EqualTo(2), "aucune annonce avant le déplacement");
    }

    [Test]
    public void D2_02_un_dossier_remis_apres_sa_suppression_repart_au_lieu_d_aller_en_corbeille()
    {
        // Le seul bâtiment qui a des documents est supprimé (suppressions transmises : manifeste vide), puis « Annuler » le remet.
        const string f = "Batiment/rivoli/a.pdf";
        var serveur = new Dictionary<string, SyncDocuments.Distant> { { f, Supprime("e1") } };
        Assert.That(SyncDocuments.Plan(D((f, "e1")), D(), serveur, true), Is.EqualTo(new[] { (A.Corbeille, f) }),
                    "premier passage de ce dossier : le cloud fait foi (inchangé)");
        Assert.That(SyncDocuments.Plan(D((f, "e1")), D(), serveur, true, dejaPasse: true), Is.EqualTo(new[] { (A.Envoyer, f) }),
                    "déjà synchronisé depuis l'ouverture : le fichier remis repart");
    }

    // ── D2-07 : « Tout retélécharger depuis le cloud » ─────────────────────────

    [Test]
    public void D2_07_apres_la_remise_a_zero_tout_est_retelecharge_et_rien_n_est_supprime_au_serveur()
    {
        var serveur = new Dictionary<string, SyncDocuments.Distant>();
        for (int i = 0; i < 40; i++) serveur[$"Batiment/rivoli/Volteo/Factures/Loyers/F-{i:00}.pdf"] = Vivant("e" + i);
        serveur["Batiment/rivoli/Volteo/Bail/vieux.pdf"] = Supprime("x");

        // État remis à zéro (manifeste vide), dossier vide : 40 lignes vivantes → 40 téléchargements, et rien d'autre.
        var plan = SyncDocuments.Plan(D(), D(), serveur, telecharger: true);
        Assert.That(plan.Count(p => p.action == A.Telecharger), Is.EqualTo(40));
        Assert.That(plan.Count, Is.EqualTo(40), "ni suppression au serveur, ni envoi, ni corbeille");

        // Le dossier est revenu entre-temps avec des fichiers (identique, différent, supprimé au cloud, inconnu du cloud) :
        // sans manifeste, toujours aucune suppression au serveur.
        var local = D(("Batiment/rivoli/Volteo/Factures/Loyers/F-00.pdf", "e0"), ("Batiment/rivoli/Volteo/Factures/Loyers/F-01.pdf", "autre"),
                      ("Batiment/rivoli/Volteo/Bail/vieux.pdf", "x"), ("Batiment/rivoli/Volteo/Bail/neuf.pdf", "n"));
        plan = SyncDocuments.Plan(local, D(), serveur, telecharger: true);
        Assert.That(plan.Any(p => p.action == A.SupprimerServeur), Is.False);
        Assert.That(plan.Count(p => p.action == A.Telecharger), Is.EqualTo(39), "tout ce qui n'est pas déjà identique ici");

        StringAssert.Contains(@"C:\Docs\SCI", DocumentsCloud.MessageSuspension(@"C:\Docs\SCI", 44));
        StringAssert.Contains("44 document(s)", DocumentsCloud.MessageSuspension(@"C:\Docs\SCI", 44));
    }

    [Test]
    public void D2_07_la_fenetre_ne_vient_que_si_la_suspension_dure()
    {
        Assert.That(DocumentsCloud.IssueAProposer(1, 44, true), Is.False, "un PDF effacé puis réécrit : état passager, rien à l'écran");
        Assert.That(DocumentsCloud.IssueAProposer(2, 44, true), Is.True, "deuxième passage suspendu de suite");
        Assert.That(DocumentsCloud.IssueAProposer(5, 0, true), Is.False, "aucun fichier connu");
        Assert.That(DocumentsCloud.IssueAProposer(5, 44, false), Is.False, "mode Drive : il recopie lui-même");
    }

    // ── D2-26 : un renommage n'envoie que la ligne ─────────────────────────────

    [Test]
    public void D2_26_un_renommage_indexe_sans_renvoyer_le_contenu()
    {
        const string ancien = "Batiment/rivoli/a.pdf", nouveau = "Batiment/rivoli2/a.pdf";
        var serveur = new Dictionary<string, SyncDocuments.Distant> { { ancien, Vivant("e1") } };
        Assert.That(SyncDocuments.Plan(D((nouveau, "e1")), D((ancien, "e1")), serveur, true),
                    Is.EqualTo(new[] { (A.SupprimerServeur, ancien), (A.Indexer, nouveau) }), "le serveur a déjà ce contenu, vivant, sous l'ancien chemin");
        Assert.That(SyncDocuments.Plan(D((nouveau, "e2")), D((ancien, "e1")), serveur, true),
                    Is.EqualTo(new[] { (A.SupprimerServeur, ancien), (A.Envoyer, nouveau) }), "contenu inconnu du serveur : envoyé en entier");
        serveur[ancien] = Supprime("e1");
        Assert.That(SyncDocuments.Plan(D((nouveau, "e1")), D(), serveur, true, dejaPasse: true), Is.EqualTo(new[] { (A.Envoyer, nouveau) }),
                    "seule une ligne VIVANTE prouve que le contenu est au cloud");
        Assert.That(SyncDocuments.ReserveAdmin(A.Indexer, "Logo/logo.png"), Is.True, "le logo reste réservé à l'admin (D1-09)");
        Assert.That(SyncDocuments.ReserveAdmin(A.Indexer, "Batiment/rivoli/a.pdf"), Is.False);
    }

    // ── D2-11 : les dossiers vides ne bloquent plus un renommage ───────────────

    [Test]
    public void D2_11_un_dossier_vide_ne_bloque_pas_le_renommage()
    {
        string reelle = SaveLocationService.GetSaveRoot();
        string bac = Path.Combine(Path.GetTempPath(), "cipl_tests_lotD_" + Guid.NewGuid().ToString("N"));
        SaveLocationService.UseRoot(Path.Combine(bac, "CIPL_Saves"));
        try
        {
            string source = DossiersDonnees.DossierBatiment("rivoli 2");
            Directory.CreateDirectory(Path.Combine(source, "Volteo", "Bail"));
            File.WriteAllText(Path.Combine(source, "Volteo", "Bail", "bail.pdf"), "bail");
            // Chez ce collègue, l'ancien « rivoli » a été vidé fichier par fichier : il n'en reste que les dossiers.
            string rivoli = DossiersDonnees.DossierBatiment("rivoli");
            Directory.CreateDirectory(Path.Combine(rivoli, "Volteo", "Factures", "Loyers"));

            Assert.That(DossiersDonnees.RenommerBatiment("rivoli 2", "rivoli", out string err), Is.True, err);
            Assert.That(File.ReadAllText(Path.Combine(rivoli, "Volteo", "Bail", "bail.pdf")), Is.EqualTo("bail"));
            Assert.That(Directory.Exists(Path.Combine(rivoli, "Volteo", "Factures")), Is.False, "l'arborescence vide a été retirée, pas fusionnée");

            Directory.CreateDirectory(Path.Combine(rivoli, "Sephora", "Bail"));
            Assert.That(DossiersDonnees.RenommerLocataire("rivoli", "Volteo", "Sephora", out err), Is.True, err);
            Assert.That(File.Exists(Path.Combine(rivoli, "Sephora", "Bail", "bail.pdf")), Is.True);

            // Un dossier qui contient un fichier bloque toujours, et rien n'y est touché.
            string zara = Path.Combine(rivoli, "Zara", "Bail", "x.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(zara));
            File.WriteAllText(zara, "x");
            Assert.That(DossiersDonnees.RenommerLocataire("rivoli", "Sephora", "Zara", out err), Is.False);
            StringAssert.Contains("existe déjà", err);
            Assert.That(File.ReadAllText(zara), Is.EqualTo("x"));
            Assert.That(File.Exists(Path.Combine(rivoli, "Sephora", "Bail", "bail.pdf")), Is.True);
        }
        finally
        {
            SaveLocationService.UseRoot(reelle);   // priorité absolue : rendre la vraie racine
            try { if (Directory.Exists(bac)) Directory.Delete(bac, true); } catch { /* bac temporaire */ }
        }
    }

    [Test]
    public void D2_11_la_corbeille_des_documents_retire_les_dossiers_qu_elle_vide()
    {
        string racine = Path.Combine(Path.GetTempPath(), "cipl_tests_lotD_" + Guid.NewGuid().ToString("N"));
        try
        {
            string loyers = Path.Combine(racine, "Batiment", "rivoli", "Volteo", "Factures", "Loyers");
            string sephora = Path.Combine(racine, "Batiment", "rivoli", "Sephora");
            Directory.CreateDirectory(loyers);
            Directory.CreateDirectory(sephora);
            File.WriteAllText(Path.Combine(sephora, "a.pdf"), "a");

            DocumentsCloud.RetirerDossiersVides(racine, loyers);
            Assert.That(Directory.Exists(Path.Combine(racine, "Batiment", "rivoli", "Volteo")), Is.False, "vidés, du plus profond vers le haut");
            Assert.That(File.Exists(Path.Combine(sephora, "a.pdf")), Is.True, "un dossier qui a encore un fichier reste");

            File.Delete(Path.Combine(sephora, "a.pdf"));
            DocumentsCloud.RetirerDossiersVides(racine, sephora);
            Assert.That(Directory.Exists(Path.Combine(racine, "Batiment", "rivoli")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(racine, "Batiment")), Is.True, "jamais un dossier racine");
        }
        finally { try { if (Directory.Exists(racine)) Directory.Delete(racine, true); } catch { /* bac temporaire */ } }
    }

    // ── D2-14, D2-15, D2-23 : transfert et export ──────────────────────────────

    [Test]
    public void D2_14_les_documents_d_un_dossier_se_reconnaissent_sans_la_casse()
    {
        Assert.That(TransfertCloud.DuDossier("Batiment/RIVOLI/Volteo/Bail/bail.pdf", "Batiment/Rivoli/"), Is.True, "« RIVOLI » renommé « Rivoli » : le dossier n'a pas bougé");
        Assert.That(TransfertCloud.DuDossier("Batiment/Rivoli/Volteo/Bail/bail.pdf", "Batiment/Rivoli/Volteo/"), Is.True);
        Assert.That(TransfertCloud.DuDossier("Batiment/Rivoli 2/a.pdf", "Batiment/Rivoli/"), Is.False, "un autre bâtiment au nom voisin");
        Assert.That(TransfertCloud.DuDossier(null, "Batiment/Rivoli/"), Is.False);
    }

    [Test]
    public void D2_15_l_export_et_le_transfert_filtrent_les_chemins_par_CheminSur()
    {
        StringAssert.Contains("SyncDocuments.CheminSur(l.chemin)", Src("Cloud/ExportCloud.cs"));
        StringAssert.Contains("SyncDocuments.CheminSur(l.chemin)", Src("Cloud/TransfertCloud.cs"));
        Assert.That(SyncDocuments.CheminSur("Batiment/../../Windows/x.dll"), Is.False);
        Assert.That(SyncDocuments.CheminSur("Batiment/rivoli/a.pdf"), Is.True);
    }

    [Test]
    public void D2_23_le_transfert_note_ses_suppressions_avant_d_appeler_le_serveur()
    {
        string src = Src("Cloud/TransfertCloud.cs");
        int note = src.IndexOf("EcritureCloud.NoterSuppression(", StringComparison.Ordinal);
        int appel = src.IndexOf("\"transferer_fiches\"", StringComparison.Ordinal);
        Assert.That(note, Is.GreaterThan(0).And.LessThan(appel), "avant l'appel : l'écho des suppressions peut arriver avant la réponse");
    }

    // ── D2-03 : un document écrit part sans attendre ───────────────────────────

    [Test]
    public void D2_03_un_document_ecrit_demande_un_passage_et_la_fermeture_en_fait_un_dernier()
    {
        string pdf = Src("Facturation/FacturePdfService.cs");
        Assert.That(Regex.Matches(pdf, @"return PdfEcrit\(RunEdge\(").Count, Is.EqualTo(3), "facture, quittance, régularisation");
        StringAssert.Contains("if (ok) DocumentsCloud.Bientot();", pdf);
        Assert.That(Regex.Matches(pdf, @"return RunEdge\(Build\w+\(d\), ScreenshotArgs\(").Count, Is.EqualTo(2), "pas les aperçus PNG");
        StringAssert.Contains("if (n > 0) DocumentsCloud.Bientot();", Src("Batiment/PhotoService.cs"));
        string dossiers = Src("Save/DossiersDonnees.cs");
        int copie = dossiers.IndexOf("File.Copy(source, Path.Combine(dossier, cible));", StringComparison.Ordinal);
        Assert.That(copie, Is.GreaterThan(0));
        StringAssert.Contains("DocumentsCloud.Bientot();", dossiers.Substring(copie, 200), "après la copie d'un document (bail, pièce jointe)");
        string docs = Src("Cloud/DocumentsCloud.cs");
        StringAssert.Contains("public static async void Terminer(Action ensuite)", docs);
        StringAssert.DoesNotContain("ShowInfo(\"Envoi des documents…\")", docs, "affiché une seule fois, par FermetureGuard");
    }

    // ── D1-16, D1-17 ───────────────────────────────────────────────────────────

    [Test]
    public void D1_16_les_corps_d_insertion_echappent_aussi_les_caracteres_de_controle()
    {
        Assert.That(DonneesCloud.CorpsBatiment("ent\t1", "bat\"1", "{}"),
                    Is.EqualTo("[{\"entreprise_id\":\"ent\\t1\",\"id\":\"bat\\\"1\",\"data\":{}}]"));
        string locs = DonneesCloud.CorpsLocataires("e", "b\t", new[] { "l\n" }, new[] { "{}" });
        StringAssert.DoesNotContain("\t", locs);
        StringAssert.DoesNotContain("\n", locs);
        StringAssert.DoesNotContain("Chaine(", Src("Cloud/DonneesCloud.cs"), "la copie privée a disparu");
        StringAssert.DoesNotContain("static string Q(", Src("Cloud/TransfertCloud.cs"), "la copie privée a disparu");
    }

    [Test]
    public void D1_17_une_fonction_brute_lente_ne_fait_pas_passer_hors_ligne()
    {
        string client = Src("Cloud/CloudClient.cs");
        int brut = client.IndexOf("EnvoyerBrut(string methode", StringComparison.Ordinal);
        int envoyer = client.IndexOf("static async Task<Reponse> Envoyer(", StringComparison.Ordinal);
        Assert.That(brut, Is.GreaterThan(0).And.LessThan(envoyer));
        StringAssert.Contains("HorsLigne.Constater(rep.Ok, rep.Code, fonction);", client.Substring(brut, envoyer - brut));
        StringAssert.Contains("Encoding.UTF8.GetBytes(corpsJson), fonction: true);", client, "FonctionBrute passe le drapeau");
        Assert.That(HorsLigne.Panne(false, 0, true), Is.False, "délai dépassé sur une fonction serveur : pas une panne");
        Assert.That(HorsLigne.Panne(false, 0), Is.True);
    }
}
