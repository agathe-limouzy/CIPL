using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Colorimétrie « groupée par fonction » appliquée à TOUTE l'app.
///
/// 🟢 Identité/contrat = vert · 🟡 Argent = ambre · 🔵 Notes = gris-bleu.
/// Posé sur le Canvas racine, re-scanne périodiquement l'UI et recolore chaque
/// carte de section reconnue (par NOM) : liseré (Image racine) + bande de titre
/// (« titre ») + icône + texte du titre. Couvre tous les écrans (fiches bâtiment
/// et locataire, etc.), même instanciés dynamiquement.
///
/// Idempotent (ne retouche pas une section déjà à la bonne couleur). Ne recolore
/// QUE les noms présents dans la table (les autres objets sont ignorés).
public class AppSectionColors : MonoBehaviour
{
    public float interval = 0.5f;
    float _timer;

    struct Fam { public Color accent, clair; public Fam(string a, string c) { accent = UITheme.Hex(a); clair = UITheme.Hex(c); } }

    static readonly Fam Identity = new Fam("#0F6E56", "#D6ECE3"); // identité / contrat
    static readonly Fam Money    = new Fam("#A9741C", "#F4E7CD"); // argent
    static readonly Fam Notes    = new Fam("#5C6E85", "#E7ECF2"); // notes

    // Nom de section (trim, préfixe « Sec_ » retiré) → famille.
    static readonly Dictionary<string, Fam> Map = new Dictionary<string, Fam>
    {
        { "General", Identity }, { "Bail", Identity },
        { "Informations", Identity }, { "Informations générales", Identity },
        { "Loyer", Money }, { "Dépôt de garantie", Money }, { "Facturation", Money },
        { "Suivi de facturation", Money }, { "Rentabilité", Money },
        { "Rentabilité indexée", Money },
        { "Commentaire", Notes }, { "Objectif", Notes }, { "Objectifs", Notes },
    };

    // Revue 17 (D9-02, lot E) : le passage périodique ne visite que les objets affichés, dans un tampon réutilisé. Mesuré le
    // 16/11 : 105 ms toutes les 0,5 s pour SCI (JBC), 1,1 s pour 300 locataires, en balayant aussi les fiches cachées. Les
    // fiches sont colorées une fois à leur construction (Styler) ; les fenêtres de la scène, au lancement (OnEnable).
    static readonly List<RectTransform> _tampon = new List<RectTransform>();

    void OnEnable() { Parcourir(transform, true); }

    void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer < interval) return;
        _timer = 0f;
        Parcourir(transform, false);
    }

    /// Colore tout ce qui est sous `racine`, objets cachés compris (fiche qui vient d'être construite).
    public static void Styler(Transform racine) { if (racine != null) Parcourir(racine, true); }

    static void Parcourir(Transform racine, bool inactifs)
    {
        racine.GetComponentsInChildren(inactifs, _tampon);
        for (int i = 0; i < _tampon.Count; i++)
        {
            var rt = _tampon[i];
            var rootImg = rt.GetComponent<Image>();
            if (rootImg == null) continue;                 // une carte de section a une Image (liseré)
            var titre = rt.Find("titre");
            if (titre == null) continue;                   // ... et un bandeau « titre »

            string key = rt.name.Trim();
            if (key.StartsWith("Sec_")) key = key.Substring(4);
            if (!Map.TryGetValue(key, out var fam)) continue;

            // Taille uniforme (comme la fiche locataire) : bande 58 + titre 22.
            NormalizeTaille(titre);

            if (ApproxEq(rootImg.color, fam.accent)) continue;   // couleur déjà appliquée

            rootImg.color = fam.accent;
            var band = titre.GetComponent<Image>();
            if (band != null) band.color = fam.clair;
            var icon = titre.Find("Icon");
            if (icon != null) { var ii = icon.GetComponent<Image>(); if (ii != null) ii.color = fam.accent; }
            for (int c = 0; c < titre.childCount; c++)
            {
                var t = titre.GetChild(c).GetComponent<TMP_Text>();
                if (t != null) { t.color = fam.accent; break; }
            }
        }
        _tampon.Clear();
    }

    static bool ApproxEq(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

    // Bande de titre à la même hauteur (58) et titre au rôle Section, comme tous les
    // titres de section. Le ContentSizeFitter du « titre » lit le preferred → un
    // LayoutElement (priorité) le force. Idempotent (ne re-touche que si différent).
    // (Était 22, hors échelle : ramenait toutes les 0,5 s les titres des fiches à 22.)
    const float BandH = 58f;
    const float TitreFont = UITheme.Role.Section;
    static void NormalizeTaille(Transform titre)
    {
        var le = titre.GetComponent<LayoutElement>() ?? titre.gameObject.AddComponent<LayoutElement>();
        if (!Mathf.Approximately(le.preferredHeight, BandH)) { le.minHeight = BandH; le.preferredHeight = BandH; }
        for (int c = 0; c < titre.childCount; c++)
        {
            var t = titre.GetChild(c).GetComponent<TMP_Text>();
            if (t == null) continue;
            if (t.enableAutoSizing || !Mathf.Approximately(t.fontSize, TitreFont))
            { t.enableAutoSizing = false; t.fontSize = TitreFont; }
            break;
        }
    }
}
