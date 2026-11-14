using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot B (reprise du 09/11/2026), groupe « session » : D1-03, D1-05, D1-10, D3-19, D3-20, D3-21
/// (REVUE_17/D1.md et D3.md). D3-22 : CloudAbonnementTests (TestCase PSiret).
public class LotBSessionTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));
    static string Fonction(string f) => File.ReadAllText(Path.Combine("Serveur", "fonctions", f));
    static string Sql => string.Concat(Directory.GetFiles("Serveur", "etape*.sql").OrderBy(f => f).Select(File.ReadAllText));

    // ── D1-03 : « Se déconnecter » ne ferme que ce PC ; une session fermée sur le serveur est dite une fois ──

    [Test]
    public void D1_03_se_deconnecter_ne_ferme_que_la_session_de_ce_PC()
    {
        StringAssert.Contains("\"/auth/v1/logout?scope=local\"", Src("Cloud/CloudClient.cs"),
                              "sans scope, Supabase ferme les sessions de tous les PC de la personne");
    }

    [Test]
    public void D1_03_un_renouvellement_refuse_pour_de_bon_ferme_la_session_sauf_429()
    {
        foreach (long code in new long[] { 400, 401, 403, 404, 422 }) Assert.That(SessionCloud.RefusDefinitif(code), Is.True, code.ToString());
        foreach (long code in new long[] { 0, 200, 429, 500, 502, 503 }) Assert.That(SessionCloud.RefusDefinitif(code), Is.False, code.ToString());

        string src = Src("Cloud/SessionCloud.cs");
        Assert.That(Regex.Matches(src, @"RefusDefinitif\(r\.Code\)").Count, Is.EqualTo(2), "Restaurer (au démarrage) et Jeton (en cours de session)");
        StringAssert.DoesNotContain("(r.Code >= 400 && r.Code < 500)", src, "avant : un 429 au démarrage effaçait le jeton gardé et la copie hors ligne");
        Assert.That(SessionCloud.Fermee, Is.EqualTo("Votre connexion a été fermée sur le serveur : reconnectez-vous."));
        StringAssert.Contains("ConfirmDialog.Erreur(Fermee, DemarrageCloud.SeDeconnecter)", src, "au centre, puis l'écran de connexion");
        string jeton = src.Substring(src.IndexOf("Task<string> Jeton()", StringComparison.Ordinal));
        StringAssert.Contains("else if (Session == avant && RefusDefinitif(r.Code)) { Perdue(); return null; }", jeton,
                              "avant : le jeton périmé était rendu tel quel, « HTTP 401 » à chaque appel");
    }

    // ── D1-10 : l'expiration du jeton vient de l'horloge de ce PC ──

    [Test]
    public void D1_10_l_expiration_du_jeton_vient_d_expires_in_et_de_l_horloge_de_ce_PC()
    {
        var avant = DateTime.Now;
        var s = CloudClient.LireSession("{\"access_token\":\"a.b.c\",\"token_type\":\"bearer\",\"expires_in\":3600,\"expires_at\":0," +
                                        "\"refresh_token\":\"r\",\"user\":{\"id\":\"u1\",\"email\":\"marie@test.fr\"}}");
        Assert.That(s, Is.Not.Null);
        Assert.That(s.ExpireLe, Is.InRange(avant.AddSeconds(3600), DateTime.Now.AddSeconds(3600)),
                    "avant : expires_at (horloge du serveur) ; un PC en retard de 5 min présentait un jeton expiré");
        var horsLigne = new CloudClient.Session { access_token = "", refresh_token = "r", user = s.user };   // RestaurerHorsLigne
        Assert.That(horsLigne.ExpireLe - DateTime.Now < TimeSpan.FromMinutes(5), "sans réception : renouvelé au premier appel");
    }

    // ── D3-19 : une relecture qui échoue garde l'état connu ──

    [Test]
    public void D3_19_une_relecture_qui_echoue_garde_l_etat_connu()
    {
        AbonnementCloud.Oublier();
        AbonnementCloud.Constater("HTTP 400 abonnement expiré : lecture seule");
        var lecture = AbonnementCloud.Lire(null);   // sans entreprise : refus immédiat, sans réseau
        Assert.That(lecture.IsCompleted, Is.True);
        Assert.That(lecture.Result, Is.EqualTo("Aucune entreprise ouverte."));
        Assert.That(AbonnementCloud.Courant, Is.Not.Null, "avant : Courant = null avant l'appel, le bandeau « expiré » et les gardes disparaissaient");
        Assert.That(AbonnementCloud.Etat, Is.EqualTo(RegleAbonnement.Etat.Expire));
        AbonnementCloud.Oublier();
    }

    // ── D3-20 : l'e-mail d'invitation nomme la personne ──

    [Test]
    public void D3_20_l_invitant_de_l_email_est_la_personne_pas_le_nom_d_expedition_de_l_entreprise()
    {
        string src = Src("Cloud/UtilisateursCloud.cs");
        StringAssert.DoesNotContain("fromName", src, "le nom d'expédition est celui de l'entreprise (RegleInscription.Reglages)");
        StringAssert.Contains("string qui = await EcritureCloud.MonNom();", src.Substring(src.IndexOf("EnvoyerEmail(", StringComparison.Ordinal)));
        StringAssert.StartsWith("Bonjour,\n\nMarie Dupont vous invite à rejoindre SCI Rivoli",
                                UtilisateursCloud.CorpsEmail("Marie Dupont", "SCI Rivoli", "K7F2-9QXD", new DateTime(2026, 11, 16)));
    }

    // ── D3-21 : le code de l'essai technique (tables essai_*) a disparu ──

    [Test]
    public void D3_21_le_code_de_l_essai_technique_a_disparu_avec_ses_tables()
    {
        string client = Src("Cloud/CloudClient.cs");
        foreach (var mort in new[] { "FicheEssai", "LireFiches", "essai_" }) StringAssert.DoesNotContain(mort, client, mort);
        string fenetre = File.ReadAllText(Path.Combine(Application.dataPath, "Editor", "EssaiCloudWindow.cs"));
        foreach (var mort in new[] { "essai_fiches\"", "essai_verrous\"", "essai_prendre_verrou", "LireFiches", "TempsReel" })
            StringAssert.DoesNotContain(mort, fenetre, mort);
        StringAssert.Contains("ReglageService.SetCloudUrl(", fenetre, "la fenêtre reste le réglage du cloud en développement (DemarrageCloud)");
    }

    // ── D1-05 et D1-10 (cartes) : la lecture payante est réservée AVANT l'appel, en une instruction SQL ──

    [Test]
    public void D1_05_la_lecture_Pappers_est_reservee_avant_l_appel_payant()
    {
        string ts = Fonction("pappers.ts");
        int reservation = ts.IndexOf("service.rpc(\"prendre_lecture_pappers\"", StringComparison.Ordinal);
        int appel = ts.IndexOf("fetch(`https://api.pappers.fr", StringComparison.Ordinal);
        Assert.That(reservation, Is.GreaterThanOrEqualTo(0), "réservation absente");
        Assert.That(appel, Is.GreaterThan(reservation), "la réservation précède l'appel payant");
        StringAssert.DoesNotContain("n: (journal?.n", ts, "avant : « ancien + 1 » écrit après l'appel, avec la valeur lue avant");
        StringAssert.DoesNotContain("from(\"pappers_journal\")", ts, "le journal n'est plus lu ni écrit ici : seule la fonction SQL y touche");
    }

    [Test]
    public void D1_10_reste_cartes_a_un_plafond_par_entreprise_reserve_avant_l_appel_a_Google()
    {
        string ts = Fonction("cartes.ts");
        int reservation = ts.IndexOf("service.rpc(\"prendre_lecture_cartes\"", StringComparison.Ordinal);
        int appel = ts.IndexOf("fetch(\"https://maps.googleapis.com", StringComparison.Ordinal);
        Assert.That(reservation, Is.GreaterThanOrEqualTo(0), "réservation absente");
        Assert.That(appel, Is.GreaterThan(reservation), "la réservation précède l'appel");
        StringAssert.Contains("refus: \"quota\"", ts);
    }

    /// La fonction SQL appelée existe dans un script (SQL du lot B à intégrer), avec les paramètres envoyés, en une
    /// instruction atomique, et réservée au rôle de service.
    [TestCase("pappers.ts", "prendre_lecture_pappers")]
    [TestCase("cartes.ts", "prendre_lecture_cartes")]
    public void D1_05_la_reservation_SQL_existe_avec_les_parametres_envoyes_et_reservee_au_serveur(string fonction, string nom)
    {
        var appel = Regex.Match(Fonction(fonction), @"service\.rpc\(""" + nom + @""", \{([^}]*)\}\)");
        Assert.That(appel.Success, Is.True, nom + " non appelée par " + fonction);
        var envoyes = appel.Groups[1].Value.Split(',').Select(p => p.Split(':')[0].Trim()).OrderBy(x => x);
        string sql = Sql;
        var sig = Regex.Match(sql, @"create or replace function public\." + nom + @"\(([^)]*)\)");
        Assert.That(sig.Success, Is.True, nom + " absente des scripts Serveur/etape*.sql : intégrer le SQL du lot B (groupe session)");
        var attendus = sig.Groups[1].Value.Split(',').Select(p => p.Trim().Split(' ')[0]).OrderBy(x => x);
        Assert.That(envoyes, Is.EqualTo(attendus), nom + " : paramètres");
        string corps = sql.Substring(sig.Index, sql.IndexOf("end $$;", sig.Index, StringComparison.Ordinal) - sig.Index);
        StringAssert.Contains("on conflict (entreprise_id, jour) do update", corps, "une seule instruction : réservation atomique");
        StringAssert.Contains("< p_max", corps);
        Assert.That(Regex.IsMatch(sql, @"revoke all on function public\." + nom + @"\([^)]*\) from public, anon, authenticated;"), nom + " : révocation absente");
        Assert.That(Regex.IsMatch(sql, @"grant execute on function public\." + nom + @"\([^)]*\) to service_role;"), nom + " : droit du serveur absent");
    }
}
