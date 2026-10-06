using System;
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

    private void Awake()
    {
        Instance = this;
        _confirmLabel = btnConfirmer != null ? btnConfirmer.GetComponentInChildren<TMP_Text>(true) : null;
        _defaultConfirmLabel = _confirmLabel != null ? _confirmLabel.text : "Confirmer";
        gameObject.SetActive(false);
    }

    /// Erreur à lire (retour du 06/10 : « pour un panel qui remonte une erreur il le faut
    /// plus voyant, centré ») : cette fenêtre, un seul bouton « OK ». Les informations et
    /// changements de statut restent en bas (UndoToast.ShowInfo).
    public static void Erreur(string message)
    {
        if (Instance == null) { UndoToast.Instance?.ShowInfo(message); return; }
        Instance.Show("Attention", message, null, "OK");
        if (Instance.btnAnnuler != null) Instance.btnAnnuler.gameObject.SetActive(false);
    }

    /// `confirmLabel` : libellé du bouton de validation (par défaut « Supprimer »).
    public void Show(string titre, string message, Action onConfirm, string confirmLabel = null)
    {
        if (btnAnnuler != null) btnAnnuler.gameObject.SetActive(true);   // masqué par Erreur
        _onConfirm = onConfirm;
        txtTitre.text = titre;
        txtMessage.text = message;
        if (_confirmLabel != null)
            _confirmLabel.text = string.IsNullOrEmpty(confirmLabel) ? _defaultConfirmLabel : confirmLabel;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();   // au-dessus de tout

        btnConfirmer.onClick.RemoveAllListeners();
        btnConfirmer.onClick.AddListener(() =>
        {
            gameObject.SetActive(false);
            _onConfirm?.Invoke();
        });

        btnAnnuler.onClick.RemoveAllListeners();
        btnAnnuler.onClick.AddListener(() => gameObject.SetActive(false));
    }
}
