using System;

public static class RentabiliteCalculator
{
    // Tout en `double` : le calcul se faisait déjà en double en interne, mais le
    // retour était tronqué en `float`, ce qui reperdait la précision juste avant
    // qu'elle ne serve. Sur un emprunt de plusieurs millions, la mensualité et le
    // coût des intérêts étaient donc arrondis inutilement.
    public static double Mensualite(double montant, double tauxAnnuel, int dureeMois)
    {
        if (montant <= 0 || dureeMois <= 0) return 0d;
        if (tauxAnnuel <= 0) return montant / dureeMois;

        double t = tauxAnnuel / 100.0 / 12.0;
        return montant * t / (1.0 - Math.Pow(1.0 + t, -dureeMois));
    }

    public static double CoutInterets(double montant, double tauxAnnuel, int dureeMois)
    {
        return Mensualite(montant, tauxAnnuel, dureeMois) * dureeMois - montant;
    }

    public static double CashFlowAnnuel(double loyerAnnuel, double mensualite)
        => loyerAnnuel - mensualite * 12d;

    public static double BreakEvenAns(double investissement, double cashFlowAnnuel)
        => cashFlowAnnuel > 0 ? investissement / cashFlowAnnuel : double.MaxValue;
}