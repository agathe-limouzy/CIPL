using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Met à jour le résumé d'entreprise de TOUS les locataires au lancement.
///
/// Remplace le bouton « résumé société » de la fiche : l'information n'était à jour
/// que là où quelqu'un avait pensé à cliquer, donc jamais sur les fiches qu'on ouvre
/// rarement — précisément celles qu'on n'a pas en tête.
///
/// En arrière-plan et en série : l'API publique de l'annuaire des entreprises limite
/// le débit, et rien ici ne presse. L'écran reste utilisable pendant ce temps.
public static class PappersSync
{
    // Une requête à la fois, espacée : ni l'application ni l'API n'ont à souffrir
    // d'un démarrage. C'est un rafraîchissement de confort, pas une urgence.
    const float PauseEntreAppels = 0.4f;

    /// Parcourt les locataires, interroge l'annuaire pour ceux qui ont un SIRET
    /// exploitable, et réécrit leur bloc de résumé. Ne sauvegarde qu'à la fin, et
    /// seulement si quelque chose a changé — un démarrage ne doit pas réécrire quatre
    /// fichiers pour rien.
    public static IEnumerator Tout(IEnumerable<BatimentPrefab> bps)
    {
        if (bps == null) yield break;

        int interroges = 0, majs = 0, echecs = 0;
        foreach (var bp in bps)
        {
            if (bp == null || bp.listLocataire == null) continue;
            bool touche = false;

            foreach (var loc in bp.listLocataire)
            {
                if (loc == null || !PappersResume.SiretExploitable(loc.siretNumber)) continue;

                interroges++;
                bool change = false, echoue = false;
                yield return Interroger(loc, r => change = r, e => echoue = e);
                if (echoue) echecs++;
                if (change) { touche = true; majs++; }

                yield return new WaitForSeconds(PauseEntreAppels);
            }

            if (touche && BatimentManager.Instance != null)
                BatimentManager.Instance.SaveBatiment(bp.getBatiment());
        }

        if (interroges > 0)
            Debug.Log($"[PappersSync] {interroges} locataire(s) interrogé(s), "
                      + $"{majs} résumé(s) mis à jour, {echecs} en échec.");

        // Les fiches déjà construites affichent encore l'ancien commentaire : on les
        // rafraîchit une fois, à la fin, plutôt qu'à chaque locataire.
        if (majs > 0) RafraichirFichesOuvertes();
    }

    /// Rafraîchit le résumé d'UN locataire : à la sauvegarde de sa fiche, où le SIRET
    /// vient peut-être d'être saisi ou corrigé. Attendre le prochain lancement pour
    /// voir le résultat d'une saisie serait déroutant.
    ///
    /// `bp` sert à persister le changement ; passer null met à jour l'objet en mémoire
    /// sans écrire (l'appelant sauvegarde alors lui-même).
    public static IEnumerator Un(BatimentPrefab bp, Locataire loc)
    {
        if (loc == null || !PappersResume.SiretExploitable(loc.siretNumber)) yield break;

        bool change = false;
        yield return Interroger(loc, r => change = r, _ => { });
        if (!change) yield break;

        if (bp != null && BatimentManager.Instance != null)
            BatimentManager.Instance.SaveBatiment(bp.getBatiment());
        RafraichirFichesOuvertes();
    }

    /// L'appel lui-même : interroge l'annuaire et réécrit le bloc de résumé du
    /// locataire s'il a changé. Le SEUL endroit qui touche au commentaire — le
    /// démarrage et la sauvegarde de fiche passent tous les deux par ici.
    static IEnumerator Interroger(Locataire loc, System.Action<bool> onChange,
                                  System.Action<bool> onEchec)
    {
        string resume = null, erreur = null;
        yield return PapperService.Interroger(loc.siretNumber.Trim(),
            data => resume = PappersResume.Construire(data),
            err => erreur = err);

        if (erreur != null)
        {
            // Un SIRET inconnu ou une coupure réseau ne doit pas arrêter les autres :
            // on note et on continue.
            Debug.LogWarning($"[PappersSync] {loc.Name} : {erreur}");
            onEchec?.Invoke(true);
            yield break;
        }
        if (string.IsNullOrEmpty(resume)) yield break;

        string nouveau = PappersResume.Fusionner(loc.commentaire, resume);
        if (nouveau == loc.commentaire) yield break;
        loc.commentaire = nouveau;
        onChange?.Invoke(true);
    }

    static void RafraichirFichesOuvertes()
    {
        foreach (var fiche in Resources.FindObjectsOfTypeAll<LocatairePrefab>())
        {
            // FindObjectsOfTypeAll ramène AUSSI les prefabs d'assets, qui ne sont
            // dans aucune scène et n'ont pas de bâtiment d'origine. Les toucher n'a
            // pas de sens — et modifier un asset depuis le jeu encore moins.
            if (fiche == null || !fiche.gameObject.scene.IsValid()) continue;
            var loc = fiche.GetLocataire();
            if (loc != null && fiche.Commentaire != null) fiche.Commentaire.ApplyValue(loc.commentaire);
        }
    }
}
