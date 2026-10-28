using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// Étape 15 : CIPL › Construire l'installeur. Le build Windows, version.json et l'installeur Inno
/// Setup (Installeur/CIPL.iss), dans Build/Installation à côté du dépôt (hors git). La version se
/// règle dans Project Settings › Player › Version (1.0.0, 1.0.1…). Ensuite, les deux fichiers se
/// publient sur GitHub (PARCOURS_TEST_CIPL.md, section AS).
public static class ConstruireInstalleur
{
    /// La plus ancienne version qui marche encore avec le serveur. À monter quand un nouveau SQL rend
    /// les anciennes versions incompatibles : en dessous, la mise à jour devient obligatoire.
    public const string VersionMinimale = "1.0.0";

    static string Sortie => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Build", "Installation"));
    static string Script => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Installeur", "CIPL.iss"));

    static string Iscc() => new[]
    {
        @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), @"Programs\Inno Setup 6\ISCC.exe"),
    }.FirstOrDefault(File.Exists);

    [UnityEditor.MenuItem("CIPL/Construire l'installeur")]   // nom complet : le projet a sa propre classe MenuItem
    static void Construire()
    {
        string version = PlayerSettings.bundleVersion;
        string refus = !RegleVersion.Valide(version) ? $"Version « {version} » : écrivez-la en chiffres, par exemple 1.0.1 (Project Settings › Player › Version)."
                     : RegleVersion.Comparer(version, VersionMinimale) < 0 ? $"La version {version} est sous la version minimale ({VersionMinimale})."
                     : Iscc() == null ? "Inno Setup 6 est introuvable : installez-le (https://jrsoftware.org/isdl.php), puis recommencez."
                     : null;
        if (refus != null) { EditorUtility.DisplayDialog("Construire l'installeur", refus, "OK"); return; }

        string appli = Path.Combine(Sortie, "CIPL");
        if (Directory.Exists(appli)) Directory.Delete(appli, true);   // notre propre sortie : aucun reste du build précédent
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Path.Combine(appli, MiseAJour.Exe),
            target = BuildTarget.StandaloneWindows64,
        });
        if (r.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Construire l'installeur", "Le build a échoué : voir la console.", "OK");
            return;
        }
        foreach (var d in Directory.GetDirectories(appli, "*_DoNotShip")) Directory.Delete(d, true);   // débogage de Burst, à ne pas livrer

        File.WriteAllText(Path.Combine(Sortie, "version.json"), RegleVersion.Ecrire(new RegleVersion.Fichier
        {
            version = version, minimale = VersionMinimale, windows = MiseAJour.Publiee + MiseAJour.Installeur, mac = "",
        }));

        var p = Process.Start(new ProcessStartInfo(Iscc(), $"/Q \"/DVersion={version}\" \"/DSource={appli}\" \"/O{Sortie}\" \"{Script}\"")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        });
        string journal = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            Debug.LogError("[Installeur] Inno Setup : " + journal);
            EditorUtility.DisplayDialog("Construire l'installeur", "Inno Setup a échoué : voir la console.", "OK");
            return;
        }
        string exe = Path.Combine(Sortie, MiseAJour.Installeur);
        Debug.Log($"[Installeur] CIPL {version} prêt : {exe} ({new FileInfo(exe).Length / 1048576} Mo) et version.json");
        EditorUtility.RevealInFinder(exe);
    }

    // ── Étape 15b : la version Mac (décisions du 26/10 : un .zip, mise à jour par le lien) ────────

    [UnityEditor.MenuItem("CIPL/Construire la version Mac")]
    static void ConstruireMac()
    {
        string version = PlayerSettings.bundleVersion, json = Path.Combine(Sortie, "version.json");
        var fichier = File.Exists(json) ? RegleVersion.Analyser(File.ReadAllText(json)) : null;
        string refus = !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX)
                         ? "Le module « Mac Build Support (Mono) » n'est pas installé : Unity Hub › Installs › 2023.2.22f1 › Add modules."
                     : fichier?.version != version
                         ? $"Construisez d'abord l'installeur Windows de la version {version} (CIPL › Construire l'installeur) : la version Mac s'ajoute à son version.json."
                     : null;
        if (refus != null) { EditorUtility.DisplayDialog("Construire la version Mac", refus, "OK"); return; }

        string dossier = Path.Combine(Sortie, "Mac"), app = Path.Combine(dossier, "CIPL.app");
        if (Directory.Exists(dossier)) Directory.Delete(dossier, true);   // notre propre sortie
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = app,
            target = BuildTarget.StandaloneOSX,
        });
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);   // l'éditeur reste en Windows
        if (r.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Construire la version Mac", "Le build Mac a échoué : voir la console.", "OK");
            return;
        }
        foreach (var d in Directory.GetDirectories(dossier, "*_DoNotShip")) Directory.Delete(d, true);

        string zip = Path.Combine(Sortie, MiseAJour.ZipMac);
        ZipperApp(app, zip);
        fichier.mac = MiseAJour.Publiee + MiseAJour.ZipMac;
        File.WriteAllText(json, RegleVersion.Ecrire(fichier));
        Debug.Log($"[Installeur] CIPL {version} pour Mac prêt : {zip} ({new FileInfo(zip).Length / 1048576} Mo) ; version.json porte le lien Mac");
        EditorUtility.RevealInFinder(zip);
    }

    /// Le .zip de l'application Mac, fait sous Windows : le programme (Contents/MacOS) garde son droit
    /// d'exécution, sans quoi macOS ouvrirait une application qui ne se lance pas.
    public static void ZipperApp(string app, string zip)
    {
        if (File.Exists(zip)) File.Delete(zip);
        string parent = Path.GetDirectoryName(Path.GetFullPath(app));
        using (var z = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            foreach (var f in Directory.GetFiles(app, "*", SearchOption.AllDirectories))
            {
                string nom = Path.GetFullPath(f).Substring(parent.Length + 1).Replace('\\', '/');
                var e = System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(z, f, nom, System.IO.Compression.CompressionLevel.Optimal);
                e.ExternalAttributes = (nom.Contains("/Contents/MacOS/") ? 0x81ED : 0x81A4) << 16;   // -rwxr-xr-x ou -rw-r--r--
            }
        MarquerUnix(zip);
    }

    /// Chaque fiche du répertoire central du zip déclare « fait sous Unix » (octet haut de « version made
    /// by » = 3) : sinon l'Utilitaire d'archive de macOS ignore les droits écrits ci-dessus. Le répertoire
    /// est lu depuis sa fin (« PK\5\6 ») : jamais de recherche au hasard dans les données compressées.
    public static void MarquerUnix(string zip)
    {
        var b = File.ReadAllBytes(zip);
        int fin = -1;
        for (int i = b.Length - 22; i >= 0 && fin < 0; i--)
            if (b[i] == 0x50 && b[i + 1] == 0x4B && b[i + 2] == 0x05 && b[i + 3] == 0x06) fin = i;
        if (fin < 0) throw new IOException("Zip illisible : fin du répertoire central introuvable.");
        int n = System.BitConverter.ToUInt16(b, fin + 10), p = (int)System.BitConverter.ToUInt32(b, fin + 16);
        for (int k = 0; k < n; k++)
        {
            if (System.BitConverter.ToUInt32(b, p) != 0x02014B50) throw new IOException("Zip illisible : fiche du répertoire central attendue.");
            b[p + 5] = 3;
            p += 46 + System.BitConverter.ToUInt16(b, p + 28) + System.BitConverter.ToUInt16(b, p + 30) + System.BitConverter.ToUInt16(b, p + 32);
        }
        File.WriteAllBytes(zip, b);
    }
}
