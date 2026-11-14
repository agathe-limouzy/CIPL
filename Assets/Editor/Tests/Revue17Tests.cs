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
        Assert.That(DonneesCloud.Modele + ":" + Hash(e), Is.EqualTo("11:973caba90b7c"),   // 11 : ReglageData.mentionTvaParDefaut, FactureEtat.payeLeISO (coutures) ; 10 : Locataire.historiquePeriodicites (lot 14) ; 9 : FactureEtat.encaisseeAAnnulerId (lot 9) ; 8 : destinataire / acheteur (lot 6), retireApresEnvoi / factureCitee (lot 6b)
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
        StringAssert.Contains("proration_behavior: \"always_invoice\"", ts, "au prorata facturé tout de suite, comme « ajuster » (D3-13)");
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

    /// Revue 17, lot 2b (04/11) : abonnement et comptes, côté serveur (script etape17b, fonctions Stripe).
    [Test]
    public void Lot2b_le_serveur_porte_les_corrections()
    {
        string sql = File.ReadAllText(Path.Combine("Serveur", "etape17b_supabase.sql"));
        string import = Fonction(sql, "creer_entreprise_import");
        StringAssert.Contains("auth.jwt() -> 'amr'", import, "D1-04 : adresse prouvée par le code reçu");
        StringAssert.Contains("adresse non prouvée par le code reçu", import);
        StringAssert.Contains("update public.groupes set titulaire = auth.uid() where id = g and titulaire = p_user;",
            Fonction(sql, "retirer_membre"), "D3-04 : le titre passe à l'admin qui retire");
        string webhook = File.ReadAllText(Path.Combine("Serveur", "fonctions", "stripe-webhook.ts"));
        StringAssert.Contains("if (r && avant !== sub.id && sub.status !== \"canceled\") await repartir(sub);", webhook, "D3-02");
        StringAssert.DoesNotContain("evt.type === \"customer.subscription.created\" && !avant", webhook, "D3-02 : plus seulement au « created »");
        string abo = File.ReadAllText(Path.Combine("Serveur", "fonctions", "abonnement-stripe.ts"));
        StringAssert.Contains("billing_address_collection: \"required\"", abo, "D3-06 : adresse de facturation");
        StringAssert.Contains("customer_update: { address: \"auto\", name: \"auto\" }", abo, "D3-06 : enregistrée sur le client");
        StringAssert.Contains("tax_id_collection: { enabled: true }", abo, "D3-06 : n° de TVA facultatif");
    }

    // ── D4-02 : une facture partie ne se dé-émet plus ──

    /// Revue 17 (D4-02, décision du 05/11) : une facture envoyée, impayée ou payée ne redevient ni « À faire » ni
    /// « Automatique » ; un « Payé » cliqué par erreur se défait par « Envoyé ».
    [Test]
    public void D4_02_une_facture_partie_ne_redevient_pas_a_faire()
    {
        var loc = new Locataire { id = "D42a" };
        var oct = new FactureEtat { key = "loyer-2026-P10", type = "Loyer", statut = "Envoye", libelle = "Loyer Octobre 2026", numero = "2026/10003",
            pdfPath = "oct.pdf", montant = 1200f, echeanceISO = DateTime.Today.AddDays(10).ToString("yyyy-MM-dd") };
        loc.facturesEtat = new List<FactureEtat> { oct };
        Assert.That(FacturationSuivi.Partie(loc, oct.key), Is.True);
        FacturationSuivi.SetStatut(loc, oct, "");
        FacturationSuivi.SetStatut(loc, oct, "AFaire");
        Assert.That(oct.statut, Is.EqualTo("Envoye"), "avant : « À faire », hors des créances, et « Refaire » prenait un second numéro");
        Assert.That(FacturationSuivi.DuesDe(loc).Select(x => x.key), Does.Contain(oct.key), "elle reste dans les créances");
        Assert.That(FactureEmission.Preparer(loc, oct.key, "2026/10004").NumeroFacture, Is.EqualTo("2026/10003 corrigée(1)"));
        FacturationSuivi.SetStatut(loc, oct, "Paye");
        FacturationSuivi.SetStatut(loc, oct, "");
        Assert.That(oct.statut, Is.EqualTo("Paye"), "payée : pareil");
        FacturationSuivi.SetStatut(loc, oct, "Envoye");
        Assert.That(oct.statut, Is.EqualTo("Envoye"), "« Payé » par erreur : « Envoyé » le défait");

        var force = new FactureEtat { key = "loyer-2026-P9", type = "Loyer", statut = "Paye", echeanceISO = "2026-09-01" };
        loc.facturesEtat.Add(force);
        Assert.That(FacturationSuivi.Partie(loc, force.key), Is.False, "« Payé » forcé sur une ligne jamais émise (ni numéro ni PDF)");
        FacturationSuivi.SetStatut(loc, force, "");
        Assert.That(force.statut, Is.Empty, "elle revient en automatique");

        Assert.That(FacturationSuivi.AvecBoutonAuto(loc, oct), Is.False, "pas de bouton « Auto » sur une facture partie (même aide que SetStatut)");
        string menu = Src("Facturation/LocataireSuiviInline.cs");
        StringAssert.Contains("if (DepartLocataire.Retirable(_loc, ligne))", menu, "l'exception du départ : « Retirer du suivi… »");
        Assert.That(DepartLocataire.Retirable(loc, oct), Is.False, "pas de départ : rien à retirer");
    }

    /// Revue 17 (D4-02) : une facture seulement préparée garde « Automatique » ; refaite, elle reprend son numéro sans en
    /// consommer un autre, et le refus de périodicité (D4-04) ne la vise pas : elle n'a jamais été envoyée.
    [Test]
    public void D4_02_une_facture_preparee_remise_en_automatique_garde_son_numero()
    {
        var loc = new Locataire { id = "D42b", factureSeq = 5, factureLoyer = new FactureInfo(), periodiciteLoyer = Periodicite.mensuel };
        var nov = new FactureEtat { key = "loyer-2026-P11", type = "Loyer", statut = "AttenteEnvoi", libelle = "Loyer Novembre 2026",
            numero = "2026/11004", pdfPath = "nov.pdf", montant = 1200f, echeanceISO = "2026-11-01", periodicite = "mensuel" };
        loc.facturesEtat = new List<FactureEtat> { nov };
        Assert.That(FacturationSuivi.Partie(loc, nov.key), Is.False, "préparée : pas partie");
        FacturationSuivi.SetStatut(loc, nov, "");
        Assert.That(nov.statut, Is.Empty, "« Automatique » reste");
        Assert.That(FactureEmission.Preparer(loc, "loyer-2026-P12", "2026/11004").Refus, Does.Contain("déjà"), "son numéro lui reste");
        var d = FactureEmission.Preparer(loc, nov.key, "2026/11009", "mensuel");
        Assert.That(d.Reecriture, Is.True);
        Assert.That(d.Correction, Is.False);
        Assert.That(d.NumeroFacture, Is.EqualTo("2026/11004"), "avant : 2026/11009, et 2026/11004 manquait à la numérotation");
        FactureEmission.Enregistrer(loc, nov.key, "Loyer", d, "Loyer Novembre 2026", "2026-11-01", "nov.pdf", 1200f, null, loc.factureLoyer, "Facture", false);
        Assert.That(loc.factureSeq, Is.EqualTo(5), "aucun numéro consommé");
        Assert.That(nov.numero, Is.EqualTo("2026/11004"));
        Assert.That(nov.statut, Is.EqualTo("AttenteEnvoi"));

        var mars = new FactureEtat { key = "loyer-2026-P3", type = "Loyer", statut = "", libelle = "Loyer Mars 2026", numero = "2026/03002",
            pdfPath = "mars.pdf", montant = 1200f, echeanceISO = "2026-03-01", periodicite = "mensuel" };   // préparée au mois, remise
        loc.facturesEtat.Add(mars);
        loc.periodiciteLoyer = Periodicite.trimestriel;
        var t3 = FactureEmission.Preparer(loc, mars.key, "2026/07009", "trimestriel");
        Assert.That(t3.Refus, Is.Null, "jamais envoyée : pas de refus de périodicité");
        Assert.That(t3.NumeroFacture, Is.EqualTo("2026/03002"));
    }

    /// Revue 17 (D4-02, décision du 05/11) : exception : un loyer envoyé pour une période entièrement après un départ saisi ensuite
    /// repasse « Automatique » (« Hors bail ») et ne bloque plus l'archivage ; l'avoir qui l'annule peut le citer.
    [Test]
    public void D4_02_un_loyer_envoye_apres_le_depart_repasse_automatique()
    {
        var loc = new Locataire { id = "D42c", Name = "Dupont", loyerAnnuel = 12000f, periodiciteLoyer = Periodicite.mensuel };
        loc.DateDebutBail = new DateTime(2024, 1, 1); loc.DateFinBail = new DateTime(2032, 12, 31);
        var oct = new FactureEtat { key = "loyer-2026-P10", type = "Loyer", statut = "Envoye", libelle = "Loyer Octobre 2026", numero = "2026/10003",
            pdfPath = "oct.pdf", montant = 1200f, loyerHT = 1000f, echeanceISO = "2026-10-01", periodicite = "mensuel" };
        var nov = new FactureEtat { key = "loyer-2026-P11", type = "Loyer", statut = "Envoye", libelle = "Loyer Novembre 2026", numero = "2026/11003",
            pdfPath = "nov.pdf", montant = 1200f, loyerHT = 1000f, echeanceISO = "2026-11-01", periodicite = "mensuel" };
        loc.facturesEtat = new List<FactureEtat> { oct, nov };
        loc.dateSortieISO = "2026-10-15";   // départ saisi après l'envoi du loyer de novembre
        Assert.That(DepartLocataire.LoyerACorriger(loc, nov), Is.True, "pré-requis : l'archivage est bloqué par lui");
        Assert.That(DepartLocataire.PeriodeApresDepart(loc, nov), Is.True);
        Assert.That(DepartLocataire.PeriodeApresDepart(loc, oct), Is.False, "octobre, au prorata du départ : il se corrige");
        FacturationSuivi.SetStatut(loc, oct, "");
        Assert.That(oct.statut, Is.EqualTo("Envoye"));
        FacturationSuivi.SetStatut(loc, nov, "AFaire");
        Assert.That(nov.statut, Is.EqualTo("Envoye"), "« À faire » : jamais");
        FacturationSuivi.SetStatut(loc, nov, "");
        Assert.That(nov.statut, Is.Empty, "« Automatique » : la seule sortie (« Corriger » à 0 € est refusé)");
        Assert.That(FacturationSuivi.Lignes(loc, 2026).First(x => x.key == nov.key).statut, Is.EqualTo("HorsBail"));
        Assert.That(DepartLocataire.RaisonsNonArchivable(loc), Has.None.Contains("Loyer Novembre 2026"));
        FacturationSuivi.FacturesOrigine(loc, out _, out var ids);
        Assert.That(ids, Does.Contain("2026/11003"), "Refacturation, « Avoir sur la facture n° 2026/11003 »");

        var mars = new FactureEtat { key = "loyer-2026-P3", type = "Loyer", statut = "Envoye", libelle = "Loyer Mars 2026", numero = "2026/03001",
            pdfPath = "mars.pdf", montant = 1200f, echeanceISO = "2026-03-01", periodicite = "mensuel" };
        var trim = new Locataire { id = "D42d", periodiciteLoyer = Periodicite.trimestriel, dateSortieISO = "2026-06-30",
            facturesEtat = new List<FactureEtat> { mars } };
        Assert.That(DepartLocataire.PeriodeApresDepart(trim, mars), Is.False, "mars, émis au mois : pas le 3e trimestre (D4-04)");
        FacturationSuivi.SetStatut(trim, mars, "");
        Assert.That(mars.statut, Is.EqualTo("Envoye"));
    }

    /// Revue 17 (D4-02, relecture du 05/11) : un loyer dont la période commence après le départ ne se corrige pas (« Corriger » à
    /// 0 € est refusé) : le bandeau et l'archivage disent sa sortie (son état › « Automatique »), une seule fois, et aussi pour un
    /// loyer de l'année suivante, au lieu de « non payée ». Avant : « corrigez-le ».
    [Test]
    public void D4_02_un_loyer_apres_le_depart_dit_sa_sortie_au_lieu_de_corrigez_le()
    {
        Locataire Dupont(string sortie, params FactureEtat[] lignes)
        {
            var l = new Locataire { id = "D42e", Name = "Dupont", loyerAnnuel = 12000f, periodiciteLoyer = Periodicite.mensuel, dateSortieISO = sortie,
                facturesEtat = lignes.ToList() };
            l.DateDebutBail = new DateTime(2024, 1, 1); l.DateFinBail = new DateTime(2032, 12, 31);
            return l;
        }
        FactureEtat Loyer(string key, string libelle, string numero, string echeance) => new FactureEtat { key = key, type = "Loyer",
            statut = "Envoye", libelle = libelle, numero = numero, pdfPath = numero.Replace('/', '-') + ".pdf", montant = 1200f, loyerHT = 1000f,
            echeanceISO = echeance, periodicite = "mensuel" };
        const string sortie = "commence après le départ : rien n'y est dû ; dans le suivi, cliquez son état puis « Retirer du suivi »";
        var dec = Loyer("loyer-2026-P12", "Loyer Décembre 2026", "2026/12003", "2026-12-01");

        var loc = Dupont("2026-11-30", dec);
        Assert.That(DepartLocataire.Consigne(loc, DateTime.Today), Is.EqualTo($"Étape 2 — Dernier loyer : « Loyer Décembre 2026 » {sortie}."),
            "avant : « ne correspond plus au départ : corrigez-le »");
        Assert.That(DepartLocataire.RaisonsNonArchivable(loc).Where(x => x.Contains("Loyer Décembre 2026")),
            Is.EqualTo(new[] { $"« Loyer Décembre 2026 » {sortie}" }), "une seule raison : ni « à corriger », ni « non payée »");

        loc = Dupont("2026-11-15", Loyer("loyer-2026-P11", "Loyer Novembre 2026", "2026/11003", "2026-11-01"), dec);
        Assert.That(DepartLocataire.RaisonsNonArchivable(loc), Has.Some.EqualTo("« Loyer Novembre 2026 » à corriger : il ne correspond plus au départ"),
            "novembre, au prorata du départ : il se corrige");
        StringAssert.EndsWith("« Loyer Novembre 2026 » ne correspond plus au départ : corrigez-le.", DepartLocataire.Consigne(loc, DateTime.Today));

        loc = Dupont("2026-12-31", Loyer("loyer-2027-P1", "Loyer Janvier 2027", "2027/01001", "2027-01-01"));   // parti avant le départ au 31/12
        var r = DepartLocataire.RaisonsNonArchivable(loc);
        Assert.That(r, Has.Some.EqualTo($"« Loyer Janvier 2027 » {sortie}"));
        Assert.That(r, Has.None.Contains("non payée"), "avant : « facture n° 2027/01001 « Loyer Janvier 2027 » non payée »");
    }

    /// Revue 17 (D4-02, relecture du 05/11) : la confirmation « Retirer ce loyer du suivi ? » suit ce qui a été réglé. Impayé :
    /// l'avoir annule la dette, rien à rendre (avant : le même rappel partout ; repris coché au décompte de sortie, l'avoir rendait
    /// un loyer jamais payé). Payé en partie : seul le versé se rend. Payé : l'avoir, puis le remboursement. Et si le départ est
    /// annulé ou repoussé : remettre son état ; « Refaire » est refusé (lot 6b, décision du 09/11 : même numéro, PDF reçu écrasé).
    [Test]
    public void D4_02_le_rappel_de_l_avoir_suit_ce_qui_a_ete_regle()
    {
        string Eur(float v) => v.ToString("N2", FacturePdfService.FrCulture) + " €";   // séparateur de milliers insécable
        var dec = new FactureEtat { key = "loyer-2026-P12", type = "Loyer", statut = "Envoye", libelle = "Loyer Décembre 2026", numero = "2026/12003",
            pdfPath = "dec.pdf", montant = 1200f, loyerHT = 1000f, echeanceISO = "2026-12-01", periodicite = "mensuel" };
        var loc = new Locataire { id = "D42f", Name = "Dupont", loyerAnnuel = 12000f, periodiciteLoyer = Periodicite.mensuel, dateSortieISO = "2026-11-30",
            facturesEtat = new List<FactureEtat> { dec } };

        string impaye = DepartLocataire.ConfirmationRetrait(loc, dec);
        StringAssert.Contains("« Loyer Décembre 2026 » (n° 2026/12003) est déjà parti chez le locataire", impaye);
        StringAssert.Contains("(dernier jour de location : 30/11/2026)", impaye);
        StringAssert.Contains($"Il ne l'a pas payé : envoyez-lui un avoir de {Eur(1200f)} (Refacturation, « Avoir sur la facture n° 2026/12003 » : "
            + "choisissez-la, c'est elle qui dit ce qu'il y a à rendre), qui l'annule ; il n'y a rien à lui rendre : l'avoir restera « Envoyé » "
            + "et ne sera pas repris au décompte de sortie.", impaye);
        StringAssert.Contains("cliquez son état et remettez-le « Envoyé » : « Refaire » sera refusé, il garde son numéro", impaye);

        dec.versements.Add(new Versement { dateISO = "2026-11-20", montant = 400f });
        string partiel = DepartLocataire.ConfirmationRetrait(loc, dec);
        StringAssert.Contains($"Il en a payé {Eur(400f)} : envoyez-lui un avoir de {Eur(1200f)}", partiel);
        StringAssert.Contains($"l'avoir, qui cite cette facture, ne rendra que {Eur(400f)} (« Remboursé… » sur l'avoir, ou au décompte de sortie).", partiel);

        dec.versements.Clear(); dec.statut = "Paye";   // « Payé » à la main
        string paye = DepartLocataire.ConfirmationRetrait(loc, dec);
        StringAssert.Contains($"Il l'a payé : envoyez-lui un avoir de {Eur(1200f)}", paye);
        StringAssert.Contains("puis remboursez-le (« Remboursé… » sur l'avoir, ou au décompte de sortie).", paye);
        StringAssert.Contains("cliquez son état et remettez-le « Payé » : « Refaire » sera refusé", paye);
        StringAssert.DoesNotContain("rien à lui rendre", paye);

        StringAssert.Contains("DepartLocataire.ConfirmationRetrait(_loc, ligne)", Src("Facturation/LocataireSuiviInline.cs"), "le menu montre ce texte");
    }

    // ── D6-04 : « Facturer le loyer » ouvre la période à envoyer ──

    /// Revue 17 (D6-04, décision du 05/11) : sans ligne du suivi, le formulaire du loyer ouvre la première période, à partir de celle
    /// en cours, dont le loyer n'est pas parti (avant : la dernière enregistrée, que « Sauvegarder et envoyer » corrigeait sans le
    /// dire) ; rien à envoyer : la dernière facturable. L'alerte porte la période dont elle parle, et « (correction) » suit la règle
    /// de Preparer.
    [Test]
    public void D6_04_facturer_le_loyer_ouvre_la_periode_a_envoyer_pas_la_derniere_emise()
    {
        var auj = new DateTime(2026, 11, 16);
        Locataire Dupont(Periodicite p, string sortie = null) => new Locataire { id = "D64", Name = "Dupont", typeRevision = TypeRevision.Aucune,
            loyerAnnuel = 12000f, loyerDepart = 12000f, periodiciteLoyer = p, jourDemandeLoyer = 1, dateDebutBailISO = "2025-01-01",
            dateFinBailISO = "2033-12-31", depotInitialise = true, delaisPropres = true, envoiAvantJours = 15, dateSortieISO = sortie };
        FactureEtat Loyer(string key, string statut, string echeance, string pdf = "l.pdf") => new FactureEtat { key = key, type = "Loyer",
            statut = statut, numero = pdf == null ? null : "2026/" + key.Substring(key.LastIndexOf('P')), pdfPath = pdf, montant = 1200f,
            loyerHT = 1000f, echeanceISO = echeance };

        var loc = Dupont(Periodicite.mensuel);
        Assert.That(FactureLoyerPanel.PeriodeAOuvrir(loc, auj), Is.EqualTo((2026, 11)), "rien d'envoyé : la période en cours");
        var nov = Loyer("loyer-2026-P11", "Envoye", "2026-11-01");
        loc.facturesEtat.Add(nov);
        Assert.That(FactureLoyerPanel.PeriodeAOuvrir(loc, auj), Is.EqualTo((2026, 12)), "novembre parti : décembre (avant : novembre, corrigé)");
        Assert.That(FactureEmission.SeraUneCorrection(loc, nov.key), Is.True, "novembre choisi à la main : « (correction) »");
        Assert.That(FactureEmission.SeraUneCorrection(loc, "loyer-2026-P12"), Is.False);
        var alerte = FacturationAlertes.Pour(loc, auj).Single(a => a.type == FacturationAlertes.AlerteType.Loyer);
        Assert.That(alerte.cle, Is.EqualTo("loyer-2026-P12"), "l'alerte ouvre la période dont elle parle");

        var dec = Loyer("loyer-2026-P12", "AttenteEnvoi", "2026-12-01");
        loc.facturesEtat.Add(dec);
        Assert.That(FactureLoyerPanel.PeriodeAOuvrir(loc, auj), Is.EqualTo((2026, 12)), "préparé, pas envoyé : proposé");
        Assert.That(FactureEmission.SeraUneCorrection(loc, dec.key), Is.False, "il se remplace, sans « (correction) »");
        dec.statut = "Envoye";
        Assert.That(FactureLoyerPanel.PeriodeAOuvrir(loc, auj), Is.EqualTo((2027, 1)), "décembre parti aussi : janvier");

        nov.pdfPath = null; nov.numero = null; nov.statut = "Paye";   // « Payé » forcé par la pastille, sans facture de CIPL
        Assert.That(FactureEmission.SeraUneCorrection(loc, nov.key), Is.False, "Preparer en émet une neuve : pas de « (correction) »");
        Assert.That(FactureLoyerPanel.PeriodeAOuvrir(loc, auj), Is.EqualTo((2027, 1)), "réglé : pas à envoyer");

        var trim = Dupont(Periodicite.trimestriel);
        trim.facturesEtat.Add(Loyer("loyer-2026-P4", "Envoye", "2026-10-01"));
        Assert.That(FactureLoyerPanel.PeriodeAOuvrir(trim, auj), Is.EqualTo((2027, 1)), "trimestriel, 4e trimestre parti");

        var parti = Dupont(Periodicite.mensuel, "2026-10-15");
        var oct = Loyer("loyer-2026-P10", "Envoye", "2026-10-01");   // envoyé en entier avant la saisie du départ
        parti.facturesEtat.Add(oct);
        Assert.That(FactureLoyerPanel.PeriodeAOuvrir(parti, auj), Is.EqualTo((2026, 10)), "parti : le dernier loyer, pas une période après le départ");
        Assert.That(FactureEmission.SeraUneCorrection(parti, oct.key), Is.True, "affiché « (correction) »");
        Assert.That(FacturationAlertes.Pour(parti, auj).Single(a => a.message.Contains("montant à corriger")).cle, Is.EqualTo(oct.key),
            "« montant à corriger (départ) » ouvre octobre");

        string panneau = Src("Facturation/FactureLoyerPanel.cs");
        StringAssert.Contains("PeriodeAOuvrir(_loc, DateTime.Today)", panneau, "le chargement ouvre la période à envoyer");
        StringAssert.Contains("FactureEmission.SeraUneCorrection(_loc,", panneau, "titre et bouton : la règle de Preparer");
        StringAssert.Contains("\"Corriger et envoyer\"", panneau);
        StringAssert.DoesNotContain("PeriodePartie()", panneau, "plus le statut seul");
        string accueil = Src("General/HomeAlert.cs");
        StringAssert.Contains("cleFacture = fa.cle", accueil, "l'alerte de l'accueil et des cartes « Automatique » garde la période");
        StringAssert.Contains("FactureLoyerPanel.OpenLoyer(lp, a.cleFacture == null ? null", accueil, "le clic ouvre la période de l'alerte");
        StringAssert.Contains("a.locataire.facturesEtat?.Find(x => x.key == a.cleFacture) ?? new FactureEtat { key = a.cleFacture }", accueil,
            "avec sa ligne enregistrée (un loyer à corriger garde ses provisions), sinon une ligne neuve de cette période");
    }

    // ── D6-05 : une date illisible est refusée, plus remplacée ──

    /// Revue 17 (D6-05, D10-06) : une date vide ou illisible est refusée au centre avant l'aperçu et avant l'émission des quatre
    /// formulaires de facture, et à « Enregistrer » d'une charge (avec un nom vide ou un coût illisible) ; tirets et points se
    /// lisent (SaisieDateTests). Avant, sans rien dire : facture datée du jour, échéance vide (le loyer partait tout de suite),
    /// charge gardée à son ancienne date.
    [Test]
    public void D6_05_une_date_illisible_est_refusee_au_lieu_d_etre_remplacee()
    {
        Assert.That(SaisieDate.RefusFacture("05/11/2026", "01-12-2026"), Is.Null, "tirets : lue, le loyer attend sa date d'envoi");
        Assert.That(SaisieDate.RefusFacture("05/11/2026", "1er décembre 2026"),
            Is.EqualTo("Date d'échéance « 1er décembre 2026 » illisible : tapez-la sous la forme jj/mm/aaaa."));
        Assert.That(SaisieDate.RefusFacture(" ", "01/12/2026"), Is.EqualTo("Date de la facture : obligatoire."));

        Assert.That(ChargePanel.Refus("Eau", "", "31.12.2026"), Is.Null, "coût vide : 0 €, comme avant ; points : le 31/12/2026");
        Assert.That(ChargePanel.Refus(" ", "120", "31/12/2026"), Is.EqualTo("Nom de la charge : obligatoire."), "D10-06");
        StringAssert.StartsWith("Coût « - » illisible", ChargePanel.Refus("Eau", "-", "31/12/2026"));
        Assert.That(ChargePanel.Refus("Eau", "120", "31.13.2026"),
            Is.EqualTo("Date de la charge « 31.13.2026 » n'existe pas : vérifiez le jour et le mois."), "bien formée : le format n'est pas en cause");
        Assert.That(SaisieDate.Refus("31/02/2026", "Date"), Is.EqualTo("Date « 31/02/2026 » n'existe pas : vérifiez le jour et le mois."));
        Assert.That(ChargePanel.Refus("Eau", "120", ""), Is.EqualTo("Date de la charge : obligatoire."));
        string charge = Src("Facturation/ChargePanel.cs");
        int garde = charge.IndexOf("if (Refus(fNom.text, fCout.text, fDate.text) is string refus) { ConfirmDialog.Erreur(refus); return; }", StringComparison.Ordinal);
        Assert.That(garde >= 0 && garde < charge.IndexOf("SaveForm(fNom.text", StringComparison.Ordinal), Is.True, "refus avant l'enregistrement, fenêtre ouverte");

        // Les deux boutons des quatre formulaires : la garde passe avant SaveFromUI, qui enregistrait vide une date illisible.
        foreach (var p in new[] { "FactureLoyerPanel.cs", "FactureRegulPanel.cs", "FactureRefacPanel.cs", "FactureDepotPanel.cs" })
        {
            string src = Src("Facturation/" + p);
            foreach (var m in new[] { "void GenererApercu()", "void SauvegarderEtEnvoyer()" })
            {
                int debut = src.IndexOf(m, StringComparison.Ordinal);
                int refus = src.IndexOf("if (SaisieDate.RefusFacture(_date.text, _echeance.text) is string refusDate) { ConfirmDialog.Erreur(refusDate); return; }",
                                        Math.Max(0, debut), StringComparison.Ordinal);
                Assert.That(debut >= 0 && refus > debut && refus < src.IndexOf("SaveFromUI();", debut, StringComparison.Ordinal), Is.True, p + " : " + m);
            }
        }
    }

    // ── D6-09 : une charge déjà facturée ne se supprime plus ──

    /// Revue 17 (D6-09) : une charge portée sur une facture d'un locataire du bâtiment (archivé compris) ne se supprime plus : refus au
    /// centre qui nomme les locataires et dit quoi faire. Avant : une seule confirmation, puis la refacturation ne se corrigeait plus
    /// et la correction de la régularisation sortait sans la charge.
    [Test]
    public void D6_09_une_charge_facturee_ne_se_supprime_pas()
    {
        var dupont = new Locataire { id = "D69", Name = "Dupont" };
        var martin = new Locataire { id = "D69b", Name = "Martin", archive = true };
        var durand = new Locataire { id = "D69c", Name = "Durand" };   // présent, jamais facturé
        var locs = new List<Locataire> { dupont, martin, durand };
        var jardin = new ChargeBatiment { nom = "Jardin 2026", cout = 480f, dateISO = "2026-06-01" };
        Assert.That(ChargeBatiment.RefusSuppression(jardin, locs), Is.Null, "jamais facturée : confirmation puis « Annuler », comme avant");

        jardin.MarquerFacturee("D69", "2026-07-01");
        jardin.MarquerFacturee("D69b", "2026-07-01");
        string refus = ChargeBatiment.RefusSuppression(jardin, locs);
        StringAssert.StartsWith("« Jardin 2026 » ne peut pas être supprimée : elle est sur des factures de Dupont, Martin ;", refus, "l'archivé compris");
        StringAssert.DoesNotContain("Durand", refus, "seuls ceux à qui elle a été facturée");
        StringAssert.Contains("mettez son coût à 0, puis refaites ces factures depuis le suivi (« Corriger », ou « Refaire »", refus, "le bouton du suivi (coutures, D4-10 : « Corriger » pour une facture partie)");
        Assert.That(ChargeBatiment.RefusSuppression(jardin, new List<Locataire> { durand }), Is.Null,
                    "facturée seulement à des locataires supprimés depuis (D7a-09) : un autre locataire présent ne la bloque pas");

        string liste = Src("Achat/InvestissementListPanel.cs");
        int garde = liste.IndexOf("ChargeBatiment.RefusSuppression(ch, _batiment.getBatiment().locataireDuBatiment) is string refus) { ConfirmDialog.Erreur(refus); return; }",
                                  StringComparison.Ordinal);
        Assert.That(garde >= 0 && garde < liste.IndexOf("\"Supprimer la charge\"", StringComparison.Ordinal), Is.True, "refus avant la confirmation");
    }

    /// Revue 17 (D6-09) : une charge « Réglée pour tous » que la régularisation émise couvre déjà reste dans sa correction, comme en
    /// refacturation (avant : la correction la perdait) ; pas encore facturée, elle n'est plus proposée.
    [Test]
    public void D6_09_une_charge_reglee_pour_tous_reste_dans_la_correction_de_sa_regul()
    {
        var a = new Locataire { id = "D69A", Name = "A" };
        a.facturesEtat.Add(new FactureEtat { key = "regul-2026", type = "Regul", statut = "Envoye", numero = "2027/01001", pdfPath = "r.pdf",
                                          echeanceISO = "2027-02-15" });
        var b = new Locataire { id = "D69B", Name = "B" };
        var taxe = new ChargeBatiment { nom = "Taxe foncière 2026", cout = 1000f, dateISO = "2026-10-15", tousLocataires = true };
        var bat = new Batiment { charges = { taxe }, locataireDuBatiment = { a, b } };
        taxe.MarquerFacturee("D69A", "2027-01-15");   // portée par la régul 2026 de A
        taxe.paye = true;                              // puis « Réglée pour tous » cochée

        Assert.That(FactureRegulPanel.ChargesDeRegul(bat, a, 2026), Is.EqualTo(new[] { taxe }), "couverte par la régul émise : gardée");
        Assert.That(FactureRegulPanel.ChargesDeRegul(bat, b, 2026), Is.Empty, "jamais facturée à B : plus proposée");
    }

    // ── D6-08 : variables du menu « / » ──

    /// Revue 17 (D6-08) : {echeance} est remplie dans les quatre formulaires, comme à l'envoi au lancement. Avant, le même modèle
    /// disait « à régler avant le . » parti d'un formulaire, et portait la date parti au lancement.
    [Test]
    public void D6_08_l_echeance_est_dans_le_contexte_des_quatre_formulaires()
    {
        foreach (var p in new[] { "FactureLoyerPanel.cs", "FactureRegulPanel.cs", "FactureRefacPanel.cs", "FactureDepotPanel.cs" })
        {
            string src = Src("Facturation/" + p);
            int debut = src.IndexOf("FactureContext BuildContext()", StringComparison.Ordinal);
            int fin = debut < 0 ? -1 : src.IndexOf("\n    }", debut, StringComparison.Ordinal);
            Assert.That(debut >= 0 && fin > debut, Is.True, p + " : BuildContext introuvable");
            StringAssert.Contains("echeance = TryDate(_echeance != null ? _echeance.text : \"\", out var e) ? e.ToString(\"dd/MM/yyyy\") : \"\",",
                                  src.Substring(debut, fin - debut), p);
        }
    }

    /// Revue 17 (D6-08) : le menu « / » ne propose une variable que là où elle est remplacée : {montant} dans la phrase du montant
    /// mensuel, {depot.*} dans l'explication du dépôt, {reste} dans le modèle du rappel. Avant : tout le catalogue partout, et
    /// « Montant du dépôt (positif) » choisi dans un e-mail partait « {depot.montant} ».
    [Test]
    public void D6_08_le_menu_ne_propose_dans_un_champ_que_ce_qui_y_est_remplace()
    {
        var ctx = new FactureContext { date = new DateTime(2026, 12, 1), numero = "2026/12001", periode = "Décembre 2026", echeance = "05/12/2026" };
        var partout = SlashAutocomplete.Proposees("");
        foreach (var v in partout)
            Assert.That(FactureVarResolver.Resolve(v.token, new Locataire { Name = "D68" }, new Batiment { Name = "Rivoli" }, ctx),
                        Is.Not.EqualTo(v.token), v.token + " : proposée partout, mais pas remplacée");
        var jetons = partout.ConvertAll(v => v.token);
        foreach (var t in new[] { "{montant}", "{depot.termes}", "{depot.nb}", "{depot.base}", "{depot.montant}", "{reste}" })
            Assert.That(jetons, Does.Not.Contain(t), t + " : remplacée dans un seul texte");
        Assert.That(jetons, Does.Contain("{echeance}").And.Contain("{loc.nom}"));

        Assert.That(SlashAutocomplete.Proposees("mont", "Ligne montant mensuel").ConvertAll(v => v.token), Does.Contain("{montant}"),
                    "phrase du montant mensuel");
        Assert.That(SlashAutocomplete.Proposees("", "Explication dépôt").ConvertAll(v => v.token),
                    Does.Contain("{depot.montant}").And.Contain("{numero}").And.Not.Contain("{montant}"), "explication du dépôt");
        Assert.That(SlashAutocomplete.Proposees("reste", "Rappel d'impayé").ConvertAll(v => v.token), Is.EqualTo(new[] { "{reste}" }),
                    "modèle du rappel");

        // Le menu suit ce filtre, et les champs qui remplacent ces variables les nomment.
        StringAssert.Contains("filtered = Proposees(query, groupesEnPlus);", Src("Facturation/SlashAutocomplete.cs"));
        StringAssert.Contains("void OnDisable() => Hide();", Src("Facturation/SlashAutocomplete.cs"), "D6-14 : fermer le formulaire masque le menu");
        StringAssert.Contains("SlashAutocomplete.Attach(_texteMensuel, \"Ligne montant mensuel\")", Src("Facturation/FactureLoyerPanel.cs"));
        string depot = Src("Facturation/FactureDepotPanel.cs");
        foreach (var champ in new[] { "_depotRappel", "_depotDu", "_depotRembourse", "_depotEquilibre" })
            StringAssert.Contains($"SlashAutocomplete.Attach({champ}, \"Explication dépôt\")", depot);
        string reglages = Src("Facturation/ReglagePanel.cs");
        StringAssert.Contains("SlashAutocomplete.Attach(objet, groupes); SlashAutocomplete.Attach(corps, groupes);", reglages);
        int rappel = reglages.IndexOf("BlocModele(body.transform, \"Rappel d'impayé\"", StringComparison.Ordinal);
        Assert.That(rappel, Is.GreaterThanOrEqualTo(0));
        StringAssert.EndsWith("\"Rappel d'impayé\"", reglages.Substring(rappel, reglages.IndexOf(");", rappel, StringComparison.Ordinal) - rappel),
                              "le modèle du rappel nomme son groupe");
    }

    /// Revue 17 (D6-08) : les phrases de l'explication du dépôt reçoivent le contexte de la facture. Avant : « suite à la facture  du »,
    /// et {echeance} vide.
    [Test]
    public void D6_08_l_explication_du_depot_recoit_le_contexte_de_la_facture()
    {
        var ctx = new FactureContext { date = new DateTime(2026, 12, 1), numero = "2026/12001", echeance = "15/12/2026" };
        var depot = new FactureInfo { depotDu = "Suite à la facture {numero} du {date}, à régler avant le {echeance} : {depot.montant}." };
        string html = ExplicationDepot.Html(new Locataire { Name = "D68" }, 2, 250f, ctx.date, null, depot, false, ctx);
        StringAssert.Contains("Suite à la facture 2026/12001 du ", html);
        StringAssert.Contains(", à régler avant le 15/12/2026 : 250,00 €.", html);
        StringAssert.DoesNotContain("{", html, "aucun jeton ne reste");
        StringAssert.Contains("_ttcToggle != null && _ttcToggle.isOn, ctx)", Src("Facturation/FactureDepotPanel.cs"), "le formulaire passe son contexte");
    }

    /// Revue 17 (06/11, trouvé en vérifiant D6-08) : la phrase de règlement d'usine n'est plus mémorisée, elle suit l'échéance de
    /// la facture suivante ; une phrase d'usine déjà mémorisée, avec une ancienne date, cède la place. Avant, le loyer de novembre
    /// proposait, et imprimait, « SOMME À NOUS RÉGLER LE 5 octobre 2026 ».
    [Test]
    public void La_phrase_de_reglement_d_usine_suit_l_echeance()
    {
        Assert.That(FactureEmission.EstPhraseUsine("SOMME À NOUS RÉGLER LE 5 octobre 2026"), Is.True);
        Assert.That(FactureEmission.EstPhraseUsine("SOMME QUI VOUS SERA REMBOURSÉE"), Is.True);
        Assert.That(FactureEmission.PhraseAMemoriser("SOMME À NOUS RÉGLER LE 1 décembre 2026"), Is.EqualTo(""));
        Assert.That(FactureEmission.PhraseAMemoriser("Valeur en votre aimable règlement"), Is.EqualTo("Valeur en votre aimable règlement"));
        Assert.That(FactureEmission.PhraseAMemoriser("SOMME À NOUS RÉGLER LE {echeance}"), Is.EqualTo("SOMME À NOUS RÉGLER LE {echeance}"),
                    "personnalisée : gardée (elle suit déjà l'échéance)");
        foreach (var p in new[] { "FactureLoyerPanel.cs", "FactureRegulPanel.cs", "FactureRefacPanel.cs", "FactureDepotPanel.cs" })
        {
            string src = Src("Facturation/" + p);
            StringAssert.Contains("f.sommePhrase = FactureEmission.PhraseAMemoriser(_sommePhrase.text);", src, p + " : enregistrement");
            StringAssert.Contains("string.IsNullOrEmpty(f?.sommePhrase) || FactureEmission.EstPhraseUsine(f.sommePhrase) ? _autoSomme : f.sommePhrase",
                                  src, p + " : ouverture");
        }
    }

    // ── 2c : bouton « Auto » dans le suivi ──

    /// Revue 17 (2c, décisions du 06/11 ; relecture du 07/11) : « Automatique » et « À faire » quittent le menu de la pastille pour
    /// un bouton « Auto » à côté d'elle, sur une ligne pas encore partie ni grisée, ni remise après un départ annulé (numéro et PDF
    /// gardés). Allumé : CIPL gère la ligne. Éteint : un état mis à la main. L'éteindre bloque la ligne « À faire » ; le rallumer
    /// rend la main à CIPL (StatutAutomatique).
    [Test]
    public void Bouton_Auto_allume_eteint_et_absent_d_une_facture_partie()
    {
        FactureEtat Dec(string statut, string numero = "2026/11003", string pdf = "dec.pdf") => new FactureEtat { key = "loyer-2026-P12",
            type = "Loyer", statut = statut, libelle = "Loyer Décembre 2026", numero = numero, pdfPath = pdf, montant = 1200f, echeanceISO = "2026-12-01",
            periodicite = "mensuel" };
        var loc = new Locataire { id = "AUTO2", loyerAnnuel = 12000f, periodiciteLoyer = Periodicite.mensuel };
        loc.DateDebutBail = new DateTime(2024, 1, 1); loc.DateFinBail = new DateTime(2032, 12, 31);
        foreach (var s in new[] { "", null, "AttenteEnvoi" })
            Assert.That(FacturationSuivi.AutoAllume(Dec(s)), Is.True, "allumé : " + (s ?? "null"));
        Assert.That(FacturationSuivi.AutoAllume(null), Is.True, "ligne jamais enregistrée : CIPL la gère");
        Assert.That(FacturationSuivi.AutoAllume(Dec("AFaire")), Is.False, "« À faire » bloqué");
        foreach (var s in new[] { "Paye", "Impaye", "Envoye" })
            Assert.That(FacturationSuivi.AutoAllume(Dec(s, "", "")), Is.False, s + " mis à la main sans facture de CIPL");

        Assert.That(FacturationSuivi.StatutApresBoutonAuto(loc, Dec("")), Is.EqualTo("AFaire"), "éteindre : bloquée « À faire »");
        Assert.That(FacturationSuivi.StatutApresBoutonAuto(loc, Dec("AttenteEnvoi")), Is.EqualTo("AFaire"), "préparée : bloquée, plus proposée à l'envoi");
        Assert.That(FacturationSuivi.StatutApresBoutonAuto(loc, Dec("AFaire")), Is.EqualTo("AttenteEnvoi"), "rallumer : la facture préparée repart en attente");
        Assert.That(FacturationSuivi.StatutApresBoutonAuto(loc, Dec("AFaire", "", "")), Is.EqualTo(""), "rallumer : les dates");
        Assert.That(FacturationSuivi.StatutApresBoutonAuto(loc, Dec("Paye", "", "")), Is.EqualTo(""), "rallumer : le « Payé » mis à la main est annulé");

        var dec = Dec("AttenteEnvoi");
        loc.facturesEtat = new List<FactureEtat> { dec };
        Assert.That(FacturationSuivi.AvecBoutonAuto(loc, dec), Is.True, "préparée : le bouton est là");
        FacturationSuivi.SetStatut(loc, dec, FacturationSuivi.StatutApresBoutonAuto(loc, dec));
        Assert.That(FacturationSuivi.EtatDe(dec, loc), Is.EqualTo(FacturationSuivi.Etat.AFaire), "éteint : « À faire »");
        FacturationSuivi.SetStatut(loc, dec, FacturationSuivi.StatutApresBoutonAuto(loc, dec));
        Assert.That(FacturationSuivi.EtatDe(dec, loc), Is.EqualTo(FacturationSuivi.Etat.AttenteEnvoi), "rallumé : de nouveau en attente d'envoi");
        dec.statut = "Envoye";
        Assert.That(FacturationSuivi.AvecBoutonAuto(loc, dec), Is.False, "partie (numéro, PDF, envoyée) : pas de bouton");
        FacturationSuivi.SetStatut(loc, dec, "AttenteEnvoi");
        Assert.That(dec.statut, Is.EqualTo("Envoye"), "partie : SetStatut refuse aussi « En attente d'envoi »");
        dec.statut = "";
        Assert.That(FacturationSuivi.AvecBoutonAuto(loc, dec), Is.False,
            "en automatique avec numéro et PDF (retirée après un départ annulé) : pas de bouton, elle ne repartirait pas seule");
        loc.dateSortieISO = "2026-10-15";
        Assert.That(FacturationSuivi.AvecBoutonAuto(loc, FacturationSuivi.Lignes(loc, 2026).First(x => x.key == "loyer-2026-P11")), Is.False,
            "ligne grisée « Hors bail » : pas de bouton");

        string suivi = Src("Facturation/LocataireSuiviInline.cs");
        StringAssert.Contains("FacturationSuivi.AvecBoutonAuto(_loc, l) ? FacturationSuivi.AutoAllume(l) : (bool?)null", suivi, "la ligne passe le bouton");
        StringAssert.Contains("string statut = FacturationSuivi.StatutApresBoutonAuto(_loc, rec);", suivi, "le clic passe par la règle");
        StringAssert.DoesNotContain("MenuItem(vlg.transform, \"À faire\"", suivi, "« À faire » a quitté le menu");
        StringAssert.DoesNotContain("MenuItem(vlg.transform, \"Automatique\"", suivi, "« Automatique » a quitté le menu");
        StringAssert.Contains("MenuItem(vlg.transform, \"Retirer du suivi…\"", suivi, "l'exception du départ garde sa sortie");
    }

    /// Revue 17 (2c, décisions du 06/11) : le bouton « Auto » se crée au premier besoin, à droite de la pastille ; les deux tiennent
    /// dans la cellule État (132) sans rétrécir la pastille. Allumé et éteint n'ont pas la même couleur ; un clic appelle l'action de
    /// la ligne. Il se cache sur une facture partie (null), sur l'en-tête et sur la ligne « aucune facture ». Libellés sans accent :
    /// voir SuiviRowPrefabTests (atlas de la police).
    [Test]
    public void Bouton_Auto_tient_a_cote_de_la_pastille_et_se_cache_ailleurs()
    {
        var racine = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/Prefab/SuiviFactureRow.prefab");
        try
        {
            var r = racine.GetComponent<SuiviRowUI>();
            var neutres = new SuiviRowUI.Couleurs(Color.white, Color.black, Color.gray);
            int clics = 0;
            void Ligne(bool? auto) => r.Setup("Loyer Decembre 2026", "01/12/2026", "1 200,00 EUR", neutres, "A faire", Color.white,
                Color.black, true, null, new (string, Action)[0], auto, () => clics++);
            Transform Bouton() => r.etatCell.transform.Find("BoutonAuto");
            Vector3[] Coins(Transform t) { var c = new Vector3[4]; ((RectTransform)t).GetWorldCorners(c); return c; }

            Ligne(null);
            Assert.That(Bouton(), Is.Null, "facture partie : rien n'est créé");
            Ligne(true);
            Assert.That(Bouton().gameObject.activeSelf, Is.True);
            Assert.That(Bouton().GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("Auto"));
            var bouton = Bouton().GetComponent<UnityEngine.UI.Button>();
            Assert.That(bouton, Is.Not.Null, "un vrai bouton");
            Assert.That(((UnityEngine.UI.Graphic)bouton.targetGraphic).raycastTarget, Is.True, "le fond reçoit le clic");
            bouton.onClick.Invoke();
            Assert.That(clics, Is.EqualTo(1), "le clic appelle l'action de la ligne");
            var encreAllume = Bouton().GetComponentInChildren<TMPro.TMP_Text>().color;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)racine.transform);
            var cellule = Coins(r.etatCell.transform); var pastille = Coins(r.pastille.transform); var auto = Coins(Bouton());
            Assert.That(pastille[2].x - pastille[0].x, Is.EqualTo(96f).Within(0.5f), "la pastille garde sa largeur");
            Assert.That(auto[0].x, Is.GreaterThan(pastille[2].x), "à droite de la pastille, sans la chevaucher");
            Assert.That(auto[2].x, Is.LessThanOrEqualTo(cellule[2].x), "dans la cellule État, pas sur la colonne Actions");
            Assert.That(auto[2].x - auto[0].x, Is.GreaterThan(20f), "le texte « Auto » est mesuré");
            Assert.That(auto[1].y - auto[0].y, Is.LessThan(pastille[1].y - pastille[0].y), "plus petit que la pastille");

            Ligne(false);
            Assert.That(Bouton().gameObject.activeSelf, Is.True, "éteint : toujours là");
            Assert.That(Bouton().GetComponentInChildren<TMPro.TMP_Text>().color, Is.Not.EqualTo(encreAllume), "éteint : une autre couleur");
            Ligne(null);
            Assert.That(Bouton().gameObject.activeSelf, Is.False, "facture partie");
            Ligne(true);
            r.SetupEntete("Facture", "Echeance", "Montant TTC", "Etat", "Actions", Color.gray, Color.black);
            Assert.That(Bouton().gameObject.activeSelf, Is.False, "en-tête");
            Ligne(true);
            r.SetupVide("Aucune facture pour cette annee.", Color.white, Color.gray);
            Assert.That(Bouton().gameObject.activeSelf, Is.False, "ligne « aucune facture »");
            Ligne(true); Ligne(false); Ligne(true);
            Assert.That(r.etatCell.transform.Cast<Transform>().Count(x => x.name == "BoutonAuto"), Is.EqualTo(1), "un seul bouton, quel que soit le nombre de Setup");
        }
        finally { UnityEditor.PrefabUtility.UnloadPrefabContents(racine); }
    }

    // ── 2c : numéro repris, grisé ──

    /// Revue 17 (2c, décision du 06/11) : quand la facture a déjà un numéro, le formulaire montre celui que l'émission imprimera :
    /// pas encore partie (préparée, ou remise en automatique), le même ; partie, « corrigée(X) ». Une facture neuve, ou une émission
    /// refusée (D4-04), n'en reprend aucun : la ligne propose le numéro suivant, comme avant.
    [Test]
    public void Numero_repris_celui_que_l_emission_imprimera()
    {
        FactureEtat Dec(string statut, string pdf = "dec.pdf") => new FactureEtat { key = "loyer-2026-P12", type = "Loyer", statut = statut,
            libelle = "Loyer Décembre 2026", numero = "2026/11003", pdfPath = pdf, montant = 1200f, echeanceISO = "2026-12-01", periodicite = "mensuel" };
        Locataire Loc(FactureEtat l) => new Locataire { id = "BP8", factureSeq = 4, periodiciteLoyer = Periodicite.mensuel,
            facturesEtat = new List<FactureEtat> { l } };

        Assert.That(FactureEmission.NumeroRepris(Loc(Dec("AttenteEnvoi")), "loyer-2027-P1", "mensuel"), Is.Null, "facture neuve : la séquence");
        Assert.That(FactureEmission.NumeroRepris(Loc(Dec("AttenteEnvoi")), "loyer-2026-P12", "mensuel"), Is.EqualTo("2026/11003"), "préparée");
        var remise = Loc(Dec(""));
        Assert.That(FactureEmission.NumeroRepris(remise, "loyer-2026-P12", "mensuel"), Is.EqualTo("2026/11003"),
            "remise en automatique, statut vide (avant : 2026/11004 à l'écran, 2026/11003 sur le PDF)");
        var envoye = Dec("Envoye");
        Assert.That(FactureEmission.NumeroRepris(Loc(envoye), envoye.key, "mensuel"), Is.EqualTo("2026/11003 corrigée(1)"), "partie : sa correction");
        envoye.corrections = 1;
        Assert.That(FactureEmission.NumeroRepris(Loc(envoye), envoye.key, "mensuel"), Is.EqualTo("2026/11003 corrigée(2)"), "corrigée une fois : la suivante");
        Assert.That(FactureEmission.NumeroRepris(Loc(Dec("Envoye")), "loyer-2026-P12", "trimestriel"), Is.Null, "refus D4-04 de périodicité : rien de repris");
        // Le titre « (correction) » suit la même règle que la ligne (avant : « (correction) » au titre, un numéro neuf sur la ligne).
        Assert.That(FactureEmission.SeraUneCorrection(Loc(Dec("Envoye")), "loyer-2026-P12", "trimestriel"), Is.False, "refus D4-04 : pas de « (correction) »");
        Assert.That(FactureEmission.SeraUneCorrection(Loc(Dec("Envoye")), "loyer-2026-P12", "mensuel"), Is.True, "même périodicité : la correction");
        Assert.That(FactureEmission.NumeroRepris(Loc(Dec("AttenteEnvoi", pdf: "")), "loyer-2026-P12", "mensuel"), Is.Null, "sans PDF : une facture neuve");

        // L'émission reçoit le numéro affiché comme numéro proposé : il est ignoré, sans refus « déjà pris » ni séquence consommée.
        var d = FactureEmission.Preparer(remise, "loyer-2026-P12", "2026/11003", "mensuel");
        Assert.That(d.Refus, Is.Null);
        Assert.That(d.Reecriture, Is.True);
        Assert.That(d.NumeroFacture, Is.EqualTo("2026/11003"));
    }

    /// Revue 17 (2c) : la ligne « N° de facture » d'un numéro repris : la boîte le montre, grisé, l'ID est vide, non modifiable,
    /// « (repris) », et l'aide le dit ; préfixe + ID donnent le numéro imprimé (aperçu, {numero}). De retour sur une facture
    /// neuve : l'ID tapé revient, modifiable ; sans ID, la séquence. Les quatre formulaires y passent, avec la clé de leur
    /// émission, et la rafraîchissent après chaque émission, correction comprise.
    [Test]
    public void Numero_repris_grise_et_non_modifiable_dans_les_quatre_formulaires()
    {
        var racine = new GameObject("BP8", typeof(RectTransform));
        racine.SetActive(false);
        try
        {
            var aide = UIFactory.Text(racine.transform, "N° de facture  =  format  +  ID locataire", UITheme.Role.Aide, UITheme.TexteSecondaire);
            var prefixe = UIFactory.Text(racine.transform, "—", UITheme.Role.Libelle, UITheme.TextePrincipal, true);
            var id = UIFactory.Input(racine.transform, "ID locataire (ex. 001)");
            var indication = (TMPro.TMP_Text)id.placeholder;
            string garde = null;
            const string aideRepris = "N° de facture — repris : cette période a déjà ce numéro";

            id.text = "050";   // tapé sur une facture neuve
            FactureEmission.AfficherNumero(aide, prefixe, id, "2026/11003 corrigée(1)", "2026/11", 4, aideRepris, ref garde);
            Assert.That(prefixe.text + id.text.Trim(), Is.EqualTo("2026/11003 corrigée(1)"), "préfixe + ID : le numéro imprimé");
            Assert.That(id.interactable, Is.False, "non modifiable");
            Assert.That(indication.text, Is.EqualTo("(repris)"));
            Assert.That(aide.text, Is.EqualTo(aideRepris));
            Assert.That(prefixe.color, Is.EqualTo(UITheme.TexteExemple), "grisé, comme « (repris) »");

            FactureEmission.AfficherNumero(aide, prefixe, id, null, "2027/01", 4, aideRepris, ref garde);
            Assert.That(prefixe.text + id.text, Is.EqualTo("2027/01050"), "facture neuve : l'ID tapé revient");
            Assert.That(id.interactable, Is.True);
            Assert.That(indication.text, Is.EqualTo("ID locataire (ex. 001)"));
            Assert.That(aide.text, Is.EqualTo("N° de facture  =  format  +  ID locataire"));
            Assert.That(prefixe.color, Is.EqualTo(UITheme.TextePrincipal));

            id.text = "";   // émise : l'ID est consommé, la ligne reprend son numéro
            FactureEmission.AfficherNumero(aide, prefixe, id, "2027/01050", "2027/01", 51, aideRepris, ref garde);
            FactureEmission.AfficherNumero(aide, prefixe, id, null, "2027/02", 51, aideRepris, ref garde);
            Assert.That(id.text, Is.EqualTo("051"), "période suivante : la séquence, comme avant");
        }
        finally { UnityEngine.Object.DestroyImmediate(racine); }

        // Lot 15 (avenant de périodicité) : la périodicité passée est celle de la période affichée (PeriodiciteChoisie), plus celle de la fiche.
        foreach (var (p, cle) in new[] { ("FactureLoyerPanel.cs", "CleLoyer(), PeriodiciteChoisie().ToString()"), ("FactureRegulPanel.cs", "CleFacture()"),
                                         ("FactureRefacPanel.cs", "CleFacture()"), ("FactureDepotPanel.cs", "CleSuivi()") })
        {
            string src = Src("Facturation/" + p);
            string Corps(string debut)
            {
                int i = src.IndexOf(debut, StringComparison.Ordinal);
                Assert.That(i, Is.GreaterThanOrEqualTo(0), p + " : " + debut);
                // Revue 17 (2c) : la fin de la méthode, sans le saut de ligne qui suit : le motif marche en LF comme en CRLF (autocrlf).
                return src.Substring(i, src.IndexOf("\n    }", i, StringComparison.Ordinal) - i);
            }
            string refresh = Corps("void RefreshNumero()");
            StringAssert.Contains($"FactureEmission.NumeroRepris(_loc, {cle})", refresh, p + " : la clé de l'émission");
            StringAssert.Contains("FactureEmission.AfficherNumero(_numeroAide, _numeroPrefixe, _numeroId, repris,", refresh, p + " : grisée, non modifiable");
            StringAssert.Contains("_numeroEmis = null;", refresh, p + " : l'aperçu (NumeroDocument) suit la ligne, pas un numéro d'une émission passée");
            StringAssert.Contains("_numeroAide = UIFactory.Text(", src, p + " : l'aide suit la ligne");
            StringAssert.Contains("\n        RefreshNumero();", Corps("void Finaliser("),
                p + " : rafraîchie après chaque émission, correction comprise (avant : seulement hors correction, « corrigée(1) » restait affiché)");
            StringAssert.Contains("_numeroId.onValueChanged.AddListener(_ => { _numeroEmis = null; RefreshEntetePreview(); });", src,
                p + " : un ID retapé après une émission abandonnée, l'entête et l'aperçu le suivent");
        }
        // Les rappels qui changent la clé rafraîchissent la ligne : sans eux, le défaut vu en Play revient (un numéro neuf affiché,
        // l'ancien imprimé).
        foreach (var (p, rappel, fois) in new[] { ("FactureLoyerPanel.cs", "RefreshEcheanceDefault(); RefreshNumero(); RefreshTauxDefault(); }", 2),   // période, année (lot 10 : le taux suit aussi)
                                                  ("FactureRegulPanel.cs", "RefreshCharges(); RefreshNumero(); RefreshTauxDefault(); }", 2),           // année, listes
                                                  ("FactureRefacPanel.cs", "RefreshTotaux(); RefreshNumero(); RefreshTauxDefault(); }", 1) })          // charges cochées
            Assert.That(Src("Facturation/" + p).Split(new[] { rappel }, StringSplitOptions.None).Length - 1, Is.EqualTo(fois), p + " : " + rappel);
        string loyer = Src("Facturation/FactureLoyerPanel.cs");
        StringAssert.Contains("string key = CleLoyer();", loyer, "le loyer émet sous la clé affichée");
        StringAssert.Contains("FactureEmission.SeraUneCorrection(_loc, CleLoyer(), PeriodiciteChoisie().ToString())", loyer,
            "titre et bouton : la périodicité de la ligne (lot 15 : celle de la période affichée)");
    }

    /// Revue 17 (2c, décisions du 06/11 ; relecture du 07/11) : rallumer le bouton « Auto » d'une facture préparée puis bloquée
    /// la remet « En attente d'envoi » avec son PDF, si elle correspond toujours à ce qui est dû : pas un décompte sans départ,
    /// pas un loyer d'une période après le départ, d'une autre périodicité ou d'un autre montant (prorata d'un départ annulé).
    /// Sinon elle revient à l'état des dates et se refait. Avant : un PDF périmé repartait tel quel.
    [Test]
    public void Rallumer_ne_repropose_qu_une_facture_toujours_juste()
    {
        var loc = new Locataire { id = "AUTO3", Name = "Dupont", loyerAnnuel = 12000f, periodiciteLoyer = Periodicite.mensuel };
        loc.DateDebutBail = new DateTime(2024, 1, 1); loc.DateFinBail = new DateTime(2032, 12, 31);
        FactureEtat Nov(float loyerHT, string periodicite = "mensuel") => new FactureEtat { key = "loyer-2026-P11", type = "Loyer", statut = "AFaire",
            libelle = "Loyer Novembre 2026", numero = "2026/11003", pdfPath = "nov.pdf", montant = 1.2f * loyerHT, loyerHT = loyerHT,
            echeanceISO = "2026-11-01", periodicite = periodicite };
        Assert.That(FacturationSuivi.StatutAutomatique(loc, Nov(1000f)), Is.EqualTo("AttenteEnvoi"), "même loyer : elle repart en attente");
        Assert.That(FacturationSuivi.StatutAutomatique(loc, Nov(500f)), Is.EqualTo(""), "préparée au prorata d'un départ annulé : à refaire");
        Assert.That(FacturationSuivi.StatutAutomatique(loc, Nov(1000f, "trimestriel")), Is.EqualTo(""), "autre périodicité : à refaire");
        var sansPdf = Nov(1000f); sansPdf.pdfPath = "";
        Assert.That(FacturationSuivi.StatutAutomatique(loc, sansPdf), Is.EqualTo(""), "« À faire » sans facture : les dates");
        Assert.That(FacturationSuivi.StatutAutomatique(loc, null), Is.EqualTo(""));
        var decompte = new FactureEtat { key = DepartLocataire.CleDecompte, type = "Depot", statut = "AFaire", numero = "2026/11004", pdfPath = "d.pdf" };
        Assert.That(FacturationSuivi.StatutAutomatique(loc, decompte), Is.EqualTo(""), "décompte sans départ (départ annulé) : il ne repart pas");
        loc.dateSortieISO = "2026-11-15";
        Assert.That(FacturationSuivi.StatutAutomatique(loc, decompte), Is.EqualTo("AttenteEnvoi"), "décompte, départ toujours saisi : il repart");
        Assert.That(FacturationSuivi.StatutAutomatique(loc, new FactureEtat { key = "loyer-2026-P12", type = "Loyer", statut = "AFaire",
            numero = "2026/11005", pdfPath = "dec.pdf", loyerHT = 1000f, echeanceISO = "2026-12-01", periodicite = "mensuel" }), Is.EqualTo(""),
            "période après le départ : rien n'y est dû, il ne repart pas");
        StringAssert.Contains("ne correspond plus à ce qui est dû", Src("Facturation/LocataireSuiviInline.cs"), "le message dit qu'elle est à refaire");
    }

    /// Revue 17 (2c, relecture du 07/11) : un loyer préparé (ou bloqué) pour une période entièrement après un départ saisi ensuite
    /// se retire du suivi (« Retirer du suivi… »), sans avoir : il repasse « Hors bail », n'est plus proposé à l'envoi et ne bloque
    /// plus l'archivage ; l'alerte et l'étape « Dernier loyer » montrent le suivi. Avant : seule une facture partie avait cette
    /// sortie, et le bouton « Auto » ne faisait qu'alterner « À faire » et « En attente d'envoi ».
    [Test]
    public void Un_loyer_prepare_apres_le_depart_se_retire_du_suivi()
    {
        var loc = new Locataire { id = "RET", Name = "Dupont", loyerAnnuel = 12000f, periodiciteLoyer = Periodicite.mensuel };
        loc.DateDebutBail = new DateTime(2024, 1, 1); loc.DateFinBail = new DateTime(2032, 12, 31);
        var dec = new FactureEtat { key = "loyer-2026-P12", type = "Loyer", statut = "AttenteEnvoi", libelle = "Loyer Décembre 2026", numero = "2026/11003",
            pdfPath = "dec.pdf", montant = 1200f, loyerHT = 1000f, echeanceISO = "2026-12-01", periodicite = "mensuel" };
        loc.facturesEtat = new List<FactureEtat> { dec };
        loc.dateSortieISO = "2026-11-30";
        Assert.That(DepartLocataire.Retirable(loc, dec), Is.True, "préparé, période après le départ");
        Assert.That(DepartLocataire.LoyerACorriger(loc, dec), Is.True, "le bandeau et l'archivage le signalent");
        string texte = DepartLocataire.ConfirmationRetrait(loc, dec);
        StringAssert.Contains("n'est pas parti chez le locataire", texte);
        StringAssert.DoesNotContain("avoir", texte, "pas d'avoir : rien n'est parti");
        // L'alerte (FacturationAlertes.Pour ne calcule que pour une fiche complète) : de départ, avec sa clé et le texte du bandeau.
        StringAssert.Contains("? new Alerte { niveau = Niveau.Attention, type = AlerteType.Depart, cle = l.key, message = $\"{DepartLocataire.ARetirer(l)}.\" }",
            Src("Facturation/FacturationAlertes.cs"), "pas « Facturer le loyer » : le formulaire refuse 0 €");
        StringAssert.Contains("Retirer du suivi", DepartLocataire.ARetirer(dec));
        StringAssert.Contains("LocataireSuiviInline.MontrerPour(lp, annee)", Src("General/HomeAlert.cs"), "l'alerte montre le suivi");
        StringAssert.Contains("LocataireSuiviInline.MontrerPour(_fiche, annee)", Src("Locataire/DepartLocataireUI.cs"), "l'étape « Dernier loyer » aussi");

        FacturationSuivi.SetStatut(loc, dec, FacturationSuivi.StatutApresBoutonAuto(loc, dec));   // bouton éteint : « À faire »
        Assert.That(dec.statut, Is.EqualTo("AFaire"));
        Assert.That(DepartLocataire.Retirable(loc, dec), Is.True, "bloqué : toujours retirable");
        Assert.That(FacturationSuivi.StatutApresBoutonAuto(loc, dec), Is.EqualTo(""), "rallumé : période après le départ, il ne repart pas");
        FacturationSuivi.SetStatut(loc, dec, "");   // « Retirer du suivi »
        Assert.That(FacturationSuivi.Lignes(loc, 2026).First(x => x.key == dec.key).statut, Is.EqualTo("HorsBail"));
        Assert.That(DepartLocataire.RaisonsNonArchivable(loc), Has.None.Contains("Loyer Décembre 2026"));
    }

    /// Revue 17 (2c, relecture du 07/11) : une facture préparée puis bloquée garde son document : une régularisation regroupée ne
    /// perd pas une liste (Regroupable), ses charges restent dans sa correction (ChargesDeRegul), une refacturation garde ses
    /// montants, et elle reste dans le suivi même quand sa liste n'est plus planifiée, pour qu'on puisse la rallumer.
    [Test]
    public void Une_facture_bloquee_garde_son_document()
    {
        var loc = new Locataire { id = "BLQ", periodiciteLoyer = Periodicite.mensuel };
        var a = new FactureEtat { key = "regul-2026", type = "Regul", statut = "AFaire", libelle = "Régularisation des charges 2026", numero = "2027/01001",
            pdfPath = "regul.pdf", montant = 100f, echeanceISO = "2027-01-31" };
        var b = new FactureEtat { key = "regul-2026-L2", type = "Regul", statut = "AFaire", libelle = "Régularisation 2026 (liste 2)", numero = "2027/01001",
            pdfPath = "regul.pdf", montant = 50f, echeanceISO = "2027-01-31" };
        loc.facturesEtat = new List<FactureEtat> { a, b };
        Assert.That(FacturationSuivi.EstDejaEmise(loc, a.key, out _), Is.False, "pré-requis : pas émise au sens strict");
        Assert.That(FacturationSuivi.AFacture(loc, a.key, out _), Is.True, "mais elle a sa facture");
        Assert.That(FacturationSuivi.Regroupable(loc, new List<string> { a.key }, out _, out var oubliees), Is.False, "on ne retire pas une liste de sa facture");
        Assert.That(oubliees.Select(x => x.key), Is.EqualTo(new[] { b.key }));
        Assert.That(FacturationSuivi.Lignes(loc, 2026).Select(x => x.key), Does.Contain(a.key), "bloquée, pas planifiée : elle reste visible");
        StringAssert.Contains("FacturationSuivi.AFacture(loc, ListesCharges.Cle(listeId, year), out _)", Src("Facturation/FactureRegulPanel.cs"),
            "une régularisation bloquée garde ses charges");
        StringAssert.Contains("FacturationSuivi.AFacture(_loc, \"refac-\" + c.id, out var rec)", Src("Facturation/FactureRefacPanel.cs"),
            "une refacturation bloquée garde ses montants");
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
            StringAssert.Contains("FermetureGuard.Demander(Quitter)", Src("General/QuitButton.cs"), "la croix Quitter aussi");
        }
        finally
        {
            var f = typeof(EcritureCloud).GetField("_enAttente", BindingFlags.NonPublic | BindingFlags.Static);
            ((HashSet<Batiment>)f.GetValue(null)).Remove(b);
        }
    }

    // ── Reprise du 09/11 (Fable), lot A : les critiques ──

    [Test]
    public void D6_17_la_quittance_est_signee_par_l_entreprise_ouverte()
    {
        string html = FacturePdfService.BuildQuittanceHtml(new FacturePdfService.QuittanceData { societe = "SCI Rivoli" });
        Assert.That(html.Split(new[] { "SCI Rivoli" }, StringSplitOptions.None).Length - 1, Is.EqualTo(2), "« Je soussigné » et la signature");
        StringAssert.DoesNotContain("GROUPE CIPL", html);
        StringAssert.DoesNotContain("GROUPE CIPL", File.ReadAllText(FacturePdfService.QuittanceTemplatePath));
        StringAssert.Contains("societe = FactureVarResolver.Societe(", Src("Facturation/FactureQuittanceService.cs"));
    }

    [Test]
    public void D6_18_la_correction_d_une_revision_reprend_l_ancien_depot_de_sa_facture()
    {
        var loc = new Locataire { depotDeGarantie = 2200f, depotPrecedent = 0f };
        var f = new FactureInfo { saved = true, moisPeriode = 2, provisionMontant = 2000f };
        Assert.That(FactureDepotPanel.AncienPropose(loc, f, true), Is.EqualTo(2000f), "« Refaire » : l'ancien dépôt de la facture");
        Assert.That(FactureDepotPanel.AncienPropose(loc, f, false), Is.EqualTo(2200f), "pas encore facturée : le dépôt en place");
        loc.depotPrecedent = 2000f;
        Assert.That(FactureDepotPanel.AncienPropose(loc, f, true), Is.EqualTo(2000f));
        Assert.That(FactureDepotPanel.AncienPropose(loc, f, false), Is.EqualTo(2000f), "« Réviser » fait : son ancien dépôt");
        StringAssert.Contains("AncienPropose(_loc, f, FacturationSuivi.AFacture(_loc, CleSuivi(), out _))", Src("Facturation/FactureDepotPanel.cs"));
    }

    [Test]
    public void D6_18_sans_revision_en_attente_le_bouton_de_la_fiche_refuse()
    {
        var aujourdhui = new DateTime(2026, 11, 9);
        var loc = new Locataire { depotDeGarantie = 2200f, dateRevisionDepotISO = "2027-10-01" };
        string refus = FactureDepotPanel.RefusOuverture(loc, null, aujourdhui);
        StringAssert.Contains("01/10/2027", refus);
        StringAssert.Contains("« Réviser »", refus);
        StringAssert.Contains("« Refaire »", refus);
        Assert.That(FactureDepotPanel.RefusOuverture(loc, new FactureEtat { key = "depot-2026" }, aujourdhui), Is.Null, "depuis le suivi");
        Assert.That(FactureDepotPanel.RefusOuverture(loc, null, new DateTime(2027, 10, 1)), Is.Null, "révision échue");
        loc.depotPrecedent = 2000f;
        Assert.That(FactureDepotPanel.RefusOuverture(loc, null, aujourdhui), Is.Null, "« Réviser » fait : la facture est attendue");
        loc.depotPrecedent = 0f; loc.dateSortieISO = "2026-12-31";
        Assert.That(FactureDepotPanel.RefusOuverture(loc, null, aujourdhui), Is.Null, "départ saisi : décompte de sortie");
        string src = Src("Facturation/FactureDepotPanel.cs");
        Assert.That(src.IndexOf("RefusOuverture(fiche.GetLocataire(), ligne, DateTime.Today)", StringComparison.Ordinal),
                    Is.LessThan(src.IndexOf("EcritureCloud.Reserver(EcritureCloud.Cibles(fiche, true), \"dépôt\"", StringComparison.Ordinal)), "refusé avant de réserver");   // D4-11 : avec le bâtiment
    }

    [Test]
    public void D8_14_se_deconnecter_previent_des_fiches_qui_attendent_la_connexion()
    {
        var (titre, texte) = FermetureGuard.Avertissement(new List<string>(), new List<string> { "Rivoli" }, "Se déconnecter");
        Assert.That(titre, Is.EqualTo("Modifications pas encore envoyées"));
        StringAssert.Contains("• Rivoli", texte);
        StringAssert.Contains("Se déconnecter maintenant les perdrait", texte);
        Assert.That(FermetureGuard.Bouton("Continuer", 0), Is.EqualTo("Continuer quand même"));
        Assert.That(FermetureGuard.Bouton("Continuer", 1), Is.EqualTo("Continuer sans enregistrer"));
        string src = Src("General/FermetureGuard.cs");
        string garde = src.Substring(src.IndexOf("public static void ConfirmerPerteSaisies(", StringComparison.Ordinal));
        garde = garde.Substring(0, garde.IndexOf("ConfirmDialog.Instance.Show(", StringComparison.Ordinal));
        StringAssert.Contains("EcritureCloud.NomsEnAttente()", garde, "Se déconnecter, changer d'entreprise ou de dossier passent tous par elle");
        StringAssert.Contains("EnvoiEnCours", garde);

        // Une fiche périmée (entreprise vidée) est déjà perdue : la garde ne la cite plus.
        var b = new Batiment { Name = "Rivoli" };
        EcritureCloud.MettreEnAttente(b);
        try
        {
            Assert.That(EcritureCloud.NomsEnAttente(), Does.Contain("Rivoli"));
            EcritureCloud.Perimer(b);
            Assert.That(EcritureCloud.NomsEnAttente(), Does.Not.Contain("Rivoli"));
        }
        finally
        {
            foreach (var champ in new[] { "_enAttente", "_perimes" })
                ((HashSet<Batiment>)typeof(EcritureCloud).GetField(champ, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Remove(b);
        }
    }

    [Test]
    public void D8_01_reste_la_fermeture_qui_attend_ne_quitte_pas_sur_un_echec_ou_un_refus()
    {
        Assert.That(FermetureGuard.QuitterApresAttente(0, 0, false), Is.True);
        Assert.That(FermetureGuard.QuitterApresAttente(0, 1, false), Is.False, "panne pendant l'attente : la fiche attend la connexion");
        Assert.That(FermetureGuard.QuitterApresAttente(1, 1, false), Is.True, "« Quitter quand même » accepté avant l'attente");
        Assert.That(FermetureGuard.QuitterApresAttente(0, 0, true), Is.False, "refus du serveur affiché au centre");
        string src = Src("General/FermetureGuard.cs");
        StringAssert.Contains("_quitterConfirme = false; return;", src, "CIPL reste ouvert et la croix redemandera");
        StringAssert.DoesNotContain("QuandTermine(Application.Quit)", src);
    }

    [Test]
    public void D8_03_la_fermeture_attend_l_envoi_de_factures_en_cours()
    {
        Assert.That(FermetureGuard.Decision(0, false, 0, envoiEnCours: true), Is.EqualTo(FermetureGuard.Choix.Attendre));
        Assert.That(FermetureGuard.Decision(1, false, 0, envoiEnCours: true), Is.EqualTo(FermetureGuard.Choix.Prevenir));
        Assert.That(FermetureGuard.Decision(0, false, 0), Is.EqualTo(FermetureGuard.Choix.Quitter));
        var (titre, texte) = FermetureGuard.Avertissement(new List<string>(), new List<string>(), "Se déconnecter", envoiEnCours: true);
        Assert.That(titre, Is.EqualTo("Envoi de factures en cours"));
        StringAssert.Contains("Se déconnecter maintenant l'interromprait", texte);
        StringAssert.Contains("FermetureGuard.Demander(Quitter)", Src("General/QuitButton.cs"), "la croix : la même décision que la garde (D8-13)");
        StringAssert.Contains("public static bool EnCours", Src("Facturation/FactureEnvoiAuto.cs"), "l'envoi groupé, hors cloud aussi");
    }

    [Test]
    public void D7a_09_un_locataire_facture_ne_se_supprime_pas()
    {
        var partie = new Locataire { Name = "Dupont", facturesEtat = new List<FactureEtat> {
            new FactureEtat { key = "loyer-2026-P1", statut = "Envoye", numero = "2026/01001", pdfPath = "x.pdf" } } };
        string refus = DepartLocataire.RefusSuppression(partie);
        StringAssert.Contains("« Dupont »", refus);
        StringAssert.Contains("« Départ du locataire »", refus);
        var preparee = new Locataire { Name = "Martin", facturesEtat = new List<FactureEtat> {
            new FactureEtat { key = "loyer-2026-P1", statut = "AttenteEnvoi", numero = "2026/01001", pdfPath = "x.pdf" } } };
        Assert.That(DepartLocataire.RefusSuppression(preparee), Is.Null, "préparée, pas partie : supprimable (doublon, essai)");
        Assert.That(DepartLocataire.RefusSuppression(new Locataire { Name = "Sans" }), Is.Null);

        var bat = new Batiment { Name = "Rivoli", locataireDuBatiment = new List<Locataire> { preparee, partie } };
        StringAssert.Contains("« Dupont »", DepartLocataire.RefusSuppression(bat));
        bat.locataireDuBatiment = new List<Locataire> { preparee };
        Assert.That(DepartLocataire.RefusSuppression(bat), Is.Null);

        StringAssert.Contains("DepartLocataire.RefusSuppression(loc)", Src("Locataire/LocatairePrefab.cs"), "avant la confirmation");
        StringAssert.Contains("DepartLocataire.RefusSuppression(batiment)", Src("Batiment/BatimentPrefab.cs"), "même garde pour le bâtiment");
    }

    [Test]
    public void D5_01_reste_la_cle_d_un_numero_long_reste_unique_et_tient_en_36()
    {
        var loc = new Locataire { id = "a1b2c3d4-e5f6-0000-0000-000000000000" };
        Assert.That(FactureElectronique.Cle(loc, new FactureEtat { numero = "2026/11001" }), Is.EqualTo("a1b2c3d4-2026/11001"),
                    "inchangée : les réservations déjà prises restent valables");
        string longNumero = "2026/11000000000000000000001";   // avec « -corrigee-N », au-delà de 36
        string c1 = FactureElectronique.Cle(loc, new FactureEtat { numero = longNumero, plateformeDonnees = "{\"number\":\"" + longNumero + "-corrigee-1\"}" });
        string c2 = FactureElectronique.Cle(loc, new FactureEtat { numero = longNumero, plateformeDonnees = "{\"number\":\"" + longNumero + "-corrigee-2\"}" });
        Assert.That(c1.Length, Is.EqualTo(36));
        Assert.That(c2.Length, Is.EqualTo(36));
        Assert.That(c1, Is.Not.EqualTo(c2), "avant : coupées à 36, les deux rectificatives partageaient la clé");
        StringAssert.StartsWith("a1b2c3d4-", c1);
        StringAssert.Contains("slice(0, 36)", File.ReadAllText(Path.Combine("Serveur", "fonctions", "facture-electronique.ts")),
                              "le serveur coupe à 36 : sans effet sur une clé de 36");
    }

    [Test]
    public void D3_01_reste_une_entreprise_sans_ville_connue_part_sans_lieu_d_emission()
    {
        Assert.That(RegleInscription.Reglages(true, "SCI Rivoli", "12345678900012", null, "Marie", "", "").lieuEmission, Is.EqualTo(""), "annuaire injoignable");
        Assert.That(RegleInscription.Reglages(false, "Patrimoine de Jean Martin", "", null, "Jean Martin", "4 rue des Lilas, 31000 Toulouse", "").lieuEmission,
                    Is.EqualTo("Toulouse"));
        Assert.That(RegleInscription.ReglagesAjout(false, "Patrimoine de Jean Martin", "", null).lieuEmission, Is.EqualTo(""), "particulier ajouté : sans adresse");
        StringAssert.DoesNotContain("LieuDefaut", Src("Cloud/RegleInscription.cs"));
        StringAssert.Contains("RegleInscription.NomParticulier(\"\", await EcritureCloud.MonNom())", Src("Cloud/SectionAbonnement.cs"),
                              "un particulier ajouté sans nom : le même nom que le serveur, pour ses Réglages");
        StringAssert.Contains("R.lieuEmission ?? \"\"", Src("Facturation/ReglagePanel.cs"), "les Réglages montrent le lieu tel quel, pas la ville de CIPL");
    }
}
