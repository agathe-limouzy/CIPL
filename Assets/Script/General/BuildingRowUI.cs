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
        if (txtLots != null) txtLots.text = bp.NbEnPlace().ToString();   // Revue 17 (D7a-10, lot F) : comme la carte (partis et archivés exclus)
        if (txtLoyer != null) txtLoyer.text = $"{Montants.F(bp.GetLoyerTotal(), "N0")} €";
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
        // Garde ce qui suit le code postal. Revue 17 (D8-19, lot G) : où qu'il soit : une adresse sans virgule
        // (« 12 rue de Rivoli 75001 Paris ») donnait l'adresse entière.
        var mots = derniere.Split(' ');
        int cp = System.Array.FindIndex(mots, m => m.Length == 5 && int.TryParse(m, out _));
        if (cp >= 0 && cp < mots.Length - 1)
            return string.Join(" ", mots, cp + 1, mots.Length - cp - 1);
        // Revue visuelle (V-38, décision du 17/11) : ni virgule ni code postal, et un numéro de rue ou plus de trois mots
        // (« 56 rue des tourneurs toulouse ») : la ville ne se devine pas. Avant : l'adresse entière, coupée sur la carte.
        if (parts.Length == 1 && derniere.Length > 0 && (mots.Length > 3 || char.IsDigit(derniere[0]))) return "—";
        return string.IsNullOrEmpty(derniere) ? "—" : derniere;
    }

    /// La ligne du bas de la carte d'un bâtiment : « Ville · N lots », ou « N lots » seul quand la ville n'est pas connue
    /// (adresse vide, ou sans code postal ni virgule).
    public static string VilleEtLots(string adresse, int lots)
    {
        string ville = Ville(adresse), n = $"{lots} lot{(lots > 1 ? "s" : "")}";
        return ville == "—" ? n : $"{ville} · {n}";
    }
}
