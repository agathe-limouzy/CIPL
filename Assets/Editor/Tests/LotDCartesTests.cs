using System.IO;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot D, groupe CARTES (11/11/2026) : carte (glisser, molette), géocodage, PLU, achats et travaux, objectifs.
public class LotDCartesTests
{
    /// Retours de BW6 (13/11) : boutons + et − sur la carte ; vignette du résumé sans ouvrir la fiche complète.
    [Test]
    public void BW6_boutons_de_zoom_et_vignette_du_resume()
    {
        string dossier = System.IO.Path.Combine(Application.dataPath, "Script");
        string carte = System.IO.File.ReadAllText(System.IO.Path.Combine(dossier, "Maps", "MapController.cs"));
        StringAssert.Contains("plus.onClick.AddListener(ZoomIn);", carte);
        StringAssert.Contains("moins.onClick.AddListener(ZoomOut);", carte);
        StringAssert.Contains("AjouterBoutonsZoom();   // retour de BW6 (13/11)", carte, "créés au démarrage de la carte");
        string resume = System.IO.File.ReadAllText(System.IO.Path.Combine(dossier, "Batiment", "BatimentSummaryView.cs"));
        StringAssert.Contains("MapThumbnailService.Charge(this, _bp.getBatiment()?.adressBatiment", resume, "la vignette de l'accueil en attendant");
    }

    static string Src(string relatif) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", relatif));

    // ── D7b-15 : la carte suit le doigt sur les deux axes ─────────────────────────

    [Test]
    public void D7b_15_un_glisser_deplace_le_centre_dans_le_sens_du_doigt()
    {
        // pixelDelta = ancienne − nouvelle position (y vers le haut) : doigt vers la droite → delta.x < 0.
        var (lat0, lon0) = MapController.Deplacer(43.6, 1.4, Vector2.zero, 17);
        Assert.That(lat0, Is.EqualTo(43.6).Within(1e-12)); Assert.That(lon0, Is.EqualTo(1.4).Within(1e-12));

        var (_, lonDroite) = MapController.Deplacer(43.6, 1.4, new Vector2(-10, 0), 17);
        Assert.That(lonDroite, Is.LessThan(1.4), "doigt vers la droite : le centre part vers l'ouest (on voit l'ouest)");
        var (latDroite, _) = MapController.Deplacer(43.6, 1.4, new Vector2(-10, 0), 17);
        Assert.That(latDroite, Is.EqualTo(43.6).Within(1e-12), "un glisser horizontal ne bouge pas la latitude");

        var (latHaut, lonHaut) = MapController.Deplacer(43.6, 1.4, new Vector2(0, -10), 17);
        Assert.That(latHaut, Is.LessThan(43.6), "doigt vers le haut : le centre part vers le sud");
        Assert.That(lonHaut, Is.EqualTo(1.4).Within(1e-12));

        var (_, lonGauche) = MapController.Deplacer(43.6, 1.4, new Vector2(10, 0), 17);
        Assert.That(lonGauche - 1.4, Is.EqualTo(1.4 - lonDroite).Within(1e-9), "symétrie entre +10 et −10");
    }

    [Test]
    public void D7b_15_le_centre_reste_borne()
    {
        var (lat, lon) = MapController.Deplacer(84.99, 179.999, new Vector2(1e6f, 1e6f), 10);
        Assert.That(lat, Is.LessThanOrEqualTo(85.0));
        Assert.That(lon, Is.InRange(-180.0, 180.0), "la longitude revient dans [−180, 180]");
    }

    [Test]
    public void D7b_19_20_la_molette_seule_fait_defiler_la_fiche_et_le_glisser_attend_le_relachement()
    {
        string m = Src("Maps/MapInteractable.cs");
        StringAssert.Contains("ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.scrollHandler)", m, "D7b-19");
        StringAssert.Contains("Input.GetKey(KeyCode.LeftControl)", m, "D7b-19 : Ctrl + molette zoome");
        StringAssert.Contains("public void OnEndDrag(PointerEventData eventData)", m, "D7b-20 : une image par geste");
        Assert.That(System.Text.RegularExpressions.Regex.Matches(m, @"PanByPixels\(").Count, Is.EqualTo(1), "D7b-20 : un seul appel, au relâchement");
    }

    // ── D7b-05 : adresse imprécise, repli Nominatim ──────────────────────────────

    [Test]
    public void D7b_05_une_adresse_imprecise_ne_passe_pas_par_Nominatim()
    {
        Assert.That(GeoCodingService.Decision(true, true, 0.4f), Is.EqualTo(GeoCodingService.Verdict.Imprecise));
        Assert.That(GeoCodingService.Decision(true, false, 0f), Is.EqualTo(GeoCodingService.Verdict.Introuvable));
        Assert.That(GeoCodingService.Decision(false, false, 0f), Is.EqualTo(GeoCodingService.Verdict.Injoignable), "seul cas de repli");
        Assert.That(GeoCodingService.Decision(true, true, 0.9f), Is.EqualTo(GeoCodingService.Verdict.Accepte));
        Assert.That(GeoCodingService.Decision(true, true, 0f), Is.EqualTo(GeoCodingService.Verdict.Accepte), "score absent : accepté, comme avant");
        Assert.That(GeoCodingService.Message(GeoCodingService.Verdict.Imprecise), Is.EqualTo("Adresse imprécise : précisez la ville ou le code postal."));
        Assert.That(GeoCodingService.Message(GeoCodingService.Verdict.Accepte), Is.Null);
        StringAssert.Contains("&countrycodes=fr", Src("Maps/GeoCodingService.cs"), "Nominatim en France seulement");
    }

    /// Retour de BW17 (14/11) : sans ville ni code postal, l'adresse est imprécise ; les suggestions partent à 4 caractères.
    [Test]
    public void BW17_une_adresse_sans_ville_ni_code_postal_est_imprecise()
    {
        Assert.That(GeoCodingService.VillePresente("12 rue de la Paix", "Cherbourg-en-Cotentin"), Is.False, "api-adresse a choisi Cherbourg au hasard");
        Assert.That(GeoCodingService.VillePresente("12 rue de la Paix, Paris", "Paris"), Is.True);
        Assert.That(GeoCodingService.VillePresente("12 rue de la Paix 75002", "Paris"), Is.True, "le code postal suffit");
        Assert.That(GeoCodingService.VillePresente("3 place du peuple saint etienne", "Saint-Étienne"), Is.True, "accents et tirets ne comptent pas");
        Assert.That(GeoCodingService.VillePresente("56 rue des tourneurs toulouse", "Toulouse"), Is.True, "adresse de rivoli");
        Assert.That(GeoCodingService.VillePresente("12 rue de Parisot", "Paris"), Is.False, "un mot entier, pas un morceau");
        Assert.That(GeoCodingService.VillePresente("12 rue de la Paix", null), Is.False);
        Assert.That(AdresseAutocomplete.AChercher("12 r"), Is.True);
        Assert.That(AdresseAutocomplete.AChercher(" 12 "), Is.False, "trop court");
        StringAssert.Contains("AdresseAutocomplete.Attach(addressInput", Src("Maps/MapController.cs"), "la liste est branchée sur le champ de la fiche");
        // Second retour : la zone du bâtiment est privilégiée (sans elle, des « 12 La Rue » de communes au hasard).
        StringAssert.EndsWith("&limit=5&autocomplete=1&lat=43.6012&lon=1.4451", GeoCodingService.UrlSuggestions("12 rue de la", (43.6012, 1.4451)));
        StringAssert.EndsWith("&limit=5&autocomplete=1", GeoCodingService.UrlSuggestions("12 rue de la", null), "sans carte connue : sans zone");
        StringAssert.Contains("CadreAdresseExtensible();", Src("Maps/MapController.cs"), "le bloc d'adresse grandit avec son message");
        // Décision du 14/11 : premier bâtiment (aucune carte) : la ville du lieu d'émission, jamais le lieu d'usine.
        Assert.That(GeoCodingService.VilleDeRepli(" Meudon "), Is.EqualTo("Meudon"));
        Assert.That(GeoCodingService.VilleDeRepli(""), Is.Null, "lieu vide (particulier sans ville connue) : toute la France");
        Assert.That(GeoCodingService.VilleDeRepli(FacturePdfService.LieuDefaut), Is.Null, "le lieu d'usine n'est pas la ville du client");
        StringAssert.Contains("() => ReglageService.Current?.lieuEmission", Src("Maps/MapController.cs"));
    }

    // ── D7b-12 : PLU ─────────────────────────────────────────────────────────────

    [Test]
    public void D7b_12_une_panne_reseau_n_est_pas_une_commune_introuvable()
    {
        Assert.That(CadastreService.MessageCommune(false, false, "Agde"), Is.EqualTo("Service des communes injoignable, réessayez."));
        Assert.That(CadastreService.MessageCommune(true, false, "Agde"), Is.EqualTo("Commune introuvable : « Agde »."));
        Assert.That(CadastreService.MessageCommune(true, true, "Agde"), Is.Null);
        string plu = Src("PLU/PLUOverlayPanel.cs");
        StringAssert.Contains("if (_searchMode == mode) DisplayInfo(zoneInfo, lat, lon);", plu, "affiché seulement dans l'onglet qui a cherché");
        StringAssert.Contains("RechercherPLUByPoint(lat, lon, SearchMode.Cadastre)", plu);
    }

    // ── D7b-08 : achats et travaux ───────────────────────────────────────────────

    [Test]
    public void D7b_08_la_conversion_silencieuse_rend_NaN_sur_une_saisie_illisible()
    {
        Assert.That(double.IsNaN(SaisieNumerique.OuNaN("25O 000")), Is.True, "lettre O à la place d'un zéro");
        Assert.That(SaisieNumerique.OuNaN("250 000"), Is.EqualTo(250000d));
        Assert.That(SaisieNumerique.OuNaN("3,5"), Is.EqualTo(3.5d).Within(1e-12));
        Assert.That(SaisieNumerique.OuNaN(""), Is.EqualTo(0d), "champ vide = 0, cas normal");
    }

    [Test]
    public void D7b_08_le_refus_nomme_le_premier_champ_illisible()
    {
        Assert.That(SaisieNumerique.RefusIllisible(("Prix d'achat", "250 000"), ("Taux d'intérêt", "3,2")), Is.Null);
        string r = SaisieNumerique.RefusIllisible(("Prix d'achat", "25O 000"), ("Taux d'intérêt", "x"));
        StringAssert.StartsWith("« Prix d'achat » : « 25O 000 »", r, "le premier dans l'ordre du formulaire");
        StringAssert.Contains("« Taux d'intérêt »", SaisieNumerique.RefusIllisible(("Prix d'achat", ""), ("Taux d'intérêt", "x")));
        // Le refus vient avant toute écriture : les GetNewSave suivent le contrôle de la date.
        foreach (var f in new[] { "Achat/AchatFormPanel.cs", "Travaux/TravauxFormPanel.cs" })
        {
            string s = Src(f);
            int refus = s.IndexOf("SaisieNumerique.RefusIllisible(", System.StringComparison.Ordinal);
            int date = s.IndexOf("datePicker.LireDate(out var dateSaisie)", System.StringComparison.Ordinal);
            int ecrit = s.IndexOf(".GetNewSave()", System.StringComparison.Ordinal);
            Assert.That(refus, Is.GreaterThan(0), f);
            Assert.That(refus < date && date < ecrit, Is.True, f + " : nombres, puis date, puis écriture");
            StringAssert.DoesNotContain("ParseOuAvertirD", s, f + " : plus de message pendant la frappe");
            StringAssert.Contains("(\"Montant emprunté\", emprunt ? txtMontantEmprunte.GetValue() : \"\")", s, f + " : champ caché en « Comptant » non refusé");
            StringAssert.Contains("if (!double.IsNaN(montantLu)) _data.montantEmprunte = montantLu;", s, f + " : jamais NaN au modèle");
        }
    }

    // ── D7b-18 : textes visibles ─────────────────────────────────────────────────

    [Test]
    public void D7b_18_dates_lisibles_et_Comptant()
    {
        Assert.That(AchatItemUI.DateLisible("2026-11-09"), Is.EqualTo("09/11/2026"));
        Assert.That(AchatItemUI.DateLisible(""), Is.EqualTo("—"));
        Assert.That(AchatItemUI.DateLisible(null), Is.EqualTo("—"));
        Assert.That(AchatItemUI.DateLisible("pas une date"), Is.EqualTo("—"));
        foreach (var f in new[] { "Achat/AchatFormPanel.cs", "Travaux/TravauxFormPanel.cs" })
            StringAssert.Contains("\"Comptant\", \"Emprunt\"", Src(f), f);
        StringAssert.DoesNotContain("\"Compant\"", Src("Travaux/TravauxFormPanel.cs"));
        StringAssert.Contains("statusText.text = \" Carte indisponible\";", Src("Maps/MapController.cs"));
        StringAssert.Contains("if (_tileLoader.ImagePosee || !SessionCloud.Active) _adresseAffichee = address;", Src("Maps/MapController.cs"),
                              "hors cloud, l'adresse est marquée affichée : pas de regéocodage à chaque « Sauvegarder » (D9-19)");
    }

    // ── D7b-09 : objectifs sur le cloud ──────────────────────────────────────────

    [Test]
    public void D7b_09_un_objectif_refuse_si_un_collegue_tient_la_fiche()
    {
        Assert.That(ObjectifPanel.Refus(null, "Volteo"), Is.Null);
        Assert.That(ObjectifPanel.Refus("", "Volteo"), Is.Null);
        string r = ObjectifPanel.Refus("Marie", "Volteo");
        StringAssert.Contains("« Volteo »", r); StringAssert.Contains("Marie", r); StringAssert.Contains("rien n'est changé", r);

        var loc = new Locataire { id = "L1", Name = "Volteo" };
        Assert.That(ObjectifPanel.Cible(null, loc), Is.EqualTo(("locataire", "L1", "Volteo")), "objectif de locataire : sa fiche");
        Assert.That(ObjectifPanel.Cible(null, null).type, Is.EqualTo("batiment"));
        StringAssert.Contains("EcritureCloud.Reserver(new[] { Cible(bp, loc) }, \"objectifs\"", Src("Objectif/ObjectifPanel.cs"));
    }

    // ── D9-19 : une recherche seulement si l'adresse a changé ────────────────────

    [Test]
    public void D9_19_GetAdress_ne_relance_que_pour_une_adresse_changee()
    {
        string m = Src("Maps/MapController.cs");
        StringAssert.Contains("if (a != _adresseAffichee && a != _adresseEnCours) OnSearchClicked();", m);
        StringAssert.Contains("public string Adresse => addressInput.text;", m);
        StringAssert.Contains("if (a == _adresseEnCours) return;", m, "SetAdress ne double pas la recherche lancée par GetAdress");
    }
}
