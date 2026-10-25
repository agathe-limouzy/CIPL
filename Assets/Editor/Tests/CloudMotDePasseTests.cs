using NUnit.Framework;

/// Étape 12 : « Mot de passe oublié » et « Changer mon mot de passe » (décisions du 23/10).
public class CloudMotDePasseTests
{
    [Test]
    public void Le_formulaire_du_code_dit_ce_qu_il_manque()
    {
        Assert.That(RegleMotDePasse.RefusAdresse("agathe"), Does.Contain("adresse e-mail"));
        Assert.That(RegleMotDePasse.RefusAdresse("a@b.fr"), Is.Null);
        Assert.That(RegleMotDePasse.RefusCode("12345", "motdepasse", "motdepasse"), Does.Contain("6 chiffres"));
        Assert.That(RegleMotDePasse.RefusCode("123 456", "court", "court"), Does.Contain("au moins 8"));
        Assert.That(RegleMotDePasse.RefusCode("123456", "motdepasse", "autre1234"), Does.Contain("identiques"));
        Assert.That(RegleMotDePasse.RefusCode("123 456", "motdepasse", "motdepasse"), Is.Null);
        Assert.That(RegleMotDePasse.Code("123-456 "), Is.EqualTo("123456"));
    }

    [Test]
    public void Les_refus_de_Supabase_sont_dits_en_clair()
    {
        Assert.That(RegleMotDePasse.MotifEnvoi(429, "HTTP 429 For security purposes, you can only request this after 42 seconds."),
                    Does.Contain("attendez une minute"));
        Assert.That(RegleMotDePasse.MotifEnvoi(429, "HTTP 429 email rate limit exceeded"), Does.Contain("dans une heure"));
        Assert.That(RegleMotDePasse.MotifCode(403, "HTTP 403 Token has expired or is invalid"), Does.Contain("incorrect ou a expiré"));
        Assert.That(RegleMotDePasse.MotifCode(0, "Serveur injoignable"), Does.StartWith("Vérification du code impossible"));
        Assert.That(RegleMotDePasse.MotifChangement(422, "HTTP 422 New password should be different from the old password."),
                    Does.Contain("différent de l'ancien"));
    }

    [Test]
    public void Chaque_refus_designe_la_zone_a_montrer_en_rouge()
    {
        // Retour du 25/10 : la zone en cause passe en rouge.
        RegleMotDePasse.Refus("court", "court", out var c);
        Assert.That(c, Is.EqualTo(new[] { RegleMotDePasse.ChampMdp }));
        RegleMotDePasse.Refus("motdepasse", "autre1234", out c);
        Assert.That(c, Is.EqualTo(new[] { RegleMotDePasse.ChampMdp, RegleMotDePasse.ChampConfirmation }), "les deux mots de passe");
        RegleMotDePasse.Refus("motdepasse", "motdepasse", out c);
        Assert.That(c, Is.Empty);
        RegleMotDePasse.RefusAdresse("agathe", out c);
        Assert.That(c, Is.EqualTo(new[] { RegleMotDePasse.ChampEmail }));
        RegleMotDePasse.RefusCode("12", "motdepasse", "motdepasse", out c);
        Assert.That(c, Is.EqualTo(new[] { RegleMotDePasse.ChampCode }));
        Assert.That(RegleMotDePasse.ChampsCode(403), Is.EqualTo(new[] { RegleMotDePasse.ChampCode }), "code incorrect ou expiré");
        Assert.That(RegleMotDePasse.ChampsCode(429), Is.Empty, "trop tôt : rien à corriger dans la fenêtre");
        Assert.That(RegleMotDePasse.ChampsChangement(422), Is.EqualTo(new[] { RegleMotDePasse.ChampMdp, RegleMotDePasse.ChampConfirmation }));
        UtilisateursCloud.VerifierRejoindre("a@b.fr", "K7F2", "motdepasse", "motdepasse", out c);
        Assert.That(c, Is.EqualTo(new[] { RegleMotDePasse.ChampCode }), "invitation : le code");
    }

    [Test]
    public void Le_code_part_comme_Supabase_l_attend()
    {
        // /auth/v1/recover puis /auth/v1/verify (type recovery) : ni champ en trop, ni champ vide.
        Assert.That(CloudClient.CorpsDemande("a@b.fr"), Is.EqualTo("{\"email\":\"a@b.fr\"}"));
        Assert.That(CloudClient.CorpsCode("a@b.fr", "123456"), Is.EqualTo("{\"type\":\"recovery\",\"email\":\"a@b.fr\",\"token\":\"123456\"}"));
    }
}
