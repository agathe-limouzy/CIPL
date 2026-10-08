using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Tableau des objectifs d'une fiche (maquette validée le 06/10) : colonnes À faire · En
/// cours · Fait, une carte par objectif (liseré et badge = importance, étiquette d'échéance),
/// rappels automatiques en tête de « À faire ». Construit par code dans la section Objectifs.
public static class ObjectifsTableau
{
    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    static readonly Color FondColonne = Hex("#F1EEE6"), BordCarte = Hex("#E2DDD0");
    static readonly Color FondAuto = Hex("#F7F5EF"), Ambre = Hex("#BA7517");
    static readonly Color FondRetard = Hex("#FCEBEB"), TxRetard = Hex("#A32D2D");
    static readonly Color FondProche = Hex("#FAEEDA"), TxProche = Hex("#854F0B");
    static readonly Color Gris = Hex("#888780"), Bleu = Hex("#185FA5");

    /// Largeur sous laquelle une colonne ne descend pas (unités d'interface : suit le zoom).
    /// Une carte y tient sur deux lignes : « Obligatoire · Retard 15 j », puis
    /// « récurrent · 2 doc. · › » (vérifié par ObjectifsTableauTests).
    public const float LARGEUR_MIN_COLONNE = 190f;

    public static readonly Objective.Avancement[] Colonnes =
        { Objective.Avancement.AFaire, Objective.Avancement.EnCours, Objective.Avancement.Fait };

    public static string NomColonne(Objective.Avancement a) => a switch
    {
        Objective.Avancement.EnCours => "En cours",
        Objective.Avancement.Fait => "Fait",
        _ => "À faire"
    };

    static Color PointColonne(Objective.Avancement a) => a switch
    {
        Objective.Avancement.EnCours => Hex("#185FA5"),
        Objective.Avancement.Fait => Hex("#0F6E56"),
        _ => Hex("#5F5E5A")
    };

    /// Une colonne vide (fond, mise en page verticale). Le contenu est reconstruit à chaque rafraîchissement.
    public static RectTransform CreerColonne(Transform parent, Objective.Avancement a)
    {
        var img = UIFactory.Panel("Colonne " + NomColonne(a), parent, FondColonne);
        img.pixelsPerUnitMultiplier = 2f;
        var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(8, 8, 8, 8); v.spacing = 6;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperLeft;
        // Largeur préférée 0 : les trois colonnes se partagent la place à parts égales, quelle
        // que soit la longueur des textes (retour du 07/10 : colonnes inégales). Minimum fixe :
        // au gros zoom, le tableau défile en largeur plutôt que d'écraser les cartes.
        UIFactory.LE(img.gameObject, flexW: 1, minW: LARGEUR_MIN_COLONNE, prefW: 0);
        return (RectTransform)img.transform;
    }

    /// Case d'en-tête fixe, au-dessus de sa colonne (hors défilement : la molette ne la rogne
    /// plus, demande du 07/10). Mêmes largeurs que les colonnes.
    public static RectTransform CreerEnTete(Transform parent)
    {
        var img = UIFactory.Panel("EnTête", parent, FondColonne);
        img.pixelsPerUnitMultiplier = 2f;
        var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(8, 8, 4, 4);
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.MiddleLeft;   // texte centré dans la case, pas collé en haut
        UIFactory.LE(img.gameObject, flexW: 1, minW: LARGEUR_MIN_COLONNE, prefW: 0, minH: 34);
        return (RectTransform)img.transform;
    }

    public static void Vider(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
    }

    public static void EnTete(Transform col, Objective.Avancement a, int n, int retards)
    {
        var h = UIFactory.HBox(col, 6, false, "EnTete");
        UIFactory.LE(h.gameObject, minH: 24);
        var point = UIFactory.Panel("Point", h.transform, PointColonne(a));
        UIFactory.LE(point.gameObject, minW: 8, prefW: 8, minH: 8, prefH: 8);
        UIFactory.Text(h.transform, NomColonne(a), UITheme.Role.Donnee, UITheme.TextePrincipal, true);
        Pastille(h.transform, n.ToString(), Color.white, Gris);
        if (retards > 0) Pastille(h.transform, $"{retards} en retard", FondRetard, TxRetard);
    }

    public static TextMeshProUGUI Note(Transform parent, string texte)
    {
        var t = UIFactory.Text(parent, texte, UITheme.Role.Mention, Gris, false, TextAlignmentOptions.Center);
        UIFactory.LE(t.gameObject, minH: 28);
        return t;
    }

    public static Button Lien(Transform parent, string texte, Action clic)
    {
        var b = UIFactory.Button(parent, texte, FondColonne, Bleu, 26, UITheme.Role.Mention, false);
        b.onClick.AddListener(() => clic());
        return b;
    }

    /// Carte d'objectif. `avancer` : bouton « › » (ou « Rouvrir » dans « Fait ») ;
    /// `deposer(colonne)` : fin d'un glisser au-dessus d'une colonne.
    public static GameObject Carte(Transform col, Objective o, string origine, DateTime auj,
        Action ouvrir, Action avancer, Func<Vector2, Camera, int> colonneSous, Action<int> deposer)
    {
        var etat = Objectifs.Etat(o, auj);
        var accent = etat == Objectifs.EtatEcheance.EnRetard ? ObjectiveItem.AccentRetard : ObjectiveItem.AccentImportance(o.importance);
        var corps = Squelette(col, Color.white, BordCarte, accent, out var racine);

        var titre = UIFactory.Text(corps, o.text, UITheme.Role.Donnee,
            o.Fait ? Gris : UITheme.TextePrincipal);
        if (o.Fait) titre.fontStyle = FontStyles.Strikethrough;
        titre.enableWordWrapping = true;
        if (!string.IsNullOrEmpty(origine))
            UIFactory.Text(corps, origine, UITheme.Role.Mention, Gris);

        // Deux lignes, pour tenir dans une colonne étroite (zoom) : importance et échéance,
        // puis récurrence · documents · bouton.
        var meta = UIFactory.HBox(corps, 5, false, "Meta");
        Pastille(meta.transform, ObjectiveItem.LibelleImportance(o.importance),
            ObjectiveItem.FondImportance(o.importance), ObjectiveItem.TexteImportance(o.importance));
        string ech = Objectifs.TexteEcheance(o, auj);
        if (o.Fait) Mention(meta.transform, ech);
        // Forme courte sur la carte (« Retard 15 j ») : la longue ne tenait pas à côté de
        // « Obligatoire » dans une colonne minimale (test ObjectifsTableauTests, 07/10).
        else if (etat == Objectifs.EtatEcheance.EnRetard) Pastille(meta.transform, ech.Replace("En retard de ", "Retard "), FondRetard, TxRetard);
        else if (etat == Objectifs.EtatEcheance.Proche) Pastille(meta.transform, ech, FondProche, TxProche);
        else if (Objectifs.Echeance(o) != null) Mention(meta.transform, ech);

        var pied = UIFactory.HBox(corps, 5, false, "Pied");
        var infos = new List<string>();
        if (o.repeterTous > 0) infos.Add("récurrent");
        int n = o.documents?.Count ?? 0;
        if (n > 0) infos.Add($"{n} doc.");
        if (infos.Count > 0) Mention(pied.transform, string.Join(" · ", infos));
        UIFactory.LE(UIFactory.Rect("Espace", pied.transform).gameObject, flexW: 1);
        var bouton = UIFactory.Button(pied.transform, o.Fait ? "Rouvrir" : "›", Color.white, UITheme.TextePrincipal, 22, UITheme.Role.Donnee, false);
        bouton.image.pixelsPerUnitMultiplier = 3f;
        UIFactory.Border(bouton.gameObject);
        if (o.Fait) UIFactory.LargeurDuTexte(bouton);
        else UIFactory.LE(bouton.gameObject, minW: 26, prefW: 26, flexW: 0);   // petit bouton carré, comme la maquette
        bouton.onClick.AddListener(() => avancer());

        Cliquable(racine, ouvrir);
        var g = racine.AddComponent<CarteGlissable>();
        g.colonneSous = colonneSous;
        g.deposer = deposer;
        return racine;
    }

    /// Carte « Automatique » (rappel de bail, révision ou facturation) : non déplaçable,
    /// un clic ouvre l'écran concerné.
    public static GameObject CarteAuto(Transform col, HomeAlert a, bool avecLocataire)
    {
        var corps = Squelette(col, FondAuto, Ambre, Ambre, out var racine);
        string titre = RappelsAuto.Titre(a) + (avecLocataire && !string.IsNullOrEmpty(a.nomLocataire) ? " · " + a.nomLocataire : "");
        UIFactory.Text(corps, titre, UITheme.Role.Donnee, UITheme.TextePrincipal).enableWordWrapping = true;
        // Le détail du rappel peut être long (« Congé possible jusqu'au … (sortie le …) ») : en
        // texte qui passe à la ligne, pas en pastille, pour tenir dans une colonne étroite.
        bool urgent = a.priorite == 0;
        UIFactory.Text(corps, a.nomRappel, UITheme.Role.Mention, urgent ? TxRetard : TxProche).enableWordWrapping = true;
        var meta = UIFactory.HBox(corps, 5, false, "Meta");
        Pastille(meta.transform, "Automatique", a.typeBg, a.typeTexte);
        UIFactory.LE(UIFactory.Rect("Espace", meta.transform).gameObject, flexW: 1);
        var lien = UIFactory.Text(meta.transform, "Ouvrir ›", UITheme.Role.Mention, Bleu);
        lien.enableWordWrapping = false;

        Cliquable(racine, () => RappelsAuto.Ouvrir(a));
        return racine;
    }

    // Carte : fond + bordure, liseré coloré à gauche, corps vertical. Renvoie le corps.
    static Transform Squelette(Transform col, Color fond, Color bord, Color liseré, out GameObject racine)
    {
        var img = UIFactory.Panel("Carte", col, fond);
        img.pixelsPerUnitMultiplier = 2f;   // coins de 12 : une carte de 2 lignes n'est pas une gélule
        UIFactory.Border(img.gameObject, bord);
        racine = img.gameObject;
        var h = racine.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(6, 0, 0, 0);   // place du liseré
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        UIFactory.LE(racine, flexH: 0);   // une carte seule dans sa colonne garde sa hauteur (07/10)
        // Liseré : fine barre arrondie DANS la carte, en retrait des coins, hors mise en page.
        // (Un rectangle collé au bord dépassait des coins arrondis ; le découper par un Mask
        // grisait les cartes dans la fiche du bâtiment, dont la page est déjà sous un Mask :
        // masques imbriqués, 07/10.)
        var lis = UIFactory.Panel("Liseré", racine.transform, liseré);
        lis.pixelsPerUnitMultiplier = 12f;   // coins de 2
        lis.raycastTarget = false;
        lis.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var lrt = (RectTransform)lis.transform;
        lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0, 1); lrt.pivot = new Vector2(0, .5f);
        lrt.offsetMin = new Vector2(3, 5); lrt.offsetMax = new Vector2(7, -5);
        var corps = UIFactory.VBox(racine.transform, 4, 8, 8, 6, 6, "Corps");
        UIFactory.LE(corps.gameObject, flexW: 1, minW: 0);
        return corps.transform;
    }

    /// Rend un objet cliquable SANS effet de couleur. Ajouter un Button applique aussitôt la
    /// teinte de son état (OnEnable) : si un groupe parent est à ce moment non cliquable
    /// (CanvasGroup.interactable = false, pendant une transition de la fiche du bâtiment), la
    /// teinte « désactivé » (0,78 ; 50 %) grisait la carte pour de bon, la transition étant
    /// ensuite coupée (constaté en direct le 07/10). On efface donc cette teinte.
    public static Button Cliquable(GameObject go, Action clic)
    {
        var b = go.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        var g = go.GetComponent<Graphic>();
        if (g != null) g.canvasRenderer.SetColor(Color.white);
        b.onClick.AddListener(() => clic());
        return b;
    }

    public static void Pastille(Transform parent, string texte, Color fond, Color encre)
    {
        // Le fond arrondi est un enfant HORS mise en page : sur la pastille elle-même, l'Image
        // (sprite 9-slice, coins de 24) imposait 48 px de haut et de large, quel que soit le texte
        // (retours en Play du 07/10 ; pixelsPerUnitMultiplier ne change que le dessin, pas cette taille).
        var p = UIFactory.Rect("Pastille", parent);
        var img = UIFactory.Panel("Fond", p, fond);
        img.pixelsPerUnitMultiplier = 4f;   // coins de 6
        img.raycastTarget = false;
        UIFactory.Stretch((RectTransform)img.transform);
        img.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var h = p.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(7, 7, 2, 2);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        var t = UIFactory.Text(p.transform, texte, UITheme.Role.Pastille, encre);
        t.enableWordWrapping = false;
    }

    static void Mention(Transform parent, string texte)
    {
        var t = UIFactory.Text(parent, texte, UITheme.Role.Mention, Gris);
        t.enableWordWrapping = false;
    }
}

/// Glisser une carte d'une colonne à l'autre : un fantôme suit le pointeur, la colonne
/// sous le pointeur au relâchement reçoit la carte. Un clic sans glisser reste un clic.
public class CarteGlissable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Func<Vector2, Camera, int> colonneSous;
    public Action<int> deposer;
    GameObject _fantome;

    public void OnBeginDrag(PointerEventData e)
    {
        var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null) return;
        var rt = (RectTransform)transform;
        _fantome = Instantiate(gameObject, canvas.transform);
        Destroy(_fantome.GetComponent<CarteGlissable>());
        var frt = (RectTransform)_fantome.transform;
        frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(.5f, .5f);
        frt.sizeDelta = rt.rect.size;
        var cg = _fantome.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; cg.alpha = 0.85f;
        _fantome.transform.SetAsLastSibling();
        OnDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (_fantome == null) return;
        var parent = (RectTransform)_fantome.transform.parent;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, e.position, e.pressEventCamera, out var w))
            _fantome.transform.position = w;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_fantome != null) Destroy(_fantome);
        _fantome = null;
        int c = colonneSous != null ? colonneSous(e.position, e.pressEventCamera) : -1;
        if (c >= 0) deposer?.Invoke(c);
    }

    void OnDisable() { if (_fantome != null) Destroy(_fantome); }
}
