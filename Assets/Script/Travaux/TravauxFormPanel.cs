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

    private TravauxFinancement _data;
    private Action<TravauxFinancement> _onSave;

    public void Open(TravauxFinancement existing, Action<TravauxFinancement> onSave)
    {
        _data = existing != null ? Clone(existing) : new TravauxFinancement();
        _onSave = onSave;
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
            "Compant", "Emprunt"
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

        txtCoutTotal.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
        txtMontantEmprunte.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
        txtTaux.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
        dropdownDuree.onValueChanged.AddListener(_ => RefreshCalculs());

        btnSauvegarder.onClick.RemoveAllListeners();
        btnSauvegarder.onClick.AddListener(Save);
        btnAnnuler.onClick.RemoveAllListeners();
        btnAnnuler.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void ForceLayoutRebuild()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    private void RefreshCalculs()
    {
        float cout = Parse(txtCoutTotal.GetValue());
        float montant = Parse(txtMontantEmprunte.GetValue());
        float taux = Parse(txtTaux.GetValue());
        int duree = GetDuree();

        float apport = cout - montant;
        float mens = DropdownComptant.value == 1
            ? RentabiliteCalculator.Mensualite(montant, taux, duree) : 0f;
        float coutCreditTotal = mens * duree;

        if (txtApport != null) txtApport.text = $"{Mathf.Max(0f, apport):N0} €";
        if (txtMensualite != null) txtMensualite.text = DropdownComptant.value == 1 ? $"{mens:N2} € / mois" : "Comptant";
        if (txtCoutTotalCredit != null) txtCoutTotalCredit.text = DropdownComptant.value == 1
            ? $"Coût total crédit : {coutCreditTotal:N0} €" : "";
    }

    private void Save()
    {
        _data.description = txtDescription.GetNewSave();
        _data.coutTotal = Parse(txtCoutTotal.GetNewSave());
        _data.emprunt = DropdownComptant.value == 1;
        _data.montantEmprunte = Parse(txtMontantEmprunte.GetNewSave());
        _data.tauxInteretAnnuel = Parse(txtTaux.GetNewSave());
        _data.dureeMois = GetDuree();
        _data.mensualiteCalculee = _data.emprunt
            ? RentabiliteCalculator.Mensualite(_data.montantEmprunte, _data.tauxInteretAnnuel, _data.dureeMois)
            : 0f;
        _data.apportPersonnel = _data.coutTotal - _data.montantEmprunte;
        _data.dateDebutTravaux = datePicker?.saveThedate().ToString("yyyy-MM-dd") ?? "";
        _onSave?.Invoke(_data);
        gameObject.SetActive(false);
    }

    private float Parse(string s)
    {
        float.TryParse(s?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v);
        return v;
    }

    private int GetDuree() => dropdownDuree.value < Durees.Length ? Durees[dropdownDuree.value] : 120;
    private void SetDuree(int m) { for (int i = 0; i < Durees.Length; i++) if (Durees[i] == m) { dropdownDuree.value = i; return; } }
    private static TravauxFinancement Clone(TravauxFinancement t)
        => JsonUtility.FromJson<TravauxFinancement>(JsonUtility.ToJson(t));
}