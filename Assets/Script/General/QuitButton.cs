using UnityEngine;
using UnityEngine.UI;

/// Bouton croix (coin haut-droit) pour quitter l'application, avec confirmation.
[RequireComponent(typeof(Button))]
public class QuitButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Demander);
    }

    private void Demander()
    {
        if (ConfirmDialog.Instance != null)
            ConfirmDialog.Instance.Show("Quitter l'application",
                "Voulez-vous vraiment quitter ?", Quitter, "Quitter");
        else
            Quitter();
    }

    private void Quitter()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
