using System.Linq;
using NUnit.Framework;
using UnityEngine;
using L = PremiersPas.Ligne;

/// Étape 13b : la carte « Premiers pas » et la visite guidée (décisions du 25/10) ; 13b-2 : la
/// prochaine étape, les guides pas à pas et la place des bulles.
public class CloudPremiersPasTests
{
    [Test]
    public void L_admin_a_huit_lignes_un_collegue_les_trois_qu_il_peut_faire()
    {
        var admin = PremiersPas.Lignes(new PremiersPas.Etat { admin = true, joursEssai = 11 });
        Assert.That(admin.Select(x => x.ligne), Is.EqualTo(new[] { L.Logo, L.Rib, L.Envoi, L.Batiment, L.Locataire, L.Facture, L.Inviter, L.Abonnement }));
        Assert.That(admin.Last().texte, Is.EqualTo("Choisir l'abonnement"));
        Assert.That(admin.Last().sous, Is.EqualTo("Essai : 11 jours restants"));
        Assert.That(admin.All(x => x.sous.Length > 0), Is.True, "chaque ligne dit à quoi elle sert");

        var collegue = PremiersPas.Lignes(new PremiersPas.Etat { admin = false });
        Assert.That(collegue.Select(x => x.ligne), Is.EqualTo(new[] { L.Batiment, L.Locataire, L.Facture }));
    }

    [Test]
    public void Les_lignes_se_cochent_d_apres_les_donnees_et_la_carte_part_quand_tout_est_fait()
    {
        var e = new PremiersPas.Etat { admin = true, logo = true, batiments = 2, locataires = 1, joursEssai = 1 };
        var l = PremiersPas.Lignes(e);
        Assert.That(l.Where(x => x.fait).Select(x => x.ligne), Is.EqualTo(new[] { L.Logo, L.Batiment, L.Locataire }));
        Assert.That(PremiersPas.Titre(l), Is.EqualTo("Premiers pas · 3 / 8"));
        Assert.That(l.Last().sous, Is.EqualTo("Essai : 1 jour restant"));
        Assert.That(PremiersPas.Visible(l, masquee: false), Is.True);
        Assert.That(PremiersPas.Visible(l, masquee: true), Is.False, "« Masquer »");

        var tout = PremiersPas.Lignes(new PremiersPas.Etat
        {
            admin = true, logo = true, rib = true, envoi = true, batiments = 1, locataires = 1, facture = true, autrePersonne = true, abonne = true,
        });
        Assert.That(PremiersPas.Visible(tout, masquee: false), Is.False, "tout est fait");
        Assert.That(tout.Last().sous, Is.EqualTo("Abonné"));
    }

    [Test]
    public void La_prochaine_etape_est_la_premiere_pas_faite()
    {
        var l = PremiersPas.Lignes(new PremiersPas.Etat { admin = true, logo = true, rib = true });
        Assert.That(PremiersPas.Prochaine(l), Is.EqualTo(L.Envoi));
        Assert.That(PremiersPas.Prochaine(PremiersPas.Lignes(new PremiersPas.Etat { batiments = 1 })), Is.EqualTo(L.Locataire), "un collègue");
        Assert.That(PremiersPas.Prochaine(PremiersPas.Lignes(new PremiersPas.Etat { batiments = 1, locataires = 1, facture = true })), Is.Null);
        foreach (L x in System.Enum.GetValues(typeof(L)))
            Assert.That(PremiersPas.Phrase(x), Is.Not.Empty, x + " : la phrase du cadre « Prochaine étape »");
    }

    [Test]
    public void Chaque_ligne_a_son_guide_pas_a_pas()
    {
        foreach (L x in System.Enum.GetValues(typeof(L)))
            Assert.That(PremiersPas.AUnGuide(x), Is.True, x + " (13b-2 : Réglages ; 13b-3 : bâtiment, locataire, facture)");
    }

    [Test]
    public void La_visite_a_cinq_bulles_et_finit_par_c_est_parti()
    {
        Assert.That(VisiteGuidee.Etapes.Select(x => x.titre),
                    Is.EqualTo(new[] { "Bienvenue dans CIPL", "Vos bâtiments", "Créances", "Réglages et compte", "C'est parti" }));
        Assert.That(VisiteGuidee.Etapes.All(x => x.texte.Length > 0), Is.True);
    }

    [Test]
    public void La_bulle_se_place_a_droite_sinon_dessous_sinon_dessus_et_reste_dans_l_ecran()
    {
        var ecran = new Rect(-960, -540, 1920, 1080);
        var taille = new Vector2(380, 200);

        var aGauche = new Rect(-900, 0, 200, 40);   // place à droite
        Assert.That(Bulles.Placer(aGauche, taille, ecran).x, Is.EqualTo(-700 + 12 + 190).Within(0.01f));

        var large = new Rect(-950, 100, 1900, 60);   // pas de place à droite : dessous
        var p = Bulles.Placer(large, taille, ecran);
        Assert.That(p.y, Is.EqualTo(100 - 12 - 100).Within(0.01f));
        Assert.That(p.x + 190, Is.EqualTo(ecran.xMax - 8).Within(0.01f), "zone de toute la largeur : à droite, dans l'écran");

        var enBas = new Rect(-950, -530, 1900, 60);   // ni à droite ni dessous : dessus
        Assert.That(Bulles.Placer(enBas, taille, ecran).y, Is.EqualTo(-470 + 12 + 100).Within(0.01f));

        Assert.That(Bulles.Placer(null, taille, ecran), Is.EqualTo(ecran.center), "sans zone : au centre");
    }

    [Test]
    public void Un_RIB_faux_ou_incomplet_est_refuse_avec_sa_zone()
    {
        const string iban = "FR76 3000 6000 0112 3456 7890 189";   // IBAN fictif, clé juste
        Assert.That(RegleRib.IbanValide(iban), Is.True);
        Assert.That(RegleRib.IbanValide("fr7630006000011234567890189"), Is.True, "sans espaces, en minuscules");
        Assert.That(RegleRib.IbanValide("FR76 3000 6000 0112 3456 7890 188"), Is.False, "mauvaise clé");
        Assert.That(RegleRib.IbanValide("FR76 3000 6000 0112"), Is.False, "trop court");
        Assert.That(RegleRib.RibDepuisIban(iban), Is.EqualTo("30006 00001 12345678901 89"));

        Assert.That(RegleRib.Refus("SCI (JBC)", "30006 00001 12345678901 89", iban, "AGRIFRPP", out _), Is.Null);
        Assert.That(RegleRib.Refus("SCI (JBC)", "", iban, "", out _), Is.Null, "RIB et BIC facultatifs");

        Assert.That(RegleRib.Refus(" ", "", iban, "", out var c1), Does.StartWith("Saisissez le titulaire"));
        Assert.That(c1, Is.EqualTo(new[] { RegleRib.ChampTitulaire }));
        Assert.That(RegleRib.Refus("SCI", "", "FR76 1234", "", out var c2), Does.StartWith("Cet IBAN n'est pas valide"));
        Assert.That(c2, Is.EqualTo(new[] { RegleRib.ChampIban }));
        Assert.That(RegleRib.Refus("SCI", "", iban, "AGRI", out var c3), Does.StartWith("Le BIC compte 8 ou 11"));
        Assert.That(c3, Is.EqualTo(new[] { RegleRib.ChampBic }));
        Assert.That(RegleRib.Refus("SCI", "30004 00747 00021009833 38", iban, "", out var c4), Does.StartWith("Le n° de RIB ne correspond pas"));
        Assert.That(c4, Is.EqualTo(new[] { RegleRib.ChampRib, RegleRib.ChampIban }));
    }

    [Test]
    public void Le_batiment_d_exemple_est_facturable_et_rien_n_en_part()
    {
        var b = BatimentExemple.Construire(new System.DateTime(2026, 10, 25));
        Assert.That(BatimentExemple.Est(b), Is.True);
        Assert.That(b.Name, Is.EqualTo(BatimentExemple.Nom));
        var l = b.locataireDuBatiment.Single();
        Assert.That(ParcoursLocataire.Prochaine(l), Is.EqualTo(ParcoursLocataire.Etape.Termine), "général, bail, loyer, dépôt : facturable tout de suite");
        Assert.That(l.dateDebutBailISO, Is.EqualTo("2026-10-01"), "facturé à partir de ce mois : pas de loyers en retard ni d'alertes");
        Assert.That(l.emailLocataire, Is.Null.Or.Empty, "sans adresse, aucune facture ne peut partir");
        Assert.That(BatimentExemple.Est(JsonUtility.FromJson<Batiment>(JsonUtility.ToJson(b))), Is.True, "le drapeau survit à l'enregistrement");
        Assert.That(BatimentExemple.Est(new Batiment()), Is.False, "un bâtiment ordinaire");
    }

    [Test]
    public void Les_choix_de_la_personne_vont_dans_son_compte()
    {
        Assert.That(CloudClient.CorpsMetadonnee(SessionCloud.VisiteVue), Is.EqualTo("{\"data\":{\"cipl_visite_vue\":true}}"));
        Assert.That(CloudClient.CorpsMetadonnee(SessionCloud.PremiersPasMasques, false), Is.EqualTo("{\"data\":{\"cipl_premiers_pas_masques\":false}}"),
                    "« Réafficher les premiers pas » (retour du 25/10)");
        Assert.That(SessionCloud.PremiersPasMasques, Is.EqualTo("cipl_premiers_pas_masques"));

        var u = JsonUtility.FromJson<CloudClient.Utilisateur>("{\"id\":\"u1\",\"email\":\"a@b.fr\",\"user_metadata\":{\"cipl_visite_vue\":true}}");
        Assert.That(u.user_metadata.cipl_visite_vue, Is.True, "relu de la réponse de Supabase");
        Assert.That(u.user_metadata.cipl_premiers_pas_masques, Is.False);
    }
}
