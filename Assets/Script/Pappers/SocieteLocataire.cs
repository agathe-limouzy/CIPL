using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

/// Étape 16b (décisions du 28/10) : le bloc société d'un locataire, sur le cloud. La fiche vient de Pappers
/// par la fonction serveur « pappers » (relue une fois par mois, gardée sur le serveur pour tout CIPL) ;
/// les procédures collectives viennent du BODACC (API publique, gratuite), lu à chaque tournée. Ici, sans
/// réseau (testé) : lecture des réponses, la ligne « État », le bloc du commentaire et les objectifs créés
/// tout seuls (« Déclarer la créance », « Vérifier le bail »). Hors cloud, `PappersResume` (annuaire).
public static class SocieteLocataire
{
    [Serializable] public class Dirigeant { public string nom, qualite; }
    [Serializable] public class Exercice { public int annee; public string ca, resultat; }

    /// Le format de CIPL, traduit depuis Pappers par Serveur/fonctions/pappers.ts (montants en texte).
    [Serializable] public class FicheSociete
    {
        public string siren, nom, forme, capital, creee_le, fermee_le;
        public bool fermee;
        public Dirigeant[] dirigeants;
        public Exercice[] finances;
    }

    /// Une annonce de procédure collective au BODACC (dates « yyyy-MM-dd »).
    public class Procedure { public string id, siren, parution, jugeLe, famille, nature; }

    // ── Le BODACC ──────────────────────────────────────────────────────────────

    public const string AdresseBodacc =
        "https://bodacc-datadila.opendatasoft.com/api/explore/v2.1/catalog/datasets/annonces-commerciales/records";

    /// Les procédures collectives de ces sociétés, en un appel, de la plus récente à la plus ancienne.
    /// ponytail: 100 annonces par appel au plus (limite de l'API) ; l'appelant envoie 40 sociétés à la fois.
    public static string UrlBodacc(IEnumerable<string> sirens)
    {
        string where = $"registre in ({string.Join(",", sirens.Select(s => $"\"{s}\""))}) and familleavis=\"collective\"";
        return AdresseBodacc + "?where=" + Uri.EscapeDataString(where) + "&order_by=" + Uri.EscapeDataString("dateparution desc")
             + "&limit=100&select=" + Uri.EscapeDataString("id,registre,dateparution,jugement");
    }

    [Serializable] class Bodacc { public Annonce[] results; }
    [Serializable] class Annonce { public string id, dateparution, jugement; public string[] registre; }
    [Serializable] class Jugement { public string famille, nature, date; }

    /// La réponse du BODACC ; null si elle est illisible (pas « aucune procédure »).
    public static List<Procedure> LireBodacc(string json)
    {
        Bodacc b;
        try { b = JsonUtility.FromJson<Bodacc>(json ?? ""); } catch (Exception) { return null; }
        if (b?.results == null) return null;
        var res = new List<Procedure>();
        foreach (var a in b.results)
        {
            string siren = a?.registre?.Select(Chiffres).FirstOrDefault(s => s.Length == 9);
            if (siren == null) continue;
            Jugement j = null;   // le jugement est un texte JSON dans l'annonce
            try { j = JsonUtility.FromJson<Jugement>(a.jugement ?? ""); } catch (Exception) { }
            res.Add(new Procedure { id = a.id, siren = siren, parution = a.dateparution ?? "", jugeLe = j?.date ?? "",
                                    famille = j?.famille ?? "", nature = j?.nature ?? "" });
        }
        return res;
    }

    // ── L'état de la société (règles simples, décision du 28/10) ───────────────

    /// La procédure en cours : la dernière annonce, sauf une clôture.
    public static Procedure EnCours(IEnumerable<Procedure> procs)
    {
        var d = procs?.OrderByDescending(p => p.parution, StringComparer.Ordinal).FirstOrDefault();
        return d == null || Contient(d, "clôture") ? null : d;
    }

    /// Fermée, puis En difficulté (procédure en cours), puis À surveiller (dernier résultat publié négatif),
    /// puis Saine ; Rien à signaler sans comptes publiés. Et pourquoi, en quelques mots.
    public static (string etat, string pourquoi) Etat(FicheSociete f, IEnumerable<Procedure> procs)
    {
        if (f?.fermee == true) return ("FERMÉE", string.IsNullOrEmpty(f.fermee_le) ? "" : "depuis le " + DateFr(f.fermee_le));
        var p = EnCours(procs);
        if (p != null) return ("EN DIFFICULTÉ", $"{Simple(p)} (jugement du {DateFr(p.jugeLe)})");
        var e = f?.finances?.Where(x => x != null && Nombre(x.resultat) != null).OrderByDescending(x => x.annee).FirstOrDefault();
        if (e == null) return ("RIEN À SIGNALER", "comptes non publiés");
        return Nombre(e.resultat) < 0 ? ("À SURVEILLER", $"résultat négatif en {e.annee} ({Montant(e.resultat)})") : ("SAINE", "");
    }

    /// « redressement judiciaire », « liquidation judiciaire », « procédure de sauvegarde »…, sinon la nature lue.
    public static string Simple(Procedure p)
    {
        foreach (var (mot, nom) in new[] { ("liquidation judiciaire", "liquidation judiciaire"), ("redressement judiciaire", "redressement judiciaire"),
                                           ("sauvegarde accélérée", "sauvegarde accélérée"), ("sauvegarde", "procédure de sauvegarde") })
            if (Contient(p, mot)) return nom;
        return !string.IsNullOrWhiteSpace(p?.nature) ? p.nature : !string.IsNullOrWhiteSpace(p?.famille) ? p.famille : "procédure collective";
    }

    // ── Le bloc du commentaire (maquette validée le 28/10) ──────────────────────

    public static string Construire(FicheSociete f, IList<Procedure> procs, string luLe)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>{f.nom}</b>");
        string forme = Ligne(f.forme, Nombre(f.capital) != null ? "capital " + Montant(f.capital) : null);
        if (forme != "") sb.AppendLine(forme);
        string ident = Ligne(string.IsNullOrEmpty(f.siren) ? null : "SIREN : " + PappersResume.FormatSiren(f.siren),
                             string.IsNullOrEmpty(f.creee_le) ? null : "créée le " + DateFr(f.creee_le));
        if (ident != "") sb.AppendLine(ident);

        var (etat, pourquoi) = Etat(f, procs);
        sb.AppendLine().AppendLine($"État : <b>{etat}</b>");
        if (pourquoi != "") sb.AppendLine("  " + pourquoi);

        sb.AppendLine().AppendLine("── Dirigeants ──");
        var dirigeants = (f.dirigeants ?? new Dirigeant[0]).Where(d => d != null && !string.IsNullOrWhiteSpace(d.nom)).ToList();
        if (dirigeants.Count == 0) sb.AppendLine("Non publiés");
        foreach (var d in dirigeants) sb.AppendLine(Ligne(d.nom, d.qualite));

        sb.AppendLine().AppendLine("── Finances ──");
        var exercices = (f.finances ?? new Exercice[0]).Where(x => x != null && x.annee > 0).OrderByDescending(x => x.annee).ToList();
        if (exercices.Count == 0) sb.AppendLine("Comptes non publiés");
        foreach (var x in exercices)
            sb.AppendLine($"{x.annee} : " + Ligne(Nombre(x.ca) != null ? "CA " + Montant(x.ca) : "CA non publié",
                                                  Nombre(x.resultat) != null ? "résultat " + Montant(x.resultat) : null));

        sb.AppendLine().AppendLine("── Procédures collectives ──");
        if (procs == null || procs.Count == 0) sb.AppendLine("Aucune");
        foreach (var p in (procs ?? new List<Procedure>()).Take(5))
            sb.AppendLine($"{Majuscule(Simple(p))} : jugement du {DateFr(p.jugeLe)} (BODACC du {DateFr(p.parution)})");

        // La date de la fiche, pas celle du jour : le bloc ne change pas (et rien n'est réécrit) chaque matin.
        sb.AppendLine().Append($"Fiche Pappers du {DateFr(luLe)}");
        return sb.ToString();
    }

    // ── Les objectifs créés tout seuls (décision du 28/10) ──────────────────────

    /// « Déclarer la créance au mandataire » pour une procédure ouverte publiée il y a moins de 2 mois : le
    /// délai de déclaration court depuis la publication au BODACC, l'échéance est à 2 mois. « Vérifier le
    /// bail » pour une société fermée, sans échéance. Une seule fois chacun : leur clé va dans
    /// `Locataire.signalementsSociete`, et un objectif supprimé ensuite ne revient pas.
    public static List<(string cle, Objective objectif)> Signaler(ICollection<string> deja, FicheSociete f,
                                                                  IEnumerable<Procedure> procs, DateTime aujourdhui)
    {
        var res = new List<(string, Objective)>();
        foreach (var p in procs ?? Enumerable.Empty<Procedure>())
        {
            string cle = "bodacc:" + p.id;
            var publie = Objectifs.Date(p.parution);
            if (deja.Contains(cle) || publie == null || !Contient(p, "ouverture")) continue;
            var limite = publie.Value.AddMonths(2);
            if (limite < aujourdhui.Date) continue;
            res.Add((cle, new Objective($"Déclarer la créance au mandataire : {Simple(p)} (jugement du {DateFr(p.jugeLe)})",
                                        Objective.Importance.Obligatoire) { echeanceISO = Objectifs.Iso(limite) }));
        }
        if (f?.fermee == true && !deja.Contains("fermee:" + f.siren))
            res.Add(("fermee:" + f.siren, new Objective("Vérifier le bail : la société locataire est fermée"
                + (string.IsNullOrEmpty(f.fermee_le) ? "" : $" (depuis le {DateFr(f.fermee_le)})"), Objective.Importance.Obligatoire)));
        return res;
    }

    // ── Petits outils ──────────────────────────────────────────────────────────

    public static string Siren(string siret)
    {
        string c = Chiffres(siret);
        return c.Length >= 9 ? c.Substring(0, 9) : c;
    }

    static string Chiffres(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());

    static bool Contient(Procedure p, string mot) =>
        ((p?.famille ?? "") + " " + (p?.nature ?? "")).IndexOf(mot, StringComparison.OrdinalIgnoreCase) >= 0;

    static string Ligne(params string[] morceaux) => string.Join(" · ", morceaux.Where(m => !string.IsNullOrWhiteSpace(m)));

    static string Majuscule(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    static decimal? Nombre(string s) =>
        decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : (decimal?)null;

    // Espace ordinaire entre les milliers : la police n'a pas forcément l'espace fine de fr-FR.
    static readonly NumberFormatInfo Milliers = new NumberFormatInfo { NumberGroupSeparator = " ", NumberGroupSizes = new[] { 3 } };

    /// « 1 250 000 € », « -12 000 € ».
    public static string Montant(string s) => Nombre(s) is decimal n ? Math.Round(n).ToString("#,0", Milliers) + " €" : "";

    static string DateFr(string iso) => string.IsNullOrEmpty(iso) ? "?" : PappersResume.FormatDate(iso.Length >= 10 ? iso.Substring(0, 10) : iso);
}
