using TMPro;
using UnityEngine;

public class RentabiliteRow : MonoBehaviour
{
    public TMP_Text txtAnnee;
    public TMP_Text txtNet;
    public TMP_Text txtCumulatif;

    private static readonly Color ColorPositif = new Color(0.18f, 0.65f, 0.35f);
    private static readonly Color ColorNegatif = new Color(0.85f, 0.25f, 0.25f);
    private static readonly Color ColorSeuil = new Color(0.95f, 0.75f, 0.10f);

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