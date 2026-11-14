using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

/// Revue 17, lot D, groupe « révision » (11/11/2026) : trimestre de révision, refus au centre, historique des loyers, paliers et
/// périodicité, montants réaffichés. Règle pure d'abord, texte du code à défaut.
public class LotDRevisionTests
{
    static DateTime D(string iso) => DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));

    /// Le corps d'une méthode, de `debut` à sa fermeture (motif valable en LF comme en CRLF).
    static string Corps(string src, string debut)
    {
        int i = src.IndexOf(debut, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), debut);
        return src.Substring(i, src.IndexOf("\n    }", i, StringComparison.Ordinal) - i);
    }

    // Bail trimestriel 01/01/2026 → 31/12/2034 ; 12 000 € jusqu'au 31/12/2027, puis 13 000 € jusqu'à la fin du bail.
    static Locataire AvecDeuxPaliers()
    {
        var loc = new Locataire
        {
            Name = "Test Paliers", typeRevision = TypeRevision.Paliers, loyerAnnuel = 12000f, loyerDepart = 12000f,
            periodiciteLoyer = Periodicite.trimestriel, dateDebutBailISO = "2026-01-01", dateFinBailISO = "2034-12-31",
        };
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, -1, 12000f, D("2027-12-31")), Is.Null);
        Assert.That(Loyers.PoserPalier(loc, loc.paliers, -1, 13000f, D("2034-12-31")), Is.Null);
        return loc;
    }

    /// D7b-01 : la k-ième révision porte sur la référence + k ans, c'est-à-dire la dernière révision + 1 an.
    [Test]
    public void Trimestre_propose_un_an_apres_la_derniere_revision()
    {
        Assert.That(InseeIndiceService.TrimestreSuivant("2026-T1", "—"), Is.EqualTo("2027-T1"), "1re révision : référence + 1 an");
        Assert.That(InseeIndiceService.TrimestreSuivant("2026-T1", "137.16  (2027-T1)"), Is.EqualTo("2028-T1"), "dernière révision + 1 an");
        Assert.That(InseeIndiceService.TrimestreSuivant("2026-Q2", null), Is.EqualTo("2027-T2"), "ancien format « Q »");
        Assert.That(InseeIndiceService.TrimestreSuivant("T1", ""), Is.EqualTo(""), "illisible : rien de proposé");
        StringAssert.DoesNotContain("DateTime.Now.Year - 1", Src("RevisionLoyer/RevisionPanel.cs"), "plus d'année « en cours − 1 »");
    }

    /// D7b-01 : un trimestre égal (ou antérieur) à la référence ou à la dernière révision est refusé : loyer inchangé, prochaine
    /// révision repoussée d'un an.
    [Test]
    public void Un_trimestre_egal_a_la_reference_ou_a_la_derniere_revision_est_refuse()
    {
        Assert.That(InseeIndiceService.RefusTrimestre("2026-T1", "2026-T1", "—"), Does.Contain("postérieur au trimestre de référence (2026-T1)"));
        Assert.That(InseeIndiceService.RefusTrimestre("2025-T4", "2026-T1", "—"), Is.Not.Null, "avant la référence");
        Assert.That(InseeIndiceService.RefusTrimestre("2027-T1", "2026-T1", "—"), Is.Null, "1re révision juste");
        Assert.That(InseeIndiceService.RefusTrimestre("2027-T1", "2026-T1", "137.16  (2027-T1)"),
            Does.Contain("dernière révision (2027-T1)"), "même trimestre que la dernière révision");
        Assert.That(InseeIndiceService.RefusTrimestre("2028-T1", "2026-T1", "137.16  (2027-T1)"), Is.Null);
        Assert.That(InseeIndiceService.RefusTrimestre("", "2026-T1", "—"), Is.Null, "illisible : ne refuse rien (comme avant)");
        string reviser = Corps(Src("RevisionLoyer/RevisionPanel.cs"), "private IEnumerator Reviser()");
        int refus = reviser.IndexOf("InseeIndiceService.RefusTrimestre(", StringComparison.Ordinal);
        Assert.That(refus, Is.GreaterThan(0).And.LessThan(reviser.IndexOf("InseeIndiceService.FetchObservations(", StringComparison.Ordinal)),
            "refusé avant l'appel réseau");
        StringAssert.Contains("{ AfficheTrimWarn(refusTrim); ConfirmDialog.Erreur(refusTrim); yield break; }", reviser, "au centre et sous le champ");
        StringAssert.Contains("{VariationLoyer(_loc.loyerAnnuelPrecedent, loyerRevise)}", reviser, "la variation dans le message de fin");
        Assert.That(RevisionPanel.VariationLoyer(15000f, 15450f), Is.EqualTo(" (+3,0 %)"));
        Assert.That(RevisionPanel.VariationLoyer(15000f, 15000f), Is.EqualTo(" (+0,0 %)"));
        Assert.That(RevisionPanel.VariationLoyer(15000f, 14000f), Is.EqualTo(" (-6,7 %)"));
        Assert.That(RevisionPanel.VariationLoyer(0f, 14000f), Is.EqualTo(""), "sans loyer précédent");
    }

    /// D7b-06 : l'échec réseau de l'INSEE est dit en français, et au centre.
    [Test]
    public void Un_echec_INSEE_est_dit_en_francais_au_centre()
    {
        Assert.That(InseeIndiceService.MessageEchec(UnityWebRequest.Result.ConnectionError),
            Is.EqualTo("INSEE injoignable : vérifiez la connexion, puis réessayez."));
        Assert.That(InseeIndiceService.MessageEchec(UnityWebRequest.Result.ProtocolError), Does.Contain("réessayez plus tard"));
        string rp = Src("RevisionLoyer/RevisionPanel.cs");
        StringAssert.DoesNotContain("statusText.text = $\"Erreur :", rp);
        StringAssert.DoesNotContain("statusText.text = \"Saisissez", rp, "refus au centre");
        StringAssert.DoesNotContain("statusText.text = \"Loyer de départ invalide\"", rp);
        StringAssert.Contains("onError?.Invoke(MessageEchec(req.result));", Src("RevisionLoyer/InseeIndiceService.cs"));
    }

    /// D7b-14 : fermer pendant la récupération INSEE laissait « Initialiser » grisé pour la session.
    [Test]
    public void Initialiser_redevient_cliquable_a_chaque_ouverture()
    {
        Assert.That(System.Text.RegularExpressions.Regex.IsMatch(Src("RevisionLoyer/RevisionPanel.cs"),
            @"SetMode\(!loc\.IndiceInitialise\);\s*btnInitialiser\.interactable = true"), Is.True);
    }

    /// D7b-03 : la date de la référence est lue avant l'avenant (qui pose debutConditionsISO au jour de la révision).
    [Test]
    public void La_date_de_la_reference_est_lue_avant_l_avenant()
    {
        string reviser = Corps(Src("RevisionLoyer/RevisionPanel.cs"), "private IEnumerator Reviser()");
        int lue = reviser.IndexOf("var dateReference = FacturationSuivi.TryEcheance(_loc.debutConditionsISO", StringComparison.Ordinal);
        Assert.That(lue, Is.GreaterThan(0).And.LessThan(reviser.IndexOf("Loyers.EnregistrerAvenant(_loc, d.Date);", StringComparison.Ordinal)));
        StringAssert.Contains("LoyerHistoryService.EnregistrerReference(_loc, dateReference,", reviser);
        StringAssert.Contains("float loyer = _loc.loyerDepart;", reviser, "D7b-10 : la base enregistrée, pas le texte du champ");
    }

    /// D7b-04 : à la première révision d'un bail jamais renégocié, la ligne d'historique (début vide) couvre l'année du bail.
    [Test]
    public void Une_premiere_revision_suffit_pour_lire_le_loyer_facture()
    {
        var loc = new Locataire
        {
            typeRevision = TypeRevision.Indice, loyerAnnuel = 12000f, loyerDepart = 12000f, periodiciteLoyer = Periodicite.trimestriel,
            dateDebutBailISO = "2025-02-15", dateFinBailISO = "2034-02-14",
        };
        Loyers.EnregistrerAvenant(loc, D("2026-02-15"));
        Assert.That(loc.historiqueLoyers[0].debutISO, Is.EqualTo(""), "le cas : début vide");
        Assert.That(LoyerHistoryService.HistoriqueCouvre(loc, 2025), Is.True);
        Assert.That(LoyerHistoryService.HistoriqueCouvre(loc, 2024), Is.False, "avant le bail");
        // 45 jours sur 90 du 1er trimestre, puis trois trimestres pleins : 1 500 + 3 × 3 000.
        Assert.That(LoyerHistoryService.LoyerPourAnnee(loc, 2025, null), Is.EqualTo(10500f).Within(0.05f));
        loc.historiqueLoyers.Add(null);
        Assert.That(LoyerHistoryService.HistoriqueCouvre(loc, 2025), Is.True, "une ligne nulle ne compte pas");
    }

    /// D7b-17 : une périodicité (ou des mois) qui désaligne les paliers est refusée ; la fiche ne change pas.
    [Test]
    public void Un_changement_de_periodicite_qui_desaligne_les_paliers_est_refuse()
    {
        var loc = AvecDeuxPaliers();
        Assert.That(Loyers.PaliersDesalignesApres(loc, Periodicite.trimestriel, new List<int>(), null), Is.False);
        Assert.That(Loyers.PaliersDesalignesApres(loc, Periodicite.mensuel, new List<int>(), null), Is.False);
        Assert.That(Loyers.PaliersDesalignesApres(loc, Periodicite.Annuel, new List<int>(), null), Is.False, "le 01/01 ouvre l'année");
        Assert.That(Loyers.PaliersDesalignesApres(loc, Periodicite.trimestriel, new List<int> { 2, 5, 8, 11 }, null), Is.True,
            "le 01/01/2028 n'ouvre plus de période");
        Assert.That(Loyers.PaliersDesalignesApres(loc, Periodicite.trimestriel, new List<int> { 2, 5, 8, 11 }, D("2028-02-01")), Is.False,
            "avenant de périodicité après la fin du palier : l'ancienne périodicité vaut encore le 01/01/2028");
        Assert.That(loc.periodiciteLoyer, Is.EqualTo(Periodicite.trimestriel), "calculé sur une copie");
        Assert.That(loc.historiquePeriodicites == null || loc.historiquePeriodicites.Count == 0, Is.True, "calculé sur une copie");
        Assert.That(Loyers.PaliersDesalignesApres(new Locataire(), Periodicite.mensuel, null, null), Is.False, "sans paliers");
        string valider = Corps(Src("RevisionLoyer/RevisionPanel.cs"), "void ValiderInitialisation()");
        int refus = valider.IndexOf("Loyers.PaliersDesalignesApres(_loc, perChoisie, moisChoisis, effetPeriodicite)", StringComparison.Ordinal);
        Assert.That(refus, Is.GreaterThan(0).And.LessThan(valider.IndexOf("Loyers.EnregistrerAvenant(_loc, Iso(avenant));", StringComparison.Ordinal)),
            "refusé avant toute écriture");
    }

    /// D7b-16 : le second tableau n'est construit que s'il manque un indice au cache ; D7b-07 : les types sans observation.
    [Test]
    public void Les_indices_manquants_sont_reperes()
    {
        var obs = new Dictionary<IndiceImmo, List<(string periode, float valeur)>>
        {
            { IndiceImmo.ILC, new List<(string periode, float valeur)> { ("2026-T1", 137f) } },
            { IndiceImmo.ICC, new List<(string periode, float valeur)>() },
        };
        CollectionAssert.AreEqual(new[] { IndiceImmo.IRL, IndiceImmo.ICC },
            LoyerHistoryService.Manquants(new[] { IndiceImmo.ILC, IndiceImmo.IRL, IndiceImmo.ICC, IndiceImmo.IRL }, obs));
        Assert.That(LoyerHistoryService.Manquants(new[] { IndiceImmo.ILC }, obs), Is.Empty);
        Assert.That(LoyerHistoryService.Manquants(new[] { IndiceImmo.ILC }, null), Has.Count.EqualTo(1));
        string ctrl = Src("Travaux/RentabiliteGlobaleController.cs");
        StringAssert.Contains("bool attendre = isActiveAndEnabled && LoyerHistoryService.Manquants(types, cache).Count > 0;", ctrl);
        // Contrôleur inactif : aucun essai à l'INSEE, donc pas de ligne « indisponibles » (relecture du lot D).
        StringAssert.Contains("BuildTableauRentabilite(locataires, achats, travaux, cache, types, isActiveAndEnabled && !attendre);", ctrl);
        StringAssert.Contains("Indices INSEE indisponibles : loyers des années anciennes non indexés, valeurs approchées.", ctrl);
    }

    /// D7b-16 : trier les paliers une fois par période ne change aucun montant.
    [Test]
    public void Le_montant_d_une_periode_a_paliers_ne_change_pas()
    {
        var loc = AvecDeuxPaliers();
        Assert.That(Loyers.MontantPeriode(loc, 2027, 4), Is.EqualTo(3000f).Within(0.001f));
        Assert.That(Loyers.MontantPeriode(loc, 2028, 1), Is.EqualTo(3250f).Within(0.001f));
        Assert.That(Loyers.LoyerAnnuelA(loc, D("2028-01-01")), Is.EqualTo(13000f));
        Assert.That(Loyers.LoyerAnnuelA(loc, D("2025-06-01")), Is.EqualTo(12000f), "avant le début : le premier palier");
    }

    /// D7b-10 : les montants réaffichés gardent leurs centimes et jamais la notation scientifique.
    [Test]
    public void Les_montants_reaffiches_ont_un_format_fixe()
    {
        Assert.That(RevisionPanel.Nombre(123456.78f), Is.EqualTo("123456.78"));
        Assert.That(RevisionPanel.Nombre(12345678f), Is.EqualTo("12345678"));
        Assert.That(RevisionPanel.Nombre(12000f), Is.EqualTo("12000"));
        Assert.That(RevisionPanel.Nombre(0f), Is.EqualTo(""));
        Assert.That(SaisieNumerique.TryParse(RevisionPanel.Nombre(123456.78f), out float relu), Is.True);
        Assert.That(relu, Is.EqualTo(123456.78f), "relu à l'identique");
        StringAssert.Contains("_formLoyer.text = RevisionPanel.Nombre(loyer);", Src("RevisionLoyer/PaliersPanel.cs"));
        StringAssert.Contains("loyerDepart.text = Nombre(loc.loyerDepart);", Src("RevisionLoyer/RevisionPanel.cs"));
    }

    /// D7b-18 (part) : le menu de périodicité montre des libellés, dans l'ordre de l'énumération (l'indice du menu = la valeur).
    [Test]
    public void Le_menu_de_periodicite_montre_des_libelles_dans_l_ordre_de_l_enumeration()
    {
        var l = RevisionPanel.LibellesPeriodicite;
        Assert.That(l.Length, Is.EqualTo(Enum.GetValues(typeof(Periodicite)).Length));
        Assert.That(l[(int)Periodicite.mensuel], Is.EqualTo("Mensuel"));
        Assert.That(l[(int)Periodicite.trimestriel], Is.EqualTo("Trimestriel"));
        Assert.That(l[(int)Periodicite.BiAnnuel], Is.EqualTo("Semestriel (bi-annuel)"));
        Assert.That(l[(int)Periodicite.Annuel], Is.EqualTo("Annuel"));
        string rp = Src("RevisionLoyer/RevisionPanel.cs");
        StringAssert.DoesNotContain("Enum.GetNames(typeof(Periodicite))", rp);
        StringAssert.Contains("periodiciteDropdown.value = (int)loc.periodiciteLoyer;", rp, "l'indice reste la valeur de l'énumération");
    }

    /// Retour de BW5 (12/11) : le loyer révisé se voit avant « Réviser », calculé comme « Réviser » le fera.
    [Test]
    public void BW5_l_apercu_dit_le_loyer_revise_avant_Reviser()
    {
        var obs = new List<(string periode, float valeur)> { ("2024-Q1", 134.58f), ("2025-Q1", 136.10f) };
        Assert.That(RevisionPanel.CalculApercu(obs, "2024-T1", "2025-T1", 12000f, out var dep, out var act, out float revise), Is.True);
        Assert.That(dep.periode, Is.EqualTo("2024-Q1"), "indice de départ");
        Assert.That(act.periode, Is.EqualTo("2025-Q1"), "indice voulu");
        Assert.That(revise, Is.EqualTo(12000f * 136.10f / 134.58f).Within(0.01f), "le loyer que « Réviser le loyer » écrira");
        StringAssert.Contains("« Réviser le loyer » enregistre ce loyer", RevisionPanel.Apercu(obs, "2024-T1", "2025-T1", 12000f), "le vrai libellé du bouton");
        StringAssert.Contains("n'est pas encore publié", RevisionPanel.Apercu(obs, "2024-T1", "2026-T1", 12000f));
        Assert.That(RevisionPanel.Apercu(null, "2024-T1", "2025-T1", 12000f), Is.Null, "INSEE pas encore lu");
        Assert.That(RevisionPanel.Apercu(obs, "2024-T1", "2025-T1", 0f), Is.Null, "pas de base");

        string rp = Src("RevisionLoyer/RevisionPanel.cs");
        StringAssert.Contains("private void OnTrimVouluChanged(string _) { ValiderTrimestreVoulu(); RafraichirApercu(); }", rp, "l'aperçu suit le trimestre tapé");
        StringAssert.Contains("trimestreDepart.OnTrimestreChanged += OnTrimDepartChanged;", rp, "et le trimestre de référence");
        StringAssert.Contains("var parent = _trimWarn != null ? _trimWarn.transform.parent : trimestreVoulu.transform.parent;", rp,
            "sous la ligne des trimestres, pas dedans (chevauchement vu en Play)");
        StringAssert.Contains("AfficherResultat(precedent, FormatIndice(act.valeur, act.periode)", rp,
            "l'aperçu remplit le cadre « Résultat de la révision »");
        // Retour de BW5 : « Indice précédent » = la dernière révision, sinon la référence ; la variation se compte depuis lui.
        var l = new Locataire { indiceImmoAuDepart = "134.58  (2024-Q1)", indiceImmoActuel = "—" };
        Assert.That(RevisionPanel.IndicePrecedent(l), Is.EqualTo("134.58  (2024-Q1)"), "aucune révision : la référence");
        l.indiceImmoActuel = "135.87  (2025-Q1)";
        Assert.That(RevisionPanel.IndicePrecedent(l), Is.EqualTo("135.87  (2025-Q1)"), "après une révision : son indice");
        Assert.That(RevisionPanel.VariationIndice("135.87  (2025-Q1)", 135.26f), Is.EqualTo("-0,4 %"), "baisse depuis la dernière révision");
        Assert.That(RevisionPanel.VariationIndice("134.58  (2024-Q1)", 135.87f), Is.EqualTo("+1,0 %"));
        Assert.That(RevisionPanel.VariationIndice("—", 135.87f), Is.EqualTo("—"));
        StringAssert.Contains("string precedentAvant = IndicePrecedent(_loc);", rp, "lu avant que la révision n'écrive le nouvel indice");

        // Décisions du 12/11 : le volet reste ouvert après la révision, bouton grisé ; le bouton s'appelle « Réviser le loyer ».
        string fin = rp.Substring(rp.IndexOf("_reviseeIci = true;", StringComparison.Ordinal) - 400, 900);
        StringAssert.DoesNotContain("gameObject.SetActive(false);", fin, "la révision ne ferme plus le volet");
        StringAssert.Contains("btnReviser.interactable = false;", fin, "bouton grisé : pas de seconde révision par erreur");
        StringAssert.Contains("btnReviser.interactable = !_reviseeIci;", rp, "changer le trimestre ne le rend pas cliquable");
        StringAssert.Contains("const string LibelleReviser = \"Réviser le loyer\";", rp);
    }
}
