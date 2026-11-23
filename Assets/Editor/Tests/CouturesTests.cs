using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot C « coutures » (10/11/2026) : les défauts aux jointures de la facturation (réservations, verrou d'envoi, suivi).
/// Un test par défaut corrigé, règle pure d'abord, texte du code à défaut.
public class CouturesTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));

    /// Le corps d'une méthode, de `debut` à sa fermeture (motif valable en LF comme en CRLF).
    static string Corps(string src, string debut)
    {
        int i = src.IndexOf(debut, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), debut);
        return src.Substring(i, src.IndexOf("\n    }", i, StringComparison.Ordinal) - i);
    }

    static int Fois(string src, string fragment) => src.Split(new[] { fragment }, StringSplitOptions.None).Length - 1;

    static readonly string[] QuatreFormulaires = { "FactureLoyerPanel", "FactureRegulPanel", "FactureRefacPanel", "FactureDepotPanel" };

    // ── Lot A1 · réservations, verrou d'envoi et suivi ──

    /// D4-11 : le décompte de sortie passe « Payé » les créances reprises ; pour une refacturation, la charge du bâtiment change, et le
    /// bâtiment part au serveur AVANT le locataire : il doit être réservé à l'ouverture, comme pour la régularisation et la refacturation.
    [Test]
    public void D4_11_le_decompte_de_sortie_ecrit_le_batiment_donc_le_formulaire_le_reserve()
    {
        var creance = new FactureEtat { key = "refac-coutElec25", type = "Refac", libelle = "Électricité 2025", statut = "Impaye", numero = "COUT/11001",
                                        pdfPath = "x.pdf", echeanceISO = "2026-01-31", montant = 360f };
        var loc = new Locataire { id = "COUT-D411-LOC", Name = "Locataire D4-11", facturesEtat = new List<FactureEtat> { creance } };
        var charge = new ChargeBatiment { id = "coutElec25", nom = "Électricité 2025", cout = 300f, dateISO = "2025-12-31" };
        charge.MarquerFacturee(loc.id, "2026-01-02");
        var bat = new Batiment { id = "COUT-D411-BAT", Name = "Bâtiment D4-11", charges = new List<ChargeBatiment> { charge }, locataireDuBatiment = new List<Locataire> { loc } };
        var cache = new Dictionary<string, EcritureCloud.Etat>();
        EcritureCloud.Memoriser(cache, bat, 3, new Dictionary<string, int> { { loc.id, 7 } });
        Assert.That(EcritureCloud.Differences(bat, cache), Is.Empty, "rien à envoyer avant le décompte");

        FacturationSuivi.SetStatut(loc, creance, "Paye", bat);   // ce que fait FactureDepotPanel.Finaliser pour chaque créance cochée

        Assert.That(charge.FacturationDe(loc.id).paye, Is.True, "la charge refacturée est soldée : le bâtiment change");
        var ops = EcritureCloud.Differences(bat, cache);
        Assert.That(ops.Select(o => o.Type), Is.EqualTo(new[] { EcritureCloud.TypeOp.Batiment, EcritureCloud.TypeOp.Locataire }),
            "le bâtiment part d'abord : tenu par un collègue, il faisait refuser tout l'enregistrement après l'e-mail");
        StringAssert.Contains("EcritureCloud.Reserver(EcritureCloud.Cibles(fiche, true), \"dépôt\"", Src("Facturation/FactureDepotPanel.cs"),
            "OpenDepot réserve le bâtiment et le locataire");
    }

    /// D4-14 : les créances reprises par le décompte passent « Payé » : « Encaissée » part tout de suite (Rattraper), après l'enregistrement.
    [Test]
    public void D4_14_le_decompte_rattrape_la_plateforme_apres_l_enregistrement()
    {
        string finaliser = Corps(Src("Facturation/FactureDepotPanel.cs"), "void Finaliser(");
        int enregistre = finaliser.IndexOf("_fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();", StringComparison.Ordinal);
        int rattrape = finaliser.IndexOf("FactureElectronique.Rattraper(_fiche.batimentPrefabOrigin, _loc);", StringComparison.Ordinal);
        Assert.That(enregistre, Is.GreaterThan(0));
        Assert.That(rattrape, Is.GreaterThan(enregistre), "comme les trois autres formulaires");
        foreach (var p in QuatreFormulaires)
            StringAssert.Contains("FactureElectronique.Rattraper(_fiche.batimentPrefabOrigin, _loc);", Corps(Src("Facturation/" + p + ".cs"), "void Finaliser("), p);
    }

    /// D4-09 : la décision d'émission (FactureEmission.Preparer) est prise une fois par formulaire, son refus lu, puis passée à l'émission.
    [Test]
    public void D4_09_la_decision_d_emission_est_prise_une_seule_fois()
    {
        foreach (var p in QuatreFormulaires)
            Assert.That(Fois(Src("Facturation/" + p + ".cs"), "FactureEmission.Preparer("), Is.EqualTo(1), p + " : un seul Preparer");
        string regul = Src("Facturation/FactureRegulPanel.cs"), depot = Src("Facturation/FactureDepotPanel.cs");
        StringAssert.Contains("void Emettre(FacturePdfService.RegulData d, string key, FactureEmission.Decision emission)", regul);
        StringAssert.Contains("void Emettre(Document d, string key, FactureEmission.Decision emission)", depot);
        foreach (var (nom, src) in new[] { ("régularisation", regul), ("dépôt", depot) })
        {
            string envoyer = Corps(src, "void SauvegarderEtEnvoyer()");
            StringAssert.Contains("if (emission.Refus != null) { ConfirmDialog.Erreur(emission.Refus); return; }", envoyer, nom + " : le refus est lu");
            Assert.That(Fois(envoyer, "Emettre(d, key, emission)"), Is.EqualTo(2), nom + " : la même décision, avec ou sans confirmation du remboursement");
        }
        Assert.That(Fois(regul, "Regroupement(_loc"), Is.EqualTo(2), "régularisation : la ligne de la facture décidée une fois (plus CleFacture, pour l'affichage)");
    }

    /// D4-07 : l'envoi d'un formulaire compte comme un envoi en cours (rechargement, fermeture de CIPL et relecture en direct attendent),
    /// et une fermeture venue du cloud attend la fin de l'envoi au lieu de l'arrêter entre l'e-mail et l'enregistrement.
    [Test]
    public void D4_07_l_envoi_d_un_formulaire_est_compte_et_la_fermeture_forcee_attend_sa_fin()
    {
        Assert.That(FactureEmission.EnvoisEnCours, Is.EqualTo(0), "aucun envoi au départ");
        Assert.That(EcritureCloud.EnvoiEnCours, Is.False);
        Assert.That(EcritureCloud.Occupe("COUT-D407-BAT"), Is.False);
        bool drapeau = false;
        try
        {
            FactureEmission.NoterEnvoi(ref drapeau, true);
            FactureEmission.NoterEnvoi(ref drapeau, true);   // noté deux fois : compté une fois
            Assert.That(drapeau, Is.True);
            Assert.That(FactureEmission.EnvoisEnCours, Is.EqualTo(1));
            Assert.That(EcritureCloud.EnvoiEnCours, Is.True, "DemarrageCloud.Recharger attend");
            Assert.That(FermetureGuard.EnvoiEnCours, Is.True, "la fermeture de CIPL attend, et prévient");
            Assert.That(EcritureCloud.Occupe("COUT-D407-BAT"), Is.True, "la relecture en direct attend aussi");
        }
        finally { FactureEmission.NoterEnvoi(ref drapeau, false); }
        FactureEmission.NoterEnvoi(ref drapeau, false);   // la fin rappelée par OnDisable : pas décomptée deux fois
        Assert.That(drapeau, Is.False);
        Assert.That(FactureEmission.EnvoisEnCours, Is.EqualTo(0));
        Assert.That(EcritureCloud.EnvoiEnCours, Is.False);

        foreach (var p in QuatreFormulaires)
        {
            string s = Src("Facturation/" + p + ".cs");
            StringAssert.Contains("() => { if (p != null) p.FermerDeForce(); });", s, p + " : la réservation ne ferme plus de force");
            StringAssert.DoesNotContain("{ if (p != null) p.Close(); }", s, p);
            StringAssert.Contains("void FermerDeForce() { if (FactureEmission.RefuseFermeture(_envoiEnCours)) _fermerApresEnvoi = true; else Close(); }", s,
                p + " : en plein envoi, la fermeture est remise à la fin (même message que « Retour »)");
            StringAssert.DoesNotContain("_envoiEnCours = true;", s, p + " : le drapeau ne bouge qu'avec le compteur");
            StringAssert.DoesNotContain("_envoiEnCours = false;", s, p + " : le drapeau ne bouge qu'avec le compteur");
            string envoi = Corps(s, "System.Collections.IEnumerator EnvoyerPuisFinaliser(");
            StringAssert.Contains("FactureEmission.NoterEnvoi(ref _envoiEnCours, true);", envoi, p);
            StringAssert.Contains("FactureEmission.NoterEnvoi(ref _envoiEnCours, false);", envoi, p);
            Assert.That(Fois(envoi, "if (_fermerApresEnvoi) Close();"), Is.EqualTo(2), p + " : après un échec, et après l'enregistrement");
            Assert.That(envoi.LastIndexOf("if (_fermerApresEnvoi) Close();", StringComparison.Ordinal),
                        Is.GreaterThan(envoi.LastIndexOf("Finaliser(", StringComparison.Ordinal)), p + " : la facture est enregistrée avant la fermeture");
        }
    }

    /// D4-07 (reprise) : la fiche vidée pendant l'envoi (changement d'entreprise, exclusion, rechargement après l'attente) : l'e-mail est
    /// parti, rien n'est finalisé sur la fiche morte. Avant : enregistrement refusé au centre, puis le toast « enregistrée et envoyée ».
    [Test]
    public void D4_07_une_fiche_videe_pendant_l_envoi_n_est_pas_finalisee()
    {
        var bat = new Batiment { id = "COUT-D407-PERDUE", Name = "Bâtiment D4-07" };
        Assert.That(FactureEmission.FichePerdue(bat, "locataire@exemple.invalid"), Is.False, "fiche vivante : la facture s'enregistre");
        Assert.That(FactureEmission.FichePerdue(null, "locataire@exemple.invalid"), Is.False, "sans bâtiment : comme avant");
        EcritureCloud.Perimer(bat);   // ce que fait BatimentManager.Vider
        try { Assert.That(FactureEmission.FichePerdue(bat, "locataire@exemple.invalid"), Is.True, "fiche vidée : on s'arrête avant Finaliser"); }
        finally
        {
            ((System.Runtime.CompilerServices.ConditionalWeakTable<Batiment, object>)typeof(EcritureCloud)
                .GetField("_perimes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null)).Remove(bat);   // Revue 17 (D9-09)
        }
        StringAssert.Contains("mais sa fiche a été fermée pendant l'envoi : elle n'est pas", Src("Facturation/FactureEmission.cs"), "le message dit que l'e-mail est parti");
        foreach (var p in QuatreFormulaires)
        {
            string envoi = Corps(Src("Facturation/" + p + ".cs"), "System.Collections.IEnumerator EnvoyerPuisFinaliser(");
            int perdue = envoi.IndexOf("if (FactureEmission.FichePerdue(_bat, dest)) { Close(); yield break; }", StringComparison.Ordinal);
            Assert.That(perdue, Is.GreaterThan(envoi.IndexOf("if (!envoi.Succes)", StringComparison.Ordinal)), p + " : après le cas de l'échec (l'e-mail est vraiment parti)");
            Assert.That(perdue, Is.LessThan(envoi.LastIndexOf("Finaliser(", StringComparison.Ordinal)), p + " : avant Finaliser");
        }
    }

    /// D4-05 (majeur, décision du 09/11) : l'état d'une ligne se lit avec son locataire : ses délais propres valent pour le bouton « Rappel »
    /// et la pastille d'année, comme pour la pastille d'état. Le paramètre est obligatoire : un appel sans locataire ne compile plus.
    [Test]
    public void D4_05_les_delais_propres_du_locataire_decident_de_l_etat_partout()
    {
        string echeance = DateTime.Today.AddDays(-20).ToString("yyyy-MM-dd");
        FactureEtat Loyer() => new FactureEtat { key = "loyer-2026-P10", type = "Loyer", libelle = "Loyer Octobre 2026", statut = "Envoye", numero = "COUT/10001",
                                                 pdfPath = "x.pdf", echeanceISO = echeance, montant = 1200f };
        Locataire Bail(string id, int impayeApres) => new Locataire { id = id, Name = id, delaisPropres = true, envoiAvantJours = 15, impayeApresJours = impayeApres,
                                                                     echeanceAutresJours = 30, facturesEtat = new List<FactureEtat> { Loyer() } };
        var grandCompte = Bail("COUT-D405-45J", 45);
        var autre = Bail("COUT-D405-15J", 15);
        Assert.That(FacturationSuivi.EtatDe(grandCompte.facturesEtat[0], grandCompte), Is.EqualTo(FacturationSuivi.Etat.Envoye), "45 jours : pas de « Rappel », pas d'année en rouge");
        Assert.That(FacturationSuivi.EtatAffiche(grandCompte, grandCompte.facturesEtat[0]), Is.EqualTo(FacturationSuivi.Etat.Envoye), "la pastille dit pareil");
        Assert.That(FacturationSuivi.EtatDe(autre.facturesEtat[0], autre), Is.EqualTo(FacturationSuivi.Etat.Impaye), "15 jours : impayée");

        var parametres = typeof(FacturationSuivi).GetMethod("EtatDe").GetParameters();
        Assert.That(parametres.Length, Is.EqualTo(2));
        Assert.That(parametres[1].IsOptional, Is.False, "le locataire est obligatoire : l'oublier ne compile plus");

        string suivi = Src("Facturation/LocataireSuiviInline.cs");
        Assert.That(Fois(suivi, "FacturationSuivi.EtatDe(l, _loc)"), Is.EqualTo(2), "pastille d'année et boutons de la ligne");
        StringAssert.Contains("FacturationSuivi.EtatDe(ligne, _loc)", suivi, "menu « Paiement partiel… »");
        StringAssert.Contains("var e = FacturationSuivi.EtatDe(l, loc);", Corps(Src("Locataire/DepartLocataire.cs"), "public static List<string> RaisonsNonArchivable("), "archivage");
        StringAssert.Contains("FacturationSuivi.EtatDe(_ligneCiblee, _loc)", Src("Facturation/FactureLoyerPanel.cs"), "provisions d'une correction");
        StringAssert.Contains("var etat = FacturationSuivi.EtatDe(r, loc);", Src("Facturation/FactureElectronique.cs"), "« Encaissée » (Traiter)");
    }

    /// D5-16 : une facture émise, pas encore essayée, est « À transmettre » ; « À renvoyer » seulement après un essai en panne.
    [Test]
    public void D5_16_a_transmettre_avant_l_essai_a_renvoyer_apres_une_panne()
    {
        var r = new FactureEtat { key = "loyer-2026-P11", type = "Loyer", numero = "COUT/11016", statut = "Envoye", pdfPath = "x.pdf", plateformeDonnees = "{}",
                                  echeanceISO = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd") };
        Assert.That(FactureElectronique.Pastille(r), Is.EqualTo("À transmettre"), "juste après l'envoi : pas un échec");
        r.plateformeEtat = "a_renvoyer";
        Assert.That(FactureElectronique.Pastille(r), Is.EqualTo("À renvoyer"), "Super PDP injoignable au dernier essai");
        r.plateformeId = "42"; r.plateformeEtat = "envoyee";
        Assert.That(FactureElectronique.Pastille(r), Is.EqualTo("Plateforme"));
        StringAssert.Contains("r.plateformeEtat = \"a_renvoyer\"", Src("Facturation/FactureElectronique.cs"), "posé par Traiter sur une panne");
    }

    /// D5-19 : la ligne « Envoi » ne peut plus dire « locataire non assujetti à la TVA » (branche jamais atteinte, texte faux).
    [Test]
    public void D5_19_la_ligne_envoi_ne_parle_plus_de_locataire_non_assujetti()
    {
        string src = Src("Facturation/FactureElectronique.cs");
        int debut = src.IndexOf("public static string LibelleEnvoi(", StringComparison.Ordinal);
        int fin = src.IndexOf("public static void ConfirmerTransmission(", StringComparison.Ordinal);
        Assert.That(debut > 0 && fin > debut, Is.True, "LibelleEnvoi introuvable");
        string libelle = src.Substring(debut, fin - debut);
        StringAssert.DoesNotContain("non assujetti", libelle);
        StringAssert.DoesNotContain(": Active ?", libelle, "SCI active et TVA sur le document : Concerne ou SansSiret ont déjà répondu");
        StringAssert.Contains(": SansSiret(loc, tva, siretFormulaire) ?", libelle, "les autres lignes restent");
    }

    /// D6-22 : lot 0 = « non renseigné » : la page 2 de la régularisation n'imprime que l'adresse.
    [Test]
    public void D6_22_le_detail_de_la_regularisation_n_imprime_pas_un_lot_zero()
    {
        Assert.That(FactureRegulPanel.TitreDetail("12 rue de Rivoli", 0), Is.EqualTo("12 rue de Rivoli"));
        Assert.That(FactureRegulPanel.TitreDetail("12 rue de Rivoli", 3), Is.EqualTo("12 rue de Rivoli, LOT N°3"));
        StringAssert.Contains("TitreDetail(adr, _loc.lotBatiment)", Src("Facturation/FactureRegulPanel.cs"), "BuildData passe par la règle");
    }

    /// D6-24 : l'aide « Dans cette phrase, {montant}… » est cachée avec sa phrase, d'après la PÉRIODE affichée (un mois : cachée).
    [Test]
    public void D6_24_l_aide_du_montant_mensuel_suit_sa_phrase()
    {
        string loyer = Src("Facturation/FactureLoyerPanel.cs");
        StringAssert.Contains("_aideMensuel = UIFactory.Text(mo.transform,", loyer, "l'aide est gardée");
        string refresh = Corps(loyer, "void RefreshMensuel()");
        StringAssert.Contains("bool plusieursMois = MoisFactures(_loc, PeriodeAnnee(), PeriodeChoisie()) > 1;", refresh, "la période affichée, pas la périodicité de la fiche");
        StringAssert.Contains("_texteMensuel.gameObject.SetActive(plusieursMois);", refresh);
        StringAssert.Contains("_aideMensuel.gameObject.SetActive(plusieursMois);", refresh, "même règle que la phrase");
    }

    // ── Lot A2 · corrections, émission et lignes du suivi ──

    /// Un locataire des tests A2 : ses délais propres (pas de lecture des Réglages), bail soumis à la TVA, un SIRET.
    static Locataire LocA2(string id) => new Locataire { id = id, Name = id, delaisPropres = true, envoiAvantJours = 15, impayeApresJours = 15, echeanceAutresJours = 30,
                                                         tvaLoyer = "oui", siretNumber = "55203253400646", factureRegul = new FactureInfo() };

    /// D4-06 (majeur, décision du 09/11 ; D4-N6, D4-N13) : une facture portée par plusieurs lignes (régularisation regroupée, refacturation de
    /// plusieurs charges) : à la correction, les autres lignes reçoivent ce que reçoit la ligne porteuse (échéance, RIB), et une ligne ajoutée
    /// porte le numéro de base. Avant : l'ancienne échéance sur les autres lignes (deux états pour une facture) et « N corrigée(1) » en numéro.
    [Test]
    public void D4_06_les_lignes_secondaires_d_une_correction_recoivent_ce_que_recoit_la_ligne_porteuse()
    {
        var loc = LocA2("COUT-D406");
        const string generale = "regul-2025", fonciere = "regul-2025-coutTF", eau = "regul-2025-coutEau";
        const string pdf1 = "Batiment/Coutures/D406/Facture/regul.pdf", pdf2 = "Batiment/Coutures/D406/Facture/regul-corrigee1.pdf";
        var emission = FactureEmission.Preparer(loc, generale, "COUT/01005");
        Assert.That(emission.NumeroBase, Is.EqualTo("COUT/01005"), "facture neuve : le numéro proposé");
        FactureEmission.Enregistrer(loc, generale, "Regul", emission, "Régularisation des charges 2025", "2026-01-31", pdf1, 300f, "rib-a", loc.factureRegul, "Régularisation", true);
        FactureEmission.EnregistrerLiee(loc, fonciere, "Regul", emission, "Régularisation Taxe foncière 2025", "2026-01-31", pdf1, 100f, "rib-a", true);
        int sequence = loc.factureSeq;
        Assert.That(loc.facturesEtat.Select(l => l.numero), Is.All.EqualTo("COUT/01005"));

        // La correction : échéance au 15/03, un autre RIB, une liste ajoutée.
        var correction = FactureEmission.Preparer(loc, generale, "COUT/02009");
        Assert.That(correction.Correction, Is.True);
        Assert.That(correction.NumeroFacture, Is.EqualTo("COUT/01005 corrigée(1)"), "le numéro imprimé");
        Assert.That(correction.NumeroBase, Is.EqualTo("COUT/01005"), "le numéro des lignes du suivi");
        FactureEmission.Enregistrer(loc, generale, "Regul", correction, "Régularisation des charges 2025", "2026-03-15", pdf2, 320f, "rib-b", loc.factureRegul, "Régularisation", true);
        FactureEmission.EnregistrerLiee(loc, fonciere, "Regul", correction, "Régularisation Taxe foncière 2025", "2026-03-15", pdf2, 110f, "rib-b", true);
        FactureEmission.EnregistrerLiee(loc, eau, "Regul", correction, "Régularisation Eau 2025", "2026-03-15", pdf2, 50f, "rib-b", true);

        var lignes = FacturationSuivi.MemeFacture(loc, generale);
        Assert.That(lignes.Select(l => l.key), Is.EquivalentTo(new[] { generale, fonciere, eau }), "une facture, trois lignes");
        Assert.That(lignes.Select(l => l.echeanceISO), Is.All.EqualTo("2026-03-15"), "la nouvelle échéance sur toutes les lignes (avant : la porteuse seule)");
        Assert.That(lignes.Select(l => l.ribId), Is.All.EqualTo("rib-b"), "le nouveau RIB aussi");
        Assert.That(lignes.Select(l => l.numero), Is.All.EqualTo("COUT/01005"), "la liste ajoutée porte le numéro de base (avant : « COUT/01005 corrigée(1) »)");
        Assert.That(lignes.Select(l => FacturationSuivi.EtatDe(l, loc)).Distinct().Count(), Is.EqualTo(1), "un seul état pour la facture");
        var seconde = loc.facturesEtat.Find(l => l.key == fonciere);
        Assert.That(seconde.montant, Is.EqualTo(110f));
        Assert.That(seconde.libelle, Does.EndWith("— corrigée(1)"));
        Assert.That(loc.factureSeq, Is.EqualTo(sequence), "une correction ne consomme pas de numéro, une ligne ajoutée non plus");
        FacturationSuivi.FacturesOrigine(loc, out _, out var numeros);
        Assert.That(numeros, Is.EqualTo(new[] { "", "COUT/01005" }), "« Avoir sur la facture n° » : une seule facture proposée");

        foreach (var (p, type) in new[] { ("FactureRegulPanel", "Regul"), ("FactureRefacPanel", "Refac") })
        {
            string finaliser = Corps(Src("Facturation/" + p + ".cs"), "void Finaliser(");
            StringAssert.Contains("FactureEmission.EnregistrerLiee(_loc, ", finaliser, p + " : la règle partagée");
            StringAssert.Contains("\"" + type + "\", emission, ", finaliser, p + " : avec la décision de l'émission");
            StringAssert.DoesNotContain("FacturationSuivi.MarquerCorrige(", finaliser, p + " : plus de copie de la règle, sans l'échéance ni le RIB");
            StringAssert.DoesNotContain("FacturationSuivi.MarquerEnvoye(", finaliser, p);
        }
    }

    /// D4-10 (D4-N11, D6-N10) : « Corriger » et le titre « (correction) » suivent la règle de l'émission (FactureEmission.SeraUneCorrection) pour
    /// tous les types. Avant : « Corriger » pour un loyer envoyé ou impayé seulement. Un « Payé » posé à la main, sans PDF, n'est pas une
    /// facture à corriger (décision du 10/11, Tva.FacturePartie).
    [Test]
    public void D4_10_corriger_est_propose_pour_toute_facture_partie_et_le_titre_l_annonce()
    {
        string echeance = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd");
        FactureEtat L(string key, string type, string statut, bool pdf = true)
            => new FactureEtat { key = key, type = type, libelle = key, statut = statut, numero = "COUT/" + key, pdfPath = pdf ? key + ".pdf" : null, echeanceISO = echeance, montant = 500f };
        var loc = LocA2("COUT-D410");
        loc.facturesEtat = new List<FactureEtat>
        {
            L("loyer-2026-P10", "Loyer", "Paye"), L("regul-2025", "Regul", "Envoye"), L("refac-coutD410", "Refac", "Impaye"), L("depot-2026", "Depot", "Envoye"),
            L("loyer-2026-P11", "Loyer", "AttenteEnvoi"), L("loyer-2026-P9", "Loyer", "Paye", pdf: false),
        };
        foreach (var cle in new[] { "loyer-2026-P10", "regul-2025", "refac-coutD410", "depot-2026" })
            Assert.That(FactureEmission.SeraUneCorrection(loc, cle), Is.True, cle + " : partie, « Corriger »");
        Assert.That(FactureEmission.SeraUneCorrection(loc, "loyer-2026-P11"), Is.False, "en attente d'envoi : « Refaire » (même numéro, même fichier)");
        Assert.That(FactureEmission.SeraUneCorrection(loc, "loyer-2026-P9"), Is.False, "« Payé » posé à la main, sans PDF : pas une facture à corriger");
        Assert.That(Tva.FacturePartie(loc.facturesEtat[5]), Is.False, "la même réponse que les formulaires (décision du 10/11)");
        Assert.That(FactureEmission.SeraUneCorrection(loc, "loyer-2026-P12"), Is.False, "jamais émise");

        string suivi = Src("Facturation/LocataireSuiviInline.cs");
        StringAssert.Contains("bool corrigeable = FactureEmission.SeraUneCorrection(_loc, l.key);", suivi, "le bouton du suivi suit la règle de l'émission, pour tous les types");
        StringAssert.DoesNotContain("corrigeable = l.type == \"Loyer\"", suivi);
        foreach (var (p, cle) in new[] { ("FactureRegulPanel", "CleFacture()"), ("FactureRefacPanel", "CleFacture()"), ("FactureDepotPanel", "CleSuivi()") })
            StringAssert.Contains("FactureEmission.SeraUneCorrection(_loc, " + cle + ") ? \" (correction)\" : \"\"", Corps(Src("Facturation/" + p + ".cs"), "void RefreshNumero()"),
                p + " : le titre annonce la correction, et suit la facture visée");
    }

    /// D4-08 : le dépôt garde le taux de TVA de son document sur sa ligne (0 pour un dépôt ou une révision ; le taux du bail pour un décompte
    /// de sortie aux retenues soumises), et la TVA du décompte se relit pour le rappel. Avant : 20 % d'office sur la ligne, et 0 € de TVA.
    [Test]
    public void D4_08_le_decompte_garde_son_taux_et_sa_TVA_se_relit_sur_sa_ligne()
    {
        var loc = LocA2("COUT-D408");
        loc.factureDepot = new FactureInfo { retenuesSortie = new List<RetenueSortie> {
            new RetenueSortie { libelle = "Remise en état", ht = 500f, tva = true }, new RetenueSortie { libelle = "Clés", ht = 50f, tva = false } } };
        // Dépôt rendu 3 000 €, retenues 550 € HT, TVA 100 € : −2 350 € (un avoir).
        var decompte = new FactureEtat { key = DepartLocataire.CleDecompte, type = "Depot", libelle = "Décompte de sortie", statut = "Envoye", numero = "COUT/11008",
                                         pdfPath = "d.pdf", montant = -2350f, tauxTva = 20f };
        var revision = new FactureEtat { key = "depot-2026", type = "Depot", libelle = "Révision du dépôt de garantie", statut = "Envoye", montant = 200f };   // ligne d'avant : 20 % d'office
        loc.facturesEtat = new List<FactureEtat> { decompte, revision };

        var ctx = FactureEnvoiAuto.Contexte(loc, decompte);
        Assert.That(ctx.tva, Is.EqualTo(100f).Within(0.001f), "la TVA des retenues soumises (avant : 0 € dans le rappel)");
        Assert.That(ctx.loyerHT, Is.EqualTo(-2450f).Within(0.001f));
        Assert.That(ctx.ttc, Is.EqualTo(-2350f).Within(0.001f));
        decompte.tauxTva = 0f;   // aucune retenue soumise à l'émission : le formulaire garde 0
        Assert.That(FactureEnvoiAuto.Contexte(loc, decompte).tva, Is.EqualTo(0f).Within(0.001f));
        Assert.That(FactureEnvoiAuto.Contexte(loc, revision).tva, Is.EqualTo(0f).Within(0.001f), "un dépôt ou une révision n'a pas de TVA, quel que soit le taux gardé");

        string depot = Src("Facturation/FactureDepotPanel.cs");
        StringAssert.Contains("if (ligne != null) ligne.tauxTva = d.Taux;", Corps(depot, "void Finaliser("), "le taux du document sur sa ligne (avant : 20 % d'office)");
        StringAssert.Contains("Taux = d.masquerTva ? 0f : Tva.Taux(_loc),", depot, "0 sans TVA ; le taux du bail pour un décompte aux retenues soumises");
        StringAssert.Contains("float tva = _sortie ? DecompteSaisi().tva : 0f;", depot, "l'e-mail qui part du formulaire dit la même TVA");
    }

    /// D6-19 (majeur ; D6-N3, D4-N9 ; décision du 09/11) : un total nul est refusé pour le loyer AVANT toute branche (le PDF, « premiers pas » et le
    /// bâtiment d'exemple, l'envoi différé), pour la régularisation et pour la refacturation ; jamais pour le dépôt. Une seule règle.
    [Test]
    public void D6_19_un_total_nul_est_refuse_avant_tout_sauf_pour_le_depot()
    {
        Assert.That(FactureEmission.RefusTotalNul(0f, "Loyer Janvier 2027"), Is.EqualTo("Le total de « Loyer Janvier 2027 » est nul : rien à facturer. Corrigez les montants."));
        Assert.That(FactureEmission.RefusTotalNul(0.003f, "Loyer"), Is.Not.Null, "moins d'un demi-centime : nul");
        Assert.That(FactureEmission.RefusTotalNul(-0.003f, "Loyer"), Is.Not.Null);
        Assert.That(FactureEmission.RefusTotalNul(0.01f, "Loyer"), Is.Null, "un centime : une facture");
        Assert.That(FactureEmission.RefusTotalNul(-5f, "Régularisation"), Is.Null, "un avoir n'est pas un total nul");

        const string refus = "if (FactureEmission.RefusTotalNul(d.ttc, d.subtitle) is string refusNul) { ConfirmDialog.Erreur(refusNul); return; }";
        string loyer = Corps(Src("Facturation/FactureLoyerPanel.cs"), "void SauvegarderEtEnvoyer()");
        int nul = loyer.IndexOf(refus, StringComparison.Ordinal);
        Assert.That(nul, Is.GreaterThan(0), "loyer");
        foreach (var apres in new[] { "FacturePdfService.GeneratePdfAsync(d, pdf)", "if (sansEnvoi)", "PreparerPourEnvoi(d, key,", "FactureElectronique.ConfirmerTransmission(" })
            Assert.That(loyer.IndexOf(apres, StringComparison.Ordinal), Is.GreaterThan(nul), "loyer : le refus passe avant « " + apres + " »");
        string regul = Corps(Src("Facturation/FactureRegulPanel.cs"), "void SauvegarderEtEnvoyer()");
        Assert.That(regul.IndexOf(refus, StringComparison.Ordinal), Is.GreaterThan(0).And.LessThan(regul.IndexOf("Emettre(d, key, emission)", StringComparison.Ordinal)),
            "régularisation : avant la confirmation du remboursement et le PDF");
        string refac = Corps(Src("Facturation/FactureRefacPanel.cs"), "void SauvegarderEtEnvoyer()");
        Assert.That(refac.IndexOf(refus, StringComparison.Ordinal), Is.GreaterThan(0).And.LessThan(refac.IndexOf("FacturePdfService.GeneratePdfAsync(d, pdf)", StringComparison.Ordinal)),
            "refacturation : avant le PDF");
        StringAssert.DoesNotContain("RefusTotalNul(", Src("Facturation/FactureDepotPanel.cs"), "jamais pour le dépôt : un dépôt « à l'équilibre » est voulu");
    }

    /// Reste du lot 11 : les refus de l'envoi (correction d'une facture transmise, obligation de la plateforme, avoir sans facture à citer) passent
    /// avant l'écriture du PDF dans les trois formulaires : un refus ne laisse plus sur le disque un fichier « corrigée(X) » jamais émis.
    [Test]
    public void Reste_du_lot_11_les_refus_passent_avant_la_generation_du_PDF()
    {
        foreach (var (p, methode, pdf) in new[] { ("FactureLoyerPanel", "void SauvegarderEtEnvoyer()", "FacturePdfService.GeneratePdfAsync(d, pdf)"),
                                                  ("FactureRegulPanel", "void Emettre(", "FacturePdfService.GenerateRegulPdfAsync(d, pdf)"),
                                                  ("FactureRefacPanel", "void SauvegarderEtEnvoyer()", "FacturePdfService.GeneratePdfAsync(d, pdf)") })
        {
            string corps = Corps(Src("Facturation/" + p + ".cs"), methode);
            int ecrit = corps.IndexOf(pdf, StringComparison.Ordinal);
            Assert.That(ecrit, Is.GreaterThan(0), p);
            foreach (var refus in new[] { "FactureElectronique.RefusCorrection(", "FactureElectronique.RefusObligation(", "FactureElectronique.RefusAvoir(" })
                Assert.That(corps.IndexOf(refus, StringComparison.Ordinal), Is.GreaterThan(0).And.LessThan(ecrit), p + " : « " + refus + " » avant le PDF");
        }
    }

    /// D6-15 (D6-N18) : sans RIB réglé, la facture est refusée au centre avant le PDF, dans les quatre formulaires, « premiers pas » compris ; le
    /// message dit où ajouter le RIB. Avant : elle partait avec un relevé d'identité bancaire vide.
    [Test]
    public void D6_15_sans_RIB_la_facture_est_refusee_avant_le_PDF()
    {
        Assert.That(FactureEmission.RefusSansRib(null),
            Does.Contain("Aucun RIB").And.Contain("Réglages → Entreprise").And.Contain("« + Ajouter un RIB »").And.Contain("Rien n'a été émis"));
        Assert.That(FactureEmission.RefusSansRib(new RibData { name = "Compte loyers" }), Is.Null, "un RIB choisi : rien à refuser");

        const string garde = "FactureEmission.RefusSansRib(ReglageService.GetRib(_ribDD?.SelectedId)) is string refusRib) { ConfirmDialog.Erreur(refusRib); return; }";
        foreach (var p in QuatreFormulaires)
            StringAssert.Contains(garde, Corps(Src("Facturation/" + p + ".cs"), "void SauvegarderEtEnvoyer()"), p + " : refusé dès le clic, avant le PDF (écrit ici, ou dans Emettre)");
        string loyer = Corps(Src("Facturation/FactureLoyerPanel.cs"), "void SauvegarderEtEnvoyer()");
        int rib = loyer.IndexOf(garde, StringComparison.Ordinal);
        Assert.That(rib, Is.LessThan(loyer.IndexOf("FacturePdfService.GeneratePdfAsync(d, pdf)", StringComparison.Ordinal)), "loyer : avant le PDF");
        Assert.That(rib, Is.LessThan(loyer.IndexOf("if (sansEnvoi)", StringComparison.Ordinal)), "loyer : avant la branche « premiers pas » (la facture émise sans envoi part plus tard)");
        StringAssert.Contains("if (!exemple && " + garde, loyer, "pas sur le bâtiment d'exemple : rien n'en part");
        string refac = Corps(Src("Facturation/FactureRefacPanel.cs"), "void SauvegarderEtEnvoyer()");
        Assert.That(refac.IndexOf(garde, StringComparison.Ordinal), Is.LessThan(refac.IndexOf("FacturePdfService.GeneratePdfAsync(d, pdf)", StringComparison.Ordinal)), "refacturation : avant le PDF");
    }

    /// Reste du lot 11 : la ligne « Envoi » juge le jour où la facture partira, comme la garde de l'émission (RefusObligation). Avant : le jour
    /// même ; en août 2027, un loyer à envoyer en septembre annonçait encore l'e-mail et l'émission le refusait.
    [Test]
    public void Reste_du_lot_11_la_ligne_Envoi_juge_le_jour_du_depart_comme_la_garde()
    {
        var loc = LocA2("COUT-ENVOI");   // TVA et SIRET ; en EditMode, Super PDP n'est pas actif : la facture ne part pas par la plateforme
        DateTime avant = FactureElectronique.Bascule.AddDays(-1), apres = FactureElectronique.Bascule.AddDays(15);
        try
        {
            FactureElectronique.EssaiApresBascule = false;
            Assert.That(FactureElectronique.RefusObligation(loc, 20f, "", "Loyer Octobre 2027", false, apres), Is.Not.Null, "la garde refuse un loyer qui partira après la bascule");
            Assert.That(FactureElectronique.LibelleEnvoi(loc, 20f, "", false, apres), Does.StartWith("Envoi : impossible pour le moment"),
                "la ligne l'annonce dès la préparation (avant : « e-mail seulement » jusqu'au 1er septembre)");
            Assert.That(FactureElectronique.RefusObligation(loc, 20f, "", "Loyer Août 2027", false, avant), Is.Null);
            Assert.That(FactureElectronique.LibelleEnvoi(loc, 20f, "", false, avant), Does.StartWith("Envoi : e-mail seulement ("), "départ avant la bascule : e-mail, avec la raison");
        }
        finally { FactureElectronique.EssaiApresBascule = false; }

        StringAssert.Contains("(notification || depart >= Bascule", Src("Facturation/FactureElectronique.cs"), "par la plateforme, un départ après la bascule annonce la notification seule");
        string loyer = Src("Facturation/FactureLoyerPanel.cs");
        StringAssert.Contains("FactureElectronique.NotificationSeule, DepartSaisi());", Corps(loyer, "void RefreshModeEnvoi()"), "le loyer passe le jour où il partira");
        StringAssert.Contains("return envoi > DateTime.Today && !FactureEmission.SeraUneCorrection(_loc, CleLoyer(), PeriodiciteChoisie().ToString()) ? envoi.Value : DateTime.Today;",
            Corps(loyer, "DateTime DepartSaisi()"), "le jour de la garde : la date d'envoi à venir, sauf pour une correction");
        StringAssert.Contains("RefreshEntetePreview(); RefreshModeEnvoi(); });", loyer, "la ligne suit l'échéance tapée");
    }

    /// Reste du lot 12 : une facture en attente d'envoi retenue au lancement (préparée sans facture électronique alors qu'elle doit partir par la
    /// plateforme le jour de son départ) est dite sur sa ligne du suivi, comme celle préparée au nom d'un autre titulaire.
    [Test]
    public void Reste_du_lot_12_la_facture_retenue_au_lancement_est_dite_sur_sa_ligne()
    {
        var loc = LocA2("COUT-RETENUE");
        var ligne = new FactureEtat { key = "loyer-2027-P10", type = "Loyer", libelle = "Loyer Octobre 2027", statut = "AttenteEnvoi", numero = "COUT/10027", pdfPath = "x.pdf",
                                      echeanceISO = "2027-10-01", montant = 1200f, tauxTva = 20f };
        loc.facturesEtat = new List<FactureEtat> { ligne };
        var aout = new DateTime(2027, 8, 20);
        Assert.That(FactureEnvoiAuto.Retenue(loc, ligne, aout), Is.True, "elle partira le 16/09/2027, sans facture électronique : retenue, dès sa préparation");
        ligne.plateformeDonnees = "{}";
        Assert.That(FactureEnvoiAuto.Retenue(loc, ligne, aout), Is.False, "avec sa facture électronique : elle partira");
        ligne.plateformeDonnees = null; ligne.statut = "Envoye";
        Assert.That(FactureEnvoiAuto.Retenue(loc, ligne, aout), Is.False, "déjà partie");

        string libelle = Corps(Src("Facturation/LocataireSuiviInline.cs"), "string Libelle(FactureEtat l)");
        int retenue = libelle.IndexOf("if (FactureEnvoiAuto.Retenue(_loc, l, DateTime.Today))", StringComparison.Ordinal);
        Assert.That(retenue, Is.GreaterThan(libelle.IndexOf("Cessions.HorsTitulaire(_loc, l)", StringComparison.Ordinal)), "après la mention « préparé au nom de … »");
        StringAssert.Contains("(préparée sans facture électronique, elle doit partir par Super PDP : à refaire)", libelle.Substring(retenue), "la ligne le dit, pas seulement le message du lancement");
    }

    /// Hors plan (PLAN_2C, vu en Play le 06/11) : une facture de CIPL à 0 € ne passe pas « Impayé » après son échéance : rien n'est dû. Sans PDF,
    /// 0 est un montant inconnu (état posé à la main) : comme avant. Une part nulle d'une facture due suit sa facture.
    [Test]
    public void Une_facture_de_zero_euro_ne_passe_pas_impayee()
    {
        string echue = DateTime.Today.AddDays(-60).ToString("yyyy-MM-dd");
        var loc = LocA2("COUT-ZERO");
        var equilibre = new FactureEtat { key = "depot-2026", type = "Depot", libelle = "Révision du dépôt de garantie", statut = "Envoye", numero = "COUT/09001", pdfPath = "r.pdf",
                                          echeanceISO = echue, montant = 0f };
        var aLaMain = new FactureEtat { key = "loyer-2026-P8", type = "Loyer", libelle = "Loyer Août 2026", statut = "Envoye", echeanceISO = echue };
        var due = new FactureEtat { key = "loyer-2026-P9", type = "Loyer", libelle = "Loyer Septembre 2026", statut = "Envoye", numero = "COUT/09002", pdfPath = "l.pdf",
                                    echeanceISO = echue, montant = 1200f };
        var partNulle = new FactureEtat { key = "regul-2025", type = "Regul", statut = "Envoye", numero = "COUT/09003", pdfPath = "g.pdf", echeanceISO = echue, montant = 0f };
        var autrePart = new FactureEtat { key = "regul-2025-coutTF", type = "Regul", statut = "Envoye", numero = "COUT/09003", pdfPath = "g.pdf", echeanceISO = echue, montant = 300f };
        loc.facturesEtat = new List<FactureEtat> { equilibre, aLaMain, due, partNulle, autrePart };

        Assert.That(FacturationSuivi.EtatDe(equilibre, loc), Is.EqualTo(FacturationSuivi.Etat.Envoye), "0 € : rien n'est dû, pas de « Rappel » ni d'année en rouge");
        Assert.That(FacturationSuivi.EtatDe(equilibre, null), Is.EqualTo(FacturationSuivi.Etat.Envoye), "sans le locataire aussi");
        Assert.That(FacturationSuivi.EtatDe(aLaMain, loc), Is.EqualTo(FacturationSuivi.Etat.Impaye), "« Envoyé » posé à la main, sans PDF : montant inconnu, comme avant");
        Assert.That(FacturationSuivi.EtatDe(due, loc), Is.EqualTo(FacturationSuivi.Etat.Impaye));
        Assert.That(FacturationSuivi.EtatDe(partNulle, loc), Is.EqualTo(FacturationSuivi.Etat.Impaye), "une part nulle d'une facture due suit sa facture : un seul état");
        Assert.That(FacturationSuivi.EtatDe(autrePart, loc), Is.EqualTo(FacturationSuivi.Etat.Impaye));
    }

    /// Reprise (10/11) : une correction écrit toujours son montant sur la ligne, 0 compris (FacturationSuivi.MarquerCorrige). Avant, 0 voulait
    /// dire « non fourni » : la part d'une facture regroupée tombée à 0 € gardait son ancien montant (la somme des lignes ne redonnait plus le
    /// total, Créances faux), et une révision du dépôt corrigée « à l'équilibre » gardait son complément et passait encore « Impayé ».
    [Test]
    public void Une_correction_a_zero_euro_ecrit_zero_sur_la_ligne()
    {
        // Régularisation regroupée, deux listes : à la correction, la part de la seconde tombe à 0 € (sa charge fausse, mise à 0).
        var loc = LocA2("COUT-CORR0");
        const string generale = "regul-2025", fonciere = "regul-2025-coutTF";
        const string pdf1 = "Batiment/Coutures/Corr0/Facture/regul.pdf", pdf2 = "Batiment/Coutures/Corr0/Facture/regul-corrigee1.pdf";
        string echeance = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
        var emission = FactureEmission.Preparer(loc, generale, "COUT/01031");
        FactureEmission.Enregistrer(loc, generale, "Regul", emission, "Régularisation des charges 2025", echeance, pdf1, 300f, "rib-a", loc.factureRegul, "Régularisation", true);
        FactureEmission.EnregistrerLiee(loc, fonciere, "Regul", emission, "Régularisation Taxe foncière 2025", echeance, pdf1, 100f, "rib-a", true);
        Assert.That(FacturationSuivi.DuesDe(loc).Sum(x => x.montant), Is.EqualTo(400f).Within(0.001f));

        var correction = FactureEmission.Preparer(loc, generale, "COUT/02032");
        Assert.That(correction.Correction, Is.True);
        FactureEmission.Enregistrer(loc, generale, "Regul", correction, "Régularisation des charges 2025", echeance, pdf2, 300f, "rib-a", loc.factureRegul, "Régularisation", true);
        FactureEmission.EnregistrerLiee(loc, fonciere, "Regul", correction, "Régularisation Taxe foncière 2025", echeance, pdf2, 0f, "rib-a", true);

        Assert.That(loc.facturesEtat.Find(l => l.key == fonciere).montant, Is.EqualTo(0f), "la part nulle est écrite (avant : les 100 € d'avant restaient)");
        FacturationSuivi.Solde(loc, generale, out float total, out _);
        Assert.That(total, Is.EqualTo(300f).Within(0.001f), "la somme des lignes redonne le total de la facture corrigée");
        Assert.That(FacturationSuivi.DuesDe(loc).Sum(x => x.montant), Is.EqualTo(300f).Within(0.001f), "Créances : 300 € (avant : 400 €)");

        // Révision du dépôt échue, corrigée « à l'équilibre » (0 €, voulu : jamais refusé pour le dépôt) : plus rien n'est dû.
        var depot = LocA2("COUT-CORR0-DEPOT");
        var revision = new FactureEtat { key = "depot-2026", type = "Depot", libelle = "Révision du dépôt de garantie", statut = "Envoye", numero = "COUT/09031", pdfPath = "r.pdf",
                                         echeanceISO = DateTime.Today.AddDays(-60).ToString("yyyy-MM-dd"), montant = 200f };
        depot.facturesEtat = new List<FactureEtat> { revision };
        Assert.That(FacturationSuivi.EtatDe(revision, depot), Is.EqualTo(FacturationSuivi.Etat.Impaye), "200 € échus : impayée");
        FacturationSuivi.MarquerCorrige(depot, revision.key, "Révision du dépôt de garantie", "r-corrigee1.pdf", 0f);
        Assert.That(revision.montant, Is.EqualTo(0f), "le complément n'est plus sur la ligne (avant : 200 € gardés)");
        Assert.That(FacturationSuivi.EtatDe(revision, depot), Is.EqualTo(FacturationSuivi.Etat.Envoye), "0 € : plus « Impayé », plus de « Rappel »");
        Assert.That(FacturationSuivi.DuesDe(depot).Sum(x => x.montant), Is.EqualTo(0f).Within(0.001f));
    }

    /// D6-19 (reprise ; décision du 09/11) : le total nul est refusé pour une correction aussi. Le conseil donné au refus de supprimer une charge
    /// déjà facturée (« mettez son coût à 0, puis refaites ces factures ») finissait sur ce refus pour une refacturation de cette seule charge :
    /// il dit maintenant quoi faire d'une facture qui tomberait à 0 € (l'avoir qui cite la facture, comme les autres refus).
    [Test]
    public void D6_19_le_conseil_pour_une_charge_fausse_dit_quoi_faire_d_une_facture_qui_tombe_a_zero()
    {
        var loc = new Locataire { id = "COUT-D619-LOC", Name = "Locataire D6-19" };
        var charge = new ChargeBatiment { id = "coutD619", nom = "Jardin 2026", cout = 480f, dateISO = "2026-06-01" };
        charge.MarquerFacturee(loc.id, "2026-07-01");
        string refus = ChargeBatiment.RefusSuppression(charge, new List<Locataire> { loc });
        StringAssert.Contains("dans une refacturation, mettez aussi son montant HT à 0). ", refus, "le conseil d'avant reste (facture de plusieurs charges)");
        StringAssert.EndsWith("Une facture qui tomberait à 0 € ne se refait pas : annulez-la par un avoir (Refacturation, « Avoir sur la facture n° … »).", refus);
    }

    /// Hors plan (PLAN_2C) : l'aperçu de l'entête suit un changement d'échéance (un entête avec {echeance} gardait l'ancienne date).
    [Test]
    public void L_apercu_de_l_entete_suit_l_echeance()
    {
        StringAssert.Contains("_echeance.onValueChanged.AddListener(_ => { RefreshSommeDefault(); RefreshBoutonSauver(); RefreshEntetePreview(); RefreshModeEnvoi(); });",
            Src("Facturation/FactureLoyerPanel.cs"), "loyer");
        foreach (var p in new[] { "FactureRegulPanel", "FactureRefacPanel", "FactureDepotPanel" })
            StringAssert.Contains("_echeance.onValueChanged.AddListener(_ => { RefreshSommeDefault(); RefreshEntetePreview(); });", Src("Facturation/" + p + ".cs"), p);
    }

    // ── Lot A3 · plateforme, TVA, PDF et ménage ──

    /// D5-12 (décision du 10/11) : « Payé » d'un clic, sans versement : l'« Encaissée » porte le jour du clic (FactureEtat.payeLeISO), même
    /// déclaré plus tard (panne, hors ligne). Avant : le jour où l'appel réussissait. Un versement saisi garde la priorité.
    [Test]
    public void D5_12_l_encaissee_d_un_paye_sans_versement_porte_le_jour_du_clic()
    {
        string aujourdhui = DateTime.Today.ToString("yyyy-MM-dd"), j10 = DateTime.Today.AddDays(-10).ToString("yyyy-MM-dd"), j20 = DateTime.Today.AddDays(-20).ToString("yyyy-MM-dd");
        var payee = new FactureEtat { key = "loyer-2026-P9", type = "Loyer", statut = "Paye", montant = 1200f, payeLeISO = j10 };
        var ancienne = new FactureEtat { key = "loyer-2026-P8", type = "Loyer", statut = "Paye", montant = 1200f };
        var due = new FactureEtat { key = "loyer-2026-P10", type = "Loyer", statut = "Impaye", montant = 1200f, echeanceISO = "2026-10-05" };
        var loc = LocA2("COUT-D512-LOC");   // délais propres : rien ne se lit dans les Réglages
        loc.facturesEtat = new List<FactureEtat> { payee, ancienne, due };
        Assert.That(FactureElectronique.Encaissement(loc, payee).dateISO, Is.EqualTo(j10), "le jour du clic, pas le jour de l'appel");
        Assert.That(FactureElectronique.Encaissement(loc, ancienne).dateISO, Is.EqualTo(aujourdhui), "payée avant cette version : la date du jour, comme avant");
        payee.versements.Add(new Versement { dateISO = j20, montant = 1200f });
        Assert.That(FactureElectronique.Encaissement(loc, payee).dateISO, Is.EqualTo(j20), "un versement saisi garde la priorité");

        FacturationSuivi.SetStatut(loc, due, "Paye");   // le clic « Payé » du suivi
        Assert.That(due.payeLeISO, Is.EqualTo(aujourdhui), "posé au passage à « Payé »");
        due.payeLeISO = j10;
        FacturationSuivi.SetStatut(loc, due, "Paye");
        Assert.That(due.payeLeISO, Is.EqualTo(j10), "déjà payée : sa date ne bouge pas");
        FacturationSuivi.SetStatut(loc, due, "Envoye");
        Assert.That(due.payeLeISO, Is.Empty, "vidé par SetStatut dès qu'il pose un autre état");

        // FacturationSuivi.ApresCorrection en profite : le règlement d'une facture payée d'un clic puis corrigée est noté au jour du clic.
        var corrigee = new FactureEtat { key = "loyer-2026-P7", type = "Loyer", statut = "Paye", numero = "COUT/12007", pdfPath = "l7-corrigee1.pdf", montant = 1500f, payeLeISO = j10 };
        loc.facturesEtat.Add(corrigee);
        FacturationSuivi.ApresCorrection(loc, corrigee.key, 1200f);
        Assert.That(corrigee.versements.Select(v => (v.dateISO, v.montant)), Is.EqualTo(new[] { (j10, 1200f) }));
    }

    /// D5-14 : un avoir ne cite que des factures parties chez le locataire (envoyées, impayées, payées) et pas refusées par Super PDP.
    /// Avant : tout loyer de l'année qui avait un numéro, même « en attente d'envoi », bloqué « À faire » ou refusé.
    [Test]
    public void D5_14_un_avoir_ne_cite_que_des_factures_parties()
    {
        var attente = new FactureEtat { key = "loyer-2026-P2", type = "Loyer", statut = "AttenteEnvoi", numero = "COUT/14002", pdfPath = "p2.pdf", echeanceISO = "2026-04-05" };
        var loc = new Locataire { id = "COUT-D514-LOC", Name = "Locataire D5-14", facturesEtat = new List<FactureEtat>
        {
            new FactureEtat { key = "loyer-2026-P1", type = "Loyer", statut = "Paye", numero = "COUT/14001", dateEnvoiISO = "2026-01-02" },
            attente,
            new FactureEtat { key = "loyer-2026-P3", type = "Loyer", statut = "AFaire", numero = "COUT/14003", pdfPath = "p3.pdf", echeanceISO = "2026-07-05" },
            new FactureEtat { key = "loyer-2026-P4", type = "Loyer", statut = "Impaye", numero = "COUT/14004", plateformeDonnees = "{}", plateformeEtat = "refusee" },
            new FactureEtat { key = "loyer-2026-P5", type = "Loyer", statut = "Impaye", numero = "COUT/14005", plateformeDonnees = "{}", plateformeEtat = "perimee", dateEnvoiISO = "2026-05-02" },
            new FactureEtat { key = "loyer-2026-P6", type = "Loyer", statut = "", retireApresEnvoi = true, numero = "COUT/14006", pdfPath = "p6.pdf", echeanceISO = "2026-06-05" },
        } };
        var parties = new[] { "COUT/14001", "COUT/14005", "COUT/14006" };
        Assert.That(FactureElectronique.ReferencesAvoir(loc, null, 2026).Select(x => x.Key), Is.EqualTo(parties),
            "ni « en attente d'envoi », ni bloquée « À faire », ni refusée ; citées : refaite hors plateforme et partie par e-mail, partie puis retirée du suivi (départ)");
        Assert.That(FactureElectronique.ReferencesAvoir(loc, attente, 2026).Select(x => x.Key), Is.EqualTo(parties),
            "une version précédente jamais partie n'est pas citée : les loyers partis de l'année prennent le relais");
        Assert.That(FactureElectronique.ReferencesAvoir(loc, null, 0, "COUT/14006").Select(x => x.Key), Is.EqualTo(new[] { "COUT/14006" }),
            "« Avoir sur la facture n° … » d'un loyer retiré du suivi après le départ : toujours cité");
        attente.statut = "Paye";
        Assert.That(FactureElectronique.ReferencesAvoir(loc, attente, 2026).Select(x => x.Key), Is.EqualTo(new[] { "COUT/14002" }), "partie (et payée) : la correction la cite, seule");

        // L'avoir ne se cite pas lui-même : ses références se lisent avant que sa propre ligne soit enregistrée « Envoyé » (trois formulaires).
        foreach (var p in new[] { "FactureLoyerPanel", "FactureRegulPanel", "FactureRefacPanel" })
        {
            string finaliser = Corps(Src("Facturation/" + p + ".cs"), "void Finaliser(");
            int lues = finaliser.IndexOf("var citees = FactureElectronique.ReferencesAvoir(_loc, avant, ", StringComparison.Ordinal);
            Assert.That(lues, Is.GreaterThan(0).And.LessThan(finaliser.IndexOf("FactureEmission.Enregistrer(_loc, key, ", StringComparison.Ordinal)), p + " : lues avant l'enregistrement");
            StringAssert.Contains("references = citees,", finaliser, p + " : les mêmes dans le XML");
        }
    }

    /// D5-14 (reprise) : le refus d'un avoir sans facture à citer donne la vraie raison : aucune facture ENVOYÉE au locataire.
    /// Avant : « Aucun loyer … émis dans CIPL », « émettez d'abord … », alors que le loyer en attente d'envoi est émis.
    [Test]
    public void D5_14_le_refus_d_un_avoir_sans_facture_partie_dit_envoye_et_non_emis()
    {
        const string raison = " envoyé au locataire à citer (un loyer en attente d'envoi, ou refusé par Super PDP, ne compte pas) : \"";
        foreach (var (p, texte) in new[] { ("FactureLoyerPanel", raison), ("FactureRegulPanel", raison),
                     ("FactureRefacPanel", "$\"Choisissez une facture d'origine déjà envoyée (« Avoir sur la facture n° »), ou envoyez d'abord un loyer {anneeCharge}.\"") })
        {
            string src = Src("Facturation/" + p + ".cs");
            StringAssert.Contains(texte, src, p);
            StringAssert.DoesNotContain("émis dans CIPL", src, p);
            StringAssert.DoesNotContain("mettez d'abord", src, p);
        }
        StringAssert.Contains("+ \"envoyez d'abord un loyer de l'année, ou rendez ce trop-perçu sur le prochain loyer.\"", Src("Facturation/FactureRegulPanel.cs"));
        StringAssert.Contains("+ \"corrigez plutôt une facture existante depuis le suivi (« Corriger »).\"", Src("Facturation/FactureLoyerPanel.cs"));
    }

    /// D5-15 : par la plateforme, le PDF d'un avoir de loyer ou de régularisation nomme les factures que son XML cite. Avant : rien.
    [Test]
    public void D5_15_le_PDF_d_un_avoir_nomme_les_factures_que_son_XML_cite()
    {
        var refs = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("2026/10001", "2026-01-02"), new KeyValuePair<string, string>("2026/10002", "2026-04-02") };
        Assert.That(FactureElectronique.NumerosCites(refs), Is.EqualTo("2026/10001, 2026/10002"));
        Assert.That(FacturePdfService.BuildHtml(new FacturePdfService.Data { ttc = -300f, factureOrigine = FactureElectronique.NumerosCites(refs) }),
            Does.Contain("Avoir sur les factures n° 2026/10001, 2026/10002"), "loyer à total négatif");
        Assert.That(FacturePdfService.BuildRegulHtml(new FacturePdfService.RegulData { soldeHT = -248f, factureOrigine = "2026/10001" }),
            Does.Contain("Avoir sur la facture n° 2026/10001"), "régularisation qui rembourse, par la plateforme");
        Assert.That(FacturePdfService.BuildRegulHtml(new FacturePdfService.RegulData { soldeHT = 248f, factureOrigine = "2026/10001" }),
            Does.Not.Contain("Avoir sur"), "jamais sur une facture");

        const string pose = "if (citees != null) d.factureOrigine = FactureElectronique.NumerosCites(citees);";
        string loyer = Corps(Src("Facturation/FactureLoyerPanel.cs"), "void SauvegarderEtEnvoyer()");
        Assert.That(loyer.IndexOf(pose, StringComparison.Ordinal), Is.GreaterThan(0).And.LessThan(loyer.IndexOf("FacturePdfService.GeneratePdfAsync(d, pdf)", StringComparison.Ordinal)), "loyer : avant le PDF");
        string regul = Corps(Src("Facturation/FactureRegulPanel.cs"), "void Emettre(");
        Assert.That(regul.IndexOf(pose, StringComparison.Ordinal), Is.GreaterThan(0).And.LessThan(regul.IndexOf("FacturePdfService.GenerateRegulPdfAsync(d, pdf)", StringComparison.Ordinal)), "régularisation : avant le PDF");
    }

    /// D5-17 : un taux de TVA inhabituel (« 2 » tapé pour « 20 ») se confirme, aux trois niveaux (entreprise, bail, facture) ; un taux
    /// déjà en place n'est pas redemandé. Avant : tout nombre positif passait sans un mot.
    [Test]
    public void D5_17_un_taux_de_TVA_inhabituel_se_confirme_aux_trois_niveaux()
    {
        foreach (float connu in new[] { 20f, 10f, 8.5f, 5.5f, 2.1f, 0f }) Assert.That(Tva.TauxConnu(connu), Is.True, connu.ToString());
        Assert.That(Tva.TauxConnu(2f), Is.False, "« 2 » pour « 20 »");
        Assert.That(Tva.TauxConnu(200f), Is.False);
        Assert.That(Tva.TauxAConfirmer(2f, 20f), Is.True);
        Assert.That(Tva.TauxAConfirmer(13f, 13f), Is.False, "déjà en place (confirmé une fois) : pas redemandé");
        Assert.That(Tva.TauxAConfirmer(10f, 20f), Is.False, "un taux courant ne se confirme pas");
        bool fait = false;
        Tva.ConfirmerTaux(20f, 13f, () => fait = true);
        Assert.That(fait, Is.True, "taux courant : la suite s'exécute tout de suite");

        StringAssert.Contains("void SaveFromUI() => Tva.ConfirmerTaux(TauxSaisi(), R.tauxTva, Enregistrer);", Src("Facturation/ReglagePanel.cs"), "entreprise : Réglages");
        string bail = Src("RevisionLoyer/RevisionPanel.cs");
        StringAssert.Contains("_btnEnregistrer.onClick.AddListener(ConfirmerTauxPuisValider);", bail, "bail : Gestion du loyer");
        StringAssert.Contains("Tva.ConfirmerTaux(taux, enPlace, ValiderInitialisation);", bail);
        foreach (var p in new[] { "FactureLoyerPanel", "FactureRegulPanel", "FactureRefacPanel" })
            StringAssert.Contains("Tva.ConfirmerTaux(TauxFacture(), Tva.TauxPropose(_loc, null, Corrigee()), SauvegarderEtEnvoyer)", Src("Facturation/" + p + ".cs"), p + " : facture");
    }

    /// D5-18 : la ligne « Envoi » d'un décompte de sortie dont une retenue est soumise à TVA ne dit plus « sans TVA » ; elle dit qu'il ne
    /// part pas en facture électronique. Le dépôt et le décompte sans retenue soumise gardent leur ligne.
    [Test]
    public void D5_18_la_ligne_Envoi_du_decompte_ne_dit_plus_sans_TVA()
    {
        Assert.That(FactureDepotPanel.LibelleEnvoi(0f), Is.EqualTo("Envoi : e-mail (le dépôt de garantie, sans TVA, ne passe pas par la plateforme)."));
        string avecTva = FactureDepotPanel.LibelleEnvoi(40f);
        Assert.That(avecTva, Does.StartWith("Envoi : e-mail seulement").And.Contain("TVA sur ses retenues").And.Contain("ne part pas en facture électronique"));
        StringAssert.DoesNotContain("sans TVA", avecTva);
        StringAssert.Contains("_modeInfo.text = LibelleEnvoi(d.tva);", Corps(Src("Facturation/FactureDepotPanel.cs"), "void RefreshTotaux()"), "la ligne suit les retenues cochées");
    }

    /// D6-23 (décision du 09/11) : le dépôt de garantie propose « Aucune mention » d'office, quel que soit le réglage d'entreprise ; un
    /// choix enregistré est gardé. Le décompte de sortie neuf, qui peut porter de la TVA, propose la mention de l'entreprise.
    [Test]
    public void D6_23_le_depot_propose_aucune_mention_d_office()
    {
        var encaissements = new ReglageData { mentionTvaParDefaut = MentionTva.Encaissements };
        Assert.That(MentionTva.Lire(new Locataire().factureDepot, MentionTva.Aucune), Is.EqualTo(MentionTva.Aucune), "premier dépôt : aucune mention");
        Assert.That(MentionTva.Lire(new FactureInfo(), MentionTva.Aucune), Is.Not.EqualTo(MentionTva.ParDefaut(encaissements)), "il ne suit pas le réglage d'entreprise");
        var choisi = new FactureInfo { saved = true };
        MentionTva.Ecrire(choisi, MentionTva.Encaissements);
        Assert.That(MentionTva.Lire(choisi, MentionTva.Aucune), Is.EqualTo(MentionTva.Encaissements), "un choix enregistré est gardé");

        string depot = Corps(Src("Facturation/FactureDepotPanel.cs"), "void LoadIntoUI()");
        StringAssert.Contains("MentionTva.Charger(_mentionTva, _texteTvaDebit, f, MentionTva.Aucune);", depot);
        StringAssert.Contains("MentionTva.Charger(_mentionTvaS, _texteTvaDebitS, _sortie && FacturationSuivi.EstDejaEmise(_loc, DepartLocataire.CleDecompte, out _) ? f : null);", depot,
            "décompte neuf : la mention de l'entreprise, pas le « Aucune mention » gardé par le dépôt");
    }

    /// Reste du lot 13 : la carte des Réglages dit ce qui part par Super PDP aujourd'hui (loyer, régularisation, refacturation, avec TVA,
    /// locataire avec SIRET). Avant : « les factures de loyer aux locataires assujettis partent aussi ».
    [Test]
    public void Reste_du_lot_13_la_carte_des_Reglages_dit_ce_qui_part_par_Super_PDP()
    {
        string active = FactureElectronique.Statut(new FactureElectronique.Reponse { ok = true, relie = true, statut = "verified" });
        Assert.That(active, Is.EqualTo("Active : les factures avec TVA (loyer, régularisation, refacturation) des locataires qui ont un SIRET partent par Super PDP."));
    }

    /// D6-11 : un loyer à total négatif est titré « AVOIR » : sa phrase de règlement ne dit plus « SOMME À NOUS RÉGLER », à l'écran
    /// (phrase proposée) comme sur le PDF (filet de l'émission), comme dans les trois autres formulaires.
    [Test]
    public void D6_11_un_loyer_en_avoir_ne_dit_plus_somme_a_nous_regler()
    {
        Assert.That(FactureEmission.PhraseSomme("SOMME À NOUS RÉGLER LE 1 décembre 2026", -300f), Is.EqualTo("SOMME QUI VOUS SERA REMBOURSÉE"));
        string loyer = Src("Facturation/FactureLoyerPanel.cs");
        StringAssert.Contains("sommePhrase = FactureVarResolver.Resolve(FactureEmission.PhraseSomme(_sommePhrase.text, ttc), _loc, _bat, ctx),", Corps(loyer, "FacturePdfService.Data BuildData()"),
            "le PDF : la phrase suit le signe du total");
        StringAssert.Contains("< -0.005f) return \"SOMME QUI VOUS SERA REMBOURSÉE\";", Corps(loyer, "string DefaultSomme()"), "l'écran : la phrase proposée aussi");
        StringAssert.Contains("RefreshSommeDefault();", Corps(loyer, "void RefreshMontants()"), "elle suit le total quand il change de signe");
    }

    /// D6-20 (majeur ; décision du 09/11) : la refacturation qui imprime « Justificatif joint » part avec le PDF de ses charges, depuis le
    /// formulaire, à l'envoi au lancement et avec la notification d'après la bascule. Un justificatif introuvable ne bloque pas l'envoi : il est dit.
    [Test]
    public void D6_20_les_justificatifs_coches_partent_avec_la_facture()
    {
        var tous = FactureRefacPanel.PiecesJointes("f.pdf", new[] { "a.pdf", "", "b.pdf", "a.pdf" }, true, out var absents, _ => true);
        Assert.That(tous, Is.EqualTo(new[] { "f.pdf", "a.pdf", "b.pdf" }), "la facture, puis les justificatifs, une fois chacun ; un vide est ignoré");
        Assert.That(absents, Is.Empty);
        Assert.That(FactureRefacPanel.AbsentsDits(absents), Is.Null);
        Assert.That(FactureRefacPanel.PiecesJointes("f.pdf", new[] { "a.pdf", "b.pdf" }, false, out _, _ => true), Is.EqualTo(new[] { "f.pdf" }), "case décochée : la facture seule");
        Assert.That(FactureRefacPanel.PiecesJointes(null, new[] { "a.pdf" }, true, out _, _ => true), Is.EqualTo(new[] { "a.pdf" }), "notification : pas de PDF de facture");
        var presents = FactureRefacPanel.PiecesJointes("f.pdf", new[] { "a.pdf", "dossier/b.pdf" }, true, out absents, p => p != "dossier/b.pdf");
        Assert.That(presents, Is.EqualTo(new[] { "f.pdf", "a.pdf" }), "un justificatif introuvable ne bloque pas l'envoi");
        Assert.That(FactureRefacPanel.AbsentsDits(absents), Is.EqualTo("Justificatif introuvable sur ce poste, non joint : b.pdf."));

        // Une ligne du suivi (envoi au lancement, notification) : les charges de ses clés « refac-<id> », la case du réglage du type.
        var avecPdf = new ChargeBatiment { id = "coutD620a", nom = "Serrurier", cout = 480f, dateISO = "2026-06-01", pdfPath = "cout-d620-serrurier.pdf" };
        var sansPdf = new ChargeBatiment { id = "coutD620b", nom = "Jardin", cout = 120f, dateISO = "2026-06-02" };
        var bat = new Batiment { id = "COUT-D620-BAT", Name = "Bâtiment D6-20", charges = new List<ChargeBatiment> { avecPdf, sansPdf } };
        var ligne = new FactureEtat { key = "refac-coutD620a", type = "Refac", statut = "AttenteEnvoi", numero = "COUT/20001", pdfPath = "refac-d620.pdf", montant = 576f };
        var liee = new FactureEtat { key = "refac-coutD620b", type = "Refac", statut = "AttenteEnvoi", numero = "COUT/20001", pdfPath = "refac-d620.pdf", montant = 144f };
        var loyer = new FactureEtat { key = "loyer-2026-P6", type = "Loyer", statut = "AttenteEnvoi", numero = "COUT/20002", pdfPath = "loyer-d620.pdf", montant = 1200f };
        var loc = new Locataire { id = "COUT-D620-LOC", Name = "Locataire D6-20", factureRefac = new FactureInfo { joindrePj = true }, facturesEtat = new List<FactureEtat> { ligne, liee, loyer } };
        Assert.That(FactureRefacPanel.PiecesDeLigne(loc, ligne, bat, "facture.pdf", out absents), Is.EqualTo(new[] { "facture.pdf" }), "le justificatif n'est pas sur ce poste : la facture part quand même");
        Assert.That(absents, Is.EqualTo(new[] { "cout-d620-serrurier.pdf" }), "retrouvé par la clé de la ligne, et dit");
        Assert.That(FactureRefacPanel.PiecesDeLigne(loc, loyer, bat, "facture.pdf", out absents), Is.EqualTo(new[] { "facture.pdf" }));
        Assert.That(absents, Is.Empty, "un loyer n'a pas de justificatif");
        loc.factureRefac.joindrePj = false;
        FactureRefacPanel.PiecesDeLigne(loc, ligne, bat, "facture.pdf", out absents);
        Assert.That(absents, Is.Empty, "case décochée à l'émission : rien à joindre");

        StringAssert.Contains("var envoi = EmailService.Envoyer(dest, objet, corps, pieces);", Corps(Src("Facturation/FactureRefacPanel.cs"), "System.Collections.IEnumerator EnvoyerPuisFinaliser("),
            "le formulaire");
        string auto = Src("Facturation/FactureEnvoiAuto.cs");
        StringAssert.Contains("var pieces = FactureRefacPanel.PiecesDeLigne(c.loc, c.rec, bat, c.pdfAbsolu, out var absents);", auto, "l'envoi au lancement");
        StringAssert.Contains("envoi = EmailService.Envoyer(c.destinataire, objet, corps, pieces);", auto);
        StringAssert.Contains("var pieces = FactureRefacPanel.PiecesDeLigne(loc, r, bat, null, out var absents);", Src("Facturation/FactureElectronique.cs"), "la notification d'après la bascule");
    }

    // ── Reprise de l'ensemble ──

    /// D6-20 (reprise de l'ensemble, ENS-1) : à l'envoi groupé du lancement, une facture partie sans un justificatif (introuvable sur ce
    /// poste) est dite au centre, même quand tout est parti : il y a une pièce à renvoyer, et le message du bas disparaît seul.
    [Test]
    public void D6_20_un_justificatif_non_joint_a_l_envoi_groupe_est_dit_au_centre()
    {
        const string note = "Justificatif introuvable sur ce poste, non joint : a.pdf.";
        Assert.That(FactureEnvoiAuto.Bilan(2, 0, "", new string[0], ""), Is.Null, "tout est parti, rien à dire : le message du bas suffit");
        Assert.That(FactureEnvoiAuto.Bilan(2, 0, "", new string[0], "\nSARL A : " + note + "\nSARL B : " + note),
            Is.EqualTo("2 facture(s) envoyée(s).\n\nSARL A : " + note + "\nSARL B : " + note), "tout est parti, mais il manque une pièce : un bilan, au centre");
        StringAssert.EndsWith("\n• Sephora — déjà envoyée par Odile\n\nSARL A : " + note,
            FactureEnvoiAuto.Bilan(1, 0, "", new[] { "Sephora — déjà envoyée par Odile" }, "\nSARL A : " + note), "avec une facture sautée : les notes à la fin");

        string auto = Src("Facturation/FactureEnvoiAuto.cs");
        string envoyer = Corps(auto, "IEnumerator Envoyer(List<Candidate> choisies)");
        StringAssert.Contains("string bilan = Bilan(ok, echecs, derniereErreur, sautees, notes);", envoyer, "la fenêtre du lancement passe ses notes à la règle");
        StringAssert.Contains("else ConfirmDialog.Erreur(bilan);", envoyer);
        StringAssert.Contains("if (issue.Partie && issue.Note != null) ConfirmDialog.Erreur($\"Facture de {c.loc.Name} envoyée : sa fiche est libre. {issue.Note}\");",
            Corps(auto, "IEnumerator Envoyer(int i)"), "la file d'attente (fiche enfin libre) aussi");
    }

    // ── Décisions du 11/11 ──

    /// D5-12 (décision du 11/11) : des versements qui ne couvrent pas le total (un acompte) ne datent pas le « Payé » d'un clic : le
    /// reste est reçu le jour du clic. Avant : acompte du 05/09, « Payé » cliqué le 30/09, « Encaissée » déclarée au 05/09.
    [Test]
    public void D5_12_un_acompte_ne_date_pas_le_paye_d_un_clic()
    {
        var f = new FactureEtat { key = "loyer-2026-P9", type = "Loyer", statut = "Paye", montant = 1200f, payeLeISO = "2026-09-30" };
        f.versements.Add(new Versement { dateISO = "2026-09-05", montant = 500f });
        var loc = LocA2("COUT-D512B-LOC");
        loc.facturesEtat = new List<FactureEtat> { f };
        Assert.That(FactureElectronique.Encaissement(loc, f).dateISO, Is.EqualTo("2026-09-30"), "acompte : le jour du clic");
        f.versements.Add(new Versement { dateISO = "2026-09-20", montant = 700f });
        Assert.That(FactureElectronique.Encaissement(loc, f).dateISO, Is.EqualTo("2026-09-20"), "versements qui couvrent le total : le dernier versement");
        f.payeLeISO = "";
        f.versements.RemoveAt(1);
        Assert.That(FactureElectronique.Encaissement(loc, f).dateISO, Is.EqualTo("2026-09-05"), "payée avant cette version : le dernier versement, comme avant");
    }

    /// D4-10 (décision du 11/11 ; D6-N10) : pendant une correction, le bouton de la régularisation, de la refacturation et du dépôt
    /// dit « Corriger et envoyer », comme celui du loyer. Avant : seul le titre disait « (correction) ».
    [Test]
    public void D4_10_le_bouton_des_trois_formulaires_dit_corriger_pendant_une_correction()
    {
        var bouton = new GameObject("bouton");
        try
        {
            var texte = new GameObject("texte").AddComponent<TMPro.TextMeshProUGUI>();
            texte.transform.SetParent(bouton.transform);
            FactureEmission.NommerBouton(bouton.transform, true);
            Assert.That(texte.text, Is.EqualTo("Corriger et envoyer"));
            FactureEmission.NommerBouton(bouton.transform, false);
            Assert.That(texte.text, Is.EqualTo("Sauvegarder et envoyer"));
            FactureEmission.NommerBouton(null, true);   // formulaire pas encore construit : rien
        }
        finally { UnityEngine.Object.DestroyImmediate(bouton); }
        foreach (var f in new[] { "FactureRegulPanel", "FactureRefacPanel", "FactureDepotPanel" })
            Assert.That(Fois(Src("Facturation/" + f + ".cs"), "FactureEmission.NommerBouton(_btnSauver, FactureEmission.SeraUneCorrection(_loc, "), Is.EqualTo(1), f);
    }

    /// D6-15 (décision du 11/11) : sans RIB, le guide « Une première facture » d'un administrateur commence par « Vos RIB » : la
    /// facture serait refusée (FactureEmission.RefusSansRib). Un membre ne peut pas ajouter de RIB : il garde le message du refus.
    [Test]
    public void D6_15_sans_rib_le_guide_de_la_premiere_facture_commence_par_les_rib()
    {
        var commencer = Corps(Src("Cloud/PremiersPas.cs"), "public static void Commencer(Ligne l)");
        StringAssert.Contains("if (l == Ligne.Facture) { var e = Lire(); if (e.admin && !e.rib) l = Ligne.Rib; }", commencer);
        Assert.That(commencer.IndexOf("l = Ligne.Rib;", StringComparison.Ordinal), Is.LessThan(commencer.IndexOf("var avant = Maintenant();", StringComparison.Ordinal)), "avant d'ouvrir l'écran");
    }

    // ── Décisions du 14/11 (tests en Play des coutures) ──

    /// Décision du 14/11 : une régularisation neuve à 0 € part comme un décompte : titre « DÉCOMPTE DE RÉGULARISATION », sans numéro,
    /// avant le refus du total nul (qui reste pour la correction d'une facture partie).
    [Test]
    public void Une_regularisation_a_zero_part_comme_un_decompte()
    {
        var html = FacturePdfService.BuildRegulHtml(new FacturePdfService.RegulData { decompte = true, numero = "", soldeHT = 0f });
        StringAssert.Contains("DÉCOMPTE DE RÉGULARISATION", html);
        StringAssert.DoesNotContain("FACTURE :", html);
        StringAssert.Contains("FACTURE : 2026/11001", FacturePdfService.BuildRegulHtml(new FacturePdfService.RegulData { numero = "2026/11001", soldeHT = 200f }), "une facture garde son titre");
        var sauver = Corps(Src("Facturation/FactureRegulPanel.cs"), "void SauvegarderEtEnvoyer()");
        int decompte = sauver.IndexOf("if (Math.Abs(d.ttc) <= 0.004f && !emission.Correction) { EnvoyerDecompte(d); return; }", StringComparison.Ordinal);
        Assert.That(decompte, Is.GreaterThan(0));
        Assert.That(decompte, Is.LessThan(sauver.IndexOf("FactureEmission.RefusTotalNul(d.ttc", StringComparison.Ordinal)), "avant le refus du total nul");
    }

    /// Décision du 15/11 (vu en jouant BT39) : la correction d'un loyer émis avant D6-07 (provision imprimée inconnue) retrouve sa
    /// provision à partir du montant imprimé, au lieu de la provision mémorisée par la dernière utilisation du formulaire (210 € d'un
    /// trimestre de 2027 sur le loyer de mai de sephora : la correction partait à 1 252 € au lieu de 1 000 €).
    [Test]
    public void La_correction_d_un_ancien_loyer_retrouve_sa_provision_imprimee()
    {
        var mai = new FactureEtat { key = "loyer-2026-P5", type = "Loyer", statut = "Envoye", pdfPath = "mai.pdf", montant = 999.996f, tauxTva = 20f };
        Assert.That(FactureLoyerPanel.ProvisionDeduite(mai, 833.33f, 0f), Is.EqualTo(0f), "1 000 € TTC = 833,33 € de loyer : aucune provision");
        Assert.That(FactureLoyerPanel.ProvisionDeduite(new FactureEtat { montant = 1084f, tauxTva = 20f }, 833.33f, 0f), Is.EqualTo(70f).Within(0.005f));
        Assert.That(FactureLoyerPanel.ProvisionDeduite(new FactureEtat { montant = 903.33f, tauxTva = 0f }, 833.33f, 20f), Is.EqualTo(50f).Within(0.005f), "moins les listes");
        Assert.That(FactureLoyerPanel.ProvisionDeduite(new FactureEtat { montant = 500f, tauxTva = 20f }, 833.33f, 0f), Is.Zero, "jamais négative");
        var memo = new FactureInfo { saved = true, provisionMontant = 210f };
        Assert.That(FactureLoyerPanel.ProvisionProposee(memo, 70f, mai, 0f), Is.EqualTo(0f), "pas la provision mémorisée d'une autre période");
        Assert.That(FactureLoyerPanel.ProvisionProposee(memo, 70f, mai), Is.EqualTo(210f), "sans déduction, comme avant (appels existants)");
        var defaut = Corps(Src("Facturation/FactureLoyerPanel.cs"), "string DefaultProvision(string listeId)");
        StringAssert.Contains("var memos = corrigee == null || inconnue ? null : corrigee.provisionsListes;", defaut, "les listes non plus");
    }

    /// Décision du 15/11 (BS9) : départ annulé : un loyer parti puis retiré du suivi revient de lui-même dans son état (« Payé » si ce
    /// qui a été versé le couvre, sinon « Envoyé »), sauf s'il est cité par un avoir. Avant : « À faire », à remettre à la main.
    [Test]
    public void Un_depart_annule_remet_les_loyers_retires_dans_leur_etat()
    {
        FactureEtat Retire(string key, string numero) => new FactureEtat { key = key, type = "Loyer", libelle = key, numero = numero, pdfPath = key + ".pdf",
                                                                     montant = 1200f, echeanceISO = "2026-12-01", retireApresEnvoi = true };
        var impaye = Retire("loyer-2026-P12", "COUT/12003");
        var paye = Retire("loyer-2026-P11", "COUT/11003");
        paye.versements.Add(new Versement { dateISO = "2026-11-02", montant = 1200f });
        var annule = Retire("loyer-2026-P10", "COUT/10003");
        var avoir = new FactureEtat { key = "refac-avoir", type = "Refac", numero = "COUT/11009", pdfPath = "avoir.pdf", statut = "Envoye", montant = -1200f, factureCitee = "loyer-2026-P10" };
        var loc = LocA2("COUT-BS9-LOC");
        loc.facturesEtat = new List<FactureEtat> { impaye, paye, annule, avoir };
        Assert.That(DepartLocataire.RemettreRetires(loc), Is.EqualTo(2));
        Assert.That((impaye.statut, impaye.retireApresEnvoi), Is.EqualTo(("Envoye", false)), "impayé : « Envoyé »");
        Assert.That((paye.statut, paye.retireApresEnvoi), Is.EqualTo(("Paye", false)), "réglé : « Payé »");
        Assert.That((annule.statut ?? "", annule.retireApresEnvoi), Is.EqualTo(("", true)), "annulé par un avoir : reste retiré");
        StringAssert.Contains("int remis = DepartLocataire.RemettreRetires(loc);", File.ReadAllText(Path.Combine(Application.dataPath, "Script", "Locataire", "DepartLocataireUI.cs")));
    }

    /// Décision du 14/11 (BV8) : la séquence est commune aux quatre formulaires : quand un numéro est pris, l'ID mémorisé de TOUS est
    /// oublié. Avant : l'ID « 001 » gardé par un loyer refusé revenait après une régularisation émise sous 2026/11001.
    [Test]
    public void Un_numero_pris_fait_oublier_l_id_memorise_des_quatre_formulaires()
        => Assert.That(Fois(Src("Facturation/FactureEmission.cs"),
               "foreach (var i in new[] { info, loc.factureLoyer, loc.factureRegul, loc.factureRefac, loc.factureDepot }) if (i != null) i.numeroId = \"\";"), Is.EqualTo(1));
}
