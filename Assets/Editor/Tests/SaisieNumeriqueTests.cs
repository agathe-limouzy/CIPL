using NUnit.Framework;

/// Non-régression de M3 et M3-bis : la saisie de montants.
///
/// Règle fondatrice : **le point et la virgule sont strictement interchangeables**.
/// Selon le clavier, le séparateur décimal est tapé au pavé numérique (point) ou à la
/// frappe française (virgule) — les deux doivent donner le même nombre. Avec plusieurs
/// séparateurs, seul le DERNIER est décimal.
public class SaisieNumeriqueTests
{
    static float Valeur(string saisie)
    {
        Assert.That(SaisieNumerique.TryParse(saisie, out float v), Is.True,
                    $"« {saisie} » aurait dû être reconnu comme un nombre");
        return v;
    }

    [TestCase("3.5", "3,5", 3.5f)]
    [TestCase("1200.50", "1200,50", 1200.50f)]
    [TestCase("0.5", "0,5", 0.5f)]
    [TestCase(".5", ",5", 0.5f)]
    [TestCase("1200.", "1200,", 1200f)]
    [TestCase("-450.75", "-450,75", -450.75f)]
    [TestCase("1200.50 €", "1200,50 €", 1200.50f)]
    [TestCase("3.5 %", "3,5 %", 3.5f)]
    public void Point_et_virgule_donnent_le_meme_nombre(string avecPoint, string avecVirgule, float attendu)
    {
        Assert.That(Valeur(avecPoint), Is.EqualTo(attendu).Within(0.0001f));
        Assert.That(Valeur(avecVirgule), Is.EqualTo(attendu).Within(0.0001f));
    }

    [Test]
    public void Un_separateur_unique_reste_decimal_des_deux_cotes()
    {
        // Choix assumé : « 1,200 » vaut 1,2 — sinon « 0,500 » vaudrait 500.
        // Pour mille deux cents, il faut taper « 1200 » ou « 1 200 ».
        Assert.That(Valeur("1.200"), Is.EqualTo(1.2f).Within(0.0001f));
        Assert.That(Valeur("1,200"), Is.EqualTo(1.2f).Within(0.0001f));
    }

    [TestCase("1.200,50")]   // écriture française
    [TestCase("1,200.50")]   // écriture anglo-saxonne
    [TestCase("1 200,50")]   // espace de milliers
    [TestCase("1 200.50")]
    [TestCase("1 200,50")]   // espace insécable
    [TestCase("1 200,50")]   // espace insécable étroit
    public void Avec_plusieurs_separateurs_seul_le_dernier_est_decimal(string saisie)
    {
        Assert.That(Valeur(saisie), Is.EqualTo(1200.50f).Within(0.0001f));
    }

    [Test]
    public void Plusieurs_groupements_de_milliers()
    {
        // `float` ne garantit plus le centime au-delà de ~7 chiffres (finding M6) :
        // on vérifie l'ordre de grandeur, pas la décimale.
        Assert.That(Valeur("1.234.567,89"), Is.EqualTo(1234567.89f).Within(1f));
    }

    [Test]
    public void Champ_vide_vaut_zero_sans_erreur()
    {
        Assert.That(SaisieNumerique.TryParse("", out float v), Is.True);
        Assert.That(v, Is.EqualTo(0f));
        Assert.That(SaisieNumerique.TryParse(null, out float v2), Is.True);
        Assert.That(v2, Is.EqualTo(0f));
    }

    [TestCase("abc")]
    [TestCase(",")]
    [TestCase(".")]
    [TestCase("12 euros et des poussières")]
    public void Une_saisie_non_numerique_est_refusee(string saisie)
    {
        // Le cœur de M3 : refuser, pour que l'appelant avertisse — au lieu de
        // renvoyer 0 en silence (un taux à 0 produit une mensualité sans intérêts,
        // présentée comme un résultat valide).
        Assert.That(SaisieNumerique.TryParse(saisie, out float v), Is.False);
        Assert.That(v, Is.EqualTo(0f));
    }

    [Test]
    public void Parse_silencieux_renvoie_zero_sur_saisie_invalide()
    {
        Assert.That(SaisieNumerique.Parse("abc"), Is.EqualTo(0f));
        Assert.That(SaisieNumerique.Parse("1 200,50"), Is.EqualTo(1200.50f).Within(0.0001f));
    }
}
