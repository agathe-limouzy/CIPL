using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TravauxFormPanel : MonoBehaviour
{
    [Header("Champs")]
    public InputAndText txtDescription;
    public InputAndText txtCoutTotal;
    public DateInputController datePicker;

    [Header("Financement")]
    public TMP_Dropdown DropdownComptant;
    public GameObject panelEmprunt;
    public InputAndText txtMontantEmprunte;
    public InputAndText txtTaux;
    public TMP_Dropdown dropdownDuree;

    [Header("Résumé calculé")]
    public TMP_Text txtApport;
    public TMP_Text txtMensualite;
    public TMP_Text txtCoutTotalCredit;

    [Header("Boutons")]
    public Button btnSauvegarder;
    public Button btnAnnuler;

    private static readonly int[] Durees = { 60, 84, 120, 180, 240, 300 };
    bool _ecouteursPoses;   // revue 16c (D7b-17)

    private TravauxFinancement _data;
    private Action<TravauxFinancement> _onSave;
    private Action _onClose;

    public void Open(TravauxFinancement existing, Action<TravauxFinancement> onSave, Action onClose = null)
    {
        _data = existing != null ? Clone(existing) : new TravauxFinancement();
        _onSave = onSave;
        _onClose = onClose;
        gameObject.SetActive(true);

        InitDropdownDuree();
        ApplyToUI(_data);
        WireListeners();
    }

    private void InitDropdownDuree()
    {
        dropdownDuree.ClearOptions();
        dropdownDuree.AddOptions(new System.Collections.Generic.List<string>
        {
            "60 mois (5 ans)", "84 mois (7 ans)", "120 mois (10 ans)",
            "180 mois (15 ans)", "240 mois (20 ans)", "300 mois (25 ans)"
        });

        DropdownComptant.ClearOptions();
        DropdownComptant.AddOptions(new System.Collections.Generic.List<string>
        {
            "Comptant", "Emprunt"   // Revue 17 (D7b-18, lot D) : « Compant », reliquat de BN16
        });
    }

    private void ApplyToUI(TravauxFinancement d)
    {
        txtDescription.ApplySave(d.description ?? "");
        txtDescription.Modify();

        txtCoutTotal.ApplySave(d.coutTotal.ToString());
        txtCoutTotal.Modify();

        txtMontantEmprunte.ApplySave(d.montantEmprunte.ToString());
        txtMontantEmprunte.Modify();

        txtTaux.ApplySave(d.tauxInteretAnnuel.ToString());
        txtTaux.Modify();

        if (!string.IsNullOrEmpty(d.dateDebutTravaux) && DateTime.TryParse(d.dateDebutTravaux, out DateTime dt))
            datePicker?.ApplyDate(dt);
        else datePicker?.ApplyDate(DateTime.Today);   // revue 16c (D7b-07) : sinon la date du dernier formulaire ouvert restait
        datePicker?.ModifyDate();


        if (d.emprunt)
        {
            DropdownComptant.value = 1;
        }
        else
        {
            DropdownComptant.value = 0;
        }
        DropdownComptant.interactable = true;
        panelEmprunt?.SetActive(d.emprunt);
        dropdownDuree.interactable = true;

        SetDuree(d.dureeMois);
        RefreshCalculs();
        ForceLayoutRebuild();
    }

    private void WireListeners()
    {
       

        DropdownComptant.onValueChanged.RemoveAllListeners();
        DropdownComptant.onValueChanged.AddListener(isOn => { panelEmprunt?.SetActive(isOn != 0); RefreshCalculs(); ForceLayoutRebuild(); });

        // Revue 16c (D7b-17) : posés une fois (chaque ouverture les empilait : N recalculs par frappe).
        if (!_ecouteursPoses)
        {
            _ecouteursPoses = true;
            txtCoutTotal.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
            txtMontantEmprunte.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
            txtTaux.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
            dropdownDuree.onValueChanged.AddListener(_ => RefreshCalculs());
        }

        btnSauvegarder.onClick.RemoveAllListeners();
        btnSauvegarder.onClick.AddListener(Save);
        btnAnnuler.onClick.RemoveAllListeners();
        btnAnnuler.onClick.AddListener(() => { gameObject.SetActive(false); _onClose?.Invoke(); });
    }

    private void ForceLayoutRebuild()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    // Revue 17 (D7b-08, lot D) : recalculé à chaque frappe, donc en silence : « — » tant qu'un nombre est illisible.
    private void RefreshCalculs()
    {
        double cout = Parse(txtCoutTotal.GetValue());
        double montant = Parse(txtMontantEmprunte.GetValue());
        double taux = Parse(txtTaux.GetValue());
        int duree = GetDuree();

        double apport = cout - montant;
        double mens = DropdownComptant.value == 1
            ? RentabiliteCalculator.Mensualite(montant, taux, duree) : 0d;
        double coutCreditTotal = mens * duree;

        if (txtApport != null) txtApport.text = double.IsNaN(apport) ? "—" : $"{System.Math.Max(0d, apport):N0} €";
        if (txtMensualite != null) txtMensualite.text = DropdownComptant.value == 1 ? (double.IsNaN(mens) ? "—" : $"{mens:N0} € / mois") : "Comptant";
        if (txtCoutTotalCredit != null) txtCoutTotalCredit.text = DropdownComptant.value == 1 && !double.IsNaN(coutCreditTotal)
            ? $"{coutCreditTotal:N0} €" : "—";
    }

    private void Save()
    {
        // Revue 17 (D7b-08, lot D) : tout est lu et contrôlé AVANT d'écrire (un nombre illisible partait à 0, un refus de
        // date laissait les autres champs en lecture) : au refus, le formulaire reste ouvert avec la saisie.
        bool emprunt = DropdownComptant.value == 1;   // en « Comptant », les champs d'emprunt sont cachés : pas de refus sur eux
        string refus = SaisieNumerique.RefusIllisible(("Coût des travaux", txtCoutTotal.GetValue()),
                                                      ("Montant emprunté", emprunt ? txtMontantEmprunte.GetValue() : ""),
                                                      ("Taux d'intérêt", emprunt ? txtTaux.GetValue() : ""));
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }
        // Revue 16c (D7b-07) : la date est lue telle qu'affichée et doit être complète.
        if (datePicker != null)
        {
            if (!datePicker.LireDate(out var dateSaisie)) { ConfirmDialog.Erreur("Saisissez une date de début des travaux complète (jour, mois, année)."); return; }
            datePicker.ApplyDate(dateSaisie);
        }

        _data.description = txtDescription.GetNewSave();
        _data.coutTotal = Parse(txtCoutTotal.GetNewSave());
        _data.emprunt = DropdownComptant.value == 1;
        // Revue 17 (D7b-08, lot D) : un champ caché illisible (« Comptant ») garde l'ancienne valeur, jamais NaN au modèle.
        double montantLu = Parse(txtMontantEmprunte.GetNewSave()), tauxLu = Parse(txtTaux.GetNewSave());
        if (!double.IsNaN(montantLu)) _data.montantEmprunte = montantLu;
        if (!double.IsNaN(tauxLu)) _data.tauxInteretAnnuel = tauxLu;
        _data.dureeMois = GetDuree();
        _data.mensualiteCalculee = _data.emprunt
            ? RentabiliteCalculator.Mensualite(_data.montantEmprunte, _data.tauxInteretAnnuel, _data.dureeMois)
            : 0f;
        _data.apportPersonnel = _data.coutTotal - _data.montantEmprunte;
        _data.dateDebutTravaux = datePicker?.saveThedate().ToString("yyyy-MM-dd") ?? "";
        _onSave?.Invoke(_data);
        gameObject.SetActive(false);
        _onClose?.Invoke();
    }

    // Voir SaisieNumerique : tolère la saisie française. Revue 17 (D7b-08, lot D) : silencieuse (NaN si illisible) ;
    // Save refuse avant d'écrire (RefusIllisible), RefreshCalculs affiche « — ».
    private double Parse(string s) => SaisieNumerique.OuNaN(s);   // revue 16c (D7b-12) : en double, comme le modèle

    private int GetDuree() => dropdownDuree.value < Durees.Length ? Durees[dropdownDuree.value] : 120;
    private void SetDuree(int m)
    {
        for (int i = 0; i < Durees.Length; i++) if (Durees[i] == m) { dropdownDuree.value = i; return; }
        dropdownDuree.value = System.Array.IndexOf(Durees, 120);   // revue 16c (D7b-17) : durée inconnue (nouvel élément) → 120 mois
    }
    private static TravauxFinancement Clone(TravauxFinancement t)
        => JsonUtility.FromJson<TravauxFinancement>(JsonUtility.ToJson(t));
}