using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Formulaire d'ajout / modification d'une charge de bâtiment (modale, construite
/// par code). La LISTE (historique) est rendue par InvestissementListPanel, au même
/// visuel qu'Achat / Travaux ; ce composant ne fournit que le formulaire.
/// Champs : nom, coût, date, locataire(s) concerné(s) (Tous ou sélection +
/// répartition par ratio), PDF de la facture, statut payé/impayé.
public class ChargePanel : MonoBehaviour
{
    public static ChargePanel Instance { get; private set; }

    static readonly Color Accent = Hex("#7A5AA6");      // violet — charges
    static readonly Color AccentClair = Hex("#ECE4F5");

    BatimentPrefab _bp;
    Action _onSaved;

    // ── État du formulaire en cours ────────────────────────────────────────────
    ChargeBatiment _edit;
    bool _isNew;
    Toggle _fTous;
    UIDropdown _fListe;   // liste de charges ; null s'il n'y a que la générale
    readonly Dictionary<string, Toggle> _fLoc = new Dictionary<string, Toggle>();
    readonly Dictionary<string, TMP_InputField> _fRatio = new Dictionary<string, TMP_InputField>();
    readonly List<TMP_InputField> _manualEditable = new List<TMP_InputField>();
    TMP_InputField _manualLast;      // dernier locataire (Manuel) = 1 − somme des autres
    TMP_InputField _fCout;
    readonly List<(Locataire l, TMP_Text t)> _fMontant = new List<(Locataire, TMP_Text)>();   // part en euros de chacun
    Transform _locBox, _ratioBox;
    string _fType = "Surface";
    string _fPdf;
    TMP_Text _fPdfLabel;
    Button _chipSurface, _chipEgal, _chipManuel;
    // Revue 17 (retour BL4, 04/11) : période de la charge (sur l'année / ponctuelle) et la date qui décide des présents.
    Button _chipAnnee, _chipPonctuelle;
    bool _fPonctuelle;
    TMP_Text _aidePeriode;
    TMP_InputField _fDate;
    TMP_Text _fDetail;   // retour BL8 (04/11) : jours de présence et part du propriétaire, sous les montants

    Batiment Bat => _bp != null ? _bp.getBatiment() : null;

    /// Surface du lot pour la répartition « Surface » : la saisie, sinon sa part du disponible (point de passage
    /// unique, BatimentPrefab.TailleLotEffective). Revue 16c, D6-01 : un lot à surface déduite comptait pour 1 m².
    float Surface(Locataire l) => _bp != null ? _bp.TailleLotEffective(l) : (l?.tailleLot ?? 0f);
    RectTransform _scrim;   // formulaire ouvert tant qu'il existe (verrou du bâtiment, étape 4b)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        gameObject.SetActive(false);
    }

    // ── Entrée publique ────────────────────────────────────────────────────────

    /// Ouvre le formulaire (ajout si existing == null). `onSaved` est invoqué après
    /// écriture de la charge dans le bâtiment (le caller persiste + rafraîchit).
    public static void OpenForm(BatimentPrefab bp, ChargeBatiment existing, Action onSaved)
    {
        if (bp == null) return;
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("ChargeFormHost", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<ChargePanel>();
        }
        var p = Instance;   // sur le cloud : bâtiment réservé tant que le formulaire est ouvert (étape 4b)
        EcritureCloud.Reserver(new[] { ("batiment", bp.getID(), bp.getName()) }, "charges", () =>
        {
            p._bp = bp;
            p._onSaved = onSaved;
            p.OpenChargeForm(existing);
        }, () => p != null && p._scrim != null, () => { if (p != null && p._scrim != null) Destroy(p._scrim.gameObject); });
    }

    // ── Formulaire d'ajout / modification (modale) ─────────────────────────────

    void OpenChargeForm(ChargeBatiment existing)
    {
        _isNew = existing == null;
        _edit = existing != null ? Clone(existing) : new ChargeBatiment { dateISO = DateTime.Today.ToString("yyyy-MM-dd") };
        _fType = string.IsNullOrEmpty(_edit.typeRatio) ? "Surface" : _edit.typeRatio;
        _fPonctuelle = _edit.ponctuelle;
        _fPdf = DossiersDonnees.PdfCharge(_bp.getName(), _edit.pdfPath);
        _fLoc.Clear(); _fRatio.Clear();

        var root = (FindObjectOfType<Canvas>()?.rootCanvas.transform) ?? transform;
        var scrim = UIFactory.Rect("ChargeScrim", root);
        _scrim = scrim;
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        scrim.SetAsLastSibling();

        // Carte avec scroll interne (hauteur bornée)
        var cardImg = UIFactory.Panel("ChargeCard", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(660, 860);
        var cv = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.spacing = 10; cv.padding = new RectOffset(18, 18, 16, 16);
        cv.childControlWidth = true; cv.childControlHeight = true;
        cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;

        UIFactory.Text(cv.transform, _isNew ? "Nouvelle charge" : "Modifier la charge", UITheme.Role.Valeur, UITheme.TextePrincipal, true);

        var body = MakeScroll(cv.transform);   // corps défilable
        UIFactory.LE(((Transform)body.parent.parent).gameObject, flexH: 1);

        var fNom = LabeledInput(body, "Nom de la charge", "Taxe foncière 2026…", _edit.nom);
        // Négatif = avoir d'un fournisseur : il doit se relire tel quel, sinon un nouvel
        // enregistrement le remettrait à 0 en silence.
        var fCout = LabeledInput(body, "Coût (€, négatif pour un avoir)", "0", _edit.cout != 0 ? _edit.cout.ToString(CultureInfo.InvariantCulture) : "");
        SaisieNumerique.Montant(fCout);   // retour BN2 (04/11) : la virgule aussi
        _fCout = fCout;
        fCout.onValueChanged.AddListener(_ => MajMontants());
        var fDate = LabeledInput(body, "Date", "JJ/MM/AAAA",
            DateTime.TryParse(_edit.dateISO, out var de) ? de.ToString("dd/MM/yyyy") : "");
        _fDate = fDate;   // ponctuelle « Tous » : la date décide de qui la paie
        fDate.onValueChanged.AddListener(_ => { MajAidePeriode(); if (_fPonctuelle && _fTous.isOn) RebuildRatio(); else MajMontants(); });

        // Liste de charges : proposée seulement si des listes spécifiques existent
        // (Réglages). Chaque liste est régularisée à sa propre date.
        _fListe = null;
        if (ListesCharges.Specifiques().Count > 0)
        {
            UIFactory.Text(body, "Liste de charges (régularisée à sa propre date)", UITheme.Role.Libelle, UITheme.TexteSecondaire);
            var ids = new List<string> { "" };
            ids.AddRange(ListesCharges.Specifiques().Select(l => l.id));
            // Changer de liste change les locataires qui peuvent la porter.
            _fListe = UIDropdown.Create(body, ids.Select(ListesCharges.Nom).ToList(), ids,
                Mathf.Max(0, ids.IndexOf(ListesCharges.Effective(_edit.listeId))),
                _ => { if (_locBox != null) { RefreshLocBox(); RebuildRatio(); } });
        }

        // Locataires concernés
        UIFactory.Text(body, "Locataire(s) concerné(s)", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        _fTous = UIFactory.Toggle(body, "Tous les locataires", _edit.tousLocataires);
        _fTous.onValueChanged.AddListener(_ => { RefreshLocBox(); RebuildRatio(); MajAidePeriode(); });

        var locWrap = UIFactory.VBox(body, 4, 0, 0, 0, 0, "LocBox");
        _locBox = locWrap.transform;

        // Répartition (ratios)
        var ratioWrap = UIFactory.VBox(body, 6, 0, 0, 0, 0, "RatioBox");
        _ratioBox = ratioWrap.transform;

        // Période (Revue 17, retour BL4, 04/11), visible même pour un seul locataire.
        UIFactory.Text(body, "Période", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        var periode = UIFactory.HBox(body, 6, false, "PeriodeChips");
        _chipAnnee = PeriodeChip(periode.transform, "Sur l'année", false);
        _chipPonctuelle = PeriodeChip(periode.transform, "Ponctuelle", true);
        _aidePeriode = UIFactory.Text(body, "", UITheme.Role.Aide, UITheme.TexteSecondaire);
        MajAidePeriode();

        // PDF
        UIFactory.Text(body, "PDF de la facture", UITheme.Role.Libelle, UITheme.TexteSecondaire);
        var pdfRow = UIFactory.HBox(body, 8, false, "PdfRow");
        var pick = UIFactory.Button(pdfRow.transform, "Choisir un PDF…", AccentClair, Accent, 38, UITheme.Role.Bouton);
        UIFactory.LE(pick.gameObject, prefW: 180, flexW: 0);
        pick.onClick.AddListener(ChoisirPdf);
        _fPdfLabel = UIFactory.Text(pdfRow.transform, PdfLabel(), UITheme.Role.Donnee, UITheme.TexteSecondaire);
        UIFactory.LE(_fPdfLabel.gameObject, flexW: 1);
        var clearPdf = UIFactory.Button(pdfRow.transform, "Retirer", UITheme.Carte, UITheme.TexteSecondaire, 34, UITheme.Role.Action);
        UIFactory.Border(clearPdf.gameObject); UIFactory.LE(clearPdf.gameObject, prefW: 90, flexW: 0);
        clearPdf.onClick.AddListener(() => { _fPdf = null; if (_fPdfLabel != null) _fPdfLabel.text = PdfLabel(); });

        // Statut. Le paiement d'une facture se suit par locataire, dans son suivi ;
        // cette case retire la charge du choix de TOUS (réglée hors facturation).
        var fPaye = UIFactory.Toggle(body, "Réglée pour tous (plus proposée à la refacturation ni à la régularisation)", _edit.paye);
        if (_edit.EstFacturee)
            UIFactory.Text(body, $"État par locataire : {_edit.Etat}.", UITheme.Role.Aide, UITheme.TexteSecondaire);

        RefreshLocBox();
        RebuildRatio();

        // Actions
        var actions = UIFactory.HBox(cv.transform, 10, false, "Actions");
        UIFactory.LE(actions.gameObject, minH: 46, prefH: 46);
        var cancel = UIFactory.Button(actions.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 44, UITheme.Role.Bouton);
        UIFactory.Border(cancel.gameObject); UIFactory.LE(cancel.gameObject, flexW: 1);
        cancel.onClick.AddListener(() => Destroy(scrim.gameObject));
        var ok = UIFactory.Button(actions.transform, "Enregistrer", Accent, Color.white, 44, UITheme.Role.Bouton);
        UIFactory.LE(ok.gameObject, flexW: 1);
        ok.onClick.AddListener(() =>
        {
            SaveForm(fNom.text, fCout.text, fDate.text, fPaye.isOn);
            Destroy(scrim.gameObject);
        });
    }

    // Cases à cocher par locataire (visibles seulement si « Tous » décoché).
    void RefreshLocBox()
    {
        foreach (Transform c in _locBox) Destroy(c.gameObject);
        _fLoc.Clear();
        bool show = !_fTous.isOn;
        _locBox.gameObject.SetActive(show);
        if (!show) return;

        foreach (var l in Attribuables())
        {
            string nom = string.IsNullOrEmpty(l.Name) ? $"Lot {l.lotBatiment}" : l.Name;
            bool on = _edit.locatairesConcernes != null && _edit.locatairesConcernes.Contains(l.id);
            var t = UIFactory.Toggle(_locBox, nom, on);
            t.onValueChanged.AddListener(_ => RebuildRatio());
            _fLoc[l.id] = t;
        }
    }

    List<Locataire> Concerned()
    {
        if (_fTous.isOn)
            return _fPonctuelle && _fDate != null && TryParseFr(_fDate.text, out var jour)
                ? Attribuables().Where(l => Loyers.PresentLe(l, jour)).ToList()   // ponctuelle : les présents à sa date
                : Attribuables();
        return Attribuables().Where(l => _fLoc.TryGetValue(l.id, out var t) && t.isOn).ToList();
    }

    /// Locataires qui peuvent porter la charge : ceux concernés par sa liste. Un
    /// locataire limité à une autre liste n'est ni proposé, ni compté dans « Tous ».
    List<Locataire> Attribuables()
    {
        string liste = _fListe != null ? _fListe.SelectedId : _edit.listeId;
        return _bp.listLocataire.Where(l => ListesCharges.ConcerneParListe(l, liste)).ToList();
    }

    // Section « répartition » : visible seulement si > 1 locataire concerné.
    // Surface  → surface de chaque lot (lecture seule).
    // Égalité  → 1/N (lecture seule).
    // Manuel   → fractions modifiables ; le dernier locataire = 1 − somme des autres.
    void RebuildRatio()
    {
        foreach (Transform c in _ratioBox) Destroy(c.gameObject);
        _fRatio.Clear(); _manualEditable.Clear(); _manualLast = null; _fMontant.Clear(); _fDetail = null;
        var concerned = Concerned();
        // Retour BL8 (04/11) : un locataire seul aussi, quand il ne paie pas tout (arrivée en cours d'année, locaux vides).
        bool show = concerned.Count > 1 || (concerned.Count == 1 && DetailDuPartage(Apercu(), concerned, Bat) != "");
        _ratioBox.gameObject.SetActive(show);
        if (!show) return;

        UIFactory.Text(_ratioBox, "Répartition entre locataires", UITheme.Role.Libelle, UITheme.TexteSecondaire);

        var chips = UIFactory.HBox(_ratioBox, 6, false, "TypeChips");
        _chipSurface = TypeChip(chips.transform, "Surface");
        _chipEgal = TypeChip(chips.transform, "Égalité");
        _chipManuel = TypeChip(chips.transform, "Manuel");
        StyleChips();

        int n = concerned.Count;
        var manualInit = InitialManual(concerned);

        for (int i = 0; i < n; i++)
        {
            var l = concerned[i];
            var row = UIFactory.HBox(_ratioBox, 8, false, "RatioRow");
            UIFactory.LE(row.gameObject, minH: 40);
            var nom = UIFactory.Text(row.transform,
                string.IsNullOrEmpty(l.Name) ? $"Lot {l.lotBatiment}" : l.Name, UITheme.Role.Donnee, UITheme.TextePrincipal);
            UIFactory.LE(nom.gameObject, flexW: 1);

            if (_fType == "Manuel")
            {
                bool isLast = i == n - 1;
                var inp = UIFactory.Input(row.transform, "0", 38);
                SaisieNumerique.Montant(inp);   // retour BN2 (04/11) : la virgule aussi
                UIFactory.LE(inp.gameObject, prefW: 120, flexW: 0);
                inp.text = manualInit[i].ToString("0.###", CultureInfo.InvariantCulture);
                inp.interactable = !isLast;         // dernier = reste (auto)
                _fRatio[l.id] = inp;
                if (isLast) _manualLast = inp;
                else { _manualEditable.Add(inp); inp.onValueChanged.AddListener(_ => { RecomputeManualLast(); MajMontants(); }); }
            }
            else
            {
                string txt = _fType == "Surface"
                    ? $"{Surface(l):0.##} m²"
                    : $"1/{n}";
                var val = UIFactory.Text(row.transform, txt, UITheme.Role.Donnee, UITheme.TextePrincipal, true,
                    TextAlignmentOptions.Right);
                UIFactory.LE(val.gameObject, prefW: 120, flexW: 0);
            }
            var montant = UIFactory.Text(row.transform, "", UITheme.Role.Donnee, UITheme.TextePrincipal, true,
                TextAlignmentOptions.Right);
            UIFactory.LE(montant.gameObject, prefW: 120, flexW: 0);
            _fMontant.Add((l, montant));
        }
        if (_fType == "Manuel") RecomputeManualLast();
        _fDetail = UIFactory.Text(_ratioBox, "", UITheme.Role.Aide, UITheme.TexteSecondaire);
        MajMontants();
    }

    /// Parts de répartition : surface du lot (1 si inconnue), 1 chacun (Égalité) ou part saisie (Manuel) ; une part (le
    /// tout) pour un seul locataire (Revue 17, D6-01). Les mêmes pour l'enregistrement et pour les montants affichés.
    public static List<ChargeRatio> Ratios(string type, List<Locataire> concernes, Func<Locataire, float> surface,
                                           Func<Locataire, float> manuel)
    {
        var res = new List<ChargeRatio>();
        // Revue 17 (D6-01) : un seul locataire a aussi sa part : sans part, chaque locataire arrivé ensuite payait la charge
        // en entier. Un arrivant suit la règle des charges partagées (pas de part, rien à payer). Part 1 quel que soit le
        // type : en « Manuel », le formulaire n'affiche pas de champ pour un seul locataire.
        if (concernes.Count == 1) { res.Add(new ChargeRatio(concernes[0].id, 1f)); return res; }
        if (concernes.Count > 1)
            foreach (var l in concernes)
            {
                float part;
                if (type == "Surface") part = surface(l) > 0f ? surface(l) : 1f;
                else if (type == "Égalité") part = 1f;
                else part = manuel(l);   // Manuel (dernier = reste déjà calculé)
                res.Add(new ChargeRatio(l.id, part));
            }
        return res;
    }

    float PartManuelle(Locataire l) => _fRatio.TryGetValue(l.id, out var inp) ? ParseFloat(inp.text) : 0f;

    /// La part de chacun en euros, à côté de sa base (décision du 01/11) : ce que la régularisation facturera, et la
    /// refacturation avec sa case « au prorata » (cochée par défaut). Retour BL8 (04/11) : au prorata de la présence (avant :
    /// la part entière), avec dessous les jours de chacun et la part du propriétaire (DetailDuPartage).
    void MajMontants()
    {
        if (_fMontant.Count == 0 || _fCout == null) return;
        var c = Apercu();
        foreach (var (l, t) in _fMontant)
            if (t != null) t.text = Euros(ListesCharges.QuotePartAuProrata(c, l, Bat));
        if (_fDetail == null) return;
        _fDetail.text = DetailDuPartage(c, _fMontant.Select(x => x.l).ToList(), Bat);
        _fDetail.gameObject.SetActive(_fDetail.text != "");
    }

    /// La charge telle que le formulaire la décrit, sans l'enregistrer. Revue 17 (retour BL4, 04/11) : le calcul relit son
    /// type, ses désignés, sa date et sa période (PartsEffectives) ; sans eux, il comptait « Surface, tous les locataires ».
    ChargeBatiment Apercu()
    {
        var concernes = Concerned();
        return new ChargeBatiment
        {
            cout = ParseFloat(_fCout.text), ratios = Ratios(_fType, concernes, Surface, PartManuelle),
            listeId = _fListe != null ? ListesCharges.Effective(_fListe.SelectedId) : _edit.listeId,
            typeRatio = _fType, tousLocataires = _fTous.isOn, ponctuelle = _fPonctuelle,
            locatairesConcernes = _fTous.isOn ? new List<string>() : concernes.Select(l => l.id).ToList(),
            dateISO = _fDate != null && TryParseFr(_fDate.text, out var jour) ? jour.ToString("yyyy-MM-dd") : _edit.dateISO,
        };
    }

    /// Retour BL8 (04/11) : sous les montants, pourquoi un locataire paie moins que sa part entière (ses jours dans l'année
    /// de la charge) et la part du propriétaire (locaux vides, ou vides une partie de l'année). Vide s'il n'y a rien à dire.
    /// « BH4 C arrive le 12/12/2026 : 20 jours sur 365 (part entière 250,00 €). Part du propriétaire (locaux vides) : 236,30 €. »
    public static string DetailDuPartage(ChargeBatiment c, IList<Locataire> concernes, Batiment bat)
    {
        if (c == null || concernes == null) return "";
        static string Jour(DateTime x) => x.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        DateTime d = default;
        bool annee = !c.ponctuelle && FacturationSuivi.TryEcheance(c.dateISO, out d);   // ponctuelle : part entière
        int joursAn = annee && DateTime.IsLeapYear(d.Year) ? 366 : 365;
        var phrases = new List<string>();
        double du = 0;
        foreach (var l in concernes)
        {
            if (l == null) continue;
            float part = ListesCharges.QuotePart(c, l, bat);
            double f = annee ? Loyers.FractionPresence(l, d.Year) : 1;
            du += part * f;
            if (c.typeRatio == "Surface" && LotsBatiment.Definis(bat) && LotsBatiment.Lot(bat, l) == null)   // lots déclarés (04/11)
            {
                phrases.Add($"{LotsBatiment.Nom(l)} n'a pas de lot : il ne paie pas cette charge ; choisissez son lot sur sa fiche.");
                continue;
            }
            if (f >= 0.9999) continue;
            string nom = string.IsNullOrEmpty(l.Name) ? $"Lot {l.lotBatiment}" : l.Name;
            if (f <= 0) { phrases.Add($"{nom} : pas dans les lieux en {d.Year}."); continue; }
            bool arrive = Loyers.DebutPremierBail(l, out var debut) && debut.Year == d.Year;
            var fin = Loyers.FinFacturation(l);
            string qui = arrive && fin.Year == d.Year ? $"{nom}, du {Jour(debut)} au {Jour(fin)}"
                       : arrive ? $"{nom} arrive le {Jour(debut)}" : $"{nom} part le {Jour(fin)}";
            phrases.Add($"{qui} : {Math.Round(f * joursAn)} jours sur {joursAn} (part entière {Euros(part)}).");
        }
        double reste = c.cout - du;
        if (Math.Abs(reste) >= 0.01) phrases.Add($"Part du propriétaire (locaux vides) : {Euros((float)reste)}.");
        return string.Join(" ", phrases);
    }

    static string Euros(float v) => v.ToString("N2", FacturePdfService.FrCulture) + " €";

    // Le dernier champ (Manuel) absorbe le reste pour que le total fasse 1.
    void RecomputeManualLast()
    {
        if (_manualLast == null) return;
        float sum = 0f;
        foreach (var inp in _manualEditable) sum += ParseFloat(inp.text);
        float rest = Mathf.Clamp(1f - sum, 0f, 1f);
        _manualLast.SetTextWithoutNotify(rest.ToString("0.###", CultureInfo.InvariantCulture));
    }

    // Valeurs initiales Manuel (fractions sommant à 1) : reprend les ratios existants
    // normalisés, sinon répartit à parts égales.
    List<float> InitialManual(List<Locataire> concerned)
    {
        int n = concerned.Count;
        var res = new List<float>();
        float total = 0f;
        var existing = new List<float>();
        foreach (var l in concerned)
        {
            var r = _edit.ratios?.FirstOrDefault(x => x.locataireId == l.id);
            float v = r != null ? r.part : 0f;
            existing.Add(v); total += v;
        }
        if (total > 0f) for (int i = 0; i < n; i++) res.Add(existing[i] / total);
        else for (int i = 0; i < n; i++) res.Add(1f / n);
        return res;
    }

    Button TypeChip(Transform parent, string type)
    {
        var b = UIFactory.Button(parent, type, UITheme.Carte, UITheme.TextePrincipal, 34, UITheme.Role.Action, false);
        UIFactory.Border(b.gameObject); UIFactory.LE(b.gameObject, prefW: 120, flexW: 0);
        b.onClick.AddListener(() => { _fType = type; RebuildRatio(); });
        return b;
    }

    Button PeriodeChip(Transform parent, string libelle, bool ponctuelle)
    {
        var b = UIFactory.Button(parent, libelle, UITheme.Carte, UITheme.TextePrincipal, 34, UITheme.Role.Action, false);
        UIFactory.Border(b.gameObject); UIFactory.LE(b.gameObject, prefW: 140, flexW: 0);
        b.onClick.AddListener(() => { _fPonctuelle = ponctuelle; MajAidePeriode(); RebuildRatio(); });
        return b;
    }

    /// La pastille choisie et sa phrase d'aide (avec la date tapée, pour une charge ponctuelle « Tous »).
    void MajAidePeriode()
    {
        StyleChip(_chipAnnee, !_fPonctuelle);
        StyleChip(_chipPonctuelle, _fPonctuelle);
        if (_aidePeriode == null) return;
        string jour = _fDate != null && TryParseFr(_fDate.text, out var d) ? "le " + d.ToString("dd/MM/yyyy") : "à sa date";
        _aidePeriode.text = !_fPonctuelle
            ? "Sur l'année (taxe foncière, entretien…) : les présents de l'année se la partagent ; un locataire arrivé ou parti en cours d'année paie au prorata de sa présence."
            : _fTous.isOn
                ? $"Ponctuelle (réparation, intervention…) : seuls les locataires présents {jour} la paient, chacun sa part entière."
                : "Ponctuelle (réparation, intervention…) : les locataires cochés la paient, chacun sa part entière, sans prorata de présence.";
    }

    void StyleChips()
    {
        StyleChip(_chipSurface, _fType == "Surface");
        StyleChip(_chipEgal, _fType == "Égalité");
        StyleChip(_chipManuel, _fType == "Manuel");
    }

    static void StyleChip(Button b, bool active)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>(); if (img != null) img.color = active ? Accent : UITheme.Carte;
        var t = b.GetComponentInChildren<TMP_Text>(true); if (t != null) t.color = active ? Color.white : UITheme.TextePrincipal;
    }

    // ── PDF ────────────────────────────────────────────────────────────────────

    void ChoisirPdf()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        var paths = SFB.StandaloneFileBrowser.OpenFilePanel(
            "Choisir la facture de charge", "",
            new[] { new SFB.ExtensionFilter("Documents", "pdf", "jpg", "jpeg", "png") }, false);
        if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            _fPdf = paths[0];
            if (_fPdfLabel != null) _fPdfLabel.text = PdfLabel();
        }
#endif
    }

    string PdfLabel() => string.IsNullOrEmpty(_fPdf) ? "Aucun fichier" : Path.GetFileName(_fPdf);

    // ── Sauvegarde du formulaire ───────────────────────────────────────────────

    void SaveForm(string nom, string coutTxt, string dateTxt, bool paye)
    {
        _edit.nom = nom?.Trim();
        _edit.cout = ParseFloat(coutTxt);
        _edit.dateISO = TryParseFr(dateTxt, out var dt) ? dt.ToString("yyyy-MM-dd") : _edit.dateISO;
        _edit.paye = paye;
        if (_fListe != null) _edit.listeId = ListesCharges.Effective(_fListe.SelectedId);

        _edit.tousLocataires = _fTous.isOn;
        var concerned = Concerned();
        _edit.locatairesConcernes = _fTous.isOn ? new List<string>() : concerned.Select(l => l.id).ToList();
        _edit.typeRatio = _fType;
        _edit.ponctuelle = _fPonctuelle;
        _edit.ratios = Ratios(_fType, concerned, Surface, PartManuelle);

        // Copie du PDF dans le dossier de sauvegarde (comme le bail / les photos).
        // Comparaison entre chemins ABSOLUS : _edit.pdfPath est desormais relatif,
        // le comparer tel quel aurait recopie le PDF a chaque enregistrement. Meme
        // resolution qu'a l'ouverture (PdfCharge) : apres un renommage, elle seule
        // retrouve le PDF, et une autre recopierait le meme fichier en « x_1.pdf ».
        if (!string.IsNullOrEmpty(_fPdf) && _fPdf != DossiersDonnees.PdfCharge(_bp.getName(), _edit.pdfPath)
            && File.Exists(_fPdf))
            _edit.pdfPath = DossiersDonnees.VersRelatif(CopyPdf(_fPdf));
        else if (string.IsNullOrEmpty(_fPdf))
            _edit.pdfPath = "";

        if (Bat.charges == null) Bat.charges = new List<ChargeBatiment>();
        int idx = Bat.charges.FindIndex(c => c.id == _edit.id);
        if (idx >= 0) Bat.charges[idx] = _edit; else Bat.charges.Add(_edit);

        _onSaved?.Invoke();
    }

    string CopyPdf(string src)
    {
        // Dossier du batiment par son NOM (via DossiersDonnees), comme les factures
        // et les photos : les justificatifs atterrissaient dans un dossier nomme par
        // GUID, invisible pour qui ouvre le dossier lisible du batiment.
        string dossier = DossiersDonnees.DossierCharges(_bp.getName());
        Directory.CreateDirectory(dossier);
        string bn = Path.GetFileNameWithoutExtension(src), ext = Path.GetExtension(src);
        string dest = Path.Combine(dossier, bn + ext);
        int i = 1;
        while (File.Exists(dest)) dest = Path.Combine(dossier, $"{bn}_{i++}{ext}");
        File.Copy(src, dest, false);
        return dest;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    internal static Transform MakeScroll(Transform parent)   // aussi la fenêtre des lots (LotsPanel, 04/11)
    {
        var srGO = UIFactory.Rect("Scroll", parent);
        var sr = srGO.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 32;
        sr.movementType = ScrollRect.MovementType.Clamped;
        UIFactory.LE(srGO.gameObject, flexH: 1);

        var viewport = UIFactory.Rect("Viewport", srGO);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        // 30/10 : une surface transparente capte la molette aussi dans les vides entre les sections (sinon elle se perdait sur le fond).
        if (viewport.GetComponent<Graphic>() == null) viewport.gameObject.AddComponent<Image>().color = Color.clear;

        var content = UIFactory.VBox(viewport, 10, 2, 8, 2, 12, "Content");
        var crt = (RectTransform)content.transform;
        crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(.5f, 1);
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        sr.viewport = viewport; sr.content = crt;
        return content.transform;
    }

    TMP_InputField LabeledInput(Transform parent, string label, string placeholder, string value)
    {
        UIFactory.Text(parent, label, UITheme.Role.Libelle, UITheme.TexteSecondaire);
        var f = UIFactory.Input(parent, placeholder);
        f.text = value ?? "";
        return f;
    }

    // Passe par SaisieNumerique : « . » et « , » y sont interchangeables et les
    // espaces de milliers acceptes (cette copie locale ne gerait que la virgule).
    static float ParseFloat(string s) => SaisieNumerique.Parse(s);

    static ChargeBatiment Clone(ChargeBatiment c) => JsonUtility.FromJson<ChargeBatiment>(JsonUtility.ToJson(c));
    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // Parse une date saisie au format français JJ/MM/AAAA (sans ambiguïté mois/jour).
    static bool TryParseFr(string s, out DateTime d) =>
        DateTime.TryParseExact((s ?? "").Trim(),
            new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
}
