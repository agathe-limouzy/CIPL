using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RentabiliteRow : MonoBehaviour
{
    public TMP_Text txtAnnee;
    public TMP_Text txtNetAnnee;
    public TMP_Text txtCumulatif;
    public TMP_Text txtStatut;
    public Image rowBackground;

    private static readonly Color ColorPositif = new Color(0.15f, 0.65f, 0.35f, 0.3f);
    private static readonly Color ColorNegatif = new Color(0.80f, 0.20f, 0.20f, 0.3f);
    private static readonly Color ColorSeuil = new Color(0.95f, 0.75f, 0.10f, 0.3f);

    public void Setup(int anneeReelle, float netAnnee, float cumulatif)
    {
        bool rentable = cumulatif >= 0;
        bool seuilAtteint = cumulatif >= 0 && (cumulatif - netAnnee) < 0;

        if (txtAnnee != null) txtAnnee.text = anneeReelle.ToString();
        if (txtNetAnnee != null) txtNetAnnee.text = $"{netAnnee:+0.##;-0.##;0} €";
        if (txtCumulatif != null) txtCumulatif.text = $"{cumulatif:+0.##;-0.##;0} €";

        if (txtStatut != null)
            txtStatut.text = seuilAtteint ? "✅ Seuil atteint" : rentable ? "✅" : "";

        if (rowBackground != null)
            rowBackground.color = seuilAtteint ? ColorSeuil
                : rentable ? ColorPositif : ColorNegatif;
    }
}