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
/// que les fiches changées (bâtiment et/ou locataires), dans une file d'attente.
/// Le SERVEUR refuse si la version a changé ou si la fiche est verrouillée par une autre
/// session : le verrou tient donc aussi pour les actions en un clic.
/// Revue 17 (D2-18, lot D) : les fiches changées d'un enregistrement partent en UN appel (enregistrer_fiches) :
/// tout est écrit ou rien ne l'est. Avant, une fiche après l'autre : un refus sur la seconde laissait la première écrite.
public static class EcritureCloud
{
    public class Etat { public string Json; public int Version; public string BatimentId; public int Ordre; }
    public enum TypeOp { Batiment, Locataire, SupprimerLocataire }
    public class Op { public TypeOp Type; public string Id, BatimentId, Json; public int Version, Ordre; }
    /// Revue 17 (D2-18, D2-10, lot D) : `type` et `id` nomment la fiche refusée par enregistrer_fiches ; `version` et
    /// `maj_session` accompagnent un refus « version » (version de la ligne au serveur, session de sa dernière écriture) ;
    /// `versions` : une entrée par fiche écrite, dans l'ordre de l'envoi.
    [Serializable] public class VersionFiche { public string type, id; public int version; }
    [Serializable] public class Resultat
    {
        public bool ok; public int version; public string refus, nom, motif, pris_le, type, id, maj_session;
        public VersionFiche[] versions;
    }

    /// Une session = cette fenêtre CIPL (deux PC avec le même compte = deux sessions).
    public static readonly string SessionId = Guid.NewGuid().ToString("N");
    static readonly Dictionary<string, Etat> _cache = new Dictionary<string, Etat>();

    // ── Sans réseau (testé en EditMode) ────────────────────────────────────────

    public static string Cle(bool batiment, string id) => (batiment ? "b:" : "l:") + id;

    /// La version du serveur telle que ce poste la connaît (0 : inconnue). Transfert (01/11) : le serveur refuse si elle a bougé.
    public static int Version(bool batiment, string id) => _cache.TryGetValue(Cle(batiment, id), out var e) ? e.Version : 0;

    /// Retient l'état du serveur d'un bâtiment et de ses locataires (JSON de CIPL + versions).
    /// Revue 17 (D2-05, lot D) : `ordres` = l'ordre RÉEL de chaque locataire au serveur (trous et doublons compris) ;
    /// sans lui (ou pour un locataire qui n'y est pas), la suite du précédent : 0, 1, 2… comme avant.
    public static void Memoriser(IDictionary<string, Etat> cache, Batiment b, int versionBat, IDictionary<string, int> versionsLoc,
                                 IDictionary<string, int> ordres = null)
    {
        DonneesCloud.Decouper(b, out var jb, out var jl);
        cache[Cle(true, b.id)] = new Etat { Json = jb, Version = versionBat };
        var locs = (b.locataireDuBatiment ?? new List<Locataire>()).Where(l => l != null).ToList();
        int ordre = -1;
        for (int i = 0; i < locs.Count; i++)
        {
            ordre = ordres != null && ordres.TryGetValue(locs[i].id, out int o) ? o : ordre + 1;
            cache[Cle(false, locs[i].id)] = new Etat
            {
                Json = jl[i], BatimentId = b.id, Ordre = ordre,
                Version = versionsLoc != null && versionsLoc.TryGetValue(locs[i].id, out int v) ? v : 0,
            };
        }
    }

    /// Ce qui a changé depuis le dernier état connu : bâtiment d'abord (les locataires en dépendent).
    /// Revue 17 (D2-05, lot D) : l'ordre n'est plus renuméroté. Un locataire connu dont l'ordre connu dépasse celui du
    /// précédent le garde (rien ne part si son contenu n'a pas changé) ; sinon (nouveau, ou déplacé) il prend l'ordre du
    /// précédent + 1 et part. Les trous sont permis : le serveur trie par ordre, sans contrainte d'unicité (etape2).
    /// Avant, supprimer le 2e de 8 locataires réécrivait les 6 suivants : un seul tenu par un collègue faisait tout refuser.
    public static List<Op> Differences(Batiment b, IDictionary<string, Etat> cache)
    {
        var ops = new List<Op>();
        DonneesCloud.Decouper(b, out var jb, out var jl);
        cache.TryGetValue(Cle(true, b.id), out var eb);
        if (eb == null || eb.Json != jb)
            ops.Add(new Op { Type = TypeOp.Batiment, Id = b.id, Json = jb, Version = eb?.Version ?? 0 });

        var locs = (b.locataireDuBatiment ?? new List<Locataire>()).Where(l => l != null).ToList();
        int ordre = -1;
        for (int i = 0; i < locs.Count; i++)
        {
            cache.TryGetValue(Cle(false, locs[i].id), out var el);
            bool garde = el != null && el.BatimentId == b.id && el.Ordre > ordre;
            ordre = garde ? el.Ordre : ordre + 1;
            if (!garde || el.Json != jl[i])
                ops.Add(new Op { Type = TypeOp.Locataire, Id = locs[i].id, BatimentId = b.id, Json = jl[i], Ordre = ordre, Version = el?.Version ?? 0 });
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
            var siens = cache.Where(kv => kv.Key.StartsWith("l:") && kv.Value.BatimentId == b.id).ToList();
            // Revue 17 (D2-05, lot D) : l'ordre connu du serveur reste, comme les versions (seul le contenu se recale).
            Memoriser(cache, b, vb, siens.ToDictionary(kv => kv.Key.Substring(2), kv => kv.Value.Version),
                      siens.ToDictionary(kv => kv.Key.Substring(2), kv => kv.Value.Ordre));
        }
    }

    public static void Recaler(IEnumerable<Batiment> bats) { Recaler(_cache, bats); _copieAEcrire = true; }

    // ── Revue 17 (lot D) : règles de l'envoi groupé (sans réseau, testées en EditMode) ──

    static string NomType(TypeOp t) => t == TypeOp.Batiment ? "batiment" : t == TypeOp.Locataire ? "locataire" : "supprimer_locataire";

    /// Revue 17 (D2-18, lot D) : paramètres de enregistrer_fiches. Toutes les fiches d'un enregistrement dans un seul
    /// corps, dans l'ordre d'application (le bâtiment d'abord) ; le JSON de CIPL part en objet. « batiment » et « ordre »
    /// seulement pour un locataire ; ni « version » ni « data » pour une suppression.
    public static string CorpsEnregistrerTous(string entreprise, IList<Op> ops, string session)
    {
        var sb = new StringBuilder("{\"p_entreprise\":").Append(Q(entreprise)).Append(",\"p_ops\":[");
        for (int i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            sb.Append(i > 0 ? "," : "").Append("{\"type\":").Append(Q(NomType(op.Type))).Append(",\"id\":").Append(Q(op.Id));
            if (op.Type == TypeOp.Locataire) sb.Append(",\"batiment\":").Append(Q(op.BatimentId)).Append(",\"ordre\":").Append(op.Ordre);
            if (op.Type != TypeOp.SupprimerLocataire) sb.Append(",\"version\":").Append(op.Version).Append(",\"data\":").Append(op.Json);
            sb.Append('}');
        }
        return sb.Append("],\"p_session\":").Append(Q(session)).Append(",\"p_modele\":").Append(DonneesCloud.Modele).Append('}').ToString();
    }

    /// Réponse d'une fonction d'enregistrement ; null si elle est illisible.
    public static Resultat LireResultat(string corps)
    {
        try { return JsonUtility.FromJson<Resultat>(corps); }
        catch (Exception) { return null; }
    }

    /// Revue 17 (D2-18, lot D) : l'état connu avance d'un coup, et seulement sur l'accord du serveur : `ok`, et une
    /// version par fiche, dans l'ordre de l'envoi. Sinon rien ne bouge (faux) : pas d'état connu à moitié avancé.
    public static bool Avancer(IDictionary<string, Etat> cache, IList<Op> ops, Resultat res)
    {
        var v = res?.versions;
        if (res == null || !res.ok || v == null || v.Length != ops.Count) return false;
        for (int i = 0; i < ops.Count; i++)
            if (v[i] == null || v[i].id != ops[i].Id || (ops[i].Type != TypeOp.SupprimerLocataire && v[i].version <= 0)) return false;
        for (int i = 0; i < ops.Count; i++)
        {
            if (ops[i].Type == TypeOp.SupprimerLocataire) cache.Remove(Cle(false, ops[i].Id));
            else cache[Cle(ops[i].Type == TypeOp.Batiment, ops[i].Id)] =
                new Etat { Json = ops[i].Json, Version = v[i].version, BatimentId = ops[i].BatimentId, Ordre = ops[i].Ordre };
        }
        return true;
    }

    /// La fiche que le serveur a refusée (son type et son id sont dans le refus) ; null s'il ne la nomme pas.
    public static Op OpRefusee(IList<Op> ops, Resultat res) =>
        string.IsNullOrEmpty(res?.id) ? null
        : ops.FirstOrDefault(o => o.Id == res.id && NomType(o.Type) == res.type) ?? ops.FirstOrDefault(o => o.Id == res.id);

    /// Revue 17 (D2-10, lot D) : refus « version » alors que la dernière écriture de la ligne vient de MA session : mon
    /// écriture précédente était passée et sa réponse s'est perdue. Personne n'a écrit depuis : on reprend la version
    /// rendue et on réessaie (une fois, voir Envoyer).
    public static bool Reessayer(Resultat res, string session) =>
        res != null && res.refus == "version" && res.version > 0 && !string.IsNullOrEmpty(session) && res.maj_session == session;

    /// Revue 17 (D2-05, lot D) : une fiche refusée à une écriture de fond ne repart pas telle quelle avec la saisie
    /// suivante (son refus emportait cette saisie). `refuses` : clé de fiche → JSON refusé. Dès que la fiche est modifiée,
    /// son JSON diffère : elle repart, et son refus s'affiche normalement. Une suppression ou une création (version 0,
    /// par exemple après « Annuler » d'une suppression) n'est jamais retenue : c'est toujours un geste.
    public static List<Op> SansRefuses(List<Op> ops, IDictionary<string, string> refuses) =>
        refuses == null || refuses.Count == 0 ? ops
        : ops.Where(o => o.Type == TypeOp.SupprimerLocataire || o.Version == 0
                         || !refuses.TryGetValue(Cle(o.Type == TypeOp.Batiment, o.Id), out var j) || j != o.Json).ToList();

    /// Revue 17 (D2-16, lot D) : qui tient ce bâtiment ou l'un de ses locataires (d'après l'état connu), selon le dernier
    /// relevé des verrous des autres (`detenteurs` : clé de fiche → prénom). null : personne.
    public static string TenuParAutre(string batimentId, IDictionary<string, Etat> cache, IDictionary<string, string> detenteurs) =>
        RegleVerrou.Detenteurs(
            detenteurs.Select(kv => (kv.Key.StartsWith("b:") ? "batiment" : "locataire", kv.Key.Substring(2), kv.Value)),
            batimentId,
            new HashSet<string>(cache.Where(kv => kv.Key.StartsWith("l:") && kv.Value.BatimentId == batimentId).Select(kv => kv.Key.Substring(2))));

    // ── Étape 8b : copie locale de l'état connu du serveur ─────────────────────

    static bool _copieAEcrire;
    static float _prochaineCopie;

    /// Réécrit la copie locale si l'état connu du serveur a changé (au plus toutes les 5 s, et à la
    /// fermeture). Pas pendant un démarrage sur la copie : rien de neuf n'est connu.
    public static void GarderCopie(bool maintenant = false)
    {
        if (!_copieAEcrire || HorsLigne.DepuisCopie || SessionCloud.Entreprise == null) return;
        if (!maintenant && Time.realtimeSinceStartup < _prochaineCopie) return;
        _prochaineCopie = Time.realtimeSinceStartup + 5f;
        _copieAEcrire = false;
        CopieLocale.Garder(SessionCloud.Entreprise, SessionCloud.Session?.user, HorsLigne.AJourLe, _cache);
    }

    /// Démarrage sur la copie : elle devient l'état connu du serveur (versions comprises).
    public static void ReprendreCopie(IDictionary<string, Etat> etats)
    {
        _cache.Clear();
        _refusDeFond.Clear(); _saisieEchouee.Clear();
        foreach (var kv in etats) _cache[kv.Key] = kv.Value;
    }

    /// Paramètres de enregistrer_batiment / enregistrer_locataire ; le JSON de CIPL part en objet.
    public static string CorpsEnregistrer(string entreprise, Op op, string session)
    {
        var sb = new StringBuilder("{\"p_entreprise\":").Append(Q(entreprise)).Append(",\"p_id\":").Append(Q(op.Id));
        if (op.Type == TypeOp.Locataire) sb.Append(",\"p_batiment\":").Append(Q(op.BatimentId)).Append(",\"p_ordre\":").Append(op.Ordre);
        return sb.Append(",\"p_version\":").Append(op.Version).Append(",\"p_data\":").Append(op.Json)
                 .Append(",\"p_session\":").Append(Q(session)).Append(",\"p_modele\":").Append(DonneesCloud.Modele).Append('}').ToString();
    }

    public static string MotifRefus(Resultat r, string nomFiche) => r.refus == "verrou"
        ? $"« {nomFiche} » : {RegleVerrou.Libelle(r.nom, r.motif, CloudClient.Date(r.pris_le) ?? DateTime.Now)}. Vos modifications ne sont pas enregistrées."
        : r.refus == "application"   // Revue 17 (D2-01) : une version plus récente a écrit la fiche
        ? $"Une version plus récente de CIPL a enregistré « {nomFiche} » : mettez CIPL à jour (bandeau en haut, ou relancez CIPL) " +
          "pour l'enregistrer. Vos modifications ne sont pas enregistrées."
        : r.refus == "supprimee"   // revue 16c, D2-15 : la fiche n'existe plus (le serveur le dit depuis etape16c4)
        ? $"« {nomFiche} » a été supprimé entre-temps par un collègue : vos modifications ne sont pas enregistrées."
        : $"« {nomFiche} » a été modifié entre-temps par {(string.IsNullOrEmpty(r.nom) ? "un collègue" : r.nom)} : " +
          "vos modifications ne sont pas enregistrées, la fiche est rechargée.";

    static string Q(string s) => CloudClient.Q(s);   // D3-14 : un seul échappement JSON pour tout le cloud

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
        // Revue 17 (contrat C1, lot D) : une lecture en plusieurs pages (CloudClient.TableTout) peut rendre deux fois la même
        // ligne si un collègue insère entre deux pages : une fiche n'est gardée qu'une fois (sinon deux onglets du même id).
        var vus = new HashSet<string>();
        foreach (var lb in bats)
        {
            if (lb?.data == null || !vus.Add(lb.id)) continue;
            var siens = locs.Where(l => l?.data != null && l.batiment_id == lb.id)
                            .GroupBy(l => l.data.id).Select(g => g.First()).OrderBy(l => l.ordre).ToList();
            lb.data.locataireDuBatiment = siens.Select(l => l.data).ToList();
            // Revue 17 (D2-05, lot D) : l'état connu garde l'ordre lu au serveur (avant : l'indice, d'où la renumérotation).
            var parId = siens.GroupBy(l => l.data.id).ToList();
            Memoriser(cache, lb.data, lb.version, parId.ToDictionary(g => g.Key, g => g.First().version),
                      parId.ToDictionary(g => g.Key, g => g.First().ordre));
            res.Add(lb.data);
        }
        return res;
    }

    // ── Réseau ─────────────────────────────────────────────────────────────────

    static string Url => SessionCloud.Url;
    static string CleApi => SessionCloud.Cle;

    public static Task<(List<Batiment> batiments, string erreur)> Charger(string entrepriseId) => Charger(entrepriseId, _cache);

    /// Les fiches d'une AUTRE entreprise, sans toucher au cache de celle qui est ouverte (transfert, 01/11 : lire la
    /// destination vidait le cache de l'origine, et toute écriture suivante était refusée « version »).
    public static Task<(List<Batiment> batiments, string erreur)> Consulter(string entrepriseId)
        => Charger(entrepriseId, new Dictionary<string, Etat>());

    static async Task<(List<Batiment> batiments, string erreur)> Charger(string entrepriseId, IDictionary<string, Etat> cache)
    {
        string jeton = await SessionCloud.Jeton();
        // Revue 17 (D2-06, lot D) : lecture page après page (CloudClient.TableTout) : au-delà du plafond de lignes du
        // serveur, une lecture en une fois rendait une liste coupée, sans erreur. L'ordre est total (clé unique), pour
        // qu'aucune ligne ne soit sautée ni lue deux fois d'une page à l'autre.
        // Revue 17 (D9-08, lot E) : les deux lectures partent ensemble (un aller-retour de moins à l'ouverture).
        var tb = CloudClient.TableTout(Url, CleApi, jeton, "batiments", $"entreprise_id=eq.{entrepriseId}&select=id,version,data&order=id.asc");
        var tl = CloudClient.TableTout(Url, CleApi, jeton, "locataires",
                                       $"entreprise_id=eq.{entrepriseId}&select=id,batiment_id,ordre,version,data&order=ordre.asc,id.asc");
        var rb = await tb;
        var rl = await tl;
        if (!rb.Ok) return (null, rb.Erreur);
        if (!rl.Ok) return (null, rl.Erreur);
        try
        {
            var bats = Lire(rb.Corps, rl.Corps, cache);
            if (ReferenceEquals(cache, _cache)) { _copieAEcrire = true; _refusDeFond.Clear(); _saisieEchouee.Clear(); }   // D2-05 : tout est relu
            return (bats, null);
        }
        catch (Exception e) { return (null, "Données illisibles : " + e.Message); }
    }

    /// Revue 17 (D2-20, lot D) : l'entreprise change : l'état connu du serveur est vidé AVANT de poser la nouvelle.
    /// Sinon le gardien pouvait écrire la copie locale de la nouvelle entreprise avec les fiches de l'ancienne.
    public static void Oublier() { _cache.Clear(); _copieAEcrire = false; _refusDeFond.Clear(); _saisieEchouee.Clear(); }

    /// Revue 17 (lot D) : la fiche n'est pas périmée et le serveur connaît tout son contenu (aucune différence avec
    /// l'état connu). À lire APRÈS « await Attendre() » : un refus ou une coupure laissent une différence. Hors cloud : vrai.
    public static bool AJour(Batiment b) =>
        b != null && (!SessionCloud.Active || SessionCloud.Entreprise == null || (!Perime(b) && Differences(b, _cache).Count == 0));

    // ── Étape 5 : fiches relues pendant qu'un écran était ouvert dessus ─────────

    /// Objets Batiment remplacés par une relecture (changement d'un collègue, rechargement) :
    /// un écran resté ouvert sur l'ancien ne doit plus rien écrire, sinon il renverrait des
    /// locataires périmés avec la version à jour et effacerait le travail du collègue.
    // Revue 17 (D9-09, lot E) : une table à clés faibles : l'entrée disparaît quand plus rien ne tient l'ancien bâtiment. Le
    // HashSet ne faisait qu'ajouter : chaque rechargement, reconnexion ou relecture d'un collègue y gardait des bâtiments entiers
    // (+19 Mo jamais rendus après 20 relectures d'un bâtiment de 10 locataires, mesuré le 16/11). Un écran resté ouvert sur
    // l'ancien bâtiment le tient encore : il reste périmé, comme avant.
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Batiment, object> _perimes =
        new System.Runtime.CompilerServices.ConditionalWeakTable<Batiment, object>();
    static readonly object Marque = new object();
    public static void Perimer(Batiment b) { if (b != null) _perimes.GetValue(b, _ => Marque); }
    public static bool Perime(Batiment b) => b != null && _perimes.TryGetValue(b, out _);

    /// Fiches que j'ai supprimées : leur écho (sans auteur) ne doit pas passer pour un collègue.
    static readonly HashSet<string> _supprimesParMoi = new HashSet<string>();
    public static bool SupprimeParMoi(string id) => id != null && _supprimesParMoi.Contains(id);

    /// Revue 17 (D2-23, lot D) : ces fiches vont être supprimées par moi hors de la file (transfert : le serveur supprime
    /// les lignes d'origine). À appeler AVANT l'appel : leur écho passait pour « supprimé par un collègue ».
    public static void NoterSuppression(IEnumerable<string> ids)
    {
        if (ids == null) return;
        foreach (var id in ids) if (id != null) _supprimesParMoi.Add(id);
    }

    /// Revue 17 (D2-23, lot D ; reprise) : l'appel a échoué ou a été refusé, les fiches restent : on retire la note (comme la
    /// file sur un échec). Sinon la suppression de l'une d'elles par un collègue ne se voyait plus sur ce poste.
    public static void RetirerSuppression(IEnumerable<string> ids)
    {
        if (ids == null) return;
        foreach (var id in ids) if (id != null) _supprimesParMoi.Remove(id);
    }

    public static string BatimentDuLocataire(string locataireId) =>
        locataireId != null && _cache.TryGetValue(Cle(false, locataireId), out var e) ? e.BatimentId : null;

    /// Je travaille dans ce bâtiment (fiche ou écran réservés, envoi en cours, ou enregistrement
    /// en attente de la connexion) : sa relecture attend.
    public static bool Occupe(string batimentId) =>
        !_file.IsCompleted
        || FactureEmission.EnvoisEnCours > 0   // Revue 17 (D4-07, coutures) : un formulaire envoie sa facture, même sa réservation perdue : il l'enregistrera sur cette fiche
        || _enAttente.Any(b => b.id == batimentId)
        || _refusOuverts.Contains(batimentId)
        || _tenus.Any(t => (t.Type == "batiment" && t.Id == batimentId) || (t.Type == "locataire" && BatimentDuLocataire(t.Id) == batimentId));

    // ── Étape 8 : enregistrements en attente de la connexion ───────────────────

    /// Saisies commencées avant la coupure, ou envoi coupé en route : gardées ici, elles repartent
    /// au retour de la connexion, contrôlées par la version (décision du 09/10).
    static readonly HashSet<Batiment> _enAttente = new HashSet<Batiment>();

    /// Bâtiments dont un enregistrement refusé est encore affiché : leur relecture attend le « OK ».
    static readonly HashSet<string> _refusOuverts = new HashSet<string>();

    public static void MettreEnAttente(Batiment b)
    {
        if (b == null || !_enAttente.Add(b)) return;
        UndoToast.Instance?.ShowInfo($"Hors ligne : « {b.Name} » sera enregistré au retour de la connexion.");
    }

    /// Revue 17 (D8-01) : les fiches qui attendent la connexion (en mémoire seulement : fermer CIPL, se déconnecter ou changer
    /// d'entreprise les perdrait, D8-14). Une fiche périmée (entreprise vidée depuis) est déjà perdue : elle n'est plus citée.
    public static List<string> NomsEnAttente() => _enAttente.Where(b => b != null && !Perime(b))
        .Select(b => string.IsNullOrWhiteSpace(b.Name) ? "(sans nom)" : b.Name).Distinct().ToList();

    public static void RenvoyerEnAttente()
    {
        foreach (var b in _enAttente.ToList())
        {
            _enAttente.Remove(b);
            if (!Perime(b)) Enregistrer(b);   // relue entre-temps (rechargement) : il n'y a plus rien à renvoyer
        }
    }

    /// Une de ses fiches est réservée par moi : une saisie était en cours quand la connexion est tombée.
    static bool TientUneFiche(Batiment b) =>
        _tenus.Any(t => (t.Type == "batiment" && t.Id == b.id) || (t.Type == "locataire" && BatimentDuLocataire(t.Id) == b.id));

    /// Un bâtiment et ses locataires, avec leurs versions (null = supprimé). Le cache n'est
    /// pas touché : voir Adopter, une fois la fiche reconstruite.
    public static async Task<(Batiment bat, Dictionary<string, Etat> etat, string erreur)> ChargerUn(string entrepriseId, string batimentId)
    {
        string jeton = await SessionCloud.Jeton();
        string id = Uri.EscapeDataString(batimentId);
        // Revue 17 (D9-08, lot E) : les deux lectures partent ensemble.
        var tb = CloudClient.Table("GET", Url, CleApi, jeton, "batiments", $"entreprise_id=eq.{entrepriseId}&id=eq.{id}&select=id,version,data");
        var tl = CloudClient.Table("GET", Url, CleApi, jeton, "locataires",
                                   $"entreprise_id=eq.{entrepriseId}&batiment_id=eq.{id}&select=id,batiment_id,ordre,version,data&order=ordre.asc,id.asc");   // D2-05 (lot D) : ordre total, comme Charger
        var rb = await tb;
        var rl = await tl;
        if (!rb.Ok) return (null, null, rb.Erreur);
        if (!rl.Ok) return (null, null, rl.Erreur);
        var etat = new Dictionary<string, Etat>();
        try { return (Lire(rb.Corps, rl.Corps, etat).FirstOrDefault(), etat, null); }
        catch (Exception e) { return (null, null, "Données illisibles : " + e.Message); }
    }

    /// Le cache prend l'état relu d'un bâtiment (ses anciens locataires en sortent), puis se
    /// recale sur la fiche reconstruite (mises à niveau du chargement).
    public static void Adopter(string batimentId, IDictionary<string, Etat> etat, Batiment b)
    {
        Adopter(_cache, batimentId, etat, b);
        _copieAEcrire = true;
        if (batimentId != null) { _refusDeFond.Remove(batimentId); _saisieEchouee.Remove(batimentId); }   // Revue 17 (D2-05, lot D) : fiche relue, ses refus de fond sont oubliés
    }

    public static void Adopter(IDictionary<string, Etat> cache, string batimentId, IDictionary<string, Etat> etat, Batiment b)
    {
        foreach (var k in cache.Where(kv => kv.Key == Cle(true, batimentId) || kv.Value.BatimentId == batimentId).Select(kv => kv.Key).ToList())
            cache.Remove(k);
        foreach (var kv in etat) cache[kv.Key] = kv.Value;
        if (b != null) Recaler(cache, new[] { b });
    }

    /// Revue 17 (D9-04, lot E) : la relecture d'un bâtiment n'apporte rien : les mêmes fiches (bâtiment et locataires), aux
    /// mêmes versions que l'état connu. Il n'y a alors rien à reconstruire (après une reconnexion, tous les bâtiments sont relus :
    /// 27,6 s pour 30 bâtiments de 10 locataires, mesuré le 16/11). Une fiche refusée a une version connue en retard sur le
    /// serveur : elle n'est jamais « inchangée ». Pure, testée.
    public static bool Inchange(string batimentId, IDictionary<string, Etat> etat) => Inchange(_cache, batimentId, etat);

    public static bool Inchange(IDictionary<string, Etat> cache, string batimentId, IDictionary<string, Etat> etat)
    {
        if (cache == null || etat == null || string.IsNullOrEmpty(batimentId)) return false;
        var connues = cache.Where(kv => kv.Key == Cle(true, batimentId) || kv.Value?.BatimentId == batimentId).ToList();
        return connues.Count > 0 && connues.Count == etat.Count
               && connues.All(kv => etat.TryGetValue(kv.Key, out var e) && e != null && e.Version == kv.Value.Version);
    }

    static Task _file = Task.CompletedTask;   // un enregistrement à la fois, dans l'ordre

    /// `silencieux` (revue 16c, D2-04) : une écriture de fond (tournée de l'annuaire, plateforme) ; un refus est
    /// journalisé et abandonné, sans message : les saisies en cours de l'utilisateur restent, et la fiche refusée est
    /// relue en silence quand personne n'y travaille (Revue 17, D2-05 : voir Envoyer).
    public static void Enregistrer(Batiment b, bool silencieux = false)
    {
        // Étape 8 : hors ligne, une saisie commencée avant la coupure attend la connexion ; une
        // action en un clic est refusée (lecture seule), et la fiche sera relue au retour.
        if (HorsLigne.Actif)
        {
            if (b != null && TientUneFiche(b)) MettreEnAttente(b);
            else if (!silencieux) ConfirmDialog.Erreur(HorsLigne.Refus + " Cette modification n'est pas enregistrée.");
            return;
        }
        // Étape 9c1 : abonnement expiré, lecture seule ; la fiche est relue (la modification n'est
        // faite qu'en mémoire).
        if (AbonnementCloud.LectureSeule) { if (!silencieux) ConfirmDialog.Erreur(AbonnementCloud.Refus, DemarrageCloud.Recharger); return; }
        if (!silencieux) _saisiesEnFile++;   // Revue 17 (D2-05, lot D) : rendu à la fin d'Envoyer
        var avant = _file; _file = Envoyer(avant, b, silencieux, SessionCloud.Entreprise);   // D2-09 : l'entreprise de la mise en file
    }

    /// Revue 17 (D2-05, lot D) : saisies (enregistrements non silencieux) en file ou en vol. Tant qu'il y en a, un refus
    /// de fond n'est pas retenu : la fiche refusée peut porter une saisie déjà faite en mémoire, qui doit avoir son message.
    static int _saisiesEnFile;

    /// Revue 17 (D2-05, lot D) : par bâtiment, les fiches qu'une écriture de fond s'est vu refuser (clé de fiche → JSON
    /// refusé). Elles ne repartent pas telles quelles (SansRefuses) ; oubliées dès que le bâtiment est relu (Adopter,
    /// Charger) ou l'état vidé (Oublier).
    static readonly Dictionary<string, Dictionary<string, string>> _refusDeFond = new Dictionary<string, Dictionary<string, string>>();

    /// Revue 17 (D2-05, lot D ; reprise) : bâtiments dont une saisie a échoué sans panne (HTTP 400, 500, exception) : elle
    /// reste en mémoire, retentée au prochain enregistrement. Un refus de fond sur l'un d'eux n'est pas retenu ni relu.
    /// Oubliés à l'accord du serveur, à la relecture (Adopter, Charger) et quand l'état est vidé.
    static readonly HashSet<string> _saisieEchouee = new HashSet<string>();

    /// Revue 17 (D2-05, lot D ; reprise) : retenir (et faire relire) une fiche refusée à une écriture de fond, seulement si
    /// elle ne peut porter aucune saisie : ni suppression ni création (toujours un geste), ni `saisie` en cours sur elle.
    public static bool RetenirRefusDeFond(Op refusee, bool saisie) =>
        refusee != null && refusee.Type != TypeOp.SupprimerLocataire && refusee.Version != 0 && !saisie;

    /// Revue 17 (D2-18, lot D ; décision du 11/11) : tant que Serveur/etape17c_supabase.sql n'est pas passé sur le serveur,
    /// enregistrer_fiches n'existe pas (HTTP 404) : CIPL se replie sur l'ancienne façon, une fiche après l'autre, et le dit
    /// UNE fois au journal. Un 404 est une réponse du serveur : ni hors ligne (HorsLigne.Panne), ni message d'erreur.
    static bool _sansEnregistrerFiches;

    static async Task Envoyer(Task avant, Batiment b, bool silencieux, EntrepriseCloud e = null)
    {
        try
        {
            try { await avant; } catch (Exception) { /* l'échec précédent a déjà été signalé */ }
            if (e == null) e = SessionCloud.Entreprise;
            if (e == null || b == null) return;
            var repris = new HashSet<string>();   // D2-10 : une reprise par fiche, jamais en boucle
            // ponytail: 30 tours au plus. Plusieurs tours : une écriture de fond (un par fiche refusée) ou une reprise D2-10
            // (une par fiche) ; au-delà, le reste part avec l'enregistrement suivant (message après la boucle).
            for (int tour = 0; tour < 30; tour++)
            {
                // Les deux gardes sont revérifiées à chaque tour (un tour = un aller-retour au serveur).
                // Revue 16c (D2-09) : l'entreprise a changé pendant l'attente : la fiche de A ne part pas vers B.
                if (e != SessionCloud.Entreprise) { Debug.LogWarning($"[Cloud] « {b.Name} » : entreprise changée pendant l'attente, enregistrement abandonné."); return; }
                // Revue 16c (D2-07) : vérifié au DÉPART réel, pas seulement à la mise en file : une fiche périmée par un
                // rechargement pendant l'attente repartirait avec les versions neuves et écraserait l'écriture d'un collègue.
                if (Perime(b)) { Debug.LogWarning($"[Cloud] « {b.Name} » : fiche périmée pendant l'attente, enregistrement abandonné."); return; }

                _refusDeFond.TryGetValue(b.id, out var refuses);
                var ops = SansRefuses(Differences(b, _cache), refuses);
                if (ops.Count == 0) return;

                var (echec, refus, refusee) = await Transmettre(e, b.id, ops);
                if (echec == null && refus == null) { _saisieEchouee.Remove(b.id); return; }   // tout est écrit, l'état connu a avancé

                // Réseau en panne : l'état connu n'est pas avancé ; la fiche repart au retour de la
                // connexion (étape 8). Autre échec : retenté au prochain enregistrement.
                if (echec != null)
                {
                    if (HorsLigne.Panne(echec.Ok, echec.Code)) MettreEnAttente(b);
                    else if (silencieux) Debug.LogWarning($"[Cloud] « {b.Name} » : écriture de fond impossible ({echec.Erreur}), reprise plus tard.");
                    else { _saisieEchouee.Add(b.id); ConfirmDialog.Erreur($"Enregistrement de « {b.Name} » impossible : {echec.Erreur}"); }
                    return;
                }

                string nom = refusee == null || refusee.Type == TypeOp.Batiment ? b.Name
                    : b.locataireDuBatiment?.FirstOrDefault(l => l?.id == refusee.Id)?.Name ?? b.Name;
                string cle = refusee == null ? null : Cle(refusee.Type == TypeOp.Batiment, refusee.Id);

                // Revue 17 (D2-10, lot D) : refus « version » sur MA dernière écriture (sa réponse s'était perdue) : la version
                // rendue par le serveur est reprise pour cette fiche (le contenu connu ne change pas : la fiche repart).
                if (refusee != null && refusee.Type != TypeOp.SupprimerLocataire && Reessayer(refus, SessionId) && repris.Add(cle))
                {
                    _cache.TryGetValue(cle, out var connu);
                    _cache[cle] = new Etat { Json = connu?.Json, Version = refus.version,
                                             BatimentId = connu?.BatimentId ?? refusee.BatimentId, Ordre = connu?.Ordre ?? refusee.Ordre };
                    Debug.Log($"[Cloud] « {nom} » : ma dernière écriture était passée (réponse perdue) : version {refus.version} reprise, nouvel essai.");
                    continue;
                }

                // Les saisies refusées restent à l'écran derrière le message ; la fiche se recharge
                // au clic sur « OK » (décision du 09/10, reprise après une coupure). D'ici là, la
                // relecture en direct attend (retour du 16/10 : elle remplaçait les saisies avant le OK).
                string id = b.id;
                if (silencieux)
                {
                    // Revue 17 (D2-05, lot D ; reprise) : la fiche peut porter une saisie (en file, en attente de la connexion,
                    // échouée et gardée en mémoire, formulaire en plein envoi, écran ouvert dessus) : arrêt comme avant, SANS
                    // relecture (elle remplacerait la saisie sans un mot) : la fiche repartira, son refus s'affichera.
                    bool saisie = _saisiesEnFile > 0 || FactureEmission.EnvoisEnCours > 0 || _enAttente.Contains(b) || _saisieEchouee.Contains(id)
                                  || (refusee != null && Tient(refusee.Type == TypeOp.Batiment ? "batiment" : "locataire", refusee.Id));
                    if (!RetenirRefusDeFond(refusee, saisie))
                    {
                        Debug.LogWarning($"[Cloud] « {nom} » : {MotifRefus(refus, nom)} — écriture de fond abandonnée, reprise à la prochaine occasion.");
                        return;
                    }
                    // Sinon la fiche est relue (en silence, quand personne n'y travaille), et son JSON refusé est retenu : il
                    // ne repartira pas avec la saisie suivante. Les autres fiches de cette écriture de fond repartent au tour suivant.
                    Debug.LogWarning($"[Cloud] « {nom} » : {MotifRefus(refus, nom)} — écriture de fond abandonnée pour cette fiche, relecture demandée.");
                    DirectCloud.ARelire(id);
                    if (refuses == null) _refusDeFond[id] = refuses = new Dictionary<string, string>();
                    refuses[cle] = refusee.Json;
                    continue;
                }
                // BH8 (02/11) : une trace par refus (deux essais ont été perdus à deviner ce qui s'était passé), et au OK la seule
                // fiche concernée se relit, pas toute l'entreprise.
                Debug.LogWarning($"[Cloud] « {nom} » : enregistrement refusé ({(string.IsNullOrEmpty(refus.refus) ? "réponse illisible" : refus.refus)}{(string.IsNullOrEmpty(refus.nom) ? "" : ", " + refus.nom)}).");
                _refusOuverts.Add(id);
                ConfirmDialog.Erreur(MotifRefus(refus, nom), () => { _refusOuverts.Remove(id); DirectCloud.ARelire(id); });
                return;
            }
            // Revue 17 (D2-10, lot D ; reprise) : les 30 tours sont passés sans tout écrire (réponse perdue sur plus de 30 fiches,
            // une reprise par tour) : une saisie le dit au lieu de s'arrêter sans un mot. Ce qui reste repart au prochain enregistrement.
            Debug.LogWarning($"[Cloud] « {b.Name} » : enregistrement incomplet après 30 tours.");
            if (!silencieux) { _saisieEchouee.Add(b.id); ConfirmDialog.Erreur($"Enregistrement de « {b.Name} » incomplet : enregistrez à nouveau."); }
        }
        catch (Exception ex)
        {
            if (!silencieux && b != null) _saisieEchouee.Add(b.id);   // D2-05 (reprise) : la saisie reste en mémoire, pas de relecture de fond
            Debug.LogException(ex); ConfirmDialog.Erreur("Enregistrement impossible : " + ex.Message);
        }
        finally { if (!silencieux) _saisiesEnFile--; }
    }

    /// Revue 17 (D2-18, lot D) : envoie les fiches en UN appel (enregistrer_fiches : tout ou rien) et avance l'état connu
    /// d'un coup, sur l'accord du serveur. Rend (null, null, null) si tout est écrit ; sinon l'appel en échec, ou le
    /// refus et la fiche refusée (null si le serveur ne la nomme pas). Dans les deux cas, RIEN n'est écrit.
    static async Task<(CloudClient.Reponse echec, Resultat refus, Op refusee)> Transmettre(EntrepriseCloud e, string batimentId, List<Op> ops)
    {
        if (_sansEnregistrerFiches) return await TransmettreUneParUne(e, ops);
        // Revue 16c (D2-10) : noté AVANT l'appel : l'écho temps réel de ma suppression peut arriver avant la réponse,
        // il passait pour la suppression d'un collègue (fiche relue, « mis à jour par un collègue »).
        var supprimes = ops.Where(o => o.Type == TypeOp.SupprimerLocataire).Select(o => o.Id).ToList();
        string jeton = await SessionCloud.Jeton();
        foreach (var id in supprimes) _supprimesParMoi.Add(id);
        var r = await CloudClient.FonctionJson(Url, CleApi, jeton, "enregistrer_fiches", CorpsEnregistrerTous(e.id, ops, SessionId));
        var res = r.Ok ? LireResultat(r.Corps) : null;
        if (res != null && res.ok)
        {
            // Accord sans les versions attendues (ne devrait pas arriver) : tout est écrit au serveur, mais l'état connu
            // ne peut pas avancer : la fiche est relue (sinon l'enregistrement suivant serait refusé « version »).
            if (!Avancer(_cache, ops, res))
            {
                Debug.LogWarning("[Cloud] enregistrer_fiches : accord sans les versions attendues, fiche relue.");
                DirectCloud.ARelire(batimentId);
            }
            Accord(ops);
            return (null, null, null);
        }
        foreach (var id in supprimes) _supprimesParMoi.Remove(id);
        if (r.Code == 404)
        {
            _sansEnregistrerFiches = true;
            Debug.LogWarning("[Cloud] enregistrer_fiches absente : passez Serveur/etape17c_supabase.sql");
            return await TransmettreUneParUne(e, ops);
        }
        if (!r.Ok) return (r, null, null);
        return (null, res ?? new Resultat(), OpRefusee(ops, res));
    }

    /// L'ancienne façon, gardée en repli tant que le serveur n'a pas enregistrer_fiches : une fiche après l'autre, l'état
    /// connu avancé fiche par fiche. Sans transaction : un refus laisse écrites les fiches d'avant (D2-18 n'est corrigé
    /// qu'une fois le SQL passé).
    static async Task<(CloudClient.Reponse echec, Resultat refus, Op refusee)> TransmettreUneParUne(EntrepriseCloud e, List<Op> ops)
    {
        foreach (var op in ops)
        {
            string jeton = await SessionCloud.Jeton();
            if (op.Type == TypeOp.SupprimerLocataire) _supprimesParMoi.Add(op.Id);   // Revue 16c (D2-10) : avant l'appel
            var r = op.Type == TypeOp.SupprimerLocataire
                ? await CloudClient.FonctionJson(Url, CleApi, jeton, "supprimer_fiche",
                    $"{{\"p_entreprise\":{Q(e.id)},\"p_type\":\"locataire\",\"p_id\":{Q(op.Id)},\"p_session\":{Q(SessionId)}}}")
                : await CloudClient.FonctionJson(Url, CleApi, jeton,
                    op.Type == TypeOp.Batiment ? "enregistrer_batiment" : "enregistrer_locataire", CorpsEnregistrer(e.id, op, SessionId));
            var res = r.Ok ? LireResultat(r.Corps) : null;
            if (res == null || !res.ok)
            {
                if (op.Type == TypeOp.SupprimerLocataire) _supprimesParMoi.Remove(op.Id);
                if (!r.Ok) return (r, null, null);
                return (null, res ?? new Resultat(), op);
            }
            if (op.Type == TypeOp.SupprimerLocataire) _cache.Remove(Cle(false, op.Id));
            else _cache[Cle(op.Type == TypeOp.Batiment, op.Id)] =
                new Etat { Json = op.Json, Version = res.version, BatimentId = op.BatimentId, Ordre = op.Ordre };
            Accord(new[] { op });
        }
        return (null, null, null);
    }

    /// Après l'accord du serveur : ce qui attendait la suppression d'un locataire (son dossier en corbeille, D7a-09 de la
    /// 16c), et la copie locale à réécrire.
    static void Accord(IEnumerable<Op> ops)
    {
        foreach (var op in ops.Where(o => o.Type == TypeOp.SupprimerLocataire).ToList())
            if (_apresSuppressionLocataire.TryGetValue(op.Id, out var apres)) { _apresSuppressionLocataire.Remove(op.Id); apres?.Invoke(); }
        // Revue 17 (D2-05, lot D ; reprise) : une fiche écrite n'est plus « refusée en fond » : sinon, revenue plus tard au
        // JSON refusé, elle ne repartait pas (et sa relecture remettait l'écriture d'entre-temps, sans un mot).
        foreach (var op in ops)
            if (_refusDeFond.TryGetValue(op.Type == TypeOp.Batiment ? op.Id : op.BatimentId ?? "", out var refuses))
                refuses.Remove(Cle(op.Type == TypeOp.Batiment, op.Id));
        _copieAEcrire = true;
    }

    /// `apresSucces` (revue 16c, D2-03) : ce qui ne doit se faire qu'après l'accord du serveur (le dossier en corbeille).
    public static void SupprimerBatiment(string id, string nom, Action apresSucces = null)
    {
        // Revue 17 (D2-16, lot D) : un collègue tient le bâtiment ou l'un de ses locataires (dernier relevé des verrous) :
        // refus tout de suite, rien n'est envoyé. Avant, seul le verrou du bâtiment comptait : le collègue en saisie sur un
        // locataire perdait tout à son « Sauvegarder ». Le serveur refuse aussi (supprimer_fiche, etape17c) : le relevé a 10 s.
        string qui = TenuParAutre(id, _cache, _detenteurs);
        if (qui != null)
        {
            ConfirmDialog.Erreur($"« {nom} » : en cours de modification par {qui}. Rien n'est supprimé : réessayez quand la fiche sera libérée.");
            return;
        }
        var avant = _file; _file = Supprimer(avant, id, nom, apresSucces);
    }

    static async Task Supprimer(Task avant, string id, string nom, Action apresSucces)
    {
        try { await avant; } catch (Exception) { }
        var e = SessionCloud.Entreprise;
        if (e == null) return;
        _supprimesParMoi.Add(id);   // D2-10 : avant l'appel (voir Envoyer)
        var r = await CloudClient.FonctionJson(Url, CleApi, await SessionCloud.Jeton(), "supprimer_fiche",
            $"{{\"p_entreprise\":{Q(e.id)},\"p_type\":\"batiment\",\"p_id\":{Q(id)},\"p_session\":{Q(SessionId)}}}");
        var res = r.Ok ? JsonUtility.FromJson<Resultat>(r.Corps) : null;
        if (res == null || !res.ok)
        {
            _supprimesParMoi.Remove(id);
            // Retour du 01/11 : le bâtiment n'a pas quitté l'écran, rien à recharger ; la relecture en direct le tient à jour.
            // Revue 17 (D2-16, lot D) : le refus « verrou » vaut aussi pour un locataire du bâtiment tenu par un collègue :
            // le message parle de la suppression (avant : « Vos modifications ne sont pas enregistrées »).
            ConfirmDialog.Erreur(res == null ? $"Suppression de « {nom} » impossible : {r.Erreur}"
                : res.refus == "verrou" ? $"« {nom} » : {RegleVerrou.Libelle(res.nom, res.motif, CloudClient.Date(res.pris_le) ?? DateTime.Now)}. " +
                                          "Rien n'est supprimé : réessayez quand la fiche sera libérée."
                : MotifRefus(res, nom));
            return;
        }
        _supprimesParMoi.Add(id);
        _cache.Remove(Cle(true, id));
        foreach (var k in _cache.Where(kv => kv.Value.BatimentId == id).Select(kv => kv.Key).ToList()) _cache.Remove(k);
        _refusDeFond.Remove(id); _saisieEchouee.Remove(id);   // Revue 17 (D2-05, lot D ; reprise) : après « Annuler », la recréation repart
        _copieAEcrire = true;
        apresSucces?.Invoke();
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
    const string MotifModification = "modification";   // le motif lu par les collègues (« … (modification) depuis … »)
    public static void ModifierSiLibre(PrefabBatLoc fiche, string type, Action modifier) =>
        Reserver(new[] { (type, fiche != null ? fiche.getID() : null, fiche != null ? fiche.getName() : null) },
                 MotifModification,
                 // Revue 16c (D2-11) : pendant l'aller-retour du verrou, une relecture a pu remplacer la fiche (objet détruit) :
                 // on le dit au lieu d'ouvrir l'écran dessus ; le verrou retombe au signal suivant (`actif` faux).
                 () => { if (fiche == null) ConfirmDialog.Erreur("La fiche vient d'être mise à jour par un collègue : réessayez « Modifier »."); else modifier(); },
                 // Revue 17 (D2-17, lot D) : le verrou tient aussi tant que l'enregistrement attend ou part (RegleVerrou.ResteTenu).
                 () => fiche != null && RegleVerrou.ResteTenu(fiche.EnEdition, EnregistrementEnCours));

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
        if (HorsLigne.Actif && fiches.Any(c => !Tient(c.type, c.id))) { ConfirmDialog.Erreur(HorsLigne.Refus); return; }   // étape 8
        if (AbonnementCloud.LectureSeule) { ConfirmDialog.Erreur(AbonnementCloud.Refus); return; }   // étape 9c1
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
        if (choisi)   // « Annuler et libérer » : c'est voulu
        {
            FermerEcrans();
            UndoToast.Instance?.ShowInfo(message);
            DemarrageCloud.Recharger();
            return;
        }
        // Fiche perdue (libération forcée, reprise après une coupure) : les saisies restent à
        // l'écran derrière le message pour être recopiées ; tout se ferme au clic sur « OK »
        // (décision du 09/10). Le signal de vie ne la redemande pas entre-temps.
        if (_abandonEnCours) return;
        _abandonEnCours = true;
        ConfirmDialog.Erreur(message, () => { _abandonEnCours = false; FermerEcrans(); DemarrageCloud.Recharger(); });
    }

    static bool _abandonEnCours;

    /// Lance le gardien (bandeaux « En cours de modification par… ») dès l'ouverture d'une entreprise.
    public static void DemarrerGardien() => Gardien.Assurer();

    /// Étape 5 : un verrou a changé (canal en direct) ; sans attendre le relevé des 10 s.
    public static void RafraichirBandeaux() => Gardien.MettreAJourBandeaux();

    /// Prénom de qui modifie cette fiche (dernier relevé des verrous des autres), sinon null :
    /// une ligne de locataire reconstruite le reprend aussitôt.
    static readonly Dictionary<string, string> _detenteurs = new Dictionary<string, string>();
    public static string Detenteur(bool batiment, string id) =>
        id != null && _detenteurs.TryGetValue(Cle(batiment, id), out var p) ? p : null;

    /// Étape 10 : les locataires que personne ne modifie, d'après les verrous relus à l'instant
    /// (les autres sessions) et ceux que je tiens. Null si la lecture échoue.
    public static async Task<Func<string, bool>> LocatairesLibres()
    {
        var autres = await Gardien.VerrousDesAutres();
        if (autres == null) return null;
        var pris = new HashSet<string>(autres.Where(v => v.cible_type == "locataire").Select(v => v.cible_id));
        return id => !pris.Contains(id) && !Tient("locataire", id);
    }

    /// Ferme sans enregistrer les écrans qui tiennent une fiche, puis libère tout (rechargement,
    /// changement d'entreprise, déconnexion) : un écran resté ouvert garderait l'ancienne fiche.
    /// Revue 17 (D4-07, coutures) : un formulaire de facture en plein envoi ne se ferme qu'une fois sa facture enregistrée
    /// (FermerDeForce des quatre formulaires) ; EnvoiEnCours et Occupe le voient d'ici là.
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

    // Revue 17 (D2-25, lot D) : SiAucunBatimentPris retiré (plus d'appelant depuis D2-08 de la 16c : il lisait les verrous
    // puis écrivait sans rien tenir). Pour écrire plusieurs bâtiments : Reserver.

    // ── Étape 7 : envoi automatique des factures ───────────────────────────────

    public const string MotifEnvoi = "envoi des factures";
    [Serializable] class ParamsEnvoi { public string p_entreprise, p_locataire, p_facture, p_session; public int p_version; }
    [Serializable] public class ResultatEnvoi { public bool ok; public string refus, nom, motif, pris_le, statut; }

    // Revue 16c (D7a-09) : ce qui ne doit se faire qu'après l'accord du serveur pour la suppression d'un locataire (son
    // dossier en corbeille), comme pour un bâtiment (D2-03).
    static readonly Dictionary<string, Action> _apresSuppressionLocataire = new Dictionary<string, Action>();
    public static void ApresSuppressionLocataire(string id, Action action) { if (id != null) _apresSuppressionLocataire[id] = action; }

    /// Écrans qui tiennent une fiche (facture, achat, charge, révision, départ…) : leurs saisies attendent leur propre
    /// bouton, la garde de fermeture doit les voir (revue 16c, D8-05). L'envoi des factures n'est pas un écran.
    /// Revue 17 (D2-17, lot D) : sans la réservation de « Modifier ». Elle tient aussi pendant l'enregistrement en vol
    /// (RegleVerrou.ResteTenu) : juste après « Sauvegarder », la fermeture disait la fiche « non enregistrée ». Une fiche
    /// vraiment en saisie est déjà citée par FermetureGuard.NomsEnEdition (EnEdition) ; l'enregistrement en vol, par QuitterSansGarde.
    public static IEnumerable<string> FichesTenues() =>
        _tenus.Where(t => t.Actif() && t.Motif != MotifEnvoi && t.Motif != MotifModification).Select(t => t.Nom);

    /// Un enregistrement attend ou part : le verrou d'un envoi n'est rendu qu'après celui de son statut.
    public static bool EnregistrementEnCours => !_file.IsCompleted;
    /// Un envoi de factures tient encore son verrou : un rechargement attend sa fin (revue 16c, D2-06).
    /// Revue 17 (D4-07, coutures) : ou un formulaire de facture envoie son e-mail (FactureEmission.EnvoisEnCours) : sa fiche perdue
    /// (FermerEcrans ne le ferme pas en plein envoi), le rechargement et la fermeture de CIPL attendent qu'il ait enregistré la facture.
    public static bool EnvoiEnCours => FactureEmission.EnvoisEnCours > 0 || _tenus.Any(t => t.Motif == MotifEnvoi && t.Actif());
    /// La file des enregistrements, pour l'attendre avant de remplacer les fiches (revue 16c, D2-07).
    public static Task Attendre() => _file;

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

    /// Mon nom affiché, tel que le serveur le connaît : "" s'il n'a jamais été saisi, null si le serveur ne répond pas.
    public static async Task<string> MonNom()
    {
        var r = await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "mon_nom_saisi", new Vide());
        if (!r.Ok) return null;
        string nom = r.Corps.Trim().Trim('"');
        return nom == "null" ? "" : nom;
    }

    /// Demande « Votre nom » à la première ouverture d'une entreprise (sinon on affiche l'e-mail).
    public static async Task VerifierNom(Canvas canvas)
    {
        if (await MonNom() != "") return;   // saisi, ou serveur muet
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
                    // Revue 17 (D2-17, D7a-17, lot D) : écran fermé pendant qu'un enregistrement attend ou part : le verrou
                    // reste jusqu'à la file vide (un collègue ne prend pas la fiche avant le serveur), pour TOUS les écrans ;
                    // FichesTenues ne le cite plus (Actif faux) : la fermeture ne dit pas « non enregistrée ».
                    if (!actif) { if (EnregistrementEnCours) continue; _tenus.Remove(t); if (!Tient(t.Type, t.Id)) Liberer(t.Type, t.Id); }
                    else if (now >= t.ProchainSignal) { t.ProchainSignal = now + 30f; Renouveler(t); }
                }
                SurveillerInactivite(now);
                GarderCopie();   // étape 8b
            }
            if (now >= _prochainesBandes && SessionCloud.Active && SessionCloud.Entreprise != null)
            {
                _prochainesBandes = now + (DirectCloud.Connecte ? 60f : 10f);   // Revue 17 (D9-12, lot E) : le direct prévient déjà
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

        void OnApplicationQuit()
        {
            GarderCopie(maintenant: true);   // étape 8b : la copie locale suit jusqu'à la fermeture
            CopieLocale.Terminer();          // Revue 17 (D9-10, lot E) : elle s'écrit en tâche de fond
            LibererTout();   // au mieux ; sinon les verrous expirent après 2 min
        }

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
                // Revue 17 (D9-12, lot E) : les fiches et les lignes sont prises dans les listes déjà tenues (bâtiments installés,
                // fiches de chaque bâtiment, lignes du résumé), sans fouiller toute l'interface (70 ms pour SCI (JBC), 0,8 s pour
                // 300 locataires, mesuré le 16/11).
                var bm = BatimentManager.Instance;
                if (bm == null) return;
                foreach (var f in bm.BatimentPrefab)
                {
                    if (f == null) continue;
                    Bandeau(f, "batiment", autres.FirstOrDefault(x => x.cible_type == "batiment" && x.cible_id == f.getID()));
                    var locs = new HashSet<string>(f.listLocataire.Where(l => l != null).Select(l => l.id));
                    bm.menuManager.SetDetenteur(f, RegleVerrou.Detenteurs(liste, f.getID(), locs));
                    if (f.locataireRowContainer != null)
                        foreach (Transform t in f.locataireRowContainer)
                        {
                            var ligne = t.GetComponent<LocataireRowUI>();
                            if (ligne != null) ligne.SetDetenteur(Detenteur(false, ligne.LocataireId));
                        }
                    foreach (var fiche in f.dictionnairelocataire.Values)
                        if (fiche != null)
                            Bandeau(fiche, "locataire", autres.FirstOrDefault(x => x.cible_type == "locataire" && x.cible_id == fiche.getID()));
                }
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
