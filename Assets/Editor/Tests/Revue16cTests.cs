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
        StringAssert.Contains("FactureElectronique.EtatInconnu ?", File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Facturation", "FactureLoyerPanel.cs")));
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
}
