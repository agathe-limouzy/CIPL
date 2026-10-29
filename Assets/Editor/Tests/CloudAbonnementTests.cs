using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// Étape 9c1 : état de l'abonnement, bandeaux, prix, SIRET, onglets de Réglages par rôle.
public class CloudAbonnementTests
{
    static readonly DateTime Fin = new DateTime(2026, 10, 26);

    [Test]
    public void L_essai_dure_jusqu_a_sa_date_de_fin_incluse_puis_tout_passe_en_lecture_seule()
    {
        Assert.That(RegleAbonnement.Lire("essai", Fin, new DateTime(2026, 10, 19)), Is.EqualTo(RegleAbonnement.Etat.Essai));
        Assert.That(RegleAbonnement.Lire("essai", Fin, Fin), Is.EqualTo(RegleAbonnement.Etat.Essai), "le dernier jour compte");
        Assert.That(RegleAbonnement.Lire("essai", Fin, new DateTime(2026, 10, 27)), Is.EqualTo(RegleAbonnement.Etat.Expire));
        Assert.That(RegleAbonnement.Lire("actif", Fin, new DateTime(2027, 1, 1)), Is.EqualTo(RegleAbonnement.Etat.Actif));
        Assert.That(RegleAbonnement.Lire("refuse", Fin, new DateTime(2027, 1, 1)), Is.EqualTo(RegleAbonnement.Etat.PaiementRefuse));
        Assert.That(RegleAbonnement.Lire("expire", Fin, new DateTime(2026, 10, 19)), Is.EqualTo(RegleAbonnement.Etat.Expire));

        // Paiement refusé : Stripe réessaie, on peut toujours écrire.
        Assert.That(RegleAbonnement.PeutEcrire(RegleAbonnement.Etat.PaiementRefuse), Is.True);
        Assert.That(RegleAbonnement.PeutEcrire(RegleAbonnement.Etat.Expire), Is.False);
    }

    [Test]
    public void Le_bandeau_d_essai_parait_a_tous_les_7_derniers_jours()
    {
        Assert.That(RegleAbonnement.JoursRestants(Fin, new DateTime(2026, 10, 19)), Is.EqualTo(7));
        Assert.That(RegleAbonnement.JoursRestants(Fin, Fin), Is.EqualTo(0));
        Assert.That(RegleAbonnement.BandeauEssai(RegleAbonnement.Etat.Essai, 7), Is.True);
        Assert.That(RegleAbonnement.BandeauEssai(RegleAbonnement.Etat.Essai, 8), Is.False);
        Assert.That(RegleAbonnement.BandeauEssai(RegleAbonnement.Etat.Actif, 3), Is.False);
        Assert.That(RegleAbonnement.TexteEssai(7, true, "agathe Limouzy"), Is.EqualTo("Essai gratuit : 7 jours restants."));
        Assert.That(RegleAbonnement.TexteEssai(1, true, null), Is.EqualTo("Essai gratuit : 1 jour restant."));
        Assert.That(RegleAbonnement.TexteEssai(0, true, null), Is.EqualTo("Essai gratuit : dernier jour."));
        // Retour du 19/10 : Odile le voit aussi, avec le nom de l'admin (le bouton reste à l'admin).
        Assert.That(RegleAbonnement.TexteEssai(5, false, "agathe Limouzy"),
                    Is.EqualTo("Essai gratuit : 5 jours restants. Voyez avec agathe Limouzy (admin)."));
        Assert.That(RegleAbonnement.Pastille(RegleAbonnement.Etat.Essai, 7), Is.EqualTo("Essai gratuit · 7 jours restants"));
    }

    [Test]
    public void Expire_l_admin_est_renvoye_a_l_abonnement_les_autres_a_l_admin()
    {
        Assert.That(RegleAbonnement.TexteExpire(false, "agathe Limouzy"),
                    Is.EqualTo("Abonnement expiré : CIPL est en lecture seule. Voyez avec agathe Limouzy (admin)."));
        Assert.That(RegleAbonnement.TexteExpire(true, "agathe Limouzy"), Is.EqualTo("Abonnement expiré : CIPL est en lecture seule."));
        StringAssert.Contains("Réglages › Abonnement", RegleAbonnement.Refus(true, null));
        StringAssert.Contains("votre administrateur", RegleAbonnement.Refus(false, ""));
    }

    [Test]
    public void Prix_premiere_entreprise_suivantes_a_moins_30_pour_cent_et_licences()
    {
        Assert.That(RegleAbonnement.Total(3, 5), Is.EqualTo(171), "exemple du §13 : 40 + 2 × 28 + 5 × 15");
        Assert.That(RegleAbonnement.Total(1, 3), Is.EqualTo(85));
        Assert.That(RegleAbonnement.Total(2, 3), Is.EqualTo(113));
        Assert.That(RegleAbonnement.Total(0, 3), Is.EqualTo(0));
    }

    [Test]
    public void Le_SIRET_compte_14_chiffres_avec_une_cle_valide()
    {
        Assert.That(RegleAbonnement.VerifierSiret("732 829 320 00074"), Is.Null);
        Assert.That(RegleAbonnement.VerifierSiret("35600000000048"), Is.Null, "La Poste, siège");
        StringAssert.Contains("n'existe pas", RegleAbonnement.VerifierSiret("73282932000075"));
        StringAssert.Contains("14 chiffres", RegleAbonnement.VerifierSiret("1234"));
        StringAssert.Contains("14 chiffres", RegleAbonnement.VerifierSiret("7328293200007A"));
        StringAssert.Contains("14 chiffres", RegleAbonnement.VerifierSiret(null));
    }

    [Test]
    public void Onglets_de_Reglages_selon_le_role()
    {
        // Étape 16b : plus de token Mapbox, un utilisateur ne voit plus Intégrations (réservé à l'admin).
        Assert.That(ReglagePanel.Onglets(cloud: true, admin: false),
                    Is.EqualTo(new[] { "Entreprise", "Facturation", "Documents et affichage" }));
        Assert.That(ReglagePanel.Onglets(cloud: true, admin: true),
                    Is.EqualTo(new[] { "Entreprise", "Facturation", "Utilisateurs", "Abonnement", "Documents et affichage", "Intégrations" }));
        var local = ReglagePanel.Onglets(cloud: false, admin: true);
        Assert.That(local, Does.Contain("Intégrations"));
        Assert.That(local, Does.Not.Contain("Utilisateurs").And.Not.Contain("Abonnement"));
    }

    [Test]
    public void Le_SIRET_de_l_entreprise_s_affiche_et_ses_refus_se_lisent()
    {
        Assert.That(RegleAbonnement.LigneSiret("societe", "12345678900012"), Is.EqualTo("123 456 789 00012"));
        Assert.That(RegleAbonnement.LigneSiret("particulier", null), Is.EqualTo("Particulier : pas de SIRET"));
        Assert.That(RegleAbonnement.LigneSiret("societe", ""), Is.EqualTo("Non renseigné"), "serveur pas encore à jour");

        // Chaque refus de changer_siret (etape16_supabase.sql) a son message, pas « Changement impossible ».
        string sql = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Serveur", "etape16_supabase.sql"));
        foreach (var refus in new[] { "siret", "deja_sur_cipl", "particulier", "abonnement" })
        {
            StringAssert.Contains($"'refus', '{refus}'", sql);
            StringAssert.DoesNotStartWith("Changement impossible", AbonnementCloud.MotifSiret(new AbonnementCloud.Ajout { refus = refus }));
        }
        StringAssert.StartsWith("Changement impossible : ", AbonnementCloud.MotifSiret(new AbonnementCloud.Ajout { refus = "erreur", erreur = "x" }));
    }

    // ── Étape 9c2 : paiement avec Stripe (décisions du 22/10) ──────────────────

    [Test]
    public void Prix_HT_TVA_20_pour_cent_en_plus_et_montants_en_euros()
    {
        Assert.That(RegleAbonnement.Ttc(85), Is.EqualTo(102m));
        Assert.That(RegleAbonnement.Ttc(113), Is.EqualTo(135.6m));
        Assert.That(RegleAbonnement.Euros(102m), Is.EqualTo("102 €"));
        Assert.That(RegleAbonnement.Euros(135.6m), Is.EqualTo("135,60 €"));
        Assert.That(RegleAbonnement.Euros(RegleAbonnement.Ttc(85) - 85), Is.EqualTo("17 €"));
    }

    [Test]
    public void Premier_prelevement_le_lendemain_de_l_essai_si_Stripe_a_48_h_d_avance()
    {
        var lendemain = new DateTime(2026, 10, 27);
        Assert.That(RegleAbonnement.PremierPrelevement(RegleAbonnement.Etat.Essai, Fin, new DateTime(2026, 10, 19)), Is.EqualTo(lendemain));
        Assert.That(RegleAbonnement.PremierPrelevement(RegleAbonnement.Etat.Essai, Fin, new DateTime(2026, 10, 24)), Is.EqualTo(lendemain), "3 jours d'écart");
        Assert.That(RegleAbonnement.PremierPrelevement(RegleAbonnement.Etat.Essai, Fin, new DateTime(2026, 10, 25)),
                    Is.EqualTo(new DateTime(2026, 10, 25)), "moins de 48 h : tout de suite");
        Assert.That(RegleAbonnement.PremierPrelevement(RegleAbonnement.Etat.Expire, Fin, new DateTime(2026, 11, 2)), Is.EqualTo(new DateTime(2026, 11, 2)));
        Assert.That(RegleAbonnement.TextePrelevement(lendemain, new DateTime(2026, 10, 19)),
                    Is.EqualTo("Premier prélèvement le 27/10/2026, à la fin de l'essai."));
        Assert.That(RegleAbonnement.TextePrelevement(new DateTime(2026, 10, 25), new DateTime(2026, 10, 25)), Is.EqualTo("Premier prélèvement aujourd'hui."));
    }

    [Test]
    public void Licences_au_prorata_jamais_moins_que_les_personnes()
    {
        Assert.That(RegleAbonnement.LicencesMin(0), Is.EqualTo(1));
        Assert.That(RegleAbonnement.LicencesMin(2), Is.EqualTo(2));
        Assert.That(RegleAbonnement.TexteLicences(1, annuel: false), Is.EqualTo("Ajouter 1 licence : +15 € HT par mois, au prorata dès aujourd'hui."));
        Assert.That(RegleAbonnement.TexteLicences(-1, annuel: true), Is.EqualTo("Retirer 1 licence : -150 € HT par an, avec un avoir au prorata."));
        // Licences par entreprise (décision du 23/10) : la confirmation nomme l'entreprise.
        Assert.That(RegleAbonnement.TexteLicences(1, false, "SCI Rivoli"),
                    Is.EqualTo("Ajouter 1 licence à SCI Rivoli : +15 € HT par mois, au prorata dès aujourd'hui."));
        Assert.That(RegleAbonnement.TexteLicences(-1, false, "SCI Rivoli"),
                    Is.EqualTo("Retirer 1 licence de SCI Rivoli : -15 € HT par mois, avec un avoir au prorata."));
    }

    [Test]
    public void Abonnement_en_cours_resilie_et_paiement_refuse()
    {
        var echeance = new DateTime(2026, 11, 27);
        Assert.That(RegleAbonnement.Pastille(RegleAbonnement.Etat.Actif, 0, "mensuel"), Is.EqualTo("Actif · mensuel"));
        Assert.That(RegleAbonnement.Pastille(RegleAbonnement.Etat.Actif, 0, "annuel", resilie: true, echeance: echeance),
                    Is.EqualTo("Actif jusqu'au 27/11/2026 · résilié"));
        Assert.That(RegleAbonnement.Explication(RegleAbonnement.Etat.Actif, Fin, echeance), Is.EqualTo("Prochaine échéance le 27/11/2026."));
        StringAssert.Contains("lecture seule le 27/11/2026", RegleAbonnement.Explication(RegleAbonnement.Etat.Actif, Fin, echeance, resilie: true));
        // Bandeau orange de paiement refusé : l'admin seulement.
        Assert.That(RegleAbonnement.BandeauRefus(RegleAbonnement.Etat.PaiementRefuse, admin: true), Is.True);
        Assert.That(RegleAbonnement.BandeauRefus(RegleAbonnement.Etat.PaiementRefuse, admin: false), Is.False);
        Assert.That(RegleAbonnement.BandeauRefus(RegleAbonnement.Etat.Actif, admin: true), Is.False);
    }

    [Test]
    public void Resilier_depuis_CIPL_prend_effet_a_l_echeance()
    {
        Assert.That(RegleAbonnement.TexteResiliation(new DateTime(2026, 11, 27)),
                    Is.EqualTo("Votre abonnement reste actif jusqu'au 27/11/2026. Ensuite, CIPL passe en lecture seule pour tout le groupe. Vous pourrez le reprendre d'ici là."));
        StringAssert.StartsWith("Votre abonnement reste actif jusqu'à la fin de la période en cours.", RegleAbonnement.TexteResiliation(null));
    }

    [Test]
    public void Reprendre_l_abonnement_se_confirme_avec_la_date_du_prochain_prelevement()
    {
        Assert.That(RegleAbonnement.TexteReprise(new DateTime(2026, 11, 22)),
                    Is.EqualTo("Votre abonnement continue après le 22/11/2026 : le prochain prélèvement aura lieu à cette date, et CIPL reste modifiable."));
        StringAssert.Contains("fin de la période en cours", RegleAbonnement.TexteReprise(null));
    }

    [Test]
    /// Retour du 22/10 : un bandeau dès la résiliation, puis un avertissement les 7 derniers jours.
    public void Resilie_un_bandeau_des_la_resiliation_puis_un_avertissement_7_jours_avant()
    {
        var echeance = new DateTime(2026, 11, 22);
        Assert.That(RegleAbonnement.BandeauResilie(RegleAbonnement.Etat.Actif, true, echeance, new DateTime(2026, 10, 22)), Is.True, "dès la résiliation");
        Assert.That(RegleAbonnement.BandeauResilie(RegleAbonnement.Etat.Actif, false, echeance, new DateTime(2026, 11, 20)), Is.False, "pas résilié");
        Assert.That(RegleAbonnement.BandeauResilie(RegleAbonnement.Etat.Actif, true, null, new DateTime(2026, 11, 20)), Is.False);
        Assert.That(RegleAbonnement.AlerteResilie(echeance, new DateTime(2026, 11, 14)), Is.False, "8 jours avant : information");
        Assert.That(RegleAbonnement.AlerteResilie(echeance, new DateTime(2026, 11, 15)), Is.True, "7 jours avant : avertissement");

        Assert.That(RegleAbonnement.TexteResilie(echeance, new DateTime(2026, 10, 22), admin: true, "agathe Limouzy"),
                    Is.EqualTo("Abonnement résilié : CIPL passera en lecture seule le 22/11/2026."));
        Assert.That(RegleAbonnement.TexteResilie(echeance, new DateTime(2026, 11, 19), admin: true, "agathe Limouzy"),
                    Is.EqualTo("Abonnement résilié : 3 jours restants avant la lecture seule (le 22/11/2026)."));
        Assert.That(RegleAbonnement.TexteResilie(echeance, echeance, admin: false, "agathe Limouzy"),
                    Is.EqualTo("Abonnement résilié : dernier jour avant la lecture seule (le 22/11/2026). Voyez avec agathe Limouzy (admin)."));
    }

    /// Chaque action envoyée par AbonnementCloud est traitée par la fonction serveur (sinon : « Action inconnue »).
    [TestCase("choisir")] [TestCase("portail")] [TestCase("ajuster")] [TestCase("resilier")] [TestCase("reprendre")]
    public void La_fonction_serveur_connait_chaque_action_de_CIPL(string action)
    {
        string ts = File.ReadAllText(Path.Combine("Serveur", "fonctions", "abonnement-stripe.ts"));
        string cs = File.ReadAllText(Path.Combine("Assets", "Script", "Cloud", "AbonnementCloud.cs"));
        StringAssert.Contains($"\"{action}\"", cs, "action absente de AbonnementCloud");
        StringAssert.Contains($"action === \"{action}\"", ts, "action non traitée par abonnement-stripe.ts");
    }

    /// Ce que CIPL envoie à la fonction serveur abonnement-stripe : exactement ce qu'elle lit.
    [Test]
    public void Le_corps_envoye_a_Stripe_est_celui_que_lit_la_fonction_serveur()
    {
        string ts = File.ReadAllText(Path.Combine("Serveur", "fonctions", "abonnement-stripe.ts"));
        var lus = Regex.Match(ts, @"const \{([^}]*)\} = await req\.json\(\)");
        Assert.That(lus.Success, Is.True, "lecture du corps introuvable dans abonnement-stripe.ts");
        var attendus = lus.Groups[1].Value.Split(',').Select(x => x.Trim()).OrderBy(x => x);
        var type = typeof(AbonnementCloud).GetNestedType("PStripe", BindingFlags.NonPublic);
        var champs = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).OrderBy(x => x);
        Assert.That(champs, Is.EqualTo(attendus));
    }

    /// Même piège qu'en 9b (Retirer, 19/10) : un paramètre en trop rend la fonction SQL introuvable.
    [TestCase("PEntreprise", "abonnement")]
    [TestCase("PAjout", "ajouter_entreprise")]
    public void Les_parametres_envoyes_sont_ceux_de_la_fonction_SQL(string classe, string fonction)
    {
        string sql = File.ReadAllText(Path.Combine("Serveur", "etape9c1_supabase.sql"));
        var sig = Regex.Match(sql, @"function public\." + fonction + @"\(([^)]*)\)");
        Assert.That(sig.Success, Is.True, fonction + " absente du SQL");
        var attendus = sig.Groups[1].Value.Split(',').Select(p => p.Trim().Split(' ')[0]).OrderBy(x => x);
        var type = typeof(AbonnementCloud).GetNestedType(classe, BindingFlags.NonPublic);
        var champs = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).OrderBy(x => x);
        Assert.That(champs, Is.EqualTo(attendus));
    }

    // ── Étape 14 : e-mails de l'abonnement (fonction serveur emails-abonnement) ─────────────

    static string Sql14 => File.ReadAllText(Path.Combine("Serveur", "etape14_supabase.sql"));
    static string Emails => File.ReadAllText(Path.Combine("Serveur", "fonctions", "emails-abonnement.ts"));

    /// Les adresses des admins et la clé de la tâche : jamais lisibles par CIPL ni par un visiteur.
    [Test]
    public void Les_fonctions_des_emails_sont_reservees_au_serveur()
    {
        string sql = Sql14;
        var fonctions = Regex.Matches(sql, @"create or replace function public\.(\w+)\(").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        Assert.That(fonctions, Has.Count.EqualTo(5));
        foreach (var f in fonctions)
        {
            Assert.That(Regex.IsMatch(sql, @"revoke all on function public\." + f + @"\([^)]*\) from public, anon, authenticated;"), f + " : révocation absente");
            Assert.That(Regex.IsMatch(sql, @"grant execute on function public\." + f + @"\([^)]*\) to service_role;"), f + " : droit du serveur absent");
        }
        Assert.That(sql, Does.Contain("enable row level security"), "envois_abonnement doit être fermée");
    }

    /// Même piège qu'en 9b : un paramètre mal nommé rend la fonction SQL introuvable.
    [Test]
    public void Les_appels_de_la_fonction_serveur_ont_les_parametres_du_SQL()
    {
        string sql = Sql14;
        var appels = Regex.Matches(Emails, @"service\.rpc\(""(\w+)"", \{([^}]*)\}\)").Cast<Match>().ToList();
        Assert.That(appels, Has.Count.EqualTo(5));
        foreach (var a in appels)
        {
            var sig = Regex.Match(sql, @"function public\." + a.Groups[1].Value + @"\(([^)]*)\)");
            Assert.That(sig.Success, Is.True, a.Groups[1].Value + " absente du SQL");
            var parametres = sig.Groups[1].Value.Split(',').Where(p => p.Trim() != "").ToList();
            var envoyes = a.Groups[2].Value.Split(',').Where(p => p.Trim() != "").Select(p => p.Split(':')[0].Trim()).ToList();
            var obligatoires = parametres.Where(p => !p.Contains(" default ")).Select(p => p.Trim().Split(' ')[0]);
            Assert.That(envoyes, Is.SubsetOf(parametres.Select(p => p.Trim().Split(' ')[0])), a.Groups[1].Value);
            Assert.That(obligatoires, Is.SubsetOf(envoyes), a.Groups[1].Value);
        }
    }

    /// Chaque e-mail demandé (par le SQL du matin ou par stripe-webhook) a son texte.
    [Test]
    public void Chaque_email_demande_a_son_texte()
    {
        string webhook = File.ReadAllText(Path.Combine("Serveur", "fonctions", "stripe-webhook.ts"));
        var sortes = Regex.Matches(Sql14, @"then '(\w+)'").Cast<Match>().Select(m => m.Groups[1].Value)
            .Concat(Regex.Matches(webhook, @"prevenir\(r\.groupe, ""(\w+)""").Cast<Match>().Select(m => m.Groups[1].Value)).ToList();
        Assert.That(sortes, Is.EquivalentTo(new[] { "essai_j7", "essai_j1", "essai_fin", "abonnement_fin", "resiliation" }));
        foreach (var s in sortes) Assert.That(Emails, Does.Contain($"case \"{s}\":"), s);
    }

    /// Retour du 26/10 (« L'c3©quipe ») : le quoted-printable de denomailer 1.6.0 perd un accent à
    /// sa dernière coupure, après 74 caractères encodés. Le corps est donc encodé en base64 par la
    /// fonction ; l'objet, encodé par denomailer, doit rester sous les 74.
    [Test]
    public void Les_emails_evitent_le_quoted_printable_de_denomailer()
    {
        string ts = Emails;
        Assert.That(ts, Does.Contain("transferEncoding: \"base64\""));
        Assert.That(Regex.IsMatch(ts, @"\b(content|html): lignes"), Is.False, "corps confié à denomailer");
        var objets = Regex.Matches(ts, @"objet: [`""]([^`""]*)[`""]").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        Assert.That(objets, Has.Count.EqualTo(5));
        foreach (var o in objets)
        {
            int encode = o.Sum(c => c >= 32 && c <= 126 && c != '=' ? 1 : 3 * System.Text.Encoding.UTF8.GetByteCount(c.ToString()));
            Assert.That(encode, Is.LessThanOrEqualTo(74), o);
        }
    }
}
