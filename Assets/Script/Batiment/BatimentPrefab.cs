using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BatimentPrefab : PrefabBatLoc
{
    // Caracterristque 
    public InputAndText nameOfTheBuiding;
    public InputAndText tailleBatimentText;
    public InputAndText tailleTerrainText;
    public InputAndText cadastralTxt;      // référence cadastrale (info générale bâtiment)
    public DateInputController acquisitionDate;   // date d'acquisition (saisie manuelle)

    [Header("En-tête fiche")]
    public TMP_Text txtTitreFiche;         // titre du bandeau = nom du bâtiment
    public TMP_Text avatarInitiales;       // initiales dans l'avatar du bandeau

    [Header("Rentabilité")]
    public RentabiliteGlobaleController rentabiliteGlobale;

    public MapController mapController;
    public ObjectivesManager objectivesManager;
    public TMP_Dropdown ParkingDropdown;



    public Button btnPLU;

    private Batiment batiment;

    [Header("Loyer total")]
    public TMP_Text txtLoyerTotalAnnuel;   // Affiché seulement si > 1 locataire
    public GameObject loyerTotalContainer; // Le GameObject parent (label + valeur)


    [Header("Locataire")]
    public List<Locataire> listLocataire = new List<Locataire>();
    public Dictionary<Locataire, LocatairePrefab> dictionnairelocataire = new Dictionary<Locataire, LocatairePrefab>();
    public GameObject locatairePrefab;
    public GameObject locataireContent;
    public MenuManager menulocataire;

    [Header("Vue résumé / fiche complète")]
    public BatimentSummaryView summaryView;   // la vue résumé (carte + liste + bande)
    public GameObject fichePanel;             // le Content Prefab existant (fiche détaillée)
    public Button btnRetourResume;            // dans la fiche : retour au résumé
    public GameObject vueLocatairePanel;      // vue plein écran des fiches locataires
    public Button btnRetourResumeLocataire;   // dans la vue locataire : retour au résumé

    [Header("Liste locataires (lignes cliquables)")]
    public Transform locataireRowContainer;
    public GameObject locataireRowPrefab;

    [Header("Sections repliables")]
    public CollapsibleSection[] sections; // assigner dans l'Inspector
    private bool[] _sectionStateSnapshot;

    [Header("Navigation")]
    public Button btnRetourMenu;


public float GetLoyerTotal()
{
    float total = 0f;
    foreach (var loc in listLocataire)
        total += loc.loyerAnnuel;
    return total;
}
public float GetTailleBatiment() => batiment.tailleBatiment;

    float _depassementSignale;   // dernier dépassement annoncé, pour ne pas le répéter

    /// Surface encore disponible pour un lot dont l'occupant n'a rien saisi : ce qui
    /// reste du bâtiment une fois retirés les lots réellement saisis, partagé à parts
    /// égales entre ceux qui n'ont rien saisi (`tailleLot <= 0` = « pas encore défini »).
    ///
    /// Le bâtiment est la donnée SOURCE : c'est la surface qu'on saisit, et elle ne
    /// bouge jamais toute seule. Avant, il valait la somme des lots dès deux
    /// locataires — ajouter un locataire de 1500 m² à un bâtiment de 1250 m² en
    /// faisait un bâtiment de 2750 m², alors que c'est le lot du premier, hérité par
    /// défaut faute d'avoir pu être saisi, qui devait se réduire d'autant.
    public float TailleLotDisponible(Locataire pour)
        => TailleLotDisponible(batiment.tailleBatiment, listLocataire, pour);

    /// La règle elle-même, sans l'écran : testable, et unique.
    public static float TailleLotDisponible(float tailleBatiment, IList<Locataire> lots, Locataire pour)
    {
        if (lots == null || lots.Count == 0) return tailleBatiment;
        if (pour != null && pour.tailleLot > 0f) return pour.tailleLot;

        float saisies = 0f;
        int auto = 0;
        foreach (var l in lots)
        {
            if (l == null) continue;
            if (l.tailleLot > 0f) saisies += l.tailleLot;
            else auto++;
        }
        float reste = Mathf.Max(0f, tailleBatiment - saisies);   // dépassement : voir TailleLotDepassement
        return auto > 1 ? reste / auto : reste;
    }

    /// Surface réellement occupée par un locataire : la sienne si elle est saisie,
    /// sinon sa part du disponible. Point de passage unique — tout ce qui affiche ou
    /// calcule une surface de lot doit passer par là.
    public float TailleLotEffective(Locataire loc)
        => loc == null ? 0f : (loc.tailleLot > 0f ? loc.tailleLot : TailleLotDisponible(loc));

    /// De combien les lots saisis dépassent la surface du bâtiment (0 si tout rentre).
    /// Ne bloque rien : la saisie est respectée, l'écran avertit.
    public float TailleLotDepassement() => TailleLotDepassement(batiment.tailleBatiment, listLocataire);

    public static float TailleLotDepassement(float tailleBatiment, IList<Locataire> lots)
    {
        if (lots == null) return 0f;
        float saisies = lots.Where(l => l != null && l.tailleLot > 0f).Sum(l => l.tailleLot);
        return Mathf.Max(0f, saisies - tailleBatiment);
    }

    /// Réaffiche les lots dont la surface découle du bâtiment. Ne touche PAS à
    /// `batiment.tailleBatiment` : elle n'appartient qu'à la saisie du bâtiment.
    public void RefreshTailleBatiment()
    {
        if (listLocataire == null) return;
        foreach (var loc in listLocataire)
        {
            if (loc == null || loc.tailleLot > 0f) continue;   // surface saisie : on n'y touche pas
            if (dictionnairelocataire.TryGetValue(loc, out var locPrefab) && locPrefab != null)
                locPrefab.RefreshTailleLot(TailleLotDisponible(loc));
        }

        // Avertissement une fois par dépassement : cette méthode est appelée à chaque
        // enregistrement, un toast à chaque fois serait insupportable.
        float trop = TailleLotDepassement();
        if (trop > 0.01f && Mathf.Abs(trop - _depassementSignale) > 0.01f)
            UndoToast.Instance?.ShowInfo(
                $"Les surfaces des locataires dépassent celle du bâtiment de {trop:0.##} m². "
                + "Corrigez la surface du bâtiment ou celle d'un lot.");
        _depassementSignale = trop;
    }

    // ── Bascule vue résumé / fiche complète ──────────────────────────────────
    // La fiche est masquée via CanvasGroup (invisible mais ACTIVE) pour que
    // le MapController continue de charger la carte → vignette du résumé.

    private CanvasGroup _ficheGroup;
    private CanvasGroup FicheGroup
    {
        get
        {
            if (_ficheGroup == null && fichePanel != null)
            {
                _ficheGroup = fichePanel.GetComponent<CanvasGroup>();
                if (_ficheGroup == null)
                    _ficheGroup = fichePanel.AddComponent<CanvasGroup>();
            }
            return _ficheGroup;
        }
    }

    public void ShowSummary()
    {
        if (FicheGroup != null)
        {
            FicheGroup.alpha = 0f;
            FicheGroup.interactable = false;
            FicheGroup.blocksRaycasts = false;
        }
        vueLocatairePanel?.SetActive(false);
        if (summaryView != null)
        {
            summaryView.gameObject.SetActive(true);
            summaryView.Refresh();
        }
    }

    public void ShowFiche()
    {
        if (summaryView != null) summaryView.gameObject.SetActive(false);
        vueLocatairePanel?.SetActive(false);
        if (FicheGroup != null)
        {
            FicheGroup.alpha = 1f;
            FicheGroup.interactable = true;
            FicheGroup.blocksRaycasts = true;
        }
    }

    // Met à jour le bandeau de la fiche (titre = nom du bâtiment, avatar = initiales)
    private void RefreshFicheHeader()
    {
        string nom = (batiment == null || string.IsNullOrWhiteSpace(batiment.Name)) ? "Sans nom" : batiment.Name;
        if (txtTitreFiche != null) txtTitreFiche.text = nom;
        if (avatarInitiales != null) avatarInitiales.text = Initiales(nom);
    }

    private static string Initiales(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "?";
        var p = s.Trim().Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (p.Length >= 2) return ("" + char.ToUpper(p[0][0]) + char.ToUpper(p[1][0]));
        string t = s.Trim();
        return t.Substring(0, Math.Min(2, t.Length)).ToUpper();
    }

    public void ShowLocataireView()
    {
        if (summaryView != null) summaryView.gameObject.SetActive(false);
        if (FicheGroup != null)
        {
            FicheGroup.alpha = 0f;
            FicheGroup.interactable = false;
            FicheGroup.blocksRaycasts = false;
        }
        vueLocatairePanel?.SetActive(true);
    }

    // ── Liste des locataires (lignes compactes triées par urgence) ───────────

    public void RebuildLocataireRows()
    {
        if (locataireRowContainer == null || locataireRowPrefab == null) return;

        foreach (Transform child in locataireRowContainer)
            Destroy(child.gameObject);

        var tries = listLocataire.OrderBy(TriUrgence).ThenBy(l => l.lotBatiment).ToList();
        foreach (var loc in tries)
        {
            var go = Instantiate(locataireRowPrefab, locataireRowContainer);
            var l = loc;
            go.GetComponent<LocataireRowUI>().Setup(l, () =>
            {
                ShowLocataireView();   // la ligne ouvre la fiche du locataire
                if (dictionnairelocataire.TryGetValue(l, out var prefab))
                    menulocataire.OnSelect(prefab);
            });
        }
    }

    // 0 = retard, 1 = à initialiser, 2 = bientôt, 3 = ok
    private static int TriUrgence(Locataire loc)
    {
        bool initialise = !string.IsNullOrEmpty(loc.indiceImmoAuDepart)
                          && loc.indiceImmoAuDepart != "—";
        if (!initialise) return 1;
        double jours = (loc.MoisDeRevision - DateTime.Now).TotalDays;
        if (jours < 0) return 0;
        if (jours <= 90) return 2;
        return 3;
    }

    public void RefreshLoyerTotal()
    {
        if (loyerTotalContainer == null) return;

        bool multiLocataire = listLocataire.Count > 1;
        loyerTotalContainer.SetActive(multiLocataire);

        if (multiLocataire && txtLoyerTotalAnnuel != null)
        {
            float total = GetLoyerTotal();
            txtLoyerTotalAnnuel.text = $"{total:F2} € / an";
        }
        rentabiliteGlobale?.Refresh();
        if (summaryView != null && summaryView.gameObject.activeSelf)
            summaryView.Refresh();
    }

    // Charge la date d'acquisition : valeur saisie si présente, sinon date d'achat
    // la plus ancienne (défaut pratique), sinon aujourd'hui.
    private void LoadDateAcquisition()
    {
        if (acquisitionDate == null || batiment == null) return;
        DateTime d;
        if (!DateTime.TryParse(batiment.dateAcquisitionISO, out d))
        {
            DateTime m = DateTime.MaxValue;
            if (batiment.historiquesAchat != null)
                foreach (var a in batiment.historiquesAchat)
                    if (DateTime.TryParse(a.dateAchat, out var da) && da < m) m = da;
            d = m != DateTime.MaxValue ? m : DateTime.Today;
        }
        acquisitionDate.ApplyDate(d);
    }

    // Point d'alerte de l'onglet bâtiment : révision de loyer en retard (un
    // locataire) OU objectif obligatoire non fait (bâtiment ou locataire).
    public void RefreshBatimentTabAlert()
    {
        if (batiment == null) return;

        bool alerte = false;
        foreach (var loc in listLocataire)
            if (loc != null && LoyerSummaryUI.EstRevisionDue(loc)) { alerte = true; break; }

        if (!alerte && AUnObjectifObligatoire(batiment.objectifs)) alerte = true;
        if (!alerte)
            foreach (var loc in listLocataire)
                if (loc != null && AUnObjectifObligatoire(loc.objectifs)) { alerte = true; break; }

        BatimentManager.Instance.menuManager?.SetTabAlert(this, alerte);
    }

    private static bool AUnObjectifObligatoire(ObjectiveList list)
    {
        if (list?.items == null) return false;
        foreach (var o in list.items)
            if (o.status == Objective.ObjectiveStatus.Obligatoire) return true;
        return false;
    }

    public override void InitializeBatiment(Batiment newBatiment, bool NeedToModify)
    {
        foreach (var locPrefab in dictionnairelocataire.Values)
        {
            menulocataire.RemoveTabAndBuilding(locPrefab);
            Destroy(locPrefab.gameObject);
        }
        listLocataire.Clear();
        dictionnairelocataire.Clear();
        ParkingDropdown.ClearOptions();

        // ── Listeners — RemoveAllListeners AVANT d'ajouter ───────────────────
        btnPLU.onClick.RemoveAllListeners();
        btnPLU.onClick.AddListener(() =>
            PLUOverlayPanel.Instance.OpenWithBatiment(mapController.GetAdress(), batiment.cadastral));

        btnRetourMenu.onClick.RemoveAllListeners();
        btnRetourMenu.onClick.AddListener(() =>
            BatimentManager.Instance.menuManager.OpenGeneralMenu());

        if (btnRetourResume != null)
        {
            btnRetourResume.onClick.RemoveAllListeners();
            btnRetourResume.onClick.AddListener(ShowSummary);
        }

        if (btnRetourResumeLocataire != null)
        {
            btnRetourResumeLocataire.onClick.RemoveAllListeners();
            btnRetourResumeLocataire.onClick.AddListener(ShowSummary);
        }
      



        var options = System.Enum.GetNames(typeof(ParkingState))
            .Select(name => new TMP_Dropdown.OptionData(name))
            .ToList();

        ParkingDropdown.AddOptions(options);
        save.onClick.RemoveAllListeners();
        objectivesManager.AddNeObjectif.RemoveAllListeners();
        modifyBatiment.onClick.RemoveAllListeners();
        Delete.onClick.RemoveAllListeners();
        objectivesManager.AddNeObjectif.AddListener(() => updateListObjectif());
        save.onClick.AddListener(() => SaveBatiment());
        modifyBatiment.onClick.AddListener(() => Modify());
       

        if (!NeedToModify)
        {
       
            string json = JsonUtility.ToJson(newBatiment);
            batiment = JsonUtility.FromJson<Batiment>(json);
            if (batiment.historiquesAchat == null)
                batiment.historiquesAchat = new List<AchatFinancement>();
            if (batiment.travaux == null)
                batiment.travaux = new List<TravauxFinancement>();
            if (batiment.charges == null)
                batiment.charges = new List<ChargeBatiment>();
            rentabiliteGlobale?.Init(this);
            mapController.SetAdress(batiment.adressBatiment);
            tailleBatimentText.ApplySave ( batiment.tailleBatiment.ToString());
            tailleTerrainText.ApplySave(batiment.tailleTerrain.ToString());
            ParkingDropdown.value = (int)batiment.parkingEtat;
            ParkingDropdown.interactable = false;
            
            nameOfTheBuiding.ApplySave(batiment.Name.ToString());
            RefreshFicheHeader();
            cadastralTxt?.ApplySave(batiment.cadastral ?? "");
            LoadDateAcquisition();
            objectivesManager.LoadObjectives(batiment.objectifs);
            save.gameObject.SetActive(false);
            modifyBatiment.gameObject.SetActive(true);
            Delete.gameObject.SetActive(true);
            
            for(int i =0; i<batiment.locataireDuBatiment.Count; i++)
            {
                listLocataire.Add(batiment.locataireDuBatiment[i]);
              var locatPrefab= SpawnPrefabLocataireInPanel(batiment.locataireDuBatiment[i], locataireContent.transform, false);
                dictionnairelocataire.Add(batiment.locataireDuBatiment[i], locatPrefab);
                menulocataire.CreateTab(locatPrefab);

            }
            if (listLocataire.Count > 0)
                menulocataire.OnSelect(dictionnairelocataire.First().Value);
            RefreshTailleBatiment();
            RefreshLoyerTotal();
            RebuildLocataireRows();

            // Bâtiment existant → on arrive sur la vue résumé
            summaryView?.Init(this);
            ShowSummary();
        }
        else
        {

            string json = JsonUtility.ToJson(newBatiment);
            batiment = JsonUtility.FromJson<Batiment>(json);

            if (batiment.historiquesAchat == null)
                batiment.historiquesAchat = new List<AchatFinancement>();
            if (batiment.travaux == null)
                batiment.travaux = new List<TravauxFinancement>();
            if (batiment.charges == null)
                batiment.charges = new List<ChargeBatiment>();
            rentabiliteGlobale?.Init(this);

            // Nouveau bâtiment → direct sur la fiche en mode édition
            summaryView?.Init(this);
            ShowFiche();
            Modify();
        }

        Delete.onClick.AddListener(() =>
            ConfirmDialog.Instance.Show(
                "Supprimer le bâtiment",
                $"Supprimer « {batiment.Name} » et tous ses locataires ?",
                () =>
                {
                    // Clone AVANT suppression pour pouvoir restaurer
                    string backup = JsonUtility.ToJson(batiment);
                    string nom = batiment.Name;
                    // Racine au moment de la suppression. SaveFolder est recalculé à
                    // l'écriture : si l'utilisatrice change d'entreprise avant de cliquer
                    // « Annuler », la restauration écrirait le bâtiment de l'entreprise A
                    // dans le dossier de l'entreprise B.
                    string racineOrigine = SaveLocationService.GetSaveRoot();
                    BatimentManager.Instance.DeleteBatiment(batiment.id);

                    UndoToast.Instance.Show($"« {nom} » supprimé",
                        () =>
                        {
                            if (!string.Equals(SaveLocationService.GetSaveRoot(), racineOrigine,
                                               System.StringComparison.OrdinalIgnoreCase))
                            {
                                UndoToast.Instance?.ShowInfo(
                                    "Restauration impossible : l'entreprise active a changé depuis la suppression.");
                                return;
                            }
                            BatimentManager.Instance.RestoreBatiment(backup);
                        });
                }));

        TransfertEntrepriseUI.AjouterBouton(Delete, () => TransfertEntrepriseUI.OuvrirBatiment(this));
    }



    private void updateListObjectif()
    {
        batiment.objectifs = objectivesManager.GetAllTheObjectif();
        BatimentManager.Instance.SaveBatiment(batiment);
        RefreshBatimentTabAlert();
    }


    public LocatairePrefab SpawnPrefabLocataireInPanel(Locataire data, Transform parent, bool needToModify)
    {
        var go = Instantiate(locatairePrefab, parent);
        var prefab = go.GetComponent<LocatairePrefab>();
        prefab.batimentPrefabOrigin = this;
        // Câbler automatiquement le ScrollAutoResize
        prefab.locataireScrollContent =locataireContent.GetComponent<ScrollAutoResize>();
        prefab.InitializeLocataire(data, needToModify);
        return prefab;
    }


    public LocatairePrefab Addlocataire(bool needToModify)
    {

        var data = new Locataire();

        // Horodate la fiche et y duplique les réglages de facture du locataire créé
        // juste avant (tous bâtiments confondus). À faire AVANT de construire la fiche :
        // le panneau lit ces valeurs à son ouverture.
        HeritageFacture.Appliquer(data, HeritageFacture.TousLesLocataires());

        var prefab = SpawnPrefabLocataireInPanel(data, menulocataire.ContentPrefab.transform, needToModify);
        listLocataire.Add(data);
        dictionnairelocataire.Add(data, prefab);
        batiment.locataireDuBatiment.Add(data);

        RefreshTailleBatiment();
        RefreshLoyerTotal(); // ← ajouter
        RebuildLocataireRows();
        return prefab;


    }

    public void DeleteLocataire(string id)
    {
        var locataire = listLocataire.Find(b => b.id == id);
        listLocataire.RemoveAll(b => b.id == id);
        var tab = dictionnairelocataire[locataire];
        dictionnairelocataire.Remove(locataire);
        if (tab != null)
        {
            menulocataire.RemoveTabAndBuilding(tab);
            Destroy(tab.gameObject);
        }
      
        batiment.locataireDuBatiment.Remove(locataire);
        RefreshTailleBatiment();
        RefreshLoyerTotal(); // ← ajouter
        RebuildLocataireRows();
        BatimentManager.Instance.SaveBatiment(batiment);
    }

    public void RestoreLocataire(string json)
    {
        var data = JsonUtility.FromJson<Locataire>(json);
        if (data == null) return;

        var prefab = SpawnPrefabLocataireInPanel(data, menulocataire.ContentPrefab.transform, false);
        listLocataire.Add(data);
        dictionnairelocataire.Add(data, prefab);
        batiment.locataireDuBatiment.Add(data);
        menulocataire.CreateTab(prefab);
        menulocataire.OnSelect(prefab);

        RefreshTailleBatiment();
        RefreshLoyerTotal();
        BatimentManager.Instance.SaveBatiment(batiment);
    }


    public void SaveAfterModifyToDoListLocataire()
    {
        RefreshTailleBatiment();
        RefreshLoyerTotal();
        RebuildLocataireRows();
        BatimentManager.Instance.SaveBatiment(batiment);
        RefreshBatimentTabAlert();
        // Le suivi de facturation dépend du loyer, du dépôt et de la périodicité :
        // il doit suivre CHAQUE enregistrement, pas seulement ceux qui viennent d'un
        // panneau de facture. Sinon régler le loyer ou le dépôt d'une fiche nouvelle
        // laissait un tableau vide jusqu'au redémarrage de l'application.
        LocataireSuiviInline.RefreshTous();
    }

    /// Ouvre la galerie photos de ce bâtiment (overlay de scène).
    public void OpenPhotos()
    {
        PhotoGalleryController.Instance?.Ouvrir(this);
    }

    public override void Modify()
    {
        nameOfTheBuiding.Modify();
        // Toujours éditable : la surface du bâtiment est SA donnée, pas une somme.
        // Elle était verrouillée dès deux locataires, ce qui interdisait de la corriger
        // alors même que l'app venait de la remplacer par la somme des lots.
        tailleBatimentText.Modify();
        tailleTerrainText.Modify();
        cadastralTxt?.Modify();
        acquisitionDate?.ModifyDate();
        mapController.ModifyAdress();
        ParkingDropdown.interactable = true;
        save.gameObject.SetActive(true);
        modifyBatiment.gameObject.SetActive(false);
        Delete.gameObject.SetActive(true);
        _sectionStateSnapshot = new bool[sections.Length];
        for (int i = 0; i < sections.Length; i++)
            _sectionStateSnapshot[i] = sections[i].IsOpen;
        foreach (var s in sections) s.Open();
    }


    public override void SaveBatiment()
    {
    
        // ── Nom : unicité imposée + dossier déplacé si le nom change ──────────────
        // Les dossiers sont nommés par le NOM : deux homonymes partageraient factures
        // et photos, et un renommage sans déplacement laisserait tout orphelin sous
        // l'ancien nom. Les deux cas sont donc bloqués AVANT d'écrire quoi que ce soit.
        string ancienNom = batiment.Name;
        string nouveauNom = nameOfTheBuiding.GetNewSave();

        if (!DossiersDonnees.MemeDossier(ancienNom, nouveauNom))
        {
            var mgr = BatimentManager.Instance;
            if (mgr != null)
                foreach (var autre in mgr.Batiments)
                {
                    if (autre == null || autre.id == batiment.id) continue;
                    if (!DossiersDonnees.MemeDossier(autre.Name, nouveauNom)) continue;

                    UndoToast.Instance?.ShowInfo(
                        $"Un bâtiment nommé « {autre.Name} » existe déjà. Choisissez un autre nom : " +
                        "les dossiers de factures et de photos portent le nom du bâtiment.");
                    nameOfTheBuiding.ApplySave(ancienNom);
                    return;
                }

            if (!DossiersDonnees.RenommerBatiment(ancienNom, nouveauNom, out string errRenom))
            {
                UndoToast.Instance?.ShowInfo(
                    $"Renommage impossible ({errRenom}). Fermez les fichiers ouverts de ce bâtiment et réessayez.");
                nameOfTheBuiding.ApplySave(ancienNom);
                return;
            }

            // Le dossier a bougé : les chemins de photos doivent suivre.
            string cover = batiment.coverPhoto;
            DossiersDonnees.ReporterPhotos(batiment.photos, ref cover, nouveauNom);
            batiment.coverPhoto = cover;
        }

        batiment.Name = nouveauNom;
        // La surface saisie fait foi ; les lots non définis se replient dessus.
        SaveCorrectlyFloat(ref batiment.tailleBatiment, tailleBatimentText.GetNewSave());
        RefreshTailleBatiment();
        SaveCorrectlyFloat(ref batiment.tailleTerrain, tailleTerrainText.GetNewSave());
        Debug.Log(batiment.tailleBatiment);
        batiment.adressBatiment = mapController.GetAdress();
        if (cadastralTxt != null) batiment.cadastral = cadastralTxt.GetNewSave();
        if (acquisitionDate != null)
            batiment.dateAcquisitionISO = acquisitionDate.saveThedate().ToString("yyyy-MM-dd");
        batiment.parkingEtat = (ParkingState)ParkingDropdown.value;
        ParkingDropdown.interactable = false;
        BatimentManager.Instance.menuManager.UpdateTabLabel(this, batiment.Name);
        BatimentManager.Instance.SaveBatiment(batiment);
        save.gameObject.SetActive(false);
        modifyBatiment.gameObject.SetActive(true);
        Delete.gameObject.SetActive(true);
        rentabiliteGlobale?.Refresh();
        if (_sectionStateSnapshot != null)
            for (int i = 0; i < sections.Length; i++)
                sections[i].SetOpen(_sectionStateSnapshot[i]);
        InitializeBatiment(batiment, false);
        ShowFiche();   // après sauvegarde : rester sur la fiche complète (pas de retour au résumé)
        RefreshBatimentTabAlert();
    }
   public override string getName()
    {
        return batiment.Name;
    }
    public override  string getID()
    {
        return batiment.id;
    }

    public Batiment getBatiment()
    {
        return batiment;
    }

   


}
