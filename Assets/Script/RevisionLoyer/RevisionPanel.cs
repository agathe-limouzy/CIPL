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

    // ── Deux volets (deux « menus » ouverts par deux boutons de la fiche) ───────
    //  • Initialisation : loyer de départ, périodicité, jour, mois, provisions et
    //    listes, type de révision (indice / paliers / aucune), franchise, reprise.
    //    Bouton Initialiser / Réinitialiser / Modifier selon l'état (BoutonInit).
    //  • Indice : initialisation de l'indice (type, trimestre de référence, date de
    //    révision), puis révision (trimestre voulu, calcul + résultat).
    // Les paliers ont leur propre écran (PaliersPanel).
    public enum Volet { Initialisation, Indice }
    Volet _volet = Volet.Indice;

    // Écran « Initialiser » : type de révision, avenant, franchise, départ (construits en code).
    Toggle _revisionToggle, _franchiseToggle, _avenantToggle;
    UIDropdown _typeRevDD;
    DateInputController _franchiseDate, _avenantDate;
    GameObject _revisionBlockGO, _typeRevRowGO, _franchiseBlockGO, _avenantBlockGO;
    // Loyer selon le CA : % et bornes (écran Initialiser), CA de l'année (révision).
    readonly List<GameObject> _caChamps = new List<GameObject>();   // min, max, % (clones de « Loyer de départ »)
    GameObject _caRevisionGO, _caResultGO, _compareStripGO, _loyerPrecedentGO;
    TMP_InputField _caPct, _caMin, _caMax, _caMontant, _caPrecedent, _trimPrecedent;
    TMP_Text _caTitre, _caTitrePrecedent, _titreLoyerCalcule, _lPct, _rPct, _rMin, _rMax, _rRegle, _rAncien, _rProchaine, _rIndice;
    int _caAnnee;   // année du CA affichée dans le champ (année de révision - 1)
    // Largeur de la fenêtre : d'origine pour Initialiser, élargie pour la révision.
    const float LargeurFormulaire = 560f, LargeurRevision = 900f;
    LayoutElement _leFenetre, _leTitre;

    // ── Champs facturation ajoutés par code (jour de demande, mois, régularisation) ──
    TMP_InputField _jourDemande;
    DateInputController _dateRegulCtrl;
    GameObject _jourLabelGO, _moisLabelGO, _moisWrapGO, _regulBlockGO, _repriseBlockGO;
    UIDropdown _repriseDD;                      // reprise : dernière période déjà facturée
    Button _btnEnregistrer;                    // volet Modalités (clone de Réviser)
    readonly Button[] _moisChips = new Button[12];
    readonly bool[] _moisState = new bool[12];
    bool _extraBuilt;

    // Listes de charges spécifiques (Réglages) : une date de régularisation et une
    // provision par liste. La générale garde les champs du prefab au-dessus.
    GameObject _listesBlockGO;
    Transform _lignesListes;                    // lignes date + provision (si provision)
    // Une case par liste (la générale = id "") : celles qui concernent ce locataire.
    readonly List<(string id, Toggle t)> _choixListes = new List<(string, Toggle)>();
    readonly List<(string id, DateInputController date, TMP_InputField prov)> _listesChamps
        = new List<(string, DateInputController, TMP_InputField)>();

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

    public void Open(Locataire loc, Action onSaved) => Open(loc, onSaved, Volet.Indice);

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
        RebuildListes(loc);
        for (int i = 0; i < 12; i++) _moisState[i] = false;
        if (loc.moisFacturationLoyer != null)
            foreach (int m in loc.moisFacturationLoyer)
                if (m >= 1 && m <= 12) _moisState[m - 1] = true;
        for (int i = 0; i < 12; i++) RefreshChip(i);
        RefreshRepriseOptions();

        periodiciteDropdown.onValueChanged.RemoveAllListeners();
        periodiciteDropdown.onValueChanged.AddListener(_ => OnPeriodiciteChanged());
        RefreshMoisVisibility();

        // Champs bail. Le loyer de départ se saisit dans « Initialiser » ; il change
        // le libellé du bouton (Réinitialiser) dès qu'il diffère de celui enregistré.
        loyerDepart.text = loc.loyerDepart > 0f ? loc.loyerDepart.ToString() : "";
        loyerDepart.onValueChanged.RemoveAllListeners();
        loyerDepart.onValueChanged.AddListener(_ => RefreshBoutonInit());
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
        // Décomposition sûre : `Normalize(...).Split('-')[1]` levait IndexOutOfRange
        // sur un trimestre au format ancien (« T1 », « 20261 »), ce qui empêchait
        // purement et simplement l'ouverture de la modale de révision.
        if (InseeIndiceService.TryDecompose(loc.trimestreDeRevision, out _, out int tRev))
        {
            // Année précédente par défaut : le trimestre de l'année en cours
            // n'est en général pas encore publié par l'INSEE (recherche exacte).
            trimestreVoulu.SetTrimestre($"{DateTime.Now.Year - 1}-T{tRev}");
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

        // Type de révision + franchise (écran « Initialiser »).
        _revisionToggle.SetIsOnWithoutNotify(loc.typeRevision != TypeRevision.Aucune);
        _typeRevDD.SetOptions(TypesLibelles, TypesIds,
            loc.typeRevision == TypeRevision.Paliers ? "Paliers"
            : loc.typeRevision == TypeRevision.ChiffreAffaires ? "CA" : "Indice");
        ChargerCaseDate(_franchiseToggle, _franchiseDate, loc.debutFacturationISO);
        ChargerCaseDate(_avenantToggle, _avenantDate, "");   // un avenant se saisit à chaque réinitialisation
        RefreshInitVisibility();

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

        // Mode imposé par l'état de l'indice : l'initialiser d'abord, le réviser ensuite.
        // (La bascule manuelle du prefab est masquée dans ApplyVolet.)
        SetMode(!loc.IndiceInitialise);

        // Applique le volet demandé (Indice par défaut / Initialiser).
        ApplyVolet(volet);
        RefreshCARevision(true);
        // Référence : le trimestre (et l'indice) de la dernière révision ; vide la 1re fois.
        if (_trimPrecedent != null) _trimPrecedent.text = DerniereRevision(loc.indiceImmoActuel);
        if (loc.typeRevision == TypeRevision.ChiffreAffaires && loc.IndiceInitialise) ViderResultatCA();
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
        RefreshCARevision(false);   // l'année du CA suit la date de révision
        _onSaved?.Invoke();   // sauvegarde + refresh badge/résumé
    }

    // Selon le CA, volet Indice : à l'initialisation, loyer min / max / % sous la date de
    // révision ; à la révision, les bornes en vigueur et le CA HT de l'année civile
    // écoulée (année de la révision - 1), pré-rempli s'il a déjà été déclaré.
    void RefreshCARevision(bool preRemplir)
    {
        if (_caRevisionGO == null || _loc == null) return;
        bool ca = _loc.typeRevision == TypeRevision.ChiffreAffaires;
        bool init = !_loc.IndiceInitialise;

        // Loyer min / max / % : au format du loyer de départ, dans le volet Indice.
        foreach (var g in _caChamps) SetGO(g, ca && _volet == Volet.Indice);
        // Résultat : tableau CA / bornes / loyer retenu à la place des indices.
        SetGO(_caResultGO, ca && !init);
        SetGO(_compareStripGO, !ca);
        SetGO(_loyerPrecedentGO, !ca);
        if (_titreLoyerCalcule != null) _titreLoyerCalcule.text = ca ? "Nouveau loyer annuel" : "Loyer révisé";
        _caRevisionGO.SetActive(ca && !init);
        if (!ca) return;

        // Bornes : saisies à l'initialisation, grisées à la révision (l'indice les révise).
        // La part du CA reste modifiable à la révision : rare, mais possible (retour du 01/10).
        _caMin.interactable = _caMax.interactable = init;
        _caPct.interactable = true;
        if (preRemplir)
        {
            _caMin.text = Nombre(init || _loc.loyerMinRevise <= 0f ? _loc.loyerMinBase : _loc.loyerMinRevise);
            _caMax.text = Nombre(init || _loc.loyerMaxRevise <= 0f ? _loc.loyerMaxBase : _loc.loyerMaxRevise);
            _caPct.text = Nombre(_loc.pourcentageCA);
        }
        if (init) return;

        int annee = dateDeRevision.SelectedDate.Year - 1;
        _caAnnee = annee;
        _caTitre.text = $"Nouveau CA HT ({annee})";
        // Précédent : le dernier CA enregistré avant cette année ; vide s'il n'y en a pas.
        var prec = _loc.chiffresAffaires?.Where(c => c.annee < annee).OrderByDescending(c => c.annee).FirstOrDefault();
        _caTitrePrecedent.text = prec != null ? $"CA précédent ({prec.annee})" : "CA précédent";
        _caPrecedent.text = prec != null ? Euros(prec.montantHT) : "";
        if (preRemplir || string.IsNullOrWhiteSpace(_caMontant.text))
            _caMontant.text = Loyers.CA(_loc, annee) is float v ? v.ToString(CultureInfo.InvariantCulture) : "";
    }

    // Résultat vide (avant le calcul) : rien d'affiché ne doit passer pour un résultat.
    void ViderResultatCA()
    {
        foreach (var t in new[] { _rPct, _rMin, _rMax, _rRegle, _rAncien, _rProchaine })
            if (t != null) t.text = "—";
        if (_rIndice != null) _rIndice.text = "";
        if (txtLoyerCalcule != null) txtLoyerCalcule.text = "—";
    }

    // ── Initialisation (nouveau bail) ─────────────────────────────────────────

    private IEnumerator Initialiser()
    {
        if (!TryParseLoyer(out float loyer)) yield break;

        // Selon le CA : loyer min / max et % obligatoires, le max au moins égal au min.
        bool selonCA = _loc.typeRevision == TypeRevision.ChiffreAffaires;
        float pct = 0f, min = 0f, max = 0f;
        if (selonCA)
        {
            SaisieNumerique.TryParse(_caPct.text, out pct);
            SaisieNumerique.TryParse(_caMin.text, out min);
            SaisieNumerique.TryParse(_caMax.text, out max);
            if (min <= 0f || max < min)
            { statusText.text = "Saisissez un loyer minimum, et un maximum au moins égal au minimum."; yield break; }
            if (pct <= 0f || pct > 100f)
            { statusText.text = "Saisissez le pourcentage du chiffre d'affaires (entre 0 et 100)."; yield break; }
        }

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
        _loc.indiceImmoAuDepart = FormatIndice(obsRef.valeur, obsRef.periode);
        _loc.indiceImmoActuel = "—";
        EcrireChampsIndice();
        _loc.MoisDeRevision = dateDeRevision.SelectedDate;
        if (selonCA)
        {
            // Bornes à l'indice de référence ; pas encore de révision : en vigueur = base.
            _loc.pourcentageCA = pct;
            _loc.loyerMinBase = _loc.loyerMinRevise = min;
            _loc.loyerMaxBase = _loc.loyerMaxRevise = max;
        }

        // Historique : nouvelle référence d'indexation (prend effet au début du bail,
        // ou à la date de l'avenant après une renégociation)
        LoyerHistoryService.EnregistrerReference(_loc,
            FacturationSuivi.TryEcheance(_loc.debutConditionsISO, out var dbInit)
                || DateTime.TryParse(_loc.dateDebutBailISO, out dbInit) ? dbInit : DateTime.Now,
            (IndiceImmo)indiceDropdown.value, obsRef.periode, obsRef.valeur, loyer);

        txtIndiceDepart.text = _loc.indiceImmoAuDepart;
        txtIndiceActuel.text = "—";
        txtLoyerCalcule.text = $"{loyer:N2} €";
        if (txtVariation != null) txtVariation.text = "—";
        if (txtLoyerPrecedent != null) txtLoyerPrecedent.text = "—";

        bool estFallback = InseeIndiceService.Normalize(obsRef.periode) != periode;
        string message = estFallback
            ? $"Indice initialisé — indice {obsRef.periode} utilisé ({periode} non encore publié)"
            : $"Indice initialisé — indice {obsRef.periode} : {obsRef.valeur:F2}";

        // Retour à la fiche (schéma) : la révision se fera plus tard, par « Réviser ».
        _onSaved?.Invoke();
        UndoToast.Instance?.ShowInfo(message);
        gameObject.SetActive(false);
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

        // Le fallback peut reculer de plusieurs années sans rien dire : une base plus
        // ancienne que demandée gonfle le ratio et donc le loyer révisé, sur un écran
        // parfaitement normal. On le signale explicitement.
        if (!string.Equals(obsDepart.periode, periodeDepart, StringComparison.OrdinalIgnoreCase))
            AfficheTrimWarn($"Indice de départ {periodeDepart} non publié — calcul basé sur {obsDepart.periode}.");

        // Un indice de départ nul rendrait la division infinie : le loyer révisé
        // deviendrait Infinity/NaN et serait enregistré tel quel sur le locataire.
        if (obsDepart.valeur <= 0f)
        {
            statusText.text = $"Indice de départ invalide ({obsDepart.periode} = {obsDepart.valeur}) — révision impossible.";
            if (btnReviser != null) btnReviser.interactable = false;
            yield break;
        }

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
        float ratio = obsActuel.valeur / obsDepart.valeur;
        float loyerRevise = loyer * ratio;

        // Selon le CA : % du CA HT de l'année écoulée, borné par le minimum et le maximum
        // indexés comme le loyer en mode indice (base × nouvel indice / référence).
        bool selonCA = _loc.typeRevision == TypeRevision.ChiffreAffaires;
        int anneeCA = dateDeRevision.SelectedDate.Year - 1;
        float caHT = 0f, minRev = 0f, maxRev = 0f, brut = 0f;
        if (selonCA)
        {
            // Le montant saisi vaut pour l'année affichée. Si la date de révision a avancé
            // (révision déjà faite), on ne réutilise pas ce CA en silence pour l'année suivante.
            if (anneeCA != _caAnnee)
            {
                RefreshCARevision(false);   // l'année suit la date ; le montant reste à vérifier
                statusText.text = $"La date de révision porte sur le CA {anneeCA} : vérifiez le montant, puis recalculez.";
                yield break;
            }
            if (!SaisieNumerique.TryParse(_caMontant.text, out caHT) || caHT <= 0f)
            { statusText.text = $"Saisissez le CA HT {anneeCA} du locataire."; yield break; }
            if (!SaisieNumerique.TryParse(_caPct.text, out float pctSaisi) || pctSaisi <= 0f || pctSaisi > 100f)
            { statusText.text = "Saisissez la part du CA (entre 0 et 100 %)."; yield break; }
            minRev = (float)Math.Round(_loc.loyerMinBase * ratio, 2);
            maxRev = (float)Math.Round(_loc.loyerMaxBase * ratio, 2);
            // Part du CA éventuellement modifiée à l'écran : elle vaut pour cette révision et
            // les suivantes. Remise en place si la révision n'aboutit pas.
            float pctAvant = _loc.pourcentageCA;
            _loc.pourcentageCA = pctSaisi;
            loyerRevise = Loyers.LoyerSelonCA(_loc, anneeCA, caHT, minRev, maxRev, out brut);
            if (loyerRevise <= 0f)
            {
                _loc.pourcentageCA = pctAvant;
                statusText.text = $"Le locataire n'était pas dans les lieux en {anneeCA} : aucun CA à prendre en compte.";
                yield break;
            }
        }

        // Prochaine révision = date saisie + 1 an. Calculée AVANT toute écriture :
        // `new DateTime(d.Year + 1, d.Month, d.Day)` levait sur un 29 février (l'année
        // suivante n'est pas bissextile) APRÈS que loyerAnnuel ait été écrit, laissant
        // le locataire avec le nouveau loyer mais sans date ni historique à jour.
        var d = dateDeRevision.SelectedDate;
        int jourClamp = Math.Min(d.Day, DateTime.DaysInMonth(d.Year + 1, d.Month));
        var prochaineRevision = new DateTime(d.Year + 1, d.Month, jourClamp);

        if (selonCA)
        {
            // L'ancien loyer reste dû jusqu'à la veille de la révision : les périodes
            // passées (facture, rentabilité) gardent leur montant (voir Loyers.LoyerAnnuelA).
            Loyers.EnregistrerAvenant(_loc, d.Date);
            _loc.loyerMinRevise = minRev;
            _loc.loyerMaxRevise = maxRev;
            Loyers.PoserCA(_loc, anneeCA, caHT);
        }

        _loc.loyerDepart = loyer;
        _loc.loyerAnnuelPrecedent = _loc.loyerAnnuel;   // loyer AVANT révision
        _loc.loyerAnnuel = loyerRevise;
        _loc.indiceImmoAuDepart = FormatIndice(obsDepart.valeur, obsDepart.periode);
        _loc.indiceImmoActuel = FormatIndice(obsActuel.valeur, obsActuel.periode);
        _loc.dernierRevision = DateTime.Now.ToString("yyyy-MM-dd");

        _loc.MoisDeRevision = prochaineRevision;
        dateDeRevision.ApplyDate(_loc.MoisDeRevision);
        dateDeRevision.ModifyDate();

        EcrireChampsIndice();

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
        if (selonCA)
        {
            // Le résultat parle de CA, de bornes et de loyer ; les indices n'en sont
            // que l'origine des bornes (une ligne discrète en bas).
            var fr = FacturePdfService.FrCulture;
            bool rameneAnnee = Loyers.FractionPresence(_loc, anneeCA) < 0.999;
            txtLoyerCalcule.text = Euros(loyerRevise);
            _lPct.text = $"{_loc.pourcentageCA.ToString("0.##", fr)} % du CA" + (rameneAnnee ? " (ramené à l'année)" : "");
            _rPct.text = Euros(brut);
            _rMin.text = Euros(minRev);
            _rMax.text = Euros(maxRev);
            _rRegle.text = brut < minRev ? "le minimum" : brut > maxRev ? "le maximum" : "le % du CA";
            _rAncien.text = Euros(_loc.loyerAnnuelPrecedent);
            _rProchaine.text = _loc.MoisDeRevision.ToString("dd/MM/yyyy");
            float variationBornes = (obsActuel.valeur / obsDepart.valeur - 1f) * 100f;
            _rIndice.text = $"Bornes indexées sur l'{InseeIndiceService.GetLabel(type)} : "
                + $"{obsDepart.valeur.ToString("0.00", fr)} ({obsDepart.periode}) → {obsActuel.valeur.ToString("0.00", fr)} ({obsActuel.periode}), "
                + $"{(variationBornes >= 0 ? "+" : "")}{variationBornes.ToString("0.0", fr)} %";
            statusText.text = "";
            // Le CA reste dans son champ, là où il a été saisi (retour du 01/10) ; seules
            // les bornes affichées passent aux nouvelles valeurs.
            _caMin.text = Nombre(minRev);
            _caMax.text = Nombre(maxRev);
        }
        else
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
        t.fontSize = UITheme.Role.Aide;
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
    /// Sérialise un indice INSEE au format `"125.50  (2025-T1)"`.
    /// Le séparateur décimal est forcé en culture INVARIANTE (point) : c'est ce que
    /// `LoyerHistoryService.ParseIndiceValeur` relit. Avec l'interpolation `$"{v:F2}"`
    /// (culture courante), une machine en `fr-FR` aurait écrit `"125,50"`, que la
    /// relecture invariante rejette → indice à 0 → tableau de rentabilité indexée
    /// silencieusement rabattu sur un loyer plat.
    private static string FormatIndice(float valeur, string periode)
        => valeur.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
           + $"  ({periode})";

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

    // Champs propres à l'indice. Les modalités (périodicité, provisions…) ne sont
    // écrites que par l'écran « Initialiser » : l'indice les réécrivait autrefois
    // depuis des champs masqués.
    private void EcrireChampsIndice()
    {
        _loc.indiceTypeImmo = (IndiceImmo)indiceDropdown.value;
        _loc.trimestreDeRevision = trimestreDepart.TrimestreValue;
    }

    // ── Facturation : champs injectés (jour, mois, régularisation) ────────────

    void EnsureExtraBuilt()
    {
        if (_extraBuilt) return;
        _extraBuilt = true;

        var periodBlock = periodiciteDropdown.transform.parent;
        var content = periodBlock.parent;
        int idx = periodBlock.GetSiblingIndex() + 1;

        var l1 = UIFactory.Text(content, "Loyer demandé le … (jour du mois)", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        l1.transform.SetSiblingIndex(idx++);
        _jourLabelGO = l1.gameObject;
        _jourDemande = UIFactory.Input(content, "1");
        _jourDemande.contentType = TMP_InputField.ContentType.IntegerNumber;
        _jourDemande.transform.SetSiblingIndex(idx++);

        var l2 = UIFactory.Text(content, "Mois facturés (si trimestriel / bi-annuel)", UITheme.Role.Donnee, UITheme.TexteSecondaire);
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
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        _repriseDD = UIDropdown.Create(rv.transform,
            new List<string> { "Aucune (nouveau bail)" }, new List<string> { "" }, 0, _ => { });
        rv.transform.SetSiblingIndex(regulGO.transform.GetSiblingIndex() + 1);

        // Listes de charges spécifiques : juste sous la date de régularisation générale.
        var lb = UIFactory.VBox(content, 6, 0, 0, 4, 4, "ListesChargesBlock");
        _listesBlockGO = lb.gameObject;
        lb.transform.SetSiblingIndex(regulGO.transform.GetSiblingIndex() + 1);

        // Type de révision : case « Révision du loyer » + menu Indice / Paliers
        // (case décochée = pas de révision). Entre les listes et la reprise.
        var rb = UIFactory.VBox(content, 6, 0, 0, 4, 4, "RevisionTypeBlock");
        _revisionBlockGO = rb.gameObject;
        _revisionToggle = UIFactory.Toggle(rb.transform, "Révision du loyer", true);
        var tr = UIFactory.VBox(rb.transform, 4, 0, 0, 0, 0, "TypeRevision");
        _typeRevRowGO = tr.gameObject;
        UIFactory.Text(tr.transform, "Type de révision", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _typeRevDD = UIDropdown.Create(tr.transform, TypesLibelles, TypesIds, 0, _ => RefreshBoutonInit());
        _revisionToggle.onValueChanged.AddListener(_ => { RefreshInitVisibility(); RefreshBoutonInit(); });

        // Loyer selon le CA (schéma du 01/10) : loyer minimum, maximum et % du CA, au
        // format du « Loyer de départ » (le même bloc, cloné) et juste dessous. Saisis à
        // l'initialisation ; grisés à la révision, où ils montrent les bornes en vigueur.
        Transform ld = loyerDepart.transform;
        while (ld != null && ld.parent != content) ld = ld.parent;
        if (ld != null)
        {
            int at = ld.GetSiblingIndex();
            _caMin = ChampCA(ld.gameObject, content, ++at, "LoyerMinimum", "Loyer minimum", "€");
            _caMax = ChampCA(ld.gameObject, content, ++at, "LoyerMaximum", "Loyer maximum", "€");
            _caPct = ChampCA(ld.gameObject, content, ++at, "PourcentageCA", "Part du CA HT", "%");
        }

        // Révision : le CA de l'année écoulée, sous le trimestre de révision.
        var parentRev = trimestreVoulu != null ? trimestreVoulu.transform.parent : null;
        if (parentRev != null && ld != null)
        {
            // Trimestre de la dernière révision (référence) à côté de celui de cette
            // révision — indice comme CA (retour du 01/10). L'avertissement du trimestre
            // est créé avant le déplacement : il reste sous la ligne, pas dedans.
            EnsureTrimWarn();
            var lt = UIFactory.HBox(parentRev, 14, true, "LigneTrimestres");
            lt.childAlignment = TextAnchor.UpperLeft;
            lt.transform.SetSiblingIndex(trimestreVoulu.transform.GetSiblingIndex());
            _trimPrecedent = ClonerChamp(ld.gameObject, lt.transform, 0, "DerniereRevision", "Dernière révision", "");
            _trimPrecedent.contentType = TMP_InputField.ContentType.Standard;
            _trimPrecedent.interactable = false;
            _trimPrecedent.transform.parent.parent.gameObject.SetActive(true);
            trimestreVoulu.transform.SetParent(lt.transform, false);
            foreach (Transform b in lt.transform)
            {
                var le = b.GetComponent<LayoutElement>() ?? b.gameObject.AddComponent<LayoutElement>();
                le.minWidth = 0f; le.preferredWidth = 1f; le.flexibleWidth = 1f;
            }
        }
        if (parentRev != null)
        {
            var crv = UIFactory.VBox(parentRev, 4, 0, 0, 4, 4, "CARevision");
            _caRevisionGO = crv.gameObject;
            // CA précédent (dernier enregistré, vide sinon) à côté du nouveau CA à saisir.
            var cote = UIFactory.HBox(crv.transform, 12, true, "CAPrecedentNouveau");
            cote.childAlignment = TextAnchor.UpperLeft;
            var gauche = UIFactory.VBox(cote.transform, 4, 0, 0, 0, 0, "CAPrecedent");
            _caTitrePrecedent = UIFactory.Text(gauche.transform, "CA précédent", UITheme.Role.Donnee, UITheme.TexteSecondaire);
            _caPrecedent = UIFactory.Input(gauche.transform, "—");
            _caPrecedent.interactable = false;
            var droite = UIFactory.VBox(cote.transform, 4, 0, 0, 0, 0, "CANouveau");
            _caTitre = UIFactory.Text(droite.transform, "Nouveau CA HT", UITheme.Role.Donnee, UITheme.TexteSecondaire);
            _caMontant = UIFactory.Input(droite.transform, "CA HT de l'année (€)");
            _caMontant.contentType = TMP_InputField.ContentType.DecimalNumber;
            _caRevisionGO.SetActive(false);
        }

        // Résultat d'une révision selon le CA : ce qui compte, ce sont le CA, les bornes et
        // le loyer retenu — pas les indices (retour du 01/10). Tableau dans le cadre
        // « Résultat de la révision », la comparaison d'indices réduite à une ligne.
        Transform info = statusText.transform;
        while (info != null && info.parent != content) info = info.parent;
        if (info != null)
        {
            _compareStripGO = EnfantDirect(info, txtIndiceDepart != null ? txtIndiceDepart.transform : null);
            _loyerPrecedentGO = EnfantDirect(info, txtLoyerPrecedent != null ? txtLoyerPrecedent.transform : null);
            var ligneLoyer = EnfantDirect(info, txtLoyerCalcule.transform);
            _titreLoyerCalcule = ligneLoyer != null ? ligneLoyer.transform.Find("title")?.GetComponent<TMP_Text>() : null;
            var res = UIFactory.VBox(info, 4, 0, 0, 2, 2, "ResultatCA");
            _caResultGO = res.gameObject;
            if (ligneLoyer != null) res.transform.SetSiblingIndex(ligneLoyer.transform.GetSiblingIndex() + 1);
            // Deux colonnes : le calcul (%, bornes) à gauche, la décision à droite. Pas de
            // ligne « CA » : il reste dans son champ, là où il a été saisi.
            var deux = UIFactory.HBox(res.transform, 24, true, "DeuxColonnes");
            deux.childAlignment = TextAnchor.UpperLeft;
            var colG = UIFactory.VBox(deux.transform, 3, 0, 0, 0, 0, "Calcul");
            var colD = UIFactory.VBox(deux.transform, 3, 0, 0, 0, 0, "Decision");
            _rPct = LigneResultat(colG.transform, "% du CA", out _lPct);
            _rMin = LigneResultat(colG.transform, "Nouveau loyer minimum", out _);
            _rMax = LigneResultat(colG.transform, "Nouveau loyer maximum", out _);
            _rRegle = LigneResultat(colD.transform, "Loyer retenu", out _);
            _rAncien = LigneResultat(colD.transform, "Ancien loyer", out _);
            _rProchaine = LigneResultat(colD.transform, "Prochaine révision", out _);
            _rIndice = UIFactory.Text(res.transform, "", UITheme.Role.Aide, UITheme.TexteSecondaire);
            _caResultGO.SetActive(false);
        }

        _leFenetre = content.parent.GetComponent<LayoutElement>();
        _leTitre = content.parent.Find("titre")?.GetComponent<LayoutElement>();

        // Avenant (visible seulement pour « Réinitialiser ») et franchise. Le départ du
        // locataire est un événement du BAIL : il se saisit dans la section Bail de la
        // fiche (LocataireBailFields), pas ici.
        _avenantBlockGO = BlocCaseDate(content, "AvenantBlock",
            "Avenant : le nouveau loyer s'applique en cours de bail", "Nouvelles conditions à compter du",
            out _avenantToggle, out _avenantDate);
        _franchiseBlockGO = BlocCaseDate(content, "FranchiseBlock",
            "Franchise de loyer (les provisions restent dues)", "Loyer facturé à partir du (fin de la franchise)",
            out _franchiseToggle, out _franchiseDate);

        // Ordre : type de révision, avenant, franchise, puis la reprise.
        int ri = _repriseBlockGO.transform.GetSiblingIndex();
        rb.transform.SetSiblingIndex(ri);
        _avenantBlockGO.transform.SetSiblingIndex(ri + 1);
        _franchiseBlockGO.transform.SetSiblingIndex(ri + 2);

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
            _btnEnregistrer.onClick.AddListener(ValiderInitialisation);
            _btnEnregistrer.gameObject.SetActive(false);
        }

        // En dernier : les clones ci-dessus partent des blocs d'origine, hors des lignes.
        LignesCompactes(content);
    }

    // Mois visibles seulement dans le volet Initialiser, hors mensuel ;
    // régularisation visible seulement dans Initialiser et en cas de provision.
    void RefreshMoisVisibility()
    {
        bool show = _volet == Volet.Initialisation && periodiciteDropdown.value != (int)Periodicite.mensuel;
        if (_moisLabelGO != null) _moisLabelGO.SetActive(show);
        if (_moisWrapGO != null) _moisWrapGO.SetActive(show);
    }

    void RefreshRegulVisibility()
    {
        bool show = _volet == Volet.Initialisation && toggleProvisions.isOn;
        // Avec des listes spécifiques, la générale a sa ligne en bas comme les autres :
        // le montant à côté de la case et la date du prefab feraient doublon.
        bool avecListes = ListesCharges.Specifiques().Count > 0;
        if (_regulBlockGO != null) _regulBlockGO.SetActive(show && !avecListes);
        ProvContainer().SetActive(toggleProvisions.isOn && !avecListes);
        // Les listes n'existent qu'avec une provision : sans elle, le locataire relève de
        // toutes les charges, comme avant les listes (voir ListesCharges.ConcerneParListe).
        if (_listesBlockGO != null) _listesBlockGO.SetActive(show && avecListes);
    }

    // Champ montant + « € » à côté de la case Provisions.
    GameObject ProvContainer() => provisionValue.transform.parent != null
        ? provisionValue.transform.parent.gameObject : provisionValue.gameObject;

    // Choix des listes qui concernent le locataire (une case par liste, toutes cochées =
    // toutes), puis une ligne « date + provision » par liste cochée.
    void RebuildListes(Locataire loc)
    {
        _listesChamps.Clear();
        _choixListes.Clear(); _lignesListes = null;
        if (_listesBlockGO == null) return;
        foreach (Transform c in _listesBlockGO.transform) Destroy(c.gameObject);

        var specs = ListesCharges.Specifiques();
        if (specs.Count == 0) return;

        UIFactory.Text(_listesBlockGO.transform, "Listes de charges concernées", UITheme.Role.Libelle, UITheme.TextePrincipal, true);
        var ids = new List<string> { "" };
        ids.AddRange(specs.Select(l => l.id));
        foreach (var id in ids)
        {
            var t = UIFactory.Toggle(_listesBlockGO.transform, ListesCharges.Nom(id), ListesCharges.ConcerneParListe(loc, id));
            t.onValueChanged.AddListener(on =>
            {
                // Au moins une liste : aucune case cochée voudrait dire « aucune charge ».
                if (!on && _choixListes.All(x => !x.t.isOn)) { t.SetIsOnWithoutNotify(true); return; }
                if (!on && !RetraitImmediat(loc, id, t)) return;   // décision dans la boîte de dialogue
                RebuildLignesListes(loc);
            });
            _choixListes.Add((id, t));
        }
        UIFactory.Text(_listesBlockGO.transform,
            "Une charge d'une liste décochée ne lui est ni proposée, ni répartie.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);

        _lignesListes = UIFactory.VBox(_listesBlockGO.transform, 6, 0, 0, 0, 0, "LignesListes").transform;
        RebuildLignesListes(loc);
    }

    /// Retirer une liste à un locataire qui y a des charges. Il en doit encore une
    /// (non régularisée, ou non encaissée) → refusé : on la lui ferait perdre de vue
    /// avant de l'avoir fait payer. Toutes payées → avertissement, retrait possible.
    /// Aucune charge → retrait direct. Renvoie vrai si la case peut rester décochée
    /// tout de suite ; sinon elle est recochée et la boîte de dialogue décide.
    bool RetraitImmediat(Locataire loc, string listeId, Toggle caseListe)
    {
        // Liste qu'il n'avait pas encore (cochée puis décochée) : rien à protéger.
        if (!ListesCharges.ConcerneParListe(loc, listeId)) return true;

        var bat = BatimentManager.Instance?.BatimentPrefab?
            .Find(bp => bp != null && bp.listLocataire.Contains(loc))?.getBatiment();
        var toutes = ListesCharges.ChargesDeListe(bat, loc, listeId, false);
        if (toutes.Count == 0) return true;

        string nom = ListesCharges.Nom(listeId);
        var dues = ListesCharges.ChargesDeListe(bat, loc, listeId, true);
        caseListe.SetIsOnWithoutNotify(true);   // en attendant la décision

        if (dues.Count > 0)
        {
            string detail = $"{loc.Name} a encore {dues.Count} charge(s) « {nom} » non payée(s) : "
                + string.Join(", ", dues.Take(5).Select(c => c.nom)) + (dues.Count > 5 ? "…" : "")
                + ".\n\nFaites la régularisation de toutes les charges de cette liste et attendez son "
                + "paiement avant de la lui retirer.";
            if (ConfirmDialog.Instance != null)
                ConfirmDialog.Instance.Show($"Impossible de retirer « {nom} »", detail, () => { }, "Compris");
            else UndoToast.Instance?.ShowInfo(detail);
            return false;
        }

        string avert = $"{loc.Name} a {toutes.Count} charge(s) « {nom} », toutes payées.\n\n"
            + "Les prochaines charges de cette liste ne lui seront plus proposées ni réparties. Retirer quand même ?";
        if (ConfirmDialog.Instance == null) { UndoToast.Instance?.ShowInfo(avert); return false; }
        ConfirmDialog.Instance.Show($"Retirer « {nom} » ?", avert, () =>
        {
            caseListe.SetIsOnWithoutNotify(false);
            RebuildLignesListes(loc);
        }, "Retirer");
        return false;
    }

    /// Listes cochées ; toutes si aucune case (pas de liste spécifique).
    List<string> ListesChoisies()
        => _choixListes.Count == 0 ? new List<string> { "" }
         : _choixListes.Where(x => x.t != null && x.t.isOn).Select(x => x.id).ToList();

    void RebuildLignesListes(Locataire loc)
    {
        if (_lignesListes == null) return;
        // Ce qui a déjà été tapé survit au changement de choix.
        var saisi = _listesChamps.ToDictionary(x => x.id,
            x => (x.date.LireDate(out var dd) ? dd.ToString("yyyy-MM-dd") : null, x.prov.text));
        _listesChamps.Clear();
        foreach (Transform c in _lignesListes) Destroy(c.gameObject);

        // Une ligne par liste retenue, la générale en tête (id "") : toutes sur le même
        // modèle — nom, date de régularisation, provision par période.
        var choisies = ListesChoisies();
        var retenues = new List<(string id, string nom, string dateISO, float prov)>();
        if (choisies.Contains(""))
            retenues.Add(("", ListesCharges.NomGenerale, loc?.dateRegularisationChargeISO, loc != null ? loc.provisionPourChargeValue : 0f));
        foreach (var l in ListesCharges.Specifiques().Where(l => choisies.Contains(l.id)))
        {
            var rl = ListesCharges.De(loc, l.id);
            retenues.Add((l.id, l.nom, rl?.dateISO, rl?.provision ?? 0f));
        }

        UIFactory.Text(_lignesListes,
            "Pour chaque liste : date de régularisation (vide = pas de régularisation) et provision par période.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        foreach (var (id, nom, dateISO, provision) in retenues)
        {
            saisi.TryGetValue(id, out var deja);

            // Même saisie de date que partout dans l'app : clone du bloc « Date de
            // révision » (cases JJ / MM / AAAA), comme la date de régularisation générale.
            var bloc = Instantiate(dateDeRevision.gameObject, _lignesListes);
            bloc.name = "DateRegul_" + nom;
            bloc.SetActive(true);
            var date = bloc.GetComponent<DateInputController>();
            date.OnModify.RemoveAllListeners();
            var titre = bloc.transform.Find("Titre")?.GetComponent<TMP_Text>();
            if (titre != null) titre.text = $"{nom} — date de régularisation";
            if (DateTime.TryParse(deja.Item1 ?? dateISO, out var d)) date.ApplyDate(d);
            else { date.dayInput.text = ""; date.monthInput.text = ""; date.yearInput.text = ""; }
            date.ModifyDate();

            UIFactory.Text(_lignesListes, $"{nom} — provision par période (€)", UITheme.Role.Donnee, UITheme.TexteSecondaire);
            var prov = UIFactory.Input(_lignesListes, "0");
            prov.contentType = TMP_InputField.ContentType.DecimalNumber;
            prov.text = deja.Item2 ?? (provision > 0f ? provision.ToString("0.##", CultureInfo.InvariantCulture) : "");
            UIFactory.LE(prov.gameObject, minH: 40);
            _listesChamps.Add((id, date, prov));
        }
    }

    void AppliquerListes()
    {
        if (_loc == null) return;
        if (_choixListes.Count > 0)
        {
            // Toutes cochées = aucune restriction (liste vide) : une liste créée plus
            // tard dans les Réglages le concernera d'office, comme aujourd'hui.
            var choisies = ListesChoisies();
            _loc.listesConcernees = choisies.Count == _choixListes.Count ? new List<string>() : choisies;
        }
        _loc.regulListes ??= new List<RegulListe>();
        foreach (var (id, date, prov) in _listesChamps)
        {
            // Charges générales : ses champs historiques (ceux du prefab sont masqués
            // quand des listes existent, et écrits avant cet appel).
            string iso = date.LireDate(out var d) ? d.ToString("yyyy-MM-dd") : "";
            if (id == "")
            {
                _loc.dateRegularisationChargeISO = iso;
                _loc.provisionPourChargeValue = toggleProvisions.isOn ? SaisieNumerique.Parse(prov.text) : 0f;
                continue;
            }
            var rl = ListesCharges.De(_loc, id);
            if (rl == null) { rl = new RegulListe { listeId = id }; _loc.regulListes.Add(rl); }
            rl.dateISO = iso;
            rl.provision = toggleProvisions.isOn ? SaisieNumerique.Parse(prov.text) : 0f;
        }
    }

    // Options du sélecteur de reprise : « Aucune » + les périodes qui ont pu être
    // facturées hors de l'app, les plus récentes en tête (4 ans). Trois règles
    // (retour du 30/09) : la périodicité CHOISIE à l'écran, et non celle enregistrée
    // (un nouveau locataire, encore mensuel, se voyait proposer des mois alors qu'on
    // venait de choisir « trimestriel ») ; pas de période future — seulement celles
    // déjà échues ou dont la facture est déjà partie (22 j avant l'échéance, comme le
    // suivi) ; rien d'antérieur au bail.
    void RefreshRepriseOptions()
    {
        if (_repriseDD == null || _loc == null) return;
        var labels = new List<string> { "Aucune (nouveau bail)" };
        var ids = new List<string> { "" };
        var p = (Periodicite)periodiciteDropdown.value;
        int n = LoyerSummaryUI.NbPeriodes(p);
        int jour = _jourDemande != null && int.TryParse(_jourDemande.text, out int j) ? j : _loc.jourDemandeLoyer;
        var limite = DateTime.Today.AddDays(FacturationSuivi.Lead("Loyer"));
        bool avecBail = Loyers.DebutPremierBail(_loc, out var debutBail);
        int cur = DateTime.Now.Year;
        for (int y = cur; y >= cur - 3; y--)
            for (int per = n; per >= 1; per--)
            {
                var ech = FacturationSuivi.EcheancePeriode(p, per, y, jour);
                if (ech > limite) continue;
                var finPeriode = new DateTime(ech.Year, ech.Month, 1).AddMonths(12 / n).AddDays(-1);
                if (avecBail && finPeriode < debutBail.Date) continue;
                labels.Add(FacturationSuivi.LibellePeriode(p, per, y));
                ids.Add($"{y}-P{per}");
            }
        string sel = "";
        if (DateTime.TryParse(_loc.repriseFacturationISO, out var rd))
        {
            int perEnr = FacturationSuivi.PeriodeIndex(_loc, rd.Month);
            sel = $"{rd.Year}-P{perEnr}";
            // Reprise déjà enregistrée mais hors des options (plus de 4 ans…) : on la
            // garde proposée, sinon un simple « Modifier » l'effaçait sans le dire.
            if (!ids.Contains(sel) && p == _loc.periodiciteLoyer)
            {
                labels.Insert(1, FacturationSuivi.LibellePeriode(p, perEnr, rd.Year));
                ids.Insert(1, sel);
            }
        }
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
        bool mod = volet == Volet.Initialisation;

        // Titre de la modale
        var titleT = transform.Find("Content/titre/Revision")?.GetComponent<TMP_Text>();
        if (titleT != null)
            titleT.text = mod
                ? (_loc != null && _loc.LoyerInitialise ? "Gestion du loyer" : "Initialisation du loyer")
                : _loc != null && _loc.typeRevision == TypeRevision.ChiffreAffaires
                    ? (_loc.IndiceInitialise ? "Révision du loyer selon le CA" : "Initialisation du loyer selon le CA")
                    : (_loc != null && _loc.IndiceInitialise ? "Révision du loyer" : "Initialisation de l'indice");

        // Loyer de départ : visible partout, saisi seulement dans « Initialiser » (le
        // changer ensuite demande de réinitialiser).
        SetGO(_loyerDepartGO, true);
        loyerDepart.interactable = mod;

        // Blocs « Indice » (masqués dans Initialiser). Le mode (initialiser / réviser)
        // suit l'état de l'indice : la bascule manuelle du prefab n'est plus montrée.
        SetGO(_chipsIndiceGO, !mod);
        SetGO(_trimRefGO, !mod);
        SetGO(_dateRevGO, !mod);
        SetGO(_modeToggleGO, false);
        SetGO(_informationGO, !mod);
        AppliquerLargeur(!mod);

        // Blocs « Initialiser » (masqués dans Indice)
        SetGO(_periodiciteGO, mod);
        SetGO(_jourLabelGO, mod);
        if (_jourDemande != null) SetGO(_jourDemande.gameObject, mod);
        SetGO(_provBlockGO, mod);
        SetGO(_repriseBlockGO, mod);
        RefreshMoisVisibility();     // mois : Initialiser + hors mensuel
        RefreshRegulVisibility();    // régularisation : Initialiser + provision
        RefreshInitVisibility();     // type de révision + franchise : Initialiser

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
        RefreshBoutonInit();
    }

    // Bloc « case + date » de l'écran Initialiser : la date n'apparaît que case cochée.
    // Même saisie de date que partout (clone du bloc « Date de révision »).
    GameObject BlocCaseDate(Transform content, string nom, string libelleCase, string titreDate,
                            out Toggle caseACocher, out DateInputController date)
    {
        var bloc = UIFactory.VBox(content, 6, 0, 0, 4, 4, nom);
        caseACocher = UIFactory.Toggle(bloc.transform, libelleCase, false);
        var go = Instantiate(dateDeRevision.gameObject, bloc.transform);
        go.name = nom + "Date";
        go.SetActive(true);
        date = go.GetComponent<DateInputController>();
        date.OnModify.RemoveAllListeners();
        var titre = go.transform.Find("Titre")?.GetComponent<TMP_Text>();
        if (titre != null) titre.text = titreDate;
        caseACocher.onValueChanged.AddListener(_ => { RefreshInitVisibility(); RefreshBoutonInit(); });
        foreach (var champ in new[] { date.dayInput, date.monthInput, date.yearInput })
            if (champ != null) champ.onValueChanged.AddListener(_ => RefreshBoutonInit());
        return bloc.gameObject;
    }

    // Remet un bloc « case + date » sur une date stockée (ISO), ou vide et décoché.
    static void ChargerCaseDate(Toggle caseACocher, DateInputController date, string iso)
    {
        bool avec = FacturationSuivi.TryEcheance(iso, out var d);
        caseACocher.SetIsOnWithoutNotify(avec);
        if (avec) date.ApplyDate(d);
        else { date.dayInput.text = ""; date.monthInput.text = ""; date.yearInput.text = ""; }
        date.ModifyDate();
    }

    // Type de révision et franchise : seulement dans « Initialiser » ; le menu du type
    // suit la case « Révision du loyer », chaque date suit sa case. L'avenant, lui,
    // n'apparaît que pour « Réinitialiser » (voir RefreshBoutonInit). Le départ du
    // locataire se saisit dans la section Bail de la fiche.
    void RefreshInitVisibility()
    {
        bool init = _volet == Volet.Initialisation;
        SetGO(_revisionBlockGO, init);
        SetGO(_franchiseBlockGO, init);
        if (!init) SetGO(_avenantBlockGO, false);
        SetGO(_typeRevRowGO, _revisionToggle != null && _revisionToggle.isOn);
        if (_franchiseDate != null) SetGO(_franchiseDate.gameObject, _franchiseToggle.isOn);
        if (_avenantDate != null) SetGO(_avenantDate.gameObject, _avenantToggle.isOn);
    }

    static readonly List<string> TypesLibelles = new List<string> { "Par indice (INSEE)", "Par paliers", "Selon le chiffre d'affaires" };
    static readonly List<string> TypesIds = new List<string> { "Indice", "Paliers", "CA" };

    TypeRevision TypeChoisi()
        => !_revisionToggle.isOn ? TypeRevision.Aucune
         : _typeRevDD.SelectedId == "Paliers" ? TypeRevision.Paliers
         : _typeRevDD.SelectedId == "CA" ? TypeRevision.ChiffreAffaires : TypeRevision.Indice;

    // Clone du bloc « Loyer de départ » (titre, champ, unité) : même visuel, même
    // grisé quand il n'est pas modifiable.
    TMP_InputField ClonerChamp(GameObject modele, Transform parent, int index, string nom, string titre, string unite)
    {
        var go = Instantiate(modele, parent);
        go.name = nom;
        go.transform.SetSiblingIndex(index);
        foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name == "title") t.text = titre;
            else if (t.name == "quantité") { t.text = unite; t.gameObject.SetActive(unite.Length > 0); }
        }
        var f = go.GetComponentInChildren<TMP_InputField>(true);
        f.onValueChanged.RemoveAllListeners();
        f.contentType = TMP_InputField.ContentType.DecimalNumber;
        f.text = "";
        return f;
    }

    // Champ du loyer selon le CA (min, max, %) : visible seulement pour ce type.
    TMP_InputField ChampCA(GameObject modele, Transform parent, int index, string nom, string titre, string unite)
    {
        var f = ClonerChamp(modele, parent, index, nom, titre, unite);
        var go = EnfantDirect(parent, f.transform);
        _caChamps.Add(go);
        go.SetActive(false);
        return f;
    }

    // « 137.16  (2026-Q2) » (indice de la dernière révision) → « 2026-T2 · 137,16 » ; vide
    // s'il n'y a pas encore eu de révision.
    static string DerniereRevision(string indiceActuel)
    {
        if (string.IsNullOrWhiteSpace(indiceActuel)) return "";
        int o = indiceActuel.IndexOf('('), f = indiceActuel.IndexOf(')');
        if (o < 0 || f <= o) return "";
        string periode = InseeIndiceService.Normalize(indiceActuel.Substring(o + 1, f - o - 1).Trim());
        return float.TryParse(indiceActuel.Substring(0, o).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
            ? $"{periode} · {v.ToString("0.00", FacturePdfService.FrCulture)}" : periode;
    }

    // Une ligne « libellé ……… valeur » du résultat d'une révision selon le CA.
    static TMP_Text LigneResultat(Transform parent, string libelle, out TMP_Text lbl)
    {
        var h = UIFactory.HBox(parent, 8, false, "Ligne");
        UIFactory.LE(h.gameObject, minH: 22);
        lbl = UIFactory.Text(h.transform, libelle, UITheme.Role.Donnee, UITheme.TexteSecondaire);
        UIFactory.LE(lbl.gameObject, flexW: 1);
        return UIFactory.Text(h.transform, "—", UITheme.Role.Donnee, UITheme.TextePrincipal, true, TextAlignmentOptions.Right);
    }

    // Mise en page compacte (retour du 01/10 : en hauteur, la fenêtre dépassait de
    // l'écran) : les champs courts côte à côte sur une fenêtre plus large — loyer de
    // départ / min / max / %, puis trimestre de référence / date de révision. Les blocs
    // de la scène sont déplacés dans des lignes ; masqués, ils libèrent leur place.
    void LignesCompactes(Transform corps)
    {
        Ligne(corps, "LigneLoyers", loyerDepart.transform, _caMin?.transform, _caMax?.transform, _caPct?.transform);
        Ligne(corps, "LigneIndice", trimestreDepart.transform, dateDeRevision.transform);
    }

    // Regroupe dans une ligne, à la place du premier, les blocs (enfants directs du corps)
    // qui contiennent ces éléments ; chacun prend une part égale de la largeur.
    void Ligne(Transform corps, string nom, params Transform[] elements)
    {
        var blocs = elements.Select(e => EnfantDirect(corps, e)).Where(b => b != null).Distinct().ToList();
        if (blocs.Count < 2) return;
        var ligne = UIFactory.HBox(corps, 14, true, nom);
        ligne.childAlignment = TextAnchor.UpperLeft;
        ligne.transform.SetSiblingIndex(blocs[0].transform.GetSiblingIndex());
        foreach (var b in blocs)
        {
            b.transform.SetParent(ligne.transform, false);
            var le = b.GetComponent<LayoutElement>() ?? b.AddComponent<LayoutElement>();
            le.minWidth = 0f; le.preferredWidth = 1f; le.flexibleWidth = 1f;
        }
    }

    // Volet Indice (révision) : fenêtre large ; volet Initialiser : largeur d'origine.
    // Le parent ne pilote pas la largeur de la fenêtre (elle vient de sa taille propre) :
    // on la règle directement — changer les LayoutElement n'élargissait que le titre.
    void AppliquerLargeur(bool large)
    {
        float w = large ? LargeurRevision : LargeurFormulaire;
        foreach (var le in new[] { _leFenetre, _leTitre })
            if (le != null) { le.minWidth = w; le.preferredWidth = w; }
        if (_leFenetre != null)
            ((RectTransform)_leFenetre.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
    }

    // Enfant direct de `racine` qui contient `t` (null si `t` n'est pas dessous).
    static GameObject EnfantDirect(Transform racine, Transform t)
    {
        while (t != null && t.parent != racine) t = t.parent;
        return t != null ? t.gameObject : null;
    }

    static string Nombre(float v) => v > 0f ? v.ToString(CultureInfo.InvariantCulture) : "";
    static string Euros(float v) => v.ToString("N2", FacturePdfService.FrCulture) + " €";

    // Date d'un bloc « case + date » (ISO) : "" case décochée, null si illisible.
    static string DateSaisie(Toggle caseACocher, DateInputController date)
    {
        if (!caseACocher.isOn) return "";
        return date.LireDate(out var d) ? d.ToString("yyyy-MM-dd") : null;
    }

    string FranchiseSaisie() => DateSaisie(_franchiseToggle, _franchiseDate);

    /// Un champ qui fonde le loyer a changé depuis l'enregistrement : loyer de départ,
    /// révision oui/non, type. Le reste (périodicité, provisions, franchise…) se modifie
    /// sans réinitialiser — la franchise est comprise dans le 1er palier, elle ne le
    /// déplace pas.
    bool ChangementStructurant()
    {
        if (_loc == null) return false;
        var type = TypeChoisi();
        if (type != _loc.typeRevision) return true;
        float saisi = SaisieNumerique.TryParse(loyerDepart.text, out var v) ? v : 0f;
        return !Mathf.Approximately(saisi, _loc.loyerDepart);
    }

    // Le bouton dit ce qu'il va faire : Initialiser (jamais fait), Réinitialiser (un
    // champ structurant a changé → on repasse par l'indice ou les paliers), Modifier.
    void RefreshBoutonInit()
    {
        if (_btnEnregistrer == null || _loc == null || _volet != Volet.Initialisation) return;
        bool reinit = _loc.LoyerInitialise && ChangementStructurant();
        var t = _btnEnregistrer.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
            t.text = !_loc.LoyerInitialise ? "Initialiser" : reinit ? "Réinitialiser" : "Modifier";
        // Renégociation en cours de bail : le nouveau loyer ne vaut qu'à partir de l'avenant.
        SetGO(_avenantBlockGO, reinit);
    }

    // Habillage : Indice garde le thème (dû → terracotta / sinon prune) ;
    // Initialiser passe en ambre (famille « argent » du loyer).
    void ApplyThemeVolet(bool mod)
    {
        if (!mod) { ApplyTheme(LoyerSummaryUI.EstRevisionDue(_loc)); return; }
        var header = transform.Find("Content/titre")?.GetComponent<Image>();
        if (header != null) header.color = Hex("#A9741C");
        ColorButton(_btnEnregistrer, Hex("#A9741C"));
    }

    // ── Validation de l'écran « Initialiser » ─────────────────────────────────
    // Modifier : les modalités sont enregistrées, fin. Initialiser / Réinitialiser :
    // on repart du loyer de départ, puis l'écran du type choisi (schéma du 30/09) —
    // indice → initialisation de l'indice, paliers → tableau, aucune → la fiche.
    void ValiderInitialisation()
    {
        if (_loc == null) return;
        if (!TryParseLoyer(out float loyer) || loyer <= 0f)
        { UndoToast.Instance?.ShowInfo("Saisissez le loyer de départ annuel (HT)."); return; }

        string franchise = FranchiseSaisie();
        if (franchise == null) { UndoToast.Instance?.ShowInfo("Date de fin de franchise invalide."); return; }
        if (franchise != "")
        {
            var df = DateTime.ParseExact(franchise, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (FacturationSuivi.TryEcheance(_loc.dateDebutBailISO, out var db) && df < db.Date)
            { UndoToast.Instance?.ShowInfo($"La franchise ne peut pas finir avant le début du bail ({db:dd/MM/yyyy})."); return; }
            if (FacturationSuivi.TryEcheance(_loc.dateFinBailISO, out var fb) && df > fb.Date)
            { UndoToast.Instance?.ShowInfo($"La franchise ne peut pas finir après la fin du bail ({fb:dd/MM/yyyy})."); return; }
        }

        bool avecBail = Loyers.DebutPremierBail(_loc, out var debutBail);

        // À décider AVANT d'écrire quoi que ce soit sur le locataire.
        var type = TypeChoisi();
        bool etaitInitialise = _loc.LoyerInitialise;
        bool reinit = !etaitInitialise || ChangementStructurant();

        // Avenant : l'ancien loyer reste dû jusqu'à la veille (prorata de la période).
        string avenant = reinit && etaitInitialise ? DateSaisie(_avenantToggle, _avenantDate) : "";
        if (avenant == null) { UndoToast.Instance?.ShowInfo("Date de l'avenant invalide."); return; }
        if (avenant != "")
        {
            if (avecBail && Iso(avenant) <= debutBail.Date)
            { UndoToast.Instance?.ShowInfo($"L'avenant doit prendre effet après le début du bail ({debutBail:dd/MM/yyyy})."); return; }
            if (type == TypeRevision.Paliers && FacturationSuivi.TryEcheance(_loc.dateFinBailISO, out var finBail) && Iso(avenant) > finBail.Date)
            { UndoToast.Instance?.ShowInfo("Les paliers vont jusqu'à la fin du bail : prolongez d'abord sa date de fin (section Bail)."); return; }
            // Lit le type, les paliers et le loyer encore en place : avant toute écriture.
            Loyers.EnregistrerAvenant(_loc, Iso(avenant));
        }

        EcrireModalites();
        _loc.loyerDepart = loyer;
        _loc.debutFacturationISO = franchise;
        _loc.typeRevision = type;

        if (!reinit)
        {
            _onSaved?.Invoke();
            UndoToast.Instance?.ShowInfo("Modalités du loyer enregistrées");
            gameObject.SetActive(false);
            return;
        }

        // (Ré)initialisation : on repart du loyer de départ. L'historique des références
        // d'indexation est conservé : la rentabilité des années passées en dépend.
        _loc.indiceImmoAuDepart = "";
        _loc.indiceImmoActuel = "";
        _loc.loyerAnnuelPrecedent = 0f;
        _loc.loyerAnnuel = loyer;
        _onSaved?.Invoke();

        switch (type)
        {
            case TypeRevision.Aucune:
                UndoToast.Instance?.ShowInfo("Loyer initialisé, sans révision.");
                gameObject.SetActive(false);
                break;
            case TypeRevision.Paliers:
                PaliersPanel.Open(_loc, _onSaved, this);   // avant de masquer : il cherche le canvas depuis ce panneau
                gameObject.SetActive(false);
                break;
            default:
                Open(_loc, _onSaved, Volet.Indice);   // l'indice vient d'être vidé : mode initialisation
                break;
        }
    }

    static DateTime Iso(string iso) => DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // Modalités de facturation (périodicité, provisions, listes, jour, mois, reprise).
    void EcrireModalites()
    {
        _loc.periodiciteLoyer = (Periodicite)periodiciteDropdown.value;

        _loc.provisionPourCharges = toggleProvisions.isOn;
        float prov = SaisieNumerique.Parse(provisionValue.text);
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
        AppliquerListes();

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
            var b = UIFactory.Button(wrapGO, MoisCourts[i], UITheme.Carte, UITheme.TextePrincipal, 28, UITheme.Role.Action, false);
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
        // Champ obligatoire : un champ vide reste une erreur (SaisieNumerique
        // traite le vide comme un 0 legitime, ce qui n'a pas de sens pour un loyer).
        loyer = 0f;
        bool ok = !string.IsNullOrWhiteSpace(loyerDepart.text)
                  && SaisieNumerique.TryParse(loyerDepart.text, out loyer);
        if (!ok) statusText.text = "Loyer de départ invalide";
        return ok;
    }
}