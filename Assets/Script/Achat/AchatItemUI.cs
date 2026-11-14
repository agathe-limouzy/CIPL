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
        txtDate.text = $"{DateLisible(data.dateAchat)} · {(data.emprunt ? "emprunt" : "comptant")}";

        btnEdit.onClick.RemoveAllListeners();
        btnEdit.onClick.AddListener(() => onEdit());
        btnDelete.onClick.RemoveAllListeners();
        btnDelete.onClick.AddListener(() => onDelete());
    }

    /// Revue 17 (D7b-18, lot D) : la liste montrait la date brute « 2026-11-09 » : jj/mm/aaaa, « — » si absente ou illisible.
    public static string DateLisible(string iso)
        => FacturationSuivi.TryEcheance(iso, out var d) ? d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) : "—";
}