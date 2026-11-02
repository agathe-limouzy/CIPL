using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class BatimentManager : MonoBehaviour
{
    public static BatimentManager Instance { get; private set; }

    [Header("Prefab")]
    public GameObject batimentPrefab;
    public Transform batimentsContainer;
    public Transform batimentsContainerPanel;
    // Liste en mémoire
    private List<Batiment> _batiments = new List<Batiment>();
    public List<Batiment> Batiments => _batiments;

    public List<BatimentPrefab> BatimentPrefab;
    public MenuManager menuManager;

    // Dossier de sauvegarde — un JSON par bâtiment
    private string SaveFolder =>
    Path.Combine(SaveLocationService.GetSaveRoot(), "batiments");

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Taille d'affichage choisie dans les réglages. En tout premier : la poser
        // après la construction des écrans les obligerait à se recalculer.
        UIZoom.Appliquer();

        // Étape 2 du multi-utilisateur (CONCEPTION_MULTIUTILISATEUR.md) : les données viennent
        // du cloud. Sauvegarde au démarrage, purge, migration des dossiers et Pappers sont
        // suspendus ; l'envoi automatique des factures est relancé par DemarrageCloud, avec
        // réservation de chaque facture (étape 7). Le démarrage local d'avant le cloud a été retiré (revue 16c, D2-14).
        DemarrageCloud.Lancer(this);
    }

    public void ReloadFromDisk()
    {
        // Détruire les prefabs existants
        foreach (var prefab in BatimentPrefab)
        {
            menuManager.RemoveTabAndBuilding(prefab);
            Destroy(prefab.gameObject);
        }
        BatimentPrefab.Clear();
        _batiments.Clear();
        // Le cache photo est indexe par chemin RELATIF : deux entreprises ayant un
        // batiment de meme nom partagent la meme cle, et la fiche afficherait la
        // photo de l'entreprise precedente.
        PhotoService.ViderCache();

        LoadAll();
    }

    // ── Ajout ────────────────────────────────────────────────────────────────

    public BatimentPrefab AddBatiment(bool needToModify)
    {

        var data = new Batiment();
        var prefab = SpawnPrefabInPanel(data, menuManager.ContentPrefab.transform, needToModify);
        BatimentPrefab.Add(prefab);
        // Persistance immédiate : sans ça, un bâtiment tout juste créé n'existait
        // qu'en mémoire et disparaissait à la fermeture de l'app.
        // (SaveBatiment ajoute lui-même `data` à _batiments, d'où l'absence d'Add ici.)
        SaveBatiment(data);
        return prefab;
      
        
    }

    // ── Suppression ──────────────────────────────────────────────────────────

    // Dossiers de bâtiments supprimés, en attente d'une éventuelle annulation.
    // Clé = id du bâtiment, valeur = son emplacement en corbeille.
    static readonly Dictionary<string, string> _dossiersEnCorbeille = new Dictionary<string, string>();

    static string DossierCorbeilleBatiments =>
        Path.Combine(SaveLocationService.GetSaveRoot(), "corbeille_batiments");

    /// `apresSucces` : ce que l'appelant fait une fois le bâtiment vraiment supprimé (le message « Annuler »).
    public void DeleteBatiment(string id, Action apresSucces = null)
    {
        // Nom relevé AVANT le retrait de la liste : c'est lui qui donne le dossier.
        string nom = _batiments.Find(b => b.id == id)?.Name;

        // Revue 16c (D2-03, retour du 01/11) : sur le cloud, RIEN ne bouge avant l'accord du serveur : un refus (fiche
        // prise, réseau, lecture seule) laisse le bâtiment à l'écran et ses documents en place. Avant, il disparaissait
        // jusqu'au relancement, et son dossier partait en corbeille (puis ses documents étaient supprimés chez tout le monde).
        if (SessionCloud.Active)
        {
            if (HorsLigne.Actif) { ConfirmDialog.Erreur(HorsLigne.Refus); return; }
            if (AbonnementCloud.LectureSeule) { ConfirmDialog.Erreur(AbonnementCloud.Refus); return; }
            EcritureCloud.SupprimerBatiment(id, nom, () =>
            {
                RetirerDeLEcran(id);
                MettreDossierEnCorbeille(id, nom);
                PhotoService.ViderCache();
                Debug.Log($"[BatimentManager] Bâtiment '{id}' supprimé");
                apresSucces?.Invoke();
            });
            return;
        }

        RetirerDeLEcran(id);
        // Le dossier du bâtiment (photos, factures, charges) est DÉPLACÉ en corbeille, pas laissé en place : un dossier
        // orphelin faisait ensuite échouer tout renommage vers ce nom (« un dossier X existe déjà »). Il n'est JAMAIS
        // purgé automatiquement : il contient des pièces comptables. Sur le cloud, il ne part qu'après l'accord du
        // serveur (revue 16c, D2-03) : un refus (fiche prise, réseau) fait revenir la fiche, et ses documents n'ont pas
        // bougé — sinon la synchronisation les rangeait en corbeille chez tout le monde.
        string path = GetFilePath(id);
        if (File.Exists(path)) File.Delete(path);
        MettreDossierEnCorbeille(id, nom);

        // Le cache photo est indexé par chemin relatif : sans ce vidage, un bâtiment
        // recréé sous le même nom afficherait les photos de celui qu'on vient d'effacer.
        PhotoService.ViderCache();

        Debug.Log($"[BatimentManager] Bâtiment '{id}' supprimé");
        apresSucces?.Invoke();
    }

    void RetirerDeLEcran(string id)
    {
        _batiments.RemoveAll(b => b.id == id);
        var tab = BatimentPrefab.Find(t => t.getID() == id);
        BatimentPrefab.RemoveAll(b => b.getID() == id);
        if (tab != null)
        {
            menuManager.RemoveTabAndBuilding(tab);
            Destroy(tab.gameObject);
        }
    }

    /// Déplace le dossier disque d'un bâtiment vers la corbeille. Renvoie l'emplacement
    /// atteint, ou null si le bâtiment n'avait pas encore de dossier. Un échec est
    /// signalé plutôt que taire : la suppression logique, elle, a déjà eu lieu.
    static void MettreDossierEnCorbeille(string id, string nom)
    {
        string enCorbeille = DeplacerDossierEnCorbeille(nom);
        if (!string.IsNullOrEmpty(enCorbeille)) _dossiersEnCorbeille[id] = enCorbeille;
    }

    static string DeplacerDossierEnCorbeille(string nomBatiment)
        => string.IsNullOrWhiteSpace(nomBatiment) ? null : DeplacerEnCorbeille(DossiersDonnees.DossierBatiment(nomBatiment));

    /// Revue 16c (D7a-09) : la même corbeille pour le dossier d'un locataire supprimé (clé = son id), restauré par
    /// « Annuler » ; un dossier orphelin interdisait tout renommage ou cession vers ce nom.
    public static void MettreDossierLocataireEnCorbeille(string idLocataire, string nomBatiment, string nomLocataire)
    {
        string c = DeplacerEnCorbeille(DossiersDonnees.DossierLocataire(nomBatiment, nomLocataire));
        if (!string.IsNullOrEmpty(c)) _dossiersEnCorbeille[idLocataire] = c;
    }

    public static void RestaurerDossierLocataire(string idLocataire, string nomBatiment, string nomLocataire)
        => RestaurerDossier(idLocataire, DossiersDonnees.DossierLocataire(nomBatiment, nomLocataire), nomLocataire);

    static string DeplacerEnCorbeille(string source)
    {
        try
        {
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source)) return null;

            Directory.CreateDirectory(DossierCorbeilleBatiments);
            string dest = Path.Combine(DossierCorbeilleBatiments,
                Guid.NewGuid().ToString("N") + "_" + new DirectoryInfo(source).Name);
            Directory.Move(source, dest);
            return dest;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BatimentManager] Dossier « {source} » non déplacé en " +
                           $"corbeille : {e.Message} — il pourra bloquer un futur renommage.");
            return null;
        }
    }

    /// Remet le dossier d'un bâtiment restauré à son emplacement. Sans effet si la
    /// suppression n'avait pas de dossier, ou si un dossier occupe déjà la place
    /// (on ne l'écrase pas : mieux vaut un dossier en corbeille qu'un écrasement).
    static void RestaurerDossierDepuisCorbeille(Batiment data)
    {
        if (data != null) RestaurerDossier(data.id, DossiersDonnees.DossierBatiment(data.Name), data.Name);
    }

    static void RestaurerDossier(string cle, string destination, string nom)
    {
        if (cle == null || !_dossiersEnCorbeille.TryGetValue(cle, out string corbeille)) return;
        _dossiersEnCorbeille.Remove(cle);
        try
        {
            if (!Directory.Exists(corbeille) || Directory.Exists(destination)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            Directory.Move(corbeille, destination);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BatimentManager] Dossier de « {nom} » non restauré depuis la corbeille : {e.Message} — "
                           + $"ses fichiers sont dans {corbeille}.");
        }
    }

    // ── Restauration (annuler une suppression) ───────────────────────────────

    public void RestoreBatiment(string json)
    {
        var data = JsonUtility.FromJson<Batiment>(json);
        if (data == null) return;

        _batiments.Add(data);
        RestaurerDossierDepuisCorbeille(data);   // photos et factures reviennent avec lui
        SaveBatiment(data);   // réécrit le fichier JSON supprimé

        var prefab = SpawnPrefabInPanel(data, batimentsContainerPanel, false);
        BatimentPrefab.Add(prefab);
        menuManager.CreateTab(prefab);
        menuManager.OnSelect(prefab);
        prefab.RefreshBatimentTabAlert();

        Debug.Log($"[BatimentManager] Bâtiment '{data.Name}' restauré");
    }

    // ── Sauvegarde — 1 JSON par bâtiment ─────────────────────────────────────

    /// Renvoie true si l'écriture disque a réussi. Le retour peut être ignoré par
    /// les appelants existants, mais permet d'afficher un échec au lieu de laisser
    /// croire à une sauvegarde qui n'a pas eu lieu.
    /// `silencieux` : écriture de fond (revue 16c, D2-04) : un refus est journalisé, jamais affiché ni suivi d'un rechargement.
    public bool SaveBatiment(Batiment data, bool silencieux = false)
    {
        // Étape 5 : écran resté ouvert sur une fiche relue depuis (changement d'un collègue,
        // rechargement) : il renverrait des locataires périmés avec la version à jour.
        if (SessionCloud.Active && EcritureCloud.Perime(data))
        {
            if (silencieux) Debug.LogWarning($"[BatimentManager] « {data.Name} » : fiche périmée, écriture de fond abandonnée.");
            else ConfirmDialog.Erreur($"« {data.Name} » a été mis à jour pendant que cet écran était ouvert : " +
                                      "cette modification n'est pas enregistrée. Refaites-la sur la fiche à jour.");
            return false;
        }

        // Synchronise la liste en mémoire : les prefabs travaillent sur des
        // copies (clone JSON) — sans ça, Batiments reste périmée et le menu
        // home (objectifs, stats) ne voit pas les changements.
        int idx = _batiments.FindIndex(b => b.id == data.id);
        if (idx >= 0) _batiments[idx] = data;
        else _batiments.Add(data);

        // Étape 4a : sur le cloud, seules les fiches changées partent (EcritureCloud), dans
        // l'ordre ; un refus du serveur (version dépassée, fiche prise) est affiché puis la
        // fiche est relue. Plus aucun batiment_*.json n'est écrit sur ce PC.
        if (SessionCloud.Active) { EcritureCloud.Enregistrer(data, silencieux); return true; }

        Directory.CreateDirectory(SaveFolder);
        string json = JsonUtility.ToJson(data, prettyPrint: true);

        // Écriture atomique : une coupure pendant l'écriture ne peut plus laisser
        // un JSON tronqué (qui empêchait ensuite l'app de démarrer, cf. LoadAll).
        if (!AtomicFile.WriteAllText(GetFilePath(data.id), json, out string erreur))
        {
            Debug.LogError($"[BatimentManager] ÉCHEC de sauvegarde de '{data.Name}' — {erreur}. " +
                           "Les modifications ne sont PAS enregistrées sur le disque.");
            return false;
        }

        Debug.Log($"[BatimentManager] Sauvegardé → {GetFilePath(data.id)}");
        return true;
    }


    // ── Cloud : vider l'écran et installer les bâtiments lus (étape 2) ─────────

    /// Retire toutes les fiches et onglets (changement d'entreprise, déconnexion).
    public void Vider()
    {
        // Étape 5 : un écran resté ouvert sur ces fiches ne doit plus les enregistrer.
        foreach (var b in _batiments) EcritureCloud.Perimer(b);
        foreach (var prefab in BatimentPrefab)
        {
            if (prefab == null) continue;
            EcritureCloud.Perimer(prefab.getBatiment());
            menuManager.RemoveTabAndBuilding(prefab);
            Destroy(prefab.gameObject);
        }
        BatimentPrefab.Clear();
        _batiments.Clear();
        foreach (Transform child in batimentsContainer) Destroy(child.gameObject);
        PhotoService.ViderCache();
        // Revue 16c (D3-10, vu en BH3 le 02/11) : l'accueil gardait les cartes des fiches détruites ; un clic dessus levait
        // une MissingReferenceException. Redessiné ici, pour tous les appelants (échec de lecture, retrait, déconnexion).
        GeneralMenuPanel.Instance?.Refresh();
    }

    /// Crée les fiches et onglets des bâtiments lus sur le cloud, avec les mêmes mises à
    /// niveau en mémoire que LoadAll. Sans cession ni écriture : on est en lecture seule.
    public void Installer(List<Batiment> batiments)
    {
        foreach (var data in batiments)
        {
            try
            {
                MettreANiveau(data);
                _batiments.Add(data);
                var prefab = SpawnPrefabInPanel(data, batimentsContainerPanel, false);
                BatimentPrefab.Add(prefab);
                menuManager.CreateTab(prefab);
                prefab.RefreshBatimentTabAlert();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BatimentManager] Bâtiment du cloud non affiché : {data?.Name} — {e.Message}");
                Debug.LogException(e);   // la pile : sans elle, la cause du 13/10 était introuvable
            }
        }
        if (BatimentPrefab.Count > 0) menuManager.OnSelect(BatimentPrefab[0]);
        Debug.Log($"[BatimentManager] {_batiments.Count} bâtiment(s) installé(s) depuis le cloud");
    }

    /// Mises à niveau en mémoire faites à chaque lecture (comme LoadAll), sans écriture.
    static void MettreANiveau(Batiment data)
    {
        ChargeBatiment.Reconstituer(data);
        Objectifs.Migrer(data.objectifs);
        if (data.locataireDuBatiment != null)
            foreach (var loc in data.locataireDuBatiment)
            {
                Objectifs.Migrer(loc?.objectifs);
                if (loc != null) Loyers.Actualiser(loc, DateTime.Today);
            }
    }

    /// Étape 5 : fiche relue après le changement d'un collègue, reconstruite à la même place
    /// (onglet, vue et locataire affichés conservés). Faux si le bâtiment est nouveau.
    public bool Remplacer(Batiment data)
    {
        MettreANiveau(data);
        int idx = _batiments.FindIndex(b => b.id == data.id);
        if (idx >= 0) { EcritureCloud.Perimer(_batiments[idx]); _batiments[idx] = data; }
        else _batiments.Add(data);

        int i = BatimentPrefab.FindIndex(p => p != null && p.getID() == data.id);
        var nouveau = SpawnPrefabInPanel(data, batimentsContainerPanel, false);
        if (i < 0)
        {
            BatimentPrefab.Add(nouveau);
            menuManager.CreateTab(nouveau);
            nouveau.gameObject.SetActive(false);   // n'apparaît pas par-dessus la fiche affichée
            nouveau.RefreshBatimentTabAlert();
            return false;
        }
        var ancien = BatimentPrefab[i];
        var vue = ancien.Vue();
        EcritureCloud.Perimer(ancien.getBatiment());
        nouveau.transform.SetSiblingIndex(ancien.transform.GetSiblingIndex());
        BatimentPrefab[i] = nouveau;
        menuManager.RemplacerFiche(ancien, nouveau);
        nouveau.RetablirVue(vue);
        Destroy(ancien.gameObject);
        nouveau.RefreshBatimentTabAlert();
        return true;
    }

    /// Étape 5 : bâtiment supprimé par un collègue. Son nom, ou null s'il n'était pas affiché ici.
    public string Retirer(string id, bool montrerVoisin)
    {
        foreach (var b in _batiments) if (b.id == id) EcritureCloud.Perimer(b);
        _batiments.RemoveAll(b => b.id == id);
        var p = BatimentPrefab.Find(x => x != null && x.getID() == id);
        if (p == null) return null;
        string nom = p.getName();
        EcritureCloud.Perimer(p.getBatiment());
        BatimentPrefab.Remove(p);
        menuManager.RemoveTabAndBuilding(p, montrerVoisin);
        Destroy(p.gameObject);
        return nom;
    }

    // ── Chargement ───────────────────────────────────────────────────────────

    // Cession prévue arrivée à son jour (retour du 06/10) : le dossier suit le nouveau nom,
    // comme un renommage par « Modifier ». Nom pris ou dossier bloqué : on réessaie au
    // prochain lancement. ponytail: vérifiée au lancement seulement ; une application
    // restée ouverte passé le jour de la cession attend le lancement suivant.
    /// Revue 16c (D2-02) : sur le cloud, une cession datée s'applique à l'ouverture, comme une action en un clic : la
    /// fiche renommée est enregistrée tout de suite (refusée si elle est prise ou dépassée : elle réessaie à l'ouverture
    /// suivante) et sa fiche à l'écran est reconstruite sur les données à jour. (LoadAll fait de même hors cloud.)
    public void AppliquerCessionsDues()
    {
        if (HorsLigne.Actif || AbonnementCloud.LectureSeule) return;
        foreach (var bat in new List<Batiment>(_batiments))
        {
            bool cede = false;
            foreach (var loc in bat.locataireDuBatiment ?? new List<Locataire>())
                if (loc != null) cede |= EffectuerCession(bat, loc);
            if (!cede) continue;
            SaveBatiment(bat);
            BatimentPrefab.Find(p => p != null && p.getID() == bat.id)?.InitializeBatiment(bat, false);
        }
    }

    static bool EffectuerCession(Batiment bat, Locataire loc)
    {
        var p = Cessions.Due(loc, DateTime.Today);
        if (p == null) return false;
        bool pris = !DossiersDonnees.MemeDossier(loc.Name, p.nom)
            && bat.locataireDuBatiment.Exists(l => l != null && l.id != loc.id && DossiersDonnees.MemeDossier(l.Name, p.nom));
        string err = pris ? "nom déjà pris dans le bâtiment" : null;
        if (pris || !DossiersDonnees.RenommerLocataire(bat.Name, loc.Name, p.nom, out err))
        {
            Debug.LogWarning($"[Cession] « {loc.Name} » → « {p.nom} » reportée au prochain lancement : {err}");
            return false;
        }
        return Cessions.Effectuer(loc, DateTime.Today);
    }

    public void LoadAll()
    {
        _batiments.Clear();

        // Vide les prefabs existants
        foreach (Transform child in batimentsContainer)
            Destroy(child.gameObject);

        if (!Directory.Exists(SaveFolder))
        {
            Debug.Log("[BatimentManager] Aucun dossier de sauvegarde trouvé");
            return;
        }

        Debug.Log(SaveFolder);
        string[] files = Directory.GetFiles(SaveFolder, "*.json");
        var illisibles = new List<string>();

        foreach (string file in files)
        {
            // Chaque fichier est isolé : un JSON tronqué (coupure pendant une
            // écriture) ne doit PAS interrompre la boucle. Sans ce try, l'exception
            // remontait hors de Start() et le menu général n'était jamais ouvert :
            // l'application restait bloquée sur un écran vide, sans message.
            try
            {
                string json = File.ReadAllText(file);
                Batiment data = JsonUtility.FromJson<Batiment>(json);

                if (data != null)
                {
                    // Charges au format « facturée pour tous » → par locataire, d'après
                    // le suivi. Avant la création de la fiche : elle travaille sur une copie.
                    bool reconstitue = ChargeBatiment.Reconstituer(data);
                    // Objectifs à l'ancien format (un seul statut) → importance + avancement.
                    // En mémoire seulement : écrits au prochain enregistrement du bâtiment.
                    Objectifs.Migrer(data.objectifs);
                    if (data.locataireDuBatiment != null)
                        foreach (var l in data.locataireDuBatiment) Objectifs.Migrer(l?.objectifs);
                    // Loyer à paliers : le loyer courant suit le palier du jour. Cession
                    // prévue dont le jour est arrivé : la fiche change de titulaire.
                    bool cede = false;
                    if (data.locataireDuBatiment != null)
                        foreach (var loc in data.locataireDuBatiment)
                        {
                            Loyers.Actualiser(loc, DateTime.Today);
                            cede |= EffectuerCession(data, loc);
                        }
                    _batiments.Add(data);
                    if (reconstitue || cede) SaveBatiment(data);
                    var prefab = SpawnPrefabInPanel(data, batimentsContainerPanel, false);
                    BatimentPrefab.Add(prefab);
                    menuManager.CreateTab(prefab);
                    prefab.RefreshBatimentTabAlert();
                }
                else
                {
                    illisibles.Add(Path.GetFileName(file));
                }
            }
            catch (Exception e)
            {
                illisibles.Add(Path.GetFileName(file));
                Debug.LogError($"[BatimentManager] Fichier illisible ignoré : {Path.GetFileName(file)} — {e.Message}");
            }
        }

        if (BatimentPrefab.Count > 0)
            menuManager.OnSelect(BatimentPrefab[0]);
        Debug.Log($"[BatimentManager] {_batiments.Count} bâtiment(s) chargé(s)");

        // Les fichiers illisibles sont signalés explicitement : un bâtiment qui
        // disparaît en silence est pire qu'une erreur affichée.
        if (illisibles.Count > 0)
            Debug.LogError($"[BatimentManager] {illisibles.Count} fichier(s) non chargé(s) : {string.Join(", ", illisibles)}. " +
                           "Une sauvegarde de secours existe peut-être dans le dossier Backups.");
    }


    // ── Utilitaires ──────────────────────────────────────────────────────────



    // Change la visibilité de SpawnPrefab et ajoute un paramètre parent
    public BatimentPrefab SpawnPrefabInPanel(Batiment data, Transform parent, bool needToModify)
    {
        var go = Instantiate(batimentPrefab, parent);

        // 1. Désactive le CSF AVANT qu'il ne recalcule la taille
        var csf = go.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;

        // 2. Force le stretch pour remplir le parent
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // 3. Si ContentPanel a un VLG, flexibleHeight=1 garantit qu'il prend tout l'espace
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;

        var prefab = go.GetComponent<BatimentPrefab>();
        prefab.InitializeBatiment(data, needToModify);
        return prefab;
    }



    private string GetFilePath(string id) =>
        Path.Combine(SaveFolder, $"batiment_{id}.json");

}