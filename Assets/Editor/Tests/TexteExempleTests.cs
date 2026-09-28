using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

/// Le texte d'exemple des champs vides (placeholder).
///
/// Il n'est jamais enregistré : il doit donc se voir comme un exemple, pas comme
/// une valeur. Le 28/09, on en comptait sept couleurs différentes, certaines
/// aussi foncées qu'une valeur saisie (« JJ / MM / AAAA » était opaque), et
/// 42 « Enter text... » en anglais. Désormais : une couleur, UITheme.TexteExemple,
/// en italique, et « Saisir… » par défaut.
public class TexteExempleTests
{
    [Test]
    public void Tout_texte_d_exemple_des_prefabs_a_le_meme_style()
    {
        var fautifs = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" }))
        {
            string chemin = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var f in AssetDatabase.LoadAssetAtPath<GameObject>(chemin).GetComponentsInChildren<TMP_InputField>(true))
            {
                if (!(f.placeholder is TMP_Text ph)) continue;
                string ou = $"[{Path.GetFileNameWithoutExtension(chemin)}] {f.name}";
                if (ph.text == "Enter text...") fautifs.Add($"{ou} : « Enter text... »");
                if (ph.color != UITheme.TexteExemple) fautifs.Add($"{ou} : couleur #{ColorUtility.ToHtmlStringRGBA(ph.color)}");
                if ((ph.fontStyle & FontStyles.Italic) == 0) fautifs.Add($"{ou} : pas en italique");
            }
        }
        Assert.That(fautifs, Is.Empty, "texte d'exemple hors style — UITheme.TexteExemple, italique :\n  " + string.Join("\n  ", fautifs));
    }

    [Test]
    public void La_scene_n_affiche_plus_Enter_text()
    {
        string scene = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes/SampleScene.unity"));
        Assert.That(scene, Does.Not.Contain("Enter text..."));
    }
}
