using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ObjectiveItem : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text objectiveText;
    public TMP_Text statusBadge;
    public Button statusButton;
    public Button deleteButton;
    public Image backgroundImage;
    public Image statusIndicator;

    [Header("Source (optionnel — menu général uniquement)")]
    public TMP_Text txtSource;

    private Objective _objective;
    private Action<Objective> _onStatusChanged;
    private Action<Objective> _onDeleted;

    private static readonly Color ColorAFaire = new Color(0.55f, 0.55f, 0.60f);
    private static readonly Color ColorEnCours = new Color(0.20f, 0.55f, 0.95f);
    private static readonly Color ColorFait = new Color(0.20f, 0.78f, 0.45f);
    private static readonly Color ColorObligatoire = new Color(0.95f, 0.25f, 0.25f);
    private static readonly Color ColorRappel = new Color(0.95f, 0.65f, 0.10f);

    // ── Setup normal (bâtiment / locataire) ──────────────────────────────────

    public void Setup(Objective obj, Action<Objective> onStatusChanged, Action<Objective> onDeleted)
    {
        Setup(obj, onStatusChanged, onDeleted, source: null);
    }

    // ── Setup avec source (menu général) ─────────────────────────────────────

    public void Setup(Objective obj, Action<Objective> onStatusChanged, Action<Objective> onDeleted,
        string source)
    {
        _objective = obj;
        _onStatusChanged = onStatusChanged;
        _onDeleted = onDeleted;

        if (txtSource != null)
        {
            txtSource.gameObject.SetActive(!string.IsNullOrEmpty(source));
            txtSource.text = source ?? "";
        }

        Refresh();

        statusButton.onClick.RemoveAllListeners();
        deleteButton.onClick.RemoveAllListeners();
        statusButton.onClick.AddListener(CycleStatus);
        deleteButton.onClick.AddListener(() => _onDeleted?.Invoke(_objective));
    }

    public void Refresh()
    {
        objectiveText.text = _objective.text;
        UpdateStatusVisuals();
    }

    private void UpdateStatusVisuals()
    {
        Color statusColor = GetStatusColor(_objective.status);
        string statusLabel = GetStatusLabel(_objective.status);

        statusBadge.text = statusLabel;
        statusIndicator.color = statusColor;

        if (_objective.status == Objective.ObjectiveStatus.Fait)
        {
            objectiveText.fontStyle = FontStyles.Strikethrough;
            objectiveText.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        }
        else
        {
            objectiveText.fontStyle = FontStyles.Normal;
            objectiveText.color = Color.white;
        }
    }

    private void CycleStatus()
    {
        switch (_objective.status)
        {
            case Objective.ObjectiveStatus.AFaire: _objective.status = Objective.ObjectiveStatus.EnCours; break;
            case Objective.ObjectiveStatus.EnCours: _objective.status = Objective.ObjectiveStatus.Fait; break;
            case Objective.ObjectiveStatus.Fait: _objective.status = Objective.ObjectiveStatus.AFaire; break;
            case Objective.ObjectiveStatus.Obligatoire: _objective.status = Objective.ObjectiveStatus.EnCours; break;
            case Objective.ObjectiveStatus.Rappel: _objective.status = Objective.ObjectiveStatus.AFaire; break;
        }
        Refresh();
        _onStatusChanged?.Invoke(_objective);
    }

    public static Color GetStatusColor(Objective.ObjectiveStatus status) => status switch
    {
        Objective.ObjectiveStatus.AFaire => ColorAFaire,
        Objective.ObjectiveStatus.EnCours => ColorEnCours,
        Objective.ObjectiveStatus.Fait => ColorFait,
        Objective.ObjectiveStatus.Obligatoire => ColorObligatoire,
        Objective.ObjectiveStatus.Rappel => ColorRappel,
        _ => Color.white
    };

    public static string GetStatusLabel(Objective.ObjectiveStatus status) => status switch
    {
        Objective.ObjectiveStatus.AFaire => "À FAIRE",
        Objective.ObjectiveStatus.EnCours => "EN COURS",
        Objective.ObjectiveStatus.Fait => "✓ FAIT",
        Objective.ObjectiveStatus.Obligatoire => "⚠ OBLIGATOIRE",
        Objective.ObjectiveStatus.Rappel => "🔔 RAPPEL",
        _ => ""
    };
}