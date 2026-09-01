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

    /// `confirmLabel` : libellé du bouton de validation (par défaut « Supprimer »).
    public void Show(string titre, string message, Action onConfirm, string confirmLabel = null)
    {
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
