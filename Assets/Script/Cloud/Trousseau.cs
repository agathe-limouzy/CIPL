using System;
using System.Diagnostics;

/// Étape 15b (décision du 26/10) : la connexion gardée sur Mac, dans le trousseau de macOS (l'équivalent
/// de Dpapi sous Windows), par la commande `security` du système. Service « CIPL », un compte par usage.
/// ponytail: le jeton passe en argument de `security`, visible un instant par les programmes du même
/// compte macOS ; une extension native (SecItemAdd) si un audit de sécurité le demande.
public static class Trousseau
{
    const string Service = "CIPL", Commande = "/usr/bin/security";

    public static string[] ArgsEcrire(string compte, string secret) =>
        new[] { "add-generic-password", "-U", "-a", compte, "-s", Service, "-w", secret };
    public static string[] ArgsLire(string compte) => new[] { "find-generic-password", "-a", compte, "-s", Service, "-w" };
    public static string[] ArgsEffacer(string compte) => new[] { "delete-generic-password", "-a", compte, "-s", Service };

    public static bool Ecrire(string compte, string secret) => Lancer(ArgsEcrire(compte, secret), out _) == 0;

    /// Null s'il n'y a rien (ou si le trousseau ne répond pas).
    public static string Lire(string compte) => Lancer(ArgsLire(compte), out string sortie) == 0 ? sortie.TrimEnd('\r', '\n') : null;

    public static void Effacer(string compte) => Lancer(ArgsEffacer(compte), out _);

    static int Lancer(string[] args, out string sortie)
    {
        sortie = "";
        try
        {
            var psi = new ProcessStartInfo(Commande) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using (var p = Process.Start(psi))
            {
                sortie = p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                return p.WaitForExit(10000) ? p.ExitCode : -1;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[Trousseau] " + e.Message);
            return -1;
        }
    }
}
