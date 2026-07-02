using System;

[Serializable]
public class TravauxFinancement
{
    public string id = Guid.NewGuid().ToString();
    public string description;
    public float coutTotal;
    public bool emprunt;
    public float montantEmprunte;
    public float apportPersonnel;
    public float tauxInteretAnnuel;
    public int dureeMois;
    public float mensualiteCalculee;
    public string dateDebutTravaux;
}