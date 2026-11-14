using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot D, groupe FICHES (11/11/2026) : fiches bâtiment et locataire (cession datée, Pappers, nombres, SIRET et IBAN,
/// ajout hors ligne, création abandonnée, bail du 29 février, « Annuler » d'une suppression). Règle pure d'abord, texte du code à défaut.
public class LotDFichesTests
{
    static string Src(string f) => File.ReadAllText(Path.Combine(Application.dataPath, "Script", f));

    /// Le corps d'une méthode, de `debut` à sa fermeture (motif valable en LF comme en CRLF).
    static string Corps(string src, string debut)
    {
        int i = src.IndexOf(debut, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), debut);
        return src.Substring(i, src.IndexOf("\n    }", i, StringComparison.Ordinal) - i);
    }

    /// D7a-04 : une cession datée n'est tentée que pour un locataire que personne d'autre ne tient ; le dossier n'est renommé
    /// qu'après l'accord du serveur.
    [Test]
    public void D7a_04_la_cession_datee_saute_les_locataires_tenus_et_renomme_apres_l_accord()
    {
        var aujourdhui = new DateTime(2026, 12, 1);
        Locataire Cede(string id, string nom, string vers, string date)
            => new Locataire { id = id, Name = nom, cessionPrevue = new CessionPrevue { dateISO = date, nom = vers } };
        var tenu = Cede("LOTD-L1", "SARL Ancien", "SARL Nouveau", "2026-12-01");
        var libre = Cede("LOTD-L2", "SARL B", "SARL C", "2026-11-30");
        var aVenir = Cede("LOTD-L3", "SARL D", "SARL E", "2026-12-02");
        var bat = new Batiment { id = "LOTD-B1", Name = "Rivoli", locataireDuBatiment = new List<Locataire> { tenu, libre, aVenir, null } };

        var dues = BatimentManager.CessionsAAppliquer(new[] { bat }, id => id != "LOTD-L1", aujourdhui);
        Assert.That(dues.Count, Is.EqualTo(1), "le locataire tenu par un collègue est sauté, la cession à venir attend");
        Assert.That(dues[0].loc, Is.SameAs(libre));
        Assert.That(dues[0].bat, Is.SameAs(bat));
        Assert.That(BatimentManager.CessionsAAppliquer(new[] { bat }, null, aujourdhui).Count, Is.EqualTo(2), "libre : tenté");
        Assert.That(tenu.cessionPrevue, Is.Not.Null, "la règle n'applique rien");

        string corps = Corps(Src("Batiment/BatimentManager.cs"), "public async void AppliquerCessionsDues()");
        int envoi = corps.IndexOf("await EcritureCloud.Attendre();", StringComparison.Ordinal);
        int accord = corps.IndexOf("EcritureCloud.AJour(copie)", StringComparison.Ordinal);
        int renomme = corps.IndexOf("DossiersDonnees.RenommerLocataire(bat.Name, ancien, nouveau", StringComparison.Ordinal);
        Assert.That(envoi, Is.GreaterThan(0));
        Assert.That(accord, Is.GreaterThan(envoi), "l'accord est lu après l'envoi");
        Assert.That(renomme, Is.GreaterThan(accord), "le dossier n'est renommé qu'après l'accord");
        StringAssert.Contains("EcritureCloud.Enregistrer(copie, silencieux: true)", corps, "écriture de fond, sur une copie");
        StringAssert.Contains("EcritureCloud.LocatairesLibres()", corps);
    }

    /// Contrat C2 : le dossier mis en corbeille est déclaré comme un retrait voulu, une fois le déplacement réussi.
    [Test]
    public void C2_la_corbeille_declare_le_retrait_voulu_apres_le_deplacement()
    {
        string corps = Corps(Src("Batiment/BatimentManager.cs"), "static string DeplacerEnCorbeille(string source)");
        int deplace = corps.IndexOf("Directory.Move(source, dest);", StringComparison.Ordinal);
        Assert.That(deplace, Is.GreaterThan(0));
        Assert.That(corps.IndexOf("DocumentsCloud.RetraitVoulu(source);", StringComparison.Ordinal), Is.GreaterThan(deplace));
    }

    /// D7a-07 : seuls « acces » et « abonnement » arrêtent la tournée ; les refus de toute l'entreprise sont dits ; la partie BODACC
    /// s'applique sans fiche Pappers.
    [Test]
    public void D7a_07_un_refus_pappers_n_arrete_plus_la_tournee_et_le_bodacc_s_applique_sans_fiche()
    {
        Assert.That(PappersSync.ArreteLaTournee("acces"), Is.True);
        Assert.That(PappersSync.ArreteLaTournee("abonnement expiré"), Is.True);
        foreach (var r in new[] { "quota", "jetons", "cle", "config", "serveur réponse inattendue", "", null })
            Assert.That(PappersSync.ArreteLaTournee(r), Is.False, r ?? "null");
        StringAssert.Contains("50 fiches société lues par jour", PappersSync.MessageRefus("quota"));
        StringAssert.Contains("crédits épuisés", PappersSync.MessageRefus("jetons"));
        Assert.That(PappersSync.MessageRefus("cle"), Is.Not.Null.And.EqualTo(PappersSync.MessageRefus("config")));
        Assert.That(PappersSync.MessageRefus("serveur x"), Is.Null, "refus d'un seul locataire : journal seulement");

        var hier = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
        var procs = new List<SocieteLocataire.Procedure>
        {
            new SocieteLocataire.Procedure { id = "LOTD-B1", siren = "552100554", parution = hier, jugeLe = hier,
                                             famille = "Procédures collectives", nature = "Jugement d'ouverture de redressement judiciaire" }
        };
        var objectifs = SocieteLocataire.Signaler(new List<string>(), null, procs, DateTime.Today);
        Assert.That(objectifs.Count, Is.EqualTo(1), "sans fiche Pappers, la procédure est signalée");
        StringAssert.StartsWith("Déclarer la créance", objectifs[0].objectif.text);
        StringAssert.Contains("SocieteLocataire.Signaler(loc.signalementsSociete, fiche, procs", Src("Pappers/PappersSync.cs"),
                              "fiche null quand Pappers refuse");
    }

    /// D7a-24 : Pappers et le BODACC relus seulement quand le SIREN change ; l'écriture qui suit est silencieuse.
    [Test]
    public void D7a_24_pappers_relu_seulement_si_le_siren_change_et_en_silence()
    {
        Assert.That(PappersSync.SiretChange("552 032 534 00012", "55203253400012"), Is.False, "mêmes chiffres");
        Assert.That(PappersSync.SiretChange("552 032 534 00012", "552 032 534 00020"), Is.False, "même SIREN, autre établissement");
        Assert.That(PappersSync.SiretChange("552 032 534 00012", "552 100 554 00013"), Is.True);
        Assert.That(PappersSync.SiretChange("", "552 100 554 00013"), Is.True, "SIRET saisi pour la première fois");
        StringAssert.Contains("SaveBatiment(bp.getBatiment(), silencieux: true)",
                              Corps(Src("Pappers/PappersSync.cs"), "public static IEnumerator Un("));
        StringAssert.Contains("PappersSync.SiretChange(siretAvant, locataire.siretNumber)", Src("Locataire/LocatairePrefab.cs"));
    }

    /// D7a-14 : un nombre illisible est refusé (avant toute écriture) ; le vide et les écritures françaises passent.
    [Test]
    public void D7a_14_un_nombre_illisible_est_refuse_avant_toute_ecriture()
    {
        StringAssert.Contains("« 2B » n'est pas un nombre (numéro du lot)", PrefabBatLoc.RefusNombre("2B", "numéro du lot", entier: true));
        Assert.That(PrefabBatLoc.RefusNombre("120 m2", "surface du lot"), Is.Not.Null);
        Assert.That(PrefabBatLoc.RefusNombre("1 250,5", "surface du bâtiment"), Is.Null);
        Assert.That(PrefabBatLoc.RefusNombre("5 %", "taux de rentabilité"), Is.Null);
        Assert.That(PrefabBatLoc.RefusNombre("", "surface du terrain"), Is.Null, "vide = 0, accepté");
        Assert.That(PrefabBatLoc.RefusNombre("3", "numéro du lot", entier: true), Is.Null);

        string loc = Corps(Src("Locataire/LocatairePrefab.cs"), "public void SaveLocataire()");
        int controle = loc.IndexOf("RefusNombre(tauxDeRentabilité.GetValue()", StringComparison.Ordinal);
        Assert.That(controle, Is.GreaterThan(0));
        Assert.That(loc.IndexOf("DossiersDonnees.RenommerLocataire(nomBat", StringComparison.Ordinal), Is.GreaterThan(controle),
                    "avant le renommage du dossier");
        string bat = Corps(Src("Batiment/BatimentPrefab.cs"), "public override void SaveBatiment()");
        int controleBat = bat.IndexOf("RefusNombre(tailleTerrainText.GetValue()", StringComparison.Ordinal);
        Assert.That(controleBat, Is.GreaterThan(0));
        Assert.That(bat.IndexOf("DossiersDonnees.RenommerBatiment(", StringComparison.Ordinal), Is.GreaterThan(controleBat));
    }

    /// D7a-15 (décision du 11/11) : SIRET et IBAN refusés seulement quand ils viennent d'être modifiés, non vides et faux.
    [Test]
    public void D7a_15_siret_et_iban_controles_seulement_quand_ils_changent()
    {
        const string ok = "552 100 554 00013", faux = "552 100 554 00014";
        Assert.That(RegleAbonnement.VerifierSiret(ok), Is.Null, "jeu de test : SIRET valide");
        Assert.That(LocatairePrefab.RefusSiretIban("", faux, "", ""),
                    Is.EqualTo("Ce SIRET n'est pas valide (clé de contrôle). Corrigez-le ou videz le champ."));
        Assert.That(LocatairePrefab.RefusSiretIban(ok, faux, "", ""), Is.Not.Null, "modifié et faux");
        Assert.That(LocatairePrefab.RefusSiretIban(faux, faux, "", ""), Is.Null, "fiche ancienne, SIRET non touché : s'enregistre");
        Assert.That(LocatairePrefab.RefusSiretIban(faux, " " + faux + " ", "", ""), Is.Null, "mêmes caractères, espaces autour");
        Assert.That(LocatairePrefab.RefusSiretIban(faux, "", "", ""), Is.Null, "vidé : accepté");
        Assert.That(LocatairePrefab.RefusSiretIban("", ok, "", ""), Is.Null);

        const string iban = "FR76 3000 6000 0112 3456 7890 189", ibanFaux = "FR76 3000 6000 0112 3456 7890 198";
        Assert.That(LocatairePrefab.RefusSiretIban("", "", "", ibanFaux),
                    Is.EqualTo("Cet IBAN n'est pas valide (clé de contrôle). Corrigez-le ou videz le champ."));
        Assert.That(LocatairePrefab.RefusSiretIban("", "", ibanFaux, ibanFaux), Is.Null, "IBAN ancien non touché");
        Assert.That(LocatairePrefab.RefusSiretIban("", "", "", iban), Is.Null);
        Assert.That(LocatairePrefab.RefusSiretIban("", "", "x", null), Is.Null, "carte de facturation non construite");
    }

    /// D7a-18 : « + Ajouter » refusé tout de suite hors ligne et en lecture seule (locataire et bâtiment).
    [Test]
    public void D7a_18_ajouter_est_refuse_hors_ligne_et_en_lecture_seule()
    {
        Assert.That(BatimentManager.RefusAjout(true, true, false), Is.EqualTo(HorsLigne.Refus));
        Assert.That(BatimentManager.RefusAjout(true, false, true), Is.EqualTo(AbonnementCloud.Refus));
        Assert.That(BatimentManager.RefusAjout(true, false, false), Is.Null);
        Assert.That(BatimentManager.RefusAjout(false, true, true), Is.Null, "hors cloud : rien à refuser");
        StringAssert.Contains("if (BatimentManager.RefuserAjout()) return true;",
                              Corps(Src("Batiment/BatimentPrefab.cs"), "public bool RefuserAjoutSansLotLibre("), "les deux entrées d'un locataire");
        StringAssert.Contains("if (RefuserAjout()) return null;", Corps(Src("Batiment/BatimentManager.cs"), "public BatimentPrefab AddBatiment("),
                              "bâtiment : onglet « + » et accueil");
        StringAssert.Contains("if (prefabBatLoc == null) return;", Src("Menu/MenuManager.cs"), "un ajout refusé n'ouvre pas d'onglet");
    }

    /// D7a-16 (décision du 11/11) : une fiche jamais enregistrée, au nom par défaut, est abandonnée au retour au résumé.
    [Test]
    public void D7a_16_une_creation_abandonnee_au_nom_par_defaut_est_retiree()
    {
        var neuf = new Locataire { id = "LOTD-N1" };
        var inscrit = new Locataire { id = "LOTD-N2" };
        var bat = new Batiment { id = "LOTD-B2", locataireDuBatiment = new List<Locataire> { inscrit } };
        Assert.That(BatimentPrefab.CreationAbandonnee(neuf, bat, Data.NomParDefaut), Is.True);
        Assert.That(BatimentPrefab.CreationAbandonnee(neuf, bat, "  "), Is.True, "nom vidé");
        Assert.That(BatimentPrefab.CreationAbandonnee(neuf, bat, "SARL Nouveau"), Is.False, "un nom tapé : la fiche reste en saisie");
        Assert.That(BatimentPrefab.CreationAbandonnee(inscrit, bat, Data.NomParDefaut), Is.False, "déjà enregistrée : jamais retirée ainsi");
        StringAssert.Contains("RetirerCreationsAbandonnees();", Corps(Src("Batiment/BatimentPrefab.cs"), "public void ShowSummary()"));
    }

    /// D7a-13 : un bail commencé un 29 février finit le 28 février (et ses échéances triennales aussi).
    [Test]
    public void D7a_13_un_bail_du_29_fevrier_finit_le_28_fevrier()
    {
        Assert.That(Locataire.FinDeBail(new DateTime(2024, 2, 29), 9), Is.EqualTo(new DateTime(2033, 2, 28)));
        Assert.That(Locataire.FinDeBail(new DateTime(2024, 2, 29), 3), Is.EqualTo(new DateTime(2027, 2, 28)), "échéance triennale");
        Assert.That(Locataire.FinDeBail(new DateTime(2024, 2, 29), 4), Is.EqualTo(new DateTime(2028, 2, 28)), "année bissextile : la veille du 29");
        Assert.That(Locataire.FinDeBail(new DateTime(2020, 6, 1), 9), Is.EqualTo(new DateTime(2029, 5, 31)), "inchangé");
        Assert.That(Locataire.FinDeBail(new DateTime(2023, 3, 1), 1), Is.EqualTo(new DateTime(2024, 2, 29)), "inchangé");
    }

    /// D7a-11 et D8-09 : le bouton Pappers lit le SIREN comme partout ; « Annuler » retrouve la fiche du bâtiment au clic.
    [Test]
    public void D7a_11_D8_09_bouton_pappers_et_annuler_la_suppression()
    {
        string src = Src("Locataire/LocatairePrefab.cs");
        StringAssert.Contains("SocieteLocataire.Siren(siret.GetValue())", src);
        StringAssert.DoesNotContain("s.Substring(0, 9)", src);
        StringAssert.Contains("p.getID() == idBat", src, "D8-09 : retrouvée par son identifiant");
        StringAssert.DoesNotContain("() => bp.RestoreLocataire(backup)", src, "D8-09 : plus l'objet du moment de la suppression");
        StringAssert.DoesNotContain("les 9 du SIREN", src, "D7a-11 : un SIREN seul est refusé à l'enregistrement (D7a-15)");
        string restaure = Corps(Src("Batiment/BatimentPrefab.cs"), "public void RestoreLocataire(string json)");
        int garde = restaure.IndexOf("locataireDuBatiment.Exists(l => l != null && l.id == data.id)", StringComparison.Ordinal);
        Assert.That(garde, Is.GreaterThan(0), "D8-09 : pas de doublon si le locataire est déjà revenu");
        Assert.That(restaure.IndexOf("listLocataire.Add(data);", StringComparison.Ordinal), Is.GreaterThan(garde));
    }

    /// D7a-04 : un dossier d'arrivée vide ne bloque pas la cession (même règle que RenommerLocataire) ; un fichier, si.
    [Test]
    public void D7a_04_un_dossier_d_arrivee_vide_ne_bloque_pas_la_cession()
    {
        string d = Path.Combine(Path.GetTempPath(), "LotDFiches_" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.That(BatimentManager.DossierAvecFichiers(d), Is.False, "absent");
            Directory.CreateDirectory(Path.Combine(d, "Factures", "2026"));
            Assert.That(BatimentManager.DossierAvecFichiers(d), Is.False, "arborescence sans fichier");
            File.WriteAllText(Path.Combine(d, "Factures", "2026", "f.pdf"), "x");
            Assert.That(BatimentManager.DossierAvecFichiers(d), Is.True, "un fichier, même en profondeur");
        }
        finally { if (Directory.Exists(d)) Directory.Delete(d, true); }
    }
}
