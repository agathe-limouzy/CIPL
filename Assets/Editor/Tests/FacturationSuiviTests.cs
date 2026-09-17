using System;
using NUnit.Framework;

/// Non-régression de H2, H2-bis, H3 et G1 : le suivi de facturation.
///
/// Le point le plus coûteux du projet : une facture déjà émise ne doit JAMAIS
/// consommer un second numéro de séquence. Le correctif initial ne couvrait que
/// les états Envoyé/Impayé, laissant passer les deux cas les plus courants.
public class FacturationSuiviTests
{
    const string Key = "loyer-2026-P3";

    static string Iso(int joursDepuisAujourdhui)
        => DateTime.Today.AddDays(joursDepuisAujourdhui).ToString("yyyy-MM-dd");

    static Locataire LocataireAvecFactureEmise(string echeanceISO, string statutForce = null)
    {
        var loc = new Locataire();
        // Chemin SOUS la racine courante : c'est la condition pour qu'il soit
        // relativise. Aucun fichier n'est ecrit, on ne calcule qu'un chemin.
        string pdf = System.IO.Path.Combine(
            DossiersDonnees.DossierFactures("Test", "Dupont"), "loyer.pdf");
        FacturationSuivi.MarquerEnvoye(loc, Key, "Loyer", "Loyer 3e trimestre 2026",
            echeanceISO, "2026/09001", pdf, 3000f, "rib1", "Banque");
        if (statutForce != null) loc.facturesEtat.Find(x => x.key == Key).statut = statutForce;
        return loc;
    }

    // ── H2-bis : les quatre états « émise » ─────────────────────────────────

    [Test]
    public void Une_facture_envoyee_est_detectee_comme_deja_emise()
    {
        var loc = LocataireAvecFactureEmise(Iso(-2));
        Assert.That(FacturationSuivi.EstDejaEmise(loc, Key, out _), Is.True);
    }

    [Test]
    public void Un_loyer_prepare_en_avance_est_detecte_comme_deja_emis()
    {
        // Cas NORMAL du panneau Loyer : préparé plus de 15 j avant l'échéance, donc
        // « En attente d'envoi ». Il échappait à la garde : un second clic consommait
        // une nouvelle séquence ET écrasait le PDF déjà généré.
        var loc = LocataireAvecFactureEmise(Iso(60));
        var rec = loc.facturesEtat.Find(x => x.key == Key);

        Assert.That(rec.statut, Is.EqualTo("AttenteEnvoi"), "pré-requis du scénario");
        Assert.That(FacturationSuivi.EstDejaEmise(loc, Key, out _), Is.True);
    }

    [Test]
    public void Une_facture_payee_regeneree_est_detectee_comme_deja_emise()
    {
        // Sinon : deux numéros pour une seule période. Un paiement ne « dé-émet » pas
        // une facture — un numéro consommé le reste.
        var loc = LocataireAvecFactureEmise(Iso(-90), statutForce: "Paye");
        Assert.That(FacturationSuivi.EstDejaEmise(loc, Key, out _), Is.True);
    }

    [Test]
    public void Une_ligne_sans_PDF_n_est_pas_une_emission()
    {
        var loc = new Locataire();
        FacturationSuivi.MarquerEnvoye(loc, Key, "Loyer", "Loyer", Iso(-5), "", "", 0f, "", "");
        Assert.That(FacturationSuivi.EstDejaEmise(loc, Key, out _), Is.False);
    }

    [Test]
    public void Une_ligne_planifiee_n_est_pas_une_emission()
    {
        var loc = new Locataire();
        loc.facturesEtat.Add(new FactureEtat { key = Key, type = "Loyer", echeanceISO = Iso(200), statut = "" });
        Assert.That(FacturationSuivi.EstDejaEmise(loc, Key, out _), Is.False);
    }

    // ── Correction : numéro et statut conservés ─────────────────────────────

    [Test]
    public void Corriger_conserve_le_numero_et_ne_consomme_pas_de_sequence()
    {
        var loc = LocataireAvecFactureEmise(Iso(-2));
        int seqAvant = loc.factureSeq;

        int corrections = FacturationSuivi.MarquerCorrige(loc, Key, "Loyer 3e trimestre 2026",
            @"C:\Saves\Batiment\Test\Dupont\Facture\loyer-corrigee1.pdf", 3100f);
        var rec = loc.facturesEtat.Find(x => x.key == Key);

        Assert.That(corrections, Is.EqualTo(1));
        Assert.That(rec.numero, Is.EqualTo("2026/09001"), "le numéro doit être conservé");
        Assert.That(loc.factureSeq, Is.EqualTo(seqAvant), "aucune séquence consommée");
        Assert.That(rec.libelle, Does.EndWith("corrigée(1)"));
    }

    [Test]
    public void Corriger_un_loyer_non_encore_envoye_le_laisse_en_attente()
    {
        // Le passer à « Envoyé » afficherait un envoi qui n'a pas eu lieu.
        var loc = LocataireAvecFactureEmise(Iso(60));
        FacturationSuivi.MarquerCorrige(loc, Key, "Loyer", @"C:\x\corrigee.pdf", 3100f);
        var rec = loc.facturesEtat.Find(x => x.key == Key);

        Assert.That(rec.statut, Is.EqualTo("AttenteEnvoi"));
        Assert.That(FacturationSuivi.EtatDe(rec), Is.EqualTo(FacturationSuivi.Etat.AttenteEnvoi));
    }

    [Test]
    public void Corriger_une_facture_payee_la_laisse_payee()
    {
        var loc = LocataireAvecFactureEmise(Iso(-90), statutForce: "Paye");
        FacturationSuivi.MarquerCorrige(loc, Key, "Loyer", @"C:\x\corrigee.pdf", 3050f);
        Assert.That(loc.facturesEtat.Find(x => x.key == Key).statut, Is.EqualTo("Paye"));
    }

    // ── H3 : l'échéance décide de l'impayé ──────────────────────────────────

    [Test]
    public void Une_facture_echue_depuis_plus_de_quinze_jours_passe_impayee()
    {
        var rec = new FactureEtat { key = Key, type = "Loyer", statut = "Envoye", echeanceISO = Iso(-20) };
        Assert.That(FacturationSuivi.EtatDe(rec), Is.EqualTo(FacturationSuivi.Etat.Impaye));
    }

    [Test]
    public void Une_echeance_est_lue_en_culture_invariante()
    {
        // Un échec de parse faisait retomber EtatDe sur « Envoyé » : la facture ne
        // passait JAMAIS impayée et disparaissait des créances.
        Assert.That(FacturationSuivi.TryEcheance("2026-09-30", out var d), Is.True);
        Assert.That(d, Is.EqualTo(new DateTime(2026, 9, 30)));
        Assert.That(FacturationSuivi.TryEcheance("", out _), Is.False);
        Assert.That(FacturationSuivi.TryEcheance("pas une date", out _), Is.False);
    }

    // ── G1 : le chemin du PDF ───────────────────────────────────────────────

    [Test]
    public void Le_chemin_du_PDF_est_stocke_en_relatif()
    {
        var loc = LocataireAvecFactureEmise(Iso(-2));
        string stocke = loc.facturesEtat.Find(x => x.key == Key).pdfPath;

        // Un chemin absolu cassait dès que la sauvegarde changeait de dossier, de
        // disque ou de machine — alors que les PDF, eux, suivaient le déplacement.
        Assert.That(System.IO.Path.IsPathRooted(stocke), Is.False,
                    $"pdfPath devrait être relatif, obtenu « {stocke} »");
    }

    [Test]
    public void Un_ancien_chemin_absolu_reste_lisible()
    {
        // Les enregistrements antérieurs au correctif ne doivent pas être perdus.
        var ancien = new FactureEtat { key = Key, pdfPath = @"D:\Ancien\CIPL_Saves\facture.pdf" };
        Assert.That(FacturationSuivi.CheminPdf(ancien), Is.EqualTo(@"D:\Ancien\CIPL_Saves\facture.pdf"));
    }

    [Test]
    public void CheminPdf_tolere_une_ligne_nulle()
    {
        Assert.That(FacturationSuivi.CheminPdf(null), Is.Null);
    }
}
