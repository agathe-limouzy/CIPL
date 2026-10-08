using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// Une ligne de la zone « À traiter » du menu home :
/// révisions de loyer à faire + objectifs actifs, triés par urgence.
public class HomeAlert
{
    public enum Kind { RevisionRetard, RevisionProche, BailRenouvellement, Objectif, Facturation, BailResiliation }

    public Kind kind;
    public string typeTodo;      // colonne « Type » : Révision de loyer / Fin de bail / Objectif obligatoire / Rappel / En cours / À faire
    public string nomRappel;     // colonne « Rappel » : texte de l'objectif, ou détail (En retard / Dans X j / Expiré…)
    public string nomLocataire;  // colonne « Locataire » : vide si c'est un objectif de bâtiment
    public string nomBatiment;   // colonne « Bâtiment »
    public int priorite;         // tri : petit = plus urgent
    public Color pastille;       // accent saturé du liseré à gauche
    public Color typeBg;         // fond pastel de la pastille « Type »
    public Color typeTexte;      // couleur du texte de la pastille « Type »

    public BatimentPrefab batiment;
    public Locataire locataire;  // optionnel (null pour un objectif bâtiment)
    public Objective objectif;   // l'objectif lui-même (Kind.Objectif) : le clic ouvre sa fenêtre
    public FacturationAlertes.AlerteType? facturation;   // Kind.Facturation : l'écran à ouvrir
}

/// Tuiles de filtre de « À traiter » (accueil et résumé du bâtiment, maquette « tuiles »
/// choisie le 07/10) : Tout · En retard · À venir · Automatiques · Objectifs.
public static class ATraiterFiltre
{
    public enum Filtre { Tout, EnRetard, AVenir, Automatiques, Objectifs }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    public static readonly (Filtre f, string nom, Color fond, Color encre)[] Tuiles =
    {
        (Filtre.Tout, "Tout", Hex("#ECEAE3"), Hex("#2C3E5E")),
        (Filtre.EnRetard, "En retard", Hex("#FCEBEB"), Hex("#A32D2D")),
        (Filtre.AVenir, "À venir", Hex("#FAEEDA"), Hex("#854F0B")),
        (Filtre.Automatiques, "Automatiques", Hex("#F4E7CD"), Hex("#8A5A0C")),
        (Filtre.Objectifs, "Objectifs", Hex("#F1EFE8"), Hex("#5F5E5A")),
    };

    /// Objectif : son échéance (en retard / proche). Rappel automatique (bail, révision,
    /// facturation) : urgent (priorité 0) = en retard, sinon à venir.
    /// Catégorie (pastilles sous les tuiles, 08/10) : celle de l'objectif ; un rappel
    /// automatique compte comme « Rappel ».
    public static bool Garde(HomeAlert a, Filtre f, Objective.Importance? im, DateTime auj)
    {
        bool obj = a.kind == HomeAlert.Kind.Objectif && a.objectif != null;
        if (im.HasValue && (obj ? a.objectif.importance : Objective.Importance.Rappel) != im.Value) return false;
        switch (f)
        {
            case Filtre.EnRetard: return obj ? Objectifs.Etat(a.objectif, auj) == Objectifs.EtatEcheance.EnRetard : a.priorite == 0;
            case Filtre.AVenir: return obj ? Objectifs.Etat(a.objectif, auj) == Objectifs.EtatEcheance.Proche : a.priorite > 0;
            case Filtre.Automatiques: return !obj;
            case Filtre.Objectifs: return obj;
            default: return true;
        }
    }
}

/// Tuiles cliquables (nombre + libellé) au-dessus d'une liste « À traiter », et pastilles
/// de catégorie dessous (08/10, comme la liste de tous les objectifs) ; les deux se combinent.
public class TuilesATraiter
{
    readonly List<(Image fond, TMPro.TMP_Text nombre, TMPro.TMP_Text libelle, ATraiterFiltre.Filtre f, Color clair, Color encre)> _tuiles = new();
    readonly List<(Button b, Objective.Importance? im)> _chips = new();
    public ATraiterFiltre.Filtre Courant { get; private set; }
    public Objective.Importance? Categorie { get; private set; }
    public Transform Racine { get; }

    public bool Garde(HomeAlert a, DateTime auj) => ATraiterFiltre.Garde(a, Courant, Categorie, auj);

    public TuilesATraiter(Transform parent, Action changement)
    {
        var bloc = UIFactory.VBox(parent, 6, 0, 0, 0, 0, "TuilesATraiter");
        UIFactory.LE(bloc.gameObject, flexH: 0);
        Racine = bloc.transform;
        var row = UIFactory.HBox(bloc.transform, 8, true, "Tuiles");
        row.childControlHeight = true; row.childForceExpandHeight = true;
        UIFactory.LE(row.gameObject, minH: 52, prefH: 52, flexH: 0);
        foreach (var d in ATraiterFiltre.Tuiles)
        {
            var tuile = UIFactory.Panel("Tuile " + d.nom, row.transform, d.fond);
            tuile.pixelsPerUnitMultiplier = 2f;
            var v = tuile.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(10, 10, 3, 3);
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.MiddleLeft;
            UIFactory.LE(tuile.gameObject, flexW: 1, minW: 0, prefW: 0);
            var n = UIFactory.Text(tuile.transform, "0", UITheme.Role.Valeur, d.encre, true);
            var l = UIFactory.Text(tuile.transform, d.nom, UITheme.Role.Mention, d.encre);
            l.enableWordWrapping = false; l.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            var f = d.f;
            ObjectifsTableau.Cliquable(tuile.gameObject, () => { Courant = f; changement?.Invoke(); });
            _tuiles.Add((tuile, n, l, d.f, d.fond, d.encre));
        }

        var imp = UIFactory.HBox(bloc.transform, 6, false, "Categorie");
        UIFactory.LE(imp.gameObject, minH: 30, prefH: 30, flexH: 0);
        UIFactory.Text(imp.transform, "Catégorie", UITheme.Role.Mention, UITheme.TexteSecondaire).enableWordWrapping = false;
        foreach (var (im, nom) in new (Objective.Importance?, string)[] {
            (null, "Toutes"), (Objective.Importance.Obligatoire, "Obligatoire"),
            (Objective.Importance.Rappel, "Rappel"), (Objective.Importance.Normale, "Tâche") })
        {
            var b = UIFactory.Button(imp.transform, nom, Color.white, UITheme.TextePrincipal, 30, UITheme.Role.Action, false);
            b.image.pixelsPerUnitMultiplier = 2.5f;   // coins à l'échelle d'une pastille de 30 px
            UIFactory.LargeurDuTexte(b);
            var i = im;
            b.onClick.AddListener(() => { Categorie = i; changement?.Invoke(); });
            _chips.Add((b, im));
        }
    }

    /// Nombres (sur toutes les alertes, catégorie choisie comprise), tuile et pastille actives
    /// en couleur pleine.
    public void Maj(List<HomeAlert> toutes, DateTime auj)
    {
        foreach (var (fond, nombre, libelle, f, clair, encre) in _tuiles)
        {
            bool on = f == Courant;
            fond.color = on ? encre : clair;
            nombre.text = toutes.Count(a => ATraiterFiltre.Garde(a, f, Categorie, auj)).ToString();
            nombre.color = libelle.color = on ? Color.white : encre;
        }
        foreach (var (b, im) in _chips)
        {
            bool on = Categorie == im;
            Color clair = im.HasValue ? ObjectiveItem.FondImportance(im.Value) : (Color)new Color32(0xEC, 0xEA, 0xE3, 0xFF);
            Color encre = im.HasValue ? ObjectiveItem.TexteImportance(im.Value) : (Color)new Color32(0x2C, 0x3E, 0x5E, 0xFF);
            b.image.color = on ? encre : clair;
            var t = b.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (t != null) t.color = on ? Color.white : encre;
        }
    }
}

/// Rappels automatiques (bail, révision, facturation) affichés en cartes « Automatique » dans
/// le tableau des objectifs d'une fiche (décision du 06/10) : titre, et écran à ouvrir.
public static class RappelsAuto
{
    public static string Titre(HomeAlert a)
    {
        if (a.kind == HomeAlert.Kind.Facturation)
            return a.facturation switch
            {
                FacturationAlertes.AlerteType.Loyer => "Facturer le loyer",
                FacturationAlertes.AlerteType.Regul => "Régulariser les charges",
                FacturationAlertes.AlerteType.Depot => "Réviser le dépôt de garantie",
                FacturationAlertes.AlerteType.Depart => "Départ du locataire",
                FacturationAlertes.AlerteType.Restitution => "Restituer le dépôt",
                FacturationAlertes.AlerteType.Archiver => "Archiver la fiche",
                _ => "Facturation"
            };
        return a.kind switch
        {
            HomeAlert.Kind.RevisionRetard or HomeAlert.Kind.RevisionProche => "Réviser le loyer",
            HomeAlert.Kind.BailRenouvellement => "Fin de bail",
            HomeAlert.Kind.BailResiliation => "Résiliation triennale",
            _ => a.typeTodo
        };
    }

    /// Clic sur la carte : la fiche du locataire, puis l'écran du rappel (facture, régul,
    /// dépôt, départ, révision). Fin de bail / résiliation : la fiche seulement.
    public static void Ouvrir(HomeAlert a)
    {
        var bp = a?.batiment;
        if (bp == null || a.locataire == null || !bp.dictionnairelocataire.TryGetValue(a.locataire, out var lp) || lp == null) return;
        BatimentManager.Instance?.menuManager?.OnSelect(bp);
        bp.ShowLocataireView();
        bp.menulocataire.OnSelect(lp);

        if (a.kind == HomeAlert.Kind.Facturation)
            switch (a.facturation)
            {
                case FacturationAlertes.AlerteType.Loyer: FactureLoyerPanel.OpenLoyer(lp); break;
                case FacturationAlertes.AlerteType.Regul: FactureRegulPanel.OpenRegul(lp); break;
                case FacturationAlertes.AlerteType.Depot:
                case FacturationAlertes.AlerteType.Restitution: FactureDepotPanel.OpenDepot(lp); break;
                case FacturationAlertes.AlerteType.Depart: DepartPanel.Ouvrir(lp); break;
            }
        else if (a.kind == HomeAlert.Kind.RevisionRetard || a.kind == HomeAlert.Kind.RevisionProche)
        {
            var loc = a.locataire;
            if (loc.typeRevision == TypeRevision.Paliers) PaliersPanel.Open(loc, () => lp.OnRevisionSaved(), lp);
            else RevisionPanel.Instance?.Open(loc, () => lp.OnRevisionSaved(), RevisionPanel.Volet.Indice);
        }
    }
}

public static class HomeAlertCollector
{
    private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    // Pastille « Type » — paires fond pastel / texte foncé (mêmes teintes que les badges d'objectifs)
    private static readonly Color BgRevision = Hex("#F3D8E4");   // prune/rose (section financière)
    private static readonly Color TxRevision = Hex("#8E3B5A");
    private static readonly Color BgBail = Hex("#DEE3EB");       // bleu ardoise (section Bail)
    private static readonly Color TxBail = Hex("#2C3E5E");
    private static readonly Color BgMoney = Hex("#F4E7CD");      // ambre (facturation = argent)
    private static readonly Color TxMoney = Hex("#8A5A0C");

    public static List<HomeAlert> Collect(IEnumerable<BatimentPrefab> batiments)
    {
        var alertes = new List<HomeAlert>();
        var today = DateTime.Today;

        foreach (var bp in batiments)
        {
            string nomBat = bp.getName();
            if (string.IsNullOrEmpty(nomBat)) nomBat = "Bâtiment";

            // ── Fin de bail / renouvellement, et résiliation triennale ─────────
            foreach (var loc in bp.listLocataire)
            {
                if (Locataire.ResiliationProche(loc, out var echeanceRes, out var limiteRes))
                    alertes.Add(new HomeAlert
                    {
                        kind = HomeAlert.Kind.BailResiliation,
                        typeTodo = "Résiliation",
                        nomRappel = $"Congé possible jusqu'au {limiteRes:dd/MM/yyyy} (sortie le {echeanceRes:dd/MM/yyyy})",
                        nomLocataire = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name,
                        nomBatiment = nomBat,
                        priorite = 2,
                        pastille = UITheme.Attention,
                        typeBg = BgBail,
                        typeTexte = TxBail,
                        batiment = bp,
                        locataire = loc
                    });

                if (!Locataire.RenouvellementProche(loc, out int joursBail)) continue;

                string nomLocBail = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;
                bool expire = joursBail < 0;
                alertes.Add(new HomeAlert
                {
                    kind = HomeAlert.Kind.BailRenouvellement,
                    typeTodo = "Fin de bail",
                    nomRappel = Locataire.TexteFinDeBail(loc, today),   // même texte que la pastille de la fiche
                    nomLocataire = nomLocBail,
                    nomBatiment = nomBat,
                    priorite = expire ? 0 : 1,
                    pastille = expire ? UITheme.Alerte : UITheme.Attention,
                    typeBg = BgBail,
                    typeTexte = TxBail,
                    batiment = bp,
                    locataire = loc
                });
            }

            // ── Révisions de loyer ────────────────────────────────────────────
            foreach (var loc in bp.listLocataire)
            {
                if (!loc.RevisionIndiceSuivie) continue;   // paliers / sans révision : rien à réviser

                int jours = (loc.MoisDeRevision - today).Days;
                string nomLoc = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;

                if (jours < 0)
                    alertes.Add(new HomeAlert
                    {
                        kind = HomeAlert.Kind.RevisionRetard,
                        typeTodo = "Révision",
                        nomRappel = "En retard",
                        nomLocataire = nomLoc,
                        nomBatiment = nomBat,
                        priorite = 0,
                        pastille = UITheme.Alerte,
                        typeBg = BgRevision,
                        typeTexte = TxRevision,
                        batiment = bp,
                        locataire = loc
                    });
                else if (jours <= BatimentEtatHelper.SEUIL_BIENTOT)
                    alertes.Add(new HomeAlert
                    {
                        kind = HomeAlert.Kind.RevisionProche,
                        typeTodo = "Révision",
                        nomRappel = $"Dans {jours} j",
                        nomLocataire = nomLoc,
                        nomBatiment = nomBat,
                        priorite = 2,
                        pastille = UITheme.Attention,
                        typeBg = BgRevision,
                        typeTexte = TxRevision,
                        batiment = bp,
                        locataire = loc
                    });
            }

            // ── Facturation : envoi loyer / régul charges / révision dépôt ────
            foreach (var loc in bp.listLocataire)
            {
                string nomLocF = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;
                foreach (var fa in FacturationAlertes.Pour(loc))
                {
                    bool urgent = fa.niveau == FacturationAlertes.Niveau.Urgent;
                    string typeF = fa.type switch
                    {
                        FacturationAlertes.AlerteType.Loyer => "Facturer",
                        FacturationAlertes.AlerteType.Regul => "Régul.",
                        FacturationAlertes.AlerteType.Depot => "Rév. dépôt",
                        FacturationAlertes.AlerteType.Depart => "Départ",
                        FacturationAlertes.AlerteType.Restitution => "Rest. dépôt",
                        FacturationAlertes.AlerteType.Archiver => "Archiver",
                        _ => "Facturation"
                    };
                    alertes.Add(new HomeAlert
                    {
                        kind = HomeAlert.Kind.Facturation,
                        typeTodo = typeF,
                        nomRappel = urgent ? "À faire" : "À préparer",
                        nomLocataire = nomLocF,
                        nomBatiment = nomBat,
                        priorite = urgent ? 0 : 2,
                        pastille = urgent ? UITheme.Alerte : UITheme.Attention,
                        typeBg = BgMoney,
                        typeTexte = TxMoney,
                        batiment = bp,
                        locataire = loc,
                        facturation = fa.type
                    });
                }
            }

            // ── Objectifs (bâtiment + locataires) ─────────────────────────────
            var data = bp.getBatiment();
            if (data?.objectifs?.items != null)
                foreach (var obj in ParDate(data.objectifs.items))
                    AjouteObjectif(alertes, obj, nomBat, "", bp, null);

            foreach (var loc in bp.listLocataire)
            {
                if (loc.archive || loc.objectifs?.items == null) continue;   // archivé : plus rien à traiter
                string nomLoc = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;
                foreach (var obj in ParDate(loc.objectifs.items))
                    AjouteObjectif(alertes, obj, nomBat, nomLoc, bp, loc);
            }
        }

        // Tri STABLE par priorité : à priorité égale, l'ordre d'ajout (objectifs par date) est gardé.
        return alertes.OrderBy(a => a.priorite).ToList();
    }

    private static List<Objective> ParDate(List<Objective> items)
    {
        var l = new List<Objective>(items);
        var auj = DateTime.Today;
        l.Sort((a, b) => Objectifs.Comparer(a, b, auj));
        return l;
    }

    private static void AjouteObjectif(List<HomeAlert> alertes, Objective obj,
        string nomBat, string nomLoc, BatimentPrefab bp, Locataire loc)
    {
        if (obj.Fait) return;
        var auj = DateTime.Today;
        var etat = Objectifs.Etat(obj, auj);

        // En retard au rang des urgences, échéance proche juste après, puis l'importance
        // (06/10 : tous les objectifs non faits, triés par date). Rang des autres alertes inchangé.
        int priorite = Objectifs.Rang(obj, auj) switch { 0 => 0, 1 => 1, 2 => 2, 3 => 3, _ => 5 };
        bool date = etat == Objectifs.EtatEcheance.EnRetard || etat == Objectifs.EtatEcheance.Proche;

        alertes.Add(new HomeAlert
        {
            kind = HomeAlert.Kind.Objectif,
            // Mêmes libellé et couleurs que les badges des listes d'objectifs
            typeTodo = ObjectiveItem.LibelleImportance(obj.importance),
            nomRappel = date ? $"{obj.text} · {Objectifs.TexteEcheance(obj, auj).ToLowerInvariant()}" : obj.text,
            nomLocataire = nomLoc,   // vide pour un objectif de bâtiment
            nomBatiment = nomBat,
            priorite = priorite,
            pastille = etat == Objectifs.EtatEcheance.EnRetard ? ObjectiveItem.AccentRetard
                     : etat == Objectifs.EtatEcheance.Proche ? UITheme.Attention
                     : ObjectiveItem.AccentImportance(obj.importance),
            typeBg = ObjectiveItem.FondImportance(obj.importance),
            typeTexte = ObjectiveItem.TexteImportance(obj.importance),
            batiment = bp,
            locataire = loc,
            objectif = obj
        });
    }
}
