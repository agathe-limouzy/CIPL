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

    [Header("Sections repliables")]
    public CollapsibleSection[] sections;
    private bool[] _sectionStateSnapshot;

    private const string PappersStart = "-- PAPPERS --";
    private const string PappersEnd = "-- FIN PAPPERS --";

    // ─────────────────────────────────────────────────────────────────────────


    public Locataire GetLocataire()
    => batimentPrefabOrigin.listLocataire.Find(b => b.id == id);

    /// Appelé par le RevisionPanel après initialisation ou révision


    public void RefreshRevisionAlert(Locataire loc)
    {
        // Le point rouge d'onglet couvre révision de loyer ET renouvellement de bail.
        bool due = LoyerSummaryUI.EstRevisionDue(loc)
                   || Locataire.RenouvellementProche(loc, out _);
        batimentPrefabOrigin.menulocataire.SetTabAlert(this, due);
        batimentPrefabOrigin.RefreshBatimentTabAlert();
        RefreshBailAlert(loc);
    }

    /// Badge de la section Bail : visible quand le bail se termine dans moins de
    /// 6 mois (ambre) ou est déjà expiré (terracotta).
    public void RefreshBailAlert(Locataire loc)
    {
        if (badgeBail == null) return;
        bool proche = Locataire.RenouvellementProche(loc, out int jours);
        badgeBail.SetActive(proche);
        if (!proche) return;

        bool expire = jours < 0;
        // Pastille pleine couleur + texte blanc pour bien la faire ressortir :
        // rouge si le bail est expiré (urgent), ambre soutenu si renouvellement à prévoir.
        Color bgc;
        ColorUtility.TryParseHtmlString(expire ? "#A32D2D" : "#B26A0C", out bgc);
        if (badgeBailTxt != null)
        {
            badgeBailTxt.text = expire ? "Bail expiré" : "À renouveler";
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
        // Dropdowns
        typedeBailDropDown.ClearOptions();
        typedeBailDropDown.AddOptions(
            Enum.GetNames(typeof(BailType))
                .Select(n => new TMP_Dropdown.OptionData(n)).ToList());


        id = newLocataire.id;
        loyerSummary.Init(this);
        bailFile?.Init(this);
        RefreshHeader(newLocataire);

        if (facturationFields == null) facturationFields = gameObject.AddComponent<LocataireFacturationFields>();
        facturationFields.EnsureBuilt(this);




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
        resumeSocieteBtn.onClick.RemoveAllListeners();
        resumeSocieteBtn.onClick.AddListener(CreateResume);

        if (!NeedToModify)
        {
            // ── Chargement locataire existant ────────────────────────────────
            nameOfLocataire.ApplySave(newLocataire.Name);
            codeComptableTxt.ApplySave(newLocataire.codeCompatable);
            emailLocataireTxt.ApplySave(newLocataire.emailLocataire);
            telephoneLocataireTxt.ApplySave(newLocataire.telephoneLocataire);
            mapController.ApplySave(newLocataire.adresseLocataire);
            lotBatimentTxt.ApplySave(newLocataire.lotBatiment.ToString());
            tailleLotTxt.ApplySave(newLocataire.tailleLot.ToString());
            typedeBailDropDown.value = (int)newLocataire.typeDeBail;
            typedeBailDropDown.interactable = false;

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

        locataire.Name = nameOfLocataire.GetNewSave();
        locataire.codeCompatable = codeComptableTxt.GetNewSave();
        locataire.emailLocataire = emailLocataireTxt.GetNewSave();
        locataire.telephoneLocataire = telephoneLocataireTxt.GetNewSave();
        locataire.adresseLocataire = mapController.GetNewSave();

        SaveCorrectlyInt(ref locataire.lotBatiment, lotBatimentTxt.GetNewSave());

        if (batimentPrefabOrigin.listLocataire.Count > 1)
        {
            SaveCorrectlyFloat(ref locataire.tailleLot, tailleLotTxt.GetNewSave());
            batimentPrefabOrigin.RefreshTailleBatiment();
        }
        else
        {
            tailleLotTxt.ApplySave(batimentPrefabOrigin.GetTailleBatiment().ToString());
        }

        locataire.typeDeBail = (BailType)typedeBailDropDown.value;
        typedeBailDropDown.interactable = false;
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
        if (batimentPrefabOrigin.listLocataire.Count > 1)
            tailleLotTxt.Modify();
        typedeBailDropDown.interactable = true;
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
            data => SetPappersSection(BuildResume(data)),
            err => { SetPappersSection($"Erreur : {err}"); Debug.LogWarning($"[Annuaire] {err}"); });
    }

    private void SetPappersSection(string pappersContent)
    {
        string current = Commentaire.GetValue();
        int start = current.IndexOf(PappersStart, StringComparison.Ordinal);
        int end = current.IndexOf(PappersEnd, StringComparison.Ordinal);
        string before = string.Empty, after = string.Empty;

        if (start >= 0 && end > start)
        {
            before = current.Substring(0, start).TrimEnd('\n', '\r', ' ');
            int afterIndex = end + PappersEnd.Length;
            if (afterIndex < current.Length)
                after = current.Substring(afterIndex).TrimStart('\n', '\r', ' ');
        }
        else
        {
            before = current.TrimEnd('\n', '\r', ' ');
        }

        string bloc = $"{PappersStart}\n{pappersContent}\n{PappersEnd}";
        string newText;
        if (!string.IsNullOrEmpty(before) && !string.IsNullOrEmpty(after)) newText = $"{before}\n\n{bloc}\n\n{after}";
        else if (!string.IsNullOrEmpty(before)) newText = $"{before}\n\n{bloc}";
        else if (!string.IsNullOrEmpty(after)) newText = $"{bloc}\n\n{after}";
        else newText = bloc;

        Commentaire.ApplyValue(newText);
        locataireScrollContent?.SetDirty();
    }

    private string BuildResume(AnnuaireEntreprise data)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{data.nom_complet}</b>");
        if (!string.IsNullOrEmpty(data.libelle_nature_juridique)) sb.AppendLine(data.libelle_nature_juridique);
        if (!string.IsNullOrEmpty(data.siren)) sb.AppendLine($"SIREN : {FormatSiren(data.siren)}");
        if (!string.IsNullOrEmpty(data.date_creation)) sb.AppendLine($"Créée le : {FormatDate(data.date_creation)}");
        if (!string.IsNullOrEmpty(data.libelle_tranche_effectif)) sb.AppendLine($"Effectif : {data.libelle_tranche_effectif}");
        sb.AppendLine();
        sb.AppendLine("── Statut ──");
        sb.AppendLine(data.etat_administratif == "A" ? "Actif" : "Cessé");
        sb.AppendLine();
        sb.AppendLine("── Activité ──");
        if (!string.IsNullOrEmpty(data.activite_principale))
            sb.AppendLine($"NAF {data.activite_principale} · {data.libelle_activite_principale}");
        if (data.siege != null)
        {
            sb.AppendLine();
            sb.AppendLine("── Siège ──");
            sb.AppendLine(data.siege.adresse);
            sb.AppendLine($"{data.siege.code_postal} {data.siege.commune}");
        }
        sb.AppendLine();
        sb.AppendLine("── Finances ──");
        sb.AppendLine("Consulter sur Pappers pour CA et résultat");
        return sb.ToString().TrimEnd();
    }

    private static string FormatSiren(string s) =>
        s?.Length == 9 ? $"{s.Substring(0, 3)} {s.Substring(3, 3)} {s.Substring(6, 3)}" : s;

    private static string FormatDate(string iso) =>
        DateTime.TryParse(iso, out var dt) ? dt.ToString("dd/MM/yyyy") : iso;

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