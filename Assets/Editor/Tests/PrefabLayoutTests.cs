using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Trois règles de mise en page, figées sur *tous* les prefabs.
///
/// Elles viennent chacune d'un défaut réellement constaté, pas d'un principe :
/// le nettoyage du 23/09 a cassé la fiche bâtiment, qui s'est affichée entièrement
/// blanche. La cause tenait en une ligne — un groupe passé en `childControlHeight`
/// alors que son enfant est un `ScrollRect`, qui ne sait pas dire sa hauteur.
///
/// Ces tests existent parce qu'aucune de ces règles n'était écrite nulle part :
/// elles vivaient dans la tête de celui qui venait de les découvrir, ce qui est
/// exactement la situation que la revue de code reproche au reste du projet.
public class PrefabLayoutTests
{
    static IEnumerable<GameObject> Prefabs()
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(go => go != null);
    }

    static string Chemin(Transform t)
    {
        string p = t.name;
        for (var h = t.parent; h != null; h = h.parent) p = h.name + "/" + p;
        return p;
    }

    /// Le défaut du 23/09, celui qui a vidé l'écran — deux fois, pour deux raisons.
    ///
    /// Un `ScrollRect` n'a pas de hauteur préférée : il est dimensionné par son parent,
    /// jamais par son contenu. Le conteneur qui l'enveloppe annonce donc 0, et le groupe
    /// au-dessus doit lui donner sa hauteur autrement — par `childForceExpandHeight`, ou
    /// par un `LayoutElement` posé sur le maillon.
    ///
    /// Sans l'un des deux, la section s'effondre : `Information Batiment` est tombé à
    /// 10 px (son seul padding) et `ContentLocataire` à 0, chacun entraînant tout le
    /// panneau. Le second cas s'est caché au premier correctif parce que le ScrollRect
    /// n'était pas l'enfant *direct* du groupe — d'où la remontée de chaîne ici.
    [Test]
    public void Tout_conteneur_de_scroll_peut_recevoir_une_hauteur()
    {
        var fautifs = new List<string>();

        foreach (var prefab in Prefabs())
        foreach (var sr in prefab.GetComponentsInChildren<ScrollRect>(true))
        {
            // Le Template d'un dropdown est désactivé et positionné à la main à
            // l'ouverture de la liste : il ne participe pas au layout.
            if (sr.GetComponentInParent<TMP_Dropdown>(true) != null) continue;
            if (sr.GetComponentInParent<Dropdown>(true) != null) continue;

            // On remonte jusqu'au premier groupe qui contrôle la hauteur.
            Transform maillon = sr.transform;
            HorizontalOrVerticalLayoutGroup groupe = null;
            while (maillon.parent != null)
            {
                var lg = maillon.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (lg != null && lg.childControlHeight) { groupe = lg; break; }
                maillon = maillon.parent;
            }
            if (groupe == null || groupe.childForceExpandHeight) continue;

            var le = maillon.GetComponent<LayoutElement>();
            if (le != null && (le.preferredHeight >= 0 || le.minHeight >= 0)) continue;

            fautifs.Add($"{prefab.name} : {Chemin(groupe.transform)}"
                      + $" contrôle « {maillon.name} » qui n'enveloppe qu'un scroll");
        }

        Assert.That(fautifs, Is.Empty,
            "ni expansion ni LayoutElement : le conteneur de scroll tombera à zéro :\n  "
            + string.Join("\n  ", fautifs));
    }

    /// TMP implémente `ILayoutElement` : un retour à la ligne final réclame une ligne
    /// de plus. Cent vingt textes en portaient un — d'où les blancs entre un titre et
    /// son contenu, cherchés trois fois avant d'être mesurés.
    [Test]
    public void Aucun_texte_ne_finit_par_un_retour_a_la_ligne()
    {
        var fautifs = new List<string>();

        foreach (var prefab in Prefabs())
        foreach (var tmp in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            string s = tmp.text ?? "";
            if (s.Trim().Length == 0) continue;
            if (s == s.TrimEnd('\n', '\r', ' ', '\t')) continue;

            fautifs.Add($"{prefab.name} : {Chemin(tmp.transform)}");
        }

        Assert.That(fautifs, Is.Empty,
            "un blanc final fait réclamer une ligne de plus à TMP :\n  "
            + string.Join("\n  ", fautifs));
    }

    /// Un override qui répète mot pour mot le texte de son prefab source ne change rien
    /// à l'écran, mais coupe l'instance de sa source : modifier le libellé d'origine ne
    /// se propage plus. Soixante-cinq s'étaient accumulés, dont cinquante-et-un créés
    /// par la passe de nettoyage elle-même.
    [Test]
    public void Aucun_override_de_texte_ne_repete_sa_source()
    {
        var fautifs = new List<string>();

        foreach (var prefab in Prefabs())
        foreach (var tmp in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(tmp)) continue;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(tmp) as TMP_Text;
            if (source == null || source.text != tmp.text) continue;

            var prop = new SerializedObject(tmp).FindProperty("m_text");
            if (prop == null || !prop.prefabOverride) continue;

            fautifs.Add($"{prefab.name} : {Chemin(tmp.transform)}");
        }

        Assert.That(fautifs, Is.Empty,
            "override identique à la source — l'instance ne suivra plus son prefab :\n  "
            + string.Join("\n  ", fautifs));
    }
}
