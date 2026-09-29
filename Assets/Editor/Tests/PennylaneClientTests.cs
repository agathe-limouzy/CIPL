using System.Globalization;
using System.Linq;
using NUnit.Framework;

/// Ce que Pennylane refuse d'office : des lignes dont la somme TTC n'égale pas le
/// total (422), un numéro avec espace ou accent, une adresse sans code postal.
/// Tout est vérifié ici sans réseau.
public class PennylaneClientTests
{
    static decimal D(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    static void AssertEquilibre(PennylaneClient.Import imp)
    {
        Assert.That(imp.invoice_lines.Sum(l => D(l.currency_amount)), Is.EqualTo(D(imp.currency_amount)), "somme TTC des lignes");
        Assert.That(imp.invoice_lines.Sum(l => D(l.currency_tax)), Is.EqualTo(D(imp.currency_tax)), "somme TVA des lignes");
        Assert.That(D(imp.currency_amount_before_tax) + D(imp.currency_tax), Is.EqualTo(D(imp.currency_amount)));
    }

    [Test]
    public void Loyer_et_provision_tombent_juste_au_centime()
    {
        var imp = PennylaneClient.Montants("Loyer Avril 2026", 4293.22f, 150f, true);

        Assert.That(imp.currency_amount_before_tax, Is.EqualTo("4443.22"));
        Assert.That(imp.currency_tax, Is.EqualTo("888.64"));
        Assert.That(imp.currency_amount, Is.EqualTo("5331.86"));
        Assert.That(imp.invoice_lines.Count, Is.EqualTo(2));
        Assert.That(imp.invoice_lines[0].vat_rate, Is.EqualTo("FR_200"));
        AssertEquilibre(imp);
    }

    [Test]
    public void L_ecart_d_arrondi_entre_lignes_est_absorbe()
    {
        // 20,006 + 20,006 arrondis séparément = 40,02 ; sur le total HT = 40,01 (ce
        // qu'imprime le PDF). La provision prend 20,00 pour que tout tombe juste.
        var imp = PennylaneClient.Montants("Loyer", 100.03f, 100.03f, true);

        Assert.That(imp.currency_tax, Is.EqualTo("40.01"));
        Assert.That(imp.invoice_lines[1].currency_tax, Is.EqualTo("20.00"));
        AssertEquilibre(imp);
    }

    [Test]
    public void Sans_provision_une_seule_ligne()
    {
        var imp = PennylaneClient.Montants("Loyer", 5097.30f, 0f, true);

        Assert.That(imp.invoice_lines.Count, Is.EqualTo(1));
        Assert.That(imp.currency_tax, Is.EqualTo("1019.46"));
        AssertEquilibre(imp);
    }

    [Test]
    public void Sans_TVA_les_lignes_sont_exonerees()
    {
        var imp = PennylaneClient.Montants("Loyer", 800f, 50f, false);

        Assert.That(imp.currency_tax, Is.EqualTo("0.00"));
        Assert.That(imp.invoice_lines.All(l => l.vat_rate == "exempt"));
        AssertEquilibre(imp);
    }

    [Test]
    public void Le_SIREN_sort_du_SIRET_meme_avec_espaces()
    {
        Assert.That(PennylaneClient.Siren("494 972 698 00034"), Is.EqualTo("494972698"));
        Assert.That(PennylaneClient.Siren("494972698"), Is.EqualTo("494972698"));
        Assert.That(PennylaneClient.Siren(""), Is.Null);
        Assert.That(PennylaneClient.Siren(null), Is.Null);
    }

    [Test]
    public void Numero_accepte_ou_refuse()
    {
        Assert.That(PennylaneClient.NumeroValide("2026/09001"), Is.True);
        Assert.That(PennylaneClient.NumeroValide("2026/09001 corrigée(1)"), Is.False, "correction : espace + accent");
        Assert.That(PennylaneClient.NumeroValide(new string('1', 36)), Is.False);
        Assert.That(PennylaneClient.NumeroValide(""), Is.False);
    }

    [Test]
    public void Une_fiche_sans_email_n_efface_pas_celui_de_Pennylane()
    {
        var adr = new PennylaneClient.Adresse { address = "1 rue X", postal_code = "31000", city = "Toulouse" };

        string maj = UnityEngine.JsonUtility.ToJson(PennylaneClient.FicheClient("Volteo", adr, "  "));
        Assert.That(maj, Does.Not.Contain("emails"), "mise à jour sans email : le champ doit être absent");
        Assert.That(maj, Does.Not.Contain("reg_no"), "le SIREN ne se réécrit pas");

        string majEmail = UnityEngine.JsonUtility.ToJson(PennylaneClient.FicheClient("Volteo", adr, "compta@volteo.fr"));
        Assert.That(majEmail, Does.Contain("\"emails\":[\"compta@volteo.fr\"]"));

        string creation = UnityEngine.JsonUtility.ToJson(PennylaneClient.FicheClient("Volteo", adr, "", "494972698"));
        Assert.That(creation, Does.Contain("\"reg_no\":\"494972698\""));
        Assert.That(creation, Does.Contain("\"city\":\"Toulouse\""));
    }

    [Test]
    public void Adresse_sur_plusieurs_lignes_ou_une_seule()
    {
        Assert.That(PennylaneClient.DecouperAdresse("12 rue de la Paix\nZone Nord\n31000 Toulouse", out var a), Is.True);
        Assert.That(a.address, Is.EqualTo("12 rue de la Paix, Zone Nord"));
        Assert.That(a.postal_code, Is.EqualTo("31000"));
        Assert.That(a.city, Is.EqualTo("Toulouse"));

        Assert.That(PennylaneClient.DecouperAdresse("6 route d'Agde 31590 St Marcel Paulel, France", out var b), Is.True);
        Assert.That(b.address, Is.EqualTo("6 route d'Agde"));
        Assert.That(b.city, Is.EqualTo("St Marcel Paulel"));

        Assert.That(PennylaneClient.DecouperAdresse("12 rue sans code postal", out _), Is.False);
        Assert.That(PennylaneClient.DecouperAdresse(null, out _), Is.False);
    }
}
