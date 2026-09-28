using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Les rangées de colonnes des fiches, au zoom maximal et à 100 %.
///
/// À 175 %, « Informations générales » débordait sous « Rentabilité » et les
/// tuiles de rentabilité se chevauchaient : deux colonnes qui annonçaient des
/// minimums faux, serrées côte à côte dans 1092 unités. ColonnesAdaptatives les
/// empile désormais ; ces tests vérifient qu'elles ne débordent plus, et qu'à
/// 100 % elles restent bien côte à côte.
public class ColonnesAdaptativesTests
{
    static readonly string[] Chemins =
    {
        "Assets/Prefab/BatimentPrefab.prefab",
        "Assets/Prefab/LocatairePrefab.prefab",
    };

    [Test]
    public void Au_zoom_maximal_aucune_colonne_ne_deborde()
    {
        var fautifs = new List<string>();
        Disposer(UIZoom.Max, (nom, colonnes) =>
        {
            // La fiche entière doit tenir dans l'écran. Dans l'application, elle est
            // logée dans un groupe vertical : si son minimum dépasse, elle déborde des
            // deux côtés (fiche locataire, 175 % : 1188 exigées pour 1077).
            var racine = colonnes[0];
            while (racine.parent != racine.root) racine = (RectTransform)racine.parent;   // la fiche, sous le Canvas
            float ecran = ((RectTransform)racine.root).rect.width;
            float exigee = LayoutUtility.GetMinWidth(racine);
            if (exigee > ecran + 1f)
                fautifs.Add($"[{nom}] la fiche exige {exigee:F0} pour un écran de {ecran:F0}");

            foreach (var col in colonnes)
            {
                // La rangée la plus exigeante de la colonne doit tenir dans sa largeur.
                float plusLarge = col.GetComponentsInChildren<HorizontalLayoutGroup>()
                    .Select(h => LayoutUtility.GetMinWidth((RectTransform)h.transform))
                    .DefaultIfEmpty(0f).Max();
                if (plusLarge > col.rect.width + 1f)
                    fautifs.Add($"[{nom}] {col.name} : {col.rect.width:F0} de large, une rangée en exige {plusLarge:F0}");
            }
        });
        Assert.That(fautifs, Is.Empty, "au zoom maximal :\n  " + string.Join("\n  ", fautifs));
    }

    [Test]
    public void A_100_pourcent_les_colonnes_restent_cote_a_cote()
    {
        var empilees = new List<string>();
        Disposer(1f, (nom, colonnes) =>
        {
            foreach (var col in colonnes)
                if (col.parent.GetComponent<ColonnesAdaptatives>() == null)
                    empilees.Add($"[{nom}] {col.name}");
        });
        Assert.That(empilees, Is.Empty, "empilées à 100 % :\n  " + string.Join("\n  ", empilees));
    }

    /// Instancie chaque fiche sous un Canvas à la largeur du zoom demandé, laisse
    /// ColonnesAdaptatives décider, puis fait inspecter les colonnes mises en page.
    static void Disposer(float zoom, System.Action<string, List<RectTransform>> inspecter)
    {
        var canvasGO = new GameObject("CanvasDeTest", typeof(Canvas), typeof(CanvasScaler));
        try
        {
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            ((RectTransform)canvasGO.transform).sizeDelta = new Vector2(1920f, 1080f) / zoom;
            foreach (var chemin in Chemins)
            {
                var inst = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(chemin), canvasGO.transform);
                var rt = (RectTransform)inst.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                foreach (var rangee in inst.GetComponentsInChildren<ColonnesAdaptatives>(true))
                {
                    for (var p = rangee.transform; p != null; p = p.parent) p.gameObject.SetActive(true);
                    Mettre(rt);
                    var colonnes = rangee.transform.Cast<Transform>()
                        .Where(c => c.gameObject.activeSelf).Select(c => (RectTransform)c).ToList();
                    rangee.Disposer();
                    Mettre(rt);
                    inspecter(System.IO.Path.GetFileNameWithoutExtension(chemin), colonnes);
                }
            }
        }
        finally { Object.DestroyImmediate(canvasGO); }
    }

    static void Mettre(RectTransform rt)
    {
        for (int i = 0; i < 3; i++) { Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(rt); }
    }
}
