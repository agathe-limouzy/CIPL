using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

/// Revue 17, lot D (11/11/2026), groupe « sql » : les sections 8 à 12 de Serveur/etape17c_supabase.sql (D2-16, D2-10, D1-06,
/// D2-18 ; REVUE_17/D2.md et D1.md). Tests texte : le SQL ne s'exécute pas dans Unity. La comparaison de chaque fonction
/// recréée à sa dernière définition (aucune ligne perdue) est dans LotBSqlTests ; ici : ce que le lot D ajoute, et les
/// clauses vitales nommées une à une (maj_session, version + 1, modèle, « supprimee », mes_acces).
public class LotDSqlTests
{
    static string Serveur(string f) => File.ReadAllText(Path.Combine("Serveur", f)).Replace("\r\n", "\n");
    static string Sql => Serveur("etape17c_supabase.sql");
    static string Fonction(string sql, string nom)
    {
        int i = sql.IndexOf("create or replace function public." + nom + "(", StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), nom + " absente du SQL");
        return sql.Substring(i, sql.IndexOf("end $$;", i, StringComparison.Ordinal) - i);
    }
    static int Fois(string src, string fragment) => src.Split(new[] { fragment }, StringSplitOptions.None).Length - 1;
    static int Place(string src, string fragment)
    {
        int i = src.IndexOf(fragment, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), "absent : " + fragment);
        return i;
    }
    /// De `debut` à `fin` (exclu), dans `src`.
    static string Entre(string src, string debut, string fin)
    {
        string s = src.Substring(Place(src, debut));
        return s.Substring(0, Place(s, fin));
    }
    /// « p_entreprise uuid, p_modele int default 0 » → « uuid, int » : les types d'une signature, comme les droits les nomment.
    static string Types(string corps)
    {
        int i = corps.IndexOf('(') + 1;
        return string.Join(", ", corps.Substring(i, corps.IndexOf(')', i) - i).Split(',').Select(p => p.Trim().Split(' ')[1]));
    }

    const string Acces = "if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;";

    // ── D2-16 : supprimer un bâtiment regarde les verrous de ses locataires ──

    [Test]
    public void D2_16_supprimer_un_batiment_est_refuse_si_un_de_ses_locataires_est_tenu()
    {
        string corps = Fonction(Sql, "supprimer_fiche");
        int branche = Place(corps, "if p_type = 'batiment' then");
        int controle = Place(corps, "where vl.entreprise_id = p_entreprise and vl.cible_type = 'locataire' and lo.batiment_id = p_id");
        int suppression = Place(corps, "delete from public.batiments where entreprise_id = p_entreprise and id = p_id;");
        Assert.That(controle, Is.GreaterThan(branche), "seulement pour un bâtiment");
        Assert.That(controle, Is.LessThan(suppression), "le contrôle précède la suppression");
        StringAssert.Contains("join public.locataires lo on lo.entreprise_id = vl.entreprise_id and lo.id = vl.cible_id", corps, "les verrous des locataires de CE bâtiment");
        // Un verrou « vivant » se juge comme dans detenteur_autre (etape4) : une autre session, pas encore expiré.
        StringAssert.Contains("and session_id <> p_session and expire_le >= now();", Serveur("etape4_supabase.sql"), "la règle de detenteur_autre");
        StringAssert.Contains("and vl.session_id <> p_session and vl.expire_le >= now()", corps, "la même règle ici");
        // Contrat C10 : même forme de refus que pour le bâtiment lui-même, avant toute écriture.
        Assert.That(Fois(corps, "return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le);"), Is.EqualTo(2));
        StringAssert.Contains("order by vl.pris_le limit 1;\n    if v.cible_id is not null then\n      return json_build_object('ok', false, 'refus', 'verrou'", corps,
            "le refus sort dès qu'un verrou de locataire est trouvé");
        int refus = corps.IndexOf("'refus', 'verrou'", controle, StringComparison.Ordinal);
        Assert.That(refus, Is.GreaterThan(controle).And.LessThan(Place(corps, "delete from")), "le refus sort avant la première écriture");
    }

    [Test]
    public void D2_16_les_verrous_des_locataires_supprimes_avec_le_batiment_sont_effaces()
    {
        string corps = Fonction(Sql, "supprimer_fiche");
        int verrous = Place(corps, "delete from public.verrous vl using public.locataires lo");
        StringAssert.Contains("and lo.entreprise_id = vl.entreprise_id and lo.id = vl.cible_id and lo.batiment_id = p_id;", corps);
        Assert.That(verrous, Is.GreaterThan(Place(corps, "order by vl.pris_le limit 1;")), "après le contrôle");
        Assert.That(verrous, Is.LessThan(Place(corps, "delete from public.batiments")), "avant la cascade : après, la jointure ne trouve plus les locataires");
    }

    // ── D1-06 : le transfert regarde les verrous de la destination ──

    [Test]
    public void D1_06_le_transfert_refuse_un_homonyme_de_la_destination_tenu_par_un_collegue()
    {
        string corps = Fonction(Sql, "transferer_fiches");
        int homonyme = Place(corps, "select id into dest_id from public.batiments");
        int batiment = Place(corps, "v := public.detenteur_autre(p_vers, 'batiment', dest_id, p_session);");
        int locataire = Place(corps, "v := public.detenteur_autre(p_vers, 'locataire', hid, p_session);");
        int ecriture = new[] { "insert into", "update public.", "delete from" }.Min(e => Place(corps, e));
        Assert.That(batiment, Is.GreaterThan(homonyme), "une fois l'homonyme connu");
        Assert.That(batiment, Is.LessThan(ecriture), "bâtiment : avant la première écriture (un « return » n'annule rien)");
        Assert.That(locataire, Is.GreaterThan(batiment).And.LessThan(ecriture), "locataires : avant la première écriture");
        StringAssert.Contains("if not p_seul then\n      v := public.detenteur_autre(p_vers, 'batiment'", corps, "le bâtiment n'est remplacé que par un transfert entier");
        // Les homonymes contrôlés sont ceux que la boucle d'écriture garde en corbeille puis supprime : même filtre.
        const string filtre = "where entreprise_id = p_vers and batiment_id = dest_id and lower(trim(data->>'Name')) = cible and id <> lid";
        StringAssert.Contains(filtre + " loop", corps, "contrôle");
        Assert.That(Fois(corps, filtre + ";"), Is.EqualTo(2), "corbeille et suppression");
        // Même refus que pour l'origine, qui reste contrôlée.
        Assert.That(Fois(corps, "return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'cible', p_batiment->>'Name');"), Is.EqualTo(2));
        Assert.That(Fois(corps, "return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'cible', l->'data'->>'Name');"), Is.EqualTo(3));
        // Même identifiant dans la destination : « on conflict … do update » le remplace, homonyme ou non, dans les deux
        // branches. Contrôlé hors du « if dest_id is not null », avant la première écriture.
        int meme = Place(corps, "v := public.detenteur_autre(p_vers, 'locataire', l->>'id', p_session);");
        Assert.That(meme, Is.GreaterThan(locataire).And.LessThan(ecriture), "même identifiant : avant la première écriture");
        Assert.That(corps.LastIndexOf("\n  end if;\n", meme, StringComparison.Ordinal),
            Is.GreaterThan(corps.LastIndexOf("  if dest_id is not null then\n", meme, StringComparison.Ordinal)), "même identifiant : hors du « if dest_id »");
        StringAssert.Contains("on conflict (entreprise_id, id) do update", corps, "le remplacement que ce contrôle protège");
        StringAssert.Contains("v := public.detenteur_autre(p_de, 'batiment', bid, p_session);", corps);
        StringAssert.Contains("v := public.detenteur_autre(p_de, 'locataire', lid, p_session);", corps);
    }

    // ── D2-10 : le refus « version » dit la version et la session de la ligne (contrat C9) ──

    [TestCase("enregistrer_batiment", "batiments")]
    [TestCase("enregistrer_locataire", "locataires")]
    public void D2_10_le_refus_version_rend_la_version_et_la_session_de_la_ligne(string nom, string table)
    {
        string corps = Fonction(Sql, nom);
        string ligne = " from public." + table + " where entreprise_id = p_entreprise and id = p_id)";
        string refus = Entre(corps, "return json_build_object('ok', false, 'refus', 'version', 'nom',", "end if;");
        StringAssert.Contains("(select maj_par" + ligne + ",", refus, "« nom » gardé : la version en service le lit");
        StringAssert.Contains("'version', (select version" + ligne + ",", refus, "version actuelle de la ligne");
        StringAssert.Contains("'maj_session', (select maj_session" + ligne + ");", refus, "session de sa dernière écriture");
        Assert.That(Place(corps, "'refus', 'supprimee'"), Is.LessThan(Place(corps, "'refus', 'version'")), "une fiche supprimée reste dite « supprimee »");
        StringAssert.Contains("return json_build_object('ok', true, 'version', nv);", corps, "succès inchangé");
    }

    // ── Clauses vitales des fonctions recréées (le 02/11, une recopie avait perdu maj_session) ──

    [TestCase("enregistrer_batiment", "batiments", "batiment")]
    [TestCase("enregistrer_locataire", "locataires", "locataire")]
    public void Les_clauses_vitales_d_un_enregistrement_sont_la(string nom, string table, string type)
    {
        string corps = Fonction(Sql, nom);
        StringAssert.Contains(Acces, corps);
        StringAssert.Contains("p_session text, p_modele int default 0)", corps, "signature en service");
        StringAssert.Contains("v := public.detenteur_autre(p_entreprise, '" + type + "', p_id, p_session);", corps, "verrou d'une autre session");
        string insert = Entre(corps, "insert into public." + table, "returning version into nv;");
        StringAssert.Contains("maj_par, maj_session, modele)", insert, "insert : la colonne");
        StringAssert.Contains("nom, p_session, coalesce(p_modele, 0))", insert, "insert : ma session");
        string update = Entre(corps, "update public." + table + " set", "returning version into nv;");
        StringAssert.Contains("version = version + 1", update);
        StringAssert.Contains("maj_session = p_session", update, "update : ma session (sinon chaque sauvegarde passe pour celle d'un collègue)");
        StringAssert.Contains("modele = greatest(modele, coalesce(p_modele, 0))", update);
        StringAssert.Contains("and version = p_version", update, "contrôle de version");
        StringAssert.Contains("return json_build_object('ok', false, 'refus', 'application');", corps, "contrôle p_modele");
        StringAssert.Contains("return json_build_object('ok', false, 'refus', 'supprimee');", corps);
    }

    [Test]
    public void Les_clauses_vitales_de_supprimer_fiche_et_de_transferer_fiches_sont_la()
    {
        string sql = Sql, supprimer = Fonction(sql, "supprimer_fiche"), transferer = Fonction(sql, "transferer_fiches");
        StringAssert.Contains(Acces, supprimer);
        StringAssert.Contains("if p_type not in ('batiment', 'locataire') then raise exception 'type inconnu'; end if;", supprimer);
        StringAssert.Contains("v := public.detenteur_autre(p_entreprise, p_type, p_id, p_session);", supprimer);
        StringAssert.Contains("delete from public.verrous where entreprise_id = p_entreprise and cible_type = p_type and cible_id = p_id;", supprimer);

        StringAssert.Contains("if not public.est_admin(p_de) or not public.est_admin(p_vers) then", transferer);
        StringAssert.Contains("if not public.ecriture_permise(g_vers) then return json_build_object('ok', false, 'refus', 'expire'); end if;", transferer);
        StringAssert.Contains("return json_build_object('ok', false, 'refus', 'application');", transferer, "contrôle p_modele");
        Assert.That(Fois(transferer, "maj_session = p_session"), Is.EqualTo(2), "bâtiment homonyme et Réglages");
        StringAssert.Contains("maj_session = excluded.maj_session;", transferer, "locataire déjà présent");
        Assert.That(Fois(transferer, "nom, p_session, coalesce(p_modele, 0))"), Is.EqualTo(3), "les trois insert portent ma session et le modèle");
        Assert.That(Fois(transferer, "version = version + 1"), Is.EqualTo(2), "bâtiment homonyme et Réglages");
        StringAssert.Contains("version = public.locataires.version + 1", transferer);
        StringAssert.Contains("raise exception 'réglages de la destination modifiés entre-temps : recommencez';", transferer);
    }

    // ── D2-18 : enregistrer_fiches, tout ou rien (contrat C8) ──

    [Test]
    public void D2_18_enregistrer_fiches_a_la_signature_du_contrat_et_passe_par_les_fonctions_unitaires()
    {
        string sql = Sql, corps = Fonction(sql, "enregistrer_fiches");
        StringAssert.Contains("create or replace function public.enregistrer_fiches(p_entreprise uuid, p_ops jsonb, p_session text, p_modele int default 0)\n"
                              + "returns json language plpgsql security definer set search_path = public as $$", sql, "contrat C8");
        StringAssert.Contains("r := public.enregistrer_batiment(p_entreprise, op->>'id', (op->>'version')::int, op->'data', p_session, p_modele);", corps);
        StringAssert.Contains("r := public.enregistrer_locataire(p_entreprise, op->>'id', op->>'batiment', coalesce((op->>'ordre')::int, 0),", corps);
        StringAssert.Contains("(op->>'version')::int, op->'data', p_session, p_modele);", corps);
        StringAssert.Contains("r := public.supprimer_fiche(p_entreprise, 'locataire', op->>'id', p_session);", corps);
        foreach (var ecriture in new[] { "insert into", "update public.", "delete from" })
            StringAssert.DoesNotContain(ecriture, corps, "aucune écriture ni contrôle recopié : tout passe par les fonctions unitaires");
        Assert.That(Place(corps, Acces), Is.LessThan(Place(corps, "for op in")), "l'accès d'abord");
        StringAssert.Contains("with ordinality as e(value, n) order by e.n loop", corps, "dans l'ordre du tableau");
        Assert.That(corps.Substring(0, Place(corps, "raise exception 'type inconnu';")).TrimEnd().EndsWith("else", StringComparison.Ordinal),
                    "tout autre type lève une erreur");
        // Succès : une entrée par fiche, dans l'ordre, 0 pour une suppression ; tableau vide : versions vides.
        StringAssert.Contains("versions jsonb := '[]'::jsonb;", corps);
        StringAssert.Contains("versions := versions || jsonb_build_object('type', op->>'type', 'id', op->>'id', 'version', coalesce((r->>'version')::int, 0));", corps);
        StringAssert.Contains("return json_build_object('ok', true, 'versions', versions);", corps);
    }

    [Test]
    public void D2_18_un_refus_annule_tout_par_une_exception_a_nous_et_rend_le_refus_complete()
    {
        string corps = Fonction(Sql, "enregistrer_fiches");
        int bloc = Place(corps, "  begin\n    for op in");
        int garde = Place(corps, "refus := r::jsonb || jsonb_build_object('type', op->>'type', 'id', op->>'id');");
        int leve = Place(corps, "raise exception 'refus' using errcode = 'RF001';");
        int gestionnaire = Place(corps, "exception when sqlstate 'RF001' then");
        int rendu = Place(corps, "return refus::json;");
        Assert.That(new[] { bloc, garde, leve, gestionnaire, rendu }, Is.Ordered, "bloc intérieur, refus gardé, exception, gestionnaire, refus rendu");
        Assert.That(Place(corps, "return json_build_object('ok', true, 'versions', versions);"), Is.GreaterThan(rendu), "le succès sort après le bloc");
        // Seule NOTRE exception est attrapée : « accès refusé », un abonnement expiré ou un type inconnu remontent.
        Assert.That(Fois(corps, "exception when"), Is.EqualTo(1), "un seul gestionnaire");
        StringAssert.DoesNotContain("when others", corps);
    }

    // ── Droits, fin du script ──

    [Test]
    public void Les_droits_du_lot_D_nomment_les_vraies_signatures_et_reservent_aux_comptes_connectes()
    {
        string sql = Sql;
        string droits = Entre(sql, "-- ── 12. Droits du lot D", "end $$;");
        foreach (var nom in new[] { "supprimer_fiche", "enregistrer_batiment", "enregistrer_locataire", "transferer_fiches", "enregistrer_fiches" })
            StringAssert.Contains("'" + nom + "(" + Types(Fonction(sql, nom)) + ")'", droits,
                                  nom + " : la signature des droits est celle de la fonction (sinon le script s'arrête : « function does not exist »)");
        StringAssert.Contains("execute format('revoke all on function public.%s from public, anon', f);", droits);
        StringAssert.Contains("execute format('grant execute on function public.%s to authenticated', f);", droits);
        StringAssert.Contains("'enregistrer_fiches(uuid, jsonb, text, int)'", droits, "contrat C8 : mêmes droits que enregistrer_batiment");
    }

    [Test]
    public void Le_script_finit_par_un_seul_notify_et_le_lot_D_est_fait()
    {
        string sql = Sql;
        Assert.That(Fois(sql, "notify pgrst"), Is.EqualTo(1), "un seul rechargement du schéma");
        Assert.That(sql.TrimEnd().EndsWith("notify pgrst, 'reload schema';\n\n-- Lot D : fait le 11/11/2026 (sections 8 à 12)", StringComparison.Ordinal),
                    "le notify est la dernière instruction ; après lui, le repère seulement");
        StringAssert.DoesNotContain("à compléter", sql);
        for (int n = 8; n <= 12; n++)
        {
            StringAssert.Contains("\n-- ── " + n + ". ", sql, "section " + n);
            StringAssert.Contains("\n--" + (n < 10 ? "   " : "  ") + n + ". ", sql, "en-tête : section " + n);
        }
    }
}
