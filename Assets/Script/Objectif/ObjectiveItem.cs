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

    // Paires fond pastel / texte foncé, alignées sur le thème
    private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    private static readonly Color BgAFaire = Hex("#F1EFE8");
    private static readonly Color TxAFaire = Hex("#5F5E5A");
    private static readonly Color BgEnCours = Hex("#E6F1FB");
    private static readonly Color TxEnCours = Hex("#185FA5");
    private static readonly Color BgFait = Hex("#E1F5EE");
    private static readonly Color TxFait = Hex("#0F6E56");
    private static readonly Color BgObligatoire = Hex("#FAECE7");
    private static readonly Color TxObligatoire = Hex("#712B13");
    private static readonly Color BgRappel = Hex("#FAEEDA");
    private static readonly Color TxRappel = Hex("#633806");

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
        statusBadge.text = GetStatusLabel(_objective.status);
        statusBadge.color = GetStatusTextColor(_objective.status);
        statusIndicator.color = GetStatusColor(_objective.status);   // fond pastel du badge

        if (_objective.status == Objective.ObjectiveStatus.Fait)
        {
            objectiveText.fontStyle = FontStyles.Strikethrough;
            objectiveText.color = new Color(0.45f, 0.45f, 0.42f, 0.7f);
        }
        else
        {
            objectiveText.fontStyle = FontStyles.Normal;
            objectiveText.color = UITheme.TextePrincipal;
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

    /// Fond pastel du badge
    public static Color GetStatusColor(Objective.ObjectiveStatus status) => status switch
    {
        Objective.ObjectiveStatus.AFaire => BgAFaire,
        Objective.ObjectiveStatus.EnCours => BgEnCours,
        Objective.ObjectiveStatus.Fait => BgFait,
        Objective.ObjectiveStatus.Obligatoire => BgObligatoire,
        Objective.ObjectiveStatus.Rappel => BgRappel,
        _ => Color.white
    };

    /// Texte foncé du badge
    public static Color GetStatusTextColor(Objective.ObjectiveStatus status) => status switch
    {
        Objective.ObjectiveStatus.AFaire => TxAFaire,
        Objective.ObjectiveStatus.EnCours => TxEnCours,
        Objective.ObjectiveStatus.Fait => TxFait,
        Objective.ObjectiveStatus.Obligatoire => TxObligatoire,
        Objective.ObjectiveStatus.Rappel => TxRappel,
        _ => Color.black
    };

    public static string GetStatusLabel(Objective.ObjectiveStatus status) => status switch
    {
        Objective.ObjectiveStatus.AFaire => "À faire",
        Objective.ObjectiveStatus.EnCours => "En cours",
        Objective.ObjectiveStatus.Fait => "✓ Fait",
        Objective.ObjectiveStatus.Obligatoire => "Obligatoire",
        Objective.ObjectiveStatus.Rappel => "Rappel",
        _ => ""
    };
}