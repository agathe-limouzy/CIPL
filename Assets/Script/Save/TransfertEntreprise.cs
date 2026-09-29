using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// Transfert d'un bâtiment, ou d'un seul locataire, vers une AUTRE entreprise.
///
/// Ne travaille que sur le disque, avec des racines explicites : l'entreprise de
/// destination n'est pas chargée dans l'app, et la racine active n'est jamais lue.
///
/// Identité = le NOM (insensible à la casse), celui qui nomme déjà les dossiers.
/// Un homonyme dans la destination est remplacé par la version d'origine en gardant
/// son id de destination : les charges de ce bâtiment y font référence.
///
/// Ordre de sécurité : tout est écrit dans la destination d'abord, et c'est à
/// l'appelant de retirer l'élément de l'origine, seulement après un succès. Rien
/// n'est effacé sans copie : la version de destination remplacée part d'abord dans
/// sa corbeille.
public static class TransfertEntreprise
{
    public class Rapport
    {
        public bool Succes;
        public string Erreur = "";
        public bool BatimentExistant;   // fusion dans un bâtiment du même nom
        public int LocatairesMisAJour, LocatairesAjoutes;
    }

    class Entree { public string Fichier; public Batiment Bat; }

    const string Corbeille = "corbeille_batiments";

    // ── Aperçu (rien n'est écrit) ───────────────────────────────────────────────

    /// `seul` null = le bâtiment entier.
    public static Rapport Apercu(Batiment src, Locataire seul, string racineDest)
    {
        var cible = Charger(racineDest).Find(e => DossiersDonnees.MemeDossier(e.Bat.Name, src.Name))?.Bat;
        var r = new Rapport { Succes = true, BatimentExistant = cible != null };
        foreach (var l in seul != null ? new List<Locataire> { seul } : src.locataireDuBatiment)
        {
            if (cible != null && cible.locataireDuBatiment.Exists(d => DossiersDonnees.MemeDossier(d.Name, l.Name)))
                r.LocatairesMisAJour++;
            else
                r.LocatairesAjoutes++;
        }
        return r;
    }

    // ── Transfert ───────────────────────────────────────────────────────────────

    public static Rapport TransfererBatiment(Batiment src, string racineOrigine, string racineDest)
        => Executer(src, null, racineOrigine, racineDest);

    public static Rapport TransfererLocataire(Batiment batSrc, Locataire loc, string racineOrigine, string racineDest)
        => Executer(batSrc, loc, racineOrigine, racineDest);

    static Rapport Executer(Batiment src, Locataire seul, string racineOrigine, string racineDest)
    {
        var r = new Rapport();
        try
        {
            if (string.Equals(Plein(racineOrigine), Plein(racineDest), StringComparison.OrdinalIgnoreCase))
                throw new IOException("l'entreprise de destination est celle d'origine");
            if (!Directory.Exists(racineDest))
                throw new IOException("dossier de l'entreprise de destination introuvable : " + racineDest);

            var existants = Charger(racineDest);
            var entree = existants.Find(e => DossiersDonnees.MemeDossier(e.Bat.Name, src.Name));
            r.BatimentExistant = entree != null;

            // Copie de travail : l'origine n'est jamais modifiée ici.
            var copie = Cloner(src);
            Relativiser(copie, racineOrigine, src.Name);
            var transferes = seul == null
                ? copie.locataireDuBatiment
                : copie.locataireDuBatiment.Where(l => l.id == seul.id).ToList();
            if (seul != null && transferes.Count == 0)
                throw new InvalidOperationException("locataire introuvable dans son bâtiment");

            Batiment resultat;
            string fichier;

            if (entree == null)
            {
                // Bâtiment absent : il arrive complet (avec ce seul locataire si c'est
                // un transfert de locataire). Nouvel id seulement en cas de collision.
                resultat = copie;
                resultat.locataireDuBatiment = transferes;
                if (existants.Exists(e => e.Bat.id == resultat.id)) resultat.id = Guid.NewGuid().ToString();
                fichier = Path.Combine(racineDest, "batiments", $"batiment_{resultat.id}.json");
                r.LocatairesAjoutes = transferes.Count;
            }
            else
            {
                var dest = entree.Bat;
                SauvegarderAvantFusion(entree, racineDest);

                var remap = new Dictionary<string, string>();
                var locs = FusionnerLocataires(dest.locataireDuBatiment, transferes, remap, r);

                if (seul == null)
                {
                    // Bâtiment : les infos d'origine l'emportent, l'identité de
                    // destination est conservée.
                    resultat = copie;
                    resultat.id = dest.id;
                    resultat.charges = FusionnerCharges(dest.charges, copie.charges, remap);
                    resultat.photos = copie.photos.Concat(dest.photos ?? new List<string>())
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    if (string.IsNullOrEmpty(resultat.coverPhoto)) resultat.coverPhoto = dest.coverPhoto;
                }
                else
                {
                    // Locataire seul : le bâtiment de destination garde ses infos.
                    resultat = dest;
                }
                resultat.locataireDuBatiment = locs;
                fichier = entree.Fichier;
            }

            CopierFichiers(src, transferes.Select(l => l.Name).ToList(),
                           Dossier(racineOrigine, src.Name), Dossier(racineDest, resultat.Name),
                           avecBatiment: seul == null || entree == null);

            // Le JSON en dernier : c'est lui qui fait apparaître le bâtiment.
            Directory.CreateDirectory(Path.GetDirectoryName(fichier));
            if (!AtomicFile.WriteAllText(fichier, JsonUtility.ToJson(resultat, prettyPrint: true), out string err))
                throw new IOException(err);

            r.Succes = true;
        }
        catch (Exception e)
        {
            r.Succes = false;
            r.Erreur = e.Message;
            Debug.LogError("[Transfert] Échec — l'entreprise d'origine n'a pas été modifiée : " + e.Message);
        }
        return r;
    }

    /// Déplace un dossier dans la corbeille d'une entreprise (même convention que la
    /// suppression d'un bâtiment). Renvoie l'emplacement atteint, ou null si absent.
    public static string MettreEnCorbeille(string dossier, string racine)
    {
        if (string.IsNullOrEmpty(dossier) || !Directory.Exists(dossier)) return null;
        string dest = Path.Combine(racine, Corbeille, Guid.NewGuid().ToString("N") + "_" + new DirectoryInfo(dossier).Name);
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        Directory.Move(dossier, dest);
        return dest;
    }

    /// Dossier d'un bâtiment sous une racine donnée (DossiersDonnees, lui, suit la
    /// racine active).
    public static string Dossier(string racine, string nomBatiment)
        => Path.Combine(racine, "Batiment", DossiersDonnees.NomDossier(nomBatiment));

    // ── Fusion ──────────────────────────────────────────────────────────────────

    static List<Locataire> FusionnerLocataires(List<Locataire> dest, List<Locataire> transferes,
                                               Dictionary<string, string> remap, Rapport r)
    {
        var locs = new List<Locataire>(dest);
        foreach (var l in transferes)
        {
            int i = locs.FindIndex(d => DossiersDonnees.MemeDossier(d.Name, l.Name));
            if (i >= 0)
            {
                remap[l.id] = locs[i].id;
                l.id = locs[i].id;
                locs[i] = l;
                r.LocatairesMisAJour++;
            }
            else
            {
                if (locs.Exists(d => d.id == l.id)) { string n = Guid.NewGuid().ToString(); remap[l.id] = n; l.id = n; }
                locs.Add(l);
                r.LocatairesAjoutes++;
            }
        }
        return locs;
    }

    static List<ChargeBatiment> FusionnerCharges(List<ChargeBatiment> dest, List<ChargeBatiment> src,
                                                 Dictionary<string, string> remap)
    {
        var res = new List<ChargeBatiment>(dest ?? new List<ChargeBatiment>());
        foreach (var c in src ?? new List<ChargeBatiment>())
        {
            c.locatairesConcernes = c.locatairesConcernes?.Select(id => remap.TryGetValue(id, out var n) ? n : id).ToList();
            if (c.ratios != null)
                foreach (var ratio in c.ratios)
                    if (ratio.locataireId != null && remap.TryGetValue(ratio.locataireId, out var n)) ratio.locataireId = n;

            int i = res.FindIndex(x => x.id == c.id);
            if (i >= 0) res[i] = c; else res.Add(c);
        }
        return res;
    }

    // ── Fichiers ────────────────────────────────────────────────────────────────

    /// La version de destination (dossier + JSON) est copiée dans sa corbeille AVANT
    /// toute modification : la fusion remplace des locataires entiers.
    static void SauvegarderAvantFusion(Entree entree, string racineDest)
    {
        string sauvegarde = Path.Combine(racineDest, Corbeille,
            Guid.NewGuid().ToString("N") + "_avant-transfert_" + DossiersDonnees.NomDossier(entree.Bat.Name));
        CopierDossier(Dossier(racineDest, entree.Bat.Name), sauvegarde, null);
        Directory.CreateDirectory(sauvegarde);
        File.Copy(entree.Fichier, Path.Combine(sauvegarde, Path.GetFileName(entree.Fichier)));
    }

    /// Chaque locataire transféré remplace EN ENTIER son dossier de destination.
    /// `avecBatiment` : on copie aussi le reste du dossier du bâtiment (photos,
    /// charges), sans les dossiers des locataires non transférés.
    static void CopierFichiers(Batiment src, List<string> transferes, string srcBat, string dstBat, bool avecBatiment)
    {
        foreach (var nom in transferes)
        {
            string d = Path.Combine(dstBat, DossiersDonnees.NomDossier(nom));
            if (Directory.Exists(d)) Directory.Delete(d, true);   // sauvegardé juste avant
        }
        if (!Directory.Exists(srcBat)) return;

        if (avecBatiment)
        {
            var autres = src.locataireDuBatiment.Select(l => l.Name)
                .Where(n => !transferes.Any(t => DossiersDonnees.MemeDossier(t, n)))
                .Select(DossiersDonnees.NomDossier);
            CopierDossier(srcBat, dstBat, new HashSet<string>(autres, StringComparer.OrdinalIgnoreCase));
        }
        else
        {
            foreach (var nom in transferes)
                CopierDossier(Path.Combine(srcBat, DossiersDonnees.NomDossier(nom)),
                              Path.Combine(dstBat, DossiersDonnees.NomDossier(nom)), null);
        }
    }

    /// Copie récursive en écrasant. `exclus` : sous-dossiers de premier niveau ignorés.
    static void CopierDossier(string src, string dst, HashSet<string> exclus)
    {
        if (!Directory.Exists(src)) return;
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src))
        {
            string nom = Path.GetFileName(d);
            if (exclus != null && exclus.Contains(nom)) continue;
            CopierDossier(d, Path.Combine(dst, nom), null);
        }
    }

    // ── Données ─────────────────────────────────────────────────────────────────

    static List<Entree> Charger(string racine)
    {
        var res = new List<Entree>();
        string dir = Path.Combine(racine, "batiments");
        if (!Directory.Exists(dir)) return res;
        foreach (var f in Directory.GetFiles(dir, "*.json"))
        {
            // Un fichier illisible fait échouer le transfert : fusionner à l'aveugle
            // risquerait de créer un doublon du bâtiment qu'il contient.
            var b = JsonUtility.FromJson<Batiment>(File.ReadAllText(f));
            if (b == null) throw new IOException("bâtiment illisible dans la destination : " + Path.GetFileName(f));
            b.locataireDuBatiment ??= new List<Locataire>();
            res.Add(new Entree { Fichier = f, Bat = b });
        }
        return res;
    }

    static Batiment Cloner(Batiment b)
    {
        var c = JsonUtility.FromJson<Batiment>(JsonUtility.ToJson(b));
        c.locataireDuBatiment ??= new List<Locataire>();
        c.photos ??= new List<string>();
        return c;
    }

    /// Les chemins stockés sont relatifs à la racine (`Batiment/<nom>/…`) et restent
    /// donc valides : les noms sont conservés. Seuls les anciens chemins ABSOLUS sous
    /// la racine d'origine pointeraient encore vers elle — on les rend relatifs.
    static void Relativiser(Batiment b, string racineOrigine, string nomBatiment)
    {
        for (int i = 0; i < b.photos.Count; i++) b.photos[i] = Relatif(b.photos[i], racineOrigine);
        b.coverPhoto = Relatif(b.coverPhoto, racineOrigine);
        if (b.charges != null) foreach (var c in b.charges) c.pdfPath = Relatif(c.pdfPath, racineOrigine);

        foreach (var l in b.locataireDuBatiment)
        {
            if (l.facturesEtat != null) foreach (var f in l.facturesEtat) f.pdfPath = Relatif(f.pdfPath, racineOrigine);

            // Bail : seul un NOM de fichier est relu dans le dossier Bail du locataire.
            string bail = Path.Combine(Dossier(racineOrigine, nomBatiment), DossiersDonnees.NomDossier(l.Name), "Bail");
            l.cheminBail = NomSiDans(l.cheminBail, bail);
            if (l.avenants != null) l.avenants = l.avenants.Select(a => NomSiDans(a, bail)).ToList();
        }
    }

    static string Relatif(string chemin, string racine)
    {
        if (string.IsNullOrEmpty(chemin) || !Path.IsPathRooted(chemin)) return chemin;
        string r = Plein(racine) + Path.DirectorySeparatorChar, c = Path.GetFullPath(chemin);
        return c.StartsWith(r, StringComparison.OrdinalIgnoreCase) ? c.Substring(r.Length).Replace('\\', '/') : chemin;
    }

    static string NomSiDans(string chemin, string dossier)
    {
        if (string.IsNullOrEmpty(chemin) || !Path.IsPathRooted(chemin)) return chemin;
        return string.Equals(Path.GetDirectoryName(Path.GetFullPath(chemin)), Plein(dossier), StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(chemin) : chemin;
    }

    static string Plein(string dossier)
        => Path.GetFullPath(dossier).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
