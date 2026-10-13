using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// Essai technique (étape 1), dans un BUILD seulement :
///  - déclare cipl:// auprès de Windows pour la session courante ;
///  - si CIPL est déjà ouvert, une 2e fenêtre lancée PAR UN LIEN dépose ce lien dans
///    une boîte aux lettres et se ferme ; la 1re fenêtre le lit et l'affiche.
/// Ne fait rien dans l'éditeur. Sans lien en argument, rien ne change : on peut
/// toujours ouvrir deux fenêtres à la main, comme avant.
public class LienCiplRecepteur : MonoBehaviour
{
    static FileStream _instance;   // gardé ouvert en exclusif tant que la 1re fenêtre vit
    static string _enAttente;
    float _prochain;

    static string Boite => Path.Combine(Application.persistentDataPath, "cipl_lien_entrant.txt");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Amorcer()
    {
        if (Application.isEditor) return;

        string exe = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(exe) && LienCipl.EnregistrerProtocole(exe))
            Debug.Log("[LienCipl] cipl:// déclaré pour " + exe);

        LienCipl.TrouverDansArguments(Environment.GetCommandLineArgs(), out _, out string brut);

        if (!PrendreInstance())
        {
            if (brut == null) return;   // 2e fenêtre ouverte à la main : comportement inchangé
            try { File.WriteAllText(Boite, brut); }
            catch (Exception e) { Debug.LogError("[LienCipl] Lien non transmis : " + e.Message); return; }
            // ponytail: arrêt immédiat, AVANT le chargement de la scène, pour que cette 2e fenêtre
            // ne charge ni n'écrive aucune donnée (Application.Quit laisserait démarrer BatimentManager).
            // À remplacer par un vrai canal entre fenêtres quand le lien sera branché (étape 2).
            Process.GetCurrentProcess().Kill();
            return;
        }

        _enAttente = brut;
        var go = new GameObject("LienCiplRecepteur");
        DontDestroyOnLoad(go);
        go.AddComponent<LienCiplRecepteur>();
    }

    static bool PrendreInstance()
    {
        try
        {
            _instance = new FileStream(Path.Combine(Application.persistentDataPath, "cipl_instance.lock"),
                                       FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException) { return false; }
    }

    void Update()
    {
        if (_enAttente != null && UndoToast.Instance != null) { Afficher(_enAttente); _enAttente = null; }

        if (Time.unscaledTime < _prochain) return;
        _prochain = Time.unscaledTime + 0.5f;
        if (!File.Exists(Boite)) return;
        try
        {
            string brut = File.ReadAllText(Boite);
            File.Delete(Boite);
            _enAttente = brut;
        }
        catch (IOException) { /* fichier en cours d'écriture : prochain passage */ }
    }

    static void Afficher(string brut)
    {
        if (!LienCipl.Analyser(brut, out var lien)) return;
        string debut = lien.Jeton.Length > 6 ? lien.Jeton.Substring(0, 6) + "…" : lien.Jeton;
        Debug.Log($"[LienCipl] Lien reçu : {lien.Type}, jeton {debut}");
        UndoToast.Instance.ShowInfo($"Lien reçu : {lien.Type} (jeton {debut})");
    }
}
