using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RentabiliteGlobaleController : MonoBehaviour
{
    [Header("Boutons listes")]
    public Button btnOuvrirAchats;
    public Button btnOuvrirTravaux;

    [Header("Résumé investissement")]
    public TMP_Text txtInvestissementAchat;
    public TMP_Text txtInvestissementTravaux;
    public TMP_Text txtInvestissementTotal;

    [Header("Cash flow")]
    public TMP_Text txtMensualitesTotales;
    public TMP_Text txtLoyerAnnuel;
    public TMP_Text txtCashFlowAnnuel;
    public TMP_Text txtCashFlowMensuel;

    [Header("Rentabilité")]
    public TMP_Text txtBreakEven;
    public TMP_Text txtStatus;

    [Header("Tableau annuel")]
    public Transform tableauContent;
    public GameObject rentabiliteRowPrefab;

    [Header("Panel liste")]
    public InvestissementListPanel listPanel;

    // ── Interne ───────────────────────────────────────────────────────────────

    private BatimentPrefab _bp;

    private struct LoanEntry
    {
        public DateTime startDate;
        public float mensualite;
        public int dureeMois;
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(BatimentPrefab bp)
    {
        _bp = bp;

        btnOuvrirAchats.onClick.RemoveAllListeners();
        btnOuvrirAchats.onClick.AddListener(() =>
            listPanel.Open(_bp, TypeInvestissement.Achat, Refresh));

        btnOuvrirTravaux.onClick.RemoveAllListeners();
        btnOuvrirTravaux.onClick.AddListener(() =>
            listPanel.Open(_bp, TypeInvestissement.Travaux, Refresh));

        Refresh();
    }

    // ── Refresh principal ─────────────────────────────────────────────────────

    public void Refresh()
    {
        if (_bp == null) return;              // Init() pas encore appelé
        var bat = _bp.getBatiment();
        if (bat == null) return;

        if (bat.historiquesAchat == null)
            bat.historiquesAchat = new List<AchatFinancement>();
        if (bat.travaux == null)
            bat.travaux = new List<TravauxFinancement>();

        float loyerAnnuel = _bp.GetLoyerTotal();
     

        var loans = new List<LoanEntry>();

        // ── Achats ────────────────────────────────────────────────────────────
        float investAchat = 0f;
        foreach (var a in bat.historiquesAchat)
        {
            investAchat += a.prixAchat + a.fraisNotaire + a.fraisAgence;
            if (a.emprunt && a.dureeMois > 0)
            {
                loans.Add(new LoanEntry
                {
                    startDate = ParseDate(a.dateAchat),
                    mensualite = RentabiliteCalculator.Mensualite(
                        a.montantEmprunte, a.tauxInteretAnnuel, a.dureeMois),
                    dureeMois = a.dureeMois
                });
            }
        }

        // ── Travaux ───────────────────────────────────────────────────────────
        float investTravaux = 0f;
        foreach (var t in bat.travaux)
        {
            investTravaux += t.coutTotal;
            if (t.emprunt && t.dureeMois > 0)
            {
                loans.Add(new LoanEntry
                {
                    startDate = ParseDate(t.dateDebutTravaux),
                    mensualite = RentabiliteCalculator.Mensualite(
                        t.montantEmprunte, t.tauxInteretAnnuel, t.dureeMois),
                    dureeMois = t.dureeMois
                });
            }
        }

        // ── Totaux ────────────────────────────────────────────────────────────
        float investTotal = investAchat + investTravaux;
        float mensTotales = 0f;
        foreach (var l in loans) mensTotales += l.mensualite;

        float chargesAnnuel = mensTotales * 12f;
        float cashFlowAnnuel = loyerAnnuel - chargesAnnuel;
        float cashFlowMois = cashFlowAnnuel / 12f;
        float rendBrut = investTotal > 0 ? loyerAnnuel / investTotal * 100f : 0f;
        float rendNet = investTotal > 0 ? cashFlowAnnuel / investTotal * 100f : 0f;
        float breakEven = RentabiliteCalculator.BreakEvenAns(investTotal, cashFlowAnnuel);

        // ── Affichage résumé ──────────────────────────────────────────────────
        Set(txtInvestissementAchat, $"{investAchat:N0} €  ({bat.historiquesAchat.Count})");
        Set(txtInvestissementTravaux, $"{investTravaux:N0} €  ({bat.travaux.Count})");
        Set(txtInvestissementTotal, $"{investTotal:N0} €");
        Set(txtMensualitesTotales, $"{mensTotales:N0} € / mois");
        Set(txtLoyerAnnuel, $"{loyerAnnuel:N0} € / an");
        Set(txtCashFlowAnnuel, Signe(cashFlowAnnuel) + $"{cashFlowAnnuel:N0} € / an");
        Set(txtCashFlowMensuel, Signe(cashFlowMois) + $"{cashFlowMois:N0} € / mois");
        Set(txtBreakEven, cashFlowAnnuel > 0 ? $"{breakEven:F1} ans" : "—");

        if (txtStatus != null)
        {
            if (cashFlowAnnuel < 0) { txtStatus.text = "Effort d'epargne mensuel"; txtStatus.color = Color.red; }
            else if (breakEven <= 10f) { txtStatus.text = "Rentable < 10 ans"; txtStatus.color = Color.green; }
            else if (breakEven <= 20f) { txtStatus.text = "Rentable < 20 ans"; txtStatus.color = Color.yellow; }
            else { txtStatus.text = "Rentabilite longue"; txtStatus.color = Color.grey; }
        }

        // ── Tableau annuel ────────────────────────────────────────────────────
        if (tableauContent != null && rentabiliteRowPrefab != null)
            BuildTableauRentabilite(loans, loyerAnnuel, investTotal);
    }

    // ── Tableau année par année ───────────────────────────────────────────────

    private void BuildTableauRentabilite(List<LoanEntry> loans, float loyerAnnuel, float investTotal)
    {
        foreach (Transform child in tableauContent) Destroy(child.gameObject);

        if (loans.Count == 0 && investTotal <= 0f) return;

        // Date de départ = plus ancienne date de début parmi tous les prêts
        // (ou aujourd'hui si pas de prêt)
        DateTime dateDebut = DateTime.Today;
        foreach (var l in loans)
            if (l.startDate < dateDebut) dateDebut = l.startDate;

        // Fin du dernier prêt
        DateTime dateFin = dateDebut;
        foreach (var l in loans)
        {
            DateTime loanEnd = l.startDate.AddMonths(l.dureeMois);
            if (loanEnd > dateFin) dateFin = loanEnd;
        }

        // Afficher jusqu'à la fin du dernier crédit + 5 ans, max 40 ans
        int nbAnnees = Mathf.Min(dateFin.Year - dateDebut.Year + 6, 40);

        float cumul = -investTotal;  // On part du coût total investi
        float cumulPrev;

        for (int i = 0; i < nbAnnees; i++)
        {
            int annee = dateDebut.Year + i;

            // Loyer perçu cette année (proraté pour la première année)
            float loyerAnnee;
            if (i == 0)
                loyerAnnee = loyerAnnuel * (float)(12 - dateDebut.Month + 1) / 12f;
            else
                loyerAnnee = loyerAnnuel;

            // Charges crédit actives cette année
            float chargesAnnee = 0f;
            foreach (var loan in loans)
            {
                int moisActifs = MoisActifsDansAnnee(loan.startDate, loan.dureeMois, annee);
                chargesAnnee += moisActifs * loan.mensualite;
            }

            float netAnnee = loyerAnnee - chargesAnnee;
            cumulPrev = cumul;
            cumul += netAnnee;

            bool seuilAtteint = cumulPrev < 0f && cumul >= 0f;

            var go = Instantiate(rentabiliteRowPrefab, tableauContent);
            var row = go.GetComponent<RentabiliteRow>();
            row.Setup(annee, netAnnee, cumul, seuilAtteint);
        }
    }

    // Nombre de mois du prêt actifs durant l'année calendaire donnée
    private static int MoisActifsDansAnnee(DateTime loanStart, int dureeMois, int annee)
    {
        int startAbs = loanStart.Year * 12 + loanStart.Month - 1;
        int endAbs = startAbs + dureeMois;          // exclusif
        int yearStart = annee * 12;                    // janvier de l'année
        int yearEnd = yearStart + 12;                // exclusif

        int overlap = Math.Max(0, Math.Min(endAbs, yearEnd) - Math.Max(startAbs, yearStart));
        return overlap;
    }

    // ── Utilitaires ───────────────────────────────────────────────────────────

    private static DateTime ParseDate(string s)
    {
        if (DateTime.TryParse(s, out DateTime d)) return d;
        return DateTime.Today;
    }

    private static void Set(TMP_Text t, string v) { if (t != null) t.text = v; }
    private static string Signe(float v) => v >= 0 ? "+" : "";
}