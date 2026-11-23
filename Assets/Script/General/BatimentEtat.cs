using System;
using UnityEngine;

public enum BatimentEtat { AJour, Bientot, Retard, Vacant }

/// Logique partagée d'état d'un bâtiment (pastille de couleur) et de ses alertes.
public static class BatimentEtatHelper
{
    public const int SEUIL_BIENTOT = 90;   // jours avant révision → "proche"

    /// État le plus urgent parmi les locataires du bâtiment.
    /// Priorité : Retard > Bientôt > Vacant > À jour.
    public static BatimentEtat GetEtat(BatimentPrefab bp)
    {
        // Revue 17 (D7a-10, lot F) : l'état se lit sur les locataires en place (un locataire parti ou archivé ne met plus le
        // bâtiment « en retard », et un bâtiment que tous ont quitté est vacant).
        var enPlace = bp == null ? null : System.Linq.Enumerable.ToList(BatimentPrefab.EnPlace(bp.listLocataire));
        if (enPlace == null || enPlace.Count == 0)
            return BatimentEtat.Vacant;   // bâtiment sans locataire = lot vide

        bool retard = false, bientot = false, vacant = false;
        var today = DateTime.Today;

        foreach (var loc in enPlace)
        {
            if (string.IsNullOrEmpty(loc.Name))
            {
                vacant = true;
                continue;
            }

            if (!loc.RevisionAAnnoncer) continue;   // paliers / sans révision, ou parti avant la date (D8-06) : rien à réviser

            int jours = (loc.MoisDeRevision - today).Days;
            if (jours < 0) retard = true;
            else if (jours <= SEUIL_BIENTOT) bientot = true;
        }

        if (retard) return BatimentEtat.Retard;
        if (bientot) return BatimentEtat.Bientot;
        if (vacant) return BatimentEtat.Vacant;
        return BatimentEtat.AJour;
    }

    public static Color Couleur(BatimentEtat etat) => etat switch
    {
        BatimentEtat.Retard => UITheme.Alerte,      // terracotta
        BatimentEtat.Bientot => UITheme.Attention,   // ambre
        BatimentEtat.Vacant => new Color(0.706f, 0.698f, 0.663f), // gris
        _ => UITheme.Primaire                        // vert
    };

    // Ordre de tri : plus urgent en premier
    public static int Priorite(BatimentEtat etat) => etat switch
    {
        BatimentEtat.Retard => 0,
        BatimentEtat.Bientot => 1,
        BatimentEtat.Vacant => 2,
        _ => 3
    };
}
