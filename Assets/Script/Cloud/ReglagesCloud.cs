using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// Étape 9a : Réglages et secrets de l'entreprise, partagés par le serveur (décisions du 19/10).
/// Le serveur fait référence ; chaque PC en garde une copie (ReglageService). Enregistrement
/// contrôlé par la version. Un changement d'un collègue est relu tout de suite, sauf pendant que
/// la fenêtre Réglages est ouverte ici : il l'est à sa fermeture.
public static class ReglagesCloud
{
    [Serializable] class LigneReglages { public int version; public string maj_par; public ReglageData data; }
    [Serializable] class LignesReglages { public LigneReglages[] items; }
    [Serializable] class LigneSecret { public string cle, valeur; }
    [Serializable] class LignesSecrets { public LigneSecret[] items; }
    [Serializable] class ParamsSecret { public string p_entreprise, p_cle, p_valeur; }

    static int _version;   // version connue sur le serveur ; 0 : pas encore partagés
    static bool _aRelire;
    static string _auteur;  // qui a changé les Réglages, annoncé à la relecture
    static float _prochainEssai;
    static Task _file = Task.CompletedTask;   // un envoi à la fois, dans l'ordre

    static string Url => SessionCloud.Url;
    static string CleApi => SessionCloud.Cle;

    // ── Sans réseau (testé en EditMode) ────────────────────────────────────────

    /// Paramètres de enregistrer_reglages : les Réglages partent en objet JSON.
    public static string Corps(string entreprise, int version, string jsonReglages, string session) =>
        "{\"p_entreprise\":" + Q(entreprise) + ",\"p_version\":" + version + ",\"p_data\":" + jsonReglages +
        ",\"p_session\":" + Q(session) + "}";

    /// Pas encore partagés (version 0) : seul un admin met les siens en commun (décision du 19/10).
    public static bool PeutEnvoyer(int version, bool admin) => version > 0 || admin;

    static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    static string Qui(string nom) => string.IsNullOrEmpty(nom) ? "un collègue" : RegleVerrou.Prenom(nom);

    // ── Ouverture d'une entreprise ─────────────────────────────────────────────

    /// Les Réglages et secrets du serveur remplacent la copie de ce PC. Pas encore partagés : le
    /// premier admin qui ouvre l'entreprise met les siens en commun. Lecture impossible : on garde
    /// la copie de ce PC.
    public static async Task Synchroniser(EntrepriseCloud e)
    {
        _version = 0; _aRelire = false; _auteur = null; _file = Task.CompletedTask;
        var lu = await Lire(e);
        if (lu == false && e.EstAdmin) await MettreEnCommun(e);
    }

    /// Null si la lecture a échoué ; faux si les Réglages ne sont pas encore partagés.
    static async Task<bool?> Lire(EntrepriseCloud e)
    {
        string jeton = await SessionCloud.Jeton();
        var r = await CloudClient.Table("GET", Url, CleApi, jeton, "reglages", $"entreprise_id=eq.{e.id}&select=version,maj_par,data");
        if (!r.Ok) { Debug.LogWarning("[Réglages] Lecture : " + r.Erreur); return null; }
        var l = JsonUtility.FromJson<LignesReglages>("{\"items\":" + r.Corps + "}")?.items?.FirstOrDefault();
        if (l?.data == null) return false;
        _version = l.version;
        ReglageService.Remplacer(l.data);

        var s = await CloudClient.Table("GET", Url, CleApi, jeton, "secrets", $"entreprise_id=eq.{e.id}&select=cle,valeur");
        var secrets = s.Ok ? JsonUtility.FromJson<LignesSecrets>("{\"items\":" + s.Corps + "}")?.items : null;
        if (secrets != null && secrets.Length > 0)
            ReglageService.RemplacerSecrets(secrets.Where(x => !string.IsNullOrEmpty(x?.cle))
                                                   .GroupBy(x => x.cle).ToDictionary(g => g.Key, g => g.First().valeur ?? ""));
        Debug.Log($"[Réglages] Lus sur le serveur (version {_version})");
        return true;
    }

    static async Task MettreEnCommun(EntrepriseCloud e)
    {
        var res = await EnvoyerMaintenant(e, 0);
        if (res?.ok != true) { Debug.LogWarning("[Réglages] Mise en commun : " + (res?.motif ?? res?.refus)); return; }
        foreach (var cle in ReglageService.ClesSecrets)
        {
            string v = ReglageService.LireSecret(cle);
            if (!string.IsNullOrEmpty(v)) await DefinirSecretMaintenant(e, cle, v);
        }
        Debug.Log("[Réglages] Mis en commun par cet admin");
        UndoToast.Instance?.ShowInfo("Vos Réglages sont maintenant ceux de toute l'entreprise.");
    }

    // ── Enregistrement ─────────────────────────────────────────────────────────

    /// Après chaque ReglageService.Save sur le cloud.
    public static void Envoyer() { var avant = _file; _file = EnvoyerApres(avant); }

    static async Task EnvoyerApres(Task avant)
    {
        try { await avant; } catch (Exception) { /* déjà signalé */ }
        var e = SessionCloud.Entreprise;
        if (e == null || !PeutEnvoyer(_version, e.EstAdmin)) return;   // pas encore partagés : seule la copie de ce PC change
        try
        {
            var res = await EnvoyerMaintenant(e, _version);
            if (res?.ok == true) return;
            if (res?.refus == "version")
                ConfirmDialog.Erreur($"Réglages modifiés entre-temps par {Qui(res.nom)} : vos modifications ne sont pas enregistrées. " +
                                     "Les Réglages sont rechargés.", () => RelireMaintenant(null));
            else if (res?.refus == "reseau")
            {
                ConfirmDialog.Erreur(HorsLigne.Refus + " Les Réglages ne sont pas enregistrés.");
                ARelire(null);   // la copie de ce PC reprendra celle du serveur au retour
            }
            else ConfirmDialog.Erreur("Réglages non enregistrés sur le serveur : " + (res?.motif ?? res?.refus));
        }
        catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Réglages non enregistrés sur le serveur : " + ex.Message); }
    }

    static async Task<EcritureCloud.Resultat> EnvoyerMaintenant(EntrepriseCloud e, int version)
    {
        var r = await CloudClient.FonctionJson(Url, CleApi, await SessionCloud.Jeton(), "enregistrer_reglages",
            Corps(e.id, version, JsonUtility.ToJson(ReglageService.Current), EcritureCloud.SessionId));
        if (!r.Ok) return new EcritureCloud.Resultat { refus = HorsLigne.Panne(r.Ok, r.Code) ? "reseau" : "erreur", motif = r.Erreur };
        var res = JsonUtility.FromJson<EcritureCloud.Resultat>(r.Corps);
        if (res?.ok == true) _version = res.version;
        return res;
    }

    /// Un secret changé par un admin (Réglages › Connexion & envoi).
    public static async void DefinirSecret(string cle, string valeur)
    {
        var e = SessionCloud.Entreprise;
        if (e == null) return;
        try
        {
            var res = await DefinirSecretMaintenant(e, cle, valeur);
            if (res?.ok != true) ConfirmDialog.Erreur("Secret non enregistré sur le serveur : " + (res?.motif ?? res?.refus));
        }
        catch (Exception ex) { Debug.LogException(ex); ConfirmDialog.Erreur("Secret non enregistré sur le serveur : " + ex.Message); }
    }

    static async Task<EcritureCloud.Resultat> DefinirSecretMaintenant(EntrepriseCloud e, string cle, string valeur)
    {
        var r = await CloudClient.Fonction(Url, CleApi, await SessionCloud.Jeton(), "definir_secret",
                                           new ParamsSecret { p_entreprise = e.id, p_cle = cle, p_valeur = valeur ?? "" });
        return r.Ok ? JsonUtility.FromJson<EcritureCloud.Resultat>(r.Corps) : new EcritureCloud.Resultat { refus = "erreur", motif = r.Erreur };
    }

    // ── Relecture (changement d'un collègue, retour de la connexion) ───────────

    /// Les Réglages ou un secret ont changé ailleurs (direct), ou ce PC doit reprendre ceux du
    /// serveur (retour de la connexion). `auteur` : annoncé à la relecture (null : en silence).
    public static void ARelire(string auteur)
    {
        _aRelire = true;
        if (auteur != null) _auteur = auteur;
    }

    /// Appelé par la pompe du direct : relit dès que la fenêtre Réglages est fermée (décision du 19/10).
    public static void Tic()
    {
        if (!_aRelire || HorsLigne.Actif || SessionCloud.Entreprise == null || Time.realtimeSinceStartup < _prochainEssai) return;
        if (ReglagePanel.Instance != null && ReglagePanel.Instance.gameObject.activeInHierarchy) return;
        _aRelire = false;
        string auteur = _auteur; _auteur = null;
        RelireMaintenant(auteur);
    }

    static async void RelireMaintenant(string auteur)
    {
        var e = SessionCloud.Entreprise;
        if (e == null) return;
        try
        {
            var lu = await Lire(e);
            if (lu == null) { _aRelire = true; _prochainEssai = Time.realtimeSinceStartup + 10f; return; }   // on réessaiera
            if (ReglagePanel.Instance != null && ReglagePanel.Instance.gameObject.activeInHierarchy) ReglagePanel.Instance.Rafraichir();
            if (lu == true && auteur != null) UndoToast.Instance?.ShowInfo($"Réglages mis à jour par {Qui(auteur)}.");
        }
        catch (Exception ex) { Debug.LogWarning("[Réglages] Relecture : " + ex.Message); }
    }
}
