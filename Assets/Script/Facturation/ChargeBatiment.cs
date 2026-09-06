using System;
using System.Collections.Generic;

/// Une charge saisie au niveau du BÂTIMENT (onglet « Charges », à côté d'Achat /
/// Travaux). Sert de base à la refacturation et à la régularisation.
/// Statut : impayé à la création → passe payé une fois refacturée / régularisée.
[Serializable]
public class ChargeBatiment
{
    public string id = Guid.NewGuid().ToString();
    public string nom;                 // ex. « Taxe foncière 2026 », « Entretien toiture »
    public float cout;                 // coût total de la charge (€)
    public string dateISO;             // date de la charge ("yyyy-MM-dd")
    public string pdfPath;             // PDF de la facture de charge (copié dans le dossier de sauvegarde)

    // Répartition entre locataires.
    public bool tousLocataires = true;                     // « Tous » ou sélection
    public List<string> locatairesConcernes = new List<string>(); // ids si pas « Tous »
    public string typeRatio = "Surface";                   // Surface | Égalité | Manuel
    public List<ChargeRatio> ratios = new List<ChargeRatio>(); // poids par locataire (part relative)

    public bool paye = false;          // impayé par défaut
}

/// Poids de répartition d'une charge pour un locataire donné.
/// La part réelle = part / somme(parts) × coût.
[Serializable]
public class ChargeRatio
{
    public string locataireId;
    public float part;

    public ChargeRatio() { }
    public ChargeRatio(string locataireId, float part) { this.locataireId = locataireId; this.part = part; }
}
