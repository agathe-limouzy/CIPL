using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TypeInvestissement { Achat, Travaux }

public class InvestissementListPanel : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text txtTitre;
    public Transform listContent;
    public GameObject achatItemPrefab;
    public GameObject travauxItemPrefab;
    public Button btnAjouter;
    public Button btnFermer;

    [Header("Accent par type")]
    public Image headerBg;
    public Image ajouterBg;
    public TMP_Text ajouterLabel;

    [Header("Form panels")]
    public AchatFormPanel achatFormPanel;
    public TravauxFormPanel travauxFormPanel;

    private BatimentPrefab _batiment;
    private TypeInvestissement _type;
    private Action _onChanged;

    // Mode « charges » (réutilise ce panneau au même visuel qu'Achat / Travaux).
    private bool _charge;
    private int _chargeYear;              // 0 = toutes
    private GameObject _yearBarGO;
    private Transform _yearChips;
    private static readonly Color ChargeAccent = ColHex("#7A5AA6");
    private static readonly Color ChargeAccentClair = ColHex("#ECE4F5");
    private static Color ColHex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    public void Open(BatimentPrefab bp, TypeInvestissement type, Action onChanged)
    {
        _batiment = bp;
        _type = type;
        _onChanged = onChanged;
        _charge = false;
        if (_yearBarGO != null) _yearBarGO.SetActive(false);

        bool achat = type == TypeInvestissement.Achat;
        txtTitre.text = achat ? "Historique des achats" : "Historique des travaux";

        Color acc = achat ? Col("#185FA5") : Col("#854F0B");
        Color accT = achat ? Col("#E6F1FB") : Col("#FAEEDA");
        if (headerBg != null) headerBg.color = acc;
        if (txtTitre != null) txtTitre.color = accT;
        if (ajouterBg != null) ajouterBg.color = acc;
        if (ajouterLabel != null)
        {
            ajouterLabel.color = accT;
            ajouterLabel.text = achat ? "+ Ajouter un achat" : "+ Ajouter des travaux";
        }

        gameObject.SetActive(true);
        Rebuild();

        btnAjouter.onClick.RemoveAllListeners();
        btnAjouter.onClick.AddListener(OpenAddForm);

        btnFermer.onClick.RemoveAllListeners();
        btnFermer.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void Rebuild()
    {
        foreach (Transform child in listContent) Destroy(child.gameObject);

        if (_type == TypeInvestissement.Achat)
        {
            foreach (var achat in _batiment.getBatiment().historiquesAchat)
            {
                var go = Instantiate(achatItemPrefab, listContent);
                go.GetComponent<AchatItemUI>().Setup(achat,
                    () => OpenAchatForm(achat),
                    () => DemanderSuppressionAchat(achat));
            }
        }
        else
        {
            foreach (var travaux in _batiment.getBatiment().travaux)
            {
                var go = Instantiate(travauxItemPrefab, listContent);
                go.GetComponent<TravauxItemUI>().Setup(travaux,
                    () => OpenTravauxForm(travaux),
                    () => DemanderSuppressionTravaux(travaux));
            }
        }

        // ← Forcer le rebuild après instantiation
        StartCoroutine(RebuildLayout());
    }

    private System.Collections.IEnumerator RebuildLayout()
    {
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        var rt = listContent as RectTransform;
        while (rt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            rt = rt.parent as RectTransform;
        }
    }

    private void OpenAddForm()
    {
        if (_type == TypeInvestissement.Achat)
            OpenAchatForm(null);
        else
            OpenTravauxForm(null);
    }

    // Ouvre un formulaire en masquant la carte liste dessous (pas de superposition),
    // puis la ré-affiche quand le formulaire se ferme (sauvegarde ou annulation).
    private void OpenAchatForm(AchatFinancement a)
    {
        var card = transform.childCount > 0 ? transform.GetChild(0).gameObject : null;
        if (card != null) card.SetActive(false);
        achatFormPanel.Open(a, OnAchatSaved, () => { if (card != null) card.SetActive(true); });
    }

    private void OpenTravauxForm(TravauxFinancement t)
    {
        var card = transform.childCount > 0 ? transform.GetChild(0).gameObject : null;
        if (card != null) card.SetActive(false);
        travauxFormPanel.Open(t, OnTravauxSaved, () => { if (card != null) card.SetActive(true); });
    }

    private void OnAchatSaved(AchatFinancement data)
    {
        var list = _batiment.getBatiment().historiquesAchat;
        int idx = list.FindIndex(a => a.id == data.id);
        if (idx >= 0) list[idx] = data; else list.Add(data);
        SaveAndRefresh();
    }

    private void OnTravauxSaved(TravauxFinancement data)
    {
        var list = _batiment.getBatiment().travaux;
        int idx = list.FindIndex(t => t.id == data.id);
        if (idx >= 0) list[idx] = data; else list.Add(data);
        SaveAndRefresh();
    }

    private void DemanderSuppressionAchat(AchatFinancement achat)
    {
        string nom = string.IsNullOrEmpty(achat.label) ? "cet achat" : achat.label;
        ConfirmDialog.Instance.Show("Supprimer l'achat", $"Supprimer « {nom} » ?", () =>
        {
            _batiment.getBatiment().historiquesAchat.RemoveAll(a => a.id == achat.id);
            SaveAndRefresh();

            UndoToast.Instance.Show("Achat supprimé", () =>
            {
                _batiment.getBatiment().historiquesAchat.Add(achat);
                SaveAndRefresh();
            });
        });
    }

    private void DemanderSuppressionTravaux(TravauxFinancement travaux)
    {
        string nom = string.IsNullOrEmpty(travaux.description) ? "ces travaux" : travaux.description;
        ConfirmDialog.Instance.Show("Supprimer les travaux", $"Supprimer « {nom} » ?", () =>
        {
            _batiment.getBatiment().travaux.RemoveAll(t => t.id == travaux.id);
            SaveAndRefresh();

            UndoToast.Instance.Show("Travaux supprimés", () =>
            {
                _batiment.getBatiment().travaux.Add(travaux);
                SaveAndRefresh();
            });
        });
    }

    private static Color Col(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    private void SaveAndRefresh()
    {
        BatimentManager.Instance.SaveBatiment(_batiment.getBatiment());
        _onChanged?.Invoke();
        if (gameObject.activeInHierarchy)
            Rebuild();
    }

    // ── Mode « charges » ───────────────────────────────────────────────────────
    // Réutilise l'en-tête, la liste et le prefab d'item d'Achat/Travaux pour un
    // rendu identique. Le formulaire est fourni par ChargePanel (modale).

    public void OpenCharges(BatimentPrefab bp, Action onChanged)
    {
        _batiment = bp;
        _onChanged = onChanged;
        _charge = true;

        if (txtTitre != null) { txtTitre.text = "Historique des charges"; txtTitre.color = ChargeAccentClair; }
        if (headerBg != null) headerBg.color = ChargeAccent;
        if (ajouterBg != null) ajouterBg.color = ChargeAccent;
        if (ajouterLabel != null) { ajouterLabel.color = ChargeAccentClair; ajouterLabel.text = "+ Ajouter une charge"; }

        gameObject.SetActive(true);
        EnsureYearBar();
        RebuildCharges();

        btnAjouter.onClick.RemoveAllListeners();
        btnAjouter.onClick.AddListener(() => ChargePanel.OpenForm(_batiment, null, OnChargeSaved));

        btnFermer.onClick.RemoveAllListeners();
        btnFermer.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void OnChargeSaved()
    {
        BatimentManager.Instance.SaveBatiment(_batiment.getBatiment());
        _onChanged?.Invoke();
        if (gameObject.activeInHierarchy) { EnsureYearBar(); RebuildCharges(); }
    }

    private void RebuildCharges()
    {
        foreach (Transform child in listContent) Destroy(child.gameObject);

        var bat = _batiment.getBatiment();
        if (bat.charges == null) bat.charges = new List<ChargeBatiment>();

        var charges = bat.charges
            .Where(c => _chargeYear == 0 || YearOf(c) == _chargeYear)
            .OrderByDescending(c => c.dateISO ?? "").ToList();

        foreach (var ch in charges)
        {
            var ch2 = ch;
            var go = Instantiate(achatItemPrefab, listContent);
            var ui = go.GetComponent<AchatItemUI>();
            if (ui == null) continue;

            if (ui.txtLabel != null) ui.txtLabel.text = string.IsNullOrWhiteSpace(ch.nom) ? "(charge)" : ch.nom;
            if (ui.txtPrix != null) ui.txtPrix.text = $"{ch.cout:N0} €";
            if (ui.txtMensualite != null)
            {
                ui.txtMensualite.text = ch.paye ? "Payé" : "Impayé";
                ui.txtMensualite.color = ch.paye ? Col("#0F6E56") : Col("#D85A30");
            }
            if (ui.txtDate != null)
            {
                string date = DateTime.TryParse(ch.dateISO, out var d) ? d.ToString("dd/MM/yyyy") : "—";
                ui.txtDate.text = $"{date} · {QuiConcerne(ch)}";
            }
            if (ui.btnEdit != null)
            {
                ui.btnEdit.onClick.RemoveAllListeners();
                ui.btnEdit.onClick.AddListener(() => ChargePanel.OpenForm(_batiment, ch2, OnChargeSaved));
            }
            if (ui.btnDelete != null)
            {
                ui.btnDelete.onClick.RemoveAllListeners();
                ui.btnDelete.onClick.AddListener(() => DemanderSuppressionCharge(ch2));
            }
        }
        StartCoroutine(RebuildLayout());
    }

    private void DemanderSuppressionCharge(ChargeBatiment ch)
    {
        string nom = string.IsNullOrEmpty(ch.nom) ? "cette charge" : ch.nom;
        ConfirmDialog.Instance.Show("Supprimer la charge", $"Supprimer « {nom} » ?", () =>
        {
            _batiment.getBatiment().charges.RemoveAll(c => c.id == ch.id);
            OnChargeSaved();
            UndoToast.Instance.Show("Charge supprimée", () =>
            {
                _batiment.getBatiment().charges.Add(ch);
                OnChargeSaved();
            });
        });
    }

    private string QuiConcerne(ChargeBatiment ch)
    {
        if (ch.tousLocataires) return "Tous les locataires";
        var noms = (ch.locatairesConcernes ?? new List<string>())
            .Select(id => _batiment.listLocataire.FirstOrDefault(l => l.id == id))
            .Where(l => l != null).Select(l => string.IsNullOrEmpty(l.Name) ? "?" : l.Name).ToList();
        return noms.Count == 0 ? "Aucun locataire" : string.Join(" · ", noms);
    }

    // Filtre par année : barre de chips injectée dans le corps de la carte.

    private static int YearOf(ChargeBatiment ch) => DateTime.TryParse(ch.dateISO, out var d) ? d.Year : 0;

    private void EnsureYearBar()
    {
        if (_yearBarGO == null)
        {
            var card = transform.childCount > 0 ? transform.GetChild(0) : null;
            var bodyT = card != null ? card.Find("Body") : null;
            if (bodyT == null) return;

            var bar = UIFactory.HBox(bodyT, 6, false, "YearBar");
            bar.padding = new RectOffset(18, 14, 4, 4);
            _yearBarGO = bar.gameObject;
            UIFactory.LE(_yearBarGO, minH: 36);
            var lbl = UIFactory.Text(bar.transform, "Année :", 15, UITheme.TexteSecondaire);
            UIFactory.LE(lbl.gameObject, prefW: 64, flexW: 0);
            _yearChips = UIFactory.HBox(bar.transform, 6, false, "Chips").transform;
            UIFactory.LE(((Transform)_yearChips).gameObject, flexW: 1);
            _yearBarGO.transform.SetSiblingIndex(0);
        }
        _yearBarGO.SetActive(true);
        RebuildYearChips();
    }

    private void RebuildYearChips()
    {
        if (_yearChips == null) return;
        var bat = _batiment.getBatiment();
        var years = (bat.charges ?? new List<ChargeBatiment>())
            .Select(YearOf).Where(y => y > 0).Distinct().OrderByDescending(y => y).ToList();
        if (_chargeYear != 0 && !years.Contains(_chargeYear)) _chargeYear = 0;

        foreach (Transform c in _yearChips) Destroy(c.gameObject);
        AddYearChip("Toutes", 0);
        foreach (var y in years) AddYearChip(y.ToString(), y);
    }

    private void AddYearChip(string label, int year)
    {
        bool on = _chargeYear == year;
        var b = UIFactory.Button(_yearChips, label, on ? ChargeAccent : UITheme.Carte,
            on ? Color.white : UITheme.TextePrincipal, 30, 14, false);
        UIFactory.Border(b.gameObject);
        UIFactory.LE(b.gameObject, prefW: 80, flexW: 0);
        b.onClick.AddListener(() => { _chargeYear = year; RebuildYearChips(); RebuildCharges(); });
    }
}