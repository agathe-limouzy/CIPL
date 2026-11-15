using TMPro;
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
    /// Revue 16c (D4-09) : le mode d'envoi est un réglage d'entreprise (Réglages › Intégrations), plus une case par facture.

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
        /// Le numéro de base (sans « corrigée »), lisible dans un nom de fichier (« 2026-10003 ») : deux factures
        /// différentes n'ont jamais le même PDF (revue 16c, D4-01 : le suivi fusionne deux lignes au même PDF).
        public string NumeroFichier;
        /// Revue 17 (D4-06, coutures) : le numéro de la facture sans « corrigée(X) », celui que porte chaque ligne du suivi : une ligne
        /// ajoutée à une facture corrigée (liste, charge) le reçoit (EnregistrerLiee).
        public string NumeroBase;
        /// Non null : l'émission est refusée (numéro déjà porté par une autre facture du locataire, revue 16c D4-04) ;
        /// le panneau l'affiche et s'arrête.
        public string Refus;
    }

    /// Les chiffres de fin de l'ID saisi (« 047 » → 47, « A12 » → 12 ; 0 sans chiffres) : la séquence ne doit
    /// jamais redescendre sous un numéro déjà imprimé (revue 16c, D4-04).
    public static int NumeroSaisi(string numeroId)
    {
        string s = (numeroId ?? "").Trim();
        int fin = s.Length;
        while (fin > 0 && char.IsDigit(s[fin - 1])) fin--;
        return fin < s.Length && int.TryParse(s.Substring(fin), out int n) ? n : 0;
    }

    /// Étape 1 — décide correction ou nouvelle émission, AVANT toute génération.
    ///
    /// `numeroPropose` est le numéro calculé par le panneau pour une facture neuve ;
    /// il est ignoré si la facture a déjà été émise, puisqu'on conserve alors le sien.
    /// `periodicite` (Revue 17, D4-04) : celle du loyer préparé ; une facture émise sous la même clé avec une autre périodicité
    /// n'est pas la même période, elle ne se corrige pas d'ici.
    public static Decision Preparer(Locataire loc, string key, string numeroPropose, string periodicite = null)
    {
        bool dejaEmise = FacturationSuivi.EstDejaEmise(loc, key, out var existante);
        // Revue 17 (D4-02, décision du 05/11) : une ligne « remise » (un numéro et un PDF, mais un statut vide ou « À faire » :
        // préparée puis remise en automatique) n'est jamais partie : refaite, elle reprend son numéro et son fichier, comme une
        // facture en attente d'envoi (avant : un numéro neuf, et l'ancien laissait un trou dans la numérotation).
        bool remise = !dejaEmise && !string.IsNullOrEmpty(existante?.numero) && !string.IsNullOrEmpty(existante?.pdfPath);
        dejaEmise |= remise;

        // Une facture PRÉPARÉE mais jamais expédiée se remplace, elle ne se corrige
        // pas : personne n'a vu la version précédente. La marquer « corrigée(1) »
        // fabriquait une rectificative sans facture d'origine — un document faux — et
        // laissait deux PDF pour une seule facture.
        //
        // Le numéro, lui, reste acquis dans les deux cas : il a été consommé à la
        // première génération, et un numéro consommé le reste (H2-bis).
        bool jamaisPartie = dejaEmise && (existante.statut == "AttenteEnvoi" || remise);
        bool correction = dejaEmise && !jamaisPartie;

        int x = correction ? existante.corrections + 1 : 0;

        string numero = numeroPropose;
        string numeroBase = dejaEmise && !string.IsNullOrEmpty(existante.numero) ? existante.numero : numeroPropose;
        if (dejaEmise && !string.IsNullOrEmpty(existante.numero))
            numero = correction ? existante.numero + $" corrigée({x})" : existante.numero;

        // Revue 17 (lot 6b, décision du 09/11) : un loyer parti puis retiré du suivi (marqueur) ne se refait pas : refus commun aux
        // quatre formulaires (et à NumeroRepris / SeraUneCorrection), avant toute autre règle.
        string refus = RefusRetire(existante);
        // Revue 16c (D4-04) : un numéro déjà porté par une AUTRE facture du locataire (ID saisi à la main) est refusé.
        // Revue 17 (D4-02) : une ligne remise garde le sien (numéro et PDF), même sans statut.
        if (refus == null && !dejaEmise && !string.IsNullOrEmpty(numeroPropose) && loc?.facturesEtat != null)
            foreach (var autre in loc.facturesEtat)
                if (autre.key != key && autre.numero == numeroPropose && (!string.IsNullOrEmpty(autre.statut) || !string.IsNullOrEmpty(autre.pdfPath)))
                {
                    refus = $"Le numéro {numeroPropose} est déjà celui d'une autre facture de ce locataire (« {autre.libelle} ») : changez l'ID.";
                    break;
                }

        // Revue 17 (D4-04) : la clé d'un loyer est un numéro de période ; après un changement de périodicité, « P3 » peut porter
        // la facture de mars (mensuel) quand on prépare le 3e trimestre : la « corriger » ferait de mars une rectificative pour
        // juillet à septembre. Refusé ; les périodes déjà facturées gardent leur périodicité. Une ligne remise n'a jamais été
        // envoyée : elle se refait dans la nouvelle périodicité (D4-02 ; sinon ce refus ne se levait plus).
        // Revue 17 (avenant de périodicité, lot 14) : ce refus reste le filet des fiches changées sans date. Son conseil « au 1er
        // janvier » était faux (du trimestriel au mensuel, les numéros se recouvrent quand même) : il renvoie à la date d'effet,
        // qui garde aux périodes d'avant leur périodicité et leurs numéros (FacturationSuivi.Plan).
        if (refus == null && dejaEmise && !remise && periodicite != null && !string.IsNullOrEmpty(existante.periodicite)
            && existante.periodicite != periodicite)
        {
            string n = string.IsNullOrEmpty(existante.numero) ? "" : " (n° " + existante.numero + ")";
            refus = $"« {existante.libelle} »{n} a été émise avec l'ancienne périodicité ({existante.periodicite}) sous le même "
                  + "numéro de période : générer cette période la remplacerait par une rectificative qui ne rectifie rien. Les "
                  + "périodes déjà facturées gardent leur périodicité ; pour en changer, saisissez la nouvelle périodicité avec sa "
                  + "date d'effet dans « Gestion du loyer ».";
        }

        return new Decision
        {
            Refus = refus,
            Correction = correction,
            Reecriture = jamaisPartie,
            NumeroCorrection = x,
            NumeroFacture = numero,
            SuffixeFichier = correction ? $"-corrigee{x}" : "",
            NumeroBase = numeroBase,
            NumeroFichier = (numeroBase ?? "").Replace('/', '-').Replace('\\', '-').Trim(),
        };
    }

    /// Revue 17 (lot 6b, décision du 09/11) : un loyer PARTI chez le locataire puis retiré du suivi (exception du départ, D4-02) garde
    /// son numéro et son PDF avec le marqueur `retireApresEnvoi` : tant qu'il est posé, l'émission sur cette ligne est refusée au
    /// centre (« Refaire » du suivi, et Preparer pour les quatre formulaires) : refaite, elle reprendrait le numéro et écraserait le
    /// PDF reçu. Le marqueur tombe dès que la ligne reçoit un état par le menu de la pastille (FacturationSuivi.SetStatut). La ligne
    /// du suivi est une copie (CopieDe) qui porte le marqueur : elle convient aussi. Null = permis.
    public static string RefusRetire(FactureEtat rec)
        => rec == null || !rec.retireApresEnvoi ? null
         : $"« {rec.libelle} » est parti chez le locataire avant d'être retiré du suivi : ne le refaites pas, son numéro et le PDF reçu "
         + "seraient écrasés. Si le départ est annulé ou repoussé, cliquez son état et remettez-le « Envoyé » ou « Payé ».";

    /// Revue 17 (D6-04, décision du 05/11) : vrai si émettre cette clé fera une correction (« corrigée(X) », envoyée tout de suite) :
    /// la règle de Preparer elle-même (facture émise avec son PDF, et pas en attente d'envoi), lue par le titre « (correction) » et
    /// le bouton « Corriger et envoyer » du loyer. Une période forcée « Payé » sans PDF n'en est pas une : Preparer en émet une neuve.
    /// Revue 17 (2c, décision du 06/11) : avec la périodicité, la règle de la ligne « N° de facture » (NumeroRepris) : une émission
    /// refusée (D4-04, autre périodicité) n'est pas une correction. Avant : titre « (correction) », mais un numéro neuf sur la ligne.
    public static bool SeraUneCorrection(Locataire loc, string key, string periodicite = null)
    {
        var d = Preparer(loc, key, null, periodicite);
        return d.Refus == null && d.Correction;
    }

    /// Décision du 11/11 (D4-10, D6-N10) : pendant une correction, le bouton d'émission de la régularisation, de la refacturation et
    /// du dépôt dit « Corriger et envoyer », comme celui du loyer ; sinon « Sauvegarder et envoyer ».
    public static void NommerBouton(Component bouton, bool correction)
    {
        var t = bouton != null ? bouton.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = correction ? "Corriger et envoyer" : "Sauvegarder et envoyer";
    }

    /// Revue 17 (2c, décision du 06/11) : le numéro que Preparer imprimera en gardant celui de la facture (pas encore partie : le
    /// même ; déjà partie : « N corrigée(X) »), sinon null (facture neuve, ou émission refusée). Lecture seule : rien n'est modifié.
    public static string NumeroRepris(Locataire loc, string key, string periodicite = null)
    {
        var d = Preparer(loc, key, null, periodicite);
        return d.Refus == null && (d.Correction || d.Reecriture) && !string.IsNullOrEmpty(d.NumeroFacture) ? d.NumeroFacture : null;
    }

    /// Revue 17 (2c, décision du 06/11) : la ligne « N° de facture » des quatre formulaires (BuildNumeroRow : l'aide, puis
    /// [préfixe] [ID locataire]). Numéro repris : la boîte le montre, grisé, et l'ID est vide, non modifiable, « (repris) » ; préfixe
    /// + ID donnent donc le numéro imprimé, et l'aperçu le montre aussi. Sinon, comme avant : le préfixe, et l'ID modifiable (celui
    /// tapé avant le numéro repris, mis de côté dans `idGarde`, ou l'ID chargé, sinon la séquence). Avant : la ligne proposait le
    /// numéro suivant de la séquence, que l'émission n'imprimait pas.
    public static void AfficherNumero(TMP_Text aide, TMP_Text prefixe, TMP_InputField id, string repris, string prefixeNeuf,
                                      int seq, string aideRepris, ref string idGarde)
    {
        if (repris != null) { if (idGarde == null) idGarde = id.text ?? ""; id.text = ""; }
        else if (idGarde != null) { id.text = idGarde; idGarde = null; }
        if (repris == null && string.IsNullOrWhiteSpace(id.text)) id.text = Mathf.Max(1, seq).ToString("D3");
        id.interactable = repris == null;
        if (id.placeholder is TMP_Text indication) indication.text = repris != null ? "(repris)" : "ID locataire (ex. 001)";
        prefixe.text = repris ?? prefixeNeuf;
        // Revue 17 (2c, décision du 06/11) : grisé comme « (repris) » à côté et les lignes grisées du suivi (TexteSecondaire, la
        // couleur des libellés, ne se distinguait pas d'un numéro modifiable).
        prefixe.color = repris != null ? UITheme.TexteExemple : UITheme.TextePrincipal;
        if (aide != null) aide.text = repris != null ? aideRepris : "N° de facture  =  format  +  ID locataire";
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
            FacturationSuivi.MarquerCorrige(loc, key, libelle, pdfPath, montant, echeanceISO, ribId, RibNom(ribId));
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
            // Revue 16c (D4-04) : un ID saisi à la main plus grand que la séquence la fait avancer (sinon le numéro
            // suivant revenait en arrière, puis retombait sur un numéro déjà imprimé).
            loc.factureSeq = Mathf.Max(Mathf.Max(1, loc.factureSeq), NumeroSaisi(info?.numeroId)) + 1;
            // Décision du 14/11 (BV8) : la séquence est commune aux quatre formulaires : l'ID mémorisé par un essai non émis dans un
            // AUTRE formulaire (refus, envoi échoué) désigne maintenant un numéro pris (avant : « Le numéro … est déjà celui d'une autre
            // facture »). Tous sont oubliés, pas seulement celui du formulaire qui émet.
            foreach (var i in new[] { info, loc.factureLoyer, loc.factureRegul, loc.factureRefac, loc.factureDepot }) if (i != null) i.numeroId = "";
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

    /// Revue 17 (D4-06, coutures) : les AUTRES lignes d'une même facture (régularisation regroupée : une ligne par liste ; refacturation
    /// de plusieurs charges : une par charge) reçoivent ce que reçoit la ligne porteuse (Enregistrer) : le PDF, l'échéance, le RIB et le
    /// numéro de base ; le numéro n'est consommé qu'une fois, par la ligne porteuse. Avant, la règle était recopiée dans les deux
    /// formulaires : une correction ne donnait ni la nouvelle échéance ni le RIB à ces lignes (la même facture « Impayé » sur une ligne,
    /// « Envoyé » sur l'autre), et une ligne ajoutée à une correction portait « N corrigée(X) » comme numéro (lue comme une autre facture).
    public static void EnregistrerLiee(Locataire loc, string key, string type, Decision decision, string libelle, string echeanceISO,
                                       string pdfPath, float montant, string ribId, bool envoyeReellement)
    {
        if (decision.Correction && FacturationSuivi.EstDejaEmise(loc, key, out _))
            FacturationSuivi.MarquerCorrige(loc, key, libelle, pdfPath, montant, echeanceISO, ribId, RibNom(ribId));
        else
            FacturationSuivi.MarquerEnvoye(loc, key, type, libelle, echeanceISO, decision.NumeroBase, pdfPath, montant, ribId, RibNom(ribId),
                                           envoyeReellement);
    }

    /// Revue 17 (D6-19, coutures ; décision du 09/11) : un total nul n'est ni une facture ni un avoir : rien à facturer. Refusé au centre
    /// AVANT tout (le PDF, l'envoi différé, la branche « premiers pas »), pour le loyer, la régularisation et la refacturation ; jamais
    /// pour le dépôt de garantie (un dépôt « à l'équilibre » est voulu). Avant : le loyer seul, après le PDF et la branche « premiers
    /// pas » (un loyer de 0 € y prenait un numéro). Null : rien à refuser.
    public static string RefusTotalNul(float ttc, string sujet)
        => Mathf.Abs(ttc) > 0.004f ? null : $"Le total de « {sujet} » est nul : rien à facturer. Corrigez les montants.";

    /// Revue 17 (D6-15, coutures) : sans RIB réglé, la facture partait avec un relevé d'identité bancaire vide (le locataire ne peut pas
    /// payer par virement). Refusé au centre avant la génération du PDF, dans les quatre formulaires (« premiers pas » compris : la
    /// facture émise sans envoi part plus tard). `rib` : celui choisi dans le formulaire. Null : rien à refuser.
    public static string RefusSansRib(RibData rib)
        => rib != null ? null
         : "Aucun RIB n'est enregistré : la facture partirait avec un relevé d'identité bancaire vide. Ajoutez un RIB dans "
         + "Réglages → Entreprise (« + Ajouter un RIB ») ; sur le cloud, seul l'administrateur le peut. Rien n'a été émis.";

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

    /// Revue 17 (06/11, trouvé en vérifiant D6-08) : la phrase de règlement d'usine (« SOMME À NOUS RÉGLER LE 5 octobre 2026 »,
    /// « SOMME QUI VOUS SERA REMBOURSÉE ») suit l'échéance et le signe du solde : elle n'est pas mémorisée, la facture suivante
    /// reprend celle du moment. Avant, elle était gardée telle quelle : le loyer de novembre proposait, et imprimait,
    /// « … LE 5 octobre 2026 ». Une phrase d'usine déjà mémorisée, quelle que soit sa date, est reconnue à l'ouverture.
    public static bool EstPhraseUsine(string phrase)
    {
        string p = (phrase ?? "").Trim();
        const string debut = "SOMME À NOUS RÉGLER LE ";
        return p == "SOMME QUI VOUS SERA REMBOURSÉE"
            || p.StartsWith(debut, System.StringComparison.Ordinal)
               && System.DateTime.TryParseExact(p.Substring(debut.Length), "d MMMM yyyy", FacturePdfService.FrCulture,
                                                System.Globalization.DateTimeStyles.None, out _);
    }

    /// Ce que les formulaires mémorisent : rien pour une phrase d'usine, la phrase personnalisée sinon.
    public static string PhraseAMemoriser(string phrase) => EstPhraseUsine(phrase) ? "" : phrase;

    /// Libellé du solde selon son sens, pour le tableau des totaux.
    public static string LibelleSolde(float solde, string libellePositif)
        => solde < -0.005f ? "Montant à vous rembourser" : libellePositif;

    /// Libellé du RIB CIPL retenu, pour l'affichage dans le suivi. Les quatre panneaux
    /// en avaient chacun leur copie de trois lignes.
    /// « Retour » pendant un envoi (revue 16c, D4-02) : refusé, avec un message. Fermer le panneau arrête son envoi
    /// entre l'e-mail et l'enregistrement : l'e-mail partait, la facture n'était ni enregistrée ni numérotée.
    public static bool RefuseFermeture(bool envoiEnCours)
    {
        if (envoiEnCours) UndoToast.Instance?.ShowInfo("Envoi en cours : patientez, la facture n'est pas encore enregistrée.");
        return envoiEnCours;
    }

    /// Revue 17 (D4-07, coutures) : le nombre de formulaires de facture dont l'e-mail est en train de partir. Lu par
    /// EcritureCloud.EnvoiEnCours (un rechargement et la fermeture de CIPL attendent la fin) et par EcritureCloud.Occupe (la relecture
    /// en direct aussi). Avant, seul l'envoi groupé du lancement comptait : une fermeture venue du cloud (fiche libérée par un admin,
    /// reprise après une coupure) arrêtait le formulaire entre l'e-mail et l'enregistrement, la facture reçue n'était ni enregistrée
    /// ni numérotée.
    public static int EnvoisEnCours { get; private set; }

    /// Début (`debut` vrai) ou fin de l'envoi d'un formulaire : son drapeau et le compteur bougent ensemble, une seule fois. La fin
    /// est rappelée par OnDisable (formulaire fermé ou détruit) : le compteur ne reste jamais bloqué.
    public static void NoterEnvoi(ref bool enCours, bool debut)
    {
        if (enCours == debut) return;
        enCours = debut;
        EnvoisEnCours += debut ? 1 : -1;
    }

    /// Revue 17 (D4-07, coutures) : à la fin de l'envoi, la fiche du formulaire a été vidée (BatimentManager.Vider : changement
    /// d'entreprise, déconnexion, exclusion, rechargement après ses 60 s d'attente) : l'e-mail est parti, mais Finaliser écrirait sur
    /// une fiche morte (enregistrement refusé, puis le toast « enregistrée et envoyée »). Dit au centre ce qui s'est passé ; le
    /// formulaire se ferme sans finaliser. Rien n'est consommé : refaite, la facture reprend son numéro.
    public static bool FichePerdue(Batiment bat, string dest)
    {
        if (!EcritureCloud.Perime(bat)) return false;
        ConfirmDialog.Erreur($"La facture est partie par e-mail à {dest}, mais sa fiche a été fermée pendant l'envoi : elle n'est pas "
            + "enregistrée. Refaites-la depuis le suivi : elle reprendra le même numéro.");
        return true;
    }

    public static string RibNom(string ribId)
    {
        var rib = ReglageService.GetRib(ribId);
        if (rib == null) return "";
        return !string.IsNullOrWhiteSpace(rib.name) ? rib.name : rib.titulaire;
    }
}
