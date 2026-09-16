using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Migration des dossiers nommés par identifiant (GUID) vers un nommage par NOM.
///
/// Ancien format :  Batiment/{idBatiment}/{idLocataire}/Facture/   +  Photos/{idBatiment}/
/// Nouveau format : Batiment/{nomBatiment}/{nomLocataire}/Facture/ +  Batiment/{nomBatiment}/Photos/
///
/// Déplacer des dossiers n'est pas une opération atomique : on ne peut pas « annuler »
/// à mi-parcours. La sécurité vient donc de trois précautions, dans cet ordre :
///   1. VALIDER entièrement le plan avant de bouger quoi que ce soit (collisions de
///      noms, destinations déjà occupées) — si un seul point bloque, rien n'est touché ;
///   2. SAUVEGARDER l'arborescence existante avant exécution ;
///   3. n'écrire le marqueur de fin QUE si tout a réussi, pour que la migration soit
///      retentée au prochain démarrage en cas d'échec partiel.
public static class MigrationDossiers
{
    private const string MARQUEUR = ".migration_dossiers_v1";

    private static string Racine => SaveLocationService.GetSaveRoot();
    private static string CheminMarqueur => Path.Combine(Racine, MARQUEUR);

    public static bool DejaFaite => File.Exists(CheminMarqueur);

    private struct Deplacement
    {
        public string Source;
        public string Destination;
        public string Libelle;
    }

    /// Exécute la migration si nécessaire. Renvoie false en cas d'échec ou de refus,
    /// avec le motif dans `rapport`. Ne fait rien (et renvoie true) si déjà migré.
    public static bool Migrer(IReadOnlyList<Batiment> batiments, out string rapport)
    {
        rapport = "";
        if (DejaFaite) { rapport = "Migration déjà effectuée."; return true; }
        if (batiments == null || batiments.Count == 0)
        {
            MarquerFait();   // rien à migrer : on évite de re-scanner à chaque démarrage
            rapport = "Aucun bâtiment : rien à migrer.";
            return true;
        }

        // ── 1. Construction ET validation du plan ─────────────────────────────
        var plan = new List<Deplacement>();
        var erreurs = new List<string>();
        var dossiersBatiment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var b in batiments)
        {
            string nomDossier = DossiersDonnees.NomDossier(b.Name);

            // Deux bâtiments aboutissant au même dossier fusionneraient leurs factures
            // et leurs photos : on refuse la migration entière plutôt que de mélanger.
            if (dossiersBatiment.TryGetValue(nomDossier, out string dejaVu))
            {
                erreurs.Add($"« {b.Name} » et « {dejaVu} » donnent le même dossier ({nomDossier}) — renommez-en un.");
                continue;
            }
            dossiersBatiment[nomDossier] = b.Name;

            string ancienBat = Path.Combine(Racine, "Batiment", b.id);
            string nouveauBat = DossiersDonnees.DossierBatiment(b.Name);

            if (Directory.Exists(ancienBat))
            {
                if (Directory.Exists(nouveauBat))
                    erreurs.Add($"La destination existe déjà : {nouveauBat}");
                else
                    plan.Add(new Deplacement { Source = ancienBat, Destination = nouveauBat, Libelle = $"bâtiment « {b.Name} »" });
            }

            // Locataires : sous-dossiers nommés par id à l'intérieur du bâtiment.
            var vusLoc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (b.locataireDuBatiment != null)
                foreach (var l in b.locataireDuBatiment)
                {
                    if (l == null) continue;
                    string nomLoc = DossiersDonnees.NomDossier(l.Name);
                    if (vusLoc.TryGetValue(nomLoc, out string dejaLoc))
                    {
                        erreurs.Add($"Dans « {b.Name} », « {l.Name} » et « {dejaLoc} » donnent le même dossier ({nomLoc}) — renommez-en un.");
                        continue;
                    }
                    vusLoc[nomLoc] = l.Name;

                    // Le dossier du bâtiment aura déjà été déplacé : on raisonne sur la
                    // destination finale, pas sur l'ancien emplacement.
                    string ancienLoc = Path.Combine(nouveauBat, l.id);
                    string nouveauLoc = Path.Combine(nouveauBat, nomLoc);
                    plan.Add(new Deplacement { Source = ancienLoc, Destination = nouveauLoc, Libelle = $"locataire « {l.Name} »" });
                }

            // Photos : Photos/{id} → Batiment/{nom}/Photos
            string anciennesPhotos = Path.Combine(Racine, "Photos", b.id);
            if (Directory.Exists(anciennesPhotos))
            {
                string nouvellesPhotos = DossiersDonnees.DossierPhotos(b.Name);
                if (Directory.Exists(nouvellesPhotos))
                    erreurs.Add($"La destination photos existe déjà : {nouvellesPhotos}");
                else
                    plan.Add(new Deplacement { Source = anciennesPhotos, Destination = nouvellesPhotos, Libelle = $"photos de « {b.Name} »" });
            }
        }

        if (erreurs.Count > 0)
        {
            rapport = "Migration annulée, rien n'a été modifié :\n• " + string.Join("\n• ", erreurs);
            Debug.LogError("[MigrationDossiers] " + rapport);
            return false;
        }

        if (plan.Count == 0)
        {
            MigrerCheminsPhotos(batiments);
            MarquerFait();
            rapport = "Aucun dossier à déplacer ; chemins de photos mis à jour.";
            return true;
        }

        // ── 2. Sauvegarde avant exécution ─────────────────────────────────────
        if (!Sauvegarder(out string erreurBackup))
        {
            rapport = "Migration annulée : sauvegarde préalable impossible — " + erreurBackup;
            Debug.LogError("[MigrationDossiers] " + rapport);
            return false;
        }

        // ── 3. Exécution ──────────────────────────────────────────────────────
        var faits = new List<string>();
        foreach (var d in plan)
        {
            try
            {
                if (!Directory.Exists(d.Source)) continue;        // rien à déplacer
                Directory.CreateDirectory(Path.GetDirectoryName(d.Destination));
                Directory.Move(d.Source, d.Destination);
                faits.Add(d.Libelle);
            }
            catch (Exception e)
            {
                rapport = $"Migration interrompue sur {d.Libelle} : {e.Message}\n" +
                          $"{faits.Count} déplacement(s) déjà effectué(s). Une copie d'origine est dans Backups/. " +
                          "La migration sera retentée au prochain démarrage.";
                Debug.LogError("[MigrationDossiers] " + rapport);
                return false;   // marqueur NON écrit → nouvelle tentative au prochain lancement
            }
        }

        MigrerCheminsPhotos(batiments);
        MarquerFait();

        rapport = $"Migration réussie : {faits.Count} dossier(s) renommé(s) par nom.";
        Debug.Log("[MigrationDossiers] " + rapport);
        return true;
    }

    /// Réécrit les chemins de photos stockés (absolus, ancien emplacement) en chemins
    /// relatifs pointant vers le nouveau dossier du bâtiment.
    private static void MigrerCheminsPhotos(IReadOnlyList<Batiment> batiments)
    {
        foreach (var b in batiments)
        {
            if (b.photos != null)
                for (int i = 0; i < b.photos.Count; i++)
                    b.photos[i] = CheminPhotoMigre(b, b.photos[i]);

            if (!string.IsNullOrEmpty(b.coverPhoto))
                b.coverPhoto = CheminPhotoMigre(b, b.coverPhoto);
        }
    }

    private static string CheminPhotoMigre(Batiment b, string stocke)
    {
        if (string.IsNullOrEmpty(stocke)) return stocke;
        // Seul le nom de fichier est conservé : le dossier, lui, est recalculé depuis
        // le nom du bâtiment, donc le lien survit à un déplacement de la sauvegarde.
        string fichier = Path.GetFileName(stocke);
        if (string.IsNullOrEmpty(fichier)) return stocke;
        return DossiersDonnees.VersRelatif(
            Path.Combine(DossiersDonnees.DossierPhotos(b.Name), fichier));
    }

    private static bool Sauvegarder(out string erreur)
    {
        erreur = null;
        try
        {
            string dest = Path.Combine(Racine, "Backups",
                "avant_migration_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(dest);

            foreach (string nom in new[] { "Batiment", "Photos" })
            {
                string src = Path.Combine(Racine, nom);
                if (Directory.Exists(src)) CopierRecursif(src, Path.Combine(dest, nom));
            }
            Debug.Log($"[MigrationDossiers] Sauvegarde avant migration : {dest}");
            return true;
        }
        catch (Exception e) { erreur = e.Message; return false; }
    }

    private static void CopierRecursif(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string f in Directory.GetFiles(source))
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), overwrite: true);
        foreach (string d in Directory.GetDirectories(source))
            CopierRecursif(d, Path.Combine(dest, Path.GetFileName(d)));
    }

    private static void MarquerFait()
    {
        try
        {
            Directory.CreateDirectory(Racine);
            File.WriteAllText(CheminMarqueur,
                "Migration des dossiers vers un nommage par nom, effectuée le " +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                ". Supprimer ce fichier forcerait une nouvelle tentative.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MigrationDossiers] Marqueur non écrit ({e.Message}) — " +
                             "la migration sera re-tentée au prochain démarrage (sans effet si déjà faite).");
        }
    }
}
