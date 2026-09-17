using System;
using System.Globalization;

/// Lecture d'une date saisie dans un formulaire (pendant de `SaisieNumerique`).
///
/// Mutualise un `TryDate` qui existait à l'identique dans les quatre panneaux de
/// facture. La culture est INVARIANTE et les formats explicites : `DateTime.TryParse`
/// en culture courante donne des résultats différents selon la machine — c'est ce qui
/// avait rendu des échéances illisibles et empêché des factures de passer impayées.
public static class SaisieDate
{
    /// Formats réellement tapés dans les formulaires (jour/mois/année).
    static readonly string[] Formats = { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" };

    /// Convertit une saisie en date. Renvoie false si elle n'est pas lisible —
    /// l'appelant décide alors du repli (souvent la date du jour) plutôt que de
    /// se retrouver avec une date silencieusement fausse.
    public static bool TryParse(string saisie, out DateTime date)
        => DateTime.TryParseExact((saisie ?? "").Trim(), Formats,
                                  CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
