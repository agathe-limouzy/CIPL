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
        if (bp == null || bp.listLocataire == null || bp.listLocataire.Count == 0)
            return BatimentEtat.Vacant;   // bâtiment sans locataire = lot vide

        bool retard = false, bientot = false, vacant = false;
        var today = DateTime.Today;

        foreach (var loc in bp.listLocataire)
        {
            if (string.IsNullOrEmpty(loc.Name))
            {
                vacant = true;
                continue;
            }

            bool initialise = !string.IsNullOrEmpty(loc.indiceImmoAuDepart)
                              && loc.indiceImmoAuDepart != "—";
            if (!initialise) continue;

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
