using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveLocationMenu : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text txtCheminActuel;
    public TMP_InputField inputChemin;      // Fallback saisie manuelle
    public Button btnParcourir;      // Nécessite StandaloneFileBrowser
    public Button btnAppliquer;
    public Button btnDefaut;
    public Button btnFermer;
    public Toggle toggleMigrerDonnees;
    public TMP_Text txtStatus;

    private void OnEnable()
    {
        RefreshAffichage();

        btnParcourir.onClick.RemoveAllListeners();
        btnParcourir.onClick.AddListener(OuvrirExplorateur);

        btnAppliquer.onClick.RemoveAllListeners();
        btnAppliquer.onClick.AddListener(() => Appliquer(inputChemin.text));

        btnDefaut.onClick.RemoveAllListeners();
        btnDefaut.onClick.AddListener(RemettreDefaut);

        btnFermer.onClick.RemoveAllListeners();
        btnFermer.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void RefreshAffichage()
    {
        txtCheminActuel.text = SaveLocationService.GetSaveRoot();
        if (txtStatus != null) txtStatus.text = "";
    }

    private void OuvrirExplorateur()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        // Nécessite le plugin StandaloneFileBrowser
        var paths = SFB.StandaloneFileBrowser.OpenFolderPanel(
            "Choisir l'emplacement de sauvegarde", "", false);

        if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            inputChemin.text = paths[0];
            Appliquer(paths[0]);
        }
#else
        if (txtStatus != null)
            txtStatus.text = "Saisir le chemin manuellement";
#endif
    }

    private void Appliquer(string chemin)
    {
        if (string.IsNullOrEmpty(chemin) || !Directory.Exists(chemin))
        {
            if (txtStatus != null)
            {
                txtStatus.text = "⚠ Dossier introuvable";
                txtStatus.color = Color.red;
            }
            return;
        }

        string ancienRoot = SaveLocationService.GetSaveRoot();
        string nouveauRoot = SaveLocationService.SetSaveRoot(chemin);

        if (toggleMigrerDonnees != null && toggleMigrerDonnees.isOn)
            SaveLocationService.MigrateData(ancienRoot, nouveauRoot);

        // Recharger les données depuis le nouvel emplacement
        BatimentManager.Instance.ReloadFromDisk();

        if (txtStatus != null)
        {
            txtStatus.text = "✅ Emplacement mis à jour";
            txtStatus.color = Color.green;
        }
        RefreshAffichage();
    }

    private void RemettreDefaut()
    {
        PlayerPrefs.DeleteKey("save_root_path");
        PlayerPrefs.Save();
        BatimentManager.Instance.ReloadFromDisk();
        RefreshAffichage();
    }
}