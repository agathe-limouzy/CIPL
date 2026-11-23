using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot G (mineurs restants et tests, 16/11) : les règles ajoutées par le lot, et les fonctions pures du domaine
/// qui n'avaient aucun test (D8-19 : la ville d'une adresse, le titre d'un rappel, le calcul rapide, les règles de l'accueil).
public class LotGTests
{
    static string Iso(int joursDepuisAujourdhui) => DateTime.Today.AddDays(joursDepuisAujourdhui).ToString("yyyy-MM-dd");

    /// Un locataire dont la révision par indice est suivie, à `revisionDans` jours ; `sortieDans` : départ saisi (null : aucun).
    static Locataire Suivi(string nom, int revisionDans, int? sortieDans = null) => new Locataire
    {
        Name = nom, typeRevision = TypeRevision.Indice, indiceImmoAuDepart = "130,5",
        moisDeRevisionISO = Iso(revisionDans), dateSortieISO = sortieDans.HasValue ? Iso(sortieDans.Value) : "",
    };

    [Test]
    public void D8_06_une_revision_apres_le_depart_n_est_plus_annoncee()
    {
        Assert.That(Suivi("reste", 60).RevisionAAnnoncer, Is.True, "pas de départ");
        Assert.That(Suivi("part avant", 60, 30).RevisionAAnnoncer, Is.False, "départ dans 30 j, révision dans 60 j : elle n'aura pas lieu");
        Assert.That(Suivi("part après", 60, 90).RevisionAAnnoncer, Is.True, "encore là à la date de révision");
        Assert.That(Suivi("part le jour même", 60, 60).RevisionAAnnoncer, Is.True, "le jour de la révision compte");
        Assert.That(Suivi("en retard", -10, 30).RevisionAAnnoncer, Is.True, "révision due avant le départ : toujours à faire");
        Assert.That(Suivi("parti", -10, -5).RevisionAAnnoncer, Is.False, "déjà parti : plus suivie");
        Assert.That(new Locataire { Name = "paliers", typeRevision = TypeRevision.Paliers }.RevisionAAnnoncer, Is.False);
    }

    [Test]
    public void D8_19_l_accueil_annonce_les_revisions_dans_l_ordre_et_sans_les_partants()
    {
        var locs = new List<Locataire> { Suivi("Bientôt", 30), Suivi("Partant", 60, 30), Suivi("Retard", -5), Suivi("Loin", 200) };
        var alertes = new List<HomeAlert>();
        HomeAlertCollector.Ajouter(alertes, "", new Batiment(), locs, null);
        var revisions = HomeAlertCollector.Trier(alertes)
            .Where(a => a.kind == HomeAlert.Kind.RevisionRetard || a.kind == HomeAlert.Kind.RevisionProche).ToList();

        Assert.That(revisions.Select(a => a.nomLocataire), Is.EqualTo(new[] { "Retard", "Bientôt" }), "le retard d'abord ; ni le partant, ni la révision lointaine");
        Assert.That(revisions[0].nomRappel, Is.EqualTo("En retard"));
        Assert.That(revisions[1].nomRappel, Is.EqualTo("Dans 30 j"));
        Assert.That(revisions[0].priorite, Is.LessThan(revisions[1].priorite));
        Assert.That(alertes.All(a => a.nomBatiment == "Bâtiment"), "un bâtiment sans nom s'appelle « Bâtiment »");
        Assert.That(alertes.All(a => a.batiment == null), "la collecte ne demande pas de fiche à l'écran");
    }

    [Test]
    public void D8_19_la_ville_se_lit_apres_la_virgule_et_sans_code_postal()
    {
        Assert.That(BuildingRowUI.Ville("12 rue de Rivoli, 75001 Paris"), Is.EqualTo("Paris"));
        Assert.That(BuildingRowUI.Ville("12 rue de Rivoli, Paris"), Is.EqualTo("Paris"));
        Assert.That(BuildingRowUI.Ville("3 place Bellecour, 69002 Lyon 2e"), Is.EqualTo("Lyon 2e"));
        Assert.That(BuildingRowUI.Ville("12 rue de Rivoli 75001 Paris"), Is.EqualTo("Paris"), "sans virgule : ce qui suit le code postal (avant : l'adresse entière)");
        Assert.That(BuildingRowUI.Ville("Paris"), Is.EqualTo("Paris"));
        Assert.That(BuildingRowUI.Ville(""), Is.EqualTo("—"));
        Assert.That(BuildingRowUI.Ville(null), Is.EqualTo("—"));
        // Revue visuelle (V-38, décision du 17/11) : la ville ne se devine pas : rien, plutôt que l'adresse entière.
        Assert.That(BuildingRowUI.Ville("56 rue des tourneurs toulouse"), Is.EqualTo("—"), "numéro de rue, ni virgule ni code postal");
        Assert.That(BuildingRowUI.Ville("rue des tourneurs toulouse"), Is.EqualTo("—"), "plus de trois mots");
        Assert.That(BuildingRowUI.Ville("Saint Marcel Paulel"), Is.EqualTo("Saint Marcel Paulel"), "une ville seule reste une ville");
        Assert.That(BuildingRowUI.VilleEtLots("56 rue des tourneurs toulouse", 3), Is.EqualTo("3 lots"));
        Assert.That(BuildingRowUI.VilleEtLots("12 rue de Rivoli, 75001 Paris", 1), Is.EqualTo("Paris · 1 lot"));
        Assert.That(BuildingRowUI.VilleEtLots("", 0), Is.EqualTo("0 lot"));
        // Revue visuelle (V-32) : la rangée des deux panneaux remplit la vue, sauf si le contenu la dépasse déjà.
        Assert.That(BatimentSummaryView.HauteurPourRemplir(1000f, 865f, 400f), Is.EqualTo(535f), "135 px libres : ils vont à la rangée");
        Assert.That(BatimentSummaryView.HauteurPourRemplir(1000f, 1200f, 400f), Is.EqualTo(-1f), "le contenu dépasse : hauteur naturelle, la page défile");
    }

    [Test]
    public void D8_19_le_titre_d_un_rappel_automatique_suit_son_type()
    {
        string Fact(FacturationAlertes.AlerteType t) => RappelsAuto.Titre(new HomeAlert { kind = HomeAlert.Kind.Facturation, facturation = t });
        Assert.That(Fact(FacturationAlertes.AlerteType.Loyer), Is.EqualTo("Facturer le loyer"));
        Assert.That(Fact(FacturationAlertes.AlerteType.Regul), Is.EqualTo("Régulariser les charges"));
        Assert.That(Fact(FacturationAlertes.AlerteType.Depart), Is.EqualTo("Départ du locataire"));
        Assert.That(Fact(FacturationAlertes.AlerteType.Restitution), Is.EqualTo("Restituer le dépôt"));
        Assert.That(RappelsAuto.Titre(new HomeAlert { kind = HomeAlert.Kind.RevisionRetard }), Is.EqualTo("Réviser le loyer"));
        Assert.That(RappelsAuto.Titre(new HomeAlert { kind = HomeAlert.Kind.RevisionProche }), Is.EqualTo("Réviser le loyer"));
        Assert.That(RappelsAuto.Titre(new HomeAlert { kind = HomeAlert.Kind.BailRenouvellement }), Is.EqualTo("Fin de bail"));
        Assert.That(RappelsAuto.Titre(new HomeAlert { kind = HomeAlert.Kind.Objectif, typeTodo = "Important" }), Is.EqualTo("Important"));
    }

    [Test]
    public void D8_19_les_formules_du_calcul_rapide()
    {
        int Mode(string nom) => Enumerable.Range(0, QuickCalcInline.NbModes).Single(i => QuickCalcInline.NomMode(i) == nom);
        float R(string nom, params float[] v) => QuickCalcInline.Resultat(Mode(nom), v.Concat(new float[4]).Take(4).ToArray());

        Assert.That(R("Rentabilité", 200000f, 12000f), Is.EqualTo(6f).Within(0.001f));
        Assert.That(R("Prix d'achat réel", 1000000f, 7.5f, 0f), Is.EqualTo(1075000f).Within(0.5f));
        Assert.That(R("Prix cible", 12000f, 6f), Is.EqualTo(200000f).Within(0.5f));
        Assert.That(R("Loyer cible", 200000f, 6f), Is.EqualTo(12000f).Within(0.01f));
        Assert.That(R("Loyer au m²", 12000f, 80f), Is.EqualTo(150f).Within(0.001f));
        // D8-07 (décision du 16/11) : le loyer sur le prix frais compris, sans aucune charge : le nom ne dit plus « nette ».
        Assert.That(R("Rentabilité frais inclus", 1000000f, 7.5f, 0f, 52500f), Is.EqualTo(4.8837f).Within(0.001f));
        Assert.That(Enumerable.Range(0, QuickCalcInline.NbModes).Select(QuickCalcInline.NomMode), Has.None.EqualTo("Rentabilité nette"));
        Assert.That(R("Rentabilité", 0f, 12000f), Is.EqualTo(0f), "prix nul : pas de division");
        Assert.That(R("Loyer au m²", 12000f, 0f), Is.EqualTo(0f), "surface nulle : pas de division");
    }

    [Test]
    public void L_encaissement_declare_est_arrondi_au_centime()
    {
        // Relevé le 14/11 : encaisseeMontant enregistré à 999,996 au lieu de 1 000 (somme en float des lignes de la facture).
        var loc = new Locataire { Name = "Lot G" };
        var rec = new FactureEtat { key = "loyer-2026-P1", type = "Loyer", montant = 999.996f };
        loc.facturesEtat.Add(rec);
        Assert.That(FactureElectronique.Encaissement(loc, rec).montant, Is.EqualTo(1000f));
        rec.montant = -500.004f;   // un avoir
        Assert.That(FactureElectronique.Encaissement(loc, rec).montant, Is.EqualTo(500f), "arrondi, et en positif depuis le lot 8 (D5-11)");
    }

    [Test]
    public void B_12_les_exemples_du_calendrier_suivent_le_nombre_de_jours()
    {
        StringAssert.Contains("envoi le 16 novembre", ReglagePanel.ExempleEnvoi(15));
        StringAssert.Contains("envoi le 11 novembre", ReglagePanel.ExempleEnvoi(20), "SCI (JBC) : 20 jours, l'exemple disait toujours le 16");
        StringAssert.Contains("envoi le 1er décembre", ReglagePanel.ExempleEnvoi(0));
        StringAssert.Contains("impayé le 16 décembre", ReglagePanel.ExempleImpaye(15));
        StringAssert.Contains("impayé le 1er janvier", ReglagePanel.ExempleImpaye(31));
    }

    // D10-09 (décision du 16/11 : les copies exactes seulement) : une couleur du thème ne se recopie plus en dur, et la lecture
    // d'un « #RRGGBB » n'existe qu'à un endroit (UITheme.Hex). Les teintes propres à un écran (les ambres, les familles de section)
    // restent écrites chez elles.
    [Test]
    public void D10_09_les_couleurs_du_theme_ne_sont_plus_recopiees()
    {
        var theme = new[] { UITheme.Fond, UITheme.Carte, UITheme.Primaire, UITheme.PrimaireClair, UITheme.Alerte, UITheme.AlerteClair, UITheme.AlerteTexte,
                            UITheme.Attention, UITheme.AttentionClair, UITheme.AttentionTexte, UITheme.TextePrincipal, UITheme.TexteSecondaire,
                            UITheme.Bordure, UITheme.Erreur, UITheme.BordureChamp }
            .Select(c => ColorUtility.ToHtmlStringRGB(c)).ToHashSet();
        var copies = new List<string>();
        var definitions = new List<string>();
        foreach (string f in Directory.GetFiles(Path.Combine(Application.dataPath, "Script"), "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(f) == "UITheme.cs") continue;
            string src = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(src, "\\(\"#([0-9A-Fa-f]{6})\"\\)"))
                if (theme.Contains(m.Groups[1].Value.ToUpperInvariant())) copies.Add(Path.GetFileName(f) + " : #" + m.Groups[1].Value);
            if (Regex.IsMatch(src, @"static Color \w+\(string \w+\)\s*\{\s*ColorUtility\.TryParseHtmlString")) definitions.Add(Path.GetFileName(f));
        }
        Assert.That(copies, Is.Empty, "couleur du thème recopiée en dur : lire UITheme");
        Assert.That(definitions, Is.Empty, "lecture d'une couleur recopiée : passer par UITheme.Hex");
        Assert.That(UITheme.Hex("#0F6E56"), Is.EqualTo(UITheme.Primaire));
    }

    // Revue visuelle du 16/11 (décisions du 16/11) : le modèle de fenêtre. Un bandeau plein en tête, hors des marges de la carte,
    // titre blanc de 24 ; en bas, l'abandon puis l'action, 40 de haut, poussés à droite.
    [Test]
    public void Revue_visuelle_le_bandeau_et_le_bas_de_fenetre_suivent_le_modele()
    {
        var carte = new GameObject("carte", typeof(RectTransform));
        var groupe = carte.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        groupe.padding = new RectOffset(18, 18, 16, 16);
        var titre = UIFactory.Bandeau((RectTransform)carte.transform, "Nouvelle charge", UITheme.Primaire, () => { });
        var bandeau = (RectTransform)titre.transform.parent;
        Assert.That(bandeau.GetSiblingIndex(), Is.EqualTo(0));
        Assert.That(bandeau.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout, Is.True, "toute la largeur, hors des marges de la carte");
        Assert.That(groupe.padding.top, Is.EqualTo((int)UIFactory.HauteurBandeau + 12), "le contenu commence sous le bandeau");
        Assert.That(groupe.padding.left, Is.EqualTo(18), "les autres marges ne bougent pas");
        Assert.That(titre.fontSize, Is.EqualTo(UITheme.Role.Section));
        Assert.That(titre.color, Is.EqualTo(Color.white));
        Assert.That(bandeau.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length, Is.EqualTo(1), "le « × » demandé");

        var rangee = new GameObject("pied", typeof(RectTransform));
        var (abandon, action) = UIFactory.PiedFenetre(rangee.transform, "Annuler", "Enregistrer", UITheme.Primaire);
        Assert.That(rangee.transform.GetChild(0).name, Is.EqualTo("Espace"), "les boutons sont poussés à droite");
        Assert.That(abandon.transform.GetSiblingIndex(), Is.LessThan(action.transform.GetSiblingIndex()), "l'abandon à gauche de l'action");
        Assert.That(abandon.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight, Is.EqualTo(40f));
        Assert.That(action.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight, Is.EqualTo(40f));

        var seule = new GameObject("pied2", typeof(RectTransform));
        var (annuler, rien) = UIFactory.PiedFenetre(seule.transform, "Annuler", null, UITheme.Primaire);
        Assert.That(annuler, Is.Not.Null);
        Assert.That(rien, Is.Null, "sans action : le seul bouton d'abandon");
        // V-10 : la carte d'une confirmation s'élargit pour un libellé long.
        Assert.That(ConfirmDialog.LargeurCarte(90f, 2), Is.EqualTo(380f), "libellés courts : la largeur d'origine");
        Assert.That(ConfirmDialog.LargeurCarte(250f, 2), Is.EqualTo(554f), "« Continuer sans enregistrer » tient sur une ligne");
        Assert.That(ConfirmDialog.LargeurCarte(400f, 2), Is.EqualTo(560f), "jamais plus de 560");
        UnityEngine.Object.DestroyImmediate(carte); UnityEngine.Object.DestroyImmediate(rangee); UnityEngine.Object.DestroyImmediate(seule);
    }

    /// Revue visuelle (17/11) : « Transférer » et « Départ » sont replacés à chaque initialisation de la fiche : l'ordre ne bouge pas.
    [Test]
    public void Revue_visuelle_un_bouton_replace_avant_son_voisin_garde_sa_place()
    {
        var parent = new GameObject("entete", typeof(RectTransform)).transform;
        Transform Enfant(string nom) { var g = new GameObject(nom, typeof(RectTransform)); g.transform.SetParent(parent); return g.transform; }
        Enfant("Modifier"); var supprimer = Enfant("Supprimer"); var transferer = Enfant("Transferer"); var depart = Enfant("Depart");
        for (int tour = 1; tour <= 3; tour++)
        {
            UIFactory.PlacerAvant(transferer, supprimer);
            UIFactory.PlacerAvant(depart, transferer);
            Assert.That(string.Join(" ", parent.Cast<Transform>().Select(t => t.name)), Is.EqualTo("Modifier Depart Transferer Supprimer"), "initialisation n° " + tour);
        }
        UnityEngine.Object.DestroyImmediate(parent.gameObject);
    }

    /// Revue visuelle (V-17) : la date en trois cases donne aux contrôles le même texte « jj/mm/aaaa » que l'ancien champ unique.
    [Test]
    public void Revue_visuelle_la_date_en_trois_cases_se_lit_comme_l_ancien_champ()
    {
        Assert.That(ChampDate.Composer("", " ", ""), Is.EqualTo(""), "trois cases vides : pas de date");
        Assert.That(SaisieDate.TryParse(ChampDate.Composer("5", "3", "2027"), out var d) && d == new System.DateTime(2027, 3, 5), "5 / 3 / 2027");
        Assert.That(SaisieDate.TryParse(ChampDate.Composer("05", "03", "27"), out d) && d == new System.DateTime(2027, 3, 5), "année sur deux chiffres");
        StringAssert.Contains("obligatoire", SaisieDate.Refus(ChampDate.Composer("", "", ""), "Date de la facture"));
        StringAssert.Contains("incomplète", SaisieDate.Refus(ChampDate.Composer("5", "", "2027"), "Date de la facture"));
        StringAssert.Contains("n'existe pas", SaisieDate.Refus(ChampDate.Composer("31", "2", "2027"), "Date de la facture"));
        StringAssert.Contains("illisible", SaisieDate.Refus("5 mars", "Date de la facture"), "une saisie libre garde l'ancien refus");

        var parent = new GameObject("essai", typeof(RectTransform));
        var c = new ChampDate(parent.transform);
        int signaux = 0; c.onValueChanged.AddListener(_ => signaux++);
        c.text = "16/11/2026";
        Assert.That(c.jour.text + "|" + c.mois.text + "|" + c.annee.text, Is.EqualTo("16|11|2026"));
        Assert.That(c.text, Is.EqualTo("16/11/2026"), "relue telle quelle : l'échéance proposée se compare à ce texte");
        Assert.That(signaux, Is.EqualTo(1), "un seul signal pour trois cases");
        c.text = "16/11/2026";
        Assert.That(signaux, Is.EqualTo(1), "même date : pas de signal");
        c.text = "";
        Assert.That(c.text, Is.EqualTo("")); Assert.That(signaux, Is.EqualTo(2));
        Assert.That(c.Vide(), "trois cases vides");
        c.text = "5/3/27";
        Assert.That(c.LireDate(out var lue) && lue == new System.DateTime(2027, 3, 5), "LireDate : comme le champ des fiches");
        c.annee.text = "";
        Assert.That(!c.Vide() && !c.LireDate(out _), "incomplète : ni vide ni lisible");
        var rangee = new GameObject("rangee", typeof(RectTransform));
        var col = ChampDate.Colonne(rangee.transform, "Date", new System.DateTime(2026, 11, 16));
        Assert.That(col.text, Is.EqualTo("16/11/2026"), "colonne préremplie");
        Assert.That(rangee.transform.GetChild(0).GetChild(0).GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Date"), "le libellé est au-dessus des cases");
        UnityEngine.Object.DestroyImmediate(rangee);
        UnityEngine.Object.DestroyImmediate(parent);
    }
}
