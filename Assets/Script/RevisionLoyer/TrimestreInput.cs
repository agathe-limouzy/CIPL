using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TrimestreInput : MonoBehaviour
{
    [Header("UI")]
    public TMP_Dropdown trimestreDropdown;
    public TMP_Dropdown anneeDropdown;
    public TMP_Text validationText;

    public string TrimestreValue { get; private set; }
    public bool IsValid { get; private set; }

    public event Action<string> OnTrimestreChanged;

    private void Start()
    {
    
        OnChanged();
    }

    private void OnChanged()
    {
        string t = trimestreDropdown.options[trimestreDropdown.value].text;
        string annee = anneeDropdown.options[anneeDropdown.value].text;

        TrimestreValue = $"{annee}-{t}";
        IsValid = true;

        if (validationText != null)
            validationText.text = $"✅ {t} {annee}";

        OnTrimestreChanged?.Invoke(TrimestreValue);
    }

    public void Init()
    {
        // ── Dropdown Trimestre ────────────────────────────────────────────────
        trimestreDropdown.ClearOptions();
        trimestreDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "T1", "T2", "T3", "T4"
        });

        // ── Dropdown Année (de 1990 à aujourd'hui) ────────────────────────────
        anneeDropdown.ClearOptions();
        var annees = new System.Collections.Generic.List<string>();
        for (int y = DateTime.Now.Year; y >= 1990; y--)
            annees.Add(y.ToString());
        anneeDropdown.AddOptions(annees);

        trimestreDropdown.onValueChanged.AddListener(_ => OnChanged());
        anneeDropdown.onValueChanged.AddListener(_ => OnChanged());
        OnChanged();

    }

    public void SetTrimestre(string normalized)
    {
        Init();
        // Format attendu : "2022-T1"
        string[] parts = normalized.Split(new string[] { "-T" },
            StringSplitOptions.None);
        if (parts.Length != 2)return;

        // Sélectionne l'année
        for (int i = 0; i < anneeDropdown.options.Count; i++)
            if (anneeDropdown.options[i].text == parts[0])
            { anneeDropdown.value = i; break; }

        // Sélectionne le trimestre (T1=0, T2=1, T3=2, T4=3)
        if (int.TryParse(parts[1], out int t))
            trimestreDropdown.value = t - 1;
    }

    public void CannotModify()
    {
        anneeDropdown.interactable = false;
        trimestreDropdown.interactable = false;
    }

    public void CanModify()
    {
        anneeDropdown.interactable = true;
        trimestreDropdown.interactable = true;
    }

    

    public string GetPreviousYear()
    {
        if (!IsValid) return "";
        string[] parts = TrimestreValue.Split(new string[] { "-T" },
            StringSplitOptions.None);
        if (parts.Length != 2) return "";
        return $"{int.Parse(parts[0]) - 1}-T{parts[1]}";
    }
}