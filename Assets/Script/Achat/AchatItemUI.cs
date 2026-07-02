using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchatItemUI : MonoBehaviour
{
    public TMP_Text txtLabel;
    public TMP_Text txtPrix;
    public TMP_Text txtMensualite;
    public TMP_Text txtDate;
    public Button btnEdit;
    public Button btnDelete;

    public void Setup(AchatFinancement data, Action onEdit, Action onDelete)
    {
        txtLabel.text = string.IsNullOrEmpty(data.label) ? "Achat" : data.label;
        txtPrix.text = $"{data.prixAchat + data.fraisNotaire + data.fraisAgence:N0} €";
        txtMensualite.text = data.emprunt
            ? $"{RentabiliteCalculator.Mensualite(data.montantEmprunte, data.tauxInteretAnnuel, data.dureeMois):N0} € / mois"
            : "Comptant";
        txtDate.text = string.IsNullOrEmpty(data.dateAchat) ? "—" : data.dateAchat;

        btnEdit.onClick.RemoveAllListeners();
        btnEdit.onClick.AddListener(() => onEdit());
        btnDelete.onClick.RemoveAllListeners();
        btnDelete.onClick.AddListener(() => onDelete());
    }
}