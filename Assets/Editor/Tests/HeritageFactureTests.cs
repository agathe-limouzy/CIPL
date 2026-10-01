using System;
using System.Collections.Generic;
using NUnit.Framework;

/// La CHAÎNE d'héritage des réglages de facture : le 1er locataire se règle, le 2e
/// est créé avec une copie de ses réglages, le 3e copie ceux du 2e.
///
/// Deux choses à verrouiller, et la seconde est la plus importante :
///   1. la source est le dernier locataire **créé** — pas le dernier enregistré, ni
///      le premier de la liste ;
///   2. seuls les RÉGLAGES sont copiés. Une date, un numéro ou un montant qui suivrait
///      d'un locataire à l'autre produirait un document faux.
///
/// Aucune scène requise : `HeritageFacture` reçoit la liste des locataires en
/// paramètre, ce qui permet de tester la règle sans `BatimentManager`.
public class HeritageFactureTests
{
    static int _rang;

    /// Crée un locataire horodaté dans l'ordre d'appel, pour que « le dernier créé »
    /// soit sans ambiguïté, quel que soit l'ordre des listes.
    static Locataire Locataire_(string nom)
    {
        _rang++;
        return new Locataire
        {
            Name = nom,
            creationISO = new DateTime(2026, 1, 1).AddMinutes(_rang).ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    /// Réglages « type » posés sur les quatre types de facture d'un locataire.
    static void Regler(Locataire l, string rib, string format, bool retard)
    {
        l.factureLoyer = Reglage(rib, format, retard);
        l.factureRegul = Reglage(rib, format, retard);
        l.factureRefac = Reglage(rib, format, retard);
        l.factureDepot = Reglage(rib, format, retard);
    }

    static FactureInfo Reglage(string rib, string format, bool retard) => new FactureInfo
    {
        ribId = rib,
        enteteId = "entete-" + rib,
        numeroFormat = format,
        ajouterRetard = retard,
        tvaDebit = retard,
        joindrePj = !retard,
        ajouterMensuel = retard
    };

    [SetUp]
    public void Reinitialiser() => _rang = 0;

    // ── Le tout premier locataire : « le truc initial » ─────────────────────

    [Test]
    public void Le_premier_locataire_ne_recoit_rien_et_part_des_valeurs_d_usine()
    {
        var t1 = Locataire_("Premier");

        HeritageFacture.Appliquer(t1, new List<Locataire>());

        Assert.That(t1.factureLoyer, Is.Null, "sans précédent, les panneaux appliquent leurs valeurs d'usine");
        Assert.That(t1.factureRegul, Is.Null);
        Assert.That(t1.factureRefac, Is.Null);
        Assert.That(t1.factureDepot, Is.Null);
        Assert.That(t1.creationISO, Is.Not.Null.And.Not.Empty, "la fiche doit être horodatée");
    }

    // ── La chaîne T1 → T2 → T3 ──────────────────────────────────────────────

    [Test]
    public void Le_second_recoit_une_copie_des_reglages_du_premier()
    {
        var t1 = Locataire_("Premier");
        Regler(t1, "rib-bnp", "AJMN", retard: false);

        var t2 = Locataire_("Second");
        HeritageFacture.Appliquer(t2, new List<Locataire> { t1 });

        Assert.That(t2.factureLoyer, Is.Not.Null);
        Assert.That(t2.factureLoyer.ribId, Is.EqualTo("rib-bnp"));
        Assert.That(t2.factureLoyer.numeroFormat, Is.EqualTo("AJMN"));
        Assert.That(t2.factureLoyer.ajouterRetard, Is.False, "une case décochée se duplique décochée");
        Assert.That(t2.factureDepot.enteteId, Is.EqualTo("entete-rib-bnp"), "les 4 types sont dupliqués");
    }

    [Test]
    public void Le_troisieme_suit_le_second_et_non_le_premier()
    {
        // C'est le cœur de la demande : modifier le 2e change ce qu'héritera le 3e.
        var t1 = Locataire_("Premier");
        Regler(t1, "rib-bnp", "AJMN", retard: false);

        var t2 = Locataire_("Second");
        HeritageFacture.Appliquer(t2, new List<Locataire> { t1 });
        Regler(t2, "rib-credit-agricole", "AN", retard: true);   // l'utilisatrice retouche le 2e

        var t3 = Locataire_("Troisieme");
        HeritageFacture.Appliquer(t3, new List<Locataire> { t1, t2 });

        Assert.That(t3.factureLoyer.ribId, Is.EqualTo("rib-credit-agricole"));
        Assert.That(t3.factureLoyer.numeroFormat, Is.EqualTo("AN"));
        Assert.That(t3.factureLoyer.ajouterRetard, Is.True);
    }

    [Test]
    public void Les_phrases_du_depot_suivent_la_chaine()
    {
        // Reformuler une fois doit suffire : les locataires suivants héritent du
        // texte, sinon il faudrait le retaper sur chaque nouvelle facture de dépôt.
        var t1 = Locataire_("Premier");
        t1.factureDepot = new FactureInfo
        {
            depotDu = "et ainsi, vous nous devez {depot.montant}",
            depotRembourse = "nous vous remboursons {depot.montant}",
            depotRappel = "rappel : {depot.termes}",
            depotEquilibre = "rien à régler"
        };

        var t2 = Locataire_("Second");
        HeritageFacture.Appliquer(t2, new List<Locataire> { t1 });

        Assert.That(t2.factureDepot.depotDu, Is.EqualTo("et ainsi, vous nous devez {depot.montant}"));
        Assert.That(t2.factureDepot.depotRembourse, Is.EqualTo("nous vous remboursons {depot.montant}"));
        Assert.That(t2.factureDepot.depotRappel, Is.EqualTo("rappel : {depot.termes}"));
        Assert.That(t2.factureDepot.depotEquilibre, Is.EqualTo("rien à régler"));

        // …et restent propres à chacun.
        t2.factureDepot.depotDu = "formulation du second";
        Assert.That(t1.factureDepot.depotDu, Is.EqualTo("et ainsi, vous nous devez {depot.montant}"));
    }

    [Test]
    public void Modifier_le_second_ne_touche_pas_au_premier()
    {
        var t1 = Locataire_("Premier");
        Regler(t1, "rib-bnp", "AJMN", retard: false);

        var t2 = Locataire_("Second");
        HeritageFacture.Appliquer(t2, new List<Locataire> { t1 });
        t2.factureLoyer.ribId = "rib-autre";

        Assert.That(t1.factureLoyer.ribId, Is.EqualTo("rib-bnp"),
                    "chaque locataire possède ses réglages : ce doit être une copie, pas un partage");
    }

    [Test]
    public void L_ordre_de_la_liste_ne_decide_pas_c_est_la_date_de_creation()
    {
        var t1 = Locataire_("Premier");
        Regler(t1, "rib-premier", "AJMN", retard: false);

        var t2 = Locataire_("Second");
        Regler(t2, "rib-second", "AN", retard: true);

        var t3 = Locataire_("Troisieme");
        // Liste volontairement désordonnée : le plus récemment créé est t2, pas le dernier de la liste.
        HeritageFacture.Appliquer(t3, new List<Locataire> { t2, t1 });

        Assert.That(t3.factureLoyer.ribId, Is.EqualTo("rib-second"));
    }

    [Test]
    public void Un_type_jamais_regle_sur_le_precedent_reste_vide()
    {
        var t1 = Locataire_("Premier");
        t1.factureLoyer = new FactureInfo { ribId = "rib-bnp" };   // seul le loyer est réglé

        var t2 = Locataire_("Second");
        HeritageFacture.Appliquer(t2, new List<Locataire> { t1 });

        Assert.That(t2.factureLoyer, Is.Not.Null);
        Assert.That(t2.factureRegul, Is.Null, "rien à copier → valeurs d'usine à l'ouverture du panneau");
    }

    // ── D'un bâtiment à l'autre ─────────────────────────────────────────────

    static Batiment Batiment_(string nom, params Locataire[] locataires)
    {
        var b = new Batiment { Name = nom };
        b.locataireDuBatiment = new List<Locataire>(locataires);
        return b;
    }

    [Test]
    public void Le_premier_locataire_d_un_batiment_neuf_copie_celui_d_un_autre_batiment()
    {
        // Le cas demandé : nouveau bâtiment, donc aucun locataire dedans — la chaîne
        // doit quand même reprendre le dernier créé, qui vit ailleurs.
        var t1 = Locataire_("Locataire de Rivoli");
        Regler(t1, "rib-immeuble-rivoli", "AJMN", retard: false);

        var rivoli = Batiment_("Immeuble Rivoli", t1);
        var neuf   = Batiment_("Bâtiment neuf");   // vide

        var premierDuNeuf = Locataire_("Premier du neuf");
        HeritageFacture.Appliquer(premierDuNeuf,
            HeritageFacture.TousLesLocataires(new List<Batiment> { rivoli, neuf }));

        Assert.That(premierDuNeuf.factureLoyer, Is.Not.Null,
                    "un bâtiment neuf ne doit pas faire repartir les réglages d'usine");
        Assert.That(premierDuNeuf.factureLoyer.ribId, Is.EqualTo("rib-immeuble-rivoli"));
        Assert.That(premierDuNeuf.factureLoyer.numeroFormat, Is.EqualTo("AJMN"));
    }

    [Test]
    public void La_chaine_suit_la_chronologie_meme_a_travers_les_batiments()
    {
        var ancien = Locataire_("Ancien, bâtiment A");
        Regler(ancien, "rib-ancien", "AJMN", retard: false);

        var recent = Locataire_("Récent, bâtiment B");
        Regler(recent, "rib-recent", "AN", retard: true);

        // Le plus récent est dans le SECOND bâtiment ; l'ordre des bâtiments ne doit
        // pas primer sur la date de création.
        var a = Batiment_("Bâtiment A", ancien);
        var b = Batiment_("Bâtiment B", recent);

        var neuf = Locataire_("Nouveau, bâtiment C");
        HeritageFacture.Appliquer(neuf,
            HeritageFacture.TousLesLocataires(new List<Batiment> { a, b }));

        Assert.That(neuf.factureLoyer.ribId, Is.EqualTo("rib-recent"));

        // Et l'inverse : bâtiments dans l'autre sens, même résultat.
        var neuf2 = Locataire_("Nouveau bis");
        HeritageFacture.Appliquer(neuf2,
            HeritageFacture.TousLesLocataires(new List<Batiment> { b, a }));

        Assert.That(neuf2.factureLoyer.ribId, Is.EqualTo("rib-recent"));
    }

    [Test]
    public void Tous_les_locataires_agrege_bien_les_batiments()
    {
        var a = Batiment_("A", Locataire_("A1"), Locataire_("A2"));
        var b = Batiment_("B", Locataire_("B1"));

        var tous = HeritageFacture.TousLesLocataires(new List<Batiment> { a, b });

        Assert.That(tous.Count, Is.EqualTo(3));
    }

    [Test]
    public void Un_batiment_nul_ou_sans_liste_ne_leve_rien()
    {
        var sansListe = new Batiment { Name = "Sans liste" };
        sansListe.locataireDuBatiment = null;

        Assert.That(HeritageFacture.TousLesLocataires(null), Is.Empty);
        Assert.That(HeritageFacture.TousLesLocataires(
            new List<Batiment> { null, sansListe }), Is.Empty);
    }

    // ── La garde qui compte : aucune donnée de facture ne suit ───────────────

    [Test]
    public void Aucune_donnee_de_facture_n_est_dupliquee()
    {
        var t1 = Locataire_("Premier");
        t1.factureLoyer = new FactureInfo
        {
            // Réglages — doivent suivre.
            ribId = "rib-bnp", numeroFormat = "AJMN",
            // Données — ne doivent JAMAIS suivre.
            destNom = "SARL DEMO", destAdresse = "6 route d'Agde", destSiret = "111 222 333 00044",
            dateISO = "2026-04-03", dateEcheanceISO = "2026-05-03",
            numeroId = "777", numero = "2026/04777",
            sommePhrase = "SOMME À NOUS RÉGLER LE 3 mai 2026",
            refInterne = "N° Interne Magasin 001048",
            emailDest = "demo@example.invalid", objet = "Loyer avril 2026",
            loyerMontant = 12345.67f, provisionMontant = 890.12f,
            moisPeriode = 4, anneePeriode = 2026,
            saved = true
        };

        var t2 = Locataire_("Second");
        HeritageFacture.Appliquer(t2, new List<Locataire> { t1 });
        var f = t2.factureLoyer;

        Assert.That(f.ribId, Is.EqualTo("rib-bnp"), "le réglage, lui, doit bien suivre");

        Assert.That(f.destNom, Is.Null.Or.Empty);
        Assert.That(f.destAdresse, Is.Null.Or.Empty);
        Assert.That(f.destSiret, Is.Null.Or.Empty);
        Assert.That(f.dateISO, Is.Null.Or.Empty, "une date héritée daterait la facture du locataire d'à côté");
        Assert.That(f.dateEcheanceISO, Is.Null.Or.Empty);
        Assert.That(f.numeroId, Is.Null.Or.Empty, "un numéro hérité, c'est un doublon de numérotation");
        Assert.That(f.numero, Is.Null.Or.Empty);
        Assert.That(f.sommePhrase, Is.Null.Or.Empty);
        Assert.That(f.refInterne, Is.Null.Or.Empty);
        Assert.That(f.emailDest, Is.Null.Or.Empty, "l'email doit venir de la fiche du nouveau locataire");
        Assert.That(f.objet, Is.Null.Or.Empty);
        Assert.That(f.loyerMontant, Is.EqualTo(0f));
        Assert.That(f.provisionMontant, Is.EqualTo(0f));
        Assert.That(f.moisPeriode, Is.EqualTo(0));
        Assert.That(f.anneePeriode, Is.EqualTo(0));
        Assert.That(f.saved, Is.False, "sans ça, les montants ne seraient pas recalculés depuis la fiche");
    }

    // ── Robustesse ──────────────────────────────────────────────────────────

    [Test]
    public void Le_nouveau_locataire_ne_peut_pas_s_heriter_de_lui_meme()
    {
        var t1 = Locataire_("Premier");
        Regler(t1, "rib-bnp", "AJMN", retard: false);

        var t2 = Locataire_("Second");
        // La liste contient déjà le nouveau (cas d'un appel après l'ajout à la liste).
        HeritageFacture.Appliquer(t2, new List<Locataire> { t1, t2 });

        Assert.That(t2.factureLoyer, Is.Not.Null);
        Assert.That(t2.factureLoyer.ribId, Is.EqualTo("rib-bnp"));
    }

    [Test]
    public void Une_liste_nulle_ou_un_locataire_nul_ne_leve_rien()
    {
        var t = Locataire_("Seul");

        Assert.DoesNotThrow(() => HeritageFacture.Appliquer(t, null));
        Assert.DoesNotThrow(() => HeritageFacture.Appliquer(null, new List<Locataire>()));
        Assert.DoesNotThrow(() => HeritageFacture.Appliquer(t, new List<Locataire> { null }));
        Assert.That(HeritageFacture.Precedent(null, t), Is.Null);
    }

    [Test]
    public void Une_fiche_anterieure_sans_date_reste_eligible()
    {
        // Fiches créées avant l'ajout de `creationISO` : sans elles comme source,
        // une sauvegarde existante repartirait d'usine à chaque nouveau locataire.
        var ancien = new Locataire { Name = "Ancien", creationISO = null };
        Regler(ancien, "rib-historique", "AMN", retard: true);

        var neuf = Locataire_("Neuf");
        HeritageFacture.Appliquer(neuf, new List<Locataire> { ancien });

        Assert.That(neuf.factureLoyer, Is.Not.Null);
        Assert.That(neuf.factureLoyer.ribId, Is.EqualTo("rib-historique"));
    }

    [Test]
    public void L_horodatage_d_une_fiche_deja_datee_n_est_pas_ecrase()
    {
        var t = new Locataire { Name = "Repris", creationISO = "2026-01-01 08:00:00" };

        HeritageFacture.Appliquer(t, new List<Locataire>());

        Assert.That(t.creationISO, Is.EqualTo("2026-01-01 08:00:00"));
    }
}
