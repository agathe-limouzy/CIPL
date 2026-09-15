using System;
using System.Collections.Generic;
using UnityEngine;

/// Une ligne de la zone « À traiter » du menu home :
/// révisions de loyer à faire + objectifs actifs, triés par urgence.
public class HomeAlert
{
    public enum Kind { RevisionRetard, RevisionProche, BailRenouvellement, Objectif, Facturation }

    public Kind kind;
    public string typeTodo;      // colonne « Type » : Révision de loyer / Fin de bail / Objectif obligatoire / Rappel / En cours / À faire
    public string nomRappel;     // colonne « Rappel » : texte de l'objectif, ou détail (En retard / Dans X j / Expiré…)
    public string nomLocataire;  // colonne « Locataire » : vide si c'est un objectif de bâtiment
    public string nomBatiment;   // colonne « Bâtiment »
    public int priorite;         // tri : petit = plus urgent
    public Color pastille;       // accent saturé du liseré à gauche
    public Color typeBg;         // fond pastel de la pastille « Type »
    public Color typeTexte;      // couleur du texte de la pastille « Type »

    public BatimentPrefab batiment;
    public Locataire locataire;  // optionnel (null pour un objectif bâtiment)
}

public static class HomeAlertCollector
{
    private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    // Pastille « Type » — paires fond pastel / texte foncé (mêmes teintes que les badges d'objectifs)
    private static readonly Color BgRevision = Hex("#F3D8E4");   // prune/rose (section financière)
    private static readonly Color TxRevision = Hex("#8E3B5A");
    private static readonly Color BgBail = Hex("#DEE3EB");       // bleu ardoise (section Bail)
    private static readonly Color TxBail = Hex("#2C3E5E");
    private static readonly Color BgMoney = Hex("#F4E7CD");      // ambre (facturation = argent)
    private static readonly Color TxMoney = Hex("#8A5A0C");

    public static List<HomeAlert> Collect(IEnumerable<BatimentPrefab> batiments)
    {
        var alertes = new List<HomeAlert>();
        var today = DateTime.Today;

        foreach (var bp in batiments)
        {
            string nomBat = bp.getName();
            if (string.IsNullOrEmpty(nomBat)) nomBat = "Bâtiment";

            // ── Fin de bail / renouvellement ──────────────────────────────────
            foreach (var loc in bp.listLocataire)
            {
                if (!Locataire.RenouvellementProche(loc, out int joursBail)) continue;

                string nomLocBail = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;
                bool expire = joursBail < 0;
                alertes.Add(new HomeAlert
                {
                    kind = HomeAlert.Kind.BailRenouvellement,
                    typeTodo = "Fin de bail",
                    nomRappel = expire
                        ? "Bail expiré — à renouveler"
                        : (joursBail <= 31
                            ? $"Dans {joursBail} j"
                            : $"Dans {Mathf.CeilToInt(joursBail / 30f)} mois"),
                    nomLocataire = nomLocBail,
                    nomBatiment = nomBat,
                    priorite = expire ? 0 : 1,
                    pastille = expire ? UITheme.Alerte : UITheme.Attention,
                    typeBg = BgBail,
                    typeTexte = TxBail,
                    batiment = bp,
                    locataire = loc
                });
            }

            // ── Révisions de loyer ────────────────────────────────────────────
            foreach (var loc in bp.listLocataire)
            {
                bool initialise = !string.IsNullOrEmpty(loc.indiceImmoAuDepart)
                                  && loc.indiceImmoAuDepart != "—";
                if (!initialise) continue;

                int jours = (loc.MoisDeRevision - today).Days;
                string nomLoc = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;

                if (jours < 0)
                    alertes.Add(new HomeAlert
                    {
                        kind = HomeAlert.Kind.RevisionRetard,
                        typeTodo = "Révision",
                        nomRappel = "En retard",
                        nomLocataire = nomLoc,
                        nomBatiment = nomBat,
                        priorite = 0,
                        pastille = UITheme.Alerte,
                        typeBg = BgRevision,
                        typeTexte = TxRevision,
                        batiment = bp,
                        locataire = loc
                    });
                else if (jours <= BatimentEtatHelper.SEUIL_BIENTOT)
                    alertes.Add(new HomeAlert
                    {
                        kind = HomeAlert.Kind.RevisionProche,
                        typeTodo = "Révision",
                        nomRappel = $"Dans {jours} j",
                        nomLocataire = nomLoc,
                        nomBatiment = nomBat,
                        priorite = 2,
                        pastille = UITheme.Attention,
                        typeBg = BgRevision,
                        typeTexte = TxRevision,
                        batiment = bp,
                        locataire = loc
                    });
            }

            // ── Facturation : envoi loyer / régul charges / révision dépôt ────
            foreach (var loc in bp.listLocataire)
            {
                string nomLocF = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;
                foreach (var fa in FacturationAlertes.Pour(loc))
                {
                    bool urgent = fa.niveau == FacturationAlertes.Niveau.Urgent;
                    string typeF = fa.type switch
                    {
                        FacturationAlertes.AlerteType.Loyer => "Facturer",
                        FacturationAlertes.AlerteType.Regul => "Régul.",
                        FacturationAlertes.AlerteType.Depot => "Rév. dépôt",
                        _ => "Facturation"
                    };
                    alertes.Add(new HomeAlert
                    {
                        kind = HomeAlert.Kind.Facturation,
                        typeTodo = typeF,
                        nomRappel = urgent ? "À faire" : "À préparer",
                        nomLocataire = nomLocF,
                        nomBatiment = nomBat,
                        priorite = urgent ? 0 : 2,
                        pastille = urgent ? UITheme.Alerte : UITheme.Attention,
                        typeBg = BgMoney,
                        typeTexte = TxMoney,
                        batiment = bp,
                        locataire = loc
                    });
                }
            }

            // ── Objectifs (bâtiment + locataires) ─────────────────────────────
            var data = bp.getBatiment();
            if (data?.objectifs?.items != null)
                foreach (var obj in data.objectifs.items)
                    AjouteObjectif(alertes, obj, nomBat, "", bp, null);

            foreach (var loc in bp.listLocataire)
            {
                if (loc.objectifs?.items == null) continue;
                string nomLoc = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;
                foreach (var obj in loc.objectifs.items)
                    AjouteObjectif(alertes, obj, nomBat, nomLoc, bp, loc);
            }
        }

        alertes.Sort((a, b) => a.priorite.CompareTo(b.priorite));
        return alertes;
    }

    private static void AjouteObjectif(List<HomeAlert> alertes, Objective obj,
        string nomBat, string nomLoc, BatimentPrefab bp, Locataire loc)
    {
        if (obj.status == Objective.ObjectiveStatus.Fait) return;

        int priorite = obj.status switch
        {
            Objective.ObjectiveStatus.Obligatoire => 1,
            Objective.ObjectiveStatus.Rappel => 3,
            Objective.ObjectiveStatus.EnCours => 4,
            _ => 5
        };

        alertes.Add(new HomeAlert
        {
            kind = HomeAlert.Kind.Objectif,
            // Mêmes libellé et couleurs que les badges des listes d'améliorations
            typeTodo = ObjectiveItem.GetStatusLabel(obj.status),
            nomRappel = obj.text,
            nomLocataire = nomLoc,   // vide pour un objectif de bâtiment
            nomBatiment = nomBat,
            priorite = priorite,
            pastille = ObjectiveItem.GetStatusAccent(obj.status),
            typeBg = ObjectiveItem.GetStatusColor(obj.status),
            typeTexte = ObjectiveItem.GetStatusTextColor(obj.status),
            batiment = bp,
            locataire = loc
        });
    }
}
