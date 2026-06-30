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

    // Achat
    public string dateAchatISO; // JsonUtility ne gère pas DateTime
    public float coutAchat;

    // Emprunt
    public bool emprunt;
    public float empruntCout;
    public float empruntDureeAnnees;
    public float empruntTauxPourcent;

    // Travaux
    public bool travauxEnCours;
    public float coutTravaux;

    // Objectifs
    public ObjectiveList objectifs = new ObjectiveList();

    //Locataire 
    public List<Locataire> locataireDuBatiment = new List<Locataire>();

    // Propriété DateTime pratique (non sérialisée)
    [NonSerialized]
    private DateTime _dateAchat;
    public DateTime DateAchat
    {
        get => DateTime.TryParse(dateAchatISO, out var d) ? d : DateTime.Today;
        set => dateAchatISO = value.ToString("yyyy-MM-dd");
    }


    // Propriétés calculées
    public float MensualiteEmprunt
    {
        get
        {
            if (!emprunt || empruntCout <= 0) return 0f;
            float tauxMensuel = (empruntTauxPourcent / 100f) / 12f;
            int nbMois = (int)(empruntDureeAnnees * 12);
            if (tauxMensuel == 0) return empruntCout / nbMois;
            return empruntCout * tauxMensuel /
                   (1f - Mathf.Pow(1f + tauxMensuel, -nbMois));
        }
    }

    public float CoutTotal => coutAchat + coutTravaux;

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
