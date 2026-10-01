using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Suivi de facturation d'UN locataire, intégré en bandeau pleine largeur dans la
/// fiche (option A) : sélecteur d'année, tableau des factures avec état
/// À venir/À faire/Envoyé/Impayé/Payé + actions ; la GameObject devient une carte titrée.
/// Seule vue du suivi depuis le 28/09 (la vue plein écran a été supprimée) : un clic sur
/// une créance du menu arrive ici, via MontrerPour.
public class LocataireSuiviInline : MonoBehaviour
{
    LocatairePrefab _fiche; Locataire _loc;
    int _year;
    Transform _tableBox, _yearRow;
    bool _built;

    static readonly Color Accent = Hex("#9A5B2E");
    static readonly Color AccentClair = Hex("#F3E7D9");
    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    Transform _extBody, _extTitre;

    public void Setup(LocatairePrefab fiche) => Setup(fiche, null, null);

    // Coquille externe : `extBody` = Content (tableau) ; `extTitre` = bandeau de
    // titre cloné (sélecteur d'année ajouté à droite). Si null → chrome interne.
    public void Setup(LocatairePrefab fiche, Transform extBody, Transform extTitre)
    {
        _fiche = fiche;
        // La coquille n'est fournie qu'au premier appel, par LocataireFacturationFields.
        // Les appels suivants (Load, après chaque enregistrement) arrivent sans elle :
        // l'écraser ferait reconstruire le décor interne PAR-DESSUS la section de la
        // fiche, au lieu de la remplir.
        if (extBody != null) _extBody = extBody;
        if (extTitre != null) _extTitre = extTitre;
        _year = DateTime.Today.Year;
        Refresh();
    }

    // Rafraîchit le tableau (après génération/modification d'une facture, ou après
    // l'enregistrement de la fiche) — et rattrape le cas où le locataire n'existait
    // pas encore au premier appel.
    //
    // À la création d'une fiche, la section est construite AVANT que le locataire
    // entre dans `listLocataire` : `GetLocataire()` renvoyait null, le bandeau se
    // masquait, et plus rien ne le rallumait — le suivi restait vide jusqu'au
    // redémarrage de l'application. Pire, la sortie se faisait avant `Build()`, donc
    // la coquille était perdue. Le locataire est donc relu à CHAQUE passage.
    public void Refresh()
    {
        _aReconstruire = true;

        // Un suivi masqué faute de locataire (fiche tout juste créée) doit pouvoir se
        // rallumer : désactivé, il ne reçoit plus LateUpdate, donc il resterait vide
        // pour toujours — le défaut corrigé plus tôt, qui reviendrait par la fenêtre.
        if (!gameObject.activeSelf && _fiche != null && _fiche.GetLocataire() != null)
            gameObject.SetActive(true);
    }

    bool _aReconstruire;

    // La reconstruction est DIFFÉRÉE, pour deux raisons mesurées sur un simple
    // « Modifier + Sauvegarder » (2,9 s au total) :
    //
    // • elle était faite pour les 8 suivis existants alors qu'UN SEUL est affiché —
    //   356 ms dont sept huitièmes de travail que personne ne voit ;
    // • elle était faite DEUX fois pour le suivi visible, une fois par l'enregistrement
    //   du bâtiment, une fois par la réinitialisation de la fiche qui suit.
    //
    // Marquer puis reconstruire une seule fois en fin d'image règle les deux. Un suivi
    // masqué garde sa marque et se reconstruit à sa réapparition.
    void LateUpdate()
    {
        if (!_aReconstruire || !gameObject.activeInHierarchy) return;
        _aReconstruire = false;
        Reconstruire();
    }

    void Reconstruire()
    {
        _loc = _fiche != null ? _fiche.GetLocataire() : null;
        if (_loc == null) { gameObject.SetActive(false); return; }
        if (!_built) Build();
        RebuildYears();
        RebuildTable();
    }

    // Rafraîchit tous les Suivis ouverts. Appelé depuis l'enregistrement du bâtiment,
    // point de passage unique des 18 chemins qui modifient un locataire : le loyer
    // (RevisionPanel) et le dépôt (pop-up de révision) alimentent le suivi sans
    // passer par un panneau de facture, et laissaient donc un tableau périmé — ou
    // vide, sur une fiche tout juste créée.
    public static void RefreshTous()
    {
        foreach (var s in Resources.FindObjectsOfTypeAll<LocataireSuiviInline>())
            if (s != null && s._fiche != null) s.Refresh();
    }

    // Rafraîchit le Suivi de la fiche concernée (appelé par les panneaux de facture).
    public static void RefreshFor(LocatairePrefab fiche)
    {
        if (fiche == null) return;
        foreach (var s in Resources.FindObjectsOfTypeAll<LocataireSuiviInline>())
            if (s != null && s._fiche == fiche) s.Refresh();
    }

    // ── Arrivée depuis « Créances » ─────────────────────────────────────────────

    /// Amène la fiche sur son suivi, à l'année demandée (0 : garder l'année
    /// affichée). Appelé au clic sur une facture de « Créances », juste après la
    /// sélection de la fiche.
    public static void MontrerPour(LocatairePrefab fiche, int annee)
    {
        if (fiche == null) return;
        foreach (var s in Resources.FindObjectsOfTypeAll<LocataireSuiviInline>())
            if (s != null && s._fiche == fiche) { s.Montrer(annee); return; }
    }

    void Montrer(int annee)
    {
        if (isActiveAndEnabled) { StartCoroutine(MontrerApres(annee)); return; }
        if (annee > 0) { _year = annee; Refresh(); }   // masqué : au moins la bonne année à sa réapparition
    }

    IEnumerator MontrerApres(int annee)
    {
        // La fiche vient d'être sélectionnée : ses initialisations passent d'abord
        // (Setup remet l'année courante, qui écraserait celle demandée).
        yield return null;
        if (annee > 0 && annee != _year) { _year = annee; Refresh(); }
        yield return new WaitForEndOfFrame();   // tableau reconstruit (LateUpdate) et mis en page
        Canvas.ForceUpdateCanvases();
        AmenerEnHaut();
    }

    // Fait défiler la fiche pour que le haut du suivi arrive en haut de la vue.
    void AmenerEnHaut()
    {
        var sr = GetComponentInParent<ScrollRect>();
        if (sr == null || sr.content == null) return;
        var contenu = sr.content;
        var vue = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
        var coins = new Vector3[4];
        ((RectTransform)transform).GetWorldCorners(coins);                   // [1] = coin haut gauche
        float depuisLeHaut = contenu.rect.yMax - contenu.InverseTransformPoint(coins[1]).y;
        float max = Mathf.Max(0f, contenu.rect.height - vue.rect.height);
        sr.StopMovement();
        contenu.anchoredPosition = new Vector2(contenu.anchoredPosition.x, Mathf.Clamp(depuisLeHaut - 12f, 0f, max));
    }

    // ── Construction (la GameObject devient la carte) ───────────────────────────

    void Build()
    {
        _built = true;

        // Mode coquille externe (clone de section de scène) : on ne crée pas le
        // décor, on ajoute juste le sélecteur d'année dans le bandeau et le tableau
        // dans le corps fournis.
        if (_extBody != null)
        {
            if (_extTitre != null)
            {
                // Le HLG du bandeau cloné étire ses enfants en hauteur → les boutons
                // d'année rempliraient les 58px. On désactive l'étirement (les enfants
                // gardent leur hauteur, centrée verticalement).
                var hlg = _extTitre.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (hlg != null) { hlg.childForceExpandHeight = false; hlg.childControlHeight = true; hlg.childAlignment = TextAnchor.MiddleLeft; }
                var sp = UIFactory.Rect("Spacer", _extTitre);
                UIFactory.LE(sp.gameObject, flexW: 1);
                UIFactory.Text(_extTitre, "Année", UITheme.Role.Donnee, UITheme.TexteSecondaire);
                _yearRow = _extTitre;
            }
            var rows0 = UIFactory.VBox(_extBody, 6, 14, 14, 12, 14, "Rows");
            _tableBox = rows0.transform;
            return;
        }

        var img = gameObject.AddComponent<Image>();
        img.color = UITheme.Carte; img.sprite = UIFactory.Rounded(); img.type = Image.Type.Sliced;
        UIFactory.Border(gameObject);
        var v = gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(0, 0, 0, 0); v.spacing = 0;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        // Bande de titre : titre à gauche, sélecteur d'année à droite.
        var band = UIFactory.Panel("TitleBand", transform, AccentClair);
        UIFactory.LE(band.gameObject, minH: 44, prefH: 44, flexH: 0);
        var bh = band.gameObject.AddComponent<HorizontalLayoutGroup>();
        bh.padding = new RectOffset(16, 16, 4, 4); bh.spacing = 8;
        bh.childControlWidth = true; bh.childControlHeight = true;
        bh.childForceExpandWidth = false; bh.childForceExpandHeight = true;
        bh.childAlignment = TextAnchor.MiddleLeft;
        UIFactory.Text(bh.transform, "Suivi de facturation", UITheme.Role.Page, Accent, true);
        var spacer = UIFactory.Rect("Spacer", bh.transform);
        UIFactory.LE(spacer.gameObject, flexW: 1);
        UIFactory.Text(bh.transform, "Année", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        _yearRow = bh.transform;   // les boutons année sont ajoutés à la suite (à droite)

        // Corps : le tableau.
        var body = UIFactory.VBox(transform, 8, 14, 14, 12, 14, "Body");
        var tbl = UIFactory.VBox(body.transform, 6, 0, 0, 0, 0, "Rows");
        _tableBox = tbl.transform;
    }

    void RebuildYears()
    {
        foreach (Transform c in _yearRow) if (c.GetComponent<Button>() != null) Destroy(c.gameObject);
        // Chip modèle = un bouton de filtre des « améliorations » (même sprite/forme).
        var src = (_fiche != null && _fiche.objectivesManager != null) ? _fiche.objectivesManager.filterAllButton : null;
        int cur = DateTime.Today.Year;
        for (int y = cur - 3; y <= cur + 1; y++)
        {
            int yy = y;
            bool on = y == _year;
            Button b;
            GameObject chip = null;
            if (src != null)
            {
                var go = Instantiate(src.gameObject, _yearRow);
                go.name = "Year" + y;
                go.SetActive(true);
                chip = go;
                b = go.GetComponent<Button>();
                var lbl = go.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) { lbl.text = y.ToString(); lbl.color = on ? Color.white : Hex("#2C2C2A"); }
                var im = go.GetComponent<Image>();
                if (im != null) im.color = on ? Hex("#A9741C") : Hex("#E6E3DA");
                var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                le.minWidth = 62; le.preferredWidth = 62; le.flexibleWidth = 0;
                le.minHeight = 34; le.preferredHeight = 34; le.flexibleHeight = 0;
                var rt = (RectTransform)go.transform; rt.localScale = Vector3.one; rt.sizeDelta = new Vector2(62, 34);
            }
            else
            {
                b = UIFactory.Button(_yearRow, y.ToString(), on ? Hex("#A9741C") : Hex("#E6E3DA"),
                    on ? Color.white : Hex("#2C2C2A"), 34, UITheme.Role.Action, false);
                UIFactory.LE(b.gameObject, prefW: 62, minW: 62, flexW: 0, minH: 34, prefH: 34, flexH: 0);
                chip = b.gameObject;
            }
            if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(() => { _year = yy; RebuildYears(); RebuildTable(); }); }
            // Pastille si l'année contient une facturation à faire / en attente / impayée.
            var pc = YearAlerteColor(yy);
            if (chip != null && pc.HasValue) AjouteYearPastille(chip, pc.Value);
        }
    }

    // Rouge si un impayé, ambre s'il y a du « à faire » / « en attente d'envoi »,
    // sinon rien (année sans action). Pastille posée en haut-droite du chip.
    Color? YearAlerteColor(int y)
    {
        if (_loc == null) return null;
        bool impaye = false, autre = false;
        foreach (var l in FacturationSuivi.LignesAffichees(_loc, y))
        {
            var e = FacturationSuivi.EtatDe(l);
            if (e == FacturationSuivi.Etat.Impaye) impaye = true;
            else if (e == FacturationSuivi.Etat.AFaire || e == FacturationSuivi.Etat.AttenteEnvoi) autre = true;
        }
        if (impaye) return Hex("#D85A30");
        if (autre) return Hex("#A9741C");
        return null;
    }

    static void AjouteYearPastille(GameObject chip, Color c)
    {
        var p = UIFactory.Panel("Pastille", chip.transform, c);
        p.raycastTarget = false;
        var rt = (RectTransform)p.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(10f, 10f);
        rt.anchoredPosition = new Vector2(-2f, -2f);
    }

    // ── Tableau ─────────────────────────────────────────────────────────────────

    void RebuildTable()
    {
        if (_tableBox == null || _loc == null) return;
        foreach (Transform c in _tableBox) Destroy(c.gameObject);

        // La ligne vient du prefab SuiviFactureRow, porté par la fiche (les deux vues
        // du suivi sont créées par code et ne peuvent pas recevoir de référence par
        // l'inspecteur). Sans lui, aucun tableau : on le dit, plutôt que d'afficher
        // une carte vide sans cause visible.
        var prefab = _fiche != null ? _fiche.suiviRowPrefab : null;
        if (prefab == null)
        {
            Debug.LogError("Suivi de facturation : le champ « suiviRowPrefab » n'est pas câblé "
                + "sur le prefab LocatairePrefab — le tableau ne peut pas être construit.");
            return;
        }

        var lignes = FacturationSuivi.LignesAffichees(_loc, _year);

        var card = UIFactory.Panel("Card", _tableBox, UITheme.Carte);
        UIFactory.Border(card.gameObject);
        var cv = card.gameObject.AddComponent<VerticalLayoutGroup>();
        // Explicite : le defaut d'Unity est TRUE, et un groupe qui « veut s'etendre »
        // propage un flexibleHeight jusqu'en haut de la hierarchie — c'est ce qui
        // creusait 104 px de blanc dans la section Loyer.
        cv.childForceExpandHeight = false;
        cv.spacing = 0; cv.padding = new RectOffset(0, 0, 0, 0);
        cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Ligne(prefab, cv.transform).SetupEntete("Facture", "Échéance", "Montant TTC", "État", "Actions",
            Hex("#EDEBE3"), UITheme.TexteSecondaire);

        if (lignes.Count == 0)
        {
            // Locataire incomplet : dire pourquoi le suivi est vide, et quoi faire.
            var etape = ParcoursLocataire.Prochaine(_loc);
            string vide = etape == ParcoursLocataire.Etape.Termine
                ? "Aucune facture pour cette année."
                : "Le suivi de facturation apparaîtra une fois le locataire complet. " + ParcoursLocataire.Consigne(etape);
            Ligne(prefab, cv.transform).SetupVide(vide, UITheme.Carte, UITheme.TexteSecondaire);
            return;
        }

        int i = 0;
        foreach (var l in lignes)
        {
            var etat = FacturationSuivi.EtatDe(l);
            bool clot = FacturationSuivi.EstGrisee(etat);   // reprise, franchise, hors bail → grisée
            var couleurs = new SuiviRowUI.Couleurs(
                clot ? Hex("#F0EEE8") : ((i % 2 == 0) ? UITheme.Carte : Hex("#F6F4EC")),
                clot ? Hex("#A8A7A1") : UITheme.TextePrincipal,
                clot ? Hex("#B7B6B0") : UITheme.TexteSecondaire);
            i++;
            var lgn = l;
            var row = Ligne(prefab, cv.transform);
            row.Setup(Libelle(l), Ech(l.echeanceISO), Montant(l.montant), couleurs,
                FacturationSuivi.EtatLibelle(etat), EtatBg(etat), EtatFg(etat),
                // Ligne grisée (reprise, franchise, hors bail) : pastille statique, aucune action.
                !clot, () => OpenStatutMenu(lgn, (RectTransform)row.pastille.transform),
                Actions(l, etat));
        }
    }

    SuiviRowUI Ligne(GameObject prefab, Transform parent)
        => Instantiate(prefab, parent).GetComponent<SuiviRowUI>();

    // Actions de la ligne, dans l'ordre d'affichage — 4 au maximum (le prefab en
    // porte 4), ce que le pire cas atteint tout juste : PDF · Corriger · Rappel.
    List<(string libelle, Action onClick)> Actions(FactureEtat l, FacturationSuivi.Etat etat)
    {
        var a = new List<(string libelle, Action onClick)>();
        if (FacturationSuivi.EstGrisee(etat)) return a;   // reprise, franchise, hors bail : rien à faire
        string pdfAbs = FacturationSuivi.CheminPdf(l);
        bool genere = !string.IsNullOrEmpty(pdfAbs) && File.Exists(pdfAbs);
        if (genere)
        {
            a.Add(("PDF", () => Application.OpenURL("file:///" + pdfAbs.Replace("\\", "/"))));
            // Facture déjà émise → « Corriger » (crée une version corrigée, même numéro) ;
            // sinon « Refaire » (regénère). Correction câblée pour le loyer.
            // La ligne est TOUJOURS transmise : elle désigne la période à ouvrir.
            // Sans elle, « Refaire » ouvrait le panneau sur la dernière période
            // mémorisée — on croyait refaire mars et on éditait avril.
            // Correction ou simple remplacement, c'est FactureEmission.Preparer qui
            // tranche, d'après le statut réel de la facture.
            bool corrigeable = l.type == "Loyer"
                && (etat == FacturationSuivi.Etat.Envoye || etat == FacturationSuivi.Etat.Impaye);
            a.Add((corrigeable ? "Corriger" : "Refaire", () => OuvrirGeneration(l.type, l)));
        }
        // La ligne désigne la période à ouvrir : cliquer « Générer » sur le loyer
        // d'octobre doit ouvrir le panneau sur octobre, et non sur la dernière
        // période éditée.
        else a.Add(("Générer", () => OuvrirGeneration(l.type, l)));

        // Facture impayée → option « rappel d'échéance » (brouillon email manuel).
        if (etat == FacturationSuivi.Etat.Impaye)
            a.Add(("Rappel", () => FactureRappelService.Demander(_loc, l, () =>
            {
                _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
                RebuildTable();
            })));

        // Loyer payé sur bail NON commercial → possibilité d'émettre la quittance de loyer.
        // L'état « Payé » peut être forcé à la main sur une ligne JAMAIS émise : sans le
        // contrôle numéro + PDF, on éditait une quittance (un reçu) pour une facture
        // qui n'existe pas.
        bool reellementEmise = !string.IsNullOrEmpty(l.numero) && !string.IsNullOrEmpty(l.pdfPath);
        if (etat == FacturationSuivi.Etat.Paye && l.type == "Loyer"
            && !Locataire.EstBailCommercial(_loc.typeDeBail)
            && reellementEmise)
            a.Add(("Quittance", () => FactureQuittanceService.Emettre(_fiche, _loc, l)));
        return a;
    }

    // ── Menu de changement d'état ───────────────────────────────────────────────

    void OpenStatutMenu(FactureEtat ligne, RectTransform anchor)
    {
        var root = (FindObjectOfType<Canvas>()?.rootCanvas.transform) ?? transform;
        var scrim = UIFactory.Rect("StatutScrim", root);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        var sBtn = scrim.gameObject.AddComponent<Button>();
        scrim.SetAsLastSibling();
        sBtn.onClick.AddListener(() => Destroy(scrim.gameObject));

        var listBg = UIFactory.Panel("StatutList", scrim, UITheme.Carte);
        UIFactory.Border(listBg.gameObject);
        var vlg = listBg.gameObject.AddComponent<VerticalLayoutGroup>();
        // Explicite : le defaut d'Unity est TRUE, et un groupe qui « veut s'etendre »
        // propage un flexibleHeight jusqu'en haut de la hierarchie — c'est ce qui
        // creusait 104 px de blanc dans la section Loyer.
        vlg.childForceExpandHeight = false;
        vlg.spacing = 2; vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true;
        listBg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var lrt = (RectTransform)listBg.transform;
        lrt.anchorMin = lrt.anchorMax = Vector2.zero;
        lrt.sizeDelta = new Vector2(190, 0);

        Action<string> set = statut =>
        {
            // Le bâtiment porte les charges : sans lui, marquer « payé » ne les
            // encaisserait pas.
            FacturationSuivi.SetStatut(_loc, ligne, statut,
                _fiche.batimentPrefabOrigin != null ? _fiche.batimentPrefabOrigin.getBatiment() : null);
            _fiche.batimentPrefabOrigin.SaveAfterModifyToDoListLocataire();
            Destroy(scrim.gameObject);
            RebuildTable();
        };
        MenuItem(vlg.transform, "Payé", () => set("Paye"));
        MenuItem(vlg.transform, "Impayé", () => set("Impaye"));
        MenuItem(vlg.transform, "Envoyé", () => set("Envoye"));
        MenuItem(vlg.transform, "À faire", () => set("AFaire"));
        MenuItem(vlg.transform, "Automatique", () => set(""));

        // Placement intelligent : sous l'ancre, ou au-dessus s'il manque de place en bas.
        UIFactory.PlacePopup(lrt, anchor);
    }

    void MenuItem(Transform parent, string label, Action onClick)
    {
        var b = UIFactory.Button(parent, label, UITheme.Carte, UITheme.TextePrincipal, 34, UITheme.Role.Action, false);
        b.onClick.AddListener(() => onClick());
    }

    void OuvrirGeneration(string type, FactureEtat ligneCiblee = null)
    {
        // Parcours : aucune facture tant que général, bail, loyer et dépôt manquent.
        string bloque = ParcoursLocataire.FacturationBloquee(_loc);
        if (bloque != null) { UndoToast.Instance?.ShowInfo(bloque); return; }
        switch (type)
        {
            case "Loyer": FactureLoyerPanel.OpenLoyer(_fiche, ligneCiblee); break;
            case "Regul": FactureRegulPanel.OpenRegul(_fiche, ligneCiblee); break;
            case "Refac": FactureRefacPanel.OpenRefac(_fiche, ligneCiblee); break;
            // Révision : l'année vient de la date de révision de la fiche. La ligne ne
            // sert qu'à reconnaître le dépôt initial (« depot-initial »).
            case "Depot": FactureDepotPanel.OpenDepot(_fiche, ligneCiblee); break;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// Libellé de la ligne, augmenté de l'écart d'échéance quand il y en a un : une
    /// facture préparée garde la date imprimée sur son PDF, même si les modalités ont
    /// changé depuis. Sans cette mention, l'écart n'apparaissait nulle part et la date
    /// d'envoi, calculée sur l'ancienne échéance, semblait fausse.
    string Libelle(FactureEtat l)
    {
        var attendue = FacturationSuivi.EcheanceAttendue(_loc, l);
        return attendue.HasValue
            ? $"{l.libelle}  <color=#854F0B>(modalités : {attendue.Value:dd/MM} — refaire ?)</color>"
            : l.libelle;
    }

    static string Montant(float v) => v > 0f ? v.ToString("#,##0.00", Fr) + " €" : "—";
    static string Ech(string iso) => DateTime.TryParse(iso, out var d) ? d.ToString("dd/MM/yyyy") : "—";

    static Color EtatBg(FacturationSuivi.Etat e)
    {
        switch (e)
        {
            case FacturationSuivi.Etat.AVenir: return Hex("#F1EFE8");
            case FacturationSuivi.Etat.AFaire: return Hex("#FAEEDA");
            case FacturationSuivi.Etat.AttenteEnvoi: return Hex("#EDE8F6");
            case FacturationSuivi.Etat.Envoye: return Hex("#E6F1FB");
            case FacturationSuivi.Etat.Impaye: return Hex("#FCEBEB");
            case FacturationSuivi.Etat.Cloture:
            case FacturationSuivi.Etat.Franchise:
            case FacturationSuivi.Etat.HorsBail: return Hex("#ECEAE3");
            default:                           return Hex("#E1F5EE");
        }
    }

    static Color EtatFg(FacturationSuivi.Etat e)
    {
        switch (e)
        {
            case FacturationSuivi.Etat.AVenir: return Hex("#888780");
            case FacturationSuivi.Etat.AFaire: return Hex("#854F0B");
            case FacturationSuivi.Etat.AttenteEnvoi: return Hex("#6A5AA0");
            case FacturationSuivi.Etat.Envoye: return Hex("#185FA5");
            case FacturationSuivi.Etat.Impaye: return Hex("#A32D2D");
            case FacturationSuivi.Etat.Cloture:
            case FacturationSuivi.Etat.Franchise:
            case FacturationSuivi.Etat.HorsBail: return Hex("#9B9A94");
            default:                           return Hex("#0F6E56");
        }
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
