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
    Button chipICC;   // revue 16c (D7b-20) : quatrième puce, clonée de la troisième à la volée (la scène n'en a que trois)

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
    // Revue 17 (avenant de périodicité, lot 15) : la date d'effet d'un changement de périodicité, dans le même bloc que l'avenant de
    // loyer (case cachée et cochée : la date est demandée, pas proposée) ; `_avenantPerAuto` : la dernière date proposée, que le
    // champ suit tant qu'elle n'a pas été retouchée.
    Toggle _avenantPerToggle; DateInputController _avenantPerDate; GameObject _avenantPerBlockGO; TMP_Text _avenantPerAide; string _avenantPerAuto;
    // Loyer selon le CA : % et bornes (écran Initialiser), CA de l'année (révision).
    readonly List<GameObject> _caChamps = new List<GameObject>();   // min, max, % (clones de « Loyer de départ »)
    GameObject _caRevisionGO, _caResultGO, _compareStripGO, _loyerPrecedentGO;
    TMP_InputField _caPct, _caMin, _caMax, _caMontant, _caPrecedent, _trimPrecedent;
    TMP_Text _caTitre, _caTitrePrecedent, _titreLoyerCalcule, _lPct, _rPct, _rMin, _rMax, _rRegle, _rAncien, _rProchaine, _rIndice;
    int _caAnnee;   // année du CA affichée dans le champ (année de révision - 1)
    // Largeur de la fenêtre : d'origine pour Initialiser, élargie pour la révision.
    const float LargeurFormulaire = 560f, LargeurRevision = 900f;
    LayoutElement _leFenetre, _leTitre;
    ScrollRect _scroll; LayoutElement _leScroll; RectTransform _fenetre;   // BI2 (02/11) : corps défilant, fenêtre plafonnée à l'écran
    RectTransform _pied;         // Revue 17 (B-13, lot G) : la rangée des boutons, sortie du défilement
    TMP_Text _libelleProvisions; // Revue visuelle (V-19) : le libellé à côté de la case « Provisions », comme les autres cases

    // ── Champs facturation ajoutés par code (jour de demande, mois, régularisation) ──
    TMP_InputField _jourDemande, _dEnvoi, _dImpaye, _dAutres;
    Toggle _delaisToggle; GameObject _delaisBlockGO;   // délais de facturation propres au locataire (01/11)
    DateInputController _dateRegulCtrl;
    GameObject _jourLabelGO, _moisLabelGO, _moisWrapGO, _regulBlockGO, _repriseBlockGO;
    // TVA sur le loyer (02/11) : case « soumis » et taux du bail. Le bail garde son propre choix dès qu'on y a touché (BK4) ;
    // sinon il suit l'entreprise (Réglages).
    Toggle _tvaToggle; TMP_InputField _tvaTaux; GameObject _tvaLabelGO; bool _tvaTouche;
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
    // Revue 17 (avenant de périodicité, lot 16) : la dernière provision proposée dans chaque champ (clé : l'id de la liste pour sa
    // ligne, "" la générale ; ProvCase : le champ à côté de la case « Provisions »). Un champ qui la porte encore suit la
    // périodicité choisie (ConvertirProvisions) ; un champ retouché à la main reste tel quel.
    readonly Dictionary<string, string> _provAuto = new Dictionary<string, string>();
    const string ProvCase = "~case";

    // Avertissement inline placé SOUS le « Trimestre de révision » (au lieu du bas
    // de la modale) + désactivation du bouton « Réviser » quand le trimestre est mauvais.
    TMP_Text _trimWarn;

    // Retour de BW5 (12/11) : l'aperçu du loyer révisé, sous le trimestre, avant « Réviser » (qui ferme le volet : le résultat
    // ne se voyait que dans le message du bas). Observations INSEE lues une fois par type d'indice.
    TMP_Text _apercu;
    List<(string periode, float valeur)> _obsApercu;
    IndiceImmo _obsApercuType;
    bool _obsApercuEnCours;
    // Retour de BW5 (12/11, décision) : la révision vient d'être enregistrée dans ce volet, resté ouvert : bouton grisé jusqu'à la
    // réouverture (un second clic repoussait la prochaine révision d'un an de plus, revue 16c D7b-01).
    bool _reviseeIci;
    const string LibelleReviser = "Réviser le loyer";

    // Blocs de la scène résolus une fois (pour l'affichage par volet).
    Transform _body;
    GameObject _periodiciteGO, _chipsIndiceGO, _loyerDepartGO, _trimRefGO,
               _dateRevGO, _modeToggleGO, _provBlockGO, _informationGO;
    /// Revue 17 (D7b-18, lot D) : libellés du menu de périodicité, dans l'ordre de l'énumération Periodicite (l'indice du menu
    /// reste la valeur : (Periodicite)periodiciteDropdown.value). Avant : les noms de l'énumération (« BiAnnuel »).
    public static readonly string[] LibellesPeriodicite = { "Mensuel", "Trimestriel", "Semestriel (bi-annuel)", "Annuel" };
    static readonly string[] MoisCourts =
        { "Jan", "Fév", "Mar", "Avr", "Mai", "Juin", "Juil", "Août", "Sep", "Oct", "Nov", "Déc" };

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    // ── Ouverture ─────────────────────────────────────────────────────────────

    public void Open(Locataire loc, Action onSaved) => Open(loc, onSaved, Volet.Indice);

    public void Open(Locataire loc, Action onSaved, Volet volet) =>   // sur le cloud : locataire réservé (étape 4b)
        EcritureCloud.Reserver(new[] { ("locataire", loc?.id, loc?.Name) }, "loyer", () => OuvrirLibre(loc, onSaved, volet),
            () => this != null && gameObject.activeSelf && _loc == loc, () => { if (this != null) gameObject.SetActive(false); });

    void OuvrirLibre(Locataire loc, Action onSaved, Volet volet)
    {
        _reviseeIci = false;   // retour de BW5 (12/11)
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

        // Revue 17 (lot 16) : vidé AVANT de poser la périodicité : l'écouteur de l'ouverture précédente, encore branché, se déclenche
        // ici si elle diffère, et ne doit rien convertir (les champs de provision sont rechargés plus bas, avec leur proposition).
        _provAuto.Clear();
        periodiciteDropdown.ClearOptions();
        periodiciteDropdown.AddOptions(new List<string>(LibellesPeriodicite));   // Revue 17 (D7b-18, lot D)
        periodiciteDropdown.value = (int)loc.periodiciteLoyer;

        // Champs facturation (jour de demande, mois facturés, date de régularisation)
        EnsureExtraBuilt();
        if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;   // BI2 (02/11) : la fenêtre s'ouvre en haut de son contenu
        _jourDemande.text = loc.jourDemandeLoyer > 0 ? loc.jourDemandeLoyer.ToString() : "";
        _tvaToggle?.SetIsOnWithoutNotify(Tva.Soumis(loc));   // 02/11
        if (_tvaTaux != null) _tvaTaux.text = (loc.tauxTvaLoyer > 0f ? loc.tauxTvaLoyer : TauxEntreprise()).ToString("0.##", FacturePdfService.FrCulture);
        _tvaTouche = false;   // le chargement n'est pas un choix
        if (_delaisToggle != null)
        {
            _delaisToggle.SetIsOnWithoutNotify(!loc.delaisPropres);
            _dEnvoi.text  = loc.delaisPropres ? loc.envoiAvantJours.ToString()     : "";
            _dImpaye.text = loc.delaisPropres ? loc.impayeApresJours.ToString()    : "";
            _dAutres.text = loc.delaisPropres ? loc.echeanceAutresJours.ToString() : "";
            RefreshDelaisVisibility();
        }
        if (System.DateTime.TryParse(loc.dateRegularisationChargeISO, out var drDate))
            _dateRegulCtrl.ApplyDate(drDate);
        else { _dateRegulCtrl.dayInput.text = ""; _dateRegulCtrl.monthInput.text = ""; _dateRegulCtrl.yearInput.text = ""; }
        _dateRegulCtrl.ModifyDate();
        RebuildListes(loc);
        for (int i = 0; i < 12; i++) _moisState[i] = false;
        if (loc.moisFacturationLoyer != null)
            foreach (int m in loc.moisFacturationLoyer)
                if (m >= 1 && m <= 12) _moisState[m - 1] = true;
        CocherMoisStandard();   // retour BN11 (04/11) : aucun mois coché → ceux que le suivi prend d'office, cochés
        for (int i = 0; i < 12; i++) RefreshChip(i);
        RefreshRepriseOptions();

        periodiciteDropdown.onValueChanged.RemoveAllListeners();
        periodiciteDropdown.onValueChanged.AddListener(_ => OnPeriodiciteChanged());
        RefreshMoisVisibility();

        // Champs bail. Le loyer de départ se saisit dans « Initialiser » ; il change
        // le libellé du bouton (Réinitialiser) dès qu'il diffère de celui enregistré.
        loyerDepart.text = Nombre(loc.loyerDepart);   // Revue 17 (D7b-10, lot D) : format fixe (float.ToString() : 7 chiffres sous Mono)
        loyerDepart.onValueChanged.RemoveAllListeners();
        loyerDepart.onValueChanged.AddListener(_ => RefreshBoutonInit());
        trimestreDepart.Init();
        if (!string.IsNullOrEmpty(loc.trimestreDeRevision))
            trimestreDepart.SetTrimestre(loc.trimestreDeRevision);
        trimestreDepart.CanModify();
        trimestreDepart.OnTrimestreChanged -= OnTrimDepartChanged;   // retour de BW5 (12/11) : l'aperçu suit aussi la référence
        trimestreDepart.OnTrimestreChanged += OnTrimDepartChanged;

        // Date de révision : toujours éditable, sauvegardée à chaque modification
        dateDeRevision.ApplyDate(Loyers.PremiereRevisionParDefaut(loc));   // revue 16c (D7b-03) : début du bail + 1 an par défaut
        dateDeRevision.ModifyDate();
        dateDeRevision.OnModify.RemoveAllListeners();
        dateDeRevision.OnModify.AddListener(OnDateRevisionModifiee);

        // Trimestre de révision : toujours visible, pré-rempli (modifiable)
        trimestreVoulu.Init();
        trimestreVoulu.CanModify();
        trimestreVoulu.gameObject.SetActive(true);
        // Revue 17 (D7b-01, lot D) : le trimestre de la dernière révision + 1 an, sinon celui de la référence + 1 an. Avant : l'année
        // précédant aujourd'hui, qui retombait sur la référence pour un bail signé l'année de sa référence. Lecture sûre : un
        // trimestre au format ancien (« T1 », « 20261 ») ne propose rien, sans empêcher l'ouverture.
        string propose = InseeIndiceService.TrimestreSuivant(loc.trimestreDeRevision, loc.indiceImmoActuel);
        if (propose != "") trimestreVoulu.SetTrimestre(propose);
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
        _provAuto[ProvCase] = provisionValue.text;   // Revue 17 (lot 16) : la valeur enregistrée, convertie si la périodicité change à l'écran
        toggleProvisions.onValueChanged.AddListener(_ => RefreshRegulVisibility());
        RefreshRegulVisibility();

        // Type de révision + franchise (écran « Initialiser »).
        _revisionToggle.SetIsOnWithoutNotify(loc.typeRevision != TypeRevision.Aucune);
        _typeRevDD.SetOptions(TypesLibelles, TypesIds,
            loc.typeRevision == TypeRevision.Paliers ? "Paliers"
            : loc.typeRevision == TypeRevision.ChiffreAffaires ? "CA" : "Indice");
        ChargerCaseDate(_franchiseToggle, _franchiseDate, loc.debutFacturationISO);
        ChargerCaseDate(_avenantToggle, _avenantDate, "");   // un avenant se saisit à chaque réinitialisation
        // Revue 17 (avenant de périodicité, lot 15) : la date d'effet aussi ; le bloc ne revient que si la périodicité change à l'écran.
        ChargerCaseDate(_avenantPerToggle, _avenantPerDate, "");
        _avenantPerToggle.SetIsOnWithoutNotify(true);
        _avenantPerAuto = null;
        SetGO(_avenantPerBlockGO, false);
        RefreshInitVisibility();

        // Infos
        txtIndiceDepart.text = string.IsNullOrEmpty(loc.indiceImmoAuDepart) ? "—" : loc.indiceImmoAuDepart;
        txtIndiceActuel.text = string.IsNullOrEmpty(loc.indiceImmoActuel) ? "—" : loc.indiceImmoActuel;
        txtLoyerCalcule.text = $"{Montants.F(loc.loyerAnnuel, "N2")} €";
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
                ? $"{Montants.F(loc.loyerAnnuelPrecedent, "N2")} €" : "—";

        // Actions
        btnInitialiser.onClick.RemoveAllListeners();
        btnInitialiser.onClick.AddListener(() => StartCoroutine(Initialiser()));
        btnReviser.onClick.RemoveAllListeners();
        btnReviser.onClick.AddListener(() => StartCoroutine(Reviser()));
        LibelleBouton(LibelleReviser);   // retour de BW5 (12/11, décision) : le prefab disait « Calculer la révision »
        btnFermer.onClick.RemoveAllListeners();
        btnFermer.onClick.AddListener(() => gameObject.SetActive(false));

        // Mode imposé par l'état de l'indice : l'initialiser d'abord, le réviser ensuite.
        // (La bascule manuelle du prefab est masquée dans ApplyVolet.)
        SetMode(!loc.IndiceInitialise);
        btnInitialiser.interactable = true;   // Revue 17 (D7b-14, lot D) : fermé pendant la récupération INSEE, il restait grisé

        // Applique le volet demandé (Indice par défaut / Initialiser).
        ApplyVolet(volet);
        RefreshCARevision(true);
        // Référence : le trimestre (et l'indice) de la dernière révision ; vide la 1re fois.
        if (_trimPrecedent != null) _trimPrecedent.text = DerniereRevision(loc.indiceImmoActuel);
        if (loc.typeRevision == TypeRevision.ChiffreAffaires && loc.IndiceInitialise) ViderResultatCA();
        RafraichirApercu();   // retour de BW5 (12/11)
    }

    // ── Thème dynamique : couleur de la modale = état de la révision ──────────
    // Due -> terracotta (comme le bouton Réviser en alerte), sinon -> prune.
    private static Color s_accent      = UITheme.Hex("#8E3B5A");
    private static Color s_accentDark  = UITheme.Hex("#5E2438");
    private static Color s_accentLight = UITheme.Hex("#F6E5EB");

    private void ApplyTheme(bool due)
    {
        s_accent      = UITheme.Hex(due ? "#D85A30" : "#8E3B5A");
        s_accentDark  = UITheme.Hex(due ? "#712B13" : "#5E2438");
        s_accentLight = UITheme.Hex(due ? "#FAECE7" : "#F6E5EB");

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
        if (chipICC == null && chipILAT != null)
        {
            chipICC = Instantiate(chipILAT, chipILAT.transform.parent);
            chipICC.name = "chipICC";
            var lbl = chipICC.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = "ICC";
        }
        if (chipICC != null) { chipICC.onClick.RemoveAllListeners(); chipICC.onClick.AddListener(() => SetIndice(3)); }
    }

    private void SetIndice(int i)
    {
        if (indiceDropdown != null) indiceDropdown.value = i;
        StyleChip(chipILC,  i == 0);
        StyleChip(chipIRL,  i == 1);
        StyleChip(chipILAT, i == 2);
        StyleChip(chipICC, i == 3);
        RafraichirApercu();   // retour de BW5 (12/11) : l'aperçu suit l'indice choisi
    }

    private static void StyleChip(Button b, bool active)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null) img.color = active ? s_accentLight : UITheme.Carte;
        var txt = b.GetComponentInChildren<TMP_Text>(true);
        if (txt != null) txt.color = active ? s_accentDark : UITheme.Hex("#888780");
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
        if (txt != null) txt.color = active ? Color.white : UITheme.TexteSecondaire;
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
            _caMontant.text = Loyers.CA(_loc, annee) is float v ? Nombre(v) : "";   // Revue 17 (D7b-10, lot D) : « 1.234568E+07 »
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
        // Revue 17 (D7b-06, lot D) : refus et erreurs au centre ; le cadre du bas garde l'attente et le résultat.
        if (!TryParseLoyer(out float loyer)) { ConfirmDialog.Erreur("Loyer de départ invalide."); yield break; }
        if (DatesIllisibles() is string refusDate) { ConfirmDialog.Erreur(refusDate); yield break; }   // BJ4 (02/11)

        // Selon le CA : loyer min / max et % obligatoires, le max au moins égal au min.
        bool selonCA = _loc.typeRevision == TypeRevision.ChiffreAffaires;
        float pct = 0f, min = 0f, max = 0f;
        if (selonCA)
        {
            SaisieNumerique.TryParse(_caPct.text, out pct);
            SaisieNumerique.TryParse(_caMin.text, out min);
            SaisieNumerique.TryParse(_caMax.text, out max);
            if (min <= 0f || max < min)
            { ConfirmDialog.Erreur("Saisissez un loyer minimum, et un maximum au moins égal au minimum."); yield break; }
            if (pct <= 0f || pct > 100f)
            { ConfirmDialog.Erreur("Saisissez le pourcentage du chiffre d'affaires (entre 0 et 100)."); yield break; }
        }

        btnInitialiser.interactable = false;
        statusText.text = "Récupération de l'indice de référence…";

        var type = (IndiceImmo)indiceDropdown.value;
        List<(string, float)> observations = null;
        string erreur = null;

        yield return InseeIndiceService.FetchObservations(type,
            obs => observations = obs, err => erreur = err);

        btnInitialiser.interactable = true;
        statusText.text = "";
        if (observations == null) { ConfirmDialog.Erreur(erreur); yield break; }   // déjà en français (InseeIndiceService.MessageEchec)

        // Indice de départ — fallback autorisé (trimestre pas encore publié)
        string periode = InseeIndiceService.Normalize(trimestreDepart.TrimestreValue);
        var obsRef = InseeIndiceService.TrouveAvecFallback(observations, periode);
        if (string.IsNullOrEmpty(obsRef.periode))
        { ConfirmDialog.Erreur($"Indice INSEE introuvable pour {periode}."); yield break; }

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
                || FacturationSuivi.TryEcheance(_loc.dateDebutBailISO, out dbInit) ? dbInit : DateTime.Now,
            (IndiceImmo)indiceDropdown.value, obsRef.periode, obsRef.valeur, loyer);

        txtIndiceDepart.text = _loc.indiceImmoAuDepart;
        txtIndiceActuel.text = "—";
        txtLoyerCalcule.text = $"{Montants.F(loyer, "N2")} €";
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
        // Revue 17 (D7b-10, lot D) : la base enregistrée, pas le texte du champ (non modifiable ici) : le réaffichage arrondissait à
        // 7 chiffres, et « Réviser » réécrivait cette base arrondie sur la fiche.
        float loyer = _loc.loyerDepart;
        if (loyer <= 0f) { ConfirmDialog.Erreur("Loyer de départ invalide."); yield break; }   // Revue 17 (D7b-06, lot D) : au centre
        if (DatesIllisibles() is string refusDate) { ConfirmDialog.Erreur(refusDate); yield break; }   // BJ4 (02/11)
        // Revue 17 (D7b-01, lot D) : un trimestre qui n'est pas postérieur à la référence et à la dernière révision est refusé avant
        // l'appel réseau, au centre et sous le champ. Avant : seul « avant la référence » l'était ; un trimestre égal passait (loyer
        // inchangé, prochaine révision repoussée d'un an).
        if (InseeIndiceService.RefusTrimestre(trimestreVoulu.TrimestreValue, trimestreDepart.TrimestreValue, _loc.indiceImmoActuel) is string refusTrim)
        { AfficheTrimWarn(refusTrim); ConfirmDialog.Erreur(refusTrim); yield break; }

        btnReviser.interactable = false;
        statusText.text = "Récupération des données INSEE…";

        var type = (IndiceImmo)indiceDropdown.value;
        List<(string, float)> observations = null;
        string erreur = null;

        yield return InseeIndiceService.FetchObservations(type,
            obs => observations = obs, err => erreur = err);

        btnReviser.interactable = true;
        statusText.text = "";   // Revue 17 (D7b-06, lot D) : refus et erreurs au centre, le cadre du bas garde le résultat
        if (observations == null) { ConfirmDialog.Erreur(erreur); yield break; }   // déjà en français (InseeIndiceService.MessageEchec)

        // Indice de départ — fallback autorisé
        string periodeDepart = InseeIndiceService.Normalize(trimestreDepart.TrimestreValue);
        var obsDepart = InseeIndiceService.TrouveAvecFallback(observations, periodeDepart);
        if (string.IsNullOrEmpty(obsDepart.periode))
        { ConfirmDialog.Erreur($"Indice de départ introuvable pour {periodeDepart}."); yield break; }

        // Le fallback peut reculer de plusieurs années sans rien dire : une base plus
        // ancienne que demandée gonfle le ratio et donc le loyer révisé, sur un écran
        // parfaitement normal. On le signale explicitement.
        if (!string.Equals(obsDepart.periode, periodeDepart, StringComparison.OrdinalIgnoreCase))
            AfficheTrimWarn($"Indice de départ {periodeDepart} non publié — calcul basé sur {obsDepart.periode}.");

        // Un indice de départ nul rendrait la division infinie : le loyer révisé
        // deviendrait Infinity/NaN et serait enregistré tel quel sur le locataire.
        if (obsDepart.valeur <= 0f)
        {
            ConfirmDialog.Erreur($"Indice de départ invalide ({obsDepart.periode} = {obsDepart.valeur}) — révision impossible.");
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
        // Revue 16c (D7b-03) puis Revue 17 (D7b-01, lot D) : le trimestre voulu est contrôlé avant l'appel réseau (RefusTrimestre).
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
                ConfirmDialog.Erreur($"La date de révision porte sur le CA {anneeCA} : vérifiez le montant, puis recalculez.");
                yield break;
            }
            if (!SaisieNumerique.TryParse(_caMontant.text, out caHT) || caHT <= 0f)
            { ConfirmDialog.Erreur($"Saisissez le CA HT {anneeCA} du locataire."); yield break; }
            if (!SaisieNumerique.TryParse(_caPct.text, out float pctSaisi) || pctSaisi <= 0f || pctSaisi > 100f)
            { ConfirmDialog.Erreur("Saisissez la part du CA (entre 0 et 100 %)."); yield break; }
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
                ConfirmDialog.Erreur($"Le locataire n'était pas dans les lieux en {anneeCA} : aucun CA à prendre en compte.");
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

        // L'ancien loyer reste dû jusqu'à la veille de la révision : les périodes passées (facture, rentabilité)
        // gardent leur montant (voir Loyers.LoyerAnnuelA). Pour les deux modes : en mode indice, une facture d'une
        // période passée corrigée après la révision prenait le nouveau loyer pour toute la période (revue 16c, D7b-02).
        // Revue 17 (D7b-03, lot D) : la date de la référence, lue AVANT l'avenant (qui pose debutConditionsISO au jour de la
        // révision) : la vraie base remplace la base de repli dès le début des conditions, pas à partir de la révision.
        var dateReference = FacturationSuivi.TryEcheance(_loc.debutConditionsISO, out var dbRev)
            || FacturationSuivi.TryEcheance(_loc.dateDebutBailISO, out dbRev) ? dbRev : d;
        string precedentAvant = IndicePrecedent(_loc);   // retour de BW5 (12/11) : lu avant que l'indice actuel ne change
        Loyers.EnregistrerAvenant(_loc, d.Date);
        if (selonCA)
        {
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
        // Revue 16c (D7b-08) : datée comme à l'initialisation (début des conditions ou du bail), pas du jour de la révision :
        // la vraie base remplace ainsi la base de repli pour TOUTES les années (Segments : à date égale, le dernier l'emporte).
        LoyerHistoryService.EnregistrerReference(_loc, dateReference,
            (IndiceImmo)indiceDropdown.value, obsDepart.periode, obsDepart.valeur, loyer);

        txtIndiceDepart.text = precedentAvant;   // retour de BW5 (12/11) : l'indice de la dernière révision (sinon la référence)
        txtIndiceActuel.text = _loc.indiceImmoActuel;
        txtLoyerCalcule.text = $"{Montants.F(loyerRevise, "N2")} €";
        if (txtVariation != null) txtVariation.text = VariationIndice(precedentAvant, obsActuel.valeur);
        if (txtLoyerPrecedent != null)
            txtLoyerPrecedent.text = $"{Montants.F(_loc.loyerAnnuelPrecedent, "N2")} €";
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
            statusText.text = $"{InseeIndiceService.GetLabel(type)} — loyer révisé : {Montants.F(loyerRevise, "N2")} € " +
                              $"(prochaine révision {_loc.MoisDeRevision:dd/MM/yyyy})";

        _onSaved?.Invoke();
        // Revue 16c (D7b-01) : le panneau se ferme, comme après « Initialiser » : un second clic sur « Réviser » repoussait
        // la prochaine révision d'un an de plus et écrasait le loyer précédent.
        // Revue 17 (D7b-01, lot D) : la variation du loyer est dite (le panneau se ferme : « +0,0 % » ne se voyait plus).
        UndoToast.Instance?.ShowInfo($"Loyer révisé : {Montants.F(loyerRevise, "N2")} €/an{VariationLoyer(_loc.loyerAnnuelPrecedent, loyerRevise)} — prochaine révision le {_loc.MoisDeRevision:dd/MM/yyyy}.");
        // Retour de BW5 (12/11, décision) : le volet reste ouvert sur le résultat enregistré (cadre du bas, déjà rempli ci-dessus) ;
        // le bouton est grisé jusqu'à la réouverture, au lieu de fermer le volet (on ne voyait le résultat qu'en le rouvrant).
        _reviseeIci = true;
        btnReviser.interactable = false;
        LibelleBouton("Révision enregistrée");
        RafraichirApercu();
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
        t.color = UITheme.Erreur;                        // rouge lisible sur fond clair
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
            if (btnReviser != null) btnReviser.interactable = !_reviseeIci;   // retour de BW5 : grisé après la révision
        }
    }

    private void OnTrimVouluChanged(string _) { ValiderTrimestreVoulu(); RafraichirApercu(); }
    private void OnTrimDepartChanged(string _) => RafraichirApercu();

    /// Retour de BW5 (12/11) : le calcul que « Réviser le loyer » fera en mode indice (même base, indice de départ avec repli,
    /// indice voulu exact). Faux si un indice manque (`dep` rempli et `act` vide : l'indice voulu n'est pas encore publié).
    /// Pur : testé (LotDRevisionTests).
    public static bool CalculApercu(List<(string periode, float valeur)> obs, string periodeDepart, string periodeVoulue, float loyerBase,
                                    out (string periode, float valeur) dep, out (string periode, float valeur) act, out float revise)
    {
        dep = ("", 0f); act = ("", 0f); revise = 0f;
        if (obs == null || loyerBase <= 0f) return false;
        dep = InseeIndiceService.TrouveAvecFallback(obs, InseeIndiceService.Normalize(periodeDepart));
        if (string.IsNullOrEmpty(dep.periode) || dep.valeur <= 0f) return false;
        act = InseeIndiceService.TrouveExact(obs, InseeIndiceService.Normalize(periodeVoulue));
        if (string.IsNullOrEmpty(act.periode)) return false;
        revise = loyerBase * act.valeur / dep.valeur;
        return true;
    }

    /// La ligne sous les trimestres : le calcul s'affiche dans le cadre « Résultat de la révision » (retour de BW5 : c'est là qu'on
    /// le cherche), ou l'indice voulu n'est pas encore publié ; null s'il n'y a rien à dire.
    public static string Apercu(List<(string periode, float valeur)> obs, string periodeDepart, string periodeVoulue, float loyerBase)
    {
        if (CalculApercu(obs, periodeDepart, periodeVoulue, loyerBase, out var dep, out _, out _))
            return "Aperçu dans le cadre du bas, pas encore enregistré : « Réviser le loyer » enregistre ce loyer.";
        return string.IsNullOrEmpty(dep.periode) ? null
            : $"Aperçu : l'indice {InseeIndiceService.Normalize(periodeVoulue)} n'est pas encore publié.";
    }

    // Montre ou cache l'aperçu : mode indice, indice initialisé, trimestre voulu commencé et accepté (sinon l'avertissement le dit).
    // Le cadre « Résultat de la révision » prend les valeurs calculées ; sans aperçu, il revient à l'état enregistré.
    void RafraichirApercu()
    {
        if (_loc == null || trimestreVoulu == null || trimestreDepart == null) return;
        if (_reviseeIci)   // retour de BW5 (12/11) : le cadre montre le résultat enregistré, rien à recalculer
        {
            EnsureApercu();
            if (_apercu == null) return;
            _apercu.text = $"Révision enregistrée : prochaine révision le {_loc.MoisDeRevision:dd/MM/yyyy}. « Fermer » pour revenir à la fiche.";
            _apercu.gameObject.SetActive(true);
            return;
        }
        bool modeIndice = _loc.typeRevision == TypeRevision.Indice && _loc.IndiceInitialise;
        string voulu = trimestreVoulu.TrimestreValue;
        bool montrer = modeIndice && TrimestreDejaCommence(voulu)
            && InseeIndiceService.RefusTrimestre(voulu, trimestreDepart.TrimestreValue, _loc.indiceImmoActuel) == null;
        var type = indiceDropdown != null ? (IndiceImmo)indiceDropdown.value : _loc.indiceTypeImmo;
        if (montrer && (_obsApercu == null || _obsApercuType != type))
        {
            if (!_obsApercuEnCours && isActiveAndEnabled) StartCoroutine(ChargerApercu(type));
            montrer = false;   // reviendra à la fin de la lecture
        }
        string texte = montrer ? Apercu(_obsApercu, trimestreDepart.TrimestreValue, voulu, _loc.loyerDepart) : null;
        if (montrer && CalculApercu(_obsApercu, trimestreDepart.TrimestreValue, voulu, _loc.loyerDepart, out var dep, out var act, out float revise))
        {
            string precedent = IndicePrecedent(_loc);   // comme « Réviser le loyer » l'écrira
            AfficherResultat(precedent, FormatIndice(act.valeur, act.periode), $"{Montants.F(revise, "N2")} €",
                             VariationIndice(precedent, act.valeur), $"{Montants.F(_loc.loyerAnnuel, "N2")} €");
        }
        else if (modeIndice)
            AfficherResultat(IndicePrecedent(_loc), "—",   // pas d'aperçu : l'indice en vigueur, rien de nouveau
                             $"{Montants.F(_loc.loyerAnnuel, "N2")} €", "—",
                             _loc.loyerAnnuelPrecedent > 0f ? $"{Montants.F(_loc.loyerAnnuelPrecedent, "N2")} €" : "—");
        EnsureApercu();
        if (_apercu == null) return;
        _apercu.text = texte ?? "";
        _apercu.gameObject.SetActive(texte != null);
    }

    void LibelleBouton(string texte)
    {
        var t = btnReviser != null ? btnReviser.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = texte;
    }

    /// Retour de BW5 (12/11) : l'« Indice précédent » du cadre : celui de la dernière révision, sinon l'indice de référence.
    public static string IndicePrecedent(Locataire l)
        => !string.IsNullOrEmpty(l.indiceImmoActuel) && l.indiceImmoActuel != "—" ? l.indiceImmoActuel
         : string.IsNullOrEmpty(l.indiceImmoAuDepart) ? "—" : l.indiceImmoAuDepart;

    /// La variation du nouvel indice depuis l'indice précédent (« 135.87  (2025-Q1) ») : c'est aussi celle du loyer.
    public static string VariationIndice(string precedent, float nouveau)
    {
        float p = LoyerHistoryService.IndiceValeur(precedent);
        if (p <= 0f) return "—";
        float v = (nouveau / p - 1f) * 100f;
        return $"{(v >= 0 ? "+" : "")}{v.ToString("0.0", FacturePdfService.FrCulture)} %";
    }

    void AfficherResultat(string indiceDepart, string indiceActuel, string loyer, string variation, string loyerPrecedent)
    {
        if (txtIndiceDepart != null) txtIndiceDepart.text = indiceDepart;
        if (txtIndiceActuel != null) txtIndiceActuel.text = indiceActuel;
        if (txtLoyerCalcule != null) txtLoyerCalcule.text = loyer;
        if (txtVariation != null) txtVariation.text = variation;
        if (txtLoyerPrecedent != null) txtLoyerPrecedent.text = loyerPrecedent;
    }

    IEnumerator ChargerApercu(IndiceImmo type)
    {
        _obsApercuEnCours = true;
        List<(string periode, float valeur)> obs = null;
        yield return InseeIndiceService.FetchObservations(type, o => obs = o, _ => { });
        _obsApercuEnCours = false;
        if (obs == null) yield break;   // INSEE injoignable : pas d'aperçu ; « Réviser le loyer » dira l'erreur au centre
        _obsApercu = obs; _obsApercuType = type;
        RafraichirApercu();
    }

    // Le texte de l'aperçu, construit une fois, juste sous l'avertissement du trimestre (même place, même police).
    void EnsureApercu()
    {
        if (_apercu != null || trimestreVoulu == null) return;
        // Sous l'avertissement, dans la section : le trimestre voulu est dans une ligne horizontale (LigneTrimestres) ; y mettre
        // l'aperçu le plaçait par-dessus « Dernière révision » et « Trimestre de révision » (vu en Play, BW5).
        EnsureTrimWarn();
        var parent = _trimWarn != null ? _trimWarn.transform.parent : trimestreVoulu.transform.parent;
        if (parent == null) return;
        var go = new GameObject("ApercuRevision", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TMPro.TextMeshProUGUI>();
        var refT = trimestreVoulu.GetComponentInChildren<TMP_Text>(true);
        if (refT != null) t.font = refT.font;
        t.fontSize = UITheme.Role.Aide;
        t.color = UITheme.TextePrincipal;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        t.alignment = TextAlignmentOptions.TopLeft;
        go.AddComponent<LayoutElement>().minHeight = 0;
        if (_trimWarn != null) go.transform.SetSiblingIndex(_trimWarn.transform.GetSiblingIndex() + 1); else go.transform.SetAsLastSibling();
        go.SetActive(false);
        _apercu = t;
    }

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

        var l1 = UIFactory.Text(content, "Loyer demandé le … (jour du mois)", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        l1.transform.SetSiblingIndex(idx++);
        _jourLabelGO = l1.gameObject;
        _jourDemande = UIFactory.Input(content, "1");
        _jourDemande.contentType = TMP_InputField.ContentType.IntegerNumber;
        _jourDemande.transform.SetSiblingIndex(idx++);
        _jourDemande.onValueChanged.AddListener(_ => RefreshAvenantPeriodicite());   // Revue 17 (lot 15) : la date d'effet proposée est un jour de demande

        // TVA sur le loyer (02/11) : case « soumis » (cochée d'après l'entreprise) et taux du bail, à défaut celui de l'entreprise.
        _tvaToggle = UIFactory.Toggle(content, "TVA sur le loyer (option du bailleur)", true);
        _tvaToggle.transform.SetSiblingIndex(idx++);
        _tvaToggle.onValueChanged.AddListener(_ => { _tvaTouche = true; RefreshTvaVisibility(_volet == Volet.Initialisation); });
        _tvaLabelGO = UIFactory.Text(content, "Taux de TVA du bail (%)", UITheme.Role.Libelle, UITheme.TexteSecondaire).gameObject;
        _tvaLabelGO.transform.SetSiblingIndex(idx++);
        _tvaTaux = UIFactory.Input(content, Tva.TauxDefaut.ToString("0.##", FacturePdfService.FrCulture));
        SaisieNumerique.Montant(_tvaTaux);   // retour BN2 (04/11) : la virgule aussi
        _tvaTaux.onValueChanged.AddListener(_ => _tvaTouche = true);
        _tvaTaux.transform.SetSiblingIndex(idx++);

        // Délais de facturation (01/11) : ceux de l'entreprise (Réglages › Facturation › Calendrier des factures), ou les siens.
        _delaisToggle = UIFactory.Toggle(content, "Délais des factures : ceux de l'entreprise (Réglages › Facturation)", true);
        _delaisToggle.transform.SetSiblingIndex(idx++);
        _delaisToggle.onValueChanged.AddListener(_ => RefreshDelaisVisibility());
        _delaisBlockGO = UIFactory.VBox(content, 4, 0, 0, 0, 0, "Delais").gameObject;
        _delaisBlockGO.transform.SetSiblingIndex(idx++);
        UIFactory.Text(_delaisBlockGO.transform, "Ces délais remplacent ceux de l'entreprise pour ce locataire seulement.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        _dEnvoi  = ChampJours(_delaisBlockGO.transform, "La facture de loyer part … jours avant l'échéance");
        _dImpaye = ChampJours(_delaisBlockGO.transform, "Sans règlement, la facture passe « Impayé » … jours après l'échéance");
        _dAutres = ChampJours(_delaisBlockGO.transform, "Les autres factures (charges, régularisation, dépôt) sont à payer … jours après leur date");

        var l2 = UIFactory.Text(content, "Mois facturés (si trimestriel / bi-annuel)", UITheme.Role.Libelle, UITheme.TexteSecondaire);
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
        // Revue 17 (avenant de périodicité, finitions) : la date d'effet proposée suit la reprise choisie ici.
        _repriseDD = UIDropdown.Create(rv.transform,
            new List<string> { "Aucune (nouveau bail)" }, new List<string> { "" }, 0, _ => RefreshAvenantPeriodicite());
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
        UIFactory.Text(tr.transform, "Type de révision", UITheme.Role.Libelle, UITheme.TexteSecondaire);
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
            _caTitrePrecedent = UIFactory.Text(gauche.transform, "CA précédent", UITheme.Role.Libelle, UITheme.TexteSecondaire);
            _caPrecedent = UIFactory.Input(gauche.transform, "—");
            _caPrecedent.interactable = false;
            var droite = UIFactory.VBox(cote.transform, 4, 0, 0, 0, 0, "CANouveau");
            _caTitre = UIFactory.Text(droite.transform, "Nouveau CA HT", UITheme.Role.Libelle, UITheme.TexteSecondaire);
            _caMontant = UIFactory.Input(droite.transform, "CA HT de l'année (€)");
            SaisieNumerique.Montant(_caMontant);   // retour BN2 (04/11) : la virgule aussi
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
            "Avenant : le nouveau loyer s'applique en cours de bail", "Nouveau loyer à compter du",   // BU24 (22/11) : « Nouvelles conditions » se confondait avec « Nouvelle périodicité à compter du »
            out _avenantToggle, out _avenantDate);
        _franchiseBlockGO = BlocCaseDate(content, "FranchiseBlock",
            "Franchise de loyer (les provisions restent dues)", "Loyer facturé à partir du (fin de la franchise)",
            out _franchiseToggle, out _franchiseDate);

        // Ordre : type de révision, avenant, franchise, puis la reprise.
        int ri = _repriseBlockGO.transform.GetSiblingIndex();
        rb.transform.SetSiblingIndex(ri);
        _avenantBlockGO.transform.SetSiblingIndex(ri + 1);
        _franchiseBlockGO.transform.SetSiblingIndex(ri + 2);

        // Revue 17 (avenant de périodicité, lot 15 ; décision du 05/11) : « Nouvelle périodicité à compter du », juste sous la
        // périodicité. Le même bloc que l'avenant de loyer, sans sa case (cachée, cochée) : dès qu'un loyer est déjà facturé, la
        // date est demandée (RefreshAvenantPeriodicite la montre, la propose et dit pourquoi).
        _avenantPerBlockGO = BlocCaseDate(content, "AvenantPeriodiciteBlock",
            "Nouvelle périodicité en cours de bail", "Nouvelle périodicité à compter du",
            out _avenantPerToggle, out _avenantPerDate);
        _avenantPerToggle.SetIsOnWithoutNotify(true);
        _avenantPerToggle.gameObject.SetActive(false);
        _avenantPerAide = UIFactory.Text(_avenantPerBlockGO.transform, "", UITheme.Role.Aide, UITheme.TexteSecondaire);
        _avenantPerBlockGO.transform.SetSiblingIndex(periodBlock.GetSiblingIndex() + 1);
        _avenantPerBlockGO.SetActive(false);

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
            _btnEnregistrer.onClick.AddListener(ConfirmerTauxPuisValider);
            _btnEnregistrer.gameObject.SetActive(false);
        }

        // En dernier : les clones ci-dessus partent des blocs d'origine, hors des lignes.
        LignesCompactes(content);
        EnvelopperDansScroll((RectTransform)content);
    }

    // ── Fenêtre plafonnée à l'écran (BI2, 02/11) ──────────────────────────────
    // Les champs ajoutés (délais propres, listes de charges) ont porté « Initialiser » à 1 519 px pour un écran de
    // 1 080 : le bas (Enregistrer) n'était plus atteignable. Le corps défile dans la fenêtre, qui ne dépasse plus
    // l'écran et se centre. Même montage que ChargePanel.MakeScroll ; comme là-bas, un menu déroulant ouvert tout
    // en bas est rogné par le masque.
    const float MargeEcran = 24f;

    void EnvelopperDansScroll(RectTransform corps)
    {
        _fenetre = (RectTransform)corps.parent;
        var srGO = UIFactory.Rect("Scroll", _fenetre);
        srGO.SetSiblingIndex(corps.GetSiblingIndex());
        _scroll = srGO.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false; _scroll.vertical = true; _scroll.scrollSensitivity = 32;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _leScroll = UIFactory.LE(srGO.gameObject, flexH: 1);
        var viewport = UIFactory.Rect("Viewport", srGO);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear;   // la molette prend aussi dans les vides
        corps.SetParent(viewport, false);
        corps.anchorMin = new Vector2(0, 1); corps.anchorMax = new Vector2(1, 1); corps.pivot = new Vector2(.5f, 1);
        corps.offsetMin = Vector2.zero; corps.offsetMax = Vector2.zero;
        var csf = corps.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        _scroll.viewport = viewport; _scroll.content = corps;
        // Revue 17 (B-13, lot G) : « Modifier » et « Fermer » restent sous le défilement (pied fixe). Avant : au bout du contenu, donc
        // invisibles à 150 % tant qu'on n'avait pas fait défiler jusqu'en bas.
        var boutons = btnFermer != null ? btnFermer.transform.parent as RectTransform : null;
        if (boutons != null && boutons.parent == corps && boutons.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup rangee)
        {
            boutons.SetParent(_fenetre, false);
            boutons.SetSiblingIndex(srGO.GetSiblingIndex() + 1);
            rangee.padding = new RectOffset(18, 18, 10, 18);   // les marges du corps, qu'elle n'a plus autour d'elle
            // Revue visuelle du 16/11 (V-05 à V-08 ; décisions du 16/11) : l'abandon avant l'action, compacts à droite, 40 de haut ;
            // « Annuler » à côté d'une action (c'était « Modifier · Fermer », en deux moitiés de 42 et 38).
            rangee.childForceExpandWidth = false; rangee.childAlignment = TextAnchor.MiddleRight; rangee.spacing = 8;
            var espace = UIFactory.Rect("Espace", boutons);
            UIFactory.LE(espace.gameObject, flexW: 1);
            espace.SetAsFirstSibling();
            btnFermer.transform.SetSiblingIndex(1);
            var texteFermer = btnFermer.GetComponentInChildren<TMP_Text>(true);
            if (texteFermer != null) texteFermer.text = "Annuler";
            foreach (var b in boutons.GetComponentsInChildren<Button>(true))
            {
                var le = b.GetComponent<LayoutElement>();
                if (le != null) { le.minHeight = le.preferredHeight = 40f; le.flexibleWidth = 0f; }
                UIFactory.LargeurDuTexte(b);
            }
            // Revue visuelle du 16/11 : le fond blanc de la fenêtre est celui du corps (bas arrondi). Sortie du corps, la rangée
            // flottait sur le voile sombre : elle reprend ce fond et son bas arrondi ; le corps, au-dessus, devient droit.
            var fondCorps = corps.GetComponent<Image>();
            if (fondCorps != null && boutons.GetComponent<Graphic>() == null)
            {
                var fond = boutons.gameObject.AddComponent<Image>();
                fond.sprite = fondCorps.sprite; fond.type = fondCorps.type; fond.color = fondCorps.color;
                fond.pixelsPerUnitMultiplier = fondCorps.pixelsPerUnitMultiplier;
                fondCorps.sprite = null;
            }
            _pied = boutons;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(corps);
        PlafonnerHauteur();
    }

    void LateUpdate() { if (_leScroll != null) PlafonnerHauteur(); }

    // Le panneau (parent de la fenêtre) couvre l'écran : sa hauteur est celle de l'écran.
    void PlafonnerHauteur()
    {
        var ecran = (RectTransform)transform;
        float titre = _leTitre != null ? LayoutUtility.GetPreferredHeight((RectTransform)_leTitre.transform) : 0f;
        float pied = _pied != null && _pied.gameObject.activeSelf ? LayoutUtility.GetPreferredHeight(_pied) : 0f;
        float h = Mathf.Min(LayoutUtility.GetPreferredHeight(_scroll.content), ecran.rect.height - 2 * MargeEcran - titre - pied);
        if (!Mathf.Approximately(_leScroll.preferredHeight, h)) _leScroll.preferredHeight = h;
        float y = -ecran.rect.height / 2f;   // centrée : la scène la posait à une hauteur fixe, pensée pour un contenu court
        if (!Mathf.Approximately(_fenetre.anchoredPosition.y, y)) _fenetre.anchoredPosition = new Vector2(_fenetre.anchoredPosition.x, y);
    }

    // TVA (02/11) : visible dans Initialiser ; le taux seulement quand la case est cochée.
    void RefreshTvaVisibility(bool mod)
    {
        if (_tvaToggle == null) return;
        bool soumis = _tvaToggle.isOn;
        SetGO(_tvaToggle.gameObject, mod);
        SetGO(_tvaLabelGO, mod && soumis);
        if (_tvaTaux != null) _tvaTaux.gameObject.SetActive(mod && soumis);
    }
    static float TauxEntreprise() => ReglageService.Current?.tauxTva > 0f ? ReglageService.Current.tauxTva : Tva.TauxDefaut;

    // Revue 17 (D5-17, coutures) : le taux de TVA du bail, s'il est inhabituel (« 2 » tapé pour « 20 ») et pas déjà en place, se confirme
    // avant l'enregistrement des modalités ; « Annuler » laisse la fenêtre ouverte avec la saisie. Avant : 2 % sur toutes ses factures.
    void ConfirmerTauxPuisValider()
    {
        float enPlace = _loc != null && _loc.tauxTvaLoyer > 0f ? _loc.tauxTvaLoyer : TauxEntreprise(), taux = enPlace;
        if (_tvaToggle != null && _tvaToggle.isOn && _tvaTaux != null && SaisieNumerique.TryParse(_tvaTaux.text, out float tx) && tx > 0f) taux = tx;
        Tva.ConfirmerTaux(taux, enPlace, ValiderInitialisation);
    }

    // Mois visibles seulement dans le volet Initialiser, hors mensuel ;
    // régularisation visible seulement dans Initialiser et en cas de provision.
    void RefreshMoisVisibility()
    {
        bool show = _volet == Volet.Initialisation && periodiciteDropdown.value != (int)Periodicite.mensuel;
        if (_moisLabelGO != null) _moisLabelGO.SetActive(show);
        if (_moisWrapGO != null) _moisWrapGO.SetActive(show);
    }

    // Délais de facturation propres au locataire (01/11) : visibles dans « Initialiser » quand la case
    // « ceux de l'entreprise » est décochée, pré-remplis avec les délais de l'entreprise.
    TMP_InputField ChampJours(Transform parent, string libelle)
    {
        UIFactory.Text(parent, libelle, UITheme.Role.Libelle, UITheme.TexteSecondaire);
        var f = UIFactory.Input(parent, "15");
        f.contentType = TMP_InputField.ContentType.IntegerNumber;
        return f;
    }

    void RefreshDelaisVisibility()
    {
        bool show = _volet == Volet.Initialisation && _delaisToggle != null && !_delaisToggle.isOn;
        if (_delaisBlockGO != null) _delaisBlockGO.SetActive(show);
        if (!show) return;
        var R = ReglageService.Current;
        if (_dEnvoi.text == "")  _dEnvoi.text  = R.envoiAvantJours.ToString();
        if (_dImpaye.text == "") _dImpaye.text = R.impayeApresJours.ToString();
        if (_dAutres.text == "") _dAutres.text = R.echeanceAutresJours.ToString();
    }

    void RefreshRegulVisibility()
    {
        bool show = _volet == Volet.Initialisation && toggleProvisions.isOn;
        // Avec des listes spécifiques, la générale a sa ligne en bas comme les autres :
        // le montant à côté de la case et la date du prefab feraient doublon.
        bool avecListes = ListesCharges.Specifiques().Count > 0;
        if (_regulBlockGO != null) _regulBlockGO.SetActive(show && !avecListes);
        ProvContainer().SetActive(toggleProvisions.isOn && !avecListes);
        // Revue visuelle (V-19, décision du 16/11) : la case du prefab (un carré gris sous le titre « Provisions ») devient une
        // case ronde verte avec son libellé à côté, comme les autres cases de la fenêtre. Le libellé coche aussi.
        if (_libelleProvisions == null)
        {
            UIFactory.CaseRonde(toggleProvisions);
            _libelleProvisions = UIFactory.Text(toggleProvisions.transform.parent, "Provisions pour charges", UITheme.Role.Libelle, UITheme.TextePrincipal);
            _libelleProvisions.alignment = TextAlignmentOptions.MidlineLeft;
            _libelleProvisions.raycastTarget = true;
            _libelleProvisions.gameObject.AddComponent<Button>().onClick.AddListener(
                () => { if (toggleProvisions.interactable) toggleProvisions.isOn = !toggleProvisions.isOn; });
            _libelleProvisions.transform.SetSiblingIndex(toggleProvisions.transform.GetSiblingIndex() + 1);
        }
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
                ConfirmDialog.Erreur($"Impossible de retirer « {nom} ».\n\n" + detail);   // Revue 17 (D10-12, lot F)
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

            UIFactory.Text(_lignesListes, $"{nom} — provision par période (€)", UITheme.Role.Libelle, UITheme.TexteSecondaire);
            var prov = UIFactory.Input(_lignesListes, "0");
            SaisieNumerique.Montant(prov);   // retour BN2 (04/11) : la virgule aussi
            // Revue 17 (lot 16) : la provision enregistrée, ramenée à la périodicité choisie à l'écran (telle quelle tant que la
            // périodicité n'a pas changé) ; une ligne déjà à l'écran garde ce qui y est tapé, et sa dernière proposition.
            string propose = TexteProvision(provision);
            prov.text = deja.Item2 ?? propose;
            if (deja.Item2 == null) _provAuto[id] = propose;
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
    // Revue 17 (avenant de périodicité, lot 15) : les options sont les périodes du PLAN (FacturationSuivi.Plan), « {année}-P{n} »
    // avec n = leur rang dans l'année, comme les clés des loyers. Une fiche qui a un passé de facturation (loyer facturé, reprise,
    // périodicité déjà changée à une date : Loyers.DateAvenantRequise) propose son plan enregistré : les périodes passées gardent
    // leur périodicité, quelle que soit celle choisie à l'écran (avant : la reprise « Mars 2026 » d'une fiche passée au
    // trimestriel s'affichait « 3e trimestre 2026 », et « Modifier » la déplaçait en juillet sans le dire). Sinon, comme avant :
    // la périodicité choisie à l'écran, mois d'office.
    void RefreshRepriseOptions()
    {
        if (_repriseDD == null || _loc == null) return;
        var labels = new List<string> { "Aucune (nouveau bail)" };
        var ids = new List<string> { "" };
        var fiche = Loyers.DateAvenantRequise(_loc) ? _loc : new Locataire { periodiciteLoyer = (Periodicite)periodiciteDropdown.value };
        int jour = _jourDemande != null && int.TryParse(_jourDemande.text, out int j) ? j : _loc.jourDemandeLoyer;
        var limite = DateTime.Today.AddDays(FacturationSuivi.Lead("Loyer", _loc));
        bool avecBail = Loyers.DebutPremierBail(_loc, out var debutBail);
        int cur = DateTime.Now.Year;
        for (int y = cur; y >= cur - 3; y--)
        {
            var plan = FacturationSuivi.Plan(fiche, y);
            for (int per = plan.Count; per >= 1; per--)
            {
                var debut = plan[per - 1].debut;
                var ech = new DateTime(debut.Year, debut.Month, Mathf.Clamp(jour > 0 ? jour : 1, 1, DateTime.DaysInMonth(debut.Year, debut.Month)));
                if (ech > limite) continue;
                if (avecBail && plan[per - 1].fin < debutBail.Date) continue;
                labels.Add(FacturationSuivi.LibellePeriode(fiche, y, per));
                ids.Add($"{y}-P{per}");
            }
        }
        string sel = "";
        if (DateTime.TryParse(_loc.repriseFacturationISO, out var rd))
        {
            var (anEnr, perEnr) = PeriodeReprise(_loc, rd);
            // Revue 17 (avenant de périodicité, finitions) : introuvable dans le plan : une option « gardée », que RepriseChoisie ne
            // lit pas (null : la reprise enregistrée reste telle quelle). Avant : « Aucune », et « Modifier » l'effaçait.
            sel = perEnr > 0 ? $"{anEnr}-P{perEnr}" : RepriseGardee;
            // Reprise déjà enregistrée mais hors des options (plus de 4 ans…) : on la
            // garde proposée, sinon un simple « Modifier » l'effaçait sans le dire.
            if (!ids.Contains(sel))
            {
                labels.Insert(1, perEnr > 0 ? FacturationSuivi.LibellePeriode(_loc, anEnr, perEnr) : $"Reprise enregistrée ({rd:dd/MM/yyyy})");
                ids.Insert(1, sel);
            }
        }
        _repriseDD.SetOptions(labels, ids, sel);
    }

    const string RepriseGardee = "gardee";   // ni « {année}-P{n} » ni vide : RepriseChoisie rend null, rien n'est réécrit

    /// Revue 17 (avenant de périodicité, finitions) : la période du plan qui contient la reprise enregistrée (année, rang) : celle
    /// de son année, sinon celle de l'année d'avant, pour une période commencée avant le 1er janvier (mois cochés à cheval sur
    /// janvier), comme Loyers.FactureJusquAu ; rang 0 si aucune. Avant, avec un historique de périodicité, ce cas donnait
    /// « {année}-P0 », absent du menu : il retombait sur « Aucune » et « Modifier » effaçait la reprise sans le dire. Sans
    /// historique, rien ne change (FacturationSuivi.PeriodeIndex ne rend jamais 0).
    public static (int annee, int periode) PeriodeReprise(Locataire loc, DateTime reprise)
    {
        int per = FacturationSuivi.PeriodeIndex(loc, reprise.Year, reprise.Month);
        if (per > 0) return (reprise.Year, per);
        per = FacturationSuivi.Plan(loc, reprise.Year - 1).FindIndex(p => p.debut <= reprise.Date && reprise.Date <= p.fin) + 1;
        return (per > 0 ? reprise.Year - 1 : reprise.Year, per);
    }

    /// Revue 17 (avenant de périodicité, lot 15) : la reprise choisie à l'écran, en date : l'échéance de la dernière période déjà
    /// facturée hors de CIPL, lue dans le plan de la fiche à cet instant ("" : aucune ; null : option illisible, rien ne change).
    /// Avant l'écriture, elle sert au refus de la date d'effet ; après, elle est la reprise enregistrée.
    string RepriseChoisie()
    {
        string id = _repriseDD != null ? _repriseDD.SelectedId : null;
        if (string.IsNullOrEmpty(id)) return _repriseDD != null ? "" : null;
        var parts = id.Split('-');   // « 2026-P2 »
        return parts.Length == 2 && int.TryParse(parts[0], out int y) && int.TryParse(parts[1].TrimStart('P', 'p'), out int per)
            ? FacturationSuivi.EcheanceLoyer(_loc, y, per).ToString("yyyy-MM-dd") : null;
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
        RefreshTvaVisibility(mod);   // 02/11
        if (_jourDemande != null) SetGO(_jourDemande.gameObject, mod);
        if (_delaisToggle != null) SetGO(_delaisToggle.gameObject, mod);
        RefreshDelaisVisibility();
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
        if (!init) SetGO(_avenantPerBlockGO, false);   // Revue 17 (lot 15) : comme l'avenant de loyer, seulement dans « Gestion du loyer »
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
        SaisieNumerique.Montant(f);   // retour BN2 (04/11) : la virgule aussi
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

    // Revue 17 (D7b-10, lot D) : en double et au format fixe : float.ToString() n'écrit que 7 chiffres sous Mono (123456,78 → 123456,8 ;
    // 12 345 678 → 1.234568E+07). Vide pour 0. Aussi pour PaliersPanel.
    public static string Nombre(float v) => v > 0f ? ((double)v).ToString("0.##", CultureInfo.InvariantCulture) : "";

    /// Revue 17 (D7b-01, lot D) : « (+3,0 %) » pour le message de fin d'une révision ; vide sans loyer précédent.
    public static string VariationLoyer(float avant, float apres)
        => avant > 0f ? $" ({(apres >= avant ? "+" : "")}{((apres / avant - 1f) * 100f).ToString("0.0", FacturePdfService.FrCulture)} %)" : "";
    static string Euros(float v) => v.ToString("N2", FacturePdfService.FrCulture) + " €";

    // Revue 16c (BJ4, 02/11) : une date tapée mais illisible (année « 27 »…) est refusée. Avant, « Réviser » partait avec la
    // date précédente et « Enregistrer » effaçait en silence une date de régularisation illisible. Vide = pas de date, admis.
    static bool DateIllisible(DateInputController d)
        => d != null && d.gameObject.activeInHierarchy && !d.LireDate(out _)
           && !(string.IsNullOrWhiteSpace(d.dayInput.text) && string.IsNullOrWhiteSpace(d.monthInput.text) && string.IsNullOrWhiteSpace(d.yearInput.text));

    /// Le refus à afficher pour les dates de l'écran, ou null si elles sont toutes lisibles (ou vides).
    string DatesIllisibles()
    {
        if (DateIllisible(dateDeRevision)) return "Date de révision incomplète : jour, mois et année sur quatre chiffres.";
        // Revue 17 (avenant de périodicité, lot 15) : la date d'effet aussi (ici, pas dans un appel de plus : Revue16cTests les compte).
        if (DateIllisible(_avenantPerDate)) return "Date de la nouvelle périodicité incomplète : jour, mois et année sur quatre chiffres.";
        if (DateIllisible(_dateRegulCtrl)) return "Date de régularisation incomplète : jour, mois et année sur quatre chiffres, ou champs vides (pas de régularisation).";
        foreach (var (id, date, prov) in _listesChamps)
            if (DateIllisible(date)) return $"Date de régularisation « {ListesCharges.Nom(id)} » incomplète : jour, mois et année sur quatre chiffres, ou champs vides (pas de régularisation).";
        return null;
    }

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
        float saisi = SaisieNumerique.TryParse(loyerDepart.text, out float v) ? v : 0f;   // explicite : une version double existe (D7b-12)
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
        RefreshAvenantPeriodicite();   // Revue 17 (lot 15) : la date d'effet proposée suit l'avenant de loyer (case, date)
    }

    // Habillage : Indice garde le thème (dû → terracotta / sinon prune) ;
    // Initialiser passe en ambre (famille « argent » du loyer).
    void ApplyThemeVolet(bool mod)
    {
        if (!mod) { ApplyTheme(LoyerSummaryUI.EstRevisionDue(_loc)); return; }
        var header = transform.Find("Content/titre")?.GetComponent<Image>();
        if (header != null) header.color = UITheme.Hex("#A9741C");
        ColorButton(_btnEnregistrer, UITheme.Hex("#854F0B"));   // Revue 17 (D10-10, lot F)
    }

    // ── Validation de l'écran « Initialiser » ─────────────────────────────────
    // Modifier : les modalités sont enregistrées, fin. Initialiser / Réinitialiser :
    // on repart du loyer de départ, puis l'écran du type choisi (schéma du 30/09) —
    // indice → initialisation de l'indice, paliers → tableau, aucune → la fiche.
    void ValiderInitialisation()
    {
        if (_loc == null) return;
        if (!TryParseLoyer(out float loyer) || loyer <= 0f)
        { ConfirmDialog.Erreur("Saisissez le loyer de départ annuel (HT)."); return; }
        if (DatesIllisibles() is string refusDate) { ConfirmDialog.Erreur(refusDate); return; }   // BJ4 (02/11)
        // Retour BN11 (décision du 04/11) : autant de mois cochés que de périodes, sinon deux échéances tombent le même mois.
        if (RefusMois((Periodicite)periodiciteDropdown.value, _moisState.Count(x => x)) is string refusMois)
        { ConfirmDialog.Erreur(refusMois); return; }

        string franchise = FranchiseSaisie();
        if (franchise == null) { ConfirmDialog.Erreur("Date de fin de franchise invalide."); return; }
        if (franchise != "")
        {
            var df = DateTime.ParseExact(franchise, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (FacturationSuivi.TryEcheance(_loc.dateDebutBailISO, out var db) && df < db.Date)
            { ConfirmDialog.Erreur($"La franchise ne peut pas finir avant le début du bail ({db:dd/MM/yyyy})."); return; }
            if (FacturationSuivi.TryEcheance(_loc.dateFinBailISO, out var fb) && df > fb.Date)
            { ConfirmDialog.Erreur($"La franchise ne peut pas finir après la fin du bail ({fb:dd/MM/yyyy})."); return; }
        }

        bool avecBail = Loyers.DebutPremierBail(_loc, out var debutBail);

        // À décider AVANT d'écrire quoi que ce soit sur le locataire.
        var type = TypeChoisi();
        bool etaitInitialise = _loc.LoyerInitialise;
        bool reinit = !etaitInitialise || ChangementStructurant();

        // Revue 17 (avenant de périodicité, lot 15 ; décision du 05/11) : la périodicité ou les mois changent alors qu'un loyer est
        // déjà facturé : la date d'effet est demandée, et refusée au centre AVANT toute écriture (EnregistrerAvenant, plus bas,
        // écrit déjà l'historique des loyers). Elle est le jour de demande du loyer d'un mois, après la dernière période facturée
        // (la reprise choisie à l'écran comprise) : aucun mois facturé n'est reproposé, aucun loyer émis ne change de période.
        DateTime? effetPeriodicite = null;
        if (AvenantPeriodiciteDemande())
        {
            string saisie = DateSaisie(_avenantPerToggle, _avenantPerDate);
            if (string.IsNullOrEmpty(saisie))
            { ConfirmDialog.Erreur("Saisissez la date à partir de laquelle la nouvelle périodicité s'applique (« Nouvelle périodicité à compter du »)."); return; }
            effetPeriodicite = Iso(saisie);
            if (Loyers.RefusAvenantPeriodicite(_loc, effetPeriodicite.Value, JourSaisi(), RepriseChoisie()) is string refusPeriodicite)
            { ConfirmDialog.Erreur(refusPeriodicite); return; }
        }

        // Revue 17 (D7b-17, lot D) : « Modifier » la périodicité ou les mois d'un locataire à paliers est refusé si un palier ne finirait
        // plus la veille d'un début de période (copie de la fiche avec les nouvelles modalités) ; la saisie reste à l'écran.
        if (!reinit && type == TypeRevision.Paliers)
        {
            var perChoisie = (Periodicite)periodiciteDropdown.value;
            var moisChoisis = Enumerable.Range(1, 12).Where(k => _moisState[k - 1]).ToList();
            if (CalendrierChange(_loc.periodiciteLoyer, _loc.moisFacturationLoyer, perChoisie, moisChoisis)
                && Loyers.PaliersDesalignesApres(_loc, perChoisie, moisChoisis, effetPeriodicite))
            {
                ConfirmDialog.Erreur("Avec cette périodicité, un palier ne finirait plus la veille d'un début de période de facturation : "
                                     + "adaptez d'abord les paliers (bouton « Paliers » de la fiche), puis changez la périodicité.");
                return;
            }
        }

        // Avenant : l'ancien loyer reste dû jusqu'à la veille (prorata de la période).
        string avenant = reinit && etaitInitialise ? DateSaisie(_avenantToggle, _avenantDate) : "";
        if (avenant == null) { ConfirmDialog.Erreur("Date de l'avenant invalide."); return; }
        if (avenant != "")
        {
            if (avecBail && Iso(avenant) <= debutBail.Date)
            { ConfirmDialog.Erreur($"L'avenant doit prendre effet après le début du bail ({debutBail:dd/MM/yyyy})."); return; }
            if (type == TypeRevision.Paliers && FacturationSuivi.TryEcheance(_loc.dateFinBailISO, out var finBail) && Iso(avenant) > finBail.Date)
            { ConfirmDialog.Erreur("Les paliers vont jusqu'à la fin du bail : prolongez d'abord sa date de fin (section Bail)."); return; }
            // Lit le type, les paliers et le loyer encore en place : avant toute écriture.
            Loyers.EnregistrerAvenant(_loc, Iso(avenant));
        }

        // Revue 17 (avenant de périodicité, lot 15) : la périodicité et les mois encore en place passent dans l'historique jusqu'à
        // la veille du mois d'effet : avant EcrireModalites, qui les remplace.
        if (effetPeriodicite.HasValue) Loyers.EnregistrerAvenantPeriodicite(_loc, effetPeriodicite.Value);
        string infoPeriodicite = effetPeriodicite.HasValue
            ? $" Nouvelle périodicité à compter du {effetPeriodicite.Value:dd/MM/yyyy} : les loyers d'avant gardent l'ancienne." : "";

        EcrireModalites();
        _loc.loyerDepart = loyer;
        _loc.debutFacturationISO = franchise;
        _loc.typeRevision = type;

        if (!reinit)
        {
            _onSaved?.Invoke();
            UndoToast.Instance?.ShowInfo(effetPeriodicite.HasValue ? "Modalités du loyer enregistrées." + infoPeriodicite : "Modalités du loyer enregistrées");
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
                UndoToast.Instance?.ShowInfo("Loyer initialisé, sans révision." + infoPeriodicite);
                gameObject.SetActive(false);
                break;
            case TypeRevision.Paliers:
                // Revue 17 (avenant de périodicité, finitions) : la date d'effet enregistrée est dite ici aussi (PaliersPanel ne
                // dira ensuite que « Paliers enregistrés »).
                if (effetPeriodicite.HasValue) UndoToast.Instance?.ShowInfo("Modalités du loyer enregistrées." + infoPeriodicite);
                PaliersPanel.Open(_loc, _onSaved, this);   // avant de masquer : il cherche le canvas depuis ce panneau
                gameObject.SetActive(false);
                break;
            default:
                // Revue 17 (avenant de périodicité, finitions) : la date d'effet enregistrée est dite ici aussi, comme dans les
                // autres branches (avant : la fenêtre se rouvrait sur l'indice sans rien dire de la nouvelle périodicité).
                if (effetPeriodicite.HasValue) UndoToast.Instance?.ShowInfo("Modalités du loyer enregistrées." + infoPeriodicite);
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
        if (_tvaToggle != null)   // 02/11 (BK4) : dès qu'on a touché à la case ou au taux, le bail garde son propre choix ; sinon il suit l'entreprise
        {
            if (_tvaTouche) _loc.tvaLoyer = _tvaToggle.isOn ? "oui" : "non";
            _loc.tauxTvaLoyer = _tvaToggle.isOn && _tvaTaux != null && SaisieNumerique.TryParse(_tvaTaux.text, out float tx) && tx > 0f
                                && Mathf.Abs(tx - TauxEntreprise()) > 0.001f ? tx : 0f;
        }
        if (_delaisToggle != null)
        {
            _loc.delaisPropres = !_delaisToggle.isOn;
            var R = ReglageService.Current;
            _loc.envoiAvantJours     = FacturationSuivi.Jours(_dEnvoi.text,  R.envoiAvantJours);
            _loc.impayeApresJours    = FacturationSuivi.Jours(_dImpaye.text, R.impayeApresJours);
            _loc.echeanceAutresJours = FacturationSuivi.Jours(_dAutres.text, R.echeanceAutresJours);
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
        // Revue 16c (D7b-04) : la même échéance que les lignes du suivi (mois cochés, écrits juste avant), sinon la dernière
        // période déclarée facturée restait « À faire » puis « Impayé ». Revue 17 (lot 15) : lue par RepriseChoisie, dans le plan
        // qui vient d'être écrit (FacturationSuivi.EcheanceLoyer(_loc, y, per)) ; une période d'avant la date d'effet y garde son rang.
        if (RepriseChoisie() is string reprise) _loc.repriseFacturationISO = reprise;
    }

    // ── Avenant de périodicité : la date d'effet (Revue 17, lot 15 ; décision du 05/11) ──

    /// Les mois de facturation EFFECTIFS d'une périodicité : les mois cochés, sinon ceux d'office (MoisStandard) ; aucun en mensuel
    /// (ils sont ignorés). C'est eux qu'on compare, pas la liste enregistrée : à l'ouverture, CocherMoisStandard coche janvier,
    /// avril, juillet et octobre sur une fiche trimestrielle qui n'a aucun mois enregistré.
    public static List<int> MoisEffectifs(Periodicite p, IEnumerable<int> coches)
    {
        if (p == Periodicite.mensuel) return new List<int>();
        var tries = (coches ?? Enumerable.Empty<int>()).Where(m => m >= 1 && m <= 12).Distinct().OrderBy(m => m).ToList();
        return tries.Count > 0 ? tries : MoisStandard(p);
    }

    /// Le calendrier des loyers change : une autre périodicité, ou d'autres mois de facturation effectifs.
    public static bool CalendrierChange(Periodicite avant, IEnumerable<int> moisAvant, Periodicite apres, IEnumerable<int> moisApres)
        => avant != apres || !MoisEffectifs(avant, moisAvant).SequenceEqual(MoisEffectifs(apres, moisApres));

    // Le jour de demande saisi, tel qu'EcrireModalites va l'enregistrer.
    int JourSaisi()
    {
        int.TryParse(_jourDemande != null ? _jourDemande.text : "", out int j);
        return Mathf.Clamp(j, 0, 31);
    }

    /// La date d'effet est demandée : le calendrier des loyers change à l'écran, et la fiche a un passé de facturation (loyer déjà
    /// facturé, reprise, périodicité déjà changée à une date). Sinon « Modifier » vaut pour tout le bail, sans date, comme avant.
    bool AvenantPeriodiciteDemande()
        => _loc != null && _volet == Volet.Initialisation
           && CalendrierChange(_loc.periodiciteLoyer, _loc.moisFacturationLoyer, (Periodicite)periodiciteDropdown.value,
                               Enumerable.Range(1, 12).Where(m => _moisState[m - 1]))
           && Loyers.DateAvenantRequise(_loc);

    /// Montre le bloc « Nouvelle périodicité à compter du » quand la date est demandée, et la propose : le jour de demande du loyer
    /// du mois qui suit la dernière période facturée (Loyers.PremiereDateAvenant), ou d'après l'avenant de loyer saisi à côté. Le
    /// champ suit la proposition (jour de demande ou avenant de loyer retouchés) tant qu'il n'a pas été retouché lui-même.
    void RefreshAvenantPeriodicite()
    {
        if (_avenantPerBlockGO == null || _loc == null) return;
        bool etait = _avenantPerBlockGO.activeSelf, demande = AvenantPeriodiciteDemande();
        SetGO(_avenantPerBlockGO, demande);
        if (!demande) return;

        bool avecLoyer = _avenantBlockGO != null && _avenantBlockGO.activeSelf && _avenantToggle.isOn;
        // Revue 17 (avenant de périodicité, finitions) : la reprise CHOISIE à l'écran, comme le refus de « Modifier » (avant : celle
        // enregistrée ; une reprise plus tardive choisie au même écran faisait refuser la date proposée). Le menu de reprise rappelle
        // cette méthode. « Aucune » choisie et rien d'autre de facturé : pas de proposition, la dernière reste suivie.
        string reprise = RepriseChoisie();
        var propose = Loyers.PremiereDateAvenant(_loc, JourSaisi(), avecLoyer && _avenantDate.LireDate(out var al) ? al : (DateTime?)null, reprise);
        bool suit = ((!etait || _avenantPerAuto == null) && _avenantPerDate.Vide())   // jamais proposée : un champ vide n'a pas été retouché
                    || (_avenantPerDate.LireDate(out var saisie) && saisie.ToString("yyyy-MM-dd") == _avenantPerAuto);
        if (propose.HasValue) _avenantPerAuto = propose.Value.ToString("yyyy-MM-dd");
        if (suit && propose.HasValue && !(_avenantPerDate.LireDate(out var deja) && deja == propose.Value))
        {
            _avenantPerDate.ApplyDate(propose.Value);
            _avenantPerDate.ModifyDate();
        }
        _avenantPerAide.text = (Loyers.FactureJusquAu(_loc, out var ligne, reprise) is DateTime fin
                ? $"Le loyer est déjà facturé jusqu'au {fin:dd/MM/yyyy} ({(ligne != null ? $"« {ligne.libelle} »" : "reprise de facturation")}) : "
                  + "la nouvelle périodicité commence après. " : "")
            + "Saisissez le jour de demande du loyer du mois où elle commence. Les loyers d'avant gardent l'ancienne périodicité.";
    }

    // Nombre max de mois sélectionnables selon la périodicité.
    // Retour BN11 (décision du 04/11) : aucun mois coché → ceux que le suivi prend d'office, cochés pour qu'on les voie.
    void CocherMoisStandard()
    {
        if (_moisState.Any(x => x)) return;
        foreach (int m in MoisStandard((Periodicite)periodiciteDropdown.value)) _moisState[m - 1] = true;
    }

    /// Retour BN11 : les mois facturés d'office d'une périodicité, ceux que le suivi prend sans mois cochés (MoisEcheance) :
    /// trimestriel → janvier, avril, juillet, octobre ; bi-annuel → janvier, juillet ; annuel → janvier. Vide en mensuel.
    public static List<int> MoisStandard(Periodicite p)
    {
        var res = new List<int>();
        int n = LoyerSummaryUI.NbPeriodes(p);
        if (p != Periodicite.mensuel) for (int k = 1; k <= n; k++) res.Add((k - 1) * (12 / n) + 1);
        return res;
    }

    /// Retour BN11 (décision du 04/11) : refus si le nombre de mois cochés n'est pas celui des périodes, sinon null.
    public static string RefusMois(Periodicite p, int coches)
    {
        if (p == Periodicite.mensuel || coches == LoyerSummaryUI.NbPeriodes(p)) return null;
        switch (p)
        {
            case Periodicite.trimestriel: return "Trimestriel : cochez 4 mois, un par trimestre (par exemple Jan, Avr, Juil, Oct).";
            case Periodicite.BiAnnuel:    return "Bi-annuel : cochez 2 mois, un par semestre (par exemple Jan et Juil).";
            default:                      return "Annuel : cochez le mois de facturation (par exemple Jan).";
        }
    }

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
        // Retour BN11 (décision du 04/11) : une autre périodicité repart des mois d'office (janvier, avril, juillet, octobre en
        // trimestriel…) quand le nombre de mois cochés ne lui va pas, plutôt que de garder les premiers de l'ancienne.
        int max = MaxMois(), c = _moisState.Count(x => x);
        if (max < 12 && c != max)
        {
            for (int i = 0; i < 12; i++) _moisState[i] = false;
            foreach (int m in MoisStandard((Periodicite)periodiciteDropdown.value)) _moisState[m - 1] = true;
        }
        for (int i = 0; i < 12; i++) RefreshChip(i);
        RefreshMoisVisibility();
        // Revue 17 (avenant de périodicité, lot 15) : une fiche qui a un passé de facturation propose les périodes de son plan
        // enregistré, quelle que soit la périodicité à l'écran : rien à reconstruire (et la reprise choisie n'est pas perdue).
        if (!Loyers.DateAvenantRequise(_loc)) RefreshRepriseOptions();
        RefreshAvenantPeriodicite();
        ConvertirProvisions();
    }

    // ── Provisions converties avec la périodicité (Revue 17, avenant de périodicité, lot 16 ; décision du 05/11) ──

    /// La périodicité change à l'écran : la provision générale et celle de chaque liste, saisies « par période », sont converties
    /// d'office (100 € par mois → 300 € par trimestre), d'après la valeur et la périodicité ENREGISTRÉES (Loyers.ProvisionConvertie),
    /// pas d'après le champ affiché : les allers-retours dériveraient (100 € par trimestre → 33,33 € → 99,99 €). Un champ retouché
    /// à la main n'est pas converti ; tous restent modifiables avant « Modifier ». Avant : montant gardé tel quel, soit 100 € par
    /// trimestre au lieu de 300 €, vu seulement à la régularisation.
    void ConvertirProvisions()
    {
        if (_loc == null) return;
        var choisie = (Periodicite)periodiciteDropdown.value;
        // Le champ à côté de la case garde son format d'ouverture (ToString simple) : revenu à la périodicité enregistrée, il retrouve son texte.
        // Revue 17 (avenant de périodicité, finitions) : caché dès qu'une liste de charges existe (RefreshRegulVisibility), ce champ suit
        // toujours la conversion : il ne compte pour le toast que s'il est à l'écran (avant : toast même quand rien de visible ne bougeait).
        bool change = SuivreProvision(provisionValue, ProvCase,
            Loyers.ProvisionConvertie(_loc.provisionPourChargeValue, _loc.periodiciteLoyer, choisie).ToString()) && ProvContainer().activeSelf;
        foreach (var (id, _, prov) in _listesChamps)
            change |= SuivreProvision(prov, id, TexteProvision(id == "" ? _loc.provisionPourChargeValue : ListesCharges.De(_loc, id)?.provision ?? 0f));
        if (change && toggleProvisions.isOn)
            UndoToast.Instance?.ShowInfo("Provisions pour charges converties à la nouvelle périodicité : vérifiez-les, elles restent modifiables.");
    }

    // Le champ suit la proposition tant qu'il la porte encore (pas retouché à la main). Vrai si son texte a changé.
    bool SuivreProvision(TMP_InputField champ, string cle, string propose)
    {
        bool suit = champ != null && _provAuto.TryGetValue(cle, out string auto) && champ.text == auto && auto != propose;
        if (suit) champ.text = propose;
        _provAuto[cle] = propose;
        return suit;
    }

    // Le texte d'une provision de liste pour la périodicité choisie à l'écran, d'après la valeur enregistrée ; vide à zéro, comme avant.
    string TexteProvision(float enregistree)
    {
        float v = _loc == null ? enregistree
                : Loyers.ProvisionConvertie(enregistree, _loc.periodiciteLoyer, (Periodicite)periodiciteDropdown.value);
        return v > 0f ? v.ToString("0.##", CultureInfo.InvariantCulture) : "";
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
                RefreshAvenantPeriodicite();   // Revue 17 (lot 15) : d'autres mois facturés, c'est un autre calendrier
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
        return ok;   // Revue 17 (D7b-06, lot D) : le refus est dit au centre par l'appelant
    }
}