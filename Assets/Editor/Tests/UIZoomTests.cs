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

    /// Menus déroulants au zoom 175 % (07/10) : PlacePopup posait la position du champ en
    /// pixels d'écran comme si c'étaient des unités d'interface ; le menu partait en haut à
    /// droite. Il doit s'ouvrir collé sous son champ, quelle que soit l'échelle du canevas.
    [Test]
    public void Un_menu_deroulant_s_ouvre_sous_son_champ_au_zoom_175()
    {
        var racine = new GameObject("RacineZoom", typeof(RectTransform));
        try
        {
            var rr = (RectTransform)racine.transform;
            rr.sizeDelta = new Vector2(1097f, 617f);           // 1920 × 1080 à 175 %
            rr.localScale = Vector3.one * 1.75f;              // échelle d'un canevas zoomé
            var voile = UIFactory.Rect("Voile", racine.transform);
            UIFactory.Stretch(voile);
            var champ = UIFactory.Rect("Champ", racine.transform);
            champ.anchorMin = champ.anchorMax = new Vector2(.5f, .5f);
            champ.sizeDelta = new Vector2(280, 44);
            champ.anchoredPosition = new Vector2(-100, 20);
            var menu = UIFactory.Rect("Menu", voile);
            menu.anchorMin = menu.anchorMax = Vector2.zero;
            menu.sizeDelta = new Vector2(280, 120);

            UIFactory.PlacePopup(menu, champ, 6f);

            var c = new Vector3[4]; champ.GetWorldCorners(c);
            var m = new Vector3[4]; menu.GetWorldCorners(m);
            Assert.That(Vector3.Distance(m[1], c[0]), Is.LessThan(0.5f),
                $"le haut-gauche du menu {m[1]} doit être au bas-gauche du champ {c[0]}");
        }
        finally { Object.DestroyImmediate(racine); }
    }

    [Test]
    public void Tous_les_paliers_proposes_sont_valides()
    {
        foreach (float p in UIZoom.Paliers)
            Assert.That(UIZoom.Borner(p), Is.EqualTo(p).Within(0.001f),
                $"le palier {p} sort des bornes du réglage");
    }
}
