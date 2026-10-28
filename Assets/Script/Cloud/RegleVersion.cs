using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// Étape 15 : la version installée face à la dernière publiée (version.json, publié sur GitHub avec
/// l'installeur ; décisions du 26/10). Plus récente : proposée (« Plus tard » possible) ; sous la
/// version minimale, que le serveur exige encore : obligatoire.
public static class RegleVersion
{
    public enum Etat { AJour, Proposee, Obligatoire }

    /// version.json. `mac` : le lien de la version Mac, vide tant qu'elle n'existe pas (étape 15b).
    [Serializable]
    public class Fichier { public string version, minimale, windows, mac; }

    static readonly Regex Forme = new Regex(@"^\d+(\.\d+){0,3}$");

    public static bool Valide(string v) => v != null && Forme.IsMatch(v);

    /// « 1.0.10 » > « 1.0.9 » ; « 1.2 » = « 1.2.0 ».
    public static int Comparer(string a, string b)
    {
        var x = a.Split('.').Select(int.Parse).ToArray();
        var y = b.Split('.').Select(int.Parse).ToArray();
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            int c = (i < x.Length ? x[i] : 0).CompareTo(i < y.Length ? y[i] : 0);
            if (c != 0) return c;
        }
        return 0;
    }

    /// Fichier illisible ou absent : à jour (rien ne s'affiche). Obligatoire seulement si la dernière
    /// version publiée suffit à remonter au-dessus de la minimale (sinon, mise à jour sans fin).
    /// `mac` (étape 15b) : sans version Mac publiée (lien vide), rien à proposer sur Mac.
    public static Etat Lire(string actuelle, Fichier f, bool mac = false)
    {
        if (f == null || !Valide(actuelle) || !Valide(f.version) || Comparer(actuelle, f.version) >= 0) return Etat.AJour;
        if (mac && string.IsNullOrEmpty(f.mac)) return Etat.AJour;
        bool tropVieille = Valide(f.minimale) && Comparer(actuelle, f.minimale) < 0 && Comparer(f.version, f.minimale) >= 0;
        return tropVieille ? Etat.Obligatoire : Etat.Proposee;
    }

    public static Fichier Analyser(string json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<Fichier>(json); }
        catch (Exception) { return null; }
    }

    public static string Ecrire(Fichier f) => JsonUtility.ToJson(f, true);

    /// Essais avant la publication (dépôt privé, décision du 26/10) : le contenu du fichier
    /// MiseAJour.FichierEssai, un dossier du PC ou une adresse, devient l'endroit où lire version.json
    /// et l'installeur, terminé par « / ». Vide ou illisible : null (GitHub).
    public static string Source(string contenu)
    {
        string s = contenu?.Trim().Trim('"').Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (s.Contains("://")) return s.TrimEnd('/') + "/";
        try { return new Uri(System.IO.Path.GetFullPath(s).TrimEnd('\\', '/') + "/").AbsoluteUri; }
        catch (Exception) { return null; }
    }

    public static string TexteProposee(string derniere, string actuelle) =>
        $"Nouvelle version de CIPL : {derniere} (vous avez la {actuelle}).";

    public static string TexteObligatoire(string actuelle, string derniere) =>
        $"Cette version de CIPL ({actuelle}) ne fonctionne plus avec le serveur. Installez la version {derniere} " +
        "pour continuer : vos données ne sont pas touchées.";

    /// `avancement` de 0 à 1 (UnityWebRequest.downloadProgress).
    public static string TexteTelechargement(string derniere, float avancement) =>
        $"Téléchargement de la version {derniere}… {Mathf.Clamp(Mathf.RoundToInt(avancement * 100), 0, 100)} % — " +
        "ensuite, CIPL se ferme, s'installe et se rouvre tout seul.";

    public const string TextePret = "Mise à jour prête : elle s'installe dès que CIPL se ferme, puis CIPL se rouvre.";

    /// Étape 15b (décision du 26/10) : sur Mac, la mise à jour se fait par le lien de téléchargement.
    public const string TexteMac = "La nouvelle version se télécharge dans votre navigateur : quittez CIPL, ouvrez le fichier " +
                                   "téléchargé, puis glissez CIPL dans Applications (« Remplacer »).";

    /// Retour du 26/10 (Windows avait bloqué l'installeur, sans rien afficher) : la version visée est
    /// notée avant la fermeture ; au lancement suivant, toujours en dessous, l'installation a échoué.
    public static bool MiseAJourEchouee(string visee, string actuelle) =>
        Valide(visee) && Valide(actuelle) && Comparer(actuelle, visee) < 0;

    public static string TexteEchec(string visee, string actuelle) =>
        $"La mise à jour vers la version {visee} n'a pas pu s'installer : votre version {actuelle} fonctionne toujours. " +
        "Vous pouvez réessayer avec « Mettre à jour ». Si cela se reproduit, Windows bloque peut-être l'installeur : signalez-le au support de CIPL.";
}
