using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// Tableau des objectifs et zoom (07/10) : tout est en unités d'interface, donc ce qui
/// tient dans une colonne à sa largeur minimale tient à tous les zooms (au-delà, le tableau
/// défile en largeur). On construit les cartes les plus chargées dans une colonne réduite à
/// LARGEUR_MIN_COLONNE et aucune de leurs lignes ne doit en dépasser.
public class ObjectifsTableauTests
{
    [Test]
    public void Les_cartes_les_plus_chargees_tiennent_dans_une_colonne_minimale()
    {
        var canvasGO = new GameObject("CanvasDeTest", typeof(Canvas), typeof(CanvasScaler));
        try
        {
            var auj = new DateTime(2026, 10, 6);
            var col = ObjectifsTableau.CreerColonne(canvasGO.transform, Objective.Avancement.AFaire);
            col.anchorMin = col.anchorMax = new Vector2(0, 1);
            col.sizeDelta = new Vector2(ObjectifsTableau.LARGEUR_MIN_COLONNE, 600);

            var o = new Objective("Contrôle des extincteurs et des issues de secours", Objective.Importance.Obligatoire)
            {
                echeanceISO = "2026-09-21", repeterTous = 1, repeterUnite = Objective.UniteRecurrence.Ans,
                documents = { "a.pdf", "b.pdf" }
            };
            ObjectifsTableau.Carte(col, o, null, auj, () => { }, () => { }, (_, __) => -1, _ => { });
            var a = new HomeAlert
            {
                kind = HomeAlert.Kind.BailResiliation, typeTodo = "Résiliation",
                nomRappel = "Congé possible jusqu'au 30/06/2027 (sortie le 31/12/2027)",
                nomLocataire = "Dupont Objectifs", priorite = 2, typeBg = Color.white, typeTexte = Color.black
            };
            ObjectifsTableau.CarteAuto(col, a, true);

            LayoutRebuilder.ForceRebuildLayoutImmediate(col);
            float interieur = ObjectifsTableau.LARGEUR_MIN_COLONNE - col.GetComponent<VerticalLayoutGroup>().padding.horizontal;
            var fautifs = col.GetComponentsInChildren<HorizontalLayoutGroup>()
                .Where(h => h.name == "Meta" || h.name == "Pied")
                .Select(h => (h, l: LayoutUtility.GetPreferredWidth((RectTransform)h.transform)))
                .Where(x => x.l > interieur - 22f)   // place du liseré (6) + marges du corps (16)
                .Select(x => $"{x.h.transform.parent.parent.name}/{x.h.name} : {x.l:F0}")
                .ToList();
            Assert.That(fautifs, Is.Empty, $"lignes plus larges que la colonne ({interieur - 22f:F0}) :\n  " + string.Join("\n  ", fautifs));
        }
        finally { UnityEngine.Object.DestroyImmediate(canvasGO); }
    }

    /// Cartes grises dans la fiche du bâtiment (07/10, vu en direct) : construites pendant que
    /// la fiche était non cliquable (CanvasGroup.interactable = false), elles gardaient la teinte
    /// « désactivé » (0,78 ; 50 %) posée par leur bouton. Une carte doit rester blanche.
    [Test]
    public void Une_carte_creee_dans_un_groupe_non_cliquable_reste_blanche()
    {
        var canvasGO = new GameObject("CanvasDeTest", typeof(Canvas), typeof(CanvasScaler));
        try
        {
            canvasGO.AddComponent<CanvasGroup>().interactable = false;
            var col = ObjectifsTableau.CreerColonne(canvasGO.transform, Objective.Avancement.AFaire);
            var carte = ObjectifsTableau.Carte(col, new Objective("x"), null, new DateTime(2026, 10, 6),
                () => { }, () => { }, (_, __) => -1, _ => { });
            Assert.That(carte.GetComponent<CanvasRenderer>().GetColor(), Is.EqualTo(Color.white));
            var auto = ObjectifsTableau.CarteAuto(col, new HomeAlert { kind = HomeAlert.Kind.BailRenouvellement,
                nomRappel = "Dans 86 j", typeBg = Color.white, typeTexte = Color.black }, false);
            Assert.That(auto.GetComponent<CanvasRenderer>().GetColor(), Is.EqualTo(Color.white));
        }
        finally { UnityEngine.Object.DestroyImmediate(canvasGO); }
    }

    /// Le tableau est construit dans la section Objectif des DEUX fiches. Dans celle du bâtiment,
    /// la section ne pilotait pas la hauteur de ses enfants : le tableau restait à 100 (taille
    /// par défaut d'un objet créé par code) et on ne voyait que le haut d'une carte (07/10).
    [Test]
    public void Le_tableau_a_sa_hauteur_dans_la_fiche_batiment_et_la_fiche_locataire()
    {
        var canvasGO = new GameObject("CanvasDeTest", typeof(Canvas), typeof(CanvasScaler));
        try
        {
            ((RectTransform)canvasGO.transform).sizeDelta = new Vector2(1920f, 1080f);
            foreach (var chemin in new[] { "Assets/Prefab/BatimentPrefab.prefab", "Assets/Prefab/LocatairePrefab.prefab" })
            {
                var inst = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(chemin), canvasGO.transform);
                var om = inst.GetComponentInChildren<ObjectivesManager>(true);
                Assert.That(om, Is.Not.Null, chemin);
                typeof(ObjectivesManager).GetMethod("Construire",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(om, null);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)om.transform);
                var defil = (RectTransform)om.transform.Find("TableauDefilement");
                Assert.That(defil, Is.Not.Null, chemin);
                Assert.That(defil.rect.height, Is.GreaterThan(300f),
                    $"{System.IO.Path.GetFileNameWithoutExtension(chemin)} : le tableau doit prendre sa hauteur (plafond 360)");
                // En-têtes fixes : juste au-dessus du défilement, hors de lui (la molette ne les rogne pas).
                var entetes = om.transform.Find("EnTetesColonnes");
                Assert.That(entetes, Is.Not.Null, chemin);
                Assert.That(entetes.GetSiblingIndex(), Is.EqualTo(defil.GetSiblingIndex() - 1), chemin);
                Assert.That(entetes.childCount, Is.EqualTo(3), chemin);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(canvasGO); }
    }
}
