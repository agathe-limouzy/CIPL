using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// Les entreprises de ce PC (anciens dossiers CIPL_Saves), comme SOURCE d'import vers le
/// cloud. Leurs fichiers ne sont que lus. Seul fichier écrit : le repère CIPL_entreprise.json,
/// qui dit à quelle entreprise du cloud ce dossier correspond (évite un 2e import, et
/// servira à retrouver le dossier sur un Drive à l'étape 6).
public static class ImportLocal
{
    public const string NomRepere = "CIPL_entreprise.json";

    [Serializable]
    public class Repere
    {
        public string id, nom, projet;   // projet : adresse du projet cloud (test ou réel)
        public bool complet;             // faux tant que l'import n'est pas allé au bout
    }

    public class Source
    {
        public string Nom, Racine;
        public int NbBatiments, NbLocataires;
        public Repere Repere;
    }

    /// Entreprises connues de ce PC (registre de l'app), avec ce qu'elles contiennent.
    public static List<Source> EntreprisesDuPC()
    {
        var res = new List<Source>();
        foreach (var e in EntrepriseService.All())
        {
            if (e == null || string.IsNullOrEmpty(e.racine) || !Directory.Exists(e.racine)) continue;
            var bats = LireBatiments(e.racine, out _);
            res.Add(new Source
            {
                Nom = e.nom, Racine = e.racine, NbBatiments = bats.Count,
                NbLocataires = bats.Sum(b => b.locataireDuBatiment?.Count(l => l != null) ?? 0),
                Repere = LireRepere(e.racine),
            });
        }
        return res;
    }

    /// Lecture seule des batiment_*.json. Un fichier illisible est signalé, pas bloquant.
    public static List<Batiment> LireBatiments(string racine, out List<string> illisibles)
    {
        var res = new List<Batiment>();
        illisibles = new List<string>();
        string dossier = Path.Combine(racine, "batiments");
        if (!Directory.Exists(dossier)) return res;
        foreach (var f in Directory.GetFiles(dossier, "*.json"))
        {
            try
            {
                var b = JsonUtility.FromJson<Batiment>(File.ReadAllText(f));
                if (b != null && !string.IsNullOrEmpty(b.id)) res.Add(b); else illisibles.Add(Path.GetFileName(f));
            }
            catch (Exception) { illisibles.Add(Path.GetFileName(f)); }
        }
        return res;
    }

    public static Repere LireRepere(string racine)
    {
        try
        {
            string f = Path.Combine(racine, NomRepere);
            if (!File.Exists(f)) return null;
            var r = JsonUtility.FromJson<Repere>(File.ReadAllText(f));
            return r != null && !string.IsNullOrEmpty(r.id) ? r : null;
        }
        catch (Exception) { return null; }
    }

    public static bool EcrireRepere(string racine, Repere r, out string erreur) =>
        AtomicFile.WriteAllText(Path.Combine(racine, NomRepere), JsonUtility.ToJson(r, true), out erreur);

    public enum Etat { AImporter, AReprendre, DejaSurLeCloud, DejaImportee }

    /// Que proposer pour ce dossier, vu mes entreprises du cloud et le projet courant.
    ///  - pas de repère (ou repère d'un autre projet) : à importer ;
    ///  - repère d'une de mes entreprises, import inachevé : à reprendre (si je suis admin) ;
    ///  - repère d'une de mes entreprises, import achevé : rien à faire ;
    ///  - repère d'une entreprise dont je ne suis pas membre : déjà sur le cloud, refus.
    public static Etat Statut(Repere r, string projet, IList<EntrepriseCloud> miennes)
    {
        if (r == null || !string.Equals(r.projet, projet, StringComparison.OrdinalIgnoreCase)) return Etat.AImporter;
        var e = miennes?.FirstOrDefault(x => x.id == r.id);
        if (e == null) return Etat.DejaSurLeCloud;
        if (r.complet) return Etat.DejaImportee;
        return e.EstAdmin ? Etat.AReprendre : Etat.DejaSurLeCloud;
    }
}
