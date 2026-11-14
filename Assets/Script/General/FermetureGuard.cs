using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// Empêche de quitter l'application en perdant une saisie en cours.
///
/// Sur le cloud, chaque modification part au fil de l'eau (EcritureCloud) ; rien n'est
/// réécrit à la fermeture. Une fiche restée en cours d'édition n'est pas enregistrée :
/// reverser l'UI dans les données passe par BatimentPrefab.SaveBatiment(),
/// qui manipule aussi l'interface (boutons, ShowFiche) et n'a rien à faire pendant la
/// destruction de l'application. La seule réponse correcte est donc de PRÉVENIR
/// l'utilisatrice avant de fermer, pas d'enregistrer à sa place.
///
/// L'état « en édition » n'est pas dupliqué dans un booléen (qui finirait par se
/// désynchroniser) : il est lu là où il vit déjà, la visibilité du bouton Enregistrer.
/// Modify() l'affiche, SaveBatiment() le masque.
public static class FermetureGuard
{
    private static bool _quitterConfirme;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        Application.wantsToQuit -= AutoriserFermeture;   // pas de double abonnement
        Application.wantsToQuit += AutoriserFermeture;
    }

    /// Noms des fiches (bâtiments et locataires) actuellement en mode édition.
    public static List<string> NomsEnEdition()
    {
        var noms = new List<string>();
        var fiches = Object.FindObjectsByType<PrefabBatLoc>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var f in fiches)
        {
            if (f == null || !f.EnEdition) continue;
            string nom = f.getName();
            noms.Add(string.IsNullOrWhiteSpace(nom) ? "(sans nom)" : nom);
        }
        return NomsEnEdition(noms, SessionCloud.Active ? EcritureCloud.FichesTenues() : null);   // revue 16c, D8-05
    }

    /// La règle : fiches en saisie + écrans qui tiennent une fiche (facture, achat, charge, révision, départ…), sans doublon.
    public static List<string> NomsEnEdition(IEnumerable<string> fiches, IEnumerable<string> tenues)
        => fiches.Concat(tenues ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();

    /// Revue 17 (D8-03) : un envoi de factures est en cours : le verrou d'envoi tenu sur le cloud, l'e-mail d'un formulaire de facture
    /// en train de partir (FactureEmission.EnvoisEnCours, Revue 17 D4-07, hors cloud aussi), ou l'envoi groupé du lancement (hors cloud
    /// aussi). Quitter au milieu laisserait une facture partie sans son « Envoyé » (elle repartirait au lancement suivant) ou sans son enregistrement.
    public static bool EnvoiEnCours => EcritureCloud.EnvoiEnCours || FactureEnvoiAuto.EnCours;

    /// Quitte sans repasser par la garde (revue 16c, D8-04 : la croix redemandait la confirmation), après l'enregistrement
    /// cloud encore en vol s'il y en a un (D8-03 : il était perdu sans un mot) et après l'envoi de factures en cours (Revue 17, D8-03),
    /// puis après une dernière passe d'envoi des documents (Revue 17, D2-03, lot D).
    public static void QuitterSansGarde()
    {
        _quitterConfirme = true;
        bool envoi = EnvoiEnCours;
        if (!EcritureCloud.EnregistrementEnCours && !envoi) { QuitterApresDocuments(); return; }
        UndoToast.Instance?.ShowInfo(envoi ? "Envoi des factures en cours, fermeture à la fin de l'envoi…"
                                           : "Enregistrement en cours, fermeture dans un instant…");
        int enAttenteAvant = EcritureCloud.NomsEnAttente().Count;
        QuandTermine(() =>
        {
            // Revue 17 (D8-01 reste : D8-N1, D8-V1) : l'écriture attendue a échoué sur une panne (la fiche attend la connexion) ou a
            // été refusée (message au centre) : CIPL reste ouvert, la fiche n'est pas perdue sans un mot ; la croix redemandera.
            bool erreurAffichee = ConfirmDialog.Instance != null && ConfirmDialog.Instance.gameObject.activeSelf;
            if (!QuitterApresAttente(enAttenteAvant, EcritureCloud.NomsEnAttente().Count, erreurAffichee)) { _quitterConfirme = false; return; }
            QuitterApresDocuments();
        });
    }

    /// Revue 17 (D2-03, lot D) : la fermeture fait une dernière passe d'envoi des documents. Un PDF de facture, une photo ou un bail créé
    /// juste avant de fermer ne partait au cloud qu'au prochain lancement de ce poste, alors que sa fiche était déjà au serveur.
    /// Sans entreprise ouverte sur le cloud, ou hors ligne (rien ne partirait) : pas de passe, on quitte tout de suite.
    public static bool DernierePasse(bool entrepriseCloud, bool horsLigne) => entrepriseCloud && !horsLigne;

    static bool DernierePasse() => DernierePasse(SessionCloud.Active && SessionCloud.Entreprise != null, HorsLigne.Actif);

    /// Quitte après la dernière passe des documents (DocumentsCloud.Terminer : 15 s au plus, puis `ensuite` une seule fois).
    static async void QuitterApresDocuments()
    {
        if (!DernierePasse()) { Application.Quit(); return; }
        UndoToast.Instance?.ShowInfo("Envoi des documents…");
        await Task.Yield();   // jamais Application.Quit pendant wantsToQuit (AutoriserFermeture) : Unity annulerait cette fermeture-là
        DocumentsCloud.Terminer(Application.Quit);
    }

    /// Règle pure : après l'attente, on quitte seulement si aucune fiche de plus n'attend la connexion et qu'aucune erreur n'est affichée.
    public static bool QuitterApresAttente(int enAttenteAvant, int enAttenteApres, bool erreurAffichee)
        => enAttenteApres <= enAttenteAvant && !erreurAffichee;

    /// `action` une fois l'écriture en vol et l'envoi de factures terminés (revue 16c, D8-03 : la fermeture attend l'écriture en vol).
    static async void QuandTermine(System.Action action)
    {
        while (EcritureCloud.EnregistrementEnCours || EnvoiEnCours)
        {
            if (EcritureCloud.EnregistrementEnCours) { try { await EcritureCloud.Attendre(); } catch (System.Exception) { /* déjà signalé */ } }
            else await Task.Delay(200);
        }
        action?.Invoke();
    }

    public static string MessageAvertissement(List<string> enEdition)
        => MessagePerte(enEdition, "Quitter");

    /// `action` ouvre la phrase : « Quitter maintenant perdrait ces saisies. »
    private static string MessagePerte(List<string> enEdition, string action)
        => "Ces fiches sont en cours de modification et n'ont pas été enregistrées :\n\n" +
           $"• {string.Join("\n• ", enEdition)}\n\n" +
           $"{action} maintenant perdrait ces saisies. Vous pouvez annuler, puis utiliser " +
           "le bouton Enregistrer de la fiche.";

    /// « Quitter sans enregistrer » quand une saisie serait perdue, « Quitter quand même » sinon (même règle pour « Continuer »).
    public static string Bouton(string verbe, int enEdition) => enEdition > 0 ? $"{verbe} sans enregistrer" : $"{verbe} quand même";

    /// Exécute `suite`, après confirmation si une saisie, une fiche enregistrée hors ligne qui attend la connexion (Revue 17, D8-14 :
    /// « Se déconnecter » hors ligne la perdait sans un mot) ou un envoi de factures en cours (D8-03) serait perdu.
    ///
    /// Quitter n'est pas la seule façon de perdre une saisie : basculer d'entreprise,
    /// changer d'emplacement de sauvegarde ou revenir au dossier par défaut passent
    /// tous par `BatimentManager.ReloadFromDisk`, qui DÉTRUIT les prefabs ouverts avant
    /// de relire le disque. Une fiche en cours de modification disparaissait sans un mot.
    ///
    /// La confirmation est demandée AVANT le début de l'opération, jamais au moment du
    /// rechargement : quand `ReloadFromDisk` est atteint, les fichiers ont déjà été
    /// déplacés, et renoncer à cet instant laisserait le disque et l'affichage
    /// désaccordés.
    public static void ConfirmerPerteSaisies(string action, System.Action suite)
    {
        if (suite == null) return;

        var enEdition = NomsEnEdition();
        var enAttente = EcritureCloud.NomsEnAttente();
        bool envoi = EnvoiEnCours;
        if (enEdition.Count == 0 && enAttente.Count == 0 && !envoi) { suite(); return; }

        // Même principe que pour la fermeture : sans boîte de dialogue disponible, on
        // trace dans la console plutôt que de rendre l'action impossible.
        if (ConfirmDialog.Instance == null)
        {
            Debug.LogWarning($"[FermetureGuard] {action} : {enEdition.Count} fiche(s) non enregistrée(s), {enAttente.Count} en attente de "
                             + $"connexion{(envoi ? ", envoi de factures en cours" : "")} : perdues.");
            suite();
            return;
        }

        var (_, texte) = Avertissement(enEdition, enAttente, action, envoi);
        ConfirmDialog.Instance.Show(action, texte, suite, Bouton("Continuer", enEdition.Count));
    }

    /// Hook Unity : renvoyer false ANNULE la fermeture en cours. On affiche alors la
    /// confirmation, et on relance la fermeture seulement si l'utilisatrice l'accepte.
    /// (Non appelé dans l'éditeur quand on sort du mode Play — le bouton Quitter de
    /// l'app couvre ce cas en passant par le même contrôle.)
    /// Revue 17 (D8-01) : ce que fait la fermeture. Prévenir dès qu'une saisie, ou une fiche enregistrée hors ligne qui attend la
    /// connexion (gardée en mémoire seulement), serait perdue ; attendre une écriture en vol ou un envoi de factures (D8-03) ; sinon quitter.
    public enum Choix { Quitter, Attendre, Prevenir }

    public static Choix Decision(int enEdition, bool enregistrementEnCours, int enAttente, bool envoiEnCours = false)
        => enEdition > 0 || enAttente > 0 ? Choix.Prevenir : enregistrementEnCours || envoiEnCours ? Choix.Attendre : Choix.Quitter;

    /// Titre et texte de l'avertissement : les fiches en saisie, celles qui attendent la connexion, l'envoi en cours. `action` ouvre
    /// les phrases (« Quitter », « Se déconnecter », « Changer d'entreprise »…).
    public static (string titre, string texte) Avertissement(List<string> enEdition, List<string> enAttente,
                                                             string action = "Quitter", bool envoiEnCours = false)
    {
        var parts = new List<string>();
        if (enEdition.Count > 0) parts.Add(MessagePerte(enEdition, action));
        if (enAttente.Count > 0)
            parts.Add("Hors ligne : ces fiches attendent la connexion pour être enregistrées :\n\n" +
                      $"• {string.Join("\n• ", enAttente)}\n\n" +
                      $"{action} maintenant les perdrait. Attendez le retour de la connexion (bandeau « Réessayer »).");
        if (envoiEnCours)
            parts.Add($"Un envoi de factures est en cours : {action} maintenant l'interromprait, et une facture partie à l'instant " +
                      "pourrait repartir au prochain lancement. Attendez la fin de l'envoi.");
        string titre = enEdition.Count > 0 ? "Modifications non enregistrées"
                     : enAttente.Count > 0 ? "Modifications pas encore envoyées" : "Envoi de factures en cours";
        return (titre, string.Join("\n\n", parts));
    }

    /// La croix « Quitter » : la même décision que la garde (Revue 17, D8-13 : elle était écrite deux fois), et une confirmation
    /// même quand rien ne serait perdu.
    public static void Demander(System.Action quitter)
    {
        var enEdition = NomsEnEdition();
        var enAttente = EcritureCloud.NomsEnAttente();
        bool envoi = EnvoiEnCours;
        if (enEdition.Count == 0 && enAttente.Count == 0 && !envoi)
        {
            ConfirmDialog.Instance.Show("Quitter l'application", "Voulez-vous vraiment quitter ?", quitter, "Quitter");
            return;
        }
        var (titre, texte) = Avertissement(enEdition, enAttente, "Quitter", envoi);
        ConfirmDialog.Instance.Show(titre, texte, quitter, Bouton("Quitter", enEdition.Count));
    }

    private static bool AutoriserFermeture()
    {
        if (_quitterConfirme) return true;

        var enEdition = NomsEnEdition();
        var enAttente = EcritureCloud.NomsEnAttente();   // Revue 17 (D8-01)
        bool envoi = EnvoiEnCours;                        // Revue 17 (D8-03)
        switch (Decision(enEdition.Count, EcritureCloud.EnregistrementEnCours, enAttente.Count, envoi))
        {
            case Choix.Quitter:
                // Revue 17 (D2-03, lot D) : la croix de Windows et Alt+F4 font aussi la dernière passe d'envoi des documents.
                if (!DernierePasse()) return true;
                QuitterSansGarde(); return false;
            case Choix.Attendre: QuitterSansGarde(); return false;   // revue 16c (D8-03) : la fermeture attend l'écriture en vol
        }

        // Sans boîte de dialogue disponible, on ne bloque pas la fermeture : mieux vaut
        // quitter que laisser l'application impossible à fermer.
        if (ConfirmDialog.Instance == null)
        {
            Debug.LogWarning($"[FermetureGuard] Fermeture avec {enEdition.Count} fiche(s) non enregistrée(s), {enAttente.Count} en attente de connexion.");
            return true;
        }

        var (titre, texte) = Avertissement(enEdition, enAttente, "Quitter", envoi);
        ConfirmDialog.Instance.Show(titre, texte, QuitterSansGarde, Bouton("Quitter", enEdition.Count));
        return false;
    }
}
