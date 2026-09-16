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
        if (ConfirmDialog.Instance == null) { Quitter(); return; }

        // Des fiches en cours de modification ? Leur contenu n'est pas récupérable par
        // la sauvegarde de fermeture (qui n'écrit que l'état déjà reversé en mémoire) :
        // on le dit explicitement au lieu de laisser croire que tout est enregistré.
        var enEdition = FermetureGuard.NomsEnEdition();

        if (enEdition.Count > 0)
            ConfirmDialog.Instance.Show("Modifications non enregistrées",
                FermetureGuard.MessageAvertissement(enEdition),
                Quitter, "Quitter sans enregistrer");
        else
            ConfirmDialog.Instance.Show("Quitter l'application",
                "Voulez-vous vraiment quitter ?", Quitter, "Quitter");
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
