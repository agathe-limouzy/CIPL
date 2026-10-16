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

    /// Après l'installation des fiches : les mises à niveau faites en mémoire au chargement
    /// (Reconstituer, Migrer, Actualiser) ne sont pas des saisies. Sans ce recalage, le premier
    /// enregistrement d'un locataire renverrait aussi son bâtiment, refusé si un collègue le tient.
    public static void Recaler(IDictionary<string, Etat> cache, IEnumerable<Batiment> bats)
    {
        foreach (var b in bats.Where(b => b != null))
        {
            int vb = cache.TryGetValue(Cle(true, b.id), out var eb) ? eb.Version : 0;
            var vl = cache.Where(kv => kv.Key.StartsWith("l:") && kv.Value.BatimentId == b.id)
                          .ToDictionary(kv => kv.Key.Substring(2), kv => kv.Value.Version);
            Memoriser(cache, b, vb, vl);
        }
    }

    public static void Recaler(IEnumerable<Batiment> bats) => Recaler(_cache, bats);

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

    // ── Étape 5 : fiches relues pendant qu'un écran était ouvert dessus ─────────

    /// Objets Batiment remplacés par une relecture (changement d'un collègue, rechargement) :
    /// un écran resté ouvert sur l'ancien ne doit plus rien écrire, sinon il renverrait des
    /// locataires périmés avec la version à jour et effacerait le travail du collègue.
    static readonly HashSet<Batiment> _perimes = new HashSet<Batiment>();
    public static void Perimer(Batiment b) { if (b != null) _perimes.Add(b); }
    public static bool Perime(Batiment b) => b != null && _perimes.Contains(b);

    /// Fiches que j'ai supprimées : leur écho (sans auteur) ne doit pas passer pour un collègue.
    static readonly HashSet<string> _supprimesParMoi = new HashSet<string>();
    public static bool SupprimeParMoi(string id) => id != null && _supprimesParMoi.Contains(id);

    public static string BatimentDuLocataire(string locataireId) =>
        locataireId != null && _cache.TryGetValue(Cle(false, locataireId), out var e) ? e.BatimentId : null;

    /// Je travaille dans ce bâtiment (fiche ou écran réservés, envoi en cours) : sa relecture attend.
    public static bool Occupe(string batimentId) =>
        !_file.IsCompleted
        || _tenus.Any(t => (t.Type == "batiment" && t.Id == batimentId) || (t.Type == "locataire" && BatimentDuLocataire(t.Id) == batimentId));

    /// Un bâtiment et ses locataires, avec leurs versions (null = supprimé). Le cache n'est
    /// pas touché : voir Adopter, une fois la fiche reconstruite.
    public static async Task<(Batiment bat, Dictionary<string, Etat> etat, string erreur)> ChargerUn(string entrepriseId, string batimentId)
    {
        string jeton = await SessionCloud.Jeton();
        string id = Uri.EscapeDataString(batimentId);
        var rb = await CloudClient.Table("GET", Url, CleApi, jeton, "batiments", $"entreprise_id=eq.{entrepriseId}&id=eq.{id}&select=id,version,data");
        if (!rb.Ok) return (null, null, rb.Erreur);
        var rl = await CloudClient.Table("GET", Url, CleApi, jeton, "locataires",
                                         $"entreprise_id=eq.{entrepriseId}&batiment_id=eq.{id}&select=id,batiment_id,ordre,version,data&order=ordre.asc");
        if (!rl.Ok) return (null, null, rl.Erreur);
        var etat = new Dictionary<string, Etat>();
        try { return (Lire(rb.Corps, rl.Corps, etat).FirstOrDefault(), etat, null); }
        catch (Exception e) { return (null, null, "Données illisibles : " + e.Message); }
    }

    /// Le cache prend l'état relu d'un bâtiment (ses anciens locataires en sortent), puis se
    /// recale sur la fiche reconstruite (mises à niveau du chargement).
    public static void Adopter(string batimentId, IDictionary<string, Etat> etat, Batiment b) =>
        Adopter(_cache, batimentId, etat, b);

    public static void Adopter(IDictionary<string, Etat> cache, string batimentId, IDictionary<string, Etat> etat, Batiment b)
    {
        foreach (var k in cache.Where(kv => kv.Key == Cle(true, batimentId) || kv.Value.BatimentId == batimentId).Select(kv => kv.Key).ToList())
            cache.Remove(k);
        foreach (var kv in etat) cache[kv.Key] = kv.Value;
        if (b != null) Recaler(cache, new[] { b });
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
                if (op.Type == TypeOp.SupprimerLocataire) { _cache.Remove(Cle(false, op.Id)); _supprimesParMoi.Add(op.Id); }
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
        _supprimesParMoi.Add(id);
        _cache.Remove(Cle(true, id));
        foreach (var k in _cache.Where(kv => kv.Value.BatimentId == id).Select(kv => kv.Key).ToList()) _cache.Remove(k);
    }

    // ── Verrous ────────────────────────────────────────────────────────────────

    /// Une réservation : la fiche reste prise tant que `Actif` est vrai (fiche en saisie, écran
    /// ouvert). Plusieurs réservations peuvent tenir la même fiche (Modifier puis Facturation) :
    /// elle n'est libérée qu'à la fin de la dernière.
    class Tenu { public string Type, Id, Nom, Motif; public Func<bool> Actif; public Action Fermer; public float ProchainSignal; }
    static readonly List<Tenu> _tenus = new List<Tenu>();
    [Serializable] class ParamsVerrou { public string p_entreprise, p_type, p_id, p_session, p_motif; }
    [Serializable] class ParamsLiberer { public string p_entreprise, p_type, p_id, p_session; }
    [Serializable] class ParamsForcer { public string p_entreprise, p_type, p_id; }

    static bool Tient(string type, string id) => _tenus.Any(t => t.Type == type && t.Id == id);

    /// Fiches d'un écran ouvert depuis un locataire : le locataire, précédé de son bâtiment
    /// quand l'écran écrit aussi les charges (régularisation, refacturation ; décision du 13/10).
    public static (string type, string id, string nom)[] Cibles(LocatairePrefab fiche, bool avecBatiment)
    {
        var loc = ("locataire", fiche.getID(), fiche.getName());
        var bp = fiche.batimentPrefabOrigin;
        return avecBatiment && bp != null ? new[] { ("batiment", bp.getID(), bp.getName()), loc } : new[] { loc };
    }

    /// « Modifier » : demande le verrou de la fiche, et ne passe en saisie que s'il est obtenu.
    public static void ModifierSiLibre(PrefabBatLoc fiche, string type, Action modifier) =>
        Reserver(new[] { (type, fiche != null ? fiche.getID() : null, fiche != null ? fiche.getName() : null) },
                 "modification", modifier, () => fiche != null && fiche.EnEdition);

    /// Réserve les fiches, puis ouvre l'écran ; refus au centre, avec le nom, si l'une est prise
    /// (rien n'est gardé à moitié). Une fiche que je tiens déjà ne coûte pas d'aller-retour :
    /// l'écran s'ouvre tout de suite (Révision → Paliers). `fermer` ferme l'écran sans enregistrer
    /// (« Toujours là ? » sans réponse, libération forcée, rechargement). Hors cloud : ouvre.
    public static async void Reserver((string type, string id, string nom)[] cibles, string motif,
                                      Action ouvrir, Func<bool> actif, Action fermer = null)
    {
        var e = SessionCloud.Entreprise;
        var fiches = cibles.Where(c => !string.IsNullOrEmpty(c.id)).ToList();
        if (!SessionCloud.Active || e == null || fiches.Count == 0) { ouvrir(); return; }
        var prises = new List<(string type, string id, string nom)>();
        try
        {
            foreach (var c in fiches.Where(c => !Tient(c.type, c.id)).ToList())
            {
                var r = await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "prendre_verrou",
                    new ParamsVerrou { p_entreprise = e.id, p_type = c.type, p_id = c.id, p_session = SessionId, p_motif = motif });
                var v = r.Ok ? CloudClient.LireVerrou(r.Corps) : null;
                if (v == null || !v.obtenu)
                {
                    foreach (var p in prises) Liberer(p.type, p.id);
                    ConfirmDialog.Erreur(v == null ? "Impossible de réserver la fiche : " + r.Erreur
                        : $"« {c.nom} » : {RegleVerrou.Libelle(v.nom, v.motif, CloudClient.Date(v.pris_le) ?? DateTime.Now)}.");
                    return;
                }
                prises.Add(c);
            }
            float signal = Time.realtimeSinceStartup + 30f;
            foreach (var c in fiches)
                _tenus.Add(new Tenu { Type = c.type, Id = c.id, Nom = c.nom, Motif = motif, Actif = actif, Fermer = fermer, ProchainSignal = signal });
            Gardien.Assurer();
            ouvrir();
        }
        catch (Exception ex)
        {
            foreach (var p in prises) if (!Tient(p.type, p.id)) Liberer(p.type, p.id);
            Debug.LogException(ex);
            ConfirmDialog.Erreur("Impossible de réserver la fiche : " + ex.Message);
        }
    }

    static async void Liberer(string type, string id)
    {
        var e = SessionCloud.Entreprise;
        if (e == null || !SessionCloud.Active) return;
        try
        {
            await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "liberer_verrou",
                new ParamsLiberer { p_entreprise = e.id, p_type = type, p_id = id, p_session = SessionId });
        }
        catch (Exception ex) { Debug.LogWarning("[Verrou] Libération : " + ex.Message); }   // expirera seul après 2 min
    }

    /// Signal de vie. La fiche n'est plus à moi (libération forcée par un admin, ou reprise par
    /// un collègue après 2 min sans signal) : mes saisies sont annulées.
    static async void Renouveler(Tenu t)
    {
        var e = SessionCloud.Entreprise;
        if (e == null || !SessionCloud.Active) return;
        try
        {
            var r = await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "renouveler_verrou",
                new ParamsVerrou { p_entreprise = e.id, p_type = t.Type, p_id = t.Id, p_session = SessionId, p_motif = t.Motif });
            if (!r.Ok) return;   // réseau : on réessaie au prochain signal ; le serveur garde la fiche 2 min
            var v = CloudClient.LireVerrou(r.Corps);
            if (v == null || v.obtenu || !_tenus.Contains(t)) return;
            Abandonner(v.motif == RegleVerrou.MotifForce
                ? $"{v.nom} (admin) a libéré « {t.Nom} » : vos saisies non enregistrées sont annulées."
                : $"« {t.Nom} » a été reprise par {v.nom} après une coupure : vos saisies non enregistrées sont annulées.");
        }
        catch (Exception ex) { Debug.LogWarning("[Verrou] Signal de vie : " + ex.Message); }
    }

    /// Ferme les écrans qui tiennent une fiche et relit toute l'entreprise (décision du 13/10) :
    /// les saisies non enregistrées disparaissent.
    static void Abandonner(string message, bool choisi = false)
    {
        FermerEcrans();
        if (choisi) UndoToast.Instance?.ShowInfo(message);   // « Annuler et libérer » : c'est voulu
        else ConfirmDialog.Erreur(message);
        DemarrageCloud.Recharger();
    }

    /// Lance le gardien (bandeaux « En cours de modification par… ») dès l'ouverture d'une entreprise.
    public static void DemarrerGardien() => Gardien.Assurer();

    /// Étape 5 : un verrou a changé (canal en direct) ; sans attendre le relevé des 10 s.
    public static void RafraichirBandeaux() => Gardien.MettreAJourBandeaux();

    /// Prénom de qui modifie cette fiche (dernier relevé des verrous des autres), sinon null :
    /// une ligne de locataire reconstruite le reprend aussitôt.
    static readonly Dictionary<string, string> _detenteurs = new Dictionary<string, string>();
    public static string Detenteur(bool batiment, string id) =>
        id != null && _detenteurs.TryGetValue(Cle(batiment, id), out var p) ? p : null;

    /// Ferme sans enregistrer les écrans qui tiennent une fiche, puis libère tout (rechargement,
    /// changement d'entreprise, déconnexion) : un écran resté ouvert garderait l'ancienne fiche.
    public static void FermerEcrans()
    {
        foreach (var t in _tenus.ToList())
            try { t.Fermer?.Invoke(); }
            catch (Exception ex) { Debug.LogWarning("[Verrou] Fermeture : " + ex.Message); }
        LibererTout();
    }

    public static void LibererTout()
    {
        foreach (var t in _tenus.GroupBy(t => (t.Type, t.Id)).Select(g => g.Key)) Liberer(t.Type, t.Id);
        _tenus.Clear();
    }

    /// Réglages › supprimer une liste de charges écrit TOUS les bâtiments : refusé si l'un est
    /// pris par quelqu'un, pour ne rien écrire à moitié. Hors cloud : exécute.
    public static async void SiAucunBatimentPris(IList<Batiment> bats, Action action)
    {
        if (!SessionCloud.Active || SessionCloud.Entreprise == null) { action(); return; }
        var autres = await Gardien.VerrousDesAutres();
        if (autres == null) { ConfirmDialog.Erreur("Impossible de vérifier les fiches en cours de modification : réessayez."); return; }
        var pris = autres.FirstOrDefault(v => v.cible_type == "batiment" && bats.Any(b => b.id == v.cible_id));
        if (pris == null) { action(); return; }
        string nom = bats.First(b => b.id == pris.cible_id).Name;
        ConfirmDialog.Erreur($"« {nom} » : {RegleVerrou.Libelle(pris.nom, pris.motif, CloudClient.Date(pris.pris_le) ?? DateTime.Now)}. " +
                             "Rien n'est modifié : réessayez quand la fiche sera libérée.");
    }

    // ── Étape 7 : envoi automatique des factures ───────────────────────────────

    public const string MotifEnvoi = "envoi des factures";
    [Serializable] class ParamsEnvoi { public string p_entreprise, p_locataire, p_facture, p_session; public int p_version; }
    [Serializable] public class ResultatEnvoi { public bool ok; public string refus, nom, motif, pris_le, statut; }

    /// Un enregistrement attend ou part : le verrou d'un envoi n'est rendu qu'après celui de son statut.
    public static bool EnregistrementEnCours => !_file.IsCompleted;

    /// Avant d'envoyer une facture : le serveur réserve son locataire et vérifie qu'elle attend
    /// toujours son envoi, sur la fiche que je connais (refus sinon : déjà envoyée, fiche prise ou
    /// modifiée). Le verrou tient tant que `actif` est vrai : le statut « Envoyé » ne peut donc pas
    /// être refusé ensuite. Hors cloud : accordé.
    public static async Task<ResultatEnvoi> ReserverEnvoi(Locataire loc, string factureKey, Func<bool> actif)
    {
        var e = SessionCloud.Entreprise;
        if (!SessionCloud.Active || e == null || loc == null) return new ResultatEnvoi { ok = true };
        try
        {
            var r = await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "reserver_envoi", new ParamsEnvoi
            {
                p_entreprise = e.id, p_locataire = loc.id, p_facture = factureKey, p_session = SessionId,
                p_version = _cache.TryGetValue(Cle(false, loc.id), out var etat) ? etat.Version : 0,
            });
            var res = r.Ok ? JsonUtility.FromJson<ResultatEnvoi>(r.Corps) : null;
            if (res == null) return new ResultatEnvoi { refus = "reseau", motif = r.Ok ? "réponse illisible" : r.Erreur };
            if (res.ok)
            {
                _tenus.Add(new Tenu { Type = "locataire", Id = loc.id, Nom = loc.Name, Motif = MotifEnvoi, Actif = actif,
                                      ProchainSignal = Time.realtimeSinceStartup + 30f });
                Gardien.Assurer();
            }
            return res;
        }
        catch (Exception ex) { Debug.LogException(ex); return new ResultatEnvoi { refus = "reseau", motif = ex.Message }; }
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
        float _prochainTour, _prochainesBandes, _derniereActivite;
        Vector3 _souris;
        GameObject _toujoursLa;
        TMP_Text _texteToujoursLa;

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
            // Activité = un clic, une touche, la molette ou la souris qui bouge, dans CIPL.
            // Pas pendant « Toujours là ? » : il faut y répondre.
            if (_toujoursLa == null && (Input.anyKeyDown || Input.mouseScrollDelta != Vector2.zero || Input.mousePosition != _souris))
                _derniereActivite = now;
            _souris = Input.mousePosition;

            if (now >= _prochainTour)
            {
                _prochainTour = now + 1f;
                foreach (var t in _tenus.ToList())
                {
                    bool actif;
                    try { actif = t.Actif(); } catch (Exception) { actif = false; }
                    if (!actif) { _tenus.Remove(t); if (!Tient(t.Type, t.Id)) Liberer(t.Type, t.Id); }
                    else if (now >= t.ProchainSignal) { t.ProchainSignal = now + 30f; Renouveler(t); }
                }
                SurveillerInactivite(now);
            }
            if (now >= _prochainesBandes && SessionCloud.Active && SessionCloud.Entreprise != null)
            {
                _prochainesBandes = now + 10f;
                MettreAJourBandeaux();
            }
        }

        void SurveillerInactivite(float now)
        {
            if (_tenus.Count == 0) { _derniereActivite = now; FermerToujoursLa(); return; }
            var inactif = TimeSpan.FromSeconds(now - _derniereActivite);
            var fiches = _tenus.Select(t => t.Nom).Distinct().ToList();
            switch (RegleVerrou.EtatInactivite(inactif))
            {
                case RegleVerrou.Presence.Avertir:
                    if (_toujoursLa == null)
                    {
                        var canvas = FindObjectOfType<Canvas>();
                        if (canvas == null) return;
                        (_toujoursLa, _texteToujoursLa) = EcransCloud.ToujoursLa(canvas,
                            continuer: () => _derniereActivite = Time.realtimeSinceStartup,
                            liberer: () => Abandonner("Saisies annulées, fiche libérée.", choisi: true));
                    }
                    var fin = DateTime.Now + (RegleVerrou.Inactivite + RegleVerrou.DelaiReponse - inactif);
                    _texteToujoursLa.text = EcransCloud.TexteToujoursLa(fiches, RegleVerrou.Restant(fin, DateTime.Now));
                    break;
                case RegleVerrou.Presence.Liberer:
                    FermerToujoursLa();
                    _derniereActivite = now;
                    Abandonner($"Sans réponse à « Toujours là ? », vos saisies non enregistrées sur {string.Join(", ", fiches)} " +
                               "sont annulées et la fiche est libérée.");
                    break;
            }
        }

        void FermerToujoursLa() { if (_toujoursLa != null) Destroy(_toujoursLa); _toujoursLa = null; }

        void OnApplicationQuit() => LibererTout();   // au mieux ; sinon les verrous expirent après 2 min

        /// Verrous valables des AUTRES sessions ; null si la lecture échoue.
        public static async Task<List<LigneVerrou>> VerrousDesAutres()
        {
            var e = SessionCloud.Entreprise;
            if (e == null) return null;
            var r = await CloudClient.Table("GET", Url, CleApi, await SessionCloud.Jeton(), "verrous",
                $"entreprise_id=eq.{e.id}&select=cible_type,cible_id,session_id,nom,motif,pris_le,expire_le");
            if (!r.Ok) return null;
            var verrous = JsonUtility.FromJson<LignesVerrous>("{\"items\":" + r.Corps + "}")?.items ?? new LigneVerrou[0];
            return verrous.Where(v => v.session_id != SessionId &&
                                      (CloudClient.Date(v.expire_le) ?? DateTime.MinValue) >= DateTime.Now).ToList();
        }

        public static async void MettreAJourBandeaux()
        {
            try
            {
                var autres = await VerrousDesAutres();
                if (autres == null) return;
                // Bandeau sur la fiche, et prénom (étape 5, décision du 14/10) : sur l'onglet du
                // bâtiment dès qu'on y modifie quelque chose, et sur la ligne du locataire dans la
                // liste du résumé (les onglets des locataires sont masqués).
                _detenteurs.Clear();
                foreach (var x in autres) _detenteurs[Cle(x.cible_type == "batiment", x.cible_id)] = RegleVerrou.Prenom(x.nom);
                var liste = autres.Select(x => (x.cible_type, x.cible_id, x.nom)).ToList();
                var bm = BatimentManager.Instance;
                foreach (var f in FindObjectsOfType<BatimentPrefab>(true))
                {
                    Bandeau(f, "batiment", autres.FirstOrDefault(x => x.cible_type == "batiment" && x.cible_id == f.getID()));
                    var locs = new HashSet<string>(f.listLocataire.Where(l => l != null).Select(l => l.id));
                    if (bm != null) bm.menuManager.SetDetenteur(f, RegleVerrou.Detenteurs(liste, f.getID(), locs));
                    foreach (var ligne in f.GetComponentsInChildren<LocataireRowUI>(true)) ligne.SetDetenteur(Detenteur(false, ligne.LocataireId));
                }
                foreach (var f in FindObjectsOfType<LocatairePrefab>(true))
                    Bandeau(f, "locataire", autres.FirstOrDefault(x => x.cible_type == "locataire" && x.cible_id == f.getID()));
            }
            catch (Exception ex) { Debug.LogWarning("[Verrou] Bandeaux : " + ex.Message); }
        }

        /// Bandeau ambre posé en haut de la fiche, hors mise en page (n'en décale rien), avec
        /// « Forcer la libération » pour un admin (maquette 2 du 09/10).
        static void Bandeau(PrefabBatLoc fiche, string type, LigneVerrou v)
        {
            if (fiche == null) return;
            var existant = fiche.transform.Find("BandeauVerrou");
            string texte = v == null ? null : RegleVerrou.Libelle(v.nom, v.motif, CloudClient.Date(v.pris_le) ?? DateTime.Now) + " — lecture seule";
            if (existant != null && existant.GetChild(0).GetComponent<TMP_Text>().text == texte) { existant.SetAsLastSibling(); return; }
            if (existant != null) { existant.name = "BandeauVerrou (ancien)"; Destroy(existant.gameObject); }
            if (v == null) return;

            var brun = new Color32(0x63, 0x38, 0x06, 0xFF);
            var fond = UIFactory.Panel("BandeauVerrou", fiche.transform, new Color32(0xFA, 0xEE, 0xDA, 0xF5), false);
            fond.raycastTarget = false;
            fond.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)fond.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 1);
            rt.sizeDelta = new Vector2(0, 32); rt.anchoredPosition = Vector2.zero;
            var h = fond.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 6, 3, 3); h.spacing = 8; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            var t = UIFactory.Text(fond.transform, texte, UITheme.Role.Aide, brun, true, TextAlignmentOptions.Center);
            UIFactory.LE(t.gameObject, flexW: 1);
            if (SessionCloud.Entreprise?.EstAdmin == true)
            {
                var b = UIFactory.Button(fond.transform, "Forcer la libération", Color.white, brun, 26, UITheme.Role.Aide, false);
                UIFactory.Border(b.gameObject);
                UIFactory.LargeurDuTexte(b);
                string id = fiche.getID(), nomFiche = fiche.getName(), detenteur = v.nom;
                b.onClick.AddListener(() => ConfirmDialog.Instance?.Show("Forcer la libération ?",
                    $"« {nomFiche} » est en cours de modification par {detenteur}. Ses saisies non enregistrées seront perdues.",
                    () => Forcer(type, id, nomFiche), "Libérer"));
            }
            fond.transform.SetAsLastSibling();
        }

        static async void Forcer(string type, string id, string nomFiche)
        {
            var e = SessionCloud.Entreprise;
            if (e == null) return;
            try
            {
                var r = await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "forcer_liberation",
                    new ParamsForcer { p_entreprise = e.id, p_type = type, p_id = id });
                var res = r.Ok ? JsonUtility.FromJson<Resultat>(r.Corps) : null;
                if (res == null || !res.ok)
                {
                    ConfirmDialog.Erreur(res?.refus == "admin" ? "Seul un admin peut forcer la libération d'une fiche."
                                                               : "Libération impossible : " + r.Erreur);
                    return;
                }
                UndoToast.Instance?.ShowInfo($"« {nomFiche} » est libérée.");
                MettreAJourBandeaux();
            }
            catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Libération impossible : " + ex.Message); }
        }
    }
}
