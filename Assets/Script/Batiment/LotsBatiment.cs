using System.Collections.Generic;
using System.Linq;

/// Demande du 04/11 : les lots déclarés dans le bâtiment (numéro, surface, désignation). Un locataire est rattaché à son lot
/// par son numéro (Locataire.lotBatiment) et en prend la surface ; la surface du bâtiment est leur somme ; un lot sans
/// occupant est vide (sa part des charges « Surface » reste au propriétaire, ListesCharges.PartsEffectives). Sans lots
/// déclarés (bâtiments saisis avant), l'ancienne règle reste : la surface saisie sur le locataire, ou sa part du disponible.
/// Les occupants sont les locataires enregistrés en place : ni archivés, ni partis (BatimentPrefab.EnPlace).
public static class LotsBatiment
{
    public static bool Definis(Batiment bat) => bat?.lots != null && bat.lots.Count > 0;

    /// Le lot déclaré au numéro du locataire (null : aucun).
    public static LotBatiment Lot(Batiment bat, Locataire loc)
        => loc == null || loc.lotBatiment <= 0 || !Definis(bat) ? null : bat.lots.Find(x => x != null && x.numero == loc.lotBatiment);

    /// La surface du locataire : celle de son lot (0 sans lot) ; sans lots déclarés, l'ancienne règle.
    public static float Surface(Batiment bat, Locataire loc)
        => Definis(bat) ? (Lot(bat, loc)?.surface ?? 0f)
                        : BatimentPrefab.TailleLotDisponible(bat?.tailleBatiment ?? 0f, bat?.locataireDuBatiment, loc);

    /// La somme des lots ; sans lots déclarés, la surface saisie du bâtiment.
    public static float SurfaceTotale(Batiment bat)
        => Definis(bat) ? bat.lots.Where(x => x != null).Sum(x => x.surface) : (bat?.tailleBatiment ?? 0f);

    public static List<Locataire> Occupants(Batiment bat, int numero)
        => numero <= 0 ? new List<Locataire>()
           : BatimentPrefab.EnPlace(bat?.locataireDuBatiment).Where(l => l.lotBatiment == numero).ToList();

    /// Les lots sans autre occupant que `pour` (le locataire qu'on rattache), dans l'ordre des numéros.
    public static List<LotBatiment> Libres(Batiment bat, Locataire pour = null)
        => !Definis(bat) ? new List<LotBatiment>()
           : bat.lots.Where(x => x != null && Occupants(bat, x.numero).All(o => pour != null && o.id == pour.id))
                     .OrderBy(x => x.numero).ToList();

    /// Les locataires en place qu'aucun lot déclaré ne porte : à rattacher depuis leur fiche.
    public static List<Locataire> SansLot(Batiment bat)
        => !Definis(bat) ? new List<Locataire>()
           : BatimentPrefab.EnPlace(bat.locataireDuBatiment).Where(l => Lot(bat, l) == null).ToList();

    /// Un locataire occupe un lot : sans lot libre, on ne le crée pas (null : un lot est libre).
    public static string RefusSansLotLibre(Batiment bat)
    {
        if (Libres(bat).Count > 0) return null;
        string nom = string.IsNullOrEmpty(bat?.Name) ? "ce bâtiment" : $"« {bat.Name} »";
        string suite = " Créez d'abord le lot du nouveau locataire (son numéro et sa surface), puis ajoutez-le.";
        return Definis(bat) ? $"Tous les lots de {nom} sont occupés." + suite
                            : $"Il n'y a pas encore de lot dans {nom} : un locataire occupe un lot du bâtiment." + suite;
    }

    /// Ce qui empêche d'enregistrer ces lots (null : rien). Un lot occupé ne peut pas disparaître : son locataire y est
    /// rattaché par son numéro.
    public static string Refus(IList<LotBatiment> lots, Batiment bat)
    {
        if (lots == null) return null;
        foreach (var x in lots)
        {
            if (x == null || x.numero <= 0) return "Chaque lot a un numéro : un nombre entier, à partir de 1.";
            if (x.surface <= 0f) return $"Saisissez la surface du lot {x.numero}, en m².";
        }
        var doublon = lots.GroupBy(x => x.numero).FirstOrDefault(g => g.Count() > 1);
        if (doublon != null) return $"Deux lots portent le n° {doublon.Key} : chaque lot a son propre numéro.";
        foreach (var l in BatimentPrefab.EnPlace(bat?.locataireDuBatiment))
            if (Lot(bat, l) != null && !lots.Any(x => x.numero == l.lotBatiment))
                return $"Le lot {l.lotBatiment} est occupé par « {Nom(l)} » : il ne peut pas disparaître. Changez d'abord son lot, sur sa fiche.";
        return null;
    }

    /// « Lot 3 · 50 m² · Bureau 2e · vide » : la liste de la fiche du locataire.
    public static string Libelle(Batiment bat, LotBatiment lot)
    {
        var occ = Occupants(bat, lot.numero);
        return $"Lot {lot.numero} · {lot.surface:0.##} m²"
               + (string.IsNullOrWhiteSpace(lot.designation) ? "" : " · " + lot.designation.Trim())
               + " · " + (occ.Count == 0 ? "vide" : string.Join(", ", occ.Select(NomOccupant)));
    }

    public static string Nom(Locataire l) => string.IsNullOrEmpty(l?.Name) ? $"Lot {l?.lotBatiment}" : l.Name;

    /// Retour BM6 (04/11) : un occupant dont le bail n'a pas commencé est dit « à venir ».
    public static string NomOccupant(Locataire l) => Nom(l) + (l != null && l.EstAVenir ? " (à venir)" : "");
}
