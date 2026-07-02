using System;

[Serializable]
public class AchatFinancement
{
    public string id = Guid.NewGuid().ToString();
    public string label;               // "Achat initial", "Extension 2024"…
    public float prixAchat;
    public float fraisNotaire;
    public float fraisAgence;
    public bool emprunt;
    public float montantEmprunte;
    public float tauxInteretAnnuel;
    public float apportPersonnel;
    public int dureeMois;
    public float mensualiteCalculee;
    public string dateAchat;           // "YYYY-MM-DD"
}