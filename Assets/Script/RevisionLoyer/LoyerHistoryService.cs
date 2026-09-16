using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// Reconstitue le loyer réel d'un locataire année par année, avec les vraies
/// valeurs d'indice INSEE, à partir de son historique de références d'indexation.
/// Utilisé par la rentabilité pour ne plus appliquer le loyer actuel à plat.
public static class LoyerHistoryService
{
    // ── Cache des observations INSEE (par type d'indice, pour la session) ──────
    private static readonly Dictionary<IndiceImmo, List<(string periode, float valeur)>> _cache = new();

    /// Récupère (avec cache) les observations pour tous les types demandés, puis
    /// renvoie le dictionnaire. Les types déjà en cache ne sont pas re-téléchargés.
    public static IEnumerator FetchIndices(IEnumerable<IndiceImmo> types,
        Action<Dictionary<IndiceImmo, List<(string periode, float valeur)>>> onDone)
    {
        var result = new Dictionary<IndiceImmo, List<(string periode, float valeur)>>();
        foreach (var t in types.Distinct())
        {
            if (_cache.TryGetValue(t, out var cached)) { result[t] = cached; continue; }

            List<(string, float)> obs = null;
            string erreurReseau = null;
            yield return InseeIndiceService.FetchObservations(t, o => obs = o, e => erreurReseau = e);

            if (obs != null && obs.Count > 0) { _cache[t] = obs; result[t] = obs; }
            else
            {
                // Sans indices, le tableau de rentabilité se reconstruit avec le loyer
                // courant à plat : VISUELLEMENT IDENTIQUE à un vrai calcul indexé, mais
                // faux. L'erreur était avalée (`_ => { }`) — on la journalise au moins.
                Debug.LogWarning($"[LoyerHistoryService] Indices {t} indisponibles" +
                                 (string.IsNullOrEmpty(erreurReseau) ? "" : $" ({erreurReseau})") +
                                 " — le tableau de rentabilité utilisera le loyer courant à plat, non indexé.");
            }
        }
        onDone?.Invoke(result);
    }

    /// Observations déjà présentes en cache pour les types donnés (sans réseau).
    /// Permet d'afficher le tableau immédiatement, avant tout téléchargement.
    public static Dictionary<IndiceImmo, List<(string periode, float valeur)>> ObsEnCache(
        IEnumerable<IndiceImmo> types)
    {
        var result = new Dictionary<IndiceImmo, List<(string periode, float valeur)>>();
        foreach (var t in types.Distinct())
            if (_cache.TryGetValue(t, out var c)) result[t] = c;
        return result;
    }

    // ── Segments de référence d'un locataire ──────────────────────────────────

    /// Historique des références, trié par date d'effet. Si aucun historique
    /// n'existe (données antérieures à cette fonctionnalité), synthétise une
    /// référence unique à partir des champs courants du locataire.
    public static List<RevisionReference> Segments(Locataire loc)
    {
        if (loc.historiqueReferences != null && loc.historiqueReferences.Count > 0)
            return loc.historiqueReferences.OrderBy(s => ParseDate(s.dateEffetISO)).ToList();

        float baseVal = ParseIndiceValeur(loc.indiceImmoAuDepart);
        if (baseVal <= 0f) return new List<RevisionReference>();   // pas d'indexation connue

        string periode = ParseIndicePeriode(loc.indiceImmoAuDepart, loc.trimestreDeRevision);
        string dateEffet = PremierBail(loc).ToString("yyyy-MM-dd");
        return new List<RevisionReference>
        {
            new RevisionReference(dateEffet, loc.indiceTypeImmo, periode, baseVal, loc.loyerDepart)
        };
    }

    /// Types d'indice utilisés par une liste de locataires (pour le fetch).
    public static IEnumerable<IndiceImmo> TypesUtilises(IEnumerable<Locataire> locataires)
    {
        var set = new HashSet<IndiceImmo>();
        foreach (var loc in locataires)
            foreach (var s in Segments(loc))
                set.Add(s.indiceType);
        return set;
    }

    /// Date du premier bail (perception des loyers) — conservée à travers les
    /// renouvellements. Retombe sur la date de début de bail courante sinon.
    public static DateTime PremierBail(Locataire loc)
    {
        if (DateTime.TryParse(loc.dateDebutPremierBailISO, out var d1)) return d1;
        if (DateTime.TryParse(loc.dateDebutBailISO, out var d2)) return d2;
        if (loc.historiqueReferences != null && loc.historiqueReferences.Count > 0
            && DateTime.TryParse(loc.historiqueReferences[0].dateEffetISO, out var d3)) return d3;
        return DateTime.Today;
    }

    // ── Loyer réel d'une année civile ─────────────────────────────────────────

    /// Loyer annuel réel du locataire pour l'année civile donnée.
    /// 0 avant le premier bail. Sinon loyerDepart × indice(année) / indiceBase,
    /// segment actif cette année-là. Proratisé pour la 1re année du bail.
    /// Sans donnée d'indice : retombe sur le loyer courant (comportement d'avant).
    public static float LoyerPourAnnee(Locataire loc, int annee,
        Dictionary<IndiceImmo, List<(string periode, float valeur)>> obsParType)
    {
        if (loc == null) return 0f;
        var premierBail = PremierBail(loc);
        if (annee < premierBail.Year) return 0f;   // local vacant avant le premier bail

        var segs = Segments(loc);

        float loyer;
        if (segs.Count == 0)
        {
            loyer = loc.loyerAnnuel;   // aucune indexation connue : loyer courant
        }
        else
        {
            // Segment actif : le dernier dont la date d'effet tombe avant la fin de l'année.
            var finAnnee = new DateTime(annee, 12, 31);
            RevisionReference seg = segs[0];
            foreach (var s in segs)
            {
                if (ParseDate(s.dateEffetISO) <= finAnnee) seg = s;
                else break;
            }
            loyer = LoyerSegmentPourAnnee(loc, seg, annee, obsParType);
        }

        // Proratisation de la première année du bail
        if (annee == premierBail.Year)
            loyer *= (12 - premierBail.Month + 1) / 12f;

        return loyer;
    }

    private static float LoyerSegmentPourAnnee(Locataire loc, RevisionReference seg, int annee,
        Dictionary<IndiceImmo, List<(string periode, float valeur)>> obsParType)
    {
        if (seg.indiceBaseValeur > 0f && obsParType != null
            && obsParType.TryGetValue(seg.indiceType, out var obs) && obs != null)
        {
            string norm = InseeIndiceService.Normalize(seg.trimestreReference);
            string trimestre = norm.Contains("-") ? norm.Split('-')[1] : "T2";
            var o = TrouveAnneeTrimestre(obs, annee, trimestre);
            if (o.valeur > 0f)
                return seg.loyerDepart * (o.valeur / seg.indiceBaseValeur);
        }
        // Pas de donnée d'indice : loyer courant (comportement d'avant, ne casse rien).
        return loc.loyerAnnuel > 0f ? loc.loyerAnnuel : seg.loyerDepart;
    }

    /// Cherche l'indice de l'année/trimestre demandés, avec repli sur les années
    /// précédentes (max 6 ans) — utile pour l'année courante non encore publiée.
    private static (string periode, float valeur) TrouveAnneeTrimestre(
        List<(string periode, float valeur)> obs, int annee, string trimestre)
    {
        for (int r = 0; r <= 6; r++)
        {
            var e = obs.Find(o => InseeIndiceService.Normalize(o.periode) == $"{annee - r}-{trimestre}");
            if (!string.IsNullOrEmpty(e.periode)) return e;
        }
        return ("", 0f);
    }

    // ── Enregistrement d'une référence (appelé depuis RevisionPanel) ───────────

    /// Enregistre une nouvelle référence d'indexation si elle diffère de la
    /// dernière. Mémorise aussi la date du premier bail (une seule fois).
    public static void EnregistrerReference(Locataire loc, DateTime dateEffet,
        IndiceImmo type, string trimestreReferencePeriode, float indiceBaseValeur, float loyerDepart)
    {
        if (loc.historiqueReferences == null)
            loc.historiqueReferences = new List<RevisionReference>();

        if (string.IsNullOrEmpty(loc.dateDebutPremierBailISO))
            loc.dateDebutPremierBailISO = DateTime.TryParse(loc.dateDebutBailISO, out _)
                ? loc.dateDebutBailISO
                : dateEffet.ToString("yyyy-MM-dd");

        string periode = InseeIndiceService.Normalize(trimestreReferencePeriode);

        var last = loc.historiqueReferences.Count > 0
            ? loc.historiqueReferences[loc.historiqueReferences.Count - 1] : null;
        bool identique = last != null
            && last.indiceType == type
            && InseeIndiceService.Normalize(last.trimestreReference) == periode
            && Mathf.Approximately(last.indiceBaseValeur, indiceBaseValeur)
            && Mathf.Approximately(last.loyerDepart, loyerDepart);
        if (identique) return;

        loc.historiqueReferences.Add(new RevisionReference(
            dateEffet.ToString("yyyy-MM-dd"), type, periode, indiceBaseValeur, loyerDepart));
    }

    // ── Parsing utilitaires ───────────────────────────────────────────────────

    // "125.50  (2020-T2)" -> 125.50
    private static float ParseIndiceValeur(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        int par = s.IndexOf('(');
        string tete = (par >= 0 ? s.Substring(0, par) : s).Trim();
        return float.TryParse(tete, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }

    // "125.50  (2020-T2)" -> "2020-T2" ; sinon repli sur trimestreDeRevision
    private static string ParseIndicePeriode(string s, string fallback)
    {
        if (!string.IsNullOrEmpty(s))
        {
            int a = s.IndexOf('(');
            int b = s.IndexOf(')');
            if (a >= 0 && b > a)
                return InseeIndiceService.Normalize(s.Substring(a + 1, b - a - 1));
        }
        return InseeIndiceService.Normalize(fallback);
    }

    private static DateTime ParseDate(string s)
        => DateTime.TryParse(s, out var d) ? d : DateTime.MinValue;
}
