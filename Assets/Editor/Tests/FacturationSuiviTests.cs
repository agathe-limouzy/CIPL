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

    static Locataire LocataireAvecFactureEmise(string echeanceISO, string statutForce = null,
                                               bool envoye = true)
    {
        var loc = new Locataire();
        // Chemin SOUS la racine courante : c'est la condition pour qu'il soit
        // relativise. Aucun fichier n'est ecrit, on ne calcule qu'un chemin.
        string pdf = System.IO.Path.Combine(
            DossiersDonnees.DossierFactures("Test", "Dupont"), "loyer.pdf");
        FacturationSuivi.MarquerEnvoye(loc, Key, "Loyer", "Loyer 3e trimestre 2026",
            echeanceISO, "2026/09001", pdf, 3000f, "rib1", "Banque", envoye);
        if (statutForce != null) loc.facturesEtat.Find(x => x.key == Key).statut = statutForce;
        return loc;
    }

    // ── Paiements partiels (08/10) ───────────────────────────────────────────

    [Test]
    public void Un_paiement_partiel_laisse_le_reste_du()
    {
        var loc = LocataireAvecFactureEmise(Iso(-2));   // 3 000 €, envoyée
        var rec = loc.facturesEtat.Find(x => x.key == Key);
        rec.versements.Add(new Versement { dateISO = Iso(-1), montant = 1000f });
        rec.versements.Add(new Versement { dateISO = Iso(0), montant = 500f });

        FacturationSuivi.Solde(loc, Key, out float total, out float paye);
        Assert.That(total, Is.EqualTo(3000f));
        Assert.That(paye, Is.EqualTo(1500f));
        Assert.That(FacturationSuivi.EtatAffiche(loc, rec), Is.EqualTo(FacturationSuivi.Etat.Partiel));
        Assert.That(FacturationSuivi.EtatDe(rec), Is.EqualTo(FacturationSuivi.Etat.Envoye), "Partiel n'est qu'un affichage");

        var dues = FacturationSuivi.DuesDe(loc);
        Assert.That(dues, Has.Count.EqualTo(1));
        Assert.That(dues[0].montant, Is.EqualTo(1500f), "créances, décompte, archivage : le reste");
    }

    [Test]
    public void Une_facture_partielle_echue_affiche_partiel_mais_reste_impayee()
    {
        // Retour du 08/10 : la pastille dit « Partiel » dès qu'un versement existe ; l'état
        // réel reste Impayé (Créances, filtre, rappel).
        var loc = LocataireAvecFactureEmise(Iso(-20));   // échéance + 15 j dépassée
        var rec = loc.facturesEtat.Find(x => x.key == Key);
        rec.versements.Add(new Versement { dateISO = Iso(-5), montant = 1000f });
        Assert.That(FacturationSuivi.EtatAffiche(loc, rec), Is.EqualTo(FacturationSuivi.Etat.Partiel));
        Assert.That(FacturationSuivi.EtatDe(rec), Is.EqualTo(FacturationSuivi.Etat.Impaye));
        Assert.That(FacturationSuivi.DuesDe(loc)[0].montant, Is.EqualTo(2000f));
    }

    [Test]
    public void Le_statut_suit_les_versements()
    {
        // Tout réglé → Payé ; on retire un versement → Envoyé ; un « Payé » mis à la main
        // sans versement n'est pas défait en rouvrant la fenêtre.
        Assert.That(FacturationSuivi.StatutApresVersements("Envoye", 3000f, 1500f, 3000f), Is.EqualTo("Paye"));
        Assert.That(FacturationSuivi.StatutApresVersements("Impaye", 3000f, 0f, 1000f), Is.EqualTo("Impaye"));
        Assert.That(FacturationSuivi.StatutApresVersements("Paye", 3000f, 3000f, 2000f), Is.EqualTo("Envoye"));
        Assert.That(FacturationSuivi.StatutApresVersements("Paye", 3000f, 0f, 0f), Is.EqualTo("Paye"));
    }

    [Test]
    public void Un_versement_doit_etre_positif_et_ne_pas_depasser_le_reste()
    {
        Assert.That(FacturationSuivi.VerifierVersement(0f, 500f), Is.Not.Null);
        Assert.That(FacturationSuivi.VerifierVersement(600f, 500f), Does.Contain("500,00"));
        Assert.That(FacturationSuivi.VerifierVersement(500f, 500f), Is.Null);
    }

    [Test]
    public void Une_facture_sur_plusieurs_lignes_porte_ses_versements_sur_la_premiere()
    {
        // Régul regroupée : deux lignes, un seul PDF, une seule créance.
        var loc = LocataireAvecFactureEmise(Iso(-2));
        var premiere = loc.facturesEtat[0];
        loc.facturesEtat.Add(new FactureEtat { key = "regul-b", type = "Loyer", statut = "Envoye",
            echeanceISO = premiere.echeanceISO, pdfPath = premiere.pdfPath, montant = 1000f });
        premiere.versements.Add(new Versement { dateISO = Iso(0), montant = 1000f });

        Assert.That(FacturationSuivi.Porteuse(loc, "regul-b"), Is.SameAs(premiere));
        FacturationSuivi.Solde(loc, "regul-b", out float total, out float paye);
        Assert.That(total, Is.EqualTo(4000f));
        Assert.That(paye, Is.EqualTo(1000f));
        Assert.That(FacturationSuivi.EtatAffiche(loc, loc.facturesEtat[1]), Is.EqualTo(FacturationSuivi.Etat.Partiel));
        var dues = FacturationSuivi.DuesDe(loc);
        Assert.That(dues, Has.Count.EqualTo(1));
        Assert.That(dues[0].montant, Is.EqualTo(3000f));
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
        // Cas NORMAL du panneau Loyer : PDF généré, rien d'envoyé, donc « En attente
        // d'envoi ». Il échappait à la garde : un second clic consommait une nouvelle
        // séquence ET écrasait le PDF déjà généré.
        var loc = LocataireAvecFactureEmise(Iso(60), envoye: false);
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
        FacturationSuivi.MarquerEnvoye(loc, Key, "Loyer", "Loyer", Iso(-5), "", "", 0f, "", "", true);
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
        var loc = LocataireAvecFactureEmise(Iso(60), envoye: false);
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

    // ── « Envoyé » veut dire envoyé (22/09/2026) ────────────────────────────
    //
    // Le statut se déduisait de la date : une facture préparée à plus de 15 j de
    // l'échéance était « en attente », puis `EtatDe` la basculait toute seule à J-15.
    // Le suivi annonçait donc des envois qui n'avaient jamais eu lieu — y compris
    // quand l'envoi par email n'était même pas activé.

    [Test]
    public void Une_facture_generee_sans_envoi_reste_en_attente_d_envoi()
    {
        var loc = LocataireAvecFactureEmise(Iso(60), envoye: false);
        var rec = loc.facturesEtat.Find(x => x.key == Key);
        Assert.That(rec.statut, Is.EqualTo("AttenteEnvoi"));
        Assert.That(FacturationSuivi.EtatDe(rec), Is.EqualTo(FacturationSuivi.Etat.AttenteEnvoi));
    }

    [Test]
    public void Une_facture_non_envoyee_ne_devient_pas_envoyee_avec_le_temps()
    {
        // Échéance dans 5 jours : la date d'envoi (J-15) est dépassée sans que rien
        // ne soit parti. C'est précisément le cas qui affichait « Envoyé » à tort.
        var loc = LocataireAvecFactureEmise(Iso(5), envoye: false);
        var rec = loc.facturesEtat.Find(x => x.key == Key);
        Assert.That(FacturationSuivi.EtatDe(rec), Is.EqualTo(FacturationSuivi.Etat.AttenteEnvoi));
    }

    [Test]
    public void Une_facture_jamais_envoyee_ne_devient_pas_impayee()
    {
        // On ne peut pas reprocher un impayé à qui n'a jamais reçu sa facture.
        var loc = LocataireAvecFactureEmise(Iso(-40), envoye: false);
        var rec = loc.facturesEtat.Find(x => x.key == Key);
        Assert.That(FacturationSuivi.EtatDe(rec), Is.EqualTo(FacturationSuivi.Etat.AttenteEnvoi));
    }

    [Test]
    public void Une_facture_non_envoyee_garde_son_alerte_allumee()
    {
        // Le filet contre l'oubli : tant que rien n'est parti, le rappel reste.
        var loc = LocataireAvecFactureEmise(Iso(5), envoye: false);
        Assert.That(FacturationSuivi.DejaTraite(loc, Key), Is.False);

        // …mais elle compte toujours comme émise : le numéro est consommé, un second
        // clic ne doit pas en prendre un autre (non-régression H2-bis).
        Assert.That(FacturationSuivi.EstDejaEmise(loc, Key, out _), Is.True);
    }

    [Test]
    public void Un_envoi_reel_marque_la_facture_envoyee()
    {
        var loc = LocataireAvecFactureEmise(Iso(60), envoye: true);
        var rec = loc.facturesEtat.Find(x => x.key == Key);
        Assert.That(rec.statut, Is.EqualTo("Envoye"));
        Assert.That(FacturationSuivi.EtatDe(rec), Is.EqualTo(FacturationSuivi.Etat.Envoye));
        Assert.That(FacturationSuivi.DejaTraite(loc, Key), Is.True, "l'alerte peut s'éteindre");
    }

    [Test]
    public void L_utilisatrice_peut_forcer_Envoye_a_la_main()
    {
        // Soupape indispensable : la facture peut partir autrement (Pennylane,
        // courrier, remise en main propre). Le menu de la pastille doit suffire.
        var loc = LocataireAvecFactureEmise(Iso(5), envoye: false);
        loc.facturesEtat.Find(x => x.key == Key).statut = "Envoye";
        var rec = loc.facturesEtat.Find(x => x.key == Key);
        Assert.That(FacturationSuivi.EtatDe(rec), Is.EqualTo(FacturationSuivi.Etat.Envoye));
    }

    // ── Une ligne ambre sans PDF est un piège (22/09/2026) ──────────────────

    [Test]
    public void Une_ligne_en_attente_sans_PDF_retombe_dans_le_cycle_normal()
    {
        // Constaté sur une fiche réelle : « en attente d'envoi » sans PDF, donc rien
        // à envoyer. Elle dormait en ambre, absente des récapitulatifs comme des
        // alertes. Il y a bien une facture à produire : elle doit dire « À faire ».
        var proche = new FactureEtat { key = Key, type = "Loyer", statut = "AttenteEnvoi",
                                       echeanceISO = Iso(5), pdfPath = "" };
        Assert.That(FacturationSuivi.EtatDe(proche), Is.EqualTo(FacturationSuivi.Etat.AFaire));

        // Loin de l'échéance, elle n'est pas encore à faire : « À venir ».
        var lointaine = new FactureEtat { key = Key, type = "Loyer", statut = "AttenteEnvoi",
                                          echeanceISO = Iso(200), pdfPath = "" };
        Assert.That(FacturationSuivi.EtatDe(lointaine), Is.EqualTo(FacturationSuivi.Etat.AVenir));
    }

    [Test]
    public void Une_ligne_en_attente_avec_PDF_reste_en_attente()
    {
        // Non-régression : le cas normal ne doit pas changer.
        var loc = LocataireAvecFactureEmise(Iso(5), envoye: false);
        var rec = loc.facturesEtat.Find(x => x.key == Key);
        Assert.That(FacturationSuivi.EtatDe(rec), Is.EqualTo(FacturationSuivi.Etat.AttenteEnvoi));
    }

    // ── Échéance figée ≠ modalités actuelles (22/09/2026) ───────────────────
    //
    // Préparer le loyer d'octobre au 8, puis passer le jour de demande au 7 : la
    // facture garde le 8, c'est ce que porte son PDF. Mais l'écart doit SE VOIR,
    // sinon la date d'envoi calculée dessus paraît fausse.

    [Test]
    public void L_ecart_entre_l_echeance_figee_et_les_modalites_est_signale()
    {
        var loc = new Locataire { jourDemandeLoyer = 7, periodiciteLoyer = Periodicite.mensuel };
        loc.facturesEtat.Add(new FactureEtat { key = "loyer-2026-P10", type = "Loyer",
            statut = "AttenteEnvoi", echeanceISO = "2026-10-08", pdfPath = "x.pdf" });

        var attendue = FacturationSuivi.EcheanceAttendue(loc, loc.facturesEtat[0]);
        Assert.That(attendue, Is.EqualTo(new DateTime(2026, 10, 7)));
    }

    [Test]
    public void Aucun_ecart_signale_quand_l_echeance_correspond()
    {
        var loc = new Locataire { jourDemandeLoyer = 8, periodiciteLoyer = Periodicite.mensuel };
        loc.facturesEtat.Add(new FactureEtat { key = "loyer-2026-P10", type = "Loyer",
            statut = "AttenteEnvoi", echeanceISO = "2026-10-08", pdfPath = "x.pdf" });

        Assert.That(FacturationSuivi.EcheanceAttendue(loc, loc.facturesEtat[0]), Is.Null);
    }

    [Test]
    public void Une_facture_partie_ne_signale_aucun_ecart()
    {
        // Sa date est gravée chez le locataire : la changer n'aurait aucun sens.
        var loc = new Locataire { jourDemandeLoyer = 7, periodiciteLoyer = Periodicite.mensuel };
        foreach (var statut in new[] { "Envoye", "Impaye", "Paye" })
        {
            var rec = new FactureEtat { key = "loyer-2026-P10", type = "Loyer",
                statut = statut, echeanceISO = "2026-10-08", pdfPath = "x.pdf" };
            Assert.That(FacturationSuivi.EcheanceAttendue(loc, rec), Is.Null, statut);
        }
    }

    [Test]
    public void Les_autres_types_ne_signalent_aucun_ecart()
    {
        // Seul le loyer a une échéance dérivée d'un jour de demande.
        var loc = new Locataire { jourDemandeLoyer = 7, periodiciteLoyer = Periodicite.mensuel };
        var rec = new FactureEtat { key = "regul-2025", type = "Regul",
            statut = "AttenteEnvoi", echeanceISO = "2026-01-31", pdfPath = "x.pdf" };
        Assert.That(FacturationSuivi.EcheanceAttendue(loc, rec), Is.Null);
        Assert.That(FacturationSuivi.EcheanceAttendue(null, rec), Is.Null);
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
