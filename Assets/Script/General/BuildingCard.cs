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
            if (batiment.mapController != null
                && batiment.mapController.mapImage != null
                && batiment.mapController.mapImage.texture != null)
            {
                vignetteCarte.texture = batiment.mapController.mapImage.texture;
                vignetteCarte.color = Color.white;
            }
            else
            {
                MapThumbnailService.Charge(this, data.adressBatiment, tex =>
                {
                    if (this != null && vignetteCarte != null)
                    {
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
}
