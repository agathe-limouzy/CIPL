using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Ligne de la zone « À traiter ».
public class HomeAlertRowUI : MonoBehaviour
{
    public Image pastille;
    public TMP_Text txtTitre;
    public TMP_Text txtSujet;
    public TMP_Text txtBatiment;
    public Button btnOuvrir;

    public void Setup(HomeAlert alerte, Action<HomeAlert> onOpen)
    {
        if (pastille != null)
            pastille.color = alerte.pastille;

        if (txtTitre != null) txtTitre.text = alerte.titre;
        if (txtSujet != null) txtSujet.text = alerte.sujet;
        if (txtBatiment != null) txtBatiment.text = alerte.batimentNom;

        if (btnOuvrir != null)
        {
            btnOuvrir.onClick.RemoveAllListeners();
            btnOuvrir.onClick.AddListener(() => onOpen?.Invoke(alerte));
        }
    }
}
