using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// Étape 6b : où sont les documents de l'entreprise sur ce PC (décisions du 12/10 et du 14/10).
/// En mode « Drive partagé », CIPL retrouve seul le dossier grâce au repère CIPL_entreprise.json
/// (ImportLocal) déposé par l'admin ; chaque ordinateur range le Drive à sa façon.
public static class DossierEntreprise
{
    /// Nature du dossier retenu sur ce PC (gardée à côté de son chemin) : Drive trouvé tout
    /// seul, Drive choisi à la main, ou dossier hors du Drive (« Continuer sans le dossier »).
    public const string DriveAuto = "drive-auto", DriveMain = "drive-main", HorsDrive = "hors-drive";

    /// Où chercher : OneDrive (variables de Windows, dossiers « OneDrive… » du profil) et Google
    /// Drive (« Mon Drive » à la racine d'un lecteur, « Google Drive » dans le profil).
    /// ponytail: les bibliothèques SharePoint synchronisées (profil\<organisation>\…) ne sont pas
    /// devinées : « Choisir à la main » les couvre.
    public static List<string> Racines(Func<string, string> env, IEnumerable<string> dossiersProfil, IEnumerable<string> lecteurs)
    {
        var r = new List<string>();
        foreach (var v in new[] { "OneDriveCommercial", "OneDrive", "OneDriveConsumer" })
        {
            string p = env(v);
            if (!string.IsNullOrEmpty(p)) r.Add(p);
        }
        foreach (var d in dossiersProfil)
        {
            string nom = Path.GetFileName(d.TrimEnd('\\', '/'));
            if (nom.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase) || nom.Equals("Google Drive", StringComparison.OrdinalIgnoreCase))
                r.Add(d);
        }
        foreach (var l in lecteurs)
            foreach (var s in new[] { "Mon Drive", "My Drive", "Drives partagés", "Shared drives" })
                r.Add(Path.Combine(l, s));
        return r.Select(p => p.TrimEnd('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<string> RacinesDuPC()
    {
        string profil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] dossiers;
        try { dossiers = Directory.GetDirectories(profil); } catch (Exception) { dossiers = new string[0]; }
        var lecteurs = new List<string>();
        try
        {
            foreach (var d in DriveInfo.GetDrives())
                if (d.DriveType == DriveType.Fixed && d.IsReady) lecteurs.Add(d.RootDirectory.FullName);
        }
        catch (Exception) { /* lecteurs illisibles : on s'en passe */ }
        return Racines(Environment.GetEnvironmentVariable, dossiers, lecteurs).Where(Directory.Exists).ToList();
    }

    /// Le dossier qui porte le repère de cette entreprise sous l'une des racines (4 niveaux au
    /// plus, 5 000 dossiers par racine en moyenne) ; null sinon. Toutes les racines sont parcourues
    /// ensemble, niveau par niveau : un repère proche dans le 3e Drive est trouvé sans fouiller d'abord
    /// tout le 1er (3 min 30 le 15/10 avec un OneDrive et deux Google Drive). Lent sur un gros Drive :
    /// à lancer hors du fil principal.
    public static string Chercher(string entrepriseId, IEnumerable<string> racines, int profondeur = 4)
    {
        var file = new Queue<(string dossier, int niveau)>();
        foreach (var r in racines) file.Enqueue((r, 0));
        for (int vus = 0, max = 5000 * Math.Max(1, file.Count); file.Count > 0 && vus < max; vus++)
        {
            var (d, n) = file.Dequeue();
            if (ImportLocal.LireRepere(d)?.id == entrepriseId) return d;
            if (n >= profondeur) continue;
            string[] sous;
            try { sous = Directory.GetDirectories(d); } catch (Exception) { continue; }
            foreach (var s in sous)
            {
                string nom = Path.GetFileName(s);
                if (nom.StartsWith(".") || nom.StartsWith("$") || nom.StartsWith("corbeille_")) continue;
                file.Enqueue((s, n + 1));
            }
        }
        return null;
    }

    /// Dossier des documents d'après le dossier désigné : un sous-dossier au nom de l'entreprise,
    /// sauf si le dossier désigné est déjà le sien (il porte son repère).
    public static string Cible(string designe, string entrepriseId, string nomEntreprise) =>
        ImportLocal.LireRepere(designe)?.id == entrepriseId ? designe : Path.Combine(designe, DossiersDonnees.NomDossier(nomEntreprise));

    public static bool Meme(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    /// Dossier choisi à la main : refusé s'il ne porte pas le repère de CETTE entreprise ; null si accepté.
    public static string Refus(string dossier, string entrepriseId, string nomEntreprise)
    {
        var r = ImportLocal.LireRepere(dossier);
        if (r == null) return $"Ce dossier ne contient pas les documents d'une entreprise CIPL ({ImportLocal.NomRepere} absent).";
        if (r.id != entrepriseId) return $"Ce dossier est celui de « {r.nom} », pas de « {nomEntreprise} ».";
        return null;
    }

    /// Recopie Batiment/, Logo/ et reglage.json d'un dossier vers un autre, sans rien écraser
    /// (passage au Drive partagé, ou retour dans CIPL). Renvoie le nombre de documents recopiés.
    public static int Recopier(string depuis, string vers)
    {
        int n = 0;
        foreach (var sous in new[] { "Batiment", "Logo" })
        {
            string src = Path.Combine(depuis, sous);
            if (!Directory.Exists(src)) continue;
            foreach (var f in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
            {
                string dest = Path.Combine(vers, f.Substring(depuis.Length).TrimStart('\\', '/'));
                if (File.Exists(dest)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(f, dest);
                n++;
            }
        }
        string reglage = Path.Combine(depuis, "reglage.json"), reglageVers = Path.Combine(vers, "reglage.json");
        if (File.Exists(reglage) && !File.Exists(reglageVers)) File.Copy(reglage, reglageVers);
        return n;
    }
}
