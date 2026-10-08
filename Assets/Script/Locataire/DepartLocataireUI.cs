using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Parcours de départ du locataire (maquettes validées le 02/10) : bouton de l'en-tête
/// (« Départ du locataire » → « Départ en cours » → « Archivé · Réactiver ») et bandeau
/// de six étapes en tête de fiche, du départ saisi à l'archivage. Les règles (état de
/// chaque étape, consigne) sont dans DepartLocataire ; ici, seulement l'écran.
public class ParcoursDepartUI : MonoBehaviour
{
    static readonly Color Ambre = Hex("#BA7517");

    LocatairePrefab _fiche;
    GameObject _bandeau;
    TMP_Text _titre, _consigne, _btnEnteteTexte, _continuerTexte;
    readonly Image[] _pastilles = new Image[6];
    readonly TMP_Text[] _libelles = new TMP_Text[6];
    Button _continuer, _btnEntete;

    public void EnsureBuilt(LocatairePrefab fiche)
    {
        _fiche = fiche;
        BoutonEntete();   // recâblé à chaque initialisation, comme « Transférer »
        if (_bandeau != null) return;

        var fond = UIFactory.Panel("ParcoursDepart", fiche.transform, Hex("#F1EFE8"));
        UIFactory.Border(fond.gameObject, Hex("#B4B2A9"));
        var header = fiche.transform.Find("HeaderLocataire");
        fond.transform.SetSiblingIndex(header != null ? header.GetSiblingIndex() + 1 : 0);
        var v = fond.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(16, 16, 10, 12); v.spacing = 8;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        _bandeau = fond.gameObject;

        _titre = UIFactory.Text(fond.transform, "", UITheme.Role.Libelle, Hex("#444441"), true);

        var etapes = UIFactory.HBox(fond.transform, 8, true, "Etapes");
        for (int i = 0; i < 6; i++)
        {
            int n = i;
            var b = UIFactory.Button(etapes.transform, DepartLocataire.Titres[i], UITheme.Carte, UITheme.TexteSecondaire, 34, UITheme.Role.Action);
            UIFactory.LE(b.gameObject, minH: 34, prefH: 34, flexW: 1, minW: 0);
            UIFactory.Border(b.gameObject);
            b.onClick.AddListener(() => Ouvrir((DepartLocataire.Etape)n));   // une étape s'ouvre aussi hors de l'ordre
            _pastilles[i] = b.GetComponent<Image>();
            _libelles[i] = b.GetComponentInChildren<TMP_Text>(true);
            _libelles[i].enableWordWrapping = true;
        }

        var bas = UIFactory.HBox(fond.transform, 12, false, "Consigne");
        _consigne = UIFactory.Text(bas.transform, "", UITheme.Role.Donnee, Hex("#444441"));
        _consigne.enableWordWrapping = true;
        UIFactory.LE(_consigne.gameObject, flexW: 1);
        _continuer = UIFactory.Button(bas.transform, "Continuer", Ambre, Color.white, 36, UITheme.Role.Action);
        UIFactory.LargeurDuTexte(_continuer);
        _continuerTexte = _continuer.GetComponentInChildren<TMP_Text>(true);
        _continuer.onClick.AddListener(() =>
        {
            var p = DepartLocataire.Prochaine(_fiche.GetLocataire(), DateTime.Today);
            if (p != null) Ouvrir(p.Value);
        });

        _bandeau.SetActive(false);
    }

    // Bouton de l'en-tête, cloné de « Supprimer » comme « Transférer » (même allure).
    void BoutonEntete()
    {
        var supprimer = _fiche.Delete;
        if (supprimer == null) return;
        var existant = supprimer.transform.parent.Find("Depart");
        _btnEntete = existant != null ? existant.GetComponent<Button>() : Instantiate(supprimer, supprimer.transform.parent);
        _btnEntete.name = "Depart";
        _btnEntete.onClick = new Button.ButtonClickedEvent();
        _btnEntete.onClick.AddListener(SurBoutonEntete);
        var transferer = supprimer.transform.parent.Find("Transferer");
        _btnEntete.transform.SetSiblingIndex((transferer != null ? transferer : supprimer.transform).GetSiblingIndex());
        _btnEnteteTexte = _btnEntete.GetComponentInChildren<TMP_Text>(true);
        if (existant == null) { UIFactory.Border(_btnEntete.gameObject); UIFactory.LargeurDuTexte(_btnEntete); }
    }

    void SurBoutonEntete()
    {
        var loc = _fiche.GetLocataire();
        if (loc == null) return;
        if (!loc.archive) { DepartPanel.Ouvrir(_fiche); return; }
        ConfirmDialog.Instance?.Show("Réactiver le locataire",
            $"« {loc.Name} » revient dans la liste du bâtiment et dans les alertes. Son départ reste saisi.",
            () => { _fiche.batimentPrefabOrigin.Archiver(loc, false); _fiche.ApresDepart(); }, "Réactiver");
    }

    /// Ouvre l'écran d'une étape.
    void Ouvrir(DepartLocataire.Etape e)
    {
        var loc = _fiche.GetLocataire();
        if (loc == null) return;
        switch (e)
        {
            case DepartLocataire.Etape.Depart:
            case DepartLocataire.Etape.EtatDesLieux:
                DepartPanel.Ouvrir(_fiche);
                break;
            case DepartLocataire.Etape.DernierLoyer:
                FactureLoyerPanel.OpenLoyer(_fiche, DepartLocataire.LigneDernierLoyer(loc));
                break;
            case DepartLocataire.Etape.Decompte:
                FactureDepotPanel.OpenDepot(_fiche);   // départ saisi → mode « décompte de sortie »
                break;
            case DepartLocataire.Etape.Regul:
                FactureRegulPanel.OpenRegul(_fiche, DepartLocataire.LigneRegulSortie(loc));
                break;
            case DepartLocataire.Etape.Archiver:
                if (!DepartLocataire.PretAArchiver(loc))
                { ConfirmDialog.Erreur("Pas encore : " + DepartLocataire.Consigne(loc, DateTime.Today)); return; }
                _fiche.batimentPrefabOrigin.Archiver(loc, true);
                _fiche.ApresDepart();
                break;
        }
    }

    public void Refresh(Locataire loc, bool enModification)
    {
        if (_bandeau == null || loc == null) return;
        bool depart = DepartLocataire.Sortie(loc, out var s);

        if (_btnEntete != null)
        {
            _btnEnteteTexte.text = loc.archive ? "Archivé · Réactiver" : depart ? "Départ en cours" : "Départ du locataire";
            _btnEnteteTexte.color = UITheme.TextePrincipal;
            if (_btnEntete.TryGetComponent<Image>(out var img)) img.color = loc.archive ? UITheme.Bordure : UITheme.Carte;
            // Ni pendant la création (parcours inachevé), ni pendant la saisie de la fiche.
            _btnEntete.gameObject.SetActive(!enModification && ParcoursLocataire.Prochaine(loc) == ParcoursLocataire.Etape.Termine);
        }

        bool visible = depart && !loc.archive;
        _bandeau.SetActive(visible);
        if (!visible) return;

        _titre.text = $"Départ de {loc.Name} · dernier jour le {s:dd/MM/yyyy}";
        var today = DateTime.Today;
        var prochaine = DepartLocataire.Prochaine(loc, today);
        for (int i = 0; i < 6; i++)
        {
            var e = (DepartLocataire.Etape)i;
            var st = DepartLocataire.StatutDe(loc, e, today, out var quand);
            string titre = $"{i + 1}  {DepartLocataire.Titres[i]}";
            if (st == DepartLocataire.Statut.Faite)
            {
                _pastilles[i].color = UITheme.PrimaireClair; _libelles[i].color = UITheme.Primaire;
                _libelles[i].text = DepartLocataire.Titres[i] + " : fait";   // pas de « ✓ » : absent de la police
            }
            else if (prochaine == e)
            {
                _pastilles[i].color = Ambre; _libelles[i].color = Color.white; _libelles[i].text = titre;
            }
            else
            {
                _pastilles[i].color = UITheme.Carte;
                _libelles[i].color = st == DepartLocataire.Statut.EnAttente ? UITheme.TexteSecondaire : UITheme.TextePrincipal;
                _libelles[i].text = quand != null ? $"{titre} · {quand:dd/MM/yyyy}" : titre;
            }
        }

        _consigne.text = DepartLocataire.Consigne(loc, today);
        _continuer.gameObject.SetActive(prochaine != null && !enModification);
        _continuerTexte.text = prochaine == DepartLocataire.Etape.Archiver ? "Archiver" : "Continuer";
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}

/// Fenêtre « Départ du locataire ». D'abord ce qui se passe (06/10, « c'est la même
/// origine, un locataire part ») : il part et le lot reste libre, un nouveau locataire
/// reprend le lot, ou cession du bail. Départ : dernier jour, délai de restitution, état
/// des lieux (oui : date + PDF ; non), « Annuler le départ ». Cession : le nouveau
/// titulaire. Seul endroit où l'on saisit l'un ou l'autre (la section Bail les affiche).
public static class DepartPanel
{
    static readonly Color Ambre = Hex("#BA7517");

    public static void Ouvrir(LocatairePrefab fiche)
    {
        var loc = fiche != null ? fiche.GetLocataire() : null;
        var canvas = fiche != null ? fiche.GetComponentInParent<Canvas>() : null;
        if (loc == null || canvas == null) return;

        var scrim = UIFactory.Rect("DepartScrim", canvas.rootCanvas.transform);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        scrim.SetAsLastSibling();
        Action fermer = () => UnityEngine.Object.Destroy(scrim.gameObject);

        var cardImg = UIFactory.Panel("DepartCard", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(620, 100);
        var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        cardImg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titre = Entete(v.transform, $"Départ du locataire — {loc.Name}", Ambre);

        var body = UIFactory.VBox(v.transform, 10, 18, 18, 14, 16, "Body");
        bool depart = DepartLocataire.Sortie(loc, out var s);

        // Type de départ (menu déroulant, retour du 06/10). La cession n'a plus de sens
        // une fois un départ saisi.
        UIFactory.Text(body.transform, "Type de départ", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var types = new List<string> { "Le locataire part, le lot reste libre",
            "Il part, un nouveau locataire reprend le lot (bail au lendemain du départ)" };
        if (!depart) types.Add("Cession du bail : un nouveau titulaire reprend le bail tel quel");
        Action majCas = null;
        bool cessionPrevue = !depart && Cessions.Prevue(loc) != null;   // rouvrir = la retrouver
        var type = UIDropdown.Create(body.transform, types, null, cessionPrevue ? 2 : 0, _ => majCas?.Invoke());
        Func<bool> remplacant = () => type.Index == 1, cession = () => type.Index == 2;

        var blocDepart = UIFactory.VBox(body.transform, 10, name: "BlocDepart").transform;

        // Dernier jour · délai de restitution, puis la date limite calculée.
        var ligne1 = UIFactory.HBox(blocDepart, 16, false, "Ligne1");
        var jour = ChampDate(fiche, ligne1.transform, "Dernier jour de location", depart ? s : (DateTime?)null);
        var delai = Champ(ligne1.transform, "Restitution du dépôt (mois)", DepartLocataire.DelaiRestitutionDefautMois.ToString(), 1f);
        delai.contentType = TMP_InputField.ContentType.IntegerNumber;
        delai.text = DepartLocataire.DelaiMois(loc).ToString();
        var limite = UIFactory.Text(blocDepart, "", UITheme.Role.Aide, UITheme.TexteSecondaire);
        Action majLimite = () =>
        {
            int.TryParse(delai.text, out int m);
            limite.text = jour.LireDate(out var j) && m > 0
                ? $"Dépôt à restituer avant le {j.AddMonths(m):dd/MM/yyyy}." : "";
        };
        foreach (var champ in new[] { jour.dayInput, jour.monthInput, jour.yearInput })
            champ.onValueChanged.AddListener(_ => majLimite());
        delai.onValueChanged.AddListener(_ => majLimite());
        majLimite();

        // État des lieux : oui (date + PDF) / non.
        UIFactory.Text(blocDepart, "État des lieux de sortie", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var choix = UIFactory.HBox(blocDepart, 24, false, "ChoixEdl");
        var groupe = choix.gameObject.AddComponent<ToggleGroup>();
        var oui = UIFactory.Toggle(choix.transform, "Oui", !loc.sansEtatDesLieux);
        var non = UIFactory.Toggle(choix.transform, "Non, pas d'état des lieux", loc.sansEtatDesLieux);
        oui.group = groupe; non.group = groupe; groupe.allowSwitchOff = false;

        var blocEdl = UIFactory.HBox(blocDepart, 16, false, "Edl");
        var dateEdl = ChampDate(fiche, blocEdl.transform, "Date de l'état des lieux",
            FacturationSuivi.TryEcheance(loc.dateEtatDesLieuxISO, out var e) ? e : (DateTime?)null);
        var colPdf = UIFactory.VBox(blocEdl.transform, 4, name: "PdfEdl");
        UIFactory.LE(colPdf.gameObject, flexW: 1.4f);
        UIFactory.Text(colPdf.transform, "Document (PDF)", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var lignePdf = UIFactory.HBox(colPdf.transform, 8, false, "LignePdf");
        var nomPdf = UIFactory.Text(lignePdf.transform, "", UITheme.Role.Donnee, UITheme.TextePrincipal);
        UIFactory.LE(nomPdf.gameObject, flexW: 1);
        Action majPdf = () => nomPdf.text = string.IsNullOrEmpty(loc.etatDesLieux) ? "Aucun document" : loc.etatDesLieux;
        majPdf();
        var joindre = UIFactory.Button(lignePdf.transform, "Joindre…", UITheme.Carte, UITheme.TextePrincipal, 36, UITheme.Role.Action, false);
        UIFactory.Border(joindre.gameObject);
        UIFactory.LargeurDuTexte(joindre);
        joindre.onClick.AddListener(() => { if (fiche.bailFile != null && fiche.bailFile.JoindreEtatDesLieux()) majPdf(); });
        oui.onValueChanged.AddListener(on => blocEdl.gameObject.SetActive(on));
        blocEdl.gameObject.SetActive(oui.isOn);

        UIFactory.Text(blocDepart,
            "Le dernier loyer est facturé au prorata des jours. La régularisation de sortie se fera à sa date habituelle, "
            + "charges au prorata de présence.", UITheme.Role.Aide, UITheme.TexteSecondaire);

        GameObject blocCession = null;
        Func<string> enregistrerCession = depart ? null : CessionPanel.Bloc(fiche, loc, body.transform, fermer, out blocCession);

        // Pied : Annuler le départ · Fermer · Enregistrer.
        var pied = UIFactory.HBox(v.transform, 8, false, "Pied");
        pied.padding = new RectOffset(18, 18, 10, 14);
        if (depart)
        {
            var annuler = UIFactory.Button(pied.transform, "Annuler le départ", UITheme.Carte, UITheme.Alerte, 40, UITheme.Role.Bouton, false);
            UIFactory.Border(annuler.gameObject, UITheme.Alerte);
            UIFactory.LargeurDuTexte(annuler);
            annuler.onClick.AddListener(() => Annuler(fiche, loc, fermer));
        }
        var espace = UIFactory.Rect("Espace", pied.transform);
        UIFactory.LE(espace.gameObject, flexW: 1);
        var btnFermer = UIFactory.Button(pied.transform, "Fermer", UITheme.Carte, UITheme.TextePrincipal, 40, UITheme.Role.Bouton, false);
        UIFactory.Border(btnFermer.gameObject);
        UIFactory.LargeurDuTexte(btnFermer);
        btnFermer.onClick.AddListener(() => fermer());
        var enregistrer = UIFactory.Button(pied.transform, "Enregistrer", Ambre, Color.white, 40, UITheme.Role.Bouton);
        UIFactory.LargeurDuTexte(enregistrer);
        var enregistrerTexte = enregistrer.GetComponentInChildren<TMP_Text>(true);

        // Cession choisie : ses champs à la place de ceux du départ.
        majCas = () =>
        {
            bool c = cession();
            blocDepart.gameObject.SetActive(!c);
            if (blocCession != null) blocCession.SetActive(c);
            titre.text = (c ? "Cession du bail — " : "Départ du locataire — ") + loc.Name;
            enregistrerTexte.text = c ? "Enregistrer la cession" : "Enregistrer";
        };
        majCas();

        enregistrer.onClick.AddListener(() =>
        {
            if (cession())
            {
                string refus = enregistrerCession();
                if (refus != null) { ConfirmDialog.Erreur(refus); return; }
                fermer();
                fiche.ApresDepart();
                var p = Cessions.Prevue(loc);
                UndoToast.Instance?.ShowInfo(p != null && FacturationSuivi.TryEcheance(p.dateISO, out var dp)
                    ? $"Cession enregistrée : « {p.nom} » reprendra le bail le {dp:dd/MM/yyyy}. D'ici là, la fiche reste au nom de « {loc.Name} »."
                    : $"Bail cédé : « {loc.Name} » est le nouveau locataire.");
                return;
            }
            string erreur = Enregistrer(loc, jour, delai.text, oui.isOn, dateEdl);
            if (erreur != null) { ConfirmDialog.Erreur(erreur); return; }
            fermer();
            fiche.ApresDepart();
            if (remplacant()) CreerRemplacant(fiche, loc);
            else UndoToast.Instance?.ShowInfo($"Départ de « {loc.Name} » enregistré.");
        });
    }

    // Nouvelle fiche sur le même lot, pré-remplie, en création (parcours habituel :
    // nom, fin du bail, puis loyer et dépôt).
    static void CreerRemplacant(LocatairePrefab fiche, Locataire ancien)
    {
        var bp = fiche.batimentPrefabOrigin;
        var prefab = bp.Addlocataire(true);
        var nouveau = prefab.GetLocataire();
        DepartLocataire.PreparerRemplacant(ancien, nouveau, bp.getBatiment());
        bp.menulocataire.CreateTab(prefab);
        bp.menulocataire.OnSelect(prefab, true);
        prefab.InitializeLocataire(nouveau, true);   // affiche le lot et le début du bail repris
        UndoToast.Instance?.ShowInfo($"Départ de « {ancien.Name} » enregistré. Nouveau locataire du lot {nouveau.lotBatiment} : "
            + "saisissez son nom et la fin de son bail, puis « Sauvegarder ».");
    }

    // Bandeau plein en tête, coins arrondis en haut, comme les modales Loyer, Dépôt et
    // Paliers (retour du 06/10 : l'ancien bandeau était une pastille et le titre débordait).
    internal static TMP_Text Entete(Transform carte, string texte, Color fond)
    {
        var header = UIFactory.Panel("Header", carte, fond);
        var haut = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sp => sp != null && sp.name == "RoundedTop");
        if (haut != null) header.sprite = haut;
        UIFactory.LE(header.gameObject, minH: 52, prefH: 52, flexH: 0);
        var hh = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        hh.padding = new RectOffset(16, 16, 4, 4);
        hh.childControlWidth = true; hh.childControlHeight = true;
        hh.childForceExpandWidth = false; hh.childForceExpandHeight = true;
        hh.childAlignment = TextAnchor.MiddleLeft;
        var t = UIFactory.Text(header.transform, texte, UITheme.Role.Section, Color.white, true);
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;   // nom long : « … » plutôt qu'un débordement
        UIFactory.LE(t.gameObject, flexW: 1, minW: 0);
        return t;
    }

    // Libellé + champ de saisie, en colonne (largeur relative `parts`).
    internal static TMP_InputField Champ(Transform parent, string libelle, string placeholder, float parts)
    {
        var col = UIFactory.VBox(parent, 4, name: libelle);
        UIFactory.LE(col.gameObject, flexW: parts);
        UIFactory.Text(col.transform, libelle, UITheme.Role.Donnee, UITheme.TexteSecondaire);
        return UIFactory.Input(col.transform, placeholder);
    }

    // Champ date cloné de la fin du bail de la fiche : même rendu et même saisie
    // JJ / MM / AAAA que partout ailleurs (retour du 05/10), en saisie dès l'ouverture.
    internal static DateInputController ChampDate(LocatairePrefab fiche, Transform parent, string titre, DateTime? valeur)
    {
        var bloc = UnityEngine.Object.Instantiate(fiche.dateFinBail.gameObject, parent);
        bloc.name = titre;
        bloc.SetActive(true);
        var d = bloc.GetComponent<DateInputController>();
        d.OnModify.RemoveAllListeners();
        var t = bloc.transform.Find("Titre")?.GetComponent<TMP_Text>();
        if (t != null) t.text = titre;
        var le = bloc.GetComponent<LayoutElement>() ?? bloc.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        if (valeur.HasValue) d.ApplyDate(valeur.Value);
        else { d.dayInput.text = ""; d.monthInput.text = ""; d.yearInput.text = ""; }
        d.ModifyDate();
        return d;
    }

    static bool Vide(DateInputController d)
        => string.IsNullOrWhiteSpace(d.dayInput.text) && string.IsNullOrWhiteSpace(d.monthInput.text)
           && string.IsNullOrWhiteSpace(d.yearInput.text);

    /// Vérifie puis écrit la saisie. Message d'erreur, ou null si tout est enregistré.
    static string Enregistrer(Locataire loc, DateInputController jour, string delai, bool avecEdl, DateInputController dateEdl)
    {
        if (!jour.LireDate(out var dernier))
            return "Dernier jour de location : saisissez une date (JJ/MM/AAAA).";
        if (FacturationSuivi.TryEcheance(loc.dateDebutBailISO, out var debut) && dernier.Date < debut.Date)
            return $"Le départ ne peut pas précéder le début du bail ({debut:dd/MM/yyyy}).";
        var cession = Cessions.Prevue(loc);
        if (cession != null && FacturationSuivi.TryEcheance(cession.dateISO, out var dc) && dc.Date > dernier.Date)
            return $"Une cession du bail est prévue le {dc:dd/MM/yyyy} à « {cession.nom} » : annulez-la d'abord (type « Cession du bail »).";
        delai = (delai ?? "").Trim();
        int mois = 0;
        if (delai != "" && (!int.TryParse(delai, out mois) || mois < 1 || mois > 24))
            return "Restitution du dépôt : un nombre de mois entre 1 et 24.";
        DateTime edl = default;
        bool edlSaisi = avecEdl && !Vide(dateEdl);
        if (edlSaisi && !dateEdl.LireDate(out edl))
            return "Date de l'état des lieux : date incomplète (JJ/MM/AAAA), ou laissez-la vide.";

        loc.dateSortieISO = dernier.ToString("yyyy-MM-dd");
        loc.delaiRestitutionMois = mois == DepartLocataire.DelaiRestitutionDefautMois ? 0 : mois;
        loc.sansEtatDesLieux = !avecEdl;
        loc.dateEtatDesLieuxISO = edlSaisi ? edl.ToString("yyyy-MM-dd") : "";
        return null;
    }

    static void Annuler(LocatairePrefab fiche, Locataire loc, Action fermer)
    {
        string refus = DepartLocataire.PeutAnnuler(loc);
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }
        ConfirmDialog.Instance?.Show("Annuler le départ",
            $"« {loc.Name} » reste dans les lieux : le dernier jour, l'état des lieux et le délai sont effacés. "
            + "Les factures déjà émises restent dans le suivi.",
            () =>
            {
                loc.dateSortieISO = ""; loc.dateEtatDesLieuxISO = ""; loc.etatDesLieux = "";
                loc.delaiRestitutionMois = 0; loc.sansEtatDesLieux = false; loc.archive = false;
                fermer();
                fiche.ApresDepart();
            }, "Annuler le départ");
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}

/// Cession du bail (décision du 06/10), choisie dans la fenêtre « Départ du locataire » :
/// date + coordonnées du cessionnaire. La fiche continue (bail, loyer, dépôt, suivi) ;
/// l'ancien titulaire va dans l'historique.
public static class CessionPanel
{
    /// Champs de la cession dans `parent`. Rend l'enregistrement (message d'erreur, ou null).
    internal static Func<string> Bloc(LocatairePrefab fiche, Locataire loc, Transform parent, Action fermer, out GameObject bloc)
    {
        var v = UIFactory.VBox(parent, 10, name: "BlocCession");
        bloc = v.gameObject;
        UIFactory.Text(v.transform,
            "Le nouveau titulaire reprend le bail tel quel : loyer, dépôt de garantie et suivi continuent. "
            + "Cédant et cessionnaire s'arrangent entre eux (dépôt, factures). Les factures déjà émises gardent l'ancien nom. "
            + "Une date à venir : la fiche change de titulaire ce jour-là.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        // Cession déjà prévue : ses valeurs, pour la modifier ou l'annuler.
        var prevue = Cessions.Prevue(loc);
        var ligne = UIFactory.HBox(v.transform, 16, false, "Ligne");
        var date = DepartPanel.ChampDate(fiche, ligne.transform, "Date de la cession",
            prevue != null && FacturationSuivi.TryEcheance(prevue.dateISO, out var dp) ? dp : (DateTime?)null);
        var nom = DepartPanel.Champ(ligne.transform, "Nouveau locataire (nom)", "ex. SARL Dupont", 1.4f);
        var ligne2 = UIFactory.HBox(v.transform, 16, false, "Ligne2");
        var siret = DepartPanel.Champ(ligne2.transform, "SIRET", "", 1f);
        var email = DepartPanel.Champ(ligne2.transform, "Email", "", 1.4f);
        UIFactory.Text(v.transform, "Adresse de facturation", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var adresse = UIFactory.Input(v.transform, "Adresse (plusieurs lignes possibles)", 70, true);
        if (prevue != null)
        {
            nom.text = prevue.nom; siret.text = prevue.siret; email.text = prevue.email; adresse.text = prevue.adresse;
            var annuler = UIFactory.Button(v.transform, "Annuler la cession prévue", UITheme.Carte, UITheme.Alerte, 40, UITheme.Role.Bouton, false);
            UIFactory.Border(annuler.gameObject, UITheme.Alerte);
            UIFactory.LargeurDuTexte(annuler);
            annuler.onClick.AddListener(() =>
            {
                loc.cessionPrevue = null;
                fermer();
                fiche.ApresDepart();
                UndoToast.Instance?.ShowInfo($"Cession annulée : « {loc.Name} » reste le locataire.");
            });
        }
        return () => Enregistrer(fiche, loc, date, nom.text, siret.text, adresse.text, email.text);
    }

    static string Enregistrer(LocatairePrefab fiche, Locataire loc, DateInputController date,
                              string nom, string siret, string adresse, string email)
    {
        if (!date.LireDate(out var d)) return "Date de la cession : saisissez une date (JJ/MM/AAAA).";
        string refus = Cessions.Verifier(loc, d, nom);
        if (refus != null) return refus;

        // Même règle que « Modifier » : nom unique dans le bâtiment, dossier renommé.
        var bp = fiche.batimentPrefabOrigin;
        string ancien = loc.Name, nouveau = nom.Trim();
        if (!DossiersDonnees.MemeDossier(ancien, nouveau)
            && bp.listLocataire.Exists(l => l != null && l.id != loc.id && DossiersDonnees.MemeDossier(l.Name, nouveau)))
            return $"Un locataire nommé « {nouveau} » existe déjà dans ce bâtiment.";
        // Date à venir : rien ne change avant le jour de la cession (retour du 06/10) ;
        // le dossier sera renommé ce jour-là (BatimentManager, au lancement).
        if (d.Date > DateTime.Today)
        {
            Cessions.Enregistrer(loc, d, nouveau, siret, adresse, email, DateTime.Today);
            return null;
        }
        if (!DossiersDonnees.RenommerLocataire(bp.getName(), ancien, nouveau, out string err))
            return $"Renommage du dossier impossible ({err}). Fermez les fichiers ouverts de ce locataire et réessayez.";
        Cessions.Enregistrer(loc, d, nouveau, siret, adresse, email, DateTime.Today);
        bp.menulocataire.UpdateTabLabel(fiche, loc.Name);
        return null;
    }
}
