using System;
using System.Collections.Generic;

/// Une charge saisie au niveau du BÂTIMENT (onglet « Charges », à côté d'Achat /
/// Travaux). Sert de base à la refacturation et à la régularisation.
///
/// Trois états, et non deux — c'est la distinction qui manquait :
///   1. **à refacturer** : ni facturée ni payée, elle est proposée au choix ;
///   2. **en attente de paiement** : portée sur une facture émise, donc retirée du
///      choix pour ne pas être refacturée deux fois — mais PAS payée pour autant ;
///   3. **payée** : le virement est arrivé, et c'est l'utilisatrice qui le constate.
///
/// L'émission faisait auparavant passer la charge directement en « payé », ce qui
/// faisait dire au bâtiment qu'il avait encaissé une somme jamais reçue.
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

    public bool paye = false;          // le virement est arrivé (constaté à la main)

    // Date à laquelle la charge a été portée sur une facture émise (régularisation
    // ou refacturation), au format "yyyy-MM-dd". Vide = jamais facturée.
    // Une charge antérieure à ce champ est donc « non facturée », ce qui est le bon
    // repli : elle reste proposée au choix si elle n'est pas payée.
    public string factureeISO;

    /// Déjà portée sur une facture : elle ne doit plus être proposée au choix,
    /// qu'elle soit payée ou non.
    public bool EstFacturee => !string.IsNullOrEmpty(factureeISO);

    /// Facturée mais pas encore encaissée — l'état intermédiaire.
    public bool EnAttentePaiement => EstFacturee && !paye;

    /// Ce qu'on affiche à l'utilisatrice, en un seul endroit pour que les écrans ne
    /// puissent pas diverger.
    public string Etat => paye ? "Payé" : EstFacturee ? "En attente de paiement" : "Impayé";
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
