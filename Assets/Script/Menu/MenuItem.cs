using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuItem : MonoBehaviour
{
    [Header("UI")]
    public Button tabButton;
    public TMP_Text tabLabel;
    public Image tabBackground;


    // Couleurs actif/inactif — palette UITheme
    private static readonly Color ColorActive = UITheme.Primaire;
    private static readonly Color ColorInactive = UITheme.Carte;

    public PrefabBatLoc _BatLocLinked;
    private MenuManager _menu;   // pour lire les couleurs d'onglet propres à la barre



    [Header("Alerte")]
    public GameObject alertBadge;   // pastille rouge, optionnelle

    public void SetAlert(bool on)
    {
        if (alertBadge != null) alertBadge.SetActive(on);
    }



    public void Setup(PrefabBatLoc prefabBatLoc, MenuManager MainMenu)
    {
        _menu = MainMenu;
        _BatLocLinked = prefabBatLoc;

        string nom = prefabBatLoc.getName();
        _libelle = string.IsNullOrWhiteSpace(nom) ? "Sans nom" : nom;
        Afficher();

        tabButton.onClick.AddListener(() => MainMenu.OnSelect(_BatLocLinked, this));

    }

    public void SetActive(bool active)
    {
        if (_menu != null)
        {
            tabBackground.color = active ? _menu.tabActiveBg : _menu.tabInactiveBg;
            tabLabel.color = active ? _menu.tabActiveText : _menu.tabInactiveText;
        }
        else
        {
            tabBackground.color = active ? ColorActive : ColorInactive;
            tabLabel.color = active ? UITheme.PrimaireClair : UITheme.TexteSecondaire;
        }
        tabLabel.fontStyle = active ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
    }

    public void UpdateLabel(string label)
    {
        _libelle = string.IsNullOrWhiteSpace(label) ? "Sans nom" : label;
        Afficher();
    }

    // Étape 5 (maquette 2 : « Volteo [Marie] ») : prénom de la personne qui modifie la fiche,
    // en ambre après le nom. Texte enrichi : l'onglet n'a pas de place prévue pour une pastille.
    string _libelle, _detenteur;

    public void SetDetenteur(string prenom)
    {
        if (_detenteur == prenom) return;
        _detenteur = prenom;
        Afficher();
    }

    void Afficher()
    {
        tabLabel.text = _libelle + RegleVerrou.Marque(_detenteur);
        // Retour du 25/10 (« Exemple · Les Tilleuls » débordait) : l'onglet s'élargit pour son nom,
        // jusqu'à un maximum ; au-delà, « … ». La barre ne règle pas la largeur des onglets.
        tabLabel.overflowMode = TextOverflowModes.Ellipsis;
        var rt = (RectTransform)transform;
        float voulu = tabLabel.GetPreferredValues(tabLabel.text, 9999f, rt.rect.height).x + 36f;   // marges de l'onglet (14 + 14) et un peu d'air
        rt.sizeDelta = new Vector2(Mathf.Clamp(voulu, LargeurMin, LargeurMax), rt.sizeDelta.y);
    }

    const float LargeurMin = 160f, LargeurMax = 320f;   // 160 : la largeur du prefab
}
