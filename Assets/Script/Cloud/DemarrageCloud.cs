using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// Démarrage de CIPL sur le cloud (étape 2) : connexion (ou reprise de la connexion gardée),
/// import des entreprises de ce PC si le cloud est vide (ou si un import est à reprendre),
/// choix de l'entreprise, puis chargement des bâtiments en LECTURE SEULE.
/// Voir FLOW_MULTIUTILISATEUR.drawio, page 1 « Après connexion ».
public static class DemarrageCloud
{
    static BatimentManager _bm;
    static Canvas Canvas => _bm != null ? _bm.GetComponentInParent<Canvas>() ?? UnityEngine.Object.FindObjectOfType<Canvas>() : null;
    static string ClePrefsDossier(string entrepriseId) => "cipl_dossier_" + entrepriseId;

    public static async void Lancer(BatimentManager bm)
    {
        _bm = bm;
        MiseAJour.Verifier(() => Canvas);   // étape 15 : en parallèle, sans retarder la connexion
        if (!SessionCloud.Configure)
        {
            // Revue 16c (D3-01) : le projet de CIPL est compilé (CloudDefaut) ; ce cas ne reste possible qu'en développement.
            ConfirmDialog.Erreur("CIPL n'est pas configuré pour le cloud : réinstallez-le. (Développement : menu CIPL › Essai cloud.)");
            return;
        }
        try
        {
            // Traces du démarrage (Player.log d'un build) : sans elles, un blocage ne disait pas où (14/10).
            Debug.Log("[Cloud] Démarrage : reprise de la connexion gardée…");
            if (!SessionCloud.Active && !await SessionCloud.Restaurer())
            {
                // Étape 8b : le serveur ne répond pas. Dernière copie de ce PC, en lecture seule.
                if (HorsLigne.Actif && await OuvrirCopie()) return;
                Debug.Log("[Cloud] Pas de connexion gardée : écran de connexion");
                await EcransCloud.Connexion(Canvas);
            }
            Debug.Log("[Cloud] Connecté(e) : " + SessionCloud.Session?.user?.email);
            await OuvrirUneEntreprise(forcerChoix: false);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            ConfirmDialog.Erreur("Démarrage sur le cloud impossible : " + e.Message);
        }
    }

    public static void MenuCompte() =>
        EcransCloud.MenuCompte(Canvas, ChangerEntreprise, SeDeconnecter);

    // Revue 17 (D2-04, lot D) : rien n'est arrêté avant d'avoir une entreprise à ouvrir. Avant, le direct (changements des collègues,
    // présence, documents) et les écrans étaient coupés dès le clic : sans réseau, la liste des entreprises ne se lisait pas, on restait
    // sur l'entreprise ouverte, modifiable, mais sans aucune synchronisation jusqu'au prochain lancement. C'est Ouvrir qui arrête et ferme.
    public static async void ChangerEntreprise()
    {
        // Revue 17 (D2-04, lot D) : le direct tourne pendant le choix ; un retrait par un admin (Exclu, puis ChangerEntreprise au OK)
        // ouvrait une seconde fenêtre de choix par-dessus la première. La fenêtre déjà ouverte suffit.
        if (_choix) return;
        _choix = true;
        try { await OuvrirUneEntreprise(forcerChoix: true); }
        catch (Exception e) { Debug.LogException(e); ConfirmDialog.Erreur("Changement d'entreprise impossible : " + e.Message); }
        finally { _choix = false; }
    }

    static bool _choix;        // un choix d'entreprise (ChangerEntreprise) attend déjà
    static int _ouvertures;    // ouvertures (Ouvrir) en cours : chacune relit tout

    /// Revue 16c (D2-06, D2-07) : un envoi de factures en cours finit d'abord (son statut « Envoyé » se pose sur la fiche d'avant), et
    /// la file des enregistrements se vide, avant que les fiches ne soient fermées ou remplacées. Revue 17 (D2-09, lot D) : la même
    /// attente pour toute ouverture (Ouvrir : changement d'entreprise, rechargement), le retrait par un admin et la déconnexion ; avant,
    /// pour le rechargement seulement : l'e-mail partait, « Envoyé » ne s'enregistrait nulle part, et la facture repartait au lancement suivant.
    static async Task FinDesEnvois()
    {
        for (int i = 0; i < 120 && EcritureCloud.EnvoiEnCours; i++) await Task.Delay(500);
        try { await EcritureCloud.Attendre(); } catch (Exception) { /* déjà signalé */ }
    }

    static bool _sortie;   // un retrait ou une déconnexion attend déjà la fin d'un envoi

    /// Faux : une sortie attend déjà (second clic, second refus de présence) : l'appelant ne fait rien.
    static async Task<bool> SortieApresLesEnvois()
    {
        if (_sortie) return false;
        _sortie = true;
        try { await FinDesEnvois(); }
        finally { _sortie = false; }
        return true;
    }

    /// Étape 9b : un admin m'a retiré de l'entreprise ouverte (ma présence est refusée). On la
    /// ferme, on efface sa copie locale, et on propose les autres entreprises (s'il y en a).
    public static async void Exclu()
    {
        var e = SessionCloud.Entreprise;
        if (e == null || _bm == null) return;
        if (!await SortieApresLesEnvois()) return;   // Revue 17 (D2-09, lot D)
        if (SessionCloud.Entreprise != e || _bm == null) return;   // une autre entreprise ouverte, ou Play arrêté, pendant l'attente
        Debug.Log($"[Cloud] Retiré(e) de « {e.nom} » par un administrateur");
        DirectCloud.Arreter();
        EcritureCloud.FermerEcrans();
        _bm.Vider();
        EcransCloud.Pastille(Canvas, null);
        CopieLocale.Effacer(e.id);   // D2-13 : seulement celle-ci, les autres restent ouvrables hors ligne
        ReglageService.EffacerCopie(e.id);   // Revue 17 (D2-21, lot D) : ses Réglages (RIB en clair) et ses secrets (mot de passe SMTP) aussi
        SessionCloud.Entreprise = null;
        ConfirmDialog.Erreur($"Vous n'avez plus accès à « {e.nom} » : un administrateur vous a retiré(e) de l'entreprise.",
                             ChangerEntreprise);
    }

    public static async void SeDeconnecter()
    {
        // Revue 17 (D2-09, lot D) : la garde (ConfirmerPerteSaisies) a prévenu ; « Continuer quand même » laisse finir la facture en train de partir.
        if (EcritureCloud.EnvoiEnCours) UndoToast.Instance?.ShowInfo("Envoi de factures en cours : déconnexion à la fin de l'envoi…");
        if (!await SortieApresLesEnvois()) return;
        DirectCloud.Arreter();   // avant Oublier : il signale mon départ avec le jeton
        EcritureCloud.FermerEcrans();
        SessionCloud.Oublier();
        CopieLocale.Effacer();   // étape 8b : plus rien de lisible sur ce PC sans connexion
        ReglageService.EffacerCopie();   // Revue 17 (D2-21, lot D) : ni les Réglages ni les secrets des entreprises du cloud
        _bm.Vider();
        EcransCloud.Pastille(Canvas, null);
        Lancer(_bm);
    }

    static async Task OuvrirUneEntreprise(bool forcerChoix)
    {
        while (true)
        {
            var (liste, erreur) = await DonneesCloud.MesEntreprises();
            if (liste == null) { Debug.LogWarning("[Cloud] Entreprises : " + erreur); ConfirmDialog.Erreur("Impossible de lire vos entreprises : " + erreur); return; }
            Debug.Log($"[Cloud] {liste.Count} entreprise(s) : {string.Join(", ", liste.Select(x => x.nom))}");
            // Revue 17 (D2-19, lot D) : chacune de mes entreprises marque le dossier local qu'elle a déjà sur ce PC, avant qu'une
            // entreprise de même nom, ouverte ici pour la première fois, ne prenne ce dossier sans repère pour le sien.
            foreach (var mienne in liste) MarquerDossierRetenu(mienne);

            // Import proposé si le cloud est vide, ou si un import précédent est à reprendre.
            var sources = ImportLocal.EntreprisesDuPC()
                .Select(s => (source: s, etat: ImportLocal.Statut(s.Repere, SessionCloud.Url, liste)))
                .Where(x => x.etat != ImportLocal.Etat.DejaImportee && x.source.NbBatiments > 0)
                .ToList();
            bool aReprendre = sources.Any(x => x.etat == ImportLocal.Etat.AReprendre);
            if (sources.Count > 0 && (liste.Count == 0 || aReprendre))
            {
                if (!aReprendre) sources = sources.Where(x => x.etat != ImportLocal.Etat.DejaSurLeCloud || liste.Count == 0).ToList();
                if (await Importer(sources)) continue;   // relire la liste après l'import
            }

            EntrepriseCloud choix = liste.Count == 1 && !forcerChoix
                ? liste[0]
                : await EcransCloud.Choisir(Canvas, liste, SessionCloud.Session?.user?.email);
            if (choix == null) { SeDeconnecter(); return; }
            await Ouvrir(choix);
            if (_bm == null) return;   // Play arrêté entre-temps : pas de fenêtre dans la scène d'édition
            // Étape 7 : à l'ouverture seulement, pas à chaque relecture. Chez chacun (décision du
            // 15/10) : la réservation de chaque facture empêche qu'elle parte deux fois.
            FactureEnvoiAuto.ProposerAuDemarrage(_bm.BatimentPrefab);
            TourneeAnnuaire(choix);
            FactureElectronique.Demarrer(choix);   // étape 18b : envois en panne et « Encaissée » en attente
            VisiteGuidee.LancerSiNouvelle();   // étape 13b : une fois par personne, l'accueil seul à l'écran
            return;
        }
    }

    /// Vrai si au moins une entreprise a été (re)importée.
    static async Task<bool> Importer(System.Collections.Generic.List<(ImportLocal.Source source, ImportLocal.Etat etat)> sources)
    {
        bool fait = false;
        await EcransCloud.Import(Canvas, sources, async (s, etat, progression) =>
        {
            string id;
            var repere = s.Repere;
            if (etat == ImportLocal.Etat.AReprendre) id = repere.id;
            else
            {
                progression($"Création de « {s.Nom} » sur le cloud…");
                var (nouvel, err) = await DonneesCloud.CreerPourImport(s.Nom);
                if (nouvel == null) return err;
                id = nouvel;
                repere = new ImportLocal.Repere { id = id, nom = s.Nom, projet = SessionCloud.Url, complet = false };
                if (!ImportLocal.EcrireRepere(s.Racine, repere, out string errRepere))
                    Debug.LogWarning($"[Import] Repère non écrit dans {s.Racine} : {errRepere}. Un 2e import ne serait pas détecté.");
            }
            PlayerPrefs.SetString(ClePrefsDossier(id), s.Racine);   // documents de cette entreprise sur ce PC
            PlayerPrefs.Save();

            var bats = ImportLocal.LireBatiments(s.Racine, out var illisibles);
            for (int i = 0; i < bats.Count; i++)
            {
                progression($"« {s.Nom} » : bâtiment {i + 1} / {bats.Count} ({bats[i].Name})…");
                string err = await DonneesCloud.Envoyer(id, bats[i]);
                if (err != null) return err;
            }
            repere.complet = true;
            ImportLocal.EcrireRepere(s.Racine, repere, out _);
            fait = true;
            if (illisibles.Count > 0)
                ConfirmDialog.Erreur($"« {s.Nom} » importée, sauf {illisibles.Count} fichier(s) illisible(s) : {string.Join(", ", illisibles)}.");
            else
                UndoToast.Instance?.ShowInfo($"« {s.Nom} » importée : {bats.Count} bâtiment(s).");
            return null;
        });
        return fait;
    }

    static async Task Ouvrir(EntrepriseCloud e)
    {
        // BH3 (02/11) : un voile bloque l'écran pendant les lectures (les boutons de l'accueil répondaient encore, alors que
        // CIPL avait déjà basculé d'entreprise) ; retiré au succès comme à l'échec.
        var attente = _bm != null ? EcransCloud.Attente(Canvas, $"Ouverture de {e.nom}…") : null;
        _ouvertures++;   // Revue 17 (D2-09, lot D) : voir Recharger
        try
        {
            await FinDesEnvois();   // Revue 17 (D2-09, lot D) : sous le voile, et tant que l'entreprise d'avant est encore l'entreprise ouverte
            // Revue 17 (D2-04, lot D) : c'est ici, une entreprise à ouvrir en main, que l'ancienne s'arrête : ses écrans se ferment et
            // rendent leurs verrous (avant de changer d'entreprise : Liberer les rend à l'entreprise ouverte), puis son direct s'arrête.
            if (_bm != null) EcritureCloud.FermerEcrans();
            DirectCloud.Arreter();
            // Revue 17 (D2-20, lot D) : l'état connu du serveur (cache, copie à écrire) est celui de l'entreprise d'avant : sans cela, le
            // gardien écrivait la copie locale de la nouvelle entreprise avec les fiches de l'ancienne jusqu'à la fin de la lecture.
            if (SessionCloud.Entreprise?.id != e.id) EcritureCloud.Oublier();
            SessionCloud.Entreprise = e;
            // BH3 (02/11) : dès le changement, plus rien de l'ancienne entreprise à l'écran (une saisie pendant l'attente serait
            // enregistrée sous la nouvelle), un état visible le temps des lectures, et une sonde de 5 s avant de lire.
            if (_bm != null) { _bm.Vider(); _bm.menuManager.OpenGeneralMenu(); EcransCloud.Pastille(Canvas, $"Ouverture de {e.nom}…"); }
            var sonde = await CloudClient.Sonder(SessionCloud.Url, SessionCloud.Cle, timeout: 5);
            if (HorsLigne.Panne(sonde.Ok, sonde.Code)) { EchecLecture(e, "pas de connexion"); return; }

            var (dossier, telecharger) = await DossierDocuments(e);
            SaveLocationService.UseRoot(dossier);
            ReglageService.Load();
            Debug.Log($"[Cloud] Ouverture de « {e.nom} », dossier {dossier}{(telecharger ? "" : " (Drive partagé)")}");
            await ReglagesCloud.Synchroniser(e);   // étape 9a : Réglages et secrets de l'entreprise
            // Revue 16c (D3-10, BH3 du 02/11) : sans connexion, Réglages, abonnement puis lecture attendaient chacun 15 s sans rien
            // à l'écran (45 s « gelées », le temps de recliquer partout). Le premier échec suffit.
            if (HorsLigne.Actif) { EchecLecture(e, "pas de connexion"); return; }
            string abo = await AbonnementCloud.Lire(e);   // étape 9c1 : expiré → lecture seule
            if (abo != null) Debug.LogWarning("[Cloud] Abonnement illisible : " + abo);

            await EcritureCloud.VerifierNom(Canvas);   // « Votre nom », à la première ouverture

            Debug.Log("[Cloud] Lecture des bâtiments…");
            var (bats, erreur) = await EcritureCloud.Charger(e.id);   // avec les versions (étape 4a)
            if (bats == null) { EchecLecture(e, erreur); return; }
            // Play arrêté pendant la lecture : la suite s'exécuterait hors Play et installerait
            // les fiches et leurs onglets dans la scène d'édition (retour du 16/10).
            if (_bm == null) return;

            Afficher(e, bats, telecharger);
            CopieLocale.Retenir(e.id);                  // étape 8b : celle qu'on ouvre sans connexion
            EcritureCloud.GarderCopie(maintenant: true);
            UndoToast.Instance?.ShowInfo($"{e.nom} : {bats.Count} bâtiment(s) lu(s) depuis le cloud.");
        }
        finally { _ouvertures--; if (attente != null) UnityEngine.Object.Destroy(attente.gameObject); }
    }

    /// Revue 16c (D3-10) : lecture impossible à l'ouverture : l'écran ne garde pas les fiches de l'entreprise d'avant sous
    /// les Réglages (RIB, logo…) de celle-ci : fiches vidées, accueil redessiné, message, puis le choix d'entreprise.
    static void EchecLecture(EntrepriseCloud e, string erreur)
    {
        Debug.LogWarning("[Cloud] Lecture : " + erreur);
        if (_bm != null) { EcritureCloud.FermerEcrans(); _bm.Vider(); _bm.menuManager.OpenGeneralMenu(); EcransCloud.Pastille(Canvas, null); }
        ConfirmDialog.Erreur($"Lecture de « {e.nom} » impossible : {erreur}", ChangerEntreprise);
    }

    static void Afficher(EntrepriseCloud e, System.Collections.Generic.List<Batiment> bats, bool telecharger)
    {
        EcritureCloud.FermerEcrans();   // un écran resté ouvert écrirait l'ancienne fiche
        _bm.Vider();
        _bm.Installer(bats);
        EcritureCloud.Recaler(bats);   // les mises à niveau du chargement ne repartent pas au serveur
        _bm.AppliquerCessionsDues();   // revue 16c (D2-02) : une cession datée arrivée à son jour s'applique maintenant
        _bm.menuManager.OpenGeneralMenu();
        EcritureCloud.DemarrerGardien();
        DirectCloud.Demarrer(e.id, telecharger);   // étape 5 : changements des collègues, présence ; 6 : documents
        EcransCloud.Pastille(Canvas, $"{e.nom} · cloud");
    }

    /// Étape 8b : pas de connexion au démarrage. On ouvre la dernière copie de l'entreprise ouverte
    /// en dernier sur ce PC, en lecture seule (bandeau « Données à jour au … ») ; la connexion
    /// revenue, tout est relu (ReprendreEnLigne). Faux sans copie ni connexion gardée.
    static async Task<bool> OuvrirCopie()
    {
        string id = CopieLocale.Derniere;
        if (string.IsNullOrEmpty(id) || !CopieLocale.Charger(id, out var e, out var u, out var aJour, out var etats)) return false;
        if (!SessionCloud.RestaurerHorsLigne(u)) return false;
        Debug.Log($"[Cloud] Hors ligne : ouverture de la copie de « {e.nom} » du {aJour:dd/MM/yyyy HH:mm}");
        SessionCloud.Entreprise = e;
        HorsLigne.SurCopie(aJour);
        var (dossier, telecharger) = await DossierDocuments(e);
        SaveLocationService.UseRoot(dossier);
        ReglageService.Load();
        if (_bm == null) return true;
        EcritureCloud.ReprendreCopie(etats);
        Afficher(e, CopieLocale.Recomposer(etats), telecharger);
        UndoToast.Instance?.ShowInfo($"Hors ligne : {e.nom} est affichée telle qu'au {aJour:dd'/'MM} à {aJour.Hour} h {aJour:mm}.");
        return true;
    }

    /// Étape 8b : la connexion est revenue après un démarrage sur la copie. Tout est relu, puis
    /// l'envoi des factures est proposé, comme à une ouverture normale.
    public static async void ReprendreEnLigne()
    {
        var e = SessionCloud.Entreprise;
        if (e == null || _bm == null) return;
        try
        {
            await Ouvrir(e);
            if (_bm == null) return;
            FactureEnvoiAuto.ProposerAuDemarrage(_bm.BatimentPrefab);
            // Revue 17 (D5-05, lot 12) : l'état de Super PDP est relu, comme à une ouverture normale (puis la tournée de ce qui attend) : sans
            // cela, Active restait faux toute la session après un démarrage hors ligne, et toute facture due à la plateforme était refusée ou retenue.
            FactureElectronique.Demarrer(e);
        }
        catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Relecture de l'entreprise impossible : " + ex.Message); }
    }

    // ── Dossier des documents sur ce PC (étapes 6a et 6b) ───────────────────────

    static string ClePrefsType(string entrepriseId) => "cipl_dossier_type_" + entrepriseId;

    /// Nature du dossier retenu sur ce PC (DossierEntreprise.DriveAuto…), "" pour un dossier de CIPL.
    public static string TypeDossier(string entrepriseId) => PlayerPrefs.GetString(ClePrefsType(entrepriseId), "");

    static void Retenir(string entrepriseId, string dossier, string type)
    {
        PlayerPrefs.SetString(ClePrefsDossier(entrepriseId), dossier);
        PlayerPrefs.SetString(ClePrefsType(entrepriseId), type);
        PlayerPrefs.Save();
    }

    /// « Dans CIPL » : Documents\CIPL\<entreprise>, créé sans question (§13). Jamais la racine par
    /// défaut : c'est celle d'une autre entreprise (« Mon entreprise »).
    /// Revue 17 (D2-19, lot D) : ni le dossier d'une autre entreprise de même nom (« nom (2) » alors : DossierEntreprise.DossierLibre).
    public static string DossierLocal(EntrepriseCloud e) =>
        DossierEntreprise.DossierLibre(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CIPL"),
                                       DossiersDonnees.NomDossier(e.nom), e.id, ImportLocal.LireRepere);

    /// Revue 17 (D2-19, lot D) : le repère CIPL_entreprise.json dit à quelle entreprise est ce dossier local. Déposé à sa création, et à
    /// l'ouverture s'il manque (dossier d'avant la correction). Un repère déjà là n'est jamais remplacé (import, Drive partagé). Il reste à
    /// la racine du dossier : la synchronisation des documents ne le voit pas (SyncDocuments.Partage : Batiment/, Logo/, Anciens documents/).
    static void DeposerRepere(EntrepriseCloud e, string dossier)
    {
        var r = ImportLocal.LireRepere(dossier);
        if (r != null)
        {
            // Deux entreprises de même nom partageaient déjà ce dossier avant la correction : le dossier retenu ne change pas tout seul.
            if (r.id != e.id) Debug.LogWarning($"[Cloud] Le dossier de « {e.nom} » ({dossier}) porte le repère de « {r.nom} » : dossier partagé par deux entreprises.");
            return;
        }
        if (!ImportLocal.EcrireRepere(dossier, new ImportLocal.Repere { id = e.id, nom = e.nom, projet = SessionCloud.Url, complet = true, local = true }, out string err))
            Debug.LogWarning($"[Cloud] Repère non écrit dans {dossier} : {err}. Une entreprise de même nom pourrait prendre ce dossier.");
    }

    /// Le dossier local que cette entreprise a déjà sur ce PC reçoit son repère s'il manque (mode « Dans CIPL » seulement).
    static void MarquerDossierRetenu(EntrepriseCloud e)
    {
        if (e == null || e.ModeDrive) return;
        string dossier = PlayerPrefs.GetString(ClePrefsDossier(e.id), "");
        if (DossierEntreprise.DecisionLocal(dossier, Directory.Exists(dossier), TypeDossier(e.id)) == DossierEntreprise.Local.Garder)
            DeposerRepere(e, dossier);
    }

    static string ChoisirDossier(string titre)
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        var p = SFB.StandaloneFileBrowser.OpenFolderPanel(titre, "", false);
        return p != null && p.Length > 0 && !string.IsNullOrEmpty(p[0]) ? p[0] : null;
#else
        return null;
#endif
    }

    /// Où ranger les documents sur ce PC, et si ce PC les reçoit lui-même du cloud (faux dans un
    /// Drive partagé : c'est le Drive qui les recopie).
    static async Task<(string dossier, bool telecharger)> DossierDocuments(EntrepriseCloud e)
    {
        string dossier = PlayerPrefs.GetString(ClePrefsDossier(e.id), "");
        string type = TypeDossier(e.id);
        bool existe = !string.IsNullOrEmpty(dossier) && Directory.Exists(dossier);
        bool dansDrive = type == DossierEntreprise.DriveAuto || type == DossierEntreprise.DriveMain;

        if (!e.ModeDrive)
        {
            var quoi = DossierEntreprise.DecisionLocal(dossier, existe, type);
            // Revue 17 (D2-12, lot D) : le dossier retenu sur ce PC manque (disque externe ou NAS pas monté) : avant, Documents\CIPL le
            // remplaçait sans le dire et pour toujours, tout était retéléchargé, et l'ancien dossier n'était plus jamais utilisé.
            // On demande ; le nouveau dossier n'est retenu que sur « Utiliser Documents\CIPL ».
            DateTime? essai = null;   // retour de BW18 (14/11) : l'heure du dernier « Réessayer », dite dans la fenêtre suivante
            while (quoi == DossierEntreprise.Local.Demander)
            {
                Debug.LogWarning($"[Cloud] Dossier des documents de « {e.nom} » introuvable : {dossier}");
                bool reessayer = await EcransCloud.DossierLocalIntrouvable(Canvas, e.nom, dossier, essai);
                quoi = reessayer
                    ? DossierEntreprise.DecisionLocal(dossier, Directory.Exists(dossier), type)   // « Réessayer »
                    : DossierEntreprise.Local.Nouveau;
                if (reessayer) essai = DateTime.Now;
            }
            // Jamais ouverte sur ce PC, ou retour « Dans CIPL » : on quitte le Drive, où N copies reçues du cloud se battraient.
            if (quoi == DossierEntreprise.Local.Nouveau)
            {
                dossier = DossierLocal(e);
                Directory.CreateDirectory(dossier);
                Retenir(e.id, dossier, "");
            }
            DeposerRepere(e, dossier);   // Revue 17 (D2-19, lot D)
            return (dossier, true);
        }

        if (existe && dansDrive && DossierEntreprise.Refus(dossier, e.id, e.nom) == null) return (dossier, false);
        if (existe && type == DossierEntreprise.HorsDrive) return (dossier, true);

        string refus = null;
        while (true)
        {
            Debug.Log("[Cloud] Recherche du Drive partagé…");
            UndoToast.Instance?.ShowInfo($"Recherche des documents de {e.nom} dans votre Drive…");
            var trouve = await Task.Run(() => DossierEntreprise.Chercher(e.id, DossierEntreprise.RacinesDuPC()));
            if (trouve != null)
            {
                Debug.Log("[Cloud] Drive partagé trouvé : " + trouve);
                Retenir(e.id, trouve, DossierEntreprise.DriveAuto);
                UndoToast.Instance?.ShowInfo($"Documents de {e.nom} trouvés dans votre Drive : {trouve}");
                return (trouve, false);
            }
            switch (await EcransCloud.DossierIntrouvable(Canvas, e.nom, refus))
            {
                case EcransCloud.ChoixDossier.Rechercher:
                    refus = null;
                    continue;
                case EcransCloud.ChoixDossier.Choisir:
                    string choisi = ChoisirDossier($"Dossier partagé de {e.nom}");
                    if (choisi == null) continue;
                    refus = DossierEntreprise.Refus(choisi, e.id, e.nom);
                    if (refus != null) continue;
                    Retenir(e.id, choisi, DossierEntreprise.DriveMain);
                    return (choisi, false);
                default:
                    // Continuer sans le dossier : ce PC reçoit lui-même les documents, là où on lui
                    // dit de les ranger (décision du 14/10).
                    string parent = ChoisirDossier($"Où ranger les documents de {e.nom} sur cet ordinateur ?");
                    if (parent == null) continue;
                    string local = DossierEntreprise.Cible(parent, e.id, e.nom);
                    // Revue 17 (D2-19, lot D) : comme ChangerDossierLocal : le dossier local d'une entreprise de même nom porte son repère ;
                    // le prendre enverrait ses documents sur le cloud de celle-ci au premier passage.
                    var autre = ImportLocal.LireRepere(local);
                    if (autre != null && autre.id != e.id) { refus = $"Ce dossier est celui de « {autre.nom} », pas de « {e.nom} »."; continue; }
                    Directory.CreateDirectory(local);
                    Retenir(e.id, local, DossierEntreprise.HorsDrive);
                    return (local, true);
            }
        }
    }

    // ── Réglages › Documents (étape 6b, maquette validée le 14/10) ─────────────

    [Serializable] class ParamsMode { public string p_entreprise, p_mode; }
    [Serializable] class ParamsEntreprise { public string p_entreprise; }

    /// Étape 10 (décision du 23/10) : la tournée de l'annuaire des entreprises (résumé d'entreprise
    /// de tous les locataires), une fois par jour et par entreprise, sur le premier poste qui
    /// l'ouvre (le serveur le note). Jamais en lecture seule ; fiches en cours de modification sautées.
    static async void TourneeAnnuaire(EntrepriseCloud e)
    {
        try
        {
            await Task.Delay(30000);   // après le démarrage, une fois les verrous des autres relus
            if (_bm == null || SessionCloud.Entreprise != e || HorsLigne.Actif || AbonnementCloud.LectureSeule) return;
            var r = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(),
                                               "prendre_tournee_annuaire", new ParamsEntreprise { p_entreprise = e.id });
            // Retour du 23/10 : sans trace, une tournée faite par un autre poste (le build) ne se voyait pas.
            if (!r.Ok) { Debug.LogWarning("[Cloud] Tournée de l'annuaire impossible : " + r.Erreur); return; }
            if (r.Corps.Trim() != "true") { Debug.Log("[Cloud] Tournée de l'annuaire : déjà faite aujourd'hui (par ce poste ou un autre)."); return; }
            var libre = await EcritureCloud.LocatairesLibres();
            if (libre == null) { Debug.LogWarning("[Cloud] Tournée de l'annuaire : verrous illisibles, reportée à demain."); return; }
            if (_bm == null || SessionCloud.Entreprise != e) return;
            Debug.Log($"[Cloud] Tournée de l'annuaire des entreprises pour « {e.nom} »");
            _bm.StartCoroutine(PappersSync.Tout(_bm.BatimentPrefab, libre));
        }
        catch (Exception ex) { Debug.LogWarning("[Cloud] Tournée de l'annuaire : " + ex.Message); }
    }

    static async Task<bool> DefinirMode(EntrepriseCloud e, string mode)
    {
        var r = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "definir_mode_documents",
                                           new ParamsMode { p_entreprise = e.id, p_mode = mode });
        var res = r.Ok ? JsonUtility.FromJson<EcritureCloud.Resultat>(r.Corps) : null;
        if (res == null || !res.ok)
        {
            ConfirmDialog.Erreur(res?.refus == "admin" ? "Seul un administrateur peut changer le rangement des documents."
                                                       : "Changement impossible : " + r.Erreur);
            return false;
        }
        e.mode_documents = mode;
        return true;
    }

    /// Admin : les documents passent dans un dossier partagé (OneDrive, Google Drive…), ou changent
    /// de dossier partagé. Ceux de ce PC y sont recopiés avec les Réglages, et le repère y est
    /// déposé : les collègues le retrouvent seuls à leur prochain démarrage.
    public static void PasserEnDrive()
    {
        var e = SessionCloud.Entreprise;
        if (e == null) return;
        string choisi = ChoisirDossier($"Dossier partagé (OneDrive, Google Drive…) où ranger les documents de {e.nom}");
        if (choisi == null) return;
        // Un sous-dossier au nom de l'entreprise, sauf si le dossier choisi est déjà le sien.
        string dossier = DossierEntreprise.Cible(choisi, e.id, e.nom);
        string actuel = SaveLocationService.GetSaveRoot();
        if (DossierEntreprise.Meme(dossier, actuel))
        {
            UndoToast.Instance?.ShowInfo("C'est déjà le dossier des documents.");
            return;
        }
        ConfirmDialog.Instance?.Show("Ranger les documents dans ce dossier partagé ?",
            $"Les documents et les Réglages de ce PC seront recopiés dans :\n{dossier}\n\n" +
            "Vos collègues le retrouveront automatiquement à leur prochain démarrage de CIPL.",
            async () =>
            {
                try
                {
                    Directory.CreateDirectory(dossier);
                    int n = DossierEntreprise.Recopier(actuel, dossier);
                    if (!ImportLocal.EcrireRepere(dossier, new ImportLocal.Repere { id = e.id, nom = e.nom, projet = SessionCloud.Url, complet = true }, out string err))
                    { ConfirmDialog.Erreur("Repère non écrit dans le dossier partagé : " + err); return; }
                    // Changement de dossier partagé : l'ancien repère est retiré, sinon les collègues y resteraient.
                    if (e.ModeDrive && ImportLocal.LireRepere(actuel)?.id == e.id)
                    {
                        string ancien = Path.Combine(actuel, "CIPL_entreprise.ancien.json");
                        if (File.Exists(ancien)) File.Delete(ancien);
                        File.Move(Path.Combine(actuel, ImportLocal.NomRepere), ancien);
                    }
                    if (!e.ModeDrive && !await DefinirMode(e, "drive")) return;
                    Retenir(e.id, dossier, DossierEntreprise.DriveMain);
                    UndoToast.Instance?.ShowInfo($"Documents rangés dans le Drive partagé ({n} recopié(s)).");
                    Recharger();
                }
                catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Passage au Drive partagé impossible : " + ex.Message); }
            }, "Ranger ici");
    }

    /// Admin : retour « Dans CIPL », chaque PC reçoit sa copie. Rien n'est effacé du Drive.
    public static void PasserEnCipl()
    {
        var e = SessionCloud.Entreprise;
        if (e == null || !e.ModeDrive) return;
        string local = DossierLocal(e);
        ConfirmDialog.Instance?.Show("Ranger les documents dans CIPL ?",
            $"Chaque ordinateur recevra sa copie des documents (ici : {local}). Le dossier partagé n'est plus utilisé ; rien n'y est effacé.\n\n" +
            "Vos collègues basculent à leur prochain démarrage de CIPL.",
            async () =>
            {
                try
                {
                    if (!await DefinirMode(e, "cipl")) return;
                    Directory.CreateDirectory(local);
                    DossierEntreprise.Recopier(SaveLocationService.GetSaveRoot(), local);
                    Retenir(e.id, local, "");
                    Recharger();
                }
                catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Retour dans CIPL impossible : " + ex.Message); }
            }, "Ranger dans CIPL");
    }

    /// Sur cet ordinateur : chercher de nouveau le dossier partagé (après l'avoir synchronisé).
    public static async void RechercherDossier()
    {
        var e = SessionCloud.Entreprise;
        if (e == null) return;
        UndoToast.Instance?.ShowInfo("Recherche du dossier partagé…");
        var trouve = await Task.Run(() => DossierEntreprise.Chercher(e.id, DossierEntreprise.RacinesDuPC()));
        if (trouve == null)
        {
            ConfirmDialog.Erreur($"Le dossier partagé de « {e.nom} » est introuvable sur cet ordinateur. " +
                                 "Ouvrez ce Drive dans votre navigateur et cliquez sur « Synchroniser », ou choisissez-le à la main.");
            return;
        }
        Retenir(e.id, trouve, DossierEntreprise.DriveAuto);
        Recharger();
    }

    /// Sur cet ordinateur : désigner soi-même le dossier partagé (refusé s'il n'est pas celui de l'entreprise).
    public static void ChoisirDossierPartage()
    {
        var e = SessionCloud.Entreprise;
        if (e == null) return;
        string choisi = ChoisirDossier($"Dossier partagé de {e.nom}");
        if (choisi == null) return;
        string refus = DossierEntreprise.Refus(choisi, e.id, e.nom);
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }
        Retenir(e.id, choisi, DossierEntreprise.DriveMain);
        Recharger();
    }

    /// Dans CIPL : ranger les documents de ce PC dans un autre dossier (décision du 15/10). On désigne
    /// le dossier parent ; ce qui est déjà ici y est recopié, rien n'est effacé, le reste arrive du cloud.
    public static void ChangerDossierLocal()
    {
        var e = SessionCloud.Entreprise;
        if (e == null || e.ModeDrive) return;
        string choisi = ChoisirDossier($"Où ranger les documents de {e.nom} sur cet ordinateur ?");
        if (choisi == null) return;
        string dossier = DossierEntreprise.Cible(choisi, e.id, e.nom);
        string actuel = SaveLocationService.GetSaveRoot();
        if (DossierEntreprise.Meme(dossier, actuel)) { UndoToast.Instance?.ShowInfo("C'est déjà le dossier des documents."); return; }
        var autre = ImportLocal.LireRepere(dossier);
        if (autre != null && autre.id != e.id) { ConfirmDialog.Erreur($"Ce dossier est celui de « {autre.nom} », pas de « {e.nom} »."); return; }
        try
        {
            Directory.CreateDirectory(dossier);
            int n = DossierEntreprise.Recopier(actuel, dossier);
            Retenir(e.id, dossier, "");
            UndoToast.Instance?.ShowInfo($"Documents rangés dans {dossier} ({n} recopié(s)).");
            Recharger();
        }
        catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Changement de dossier impossible : " + ex.Message); }
    }

    /// Relit l'entreprise ouverte (après un refus du serveur : version dépassée, fiche prise).
    public static async void Recharger()
    {
        var e = SessionCloud.Entreprise;
        if (e == null || _bm == null) return;
        // Revue 16c (D2-06, D2-07) : l'envoi de factures en cours et la file des enregistrements finissent d'abord : Ouvrir les attend
        // (FinDesEnvois ; Revue 17, D2-09, lot D : pour toute ouverture, plus seulement ici).
        // Revue 17 (D2-09, lot D) : une ouverture en cours peut attendre la fin d'un envoi (FinDesEnvois) alors que l'entreprise ouverte
        // est encore celle d'avant : relancer Ouvrir ici rouvrirait l'ancienne par-dessus la nouvelle. Celle en cours relit tout.
        if (_ouvertures > 0) return;
        try { await Ouvrir(e); }
        catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Rechargement impossible : " + ex.Message); }
    }
}
