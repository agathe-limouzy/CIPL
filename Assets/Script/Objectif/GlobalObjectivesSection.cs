using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Vue de tous les objectifs (menu général). Lit et enregistre par les fiches
/// (BatimentPrefab), comme « À traiter » : chaque fiche travaille sur sa copie des
/// données, une modification faite ailleurs serait écrasée à son prochain enregistrement.
public class GlobalObjectivesSection : MonoBehaviour
{
    [Header("Liste")]
    public Transform listContent;
    public GameObject objectiveItemPrefab;
    public TMP_Text txtTotalActifs;
    public TMP_Text txtUrgents;
    public GameObject emptyText;

    [Header("Filtres")]
    public Button btnTous;
    public Button btnObligatoire;
    public Button btnRappel;
    public Button btnEnCours;   // → « En retard »
    public Button btnAFaire;    // → « Normale »

    private class Entry
    {
        public Objective Obj;
        public string Source, NomBat, NomLoc;
        public BatimentPrefab Bp;
        public Locataire Loc;            // null pour un objectif de bâtiment
        public List<Objective> Liste;    // liste d'origine (bâtiment ou locataire)
    }

    private List<Entry> _entries = new();
    private List<GameObject> _rows = new();
    // Filtres (maquette validée le 06/10) : tuiles d'avancement avec leur nombre, pastilles
    // d'importance dessous ; les deux se combinent. Les anciens boutons sont masqués.
    private Objectifs.FiltreAvancement _avancement = Objectifs.FiltreAvancement.Tous;
    private Objective.Importance? _importance;
    private readonly List<(Image fond, TMP_Text nombre, TMP_Text libelle, Objectifs.FiltreAvancement av)> _tuiles = new();
    private readonly List<(Button b, Objective.Importance? im)> _chips = new();

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    static (Color fond, Color encre) CouleursTuile(Objectifs.FiltreAvancement av) => av switch
    {
        Objectifs.FiltreAvancement.AFaire => (Hex("#F1EFE8"), Hex("#5F5E5A")),
        Objectifs.FiltreAvancement.EnCours => (Hex("#E6F1FB"), Hex("#185FA5")),
        Objectifs.FiltreAvancement.EnRetard => (Hex("#FCEBEB"), Hex("#A32D2D")),
        Objectifs.FiltreAvancement.Faits => (Hex("#E1F5EE"), Hex("#0F6E56")),
        _ => (Hex("#ECEAE3"), Hex("#2C3E5E"))
    };

    private void Awake()
    {
        var row = btnTous != null ? btnTous.transform.parent : null;
        if (row == null) return;
        foreach (var b in new[] { btnTous, btnObligatoire, btnRappel, btnEnCours, btnAFaire })
            if (b != null) b.gameObject.SetActive(false);

        // Tuiles d'avancement dans la ligne des filtres.
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.spacing = 8;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
        }
        UIFactory.LE(row.gameObject, minH: 58, prefH: 58, flexH: 0);
        foreach (var (av, nom) in new[] {
            (Objectifs.FiltreAvancement.Tous, "Tous"), (Objectifs.FiltreAvancement.AFaire, "À faire"),
            (Objectifs.FiltreAvancement.EnCours, "En cours"), (Objectifs.FiltreAvancement.EnRetard, "En retard"),
            (Objectifs.FiltreAvancement.Faits, "Faits") })
        {
            var tuile = UIFactory.Panel("Tuile " + nom, row, CouleursTuile(av).fond);
            tuile.pixelsPerUnitMultiplier = 2f;
            var v = tuile.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 4, 4); v.spacing = 0;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.MiddleLeft;
            UIFactory.LE(tuile.gameObject, flexW: 1, minW: 0);
            var n = UIFactory.Text(tuile.transform, "0", UITheme.Role.Valeur, CouleursTuile(av).encre, true);
            var l = UIFactory.Text(tuile.transform, nom, UITheme.Role.Donnee, CouleursTuile(av).encre);
            var a = av;
            ObjectifsTableau.Cliquable(tuile.gameObject, () => { _avancement = a; Rebuild(); });
            _tuiles.Add((tuile, n, l, av));
        }

        // Pastilles d'importance, sur une ligne juste en dessous.
        var imp = UIFactory.HBox(row.parent, 6, false, "Importance");
        imp.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);
        UIFactory.LE(imp.gameObject, minH: 34, prefH: 34, flexH: 0);
        UIFactory.Text(imp.transform, "Catégorie", UITheme.Role.Mention, UITheme.TexteSecondaire).enableWordWrapping = false;
        foreach (var (im, nom) in new (Objective.Importance?, string)[] {
            (null, "Toutes"), (Objective.Importance.Obligatoire, "Obligatoire"),
            (Objective.Importance.Rappel, "Rappel"), (Objective.Importance.Normale, "Tâche") })
        {
            var b = UIFactory.Button(imp.transform, nom, Color.white, UITheme.TextePrincipal, 30, UITheme.Role.Action, false);
            b.image.pixelsPerUnitMultiplier = 2.5f;   // coins à l'échelle d'une pastille de 30 px
            UIFactory.LargeurDuTexte(b);
            var i = im;
            b.onClick.AddListener(() => { _importance = i; Rebuild(); });
            _chips.Add((b, im));
        }
    }

    public void Refresh()
    {
        _entries.Clear();

        foreach (var bp in BatimentManager.Instance.BatimentPrefab)
        {
            var bat = bp?.getBatiment();
            if (bat == null) continue;
            string nomBat = string.IsNullOrEmpty(bat.Name) ? "Bâtiment" : bat.Name;

            if (bat.objectifs?.items != null)
                foreach (var obj in bat.objectifs.items)
                    _entries.Add(new Entry
                    {
                        Obj = obj, Source = Objectifs.Origine(nomBat, null), NomBat = bat.Name,
                        Bp = bp, Liste = bat.objectifs.items
                    });

            foreach (var loc in bp.listLocataire)
            {
                if (loc == null || loc.archive || loc.objectifs?.items == null) continue;   // archivé : masqué, comme « À traiter »
                string nomLoc = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;
                foreach (var obj in loc.objectifs.items)
                    _entries.Add(new Entry
                    {
                        Obj = obj, Source = Objectifs.Origine(nomBat, nomLoc), NomBat = bat.Name, NomLoc = loc.Name,
                        Bp = bp, Loc = loc, Liste = loc.objectifs.items
                    });
            }
        }

        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var r in _rows) Destroy(r);
        _rows.Clear();

        var auj = DateTime.Today;
        var filtered = _entries.Where(e => Objectifs.Garde(e.Obj, _avancement, _importance, auj)).ToList();
        if (_avancement == Objectifs.FiltreAvancement.Faits)   // historique : le plus récent d'abord
            filtered = filtered.OrderByDescending(e => Objectifs.Date(e.Obj.faitLeISO) ?? DateTime.MinValue).ToList();
        else
            filtered.Sort((a, b) => Objectifs.Comparer(a.Obj, b.Obj, auj));

        // Tuiles : leur nombre tient compte de l'importance choisie ; la tuile active est pleine.
        foreach (var (fond, nombre, libelle, av) in _tuiles)
        {
            bool on = av == _avancement;
            var (f, encre) = CouleursTuile(av);
            fond.color = on ? encre : f;
            nombre.text = _entries.Count(e => Objectifs.Garde(e.Obj, av, _importance, auj)).ToString();
            nombre.color = libelle.color = on ? Color.white : encre;
        }
        foreach (var (b, im) in _chips)
        {
            bool on = _importance == im;
            Color f = im.HasValue ? ObjectiveItem.FondImportance(im.Value) : Hex("#ECEAE3");
            Color encre = im.HasValue ? ObjectiveItem.TexteImportance(im.Value) : Hex("#2C3E5E");
            b.GetComponent<Image>().color = on ? encre : f;
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.color = on ? Color.white : encre;
        }

        int actifs = _entries.Count(e => !e.Obj.Fait);
        int retards = _entries.Count(e => Objectifs.Etat(e.Obj, auj) == Objectifs.EtatEcheance.EnRetard);
        int urgents = _entries.Count(e => !e.Obj.Fait && e.Obj.importance == Objective.Importance.Obligatoire);

        if (txtTotalActifs != null)
            txtTotalActifs.text = $"{actifs} objectif{(actifs > 1 ? "s" : "")} actif{(actifs > 1 ? "s" : "")}";
        if (txtUrgents != null)
        {
            var parts = new List<string>();
            if (retards > 0) parts.Add($"{retards} en retard");
            if (urgents > 0) parts.Add($"{urgents} obligatoire{(urgents > 1 ? "s" : "")}");
            txtUrgents.text = parts.Count > 0 ? "Urgent : " + string.Join(" · ", parts) : "";
        }

        emptyText?.SetActive(filtered.Count == 0);

        foreach (var entry in filtered)
        {
            var go = Instantiate(objectiveItemPrefab, listContent);
            var item = go.GetComponent<ObjectiveItem>();
            var e = entry;
            item.Setup(e.Obj,
                onAvancer: obj =>
                {
                    var s = Objectifs.Avancer(e.Liste, obj, DateTime.Today);
                    Enregistrer(e);
                    if (s != null)
                        UndoToast.Instance?.ShowInfo($"Fait. Prochain « {s.text} » le {Objectifs.Echeance(s):dd/MM/yyyy}.");
                },
                onDeleted: obj => OnDeleted(obj, e),
                onOuvrir: obj => ObjectifPanel.Ouvrir(transform, e.Liste, obj, e.NomBat, e.NomLoc,
                    () => Enregistrer(e), o => OnDeleted(o, e)),
                source: e.Source);
            _rows.Add(go);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent.GetComponent<RectTransform>());
        StartCoroutine(RebuildLayout());
    }

    private IEnumerator RebuildLayout()
    {
        // Frame 1 : attend que les Destroy soient effectifs
        yield return null;

        // Frame 2 : force Unity à recalculer tous les layouts
        yield return null;

        Canvas.ForceUpdateCanvases();

        // Rebuild de bas en haut depuis listContent jusqu'au Canvas
        Transform t = listContent;
        while (t != null)
        {
            var rt = t.GetComponent<RectTransform>();
            if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            if (t.GetComponent<Canvas>() != null) break;
            t = t.parent;
        }

        Canvas.ForceUpdateCanvases();

        // Notifie le ScrollAutoResize si présent sur un parent
        var scrollAutoResizes = GetComponentsInParent<ScrollAutoResize>(true);
        foreach (var sar in scrollAutoResizes)
            sar.SetDirty();
    }

    // Enregistre par la fiche, et rafraîchit sa liste d'objectifs et la vue.
    private void Enregistrer(Entry e)
    {
        ObjectifPanel.EnregistrerFiche(e.Bp, e.Loc);
        if (this != null) Refresh();
    }

    private void OnDeleted(Objective obj, Entry e)
    {
        var annuler = Objectifs.Supprimer(e.Liste, obj);
        Enregistrer(e);
        UndoToast.Instance?.Show("Objectif supprimé", () =>
        {
            annuler();
            Enregistrer(e);
        });
    }

    static void Libelle(Button b, string texte)
    {
        var t = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = texte;
    }
}
