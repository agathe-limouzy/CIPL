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
            ConfirmDialog.Erreur("Adresse du projet cloud non renseignée : dans Unity, menu CIPL › Essai cloud (étape 1).");
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

    public static async void ChangerEntreprise()
    {
        DirectCloud.Arreter();
        EcritureCloud.FermerEcrans();
        try { await OuvrirUneEntreprise(forcerChoix: true); }
        catch (Exception e) { Debug.LogException(e); ConfirmDialog.Erreur("Changement d'entreprise impossible : " + e.Message); }
    }

    /// Étape 9b : un admin m'a retiré de l'entreprise ouverte (ma présence est refusée). On la
    /// ferme, on efface sa copie locale, et on propose les autres entreprises (s'il y en a).
    public static void Exclu()
    {
        var e = SessionCloud.Entreprise;
        if (e == null || _bm == null) return;
        Debug.Log($"[Cloud] Retiré(e) de « {e.nom} » par un administrateur");
        DirectCloud.Arreter();
        EcritureCloud.FermerEcrans();
        _bm.Vider();
        EcransCloud.Pastille(Canvas, null);
        CopieLocale.Effacer();
        SessionCloud.Entreprise = null;
        ConfirmDialog.Erreur($"Vous n'avez plus accès à « {e.nom} » : un administrateur vous a retiré(e) de l'entreprise.",
                             ChangerEntreprise);
    }

    public static void SeDeconnecter()
    {
        DirectCloud.Arreter();   // avant Oublier : il signale mon départ avec le jeton
        EcritureCloud.FermerEcrans();
        SessionCloud.Oublier();
        CopieLocale.Effacer();   // étape 8b : plus rien de lisible sur ce PC sans connexion
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
        SessionCloud.Entreprise = e;

        var (dossier, telecharger) = await DossierDocuments(e);
        SaveLocationService.UseRoot(dossier);
        ReglageService.Load();
        Debug.Log($"[Cloud] Ouverture de « {e.nom} », dossier {dossier}{(telecharger ? "" : " (Drive partagé)")}");
        await ReglagesCloud.Synchroniser(e);   // étape 9a : Réglages et secrets de l'entreprise
        string abo = await AbonnementCloud.Lire(e);   // étape 9c1 : expiré → lecture seule
        if (abo != null) Debug.LogWarning("[Cloud] Abonnement illisible : " + abo);

        await EcritureCloud.VerifierNom(Canvas);   // « Votre nom », à la première ouverture

        Debug.Log("[Cloud] Lecture des bâtiments…");
        var (bats, erreur) = await EcritureCloud.Charger(e.id);   // avec les versions (étape 4a)
        if (bats == null) { Debug.LogWarning("[Cloud] Lecture : " + erreur); ConfirmDialog.Erreur($"Lecture de « {e.nom} » impossible : {erreur}"); return; }
        // Play arrêté pendant la lecture : la suite s'exécuterait hors Play et installerait
        // les fiches et leurs onglets dans la scène d'édition (retour du 16/10).
        if (_bm == null) return;

        Afficher(e, bats, telecharger);
        CopieLocale.Retenir(e.id);                  // étape 8b : celle qu'on ouvre sans connexion
        EcritureCloud.GarderCopie(maintenant: true);
        UndoToast.Instance?.ShowInfo($"{e.nom} : {bats.Count} bâtiment(s) lu(s) depuis le cloud.");
    }

    static void Afficher(EntrepriseCloud e, System.Collections.Generic.List<Batiment> bats, bool telecharger)
    {
        EcritureCloud.FermerEcrans();   // un écran resté ouvert écrirait l'ancienne fiche
        _bm.Vider();
        _bm.Installer(bats);
        EcritureCloud.Recaler(bats);   // les mises à niveau du chargement ne repartent pas au serveur
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
    public static string DossierLocal(EntrepriseCloud e) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CIPL", DossiersDonnees.NomDossier(e.nom));

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
            // Retour « Dans CIPL » : on quitte le Drive, où N copies reçues du cloud se battraient.
            if (!existe || dansDrive)
            {
                dossier = DossierLocal(e);
                Directory.CreateDirectory(dossier);
                Retenir(e.id, dossier, "");
            }
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
        try { await Ouvrir(e); }
        catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Rechargement impossible : " + ex.Message); }
    }
}
