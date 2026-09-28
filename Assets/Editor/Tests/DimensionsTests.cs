using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Les dimensions écrites en dur.
///
/// Toute la journée du 23/09 s'est passée à corriger le même défaut sur des
/// écrans différents : une hauteur ou une largeur fixée à un nombre, plus petite
/// que ce qu'elle doit contenir. Le contenu déborde alors **en silence** — le
/// layout croit que tout tient, donc rien ne défile et rien ne se signale.
///
/// La section « Objectif » imposait 120 px pour un contenu de 275. La carte
/// d'identité, 168 px pour 397. `ContentLocataire` gardait 1934 px de large sur
/// un écran de 1536. Chacun a coûté un aller-retour pour être découvert.
///
/// Les prefabs sont **instanciés sous un Canvas** pour être mesurés : hors
/// Canvas, tous les rects valent zéro et aucune comparaison n'a de sens. C'est
/// la même leçon qui a corrompu deux fois `BatimentPrefab` dans la journée.
public class DimensionsTests
{
    /// En dessous, l'écart relève du pixel près et non d'un contenu écrasé.
    const float SeuilHauteur = 40f;

    /// Un conteneur aussi large que son parent est censé le remplir ; plus étroit,
    /// c'est un panneau centré, dont la largeur fixe est voulue.
    const float PartDeLargeur = 0.95f;

    static IEnumerable<string> Chemins()
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p);
    }

    static string Chemin(Transform t, Transform racine)
    {
        string p = t.name;
        for (var h = t.parent; h != null && h != racine; h = h.parent) p = h.name + "/" + p;
        return p;
    }

    /// Instancie chaque prefab sous un Canvas jetable et laisse l'inspecteur
    /// mesurer ce qu'il veut, sur des valeurs réelles.
    static void PourChaquePrefab(System.Action<GameObject, string> inspecter)
    {
        var canvasGO = new GameObject("CanvasDeTest", typeof(Canvas), typeof(CanvasScaler));
        try
        {
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ((RectTransform)canvasGO.transform).sizeDelta = new Vector2(1920f, 1080f);

            foreach (var chemin in Chemins())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(chemin);
                if (prefab == null) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGO.transform);
                try
                {
                    var rt = (RectTransform)inst.transform;
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

                    inspecter(inst, System.IO.Path.GetFileNameWithoutExtension(chemin));
                }
                finally { Object.DestroyImmediate(inst); }
            }
        }
        finally { Object.DestroyImmediate(canvasGO); }
    }

    /// Une hauteur imposée plus petite que ce que ses enfants demandent.
    ///
    /// C'est le défaut d'« Objectif » : 120 px imposés, 275 demandés. Le layout
    /// n'en sait rien, le ScrollRect croit n'avoir rien à faire défiler, et le
    /// contenu dépasse hors de sa section sans que rien ne l'indique.
    [Test]
    public void Aucune_hauteur_imposee_ne_ment_sur_son_contenu()
    {
        var fautifs = new List<string>();

        PourChaquePrefab((inst, nom) =>
        {
            foreach (var le in inst.GetComponentsInChildren<LayoutElement>(true))
            {
                if (!le.gameObject.activeInHierarchy) continue;
                float impose = Mathf.Max(le.preferredHeight, le.minHeight);
                if (impose <= 0f) continue;

                // Seul un empilement vertical permet la comparaison : ailleurs,
                // la hauteur ne se déduit pas de la somme des enfants.
                var v = le.GetComponent<VerticalLayoutGroup>();
                var rt = (RectTransform)le.transform;
                if (v == null || rt.childCount == 0) continue;

                float demande = v.padding.top + v.padding.bottom;
                int n = 0;
                foreach (Transform e in rt)
                {
                    if (!e.gameObject.activeSelf) continue;
                    demande += LayoutUtility.GetPreferredHeight((RectTransform)e);
                    n++;
                }
                if (n > 1) demande += v.spacing * (n - 1);

                float ecart = demande - impose;
                if (ecart < SeuilHauteur) continue;

                fautifs.Add($"{nom} : {Chemin(le.transform, inst.transform)}"
                          + $" impose {impose:F0} px pour {demande:F0} demandes (+{ecart:F0})");
            }
        });

        Assert.That(fautifs, Is.Empty,
            "hauteur figée plus petite que son contenu — il débordera sans que rien ne le signale :\n  "
            + string.Join("\n  ", fautifs));
    }

    /// Un conteneur censé remplir son parent mais figé à une largeur.
    ///
    /// Tant que l'écran fait exactement cette largeur, cela tombe juste par
    /// coïncidence. Au premier écran plus étroit — ou dès que le réglage de
    /// taille d'affichage monte — il déborde.
    [Test]
    public void Aucun_conteneur_pleine_largeur_ne_reste_fige()
    {
        var fautifs = new List<string>();

        PourChaquePrefab((inst, nom) =>
        {
            foreach (var rt in inst.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt == inst.transform) continue;
                if (!rt.gameObject.activeInHierarchy) continue;
                if (!Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x)) continue;   // déjà étiré

                var pere = rt.parent as RectTransform;
                if (pere == null || pere.rect.width < 1f) continue;
                if (rt.rect.width < PartDeLargeur * pere.rect.width) continue;   // panneau centré
                // Au-dessous, c'est une carte ou une colonne : sa largeur vient
                // de son propre conteneur, pas de l'écran, et la figer est sans
                // conséquence quand l'affichage change de taille.
                if (rt.rect.width < 1000f) continue;

                // Sous un ScrollRect, c'est le scroll qui gère la largeur.
                bool sousScroll = false;
                for (var t = rt.parent; t != null; t = t.parent)
                    if (t.GetComponent<ScrollRect>() != null) { sousScroll = true; break; }
                if (sousScroll) continue;

                // Deux façons correctes de suivre le parent : un LayoutElement qui
                // réclame la place, ou un parent qui contrôle et étire.
                var le = rt.GetComponent<LayoutElement>();
                if (le != null && le.flexibleWidth > 0f) continue;
                var lgp = pere.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (lgp != null && lgp.childControlWidth && lgp.childForceExpandWidth) continue;

                fautifs.Add($"{nom} : {Chemin(rt, inst.transform)}"
                          + $" fige a {rt.rect.width:F0} px dans un parent de {pere.rect.width:F0}");
            }
        });

        Assert.That(fautifs, Is.Empty,
            "largeur figée : correct tant que l'écran fait cette taille, faux ensuite :\n  "
            + string.Join("\n  ", fautifs));
    }
}
