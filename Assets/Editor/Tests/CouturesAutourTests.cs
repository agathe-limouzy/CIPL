using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot « coutures », piste B (10/11/2026) : autour des formulaires de facture (fenêtre des versements, fenêtre de
/// confirmation, SIREN du locataire, menu « / », textes d'usine, mention TVA par défaut). Un test par défaut corrigé, règle
/// pure d'abord, texte du code à défaut.
public class CouturesAutourTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));
    static int Fois(string src, string fragment) => src.Split(new[] { fragment }, StringSplitOptions.None).Length - 1;

    // ── D4-12 : « Paiement partiel… » et « Remboursé… » réservent le locataire et son bâtiment ──

    /// Les deux fenêtres ne se construisent que par EcritureCloud.Reserver (locataire + bâtiment : « Payé » écrit les charges) :
    /// fiche prise → refus à l'ouverture ; fenêtre ouverte → la relecture en direct attend et la fermeture de CIPL la voit.
    [Test]
    public void D4_12_les_fenetres_des_versements_reservent_le_locataire_et_le_batiment()
    {
        string src = Src("Facturation/VersementsPanel.cs");
        StringAssert.Contains("EcritureCloud.Reserver(EcritureCloud.Cibles(fiche, true), motif,", src, "le bâtiment aussi");
        StringAssert.Contains("() => scrim != null, () => { if (scrim != null) UnityEngine.Object.Destroy(scrim.gameObject); });", src,
            "réservée tant que la fenêtre existe ; fermée sans enregistrer si la fiche est perdue");
        StringAssert.Contains("Reserver(fiche, \"paiement\", () => Fenetre(fiche, ligne, apres));", src, "Ouvrir");
        StringAssert.Contains("Reserver(fiche, \"remboursement\", () => FenetreRemboursement(fiche, ligne, apres));", src, "Rembourser");
        Assert.That(Fois(src, "Fenetre(fiche, ligne, apres)"), Is.EqualTo(1), "« Paiement partiel… » ne s'ouvre que réservée");
        Assert.That(Fois(src, "FenetreRemboursement(fiche, ligne, apres)"), Is.EqualTo(1), "« Remboursé… » ne s'ouvre que réservée");
        StringAssert.DoesNotContain("public static RectTransform Fenetre", src, "aucun autre écran ne les construit sans réservation");
        Assert.That(typeof(VersementsPanel).GetMethod("Ouvrir").GetParameters().Select(p => p.ParameterType),
            Is.EqualTo(new[] { typeof(LocatairePrefab), typeof(FactureEtat), typeof(Action) }), "le suivi appelle comme avant");
    }

    // ── D8-02 : une confirmation arrivée pendant un message d'erreur ne perd pas sa suite ──

    /// Fenêtre de confirmation montée comme dans la scène (deux textes, deux boutons). Awake ne tourne pas hors Play : appelé ici.
    static ConfirmDialog Dialogue(GameObject go)
    {
        var d = go.AddComponent<ConfirmDialog>();
        d.txtTitre = UIFactory.Text(go.transform, "", UITheme.Role.Libelle);
        d.txtMessage = UIFactory.Text(go.transform, "", UITheme.Role.Libelle);
        d.btnConfirmer = UIFactory.Button(go.transform, "Supprimer", Color.white, Color.black);
        d.btnAnnuler = UIFactory.Button(go.transform, "Annuler", Color.white, Color.black);
        typeof(ConfirmDialog).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(d, null);
        return d;
    }

    [Test]
    public void D8_02_une_confirmation_pendant_une_erreur_ne_perd_pas_sa_suite()
    {
        var instance = typeof(ConfirmDialog).GetProperty("Instance");
        var avant = instance.GetValue(null);
        var go = new GameObject("ConfirmDialogCouturesB", typeof(RectTransform));
        try
        {
            var d = Dialogue(go);
            int suites = 0, confirmes = 0;

            // Perte de verrou : le message attend son « OK » ; la croix de la fenêtre ouvre une confirmation par-dessus.
            ConfirmDialog.Erreur("CA-B fiche reprise par un collègue.", () => suites++);
            Assert.That(d.txtMessage.text, Is.EqualTo("CA-B fiche reprise par un collègue."));
            d.Show("Quitter", "CA-B modifications non enregistrées.", () => confirmes++, "Quitter");
            Assert.That(d.txtMessage.text, Is.EqualTo("CA-B modifications non enregistrées."), "la confirmation passe devant");
            Assert.That(d.btnAnnuler.gameObject.activeSelf, Is.True);

            d.btnAnnuler.onClick.Invoke();
            Assert.That(go.activeSelf, Is.True, "l'erreur revient après la confirmation");
            Assert.That(d.txtMessage.text, Is.EqualTo("CA-B fiche reprise par un collègue."));
            Assert.That(d.btnAnnuler.gameObject.activeSelf, Is.False, "un message d'erreur n'a que « OK »");
            Assert.That((suites, confirmes), Is.EqualTo((0, 0)), "rien n'est exécuté avant le « OK »");

            d.btnConfirmer.onClick.Invoke();
            Assert.That((suites, confirmes), Is.EqualTo((1, 0)), "la suite de l'erreur s'exécute, une seule fois");
            Assert.That(go.activeSelf, Is.False, "plus rien à montrer");

            // La confirmation acceptée : son action, puis l'erreur revient quand même.
            ConfirmDialog.Erreur("CA-B enregistrement refusé.", () => suites++);
            d.Show("Continuer ?", "CA-B question.", () => confirmes++);
            d.btnConfirmer.onClick.Invoke();
            Assert.That((suites, confirmes), Is.EqualTo((1, 1)));
            Assert.That(d.txtMessage.text, Is.EqualTo("CA-B enregistrement refusé."));
            d.btnConfirmer.onClick.Invoke();
            Assert.That((suites, confirmes), Is.EqualTo((2, 1)));

            // Sans erreur affichée, une confirmation annulée ne fait rien revenir.
            d.Show("Supprimer ?", "CA-B question seule.", () => confirmes++);
            d.btnAnnuler.onClick.Invoke();
            Assert.That(go.activeSelf, Is.False);
            Assert.That((suites, confirmes), Is.EqualTo((2, 1)));
        }
        finally
        {
            instance.SetValue(null, avant);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    // ── D5-22 : un SIREN seulement pour 9 ou 14 chiffres ──

    [Test]
    public void D5_22_un_telephone_ou_un_numero_de_tva_n_est_pas_un_siren()
    {
        Assert.That(SocieteLocataire.Siren("FR 44 384 826 971"), Is.Empty, "n° de TVA : 11 chiffres");
        Assert.That(SocieteLocataire.Siren("06 07 04 69 28"), Is.Empty, "téléphone : 10 chiffres");
        Assert.That(SocieteLocataire.Siren("FR76 1234 5678 9012 3456 7890 123"), Is.Empty, "IBAN");
        Assert.That(SocieteLocataire.Siren("12345"), Is.Empty, "tronqué : plus les chiffres tels quels");
        Assert.That(SocieteLocataire.Siren(null), Is.Empty);
        Assert.That(SocieteLocataire.Siren("552 032 534 00646"), Is.EqualTo("552032534"), "SIRET : 14 chiffres");
        Assert.That(SocieteLocataire.Siren("552 032 534"), Is.EqualTo("552032534"), "SIREN : 9 chiffres");
        Assert.That(SocieteLocataire.Siren("00000000200000"), Is.EqualTo("000000002"), "le SIRET de test du bac à sable Super PDP reste lu");

        // Ce qui en dépend : un texte douteux vaut « pas de SIRET » (la ligne « Envoi » le réclame), jamais une erreur.
        var apresBascule = new DateTime(2027, 9, 16);
        Assert.That(FactureElectronique.DoitPartirParPlateforme(240f, "FR 44 384 826 971", apresBascule), Is.False);
        Assert.That(FactureElectronique.DoitPartirParPlateforme(240f, "552 032 534 00646", apresBascule), Is.True);
        Assert.That(FactureElectronique.Professionnel(new Locataire { siretNumber = "06 07 04 69 28" }, ""), Is.False);
        Assert.That(FactureElectronique.Professionnel(new Locataire(), "FR 44 384 826 971"), Is.False, "tapé dans le formulaire");
        Assert.That(FactureElectronique.RaisonHorsPlateforme(new Locataire { tvaLoyer = "oui" }, 20f, "FR 44 384 826 971"),
            Does.Contain("pas de SIRET"));
        Assert.That(PappersResume.SiretExploitable("FR 44 384 826 971"), Is.False, "ni Pappers ni le BODACC ne sont interrogés");
    }

    // ── D6-26 : le menu « / » s'ouvre au-dessus du champ quand la place manque en bas ──

    [Test]
    public void D6_26_le_menu_des_variables_se_place_comme_un_menu_deroulant()
    {
        string src = Src("Facturation/SlashAutocomplete.cs");
        StringAssert.Contains("UIFactory.PlacePopup(popup, inputRT);", src, "sous le champ, ou au-dessus");
        StringAssert.DoesNotContain("popup.position = corners[0];", src, "plus jamais d'office sous le champ");
        StringAssert.Contains("c.gameObject.SetActive(false); Destroy(c.gameObject);", src, "la hauteur mesurée ne compte pas les anciennes lignes");

        // Ce que PlacePopup fait d'un menu ancré en bas à gauche, comme celui du « / » : champ près du bas → au-dessus.
        var racine = new GameObject("RacineCouturesB", typeof(RectTransform));
        try
        {
            ((RectTransform)racine.transform).sizeDelta = new Vector2(800f, 600f);
            var champ = UIFactory.Rect("Champ", racine.transform);
            champ.anchorMin = champ.anchorMax = new Vector2(.5f, .5f);
            champ.sizeDelta = new Vector2(280f, 44f);
            var menu = UIFactory.Rect("Menu", racine.transform);
            menu.anchorMin = menu.anchorMax = Vector2.zero;
            menu.sizeDelta = new Vector2(280f, 240f);
            var c = new Vector3[4]; var m = new Vector3[4];

            champ.anchoredPosition = new Vector2(0f, -250f);   // 28 px sous le champ : la liste n'y tient pas
            UIFactory.PlacePopup(menu, champ);
            champ.GetWorldCorners(c); menu.GetWorldCorners(m);
            Assert.That(Vector3.Distance(m[0], c[1]), Is.LessThan(0.5f), "le bas du menu est posé sur le haut du champ");

            champ.anchoredPosition = new Vector2(0f, 200f);    // en haut de l'écran : dessous, comme avant
            UIFactory.PlacePopup(menu, champ);
            champ.GetWorldCorners(c); menu.GetWorldCorners(m);
            Assert.That(Vector3.Distance(m[1], c[0]), Is.LessThan(0.5f), "le haut du menu est posé sous le champ");
        }
        finally { UnityEngine.Object.DestroyImmediate(racine); }
    }

    // ── D6-25 : {loyer.ht}, {loyer.tva}, {loyer.ttc} sont les totaux de la facture ──

    [Test]
    public void D6_25_les_totaux_de_la_facture_ne_s_appellent_plus_loyer()
    {
        string Libelle(string jeton) => FactureVariables.All.Single(v => v.token == jeton).label;
        Assert.That(Libelle("{loyer.ht}"), Is.EqualTo("Total HT de la facture"));
        Assert.That(Libelle("{loyer.tva}"), Is.EqualTo("TVA de la facture"));
        Assert.That(Libelle("{loyer.ttc}"), Is.EqualTo("Total TTC de la facture"));
        foreach (string jeton in new[] { "{loyer.ht}", "{loyer.tva}", "{loyer.ttc}" })
            Assert.That(Libelle(jeton).ToLowerInvariant(), Does.Not.Contain("loyer"), jeton + " : le total du document, pas le loyer seul");
        Assert.That(SlashAutocomplete.Proposees("total").Select(v => v.token), Is.EquivalentTo(new[] { "{loyer.ht}", "{loyer.ttc}" }),
            "le menu « / » les trouve par leur nouveau nom");
    }

    // ── D6-27 : la phrase de retard d'usine cite l'article L441-10 ──

    [Test]
    public void D6_27_la_phrase_de_retard_d_usine_cite_le_bon_article()
    {
        string usine = new ReglageData().phraseRetard;
        StringAssert.Contains("(art. L441-10 du Code de commerce)", usine);
        StringAssert.DoesNotContain("441-6", usine);
        Assert.That(JsonUtility.FromJson<ReglageData>("{\"phraseRetard\":\"Ma phrase (Art L 441-6 du C.com).\"}").phraseRetard,
            Is.EqualTo("Ma phrase (Art L 441-6 du C.com)."), "des Réglages déjà enregistrés gardent leur phrase");
    }

    // ── D6-12 : la phrase « montant mensuel » est échappée dans le HTML ──

    [Test]
    public void D6_12_la_phrase_du_montant_mensuel_est_echappee()
    {
        string html = FacturePdfService.BuildHtml(new FacturePdfService.Data
            { ttc = 3600f, afficherMensuel = true, montantMensuel = 1200f, texteMensuel = "Pour Dupont & Fils <SARL> : {montant}" });
        StringAssert.Contains("<div class=\"mensuel\">Pour Dupont &amp; Fils &lt;SARL&gt; : <b>1", html, "le nom s'imprime en entier, le montant reste en gras");
        StringAssert.DoesNotContain("<SARL>", html);
        StringAssert.Contains("le montant mensuel à régler est de : <b>1",
            FacturePdfService.BuildHtml(new FacturePdfService.Data { ttc = 3600f, afficherMensuel = true, montantMensuel = 1200f }),
            "la phrase d'usine sort comme avant");
    }

    // ── D5-25 : « Mention TVA par défaut », réglage d'entreprise ──

    [Test]
    public void D5_25_le_reglage_d_entreprise_garde_les_debits_tant_qu_on_n_y_touche_pas()
    {
        Assert.That(MentionTva.ParDefaut(new ReglageData()), Is.EqualTo(MentionTva.Debits), "entreprise neuve");
        Assert.That(MentionTva.ParDefaut(JsonUtility.FromJson<ReglageData>("{\"phraseRetard\":\"x\"}")), Is.EqualTo(MentionTva.Debits),
            "Réglages d'avant le champ : rien ne change");
        Assert.That(MentionTva.ParDefaut(null), Is.EqualTo(MentionTva.Debits));
        Assert.That(MentionTva.ParDefaut(new ReglageData { mentionTvaParDefaut = 7 }), Is.EqualTo(MentionTva.Debits), "valeur inconnue");

        var r = new ReglageData { mentionTvaParDefaut = MentionTva.Encaissements };
        Assert.That(MentionTva.ParDefaut(JsonUtility.FromJson<ReglageData>(JsonUtility.ToJson(r))), Is.EqualTo(MentionTva.Encaissements),
            "le choix est enregistré avec les Réglages");
        Assert.That(DonneesCloud.Modele, Is.GreaterThanOrEqualTo(11), "un champ enregistré de plus : modèle 11 (empreinte : Revue17Tests.D2_01)");
    }

    [Test]
    public void D5_25_une_facture_sans_rien_de_memorise_suit_le_reglage_d_entreprise()
    {
        const int enc = MentionTva.Encaissements;
        Assert.That(MentionTva.Lire(null, enc), Is.EqualTo(enc), "type jamais ouvert");
        // Une fiche relue du serveur porte une FactureInfo d'usine pour chaque type jamais enregistré, pas « rien ».
        var relu = JsonUtility.FromJson<Locataire>(JsonUtility.ToJson(new Locataire { id = "CA-B25", Name = "SARL Coutures B" }));
        Assert.That(MentionTva.Lire(relu.factureRegul, enc), Is.EqualTo(enc), "fiche relue, régularisation jamais enregistrée");
        Assert.That(MentionTva.Lire(JsonUtility.FromJson<FactureInfo>("{}"), enc), Is.EqualTo(enc));
        Assert.That(MentionTva.Lire(null, MentionTva.Aucune), Is.EqualTo(MentionTva.Aucune), "un formulaire peut imposer le sien (dépôt, D6-23)");
        Assert.That(MentionTva.Lire(null), Is.EqualTo(MentionTva.Debits), "sans réglage : l'usine, comme avant");

        // Ce qui est mémorisé reste : un choix enregistré, ou hérité du locataire précédent quand il s'écarte de l'usine.
        Assert.That(MentionTva.Lire(new FactureInfo { saved = true }, enc), Is.EqualTo(MentionTva.Debits), "« débits » enregistré");
        var sans = new FactureInfo(); MentionTva.Ecrire(sans, MentionTva.Aucune);
        Assert.That(MentionTva.Lire(sans, enc), Is.EqualTo(MentionTva.Aucune), "« aucune mention » héritée");
        var herite = new FactureInfo(); MentionTva.Ecrire(herite, enc);
        Assert.That(MentionTva.Lire(herite, MentionTva.Debits), Is.EqualTo(enc), "« encaissements » hérité dans une entreprise aux débits");
        Assert.That(MentionTva.Lire(new FactureInfo { texteTvaDebit = "TVA acquittée sur les débits" }, enc), Is.EqualTo(MentionTva.Debits),
            "une phrase « débits » reformulée ne passe pas sous « encaissements »");

        // La phrase suit le même choix que le menu.
        var r = new ReglageData { mentionTvaParDefaut = enc };
        Assert.That(MentionTva.PhraseFacture(r, null), Is.EqualTo(FacturePdfService.TvaEncaissementsDefaut));
        Assert.That(MentionTva.PhraseFacture(new ReglageData(), null), Is.EqualTo(FacturePdfService.TvaDebitDefaut));
    }

    [Test]
    public void D5_25_les_reglages_et_les_formulaires_lisent_la_mention_par_defaut()
    {
        string reglages = Src("Facturation/ReglagePanel.cs");
        StringAssert.Contains("UIFactory.Text(body.transform, \"Mention TVA par défaut\", UITheme.Role.Libelle, UITheme.TexteSecondaire);", reglages);
        StringAssert.Contains("_mentionTvaDefaut = UIDropdown.Create(body.transform, MentionTva.Libelles.GetRange(MentionTva.Debits, 2), null, 0, null);",
            reglages, "débits ou encaissements, dans la section « TVA sur les loyers »");
        StringAssert.Contains("_mentionTvaDefaut.SetIndex(MentionTva.ParDefaut(R) - MentionTva.Debits, false);", reglages, "LoadIntoUI");
        StringAssert.Contains("R.mentionTvaParDefaut = MentionTva.Debits + _mentionTvaDefaut.Index;", reglages, "SaveFromUI");
        // Reprise : l'aide dit la règle codée (MentionTva.Lire) : par type de facture ET par locataire, pas « pour ce locataire » en entier.
        StringAssert.Contains("tant que ce type de facture \"", reglages);
        StringAssert.Contains("\"n'a jamais été enregistré pour ce locataire ; chaque facture peut en changer.\"", reglages);
        // Reprise de l'ensemble (ENS-2) : l'aide dit aussi les deux cas du formulaire du dépôt (D6-23, FactureDepotPanel.LoadIntoUI).
        StringAssert.Contains("\" Un décompte de sortie neuf la propose aussi ; le dépôt de garantie, sans TVA, propose « "
            + MentionTva.Libelles[MentionTva.Aucune] + " ».\"", reglages);
        Assert.That(MentionTva.Libelles.GetRange(MentionTva.Debits, 2),
            Is.EqualTo(new[] { MentionTva.Libelles[MentionTva.Debits], MentionTva.Libelles[MentionTva.Encaissements] }), "0 = débits, 1 = encaissements");
        StringAssert.Contains("=> Charger(menu, phrase, f, ParDefaut(R));", Src("Facturation/MentionTva.cs"),
            "les formulaires (MentionTva.Charger) partent du réglage d'entreprise");
    }
}
