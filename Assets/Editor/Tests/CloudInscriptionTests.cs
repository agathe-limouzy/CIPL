using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// Étape 13 : « Créer mon compte » (décisions du 23/10).
public class CloudInscriptionTests
{
    [Test]
    public void Le_formulaire_dit_ce_qu_il_manque()
    {
        string R(bool societe, string siret = "73282932000074", string ent = "SCI Rivoli", string prenom = "Jean", string nom = "Martin",
                 string adresse = "4 rue des Lilas, 31000 Toulouse", string monNom = "Marie Dupont", string email = "a@b.fr",
                 string mdp = "motdepasse", string conf = "motdepasse")
            => RegleInscription.Refus(societe, siret, ent, prenom, nom, adresse, monNom, email, mdp, conf);

        Assert.That(R(true), Is.Null);
        Assert.That(R(false), Is.Null);
        Assert.That(R(true, siret: "123"), Does.Contain("14 chiffres"));
        Assert.That(R(true, ent: " "), Does.Contain("raison sociale"));
        Assert.That(R(true, monNom: ""), Does.Contain("votre nom"));
        Assert.That(R(false, prenom: ""), Does.Contain("prénom et votre nom"));
        Assert.That(R(false, adresse: "4 rue des Lilas"), Does.Contain("code postal et la ville"));
        Assert.That(R(false, siret: "", monNom: ""), Is.Null, "particulier : ni SIRET ni « votre nom »");
        Assert.That(R(true, email: "agathe"), Does.Contain("adresse e-mail"));
        Assert.That(R(true, conf: "autre1234"), Does.Contain("identiques"));
    }

    [Test]
    public void Chaque_refus_designe_la_zone_a_montrer_en_rouge()
    {
        string[] C(bool societe, string siret = "73282932000074", string ent = "SCI Rivoli", string prenom = "Jean", string nom = "Martin",
                   string adresse = "4 rue des Lilas, 31000 Toulouse", string monNom = "Marie", string email = "a@b.fr",
                   string mdp = "motdepasse", string conf = "motdepasse")
        {
            RegleInscription.Refus(societe, siret, ent, prenom, nom, adresse, monNom, email, mdp, conf, out var champs);
            return champs;
        }
        Assert.That(C(true, siret: "123"), Is.EqualTo(new[] { RegleInscription.ChampSiret }));
        Assert.That(C(false, prenom: ""), Is.EqualTo(new[] { RegleInscription.ChampPrenom }));
        Assert.That(C(false, prenom: "", nom: " "), Is.EqualTo(new[] { RegleInscription.ChampPrenom, RegleInscription.ChampNom }));
        Assert.That(C(false, adresse: "4 rue des Lilas"), Is.EqualTo(new[] { RegleInscription.ChampAdresse }));
        Assert.That(C(true, ent: ""), Is.EqualTo(new[] { RegleInscription.ChampEntreprise }));
        Assert.That(C(true, monNom: ""), Is.EqualTo(new[] { RegleInscription.ChampMonNom }));
        Assert.That(C(true, email: "agathe"), Is.EqualTo(new[] { RegleMotDePasse.ChampEmail }));
        Assert.That(C(true, conf: "autre1234"), Is.EqualTo(new[] { RegleMotDePasse.ChampMdp, RegleMotDePasse.ChampConfirmation }));
        Assert.That(C(true), Is.Empty);

        Assert.That(RegleInscription.Champs(new RegleInscription.Resultat { refus = "deja_membre" }), Is.EqualTo(new[] { RegleMotDePasse.ChampEmail }));
        Assert.That(RegleInscription.Champs(new RegleInscription.Resultat { refus = "deja_sur_cipl" }), Is.EqualTo(new[] { RegleInscription.ChampSiret }));
        Assert.That(RegleInscription.Champs(null), Is.Empty);
    }

    [Test]
    public void Un_particulier_gere_le_patrimoine_de_son_nom_dans_sa_ville()
    {
        Assert.That(RegleInscription.NomParticulier(" Jean ", "Martin"), Is.EqualTo("Patrimoine de Jean Martin"));
        Assert.That(RegleInscription.NomParticulier("", ""), Is.EqualTo(""));
        Assert.That(RegleInscription.Ville("4 rue des Lilas, 31000 Toulouse"), Is.EqualTo("Toulouse"));
        Assert.That(RegleInscription.Ville("12 avenue Foch 75016 Paris "), Is.EqualTo("Paris"));
        Assert.That(RegleInscription.Ville("4 rue des Lilas"), Is.Null);

        var r = RegleInscription.Reglages(false, "Patrimoine de Jean Martin", "", null, "Jean Martin", "4 rue des Lilas, 31000 Toulouse", "jean@b.fr");
        Assert.That(r.entrepriseNom, Is.EqualTo("Patrimoine de Jean Martin"));
        Assert.That(r.basDePage, Is.EqualTo("Jean Martin - 4 rue des Lilas, 31000 Toulouse"));
        Assert.That(r.lieuEmission, Is.EqualTo("Toulouse"));
        Assert.That(r.smtp.fromName, Is.EqualTo("Patrimoine de Jean Martin"));
        Assert.That(r.smtp.fromEmail, Is.EqualTo("jean@b.fr"));
    }

    [Test]
    public void Une_societe_part_avec_son_bas_de_page_et_sa_ville_pas_ceux_de_CIPL()
    {
        var annuaire = new AnnuaireEntreprise
        {
            nom_complet = "SCI RIVOLI", libelle_nature_juridique = "Société civile immobilière",
            // L'annuaire donne le code INSEE dans « commune », le nom dans « libelle_commune » (retour du 25/10 : « 92048 »).
            siege = new AnnuaireSiege { adresse = "12 RUE DE RIVOLI 75004 PARIS", code_postal = "75004", commune = "31490", libelle_commune = "SAINT-MARCEL-PAULEL" },
        };
        var r = RegleInscription.Reglages(true, "SCI Rivoli", "732 829 320 00074", annuaire, "Marie Dupont", "", "marie@b.fr");
        Assert.That(r.basDePage, Is.EqualTo("SCI RIVOLI - Société civile immobilière - Siret : 732 829 320 00074\nSiège social : 12 RUE DE RIVOLI 75004 PARIS"));
        Assert.That(r.lieuEmission, Is.EqualTo("Saint-Marcel-Paulel"));
        Assert.That(r.basDePage, Does.Not.Contain("717 220 883"), "plus le bas de page de CIPL");
        Assert.That(r.sansLogo, Is.True, "pas le logo de CIPL (retour du 25/10)");
        Assert.That(r.modeEnvoi, Is.EqualTo(ModeEnvoi.Email), "envoi par e-mail au départ (retour du 25/10)");
        Assert.That(r.smtp.host, Is.Empty, "pas le serveur d'envoi de CIPL");
        Assert.That(new ReglageData().modeEnvoi, Is.EqualTo(ModeEnvoi.Pennylane), "les Réglages déjà enregistrés ne changent pas");
        Assert.That(new AnnuaireSiege { commune = "92048" }.Ville, Is.EqualTo(""), "jamais le code INSEE comme nom de ville");
        Assert.That(new ReglageData().sansLogo, Is.False, "les Réglages déjà enregistrés gardent le logo CIPL");

        var sansAnnuaire = RegleInscription.Reglages(true, "SCI Rivoli", "73282932000074", null, "Marie Dupont", "", "marie@b.fr");
        Assert.That(sansAnnuaire.basDePage, Is.EqualTo("SCI Rivoli - Siret : 732 829 320 00074"));
    }

    [Test]
    public void Les_refus_du_serveur_sont_dits_en_clair()
    {
        Assert.That(RegleInscription.Motif(new RegleInscription.Resultat { refus = "deja_membre", nom = "Démo CIPL" }),
                    Does.Contain("« Démo CIPL »").And.Contain("autre adresse"));
        Assert.That(RegleInscription.Motif(new RegleInscription.Resultat { refus = "deja_membre" }),
                    Does.StartWith("Cette adresse e-mail a déjà un compte CIPL : "), "avant le code : sans nommer le groupe");
        Assert.That(RegleInscription.Motif(new RegleInscription.Resultat { refus = "deja_sur_cipl" }), Does.Contain("demandez une invitation"));
        Assert.That(RegleInscription.Motif(null), Does.Contain("illisible"));
    }

    [Test]
    public void Les_appels_envoient_ce_que_Supabase_et_la_fonction_inscrire_attendent()
    {
        Assert.That(CloudClient.CorpsDemandeInscription("a@b.fr"), Is.EqualTo("{\"email\":\"a@b.fr\",\"create_user\":true}"));
        Assert.That(CloudClient.CorpsCode("a@b.fr", "123456", "email"), Is.EqualTo("{\"type\":\"email\",\"email\":\"a@b.fr\",\"token\":\"123456\"}"));

        string json = RegleInscription.Corps("societe", "SCI \"Rivoli\"", "73282932000074", "Marie", JsonUtility.ToJson(new ReglageData()));
        Assert.That(JsonUtility.FromJson<Params>(json).p_nom, Is.EqualTo("SCI \"Rivoli\""), "JSON valide, guillemets échappés");
        StringAssert.Contains("\"p_reglages\":{", json, "les Réglages partent en objet, pas en texte");

        string sql = File.ReadAllText(Path.Combine("Serveur", "etape13_supabase.sql"));
        foreach (var (fonction, corps) in new[] { ("inscrire", json), ("adresse_deja_membre", RegleInscription.CorpsAdresse(" a@b.fr ")),
                                                  ("siret_deja_sur_cipl", RegleInscription.CorpsSiret("528 808 678 00023")) })
        {
            var sig = Regex.Match(sql, @"function public\." + fonction + @"\(([^)]*)\)");
            Assert.That(sig.Success, Is.True, fonction + " absente du SQL");
            var attendus = sig.Groups[1].Value.Split(',').Select(p => p.Trim().Split(' ')[0]).OrderBy(x => x);
            var cles = Regex.Matches(corps, "\"(p_\\w+)\":").Cast<Match>().Select(m => m.Groups[1].Value).OrderBy(x => x);
            Assert.That(cles, Is.EqualTo(attendus), fonction);
        }
        Assert.That(RegleInscription.CorpsAdresse(" a@b.fr "), Is.EqualTo("{\"p_email\":\"a@b.fr\"}"));
        Assert.That(RegleInscription.CorpsSiret("528 808 678 00023"), Is.EqualTo("{\"p_siret\":\"52880867800023\"}"));
    }

    [System.Serializable] class Params { public string p_type, p_nom, p_siret, p_mon_nom; }
}
