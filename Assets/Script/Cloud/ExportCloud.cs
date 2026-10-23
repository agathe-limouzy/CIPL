using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// Étape 11 : les règles de l'export (testées en EditMode). Décisions du 23/10 : l'admin, et
/// tout le monde quand l'abonnement a expiré ; l'entreprise ouverte ; un dossier au format CIPL ;
/// les documents absents de ce PC pris dans le cloud.
public static class RegleExport
{
    public static bool PeutExporter(bool admin, RegleAbonnement.Etat etat) => admin || etat == RegleAbonnement.Etat.Expire;

    /// « Démo CIPL — export du 23-10-2026 » (nom de dossier valable sous Windows).
    public static string NomDossier(string entreprise, DateTime date) =>
        DossiersDonnees.NomDossier($"{entreprise} — export du {date:dd-MM-yyyy}");

    /// Le nom, ou « nom (2) », « nom (3) »… s'il est déjà pris : un export n'en écrase jamais un autre.
    public static string Libre(string nom, Func<string, bool> existe)
    {
        if (!existe(nom)) return nom;
        for (int i = 2; ; i++)
        {
            string n = $"{nom} ({i})";
            if (!existe(n)) return n;
        }
    }

    /// Les documents à exporter : ceux du cloud et ceux de ce PC ; un document présent ici est
    /// copié (`depuisCloud` faux), les autres sont téléchargés.
    public static List<(string chemin, bool depuisCloud)> Documents(IEnumerable<string> serveur, ICollection<string> locaux) =>
        serveur.Concat(locaux).Distinct().OrderBy(c => c, StringComparer.Ordinal)
               .Select(c => (c, !locaux.Contains(c))).ToList();

    public static string Bilan(int batiments, int locataires, int documents) =>
        $"{batiments} bâtiment{S(batiments)}, {locataires} locataire{S(locataires)}, {documents} document{S(documents)}";

    public static string DernierExport(DateTime quand, string bilan) =>
        $"Dernier export : {quand:dd/MM/yyyy} à {quand.Hour} h {quand:mm} — {bilan}.";

    static string S(int n) => n > 1 ? "s" : "";
}

/// Étape 11 : « Exporter toutes les données » de l'entreprise ouverte, dans un dossier rangé comme
/// celui de l'entreprise (rangement du 23/10) : la fiche du bâtiment dans « _Infos du bâtiment »,
/// celle du locataire dans son dossier, reglage.json à la racine ; son import dans CIPL reste à
/// faire. Les fiches sont relues sur le serveur ; les documents absents de ce PC sont pris dans le
/// stockage du cloud. Ni les secrets (fichier à part), ni le repère d'import, ni la corbeille ne partent.
public static class ExportCloud
{
    public class Resultat { public string Dossier, Bilan, Erreur; public List<string> Manquants = new List<string>(); }
    [Serializable] class LigneDoc { public string chemin, empreinte; }
    [Serializable] class LignesDoc { public LigneDoc[] items; }

    /// `progression(fait, total)` : les documents, un par un.
    public static async Task<Resultat> Exporter(string parent, Action<int, int> progression)
    {
        var e = SessionCloud.Entreprise;
        if (e == null) return new Resultat { Erreur = "aucune entreprise ouverte." };
        string racine = SaveLocationService.GetSaveRoot();
        string nom = RegleExport.Libre(RegleExport.NomDossier(e.nom, DateTime.Now),
                                       n => Directory.Exists(Path.Combine(parent, n)) || File.Exists(Path.Combine(parent, n)));
        string dossier = Path.Combine(parent, nom);

        var (bats, erreur) = await DonneesCloud.Charger(e.id);
        if (bats == null) return new Resultat { Erreur = "lecture des fiches impossible : " + erreur };

        // ponytail: une seule lecture, plafonnée à 1 000 lignes par Supabase ; paginer au-delà.
        var r = await CloudClient.Table("GET", SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "documents",
                                        $"entreprise_id=eq.{e.id}&supprime_le=is.null&select=chemin,empreinte");
        if (!r.Ok) return new Resultat { Erreur = "liste des documents illisible : " + r.Erreur };
        var lignes = (JsonUtility.FromJson<LignesDoc>("{\"items\":" + r.Corps + "}")?.items ?? new LigneDoc[0])
            .Where(l => SyncDocuments.Partage(l.chemin) && !string.IsNullOrEmpty(l.empreinte));
        // Une ligne encore à l'ancien chemin part à sa nouvelle place (rangement du 23/10).
        var serveur = SyncDocuments.Redresser(lignes.Select(l => (l.chemin, l.empreinte, false)), out _)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Empreinte);
        var locaux = await Task.Run(() => Locaux(racine));

        try
        {
            Directory.CreateDirectory(dossier);
            foreach (var b in bats)
            {
                var locs = b.locataireDuBatiment ?? new List<Locataire>();
                b.locataireDuBatiment = new List<Locataire>();   // chaque locataire a sa fiche, dans son dossier
                string bat = Path.Combine(dossier, "Batiment", DossiersDonnees.NomDossier(b.Name));
                Ecrire(Path.Combine(bat, DossiersDonnees.INFOS, DossiersDonnees.NomDossier(b.Name) + ".json"), JsonUtility.ToJson(b, true));
                foreach (var loc in locs.Where(x => x != null))
                    Ecrire(Path.Combine(bat, DossiersDonnees.NomDossier(loc.Name), DossiersDonnees.NomDossier(loc.Name) + ".json"), JsonUtility.ToJson(loc, true));
                b.locataireDuBatiment = locs;
            }
            File.WriteAllText(Path.Combine(dossier, "reglage.json"), JsonUtility.ToJson(ReglageService.Current, true));
        }
        catch (Exception ex) { return new Resultat { Erreur = ex.Message }; }

        var res = new Resultat { Dossier = dossier };
        var docs = RegleExport.Documents(serveur.Keys, locaux);
        int faits = 0;
        for (int i = 0; i < docs.Count; i++)
        {
            progression?.Invoke(i, docs.Count);
            var (chemin, depuisCloud) = docs[i];
            string dest = Absolu(dossier, chemin);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (!depuisCloud)
                {
                    string src = Absolu(racine, chemin);
                    await Task.Run(() => File.Copy(src, dest, true));
                }
                else
                {
                    var (contenu, _) = await CloudClient.LireFichier(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(),
                                                                     "documents", e.id + "/" + serveur[chemin]);
                    if (contenu == null) { res.Manquants.Add(chemin); continue; }
                    await Task.Run(() => File.WriteAllBytes(dest, contenu));
                }
                faits++;
            }
            catch (Exception) { res.Manquants.Add(chemin); }
        }
        progression?.Invoke(docs.Count, docs.Count);
        res.Bilan = RegleExport.Bilan(bats.Count, bats.Sum(b => b.locataireDuBatiment?.Count(l => l != null) ?? 0), faits);
        Debug.Log($"[Export] « {e.nom} » → {dossier} : {res.Bilan}, {res.Manquants.Count} introuvable(s)");
        return res;
    }

    static string Absolu(string racine, string chemin) => Path.Combine(racine, chemin.Replace('/', Path.DirectorySeparatorChar));

    static void Ecrire(string chemin, string contenu)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(chemin));
        File.WriteAllText(chemin, contenu);
    }

    /// Les documents partagés de ce PC, en chemins relatifs « Batiment/… » ; un fichier encore rangé
    /// à l'ancienne est pris dans le cloud, à sa nouvelle place.
    static HashSet<string> Locaux(string racine)
    {
        var res = new HashSet<string>();
        foreach (var sous in SyncDocuments.Racines)
        {
            string d = Path.Combine(racine, sous);
            if (!Directory.Exists(d)) continue;
            foreach (var abs in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
            {
                string c = abs.Substring(racine.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (SyncDocuments.Partage(c) && DossiersDonnees.Nouveau(c) == c) res.Add(c);
            }
        }
        return res;
    }
}
