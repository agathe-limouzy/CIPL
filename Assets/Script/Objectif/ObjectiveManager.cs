using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ObjectivesManager : MonoBehaviour
{
    [Header("Panel principal")]
    public GameObject objectivesPanel;

    [Header("Input ajout")]
    public GameObject inputRow;
    public TMP_InputField newObjectiveInput;
    public TMP_Dropdown statusDropdown;
    public Button confirmAddButton;
    public Button addButton;

    [Header("Filtres")]
    public Button filterAllButton;
    public Button filterAFaireButton;
    public Button filterEnCoursButton;
    public Button filterObligatoireButton;
    public Button filterRappelButton;

    [Header("Liste")]
    public Transform listContent;
    public GameObject objectiveItemPrefab;
    public TMP_Text emptyText;


    // Données
    private ObjectiveList _objectives;
    private Objective.ObjectiveStatus? _currentFilter = null;

    // ✅ Pool d'items réutilisables
    private List<ObjectiveItem> _pool = new List<ObjectiveItem>();
    public UnityEvent AddNeObjectif= new UnityEvent();
    private void Start()
    {
        statusDropdown.ClearOptions();
        statusDropdown.AddOptions(new List<string>
        {
            "À FAIRE", "EN COURS", "OBLIGATOIRE", "RAPPEL"
        });

        addButton.onClick.AddListener(ToggleInputRow);
        confirmAddButton.onClick.AddListener(AddObjective);
        newObjectiveInput.onSubmit.AddListener(_ => AddObjective());

        filterAllButton.onClick.AddListener(() => SetFilter(null));
        filterAFaireButton.onClick.AddListener(() => SetFilter(Objective.ObjectiveStatus.AFaire));
        filterEnCoursButton.onClick.AddListener(() => SetFilter(Objective.ObjectiveStatus.EnCours));
        filterObligatoireButton.onClick.AddListener(() => SetFilter(Objective.ObjectiveStatus.Obligatoire));
        filterRappelButton.onClick.AddListener(() => SetFilter(Objective.ObjectiveStatus.Rappel));

        inputRow.SetActive(false);
      
       // LoadObjectives();
       // RefreshList();
    }

    private void ToggleInputRow()
    {
        inputRow.SetActive(!inputRow.activeSelf);
        if (inputRow.activeSelf)
            newObjectiveInput.ActivateInputField();
    }

    private void AddObjective()
    {
        string text = newObjectiveInput.text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (_objectives == null)
        {
            _objectives = new ObjectiveList();
        }
        if (_objectives.items == null)
        {
            _objectives.items = new List<Objective>();
        }

        Objective.ObjectiveStatus status = statusDropdown.value switch
        {
            1 => Objective.ObjectiveStatus.EnCours,
            2 => Objective.ObjectiveStatus.Obligatoire,
            3 => Objective.ObjectiveStatus.Rappel,
            _ => Objective.ObjectiveStatus.AFaire
        };

        _objectives.items.Add(new Objective(text, status));
        newObjectiveInput.text = "";
        inputRow.SetActive(false);

       // SaveObjectives();
        RefreshList();
        AddNeObjectif.Invoke();
    }

    private void SetFilter(Objective.ObjectiveStatus? filter)
    {
        _currentFilter = filter;
        RefreshList();
    }

    public void RefreshList()
    {
        Debug.Log(_objectives.items.Count);
        // ✅ Désactive tous les items du pool
        foreach (var item in _pool)
            item.gameObject.SetActive(false);

        // Filtre et tri
        var filtered = _objectives.items
            .Where(o => o.status != Objective.ObjectiveStatus.Fait)
            .Where(o => !_currentFilter.HasValue || o.status == _currentFilter.Value)
            .OrderBy(o =>
                o.status == Objective.ObjectiveStatus.Obligatoire ? 0 :
                o.status == Objective.ObjectiveStatus.Rappel ? 1 :
                o.status == Objective.ObjectiveStatus.EnCours ? 2 : 3)
            .ToList();

        emptyText.gameObject.SetActive(filtered.Count == 0);

        for (int i = 0; i < filtered.Count; i++)
        {
            ObjectiveItem item = GetOrCreatePoolItem(i);
            item.gameObject.SetActive(true);
            item.Setup(filtered[i], OnStatusChanged, OnDeleted);
        }

        // Compteur
        int total = _objectives.items.Count(o => o.status != Objective.ObjectiveStatus.Fait);
        int done = _objectives.items.Count(o => o.status == Objective.ObjectiveStatus.Fait);
        int urgent = _objectives.items.Count(o => o.status == Objective.ObjectiveStatus.Obligatoire);
      
    }

    // ✅ Récupère un item existant du pool ou en crée un nouveau
    private ObjectiveItem GetOrCreatePoolItem(int index)
    {
        if (index < _pool.Count)
            return _pool[index];

        // Crée un nouvel item et l'ajoute au pool
        var go = Instantiate(objectiveItemPrefab, listContent);
        var item = go.GetComponent<ObjectiveItem>();
        _pool.Add(item);
        return item;
    }

    private void OnStatusChanged(Objective obj)
    {
        //SaveObjectives();
        RefreshList();
    }

    private void OnDeleted(Objective obj)
    {
        _objectives.items.Remove(obj);
       // SaveObjectives();
        RefreshList();
        AddNeObjectif.Invoke();
    }

 

    public ObjectiveList GetAllTheObjectif()
    {
        // PlayerPrefs.SetString("Objectives", JsonUtility.ToJson(new ObjectiveList { items = _objectives }));
        //PlayerPrefs.Save();
        return _objectives;
    }

    public void LoadObjectives(ObjectiveList listofObjectif)
    {
     
          Debug.Log("list of objectif "+ listofObjectif.items.Count);
            if (listofObjectif?.items != null)
                _objectives = listofObjectif;
        Debug.Log(_objectives.items.Count);
        RefreshList();
      
    }
}

[Serializable]
public class ObjectiveList
{
    public List<Objective> items = new List<Objective>();
}