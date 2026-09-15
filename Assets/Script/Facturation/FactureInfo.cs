using System;

/// État mémorisé du menu « Information Facture » pour un type de facture donné,
/// par locataire (le doc parle de factureState{ Loyer, Refacturation, … }).
/// Les montants ne sont pas stockés ici : ils sont recalculés depuis le locataire.
[Serializable]
public class FactureInfo
{
    // Destinataire (mémorisé sur la facture — peut différer du locataire).
    public string destNom;
    public string destAdresse;      // multi-lignes (retours à la ligne conservés)
    public string destSiret;

    public string ribId;            // RIB CIPL choisi (référence Réglage)
    public string enteteId;         // modèle d'entête / paragraphe (référence Réglage)
    public string dateISO;          // date de la facture ("yyyy-MM-dd")
    public string dateEcheanceISO;  // date d'échéance (défaut de la phrase de règlement)
    public string sommePhrase;      // phrase de bas de facture, éditable (ex. « Valeur en votre aimable règlement »)
    public string numeroFormat = "AMN"; // AN=Année/Numéro · AMN=Année/MoisNuméro · AJMN=Année/JourMoisNuméro
    public string numeroId;         // partie « ID locataire » saisie (le préfixe format est recalculé)
    public string numero;           // n° complet = préfixe format + numeroId (mémorisé pour référence)
    public bool tvaDebit = true;    // mention « TVA payée sur les débits »
    public bool ajouterRetard = true; // ajoute la phrase de retard/pénalités
    public bool ajouterMensuel = true; // ligne « montant mensuel à régler » (loyer période ÷ nb mois ; hors mensuel)
    public bool envoiEmail;         // true = email direct ; false = Pennylane (selon réglage global)
    public string emailDest;        // adresse d'envoi (pré-remplie depuis le locataire)
    public string objet;            // objet/titre (ex « Loyer avril 2026 »), éditable
    public string refInterne;       // texte libre affiché avec le n° (ex « N° Interne Magasin 001048 »)
    public string chargeId;         // refacturation : id de la charge refacturée
    public bool joindrePj;          // refacturation : joindre le justificatif de la charge

    // Montants (pré-remplis depuis le locataire, éditables et mémorisés).
    public float loyerMontant;      // loyer HT de la période
    public float provisionMontant;  // provision pour charges de la période

    // Loyer : période facturée.
    public int moisPeriode;         // 1-12 (0 = non défini)
    public int anneePeriode;        // ex 2026

    public bool saved;              // true dès le premier enregistrement (→ on reprend les montants saisis)
}
