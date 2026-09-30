using System;
using System.Collections.Generic;

/// Mode d'envoi global des factures.
public enum ModeEnvoi
{
    Pennylane,   // e-facture Factur-X via l'API Pennylane
    Email        // envoi direct du PDF par email au locataire (SMTP)
}

/// Un RIB (compte émetteur) affiché sur les factures. Référencé par ID depuis
/// les locataires — modifier un RIB le met à jour partout où il est utilisé.
[Serializable]
public class RibData
{
    public string id = Guid.NewGuid().ToString();
    public string name;          // libellé court (ex "BNP Mazamet")
    public string titulaire;     // ex "Groupe CIPL"
    public string domiciliation; // ex "BNP PARIBAS Mazamet (00747)"
    public string rib;           // ex "30004 00747 00021009833 38"
    public string iban;          // ex "FR76 3000 4007 4700 0210 0983 338"
    public string bic;           // ex "BNPAFRPPALB"

    public RibData() { }
}

/// Un « entête » = modèle de texte (paragraphe du bail / objet) avec des
/// variables ({nom}, {adresse}, {lot}...) remplacées au moment de la facture.
/// Référencé par ID depuis les locataires.
[Serializable]
public class EnteteData
{
    public string id = Guid.NewGuid().ToString();
    public string nom;    // nom du modèle (ex "Bail trimestriel Volteo")
    public string texte;  // texte avec variables

    public EnteteData() { }
}

/// Configuration SMTP pour le mode d'envoi Email (le mot de passe n'est PAS
/// stocké ici — il vit dans le fichier secrets, hors JSON/backup/repo).
[Serializable]
public class SmtpConfig
{
    // Valeurs de départ, pas une contrainte : n'importe quelle messagerie convient
    // (Gmail, Outlook/M365, OVH, Free, Orange…) dès lors qu'elle accepte STARTTLS,
    // ce que toutes proposent sur le port 587.
    public string host = "smtp.office365.com";
    public int port = 587;
    public bool useStartTls = true;

    // Identifiant de connexion au serveur. Vide = on utilise l'adresse d'expédition.
    // Ils diffèrent chez beaucoup de fournisseurs (compte OVH, alias Gmail…) : sans
    // ce champ, impossible d'envoyer depuis une adresse autre que son login.
    public string username = "";

    public string fromEmail = "";              // ex "contact@cipl.fr"
    public string fromName = "GROUPE CIPL";
}

/// Réglages globaux de l'app (facturation). Persistés dans <SaveRoot>/reglage.json.
/// NB : ne contient AUCUN secret (clé API, mot de passe) — voir ReglageService.
[Serializable]
public class ReglageData
{
    public string entrepriseNom = "";   // nom de l'entreprise (multi-entreprise / sélecteur)
    public ModeEnvoi modeEnvoi = ModeEnvoi.Pennylane;
    public SmtpConfig smtp = new SmtpConfig();

    public List<RibData> ribs = new List<RibData>();
    public List<EnteteData> entetes = new List<EnteteData>();

    // Listes de charges régularisées À PART (ex. « Taxe foncière »). Vide = une seule
    // liste, la générale, comme avant. Voir ListesCharges.
    public List<ListeCharges> listesCharges = new List<ListeCharges>();
    // Nom affiché de la liste générale (toujours présente, renommable, non supprimable).
    public string nomListeGenerale = ListesCharges.NomGeneraleDefaut;

    // Logo affiché sur les factures (chemin d'image copiée dans le dossier de
    // sauvegarde). Vide = logo CIPL par défaut (StreamingAssets/logo_cipl.png).
    public string logoPath;

    // Lieu d'émission imprimé devant la date (« St Marcel Paulel, le … »), sur tous les
    // documents. Réglage d'entreprise seulement : aucune facture ne le remplace.
    public string lieuEmission = FacturePdfService.LieuDefaut;

    public string phraseRetard =
        "En cas de retard de paiement, des pénalités de retard, à un taux d'intérêt égal à trois fois " +
        "le taux d'intérêt légal seront appliquées + une indemnité forfaitaire de 40 € pour frais de " +
        "recouvrement (Art L 441-6 du C.com).";

    // Phrases de BASE des mentions TVA. Chaque facture peut les remplacer pour
    // elle-même (`FactureInfo.texteTvaDebit`) — voir MentionTva.
    public string mentionTvaDebits = FacturePdfService.TvaDebitDefaut;
    public string mentionTvaEncaissements = FacturePdfService.TvaEncaissementsDefaut;

    // NB : les phrases de l'explication du dépôt ne sont PAS ici. Elles appartiennent
    // à la facture de dépôt (`FactureInfo.depot*`), pas à l'entreprise — seuls la
    // phrase de retard, le bas de page et les bases des mentions TVA sont des
    // textes d'entreprise.
    public string basDePage =
        "SAS au capital de 1 500 000 € - Siret : 717 220 883 00044 - TVA intracommunautaire FR 28 717 220 883 - Code APE 6820B\n" +
        "Siège Social : \"La Louve\" 6 route d'Agde - 31 590 Saint Marcel Paulel - Tel : 06.07.04.69.28 - Email : contact@cipl.fr";
}
