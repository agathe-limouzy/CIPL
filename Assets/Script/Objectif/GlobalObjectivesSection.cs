using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    public Button btnEnCours;
    public Button btnAFaire;

    private struct Entry
    {
        public Objective Obj;
        public string Source;
        public string BatimentId;
    }

    private List<Entry> _entries = new();
    private List<GameObject> _rows = new();
    private Objective.ObjectiveStatus? _filter = null;

    private void Awake()
    {
        btnTous?.onClick.AddListener(() => SetFilter(null));
        btnObligatoire?.onClick.AddListener(() => SetFilter(Objective.ObjectiveStatus.Obligatoire));
        btnRappel?.onClick.AddListener(() => SetFilter(Objective.ObjectiveStatus.Rappel));
        btnEnCours?.onClick.AddListener(() => SetFilter(Objective.ObjectiveStatus.EnCours));
        btnAFaire?.onClick.AddListener(() => SetFilter(Objective.ObjectiveStatus.AFaire));
    }

    public void Refresh()
    {
        _entries.Clear();

        foreach (var bat in BatimentManager.Instance.Batiments)
        {
            string nomBat = string.IsNullOrEmpty(bat.Name) ? "Bâtiment" : bat.Name;

            if (bat.objectifs?.items != null)
                foreach (var obj in bat.objectifs.items)
                    _entries.Add(new Entry
                    {
                        Obj = obj,
                        Source = $"🏢 {nomBat}",
                        BatimentId = bat.id
                    });

            foreach (var loc in bat.locataireDuBatiment)
            {
                if (loc.objectifs?.items == null) continue;
                string nomLoc = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;

                foreach (var obj in loc.objectifs.items)
                    _entries.Add(new Entry
                    {
                        Obj = obj,
                        Source = $"🏢 {nomBat}  ›  👤 {nomLoc}",
                        BatimentId = bat.id
                    });
            }
        }

        Rebuild();
    }

    private void SetFilter(Objective.ObjectiveStatus? filter)
    {
        _filter = filter;
        Rebuild();
    }

    private void Rebuild()
    {

        foreach (var r in _rows) Destroy(r);
        _rows.Clear();

        var filtered = _entries
            .Where(e => e.Obj.status != Objective.ObjectiveStatus.Fait)
            .Where(e => !_filter.HasValue || e.Obj.status == _filter.Value)
            .OrderBy(e => e.Obj.status switch
            {
                Objective.ObjectiveStatus.Obligatoire => 0,
                Objective.ObjectiveStatus.Rappel => 1,
                Objective.ObjectiveStatus.EnCours => 2,
                _ => 3
            })
            .ToList();

        int actifs = _entries.Count(e => e.Obj.status != Objective.ObjectiveStatus.Fait);
        int urgents = _entries.Count(e => e.Obj.status == Objective.ObjectiveStatus.Obligatoire);

        if (txtTotalActifs != null)
            txtTotalActifs.text = $"{actifs} objectif{(actifs > 1 ? "s" : "")} actif{(actifs > 1 ? "s" : "")}";
        if (txtUrgents != null)
            txtUrgents.text = urgents > 0 ? $"⚠ {urgents} obligatoire{(urgents > 1 ? "s" : "")}" : "";

        emptyText?.SetActive(filtered.Count == 0);

        foreach (var entry in filtered)
        {
            var go = Instantiate(objectiveItemPrefab, listContent);
            var item = go.GetComponent<ObjectiveItem>();
            item.Setup(entry.Obj,
                onStatusChanged: obj => OnStatusChanged(entry.BatimentId),
                onDeleted: obj => OnDeleted(obj, entry.BatimentId),
                source: entry.Source);
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

    private void OnStatusChanged(string batimentId)
    {
        var bat = BatimentManager.Instance.Batiments.Find(b => b.id == batimentId);
        if (bat != null) BatimentManager.Instance.SaveBatiment(bat);
        Rebuild();
    }

    private void OnDeleted(Objective obj, string batimentId)
    {
        var bat = BatimentManager.Instance.Batiments.Find(b => b.id == batimentId);
        if (bat == null) return;

        bat.objectifs?.items?.Remove(obj);
        foreach (var loc in bat.locataireDuBatiment)
            loc.objectifs?.items?.Remove(obj);

        BatimentManager.Instance.SaveBatiment(bat);
        Refresh();
    }
}