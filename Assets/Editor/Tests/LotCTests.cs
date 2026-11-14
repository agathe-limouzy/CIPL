using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot C (reprise du 09/11/2026) : la facturation, lot par lot (REVUE_17/PLAN_2C.md). Un test par défaut corrigé,
/// règle pure d'abord, texte du code à défaut.
public class LotCTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));

    // ── Lot 6 · D4-01 : destinataire et acheteur gardés sur la facture (+ D4-N3, D4-N4/D5-N5, D4-13, D4-15) ──

    static Locataire SarlX()
        => new Locataire { id = "LC6", Name = "SARL X", siretNumber = "11111111111111", adresseLocataire = "1 rue X", emailLocataire = "x@x.fr",
                           periodiciteLoyer = Periodicite.mensuel, loyerAnnuel = 12000f, facturesEtat = new List<FactureEtat>() };

    /// Décision du 05/11 : la facture du titulaire de la fiche suit l'adresse actuelle ; celle d'un cédant garde la sienne (rappel compris).
    [Test]
    public void D4_01_la_facture_d_un_cedant_garde_son_adresse_celle_de_la_fiche_suit_l_adresse_actuelle()
    {
        var loc = SarlX();
        loc.factureLoyer = new FactureInfo { emailDest = "actuel@x.fr" };
        var rec = new FactureEtat { key = "loyer-2026-P9", type = "Loyer", statut = "Impaye", numero = "2026/09001", pdfPath = "x.pdf",
                                    echeanceISO = "2026-09-05", acheteur = "SARL X", destinataire = "compta@x.fr" };
        loc.facturesEtat.Add(rec);
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Loyer", rec), Is.EqualTo("actuel@x.fr"), "titulaire de la fiche : l'adresse actuelle, pas celle gardée");
        Assert.That(FactureRappelService.Adresse(loc, rec), Is.EqualTo("actuel@x.fr"));

        var le = new DateTime(2026, 11, 2);
        Assert.That(Cessions.Enregistrer(loc, le, "SARL Y", "22222222222222", "2 rue Y", "y@y.fr", le), Is.True, "cession faite le jour même");
        Assert.That(loc.Name, Is.EqualTo("SARL Y"));
        Assert.That(loc.factureLoyer.emailDest, Is.Empty, "la cession efface le réglage du type (D7a-05)");
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Loyer", rec), Is.EqualTo("compta@x.fr"), "facture du cédant : l'adresse gardée sur la facture");
        Assert.That(FactureRappelService.Adresse(loc, rec), Is.EqualTo("compta@x.fr"), "le rappel de l'impayé du cédant va au cédant (D4-N4)");
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Loyer"), Is.EqualTo("y@y.fr"), "sans ligne : la fiche");
        rec.destinataire = "";
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Loyer", rec), Is.EqualTo("x@x.fr"), "sans adresse gardée : l'e-mail du cédant dans sa cession");
        CollectionAssert.AreEqual(new[] { "SARL X", "11111111111111", "1 rue X", "x@x.fr" }, Cessions.AcheteurDe(loc, rec), "la correction reprend le cédant");
        Assert.That(Cessions.AcheteurDe(loc, new FactureEtat { key = "regul-2026", type = "Regul" }), Is.Null, "sans acheteur : la fiche");
    }

    /// Relecture du lot 6 (plan D4-01 (g)) : seul le nom d'une cession fait un autre titulaire. Une fiche renommée sans cession garde
    /// la règle de la fiche pour ses factures préparées sous l'ancien nom : le loyer en attente part, à l'adresse actuelle ; le
    /// rappel aussi ; la quittance et les formulaires reprennent la fiche. Avant, elles étaient retenues « à refaire », rappelées
    /// à l'adresse figée, et la quittance sortait sans adresse.
    [Test]
    public void D4_01_une_fiche_renommee_sans_cession_garde_la_regle_de_la_fiche()
    {
        var loc = SarlX();
        loc.factureLoyer = new FactureInfo { emailDest = "actuel@x.fr" };
        var rec = new FactureEtat { key = "loyer-2026-P12", type = "Loyer", libelle = "Loyer Décembre 2026", statut = "AttenteEnvoi", numero = "2026/11001",
                                    pdfPath = "Batiment/Test/SARL X/Facture/l.pdf", echeanceISO = "2026-12-01", acheteur = "SARL X", destinataire = "compta@x.fr", montant = 1000f };
        loc.facturesEtat.Add(rec);
        loc.Name = "SAS X";   // faute corrigée sur la fiche, sans cession
        Assert.That(Cessions.AcheteurDe(loc, rec), Is.Null, "l'ancien nom n'est celui d'aucune cession : la facture reste celle de la fiche (formulaires : la fiche)");
        Assert.That(Cessions.AutreTitulaire(loc, rec), Is.False);
        Assert.That(Cessions.HorsTitulaire(loc, rec), Is.False, "rien à refaire");
        Assert.That(FactureEnvoiAuto.DoitPartir("Test", loc, rec, new DateTime(2026, 11, 16), _ => true, out string dest, out _), Is.True, "le loyer en attente part");
        Assert.That(dest, Is.EqualTo("actuel@x.fr"), "à l'adresse actuelle, pas celle gardée");
        Assert.That(FactureRappelService.Adresse(loc, rec), Is.EqualTo("actuel@x.fr"));
        Assert.That(FactureQuittanceService.Client(loc, rec), Is.EqualTo(("SAS X", "1 rue X")), "la quittance : la fiche, avec son adresse");

        var le = new DateTime(2026, 11, 20);
        Cessions.Enregistrer(loc, le, "SARL Y", "", "", "y@y.fr", le);   // puis une vraie cession : SAS X devient cédant
        CollectionAssert.AreEqual(new[] { "SAS X", "11111111111111", "1 rue X", "compta@x.fr" }, Cessions.AcheteurDe(loc, new FactureEtat { acheteur = "SAS X", destinataire = "compta@x.fr" }),
            "le nom d'une cession faite : l'acheteur, avec l'adresse gardée");
        Assert.That(Cessions.AcheteurDe(loc, rec), Is.Null, "l'ancien nom reste hors du bail");
    }

    /// Les lignes d'avant la mise à jour sont figées dès la saisie d'une cession, même à venir : le nom du titulaire et l'adresse
    /// d'envoi du jour sur toute ligne qui porte un document (relecture du lot 6 : un loyer pas encore parti aussi).
    [Test]
    public void D4_01_les_lignes_d_avant_sont_figees_a_la_saisie_de_la_cession_meme_prevue()
    {
        var loc = SarlX();
        loc.factureRegul = new FactureInfo { emailDest = "charges@x.fr" };
        var regul = new FactureEtat { key = "regul-2025", type = "Regul", statut = "Envoye", numero = "2026/02001", pdfPath = "r.pdf", echeanceISO = "2026-03-01" };
        var attente = new FactureEtat { key = "loyer-2026-P12", type = "Loyer", statut = "AttenteEnvoi", numero = "2026/11001", pdfPath = "l.pdf", echeanceISO = "2026-12-01" };
        var sansPdf = new FactureEtat { key = "loyer-2026-P10", type = "Loyer", statut = "Paye" };
        loc.facturesEtat.AddRange(new[] { regul, attente, sansPdf });

        Assert.That(Cessions.Enregistrer(loc, new DateTime(2026, 12, 1), "SARL Y", "", "", "y@y.fr", new DateTime(2026, 11, 9)), Is.False, "cession à venir");
        Assert.That(regul.destinataire, Is.EqualTo("charges@x.fr"), "facture partie : l'adresse du jour, figée avant que la cession ne l'efface");
        Assert.That(regul.acheteur, Is.EqualTo("SARL X"));
        Assert.That(attente.acheteur, Is.EqualTo("SARL X"), "un loyer préparé avant la saisie de la cession : au nom du titulaire d'alors");
        Assert.That(attente.destinataire, Is.EqualTo("x@x.fr"), "pas partie : l'adresse du jour aussi, pour le jour où elle devient celle d'un cédant");
        Assert.That(sansPdf.acheteur, Is.Null.Or.Empty, "sans document : rien à figer");
        Assert.That(Cessions.HorsTitulaire(loc, attente), Is.True, "décembre revient au cessionnaire : le loyer préparé au nom du cédant est à refaire");
        loc.factureRegul.emailDest = "charges2@x.fr";
        Assert.That(FactureRappelService.Adresse(loc, regul), Is.EqualTo("charges2@x.fr"), "tant que la fiche est au cédant, sa facture suit l'adresse actuelle, pas celle figée");

        Cessions.Effectuer(loc, new DateTime(2026, 12, 1));
        Assert.That(loc.Name, Is.EqualTo("SARL Y"));
        Assert.That(FactureRappelService.Adresse(loc, regul), Is.EqualTo("charges@x.fr"), "le rappel de la régularisation du cédant");
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Regul"), Is.EqualTo("y@y.fr"), "une nouvelle régularisation part au cessionnaire");
    }

    /// Décision du 05/11 : un loyer préparé au nom du cédant avant la saisie de la cession est retenu et signalé ; refait au même
    /// numéro au nom du cessionnaire, il part, à l'adresse tapée pour lui (D4-N3), sinon à celle de la cession.
    [Test]
    public void D4_01_un_loyer_prepare_au_nom_du_cedant_avant_la_cession_ne_part_pas()
    {
        var loc = SarlX();
        var rec = new FactureEtat { key = "loyer-2026-P12", type = "Loyer", libelle = "Loyer Décembre 2026", statut = "AttenteEnvoi", numero = "2026/11001",
                                    pdfPath = "Batiment/Test/SARL X/Facture/l.pdf", echeanceISO = "2026-12-01", acheteur = "SARL X", montant = 1000f };
        loc.facturesEtat.Add(rec);
        var le = new DateTime(2026, 11, 16);
        bool Part(out string dest) => FactureEnvoiAuto.DoitPartir("Test", loc, rec, le, _ => true, out dest, out _);
        Assert.That(Part(out _), Is.True, "avant la cession : prêt");

        Cessions.Enregistrer(loc, new DateTime(2026, 12, 1), "SARL Y", "", "", "y@y.fr", le);
        Assert.That(Cessions.HorsTitulaire(loc, rec), Is.True);
        Assert.That(Part(out _), Is.False, "préparé au nom du cédant pour une période du cessionnaire : retenu");
        Assert.That(rec.destinataire, Is.EqualTo("x@x.fr"), "l'adresse du jour est figée avec le nom (relecture du lot 6) ; retenu, il ne part pas avec");

        rec.acheteur = "SARL Y"; rec.destinataire = "";   // « Refaire » : même numéro, au nom du titulaire (PoserAcheteur repose aussi l'adresse tapée)
        Assert.That(Cessions.HorsTitulaire(loc, rec), Is.False);
        Assert.That(Part(out string d1), Is.True);
        Assert.That(d1, Is.EqualTo("y@y.fr"), "sans adresse tapée : celle de la cession");
        rec.destinataire = "compta@y.fr";
        Part(out string d2);
        Assert.That(d2, Is.EqualTo("compta@y.fr"), "l'adresse tapée pour le cessionnaire, gardée sur la facture (D4-N3)");

        rec.acheteur = "";
        Assert.That(Part(out _), Is.True, "ligne d'avant la mise à jour, sans acheteur : rien à comparer, comme avant");
    }

    /// D4-N3 : ce que le formulaire du loyer tape pour une période d'une cession prévue va dans cette cession (un nom ou un e-mail
    /// laissé vide n'y efface rien ; une cession faite n'est pas réécrite : relecture du lot 6) ; sans adresse, l'envoi différé est
    /// refusé avant d'être enregistré.
    [Test]
    public void D4_N3_l_email_tape_pour_le_cessionnaire_va_dans_sa_cession_et_l_envoi_differe_sans_adresse_est_refuse()
    {
        var loc = SarlX();
        Cessions.Enregistrer(loc, new DateTime(2026, 12, 1), "SARL Y", "", "", "", new DateTime(2026, 11, 9));   // cessionnaire sans e-mail
        Assert.That(Cessions.Reporter(loc, new DateTime(2026, 12, 1), "SARL Y", "22222222222222", "2 rue Y", "compta@y.fr"), Is.True);
        Assert.That(loc.cessionPrevue.email, Is.EqualTo("compta@y.fr"));
        Assert.That(loc.cessionPrevue.siret, Is.EqualTo("22222222222222"));
        Assert.That(Cessions.Reporter(loc, new DateTime(2026, 11, 1), "SARL X", "", "", "autre@x.fr"), Is.False, "novembre : la fiche, rien à reporter");
        Assert.That(loc.emailLocataire, Is.EqualTo("x@x.fr"));
        Assert.That(Cessions.Reporter(loc, new DateTime(2026, 12, 1), "", "22222222222222", "2 rue Y", ""), Is.True);
        Assert.That(loc.cessionPrevue.email, Is.EqualTo("compta@y.fr"), "un e-mail effacé par erreur dans le formulaire n'efface pas celui de la cession");
        Assert.That(loc.cessionPrevue.nom, Is.EqualTo("SARL Y"), "ni le nom");

        Cessions.Effectuer(loc, new DateTime(2026, 12, 1));
        Assert.That(loc.emailLocataire, Is.EqualTo("compta@y.fr"), "le jour venu, la fiche reçoit l'e-mail tapé");
        Assert.That(Cessions.Reporter(loc, new DateTime(2026, 11, 1), "SARL X", "", "", "compta@x.fr"), Is.False, "la correction d'un loyer du cédant : l'historique n'est pas réécrit, l'adresse tapée reste sur la ligne");
        Assert.That(loc.cessions[0].ancienEmail, Is.EqualTo("x@x.fr"));
        Assert.That(loc.cessions[0].ancienNom, Is.EqualTo("SARL X"), "un nom retouché ne détache pas les autres factures du cédant de sa cession");

        string loyer = Src("Facturation/FactureLoyerPanel.cs");
        StringAssert.Contains("Cessions.Reporter(_loc, debut, _nom.text, _siret.text, _adresse.text, _emailEnvoi.text)", loyer, "SaveFromUI reporte la saisie");
        int refus = loyer.IndexOf("Aucune adresse email pour ce locataire : la facture ne pourrait pas partir", StringComparison.Ordinal);
        int differe = loyer.IndexOf("PreparerPourEnvoi(d, key, emission, correction, pdf, dateEnvoi.Value, parPlateforme)", StringComparison.Ordinal);   // lot 11 : parPlateforme suit
        Assert.That(refus, Is.GreaterThan(0).And.LessThan(differe), "l'envoi différé est refusé sans adresse, avant d'être enregistré");
    }

    /// D4-N4 / D5-N5 : le rappel et la notification lisent la ligne ; les quatre Finaliser posent acheteur et destinataire, l'envoi
    /// immédiat garde l'adresse réellement envoyée, le départ au lancement la réécrit ; le diagnostic dit « à refaire » avant
    /// « refusée par la plateforme » ; le suivi signale le loyer à refaire.
    [Test]
    public void D4_01_rappel_notification_et_formulaires_lisent_et_posent_la_ligne()
    {
        StringAssert.Contains("FactureEnvoiAuto.Destinataire(loc, r.type, r)", Src("Facturation/FactureElectronique.cs"), "la notification lit la ligne (D5-N5)");
        StringAssert.Contains("FactureEnvoiAuto.Destinataire(loc, l?.type, l)", Src("Facturation/FactureRappelService.cs"), "le rappel lit la ligne (D4-N4)");
        string auto = Src("Facturation/FactureEnvoiAuto.cs");
        StringAssert.Contains("if (envoi != null) r.destinataire = c.destinataire;", auto, "l'adresse réellement envoyée, au départ réel");
        StringAssert.Contains("if (Cessions.HorsTitulaire(loc, rec)) return false;", auto, "DoitPartir retient le loyer préparé au nom d'un autre");
        int aRefaire = auto.IndexOf("else if (Cessions.HorsTitulaire(loc, rec))", StringComparison.Ordinal);
        int plateforme = auto.IndexOf("refusée par la plateforme (à corriger depuis le suivi)", StringComparison.Ordinal);
        Assert.That(aRefaire, Is.GreaterThan(0).And.LessThan(plateforme), "Diagnostic : « à refaire » avant « refusée par la plateforme »");
        foreach (var p in new[] { "FactureLoyerPanel", "FactureRegulPanel", "FactureRefacPanel", "FactureDepotPanel" })
        {
            string s = Src("Facturation/" + p + ".cs");
            StringAssert.Contains("FacturationSuivi.PoserAcheteur(_loc, key, ", s, p + " : Finaliser pose l'acheteur et le destinataire");
            StringAssert.Contains("dest ?? _emailEnvoi.text", s, p + " : l'adresse réellement envoyée d'abord");
            StringAssert.Contains("$\" et envoyée à {dest}\", dest)", s, p + " : l'envoi immédiat passe l'adresse envoyée");
        }
        foreach (var p in new[] { "FactureRegulPanel", "FactureRefacPanel", "FactureDepotPanel" })
        {
            string s = Src("Facturation/" + p + ".cs");
            StringAssert.Contains("var acheteur = Cessions.AcheteurDe(_loc, _ligneCiblee);", s, p + " : la correction d'une facture d'un cédant reprend son acheteur");
            StringAssert.Contains("if (!_acheteurAutre) { f.destNom = _nom.text;", s, p + " : sans devenir celui du type");
        }
        StringAssert.Contains("is string[] a && a[0] == t[0]) t = a;", Src("Facturation/FactureLoyerPanel.cs"), "le loyer reprend l'adresse gardée sur la ligne");
        StringAssert.Contains("Cessions.HorsTitulaire(_loc, l)", Src("Facturation/LocataireSuiviInline.cs"), "le suivi signale le loyer à refaire");
        Assert.That(DonneesCloud.Modele, Is.GreaterThanOrEqualTo(8), "deux champs ajoutés à FactureEtat : modèle 8 (lot 6)");
    }

    /// D4-13 : la quittance nomme qui a payé : l'acheteur gardé, sinon le titulaire de la période, sinon la fiche.
    [Test]
    public void D4_13_la_quittance_nomme_qui_a_paye()
    {
        var loc = new Locataire { id = "LC6Q", Name = "Mme Durand", adresseLocataire = "3 rue D", periodiciteLoyer = Periodicite.mensuel,
            facturesEtat = new List<FactureEtat>(),
            cessions = new List<CessionBail> { new CessionBail { dateISO = "2026-12-01", ancienNom = "M. Martin", ancienAdresse = "2 rue M", ancienEmail = "m@m.fr" } } };
        var nov = new FactureEtat { key = "loyer-2026-P11", type = "Loyer", statut = "Paye" };
        var dec = new FactureEtat { key = "loyer-2026-P12", type = "Loyer", statut = "Paye" };
        Assert.That(FactureQuittanceService.Client(loc, nov), Is.EqualTo(("M. Martin", "2 rue M")), "ligne d'avant : le titulaire de la période");
        Assert.That(FactureQuittanceService.Client(loc, dec), Is.EqualTo(("Mme Durand", "3 rue D")));
        nov.acheteur = "M. Martin"; nov.destinataire = "compta@m.fr";
        Assert.That(FactureQuittanceService.Client(loc, nov), Is.EqualTo(("M. Martin", "2 rue M")), "l'acheteur gardé, l'adresse de sa cession");
        dec.acheteur = "Mme Durand";
        Assert.That(FactureQuittanceService.Client(loc, dec).nom, Is.EqualTo("Mme Durand"));
        StringAssert.Contains("Sanitize($\"Quittance-{clientNom}-{periode}\")", Src("Facturation/FactureQuittanceService.cs"), "le nom du fichier aussi");
    }

    /// D4-15 : TitulaireLigne lit la périodicité de la ligne : émise sous une autre, la clé ne désigne pas cette période (D4-04).
    [Test]
    public void D4_15_le_titulaire_d_une_ligne_respecte_sa_periodicite()
    {
        var loc = new Locataire { id = "LC6P", Name = "SARL X", emailLocataire = "x@x.fr", periodiciteLoyer = Periodicite.trimestriel, facturesEtat = new List<FactureEtat>() };
        Cessions.Enregistrer(loc, new DateTime(2027, 2, 15), "SARL Y", "", "", "y@y.fr", new DateTime(2026, 11, 9));
        var p2 = new FactureEtat { key = "loyer-2027-P2", type = "Loyer", statut = "AttenteEnvoi" };
        Assert.That(Cessions.TitulaireLigne(loc, p2)?[0], Is.EqualTo("SARL Y"), "2e trimestre 2027 (avril) : au cessionnaire");
        p2.periodicite = "mensuel";
        Assert.That(Cessions.TitulaireLigne(loc, p2), Is.Null, "émise en mensuel (février 2027) : la clé ne désigne pas ce trimestre, la fiche");
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Loyer", p2), Is.EqualTo("x@x.fr"));
        p2.periodicite = "trimestriel";
        Assert.That(Cessions.TitulaireLigne(loc, p2)?[0], Is.EqualTo("SARL Y"), "même périodicité : comme avant");
    }

    // ── Lot 6b · décisions du 09/11 (modèle 8 avec le lot 6) : (a) retrait après envoi (marqueur), (b) l'avoir cite sa facture ──

    static Locataire Dupont(string sortie, params FactureEtat[] lignes)
    {
        var l = new Locataire { id = "LC6B", Name = "Dupont", loyerAnnuel = 12000f, periodiciteLoyer = Periodicite.mensuel, dateSortieISO = sortie,
                                factureLoyer = new FactureInfo(), factureSeq = 5, facturesEtat = new List<FactureEtat>(lignes) };
        l.DateDebutBail = new DateTime(2024, 1, 1); l.DateFinBail = new DateTime(2032, 12, 31);
        return l;
    }

    static FactureEtat Loyer(string key, string libelle, string numero, string echeance, string statut = "Envoye")
        => new FactureEtat { key = key, type = "Loyer", statut = statut, libelle = libelle, numero = numero, pdfPath = numero.Replace('/', '-') + ".pdf",
                             montant = 1200f, loyerHT = 1000f, echeanceISO = echeance, periodicite = "mensuel" };

    /// (a) Un loyer PARTI puis retiré du suivi (exception du départ, D4-02) porte le marqueur `retireApresEnvoi` : « Refaire » et les
    /// formulaires refusent au centre (FactureEmission.RefusRetire, lu par Preparer), numéro et PDF gardés ; le marqueur tombe avec
    /// le prochain état reçu par le menu de la pastille. Un loyer jamais parti, retiré de même, se refait au même numéro, comme avant.
    /// Avant : « Refaire » reprenait le numéro et écrasait le PDF reçu par le locataire.
    [Test]
    public void Lot6b_un_loyer_parti_puis_retire_du_suivi_ne_se_refait_pas()
    {
        var dec = Loyer("loyer-2026-P12", "Loyer Décembre 2026", "2026/12003", "2026-12-01");
        var loc = Dupont("2026-11-30", dec);
        Assert.That(FactureEmission.RefusRetire(dec), Is.Null, "parti, pas retiré : rien à refuser");
        Assert.That(DepartLocataire.Retirable(loc, dec), Is.True);
        FacturationSuivi.SetStatut(loc, dec, "");   // « Retirer du suivi »
        Assert.That(dec.statut, Is.Empty);
        Assert.That(dec.retireApresEnvoi, Is.True, "le marqueur est posé");
        Assert.That(dec.numero, Is.EqualTo("2026/12003"));
        Assert.That(dec.pdfPath, Is.Not.Empty, "numéro et PDF gardés");

        loc.dateSortieISO = "";   // départ annulé : la ligne revient « À faire », avec « Refaire »
        var d = FactureEmission.Preparer(loc, dec.key, "2026/12009", "mensuel");
        StringAssert.Contains("« Loyer Décembre 2026 » est parti chez le locataire avant d'être retiré du suivi : ne le refaites pas, son numéro et le PDF reçu seraient écrasés.", d.Refus);
        StringAssert.Contains("Si le départ est annulé ou repoussé, cliquez son état et remettez-le « Envoyé » ou « Payé ».", d.Refus);
        Assert.That(FactureEmission.NumeroRepris(loc, dec.key, "mensuel"), Is.Null, "les formulaires n'annoncent aucun numéro repris");
        Assert.That(FactureEmission.SeraUneCorrection(loc, dec.key, "mensuel"), Is.False);
        var copie = FacturationSuivi.Lignes(loc, 2026).First(x => x.key == dec.key);
        Assert.That(copie.retireApresEnvoi, Is.True, "la ligne affichée porte le marqueur (CopieDe)");
        Assert.That(FactureEmission.RefusRetire(copie), Is.Not.Null, "« Refaire » du suivi refuse sur la copie");
        Assert.That(FacturationSuivi.AvecBoutonAuto(loc, copie), Is.False, "pas de bouton « Auto »");
        FacturationSuivi.SetStatut(loc, dec, "");   // un second « automatique » ne lève pas le marqueur
        Assert.That(dec.retireApresEnvoi, Is.True);

        FacturationSuivi.SetStatut(loc, dec, "Envoye");   // remis « Envoyé » par le menu
        Assert.That(dec.retireApresEnvoi, Is.False, "le marqueur tombe avec le nouvel état");
        d = FactureEmission.Preparer(loc, dec.key, "2026/12009", "mensuel");
        Assert.That(d.Refus, Is.Null);
        Assert.That(d.Correction, Is.True);
        Assert.That(d.NumeroFacture, Is.EqualTo("2026/12003 corrigée(1)"), "partie : une correction, comme avant");

        // « Payé » à la main, retiré : ce qui était réglé est noté en versement, pour l'avoir qui le citera (b).
        loc.dateSortieISO = "2026-11-30"; dec.statut = "Paye";
        StringAssert.Contains("cliquez son état et remettez-le « Payé » : « Refaire » sera refusé, il garde son numéro et le PDF reçu par le locataire.",
            DepartLocataire.ConfirmationRetrait(loc, dec), "la confirmation du retrait le dit (avant : « sans le refaire : « Refaire » reprendrait son numéro… »)");
        FacturationSuivi.SetStatut(loc, dec, "");
        Assert.That(dec.retireApresEnvoi, Is.True);
        Assert.That(FacturationSuivi.Paye(dec), Is.EqualTo(1200f).Within(0.01f), "« Payé » sans versement : le règlement est noté");
        Assert.That(FacturationSuivi.Verse(loc, dec.key), Is.EqualTo(1200f).Within(0.01f));
        Assert.That(FacturationSuivi.DuesDe(loc), Is.Empty, "retiré : rien n'est dû");

        // Un loyer jamais parti (préparé), retiré de même : pas de marqueur, il se refait au même numéro (D4-02).
        var jan = Loyer("loyer-2027-P1", "Loyer Janvier 2027", "2027/01001", "2027-01-01", "AttenteEnvoi");
        loc.facturesEtat.Add(jan);
        FacturationSuivi.SetStatut(loc, jan, "");
        Assert.That(jan.retireApresEnvoi, Is.False);
        var r = FactureEmission.Preparer(loc, jan.key, "2027/01009", "mensuel");
        Assert.That(r.Refus, Is.Null);
        Assert.That(r.Reecriture, Is.True);
        Assert.That(r.NumeroFacture, Is.EqualTo("2027/01001"));

        StringAssert.Contains("if (FactureEmission.RefusRetire(ligneCiblee) is string retire) { ConfirmDialog.Erreur(retire); return; }",
            Src("Facturation/LocataireSuiviInline.cs"), "« Refaire » refuse au centre avant d'ouvrir le formulaire");
    }

    /// (b) L'avoir garde la clé de la facture qu'il cite (« Avoir sur la facture n° … », posée par la refacturation) : son « À
    /// rembourser » = min(|avoir|, versé sur la facture citée) ; jamais payée, rien à rendre : il reste « Envoyé », hors des créances
    /// et de l'archivage ; payée en partie, le versé seulement ; payée en entier, tout l'avoir. Sans facture citée, comme avant.
    [Test]
    public void Lot6b_l_avoir_qui_cite_sa_facture_ne_rend_que_ce_qui_a_ete_verse()
    {
        string echue = DateTime.Today.AddDays(-60).ToString("yyyy-MM-dd");
        var loyer = Loyer("loyer-2026-P12", "Loyer Décembre 2026", "2026/12003", "2026-12-01");
        var loc = Dupont("2026-11-30", loyer);
        FacturationSuivi.SetStatut(loc, loyer, "");   // retiré du suivi, jamais payé
        var avoir = new FactureEtat { key = "refac-eau", type = "Refac", statut = "Envoye", libelle = "Avoir : Eau", numero = "2026/12010",
                                      pdfPath = "avoir.pdf", montant = -1200f, echeanceISO = echue, factureCitee = loyer.key };
        loc.facturesEtat.Add(avoir);
        Assert.That(FacturationSuivi.ARembourser(loc, avoir.key), Is.EqualTo(0f), "facture jamais payée : rien à rendre (avant : 1 200 €)");
        Assert.That(FacturationSuivi.EtatAffiche(loc, avoir), Is.EqualTo(FacturationSuivi.Etat.Envoye), "il reste « Envoyé »");
        Assert.That(FacturationSuivi.DuesDe(loc).Select(x => x.key), Has.None.EqualTo(avoir.key), "ni créance, ni repris au décompte de sortie");
        Assert.That(DepartLocataire.RaisonsNonArchivable(loc), Has.None.Contains("avoir"), "il n'empêche plus d'archiver");
        Assert.That(DepartLocataire.RaisonsNonArchivable(loc), Has.None.Contains("Loyer Décembre 2026"));

        loyer.versements.Add(new Versement { dateISO = "2026-11-20", montant = 400f });   // payé en partie
        Assert.That(FacturationSuivi.ARembourser(loc, avoir.key), Is.EqualTo(400f).Within(0.01f), "en partie : le versé seulement");
        Assert.That(FacturationSuivi.EtatAffiche(loc, avoir), Is.EqualTo(FacturationSuivi.Etat.ARembourser));
        Assert.That(FacturationSuivi.DuesDe(loc).First(x => x.key == avoir.key).montant, Is.EqualTo(-400f).Within(0.01f), "le décompte de sortie rend 400 €");
        Assert.That(DepartLocataire.RaisonsNonArchivable(loc),
            Has.Some.EqualTo($"avoir n° 2026/12010 « Avoir : Eau » à rembourser ({400f.ToString("N2", FacturePdfService.FrCulture)} €), puis à passer « Payé »"));
        Assert.That(FacturationSuivi.EnregistrerRemboursement(loc, avoir, 500f, new DateTime(2026, 12, 10)), Does.Contain("dépasse"));
        Assert.That(FacturationSuivi.EnregistrerRemboursement(loc, avoir, 400f, new DateTime(2026, 12, 10)), Is.Null);
        Assert.That(avoir.statut, Is.EqualTo("Paye"), "tout rendu : « Payé »");

        avoir.statut = "Envoye"; avoir.versements.Clear();
        loyer.versements.Clear(); loyer.statut = "Paye";   // payé en entier, à la main (sans versement)
        Assert.That(FacturationSuivi.ARembourser(loc, avoir.key), Is.EqualTo(1200f).Within(0.01f), "payée en entier : tout l'avoir");
        loyer.versements.Add(new Versement { dateISO = "2026-11-20", montant = 1200f }); loyer.statut = "";   // retiré, versements gardés
        Assert.That(FacturationSuivi.ARembourser(loc, avoir.key), Is.EqualTo(1200f).Within(0.01f));
        loyer.versements.Clear();
        avoir.factureCitee = "";   // avoir libre, ou d'avant la mise à jour : comme avant
        Assert.That(FacturationSuivi.ARembourser(loc, avoir.key), Is.EqualTo(1200f).Within(0.01f), "sans facture citée : rien ne change");
        Assert.That(FacturationSuivi.EtatAffiche(loc, avoir), Is.EqualTo(FacturationSuivi.Etat.ARembourser));
        Assert.That(FacturationSuivi.DuesDe(loc).First(x => x.key == avoir.key).montant, Is.EqualTo(-1200f).Within(0.01f));

        // La refacturation pose la clé citée d'après le numéro choisi, sur toutes les lignes de l'avoir (plusieurs charges).
        var gaz = new FactureEtat { key = "refac-gaz", type = "Refac", statut = "Envoye", numero = "2026/12010", pdfPath = "avoir.pdf", montant = -100f, echeanceISO = echue };
        loc.facturesEtat.Add(gaz);
        FacturationSuivi.PoserFactureCitee(loc, avoir.key, "2026/12003");
        Assert.That(avoir.factureCitee, Is.EqualTo(loyer.key));
        Assert.That(gaz.factureCitee, Is.EqualTo(loyer.key));
        FacturationSuivi.PoserFactureCitee(loc, avoir.key, "");
        Assert.That(avoir.factureCitee, Is.EqualTo(loyer.key), "correction sans choix : la facture citée reste");
        FacturationSuivi.PoserFactureCitee(loc, avoir.key, "2099/99999");
        Assert.That(avoir.factureCitee, Is.EqualTo(loyer.key), "numéro inconnu du suivi (avoir libre) : rien");
        StringAssert.Contains("FacturationSuivi.PoserFactureCitee(_loc, key, d.factureOrigine);", Src("Facturation/FactureRefacPanel.cs"), "Finaliser la pose");
        Assert.That(DonneesCloud.Modele, Is.GreaterThanOrEqualTo(8), "les deux champs rejoignent le modèle 8 du lot 6 (9 depuis le lot 9)");
    }

    // ── Lot 7 · D5-04 : un refus d'« Encaissée » ne bloque plus le locataire ni la tournée (+ D5-20, D5-23, D5-27) ──

    static FactureElectronique.Reponse Rep(string refus, string erreur = null) => new FactureElectronique.Reponse { refus = refus, erreur = erreur ?? refus };

    /// Seule une panne arrête la tournée ; un refus nommé (« refusee » : une règle de Super PDP ; « origine » : pas membre de l'entreprise
    /// d'origine) et « rien » n'arrêtent pas. Les refus d'un même traitement se cumulent dans une réponse (une seule fenêtre) ; une panne prime.
    /// Avant : tout refus d'« Encaissée » rendait la main (Traiter) et arrêtait la tournée (Demarrer : `break`).
    [Test]
    public void D5_04_seule_une_panne_arrete_la_tournee_et_les_refus_se_disent_ensemble()
    {
        foreach (var p in new[] { "indisponible", "serveur", "jeton", "retire", "changee", "cloud", "non_relie", "acces", "" })
            Assert.That(FactureElectronique.ArreteLaTournee(Rep(p)), Is.True, $"« {p} » : une panne, la tournée s'arrête");
        Assert.That(FactureElectronique.ArreteLaTournee(Rep(null)), Is.True, "réponse sans motif : une panne");
        Assert.That(FactureElectronique.ArreteLaTournee(Rep("refusee")), Is.False, "refus de Super PDP : les autres continuent");
        Assert.That(FactureElectronique.ArreteLaTournee(Rep("origine")), Is.False, "pas membre de l'origine : comme avant (BG7)");
        Assert.That(FactureElectronique.ArreteLaTournee(null), Is.False, "rien à dire : rien n'arrête");

        var a = Rep("refusee", "« Loyer T1 » de Dupont : « Encaissée » refusé."); var b = Rep("origine", "« Loyer T2 » de Martin a été transmise par SCI X…");
        Assert.That(FactureElectronique.Cumul(null, a), Is.SameAs(a));
        Assert.That(FactureElectronique.Cumul(a, null), Is.SameAs(a));
        var deux = FactureElectronique.Cumul(a, b);
        Assert.That(FactureElectronique.ArreteLaTournee(deux), Is.False);
        Assert.That(deux.erreur, Is.EqualTo(a.erreur + "\n" + b.erreur), "les deux refus dans une seule fenêtre");
        var panne = Rep("indisponible");
        Assert.That(FactureElectronique.Cumul(a, panne), Is.SameAs(panne), "une panne prime");
        Assert.That(FactureElectronique.Cumul(panne, a), Is.SameAs(panne));
    }

    /// Un « Encaissée » (ou son annulation) refusé est noté pour la session : la ligne n'est plus traitée (ATraiter : pas de nouvel appel, pas de
    /// seconde passe, pas de tournée pour elle seule) ni retentée avant le prochain lancement, mais elle reste « en attente » (pastille, transfert
    /// bloqué) ; Traiter continue au lieu de rendre la main, Demarrer et Rattraper ne s'arrêtent que sur une panne, et les refus de la tournée
    /// sont dits dans une seule fenêtre, à la fin. Les tests texte « condition Encaissée » de Revue17Tests restent vrais (BN9, BN17).
    [Test]
    public void D5_04_un_refus_note_pour_la_session_n_est_plus_retente_et_ne_bloque_rien()
    {
        FactureElectronique.NoterRefusEncaissement("lotc-refus-1");
        Assert.That(FactureElectronique.RefuseeCetteSession("lotc-refus-1"), Is.True);
        Assert.That(FactureElectronique.RefuseeCetteSession("lotc-refus-2"), Is.False);
        Assert.That(FactureElectronique.RefuseeCetteSession(""), Is.False);
        Assert.That(FactureElectronique.RefuseeCetteSession(null), Is.False);
        FactureElectronique.NoterRefusEncaissement(null);   // sans identifiant : rien à noter

        var r = new FactureEtat { key = "loyer-2026-P1", statut = "Paye", pdfPath = "x.pdf", plateformeDonnees = "{}", plateformeId = "lotc-refus-1", plateformeEtat = "envoyee",
                                  plateformeOrigine = "SCI X", plateformeOrigineId = "ent-x" };   // transmise par une autre SCI : traitée même sans session ouverte (BG7), ce qui isole la règle
        Assert.That(FactureElectronique.EnAttente(r), Is.True, "elle attend toujours : « Encaissée » au prochain lancement, transfert bloqué");
        var aTraiter = typeof(FactureElectronique).GetMethod("ATraiter", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(aTraiter, Is.Not.Null, "FactureElectronique.ATraiter");
        Assert.That((bool)aTraiter.Invoke(null, new object[] { r }), Is.False, "refusée cette session : plus traitée");
        r.plateformeId = "lotc-refus-2";
        Assert.That((bool)aTraiter.Invoke(null, new object[] { r }), Is.True, "une autre facture : traitée");

        string src = Src("Facturation/FactureElectronique.cs");
        StringAssert.Contains("static bool ATraiter(FactureEtat r) => EnAttente(r) && !RefuseeCetteSession(r.plateformeId)", src);
        StringAssert.Contains("if (RefusNomme(loc, r, rep, \"« Encaissée » refusé\") is Reponse ro) { refus = Cumul(refus, ro); continue; }", src, "Traiter continue au lieu de rendre la main");
        StringAssert.Contains("if (RefusNomme(loc, r, rep, \"annulation de l'encaissement refusée\") is Reponse ro) { refus = Cumul(refus, ro); continue; }", src, "l'annulation aussi");
        StringAssert.Contains("NoterRefusEncaissement(id ?? r.plateformeId);", src, "le refus est noté pour la session (lot 9 : sous l'identifiant annulé, sinon celui de la ligne)");
        StringAssert.Contains("if (ArreteLaTournee(p)) { panne = p; bloque = loc; } else refus = Cumul(refus, p);", src, "Demarrer : seule une panne arrête la tournée");
        StringAssert.DoesNotContain("panne.refus != \"origine\"", src);
        StringAssert.Contains("if (!ArreteLaTournee(panne) && loc.facturesEtat.Any(ATraiter))", src, "Rattraper : la seconde passe malgré un refus nommé");
        StringAssert.Contains("panne = Cumul(panne, p);", src, "… sans perdre le refus de la première");
        Assert.That(src.Split(new[] { "Panne(null, refus)" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1), "les refus de la tournée : une seule fenêtre, à la fin");
        StringAssert.Contains("Les autres factures ont continué. Nouvel essai au prochain lancement", src);
    }

    /// D5-20 : la correction, depuis la nouvelle SCI, d'une facture transmise par l'entreprise d'origine (transfert BG7) est refusée avant tout
    /// envoi : la rectificative partirait au nom de la SCI ouverte en citant la facture de l'autre (document faux), la pastille dirait encore
    /// « transmise par SCI X », et son « Encaissée » partirait au nom de SCI X avec un identifiant inconnu d'elle (refus à chaque lancement).
    [Test]
    public void D5_20_la_correction_d_une_facture_transferee_est_refusee()
    {
        var avant = new FactureEtat { plateformeId = "42", plateformeDonnees = "{\"number\":\"2026/08001\",\"type_code\":380}", plateformeOrigine = "SCI (JBC)", plateformeOrigineId = "id-jbc" };
        Assert.That(FactureElectronique.RefusCorrection(avant, 1200f, "Loyer Août 2026"), Does.Contain("SCI (JBC)").And.Contain("Rien n'a été émis"));
        avant.plateformeOrigineId = "";   // marque d'avant (nom seul, lecture seule) : refusée aussi
        Assert.That(FactureElectronique.RefusCorrection(avant, 1200f, "Loyer Août 2026"), Does.Contain("SCI (JBC)"));
        avant.plateformeOrigine = "";
        Assert.That(FactureElectronique.RefusCorrection(avant, 1200f, "Loyer Août 2026"), Is.Null, "sans marque d'origine : la rectificative part, comme avant");
    }

    /// D5-23 / D5-27 : RecopierPlateforme (fiche relue pendant l'appel à Super PDP) recopie TOUS les champs plateforme* et encaissee* (le montant
    /// et la date déclarés manquaient), et le retour « en attente d'envoi » d'un refus après la bascule, sur toutes les lignes de la facture ;
    /// le statut n'est pas recopié dans les autres cas.
    [Test]
    public void D5_23_RecopierPlateforme_recopie_tous_les_champs_de_la_plateforme_et_le_retour_en_attente()
    {
        var champs = typeof(FactureEtat).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => (f.Name.StartsWith("plateforme") || f.Name.StartsWith("encaissee")) && f.Name != "plateformeDonnees").ToList();
        CollectionAssert.IsSubsetOf(new[] { "plateformeId", "plateformeEtat", "encaisseeDeclaree", "encaisseeMontant", "encaisseeDateISO" }, champs.Select(f => f.Name).ToList());
        var r = new FactureEtat { key = "k", plateformeDonnees = "{}" };
        foreach (var f in champs)
        {
            if (f.FieldType == typeof(string)) f.SetValue(r, "v-" + f.Name);
            else if (f.FieldType == typeof(bool)) f.SetValue(r, true);
            else if (f.FieldType == typeof(float)) f.SetValue(r, 1200f);
            else Assert.Fail($"type non prévu pour {f.Name} : {f.FieldType.Name} (compléter le test)");
        }
        var de = new Locataire { id = "LC7", facturesEtat = new List<FactureEtat> { r } };
        var vers = new Locataire { id = "LC7", facturesEtat = new List<FactureEtat> { new FactureEtat { key = "k", plateformeDonnees = "{}", statut = "Paye" } } };
        FactureElectronique.RecopierPlateforme(de, vers);
        foreach (var f in champs) Assert.That(f.GetValue(vers.facturesEtat[0]), Is.EqualTo(f.GetValue(r)), $"champ oublié par RecopierPlateforme : {f.Name}");
        Assert.That(vers.facturesEtat[0].statut, Is.EqualTo("Paye"), "le statut n'est pas recopié (un collègue a pu le changer)");

        // D5-27 : après la bascule, un refus remet « en attente d'envoi » (rien n'est parti) : la fiche relue le reçoit, toutes les lignes de la facture.
        var refusee = new FactureEtat { key = "refac-eau", type = "Refac", plateformeDonnees = "{}", plateformeEtat = "refusee", plateformeErreur = "HTTP 422", statut = "AttenteEnvoi", pdfPath = "f.pdf" };
        var relue = new Locataire { id = "LC7b", facturesEtat = new List<FactureEtat> {
            new FactureEtat { key = "refac-eau", type = "Refac", plateformeDonnees = "{}", statut = "Envoye", pdfPath = "f.pdf" },
            new FactureEtat { key = "refac-gaz", type = "Refac", statut = "Envoye", pdfPath = "f.pdf" } } };
        FactureElectronique.RecopierPlateforme(new Locataire { id = "LC7b", facturesEtat = new List<FactureEtat> { refusee } }, relue);
        Assert.That(relue.facturesEtat[0].statut, Is.EqualTo("AttenteEnvoi"), "rien n'est parti : la fiche enregistrée le dit (avant : « Envoyé » + « Refusée par la plateforme »)");
        Assert.That(relue.facturesEtat[0].plateformeEtat, Is.EqualTo("refusee"));
        Assert.That(relue.facturesEtat[1].statut, Is.EqualTo("AttenteEnvoi"), "la seconde ligne de la même facture (même PDF) aussi, comme Traiter");
        refusee.plateformeEtat = "envoyee"; relue.facturesEtat[0].statut = "Paye"; relue.facturesEtat[1].statut = "Paye";
        FactureElectronique.RecopierPlateforme(new Locataire { id = "LC7b", facturesEtat = new List<FactureEtat> { refusee } }, relue);
        Assert.That(relue.facturesEtat[0].statut, Is.EqualTo("Paye"), "transmise : le statut reste celui de la fiche");
        Assert.That(DonneesCloud.Modele, Is.GreaterThanOrEqualTo(8), "lot 7 : aucun champ enregistré nouveau (le modèle 9 vient du lot 9)");
    }

    // ── Lot 9 · D5-06 : annulation de l'« Encaissée » de la facture remplacée (+ D5-N1, D4-N2 ; modèle 9) ──

    /// Le JSON qu'une ligne transmise garde (FactureEn16931.Json) : son numéro et son total TTC.
    static string JsonTransmis(string numero, string ttc) =>
        "{\"number\":\"" + numero + "\",\"issue_date\":\"2026-07-01\",\"type_code\":380,\"currency_code\":\"EUR\",\"totals\":{\"sum_invoice_lines_amount\":\"833.33\","
        + "\"total_without_vat\":\"833.33\",\"total_vat_amount\":{\"value\":\"166.67\",\"currency_code\":\"EUR\"},\"total_with_vat\":\"" + ttc + "\",\"amount_due_for_payment\":\"" + ttc + "\"}}";

    /// Décision du 05/11 : corriger une facture payée et déclarée « Encaissée » garde l'identifiant de la version remplacée (encaisseeAAnnulerId)
    /// avec le montant et la date déclarés : l'annulation (212 négatif du même montant, datée comme l'encaissement annulé) part AVANT la
    /// rectificative, qui reçoit le sien une fois payée. Avant : identifiant et drapeau effacés, rien n'annulait (1 000 € + 880 € déclarés pour
    /// 880 € gardés : juillet de sephora au bac à sable, le 04/11).
    [Test]
    public void D5_06_corriger_une_facture_encaissee_annule_d_abord_l_ancien_encaissement()
    {
        var r = new FactureEtat { key = "loyer-2026-P7", type = "Loyer", libelle = "Loyer Juillet 2026", statut = "Paye", pdfPath = "x.pdf", montant = 1000f,
                                  plateformeDonnees = JsonTransmis("2026/07001", "1000.00"), plateformeId = "1672792", plateformeEtat = "envoyee",
                                  encaisseeDeclaree = true, encaisseeMontant = 1000f, encaisseeDateISO = "2026-07-03" };
        FactureElectronique.Preparer(r, JsonTransmis("2026/07001-corrigee-1", "880.00"));
        Assert.That(r.encaisseeAAnnulerId, Is.EqualTo("1672792"), "l'identifiant de la version remplacée est gardé pour l'annulation");
        Assert.That(r.plateformeId, Is.Empty, "la rectificative aura le sien");
        Assert.That(r.encaisseeDeclaree, Is.False);
        Assert.That(r.encaisseeMontant, Is.EqualTo(1000f), "le montant à annuler : celui déclaré, pas celui de la rectificative");
        Assert.That(r.encaisseeDateISO, Is.EqualTo("2026-07-03"), "l'annulation est datée comme l'encaissement annulé");
        Assert.That(FactureElectronique.EnAttente(r), Is.True);
        Assert.That(TransfertCloud.Bloque(new[] { new Locataire { Name = "Sephora", facturesEtat = new List<FactureEtat> { r } } }), Does.Contain("attend sa transmission"));
        r.plateformeEtat = "refusee";
        Assert.That(FactureElectronique.EnAttente(r), Is.True, "rectificative refusée : l'annulation attend toujours (avant : plus rien n'attendait)");
        r.plateformeEtat = "perimee";
        Assert.That(FactureElectronique.EnAttente(r), Is.True, "refaite hors plateforme ensuite : l'annulation attend encore");
        r.encaisseeAAnnulerId = "";   // annulation passée (Traiter)
        Assert.That(FactureElectronique.EnAttente(r), Is.False);

        // Montant inconnu (déclaré avant le 01/11, ou fiche relue avant le lot 7) : le total TTC de l'ancien JSON ; date vide : aujourd'hui.
        var sansMontant = new FactureEtat { key = "loyer-2026-P6", statut = "Paye", plateformeDonnees = JsonTransmis("2026/06001", "1200.00"), plateformeId = "1672000", encaisseeDeclaree = true };
        FactureElectronique.Preparer(sansMontant, JsonTransmis("2026/06001-corrigee-1", "1080.00"));
        Assert.That(sansMontant.encaisseeAAnnulerId, Is.EqualTo("1672000"));
        Assert.That(sansMontant.encaisseeMontant, Is.EqualTo(1200f).Within(0.001f), "le total TTC que la plateforme a reçu, pas celui de la rectificative");
        Assert.That(sansMontant.encaisseeDateISO, Is.EqualTo(DateTime.Today.ToString("yyyy-MM-dd")));
        Assert.That(FactureEn16931.TotalDe(null), Is.EqualTo(0f));
        Assert.That(FactureEn16931.TotalDe("{\"number\":\"x\"}"), Is.EqualTo(0f), "sans total : rien à annuler (jamais 0 € envoyé)");
        var sansTotal = new FactureEtat { key = "loyer-2026-P3", statut = "Paye", plateformeDonnees = "{\"number\":\"2026/03001\"}", plateformeId = "1669000", encaisseeDeclaree = true };
        FactureElectronique.Preparer(sansTotal, JsonTransmis("2026/03001-corrigee-1", "900.00"));
        Assert.That(sansTotal.encaisseeAAnnulerId, Is.Null.Or.Empty);

        // Jamais déclarée : rien à annuler. Correction hors plateforme d'une ligne déclarée : la version transmise ne bouge pas (D5-07),
        // le chemin d'annulation d'avant (facture qui quitte « Payé ») suffit.
        var jamais = new FactureEtat { key = "loyer-2026-P5", statut = "Paye", plateformeDonnees = JsonTransmis("2026/05001", "1000.00"), plateformeId = "1671000", encaisseeMontant = 1000f };
        FactureElectronique.Preparer(jamais, JsonTransmis("2026/05001-corrigee-1", "900.00"));
        Assert.That(jamais.encaisseeAAnnulerId, Is.Null.Or.Empty);
        var horsPlateforme = new FactureEtat { key = "loyer-2026-P4", statut = "Paye", plateformeDonnees = JsonTransmis("2026/04001", "1000.00"), plateformeId = "1670000",
                                               encaisseeDeclaree = true, encaisseeMontant = 1000f, encaisseeDateISO = "2026-04-03" };
        FactureElectronique.Preparer(horsPlateforme, null);
        Assert.That(horsPlateforme.encaisseeAAnnulerId, Is.Null.Or.Empty);
        Assert.That(horsPlateforme.plateformeId, Is.EqualTo("1670000"));
        Assert.That(horsPlateforme.encaisseeDeclaree, Is.True);

        // Point « À vérifier » du transfert (D5.md) : une facture transmise par une autre SCI est refusée avant Preparer (D5-20, lot 7) : l'annulation
        // partirait au nom de l'origine et la rectificative au nom de la SCI ouverte, avec une seule marque d'origine sur la ligne.
        var transferee = new FactureEtat { plateformeId = "42", plateformeDonnees = JsonTransmis("2026/08001", "1000.00"), plateformeOrigine = "SCI (JBC)", plateformeOrigineId = "id-jbc",
                                           encaisseeDeclaree = true, encaisseeMontant = 1000f };
        Assert.That(FactureElectronique.RefusCorrection(transferee, 880f, "Loyer Août 2026"), Does.Contain("SCI (JBC)"));
        Assert.That(DonneesCloud.Modele, Is.GreaterThanOrEqualTo(9), "un champ enregistré de plus : modèle 9 (la montée elle-même : empreinte de Revue17Tests.D2_01)");
    }

    /// L'annulation part en tête de Traiter, avant « envoyer » ; l'« Encaissée » de la rectificative attend qu'elle soit passée (sinon son
    /// montant écraserait celui à annuler : −880 € au lieu de −1 000 €) ; une panne fait attendre la rectificative ; un refus de l'annulation est
    /// noté pour la session sous l'identifiant annulé (Traiter : l'appel n'est plus retenté avant le prochain lancement ; la ligne, elle, reste
    /// traitée : la rectificative part quand même) ; la fiche relue pendant l'appel et la ligne affichée portent le champ.
    [Test]
    public void D5_06_l_annulation_part_avant_la_rectificative_et_son_encaissee_attend()
    {
        string src = Src("Facturation/FactureElectronique.cs");
        int annulation = src.IndexOf("id = r.encaisseeAAnnulerId,", StringComparison.Ordinal);
        int envoi = src.IndexOf("action = \"envoyer\"", StringComparison.Ordinal);
        int encaissee = src.IndexOf("&& string.IsNullOrEmpty(r.encaisseeAAnnulerId)   // Revue 17 (D5-06, lot 9)", StringComparison.Ordinal);
        Assert.That(annulation, Is.GreaterThan(0).And.LessThan(envoi), "l'annulation avant la transmission de la rectificative");
        Assert.That(encaissee, Is.GreaterThan(envoi), "l'« Encaissée » de la rectificative attend l'annulation");
        StringAssert.Contains("montant = Montant(-r.encaisseeMontant), date = r.encaisseeDateISO", src, "le même montant en négatif, à la date de l'encaissement annulé");
        StringAssert.Contains("if (rep.ok) { r.encaisseeAAnnulerId = \"\"; r.encaisseeMontant = 0f; r.encaisseeDateISO = \"\"; annulees++; change = true;", src);
        StringAssert.Contains("else return (envoyees, encaissees, annulees, change, rep, auNomDe);", src, "panne : la rectificative attend");
        StringAssert.Contains("RefusNomme(loc, r, rep, \"annulation de l'encaissement de la facture remplacée refusée\", r.encaisseeAAnnulerId) is Reponse ro) refus = Cumul(refus, ro);", src,
            "refus : noté, la rectificative part quand même");
        StringAssert.Contains("NoterRefusEncaissement(id ?? r.plateformeId);", src, "noté sous l'identifiant annulé");
        StringAssert.Contains("(facture plus payée ou remplacée)", src, "le résumé de Rattraper");

        // Relecture : une annulation refusée n'exclut pas la ligne (sa rectificative, ou la reprise d'une transmission en panne, part quand même
        // dans la session, comme promis) ; seul l'appel d'annulation est sauté, dans Traiter. Avant : ATraiter rendait faux, Rattraper rendait
        // la main sans un mot jusqu'au prochain lancement.
        FactureElectronique.NoterRefusEncaissement("lotc-annul-1");
        var r = new FactureEtat { key = "loyer-2026-P7", statut = "Paye", pdfPath = "x.pdf", plateformeDonnees = "{}", encaisseeAAnnulerId = "lotc-annul-1", encaisseeMontant = 1000f,
                                  plateformeOrigine = "SCI X", plateformeOrigineId = "ent-x" };   // transmise par une autre SCI : traitée sans session ouverte, ce qui isole la règle
        var aTraiter = typeof(FactureElectronique).GetMethod("ATraiter", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That((bool)aTraiter.Invoke(null, new object[] { r }), Is.True, "annulation refusée cette session : la ligne reste traitée (sa rectificative part)");
        Assert.That(FactureElectronique.EnAttente(r), Is.True, "et elle attend toujours (prochain lancement, transfert bloqué)");
        StringAssert.Contains("if (!string.IsNullOrEmpty(r.encaisseeAAnnulerId) && !RefuseeCetteSession(r.encaisseeAAnnulerId))", src,
            "Traiter : l'appel d'annulation refusé n'est pas retenté avant le prochain lancement");
        r.plateformeId = "lotc-annul-1";   // la règle du lot 7, sur l'identifiant de la ligne : elle, exclut la ligne
        Assert.That((bool)aTraiter.Invoke(null, new object[] { r }), Is.False);

        // Recopies : la fiche relue pendant l'appel (RecopierPlateforme) et la ligne affichée (CopieDe).
        FactureEtat Pendante() => new FactureEtat { key = "loyer-2026-P7", plateformeDonnees = "{}", encaisseeAAnnulerId = "1672792", encaisseeMontant = 1000f, encaisseeDateISO = "2026-07-03" };
        var de = new Locataire { id = "LC9", facturesEtat = new List<FactureEtat> { Pendante() } };
        var vers = new Locataire { id = "LC9", facturesEtat = new List<FactureEtat> { Pendante() } };
        de.facturesEtat[0].encaisseeAAnnulerId = ""; de.facturesEtat[0].encaisseeMontant = 0f; de.facturesEtat[0].encaisseeDateISO = "";   // annulée pendant l'appel
        FactureElectronique.RecopierPlateforme(de, vers);
        Assert.That(vers.facturesEtat[0].encaisseeAAnnulerId, Is.Empty, "annulation passée : la fiche relue ne la renvoie pas une seconde fois");
        Assert.That(vers.facturesEtat[0].encaisseeMontant, Is.EqualTo(0f));
        var juillet = Loyer("loyer-2026-P7", "Loyer Juillet 2026", "2026/07001", "2026-07-01", "Paye");
        juillet.plateformeDonnees = "{}"; juillet.encaisseeAAnnulerId = "1672792";
        var copie = FacturationSuivi.Lignes(Dupont("", juillet), 2026).First(x => x.key == juillet.key);
        Assert.That(copie.encaisseeAAnnulerId, Is.EqualTo("1672792"), "la ligne affichée porte l'annulation en attente (EnAttente, transfert)");
        Assert.That(FactureElectronique.EnAttente(copie), Is.True);
    }

    /// Relecture du lot 9 : avec une annulation en attente, EnAttente reste vrai pour une version périmée (refaite hors plateforme ensuite) ou
    /// refusée ; Traiter ne doit pas pour autant la transmettre : ce qui part passe par ATransmettre (émise, JSON posé, pas d'identifiant, ni
    /// refusée ni périmée), lue aussi par EnAttente. Avant : le bloc « envoyer » ne regardait que l'identifiant vide, et le JSON périmé (anciens
    /// montants) partait avec le PDF de la correction suivante (document faux, revue 16c D5-01) ; une facture refusée repartait telle quelle.
    [Test]
    public void D5_06_une_version_perimee_ou_refusee_ne_repart_pas_quand_une_annulation_attend()
    {
        var r = new FactureEtat { key = "loyer-2026-P7", type = "Loyer", statut = "Paye", pdfPath = "x.pdf", echeanceISO = "2026-07-05", plateformeDonnees = "{}",
                                  encaisseeAAnnulerId = "1672792", encaisseeMontant = 1000f, encaisseeDateISO = "2026-07-03" };
        Assert.That(FactureElectronique.ATransmettre(r), Is.True, "la rectificative est à transmettre");
        Assert.That(FactureElectronique.EnAttente(r), Is.True);
        r.plateformeEtat = "perimee";
        Assert.That(FactureElectronique.ATransmettre(r), Is.False, "refaite hors plateforme : ses montants ne partent plus (D5-01)");
        Assert.That(FactureElectronique.EnAttente(r), Is.True, "mais l'annulation attend");
        r.plateformeEtat = "refusee";
        Assert.That(FactureElectronique.ATransmettre(r), Is.False, "refusée : elle attend « Corriger », pas un nouvel envoi tel quel");
        Assert.That(FactureElectronique.EnAttente(r), Is.True);
        r.plateformeEtat = ""; r.statut = "AttenteEnvoi";
        Assert.That(FactureElectronique.ATransmettre(r), Is.False, "pas encore émise");
        r.statut = "Paye"; r.plateformeId = "1672800";
        Assert.That(FactureElectronique.ATransmettre(r), Is.False, "déjà transmise");
        r.plateformeId = ""; r.plateformeDonnees = "";
        Assert.That(FactureElectronique.ATransmettre(r), Is.False, "sans facture électronique");
        Assert.That(FactureElectronique.ATransmettre(null), Is.False);
        r.encaisseeAAnnulerId = "";
        Assert.That(FactureElectronique.EnAttente(r), Is.False, "annulation passée, rien à transmettre : plus rien n'attend");

        string src = Src("Facturation/FactureElectronique.cs");
        StringAssert.Contains("if (ATransmettre(r))", src, "Traiter : le bloc « envoyer » passe par la règle");
        StringAssert.DoesNotContain("if (string.IsNullOrEmpty(r.plateformeId))", src, "… plus par l'identifiant vide seul");
        StringAssert.Contains("return ATransmettre(r)", src, "EnAttente lit la même règle");
    }

    /// Décision du 10/11 : le règlement d'une facture payée, reporté en versement par la correction (ApresCorrection), porte la date de
    /// l'encaissement d'origine : celle déclarée à Super PDP, sinon celle du dernier versement, sinon le jour. L'« Encaissée » de la rectificative
    /// (FactureElectronique.Encaissement : date du dernier versement) reste donc dans le mois du paiement, comme l'annulation. Avant : le jour de
    /// la correction. Au même montant, seule une facture déclarée « Encaissée », payée sans versement saisi, reçoit ce versement (sinon rien, comme avant).
    [Test]
    public void D5_06_le_reglement_reporte_garde_la_date_de_l_encaissement_d_origine()
    {
        string jour = DateTime.Today.ToString("yyyy-MM-dd");
        FactureEtat Payee(string key, float montant, string declaree = null, params Versement[] v)
            => new FactureEtat { key = key, type = "Loyer", statut = "Paye", montant = montant, pdfPath = key + ".pdf", encaisseeDeclaree = declaree != null,
                                 encaisseeMontant = declaree != null ? 1000f : 0f, encaisseeDateISO = declaree, versements = v.ToList() };
        var loc = new Locataire { id = "LC9", Name = "Sephora", facturesEtat = new List<FactureEtat>() };

        // Corrigée à la hausse, déclarée « Encaissée » le 05/10 : 1 000 € reportés au 05/10, l'écart redevient dû.
        var hausse = Payee("loyer-2026-P10", 1200f, "2026-10-05"); loc.facturesEtat.Add(hausse);
        FacturationSuivi.ApresCorrection(loc, hausse.key, 1000f);
        Assert.That(hausse.versements.Single().dateISO, Is.EqualTo("2026-10-05"), "la date déclarée, pas le jour de la correction");
        Assert.That(hausse.versements.Single().montant, Is.EqualTo(1000f));
        Assert.That(hausse.statut, Is.EqualTo("Envoye"));

        // Correction de forme (même montant) d'une facture déclarée, « Payé » à la main : le règlement est noté à sa date, elle reste payée ;
        // l'« Encaissée » de la rectificative prendra cette date (avant : le jour de sa déclaration, un autre mois de TVA).
        var forme = Payee("loyer-2026-P9", 1000f, "2026-09-04"); loc.facturesEtat.Add(forme);
        FacturationSuivi.ApresCorrection(loc, forme.key, 1000f);
        Assert.That(forme.statut, Is.EqualTo("Paye"));
        Assert.That(forme.versements.Single().dateISO, Is.EqualTo("2026-09-04"));
        Assert.That(FactureElectronique.Encaissement(loc, forme).dateISO, Is.EqualTo("2026-09-04"), "ce que la rectificative déclarera");
        FacturationSuivi.ApresCorrection(loc, forme.key, 1000f);
        Assert.That(forme.versements.Count, Is.EqualTo(1), "une seconde correction ne reporte rien de plus");

        // Date déclarée inconnue (hors plateforme) : un acompte donne sa date au reste ; sans versement : le jour ; au même montant : rien.
        var acompte = Payee("loyer-2026-P8", 1120f, null, new Versement { dateISO = "2026-08-06", montant = 300f }); loc.facturesEtat.Add(acompte);
        FacturationSuivi.ApresCorrection(loc, acompte.key, 1000f);
        Assert.That(acompte.versements.Last().dateISO, Is.EqualTo("2026-08-06"));
        Assert.That(acompte.versements.Last().montant, Is.EqualTo(700f));
        var sans = Payee("loyer-2026-P7", 1120f); loc.facturesEtat.Add(sans);
        FacturationSuivi.ApresCorrection(loc, sans.key, 1000f);
        Assert.That(sans.versements.Single().dateISO, Is.EqualTo(jour));
        var formeSans = Payee("loyer-2026-P6", 1000f); loc.facturesEtat.Add(formeSans);
        FacturationSuivi.ApresCorrection(loc, formeSans.key, 1000f);
        Assert.That(formeSans.versements, Is.Empty, "correction de forme sans date connue : rien ne change");
        // Une facture « Envoyé » encore déclarée (elle vient de quitter « Payé », l'annulation n'est pas passée) : rien n'est noté au même montant.
        var quittee = Payee("loyer-2026-P5", 1000f, "2026-05-04"); quittee.statut = "Envoye"; loc.facturesEtat.Add(quittee);
        FacturationSuivi.ApresCorrection(loc, quittee.key, 1000f);
        Assert.That(quittee.versements, Is.Empty);
        Assert.That(quittee.statut, Is.EqualTo("Envoye"));
    }

    // ── Lot 10 · D5-08 (+ D5-N7) : la correction d'une facture partie garde son taux de TVA, qui décide aussi du passage par Super PDP ──

    /// Règle (corrections sans décision, D5-08) : le taux proposé pour la correction d'une facture partie est celui imprimé sur elle, 0 compris ;
    /// une facture neuve suit le type, le bail, l'entreprise (Tva.Taux). Avant : le taux du jour (0 forcé si le bail était devenu exonéré).
    [Test]
    public void Lot10_D5_08_une_correction_garde_le_taux_de_l_epoque()
    {
        var r = new ReglageData();   // soumis, 20 %
        var loc = new Locataire { tvaLoyer = "oui", tauxTvaLoyer = 10f };
        var partie = new FactureEtat { key = "loyer-2026-P1", type = "Loyer", statut = "Envoye", tauxTva = 20f, numero = "2026/01001", pdfPath = "x.pdf", echeanceISO = "2026-01-05" };
        Assert.That(Tva.TauxPropose(loc, null, partie, r), Is.EqualTo(20f), "correction d'une facture partie : son taux, pas celui du bail");
        Assert.That(Tva.TauxPropose(loc, null, null, r), Is.EqualTo(10f), "facture neuve : le bail");
        Assert.That(Tva.TauxPropose(loc, new FactureInfo { tauxTva = 5.5f }, null, r), Is.EqualTo(5.5f), "facture neuve : le taux mémorisé du type d'abord");
        var preparee = new FactureEtat { key = "loyer-2026-P2", type = "Loyer", statut = "AttenteEnvoi", tauxTva = 20f, pdfPath = "x.pdf", echeanceISO = "2026-02-05" };
        Assert.That(Tva.TauxPropose(loc, null, preparee, r), Is.EqualTo(10f), "jamais partie : pas une correction, le bail");
        loc.tvaLoyer = "non";
        Assert.That(Tva.TauxPropose(loc, null, partie, r), Is.EqualTo(20f), "bail devenu exonéré : la correction reste taxée");
        Assert.That(Tva.TauxPropose(loc, null, null, r), Is.EqualTo(0f), "facture neuve d'un bail exonéré");
        loc.tvaLoyer = "oui"; partie.tauxTva = 0f; partie.statut = "Paye";
        Assert.That(Tva.TauxPropose(loc, null, partie, r), Is.EqualTo(0f), "facture partie exonérée : 0 compris, même sur un bail soumis");
        Assert.That(Tva.FacturePartie(partie), Is.True);
        Assert.That(Tva.FacturePartie(preparee), Is.False);
        Assert.That(Tva.FacturePartie(null), Is.False);
        // Finitions de la vague 3 (10/11) : « Payé » posé à la main, sans PDF : pas une facture. Bail exonéré : 0, pas les 20 % d'office de la ligne.
        var aLaMain = new FactureEtat { key = "loyer-2026-P3", type = "Loyer", statut = "Paye", echeanceISO = "2026-03-05" };
        Assert.That(Tva.FacturePartie(aLaMain), Is.False);
        loc.tvaLoyer = "non";
        Assert.That(Tva.TauxPropose(loc, null, aLaMain, r), Is.EqualTo(0f), "état posé à la main : facture neuve, le bail");
    }

    /// Risque transverse 5 : c'est le taux du document, et non le bail du jour, qui décide du passage par Super PDP : la correction taxée
    /// d'une facture transmise d'un bail devenu exonéré repart par la plateforme, une facture sans TVA n'y va pas, et la ligne « Envoi »
    /// ne parle plus d'un « bail exonéré » pour un document taxé (ni l'inverse).
    [Test]
    public void Lot10_D5_08_la_plateforme_suit_le_taux_du_document_pas_le_bail_du_jour()
    {
        var exonere = new Locataire { tvaLoyer = "non", siretNumber = "55203253400646" };
        var soumis = new Locataire { tvaLoyer = "oui", siretNumber = "55203253400646" };
        StringAssert.Contains("bail exonéré de TVA", FactureElectronique.LibelleEnvoi(exonere, 0f, ""), "facture neuve d'un bail exonéré");
        StringAssert.DoesNotContain("exonéré", FactureElectronique.LibelleEnvoi(exonere, 20f, ""), "correction taxée d'un bail devenu exonéré : pas « bail exonéré »");
        StringAssert.Contains("facture sans TVA", FactureElectronique.LibelleEnvoi(soumis, 0f, ""), "0 tapé, ou correction d'une facture exonérée : e-mail");
        Assert.That(FactureElectronique.Concerne(soumis, 0f, ""), Is.False, "sans TVA : pas de facture électronique");
        Assert.That(FactureElectronique.SansSiret(new Locataire { tvaLoyer = "oui" }, 0f, ""), Is.False, "sans TVA : pas de SIRET à réclamer");
        string src = Src("Facturation/FactureElectronique.cs");
        StringAssert.DoesNotContain("Active && Tva.Soumis(loc)", src, "Concerne et SansSiret ne lisent plus le bail du jour");
        Assert.That(src.Split(new[] { "Active && Math.Abs(tva) > 0.005f" }, StringSplitOptions.None).Length - 1, Is.EqualTo(2), "Concerne et SansSiret lisent le taux du document");
        StringAssert.Contains("Concerne(loc, tva, siretFormulaire)", src, "LibelleEnvoi aussi");
    }

    /// Les trois formulaires : ChargerTva propose le taux du document (champ montré même à 0 pour une correction), TauxFacture rend le taux
    /// tapé (0 compris) sinon le taux proposé (plus de 0 forcé par le bail du jour), SaveFromUI ne mémorise pas le taux d'une correction,
    /// la plateforme et la ligne « Envoi » lisent le taux du document dès l'ouverture, et le taux proposé suit la facture visée (période,
    /// année, listes, charges : RefreshTauxDefault) et repart de la facture émise (Finaliser).
    [Test]
    public void Lot10_D5_08_les_trois_formulaires_lisent_le_taux_du_document()
    {
        foreach (var p in new[] { "Facturation/FactureLoyerPanel.cs", "Facturation/FactureRegulPanel.cs", "Facturation/FactureRefacPanel.cs" })
        {
            string s = Src(p);
            StringAssert.Contains("Tva.TauxPropose(_loc, f, corrigee)", s, p + " : ChargerTva");
            StringAssert.Contains("Tva.TauxPropose(_loc, null, Corrigee())", s, p + " : TauxFacture");
            StringAssert.Contains("&& t >= 0f", s, p + " : un 0 tapé est accepté");
            StringAssert.DoesNotContain("if (!Tva.Soumis(_loc)) return 0f;", s, p + " : plus de 0 forcé par le bail du jour");
            StringAssert.Contains("if (Corrigee() == null) f.tauxTva = TauxAMemoriser();", s, p + " : SaveFromUI");
            StringAssert.DoesNotContain("Concerne(_loc, 1f,", s, p + " : la plateforme suit le taux du document dès l'ouverture");
            StringAssert.Contains("LibelleEnvoi(_loc, TauxFacture(),", s, p + " : ligne « Envoi »");
            StringAssert.Contains("RefreshTauxDefault();", s, p + " : le taux proposé suit la facture visée");
        }
    }

    // ── Lot 11 · D5-05 / D5-07 (décision du 05/11) : Super PDP indisponible : refus avant l'envoi dans les trois formulaires (+ D4-16) ──

    static readonly string[] TroisFormulaires = { "Facturation/FactureLoyerPanel.cs", "Facturation/FactureRegulPanel.cs", "Facturation/FactureRefacPanel.cs" };

    /// Règle pure : depuis le 1/9/2027 (ou l'essai en Play), une facture avec TVA (taux du document, avoir compris) à un locataire qui a un
    /// SIREN doit partir par la plateforme, quel que soit l'état de la SCI ; le jour qui compte est celui du départ. Le refus au centre
    /// (RefusObligation) ne tombe que si elle n'y part pas, avec la raison ; une facture pas concernée (sans TVA, sans SIRET, avant la
    /// bascule) n'est pas refusée. Avant : elle partait par e-mail seul, sans un mot.
    [Test]
    public void Lot11_D5_05_une_facture_avec_TVA_a_un_professionnel_doit_partir_par_la_plateforme_des_la_bascule()
    {
        const string siret = "55203253400646";
        Assert.That(FactureElectronique.DoitPartirParPlateforme(240f, siret, new DateTime(2027, 9, 1)), Is.True, "le jour de la bascule");
        Assert.That(FactureElectronique.DoitPartirParPlateforme(240f, siret, new DateTime(2027, 8, 31)), Is.False, "la veille : pas encore");
        Assert.That(FactureElectronique.DoitPartirParPlateforme(-240f, siret, new DateTime(2027, 9, 16)), Is.True, "un avoir (TVA négative) aussi");
        Assert.That(FactureElectronique.DoitPartirParPlateforme(0f, siret, new DateTime(2027, 9, 16)), Is.False, "sans TVA : pas de facture électronique");
        Assert.That(FactureElectronique.DoitPartirParPlateforme(240f, "", new DateTime(2027, 9, 16)), Is.False, "sans SIRET : l'annuaire ne le trouve pas");
        Assert.That(FactureElectronique.DoitPartirParPlateforme(240f, "12345", new DateTime(2027, 9, 16)), Is.False, "SIRET tronqué");
        Assert.That(FactureElectronique.DoitPartirParPlateforme(240f, null, new DateTime(2027, 9, 16)), Is.False);

        var loc = new Locataire { tvaLoyer = "oui", siretNumber = siret, Name = "Dupont" };
        var sans = new Locataire { tvaLoyer = "oui", Name = "Asso" };
        // EditMode : pas de session cloud, l'état de Super PDP n'est jamais lu → Concerne est faux (parPlateforme faux).
        Assert.That(FactureElectronique.Concerne(loc, 20f, ""), Is.False, "préalable : hors plateforme ici");
        string refus = FactureElectronique.RefusObligation(loc, 20f, "", "Loyer Novembre 2027", false, new DateTime(2027, 9, 16));
        Assert.That(refus, Does.Contain("doit partir par la plateforme agréée").And.Contain("Rien n'a été émis").And.Contain("Loyer Novembre 2027"));
        Assert.That(refus, Does.Contain("l'état de Super PDP n'est pas encore lu"), "la raison : la même que la ligne « Envoi »");
        Assert.That(FactureElectronique.RefusObligation(loc, 20f, "", "Loyer", true, new DateTime(2027, 9, 16)), Is.Null, "elle part par la plateforme : rien à refuser");
        Assert.That(FactureElectronique.RefusObligation(loc, 20f, "", "Loyer", false, new DateTime(2027, 8, 16)), Is.Null, "avant la bascule : e-mail seul permis");
        Assert.That(FactureElectronique.RefusObligation(sans, 20f, "", "Loyer", false, new DateTime(2027, 9, 16)), Is.Null, "sans SIRET : pas concernée");
        Assert.That(FactureElectronique.RefusObligation(sans, 20f, siret, "Loyer", false, new DateTime(2027, 9, 16)), Is.Not.Null, "le SIRET du formulaire compte (acheteur du PDF)");
        Assert.That(FactureElectronique.RefusObligation(loc, 0f, "", "Loyer", false, new DateTime(2027, 9, 16)), Is.Null, "sans TVA : pas concernée");

        Assert.That(FactureElectronique.RaisonHorsPlateforme(loc, 0f, ""), Does.Contain("sans TVA"));
        Assert.That(FactureElectronique.RaisonHorsPlateforme(sans, 20f, ""), Does.Contain("SIRET"));
        Assert.That(FactureElectronique.RaisonHorsPlateforme(loc, 20f, ""), Does.Contain("pas encore lu"), "état jamais lu (ni en vérification, ni non lu au lancement, ni déliée)");

        try
        {
            FactureElectronique.EssaiApresBascule = true;   // réglé par script en Play, lu par la règle
            Assert.That(FactureElectronique.DoitPartirParPlateforme(240f, siret, new DateTime(2026, 11, 10)), Is.True, "l'essai fait comme si la bascule était passée");
            Assert.That(FactureElectronique.RefusObligation(loc, 20f, "", "Loyer", false, DateTime.Today), Is.Not.Null);
        }
        finally { FactureElectronique.EssaiApresBascule = false; }
    }

    /// Les trois formulaires refusent AVANT tout envoi (plus de « parPlateforme && refusCorrection »), la plateforme se décide une fois et
    /// suit jusqu'à Finaliser, et le loyer ne prévient plus après l'e-mail. Dans le loyer, les refus (total nul, correction transmise,
    /// obligation, avoir sans référence) passent avant l'envoi différé, à la date du départ.
    [Test]
    public void Lot11_D5_05_D5_07_les_trois_formulaires_refusent_avant_l_envoi()
    {
        foreach (var p in TroisFormulaires)
        {
            string s = Src(p);
            StringAssert.DoesNotContain("&& refusCorrection != null", s, p + " : le refus se lit seul");
            StringAssert.DoesNotContain("Concerne(_loc, d.tva, _siret.text) && ", s, p + " : la plateforme n'est plus relue à chaque garde");
            Assert.That(s.Split(new[] { "FactureElectronique.Concerne(_loc, d.tva," }, StringSplitOptions.None).Length - 1, Is.EqualTo(1), p + " : parPlateforme décidé une fois");
            StringAssert.Contains(", d.subtitle, parPlateforme,", s, p + " : RefusCorrection reçoit parPlateforme");
            StringAssert.Contains("FactureElectronique.RaisonHorsPlateforme(_loc, d.tva,", s, p + " : avec la raison");
            StringAssert.Contains(", d.subtitle, parPlateforme, ", s, p + " : RefusObligation (D5-05)");
            StringAssert.Contains("?? FactureElectronique.RefusObligation(_loc, d.tva,", s, p + " : la garde de la bascule, après celle de la correction");
            StringAssert.Contains("bool envoye, bool parPlateforme,", s, p + " : Finaliser reçoit la décision, ne rappelle plus Concerne");
            // Sans « string dest » : dans la refacturation, la signature est coupée après « bool parPlateforme, » (mise en forme d'origine).
            StringAssert.Contains("bool correction, string pdf, bool parPlateforme,", s, p + " : EnvoyerPuisFinaliser la transmet");
        }
        string loyer = Src("Facturation/FactureLoyerPanel.cs");
        StringAssert.DoesNotContain("cette correction n'y part pas", loyer, "plus de message après l'e-mail (D5-07)");
        StringAssert.DoesNotContain("dejaTransmise", loyer);
        StringAssert.Contains("FactureElectronique.Preparer(ligne, !parPlateforme ? null", loyer, "la facture électronique suit la même décision");
        foreach (var p in new[] { "Facturation/FactureRegulPanel.cs", "Facturation/FactureRefacPanel.cs" })
            StringAssert.Contains("parPlateforme, new FactureEn16931.Entree", Src(p), p + " : Preparer(ligne, avant, parPlateforme, …) reçoit la décision");

        int differe = loyer.IndexOf("PreparerPourEnvoi(d, key, emission, correction, pdf, dateEnvoi.Value, parPlateforme)", StringComparison.Ordinal);
        Assert.That(differe, Is.GreaterThan(0));
        foreach (var garde in new[] { "if (FactureEmission.RefusTotalNul(d.ttc, d.subtitle) is string refusNul)", "FactureElectronique.RefusCorrection(", "FactureElectronique.RefusObligation(",
                                      "FactureElectronique.ReferencesAvoir(_loc, _loc.facturesEtat" })
        {
            int i = loyer.IndexOf(garde, StringComparison.Ordinal);
            Assert.That(i, Is.GreaterThan(0).And.LessThan(differe), "loyer : « " + garde + " » passe avant l'envoi différé (un 0 €, un avoir sans référence ou une facture due à la plateforme ne s'enregistre plus pour plus tard)");
        }
        StringAssert.Contains("DateTime depart = !correction && dateEnvoi > DateTime.Today ? dateEnvoi.Value : DateTime.Today;", loyer, "la date qui compte est celle du départ");
        StringAssert.Contains("if (!correction && dateEnvoi.HasValue", loyer, "l'envoi différé lui-même ne change pas (Revue16cTests.D4_03)");
    }

    /// Règle : la correction d'une facture transmise qui ne part pas par la plateforme est refusée (décision du 05/11 : une SCI non reliée ne
    /// corrige plus une facture transmise), avec la raison ; par la plateforme, la rectificative passe (D5-02 inchangé) ; une facture jamais
    /// transmise se corrige librement ; le refus du transfert (D5-20, lot 7) prime.
    [Test]
    public void Lot11_D5_07_une_correction_transmise_ne_part_pas_hors_plateforme()
    {
        var transmise = new FactureEtat { plateformeId = "pdp-1", plateformeDonnees = "{\"number\":\"2026/10004\",\"type_code\":380}" };
        string refus = FactureElectronique.RefusCorrection(transmise, 1620f, "Refacturation Taxe foncière 2026", parPlateforme: false, raison: "Super PDP : état non lu au lancement ; relancez CIPL");
        Assert.That(refus, Does.Contain("a été transmise à Super PDP").And.Contain("Rien n'a été émis").And.Contain("état non lu au lancement").And.Contain("Recommencez"));
        Assert.That(FactureElectronique.RefusCorrection(transmise, 1620f, "Refacturation", parPlateforme: false), Does.Contain("Rien n'a été émis"), "sans raison : le refus tient quand même");
        Assert.That(FactureElectronique.RefusCorrection(transmise, 1620f, "Refacturation", parPlateforme: true), Is.Null, "par la plateforme : la rectificative remplace");
        Assert.That(FactureElectronique.RefusCorrection(transmise, 1620f, "Refacturation"), Is.Null, "valeur par défaut : par la plateforme (Revue17Tests.D5_02)");
        Assert.That(FactureElectronique.RefusCorrection(transmise, -240f, "Refacturation", parPlateforme: false), Does.Contain("doit y partir aussi"), "hors plateforme, le refus D5-07 passe avant celui de l'avoir");
        Assert.That(FactureElectronique.RefusCorrection(new FactureEtat { plateformeDonnees = "{\"type_code\":380}" }, 1620f, "Refacturation", parPlateforme: false), Is.Null, "jamais transmise : libre");
        Assert.That(FactureElectronique.RefusCorrection(null, 1620f, "Refacturation", parPlateforme: false), Is.Null);
        var transferee = new FactureEtat { plateformeId = "pdp-9", plateformeOrigine = "SCI (JBC)", plateformeDonnees = "{\"type_code\":380}" };
        Assert.That(FactureElectronique.RefusCorrection(transferee, 880f, "Loyer Août 2026", parPlateforme: false), Does.Contain("SCI (JBC)"), "D5-20 prime : il dit quoi faire");
    }

    /// La ligne « Envoi » : après la bascule, une facture due à la plateforme qui n'y part pas est annoncée « impossible pour le moment »,
    /// avec la raison, au lieu d'« e-mail seulement » ; avant la bascule, « e-mail seulement (raison) » couvre aussi la SCI non reliée et
    /// l'état jamais lu (avant : « Mode global : Email direct (SMTP). »). Les cas sans TVA et sans SIRET ne changent pas.
    [Test]
    public void Lot11_D5_05_la_ligne_Envoi_annonce_le_refus_apres_la_bascule()
    {
        var loc = new Locataire { tvaLoyer = "oui", siretNumber = "55203253400646" };
        var sans = new Locataire { tvaLoyer = "oui" };
        try
        {
            FactureElectronique.EssaiApresBascule = false;
            string avant = FactureElectronique.LibelleEnvoi(loc, 20f, "");
            Assert.That(avant, Does.StartWith("Envoi : e-mail seulement (").And.Not.Contain("impossible"), "avant la bascule : e-mail seul, avec la raison");
            StringAssert.DoesNotContain("Mode global", avant);
            FactureElectronique.EssaiApresBascule = true;
            string apres = FactureElectronique.LibelleEnvoi(loc, 20f, "");
            Assert.That(apres, Does.Contain("impossible pour le moment").And.Contain("doit partir par Super PDP").And.Contain("pas encore lu"));
            Assert.That(FactureElectronique.LibelleEnvoi(loc, 20f, "", true), Does.Contain("impossible pour le moment"), "avec ou sans le paramètre notification (lot 13 : les trois formulaires le passent) : la règle est dans la fonction");
            Assert.That(FactureElectronique.LibelleEnvoi(sans, 20f, ""), Does.StartWith("Envoi : e-mail seulement (").And.Contain("SIRET"), "sans SIRET : pas due à la plateforme");
            StringAssert.Contains("facture sans TVA", FactureElectronique.LibelleEnvoi(loc, 0f, ""));
        }
        finally { FactureElectronique.EssaiApresBascule = false; }
        string src = Src("Facturation/FactureElectronique.cs");
        StringAssert.Contains(": EtatInconnu ?", src, "Revue16cTests.D1_06 : la raison « état non lu » reste");
        StringAssert.Contains("DoitPartirParPlateforme(tva, SiretAcheteur(loc, siretFormulaire), depart ?? DateTime.Today)", src, "LibelleEnvoi juge la bascule dans la fonction (coutures : au jour du départ)");
    }

    /// D4-16 (D4-N15) : l'envoi groupé n'envoie plus l'e-mail « copie pour information… transmise par notre plateforme » quand la SCI est
    /// connue comme déliée (état lu, non reliée) : le PDF part avec l'e-mail de facture ordinaire. Jamais sur Active (faux tant que l'état
    /// n'est pas lu : l'envoi est proposé avant Demarrer, la transmission suivrait l'e-mail de facture).
    [Test]
    public void Lot11_D4_16_l_envoi_groupe_n_annonce_plus_une_copie_pour_une_SCI_deliee()
    {
        Assert.That(FactureElectronique.Deliee, Is.False, "état jamais lu (EditMode) : pas « déliée connue »");
        Assert.That(FactureElectronique.Active, Is.False);
        string src = Src("Facturation/FactureElectronique.cs");
        StringAssert.Contains("public static bool Deliee => Courante && !_relie;", src, "déliée = état lu ET non reliée");
        string auto = Src("Facturation/FactureEnvoiAuto.cs");
        StringAssert.Contains("bool plateforme = FactureElectronique.PartParPlateforme(c.rec) && !FactureElectronique.Deliee;", auto, "EnvoyerUne : plus de « copie » pour une SCI déliée (lot 12 : ni pour une ligne périmée)");
        StringAssert.DoesNotContain("plateformeDonnees) && FactureElectronique.Active", auto, "jamais Active (D4.md, vérification du 09/11)");
    }

    // ── Lot 12 · D5-05 (décision du 05/11) : envoi au lancement après le 1/9/2027 : facture sans facture électronique retenue (+ D5-21) ──

    const string SiretLot12 = "55203253400646";

    static Locataire LocLot12(string email = "locataire@exemple.fr", string siret = SiretLot12)
        => new Locataire { id = "LC12", Name = "Dupont", tvaLoyer = "oui", siretNumber = siret, emailLocataire = email };

    /// Une ligne « en attente d'envoi » prête (PDF, 3 000 € TTC au taux de la ligne), sans facture électronique sauf `json`.
    static FactureEtat LigneLot12(string echeanceISO, string json = null, string etat = null, string type = "Loyer", float taux = 20f, float montant = 3000f)
        => new FactureEtat { key = type == "Loyer" ? "loyer-2027-P1" : "depot-2027", type = type, libelle = "Loyer Octobre 2027", statut = "AttenteEnvoi",
                             echeanceISO = echeanceISO, pdfPath = "Batiment/Test/Dupont/Facture/loyer.pdf", montant = montant, tauxTva = taux,
                             plateformeDonnees = json, plateformeEtat = etat };

    static bool PartLot12(Locataire loc, FactureEtat rec, DateTime jour, out string dest)
        => FactureEnvoiAuto.DoitPartir("Test", loc, rec, jour, _ => true, out dest, out _);

    /// Règle : après la bascule, une facture avec TVA à un locataire qui a un SIRET et qui n'a pas sa facture électronique (préparée quand Super PDP
    /// n'était pas actif, ou refaite hors plateforme : JSON périmé) ne part plus par e-mail seul au lancement ; avant la bascule, avec sa facture
    /// électronique, sans TVA, sans SIRET, à total nul ou pour un décompte de sortie, rien ne change. Dates fixes : le test ne vieillit pas.
    [Test]
    public void Lot12_D5_05_apres_la_bascule_une_facture_sans_facture_electronique_ne_part_pas_par_e_mail()
    {
        var sept = new DateTime(2027, 9, 16);   // la date d'envoi d'une échéance au 01/10 (15 jours avant)
        var loc = LocLot12();
        Assert.That(PartLot12(loc, LigneLot12("2027-10-01"), sept, out _), Is.False, "après la bascule, sans facture électronique : retenue (à refaire)");
        Assert.That(PartLot12(loc, LigneLot12("2027-08-31"), new DateTime(2027, 8, 16), out _), Is.True, "avant la bascule : e-mail seul permis, comme avant");
        Assert.That(PartLot12(loc, LigneLot12("2027-10-01", json: "{}"), sept, out _), Is.True, "avec sa facture électronique : elle part");
        Assert.That(PartLot12(loc, LigneLot12("2027-10-01", json: "{}", etat: "perimee"), sept, out _), Is.False, "refaite hors plateforme : son JSON périmé ne compte pas");
        Assert.That(PartLot12(loc, LigneLot12("2027-10-01", json: "{}", etat: "refusee"), sept, out _), Is.False, "refusée : à corriger d'abord (16c)");
        Assert.That(PartLot12(loc, LigneLot12("2027-10-01", type: "Depot"), sept, out _), Is.True, "le décompte de sortie ne passe pas par la plateforme (D5-18, à part)");
        Assert.That(PartLot12(loc, LigneLot12("2027-10-01", taux: 0f), sept, out _), Is.True, "sans TVA : pas de facture électronique due");
        Assert.That(PartLot12(loc, LigneLot12("2027-10-01", montant: 0f), sept, out _), Is.True, "total nul : pas de TVA, pas retenue (elle serait refusée au « Refaire »)");
        Assert.That(PartLot12(LocLot12(siret: ""), LigneLot12("2027-10-01"), sept, out _), Is.True, "sans SIRET : l'annuaire ne le trouverait pas, e-mail comme avant");
    }

    /// Les deux règles pures : PartParPlateforme (JSON présent, ni périmée ni refusée) et ARefaire (pas PartParPlateforme, due à la plateforme au
    /// jour du départ : TVA du document au taux de la ligne, SIRET de l'acheteur de la ligne dans l'ordre du formulaire).
    [Test]
    public void Lot12_D5_05_PartParPlateforme_et_ARefaire_jugent_la_ligne_au_jour_du_depart()
    {
        Assert.That(FactureElectronique.PartParPlateforme(null), Is.False);
        Assert.That(FactureElectronique.PartParPlateforme(new FactureEtat()), Is.False, "sans JSON");
        Assert.That(FactureElectronique.PartParPlateforme(new FactureEtat { plateformeDonnees = "{}" }), Is.True);
        Assert.That(FactureElectronique.PartParPlateforme(new FactureEtat { plateformeDonnees = "{}", plateformeEtat = "envoyee" }), Is.True);
        Assert.That(FactureElectronique.PartParPlateforme(new FactureEtat { plateformeDonnees = "{}", plateformeEtat = "perimee" }), Is.False, "refaite hors plateforme");
        Assert.That(FactureElectronique.PartParPlateforme(new FactureEtat { plateformeDonnees = "{}", plateformeEtat = "refusee" }), Is.False);

        var loc = LocLot12();
        var sans = LigneLot12("2027-10-01");
        Assert.That(FactureElectronique.ARefaire(loc, sans, new DateTime(2027, 9, 1)), Is.True, "le jour de la bascule");
        Assert.That(FactureElectronique.ARefaire(loc, sans, new DateTime(2027, 8, 31)), Is.False, "la veille");
        Assert.That(FactureElectronique.ARefaire(loc, LigneLot12("2027-10-01", json: "{}"), new DateTime(2027, 9, 16)), Is.False, "elle a sa facture électronique");
        Assert.That(FactureElectronique.ARefaire(loc, LigneLot12("2027-10-01", json: "{}", etat: "perimee"), new DateTime(2027, 9, 16)), Is.True, "périmée : comme sans");
        Assert.That(FactureElectronique.ARefaire(loc, LigneLot12("2027-10-01", json: "{}", etat: "refusee"), new DateTime(2027, 9, 16)), Is.False, "refusée par Super PDP : à corriger (16c D5-05), pas « préparée sans facture électronique »");
        Assert.That(FactureElectronique.ARefaire(loc, LigneLot12("2027-10-01", type: "Depot"), new DateTime(2027, 9, 16)), Is.False);
        Assert.That(FactureElectronique.ARefaire(loc, LigneLot12("2027-10-01", taux: 0f), new DateTime(2027, 9, 16)), Is.False, "le taux de la ligne, pas le bail du jour");
        Assert.That(FactureElectronique.ARefaire(loc, LigneLot12("2027-10-01", montant: 0f), new DateTime(2027, 9, 16)), Is.False, "total nul : pas de TVA");
        Assert.That(FactureElectronique.ARefaire(LocLot12(siret: ""), sans, new DateTime(2027, 9, 16)), Is.False);
        Assert.That(FactureElectronique.ARefaire(null, sans, new DateTime(2027, 9, 16)), Is.False);
        Assert.That(FactureElectronique.ARefaire(loc, null, new DateTime(2027, 9, 16)), Is.False);

        Assert.That(FactureEnvoiAuto.SiretLigne(loc, sans), Is.EqualTo(SiretLot12), "la fiche");
        loc.factureLoyer = new FactureInfo { destSiret = "11111111100011" };
        Assert.That(FactureEnvoiAuto.SiretLigne(loc, sans), Is.EqualTo("11111111100011"), "le SIRET mémorisé sur le réglage du type (l'acheteur du PDF) d'abord");
        var cedant = LocLot12();
        cedant.cessions = new List<CessionBail> { new CessionBail { dateISO = "2027-11-01", ancienNom = "SARL X", ancienSiret = "", ancienEmail = "x@x.fr" } };
        var duCedant = LigneLot12("2027-10-01"); duCedant.acheteur = "SARL X";
        Assert.That(FactureEnvoiAuto.SiretLigne(cedant, duCedant), Is.Empty, "la facture d'un cédant sans SIRET : aucun, jamais celui de la fiche");
        Assert.That(FactureElectronique.ARefaire(cedant, duCedant, new DateTime(2027, 9, 16)), Is.False, "donc pas due à la plateforme");
        Assert.That(FactureEnvoiAuto.SiretLigne(null, sans), Is.Empty);
    }

    /// D5-21 (D5-N4) : après la bascule, un loyer qui a sa facture électronique part par la plateforme seule : pas besoin d'adresse e-mail (Notifier
    /// dira que le locataire n'a pas été prévenu) ; sans facture électronique, il est retenu (D5-05) ; avant la bascule, l'adresse reste exigée.
    [Test]
    public void Lot12_D5_21_apres_la_bascule_un_loyer_par_la_plateforme_part_sans_adresse()
    {
        var sansEmail = LocLot12(email: "");
        string echeance = DateTime.Today.AddDays(15).ToString("yyyy-MM-dd");   // date d'envoi : aujourd'hui
        try
        {
            FactureElectronique.EssaiApresBascule = true;
            Assert.That(PartLot12(sansEmail, LigneLot12(echeance, json: "{}"), DateTime.Today, out string dest), Is.True, "par la plateforme seule : elle part");
            Assert.That(dest, Is.Empty, "sans adresse : rien ne part d'ici, la plateforme envoie");
            Assert.That(PartLot12(sansEmail, LigneLot12(echeance), DateTime.Today, out _), Is.False, "sans facture électronique : retenue (D5-05)");
            Assert.That(PartLot12(sansEmail, LigneLot12(echeance, json: "{}", etat: "perimee"), DateTime.Today, out _), Is.False, "périmée : retenue aussi");
            Assert.That(FactureEnvoiAuto.ParPlateformeSeule(LigneLot12(echeance, json: "{}")), Is.True);
            Assert.That(FactureEnvoiAuto.ParPlateformeSeule(LigneLot12(echeance)), Is.False);
        }
        finally { FactureElectronique.EssaiApresBascule = false; }
        if (DateTime.Today < FactureElectronique.Bascule)
        {
            Assert.That(PartLot12(sansEmail, LigneLot12(echeance, json: "{}"), DateTime.Today, out _), Is.False, "avant la bascule : le PDF part par e-mail, l'adresse est exigée");
            Assert.That(FactureEnvoiAuto.ParPlateformeSeule(LigneLot12(echeance, json: "{}")), Is.False);
        }
        string auto = Src("Facturation/FactureEnvoiAuto.cs");
        StringAssert.Contains("return ParPlateformeSeule(rec) || !string.IsNullOrWhiteSpace(destinataire);", auto, "DoitPartir");
        StringAssert.Contains("liste = liste.Where(c => ParPlateformeSeule(c.rec)).ToList();", auto, "sans e-mail réglé, les factures par la plateforme seule sont quand même proposées");
        StringAssert.Contains("else if (!ParPlateformeSeule(rec) && string.IsNullOrWhiteSpace(Destinataire(loc, rec.type, rec)))", auto, "le diagnostic ne dit plus « aucune adresse » pour elles");
    }

    /// L'envoi au lancement relit la règle : EnvoyerUne (après Retrouver, la file d'attente comprise) retient une ligne refaite hors plateforme
    /// entre-temps ; « par la plateforme » = PartParPlateforme (une ligne périmée part avec l'e-mail de facture et le PDF) ; les lignes retenues sont
    /// nommées au centre quel que soit le retour, après la fenêtre d'envoi (sinon cachées par son voile et effacées par « Tout envoyer », D8-02),
    /// jugées au jour de leur départ (une refusée n'en est pas) ; le diagnostic les explique ; ReprendreEnLigne relit l'état de Super PDP ; le toast
    /// « état non lu » dit qu'après la bascule rien ne part.
    [Test]
    public void Lot12_D5_05_le_lancement_nomme_les_retenues_et_relit_l_etat_de_Super_PDP()
    {
        var loc = LocLot12();
        var sans = LigneLot12("2027-10-01");   // date d'envoi : le 16/09/2027
        Assert.That(FactureEnvoiAuto.Retenue(loc, sans, new DateTime(2027, 8, 20)), Is.True, "jugée au jour du départ (16/09) : nommée dès la préparation");
        Assert.That(FactureEnvoiAuto.Retenue(loc, LigneLot12("2027-09-10"), new DateTime(2027, 8, 20)), Is.False, "part le 26/08 : avant la bascule");
        Assert.That(FactureEnvoiAuto.Retenue(loc, sans, new DateTime(2027, 10, 20)), Is.True, "en retard : aujourd'hui");
        Assert.That(FactureEnvoiAuto.Retenue(loc, LigneLot12("2027-10-01", json: "{}"), new DateTime(2027, 9, 20)), Is.False, "elle a sa facture électronique");
        Assert.That(FactureEnvoiAuto.Retenue(loc, LigneLot12("2027-10-01", json: "{}", etat: "refusee"), new DateTime(2027, 9, 20)), Is.False, "refusée par Super PDP (revenue en attente d'envoi) : la pastille et le refus le disent, pas ce message");
        var partie = LigneLot12("2027-10-01"); partie.statut = "Envoye";
        Assert.That(FactureEnvoiAuto.Retenue(loc, partie, new DateTime(2027, 9, 20)), Is.False, "seule une ligne en attente d'envoi");
        Assert.That(FactureEnvoiAuto.Retenue(null, sans, new DateTime(2027, 9, 20)), Is.False);

        string un = FactureEnvoiAuto.MessageRetenues(new[] { "Dupont / Loyer Octobre 2027" });
        Assert.That(un, Does.StartWith("Une facture ne partira pas à sa date :").And.Contain("• Dupont / Loyer Octobre 2027").And.Contain("« Refaire »").And.Contain("chaque lancement"));
        Assert.That(FactureEnvoiAuto.MessageRetenues(new[] { "a", "b" }), Does.StartWith("2 factures ne partiront pas à leur date :"));

        string auto = Src("Facturation/FactureEnvoiAuto.cs");
        StringAssert.Contains("if (FactureElectronique.ARefaire(loc, rec, aujourdhui)) return false;", auto, "DoitPartir");
        StringAssert.Contains("bool plateforme = FactureElectronique.PartParPlateforme(c.rec) && !FactureElectronique.Deliee;", auto, "EnvoyerUne : une ligne périmée n'est pas « par la plateforme »");
        int retrouver = auto.IndexOf(": null) ?? choisie;", StringComparison.Ordinal);
        int relu = auto.IndexOf("if (FactureElectronique.ARefaire(c.loc, c.rec, DateTime.Today)) { issue.Erreur = TexteARefaire; tenu = false; yield break; }", StringComparison.Ordinal);
        int envoi = auto.IndexOf("envoi = EmailService.Envoyer(c.destinataire", StringComparison.Ordinal);
        Assert.That(relu, Is.GreaterThan(retrouver).And.LessThan(envoi), "le contrôle est refait sur la ligne relue, avant l'e-mail (file d'attente comprise : même fonction)");
        int calcul = auto.IndexOf("var retenues = Retenues(bps, DateTime.Today);", StringComparison.Ordinal);
        int lectureSeule = auto.IndexOf("if (AbonnementCloud.LectureSeule)", StringComparison.Ordinal);
        int construire = auto.IndexOf(".Construire(liste);", StringComparison.Ordinal);
        int nomme = auto.IndexOf("finally { if (retenues.Count > 0) ConfirmDialog.Erreur(MessageRetenues(retenues)); }", StringComparison.Ordinal);
        Assert.That(calcul, Is.GreaterThan(0).And.LessThan(lectureSeule), "retenues calculées avant les retours anticipés (lecture seule, e-mail, « Plus tard »)");
        Assert.That(nomme, Is.GreaterThan(construire), "nommées en dernier, après la fenêtre d'envoi (au-dessus de son voile, pas effacées par « Tout envoyer » : D8-02), sur chaque retour");
        int horsTitulaire = auto.IndexOf("else if (Cessions.HorsTitulaire(loc, rec))", StringComparison.Ordinal);
        int aRefaire = auto.IndexOf("else if (FactureElectronique.ARefaire(loc, rec, DateTime.Today))", StringComparison.Ordinal);
        int refusee = auto.IndexOf("refusée par la plateforme (à corriger depuis le suivi)", StringComparison.Ordinal);
        Assert.That(aRefaire, Is.GreaterThan(horsTitulaire).And.LessThan(refusee), "Diagnostic : la raison avant « refusée par la plateforme »");

        string demarrage = Src("Cloud/DemarrageCloud.cs");
        int reprendre = demarrage.IndexOf("public static async void ReprendreEnLigne()", StringComparison.Ordinal);
        Assert.That(reprendre, Is.GreaterThan(0));
        Assert.That(demarrage.IndexOf("FactureElectronique.Demarrer(e);", reprendre, StringComparison.Ordinal), Is.GreaterThan(reprendre),
                    "la reprise en ligne relit l'état de Super PDP (sinon tout est refusé ou retenu toute la session)");

        string src = Src("Facturation/FactureElectronique.cs");
        StringAssert.Contains("ne peuvent pas être émises d'ici le prochain lancement", src, "toast « état non lu » après la bascule");
        StringAssert.Contains("partiront par e-mail seul", src, "avant la bascule : comme avant");
    }

    // ── Lot 13 · D5-10 (+ D5-26 / D5-V1) : régularisation et refacturation suivent la règle du loyer (copie, puis notification), le SIRET tapé est suivi ──

    /// Une règle pour les trois formulaires (EmailService) : le texte du mode (propre à la facture, sinon le modèle des Réglages : comparé aux
    /// fonctions, les Réglages du poste ont pu changer les modèles), l'enregistrement dans la paire du mode affiché, le texte qui part quand le
    /// mode affiché n'est pas le vrai (dans les deux sens), et l'aide. Avant : régul et refac n'avaient que l'e-mail de facture.
    [Test]
    public void Lot13_D5_10_une_regle_du_mode_de_l_e_mail_pour_les_trois_formulaires()
    {
        var f = new FactureInfo { emailObjet = "Ma facture", copieObjet = "Ma copie", notifObjet = "Ma notification" };
        Assert.That(EmailService.Modeles(f, true, false).objet, Is.EqualTo("Ma copie"), "par la plateforme, avant la bascule : la paire « copie »");
        Assert.That(EmailService.Modeles(f, true, true).objet, Is.EqualTo("Ma notification"), "après : la notification");
        Assert.That(EmailService.Modeles(f, false, false).objet, Is.EqualTo("Ma facture"));
        Assert.That(EmailService.Modeles(f, false, true).objet, Is.EqualTo("Ma facture"), "la notification suppose la plateforme");
        Assert.That(EmailService.Modeles(f, true, false).corps, Is.EqualTo(EmailService.CorpsCopie()), "corps non retouché : le modèle");
        Assert.That(EmailService.Modeles(null, false, false).objet, Is.EqualTo(EmailService.ObjetFacture()));
        Assert.That(EmailService.Modeles(null, true, false).objet, Is.EqualTo(EmailService.ObjetCopie()));
        Assert.That(EmailService.Modeles(null, true, true).objet, Is.EqualTo(EmailService.ObjetNotif()));
        Assert.That(EmailService.Modeles(null, true, true).corps, Is.EqualTo(EmailService.CorpsNotif()));

        // Garder : dans la paire du mode affiché ; un texte égal au modèle redevient vide (il suit le modèle) ; les autres paires ne bougent pas.
        var g = new FactureInfo { emailObjet = "Ma facture" };
        EmailService.Garder(g, true, false, "Objet copie à moi", EmailService.CorpsCopie());
        Assert.That(g.copieObjet, Is.EqualTo("Objet copie à moi"));
        Assert.That(g.copieCorps, Is.Empty, "le corps suit le modèle");
        Assert.That(g.emailObjet, Is.EqualTo("Ma facture"), "la paire de l'e-mail de facture est intacte : un e-mail personnalisé ne devient pas la « copie »");
        EmailService.Garder(g, true, true, "Notif à moi", "Corps notif à moi");
        Assert.That(g.notifObjet, Is.EqualTo("Notif à moi"));
        Assert.That(g.notifCorps, Is.EqualTo("Corps notif à moi"));
        Assert.That(g.copieObjet, Is.EqualTo("Objet copie à moi"), "la paire « copie » est intacte");
        EmailService.Garder(g, false, false, EmailService.ObjetFacture(), "Corps à moi");
        Assert.That(g.emailObjet, Is.Empty);
        Assert.That(g.emailCorps, Is.EqualTo("Corps à moi"));
        EmailService.Garder(null, true, false, "x", "y");   // facture du type jamais enregistrée : rien à garder, pas d'exception

        // TexteEnvoi : le texte affiché s'il est du vrai mode (vide : le modèle des Réglages), sinon le texte du vrai mode : « copie » affichée
        // mais plus de plateforme → l'e-mail de facture (celui propre à la facture) ; facture affichée mais plateforme → la copie.
        var affiche = EmailService.TexteEnvoi(f, true, true, "Objet tapé", "");
        Assert.That(affiche.objet, Is.EqualTo("Objet tapé"), "affiché dans le vrai mode : tel quel");
        Assert.That(affiche.corps, Is.EqualTo(EmailService.CorpsCopie()), "vide → le modèle des Réglages");
        var copieFausse = EmailService.TexteEnvoi(f, false, false, "Objet copie affiché", "Corps copie affiché");
        Assert.That(copieFausse.objet, Is.EqualTo("Ma facture"), "copie affichée, plus de plateforme à l'envoi : l'e-mail de facture");
        Assert.That(copieFausse.corps, Is.EqualTo(EmailService.CorpsFacture()));
        var factureFausse = EmailService.TexteEnvoi(f, true, false, "Objet facture affiché", "x");
        Assert.That(factureFausse.objet, Is.EqualTo("Ma copie"), "facture affichée, plateforme à l'envoi : la copie (l'autre sens, nouveau)");
        Assert.That(EmailService.TexteEnvoi(null, false, false, "a", "b").objet, Is.EqualTo(EmailService.ObjetFacture()));

        Assert.That(EmailService.Aide(true, true), Does.Contain("sans pièce jointe"));
        Assert.That(EmailService.Aide(true, false), Does.Contain("copie PDF"));
        Assert.That(EmailService.Aide(false, false), Does.Contain("jointe en PDF automatiquement"));
    }

    /// Un avoir ne se règle pas : la notification (texte d'usine, ou modèle des Réglages qui a gardé la phrase) perd « , à régler le {echeance} »
    /// pour un total négatif ; une facture la garde (FactureElectroniqueTests l'exige) ; un texte propre sans la phrase ne bouge pas.
    [Test]
    public void Lot13_D5_10_la_notification_d_un_avoir_ne_dit_plus_a_regler()
    {
        StringAssert.Contains(", à régler le {echeance}", EmailService.NotifCorpsDefaut, "le texte d'usine garde la phrase pour une facture");
        StringAssert.DoesNotContain(", à régler le {echeance}", EmailService.CorpsNotif(null, true), "avoir : la phrase tombe, quel que soit le modèle du poste");
        const string propre = "Bonjour, votre facture {numero} ({periode}, à régler le {echeance}) est transmise.";
        Assert.That(EmailService.CorpsNotif(propre, true), Is.EqualTo("Bonjour, votre facture {numero} ({periode}) est transmise."));
        Assert.That(EmailService.CorpsNotif(propre, false), Is.EqualTo(propre), "une facture garde la phrase");
        Assert.That(EmailService.CorpsNotif("Votre avoir {numero} est transmis.", true), Is.EqualTo("Votre avoir {numero} est transmis."));
        StringAssert.Contains("EmailService.CorpsNotif(info?.notifCorps, ctx.ttc < -0.004f)", Src("Facturation/FactureElectronique.cs"), "Notifier juge l'avoir au total du document");
    }

    /// Les trois formulaires : un SIRET (ou un taux) tapé relance la partie « plateforme » (RefreshPlateforme : ligne « Envoi », case retard, mode de
    /// l'e-mail), posée en dernier au chargement ; le mode, l'enregistrement et le texte qui part passent par la règle d'EmailService ; la ligne
    /// « Envoi » de la régul et de la refac annonce la notification après la bascule. Les tests texte à garder (LibelleEnvoi dans les trois,
    /// Professionnel(_loc, SiretSaisi()) dans le loyer) restent vrais. Avant : décidés une fois au chargement (SIRET tapé → ligne orange, e-mail
    /// de facture, case retard libre, et la facture partait quand même par la plateforme).
    [Test]
    public void Lot13_D5_10_les_trois_formulaires_suivent_le_SIRET_tape_et_partagent_la_regle()
    {
        foreach (var p in TroisFormulaires)
        {
            string s = Src(p).Replace("\r\n", "\n");
            StringAssert.Contains("_siret.onValueChanged.AddListener(_ => RefreshPlateforme());", s, p + " : le SIRET tapé est suivi");
            StringAssert.Contains("RefreshPlateforme(); });", s, p + " : le taux aussi");
            StringAssert.Contains("void RefreshPlateforme(bool chargement = false)", s, p);
            StringAssert.Contains("if (_loc == null || _retard == null || (_chargement && !chargement)) return;", s, p + " : inerte pendant LoadIntoUI");
            StringAssert.Contains("_chargement = false;\n        RefreshPlateforme(chargement: true);", s, p + " : posée en dernier au chargement");
            StringAssert.Contains("if (obligatoire) _retard.isOn = true;", s, p + " : case retard forcée (plateforme ou professionnel)");
            StringAssert.Contains("if (!chargement) EmailService.Garder(f, _modeCopie || _modeNotif, _modeNotif, _emailObjet.text, _emailCorps.text);", s, p + " : le texte retouché reste dans la paire de son mode");
            StringAssert.Contains("(_emailObjet.text, _emailCorps.text) = EmailService.Modeles(f, plateforme, notif);", s, p + " : les champs du mode");
            StringAssert.Contains("_aideEmail.text = EmailService.Aide(plateforme, notif);", s, p);
            StringAssert.Contains("EmailService.Garder(f, _modeCopie || _modeNotif, _modeNotif, _emailObjet.text, _emailCorps.text);\n        f.", s, p + " : SaveFromUI");
            StringAssert.Contains("!_modeNotif && _modeCopie == parPlateforme, _emailObjet.text, _emailCorps.text);", s, p + " : TexteEnvoi, le mode affiché est-il le vrai ?");
            StringAssert.Contains("FactureElectronique.LibelleEnvoi(_loc, TauxFacture(),", s, p + " : test à garder (Revue16cTests)");
            StringAssert.Contains(", FactureElectronique.NotificationSeule", s, p + " : la ligne « Envoi » annonce la notification après la bascule (coutures : le loyer passe ensuite le jour du départ)");
            StringAssert.DoesNotContain("EmailService.ObjetFacture(f?.emailObjet)", s, p + " : plus de choix du mode à la main");
            StringAssert.DoesNotContain("bool copieFausse", s, p);
        }
        string loyer = Src("Facturation/FactureLoyerPanel.cs");
        StringAssert.Contains("FactureElectronique.Professionnel(_loc, SiretSaisi())", loyer, "test à garder (Revue17Tests)");
        StringAssert.Contains("_acheteurCession = t != null;\n        RefreshPlateforme();", loyer.Replace("\r\n", "\n"), "RefreshTitulaire relance la plateforme une fois _acheteurCession posé (le SIRET d'un cessionnaire)");
        foreach (var p in new[] { "Facturation/FactureRegulPanel.cs", "Facturation/FactureRefacPanel.cs" })
            StringAssert.Contains("FactureElectronique.Professionnel(_loc, _siret.text)", Src(p), p + " : test à garder (Revue17Tests)");
        string reglages = Src("Facturation/ReglagePanel.cs");
        StringAssert.Contains("pour un loyer, une régularisation ou une refacturation qui part aussi par la plateforme agréée", reglages, "modèle « copie »");
        StringAssert.Contains("un loyer, une régularisation ou une refacturation qui part par la plateforme agréée n'est", reglages, "modèle « notification »");
    }

    /// D5-26 (D5-V1) : après la bascule, la régularisation et la refacturation ne joignent plus de PDF : « Transmettre la facture par la plateforme ? »
    /// (confirmation partagée, FactureElectronique.ConfirmerTransmission) avant tout contrôle d'adresse, Finaliser sans e-mail, puis Traiter → Notifier
    /// pour toute ligne transmise (une seule règle, pas de condition sur le type) : un seul e-mail par document. Pour une régul en avoir, cette
    /// confirmation suit « Remboursement au locataire ». Avant : l'e-mail de facture avec le PDF, puis la notification (deux e-mails).
    [Test]
    public void Lot13_D5_26_apres_la_bascule_regul_et_refac_transmettent_sans_PDF_et_la_notification_part_une_fois()
    {
        foreach (var p in new[] { "Facturation/FactureRegulPanel.cs", "Facturation/FactureRefacPanel.cs" })
        {
            string s = Src(p);
            int decision = s.IndexOf("bool parPlateforme = FactureElectronique.Concerne(_loc, d.tva, _siret.text);", StringComparison.Ordinal);
            int transmettre = s.IndexOf("if (parPlateforme && FactureElectronique.NotificationSeule)", StringComparison.Ordinal);
            int confirmer = s.IndexOf("FactureElectronique.ConfirmerTransmission(_emailEnvoi.text,", StringComparison.Ordinal);
            int adresse = s.IndexOf("string dest = (_emailEnvoi.text ?? \"\").Trim();", StringComparison.Ordinal);
            Assert.That(decision, Is.GreaterThan(0), p);
            Assert.That(transmettre, Is.GreaterThan(decision).And.LessThan(confirmer), p + " : la branche « Transmettre ? » lit la décision prise une fois");
            Assert.That(confirmer, Is.LessThan(adresse), p + " : avant le contrôle de l'adresse e-mail (par la plateforme seule, pas besoin d'adresse)");
            StringAssert.Contains("true, parPlateforme, \"  Transmise par la plateforme agréée.\"));", s, p + " : Finaliser enregistre « Envoyé » sans e-mail");
            Assert.That(s.Split(new[] { "EmailService.Envoyer(" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1), p + " : un seul envoi d'e-mail, celui du PDF (EnvoyerPuisFinaliser)");
        }
        string regul = Src("Facturation/FactureRegulPanel.cs");
        Assert.That(regul.IndexOf("\"Remboursement au locataire\",", StringComparison.Ordinal),
                    Is.GreaterThan(0).And.LessThan(regul.IndexOf("FactureElectronique.ConfirmerTransmission(", StringComparison.Ordinal)),
                    "avoir : « Remboursement au locataire » (SauvegarderEtEnvoyer), puis « Transmettre ? » (Emettre)");

        string src = Src("Facturation/FactureElectronique.cs");
        StringAssert.Contains("if (NotificationSeule) await Notifier(loc, r, bp.getBatiment());", src, "Traiter notifie toute ligne transmise, sans condition sur le type");
        StringAssert.DoesNotContain("r.type == \"Loyer\") await Notifier", src, "option B écartée : les trois types ont leur mode notification");
        StringAssert.Contains("public static void ConfirmerTransmission(string adresse, Action transmettre)", src);
        StringAssert.Contains("\"Transmettre la facture par la plateforme ?\"", src, "une seule confirmation, partagée par les trois formulaires");
        StringAssert.DoesNotContain("\"Transmettre la facture par la plateforme ?\"", Src("Facturation/FactureLoyerPanel.cs"), "le loyer passe par la même");
        StringAssert.Contains("FactureEnvoiAuto.Destinataire(loc, r.type, r)", src, "la notification va au titulaire de la ligne (lot 6)");
    }

    // ── Décisions du 10/11 (après la vague 2) : l'envoi au lancement attend Super PDP ; la mention TVA d'une correction ne vaut que pour elle ──

    /// Décision du 10/11 (« Attendre Super PDP ») : après la bascule, une facture qui a sa facture électronique ne part plus en PDF par e-mail
    /// quand Super PDP n'est pas actif (état pas lu, vérification en cours, SCI non reliée) : EnvoyerUne relit l'état s'il n'est pas lu, puis la
    /// laisse en attente d'envoi avec la raison (RefusLancement), avant tout e-mail. Avant : « copie » + PDF, puis plateforme et notification
    /// (deux e-mails) ; SCI déliée : e-mail de facture seul.
    [Test]
    public void Decision_10_11_apres_la_bascule_l_envoi_au_lancement_attend_Super_PDP()
    {
        var loc = LocLot12();
        var avecJson = LigneLot12("2027-10-01", json: JsonTransmis("2027/10001", "3000.00"));
        var sansJson = LigneLot12("2027-10-01");
        var perimee = LigneLot12("2027-10-01", json: JsonTransmis("2027/10002", "3000.00"), etat: "perimee");
        Assert.That(FactureElectronique.RefusLancement(loc, avecJson), Is.Null, "avant la bascule : rien ne change (copie + PDF)");
        FactureElectronique.EssaiApresBascule = true;
        try
        {
            string refus = FactureElectronique.RefusLancement(loc, avecJson);   // en test, l'état de Super PDP n'est jamais lu : pas actif
            Assert.That(refus, Does.Contain("Dupont / Loyer Octobre 2027"), "le bilan nomme la facture");
            Assert.That(refus, Does.Contain("doit partir par la plateforme, et elle ne le peut pas en ce moment"));
            Assert.That(refus, Does.Contain(FactureElectronique.RaisonInactive), "avec la raison");
            Assert.That(FactureElectronique.RaisonHorsPlateforme(loc, 20f, ""), Is.EqualTo(FactureElectronique.RaisonInactive), "la même raison que les formulaires");
            Assert.That(FactureElectronique.RefusLancement(loc, sansJson), Is.Null, "sans facture électronique : c'est ARefaire qui la retient (lot 12)");
            Assert.That(FactureElectronique.RefusLancement(loc, perimee), Is.Null, "périmée : idem");
            Assert.That(PartLot12(loc, avecJson, new DateTime(2027, 9, 16), out _), Is.True, "elle reste proposée à chaque lancement : c'est l'envoi qui la retient");
        }
        finally { FactureElectronique.EssaiApresBascule = false; }

        string auto = Src("Facturation/FactureEnvoiAuto.cs");
        int relu = auto.IndexOf("if (FactureElectronique.ARefaire(c.loc, c.rec, DateTime.Today))", StringComparison.Ordinal);
        int lecture = auto.IndexOf("if (FactureElectronique.RefusLancement(c.loc, c.rec) != null && !FactureElectronique.EtatLu)", StringComparison.Ordinal);
        int retenue = auto.IndexOf("if (FactureElectronique.RefusLancement(c.loc, c.rec) is string inactif) { issue.Erreur = inactif; tenu = false; yield break; }", StringComparison.Ordinal);
        int envoi = auto.IndexOf("envoi = EmailService.Envoyer(c.destinataire", StringComparison.Ordinal);
        Assert.That(lecture, Is.GreaterThan(relu), "l'état pas encore lu est relu d'abord");
        Assert.That(retenue, Is.GreaterThan(lecture).And.LessThan(envoi), "puis la facture est retenue, avant tout e-mail");
    }

    /// Décision du 10/11 (« Seulement cette correction ») : la mention de TVA choisie pendant la correction d'une facture partie est imprimée
    /// sur elle (BuildData lit le menu), mais n'est pas gardée pour les factures suivantes du type, comme le taux (lot 10).
    [Test]
    public void Decision_10_11_la_mention_TVA_d_une_correction_ne_vaut_que_pour_elle()
    {
        foreach (var p in new[] { "FactureLoyerPanel.cs", "FactureRegulPanel.cs", "FactureRefacPanel.cs" })
        {
            string s = Src("Facturation/" + p);
            StringAssert.Contains("if (Corrigee() == null) MentionTva.Enregistrer(_mentionTva, _texteTvaDebit, f);", s, p + " : SaveFromUI");
            Assert.That(s.Split(new[] { "MentionTva.Enregistrer(" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1), p + " : un seul enregistrement de la mention");
            StringAssert.Contains("MentionTva.Phrase(_mentionTva, _texteTvaDebit)", s, p + " : le document imprime la mention du menu, pas la mémoire du type");
        }
    }
}
