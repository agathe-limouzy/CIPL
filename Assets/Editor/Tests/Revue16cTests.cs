using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Étape 16c : un test par défaut corrigé de la revue complète du code (REVUE_16C/SYNTHESE.md).
public class Revue16cTests
{
    static string Serveur(params string[] chemin) =>
        File.ReadAllText(Path.Combine(new[] { Application.dataPath, "..", "Serveur" }.Concat(chemin).ToArray()));

    // ── Critiques ──────────────────────────────────────────────────────────────

    [Test]
    public void D1_01_un_chemin_de_document_ne_sort_pas_du_dossier()
    {
        Assert.That(SyncDocuments.CheminSur("Batiment/rivoli/Factures/f.pdf"), Is.True);
        Assert.That(SyncDocuments.CheminSur("Logo/logo.png"), Is.True);
        Assert.That(SyncDocuments.CheminSur("Anciens documents/rivoli/a.pdf"), Is.True);
        foreach (var mauvais in new[] { "Batiment/../x.pdf", "Batiment/rivoli/../../x", "../x", "C:/Windows/x", "Batiment/a\\b.pdf",
                                        "/Batiment/x", "Batiment//x", "Batiment/./x", "Autre/x.pdf", "", null })
            Assert.That(SyncDocuments.CheminSur(mauvais), Is.False, mauvais ?? "null");

        // Le serveur refuse aussi ces chemins, à l'enregistrement comme à la suppression.
        string sql = Serveur("etape16c_supabase.sql");
        StringAssert.Contains("create or replace function public.chemin_document_valide", sql);
        Assert.That(System.Text.RegularExpressions.Regex.Matches(sql, @"if not public\.chemin_document_valide\(p_chemin\)").Count, Is.EqualTo(2),
            "documenter et supprimer_document");
        StringAssert.DoesNotContain("chr(0)", sql, "Postgres refuse chr(0) : plus aucun document enregistré (constaté le 01/11)");
    }

    [Test]
    public void D1_02_une_erreur_reseau_ne_renvoie_pas_la_cle()
    {
        foreach (var f in new[] { "pappers.ts", "cartes.ts" })
        {
            string ts = Serveur("fonctions", f);
            StringAssert.DoesNotContain("erreur: String((e as Error)?.message", ts, f);
            StringAssert.Contains("console.error(", ts, f);
        }
    }

    [Test]
    public void D4_01_deux_factures_n_ont_jamais_le_meme_pdf()
    {
        var loc = new Locataire { facturesEtat = new List<FactureEtat>() };
        var a = FactureEmission.Preparer(loc, "refac-1", "2026/10003");
        var b = FactureEmission.Preparer(loc, "refac-2", "2026/10004");
        Assert.That(a.NumeroFichier, Is.EqualTo("2026-10003"));
        Assert.That(b.NumeroFichier, Is.Not.EqualTo(a.NumeroFichier));

        // Correction : le numéro de base, sans « corrigée » (le suffixe de fichier le dit déjà).
        loc.facturesEtat.Add(new FactureEtat { key = "refac-1", statut = "Envoye", numero = "2026/10003", pdfPath = "x.pdf" });
        var c = FactureEmission.Preparer(loc, "refac-1", "2026/10009");
        Assert.That(c.Correction, Is.True);
        Assert.That(c.NumeroFichier, Is.EqualTo("2026-10003"));
        Assert.That(c.SuffixeFichier, Is.EqualTo("-corrigee1"));
    }

    [Test]
    public void D5_01_une_correction_hors_plateforme_ne_repart_pas()
    {
        string futur = System.DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
        var jamaisTransmise = new FactureEtat { key = "loyer-2026-P4", statut = "Envoye", echeanceISO = futur, pdfPath = "x.pdf", plateformeDonnees = "{\"number\":\"v1\"}" };
        Assert.That(FactureElectronique.EnAttente(jamaisTransmise), Is.True, "avant : elle serait partie avec l'ancien montant");
        FactureElectronique.Preparer(jamaisTransmise, null);
        Assert.That(jamaisTransmise.plateformeEtat, Is.EqualTo("perimee"));
        Assert.That(FactureElectronique.EnAttente(jamaisTransmise), Is.False);
        Assert.That(FactureElectronique.Pastille(jamaisTransmise), Is.Null);
        Assert.That(jamaisTransmise.plateformeDonnees, Is.Not.Empty, "gardée pour la référence d'une rectificative");

        var transmise = new FactureEtat { key = "loyer-2026-P3", statut = "Envoye", echeanceISO = futur, pdfPath = "y.pdf", plateformeDonnees = "{}", plateformeId = "42", plateformeEtat = "envoyee" };
        FactureElectronique.Preparer(transmise, null);
        Assert.That(transmise.plateformeId, Is.EqualTo("42"));
        Assert.That(transmise.plateformeEtat, Is.EqualTo("envoyee"));
    }

    [Test]
    public void D5_02_le_resultat_va_sur_la_fiche_relue_et_la_facture_ne_part_qu_une_fois()
    {
        var avant = new Locataire { id = "L1", facturesEtat = new List<FactureEtat> {
            new FactureEtat { key = "loyer-2026-P1", plateformeDonnees = "J1", plateformeId = "42", plateformeEtat = "envoyee" },
            new FactureEtat { key = "loyer-2026-P2", plateformeDonnees = "J2", plateformeId = "43", plateformeEtat = "envoyee" } } };
        var relue = new Locataire { id = "L1", facturesEtat = new List<FactureEtat> {
            new FactureEtat { key = "loyer-2026-P1", plateformeDonnees = "J1" },
            new FactureEtat { key = "loyer-2026-P2", plateformeDonnees = "J2-refaite-ailleurs" } } };
        FactureElectronique.RecopierPlateforme(avant, relue);
        Assert.That(relue.facturesEtat[0].plateformeId, Is.EqualTo("42"), "même facture : le résultat suit la fiche relue");
        Assert.That(relue.facturesEtat[1].plateformeId, Is.Null.Or.Empty, "refaite ailleurs entre-temps : la sienne prime");

        // Côté serveur : une réservation par facture, la première gagne ; une facture transmise rend son identifiant.
        string ts = Serveur("fonctions", "facture-electronique.ts");
        StringAssert.Contains("plateformes_envois", ts);
        StringAssert.Contains("ignoreDuplicates: true", ts);
        StringAssert.Contains("if (deja?.facture_id) return json({ ok: true, id: deja.facture_id });", ts);
        StringAssert.Contains("create table if not exists public.plateformes_envois", Serveur("etape16c_supabase.sql"));
    }

    [Test]
    public void D6_01_la_taille_d_un_lot_est_sa_surface_effective()
    {
        var a = new Locataire { id = "A", Name = "A", tailleLot = 800f };
        var b = new Locataire { id = "B", Name = "B", tailleLot = 0f };   // surface non saisie : la part du disponible
        var bat = new Batiment { tailleBatiment = 2000f, locataireDuBatiment = new List<Locataire> { a, b } };
        Assert.That(FactureVarResolver.Resolve("{loc.taille}", b, bat, new FactureContext()), Is.EqualTo("1200 m²"));
        Assert.That(FactureVarResolver.Resolve("{loc.taille}", a, bat, new FactureContext()), Is.EqualTo("800 m²"));
        Assert.That(BatimentPrefab.TailleLotDisponible(bat.tailleBatiment, bat.locataireDuBatiment, b), Is.EqualTo(1200f),
            "la même règle que ChargePanel (répartition « Surface »)");
    }

    // ── Majeurs ────────────────────────────────────────────────────────────────

    [Test]
    public void D4_02_retour_refuse_pendant_l_envoi()
    {
        Assert.That(FactureEmission.RefuseFermeture(true), Is.True);
        Assert.That(FactureEmission.RefuseFermeture(false), Is.False);
        foreach (var p in new[] { "FactureLoyerPanel", "FactureDepotPanel", "FactureRefacPanel", "FactureRegulPanel" })
        {
            string cs = File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Facturation", p + ".cs"));
            StringAssert.Contains("back.onClick.AddListener(Retour);", cs, p);
            StringAssert.Contains("void OnDisable() => _envoiEnCours = false;", cs, p);
        }
    }

    // ── Trouvés pendant le parcours BC (01/11) ─────────────────────────────────

    [Test]
    public void BC7_le_titre_du_PDF_est_son_nom_de_fichier()
    {
        string html = FacturePdfService.AvecTitre("<html lang=\"fr\"><head><meta charset=\"utf-8\"></head><body></body></html>",
            @"C:\CIPL\Factures\Refacturation-2026-11001-2 charges-A & B-11-2026.pdf");
        StringAssert.Contains("<head><title>Refacturation-2026-11001-2 charges-A &amp; B-11-2026</title><meta", html);
        foreach (var t in new[] { "facture_template.html", "facture_regul_template.html", "quittance_template.html" })
            StringAssert.Contains("<head>", File.ReadAllText(Path.Combine(Application.streamingAssetsPath, t)), t);
    }

    [Test]
    public void BC8_le_formulaire_de_charge_montre_la_part_de_chacun()
    {
        var a = new Locataire { id = "A", lotBatiment = 1, tailleLot = 800f };
        var b = new Locataire { id = "B", lotBatiment = 2, tailleLot = 0f };
        var bat = new Batiment { tailleBatiment = 2000f, locataireDuBatiment = new List<Locataire> { a, b } };
        float Surface(Locataire l) => l.tailleLot > 0f ? l.tailleLot : BatimentPrefab.TailleLotDisponible(bat.tailleBatiment, bat.locataireDuBatiment, l);
        var c = new ChargeBatiment { cout = 1000f };

        c.ratios = ChargePanel.Ratios("Surface", bat.locataireDuBatiment, Surface, null);
        Assert.That(ListesCharges.QuotePart(c, a, bat), Is.EqualTo(400f).Within(0.001f));
        Assert.That(ListesCharges.QuotePart(c, b, bat), Is.EqualTo(600f).Within(0.001f), "surface déduite : 1 200 m²");
        c.ratios = ChargePanel.Ratios("Égalité", bat.locataireDuBatiment, Surface, null);
        Assert.That(ListesCharges.QuotePart(c, b, bat), Is.EqualTo(500f).Within(0.001f));
        c.ratios = ChargePanel.Ratios("Manuel", bat.locataireDuBatiment, Surface, l => l == a ? 0.3f : 0.7f);
        Assert.That(ListesCharges.QuotePart(c, a, bat), Is.EqualTo(300f).Within(0.001f));
        Assert.That(ChargePanel.Ratios("Surface", new List<Locataire> { a }, Surface, null), Is.Empty, "un seul locataire : il paie tout");
    }

    // ── Lot 2 : serveur, sécurité et argent (01/11) ───────────────────────────

    static string Sql16c2 => Serveur("etape16c2_supabase.sql");
    static string Fonction(string sql, string nom) =>
        System.Text.RegularExpressions.Regex.Match(sql, @"create or replace function public\." + nom + @"\(.*?\$\$;", System.Text.RegularExpressions.RegexOptions.Singleline).Value;

    [Test]
    public void D1_03_le_serveur_reserve_RIB_logo_et_listes_a_l_admin()
    {
        string f = Fonction(Sql16c2, "enregistrer_reglages");
        Assert.That(f, Is.Not.Empty);
        foreach (var champ in new[] { "'ribs'", "'logoPath'", "'sansLogo'", "'listesCharges'" }) StringAssert.Contains(champ, f, champ);
        Assert.That(System.Text.RegularExpressions.Regex.Matches(f, "'refus', 'admin'").Count, Is.EqualTo(2), "version 0 et modification");
    }

    [Test]
    public void D1_04_l_import_suit_les_regles_du_groupe()
    {
        string f = Fonction(Sql16c2, "creer_entreprise_import");
        StringAssert.Contains("ecriture_permise(g)", f);
        StringAssert.Contains("case when st = 'essai' then 3 else 1 end", f);
        StringAssert.Contains("r <> 'admin'", f);
        StringAssert.DoesNotContain("do update set role = 'admin'", f, "un membre n'est plus promu admin par l'import");
    }

    [Test]
    public void D1_05_pas_de_second_abonnement_Stripe()
    {
        string ts = Serveur("fonctions", "abonnement-stripe.ts");
        string choisir = ts.Substring(ts.IndexOf("action === \"choisir\""), ts.IndexOf("action === \"portail\"") - ts.IndexOf("action === \"choisir\""));
        StringAssert.Contains("stripe.subscriptions.list(", choisir);
        StringAssert.Contains("checkout.sessions.expire(", choisir);
    }

    [Test]
    public void D1_06_l_etat_de_la_plateforme_survit_a_une_panne()
    {
        StringAssert.Contains("indisponible: true", Serveur("fonctions", "facture-electronique.ts"));
        var r = JsonUtility.FromJson<FactureElectronique.Reponse>("{\"ok\":true,\"relie\":true,\"statut\":\"verified\",\"indisponible\":true}");
        Assert.That(r.ok && r.relie && r.indisponible && r.statut == "verified");
        // 02/11 (BI5) : la ligne « Envoi : … » est partagée par les trois formulaires (FactureElectronique.LibelleEnvoi).
        StringAssert.Contains(": EtatInconnu ?", File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Facturation", "FactureElectronique.cs")));
        foreach (var panneau in new[] { "FactureLoyerPanel", "FactureRegulPanel", "FactureRefacPanel" })
            StringAssert.Contains("FactureElectronique.LibelleEnvoi(", File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Facturation", panneau + ".cs")), panneau);
    }

    [Test]
    public void BI8_un_avoir_part_par_la_plateforme_et_s_affiche()
    {
        // 02/11 : la régul corrigée en avoir (TVA −49,60) restait par e-mail (« tva > 0 ») et le suivi affichait « — » pour son montant.
        StringAssert.Contains("Math.Abs(tva) > 0.005f", File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Facturation", "FactureElectronique.cs")));
        StringAssert.Contains("Math.Abs(v) > 0.004f ? v.ToString", File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Facturation", "LocataireSuiviInline.cs")));
        // Retours du 02/11 : la pastille sur toutes les lignes d'une régul regroupée ; un loyer de 0 € refusé, plateforme ou non.
        StringAssert.Contains("MemeFacture(_loc, l.key).Select(FactureElectronique.Pastille)", File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Facturation", "LocataireSuiviInline.cs")));
        string loyer = File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Facturation", "FactureLoyerPanel.cs"));
        StringAssert.Contains("if (Math.Abs(d.ttc) <= 0.004f)", loyer);
        StringAssert.DoesNotContain("parPlateforme && Math.Abs(d.ttc)", loyer);
    }

    [Test]
    public void D1_07_un_jeton_refuse_est_relu_avant_de_conclure_retire()
    {
        string ts = Serveur("fonctions", "facture-electronique.ts");
        int relu = ts.IndexOf("select(\"access_token, refresh_token, expire_le\")"), retire = ts.IndexOf("\"retire\"");
        Assert.That(relu, Is.GreaterThan(0).And.LessThan(retire), "la relecture précède la réponse « retire »");
        StringAssert.Contains("> 300_000", ts);
    }

    [Test]
    public void D1_08_Stripe_avant_la_base_et_un_nombre_entier()
    {
        string ts = Serveur("fonctions", "abonnement-stripe.ts");
        string ajuster = ts.Substring(ts.IndexOf("action === \"ajuster\""));
        StringAssert.Contains("Number.isInteger(licences)", ajuster);
        Assert.That(ajuster.IndexOf("subscriptionItems.update(l.id"), Is.LessThan(ajuster.IndexOf(".from(\"entreprises\").update({ licences_max")));
    }

    [Test]
    public void D1_09_les_secrets_du_cloud_sont_chiffres_sur_ce_PC()
    {
        string dossier = Path.Combine(Path.GetTempPath(), "cipl_test_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            string chemin = Path.Combine(dossier, "cipl_secrets_test.dat");
            Assert.That(ReglageService.EcrireTexte(chemin, "pennylane_key=abc\nsmtp_password=xyz\n", out var erreur), Is.True, erreur);
            string brut = File.ReadAllText(chemin);
            StringAssert.StartsWith("DPAPI:", brut);
            StringAssert.DoesNotContain("abc", brut); StringAssert.DoesNotContain("xyz", brut);
            Assert.That(ReglageService.LireTexte(chemin), Is.EqualTo("pennylane_key=abc\nsmtp_password=xyz\n"));
            File.WriteAllText(chemin, "smtp_password=clair\n");   // fichier d'avant le chiffrement
            Assert.That(ReglageService.LireTexte(chemin), Is.EqualTo("smtp_password=clair\n"));
            string app = Path.Combine(dossier, "cipl_app_secrets.dat");   // adresse du cloud : publique, en clair
            Assert.That(ReglageService.EcrireTexte(app, "cloud_url=x\n", out erreur), Is.True, erreur);
            Assert.That(File.ReadAllText(app), Is.EqualTo("cloud_url=x\n"));
        }
        finally { Directory.Delete(dossier, true); }
    }

    [Test]
    public void D3_01_une_installation_neuve_connait_le_cloud()
    {
        StringAssert.StartsWith("https://", CloudDefaut.Url);
        Assert.That(CloudDefaut.Cle.Length, Is.GreaterThan(20));
        Assert.That(ReglageService.OuDefaut("", "https://x.supabase.co"), Is.EqualTo("https://x.supabase.co"));
        Assert.That(ReglageService.OuDefaut("https://test.supabase.co", "https://x.supabase.co"), Is.EqualTo("https://test.supabase.co"), "le fichier l'emporte");
    }

    [Test]
    public void D3_03_changer_d_entreprise_et_se_deconnecter_previennent()
    {
        string cs = File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Cloud", "EcransCloud.cs"));
        StringAssert.Contains("FermetureGuard.ConfirmerPerteSaisies(\"Changer d'entreprise\"", cs);
        StringAssert.Contains("FermetureGuard.ConfirmerPerteSaisies(\"Se déconnecter\"", cs);
    }

    [Test]
    public void D3_06_inscrire_exige_une_adresse_prouvee()
    {
        string f = Fonction(Sql16c2, "inscrire");
        StringAssert.Contains("auth.jwt() -> 'amr'", f);
        StringAssert.Contains("{\"method\":\"otp\"}", f);
        StringAssert.DoesNotContain("\"password\"", f);
    }

    [Test]
    public void D3_07_Stripe_unpaid_vaut_expire()
    {
        var ligne = Serveur("fonctions", "stripe-webhook.ts").Split('\n').First(l => l.Contains("\"unpaid\""));
        StringAssert.Contains("\"expire\"", ligne);
    }

    // ── Lot 3 : facturation (01/11) ───────────────────────────────────────────

    static string Script(params string[] chemin) =>
        File.ReadAllText(Path.Combine(new[] { Application.dataPath, "Script" }.Concat(chemin).ToArray()));

    [Test]
    public void D4_03_corriger_met_a_jour_l_echeance_et_le_RIB_et_part_tout_de_suite()
    {
        var loc = new Locataire { facturesEtat = new List<FactureEtat> {
            new FactureEtat { key = "loyer-2026-P10", statut = "Envoye", echeanceISO = "2026-10-08", pdfPath = "x.pdf", ribId = "r1", ribNom = "CIC" } } };
        FacturationSuivi.MarquerCorrige(loc, "loyer-2026-P10", "Loyer Octobre 2026", "y.pdf", 1200f, "2026-11-15", "r2", "BNP");
        var r = loc.facturesEtat[0];
        Assert.That(r.echeanceISO, Is.EqualTo("2026-11-15"));
        Assert.That(r.ribId, Is.EqualTo("r2")); Assert.That(r.ribNom, Is.EqualTo("BNP"));
        Assert.That(r.statut, Is.EqualTo("Envoye"), "une correction ne change pas le statut");
        StringAssert.Contains("if (!correction && dateEnvoi.HasValue", Script("Facturation", "FactureLoyerPanel.cs"), "plus d'envoi différé pour une correction");
    }

    [Test]
    public void D4_04_un_numero_saisi_fait_avancer_la_sequence_et_un_doublon_est_refuse()
    {
        Assert.That(FactureEmission.NumeroSaisi("047"), Is.EqualTo(47));
        Assert.That(FactureEmission.NumeroSaisi("A12"), Is.EqualTo(12));
        Assert.That(FactureEmission.NumeroSaisi(""), Is.EqualTo(0));
        var loc = new Locataire { factureSeq = 2, facturesEtat = new List<FactureEtat>(), factureLoyer = new FactureInfo { numeroId = "047" } };
        var d = FactureEmission.Preparer(loc, "loyer-2026-P1", "2026/10047");
        Assert.That(d.Refus, Is.Null);
        FactureEmission.Enregistrer(loc, "loyer-2026-P1", "Loyer", d, "Loyer Janvier 2026", "2026-01-01", "x.pdf", 1200f, null, loc.factureLoyer, "Facture", true);
        Assert.That(loc.factureSeq, Is.EqualTo(48), "la séquence suit l'ID saisi");
        Assert.That(FactureEmission.Preparer(loc, "loyer-2026-P2", "2026/10047").Refus, Does.Contain("déjà"));
        Assert.That(FactureEmission.Preparer(loc, "loyer-2026-P1", "2026/10047").Refus, Is.Null, "la même facture (correction) garde son numéro");
    }

    [Test]
    public void D4_05_une_facture_emise_reste_dans_le_suivi_quand_la_planification_change()
    {
        // Révision du dépôt 2026 émise, puis « Réviser » dans la fiche : la prochaine révision passe en 2027.
        var loc = new Locataire { Name = "L", depotDeGarantie = 3090f, dateRevisionDepotISO = "2027-01-15", periodiciteLoyer = Periodicite.mensuel, loyerAnnuel = 12000f,
            facturesEtat = new List<FactureEtat> { new FactureEtat { key = "depot-2026", type = "Depot", libelle = "Révision du dépôt de garantie",
                statut = "Envoye", echeanceISO = "2026-01-15", pdfPath = "d.pdf", numero = "2026/01001", montant = 90f } } };
        Assert.That(FacturationSuivi.Lignes(loc, 2026).Any(l => l.key == "depot-2026" && l.statut == "Envoye"), "la facture envoyée ne disparaît plus");
        Assert.That(FacturationSuivi.Lignes(loc, 2027).Any(l => l.key == "depot-2026"), Is.False);
    }

    [Test]
    public void D4_06_rien_n_est_propose_en_mode_Pennylane()
    {
        Assert.That(FactureEnvoiAuto.PropositionPossible(ModeEnvoi.Pennylane), Is.False);
        Assert.That(FactureEnvoiAuto.PropositionPossible(ModeEnvoi.Email), Is.True);
        Assert.That(FactureEnvoiAuto.PropositionPossible(null), Is.True);
    }

    [Test]
    public void D4_12_une_facture_payee_ne_recoit_plus_de_charge()
    {
        var loc = new Locataire { facturesEtat = new List<FactureEtat> {
            new FactureEtat { key = "refac-a", statut = "Paye", pdfPath = "r.pdf", numero = "2026/10001" },
            new FactureEtat { key = "refac-b", statut = "Paye", pdfPath = "r.pdf", numero = "2026/10001" } } };
        Assert.That(FacturationSuivi.ElargitUneFacturePayee(loc, "refac-a", new[] { "refac-a", "refac-b", "refac-c" }), Is.True);
        Assert.That(FacturationSuivi.ElargitUneFacturePayee(loc, "refac-a", new[] { "refac-a", "refac-b" }), Is.False, "la corriger sans l'élargir reste permis");
        loc.facturesEtat[0].statut = loc.facturesEtat[1].statut = "Envoye";
        Assert.That(FacturationSuivi.ElargitUneFacturePayee(loc, "refac-a", new[] { "refac-a", "refac-b", "refac-c" }), Is.False, "pas encore payée : élargir reste permis");
    }

    [Test]
    public void D5_03_la_pastille_de_la_plateforme_s_affiche()
    {
        var loc = new Locataire { Name = "L", periodiciteLoyer = Periodicite.mensuel, loyerAnnuel = 12000f,
            facturesEtat = new List<FactureEtat> { new FactureEtat { key = "loyer-2026-P4", type = "Loyer", statut = "Envoye", echeanceISO = "2026-04-01",
                pdfPath = "x.pdf", plateformeDonnees = "{}", plateformeId = "42", plateformeEtat = "envoyee" } } };
        var l = FacturationSuivi.Lignes(loc, 2026).First(x => x.key == "loyer-2026-P4");
        Assert.That(l.plateformeId, Is.EqualTo("42"));
        Assert.That(FactureElectronique.Pastille(l), Is.EqualTo("Plateforme"));
    }

    [Test]
    public void D5_05_une_facture_refusee_par_la_plateforme_ne_repart_pas_telle_quelle()
    {
        var loc = new Locataire { Name = "L", emailLocataire = "a@b.fr" };
        var rec = new FactureEtat { key = "loyer-2026-P9", type = "Loyer", statut = "AttenteEnvoi", echeanceISO = "2026-09-01", pdfPath = "x.pdf", plateformeEtat = "refusee" };
        Assert.That(FactureEnvoiAuto.DoitPartir("B", loc, rec, new System.DateTime(2026, 9, 10), _ => true, out _, out _), Is.False);
        rec.plateformeEtat = "";
        Assert.That(FactureEnvoiAuto.DoitPartir("B", loc, rec, new System.DateTime(2026, 9, 10), _ => true, out _, out _), Is.True);
        StringAssert.Contains("m.statut = \"AttenteEnvoi\"", Script("Facturation", "FactureElectronique.cs"), "après la bascule, une facture refusée revient en attente");
    }

    [Test]
    public void D5_07_D5_12_encaissee_porte_le_montant_et_la_date_du_versement_et_s_annule()
    {
        var loc = new Locataire { facturesEtat = new List<FactureEtat> {
            new FactureEtat { key = "loyer-2026-P3", statut = "Paye", pdfPath = "x.pdf", montant = 1200f, plateformeId = "7",
                versements = new List<Versement> { new Versement { dateISO = "2026-03-28", montant = 1200f } } } } };
        var r = loc.facturesEtat[0];
        var (montant, date) = FactureElectronique.Encaissement(loc, r);
        Assert.That(montant, Is.EqualTo(1200f)); Assert.That(date, Is.EqualTo("2026-03-28"));
        Assert.That(FactureElectronique.EnAttente(r), Is.True, "à déclarer");
        r.encaisseeDeclaree = true; r.encaisseeMontant = 1200f; r.encaisseeDateISO = date;
        Assert.That(FactureElectronique.EnAttente(r), Is.False);
        r.statut = "Envoye";
        Assert.That(FactureElectronique.EnAttente(r), Is.True, "plus payée : l'annulation (212 négatif) attend");
        string ts = Serveur("fonctions", "facture-electronique.ts");
        StringAssert.Contains("reported_data", ts); StringAssert.Contains("type_code: \"MEN\"", ts);
    }

    [Test]
    public void D5_08_D5_13_l_acheteur_de_la_facture_electronique_est_celui_du_PDF()
    {
        var loc = new Locataire { siretNumber = "111 111 111 00011" };
        Assert.That(FactureElectronique.SiretAcheteur(loc, "  "), Is.EqualTo("111 111 111 00011"));
        Assert.That(FactureElectronique.SiretAcheteur(loc, "222 222 222 00022"), Is.EqualTo("222 222 222 00022"));
        Assert.That(FactureElectronique.Siret14("222 222 222 00022"), Is.EqualTo("22222222200022"));
        Assert.That(FactureElectronique.Siret14("222222222"), Is.Null);
        StringAssert.Contains("siret.Length == 14 && siret.StartsWith(siren) ? siret : siren", Script("Facturation", "FactureEn16931.cs"));
        StringAssert.Contains("0225:${siretAcheteur}", Serveur("fonctions", "facture-electronique.ts"));
    }

    [Test]
    public void D5_10_D5_11_D6_06_D6_07_regles_simples()
    {
        var ctx = new FactureContext { ttc = 48894.58f };   // D6-06 : écriture française des montants
        Assert.That(FactureVarResolver.Resolve("{loyer.ttc}", null, null, ctx), Is.EqualTo(48894.58f.ToString("N2", FacturePdfService.FrCulture) + " €"));
        var bat = new Batiment { locataireDuBatiment = new List<Locataire> {   // D6-07 : un RIB utilisé par un locataire
            new Locataire { Name = "sephora", factureLoyer = new FactureInfo { ribId = "r1" } }, new Locataire { Name = "B" } } };
        Assert.That(ReglagePanel.LocatairesUtilisant(new[] { bat }, i => i?.ribId, "r1"), Is.EqualTo(new[] { "sephora" }));
        Assert.That(ReglagePanel.LocatairesUtilisant(new[] { bat }, i => i?.ribId, "r9"), Is.Empty);
        StringAssert.Contains("_fe.refus == \"retire\" || _fe.refus == \"jeton\"", Script("Facturation", "ReglagePanel.cs"));   // D5-10
        StringAssert.Contains("entreprise changée pendant le traitement", Script("Facturation", "FactureElectronique.cs"));   // D5-11
    }

    [Test]
    public void D6_02_D6_04_D6_05_D6_08_dates_provision_base_et_ancien_depot()
    {
        var f = new FactureInfo { saved = true, provisionMontant = 300f };   // D6-04
        Assert.That(FactureLoyerPanel.ProvisionProposee(f, 350f, null), Is.EqualTo(350f));
        Assert.That(FactureLoyerPanel.ProvisionProposee(f, 350f, new FactureEtat { key = "loyer-2026-P1", statut = "AttenteEnvoi" }), Is.EqualTo(350f));
        Assert.That(FactureLoyerPanel.ProvisionProposee(f, 350f, new FactureEtat { key = "loyer-2026-P1", statut = "Envoye" }), Is.EqualTo(300f));
        var loc = new Locataire { depotDeGarantie = 3090f, depotPrecedent = 3000f };   // D6-08
        Assert.That(FactureDepotPanel.AncienDepot(loc), Is.EqualTo(3000f));
        loc.depotPrecedent = 0f;
        Assert.That(FactureDepotPanel.AncienDepot(loc), Is.EqualTo(3090f));
        var l2 = new Locataire { depotSurTTC = true, loyerAnnuel = 12000f, periodiciteLoyer = Periodicite.mensuel };   // D6-05
        StringAssert.Contains("H.T.", ExplicationDepot.Html(l2, 2, 90f, new System.DateTime(2026, 1, 15), null, null, false));
        StringAssert.Contains("T.T.C.", ExplicationDepot.Html(l2, 2, 90f, new System.DateTime(2026, 1, 15)));
        foreach (var p in new[] { "FactureLoyerPanel", "FactureDepotPanel", "FactureRefacPanel", "FactureRegulPanel" })   // D6-02
            StringAssert.DoesNotContain("DateTime.TryParse(f.dateISO", Script("Facturation", p + ".cs"), p);
    }

    [Test]
    public void D7b_02_D7b_04_revision_par_indice_et_reprise()
    {
        var loc = new Locataire { periodiciteLoyer = Periodicite.trimestriel, loyerAnnuel = 12000f, loyerDepart = 12000f, typeRevision = TypeRevision.Aucune };
        loc.DateDebutBail = new System.DateTime(2025, 1, 1);
        loc.DateFinBail = new System.DateTime(2033, 12, 31);
        loc.dateDebutPremierBailISO = loc.dateDebutBailISO;
        Loyers.EnregistrerAvenant(loc, new System.DateTime(2026, 2, 15));
        loc.loyerAnnuel = 12600f;
        Assert.That(Loyers.MontantPeriode(loc, 2025, 4), Is.EqualTo(3000f).Within(0.01f));
        Assert.That(Loyers.MontantPeriode(loc, 2026, 2), Is.EqualTo(3150f).Within(0.01f));
        Assert.That(Loyers.MontantPeriode(loc, 2026, 1), Is.EqualTo(3075f).Within(0.01f), "45 jours à l'ancien loyer, 45 au nouveau");
        string rp = Script("RevisionLoyer", "RevisionPanel.cs");
        Assert.That(System.Text.RegularExpressions.Regex.IsMatch(rp, @"Loyers\.EnregistrerAvenant\(_loc, d\.Date\);\s*if \(selonCA\)"), "l'avenant est enregistré pour les deux modes");
        StringAssert.Contains("FacturationSuivi.EcheanceLoyer(_loc, y, per)", rp);
    }

    [Test]
    public void BC10_une_surface_saisie_reste_apres_enregistrement()
    {
        // La fiche de A (800 m² saisis) réenregistrée sans rien changer : TailleLotDisponible(A) rend 800.
        var a = new Locataire { id = "A", tailleLot = 800f };
        var b = new Locataire { id = "B", tailleLot = 0f };
        var lots = new List<Locataire> { a, b };
        Assert.That(LocatairePrefab.SurfaceAEnregistrer(a.tailleLot, 800f, BatimentPrefab.TailleLotDisponible(2000f, lots, a)), Is.EqualTo(800f));
        Assert.That(LocatairePrefab.SurfaceAEnregistrer(b.tailleLot, 1200f, BatimentPrefab.TailleLotDisponible(2000f, lots, b)), Is.EqualTo(0f),
            "proposée et rendue telle quelle : reste non définie, suit le bâtiment");
        Assert.That(LocatairePrefab.SurfaceAEnregistrer(0f, 900f, 1200f), Is.EqualTo(900f), "saisie nouvelle");
        Assert.That(LocatairePrefab.SurfaceAEnregistrer(800f, 0f, 800f), Is.EqualTo(0f), "effacée à la main");
    }

    // ── Délais de facturation réglables (01/11, demande faite pendant le parcours BE) ──────────────

    [Test]
    public void Delais_entreprise_puis_locataire()
    {
        var R = ReglageService.Current;
        int e0 = R.envoiAvantJours, i0 = R.impayeApresJours, a0 = R.echeanceAutresJours;
        try
        {
            R.envoiAvantJours = 10; R.impayeApresJours = 20; R.echeanceAutresJours = 45;
            var loc = new Locataire();
            Assert.That(FacturationSuivi.EnvoiAvant(loc), Is.EqualTo(10));
            Assert.That(FacturationSuivi.EcheanceAutres(loc), Is.EqualTo(45));
            Assert.That(FacturationSuivi.DateEnvoi("Loyer", "2026-12-01", loc), Is.EqualTo(new System.DateTime(2026, 11, 21)));
            Assert.That(FacturationSuivi.DateEnvoi("Loyer", "2026-12-01"), Is.EqualTo(new System.DateTime(2026, 11, 21)),
                "ancien appel sans locataire : les délais de l'entreprise");
            Assert.That(FacturationSuivi.Lead("Loyer", loc), Is.EqualTo(10 + FacturationSuivi.RappelAvantEnvoiJours));

            // Échéance il y a 12 jours : « Envoyé » avec les 20 j de l'entreprise, « Impayé » avec 5 j propres.
            var f = new FactureEtat { key = "loyer-2026-P1", type = "Loyer", statut = "Envoye", montant = 100f,
                                      echeanceISO = System.DateTime.Today.AddDays(-12).ToString("yyyy-MM-dd") };
            Assert.That(FacturationSuivi.EtatDe(f, loc), Is.EqualTo(FacturationSuivi.Etat.Envoye));
            loc.delaisPropres = true; loc.envoiAvantJours = 16; loc.impayeApresJours = 5; loc.echeanceAutresJours = 60;
            Assert.That(FacturationSuivi.EtatDe(f, loc), Is.EqualTo(FacturationSuivi.Etat.Impaye));
            Assert.That(FacturationSuivi.DateEnvoi("Loyer", "2026-12-01", loc), Is.EqualTo(new System.DateTime(2026, 11, 15)));
            Assert.That(FacturationSuivi.EcheanceAutres(loc), Is.EqualTo(60));
        }
        finally { R.envoiAvantJours = e0; R.impayeApresJours = i0; R.echeanceAutresJours = a0; }
    }

    [Test]
    public void Delais_defauts_et_saisie()
    {
        var R = new ReglageData();
        Assert.That(R.envoiAvantJours, Is.EqualTo(15));
        Assert.That(R.impayeApresJours, Is.EqualTo(15));
        Assert.That(R.echeanceAutresJours, Is.EqualTo(30));
        Assert.That(FacturationSuivi.Jours("", 15), Is.EqualTo(15), "vide : le défaut");
        Assert.That(FacturationSuivi.Jours(" 12 ", 15), Is.EqualTo(12));
        Assert.That(FacturationSuivi.Jours("400", 15), Is.EqualTo(365), "borné à un an");
        Assert.That(FacturationSuivi.Jours("-3", 15), Is.EqualTo(0));
    }

    // ── Lot 4, cloud (D2) ─────────────────────────────────────────────────────────────────────────

    [Test]
    public void D2_02_cession_due_appliquee_future_attend()
    {
        var loc = new Locataire { Name = "SARL X", siretNumber = "1",
                                  cessionPrevue = new CessionPrevue { dateISO = "2026-11-01", nom = "SARL Y", siret = "2" } };
        Assert.That(Cessions.Effectuer(loc, new System.DateTime(2026, 10, 31)), Is.False, "pas encore due");
        Assert.That(loc.Name, Is.EqualTo("SARL X"));
        Assert.That(Cessions.Effectuer(loc, new System.DateTime(2026, 11, 1)), Is.True, "le jour même");
        Assert.That(loc.Name, Is.EqualTo("SARL Y"));
        Assert.That(loc.siretNumber, Is.EqualTo("2"));
        Assert.That(Cessions.Prevue(loc), Is.Null, "plus rien de prévu");
        Assert.That(loc.cessions[0].ancienNom, Is.EqualTo("SARL X"), "l'ancien titulaire est gardé");
        Assert.That(Cessions.Effectuer(loc, new System.DateTime(2026, 11, 2)), Is.False, "une seule fois");
    }

    // ── Lot 4, données et écrans (D7a, D7b, D8) ──────────────────────────────────────────────────

    [Test]
    public void D7a_02_partis_et_archives_ne_comptent_plus()
    {
        var parti = new Locataire { id = "P", tailleLot = 0f, loyerAnnuel = 12000f, dateSortieISO = "2020-01-01" };
        var archive = new Locataire { id = "A", tailleLot = 100f, loyerAnnuel = 5000f, archive = true };
        var nouveau = new Locataire { id = "N", tailleLot = 0f, loyerAnnuel = 12000f };
        var lots = new List<Locataire> { parti, archive, nouveau };
        Assert.That(BatimentPrefab.EnPlace(lots).Select(l => l.id), Is.EqualTo(new[] { "N" }));
        Assert.That(BatimentPrefab.TailleLotDisponible(1000f, lots, nouveau), Is.EqualTo(1000f), "le parti ne prend plus de part");
        Assert.That(BatimentPrefab.TailleLotDepassement(50f, lots), Is.EqualTo(0f), "la surface de l'archivé ne dépasse plus");
        Assert.That(BatimentPrefab.EnPlace(lots).Sum(l => l.loyerAnnuel), Is.EqualTo(12000f));
    }

    [Test]
    public void D7a_05_cession_vide_ce_qui_est_propre_au_cedant()
    {
        var loc = new Locataire { Name = "SARL Ancien", ribLocataireIban = "FR76", ribLocataireTitulaire = "SARL Ancien", ribLocataireBic = "BNP",
                                  telephoneLocataire = "06", codeCompatable = "411ANC", assujettiTva = "oui" };
        Cessions.Appliquer(loc, new System.DateTime(2026, 11, 1), "Jean Martin", "", "", "");
        Assert.That(loc.ribLocataireIban + loc.ribLocataireTitulaire + loc.ribLocataireBic + loc.telephoneLocataire
                    + loc.codeCompatable + loc.assujettiTva, Is.EqualTo(""));
        Assert.That(loc.Name, Is.EqualTo("Jean Martin"));
    }

    [Test]
    public void D7a_06_dates_du_bail_verifiees()
    {
        var d = new System.DateTime(2026, 9, 1);
        Assert.That(Locataire.VerifierDatesBail(true, true, d, d.AddYears(9)), Is.Null);
        Assert.That(Locataire.VerifierDatesBail(false, true, d, d.AddYears(9)), Is.Not.Null, "date manquante");
        Assert.That(Locataire.VerifierDatesBail(true, true, d, d), Is.Not.Null, "fin = début");
    }

    [Test]
    public void D7a_07_ligne_du_resume_affiche_la_surface_effective()
        => Assert.That(LocataireRowUI.SousTitre(1, 1250f), Is.EqualTo("Lot 1 · 1250 m²"));

    [Test]
    public void D7a_09_dossier_du_locataire_en_corbeille_et_restaure()
    {
        string reelle = SaveLocationService.GetSaveRoot();
        string bac = Path.Combine(Path.GetTempPath(), "cipl_tests_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            SaveLocationService.UseRoot(Path.Combine(bac, "CIPL_Saves"));
            string dossier = DossiersDonnees.DossierLocataire("Rivoli", "Sephora");
            Directory.CreateDirectory(dossier);
            File.WriteAllText(Path.Combine(dossier, "bail.pdf"), "x");
            BatimentManager.MettreDossierLocataireEnCorbeille("id-s", "Rivoli", "Sephora");
            Assert.That(Directory.Exists(dossier), Is.False, "parti en corbeille : le nom est libre");
            BatimentManager.RestaurerDossierLocataire("id-s", "Rivoli", "Sephora");
            Assert.That(File.Exists(Path.Combine(dossier, "bail.pdf")), Is.True, "revenu avec « Annuler »");
        }
        finally
        {
            SaveLocationService.UseRoot(reelle);
            try { if (Directory.Exists(bac)) Directory.Delete(bac, true); } catch { /* bac temporaire */ }
        }
    }

    [Test]
    public void D7b_03_revision_par_defaut_et_trimestre_voulu()
    {
        var loc = new Locataire { dateDebutBailISO = "2026-07-01" };
        Assert.That(Loyers.PremiereRevisionParDefaut(loc), Is.EqualTo(new System.DateTime(2027, 7, 1)));
        loc.moisDeRevisionISO = "2027-04-01";
        Assert.That(Loyers.PremiereRevisionParDefaut(loc), Is.EqualTo(new System.DateTime(2027, 4, 1)));
        Assert.That(InseeIndiceService.RangAvant("2025-T1", "2026-T1"), Is.True);
        Assert.That(InseeIndiceService.RangAvant("2026-T1", "2026-T1"), Is.False);
        Assert.That(InseeIndiceService.RangAvant("2027-T1", "2026-T1"), Is.False);
    }

    [Test]
    public void D7b_05_mensualite_nulle_apres_le_dernier_remboursement()
    {
        double pleine = RentabiliteCalculator.Mensualite(100000, 2, 120);
        Assert.That(RentabiliteCalculator.MensualiteEnCours(100000, 2, 120, new System.DateTime(2010, 1, 1), new System.DateTime(2026, 1, 1)), Is.EqualTo(0d));
        Assert.That(RentabiliteCalculator.MensualiteEnCours(100000, 2, 120, new System.DateTime(2010, 1, 1), new System.DateTime(2015, 1, 1)), Is.EqualTo(pleine));
        Assert.That(RentabiliteCalculator.MensualiteEnCours(100000, 2, 120, default, new System.DateTime(2026, 1, 1)), Is.EqualTo(pleine), "date inconnue : compté");
    }

    [Test]
    public void D8_02_racine_cloud_sans_repli()
    {
        Assert.That(SaveLocationService.Racine("D:/x", false, true), Is.EqualTo("D:/x"), "cloud : le dossier enregistré, même absent");
        Assert.That(SaveLocationService.Racine("D:/x", false, false), Is.Null, "local : repli");
        Assert.That(SaveLocationService.Racine("", false, true), Is.Null);
        Assert.That(SaveLocationService.Racine("D:/x", true, false), Is.EqualTo("D:/x"));
    }

    [Test]
    public void D8_05_garde_de_fermeture_voit_les_ecrans_tenus()
        => Assert.That(FermetureGuard.NomsEnEdition(new[] { "A", "B" }, new[] { "B", "C", "" }), Is.EqualTo(new List<string> { "A", "B", "C" }));

    [Test]
    public void Depot_revision_en_attente_corrigeable()
    {
        var loc = new Locataire { dateRevisionDepotISO = "2027-10-01", depotPrecedent = 2000f, depotPrecedentRevisionISO = "2026-10-01" };
        Assert.That(LocataireFacturationFields.DateRevisionProposee(loc, System.DateTime.Today), Is.EqualTo(new System.DateTime(2026, 10, 1)), "révision en attente");
        loc.depotPrecedent = 0f;
        Assert.That(LocataireFacturationFields.DateRevisionProposee(loc, System.DateTime.Today), Is.EqualTo(new System.DateTime(2027, 10, 1)));
    }

    [Test]
    public void Sync_dossier_deplace_et_fusible_des_suppressions()
    {
        Assert.That(SyncDocuments.DossierDeplace(0, 44), Is.True, "plus rien ici, 44 connus : le dossier a bougé");
        Assert.That(SyncDocuments.DossierDeplace(1, 44), Is.False, "un fichier : le fusible prend le relais");
        Assert.That(SyncDocuments.DossierDeplace(0, 0), Is.False, "dossier neuf");
        Assert.That(SyncDocuments.SuppressionsSuspectes(44, 45), Is.True);
        Assert.That(SyncDocuments.SuppressionsSuspectes(3, 45), Is.False, "quelques suppressions voulues");
        Assert.That(SyncDocuments.SuppressionsSuspectes(6, 40), Is.False, "6 sur 40 : moins de la moitié");
        Assert.That(SyncDocuments.SuppressionsSuspectes(6, 10), Is.True);
    }

    // ── Transfert entre entreprises du cloud (01/11, CONCEPTION §16) ─────────────────────────────────

    [Test]
    public void Transfert_bloque_tant_que_la_plateforme_a_du_travail()
    {
        string hier = System.DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
        var attend = new FactureEtat { key = "loyer-2026-P11", type = "Loyer", libelle = "Loyer Novembre 2026", statut = "Envoye",
                                       echeanceISO = hier, montant = 100f, plateformeDonnees = "{}", plateformeId = "" };
        var loc = new Locataire { Name = "Sephora", facturesEtat = new List<FactureEtat> { attend } };
        Assert.That(TransfertCloud.Bloque(new[] { loc }), Does.Contain("attend sa transmission"));

        attend.plateformeId = "1612689"; attend.statut = "Paye";
        Assert.That(TransfertCloud.Bloque(new[] { loc }), Does.Contain("pas encore déclarée"));

        attend.encaisseeDeclaree = true;
        Assert.That(TransfertCloud.Bloque(new[] { loc }), Is.Null, "tout est réglé chez Super PDP");

        attend.encaisseeDeclaree = false; attend.plateformeOrigine = "SCI (JBC)";
        Assert.That(TransfertCloud.Bloque(new[] { loc }), Is.Null, "marque sans identifiant (transfert d'avant) : lecture seule");
        Assert.That(FactureElectronique.EnAttente(attend), Is.False);
        Assert.That(FactureElectronique.Pastille(attend), Is.EqualTo("transmise par SCI (JBC)"));

        // BG7 (01/11) : avec l'identifiant de l'origine, « Payé » se déclare en son nom : la ligne attend donc, et bloque.
        attend.plateformeOrigineId = "ent-jbc";
        Assert.That(FactureElectronique.EnAttente(attend), Is.True);
        Assert.That(TransfertCloud.Bloque(new[] { loc }), Does.Contain("pas encore déclarée"));
        attend.encaisseeDeclaree = true;
        Assert.That(FactureElectronique.EnAttente(attend), Is.False);
        Assert.That(FactureElectronique.Pastille(attend), Is.EqualTo("Encaissée déclarée (SCI (JBC))"));
        attend.statut = "Impaye";
        Assert.That(FactureElectronique.EnAttente(attend), Is.True, "plus payée : l'encaissement déclaré s'annule, au nom de l'origine");
    }

    [Test]
    public void Transfert_facture_transmise_suit_le_locataire_et_revient()
    {
        var jbc = new EntrepriseCloud { id = "ent-jbc", nom = "SCI (JBC)" };
        var test2 = new EntrepriseCloud { id = "ent-2", nom = "SCI test 2" };
        var test3 = new EntrepriseCloud { id = "ent-3", nom = "SCI test 3" };
        var f = new FactureEtat { key = "loyer-2026-P10", plateformeId = "1612689" };
        TransfertCloud.MarquerOrigine(f, jbc, test2);
        Assert.That(f.plateformeOrigine, Is.EqualTo("SCI (JBC)"), "transmise par SCI (JBC)");
        Assert.That(f.plateformeOrigineId, Is.EqualTo("ent-jbc"), "déclarée en son nom");
        TransfertCloud.MarquerOrigine(f, test2, test3);
        Assert.That(f.plateformeOrigineId, Is.EqualTo("ent-jbc"), "un second transfert garde qui l'a transmise");
        TransfertCloud.MarquerOrigine(f, test3, jbc);
        Assert.That(f.plateformeOrigine + f.plateformeOrigineId, Is.Empty, "revenue chez qui l'a transmise : facture ordinaire");

        var ancienne = new FactureEtat { plateformeId = "1612690", plateformeOrigine = "SCI (JBC)" };   // marque d'avant (nom seul)
        TransfertCloud.MarquerOrigine(ancienne, test2, test3);
        Assert.That(ancienne.plateformeOrigineId, Is.Null.Or.Empty, "nom seul : gardée telle quelle (lecture seule)");
        TransfertCloud.MarquerOrigine(ancienne, test3, jbc);
        Assert.That(ancienne.plateformeOrigine, Is.Empty, "retour reconnu par le nom");

        var neuve = new FactureEtat { plateformeDonnees = "{}" };   // jamais transmise : rien à marquer
        TransfertCloud.MarquerOrigine(neuve, jbc, test2);
        Assert.That(neuve.plateformeOrigine, Is.Null.Or.Empty);
    }

    [Test]
    public void Suivi_la_copie_affichee_porte_tous_les_champs()
    {
        // La ligne affichée dans le suivi est une copie (FacturationSuivi.CopieDe) : un champ oublié (plateformeOrigine,
        // trouvé le 01/11 en BG7) rend une pastille fausse sans qu'aucun test ne le voie. Chaque champ public doit suivre.
        var champs = typeof(FactureEtat).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var r = new FactureEtat();
        foreach (var f in champs)
        {
            if (f.FieldType == typeof(string)) f.SetValue(r, f.Name);
            else if (f.FieldType == typeof(bool)) f.SetValue(r, true);
            else if (f.FieldType == typeof(int)) f.SetValue(r, 7);
            else if (f.FieldType == typeof(float)) f.SetValue(r, 7.5f);
            else if (f.FieldType == typeof(List<Versement>)) f.SetValue(r, new List<Versement> { new Versement() });
            else Assert.Fail($"type non prévu pour {f.Name} : {f.FieldType.Name} (compléter le test)");
        }
        var copieDe = typeof(FacturationSuivi).GetMethod("CopieDe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.That(copieDe, Is.Not.Null, "FacturationSuivi.CopieDe");
        var c = (FactureEtat)copieDe.Invoke(null, new object[] { r });
        foreach (var f in champs)
        {
            if (f.FieldType == typeof(List<Versement>)) Assert.That(((List<Versement>)f.GetValue(c)).Count, Is.EqualTo(1), f.Name);
            else Assert.That(f.GetValue(c), Is.EqualTo(f.GetValue(r)), $"champ oublié par CopieDe : {f.Name}");
        }
        Assert.That(FactureElectronique.Pastille(c), Is.EqualTo("Encaissée déclarée (plateformeOrigine)"));
    }

    // ── Mineurs, lot 5 (serveur, cloud et comptes), partie client (01/11) ──────────────────────────────

    [Test]
    public void Mineurs_D1_12_une_fonction_serveur_lente_n_est_pas_une_panne()
    {
        Assert.That(HorsLigne.Panne(false, 0), Is.True, "délai dépassé sur Supabase : panne");
        Assert.That(HorsLigne.Panne(false, 0, fonctionServeur: true), Is.False, "délai dépassé sur une fonction serveur : l'appel échoue seul");
        Assert.That(HorsLigne.Panne(false, 503, fonctionServeur: true), Is.True, "indisponible : panne, fonction ou pas");
        Assert.That(HorsLigne.Panne(false, 400, fonctionServeur: true), Is.False);
    }

    [Test]
    public void Mineurs_D2_15_refus_supprimee()
    {
        string m = EcritureCloud.MotifRefus(new EcritureCloud.Resultat { refus = "supprimee" }, "Volteo");
        Assert.That(m, Does.Contain("« Volteo » a été supprimé entre-temps par un collègue"));
        Assert.That(m, Does.Not.Contain("rechargée"));
        Assert.That(EcritureCloud.MotifRefus(new EcritureCloud.Resultat { refus = "version", nom = "Marie" }, "Volteo"), Does.Contain("modifié entre-temps par Marie"));
    }

    [Test]
    public void Mineurs_D3_12_le_refus_du_serveur_fait_foi_sans_abonnement_lu()
    {
        AbonnementCloud.Oublier();
        AbonnementCloud.Constater("HTTP 400 abonnement expiré : lecture seule");
        Assert.That(AbonnementCloud.Etat, Is.EqualTo(RegleAbonnement.Etat.Expire));
        AbonnementCloud.Oublier();
        AbonnementCloud.Constater("HTTP 500 autre chose");
        Assert.That(AbonnementCloud.Courant, Is.Null, "un autre refus ne change rien");
    }

    [Test]
    public void Mineurs_D3_14_un_seul_echappement_JSON_qui_couvre_les_controles()
    {
        Assert.That(CloudClient.Q("SCI\tRivoli"), Is.EqualTo("\"SCI\\tRivoli\""));
        Assert.That(CloudClient.Q("a\"b\\c\r\nd\u0001"), Is.EqualTo("\"a\\\"b\\\\c\\r\\nd\\u0001\""));
        Assert.That(CloudClient.Q(null), Is.EqualTo("\"\""));
        var relu = JsonUtility.FromJson<Mot>("{\"v\":" + CloudClient.Q("SCI\tRivoli \"x\"") + "}");
        Assert.That(relu.v, Is.EqualTo("SCI\tRivoli \"x\""));
        string corps = RegleInscription.Corps("societe", "SCI\tRivoli", "12345678901234", "Marie", "{}");
        Assert.That(corps, Does.Contain("SCI\\tRivoli").And.Not.Contain("SCI\tRivoli"), "la tabulation part échappée");
    }
    [System.Serializable] class Mot { public string v; }

    // ── Mineurs, lot 5, partie serveur (02/11) : le texte du script SQL et des fonctions ─────────────────

    [Test]
    public void Mineurs_5b_le_script_SQL_porte_les_corrections()
    {
        string sql = Serveur("etape16c4_supabase.sql");
        foreach (var f in new[] { "ecriture_permise(uuid)", "licences_utilisees(uuid)", "licences_utilisees_entreprise(uuid)", "entreprises_licences(uuid)" })
            StringAssert.Contains("'" + f + "'", sql, "D1-10 : " + f);
        StringAssert.Contains("from public, anon, authenticated", sql, "D1-10");
        StringAssert.Contains("if avant.cible_id is not null then", sql, "D1-13");
        StringAssert.Contains("if p_type not in ('batiment', 'locataire')", sql, "D1-16");
        Assert.That(System.Text.RegularExpressions.Regex.Matches(sql, "'refus', 'supprimee'").Count, Is.EqualTo(2), "D2-15 : bâtiment et locataire");
        Assert.That(System.Text.RegularExpressions.Regex.Matches(sql, "for update;").Count, Is.EqualTo(2), "D3-08 : inviter et changer_acces");
        StringAssert.Contains("create table if not exists public.pappers_journal", sql, "D1-15");
        StringAssert.Contains("drop table if exists public.essai_fiches", sql, "D1-16");
        // 02/11 : la première version recréait enregistrer_* sans maj_session (texte de l'étape 4) : chaque enregistrement
        // passait pour celui d'un collègue. Les clauses vitales de la dernière définition (étape 5) doivent y être.
        Assert.That(System.Text.RegularExpressions.Regex.Matches(sql, "maj_session = p_session").Count, Is.EqualTo(2), "maj_session (étape 5) dans les deux enregistrer_*");
        Assert.That(System.Text.RegularExpressions.Regex.Matches(sql, @"maj_par, maj_session\)").Count, Is.EqualTo(2), "maj_session à l'insertion aussi");
    }

    [Test]
    public void Mineurs_5b_les_fonctions_serveur_portent_les_corrections()
    {
        string webhook = Serveur("fonctions", "stripe-webhook.ts");
        StringAssert.Contains("avant !== sub.id", webhook, "D1-11");
        StringAssert.Contains("evt.type === \"customer.subscription.created\" && !avant", webhook, "D1-11 : répartition une seule fois");
        Assert.That(webhook, Does.Not.Contain("licences_max: licence"), "D3-15 : groupes.licences_max n'est plus écrit");
        string retour = Serveur("fonctions", "facture-electronique-retour.ts");
        StringAssert.Contains(".delete().eq(\"etat\", etat).select()", retour, "D1-14");
        Assert.That(retour, Does.Not.Contain("\"Erreur du serveur de CIPL : \" + String("), "D1-14 : détail interne hors de la page");
        string pappers = Serveur("fonctions", "pappers.ts");
        StringAssert.Contains("const QUOTA = 50;", pappers, "D1-15");
        StringAssert.Contains("refus: \"quota\"", pappers, "D1-15");
        StringAssert.Contains("if (!d.id_token) return json({ ok: false, refus: \"echange\"", Serveur("fonctions", "messagerie.ts"), "D1-16");
        string abo = Serveur("fonctions", "abonnement-stripe.ts");
        StringAssert.Contains("stripe.customers.update(", abo, "D3-11");
        Assert.That(abo, Does.Not.Contain(".from(\"groupes\").update({ licences_max"), "D3-15");
        StringAssert.Contains("/auth/v1/logout", File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Cloud", "CloudClient.cs")), "D3-13");
    }

    // ── Mineurs, lot 6 (facturation), 02/11 ─────────────────────────────────────────────────────────────

    [Test]
    public void Mineurs_D4_07_une_refacturation_en_attente_part_sans_attendre_J_moins_15()
    {
        var loc = new Locataire { Name = "Dupont", emailLocataire = "locataire@exemple.fr" };
        string dans30j = System.DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
        var refac = new FactureEtat { key = "refac-x", type = "Refac", libelle = "Refacturation : Jardin", statut = "AttenteEnvoi", echeanceISO = dans30j, montant = 120f, pdfPath = "Batiment/Test/Dupont/Factures/refac.pdf" };
        Assert.That(FactureEnvoiAuto.DoitPartir("Test", loc, refac, System.DateTime.Today, _ => true, out _, out _), Is.True, "pas de date d'envoi pour une refacturation");
        var loyer = new FactureEtat { key = "loyer-2026-P9", type = "Loyer", libelle = "Loyer", statut = "AttenteEnvoi", echeanceISO = dans30j, montant = 1000f, pdfPath = "Batiment/Test/Dupont/Factures/loyer.pdf" };
        Assert.That(FactureEnvoiAuto.DoitPartir("Test", loc, loyer, System.DateTime.Today, _ => true, out _, out _), Is.False, "un loyer attend sa date d'envoi");
        refac.echeanceISO = "";
        Assert.That(FactureEnvoiAuto.DoitPartir("Test", loc, refac, System.DateTime.Today, _ => true, out _, out _), Is.False, "échéance illisible : retenue");
    }

    [Test]
    public void Mineurs_D4_08_les_parts_par_liste_suivent_les_periodes_facturees()
    {
        var listes = new List<string> { "generale", "tf" };
        System.Func<string, float> charges = id => id == "tf" ? 800f : 2000f, provision = id => id == "tf" ? 300f : 0f;
        var parts = FactureRegulPanel.MontantsParListe(listes, charges, provision, 2, 1000f);
        Assert.That(parts["tf"], Is.EqualTo(240f).Within(0.01f), "départ en cours d'année : 800 − 2 × 300 = 200 HT, 240 TTC");
        Assert.That(parts["generale"] + parts["tf"], Is.EqualTo(1000f).Within(0.01f), "la somme des lignes redonne le total");
        Assert.That(FactureRegulPanel.MontantsParListe(listes, charges, provision, 4, 1000f)["tf"], Is.EqualTo(-480f).Within(0.01f), "année pleine : quatre provisions déduites");
    }

    [Test]
    public void Mineurs_D4_09_mode_d_envoi_par_defaut_et_message()
    {
        Assert.That(new ReglageData().modeEnvoi, Is.EqualTo(ModeEnvoi.Email), "une entreprise neuve envoie par e-mail (décision du 02/11)");
        StringAssert.Contains("Réglages › Intégrations", FactureEmission.SansEmail);
        foreach (var p in new[] { "FactureRegulPanel", "FactureRefacPanel", "FactureDepotPanel" })
            Assert.That(File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Facturation", p + ".cs")), Does.Not.Contain("est décochée (Options & envoi)"), p);
    }

    [Test]
    public void Mineurs_D4_10_le_rappel_part_vers_le_destinataire_de_la_facture()
    {
        var loc = new Locataire { Name = "Dupont", emailLocataire = "gerant@locataire.fr", factureLoyer = new FactureInfo { emailDest = "compta@locataire.fr" } };
        var l = new FactureEtat { key = "loyer-2026-P1", type = "Loyer" };
        Assert.That(FactureRappelService.Adresse(loc, l), Is.EqualTo("compta@locataire.fr"));
        loc.factureLoyer.emailDest = "";
        Assert.That(FactureRappelService.Adresse(loc, l), Is.EqualTo("gerant@locataire.fr"));
    }

    [Test]
    public void Mineurs_D4_11_le_contexte_d_un_depot_n_invente_pas_de_TVA()
    {
        var depot = new FactureEtat { key = "depot-2026", type = "Depot", libelle = "Dépôt de garantie", montant = 3000f, statut = "Envoye" };
        var loyer = new FactureEtat { key = "loyer-2026-P1", type = "Loyer", libelle = "Loyer", montant = 1200f, statut = "Envoye" };
        var loc = new Locataire { Name = "Dupont", facturesEtat = new List<FactureEtat> { depot, loyer } };
        var ctx = FactureEnvoiAuto.Contexte(loc, depot);
        Assert.That(ctx.tva, Is.EqualTo(0f).Within(0.001f));
        Assert.That(ctx.loyerHT, Is.EqualTo(3000f).Within(0.001f));
        Assert.That(FactureEnvoiAuto.Contexte(loc, loyer).tva, Is.EqualTo(200f).Within(0.01f));
    }

    [Test]
    public void Mineurs_D5_14_la_phrase_du_resume_dit_le_vrai_refus()
    {
        StringAssert.Contains("injoignable", FactureElectronique.Phrase(new FactureElectronique.Reponse { refus = "indisponible" }));
        StringAssert.Contains("injoignable", FactureElectronique.Phrase(new FactureElectronique.Reponse { refus = "serveur", erreur = "HTTP 500" }));
        StringAssert.Contains("autorisation", FactureElectronique.Phrase(new FactureElectronique.Reponse { refus = "retire" }));
        StringAssert.Contains("SIRET", FactureElectronique.Phrase(new FactureElectronique.Reponse { refus = "siren" }));
    }

    [Test]
    public void Mineurs_D5_16_17_18_19_ligne_negative_mentions_de_repli_vendeur_seul_avec_TVA()
    {
        var e = new FactureEn16931.Entree
        {
            numero = "F-2026-010", dateISO = "2026-11-02", echeanceISO = "2026-12-01",
            vendeurNom = "SCI JBC", vendeurSiren = "843965716",
            acheteurNom = "Danone", acheteurSiren = "552032534", acheteurAdresse = "17 boulevard Haussmann 75009 Paris",
        };
        e.lignes.Add(new KeyValuePair<string, float>("Loyer", 1000f));
        e.lignes.Add(new KeyValuePair<string, float>("Régularisation de provision", -50f));
        string j = FactureEn16931.Json(e);
        StringAssert.Contains("\"net_amount\":\"-50.00\",\"invoiced_quantity\":\"-1\"", j, "D5-16 : la quantité porte le signe");
        StringAssert.Contains("\"item_net_price\":\"50.00\"", j, "D5-16 : le prix unitaire reste positif (BR-27)");
        StringAssert.Contains("\"sum_invoice_lines_amount\":\"950.00\"", j);
        Assert.That(System.Text.RegularExpressions.Regex.Matches(j, "vat_identifier").Count, Is.EqualTo(1), "D5-19 : le vendeur seul");
        StringAssert.Contains(FactureEn16931.PenalitesDefaut, j, "D5-17 : phrase de repli dans le XML");
        Assert.That(FactureEn16931.MentionsRetard(""), Does.Contain("40 €").And.Contain("pénalités"), "D5-17 : le PDF reprend les deux mentions");
        Assert.That(FactureEn16931.MentionsRetard(" Ma phrase. "), Is.EqualTo("Ma phrase."));
        Assert.That(Serveur("fonctions", "facture-electronique-retour.ts"), Does.Not.Contain("societe:"), "D5-18");
    }

    [Test]
    public void Mineurs_D6_09_les_valeurs_substituees_dans_l_explication_du_depot_sont_echappees()
    {
        var loc = new Locataire
        {
            Name = "Dupont & Fils <SARL>", loyerAnnuelPrecedent = 55800f, loyerAnnuel = 57490.78f,
            indiceImmoAuDepart = "133.66  (2023-T3)", indiceImmoActuel = "137.71  (2024-T3)", trimestreDeRevision = "2023-T3", depotSurTTC = false
        };
        var depot = new FactureInfo { depotDu = "Pour {loc.nom} : {depot.montant}." };
        string html = ExplicationDepot.Html(loc, 2, 281.80f, System.DateTime.Today, null, depot);
        StringAssert.Contains("Dupont &amp; Fils &lt;SARL&gt;", html);
        Assert.That(html, Does.Not.Contain("<SARL>"));
    }

    [Test]
    public void Transfert_listes_de_charges_retrouvees_par_nom()
    {
        var origine = new ReglageData { listesCharges = new List<ListeCharges> { new ListeCharges { id = "tf-1", nom = "Taxe foncière" } } };
        var dest = new ReglageData { listesCharges = new List<ListeCharges> { new ListeCharges { id = "tf-9", nom = "taxe foncière " } } };
        var bat = new Batiment { Name = "rivoli", charges = new List<ChargeBatiment> { new ChargeBatiment { listeId = "tf-1" } },
                                 locataireDuBatiment = new List<Locataire>() };
        var aEnvoyer = TransfertEntreprise.ReconcilierListes(bat, origine, dest);
        Assert.That(aEnvoyer, Is.Null, "rien à créer : la liste existe déjà dans la destination");
        Assert.That(bat.charges[0].listeId, Is.EqualTo("tf-9"), "la charge pointe sur la liste de la destination");

        var dest2 = new ReglageData { listesCharges = new List<ListeCharges>() };
        bat.charges[0].listeId = "tf-1";
        var aEnvoyer2 = TransfertEntreprise.ReconcilierListes(bat, origine, dest2);
        Assert.That(aEnvoyer2, Is.Not.Null, "la liste manque : les Réglages de destination sont à enregistrer");
        Assert.That(aEnvoyer2.listesCharges.Count, Is.EqualTo(1));
        Assert.That(bat.charges[0].listeId, Is.EqualTo(aEnvoyer2.listesCharges[0].id));
    }
}
