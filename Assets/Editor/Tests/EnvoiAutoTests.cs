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

    // ── Date d'envoi : le panneau n'expédie pas en avance ───────────────────────
    //
    // Le bouton « Sauvegarder et envoyer » expédiait sur-le-champ, quelle que soit la
    // date : préparer une facture un mois à l'avance l'envoyait un mois à l'avance, et
    // un loyer parti trop tôt ne se rappelle pas.

    [Test]
    public void La_date_d_envoi_d_un_loyer_est_quinze_jours_avant_l_echeance()
    {
        var d = FacturationSuivi.DateEnvoi("Loyer", "2026-10-08");
        Assert.That(d, Is.EqualTo(new DateTime(2026, 9, 23)));
    }

    [Test]
    public void Les_autres_types_n_ont_pas_de_date_d_envoi()
    {
        // Une régularisation ou une révision de dépôt s'envoie quand on la fait.
        Assert.That(FacturationSuivi.DateEnvoi("Regul", "2026-10-08"), Is.Null);
        Assert.That(FacturationSuivi.DateEnvoi("Refac", "2026-10-08"), Is.Null);
        Assert.That(FacturationSuivi.DateEnvoi("Depot", "2026-10-08"), Is.Null);
    }

    [Test]
    public void Une_echeance_illisible_ne_donne_pas_de_date_d_envoi()
    {
        Assert.That(FacturationSuivi.DateEnvoi("Loyer", "pas une date"), Is.Null);
        Assert.That(FacturationSuivi.DateEnvoi("Loyer", ""), Is.Null);
    }

    [Test]
    public void Un_loyer_ne_part_pas_avant_sa_date_d_envoi()
    {
        // Le cas vécu : échéance au 8 octobre, date d'envoi le 23 septembre.
        var veille = new DateTime(2026, 9, 22);
        Assert.That(FacturationSuivi.PeutPartir("Loyer", "2026-10-08", veille), Is.False);

        var jourJ = new DateTime(2026, 9, 23);
        Assert.That(FacturationSuivi.PeutPartir("Loyer", "2026-10-08", jourJ), Is.True);
    }

    [Test]
    public void Un_loyer_en_retard_peut_toujours_partir()
    {
        Assert.That(FacturationSuivi.PeutPartir("Loyer", "2026-10-08", new DateTime(2026, 10, 20)),
                    Is.True, "une facture en retard doit pouvoir partir");
    }

    [Test]
    public void Les_types_sans_date_d_envoi_partent_toujours()
    {
        Assert.That(FacturationSuivi.PeutPartir("Regul", "2026-10-08", new DateTime(2026, 1, 1)), Is.True);
        Assert.That(FacturationSuivi.PeutPartir("Loyer", "illisible", new DateTime(2026, 1, 1)), Is.True,
                    "sans échéance exploitable, on ne peut rien interdire");
    }

    // ── La ligne cliquée désigne la période à ouvrir ────────────────────────────
    //
    // « Générer » et « Refaire » ouvraient le panneau sur la dernière période éditée :
    // on cliquait sur mars et on modifiait avril. Chaque type lit sa cible dans la
    // clé de la ligne.

    [Test]
    public void La_cle_d_une_regularisation_donne_son_annee()
    {
        Assert.That(FactureRegulPanel.AnneeDeCle("regul-2025"), Is.EqualTo(2025));
        Assert.That(FactureRegulPanel.AnneeDeCle("loyer-2026-P3"), Is.Zero, "autre type");
        Assert.That(FactureRegulPanel.AnneeDeCle("regul-"), Is.Zero);
        Assert.That(FactureRegulPanel.AnneeDeCle(null), Is.Zero);
    }

    /// Un identifiant de charge est un GUID : il contient des tirets. Découper la clé
    /// avec `Split('-')` ne rendrait que « 3f2a », donc une charge introuvable — et le
    /// panneau retomberait silencieusement sur une autre charge.
    [Test]
    public void La_cle_d_une_refacturation_garde_l_identifiant_entier()
    {
        const string guid = "3f2a1b4c-8d90-4e11-9a77-0c5e2b6d7f88";
        Assert.That(FactureRefacPanel.ChargeDeCle("refac-" + guid), Is.EqualTo(guid));
    }

    [Test]
    public void Une_cle_d_un_autre_type_ne_designe_aucune_charge()
    {
        Assert.That(FactureRefacPanel.ChargeDeCle("depot-2026"), Is.Null);
        Assert.That(FactureRefacPanel.ChargeDeCle(""), Is.Null);
        Assert.That(FactureRefacPanel.ChargeDeCle(null), Is.Null);
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
