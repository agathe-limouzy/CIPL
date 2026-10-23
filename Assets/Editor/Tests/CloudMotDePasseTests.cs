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
    public void Le_code_part_comme_Supabase_l_attend()
    {
        // /auth/v1/recover puis /auth/v1/verify (type recovery) : ni champ en trop, ni champ vide.
        Assert.That(CloudClient.CorpsDemande("a@b.fr"), Is.EqualTo("{\"email\":\"a@b.fr\"}"));
        Assert.That(CloudClient.CorpsCode("a@b.fr", "123456"), Is.EqualTo("{\"type\":\"recovery\",\"email\":\"a@b.fr\",\"token\":\"123456\"}"));
    }
}
