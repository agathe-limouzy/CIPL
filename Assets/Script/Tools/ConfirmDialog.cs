using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmDialog : MonoBehaviour
{
    public static ConfirmDialog Instance { get; private set; }

    [Header("UI")]
    public TMP_Text txtTitre;
    public TMP_Text txtMessage;
    public Button btnConfirmer;
    public Button btnAnnuler;

    private Action _onConfirm;
    private TMP_Text _confirmLabel;
    private string _defaultConfirmLabel;
    private TMP_Text _annulerLabel;          // retour du 04/11 : « Retour » au lieu d'« Annuler » quand on le demande
    private string _defaultAnnulerLabel;

    private void Awake()
    {
        Instance = this;
        // Un chemin Windows (« …\Batiment\rivoli\… ») n'est pas une séquence d'échappement : lu comme
        // « \r », il renvoyait la fin du message au début de la ligne, par-dessus (retour du 23/10).
        txtTitre.parseCtrlCharacters = txtMessage.parseCtrlCharacters = false;
        _confirmLabel = btnConfirmer != null ? btnConfirmer.GetComponentInChildren<TMP_Text>(true) : null;
        _defaultConfirmLabel = _confirmLabel != null ? _confirmLabel.text : "Confirmer";
        _annulerLabel = btnAnnuler != null ? btnAnnuler.GetComponentInChildren<TMP_Text>(true) : null;
        _defaultAnnulerLabel = _annulerLabel != null ? _annulerLabel.text : "Annuler";
        gameObject.SetActive(false);
    }

    /// Erreur à lire (retour du 06/10 : « pour un panel qui remonte une erreur il le faut
    /// plus voyant, centré ») : cette fenêtre, un seul bouton « OK ». Les informations et
    /// changements de statut restent en bas (UndoToast.ShowInfo).
    public static void Erreur(string message) => Erreur(message, null);

    /// `ensuite` s'exécute au clic sur « OK » : l'écran reste visible derrière le message jusque-là
    /// (étape 8 : les saisies refusées restent à l'écran pour être recopiées, puis la fiche se recharge).
    public static void Erreur(string message, Action ensuite)
    {
        if (Instance == null) { UndoToast.Instance?.ShowInfo(message); ensuite?.Invoke(); return; }
        if (Instance.gameObject.activeSelf)
        {
            // Revue 17 (B-10, lot F) : le même message, déjà affiché ou déjà en attente, n'est pas remis en file (deux « OK » à
            // cliquer pour un seul avertissement) ; sa suite, elle, s'exécute toujours, avec celle du premier.
            if (Instance._erreurAffichee && Instance.txtMessage.text == message) { if (ensuite != null) Instance._onConfirm += ensuite; return; }
            int i = Instance._attente.FindIndex(a => a.message == message);
            if (i >= 0) { if (ensuite != null) Instance._attente[i] = (message, Instance._attente[i].ensuite + ensuite); return; }
            Instance._attente.Add((message, ensuite));
            return;
        }
        Instance.MontrerErreur(message, ensuite);
    }

    // Revue 16c (D2-05) : un message arrivé pendant qu'un autre est affiché attend son tour au lieu de le remplacer :
    // chaque `ensuite` s'exécute (un `ensuite` perdu laissait une fiche jamais relue, ou une perte de verrou ignorée).
    readonly List<(string message, Action ensuite)> _attente = new List<(string message, Action ensuite)>();
    bool _erreurAffichee;   // Revue 17 (D8-02) : la fenêtre montre une erreur, dont la suite (`ensuite`) n'a pas encore été exécutée

    void MontrerErreur(string message, Action ensuite)
    {
        Show("Attention", message, ensuite, "OK");
        _erreurAffichee = true;
        if (btnAnnuler != null) btnAnnuler.gameObject.SetActive(false);
        AjusterLargeur();
    }

    /// Revue visuelle du 16/11 (V-10) : un libellé long (« Continuer sans enregistrer ») passait sur deux lignes dans un bouton de
    /// 163 : la carte s'élargit (de 380 à 560) pour que chaque bouton tienne son texte sur une ligne.
    void AjusterLargeur()
    {
        var carte = btnConfirmer != null ? btnConfirmer.transform.parent.parent as RectTransform : null;
        if (carte == null) return;
        float besoin = 0f; int n = 0;
        foreach (var b in new[] { btnAnnuler, btnConfirmer })
        {
            if (b == null || !b.gameObject.activeSelf) continue;
            n++;
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) besoin = Mathf.Max(besoin, t.GetPreferredValues(t.text).x + 28f);
        }
        carte.sizeDelta = new Vector2(LargeurCarte(besoin, n), carte.sizeDelta.y);
    }

    /// Pure, testée : `besoin` = la largeur du bouton le plus exigeant ; marges 22 + 22, 10 entre deux boutons.
    public static float LargeurCarte(float besoin, int boutons) => Mathf.Clamp(boutons * besoin + (boutons - 1) * 10f + 44f, 380f, 560f);

    void Suivant()
    {
        if (_attente.Count == 0 || gameObject.activeSelf) return;   // actif : le clic vient d'en ouvrir un autre, il passera après
        var (message, ensuite) = _attente[0];
        _attente.RemoveAt(0);
        MontrerErreur(message, ensuite);
    }

    /// `confirmLabel` : libellé du bouton de validation (par défaut « Supprimer ») ; `annulerLabel` : celui de l'autre bouton
    /// (par défaut « Annuler »).
    public void Show(string titre, string message, Action onConfirm, string confirmLabel = null, string annulerLabel = null)
    {
        // Revue 17 (D8-02, coutures) : une confirmation arrivée pendant un message d'erreur (croix de la fenêtre, Alt+F4) le
        // remplaçait, et sa suite (relire la fiche, fermer les écrans après une perte de verrou) n'était jamais exécutée :
        // l'erreur repasse en tête de la file et revient dès que la confirmation est fermée.
        if (_erreurAffichee && gameObject.activeSelf) _attente.Insert(0, (txtMessage.text, _onConfirm));
        _erreurAffichee = false;
        if (btnAnnuler != null) btnAnnuler.gameObject.SetActive(true);   // masqué par Erreur
        _onConfirm = onConfirm;
        txtTitre.text = titre;
        txtMessage.text = message;
        if (_confirmLabel != null)
            _confirmLabel.text = string.IsNullOrEmpty(confirmLabel) ? _defaultConfirmLabel : confirmLabel;
        if (_annulerLabel != null)
            _annulerLabel.text = string.IsNullOrEmpty(annulerLabel) ? _defaultAnnulerLabel : annulerLabel;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();   // au-dessus de tout
        AjusterLargeur();
        // Revue 17 (D8-12, lot F) : le champ en cours de saisie perd la main : avant, la frappe continuait dans le champ caché
        // derrière le message. Et la touche qui vient d'ouvrir ce message (Entrée dans un champ) ne le referme pas aussitôt.
        _imageOuverture = Time.frameCount;
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null && !es.alreadySelecting) es.SetSelectedGameObject(null);

        btnConfirmer.onClick.RemoveAllListeners();
        btnConfirmer.onClick.AddListener(() =>
        {
            gameObject.SetActive(false);
            _onConfirm?.Invoke();
            Suivant();
        });

        btnAnnuler.onClick.RemoveAllListeners();
        btnAnnuler.onClick.AddListener(() => { gameObject.SetActive(false); Suivant(); });
    }

    int _imageOuverture = -1;

    /// Revue 17 (D10-02, lot F ; décision du 16/11) : une touche pendant le message (ClavierUI). Erreur : Entrée ou Échap = « OK ».
    /// Confirmation : Échap = « Annuler » ; Entrée ne fait rien (une suppression ou un envoi se confirme à la souris).
    public void Touche(bool entree)
    {
        if (Time.frameCount == _imageOuverture) return;
        var bouton = BoutonPour(_erreurAffichee, entree) ? btnConfirmer : (!_erreurAffichee && !entree ? btnAnnuler : null);
        if (bouton != null && bouton.gameObject.activeInHierarchy && bouton.interactable) bouton.onClick.Invoke();
    }

    /// Vrai si la touche vaut le bouton de validation : seulement pour une erreur (son « OK »). Pure, testée.
    public static bool BoutonPour(bool erreur, bool entree) => erreur;
}
