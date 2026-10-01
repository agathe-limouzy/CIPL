using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Parcours de création d'un locataire (demande utilisatrice du 30/09) : Général, puis
/// Bail, puis Loyer, puis Dépôt de garantie — dans cet ordre, pour qu'aucun écran ne
/// travaille sur des données manquantes (un loyer sans dates de bail, un dépôt sans
/// loyer). L'état se déduit de la fiche enregistrée : rien n'est stocké.
public static class ParcoursLocataire
{
    public enum Etape { General, Bail, Loyer, Depot, Termine }

    /// Un nom saisi — pas le « Nouveau » que porte toute fiche à sa création.
    public static bool GeneralRempli(Locataire loc)
        => !string.IsNullOrWhiteSpace(loc?.Name) && loc.Name != Data.NomParDefaut;

    /// Dates de début et de fin lisibles, la fin après le début. Un bail jamais saisi
    /// était enregistré au « 01/01/0001 » (date par défaut) : il ne compte pas.
    public static bool BailRempli(Locataire loc)
        => loc != null
        && FacturationSuivi.TryEcheance(loc.dateDebutBailISO, out var debut) && debut.Year > 1900
        && FacturationSuivi.TryEcheance(loc.dateFinBailISO, out var fin) && fin > debut;

    public static Etape Prochaine(Locataire loc)
        => !GeneralRempli(loc) ? Etape.General
         : !BailRempli(loc) ? Etape.Bail
         : !loc.LoyerInitialise ? Etape.Loyer
         : !loc.DepotInitialise ? Etape.Depot
         : Etape.Termine;

    /// Ouvrir l'étape `voulue` avant les précédentes : le message à afficher, sinon null.
    public static string Bloque(Locataire loc, Etape voulue)
    {
        var p = Prochaine(loc);
        return p < voulue ? Consigne(p) : null;
    }

    /// Facturer avant la fin du parcours : le message à afficher, sinon null. Une
    /// facture s'appuie sur tout le reste (bail, loyer, dépôt) : rien avant.
    public static string FacturationBloquee(Locataire loc)
    {
        var p = Prochaine(loc);
        return p == Etape.Termine ? null : "Facturation possible une fois le locataire complet. " + Consigne(p);
    }

    public static string Consigne(Etape e)
    {
        switch (e)
        {
            case Etape.General: return "Étape 1 — Général : saisissez au moins le nom du locataire, puis enregistrez.";
            case Etape.Bail:    return "Étape 2 — Bail : saisissez les dates de début et de fin du bail, puis enregistrez.";
            case Etape.Loyer:   return "Étape 3 — Loyer : initialisez le loyer.";
            case Etape.Depot:   return "Étape 4 — Dépôt de garantie : initialisez le dépôt.";
            default:            return "";
        }
    }
}

/// Bandeau du parcours, en tête de fiche, tant que le locataire n'est pas complet :
/// les quatre étapes (faite / en cours / à venir), la consigne, et « Continuer » qui
/// ouvre l'écran de l'étape en cours.
public class ParcoursLocataireUI : MonoBehaviour
{
    static readonly string[] Titres = { "1  Général", "2  Bail", "3  Loyer", "4  Dépôt de garantie" };
    static readonly Color Ambre = Hex("#A9741C");

    LocatairePrefab _fiche;
    GameObject _bandeau;
    readonly Image[] _pastilles = new Image[4];
    readonly TMP_Text[] _libelles = new TMP_Text[4];
    TMP_Text _consigne;
    Button _continuer;

    public void EnsureBuilt(LocatairePrefab fiche)
    {
        if (_bandeau != null) return;
        _fiche = fiche;

        var fond = UIFactory.Panel("ParcoursLocataire", fiche.transform, Hex("#E7ECF2"));
        UIFactory.Border(fond.gameObject, Hex("#5C6E85"));
        var header = fiche.transform.Find("HeaderLocataire");
        fond.transform.SetSiblingIndex(header != null ? header.GetSiblingIndex() + 1 : 0);
        var v = fond.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(16, 16, 10, 12); v.spacing = 8;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        _bandeau = fond.gameObject;

        UIFactory.Text(fond.transform, "Nouveau locataire — à remplir dans l'ordre", UITheme.Role.Libelle, Hex("#5C6E85"), true);

        var etapes = UIFactory.HBox(fond.transform, 8, true, "Etapes");
        for (int i = 0; i < 4; i++)
        {
            var p = UIFactory.Panel("Etape", etapes.transform, UITheme.Carte);
            UIFactory.LE(p.gameObject, minH: 34, prefH: 34, flexW: 1);
            _libelles[i] = UIFactory.Text(p.transform, Titres[i], UITheme.Role.Action, UITheme.TexteSecondaire, true, TextAlignmentOptions.Center);
            UIFactory.Stretch((RectTransform)_libelles[i].transform, 8, 0, 8, 0);
            _pastilles[i] = p;
        }

        var bas = UIFactory.HBox(fond.transform, 12, false, "Consigne");
        _consigne = UIFactory.Text(bas.transform, "", UITheme.Role.Donnee, UITheme.TextePrincipal);
        _consigne.enableWordWrapping = true;
        UIFactory.LE(_consigne.gameObject, flexW: 1);
        _continuer = UIFactory.Button(bas.transform, "Continuer", Ambre, Color.white, 36, UITheme.Role.Action);
        UIFactory.LargeurDuTexte(_continuer);
        _continuer.onClick.AddListener(() => _fiche.ContinuerParcours());

        _bandeau.SetActive(false);
    }

    /// `etape` : l'étape en cours, calculée par la fiche — pendant la saisie, d'après ce
    /// qui est tapé (le bandeau avance au fil de la frappe). `enCreation` : la fiche
    /// vient d'être créée (sinon le bandeau ne s'affiche que pour un locataire nommé —
    /// une fiche sans nom est un lot vacant).
    public void Refresh(Locataire loc, ParcoursLocataire.Etape etape, bool enCreation, bool enModification)
    {
        if (_bandeau == null || loc == null) return;
        bool visible = etape != ParcoursLocataire.Etape.Termine && (enCreation || ParcoursLocataire.GeneralRempli(loc));
        _bandeau.SetActive(visible);
        if (!visible) return;

        for (int i = 0; i < 4; i++)
        {
            bool faite = i < (int)etape, courante = i == (int)etape;
            _pastilles[i].color = faite ? UITheme.PrimaireClair : courante ? Ambre : UITheme.Carte;
            _libelles[i].color = faite ? UITheme.Primaire : courante ? Color.white : UITheme.TexteSecondaire;
            _libelles[i].text = faite ? $"✓ {Titres[i].Substring(3)}" : Titres[i];
        }

        // Pendant la saisie de la fiche, la consigne suit la frappe ; « Continuer » ne
        // sert qu'hors saisie, pour ouvrir l'écran de l'étape (loyer, dépôt).
        if (!enModification) _consigne.text = ParcoursLocataire.Consigne(etape);
        else if (etape == ParcoursLocataire.Etape.General)
            _consigne.text = "Étape 1 — Général : saisissez au moins le nom du locataire.";
        else if (etape == ParcoursLocataire.Etape.Bail)
            _consigne.text = "Étape 2 — Bail : saisissez les dates de début et de fin du bail.";
        else
            _consigne.text = "Cliquez sur « Sauvegarder », puis : " + ParcoursLocataire.Consigne(etape);
        _continuer.gameObject.SetActive(!enModification);
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
