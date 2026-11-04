using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot 1 (03/11/2026) : les sept critiques, chacun avec son test (REVUE_17/SYNTHESE.md).
public class Revue17Tests
{
    static string Sql17 => File.ReadAllText(Path.Combine("Serveur", "etape17a_supabase.sql"));
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));
    static string Fonction(string sql, string nom)
    {
        int i = sql.IndexOf("create or replace function public." + nom + "(", StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), nom + " absente du SQL");
        return sql.Substring(i, sql.IndexOf("end $$;", i, StringComparison.Ordinal) - i);
    }

    // ── D2-01 : une version plus ancienne ne réécrit pas ce qu'une plus récente a enregistré ──

    [Test]
    public void D2_01_un_champ_inconnu_disparait_a_la_reecriture_et_le_modele_part_avec_chaque_ecriture()
    {
        // Le risque que le serveur bloque désormais : JsonUtility ne garde pas un champ qu'il ne connaît pas.
        var e = JsonUtility.FromJson<FactureEtat>("{\"key\":\"loyer-2026-P4\",\"champFutur\":7}");
        StringAssert.DoesNotContain("champFutur", JsonUtility.ToJson(e));
        var op = new EcritureCloud.Op { Type = EcritureCloud.TypeOp.Batiment, Id = "b1", Version = 3, Json = "{}" };
        StringAssert.Contains("\"p_modele\":" + DonneesCloud.Modele, EcritureCloud.CorpsEnregistrer("e1", op, "s1"));
        StringAssert.Contains("\"p_modele\":" + DonneesCloud.Modele, ReglagesCloud.Corps("e1", 2, "{}", "s1"));
        StringAssert.Contains("p_modele", Src("Cloud/TransfertCloud.cs"), "le transfert aussi");
        StringAssert.Contains("mettez CIPL à jour", EcritureCloud.MotifRefus(new EcritureCloud.Resultat { refus = "application" }, "Rivoli"));
    }

    [Test]
    public void D2_01_le_serveur_refuse_une_version_plus_ancienne()
    {
        string sql = Sql17;
        foreach (var t in new[] { "batiments", "locataires", "reglages" })
            StringAssert.Contains($"alter table public.{t} add column if not exists modele int not null default 0;", sql);
        foreach (var f in new[] { "enregistrer_batiment", "enregistrer_locataire", "enregistrer_reglages", "transferer_fiches" })
        {
            string corps = Fonction(sql, f);
            StringAssert.Contains("p_modele int default 0", corps, f);
            StringAssert.Contains("'refus', 'application'", corps, f);
            StringAssert.Contains("coalesce(p_modele, 0))", corps.Substring(corps.IndexOf("insert into public.", StringComparison.Ordinal)), f + " : la colonne est écrite");
        }
    }

    /// Le numéro de modèle ne protège que s'il monte à chaque champ ajouté ou retiré dans ce qui part au serveur.
    [Test]
    public void D2_01_un_champ_ajoute_fait_monter_le_numero_de_modele()
    {
        string e = Empreinte(typeof(Batiment), typeof(Locataire), typeof(ReglageData));
        Assert.That(DonneesCloud.Modele + ":" + Hash(e), Is.EqualTo("7:1d9bc7661d3e"),
            "Un champ des données a changé : montez DonneesCloud.Modele, puis recopiez ici le nouveau « numéro:empreinte ».");
    }

    static string Empreinte(params Type[] racines)
    {
        var vus = new HashSet<Type>(); var lignes = new List<string>(); var file = new Queue<Type>(racines);
        while (file.Count > 0)
        {
            var t = file.Dequeue();
            if (!vus.Add(t)) continue;
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.IsNotSerialized || (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null)) continue;
                lignes.Add(t.Name + "." + f.Name + ":" + f.FieldType.Name);
                var ft = f.FieldType.IsArray ? f.FieldType.GetElementType() : f.FieldType.IsGenericType ? f.FieldType.GetGenericArguments()[0] : f.FieldType;
                if (ft.IsClass && ft != typeof(string) && ft.GetCustomAttribute<SerializableAttribute>() != null && ft.Assembly == typeof(Batiment).Assembly) file.Enqueue(ft);
            }
        }
        lignes.Sort(StringComparer.Ordinal);
        return string.Join("\n", lignes);
    }

    static string Hash(string s)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
            return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes(s)).Take(6).Select(b => b.ToString("x2")));
    }

    // ── D3-01 : une entreprise ajoutée part avec ses Réglages, pas ceux de CIPL ──

    [Test]
    public void D3_01_une_entreprise_ajoutee_part_avec_ses_reglages_pas_ceux_de_CIPL()
    {
        var r = RegleInscription.ReglagesAjout(true, "SCI Rivoli", "73282932000074", null);
        Assert.That(r.sansLogo, Is.True, "pas le logo de CIPL");
        Assert.That(r.entrepriseNom, Is.EqualTo("SCI Rivoli"));
        StringAssert.Contains("Siret : 732 829 320 00074", r.basDePage);
        StringAssert.Contains("TVA intracommunautaire : " + FactureEn16931.TvaIntra("732829320"), r.basDePage);
        StringAssert.DoesNotContain("717 220 883", r.basDePage);
        Assert.That(RegleInscription.ReglagesAjout(false, "Patrimoine de Jean Martin", "", null).basDePage, Is.EqualTo("Patrimoine de Jean Martin"));
        StringAssert.Contains("insert into public.reglages", Fonction(Sql17, "ajouter_entreprise"));
        StringAssert.Contains("RegleInscription.ReglagesAjout(", Src("Cloud/SectionAbonnement.cs"));
    }

    // ── D5-01 et D5-02 : la correction d'une facture transmise ──

    [Test]
    public void D5_01_la_rectificative_a_sa_propre_cle()
    {
        var loc = new Locataire { id = "a1b2c3d4-0000-0000-0000-000000000000" };
        var r = new FactureEtat { numero = "2026/10004", plateformeDonnees = "{\"number\":\"2026/10004\",\"issue_date\":\"2026-10-01\",\"type_code\":380}" };
        Assert.That(FactureElectronique.Cle(loc, r), Is.EqualTo("a1b2c3d4-2026/10004"), "une facture déjà transmise garde sa clé");
        r.plateformeDonnees = "{\"number\":\"2026/10004-corrigee-1\",\"issue_date\":\"2026-10-15\",\"type_code\":384}";
        Assert.That(FactureElectronique.Cle(loc, r), Is.EqualTo("a1b2c3d4-2026/10004-corrigee-1"), "même numéro sur la ligne après MarquerCorrige");
        Assert.That(FactureElectronique.Cle(loc, new FactureEtat { key = "loyer-2026-P4" }), Is.EqualTo("a1b2c3d4-loyer-2026-P4"));
        Assert.That(FactureEn16931.NumeroDe(null), Is.Null);
    }

    [Test]
    public void D5_02_un_avoir_ne_remplace_pas_une_facture_transmise()
    {
        var transmise = new FactureEtat { plateformeId = "pdp-1", plateformeDonnees = "{\"number\":\"2026/10004\",\"type_code\":380}" };
        Assert.That(FactureElectronique.RefusCorrection(transmise, -240f, "Régularisation 2026"), Is.Not.Null, "facture transmise corrigée en avoir");
        Assert.That(FactureElectronique.RefusCorrection(transmise, 1080f, "Loyer"), Is.Null, "rectificative (384) : elle remplace");
        var avoir = new FactureEtat { plateformeId = "pdp-2", plateformeDonnees = "{\"number\":\"2026/10005\",\"type_code\":381}" };
        Assert.That(FactureElectronique.RefusCorrection(avoir, -177.6f, "Régularisation 2026"), Is.Not.Null, "correction d'un avoir transmis");
        Assert.That(FactureElectronique.RefusCorrection(new FactureEtat(), -240f, "Régularisation"), Is.Null, "jamais transmise");
        Assert.That(FactureElectronique.RefusCorrection(null, -240f, "Régularisation"), Is.Null);
        foreach (var p in new[] { "FactureLoyerPanel.cs", "FactureRegulPanel.cs", "FactureRefacPanel.cs" })
            StringAssert.Contains("FactureElectronique.RefusCorrection(", Src("Facturation/" + p), p);
    }

    // ── D6-01 : une charge saisie pour un seul locataire ──

    [Test]
    public void D6_01_une_charge_Tous_saisie_pour_un_seul_locataire_n_est_pas_payee_deux_fois()
    {
        var a = new Locataire { id = "A", lotBatiment = 1 };
        var bat = new Batiment { tailleBatiment = 100f, locataireDuBatiment = new List<Locataire> { a } };
        var c = new ChargeBatiment { cout = 3000f, tousLocataires = true };
        c.ratios = ChargePanel.Ratios("Manuel", new List<Locataire> { a }, l => 0f, l => 0f);   // un seul : pas de champ manuel affiché
        Assert.That(c.ratios, Has.Count.EqualTo(1));
        Assert.That(ListesCharges.QuotePart(c, a, bat), Is.EqualTo(3000f).Within(0.01f));
        var b = new Locataire { id = "B", lotBatiment = 2 };
        bat.locataireDuBatiment.Add(b);
        Assert.That(ListesCharges.QuotePart(c, a, bat) + ListesCharges.QuotePart(c, b, bat), Is.EqualTo(3000f).Within(0.01f), "l'arrivant ne la paie pas en entier");
        c.ratios = new List<ChargeRatio>();   // charge enregistrée avant le 03/11, sans part
        Assert.That(ListesCharges.QuotePart(c, a, bat) + ListesCharges.QuotePart(c, b, bat), Is.EqualTo(3000f).Within(0.01f), "partagée, pas payée deux fois");
    }

    /// Retour BL4 (04/11) : une charge « Tous » se répartit entre les locataires présents l'année de la charge, même ajoutés
    /// dans CIPL après sa saisie ; le prorata de présence s'applique ensuite.
    [Test]
    public void BL4_une_charge_Tous_se_repartit_entre_les_presents_de_l_annee()
    {
        var a = new Locataire { id = "A", Name = "Locataire BL4 A", lotBatiment = 1, tailleLot = 100f };
        a.DateDebutBail = new DateTime(2026, 1, 1); a.DateFinBail = new DateTime(2034, 12, 31);
        var bat = new Batiment { tailleBatiment = 200f, locataireDuBatiment = new List<Locataire> { a } };
        var c = new ChargeBatiment { cout = 1000f, tousLocataires = true, typeRatio = "Surface", dateISO = "2026-11-04" };
        c.ratios = ChargePanel.Ratios("Surface", new List<Locataire> { a }, l => 100f, null);   // saisie quand A était seul
        var b = new Locataire { id = "B", Name = "BL4 b" };   // lot 0, surface non saisie : les 100 m² restants
        b.DateDebutBail = new DateTime(2025, 1, 10); b.DateFinBail = new DateTime(2034, 1, 9);
        bat.locataireDuBatiment.Add(b);
        Assert.That(ListesCharges.QuotePartAuProrata(c, a, bat), Is.EqualTo(500f).Within(0.01f));
        Assert.That(ListesCharges.QuotePartAuProrata(c, b, bat), Is.EqualTo(500f).Within(0.01f), "présent toute l'année : sa part, pas 0");
        b.DateDebutBail = new DateTime(2026, 7, 1);
        Assert.That(ListesCharges.QuotePartAuProrata(c, b, bat), Is.EqualTo(500f * 184f / 365f).Within(0.02f), "arrivé le 01/07 : au prorata");
        var avant = new Locataire { id = "Z", Name = "Parti en 2025" };
        avant.DateDebutBail = new DateTime(2020, 1, 1); avant.DateFinBail = new DateTime(2028, 12, 31);
        avant.dateSortieISO = "2025-06-30";   // départ saisi (la fin du bail seule ne veut pas dire départ : il se prolonge)
        bat.locataireDuBatiment.Add(avant);
        Assert.That(ListesCharges.QuotePartAuProrata(c, avant, bat), Is.EqualTo(0f), "absent en 2026 : rien");
        c.typeRatio = "Manuel"; c.ratios = new List<ChargeRatio> { new ChargeRatio("A", 1f) };
        Assert.That(ListesCharges.QuotePart(c, b, bat), Is.EqualTo(0f), "Manuel : les parts saisies, telles quelles");
    }

    /// Demande du 04/11 (BL4) : une charge ponctuelle (réparation) « Tous » ne concerne que les locataires présents à sa date,
    /// chacun sa part entière ; une charge de l'année, les présents de l'année au prorata de leur présence.
    [Test]
    public void BL4_une_charge_ponctuelle_ne_concerne_que_les_presents_a_sa_date()
    {
        var a = new Locataire { id = "A", lotBatiment = 1, tailleLot = 100f };
        a.DateDebutBail = new DateTime(2025, 1, 1); a.DateFinBail = new DateTime(2033, 12, 31);
        var b = new Locataire { id = "B", lotBatiment = 2, tailleLot = 100f };
        b.DateDebutBail = new DateTime(2026, 7, 1); b.DateFinBail = new DateTime(2035, 6, 30);
        var bat = new Batiment { tailleBatiment = 200f, locataireDuBatiment = new List<Locataire> { a, b } };
        var c = new ChargeBatiment { cout = 1000f, tousLocataires = true, typeRatio = "Surface", dateISO = "2026-03-15", ponctuelle = true };
        Assert.That(ListesCharges.QuotePartAuProrata(c, a, bat), Is.EqualTo(500f).Within(0.01f),
            "présent le 15/03 : 100 m² sur 200 ; le lot de B, vide ce jour-là, reste au propriétaire (retour BL8)");
        c.dateISO = "2026-09-15";
        Assert.That(ListesCharges.QuotePartAuProrata(c, b, bat), Is.EqualTo(500f).Within(0.01f), "présent le 15/09 : sa part entière, sans prorata");
        c.dateISO = "2026-03-15";
        Assert.That(ListesCharges.QuotePartAuProrata(c, b, bat), Is.EqualTo(0f), "arrivé après la date : rien");
        Assert.That(ListesCharges.Concerne(c, b), Is.False, "ni proposée à sa refacturation ni à sa régularisation");
        Assert.That(ListesCharges.PresencePartielle(a, c), Is.False, "pas de case « au prorata » pour une charge ponctuelle");
        var cochee = new ChargeBatiment { cout = 300f, tousLocataires = false, locatairesConcernes = new List<string> { "B" },
                                          typeRatio = "Surface", dateISO = "2026-03-15", ponctuelle = true };
        Assert.That(ListesCharges.QuotePartAuProrata(cochee, b, bat), Is.EqualTo(300f).Within(0.01f), "cochée à la main : en entier");
        c.ponctuelle = false;
        Assert.That(ListesCharges.Concerne(c, b), Is.True);
        Assert.That(ListesCharges.QuotePartAuProrata(c, a, bat), Is.EqualTo(500f).Within(0.01f), "sur l'année");
        Assert.That(ListesCharges.QuotePartAuProrata(c, b, bat), Is.EqualTo(500f * 184f / 365f).Within(0.02f), "sur l'année : au prorata");
        Assert.That(ListesCharges.PresencePartielle(b, c), Is.True);
        Assert.That(Loyers.PresentLe(b, new DateTime(2026, 6, 30)), Is.False);
        Assert.That(Loyers.PresentLe(b, new DateTime(2026, 7, 1)), Is.True);
        Assert.That(new ChargeBatiment().ponctuelle, Is.False, "par défaut, et pour les charges déjà saisies : sur l'année");
        string panneau = Src("Facturation/ChargePanel.cs");
        StringAssert.Contains("_edit.ponctuelle = _fPonctuelle", panneau, "le formulaire l'enregistre");
        StringAssert.Contains("typeRatio = _fType, tousLocataires = _fTous.isOn", panneau, "les euros du formulaire suivent son choix");
    }

    /// Retour BL8 (04/11) : le formulaire montre ce qui sera facturé (au prorata de la présence) et pourquoi ; décision du 04/11
    /// (Code de commerce, art. R145-35) : la part des locaux vides reste au propriétaire, elle n'est plus reportée sur les autres.
    [Test]
    public void BL8_la_part_des_locaux_vides_reste_au_proprietaire()
    {
        var a = new Locataire { id = "A", Name = "A", lotBatiment = 1, tailleLot = 100f };
        a.DateDebutBail = new DateTime(2026, 1, 1); a.DateFinBail = new DateTime(2034, 12, 31);
        var b = new Locataire { id = "B", Name = "B", lotBatiment = 2, tailleLot = 50f };
        b.DateDebutBail = new DateTime(2025, 1, 10); b.DateFinBail = new DateTime(2034, 1, 9);
        var cc = new Locataire { id = "C", Name = "BH4 C", lotBatiment = 3, tailleLot = 50f };
        cc.DateDebutBail = new DateTime(2027, 1, 1); cc.DateFinBail = new DateTime(2035, 12, 31);
        var bat = new Batiment { tailleBatiment = 200f, locataireDuBatiment = new List<Locataire> { a, b, cc } };
        var c = new ChargeBatiment { cout = 1000f, tousLocataires = true, typeRatio = "Surface", dateISO = "2026-11-04" };
        Assert.That(ListesCharges.QuotePartAuProrata(c, a, bat), Is.EqualTo(500f).Within(0.01f), "lot de C vide toute l'année : pas reporté sur A");
        Assert.That(ListesCharges.QuotePartAuProrata(c, b, bat), Is.EqualTo(250f).Within(0.01f));
        Assert.That(ListesCharges.QuotePartAuProrata(c, cc, bat), Is.EqualTo(0f));
        Assert.That(ChargePanel.DetailDuPartage(c, bat.locataireDuBatiment, bat),
            Is.EqualTo("BH4 C : pas dans les lieux en 2026. Part du propriétaire (locaux vides) : 250,00 €."));

        cc.DateDebutBail = new DateTime(2026, 12, 12);
        Assert.That(ListesCharges.QuotePartAuProrata(c, cc, bat), Is.EqualTo(13.70f).Within(0.01f), "20 jours sur 365");
        Assert.That(ChargePanel.DetailDuPartage(c, bat.locataireDuBatiment, bat), Is.EqualTo(
            "BH4 C arrive le 12/12/2026 : 20 jours sur 365 (part entière 250,00 €). Part du propriétaire (locaux vides) : 236,30 €."));
        c.typeRatio = "Égalité";
        Assert.That(ListesCharges.QuotePart(c, a, bat), Is.EqualTo(1000f / 3f).Within(0.01f), "Égalité : pas de surface, pas de lot vide");

        cc.DateDebutBail = new DateTime(2027, 1, 1); c.typeRatio = "Surface"; bat.tailleBatiment = 0f;
        Assert.That(ListesCharges.QuotePart(c, a, bat), Is.EqualTo(1000f * 100f / 150f).Within(0.01f), "surface du bâtiment non saisie : entre les présents");
        var seul = new Batiment { tailleBatiment = 200f, locataireDuBatiment = new List<Locataire> { a } };
        Assert.That(ListesCharges.QuotePart(c, a, seul), Is.EqualTo(500f).Within(0.01f), "seul sur 100 m² de 200 : sa surface, plus le tout");
        c.ponctuelle = true;
        Assert.That(ChargePanel.DetailDuPartage(c, seul.locataireDuBatiment, seul), Is.EqualTo("Part du propriétaire (locaux vides) : 500,00 €."),
            "ponctuelle : pas de jours, la part du lot vide à sa date");
    }

    /// Demande du 04/11 : les lots se déclarent dans le bâtiment ; le locataire y est rattaché par son numéro et en prend la
    /// surface ; la surface du bâtiment est leur somme ; un lot vide reste au propriétaire ; pas de locataire sans lot libre.
    [Test]
    public void Lots_le_locataire_prend_la_surface_de_son_lot()
    {
        var a = new Locataire { id = "A", Name = "A", lotBatiment = 1, tailleLot = 80f };   // ancienne surface saisie
        a.DateDebutBail = new DateTime(2025, 1, 1); a.DateFinBail = new DateTime(2033, 12, 31);
        var b = new Locataire { id = "B", Name = "B" };   // pas encore rattaché
        b.DateDebutBail = new DateTime(2025, 1, 1); b.DateFinBail = new DateTime(2033, 12, 31);
        var bat = new Batiment { Name = "Immeuble", tailleBatiment = 999f, locataireDuBatiment = new List<Locataire> { a, b } };
        Assert.That(LotsBatiment.RefusSansLotLibre(bat), Does.Contain("pas encore de lot"));
        Assert.That(LotsBatiment.Surface(bat, a), Is.EqualTo(80f), "sans lots déclarés : l'ancienne règle");

        bat.lots = new List<LotBatiment> { new LotBatiment { numero = 1, surface = 100f }, new LotBatiment { numero = 2, surface = 50f },
                                           new LotBatiment { numero = 3, surface = 50f } };
        Assert.That(LotsBatiment.Surface(bat, a), Is.EqualTo(100f), "la surface de son lot, plus celle saisie");
        Assert.That(LotsBatiment.Surface(bat, b), Is.EqualTo(0f), "sans lot : pas de surface");
        Assert.That(LotsBatiment.SurfaceTotale(bat), Is.EqualTo(200f), "la somme des lots, plus la surface saisie");
        Assert.That(LotsBatiment.SansLot(bat), Is.EqualTo(new[] { b }));
        Assert.That(LotsBatiment.Libres(bat).Select(x => x.numero), Is.EqualTo(new[] { 2, 3 }));
        Assert.That(LotsBatiment.Libres(bat, a).Select(x => x.numero), Is.EqualTo(new[] { 1, 2, 3 }), "son propre lot lui reste proposé");
        Assert.That(LotsBatiment.RefusSansLotLibre(bat), Is.Null);
        Assert.That(LotsBatiment.Libelle(bat, bat.lots[0]), Is.EqualTo("Lot 1 · 100 m² · A"));
        Assert.That(FactureVarResolver.Resolve("{loc.taille}", a, bat, new FactureContext()), Is.EqualTo("100 m²"));

        b.lotBatiment = 2;
        var c = new ChargeBatiment { cout = 1000f, tousLocataires = true, typeRatio = "Surface", dateISO = "2026-06-01" };
        Assert.That(ListesCharges.QuotePart(c, a, bat), Is.EqualTo(500f).Within(0.01f), "100 m² sur les 200 des lots");
        Assert.That(ListesCharges.QuotePart(c, b, bat), Is.EqualTo(250f).Within(0.01f), "le lot 3, vide, reste au propriétaire");

        Assert.That(LotsBatiment.Refus(new List<LotBatiment> { new LotBatiment { numero = 1, surface = 100f } }, bat),
            Does.Contain("Le lot 2 est occupé par « B »"), "un lot occupé ne disparaît pas");
        Assert.That(LotsBatiment.Refus(new List<LotBatiment> { new LotBatiment { numero = 1, surface = 100f }, new LotBatiment { numero = 1, surface = 50f } }, bat),
            Does.Contain("Deux lots portent le n° 1"));
        Assert.That(LotsBatiment.Refus(new List<LotBatiment> { new LotBatiment { numero = 1, surface = 0f } }, bat), Does.Contain("surface du lot 1"));
        Assert.That(LotsBatiment.Refus(bat.lots, bat), Is.Null);

        bat.locataireDuBatiment.Add(new Locataire { id = "C", Name = "C", lotBatiment = 3 });
        Assert.That(LotsBatiment.RefusSansLotLibre(bat), Does.Contain("Tous les lots de « Immeuble » sont occupés").And.Not.Contain("« OK »"));
        StringAssert.Contains("\"Ajouter un lot\", \"Retour\"", Src("Batiment/BatimentPrefab.cs"), "retour BM3 : le choix, pas la fenêtre d'office");
        // Retour BM3 : le lot créé depuis le refus, l'ajout reprend (la fiche du nouveau locataire s'ouvre).
        StringAssert.Contains("RefuserAjoutSansLotLibre(OnAddTabLocataire)", Src("Menu/MenuManager.cs"), "l'onglet « + » aussi");
        StringAssert.Contains("RefuserAjoutSansLotLibre(Ajouter)", Src("Batiment/BatimentSummaryView.cs"), "« + Ajouter » du résumé");
        StringAssert.Contains("LotsPanel.Ouvrir(this, ajouter)", Src("Batiment/BatimentPrefab.cs"));
    }

    /// Retour BM6 (04/11) : un locataire dont le bail n'a pas commencé (remplaçant) se voit : « Arrive le … » dans la liste,
    /// « (à venir) » comme occupant de son lot.
    [Test]
    public void BM6_un_locataire_a_venir_se_voit()
    {
        var r = new Locataire { id = "R", Name = "remplacant", lotBatiment = 2 };
        r.DateDebutBail = DateTime.Today.AddDays(30); r.DateFinBail = DateTime.Today.AddYears(9);
        Assert.That(r.EstAVenir, Is.True);
        Assert.That(LotsBatiment.NomOccupant(r), Is.EqualTo("remplacant (à venir)"));
        var bat = new Batiment { Name = "Immeuble", locataireDuBatiment = new List<Locataire> { r },
                                 lots = new List<LotBatiment> { new LotBatiment { numero = 2, surface = 50f } } };
        Assert.That(LotsBatiment.Libelle(bat, bat.lots[0]), Is.EqualTo("Lot 2 · 50 m² · remplacant (à venir)"));
        r.DateDebutBail = DateTime.Today;
        Assert.That(r.EstAVenir, Is.False, "arrivé aujourd'hui : dans les lieux");
        StringAssert.Contains("Arrive le {arrivee:dd/MM/yyyy}", Src("Locataire/LocataireRowUI.cs"));
        StringAssert.Contains("\"BadgePresence\"", Src("Locataire/LocataireRowUI.cs"), "retour BM7 : sa propre colonne, à côté du loyer");
    }

    // ── Lot 2a (majeurs : l'argent) ──

    [Test]
    public void D8_04_un_separateur_repete_est_un_separateur_de_milliers()
    {
        Assert.That(SaisieNumerique.Parse("2.500.000"), Is.EqualTo(2500000f));
        Assert.That(SaisieNumerique.Parse("2,500,000"), Is.EqualTo(2500000f));
        Assert.That(SaisieNumerique.Parse("1.200,50"), Is.EqualTo(1200.5f).Within(0.001f), "deux sortes : le dernier est la virgule");
        Assert.That(SaisieNumerique.Parse("1,5"), Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(SaisieNumerique.Parse("2 500 000,25"), Is.EqualTo(2500000.25f).Within(0.01f));
    }

    [Test]
    public void D5_03_la_TVA_s_arrondit_comme_dans_le_XML()
    {
        Assert.That(Tva.Montant(1234.45f, 10f), Is.EqualTo(123.45f).Within(0.0001f), "123,445 : au plus loin (avant : 123,44 en float)");
        Assert.That(Tva.Montant(1000f, 20f), Is.EqualTo(200f));
        Assert.That(Tva.Montant(99.9f, 5.5f), Is.EqualTo(5.49f).Within(0.0001f), "5,4945 : 5,49");
        Assert.That(Tva.Cents(0.125f), Is.EqualTo(0.13f).Within(0.0001f));
        Assert.That(Tva.Centimes(123456.78f), Is.EqualTo(123456.78m), "les centimes d'un gros montant restent");
        StringAssert.Contains("Arrondi(float x) => Tva.Centimes(x)", Src("Facturation/FactureEn16931.cs"));
        foreach (var p in new[] { "FactureLoyerPanel.cs", "FactureRegulPanel.cs", "FactureDepotPanel.cs" })
            StringAssert.Contains("Cents(float v) => Tva.Cents(v)", Src("Facturation/" + p), p);
    }

    [Test]
    public void D3_05_l_ajout_d_une_entreprise_annonce_aussi_la_licence()
    {
        Assert.That(RegleAbonnement.TexteAjoutEntreprise(true, false),
            Does.Contain("43 € HT par mois (28 € pour l'entreprise, 15 € pour votre licence)"));
        Assert.That(RegleAbonnement.TexteAjoutEntreprise(true, true), Does.Contain("430 € HT par an (280 € pour l'entreprise, 150 € pour votre licence)"));
        Assert.That(RegleAbonnement.TexteAjoutEntreprise(false, false), Does.Contain("plus ses licences"));
    }

    [Test]
    public void D7a_06_une_date_d_acquisition_avant_1900_n_en_est_pas_une()
    {
        var bat = new Batiment { dateAcquisitionISO = "0001-01-01" };
        bat.historiquesAchat.Add(new AchatFinancement { dateAchat = DateTime.Today.AddYears(-2).ToString("yyyy-MM-dd") });
        Assert.That(BatimentSummaryView.AcquisitionSaisie(bat, out _), Is.False);
        Assert.That(BatimentSummaryView.AnneesDepuisAcquisition(bat), Is.EqualTo(2f).Within(0.01f), "le premier achat, pas l'an 1 (× 2 025)");
        bat.dateAcquisitionISO = "";
        Assert.That(BatimentSummaryView.AnneesDepuisAcquisition(bat), Is.EqualTo(2f).Within(0.01f));
        StringAssert.Contains("acquisitionDate.LireDate(out var acq)", Src("Batiment/BatimentPrefab.cs"), "lue et contrôlée à l'enregistrement");
    }

    [Test]
    public void D7b_02_la_case_des_frais_de_notaire_est_enregistree_avec_l_achat()
    {
        Assert.That(new AchatFinancement().fraisNotaireAuto, Is.False, "achats déjà enregistrés : leurs frais saisis");
        var a = JsonUtility.FromJson<AchatFinancement>("{\"prixAchat\":100000,\"fraisNotaire\":6500}");
        Assert.That(a.fraisNotaireAuto, Is.False);
        StringAssert.Contains("SetIsOnWithoutNotify(d.fraisNotaireAuto)", Src("Achat/AchatFormPanel.cs"));
        StringAssert.Contains("_data.fraisNotaireAuto = toggleFraisAuto", Src("Achat/AchatFormPanel.cs"));
    }

    [Test]
    public void D6_06_le_depot_suit_la_TVA_du_bail()
    {
        var loc = new Locataire { loyerAnnuel = 12000f, periodiciteLoyer = Periodicite.mensuel, tvaLoyer = "oui", tauxTvaLoyer = 10f };
        Assert.That(Tva.PeriodeDepot(loc, true), Is.EqualTo(1100f).Within(0.01f), "au taux du bail (10 %), pas × 1,2");
        Assert.That(Tva.PeriodeDepot(loc, false), Is.EqualTo(1000f).Within(0.01f));
        loc.tvaLoyer = "non";
        Assert.That(Tva.PeriodeDepot(loc, true), Is.EqualTo(1000f).Within(0.01f), "bail exonéré : HT, même case cochée");
        var d = DepartLocataire.Calculer(0f, new[] { new RetenueSortie { libelle = "peinture", ht = 100f, tva = true } }, null, 10f);
        Assert.That(d.tva, Is.EqualTo(10f).Within(0.001f));
        StringAssert.Contains("TVA 10 %", d.lignes[0].Key);
        d = DepartLocataire.Calculer(0f, new[] { new RetenueSortie { libelle = "peinture", ht = 100f, tva = true } }, null, 0f);
        Assert.That(d.tva, Is.EqualTo(0f), "bail exonéré : pas de TVA sur les retenues");
        StringAssert.Contains("Tva.PeriodeDepot(loc, loc.depotSurTTC)", Src("Locataire/LocataireFacturationFields.cs"));
    }

    [Test]
    public void D6_07_la_correction_reprend_les_provisions_de_sa_facture()
    {
        var ligne = new FactureEtat { key = "loyer-2026-P3", statut = "Paye", provisionsConnues = true, provision = 50f };
        var f = new FactureInfo { saved = true, provisionMontant = 80f };
        Assert.That(FactureLoyerPanel.ProvisionProposee(f, 100f, ligne), Is.EqualTo(50f), "celle de CETTE facture");
        ligne.provisionsConnues = false;
        Assert.That(FactureLoyerPanel.ProvisionProposee(f, 100f, ligne), Is.EqualTo(80f), "ligne émise avant : la dernière facture");
        Assert.That(FactureLoyerPanel.ProvisionProposee(f, 100f, null), Is.EqualTo(100f), "hors correction : la fiche");
        StringAssert.Contains("var memo = memos?.Find(", Src("Facturation/FactureLoyerPanel.cs"), "les listes aussi");
    }

    [Test]
    public void D7a_05_un_remplacant_pas_encore_arrive_ne_compte_pas_double()
    {
        var ancien = new Locataire { id = "A", Name = "Ancien", lotBatiment = 2, loyerAnnuel = 10000f };
        ancien.DateDebutBail = DateTime.Today.AddYears(-3); ancien.DateFinBail = DateTime.Today.AddYears(6);
        ancien.dateSortieISO = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd");
        var nouveau = new Locataire { id = "N", Name = "Nouveau", lotBatiment = 2, loyerAnnuel = 11000f };
        nouveau.DateDebutBail = DateTime.Today.AddDays(21); nouveau.DateFinBail = DateTime.Today.AddYears(9);
        var locs = new List<Locataire> { ancien, nouveau };
        Assert.That(BatimentPrefab.EnPlace(locs), Is.EqualTo(new[] { ancien }), "un seul loyer, un seul lot, une seule surface");
        var bat = new Batiment { locataireDuBatiment = locs, lots = new List<LotBatiment> { new LotBatiment { numero = 2, surface = 50f } } };
        Assert.That(LotsBatiment.Occupants(bat, 2).Count, Is.EqualTo(2), "le lot reste occupé : le remplaçant y arrive");
        Assert.That(LotsBatiment.Libres(bat), Is.Empty);
    }

    /// Revue 17 (D1-01) : la tâche du matin aligne les quantités Stripe de chaque groupe abonné sur ses entreprises et licences.
    [Test]
    public void D1_01_la_tache_du_matin_aligne_les_abonnements_Stripe()
    {
        string ts = File.ReadAllText(Path.Combine("Serveur", "fonctions", "emails-abonnement.ts"));
        StringAssert.Contains("async function alignerAbonnements()", ts);
        StringAssert.Contains("if (!corps.groupe) {", ts, "seulement la tâche du matin, pas un e-mail demandé par stripe-webhook");
        StringAssert.Contains("ligne(\"licence\")", ts);
        StringAssert.Contains("ligne(\"entreprise_suivante\")", ts);
        StringAssert.Contains("proration_behavior: \"create_prorations\"", ts, "au prorata, comme « ajuster »");
        StringAssert.Contains("l.quantity !== total", ts, "seulement si une quantité diffère");
    }

    [Test]
    public void D4_03_une_facture_payee_corrigee_a_la_hausse_redevient_due()
    {
        var loc = new Locataire { id = "L" };
        var ligne = new FactureEtat { key = "loyer-2026-P10", type = "Loyer", statut = "Paye", montant = 1000f, pdfPath = "a.pdf",
                                      numero = "2026/10003", echeanceISO = DateTime.Today.AddDays(10).ToString("yyyy-MM-dd") };
        loc.facturesEtat = new List<FactureEtat> { ligne };
        ligne.montant = 1200f;   // la correction (MarquerCorrige) : nouveau montant
        FacturationSuivi.ApresCorrection(loc, ligne.key, 1000f);
        Assert.That(ligne.statut, Is.EqualTo("Envoye"), "l'écart redevient dû");
        FacturationSuivi.Solde(loc, ligne.key, out float total, out float paye);
        Assert.That(paye, Is.EqualTo(1000f), "ce qui avait été réglé, noté en versement");
        Assert.That(FacturationSuivi.DuesDe(loc).Sum(x => x.montant), Is.EqualTo(200f).Within(0.01f), "200 € dans les créances");

        var baisse = new FactureEtat { key = "loyer-2026-P11", type = "Loyer", statut = "Paye", montant = 800f, pdfPath = "b.pdf",
                                       numero = "2026/11003", echeanceISO = DateTime.Today.AddDays(10).ToString("yyyy-MM-dd") };
        loc.facturesEtat.Add(baisse);
        FacturationSuivi.ApresCorrection(loc, baisse.key, 1000f);
        Assert.That(baisse.statut, Is.EqualTo("Envoye"), "à la baisse : le trop-perçu est à rembourser (décision révisée du 04/11, BN17)");
        Assert.That(FacturationSuivi.ARembourser(loc, baisse.key), Is.EqualTo(200f).Within(0.01f), "le trop-perçu se lit");

        var forme = new FactureEtat { key = "loyer-2026-P12", type = "Loyer", statut = "Paye", montant = 1000f, pdfPath = "c.pdf" };
        loc.facturesEtat.Add(forme);
        FacturationSuivi.ApresCorrection(loc, forme.key, 1000f);
        Assert.That(forme.statut, Is.EqualTo("Paye"));
        Assert.That(forme.versements, Is.Empty, "correction de forme : rien ne change");
        foreach (var p in new[] { "FactureLoyerPanel.cs", "FactureRegulPanel.cs", "FactureRefacPanel.cs", "FactureDepotPanel.cs" })
            StringAssert.Contains("FacturationSuivi.ApresCorrection(_loc, key, totalAvantCorrection);", Src("Facturation/" + p), p);
    }

    [Test]
    public void D7a_01_un_depart_repousse_apres_le_dernier_loyer_le_signale_et_bloque_l_annulation()
    {
        var loc = new Locataire { id = "D", Name = "Dupont", loyerAnnuel = 36000f, periodiciteLoyer = Periodicite.mensuel };
        loc.DateDebutBail = new DateTime(2024, 1, 1); loc.DateFinBail = new DateTime(2032, 12, 31);
        loc.dateSortieISO = "2026-12-15";
        float prorata = Loyers.MontantPeriode(loc, 2026, 12);
        Assert.That(prorata, Is.EqualTo(3000f * 15f / 31f).Within(0.02f));
        var dec = new FactureEtat { key = "loyer-2026-P12", type = "Loyer", statut = "Envoye", montant = prorata * 1.2f, loyerHT = prorata,
                                    pdfPath = "dec.pdf", numero = "2026/12001", echeanceISO = "2026-12-01" };
        loc.facturesEtat = new List<FactureEtat> { dec };
        Assert.That(DepartLocataire.LoyerACorriger(loc, dec), Is.False, "facturé au prorata du départ : juste");
        loc.dateSortieISO = "2026-12-31";   // Dupont reste jusqu'au 31/12
        Assert.That(DepartLocataire.LoyerACorriger(loc, dec), Is.True, "la fin de décembre manque : à corriger");
        Assert.That(DepartLocataire.PeutAnnuler(loc), Does.Contain("a été facturé au prorata du départ"), "annuler le départ : refusé");
        Assert.That(loc.dateSortieISO, Is.EqualTo("2026-12-31"), "la fiche est rendue telle quelle");
    }

    /// Revue 17 (D4-04) : après un changement de périodicité, un loyer émis garde son libellé, et la période homonyme de la
    /// nouvelle périodicité ne le « corrige » pas.
    [Test]
    public void D4_04_un_loyer_emis_garde_son_libelle_et_sa_periodicite()
    {
        var loc = new Locataire { id = "P", loyerAnnuel = 12000f, periodiciteLoyer = Periodicite.mensuel };
        loc.DateDebutBail = new DateTime(2025, 1, 1); loc.DateFinBail = new DateTime(2033, 12, 31);
        loc.facturesEtat = new List<FactureEtat> { new FactureEtat { key = "loyer-2026-P3", type = "Loyer", statut = "Envoye",
            libelle = "Loyer Mars 2026", numero = "2026/03001", pdfPath = "mars.pdf", echeanceISO = "2026-03-01", montant = 1200f,
            periodicite = "mensuel" } };
        loc.periodiciteLoyer = Periodicite.trimestriel;   // passage au trimestriel
        var p3 = FacturationSuivi.Lignes(loc, 2026).First(x => x.key == "loyer-2026-P3");
        Assert.That(p3.libelle, Is.EqualTo("Loyer Mars 2026"), "le document imprimé fait foi");
        Assert.That(FactureEmission.Preparer(loc, "loyer-2026-P3", "2026/09001", "trimestriel").Refus, Does.Contain("ancienne périodicité"),
            "le 3e trimestre ne corrige pas la facture de mars");
        Assert.That(FactureEmission.Preparer(loc, "loyer-2026-P3", "2026/03001", "mensuel").Refus, Is.Null, "même périodicité : correction normale");
    }

    /// Revue 17 (D7a-03) : une période qui commence le jour d'une cession prévue, ou après, est facturée au cessionnaire ; une
    /// période d'avant une cession faite, au cédant.
    [Test]
    public void D7a_03_le_titulaire_d_une_periode_apres_la_cession_prevue_est_le_cessionnaire()
    {
        var loc = new Locataire { id = "C", Name = "SARL Ancien", siretNumber = "11111111111111", emailLocataire = "ancien@exemple.fr",
            periodiciteLoyer = Periodicite.mensuel, loyerAnnuel = 12000f };
        loc.DateDebutBail = new DateTime(2025, 1, 1); loc.DateFinBail = new DateTime(2033, 12, 31);
        Cessions.Enregistrer(loc, new DateTime(2026, 12, 1), "SARL Nouveau", "22222222222222", "1 rue Neuve", "nouveau@exemple.fr",
            new DateTime(2026, 10, 20));
        Assert.That(Cessions.TitulaireDe(loc, new DateTime(2026, 12, 1))?[0], Is.EqualTo("SARL Nouveau"));
        Assert.That(Cessions.TitulaireDe(loc, new DateTime(2026, 11, 1)), Is.Null, "novembre : la fiche, donc le cédant");
        var dec = new FactureEtat { key = "loyer-2026-P12", type = "Loyer", statut = "AttenteEnvoi" };
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Loyer", dec), Is.EqualTo("nouveau@exemple.fr"), "l'envoi du 16/11 part au cessionnaire");
        Assert.That(FactureEnvoiAuto.Destinataire(loc, "Loyer"), Is.EqualTo("ancien@exemple.fr"), "sans ligne : la fiche");
        Cessions.Effectuer(loc, new DateTime(2026, 12, 1));   // le jour venu, la fiche passe au cessionnaire
        Assert.That(loc.Name, Is.EqualTo("SARL Nouveau"));
        Assert.That(Cessions.TitulaireDe(loc, new DateTime(2026, 11, 1))?[0], Is.EqualTo("SARL Ancien"), "novembre corrigé : le cédant");
        Assert.That(Cessions.TitulaireDe(loc, new DateTime(2026, 12, 1)), Is.Null, "décembre : la fiche, le cessionnaire");
    }

    /// Retour BN9 (04/11) : D4-03, deux cas oubliés : « Payé » à la main avec un acompte, puis « Partiel » corrigé sous ce qui
    /// a été versé.
    [Test]
    public void BN9_paye_avec_acompte_puis_partiel_corrige_a_la_baisse()
    {
        var loc = new Locataire { id = "Q" };
        var l = new FactureEtat { key = "loyer-2026-P9", type = "Loyer", statut = "Paye", montant = 1000f, pdfPath = "d.pdf",
            versements = new List<Versement> { new Versement { dateISO = "2026-09-05", montant = 600f } } };
        loc.facturesEtat = new List<FactureEtat> { l };
        l.montant = 1120f;   // corrigée à la hausse
        FacturationSuivi.ApresCorrection(loc, l.key, 1000f);
        FacturationSuivi.Solde(loc, l.key, out float total, out float paye);
        Assert.That(total - paye, Is.EqualTo(120f).Within(0.01f), "l'écart seulement (avant : 520, l'acompte seul comptait)");
        Assert.That(l.statut, Is.EqualTo("Envoye"));
        l.montant = 880f;    // puis à la baisse, sous ce qui a été versé
        FacturationSuivi.ApresCorrection(loc, l.key, 1120f);
        Assert.That(l.statut, Is.EqualTo("Envoye"), "trop-perçu : à rembourser avant « Payé » (BN17)");
        Assert.That(FacturationSuivi.EtatAffiche(loc, l), Is.EqualTo(FacturationSuivi.Etat.ARembourser), "avant : restait « Partiel »");
        Assert.That(FacturationSuivi.ARembourser(loc, l.key), Is.EqualTo(120f).Within(0.01f));
    }

    /// Retour BN2 (04/11) : les champs de montant des formulaires acceptent la virgule.
    [Test]
    public void BN2_un_champ_de_montant_accepte_la_virgule()
    {
        Assert.That(SaisieNumerique.CaractereMontant("", 0, ','), Is.EqualTo(','));
        Assert.That(SaisieNumerique.CaractereMontant("12", 2, '.'), Is.EqualTo('.'));
        Assert.That(SaisieNumerique.CaractereMontant("", 0, '-'), Is.EqualTo('-'));
        Assert.That(SaisieNumerique.CaractereMontant("1", 1, '-'), Is.EqualTo('\0'), "le signe seulement en tête");
        Assert.That(SaisieNumerique.CaractereMontant("", 0, 'a'), Is.EqualTo('\0'));
        Assert.That(SaisieNumerique.Parse("1234,45"), Is.EqualTo(1234.45f).Within(0.001f));
        foreach (var s in new[] { "Facturation/FactureLoyerPanel.cs", "Facturation/FactureDepotPanel.cs", "Facturation/FactureRegulPanel.cs",
                                  "Facturation/FactureRefacPanel.cs", "Facturation/ChargePanel.cs", "RevisionLoyer/RevisionPanel.cs",
                                  "RevisionLoyer/PaliersPanel.cs", "Batiment/LotsPanel.cs" })
            StringAssert.DoesNotContain("ContentType.DecimalNumber", Src(s), s);
    }

    /// Retour BN11 (décision du 04/11) : mois facturés cochés d'office, et autant de mois que de périodes.
    [Test]
    public void BN11_mois_factures_d_office_et_controles()
    {
        CollectionAssert.AreEqual(new[] { 1, 4, 7, 10 }, RevisionPanel.MoisStandard(Periodicite.trimestriel));
        CollectionAssert.AreEqual(new[] { 1, 7 }, RevisionPanel.MoisStandard(Periodicite.BiAnnuel));
        CollectionAssert.AreEqual(new[] { 1 }, RevisionPanel.MoisStandard(Periodicite.Annuel));
        Assert.That(RevisionPanel.MoisStandard(Periodicite.mensuel), Is.Empty);
        Assert.That(RevisionPanel.RefusMois(Periodicite.trimestriel, 2), Does.Contain("cochez 4 mois"));
        Assert.That(RevisionPanel.RefusMois(Periodicite.trimestriel, 4), Is.Null);
        Assert.That(RevisionPanel.RefusMois(Periodicite.BiAnnuel, 1), Does.Contain("cochez 2 mois"));
        Assert.That(RevisionPanel.RefusMois(Periodicite.mensuel, 0), Is.Null);
    }

    /// Retour BN12 (04/11) : la cession prévue se voit sur le locataire, et un cessionnaire sans SIRET n'hérite pas de celui du
    /// cédant.
    [Test]
    public void BN12_cession_signalee_et_cessionnaire_sans_siret()
    {
        var loc = new Locataire { id = "C2", Name = "SARL Ancien", siretNumber = "00000000200000" };
        Assert.That(Cessions.Etiquette(loc), Is.Null);
        Assert.That(Cessions.TexteEntete(loc), Is.Null);
        Cessions.Enregistrer(loc, new DateTime(2026, 12, 1), "cession", "", "", "nouveau@exemple.fr", new DateTime(2026, 11, 4));
        Assert.That(Cessions.Etiquette(loc), Is.EqualTo("Cession le 01/12/2026"));
        Assert.That(Cessions.TexteEntete(loc), Is.EqualTo("Cession le 01/12/2026 → cession"), "la pastille de l'en-tête (BN15)");
        Assert.That(SocieteLocataire.Siren(FactureElectronique.SiretAcheteur(loc, FactureLoyerPanel.SiretPourPlateforme(true, ""))), Is.Empty,
            "le cessionnaire sans SIRET n'a pas celui du cédant");
        Assert.That(FactureElectronique.SiretAcheteur(loc, FactureLoyerPanel.SiretPourPlateforme(false, "")), Is.EqualTo("00000000200000"),
            "hors cession, un champ vide reprend la fiche, comme avant");
        StringAssert.DoesNotContain("UndoToast.Instance?.ShowInfo($\"Cession du bail", Src("Facturation/FactureLoyerPanel.cs"),
            "plus de message du bas : la ligne sous « Destinataire »");
    }

    /// Retour BN9 (04/11) : « Encaissée » attend quelques minutes après la transmission (Super PDP n'a pas encore lu la facture).
    [Test]
    public void BN9_encaissee_attend_que_Super_PDP_ait_lu_la_facture()
    {
        var t0 = new DateTime(2026, 11, 4, 10, 0, 0);
        FactureElectronique.NoterTransmission("test-1778163", t0);
        Assert.That(FactureElectronique.TransmiseALinstant("test-1778163", t0.AddSeconds(10)), Is.True, "tout de suite : on attend");
        Assert.That(FactureElectronique.TransmiseALinstant("test-1778163", t0.AddMinutes(6)), Is.False, "après quelques minutes : déclaré");
        Assert.That(FactureElectronique.TransmiseALinstant("autre", t0), Is.False, "facture transmise une autre fois (lancement précédent)");
        StringAssert.Contains("&& !TransmiseALinstant(r.plateformeId, DateTime.Now)", Src("Facturation/FactureElectronique.cs"));
    }

    /// Retour BN17 (décisions du 04/11) : ce que nous devons au locataire (trop-perçu, avoir) est « À rembourser » : jamais
    /// « Impayé », pas d'« Encaissée », « Payé » une fois le remboursement enregistré.
    [Test]
    public void BN17_trop_percu_et_avoir_a_rembourser_puis_payes()
    {
        var loc = new Locataire { id = "R" };
        var echue = DateTime.Today.AddDays(-60).ToString("yyyy-MM-dd");
        var trop = new FactureEtat { key = "loyer-2026-P7", type = "Loyer", statut = "Envoye", montant = 880f, pdfPath = "t.pdf", echeanceISO = echue,
            versements = new List<Versement> { new Versement { dateISO = "2026-07-03", montant = 1000f } } };
        var avoir = new FactureEtat { key = "regul-2025", type = "Regul", statut = "Envoye", montant = -297.6f, pdfPath = "a.pdf", echeanceISO = echue };
        loc.facturesEtat = new List<FactureEtat> { trop, avoir };
        Assert.That(FacturationSuivi.EtatDe(trop, loc), Is.EqualTo(FacturationSuivi.Etat.Envoye), "rien à réclamer : jamais « Impayé »");
        Assert.That(FacturationSuivi.EtatAffiche(loc, trop), Is.EqualTo(FacturationSuivi.Etat.ARembourser));
        Assert.That(FacturationSuivi.EtatAffiche(loc, avoir), Is.EqualTo(FacturationSuivi.Etat.ARembourser), "un avoir aussi");
        Assert.That(FacturationSuivi.ARembourser(loc, avoir.key), Is.EqualTo(297.6f).Within(0.01f));
        Assert.That(FacturationSuivi.EnregistrerRemboursement(loc, trop, 150f, new DateTime(2026, 11, 10)), Does.Contain("dépasse"));
        Assert.That(FacturationSuivi.EnregistrerRemboursement(loc, trop, 120f, new DateTime(2026, 11, 10)), Is.Null);
        Assert.That(trop.statut, Is.EqualTo("Paye"), "tout rendu : payée");
        var enc = FactureElectronique.Encaissement(loc, trop);
        Assert.That(enc.dateISO, Is.EqualTo("2026-07-03"), "la date du paiement, pas celle du remboursement");
        Assert.That(enc.montant, Is.EqualTo(880f).Within(0.01f));
        Assert.That(FacturationSuivi.EnregistrerRemboursement(loc, avoir, 100f, new DateTime(2026, 11, 10)), Is.Null);
        Assert.That(avoir.statut, Is.EqualTo("Envoye"), "remboursé en partie");
        Assert.That(FacturationSuivi.ARembourser(loc, avoir.key), Is.EqualTo(197.6f).Within(0.01f));
        Assert.That(FacturationSuivi.EnregistrerRemboursement(loc, avoir, 197.6f, new DateTime(2026, 11, 12)), Is.Null);
        Assert.That(avoir.statut, Is.EqualTo("Paye"));
        var payeMain = new FactureEtat { key = "refac-x", type = "Refac", statut = "Paye", montant = -50f, pdfPath = "p.pdf" };
        loc.facturesEtat.Add(payeMain);
        Assert.That(FacturationSuivi.ARembourser(loc, payeMain.key), Is.EqualTo(0f), "un avoir marqué « Payé » à la main est remboursé");
        Assert.That(FacturationSuivi.StatutApresVersements("Paye", 880f, 1000f, 1000f), Is.EqualTo("Envoye"), "trop-perçu : plus « Payé »");
        StringAssert.Contains("&& FacturationSuivi.ARembourser(loc, r.key) <= 0.005f)", Src("Facturation/FactureElectronique.cs"),
            "pas d'« Encaissée » avant le remboursement");
    }

    // ── D6-03 : mentions obligatoires ──

    [Test]
    public void D6_03_le_PDF_dit_toujours_l_escompte_et_un_professionnel_garde_les_penalites()
    {
        StringAssert.Contains(FactureEn16931.SansEscompte, FacturePdfService.BuildHtml(new FacturePdfService.Data { ttc = 240f, retard = false }));
        StringAssert.Contains(FactureEn16931.SansEscompte, FacturePdfService.BuildRegulHtml(new FacturePdfService.RegulData { ttc = 240f, retard = false }));
        // Demande du 04/11 : la tournure se règle dans Réglages › Facturation ; vide, la phrase d'usine.
        const string tournure = "Aucun escompte n'est accordé en cas de paiement anticipé.";
        string html = FacturePdfService.BuildHtml(new FacturePdfService.Data { ttc = 240f, retard = false, escompte = tournure });
        StringAssert.Contains(tournure, html);
        StringAssert.DoesNotContain(FactureEn16931.SansEscompte, html);
        StringAssert.Contains(tournure, FacturePdfService.BuildRegulHtml(new FacturePdfService.RegulData { ttc = 240f, escompte = tournure }));
        Assert.That(FactureEn16931.Escompte(" "), Is.EqualTo(FactureEn16931.SansEscompte), "vide : la phrase d'usine, jamais rien");
        StringAssert.Contains("{\"note\":\"" + tournure + "\",\"subject_code\":\"AAB\"}",
            FactureEn16931.Json(new FactureEn16931.Entree { numero = "F-1", dateISO = "2026-11-04", echeanceISO = "2026-11-04", escompte = tournure }));
        Assert.That(new ReglageData().phraseEscompte, Is.EqualTo(FactureEn16931.SansEscompte), "Réglages déjà enregistrés : la phrase d'usine");
        foreach (var p in new[] { "FactureLoyerPanel.cs", "FactureRegulPanel.cs", "FactureRefacPanel.cs", "FactureDepotPanel.cs" })
            StringAssert.Contains("escompte = R.phraseEscompte", Src("Facturation/" + p), p);
        Assert.That(FactureElectronique.Professionnel(new Locataire { siretNumber = "55203253400646" }, ""), Is.True);
        Assert.That(FactureElectronique.Professionnel(new Locataire(), ""), Is.False, "particulier : case libre");
        Assert.That(FactureElectronique.Professionnel(new Locataire(), "552 032 534 00646"), Is.True, "SIRET tapé dans le formulaire");
        foreach (var p in new[] { "FactureRegulPanel.cs", "FactureRefacPanel.cs", "FactureDepotPanel.cs" })
            StringAssert.Contains("FactureElectronique.Professionnel(_loc, _siret.text)", Src("Facturation/" + p), p);
        // Retour BN12 : le loyer passe par SiretSaisi() (le SIRET du cessionnaire, sans retomber sur celui du cédant).
        StringAssert.Contains("FactureElectronique.Professionnel(_loc, SiretSaisi())", Src("Facturation/FactureLoyerPanel.cs"));
    }

    // ── D8-01 : fermer hors ligne ──

    [Test]
    public void D8_01_la_fermeture_previent_des_fiches_en_attente_de_connexion()
    {
        Assert.That(FermetureGuard.Decision(0, false, 1), Is.EqualTo(FermetureGuard.Choix.Prevenir));
        Assert.That(FermetureGuard.Decision(2, true, 0), Is.EqualTo(FermetureGuard.Choix.Prevenir));
        Assert.That(FermetureGuard.Decision(0, true, 0), Is.EqualTo(FermetureGuard.Choix.Attendre));
        Assert.That(FermetureGuard.Decision(0, false, 0), Is.EqualTo(FermetureGuard.Choix.Quitter));
        var b = new Batiment { Name = "Rivoli" };
        EcritureCloud.MettreEnAttente(b);
        try
        {
            Assert.That(EcritureCloud.NomsEnAttente(), Does.Contain("Rivoli"));
            var (titre, texte) = FermetureGuard.Avertissement(new List<string>(), EcritureCloud.NomsEnAttente());
            Assert.That(titre, Is.EqualTo("Modifications pas encore envoyées"));
            StringAssert.Contains("• Rivoli", texte);
            StringAssert.Contains("EcritureCloud.NomsEnAttente()", Src("General/QuitButton.cs"), "la croix Quitter aussi");
        }
        finally
        {
            var f = typeof(EcritureCloud).GetField("_enAttente", BindingFlags.NonPublic | BindingFlags.Static);
            ((HashSet<Batiment>)f.GetValue(null)).Remove(b);
        }
    }
}
