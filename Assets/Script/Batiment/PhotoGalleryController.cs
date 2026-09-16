using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Overlay plein écran : galerie photos d'un bâtiment.
/// Grande image + navigation ◄►, compteur, ajout, suppression et définition
/// de la photo de couverture (celle qui remplace la carte GPS dans le home).
/// Singleton de scène (un seul overlay), ouvert par la fiche bâtiment.
public class PhotoGalleryController : MonoBehaviour
{
    public static PhotoGalleryController Instance { get; private set; }

    [Header("Racine")]
    public GameObject overlay;           // panneau plein écran (activé/désactivé)

    [Header("Affichage")]
    public RawImage image;               // grande photo courante
    public AspectRatioFitter aspectFitter; // optionnel : garde les proportions
    public GameObject emptyState;        // bloc "aucune photo"
    public TMP_Text txtEmpty;
    public TMP_Text txtCompteur;         // "2 / 5"
    public GameObject badgeCouverture;   // pastille "Couverture" sur la photo courante

    [Header("Boutons")]
    public Button btnPrev;
    public Button btnNext;
    public Button btnAjouter;
    public Button btnSupprimer;
    public Button btnCouverture;
    public TMP_Text txtCouverture;       // libellé du bouton couverture
    public Button btnFermer;

    private BatimentPrefab _bp;
    private Batiment _batiment;
    private int _index;

    void Awake()
    {
        Instance = this;
        if (overlay != null) overlay.SetActive(false);

        if (btnPrev != null) btnPrev.onClick.AddListener(() => Naviguer(-1));
        if (btnNext != null) btnNext.onClick.AddListener(() => Naviguer(+1));
        if (btnAjouter != null) btnAjouter.onClick.AddListener(OnAjouter);
        if (btnSupprimer != null) btnSupprimer.onClick.AddListener(OnSupprimer);
        if (btnCouverture != null) btnCouverture.onClick.AddListener(OnCouverture);
        if (btnFermer != null) btnFermer.onClick.AddListener(Fermer);
    }

    public void Ouvrir(BatimentPrefab bp)
    {
        _bp = bp;
        _batiment = bp != null ? bp.getBatiment() : null;
        if (_batiment == null) return;
        _index = 0;
        if (overlay != null) overlay.SetActive(true);
        Refresh();
    }

    public void Fermer()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    private List<string> Photos => _batiment != null ? _batiment.photos : new List<string>();

    private void Naviguer(int delta)
    {
        int n = Photos.Count;
        if (n == 0) return;
        _index = ((_index + delta) % n + n) % n;   // navigation cyclique
        Refresh();
    }

    private void OnAjouter()
    {
        int ajoutes = PhotoService.AjouterPhotos(_batiment);
        if (ajoutes > 0)
        {
            _index = Mathf.Max(0, Photos.Count - ajoutes); // aller sur la 1re ajoutée
            Sauver();
        }
        Refresh();
    }

    private void OnSupprimer()
    {
        int n = Photos.Count;
        if (n == 0) return;
        string courant = Photos[Mathf.Clamp(_index, 0, n - 1)];
        var suppr = PhotoService.Supprimer(_batiment, courant);
        if (_index >= Photos.Count) _index = Mathf.Max(0, Photos.Count - 1);
        Sauver();
        Refresh();

        // Le fichier part en corbeille, pas à la poubelle : on propose l'annulation,
        // comme pour les achats, travaux, charges et fiches. C'était la seule
        // suppression de l'app qui était définitive dès le premier clic.
        if (suppr == null || UndoToast.Instance == null) return;
        UndoToast.Instance.Show("Photo supprimée", () =>
        {
            if (!PhotoService.Restaurer(_batiment, suppr))
            {
                UndoToast.Instance.ShowInfo("Photo introuvable : restauration impossible.");
                return;
            }
            _index = Mathf.Clamp(suppr.index, 0, Mathf.Max(0, Photos.Count - 1));
            Sauver();
            Refresh();
        });
    }

    private void OnCouverture()
    {
        int n = Photos.Count;
        if (n == 0) return;
        string courant = Photos[Mathf.Clamp(_index, 0, n - 1)];
        _batiment.coverPhoto = (_batiment.coverPhoto == courant) ? "" : courant;
        Sauver();
        Refresh();
    }

    private void Sauver()
    {
        if (_batiment != null && BatimentManager.Instance != null)
            BatimentManager.Instance.SaveBatiment(_batiment);
    }

    private void Refresh()
    {
        int n = Photos.Count;
        bool aDesPhotos = n > 0;

        if (emptyState != null) emptyState.SetActive(!aDesPhotos);
        if (image != null) image.gameObject.SetActive(aDesPhotos);
        if (btnPrev != null) btnPrev.gameObject.SetActive(n > 1);
        if (btnNext != null) btnNext.gameObject.SetActive(n > 1);
        if (btnSupprimer != null) btnSupprimer.interactable = aDesPhotos;
        if (btnCouverture != null) btnCouverture.interactable = aDesPhotos;

        if (!aDesPhotos)
        {
            if (txtCompteur != null) txtCompteur.text = "";
            if (badgeCouverture != null) badgeCouverture.SetActive(false);
            if (txtEmpty != null) txtEmpty.text = "Aucune photo.\nCliquez sur « + Ajouter » pour en importer.";
            return;
        }

        _index = Mathf.Clamp(_index, 0, n - 1);
        string courant = Photos[_index];

        if (image != null)
        {
            var tex = PhotoService.Charger(courant);
            image.texture = tex;
            image.color = tex != null ? Color.white : new Color(0f, 0f, 0f, 0.15f);
            if (aspectFitter != null && tex != null && tex.height > 0)
                aspectFitter.aspectRatio = (float)tex.width / tex.height;
        }
        if (txtCompteur != null) txtCompteur.text = $"{_index + 1} / {n}";

        bool estCouverture = _batiment.coverPhoto == courant;
        if (badgeCouverture != null) badgeCouverture.SetActive(estCouverture);
        if (txtCouverture != null)
            txtCouverture.text = estCouverture ? "Retirer la couverture" : "Définir comme couverture";
    }
}
