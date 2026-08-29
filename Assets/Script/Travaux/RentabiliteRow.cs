using TMPro;
using UnityEngine;

public class RentabiliteRow : MonoBehaviour
{
    public TMP_Text txtAnnee;
    public TMP_Text txtRentrees;      // loyers encaissés dans l'année
    public TMP_Text txtCoutAchat;     // dépense d'achat de l'année
    public TMP_Text txtCoutTravaux;   // dépense de travaux de l'année
    public TMP_Text txtCumulatif;     // bilan cumulé (rentrées − coûts) depuis le début

    private static readonly Color Vert   = UITheme.Primaire;   // argent qui entre / positif
    private static readonly Color Rouge  = UITheme.Alerte;     // argent qui sort / négatif
    private static readonly Color Seuil  = UITheme.Attention;  // année où le cumulé passe positif
    private static readonly Color Neutre = new Color(0.55f, 0.55f, 0.53f);

    public void Setup(int annee, float rentrees, float coutAchat, float coutTravaux,
        float cumul, bool seuilAtteint)
    {
        if (txtAnnee != null) txtAnnee.text = annee.ToString();

        SetMontant(txtRentrees, rentrees, Vert, negatif: false);
        SetMontant(txtCoutAchat, coutAchat, Rouge, negatif: true);
        SetMontant(txtCoutTravaux, coutTravaux, Rouge, negatif: true);

        if (txtCumulatif != null)
        {
            txtCumulatif.text = (cumul >= 0f ? "+" : "") + $"{cumul:N0} €";
            txtCumulatif.color = seuilAtteint ? Seuil : (cumul >= 0f ? Vert : Rouge);
        }
    }

    // Affiche un montant ; "—" gris si nul. `negatif` = préfixe "-" (dépense).
    private static void SetMontant(TMP_Text t, float montant, Color couleur, bool negatif)
    {
        if (t == null) return;
        if (montant > 0f)
        {
            t.text = (negatif ? "-" : "") + $"{montant:N0} €";
            t.color = couleur;
        }
        else
        {
            t.text = "—";
            t.color = Neutre;
        }
    }
}
