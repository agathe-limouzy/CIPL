using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// Réglage du cloud en développement (menu CIPL › Essai cloud, cité par DemarrageCloud) : adresse et clé
/// publique du projet Supabase, puis un essai de connexion et de renouvellement du jeton, deux sessions
/// côte à côte (A = Marie, B = Paul). Ne touche à aucune donnée.
/// Revue 17 (D3-21) : la partie « fiches » de l'essai technique de l'étape 1 (tables essai_fiches et
/// essai_verrous, verrou, temps réel) a été retirée : ces objets ont été supprimés du serveur à l'étape 16c (D1-16).
public class EssaiCloudWindow : EditorWindow
{
    class Poste
    {
        public string Titre, Email = "", MotDePasse = "";
        public CloudClient.Session Session;
    }

    string _url, _cle;
    readonly Poste _a = new Poste { Titre = "Session A" };
    readonly Poste _b = new Poste { Titre = "Session B" };
    readonly List<string> _journal = new List<string>();
    Vector2 _defilPage;

    [UnityEditor.MenuItem("CIPL/Essai cloud (étape 1)")]   // nom complet : le projet a sa propre classe MenuItem
    static void Ouvrir() => GetWindow<EssaiCloudWindow>("Essai cloud");

    void OnEnable()
    {
        _url = ReglageService.GetCloudUrl();
        _cle = ReglageService.GetCloudCle();
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

    void Colonne(Poste p)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(320)))
        {
            EditorGUILayout.LabelField(p.Titre, EditorStyles.boldLabel);
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
        }
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

    void Journal(string message)
    {
        _journal.Add($"{DateTime.Now:HH:mm:ss.fff}  {message}");
        if (_journal.Count > 300) _journal.RemoveAt(0);
        Debug.Log("[EssaiCloud] " + message);
        Repaint();
    }
}
