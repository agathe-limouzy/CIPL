using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Ligne dense d'un bâtiment (mode liste, ≥ 11 bâtiments).
public class BuildingRowUI : MonoBehaviour
{
    public Image pastille;
    public TMP_Text txtNom;
    public TMP_Text txtVille;
    public TMP_Text txtLots;
    public TMP_Text txtLoyer;
    public TMP_Text txtEtat;
    public Button btnOuvrir;

    public void Setup(BatimentPrefab bp, Action<BatimentPrefab> onClick)
    {
        var data = bp.getBatiment();
        var etat = BatimentEtatHelper.GetEtat(bp);

        if (pastille != null) pastille.color = BatimentEtatHelper.Couleur(etat);
        if (txtNom != null) txtNom.text = string.IsNullOrEmpty(data.Name) ? "Sans nom" : data.Name;
        if (txtVille != null) txtVille.text = Ville(data.adressBatiment);
        if (txtLots != null) txtLots.text = bp.listLocataire.Count.ToString();
        if (txtLoyer != null) txtLoyer.text = $"{bp.GetLoyerTotal():N0} €";
        if (txtEtat != null)
        {
            txtEtat.text = LabelEtat(etat);
            txtEtat.color = etat == BatimentEtat.AJour ? UITheme.TexteSecondaire
                                                       : BatimentEtatHelper.Couleur(etat);
        }

        if (btnOuvrir != null)
        {
            btnOuvrir.onClick.RemoveAllListeners();
            btnOuvrir.onClick.AddListener(() => onClick?.Invoke(bp));
        }
    }

    private static string LabelEtat(BatimentEtat e) => e switch
    {
        BatimentEtat.Retard => "Retard",
        BatimentEtat.Bientot => "Révision proche",
        BatimentEtat.Vacant => "Lot vacant",
        _ => "À jour"
    };

    // Extrait grossièrement la ville d'une adresse "12 rue X, Ville"
    public static string Ville(string adresse)
    {
        if (string.IsNullOrEmpty(adresse)) return "—";
        var parts = adresse.Split(',');
        string derniere = parts[parts.Length - 1].Trim();
        // Retire un éventuel code postal en tête
        var mots = derniere.Split(' ');
        if (mots.Length > 1 && mots[0].Length == 5 && int.TryParse(mots[0], out _))
            return string.Join(" ", mots, 1, mots.Length - 1);
        return string.IsNullOrEmpty(derniere) ? "—" : derniere;
    }
}
