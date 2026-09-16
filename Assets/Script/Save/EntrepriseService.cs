using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Une entreprise gérée par l'app = un nom + sa racine de sauvegarde propre
/// (dossier « …/CIPL_Saves » contenant reglage.json + batiments/…).
[System.Serializable]
public class Entreprise
{
    public string nom;
    public string racine;   // racine FINALE (…/CIPL_Saves)
}

[System.Serializable]
class EntrepriseRegistry
{
    public List<Entreprise> items = new List<Entreprise>();
}

/// Registre GLOBAL des entreprises (PlayerPrefs, indépendant de la sauvegarde
/// active) + opérations de création / ouverture / bascule. Chaque entreprise a sa
/// propre racine ; basculer = changer la racine active puis recharger à chaud.
public static class EntrepriseService
{
    const string KEY = "cipl_entreprises";
    static EntrepriseRegistry _reg;

    static EntrepriseRegistry Reg
    {
        get
        {
            if (_reg == null)
            {
                string j = PlayerPrefs.GetString(KEY, "");
                _reg = string.IsNullOrEmpty(j) ? new EntrepriseRegistry()
                    : (JsonUtility.FromJson<EntrepriseRegistry>(j) ?? new EntrepriseRegistry());
            }
            return _reg;
        }
    }

    static void Save() { PlayerPrefs.SetString(KEY, JsonUtility.ToJson(Reg)); PlayerPrefs.Save(); }

    public static List<Entreprise> All() => Reg.items;
    public static string ActiveRoot => SaveLocationService.GetSaveRoot();
    public static bool EstActive(Entreprise e) => e != null && e.racine == ActiveRoot;

    /// Ajoute (ou met à jour le nom d')une entreprise dans le registre.
    public static void Register(string nom, string racine)
    {
        if (string.IsNullOrEmpty(racine)) return;
        var e = Reg.items.Find(x => x.racine == racine);
        if (e != null) { if (!string.IsNullOrWhiteSpace(nom)) e.nom = nom; }
        else Reg.items.Add(new Entreprise { nom = string.IsNullOrWhiteSpace(nom) ? "Entreprise" : nom, racine = racine });
        Save();
    }

    /// Retire une entreprise du registre (ne supprime AUCUNE donnée sur le disque).
    public static void Oublier(string racine) { Reg.items.RemoveAll(e => e.racine == racine); Save(); }

    /// Enregistre l'entreprise active si elle n'est pas encore dans le registre
    /// (1er lancement : la sauvegarde courante devient la 1re entreprise).
    public static void EnsureActiveRegistered()
    {
        string r = ActiveRoot;
        if (Reg.items.Exists(e => e.racine == r)) return;
        string nom = !string.IsNullOrWhiteSpace(ReglageService.Current?.entrepriseNom)
            ? ReglageService.Current.entrepriseNom : "Mon entreprise";
        Register(nom, r);
    }

    // ── Bascule / création / ouverture (recharge à chaud + retour au home) ──────

    /// Bascule sur une entreprise déjà enregistrée (racine finale connue).
    public static void Activer(string racine)
    {
        SaveLocationService.UseRoot(racine);
        RechargerEtRetourHome();
    }

    /// Crée une entreprise : dossier parent choisi → « …/CIPL_Saves », nom mémorisé.
    /// Renvoie false si le dossier abrite déjà une entreprise (rien n'est modifié).
    public static bool Creer(string nom, string dossierParent)
    {
        // Un CIPL_Saves déjà peuplé à cet endroit signifie qu'une entreprise y vit.
        // Sans ce contrôle, Load() chargeait SES réglages, le nom était écrasé par le
        // nouveau, et la « nouvelle » entreprise adoptait les bâtiments et les RIB de
        // l'ancienne — les deux entrées pointant alors sur les mêmes données.
        string cible = Path.Combine(dossierParent, "CIPL_Saves");
        string batimentsCible = Path.Combine(cible, "batiments");
        bool dejaOccupe = File.Exists(Path.Combine(cible, "reglage.json"))
                          || (Directory.Exists(batimentsCible)
                              && Directory.GetFiles(batimentsCible, "*.json").Length > 0);
        if (dejaOccupe)
        {
            Debug.LogError($"[EntrepriseService] {cible} contient déjà une entreprise — création annulée.");
            UndoToast.Instance?.ShowInfo(
                "Ce dossier contient déjà une entreprise. Utilisez « Ouvrir » pour la charger, " +
                "ou choisissez un dossier vide pour en créer une nouvelle.");
            return false;
        }

        string racine = SaveLocationService.SetSaveRoot(dossierParent);   // parent/CIPL_Saves + active
        ReglageService.Load();
        ReglageService.Current.entrepriseNom = string.IsNullOrWhiteSpace(nom) ? "Entreprise" : nom;
        ReglageService.Save();
        Register(ReglageService.Current.entrepriseNom, racine);
        RechargerEtRetourHome();
        return true;
    }

    /// Ouvre un dossier existant comme entreprise (reprend son nom si présent).
    public static void Ouvrir(string dossierParent)
    {
        string racine = SaveLocationService.SetSaveRoot(dossierParent);
        ReglageService.Load();
        string nom = !string.IsNullOrWhiteSpace(ReglageService.Current?.entrepriseNom)
            ? ReglageService.Current.entrepriseNom : Path.GetFileName(dossierParent.TrimEnd('/', '\\'));
        Register(nom, racine);
        RechargerEtRetourHome();
    }

    static void RechargerEtRetourHome()
    {
        ReglageService.Load();
        if (BatimentManager.Instance != null)
        {
            BatimentManager.Instance.ReloadFromDisk();
            BatimentManager.Instance.menuManager?.OpenGeneralMenu();
        }
    }
}
