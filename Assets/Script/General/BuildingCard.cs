using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Carte d'un bâtiment (mode grille, ≤ 10 bâtiments).
/// Pastille de couleur seule + vignette carte optionnelle.
public class BuildingCard : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text txtNom;
    public TMP_Text txtSousTitre;    // "Ville · N lots"
    public TMP_Text txtLoyer;
    public Button btnOuvrir;

    [Header("État")]
    public Image pastille;           // pastille de couleur (coin vignette)

    [Header("Vignette")]
    public RawImage vignetteCarte;   // miroir de la carte Mapbox du bâtiment

    public void Setup(BatimentPrefab batiment, Action<BatimentPrefab> onClick)
    {
        var data = batiment.getBatiment();
        int nbLoc = batiment.listLocataire.Count;

        if (txtNom != null)
            txtNom.text = string.IsNullOrEmpty(data.Name) ? "Sans nom" : data.Name;

        if (txtSousTitre != null)
            txtSousTitre.text = $"{BuildingRowUI.Ville(data.adressBatiment)} · {nbLoc} lot{(nbLoc > 1 ? "s" : "")}";

        if (txtLoyer != null)
            txtLoyer.text = $"{batiment.GetLoyerTotal():N0} €/an";

        if (pastille != null)
            pastille.color = BatimentEtatHelper.Couleur(BatimentEtatHelper.GetEtat(batiment));

        // Vignette : texture de la fiche si déjà chargée, sinon service de
        // vignettes (indispensable au démarrage sur le home, où les cartes
        // des fiches ne sont pas actives).
        if (vignetteCarte != null)
        {
            var coverTex = PhotoService.Charger(data.coverPhoto);
            if (coverTex != null)
            {
                // Photo de couverture : format conservé, recadrée au centre (cover).
                AppliquerCover(vignetteCarte, coverTex);
            }
            else if (batiment.mapController != null
                && batiment.mapController.mapImage != null
                && batiment.mapController.mapImage.texture != null)
            {
                vignetteCarte.uvRect = new Rect(0, 0, 1, 1);
                vignetteCarte.texture = batiment.mapController.mapImage.texture;
                vignetteCarte.color = Color.white;
            }
            else
            {
                vignetteCarte.uvRect = new Rect(0, 0, 1, 1);
                MapThumbnailService.Charge(this, data.adressBatiment, tex =>
                {
                    if (this != null && vignetteCarte != null)
                    {
                        vignetteCarte.uvRect = new Rect(0, 0, 1, 1);
                        vignetteCarte.texture = tex;
                        vignetteCarte.color = Color.white;
                    }
                });
            }
        }

        if (btnOuvrir != null)
        {
            btnOuvrir.onClick.RemoveAllListeners();
            btnOuvrir.onClick.AddListener(() => onClick?.Invoke(batiment));
        }
    }

    /// Affiche une photo en « cover » : format conservé, recadrée au centre
    /// pour remplir la zone de la vignette sans déformer.
    private static void AppliquerCover(RawImage img, Texture tex)
    {
        img.texture = tex;
        img.color = Color.white;
        var r = img.rectTransform.rect;
        float cardA = (r.width > 1f && r.height > 1f) ? r.width / r.height : 1.5f;
        float imgA = tex.height > 0 ? (float)tex.width / tex.height : 1f;
        if (imgA > cardA)
        {
            float w = cardA / imgA;
            img.uvRect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        else
        {
            float h = imgA / cardA;
            img.uvRect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
    }
}
