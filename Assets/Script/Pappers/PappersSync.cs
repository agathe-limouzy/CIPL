using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// Met à jour le résumé d'entreprise de TOUS les locataires au lancement.
/// Sur le cloud (étape 16b) : la fiche Pappers par la fonction serveur et les procédures du BODACC,
/// avec la ligne « État » et les objectifs automatiques (`SocieteLocataire`) ; hors cloud, l'annuaire.
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
    /// Étape 10 (cloud) : `libre` dit si un locataire peut être réécrit (personne ne modifie sa
    /// fiche) ; ceux qui ne le sont pas sont sautés, ils seront rafraîchis à la tournée suivante.
    public static IEnumerator Tout(IEnumerable<BatimentPrefab> bps, System.Func<string, bool> libre = null)
    {
        if (bps == null) yield break;
        if (SessionCloud.Active) { yield return ToutCloud(bps.ToList(), libre); yield break; }

        int interroges = 0, majs = 0, echecs = 0;
        foreach (var bp in bps)
        {
            if (bp == null || bp.listLocataire == null) continue;
            bool touche = false;

            foreach (var loc in bp.listLocataire)
            {
                if (loc == null || !PappersResume.SiretExploitable(loc.siretNumber)) continue;
                if (libre != null && !libre(loc.id)) continue;

                interroges++;
                bool change = false, echoue = false;
                yield return Interroger(loc, r => change = r, e => echoue = e);
                if (echoue) echecs++;
                if (change) { touche = true; majs++; }

                yield return new WaitForSeconds(PauseEntreAppels);
            }

            // Fiche détruite pendant la tournée (entreprise changée, fiche relue) : rien à écrire,
            // sinon l'ancienne fiche partirait dans l'entreprise ouverte depuis.
            if (bp == null) yield break;
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
        if (SessionCloud.Active)
        {
            List<SocieteLocataire.Procedure> procs = null;
            yield return Bodacc(new List<string> { SocieteLocataire.Siren(loc.siretNumber) }, r => procs = r);
            if (procs == null) yield break;   // BODACC injoignable : la tournée de demain s'en chargera
            yield return Appliquer(loc, procs, r => change = r, refus => Debug.LogWarning($"[PappersSync] {loc.Name} : {refus}"));
        }
        else yield return Interroger(loc, r => change = r, _ => { });
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

    // ── Étape 16b (décisions du 28/10) : sur le cloud, Pappers par le serveur et le BODACC ─────

    // Refus qui vaudront pour tous les locataires : inutile d'interroger les suivants.
    static readonly HashSet<string> Bloquants = new HashSet<string> { "config", "cle", "jetons", "abonnement", "acces" };

    /// La tournée du cloud : le BODACC d'abord (gratuit, 40 sociétés par appel), puis la fiche Pappers de chaque
    /// locataire (relue par le serveur une fois par mois au plus). BODACC injoignable : rien n'est écrit
    /// aujourd'hui, la ligne « Procédures » serait fausse ; la tournée de demain recommencera.
    static IEnumerator ToutCloud(List<BatimentPrefab> bps, System.Func<string, bool> libre)
    {
        var sirens = bps.Where(b => b != null && b.listLocataire != null).SelectMany(b => b.listLocataire)
                        .Where(l => l != null && PappersResume.SiretExploitable(l.siretNumber))
                        .Select(l => SocieteLocataire.Siren(l.siretNumber)).Where(s => s.Length == 9).Distinct().ToList();
        if (sirens.Count == 0) yield break;
        List<SocieteLocataire.Procedure> procs = null;
        yield return Bodacc(sirens, r => procs = r);
        if (procs == null) yield break;

        int majs = 0, echecs = 0;
        string arret = null;
        foreach (var bp in bps)
        {
            if (bp == null || bp.listLocataire == null) continue;
            bool touche = false;
            foreach (var loc in bp.listLocataire)
            {
                if (loc == null || !PappersResume.SiretExploitable(loc.siretNumber)) continue;
                if (libre != null && !libre(loc.id)) continue;
                bool change = false;
                string refus = null;
                yield return Appliquer(loc, procs, r => change = r, r => refus = r);
                if (change) { touche = true; majs++; }
                if (refus == null) continue;
                echecs++;
                Debug.LogWarning($"[PappersSync] {loc.Name} : {refus}");
                if (Bloquants.Contains(refus.Split(' ')[0])) { arret = refus; break; }
            }
            if (bp == null) yield break;
            if (touche && BatimentManager.Instance != null) BatimentManager.Instance.SaveBatiment(bp.getBatiment());
            if (arret != null) break;
        }
        Debug.Log($"[PappersSync] Pappers et BODACC : {majs} locataire(s) mis à jour, {echecs} en échec"
                  + (arret != null ? $", tournée arrêtée ({arret})." : "."));
        if (majs > 0) RafraichirFichesOuvertes();
    }

    /// Un locataire : sa fiche Pappers (par le serveur), le bloc du commentaire et les objectifs à créer.
    static IEnumerator Appliquer(Locataire loc, List<SocieteLocataire.Procedure> procsTous,
                                 System.Action<bool> onChange, System.Action<string> onRefus)
    {
        string siren = SocieteLocataire.Siren(loc.siretNumber);
        var t = FicheServeur(siren);
        yield return new WaitUntil(() => t.IsCompleted);
        var r = t.Result;
        if (r == null || !r.ok || r.fiche == null) { onRefus?.Invoke($"{r?.refus ?? "serveur"} {r?.erreur}".Trim()); yield break; }

        var procs = procsTous.Where(p => p.siren == siren).ToList();
        bool change = false;
        string nouveau = PappersResume.Fusionner(loc.commentaire, SocieteLocataire.Construire(r.fiche, procs, r.lu_le));
        if (nouveau != loc.commentaire) { loc.commentaire = nouveau; change = true; }

        if (loc.signalementsSociete == null) loc.signalementsSociete = new List<string>();
        foreach (var (cle, objectif) in SocieteLocataire.Signaler(loc.signalementsSociete, r.fiche, procs, System.DateTime.Today))
        {
            if (loc.objectifs == null) loc.objectifs = new ObjectiveList();
            if (loc.objectifs.items == null) loc.objectifs.items = new List<Objective>();
            loc.objectifs.items.Add(objectif);
            loc.signalementsSociete.Add(cle);
            change = true;
        }
        onChange?.Invoke(change);
    }

    /// Les procédures collectives des sociétés au BODACC ; null s'il n'a pas répondu.
    static IEnumerator Bodacc(List<string> sirens, System.Action<List<SocieteLocataire.Procedure>> fin)
    {
        var tout = new List<SocieteLocataire.Procedure>();
        for (int i = 0; i < sirens.Count; i += 40)
        {
            using var req = UnityWebRequest.Get(SocieteLocataire.UrlBodacc(sirens.Skip(i).Take(40)));
            req.timeout = 30;
            yield return req.SendWebRequest();
            var lu = req.result == UnityWebRequest.Result.Success ? SocieteLocataire.LireBodacc(req.downloadHandler.text) : null;
            if (lu == null)
            {
                Debug.LogWarning($"[PappersSync] BODACC injoignable ({req.responseCode} {req.error}) : réessai à la prochaine tournée.");
                fin(null);
                yield break;
            }
            tout.AddRange(lu);
        }
        fin(tout);
    }

    [System.Serializable] class DemandePappers { public string entreprise, siren; }
    [System.Serializable] class ReponsePappers { public bool ok; public string refus, erreur, lu_le; public SocieteLocataire.FicheSociete fiche; }

    static async Task<ReponsePappers> FicheServeur(string siren)
    {
        try
        {
            var r = await CloudClient.FonctionServeur(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "pappers",
                        JsonUtility.ToJson(new DemandePappers { entreprise = SessionCloud.Entreprise?.id, siren = siren }));
            ReponsePappers rep = null;
            try { rep = JsonUtility.FromJson<ReponsePappers>(r.Corps); } catch (System.Exception) { /* pas du JSON : r.Erreur dit pourquoi */ }
            return rep != null && (rep.ok || !string.IsNullOrEmpty(rep.refus))
                ? rep : new ReponsePappers { refus = "serveur", erreur = r.Ok ? "réponse inattendue" : r.Erreur };
        }
        catch (System.Exception e) { return new ReponsePappers { refus = "serveur", erreur = e.Message }; }
    }

    static void RafraichirFichesOuvertes()
    {
        foreach (var fiche in Resources.FindObjectsOfTypeAll<LocatairePrefab>())
        {
            // FindObjectsOfTypeAll ramène AUSSI les prefabs d'assets, qui ne sont
            // dans aucune scène et n'ont pas de bâtiment d'origine. Les toucher n'a
            // pas de sens — et modifier un asset depuis le jeu encore moins.
            if (fiche == null || !fiche.gameObject.scene.IsValid()) continue;
            // Fiche en cours de saisie : le commentaire tapé ne doit pas être remplacé sous les
            // doigts (la tournée du cloud, étape 10, passe pendant qu'on travaille).
            if (fiche.EnEdition) continue;
            var loc = fiche.GetLocataire();
            if (loc != null && fiche.Commentaire != null) fiche.Commentaire.ApplyValue(loc.commentaire);
            // Étape 16b : un objectif créé tout seul (« Déclarer la créance ») se voit sans rouvrir la fiche.
            if (loc != null && fiche.objectivesManager != null) fiche.objectivesManager.LoadObjectives(loc.objectifs);
        }
    }
}
