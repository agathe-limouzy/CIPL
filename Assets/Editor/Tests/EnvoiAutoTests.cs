using System;
using NUnit.Framework;

/// Ce qui part tout seul, et ce qui ne part surtout pas.
///
/// Un envoi ne se rappelle pas : la règle qui décide des factures proposées au
/// lancement mérite d'être tenue par des tests plutôt que par la relecture. Elle est
/// volontairement séparée de l'écran, et l'existence du PDF lui est injectée — les
/// assertions ci-dessous ne touchent pas au disque.
public class EnvoiAutoTests
{
    const string Pdf = "Batiment/Test/Dupont/Facture/loyer.pdf";

    static string Iso(int jours) => DateTime.Today.AddDays(jours).ToString("yyyy-MM-dd");

    static Locataire Loc(string email = "locataire@exemple.fr")
        => new Locataire { Name = "Dupont", emailLocataire = email };

    static FactureEtat Ligne(string statut, int joursAvantEcheance, string pdf = Pdf)
        => new FactureEtat
        {
            key = "loyer-2026-P3", type = "Loyer", libelle = "Loyer 3e trimestre 2026",
            statut = statut, echeanceISO = Iso(joursAvantEcheance), pdfPath = pdf, montant = 3000f
        };

    static bool Part(Locataire loc, FactureEtat rec, bool pdfPresent = true)
        => FactureEnvoiAuto.DoitPartir(loc, rec, DateTime.Today, _ => pdfPresent, out _, out _);

    // ── Ce qui part ─────────────────────────────────────────────────────────────

    [Test]
    public void Une_facture_en_attente_dont_la_date_d_envoi_est_atteinte_part()
    {
        // Échéance dans 15 jours : c'est exactement le jour d'envoi.
        Assert.That(Part(Loc(), Ligne("AttenteEnvoi", 15)), Is.True);
    }

    [Test]
    public void Une_facture_en_retard_est_rattrapee()
    {
        // L'application doit être ouverte pour que quoi que ce soit parte : si elle ne
        // l'a pas été le jour dit, la facture doit partir au lancement suivant.
        Assert.That(Part(Loc(), Ligne("AttenteEnvoi", 3)), Is.True, "date d'envoi dépassée");
        Assert.That(Part(Loc(), Ligne("AttenteEnvoi", -30)), Is.True, "échéance dépassée");
    }

    // ── Ce qui ne part pas ──────────────────────────────────────────────────────

    [Test]
    public void Une_facture_trop_tot_ne_part_pas()
    {
        // Échéance dans 16 jours : la veille du jour d'envoi, rien ne bouge.
        Assert.That(Part(Loc(), Ligne("AttenteEnvoi", 16)), Is.False);
    }

    [Test]
    public void Une_facture_deja_envoyee_ne_repart_pas()
    {
        Assert.That(Part(Loc(), Ligne("Envoye", 5)), Is.False);
        Assert.That(Part(Loc(), Ligne("Paye", 5)), Is.False);
        Assert.That(Part(Loc(), Ligne("Impaye", -20)), Is.False);
        Assert.That(Part(Loc(), Ligne("Cloture", 5)), Is.False, "période reprise : historique");
    }

    [Test]
    public void Une_ligne_sans_facture_generee_ne_part_pas()
    {
        // Statut vide = rien n'a été généré, il n'y aurait pas de PDF à joindre.
        Assert.That(Part(Loc(), Ligne("", 5)), Is.False);
        Assert.That(Part(Loc(), Ligne("AFaire", 5)), Is.False);
    }

    [Test]
    public void Un_PDF_absent_du_disque_empeche_l_envoi()
    {
        // Envoyer une facture sans sa pièce jointe serait pire que ne rien envoyer.
        Assert.That(Part(Loc(), Ligne("AttenteEnvoi", 5), pdfPresent: false), Is.False);
        Assert.That(Part(Loc(), Ligne("AttenteEnvoi", 5, pdf: "")), Is.False);
    }

    [Test]
    public void Sans_destinataire_rien_ne_part()
    {
        Assert.That(Part(Loc(email: ""), Ligne("AttenteEnvoi", 5)), Is.False);
        Assert.That(Part(Loc(email: "   "), Ligne("AttenteEnvoi", 5)), Is.False);
    }

    [Test]
    public void Une_echeance_illisible_ne_part_pas()
    {
        var rec = Ligne("AttenteEnvoi", 5);
        rec.echeanceISO = "pas une date";
        Assert.That(Part(Loc(), rec), Is.False);
    }

    // ── Destinataire ────────────────────────────────────────────────────────────

    [Test]
    public void L_adresse_memorisee_sur_la_facture_prime_sur_celle_de_la_fiche()
    {
        // Même ordre que le panneau de facture : l'adresse saisie pour l'envoi gagne,
        // sinon on retombe sur celle de la fiche locataire.
        var loc = Loc();
        loc.factureLoyer = new FactureInfo { emailDest = "compta@exemple.fr" };
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Loyer"), Is.EqualTo("compta@exemple.fr"));

        loc.factureLoyer.emailDest = "";
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Loyer"), Is.EqualTo("locataire@exemple.fr"));
    }

    [Test]
    public void Chaque_type_de_facture_a_son_propre_destinataire()
    {
        var loc = Loc();
        loc.factureRegul = new FactureInfo { emailDest = "charges@exemple.fr" };
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Regul"), Is.EqualTo("charges@exemple.fr"));
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Loyer"), Is.EqualTo("locataire@exemple.fr"),
            "le réglage de régularisation ne doit pas déteindre sur le loyer");
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Inconnu"), Is.EqualTo("locataire@exemple.fr"));
    }
}
