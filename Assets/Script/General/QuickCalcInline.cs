using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Mini-calculateur immobilier inline dans le menu home.
/// Les pastilles changent le mode ; les champs de saisie s'adaptent au calcul choisi.
public class QuickCalcInline : MonoBehaviour
{
    [Serializable]
    public class InputSlot
    {
        public GameObject root;    // conteneur label+input, activé/désactivé selon le mode
        public TMP_Text label;
        public TMP_InputField input;
    }

    [Header("Saisie (4 emplacements réutilisés)")]
    public InputSlot[] slots;

    [Header("Résultat")]
    public TMP_Text txtResultat;

    [Header("Modes (pastilles)")]
    public Button[] modeButtons;      // même ordre que les modes ci-dessous
    public Image[] modeButtonBg;      // fonds des pastilles (pour surligner l'actif)

    private int _mode = 0;

    // ── Définition des modes ────────────────────────────────────────────────
    private class Mode
    {
        public string nom;
        public string[] labels;                 // libellés des champs utilisés (1 à 4)
        public string unite;
        public Func<float[], float> calcul;
    }

    private static readonly Mode[] Modes =
    {
        new Mode {
            nom = "Rentabilité",
            labels = new[] { "Prix (€)", "Loyer annuel (€)" },
            unite = "%",
            calcul = v => v[0] > 0 ? v[1] / v[0] * 100f : 0f
        },
        new Mode {
            nom = "Prix d'achat réel",
            labels = new[] { "Prix (€)", "Droits (%)", "Autres (%)" },
            unite = "€",
            calcul = v => v[0] * (1f + v[1] / 100f + v[2] / 100f)
        },
        new Mode {
            nom = "Prix cible",
            labels = new[] { "Loyer annuel (€)", "Rentabilité (%)" },
            unite = "€",
            calcul = v => v[1] > 0 ? v[0] / v[1] * 100f : 0f
        },
        new Mode {
            nom = "Loyer cible",
            labels = new[] { "Prix (€)", "Rentabilité (%)" },
            unite = "€",
            calcul = v => v[0] / 100f * v[1]
        },
        new Mode {
            // Revue 17 (D8-07, lot G ; décision du 16/11) : « Rentabilité nette » ne retirait aucune charge : le nom dit ce qui est calculé.
            nom = "Rentabilité frais inclus",
            labels = new[] { "Prix (€)", "Droits (%)", "Autres (%)", "Loyer annuel (€)" },
            unite = "%",
            calcul = v => {
                float prixReel = v[0] * (1f + v[1] / 100f + v[2] / 100f);
                return prixReel > 0 ? v[3] / prixReel * 100f : 0f;
            }
        },
        new Mode {
            nom = "Loyer au m²",
            labels = new[] { "Loyer (€)", "Surface (m²)" },
            unite = "€/m²",
            calcul = v => v[1] > 0 ? v[0] / v[1] : 0f
        },
    };

    /// Revue 17 (D8-19, lot G) : les formules, sans écran (tests). `valeurs` : les quatre cases, dans l'ordre des libellés.
    public static int NbModes => Modes.Length;
    public static string NomMode(int mode) => Modes[mode].nom;
    public static float Resultat(int mode, float[] valeurs) => Modes[mode].calcul(valeurs);

    private static readonly Color ActifBg   = UITheme.Primaire;
    private static readonly Color ActifTxt  = UITheme.PrimaireClair;
    private static readonly Color InactifBg = Color.white;
    private static readonly Color InactifTxt = UITheme.Hex("#085041");

    private void Start()
    {
        if (slots != null)
            foreach (var s in slots)
                if (s?.input != null)
                    s.input.onValueChanged.AddListener(_ => Calculer());

        if (modeButtons != null)
            for (int i = 0; i < modeButtons.Length; i++)
            {
                int idx = i;
                if (modeButtons[i] != null)
                {
                    modeButtons[i].onClick.RemoveAllListeners();
                    modeButtons[i].onClick.AddListener(() => SetMode(idx));
                    // Lot G (D8-07) : le nom de la pastille vient du mode (avant : écrit à part dans la scène).
                    var nom = modeButtons[i].GetComponentInChildren<TMP_Text>(true);
                    if (nom != null && i < Modes.Length) nom.text = Modes[i].nom;
                    // La pastille prend la largeur de son nom (12 de marge de chaque côté) : « Rentabilité frais inclus » demandait 146
                    // dans une pastille de 147, et les six tiennent ainsi dans la rangée (711 pour 728).
                    var lePastille = modeButtons[i].GetComponent<LayoutElement>();
                    if (nom != null && lePastille != null) lePastille.preferredWidth = nom.GetPreferredValues(nom.text).x + 24f;
                }
            }

        SetMode(0);
    }

    public void SetMode(int mode)
    {
        if (mode < 0 || mode >= Modes.Length) return;
        _mode = mode;
        var m = Modes[mode];

        // Configure les slots
        for (int i = 0; i < slots.Length; i++)
        {
            bool used = i < m.labels.Length;
            if (slots[i]?.root != null) slots[i].root.SetActive(used);
            if (used)
            {
                if (slots[i].label != null) slots[i].label.text = m.labels[i];
                // Lot G (B-16) : la case prend la largeur de son libellé (« Loyer annuel (€) » : 131 dans une case de 120).
                var le = slots[i].root != null ? slots[i].root.GetComponent<LayoutElement>() : null;
                if (le != null && slots[i].label != null) le.preferredWidth = Mathf.Max(120f, slots[i].label.GetPreferredValues(m.labels[i]).x + 4f);
                if (slots[i].input != null) slots[i].input.text = "";
            }
        }

        // Surligne la pastille active
        if (modeButtonBg != null)
            for (int i = 0; i < modeButtonBg.Length; i++)
            {
                if (modeButtonBg[i] != null) modeButtonBg[i].color = i == mode ? ActifBg : InactifBg;
                var txt = modeButtons != null && i < modeButtons.Length
                    ? modeButtons[i].GetComponentInChildren<TMP_Text>() : null;
                if (txt != null) txt.color = i == mode ? ActifTxt : InactifTxt;
            }

        Calculer();
    }

    private void Calculer()
    {
        var m = Modes[_mode];
        var v = new float[4];
        for (int i = 0; i < slots.Length && i < 4; i++)
            v[i] = i < m.labels.Length ? Parse(slots[i].input) : 0f;

        float res = m.calcul(v);
        if (txtResultat != null)
        {
            string fmt = m.unite == "%" ? "F2" : "N0";
            txtResultat.text = $"{res.ToString(fmt, FacturePdfService.FrCulture)} {m.unite}";
        }
    }

    private static float Parse(TMP_InputField field)
    {
        if (field == null) return 0f;
        return SaisieNumerique.Parse(field.text);
    }

}
