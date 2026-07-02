using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TravauxItemUI : MonoBehaviour
{
    public TMP_Text txtDescription;
    public TMP_Text txtCout;
    public TMP_Text txtMensualite;
    public TMP_Text txtDate;
    public Button btnEdit;
    public Button btnDelete;

    public void Setup(TravauxFinancement data, Action onEdit, Action onDelete)
    {
        txtDescription.text = string.IsNullOrEmpty(data.description) ? "Travaux" : data.description;
        txtCout.text = $"{data.coutTotal:N0} €";
        txtMensualite.text = data.emprunt
            ? $"{RentabiliteCalculator.Mensualite(data.montantEmprunte, data.tauxInteretAnnuel, data.dureeMois):N0} € / mois"
            : "Comptant";
        txtDate.text = string.IsNullOrEmpty(data.dateDebutTravaux) ? "—" : data.dateDebutTravaux;

        btnEdit.onClick.RemoveAllListeners();
        btnEdit.onClick.AddListener(() => onEdit());
        btnDelete.onClick.RemoveAllListeners();
        btnDelete.onClick.AddListener(() => onDelete());
    }
}