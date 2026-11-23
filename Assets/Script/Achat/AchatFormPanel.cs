using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchatFormPanel : MonoBehaviour
{
    [Header("Champs")]
    public InputAndText txtLabel;
    public InputAndText txtPrixAchat;
    public InputAndText txtFraisNotaire;
    public Toggle toggleFraisAuto;
    public InputAndText txtFraisAgence;
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
    public TMP_Text txtCoutTotal;

    [Header("Boutons")]
    public Button btnSauvegarder;
    public Button btnAnnuler;

    private static readonly int[] Durees = { 60, 84, 120, 180, 240, 300 };
    bool _ecouteursPoses;   // revue 16c (D7b-17)
    private const double TAUX_FRAIS_AUTO = 0.08;   // Revue 17 (D7b-N9, lot G) : en double, comme le prix (en float : 7 999,99982 pour 100 000)

    private AchatFinancement _data;
    private Action<AchatFinancement> _onSave;
    private Action _onClose;

    public void Open(AchatFinancement existing, Action<AchatFinancement> onSave, Action onClose = null)
    {
        _data = existing != null ? Clone(existing) : new AchatFinancement { fraisNotaireAuto = true };   // nouvel achat : 8 % proposés
        _onSave = onSave;
        _onClose = onClose;
        gameObject.SetActive(true);
        PlafondFenetre.Poser(transform.GetChild(0) as RectTransform, "Body/Bouton");   // BZ7 (22/11) : jamais plus haute que l'écran, boutons fixes

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
            "Comptant", "Emprunt"
        });
    }

    private void ApplyToUI(AchatFinancement d)
    {
        txtLabel.ApplySave(d.label ?? "");
        txtLabel.Modify();

        txtPrixAchat.ApplySave(d.prixAchat.ToString());
        txtPrixAchat.Modify();

        txtFraisNotaire.ApplySave(d.fraisNotaire.ToString());
        txtFraisNotaire.Modify();

        txtFraisAgence.ApplySave(d.fraisAgence.ToString());
        txtFraisAgence.Modify();

        txtMontantEmprunte.ApplySave(d.montantEmprunte.ToString());
        txtMontantEmprunte.Modify();

        txtTaux.ApplySave(d.tauxInteretAnnuel.ToString());
        txtTaux.Modify();

        if (!string.IsNullOrEmpty(d.dateAchat) && DateTime.TryParse(d.dateAchat, out DateTime dt))
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
       
        if (toggleFraisAuto != null) toggleFraisAuto.interactable = true;
        if (toggleFraisAuto != null) toggleFraisAuto.SetIsOnWithoutNotify(d.fraisNotaireAuto);   // Revue 17 (D7b-02)
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
            if (toggleFraisAuto != null) UIFactory.CaseRonde(toggleFraisAuto);   // Revue visuelle (V-19) : la case ronde verte, comme partout
            toggleFraisAuto?.onValueChanged.AddListener(_ => RefreshCalculs());
            txtPrixAchat.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
            txtFraisNotaire.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
            txtFraisAgence.inputModify.onValueChanged.AddListener(_ => RefreshCalculs());
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

    // Revue 17 (D7b-08, lot D) : recalculé à chaque frappe, donc en silence (le message s'ouvrait au milieu de la
    // saisie) : « — » tant qu'un nombre est illisible ; le refus vient à « Sauvegarder ».
    private void RefreshCalculs()
    {
        double prix = Parse(txtPrixAchat.GetValue());
        double fraisNot = GetFraisNotaire(prix);
        double fraisAg = Parse(txtFraisAgence.GetValue());
        double montant = Parse(txtMontantEmprunte.GetValue());
        double taux = Parse(txtTaux.GetValue());
        int duree = GetDuree();

        if (toggleFraisAuto != null && toggleFraisAuto.isOn)
            txtFraisNotaire.ApplyValue(double.IsNaN(fraisNot) ? "" : $"{fraisNot:F0}");

        double invest = prix + fraisNot + fraisAg;
        double apport = invest - montant;
        double mens = DropdownComptant.value == 1
            ? RentabiliteCalculator.Mensualite(montant, taux, duree) : 0d;
        double coutCreditTotal = mens * duree;

        if (txtApport != null) txtApport.text = double.IsNaN(apport) ? "—" : $"{Montants.F(System.Math.Max(0d, apport), "N0")} €";
        if (txtMensualite != null) txtMensualite.text = DropdownComptant.value == 1 ? (double.IsNaN(mens) ? "—" : $"{Montants.F(mens, "N0")} € / mois") : "Comptant";
        if (txtCoutTotal != null) txtCoutTotal.text = DropdownComptant.value == 1 && !double.IsNaN(coutCreditTotal)
            ? $"{Montants.F(coutCreditTotal, "N0")} €" : "—";
    }

    private void Save()
    {
        // Revue 17 (D7b-08, lot D) : un nombre illisible partait à 0 après le message et le formulaire se fermait ; un
        // refus de date laissait les autres champs déjà repassés en lecture (GetNewSave). Tout est lu et contrôlé AVANT
        // d'écrire : au refus, le formulaire reste ouvert avec la saisie.
        bool fraisAuto = toggleFraisAuto != null && toggleFraisAuto.isOn;
        bool emprunt = DropdownComptant.value == 1;   // en « Comptant », les champs d'emprunt sont cachés : pas de refus sur eux
        string refus = SaisieNumerique.RefusIllisible(("Prix d'achat", txtPrixAchat.GetValue()),
                                                      ("Frais de notaire", fraisAuto ? "" : txtFraisNotaire.GetValue()),
                                                      ("Frais d'agence", txtFraisAgence.GetValue()),
                                                      ("Montant emprunté", emprunt ? txtMontantEmprunte.GetValue() : ""),
                                                      ("Taux d'intérêt", emprunt ? txtTaux.GetValue() : ""));
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }
        // Revue 16c (D7b-07) : la date est lue telle qu'affichée et doit être complète.
        if (datePicker != null)
        {
            if (!datePicker.LireDate(out var dateSaisie)) { ConfirmDialog.Erreur("Saisissez une date d'achat complète (jour, mois, année)."); return; }
            datePicker.ApplyDate(dateSaisie);
        }

        double prix = Parse(txtPrixAchat.GetNewSave());
        double fraisNot = GetFraisNotaire(prix);
        double fraisAg = Parse(txtFraisAgence.GetNewSave());

        _data.label = txtLabel.GetNewSave();
        _data.prixAchat = prix;
        _data.fraisNotaire = fraisNot;
        _data.fraisNotaireAuto = toggleFraisAuto != null && toggleFraisAuto.isOn;   // Revue 17 (D7b-02)
        _data.fraisAgence = fraisAg;
        _data.emprunt = DropdownComptant.value == 1;
        // Revue 17 (D7b-08, lot D) : un champ caché illisible (« Comptant ») garde l'ancienne valeur, jamais NaN au modèle.
        double montantLu = Parse(txtMontantEmprunte.GetNewSave()), tauxLu = Parse(txtTaux.GetNewSave());
        if (!double.IsNaN(montantLu)) _data.montantEmprunte = montantLu;
        if (!double.IsNaN(tauxLu)) _data.tauxInteretAnnuel = tauxLu;
        _data.dureeMois = GetDuree();
        _data.mensualiteCalculee = _data.emprunt
            ? RentabiliteCalculator.Mensualite(_data.montantEmprunte, _data.tauxInteretAnnuel, _data.dureeMois)
            : 0f;
        // Revue 17 (D7b-N9, lot G) : en « Comptant », le montant d'un ancien emprunt resté dans le champ caché rendait l'apport négatif.
        _data.apportPersonnel = prix + fraisNot + fraisAg - (_data.emprunt ? _data.montantEmprunte : 0d);

        // Save() — convertir DateTime? → string (date contrôlée plus haut)
        _data.dateAchat = datePicker?.saveThedate().ToString("yyyy-MM-dd") ?? "";

        _onSave?.Invoke(_data);
        gameObject.SetActive(false);
        _onClose?.Invoke();
    }

    private double GetFraisNotaire(double prix)
        => toggleFraisAuto != null && toggleFraisAuto.isOn
            ? prix * TAUX_FRAIS_AUTO
            : Parse(txtFraisNotaire.GetValue());

    // Voir SaisieNumerique : tolère la saisie française. Revue 17 (D7b-08, lot D) : silencieuse (NaN si illisible) ;
    // Save refuse avant d'écrire (RefusIllisible), RefreshCalculs affiche « — ».
    private double Parse(string s) => SaisieNumerique.OuNaN(s);   // revue 16c (D7b-12) : en double, comme le modèle

    private int GetDuree() => dropdownDuree.value < Durees.Length ? Durees[dropdownDuree.value] : 120;
    private void SetDuree(int m)
    {
        for (int i = 0; i < Durees.Length; i++) if (Durees[i] == m) { dropdownDuree.value = i; return; }
        dropdownDuree.value = System.Array.IndexOf(Durees, 120);   // revue 16c (D7b-17) : durée inconnue (nouvel élément) → 120 mois, pas celle du formulaire précédent
    }
    private static AchatFinancement Clone(AchatFinancement a)
        => JsonUtility.FromJson<AchatFinancement>(JsonUtility.ToJson(a));
}