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
    public InputAndText mapController;
    public InputAndText lotBatimentTxt;
    public InputAndText tailleLotTxt;
    public TMP_Dropdown typedeBailDropDown;
    public InputAndText cadastralTxt;

    public DateInputController dateDebutBail;
    public DateInputController dateFinBail;
    public InputAndText depotDeGarantieTxt;
    public InputAndText tauxDeRentabilité;

    [Header("Loyer")]
    public LoyerSummaryUI loyerSummary;

    public ObjectivesManager objectivesManager;
    public string id;
    public BatimentPrefab batimentPrefabOrigin;

    public Button pappersBtn;
    public Button resumeSocieteBtn;
    public PapperService papperService;
    public InputAndText Commentaire;

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
        bool due = LoyerSummaryUI.EstRevisionDue(loc);
        batimentPrefabOrigin.menulocataire.SetTabAlert(this, due);
    }

    public void OnEnable()
    {
        locataireScrollContent?.SetDirty();
    }

    public void RefreshTailleLot(float value)
    {
        tailleLotTxt.ApplySave(value.ToString());
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
            mapController.ApplySave(newLocataire.adresseLocataire);
            lotBatimentTxt.ApplySave(newLocataire.lotBatiment.ToString());
            tailleLotTxt.ApplySave(newLocataire.tailleLot.ToString());
            typedeBailDropDown.value = (int)newLocataire.typeDeBail;
            typedeBailDropDown.interactable = false;
            cadastralTxt.ApplySave(newLocataire.cadastral);

            loyerSummary.Refresh(newLocataire);
            RefreshRevisionAlert(newLocataire);

            dateDebutBail.ApplyDate(newLocataire.DateDebutBail);
            dateFinBail.ApplyDate(newLocataire.DateFinBail);
            depotDeGarantieTxt.ApplySave(newLocataire.depotDeGarantie.ToString());
            tauxDeRentabilité.ApplySave(newLocataire.tauxDeRentabilité.ToString());
            siret.ApplySave(newLocataire.siretNumber);
            Commentaire.ApplySave(newLocataire.commentaire);

     
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
        locataireScrollContent?.SetDirty();
    }

    public void SaveLocataire()
    {
        var locataire = batimentPrefabOrigin.listLocataire.Find(b => b.id == id);
        var index = batimentPrefabOrigin.listLocataire.IndexOf(locataire);

        locataire.Name = nameOfLocataire.GetNewSave();
        locataire.codeCompatable = codeComptableTxt.GetNewSave();
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
        locataire.cadastral = cadastralTxt.GetNewSave();
        locataire.DateDebutBail = dateDebutBail.saveThedate();
        locataire.DateFinBail = dateFinBail.saveThedate();

        SaveCorrectlyFloat(ref locataire.tauxDeRentabilité, tauxDeRentabilité.GetNewSave());
        locataire.siretNumber = siret.GetNewSave();
        locataire.commentaire = Commentaire.GetNewSave();

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
        mapController.Modify();
        lotBatimentTxt.Modify();
        if (batimentPrefabOrigin.listLocataire.Count > 1)
            tailleLotTxt.Modify();
        typedeBailDropDown.interactable = true;
        cadastralTxt.Modify();
        dateDebutBail.ModifyDate();
        dateFinBail.ModifyDate();
        depotDeGarantieTxt.Modify();
        tauxDeRentabilité.Modify();
        save.gameObject.SetActive(true);
        modifyBatiment.gameObject.SetActive(false);
        Delete.gameObject.SetActive(true);
        siret.Modify();
        Commentaire.Modify();

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
        if (s.Length < 9) return;
        Application.OpenURL($"https://www.pappers.fr/entreprise/{s.Substring(0, 9)}");
    }

    private void CreateResume()
    {
        var s = siret.GetValue().Trim();
        if (s.Length < 9) return;
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