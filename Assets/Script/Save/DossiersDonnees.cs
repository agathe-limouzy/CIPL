using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// Emplacement des données d'un bâtiment et de ses locataires, nommé par le NOM
/// (lisible dans l'explorateur) et non par l'identifiant technique.
///
/// Le nommage par nom impose deux contraintes, traitées ici et dans la validation
/// de saisie : les noms doivent être UNIQUES (deux homonymes partageraient le même
/// dossier, donc leurs factures et leurs photos), et ils doivent être convertis en
/// noms de dossier valides — ce qui va bien au-delà des caractères interdits.
public static class DossiersDonnees
{
    /// Longueur maximale retenue pour un segment. Le chemin complet
    /// (racine + Batiment + bâtiment + locataire + Facture + nom de PDF) doit rester
    /// sous la limite historique de 260 caractères de Windows.
    private const int LONGUEUR_MAX = 60;

    public const string SANS_NOM = "Sans nom";

    /// Noms réservés par Windows : un dossier ainsi nommé est refusé par le système,
    /// quelle que soit l'extension.
    private static readonly HashSet<string> Reserves = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// Convertit un nom saisi en segment de dossier sûr.
    /// Déterministe : deux appels sur le même nom donnent toujours le même dossier.
    public static string NomDossier(string nom)
    {
        if (string.IsNullOrWhiteSpace(nom)) return SANS_NOM;

        var invalides = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (char c in nom.Trim())
            sb.Append(Array.IndexOf(invalides, c) >= 0 ? '-' : c);

        // Windows supprime SILENCIEUSEMENT les points et espaces finaux : sans ce
        // nettoyage, le dossier créé ne porterait pas le nom qu'on croit avoir écrit,
        // et toute recherche ultérieure par ce nom échouerait.
        string s = sb.ToString().TrimEnd('.', ' ');
        if (s.Length == 0) return SANS_NOM;

        if (s.Length > LONGUEUR_MAX)
            s = s.Substring(0, LONGUEUR_MAX).TrimEnd('.', ' ');
        if (s.Length == 0) return SANS_NOM;

        // Un nom réservé est préfixé plutôt que remplacé, pour rester reconnaissable.
        if (Reserves.Contains(s)) s = "_" + s;

        return s;
    }

    /// Vrai si deux noms aboutiraient au même dossier (comparaison insensible à la
    /// casse : Windows ne distingue pas « Rivoli » de « rivoli »).
    public static bool MemeDossier(string nomA, string nomB)
        => string.Equals(NomDossier(nomA), NomDossier(nomB), StringComparison.OrdinalIgnoreCase);

    private static string Racine => SaveLocationService.GetSaveRoot();

    public static string DossierBatiment(string nomBatiment)
        => Path.Combine(Racine, "Batiment", NomDossier(nomBatiment));

    public static string DossierLocataire(string nomBatiment, string nomLocataire)
        => Path.Combine(DossierBatiment(nomBatiment), NomDossier(nomLocataire));

    /// Photos rangées DANS le dossier du bâtiment (et non dans un « Photos/&lt;id&gt; »
    /// séparé), pour que tout ce qui concerne un bâtiment vive au même endroit.
    public static string DossierPhotos(string nomBatiment)
        => Path.Combine(DossierBatiment(nomBatiment), "Photos");

    public static string DossierFactures(string nomBatiment, string nomLocataire)
        => Path.Combine(DossierLocataire(nomBatiment, nomLocataire), "Facture");

    public static string DossierCharges(string nomBatiment)
        => Path.Combine(DossierBatiment(nomBatiment), "Charge");

    // ── Renommage ──────────────────────────────────────────────────────────────

    /// Déplace le dossier d'un bâtiment quand son nom change.
    /// Renvoie false (sans rien modifier) si la destination est déjà prise ou si le
    /// déplacement échoue — un PDF ouvert dans un lecteur suffit à verrouiller le
    /// dossier. L'appelant DOIT alors refuser le renommage, sinon les factures et les
    /// photos resteraient orphelines sous l'ancien nom.
    public static bool RenommerBatiment(string ancienNom, string nouveauNom, out string erreur)
    {
        erreur = null;
        if (MemeDossier(ancienNom, nouveauNom)) return true;   // rien à faire

        string source = DossierBatiment(ancienNom);
        string destination = DossierBatiment(nouveauNom);

        if (!Directory.Exists(source)) return true;            // aucun dossier encore créé

        if (Directory.Exists(destination))
        {
            erreur = $"un dossier « {NomDossier(nouveauNom)} » existe déjà";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            Directory.Move(source, destination);
            return true;
        }
        catch (Exception e)
        {
            erreur = e.Message;
            return false;
        }
    }

    /// Déplace le dossier d'un locataire à l'intérieur de son bâtiment.
    public static bool RenommerLocataire(string nomBatiment, string ancienNom, string nouveauNom, out string erreur)
    {
        erreur = null;
        if (MemeDossier(ancienNom, nouveauNom)) return true;

        string source = DossierLocataire(nomBatiment, ancienNom);
        string destination = DossierLocataire(nomBatiment, nouveauNom);

        if (!Directory.Exists(source)) return true;

        if (Directory.Exists(destination))
        {
            erreur = $"un dossier « {NomDossier(nouveauNom)} » existe déjà dans ce bâtiment";
            return false;
        }

        try
        {
            Directory.Move(source, destination);
            return true;
        }
        catch (Exception e)
        {
            erreur = e.Message;
            return false;
        }
    }

    /// Réécrit les chemins de photos après un changement de nom de bâtiment : le
    /// dossier est recalculé depuis le nouveau nom, seul le nom de fichier est conservé.
    public static void ReporterPhotos(List<string> photos, ref string coverPhoto, string nouveauNom)
    {
        if (photos != null)
            for (int i = 0; i < photos.Count; i++)
                photos[i] = RecomposerPhoto(photos[i], nouveauNom);

        if (!string.IsNullOrEmpty(coverPhoto))
            coverPhoto = RecomposerPhoto(coverPhoto, nouveauNom);
    }

    private static string RecomposerPhoto(string stocke, string nouveauNom)
    {
        if (string.IsNullOrEmpty(stocke)) return stocke;
        string fichier = Path.GetFileName(stocke);
        if (string.IsNullOrEmpty(fichier)) return stocke;
        return VersRelatif(Path.Combine(DossierPhotos(nouveauNom), fichier));
    }

    // ── Chemins relatifs ───────────────────────────────────────────────────────

    /// Convertit un chemin absolu situé sous la racine en chemin RELATIF à celle-ci.
    /// Les chemins absolus stockés dans le JSON cassaient dès que la sauvegarde
    /// changeait de disque ou de machine, alors même que les fichiers suivaient.
    public static string VersRelatif(string cheminAbsolu)
    {
        if (string.IsNullOrEmpty(cheminAbsolu)) return cheminAbsolu;
        try
        {
            string racine = Path.GetFullPath(Racine)
                                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string complet = Path.GetFullPath(cheminAbsolu);

            if (complet.StartsWith(racine, StringComparison.OrdinalIgnoreCase))
                return complet.Substring(racine.Length)
                              .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              .Replace('\\', '/');   // séparateur stable dans le JSON
        }
        catch { /* chemin mal formé : on le laisse tel quel */ }
        return cheminAbsolu;
    }

    /// Reconstruit un chemin exploitable. Accepte aussi bien un chemin relatif
    /// (nouveau format) qu'un chemin absolu hérité de l'ancien.
    public static string VersAbsolu(string cheminStocke)
    {
        if (string.IsNullOrEmpty(cheminStocke)) return cheminStocke;
        if (Path.IsPathRooted(cheminStocke)) return cheminStocke;   // ancien format
        return Path.Combine(Racine, cheminStocke.Replace('/', Path.DirectorySeparatorChar));
    }
}
