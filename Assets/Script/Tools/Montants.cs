using System;

/// Revue 17 (B-02 / D6-13 / D10-04, lot F) : un nombre affiché s'écrit à la française (« 87 419 € », « 73,6 % »), quel que soit le
/// réglage de Windows. Avant, les `$"{Montants.F(x, "N0")} €"` suivaient la culture du PC (« 87,419 € » sur un Windows anglais, lu 87 euros).
/// Un seul point d'écriture : `Montants.F(x, "N0")`, sur la culture des factures (FacturePdfService.FrCulture).
public static class Montants
{
    public static string F(IFormattable valeur, string format) => valeur.ToString(format, FacturePdfService.FrCulture);
}
