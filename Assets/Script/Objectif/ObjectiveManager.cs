using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// Objectifs d'une fiche (bâtiment ou locataire), en tableau à colonnes À faire · En cours ·
/// Fait (maquette validée le 06/10) : filtre d'importance, rappels automatiques en tête de
/// « À faire », cartes à glisser ou à avancer avec « › ». Création et modification dans la
/// fenêtre ObjectifPanel.
public class ObjectivesManager : MonoBehaviour
{
    [Header("Panel principal")]
    public GameObject objectivesPanel;

    [Header("Input ajout (remplacé par la fenêtre ObjectifPanel — masqué)")]
    public GameObject inputRow;
    public TMP_InputField newObjectiveInput;
    public TMP_Dropdown statusDropdown;
    public Button confirmAddButton;
    public Button addButton;

    [Header("Filtres (remplacés par la barre d'importance — masqués)")]
    public Button filterAllButton;
    public Button filterAFaireButton;
    public Button filterEnCoursButton;
    public Button filterObligatoireButton;
    public Button filterRappelButton;

    [Header("Liste (remplacée par le tableau — masquée)")]
    public Transform listContent;
    public GameObject objectiveItemPrefab;
    public TMP_Text emptyText;

    const int FAITS_VISIBLES = 2;   // « Fait » : les derniers, puis « Voir tout l'historique »
    const float HAUTEUR_MAX = 360f; // environ 5 cartes, puis le tableau défile (décision du 07/10)
    private LayoutElement _hauteur;

    // Données
    private ObjectiveList _objectives;
    private Objective.Importance? _importance;
    private bool _seulementAuto;   // pastille « Automatique » (08/10) : les cartes automatiques seules
    private bool _historique;

    // Tableau
    private RectTransform _tableau;
    private readonly RectTransform[] _colonnes = new RectTransform[3];
    private readonly RectTransform[] _entetes = new RectTransform[3];
    private readonly List<(Button b, Objective.Importance? im, bool auto)> _chips = new();

    public UnityEvent AddNeObjectif = new UnityEvent();   // enregistre (updateListObjectif de la fiche)

    private void Start()
    {
        addButton.onClick.AddListener(() => Ouvrir(null));
        Construire();
        RefreshList();
    }

    // Les rappels automatiques changent sans passer par ici (facture émise, révision faite) :
    // on les relit à chaque affichage de la fiche.
    private void OnEnable() { _remonter = true; if (_tableau != null) RefreshList(); }

    // Remettre le tableau en haut : à l'affichage de la fiche et au changement de filtre (le
    // contenu démarrait décalé de 28 vers le bas, en-têtes de colonnes rognés — 07/10), mais
    // pas quand on déplace une carte (on garde sa place dans la liste).
    private bool _remonter = true;

    // Zoom des réglages ou fenêtre redimensionnée : la largeur offerte change, on remesure
    // (défilement en largeur ou non, hauteur des cartes qui passent à la ligne).
    private float _largeurVue = -1f;
    private void LateUpdate()
    {
        if (_tableau == null || _hauteur == null) return;
        float w = ((RectTransform)_tableau.parent).rect.width;
        if (Mathf.Abs(w - _largeurVue) < 1f) return;
        _largeurVue = w;
        StartCoroutine(AjusterHauteur());
    }

    private List<Objective> Items
    {
        get
        {
            if (_objectives == null) _objectives = new ObjectiveList();
            if (_objectives.items == null) _objectives.items = new List<Objective>();
            return _objectives.items;
        }
    }

    // Fiche d'appartenance : bâtiment, et locataire pour une fiche locataire.
    private void Fiche(out BatimentPrefab bp, out Locataire loc)
    {
        var lp = GetComponentInParent<LocatairePrefab>(true);
        if (lp != null) { bp = lp.batimentPrefabOrigin; loc = lp.GetLocataire(); return; }
        bp = GetComponentInParent<BatimentPrefab>(true);
        loc = null;
    }

    private void Ouvrir(Objective obj)
    {
        Fiche(out var bp, out var loc);
        ObjectifPanel.Ouvrir(transform, Items, obj, bp != null ? bp.getName() : null, loc?.Name, Enregistrer, OnDeleted);
    }

    private void Enregistrer()
    {
        RefreshList();
        AddNeObjectif.Invoke();
    }

    // ── Construction (une fois) ───────────────────────────────────────────────

    private void Construire()
    {
        if (_tableau != null || filterAllButton == null) return;
        var row = filterAllButton.transform.parent;

        // « Header » interne vide (le vrai titre est dans la bande de la section).
        var innerHeader = row.parent.Find("Header ") ?? row.parent.Find("Header");
        if (innerHeader != null) innerHeader.gameObject.SetActive(false);
        if (inputRow != null) inputRow.SetActive(false);

        // Ligne de filtres : les anciens boutons laissent la place à « Importance : Toutes ·
        // Obligatoire · Rappel · Normale » puis « + Ajouter » à droite.
        foreach (var b in new[] { filterAllButton, filterAFaireButton, filterEnCoursButton, filterObligatoireButton, filterRappelButton })
            if (b != null) b.gameObject.SetActive(false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.spacing = 6;
            hlg.childForceExpandWidth = false; hlg.childControlWidth = true;
            hlg.childForceExpandHeight = false; hlg.childControlHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;
        }
        var rle = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
        rle.minHeight = 38; rle.preferredHeight = 38; rle.flexibleHeight = 0;

        Chip(row, "Toutes", null);
        Chip(row, "Obligatoire", Objective.Importance.Obligatoire);
        Chip(row, "Rappel", Objective.Importance.Rappel);
        Chip(row, "Tâche", Objective.Importance.Normale);
        Chip(row, "Automatique", null, true);
        UIFactory.LE(UIFactory.Rect("Espace", row).gameObject, flexW: 1);

        addButton.transform.SetParent(row, false);
        addButton.transform.SetAsLastSibling();
        var ale = addButton.GetComponent<LayoutElement>() ?? addButton.gameObject.AddComponent<LayoutElement>();
        ale.preferredWidth = 110; ale.minWidth = 110; ale.flexibleWidth = 0;
        ale.preferredHeight = 34; ale.minHeight = 34;
        var addImg = addButton.GetComponent<Image>();
        if (addImg != null) addImg.color = new Color32(0x5C, 0x6E, 0x85, 0xFF);
        var addLbl = addButton.GetComponentInChildren<TMP_Text>(true);
        if (addLbl != null) addLbl.color = Color.white;

        // La section de la fiche BÂTIMENT ne pilotait pas la hauteur de ses enfants (prefab :
        // childControlHeight = false) : le tableau restait à 100, la taille par défaut d'un objet
        // créé par code (retour du 07/10). Seuls la ligne de filtres et le tableau sont actifs.
        var vlgSection = GetComponent<VerticalLayoutGroup>();
        if (vlgSection != null) { vlgSection.childControlHeight = true; vlgSection.childForceExpandHeight = false; }

        // Tableau à la place de l'ancienne liste, dans UN défilement commun aux trois colonnes
        // (07/10 : « le défilement doit être général sur objectif, pas colonne par colonne »),
        // hauteur plafonnée à environ 5 cartes, réduite au contenu s'il y en a moins.
        var liste = row.parent.Find("ObjectifList");

        // En-têtes fixes des trois colonnes, au-dessus du défilement (07/10).
        var entetes = UIFactory.HBox(row.parent, 10, true, "EnTetesColonnes");
        entetes.childControlHeight = true; entetes.childForceExpandHeight = true;
        for (int i = 0; i < 3; i++) _entetes[i] = ObjectifsTableau.CreerEnTete(entetes.transform);

        var defil = UIFactory.Rect("TableauDefilement", row.parent);
        if (liste != null)
        {
            entetes.transform.SetSiblingIndex(liste.GetSiblingIndex() + 1);
            defil.SetSiblingIndex(entetes.transform.GetSiblingIndex() + 1);
            liste.gameObject.SetActive(false);
        }
        var sr = defil.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 28;
        sr.movementType = ScrollRect.MovementType.Clamped;
        _hauteur = UIFactory.LE(defil.gameObject, minH: HAUTEUR_MAX, prefH: HAUTEUR_MAX, flexH: 0);
        // Vraie largeur minimale (3 colonnes + espacements) : ColonnesAdaptatives n'empile les
        // deux grandes colonnes de la fiche que si chacune dit la vérité sur son minimum. Sans
        // ça, à 175 % elles restaient côte à côte et le tableau était coupé (retour du 07/10).
        _hauteur.minWidth = 3 * ObjectifsTableau.LARGEUR_MIN_COLONNE + 2 * 10f;
        var viewport = UIFactory.Rect("Viewport", defil);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var tab = UIFactory.HBox(viewport, 10, true, "Tableau");
        tab.childForceExpandHeight = true;
        tab.childAlignment = TextAnchor.UpperLeft;
        tab.padding = new RectOffset(0, 0, 4, 8);
        var crt = (RectTransform)tab.transform;
        crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0, 1);
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        tab.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = viewport; sr.content = crt;
        _tableau = crt;
        for (int i = 0; i < 3; i++) _colonnes[i] = ObjectifsTableau.CreerColonne(_tableau, ObjectifsTableau.Colonnes[i]);
    }

    private void Chip(Transform row, string texte, Objective.Importance? im, bool auto = false)
    {
        var b = UIFactory.Button(row, texte, Color.white, UITheme.TextePrincipal, 34, UITheme.Role.Action, false);
        b.image.pixelsPerUnitMultiplier = 2.5f;   // coins à l'échelle d'une pastille, même hauteur que « + Ajouter »
        UIFactory.LargeurDuTexte(b);
        b.onClick.AddListener(() => { _importance = im; _seulementAuto = auto; _remonter = true; RefreshList(); });
        _chips.Add((b, im, auto));
    }

    // ── Affichage ─────────────────────────────────────────────────────────────

    public void RefreshList()
    {
        // Pas encore construit (Start rafraîchira), ou colonnes perdues par un rechargement du
        // code en cours de jeu (07/10 : NullReferenceException dans Vider).
        if (_tableau == null || _colonnes.Any(c => c == null)) return;
        var auj = DateTime.Today;

        var tuileAuto = ATraiterFiltre.Tuiles[(int)ATraiterFiltre.Filtre.Automatiques];
        foreach (var (b, im, seulAuto) in _chips)
        {
            bool on = seulAuto ? _seulementAuto : !_seulementAuto && _importance == im;
            Color fond = seulAuto ? tuileAuto.fond : im.HasValue ? ObjectiveItem.FondImportance(im.Value) : (Color)new Color32(0xEC, 0xEA, 0xE3, 0xFF);
            Color encre = seulAuto ? tuileAuto.encre : im.HasValue ? ObjectiveItem.TexteImportance(im.Value) : (Color)new Color32(0x2C, 0x3E, 0x5E, 0xFF);
            b.GetComponent<Image>().color = on ? encre : fond;
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.color = on ? Color.white : encre;
        }

        Fiche(out var bp, out var loc);
        // Cartes automatiques : même règle que « À traiter » (08/10) — visibles avec « Toutes »,
        // « Rappel » et « Automatique » (seules alors), cachées par « Obligatoire » et « Tâche ».
        var filtre = _seulementAuto ? ATraiterFiltre.Filtre.Automatiques : ATraiterFiltre.Filtre.Tout;
        var auto = new List<HomeAlert>();
        if (bp != null && !(loc?.archive ?? false))
            foreach (var a in HomeAlertCollector.Collect(new[] { bp }))
                if (a.kind != HomeAlert.Kind.Objectif && a.locataire != null && (loc == null || a.locataire == loc)
                    && ATraiterFiltre.Garde(a, filtre, _importance, auj))
                    auto.Add(a);

        for (int i = 0; i < 3; i++)
        {
            var colonne = ObjectifsTableau.Colonnes[i];
            var col = _colonnes[i];
            ObjectifsTableau.Vider(col);
            var cartes = _seulementAuto ? new List<Objective>() : Objectifs.Colonne(Items, colonne, _importance, auj);
            int retards = cartes.Count(o => Objectifs.Etat(o, auj) == Objectifs.EtatEcheance.EnRetard);
            bool afaire = colonne == Objective.Avancement.AFaire;
            var entete = _entetes[i] != null ? (Transform)_entetes[i] : col;
            ObjectifsTableau.Vider(entete);
            ObjectifsTableau.EnTete(entete, colonne, cartes.Count + (afaire ? auto.Count : 0), retards);

            if (afaire)
                foreach (var a in auto) ObjectifsTableau.CarteAuto(col, a, loc == null);

            var visibles = colonne == Objective.Avancement.Fait && !_historique ? cartes.Take(FAITS_VISIBLES).ToList() : cartes;
            foreach (var o in visibles) Carte(col, o, auj);

            if (colonne == Objective.Avancement.Fait && cartes.Count > FAITS_VISIBLES)
                ObjectifsTableau.Lien(col, _historique ? "Réduire" : $"Voir tout l'historique ({cartes.Count})",
                    () => { _historique = !_historique; RefreshList(); });
            if (cartes.Count == 0 && !(afaire && auto.Count > 0))
                ObjectifsTableau.Note(col, "Rien ici");
        }

        LayoutRebuilder.MarkLayoutForRebuild(_tableau);
        if (isActiveAndEnabled) StartCoroutine(AjusterHauteur());
    }

    // Hauteur du défilement = celle du tableau, plafonnée : mesurée à l'image suivante, une
    // fois la largeur des colonnes connue (le texte des cartes passe à la ligne selon elle).
    private System.Collections.IEnumerator AjusterHauteur()
    {
        yield return null;
        if (_tableau == null || _hauteur == null) yield break;
        var sr = _hauteur.GetComponent<ScrollRect>();

        // Largeur : au gros zoom (ou fenêtre étroite), si les trois colonnes n'ont plus leur
        // minimum, le tableau garde sa largeur minimale et défile aussi en largeur.
        float offerte = ((RectTransform)_tableau.parent).rect.width;
        _largeurVue = offerte;
        float mini = LayoutUtility.GetMinWidth(_tableau);
        bool large = mini > offerte + 1f;
        _tableau.anchorMax = new Vector2(large ? 0 : 1, 1);
        _tableau.sizeDelta = new Vector2(large ? mini : 0, _tableau.sizeDelta.y);
        if (sr != null) sr.horizontal = large;

        LayoutRebuilder.ForceRebuildLayoutImmediate(_tableau);
        float contenu = LayoutUtility.GetPreferredHeight(_tableau);
        float h = Mathf.Min(contenu, HAUTEUR_MAX);
        // Tout tient : le défilement interne est coupé, la molette fait défiler la fiche.
        if (sr != null)
        {
            sr.enabled = large || contenu > HAUTEUR_MAX + 1f;
            if (!sr.enabled || _remonter) { _tableau.anchoredPosition = Vector2.zero; sr.velocity = Vector2.zero; }
        }
        _remonter = false;
        if (Mathf.Abs(_hauteur.preferredHeight - h) < 1f) yield break;
        _hauteur.minHeight = _hauteur.preferredHeight = h;
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)_hauteur.transform);
    }

    private void Carte(Transform col, Objective o, DateTime auj)
    {
        ObjectifsTableau.Carte(col, o, null, auj,
            ouvrir: () => Ouvrir(o),
            avancer: () =>
            {
                var cible = o.avancement switch
                {
                    Objective.Avancement.AFaire => Objective.Avancement.EnCours,
                    Objective.Avancement.EnCours => Objective.Avancement.Fait,
                    _ => Objective.Avancement.AFaire
                };
                Deplacer(o, cible);
            },
            colonneSous: ColonneSous,
            deposer: i => Deplacer(o, ObjectifsTableau.Colonnes[i]));
    }

    private int ColonneSous(Vector2 ecran, Camera cam)
    {
        for (int i = 0; i < 3; i++)
            if (_colonnes[i] != null && RectTransformUtility.RectangleContainsScreenPoint(_colonnes[i], ecran, cam)) return i;
        return -1;
    }

    private void Deplacer(Objective o, Objective.Avancement cible)
    {
        if (o.avancement == cible) return;
        var s = Objectifs.Deplacer(Items, o, cible, DateTime.Today);
        Enregistrer();
        if (s != null)
            UndoToast.Instance?.ShowInfo($"Fait. Prochain « {s.text} » le {Objectifs.Echeance(s):dd/MM/yyyy}.");
    }

    private void OnDeleted(Objective obj)
    {
        var annuler = Objectifs.Supprimer(Items, obj);
        Enregistrer();
        UndoToast.Instance?.Show("Objectif supprimé", () =>
        {
            annuler();
            if (this == null) return;   // fiche détruite entre-temps
            Enregistrer();
        });
    }

    public ObjectiveList GetAllTheObjectif() => _objectives;

    public void LoadObjectives(ObjectiveList listofObjectif)
    {
        if (listofObjectif?.items != null)
            _objectives = listofObjectif;
        Objectifs.Migrer(_objectives);
        RefreshList();
    }
}

[Serializable]
public class ObjectiveList
{
    public List<Objective> items = new List<Objective>();
}
