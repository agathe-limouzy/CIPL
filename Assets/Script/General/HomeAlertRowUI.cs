using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Ligne de la zone « À traiter » — 4 colonnes : Type · Rappel · Locataire · Bâtiment.
public class HomeAlertRowUI : MonoBehaviour
{
    public Image pastille;
    public Image typeBadgeBg;       // fond pastel de la pastille « Type »
    public TMP_Text txtType;        // colonne « Type » (Révision, Fin de bail, Obligatoire, Rappel…)
    public TMP_Text txtRappel;      // colonne « Rappel » (texte de l'objectif ou détail)
    public TMP_Text txtLocataire;   // colonne « Locataire » (vide si c'est un objectif de bâtiment)
    public TMP_Text txtBatiment;    // colonne « Bâtiment »
    public Button btnOuvrir;

    public void Setup(HomeAlert alerte, Action<HomeAlert> onOpen)
    {
        if (pastille != null)
            pastille.color = alerte.pastille;

        if (typeBadgeBg != null) typeBadgeBg.color = alerte.typeBg;
        if (txtType != null)
        {
            txtType.text = alerte.typeTodo;
            txtType.color = alerte.typeTexte;
        }
        if (txtRappel != null) txtRappel.text = alerte.nomRappel;
        if (txtLocataire != null) txtLocataire.text = alerte.nomLocataire ?? "";  // vide pour un objectif de bâtiment
        if (txtBatiment != null) txtBatiment.text = alerte.nomBatiment;

        if (btnOuvrir != null)
        {
            btnOuvrir.onClick.RemoveAllListeners();
            btnOuvrir.onClick.AddListener(() => onOpen?.Invoke(alerte));
        }
    }
}
