using TMPro;
using UnityEngine;

public class RentabiliteRow : MonoBehaviour
{
    public TMP_Text txtAnnee;
    public TMP_Text txtNet;
    public TMP_Text txtCumulatif;

    private static readonly Color ColorPositif = UITheme.Primaire;
    private static readonly Color ColorNegatif = UITheme.Alerte;
    private static readonly Color ColorSeuil = UITheme.Attention;

    public void Setup(int annee, float netAnnee, float cumulatif, bool seuilAtteint)
    {
        if (txtAnnee != null) txtAnnee.text = annee.ToString();
        if (txtNet != null) txtNet.text = Signe(netAnnee) + $"{netAnnee:N0} €";
        if (txtCumulatif != null) txtCumulatif.text = Signe(cumulatif) + $"{cumulatif:N0} €";

        Color couleur = seuilAtteint ? ColorSeuil
            : cumulatif >= 0f ? ColorPositif
                                    : ColorNegatif;

        if (txtNet != null) txtNet.color = couleur;
        if (txtCumulatif != null) txtCumulatif.color = couleur;
    }

    private static string Signe(float v) => v >= 0 ? "+" : "";
}