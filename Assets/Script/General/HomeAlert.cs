using System;
using System.Collections.Generic;
using UnityEngine;

/// Une ligne de la zone « À traiter » du menu home :
/// révisions de loyer à faire + objectifs actifs, triés par urgence.
public class HomeAlert
{
    public enum Kind { RevisionRetard, RevisionProche, Objectif }

    public Kind kind;
    public string titre;         // "Révision en retard", "Objectif obligatoire", "À faire"…
    public string sujet;         // nom du locataire / texte de l'objectif
    public string batimentNom;   // source affichée à droite
    public int priorite;         // tri : petit = plus urgent
    public Color pastille;       // couleur du point

    public BatimentPrefab batiment;
    public Locataire locataire;  // optionnel (null pour un objectif bâtiment)
}

public static class HomeAlertCollector
{
    private static readonly Color Bleu = new Color(0.216f, 0.541f, 0.867f);
    private static readonly Color Gris = new Color(0.706f, 0.698f, 0.663f);

    public static List<HomeAlert> Collect(IEnumerable<BatimentPrefab> batiments)
    {
        var alertes = new List<HomeAlert>();
        var today = DateTime.Today;

        foreach (var bp in batiments)
        {
            string nomBat = bp.getName();
            if (string.IsNullOrEmpty(nomBat)) nomBat = "Bâtiment";

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
                        titre = "Révision en retard",
                        sujet = nomLoc,
                        batimentNom = nomBat,
                        priorite = 0,
                        pastille = UITheme.Alerte,
                        batiment = bp,
                        locataire = loc
                    });
                else if (jours <= BatimentEtatHelper.SEUIL_BIENTOT)
                    alertes.Add(new HomeAlert
                    {
                        kind = HomeAlert.Kind.RevisionProche,
                        titre = $"Révision dans {jours} j",
                        sujet = nomLoc,
                        batimentNom = nomBat,
                        priorite = 2,
                        pastille = UITheme.Attention,
                        batiment = bp,
                        locataire = loc
                    });
            }

            // ── Objectifs (bâtiment + locataires) ─────────────────────────────
            var data = bp.getBatiment();
            if (data?.objectifs?.items != null)
                foreach (var obj in data.objectifs.items)
                    AjouteObjectif(alertes, obj, nomBat, bp, null);

            foreach (var loc in bp.listLocataire)
            {
                if (loc.objectifs?.items == null) continue;
                string nomLoc = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;
                foreach (var obj in loc.objectifs.items)
                    AjouteObjectif(alertes, obj, $"{nomBat} › {nomLoc}", bp, loc);
            }
        }

        alertes.Sort((a, b) => a.priorite.CompareTo(b.priorite));
        return alertes;
    }

    private static void AjouteObjectif(List<HomeAlert> alertes, Objective obj,
        string source, BatimentPrefab bp, Locataire loc)
    {
        if (obj.status == Objective.ObjectiveStatus.Fait) return;

        string titre; int priorite; Color pastille;
        switch (obj.status)
        {
            case Objective.ObjectiveStatus.Obligatoire:
                titre = "Objectif obligatoire"; priorite = 1; pastille = UITheme.Alerte; break;
            case Objective.ObjectiveStatus.Rappel:
                titre = "Rappel"; priorite = 3; pastille = UITheme.Attention; break;
            case Objective.ObjectiveStatus.EnCours:
                titre = "En cours"; priorite = 4; pastille = Bleu; break;
            default:
                titre = "À faire"; priorite = 5; pastille = Gris; break;
        }

        alertes.Add(new HomeAlert
        {
            kind = HomeAlert.Kind.Objectif,
            titre = titre,
            sujet = obj.text,
            batimentNom = source,
            priorite = priorite,
            pastille = pastille,
            batiment = bp,
            locataire = loc
        });
    }
}
