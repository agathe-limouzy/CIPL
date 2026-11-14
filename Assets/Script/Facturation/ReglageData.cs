using System;
using System.Collections.Generic;

/// Un RIB (compte émetteur) affiché sur les factures. Référencé par ID depuis
/// les locataires — modifier un RIB le met à jour partout où il est utilisé.
[Serializable]
public class RibData
{
    public string id = Guid.NewGuid().ToString();
    // Revue 17 (D6-21) : exemples neutres, jamais un vrai compte.
    public string name;          // libellé court (ex "Compte loyers")
    public string titulaire;     // ex "SCI Exemple"
    public string domiciliation; // banque et agence (ex "Banque, agence (guichet)")
    public string rib;           // banque, guichet, compte, clé (ex "12345 67890 00012345678 12")
    public string iban;          // 27 caractères en France (ex "FR76 1234 5678 9012 3456 7890 123", exemple non valide)
    public string bic;           // 8 ou 11 caractères (ex "ABCDFRPPXXX")

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

    // Étape 16a : « google » ou « microsoft » quand l'adresse d'expédition est reliée (l'envoi passe
    // alors par leur API, sans serveur ni mot de passe) ; vide : SMTP.
    public string fournisseur = "";
}

/// Réglages globaux de l'app (facturation). Persistés dans <SaveRoot>/reglage.json.
/// NB : ne contient AUCUN secret (clé API, mot de passe) — voir ReglageService.
[Serializable]
public class ReglageData
{
    public string entrepriseNom = "";   // nom de l'entreprise (multi-entreprise / sélecteur)
    public SmtpConfig smtp = new SmtpConfig();

    // Délais de facturation (01/11) : le calendrier figé du code (15 / 15 / 30) devient un réglage de l'entreprise ;
    // un locataire peut avoir les siens (Locataire.delaisPropres). Lus par FacturationSuivi.EnvoiAvant & co.
    public int envoiAvantJours = 15;     // le loyer part N jours avant son échéance
    public int impayeApresJours = 15;    // « Impayé » N jours après l'échéance sans règlement
    public int echeanceAutresJours = 30; // charges, régularisation, dépôt : échéance = date de facture + N jours

    public List<RibData> ribs = new List<RibData>();
    public List<EnteteData> entetes = new List<EnteteData>();

    // Listes de charges régularisées À PART (ex. « Taxe foncière »). Vide = une seule
    // liste, la générale, comme avant. Voir ListesCharges.
    public List<ListeCharges> listesCharges = new List<ListeCharges>();
    // Nom affiché de la liste générale (toujours présente, renommable, non supprimable).
    public string nomListeGenerale = ListesCharges.NomGeneraleDefaut;

    // Logo affiché sur les factures (chemin d'image copiée dans le dossier de
    // sauvegarde). Vide = logo CIPL par défaut (StreamingAssets/logo_cipl.png),
    // sauf `sansLogo` : aucun logo (entreprise créée par « Créer mon compte », ou
    // « Retirer le logo » ; retour du 25/10).
    public string logoPath;
    public bool sansLogo;

    // Lieu d'émission imprimé devant la date (« St Marcel Paulel, le … »), sur tous les
    // documents. Réglage d'entreprise seulement : aucune facture ne le remplace.
    public string lieuEmission = FacturePdfService.LieuDefaut;

    // Revue 17 (D6-27, coutures) : l'article cité est le L441-10 (l'ancien L441-6, renuméroté en 2019). Texte d'usine
    // seulement : des Réglages déjà enregistrés gardent leur phrase (à retoucher dans Réglages › Textes fixes).
    public string phraseRetard =
        "En cas de retard de paiement, des pénalités de retard, à un taux d'intérêt égal à trois fois " +
        "le taux d'intérêt légal seront appliquées + une indemnité forfaitaire de 40 € pour frais de " +
        "recouvrement (art. L441-10 du Code de commerce).";

    // Demande du 04/11 : la phrase sur l'escompte (obligatoire, art. L441-9 du Code de commerce), au PDF et au XML (note
    // AAB). Vide : la phrase d'usine (FactureEn16931.Escompte), jamais rien.
    public string phraseEscompte = FactureEn16931.SansEscompte;

    // Phrases de BASE des mentions TVA. Chaque facture peut les remplacer pour
    // elle-même (`FactureInfo.texteTvaDebit`) — voir MentionTva.
    public string mentionTvaDebits = FacturePdfService.TvaDebitDefaut;
    public string mentionTvaEncaissements = FacturePdfService.TvaEncaissementsDefaut;
    // Revue 17 (D5-25, coutures ; décision du 09/11) : la mention proposée sur une facture dont le type n'a encore rien de
    // mémorisé pour ce locataire (MentionTva.Debits ou Encaissements ; voir MentionTva.ParDefaut). Des Réglages d'avant ce
    // champ gardent « débits » : rien ne change sans un geste dans Réglages › TVA sur les loyers.
    public int mentionTvaParDefaut = MentionTva.Debits;
    // TVA sur les loyers (02/11) : l'option du bailleur par entreprise, son taux, et la mention d'exonération (Textes fixes). Voir Tva.
    public bool loyersSoumisTva = true;
    public float tauxTva = Tva.TauxDefaut;
    public string mentionTvaExoneration = Tva.ExonerationDefaut;

    // Modèles d'e-mail (décision du 31/10 ; Réglages › Facturation › Modèles d'e-mail), voir EmailService.Modele.
    // La facture avec son PDF (les 4 types) ;
    public string factureObjet = EmailService.ObjetDefaut;
    public string factureCorps = EmailService.CorpsDefaut;
    // étape 18b : jusqu'au 31/8/2027, la même, quand elle part aussi par la plateforme (le PDF en est la copie) ;
    public string copieObjet = EmailService.CopieObjetDefaut;
    public string copieCorps = EmailService.CopieCorpsDefaut;
    // étape 18b : dès le 1/9/2027, un loyer qui part par la plateforme n'est plus envoyé en PDF, cet e-mail
    // prévient le locataire une fois la facture acceptée (FactureElectronique.Notifier) ;
    public string notifObjet = EmailService.NotifObjetDefaut;
    public string notifCorps = EmailService.NotifCorpsDefaut;
    // le brouillon de rappel d'impayé (FactureRappelService) ; l'invitation d'un collègue (UtilisateursCloud).
    public string rappelObjet = EmailService.RappelObjetDefaut;
    public string rappelCorps = EmailService.RappelCorpsDefaut;
    public string invitationObjet = UtilisateursCloud.InvitationObjetDefaut;
    public string invitationCorps = UtilisateursCloud.InvitationCorpsDefaut;

    // NB : les phrases de l'explication du dépôt ne sont PAS ici. Elles appartiennent
    // à la facture de dépôt (`FactureInfo.depot*`), pas à l'entreprise — seuls la
    // phrase de retard, le bas de page et les bases des mentions TVA sont des
    // textes d'entreprise.
    public string basDePage =
        "SAS au capital de 1 500 000 € - Siret : 717 220 883 00044 - TVA intracommunautaire FR 28 717 220 883 - Code APE 6820B\n" +
        "Siège Social : \"La Louve\" 6 route d'Agde - 31 590 Saint Marcel Paulel - Tel : 06.07.04.69.28 - Email : contact@cipl.fr";
}
