using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Ligne compacte d'un locataire dans la liste du bâtiment :
/// avatar initiales · nom + sous-titre · badge révision · loyer · clic → fiche
public class LocataireRowUI : MonoBehaviour
{
    [Header("UI")]
    public Image avatarBg;
    public TMP_Text txtInitiales;
    public TMP_Text txtNom;
    public TMP_Text txtSousTitre;
    public Image badgeBg;
    public TMP_Text txtBadge;
    public TMP_Text txtLoyer;
    public Button btnFiche;

    private const int SEUIL_BIENTOT = 90;   // jours

    public void Setup(Locataire loc, Action onOpenFiche)
    {
        string nom = string.IsNullOrEmpty(loc.Name) ? "Sans nom" : loc.Name;

        if (txtNom != null) txtNom.text = nom;
        if (txtInitiales != null) txtInitiales.text = Initiales(nom);
        if (txtSousTitre != null)
            txtSousTitre.text = $"Lot {loc.lotBatiment} · {loc.tailleLot:F0} m²";
        if (txtLoyer != null)
            txtLoyer.text = loc.loyerAnnuel > 0 ? $"{loc.loyerAnnuel:N0} €/an" : "—";

        // État révision → avatar + badge
        bool initialise = !string.IsNullOrEmpty(loc.indiceImmoAuDepart)
                          && loc.indiceImmoAuDepart != "—";
        int joursRestants = initialise
            ? (int)(loc.MoisDeRevision - DateTime.Now).TotalDays
            : int.MaxValue;

        if (!initialise)
        {
            SetEtat(UITheme.AttentionClair, UITheme.AttentionTexte, "Bail à initialiser", true);
        }
        else if (joursRestants < 0)
        {
            SetEtat(UITheme.AlerteClair, UITheme.AlerteTexte, "Révision en retard", true);
        }
        else if (joursRestants <= SEUIL_BIENTOT)
        {
            SetEtat(UITheme.AttentionClair, UITheme.AttentionTexte,
                $"Révision dans {joursRestants} j", true);
        }
        else
        {
            SetEtat(UITheme.PrimaireClair, UITheme.Primaire,
                $"Rév. {loc.MoisDeRevision:MM/yyyy}", false);
        }

        if (btnFiche != null)
        {
            btnFiche.onClick.RemoveAllListeners();
            btnFiche.onClick.AddListener(() => onOpenFiche?.Invoke());
        }
    }

    private void SetEtat(Color fond, Color texte, string label, bool badgeVisible)
    {
        if (avatarBg != null) avatarBg.color = fond;
        if (txtInitiales != null) txtInitiales.color = texte;

        if (badgeBg != null)
        {
            badgeBg.gameObject.SetActive(true);
            badgeBg.color = badgeVisible ? fond : Color.clear;
        }
        if (txtBadge != null)
        {
            txtBadge.text = label;
            txtBadge.color = badgeVisible ? texte : UITheme.TexteSecondaire;
        }
    }

    private static string Initiales(string nom)
    {
        var mots = nom.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (mots.Length == 0) return "?";
        if (mots.Length == 1) return mots[0].Substring(0, Mathf.Min(2, mots[0].Length)).ToUpper();
        return (mots[0].Substring(0, 1) + mots[1].Substring(0, 1)).ToUpper();
    }
}
