using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LocatairePrefab : PrefabBatLoc
{
    public ScrollAutoResize locataireScrollContent;
    public InputAndText nameOfLocataire;
    public InputAndText siret;
    public InputAndText codeComptableTxt;
    public InputAndText emailLocataireTxt;
    public InputAndText telephoneLocataireTxt;
    public InputAndText mapController;
    public InputAndText lotBatimentTxt;
    public InputAndText tailleLotTxt;
    public TMP_Dropdown typedeBailDropDown;

    public DateInputController dateDebutBail;
    public DateInputController dateFinBail;
    public InputAndText depotDeGarantieTxt;
    public InputAndText tauxDeRentabilité;

    [Header("Loyer")]
    public LoyerSummaryUI loyerSummary;

    // Ligne du tableau de suivi de facturation. Elle est portée ici parce que les deux
    // vues du suivi sont créées par code (AddComponent / new GameObject) et ne peuvent
    // donc pas recevoir de référence par l'inspecteur ; la fiche, elle, est un prefab.
    [Header("Facturation")]
    public GameObject suiviRowPrefab;

    [Header("Bail")]
    public BailFileUI bailFile;
    public GameObject badgeBail;     // pastille "Renouvellement à prévoir" / "Bail expiré"
    public TMP_Text badgeBailTxt;
    public Image badgeBailBg;

    [Header("En-tête fiche")]
    public Image headerAvatarBg;
    public TMP_Text headerInitiales;
    public TMP_Text headerNom;
    public TMP_Text headerSousTitre;
    public Button headerBtnResume;   // bouton "← Résumé" dans la bande titre

    public ObjectivesManager objectivesManager;
    public string id;
    public BatimentPrefab batimentPrefabOrigin;

    public Button pappersBtn;
    public Button resumeSocieteBtn;
    public PapperService papperService;
    public InputAndText Commentaire;
    private LocataireFacturationFields facturationFields;
    private LocataireBailFields bailFields;

    [Header("Sections repliables")]
    public CollapsibleSection[] sections;
    private bool[] _sectionStateSnapshot;


    // ─────────────────────────────────────────────────────────────────────────


    /// Le locataire de cette fiche, ou null si elle n'est rattachée à aucun bâtiment.
    ///
    /// Le cas existe pour de bon : `Resources.FindObjectsOfTypeAll` ramène aussi le
    /// PREFAB d'asset, jamais instancié, dont `batimentPrefabOrigin` est nul. Exploser
    /// ici faisait tomber l'appelant — vu sur la mise à jour des résumés d'entreprise.
    public Locataire GetLocataire()
        => batimentPrefabOrigin != null && batimentPrefabOrigin.listLocataire != null
            ? batimentPrefabOrigin.listLocataire.Find(b => b.id == id)
            : null;

    /// Appelé par le RevisionPanel après initialisation ou révision


    public void RefreshRevisionAlert(Locataire loc)
    {
        // Le point rouge d'onglet couvre révision de loyer ET renouvellement de bail.
        bool due = LoyerSummaryUI.EstRevisionDue(loc)
                   || Locataire.RenouvellementProche(loc, out _)
                   || Locataire.ResiliationProche(loc, out _, out _)
                   || FacturationAlertes.AUrgent(loc);
        batimentPrefabOrigin.menulocataire.SetTabAlert(this, due);
        batimentPrefabOrigin.RefreshBatimentTabAlert();
        RefreshBailAlert(loc);
    }

    /// Badge de la section Bail : visible quand le bail se termine dans moins de
    /// 9 mois (ambre) ou est déjà expiré (terracotta) ; sinon, quand le preneur peut
    /// donner congé pour une échéance triennale proche (ambre). Toujours avec les
    /// dates qui comptent (voir Locataire.TexteFinDeBail).
    public void RefreshBailAlert(Locataire loc)
    {
        if (badgeBail == null) return;
        bool proche = Locataire.RenouvellementProche(loc, out int jours);
        DateTime sortie = default, limite = default;
        bool resiliation = !proche && Locataire.ResiliationProche(loc, out sortie, out limite);
        badgeBail.SetActive(proche || resiliation);
        if (resiliation)
        {
            ColorUtility.TryParseHtmlString("#B26A0C", out var ambre);
            if (badgeBailTxt != null)
            {
                // La date limite d'abord : c'est elle qui compte (après, le preneur ne peut plus partir).
                badgeBailTxt.text = $"Résiliation possible — congé jusqu'au {limite:dd/MM/yyyy} (sortie le {sortie:dd/MM/yyyy})";
                badgeBailTxt.color = Color.white;
            }
            if (badgeBailBg != null) badgeBailBg.color = ambre;
            return;
        }
        if (!proche) return;

        bool expire = jours < 0;
        // Pastille pleine couleur + texte blanc pour bien la faire ressortir :
        // rouge si le bail est expiré (urgent), ambre soutenu si renouvellement à prévoir.
        Color bgc;
        ColorUtility.TryParseHtmlString(expire ? "#A32D2D" : "#B26A0C", out bgc);
        if (badgeBailTxt != null)
        {
            badgeBailTxt.text = Locataire.TexteFinDeBail(loc, DateTime.Today);
            badgeBailTxt.color = Color.white;
        }
        if (badgeBailBg != null)
            badgeBailBg.color = bgc;
    }

    public void OnEnable()
    {
        // (Re)configure le défilement quand la fiche devient visible : au premier
        // affichage le layout n'est pas encore stabilisé, la plage scrollable
        // n'était donc pas recalculée (il fallait Modifier+Save pour un re-init).
        EnsureScrollable();
        locataireScrollContent?.SetDirty();
    }

    public void RefreshTailleLot(float value)
    {
        tailleLotTxt.ApplySave(value.ToString());
    }

    // ── En-tête de fiche (avatar + nom + bâtiment · lot) ─────────────────────

    public void RefreshHeader(Locataire loc)
    {
        if (loc == null) return;
        string nom = string.IsNullOrEmpty(loc.Name) ? "Nouveau locataire" : loc.Name;

        if (headerBtnResume != null && batimentPrefabOrigin != null)
        {
            headerBtnResume.onClick.RemoveAllListeners();
            headerBtnResume.onClick.AddListener(batimentPrefabOrigin.ShowSummary);
        }

        if (headerNom != null) headerNom.text = nom;
        if (headerInitiales != null) headerInitiales.text = Initiales(nom);
        if (headerSousTitre != null)
        {
            string batNom = batimentPrefabOrigin != null ? batimentPrefabOrigin.getName() : "";
            headerSousTitre.text = string.IsNullOrEmpty(batNom)
                ? $"Lot {loc.lotBatiment}"
                : $"{batNom} · Lot {loc.lotBatiment}";
        }
        if (headerAvatarBg != null)
        {
            bool due = LoyerSummaryUI.EstRevisionDue(loc);
            headerAvatarBg.color = due ? UITheme.AlerteClair : UITheme.PrimaireClair;
            if (headerInitiales != null)
                headerInitiales.color = due ? UITheme.AlerteTexte : UITheme.Primaire;
        }
    }

    private static string Initiales(string nom)
    {
        var mots = nom.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (mots.Length == 0) return "?";
        if (mots.Length == 1) return mots[0].Substring(0, Mathf.Min(2, mots[0].Length)).ToUpper();
        return (mots[0].Substring(0, 1) + mots[1].Substring(0, 1)).ToUpper();
    }

    public override void InitializeLocataire(Locataire newLocataire, bool NeedToModify)
    {
        // Section Bail (liste des types, durée, années fermes, fin calculée).
        if (bailFields == null) bailFields = gameObject.AddComponent<LocataireBailFields>();
        bailFields.EnsureBuilt(this);
        bailFields.Load(newLocataire);


        id = newLocataire.id;
        loyerSummary.Init(this);
        bailFile?.Init(this);
        RefreshHeader(newLocataire);

        if (facturationFields == null) facturationFields = gameObject.AddComponent<LocataireFacturationFields>();
        facturationFields.EnsureBuilt(this);
        BuildSiretRow();




        // Listeners boutons
        save.onClick.RemoveAllListeners();
        objectivesManager.AddNeObjectif.RemoveAllListeners();
        modifyBatiment.onClick.RemoveAllListeners();
        Delete.onClick.RemoveAllListeners();

        save.onClick.AddListener(() => SaveLocataire());
        modifyBatiment.onClick.AddListener(() => Modify());
        objectivesManager.AddNeObjectif.AddListener(() => updateListObjectif());
        pappersBtn.onClick.RemoveAllListeners();
        pappersBtn.onClick.AddListener(OnPappersClick);
        // Le bouton « résumé société » a disparu : le résumé se met à jour seul au
        // lancement, pour TOUS les locataires (PappersSync). Une information à jour
        // uniquement là où l'on avait pensé à cliquer ne valait pas grand-chose.
        if (resumeSocieteBtn != null) resumeSocieteBtn.gameObject.SetActive(false);

        if (!NeedToModify)
        {
            // ── Chargement locataire existant ────────────────────────────────
            nameOfLocataire.ApplySave(newLocataire.Name);
            codeComptableTxt.ApplySave(newLocataire.codeCompatable);
            emailLocataireTxt.ApplySave(newLocataire.emailLocataire);
            telephoneLocataireTxt.ApplySave(newLocataire.telephoneLocataire);
            mapController.ApplySave(newLocataire.adresseLocataire);
            lotBatimentTxt.ApplySave(newLocataire.lotBatiment.ToString());
            tailleLotTxt.ApplySave(batimentPrefabOrigin.TailleLotEffective(newLocataire).ToString());

            loyerSummary.Refresh(newLocataire);
            RefreshRevisionAlert(newLocataire);

            dateDebutBail.ApplyDate(newLocataire.DateDebutBail);
            dateFinBail.ApplyDate(newLocataire.DateFinBail);
            depotDeGarantieTxt.ApplySave(newLocataire.depotDeGarantie.ToString());
            tauxDeRentabilité.ApplySave(newLocataire.tauxDeRentabilité.ToString());
            siret.ApplySave(newLocataire.siretNumber);
            Commentaire.ApplySave(newLocataire.commentaire);
            facturationFields.Load(newLocataire);

     
            save.gameObject.SetActive(false);
            modifyBatiment.gameObject.SetActive(true);
            Delete.gameObject.SetActive(true);
        }
        else
        {
            // ── Nouveau locataire : l'utilisateur choisit le mode ────────────
            loyerSummary.Refresh(newLocataire);
            Modify();
        }

        Delete.onClick.AddListener(() =>
        {
            var loc = batimentPrefabOrigin.listLocataire.Find(b => b.id == id);
            string nom = string.IsNullOrEmpty(loc?.Name) ? "ce locataire" : loc.Name;

            ConfirmDialog.Instance.Show(
                "Supprimer le locataire",
                $"Supprimer « {nom} » ?",
                () =>
                {
                    string backup = JsonUtility.ToJson(loc);
                    var bp = batimentPrefabOrigin;
                    bp.DeleteLocataire(id);

                    UndoToast.Instance.Show($"« {nom} » supprimé",
                        () => bp.RestoreLocataire(backup));
                });
        });
        EnsureScrollable();
        locataireScrollContent?.SetDirty();
    }

    // Rend la fiche défilable verticalement : son contenu dépasse la fenêtre depuis
    // l'ajout des champs RIB / facturation. Idempotent (appelé à chaque ouverture).
    private void EnsureScrollable()
    {
        if (locataireScrollContent == null) return;
        var content = locataireScrollContent.GetComponent<RectTransform>();
        var scr = locataireScrollContent.GetComponentInParent<ScrollRect>();
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.childForceExpandHeight = false;
        var csf = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);

        if (scr != null)
        {
            scr.vertical = true;
            scr.movementType = ScrollRect.MovementType.Clamped;
            scr.scrollSensitivity = 40f;
            // Barre de défilement toujours visible et ramenée dans la zone à l'écran
            // (la fiche déborde légèrement à droite → la barre tomberait hors écran).
            var vbar = scr.verticalScrollbar;
            if (vbar != null)
            {
                scr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                vbar.gameObject.SetActive(true);
                var vrt = (RectTransform)vbar.transform;
                vrt.anchorMin = new Vector2(1, 0);
                vrt.anchorMax = new Vector2(1, 1);
                vrt.pivot = new Vector2(1, 1);
                vrt.sizeDelta = new Vector2(16, 0);
                vrt.anchoredPosition = new Vector2(-26, 0);
                var barImg = vbar.GetComponent<Image>();
                if (barImg != null) barImg.color = new Color(0f, 0f, 0f, 0.08f);
                if (vbar.handleRect != null)
                {
                    var hImg = vbar.handleRect.GetComponent<Image>();
                    if (hImg != null) hImg.color = new Color(0.30f, 0.30f, 0.30f, 0.9f);
                }
            }
        }

        // Le layout (CSF/TMP) n'est pas stabilisé sur la même frame : on force un
        // recalcul sur les 2 frames suivantes pour que la plage scrollable soit
        // correcte dès le premier affichage.
        if (isActiveAndEnabled) StartCoroutine(RecomputeScrollNextFrames());
    }

    private System.Collections.IEnumerator RecomputeScrollNextFrames()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (locataireScrollContent == null) yield break;
        var content = locataireScrollContent.GetComponent<RectTransform>();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
    }

    public void SaveLocataire()
    {
        var locataire = batimentPrefabOrigin.listLocataire.Find(b => b.id == id);
        var index = batimentPrefabOrigin.listLocataire.IndexOf(locataire);

        // Le bail d'abord (années fermes au-delà de la durée…) : s'il ne tient pas
        // debout, rien n'est enregistré et la fiche reste en modification.
        string erreurBail = bailFields != null ? bailFields.Verifier() : null;
        if (erreurBail != null) { UndoToast.Instance?.ShowInfo(erreurBail); return; }

        // ── Nom : unicité DANS CE BÂTIMENT + dossier déplacé si le nom change ─────
        // Le dossier du locataire vit à l'intérieur de celui de son bâtiment : deux
        // locataires homonymes dans DEUX bâtiments différents ne se gênent donc pas et
        // restent autorisés. Seul un doublon au sein du même bâtiment est refusé.
        string ancienNomLoc = locataire.Name;
        string nouveauNomLoc = nameOfLocataire.GetNewSave();
        string nomBat = batimentPrefabOrigin.getName();

        if (!DossiersDonnees.MemeDossier(ancienNomLoc, nouveauNomLoc))
        {
            foreach (var autre in batimentPrefabOrigin.listLocataire)
            {
                if (autre == null || autre.id == locataire.id) continue;
                if (!DossiersDonnees.MemeDossier(autre.Name, nouveauNomLoc)) continue;

                UndoToast.Instance?.ShowInfo(
                    $"Un locataire nommé « {autre.Name} » existe déjà dans ce bâtiment. " +
                    "Choisissez un autre nom : le dossier de factures porte le nom du locataire.");
                nameOfLocataire.ApplySave(ancienNomLoc);
                return;
            }

            if (!DossiersDonnees.RenommerLocataire(nomBat, ancienNomLoc, nouveauNomLoc, out string errLoc))
            {
                UndoToast.Instance?.ShowInfo(
                    $"Renommage impossible ({errLoc}). Fermez les fichiers ouverts de ce locataire et réessayez.");
                nameOfLocataire.ApplySave(ancienNomLoc);
                return;
            }
        }

        locataire.Name = nouveauNomLoc;
        locataire.codeCompatable = codeComptableTxt.GetNewSave();
        locataire.emailLocataire = emailLocataireTxt.GetNewSave();
        locataire.telephoneLocataire = telephoneLocataireTxt.GetNewSave();
        locataire.adresseLocataire = mapController.GetNewSave();

        SaveCorrectlyInt(ref locataire.lotBatiment, lotBatimentTxt.GetNewSave());

        // Surface du lot : toujours saisissable. Si la valeur rendue est encore
        // celle qui était proposée (le disponible du bâtiment), on la laisse « non
        // définie » (0) — ainsi elle continue de suivre le bâtiment quand un autre
        // locataire prend sa part, au lieu de figer un chiffre jamais choisi.
        float saisie = locataire.tailleLot;
        SaveCorrectlyFloat(ref saisie, tailleLotTxt.GetNewSave());
        float propose = batimentPrefabOrigin.TailleLotDisponible(locataire);
        locataire.tailleLot = Mathf.Abs(saisie - propose) < 0.01f ? 0f : saisie;
        batimentPrefabOrigin.RefreshTailleBatiment();

        bailFields?.Save(locataire);
        locataire.DateDebutBail = dateDebutBail.saveThedate();
        locataire.DateFinBail = dateFinBail.saveThedate();

        SaveCorrectlyFloat(ref locataire.tauxDeRentabilité, tauxDeRentabilité.GetNewSave());
        locataire.siretNumber = siret.GetNewSave();
        locataire.commentaire = Commentaire.GetNewSave();
        facturationFields?.Save(locataire);

        batimentPrefabOrigin.menulocataire.UpdateTabLabel(this, locataire.Name);
        batimentPrefabOrigin.listLocataire[index] = locataire;

        save.gameObject.SetActive(false);
        modifyBatiment.gameObject.SetActive(true);
        Delete.gameObject.SetActive(true);

        batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();

        if (_sectionStateSnapshot != null)
            for (int i = 0; i < sections.Length; i++)
                sections[i].SetOpen(_sectionStateSnapshot[i]);

        loyerSummary.Refresh(locataire);
        RefreshRevisionAlert(locataire);

        InitializeLocataire(locataire, false);

        // Le SIRET vient peut-être d'être saisi ou corrigé : on rafraîchit le résumé
        // d'entreprise tout de suite, plutôt que d'attendre le prochain lancement.
        // Même chemin que la mise à jour du démarrage, donc même règle.
        StartCoroutine(PappersSync.Un(batimentPrefabOrigin, locataire));
        locataireScrollContent?.SetDirty();
    }

    public override void Modify()
    {
        nameOfLocataire.Modify();
        codeComptableTxt.Modify();
        emailLocataireTxt.Modify();
        telephoneLocataireTxt.Modify();
        mapController.Modify();
        lotBatimentTxt.Modify();
        // Toujours saisissable, pré-rempli avec le disponible : le lot du premier
        // locataire était imposé, donc impossible à réduire quand un second arrivait.
        tailleLotTxt.ApplyValue(batimentPrefabOrigin.TailleLotEffective(GetLocataire()).ToString());
        tailleLotTxt.Modify();
        bailFields?.Modify();
        dateDebutBail.ModifyDate();
        dateFinBail.ModifyDate();
        depotDeGarantieTxt.Modify();
        tauxDeRentabilité.Modify();
        save.gameObject.SetActive(true);
        modifyBatiment.gameObject.SetActive(false);
        Delete.gameObject.SetActive(true);
        siret.Modify();
        Commentaire.Modify();
        facturationFields?.Modify();

        _sectionStateSnapshot = new bool[sections.Length];
        for (int i = 0; i < sections.Length; i++)
            _sectionStateSnapshot[i] = sections[i].IsOpen;
        foreach (var s in sections) s.Open();

        locataireScrollContent?.SetDirty();
    }
    public void OnRevisionSaved()
    {
        var loc = GetLocataire();
        loyerSummary.Refresh(loc);
        RefreshRevisionAlert(loc);
        batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        locataireScrollContent?.SetDirty();   // ← ajouter
    }



    // Place le bouton Pappers en overlay à droite, DANS la boîte du champ Siret (une fois).
    private bool _siretRowBuilt;
    private void BuildSiretRow()
    {
        if (_siretRowBuilt || siret == null || pappersBtn == null) return;
        _siretRowBuilt = true;

        pappersBtn.transform.SetParent(siret.transform, false);
        var le = pappersBtn.GetComponent<LayoutElement>() ?? pappersBtn.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;   // hors flux : positionné manuellement dans la boîte
        var prt = (RectTransform)pappersBtn.transform;
        prt.anchorMin = new Vector2(1, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(1, 0);
        prt.sizeDelta = new Vector2(92, 26);
        prt.anchoredPosition = new Vector2(-8, 9);
    }

    // ── Pappers ───────────────────────────────────────────────────────────────

    private void OnPappersClick()
    {
        var s = siret.GetValue().Trim();
        if (s.Length < 9)
        {
            UndoToast.Instance?.ShowInfo("Siret incomplet — 9 chiffres minimum pour ouvrir Pappers.");
            return;
        }
        Application.OpenURL($"https://www.pappers.fr/entreprise/{s.Substring(0, 9)}");
    }

    private void CreateResume()
    {
        var s = siret.GetValue().Trim();
        if (s.Length < 9)
        {
            UndoToast.Instance?.ShowInfo("Siret incomplet — 9 chiffres minimum pour interroger la société.");
            return;
        }
        papperService.FetchBySiret(s,
            data => SetPappersSection(PappersResume.Construire(data)),
            err => { SetPappersSection($"Erreur : {err}"); Debug.LogWarning($"[Annuaire] {err}"); });
    }

    // Remplace le bloc Pappers du commentaire, en gardant ce qui l'entoure.
    // La règle vit dans PappersResume : la mise à jour au lancement s'en sert
    // aussi, sans écran — deux copies auraient divergé.
    private void SetPappersSection(string pappersContent)
    {
        Commentaire.ApplyValue(PappersResume.Fusionner(Commentaire.GetValue(), pappersContent));
        locataireScrollContent?.SetDirty();
    }

    // ── Objectifs ─────────────────────────────────────────────────────────────

    private void updateListObjectif()
    {
        var locataire = batimentPrefabOrigin.listLocataire.Find(b => b.id == id);
        var index = batimentPrefabOrigin.listLocataire.IndexOf(locataire);
        batimentPrefabOrigin.listLocataire[index].objectifs = objectivesManager.GetAllTheObjectif();
        batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        locataireScrollContent?.SetDirty();
    }

    // ── Divers ────────────────────────────────────────────────────────────────

 

    public override string getName()
    {
        return batimentPrefabOrigin.listLocataire.Find(b => b.id == id)?.Name ?? "";
    }

    public override string getID()
    {
        return batimentPrefabOrigin.listLocataire.Find(b => b.id == id)?.id ?? "";
    }

    void Start() { }
    void Update() { }
}