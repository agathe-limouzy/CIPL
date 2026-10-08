using System;
using System.Collections.Generic;
using System.Globalization;

[Serializable]
public class Objective
{
    public string id;
    public string text;
    public DateTime createdAt;

    // Version 0 (avant le 06/10) : un seul `status` mêlait importance et avancement.
    // Repris par Objectifs.Migrer au chargement ; plus lu ni écrit ensuite.
    public ObjectiveStatus status;
    public int version;

    public Importance importance;
    public Avancement avancement;
    public string echeanceISO;        // "yyyy-MM-dd", vide = sans échéance
    public int prevenirJours;         // l'échéance « approche » ce nombre de jours avant
    public int repeterTous;           // 0 = pas de récurrence
    public UniteRecurrence repeterUnite;
    public string faitLeISO;
    public string suivanteId;         // occurrence créée en passant « Fait » (retirée si on rouvre)
    public List<string> documents = new List<string>();   // noms de fichier, dossier « Objectifs »

    public enum ObjectiveStatus { AFaire, EnCours, Fait, Obligatoire, Rappel }
    public enum Importance { Normale, Rappel, Obligatoire }
    public enum Avancement { AFaire, EnCours, Fait }
    public enum UniteRecurrence { Jours, Semaines, Mois, Ans }

    public Objective(string text, Importance importance = Importance.Normale)
    {
        id = Guid.NewGuid().ToString();
        this.text = text;
        this.importance = importance;
        createdAt = DateTime.Now;
        version = 1;
        prevenirJours = Objectifs.PREVENIR_DEFAUT;
    }

    public bool Fait => avancement == Avancement.Fait;
}

/// Règles des listes d'objectifs (testées dans ObjectifsTests).
public static class Objectifs
{
    public const int PREVENIR_DEFAUT = 15;

    public enum EtatEcheance { Aucune, Lointaine, Proche, EnRetard }

    /// Origine d'un objectif dans la vue de tous les objectifs — en texte : la police n'a pas d'emoji.
    public static string Origine(string nomBat, string nomLoc) =>
        string.IsNullOrEmpty(nomLoc) ? $"Bât. {nomBat}" : $"Bât. {nomBat} · Loc. {nomLoc}";

    /// Retire l'objectif ; renvoie l'action qui le remet à sa place (« Annuler » du toast).
    public static Action Supprimer(List<Objective> liste, Objective obj)
    {
        int i = liste.IndexOf(obj);
        if (i < 0) return () => { };
        liste.RemoveAt(i);
        return () => { if (!liste.Contains(obj)) liste.Insert(Math.Min(i, liste.Count), obj); };
    }

    // ── Reprise de l'ancien format ─────────────────────────────────────────────

    /// Ancien statut → importance + avancement (décision du 06/10 : Obligatoire et Rappel
    /// gardent leur importance et sont « À faire »). Sans échéance. Renvoie true si modifié.
    public static bool Migrer(Objective o)
    {
        if (o == null) return false;
        bool change = false;
        if (o.documents == null) { o.documents = new List<string>(); change = true; }
        if (o.version >= 1) return change;
        (o.importance, o.avancement) = o.status switch
        {
            Objective.ObjectiveStatus.Obligatoire => (Objective.Importance.Obligatoire, Objective.Avancement.AFaire),
            Objective.ObjectiveStatus.Rappel => (Objective.Importance.Rappel, Objective.Avancement.AFaire),
            Objective.ObjectiveStatus.EnCours => (Objective.Importance.Normale, Objective.Avancement.EnCours),
            Objective.ObjectiveStatus.Fait => (Objective.Importance.Normale, Objective.Avancement.Fait),
            _ => (Objective.Importance.Normale, Objective.Avancement.AFaire)
        };
        o.prevenirJours = PREVENIR_DEFAUT;
        o.version = 1;
        return true;
    }

    public static bool Migrer(ObjectiveList liste)
    {
        bool change = false;
        if (liste?.items != null)
            foreach (var o in liste.items) change |= Migrer(o);
        return change;
    }

    // ── Échéance ───────────────────────────────────────────────────────────────

    public static DateTime? Date(string iso) =>
        DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : (DateTime?)null;

    public static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTime? Echeance(Objective o) => Date(o?.echeanceISO);

    /// Un objectif fait n'a plus d'échéance à surveiller.
    public static EtatEcheance Etat(Objective o, DateTime aujourdhui)
    {
        var e = Echeance(o);
        if (o.Fait || e == null) return EtatEcheance.Aucune;
        int jours = (e.Value.Date - aujourdhui.Date).Days;
        if (jours < 0) return EtatEcheance.EnRetard;
        return jours <= Math.Max(0, o.prevenirJours) ? EtatEcheance.Proche : EtatEcheance.Lointaine;
    }

    /// « En retard de 5 j » · « Aujourd'hui » · « Dans 4 j » · « Échéance 15/10/2027 » · « Fait le … ».
    public static string TexteEcheance(Objective o, DateTime aujourdhui)
    {
        if (o.Fait)
        {
            var f = Date(o.faitLeISO);
            return f != null ? $"Fait le {f:dd/MM/yyyy}" : "Fait";
        }
        var e = Echeance(o);
        if (e == null) return "Sans échéance";
        int jours = (e.Value.Date - aujourdhui.Date).Days;
        return Etat(o, aujourdhui) switch
        {
            EtatEcheance.EnRetard => $"En retard de {-jours} j",
            EtatEcheance.Proche => jours == 0 ? "Aujourd'hui" : $"Dans {jours} j",
            _ => $"Échéance {e:dd/MM/yyyy}"
        };
    }

    /// Ligne d'information sous l'objectif : échéance · récurrence · documents.
    public static string Detail(Objective o, DateTime aujourdhui)
    {
        var parts = new List<string> { TexteEcheance(o, aujourdhui) };
        var e = Echeance(o);
        var etat = Etat(o, aujourdhui);
        if (etat == EtatEcheance.EnRetard || etat == EtatEcheance.Proche) parts.Add($"échéance {e:dd/MM/yyyy}");
        if (o.repeterTous > 0) parts.Add(TexteRecurrence(o.repeterTous, o.repeterUnite));
        int n = o.documents?.Count ?? 0;
        if (n > 0) parts.Add(n == 1 ? "1 document" : $"{n} documents");
        return string.Join(" · ", parts);
    }

    public static string TexteRecurrence(int n, Objective.UniteRecurrence u)
    {
        string unite = u switch
        {
            Objective.UniteRecurrence.Jours => n > 1 ? "jours" : "jour",
            Objective.UniteRecurrence.Semaines => n > 1 ? "semaines" : "semaine",
            Objective.UniteRecurrence.Mois => "mois",
            _ => n > 1 ? "ans" : "an"
        };
        return n == 1 ? $"chaque {unite}" : $"tous les {n} {unite}";
    }

    // ── Récurrence ─────────────────────────────────────────────────────────────

    public static DateTime Suivante(DateTime echeance, int n, Objective.UniteRecurrence u) => u switch
    {
        Objective.UniteRecurrence.Jours => echeance.AddDays(n),
        Objective.UniteRecurrence.Semaines => echeance.AddDays(7 * n),
        Objective.UniteRecurrence.Mois => echeance.AddMonths(n),
        _ => echeance.AddYears(n)
    };

    /// Passe l'objectif « Fait ». Récurrent avec échéance : crée l'occurrence suivante,
    /// datée de l'échéance PRÉVUE + intervalle (calendrier fixe, même fait en avance ou en
    /// retard — décision du 06/10), insérée juste après, sans document. Renvoie la suivante.
    public static Objective MarquerFait(List<Objective> liste, Objective o, DateTime aujourdhui)
    {
        if (o.Fait) return null;
        o.avancement = Objective.Avancement.Fait;
        o.faitLeISO = Iso(aujourdhui);

        var e = Echeance(o);
        if (o.repeterTous <= 0 || e == null) return null;

        var s = new Objective(o.text, o.importance)
        {
            echeanceISO = Iso(Suivante(e.Value, o.repeterTous, o.repeterUnite)),
            prevenirJours = o.prevenirJours,
            repeterTous = o.repeterTous,
            repeterUnite = o.repeterUnite
        };
        o.suivanteId = s.id;
        int i = liste.IndexOf(o);
        liste.Insert(i < 0 ? liste.Count : i + 1, s);
        return s;
    }

    /// Rouvre un objectif fait (« À faire »). L'occurrence suivante qu'il avait créée
    /// disparaît si elle n'a pas encore servi (toujours « À faire », sans document).
    public static void Rouvrir(List<Objective> liste, Objective o)
    {
        if (!o.Fait) return;
        o.avancement = Objective.Avancement.AFaire;
        o.faitLeISO = null;
        var s = string.IsNullOrEmpty(o.suivanteId) ? null : liste.Find(x => x.id == o.suivanteId);
        if (s != null && s.avancement == Objective.Avancement.AFaire && (s.documents == null || s.documents.Count == 0))
            liste.Remove(s);
        o.suivanteId = null;
    }

    /// Occurrences précédentes d'un objectif récurrent, la plus récente d'abord : on remonte
    /// la chaîne des `suivanteId` (chaque occurrence faite pointe vers celle qu'elle a créée).
    public static List<Objective> Precedentes(List<Objective> liste, Objective o)
    {
        var res = new List<Objective>();
        var vu = new HashSet<string> { o.id };
        for (var cur = o; ;)
        {
            var p = liste.Find(x => x.suivanteId == cur.id);
            if (p == null || !vu.Add(p.id)) return res;   // début de chaîne (ou boucle : on s'arrête)
            res.Add(p);
            cur = p;
        }
    }

    /// « Fait le 20/10/2026 · échéance 15/10/2026 · 2 documents » : ligne d'une occurrence passée.
    public static string TexteOccurrence(Objective o)
    {
        var parts = new List<string>();
        var f = Date(o.faitLeISO);
        parts.Add(o.Fait ? (f != null ? $"Fait le {f:dd/MM/yyyy}" : "Fait") : LibelleAvancementCourt(o.avancement));
        var e = Echeance(o);
        if (e != null) parts.Add($"échéance {e:dd/MM/yyyy}");
        int n = o.documents?.Count ?? 0;
        if (n > 0) parts.Add(n == 1 ? "1 document" : $"{n} documents");
        return string.Join(" · ", parts);
    }

    static string LibelleAvancementCourt(Objective.Avancement a) =>
        a == Objective.Avancement.EnCours ? "En cours" : "À faire";

    /// Clic sur le badge : À faire → En cours → Fait → (rouvert) À faire.
    /// Renvoie l'occurrence suivante créée en passant « Fait », sinon null.
    public static Objective Avancer(List<Objective> liste, Objective o, DateTime aujourdhui)
    {
        switch (o.avancement)
        {
            case Objective.Avancement.AFaire: o.avancement = Objective.Avancement.EnCours; return null;
            case Objective.Avancement.EnCours: return MarquerFait(liste, o, aujourdhui);
            default: Rouvrir(liste, o); return null;
        }
    }

    // ── Filtres et tableau en colonnes (06/10) ───────────────────────────────

    /// Famille « avancement » des filtres. « En retard » est un sous-ensemble : un objectif
    /// en retard reste aussi sous « À faire » ou « En cours » (décision du 06/10).
    public enum FiltreAvancement { Tous, AFaire, EnCours, EnRetard, Faits }

    /// Filtre combiné : avancement ET importance (null = toutes).
    public static bool Garde(Objective o, FiltreAvancement av, Objective.Importance? im, DateTime aujourdhui)
    {
        if (im.HasValue && o.importance != im.Value) return false;
        return av switch
        {
            FiltreAvancement.Faits => o.Fait,
            FiltreAvancement.AFaire => o.avancement == Objective.Avancement.AFaire,
            FiltreAvancement.EnCours => o.avancement == Objective.Avancement.EnCours,
            FiltreAvancement.EnRetard => Etat(o, aujourdhui) == EtatEcheance.EnRetard,
            _ => !o.Fait
        };
    }

    /// Une colonne du tableau : ses objectifs (importance filtrée), retards et échéances
    /// d'abord ; la colonne « Fait » du plus récent au plus ancien.
    public static List<Objective> Colonne(IEnumerable<Objective> items, Objective.Avancement colonne,
        Objective.Importance? im, DateTime aujourdhui)
    {
        var l = new List<Objective>();
        foreach (var o in items)
            if (o.avancement == colonne && (!im.HasValue || o.importance == im.Value)) l.Add(o);
        if (colonne == Objective.Avancement.Fait)
            l.Sort((a, b) => (Date(b.faitLeISO) ?? DateTime.MinValue).CompareTo(Date(a.faitLeISO) ?? DateTime.MinValue));
        else
            l.Sort((a, b) => Comparer(a, b, aujourdhui));
        return l;
    }

    /// Carte déposée dans une autre colonne (glisser ou bouton « › »). « Fait » passe par
    /// MarquerFait (occurrence suivante d'un récurrent), quitter « Fait » par Rouvrir.
    /// Renvoie l'occurrence suivante créée, sinon null.
    public static Objective Deplacer(List<Objective> liste, Objective o, Objective.Avancement cible, DateTime aujourdhui)
    {
        if (o.avancement == cible) return null;
        if (cible == Objective.Avancement.Fait) return MarquerFait(liste, o, aujourdhui);
        Rouvrir(liste, o);
        o.avancement = cible;
        return null;
    }

    // ── Tri ────────────────────────────────────────────────────────────────────

    /// Rang d'urgence : en retard, échéance proche, puis Obligatoire, Rappel, Normale.
    public static int Rang(Objective o, DateTime aujourdhui) => Etat(o, aujourdhui) switch
    {
        EtatEcheance.EnRetard => 0,
        EtatEcheance.Proche => 1,
        _ => o.importance switch
        {
            Objective.Importance.Obligatoire => 2,
            Objective.Importance.Rappel => 3,
            _ => 4
        }
    };

    /// Ordre des listes : rang, puis échéance la plus proche (sans date en dernier).
    public static int Comparer(Objective a, Objective b, DateTime aujourdhui)
    {
        int c = Rang(a, aujourdhui).CompareTo(Rang(b, aujourdhui));
        if (c != 0) return c;
        var ea = Echeance(a) ?? DateTime.MaxValue;
        var eb = Echeance(b) ?? DateTime.MaxValue;
        return ea.CompareTo(eb);
    }

    // ── Saisie ─────────────────────────────────────────────────────────────────

    /// Contrôle de la fenêtre : message d'erreur, ou null si tout va bien.
    public static string Verifier(string texte, string echeanceISO, int prevenirJours, int repeterTous)
    {
        if (string.IsNullOrWhiteSpace(texte)) return "Écrivez l'objectif.";
        if (prevenirJours < 0) return "Le délai « me prévenir » ne peut pas être négatif.";
        if (repeterTous < 0) return "L'intervalle de répétition ne peut pas être négatif.";
        if (repeterTous > 0 && Date(echeanceISO) == null)
            return "Une répétition part de l'échéance : saisissez d'abord une date d'échéance.";
        return null;
    }
}
