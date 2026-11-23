using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Revue 17 (B-15 / D10-N3, lot F) : la largeur minimale d'un texte est celle de son libellé. Un TMP_Text annonce 0 : un bouton
/// « à la largeur de son texte » (UIFactory.LargeurDuTexte) n'avait donc pour minimum que ses marges, et une rangée trop étroite le
/// serrait jusqu'à superposer les libellés (fenêtre « Compte » : 41, 118 et 100 px pour 59, 168 et 143 px de texte). Posé sur le
/// texte, ce composant suit le libellé s'il change.
public class LargeurMinTexte : MonoBehaviour, ILayoutElement
{
    TMP_Text _texte;

    public float minWidth => (_texte != null || TryGetComponent(out _texte)) ? _texte.preferredWidth : 0f;
    public float preferredWidth => -1f;
    public float flexibleWidth => -1f;
    public float minHeight => -1f;
    public float preferredHeight => -1f;
    public float flexibleHeight => -1f;
    public int layoutPriority => 2;
    public void CalculateLayoutInputHorizontal() { }
    public void CalculateLayoutInputVertical() { }
}
