using System;
using NUnit.Framework;

/// Le protocole d'émission, désormais partagé par les quatre panneaux.
///
/// C'est ici que vit la règle qui avait été oubliée (H2) puis mal fermée (H2-bis) :
/// une facture déjà émise ne consomme JAMAIS un second numéro de séquence.
public class FactureEmissionTests
{
    const string Key = "regul-2026";

    static string Iso(int jours) => DateTime.Today.AddDays(jours).ToString("yyyy-MM-dd");

    static Locataire NouveauLocataire() => new Locataire { factureRegul = new FactureInfo() };

    static Locataire AvecFactureEmise(string echeance = null)
    {
        var loc = NouveauLocataire();
        var d = FactureEmission.Preparer(loc, Key, "2026/09001");
        FactureEmission.Enregistrer(loc, Key, "Regul", d, "Régularisation 2026",
            echeance ?? Iso(-2), "Batiment/X/Dupont/Facture/regul.pdf", 1200f, "rib1",
            loc.factureRegul, "Régularisation");
        return loc;
    }

    // ── Première émission ───────────────────────────────────────────────────

    [Test]
    public void Une_premiere_emission_consomme_un_numero()
    {
        var loc = NouveauLocataire();
        int seqAvant = loc.factureSeq;

        var decision = FactureEmission.Preparer(loc, Key, "2026/09001");

        Assert.That(decision.Correction, Is.False);
        Assert.That(decision.NumeroFacture, Is.EqualTo("2026/09001"), "le numéro proposé est retenu");
        Assert.That(decision.SuffixeFichier, Is.Empty, "aucun suffixe : le PDF porte son nom normal");

        FactureEmission.Enregistrer(loc, Key, "Regul", decision, "Régularisation 2026",
            Iso(-2), "Batiment/X/Dupont/Facture/regul.pdf", 1200f, "rib1",
            loc.factureRegul, "Régularisation");

        Assert.That(loc.factureSeq, Is.EqualTo(seqAvant + 1), "la séquence avance");
        Assert.That(loc.factureRegul.numeroId, Is.Empty, "l'ID saisi est oublié");
    }

    // ── Ré-émission : le cœur du sujet ──────────────────────────────────────

    [Test]
    public void Une_seconde_emission_est_une_correction_et_ne_consomme_rien()
    {
        var loc = AvecFactureEmise();
        int seqApresPremiere = loc.factureSeq;

        var decision = FactureEmission.Preparer(loc, Key, "2026/09002");

        Assert.That(decision.Correction, Is.True);
        Assert.That(decision.NumeroCorrection, Is.EqualTo(1));
        Assert.That(decision.NumeroFacture, Is.EqualTo("2026/09001 corrigée(1)"),
                    "le numéro d'origine est conservé, pas celui proposé");
        Assert.That(decision.SuffixeFichier, Is.EqualTo("-corrigee1"),
                    "le PDF d'origine ne doit pas être écrasé");

        FactureEmission.Enregistrer(loc, Key, "Regul", decision, "Régularisation 2026",
            Iso(-2), "Batiment/X/Dupont/Facture/regul-corrigee1.pdf", 1250f, "rib1",
            loc.factureRegul, "Régularisation");

        Assert.That(loc.factureSeq, Is.EqualTo(seqApresPremiere), "AUCUNE séquence consommée");
        Assert.That(loc.facturesEtat.Find(x => x.key == Key).numero, Is.EqualTo("2026/09001"),
                    "la ligne de suivi garde le numéro d'origine");
    }

    [Test]
    public void Les_corrections_successives_sont_numerotees()
    {
        var loc = AvecFactureEmise();

        for (int attendu = 1; attendu <= 3; attendu++)
        {
            var d = FactureEmission.Preparer(loc, Key, "ignoré");
            Assert.That(d.NumeroCorrection, Is.EqualTo(attendu));
            Assert.That(d.SuffixeFichier, Is.EqualTo($"-corrigee{attendu}"));
            FactureEmission.Enregistrer(loc, Key, "Regul", d, "Régularisation 2026",
                Iso(-2), $"Batiment/X/Dupont/Facture/regul-c{attendu}.pdf", 1200f, "rib1",
                loc.factureRegul, "Régularisation");
        }

        Assert.That(loc.factureSeq, Is.EqualTo(2), "une seule séquence consommée en tout");
    }

    [Test]
    public void Un_loyer_prepare_en_avance_est_aussi_protege()
    {
        // Le cas qui échappait à la garde : échéance lointaine → « En attente d'envoi ».
        var loc = AvecFactureEmise(Iso(60));
        Assert.That(FactureEmission.Preparer(loc, Key, "2026/09002").Correction, Is.True);
    }

    // ── Messages ────────────────────────────────────────────────────────────

    [Test]
    public void Chaque_panneau_garde_sa_formulation_en_premiere_emission()
    {
        var loc = NouveauLocataire();
        var d = FactureEmission.Preparer(loc, Key, "2026/09001");

        string message = FactureEmission.Enregistrer(loc, Key, "Regul", d, "Régularisation 2026",
            Iso(-2), "Batiment/X/Dupont/Facture/regul.pdf", 1200f, "rib1", loc.factureRegul,
            "Régularisation", "Régularisation enregistrée · charges passées en payé.");

        Assert.That(message, Is.EqualTo("Régularisation enregistrée · charges passées en payé."));
    }

    // ── Sens de la somme : remboursement au locataire ───────────────────────

    [Test]
    public void Solde_negatif_la_phrase_de_reglement_s_inverse()
    {
        // « SOMME À NOUS RÉGLER LE … » est imprimé en gras sous les totaux :
        // c'est la ligne la plus lue du document, elle ne peut pas être fausse.
        string auto = "SOMME À NOUS RÉGLER LE 31 juillet 2025";

        Assert.That(FactureEmission.PhraseSomme(auto, -281.80f),
                    Is.EqualTo("SOMME QUI VOUS SERA REMBOURSÉE"));
        Assert.That(FactureEmission.PhraseSomme("", -281.80f),
                    Is.EqualTo("SOMME QUI VOUS SERA REMBOURSÉE"));
    }

    [Test]
    public void Solde_positif_la_phrase_de_reglement_est_intacte()
    {
        string auto = "SOMME À NOUS RÉGLER LE 31 juillet 2025";
        Assert.That(FactureEmission.PhraseSomme(auto, 281.80f), Is.EqualTo(auto));
        Assert.That(FactureEmission.PhraseSomme(auto, 0f), Is.EqualTo(auto), "ni sur un solde nul");
    }

    [Test]
    public void Une_phrase_ecrite_a_la_main_est_respectee()
    {
        // L'utilisatrice sait ce qu'elle écrit : on ne réécrit que la phrase auto.
        const string perso = "Valeur en votre aimable règlement";
        Assert.That(FactureEmission.PhraseSomme(perso, -281.80f), Is.EqualTo(perso));
    }

    [Test]
    public void Le_libelle_du_total_suit_le_sens_de_la_somme()
    {
        Assert.That(FactureEmission.LibelleSolde(-281.80f, "Solde H.T."),
                    Is.EqualTo("Montant à vous rembourser"));
        Assert.That(FactureEmission.LibelleSolde(281.80f, "Solde H.T."), Is.EqualTo("Solde H.T."));
        Assert.That(FactureEmission.LibelleSolde(-0.004f, "Complément à régler"),
                    Is.EqualTo("Complément à régler"), "un arrondi au centime n'inverse rien");
    }

    [Test]
    public void Le_message_de_correction_indique_le_rang()
    {
        var loc = AvecFactureEmise();
        var d = FactureEmission.Preparer(loc, Key, "ignoré");

        string message = FactureEmission.Enregistrer(loc, Key, "Regul", d, "Régularisation 2026",
            Iso(-2), "Batiment/X/Dupont/Facture/regul-c1.pdf", 1200f, "rib1", loc.factureRegul,
            "Régularisation");

        Assert.That(message, Does.Contain("corrigée (1)"));
    }
}
