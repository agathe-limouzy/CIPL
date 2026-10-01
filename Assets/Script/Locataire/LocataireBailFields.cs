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

    // Départ du locataire (dernier jour de location), sous les dates du bail. Vide = il
    // reste : le loyer continue même après la fin du bail (tacite prolongation). En
    // lecture, la rangée n'apparaît que si un départ est saisi.
    GameObject _rangeeDepart;
    Toggle _depart;
    DateInputController _dateDepart;

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

        // Rangée « Départ du locataire », juste sous les dates, réglée comme elles.
        var rangeeDates = fiche.dateFinBail.transform.parent;
        var rd = new GameObject("RowDepart", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rd.transform.SetParent(rangeeDates.parent, false);
        rd.transform.SetSiblingIndex(rangeeDates.GetSiblingIndex() + 1);
        rd.GetComponent<LayoutElement>().flexibleWidth = 1;
        var hd = rd.GetComponent<HorizontalLayoutGroup>();
        if (modele != null)
        {
            hd.padding = new RectOffset(modele.padding.left, modele.padding.right, modele.padding.top, modele.padding.bottom);
            hd.spacing = modele.spacing;
            hd.childControlWidth = modele.childControlWidth; hd.childControlHeight = modele.childControlHeight;
            hd.childForceExpandWidth = modele.childForceExpandWidth; hd.childForceExpandHeight = modele.childForceExpandHeight;
        }
        hd.childAlignment = TextAnchor.MiddleLeft;
        _rangeeDepart = rd;
        _depart = UIFactory.Toggle(rd.transform, "Départ du locataire", false);
        Largeur(_depart.transform, 1);
        var bloc = Instantiate(fiche.dateFinBail.gameObject, rd.transform);
        bloc.name = "DepartDate";
        _dateDepart = bloc.GetComponent<DateInputController>();
        _dateDepart.OnModify.RemoveAllListeners();
        var titre = bloc.transform.Find("Titre")?.GetComponent<TMP_Text>();
        if (titre != null) titre.text = "Dernier jour de location";
        Largeur(bloc.transform, 1);
        _depart.onValueChanged.AddListener(on =>
        {
            _dateDepart.gameObject.SetActive(on);
            if (on && _enModification) _dateDepart.ModifyDate();
        });
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

        // Départ : affiché seulement s'il est saisi, en lecture.
        bool depart = FacturationSuivi.TryEcheance(loc.dateSortieISO, out var dd);
        _depart.SetIsOnWithoutNotify(depart);
        _depart.interactable = false;
        if (depart) _dateDepart.ApplyDate(dd);
        _dateDepart.gameObject.SetActive(depart);
        _rangeeDepart.SetActive(depart);
    }

    public void Modify()
    {
        _enModification = true;
        _fiche.typedeBailDropDown.interactable = true;
        _duree.Modify();
        _fermes.Modify();

        // Départ : case toujours proposée en saisie ; la date suit la case.
        _rangeeDepart.SetActive(true);
        _depart.interactable = true;
        _dateDepart.gameObject.SetActive(_depart.isOn);
        if (_depart.isOn) _dateDepart.ModifyDate();
        else { _dateDepart.dayInput.text = ""; _dateDepart.monthInput.text = ""; _dateDepart.yearInput.text = ""; }
    }

    /// Message à afficher si le bail ne tient pas debout, sinon null.
    public string Verifier()
    {
        int.TryParse(_duree.GetValue(), out int duree);
        int.TryParse(_fermes.GetValue(), out int fermes);
        string erreur = Locataire.VerifierBail(duree, Locataire.PeriodeFermePossible(TypeChoisi()) ? fermes : 0);
        if (erreur != null || !_depart.isOn) return erreur;
        if (!_dateDepart.LireDate(out var depart)) return "Départ du locataire : saisissez le dernier jour de location (JJ/MM/AAAA).";
        if (_fiche.dateDebutBail.LireDate(out var debut) && depart < debut)
            return $"Le départ ne peut pas précéder le début du bail ({debut:dd/MM/yyyy}).";
        return null;
    }

    public void Save(Locataire loc)
    {
        var type = TypeChoisi();
        loc.typeDeBail = type;
        int.TryParse(_duree.GetNewSave(), out loc.dureeBailAns);
        int.TryParse(_fermes.GetNewSave(), out int fermes);
        loc.anneesFermes = Locataire.PeriodeFermePossible(type) ? fermes : 0;
        // Départ : dernier jour de location, ou vide si le locataire reste.
        loc.dateSortieISO = _depart.isOn && _dateDepart.LireDate(out var depart) ? depart.ToString("yyyy-MM-dd") : "";
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
