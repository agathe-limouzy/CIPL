using System;

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
    public string ribId;        // RIB CIPL (banque) sur lequel la facture est réglée
    public string ribNom;       // libellé du RIB (pour affichage / filtre)
    public int corrections;     // nb de fois où la facture a été refaite/corrigée (0 = originale)
    public string dernierRappelISO; // date du dernier rappel d'échéance envoyé (impayé)
}
