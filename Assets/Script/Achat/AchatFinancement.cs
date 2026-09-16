using System;

[Serializable]
public class AchatFinancement
{
    public string id = Guid.NewGuid().ToString();
    public string label;               // "Achat initial", "Extension 2024"…
    // Montants en `double` et non `float` : au-delà d'environ 1 M€, un `float` ne peut
    // plus représenter le centime — un prix de 2 500 000,45 € était enregistré
    // 2 500 000,50 € dans le JSON, donc faux dès la saisie. `double` garde le centime
    // jusqu'à 100 M€ et reste sérialisé nativement par JsonUtility (contrairement à
    // `decimal`, que Unity ne sérialise pas du tout). Les fichiers existants se
    // relisent sans migration : en JSON, un nombre reste un nombre.
    public double prixAchat;
    public double fraisNotaire;
    public double fraisAgence;
    public bool emprunt;
    public double montantEmprunte;
    public double tauxInteretAnnuel;
    public double apportPersonnel;
    public int dureeMois;
    public double mensualiteCalculee;
    public string dateAchat;           // "YYYY-MM-DD"
}