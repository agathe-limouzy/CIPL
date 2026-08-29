using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class Locataire : Data
{


    
    public string siretNumber;
    public string codeCompatable;
    public string emailLocataire;
    public string adresseLocataire;
    public int lotBatiment;
    public float tailleLot;
    public BailType typeDeBail;
    public IndiceImmo indiceTypeImmo;
    public string indiceImmoAuDepart;
    public string indiceImmoActuel;
    public string dateDebutBailISO;
    public string dateFinBailISO;
    public string trimestreDeRevision;
    public float depotDeGarantie;
    public bool provisionPourCharges;
    public float provisionPourChargeValue;
    public float loyerAnnuel;
    public float loyerDepart;
    public float loyerAnnuelPrecedent;
    public float tauxDeRentabilité;
    public string moisDeRevisionISO;
    public string dernierRevision;
    public string commentaire;
    public string cheminBail;   // chemin du fichier bail (PDF, scan…)
    public Periodicite periodiciteLoyer;

    // Objectifs
    public ObjectiveList objectifs = new ObjectiveList();

    // Propriété DateTime pratique (non sérialisée)
    [NonSerialized]
    private DateTime _dateDebutBail;
    public DateTime DateDebutBail
    {
        get => DateTime.TryParse(dateDebutBailISO, out var d) ? d : DateTime.Today;
        set => dateDebutBailISO = value.ToString("yyyy-MM-dd");
    }

    [NonSerialized]
    private DateTime _dateFinBail;
    public DateTime DateFinBail
    {
        get => DateTime.TryParse(dateFinBailISO, out var d) ? d : DateTime.Today;
        set => dateFinBailISO = value.ToString("yyyy-MM-dd");
    }

    // ── Renouvellement de bail ────────────────────────────────────────────────
    // Seuil d'alerte : 6 mois avant la fin du bail.
    public const int SEUIL_FIN_BAIL_JOURS = 182;

    /// Vrai si le bail se termine dans moins de 6 mois (ou est déjà expiré).
    /// `jours` = nombre de jours avant la fin (négatif si le bail est expiré).
    /// Ne se déclenche que pour un locataire nommé avec une date de fin valide,
    /// afin de ne pas alerter sur les fiches vides (DateFinBail vaut Today par défaut).
    public static bool RenouvellementProche(Locataire loc, out int jours)
    {
        jours = 0;
        if (loc == null || string.IsNullOrEmpty(loc.Name)) return false;
        if (!DateTime.TryParse(loc.dateFinBailISO, out var fin)) return false;
        jours = (fin - DateTime.Today).Days;
        return jours <= SEUIL_FIN_BAIL_JOURS;
    }

    [NonSerialized]
    private DateTime _moisDeRevisionISO;
    public DateTime MoisDeRevision
    {
        get => DateTime.TryParse(moisDeRevisionISO, out var d) ? d : DateTime.Today;
        set => moisDeRevisionISO = value.ToString("yyyy-MM-dd");
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}


public enum BailType
{
    BailAContruction,
    Bail9ans,
    Bail10ans
}

public enum IndiceImmo
{
    ILC,
    IRL,
    ILAT
}

public enum Periodicite
{
    mensuel,
    trimestriel,
    BiAnnuel,
    Annuel
}

public enum RevisionMode
{
    NouveauBail,
    BailEnCours
}
