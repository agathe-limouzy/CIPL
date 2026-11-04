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
        if (Instance.gameObject.activeSelf) { Instance._attente.Enqueue((message, ensuite)); return; }
        Instance.MontrerErreur(message, ensuite);
    }

    // Revue 16c (D2-05) : un message arrivé pendant qu'un autre est affiché attend son tour au lieu de le remplacer :
    // chaque `ensuite` s'exécute (un `ensuite` perdu laissait une fiche jamais relue, ou une perte de verrou ignorée).
    readonly Queue<(string message, Action ensuite)> _attente = new Queue<(string message, Action ensuite)>();

    void MontrerErreur(string message, Action ensuite)
    {
        Show("Attention", message, ensuite, "OK");
        if (btnAnnuler != null) btnAnnuler.gameObject.SetActive(false);
    }

    void Suivant()
    {
        if (_attente.Count == 0 || gameObject.activeSelf) return;   // actif : le clic vient d'en ouvrir un autre, il passera après
        var (message, ensuite) = _attente.Dequeue();
        MontrerErreur(message, ensuite);
    }

    /// `confirmLabel` : libellé du bouton de validation (par défaut « Supprimer ») ; `annulerLabel` : celui de l'autre bouton
    /// (par défaut « Annuler »).
    public void Show(string titre, string message, Action onConfirm, string confirmLabel = null, string annulerLabel = null)
    {
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
}
