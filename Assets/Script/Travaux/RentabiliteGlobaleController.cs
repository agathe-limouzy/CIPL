using System;
using System.Collections;
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
    private Button _btnCharges;   // bouton « Charges » cloné à côté de Travaux

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

        // Bouton « Charges » : cloné à côté de « Travaux » (une seule fois),
        // ouvre l'écran Charges du bâtiment (code-first, module facturation).
        if (_btnCharges == null && btnOuvrirTravaux != null)
        {
            _btnCharges = Instantiate(btnOuvrirTravaux, btnOuvrirTravaux.transform.parent);
            _btnCharges.name = "btnOuvrirCharges";
            var lbl = _btnCharges.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) { lbl.text = "+ Charges"; lbl.color = Col("#7A5AA6"); }
            var img = _btnCharges.GetComponent<Image>();
            if (img != null) img.color = Col("#ECE4F5");
            _btnCharges.transform.SetSiblingIndex(btnOuvrirTravaux.transform.GetSiblingIndex() + 1);
        }
        if (_btnCharges != null)
        {
            _btnCharges.onClick.RemoveAllListeners();
            _btnCharges.onClick.AddListener(() => listPanel.OpenCharges(_bp, Refresh));
        }

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

        Color cfCol = cashFlowAnnuel >= 0 ? Col("#0F6E56") : Col("#D85A30");
        if (txtCashFlowAnnuel != null) txtCashFlowAnnuel.color = cfCol;
        if (txtCashFlowMensuel != null) txtCashFlowMensuel.color = cfCol;

        if (txtStatus != null)
        {
            if (cashFlowAnnuel < 0) { txtStatus.text = "Effort d'épargne"; txtStatus.color = Col("#D85A30"); }
            else if (breakEven <= 10f) { txtStatus.text = "Rentable < 10 ans"; txtStatus.color = Col("#0F6E56"); }
            else if (breakEven <= 20f) { txtStatus.text = "Rentable < 20 ans"; txtStatus.color = Col("#854F0B"); }
            else { txtStatus.text = "Rentabilité longue"; txtStatus.color = Col("#888780"); }
        }

        // ── Tableau annuel (Rentrées / Coût achat / Coût travaux / Cumulé) ────
        if (tableauContent != null && rentabiliteRowPrefab != null)
        {
            var locataires = bat != null ? _bp.listLocataire : new List<Locataire>();
            var achats = bat != null ? bat.historiquesAchat : new List<AchatFinancement>();
            var travaux = bat != null ? bat.travaux : new List<TravauxFinancement>();
            var types = LoyerHistoryService.TypesUtilises(locataires);

            // 1) Affichage immédiat : cache si dispo, sinon repli loyer courant.
            //    Le tableau ne reste jamais vide en attendant le réseau.
            BuildTableauRentabilite(locataires, achats, travaux,
                LoyerHistoryService.ObsEnCache(types));

            // 2) Récupère les vrais indices INSEE puis reconstruit avec.
            if (isActiveAndEnabled)
                StartCoroutine(BuildTableauReel(locataires, achats, travaux, types));
        }
    }

    // Récupère les vrais indices INSEE (avec cache) puis reconstruit le tableau.
    private IEnumerator BuildTableauReel(List<Locataire> locataires,
        List<AchatFinancement> achats, List<TravauxFinancement> travaux,
        IEnumerable<IndiceImmo> types)
    {
        Dictionary<IndiceImmo, List<(string periode, float valeur)>> obs = null;
        yield return LoyerHistoryService.FetchIndices(types, o => obs = o);
        if (obs != null && obs.Count > 0)
            BuildTableauRentabilite(locataires, achats, travaux, obs);
    }

    // ── Tableau année par année ───────────────────────────────────────────────

    private void BuildTableauRentabilite(List<Locataire> locataires,
        List<AchatFinancement> achats, List<TravauxFinancement> travaux,
        Dictionary<IndiceImmo, List<(string periode, float valeur)>> obs)
    {
        foreach (Transform child in tableauContent) Destroy(child.gameObject);

        if (locataires == null) locataires = new List<Locataire>();
        if (achats == null) achats = new List<AchatFinancement>();
        if (travaux == null) travaux = new List<TravauxFinancement>();

        if (achats.Count == 0 && travaux.Count == 0 && locataires.Count == 0) return;

        // Bornes : plus ancienne dépense / premier bail (années raisonnables ≥ 1970,
        // pour ignorer les dates saisies aberrantes), jusqu'à aujourd'hui + 5 ans.
        int currentYear = DateTime.Today.Year;
        int anneeMin = int.MaxValue;
        foreach (var a in achats)       { int y = ParseDate(a.dateAchat).Year;        if (y >= 1970 && y <= currentYear + 1 && y < anneeMin) anneeMin = y; }
        foreach (var t in travaux)      { int y = ParseDate(t.dateDebutTravaux).Year;  if (y >= 1970 && y <= currentYear + 1 && y < anneeMin) anneeMin = y; }
        foreach (var loc in locataires) { int y = LoyerHistoryService.PremierBail(loc).Year; if (y >= 1970 && y <= currentYear + 1 && y < anneeMin) anneeMin = y; }
        if (anneeMin == int.MaxValue) anneeMin = currentYear;

        // Fin de fenêtre : aujourd'hui + 5 ans, prolongée jusqu'à la fin des emprunts.
        int anneeMax = currentYear + 5;
        foreach (var a in achats)
            if (a.emprunt && a.dureeMois > 0)
                anneeMax = Math.Max(anneeMax, StartClamped(a.dateAchat, anneeMin, currentYear).Year + (a.dureeMois + 11) / 12);
        foreach (var t in travaux)
            if (t.emprunt && t.dureeMois > 0)
                anneeMax = Math.Max(anneeMax, StartClamped(t.dateDebutTravaux, anneeMin, currentYear).Year + (t.dureeMois + 11) / 12);

        int nbAnnees = Mathf.Min(anneeMax - anneeMin + 1, 60);

        float cumul = 0f;
        for (int i = 0; i < nbAnnees; i++)
        {
            int annee = anneeMin + i;

            // Rentrées = loyers réels indexés de chaque locataire (0 avant le bail).
            float rentrees = 0f;
            foreach (var loc in locataires)
                rentrees += LoyerHistoryService.LoyerPourAnnee(loc, annee, obs);

            // Coûts de l'année : plein tarif l'année de la dépense si comptant,
            // sinon apport (année d'achat) + mensualités étalées sur la durée du prêt.
            float coutAchat = 0f;
            foreach (var a in achats)
                coutAchat += CoutFinance(a.prixAchat + a.fraisNotaire + a.fraisAgence,
                    StartClamped(a.dateAchat, anneeMin, currentYear),
                    a.emprunt, a.montantEmprunte, a.tauxInteretAnnuel, a.dureeMois, annee);

            float coutTravaux = 0f;
            foreach (var t in travaux)
                coutTravaux += CoutFinance(t.coutTotal,
                    StartClamped(t.dateDebutTravaux, anneeMin, currentYear),
                    t.emprunt, t.montantEmprunte, t.tauxInteretAnnuel, t.dureeMois, annee);

            float cumulPrev = cumul;
            cumul += rentrees - coutAchat - coutTravaux;
            bool seuilAtteint = cumulPrev < 0f && cumul >= 0f;

            var go = Instantiate(rentabiliteRowPrefab, tableauContent);
            go.GetComponent<RentabiliteRow>()
              .Setup(annee, rentrees, coutAchat, coutTravaux, cumul, seuilAtteint);
        }
    }

    // Date de départ d'une dépense, ramenée à une année raisonnable si aberrante.
    private static DateTime StartClamped(string dateISO, int anneeMin, int currentYear)
    {
        DateTime d = ParseDate(dateISO);
        int y = Mathf.Clamp(d.Year, anneeMin, currentYear + 1);
        return y == d.Year ? d : new DateTime(y, 1, 1);
    }

    // Coût réellement décaissé une année donnée : plein tarif l'année de la dépense
    // si comptant ; sinon apport (année de départ) + mensualités du prêt actives cette année.
    private static float CoutFinance(float total, DateTime start, bool emprunt,
        float montantEmprunte, float taux, int dureeMois, int annee)
    {
        if (emprunt && dureeMois > 0 && montantEmprunte > 0f)
        {
            float apport = Mathf.Max(0f, total - montantEmprunte);
            float mens = RentabiliteCalculator.Mensualite(montantEmprunte, taux, dureeMois);
            float cout = start.Year == annee ? apport : 0f;
            cout += mens * MoisActifsDansAnnee(start, dureeMois, annee);
            return cout;
        }
        return start.Year == annee ? total : 0f;
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
    private static Color Col(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}