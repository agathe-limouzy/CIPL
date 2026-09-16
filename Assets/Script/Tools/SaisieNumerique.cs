using System.Globalization;
using UnityEngine;

/// Conversion d'une saisie utilisateur en nombre.
///
/// Mutualise une méthode `Parse` qui existait en triple (AchatFormPanel,
/// TravauxFormPanel, TravauxController) et qui, en cas de saisie non numérique,
/// renvoyait `0` **en silence** : un taux d'intérêt à 0 produit une mensualité
/// sans intérêts, présentée comme un résultat valide.
public static class SaisieNumerique
{
    /// Convertit une saisie en `float`. Tolère les conventions françaises
    /// (virgule décimale, espaces de milliers y compris insécables, € et %).
    /// Renvoie false si la saisie n'est pas un nombre — l'appelant décide alors
    /// s'il prévient l'utilisatrice.
    ///
    /// **Le point et la virgule sont strictement interchangeables** : selon le
    /// clavier et l'habitude, le séparateur décimal est tapé « . » (pavé numérique)
    /// ou « , » (frappe française), et les deux doivent donner le même nombre.
    /// Quand plusieurs séparateurs sont présents, seul le **dernier** est décimal,
    /// ceux qui le précèdent sont des groupements de milliers : « 1.200,50 »,
    /// « 1,200.50 » et « 1 200,50 » valent tous 1200,50.
    public static bool TryParse(string saisie, out float valeur)
    {
        valeur = 0f;
        if (string.IsNullOrWhiteSpace(saisie)) return true;   // champ vide = 0, cas normal

        // Espace ordinaire, insécable (U+00A0) et insécable étroit (U+202F) : les trois
        // servent de séparateur de milliers selon l'origine du texte (frappe, copier-coller).
        string net = saisie.Replace(" ", "").Replace(" ", "").Replace(" ", "")
                           .Replace("€", "").Replace("%", "")
                           .Trim();

        if (net.Length == 0) return true;

        // Normalisation des séparateurs : le dernier « . » ou « , » devient LE point
        // décimal, ceux qui le précèdent sont retirés (milliers). Sans cela,
        // « 1.200,50 » était rejeté alors que « 1 200,50 » passait.
        int indexDecimal = net.LastIndexOfAny(new[] { '.', ',' });
        if (indexDecimal >= 0)
        {
            string partieEntiere = net.Substring(0, indexDecimal).Replace(".", "").Replace(",", "");
            string partieDecimale = net.Substring(indexDecimal + 1);
            net = partieEntiere + "." + partieDecimale;
        }

        return float.TryParse(net, NumberStyles.Float, CultureInfo.InvariantCulture, out valeur);
    }

    /// Variante « formulaire » : convertit, et signale visuellement une saisie
    /// invalide au lieu de la transformer silencieusement en 0.
    public static float ParseOuAvertir(string saisie, string contexte)
    {
        if (TryParse(saisie, out float v)) return v;

        Debug.LogWarning($"[{contexte}] Valeur non numérique ignorée : « {saisie} » → 0 retenu.");
        UndoToast.Instance?.ShowInfo($"« {saisie} » n'est pas un nombre valide — valeur ignorée (0).");
        return 0f;
    }

    /// Conversion « silencieuse » : renvoie 0 sur saisie invalide, sans avertissement.
    /// Existe pour que les panneaux qui avaient chacun leur `ParseF` locale passent
    /// tous par la MÊME normalisation des séparateurs.
    public static float Parse(string saisie)
    {
        TryParse(saisie, out float v);
        return v;
    }
}
