using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BailFileUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text txtChemin;       // affiche le nom du fichier lié
    public Button btnParcourir;      // choisir le fichier
    public Button btnOuvrir;         // ouvrir le fichier
    public Button btnRetirer;        // délier

    private LocatairePrefab _locatairePrefab;

    public void Init(LocatairePrefab locatairePrefab)
    {
        _locatairePrefab = locatairePrefab;

        btnParcourir.onClick.RemoveAllListeners();
        btnParcourir.onClick.AddListener(ChoisirFichier);

        btnOuvrir.onClick.RemoveAllListeners();
        btnOuvrir.onClick.AddListener(OuvrirFichier);

        if (btnRetirer != null)
        {
            btnRetirer.onClick.RemoveAllListeners();
            btnRetirer.onClick.AddListener(() => Enregistrer(""));
        }

        Refresh();
    }

    public void Refresh()
    {
        var loc = _locatairePrefab != null ? _locatairePrefab.GetLocataire() : null;
        string chemin = loc?.cheminBail;
        bool lie = !string.IsNullOrEmpty(chemin);
        bool existe = lie && File.Exists(chemin);

        if (txtChemin != null)
        {
            if (!lie) txtChemin.text = "Aucun bail lié";
            else if (!existe) txtChemin.text = $"⚠ Introuvable : {Path.GetFileName(chemin)}";
            else txtChemin.text = Path.GetFileName(chemin);
        }

        if (btnOuvrir != null) btnOuvrir.interactable = existe;
        if (btnRetirer != null) btnRetirer.gameObject.SetActive(lie);
    }

    private void ChoisirFichier()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        var paths = SFB.StandaloneFileBrowser.OpenFilePanel(
            "Choisir le bail", "",
            new[] { new SFB.ExtensionFilter("Documents", "pdf", "doc", "docx", "jpg", "png") },
            false);

        if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            Enregistrer(paths[0]);
#endif
    }

    private void OuvrirFichier()
    {
        var loc = _locatairePrefab.GetLocataire();
        if (loc != null && File.Exists(loc.cheminBail))
            Application.OpenURL("file:///" + loc.cheminBail.Replace('\\', '/'));
    }

    private void Enregistrer(string cheminSource)
    {
        var loc = _locatairePrefab.GetLocataire();
        if (loc == null) return;

        if (!string.IsNullOrEmpty(cheminSource))
        {
            // Copie le fichier dans le dossier de sauvegarde de l'app :
            // il est ainsi inclus dans les backups et insensible aux
            // déplacements du fichier d'origine.
            string dossier = Path.Combine(SaveLocationService.GetSaveRoot(), "Documents", loc.id);
            Directory.CreateDirectory(dossier);
            string dest = Path.Combine(dossier, Path.GetFileName(cheminSource));
            File.Copy(cheminSource, dest, overwrite: true);
            loc.cheminBail = dest;
        }
        else
        {
            loc.cheminBail = "";
        }

        _locatairePrefab.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        Refresh();
    }
}
