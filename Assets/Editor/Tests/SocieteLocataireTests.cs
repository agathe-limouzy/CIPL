using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// Étape 16b : le bloc société d'un locataire (Pappers par le serveur, procédures du BODACC), la ligne
/// « État » et les objectifs créés tout seuls.
public class SocieteLocataireTests
{
    // Une vraie annonce du BODACC (28/10/2026), réduite aux champs demandés.
    const string Bodacc = "{\"total_count\":1,\"results\":[{\"id\":\"A202602071401\",\"registre\":[\"843965716\",\"843 965 716\"],"
        + "\"dateparution\":\"2026-10-28\",\"jugement\":\"{\\\"type\\\": \\\"initial\\\", \\\"famille\\\": \\\"Jugement d'ouverture\\\", "
        + "\\\"nature\\\": \\\"Jugement d'ouverture d'une procédure de sauvegarde\\\", \\\"date\\\": \\\"2026-10-12\\\"}\"}]}";

    static SocieteLocataire.FicheSociete Fiche(string resultat = "-12000", bool fermee = false) => new SocieteLocataire.FicheSociete
    {
        siren = "843965716", nom = "SCI DU MOULIN", forme = "Société civile immobilière", capital = "10000", creee_le = "2018-03-12",
        fermee = fermee, fermee_le = fermee ? "2026-10-03" : "",
        dirigeants = new[] { new SocieteLocataire.Dirigeant { nom = "Marie DUPONT", qualite = "Gérante" } },
        finances = new[]
        {
            new SocieteLocataire.Exercice { annee = 2024, ca = "1310000", resultat = "85000" },
            new SocieteLocataire.Exercice { annee = 2025, ca = "1250000", resultat = resultat },
        },
    };

    static SocieteLocataire.Procedure Proc(string id, string parution, string famille, string nature) =>
        new SocieteLocataire.Procedure { id = id, siren = "843965716", parution = parution, jugeLe = "2026-10-12", famille = famille, nature = nature };

    [Test]
    public void Le_BODACC_se_lit_et_se_demande_en_un_appel()
    {
        var p = SocieteLocataire.LireBodacc(Bodacc).Single();
        Assert.That(new[] { p.id, p.siren, p.parution, p.jugeLe, p.famille },
                    Is.EqualTo(new[] { "A202602071401", "843965716", "2026-10-28", "2026-10-12", "Jugement d'ouverture" }));
        Assert.That(SocieteLocataire.Simple(p), Is.EqualTo("procédure de sauvegarde"));
        Assert.That(SocieteLocataire.LireBodacc("{\"total_count\":0,\"results\":[]}"), Is.Empty, "aucune procédure");
        Assert.That(SocieteLocataire.LireBodacc("<html>panne</html>"), Is.Null, "illisible : la tournée n'écrit rien");

        string url = Uri.UnescapeDataString(SocieteLocataire.UrlBodacc(new[] { "843965716", "552032534" }));
        StringAssert.Contains("registre in (\"843965716\",\"552032534\") and familleavis=\"collective\"", url);
        StringAssert.Contains("order_by=dateparution desc", url);
        Assert.That(SocieteLocataire.Siren("843 965 716 00012"), Is.EqualTo("843965716"));
    }

    [Test]
    public void L_etat_de_la_societe_suit_des_regles_simples()
    {
        var ouverture = Proc("A1", "2026-10-28", "Jugement d'ouverture", "Jugement d'ouverture de redressement judiciaire");
        var cloture = Proc("A2", "2027-03-01", "Jugement de clôture", "Jugement de clôture du plan");
        Assert.That(SocieteLocataire.Etat(Fiche(fermee: true), new[] { ouverture }).etat, Is.EqualTo("FERMÉE"), "fermée d'abord");
        Assert.That(SocieteLocataire.Etat(Fiche(), new[] { ouverture }),
                    Is.EqualTo(("EN DIFFICULTÉ", "redressement judiciaire (jugement du 12/10/2026)")));
        Assert.That(SocieteLocataire.Etat(Fiche(), new[] { ouverture, cloture }).etat, Is.EqualTo("À SURVEILLER"), "procédure close");
        Assert.That(SocieteLocataire.Etat(Fiche(), null), Is.EqualTo(("À SURVEILLER", "résultat négatif en 2025 (-12 000 €)")));
        Assert.That(SocieteLocataire.Etat(Fiche("4000"), null).etat, Is.EqualTo("SAINE"));
        var sansComptes = Fiche();
        sansComptes.finances = null;
        Assert.That(SocieteLocataire.Etat(sansComptes, null), Is.EqualTo(("RIEN À SIGNALER", "comptes non publiés")));
    }

    [Test]
    public void Le_bloc_du_commentaire_suit_la_maquette()
    {
        var p = SocieteLocataire.LireBodacc(Bodacc);
        string bloc = SocieteLocataire.Construire(Fiche(), p, "2026-10-28T08:00:00.000Z");
        StringAssert.StartsWith("<b>SCI DU MOULIN</b>\nSociété civile immobilière · capital 10 000 €\nSIREN : 843 965 716 · créée le 12/03/2018",
                                bloc.Replace("\r", ""));
        StringAssert.Contains("État : <b>EN DIFFICULTÉ</b>", bloc);
        StringAssert.Contains("Marie DUPONT · Gérante", bloc);
        StringAssert.Contains("2025 : CA 1 250 000 € · résultat -12 000 €", bloc);
        StringAssert.Contains("Procédure de sauvegarde : jugement du 12/10/2026 (BODACC du 28/10/2026)", bloc);
        StringAssert.EndsWith("Fiche Pappers du 28/10/2026", bloc);
        StringAssert.Contains("Aucune", SocieteLocataire.Construire(Fiche(), new List<SocieteLocataire.Procedure>(), "2026-10-28"));
    }

    [Test]
    public void Une_procedure_ouverte_cree_l_objectif_de_declarer_la_creance_une_fois()
    {
        var procs = SocieteLocataire.LireBodacc(Bodacc);
        var deja = new List<string>();
        var (cle, o) = SocieteLocataire.Signaler(deja, Fiche(), procs, new DateTime(2026, 10, 30)).Single();
        Assert.That(cle, Is.EqualTo("bodacc:A202602071401"));
        Assert.That(o.text, Is.EqualTo("Déclarer la créance au mandataire : procédure de sauvegarde (jugement du 12/10/2026)"));
        Assert.That(o.importance, Is.EqualTo(Objective.Importance.Obligatoire));
        Assert.That(o.echeanceISO, Is.EqualTo("2026-12-28"), "2 mois après la publication au BODACC");

        deja.Add(cle);
        Assert.That(SocieteLocataire.Signaler(deja, Fiche(), procs, new DateTime(2026, 10, 31)), Is.Empty, "déjà signalé (même supprimé)");
        Assert.That(SocieteLocataire.Signaler(new List<string>(), Fiche(), procs, new DateTime(2027, 1, 5)), Is.Empty, "délai dépassé");
        var cloture = new[] { Proc("A9", "2026-10-28", "Jugement de clôture", "Clôture pour insuffisance d'actif") };
        Assert.That(SocieteLocataire.Signaler(new List<string>(), Fiche(), cloture, new DateTime(2026, 10, 30)), Is.Empty);

        var fermee = SocieteLocataire.Signaler(new List<string>(), Fiche(fermee: true), null, new DateTime(2026, 10, 30)).Single();
        Assert.That(fermee.cle, Is.EqualTo("fermee:843965716"));
        Assert.That(fermee.objectif.text, Is.EqualTo("Vérifier le bail : la société locataire est fermée (depuis le 03/10/2026)"));
        Assert.That(fermee.objectif.echeanceISO, Is.Null.Or.Empty);
    }

    [Test]
    public void Les_fonctions_serveur_parlent_comme_CIPL()
    {
        string dossier = Path.Combine(Application.dataPath, "..", "Serveur");
        string pappers = File.ReadAllText(Path.Combine(dossier, "fonctions", "pappers.ts"));
        // La fiche que pappers.ts renvoie a les champs que CIPL lit (JsonUtility ignore un nom différent).
        foreach (var champ in typeof(SocieteLocataire.FicheSociete).GetFields().Select(f => f.Name)
                     .Concat(typeof(SocieteLocataire.Exercice).GetFields().Select(f => f.Name))
                     .Concat(typeof(SocieteLocataire.Dirigeant).GetFields().Select(f => f.Name)))
            Assert.That(Regex.IsMatch(pappers, $@"\b{champ}\s*[:,]"), Is.True, champ);
        foreach (var refus in new[] { "acces", "abonnement", "config", "cle", "jetons" })
            StringAssert.Contains($"\"{refus}\"", pappers, "refus qui arrête la tournée");

        string cartes = File.ReadAllText(Path.Combine(dossier, "fonctions", "cartes.ts"));
        foreach (var champ in typeof(TileLoader.DemandeCarte).GetFields().Select(f => f.Name))
            StringAssert.Contains("d." + champ, cartes, champ);
        StringAssert.Contains("maptype: \"hybrid\"", cartes);
        StringAssert.DoesNotContain(", 502)", cartes, "502 à 504 feraient passer CIPL hors ligne");

        StringAssert.Contains("pappers_fiches", File.ReadAllText(Path.Combine(dossier, "etape16b_supabase.sql")));
    }
}
