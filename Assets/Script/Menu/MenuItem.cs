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


    // Couleurs actif/inactif
    private static readonly Color ColorActive = new Color(0.2f, 0.5f, 0.9f);
    private static readonly Color ColorInactive = new Color(0.15f, 0.15f, 0.15f);

    public PrefabBatLoc _BatLocLinked;
    




 

    public void Setup(PrefabBatLoc prefabBatLoc, MenuManager MainMenu)
    {
        _BatLocLinked = prefabBatLoc;


        tabLabel.text = prefabBatLoc.getName();

        tabButton.onClick.AddListener(() => MainMenu.OnSelect(_BatLocLinked, this));

    }

    public void SetActive(bool active)
    {
        tabBackground.color = active ? ColorActive : ColorInactive;
        tabLabel.fontStyle = active ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
    }

    public void UpdateLabel(string label)
    {
        tabLabel.text = label;
    }
}
