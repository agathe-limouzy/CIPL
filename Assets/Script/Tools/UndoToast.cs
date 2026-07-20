using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UndoToast : MonoBehaviour
{
    public static UndoToast Instance { get; private set; }

    [Header("UI")]
    public TMP_Text txtMessage;
    public Button btnAnnuler;
    public Image barreProgression;   // optionnel : barre qui se vide

    private const float DUREE = 8f;
    private Coroutine _timer;
    private Action _onUndo;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    /// Affiche "message — Annuler". Si l'utilisateur clique, onUndo est appelé.
    public void Show(string message, Action onUndo)
    {
        _onUndo = onUndo;
        txtMessage.text = message;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        btnAnnuler.onClick.RemoveAllListeners();
        btnAnnuler.onClick.AddListener(() =>
        {
            Cacher();
            _onUndo?.Invoke();
        });

        if (_timer != null) StopCoroutine(_timer);
        _timer = StartCoroutine(TimerFermeture());
    }

    private IEnumerator TimerFermeture()
    {
        float t = 0f;
        while (t < DUREE)
        {
            t += Time.deltaTime;
            if (barreProgression != null)
                barreProgression.fillAmount = 1f - t / DUREE;
            yield return null;
        }
        Cacher();
    }

    private void Cacher()
    {
        if (_timer != null) { StopCoroutine(_timer); _timer = null; }
        gameObject.SetActive(false);
    }
}
