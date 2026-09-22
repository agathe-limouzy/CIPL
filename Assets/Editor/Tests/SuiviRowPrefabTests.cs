using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Le prefab de ligne du suivi de facturation et son câblage.
///
/// La revue de code écartait la conversion des formulaires en prefab au motif que les
/// références sérialisées sont « fragiles, invisibles aux tests ». Pour la ligne de
/// suivi, la conversion a tout de même été faite — parce que la ligne était construite
/// en double, ce qui avait laissé le défaut d'alignement `childForceExpandWidth`
/// exister deux fois, et la garde « Quittance » diverger entre les deux vues.
///
/// Ces tests rendent le câblage visible : une référence qui saute à la réimportation,
/// un `childForceExpandWidth` recoché par erreur dans l'inspecteur ou une largeur de
/// colonne modifiée d'un côté seulement font échouer la suite, sans passer par le Play.
public class SuiviRowPrefabTests
{
    const string CheminLigne = "Assets/Prefab/SuiviFactureRow.prefab";
    const string CheminFiche = "Assets/Prefab/LocatairePrefab.prefab";

    static SuiviRowUI Ligne()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(CheminLigne);
        Assert.That(go, Is.Not.Null, CheminLigne + " est introuvable");
        var ui = go.GetComponent<SuiviRowUI>();
        Assert.That(ui, Is.Not.Null, "le prefab ne porte pas de SuiviRowUI");
        return ui;
    }

    [Test]
    public void Les_references_du_prefab_sont_toutes_cablees()
    {
        var r = Ligne();
        Assert.That(r.fond, Is.Not.Null, "fond");
        Assert.That(r.libelle, Is.Not.Null, "libelle");
        Assert.That(r.echeance, Is.Not.Null, "echeance");
        Assert.That(r.montant, Is.Not.Null, "montant");
        Assert.That(r.etatCell, Is.Not.Null, "etatCell");
        Assert.That(r.actionsCell, Is.Not.Null, "actionsCell");
        Assert.That(r.pastille, Is.Not.Null, "pastille");

        // Quatre boutons d'action pré-créés : c'est le maximum simultané prévu
        // (PDF · Corriger/Refaire · Rappel · Quittance).
        Assert.That(r.actions, Is.Not.Null.And.Length.EqualTo(4), "actions");
        for (int i = 0; i < r.actions.Length; i++)
            Assert.That(r.actions[i], Is.Not.Null, "actions[" + i + "]");
    }

    [Test]
    public void Chaque_bouton_porte_un_libelle_et_une_bordure()
    {
        var r = Ligne();
        Assert.That(r.pastille.GetComponentInChildren<TMP_Text>(true), Is.Not.Null,
            "la pastille n'a pas de texte — Setup n'aurait rien où écrire l'état");
        Assert.That(r.pastille.GetComponent<Outline>(), Is.Not.Null, "bordure de la pastille");
        foreach (var b in r.actions)
        {
            Assert.That(b.GetComponentInChildren<TMP_Text>(true), Is.Not.Null,
                "le bouton " + b.name + " n'a pas de texte");
            Assert.That(b.GetComponent<Outline>(), Is.Not.Null, "bordure de " + b.name);
        }
    }

    /// Le défaut historique, figé ici : activé, `childForceExpandWidth` fait ignorer les
    /// `flexibleWidth` à Unity, qui répartit alors l'espace restant à parts égales — les
    /// largeurs fixes ne sont plus respectées et l'en-tête se décale des lignes.
    [Test]
    public void La_ligne_ne_force_pas_l_expansion_en_largeur()
    {
        var hl = Ligne().GetComponent<HorizontalLayoutGroup>();
        Assert.That(hl, Is.Not.Null, "la ligne n'a pas de HorizontalLayoutGroup");
        Assert.That(hl.childForceExpandWidth, Is.False,
            "childForceExpandWidth réactivé : les colonnes vont se décaler de l'en-tête");
        Assert.That(hl.childControlWidth, Is.True);
        Assert.That(hl.childControlHeight, Is.True);
    }

    /// En-tête et lignes passent par le même prefab, donc par ces largeurs-là : elles
    /// ne peuvent plus diverger. Le test les compare aux constantes du script, seule
    /// source pour le code appelant.
    [Test]
    public void Les_largeurs_de_colonnes_suivent_les_constantes()
    {
        var r = Ligne();
        Assert.That(Largeur(r.echeance.gameObject), Is.EqualTo(SuiviRowUI.WEch), "Échéance");
        Assert.That(Largeur(r.montant.gameObject), Is.EqualTo(SuiviRowUI.WMontant), "Montant");
        Assert.That(Largeur(r.etatCell), Is.EqualTo(SuiviRowUI.WEtat), "État");
        Assert.That(Largeur(r.actionsCell), Is.EqualTo(SuiviRowUI.WActions), "Actions");

        // La colonne « Facture » est la seule flexible : c'est elle qui absorbe le reste.
        var le = r.libelle.GetComponent<LayoutElement>();
        Assert.That(le, Is.Not.Null, "LayoutElement du libellé");
        Assert.That(le.flexibleWidth, Is.EqualTo(1f));
        Assert.That(le.minWidth, Is.EqualTo(200f));
    }

    static float Largeur(GameObject go)
    {
        var le = go.GetComponent<LayoutElement>();
        Assert.That(le, Is.Not.Null, "LayoutElement absent sur " + go.name);
        Assert.That(le.preferredWidth, Is.EqualTo(le.minWidth),
            go.name + " : largeur mini et préférée doivent être identiques (colonne fixe)");
        Assert.That(le.flexibleWidth, Is.EqualTo(0f), go.name + " : une colonne fixe ne s'étire pas");
        return le.preferredWidth;
    }

    // ── Comportement de Setup ───────────────────────────────────────────────────
    //
    // Le prefab est chargé dans une scène de prévisualisation isolée
    // (LoadPrefabContents) : instancier dans la scène ouverte la salirait, et le
    // lanceur de tests refuse de démarrer sur une scène non enregistrée.
    //
    // Les libellés passés à TMP ci-dessous sont VOLONTAIREMENT sans accent, sans « € »
    // et sans tiret cadratin : un glyphe absent de la police fait grossir l'atlas
    // dynamique de `LiberationSans SDF - Fallback.asset`, un asset du dépôt, à chaque
    // exécution des tests (constaté : +114 lignes de glyphes). Les assertions ne
    // comparent que la chaîne transmise, jamais le rendu — l'ASCII suffit. Ne pas
    // « corriger » l'orthographe de ces littéraux.

    static void SurUneLigne(System.Action<SuiviRowUI> test)
    {
        var racine = PrefabUtility.LoadPrefabContents(CheminLigne);
        try { test(racine.GetComponent<SuiviRowUI>()); }
        finally { PrefabUtility.UnloadPrefabContents(racine); }
    }

    static readonly SuiviRowUI.Couleurs Neutres =
        new SuiviRowUI.Couleurs(Color.white, Color.black, Color.gray);

    [Test]
    public void Setup_remplit_les_cellules_et_n_active_que_les_actions_fournies()
    {
        SurUneLigne(r =>
        {
            int clics = 0;
            r.Setup("Loyer mars 2026", "05/03/2026", "1 200,00 EUR", Neutres,
                "Envoye", Color.blue, Color.white, true, null,
                new[] { ("PDF", (System.Action)(() => clics++)), ("Corriger", (System.Action)(() => { })) });

            Assert.That(r.libelle.text, Is.EqualTo("Loyer mars 2026"));
            Assert.That(r.echeance.text, Is.EqualTo("05/03/2026"));
            Assert.That(r.montant.text, Is.EqualTo("1 200,00 EUR"));
            Assert.That(r.pastille.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Envoye"));
            Assert.That(r.pastille.interactable, Is.True);

            Assert.That(r.actions[0].gameObject.activeSelf, Is.True, "1re action");
            Assert.That(r.actions[1].gameObject.activeSelf, Is.True, "2e action");
            Assert.That(r.actions[2].gameObject.activeSelf, Is.False, "3e action non fournie");
            Assert.That(r.actions[3].gameObject.activeSelf, Is.False, "4e action non fournie");
            Assert.That(r.actions[0].GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("PDF"));

            // Le callback doit être celui de CETTE ligne : une ligne réutilisée qui
            // garderait l'abonnement précédent agirait sur la mauvaise facture.
            r.actions[0].onClick.Invoke();
            Assert.That(clics, Is.EqualTo(1));
        });
    }

    /// Période clôturée (reprise) : pastille non cliquable et aucune action.
    [Test]
    public void Setup_sans_action_laisse_les_quatre_boutons_eteints()
    {
        SurUneLigne(r =>
        {
            r.Setup("Loyer 2023 (repris)", "-", "-", Neutres,
                "Cloture", Color.gray, Color.black, false, null, new (string, System.Action)[0]);

            Assert.That(r.pastille.interactable, Is.False);
            foreach (var b in r.actions)
                Assert.That(b.gameObject.activeSelf, Is.False, b.name);
        });
    }

    /// Une ligne réutilisée après un `SetupVide` doit retrouver toutes ses cellules :
    /// `SetupVide` les désactive, `Setup` doit les rallumer.
    [Test]
    public void SetupVide_masque_les_cellules_et_Setup_les_retablit()
    {
        SurUneLigne(r =>
        {
            r.SetupVide("Aucune facture pour cette annee.", Color.white, Color.gray);
            Assert.That(r.libelle.text, Is.EqualTo("Aucune facture pour cette annee."));
            Assert.That(r.echeance.gameObject.activeSelf, Is.False, "échéance masquée");
            Assert.That(r.montant.gameObject.activeSelf, Is.False, "montant masqué");
            Assert.That(r.etatCell.activeSelf, Is.False, "colonne État masquée");
            Assert.That(r.actionsCell.activeSelf, Is.False, "colonne Actions masquée");

            r.Setup("Loyer avril 2026", "05/04/2026", "1 200,00 EUR", Neutres,
                "A faire", Color.yellow, Color.black, true, null,
                new[] { ("Générer", (System.Action)(() => { })) });
            Assert.That(r.echeance.gameObject.activeSelf, Is.True);
            Assert.That(r.montant.gameObject.activeSelf, Is.True);
            Assert.That(r.etatCell.activeSelf, Is.True);
            Assert.That(r.actionsCell.activeSelf, Is.True);
        });
    }

    /// L'en-tête passe par le même prefab que les lignes — c'est tout l'intérêt : ses
    /// colonnes ne peuvent plus se décaler. État et Actions y deviennent du texte nu.
    [Test]
    public void SetupEntete_rend_les_colonnes_Etat_et_Actions_non_cliquables()
    {
        SurUneLigne(r =>
        {
            r.SetupEntete("Facture", "Echeance", "Montant TTC", "Etat", "Actions",
                Color.gray, Color.black);

            Assert.That(r.libelle.text, Is.EqualTo("Facture"));
            Assert.That(r.pastille.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Etat"));
            Assert.That(r.pastille.interactable, Is.False, "l'en-tête n'ouvre pas le menu d'état");
            Assert.That(r.pastille.GetComponent<Outline>().enabled, Is.False, "pas de bordure sur un titre");

            Assert.That(r.actions[0].gameObject.activeSelf, Is.True);
            Assert.That(r.actions[0].GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Actions"));
            Assert.That(r.actions[0].interactable, Is.False);
            for (int i = 1; i < r.actions.Length; i++)
                Assert.That(r.actions[i].gameObject.activeSelf, Is.False, "action " + i);
        });
    }

    /// Les deux vues du suivi sont créées par code (AddComponent / new GameObject) et ne
    /// peuvent donc pas recevoir de référence par l'inspecteur : le prefab transite par
    /// la fiche locataire. Si ce câblage saute, le tableau de suivi n'affiche plus rien.
    [Test]
    public void La_fiche_locataire_porte_le_prefab_de_ligne()
    {
        var fiche = AssetDatabase.LoadAssetAtPath<GameObject>(CheminFiche);
        Assert.That(fiche, Is.Not.Null, CheminFiche + " est introuvable");
        var lp = fiche.GetComponent<LocatairePrefab>();
        Assert.That(lp, Is.Not.Null, "LocatairePrefab absent du prefab de fiche");
        Assert.That(lp.suiviRowPrefab, Is.Not.Null,
            "suiviRowPrefab n'est pas câblé — le suivi de facturation resterait vide");
        Assert.That(lp.suiviRowPrefab.GetComponent<SuiviRowUI>(), Is.Not.Null,
            "le prefab câblé n'est pas une ligne de suivi");
    }
}
