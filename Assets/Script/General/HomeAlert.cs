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
    public string cleFacture;    // Revue 17 (D6-04) : la ligne du suivi dont parle une alerte de loyer, la période à ouvrir
}

/// Tuiles de filtre de « À traiter » (accueil et résumé du bâtiment, maquette « tuiles »
/// choisie le 07/10) : Tout · En retard · À venir · Automatiques · Objectifs.
public static class ATraiterFiltre
{
    public enum Filtre { Tout, EnRetard, AVenir, Automatiques, Objectifs }

    public static readonly (Filtre f, string nom, Color fond, Color encre)[] Tuiles =
    {
        (Filtre.Tout, "Tout", UITheme.Hex("#ECEAE3"), UITheme.Hex("#2C3E5E")),
        (Filtre.EnRetard, "En retard", UITheme.Hex("#FCEBEB"), UITheme.Erreur),
        (Filtre.AVenir, "À venir", UITheme.AttentionClair, UITheme.Hex("#854F0B")),
        (Filtre.Automatiques, "Automatiques", UITheme.Hex("#F4E7CD"), UITheme.Hex("#8A5A0C")),
        (Filtre.Objectifs, "Objectifs", UITheme.Fond, UITheme.TexteSecondaire),
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
                // Revue 17 (D6-04, décision du 05/11) : l'alerte ouvre la période dont elle parle, avec sa ligne stockée (un loyer
                // à corriger après un départ garde ainsi ses provisions, D6-07) ; avant : la dernière période enregistrée.
                case FacturationAlertes.AlerteType.Loyer:
                    FactureLoyerPanel.OpenLoyer(lp, a.cleFacture == null ? null
                        : a.locataire.facturesEtat?.Find(x => x.key == a.cleFacture) ?? new FactureEtat { key = a.cleFacture });
                    break;
                case FacturationAlertes.AlerteType.Regul: FactureRegulPanel.OpenRegul(lp); break;
                case FacturationAlertes.AlerteType.Depot:
                case FacturationAlertes.AlerteType.Restitution: FactureDepotPanel.OpenDepot(lp); break;
                // Revue 17 (2c, relecture du 07/11) : un loyer à retirer du suivi après le départ : l'alerte montre le suivi.
                case FacturationAlertes.AlerteType.Depart:
                    if (a.cleFacture != null && DepartLocataire.LirePeriode(a.cleFacture, out int annee, out _)) LocataireSuiviInline.MontrerPour(lp, annee);
                    else DepartPanel.Ouvrir(lp);
                    break;
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
    // Pastille « Type » — paires fond pastel / texte foncé (mêmes teintes que les badges d'objectifs)
    private static readonly Color BgRevision = UITheme.Hex("#F3D8E4");   // prune/rose (section financière)
    private static readonly Color TxRevision = UITheme.Hex("#8E3B5A");
    private static readonly Color BgBail = UITheme.Hex("#DEE3EB");       // bleu ardoise (section Bail)
    private static readonly Color TxBail = UITheme.Hex("#2C3E5E");
    private static readonly Color BgMoney = UITheme.Hex("#F4E7CD");      // ambre (facturation = argent)
    private static readonly Color TxMoney = UITheme.Hex("#8A5A0C");

    public static List<HomeAlert> Collect(IEnumerable<BatimentPrefab> batiments)
    {
        var alertes = new List<HomeAlert>();
        foreach (var bp in batiments) Ajouter(alertes, bp.getName(), bp.getBatiment(), bp.listLocataire, bp);
        return Trier(alertes);
    }

    /// Tri STABLE par priorité : à priorité égale, l'ordre d'ajout (objectifs par date) est gardé.
    public static List<HomeAlert> Trier(List<HomeAlert> alertes) => alertes.OrderBy(a => a.priorite).ToList();

    /// Revue 17 (D8-19, lot G) : les alertes d'un bâtiment, lues sur les données : la fiche à l'écran (`bp`) ne sert qu'au clic.
    /// Les règles de l'accueil (exclusions, priorités) se testent ainsi sans écran.
    public static void Ajouter(List<HomeAlert> alertes, string nomBat, Batiment data, IEnumerable<Locataire> locataires, BatimentPrefab bp)
    {
        var today = DateTime.Today;
        {
            if (string.IsNullOrEmpty(nomBat)) nomBat = "Bâtiment";

            // ── Fin de bail / renouvellement, et résiliation triennale ─────────
            foreach (var loc in locataires)
            {
                if (Locataire.ResiliationProche(loc, out var echeanceRes, out var limiteRes))
                    alertes.Add(new HomeAlert
                    {
                        kind = HomeAlert.Kind.BailResiliation,
                        typeTodo = "Résiliation",
                        nomRappel = $"Congé avant le {limiteRes:dd/MM/yyyy}",   // Revue 17 (D8-15, lot F) : court ; la sortie ({echeanceRes:dd/MM/yyyy}) se lit sur la fiche
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
                    nomRappel = Locataire.TexteFinDeBailCourt(loc, today),   // Revue 17 (D8-15, lot F) : la pastille de la fiche garde le texte complet
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
            foreach (var loc in locataires)
            {
                if (!loc.RevisionAAnnoncer) continue;   // paliers / sans révision, ou parti avant la date (D8-06) : rien à réviser

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
            foreach (var loc in locataires)
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
                        facturation = fa.type,
                        cleFacture = fa.cle   // Revue 17 (D6-04)
                    });
                }
            }

            // ── Objectifs (bâtiment + locataires) ─────────────────────────────
            if (data?.objectifs?.items != null)
                foreach (var obj in ParDate(data.objectifs.items))
                    AjouteObjectif(alertes, obj, nomBat, "", bp, null);

            foreach (var loc in locataires)
            {
                if (loc.archive || loc.objectifs?.items == null) continue;   // archivé : plus rien à traiter
                string nomLoc = string.IsNullOrEmpty(loc.Name) ? "Locataire" : loc.Name;
                foreach (var obj in ParDate(loc.objectifs.items))
                    AjouteObjectif(alertes, obj, nomBat, nomLoc, bp, loc);
            }
        }
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
