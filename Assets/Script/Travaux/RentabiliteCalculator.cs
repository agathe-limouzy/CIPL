using System;

public static class RentabiliteCalculator
{
    public static float Mensualite(float montant, float tauxAnnuel, int dureeMois)
    {
        if (montant <= 0 || dureeMois <= 0) return 0f;
        if (tauxAnnuel <= 0) return montant / dureeMois;

        double t = tauxAnnuel / 100.0 / 12.0;
        double m = montant * t / (1.0 - Math.Pow(1.0 + t, -dureeMois));
        return (float)m;
    }

    public static float CoutInterets(float montant, float tauxAnnuel, int dureeMois)
    {
        return Mensualite(montant, tauxAnnuel, dureeMois) * dureeMois - montant;
    }

    public static float CashFlowAnnuel(float loyerAnnuel, float mensualite)
        => loyerAnnuel - mensualite * 12f;

    public static float BreakEvenAns(float investissement, float cashFlowAnnuel)
        => cashFlowAnnuel > 0 ? investissement / cashFlowAnnuel : float.MaxValue;
}