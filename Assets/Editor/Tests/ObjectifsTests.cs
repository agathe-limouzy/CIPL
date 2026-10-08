using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// Listes d'objectifs (06/10) : origine en texte, suppression annulable.
public class ObjectifsTests
{
    /// « Bât. rivoli · Loc. Dupont » : la police n'a pas les emojis 🏢 / 👤 (carrés à l'écran).
    [Test]
    public void L_origine_s_ecrit_en_texte()
    {
        Assert.That(Objectifs.Origine("rivoli", "Dupont"), Is.EqualTo("Bât. rivoli · Loc. Dupont"));
        Assert.That(Objectifs.Origine("rivoli", null), Is.EqualTo("Bât. rivoli"));
        Assert.That(Objectifs.Origine("rivoli", ""), Is.EqualTo("Bât. rivoli"));
    }

    /// Toast « Objectif supprimé · Annuler » : l'objectif revient à sa place, une seule fois.
    [Test]
    public void Annuler_une_suppression_remet_l_objectif_a_sa_place()
    {
        var a = new Objective("A"); var b = new Objective("B"); var c = new Objective("C");
        var liste = new List<Objective> { a, b, c };

        var annuler = Objectifs.Supprimer(liste, b);
        Assert.That(liste, Is.EqualTo(new[] { a, c }));

        annuler();
        Assert.That(liste, Is.EqualTo(new[] { a, b, c }));

        annuler();   // double clic : pas de doublon
        Assert.That(liste, Is.EqualTo(new[] { a, b, c }));

        // La liste a raccourci entre-temps : l'objectif revient en fin, sans erreur.
        var annulerC = Objectifs.Supprimer(liste, c);
        liste.Remove(a); liste.Remove(b);
        annulerC();
        Assert.That(liste, Is.EqualTo(new[] { c }));

        // Objectif absent : rien ne bouge.
        Assert.DoesNotThrow(() => Objectifs.Supprimer(liste, a)());
        Assert.That(liste, Is.EqualTo(new[] { c }));
    }

    static readonly DateTime Auj = new DateTime(2026, 10, 6);

    static Objective Ancien(Objective.ObjectiveStatus s)
    {
        var o = new Objective("x");
        o.version = 0; o.status = s; o.prevenirJours = 0; o.documents = null;   // tel que relu d'un ancien fichier
        return o;
    }

    /// Décision du 06/10 : Obligatoire et Rappel gardent leur importance (« À faire »),
    /// les autres gardent leur avancement ; prévenir 15 j, pas d'échéance. Une seule fois.
    [Test]
    public void Les_anciens_objectifs_sont_repris()
    {
        var obl = Ancien(Objective.ObjectiveStatus.Obligatoire);
        Assert.That(Objectifs.Migrer(obl), Is.True);
        Assert.That((obl.importance, obl.avancement), Is.EqualTo((Objective.Importance.Obligatoire, Objective.Avancement.AFaire)));
        Assert.That(obl.prevenirJours, Is.EqualTo(15));
        Assert.That(obl.documents, Is.Not.Null.And.Empty);
        Assert.That(obl.echeanceISO, Is.Null.Or.Empty);

        var rap = Ancien(Objective.ObjectiveStatus.Rappel); Objectifs.Migrer(rap);
        Assert.That((rap.importance, rap.avancement), Is.EqualTo((Objective.Importance.Rappel, Objective.Avancement.AFaire)));
        var enc = Ancien(Objective.ObjectiveStatus.EnCours); Objectifs.Migrer(enc);
        Assert.That((enc.importance, enc.avancement), Is.EqualTo((Objective.Importance.Normale, Objective.Avancement.EnCours)));
        var fait = Ancien(Objective.ObjectiveStatus.Fait); Objectifs.Migrer(fait);
        Assert.That(fait.Fait, Is.True);

        // Déjà repris : on ne touche plus à rien (l'importance choisie ensuite reste).
        obl.importance = Objective.Importance.Normale;
        Assert.That(Objectifs.Migrer(obl), Is.False);
        Assert.That(obl.importance, Is.EqualTo(Objective.Importance.Normale));

        // Un objectif repris survit à l'aller-retour JSON (copie des fiches, fichier).
        var relu = JsonUtility.FromJson<ObjectiveList>(JsonUtility.ToJson(new ObjectiveList { items = { obl } }));
        Assert.That(Objectifs.Migrer(relu), Is.False);
        Assert.That(relu.items[0].importance, Is.EqualTo(Objective.Importance.Normale));
    }

    /// Échéance au 31/10, prévenir 15 j : lointaine le 06/10, « Dans 15 j » le 16/10,
    /// « Aujourd'hui » le 31/10, « En retard de 1 j » le 01/11. Faite : plus surveillée.
    [Test]
    public void L_echeance_approche_puis_passe_en_retard()
    {
        var o = new Objective("Relancer le syndic") { echeanceISO = "2026-10-31" };
        Assert.That(Objectifs.Etat(o, Auj), Is.EqualTo(Objectifs.EtatEcheance.Lointaine));
        Assert.That(Objectifs.TexteEcheance(o, Auj), Is.EqualTo("Échéance 31/10/2026"));
        Assert.That(Objectifs.Etat(o, new DateTime(2026, 10, 15)), Is.EqualTo(Objectifs.EtatEcheance.Lointaine));
        Assert.That(Objectifs.Etat(o, new DateTime(2026, 10, 16)), Is.EqualTo(Objectifs.EtatEcheance.Proche));
        Assert.That(Objectifs.TexteEcheance(o, new DateTime(2026, 10, 16)), Is.EqualTo("Dans 15 j"));
        Assert.That(Objectifs.TexteEcheance(o, new DateTime(2026, 10, 31)), Is.EqualTo("Aujourd'hui"));
        Assert.That(Objectifs.Etat(o, new DateTime(2026, 11, 1)), Is.EqualTo(Objectifs.EtatEcheance.EnRetard));
        Assert.That(Objectifs.TexteEcheance(o, new DateTime(2026, 11, 5)), Is.EqualTo("En retard de 5 j"));

        o.prevenirJours = 7;   // réglable par objectif
        Assert.That(Objectifs.Etat(o, new DateTime(2026, 10, 23)), Is.EqualTo(Objectifs.EtatEcheance.Lointaine));
        Assert.That(Objectifs.Etat(o, new DateTime(2026, 10, 24)), Is.EqualTo(Objectifs.EtatEcheance.Proche));

        Objectifs.MarquerFait(new List<Objective> { o }, o, new DateTime(2026, 11, 5));
        Assert.That(Objectifs.Etat(o, new DateTime(2026, 11, 6)), Is.EqualTo(Objectifs.EtatEcheance.Aucune));
        Assert.That(Objectifs.TexteEcheance(o, Auj), Is.EqualTo("Fait le 05/11/2026"));

        Assert.That(Objectifs.Etat(new Objective("Repeindre le hall"), Auj), Is.EqualTo(Objectifs.EtatEcheance.Aucune));
        Assert.That(Objectifs.TexteEcheance(new Objective("Repeindre le hall"), Auj), Is.EqualTo("Sans échéance"));
    }

    /// « Entretien chaudière », chaque année, échéance 15/10/2026 : fait le 20/10 (en retard)
    /// ou le 01/09 (en avance) → nouvel objectif au 15/10/2027 (calendrier fixe), sans document ;
    /// l'objectif fait reste dans la liste avec sa date. Rouvrir retire l'occurrence inutilisée.
    [Test]
    public void Un_objectif_recurrent_fait_cree_le_suivant_au_calendrier_fixe()
    {
        foreach (var faitLe in new[] { new DateTime(2026, 10, 20), new DateTime(2026, 9, 1) })
        {
            var o = new Objective("Entretien chaudière", Objective.Importance.Obligatoire)
            {
                echeanceISO = "2026-10-15", prevenirJours = 30, repeterTous = 1,
                repeterUnite = Objective.UniteRecurrence.Ans, documents = { "attestation-2026.pdf" }
            };
            var autre = new Objective("Autre");
            var liste = new List<Objective> { o, autre };

            var s = Objectifs.MarquerFait(liste, o, faitLe);
            Assert.That(o.Fait, Is.True);
            Assert.That(o.faitLeISO, Is.EqualTo(Objectifs.Iso(faitLe)));
            Assert.That(o.documents, Is.EqualTo(new[] { "attestation-2026.pdf" }));
            Assert.That(s.echeanceISO, Is.EqualTo("2027-10-15"));
            Assert.That((s.text, s.importance, s.prevenirJours, s.repeterTous, s.repeterUnite),
                Is.EqualTo(("Entretien chaudière", Objective.Importance.Obligatoire, 30, 1, Objective.UniteRecurrence.Ans)));
            Assert.That(s.documents, Is.Empty);
            Assert.That(s.Fait, Is.False);
            Assert.That(liste, Is.EqualTo(new[] { o, s, autre }), "la suivante juste après");

            Assert.That(Objectifs.MarquerFait(liste, o, faitLe), Is.Null, "déjà fait : pas de doublon");

            Objectifs.Rouvrir(liste, o);
            Assert.That(o.avancement, Is.EqualTo(Objective.Avancement.AFaire));
            Assert.That(o.faitLeISO, Is.Null);
            Assert.That(liste, Is.EqualTo(new[] { o, autre }), "occurrence inutilisée retirée");
        }

        // Unités : 2 semaines, 1 mois (fin de mois), 10 jours.
        var d = new DateTime(2026, 1, 31);
        Assert.That(Objectifs.Suivante(d, 2, Objective.UniteRecurrence.Semaines), Is.EqualTo(new DateTime(2026, 2, 14)));
        Assert.That(Objectifs.Suivante(d, 1, Objective.UniteRecurrence.Mois), Is.EqualTo(new DateTime(2026, 2, 28)));
        Assert.That(Objectifs.Suivante(d, 10, Objective.UniteRecurrence.Jours), Is.EqualTo(new DateTime(2026, 2, 10)));
        Assert.That(Objectifs.TexteRecurrence(1, Objective.UniteRecurrence.Ans), Is.EqualTo("chaque an"));
        Assert.That(Objectifs.TexteRecurrence(2, Objective.UniteRecurrence.Ans), Is.EqualTo("tous les 2 ans"));
    }

    /// Une occurrence suivante déjà commencée n'est pas supprimée en rouvrant.
    [Test]
    public void Rouvrir_garde_une_occurrence_deja_commencee()
    {
        var o = new Objective("Contrôle") { echeanceISO = "2026-10-01", repeterTous = 6, repeterUnite = Objective.UniteRecurrence.Mois };
        var liste = new List<Objective> { o };
        var s = Objectifs.MarquerFait(liste, o, Auj);
        Assert.That(s.echeanceISO, Is.EqualTo("2027-04-01"));
        s.avancement = Objective.Avancement.EnCours;
        Objectifs.Rouvrir(liste, o);
        Assert.That(liste, Does.Contain(s));

        // Sans échéance : rien n'est créé.
        var simple = new Objective("Repeindre") { repeterTous = 1 };
        Assert.That(Objectifs.MarquerFait(new List<Objective> { simple }, simple, Auj), Is.Null);
        Assert.That(simple.Fait, Is.True);
    }

    /// Clic sur le badge : À faire → En cours → Fait → À faire ; l'importance ne bouge plus.
    [Test]
    public void Le_badge_fait_avancer_puis_rouvre()
    {
        var o = new Objective("x", Objective.Importance.Obligatoire);
        var l = new List<Objective> { o };
        Objectifs.Avancer(l, o, Auj); Assert.That(o.avancement, Is.EqualTo(Objective.Avancement.EnCours));
        Objectifs.Avancer(l, o, Auj); Assert.That(o.Fait, Is.True);
        Objectifs.Avancer(l, o, Auj); Assert.That(o.avancement, Is.EqualTo(Objective.Avancement.AFaire));
        Assert.That(o.importance, Is.EqualTo(Objective.Importance.Obligatoire));
    }

    /// Ordre : en retard, échéance proche, Obligatoire, Rappel, Normale ; à rang égal, la date.
    [Test]
    public void Les_objectifs_sont_tries_par_urgence_puis_par_date()
    {
        var normaleSansDate = new Objective("Repeindre le hall");
        var normaleLoin = new Objective("Chaudière") { echeanceISO = "2027-10-15" };
        var rappel = new Objective("Relancer", Objective.Importance.Rappel);
        var obligatoire = new Objective("Assurance", Objective.Importance.Obligatoire);
        var proche = new Objective("Syndic") { echeanceISO = "2026-10-10" };
        var retard = new Objective("Extincteurs") { echeanceISO = "2026-10-01" };
        var l = new List<Objective> { normaleSansDate, normaleLoin, rappel, obligatoire, proche, retard };
        l.Sort((a, b) => Objectifs.Comparer(a, b, Auj));
        Assert.That(l, Is.EqualTo(new[] { retard, proche, obligatoire, rappel, normaleLoin, normaleSansDate }));
        Assert.That(Objectifs.Detail(retard, Auj), Is.EqualTo("En retard de 5 j · échéance 01/10/2026"));
    }

    /// Un objectif fait n'est pas supprimé : il quitte les listes courantes et reste dans
    /// l'historique (filtre « Faits »), avec sa date.
    [Test]
    public void Un_objectif_fait_reste_dans_l_historique()
    {
        var o = new Objective("Contrôle", Objective.Importance.Obligatoire) { echeanceISO = "2026-10-01" };
        var l = new List<Objective> { o };
        Objectifs.MarquerFait(l, o, Auj);
        Assert.That(l, Does.Contain(o));
        Assert.That(Objectifs.Garde(o, Objectifs.FiltreAvancement.Faits, null, Auj), Is.True);
        foreach (var f in new[] { Objectifs.FiltreAvancement.Tous, Objectifs.FiltreAvancement.AFaire, Objectifs.FiltreAvancement.EnRetard })
            Assert.That(Objectifs.Garde(o, f, Objective.Importance.Obligatoire, Auj), Is.False, f.ToString());
        Assert.That(Objectifs.Detail(o, Auj), Is.EqualTo("Fait le 06/10/2026"));
    }

    /// « Entretien chaudière » fait en 2026 puis 2027 : l'occurrence 2028 retrouve 2027 puis 2026
    /// (la plus récente d'abord) ; la première n'en a aucune ; un autre objectif n'est pas mêlé.
    [Test]
    public void Les_occurrences_precedentes_se_retrouvent()
    {
        var o26 = new Objective("Entretien chaudière") { echeanceISO = "2026-10-15", repeterTous = 1, documents = { "a.pdf", "b.pdf" } };
        var autre = new Objective("Entretien chaudière");   // même texte, sans lien
        var l = new List<Objective> { o26, autre };
        var o27 = Objectifs.MarquerFait(l, o26, new DateTime(2026, 10, 20));
        var o28 = Objectifs.MarquerFait(l, o27, new DateTime(2027, 10, 2));

        Assert.That(Objectifs.Precedentes(l, o28), Is.EqualTo(new[] { o27, o26 }));
        Assert.That(Objectifs.Precedentes(l, o27), Is.EqualTo(new[] { o26 }));
        Assert.That(Objectifs.Precedentes(l, o26), Is.Empty);
        Assert.That(Objectifs.Precedentes(l, autre), Is.Empty);
        Assert.That(Objectifs.TexteOccurrence(o26), Is.EqualTo("Fait le 20/10/2026 · échéance 15/10/2026 · 2 documents"));

        // Rouvrir 2027 retire 2028 (inutilisée) : 2027 garde 2026 comme précédente.
        Objectifs.Rouvrir(l, o27);
        Assert.That(l, Has.No.Member(o28));
        Assert.That(Objectifs.Precedentes(l, o27), Is.EqualTo(new[] { o26 }));
    }

    /// Filtres combinés (06/10) : « En retard » est un sous-ensemble de « À faire » / « En cours » ;
    /// l'importance s'ajoute. Ex. En retard × Obligatoire = les seuls obligatoires en retard.
    [Test]
    public void Les_filtres_avancement_et_importance_se_combinent()
    {
        var retardObl = new Objective("Extincteurs", Objective.Importance.Obligatoire) { echeanceISO = "2026-10-01" };
        var retardEnCours = new Objective("Syndic", Objective.Importance.Rappel) { echeanceISO = "2026-09-30", avancement = Objective.Avancement.EnCours };
        var normale = new Objective("Hall");
        var fait = new Objective("Clés", Objective.Importance.Obligatoire);
        Objectifs.MarquerFait(new List<Objective> { fait }, fait, Auj);
        var tous = new[] { retardObl, retardEnCours, normale, fait };
        List<Objective> F(Objectifs.FiltreAvancement av, Objective.Importance? im) =>
            new List<Objective>(System.Linq.Enumerable.Where(tous, o => Objectifs.Garde(o, av, im, Auj)));

        Assert.That(F(Objectifs.FiltreAvancement.Tous, null), Is.EqualTo(new[] { retardObl, retardEnCours, normale }));
        Assert.That(F(Objectifs.FiltreAvancement.EnRetard, null), Is.EqualTo(new[] { retardObl, retardEnCours }));
        Assert.That(F(Objectifs.FiltreAvancement.AFaire, null), Is.EqualTo(new[] { retardObl, normale }), "le retard reste aussi « À faire »");
        Assert.That(F(Objectifs.FiltreAvancement.EnRetard, Objective.Importance.Obligatoire), Is.EqualTo(new[] { retardObl }));
        Assert.That(F(Objectifs.FiltreAvancement.Faits, Objective.Importance.Obligatoire), Is.EqualTo(new[] { fait }));
        Assert.That(F(Objectifs.FiltreAvancement.Tous, Objective.Importance.Normale), Is.EqualTo(new[] { normale }));
    }

    /// Tableau en colonnes : chaque colonne trie retards et échéances d'abord, « Fait » du
    /// plus récent au plus ancien ; déposer une carte change son avancement (« Fait » crée
    /// l'occurrence suivante d'un récurrent, le retirer de « Fait » la supprime).
    [Test]
    public void Le_tableau_range_et_deplace_les_cartes()
    {
        var hall = new Objective("Hall");
        var ext = new Objective("Extincteurs", Objective.Importance.Obligatoire)
            { echeanceISO = "2026-10-01", repeterTous = 1, repeterUnite = Objective.UniteRecurrence.Ans };
        var syndic = new Objective("Syndic", Objective.Importance.Rappel) { echeanceISO = "2026-10-10" };
        var vieux = new Objective("Août"); var recent = new Objective("Septembre");
        var l = new List<Objective> { hall, ext, syndic, vieux, recent };
        Objectifs.MarquerFait(l, vieux, new DateTime(2026, 8, 16));
        Objectifs.MarquerFait(l, recent, new DateTime(2026, 9, 14));

        Assert.That(Objectifs.Colonne(l, Objective.Avancement.AFaire, null, Auj), Is.EqualTo(new[] { ext, syndic, hall }));
        Assert.That(Objectifs.Colonne(l, Objective.Avancement.Fait, null, Auj), Is.EqualTo(new[] { recent, vieux }));
        Assert.That(Objectifs.Colonne(l, Objective.Avancement.AFaire, Objective.Importance.Rappel, Auj), Is.EqualTo(new[] { syndic }));

        Assert.That(Objectifs.Deplacer(l, hall, Objective.Avancement.EnCours, Auj), Is.Null);
        Assert.That(hall.avancement, Is.EqualTo(Objective.Avancement.EnCours));
        Assert.That(Objectifs.Deplacer(l, hall, Objective.Avancement.EnCours, Auj), Is.Null, "même colonne : rien");

        var s = Objectifs.Deplacer(l, ext, Objective.Avancement.Fait, Auj);
        Assert.That(s.echeanceISO, Is.EqualTo("2027-10-01"));
        Assert.That(Objectifs.Colonne(l, Objective.Avancement.AFaire, null, Auj), Does.Contain(s));

        Objectifs.Deplacer(l, ext, Objective.Avancement.EnCours, Auj);   // ressorti de « Fait »
        Assert.That(ext.avancement, Is.EqualTo(Objective.Avancement.EnCours));
        Assert.That(ext.faitLeISO, Is.Null);
        Assert.That(l, Has.No.Member(s), "occurrence inutilisée retirée");
    }

    /// Tuiles de « À traiter » (07/10) : un objectif se classe par son échéance, un rappel
    /// automatique par son urgence (priorité 0 = en retard, sinon à venir).
    [Test]
    public void Les_tuiles_a_traiter_classent_objectifs_et_rappels()
    {
        HomeAlert Obj(Objective o) => new HomeAlert { kind = HomeAlert.Kind.Objectif, objectif = o, priorite = 5 };
        var retard = Obj(new Objective("Extincteurs", Objective.Importance.Obligatoire) { echeanceISO = "2026-10-01" });
        var proche = Obj(new Objective("Syndic", Objective.Importance.Rappel) { echeanceISO = "2026-10-10" });
        var sansDate = Obj(new Objective("Hall"));
        var factUrgent = new HomeAlert { kind = HomeAlert.Kind.Facturation, priorite = 0 };
        var bail = new HomeAlert { kind = HomeAlert.Kind.BailRenouvellement, priorite = 1 };
        var toutes = new[] { retard, proche, sansDate, factUrgent, bail };
        HomeAlert[] F(ATraiterFiltre.Filtre f, Objective.Importance? im = null) => System.Linq.Enumerable.ToArray(
            System.Linq.Enumerable.Where(toutes, a => ATraiterFiltre.Garde(a, f, im, Auj)));

        Assert.That(F(ATraiterFiltre.Filtre.Tout), Is.EqualTo(toutes));
        Assert.That(F(ATraiterFiltre.Filtre.EnRetard), Is.EqualTo(new[] { retard, factUrgent }));
        Assert.That(F(ATraiterFiltre.Filtre.AVenir), Is.EqualTo(new[] { proche, bail }));
        Assert.That(F(ATraiterFiltre.Filtre.Automatiques), Is.EqualTo(new[] { factUrgent, bail }));
        Assert.That(F(ATraiterFiltre.Filtre.Objectifs), Is.EqualTo(new[] { retard, proche, sansDate }));

        // Catégorie (08/10) : celle de l'objectif ; un rappel automatique compte comme « Rappel ».
        Assert.That(F(ATraiterFiltre.Filtre.Tout, Objective.Importance.Obligatoire), Is.EqualTo(new[] { retard }));
        Assert.That(F(ATraiterFiltre.Filtre.Tout, Objective.Importance.Rappel), Is.EqualTo(new[] { proche, factUrgent, bail }));
        Assert.That(F(ATraiterFiltre.Filtre.Tout, Objective.Importance.Normale), Is.EqualTo(new[] { sansDate }));
        Assert.That(F(ATraiterFiltre.Filtre.EnRetard, Objective.Importance.Rappel), Is.EqualTo(new[] { factUrgent }));
    }

    [Test]
    public void La_saisie_est_controlee()
    {
        Assert.That(Objectifs.Verifier("  ", "", 15, 0), Is.Not.Null);
        Assert.That(Objectifs.Verifier("x", "", -1, 0), Is.Not.Null);
        Assert.That(Objectifs.Verifier("x", "", 15, 1), Is.Not.Null, "répéter sans échéance");
        Assert.That(Objectifs.Verifier("x", "2026-10-15", 15, 1), Is.Null);
        Assert.That(Objectifs.Verifier("x", "", 0, 0), Is.Null, "prévenir le jour même");
    }
}
