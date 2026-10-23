using System;
using System.Collections.Generic;
using NUnit.Framework;

/// Étape 11 : l'export de toutes les données (décisions du 23/10).
public class CloudExportTests
{
    [Test]
    public void L_admin_exporte_toujours_les_autres_quand_l_abonnement_a_expire()
    {
        Assert.That(RegleExport.PeutExporter(admin: true, RegleAbonnement.Etat.Actif), Is.True);
        Assert.That(RegleExport.PeutExporter(admin: false, RegleAbonnement.Etat.Actif), Is.False);
        Assert.That(RegleExport.PeutExporter(admin: false, RegleAbonnement.Etat.Essai), Is.False);
        Assert.That(RegleExport.PeutExporter(admin: false, RegleAbonnement.Etat.Expire), Is.True);
    }

    [Test]
    public void Un_export_a_son_dossier_date_et_n_en_ecrase_jamais_un_autre()
    {
        Assert.That(RegleExport.NomDossier("Démo CIPL", new DateTime(2026, 10, 23)), Is.EqualTo("Démo CIPL — export du 23-10-2026"));
        Assert.That(RegleExport.NomDossier("SCI A/B", new DateTime(2026, 10, 23)), Is.EqualTo("SCI A-B — export du 23-10-2026"),
                    "un caractère interdit devient un tiret");
        var pris = new HashSet<string> { "X", "X (2)" };
        Assert.That(RegleExport.Libre("X", pris.Contains), Is.EqualTo("X (3)"));
        Assert.That(RegleExport.Libre("Y", pris.Contains), Is.EqualTo("Y"));
    }

    [Test]
    public void Les_documents_de_ce_PC_sont_copies_les_autres_pris_dans_le_cloud()
    {
        var docs = RegleExport.Documents(new[] { "Batiment/rivoli/Photos/a.jpg", "Logo/logo.png" },
                                         new HashSet<string> { "Logo/logo.png", "Batiment/rivoli/Bail/bail.pdf" });
        Assert.That(docs, Is.EqualTo(new List<(string, bool)>
        {
            ("Batiment/rivoli/Bail/bail.pdf", false),   // seulement sur ce PC (pas encore envoyé) : copié
            ("Batiment/rivoli/Photos/a.jpg", true),     // seulement sur le cloud : téléchargé
            ("Logo/logo.png", false),                   // aux deux : la copie de ce PC
        }));
    }

    [Test]
    public void Le_bilan_et_le_dernier_export_se_lisent()
    {
        string bilan = RegleExport.Bilan(4, 17, 48);
        Assert.That(bilan, Is.EqualTo("4 bâtiments, 17 locataires, 48 documents"));
        Assert.That(RegleExport.Bilan(1, 1, 0), Is.EqualTo("1 bâtiment, 1 locataire, 0 document"));
        Assert.That(RegleExport.DernierExport(new DateTime(2026, 10, 23, 16, 42, 0), bilan),
                    Is.EqualTo("Dernier export : 23/10/2026 à 16 h 42 — 4 bâtiments, 17 locataires, 48 documents."));
    }
}
