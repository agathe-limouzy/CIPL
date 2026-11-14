using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot D « données et cloud » (11/11/2026), groupe ÉCRITURE : l'écriture des fiches (EcritureCloud), le verrou
/// d'une fiche en saisie (RegleVerrou) et le canal temps réel (DirectCloud, TempsReel).
/// Un test par règle, sur une fonction pure ; texte du code seulement quand aucune règle pure n'est possible.
public class LotDEcritureTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));

    static Locataire Loc(string id) => new Locataire { id = id, Name = "Locataire " + id };

    static Batiment Bat(params Locataire[] locs) =>
        new Batiment { id = "LOTD-BAT", Name = "Atelier Nord", locataireDuBatiment = locs.ToList() };

    /// L'état connu après une lecture du serveur : bâtiment en version 3, locataires en version 7, aux ordres donnés.
    static Dictionary<string, EcritureCloud.Etat> Lu(Batiment b, params int[] ordres)
    {
        var cache = new Dictionary<string, EcritureCloud.Etat>();
        var locs = b.locataireDuBatiment;
        EcritureCloud.Memoriser(cache, b, 3, locs.ToDictionary(l => l.id, l => 7),
                                ordres.Length == 0 ? null : Enumerable.Range(0, locs.Count).ToDictionary(i => locs[i].id, i => ordres[i]));
        return cache;
    }

    // ── D2-05 (1) : l'ordre n'est plus renuméroté ──

    [Test]
    public void D2_05_supprimer_le_premier_de_trois_ne_renvoie_pas_les_suivants()
    {
        var a = Loc("A"); var b2 = Loc("B"); var c = Loc("C");
        var bat = Bat(a, b2, c);
        var cache = Lu(bat);   // ordres 0, 1, 2
        bat.locataireDuBatiment.Remove(a);

        var ops = EcritureCloud.Differences(bat, cache);
        Assert.That(ops.Count, Is.EqualTo(1), "B et C gardent leur ordre (1 et 2) : ils ne repartent pas");
        Assert.That(ops[0].Type, Is.EqualTo(EcritureCloud.TypeOp.SupprimerLocataire));
        Assert.That(ops[0].Id, Is.EqualTo("A"));
    }

    [Test]
    public void D2_05_un_ajout_en_fin_prend_le_plus_grand_ordre_plus_un()
    {
        var a = Loc("A"); var b2 = Loc("B");
        var bat = Bat(a, b2);
        var cache = Lu(bat, 0, 5);   // un trou au serveur
        bat.locataireDuBatiment.Add(Loc("N"));

        var ops = EcritureCloud.Differences(bat, cache);
        Assert.That(ops.Count, Is.EqualTo(1));
        Assert.That(ops[0].Type, Is.EqualTo(EcritureCloud.TypeOp.Locataire));
        Assert.That(ops[0].Id, Is.EqualTo("N"));
        Assert.That(ops[0].Ordre, Is.EqualTo(6));
        Assert.That(ops[0].Version, Is.EqualTo(0), "une création");
    }

    [Test]
    public void D2_05_un_ordre_relatif_change_ne_renvoie_que_le_deplace()
    {
        var a = Loc("A"); var b2 = Loc("B"); var c = Loc("C");
        var bat = Bat(a, b2, c);
        var cache = Lu(bat);   // ordres 0, 1, 2
        bat.locataireDuBatiment = new List<Locataire> { a, c, b2 };   // B passe en dernier

        var ops = EcritureCloud.Differences(bat, cache);
        Assert.That(ops.Count, Is.EqualTo(1), "A (0) et C (2) restent dans l'ordre : seul B repart");
        Assert.That(ops[0].Id, Is.EqualTo("B"));
        Assert.That(ops[0].Ordre, Is.EqualTo(3), "l'ordre du précédent + 1");
        Assert.That(ops[0].Version, Is.EqualTo(7), "la version connue : le serveur refusera si elle a changé");
    }

    [Test]
    public void D2_05_rien_de_change_rien_ne_part_meme_avec_des_trous_et_le_recalage_garde_l_ordre()
    {
        var a = Loc("A"); var b2 = Loc("B"); var c = Loc("C");
        var bat = Bat(a, b2, c);
        var cache = Lu(bat, 2, 7, 40);
        Assert.That(EcritureCloud.Differences(bat, cache), Is.Empty);

        b2.loyerAnnuel = 24000;   // mise à niveau du chargement, en mémoire
        EcritureCloud.Recaler(cache, new[] { bat });
        Assert.That(new[] { "A", "B", "C" }.Select(id => cache[EcritureCloud.Cle(false, id)].Ordre), Is.EqualTo(new[] { 2, 7, 40 }),
                    "Recaler garde l'ordre connu du serveur, comme les versions");
        Assert.That(EcritureCloud.Differences(bat, cache), Is.Empty);
    }

    [Test]
    public void D2_05_la_lecture_du_serveur_retient_l_ordre_reel()
    {
        var a = Loc("A"); var b2 = Loc("B");
        var bat = Bat(a, b2);
        DonneesCloud.Decouper(bat, out string jb, out var jl);
        string bats = "[{\"id\":\"LOTD-BAT\",\"version\":4,\"data\":" + jb + "}]";
        string locs = "[{\"id\":\"A\",\"batiment_id\":\"LOTD-BAT\",\"ordre\":4,\"version\":9,\"data\":" + jl[0] + "}," +
                      "{\"id\":\"B\",\"batiment_id\":\"LOTD-BAT\",\"ordre\":9,\"version\":2,\"data\":" + jl[1] + "}]";
        var cache = new Dictionary<string, EcritureCloud.Etat>();

        var lus = EcritureCloud.Lire(bats, locs, cache);
        Assert.That(cache[EcritureCloud.Cle(false, "A")].Ordre, Is.EqualTo(4));
        Assert.That(cache[EcritureCloud.Cle(false, "B")].Ordre, Is.EqualTo(9));
        Assert.That(EcritureCloud.Differences(lus[0], cache), Is.Empty, "relu tel quel : rien à renvoyer");
    }

    /// Contrat C1 : une lecture en plusieurs pages peut rendre deux fois la même ligne : une seule fiche est gardée.
    [Test]
    public void C1_une_ligne_lue_deux_fois_ne_donne_qu_une_fiche()
    {
        var bat = Bat(Loc("A"));
        DonneesCloud.Decouper(bat, out string jb, out var jl);
        string b = "{\"id\":\"LOTD-BAT\",\"version\":4,\"data\":" + jb + "}";
        string l = "{\"id\":\"A\",\"batiment_id\":\"LOTD-BAT\",\"ordre\":4,\"version\":9,\"data\":" + jl[0] + "}";

        var lus = EcritureCloud.Lire("[" + b + "," + b + "]", "[" + l + "," + l + "]", new Dictionary<string, EcritureCloud.Etat>());
        Assert.That(lus.Count, Is.EqualTo(1), "un seul bâtiment");
        Assert.That(lus[0].locataireDuBatiment.Count, Is.EqualTo(1), "un seul locataire");
    }

    /// Deux locataires au même ordre (données d'avant) : le second est départagé au premier enregistrement, une fois.
    [Test]
    public void D2_05_deux_ordres_egaux_sont_departages()
    {
        var a = Loc("A"); var b2 = Loc("B");
        var bat = Bat(a, b2);
        var ops = EcritureCloud.Differences(bat, Lu(bat, 0, 0));
        Assert.That(ops.Count, Is.EqualTo(1));
        Assert.That(ops[0].Id, Is.EqualTo("B"));
        Assert.That(ops[0].Ordre, Is.EqualTo(1));
    }

    // ── D2-18 : un enregistrement = un appel (enregistrer_fiches) ──

    static EcritureCloud.Op OpBat => new EcritureCloud.Op { Type = EcritureCloud.TypeOp.Batiment, Id = "b1", Version = 3, Json = "{\"Name\":\"Rivoli\"}" };
    static EcritureCloud.Op OpLoc => new EcritureCloud.Op { Type = EcritureCloud.TypeOp.Locataire, Id = "l1", BatimentId = "b1", Ordre = 2, Version = 5, Json = "{\"Name\":\"Volteo\"}" };
    static EcritureCloud.Op OpSuppr => new EcritureCloud.Op { Type = EcritureCloud.TypeOp.SupprimerLocataire, Id = "l2", BatimentId = "b1" };

    [Test]
    public void D2_18_toutes_les_fiches_d_un_enregistrement_partent_en_un_seul_corps()
    {
        string corps = EcritureCloud.CorpsEnregistrerTous("e1", new List<EcritureCloud.Op> { OpBat, OpLoc, OpSuppr }, "s\"1");
        Assert.That(corps, Is.EqualTo(
            "{\"p_entreprise\":\"e1\",\"p_ops\":[" +
            "{\"type\":\"batiment\",\"id\":\"b1\",\"version\":3,\"data\":{\"Name\":\"Rivoli\"}}," +
            "{\"type\":\"locataire\",\"id\":\"l1\",\"batiment\":\"b1\",\"ordre\":2,\"version\":5,\"data\":{\"Name\":\"Volteo\"}}," +
            "{\"type\":\"supprimer_locataire\",\"id\":\"l2\"}" +
            "],\"p_session\":\"s\\\"1\",\"p_modele\":" + DonneesCloud.Modele + "}"),
            "le bâtiment d'abord ; ni version ni data pour une suppression ; chaînes échappées par CloudClient.Q");
    }

    [Test]
    public void D2_18_l_accord_du_serveur_avance_toutes_les_versions_d_un_coup()
    {
        var cache = new Dictionary<string, EcritureCloud.Etat>
        {
            { "b:b1", new EcritureCloud.Etat { Json = "{}", Version = 3 } },
            { "l:l1", new EcritureCloud.Etat { Json = "{}", Version = 5, BatimentId = "b1", Ordre = 2 } },
            { "l:l2", new EcritureCloud.Etat { Json = "{}", Version = 8, BatimentId = "b1", Ordre = 3 } },
        };
        var ops = new List<EcritureCloud.Op> { OpBat, OpLoc, OpSuppr };
        var res = EcritureCloud.LireResultat("{\"ok\":true,\"versions\":[{\"type\":\"batiment\",\"id\":\"b1\",\"version\":4}," +
                                             "{\"type\":\"locataire\",\"id\":\"l1\",\"version\":6},{\"type\":\"supprimer_locataire\",\"id\":\"l2\",\"version\":0}]}");

        Assert.That(EcritureCloud.Avancer(cache, ops, res), Is.True);
        Assert.That(cache["b:b1"].Version, Is.EqualTo(4));
        Assert.That(cache["b:b1"].Json, Is.EqualTo("{\"Name\":\"Rivoli\"}"));
        Assert.That(cache["l:l1"].Version, Is.EqualTo(6));
        Assert.That(cache["l:l1"].Ordre, Is.EqualTo(2));
        Assert.That(cache["l:l1"].BatimentId, Is.EqualTo("b1"));
        Assert.That(cache.ContainsKey("l:l2"), Is.False, "la suppression sort de l'état connu");
    }

    [Test]
    public void D2_18_un_refus_nomme_sa_fiche_et_n_avance_aucune_version()
    {
        var cache = new Dictionary<string, EcritureCloud.Etat>
        {
            { "b:b1", new EcritureCloud.Etat { Json = "{}", Version = 3 } },
            { "l:l1", new EcritureCloud.Etat { Json = "{}", Version = 5, BatimentId = "b1", Ordre = 2 } },
        };
        var ops = new List<EcritureCloud.Op> { OpBat, OpLoc };
        var res = EcritureCloud.LireResultat("{\"ok\":false,\"refus\":\"version\",\"type\":\"locataire\",\"id\":\"l1\",\"nom\":\"Marie Dupont\"," +
                                             "\"version\":9,\"maj_session\":\"s2\"}");

        Assert.That(res.refus, Is.EqualTo("version"));
        Assert.That(res.type, Is.EqualTo("locataire"));
        Assert.That(res.id, Is.EqualTo("l1"));
        Assert.That(res.version, Is.EqualTo(9));
        Assert.That(res.maj_session, Is.EqualTo("s2"));
        Assert.That(EcritureCloud.OpRefusee(ops, res).Id, Is.EqualTo("l1"));
        Assert.That(EcritureCloud.OpRefusee(ops, res).Type, Is.EqualTo(EcritureCloud.TypeOp.Locataire));
        Assert.That(EcritureCloud.Avancer(cache, ops, res), Is.False);
        Assert.That(cache["b:b1"].Version, Is.EqualTo(3), "rien n'est écrit au serveur : le bâtiment n'avance pas non plus");
        Assert.That(cache["l:l1"].Version, Is.EqualTo(5));
        StringAssert.Contains("modifié entre-temps par Marie Dupont", EcritureCloud.MotifRefus(res, "Volteo"));
    }

    [Test]
    public void D2_18_un_accord_sans_une_version_par_fiche_n_avance_rien()
    {
        var cache = new Dictionary<string, EcritureCloud.Etat> { { "b:b1", new EcritureCloud.Etat { Json = "{}", Version = 3 } } };
        var ops = new List<EcritureCloud.Op> { OpBat, OpLoc };

        Assert.That(EcritureCloud.Avancer(cache, ops, EcritureCloud.LireResultat("{\"ok\":true,\"versions\":[{\"type\":\"batiment\",\"id\":\"b1\",\"version\":4}]}")),
                    Is.False, "une version manque");
        Assert.That(EcritureCloud.Avancer(cache, ops, EcritureCloud.LireResultat(
                        "{\"ok\":true,\"versions\":[{\"type\":\"batiment\",\"id\":\"b1\",\"version\":4},{\"type\":\"locataire\",\"id\":\"autre\",\"version\":6}]}")),
                    Is.False, "la deuxième version n'est pas celle de la deuxième fiche");
        Assert.That(EcritureCloud.Avancer(cache, ops, EcritureCloud.LireResultat("{\"ok\":true}")), Is.False);
        Assert.That(EcritureCloud.Avancer(cache, ops, null), Is.False);
        Assert.That(cache["b:b1"].Version, Is.EqualTo(3));
        Assert.That(cache.ContainsKey("l:l1"), Is.False);
        Assert.That(EcritureCloud.LireResultat("<html>erreur</html>"), Is.Null, "réponse illisible");
        Assert.That(EcritureCloud.OpRefusee(ops, EcritureCloud.LireResultat("{\"ok\":false,\"refus\":\"verrou\"}")), Is.Null, "le serveur ne nomme pas la fiche");
    }

    /// Décision du 11/11 : sans la fonction serveur (HTTP 404), CIPL se replie sur l'ancienne façon, sans passer hors ligne.
    [Test]
    public void D2_18_sans_enregistrer_fiches_au_serveur_CIPL_se_replie_sans_passer_hors_ligne()
    {
        Assert.That(HorsLigne.Panne(false, 404), Is.False, "un 404 est une réponse : la connexion est là");
        string src = Src("Cloud/EcritureCloud.cs");
        StringAssert.Contains("if (r.Code == 404)", src);
        StringAssert.Contains("_sansEnregistrerFiches = true;", src);
        StringAssert.Contains("[Cloud] enregistrer_fiches absente : passez Serveur/etape17c_supabase.sql", src);
        StringAssert.Contains("if (_sansEnregistrerFiches) return await TransmettreUneParUne(e, ops);", src, "le journal ne le dit qu'une fois");
    }

    // ── D2-05 (2) : une fiche refusée à une écriture de fond ne repart pas avec la saisie suivante ──

    [Test]
    public void D2_05_une_fiche_refusee_en_fond_ne_repart_pas_telle_quelle()
    {
        var ops = new List<EcritureCloud.Op> { OpBat, OpLoc, OpSuppr };
        var refuses = new Dictionary<string, string> { { "l:l1", "{\"Name\":\"Volteo\"}" }, { "l:l2", null } };

        Assert.That(EcritureCloud.SansRefuses(ops, refuses).Select(o => o.Id), Is.EqualTo(new[] { "b1", "l2" }),
                    "le locataire refusé reste ; une suppression n'est jamais retenue");

        var modifie = OpLoc; modifie.Json = "{\"Name\":\"Volteo SAS\"}";   // l'utilisatrice a modifié la fiche depuis
        Assert.That(EcritureCloud.SansRefuses(new List<EcritureCloud.Op> { modifie }, refuses).Count, Is.EqualTo(1),
                    "JSON différent : la fiche repart, son refus s'affichera");
        Assert.That(EcritureCloud.SansRefuses(ops, null), Is.SameAs(ops));
        Assert.That(EcritureCloud.SansRefuses(ops, new Dictionary<string, string> { { "b:l1", "{\"Name\":\"Volteo\"}" } }).Count, Is.EqualTo(3),
                    "la clé distingue bâtiment et locataire");
        var recree = OpLoc; recree.Version = 0;   // « Annuler » d'une suppression : même JSON, version 0
        Assert.That(EcritureCloud.SansRefuses(new List<EcritureCloud.Op> { recree }, refuses).Count, Is.EqualTo(1), "une création part toujours");
    }

    /// Reprise : la liste des cas où un refus de fond n'est PAS retenu (ni relu) porte l'invariant « aucune saisie perdue sans message ».
    [Test]
    public void D2_05_un_refus_de_fond_n_est_retenu_que_sur_une_fiche_sans_saisie()
    {
        Assert.That(EcritureCloud.RetenirRefusDeFond(OpLoc, saisie: false), Is.True);
        Assert.That(EcritureCloud.RetenirRefusDeFond(OpLoc, saisie: true), Is.False, "une saisie peut être sur cette fiche : son refus s'affichera");
        Assert.That(EcritureCloud.RetenirRefusDeFond(OpSuppr, false), Is.False, "une suppression est un geste");
        var cree = OpLoc; cree.Version = 0;
        Assert.That(EcritureCloud.RetenirRefusDeFond(cree, false), Is.False, "une création est un geste");
        Assert.That(EcritureCloud.RetenirRefusDeFond(null, false), Is.False, "fiche non nommée");

        string src = Src("Cloud/EcritureCloud.cs");
        StringAssert.Contains("bool saisie = _saisiesEnFile > 0 || FactureEmission.EnvoisEnCours > 0 || _enAttente.Contains(b) || _saisieEchouee.Contains(id)", src);
        int garde = src.IndexOf("if (!RetenirRefusDeFond(refusee, saisie))", StringComparison.Ordinal);
        int relire = src.IndexOf("DirectCloud.ARelire(id);\n                    if (refuses == null)", StringComparison.Ordinal);
        Assert.That(garde, Is.GreaterThan(0).And.LessThan(relire), "pas de relecture avant la garde : elle remplacerait la saisie");
        StringAssert.Contains("else { _saisieEchouee.Add(b.id); ConfirmDialog.Erreur($\"Enregistrement de « {b.Name} » impossible", src);
        StringAssert.Contains("refuses.Remove(Cle(op.Type == TypeOp.Batiment, op.Id));", src, "une fiche écrite n'est plus retenue");
        StringAssert.Contains("_refusDeFond.Remove(id); _saisieEchouee.Remove(id);", src, "suppression d'un bâtiment : ses refus de fond partent");
    }

    // ── D2-10 : réponse perdue après une écriture réussie ──

    [Test]
    public void D2_10_un_refus_version_sur_ma_propre_ecriture_se_reessaie()
    {
        var mien = new EcritureCloud.Resultat { refus = "version", version = 9, maj_session = "moi" };
        Assert.That(EcritureCloud.Reessayer(mien, "moi"), Is.True);
        Assert.That(EcritureCloud.Reessayer(new EcritureCloud.Resultat { refus = "version", version = 9, maj_session = "s2" }, "moi"), Is.False, "un collègue");
        Assert.That(EcritureCloud.Reessayer(new EcritureCloud.Resultat { refus = "version", version = 9 }, "moi"), Is.False, "serveur d'avant etape17c");
        Assert.That(EcritureCloud.Reessayer(new EcritureCloud.Resultat { refus = "verrou", version = 9, maj_session = "moi" }, "moi"), Is.False);
        Assert.That(EcritureCloud.Reessayer(new EcritureCloud.Resultat { refus = "version", maj_session = "moi" }, "moi"), Is.False, "sans version rendue");
        Assert.That(EcritureCloud.Reessayer(null, "moi"), Is.False);
        StringAssert.Contains("Reessayer(refus, SessionId) && repris.Add(cle)", Src("Cloud/EcritureCloud.cs"), "une reprise par fiche, jamais en boucle");
        StringAssert.Contains("ConfirmDialog.Erreur($\"Enregistrement de « {b.Name} » incomplet : enregistrez à nouveau.\")", Src("Cloud/EcritureCloud.cs"),
                              "les 30 tours passés, une saisie le dit");
    }

    // ── D2-17 : le verrou tient tant que l'enregistrement est en vol ──

    [Test]
    public void D2_17_le_verrou_de_modification_tient_tant_que_l_enregistrement_est_en_vol()
    {
        Assert.That(RegleVerrou.ResteTenu(enEdition: false, enregistrementEnCours: true), Is.True);
        Assert.That(RegleVerrou.ResteTenu(enEdition: true, enregistrementEnCours: false), Is.True);
        Assert.That(RegleVerrou.ResteTenu(enEdition: false, enregistrementEnCours: false), Is.False);
        string src = Src("Cloud/EcritureCloud.cs");
        StringAssert.Contains("RegleVerrou.ResteTenu(fiche.EnEdition, EnregistrementEnCours)", src, "ModifierSiLibre s'en sert");
        // Reprise : la fermeture ne cite pas comme « non enregistrée » une fiche qu'on vient de sauvegarder (elle attend la file).
        StringAssert.Contains("t.Motif != MotifEnvoi && t.Motif != MotifModification", src, "FichesTenues sans la réservation de « Modifier »");
        Assert.That(FermetureGuard.Decision(0, enregistrementEnCours: true, enAttente: 0), Is.EqualTo(FermetureGuard.Choix.Attendre));
    }

    // ── D2-16 : supprimer un bâtiment dont un locataire est tenu ──

    [Test]
    public void D2_16_un_batiment_dont_un_locataire_est_tenu_ne_se_supprime_pas()
    {
        var bat = Bat(Loc("sephora"), Loc("fnac"));
        var cache = Lu(bat);
        cache["l:ailleurs"] = new EcritureCloud.Etat { BatimentId = "autre-batiment" };

        Assert.That(EcritureCloud.TenuParAutre("LOTD-BAT", cache, new Dictionary<string, string> { { "l:sephora", "Paul" } }), Is.EqualTo("Paul"));
        Assert.That(EcritureCloud.TenuParAutre("LOTD-BAT", cache, new Dictionary<string, string> { { "b:LOTD-BAT", "Marie" } }), Is.EqualTo("Marie"));
        Assert.That(EcritureCloud.TenuParAutre("LOTD-BAT", cache, new Dictionary<string, string> { { "l:ailleurs", "Paul" }, { "b:autre-batiment", "Marie" } }),
                    Is.Null, "les verrous d'un autre bâtiment ne comptent pas");
        Assert.That(EcritureCloud.TenuParAutre("LOTD-BAT", cache, new Dictionary<string, string>()), Is.Null);

        string src = Src("Cloud/EcritureCloud.cs");
        int garde = src.IndexOf("string qui = TenuParAutre(id, _cache, _detenteurs);", StringComparison.Ordinal);
        int file = src.IndexOf("_file = Supprimer(", StringComparison.Ordinal);
        Assert.That(garde, Is.GreaterThan(0).And.LessThan(file), "SupprimerBatiment refuse avant la mise en file");
    }

    // ── D2-25 : plus d'écriture de plusieurs bâtiments sans rien tenir ──

    [Test]
    public void D2_25_SiAucunBatimentPris_n_existe_plus()
    {
        StringAssert.DoesNotContain("void SiAucunBatimentPris", Src("Cloud/EcritureCloud.cs"), "passer par Reserver");
    }

    // ── D2-20 et D2-23 : état connu vidé, suppressions notées ──

    [Test]
    public void D2_20_le_changement_d_entreprise_vide_l_etat_connu()
    {
        string id = "LOTD-" + Guid.NewGuid().ToString("N");
        try
        {
            EcritureCloud.ReprendreCopie(new Dictionary<string, EcritureCloud.Etat> { { EcritureCloud.Cle(true, id), new EcritureCloud.Etat { Json = "{}", Version = 5 } } });
            Assert.That(EcritureCloud.Version(true, id), Is.EqualTo(5));
            EcritureCloud.Oublier();
            Assert.That(EcritureCloud.Version(true, id), Is.EqualTo(0));
        }
        finally { EcritureCloud.Oublier(); }
    }

    [Test]
    public void D2_23_une_suppression_notee_par_moi_ne_passe_pas_pour_celle_d_un_collegue()
    {
        string id = "LOTD-" + Guid.NewGuid().ToString("N");
        Assert.That(EcritureCloud.SupprimeParMoi(id), Is.False);
        EcritureCloud.NoterSuppression(new[] { id, null });
        EcritureCloud.NoterSuppression(null);
        Assert.That(EcritureCloud.SupprimeParMoi(id), Is.True);

        var supprime = TempsReel.Lire("{\"topic\":\"realtime:cipl\",\"event\":\"postgres_changes\",\"payload\":{\"data\":{\"table\":\"batiments\",\"type\":\"DELETE\"," +
                                      "\"record\":{},\"old_record\":{\"id\":\"" + id + "\",\"entreprise_id\":\"e1\"}}}}").payload.data;
        Assert.That(DirectCloud.Concerne(supprime, "e1", "moi", _ => null, EcritureCloud.SupprimeParMoi), Is.Null);

        // Reprise : le transfert échoue ou est refusé : la note est retirée, la suppression d'un collègue se voit de nouveau.
        EcritureCloud.RetirerSuppression(new[] { id, null });
        EcritureCloud.RetirerSuppression(null);
        Assert.That(EcritureCloud.SupprimeParMoi(id), Is.False);
    }

    // ── D2-22 : canal temps réel à relancer ──

    [Test]
    public void D2_22_un_abonnement_refuse_ou_un_canal_ferme_sont_a_relancer()
    {
        var refus = TempsReel.Lire("{\"event\":\"phx_reply\",\"payload\":{\"response\":{\"reason\":\"Invalid JWT\"},\"status\":\"error\"},\"ref\":\"1\",\"topic\":\"realtime:cipl\"}");
        var accord = TempsReel.Lire("{\"event\":\"phx_reply\",\"payload\":{\"response\":{},\"status\":\"ok\"},\"ref\":\"1\",\"topic\":\"realtime:cipl\"}");
        Assert.That(DirectCloud.AbonnementRefuse(refus), Is.True);
        Assert.That(DirectCloud.AbonnementRefuse(accord), Is.False);
        Assert.That(DirectCloud.AbonnementRefuse(null), Is.False);

        Assert.That(DirectCloud.ARelancer("connecté", abonnementRefuse: true), Is.True, "le canal a l'air vivant, mais il ne recevra rien");
        Assert.That(DirectCloud.ARelancer("connecté", false), Is.False);
        Assert.That(DirectCloud.ARelancer("connexion…", false), Is.False, "bornée à 10 s par TempsReel.Demarrer");
        Assert.That(DirectCloud.ARelancer("abonnement demandé", false), Is.False);
        foreach (string etat in new[] { "coupé : connexion impossible", "coupé : erreur", "fermé", "fermé par le serveur", "arrêté", null })
            Assert.That(DirectCloud.ARelancer(etat, false), Is.True, etat ?? "(aucun canal)");
    }

    /// Le délai de connexion et l'état de fin de boucle vivent dans le fil de la socket : pas de règle pure, le texte du code.
    [Test]
    public void D2_22_la_connexion_du_canal_a_un_delai_et_la_fin_de_boucle_dit_ferme()
    {
        string src = Src("Cloud/TempsReel.cs");
        StringAssert.Contains("Task.WhenAny(connexion, Task.Delay(DelaiConnexion)) != connexion", src);
        StringAssert.Contains("Etat = \"coupé : connexion impossible\";", src);
        StringAssert.Contains("if (ReferenceEquals(ws, _ws)) Etat = \"fermé\";", src);
        string direct = Src("Cloud/DirectCloud.cs");
        StringAssert.Contains("if (_canal == null || ARelancer(etat, _abonnementRefuse))", direct, "Entretenir relance sur la règle");
        StringAssert.Contains("_abonnementRefuse = true;", direct, "Traiter retient le refus");
    }
}
