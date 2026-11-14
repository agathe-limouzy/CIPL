using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot B (09/11/2026), groupe « sql » : le script Serveur/etape17c_supabase.sql et ses deux retombées côté CIPL
/// (ReglagesCloud, DocumentsCloud). Tests texte sur le modèle de Revue17Tests : chaque correction prouvée dans le script,
/// et chaque fonction recréée comparée à sa dernière définition précédente (signature et corps : rien de perdu, comme
/// maj_session l'avait été à l'étape 16c4).
/// Lot D (11/11/2026) : les fonctions des sections 8 à 12 entrent dans la même comparaison ; leurs tests propres sont dans
/// LotDSqlTests.
public class LotBSqlTests
{
    static string Serveur(string f) => File.ReadAllText(Path.Combine("Serveur", f));
    static string Sql => Serveur("etape17c_supabase.sql");
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));
    static string Fonction(string sql, string nom)
    {
        int i = sql.IndexOf("create or replace function public." + nom + "(", StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), nom + " absente du SQL");
        return sql.Substring(i, sql.IndexOf("end $$;", i, StringComparison.Ordinal) - i);
    }

    static readonly string[] Recreees =
    {
        "documenter(uuid, text, bigint, text, text)", "supprimer_document(uuid, text, text)",
        "enregistrer_reglages(uuid, int, jsonb, text, int)",
        "retirer_membre(uuid, uuid)", "changer_role(uuid, uuid, text)", "changer_acces(uuid, uuid, uuid[])",
        "creer_entreprise_import(text)",
        // Lot D (sections 8 à 10) : D2-16, D2-10, D1-06.
        "supprimer_fiche(uuid, text, text, text)",
        "enregistrer_batiment(uuid, text, int, jsonb, text, int)", "enregistrer_locataire(uuid, text, text, int, int, jsonb, text, int)",
        "transferer_fiches(uuid, uuid, jsonb, jsonb, boolean, jsonb, text, jsonb, int, int)",
    };
    /// Fonctions neuves : aucune définition antérieure. Lot B (D1-05, D1-10) : quotas Pappers et cartes, rôle de service.
    /// Lot D (D2-18) : enregistrer_fiches, pour les comptes connectés (ses droits : LotDSqlTests).
    static readonly string[] NouvellesService = { "prendre_lecture_pappers(uuid, int)", "prendre_lecture_cartes(uuid, int)" };
    static readonly string[] Nouvelles = NouvellesService.Concat(new[] { "enregistrer_fiches(uuid, jsonb, text, int)" }).ToArray();

    // ── D1-08 : detenteur_autre ──

    [Test]
    public void D1_08_detenteur_autre_n_est_plus_appelable_par_un_compte_connecte()
    {
        StringAssert.Contains("revoke all on function public.detenteur_autre(uuid, text, text, text) from public, anon, authenticated;", Sql);
    }

    // ── D1-09 : le logo réservé à l'admin, fichier compris ──

    [Test]
    public void D1_09_le_serveur_reserve_le_logo_a_l_admin()
    {
        foreach (var nom in new[] { "documenter", "supprimer_document" })
        {
            string corps = Fonction(Sql, nom);
            StringAssert.Contains("if p_chemin like 'Logo/%' and not public.est_admin(p_entreprise) then raise exception 'admin seulement'; end if;", corps, nom);
            StringAssert.Contains("if not public.chemin_document_valide(p_chemin) then raise exception 'chemin refusé'; end if;", corps, nom + " : D1-01 gardé");
        }
    }

    [Test]
    public void D1_09_un_poste_utilisateur_n_envoie_ni_ne_supprime_le_logo_au_serveur()
    {
        Assert.That(SyncDocuments.ReserveAdmin(SyncDocuments.Action.Envoyer, "Logo/logo.png"), Is.True);
        Assert.That(SyncDocuments.ReserveAdmin(SyncDocuments.Action.SupprimerServeur, "Logo/logo.png"), Is.True);
        Assert.That(SyncDocuments.ReserveAdmin(SyncDocuments.Action.Telecharger, "Logo/logo.png"), Is.False, "il le reçoit");
        Assert.That(SyncDocuments.ReserveAdmin(SyncDocuments.Action.Corbeille, "Logo/logo.png"), Is.False);
        Assert.That(SyncDocuments.ReserveAdmin(SyncDocuments.Action.Noter, "Logo/logo.png"), Is.False);
        Assert.That(SyncDocuments.ReserveAdmin(SyncDocuments.Action.Envoyer, "Batiment/rivoli/Charge/x.pdf"), Is.False, "les bâtiments partent de tout poste");
        StringAssert.Contains("if (SessionCloud.Entreprise?.EstAdmin != true) plan.RemoveAll(p => SyncDocuments.ReserveAdmin(p.action, p.chemin));",
            Src("Cloud/DocumentsCloud.cs"), "le passage de synchronisation filtre le plan d'un poste non admin");
    }

    // ── D1-10 : empreinte contrôlée ──

    [Test]
    public void D1_10_documenter_refuse_une_empreinte_qui_n_est_pas_64_hexadecimaux()
    {
        StringAssert.Contains("if coalesce(p_empreinte, '') !~ '^[0-9a-f]{64}$' then raise exception 'empreinte refusée'; end if;", Fonction(Sql, "documenter"));
        Assert.That(Regex.IsMatch(SyncDocuments.Empreinte(new byte[] { 1, 2, 3 }), "^[0-9a-f]{64}$"), "l'empreinte calculée par CIPL passe le filtre du serveur");
    }

    // ── D1-13 : la version avant les champs réservés à l'admin ──

    [Test]
    public void D1_13_enregistrer_reglages_controle_la_version_avant_les_champs_reserves()
    {
        string corps = Fonction(Sql, "enregistrer_reglages");
        int application = corps.IndexOf("'refus', 'application'", StringComparison.Ordinal);
        int version = corps.IndexOf("is distinct from p_version", StringComparison.Ordinal);
        int admin = corps.IndexOf("select data into avant", StringComparison.Ordinal);
        Assert.That(version, Is.GreaterThan(application), "après « application » (mettez CIPL à jour)");
        Assert.That(admin, Is.GreaterThan(version), "la comparaison des champs réservés vient après la version");
        StringAssert.Contains("return json_build_object('ok', false, 'refus', 'version', 'nom',", corps.Substring(version, admin - version), "même refus que l'update");
    }

    [Test]
    public void D1_13_le_refus_admin_est_traduit_et_les_reglages_relus()
    {
        string src = Src("Cloud/ReglagesCloud.cs");
        int i = src.IndexOf("res?.refus == \"admin\"", StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThan(0), "branche « admin »");
        string branche = src.Substring(i, src.IndexOf("Réglages non enregistrés sur le serveur : ", i, StringComparison.Ordinal) - i);
        StringAssert.Contains("réservés à l'administrateur", branche, "en clair, pas le mot brut");
        StringAssert.Contains("RelireMaintenant(null)", branche, "la copie de ce PC reprend celle du serveur");
    }

    // ── D3-15 : le dernier admin se compte par entreprise ──

    [Test]
    public void D3_15_le_dernier_admin_se_compte_par_entreprise()
    {
        string sql = Sql;
        foreach (var nom in new[] { "retirer_membre", "changer_role", "changer_acces" })
        {
            string corps = Fonction(sql, nom);
            int garde = corps.IndexOf("'refus', 'dernier_admin', 'nom', n)", StringComparison.Ordinal);
            Assert.That(garde, Is.GreaterThan(0), nom + " : refus avec le nom de l'entreprise");
            StringAssert.Contains("and exists (select 1 from public.acces a where a.entreprise_id = e.id and a.user_id = p_user)", corps,
                nom + " : seule une entreprise où il a accès (une SCI déjà orpheline ne bloque pas tout)");
            StringAssert.Contains("where a.entreprise_id = e.id and m.role = 'admin' and a.user_id <> p_user)", corps, nom + " : un autre admin y ayant accès");
            string ecriture = nom == "retirer_membre" ? "delete from public.verrous" : nom == "changer_role" ? "update public.membres set role" : "perform 1 from public.entreprises";
            Assert.That(garde, Is.LessThan(corps.IndexOf(ecriture, StringComparison.Ordinal)), nom + " : la garde précède l'écriture");
        }
        StringAssert.Contains("where e.groupe_id = g and not (e.id = any(p_entreprises))", Fonction(sql, "changer_acces"), "changer_acces : l'accès conservé ne compte pas");
        StringAssert.Contains("if p_role = 'utilisateur' and exists", Fonction(sql, "changer_role"), "changer_role : seulement vers utilisateur");
    }

    // ── D1-14 : changer_acces exige un membre du groupe ──

    [Test]
    public void D1_14_changer_acces_refuse_une_personne_qui_n_est_pas_membre_du_groupe()
    {
        string corps = Fonction(Sql, "changer_acces");
        int membre = corps.IndexOf("if not exists (select 1 from public.membres where groupe_id = g and user_id = p_user) then", StringComparison.Ordinal);
        Assert.That(membre, Is.GreaterThan(0));
        StringAssert.Contains("'refus', 'membre'", corps.Substring(membre, 200));
        Assert.That(membre, Is.LessThan(corps.IndexOf("insert into public.acces", StringComparison.Ordinal)), "avant toute écriture");
    }

    // ── D1-15 : le groupe de l'import n'est plus tiré au hasard ──

    [Test]
    public void D1_15_creer_entreprise_import_choisit_le_groupe_admin_le_plus_ancien()
    {
        string corps = Fonction(Sql, "creer_entreprise_import");
        StringAssert.Contains("order by (m.role = 'admin') desc, gr.cree_le limit 1;", corps);
        StringAssert.DoesNotContain("where m.user_id = auth.uid() limit 1;", corps, "plus de « limit 1 » sans ordre");
    }

    // ── Le script entier : signatures et corps repris de la dernière définition, droits, fin ──

    static (int numero, string suffixe) Cle(string fichier)   // etape16c4_supabase.sql → (16, "c4")
    {
        var m = Regex.Match(Path.GetFileName(fichier), @"^etape(\d+)([a-z]\d*)?_");
        return (int.Parse(m.Groups[1].Value), m.Groups[2].Value);
    }

    /// Le dernier script d'étape avant etape17c qui définit la fonction, et son texte.
    static (string fichier, string texte) DerniereDefinition(string nom)
    {
        var fichiers = Directory.GetFiles("Serveur", "etape*_supabase.sql")
            .Where(f => { var (n, s) = Cle(f); return n < 17 || (n == 17 && string.CompareOrdinal(s, "c") < 0); })
            .OrderBy(f => Cle(f).numero).ThenBy(f => Cle(f).suffixe, StringComparer.Ordinal)
            .Where(f => File.ReadAllText(f).Contains("create or replace function public." + nom + "("))
            .ToList();
        Assert.That(fichiers, Is.Not.Empty, nom + " : aucune définition précédente");
        string dernier = fichiers.Last();
        return (Path.GetFileName(dernier), File.ReadAllText(dernier));
    }

    static string Signature(string corps, string nom)
    {
        int i = corps.IndexOf("public." + nom + "(", StringComparison.Ordinal);
        return Regex.Replace(corps.Substring(i, corps.IndexOf(')', i) - i + 1), @"\s+", " ");
    }

    /// Les lignes de code d'une fonction : taillées, non vides, hors commentaires.
    static IEnumerable<string> Code(string corps) =>
        corps.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("--", StringComparison.Ordinal));

    [Test]
    public void Chaque_fonction_recreee_garde_la_signature_et_le_corps_de_sa_derniere_definition()
    {
        string sql = Sql;
        var noms = Regex.Matches(sql, @"create or replace function public\.(\w+)\(").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.That(noms, Is.EquivalentTo(Recreees.Concat(Nouvelles).Select(r => r.Substring(0, r.IndexOf('(')))), "les fonctions du script et celles des droits");
        // La seule ligne que la fiche remplace (D1-15 ; D2-10 au lot D) ; tout le reste est ajouté, rien n'est retiré.
        var remplacees = new Dictionary<string, string>
        {
            ["creer_entreprise_import"] = "select m.groupe_id, m.role into g, r from public.membres m where m.user_id = auth.uid() limit 1;",
            // Lot D (D2-10) : la fin du refus « version » : « )); » devient « ), », suivi de « version » et de « maj_session ».
            ["enregistrer_batiment"] = "(select maj_par from public.batiments where entreprise_id = p_entreprise and id = p_id));",
            ["enregistrer_locataire"] = "(select maj_par from public.locataires where entreprise_id = p_entreprise and id = p_id));",
        };
        // Revue 17 (lot D, relecture) : combien de lignes de code chaque fonction du lot D AJOUTE. Une ligne en trop (un
        // « delete », un « return » placé plus tôt) se voit, même quand toutes les anciennes lignes sont là. À recompter
        // si la fonction change (D2-16 : 11 ; D2-10 : 2 = une ligne devenue trois ; D1-06 : 20 + 6 pour « même identifiant »).
        var ajoutees = new Dictionary<string, int>
        {
            ["supprimer_fiche"] = 11, ["enregistrer_batiment"] = 2, ["enregistrer_locataire"] = 2, ["transferer_fiches"] = 26,
        };
        foreach (var nom in noms)
        {
            if (Nouvelles.Any(x => x.StartsWith(nom + "(", StringComparison.Ordinal))) continue;   // neuve : rien à reprendre
            var (fichier, avant) = DerniereDefinition(nom);
            string ancien = Fonction(avant, nom), neuf = Fonction(sql, nom);
            Assert.That(Signature(neuf, nom), Is.EqualTo(Signature(ancien, nom)), nom + " : signature différente de " + fichier + " (l'application en service casserait)");
            foreach (var ligne in Code(ancien))
            {
                if (remplacees.TryGetValue(nom, out var r) && ligne == r) continue;
                StringAssert.Contains(ligne, neuf, nom + " : ligne de " + fichier + " perdue");
            }
            if (ajoutees.TryGetValue(nom, out int n))
                Assert.That(Code(neuf).Count() - Code(ancien).Count(), Is.EqualTo(n), nom + " : lignes de code ajoutées par le lot D");
        }
    }

    [Test]
    public void Le_script_repose_les_droits_et_recharge_le_schema()
    {
        string sql = Sql;
        foreach (var f in Recreees) StringAssert.Contains("'" + f + "'", sql, "droits : " + f);
        StringAssert.Contains("notify pgrst, 'reload schema';", sql);
        StringAssert.Contains("-- Lot D : fait le 11/11/2026", sql, "le lot D a rempli son repère (voir LotDSqlTests)");
        foreach (var f in NouvellesService) StringAssert.Contains("grant execute on function public." + f + " to service_role;", sql, "rôle de service seulement : " + f);
    }
}
