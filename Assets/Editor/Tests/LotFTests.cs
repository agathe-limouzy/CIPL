using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot F (interface, 16/11) : les règles pures ajoutées par le lot.
public class LotFTests
{
    static string SansInsecable(string s) => s.Replace('\u00A0', ' ').Replace('\u202F', ' ');

    [Test]
    public void B_02_un_montant_s_ecrit_a_la_francaise_quel_que_soit_le_pc()
    {
        Assert.That(SansInsecable(Montants.F(87419f, "N0")), Is.EqualTo("87 419"));
        Assert.That(Montants.F(73.6f, "F1"), Is.EqualTo("73,6"));
        Assert.That(SansInsecable(Montants.F(1250.5d, "N2")), Is.EqualTo("1 250,50"));
        Assert.That(Montants.F(0.5f, "0.##"), Is.EqualTo("0,5"));
    }

    [Test]
    public void Messagerie_la_permission_d_envoi_de_Google_est_exigee_et_le_fournisseur_nomme()
    {
        Assert.That(Messagerie.PermissionEnvoiManquante(Messagerie.Google, "email openid"), Is.True, "case « Envoyer des e-mails » décochée");
        Assert.That(Messagerie.PermissionEnvoiManquante(Messagerie.Google, "email https://www.googleapis.com/auth/gmail.send openid"), Is.False);
        Assert.That(Messagerie.PermissionEnvoiManquante(Messagerie.Google, null), Is.False, "pas de paramètre scope : on ne sait pas, comme avant");
        Assert.That(Messagerie.PermissionEnvoiManquante(Messagerie.Microsoft, "openid"), Is.False, "Microsoft : pas concerné");
        StringAssert.Contains("Envoyer des e-mails en votre nom", Messagerie.RefusPermissionEnvoi);

        string sansNom = Messagerie.Motif(new Messagerie.Reponse { refus = "retire" });
        StringAssert.Contains("L'autorisation de la messagerie a expiré", sansNom, "relevé le 14/11 : « L'autorisation de  a expiré »");
        StringAssert.Contains("L'autorisation de Google a expiré", Messagerie.Motif(new Messagerie.Reponse { refus = "retire" }, Messagerie.Google));
        StringAssert.StartsWith("Google n'a pas autorisé CIPL à envoyer",
            Messagerie.Clair(Messagerie.Google, 403, "{\"error\":{\"message\":\"Request had insufficient authentication scopes.\"}}"));
    }

    [Test]
    public void Depart_l_alerte_ne_cite_que_ce_qui_reste()
    {
        var loc = new Locataire { Name = "Lot F", depotDeGarantie = 1000f };
        Assert.That(DepartLocataire.ResteAvantDepart(loc), Is.EqualTo("état des lieux et décompte de sortie"));
        loc.sansEtatDesLieux = true;   // « Non, pas d'état des lieux » : réglé
        Assert.That(DepartLocataire.ResteAvantDepart(loc), Is.EqualTo("décompte de sortie"));
        loc.depotDeGarantie = 0f;      // sans dépôt, pas de décompte à faire : plus rien à prévoir, l'alerte se tait
        Assert.That(DepartLocataire.ResteAvantDepart(loc), Is.Null);
        loc.sansEtatDesLieux = false;
        Assert.That(DepartLocataire.ResteAvantDepart(loc), Is.EqualTo("état des lieux"));
    }

    [Test]
    public void D10_03_le_zoom_du_premier_lancement_suit_l_ecran()
    {
        Assert.That(UIZoom.PalierPourEcran(96f, 1920), Is.EqualTo(1f));
        Assert.That(UIZoom.PalierPourEcran(144f, 1920), Is.EqualTo(1.5f), "Windows à 150 %");
        Assert.That(UIZoom.PalierPourEcran(96f, 1366), Is.EqualTo(1.5f), "portable 1366 px");
        Assert.That(UIZoom.PalierPourEcran(96f, 2560), Is.EqualTo(0.9f));
        Assert.That(UIZoom.PalierPourEcran(0f, 1920), Is.EqualTo(1f), "ppp inconnu : 96");
        Assert.That(UIZoom.PalierPourEcran(96f, 0), Is.EqualTo(1f));
    }

    [Test]
    public void D10_02_Tab_suit_l_ordre_de_lecture_et_Entree_ne_confirme_qu_une_erreur()
    {
        // Deux lignes de deux champs, données dans le désordre : (droite haut), (gauche bas), (gauche haut, 3 px plus bas), (droite bas).
        var centres = new List<Vector2> { new Vector2(300, 500), new Vector2(100, 440), new Vector2(100, 497), new Vector2(300, 441) };
        Assert.That(ClavierUI.Ordre(centres, 12f), Is.EqualTo(new List<int> { 2, 0, 1, 3 }));
        Assert.That(ConfirmDialog.BoutonPour(erreur: true, entree: true), Is.True, "Entrée = OK d'une erreur");
        Assert.That(ConfirmDialog.BoutonPour(erreur: true, entree: false), Is.True, "Échap = OK d'une erreur");
        Assert.That(ConfirmDialog.BoutonPour(erreur: false, entree: true), Is.False, "Entrée ne confirme jamais une suppression ou un envoi");
    }

    [Test]
    public void BY4_Echap_annule_la_fenetre_et_Entree_enregistre_sans_jamais_envoyer()
    {
        Assert.That(ClavierUI.EstAnnuler("Annuler"), Is.True);
        Assert.That(ClavierUI.EstAnnuler("← Retour"), Is.True);
        Assert.That(ClavierUI.EstAnnuler(" Fermer "), Is.True);
        Assert.That(ClavierUI.EstAnnuler("Annuler le départ"), Is.False, "une action, pas une sortie");
        Assert.That(ClavierUI.EstEnregistrer("Enregistrer"), Is.True);
        Assert.That(ClavierUI.EstEnregistrer("Sauvegarder"), Is.True);
        Assert.That(ClavierUI.EstEnregistrer("Valider"), Is.True);
        Assert.That(ClavierUI.EstEnregistrer("Sauvegarder et envoyer"), Is.False, "Entrée n'envoie jamais");
        Assert.That(ClavierUI.EstEnregistrer("Enregistrer et envoyer par e-mail"), Is.False);
        Assert.That(ClavierUI.EstEnregistrer("Supprimer"), Is.False);
        Assert.That(ClavierUI.EstEnregistrer("Modifier"), Is.True, "Gestion du loyer");
        Assert.That(ClavierUI.EstEnregistrer("Modifier…"), Is.False, "le bouton d'une ligne de liste");
        Assert.That(ClavierUI.EstEnregistrer("Réinitialiser"), Is.False, "remet le loyer à zéro : à la souris");
    }

    [Test]
    public void Zoom_un_apercu_n_est_garde_que_par_Enregistrer()
    {
        float avant = UIZoom.Facteur;
        float autre = Mathf.Approximately(avant, 1.5f) ? 1.25f : 1.5f;
        UIZoom.Apercu(autre);
        Assert.That(UIZoom.Facteur, Is.EqualTo(autre), "l'aperçu se voit tout de suite");
        UIZoom.FinApercu();
        Assert.That(UIZoom.Facteur, Is.EqualTo(avant), "Réglages fermés sans « Enregistrer » : l'ancienne taille revient");
        string reglages = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "Script", "Facturation", "ReglagePanel.cs"));
        StringAssert.Contains("UIZoom.Apercu(valeur)", reglages, "le clic sur une taille n'enregistre plus");
        StringAssert.Contains("UIZoom.Garder();", reglages, "« Enregistrer » la garde");
        StringAssert.Contains("void OnDisable() { UIZoom.FinApercu();", reglages, "toute fermeture rend l'ancienne taille");
    }

    [Test]
    public void Essai_une_seule_entreprise_et_deux_licences()
    {
        Assert.That(RegleAbonnement.AjoutRefuse(RegleAbonnement.Etat.Essai), Is.True);
        Assert.That(RegleAbonnement.AjoutRefuse(RegleAbonnement.Etat.Actif), Is.False);
        Assert.That(RegleAbonnement.AjoutRefuse(RegleAbonnement.Etat.PaiementRefuse), Is.False, "comme avant");
        Assert.That(AbonnementCloud.Motif(new AbonnementCloud.Ajout { refus = "essai" }), Is.EqualTo(RegleAbonnement.RefusAjoutEssai));

        string sql = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", "Serveur", "etape17d_supabase.sql"));
        StringAssert.Contains("alter column licences_max set default 2", sql);
        int ajout = sql.IndexOf("function public.ajouter_entreprise", System.StringComparison.Ordinal);
        int import = sql.IndexOf("function public.creer_entreprise_import", System.StringComparison.Ordinal);
        Assert.That(ajout, Is.GreaterThan(0)); Assert.That(import, Is.GreaterThan(ajout));
        string fAjout = sql.Substring(ajout, import - ajout), fImport = sql.Substring(import);
        Assert.That(fAjout.IndexOf("'refus', 'essai'", System.StringComparison.Ordinal),
                    Is.InRange(1, fAjout.IndexOf("insert into public.entreprises", System.StringComparison.Ordinal)), "le refus précède la création");
        // Rien de perdu de la dernière définition (etape17a) : abonnement expiré, SIRET déjà pris, Réglages de départ, droits.
        foreach (var clause in new[] { "'refus', 'abonnement'", "'refus', 'deja_sur_cipl'", "insert into public.reglages", "insert into public.acces" })
            StringAssert.Contains(clause, fAjout);
        // creer_entreprise_import (etape17c) : adresse prouvée, groupe admin le plus ancien, puis la règle de l'essai.
        foreach (var clause in new[] { "adresse non prouvée par le code reçu", "order by (m.role = 'admin') desc, gr.cree_le limit 1",
                                       "une seule entreprise", "case when st = 'essai' then 2 else 1 end", "abonnement expiré : lecture seule" })
            StringAssert.Contains(clause, fImport);
        StringAssert.Contains("'ajouter_entreprise(uuid, text, text, text, jsonb)'", sql, "droits redonnés");
    }

    [Test]
    public void D10_01_une_fenetre_ne_depasse_pas_l_ecran_et_un_menu_y_revient()
    {
        Assert.That(PlafondFenetre.Hauteur(300f, 720f, 120f), Is.EqualTo(300f), "elle tient : sa taille");
        Assert.That(PlafondFenetre.Hauteur(700f, 617f, 114f), Is.EqualTo(463f), "175 % : écran − 40 − en-tête et pied");
        Assert.That(PlafondFenetre.Hauteur(700f, 100f, 114f), Is.EqualTo(120f), "jamais sous le minimum");
        Assert.That(UIFactory.DansEcran(1773f, 237f, 1920f, 6f), Is.EqualTo(1677f), "B-19 : ramené à gauche");
        Assert.That(UIFactory.DansEcran(400f, 237f, 1920f, 6f), Is.EqualTo(400f));
        Assert.That(UIFactory.DansEcran(-20f, 2500f, 1920f, 6f), Is.EqualTo(6f), "plus grand que l'écran : à la marge");
    }

    [Test]
    public void D3_16_le_tableau_des_utilisateurs_tient_a_175()
    {
        Assert.That(SectionUtilisateurs.LargeurMinimale(), Is.LessThanOrEqualTo(1011f), "1920 / 1,75 − 76");
    }

    [Test]
    public void D10_21_et_D10_17_annees_anciennes_du_suivi_et_largeur_des_creances()
    {
        var loc = new Locataire { Name = "Lot F" };
        loc.facturesEtat.Add(new FactureEtat { key = "regul-2022", echeanceISO = "2022-06-30" });
        loc.facturesEtat.Add(new FactureEtat { key = "loyer-2020-P1", echeanceISO = "2020-01-05" });
        loc.facturesEtat.Add(new FactureEtat { key = "loyer-2020-P2", echeanceISO = "2020-04-05" });
        loc.facturesEtat.Add(new FactureEtat { key = "loyer-2024-P1", echeanceISO = "2024-01-05" });
        Assert.That(LocataireSuiviInline.AnneesAnciennes(loc, 2026), Is.EqualTo(new List<int> { 2022, 2020 }), "avant 2023, la plus récente d'abord");
        Assert.That(LocataireSuiviInline.AnneesAnciennes(new Locataire(), 2026), Is.Empty, "sans facture ancienne : pas de menu");
        Assert.That(FacturationHomeSection.LargeurMin, Is.GreaterThanOrEqualTo(735f), "la rangée des filtres tient dans la carte");
    }

    // Demandes du 23/11 (compte neuf en essai) : « - » au minimum et « Inviter » sans licence libre ne disaient rien.
    [Test]
    public void Licences_la_baisse_et_l_invitation_refusees_sont_dites()
    {
        var pleine = new UtilisateursCloud.Entreprise { id = "a", nom = "SCI A", licences_max = 2, licences_utilisees = 2 };
        var libre = new UtilisateursCloud.Entreprise { id = "b", nom = "SCI B", licences_max = 3, licences_utilisees = 1 };
        Assert.That(UtilisateursCloud.RefusInvitation(new[] { pleine, libre }, true), Is.Null, "une licence libre quelque part : le formulaire s'ouvre");
        StringAssert.StartsWith("L'essai gratuit comprend 2 utilisateurs", UtilisateursCloud.RefusInvitation(new[] { pleine }, true));
        StringAssert.StartsWith("Aucune licence libre", UtilisateursCloud.RefusInvitation(new[] { pleine }, false));
        Assert.That(UtilisateursCloud.RefusInvitation(null, true), Is.Null, "liste illisible : le serveur décidera");
        StringAssert.StartsWith("SCI A : 2 licences utilisées. Retirez quelqu'un de SCI A", UtilisateursCloud.RefusBaisse(pleine));

        string dossier = System.IO.Path.Combine(Application.dataPath, "Script", "Cloud");
        string abo = System.IO.File.ReadAllText(System.IO.Path.Combine(dossier, "SectionAbonnement.cs"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(abo, @"ConfirmDialog\.Erreur\(UtilisateursCloud\.RefusBaisse\(e\)\)").Count, Is.EqualTo(2),
                    "au choix de l'abonnement comme pour un abonnement en cours");
        StringAssert.Contains("}, \" à l'abonnement\");", abo, "au choix : « licences à l'abonnement »");
        StringAssert.Contains("UtilisateursCloud.RefusInvitation(_a?.entreprises", System.IO.File.ReadAllText(System.IO.Path.Combine(dossier, "SectionUtilisateurs.cs")));
    }
}
