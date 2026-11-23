using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Étape 4a : ce qui part au serveur quand on enregistre. Seules les fiches changées, avec
/// la version lue (contrôle de version), le bâtiment avant ses locataires.
public class CloudEcritureTests
{
    static Batiment Rivoli(out Locataire volteo, out Locataire nord)
    {
        volteo = new Locataire { Name = "Volteo" };
        nord = new Locataire { Name = "Atelier Nord" };
        return new Batiment { Name = "rivoli", locataireDuBatiment = new List<Locataire> { volteo, nord } };
    }

    static Dictionary<string, EcritureCloud.Etat> Lu(Batiment b, int vBat = 3, int vLoc = 7)
    {
        var cache = new Dictionary<string, EcritureCloud.Etat>();
        EcritureCloud.Memoriser(cache, b, vBat, b.locataireDuBatiment.ToDictionary(l => l.id, l => vLoc));
        return cache;
    }

    [Test]
    public void Rien_de_change_rien_ne_part()
    {
        var b = Rivoli(out _, out _);
        Assert.That(EcritureCloud.Differences(b, Lu(b)), Is.Empty);
    }

    [Test]
    public void Un_locataire_modifie_part_seul_avec_sa_version()
    {
        var b = Rivoli(out var volteo, out _);
        var cache = Lu(b);
        volteo.Name = "Volteo SAS";

        var ops = EcritureCloud.Differences(b, cache);
        Assert.That(ops.Count, Is.EqualTo(1));
        Assert.That(ops[0].Type, Is.EqualTo(EcritureCloud.TypeOp.Locataire));
        Assert.That(ops[0].Id, Is.EqualTo(volteo.id));
        Assert.That(ops[0].Version, Is.EqualTo(7), "la version lue : le serveur refusera si elle a changé");
    }

    [Test]
    public void Un_nouveau_batiment_part_avant_ses_locataires_en_version_0()
    {
        var b = Rivoli(out _, out _);
        var ops = EcritureCloud.Differences(b, new Dictionary<string, EcritureCloud.Etat>());
        Assert.That(ops.Select(o => o.Type), Is.EqualTo(new[] { EcritureCloud.TypeOp.Batiment, EcritureCloud.TypeOp.Locataire, EcritureCloud.TypeOp.Locataire }));
        Assert.That(ops.All(o => o.Version == 0), Is.True);
    }

    /// Revue 17 (D2-05, lot D) : l'ordre n'est plus renuméroté. Avant, Atelier Nord repartait avec l'ordre 0 : tenu par
    /// un collègue, il faisait refuser la suppression de Volteo.
    [Test]
    public void Un_locataire_retire_est_supprime_sans_renvoyer_le_suivant()
    {
        var b = Rivoli(out var volteo, out var nord);
        var cache = Lu(b);
        b.locataireDuBatiment.Remove(volteo);   // Atelier Nord passe de l'onglet 2 à l'onglet 1, son ordre au serveur reste 1

        var ops = EcritureCloud.Differences(b, cache);
        Assert.That(ops.Count, Is.EqualTo(1), "une seule fiche part : la suppression");
        Assert.That(ops[0].Type, Is.EqualTo(EcritureCloud.TypeOp.SupprimerLocataire));
        Assert.That(ops[0].Id, Is.EqualTo(volteo.id));
        Assert.That(cache[EcritureCloud.Cle(false, nord.id)].Ordre, Is.EqualTo(1), "l'état connu n'est pas touché par la comparaison");
    }

    [Test]
    public void La_lecture_du_serveur_retient_les_versions()
    {
        var b = Rivoli(out var volteo, out _);
        DonneesCloud.Decouper(b, out string jb, out var jl);
        string bats = "[{\"id\":\"" + b.id + "\",\"version\":4,\"data\":" + jb + "}]";
        string locs = "[{\"id\":\"" + volteo.id + "\",\"batiment_id\":\"" + b.id + "\",\"ordre\":0,\"version\":9,\"data\":" + jl[0] + "}]";
        var cache = new Dictionary<string, EcritureCloud.Etat>();

        var lus = EcritureCloud.Lire(bats, locs, cache);
        Assert.That(lus.Count, Is.EqualTo(1));
        Assert.That(cache[EcritureCloud.Cle(true, b.id)].Version, Is.EqualTo(4));
        Assert.That(cache[EcritureCloud.Cle(false, volteo.id)].Version, Is.EqualTo(9));
        Assert.That(EcritureCloud.Differences(lus[0], cache), Is.Empty, "relu tel quel : rien à renvoyer");
    }

    [Test]
    public void Une_mise_a_niveau_du_chargement_ne_repart_pas_et_les_versions_restent()
    {
        var b = Rivoli(out var volteo, out _);
        var cache = Lu(b);
        volteo.loyerAnnuel = 24000;   // ex. Loyers.Actualiser : palier du jour, en mémoire

        EcritureCloud.Recaler(cache, new[] { b });
        Assert.That(EcritureCloud.Differences(b, cache), Is.Empty);
        Assert.That(cache[EcritureCloud.Cle(true, b.id)].Version, Is.EqualTo(3));
        Assert.That(cache[EcritureCloud.Cle(false, volteo.id)].Version, Is.EqualTo(7));
    }

    [Test]
    public void Une_relecture_remplace_l_etat_du_batiment_sans_toucher_aux_autres()
    {
        var b = Rivoli(out var volteo, out var nord);
        var cache = Lu(b);
        cache["l:autre"] = new EcritureCloud.Etat { BatimentId = "autre-batiment", Version = 2 };

        b.locataireDuBatiment.Remove(nord);   // Marie a supprimé Atelier Nord, et modifié Volteo
        var relu = new Dictionary<string, EcritureCloud.Etat>();
        EcritureCloud.Memoriser(relu, b, 4, new Dictionary<string, int> { { volteo.id, 8 } });

        EcritureCloud.Adopter(cache, b.id, relu, b);
        Assert.That(cache.ContainsKey(EcritureCloud.Cle(false, nord.id)), Is.False, "sinon on renverrait sa suppression");
        Assert.That(cache[EcritureCloud.Cle(false, volteo.id)].Version, Is.EqualTo(8));
        Assert.That(cache[EcritureCloud.Cle(true, b.id)].Version, Is.EqualTo(4));
        Assert.That(cache.ContainsKey("l:autre"), Is.True);
        Assert.That(EcritureCloud.Differences(b, cache), Is.Empty);
    }

    [Test]
    public void Les_parametres_portent_le_JSON_de_CIPL_en_objet()
    {
        var op = new EcritureCloud.Op { Type = EcritureCloud.TypeOp.Locataire, Id = "l1", BatimentId = "b1", Ordre = 2, Version = 5, Json = "{\"Name\":\"Volteo\"}" };
        Assert.That(EcritureCloud.CorpsEnregistrer("e1", op, "s1"),
            Is.EqualTo("{\"p_entreprise\":\"e1\",\"p_id\":\"l1\",\"p_batiment\":\"b1\",\"p_ordre\":2,\"p_version\":5,\"p_data\":{\"Name\":\"Volteo\"},\"p_session\":\"s1\",\"p_modele\":" + DonneesCloud.Modele + "}"));
    }

    /// 13/10 : recharger l'entreprise pendant que l'accueil est affiché construit les fiches
    /// sous un parent caché ; Button.image restait nul (TuilesATraiter) et aucune fiche n'apparaissait.
    [Test]
    public void Un_bouton_construit_sous_un_parent_cache_a_son_image()
    {
        var parent = new GameObject("Cache", typeof(RectTransform));
        parent.SetActive(false);
        try
        {
            Assert.That(UIFactory.Button(parent.transform, "Toutes", Color.white, Color.black).image, Is.Not.Null);
            Assert.That(UIFactory.Input(parent.transform, "Nom").targetGraphic, Is.Not.Null);
        }
        finally { Object.DestroyImmediate(parent); }
    }

    [Test]
    public void Un_refus_dit_qui_et_pourquoi()
    {
        var verrou = new EcritureCloud.Resultat { refus = "verrou", nom = "Marie Dupont", motif = "modification", pris_le = "pas une date" };
        StringAssert.StartsWith("« Volteo » : En cours de modification par Marie Dupont (modification) depuis", EcritureCloud.MotifRefus(verrou, "Volteo"));

        var version = new EcritureCloud.Resultat { refus = "version", nom = "Paul Lefèvre" };
        Assert.That(EcritureCloud.MotifRefus(version, "Volteo"),
            Is.EqualTo("« Volteo » a été modifié entre-temps par Paul Lefèvre : vos modifications ne sont pas enregistrées, la fiche est rechargée."));
    }

    // Revue 17 (D9-04, lot E) : une relecture aux mêmes fiches et mêmes versions ne reconstruit rien.
    [Test]
    public void E_D9_04_relecture_inchangee_seulement_si_memes_fiches_et_versions()
    {
        EcritureCloud.Etat E(int v, string bat = null) => new EcritureCloud.Etat { Version = v, BatimentId = bat };
        var connu = new Dictionary<string, EcritureCloud.Etat>
        {
            ["b:B1"] = E(3), ["l:L1"] = E(5, "B1"), ["l:L2"] = E(1, "B1"),
            ["b:B2"] = E(9), ["l:L9"] = E(2, "B2"),   // un autre bâtiment ne compte pas
        };
        var relu = new Dictionary<string, EcritureCloud.Etat> { ["b:B1"] = E(3), ["l:L1"] = E(5, "B1"), ["l:L2"] = E(1, "B1") };
        Assert.That(EcritureCloud.Inchange(connu, "B1", relu), Is.True, "mêmes fiches, mêmes versions : rien à reconstruire");

        relu["l:L1"] = E(6, "B1");
        Assert.That(EcritureCloud.Inchange(connu, "B1", relu), Is.False, "un locataire changé par un collègue");
        relu["l:L1"] = E(5, "B1");
        relu["l:L3"] = E(1, "B1");
        Assert.That(EcritureCloud.Inchange(connu, "B1", relu), Is.False, "un locataire ajouté");
        relu.Remove("l:L3"); relu.Remove("l:L2");
        Assert.That(EcritureCloud.Inchange(connu, "B1", relu), Is.False, "un locataire supprimé");
        Assert.That(EcritureCloud.Inchange(connu, "B3", new Dictionary<string, EcritureCloud.Etat> { ["b:B3"] = E(1) }), Is.False,
                    "bâtiment inconnu ici : à installer");
        Assert.That(EcritureCloud.Inchange(connu, "B1", null), Is.False);
    }
}
