using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Une ligne du tableau de suivi de facturation, portée par le prefab
/// `Assets/Prefab/SuiviFactureRow.prefab`.
///
/// Les deux vues du suivi (LocataireSuiviInline, FacturationSuiviPanel) construisaient
/// la même ligne chacune de leur côté : c'est ce qui a permis au défaut d'alignement
/// (`childForceExpandWidth`) d'exister en double. L'en-tête et la ligne « aucune
/// facture » passent par le MÊME prefab (SetupEntete / SetupVide) — sinon l'alignement
/// des colonnes redépendrait de deux constructions différentes, exactement le défaut
/// qu'on cherche à supprimer.
public class SuiviRowUI : MonoBehaviour
{
    // Largeurs des colonnes : portées par le prefab (LayoutElement), reprises ici en
    // constantes pour les appelants qui doivent encore les connaître.
    public const float WEch = 130, WMontant = 140, WEtat = 132, WActions = 280;
    public const float HLigne = 46, HEntete = 36, HVide = 44;

    public Image fond;
    public TMP_Text libelle, echeance, montant;
    public GameObject etatCell, actionsCell;
    public Button pastille;
    public Button[] actions = new Button[4];   // pré-créés dans le prefab, activés au besoin

    /// Couleurs de la ligne : fond, texte principal (libellé, montant), texte
    /// secondaire (échéance). Toutes grisées sur une période clôturée.
    public struct Couleurs
    {
        public Color fond, principal, secondaire;
        public Couleurs(Color fond, Color principal, Color secondaire)
        { this.fond = fond; this.principal = principal; this.secondaire = secondaire; }
    }

    /// Une ligne de facture. `lignesActions` : jusqu'à 4 (PDF · Corriger/Refaire ·
    /// Générer · Rappel · Quittance) ; au-delà, les suivantes sont ignorées.
    public void Setup(string libelleTxt, string echeanceTxt, string montantTxt, Couleurs c,
        string etatLibelle, Color etatBg, Color etatFg, bool etatCliquable, Action onEtat,
        IList<(string libelle, Action onClick)> lignesActions)
    {
        Fond(c.fond, HLigne);
        Texte(libelle, libelleTxt, c.principal, false);
        Texte(echeance, echeanceTxt, c.secondaire, false);
        Texte(montant, montantTxt, c.principal, false);

        if (etatCell != null) etatCell.SetActive(true);
        if (pastille != null)
        {
            pastille.gameObject.SetActive(true);
            Bouton(pastille, etatLibelle, etatBg, etatFg, etatFg, etatCliquable, onEtat);
        }

        if (actionsCell != null) actionsCell.SetActive(true);
        for (int i = 0; i < actions.Length; i++)
        {
            var b = actions[i];
            if (b == null) continue;
            bool utilise = lignesActions != null && i < lignesActions.Count;
            b.gameObject.SetActive(utilise);
            if (!utilise) continue;
            var a = lignesActions[i];
            Bouton(b, a.libelle, UITheme.Carte, UITheme.TextePrincipal, UITheme.Bordure, true, a.onClick);
        }
    }

    /// En-tête du tableau : mêmes cellules, donc mêmes largeurs que les lignes. Les
    /// colonnes État et Actions deviennent du texte nu (bouton sans fond ni bordure,
    /// non cliquable) pour rester alignées sur la pastille et le premier bouton.
    public void SetupEntete(string fac, string ech, string mont, string etat, string act, Color bg, Color fg)
    {
        Fond(bg, HEntete);
        Texte(libelle, fac, fg, true);
        Texte(echeance, ech, fg, true);
        Texte(montant, mont, fg, true);

        if (etatCell != null) etatCell.SetActive(true);
        if (pastille != null) { pastille.gameObject.SetActive(true); TexteNu(pastille, etat, fg); }

        if (actionsCell != null) actionsCell.SetActive(true);
        for (int i = 0; i < actions.Length; i++)
        {
            if (actions[i] == null) continue;
            bool premier = i == 0;
            actions[i].gameObject.SetActive(premier);
            if (premier) TexteNu(actions[i], act, fg);
        }
    }

    /// Ligne « Aucune facture pour cette année. » : seul le libellé, qui occupe toute
    /// la largeur une fois les autres cellules masquées.
    public void SetupVide(string message, Color bg, Color fg)
    {
        Fond(bg, HVide);
        Texte(libelle, message, fg, false);
        if (echeance != null) echeance.gameObject.SetActive(false);
        if (montant != null) montant.gameObject.SetActive(false);
        if (etatCell != null) etatCell.SetActive(false);
        if (actionsCell != null) actionsCell.SetActive(false);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    // Le sprite 9-slice d'UIFactory est généré par code au premier appel : il n'existe
    // pas comme asset, donc il ne peut pas être sérialisé dans le prefab. Il faut le
    // reposer sur chaque Image à l'instanciation, sinon les coins arrondis et le
    // découpage disparaissent.
    void Fond(Color bg, float hauteur)
    {
        if (fond != null)
        {
            fond.color = bg;
            fond.sprite = UIFactory.Rounded();
            fond.type = Image.Type.Sliced;
        }
        var le = GetComponent<LayoutElement>();
        if (le != null) le.minHeight = hauteur;
    }

    static void Texte(TMP_Text t, string valeur, Color couleur, bool gras)
    {
        if (t == null) return;
        t.gameObject.SetActive(true);
        t.text = valeur;
        t.color = couleur;
        t.fontStyle = gras ? FontStyles.Bold : FontStyles.Normal;
    }

    static void Bouton(Button b, string label, Color bg, Color fg, Color bordure, bool cliquable, Action onClick)
    {
        var img = b.GetComponent<Image>();
        if (img != null) { img.color = bg; img.sprite = UIFactory.Rounded(); img.type = Image.Type.Sliced; }
        var o = b.GetComponent<Outline>();
        if (o != null) { o.enabled = true; o.effectColor = bordure; }
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) { t.text = label; t.color = fg; t.fontStyle = FontStyles.Normal; t.alignment = TextAlignmentOptions.Center; }
        b.interactable = cliquable;
        b.onClick.RemoveAllListeners();
        if (cliquable && onClick != null) b.onClick.AddListener(() => onClick());
    }

    // Bouton ramené à un simple libellé de colonne : ni fond, ni bordure, ni clic.
    static void TexteNu(Button b, string label, Color fg)
    {
        var img = b.GetComponent<Image>();
        if (img != null) { img.color = Color.clear; img.sprite = null; }
        var o = b.GetComponent<Outline>();
        if (o != null) o.enabled = false;
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) { t.text = label; t.color = fg; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Left; }
        b.interactable = false;
        b.onClick.RemoveAllListeners();
    }
}
