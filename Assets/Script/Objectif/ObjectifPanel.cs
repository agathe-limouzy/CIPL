using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Fenêtre « Nouvel objectif » / « Modifier l'objectif » (maquette validée le 06/10) :
/// texte, importance, avancement, échéance + « me prévenir », récurrence, documents.
public static class ObjectifPanel
{
    static readonly Color Ardoise = Hex("#2C3E5E");

    /// `obj` null = création (ajouté en tête de `liste`). `apres` : enregistrer et rafraîchir.
    public static void Ouvrir(Transform depuis, List<Objective> liste, Objective obj,
        string nomBat, string nomLoc, Action apres, Action<Objective> supprimer)
    {
        var canvas = depuis != null ? depuis.GetComponentInParent<Canvas>() : null;
        if (canvas == null || liste == null) return;
        bool creation = obj == null;
        var documents = new List<string>(obj?.documents ?? new List<string>());

        var scrim = UIFactory.Rect("ObjectifScrim", canvas.rootCanvas.transform);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        scrim.SetAsLastSibling();
        Action fermer = () => UnityEngine.Object.Destroy(scrim.gameObject);

        var cardImg = UIFactory.Panel("ObjectifCard", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(620, 100);
        var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        cardImg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Entete(v.transform, creation ? "Nouvel objectif" : "Modifier l'objectif");
        var body = UIFactory.VBox(v.transform, 10, 18, 18, 14, 16, "Body");

        // Objectif
        var texte = DepartPanel.Champ(body.transform, "Objectif", "Contrôle des extincteurs", 1f);
        texte.text = obj?.text ?? "";

        // Importance · Avancement
        var l1 = UIFactory.HBox(body.transform, 16, false, "Ligne1");
        var importance = UIDropdown.Create(Colonne(l1.transform, "Catégorie"),
            new List<string> { "Tâche", "Rappel", "Obligatoire" }, null,
            (int)(obj?.importance ?? Objective.Importance.Normale), null);
        var avancement = UIDropdown.Create(Colonne(l1.transform, "Avancement"),
            new List<string> { "À faire", "En cours", "Fait" }, null,
            (int)(obj?.avancement ?? Objective.Avancement.AFaire), null);

        // Échéance · Me prévenir (jours avant)
        var l2 = UIFactory.HBox(body.transform, 16, false, "Ligne2");
        var echeance = ChampDate(l2.transform, "Échéance (facultative)", obj != null ? Objectifs.Echeance(obj) : null);
        var prevenir = DepartPanel.Champ(l2.transform, "Me prévenir (jours avant)", Objectifs.PREVENIR_DEFAUT.ToString(), 1f);
        prevenir.contentType = TMP_InputField.ContentType.IntegerNumber;
        prevenir.text = (obj?.prevenirJours ?? Objectifs.PREVENIR_DEFAUT).ToString();

        // Récurrence : Aucune / Répéter · tous les N · unité
        UIFactory.Text(body.transform, "Récurrence", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var l3 = UIFactory.HBox(body.transform, 10, false, "Ligne3");
        bool repete = (obj?.repeterTous ?? 0) > 0;
        Action majRepet = null;
        var repeter = UIDropdown.Create(l3.transform, new List<string> { "Aucune", "Répéter" }, null, repete ? 1 : 0, _ => majRepet?.Invoke());
        UIFactory.LE(repeter.gameObject, prefW: 150, minW: 150);
        var tousLes = UIFactory.Text(l3.transform, "tous les", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var n = UIFactory.Input(l3.transform, "1");
        n.contentType = TMP_InputField.ContentType.IntegerNumber;
        n.text = repete ? obj.repeterTous.ToString() : "1";
        UIFactory.LE(n.gameObject, prefW: 64, minW: 64);
        var unite = UIDropdown.Create(l3.transform, new List<string> { "jours", "semaines", "mois", "ans" }, null,
            repete ? (int)obj.repeterUnite : (int)Objective.UniteRecurrence.Ans, _ => majRepet?.Invoke());
        UIFactory.LE(unite.gameObject, prefW: 150, minW: 150);
        var aideRepet = UIFactory.Text(body.transform, "", UITheme.Role.Aide, UITheme.TexteSecondaire);
        majRepet = () =>
        {
            bool on = repeter.Index == 1;
            tousLes.gameObject.SetActive(on); n.gameObject.SetActive(on); unite.gameObject.SetActive(on);
            int.TryParse(n.text, out int k);
            aideRepet.text = !on ? "" : echeance.LireDate(out var e) && k > 0
                ? $"Une fois fait, un nouvel objectif est créé pour le {Objectifs.Suivante(e, k, (Objective.UniteRecurrence)unite.Index):dd/MM/yyyy}."
                : "Saisissez une échéance : la répétition part de cette date.";
        };
        n.onValueChanged.AddListener(_ => majRepet());
        foreach (var c in new[] { echeance.dayInput, echeance.monthInput, echeance.yearInput })
            c.onValueChanged.AddListener(_ => majRepet());
        majRepet();

        // Documents (copiés dans le dossier « Objectifs » du bâtiment ou du locataire)
        UIFactory.Text(body.transform, "Documents", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        var docs = UIFactory.VBox(body.transform, 4, name: "Documents").transform;
        string dossier = DossiersDonnees.DossierObjectifs(nomBat, nomLoc);
        Action majDocs = null;
        majDocs = () =>
        {
            foreach (Transform t in docs) UnityEngine.Object.Destroy(t.gameObject);
            foreach (var nom in documents.ToList())
            {
                var ligne = UIFactory.HBox(docs, 8, false, "Doc");
                string chemin = Path.Combine(dossier, nom);
                bool existe = File.Exists(chemin);
                var t = UIFactory.Text(ligne.transform, existe ? nom : $"Introuvable : {nom}", UITheme.Role.Donnee, UITheme.TextePrincipal);
                t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
                UIFactory.LE(t.gameObject, flexW: 1, minW: 0);
                var ouvrir = PetitBouton(ligne.transform, "Ouvrir");
                ouvrir.interactable = existe;
                ouvrir.onClick.AddListener(() => Application.OpenURL("file:///" + chemin.Replace('\\', '/')));
                var retirer = PetitBouton(ligne.transform, "Retirer");
                retirer.onClick.AddListener(() => { documents.Remove(nom); majDocs(); });
            }
            var joindre = PetitBouton(docs, "+ Joindre des documents…");
            joindre.onClick.AddListener(() =>
            {
                foreach (var nom in Choisir(dossier))
                    if (!documents.Contains(nom)) documents.Add(nom);
                majDocs();
            });
        };
        majDocs();

        // Occurrences précédentes (objectif récurrent) : chacune s'ouvre dans sa propre
        // fenêtre, par-dessus celle-ci (la saisie en cours n'est pas perdue).
        var precedentes = creation ? new List<Objective>() : Objectifs.Precedentes(liste, obj);
        if (precedentes.Count > 0)
        {
            UIFactory.Text(body.transform, $"Occurrences précédentes ({precedentes.Count})", UITheme.Role.Donnee, UITheme.TexteSecondaire);
            var occ = UIFactory.VBox(body.transform, 4, name: "Occurrences").transform;
            foreach (var p in precedentes)
            {
                var ligne = UIFactory.HBox(occ, 8, false, "Occurrence");
                var t = UIFactory.Text(ligne.transform, Objectifs.TexteOccurrence(p), UITheme.Role.Donnee, UITheme.TextePrincipal);
                t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
                UIFactory.LE(t.gameObject, flexW: 1, minW: 0);
                var ouvrirOcc = PetitBouton(ligne.transform, "Ouvrir");
                var cible = p;
                ouvrirOcc.onClick.AddListener(() => Ouvrir(depuis, liste, cible, nomBat, nomLoc, apres, supprimer));
            }
        }

        // Pied : Supprimer · Annuler · Enregistrer
        var pied = UIFactory.HBox(v.transform, 8, false, "Pied");
        pied.padding = new RectOffset(18, 18, 10, 14);
        if (!creation && supprimer != null)
        {
            var sup = UIFactory.Button(pied.transform, "Supprimer", UITheme.Carte, UITheme.Alerte, 40, UITheme.Role.Bouton, false);
            UIFactory.Border(sup.gameObject, UITheme.Alerte);
            UIFactory.LargeurDuTexte(sup);
            sup.onClick.AddListener(() => { fermer(); supprimer(obj); });
        }
        UIFactory.LE(UIFactory.Rect("Espace", pied.transform).gameObject, flexW: 1);
        var annuler = UIFactory.Button(pied.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 40, UITheme.Role.Bouton, false);
        UIFactory.Border(annuler.gameObject);
        UIFactory.LargeurDuTexte(annuler);
        annuler.onClick.AddListener(() => fermer());
        var enregistrer = UIFactory.Button(pied.transform, "Enregistrer", Ardoise, Color.white, 40, UITheme.Role.Bouton);
        UIFactory.LargeurDuTexte(enregistrer);

        enregistrer.onClick.AddListener(() =>
        {
            bool vide = string.IsNullOrWhiteSpace(echeance.dayInput.text) && string.IsNullOrWhiteSpace(echeance.monthInput.text)
                        && string.IsNullOrWhiteSpace(echeance.yearInput.text);
            DateTime e = default;
            if (!vide && !echeance.LireDate(out e)) { ConfirmDialog.Erreur("Échéance : date incomplète (JJ/MM/AAAA), ou laissez-la vide."); return; }
            string iso = vide ? "" : Objectifs.Iso(e);
            int jours = Objectifs.PREVENIR_DEFAUT;
            if (!string.IsNullOrWhiteSpace(prevenir.text) && !int.TryParse(prevenir.text, out jours))
            { ConfirmDialog.Erreur("Me prévenir : un nombre de jours."); return; }
            int k = 0;
            if (repeter.Index == 1 && (!int.TryParse(n.text, out k) || k < 1)) { ConfirmDialog.Erreur("Répéter : un intervalle d'au moins 1."); return; }
            string refus = Objectifs.Verifier(texte.text, iso, jours, k);
            if (refus != null) { ConfirmDialog.Erreur(refus); return; }

            var o = obj ?? new Objective(texte.text.Trim());
            o.text = texte.text.Trim();
            o.importance = (Objective.Importance)importance.Index;
            o.echeanceISO = iso;
            o.prevenirJours = jours;
            o.repeterTous = k;
            o.repeterUnite = (Objective.UniteRecurrence)Math.Max(0, unite.Index);
            o.documents = documents;
            if (creation) liste.Insert(0, o);

            // L'avancement en dernier : « Fait » crée l'occurrence suivante avec les valeurs saisies.
            var av = (Objective.Avancement)avancement.Index;
            if (av == Objective.Avancement.Fait) Objectifs.MarquerFait(liste, o, DateTime.Today);
            else { Objectifs.Rouvrir(liste, o); o.avancement = av; }

            fermer();
            apres?.Invoke();
        });

        texte.ActivateInputField();
    }

    /// Clic sur un objectif dans « À traiter » (accueil ou résumé du bâtiment) — retour du
    /// 06/10 : ouvre la fiche dédiée (locataire, sinon fiche complète du bâtiment), puis la
    /// fenêtre de l'objectif par-dessus. L'appelant masque l'accueil s'il y a lieu.
    public static void OuvrirAlerte(HomeAlert a)
    {
        var bp = a?.batiment;
        if (bp == null || a.objectif == null) return;
        var liste = a.locataire != null ? a.locataire.objectifs?.items : bp.getBatiment()?.objectifs?.items;
        if (liste == null) return;

        BatimentManager.Instance?.menuManager?.OnSelect(bp);
        Transform depuis = bp.transform;
        if (a.locataire != null && bp.dictionnairelocataire.TryGetValue(a.locataire, out var lp) && lp != null)
        {
            bp.ShowLocataireView();
            bp.menulocataire.OnSelect(lp);
            depuis = lp.transform;
        }
        else bp.ShowFiche();

        Action enregistrer = () =>
        {
            EnregistrerFiche(bp, a.locataire);
            if (bp.summaryView != null) bp.summaryView.Refresh();
        };
        Ouvrir(depuis, liste, a.objectif, bp.getName(), a.locataire?.Name, enregistrer, o =>
        {
            var annuler = Objectifs.Supprimer(liste, o);
            enregistrer();
            UndoToast.Instance?.Show("Objectif supprimé", () => { annuler(); enregistrer(); });
        });
    }

    /// Enregistre par la fiche (sa copie des données fait foi) et rafraîchit sa liste d'objectifs.
    public static void EnregistrerFiche(BatimentPrefab bp, Locataire loc)
    {
        if (bp == null) return;
        bp.SaveAfterModifyToDoListLocataire();
        var om = loc != null && bp.dictionnairelocataire.TryGetValue(loc, out var lp) && lp != null
            ? lp.objectivesManager : bp.objectivesManager;
        if (om != null) om.RefreshList();
    }

    static Transform Colonne(Transform parent, string libelle)
    {
        var col = UIFactory.VBox(parent, 4, name: libelle);
        UIFactory.LE(col.gameObject, flexW: 1);
        UIFactory.Text(col.transform, libelle, UITheme.Role.Donnee, UITheme.TexteSecondaire);
        return col.transform;
    }

    // Champ date cloné de celui de la fiche locataire (JJ / MM / AAAA, comme partout) :
    // même construction que DepartPanel.ChampDate, sans exiger une fiche locataire ouverte.
    static DateInputController ChampDate(Transform parent, string titre, DateTime? valeur)
    {
        var modele = Resources.FindObjectsOfTypeAll<LocatairePrefab>().Select(p => p.dateFinBail).First(d => d != null);
        var bloc = UnityEngine.Object.Instantiate(modele.gameObject, parent);
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

    static Button PetitBouton(Transform parent, string libelle)
    {
        var b = UIFactory.Button(parent, libelle, UITheme.Carte, UITheme.TextePrincipal, 32, UITheme.Role.Action, false);
        UIFactory.Border(b.gameObject);
        UIFactory.LargeurDuTexte(b);
        return b;
    }

    // Choisit un ou plusieurs fichiers (Ctrl / Maj dans l'explorateur) et les copie dans le
    // dossier des objectifs. Noms retenus ; un fichier qui ne se copie pas est signalé, les
    // autres sont gardés.
    // ponytail: un document joint puis « Annuler » reste copié sur le disque (non référencé).
    static List<string> Choisir(string dossier)
    {
        var noms = new List<string>();
#if UNITY_STANDALONE || UNITY_EDITOR
        var paths = SFB.StandaloneFileBrowser.OpenFilePanel("Joindre des documents", "",
            new[] { new SFB.ExtensionFilter("Documents", "pdf", "doc", "docx", "xls", "xlsx", "jpg", "jpeg", "png", "txt") }, true);
        var echecs = new List<string>();
        foreach (var p in paths ?? new string[0])
        {
            if (string.IsNullOrEmpty(p)) continue;
            try { noms.Add(DossiersDonnees.CopierSansEcraser(p, dossier)); }
            catch (Exception e) { echecs.Add($"{Path.GetFileName(p)} : {e.Message}"); }
        }
        if (echecs.Count > 0) ConfirmDialog.Erreur("Copie impossible :\n" + string.Join("\n", echecs));
#endif
        return noms;
    }

    // Bandeau plein, coins arrondis en haut (même construction que DepartPanel.Entete).
    static void Entete(Transform carte, string texte)
    {
        var header = UIFactory.Panel("Header", carte, Ardoise);
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
        t.overflowMode = TextOverflowModes.Ellipsis;
        UIFactory.LE(t.gameObject, flexW: 1, minW: 0);
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
