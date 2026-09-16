using System;

[Serializable]
public class TravauxFinancement
{
    public string id = Guid.NewGuid().ToString();
    public string description;
    // Montants en `double` — voir AchatFinancement : un `float` perd le centime
    // au-delà d'environ 1 M€, et l'écrit déjà faux dans le JSON.
    public double coutTotal;
    public bool emprunt;
    public double montantEmprunte;
    public double apportPersonnel;
    public double tauxInteretAnnuel;
    public int dureeMois;
    public double mensualiteCalculee;
    public string dateDebutTravaux;
}