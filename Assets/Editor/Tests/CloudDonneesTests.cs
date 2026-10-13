using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// Étape 2 : ce qui se vérifie sans réseau. Découpage d'un bâtiment en « bâtiment +
/// locataires » et recomposition à l'identique, liste des entreprises, statut d'import,
/// chiffrement Windows de la connexion gardée.
public class CloudDonneesTests
{
    static Batiment Rivoli()
    {
        var b = new Batiment { Name = "rivoli" };
        b.locataireDuBatiment = new List<Locataire>
        {
            new Locataire { Name = "Volteo" },
            new Locataire { Name = "Atelier Nord" },
        };
        return b;
    }

    [Test]
    public void Un_batiment_decoupe_puis_recompose_est_identique()
    {
        var b = Rivoli();
        string avant = JsonUtility.ToJson(b);

        DonneesCloud.Decouper(b, out string jsonBat, out var jsonLocs);
        Assert.That(jsonLocs.Count, Is.EqualTo(2));
        StringAssert.DoesNotContain("Volteo", jsonBat, "le bâtiment part SANS ses locataires");
        Assert.That(b.locataireDuBatiment.Count, Is.EqualTo(2), "l'original n'est pas modifié");

        // Réponses du serveur, locataires volontairement dans le désordre : l'ordre des onglets vient d'« ordre ».
        string lignesBat = "[{\"id\":\"" + b.id + "\",\"data\":" + jsonBat + "}]";
        string lignesLoc = "[{\"batiment_id\":\"" + b.id + "\",\"ordre\":1,\"data\":" + jsonLocs[1] + "}," +
                           "{\"batiment_id\":\"" + b.id + "\",\"ordre\":0,\"data\":" + jsonLocs[0] + "}]";
        var recompose = DonneesCloud.Recomposer(lignesBat, lignesLoc);

        Assert.That(recompose.Count, Is.EqualTo(1));
        Assert.That(JsonUtility.ToJson(recompose[0]), Is.EqualTo(avant));
    }

    [Test]
    public void Un_batiment_sans_locataire_revient_avec_une_liste_vide()
    {
        var b = new Batiment { Name = "Le Hangar" };
        DonneesCloud.Decouper(b, out string jsonBat, out var jsonLocs);
        var r = DonneesCloud.Recomposer("[{\"id\":\"" + b.id + "\",\"data\":" + jsonBat + "}]", "[]");
        Assert.That(jsonLocs, Is.Empty);
        Assert.That(r[0].locataireDuBatiment, Is.Not.Null.And.Empty);
    }

    [Test]
    public void Les_locataires_partent_avec_leur_ordre_et_leur_JSON_tel_quel()
    {
        string corps = DonneesCloud.CorpsLocataires("ent-1", "bat-1", new[] { "loc-a", "loc-b" }, new[] { "{\"Name\":\"A\"}", "{\"Name\":\"B\"}" });
        Assert.That(corps, Is.EqualTo("[{\"entreprise_id\":\"ent-1\",\"id\":\"loc-a\",\"batiment_id\":\"bat-1\",\"ordre\":0,\"data\":{\"Name\":\"A\"}}," +
                                      "{\"entreprise_id\":\"ent-1\",\"id\":\"loc-b\",\"batiment_id\":\"bat-1\",\"ordre\":1,\"data\":{\"Name\":\"B\"}}]"));
    }

    [Test]
    public void La_liste_des_entreprises_est_lue()
    {
        var l = DonneesCloud.LireEntreprises("[{\"id\":\"e1\",\"nom\":\"DemoCIPL\",\"type\":\"societe\",\"role\":\"admin\",\"nb_batiments\":4}," +
                                             "{\"id\":\"e2\",\"nom\":\"SCI Hangar\",\"type\":\"societe\",\"role\":\"utilisateur\",\"nb_batiments\":1}]");
        Assert.That(l.Count, Is.EqualTo(2));
        Assert.That(l[0].EstAdmin, Is.True);
        Assert.That(l[1].EstAdmin, Is.False);
        Assert.That(l[0].nb_batiments, Is.EqualTo(4));
        Assert.That(DonneesCloud.LireEntreprises("[]"), Is.Empty);
    }

    [Test]
    public void Le_repere_dit_s_il_faut_importer_reprendre_ou_refuser()
    {
        const string projet = "https://abcd.supabase.co";
        var miennes = new List<EntrepriseCloud>
        {
            new EntrepriseCloud { id = "e1", nom = "DemoCIPL", role = "admin" },
            new EntrepriseCloud { id = "e2", nom = "SCI Hangar", role = "utilisateur" },
        };
        ImportLocal.Repere R(string id, bool complet, string p = projet) => new ImportLocal.Repere { id = id, projet = p, complet = complet };

        Assert.That(ImportLocal.Statut(null, projet, miennes), Is.EqualTo(ImportLocal.Etat.AImporter), "jamais importée");
        Assert.That(ImportLocal.Statut(R("e9", true, "https://autre.supabase.co"), projet, miennes), Is.EqualTo(ImportLocal.Etat.AImporter), "repère d'un autre projet");
        Assert.That(ImportLocal.Statut(R("e1", false), projet, miennes), Is.EqualTo(ImportLocal.Etat.AReprendre), "import inachevé, je suis admin");
        Assert.That(ImportLocal.Statut(R("e1", true), projet, miennes), Is.EqualTo(ImportLocal.Etat.DejaImportee));
        Assert.That(ImportLocal.Statut(R("e2", false), projet, miennes), Is.EqualTo(ImportLocal.Etat.DejaSurLeCloud), "inachevé mais je ne suis pas admin");
        Assert.That(ImportLocal.Statut(R("e9", true), projet, miennes), Is.EqualTo(ImportLocal.Etat.DejaSurLeCloud), "importée par quelqu'un d'autre");
    }

    [Test]
    public void Le_jeton_garde_sur_ce_PC_est_chiffre_et_infalsifiable()
    {
        byte[] clair = Encoding.UTF8.GetBytes("jeton-de-renouvellement");
        byte[] chiffre = Dpapi.Proteger(clair);
        Assert.That(chiffre, Is.Not.Null);
        StringAssert.DoesNotContain("jeton-de-renouvellement", Encoding.UTF8.GetString(chiffre));
        Assert.That(Dpapi.Deproteger(chiffre), Is.EqualTo(clair));

        chiffre[chiffre.Length / 2] ^= 0xFF;   // un octet modifié
        Assert.That(Dpapi.Deproteger(chiffre), Is.Null);
    }
}
