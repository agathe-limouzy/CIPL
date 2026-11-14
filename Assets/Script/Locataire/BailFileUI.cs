using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Ligne « Bail » de la fiche locataire, avec une ligne par avenant sous elle.
///
/// Les documents choisis sont COPIÉS dans le dossier du locataire
/// (Batiment/&lt;bâtiment&gt;/&lt;locataire&gt;/Bail/, à côté de Facture/) : ils suivent les
/// sauvegardes et ne dépendent plus de l'endroit d'où on les a pris. Seul le nom de
/// fichier est enregistré (voir DossiersDonnees.DossierBail). « Retirer » détache le
/// document de la fiche sans supprimer le fichier : rien ne se perd par un clic.
public class BailFileUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text txtChemin;       // affiche le nom du fichier lié
    public Button btnParcourir;      // choisir le fichier
    public Button btnOuvrir;         // ouvrir le fichier
    public Button btnRetirer;        // délier

    private LocatairePrefab _locatairePrefab;
    private Button _btnAvenant;
    private readonly List<GameObject> _lignesDocs = new List<GameObject>();   // avenants, état des lieux

    public void Init(LocatairePrefab locatairePrefab)
    {
        _locatairePrefab = locatairePrefab;

        Brancher(btnParcourir, () => Reserve(() =>
        {
            string nom = ChoisirEtCopier("Choisir le bail");
            if (nom != null) Modifier(loc => loc.cheminBail = nom);
        }));
        Brancher(btnOuvrir, () => Ouvrir(Locataire()?.cheminBail));
        if (btnRetirer != null) Brancher(btnRetirer, () => Reserve(() => Modifier(loc => loc.cheminBail = "")));

        // « + Avenant », cloné de « Parcourir… » pour en garder l'allure, en bout de ligne.
        // Largeur au texte d'abord : le libellé change (« Ajouter le bail » / « Modifier »),
        // et le clone hérite du réglage.
        if (_btnAvenant == null)
        {
            UIFactory.LargeurDuTexte(btnParcourir);
            _btnAvenant = Instantiate(btnParcourir, btnParcourir.transform.parent);
            _btnAvenant.name = "BtnAvenant";
            _btnAvenant.onClick = new Button.ButtonClickedEvent();
            _btnAvenant.transform.SetAsLastSibling();
            var t = _btnAvenant.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = "+ Avenant";
        }
        Brancher(_btnAvenant, () => Reserve(() =>
        {
            string nom = ChoisirEtCopier("Choisir l'avenant");
            if (nom != null) Modifier(loc => (loc.avenants ??= new List<string>()).Add(nom));
        }));

        Refresh();
    }

    /// Joint le PDF de l'état des lieux de sortie (fenêtre « Départ du locataire ») :
    /// copié dans le dossier Bail du locataire, enregistré. Vrai si un document a été joint.
    public bool JoindreEtatDesLieux()
    {
        string nom = ChoisirEtCopier("Choisir l'état des lieux de sortie");
        if (nom == null) return false;
        Modifier(loc => loc.etatDesLieux = nom);
        return true;
    }

    public void Refresh()
    {
        var loc = Locataire();
        AfficherLigne(txtChemin, btnOuvrir, btnRetirer, loc?.cheminBail, "Aucun bail lié");

        // Sans bail : seulement « Ajouter le bail ». Avec : Modifier · Ouvrir · + Avenant.
        bool bailLie = !string.IsNullOrEmpty(loc?.cheminBail);
        var libelle = btnParcourir.GetComponentInChildren<TMP_Text>(true);
        if (libelle != null) libelle.text = bailLie ? "Modifier" : "Ajouter le bail";
        btnOuvrir.gameObject.SetActive(bailLie);
        if (_btnAvenant != null) _btnAvenant.gameObject.SetActive(bailLie);

        // « Bail : », « Avenant n : » et « État des lieux : » ont la même largeur : les
        // noms de fichier s'alignent, et aucune étiquette ne déborde sur le nom.
        var etiquette = transform.Find("Label")?.GetComponent<TMP_Text>();
        float largeurEtiquette = etiquette != null ? etiquette.GetPreferredValues("État des lieux :").x : 0f;
        Etiqueter(etiquette, "Bail :", largeurEtiquette);

        // Une ligne par avenant, puis l'état des lieux, clonées de celle du bail, dessous.
        foreach (var l in _lignesDocs) Destroy(l);
        _lignesDocs.Clear();
        if (loc == null) return;
        int n0 = 0;
        if (loc.avenants != null)
            for (int i = 0; i < loc.avenants.Count; i++)
            {
                int n = i;
                LigneDocument($"RowAvenant{n + 1}", $"Avenant {n + 1} :", loc.avenants[i], n0++,
                              largeurEtiquette, l => l.avenants.RemoveAt(n));
            }
        if (!string.IsNullOrEmpty(loc.etatDesLieux))
            LigneDocument("RowEtatDesLieux", "État des lieux :", loc.etatDesLieux, n0,
                          largeurEtiquette, l => l.etatDesLieux = "");
    }

    // Ligne d'un document secondaire (avenant, état des lieux) : Ouvrir et Retirer.
    void LigneDocument(string nomObjet, string etiquette, string stocke, int rang, float largeur, Action<Locataire> retirer)
    {
        var ligne = Instantiate(gameObject, transform.parent);
        ligne.name = nomObjet;
        ligne.transform.SetSiblingIndex(transform.GetSiblingIndex() + 1 + rang);
        Destroy(ligne.GetComponent<BailFileUI>());

        Etiqueter(ligne.transform.Find("Label")?.GetComponent<TMP_Text>(), etiquette, largeur);
        ligne.transform.Find(btnParcourir.name)?.gameObject.SetActive(false);
        ligne.transform.Find(_btnAvenant.name)?.gameObject.SetActive(false);

        var txt = ligne.transform.Find(txtChemin.name)?.GetComponent<TMP_Text>();
        var ouvrir = ligne.transform.Find(btnOuvrir.name)?.GetComponent<Button>();
        var btnRet = btnRetirer != null ? ligne.transform.Find(btnRetirer.name)?.GetComponent<Button>() : null;
        AfficherLigne(txt, ouvrir, btnRet, stocke, "");
        if (ouvrir != null) { ouvrir.gameObject.SetActive(true); Brancher(ouvrir, () => Ouvrir(stocke)); }
        if (btnRet != null) Brancher(btnRet, () => Reserve(() => Modifier(retirer)));
        _lignesDocs.Add(ligne);
    }

    // ── Aides ──────────────────────────────────────────────────────────────────

    Locataire Locataire() => _locatairePrefab != null ? _locatairePrefab.GetLocataire() : null;

    string NomBatiment() => _locatairePrefab.batimentPrefabOrigin.getName();

    string Chemin(string stocke)
    {
        var loc = Locataire();
        return loc == null ? stocke : DossiersDonnees.CheminDocumentBail(NomBatiment(), loc.Name, stocke);
    }

    static void Etiqueter(TMP_Text etiquette, string texte, float largeur)
    {
        if (etiquette == null) return;
        etiquette.text = texte;
        if (largeur <= 0f) return;
        var le = etiquette.GetComponent<LayoutElement>() ?? etiquette.gameObject.AddComponent<LayoutElement>();
        le.minWidth = largeur; le.preferredWidth = largeur;
    }

    static void Brancher(Button b, Action action)
    {
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => action());
    }

    void AfficherLigne(TMP_Text txt, Button ouvrir, Button retirer, string stocke, string texteVide)
    {
        bool lie = !string.IsNullOrEmpty(stocke);
        string chemin = Chemin(stocke);
        var (texte, ouvrable) = EtatLigne(lie, lie && File.Exists(chemin), SessionCloud.Active, lie ? Path.GetFileName(chemin) : "", texteVide);
        if (txt != null) txt.text = texte;
        if (ouvrir != null) ouvrir.interactable = ouvrable;
        if (retirer != null) retirer.gameObject.SetActive(lie);
    }

    /// Revue 17 (D7a-21, lot D) : texte et bouton « Ouvrir » d'une ligne de document (bail, avenant, état des lieux). Sur le cloud,
    /// un document lié pas encore arrivé sur ce PC s'ouvre quand même (DocumentsCloud.Ouvrir le lit depuis le cloud) : « Pas encore
    /// reçu : nom ». Hors cloud : « Introuvable : nom », grisé. Avant : grisé et « Introuvable » aussi sur le cloud.
    public static (string texte, bool ouvrable) EtatLigne(bool lie, bool existe, bool cloud, string nom, string texteVide)
    {
        if (!lie) return (texteVide, false);
        if (existe) return (nom, true);
        return cloud ? ($"Pas encore reçu : {nom}", true) : ($"Introuvable : {nom}", false);
    }

    // Revue 17 (D7a-23, lot D) : « Ajouter le bail », « + Avenant » et « Retirer » réservent la fiche AVANT le sélecteur de fichier :
    // un refus (fiche prise, hors ligne, lecture seule) arrive avant toute copie. Avant, le fichier était copié (et partait au
    // cloud) puis l'enregistrement refusé : un document orphelin, et « bail (2).pdf » au second essai. Le verrou tient tant que
    // l'enregistrement est en file (gardien d'EcritureCloud) ; JoindreEtatDesLieux n'en a pas besoin (fenêtre du départ, déjà réservée).
    void Reserve(Action action)
    {
        if (_locatairePrefab == null) return;
        EcritureCloud.Reserver(EcritureCloud.Cibles(_locatairePrefab, false), "bail",
            () => { if (this != null && _locatairePrefab != null) action(); }, () => false);
    }

    // Choisit un document et le copie dans le dossier du locataire. Nom retenu, ou null.
    string ChoisirEtCopier(string titre)
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        var loc = Locataire();
        if (loc == null) return null;
        var paths = SFB.StandaloneFileBrowser.OpenFilePanel(titre, "",
            new[] { new SFB.ExtensionFilter("Documents", "pdf", "doc", "docx", "jpg", "png") }, false);
        if (paths == null || paths.Length == 0 || string.IsNullOrEmpty(paths[0])) return null;
        try
        {
            return DossiersDonnees.CopierSansEcraser(paths[0], DossiersDonnees.DossierBail(NomBatiment(), loc.Name));
        }
        catch (Exception e)
        {
            ConfirmDialog.Erreur($"Copie du document impossible : {e.Message}");
            return null;
        }
#else
        return null;
#endif
    }

    void Ouvrir(string stocke)
    {
        string chemin = Chemin(stocke);
        if (!string.IsNullOrEmpty(chemin)) DocumentsCloud.Ouvrir(chemin);   // pas encore arrivé dans le Drive : lu depuis le cloud (étape 6b)
    }

    void Modifier(Action<Locataire> changement)
    {
        var loc = Locataire();
        if (loc == null) return;
        changement(loc);
        _locatairePrefab.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
        Refresh();
    }
}
