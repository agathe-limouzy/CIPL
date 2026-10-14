using System;
using System.Linq;

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

    // ── Étape 5 : qui est là ──────────────────────────────────────────────────

    /// Nom affiché, ou l'e-mail de qui n'en a pas saisi : on garde ce qui précède « @ ».
    static string SansDomaine(string nom)
    {
        string n = (nom ?? "").Trim();
        int a = n.IndexOf('@');
        return a > 0 ? n.Substring(0, a) : n;
    }

    /// « Marie Dupont » → « Marie » ; « agathe.limouzy@cipl.fr » → « agathe ».
    public static string Prenom(string nom)
    {
        var mots = SansDomaine(nom).Split(new[] { ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
        return mots.Length > 0 ? mots[0] : "?";
    }

    /// « Marie Dupont » → « MD » ; « Paul » → « PA » ; « agathe.limouzy@cipl.fr » → « AL ».
    public static string Initiales(string nom)
    {
        string n = SansDomaine(nom);
        var mots = n.Split(new[] { ' ', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (mots.Length == 0) return "?";
        return (mots.Length >= 2 ? $"{mots[0][0]}{mots[1][0]}" : mots[0].Substring(0, Math.Min(2, mots[0].Length))).ToUpperInvariant();
    }

    /// « · Odile » en ambre, après un nom d'onglet ou de ligne (texte enrichi TMP).
    public static string Marque(string prenoms) =>
        string.IsNullOrEmpty(prenoms) ? "" : $" <color=#BA7517><b>· {prenoms}</b></color>";

    /// Onglet d'un bâtiment : qui y modifie quelque chose, le bâtiment ou l'un de ses
    /// locataires (décision du 14/10 : les onglets des locataires sont masqués). null : personne.
    public static string Detenteurs(System.Collections.Generic.IEnumerable<(string type, string id, string nom)> verrous,
                                    string batimentId, System.Collections.Generic.ICollection<string> locataires)
    {
        var p = verrous.Where(v => v.type == "batiment" ? v.id == batimentId : locataires.Contains(v.id))
                       .Select(v => Prenom(v.nom)).Distinct().ToList();
        return p.Count > 0 ? string.Join(", ", p) : null;
    }

    /// Collègues en ligne : un signal de vie de moins de 2 min, hors ma session ; une personne
    /// ouverte sur deux PC ne compte qu'une fois. Triés par nom.
    public static System.Collections.Generic.List<string> EnLigne(
        System.Collections.Generic.IEnumerable<(string session, string nom, DateTime vu)> presences, string maSession, DateTime maintenant) =>
        presences.Where(p => p.session != maSession && maintenant - p.vu < DureeSansSignal)
                 .Select(p => p.nom).Distinct().OrderBy(n => n).ToList();

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
