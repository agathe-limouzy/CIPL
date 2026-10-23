using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// Étape 6a : ce qu'un passage de synchronisation des documents doit faire (testé en EditMode).
/// Trois listes, chemin → empreinte : `local` (ce PC), `manifeste` (l'empreinte au dernier
/// passage réussi : c'est elle qui dit qui, d'ici ou d'ailleurs, a changé quoi), `serveur`
/// (la table `documents`). Voir CONCEPTION_MULTIUTILISATEUR.md §8.
public static class SyncDocuments
{
    public class Distant { public string Empreinte; public bool Supprime; }
    public enum Action { Noter, Envoyer, Telecharger, SupprimerServeur, Corbeille }

    /// Les dossiers partagés de l'entreprise ; « Anciens documents » depuis le rangement du 23/10.
    public static readonly string[] Racines = { "Batiment", "Logo", DossiersDonnees.ANCIENS };

    /// Partagé (décision du 14/10) : tout ce qui est rangé dans Batiment/ et Logo/, sauf les
    /// fichiers temporaires d'Office et les réceptions en cours.
    public static bool Partage(string chemin)
    {
        if (string.IsNullOrEmpty(chemin)) return false;
        if (!Racines.Any(r => chemin.StartsWith(r + "/", StringComparison.Ordinal))) return false;
        string nom = chemin.Substring(chemin.LastIndexOf('/') + 1);
        return !nom.StartsWith("~$", StringComparison.Ordinal)
            && !nom.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            && !nom.EndsWith(".cipl-recu", StringComparison.Ordinal);
    }

    /// SHA-256 du contenu, en hexadécimal : la clé du contenu dans le stockage.
    public static string Empreinte(byte[] contenu)
    {
        using (var sha = SHA256.Create())
        {
            var sb = new StringBuilder(64);
            foreach (var b in sha.ComputeHash(contenu)) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    /// `telecharger` faux (mode Drive partagé, étape 6b) : le Drive recopie et supprime lui-même.
    public static List<(Action action, string chemin)> Plan(IDictionary<string, string> local, IDictionary<string, string> manifeste,
                                                            IDictionary<string, Distant> serveur, bool telecharger)
    {
        var plan = new List<(Action, string)>();
        bool premier = manifeste.Count == 0;   // premier passage de ce dossier (import, dossier neuf)
        foreach (var c in local.Keys.Union(manifeste.Keys).Union(serveur.Keys).OrderBy(c => c, StringComparer.Ordinal))
        {
            local.TryGetValue(c, out var l);
            manifeste.TryGetValue(c, out var m);
            serveur.TryGetValue(c, out var s);
            bool vivant = s != null && !s.Supprime;
            if (l != null)
            {
                if (vivant && s.Empreinte == l) { if (m != l) plan.Add((Action.Noter, c)); }
                // Jamais synchronisé sur ce PC alors que le cloud le connaît : le cloud fait foi (la
                // copie d'ici part en corbeille avant d'être remplacée). Sauf un chemin supprimé au
                // cloud où ce PC, déjà synchronisé, a remis un fichier depuis : il repart (renommage
                // revenu à un ancien nom, rivoli → rivoli2 → rivoli ; retour du 23/10, il partait en corbeille).
                else if (m == null && s != null && (vivant || premier)) { if (telecharger) plan.Add((vivant ? Action.Telecharger : Action.Corbeille, c)); }
                else if (l != m) plan.Add((Action.Envoyer, c));                                // nouveau ou modifié ici : il l'emporte
                else if (vivant) { if (telecharger) plan.Add((Action.Telecharger, c)); }       // modifié par un collègue
                else if (s != null) { if (telecharger) plan.Add((Action.Corbeille, c)); }      // supprimé par un collègue
                else plan.Add((Action.Envoyer, c));                                             // le serveur ne l'a plus
            }
            else if (m != null)   // je l'avais : supprimé ici
            {
                if (vivant && s.Empreinte == m) plan.Add((Action.SupprimerServeur, c));
                else if (vivant && telecharger) plan.Add((Action.Telecharger, c));             // changé ailleurs entre-temps : sa version revient
            }
            else if (vivant && telecharger) plan.Add((Action.Telecharger, c));                 // ajouté par un collègue
        }
        return plan;
    }

    /// Rangement du 23/10 : la table `documents` lue comme si tout était déjà rangé. Une ligne
    /// vivante à l'ancien chemin est vue à son nouvel emplacement s'il est libre ; `aRanger`
    /// (ancien → nouveau) dit ce qu'il faut corriger au serveur après le passage : créer la ligne
    /// au nouveau chemin (nouveau non null), puis supprimer l'ancienne. Une ligne ancienne déjà
    /// supprimée est oubliée.
    public static Dictionary<string, Distant> Redresser(IEnumerable<(string chemin, string empreinte, bool supprime)> lignes,
                                                        out List<(string ancien, string nouveau)> aRanger)
    {
        var serveur = new Dictionary<string, Distant>();
        var anciens = new List<(string c, string n, string e)>();
        foreach (var (c, e, supprime) in lignes)
        {
            if (string.IsNullOrEmpty(c)) continue;
            string n = DossiersDonnees.Nouveau(c);
            if (n == c) { if (!serveur.ContainsKey(c)) serveur[c] = new Distant { Empreinte = e, Supprime = supprime }; }
            else if (!supprime) anciens.Add((c, n, e));
        }
        aRanger = new List<(string, string)>();
        foreach (var (c, n, e) in anciens)
        {
            bool libre = n != null && !serveur.ContainsKey(n);
            if (libre) serveur[n] = new Distant { Empreinte = e };
            aRanger.Add((c, libre ? n : null));
        }
        return serveur;
    }
}

/// Étape 6a : documents partagés, mode « Sur chaque PC » (décisions du 14/10). Toutes les 20 s,
/// et 2 s après qu'un collègue en a déposé un, CIPL compare Batiment/ et Logo/ du dossier de
/// l'entreprise avec la table `documents` : il envoie ce qui est nouveau ou modifié ici, reçoit
/// ce que les collègues ont ajouté, range en corbeille ce qu'ils ont supprimé. Le contenu est
/// stocké sous son empreinte : un renommage de bâtiment ne renvoie aucun fichier.
public static class DocumentsCloud
{
    const string Compartiment = "documents";
    const float Periode = 20f;

    [Serializable] class Fichier { public string c, e, m; public long t, d; }   // chemin, empreinte ici, empreinte au dernier passage, taille, date
    [Serializable] class Etat { public List<Fichier> fichiers = new List<Fichier>(); }
    [Serializable] class LigneDoc { public string chemin, empreinte, supprime_le; public long taille; }
    [Serializable] class LignesDoc { public LigneDoc[] items; }
    [Serializable] class ParamsDoc { public string p_entreprise, p_chemin; public long p_taille; public string p_empreinte, p_session; }
    [Serializable] class ParamsSuppr { public string p_entreprise, p_chemin, p_session; }

    static string _entreprise, _racine;
    static Etat _etat;
    static bool _enCours, _premier, _telecharger = true;
    static float _prochain, _bientot = -1;
    static int _generation;

    /// `telecharger` faux (étape 6b) : dossier dans un Drive partagé, qui recopie et supprime lui-même ;
    /// ce PC n'en fait qu'envoyer ce qui change ici.
    public static void Demarrer(string entrepriseId, string racine, bool telecharger)
    {
        Arreter();
        _entreprise = entrepriseId;
        _racine = racine;
        _telecharger = telecharger;
        _premier = true;
        _etat = Charger();
        Ranger(racine, _etat);
        _prochain = 0;   // premier passage tout de suite (il envoie les documents déjà sur ce PC)
    }

    /// Rangement du 23/10, avant le premier passage : les documents de ce PC rangés à l'ancienne
    /// rejoignent leur place, et l'état du dernier passage les suit (sinon ils seraient vus
    /// supprimés ici puis renvoyés).
    static void Ranger(string racine, Etat etat)
    {
        var faits = RangerIci(racine);
        if (faits.Count == 0) return;
        foreach (var f in etat.fichiers)
            if (f?.c != null && faits.TryGetValue(f.c, out var n) && n != null) f.c = n;
        Sauver(etat);
        Debug.Log($"[Documents] Rangement : {faits.Count} document(s) déplacé(s) ou retiré(s)");
    }

    /// Déplace chaque fichier de Batiment/ rangé à l'ancienne (DossiersDonnees.Nouveau), retire les
    /// aperçus ; renvoie ancien → nouveau (null : retiré). Un fichier différent déjà à l'arrivée
    /// n'est pas écrasé : l'ancien devient « x (2) ». Un fichier bloqué attend l'ouverture suivante.
    public static Dictionary<string, string> RangerIci(string racine)
    {
        var faits = new Dictionary<string, string>();
        string bat = Path.Combine(racine, "Batiment");
        if (!Directory.Exists(bat)) return faits;
        foreach (var abs in Directory.GetFiles(bat, "*", SearchOption.AllDirectories))
        {
            string c = abs.Substring(racine.Length).TrimStart('\\', '/').Replace('\\', '/');
            string n = DossiersDonnees.Nouveau(c);
            if (n == c) continue;
            try
            {
                if (n == null) { File.Delete(abs); faits[c] = null; continue; }
                string dest = Absolu(racine, n);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest) && SyncDocuments.Empreinte(File.ReadAllBytes(dest)) == SyncDocuments.Empreinte(File.ReadAllBytes(abs)))
                    File.Delete(abs);   // déjà à sa place (recopié par un Drive)
                else
                {
                    string nom = Path.GetFileNameWithoutExtension(dest), ext = Path.GetExtension(dest), dir = Path.GetDirectoryName(dest);
                    for (int i = 2; File.Exists(dest); i++) dest = Path.Combine(dir, $"{nom} ({i}){ext}");
                    File.Move(abs, dest);
                }
                faits[c] = Relatif(racine, dest);
            }
            catch (Exception e) { Debug.LogWarning($"[Documents] Rangement de {c} : {e.Message}"); }
        }
        if (faits.Count > 0)   // dossiers vidés, les plus profonds d'abord
            foreach (var d in Directory.GetDirectories(bat, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                try { if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch (Exception) { }
        return faits;
    }

    public static void Arreter()
    {
        _generation++;
        _entreprise = _racine = null;
        _etat = null;
        _enCours = false;
    }

    /// Un collègue vient de déposer ou de supprimer un document : passage dans 2 s.
    public static void Bientot() => _bientot = Time.realtimeSinceStartup + 2f;

    public static void Tic(float maintenant)
    {
        if (_entreprise == null || _enCours || !SessionCloud.Active) return;
        if (maintenant < _prochain && (_bientot < 0 || maintenant < _bientot)) return;
        _prochain = maintenant + Periode;
        _bientot = -1;
        Passer();
    }

    static async void Passer()
    {
        int generation = _generation;
        string ent = _entreprise, racine = _racine;
        _enCours = true;
        try
        {
            var connus = _etat.fichiers.Where(f => f?.c != null).GroupBy(f => f.c).ToDictionary(g => g.Key, g => g.First());
            var locaux = await Task.Run(() => Lister(racine, connus));
            // ponytail: une seule lecture, plafonnée à 1 000 lignes par Supabase ; paginer au-delà.
            var r = await CloudClient.Table("GET", SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "documents",
                                            $"entreprise_id=eq.{ent}&select=chemin,empreinte,supprime_le,taille");
            if (generation != _generation) return;
            if (!r.Ok) { Debug.LogWarning("[Documents] Lecture de la liste : " + r.Erreur); return; }
            var lignes = (JsonUtility.FromJson<LignesDoc>("{\"items\":" + r.Corps + "}")?.items ?? new LigneDoc[0])
                .Where(l => !string.IsNullOrEmpty(l.chemin)).ToList();
            var serveur = SyncDocuments.Redresser(lignes.Select(l => (l.chemin, l.empreinte, !string.IsNullOrEmpty(l.supprime_le))),
                                                  out var aRanger);

            var plan = SyncDocuments.Plan(locaux.ToDictionary(kv => kv.Key, kv => kv.Value.e),
                                          connus.Where(kv => kv.Value.m != null).ToDictionary(kv => kv.Key, kv => kv.Value.m),
                                          serveur, _telecharger);

            // Nouvel état : les fichiers d'ici, plus ceux disparus d'ici dont la suppression reste à transmettre.
            var etat = new Dictionary<string, Fichier>(locaux);
            foreach (var kv in connus)
                if (!etat.ContainsKey(kv.Key) && kv.Value.m != null) etat[kv.Key] = new Fichier { c = kv.Key, m = kv.Value.m };

            int envoyes = 0, recus = 0, corbeille = 0;
            foreach (var (action, c) in plan)
            {
                if (generation != _generation) return;
                switch (action)
                {
                    case SyncDocuments.Action.Noter:
                        etat[c].m = etat[c].e;
                        break;
                    case SyncDocuments.Action.Envoyer:
                        if (await Envoyer(ent, racine, c, etat[c].e)) { etat[c].m = etat[c].e; envoyes++; }
                        break;
                    case SyncDocuments.Action.Telecharger:
                        var recu = await Telecharger(ent, racine, c, serveur[c].Empreinte, etat.TryGetValue(c, out var ici) ? ici : null);
                        if (recu != null) { etat[c] = recu; recus++; }
                        break;
                    case SyncDocuments.Action.SupprimerServeur:
                        if (await SupprimerServeur(ent, c)) etat.Remove(c);
                        break;
                    case SyncDocuments.Action.Corbeille:
                        if (MettreEnCorbeille(racine, c)) { etat.Remove(c); corbeille++; }
                        break;
                }
            }
            // Lignes à l'ancien chemin (rangement du 23/10) : la nouvelle, puis l'ancienne supprimée.
            // Le contenu est déjà stocké sous son empreinte : rien n'est renvoyé.
            foreach (var (ancien, nouveau) in aRanger)
            {
                if (generation != _generation) return;
                var ligne = lignes.First(x => x.chemin == ancien);
                if (nouveau == null || await Documenter(ent, nouveau, ligne.taille, ligne.empreinte)) await SupprimerServeur(ent, ancien);
            }
            if (aRanger.Count > 0) Debug.Log($"[Documents] Rangement au cloud : {aRanger.Count} chemin(s) corrigé(s)");

            // Disparus d'ici et plus rien à transmettre (supprimés au serveur, ou inconnus) : oubliés.
            foreach (var c in etat.Where(kv => kv.Value.e == null && (!serveur.TryGetValue(kv.Key, out var s) || s.Supprime))
                                  .Select(kv => kv.Key).ToList())
                etat.Remove(c);

            if (generation != _generation) return;   // entreprise changée pendant le dernier envoi
            _etat = new Etat { fichiers = etat.Values.ToList() };
            Sauver(_etat);

            if (envoyes + recus + corbeille > 0)
                Debug.Log($"[Documents] {envoyes} envoyé(s), {recus} reçu(s), {corbeille} rangé(s) en corbeille");
            if (recus > 0 || corbeille > 0)
                UndoToast.Instance?.ShowInfo(
                    (recus > 0 ? $"{recus} document(s) reçu(s) de vos collègues" : "")
                    + (recus > 0 && corbeille > 0 ? ", " : "")
                    + (corbeille > 0 ? $"{corbeille} supprimé(s) par un collègue, rangé(s) dans la corbeille" : "") + ".");
            else if (_premier && envoyes > 0)
                UndoToast.Instance?.ShowInfo($"{envoyes} document(s) de ce PC envoyé(s) au cloud.");
            _premier = false;
        }
        catch (Exception e) { Debug.LogWarning("[Documents] Passage interrompu : " + e.Message); }
        finally { if (generation == _generation) _enCours = false; }
    }

    // ── Ouvrir un document (étape 6b) ──────────────────────────────────────────

    /// Ouvre un document de l'entreprise. Absent de ce PC (le Drive ne l'a pas encore recopié) :
    /// lu depuis le cloud dans un fichier temporaire (§8). Hors cloud : comme avant.
    public static async void Ouvrir(string absolu)
    {
        if (string.IsNullOrEmpty(absolu)) return;
        string nom = Path.GetFileName(absolu);
        if (File.Exists(absolu)) { Application.OpenURL("file:///" + absolu.Replace('\\', '/')); return; }
        if (!SessionCloud.Active || SessionCloud.Entreprise == null) { ConfirmDialog.Erreur($"« {nom} » est introuvable."); return; }
        try
        {
            string chemin = Relatif(SaveLocationService.GetSaveRoot(), absolu);
            var e = SessionCloud.Entreprise;
            string jeton = await SessionCloud.Jeton();
            var r = chemin == null ? null : await CloudClient.Table("GET", SessionCloud.Url, SessionCloud.Cle, jeton, "documents",
                $"entreprise_id=eq.{e.id}&chemin=eq.{Uri.EscapeDataString(chemin)}&select=chemin,empreinte,supprime_le");
            var ligne = r != null && r.Ok ? JsonUtility.FromJson<LignesDoc>("{\"items\":" + r.Corps + "}")?.items?.FirstOrDefault() : null;
            if (ligne == null || !string.IsNullOrEmpty(ligne.supprime_le))
            {
                ConfirmDialog.Erreur($"« {nom} » n'est ni sur cet ordinateur ni sur le cloud.");
                return;
            }
            var (contenu, erreur) = await CloudClient.LireFichier(SessionCloud.Url, SessionCloud.Cle, jeton, Compartiment, e.id + "/" + ligne.empreinte);
            if (contenu == null) { ConfirmDialog.Erreur($"« {nom} » : lecture depuis le cloud impossible ({erreur})."); return; }
            string tmp = Path.Combine(Path.GetTempPath(), "CIPL", ligne.empreinte.Substring(0, 8) + "_" + nom);
            Directory.CreateDirectory(Path.GetDirectoryName(tmp));
            File.WriteAllBytes(tmp, contenu);
            UndoToast.Instance?.ShowInfo($"« {nom} » lu depuis le cloud : votre Drive ne l'a pas encore recopié.");
            Application.OpenURL("file:///" + tmp.Replace('\\', '/'));
        }
        catch (Exception ex) { ConfirmDialog.Erreur($"« {nom} » : ouverture impossible ({ex.Message})."); }
    }

    /// Chemin relatif au dossier de l'entreprise (« Batiment/rivoli/… »), null s'il est ailleurs.
    public static string Relatif(string racine, string absolu)
    {
        string r = Path.GetFullPath(racine).TrimEnd('\\', '/'), a = Path.GetFullPath(absolu);
        return a.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? a.Substring(r.Length + 1).Replace('\\', '/') : null;
    }

    // ── Fichiers de ce PC ──────────────────────────────────────────────────────

    static string Absolu(string racine, string chemin) => Path.Combine(racine, chemin.Replace('/', Path.DirectorySeparatorChar));

    /// Fichiers partagés de ce PC ; l'empreinte n'est recalculée que si la taille ou la date a changé.
    static Dictionary<string, Fichier> Lister(string racine, Dictionary<string, Fichier> connus)
    {
        var res = new Dictionary<string, Fichier>();
        foreach (var sous in SyncDocuments.Racines)
        {
            string dossier = Path.Combine(racine, sous);
            if (!Directory.Exists(dossier)) continue;
            foreach (var abs in Directory.EnumerateFiles(dossier, "*", SearchOption.AllDirectories))
            {
                string c = abs.Substring(racine.Length).TrimStart('\\', '/').Replace('\\', '/');
                // Encore rangé à l'ancienne (bloqué à l'ouverture) : ignoré jusqu'au rangement suivant.
                if (!SyncDocuments.Partage(c) || DossiersDonnees.Nouveau(c) != c) continue;
                try
                {
                    var fi = new FileInfo(abs);
                    long d = fi.LastWriteTimeUtc.Ticks;
                    connus.TryGetValue(c, out var k);
                    string e = k != null && k.e != null && k.t == fi.Length && k.d == d ? k.e : SyncDocuments.Empreinte(File.ReadAllBytes(abs));
                    res[c] = new Fichier { c = c, e = e, m = k?.m, t = fi.Length, d = d };
                }
                catch (Exception) { /* ouvert ou en cours d'écriture : au prochain passage */ }
            }
        }
        return res;
    }

    /// Rangé, jamais effacé (décision du 09/10) : corbeille_documents/<date>/<chemin>.
    static bool MettreEnCorbeille(string racine, string chemin)
    {
        try
        {
            string abs = Absolu(racine, chemin);
            if (!File.Exists(abs)) return true;
            string dest = Path.Combine(racine, "corbeille_documents", DateTime.Now.ToString("yyyy-MM-dd"),
                                       chemin.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            string nom = Path.GetFileNameWithoutExtension(dest), ext = Path.GetExtension(dest), dir = Path.GetDirectoryName(dest);
            for (int i = 2; File.Exists(dest); i++) dest = Path.Combine(dir, $"{nom} ({i}){ext}");
            File.Move(abs, dest);
            return true;
        }
        catch (Exception e) { Debug.LogWarning($"[Documents] Corbeille de {chemin} : {e.Message}"); return false; }
    }

    // ── Réseau ─────────────────────────────────────────────────────────────────

    static async Task<bool> Envoyer(string ent, string racine, string chemin, string empreinte)
    {
        byte[] contenu;
        try { contenu = File.ReadAllBytes(Absolu(racine, chemin)); }
        catch (Exception) { return false; }
        if (SyncDocuments.Empreinte(contenu) != empreinte) return false;   // modifié depuis la liste : au prochain passage
        var r = await CloudClient.DeposerFichier(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), Compartiment, ent + "/" + empreinte, contenu);
        if (!r.Ok) { Debug.LogWarning($"[Documents] Envoi de {chemin} : {r.Erreur}"); return false; }
        return await Documenter(ent, chemin, contenu.LongLength, empreinte);
    }

    static async Task<bool> Documenter(string ent, string chemin, long taille, string empreinte)
    {
        var d = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "documenter", new ParamsDoc
        {
            p_entreprise = ent, p_chemin = chemin, p_taille = taille, p_empreinte = empreinte, p_session = EcritureCloud.SessionId,
        });
        if (!d.Ok) Debug.LogWarning($"[Documents] Index de {chemin} : {d.Erreur}");
        return d.Ok;
    }

    static async Task<Fichier> Telecharger(string ent, string racine, string chemin, string empreinte, Fichier ici)
    {
        var (contenu, erreur) = await CloudClient.LireFichier(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(),
                                                              Compartiment, ent + "/" + empreinte);
        if (contenu == null) { Debug.LogWarning($"[Documents] Réception de {chemin} : {erreur}"); return null; }
        if (SyncDocuments.Empreinte(contenu) != empreinte) { Debug.LogWarning($"[Documents] {chemin} : contenu reçu altéré"); return null; }
        string abs = Absolu(racine, chemin);
        try
        {
            // Une copie d'ici jamais synchronisée part en corbeille avant d'être remplacée : rien n'est perdu.
            if (File.Exists(abs) && ici?.m == null && !MettreEnCorbeille(racine, chemin)) return null;
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            string tmp = abs + ".cipl-recu";
            File.WriteAllBytes(tmp, contenu);
            if (File.Exists(abs)) File.Replace(tmp, abs, null); else File.Move(tmp, abs);
            var fi = new FileInfo(abs);
            return new Fichier { c = chemin, e = empreinte, m = empreinte, t = fi.Length, d = fi.LastWriteTimeUtc.Ticks };
        }
        catch (Exception e) { Debug.LogWarning($"[Documents] Écriture de {chemin} : {e.Message}"); return null; }
    }

    static async Task<bool> SupprimerServeur(string ent, string chemin)
    {
        var r = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "supprimer_document",
            new ParamsSuppr { p_entreprise = ent, p_chemin = chemin, p_session = EcritureCloud.SessionId });
        if (!r.Ok) Debug.LogWarning($"[Documents] Suppression de {chemin} : {r.Erreur}");
        return r.Ok;
    }

    // ── État du dernier passage ────────────────────────────────────────────────

    /// Par entreprise ET par dossier : l'éditeur et un build sur le même PC partagent
    /// persistentDataPath, mais pas leur dossier de documents.
    static string CheminEtat() => Path.Combine(Application.persistentDataPath,
        $"cipl_docs_{_entreprise}_{SyncDocuments.Empreinte(Encoding.UTF8.GetBytes(_racine.ToLowerInvariant())).Substring(0, 12)}.json");

    static Etat Charger()
    {
        try
        {
            string p = CheminEtat();
            return File.Exists(p) ? JsonUtility.FromJson<Etat>(File.ReadAllText(p)) ?? new Etat() : new Etat();
        }
        catch (Exception) { return new Etat(); }
    }

    static void Sauver(Etat e)
    {
        if (!AtomicFile.WriteAllText(CheminEtat(), JsonUtility.ToJson(e), out string erreur))
            Debug.LogWarning("[Documents] État non enregistré : " + erreur);
    }
}
