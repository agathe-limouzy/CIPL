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
    public enum AlerteType { Loyer, Regul, Depot, Depart, Restitution, Archiver }

    public struct Alerte
    {
        public Niveau niveau;
        public string message;
        public AlerteType type;
    }

    // Le calendrier du loyer vit dans FacturationSuivi : le redéclarer ici a déjà
    // laissé les deux écrans annoncer des dates différentes.
    const int LoyerEnvoiAvant    = FacturationSuivi.EnvoiAvantJours;     // envoi 15 j avant l'échéance
    const int LoyerAttentionLead = FacturationSuivi.RappelAvantEnvoiJours; // + 1 semaine pour préparer
    const int RegulAttentionLead = 7;   // régularisation : 1 semaine avant
    const int DepotAttentionLead = 15;  // dépôt : 15 j avant

    public static List<Alerte> Pour(Locataire loc) => Pour(loc, DateTime.Today);

    public static List<Alerte> Pour(Locataire loc, DateTime today)
    {
        var res = new List<Alerte>();
        // Fiche archivée : tout est soldé, plus rien à rappeler.
        if (loc == null || loc.archive) return res;
        // Locataire incomplet (parcours de création) : rien n'est facturable, donc
        // aucune échéance à rappeler — le bandeau du parcours dit quoi faire.
        if (ParcoursLocataire.Prochaine(loc) != ParcoursLocataire.Etape.Termine) return res;
        today = today.Date;
        bool avecSortie = DepartLocataire.Sortie(loc, out var sortie);

        // ── Loyer ──
        var e = ProchaineEcheanceLoyer(loc, today);
        int pe = e.HasValue ? FacturationSuivi.PeriodeIndex(loc, e.Value.Month) : 0;
        // Période avant le bail, après le départ, ou en franchise sans provisions à
        // appeler : aucune facture attendue, aucune alerte.
        if (e.HasValue && !AvantReprise(loc, e.Value) && Loyers.PeriodeFacturable(loc, e.Value.Year, pe)
            && !FacturationSuivi.DejaTraite(loc, $"loyer-{e.Value.Year}-P{pe}"))
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
        // Une alerte par liste de charges : chacune a sa date (voir ListesCharges).
        if (loc.provisionPourCharges)
            foreach (var listeId in ListesCharges.DuLocataire(loc))
            {
                if (!DateTime.TryParse(ListesCharges.DateRegul(loc, listeId), out var rd)) continue;
                DateTime echue = DerniereOccurrence(rd.Month, rd.Day, today);          // <= aujourd'hui
                DateTime prochaine = SafeDate(echue.Year + 1, rd.Month, rd.Day);       // > aujourd'hui
                string quoi = string.IsNullOrEmpty(listeId) ? "" : $" ({ListesCharges.Nom(listeId)})";
                // Régul de l'année du départ : la dernière, au prorata de présence.
                string DeSortie(DateTime ech) => avecSortie && ech.Year - 1 == sortie.Year ? " de sortie" : "";

                // Échéance déjà arrivée : en retard, sauf si reprise (historique) ou déjà traitée.
                // Une régul porte sur les charges de l'année PRÉCÉDENTE : rien si le
                // locataire n'était pas dans les lieux cette année-là (bail plus récent).
                bool echueOk = !AvantReprise(loc, echue)
                               && Loyers.AnneeDansLeBail(loc, echue.Year - 1)
                               && !FacturationSuivi.DejaTraite(loc, ListesCharges.Cle(listeId, echue.Year - 1));
                if (echueOk)
                    res.Add(new Alerte { niveau = Niveau.Urgent, type = AlerteType.Regul,
                        message = $"URGENT — Régularisation{DeSortie(echue)} des charges{quoi} à faire (échue le {echue:dd/MM/yyyy})." });
                else if (today >= prochaine.AddDays(-RegulAttentionLead)
                         && !AvantReprise(loc, prochaine)
                         && Loyers.AnneeDansLeBail(loc, prochaine.Year - 1)
                         && !FacturationSuivi.DejaTraite(loc, ListesCharges.Cle(listeId, prochaine.Year - 1)))
                    res.Add(new Alerte { niveau = Niveau.Attention, type = AlerteType.Regul,
                        message = $"Régularisation{DeSortie(prochaine)} des charges{quoi} à préparer (le {prochaine:dd/MM/yyyy})." });
            }

        // ── Révision du dépôt de garantie ── (jamais après le départ : il sera rendu)
        if (loc.depotDeGarantie > 0f && DateTime.TryParse(loc.dateRevisionDepotISO, out var D)
            && !(avecSortie && D.Date > sortie)
            && !AvantReprise(loc, D) && !FacturationSuivi.DejaTraite(loc, $"depot-{D.Year}"))
        {
            if (today >= D)
                res.Add(new Alerte { niveau = Niveau.Urgent, type = AlerteType.Depot,
                    message = $"URGENT — Révision du dépôt de garantie (date limite {D:dd/MM/yyyy})." });
            else if (today >= D.AddDays(-DepotAttentionLead))
                res.Add(new Alerte { niveau = Niveau.Attention, type = AlerteType.Depot,
                    message = $"Révision du dépôt à préparer (date limite {D:dd/MM/yyyy})." });
        }

        // ── Départ du locataire (décisions du 01/10) ──
        if (DepartLocataire.DepartProche(loc, today))
            res.Add(new Alerte { niveau = Niveau.Attention, type = AlerteType.Depart,
                message = $"Départ le {sortie:dd/MM/yyyy} : état des lieux et décompte de sortie à prévoir." });

        // Restitution du dépôt : Attention 15 j avant la date limite, URGENT ensuite.
        if (avecSortie && today > sortie && !DepartLocataire.DecompteFait(loc)
            && DepartLocataire.EcheanceRestitution(loc, out var R))
        {
            if (today > R)
                res.Add(new Alerte { niveau = Niveau.Urgent, type = AlerteType.Restitution,
                    message = $"URGENT — Restitution du dépôt de garantie (délai dépassé le {R:dd/MM/yyyy})." });
            else if (today >= R.AddDays(-DepartLocataire.RestitutionAttentionJours))
                res.Add(new Alerte { niveau = Niveau.Attention, type = AlerteType.Restitution,
                    message = $"Décompte de sortie à faire : dépôt à restituer avant le {R:dd/MM/yyyy}." });
        }

        // Loyer émis en entier avant que le départ soit saisi : à corriger.
        if (avecSortie)
            foreach (var l in FacturationSuivi.Lignes(loc, sortie.Year))
                if (DepartLocataire.LoyerACorriger(loc, l))
                    res.Add(new Alerte { niveau = Niveau.Attention, type = AlerteType.Loyer,
                        message = $"{l.libelle} : montant à corriger (départ le {sortie:dd/MM/yyyy})." });

        if (DepartLocataire.PretAArchiver(loc))
            res.Add(new Alerte { niveau = Niveau.Attention, type = AlerteType.Archiver,
                message = "Tout est soldé : le locataire peut être archivé." });

        return res;
    }

    /// Vrai si au moins une alerte URGENTE (pour la pastille d'onglet).
    public static bool AUrgent(Locataire loc) => Pour(loc).Any(a => a.niveau == Niveau.Urgent);

    /// Prochaine échéance de loyer, calculée par `FacturationSuivi.EcheanceLoyer` —
    /// la MÊME règle que le tableau de suivi, et non plus une seconde implémentation.
    ///
    /// Les deux divergeaient sur deux points, tous deux corrigés :
    /// • le jour non renseigné faisait abandonner l'alerte (`return null`), alors que
    ///   le suivi affichait quand même les lignes — des loyers à échéance sans aucun
    ///   rappel pour les accompagner ;
    /// • les mois de facturation cochés étaient suivis ici et ignorés là-bas, ce qui
    ///   donnait deux dates différentes pour un même loyer trimestriel.
    static DateTime? ProchaineEcheanceLoyer(Locataire loc, DateTime today)
    {
        int n = FacturationSuivi.NbPeriodes(loc.periodiciteLoyer);
        DateTime best = DateTime.MaxValue;
        for (int y = today.Year; y <= today.Year + 1; y++)
            for (int p = 1; p <= n; p++)
            {
                var dt = FacturationSuivi.EcheanceLoyer(loc, y, p);
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
