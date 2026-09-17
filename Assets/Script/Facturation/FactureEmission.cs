using UnityEngine;

/// Protocole d'émission d'une facture, commun aux quatre panneaux (Loyer,
/// Régularisation, Refacturation, Dépôt).
///
/// Pourquoi ce service existe : la règle « une facture déjà émise ne consomme JAMAIS
/// un second numéro » était recopiée dans les quatre panneaux. Elle a donc pu être
/// oubliée (H2 : présente sur un seul des quatre) puis mal fermée (H2-bis : deux états
/// sur quatre). Elle ne vit plus qu'ici.
///
/// L'ordre compte et n'est pas négociable :
///   1. `Preparer` AVANT de générer le PDF — c'est lui qui donne le numéro à imprimer
///      et le suffixe du nom de fichier, donc le PDF d'origine n'est jamais écrasé ;
///   2. `Enregistrer` APRÈS une génération réussie — sinon le suivi annoncerait une
///      facture qui n'existe pas sur le disque.
public static class FactureEmission
{
    /// Ce que `Preparer` a décidé. À passer tel quel à `Enregistrer`.
    public class Decision
    {
        /// Vrai si la facture existe déjà : on refait une version corrigée.
        public bool Correction;
        /// X de « corrigée(X) ». 0 pour une première émission.
        public int NumeroCorrection;
        /// Numéro à imprimer sur la facture (suffixé si c'est une correction).
        public string NumeroFacture;
        /// À coller au nom du fichier : "" ou "-corrigee3".
        public string SuffixeFichier;
    }

    /// Étape 1 — décide correction ou nouvelle émission, AVANT toute génération.
    ///
    /// `numeroPropose` est le numéro calculé par le panneau pour une facture neuve ;
    /// il est ignoré si la facture a déjà été émise, puisqu'on conserve alors le sien.
    public static Decision Preparer(Locataire loc, string key, string numeroPropose)
    {
        bool correction = FacturationSuivi.EstDejaEmise(loc, key, out var existante);
        int x = correction ? existante.corrections + 1 : 0;

        string numero = numeroPropose;
        if (correction && !string.IsNullOrEmpty(existante.numero))
            numero = existante.numero + $" corrigée({x})";

        return new Decision
        {
            Correction = correction,
            NumeroCorrection = x,
            NumeroFacture = numero,
            SuffixeFichier = correction ? $"-corrigee{x}" : ""
        };
    }

    /// Étape 2 — met le suivi à jour après une génération réussie.
    ///
    /// En correction : conserve numéro et statut, n'avance PAS la séquence.
    /// En première émission : marque la ligne envoyée, consomme le numéro (séquence +1)
    /// et oublie l'ID mémorisé pour que le panneau propose le suivant.
    ///
    /// Renvoie le message à afficher, pour que les quatre panneaux disent la même chose.
    /// `messagePremiereEmission` permet à un panneau de garder sa formulation propre
    /// (« charges passées en payé », « le dépôt n'a pas été modifié »…). Laissé vide,
    /// un message générique est renvoyé.
    public static string Enregistrer(Locataire loc, string key, string type, Decision decision,
                                     string libelle, string echeanceISO, string pdfPath,
                                     float montant, string ribId, FactureInfo info,
                                     string nomLisible, string messagePremiereEmission = null)
    {
        if (decision.Correction)
        {
            FacturationSuivi.MarquerCorrige(loc, key, libelle, pdfPath, montant);
            return $"{nomLisible} corrigée ({decision.NumeroCorrection}) enregistrée.";
        }

        FacturationSuivi.MarquerEnvoye(loc, key, type, libelle, echeanceISO,
                                       decision.NumeroFacture, pdfPath, montant, ribId, RibNom(ribId));

        // Numéro consommé : la séquence (unique par locataire, tous types) avance, et
        // l'ID saisi est oublié pour que la prochaine ouverture propose le suivant.
        loc.factureSeq = Mathf.Max(1, loc.factureSeq) + 1;
        if (info != null) info.numeroId = "";

        return string.IsNullOrEmpty(messagePremiereEmission)
            ? $"{nomLisible} enregistrée (PDF). Envoi réel non activé — rien n'a été émis."
            : messagePremiereEmission;
    }

    /// Phrase de bas de facture adaptée au sens de la somme. « SOMME À NOUS RÉGLER
    /// LE … » est faux quand c'est nous qui remboursons — et c'est la phrase imprimée
    /// en gras sous les totaux, donc la plus lue du document.
    ///
    /// Seule la phrase AUTOMATIQUE est remplacée : un texte saisi à la main est
    /// respecté, l'utilisatrice sait ce qu'elle écrit.
    public static string PhraseSomme(string phraseSaisie, float solde)
    {
        if (solde >= -0.005f) return phraseSaisie;

        bool phraseAuto = string.IsNullOrWhiteSpace(phraseSaisie)
            || phraseSaisie.IndexOf("NOUS RÉGLER", System.StringComparison.OrdinalIgnoreCase) >= 0;

        return phraseAuto ? "SOMME QUI VOUS SERA REMBOURSÉE" : phraseSaisie;
    }

    /// Libellé du solde selon son sens, pour le tableau des totaux.
    public static string LibelleSolde(float solde, string libellePositif)
        => solde < -0.005f ? "Montant à vous rembourser" : libellePositif;

    /// Libellé du RIB CIPL retenu, pour l'affichage dans le suivi. Les quatre panneaux
    /// en avaient chacun leur copie de trois lignes.
    public static string RibNom(string ribId)
    {
        var rib = ReglageService.GetRib(ribId);
        if (rib == null) return "";
        return !string.IsNullOrWhiteSpace(rib.name) ? rib.name : rib.titulaire;
    }
}
