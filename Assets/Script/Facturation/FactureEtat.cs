using System;
using System.Collections.Generic;

/// État mémorisé d'une facture dans le suivi (une ligne « touchée » : générée,
/// envoyée, payée ou forcée à la main). Les lignes non touchées (À venir / À faire)
/// sont calculées à la volée par FacturationSuivi et n'ont pas d'enregistrement.
[Serializable]
public class FactureEtat
{
    public string key;          // identifiant stable, ex. "loyer-2026-P3", "regul-2025", "depot-2026", "refac-<chargeId>"
    public string type;         // "Loyer" | "Regul" | "Refac" | "Depot"
    public string libelle;      // ex. « Loyer 3e trimestre 2026 »
    public string echeanceISO;  // date d'échéance ("yyyy-MM-dd")
    public string statut;       // "" (auto) | "AFaire" | "Envoye" | "Impaye" | "Paye" (forçage manuel)
    public string numero;       // n° de la facture générée
    public string pdfPath;      // chemin du PDF généré
    public string dateEnvoiISO; // date de génération / envoi
    public float montant;       // montant TTC
    public float tauxTva = Tva.TauxDefaut;   // taux appliqué à l'émission (0 = exonéré) ; absent des anciennes lignes = 20 (02/11)
    public float loyerHT;       // loyer HT facturé (Loyer seulement ; 0 = inconnu) — voir DepartLocataire.LoyerACorriger
    // Revue 17 (D6-07) : les provisions imprimées sur CETTE facture (générale et par liste), pour sa correction ; faux sur les
    // lignes émises avant (la correction retombe alors sur celles de la dernière facture du type).
    public bool provisionsConnues;
    // Revue 17 (D4-04) : la périodicité du loyer à son émission (« mensuel », « trimestriel »…) ; vide sur les lignes d'avant.
    public string periodicite;
    public float provision;
    public List<MontantListe> provisionsListes = new List<MontantListe>();
    public string ribId;        // RIB CIPL (banque) sur lequel la facture est réglée
    public string ribNom;       // libellé du RIB (pour affichage / filtre)
    public int corrections;     // nb de fois où la facture a été refaite/corrigée (0 = originale)
    public string dernierRappelISO; // date du dernier rappel d'échéance envoyé (impayé)
    // Paiements reçus (paiement partiel, 08/10). Portés par la première ligne d'une facture
    // (voir FacturationSuivi.Porteuse). Absent des anciens fichiers = rien de reçu.
    public List<Versement> versements = new List<Versement>();

    // Étape 18b : la facture électronique (plateforme agréée). `plateformeDonnees` = la facture au format
    // EN 16931 (JSON), préparée à l'émission ; vide = pas concernée. `plateformeId` = son identifiant chez
    // Super PDP une fois transmise. `plateformeEtat` : "" (à transmettre), "envoyee", "a_renvoyer" (plateforme
    // injoignable : réessai au prochain lancement), "refusee" (rejetée : `plateformeErreur` dit pourquoi).
    // `encaisseeDeclaree` : le statut « Encaissée » (fr:212) est parti.
    public string plateformeDonnees;
    public string plateformeId;
    public string plateformeEtat;
    public string plateformeErreur;
    public bool encaisseeDeclaree;
    // Transfert entre entreprises (01/11, CONCEPTION §16) : transmise à Super PDP par l'entreprise d'origine, nommée
    // ici ; rien n'y est renvoyé depuis la nouvelle entreprise. BG7 (01/11) : « Encaissée » (et son annulation) se
    // déclare AU NOM de l'origine, avec son jeton, par un membre de celle-ci : `plateformeOrigineId` dit laquelle.
    // Un nom sans identifiant (transfert d'avant) : lecture seule.
    public string plateformeOrigine;
    public string plateformeOrigineId;
    // Revue 16c (D5-07, D5-12) : ce qui a été déclaré (montant TTC, date d'encaissement), pour l'annuler du même
    // montant en négatif si la facture quitte « Payé ».
    public float encaisseeMontant;
    public string encaisseeDateISO;
    // Revue 17 (D5-06, lot 9, décision du 05/11) : la correction d'une facture déjà déclarée « Encaissée » annule d'abord cet encaissement
    // (212 négatif du même montant, daté comme lui). `encaisseeAAnnulerId` : l'identifiant chez Super PDP de la version remplacée, dont
    // l'encaissement (encaisseeMontant, encaisseeDateISO) reste à annuler ; vidé une fois l'annulation passée. Tant qu'il est posé, la
    // rectificative n'est pas déclarée « Encaissée » (son montant écraserait celui à annuler). Modèle 9.
    public string encaisseeAAnnulerId;
    // Revue 17 (D4-01, décision du 05/11, lot 6) : pour qui CETTE facture a été préparée. `acheteur` : le nom du titulaire
    // (la fiche, ou le cédant / futur cessionnaire d'une cession ; jamais un nom tapé). `destinataire` : l'adresse d'envoi,
    // posée à l'émission et réécrite au départ réel (le journal « à qui c'est parti »). La facture d'un autre titulaire que
    // celui de la fiche garde cette adresse (rappel, notification, envoi différé) ; celle du titulaire de la fiche suit
    // l'adresse actuelle. Vides sur les lignes d'avant la mise à jour : une cession les fige (Cessions.Figer).
    public string destinataire;
    public string acheteur;
    // Revue 17 (lot 6b, décisions du 09/11, même modèle 8 que le lot 6) :
    // `retireApresEnvoi` : loyer PARTI chez le locataire puis retiré du suivi par l'exception du départ (D4-02) ; il garde son
    // numéro et son PDF sans statut, et « Refaire » est refusé tant que le marqueur est posé (FactureEmission.RefusRetire) ;
    // il tombe dès que la ligne reçoit un état par le menu de la pastille (FacturationSuivi.SetStatut).
    // `factureCitee` : clé de la facture que CET avoir annule (« Avoir sur la facture n° … », FactureRefacPanel), vide sinon :
    // son « À rembourser » ne compte que ce qui a été versé sur elle (FacturationSuivi.ARembourser).
    public bool retireApresEnvoi;
    public string factureCitee;
    // Revue 17 (D5-12, coutures ; décision du 10/11, modèle 11) : le jour où la facture est passée « Payé » (clic du suivi, dernier
    // versement saisi, décompte de sortie), posé par FacturationSuivi.SetStatut, qui le vide dès qu'il pose un autre état. Sans versement
    // saisi, c'est la date déclarée à Super PDP (« Encaissée »), même si l'appel part plus tard (panne, hors ligne).
    // FacturationSuivi.ApresCorrection (« Envoyé ») et le retour « en attente d'envoi » d'un refus de Super PDP ne le vident pas : sans
    // effet, il n'est lu que pour une facture payée sans versement, et SetStatut le repose au prochain « Payé ».
    public string payeLeISO;
}

/// Un paiement reçu sur une facture.
[Serializable]
public class Versement
{
    public string dateISO;   // "yyyy-MM-dd"
    public float montant;
}
