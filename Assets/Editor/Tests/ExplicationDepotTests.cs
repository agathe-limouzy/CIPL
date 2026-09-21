using System;
using NUnit.Framework;

/// Le bloc explicatif imprimé sur la facture de révision du dépôt de garantie.
/// Ce texte part chez le locataire : le sens de la dette et les libellés comptent
/// autant que les chiffres.
public class ExplicationDepotTests
{
    static readonly DateTime Effet = new DateTime(2025, 7, 1);

    // Les phrases vivent sur la FACTURE, plus dans les réglages : ces tests posent
    // donc un `FactureInfo` local et ne touchent plus rien de global. Plus besoin
    // d'isoler le reglage.json de la machine — l'ancien SetUp/TearDown qui le
    // sauvegardait est parti avec les champs qu'il protégeait.

    static Locataire LocataireRevise() => new Locataire
    {
        Name = "SARL DEMO",
        loyerAnnuelPrecedent = 55800f,
        loyerAnnuel = 57490.78f,
        indiceImmoAuDepart = "133.66  (2023-T3)",
        indiceImmoActuel = "137.71  (2024-T3)",
        trimestreDeRevision = "2023-T3",
        depotSurTTC = false
    };

    // ── Sens de la dette ────────────────────────────────────────────────────

    [Test]
    public void Complement_positif_le_locataire_nous_doit()
    {
        string html = ExplicationDepot.Html(LocataireRevise(), 2, 281.80f, Effet);

        Assert.That(html, Does.Contain("vous nous devez"));
        Assert.That(html, Does.Not.Contain("nous vous devons"));
        Assert.That(html, Does.Contain("281,80 €"));
    }

    [Test]
    public void Complement_negatif_c_est_nous_qui_devons()
    {
        // Nouveau dépôt inférieur à l'ancien : le sens s'inverse et le montant
        // s'affiche en positif — « nous vous devons : -281,80 € » n'aurait aucun sens.
        string html = ExplicationDepot.Html(LocataireRevise(), 2, -281.80f, Effet);

        Assert.That(html, Does.Contain("nous vous devons"));
        Assert.That(html, Does.Not.Contain("vous nous devez"));
        Assert.That(html, Does.Contain("281,80 €"));
        Assert.That(html, Does.Not.Contain("-281,80"));
        Assert.That(html, Does.Contain("rembours"));
    }

    [TestCase(0f)]
    [TestCase(0.004f)]
    [TestCase(-0.004f)]
    public void Complement_nul_ou_sous_le_centime_n_appelle_aucun_reglement(float complement)
    {
        // Sans seuil, un arrondi flottant ferait réclamer « 0,00 € ».
        string html = ExplicationDepot.Html(LocataireRevise(), 2, complement, Effet);

        Assert.That(html, Does.Contain("aucun ajustement"));
        Assert.That(html, Does.Not.Contain("vous nous devez"));
        Assert.That(html, Does.Not.Contain("nous vous devons"));
    }

    // ── Phrases réglables ───────────────────────────────────────────────────

    [Test]
    public void Une_phrase_reformulee_sur_la_facture_remplace_le_texte_d_usine()
    {
        // Le cas demandé à l'origine : passer « vous nous devez » en minuscules.
        // Si ce test tombe, c'est que le texte est resté écrit en dur.
        var depot = new FactureInfo
        {
            depotDu = "et ainsi, il ressort que vous nous devez : {depot.montant} — merci de nous régler."
        };

        string html = ExplicationDepot.Html(LocataireRevise(), 2, 281.80f, Effet, null, depot);

        Assert.That(html, Does.Contain("et ainsi, il ressort que vous nous devez"));
        Assert.That(html, Does.Contain("merci de nous régler"));
        Assert.That(html, Does.Not.Contain("par retour de courrier"), "l'ancien texte ne doit plus sortir");
    }

    [Test]
    public void Chaque_facture_garde_ses_propres_phrases()
    {
        // Le fond de la demande : ce n'est plus un texte unique partagé.
        var a = new FactureInfo { depotDu = "Facture A : {depot.montant}" };
        var b = new FactureInfo { depotDu = "Facture B : {depot.montant}" };

        string htmlA = ExplicationDepot.Html(LocataireRevise(), 2, 281.80f, Effet, null, a);
        string htmlB = ExplicationDepot.Html(LocataireRevise(), 2, 281.80f, Effet, null, b);

        Assert.That(htmlA, Does.Contain("Facture A").And.Not.Contain("Facture B"));
        Assert.That(htmlB, Does.Contain("Facture B").And.Not.Contain("Facture A"));
    }

    [Test]
    public void Les_variables_du_depot_sont_remplacees()
    {
        var depot = new FactureInfo
        {
            depotRappel = "[{depot.nb}|{depot.termes}|{depot.base}] pour {loc.nom}",
            depotDu = "montant : {depot.montant}"
        };

        string html = EspacesNormalisees(
            ExplicationDepot.Html(LocataireRevise(), 2, 281.80f, Effet, null, depot));

        Assert.That(html, Does.Contain("[deux|deux termes|H.T.] pour SARL DEMO"));
        Assert.That(html, Does.Contain("montant : 281,80 €"));
        Assert.That(html, Does.Not.Contain("{depot."), "aucune variable ne doit rester en clair");
        Assert.That(html, Does.Not.Contain("{loc."));
    }

    [Test]
    public void Le_montant_est_toujours_positif_meme_sur_un_remboursement()
    {
        // La phrase dit déjà « nous vous devons » : réimprimer un signe moins
        // ferait lire « nous vous devons -281,80 € », soit l'inverse du sens voulu.
        var depot = new FactureInfo { depotRembourse = "à rembourser : {depot.montant}" };

        string html = EspacesNormalisees(
            ExplicationDepot.Html(LocataireRevise(), 2, -281.80f, Effet, null, depot));

        Assert.That(html, Does.Contain("à rembourser : 281,80 €"));
        Assert.That(html, Does.Not.Contain("-281,80"));
    }

    [Test]
    public void Sans_facture_ou_avec_une_phrase_vide_le_texte_d_usine_s_imprime()
    {
        // Non-régression : les factures antérieures n'ont aucune de ces phrases,
        // leur document doit sortir exactement comme avant.
        string sansFacture = ExplicationDepot.Html(LocataireRevise(), 2, 281.80f, Effet);
        Assert.That(sansFacture, Does.Contain("par retour de courrier"));

        var vide = new FactureInfo { depotDu = "   " };
        string avecVide = ExplicationDepot.Html(LocataireRevise(), 2, 281.80f, Effet, null, vide);

        Assert.That(avecVide, Does.Contain("par retour de courrier"),
                    "un champ laissé vide retombe sur le texte d'usine");
        Assert.That(avecVide, Does.Contain("Dépôt de garantie"));
        Assert.That(avecVide, Does.Not.Contain("{depot."));
    }

    // ── Tableau d'indexation ────────────────────────────────────────────────

    /// La culture fr-FR sépare les milliers par un espace INSÉCABLE (U+00A0 ou U+202F
    /// selon la plateforme). Un test qui cherche « 55 800,00 € » avec un espace
    /// ordinaire échoue alors que la sortie est correcte.
    static string EspacesNormalisees(string s)
        => (s ?? "").Replace(' ', ' ').Replace(' ', ' ');

    [Test]
    public void Le_tableau_reprend_les_indices_et_les_deux_loyers()
    {
        string html = EspacesNormalisees(ExplicationDepot.Html(LocataireRevise(), 2, 281.80f, Effet));

        Assert.That(html, Does.Contain("3ème trimestre 2023"), "trimestre de l'indice de base");
        Assert.That(html, Does.Contain("3ème trimestre 2024"), "trimestre du nouvel indice");
        Assert.That(html, Does.Contain("133,66").And.Contain("137,71"));
        Assert.That(html, Does.Contain("55 800,00 €").And.Contain("57 490,78 €"));
        Assert.That(html, Does.Contain("4 790,90 €"), "loyer mensuel = annuel / 12");
        Assert.That(html, Does.Contain("1er juillet 2025"), "et non « 1 juillet »");
    }

    [Test]
    public void Sans_revision_le_tableau_n_est_pas_imprime()
    {
        // Locataire neuf : pas d'indice précédent, pas de loyer antérieur. Un tableau
        // à zéro serait pire que pas de tableau du tout.
        var neuf = new Locataire { Name = "NOUVEAU", loyerAnnuel = 12000f, depotSurTTC = true };
        string html = ExplicationDepot.Html(neuf, 3, 500f, Effet);

        Assert.That(html, Does.Not.Contain("Nouvel indice"));
        Assert.That(html, Does.Not.Contain("Base Loyer Annuel"));
        Assert.That(html, Does.Contain("Dépôt de garantie"), "la partie dépôt reste imprimée");
        Assert.That(html, Does.Contain("trois termes"));
    }

    // ── Libellés ────────────────────────────────────────────────────────────

    [Test]
    public void La_base_de_calcul_suit_la_fiche()
    {
        var loc = LocataireRevise();
        Assert.That(ExplicationDepot.Html(loc, 2, 100f, Effet), Does.Contain("H.T."));

        loc.depotSurTTC = true;
        Assert.That(ExplicationDepot.Html(loc, 2, 100f, Effet), Does.Contain("T.T.C."));
    }

    [Test]
    public void Un_seul_terme_reste_au_singulier()
    {
        string html = ExplicationDepot.Html(LocataireRevise(), 1, 100f, Effet);
        Assert.That(html, Does.Contain("un terme de loyer"));
        Assert.That(html, Does.Not.Contain("un termes"));
    }

    [TestCase("2023-T1", "1er trimestre 2023")]
    [TestCase("2023-T2", "2ème trimestre 2023")]
    [TestCase("2024-T4", "4ème trimestre 2024")]
    public void Les_trimestres_sont_ecrits_en_clair(string periode, string attendu)
    {
        Assert.That(ExplicationDepot.TrimestreEnClair(periode), Is.EqualTo(attendu));
    }

    [Test]
    public void Une_periode_illisible_est_recopiee_telle_quelle()
    {
        // Mieux vaut une période brute qu'une case vide sur un document client.
        Assert.That(ExplicationDepot.TrimestreEnClair("T3"), Is.EqualTo("T3"));
        Assert.That(ExplicationDepot.TrimestreEnClair(null), Is.Empty);
    }

    [TestCase(1, "un")]
    [TestCase(2, "deux")]
    [TestCase(12, "douze")]
    [TestCase(13, "13")]
    public void Le_nombre_de_termes_s_ecrit_en_lettres_jusqu_a_douze(int n, string attendu)
    {
        Assert.That(ExplicationDepot.EnLettres(n), Is.EqualTo(attendu));
    }

    [Test]
    public void Un_locataire_absent_ne_produit_rien()
    {
        Assert.That(ExplicationDepot.Html(null, 2, 100f, Effet), Is.Empty);
    }
}
