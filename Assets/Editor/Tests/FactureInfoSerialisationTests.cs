using NUnit.Framework;
using UnityEngine;

/// Le réglage des factures doit survivre à l'écriture sur disque.
///
/// Défaut trouvé le 21/09 : `[Serializable]` avait glissé de `FactureInfo` vers
/// `FactureNumerotation` — une classe statique, où l'attribut ne sert à rien — parce
/// qu'elle avait été insérée entre le commentaire de `FactureInfo` et son attribut.
/// C'est syntaxiquement valide, donc le compilateur n'a rien dit.
///
/// Conséquence : `JsonUtility` ignorait les quatre champs `factureX` du locataire.
/// Le réglage tenait en mémoire, puis disparaissait dès que les données repassaient
/// par le JSON — ce que les prefabs font en permanence. Symptôme vécu : une case
/// « Envoyer par email » cochée, et pourtant aucun email envoyé.
///
/// Rien ne signalait le problème : ni erreur, ni avertissement, ni trace. D'où ces
/// tests, qui échouent si l'attribut repart.
public class FactureInfoSerialisationTests
{
    static Locataire AvecReglages()
    {
        var loc = new Locataire { Name = "SARL DEMO" };
        loc.factureLoyer = new FactureInfo
        {
            ribId = "rib-bnp",
            enteteId = "entete-trimestriel",
            numeroFormat = "AJMN",
            emailDest = "demo@example.invalid",
            tvaDebit = false,
            texteTvaDebit = "TVA acquittée sur les débits",
            emailObjet = "CIPL — facture {numero}",
            dateISO = "2026-04-03"
        };
        return loc;
    }

    [Test]
    public void Le_reglage_de_facture_survit_a_un_aller_retour_JSON()
    {
        var relu = JsonUtility.FromJson<Locataire>(JsonUtility.ToJson(AvecReglages()));

        Assert.That(relu.factureLoyer, Is.Not.Null,
            "FactureInfo n'est pas sérialisée : vérifie que [Serializable] est bien collé à la classe.");

        Assert.That(relu.factureLoyer.emailDest, Is.EqualTo("demo@example.invalid"));
        Assert.That(relu.factureLoyer.ribId, Is.EqualTo("rib-bnp"));
        Assert.That(relu.factureLoyer.enteteId, Is.EqualTo("entete-trimestriel"));
        Assert.That(relu.factureLoyer.numeroFormat, Is.EqualTo("AJMN"));
        Assert.That(relu.factureLoyer.tvaDebit, Is.False, "une case décochée doit rester décochée");
        Assert.That(relu.factureLoyer.texteTvaDebit, Is.EqualTo("TVA acquittée sur les débits"));
        Assert.That(relu.factureLoyer.emailObjet, Is.EqualTo("CIPL — facture {numero}"));
        Assert.That(relu.factureLoyer.dateISO, Is.EqualTo("2026-04-03"));
    }

    [Test]
    public void Les_quatre_types_sont_ecrits_dans_le_JSON()
    {
        var loc = AvecReglages();
        loc.factureRegul = new FactureInfo { ribId = "rib-regul" };
        loc.factureRefac = new FactureInfo { ribId = "rib-refac" };
        loc.factureDepot = new FactureInfo { ribId = "rib-depot" };

        string json = JsonUtility.ToJson(loc);

        // Le symptôme d'origine était l'ABSENCE pure et simple de ces clés dans le
        // fichier de sauvegarde : on le vérifie sur le texte, pas seulement sur l'objet.
        foreach (string cle in new[] { "factureLoyer", "factureRegul", "factureRefac", "factureDepot" })
            Assert.That(json, Does.Contain(cle), $"« {cle} » absent du JSON : le réglage ne serait pas enregistré");

        Assert.That(json, Does.Contain("rib-depot"), "les valeurs doivent y être, pas seulement les clés");
    }
}
