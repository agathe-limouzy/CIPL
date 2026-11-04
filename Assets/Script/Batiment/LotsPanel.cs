using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Demande du 04/11 : la fenêtre « Lots du bâtiment » (bouton « Lots » de la fiche, ou « OK » d'un ajout de locataire refusé
/// faute de lot libre). Numéro, surface, désignation et occupant de chaque lot ; un lot occupé garde son numéro et ne se retire
/// pas (son locataire y est rattaché par ce numéro). À l'enregistrement, la surface du bâtiment devient la somme des lots.
/// Réserve le bâtiment le temps de la saisie, comme le formulaire des charges (étape 4b).
public class LotsPanel : MonoBehaviour
{
    public static LotsPanel Instance { get; private set; }

    class Ligne { public int numeroOccupe; public TMP_InputField numero, surface, designation; }

    readonly List<Ligne> _lignes = new List<Ligne>();
    BatimentPrefab _bp;
    Action _apres;   // retour BM3 (04/11) : ouverte depuis le refus d'un ajout, la suite de cet ajout
    RectTransform _scrim;
    Transform _liste;
    TMP_Text _total, _sansLot;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        gameObject.SetActive(false);
    }

    /// `apres` (retour BM3, 04/11) : ouverte depuis le refus d'un ajout de locataire, ce qui suit l'enregistrement quand un lot
    /// est libre : la fiche du nouveau locataire s'ouvre sur ce lot.
    public static void Ouvrir(BatimentPrefab bp, Action apres = null)
    {
        if (bp == null) return;
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("LotsHost", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<LotsPanel>();
        }
        var p = Instance;
        if (p._scrim != null) return;   // déjà ouverte
        EcritureCloud.Reserver(new[] { ("batiment", bp.getID(), bp.getName()) }, "lots", () => { p._bp = bp; p._apres = apres; p.Construire(); },
            () => p != null && p._scrim != null, () => { if (p != null && p._scrim != null) Destroy(p._scrim.gameObject); });
    }

    void Construire()
    {
        var bat = _bp.getBatiment();
        _total = _sansLot = null;
        var root = (FindObjectOfType<Canvas>()?.rootCanvas.transform) ?? transform;
        _scrim = UIFactory.Rect("LotsScrim", root);
        UIFactory.Stretch(_scrim);
        _scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        _scrim.SetAsLastSibling();

        var cardImg = UIFactory.Panel("LotsCard", _scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(780, 640);
        var cv = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.spacing = 10; cv.padding = new RectOffset(18, 18, 16, 16);
        cv.childControlWidth = true; cv.childControlHeight = true;
        cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;

        UIFactory.Text(cv.transform, $"Lots de « {bat.Name} »", UITheme.Role.Valeur, UITheme.TextePrincipal, true);
        UIFactory.Text(cv.transform, "Chaque locataire occupe un lot, qu'il choisit sur sa fiche. La surface du bâtiment est la somme "
            + "des lots ; un lot occupé garde son numéro.", UITheme.Role.Aide, UITheme.TexteSecondaire);

        var entete = UIFactory.HBox(cv.transform, 8, false, "Entete");
        entete.padding = new RectOffset(2, 8, 0, 0);   // comme les lignes de la liste défilante
        Colonne(entete.transform, "N°", 70);
        Colonne(entete.transform, "Surface (m²)", 110);
        Colonne(entete.transform, "Désignation", -1);
        Colonne(entete.transform, "Occupant", 170);
        Colonne(entete.transform, "", 90);

        _liste = ChargePanel.MakeScroll(cv.transform);
        _lignes.Clear();
        foreach (var lot in (bat.lots ?? new List<LotBatiment>()).Where(x => x != null).OrderBy(x => x.numero)) AjouterLigne(lot);

        var bas = UIFactory.HBox(cv.transform, 10, false, "Ajout");
        var ajouter = UIFactory.Button(bas.transform, "+ Ajouter un lot", UITheme.Carte, UITheme.TextePrincipal, 38, UITheme.Role.Bouton);
        UIFactory.Border(ajouter.gameObject); UIFactory.LE(ajouter.gameObject, prefW: 200, flexW: 0);
        ajouter.onClick.AddListener(() => { AjouterLigne(new LotBatiment { numero = ProchainNumero() }); MajTotal(); });
        _total = UIFactory.Text(bas.transform, "", UITheme.Role.Donnee, UITheme.TexteSecondaire, false, TextAlignmentOptions.Right);
        UIFactory.LE(_total.gameObject, flexW: 1);
        _sansLot = UIFactory.Text(cv.transform, "", UITheme.Role.Aide, UITheme.AttentionTexte);

        var actions = UIFactory.HBox(cv.transform, 10, false, "Actions");
        UIFactory.LE(actions.gameObject, minH: 46, prefH: 46);
        var annuler = UIFactory.Button(actions.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 44, UITheme.Role.Bouton);
        UIFactory.Border(annuler.gameObject); UIFactory.LE(annuler.gameObject, flexW: 1);
        annuler.onClick.AddListener(() => Destroy(_scrim.gameObject));
        var ok = UIFactory.Button(actions.transform, "Enregistrer", UITheme.Primaire, Color.white, 44, UITheme.Role.Bouton);
        UIFactory.LE(ok.gameObject, flexW: 1);
        ok.onClick.AddListener(Enregistrer);
        MajTotal();
    }

    void AjouterLigne(LotBatiment lot)
    {
        var bat = _bp.getBatiment();
        var l = new Ligne();
        var row = UIFactory.HBox(_liste, 8, false, "Lot");
        UIFactory.LE(row.gameObject, minH: 42);
        l.numero = Champ(row.transform, lot.numero > 0 ? lot.numero.ToString() : "", "N°", 70, TMP_InputField.ContentType.IntegerNumber);
        l.surface = Champ(row.transform, lot.surface > 0f ? lot.surface.ToString("0.##", CultureInfo.InvariantCulture) : "", "0", 110,
                          TMP_InputField.ContentType.Standard);
        SaisieNumerique.Montant(l.surface);   // retour BN2 (04/11) : la virgule aussi
        l.designation = Champ(row.transform, lot.designation, "Local RDC, parking 12…", -1, TMP_InputField.ContentType.Standard);
        var occupant = UIFactory.Text(row.transform, "", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        occupant.enableWordWrapping = false; occupant.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(occupant.gameObject, prefW: 170, flexW: 0);
        bool enregistre = bat.lots != null && bat.lots.Contains(lot);
        l.numeroOccupe = enregistre && LotsBatiment.Occupants(bat, lot.numero).Count > 0 ? lot.numero : 0;
        l.numero.interactable = l.numeroOccupe == 0;   // son locataire y est rattaché par ce numéro
        void MajOccupant()
        {
            var occ = LotsBatiment.Occupants(bat, Entier(l.numero.text));
            occupant.text = occ.Count == 0 ? "vide" : string.Join(", ", occ.Select(LotsBatiment.NomOccupant));
        }
        MajOccupant();
        l.numero.onValueChanged.AddListener(_ => { MajOccupant(); MajTotal(); });
        l.surface.onValueChanged.AddListener(_ => MajTotal());
        var retirer = UIFactory.Button(row.transform, "Retirer", UITheme.Carte, UITheme.TexteSecondaire, 34, UITheme.Role.Action, false);
        UIFactory.Border(retirer.gameObject); UIFactory.LE(retirer.gameObject, prefW: 90, flexW: 0);
        retirer.onClick.AddListener(() =>
        {
            if (l.numeroOccupe > 0)
            {
                ConfirmDialog.Erreur($"Le lot {l.numeroOccupe} est occupé par "
                    + string.Join(", ", LotsBatiment.Occupants(bat, l.numeroOccupe).Select(o => $"« {LotsBatiment.Nom(o)} »"))
                    + " : il ne peut pas être retiré. Changez d'abord son lot, sur sa fiche.");
                return;
            }
            _lignes.Remove(l);
            Destroy(row.gameObject);
            MajTotal();
        });
        _lignes.Add(l);
    }

    List<LotBatiment> Lire() => _lignes.Select(l => new LotBatiment
    {
        numero = Entier(l.numero.text), surface = SaisieNumerique.Parse(l.surface.text),
        designation = string.IsNullOrWhiteSpace(l.designation.text) ? null : l.designation.text.Trim(),
    }).ToList();

    /// Le total, les lots vides et les locataires qu'aucun lot ne porte encore (à rattacher sur leur fiche).
    void MajTotal()
    {
        if (_total == null || _sansLot == null) return;
        var bat = _bp.getBatiment();
        var lots = Lire();
        int vides = lots.Count(x => LotsBatiment.Occupants(bat, x.numero).Count == 0);
        _total.text = $"{lots.Count} lot{(lots.Count > 1 ? "s" : "")} · {lots.Sum(x => x.surface):0.##} m²"
                      + (vides > 0 ? $" · {vides} vide{(vides > 1 ? "s" : "")}" : "");
        var sans = LotsBatiment.Occupes(bat.locataireDuBatiment).Where(o => !lots.Any(x => x.numero > 0 && x.numero == o.lotBatiment)).ToList();
        _sansLot.text = sans.Count == 0 ? ""
            : $"Sans lot : {string.Join(", ", sans.Select(LotsBatiment.Nom))}. Après l'enregistrement, choisissez "
              + (sans.Count == 1 ? "son lot sur sa fiche." : "leur lot sur leur fiche.");
        _sansLot.gameObject.SetActive(sans.Count > 0);
    }

    void Enregistrer()
    {
        var bat = _bp.getBatiment();
        var lots = Lire();
        string refus = LotsBatiment.Refus(lots, bat);
        if (refus != null) { ConfirmDialog.Erreur(refus); return; }   // la fenêtre reste ouverte, avec la saisie
        bat.lots = lots.OrderBy(x => x.numero).ToList();
        bat.tailleBatiment = LotsBatiment.SurfaceTotale(bat);
        _bp.ApresLots();   // enregistre le bâtiment, verrou encore tenu
        Destroy(_scrim.gameObject);
        var apres = _apres; _apres = null;
        if (apres != null && LotsBatiment.RefusSansLotLibre(bat) == null) apres();   // retour BM3 : la fiche du nouveau locataire
    }

    int ProchainNumero() => _lignes.Select(x => Entier(x.numero.text)).DefaultIfEmpty(0).Max() + 1;

    static int Entier(string s) => int.TryParse((s ?? "").Trim(), out var n) ? n : 0;

    static TMP_InputField Champ(Transform parent, string valeur, string exemple, float largeur, TMP_InputField.ContentType type)
    {
        var f = UIFactory.Input(parent, exemple, 38);
        f.contentType = type;
        f.text = valeur ?? "";
        if (largeur > 0) UIFactory.LE(f.gameObject, prefW: largeur, flexW: 0); else UIFactory.LE(f.gameObject, minW: 120, flexW: 1);
        return f;
    }

    static void Colonne(Transform parent, string titre, float largeur)
    {
        var t = UIFactory.Text(parent, titre, UITheme.Role.Libelle, UITheme.TexteSecondaire);
        if (largeur > 0) UIFactory.LE(t.gameObject, prefW: largeur, flexW: 0); else UIFactory.LE(t.gameObject, minW: 120, flexW: 1);
    }
}
