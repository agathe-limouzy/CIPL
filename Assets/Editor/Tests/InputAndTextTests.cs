using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

/// Champ lecture / saisie des fiches. Défaut du 30/09 : sur une fiche construite
/// inactive (nouveau bâtiment, nouveau locataire), Awake passait APRÈS Modify() et
/// remettait le champ en lecture seule — impossible de saisir le nom.
public class InputAndTextTests
{
    static InputAndText Champ(out GameObject racine)
    {
        racine = new GameObject("Champ");
        var champ = racine.AddComponent<InputAndText>();
        champ.textSaved = new GameObject("Texte").AddComponent<TextMeshProUGUI>();
        champ.textSaved.transform.SetParent(racine.transform);
        var saisie = new GameObject("Saisie");
        saisie.transform.SetParent(racine.transform);
        champ.inputModify = saisie.AddComponent<TMP_InputField>();
        return champ;
    }

    // Awake n'est pas appelé par Unity en mode Édition : on le déclenche comme le
    // ferait la première activation de la fiche.
    static void Awake(InputAndText champ)
        => typeof(InputAndText).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(champ, null);

    [Test]
    public void Un_champ_mis_en_saisie_avant_son_Awake_reste_en_saisie()
    {
        var champ = Champ(out var racine);
        champ.Modify();          // fiche neuve : mise en modification…
        Awake(champ);            // … puis première activation de l'onglet
        Assert.That(champ.inputModify.gameObject.activeSelf, Is.True);
        Assert.That(champ.textSaved.gameObject.activeSelf, Is.False);
        Object.DestroyImmediate(racine);
    }

    [Test]
    public void Un_champ_jamais_regle_est_en_lecture_seule_au_repos()
    {
        // Le correctif du 22/09 (valeur affichée deux fois) doit tenir.
        var champ = Champ(out var racine);
        Awake(champ);
        Assert.That(champ.inputModify.gameObject.activeSelf, Is.False);
        Assert.That(champ.textSaved.gameObject.activeSelf, Is.True);
        Object.DestroyImmediate(racine);
    }
}
