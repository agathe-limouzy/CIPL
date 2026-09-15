using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Alertes d'échéance de facturation pour un locataire.
/// • Loyer : à envoyer 15 j avant l'échéance (le jour de demande des mois facturés).
///   Attention 1 semaine avant la date d'envoi, puis URGENT passé ce délai.
/// • Régularisation des charges : alerte 1 semaine avant la date de régularisation.
/// • Révision du dépôt de garantie : alerte 15 j avant la date limite.
public static class FacturationAlertes
{
    public enum Niveau { Attention, Urgent }
    public enum AlerteType { Loyer, Regul, Depot }

    public struct Alerte
    {
        public Niveau niveau;
        public string message;
        public AlerteType type;
    }

    const int LoyerEnvoiAvant   = 15;   // loyer envoyé 15 j avant l'échéance
    const int LoyerAttentionLead = 7;   // attention 1 semaine avant la date d'envoi
    const int RegulAttentionLead = 7;   // régularisation : 1 semaine avant
    const int DepotAttentionLead = 15;  // dépôt : 15 j avant

    public static List<Alerte> Pour(Locataire loc)
    {
        var res = new List<Alerte>();
        if (loc == null) return res;
        DateTime today = DateTime.Today;

        // ── Loyer ──
        var e = ProchaineEcheanceLoyer(loc, today);
        if (e.HasValue && !AvantReprise(loc, e.Value) && !FacturationSuivi.DejaTraite(loc,
                $"loyer-{e.Value.Year}-P{FacturationSuivi.PeriodeIndex(loc.periodiciteLoyer, e.Value.Month)}"))
        {
            DateTime E = e.Value, envoi = E.AddDays(-LoyerEnvoiAvant);
            if (today >= envoi)
                res.Add(new Alerte { niveau = Niveau.Urgent, type = AlerteType.Loyer,
                    message = $"URGENT — Loyer : échéance le {E:dd/MM}, à envoyer avant le {envoi:dd/MM}." });
            else if (today >= envoi.AddDays(-LoyerAttentionLead))
                res.Add(new Alerte { niveau = Niveau.Attention, type = AlerteType.Loyer,
                    message = $"Loyer à préparer : envoi attendu avant le {envoi:dd/MM} (échéance {E:dd/MM})." });
        }

        // ── Régularisation des charges (si provision pour charges) ──
        // L'échéance se répète chaque année (mois/jour de la date saisie) ; une régul
        // concerne les charges de l'année PRÉCÉDENTE. On regarde la dernière échéance
        // DÉJÀ ARRIVÉE (peut être passée) → en retard si non traitée ; sinon la
        // prochaine → à préparer si elle approche. (Corrige le cas d'une régul en
        // retard qui n'était jamais signalée car on ne regardait que le futur.)
        if (loc.provisionPourCharges && DateTime.TryParse(loc.dateRegularisationChargeISO, out var rd))
        {
            DateTime echue = DerniereOccurrence(rd.Month, rd.Day, today);          // <= aujourd'hui
            DateTime prochaine = SafeDate(echue.Year + 1, rd.Month, rd.Day);       // > aujourd'hui

            // Échéance déjà arrivée : en retard, sauf si reprise (historique) ou déjà traitée.
            bool echueOk = !AvantReprise(loc, echue)
                           && !FacturationSuivi.DejaTraite(loc, $"regul-{echue.Year - 1}");
            if (echueOk)
                res.Add(new Alerte { niveau = Niveau.Urgent, type = AlerteType.Regul,
                    message = $"URGENT — Régularisation des charges à faire (échue le {echue:dd/MM/yyyy})." });
            else if (today >= prochaine.AddDays(-RegulAttentionLead)
                     && !AvantReprise(loc, prochaine)
                     && !FacturationSuivi.DejaTraite(loc, $"regul-{prochaine.Year - 1}"))
                res.Add(new Alerte { niveau = Niveau.Attention, type = AlerteType.Regul,
                    message = $"Régularisation des charges à préparer (le {prochaine:dd/MM/yyyy})." });
        }

        // ── Révision du dépôt de garantie ──
        if (loc.depotDeGarantie > 0f && DateTime.TryParse(loc.dateRevisionDepotISO, out var D)
            && !AvantReprise(loc, D) && !FacturationSuivi.DejaTraite(loc, $"depot-{D.Year}"))
        {
            if (today >= D)
                res.Add(new Alerte { niveau = Niveau.Urgent, type = AlerteType.Depot,
                    message = $"URGENT — Révision du dépôt de garantie (date limite {D:dd/MM/yyyy})." });
            else if (today >= D.AddDays(-DepotAttentionLead))
                res.Add(new Alerte { niveau = Niveau.Attention, type = AlerteType.Depot,
                    message = $"Révision du dépôt à préparer (date limite {D:dd/MM/yyyy})." });
        }

        return res;
    }

    /// Vrai si au moins une alerte URGENTE (pour la pastille d'onglet).
    public static bool AUrgent(Locataire loc) => Pour(loc).Any(a => a.niveau == Niveau.Urgent);

    // Prochaine échéance de loyer : jour de demande, sur les mois facturés
    // (tous les mois si mensuel / non défini).
    static DateTime? ProchaineEcheanceLoyer(Locataire loc, DateTime today)
    {
        if (loc.jourDemandeLoyer <= 0) return null;
        int jour = Mathf.Clamp(loc.jourDemandeLoyer, 1, 31);
        List<int> mois = (loc.periodiciteLoyer == Periodicite.mensuel
                          || loc.moisFacturationLoyer == null || loc.moisFacturationLoyer.Count == 0)
            ? Enumerable.Range(1, 12).ToList()
            : loc.moisFacturationLoyer;

        DateTime best = DateTime.MaxValue;
        for (int y = today.Year; y <= today.Year + 1; y++)
            foreach (int m in mois)
            {
                if (m < 1 || m > 12) continue;
                int day = Mathf.Min(jour, DateTime.DaysInMonth(y, m));
                var dt = new DateTime(y, m, day);
                if (dt >= today && dt < best) best = dt;
            }
        return best == DateTime.MaxValue ? (DateTime?)null : best;
    }

    // Vrai si `d` est ≤ la date de reprise de facturation (période historique reprise).
    static bool AvantReprise(Locataire loc, DateTime d)
        => DateTime.TryParse(loc.repriseFacturationISO, out var r) && d.Date <= r.Date;

    // Dernière occurrence annuelle (mois/jour) <= aujourd'hui (année en cours si déjà
    // passée, sinon l'année précédente).
    static DateTime DerniereOccurrence(int month, int day, DateTime today)
    {
        var occ = SafeDate(today.Year, month, day);
        return occ <= today ? occ : SafeDate(today.Year - 1, month, day);
    }

    // Date sûre (borne le jour au nombre de jours du mois).
    static DateTime SafeDate(int year, int month, int day)
        => new DateTime(year, month, Mathf.Min(day, DateTime.DaysInMonth(year, month)));
}
