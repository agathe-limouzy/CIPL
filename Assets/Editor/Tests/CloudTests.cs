using System;
using NUnit.Framework;

/// Essai technique (étape 1) : tout ce qui se vérifie sans réseau. Les réponses de
/// Supabase, les messages du temps réel, le lien cipl:// et le libellé du verrou.
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

    // ── Lien cipl:// ───────────────────────────────────────────────────────────

    [Test]
    public void Un_lien_valide_donne_son_type_et_son_jeton()
    {
        Assert.That(LienCipl.Analyser("cipl://invitation?jeton=Ab12Cd34Ef", out var l), Is.True);
        Assert.That((l.Type, l.Jeton), Is.EqualTo(("invitation", "Ab12Cd34Ef")));

        // Windows ajoute parfois une barre et des guillemets ; majuscules acceptées.
        Assert.That(LienCipl.Analyser("\"CIPL://MotDePasse/?jeton=xyz_1234-AB&lang=fr\"", out l), Is.True);
        Assert.That((l.Type, l.Jeton), Is.EqualTo(("motdepasse", "xyz_1234-AB")));
    }

    [Test]
    public void Un_lien_douteux_est_refuse()
    {
        Assert.That(LienCipl.Analyser("cipl://supprimer?jeton=Ab12Cd34Ef", out _), Is.False, "type inconnu");
        Assert.That(LienCipl.Analyser("cipl://invitation?code=Ab12Cd34Ef", out _), Is.False, "pas de jeton");
        Assert.That(LienCipl.Analyser("cipl://invitation?jeton=court", out _), Is.False, "jeton trop court");
        Assert.That(LienCipl.Analyser("cipl://invitation?jeton=Ab12%22%3Bdel", out _), Is.False, "caractères interdits");
        Assert.That(LienCipl.Analyser("https://cipl.app/o/invitation?jeton=Ab12Cd34Ef", out _), Is.False, "autre protocole");
        Assert.That(LienCipl.Analyser(null, out _), Is.False);
    }

    [Test]
    public void Le_lien_est_trouve_parmi_les_arguments_de_lancement()
    {
        var args = new[] { @"C:\CIPL\CIPL.exe", "-logFile", "log.txt", "cipl://confirmation?jeton=Zz99Yy88Xx" };
        Assert.That(LienCipl.TrouverDansArguments(args, out var l, out string brut), Is.True);
        Assert.That(l.Type, Is.EqualTo("confirmation"));
        Assert.That(brut, Is.EqualTo(args[3]));

        Assert.That(LienCipl.TrouverDansArguments(new[] { @"C:\CIPL\CIPL.exe" }, out _, out brut), Is.False);
        Assert.That(brut, Is.Null);
    }

    [Test]
    public void Windows_relance_CIPL_avec_le_lien_entre_guillemets()
    {
        var cmd = LienCipl.CommandesRegistre(@"C:\Program Files\CIPL\CIPL.exe");
        Assert.That(cmd.Length, Is.EqualTo(3));
        StringAssert.Contains(@"HKCU\Software\Classes\cipl", cmd[0]);
        StringAssert.Contains("\"URL Protocol\"", cmd[1]);
        // Valeur de la commande : "C:\Program Files\CIPL\CIPL.exe" "%1" (guillemets échappés pour reg.exe).
        StringAssert.Contains("/d \"\\\"C:\\Program Files\\CIPL\\CIPL.exe\\\" \\\"%1\\\"\"", cmd[2]);
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

    [Test]
    public void Quinze_minutes_sans_activite_avertissent_puis_deux_de_plus_liberent()
    {
        Assert.That(RegleVerrou.EtatInactivite(TimeSpan.FromSeconds(14 * 60 + 59)), Is.EqualTo(RegleVerrou.Presence.Actif));
        Assert.That(RegleVerrou.EtatInactivite(TimeSpan.FromMinutes(15)), Is.EqualTo(RegleVerrou.Presence.Avertir));
        Assert.That(RegleVerrou.EtatInactivite(TimeSpan.FromSeconds(16 * 60 + 59)), Is.EqualTo(RegleVerrou.Presence.Avertir));
        Assert.That(RegleVerrou.EtatInactivite(TimeSpan.FromMinutes(17)), Is.EqualTo(RegleVerrou.Presence.Liberer));
    }
}
