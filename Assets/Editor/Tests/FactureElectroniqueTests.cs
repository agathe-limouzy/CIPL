using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Étape 18a : la SCI reliée à sa plateforme agréée (Super PDP), et ce que la carte de Réglages en dit.
public class FactureElectroniqueTests
{
    static string Serveur(params string[] chemin) =>
        File.ReadAllText(Path.Combine(new[] { Application.dataPath, "..", "Serveur" }.Concat(chemin).ToArray()));

    [Test]
    public void La_carte_dit_ou_en_est_la_SCI()
    {
        Assert.That(FactureElectronique.Statut(null), Is.Null);
        Assert.That(FactureElectronique.Statut(new FactureElectronique.Reponse { ok = true, relie = false }), Is.Null, "non reliée");
        StringAssert.StartsWith("Active", FactureElectronique.Statut(new FactureElectronique.Reponse { ok = true, relie = true, statut = "verified" }));
        StringAssert.StartsWith("En attente de la vérification", FactureElectronique.Statut(new FactureElectronique.Reponse { ok = true, relie = true, statut = "" }));
        StringAssert.Contains("manuelle", FactureElectronique.Statut(new FactureElectronique.Reponse { ok = true, relie = true, statut = "needs_review" }));
        StringAssert.Contains("refusée", FactureElectronique.Statut(new FactureElectronique.Reponse { ok = true, relie = true, statut = "failed" }));
    }

    [Test]
    public void Les_fonctions_serveur_parlent_comme_CIPL()
    {
        string fonction = Serveur("fonctions", "facture-electronique.ts");
        foreach (var action in new[] { "etat", "activer", "delier" })
            StringAssert.Contains($"action === \"{action}\"", fonction);
        // Chaque refus de la fonction a son message dans CIPL.
        foreach (var refus in new[] { "acces", "admin", "config", "siren", "retire", "jeton" })
        {
            StringAssert.Contains($"\"{refus}\"", fonction);
            StringAssert.DoesNotStartWith("La fonction « facture-electronique »",
                FactureElectronique.Motif(new FactureElectronique.Reponse { refus = refus }), refus);
        }
        Assert.That(FactureElectronique.Motif(new FactureElectronique.Reponse { ok = true }), Is.Null);

        // La fenêtre de Super PDP revient sur la fonction de retour, à la même adresse des deux côtés.
        const string retour = "/functions/v1/facture-electronique-retour";
        StringAssert.Contains(retour, fonction);
        StringAssert.Contains(retour, Serveur("fonctions", "facture-electronique-retour.ts"));
        StringAssert.Contains("plateformes_demandes", Serveur("etape18_supabase.sql"));
        StringAssert.Contains("create table if not exists public.plateformes (", Serveur("etape18_supabase.sql"));
    }

    // ── Étape 18b ──────────────────────────────────────────────────────────────

    [Test]
    public void Le_loyer_au_format_EN_16931()
    {
        Assert.That(FactureEn16931.TvaIntra("552032534"), Is.EqualTo("FR27552032534"), "Danone");
        Assert.That(FactureEn16931.TvaIntra("123"), Is.Empty);
        Assert.That(FactureEn16931.Numero("2026/10003 corrigée(1)"), Is.EqualTo("2026/10003-corrigee-1"), "BR-FR-02");
        Assert.That(FactureEn16931.Numero("2026/10003"), Is.EqualTo("2026/10003"));
        Assert.That(FactureEn16931.Adresse("12 rue de Rivoli 75004 Paris"), Is.EqualTo(("12 rue de Rivoli", "75004", "Paris")));
        Assert.That(FactureEn16931.Adresse("Bât. B, 3 avenue X\n31000 TOULOUSE"), Is.EqualTo(("Bât. B, 3 avenue X", "31000", "TOULOUSE")));
        Assert.That(FactureEn16931.Adresse("Lieu-dit les Prés"), Is.EqualTo(("Lieu-dit les Prés", "", "")));

        var e = new FactureEn16931.Entree
        {
            numero = "F-2026-004", dateISO = "2026-10-30", echeanceISO = "2026-11-05",
            vendeurNom = "SCI JBC", vendeurSiren = "843965716",
            acheteurNom = "Danone \"SA\"", acheteurSiren = "552032534", acheteurAdresse = "17 boulevard Haussmann 75009 Paris",
            mention = "TVA acquittée sur les débits",
        };
        e.lignes.Add(new System.Collections.Generic.KeyValuePair<string, float>("Loyer 4e trimestre 2026", 1000f));
        e.lignes.Add(new System.Collections.Generic.KeyValuePair<string, float>("Provision pour charges", 100.004f));
        string j = FactureEn16931.Json(e);
        StringAssert.StartsWith("{\"number\":\"F-2026-004\",\"issue_date\":\"2026-10-30\",\"type_code\":380,", j);
        StringAssert.Contains("\"sum_invoice_lines_amount\":\"1100.00\",\"total_without_vat\":\"1100.00\","
            + "\"total_vat_amount\":{\"value\":\"220.00\",\"currency_code\":\"EUR\"},\"total_with_vat\":\"1320.00\",\"amount_due_for_payment\":\"1320.00\"", j);
        StringAssert.Contains("\"vat_category_taxable_amount\":\"1100.00\",\"vat_category_tax_amount\":\"220.00\"", j);
        StringAssert.Contains("\"name\":\"Danone \\\"SA\\\"\"", j);
        StringAssert.Contains("\"vat_identifier\":\"FR27552032534\"", j);
        StringAssert.Contains("\"electronic_address\":{\"value\":\"552032534\",\"scheme\":\"0225\"}", j);
        StringAssert.Contains("\"post_code\":\"75009\",\"city\":\"Paris\",\"country_code\":\"FR\"", j);
        foreach (var code in new[] { "PMD", "PMT", "AAB" })
            StringAssert.Contains($"\"subject_code\":\"{code}\"", j, "BR-FR-05");
        StringAssert.Contains("{\"note\":\"TVA acquittée sur les débits\"}]", j);
        StringAssert.DoesNotContain(":\"\"", j, "un champ facultatif vide doit être absent");
        Assert.That(j.Count(c => c == '{'), Is.EqualTo(j.Count(c => c == '}')));
        Assert.That(j.Count(c => c == '['), Is.EqualTo(j.Count(c => c == ']')));

        StringAssert.Contains("\"payment_due_date\":\"2026-11-05\"", j);
        StringAssert.DoesNotContain("preceding_invoice_references", j, "première émission");

        // Correction d'une facture transmise (31/10) : rectificative 384, qui cite la dernière transmise.
        var (refN, refD) = FactureEn16931.Remplacee(new FactureEtat { plateformeId = "42", plateformeDonnees = j });
        Assert.That((refN, refD), Is.EqualTo(("F-2026-004", "2026-10-30")));
        Assert.That(FactureEn16931.Remplacee(new FactureEtat { plateformeDonnees = j }), Is.EqualTo(((string)null, (string)null)),
            "jamais transmise : facture ordinaire");
        var corrigee = new FactureEn16931.Entree
        {
            numero = "F-2026-004 corrigée(1)", dateISO = "2026-10-31", echeanceISO = "2026-11-05",
            vendeurNom = "SCI JBC", vendeurSiren = "843965716", acheteurNom = "Danone", acheteurSiren = "552032534",
            precedentNumero = refN, precedentDateISO = refD,
        };
        corrigee.lignes.Add(new System.Collections.Generic.KeyValuePair<string, float>("Loyer 4e trimestre 2026", 900f));
        string r = FactureEn16931.Json(corrigee);
        StringAssert.StartsWith("{\"number\":\"F-2026-004-corrigee-1\",\"issue_date\":\"2026-10-31\",\"type_code\":384,", r);
        StringAssert.Contains("\"preceding_invoice_references\":[{\"reference\":\"F-2026-004\",\"issue_date\":\"2026-10-30\"}]", r);
        StringAssert.DoesNotContain("preceding_invoice_type_code", r, "refusé par le Factur-X EN 16931 (31/10)");
        Assert.That(FactureEn16931.Remplacee(new FactureEtat { plateformeDonnees = r }), Is.EqualTo(("F-2026-004", "2026-10-30")),
            "rectificative refusée : la correction suivante cite toujours l'originale");
        Assert.That(FactureEn16931.Remplacee(new FactureEtat { plateformeId = "43", plateformeDonnees = r }), Is.EqualTo(("F-2026-004-corrigee-1", "2026-10-31")),
            "rectificative transmise : la correction suivante la cite, elle");

        e.echeanceISO = "2026-02-01";
        string passee = FactureEn16931.Json(e);
        StringAssert.DoesNotContain("payment_due_date", passee, "BR-FR-CO-07 : échéance passée");
        StringAssert.Contains("\"payment_terms\":\"Loyer exigible le 1er février 2026.\"", passee);
    }

    [Test]
    public void Le_locataire_assujetti_et_les_pastilles_du_suivi()
    {
        Assert.That(Locataire.AssujettiTva(new Locataire { siretNumber = "55203253400646" }), Is.True, "société : d'office");
        Assert.That(Locataire.AssujettiTva(new Locataire { siretNumber = "" }), Is.False, "particulier");
        Assert.That(Locataire.AssujettiTva(new Locataire { siretNumber = "55203253400646", assujettiTva = "non" }), Is.False, "association");
        Assert.That(Locataire.AssujettiTva(new Locataire { siretNumber = "", assujettiTva = "oui" }), Is.True);

        string futur = System.DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
        var r = new FactureEtat { key = "loyer-2026-P4", numero = "F-4", statut = "AttenteEnvoi", echeanceISO = futur, pdfPath = "x.pdf", plateformeDonnees = "{}" };
        Assert.That(FactureElectronique.EnAttente(r), Is.False, "envoi différé : pas avant le jour d'envoi");
        Assert.That(FactureElectronique.Pastille(r), Is.Null);
        r.statut = "Envoye";
        Assert.That(FactureElectronique.EnAttente(r), Is.True);
        Assert.That(FactureElectronique.Pastille(r), Is.EqualTo("À renvoyer"));
        r.plateformeId = "42"; r.plateformeEtat = "envoyee";
        Assert.That(FactureElectronique.EnAttente(r), Is.False);
        Assert.That(FactureElectronique.Pastille(r), Is.EqualTo("Plateforme"));
        r.statut = "Paye";
        Assert.That(FactureElectronique.EnAttente(r), Is.True, "« Encaissée » à déclarer");
        r.encaisseeDeclaree = true;
        Assert.That(FactureElectronique.EnAttente(r), Is.False);
        Assert.That(FactureElectronique.Pastille(r), Is.EqualTo("Encaissée déclarée"));
        var refusee = new FactureEtat { statut = "Envoye", echeanceISO = futur, pdfPath = "x.pdf", plateformeDonnees = "{}", plateformeEtat = "refusee" };
        Assert.That(FactureElectronique.EnAttente(refusee), Is.False, "rejetée : à corriger, pas à renvoyer telle quelle");
        Assert.That(FactureElectronique.Pastille(refusee), Is.EqualTo("Refusée par la plateforme"));

        Assert.That(FactureElectronique.Cle(new Locataire { id = "a1b2c3d4-e5f6-0000-0000-000000000000" }, r).Length, Is.LessThanOrEqualTo(36));
        StringAssert.StartsWith("a1b2c3d4-F-4", FactureElectronique.Cle(new Locataire { id = "a1b2c3d4-e5f6-0000-0000-000000000000" }, r));
    }

    [Test]
    public void La_copie_puis_la_notification_seule()
    {
        Assert.That(FactureElectronique.Bascule, Is.EqualTo(new System.DateTime(2027, 9, 1)), "obligation d'émettre des PME");

        var rec = new FactureEtat { key = "loyer-2026-P2", numero = "2026/10003", libelle = "Loyer Février 2026", montant = 1000f, echeanceISO = "2026-02-01" };
        var loc = new Locataire { facturesEtat = new System.Collections.Generic.List<FactureEtat> { rec } };
        var ctx = FactureEnvoiAuto.Contexte(loc, rec);
        Assert.That(ctx.echeance, Is.EqualTo("01/02/2026"));

        // Avant la bascule : l'e-mail « copie » (son propre modèle, 31/10).
        string copieObjet = FactureVarResolver.Resolve(EmailService.CopieObjetDefaut, loc, null, ctx);
        string copie = FactureVarResolver.Resolve(EmailService.CopieCorpsDefaut, loc, null, ctx);
        StringAssert.EndsWith("facture 2026/10003 — copie pour information", copieObjet);
        StringAssert.Contains("c'est elle qui fait foi", copie);
        StringAssert.DoesNotContain("{", copieObjet + copie);
        string notif = FactureVarResolver.Resolve(EmailService.NotifCorpsDefaut, loc, null, ctx);
        StringAssert.Contains("Votre facture 2026/10003", notif);
        StringAssert.Contains("à régler le 01/02/2026", notif);
        StringAssert.DoesNotContain("{", notif, "toutes les variables du modèle sont connues");
        StringAssert.DoesNotContain("{", FactureVarResolver.Resolve(EmailService.NotifObjetDefaut, loc, null, ctx));
    }

    [Test]
    public void Les_modeles_d_e_mail()
    {
        // Texte propre à la facture, sinon le modèle des Réglages, sinon celui d'usine (décision du 31/10).
        Assert.That(EmailService.Modele("Mon texte", "Réglages", "Usine"), Is.EqualTo("Mon texte"));
        Assert.That(EmailService.Modele("", "Réglages", "Usine"), Is.EqualTo("Réglages"));
        Assert.That(EmailService.Modele("Usine", "Réglages", "Usine"), Is.EqualTo("Réglages"), "texte d'usine enregistré avant : suit le modèle");
        Assert.That(EmailService.Modele(null, "", "Usine"), Is.EqualTo("Usine"));
        Assert.That(EmailService.AGarder("Réglages ", "Réglages"), Is.Empty, "égal au modèle : la facture le suivra");
        Assert.That(EmailService.AGarder("Autre", "Réglages"), Is.EqualTo("Autre"));

        // Rappel : le reste à payer, toutes les variables connues.
        var rec = new FactureEtat { key = "loyer-2026-P2", numero = "2026/10003", libelle = "Loyer Février 2026", montant = 1000f, echeanceISO = "2026-02-01" };
        var loc = new Locataire { facturesEtat = new System.Collections.Generic.List<FactureEtat> { rec } };
        var ctx = FactureEnvoiAuto.Contexte(loc, rec);
        ctx.reste = 400f;
        string rappel = FactureVarResolver.Resolve(EmailService.RappelCorpsDefaut, loc, null, ctx);
        StringAssert.Contains("Loyer Février 2026", rappel);
        StringAssert.Contains("01/02/2026", rappel);
        StringAssert.DoesNotContain("{", rappel);

        // Invitation : ses variables à elle ; la ligne du lien Mac disparaît tant qu'il n'existe pas.
        string invit = UtilisateursCloud.Remplir(UtilisateursCloud.InvitationCorpsDefaut, "Agathe", "SCI (JBC)", "K7F2-9QXD", new System.DateTime(2026, 10, 26));
        StringAssert.Contains("Agathe vous invite à rejoindre SCI (JBC)", invit);
        StringAssert.Contains("K7F2-9QXD", invit);
        StringAssert.Contains("26/10/2026", invit);
        StringAssert.DoesNotContain("{", invit);
        if (MiseAJour.LienMac == null) StringAssert.DoesNotContain("\n\n2. À l'écran", invit, "pas de ligne vide à la place du lien Mac");
    }

    [Test]
    public void L_envoi_et_Encaissee_parlent_comme_CIPL()
    {
        string fonction = Serveur("fonctions", "facture-electronique.ts");
        foreach (var action in new[] { "envoyer", "encaisser" })
            StringAssert.Contains($"\"{action}\"", fonction);
        StringAssert.Contains("from=en16931&to=${format}", fonction);
        StringAssert.Contains("envoi(\"factur-x\")", fonction);
        StringAssert.Contains("envoi(\"ubl\")", fonction, "repli quand le destinataire refuse le Factur-X");
        StringAssert.Contains("\"fr:212\"", fonction);
        StringAssert.DoesNotContain(", 502)", fonction, "502 à 504 feraient passer CIPL hors ligne");
        foreach (var refus in new[] { "non_relie", "refusee", "indisponible" })
        {
            StringAssert.Contains($"\"{refus}\"", fonction);
            StringAssert.DoesNotStartWith("La fonction « facture-electronique »",
                FactureElectronique.Motif(new FactureElectronique.Reponse { refus = refus }), refus);
        }
    }
}
