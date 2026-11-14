using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot B (09/11/2026), groupe « écrans » : D3-08, D3-09, D3-10, D3-12 (+ D3-N10), D6-21, D1-09 (aide).
/// D3-17 est dans CloudInscriptionTests et CloudPremiersPasTests ; la carte expirée (D3-10) dans CloudPremiersPasTests.
public class LotBEcransTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));
    static string Test(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Editor", "Tests", f));

    /// Le corps d'une méthode (indentation de 4 : la classe n'a pas d'espace de noms).
    static string Methode(string src, string signature)
    {
        int i = src.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), signature + " introuvable");
        int fin = src.IndexOf("\n    }\n", i, StringComparison.Ordinal);
        return src.Substring(i, fin - i);
    }

    // ── D3-09 : 3 licences proposées d'office, même pour une personne seule ──

    [Test]
    public void D3_09_au_choix_de_l_abonnement_les_licences_proposees_sont_celles_utilisees()
    {
        Assert.That(RegleAbonnement.LicencesProposees(1), Is.EqualTo(1));
        Assert.That(RegleAbonnement.LicencesProposees(0), Is.EqualTo(1), "jamais moins d'une");
        Assert.That(RegleAbonnement.LicencesProposees(4), Is.EqualTo(4));
        string choix = Methode(Src("Cloud/SectionAbonnement.cs"), "void Choix(AbonnementCloud.Lu a, RegleAbonnement.Etat etat)");
        StringAssert.Contains("l = RegleAbonnement.LicencesProposees(e.licences_utilisees)", choix, "le départ du choix, plus les 3 de l'essai");
        StringAssert.DoesNotContain("l = e.licences_max", choix);
    }

    // ── D6-21 : le formulaire « Ajouter un RIB » montrait le compte de CIPL ──

    [Test]
    public void D6_21_les_exemples_du_formulaire_RIB_ne_sont_pas_un_vrai_compte()
    {
        string panel = Src("Facturation/ReglagePanel.cs");
        foreach (var (nom, src) in new[] { ("ReglagePanel.cs", panel), ("ReglageData.cs", Src("Facturation/ReglageData.cs")), ("CloudPremiersPasTests.cs", Test("CloudPremiersPasTests.cs")) })
            foreach (var vrai in new[] { "0210 0983", "00021009833", "BNPAFRPPALB", "Groupe CIPL", "Mazamet" })
                StringAssert.DoesNotContain(vrai, src, nom + " : une coordonnée réelle");

        var exemples = Regex.Matches(Methode(panel, "void OpenRibForm(RibData existing"), "ModalField\\(body, \"([^\"]+)\", v\\.\\w+, \"([^\"]+)\"\\)");
        Assert.That(exemples.Count, Is.EqualTo(6), "un exemple par champ");
        string iban = null, rib = null;
        foreach (Match m in exemples)
        {
            if (m.Groups[1].Value == "IBAN") iban = m.Groups[2].Value;
            if (m.Groups[1].Value == "RIB") rib = m.Groups[2].Value;
        }
        Assert.That(iban, Does.StartWith("FR76"), "la forme d'un IBAN français");
        Assert.That(RegleRib.IbanValide(iban), Is.False, "clé fausse : l'exemple ne peut pas être enregistré tel quel");
        Assert.That(RegleRib.Refus("SCI Exemple", rib, iban, "ABCDFRPPXXX", out var champs), Does.StartWith("Cet IBAN n'est pas valide"));
        Assert.That(champs, Is.EqualTo(new[] { RegleRib.ChampIban }));
    }

    // ── D3-08 : « J'ai reçu une invitation », l'adresse corrigée après un premier essai était ignorée ──

    [Test]
    public void D3_08_l_adresse_corrigee_apres_un_premier_essai_compte()
    {
        string essai = Src("Cloud/EcransCloud.cs");
        essai = essai.Substring(essai.IndexOf("static void AfficherInvitation(", StringComparison.Ordinal));
        int oublier = essai.IndexOf("SessionCloud.Oublier();", StringComparison.Ordinal);
        int rejoindre = essai.IndexOf("await SessionCloud.Rejoindre(email.text, mdp.text)", StringComparison.Ordinal);
        int accepter = essai.IndexOf("await UtilisateursCloud.Accepter(code.text)", StringComparison.Ordinal);
        Assert.That(oublier, Is.GreaterThan(0).And.LessThan(rejoindre), "autre adresse que le compte ouvert : on l'oublie, puis Rejoindre comme au premier essai");
        Assert.That(rejoindre, Is.LessThan(accepter));
        StringAssert.Contains("string.Equals(SessionCloud.Session.user?.email, email.text.Trim(), StringComparison.OrdinalIgnoreCase)", essai,
                              "comparée à l'adresse du compte ouvert, sans la casse");
    }

    // ── D3-12 (+ D3-N10) : l'attente de paiement survivait au changement de groupe, et sans fin ──

    [Test]
    public void D3_12_l_attente_de_paiement_ne_survit_ni_a_l_entreprise_ni_aux_10_minutes()
    {
        string src = Src("Cloud/SectionAbonnement.cs");
        StringAssert.Contains("if (e.id != _entreprise) { _entreprise = e.id; _enAttente = false; _annuel = false; _repartition.Clear(); }",
                              Methode(src, "public async void Rafraichir()"), "autre entreprise : choix et attente repartent de zéro");
        string attendre = Methode(src, "async void Attendre(int n)");
        StringAssert.Contains("SessionCloud.Entreprise?.id != _entreprise) return;", attendre, "la boucle ne lit pas l'abonnement d'une autre entreprise");
        int boucle = attendre.LastIndexOf("\n        }\n", StringComparison.Ordinal);   // la fin du for
        StringAssert.Contains("_enAttente = false;", attendre.Substring(boucle), "les 120 tours passés : retour au choix");
        StringAssert.Contains("Reconstruire();", attendre.Substring(boucle));
    }

    // ── D3-10 (part client) : textes de l'état expiré ──

    [Test]
    public void D3_10_les_textes_de_l_etat_expire_ne_parlent_plus_de_l_essai()
    {
        StringAssert.Contains("(etat == RegleAbonnement.Etat.Essai ? \" (3 pendant l'essai)\" : \"\")", Src("Cloud/SectionAbonnement.cs"),
                              "« 3 pendant l'essai » seulement pendant l'essai");
        StringAssert.Contains("expire = AbonnementCloud.Etat == RegleAbonnement.Etat.Expire", Src("Cloud/PremiersPas.cs"), "la carte sait que l'essai est fini");
        foreach (var f in new[] { "Cloud/PremiersPas.cs", "Cloud/SectionAbonnement.cs", "Cloud/RegleAbonnement.cs", "Cloud/EcransCloud.cs", "Facturation/ReglagePanel.cs" })
            StringAssert.DoesNotContain("Réglage ›", Src(f), f + " : l'écran s'appelle « Réglages »");
    }

    // ── D1-09 (aide) : qui peut changer le logo ──

    [Test]
    public void D1_09_l_aide_du_logo_dit_qui_peut_le_changer()
    {
        string logo = Methode(Src("Facturation/ReglagePanel.cs"), "void BuildLogo(Transform parent)");
        StringAssert.Contains("Seul un admin peut changer le logo", logo);
        StringAssert.Contains("Drive partagé", logo, "en Drive partagé, les droits du Drive décident");
    }
}
