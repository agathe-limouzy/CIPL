using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RevisionPanel : MonoBehaviour
{
    public static RevisionPanel Instance { get; private set; }

    [Header("Paramètres du bail")]
    public TMP_Dropdown indiceDropdown;          // ILC / IRL / ILAT
    public TMP_Dropdown periodiciteDropdown;
    public TMP_InputField loyerDepart;
    public TrimestreInput trimestreDepart;       // trimestre de référence (indice de départ)
    public DateInputController dateDeRevision;

    [Header("Révision")]
    public TrimestreInput trimestreVoulu;        // indice de révision — choix manuel obligatoire

    [Header("Provisions")]
    public Toggle toggleProvisions;
    public TMP_InputField provisionValue;

    [Header("Infos affichées")]
    public TMP_Text txtIndiceDepart;
    public TMP_Text txtIndiceActuel;
    public TMP_Text txtLoyerCalcule;
    public TMP_Text statusText;

    [Header("Boutons")]
    public Button btnInitialiser;    // visible si bail jamais initialisé
    public Button btnReviser;        // visible sinon
    public Button btnFermer;

    private Locataire _loc;
    private Action _onSaved;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    // ── Ouverture ─────────────────────────────────────────────────────────────

    public void Open(Locataire loc, Action onSaved)
    {
        _loc = loc;
        _onSaved = onSaved;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // Dropdowns
        indiceDropdown.ClearOptions();
        indiceDropdown.AddOptions(new List<string>(Enum.GetNames(typeof(IndiceImmo))));
        indiceDropdown.value = (int)loc.indiceTypeImmo;

        periodiciteDropdown.ClearOptions();
        periodiciteDropdown.AddOptions(new List<string>(Enum.GetNames(typeof(Periodicite))));
        periodiciteDropdown.value = (int)loc.periodiciteLoyer;

        // Champs bail
        loyerDepart.text=(loc.loyerDepart.ToString());
        trimestreDepart.Init();
        if (!string.IsNullOrEmpty(loc.trimestreDeRevision))
            trimestreDepart.SetTrimestre(loc.trimestreDeRevision);
        trimestreDepart.CanModify();

        // Date de révision : toujours éditable, sauvegardée à chaque modification
        dateDeRevision.ApplyDate(loc.MoisDeRevision);
        dateDeRevision.ModifyDate();
        dateDeRevision.OnModify.RemoveAllListeners();
        dateDeRevision.OnModify.AddListener(OnDateRevisionModifiee);

        // Trimestre de révision : toujours visible, pré-rempli sur
        // le trimestre anniversaire de l'année en cours (modifiable)
        trimestreVoulu.Init();
        trimestreVoulu.CanModify();
        trimestreVoulu.gameObject.SetActive(true);
        if (!string.IsNullOrEmpty(loc.trimestreDeRevision))
        {
            string t = InseeIndiceService.Normalize(loc.trimestreDeRevision).Split('-')[1];
            trimestreVoulu.SetTrimestre($"{DateTime.Now.Year}-{t}");
        }

        // Provisions
        toggleProvisions.isOn = loc.provisionPourCharges;
        toggleProvisions.interactable = true;
        toggleProvisions.onValueChanged.RemoveAllListeners();
        toggleProvisions.onValueChanged.AddListener(on => provisionValue.gameObject.SetActive(on));
        provisionValue.gameObject.SetActive(loc.provisionPourCharges);
        provisionValue.text=(loc.provisionPourChargeValue.ToString());

        // Infos
        txtIndiceDepart.text = string.IsNullOrEmpty(loc.indiceImmoAuDepart) ? "—" : loc.indiceImmoAuDepart;
        txtIndiceActuel.text = string.IsNullOrEmpty(loc.indiceImmoActuel) ? "—" : loc.indiceImmoActuel;
        txtLoyerCalcule.text = $"{loc.loyerAnnuel:N2} €";
        statusText.text = "";

        // Boutons selon l'état du bail
        bool initialise = !string.IsNullOrEmpty(loc.indiceImmoAuDepart) && loc.indiceImmoAuDepart != "—";
        btnInitialiser.gameObject.SetActive(!initialise);
        btnReviser.gameObject.SetActive(initialise);

        btnInitialiser.onClick.RemoveAllListeners();
        btnInitialiser.onClick.AddListener(() => StartCoroutine(Initialiser()));
        btnReviser.onClick.RemoveAllListeners();
        btnReviser.onClick.AddListener(() => StartCoroutine(Reviser()));
        btnFermer.onClick.RemoveAllListeners();
        btnFermer.onClick.AddListener(() => gameObject.SetActive(false));
    }

    // ── Date de révision modifiée manuellement ────────────────────────────────

    private void OnDateRevisionModifiee()
    {
        if (_loc == null) return;
        _loc.MoisDeRevision = dateDeRevision.SelectedDate;
        statusText.text = $"Prochaine révision : {_loc.MoisDeRevision:dd/MM/yyyy}";
        _onSaved?.Invoke();   // sauvegarde + refresh badge/résumé
    }

    // ── Initialisation (nouveau bail) ─────────────────────────────────────────

    private IEnumerator Initialiser()
    {
        if (!TryParseLoyer(out float loyer)) yield break;

        btnInitialiser.interactable = false;
        statusText.text = "Récupération de l'indice de référence...";

        var type = (IndiceImmo)indiceDropdown.value;
        List<(string, float)> observations = null;
        string erreur = null;

        yield return InseeIndiceService.FetchObservations(type,
            obs => observations = obs, err => erreur = err);

        btnInitialiser.interactable = true;
        if (observations == null) { statusText.text = $"Erreur : {erreur}"; yield break; }

        // Indice de départ — fallback autorisé (trimestre pas encore publié)
        string periode = InseeIndiceService.Normalize(trimestreDepart.TrimestreValue);
        var obsRef = InseeIndiceService.TrouveAvecFallback(observations, periode);
        if (string.IsNullOrEmpty(obsRef.periode))
        { statusText.text = $"Indice introuvable pour {periode}"; yield break; }

        // Écriture dans le locataire
        _loc.loyerDepart = loyer;
        _loc.loyerAnnuel = loyer;
        _loc.loyerAnnuelPrecedent = 0f;   // nul à l'initialisation, aucune révision encore
        _loc.indiceImmoAuDepart = $"{obsRef.valeur:F2}  ({obsRef.periode})";
        _loc.indiceImmoActuel = "—";
        AppliquerChampsCommuns();

        txtIndiceDepart.text = _loc.indiceImmoAuDepart;
        txtIndiceActuel.text = "—";
        txtLoyerCalcule.text = $"{loyer:N2} €";

        bool estFallback = InseeIndiceService.Normalize(obsRef.periode) != periode;
        statusText.text = estFallback
            ? $"Bail initialisé — indice {obsRef.periode} utilisé ({periode} non encore publié)"
            : $"Bail initialisé — indice {obsRef.periode} : {obsRef.valeur:F2}";

        btnInitialiser.gameObject.SetActive(false);
        btnReviser.gameObject.SetActive(true);

        _onSaved?.Invoke();
    }

    // ── Révision (bail en cours) ──────────────────────────────────────────────

    private IEnumerator Reviser()
    {
        if (!TryParseLoyer(out float loyer)) yield break;

        btnReviser.interactable = false;
        statusText.text = "Récupération des données INSEE...";

        var type = (IndiceImmo)indiceDropdown.value;
        List<(string, float)> observations = null;
        string erreur = null;

        yield return InseeIndiceService.FetchObservations(type,
            obs => observations = obs, err => erreur = err);

        btnReviser.interactable = true;
        if (observations == null) { statusText.text = $"Erreur : {erreur}"; yield break; }

        // Indice de départ — fallback autorisé
        string periodeDepart = InseeIndiceService.Normalize(trimestreDepart.TrimestreValue);
        var obsDepart = InseeIndiceService.TrouveAvecFallback(observations, periodeDepart);
        if (string.IsNullOrEmpty(obsDepart.periode))
        { statusText.text = $"Indice de départ introuvable pour {periodeDepart}"; yield break; }

        // Indice de révision — choix manuel, correspondance EXACTE, pas de fallback
        string periodeVoulue = InseeIndiceService.Normalize(trimestreVoulu.TrimestreValue);
        var obsActuel = InseeIndiceService.TrouveExact(observations, periodeVoulue);
        if (string.IsNullOrEmpty(obsActuel.periode))
        {
            statusText.text = $"L'indice {periodeVoulue} n'est pas encore publié — " +
                              "choisissez un autre trimestre";
            yield break;
        }

        // Calcul
        float loyerRevise = loyer * (obsActuel.valeur / obsDepart.valeur);

        _loc.loyerDepart = loyer;
        _loc.loyerAnnuelPrecedent = _loc.loyerAnnuel;   // loyer AVANT révision
        _loc.loyerAnnuel = loyerRevise;
        _loc.indiceImmoAuDepart = $"{obsDepart.valeur:F2}  ({obsDepart.periode})";
        _loc.indiceImmoActuel = $"{obsActuel.valeur:F2}  ({obsActuel.periode})";
        _loc.dernierRevision = DateTime.Now.ToString("yyyy-MM-dd");

        // Prochaine révision = date saisie + 1 an — champ toujours éditable après
        var d = dateDeRevision.SelectedDate;
        _loc.MoisDeRevision = new DateTime(d.Year + 1, d.Month, d.Day);
        dateDeRevision.ApplyDate(_loc.MoisDeRevision);
        dateDeRevision.ModifyDate();

        AppliquerChampsCommuns(revision: true);

        txtIndiceDepart.text = _loc.indiceImmoAuDepart;
        txtIndiceActuel.text = _loc.indiceImmoActuel;
        txtLoyerCalcule.text = $"{loyerRevise:N2} €";
        statusText.text = $"{InseeIndiceService.GetLabel(type)} — loyer révisé : {loyerRevise:N2} € " +
                          $"(prochaine révision {_loc.MoisDeRevision:dd/MM/yyyy})";

        _onSaved?.Invoke();
    }

    // ── Communs ───────────────────────────────────────────────────────────────

    private void AppliquerChampsCommuns(bool revision = false)
    {
        _loc.indiceTypeImmo = (IndiceImmo)indiceDropdown.value;
        _loc.periodiciteLoyer = (Periodicite)periodiciteDropdown.value;
        _loc.trimestreDeRevision = trimestreDepart.TrimestreValue;
        if (!revision)
            _loc.MoisDeRevision = dateDeRevision.SelectedDate;

        _loc.provisionPourCharges = toggleProvisions.isOn;
        float.TryParse(provisionValue.text?.Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out float prov);
        _loc.provisionPourChargeValue = toggleProvisions.isOn ? prov : 0f;
    }

    private bool TryParseLoyer(out float loyer)
    {
        bool ok = float.TryParse(loyerDepart.text?.Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out loyer);
        if (!ok) statusText.text = "Loyer de départ invalide";
        return ok;
    }
}