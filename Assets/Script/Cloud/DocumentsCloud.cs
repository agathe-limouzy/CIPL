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
    /// `Indexer` (Revue 17, D2-26, lot D) : la ligne `documents` seulement ; le contenu est déjà au cloud sous cette empreinte.
    public enum Action { Noter, Envoyer, Telecharger, SupprimerServeur, Corbeille, Indexer }

    /// Les dossiers partagés de l'entreprise ; « Anciens documents » depuis le rangement du 23/10.
    public static readonly string[] Racines = { "Batiment", "Logo", DossiersDonnees.ANCIENS };

    /// Un chemin venu du serveur qu'on peut écrire sans sortir du dossier de l'entreprise : partagé, relatif, sans
    /// segment « . » ou « .. », ni « \ » ni « : » (revue 16c, D1-01 : `Path.Combine` ignore la racine devant un chemin
    /// complet et laisse « .. » remonter ; une ligne `documents` mal formée s'écrivait ailleurs sur les PC des collègues).
    public static bool CheminSur(string chemin)
    {
        if (!Partage(chemin) || chemin.IndexOfAny(new[] { '\\', ':', '\0' }) >= 0 || chemin.StartsWith("/")) return false;
        return !chemin.Split('/').Any(s => s == "." || s == ".." || s.Length == 0);
    }

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
    /// Retour du 01/11 (BF15) : des fichiers connus et plus aucun fichier local = le dossier a bougé (renommé, Drive fermé,
    /// recréé vide par une écriture), pas les fichiers : la passe attend, rien n'est supprimé.
    public static bool DossierDeplace(int locaux, int connus) => connus > 0 && locaux == 0;

    /// Fusible : plus de 5 suppressions d'un coup et plus de la moitié des fichiers connus, ce n'est pas une suppression
    /// voulue (l'application supprime un document à la fois) : refusées, journalisées.
    public static bool SuppressionsSuspectes(int aSupprimer, int connus) => aSupprimer > 5 && aSupprimer * 2 > connus;

    // ── Revue 17 (D2-02, lot D) : retraits voulus ──────────────────────────────
    // Renommer ou supprimer un bâtiment (ou un locataire) dans CIPL déplace tout son dossier : ses fichiers sont vus
    // « supprimés ici ». Le fusible ne distinguait pas ce geste d'un dossier disparu : dès que le dossier portait plus de
    // la moitié des documents, il refusait ces suppressions pour toujours (doublons chez les collègues). CIPL annonce
    // donc le dossier qu'il retire lui-même (DocumentsCloud.RetraitVoulu) ; ce qui est dessous n'est ni compté par le
    // fusible ni tenu pour « disparu ». Un dossier disparu SANS annonce reste bloqué, comme avant.

    /// Le préfixe (« Batiment/rivoli/ ») d'un dossier retiré, donné par son chemin relatif ; null s'il n'est pas un
    /// sous-dossier d'un dossier partagé : jamais « Batiment » entier ni la racine, qui désarmeraient le fusible.
    public static string PrefixeRetrait(string relatif)
    {
        string r = (relatif ?? "").TrimEnd('/');
        return r.Contains("/") && CheminSur(r + "/x") ? r + "/" : null;
    }

    /// Ce chemin est sous un retrait voulu (sans tenir compte de la casse, comme le disque).
    public static bool SousRetrait(string chemin, IEnumerable<string> retraits) =>
        chemin != null && retraits != null && retraits.Any(r => chemin.StartsWith(r, StringComparison.OrdinalIgnoreCase));

    /// Les suppressions du plan que le fusible doit contrôler : toutes, sauf celles sous un retrait voulu.
    public static int SuppressionsAControler(IEnumerable<(Action action, string chemin)> plan, IEnumerable<string> retraits) =>
        plan.Count(p => p.action == Action.SupprimerServeur && !SousRetrait(p.chemin, retraits));

    /// Revue 17 (D1-09) : une écriture de Logo/ au serveur (envoi, suppression), que documenter et supprimer_document
    /// réservent à l'admin : un poste « utilisateur » ne la tente pas (sinon un refus à chaque passage) ; il reçoit seulement.
    public static bool ReserveAdmin(Action action, string chemin) =>
        (action == Action.Envoyer || action == Action.Indexer || action == Action.SupprimerServeur) && chemin.StartsWith("Logo/", StringComparison.Ordinal);

    /// `dejaPasse` (Revue 17, D2-02, lot D) : un passage complet a déjà eu lieu depuis l'ouverture de l'entreprise. Un manifeste
    /// vide n'est alors pas un premier passage, mais un dossier dont tout a été retiré (le seul bâtiment supprimé, ses
    /// suppressions transmises) : si « Annuler » le remet, ses fichiers repartent au lieu d'aller en corbeille.
    public static List<(Action action, string chemin)> Plan(IDictionary<string, string> local, IDictionary<string, string> manifeste,
                                                            IDictionary<string, Distant> serveur, bool telecharger, bool dejaPasse = false)
    {
        var plan = new List<(Action, string)>();
        bool premier = manifeste.Count == 0 && !dejaPasse;   // premier passage de ce dossier (import, dossier neuf)
        // Revue 17 (D2-26, lot D) : un contenu que le serveur a déjà, vivant, sous un autre chemin (dossier renommé) n'est pas
        // renvoyé : le stockage est rangé par empreinte (<entreprise>/<empreinte>), la ligne `documents` suffit. Avant, chaque
        // fichier d'un dossier renommé était relu et renvoyé en entier, pour être refusé « existe déjà ».
        var vivantes = new HashSet<string>(serveur.Values.Where(d => d != null && !d.Supprime && d.Empreinte != null).Select(d => d.Empreinte));
        Action Depot(string empreinte) => vivantes.Contains(empreinte) ? Action.Indexer : Action.Envoyer;
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
                else if (l != m) plan.Add((Depot(l), c));                                      // nouveau ou modifié ici : il l'emporte
                else if (vivant) { if (telecharger) plan.Add((Action.Telecharger, c)); }       // modifié par un collègue
                else if (s != null) { if (telecharger) plan.Add((Action.Corbeille, c)); }      // supprimé par un collègue
                else plan.Add((Depot(l), c));                                                   // le serveur ne l'a plus
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
/// stocké sous son empreinte : un renommage de bâtiment ne renvoie aucun fichier (vrai depuis la Revue 17, D2-26 :
/// action « Indexer »).
public static class DocumentsCloud
{
    const string Compartiment = "documents";
    const float Periode = 20f;
    // Revue 17 (D9-11, lot E ; décision du 16/11) : temps réel connecté, la liste du serveur n'est relue que toutes les 120 s (le
    // direct annonce déjà les documents des collègues : Bientot). Toutes les 20 s, un passage léger regarde seulement ce dossier
    // (en tâche de fond) et ne va au serveur que s'il y a quelque chose à envoyer ou à supprimer.
    const float PeriodeEnDirect = 120f;
    static float _dernierComplet = float.MinValue;

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

    // Revue 17 (D2-02, lot D) : les dossiers que CIPL vient de retirer lui-même (préfixes « Batiment/rivoli/ »), en mémoire
    // seulement, et l'entreprise + le dossier pour lesquels ils valent.
    static readonly List<string> _retraits = new List<string>();
    static string _retraitsPour;
    // Revue 17 (D2-07, lot D) : la synchronisation est suspendue (dossier vide ou introuvable) ; l'issue n'est proposée qu'une
    // fois par session (pour cette entreprise et ce dossier).
    static bool _suspendue;
    static string _issueProposeePour;
    static int _suspensionsDeSuite;   // passages suspendus de suite (remis à 0 par un passage qui franchit les gardes)

    /// `telecharger` faux (étape 6b) : dossier dans un Drive partagé, qui recopie et supprime lui-même ;
    /// ce PC n'en fait qu'envoyer ce qui change ici.
    public static void Demarrer(string entrepriseId, string racine, bool telecharger)
    {
        Arreter();
        // Revue 17 (D2-02, lot D) : un retrait voulu ne survit pas à un changement d'entreprise (ni de dossier) ; il survit à
        // un simple rechargement de la même entreprise, sinon le fusible bloquerait de nouveau un renommage pas encore transmis.
        if (_retraitsPour != entrepriseId + "|" + racine) _retraits.Clear();
        _suspendue = false;
        _suspensionsDeSuite = 0;
        _entreprise = entrepriseId;
        _racine = racine;
        _telecharger = telecharger;
        _premier = true;
        _etat = Charger();
        Ranger(racine, _etat);
        _prochain = 0;   // premier passage tout de suite (il envoie les documents déjà sur ce PC)
        _dernierComplet = float.MinValue;
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

    /// Un collègue vient de déposer ou de supprimer un document, ou un document vient d'être écrit sur ce PC (Revue 17,
    /// D2-03 : facture, photo, bail) : passage dans 2 s.
    public static void Bientot() => _bientot = Time.realtimeSinceStartup + 2f;

    /// Revue 17 (D2-02, lot D) : CIPL va déplacer ou renommer ce dossier de documents par un geste voulu (renommage ou
    /// suppression d'un bâtiment, d'un locataire). À appeler juste avant ou, mieux, juste APRÈS le déplacement réussi, sans
    /// attente entre les deux (Passer relit les retraits sur le fil principal, après la liste des fichiers) : un déplacement
    /// raté n'annonce alors rien et la garde du 01/11 reste entière pour ce dossier. Les
    /// suppressions sous ce dossier ne comptent plus dans le fusible ; le retrait est oublié à la fin du premier passage
    /// complet qui n'a plus rien à transmettre sous lui. Sans effet hors cloud, ou hors d'un sous-dossier partagé.
    public static void RetraitVoulu(string dossierAbsolu)
    {
        if (_racine == null || string.IsNullOrEmpty(dossierAbsolu)) return;
        string prefixe;
        try { prefixe = SyncDocuments.PrefixeRetrait(Relatif(_racine, dossierAbsolu)); }
        catch (Exception) { return; }   // chemin mal formé
        if (prefixe == null) return;
        if (!_retraits.Contains(prefixe)) _retraits.Add(prefixe);
        _retraitsPour = _entreprise + "|" + _racine;
    }

    /// Revue 17 (D2-03, lot D) : à la fermeture, un dernier passage (le document écrit à l'instant part au cloud au lieu
    /// d'attendre le prochain lancement de ce poste), borné à 15 s, puis `ensuite`, exactement une fois : aussi en cas
    /// d'erreur, et tout de suite si la synchronisation n'est pas démarrée, hors cloud ou hors ligne.
    public static async void Terminer(Action ensuite)
    {
        try
        {
            if (_entreprise != null && SessionCloud.Active && !HorsLigne.Actif)
            {
                // Le message « Envoi des documents… » est affiché par l'appelant (FermetureGuard), une seule fois. CIPL se ferme :
                // pas de fenêtre « Documents introuvables » (D2-07) à cet instant, elle serait aussitôt emportée.
                _issueProposeePour = _entreprise + "|" + _racine;
                var limite = Task.Delay(15000);
                // Un passage déjà en cours a pu lister les fichiers avant le dernier document : on l'attend, puis on en fait un entier.
                while (_enCours && !limite.IsCompleted) await Task.WhenAny(Task.Delay(100), limite);
                if (_entreprise != null && !_enCours && !limite.IsCompleted)
                {
                    _bientot = -1;
                    await Task.WhenAny(Passer(), limite);
                }
            }
        }
        catch (Exception e) { Debug.LogWarning("[Documents] Dernier passage : " + e.Message); }
        ensuite?.Invoke();
    }

    public static void Tic(float maintenant)
    {
        if (_entreprise == null || _enCours || !SessionCloud.Active) return;
        if (maintenant < _prochain && (_bientot < 0 || maintenant < _bientot)) return;
        bool complet = (_bientot >= 0 && maintenant >= _bientot)
                       || maintenant >= _dernierComplet + (DirectCloud.Connecte ? PeriodeEnDirect : Periode);
        _prochain = maintenant + Periode;
        _bientot = -1;
        if (complet) _dernierComplet = maintenant;
        _ = Passer(complet);
    }

    /// Revue 17 (D2-07, lot D, décision du 11/11) : la synchronisation suspendue ne le disait qu'au journal, sans issue : un
    /// poste dont le dossier avait été vidé ne retéléchargeait jamais ses documents. Une fois par session, quand la
    /// suspension dure (IssueAProposer), une fenêtre à deux choix. Pas en mode Drive (il recopie lui-même) ; jamais
    /// par-dessus une autre fenêtre (elle la remplacerait) : elle sera proposée au passage suivant.
    static void Suspendre(string racine, int connus)
    {
        _suspendue = true;
        _suspensionsDeSuite++;
        var fenetre = ConfirmDialog.Instance;
        string ent = _entreprise, pour = _entreprise + "|" + racine;
        if (_issueProposeePour == pour || !IssueAProposer(_suspensionsDeSuite, connus, _telecharger)
            || fenetre == null || fenetre.gameObject.activeSelf) return;
        _issueProposeePour = pour;
        fenetre.Show("Documents introuvables", MessageSuspension(racine, connus), () => ToutRetelecharger(ent, racine),
                     "Tout retélécharger depuis le cloud", "Attendre");
    }

    /// Revue 17 (D2-07, reprise) : pas dès le premier passage suspendu. Un état passager (un PDF effacé puis réécrit par
    /// FacturePdfService, le seul fichier connu listé à cet instant) se résout au passage suivant, 20 s après ; la fenêtre
    /// ne vient qu'au deuxième passage suspendu de suite. Jamais en mode Drive (il recopie lui-même), ni sans fichier connu.
    public static bool IssueAProposer(int suspensionsDeSuite, int connus, bool telecharger) =>
        suspensionsDeSuite >= 2 && connus > 0 && telecharger;

    public static string MessageSuspension(string racine, int connus) =>
        $"Le dossier des documents de cette entreprise est vide ou introuvable :\n{racine}\n\n"
        + $"{connus} document(s) y étaient connus. La synchronisation des documents est suspendue sur ce PC ; rien n'est supprimé, "
        + "ni ici ni sur le cloud.\n\n"
        + "• « Attendre » : elle reprendra seule quand le dossier reviendra (disque rebranché, dossier remis à sa place).\n"
        + "• « Tout retélécharger depuis le cloud » : les documents du cloud sont recopiés dans ce dossier. Rien n'est supprimé sur le cloud.";

    /// Le second choix : l'état du dernier passage est remis à zéro. Le passage suivant est un premier passage (manifeste
    /// vide) : SyncDocuments.Plan ne supprime au serveur que ce que le manifeste connaît, il télécharge donc tout et ne
    /// supprime RIEN (test LotDDocumentsTests.D2_07).
    static void ToutRetelecharger(string ent, string racine)
    {
        if (ent == null || _entreprise != ent || _racine != racine || _etat == null) return;   // entreprise changée ou fermée depuis la fenêtre
        if (!_suspendue)
        {
            UndoToast.Instance?.ShowInfo("Le dossier des documents est revenu : la synchronisation a repris, rien à retélécharger.");
            return;
        }
        try { Directory.CreateDirectory(racine); }
        catch (Exception e)
        {
            ConfirmDialog.Erreur($"Le dossier des documents ({racine}) ne peut pas être recréé : {e.Message}\n\nLa synchronisation des documents reste suspendue.");
            return;
        }
        _generation++;      // un passage en vol, parti de l'ancien état, s'arrête (comme dans Arreter)
        _enCours = false;
        _etat = new Etat();
        Sauver(_etat);
        _premier = true;    // premier passage, au sens de SyncDocuments.Plan
        _retraits.Clear();
        _suspendue = false;
        _suspensionsDeSuite = 0;
        _prochain = 0;      // passage tout de suite
        _dernierComplet = float.MinValue;
        UndoToast.Instance?.ShowInfo("Les documents sont retéléchargés depuis le cloud…");
    }

    /// Revue 17 (D9-11, lot E) : quelque chose de ce dossier est à transmettre (fichier nouveau ou changé depuis le dernier
    /// passage, fichier connu disparu).
    static bool ChangementLocal(Dictionary<string, Fichier> locaux, Dictionary<string, Fichier> connus) =>
        locaux.Any(kv => !connus.TryGetValue(kv.Key, out var k) || k.m != kv.Value.e)
        || connus.Any(kv => kv.Value.m != null && !locaux.ContainsKey(kv.Key));

    /// `complet` faux (Revue 17, D9-11) : passage léger, qui s'arrête après la liste de ce dossier si rien n'est à transmettre.
    static async Task Passer(bool complet = true)
    {
        int generation = _generation;
        string ent = _entreprise, racine = _racine;
        _enCours = true;
        try
        {
            // Revue 17 (D2-02, lot D) : seuls les retraits annoncés AVANT ce passage seront oubliés à sa fin (un retrait annoncé
            // pendant la liste des fichiers peut ne pas y être encore visible).
            var retraitsAuDepart = _retraits.ToList();
            // Retour du 01/11 (parcours BF15) : un dossier de l'entreprise introuvable (Drive fermé, clé débranchée, dossier
            // renommé) serait lu comme « tous les fichiers supprimés ici », et le plan supprimerait TOUS les documents sur le
            // cloud, puis chez les collègues. La synchronisation attend que le dossier revienne ; rien n'est supprimé.
            if (!Directory.Exists(racine))
            {
                Debug.LogWarning($"[Documents] Dossier des documents introuvable ({racine}) : synchronisation suspendue, rien n'est supprimé.");
                Suspendre(racine, _etat.fichiers.Count(f => f?.c != null));
                return;
            }
            var connus = _etat.fichiers.Where(f => f?.c != null).GroupBy(f => f.c).ToDictionary(g => g.Key, g => g.First());
            var locaux = await Task.Run(() => Lister(racine, connus));
            if (generation != _generation) return;
            // Revue 17 (D2-02, lot D) : les fichiers connus sous un retrait voulu ne sont pas « disparus » : ils ne comptent ni
            // pour le dossier déplacé (supprimer le seul bâtiment qui a des documents ne suspend pas tout), ni pour le fusible.
            // Sans retrait annoncé, les deux gardes sont exactement celles d'avant.
            var retraits = _retraits.ToList();
            int aSurveiller = connus.Keys.Count(c => !SyncDocuments.SousRetrait(c, retraits));
            if (SyncDocuments.DossierDeplace(locaux.Count, aSurveiller))
            {
                Debug.LogWarning($"[Documents] Aucun fichier sous {racine} alors que {aSurveiller} étaient connus : dossier déplacé ou vide, "
                                 + "synchronisation suspendue, rien n'est supprimé.");
                Suspendre(racine, aSurveiller);
                return;
            }
            _suspendue = false;
            _suspensionsDeSuite = 0;
            if (!complet && retraits.Count == 0 && !ChangementLocal(locaux, connus)) return;   // Revue 17 (D9-11, lot E)
            // Revue 17 (D2-06, lot D) : toute la table, page après page, dans l'ordre de sa clé (entreprise_id, chemin) ; une page
            // en échec = pas de passage (jamais de liste partielle : un fichier absent de la liste serait renvoyé, et une
            // suppression faite par un collègue serait défaite).
            var r = await CloudClient.TableTout(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "documents",
                                                $"entreprise_id=eq.{ent}&select=chemin,empreinte,supprime_le,taille&order=chemin.asc");
            if (generation != _generation) return;
            if (!r.Ok) { Debug.LogWarning("[Documents] Lecture de la liste : " + r.Erreur); return; }
            var lignes = (JsonUtility.FromJson<LignesDoc>("{\"items\":" + r.Corps + "}")?.items ?? new LigneDoc[0])
                .Where(l => SyncDocuments.CheminSur(l.chemin)).ToList();   // D1-01 : jamais hors du dossier
            var serveur = SyncDocuments.Redresser(lignes.Select(l => (l.chemin, l.empreinte, !string.IsNullOrEmpty(l.supprime_le))),
                                                  out var aRanger);

            var plan = SyncDocuments.Plan(locaux.ToDictionary(kv => kv.Key, kv => kv.Value.e),
                                          connus.Where(kv => kv.Value.m != null).ToDictionary(kv => kv.Key, kv => kv.Value.m),
                                          serveur, _telecharger, dejaPasse: !_premier);
            // Revue 17 (D2-02, lot D) : le fusible ne juge que ce qui n'est pas sous un retrait voulu, suppressions comme
            // fichiers connus ; les suppressions voulues partent même s'il saute pour les autres.
            int aSupprimer = SyncDocuments.SuppressionsAControler(plan, retraits);
            if (SyncDocuments.SuppressionsSuspectes(aSupprimer, aSurveiller))
            {
                Debug.LogWarning($"[Documents] {aSupprimer} suppressions d'un coup sur {aSurveiller} fichiers connus : refusées "
                                 + "(dossier déplacé ?). Un document se supprime depuis l'application, un par un.");
                plan.RemoveAll(p => p.action == SyncDocuments.Action.SupprimerServeur && !SyncDocuments.SousRetrait(p.chemin, retraits));
            }
            // Revue 17 (D1-09) : le logo est réservé à l'admin au serveur : un poste « utilisateur » ne l'envoie ni ne le supprime.
            if (SessionCloud.Entreprise?.EstAdmin != true) plan.RemoveAll(p => SyncDocuments.ReserveAdmin(p.action, p.chemin));

            // Nouvel état : les fichiers d'ici, plus ceux disparus d'ici dont la suppression reste à transmettre.
            var etat = new Dictionary<string, Fichier>(locaux);
            foreach (var kv in connus)
                if (!etat.ContainsKey(kv.Key) && kv.Value.m != null) etat[kv.Key] = new Fichier { c = kv.Key, m = kv.Value.m };

            int envoyes = 0, recus = 0, corbeille = 0;
            var nonTransmises = new List<string>();   // Revue 17 (D2-02) : suppressions que le serveur n'a pas prises
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
                    case SyncDocuments.Action.Indexer:   // Revue 17 (D2-26, lot D) : la ligne seulement, sans relire ni redéposer le contenu
                        if (await Documenter(ent, c, etat[c].t, etat[c].e)) { etat[c].m = etat[c].e; envoyes++; }
                        break;
                    case SyncDocuments.Action.Telecharger:
                        var recu = await Telecharger(ent, racine, c, serveur[c].Empreinte, etat.TryGetValue(c, out var ici) ? ici : null);
                        if (recu != null) { etat[c] = recu; recus++; }
                        break;
                    case SyncDocuments.Action.SupprimerServeur:
                        if (await SupprimerServeur(ent, c)) etat.Remove(c); else nonTransmises.Add(c);
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
            // Revue 17 (D2-02, lot D) : passage complet : un retrait annoncé avant lui est oublié, sauf s'il reste sous lui une
            // suppression que le serveur n'a pas prise (elle repartira au passage suivant).
            _retraits.RemoveAll(x => retraitsAuDepart.Contains(x) && !nonTransmises.Any(c => SyncDocuments.SousRetrait(c, new[] { x })));

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
        if (File.Exists(absolu)) { Application.OpenURL(Plateforme.UrlFichier(absolu)); return; }
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
            Application.OpenURL(Plateforme.UrlFichier(tmp));
        }
        catch (Exception ex) { ConfirmDialog.Erreur($"« {nom} » : ouverture impossible ({ex.Message})."); }
    }

    /// Le contenu d'un document de l'entreprise absent de ce PC, lu dans le cloud (revue 16c, D5-06 : la facture
    /// électronique émise sur un autre poste part aussi d'ici) ; null s'il n'y est pas non plus.
    public static async Task<byte[]> LireOctets(string absolu)
    {
        try
        {
            if (string.IsNullOrEmpty(absolu) || !SessionCloud.Active || SessionCloud.Entreprise == null) return null;
            string chemin = Relatif(SaveLocationService.GetSaveRoot(), absolu);
            if (chemin == null) return null;
            var e = SessionCloud.Entreprise;
            string jeton = await SessionCloud.Jeton();
            var r = await CloudClient.Table("GET", SessionCloud.Url, SessionCloud.Cle, jeton, "documents",
                $"entreprise_id=eq.{e.id}&chemin=eq.{Uri.EscapeDataString(chemin)}&select=chemin,empreinte,supprime_le");
            var ligne = r.Ok ? JsonUtility.FromJson<LignesDoc>("{\"items\":" + r.Corps + "}")?.items?.FirstOrDefault() : null;
            if (ligne == null || !string.IsNullOrEmpty(ligne.supprime_le)) return null;
            var (contenu, _) = await CloudClient.LireFichier(SessionCloud.Url, SessionCloud.Cle, jeton, Compartiment, e.id + "/" + ligne.empreinte);
            return contenu;
        }
        catch (Exception ex) { Debug.LogWarning("[Documents] Lecture dans le cloud : " + ex.Message); return null; }
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
                connus.TryGetValue(c, out var k);
                try
                {
                    var fi = new FileInfo(abs);
                    long d = fi.LastWriteTimeUtc.Ticks;
                    string e = k != null && k.e != null && k.t == fi.Length && k.d == d ? k.e : SyncDocuments.Empreinte(File.ReadAllBytes(abs));
                    res[c] = new Fichier { c = c, e = e, m = k?.m, t = fi.Length, d = d };
                }
                // Ouvert ou en cours d'écriture : vu inchangé jusqu'au prochain passage (revue 16c, D2-12 : absent de la
                // liste, un fichier connu passait pour supprimé ici, et l'était au serveur).
                catch (Exception) { if (k != null) res[c] = k; }
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
            RetirerDossiersVides(racine, Path.GetDirectoryName(abs));
            return true;
        }
        catch (Exception e) { Debug.LogWarning($"[Documents] Corbeille de {chemin} : {e.Message}"); return false; }
    }

    /// Revue 17 (D2-11, lot D) : les dossiers vidés par ce rangement partent aussi, du plus profond vers le haut (comme
    /// RangerIci) : chez les collègues, un bâtiment supprimé ou renommé laissait ses dossiers vides, et un renommage vers
    /// ce nom était ensuite refusé sur leur poste. Jamais un dossier racine (Batiment, Logo…), jamais un dossier non vide.
    public static void RetirerDossiersVides(string racine, string dossier)
    {
        try
        {
            for (string d = dossier; Relatif(racine, d)?.Contains("/") == true; d = Path.GetDirectoryName(d))
            {
                if (Directory.EnumerateFileSystemEntries(d).Any()) return;
                Directory.Delete(d);
            }
        }
        catch (Exception) { /* dossier ouvert ailleurs : il reste, vide ; DossiersDonnees le retirera au renommage */ }
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

    /// Transfert entre entreprises (01/11) : copie d'un document de l'entreprise ouverte vers `entDest` (même chemin,
    /// même empreinte), depuis la copie de ce PC, sinon depuis le cloud. Rend null, ou le motif de l'échec.
    public static async Task<string> CopierVers(string entDest, string chemin, string empreinte)
    {
        byte[] contenu = null;
        try { string abs = Absolu(_racine, chemin); if (File.Exists(abs)) contenu = File.ReadAllBytes(abs); } catch (Exception) { }
        if (contenu == null || SyncDocuments.Empreinte(contenu) != empreinte)
        {
            var (lu, erreur) = await CloudClient.LireFichier(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(),
                                                             Compartiment, _entreprise + "/" + empreinte);
            if (lu == null) return "lecture impossible (" + erreur + ")";
            contenu = lu;
        }
        var r = await CloudClient.DeposerFichier(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), Compartiment,
                                                 entDest + "/" + empreinte, contenu);
        if (!r.Ok) return "copie impossible (" + r.Erreur + ")";
        return await Documenter(entDest, chemin, contenu.LongLength, empreinte) ? null : "index impossible";
    }

    /// Après un transfert : la ligne d'origine est marquée supprimée (chaque poste range sa copie en corbeille).
    public static Task<bool> MarquerSupprime(string ent, string chemin) => SupprimerServeur(ent, chemin);

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
        // Revue 17 (D9-20 / D9-V1, lot E) : l'empreinte et l'écriture d'un document reçu se font en tâche de fond (une photo de
        // téléphone : ≈ 10 ms d'empreinte et 5 à 20 ms d'écriture, une saccade par fichier reçu pendant la première synchronisation).
        if (await Task.Run(() => SyncDocuments.Empreinte(contenu)) != empreinte) { Debug.LogWarning($"[Documents] {chemin} : contenu reçu altéré"); return null; }
        string abs = Absolu(racine, chemin);
        if (Relatif(racine, abs) == null) { Debug.LogWarning($"[Documents] {chemin} : hors du dossier de l'entreprise, ignoré"); return null; }
        try
        {
            // Une copie d'ici jamais synchronisée part en corbeille avant d'être remplacée : rien n'est perdu.
            if (File.Exists(abs) && ici?.m == null && !MettreEnCorbeille(racine, chemin)) return null;
            return await Task.Run(() =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(abs));
                string tmp = abs + ".cipl-recu";
                File.WriteAllBytes(tmp, contenu);
                if (File.Exists(abs)) File.Replace(tmp, abs, null); else File.Move(tmp, abs);
                var fi = new FileInfo(abs);
                return new Fichier { c = chemin, e = empreinte, m = empreinte, t = fi.Length, d = fi.LastWriteTimeUtc.Ticks };
            });
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

    // Revue 17 (D9-11, lot E) : l'état n'est réécrit que s'il a changé (avant : à chaque passage, toutes les 20 s).
    static string _sauve;

    static void Sauver(Etat e)
    {
        string chemin = CheminEtat(), json = JsonUtility.ToJson(e);
        if (_sauve == chemin + "\n" + json) return;
        if (!AtomicFile.WriteAllText(chemin, json, out string erreur)) { Debug.LogWarning("[Documents] État non enregistré : " + erreur); return; }
        _sauve = chemin + "\n" + json;
    }
}
