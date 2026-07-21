using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LoyerRevisionController : MonoBehaviour
{
    [Header("UI — Sélecteur de mode")]
    public TMP_Dropdown modeDropdown;

    [Header("UI — Entrées communes")]
    public InputAndText loyerDepart;
    public TrimestreInput dateDepart;
    public TMP_Dropdown indiceDropdown;
    public DateInputController dateDeRevision;
    public LocatairePrefab locatairePrefab;

    [Header("UI — Boutons d'action")]
    public GameObject panelNouveauBail;
    public Button btnInitialiser;
    public GameObject panelBailEnCours;
    public Button calculateButton;

    [Header("UI — Résultats")]
    public TMP_Text indiceDepuisText;
    public TMP_Text indiceActuelText;
    public TMP_Text louerActuelText;
    public TMP_Text statusText;
    public InputAndText loyerAnnuelPrecedent;

    // ── Internes ──────────────────────────────────────────────────────────────

    private RevisionMode _currentMode = RevisionMode.NouveauBail;

    private static readonly Dictionary<string, (string code, string label)> Indices =
        new Dictionary<string, (string, string)>
        {
            { "ILC",  ("001532540", "Indice des Loyers Commerciaux") },
            { "IRL",  ("001515334", "Indice de Référence des Loyers") },
            { "ILAT", ("001617113", "Indice Loyers Activités Tertiaires") },
        };

    private List<(string periode, float valeur)> _allObservations = new();

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init()
    {
        // Mode dropdown
        modeDropdown.ClearOptions();
        modeDropdown.AddOptions(new List<string> { "Nouveau bail", "Bail en cours" });
        modeDropdown.onValueChanged.RemoveAllListeners();
        modeDropdown.onValueChanged.AddListener(i =>
            SetMode(i == 0 ? RevisionMode.NouveauBail : RevisionMode.BailEnCours));

        // Indice dropdown
        indiceDropdown.ClearOptions();
        indiceDropdown.AddOptions(new List<string>(Indices.Keys));

        // Boutons
        btnInitialiser?.onClick.RemoveAllListeners();
        calculateButton?.onClick.RemoveAllListeners();
        btnInitialiser?.onClick.AddListener(OnInitialiserNouveauBail);
        calculateButton?.onClick.AddListener(OnCalculate);

        loyerDepart.SetPlaceholder("Loyer de départ (ex: 1200)");
        dateDepart.Init();
        dateDeRevision.OnModify.AddListener(CheckStateButtonLoyerRevision);
    }

    // ── API publique ──────────────────────────────────────────────────────────

    /// <summary>Nouveau locataire : affiche le dropdown, mode NouveauBail par défaut.</summary>
    public void InitForNewTenant()
    {
        modeDropdown.interactable=true;
        SetMode(RevisionMode.NouveauBail);
    }

    /// <summary>Locataire existant : auto-détecte le mode, cache le dropdown.</summary>
    public void SetLoyerAndIndice(float loyerActuel, string indiceActuel,
        string trimestreDeReference, IndiceImmo indice, string indiceDepart,
        float loyerDepartF, float loyerAnneePrecent, DateTime dateRevision)
    {
        bool dejaInitialise = !string.IsNullOrEmpty(indiceDepart) && indiceDepart != "—";
        SetMode(dejaInitialise ? RevisionMode.BailEnCours : RevisionMode.NouveauBail, silent: true);

        // Cache le dropdown — mode auto-détecté
        modeDropdown.interactable = false; ;

        louerActuelText.text = loyerActuel.ToString();
        indiceActuelText.text = indiceActuel;
        indiceDepuisText.text = indiceDepart;
        dateDepart.SetTrimestre(trimestreDeReference);
        dateDepart.CannotModify();
        indiceDropdown.value = (int)indice;
        indiceDropdown.interactable = false;
        loyerDepart.ApplySave(loyerDepartF.ToString());
        loyerAnnuelPrecedent.ApplySave(loyerAnneePrecent.ToString());
        dateDeRevision.ApplyDate(dateRevision);

        CheckStateButtonLoyerRevision();
    }

    public void Modify()
    {
        dateDepart.CanModify();
        indiceDropdown.interactable = true;
        loyerDepart.Modify();
        loyerAnnuelPrecedent.Modify();
        dateDeRevision.ModifyDate();

        // Remet le dropdown visible
        modeDropdown.interactable=true;

        CheckStateButtonLoyerRevision();
    }

    // ── Getters ───────────────────────────────────────────────────────────────

    public string getLoyerAnnuel() => louerActuelText.text;
    public string getLoyerDepart() => loyerDepart.GetNewSave();
    public string getLoyerAnnuelPrecende() => loyerAnnuelPrecedent.GetNewSave();
    public string getIndiceDepart() => indiceDepuisText.text;
    public string getIndiceActuel() => indiceActuelText.text;
    public IndiceImmo getTypeIndice() => (IndiceImmo)indiceDropdown.value;
    public string getTrimestredeReference() => dateDepart.TrimestreValue;
    public DateTime getPeriodeDeRevision() => dateDeRevision.saveThedate();

    // ── Gestion des modes ─────────────────────────────────────────────────────

    private void SetMode(RevisionMode mode, bool silent = false)
    {
        _currentMode = mode;
        bool isNouveauBail = mode == RevisionMode.NouveauBail;

        panelNouveauBail?.SetActive(isNouveauBail);
        panelBailEnCours?.SetActive(!isNouveauBail);

        // Sync dropdown sans retrigger l'event
        modeDropdown.onValueChanged.RemoveAllListeners();
        modeDropdown.value = isNouveauBail ? 0 : 1;
        modeDropdown.onValueChanged.AddListener(i =>
            SetMode(i == 0 ? RevisionMode.NouveauBail : RevisionMode.BailEnCours));

        if (!silent) statusText.text = "";

        CheckStateButtonLoyerRevision();
    }

    // ── Mode Nouveau Bail ─────────────────────────────────────────────────────

    private void OnInitialiserNouveauBail()
    {
        if (!float.TryParse(loyerDepart.GetValue(),
            NumberStyles.Float, CultureInfo.InvariantCulture, out float loyer))
        {
            statusText.text = "Loyer invalide";
            return;
        }

        string periode = NormalizePeriode(dateDepart.TrimestreValue.Trim());
        if (string.IsNullOrEmpty(periode))
        {
            statusText.text = "Trimestre de référence invalide";
            return;
        }

        string key = indiceDropdown.options[indiceDropdown.value].text;
        StartCoroutine(FetchIndiceReference(key, loyer, periode));
    }

    private IEnumerator FetchIndiceReference(string indiceKey, float loyer, string periodeDepart)
    {
        btnInitialiser.interactable = false;
        statusText.text = "Récupération de l'indice de référence...";

        var indice = Indices[indiceKey];
        string url = $"https://www.bdm.insee.fr/series/sdmx/data/SERIES_BDM/{indice.code}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                statusText.text = $"Erreur réseau : {req.error}";
                btnInitialiser.interactable = true;
                yield break;
            }

            _allObservations = ParseObservations(req.downloadHandler.text);
        }

        var obsRef = TrouveIndiceAvecFallback(periodeDepart);

        if (string.IsNullOrEmpty(obsRef.periode))
        {
            statusText.text = $"Indice introuvable pour {periodeDepart} et les 5 années précédentes";
            btnInitialiser.interactable = true;
            yield break;
        }

        // Loyer actuel = loyer de départ, aucune révision
        louerActuelText.text = $"{loyer:F2}";
        loyerAnnuelPrecedent.ApplyValue($"{loyer:F2}");
        indiceDepuisText.text = $"{obsRef.valeur:F2}  ({obsRef.periode})";
        indiceActuelText.text = "—";

        bool estFallback = NormalizePeriode(obsRef.periode) != NormalizePeriode(periodeDepart);
        statusText.text = estFallback
            ? $"Bail initialisé — indice {obsRef.periode} utilisé ({periodeDepart} non encore publié)"
            : $"Bail initialisé — indice {obsRef.periode} : {obsRef.valeur:F2}";


        // Bascule automatiquement en Bail en cours
        SetMode(RevisionMode.BailEnCours);

        btnInitialiser.interactable = true;
        CheckStateButtonLoyerRevision();
    }

    // ── Mode Bail en Cours ────────────────────────────────────────────────────

    private void OnCalculate()
    {
        if (!float.TryParse(loyerDepart.GetValue(),
            NumberStyles.Float, CultureInfo.InvariantCulture, out float loyer))
        {
            statusText.text = "Loyer invalide";
            return;
        }

        string periode = NormalizePeriode(dateDepart.TrimestreValue.Trim());
        if (string.IsNullOrEmpty(periode))
        {
            statusText.text = "Trimestre de référence invalide";
            return;
        }

        string key = indiceDropdown.options[indiceDropdown.value].text;
        StartCoroutine(FetchAndCalculate(key, loyer, periode));
    }

    private IEnumerator FetchAndCalculate(string indiceKey, float loyer, string periodeDepart)
    {
        calculateButton.interactable = false;
        statusText.text = "Récupération des données INSEE...";

        var indice = Indices[indiceKey];
        string url = $"https://www.bdm.insee.fr/series/sdmx/data/SERIES_BDM/{indice.code}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                statusText.text = $"Erreur réseau : {req.error}";
                calculateButton.interactable = true;
                CheckStateButtonLoyerRevision();
                yield break;
            }

            _allObservations = ParseObservations(req.downloadHandler.text);
        }

        if (_allObservations.Count == 0)
        {
            statusText.text = "Aucune donnée reçue";
            calculateButton.interactable = true;
            CheckStateButtonLoyerRevision();
            yield break;
        }

        var obsDepart = TrouveIndiceAvecFallback(periodeDepart);

        if (string.IsNullOrEmpty(obsDepart.periode))
        {
            statusText.text = $"Indice introuvable pour {periodeDepart}";
            calculateButton.interactable = true;
            CheckStateButtonLoyerRevision();
            yield break;
        }

        string trimestreRef = NormalizePeriode(periodeDepart).Split('-')[1];
        GetIndiceActuel(trimestreRef, out var obsActuel, out var obsPrecedent);

        if (string.IsNullOrEmpty(obsActuel.periode))
        {
            statusText.text = "Aucun trimestre correspondant trouvé";
            calculateButton.interactable = true;
            CheckStateButtonLoyerRevision();
            yield break;
        }

        float indiceD = obsDepart.valeur;
        float indiceA = obsActuel.valeur;
        float indiceP = obsPrecedent.valeur;

        float loyerRevise = loyer * (indiceA / indiceD);
        float loyerReviseAnneePrecedente = loyer * (indiceP / indiceD);

        indiceDepuisText.text = $"{indiceD:F2}  ({obsDepart.periode})";
        indiceActuelText.text = $"{indiceA:F2}  ({obsActuel.periode})";
        louerActuelText.text = $"{loyerRevise:F2}";
        loyerAnnuelPrecedent.ApplyValue($"{loyerReviseAnneePrecedente:F2}");

        if (dateDeRevision.SelectedDate != null && DateTime.Now < dateDeRevision.SelectedDate)
        {
            statusText.text = $"Loyer non révisé — révision prévue le {dateDeRevision.SelectedDate:dd/MM/yyyy}";
        }
        else
        {
            var date = dateDeRevision.SelectedDate;
            dateDeRevision.ApplyDate(new DateTime(date.Year + 1, date.Month, date.Day));
            statusText.text = $"{indice.label} — données au {obsActuel.periode}";
        }

        calculateButton.interactable = true;
        CheckStateButtonLoyerRevision();
    }

    // ── Utilitaires ───────────────────────────────────────────────────────────

    private (string periode, float valeur) TrouveIndiceAvecFallback(string periodeDepart)
    {
        var obs = _allObservations.Find(o =>
            NormalizePeriode(o.periode) == NormalizePeriode(periodeDepart));

        if (!string.IsNullOrEmpty(obs.periode)) return obs;

        string trimestrePart = NormalizePeriode(periodeDepart).Split('-')[1];
        int annee = int.Parse(NormalizePeriode(periodeDepart).Substring(0, 4));

        for (int recul = 1; recul <= 5; recul++)
        {
            string periodeRecul = $"{annee - recul}-{trimestrePart}";
            obs = _allObservations.Find(o => NormalizePeriode(o.periode) == periodeRecul);
            if (!string.IsNullOrEmpty(obs.periode))
            {
                Debug.Log($"[Révision] {periodeDepart} non publié → fallback {obs.periode}");
                return obs;
            }
        }

        return ("", 0f);
    }

    private void GetIndiceActuel(string trimestreRef,
        out (string periode, float valeur) actuel,
        out (string periode, float valeur) precedent)
    {
        var memeTrimestre = _allObservations.FindAll(o =>
        {
            string p = NormalizePeriode(o.periode);
            return !string.IsNullOrEmpty(p) && p.EndsWith(trimestreRef);
        });

        if (memeTrimestre.Count == 0)
        {
            actuel = ("", 0f); precedent = ("", 0f); return;
        }

        memeTrimestre.Sort((a, b) =>
        {
            int yA = int.Parse(NormalizePeriode(a.periode).Substring(0, 4));
            int yB = int.Parse(NormalizePeriode(b.periode).Substring(0, 4));
            return yA.CompareTo(yB);
        });

        if (memeTrimestre.Count == 1)
        {
            actuel = memeTrimestre[0]; precedent = memeTrimestre[0]; return;
        }

        if (dateDeRevision.SelectedDate != null && DateTime.Now < dateDeRevision.SelectedDate)
        {
            if (memeTrimestre.Count >= 3)
            {
                actuel = memeTrimestre[memeTrimestre.Count - 2];
                precedent = memeTrimestre[memeTrimestre.Count - 3];
            }
            else
            {
                actuel = memeTrimestre[memeTrimestre.Count - 1];
                precedent = memeTrimestre[memeTrimestre.Count - 1];
            }
            return;
        }

        actuel = memeTrimestre[memeTrimestre.Count - 1];
        precedent = memeTrimestre[memeTrimestre.Count - 2];
    }

    private void CheckStateButtonLoyerRevision()
    {
        if (_currentMode == RevisionMode.NouveauBail) return;

        bool revisionDue = dateDeRevision.SelectedDate == null ||
                           DateTime.Now >= dateDeRevision.SelectedDate ||
                           string.IsNullOrEmpty(indiceActuelText.text);

        calculateButton?.gameObject.SetActive(revisionDue);
    }

    // ── Parse XML SDMX ────────────────────────────────────────────────────────

    private List<(string periode, float valeur)> ParseObservations(string xml)
    {
        var result = new List<(string, float)>();
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(xml);
        XmlNodeList obsNodes = doc.SelectNodes("//*[local-name()='Obs']");
        foreach (XmlNode obs in obsNodes)
        {
            string periode = obs.Attributes["TIME_PERIOD"]?.Value ?? "";
            string valStr = obs.Attributes["OBS_VALUE"]?.Value ?? "";
            if (string.IsNullOrEmpty(periode) || string.IsNullOrEmpty(valStr)) continue;
            if (float.TryParse(valStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float valeur))
                result.Add((periode, valeur));
        }
        return result;
    }

    private string NormalizePeriode(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        input = input.Trim().ToUpper().Replace("Q", "T").Replace(" ", "");
        if (input.Length == 6 && input[4] == 'T')
            input = input.Insert(4, "-");
        return input;
    }
}