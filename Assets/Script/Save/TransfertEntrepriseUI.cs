using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Bouton « Transférer » des fiches bâtiment et locataire : choix de l'entreprise de
/// destination, confirmation qui dit exactement ce qui va se passer, puis transfert
/// (TransfertEntreprise) et, seulement s'il a réussi, retrait de l'origine.
public static class TransfertEntrepriseUI
{
    /// Ajoute (une seule fois) un bouton « Transférer » juste avant « Supprimer ».
    /// Appelé à chaque initialisation de fiche : le bouton existant est recâblé.
    public static void AjouterBouton(Button supprimer, Action onClick)
    {
        if (supprimer == null) return;
        var existant = supprimer.transform.parent.Find("Transferer");
        var b = existant != null ? existant.GetComponent<Button>()
                                 : UnityEngine.Object.Instantiate(supprimer, supprimer.transform.parent);
        b.name = "Transferer";
        b.onClick = new Button.ButtonClickedEvent();
        b.onClick.AddListener(() => onClick());
        b.transform.SetSiblingIndex(supprimer.transform.GetSiblingIndex());
        // Pas la couleur de « Supprimer » : un transfert ne détruit rien.
        if (b.TryGetComponent<Image>(out var img)) img.color = UITheme.Carte;
        foreach (var t in b.GetComponentsInChildren<TMP_Text>(true)) { t.text = "Transférer"; t.color = UITheme.TextePrincipal; }
        if (existant == null) UIFactory.Border(b.gameObject);
        b.gameObject.SetActive(true);
    }

    public static void OuvrirBatiment(BatimentPrefab bp)
    {
        var bat = bp?.getBatiment();
        if (bat == null) return;
        Choisir($"Transférer « {bat.Name} »", dest => Confirmer(bat, null, dest, () => ExecuterBatiment(bp, dest)));
    }

    public static void OuvrirLocataire(LocatairePrefab lp)
    {
        var bat = lp?.batimentPrefabOrigin?.getBatiment();
        var loc = lp?.GetLocataire();
        if (bat == null || loc == null) return;
        Choisir($"Transférer « {loc.Name} »", dest => Confirmer(bat, loc, dest, () => ExecuterLocataire(lp, dest)));
    }

    // ── Étapes ─────────────────────────────────────────────────────────────────

    static void Confirmer(Batiment bat, Locataire loc, Entreprise dest, Action executer)
    {
        TransfertEntreprise.Rapport a;
        try { a = TransfertEntreprise.Apercu(bat, loc, dest.racine); }
        catch (Exception e) { Info($"Transfert impossible : {e.Message}"); return; }

        string quoi;
        if (loc == null)
            quoi = a.BatimentExistant
                ? $"« {bat.Name} » existe déjà dans {dest.nom} : ses informations seront mises à jour. "
                  + $"{a.LocatairesMisAJour} locataire(s) mis à jour, {a.LocatairesAjoutes} ajouté(s) ; "
                  + "ceux qui n'existent que là-bas sont conservés."
                : $"« {bat.Name} » sera créé dans {dest.nom} avec ses {bat.locataireDuBatiment.Count} locataire(s).";
        else
            quoi = !a.BatimentExistant
                ? $"« {bat.Name} » n'existe pas dans {dest.nom} : il y sera créé complet (achats, travaux, charges) "
                  + $"avec « {loc.Name} » pour seul locataire. Vérifiez la répartition des charges avant une régularisation."
                : a.LocatairesMisAJour > 0
                    ? $"« {loc.Name} » remplacera le locataire du même nom dans « {bat.Name} » ({dest.nom})."
                    : $"« {loc.Name} » sera ajouté au bâtiment « {bat.Name} » de {dest.nom}.";

        string detail = quoi
            + $"\n\nIl sera ensuite retiré de {NomActive()} (son dossier part dans la corbeille)."
            + (a.BatimentExistant ? $"\nLa version actuelle dans {dest.nom} est d'abord copiée dans sa corbeille." : "")
            + "\n\nLes RIB et textes de facture sont propres à chaque entreprise : à re-choisir à la prochaine facture.";

        if (ConfirmDialog.Instance == null) { Info("Confirmation indisponible : rien n'a été transféré."); return; }
        ConfirmDialog.Instance.Show("Transférer vers " + dest.nom + " ?", detail, executer, "Transférer");
    }

    static void ExecuterBatiment(BatimentPrefab bp, Entreprise dest)
    {
        var bat = bp.getBatiment();
        string origine = SaveLocationService.GetSaveRoot(), nomOrigine = NomActive(), nom = bat.Name;
        BatimentManager.Instance.SaveBatiment(bat);   // l'état affiché est celui qui part

        var r = TransfertEntreprise.TransfererBatiment(bat, origine, dest.racine);
        if (!r.Succes) { Info($"Transfert annulé : {r.Erreur}. Rien n'a été retiré de {nomOrigine}."); return; }

        // Retrait de l'origine par le chemin de suppression existant : onglet, JSON,
        // dossier mis en corbeille.
        BatimentManager.Instance.DeleteBatiment(bat.id);
        Info($"« {nom} » transféré vers {dest.nom}. Son dossier d'origine est dans la corbeille de {nomOrigine}.");
    }

    static void ExecuterLocataire(LocatairePrefab lp, Entreprise dest)
    {
        var bp = lp.batimentPrefabOrigin;
        var bat = bp.getBatiment();
        var loc = lp.GetLocataire();
        string origine = SaveLocationService.GetSaveRoot(), nomOrigine = NomActive(), nom = loc.Name;
        BatimentManager.Instance.SaveBatiment(bat);

        var r = TransfertEntreprise.TransfererLocataire(bat, loc, origine, dest.racine);
        if (!r.Succes) { Info($"Transfert annulé : {r.Erreur}. Rien n'a été retiré de {nomOrigine}."); return; }

        string dossier = Path.Combine(TransfertEntreprise.Dossier(origine, bat.Name), DossiersDonnees.NomDossier(loc.Name));
        bp.DeleteLocataire(loc.id);   // retire la fiche et sauvegarde le bâtiment
        try { TransfertEntreprise.MettreEnCorbeille(dossier, origine); }
        catch (Exception e)
        {
            Debug.LogError($"[Transfert] Dossier de « {nom} » resté dans {nomOrigine} : {e.Message}");
            Info($"« {nom} » transféré vers {dest.nom}, mais son dossier n'a pas pu être mis en corbeille ({e.Message}).");
            return;
        }
        Info($"« {nom} » transféré vers {dest.nom}. Son dossier d'origine est dans la corbeille de {nomOrigine}.");
    }

    // ── Choix de l'entreprise ─────────────────────────────────────────────────

    static void Choisir(string titre, Action<Entreprise> onChoix)
    {
        var autres = EntrepriseService.All().Where(e => !EntrepriseService.EstActive(e)).ToList();
        if (autres.Count == 0) { Info("Aucune autre entreprise : créez-en une depuis le menu Entreprises."); return; }

        // Une fiche en cours de modification serait transférée sans ses dernières saisies.
        FermetureGuard.ConfirmerPerteSaisies("Transférer", () =>
        {
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var scrim = UIFactory.Rect("TransfertScrim", canvas.rootCanvas.transform);
            UIFactory.Stretch(scrim);
            scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
            scrim.SetAsLastSibling();

            var cardImg = UIFactory.Panel("Card", scrim, UITheme.Carte);
            UIFactory.Border(cardImg.gameObject);
            var card = (RectTransform)cardImg.transform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(520, 100);
            var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10; v.padding = new RectOffset(18, 18, 16, 16);
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var csf = cardImg.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            UIFactory.Text(v.transform, titre, UITheme.Role.Section, UITheme.TextePrincipal, true);
            UIFactory.Text(v.transform, "Vers quelle entreprise ?", UITheme.Role.Aide, UITheme.TexteSecondaire);

            foreach (var e in autres)
            {
                var e2 = e;
                var b = UIFactory.Button(v.transform, string.IsNullOrWhiteSpace(e.nom) ? "(sans nom)" : e.nom,
                                         UITheme.Primaire, Color.white, 44, UITheme.Role.Bouton);
                b.onClick.AddListener(() => { UnityEngine.Object.Destroy(scrim.gameObject); onChoix(e2); });
            }

            var annuler = UIFactory.Button(v.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 44, UITheme.Role.Bouton);
            UIFactory.Border(annuler.gameObject);
            annuler.onClick.AddListener(() => UnityEngine.Object.Destroy(scrim.gameObject));
        });
    }

    static string NomActive()
    {
        var e = EntrepriseService.All().Find(EntrepriseService.EstActive);
        return e != null && !string.IsNullOrWhiteSpace(e.nom) ? e.nom : "l'entreprise actuelle";
    }

    static void Info(string message) => UndoToast.Instance?.ShowInfo(message);
}
