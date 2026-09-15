using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    [Header("Redesign — type d'indice (chips)")]
    public Button chipILC, chipIRL, chipILAT;   // pilotent indiceDropdown (0/1/2)

    [Header("Redesign — mode (toggle)")]
    public Button btnModeInit, btnModeReviser;
    public GameObject sectionRevision;          // bloc « cette révision » (visible en mode révision)

    [Header("Redesign — variation")]
    public TMP_Text txtVariation;               // évolution % précédent → nouvel indice
    public TMP_Text txtLoyerPrecedent;          // loyer de l'année précédente

    private Locataire _loc;
    private Action _onSaved;

    // ── Découpage en deux volets (deux « menus » ouverts par deux boutons) ──────
    //  • Revision  : indice, loyer de départ, trimestres, dates, calcul + résultat.
    //  • Modalites : gestion du loyer / facturation — périodicité, jour de demande,
    //                mois facturés, provisions, date de régularisation.
    public enum Volet { Revision, Modalites }
    Volet _volet = Volet.Revision;

    // ── Champs facturation ajoutés par code (jour de demande, mois, régularisation) ──
    TMP_InputField _jourDemande;
    DateInputController _dateRegulCtrl;
    GameObject _jourLabelGO, _moisLabelGO, _moisWrapGO, _regulBlockGO, _repriseBlockGO;
    UIDropdown _repriseDD;                      // reprise : dernière période déjà facturée
    Button _btnEnregistrer;                    // volet Modalités (clone de Réviser)
    readonly Button[] _moisChips = new Button[12];
    readonly bool[] _moisState = new bool[12];
    bool _extraBuilt;

    // Avertissement inline placé SOUS le « Trimestre de révision » (au lieu du bas
    // de la modale) + désactivation du bouton « Réviser » quand le trimestre est mauvais.
    TMP_Text _trimWarn;

    // Blocs de la scène résolus une fois (pour l'affichage par volet).
    Transform _body;
    GameObject _periodiciteGO, _chipsIndiceGO, _loyerDepartGO, _trimRefGO,
               _dateRevGO, _modeToggleGO, _provBlockGO, _informationGO;
    static readonly string[] MoisCourts =
        { "Jan", "Fév", "Mar", "Avr", "Mai", "Juin", "Juil", "Août", "Sep", "Oct", "Nov", "Déc" };

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    // ── Ouverture ─────────────────────────────────────────────────────────────

    public void Open(Locataire loc, Action onSaved) => Open(loc, onSaved, Volet.Revision);

    public void Open(Locataire loc, Action onSaved, Volet volet)
    {
        _loc = loc;
        _onSaved = onSaved;
        _volet = volet;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ApplyTheme(LoyerSummaryUI.EstRevisionDue(loc));

        // Dropdowns
        indiceDropdown.ClearOptions();
        indiceDropdown.AddOptions(new List<string>(Enum.GetNames(typeof(IndiceImmo))));
        indiceDropdown.value = (int)loc.indiceTypeImmo;

        periodiciteDropdown.ClearOptions();
        periodiciteDropdown.AddOptions(new List<string>(Enum.GetNames(typeof(Periodicite))));
        periodiciteDropdown.value = (int)loc.periodiciteLoyer;

        // Champs facturation (jour de demande, mois facturés, date de régularisation)
        EnsureExtraBuilt();
        _jourDemande.text = loc.jourDemandeLoyer > 0 ? loc.jourDemandeLoyer.ToString() : "";
        if (System.DateTime.TryParse(loc.dateRegularisationChargeISO, out var drDate))
            _dateRegulCtrl.ApplyDate(drDate);
        else { _dateRegulCtrl.dayInput.text = ""; _dateRegulCtrl.monthInput.text = ""; _dateRegulCtrl.yearInput.text = ""; }
        _dateRegulCtrl.ModifyDate();
        for (int i = 0; i < 12; i++) _moisState[i] = false;
        if (loc.moisFacturationLoyer != null)
            foreach (int m in loc.moisFacturationLoyer)
                if (m >= 1 && m <= 12) _moisState[m - 1] = true;
        for (int i = 0; i < 12; i++) RefreshChip(i);
        RefreshRepriseOptions();

        periodiciteDropdown.onValueChanged.RemoveAllListeners();
        periodiciteDropdown.onValueChanged.AddListener(_ => OnPeriodiciteChanged());
        RefreshMoisVisibility();

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
            // Année précédente par défaut : le trimestre de l'année en cours
            // n'est en général pas encore publié par l'INSEE (recherche exacte).
            trimestreVoulu.SetTrimestre($"{DateTime.Now.Year - 1}-{t}");
        }
        // Avertissement inline + verrouillage du bouton si le trimestre est mauvais.
        EnsureTrimWarn();
        trimestreVoulu.OnTrimestreChanged -= OnTrimVouluChanged;
        trimestreVoulu.OnTrimestreChanged += OnTrimVouluChanged;
        ValiderTrimestreVoulu();

        // Provisions
        toggleProvisions.isOn = loc.provisionPourCharges;
        toggleProvisions.interactable = true;
        toggleProvisions.onValueChanged.RemoveAllListeners();
        // Masque tout le conteneur (champ + « € ») et non le seul champ,
        // sinon le « € » reste visible quand la provision est décochée.
        var provContainer = provisionValue.transform.parent != null
            ? provisionValue.transform.parent.gameObject : provisionValue.gameObject;
        toggleProvisions.onValueChanged.AddListener(on => provContainer.SetActive(on));
        provContainer.SetActive(loc.provisionPourCharges);
        provisionValue.text=(loc.provisionPourChargeValue.ToString());
        toggleProvisions.onValueChanged.AddListener(_ => RefreshRegulVisibility());
        RefreshRegulVisibility();

        // Infos
        txtIndiceDepart.text = string.IsNullOrEmpty(loc.indiceImmoAuDepart) ? "—" : loc.indiceImmoAuDepart;
        txtIndiceActuel.text = string.IsNullOrEmpty(loc.indiceImmoActuel) ? "—" : loc.indiceImmoActuel;
        txtLoyerCalcule.text = $"{loc.loyerAnnuel:N2} €";
        statusText.text = "";
        // Le statut peut être long (« … loyer révisé : X € (prochaine révision …) ») :
        // sans retour à la ligne il débordait du cadre → on force le wrap.
        statusText.enableWordWrapping = true;
        statusText.overflowMode = TextOverflowModes.Overflow;

        // Type d'indice — chips
        WireChips();
        SetIndice((int)loc.indiceTypeImmo);
        if (txtVariation != null) txtVariation.text = "—";
        if (txtLoyerPrecedent != null)
            txtLoyerPrecedent.text = loc.loyerAnnuelPrecedent > 0f
                ? $"{loc.loyerAnnuelPrecedent:N2} €" : "—";

        // Actions
        btnInitialiser.onClick.RemoveAllListeners();
        btnInitialiser.onClick.AddListener(() => StartCoroutine(Initialiser()));
        btnReviser.onClick.RemoveAllListeners();
        btnReviser.onClick.AddListener(() => StartCoroutine(Reviser()));
        btnFermer.onClick.RemoveAllListeners();
        btnFermer.onClick.AddListener(() => gameObject.SetActive(false));

        // Mode (toggle) — auto-détecté selon l'état du bail, basculable
        if (btnModeInit != null) { btnModeInit.onClick.RemoveAllListeners(); btnModeInit.onClick.AddListener(() => SetMode(true)); }
        if (btnModeReviser != null) { btnModeReviser.onClick.RemoveAllListeners(); btnModeReviser.onClick.AddListener(() => SetMode(false)); }
        bool initialise = !string.IsNullOrEmpty(loc.indiceImmoAuDepart) && loc.indiceImmoAuDepart != "—";
        SetMode(!initialise);

        // Applique le volet demandé (Révision par défaut / Modalités facturation).
        ApplyVolet(volet);
    }

    // ── Thème dynamique : couleur de la modale = état de la révision ──────────
    // Due -> terracotta (comme le bouton Réviser en alerte), sinon -> prune.
    private static Color s_accent      = Hex("#8E3B5A");
    private static Color s_accentDark  = Hex("#5E2438");
    private static Color s_accentLight = Hex("#F6E5EB");

    private void ApplyTheme(bool due)
    {
        s_accent      = Hex(due ? "#D85A30" : "#8E3B5A");
        s_accentDark  = Hex(due ? "#712B13" : "#5E2438");
        s_accentLight = Hex(due ? "#FAECE7" : "#F6E5EB");

        var header = transform.Find("Content/titre")?.GetComponent<Image>();
        if (header != null) header.color = s_accent;
        ColorButton(btnInitialiser, s_accent);
        ColorButton(btnReviser, s_accent);
        ColorBox("Information");
        ColorBox("SectionRevision");
    }

    private void ColorButton(Button b, Color bg)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>(); if (img != null) img.color = bg;
        var t = b.GetComponentInChildren<TMP_Text>(true); if (t != null) t.color = Color.white;
    }

    private void ColorBox(string name)
    {
        var box = GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.Trim() == name);
        if (box != null) { var img = box.GetComponent<Image>(); if (img != null) img.color = s_accentLight; }
    }

    // ── Redesign : chips indice + mode toggle ─────────────────────────────────

    private void WireChips()
    {
        if (chipILC != null)  { chipILC.onClick.RemoveAllListeners();  chipILC.onClick.AddListener(() => SetIndice(0)); }
        if (chipIRL != null)  { chipIRL.onClick.RemoveAllListeners();  chipIRL.onClick.AddListener(() => SetIndice(1)); }
        if (chipILAT != null) { chipILAT.onClick.RemoveAllListeners(); chipILAT.onClick.AddListener(() => SetIndice(2)); }
    }

    private void SetIndice(int i)
    {
        if (indiceDropdown != null) indiceDropdown.value = i;
        StyleChip(chipILC,  i == 0);
        StyleChip(chipIRL,  i == 1);
        StyleChip(chipILAT, i == 2);
    }

    private static void StyleChip(Button b, bool active)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null) img.color = active ? s_accentLight : Hex("#FCFBF8");
        var txt = b.GetComponentInChildren<TMP_Text>(true);
        if (txt != null) txt.color = active ? s_accentDark : Hex("#888780");
    }

    private void SetMode(bool init)
    {
        if (btnInitialiser != null) btnInitialiser.gameObject.SetActive(init);
        if (btnReviser != null) btnReviser.gameObject.SetActive(!init);
        if (sectionRevision != null) sectionRevision.SetActive(!init);
        StyleToggleBtn(btnModeInit, init);
        StyleToggleBtn(btnModeReviser, !init);
    }

    private static void StyleToggleBtn(Button b, bool active)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null) img.color = active ? s_accent : new Color(0, 0, 0, 0);
        var txt = b.GetComponentInChildren<TMP_Text>(true);
        if (txt != null) txt.color = active ? Color.white : Hex("#5F5E5A");
    }

    private static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString(h, out var c);
        return c;
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

        // Historique : nouvelle référence d'indexation (prend effet au début du bail)
        LoyerHistoryService.EnregistrerReference(_loc,
            DateTime.TryParse(_loc.dateDebutBailISO, out var dbInit) ? dbInit : DateTime.Now,
            (IndiceImmo)indiceDropdown.value, obsRef.periode, obsRef.valeur, loyer);

        txtIndiceDepart.text = _loc.indiceImmoAuDepart;
        txtIndiceActuel.text = "—";
        txtLoyerCalcule.text = $"{loyer:N2} €";
        if (txtVariation != null) txtVariation.text = "—";
        if (txtLoyerPrecedent != null) txtLoyerPrecedent.text = "—";

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
            // Le trimestre voulu n'est pas publié → on EFFACE le résultat précédent
            // pour ne pas laisser un ancien « nouvel indice » trompeur à l'écran,
            // on affiche l'avertissement SOUS le champ trimestre (pas en bas) et on
            // VERROUILLE le bouton « Réviser ».
            if (txtIndiceActuel != null) txtIndiceActuel.text = "—";
            if (txtLoyerCalcule != null) txtLoyerCalcule.text = "—";
            if (txtVariation != null) txtVariation.text = "—";
            statusText.text = "";
            var dernier = InseeIndiceService.DernierPublie(observations);
            AfficheTrimWarn(string.IsNullOrEmpty(dernier.periode)
                ? $"L'indice {periodeVoulue} n'est pas encore publié — choisissez un autre trimestre."
                : $"L'indice {periodeVoulue} n'est pas encore publié — dernier disponible : {dernier.periode}. Choisissez un trimestre publié.");
            if (btnReviser != null) btnReviser.interactable = false;
            yield break;
        }
        MasqueTrimWarn();

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

        // Historique : n'enregistre un nouveau segment que si la référence
        // (trimestre de départ / indice / loyer de base) a réellement changé.
        LoyerHistoryService.EnregistrerReference(_loc, d,
            (IndiceImmo)indiceDropdown.value, obsDepart.periode, obsDepart.valeur, loyer);

        txtIndiceDepart.text = _loc.indiceImmoAuDepart;
        txtIndiceActuel.text = _loc.indiceImmoActuel;
        txtLoyerCalcule.text = $"{loyerRevise:N2} €";
        if (txtVariation != null)
        {
            float variation = obsDepart.valeur != 0f
                ? (obsActuel.valeur / obsDepart.valeur - 1f) * 100f : 0f;
            txtVariation.text = $"{(variation >= 0 ? "+" : "")}{variation:F1} %";
        }
        if (txtLoyerPrecedent != null)
            txtLoyerPrecedent.text = $"{_loc.loyerAnnuelPrecedent:N2} €";
        statusText.text = $"{InseeIndiceService.GetLabel(type)} — loyer révisé : {loyerRevise:N2} € " +
                          $"(prochaine révision {_loc.MoisDeRevision:dd/MM/yyyy})";

        _onSaved?.Invoke();
    }

    // ── Validation du trimestre de révision ───────────────────────────────────

    // Construit une fois le label d'avertissement SOUS le « Trimestre de révision ».
    private void EnsureTrimWarn()
    {
        if (_trimWarn != null || trimestreVoulu == null) return;
        var parent = trimestreVoulu.transform.parent;   // SectionRevision (VLG vertical)
        if (parent == null) return;
        var go = new GameObject("TrimWarning", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TMPro.TextMeshProUGUI>();
        var refT = trimestreVoulu.GetComponentInChildren<TMP_Text>(true);
        if (refT != null) t.font = refT.font;
        t.fontSize = 13;
        t.color = Hex("#A32D2D");                        // rouge lisible sur fond clair
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        t.alignment = TextAlignmentOptions.TopLeft;
        go.AddComponent<LayoutElement>().minHeight = 0;
        go.transform.SetAsLastSibling();
        go.SetActive(false);
        _trimWarn = t;
    }

    private void AfficheTrimWarn(string msg)
    {
        EnsureTrimWarn();
        if (_trimWarn == null) return;
        _trimWarn.text = msg;
        _trimWarn.gameObject.SetActive(true);
    }

    private void MasqueTrimWarn()
    {
        if (_trimWarn != null) _trimWarn.gameObject.SetActive(false);
    }

    // Un trimestre pas encore COMMENCÉ (futur) ne peut jamais être publié → on
    // bloque immédiatement, sans même appeler l'INSEE.
    private static bool TrimestreDejaCommence(string periode)
    {
        string n = InseeIndiceService.Normalize(periode);   // "2026-T4"
        if (n.Length < 7 || n[5] != 'T') return true;        // illisible → ne bloque pas
        if (!int.TryParse(n.Substring(0, 4), out int y)) return true;
        if (!int.TryParse(n.Substring(6, 1), out int q)) return true;
        var debut = new DateTime(y, (q - 1) * 3 + 1, 1);
        return debut <= DateTime.Now;
    }

    // Réévalue à chaque changement : bouton « Réviser » verrouillé + avertissement
    // inline si le trimestre voulu est dans le futur.
    private void ValiderTrimestreVoulu()
    {
        EnsureTrimWarn();
        string p = trimestreVoulu != null ? trimestreVoulu.TrimestreValue : "";
        if (!TrimestreDejaCommence(p))
        {
            AfficheTrimWarn($"Le trimestre {InseeIndiceService.Normalize(p)} n'a pas encore commencé — "
                            + "choisissez un trimestre déjà publié.");
            if (btnReviser != null) btnReviser.interactable = false;
        }
        else
        {
            MasqueTrimWarn();
            if (btnReviser != null) btnReviser.interactable = true;
        }
    }

    private void OnTrimVouluChanged(string _) => ValiderTrimestreVoulu();

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

        // Facturation
        if (_jourDemande != null)
        {
            int.TryParse(_jourDemande.text, out int jd);
            _loc.jourDemandeLoyer = Mathf.Clamp(jd, 0, 31);
        }
        if (_dateRegulCtrl != null)
        {
            if (int.TryParse(_dateRegulCtrl.dayInput.text, out int rdd)
                && int.TryParse(_dateRegulCtrl.monthInput.text, out int rmm)
                && int.TryParse(_dateRegulCtrl.yearInput.text, out int ryy))
            {
                try { _loc.dateRegularisationChargeISO = new DateTime(ryy, rmm, rdd).ToString("yyyy-MM-dd"); }
                catch { _loc.dateRegularisationChargeISO = ""; }
            }
            else _loc.dateRegularisationChargeISO = "";
        }
        _loc.moisFacturationLoyer = new List<int>();
        for (int i = 0; i < 12; i++) if (_moisState[i]) _loc.moisFacturationLoyer.Add(i + 1);
    }

    // ── Facturation : champs injectés (jour, mois, régularisation) ────────────

    void EnsureExtraBuilt()
    {
        if (_extraBuilt) return;
        _extraBuilt = true;

        var periodBlock = periodiciteDropdown.transform.parent;
        var content = periodBlock.parent;
        int idx = periodBlock.GetSiblingIndex() + 1;

        var l1 = UIFactory.Text(content, "Loyer demandé le … (jour du mois)", 15, UITheme.TexteSecondaire);
        l1.transform.SetSiblingIndex(idx++);
        _jourLabelGO = l1.gameObject;
        _jourDemande = UIFactory.Input(content, "1");
        _jourDemande.contentType = TMP_InputField.ContentType.IntegerNumber;
        _jourDemande.transform.SetSiblingIndex(idx++);

        var l2 = UIFactory.Text(content, "Mois facturés (si trimestriel / bi-annuel)", 15, UITheme.TexteSecondaire);
        l2.transform.SetSiblingIndex(idx++);
        _moisLabelGO = l2.gameObject;
        var wrap = BuildMoisChips(content);
        wrap.transform.SetSiblingIndex(idx++);
        _moisWrapGO = wrap;

        // Date de régularisation : APRÈS le bloc Provisions
        Transform provBlock = toggleProvisions.transform;
        while (provBlock != null && provBlock.parent != content) provBlock = provBlock.parent;
        int ridx = (provBlock != null ? provBlock.GetSiblingIndex() : content.childCount - 1) + 1;

        // Clone du bloc « Date de révision » → même visuel (cellules JJ/MM/AAAA).
        var regulGO = Instantiate(dateDeRevision.gameObject, content);
        regulGO.name = "DateRegularisation";
        regulGO.transform.SetSiblingIndex(ridx++);
        _regulBlockGO = regulGO;
        _dateRegulCtrl = regulGO.GetComponent<DateInputController>();
        var titre = regulGO.transform.Find("Titre")?.GetComponent<TMP_Text>();
        if (titre != null) titre.text = "Date de régularisation de charge";

        // Reprise de facturation (bail repris) : sélecteur « dernière période facturée ».
        var rv = UIFactory.VBox(content, 4, 0, 0, 0, 0, "RepriseBlock");
        _repriseBlockGO = rv.gameObject;
        UIFactory.Text(rv.transform,
            "Bail repris — dernière période déjà facturée (laisser « Aucune » si nouveau bail)",
            15, UITheme.TexteSecondaire);
        _repriseDD = UIDropdown.Create(rv.transform,
            new List<string> { "Aucune (nouveau bail)" }, new List<string> { "" }, 0, _ => { });
        rv.transform.SetSiblingIndex(regulGO.transform.GetSiblingIndex() + 1);

        // Bouton « Enregistrer » (volet Modalités) — clone du bouton Réviser, même
        // visuel, glissé dans la rangée de boutons juste après lui.
        if (btnReviser != null)
        {
            var eg = Instantiate(btnReviser.gameObject, btnReviser.transform.parent);
            eg.name = "Enregistrer";
            eg.transform.SetSiblingIndex(btnReviser.transform.GetSiblingIndex() + 1);
            _btnEnregistrer = eg.GetComponent<Button>();
            var t = _btnEnregistrer.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = "Enregistrer";
            _btnEnregistrer.onClick.RemoveAllListeners();
            _btnEnregistrer.onClick.AddListener(SaveModalites);
            _btnEnregistrer.gameObject.SetActive(false);
        }
    }

    // Mois visibles seulement dans le volet Modalités, hors mensuel ;
    // régularisation visible seulement dans Modalités et en cas de provision.
    void RefreshMoisVisibility()
    {
        bool show = _volet == Volet.Modalites && periodiciteDropdown.value != (int)Periodicite.mensuel;
        if (_moisLabelGO != null) _moisLabelGO.SetActive(show);
        if (_moisWrapGO != null) _moisWrapGO.SetActive(show);
    }

    void RefreshRegulVisibility()
    {
        bool show = _volet == Volet.Modalites && toggleProvisions.isOn;
        if (_regulBlockGO != null) _regulBlockGO.SetActive(show);
    }

    // Options du sélecteur de reprise : « Aucune » + périodes récentes (4 ans),
    // les plus récentes en tête, selon la périodicité du loyer.
    void RefreshRepriseOptions()
    {
        if (_repriseDD == null || _loc == null) return;
        var labels = new List<string> { "Aucune (nouveau bail)" };
        var ids = new List<string> { "" };
        var p = _loc.periodiciteLoyer;
        int n = LoyerSummaryUI.NbPeriodes(p);
        int cur = DateTime.Now.Year;
        for (int y = cur; y >= cur - 3; y--)
            for (int per = n; per >= 1; per--)
            {
                labels.Add(FacturationSuivi.LibellePeriode(p, per, y));
                ids.Add($"{y}-P{per}");
            }
        string sel = "";
        if (DateTime.TryParse(_loc.repriseFacturationISO, out var rd))
            sel = $"{rd.Year}-P{FacturationSuivi.PeriodeIndex(p, rd.Month)}";
        _repriseDD.SetOptions(labels, ids, sel);
    }

    // ── Affichage par volet (Révision / Modalités facturation) ────────────────

    // Résout une fois les blocs de la scène à montrer/masquer selon le volet.
    void ResolveBlocks()
    {
        if (_body != null) return;
        _body = periodiciteDropdown.transform.parent.parent;
        _periodiciteGO = periodiciteDropdown.transform.parent.gameObject;
        _chipsIndiceGO = chipILC != null ? DirectChild(chipILC.transform) : null;
        _loyerDepartGO = DirectChild(loyerDepart.transform);
        _trimRefGO     = DirectChild(trimestreDepart.transform);
        _dateRevGO     = DirectChild(dateDeRevision.transform);
        _modeToggleGO  = btnModeInit != null ? DirectChild(btnModeInit.transform) : null;
        _provBlockGO   = DirectChild(toggleProvisions.transform);
        _informationGO = statusText != null ? DirectChild(statusText.transform) : null;
    }

    // Remonte jusqu'à l'enfant direct de _body qui contient `t`.
    GameObject DirectChild(Transform t)
    {
        var x = t;
        while (x != null && x.parent != _body) x = x.parent;
        return x != null ? x.gameObject : null;
    }

    static void SetGO(GameObject g, bool on) { if (g != null) g.SetActive(on); }

    void ApplyVolet(Volet volet)
    {
        _volet = volet;
        ResolveBlocks();
        bool mod = volet == Volet.Modalites;

        // Titre de la modale
        var titleT = transform.Find("Content/titre/Revision")?.GetComponent<TMP_Text>();
        if (titleT != null) titleT.text = mod ? "Gestion du loyer" : "Révision du loyer";

        // Blocs « Révision » (masqués en Modalités)
        SetGO(_chipsIndiceGO, !mod);
        SetGO(_loyerDepartGO, !mod);
        SetGO(_trimRefGO, !mod);
        SetGO(_dateRevGO, !mod);
        SetGO(_modeToggleGO, !mod);
        SetGO(_informationGO, !mod);

        // Blocs « Modalités / facturation » (masqués en Révision)
        SetGO(_periodiciteGO, mod);
        SetGO(_jourLabelGO, mod);
        if (_jourDemande != null) SetGO(_jourDemande.gameObject, mod);
        SetGO(_provBlockGO, mod);
        SetGO(_repriseBlockGO, mod);
        RefreshMoisVisibility();     // mois : Modalités + hors mensuel
        RefreshRegulVisibility();    // régularisation : Modalités + provision

        // Boutons de la rangée basse
        if (mod)
        {
            if (btnInitialiser != null) btnInitialiser.gameObject.SetActive(false);
            if (btnReviser != null) btnReviser.gameObject.SetActive(false);
            if (sectionRevision != null) sectionRevision.SetActive(false);
            if (_btnEnregistrer != null) _btnEnregistrer.gameObject.SetActive(true);
        }
        else if (_btnEnregistrer != null)
        {
            // Init / Réviser / sectionRevision sont pilotés par SetMode (appelé avant).
            _btnEnregistrer.gameObject.SetActive(false);
        }

        ApplyThemeVolet(mod);
    }

    // Habillage : Révision garde le thème (dû → terracotta / sinon prune) ;
    // Modalités passe en ambre (famille « argent » du loyer).
    void ApplyThemeVolet(bool mod)
    {
        if (!mod) { ApplyTheme(LoyerSummaryUI.EstRevisionDue(_loc)); return; }
        var header = transform.Find("Content/titre")?.GetComponent<Image>();
        if (header != null) header.color = Hex("#A9741C");
        ColorButton(_btnEnregistrer, Hex("#A9741C"));
    }

    // ── Enregistrement du volet Modalités (pas de calcul, juste la sauvegarde) ──
    void SaveModalites()
    {
        if (_loc == null) return;

        _loc.periodiciteLoyer = (Periodicite)periodiciteDropdown.value;

        _loc.provisionPourCharges = toggleProvisions.isOn;
        float.TryParse(provisionValue.text?.Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out float prov);
        _loc.provisionPourChargeValue = toggleProvisions.isOn ? prov : 0f;

        if (_jourDemande != null)
        {
            int.TryParse(_jourDemande.text, out int jd);
            _loc.jourDemandeLoyer = Mathf.Clamp(jd, 0, 31);
        }

        if (_dateRegulCtrl != null)
        {
            if (int.TryParse(_dateRegulCtrl.dayInput.text, out int rdd)
                && int.TryParse(_dateRegulCtrl.monthInput.text, out int rmm)
                && int.TryParse(_dateRegulCtrl.yearInput.text, out int ryy))
            {
                try { _loc.dateRegularisationChargeISO = new DateTime(ryy, rmm, rdd).ToString("yyyy-MM-dd"); }
                catch { _loc.dateRegularisationChargeISO = ""; }
            }
            else _loc.dateRegularisationChargeISO = "";
        }

        _loc.moisFacturationLoyer = new List<int>();
        for (int i = 0; i < 12; i++) if (_moisState[i]) _loc.moisFacturationLoyer.Add(i + 1);

        // Reprise de facturation (dernière période déjà facturée → échéance).
        if (_repriseDD != null)
        {
            string id = _repriseDD.SelectedId;
            if (string.IsNullOrEmpty(id)) _loc.repriseFacturationISO = "";
            else
            {
                var parts = id.Split('-');   // « 2026-P2 »
                if (parts.Length == 2 && int.TryParse(parts[0], out int y)
                    && int.TryParse(parts[1].TrimStart('P', 'p'), out int per))
                    _loc.repriseFacturationISO = FacturationSuivi
                        .EcheancePeriode(_loc.periodiciteLoyer, per, y, _loc.jourDemandeLoyer)
                        .ToString("yyyy-MM-dd");
            }
        }

        _onSaved?.Invoke();
        UndoToast.Instance?.ShowInfo("Modalités du loyer enregistrées");
        gameObject.SetActive(false);
    }

    // Nombre max de mois sélectionnables selon la périodicité.
    int MaxMois()
    {
        switch (periodiciteDropdown.value)
        {
            case 1: return 4;   // trimestriel (une échéance par trimestre)
            case 2: return 2;   // bi-annuel
            case 3: return 1;   // annuel
            default: return 12; // mensuel
        }
    }

    // Au changement de périodicité : rogne la sélection au max + maj visibilité.
    void OnPeriodiciteChanged()
    {
        int max = MaxMois(), c = 0;
        for (int i = 0; i < 12; i++)
            if (_moisState[i]) { c++; if (c > max) _moisState[i] = false; }
        for (int i = 0; i < 12; i++) RefreshChip(i);
        RefreshMoisVisibility();
        RefreshRepriseOptions();
    }

    GameObject BuildMoisChips(Transform parent)
    {
        var wrapGO = UIFactory.Rect("MoisWrap", parent);
        var grid = wrapGO.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(66, 28);
        grid.spacing = new Vector2(5, 5);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        var csf = wrapGO.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UIFactory.LE(wrapGO.gameObject, minH: 60);

        for (int i = 0; i < 12; i++)
        {
            int idx = i;
            var b = UIFactory.Button(wrapGO, MoisCourts[i], UITheme.Carte, UITheme.TextePrincipal, 28, 15, false);
            UIFactory.Border(b.gameObject);
            b.onClick.AddListener(() =>
            {
                if (!_moisState[idx])
                {
                    int c = 0; for (int k = 0; k < 12; k++) if (_moisState[k]) c++;
                    if (c >= MaxMois()) return;   // limite atteinte pour cette périodicité
                }
                _moisState[idx] = !_moisState[idx]; RefreshChip(idx);
            });
            _moisChips[idx] = b;
        }
        return wrapGO.gameObject;
    }

    void RefreshChip(int i)
    {
        if (_moisChips[i] == null) return;
        var img = _moisChips[i].GetComponent<Image>();
        var txt = _moisChips[i].GetComponentInChildren<TMP_Text>();
        if (img != null) img.color = _moisState[i] ? UITheme.Primaire : UITheme.Carte;
        if (txt != null) txt.color = _moisState[i] ? Color.white : UITheme.TextePrincipal;
    }

    private bool TryParseLoyer(out float loyer)
    {
        bool ok = float.TryParse(loyerDepart.text?.Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out loyer);
        if (!ok) statusText.text = "Loyer de départ invalide";
        return ok;
    }
}