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

    // Étape 5 : prénom de la personne qui modifie ce locataire (« Sephora · Odile »). Les
    // onglets des locataires sont masqués : cette ligne en tient lieu (décision du 14/10).
    public string LocataireId { get; private set; }
    string _nom;

    public void SetDetenteur(string prenom)
    {
        if (txtNom != null) txtNom.text = _nom + RegleVerrou.Marque(prenom);
    }

    /// Revue 16c (D7a-07) : « Lot 1 · 1250 m² » — la surface est celle que le bâtiment calcule (TailleLotEffective),
    /// pas le champ brut (0 quand le lot suit le bâtiment).
    public static string SousTitre(int lot, float surface) => $"Lot {lot} · {surface:F0} m²";

    public void Setup(Locataire loc, float surface, Action onOpenFiche)
    {
        string nom = string.IsNullOrEmpty(loc.Name) ? "Sans nom" : loc.Name;
        _nom = nom;
        LocataireId = loc.id;

        if (txtNom != null)
        {
            SetDetenteur(EcritureCloud.Detenteur(false, loc.id));
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
            txtSousTitre.text = SousTitre(loc.lotBatiment, surface);
        if (txtLoyer != null)
            txtLoyer.text = loc.loyerAnnuel > 0 ? $"{loc.loyerAnnuel:N0} €/an" : "—";

        // État révision → avatar + badge
        bool suivie = loc.RevisionIndiceSuivie;
        int joursRestants = suivie
            ? (int)(loc.MoisDeRevision - DateTime.Now).TotalDays
            : int.MaxValue;

        // Retour BM7 (04/11) : deux colonnes d'étiquettes, alignées d'une ligne à l'autre : la présence (arrivée, départ,
        // parti, archivé), puis le loyer et la révision. Parti ou archivé : ligne grisée, plus rien à réviser ; la fiche
        // reste ouvrable.
        EnsureBadgePresence();
        Presence(Color.clear, UITheme.TexteSecondaire, "");   // vide : la colonne garde sa place
        bool horsLieux = false;
        if (loc.archive) { Presence(UITheme.Bordure, UITheme.TexteSecondaire, "Archivé"); horsLieux = true; }
        else if (DepartLocataire.Sortie(loc, out var sortie))
        {
            if (loc.EstParti) { Presence(UITheme.Bordure, UITheme.TexteSecondaire, $"Parti le {sortie:dd/MM/yyyy}"); horsLieux = true; }
            else Presence(UITheme.AttentionClair, UITheme.AttentionTexte, $"Départ le {sortie:dd/MM/yyyy}");
        }
        else if (Cessions.Etiquette(loc) is string cede)   // retour BN12 (04/11) : cession prévue
            Presence(UITheme.AttentionClair, UITheme.AttentionTexte, cede);
        else if (loc.EstAVenir && Loyers.DebutPremierBail(loc, out var arrivee))   // retour BM6 : bail pas encore commencé
            Presence(UITheme.AttentionClair, UITheme.AttentionTexte, $"Arrive le {arrivee:dd/MM/yyyy}");

        if (horsLieux)
        {
            SetEtat(UITheme.Bordure, UITheme.TexteSecondaire, "", false);
            Griser();
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

    // Retour BM7 (04/11) : l'étiquette de présence, copie de celle du loyer, juste à sa gauche ; même largeur, donc les
    // colonnes s'alignent d'une ligne à l'autre.
    Image _badgePresence;
    TMP_Text _txtPresence;

    void EnsureBadgePresence()
    {
        if (_badgePresence != null || badgeBg == null) return;
        var go = Instantiate(badgeBg.gameObject, badgeBg.transform.parent);
        go.name = "BadgePresence";
        go.transform.SetSiblingIndex(badgeBg.transform.GetSiblingIndex());   // à gauche de l'étiquette du loyer
        _badgePresence = go.GetComponent<Image>();
        _txtPresence = go.GetComponentInChildren<TMP_Text>(true);
    }

    void Presence(Color fond, Color texte, string label)
    {
        if (_badgePresence != null) { _badgePresence.gameObject.SetActive(true); _badgePresence.color = fond; }
        if (_txtPresence != null) { _txtPresence.text = label; _txtPresence.color = texte; }
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
