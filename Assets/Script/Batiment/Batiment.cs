using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
[Serializable]

public class Batiment : Data
{

    // Informations générales
    public string adressBatiment;
    public float tailleBatiment;
    public float tailleTerrain;
    public ParkingState parkingEtat;
    public string cadastral;            // référence cadastrale du bâtiment
    public string dateAcquisitionISO;   // date d'acquisition saisie manuellement ("yyyy-MM-dd")
    public bool exemple;                // étape 13b-3 : bâtiment d'exemple (rien n'en part ; hors des premiers pas)

    // Photos du bâtiment (chemins absolus dans le dossier de sauvegarde)
    public List<string> photos = new List<string>();
    public string coverPhoto = "";      // photo affichée à la place de la carte GPS (vide = carte)

    // Travaux
    public bool travauxEnCours;
    public List<AchatFinancement> historiquesAchat = new List<AchatFinancement>();
    public List<TravauxFinancement> travaux = new List<TravauxFinancement>();

    // Charges (refacturation / régularisation)
    public List<ChargeBatiment> charges = new List<ChargeBatiment>();

    // Objectifs
    public ObjectiveList objectifs = new ObjectiveList();

    //Locataire 
    public List<Locataire> locataireDuBatiment = new List<Locataire>();

    // Demande du 04/11 : les lots du bâtiment (numéro, surface, désignation). Un locataire y est rattaché par son numéro
    // (Locataire.lotBatiment) ; la surface du bâtiment devient leur somme. Vide : l'ancienne règle (LotsBatiment).
    public List<LotBatiment> lots = new List<LotBatiment>();




    // Propriétés calculées
  

}

/// Un lot du bâtiment (demande du 04/11). Son numéro est le lien avec le locataire qui l'occupe (Locataire.lotBatiment).
[Serializable]
public class LotBatiment
{
    public int numero;
    public float surface;          // m²
    public string designation;     // facultatif : « Local RDC », « Parking 12 »…
}

public enum ParkingState
{
    Aucun,
    ParkingEnCopropiete,
    ParkingPrive
}
