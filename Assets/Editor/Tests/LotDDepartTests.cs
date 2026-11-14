using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot D, groupe DEPART (11/11/2026) : départ du locataire, remplaçant, cession, documents du bail. Une règle pure par
/// défaut corrigé (D7a-19, D7a-20, D7a-21, D7a-22, D7a-26, D7a-12) ; le texte du code pour l'ordre des écritures et les
/// réservations (D7a-17, D7a-23), qui demandent la scène et le serveur.
public class LotDDepartTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));

    static Locataire Loc(string id, int lot, string debut, string sortie = null) => new Locataire
    {
        id = id, Name = "SARL " + id, lotBatiment = lot,
        typeRevision = TypeRevision.Aucune,
        loyerAnnuel = 12000f, loyerDepart = 12000f, periodiciteLoyer = Periodicite.trimestriel,
        dateDebutBailISO = debut, dateFinBailISO = "2034-12-31", depotInitialise = true, dateSortieISO = sortie,
    };

    // ── D7a-19 : la date de départ ne bouge pas sous le remplaçant ──────────────

    [Test]
    public void D7a_19_un_depart_qui_bouge_ne_chevauche_pas_le_remplacant()
    {
        var a = Loc("A", 2, "2026-01-01", "2026-06-30");
        var b = Loc("B", 2, "2026-07-01");
        b.dateDebutPremierBailISO = b.dateDebutBailISO;   // comme PreparerRemplacant
        var bat = new Batiment { locataireDuBatiment = { a, b } };
        var sortie = new DateTime(2026, 6, 30);

        Assert.That(DepartLocataire.Remplacant(a, bat, sortie), Is.SameAs(b));
        Assert.That(DepartLocataire.RefusRemplacant(a, bat, new DateTime(2026, 7, 31)),
            Does.Contain("« SARL B » reprend ce lot le 01/07/2026 : avec un autre dernier jour, ce lot serait facturé à deux locataires, ou à personne."), "départ repoussé");
        Assert.That(DepartLocataire.RefusRemplacant(a, bat, new DateTime(2026, 7, 31)),
            Does.Contain("Gardez le dernier jour au 30/06/2026, ou changez d'abord le début du bail de « SARL B »"), "les deux solutions");
        Assert.That(DepartLocataire.RefusRemplacant(a, bat, new DateTime(2026, 5, 31)), Is.Not.Null, "départ avancé : un mois à personne");
        Assert.That(DepartLocataire.RefusRemplacant(a, bat, sortie), Is.Null, "même date (état des lieux joint) : permis");
        string annul = DepartLocataire.RefusRemplacant(a, bat, null);
        StringAssert.Contains("si le départ est annulé, « SARL A » et « SARL B » seraient facturés tous les deux pour ce lot.", annul, "annuler le départ aussi : pourquoi");
        StringAssert.Contains("supprimez la fiche de « SARL B » s'il ne vient plus", annul, "sans facture partie : on peut le supprimer");
        StringAssert.Contains("ou changez le début de son bail", annul);
        b.facturesEtat = new List<FactureEtat> { new FactureEtat { key = "loyer-2026-P7", statut = "Envoye", numero = "2026/07001", pdfPath = "x.pdf" } };
        annul = DepartLocataire.RefusRemplacant(a, bat, null);
        StringAssert.DoesNotContain("supprimez", annul, "une facture est partie : sa fiche ne se supprime pas (RefusSuppression)");
        StringAssert.Contains("sa fiche ne peut pas être supprimée", annul);
        b.facturesEtat.Clear();

        b.dateDebutBailISO = "2026-08-01";   // la règle Remplacant ne lit que le bail courant
        Assert.That(DepartLocataire.Remplacant(a, bat, sortie), Is.Null, "pas au lendemain : pas son remplaçant ; le refus peut être levé");
        Assert.That(DepartLocataire.RefusRemplacant(a, bat, new DateTime(2026, 7, 31)), Is.Null);
        b.dateDebutBailISO = "2026-07-01";
        b.archive = true;
        Assert.That(DepartLocataire.Remplacant(a, bat, sortie), Is.Null, "archivé : ne compte pas");
        b.archive = false;
        a.lotBatiment = 0;
        Assert.That(DepartLocataire.Remplacant(a, bat, sortie), Is.Null, "sans lot déclaré : personne ne reprend « ce lot »");
        a.lotBatiment = 2;
        // D7a-16 : une fiche jamais enregistrée n'est pas dans le bâtiment : elle n'existe pas pour la règle.
        bat.locataireDuBatiment.Remove(b);
        Assert.That(DepartLocataire.Remplacant(a, bat, sortie), Is.Null);
    }

    /// Suite du 12/11 : changer le début du bail du remplaçant déplace aussi sa facturation, tant qu'aucune facture n'est partie.
    [Test]
    public void D7a_19_le_premier_bail_suit_un_debut_corrige_avant_toute_facture()
    {
        var aout = new DateTime(2026, 8, 1);
        var b = Loc("B", 2, "2026-07-01");
        b.dateDebutPremierBailISO = b.dateDebutBailISO;   // comme PreparerRemplacant, ou après « Initialiser »
        Assert.That(DepartLocataire.PremierBailApres(b, aout), Is.EqualTo("2026-08-01"), "correction : le premier bail suit");

        b.facturesEtat = new List<FactureEtat> { new FactureEtat { key = "loyer-2026-P3", statut = "Envoye", numero = "2026/07001", pdfPath = "x.pdf" } };
        Assert.That(DepartLocataire.PremierBailApres(b, aout), Is.EqualTo("2026-07-01"), "une facture est partie : renouvellement, premier bail gardé");
        b.facturesEtat.Clear();

        b.dateDebutPremierBailISO = "2017-07-01";
        Assert.That(DepartLocataire.PremierBailApres(b, aout), Is.EqualTo("2017-07-01"), "bail déjà renouvelé : gardé");
        b.dateDebutPremierBailISO = null;
        Assert.That(DepartLocataire.PremierBailApres(b, aout), Is.Null, "jamais posé : Loyers.DebutPremierBail lit le bail courant");

        string fiche = Src("Locataire/LocatairePrefab.cs");
        int premier = fiche.IndexOf("locataire.dateDebutPremierBailISO = DepartLocataire.PremierBailApres(locataire, debutSaisi);", StringComparison.Ordinal);
        Assert.That(premier, Is.GreaterThan(0));
        Assert.That(fiche.IndexOf("locataire.DateDebutBail = debutSaisi;", StringComparison.Ordinal), Is.GreaterThan(premier),
            "la règle compare l'ancien début : elle passe AVANT l'écriture du nouveau");
    }

    // ── D7a-22, D7a-26 : les types proposés par la fenêtre ─────────────────────

    [Test]
    public void D7a_26_un_remplacant_existant_n_est_pas_recree()
    {
        Assert.That(DepartPanel.TypesProposes(false, false, false),
            Is.EqualTo(new[] { DepartPanel.TypeLibre, DepartPanel.TypeRemplacant, DepartPanel.TypeCession }), "premier départ");
        Assert.That(DepartPanel.TypesProposes(true, false, true), Has.No.Member(DepartPanel.TypeRemplacant), "remplaçant déjà là");
        Assert.That(DepartPanel.TypesProposes(true, false, false), Has.Member(DepartPanel.TypeRemplacant), "départ sans remplaçant : on peut en créer un");
    }

    [Test]
    public void D7a_22_la_cession_prevue_reste_modifiable_apres_un_depart()
    {
        Assert.That(DepartPanel.TypesProposes(true, true, false), Has.Member(DepartPanel.TypeCession), "cession prévue : la modifier ou l'annuler");
        Assert.That(DepartPanel.TypesProposes(true, false, false), Has.No.Member(DepartPanel.TypeCession), "départ sans cession : pas de cession après");
        Assert.That(DepartPanel.TypesProposes(false, true, false), Has.Member(DepartPanel.TypeCession));

        // Retour de BW4 (12/11) : la fenêtre s'ouvre sur la cession prévue, même avec un départ saisi.
        Assert.That(DepartPanel.TypeOuvert(DepartPanel.TypesProposes(true, true, false), true), Is.EqualTo(DepartPanel.TypeCession));
        Assert.That(DepartPanel.TypeOuvert(DepartPanel.TypesProposes(false, true, false), true), Is.EqualTo(DepartPanel.TypeCession));
        Assert.That(DepartPanel.TypeOuvert(DepartPanel.TypesProposes(true, false, false), false), Is.EqualTo(DepartPanel.TypeLibre));
        Assert.That(DepartPanel.TypeOuvert(DepartPanel.TypesProposes(false, false, false), false), Is.EqualTo(DepartPanel.TypeLibre));
    }

    // ── D7a-21 : un document absent du PC reste ouvrable sur le cloud ──────────

    [Test]
    public void D7a_21_un_document_du_bail_absent_du_PC_reste_ouvrable_sur_le_cloud()
    {
        Assert.That(BailFileUI.EtatLigne(true, false, true, "x.pdf", "Aucun bail lié"), Is.EqualTo(("Pas encore reçu : x.pdf", true)));
        Assert.That(BailFileUI.EtatLigne(true, false, false, "x.pdf", "Aucun bail lié"), Is.EqualTo(("Introuvable : x.pdf", false)));
        Assert.That(BailFileUI.EtatLigne(true, true, true, "x.pdf", "Aucun bail lié"), Is.EqualTo(("x.pdf", true)));
        Assert.That(BailFileUI.EtatLigne(false, false, true, "", "Aucun bail lié"), Is.EqualTo(("Aucun bail lié", false)));
    }

    // ── D7a-20 : l'archivage regarde toutes les années du bail ─────────────────

    static FactureEtat Paye(string key, string echeance)
        => new FactureEtat { key = key, type = key.StartsWith("loyer") ? "Loyer" : "Regul", statut = "Paye", numero = key,
                             pdfPath = key + ".pdf", montant = 1000f, echeanceISO = echeance };

    [Test]
    public void D7a_20_archiver_attend_les_loyers_et_reguls_des_annees_d_avant()
    {
        // Bail mensuel au 01/01/2024, départ le 15/01/2025 ; décembre 2024 jamais émis ; janvier 2025 (au prorata) payé.
        var loc = Loc("L", 1, "2024-01-01", "2025-01-15");
        loc.periodiciteLoyer = Periodicite.mensuel;
        for (int m = 1; m <= 11; m++) loc.facturesEtat.Add(Paye($"loyer-2024-P{m}", $"2024-{m:00}-01"));
        loc.facturesEtat.Add(Paye("loyer-2025-P1", "2025-01-01"));

        Assert.That(DepartLocataire.PretAArchiver(loc), Is.False, "décembre 2024 n'a jamais été facturé");
        Assert.That(DepartLocataire.RaisonsNonArchivable(loc).Any(r => r.Contains("2024") && r.Contains("à facturer")), Is.True,
            "la raison cite le loyer de décembre 2024");
        Assert.That(DepartLocataire.StatutDe(loc, DepartLocataire.Etape.DernierLoyer, DateTime.Today, out _), Is.EqualTo(DepartLocataire.Statut.AFaire));
        Assert.That(DepartLocataire.LigneDernierLoyer(loc)?.key, Is.EqualTo("loyer-2024-P12"), "l'étape ouvre le loyer oublié");

        loc.facturesEtat.Add(Paye("loyer-2024-P12", "2024-12-01"));
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.True, "témoin : tout est payé, sans dépôt ni provision");

        // Avec provisions : la régul 2024 (une année entière de charges) compte, pas seulement celle de 2025.
        loc.provisionPourCharges = true;
        loc.facturesEtat.Add(Paye("regul-2025", "2026-01-31"));
        Assert.That(DepartLocataire.RegulsDeSortieFaites(loc), Is.False);
        Assert.That(DepartLocataire.RaisonsNonArchivable(loc), Has.Some.Contains("Régularisation des charges 2024"));
        Assert.That(DepartLocataire.LigneRegulSortie(loc)?.key, Is.EqualTo("regul-2024"));
        loc.facturesEtat.Add(Paye("regul-2024", "2025-01-31"));
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.True);

        // Une période reprise (« Clôturée », avant la reprise de facturation) ne bloque pas.
        loc.facturesEtat.RemoveAll(r => r.key == "regul-2024" || r.key == "loyer-2024-P12");
        loc.repriseFacturationISO = "2025-02-01";
        Assert.That(DepartLocataire.PretAArchiver(loc), Is.True, "années reprises : historique, rien à faire");
    }

    // ── D7a-12 : la cession retire la fiche société du cédant ──────────────────

    [Test]
    public void D7a_12_une_cession_ne_garde_pas_le_bloc_Pappers_du_cedant()
    {
        var loc = Loc("L", 1, "2026-01-01");
        loc.commentaire = "Note perso\n\n" + PappersResume.Debut + "\nSARL Ancien · SIREN : 123 456 789\nÉtat : EN DIFFICULTÉ\n" + PappersResume.Fin;
        Cessions.Appliquer(loc, new DateTime(2026, 6, 1), "Jean Martin", "", "", "");
        Assert.That(loc.commentaire, Does.StartWith("Note perso"), "ce qu'elle a écrit reste");
        Assert.That(loc.commentaire, Does.Not.Contain("SIREN").And.Not.Contain("État :"));
        Assert.That(loc.commentaire, Does.Contain("Bail cédé le 01/06/2026"));

        var sans = Loc("M", 1, "2026-01-01");
        sans.commentaire = "Rien de Pappers";
        Cessions.Appliquer(sans, new DateTime(2026, 6, 1), "Jean Martin", "", "", "");
        Assert.That(sans.commentaire, Is.EqualTo("Rien de Pappers"), "sans bloc : rien n'est ajouté");

        StringAssert.Contains("if (p == null) fiche.StartCoroutine(PappersSync.Un(fiche.batimentPrefabOrigin, loc));",
            Src("Locataire/DepartLocataireUI.cs"), "cession immédiate : la fiche société du nouveau titulaire tout de suite");
    }

    // ── D7a-17 : le départ avec remplaçant réserve le bâtiment et écrit ses charges en une fois ──

    [Test]
    public void D7a_17_le_remplacant_change_le_batiment_donc_le_depart_le_reserve()
    {
        string src = Src("Locataire/DepartLocataireUI.cs");
        StringAssert.Contains("EcritureCloud.Reserver(EcritureCloud.Cibles(fiche, true), \"départ\"", src, "bâtiment + locataire");
        StringAssert.Contains("if (!actif) { if (EnregistrementEnCours) continue;", Src("Cloud/EcritureCloud.cs"),
            "fenêtre fermée : le gardien garde le verrou jusqu'à ce que l'écriture du départ soit partie");
        int prepare = src.IndexOf("var nouveau = remplacant() ? AjouterRemplacant(fiche, loc) : null;", StringComparison.Ordinal);
        int ouvre = src.IndexOf("if (nouveau != null) OuvrirRemplacant(fiche, loc, nouveau);", StringComparison.Ordinal);
        Assert.That(prepare, Is.GreaterThan(0));
        Assert.That(ouvre, Is.GreaterThan(prepare));
        StringAssert.Contains("fiche.ApresDepart();", src.Substring(prepare, ouvre - prepare),
            "les parts du remplaçant sont posées avant l'écriture du départ, qui les emporte");
        StringAssert.DoesNotContain("CreerRemplacant", src, "plus de création après l'écriture");
    }

    // ── D7a-23 : réserver la fiche avant de copier un document du bail ─────────

    [Test]
    public void D7a_23_les_boutons_du_bail_reservent_avant_le_selecteur()
    {
        string src = Src("Locataire/BailFileUI.cs");
        StringAssert.Contains("EcritureCloud.Reserver(EcritureCloud.Cibles(_locatairePrefab, false), \"bail\"", src);
        StringAssert.Contains("Brancher(btnParcourir, () => Reserve(() =>", src, "« Ajouter le bail »");
        StringAssert.Contains("Brancher(_btnAvenant, () => Reserve(() =>", src, "« + Avenant »");
        StringAssert.Contains("Brancher(btnRetirer, () => Reserve(() => Modifier(", src, "« Retirer » du bail");
        StringAssert.Contains("Brancher(btnRet, () => Reserve(() => Modifier(retirer)))", src, "« Retirer » d'un avenant");
    }
}
