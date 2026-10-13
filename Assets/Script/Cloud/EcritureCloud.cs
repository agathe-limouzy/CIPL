using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Étape 4a : écrire dans le cloud. SaveBatiment ne change pas pour les ~20 écrans qui
/// l'appellent : on compare le bâtiment au dernier état connu du serveur et on n'envoie
/// que les fiches changées (bâtiment et/ou locataires), une par une, dans une file d'attente.
/// Le SERVEUR refuse si la version a changé ou si la fiche est verrouillée par une autre
/// session : le verrou tient donc aussi pour les actions en un clic.
public static class EcritureCloud
{
    public class Etat { public string Json; public int Version; public string BatimentId; public int Ordre; }
    public enum TypeOp { Batiment, Locataire, SupprimerLocataire }
    public class Op { public TypeOp Type; public string Id, BatimentId, Json; public int Version, Ordre; }
    [Serializable] public class Resultat { public bool ok; public int version; public string refus, nom, motif, pris_le; }

    /// Une session = cette fenêtre CIPL (deux PC avec le même compte = deux sessions).
    public static readonly string SessionId = Guid.NewGuid().ToString("N");
    static readonly Dictionary<string, Etat> _cache = new Dictionary<string, Etat>();

    // ── Sans réseau (testé en EditMode) ────────────────────────────────────────

    public static string Cle(bool batiment, string id) => (batiment ? "b:" : "l:") + id;

    /// Retient l'état du serveur d'un bâtiment et de ses locataires (JSON de CIPL + versions).
    public static void Memoriser(IDictionary<string, Etat> cache, Batiment b, int versionBat, IDictionary<string, int> versionsLoc)
    {
        DonneesCloud.Decouper(b, out var jb, out var jl);
        cache[Cle(true, b.id)] = new Etat { Json = jb, Version = versionBat };
        var locs = (b.locataireDuBatiment ?? new List<Locataire>()).Where(l => l != null).ToList();
        for (int i = 0; i < locs.Count; i++)
            cache[Cle(false, locs[i].id)] = new Etat
            {
                Json = jl[i], BatimentId = b.id, Ordre = i,
                Version = versionsLoc != null && versionsLoc.TryGetValue(locs[i].id, out int v) ? v : 0,
            };
    }

    /// Ce qui a changé depuis le dernier état connu : bâtiment d'abord (les locataires en dépendent).
    public static List<Op> Differences(Batiment b, IDictionary<string, Etat> cache)
    {
        var ops = new List<Op>();
        DonneesCloud.Decouper(b, out var jb, out var jl);
        cache.TryGetValue(Cle(true, b.id), out var eb);
        if (eb == null || eb.Json != jb)
            ops.Add(new Op { Type = TypeOp.Batiment, Id = b.id, Json = jb, Version = eb?.Version ?? 0 });

        var locs = (b.locataireDuBatiment ?? new List<Locataire>()).Where(l => l != null).ToList();
        for (int i = 0; i < locs.Count; i++)
        {
            cache.TryGetValue(Cle(false, locs[i].id), out var el);
            if (el == null || el.Json != jl[i] || el.Ordre != i || el.BatimentId != b.id)
                ops.Add(new Op { Type = TypeOp.Locataire, Id = locs[i].id, BatimentId = b.id, Json = jl[i], Ordre = i, Version = el?.Version ?? 0 });
        }

        var presents = new HashSet<string>(locs.Select(l => l.id));
        foreach (var kv in cache.Where(kv => kv.Key.StartsWith("l:") && kv.Value.BatimentId == b.id).ToList())
            if (!presents.Contains(kv.Key.Substring(2)))
                ops.Add(new Op { Type = TypeOp.SupprimerLocataire, Id = kv.Key.Substring(2), BatimentId = b.id });
        return ops;
    }

    /// Paramètres de enregistrer_batiment / enregistrer_locataire ; le JSON de CIPL part en objet.
    public static string CorpsEnregistrer(string entreprise, Op op, string session)
    {
        var sb = new StringBuilder("{\"p_entreprise\":").Append(Q(entreprise)).Append(",\"p_id\":").Append(Q(op.Id));
        if (op.Type == TypeOp.Locataire) sb.Append(",\"p_batiment\":").Append(Q(op.BatimentId)).Append(",\"p_ordre\":").Append(op.Ordre);
        return sb.Append(",\"p_version\":").Append(op.Version).Append(",\"p_data\":").Append(op.Json)
                 .Append(",\"p_session\":").Append(Q(session)).Append('}').ToString();
    }

    public static string MotifRefus(Resultat r, string nomFiche) => r.refus == "verrou"
        ? $"« {nomFiche} » : {RegleVerrou.Libelle(r.nom, r.motif, CloudClient.Date(r.pris_le) ?? DateTime.Now)}. Vos modifications ne sont pas enregistrées."
        : $"« {nomFiche} » a été modifié entre-temps par {(string.IsNullOrEmpty(r.nom) ? "un collègue" : r.nom)} : " +
          "vos modifications ne sont pas enregistrées, la fiche est rechargée.";

    static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    [Serializable] class LigneB { public string id; public int version; public Batiment data; }
    [Serializable] class LigneL { public string id, batiment_id; public int ordre, version; public Locataire data; }
    [Serializable] class LignesB { public LigneB[] items; }
    [Serializable] class LignesL { public LigneL[] items; }

    /// Bâtiments recomposés à partir des réponses du serveur ; le cache retient leur état.
    public static List<Batiment> Lire(string jsonBats, string jsonLocs, IDictionary<string, Etat> cache)
    {
        cache.Clear();
        var bats = JsonUtility.FromJson<LignesB>("{\"items\":" + jsonBats + "}")?.items ?? new LigneB[0];
        var locs = JsonUtility.FromJson<LignesL>("{\"items\":" + jsonLocs + "}")?.items ?? new LigneL[0];
        var res = new List<Batiment>();
        foreach (var lb in bats)
        {
            if (lb?.data == null) continue;
            var siens = locs.Where(l => l?.data != null && l.batiment_id == lb.id).OrderBy(l => l.ordre).ToList();
            lb.data.locataireDuBatiment = siens.Select(l => l.data).ToList();
            Memoriser(cache, lb.data, lb.version, siens.GroupBy(l => l.data.id).ToDictionary(g => g.Key, g => g.First().version));
            res.Add(lb.data);
        }
        return res;
    }

    // ── Réseau ─────────────────────────────────────────────────────────────────

    static string Url => SessionCloud.Url;
    static string CleApi => SessionCloud.Cle;

    public static async Task<(List<Batiment> batiments, string erreur)> Charger(string entrepriseId)
    {
        string jeton = await SessionCloud.Jeton();
        var rb = await CloudClient.Table("GET", Url, CleApi, jeton, "batiments", $"entreprise_id=eq.{entrepriseId}&select=id,version,data");
        if (!rb.Ok) return (null, rb.Erreur);
        var rl = await CloudClient.Table("GET", Url, CleApi, jeton, "locataires",
                                         $"entreprise_id=eq.{entrepriseId}&select=id,batiment_id,ordre,version,data&order=ordre.asc");
        if (!rl.Ok) return (null, rl.Erreur);
        try { return (Lire(rb.Corps, rl.Corps, _cache), null); }
        catch (Exception e) { return (null, "Données illisibles : " + e.Message); }
    }

    static Task _file = Task.CompletedTask;   // un enregistrement à la fois, dans l'ordre

    public static void Enregistrer(Batiment b) { var avant = _file; _file = Envoyer(avant, b); }

    static async Task Envoyer(Task avant, Batiment b)
    {
        try { await avant; } catch (Exception) { /* l'échec précédent a déjà été signalé */ }
        var e = SessionCloud.Entreprise;
        if (e == null || b == null) return;
        try
        {
            foreach (var op in Differences(b, _cache))
            {
                string jeton = await SessionCloud.Jeton();
                var r = op.Type == TypeOp.SupprimerLocataire
                    ? await CloudClient.FonctionJson(Url, CleApi, jeton, "supprimer_fiche",
                        $"{{\"p_entreprise\":{Q(e.id)},\"p_type\":\"locataire\",\"p_id\":{Q(op.Id)},\"p_session\":{Q(SessionId)}}}")
                    : await CloudClient.FonctionJson(Url, CleApi, jeton,
                        op.Type == TypeOp.Batiment ? "enregistrer_batiment" : "enregistrer_locataire", CorpsEnregistrer(e.id, op, SessionId));

                // Réseau en panne : le cache n'est pas avancé, l'envoi sera retenté au prochain enregistrement.
                if (!r.Ok) { ConfirmDialog.Erreur($"Enregistrement de « {b.Name} » impossible : {r.Erreur}"); return; }

                var res = JsonUtility.FromJson<Resultat>(r.Corps);
                if (res == null || !res.ok)
                {
                    string nom = op.Type == TypeOp.Batiment ? b.Name
                        : b.locataireDuBatiment?.FirstOrDefault(l => l?.id == op.Id)?.Name ?? b.Name;
                    ConfirmDialog.Erreur(MotifRefus(res ?? new Resultat { refus = "version" }, nom));
                    DemarrageCloud.Recharger();
                    return;
                }
                if (op.Type == TypeOp.SupprimerLocataire) _cache.Remove(Cle(false, op.Id));
                else _cache[Cle(op.Type == TypeOp.Batiment, op.Id)] =
                    new Etat { Json = op.Json, Version = res.version, BatimentId = op.BatimentId, Ordre = op.Ordre };
            }
        }
        catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Enregistrement impossible : " + ex.Message); }
    }

    public static void SupprimerBatiment(string id, string nom) { var avant = _file; _file = Supprimer(avant, id, nom); }

    static async Task Supprimer(Task avant, string id, string nom)
    {
        try { await avant; } catch (Exception) { }
        var e = SessionCloud.Entreprise;
        if (e == null) return;
        var r = await CloudClient.FonctionJson(Url, CleApi, await SessionCloud.Jeton(), "supprimer_fiche",
            $"{{\"p_entreprise\":{Q(e.id)},\"p_type\":\"batiment\",\"p_id\":{Q(id)},\"p_session\":{Q(SessionId)}}}");
        var res = r.Ok ? JsonUtility.FromJson<Resultat>(r.Corps) : null;
        if (res == null || !res.ok)
        {
            ConfirmDialog.Erreur(res != null ? MotifRefus(res, nom) : $"Suppression de « {nom} » impossible : {r.Erreur}");
            DemarrageCloud.Recharger();
            return;
        }
        _cache.Remove(Cle(true, id));
        foreach (var k in _cache.Where(kv => kv.Value.BatimentId == id).Select(kv => kv.Key).ToList()) _cache.Remove(k);
    }

    // ── Verrous ────────────────────────────────────────────────────────────────

    class Tenu { public PrefabBatLoc Fiche; public string Type, Id; public float ProchainSignal; }
    static readonly List<Tenu> _tenus = new List<Tenu>();
    [Serializable] class ParamsVerrou { public string p_entreprise, p_type, p_id, p_session, p_motif; }
    [Serializable] class ParamsLiberer { public string p_entreprise, p_type, p_id, p_session; }

    /// « Modifier » : demande le verrou de la fiche, et ne passe en saisie que s'il est obtenu.
    public static async void ModifierSiLibre(PrefabBatLoc fiche, string type, Action modifier)
    {
        var e = SessionCloud.Entreprise;
        string id = fiche != null ? fiche.getID() : null;
        if (!SessionCloud.Active || e == null || string.IsNullOrEmpty(id)) { modifier(); return; }
        try
        {
            var r = await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "prendre_verrou",
                new ParamsVerrou { p_entreprise = e.id, p_type = type, p_id = id, p_session = SessionId, p_motif = "modification" });
            if (!r.Ok) { ConfirmDialog.Erreur("Impossible de réserver la fiche : " + r.Erreur); return; }
            var v = CloudClient.LireVerrou(r.Corps);
            if (v == null || !v.obtenu)
            {
                ConfirmDialog.Erreur($"« {fiche.getName()} » : {RegleVerrou.Libelle(v?.nom, v?.motif, CloudClient.Date(v?.pris_le) ?? DateTime.Now)}.");
                return;
            }
            if (!_tenus.Any(t => t.Type == type && t.Id == id))
                _tenus.Add(new Tenu { Fiche = fiche, Type = type, Id = id, ProchainSignal = Time.realtimeSinceStartup + 30 });
            Gardien.Assurer();
            modifier();
        }
        catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Impossible de réserver la fiche : " + ex.Message); }
    }

    static async void Liberer(Tenu t)
    {
        var e = SessionCloud.Entreprise;
        if (e == null || !SessionCloud.Active) return;
        try
        {
            await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "liberer_verrou",
                new ParamsLiberer { p_entreprise = e.id, p_type = t.Type, p_id = t.Id, p_session = SessionId });
        }
        catch (Exception ex) { Debug.LogWarning("[Verrou] Libération : " + ex.Message); }   // expirera seul après 2 min
    }

    static async void Renouveler(Tenu t)
    {
        var e = SessionCloud.Entreprise;
        if (e == null || !SessionCloud.Active) return;
        try
        {
            await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "prendre_verrou",
                new ParamsVerrou { p_entreprise = e.id, p_type = t.Type, p_id = t.Id, p_session = SessionId, p_motif = "modification" });
        }
        catch (Exception ex) { Debug.LogWarning("[Verrou] Signal de vie : " + ex.Message); }
    }

    /// Lance le gardien (bandeaux « En cours de modification par… ») dès l'ouverture d'une entreprise.
    public static void DemarrerGardien() => Gardien.Assurer();

    /// Libère tout (déconnexion, changement d'entreprise).
    public static void LibererTout()
    {
        foreach (var t in _tenus.ToList()) Liberer(t);
        _tenus.Clear();
    }

    // ── Nom affiché ────────────────────────────────────────────────────────────

    [Serializable] class Vide { }
    [Serializable] class ParamNom { public string p_nom; }

    /// Demande « Votre nom » à la première ouverture d'une entreprise (sinon on affiche l'e-mail).
    public static async Task VerifierNom(Canvas canvas)
    {
        var r = await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "mon_nom_saisi", new Vide());
        if (!r.Ok) return;
        string nom = r.Corps.Trim().Trim('"');
        if (!string.IsNullOrEmpty(nom) && nom != "null") return;
        string saisi = await EcransCloud.DemanderNom(canvas);
        if (!string.IsNullOrWhiteSpace(saisi))
            await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "definir_mon_nom", new ParamNom { p_nom = saisi.Trim() });
    }

    // ── Gardien : signal de vie, libération en sortie de saisie, bandeaux ──────

    [Serializable] public class LigneVerrou { public string cible_type, cible_id, session_id, nom, motif, pris_le, expire_le; }
    [Serializable] class LignesVerrous { public LigneVerrou[] items; }

    class Gardien : MonoBehaviour
    {
        static Gardien _instance;
        float _prochainTour, _prochainesBandes;

        public static void Assurer()
        {
            if (_instance != null) return;
            var go = new GameObject("GardienVerrous");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Gardien>();
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now >= _prochainTour)
            {
                _prochainTour = now + 1f;
                foreach (var t in _tenus.ToList())
                {
                    if (t.Fiche == null || !t.Fiche.EnEdition) { Liberer(t); _tenus.Remove(t); }
                    else if (now >= t.ProchainSignal) { t.ProchainSignal = now + 30f; Renouveler(t); }
                }
            }
            if (now >= _prochainesBandes && SessionCloud.Active && SessionCloud.Entreprise != null)
            {
                _prochainesBandes = now + 10f;
                MettreAJourBandeaux();
            }
        }

        void OnApplicationQuit() => LibererTout();   // au mieux ; sinon les verrous expirent après 2 min

        static async void MettreAJourBandeaux()
        {
            try
            {
                var e = SessionCloud.Entreprise;
                var r = await CloudClient.Table("GET", Url, CleApi, await SessionCloud.Jeton(), "verrous",
                    $"entreprise_id=eq.{e.id}&select=cible_type,cible_id,session_id,nom,motif,pris_le,expire_le");
                if (!r.Ok) return;
                var verrous = JsonUtility.FromJson<LignesVerrous>("{\"items\":" + r.Corps + "}")?.items ?? new LigneVerrou[0];
                var autres = verrous.Where(v => v.session_id != SessionId &&
                                                (CloudClient.Date(v.expire_le) ?? DateTime.MinValue) >= DateTime.Now).ToList();
                foreach (var f in FindObjectsOfType<BatimentPrefab>(true)) Bandeau(f, autres.FirstOrDefault(v => v.cible_type == "batiment" && v.cible_id == f.getID()));
                foreach (var f in FindObjectsOfType<LocatairePrefab>(true)) Bandeau(f, autres.FirstOrDefault(v => v.cible_type == "locataire" && v.cible_id == f.getID()));
            }
            catch (Exception ex) { Debug.LogWarning("[Verrou] Bandeaux : " + ex.Message); }
        }

        /// Bandeau ambre posé en haut de la fiche, hors mise en page (n'en décale rien).
        static void Bandeau(PrefabBatLoc fiche, LigneVerrou v)
        {
            if (fiche == null) return;
            var existant = fiche.transform.Find("BandeauVerrou");
            if (v == null) { if (existant != null) Destroy(existant.gameObject); return; }

            if (existant == null)
            {
                var fond = UIFactory.Panel("BandeauVerrou", fiche.transform, new Color32(0xFA, 0xEE, 0xDA, 0xF5), false);
                fond.raycastTarget = false;
                fond.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var rt = (RectTransform)fond.transform;
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 1);
                rt.sizeDelta = new Vector2(0, 30); rt.anchoredPosition = Vector2.zero;
                var t = UIFactory.Text(fond.transform, "", UITheme.Role.Aide, new Color32(0x63, 0x38, 0x06, 0xFF), true, TextAlignmentOptions.Center);
                UIFactory.Stretch((RectTransform)t.transform, 12, 0, 12, 0);
                existant = fond.transform;
            }
            existant.SetAsLastSibling();
            existant.GetComponentInChildren<TMP_Text>(true).text =
                RegleVerrou.Libelle(v.nom, v.motif, CloudClient.Date(v.pris_le) ?? DateTime.Now) + " — lecture seule";
        }
    }
}
