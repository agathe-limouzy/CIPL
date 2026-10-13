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
        if (!SessionCloud.Configure)
        {
            ConfirmDialog.Erreur("Adresse du projet cloud non renseignée : dans Unity, menu CIPL › Essai cloud (étape 1).");
            return;
        }
        try
        {
            if (!SessionCloud.Active && !await SessionCloud.Restaurer())
                await EcransCloud.Connexion(Canvas);
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
        EcritureCloud.LibererTout();
        try { await OuvrirUneEntreprise(forcerChoix: true); }
        catch (Exception e) { Debug.LogException(e); ConfirmDialog.Erreur("Changement d'entreprise impossible : " + e.Message); }
    }

    public static void SeDeconnecter()
    {
        EcritureCloud.LibererTout();
        SessionCloud.Oublier();
        _bm.Vider();
        EcransCloud.Pastille(Canvas, null);
        Lancer(_bm);
    }

    static async Task OuvrirUneEntreprise(bool forcerChoix)
    {
        while (true)
        {
            var (liste, erreur) = await DonneesCloud.MesEntreprises();
            if (liste == null) { ConfirmDialog.Erreur("Impossible de lire vos entreprises : " + erreur); return; }

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

        // Les documents (photos, PDF) restent sur ce PC jusqu'à l'étape 6 : on garde la racine
        // locale de l'entreprise importée, s'il y en a une, pour que les fiches les retrouvent.
        string dossier = PlayerPrefs.GetString(ClePrefsDossier(e.id), "");
        if (!string.IsNullOrEmpty(dossier) && Directory.Exists(dossier)) SaveLocationService.UseRoot(dossier);
        ReglageService.Load();

        await EcritureCloud.VerifierNom(Canvas);   // « Votre nom », à la première ouverture

        var (bats, erreur) = await EcritureCloud.Charger(e.id);   // avec les versions (étape 4a)
        if (bats == null) { ConfirmDialog.Erreur($"Lecture de « {e.nom} » impossible : {erreur}"); return; }

        _bm.Vider();
        _bm.Installer(bats);
        _bm.menuManager.OpenGeneralMenu();
        EcritureCloud.DemarrerGardien();
        EcransCloud.Pastille(Canvas, $"{e.nom} · cloud");
        UndoToast.Instance?.ShowInfo($"{e.nom} : {bats.Count} bâtiment(s) lu(s) depuis le cloud.");
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
