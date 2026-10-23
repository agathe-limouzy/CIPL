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

    /// Rend un texte utilisable comme NOM DE FICHIER : les caractères interdits par
    /// le système deviennent des tirets. Mutualise une méthode `Sanitize` qui existait
    /// en cinq exemplaires (les quatre panneaux de facture et le service de quittance).
    public static string NomFichier(string nom)
    {
        if (nom == null) return "";
        foreach (var c in Path.GetInvalidFileNameChars()) nom = nom.Replace(c, '-');
        return nom;
    }

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

    // ── Rangement du 23/10 ─────────────────────────────────────────────────────
    //   Batiment/rivoli/_Infos du bâtiment/   Factures de charges, Objectifs, Photos
    //   Batiment/rivoli/Sephora/              Bail, Factures/<sorte>, Objectifs
    //   Anciens documents/                    les dossiers à identifiant d'avant le nommage par nom
    // ponytail: un locataire nommé « _Infos du bâtiment » partagerait ce dossier ; nom non refusé.

    public const string INFOS = "_Infos du bâtiment";
    public const string ANCIENS = "Anciens documents";

    /// Ce qui concerne le bâtiment lui-même, à part des dossiers de ses locataires.
    public static string DossierInfos(string nomBatiment)
        => Path.Combine(DossierBatiment(nomBatiment), INFOS);

    public static string DossierPhotos(string nomBatiment)
        => Path.Combine(DossierInfos(nomBatiment), "Photos");

    /// Les factures du locataire ; chaque sorte a son sous-dossier (`CheminFacture`).
    public static string DossierFactures(string nomBatiment, string nomLocataire)
        => Path.Combine(DossierLocataire(nomBatiment, nomLocataire), "Factures");

    public static string DossierCharges(string nomBatiment)
        => Path.Combine(DossierInfos(nomBatiment), "Factures de charges");

    /// Sous-dossier de « Factures », d'après le début du nom du PDF (les panneaux le nomment ainsi).
    static readonly (string debut, string dossier)[] SortesFacture =
    {
        ("Loyer-", "Loyers"), ("RegularisationdeCharge-", "Régularisations"), ("Refacturation-", "Refacturations"),
        ("Avoir-", "Avoirs"), ("DepotGarantie-", "Dépôt de garantie"), ("RevisionDepot-", "Dépôt de garantie"),
        ("DecompteSortie-", "Dépôt de garantie"), ("Quittance-", "Quittances"),
    };

    public static string SousDossierFacture(string fichier)
    {
        foreach (var (debut, dossier) in SortesFacture)
            if (fichier != null && fichier.StartsWith(debut, StringComparison.Ordinal)) return dossier;
        return "";
    }

    /// Emplacement d'un PDF de facture : `dossierFactures` (DossierFactures), puis sa sorte.
    public static string CheminFacture(string dossierFactures, string fichier)
        => Path.Combine(dossierFactures, SousDossierFacture(fichier), fichier);

    /// Aperçu d'une facture : un fichier de travail, hors du dossier de l'entreprise (ni partagé ni exporté).
    public static string Apercu(string fichier)
        => Path.Combine(Path.GetTempPath(), "CIPL", fichier);

    /// Où va un document rangé à l'ancienne (chemin relatif). Inchangé s'il est bien rangé ;
    /// null pour un aperçu laissé dans « Facture ». Sert au rangement des fichiers et à la
    /// relecture des chemins enregistrés avant (VersAbsolu) : les fiches ne sont pas réécrites.
    /// Les PDF de factures et de charges, eux, se relisent sous les noms actuels (PdfFacture,
    /// PdfCharge) : un chemin enregistré avant un renommage y mène aussi.
    public static string Nouveau(string chemin)
    {
        if (string.IsNullOrEmpty(chemin)) return chemin;
        var s = chemin.Split('/');
        if (s.Length < 3 || s[0] != "Batiment") return chemin;
        string f = s[s.Length - 1];
        if (s[s.Length - 2] == "Facture" && f.StartsWith("apercu", StringComparison.Ordinal) && f.EndsWith(".png", StringComparison.Ordinal)) return null;
        if (Guid.TryParse(s[1], out _)) return ANCIENS + chemin.Substring("Batiment".Length);

        string bat = "Batiment/" + s[1] + "/";
        if (s.Length > 3 && (s[2] == "Photos" || s[2] == "Objectifs")) return bat + INFOS + "/" + Reste(s, 2);
        if (s.Length > 3 && s[2] == "Charge") return bat + INFOS + "/Factures de charges/" + Reste(s, 3);
        if (s.Length < 5 || s[3] != "Facture") return chemin;

        string sorte = s.Length == 5 ? SousDossierFacture(f) : "";
        return bat + s[2] + "/Factures/" + (sorte == "" ? "" : sorte + "/") + Reste(s, 4);
    }

    static string Reste(string[] s, int depuis) => string.Join("/", s, depuis, s.Length - depuis);

    /// Bail et avenants, rangés dans le dossier du locataire à côté de ses factures.
    /// Seuls les NOMS de fichier sont enregistrés : un renommage déplace le dossier,
    /// et les documents suivent sans lien cassé.
    public static string DossierBail(string nomBatiment, string nomLocataire)
        => Path.Combine(DossierLocataire(nomBatiment, nomLocataire), "Bail");

    /// Documents joints aux objectifs : « Objectifs » du locataire, ou du bâtiment pour
    /// un objectif de bâtiment. Seuls les noms de fichier sont enregistrés (comme le bail).
    public static string DossierObjectifs(string nomBatiment, string nomLocataire)
        => Path.Combine(string.IsNullOrEmpty(nomLocataire) ? DossierInfos(nomBatiment)
                                                           : DossierLocataire(nomBatiment, nomLocataire), "Objectifs");

    /// Chemin d'un document du bail tel qu'enregistré : un nom de fichier (rangé dans
    /// DossierBail), ou un chemin complet hérité de l'ancien format.
    public static string CheminDocumentBail(string nomBatiment, string nomLocataire, string stocke)
    {
        if (string.IsNullOrEmpty(stocke) || Path.IsPathRooted(stocke)) return stocke;
        return Path.Combine(DossierBail(nomBatiment, nomLocataire), stocke);
    }

    /// Copie `source` dans `dossier` sans écraser un autre document : un « bail.pdf »
    /// déjà présent fait nommer la copie « bail (2).pdf ». Un fichier choisi DANS le
    /// dossier n'est pas recopié. Renvoie le nom de fichier retenu.
    public static string CopierSansEcraser(string source, string dossier)
    {
        Directory.CreateDirectory(dossier);
        string dejaIci = Path.GetFullPath(Path.Combine(dossier, Path.GetFileName(source)));
        if (string.Equals(Path.GetFullPath(source), dejaIci, StringComparison.OrdinalIgnoreCase))
            return Path.GetFileName(source);

        string nom = Path.GetFileNameWithoutExtension(source), ext = Path.GetExtension(source);
        string cible = Path.GetFileName(source);
        for (int i = 2; File.Exists(Path.Combine(dossier, cible)); i++) cible = $"{nom} ({i}){ext}";
        File.Copy(source, Path.Combine(dossier, cible));
        return cible;
    }

    // ── Renommage ──────────────────────────────────────────────────────────────
    // Les photos sont réécrites (ReporterPhotos) ; les PDF de factures et de charges gardent
    // leur chemin enregistré et sont retrouvés sous les nouveaux noms (PdfFacture, PdfCharge).

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
            DocumentsCloud.Bientot();   // les collègues voient les documents sous le nouveau nom sans attendre
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
            DocumentsCloud.Bientot();   // voir RenommerBatiment
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
        // Un chemin enregistré avant le rangement du 23/10 mène au nouvel emplacement, sauf si
        // ce PC n'a pas encore rangé ses documents (ouvert hors ligne depuis).
        string ancien = Absolu(cheminStocke), nouveau = Absolu(Nouveau(cheminStocke) ?? cheminStocke);
        return nouveau == ancien || File.Exists(nouveau) || !File.Exists(ancien) ? nouveau : ancien;
    }

    static string Absolu(string relatif) => Path.Combine(Racine, relatif.Replace('/', Path.DirectorySeparatorChar));

    /// PDF d'une facture (`FactureEtat.pdfPath`) : cherché sous les noms ACTUELS du bâtiment et
    /// du locataire, car un renommage déplace le dossier sans réécrire les fiches (sur le cloud,
    /// chaque locataire a son verrou). Le chemin enregistré ne sert que s'il mène seul au fichier.
    public static string PdfFacture(string nomBatiment, string nomLocataire, string stocke)
        => SousLesNoms(stocke, f => CheminFacture(DossierFactures(nomBatiment, nomLocataire), f));

    /// PDF d'une charge du bâtiment (`ChargeBatiment.pdfPath`), même règle que PdfFacture.
    public static string PdfCharge(string nomBatiment, string stocke)
        => SousLesNoms(stocke, f => Path.Combine(DossierCharges(nomBatiment), f));

    static string SousLesNoms(string stocke, Func<string, string> ici)
    {
        string enregistre = VersAbsolu(stocke);
        // Vide, ou absolu hors du dossier de l'entreprise (ancien format) : rien à recalculer.
        if (string.IsNullOrEmpty(stocke) || Path.IsPathRooted(VersRelatif(stocke))) return enregistre;
        string actuel = ici(Path.GetFileName(stocke));
        // Absent des deux côtés (cloud, Drive pas encore recopié) : le nom actuel, celui que le cloud connaît.
        return File.Exists(actuel) || !File.Exists(enregistre) ? actuel : enregistre;
    }
}
