using NUnit.Framework;

/// Le résumé d'entreprise inséré dans le commentaire d'un locataire.
///
/// Ce bloc est désormais réécrit **automatiquement au lancement**, pour tous les
/// locataires et sans que personne ne regarde. Ce qui était une retouche manuelle,
/// visible immédiatement, devient une écriture en masse dans un champ que
/// l'utilisatrice remplit elle-même : écraser ses notes passerait inaperçu.
public class PappersResumeTests
{
    const string Debut = "-- PAPPERS --";
    const string Fin = "-- FIN PAPPERS --";

    static string Bloc(string contenu) => $"{Debut}\n{contenu}\n{Fin}";

    [Test]
    public void Un_commentaire_vide_recoit_le_bloc_seul()
    {
        Assert.That(PappersResume.Fusionner("", "RESUME"), Is.EqualTo(Bloc("RESUME")));
        Assert.That(PappersResume.Fusionner(null, "RESUME"), Is.EqualTo(Bloc("RESUME")));
    }

    [Test]
    public void Les_notes_ecrites_avant_le_bloc_sont_conservees()
    {
        string resultat = PappersResume.Fusionner("Bail signé chez Maître X.", "RESUME");
        Assert.That(resultat, Does.StartWith("Bail signé chez Maître X."));
        Assert.That(resultat, Does.Contain(Bloc("RESUME")));
    }

    /// Le cas qui compte : un bloc déjà présent est REMPLACÉ, et ce que
    /// l'utilisatrice a écrit de part et d'autre survit intact.
    [Test]
    public void Le_bloc_existant_est_remplace_sans_toucher_au_reste()
    {
        string avant = "Note du haut.\n\n" + Bloc("ANCIEN RESUME") + "\n\nNote du bas.";
        string apres = PappersResume.Fusionner(avant, "NOUVEAU RESUME");

        Assert.That(apres, Does.Contain("Note du haut."));
        Assert.That(apres, Does.Contain("Note du bas."));
        Assert.That(apres, Does.Contain("NOUVEAU RESUME"));
        Assert.That(apres, Does.Not.Contain("ANCIEN RESUME"));
        // Un seul bloc, pas deux empilés à chaque lancement.
        Assert.That(apres.Split(new[] { Debut }, System.StringSplitOptions.None).Length - 1,
                    Is.EqualTo(1), "le bloc ne doit pas se dupliquer");
    }

    /// Relancer l'application sans changement chez l'annuaire ne doit RIEN modifier :
    /// c'est ce qui évite de réécrire les fichiers de sauvegarde à chaque démarrage.
    [Test]
    public void Refusionner_le_meme_contenu_ne_change_rien()
    {
        string une = PappersResume.Fusionner("Note.", "RESUME");
        string deux = PappersResume.Fusionner(une, "RESUME");
        Assert.That(deux, Is.EqualTo(une));
    }

    // ── Contenu du résumé ───────────────────────────────────────────────────────

    static AnnuaireEntreprise Entreprise() => new AnnuaireEntreprise
    {
        nom_complet = "BOULANGERIE DUPONT",
        siren = "123456789",
        date_creation = "2015-03-08",
        libelle_nature_juridique = "SARL",
        activite_principale = "10.71C",
        libelle_activite_principale = "Boulangerie et boulangerie-pâtisserie",
        etat_administratif = "A"
    };

    [Test]
    public void Le_resume_porte_les_informations_lisibles()
    {
        string r = PappersResume.Construire(Entreprise());
        Assert.That(r, Does.Contain("BOULANGERIE DUPONT"));
        Assert.That(r, Does.Contain("123 456 789"), "le SIREN est mis en forme");
        Assert.That(r, Does.Contain("08/03/2015"), "la date ISO devient une date française");
        Assert.That(r, Does.Contain("Actif"));
        Assert.That(r, Does.Contain("10.71C"));
    }

    [Test]
    public void Une_entreprise_cessee_est_annoncee_comme_telle()
    {
        var e = Entreprise();
        e.etat_administratif = "C";
        Assert.That(PappersResume.Construire(e), Does.Contain("Cessé"));
    }

    [Test]
    public void Une_reponse_absente_ne_produit_pas_de_texte()
    {
        // Sans cette garde, un bloc vide viendrait écraser un résumé valide.
        Assert.That(PappersResume.Construire(null), Is.Empty);
    }

    // ── SIRET ───────────────────────────────────────────────────────────────────

    [Test]
    public void Seul_un_SIRET_exploitable_declenche_une_interrogation()
    {
        // Neuf chiffres au minimum : interroger l'annuaire avec moins n'a aucun sens,
        // et au lancement ces appels inutiles se compteraient par dizaines.
        Assert.That(PappersResume.SiretExploitable("123456789"), Is.True);
        Assert.That(PappersResume.SiretExploitable("123456789 00012"), Is.True);
        Assert.That(PappersResume.SiretExploitable("12345"), Is.False);
        Assert.That(PappersResume.SiretExploitable("   "), Is.False);
        Assert.That(PappersResume.SiretExploitable(null), Is.False);
    }
}
