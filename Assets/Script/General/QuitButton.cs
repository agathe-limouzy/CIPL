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

        // Des fiches en cours de modification, enregistrées hors ligne, un envoi de factures en cours ? La même décision que la
        // garde de fermeture (Revue 17, D8-13 : elle était écrite deux fois) : on le dit au lieu de laisser croire que tout est enregistré.
        FermetureGuard.Demander(Quitter);
    }

    private void Quitter()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        FermetureGuard.QuitterSansGarde();   // revue 16c (D8-04) : sinon wantsToQuit redemandait la confirmation
#endif
    }
}
