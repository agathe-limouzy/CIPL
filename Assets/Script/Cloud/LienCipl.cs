using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

/// Liens des e-mails (invitation, mot de passe oublié, confirmation) qui ouvrent CIPL :
/// cipl://<type>?jeton=<jeton>. Windows lance CIPL avec ce lien en argument.
/// La page relais https://cipl.app/o/… (Gmail et Outlook web n'affichent pas cipl://)
/// viendra à l'étape 2. Voir CONCEPTION_MULTIUTILISATEUR.md §13.
public static class LienCipl
{
    public static readonly string[] Types = { "invitation", "motdepasse", "confirmation" };

    public struct Lien { public string Type; public string Jeton; }

    // Windows ajoute parfois une barre après l'hôte : « cipl://invitation/?jeton=… ».
    static readonly Regex Forme = new Regex(@"^cipl://([a-z]+)/?\?(.*)$", RegexOptions.IgnoreCase);
    static readonly Regex JetonValide = new Regex(@"^[A-Za-z0-9_\-\.]{8,512}$");

    public static bool Analyser(string texte, out Lien lien)
    {
        lien = default;
        if (string.IsNullOrWhiteSpace(texte)) return false;

        var m = Forme.Match(texte.Trim().Trim('"'));
        if (!m.Success) return false;

        string type = m.Groups[1].Value.ToLowerInvariant();
        if (Array.IndexOf(Types, type) < 0) return false;

        foreach (var p in m.Groups[2].Value.Split('&'))
        {
            int i = p.IndexOf('=');
            if (i <= 0 || p.Substring(0, i) != "jeton") continue;
            string jeton = Uri.UnescapeDataString(p.Substring(i + 1));
            if (!JetonValide.IsMatch(jeton)) return false;
            lien = new Lien { Type = type, Jeton = jeton };
            return true;
        }
        return false;
    }

    /// Premier lien valide parmi les arguments de lancement (Unity en ajoute d'autres).
    public static bool TrouverDansArguments(string[] args, out Lien lien, out string brut)
    {
        lien = default; brut = null;
        if (args == null) return false;
        foreach (var a in args)
            if (Analyser(a, out lien)) { brut = a; return true; }
        return false;
    }

    /// Commandes reg.exe qui déclarent cipl:// pour la session Windows courante
    /// (HKCU : pas besoin d'être administrateur ; rien n'est touché pour les autres comptes).
    public static string[] CommandesRegistre(string exe)
    {
        const string cle = @"HKCU\Software\Classes\cipl";
        return new[]
        {
            $"add \"{cle}\" /ve /d \"URL:CIPL\" /f",
            $"add \"{cle}\" /v \"URL Protocol\" /d \"\" /f",
            $"add \"{cle}\\shell\\open\\command\" /ve /d \"\\\"{exe}\\\" \\\"%1\\\"\" /f",
        };
    }

    /// Exécute les commandes ci-dessus. Appelé seulement par un build (LienCiplRecepteur).
    public static bool EnregistrerProtocole(string exe)
    {
        try
        {
            foreach (var args in CommandesRegistre(exe))
            {
                using (var p = Process.Start(new ProcessStartInfo("reg.exe", args)
                       { CreateNoWindow = true, UseShellExecute = false }))
                {
                    if (p == null || !p.WaitForExit(5000) || p.ExitCode != 0) return false;
                }
            }
            return true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[LienCipl] Enregistrement de cipl:// impossible : " + e.Message);
            return false;
        }
    }
}
