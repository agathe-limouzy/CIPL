using System;
using System.Collections.Generic;
using NUnit.Framework;
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

    // Travaux
    public bool travauxEnCours;
    public List<AchatFinancement> historiquesAchat = new List<AchatFinancement>();
    public List<TravauxFinancement> travaux = new List<TravauxFinancement>();
    // Objectifs
    public ObjectiveList objectifs = new ObjectiveList();

    //Locataire 
    public List<Locataire> locataireDuBatiment = new List<Locataire>();

    // Propriété DateTime pratique (non sérialisée)
    [NonSerialized]
    private DateTime _dateAchat;



    // Propriétés calculées
  

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

public enum ParkingState
{
    Aucun,
    ParkingEnCopropiete,
    ParkingPrive
}
