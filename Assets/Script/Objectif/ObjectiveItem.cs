using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// Une ligne d'objectif (fiche et vue de tous les objectifs) : liseré + badge d'importance,
/// texte et ligne de détail (échéance · récurrence · documents), bouton d'avancement.
/// Clic sur le texte : fenêtre « Modifier l'objectif ».
public class ObjectiveItem : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text objectiveText;
    public TMP_Text statusBadge;      // badge d'importance
    public Button statusButton;       // avancement : À faire › En cours › Fait › (rouvert)
    public Button deleteButton;
    public Image backgroundImage;
    public Image statusIndicator;     // liseré coloré à gauche (accent saturé)
    public Image statusBadgeBg;       // fond pastel du badge d'importance

    [Header("Source (optionnel — menu général uniquement)")]
    public TMP_Text txtSource;

    private Objective _objective;
    private Action<Objective> _onAvancer, _onDeleted, _onOuvrir;
    private bool _prepare;

    // Paires fond pastel / texte foncé, alignées sur le thème
    private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    private static readonly Color BgGris = Hex("#F1EFE8"), TxGris = Hex("#5F5E5A"), AcGris = Hex("#B4B2A9");
    private static readonly Color BgBleu = Hex("#E6F1FB"), TxBleu = Hex("#185FA5");
    private static readonly Color BgVert = Hex("#E1F5EE"), TxVert = Hex("#0F6E56");
    private static readonly Color BgCorail = Hex("#FAECE7"), TxCorail = Hex("#712B13"), AcCorail = Hex("#D85A30");
    private static readonly Color BgAmbre = Hex("#FAEEDA"), TxAmbre = Hex("#633806"), AcAmbre = Hex("#EF9F27");
    private static readonly Color AcRouge = Hex("#E24B4A");

    public void Setup(Objective obj, Action<Objective> onAvancer, Action<Objective> onDeleted,
        Action<Objective> onOuvrir, string source = null)
    {
        _objective = obj;
        _onAvancer = onAvancer;
        _onDeleted = onDeleted;
        _onOuvrir = onOuvrir;
        Preparer();

        if (txtSource != null)
        {
            txtSource.gameObject.SetActive(!string.IsNullOrEmpty(source));
            txtSource.text = source ?? "";
        }

        Refresh();

        statusButton.onClick.RemoveAllListeners();
        deleteButton.onClick.RemoveAllListeners();
        statusButton.onClick.AddListener(() => _onAvancer?.Invoke(_objective));
        deleteButton.onClick.AddListener(() => _onDeleted?.Invoke(_objective));
    }

    // Une fois par ligne : bouton d'avancement en texte (plus le « » » d'origine), texte
    // cliquable (ouvre la fenêtre), hauteur pour la ligne de détail.
    private void Preparer()
    {
        if (_prepare) return;
        _prepare = true;

        var le = statusButton.GetComponent<LayoutElement>() ?? statusButton.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = 84;
        var lbl = statusButton.GetComponentInChildren<TMP_Text>(true);
        if (lbl != null) { lbl.fontSize = UITheme.Role.Pastille; lbl.enableWordWrapping = false; }

        // Toute la ligne est cliquable (le clic sur le texte remonte à elle) ; les boutons
        // d'avancement et de suppression gardent le leur.
        objectiveText.richText = true;
        if (GetComponent<Button>() == null)   // teinte d'état effacée : voir ObjectifsTableau.Cliquable
            ObjectifsTableau.Cliquable(gameObject, () => _onOuvrir?.Invoke(_objective));

        var rle = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        rle.minHeight = Mathf.Max(rle.minHeight, 50);
        rle.preferredHeight = Mathf.Max(rle.preferredHeight, 54);
    }

    public void Refresh()
    {
        var o = _objective;
        var auj = DateTime.Today;
        var etat = Objectifs.Etat(o, auj);

        string titre = o.Fait ? $"<s>{Echapper(o.text)}</s>" : Echapper(o.text);
        string coulDetail = etat == Objectifs.EtatEcheance.EnRetard ? "#A32D2D"
                          : etat == Objectifs.EtatEcheance.Proche ? "#854F0B" : "#888780";
        objectiveText.text = $"{titre}\n<size=80%><color={coulDetail}>{Objectifs.Detail(o, auj)}</color></size>";
        objectiveText.fontStyle = FontStyles.Normal;
        objectiveText.color = o.Fait ? new Color(0.45f, 0.45f, 0.42f, 0.8f) : UITheme.TextePrincipal;

        statusBadge.text = LibelleImportance(o.importance);
        statusBadge.color = TexteImportance(o.importance);
        if (statusBadgeBg != null) statusBadgeBg.color = FondImportance(o.importance);
        statusIndicator.color = etat == Objectifs.EtatEcheance.EnRetard ? AcRouge : AccentImportance(o.importance);

        var img = statusButton.GetComponent<Image>();
        if (img != null) img.color = FondAvancement(o.avancement);
        var lbl = statusButton.GetComponentInChildren<TMP_Text>(true);
        if (lbl != null) { lbl.text = LibelleAvancement(o.avancement); lbl.color = TexteAvancement(o.avancement); }
    }

    // Un « < » saisi ne doit pas ouvrir une balise de texte enrichi.
    static string Echapper(string s) => $"<noparse>{s}</noparse>";

    // ── Couleurs et libellés (aussi utilisés par « À traiter ») ────────────────

    public static string LibelleImportance(Objective.Importance i) => i switch
    {
        Objective.Importance.Obligatoire => "Obligatoire",
        Objective.Importance.Rappel => "Rappel",
        _ => "Tâche"   // libellé du 07/10 (valeur interne : Normale)
    };
    public static Color FondImportance(Objective.Importance i) => i switch
    {
        Objective.Importance.Obligatoire => BgCorail,
        Objective.Importance.Rappel => BgAmbre,
        _ => BgGris
    };
    public static Color TexteImportance(Objective.Importance i) => i switch
    {
        Objective.Importance.Obligatoire => TxCorail,
        Objective.Importance.Rappel => TxAmbre,
        _ => TxGris
    };
    public static Color AccentImportance(Objective.Importance i) => i switch
    {
        Objective.Importance.Obligatoire => AcCorail,
        Objective.Importance.Rappel => AcAmbre,
        _ => AcGris
    };
    public static Color AccentRetard => AcRouge;

    public static string LibelleAvancement(Objective.Avancement a) => a switch
    {
        Objective.Avancement.EnCours => "En cours",
        Objective.Avancement.Fait => "Fait",   // pas de « ✓ » : absent de la police
        _ => "À faire"
    };
    static Color FondAvancement(Objective.Avancement a) => a switch
    {
        Objective.Avancement.EnCours => BgBleu,
        Objective.Avancement.Fait => BgVert,
        _ => BgGris
    };
    static Color TexteAvancement(Objective.Avancement a) => a switch
    {
        Objective.Avancement.EnCours => TxBleu,
        Objective.Avancement.Fait => TxVert,
        _ => TxGris
    };
}
