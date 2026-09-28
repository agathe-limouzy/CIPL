using NUnit.Framework;
using UnityEngine;

/// Le réglage de taille d'affichage.
///
/// Le calcul est court mais contre-intuitif : on **divise** la résolution de
/// référence pour **agrandir** l'interface. Une inversion de ce rapport
/// rétrécirait tout au lieu de grossir, sans rien casser d'autre — donc sans
/// que rien ne le signale. D'où ces tests.
public class UIZoomTests
{
    static readonly Vector2 Origine = new Vector2(1920f, 1080f);

    [Test]
    public void Grossir_reduit_la_resolution_de_reference()
    {
        var r = UIZoom.Reference(Origine, 1.25f);
        Assert.That(r.x, Is.EqualTo(1536f).Within(0.01f), "largeur");
        Assert.That(r.y, Is.EqualTo(864f).Within(0.01f), "hauteur");
    }

    [Test]
    public void Reduire_augmente_la_resolution_de_reference()
    {
        var r = UIZoom.Reference(Origine, 0.9f);
        Assert.That(r.x, Is.GreaterThan(Origine.x), "réduire l'affichage élargit la référence");
    }

    [Test]
    public void Le_facteur_neutre_ne_change_rien()
    {
        Assert.That(UIZoom.Reference(Origine, 1f), Is.EqualTo(Origine));
    }

    [Test]
    public void Le_facteur_reste_dans_les_bornes()
    {
        Assert.That(UIZoom.Borner(5f), Is.EqualTo(UIZoom.Max), "au-dessus du maximum");
        Assert.That(UIZoom.Borner(0.1f), Is.EqualTo(UIZoom.Min), "en dessous du minimum");

        // Une valeur absurde en PlayerPrefs (clé écrasée, fichier corrompu) ne doit
        // pas rendre l'interface inutilisable : on revient à la taille normale.
        Assert.That(UIZoom.Borner(0f), Is.EqualTo(1f), "zéro");
        Assert.That(UIZoom.Borner(-2f), Is.EqualTo(1f), "négatif");
        Assert.That(UIZoom.Borner(float.NaN), Is.EqualTo(1f), "NaN");
    }

    [Test]
    public void Tous_les_paliers_proposes_sont_valides()
    {
        foreach (float p in UIZoom.Paliers)
            Assert.That(UIZoom.Borner(p), Is.EqualTo(p).Within(0.001f),
                $"le palier {p} sort des bornes du réglage");
    }
}
