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

    /// Fiche prise sans un clic ni une touche pendant ce délai : « Toujours là ? ». Modifiable
    /// pendant le Play pour tester (script-execute), sinon 15 min.
    public static TimeSpan Inactivite = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DelaiReponse = TimeSpan.FromMinutes(2);
    public const string MotifForce = "libération forcée";   // posé par forcer_liberation (etape4b)

    public enum Presence { Actif, Avertir, Liberer }

    /// 14 min 59 s → Actif ; 15 min → Avertir (reste 2:00) ; 17 min → Liberer.
    public static Presence EtatInactivite(TimeSpan inactif) =>
        inactif < Inactivite ? Presence.Actif
        : inactif < Inactivite + DelaiReponse ? Presence.Avertir
        : Presence.Liberer;

    /// Temps avant que la fiche se libère seule : « 1:45 », ou « expiré ».
    public static string Restant(DateTime expireLe, DateTime maintenant)
    {
        var r = expireLe - maintenant;
        return r <= TimeSpan.Zero ? "expiré" : $"{(int)r.TotalMinutes}:{r.Seconds:00}";
    }
}
