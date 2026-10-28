using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

/// Étape 15 (décisions et maquettes du 26/10) : au lancement d'une version installée, la dernière
/// version est lue dans version.json, publié sur GitHub avec l'installeur. Plus récente : bandeau
/// « Plus tard » / « Mettre à jour » sous les onglets ; sous la version minimale : fenêtre bloquante.
/// « Mettre à jour » télécharge l'installeur et le lance quand CIPL se ferme vraiment, après la garde
/// de fermeture (une saisie en cours n'est jamais perdue) ; il s'installe sans question et rouvre CIPL.
public class MiseAJour : MonoBehaviour
{
    /// Le dépôt public qui ne contient que les installeurs (décision du 26/10).
    public const string Depot = "https://github.com/agathe-limouzy/CIPL-installation";
    public const string Installeur = "CIPL-installation.exe", Exe = "CIPL.exe", ZipMac = "CIPL-mac.zip";
    /// Les fichiers de la dernière version publiée (GitHub suit « latest » tout seul).
    public const string Publiee = Depot + "/releases/latest/download/";

    /// version.json lu au lancement : null avant, dans l'éditeur, ou sans connexion.
    public static RegleVersion.Fichier Derniere { get; private set; }
    public static string LienWindows => string.IsNullOrEmpty(Derniere?.windows) ? Publiee + Installeur : Derniere.windows;
    /// Null tant que la version Mac n'est pas publiée (étape 15b).
    public static string LienMac => string.IsNullOrEmpty(Derniere?.mac) ? null : Derniere.mac;

    /// Essais avant la publication (dépôt privé tant que CIPL n'est pas sorti, décision du 26/10) : ce
    /// fichier, dans le dossier caché de CIPL, indique un dossier du PC (Build\Installation) où lire
    /// version.json et l'installeur à la place de GitHub. Un client ne l'a jamais.
    public const string FichierEssai = "cipl_mises_a_jour.txt";

    static string SourceEssai()
    {
        try
        {
            string f = Path.Combine(Application.persistentDataPath, FichierEssai);
            return File.Exists(f) ? RegleVersion.Source(File.ReadAllText(f)) : null;
        }
        catch (Exception) { return null; }
    }

    static MiseAJour _instance;
    string _source = Publiee;
    RegleVersion.Etat _etat;
    bool _plusTard, _enCours;
    GameObject _bandeau, _fenetre;
    TMP_Text _texte;
    Action<string> _afficherFenetre;

    /// La version visée par la dernière mise à jour lancée : relue (puis effacée) au lancement suivant.
    static string FichierEnCours => Path.Combine(Application.persistentDataPath, "cipl_mise_a_jour_en_cours.txt");

    /// Appelé au démarrage (DemarrageCloud.Lancer) sans être attendu : le lancement n'est pas retardé.
    public static void Verifier(Func<Canvas> canvas)
    {
        if (Application.isEditor || _instance != null) return;   // l'éditeur n'est pas une version installée
        var go = new GameObject("MiseAJour");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MiseAJour>();
        _instance.StartCoroutine(_instance.Bilan());
        _instance.StartCoroutine(_instance.Lire(canvas));
    }

    /// Après une mise à jour : réussie, un message en bas ; échouée, au centre (retour du 26/10).
    IEnumerator Bilan()
    {
        string visee;
        try
        {
            if (!File.Exists(FichierEnCours)) yield break;
            visee = File.ReadAllText(FichierEnCours).Trim();
            File.Delete(FichierEnCours);
        }
        catch (Exception e) { Debug.LogWarning("[MiseAJour] Bilan illisible : " + e.Message); yield break; }
        while (ConfirmDialog.Instance == null || UndoToast.Instance == null) yield return null;
        Debug.Log($"[MiseAJour] Visée {visee}, installée {Application.version}");
        if (RegleVersion.MiseAJourEchouee(visee, Application.version))
            ConfirmDialog.Erreur(RegleVersion.TexteEchec(visee, Application.version));
        else
            UndoToast.Instance.ShowInfo($"CIPL est à jour : version {Application.version}.");
    }

    IEnumerator Lire(Func<Canvas> canvas)
    {
        _source = SourceEssai() ?? Publiee;
        using (var r = UnityWebRequest.Get(_source + "version.json"))
        {
            r.timeout = 15;
            yield return r.SendWebRequest();
            if (r.result != UnityWebRequest.Result.Success) { Debug.Log($"[MiseAJour] {_source}version.json non lu : {r.error}"); yield break; }
            Derniere = RegleVersion.Analyser(r.downloadHandler.text);
        }
        _etat = RegleVersion.Lire(Application.version, Derniere, Plateforme.Mac);
        Debug.Log($"[MiseAJour] Installée {Application.version}, publiée {Derniere?.version}, minimale {Derniere?.minimale} : {_etat}");
        if (_etat != RegleVersion.Etat.Obligatoire) yield break;
        while (canvas() == null) yield return null;
        (_fenetre, _afficherFenetre) = EcransCloud.MiseAJourObligatoire(canvas(), Application.version, Derniere.version,
                                                                       () => StartCoroutine(Telecharger(_afficherFenetre)));
    }

    void Update()
    {
        // La fenêtre obligatoire reste devant tout (connexion, choix de l'entreprise…), sauf un message.
        if (_fenetre != null && !(ConfirmDialog.Instance != null && ConfirmDialog.Instance.gameObject.activeSelf))
            _fenetre.transform.SetAsLastSibling();

        var barre = _etat == RegleVersion.Etat.Proposee && !_plusTard ? HorsLigne.BarreOnglets() : null;
        if (barre == null) { if (_bandeau != null) Destroy(_bandeau); _bandeau = null; return; }
        if (_bandeau == null) Construire(barre);
    }

    /// Maquette 1 du 26/10 : même place et même forme que le bandeau de l'abonnement.
    void Construire(RectTransform barre)
    {
        _bandeau = HorsLigne.NouveauBandeau(barre, "BandeauMiseAJour", UITheme.PrimaireClair, UITheme.Primaire,
                                            "Plus tard", Color.white, UITheme.Primaire, out _texte, out var plusTard);
        plusTard.onClick.AddListener(() => _plusTard = true);   // jusqu'au prochain lancement
        var maj = UIFactory.Button(_bandeau.transform, "Mettre à jour", UITheme.Primaire, Color.white, 34, UITheme.Role.Action, true);
        UIFactory.LargeurDuTexte(maj);
        maj.onClick.AddListener(() =>
        {
            plusTard.gameObject.SetActive(false);
            maj.gameObject.SetActive(false);
            StartCoroutine(Telecharger(AfficherBandeau));
        });
        _texte.text = _enCours ? RegleVersion.TextePret : RegleVersion.TexteProposee(Derniere.version, Application.version);
    }

    /// Null : retour à la proposition (le bandeau se reconstruit avec ses boutons).
    void AfficherBandeau(string t)
    {
        if (t == null) { if (_bandeau != null) Destroy(_bandeau); _bandeau = null; }
        else if (_texte != null) _texte.text = t;
    }

    IEnumerator Telecharger(Action<string> afficher)
    {
        if (Plateforme.Mac)
        {
            // Étape 15b : pas d'installeur ; le .zip s'ouvre dans le navigateur (essai : depuis le dossier du PC).
            Application.OpenURL(_source == Publiee ? LienMac : _source + ZipMac);
            afficher(RegleVersion.TexteMac);
            yield break;
        }
        if (_enCours) yield break;
        _enCours = true;
        string fichier = Path.Combine(Path.GetTempPath(), $"CIPL-installation-{Derniere.version}.exe");
        string lien = _source == Publiee ? LienWindows : _source + Installeur;   // essai : l'installeur du dossier du PC
        using (var r = new UnityWebRequest(lien, UnityWebRequest.kHttpVerbGET, new DownloadHandlerFile(fichier) { removeFileOnAbort = true }, null))
        {
            var envoi = r.SendWebRequest();
            while (!envoi.isDone) { afficher(RegleVersion.TexteTelechargement(Derniere.version, r.downloadProgress)); yield return null; }
            if (r.result != UnityWebRequest.Result.Success)
            {
                _enCours = false;
                afficher(null);
                ConfirmDialog.Erreur($"Téléchargement de la mise à jour impossible ({r.error}). Vérifiez la connexion, puis réessayez.");
                yield break;
            }
        }
        afficher(RegleVersion.TextePret);
        Debug.Log("[MiseAJour] Installeur téléchargé : " + fichier);
        // Lancé à la vraie fermeture : si la garde demande d'abord d'enregistrer une fiche et qu'on
        // annule, l'installation attend la fermeture suivante. /CLOSEAPPLICATIONS : l'installeur
        // attend que CIPL soit fermé ; il le rouvre à la fin (CIPL.iss, [Run]).
        try { File.WriteAllText(FichierEnCours, Derniere.version); }
        catch (Exception e) { Debug.LogWarning("[MiseAJour] Version visée non notée : " + e.Message); }
        Application.quitting += () =>
        {
            try { Process.Start(new ProcessStartInfo(fichier, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS") { UseShellExecute = true }); }
            catch (Exception e) { Debug.LogWarning("[MiseAJour] Installeur non lancé : " + e.Message); }   // le bilan le dira au prochain lancement
        };
        Application.Quit();
    }
}
