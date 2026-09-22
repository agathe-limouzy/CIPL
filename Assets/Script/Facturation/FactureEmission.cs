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
        /// Vrai si la facture est déjà PARTIE : on émet une version corrigée, qui
        /// porte le suffixe « corrigée(X) » et son propre fichier.
        public bool Correction;
        /// Vrai si la facture était préparée mais jamais expédiée : on la REMPLACE
        /// purement et simplement — même numéro, même fichier, aucun compteur.
        public bool Reecriture;
        /// X de « corrigée(X) ». 0 pour une première émission ou une réécriture.
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
        bool dejaEmise = FacturationSuivi.EstDejaEmise(loc, key, out var existante);

        // Une facture PRÉPARÉE mais jamais expédiée se remplace, elle ne se corrige
        // pas : personne n'a vu la version précédente. La marquer « corrigée(1) »
        // fabriquait une rectificative sans facture d'origine — un document faux — et
        // laissait deux PDF pour une seule facture.
        //
        // Le numéro, lui, reste acquis dans les deux cas : il a été consommé à la
        // première génération, et un numéro consommé le reste (H2-bis).
        bool jamaisPartie = dejaEmise && existante.statut == "AttenteEnvoi";
        bool correction = dejaEmise && !jamaisPartie;

        int x = correction ? existante.corrections + 1 : 0;

        string numero = numeroPropose;
        if (dejaEmise && !string.IsNullOrEmpty(existante.numero))
            numero = correction ? existante.numero + $" corrigée({x})" : existante.numero;

        return new Decision
        {
            Correction = correction,
            Reecriture = jamaisPartie,
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
    /// `envoyeReellement` : le document est parti (email accepté par le serveur).
    /// Sinon la ligne reste « En attente d'envoi » — le suivi ne doit jamais annoncer
    /// un envoi qui n'a pas eu lieu.
    public static string Enregistrer(Locataire loc, string key, string type, Decision decision,
                                     string libelle, string echeanceISO, string pdfPath,
                                     float montant, string ribId, FactureInfo info,
                                     string nomLisible, bool envoyeReellement,
                                     string messagePremiereEmission = null)
    {
        if (decision.Correction)
        {
            FacturationSuivi.MarquerCorrige(loc, key, libelle, pdfPath, montant);
            return $"{nomLisible} corrigée ({decision.NumeroCorrection}) enregistrée.";
        }

        // Remplace la ligne : PDF, montant, échéance et statut sont réécrits. Sur une
        // facture jamais partie, c'est exactement l'effet voulu — la nouvelle version
        // prend la place de l'ancienne dans la file d'envoi, et partira à sa date.
        FacturationSuivi.MarquerEnvoye(loc, key, type, libelle, echeanceISO,
                                       decision.NumeroFacture, pdfPath, montant, ribId, RibNom(ribId),
                                       envoyeReellement);

        // Numéro consommé : la séquence (unique par locataire, tous types) avance, et
        // l'ID saisi est oublié pour que la prochaine ouverture propose le suivant.
        // Une réécriture, elle, réutilise un numéro DÉJÀ consommé : l'avancer une
        // seconde fois créerait un trou dans la numérotation.
        if (!decision.Reecriture)
        {
            loc.factureSeq = Mathf.Max(1, loc.factureSeq) + 1;
            if (info != null) info.numeroId = "";
        }

        // Le message doit dire ce qui s'est RÉELLEMENT passé. Il était renvoyé à
        // l'identique dans les deux cas, si bien qu'après un envoi réussi le panneau
        // affichait « Rien n'a été envoyé » suivi de « et envoyée à … » : la phrase
        // se contredisait elle-même, alors que le mail était bien parti.
        string basse = string.IsNullOrEmpty(messagePremiereEmission)
            ? (decision.Reecriture
                ? $"{nomLisible} remplacée (même numéro)"
                : $"{nomLisible} enregistrée (PDF)")
            : messagePremiereEmission;

        // Envoi réussi : le panneau ajoute « et envoyée à … », rien à dire de plus.
        return envoyeReellement
            ? basse
            : basse + " — rien n'a été envoyé, la ligne reste « en attente d'envoi ».";
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
