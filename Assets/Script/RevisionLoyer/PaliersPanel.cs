using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Écran « Paliers » du loyer (schéma du 30/09) : tableau des paliers, trié, le palier
/// en cours surligné ; « Ajouter un palier » ouvre un formulaire dessous.
///
/// Un palier démarre le lendemain de la fin du précédent (calculé, jamais saisi) et
/// finit la veille d'un début de période de facturation, ou à la fin du bail. Modifier
/// une fin décale le palier suivant. « Valider » reste grisé tant que les paliers ne
/// couvrent pas tout le bail. Les règles sont dans `Loyers` ; cet écran ne fait que
/// les afficher et travaille sur une copie, écrite sur le locataire à la validation.
public class PaliersPanel
{
    static readonly Color Ambre = Hex("#A9741C");
    static readonly Color AmbreClair = Hex("#FBF5E8");
    static readonly Color Erreur = Hex("#A32D2D");
    // Palier · Loyer · Du · Au · Durée (les actions prennent le reste).
    static readonly float[] Largeurs = { 90, 150, 110, 110, 220 };

    readonly Locataire _loc;
    readonly Action _onSaved;
    readonly List<PalierLoyer> _liste;
    readonly RectTransform _scrim;
    DateTime _debut, _finBail;
    readonly bool _datesOk;

    Transform _table;
    GameObject _form;
    TMP_Text _formTitre, _formDuTexte, _formDuTitre, _msg;
    TMP_InputField _formLoyer;
    DateInputController _formDu;          // « Du » : calculé, affiché comme les autres dates
    UIDropdown _formAu;                   // « Au » : seulement les fins valides (Loyers.FinsPossibles)
    Button _btnAjouter, _btnFormOk, _btnValider;
    int _editIndex = -1;

    /// `ancre` sert à trouver le canvas (la fiche, ou le panneau qui ouvre l'écran).
    public static void Open(Locataire loc, Action onSaved, Component ancre)
    {
        var canvas = ancre != null ? ancre.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (loc == null || canvas == null) return;
        new PaliersPanel(loc, onSaved, canvas.rootCanvas.transform);
    }

    PaliersPanel(Locataire loc, Action onSaved, Transform root)
    {
        _loc = loc;
        _onSaved = onSaved;
        bool debutOk = Loyers.DebutPaliers(loc, out _debut);
        bool finOk = FacturationSuivi.TryEcheance(loc.dateFinBailISO, out _finBail);
        _datesOk = debutOk && finOk;
        _liste = (loc.paliers ?? new List<PalierLoyer>())
            .Select(p => new PalierLoyer { debutISO = p.debutISO, finISO = p.finISO, loyer = p.loyer }).ToList();
        if (_datesOk)
        {
            // Après un avenant, les paliers finis avant lui sont dans l'historique.
            _liste.RemoveAll(p => D(p.finISO) < _debut.Date);
            Loyers.Normaliser(_liste, _debut);   // la franchise ou l'avenant a pu changer
        }

        _scrim = UIFactory.Rect("PaliersScrim", root);
        UIFactory.Stretch(_scrim);
        _scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        _scrim.SetAsLastSibling();

        var cardImg = UIFactory.Panel("PaliersCard", _scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(980, 100);
        var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        cardImg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Bandeau ambre pleine largeur, comme les modales Loyer et Dépôt.
        var header = UIFactory.Panel("Header", v.transform, Ambre);
        var haut = SpriteNomme("RoundedTop");
        if (haut != null) header.sprite = haut;
        UIFactory.LE(header.gameObject, minH: 52, prefH: 52, flexH: 0);
        var hh = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        hh.padding = new RectOffset(16, 16, 4, 4);
        hh.childControlWidth = true; hh.childControlHeight = true;
        hh.childForceExpandWidth = false; hh.childForceExpandHeight = true;
        hh.childAlignment = TextAnchor.MiddleLeft;
        UIFactory.Text(header.transform, $"Paliers de loyer · {loc.Name}", UITheme.Role.Section, Color.white, true);

        var body = UIFactory.VBox(v.transform, 10, 18, 18, 14, 16, "Body");
        UIFactory.Text(body.transform, $"Loyer de départ : {loc.loyerDepart:N2} € / an HT", UITheme.Role.Donnee, UITheme.TextePrincipal, true);
        UIFactory.Text(body.transform, BailTexte(), UITheme.Role.Donnee, UITheme.TexteSecondaire);
        UIFactory.Text(body.transform, FranchiseTexte(), UITheme.Role.Donnee, UITheme.TexteSecondaire);
        if (FacturationSuivi.TryEcheance(loc.debutConditionsISO, out var avenant))
            UIFactory.Text(body.transform, $"Avenant : ces paliers s'appliquent à partir du {avenant:dd/MM/yyyy}",
                UITheme.Role.Donnee, UITheme.TexteSecondaire);
        UIFactory.Text(body.transform,
            "Un palier démarre le lendemain du précédent et se termine la veille d'un début de période de facturation, ou à la fin du bail.",
            UITheme.Role.Aide, UITheme.TexteSecondaire).enableWordWrapping = true;

        var tableImg = UIFactory.Panel("Paliers", body.transform, UITheme.Carte);
        UIFactory.Border(tableImg.gameObject);
        var tv = tableImg.gameObject.AddComponent<VerticalLayoutGroup>();
        tv.childControlWidth = true; tv.childControlHeight = true;
        tv.childForceExpandWidth = true; tv.childForceExpandHeight = false;
        _table = tableImg.transform;

        _btnAjouter = UIFactory.Button(body.transform, "+ Ajouter un palier", UITheme.Carte, Ambre, 40, UITheme.Role.Bouton);
        UIFactory.Border(_btnAjouter.gameObject, Ambre);
        _btnAjouter.onClick.AddListener(() => OuvrirFormulaire(-1));

        BuildFormulaire(body.transform);

        _msg = UIFactory.Text(body.transform, "", UITheme.Role.Aide, UITheme.TexteSecondaire);
        _msg.enableWordWrapping = true;

        var actions = UIFactory.HBox(body.transform, 10);
        UIFactory.LE(actions.gameObject, minH: 46);
        var fermer = UIFactory.Button(actions.transform, "Fermer", UITheme.Carte, UITheme.TextePrincipal, 44, UITheme.Role.Bouton);
        UIFactory.Border(fermer.gameObject);
        UIFactory.LE(fermer.gameObject, flexW: 1);
        fermer.onClick.AddListener(Fermer);
        _btnValider = UIFactory.Button(actions.transform, "Valider", Ambre, Color.white, 44, UITheme.Role.Bouton);
        UIFactory.LE(_btnValider.gameObject, flexW: 1);
        _btnValider.onClick.AddListener(Valider);

        Rafraichir();
        // Premier palier : le formulaire s'ouvre, pré-rempli avec le loyer de départ.
        if (_liste.Count == 0 && _datesOk) OuvrirFormulaire(-1);
    }

    // ── Tableau ────────────────────────────────────────────────────────────────

    /// Reconstruit le tableau. `erreur` s'affiche en rouge ; sans erreur, la ligne de
    /// message dit si les paliers couvrent le bail (ce qui débloque « Valider »).
    void Rafraichir(string erreur = null)
    {
        foreach (Transform c in _table) UnityEngine.Object.Destroy(c.gameObject);

        var tete = Ligne(Hex("#EDEBE3"));
        string[] titres = { "Palier", "Loyer / an HT", "Du", "Au", "Durée" };
        for (int k = 0; k < titres.Length; k++) Cellule(tete, titres[k], k, false, UITheme.TexteSecondaire);

        int enCours = EnCours();
        for (int i = 0; i < _liste.Count; i++)
        {
            var p = _liste[i];
            int idx = i;
            bool courant = i == enCours;
            var row = Ligne(courant ? UITheme.PrimaireClair : (i % 2 == 0 ? UITheme.Carte : Hex("#F6F4EC")));
            Cellule(row, courant ? $"Palier {i + 1} ●" : $"Palier {i + 1}", 0, courant, UITheme.TextePrincipal);
            Cellule(row, $"{p.loyer:N2} €", 1, true, UITheme.TextePrincipal);
            Cellule(row, Fmt(p.debutISO), 2, false, UITheme.TextePrincipal);
            Cellule(row, Fmt(p.finISO), 3, false, UITheme.TextePrincipal);
            Cellule(row, Duree(p), 4, false, UITheme.TexteSecondaire);
            var act = UIFactory.HBox(row, 6, false, "Actions");
            act.childAlignment = TextAnchor.MiddleRight;
            UIFactory.LE(act.gameObject, flexW: 1);
            PetitBouton(act.transform, "Modifier", () => OuvrirFormulaire(idx));
            PetitBouton(act.transform, "Supprimer", () => Supprimer(idx));
        }
        if (_liste.Count == 0)
        {
            var vide = Ligne(UITheme.Carte);
            UIFactory.Text(vide, "Aucun palier pour l'instant.", UITheme.Role.Aide, UITheme.TexteSecondaire);
        }

        bool couvre = Loyers.Couvrent(_loc, _liste, out var raison);
        _btnValider.interactable = couvre;
        if (erreur != null) { _msg.color = Erreur; _msg.text = erreur; }
        else { _msg.color = couvre ? UITheme.Primaire : UITheme.TexteSecondaire; _msg.text = couvre ? "Les paliers couvrent tout le bail." : raison; }
        _btnAjouter.gameObject.SetActive(!_form.activeSelf);
    }

    // Index du palier qui contient aujourd'hui, ou -1.
    int EnCours()
    {
        var today = DateTime.Today;
        for (int i = 0; i < _liste.Count; i++)
            if (D(_liste[i].debutISO) <= today && today <= D(_liste[i].finISO)) return i;
        return -1;
    }

    Transform Ligne(Color bg)
    {
        var p = UIFactory.Panel("Ligne", _table, bg, false);
        var hl = p.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(12, 12, 4, 4); hl.spacing = 8;
        hl.childControlWidth = true; hl.childControlHeight = true;
        // false : sinon la ligne réclame un flexibleHeight qui étire toute la carte.
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
        hl.childAlignment = TextAnchor.MiddleLeft;
        UIFactory.LE(p.gameObject, minH: 38);
        return p.transform;
    }

    static void Cellule(Transform row, string texte, int col, bool gras, Color couleur)
    {
        var t = UIFactory.Text(row, texte, UITheme.Role.Donnee, couleur, gras);
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(t.gameObject, prefW: Largeurs[col], minW: Largeurs[col], flexW: 0);
    }

    static Button PetitBouton(Transform parent, string libelle, Action onClick)
    {
        var b = UIFactory.Button(parent, libelle, UITheme.Carte, Ambre, 30, UITheme.Role.Action, false);
        UIFactory.Border(b.gameObject, Ambre);
        UIFactory.LargeurDuTexte(b);
        b.onClick.AddListener(() => onClick());
        return b;
    }

    // ── Formulaire (ajout / modification) ──────────────────────────────────────

    void BuildFormulaire(Transform parent)
    {
        var fImg = UIFactory.Panel("FormPalier", parent, AmbreClair);
        UIFactory.Border(fImg.gameObject, Ambre);
        var f = fImg.gameObject.AddComponent<VerticalLayoutGroup>();
        f.padding = new RectOffset(14, 14, 10, 12); f.spacing = 8;
        f.childControlWidth = true; f.childControlHeight = true;
        f.childForceExpandWidth = true; f.childForceExpandHeight = false;
        _form = fImg.gameObject;

        _formTitre = UIFactory.Text(f.transform, "", UITheme.Role.Libelle, Ambre, true);

        // Une rangée, trois colonnes de même allure : loyer · du · au.
        var rangee = UIFactory.HBox(f.transform, 14, false, "Champs");
        rangee.childAlignment = TextAnchor.UpperLeft;

        var colLoyer = Colonne(rangee.transform, "Loyer annuel HT (€)", 1f);
        _formLoyer = UIFactory.Input(colLoyer, "ex. 12000");
        _formLoyer.contentType = TMP_InputField.ContentType.DecimalNumber;

        // « Du » : même bloc de date que partout dans l'app (clone du bloc « Date de
        // révision »), en lecture seule — il est calculé, jamais saisi.
        var modele = RevisionPanel.Instance != null ? RevisionPanel.Instance.dateDeRevision : null;
        if (modele != null)
        {
            var bloc = UnityEngine.Object.Instantiate(modele.gameObject, rangee.transform);
            bloc.name = "DuPalier";
            bloc.SetActive(true);
            _formDu = bloc.GetComponent<DateInputController>();
            _formDu.OnModify.RemoveAllListeners();
            _formDuTitre = bloc.transform.Find("Titre")?.GetComponent<TMP_Text>();
            UIFactory.LE(bloc, flexW: 1f, minW: 0);
        }
        else
        {
            var colDu = Colonne(rangee.transform, "Du", 1f);
            _formDuTexte = UIFactory.Text(colDu, "", UITheme.Role.Valeur, UITheme.TextePrincipal, true);
        }

        // « Au » : une liste des SEULES fins possibles (veille d'un début de période, ou
        // fin du bail), avec la durée du palier. Plus de date impossible à saisir.
        var colAu = Colonne(rangee.transform, "Au (fin du palier)", 2f);
        _formAu = UIDropdown.Create(colAu, new List<string>(), new List<string>(), 0, _ => { });

        // Boutons à droite, de taille normale.
        var boutons = UIFactory.HBox(f.transform, 10, false, "Boutons");
        UIFactory.LE(UIFactory.Rect("Espace", boutons.transform).gameObject, flexW: 1f);
        var annuler = UIFactory.Button(boutons.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 40, UITheme.Role.Bouton);
        UIFactory.Border(annuler.gameObject);
        UIFactory.LE(annuler.gameObject, prefW: 150, minW: 150);
        annuler.onClick.AddListener(FermerFormulaire);
        _btnFormOk = UIFactory.Button(boutons.transform, "Ajouter", Ambre, Color.white, 40, UITheme.Role.Bouton);
        UIFactory.LE(_btnFormOk.gameObject, prefW: 150, minW: 150);
        _btnFormOk.onClick.AddListener(PoserFormulaire);

        _form.SetActive(false);
    }

    // Colonne « libellé au-dessus du champ ».
    static Transform Colonne(Transform parent, string libelle, float largeur)
    {
        var col = UIFactory.VBox(parent, 4, 0, 0, 0, 0, "Colonne");
        UIFactory.LE(col.gameObject, flexW: largeur, minW: 0);
        UIFactory.Text(col.transform, libelle, UITheme.Role.Donnee, UITheme.TexteSecondaire);
        return col.transform;
    }

    void OuvrirFormulaire(int index)
    {
        if (!_datesOk) { Rafraichir("Renseignez d'abord les dates de début et de fin du bail (section Bail)."); return; }
        _editIndex = index;
        bool ajout = index < 0;
        var p = ajout ? null : _liste[index];
        _formTitre.text = $"Palier {(ajout ? _liste.Count + 1 : index + 1)}";

        // Fins proposées : de ce palier jusqu'à la fin du bail — et, pour une
        // modification, avant la fin du palier suivant (qui garderait au moins un jour).
        var du = DuFormulaire();
        var fins = Loyers.FinsPossibles(_loc, du);
        if (!ajout && index + 1 < _liste.Count) fins.RemoveAll(x => x >= D(_liste[index + 1].finISO));
        if (fins.Count == 0)
        {
            Rafraichir(ajout ? "Les paliers couvrent déjà tout le bail : modifiez le dernier pour en ajouter un."
                             : "Aucune fin possible pour ce palier : modifiez d'abord le palier suivant.");
            return;
        }

        // Ajout : le loyer du dernier palier, ou le loyer de départ pour le premier.
        float loyer = !ajout ? p.loyer : _liste.Count > 0 ? _liste[_liste.Count - 1].loyer : _loc.loyerDepart;
        _formLoyer.text = loyer > 0f ? loyer.ToString("0.##", CultureInfo.InvariantCulture) : "";
        if (_formDu != null) _formDu.ApplyDate(du); else _formDuTexte.text = du.ToString("dd/MM/yyyy");
        var fin = ajout ? _finBail.Date : D(p.finISO);
        // Premier palier : il part du début du bail (ou de l'avenant) et contient la franchise.
        bool premier = ajout ? _liste.Count == 0 : index == 0;
        if (_formDuTitre != null)
            _formDuTitre.text = !premier ? "Du (lendemain du palier précédent)"
                : FacturationSuivi.TryEcheance(_loc.debutConditionsISO, out _) ? "Du (date de l'avenant)" : "Du (début du bail)";
        _formAu.SetOptions(
            fins.Select(x => x == _finBail.Date
                ? $"{x:dd/MM/yyyy} · fin du bail ({Loyers.DureeLibelle(_loc, du, x)})"
                : $"{x:dd/MM/yyyy} · {Loyers.DureeLibelle(_loc, du, x)}").ToList(),
            fins.Select(x => x.ToString("yyyy-MM-dd")).ToList(),
            fin.ToString("yyyy-MM-dd"));
        var lbl = _btnFormOk.GetComponentInChildren<TMP_Text>(true);
        if (lbl != null) lbl.text = ajout ? "Ajouter" : "Enregistrer";

        _form.SetActive(true);
        Rafraichir();
    }

    // Début du palier du formulaire : toujours calculé (lendemain du précédent).
    DateTime DuFormulaire()
    {
        if (_editIndex >= 0) return D(_liste[_editIndex].debutISO);
        return _liste.Count > 0 ? D(_liste[_liste.Count - 1].finISO).AddDays(1) : _debut.Date;
    }

    void PoserFormulaire()
    {
        if (!SaisieNumerique.TryParse(_formLoyer.text, out float loyer)) { Rafraichir("Loyer du palier invalide."); return; }
        if (!DateTime.TryParseExact(_formAu.SelectedId, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fin))
        { Rafraichir("Choisissez la fin du palier."); return; }

        string err = Loyers.PoserPalier(_loc, _liste, _editIndex, loyer, fin);
        if (err != null) { Rafraichir(err); return; }
        FermerFormulaire();
    }

    void FermerFormulaire()
    {
        _form.SetActive(false);
        _editIndex = -1;
        Rafraichir();
    }

    void Supprimer(int index)
    {
        _liste.RemoveAt(index);
        Loyers.Normaliser(_liste, _debut);   // le suivant reprend son début
        if (_form.activeSelf) { _form.SetActive(false); _editIndex = -1; }
        Rafraichir();
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    void Valider()
    {
        if (!Loyers.Couvrent(_loc, _liste, out var raison)) { Rafraichir(raison); return; }
        _loc.paliers = _liste;
        Loyers.Actualiser(_loc, DateTime.Today);
        _onSaved?.Invoke();
        UndoToast.Instance?.ShowInfo($"Paliers enregistrés — loyer actuel : {_loc.loyerAnnuel:N2} € / an");
        Fermer();
    }

    void Fermer() => UnityEngine.Object.Destroy(_scrim.gameObject);

    // ── Textes ─────────────────────────────────────────────────────────────────

    string BailTexte()
    {
        if (!FacturationSuivi.TryEcheance(_loc.dateDebutBailISO, out var db) || !FacturationSuivi.TryEcheance(_loc.dateFinBailISO, out var fb))
            return "Bail : dates de début et de fin à renseigner (section Bail).";
        return $"Bail : du {db:dd/MM/yyyy} au {fb:dd/MM/yyyy} ({Loyers.DureeTexte(db, fb)})";
    }

    string FranchiseTexte()
    {
        if (!FacturationSuivi.TryEcheance(_loc.debutFacturationISO, out var ff))
            return "Pas de franchise.";
        string duree = FacturationSuivi.TryEcheance(_loc.dateDebutBailISO, out var db) && ff > db
            ? $"{Loyers.DureeTexte(db, ff.AddDays(-1))} — " : "";
        return $"Franchise : {duree}facturation à partir du {ff:dd/MM/yyyy} — comprise dans le premier palier, sans loyer.";
    }

    string Duree(PalierLoyer p) => Loyers.DureeLibelle(_loc, D(p.debutISO), D(p.finISO));

    static string Fmt(string iso) => FacturationSuivi.TryEcheance(iso, out var d) ? d.ToString("dd/MM/yyyy") : "—";
    static DateTime D(string iso) => FacturationSuivi.TryEcheance(iso, out var d) ? d.Date : DateTime.MinValue;

    // Sprite déjà chargé en mémoire, cherché par nom (pas d'AssetDatabase en Play).
    static Sprite SpriteNomme(string nom)
    {
        foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            if (s != null && s.name == nom) return s;
        return null;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
