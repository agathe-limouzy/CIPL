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



    [Header("Alerte")]
    public GameObject alertBadge;   // pastille rouge, optionnelle

    public void SetAlert(bool on)
    {
        if (alertBadge != null) alertBadge.SetActive(on);
    }



    public void Setup(PrefabBatLoc prefabBatLoc, MenuManager MainMenu)
    {
        _BatLocLinked = prefabBatLoc;


        tabLabel.text = prefabBatLoc.getName();

        tabButton.onClick.AddListener(() => MainMenu.OnSelect(_BatLocLinked, this));

    }

    public void SetActive(bool active)
    {
        tabBackground.color = active ? ColorActive : ColorInactive;
        tabLabel.color = active ? UITheme.PrimaireClair : UITheme.TexteSecondaire;
        tabLabel.fontStyle = active ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
    }

    public void UpdateLabel(string label)
    {
        tabLabel.text = label;
    }
}
