using System;

/// Ce que l'écran dit du verrou d'une fiche. L'arbitrage (qui obtient la fiche) est fait
/// par la base, dans essai_prendre_verrou : une seule ligne par fiche, reprise possible
/// après 2 min sans signal de vie. Voir CONCEPTION_MULTIUTILISATEUR.md §6.
public static class RegleVerrou
{
    public static readonly TimeSpan DureeSansSignal = TimeSpan.FromMinutes(2);

    /// « En cours de modification par Marie Dupont (facturation) depuis 14 h 32 »
    public static string Libelle(string nom, string motif, DateTime prisLe) =>
        $"En cours de modification par {nom}"
        + (string.IsNullOrEmpty(motif) ? "" : $" ({motif})")
        + $" depuis {prisLe.Hour} h {prisLe.Minute:00}";

    public static bool Expire(DateTime expireLe, DateTime maintenant) => maintenant > expireLe;

    /// Temps avant que la fiche se libère seule : « 1:45 », ou « expiré ».
    public static string Restant(DateTime expireLe, DateTime maintenant)
    {
        var r = expireLe - maintenant;
        return r <= TimeSpan.Zero ? "expiré" : $"{(int)r.TotalMinutes}:{r.Seconds:00}";
    }
}
