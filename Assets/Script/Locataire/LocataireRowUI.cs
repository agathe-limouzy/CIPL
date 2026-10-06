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

        if (txtNom != null)
        {
            txtNom.text = nom;
            // Rôle « Nom », comme le nom d'un bâtiment sur sa carte (23/09). Il était
            // à 24 pt parce que l'utilisatrice voulait « les noms des locataires en
            // plus gros » : s'il le faut de nouveau, c'est un rôle distinct à créer
            // dans UITheme, pas une taille à réécrire ici.
            txtNom.enableAutoSizing = false; txtNom.fontSize = UITheme.Role.Nom;
        }
        // Section locataires agrandie : sous-titre + loyer + avatar + hauteur de ligne.
        if (txtSousTitre != null) { txtSousTitre.enableAutoSizing = false; txtSousTitre.fontSize = UITheme.Role.SousTitre; }
        if (txtLoyer != null)
        {
            // Assez large pour « 180 000 €/an » à fs 16 (avant : tronqué en « …/... »).
            txtLoyer.enableAutoSizing = false; txtLoyer.fontSize = UITheme.Role.Donnee;
            txtLoyer.enableWordWrapping = false; txtLoyer.overflowMode = TMPro.TextOverflowModes.Overflow;
            var lle = txtLoyer.GetComponent<LayoutElement>() ?? txtLoyer.gameObject.AddComponent<LayoutElement>();
            lle.minWidth = 128; lle.preferredWidth = 128;
        }
        if (avatarBg != null)
        {
            var ale = avatarBg.GetComponent<LayoutElement>();
            if (ale != null) { ale.minWidth = 46; ale.preferredWidth = 46; ale.minHeight = 46; ale.preferredHeight = 46; }
        }
        // Lignes plus hautes pour laisser respirer le nom agrandi.
        var le = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        le.minHeight = 64; le.preferredHeight = 64;
        if (txtInitiales != null) { txtInitiales.enableAutoSizing = false; txtInitiales.fontSize = UITheme.Role.Donnee; txtInitiales.text = Initiales(nom); }
        if (txtSousTitre != null)
            txtSousTitre.text = $"Lot {loc.lotBatiment} · {loc.tailleLot:F0} m²";
        if (txtLoyer != null)
            txtLoyer.text = loc.loyerAnnuel > 0 ? $"{loc.loyerAnnuel:N0} €/an" : "—";

        // État révision → avatar + badge
        bool suivie = loc.RevisionIndiceSuivie;
        int joursRestants = suivie
            ? (int)(loc.MoisDeRevision - DateTime.Now).TotalDays
            : int.MaxValue;

        // Départ : il prime sur la révision (plus rien à réviser après). Parti ou archivé :
        // ligne grisée, la fiche reste ouvrable.
        if (loc.archive)
        {
            SetEtat(UITheme.Bordure, UITheme.TexteSecondaire, "Archivé", true);
            Griser();
        }
        else if (DepartLocataire.Sortie(loc, out var sortie))
        {
            if (loc.EstParti) { SetEtat(UITheme.Bordure, UITheme.TexteSecondaire, $"Parti le {sortie:dd/MM/yyyy}", true); Griser(); }
            else SetEtat(UITheme.AttentionClair, UITheme.AttentionTexte, $"Départ le {sortie:dd/MM/yyyy}", true);
        }
        else if (!loc.LoyerInitialise)
        {
            SetEtat(UITheme.AttentionClair, UITheme.AttentionTexte, "Loyer à initialiser", true);
        }
        else if (!suivie)
        {
            // Paliers (appliqués seuls) ou aucune révision : rien à surveiller.
            SetEtat(UITheme.PrimaireClair, UITheme.Primaire,
                loc.typeRevision == TypeRevision.Paliers ? "Paliers" : "Sans révision", false);
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

    // Nom et loyer en gris secondaire : le locataire n'est plus dans les lieux.
    private void Griser()
    {
        if (txtNom != null) txtNom.color = UITheme.TexteSecondaire;
        if (txtLoyer != null) txtLoyer.color = UITheme.TexteSecondaire;
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
