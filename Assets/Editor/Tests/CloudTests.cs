using System;
using System.Collections.Generic;
using NUnit.Framework;

/// Essai technique (étape 1) : tout ce qui se vérifie sans réseau. Les réponses de
/// Supabase, les messages du temps réel et le libellé du verrou (les liens cipl:// ont été retirés
/// le 26/10 : invitations, mot de passe et inscription passent par des codes).
public class CloudTests
{
    // ── Réponses REST ──────────────────────────────────────────────────────────

    [Test]
    public void La_session_de_connexion_est_lue()
    {
        const string json = "{\"access_token\":\"eyJhbGciOiJIUzI1NiJ9.e30.sig\",\"token_type\":\"bearer\",\"expires_in\":3600," +
                            "\"expires_at\":1791800000,\"refresh_token\":\"r3fr3sh\",\"user\":{\"id\":\"7b0c2f1e-uuid\"," +
                            "\"aud\":\"authenticated\",\"email\":\"marie@test.fr\",\"app_metadata\":{\"provider\":\"email\"}}}";
        var s = CloudClient.LireSession(json);

        Assert.That(s, Is.Not.Null);
        Assert.That(s.access_token, Is.EqualTo("eyJhbGciOiJIUzI1NiJ9.e30.sig"));
        Assert.That(s.refresh_token, Is.EqualTo("r3fr3sh"));
        Assert.That(s.user.email, Is.EqualTo("marie@test.fr"));
        Assert.That(s.ExpireLe, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1791800000).LocalDateTime));
    }

    [Test]
    public void Une_session_incomplete_ou_illisible_est_refusee()
    {
        Assert.That(CloudClient.LireSession("{}"), Is.Null);
        Assert.That(CloudClient.LireSession("{\"access_token\":\"x\"}"), Is.Null, "sans utilisateur");
        Assert.That(CloudClient.LireSession("<html>502</html>"), Is.Null);
    }

    [Test]
    public void Le_motif_d_un_refus_est_lisible_quel_que_soit_le_format()
    {
        Assert.That(CloudClient.LireErreur(400, "{\"code\":400,\"error_code\":\"invalid_credentials\",\"msg\":\"Invalid login credentials\"}"),
                    Is.EqualTo("HTTP 400 Invalid login credentials"));
        Assert.That(CloudClient.LireErreur(400, "{\"error\":\"invalid_grant\",\"error_description\":\"Invalid Refresh Token\"}"),
                    Is.EqualTo("HTTP 400 Invalid Refresh Token"));
        Assert.That(CloudClient.LireErreur(404, "{\"code\":\"PGRST202\",\"message\":\"Could not find the function\"}"),
                    Is.EqualTo("HTTP 404 Could not find the function"));
        Assert.That(CloudClient.LireErreur(502, "<html>Bad gateway</html>"), Is.EqualTo("HTTP 502 <html>Bad gateway</html>"));
        Assert.That(CloudClient.LireErreur(0, "", "Cannot resolve destination host"),
                    Is.EqualTo("Serveur injoignable (Cannot resolve destination host)"));
    }

    [Test]
    public void La_reponse_du_verrou_dit_qui_detient_la_fiche()
    {
        var v = CloudClient.LireVerrou("{\"obtenu\":false,\"nom\":\"Marie Dupont\",\"motif\":\"facturation\"," +
                                       "\"pris_le\":\"2026-10-12T12:32:05.123456+00:00\",\"expire_le\":\"2026-10-12T12:34:05.123456+00:00\"}");
        Assert.That(v.obtenu, Is.False);
        Assert.That(v.nom, Is.EqualTo("Marie Dupont"));

        var pris = CloudClient.Date(v.pris_le).Value.ToUniversalTime();
        Assert.That((pris.Hour, pris.Minute, pris.Second), Is.EqualTo((12, 32, 5)));
        Assert.That(CloudClient.Date("pas une date"), Is.Null);
    }

    [Test]
    public void Les_lignes_d_une_table_sont_lues_meme_vides()
    {
        var f = CloudClient.LireFiches("[{\"id\":\"volteo\",\"loyer\":24600,\"maj_par\":\"Paul\",\"version\":2,\"maj_le\":\"2026-10-12T12:00:00+00:00\"}]");
        Assert.That(f.Length, Is.EqualTo(1));
        Assert.That(f[0].loyer, Is.EqualTo(24600));
        Assert.That(f[0].version, Is.EqualTo(2));

        Assert.That(CloudClient.LireFiches("[]"), Is.Empty, "version périmée : aucune ligne modifiée");
    }

    [Test]
    public void L_adresse_du_tableau_de_bord_donne_celle_de_l_API()
    {
        Assert.That(CloudClient.AdresseProjet("https://supabase.com/dashboard/project/ehxskfjmiioesmipzsad/sql/new"),
                    Is.EqualTo("https://ehxskfjmiioesmipzsad.supabase.co"));
        Assert.That(CloudClient.AdresseProjet(" https://abcd.supabase.co/ "), Is.EqualTo("https://abcd.supabase.co"),
                    "une bonne adresse reste telle quelle");
    }

    // ── Temps réel ─────────────────────────────────────────────────────────────

    [Test]
    public void L_adresse_du_temps_reel_part_de_celle_du_projet()
    {
        Assert.That(TempsReel.UrlWebSocket("https://abcd.supabase.co/", "sb_publishable_x"),
                    Is.EqualTo("wss://abcd.supabase.co/realtime/v1/websocket?apikey=sb_publishable_x&vsn=1.0.0"));
    }

    [Test]
    public void La_demande_d_abonnement_porte_les_tables_et_le_jeton()
    {
        string m = TempsReel.MessageJoin(new[] { "essai_fiches", "essai_verrous" }, "jwt.x.y", "1");

        // Les champs C# « @event » et « @ref » doivent sortir sous les noms attendus par le serveur.
        StringAssert.Contains("\"event\":\"phx_join\"", m);
        StringAssert.Contains("\"ref\":\"1\"", m);
        StringAssert.Contains("\"topic\":\"realtime:cipl\"", m);
        StringAssert.Contains("{\"event\":\"*\",\"schema\":\"public\",\"table\":\"essai_fiches\"}", m);
        StringAssert.Contains("\"table\":\"essai_verrous\"", m);
        StringAssert.Contains("\"access_token\":\"jwt.x.y\"", m);
    }

    [Test]
    public void Un_changement_recu_est_lu()
    {
        const string json = "{\"event\":\"postgres_changes\",\"payload\":{\"data\":{\"columns\":[{\"name\":\"id\",\"type\":\"text\"}]," +
                            "\"commit_timestamp\":\"2026-10-12T12:40:00.000Z\",\"errors\":null,\"record\":{\"id\":\"volteo\",\"loyer\":24600," +
                            "\"maj_par\":\"Paul Lefèvre\",\"version\":2},\"old_record\":{\"id\":\"volteo\"},\"schema\":\"public\"," +
                            "\"table\":\"essai_fiches\",\"type\":\"UPDATE\"},\"ids\":[38606455]},\"ref\":null,\"topic\":\"realtime:cipl\"}";
        var m = TempsReel.Lire(json);

        Assert.That(m.@event, Is.EqualTo("postgres_changes"));
        Assert.That(m.payload.data.type, Is.EqualTo("UPDATE"));
        Assert.That(m.payload.data.table, Is.EqualTo("essai_fiches"));
        Assert.That(m.payload.data.record.loyer, Is.EqualTo(24600));
        Assert.That(m.payload.data.record.maj_par, Is.EqualTo("Paul Lefèvre"));
    }

    [Test]
    public void Un_abonnement_refuse_donne_sa_raison()
    {
        var m = TempsReel.Lire("{\"event\":\"phx_reply\",\"payload\":{\"response\":{\"reason\":\"Invalid JWT\"},\"status\":\"error\"}," +
                               "\"ref\":\"1\",\"topic\":\"realtime:cipl\"}");
        Assert.That(m.payload.status, Is.EqualTo("error"));
        Assert.That(m.payload.response.reason, Is.EqualTo("Invalid JWT"));
        Assert.That(m.@ref, Is.EqualTo("1"));
    }

    // ── Verrou : ce que l'écran affiche ────────────────────────────────────────

    [Test]
    public void Le_libelle_du_verrou_nomme_la_personne_le_motif_et_l_heure()
    {
        Assert.That(RegleVerrou.Libelle("Marie Dupont", "facturation", new DateTime(2026, 10, 12, 14, 32, 0)),
                    Is.EqualTo("En cours de modification par Marie Dupont (facturation) depuis 14 h 32"));
        Assert.That(RegleVerrou.Libelle("Paul Lefèvre", "", new DateTime(2026, 10, 12, 9, 5, 0)),
                    Is.EqualTo("En cours de modification par Paul Lefèvre depuis 9 h 05"));
    }

    [Test]
    public void Le_verrou_se_libere_seul_apres_l_echeance()
    {
        var expire = new DateTime(2026, 10, 12, 14, 34, 0);
        Assert.That(RegleVerrou.Expire(expire, expire), Is.False, "pile à l'échéance : encore détenu");
        Assert.That(RegleVerrou.Expire(expire, expire.AddSeconds(1)), Is.True);
        Assert.That(RegleVerrou.Restant(expire, expire.AddSeconds(-105)), Is.EqualTo("1:45"));
        Assert.That(RegleVerrou.Restant(expire, expire.AddSeconds(5)), Is.EqualTo("expiré"));
    }

    // ── Étape 5 : en direct ────────────────────────────────────────────────────

    [Test]
    public void Prenom_et_initiales_viennent_du_nom_ou_de_l_e_mail()
    {
        Assert.That(RegleVerrou.Prenom("Marie Dupont"), Is.EqualTo("Marie"));
        Assert.That(RegleVerrou.Prenom("agathe.limouzy@cipl.fr"), Is.EqualTo("agathe"), "pas de nom saisi : l'e-mail");
        Assert.That(RegleVerrou.Initiales("Marie Dupont"), Is.EqualTo("MD"));
        Assert.That(RegleVerrou.Initiales("Paul"), Is.EqualTo("PA"));
        Assert.That(RegleVerrou.Initiales("agathe.limouzy@cipl.fr"), Is.EqualTo("AL"));
    }

    [Test]
    public void L_onglet_du_batiment_nomme_qui_y_modifie_le_batiment_ou_un_locataire()
    {
        var verrous = new[]
        {
            ("locataire", "sephora", "Odile Limouzy"),
            ("batiment", "rivoli", "Marie Dupont"),
            ("locataire", "decathlon", "Paul Lefèvre"),   // Le Hangar : pas rivoli
        };
        var locsRivoli = new HashSet<string> { "sephora", "starbucks" };
        Assert.That(RegleVerrou.Detenteurs(verrous, "rivoli", locsRivoli), Is.EqualTo("Odile, Marie"));
        Assert.That(RegleVerrou.Detenteurs(verrous, "auterive", new HashSet<string> { "tereva" }), Is.Null);
        Assert.That(RegleVerrou.Marque("Odile"), Is.EqualTo(" <color=#BA7517><b>· Odile</b></color>"));
        Assert.That(RegleVerrou.Marque(null), Is.Empty);
    }

    [Test]
    public void En_ligne_hors_moi_signal_recent_une_fois_par_personne()
    {
        var t = new DateTime(2026, 10, 13, 14, 0, 0);
        var presences = new[]
        {
            ("moi", "Agathe", t),
            ("s2", "Marie Dupont", t.AddSeconds(-30)),
            ("s3", "Marie Dupont", t.AddSeconds(-10)),   // Marie sur un 2e PC
            ("s4", "Paul Lefèvre", t.AddMinutes(-3)),    // plus de signal depuis 3 min
        };
        Assert.That(RegleVerrou.EnLigne(presences, "moi", t), Is.EqualTo(new[] { "Marie Dupont" }));
    }

    [Test]
    public void L_abonnement_ne_porte_que_sur_l_entreprise_ouverte()
    {
        string m = TempsReel.MessageJoinFiltre(new[] { "batiments", "locataires" }, "entreprise_id=eq.e1", "jwt", "7");
        StringAssert.Contains("\"event\":\"phx_join\"", m);
        StringAssert.Contains("{\"event\":\"*\",\"schema\":\"public\",\"table\":\"batiments\",\"filter\":\"entreprise_id=eq.e1\"}", m);
        StringAssert.Contains("\"table\":\"locataires\",\"filter\":\"entreprise_id=eq.e1\"", m);
        StringAssert.Contains("\"access_token\":\"jwt\"", m);
    }

    static TempsReel.DonneesChange Recu(string table, string type, string record, string old) =>
        TempsReel.Lire("{\"topic\":\"realtime:cipl\",\"event\":\"postgres_changes\",\"payload\":{\"data\":{\"table\":\"" + table +
                       "\",\"type\":\"" + type + "\",\"commit_timestamp\":\"2026-10-13T12:00:00Z\",\"record\":" + record +
                       ",\"old_record\":" + old + "},\"ids\":[1]},\"ref\":null}").payload.data;

    static (string, string)? Concerne(TempsReel.DonneesChange d, bool supprimeParMoi = false) =>
        DirectCloud.Concerne(d, "e1", "moi", l => l == "l1" ? "b1" : null, _ => supprimeParMoi);

    [Test]
    public void Un_changement_de_collegue_designe_le_batiment_a_relire()
    {
        const string volteo = "{\"id\":\"l1\",\"entreprise_id\":\"e1\",\"batiment_id\":\"b1\",\"maj_session\":\"s2\",\"maj_par\":\"Marie Dupont\",\"version\":4,\"data\":{\"Name\":\"Volteo\"}}";
        Assert.That(Concerne(Recu("locataires", "UPDATE", volteo, "{\"id\":\"l1\",\"entreprise_id\":\"e1\"}")),
                    Is.EqualTo(("b1", "Marie Dupont")));
        Assert.That(Concerne(Recu("batiments", "INSERT", "{\"id\":\"b9\",\"entreprise_id\":\"e1\",\"maj_session\":\"s3\",\"maj_par\":\"Paul\"}", "{}")),
                    Is.EqualTo(("b9", "Paul")));
        Assert.That(Concerne(Recu("locataires", "DELETE", "{}", "{\"id\":\"l1\",\"entreprise_id\":\"e1\"}")),
                    Is.EqualTo(("b1", (string)null)), "suppression : le bâtiment est retrouvé par le locataire");
    }

    [Test]
    public void Mes_echos_les_autres_entreprises_et_les_verrous_sont_ignores()
    {
        Assert.That(Concerne(Recu("locataires", "UPDATE", "{\"id\":\"l1\",\"entreprise_id\":\"e1\",\"batiment_id\":\"b1\",\"maj_session\":\"moi\"}", "{}")), Is.Null);
        Assert.That(Concerne(Recu("batiments", "UPDATE", "{\"id\":\"b2\",\"entreprise_id\":\"e2\",\"maj_session\":\"s2\"}", "{}")), Is.Null);
        Assert.That(Concerne(Recu("locataires", "DELETE", "{}", "{\"id\":\"l1\",\"entreprise_id\":\"e1\"}"), supprimeParMoi: true), Is.Null);
        Assert.That(Concerne(Recu("verrous", "INSERT", "{\"entreprise_id\":\"e1\"}", "{}")), Is.Null);
    }

    [Test]
    public void Quinze_minutes_sans_activite_avertissent_puis_deux_de_plus_liberent()
    {
        Assert.That(RegleVerrou.EtatInactivite(TimeSpan.FromSeconds(14 * 60 + 59)), Is.EqualTo(RegleVerrou.Presence.Actif));
        Assert.That(RegleVerrou.EtatInactivite(TimeSpan.FromMinutes(15)), Is.EqualTo(RegleVerrou.Presence.Avertir));
        Assert.That(RegleVerrou.EtatInactivite(TimeSpan.FromSeconds(16 * 60 + 59)), Is.EqualTo(RegleVerrou.Presence.Avertir));
        Assert.That(RegleVerrou.EtatInactivite(TimeSpan.FromMinutes(17)), Is.EqualTo(RegleVerrou.Presence.Liberer));
    }
}
