using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// Essai technique (étape 1 de CONCEPTION_MULTIUTILISATEUR.md) : connexion, verrou d'une
/// fiche, temps réel, avec un projet Supabase. Deux sessions côte à côte (A = Marie,
/// B = Paul) pour tester le partage dans une seule fenêtre. Ne touche à aucune donnée
/// de bâtiment : seulement les tables essai_* créées par Serveur/essai_supabase.sql.
public class EssaiCloudWindow : EditorWindow
{
    const string Cible = "volteo";
    static readonly string[] Tables = { "essai_fiches", "essai_verrous" };

    class Poste
    {
        public string Titre, Nom, Email = "", MotDePasse = "";
        public readonly string SessionId = Guid.NewGuid().ToString("N");
        public CloudClient.Session Session;
        public CloudClient.Verrou Verrou;
        public bool Detient;
        public TempsReel Canal;
    }

    [Serializable] class ParamsPrendre { public string p_cible, p_session, p_nom, p_motif; }
    [Serializable] class ParamsLiberer { public string p_cible, p_session; }
    [Serializable] class MajFiche { public double loyer; public string maj_par; public int version; public string maj_le; }

    string _url, _cle;
    readonly Poste _a = new Poste { Titre = "Session A", Nom = "Marie Dupont" };
    readonly Poste _b = new Poste { Titre = "Session B", Nom = "Paul Lefèvre" };
    readonly List<string> _journal = new List<string>();
    Vector2 _defilPage;
    DateTime _envoiModif = DateTime.MinValue;
    string _auteurModif;
    double _prochainRepaint;

    [UnityEditor.MenuItem("CIPL/Essai cloud (étape 1)")]   // nom complet : le projet a sa propre classe MenuItem
    static void Ouvrir() => GetWindow<EssaiCloudWindow>("Essai cloud");

    void OnEnable()
    {
        _url = ReglageService.GetCloudUrl();
        _cle = ReglageService.GetCloudCle();
        EditorApplication.update += Pomper;
    }

    void OnDisable()
    {
        EditorApplication.update -= Pomper;
        _a.Canal?.Dispose();
        _b.Canal?.Dispose();
    }

    // ── Affichage ──────────────────────────────────────────────────────────────

    void OnGUI()
    {
        EditorGUILayout.LabelField("Projet Supabase", EditorStyles.boldLabel);
        _url = EditorGUILayout.TextField("Adresse du projet", _url);
        _cle = EditorGUILayout.TextField("Clé publique", _cle);
        if (GUILayout.Button("Enregistrer l'adresse et la clé", GUILayout.Width(240)))
        {
            string api = CloudClient.AdresseProjet(_url);
            if (api != (_url ?? "").Trim().TrimEnd('/'))
                Journal($"Adresse du tableau de bord reconnue : adresse de l'API utilisée à la place, {api}");
            bool ok = ReglageService.SetCloudUrl(api) & ReglageService.SetCloudCle(_cle);
            _url = ReglageService.GetCloudUrl();
            GUI.FocusControl(null);   // sinon le champ en saisie garde l'ancien texte affiché
            Journal(ok ? "Adresse et clé enregistrées (cipl_app_secrets.dat)."
                       : "ÉCHEC de l'enregistrement de l'adresse et de la clé.");
        }

        EditorGUILayout.Space();
        _defilPage = EditorGUILayout.BeginScrollView(_defilPage);
        if (position.width >= 700)   // assez large : Marie et Paul côte à côte
        {
            using (new EditorGUILayout.HorizontalScope()) { Colonne(_a); Colonne(_b); }
        }
        else                         // fenêtre étroite : l'une sous l'autre
        {
            Colonne(_a);
            Colonne(_b);
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Journal (le plus récent en haut)", EditorStyles.boldLabel);
            if (GUILayout.Button("Vider", GUILayout.Width(80))) _journal.Clear();
        }
        for (int i = _journal.Count - 1; i >= 0; i--)
            EditorGUILayout.SelectableLabel(_journal[i], EditorStyles.wordWrappedLabel, GUILayout.Height(18));
        EditorGUILayout.EndScrollView();
    }

    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    static string Euros(double? v) => v.HasValue ? v.Value.ToString("N0", Fr) + " €" : "?";

    void Colonne(Poste p)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(320)))
        {
            EditorGUILayout.LabelField(p.Titre, EditorStyles.boldLabel);
            p.Nom = EditorGUILayout.TextField("Nom affiché", p.Nom);
            p.Email = EditorGUILayout.TextField("E-mail", p.Email);
            p.MotDePasse = EditorGUILayout.PasswordField("Mot de passe", p.MotDePasse);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Se connecter")) Lancer(() => Connecter(p));
                GUI.enabled = p.Session != null;
                if (GUILayout.Button("Renouveler le jeton")) Lancer(() => Renouveler(p));
                GUI.enabled = true;
            }
            EditorGUILayout.LabelField(p.Session == null
                ? "Non connectée"
                : $"Connectée : {p.Session.user.email} — jeton jusqu'à {p.Session.ExpireLe:HH:mm:ss}");

            GUI.enabled = p.Session != null;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Verrou de la fiche « Volteo »", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Prendre")) Lancer(() => Prendre(p));
                if (GUILayout.Button("Signal de vie")) Lancer(() => Prendre(p));
                if (GUILayout.Button("Libérer")) Lancer(() => Liberer(p));
            }
            EditorGUILayout.LabelField(EtatVerrou(p), EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fiche « Volteo » : temps réel et version", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (p.Canal == null) { if (GUILayout.Button("Écouter")) Lancer(() => Ecouter(p)); }
                else if (GUILayout.Button("Arrêter l'écoute")) { p.Canal.Dispose(); p.Canal = null; Journal($"{p.Titre} : écoute arrêtée."); }
                if (GUILayout.Button("Loyer + 100 €")) Lancer(() => ModifierLoyer(p, false));
                if (GUILayout.Button("Version périmée")) Lancer(() => ModifierLoyer(p, true));
            }
            EditorGUILayout.LabelField("Temps réel : " + (p.Canal?.Etat ?? "arrêté"));

            GUI.enabled = true;
        }
    }

    string EtatVerrou(Poste p)
    {
        if (p.Verrou == null) return "—";
        var expire = CloudClient.Date(p.Verrou.expire_le) ?? DateTime.Now;
        if (p.Detient)
            return $"Détenu par cette session — se libère seul dans {RegleVerrou.Restant(expire, DateTime.Now)} sans signal de vie";
        return "Refusé : " + RegleVerrou.Libelle(p.Verrou.nom, p.Verrou.motif, CloudClient.Date(p.Verrou.pris_le) ?? DateTime.Now)
             + $" (libre dans {RegleVerrou.Restant(expire, DateTime.Now)} s'il n'y a plus de signal)";
    }

    // ── Actions ────────────────────────────────────────────────────────────────

    async void Lancer(Func<Task> action)
    {
        if (string.IsNullOrEmpty(_url) || string.IsNullOrEmpty(_cle))
        {
            Journal("Renseignez d'abord l'adresse du projet et la clé publique, puis « Enregistrer ».");
            return;
        }
        try { await action(); }
        catch (Exception e) { Journal("Erreur : " + e.Message); }
        Repaint();
    }

    async Task Connecter(Poste p)
    {
        var r = await CloudClient.SeConnecter(_url, _cle, p.Email.Trim(), p.MotDePasse);
        p.MotDePasse = "";   // jamais gardé : à ressaisir en cas d'échec
        if (!r.Ok) { Journal($"{p.Titre} : connexion refusée — {r.Erreur}"); return; }
        p.Session = CloudClient.LireSession(r.Corps);
        Journal(p.Session != null
            ? $"{p.Titre} : connectée ({p.Session.user.email}), jeton valable jusqu'à {p.Session.ExpireLe:HH:mm:ss}."
            : $"{p.Titre} : réponse de connexion illisible.");
    }

    async Task Renouveler(Poste p)
    {
        var r = await CloudClient.Renouveler(_url, _cle, p.Session.refresh_token);
        if (!r.Ok) { Journal($"{p.Titre} : renouvellement refusé — {r.Erreur}"); return; }
        var s = CloudClient.LireSession(r.Corps);
        if (s == null) { Journal($"{p.Titre} : réponse de renouvellement illisible."); return; }
        p.Session = s;
        Journal($"{p.Titre} : jeton renouvelé, valable jusqu'à {s.ExpireLe:HH:mm:ss}.");
    }

    async Task Prendre(Poste p)
    {
        var t0 = DateTime.Now;
        var r = await CloudClient.Fonction(_url, _cle, p.Session.access_token, "essai_prendre_verrou",
            new ParamsPrendre { p_cible = Cible, p_session = p.SessionId, p_nom = p.Nom, p_motif = "facturation" });
        int ms = (int)(DateTime.Now - t0).TotalMilliseconds;
        if (!r.Ok) { Journal($"{p.Titre} : verrou — {r.Erreur}"); return; }

        p.Verrou = CloudClient.LireVerrou(r.Corps);
        p.Detient = p.Verrou != null && p.Verrou.obtenu;
        if (p.Verrou == null) { Journal($"{p.Titre} : réponse du verrou illisible."); return; }
        Journal(p.Detient
            ? $"{p.Titre} : verrou OBTENU sur Volteo, valable jusqu'à {CloudClient.Date(p.Verrou.expire_le):HH:mm:ss} ({ms} ms)."
            : $"{p.Titre} : verrou REFUSÉ — {RegleVerrou.Libelle(p.Verrou.nom, p.Verrou.motif, CloudClient.Date(p.Verrou.pris_le) ?? DateTime.Now)} ({ms} ms).");
    }

    async Task Liberer(Poste p)
    {
        var r = await CloudClient.Fonction(_url, _cle, p.Session.access_token, "essai_liberer_verrou",
            new ParamsLiberer { p_cible = Cible, p_session = p.SessionId });
        if (!r.Ok) { Journal($"{p.Titre} : libération — {r.Erreur}"); return; }
        p.Detient = false;
        p.Verrou = null;
        Journal($"{p.Titre} : verrou libéré (ou n'était pas détenu par cette session).");
    }

    /// Écrit loyer + 100 € en exigeant la version lue (contrôle de version). Avec
    /// `perimee`, on présente volontairement l'ancienne version : le serveur doit refuser.
    async Task ModifierLoyer(Poste p, bool perimee)
    {
        var lu = await CloudClient.Table("GET", _url, _cle, p.Session.access_token, "essai_fiches", $"id=eq.{Cible}&select=*");
        if (!lu.Ok) { Journal($"{p.Titre} : lecture de la fiche — {lu.Erreur}"); return; }
        var fiches = CloudClient.LireFiches(lu.Corps);
        if (fiches == null || fiches.Length == 0) { Journal("Fiche « volteo » introuvable : le script SQL a-t-il été passé ?"); return; }
        var f = fiches[0];

        int versionPresentee = perimee ? f.version - 1 : f.version;
        var maj = new MajFiche
        {
            loyer = f.loyer + 100, maj_par = p.Nom, version = f.version + 1,
            maj_le = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        };
        _envoiModif = DateTime.Now;
        _auteurModif = p.Nom;
        var r = await CloudClient.Table("PATCH", _url, _cle, p.Session.access_token, "essai_fiches",
                                        $"id=eq.{Cible}&version=eq.{versionPresentee}", JsonUtility.ToJson(maj));
        if (!r.Ok) { Journal($"{p.Titre} : écriture — {r.Erreur}"); return; }

        var apres = CloudClient.LireFiches(r.Corps);
        if (apres == null || apres.Length == 0)
            Journal($"{p.Titre} : écriture REFUSÉE — la fiche est en version {f.version}, la version {versionPresentee} présentée est périmée. Rien n'a été écrit.");
        else
            Journal($"{p.Titre} : loyer {Euros(f.loyer)} → {Euros(apres[0].loyer)} (version {apres[0].version}).");
    }

    async Task Ecouter(Poste p)
    {
        p.Canal = new TempsReel();
        await p.Canal.Demarrer(_url, _cle, p.Session.access_token, Tables);
        Journal($"{p.Titre} : connexion au temps réel ouverte, abonnement demandé.");
    }

    // ── Messages temps réel (lus sur le fil principal) ─────────────────────────

    void Pomper()
    {
        foreach (var p in new[] { _a, _b })
            if (p.Canal != null)
                while (p.Canal.Recus.TryDequeue(out var json)) Traiter(p, json);

        // Compte à rebours du verrou et état du canal : un rafraîchissement par seconde suffit.
        if (EditorApplication.timeSinceStartup >= _prochainRepaint)
        {
            _prochainRepaint = EditorApplication.timeSinceStartup + 1;
            Repaint();
        }
    }

    void Traiter(Poste p, string json)
    {
        var m = TempsReel.Lire(json);
        if (m == null) return;

        switch (m.@event)
        {
            case "phx_reply":
                if (m.payload?.status == "error")
                    Journal($"{p.Titre} : abonnement REFUSÉ par le serveur — {m.payload.response?.reason}");
                else if (m.@ref == p.Canal.RefAbonnement)
                    Journal($"{p.Titre} : abonnée aux changements de essai_fiches et essai_verrous.");
                break;

            case "system":
                if (!string.IsNullOrEmpty(m.payload?.message)) Journal($"{p.Titre} : serveur — {m.payload.message}");
                break;

            case "postgres_changes":
                var d = m.payload?.data;
                if (d == null) break;
                if (d.table == "essai_fiches")
                {
                    string delai = d.record != null && d.record.maj_par == _auteurModif && _envoiModif != DateTime.MinValue
                        ? $" — reçu {(int)(DateTime.Now - _envoiModif).TotalMilliseconds} ms après l'envoi"
                        : "";
                    Journal($"{p.Titre} REÇOIT : Volteo mis à jour par {d.record?.maj_par} — loyer {Euros(d.record?.loyer)} (version {d.record?.version}){delai}");
                }
                else
                {
                    Journal(d.type == "DELETE"
                        ? $"{p.Titre} REÇOIT : verrou de {d.old_record?.cible} libéré"
                        : $"{p.Titre} REÇOIT : verrou de {d.record?.cible} — {d.record?.nom} ({d.record?.motif})");
                }
                break;
        }
    }

    void Journal(string message)
    {
        _journal.Add($"{DateTime.Now:HH:mm:ss.fff}  {message}");
        if (_journal.Count > 300) _journal.RemoveAt(0);
        Debug.Log("[EssaiCloud] " + message);
        Repaint();
    }
}
