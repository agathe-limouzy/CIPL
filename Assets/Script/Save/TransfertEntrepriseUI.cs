using System;
using System.Collections.Generic;
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
        // Sur le cloud (01/11, CONCEPTION §16) : un administrateur seulement ; le transfert passe par le serveur
        // (TransfertCloud), plus par les dossiers du PC (revue 16c, D2-01 / D8-01).
        if (SessionCloud.Active && !(SessionCloud.Entreprise?.EstAdmin ?? false))
        {
            if (existant != null) existant.gameObject.SetActive(false);
            return;
        }
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
        if (SessionCloud.Active) { OuvrirCloud(bp, null); return; }
        Choisir($"Transférer « {bat.Name} »", dest => Confirmer(bat, null, dest, () => ExecuterBatiment(bp, dest)));
    }

    public static void OuvrirLocataire(LocatairePrefab lp)
    {
        var bat = lp?.batimentPrefabOrigin?.getBatiment();
        var loc = lp?.GetLocataire();
        if (bat == null || loc == null) return;
        if (SessionCloud.Active) { OuvrirCloud(lp.batimentPrefabOrigin, loc); return; }
        Choisir($"Transférer « {loc.Name} »", dest => Confirmer(bat, loc, dest, () => ExecuterLocataire(lp, dest)));
    }

    // ── Étapes ─────────────────────────────────────────────────────────────────

    static void Confirmer(Batiment bat, Locataire loc, Entreprise dest, Action executer)
    {
        TransfertEntreprise.Rapport a;
        try { a = TransfertEntreprise.Apercu(bat, loc, dest.racine); }
        catch (Exception e) { ConfirmDialog.Erreur($"Transfert impossible : {e.Message}"); return; }

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

        if (ConfirmDialog.Instance == null) { ConfirmDialog.Erreur("Confirmation indisponible : rien n'a été transféré."); return; }
        ConfirmDialog.Instance.Show("Transférer vers " + dest.nom + " ?", detail, executer, "Transférer");
    }

    static void ExecuterBatiment(BatimentPrefab bp, Entreprise dest)
    {
        var bat = bp.getBatiment();
        string origine = SaveLocationService.GetSaveRoot(), nomOrigine = NomActive(), nom = bat.Name;
        BatimentManager.Instance.SaveBatiment(bat);   // l'état affiché est celui qui part

        var r = TransfertEntreprise.TransfererBatiment(bat, origine, dest.racine);
        if (!r.Succes) { ConfirmDialog.Erreur($"Transfert annulé : {r.Erreur}. Rien n'a été retiré de {nomOrigine}."); return; }

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
        if (!r.Succes) { ConfirmDialog.Erreur($"Transfert annulé : {r.Erreur}. Rien n'a été retiré de {nomOrigine}."); return; }

        string dossier = Path.Combine(TransfertEntreprise.Dossier(origine, bat.Name), DossiersDonnees.NomDossier(loc.Name));
        bp.DeleteLocataire(loc.id);   // retire la fiche et sauvegarde le bâtiment
        try { TransfertEntreprise.MettreEnCorbeille(dossier, origine); }
        catch (Exception e)
        {
            Debug.LogError($"[Transfert] Dossier de « {nom} » resté dans {nomOrigine} : {e.Message}");
            ConfirmDialog.Erreur($"« {nom} » transféré vers {dest.nom}, mais son dossier n'a pas pu être mis en corbeille ({e.Message}).");
            return;
        }
        Info($"« {nom} » transféré vers {dest.nom}. Son dossier d'origine est dans la corbeille de {nomOrigine}.");
    }

    // ── Choix de l'entreprise ─────────────────────────────────────────────────

    static void Choisir(string titre, Action<Entreprise> onChoix)
    {
        var autres = EntrepriseService.All().Where(e => !EntrepriseService.EstActive(e)).ToList();
        if (autres.Count == 0) { ConfirmDialog.Erreur("Aucune autre entreprise : créez-en une depuis le menu Entreprises."); return; }
        ChoisirParmi(titre, autres.Select(e => (e.nom, (Action)(() => onChoix(e)))).ToList());
    }

    /// La fenêtre de choix, pour le disque comme pour le cloud (01/11) : un bouton par entreprise.
    static void ChoisirParmi(string titre, List<(string nom, Action choix)> options)
    {
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

            foreach (var o in options)
            {
                var o2 = o;
                var b = UIFactory.Button(v.transform, string.IsNullOrWhiteSpace(o.nom) ? "(sans nom)" : o.nom,
                                         UITheme.Primaire, Color.white, 44, UITheme.Role.Bouton);
                b.onClick.AddListener(() => { UnityEngine.Object.Destroy(scrim.gameObject); o2.choix(); });
            }

            var annuler = UIFactory.Button(v.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 44, UITheme.Role.Bouton);
            UIFactory.Border(annuler.gameObject);
            annuler.onClick.AddListener(() => UnityEngine.Object.Destroy(scrim.gameObject));
        });
    }

    // ── Cloud (01/11, CONCEPTION §16) ─────────────────────────────────────────

    static async void OuvrirCloud(BatimentPrefab bp, Locataire seul)
    {
        var bat = bp?.getBatiment();
        if (bat == null) return;
        string bloque = TransfertCloud.Bloque(seul != null ? new[] { seul } : (bat.locataireDuBatiment ?? new List<Locataire>()).ToArray());
        if (bloque != null)
        {
            ConfirmDialog.Erreur("Impossible pour l'instant : " + bloque + " Lancez l'application (ou cliquez « Payé » / « Envoyé » "
                                 + "depuis le suivi) pour que cela parte, puis recommencez.");
            return;
        }
        var (liste, erreur) = await DonneesCloud.MesEntreprises();
        if (liste == null) { ConfirmDialog.Erreur("Liste des entreprises illisible : " + erreur); return; }
        var autres = liste.Where(e => e != null && e.id != SessionCloud.Entreprise?.id && e.EstAdmin).ToList();
        if (autres.Count == 0) { ConfirmDialog.Erreur("Aucune autre entreprise du groupe où vous êtes administrateur."); return; }
        string quoi = seul != null ? seul.Name : bat.Name;
        ChoisirParmi($"Transférer « {quoi} »", autres.Select(e => (e.nom, (Action)(() => ConfirmerCloud(bp, seul, e)))).ToList());
    }

    static async void ConfirmerCloud(BatimentPrefab bp, Locataire seul, EntrepriseCloud dest)
    {
        var bat = bp.getBatiment();
        BatimentManager.Instance.SaveBatiment(bat);   // l'état affiché est celui qui part
        var a = await TransfertCloud.Preparer(bat, seul, dest);
        if (a.erreur != null) { ConfirmDialog.Erreur("Transfert impossible : " + a.erreur); return; }
        string origine = SessionCloud.Entreprise?.nom ?? "l'entreprise actuelle";
        string quoi;
        if (seul == null)
            quoi = a.batimentExistant
                ? $"« {bat.Name} » existe déjà dans {dest.nom} : ses informations seront mises à jour, {a.locatairesRemplaces} locataire(s) "
                  + $"remplacé(s), {a.locatairesAjoutes} ajouté(s) ; ceux qui n'existent que là-bas sont conservés"
                : $"« {bat.Name} » sera créé dans {dest.nom} avec ses {a.locatairesAjoutes} locataire(s)";
        else
            quoi = !a.batimentExistant
                ? $"« {bat.Name} » n'existe pas dans {dest.nom} : il y sera créé complet (achats, travaux, charges) avec « {seul.Name} » pour seul locataire"
                : a.locatairesRemplaces > 0 ? $"« {seul.Name} » remplacera le locataire du même nom dans « {bat.Name} » ({dest.nom})"
                                             : $"« {seul.Name} » sera ajouté au bâtiment « {bat.Name} » de {dest.nom}";
        string detail = quoi + $", avec {a.documents} document(s) (factures, baux, photos).\n\n"
            + $"Dans {origine}, " + (seul == null ? $"« {bat.Name} »" : $"« {seul.Name} »")
            + " disparaît ; ses documents restent consultables dans la corbeille de chaque poste."
            + (a.batimentExistant ? $"\nLa version actuelle de {dest.nom} est gardée avant (corbeille du serveur)." : "")
            + "\n\nLes RIB et textes de facture sont propres à chaque entreprise : à re-choisir à la prochaine facture.";
        if (ConfirmDialog.Instance == null) { ConfirmDialog.Erreur("Confirmation indisponible : rien n'a été transféré."); return; }
        ConfirmDialog.Instance.Show("Transférer vers " + dest.nom + " ?", detail, () => ExecuterCloud(bat, seul, dest, a), "Transférer");
    }

    static async void ExecuterCloud(Batiment bat, Locataire seul, EntrepriseCloud dest, TransfertCloud.Apercu a)
    {
        string origine = SessionCloud.Entreprise?.nom ?? "l'entreprise actuelle";
        string nom = seul != null ? seul.Name : bat.Name;
        string erreur = await TransfertCloud.Executer(bat, seul, dest, Info);
        if (erreur != null) { ConfirmDialog.Erreur($"Transfert annulé : {erreur} Rien n'a été retiré de {origine}."); return; }
        Info($"« {nom} » transféré vers {dest.nom} ({a.locatairesAjoutes + a.locatairesRemplaces} locataire(s), {a.documents} document(s)).");
        DemarrageCloud.Recharger();
    }

    static string NomActive()
    {
        var e = EntrepriseService.All().Find(EntrepriseService.EstActive);
        return e != null && !string.IsNullOrWhiteSpace(e.nom) ? e.nom : "l'entreprise actuelle";
    }

    static void Info(string message) => UndoToast.Instance?.ShowInfo(message);
}
