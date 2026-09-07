using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Ajoute (au runtime) des champs de facturation à la fiche locataire.
///
/// General : RIB du locataire (Titulaire / IBAN / BIC), clonés depuis un champ
///           InputAndText existant (rendu natif).
/// Dépôt de garantie : carte dédiée dans la Colone 2 (entre « Loyer » et « Autre »)
///           avec un récap lecture seule (montant · équivalent en mois de loyer ·
///           date de révision) et le bouton « Révision dépôt de garantie » (pop-up).
///           La colonne « Depot de garantie » d'origine (carte « Autre ») est masquée.
/// (Les paramètres LOYER — jour de demande, mois facturés, régularisation — sont
///  dans le pop-up « Révision du loyer » : voir RevisionPanel.)
public class LocataireFacturationFields : MonoBehaviour
{
    InputAndText ribTitulaire, ribIban, ribBic;

    // Récap dépôt de garantie (lecture seule)
    TMP_Text _valMontant, _valMoisEquiv, _valDateRev;

    LocatairePrefab _fiche;
    bool _built;

    // Accent de la carte « Dépôt de garantie » (bleu-canard, distinct des autres sections).
    static readonly Color DepotAccent = HexC("#2A6F82");
    static readonly Color DepotAccentClair = HexC("#E2EFF2");
    // Accent de la carte « Facturation ».
    static readonly Color FactAccent = HexC("#9A5B2E");
    static readonly Color FactAccentClair = HexC("#F3E7D9");
    static Color HexC(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // Parse une date au format français JJ/MM/AAAA (évite l'ambiguïté mois/jour
    // de DateTime.TryParse qui interprète parfois en mois-d'abord façon US).
    static bool TryParseFr(string s, out DateTime d) =>
        DateTime.TryParseExact((s ?? "").Trim(),
            new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    public void EnsureBuilt(LocatairePrefab fiche)
    {
        if (_built) return;
        _built = true;
        _fiche = fiche;

        var src = fiche.emailLocataireTxt;   // champ modèle à cloner
        if (src == null) return;

        // ── General : RIB du locataire ──────────────────────────────────────
        var generalContent = src.transform.parent;
        ribTitulaire = Clone(src, generalContent, "Titulaire (RIB locataire)", "Nom du titulaire", "");
        ribIban      = Clone(src, generalContent, "IBAN locataire", "FR76 …", "");
        ribBic       = Clone(src, generalContent, "BIC locataire", "BNPAFRPP", "");

        // ── Dépôt de garantie : carte dédiée ────────────────────────────────
        BuildDepotCard();

        // ── Facturation : boutons (bas de la colonne gauche) ────────────────
        BuildFacturationButtons();
    }

    // Carte « Facturation » en bas de la colonne gauche : un bouton par type.
    // Loyer actif ; les autres à venir.
    void BuildFacturationButtons()
    {
        if (_fiche.emailLocataireTxt == null) return;
        var generalContent = _fiche.emailLocataireTxt.transform.parent;   // General/Content
        var generalSection = generalContent.parent;                       // section General
        var colone1 = generalSection != null ? generalSection.parent : null;
        if (colone1 == null) return;

        var body = UIFactory.Section(colone1, "Facturation", FactAccent, FactAccentClair);
        var sectionRoot = body.transform.parent;
        UIFactory.LE(sectionRoot.gameObject, flexW: 1);
        sectionRoot.SetAsLastSibling();

        var loyer = UIFactory.Button(body.transform, "Facturer le loyer", UITheme.Primaire, Color.white, 42, 16);
        loyer.onClick.AddListener(() => FactureLoyerPanel.OpenLoyer(_fiche));

        AddSoon(body.transform, "Refacturation");
        AddSoon(body.transform, "Régularisation des charges");
        AddSoon(body.transform, "Révision du dépôt (facture)");
    }

    void AddSoon(Transform parent, string label)
    {
        var b = UIFactory.Button(parent, label + "  ·  à venir", UITheme.Carte, UITheme.TexteSecondaire, 40, 15);
        UIFactory.Border(b.gameObject);
        b.interactable = false;
    }

    // ── Carte dédiée « Dépôt de garantie » ──────────────────────────────────

    void BuildDepotCard()
    {
        if (_fiche.depotDeGarantieTxt == null) return;

        var depotField   = _fiche.depotDeGarantieTxt.transform;  // « Depot de garantie »
        var autreContent = depotField.parent.parent;            // « Autre » / Content
        var autreSection = autreContent.parent;                 // section « Autre »
        var colone2      = autreSection.parent;                 // « Colone 2 »

        // Carte titrée, insérée juste avant « Autre » (donc après « Loyer »).
        var body = UIFactory.Section(colone2, "Dépôt de garantie", DepotAccent, DepotAccentClair);
        // Marge droite renforcée : la colonne de droite déborde ~14px hors de la
        // vue de jeu ; sans ça les valeurs (alignées à droite) touchent le bord.
        body.padding = new RectOffset(body.padding.left, 30, body.padding.top, body.padding.bottom);
        var sectionRoot = body.transform.parent;
        UIFactory.LE(sectionRoot.gameObject, flexW: 1);
        sectionRoot.SetSiblingIndex(autreSection.GetSiblingIndex());

        _valMontant   = DepotRow(body.transform, "Montant du dépôt");
        _valMoisEquiv = DepotRow(body.transform, "Équivalent");
        _valDateRev   = DepotRow(body.transform, "Date de révision");

        var btn = UIFactory.Button(body.transform, "Révision dépôt de garantie",
            UITheme.Primaire, Color.white, 44, 16);
        btn.onClick.AddListener(OpenRevisionPopup);

        // « Autre » ne contenait plus que « Taux de rentabilité » → on le déplace
        // dans « General » (juste avant le bloc RIB) et on retire la carte « Autre ».
        if (_fiche.tauxDeRentabilité != null && _fiche.emailLocataireTxt != null)
        {
            var generalContent = _fiche.emailLocataireTxt.transform.parent;
            var taux = _fiche.tauxDeRentabilité.transform;
            taux.SetParent(generalContent, false);
            if (ribTitulaire != null)
                taux.SetSiblingIndex(ribTitulaire.transform.GetSiblingIndex());
        }
        autreSection.gameObject.SetActive(false);
    }

    // Ligne « label ………… valeur » (valeur à droite, en gras).
    TMP_Text DepotRow(Transform parent, string label)
    {
        var h = UIFactory.HBox(parent, 8, false, "Row");
        UIFactory.LE(h.gameObject, minH: 22);
        var l = UIFactory.Text(h.transform, label, 15, UITheme.TexteSecondaire);
        UIFactory.LE(l.gameObject, flexW: 1);
        return UIFactory.Text(h.transform, "—", 15, UITheme.TextePrincipal, true,
            TextAlignmentOptions.Right);
    }

    void RefreshDepotRecap(Locataire loc)
    {
        if (_valMontant == null || loc == null) return;
        _valMontant.text = $"{loc.depotDeGarantie:N2} €";

        // Équivalent en périodes de loyer (hors charges), sur la même base que le
        // calcul : TTC si le locataire est soumis à TVA, HT sinon. La période suit
        // la périodicité du bail (mensuel / trimestriel / bi-annuel / annuel).
        float perPeriode = loc.loyerAnnuel / LoyerSummaryUI.NbPeriodes(loc.periodiciteLoyer);
        float baseVal = loc.depotSurTTC ? perPeriode * 1.2f : perPeriode;
        _valMoisEquiv.text = baseVal > 0f
            ? $"≈ {loc.depotDeGarantie / baseVal:0.#} périodes de loyer {(loc.depotSurTTC ? "TTC" : "HT")}" : "—";

        _valDateRev.text = DateTime.TryParse(loc.dateRevisionDepotISO, out var dr)
            ? dr.ToString("dd/MM/yyyy") : "—";
    }

    // ── Clone d'un champ InputAndText (rendu natif) ─────────────────────────

    InputAndText Clone(InputAndText src, Transform parent, string label, string placeholder, string unit)
    {
        var go = Instantiate(src.gameObject, parent);
        go.name = label;
        var iat = go.GetComponent<InputAndText>();

        var titre = go.transform.Find("title")?.GetComponent<TMP_Text>();
        if (titre != null) titre.text = label;

        var q = go.transform.Find("quantité")?.GetComponent<TMP_Text>();
        if (q != null) q.text = unit;

        if (iat.inputModify != null && iat.inputModify.placeholder != null)
        {
            var ph = iat.inputModify.placeholder.GetComponent<TMP_Text>();
            if (ph != null) ph.text = placeholder;
        }
        iat.ApplyValue("");
        return iat;
    }

    // ── Révision du dépôt de garantie (pop-up) ──────────────────────────────

    void OpenRevisionPopup()
    {
        if (_fiche == null) return;
        var loc = _fiche.GetLocataire();
        if (loc == null) return;
        // Loyer par période (selon la périodicité du bail), hors charges, HT.
        float periodeHT = loc.loyerAnnuel / LoyerSummaryUI.NbPeriodes(loc.periodiciteLoyer);

        var canvas = _fiche.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var scrim = UIFactory.Rect("RevScrim", canvas.rootCanvas.transform);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        scrim.SetAsLastSibling();

        var cardImg = UIFactory.Panel("RevCard", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(540, 100);
        var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10; v.padding = new RectOffset(18, 18, 16, 16);
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var csf = cardImg.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UIFactory.Text(v.transform, "Révision du dépôt de garantie", 22, UITheme.TextePrincipal, true);

        UIFactory.Text(v.transform, "Date de révision", 15, UITheme.TexteSecondaire);
        var dateInput = UIFactory.Input(v.transform, "JJ / MM / AAAA");
        dateInput.text = DateTime.TryParse(loc.dateRevisionDepotISO, out var drx)
            ? drx.ToString("dd/MM/yyyy") : DateTime.Today.ToString("dd/MM/yyyy");

        // Soumis à TVA → loyer TTC ; sinon → loyer HT (mémorisé sur le locataire).
        var ttcToggle = UIFactory.Toggle(v.transform, "Locataire soumis à la TVA (loyer TTC)", loc.depotSurTTC);

        UIFactory.Text(v.transform, "Loyer par période hors charges (prérempli)", 15, UITheme.TexteSecondaire);
        var loyerLine = UIFactory.Text(v.transform, "", 20, UITheme.TextePrincipal, true);
        Func<float> currentPeriode = () => ttcToggle.isOn ? periodeHT * 1.2f : periodeHT;
        Action refreshLoyer = () =>
            loyerLine.text = $"{currentPeriode():0.00} € / période {(ttcToggle.isOn ? "TTC" : "HT")}";
        ttcToggle.onValueChanged.AddListener(_ => refreshLoyer());
        refreshLoyer();

        UIFactory.Text(v.transform, "Nombre de périodes", 15, UITheme.TexteSecondaire);
        var moisInput = UIFactory.Input(v.transform, "ex. 3");
        moisInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        if (currentPeriode() > 0f && loc.depotDeGarantie > 0f)
            moisInput.text = Mathf.RoundToInt(loc.depotDeGarantie / currentPeriode()).ToString();

        // Ancien dépôt (fixe) + nouveau dépôt possible (recalculé en direct).
        UIFactory.Text(v.transform, $"Ancien dépôt : {loc.depotDeGarantie:0.00} €", 16, UITheme.TexteSecondaire);
        var nouveauLine = UIFactory.Text(v.transform, "", 20, UITheme.Primaire, true);
        var hintLine = UIFactory.Text(v.transform, "", 14, UITheme.Alerte);

        var actions = UIFactory.HBox(v.transform, 10);
        UIFactory.LE(actions.gameObject, minH: 46);

        var cancel = UIFactory.Button(actions.transform, "Fermer", UITheme.Carte, UITheme.TextePrincipal, 44, 18);
        UIFactory.Border(cancel.gameObject); UIFactory.LE(cancel.gameObject, flexW: 1);
        cancel.onClick.AddListener(() => Destroy(scrim.gameObject));

        var reviser = UIFactory.Button(actions.transform, "Réviser", UITheme.Primaire, Color.white, 44, 18);
        UIFactory.LE(reviser.gameObject, flexW: 1);

        // Recalcul auto du nouveau dépôt + état du bouton : la révision n'est
        // possible qu'à partir de la date de révision (à/après ce jour).
        Action recompute = () =>
        {
            refreshLoyer();
            int.TryParse(moisInput.text, out int nb);
            float nouveau = currentPeriode() * Mathf.Max(0, nb);
            nouveauLine.text = $"Nouveau dépôt : {nouveau:0.00} €";

            bool dateOk = TryParseFr(dateInput.text, out var dr);
            bool due = dateOk && DateTime.Today.Date >= dr.Date;
            reviser.interactable = due && nb > 0;
            hintLine.text = !dateOk ? "Date de révision invalide."
                : !due ? $"Révisable à partir du {dr:dd/MM/yyyy}."
                : "";
        };
        moisInput.onValueChanged.AddListener(_ => recompute());
        ttcToggle.onValueChanged.AddListener(_ => recompute());
        dateInput.onValueChanged.AddListener(_ => recompute());
        recompute();

        reviser.onClick.AddListener(() =>
        {
            int.TryParse(moisInput.text, out int nb);
            if (nb <= 0) return;
            if (!TryParseFr(dateInput.text, out var dr) || DateTime.Today.Date < dr.Date) return;

            loc.depotDeGarantie = currentPeriode() * nb;
            loc.depotSurTTC = ttcToggle.isOn;
            // La prochaine révision est repoussée d'un an.
            var next = dr.AddYears(1);
            loc.dateRevisionDepotISO = next.ToString("yyyy-MM-dd");
            RefreshDepotRecap(loc);
            _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
            UndoToast.Instance?.ShowInfo(
                $"Dépôt révisé : {loc.depotDeGarantie:0.00} € — prochaine révision {next:dd/MM/yyyy}");
            Destroy(scrim.gameObject);
        });
    }

    // ── Load / Modify / Save ────────────────────────────────────────────────

    public void Load(Locataire loc)
    {
        ribTitulaire?.ApplySave(loc.ribLocataireTitulaire ?? "");
        ribIban?.ApplySave(loc.ribLocataireIban ?? "");
        ribBic?.ApplySave(loc.ribLocataireBic ?? "");
        RefreshDepotRecap(loc);
    }

    public void Modify()
    {
        ribTitulaire?.Modify(); ribIban?.Modify(); ribBic?.Modify();
        // Dépôt de garantie : lecture seule (édité via le pop-up de révision).
    }

    public void Save(Locataire loc)
    {
        if (ribTitulaire != null) loc.ribLocataireTitulaire = ribTitulaire.GetNewSave();
        if (ribIban != null) loc.ribLocataireIban = ribIban.GetNewSave();
        if (ribBic != null) loc.ribLocataireBic = ribBic.GetNewSave();
    }
}
