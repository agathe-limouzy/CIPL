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
}

/// Un paiement reçu sur une facture.
[Serializable]
public class Versement
{
    public string dateISO;   // "yyyy-MM-dd"
    public float montant;
}
