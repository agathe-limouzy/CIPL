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
    static readonly Color Ambre = UITheme.Hex("#854F0B");   // Revue 17 (D10-10, lot F) : texte blanc lisible (6,7:1 ; 3,7:1 sur #BA7517)

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

        var fond = UIFactory.Panel("ParcoursDepart", fiche.transform, UITheme.Fond);
        UIFactory.Border(fond.gameObject, UITheme.Hex("#B4B2A9"));
        var header = fiche.transform.Find("HeaderLocataire");
        fond.transform.SetSiblingIndex(header != null ? header.GetSiblingIndex() + 1 : 0);
        var v = fond.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(16, 16, 10, 12); v.spacing = 8;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        _bandeau = fond.gameObject;

        _titre = UIFactory.Text(fond.transform, "", UITheme.Role.Libelle, UITheme.Hex("#444441"), true);

        var etapes = UIFactory.HBox(fond.transform, 8, true, "Etapes");
        // Revue 17 (D10-20, lot F ; décision du 16/11) : un libellé sur deux ou trois lignes (150-175 %) agrandit son bouton, et
        // les six prennent la hauteur du plus haut. Avant : hauteur forcée à 34, le texte sortait du bouton (51 pour 34 à 175 %).
        etapes.childForceExpandHeight = true;
        _etapesHauteur = UIFactory.LE(etapes.gameObject, minH: 34, prefH: 34);   // posée par LateUpdate
        for (int i = 0; i < 6; i++)
        {
            int n = i;
            var b = UIFactory.Button(etapes.transform, DepartLocataire.Titres[i], UITheme.Carte, UITheme.TexteSecondaire, 34, UITheme.Role.Action);
            UIFactory.LE(b.gameObject, minH: 34, flexW: 1, minW: 0).preferredHeight = -1;   // la hauteur vient de la rangée
            UIFactory.Border(b.gameObject);
            b.onClick.AddListener(() => Ouvrir((DepartLocataire.Etape)n));   // une étape s'ouvre aussi hors de l'ordre
            _pastilles[i] = b.GetComponent<Image>();
            _libelles[i] = b.GetComponentInChildren<TMP_Text>(true);
            _libelles[i].enableWordWrapping = true;
        }

        var bas = UIFactory.HBox(fond.transform, 12, false, "Consigne");
        _consigne = UIFactory.Text(bas.transform, "", UITheme.Role.Donnee, UITheme.Hex("#444441"));
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
        UIFactory.PlacerAvant(_btnEntete.transform, transferer != null ? transferer : supprimer.transform);   // revue visuelle du 17/11
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
            {
                // Revue 17 (2c, relecture du 07/11) : un loyer d'une période après le départ se retire du suivi (le formulaire refuse
                // 0 €) : l'étape montre le suivi au lieu d'ouvrir le formulaire.
                var ligne = DepartLocataire.LigneDernierLoyer(loc);
                if (ligne != null && DepartLocataire.PeriodeApresDepart(loc, ligne) && DepartLocataire.LirePeriode(ligne.key, out int annee, out _))
                    LocataireSuiviInline.MontrerPour(_fiche, annee);
                else FactureLoyerPanel.OpenLoyer(_fiche, ligne);
                break;
            }
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

    // Revue 17 (D10-20, lot F) : la hauteur de la rangée des étapes est celle du libellé le plus haut (34 au moins), mesurée ici
    // quand la largeur des boutons ou un libellé change. La mise en page seule gardait une hauteur calculée avant de connaître la
    // largeur des boutons (48 au lieu de 34 à 100 %, mesuré le 16/11).
    LayoutElement _etapesHauteur;
    float _largeurMesuree = -1f;
    int _versionTextes, _versionMesuree = -1;

    void LateUpdate()
    {
        if (_etapesHauteur == null || _bandeau == null || !_bandeau.activeInHierarchy || _libelles[0] == null) return;
        float largeur = _libelles[0].rectTransform.rect.width;
        if (largeur <= 1f || (Mathf.Abs(largeur - _largeurMesuree) < 0.5f && _versionMesuree == _versionTextes)) return;
        _largeurMesuree = largeur; _versionMesuree = _versionTextes;
        float h = 34f;
        foreach (var t in _libelles) h = Mathf.Max(h, t.GetPreferredValues(t.text, largeur, 0f).y + 8f);
        _etapesHauteur.minHeight = _etapesHauteur.preferredHeight = h;
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
        _versionTextes++;
    }

}

/// Fenêtre « Départ du locataire ». D'abord ce qui se passe (06/10, « c'est la même
/// origine, un locataire part ») : il part et le lot reste libre, un nouveau locataire
/// reprend le lot, ou cession du bail. Départ : dernier jour, délai de restitution, état
/// des lieux (oui : date + PDF ; non), « Annuler le départ ». Cession : le nouveau
/// titulaire. Seul endroit où l'on saisit l'un ou l'autre (la section Bail les affiche).
public static class DepartPanel
{
    static readonly Color Ambre = UITheme.Hex("#854F0B");   // Revue 17 (D10-10, lot F) : texte blanc lisible (6,7:1 ; 3,7:1 sur #BA7517)

    public static void Ouvrir(LocatairePrefab fiche)
    {
        if (fiche == null) return;
        RectTransform scrim = null;   // sur le cloud : locataire réservé tant que la fenêtre est ouverte (étape 4b)
        // Revue 17 (D7a-17, lot D) : le bâtiment aussi : « un nouveau locataire reprend le lot » écrit ses charges (parts du remplaçant).
        EcritureCloud.Reserver(EcritureCloud.Cibles(fiche, true), "départ", () => scrim = Construire(fiche),
            // Revue 17 (D7a-17, lot D) : fenêtre fermée, le gardien d'EcritureCloud garde le verrou tant que l'écriture du
            // départ attend ou part : le bâtiment n'est pas libéré avant le serveur.
            () => scrim != null,
            () => { if (scrim != null) UnityEngine.Object.Destroy(scrim.gameObject); });
    }

    static RectTransform Construire(LocatairePrefab fiche)
    {
        var loc = fiche != null ? fiche.GetLocataire() : null;
        var canvas = fiche != null ? fiche.GetComponentInParent<Canvas>() : null;
        if (loc == null || canvas == null) return null;

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
        PlafondFenetre.Poser(card);   // Revue 17 (D10-01, lot F) : jamais plus haute que l'écran

        var titre = Entete(v.transform, $"Départ du locataire — {loc.Name}", Ambre);

        var body = UIFactory.VBox(v.transform, 10, 18, 18, 14, 16, "Body");
        bool depart = DepartLocataire.Sortie(loc, out var s);

        // Type de départ (menu déroulant, retour du 06/10). Revue 17 (D7a-22, D7a-26, lot D) : les types viennent de TypesProposes.
        UIFactory.Text(body.transform, "Type de départ", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        var bat = fiche.batimentPrefabOrigin != null ? fiche.batimentPrefabOrigin.getBatiment() : null;
        var remplacantExistant = depart ? DepartLocataire.Remplacant(loc, bat, s) : null;
        bool cessionPrevue = Cessions.Prevue(loc) != null;   // rouvrir = la retrouver
        var types = TypesProposes(depart, cessionPrevue, remplacantExistant != null);
        Action majCas = null;
        // Retour de BW4 (12/11) : une cession prévue vient avant le départ (refusé avant elle) : la fenêtre s'ouvre dessus, même départ saisi.
        var type = UIDropdown.Create(body.transform, types, null, types.IndexOf(TypeOuvert(types, cessionPrevue)), _ => majCas?.Invoke());
        Func<bool> remplacant = () => types[type.Index] == TypeRemplacant, cession = () => types[type.Index] == TypeCession;

        var blocDepart = UIFactory.VBox(body.transform, 10, name: "BlocDepart").transform;
        if (remplacantExistant != null)   // Revue 17 (D7a-26, lot D) : dit pourquoi « reprend le lot » n'est plus proposé
            UIFactory.Text(blocDepart, $"« {remplacantExistant.Name} » reprend ce lot le {s.AddDays(1):dd/MM/yyyy}.",
                UITheme.Role.Aide, UITheme.TexteSecondaire);

        // Dernier jour · délai de restitution, puis la date limite calculée.
        var ligne1 = UIFactory.HBox(blocDepart, 16, false, "Ligne1");
        var jour = ChampDate.Colonne(ligne1.transform, "Dernier jour de location", depart ? s : (DateTime?)null);
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
        jour.onValueChanged.AddListener(_ => majLimite());
        delai.onValueChanged.AddListener(_ => majLimite());
        majLimite();

        // État des lieux : oui (date + PDF) / non.
        UIFactory.Text(blocDepart, "État des lieux de sortie", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        var choix = UIFactory.HBox(blocDepart, 6, false, "ChoixEdl");
        var groupe = choix.gameObject.AddComponent<ToggleGroup>();
        // Revue 17 (D10-18, lot F) : deux boutons-pastilles (avant : deux cases carrées dont une seule pouvait être cochée).
        var oui = UIFactory.TogglePastille(choix.transform, "Oui", !loc.sansEtatDesLieux);
        var non = UIFactory.TogglePastille(choix.transform, "Non, pas d'état des lieux", loc.sansEtatDesLieux);
        oui.group = groupe; non.group = groupe; groupe.allowSwitchOff = false;

        var blocEdl = UIFactory.HBox(blocDepart, 16, false, "Edl");
        var dateEdl = ChampDate.Colonne(blocEdl.transform, "Date de l'état des lieux",
            FacturationSuivi.TryEcheance(loc.dateEtatDesLieuxISO, out var e) ? e : (DateTime?)null);
        var colPdf = UIFactory.VBox(blocEdl.transform, 4, name: "PdfEdl");
        UIFactory.LE(colPdf.gameObject, flexW: 1.4f);
        UIFactory.Text(colPdf.transform, "Document (PDF)", UITheme.Role.Libelle, UITheme.TexteSecondaire);
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
        // Revue 17 (D7a-22, décision du 11/11, lot D) : une cession prévue garde son bloc (et « Annuler la cession prévue ») après un départ.
        Func<string> enregistrerCession = !types.Contains(TypeCession) ? null : CessionPanel.Bloc(fiche, loc, body.transform, fermer, out blocCession);
        if (blocCession != null && depart)   // Retour de BW4 (12/11) : la fenêtre s'ouvre sur la cession : le départ saisi est rappelé
            UIFactory.Text(blocCession.transform, $"Un départ est aussi saisi au {s:dd/MM/yyyy} : choisissez « {TypeLibre} » pour le modifier ou l'annuler.",
                UITheme.Role.Aide, UITheme.TexteSecondaire).transform.SetAsFirstSibling();

        // Pied : Annuler le départ · Fermer · Enregistrer.
        var pied = UIFactory.HBox(v.transform, 8, false, "Pied");
        pied.padding = new RectOffset(18, 18, 10, 14);
        Button annuler = null;   // Retour de BW4 (12/11) : montré seulement côté départ (il se lisait « annuler la cession »)
        if (depart)
        {
            annuler = UIFactory.Button(pied.transform, "Annuler le départ", UITheme.Carte, UITheme.Alerte, 40, UITheme.Role.Bouton, false);
            UIFactory.Border(annuler.gameObject, UITheme.Alerte);
            UIFactory.LargeurDuTexte(annuler);
            annuler.onClick.AddListener(() => Annuler(fiche, loc, bat, fermer));
        }
        var espace = UIFactory.Rect("Espace", pied.transform);
        UIFactory.LE(espace.gameObject, flexW: 1);
        // Revue visuelle du 16/11 (V-07 ; décision du 16/11) : « Annuler » à côté d'une action ; « Fermer » reste quand « Annuler le
        // départ » est déjà dans la rangée (deux « Annuler » côte à côte se liraient mal).
        var btnFermer = UIFactory.Button(pied.transform, depart ? "Fermer" : "Annuler", UITheme.Carte, UITheme.TextePrincipal, 40, UITheme.Role.Bouton, false);
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
            if (annuler != null) annuler.gameObject.SetActive(!c);
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
                // Revue 17 (D7a-12, lot D) : cession faite tout de suite : la fiche société du nouveau titulaire, comme « Sauvegarder ».
                if (p == null) fiche.StartCoroutine(PappersSync.Un(fiche.batimentPrefabOrigin, loc));
                UndoToast.Instance?.ShowInfo(p != null && FacturationSuivi.TryEcheance(p.dateISO, out var dp)
                    ? $"Cession enregistrée : « {p.nom} » reprendra le bail le {dp:dd/MM/yyyy}. D'ici là, la fiche reste au nom de « {loc.Name} »."
                    : $"Bail cédé : « {loc.Name} » est le nouveau locataire.");
                return;
            }
            string erreur = Enregistrer(loc, bat, jour, delai.text, oui.isOn, dateEdl);
            if (erreur != null) { ConfirmDialog.Erreur(erreur); return; }
            // Revue 17 (D7a-17, lot D) : le remplaçant est préparé AVANT l'écriture du départ : ses parts des charges du bâtiment
            // partent dans la même écriture, sous la réservation de la fenêtre (bâtiment + locataire). Avant, elles attendaient le
            // premier « Sauvegarder » de sa fiche, sans réservation : un refus emportait alors la fiche saisie.
            var nouveau = remplacant() ? AjouterRemplacant(fiche, loc) : null;
            fermer();
            fiche.ApresDepart();
            if (nouveau != null) OuvrirRemplacant(fiche, loc, nouveau);
            else UndoToast.Instance?.ShowInfo($"Départ de « {loc.Name} » enregistré.");
        });
        return scrim;
    }

    public const string TypeLibre = "Le locataire part, le lot reste libre";
    public const string TypeRemplacant = "Il part, un nouveau locataire reprend le lot (bail au lendemain du départ)";
    public const string TypeCession = "Cession du bail : un nouveau titulaire reprend le bail tel quel";

    /// Revue 17 (D7a-22, D7a-26, décisions du 09/11 et du 11/11, lot D) : les types proposés par la fenêtre. « Reprend le lot » :
    /// pas quand un remplaçant reprend déjà le lot (un second serait créé). « Cession du bail » : sans départ, ou tant qu'une
    /// cession est prévue (pour la modifier ou l'annuler, même après un départ saisi).
    /// Retour de BW4 (12/11) : le type affiché à l'ouverture : la cession tant qu'elle est prévue (elle vient avant le départ,
    /// on y décale sa date ou on l'annule), sinon le premier proposé.
    public static string TypeOuvert(List<string> types, bool cessionPrevue)
        => cessionPrevue && types.Contains(TypeCession) ? TypeCession : types[0];

    public static List<string> TypesProposes(bool depart, bool cessionPrevue, bool remplacant)
    {
        var t = new List<string> { TypeLibre };
        if (!remplacant) t.Add(TypeRemplacant);
        if (!depart || cessionPrevue) t.Add(TypeCession);
        return t;
    }

    // Nouvelle fiche sur le même lot, pré-remplie (lot, bail au lendemain, parts des charges), pas encore inscrite dans le
    // bâtiment (« Sauvegarder » l'y inscrit). Revue 17 (D7a-17, lot D) : appelée avant l'écriture du départ.
    static LocatairePrefab AjouterRemplacant(LocatairePrefab fiche, Locataire ancien)
    {
        var bp = fiche.batimentPrefabOrigin;
        var prefab = bp.Addlocataire(true);
        DepartLocataire.PreparerRemplacant(ancien, prefab.GetLocataire(), bp.getBatiment());
        return prefab;
    }

    // Ouvre la fiche du remplaçant, en création (parcours habituel : nom, fin du bail, puis loyer et dépôt).
    static void OuvrirRemplacant(LocatairePrefab fiche, Locataire ancien, LocatairePrefab prefab)
    {
        var bp = fiche.batimentPrefabOrigin;
        var nouveau = prefab.GetLocataire();
        bp.menulocataire.CreateTab(prefab);
        bp.menulocataire.OnSelect(prefab);
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
        UIFactory.Text(col.transform, libelle, UITheme.Role.Libelle, UITheme.TexteSecondaire);
        return UIFactory.Input(col.transform, placeholder);
    }

    /// Vérifie puis écrit la saisie. Message d'erreur, ou null si tout est enregistré.
    static string Enregistrer(Locataire loc, Batiment bat, ChampDate jour, string delai, bool avecEdl, ChampDate dateEdl)
    {
        if (!jour.LireDate(out var dernier))
            return "Dernier jour de location : saisissez une date (JJ/MM/AAAA).";
        if (FacturationSuivi.TryEcheance(loc.dateDebutBailISO, out var debut) && dernier.Date < debut.Date)
            return $"Le départ ne peut pas précéder le début du bail ({debut:dd/MM/yyyy}).";
        // Revue 17 (D7a-19, décision du 09/11, lot D) : un remplaçant reprend le lot au lendemain de l'ancienne date : refus qui le nomme.
        string chevauche = DepartLocataire.RefusRemplacant(loc, bat, dernier);
        if (chevauche != null) return chevauche;
        var cession = Cessions.Prevue(loc);
        if (cession != null && FacturationSuivi.TryEcheance(cession.dateISO, out var dc) && dc.Date > dernier.Date)
            return $"Une cession du bail est prévue le {dc:dd/MM/yyyy} à « {cession.nom} » : annulez-la d'abord (type « Cession du bail »).";
        delai = (delai ?? "").Trim();
        int mois = 0;
        if (delai != "" && (!int.TryParse(delai, out mois) || mois < 1 || mois > 24))
            return "Restitution du dépôt : un nombre de mois entre 1 et 24.";
        DateTime edl = default;
        bool edlSaisi = avecEdl && !dateEdl.Vide();
        if (edlSaisi && !dateEdl.LireDate(out edl))
            return "Date de l'état des lieux : date incomplète (JJ/MM/AAAA), ou laissez-la vide.";

        loc.dateSortieISO = dernier.ToString("yyyy-MM-dd");
        loc.delaiRestitutionMois = mois == DepartLocataire.DelaiRestitutionDefautMois ? 0 : mois;
        loc.sansEtatDesLieux = !avecEdl;
        if (!avecEdl) loc.etatDesLieux = "";   // revue 16c (D7a-14) : le PDF joint n'est plus affiché (le fichier reste dans le dossier)
        loc.dateEtatDesLieuxISO = edlSaisi ? edl.ToString("yyyy-MM-dd") : "";
        return null;
    }

    static void Annuler(LocatairePrefab fiche, Locataire loc, Batiment bat, Action fermer)
    {
        // Revue 17 (D7a-19, lot D) : annuler le départ ferait aussi chevaucher le remplaçant.
        string refus = DepartLocataire.PeutAnnuler(loc) ?? DepartLocataire.RefusRemplacant(loc, bat, null);
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }
        ConfirmDialog.Instance?.Show("Annuler le départ",
            $"« {loc.Name} » reste dans les lieux : le dernier jour, l'état des lieux et le délai sont effacés. "
            + "Les factures déjà émises restent dans le suivi ; un loyer parti puis retiré du suivi revient dans son état "
            + "(sauf s'il a été annulé par un avoir).",
            () =>
            {
                loc.dateSortieISO = ""; loc.dateEtatDesLieuxISO = ""; loc.etatDesLieux = "";
                loc.delaiRestitutionMois = 0; loc.sansEtatDesLieux = false; loc.archive = false;
                int remis = DepartLocataire.RemettreRetires(loc);   // décision du 15/11 (BS9) : avant, « À faire », à remettre à la main
                fermer();
                fiche.ApresDepart();
                if (remis > 0) UndoToast.Instance?.ShowInfo($"{remis} loyer(s) retiré(s) du suivi remis dans leur état (« Envoyé » ou « Payé »).");
            }, "Annuler le départ");
    }

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
        var date = ChampDate.Colonne(ligne.transform, "Date de la cession",
            prevue != null && FacturationSuivi.TryEcheance(prevue.dateISO, out var dp) ? dp : (DateTime?)null);
        var nom = DepartPanel.Champ(ligne.transform, "Nouveau locataire (nom)", "ex. SARL Dupont", 1.4f);
        var ligne2 = UIFactory.HBox(v.transform, 16, false, "Ligne2");
        var siret = DepartPanel.Champ(ligne2.transform, "SIRET", "", 1f);
        var email = DepartPanel.Champ(ligne2.transform, "E-mail", "", 1.4f);
        UIFactory.Text(v.transform, "Adresse de facturation", UITheme.Role.Libelle, UITheme.TexteSecondaire);
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

    static string Enregistrer(LocatairePrefab fiche, Locataire loc, ChampDate date,
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
