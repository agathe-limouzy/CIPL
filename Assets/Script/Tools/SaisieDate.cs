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

    /// Revue 17 (D8-08, même geste que D6-05) : la culture invariante, avec l'année sur deux chiffres jusqu'en 2099. Celle de
    /// Unity s'arrête à 2029 : « 15/01/30 » était le 15/01/1930, une échéance passée depuis un siècle.
    static readonly DateTimeFormatInfo Lecture = DeuxChiffresAu21eSiecle();
    static DateTimeFormatInfo DeuxChiffresAu21eSiecle()
    {
        var f = (DateTimeFormatInfo)DateTimeFormatInfo.InvariantInfo.Clone();
        f.Calendar.TwoDigitYearMax = 2099;
        return f;
    }

    /// Convertit une saisie en date. Renvoie false si elle n'est pas lisible —
    /// l'appelant décide alors du repli (souvent la date du jour) plutôt que de
    /// se retrouver avec une date silencieusement fausse.
    /// Revue 17 (D6-05) : tirets et points valent des barres obliques (« 01-12-2026 », « 1.12.2026 ») ; « 2026-12-31 » reste
    /// illisible, aucun format ne commençant par l'année.
    public static bool TryParse(string saisie, out DateTime date)
        => DateTime.TryParseExact((saisie ?? "").Trim().Replace('-', '/').Replace('.', '/'), Formats,
                                  Lecture, DateTimeStyles.None, out date);

    /// Revue 17 (D6-05) : le refus à afficher au centre pour une date vide ou illisible, ou null si elle se lit. Avant, les
    /// formulaires la remplaçaient sans rien dire (date du jour, échéance vide, ancienne date d'une charge).
    public static string Refus(string saisie, string quoi)
        => TryParse(saisie, out _) ? null
         : string.IsNullOrWhiteSpace(saisie) ? $"{quoi} : obligatoire."
         : Incomplete(saisie) ? $"{quoi} : date incomplète, remplissez le jour, le mois et l'année."
         : BienFormee(saisie) ? $"{quoi} « {saisie.Trim()} » n'existe pas : vérifiez le jour et le mois."
         : $"{quoi} « {saisie.Trim()} » illisible : tapez-la sous la forme jj/mm/aaaa.";

    /// Revue visuelle (V-17) : une des trois cases de `ChampDate` est vide (« 5//2026 ») : la date est incomplète, pas mal écrite.
    static bool Incomplete(string saisie)
    {
        var p = (saisie ?? "").Split('/');
        return p.Length == 3 && Array.Exists(p, string.IsNullOrWhiteSpace);
    }

    /// Jour, mois et année en chiffres (« 31/02/2026 », « 31.13.2026 ») : la forme est bonne, c'est la date qui n'existe pas.
    static bool BienFormee(string saisie)
        => System.Text.RegularExpressions.Regex.IsMatch((saisie ?? "").Trim().Replace('-', '/').Replace('.', '/'),
                                                        @"^\d{1,2}/\d{1,2}/(\d{2}|\d{4})$");

    /// Les deux dates des quatre formulaires de facture, contrôlées avant l'aperçu et avant l'émission.
    public static string RefusFacture(string date, string echeance)
        => Refus(date, "Date de la facture") ?? Refus(echeance, "Date d'échéance");
}
