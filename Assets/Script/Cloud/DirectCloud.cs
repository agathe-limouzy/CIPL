using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// Étape 5 : en direct. Abonné aux changements des bâtiments, locataires, verrous et présences
/// de l'entreprise ouverte (TempsReel). Une fiche changée par un collègue est relue sur le
/// serveur et reconstruite (BatimentManager.Remplacer) ; si j'y travaille, elle attend que j'aie
/// fini (décisions du 13/10). Présence : signal toutes les 30 s, « En ligne » dans la barre.
public static class DirectCloud
{
    class Attente { public string Auteur; public bool Silencieux, Annonce, Note; public float Prochain; }

    static TempsReel _canal;
    static string _entreprise, _jetonEnvoye;
    static int _generation;   // change à chaque arrêt : une lecture d'avant est ignorée
    static readonly Dictionary<string, Attente> _aRelire = new Dictionary<string, Attente>();
    static readonly HashSet<string> _enLecture = new HashSet<string>();
    static bool _verrous, _presences;
    static bool _abonnementRefuse;   // Revue 17 (D2-22, lot D) : voir Traiter et Entretenir
    static GameObject _barre;
    static List<string> _enLigne = new List<string>();

    // ── Sans réseau (testé en EditMode) ────────────────────────────────────────

    /// Bâtiment à relire pour un changement reçu, et son auteur ; null = rien à faire (autre
    /// entreprise, mon propre enregistrement, ma suppression). "*" = message incomplet, tout relire.
    public static (string batimentId, string auteur)? Concerne(TempsReel.DonneesChange d, string entreprise, string maSession,
                                                                 Func<string, string> batimentDuLocataire, Func<string, bool> supprimeParMoi)
    {
        if (d == null || (d.table != "batiments" && d.table != "locataires")) return null;
        var r = d.record; var o = d.old_record;
        string ent = Val(r?.entreprise_id) ?? Val(o?.entreprise_id);
        if (ent != null && ent != entreprise) return null;               // les suppressions ne sont pas filtrées
        if (Val(r?.maj_session) == maSession) return null;               // l'écho de mon enregistrement
        string id = Val(r?.id) ?? Val(o?.id);
        if (d.type == "DELETE" && supprimeParMoi(id)) return null;
        string auteur = Val(r?.maj_par);
        if (id == null) return ("*", auteur);
        if (d.table == "batiments") return (id, auteur);
        return (Val(r?.batiment_id) ?? batimentDuLocataire(id) ?? "*", auteur);
    }

    static string Val(string s) => string.IsNullOrEmpty(s) ? null : s;

    /// Revue 17 (D2-22, lot D) : le serveur a refusé l'abonnement (réponse en erreur) : le canal reste ouvert mais ne
    /// recevra rien. Avant, ce n'était que journalisé : les fiches des collègues n'arrivaient plus qu'au rechargement.
    public static bool AbonnementRefuse(TempsReel.MessageEntrant m) =>
        m != null && m.@event == "phx_reply" && m.payload?.status == "error";

    /// Revue 17 (D2-22, lot D) : le canal est à relancer : jamais ouvert, coupé (connexion impossible, erreur de socket),
    /// fermé, arrêté, ou abonnement refusé. « connexion… » n'y est pas : TempsReel.Demarrer la borne à 10 s, puis « coupé ».
    /// Revue 17 (D9-11, D9-12, lot E ; décision du 16/11) : le canal temps réel est ouvert et abonné : les changements des
    /// collègues (documents, verrous) arrivent par lui ; les relectures de secours s'espacent (120 s et 60 s au lieu de 20 et 10).
    public static bool Connecte => _canal != null && !_abonnementRefuse && _canal.Etat == "connecté";

    public static bool ARelancer(string etat, bool abonnementRefuse) =>
        abonnementRefuse || etat == null || etat.StartsWith("coupé") || etat.StartsWith("fermé") || etat == "arrêté";

    // ── Marche / arrêt ─────────────────────────────────────────────────────────

    /// `telecharger` faux : dossier dans un Drive partagé, qui recopie les documents lui-même (étape 6b).
    public static async void Demarrer(string entrepriseId, bool telecharger)
    {
        Arreter();
        _entreprise = entrepriseId;
        DocumentsCloud.Demarrer(entrepriseId, SaveLocationService.GetSaveRoot(), telecharger);   // étape 6
        Pompe.Assurer();
        await Connecter();
        Signaler();
        _presences = _verrous = true;
    }

    public static void Arreter()
    {
        _generation++;
        DocumentsCloud.Arreter();
        _canal?.Arreter();
        _canal = null;
        _aRelire.Clear();
        _enLecture.Clear();
        if (_entreprise != null && SessionCloud.Active) Quitter(_entreprise);
        _entreprise = null;
        AfficherEnLigne(new List<string>());
    }

    static async Task Connecter()
    {
        int generation = _generation;
        TempsReel canal = null;
        try
        {
            string jeton = await SessionCloud.Jeton();
            if (generation != _generation) return;   // arrêté (ou autre entreprise) pendant l'attente du jeton
            string filtre = "entreprise_id=eq." + _entreprise;
            // Revue 17 (D2-22, lot D) : l'ancien canal est arrêté avant d'en ouvrir un autre : après un abonnement refusé
            // sa socket est encore ouverte, elle resterait à battre pour rien.
            _canal?.Arreter();
            _abonnementRefuse = false;
            _canal = canal = new TempsReel();
            await canal.Demarrer(SessionCloud.Url, SessionCloud.Cle,
                r => TempsReel.MessageJoinFiltre(new[] { "batiments", "locataires", "verrous", "presences", "documents", "reglages", "secrets" }, filtre, jeton, r));
            _jetonEnvoye = jeton;
        }
        // `_canal == canal` : un canal ouvert entre-temps par un autre appel n'est pas lâché sur l'échec de celui-ci.
        catch (Exception e) { if (_canal == canal) _canal = null; Debug.LogWarning("[Direct] Connexion impossible : " + e.Message); }
    }

    // ── Messages reçus ─────────────────────────────────────────────────────────

    static void Traiter(string json)
    {
        var m = TempsReel.Lire(json);
        if (m == null) return;
        if (AbonnementRefuse(m))
        {
            // Revue 17 (D2-22, lot D) : retenu ici (fil principal), pas dans l'état du canal : le fil de la socket le
            // remettrait à « connecté » au message suivant. Entretenir relance le canal au prochain tour (30 s au plus).
            _abonnementRefuse = true;
            Debug.LogWarning("[Direct] Abonnement refusé : " + m.payload.response?.reason);
            return;
        }
        if (m.@event != "postgres_changes" || m.payload?.data == null) return;

        var d = m.payload.data;
        if (d.table == "verrous") { _verrous = true; return; }
        if (d.table == "presences") { _presences = true; return; }
        if (d.table == "documents") { if (Val(d.record?.maj_session) != EcritureCloud.SessionId) DocumentsCloud.Bientot(); return; }   // étape 6a
        // Étape 9a : Réglages changés par un collègue (annoncés), ou un secret (en silence).
        if (d.table == "reglages") { if (Val(d.record?.maj_session) != EcritureCloud.SessionId) ReglagesCloud.ARelire(Val(d.record?.maj_par) ?? "un collègue"); return; }
        if (d.table == "secrets") { ReglagesCloud.ARelire(null); return; }
        var c = Concerne(d, _entreprise, EcritureCloud.SessionId, EcritureCloud.BatimentDuLocataire, EcritureCloud.SupprimeParMoi);
        if (c == null) return;
        if (c.Value.batimentId == "*") TousARelire(silencieux: false);
        else Marquer(c.Value.batimentId, c.Value.auteur, silencieux: false);
    }

    static void Marquer(string id, string auteur, bool silencieux)
    {
        if (!_aRelire.TryGetValue(id, out var a)) _aRelire[id] = a = new Attente { Silencieux = silencieux };
        if (!silencieux) a.Silencieux = false;
        if (auteur != null) a.Auteur = auteur;
    }

    /// Un enregistrement refusé (version dépassée, fiche supprimée) : au OK, la fiche se relit seule, pas toute
    /// l'entreprise (BH8, 02/11 : le rechargement complet était long et déroutant pour une seule fiche).
    public static void ARelire(string batimentId) { if (_entreprise != null && batimentId != null) Marquer(batimentId, null, silencieux: true); }

    /// Étape 8 : connexion revenue. Des changements ont pu être manqués : tout est relu en silence,
    /// sauf ce sur quoi je travaille (relu quand j'aurai fini).
    public static void ToutRelire()
    {
        if (_entreprise == null) return;
        TousARelire(silencieux: true);
        _verrous = _presences = true;
        DocumentsCloud.Bientot();
    }

    static void TousARelire(bool silencieux)
    {
        var bm = BatimentManager.Instance;
        if (bm != null) foreach (var b in bm.Batiments.ToList()) Marquer(b.id, null, silencieux);
    }

    // ── Relecture d'une fiche ──────────────────────────────────────────────────

    static BatimentPrefab Fiche(string id) =>
        BatimentManager.Instance?.BatimentPrefab.Find(p => p != null && p.getID() == id);

    static bool Accueil() => GeneralMenuPanel.Instance != null && GeneralMenuPanel.Instance.gameObject.activeInHierarchy;

    /// Le changement se voit : accueil, ou fiche de ce bâtiment à l'écran.
    static bool Affiche(string id)
    {
        if (Accueil()) return true;
        var f = Fiche(id);
        return f != null && f.gameObject.activeInHierarchy;
    }

    /// J'y travaille : fiche ou écran réservés, envoi en cours, ou fiche en saisie sans verrou
    /// (création d'un bâtiment ou d'un locataire).
    static bool Occupe(string id)
    {
        if (EcritureCloud.Occupe(id)) return true;
        var f = Fiche(id);
        return f != null && (f.EnEdition || f.dictionnairelocataire.Values.Any(l => l != null && l.EnEdition));
    }

    static void Relire(float maintenant)
    {
        foreach (var kv in _aRelire.ToList())
        {
            string id = kv.Key; var a = kv.Value;
            if (_enLecture.Contains(id) || maintenant < a.Prochain) continue;
            if (Occupe(id))
            {
                if (!a.Note) { a.Note = true; Debug.Log($"[Direct] « {Fiche(id)?.getName() ?? id} » changé par {a.Auteur ?? "?"} : en attente, j'y travaille"); }
                if (!a.Annonce && !a.Silencieux && Affiche(id))
                {
                    a.Annonce = true;
                    Info($"« {Fiche(id)?.getName() ?? "Un bâtiment"} » modifié par {a.Auteur ?? "un collègue"} : affiché quand vous aurez fini.");
                }
                continue;
            }
            LireUn(id, a);
        }
    }

    static async void LireUn(string id, Attente a)
    {
        int generation = _generation;
        _enLecture.Add(id);
        try
        {
            var (b, etat, erreur) = await EcritureCloud.ChargerUn(_entreprise, id);
            if (generation != _generation) return;
            if (erreur != null) { a.Prochain = Time.realtimeSinceStartup + 10f; Debug.LogWarning($"[Direct] Relecture de {id} : {erreur}"); return; }
            if (Occupe(id)) return;   // la personne s'y est mise pendant la lecture : on relira ensuite
            _aRelire.Remove(id);

            var bm = BatimentManager.Instance;
            if (bm == null) return;
            // Revue 17 (D9-04, lot E) : mêmes fiches, mêmes versions que l'état connu, fiche affichée ici : rien à reconstruire.
            if (b != null && bm.BatimentPrefab.Exists(p => p != null && p.getID() == id) && EcritureCloud.Inchange(id, etat))
            {
                Debug.Log($"[Direct] « {b.Name} » relu : inchangé, rien à reconstruire");
                return;
            }
            bool dire = !a.Silencieux && Affiche(id);
            string qui = a.Auteur ?? "un collègue";
            Debug.Log($"[Direct] « {b?.Name ?? id} » relu ({(b == null ? "supprimé" : "changé")} par {qui}{(a.Silencieux ? ", après reconnexion" : "")})");
            if (b == null)
            {
                string nom = bm.Retirer(id, montrerVoisin: !Accueil());
                EcritureCloud.Adopter(id, new Dictionary<string, EcritureCloud.Etat>(), null);
                if (nom != null && dire) Info($"« {nom} » supprimé par un collègue.");
            }
            else
            {
                bool existait = bm.Remplacer(b);
                EcritureCloud.Adopter(id, etat, b);
                if (dire) Info(existait ? $"« {b.Name} » mis à jour par {qui}." : $"« {b.Name} » ajouté par {qui}.");
            }
            if (Accueil()) GeneralMenuPanel.Instance.Refresh();
            _verrous = true;   // la fiche neuve n'a pas encore son bandeau
        }
        catch (Exception e) { Debug.LogException(e); a.Prochain = Time.realtimeSinceStartup + 10f; }
        finally { if (generation == _generation) _enLecture.Remove(id); }
    }

    static void Info(string message) => UndoToast.Instance?.ShowInfo(message);

    // ── Présence ───────────────────────────────────────────────────────────────

    [Serializable] class ParamsPresence { public string p_entreprise, p_session; }
    [Serializable] class LignePresence { public string session_id, nom, vu_le; }
    [Serializable] class LignesPresence { public LignePresence[] items; }

    static async void Signaler()
    {
        string e = _entreprise;
        if (e == null) return;
        try
        {
            var r = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "signaler_presence",
                new ParamsPresence { p_entreprise = e, p_session = EcritureCloud.SessionId });
            // Étape 9b : retiré de l'entreprise par un admin (le serveur refuse ma présence).
            if (!r.Ok && (r.Erreur ?? "").Contains("accès refusé") && e == _entreprise) { DemarrageCloud.Exclu(); return; }
            if (!r.Ok) Debug.LogWarning("[Direct] Présence : " + r.Erreur);
        }
        catch (Exception ex) { Debug.LogWarning("[Direct] Présence : " + ex.Message); }
    }

    static async void Quitter(string entreprise)
    {
        try
        {
            await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "quitter_presence",
                new ParamsPresence { p_entreprise = entreprise, p_session = EcritureCloud.SessionId });
        }
        catch (Exception) { /* la ligne expirera : 2 min sans signal */ }
    }

    static async void LirePresences()
    {
        string e = _entreprise;
        if (e == null) return;
        try
        {
            var r = await CloudClient.Table("GET", SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "presences",
                $"entreprise_id=eq.{e}&select=session_id,nom,vu_le");
            if (!r.Ok || e != _entreprise) return;
            var lignes = JsonUtility.FromJson<LignesPresence>("{\"items\":" + r.Corps + "}")?.items ?? new LignePresence[0];
            AfficherEnLigne(RegleVerrou.EnLigne(
                lignes.Select(l => (l.session_id, l.nom, CloudClient.Date(l.vu_le) ?? DateTime.MinValue)), EcritureCloud.SessionId, DateTime.Now));
        }
        catch (Exception ex) { Debug.LogWarning("[Direct] Présence : " + ex.Message); }
    }

    /// « En ligne : (MD) (PL) » dans la barre d'onglets (maquette 2), entre « + » et le bouton
    /// Quitter ; un clic donne les noms. La barre ne dimensionne ni ne centre ses enfants (retour
    /// du 14/10 : le bloc dépassait dessous) : on le place nous-mêmes, centré dans l'espace libre.
    static void AfficherEnLigne(List<string> noms)
    {
        _enLigne = noms;
        // Revue 17 (B-01 / B-11, lot F) : dans la zone d'état de la barre d'onglets (EcransCloud.ZoneEtat), qui lui fait sa place ;
        // avant, le bloc était posé à la main entre « + » et la croix « Quitter », dans une barre aux largeurs fixes.
        var zone = EcransCloud.ZoneEtat();
        if (zone == null) return;
        if (_barre == null)
        {
            var h = UIFactory.HBox(zone, 6, false, "EnLigne");
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            UIFactory.LE(h.gameObject, minH: 36, prefH: 36, flexW: 0);
            h.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);   // zone cliquable
            ObjectifsTableau.Cliquable(h.gameObject, () => Info("En ligne : " + string.Join(", ", _enLigne)));
            _barre = h.gameObject;
        }
        _barre.transform.SetAsLastSibling();   // après le nom de l'entreprise
        foreach (Transform c in _barre.transform) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }   // hors mesure dès maintenant
        _barre.SetActive(noms.Count > 0);
        if (noms.Count == 0) return;
        if (noms.Count <= 4)   // trop de monde : les initiales seules
            UIFactory.Text(_barre.transform, "En ligne :", UITheme.Role.Aide, UITheme.PrimaireClair).enableWordWrapping = false;
        foreach (var n in noms) ObjectifsTableau.Pastille(_barre.transform, RegleVerrou.Initiales(n), UITheme.PrimaireClair, UITheme.Primaire);
    }

    // ── Pompe : messages sur le fil principal, relectures, présence, entretien du canal ──

    class Pompe : MonoBehaviour
    {
        static Pompe _instance;
        float _prochainTour, _prochainSignal;

        public static void Assurer()
        {
            if (_instance != null) return;
            var go = new GameObject("DirectCloud");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Pompe>();
        }

        void Update()
        {
            if (_entreprise == null) return;
            while (_canal != null && _canal.Recus.TryDequeue(out var json)) Traiter(json);

            float now = Time.realtimeSinceStartup;
            if (now < _prochainTour) return;
            _prochainTour = now + 0.5f;
            Relire(now);
            DocumentsCloud.Tic(now);
            ReglagesCloud.Tic();   // étape 9a
            if (_verrous) { _verrous = false; EcritureCloud.RafraichirBandeaux(); }
            if (_presences) { _presences = false; LirePresences(); }
            if (now >= _prochainSignal) { _prochainSignal = now + 30f; Signaler(); LirePresences(); Entretenir(); }
        }

        /// Canal coupé : on le rouvre et on relit tout, en silence (des changements ont pu être
        /// manqués). Jeton renouvelé : on le transmet, sinon le serveur cesse d'envoyer.
        static async void Entretenir()
        {
            try
            {
                // Revue 17 (D2-22, lot D) : un abonnement refusé relance aussi le canal (règle ARelancer).
                string etat = _abonnementRefuse ? "coupé : abonnement refusé" : _canal?.Etat ?? "arrêté";
                if (_canal == null || ARelancer(etat, _abonnementRefuse))
                {
                    Debug.Log($"[Direct] Canal {etat} : reconnexion");
                    await Connecter();
                    TousARelire(silencieux: true);
                    _verrous = _presences = true;
                    DocumentsCloud.Bientot();
                    return;
                }
                string jeton = await SessionCloud.Jeton();
                if (jeton != _jetonEnvoye) { await _canal.EnvoyerJeton(jeton); _jetonEnvoye = jeton; }
            }
            catch (Exception ex) { Debug.LogWarning("[Direct] Entretien du canal : " + ex.Message); }
        }

        void OnApplicationQuit() { if (_entreprise != null) QuitterAvantFermeture(_entreprise); _canal?.Arreter(); }
    }

    /// À la fermeture, l'application s'arrête avant qu'un envoi en tâche de fond n'arrive : la
    /// pastille restait 2 min chez les autres (retour du 14/10). On attend la réponse, 2 s au plus.
    static void QuitterAvantFermeture(string entreprise)
    {
        var s = SessionCloud.Session;
        if (s == null) return;
        try
        {
            using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) })
            using (var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post,
                       SessionCloud.Url + "/rest/v1/rpc/quitter_presence"))
            {
                req.Headers.Add("apikey", SessionCloud.Cle);
                req.Headers.Add("Authorization", "Bearer " + s.access_token);
                req.Content = new System.Net.Http.StringContent(
                    JsonUtility.ToJson(new ParamsPresence { p_entreprise = entreprise, p_session = EcritureCloud.SessionId }),
                    System.Text.Encoding.UTF8, "application/json");
                http.SendAsync(req).Wait(TimeSpan.FromSeconds(2));
            }
        }
        catch (Exception) { /* hors ligne : la ligne expirera après 2 min sans signal */ }
    }
}
