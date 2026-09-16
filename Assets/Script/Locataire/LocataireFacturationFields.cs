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
    GameObject _depotBadge;   // pastille « Révision à faire » (bandeau Dépôt)
    GameObject _pastilleLoyer, _pastilleRegul, _pastilleDepot;   // rappels sur les boutons d'action

    LocatairePrefab _fiche;
    bool _built;

    // Zone d'alertes d'échéance en haut de la carte Facturation.
    Transform _alertesBox;

    // Cartes créées par ce script (pour la réorganisation des colonnes).
    Transform _depotSection, _facturationSection, _facturationBody;
    // Coquille de section de scène (« Autre ») réutilisée pour cloner Dépôt / Facturation / Suivi.
    Transform _sectionTemplate;

    // ── Colorimétrie « groupée par fonction » ───────────────────────────────
    // 🟢 Identité/contrat : General, Bail   🟡 Argent : Loyer, Dépôt, Facturation,
    // Suivi   🔵 Notes : Commentaire, Objectif.
    static readonly Color IdentityAccent = HexC("#0F6E56"); // vert primaire
    static readonly Color IdentityClair  = HexC("#D6ECE3");
    static readonly Color MoneyAccent    = HexC("#A9741C"); // ambre
    static readonly Color MoneyClair     = HexC("#F4E7CD");
    static readonly Color MoneyTileClair = HexC("#FBF5E8"); // éléments plus clairs que le bandeau
    static readonly Color NotesAccent    = HexC("#5C6E85"); // gris-bleu doux
    static readonly Color NotesClair     = HexC("#E7ECF2");

    // Accent de l'action « Révision du dépôt (facture) » (bouton CTA, distinct).
    static readonly Color DepotAccent = HexC("#2A6F82");
    static readonly Color FactAccent = HexC("#9A5B2E");
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

        // ── General : RIB du locataire (compact : Titulaire + BIC sur une ligne, IBAN dessous) ──
        var generalContent = src.transform.parent;
        var ribRow = UIFactory.HBox(generalContent, 10, false, "RibRow");
        ribRow.childControlWidth = true; ribRow.childControlHeight = true;
        ribRow.childForceExpandWidth = true; ribRow.childForceExpandHeight = false;
        ribTitulaire = Clone(src, ribRow.transform, "Titulaire (RIB)", "Nom du titulaire", "");
        ribBic       = Clone(src, ribRow.transform, "BIC", "BNPAFRPP", "");
        UIFactory.LE(ribTitulaire.gameObject, flexW: 1, minW: 0);
        UIFactory.LE(ribBic.gameObject, flexW: 1, minW: 0);
        ribIban      = Clone(src, generalContent, "IBAN (RIB locataire)", "FR76 …", "");

        // ── Dépôt de garantie : carte dédiée ────────────────────────────────
        BuildDepotCard();

        // ── Facturation : boutons (bas de la colonne gauche) ────────────────
        BuildFacturationButtons();

        // ── Suivi de facturation : bandeau pleine largeur en bas de la fiche ─
        BuildSuiviBand();

        // ── Réorganisation des colonnes (croquis utilisatrice) ──────────────
        ReorganizeColumns();

        // ── Email + Téléphone sur une même ligne ────────────────────────────
        BuildEmailTelRow();

        // ── Uniformise la hauteur des bandeaux de titre (réf. = General = 58) ─
        NormalizeSectionHeaders();

        // ── Colorimétrie groupée par fonction (sections de scène) ───────────
        ApplyColorScheme();
    }

    // Recolore les sections de scène par famille (les sections clonées Dépôt/
    // Facturation/Suivi sont déjà colorées à la construction via CloneSection).
    void ApplyColorScheme()
    {
        if (_depotSection == null || _depotSection.parent == null) return;
        var rowColonnes = _depotSection.parent.parent;   // colonne -> RowColonnes
        if (rowColonnes == null) return;
        foreach (Transform col in rowColonnes)
        {
            if (col.GetComponent<VerticalLayoutGroup>() == null) continue;
            foreach (Transform sec in col)
            {
                switch (sec.name.Trim())
                {
                    case "General":
                    case "Bail":
                        RecolorSceneSection(sec, IdentityAccent, IdentityClair); break;
                    case "Loyer":
                        RecolorSceneSection(sec, MoneyAccent, MoneyClair); break;
                    case "Commentaire":
                    case "Objectif":
                        RecolorSceneSection(sec, NotesAccent, NotesClair); break;
                }
            }
        }
    }

    // Recolore une section de scène : liseré (image racine) + bande de titre +
    // icône + texte du titre.
    static void RecolorSceneSection(Transform section, Color accent, Color clair)
    {
        var rootImg = section.GetComponent<Image>();
        if (rootImg != null) rootImg.color = accent;
        var titre = section.Find("titre");
        if (titre == null) return;
        var bandImg = titre.GetComponent<Image>();
        if (bandImg != null) bandImg.color = clair;
        var icon = titre.Find("Icon");
        if (icon != null) { var ii = icon.GetComponent<Image>(); if (ii != null) ii.color = accent; }
        foreach (Transform c in titre)
        {
            var t = c.GetComponent<TMP_Text>();
            if (t != null) { t.color = accent; break; }
        }
    }

    // Aligne la hauteur de MES bandeaux (« TitleBand » = Dépôt / Facturation /
    // Suivi) sur celle des sections de scène « General » (mesurée = 58).
    // On NE touche PAS aux bandeaux de scène « titre » : ils sont dimensionnés
    // par leur propre ContentSizeFitter et déjà à 58 ; y toucher les casse.
    const float HeaderH = 58f;
    const float IconSize = 34f;
    void NormalizeSectionHeaders()
    {
        foreach (var rt in GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.name == "TitleBand")
            {
                var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
                le.minHeight = HeaderH; le.preferredHeight = HeaderH;
            }
            else if (rt.name == "titre")
            {
                // Loyer : bandeau à 48 (contenu court). Son ContentSizeFitter lit le
                // preferred → un LayoutElement (priorité > HLG) le force à 58.
                if (rt.parent != null && rt.parent.name.Trim() == "Loyer")
                {
                    var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
                    le.minHeight = HeaderH; le.preferredHeight = HeaderH;
                }
                // Icône de section agrandie (mieux visible).
                var icon = rt.Find("Icon");
                if (icon != null) EnlargeIcon(icon, IconSize);
            }
        }
    }

    // Force la taille d'une icône (via LayoutElement car le HLG du bandeau contrôle
    // la taille de ses enfants) + RectTransform.
    static void EnlargeIcon(Transform icon, float size)
    {
        var le = icon.GetComponent<LayoutElement>() ?? icon.gameObject.AddComponent<LayoutElement>();
        le.minWidth = size; le.preferredWidth = size;
        le.minHeight = size; le.preferredHeight = size;
        ((RectTransform)icon).sizeDelta = new Vector2(size, size);
    }

    // Sprite chargé en mémoire, cherché par nom (les AssetDatabase ne sont pas
    // dispo au runtime ; on récupère un sprite déjà référencé ailleurs dans l'app).
    static Sprite FindSpriteByName(string spriteName)
    {
        foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            if (s != null && s.name == spriteName) return s;
        return null;
    }

    // Regroupe Email et Téléphone sur une même ligne (2 colonnes).
    void BuildEmailTelRow()
    {
        var email = _fiche.emailLocataireTxt;
        var tel = _fiche.telephoneLocataireTxt;
        if (email == null || tel == null) return;
        var generalContent = email.transform.parent;   // encore General/Content ici
        int idx = email.transform.GetSiblingIndex();
        var row = UIFactory.HBox(generalContent, 10, false, "EmailTelRow");
        row.childControlWidth = true; row.childControlHeight = true;
        row.childForceExpandWidth = true; row.childForceExpandHeight = false;
        row.transform.SetSiblingIndex(idx);
        email.transform.SetParent(row.transform, false);
        tel.transform.SetParent(row.transform, false);
        UIFactory.LE(email.gameObject, flexW: 1, minW: 0);
        UIFactory.LE(tel.gameObject, flexW: 1, minW: 0);
    }

    // Gauche : General · Loyer · Dépôt ; Droite : Bail · Commentaire · Objectif · Facturation.
    void ReorganizeColumns()
    {
        if (_fiche.emailLocataireTxt == null) return;
        var generalSection = _fiche.emailLocataireTxt.transform.parent.parent;   // section General
        var colone1 = generalSection.parent;
        var rowColonnes = colone1 != null ? colone1.parent : null;
        if (rowColonnes == null) return;

        Transform colone2 = null;
        foreach (Transform c in rowColonnes)
            if (c != colone1 && c.GetComponent<VerticalLayoutGroup>() != null) { colone2 = c; break; }
        if (colone2 == null) return;

        var bail    = colone1.Find("Bail");
        var comment = colone1.Find("Commentaire");
        var loyer   = colone2.Find("Loyer");
        var objectif = colone2.Find("Objectif ") ?? colone2.Find("Objectif");

        // Colonne gauche : General, Loyer, Dépôt de garantie.
        if (loyer != null) loyer.SetParent(colone1, false);
        if (_depotSection != null) _depotSection.SetParent(colone1, false);
        generalSection.SetSiblingIndex(0);
        if (loyer != null) loyer.SetSiblingIndex(1);
        if (_depotSection != null) _depotSection.SetSiblingIndex(2);

        // Colonne droite : Bail, Commentaire, Objectif, Facturation.
        if (bail != null) bail.SetParent(colone2, false);
        if (comment != null) comment.SetParent(colone2, false);
        if (_facturationSection != null) _facturationSection.SetParent(colone2, false);
        int idx = 0;
        if (bail != null) bail.SetSiblingIndex(idx++);
        if (comment != null) comment.SetSiblingIndex(idx++);
        if (objectif != null) objectif.SetSiblingIndex(idx++);
        if (_facturationSection != null) _facturationSection.SetSiblingIndex(idx++);

        EnlargeCommentaire();
    }

    // Agrandit la zone de commentaire : le champ a un enfant « Content » (à hauteur
    // fixe) qui contient la zone de texte (scroll) → on agrandit Content + la racine.
    void EnlargeCommentaire()
    {
        var c = _fiche.Commentaire;
        if (c == null) return;
        var content = c.transform.Find("Content");
        if (content != null)
        {
            var cle = content.GetComponent<LayoutElement>() ?? content.gameObject.AddComponent<LayoutElement>();
            cle.minHeight = 170; cle.preferredHeight = 170;
        }
        var rootLE = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
        rootLE.minHeight = 245; rootLE.preferredHeight = 245;
    }

    // Bandeau « Suivi de facturation » sous les 2 colonnes (option A).
    LocataireSuiviInline _suiviInline;
    void BuildSuiviBand()
    {
        if (_fiche.emailLocataireTxt == null) return;
        var generalContent = _fiche.emailLocataireTxt.transform.parent;   // General/Content
        var colone1     = generalContent.parent.parent;                   // Colone 1
        var rowColonnes = colone1 != null ? colone1.parent : null;        // RowColonnes
        var ficheRoot   = rowColonnes != null ? rowColonnes.parent : null;
        if (ficheRoot == null) return;

        // Coquille = clone de section de scène (liseré + bande + icône sec_chart) ;
        // LocataireSuiviInline peuple le corps (tableau) + le bandeau (sélecteur d'année).
        var body = CloneSection(ficheRoot, "Suivi de facturation", MoneyAccent, MoneyClair, "sec_chart", out var sectionRoot);
        if (body == null) return;
        sectionRoot.SetSiblingIndex(rowColonnes.GetSiblingIndex() + 1);
        var bodyVlg = body.GetComponent<VerticalLayoutGroup>();
        if (bodyVlg != null) { bodyVlg.padding = new RectOffset(0, 0, 0, 0); bodyVlg.spacing = 0; }
        var titre = sectionRoot.Find("titre");
        _suiviInline = sectionRoot.gameObject.AddComponent<LocataireSuiviInline>();
        _suiviInline.Setup(_fiche, body, titre);
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

        // Coquille = clone de section de scène (liseré + bande + icône sec_file).
        var body = CloneSection(colone1, "Facturation", MoneyAccent, MoneyClair, "sec_file", out var sectionRoot);
        if (body == null) return;
        _facturationSection = sectionRoot;
        _facturationBody = body;
        sectionRoot.SetAsLastSibling();
        var bodyVlg = body.GetComponent<VerticalLayoutGroup>();
        if (bodyVlg != null) { bodyVlg.padding = new RectOffset(14, 14, 8, 12); bodyVlg.spacing = 8; }

        // Alertes d'échéance (en haut de la carte).
        var alertesWrap = UIFactory.VBox(body, 6, 0, 0, 0, 0, "AlertesBox");
        _alertesBox = alertesWrap.transform;

        // (Le suivi de facturation est désormais affiché en bandeau pleine largeur
        //  en bas de la fiche — voir BuildSuiviBand.) Les 4 actions en grille 2×2.
        var row1 = UIFactory.HBox(body, 8, false, "FactRow1");
        row1.childControlWidth = true; row1.childForceExpandWidth = true; row1.childControlHeight = true;
        var bLoyer = GridBtn(row1.transform, "Facturer le loyer", UITheme.Primaire, () => FactureLoyerPanel.OpenLoyer(_fiche));
        var bRegul = GridBtn(row1.transform, "Régularisation des charges", FactAccent, () => FactureRegulPanel.OpenRegul(_fiche));

        var row2 = UIFactory.HBox(body, 8, false, "FactRow2");
        row2.childControlWidth = true; row2.childForceExpandWidth = true; row2.childControlHeight = true;
        GridBtn(row2.transform, "Refacturation d'une charge", HexC("#7A5AA6"), () => FactureRefacPanel.OpenRefac(_fiche));
        var bDepot = GridBtn(row2.transform, "Révision du dépôt (facture)", DepotAccent, () => FactureDepotPanel.OpenDepot(_fiche));

        // Pastilles de rappel (coin haut-droit) affichées quand l'échéance est due.
        _pastilleLoyer = AddReminderDot(bLoyer);
        _pastilleRegul = AddReminderDot(bRegul);
        _pastilleDepot = AddReminderDot(bDepot);

        RefreshAlertes(_fiche != null ? _fiche.GetLocataire() : null);
    }

    // Bouton d'une grille : demi-largeur (flexible), hauteur fixe.
    Button GridBtn(Transform row, string label, Color bg, Action onClick)
    {
        var b = UIFactory.Button(row, label, bg, Color.white, 46, 15);
        UIFactory.LE(b.gameObject, flexW: 1, minW: 0, minH: 46, prefH: 46);
        b.onClick.AddListener(() => onClick());
        return b;
    }

    // Pastille de rappel (petit disque « ! » rouge) en haut-droite d'un bouton ; masquée par défaut.
    GameObject AddReminderDot(Button b)
    {
        if (b == null) return null;
        var dot = UIFactory.Panel("Rappel", b.transform, UITheme.Alerte);
        var le = dot.gameObject.AddComponent<LayoutElement>(); le.ignoreLayout = true;
        var t = UIFactory.Text(dot.transform, "!", 13, Color.white, true, TextAlignmentOptions.Center);
        var tr = (RectTransform)t.transform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var rt = (RectTransform)dot.transform;
        rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(20, 20);
        rt.anchoredPosition = new Vector2(-6, -6);
        dot.gameObject.SetActive(false);
        return dot.gameObject;
    }

    // Affiche les alertes d'échéance (loyer / régularisation / dépôt) en haut de la
    // carte + les pastilles de rappel sur les boutons d'action correspondants.
    public void RefreshAlertes(Locataire loc)
    {
        if (_alertesBox != null)
            foreach (Transform c in _alertesBox) UnityEngine.Object.Destroy(c.gameObject);

        bool loyerDue = false, regulDue = false, depotDue = false;
        if (loc != null)
            foreach (var a in FacturationAlertes.Pour(loc))
            {
                if (a.type == FacturationAlertes.AlerteType.Loyer) loyerDue = true;
                else if (a.type == FacturationAlertes.AlerteType.Regul) regulDue = true;
                else if (a.type == FacturationAlertes.AlerteType.Depot) depotDue = true;

                if (_alertesBox == null) continue;
                bool urgent = a.niveau == FacturationAlertes.Niveau.Urgent;
                var panel = UIFactory.Panel("Alerte", _alertesBox, urgent ? HexC("#F6DED6") : HexC("#FBEFD6"));
                UIFactory.Border(panel.gameObject, urgent ? HexC("#D85A30") : HexC("#C79A3E"));
                var hl = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.padding = new RectOffset(10, 10, 6, 6);
                hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true;
                panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                UIFactory.Text(panel.transform, a.message, 14, urgent ? HexC("#B23A12") : HexC("#8A6D1E"), true);
            }

        if (_pastilleLoyer != null) _pastilleLoyer.SetActive(loyerDue);
        if (_pastilleRegul != null) _pastilleRegul.SetActive(regulDue);
        if (_pastilleDepot != null) _pastilleDepot.SetActive(depotDue);
    }

    void AddSoon(Transform parent, string label)
    {
        var b = UIFactory.Button(parent, label + "  ·  à venir", UITheme.Carte, UITheme.TexteSecondaire, 40, 15);
        UIFactory.Border(b.gameObject);
        b.interactable = false;
    }

    // ── Clone de coquille de section de scène (liseré + CardBg + bande + icône) ──
    // Renvoie le Content vidé ; `sectionRoot` = la carte clonée (recolorée/retitrée).
    // Utilisé pour Dépôt, Facturation et Suivi afin d'avoir EXACTEMENT le visuel
    // des sections de scène (General/Bail/…). Requiert _sectionTemplate.
    Transform CloneSection(Transform parent, string title, Color accent, Color accentClair,
                           string iconName, out Transform sectionRoot)
    {
        sectionRoot = null;
        if (_sectionTemplate == null) return null;

        var clone = Instantiate(_sectionTemplate.gameObject, parent);
        clone.SetActive(true);
        clone.name = "Sec_" + title;
        sectionRoot = clone.transform;
        UIFactory.LE(clone, flexW: 1);

        var rootImg = clone.GetComponent<Image>();
        if (rootImg != null) rootImg.color = accent;              // liseré = image racine

        var titre = clone.transform.Find("titre");
        if (titre != null)
        {
            var bandImg = titre.GetComponent<Image>();
            if (bandImg != null) bandImg.color = accentClair;    // bande = tint clair
            var icon = titre.Find("Icon");
            if (icon != null)
            {
                var ii = icon.GetComponent<Image>();
                if (ii != null)
                {
                    ii.color = accent;
                    if (!string.IsNullOrEmpty(iconName)) { var sp = FindSpriteByName(iconName); if (sp != null) ii.sprite = sp; }
                }
            }
            var tog = titre.Find("Toggle");
            if (tog != null) tog.gameObject.SetActive(false);
            foreach (Transform c in titre)
            {
                var t = c.GetComponent<TMP_Text>();
                if (t != null) { t.text = title; t.color = accent; break; }
            }
            // Masquer le Toggle raccourcit le contenu → on force la hauteur du bandeau
            // à 58 (= sections de scène) via LayoutElement (prioritaire sur le HLG/CSF).
            var tle = titre.GetComponent<LayoutElement>() ?? titre.gameObject.AddComponent<LayoutElement>();
            tle.minHeight = HeaderH; tle.preferredHeight = HeaderH;
        }

        var body = clone.transform.Find("Content") ?? clone.transform;
        for (int i = body.childCount - 1; i >= 0; i--) DestroyImmediate(body.GetChild(i).gameObject);
        return body;
    }

    // ── Carte dédiée « Dépôt de garantie » ──────────────────────────────────

    void BuildDepotCard()
    {
        if (_fiche.depotDeGarantieTxt == null) return;

        var depotField   = _fiche.depotDeGarantieTxt.transform;  // « Depot de garantie »
        var autreContent = depotField.parent.parent;            // « Autre » / Content
        var autreSection = autreContent.parent;                 // section « Autre »
        var colone2      = autreSection.parent;                 // « Colone 2 »

        // « Autre » ne contenait plus que « Taux de rentabilité » → on le déplace
        // d'abord dans « General », sur la ligne « Lot | Taille » (3 colonnes).
        if (_fiche.tauxDeRentabilité != null && _fiche.lotBatimentTxt != null)
        {
            var lotRow = _fiche.lotBatimentTxt.transform.parent;   // ligne « Lot | Taille »
            var taux = _fiche.tauxDeRentabilité.transform;
            taux.SetParent(lotRow, false);
            taux.SetAsLastSibling();
            var hlg = lotRow.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) { hlg.childControlWidth = true; hlg.childForceExpandWidth = true; }
            UIFactory.LE(_fiche.lotBatimentTxt.gameObject, flexW: 1, minW: 0);
            if (_fiche.tailleLotTxt != null) UIFactory.LE(_fiche.tailleLotTxt.gameObject, flexW: 1, minW: 0);
            UIFactory.LE(taux.gameObject, flexW: 1, minW: 0);
        }

        // Coquille visuelle = CLONE de la section de scène « Autre » (liseré + CardBg
        // + bande arrondie + icône sec_coins). L'originale reste masquée (elle porte
        // depotDeGarantieTxt pour la sauvegarde).
        _sectionTemplate = autreSection;
        var body = CloneSection(colone2, "Dépôt de garantie", MoneyAccent, MoneyClair, "sec_coins", out var sectionRoot);
        if (body == null) return;
        _depotSection = sectionRoot;
        sectionRoot.SetSiblingIndex(autreSection.GetSiblingIndex());
        var titre = sectionRoot.Find("titre");
        var bodyVlg = body.GetComponent<VerticalLayoutGroup>();
        if (bodyVlg != null) { bodyVlg.padding = new RectOffset(12, 18, 8, 10); bodyVlg.spacing = 8; }

        // Deux tuiles KPI de taille identique : Montant ⟷ Équivalent. Date dessous.
        var tiles = UIFactory.HBox(body, 10, false, "DepotTiles");
        tiles.childControlWidth = true; tiles.childForceExpandWidth = true;
        tiles.childControlHeight = true; tiles.childForceExpandHeight = false;
        tiles.childAlignment = TextAnchor.UpperLeft;
        _valMontant   = DepotTile(tiles.transform, "Montant du dépôt", 24);
        _valMoisEquiv = DepotTile(tiles.transform, "Équivalent", 24);

        _valDateRev = DepotRow(body, "Date de révision");

        // Bouton « Réviser » dans la bande de titre (clone du bouton Loyer → même forme).
        var srcBtn = _fiche.loyerSummary != null ? _fiche.loyerSummary.btnOuvrirRevision : null;
        if (titre != null && srcBtn != null)
        {
            var go = Instantiate(srcBtn.gameObject, titre);
            go.name = "BtnReviserDepot";
            go.SetActive(true);
            var b = go.GetComponent<UnityEngine.UI.Button>();
            if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(OpenRevisionPopup); }
            var lbl = go.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) { lbl.text = "Réviser"; lbl.color = Color.white; }
            var img = go.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = MoneyAccent;
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1, 0.5f); rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(110, 30);
            rt.anchoredPosition = new Vector2(-12, 0);
        }

        // Pastille « Révision à faire » dans le bandeau (à gauche du bouton), affichée
        // quand la révision du dépôt est due (comme le badge de la carte Loyer).
        if (titre != null)
        {
            var badge = UIFactory.Panel("BadgeDepotRevision", titre, HexC("#FBEFD6"));
            UIFactory.Border(badge.gameObject, HexC("#C79A3E"));
            var bl = badge.gameObject.AddComponent<LayoutElement>(); bl.ignoreLayout = true;
            var bt = UIFactory.Text(badge.transform, "Révision à faire", 12, HexC("#8A6D1E"), true, TextAlignmentOptions.Center);
            var btr = (RectTransform)bt.transform;
            btr.anchorMin = Vector2.zero; btr.anchorMax = Vector2.one; btr.offsetMin = new Vector2(10, 0); btr.offsetMax = new Vector2(-10, 0);
            var br = (RectTransform)badge.transform;
            br.anchorMin = new Vector2(1, 0.5f); br.anchorMax = new Vector2(1, 0.5f); br.pivot = new Vector2(1, 0.5f);
            br.sizeDelta = new Vector2(128, 26);
            br.anchoredPosition = new Vector2(-130, 0);   // à gauche du bouton « Réviser »
            _depotBadge = badge.gameObject;
            _depotBadge.SetActive(false);
        }

        autreSection.gameObject.SetActive(false);
    }

    // Révision du dépôt due : dépôt > 0, date passée, et pas déjà traitée cette année-là.
    static bool RevisionDepotDue(Locataire loc)
    {
        if (loc == null || loc.depotDeGarantie <= 0f) return false;
        if (!DateTime.TryParse(loc.dateRevisionDepotISO, out var d)) return false;
        if (FacturationSuivi.DejaTraite(loc, "depot-" + d.Year)) return false;
        return DateTime.Today >= d;
    }

    // Ligne « label ………… valeur » (valeur à droite, en gras).
    TMP_Text DepotRow(Transform parent, string label)
    {
        var h = UIFactory.HBox(parent, 8, false, "Row");
        UIFactory.LE(h.gameObject, minH: 24);
        var l = UIFactory.Text(h.transform, label, 17, UITheme.TexteSecondaire);
        UIFactory.LE(l.gameObject, flexW: 1);
        return UIFactory.Text(h.transform, "—", 16, UITheme.TextePrincipal, true,
            TextAlignmentOptions.Right);
    }

    // Tuile KPI dépôt (teintée) : petit libellé + grande valeur (+ unité optionnelle).
    // La ligne d'unité est toujours présente (nbsp par défaut) → hauteur homogène.
    TMP_Text DepotTile(Transform parent, string label, float valSize)
    {
        var card = UIFactory.Panel("Tile", parent, MoneyTileClair);
        UIFactory.Border(card.gameObject);
        var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 10, 10); v.spacing = 1;
        v.childControlWidth = true; v.childForceExpandWidth = true;
        v.childControlHeight = true; v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.MiddleLeft;
        UIFactory.LE(card.gameObject, flexW: 1, minW: 0);
        UIFactory.Text(card.transform, label, 17, UITheme.TexteSecondaire);
        var val = UIFactory.Text(card.transform, "—", valSize, UITheme.TextePrincipal, true);
        val.enableWordWrapping = false; val.overflowMode = TextOverflowModes.Ellipsis;
        return val;
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
        if (baseVal > 0f)
        {
            float nb = loc.depotDeGarantie / baseVal;
            string per = nb >= 2f ? "périodes" : "période";
            string suf = loc.depotSurTTC ? "TTC" : "HT";
            // Chiffre en gros + descripteur petit et gris sur la même ligne (2 lignes au total,
            // même épaisseur que la tuile « Loyer annuel »).
            _valMoisEquiv.text = $"≈ {nb:0.#} <size=71%><color=#888780>{per} de loyer {suf}</color></size>";
        }
        else
        {
            _valMoisEquiv.text = "—";
        }

        _valDateRev.text = DateTime.TryParse(loc.dateRevisionDepotISO, out var dr)
            ? dr.ToString("dd/MM/yyyy") : "—";

        bool due = RevisionDepotDue(loc);
        _valDateRev.color = due ? HexC("#D85A30") : UITheme.TextePrincipal;
        if (_depotBadge != null) _depotBadge.SetActive(due);
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
        v.spacing = 0; v.padding = new RectOffset(0, 0, 0, 0);
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var csf = cardImg.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // En-tête : bandeau PLEIN pleine largeur en tête (comme la modale « Révision
        // du loyer ») — fond ambre, icône + titre en blanc, coins arrondis en haut.
        var header = UIFactory.Panel("Header", v.transform, MoneyAccent);
        var hBg = header.GetComponent<Image>();
        var topSprite = FindSpriteByName("RoundedTop");
        if (hBg != null && topSprite != null) hBg.sprite = topSprite;
        UIFactory.LE(header.gameObject, minH: 52, prefH: 52, flexH: 0);
        var hh = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        hh.padding = new RectOffset(16, 16, 4, 4); hh.spacing = 8;
        hh.childControlWidth = true; hh.childControlHeight = true;
        hh.childForceExpandWidth = false; hh.childForceExpandHeight = true;
        hh.childAlignment = TextAnchor.MiddleLeft;
        var hIcon = UIFactory.Rect("Icon", header.transform);
        var hImg = hIcon.gameObject.AddComponent<Image>();
        var hSprite = FindSpriteByName("sec_coins");
        if (hSprite != null) hImg.sprite = hSprite;
        hImg.color = Color.white; hImg.preserveAspect = true; hImg.raycastTarget = false;
        UIFactory.LE(hIcon.gameObject, prefW: 26, minW: 26, prefH: 26, minH: 26, flexW: 0);
        var hTitle = UIFactory.Text(header.transform, "Révision du dépôt de garantie", 20, Color.white, true);
        UIFactory.LE(hTitle.gameObject, flexW: 1);

        // Corps (formulaire) sur fond crème, encadré.
        var body = UIFactory.VBox(v.transform, 10, 18, 18, 14, 16, "Body");

        UIFactory.Text(body.transform, "Date de révision", 15, UITheme.TexteSecondaire);
        var dateInput = UIFactory.Input(body.transform, "JJ / MM / AAAA");
        dateInput.text = DateTime.TryParse(loc.dateRevisionDepotISO, out var drx)
            ? drx.ToString("dd/MM/yyyy") : DateTime.Today.ToString("dd/MM/yyyy");

        // Soumis à TVA → loyer TTC ; sinon → loyer HT (mémorisé sur le locataire).
        var ttcToggle = UIFactory.Toggle(body.transform, "Locataire soumis à la TVA (loyer TTC)", loc.depotSurTTC);

        UIFactory.Text(body.transform, "Loyer par période hors charges (prérempli)", 15, UITheme.TexteSecondaire);
        var loyerLine = UIFactory.Text(body.transform, "", 20, UITheme.TextePrincipal, true);
        Func<float> currentPeriode = () => ttcToggle.isOn ? periodeHT * 1.2f : periodeHT;
        Action refreshLoyer = () =>
            loyerLine.text = $"{currentPeriode():0.00} € / période {(ttcToggle.isOn ? "TTC" : "HT")}";
        ttcToggle.onValueChanged.AddListener(_ => refreshLoyer());
        refreshLoyer();

        UIFactory.Text(body.transform, "Nombre de périodes", 15, UITheme.TexteSecondaire);
        var moisInput = UIFactory.Input(body.transform, "ex. 3");
        moisInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        if (currentPeriode() > 0f && loc.depotDeGarantie > 0f)
            moisInput.text = Mathf.RoundToInt(loc.depotDeGarantie / currentPeriode()).ToString();

        // Ancien dépôt (fixe) + nouveau dépôt possible (recalculé en direct).
        UIFactory.Text(body.transform, $"Ancien dépôt : {loc.depotDeGarantie:0.00} €", 16, UITheme.TexteSecondaire);
        var nouveauLine = UIFactory.Text(body.transform, "", 20, UITheme.Primaire, true);
        var hintLine = UIFactory.Text(body.transform, "", 14, UITheme.Alerte);

        var actions = UIFactory.HBox(body.transform, 10);
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
            // La prochaine révision garde l'ANNIVERSAIRE de l'échéance (et non la date
            // du jour, sinon l'anniversaire dériverait un peu plus à chaque révision
            // faite en retard). Sur un dépôt très en retard, un simple AddYears(1)
            // retombait dans le passé et l'échéance redevenait aussitôt dépassée :
            // on avance donc d'autant d'années qu'il faut pour repasser dans le futur.
            var next = dr.AddYears(1);
            while (next.Date <= DateTime.Today.Date) next = next.AddYears(1);
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
        RefreshAlertes(loc);
        _suiviInline?.Setup(_fiche);
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
