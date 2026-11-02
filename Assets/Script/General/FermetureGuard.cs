using System.Collections.Generic;
using System.Linq;
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

    /// Quitte sans repasser par la garde (revue 16c, D8-04 : la croix redemandait la confirmation), après l'enregistrement
    /// cloud encore en vol s'il y en a un (D8-03 : il était perdu sans un mot).
    public static void QuitterSansGarde()
    {
        _quitterConfirme = true;
        if (!EcritureCloud.EnregistrementEnCours) { Application.Quit(); return; }
        UndoToast.Instance?.ShowInfo("Enregistrement en cours, fermeture dans un instant…");
        EcritureCloud.QuandTermine(Application.Quit);
    }

    public static string MessageAvertissement(List<string> enEdition)
        => MessagePerte(enEdition, "Quitter");

    /// `action` ouvre la phrase : « Quitter maintenant perdrait ces saisies. »
    private static string MessagePerte(List<string> enEdition, string action)
        => "Ces fiches sont en cours de modification et n'ont pas été enregistrées :\n\n" +
           $"• {string.Join("\n• ", enEdition)}\n\n" +
           $"{action} maintenant perdrait ces saisies. Vous pouvez annuler, puis utiliser " +
           "le bouton Enregistrer de la fiche.";

    /// Exécute `suite`, après confirmation si des fiches sont en cours d'édition.
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
        if (enEdition.Count == 0) { suite(); return; }

        // Même principe que pour la fermeture : sans boîte de dialogue disponible, on
        // trace dans la console plutôt que de rendre l'action impossible.
        if (ConfirmDialog.Instance == null)
        {
            Debug.LogWarning($"[FermetureGuard] {action} : {enEdition.Count} fiche(s) non enregistrée(s) seront perdues.");
            suite();
            return;
        }

        ConfirmDialog.Instance.Show(action, MessagePerte(enEdition, action), suite,
                                    "Continuer sans enregistrer");
    }

    /// Hook Unity : renvoyer false ANNULE la fermeture en cours. On affiche alors la
    /// confirmation, et on relance la fermeture seulement si l'utilisatrice l'accepte.
    /// (Non appelé dans l'éditeur quand on sort du mode Play — le bouton Quitter de
    /// l'app couvre ce cas en passant par le même contrôle.)
    private static bool AutoriserFermeture()
    {
        if (_quitterConfirme) return true;

        var enEdition = NomsEnEdition();
        if (enEdition.Count == 0)
        {
            if (!EcritureCloud.EnregistrementEnCours) return true;
            QuitterSansGarde();   // revue 16c (D8-03) : la fermeture attend l'écriture en vol
            return false;
        }

        // Sans boîte de dialogue disponible, on ne bloque pas la fermeture : mieux vaut
        // quitter que laisser l'application impossible à fermer.
        if (ConfirmDialog.Instance == null)
        {
            Debug.LogWarning($"[FermetureGuard] Fermeture avec {enEdition.Count} fiche(s) non enregistrée(s).");
            return true;
        }

        ConfirmDialog.Instance.Show(
            "Modifications non enregistrées",
            MessageAvertissement(enEdition),
            QuitterSansGarde,
            "Quitter sans enregistrer");

        return false;
    }
}
