using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TravauxController : MonoBehaviour
{
    [Header("Saisie")]
    public InputAndText txtCoutTotal;
    public InputAndText txtDescription;
    public DateInputController dateDebutTravaux;

    [Header("Mode financement")]
    public TMP_Dropdown DropdownComptant;

    [Header("Panel Emprunt")]
    public GameObject panelEmprunt;
    public InputAndText txtMontantEmprunte;
    public InputAndText txtApport;
    public InputAndText txtTauxInteret;
    public TMP_Dropdown dropdownDuree;
    public TMP_Text txtMensualite;
    public TMP_Text txtCoutTotalCredit;

    [Header("Résumé rentabilité")]
    public TMP_Text txtInvestissementTotal;
    public TMP_Text txtCashFlowAnnuel;
    public TMP_Text txtBreakEven;
    public TMP_Text txtStatusBreakEven;

    [Header("Tableau")]
    public Transform rentabiliteListParent;
    public GameObject rentabiliteRowPrefab;
    public int nbAnneesAffichees = 20;

    // ── Internes ──────────────────────────────────────────────────────────────

    private BatimentPrefab _batimentPrefab;
    private TravauxFinancement _data;

    private static readonly int[] Durees = { 60, 84, 120, 180, 240, 300 };

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(BatimentPrefab batimentPrefab, TravauxFinancement data)
    {
        _batimentPrefab = batimentPrefab;
        _data = data;

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

        DropdownComptant.onValueChanged.RemoveAllListeners();
        DropdownComptant.onValueChanged.AddListener(isOn => {  SetModeEmprunt(isOn==0); });

        txtCoutTotal.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
        txtMontantEmprunte.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
        txtTauxInteret.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
        dropdownDuree.onValueChanged.AddListener(_ => RefreshCalculs());
        dateDebutTravaux?.OnModify.AddListener(RefreshCalculs);

        ApplyData(data);
    }

    // ── Chargement ────────────────────────────────────────────────────────────

    public void ApplyData(TravauxFinancement data)
    {
        _data = data;

        txtCoutTotal.ApplySave(data.coutTotal.ToString());
        txtDescription.ApplySave(data.description);
        txtMontantEmprunte.ApplySave(data.montantEmprunte.ToString());
        txtApport.ApplySave(data.apportPersonnel.ToString());
        txtTauxInteret.ApplySave(data.tauxInteretAnnuel.ToString());
        SetDureeDropdown(data.dureeMois);
        dateDebutTravaux?.ApplyDate(data.dateDebutTravaux == default
            ? DateTime.Today : data.dateDebutTravaux);

        if (data.emprunt)
        {
            DropdownComptant.value = 1;
        }else
        {
            DropdownComptant.value = 0;
        }
        
        DropdownComptant.onValueChanged.RemoveAllListeners();
        DropdownComptant.onValueChanged.AddListener(isOn => { SetModeEmprunt(isOn != 0); });

        SetModeEmprunt(data.emprunt, silent: true);
        RefreshCalculs();
    }

    public void Modify()
    {
        txtCoutTotal.Modify();
        txtDescription.Modify();
        txtMontantEmprunte.Modify();
        txtApport.Modify();
        txtTauxInteret.Modify();
        dateDebutTravaux?.ModifyDate();
        dropdownDuree.interactable = true;
        DropdownComptant.interactable = true;
    }

    public void ApplySave()
    {
        txtCoutTotal.ApplySave(txtCoutTotal.GetNewSave());
        txtDescription.ApplySave(txtDescription.GetNewSave());
        txtMontantEmprunte.ApplySave(txtMontantEmprunte.GetNewSave());
        txtApport.ApplySave(txtApport.GetNewSave());
        txtTauxInteret.ApplySave(txtTauxInteret.GetNewSave());
        dateDebutTravaux?.ApplyDate(dateDebutTravaux.saveThedate());
        dropdownDuree.interactable = false;
        DropdownComptant.interactable = false;
    }

    public TravauxFinancement GetSaveData()
    {
        _data.coutTotal = Parse(txtCoutTotal.GetNewSave());
        _data.description = txtDescription.GetNewSave();
        _data.emprunt = (DropdownComptant.value == 1);
        _data.montantEmprunte = Parse(txtMontantEmprunte.GetNewSave());
        _data.apportPersonnel = Parse(txtApport.GetNewSave());
        _data.tauxInteretAnnuel = Parse(txtTauxInteret.GetNewSave());
        _data.dureeMois = GetDureeMois();
        _data.mensualiteCalculee = RentabiliteCalculator.Mensualite(
            _data.montantEmprunte, _data.tauxInteretAnnuel, _data.dureeMois);
        _data.dateDebutTravaux = dateDebutTravaux?.saveThedate() ?? DateTime.Today;
        return _data;
    }

    // ── Mode financement ──────────────────────────────────────────────────────

    private void SetModeEmprunt(bool emprunt, bool silent = false)
    {
        panelEmprunt?.SetActive(emprunt);
        if (!silent) RefreshCalculs();
    }

    // ── Calculs ───────────────────────────────────────────────────────────────

    private void RefreshCalculs()
    {
        float coutTravaux = Parse(txtCoutTotal.GetValue());
        float montant = DropdownComptant.value==1 ? Parse(txtMontantEmprunte.GetValue()) : 0f;
        float taux = Parse(txtTauxInteret.GetValue());
        int duree = GetDureeMois();
        float apport = coutTravaux - montant;
        float loyerAnnuel = _batimentPrefab != null ? _batimentPrefab.GetLoyerTotal() : 0f;
        float coutAchat = _batimentPrefab != null ? _batimentPrefab.GetCoutAchat() : 0f;

        if (txtApport != null)
            txtApport.ApplyValue(apport >= 0 ? $"{apport:F2}" : "0");

        float mensualite = DropdownComptant.value == 1
            ? RentabiliteCalculator.Mensualite(montant, taux, duree) : 0f;
        float coutCreditTotal = mensualite * duree;
        float interets = coutCreditTotal - montant;

        if (txtMensualite != null)
            txtMensualite.text = DropdownComptant.value == 1 ? $"{mensualite:F2} € / mois" : "—";

        if (txtCoutTotalCredit != null)
            txtCoutTotalCredit.text = DropdownComptant.value == 1
                ? $"{coutCreditTotal:F2} € (dont {interets:F2} € d'intérêts)"
                : "—";

        float investissementTotal = coutAchat + coutTravaux;
        float cashFlowAnnuel = loyerAnnuel - mensualite * 12f;
        float breakEvenAns = RentabiliteCalculator.BreakEvenAns(
            investissementTotal, cashFlowAnnuel);

        if (txtInvestissementTotal != null)
            txtInvestissementTotal.text = $"{investissementTotal:F0} €";

        if (txtCashFlowAnnuel != null)
            txtCashFlowAnnuel.text = $"{cashFlowAnnuel:F0} € / an";

        if (txtBreakEven != null)
            txtBreakEven.text = cashFlowAnnuel > 0 ? $"{breakEvenAns:F1} ans" : "—";

        if (txtStatusBreakEven != null)
        {
            if (cashFlowAnnuel <= 0) txtStatusBreakEven.text = "⚠ Cash flow négatif";
            else if (breakEvenAns <= 10f) txtStatusBreakEven.text = "✅ Rentable < 10 ans";
            else if (breakEvenAns <= 20f) txtStatusBreakEven.text = "📅 Rentable < 20 ans";
            else txtStatusBreakEven.text = "⏳ Rentabilité longue";
        }

        BuildRentabiliteTable(investissementTotal, loyerAnnuel, mensualite, duree);
    }

    // ── Tableau année par année ───────────────────────────────────────────────

    private void BuildRentabiliteTable(float investissement, float loyerAnnuel,
        float mensualite, int dureeMois)
    {
        foreach (Transform child in rentabiliteListParent) Destroy(child.gameObject);

        DateTime dateDebut = dateDebutTravaux?.saveThedate() ?? DateTime.Today;
        int anneeDebut = dateDebut.Year;
        int moisDebut = dateDebut.Month;

        float cumulatif = -investissement;

        for (int i = 0; i < nbAnneesAffichees; i++)
        {
            int anneeReelle = anneeDebut + i;

            // Mois de remboursement actifs sur cette année civile
            int moisCreditActifs = 0;
            for (int mois = 0; mois < 12; mois++)
            {
                int moisDepuisDebut = i * 12 + mois - (moisDebut - 1);
                if (moisDepuisDebut >= 0 && moisDepuisDebut < dureeMois)
                    moisCreditActifs++;
            }

            // Loyer proratisé la première année selon le mois de début des travaux
            float revenuAnnee = i == 0
                ? loyerAnnuel * (12 - moisDebut + 1) / 12f
                : loyerAnnuel;

            float coutAnnee = mensualite * moisCreditActifs;
            float netAnnee = revenuAnnee - coutAnnee;
            cumulatif += netAnnee;

            var go = Instantiate(rentabiliteRowPrefab, rentabiliteListParent);
            var row = go.GetComponent<RentabiliteRow>();
            row.Setup(anneeReelle, netAnnee, cumulatif);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            rentabiliteListParent.GetComponent<RectTransform>());
    }

    // ── Utilitaires ───────────────────────────────────────────────────────────

    private float Parse(string s)
    {
        float.TryParse(s?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v);
        return v;
    }

    private int GetDureeMois()
    {
        return dropdownDuree.value < Durees.Length ? Durees[dropdownDuree.value] : 120;
    }

    private void SetDureeDropdown(int mois)
    {
        for (int i = 0; i < Durees.Length; i++)
            if (Durees[i] == mois) { dropdownDuree.value = i; return; }
    }
}