using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Section Bail de la fiche locataire : [type · durée · période ferme], puis les dates.
///
/// Le type de bail pré-remplit la durée, modifiable : c'est la durée totale du bail.
/// La date de fin en découle (la veille de l'anniversaire : 01/06/2020 + 9 ans →
/// 31/05/2029) et se recalcule quand on change le début, la durée ou le type ; elle
/// reste modifiable (bail repris, avenant). La période ferme est une portion de cette
/// durée, facultative, réservée au bail commercial ; l'enregistrement est refusé si
/// elle dépasse la durée.
///
/// Même principe que LocataireFacturationFields : les champs sont clonés d'un champ
/// natif de la fiche, pour avoir exactement le même rendu.
public class LocataireBailFields : MonoBehaviour
{
    LocatairePrefab _fiche;
    InputAndText _duree, _fermes;
    bool _enModification;

    public void EnsureBuilt(LocatairePrefab fiche)
    {
        if (_duree != null) return;
        _fiche = fiche;

        var liste = fiche.typedeBailDropDown;
        liste.ClearOptions();
        liste.AddOptions(Locataire.TypesProposes
            .Select(t => new TMP_Dropdown.OptionData(Locataire.LibelleBail(t))).ToList());
        liste.onValueChanged.AddListener(_ => SurChangementDeType());

        // Rangée [type · durée · période ferme], à la place du bloc « Type de bail »,
        // réglée comme la rangée des dates (mêmes enfants : blocs titre + saisie).
        var blocType = liste.transform.parent;
        var modele = fiche.dateDebutBail.transform.parent.GetComponent<HorizontalLayoutGroup>();
        var rangee = new GameObject("RowTypeDuree", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rangee.transform.SetParent(blocType.parent, false);
        rangee.transform.SetSiblingIndex(blocType.GetSiblingIndex());
        rangee.GetComponent<LayoutElement>().flexibleWidth = 1;
        var h = rangee.GetComponent<HorizontalLayoutGroup>();
        if (modele != null)
        {
            h.padding = new RectOffset(modele.padding.left, modele.padding.right, modele.padding.top, modele.padding.bottom);
            h.spacing = modele.spacing; h.childAlignment = modele.childAlignment;
            h.childControlWidth = modele.childControlWidth; h.childControlHeight = modele.childControlHeight;
            h.childForceExpandWidth = modele.childForceExpandWidth; h.childForceExpandHeight = modele.childForceExpandHeight;
        }
        blocType.SetParent(rangee.transform, false);
        Largeur(blocType, 2);   // le type a les libellés les plus longs

        _duree = LocataireFacturationFields.Clone(fiche.tailleLotTxt, rangee.transform, "Durée", "9", "ans");
        _fermes = LocataireFacturationFields.Clone(fiche.tailleLotTxt, rangee.transform, "Période ferme", "Aucune", "ans");
        Largeur(_duree.transform, 1);
        Largeur(_fermes.transform, 1);
        _duree.inputModify.contentType = TMP_InputField.ContentType.IntegerNumber;
        _fermes.inputModify.contentType = TMP_InputField.ContentType.IntegerNumber;

        // La fin suit le début dès qu'il est complet, et la durée à chaque frappe.
        var debut = fiche.dateDebutBail;
        foreach (var champ in new[] { debut.dayInput, debut.monthInput, debut.yearInput })
            champ.onValueChanged.AddListener(_ => RecalculerFin());
        _duree.inputModify.onValueChanged.AddListener(_ => RecalculerFin());
    }

    static void Largeur(Transform t, float parts)
    {
        var le = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = parts;
    }

    public void Load(Locataire loc)
    {
        _enModification = false;   // afficher une fiche ne recalcule rien : la fin enregistrée fait foi
        var type = Locataire.Normaliser(loc.typeDeBail);
        int i = Array.IndexOf(Locataire.TypesProposes, type);
        _fiche.typedeBailDropDown.SetValueWithoutNotify(i < 0 ? 0 : i);
        _fiche.typedeBailDropDown.interactable = false;
        int duree = loc.DureeBail(), fermes = loc.AnneesFermes();
        _duree.ApplySave(duree > 0 ? duree.ToString() : "");
        _fermes.ApplySave(fermes > 0 ? fermes.ToString() : "");
        MontrerFermes(type);
    }

    public void Modify()
    {
        _enModification = true;
        _fiche.typedeBailDropDown.interactable = true;
        _duree.Modify();
        _fermes.Modify();
    }

    /// Message à afficher si le bail ne tient pas debout, sinon null.
    public string Verifier()
    {
        int.TryParse(_duree.GetValue(), out int duree);
        int.TryParse(_fermes.GetValue(), out int fermes);
        return Locataire.VerifierBail(duree, Locataire.PeriodeFermePossible(TypeChoisi()) ? fermes : 0);
    }

    public void Save(Locataire loc)
    {
        var type = TypeChoisi();
        loc.typeDeBail = type;
        int.TryParse(_duree.GetNewSave(), out loc.dureeBailAns);
        int.TryParse(_fermes.GetNewSave(), out int fermes);
        loc.anneesFermes = Locataire.PeriodeFermePossible(type) ? fermes : 0;
        _fiche.typedeBailDropDown.interactable = false;
        _enModification = false;
    }

    BailType TypeChoisi()
        => Locataire.TypesProposes[Mathf.Clamp(_fiche.typedeBailDropDown.value, 0, Locataire.TypesProposes.Length - 1)];

    void MontrerFermes(BailType type) => _fermes.gameObject.SetActive(Locataire.PeriodeFermePossible(type));

    // Nouveau type : sa durée par défaut, et la fin qui en découle.
    void SurChangementDeType()
    {
        var type = TypeChoisi();
        MontrerFermes(type);
        if (!_enModification) return;
        int defaut = Locataire.DureeParDefaut(type);
        _duree.ApplyValue(defaut > 0 ? defaut.ToString() : "");
        if (!Locataire.PeriodeFermePossible(type)) _fermes.ApplyValue("");
        RecalculerFin();
    }

    void RecalculerFin()
    {
        if (!_enModification || !_fiche.dateDebutBail.LireDate(out var debut)) return;
        if (!int.TryParse(_duree.GetValue(), out int ans) || ans <= 0 || ans > 99) return;
        _fiche.dateFinBail.ApplyDate(Locataire.FinDeBail(debut, ans));
        _fiche.dateFinBail.ModifyDate();   // ApplyDate repasse en lecture : on reste en saisie
    }
}
