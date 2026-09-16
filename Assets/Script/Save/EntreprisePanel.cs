using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Sélecteur d'entreprises (multi-entreprise). Construit 100 % par code : liste des
/// entreprises connues, bascule, création (nom + dossier) et ouverture d'un dossier
/// existant. Chaque entreprise = sa propre sauvegarde (voir EntrepriseService).
public class EntreprisePanel : MonoBehaviour
{
    public static EntreprisePanel Instance { get; private set; }

    static readonly Color Vert = UITheme.Primaire, VertL = UITheme.PrimaireClair;
    static readonly Color Ambre = Hex("#854F0B"), AmbreL = Hex("#F6E6C8");

    Transform _content;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        UIFactory.Stretch(rt);
        Build();
        gameObject.SetActive(false);
    }

    public void Open() { gameObject.SetActive(true); transform.SetAsLastSibling(); Rebuild(); }
    public void Close() => gameObject.SetActive(false);

    public static void OpenPanel()
    {
        if (Instance == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("EntreprisePanel", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<EntreprisePanel>();
        }
        Instance.Open();
    }

    // ── Construction (coquille) ─────────────────────────────────────────────────

    void Build()
    {
        gameObject.AddComponent<Image>().color = UITheme.Fond;

        var col = UIFactory.VBox(transform, 12, 24, 24, 14, 16, "Col");
        UIFactory.Stretch((RectTransform)col.transform);
        col.childForceExpandHeight = false;

        var header = UIFactory.HBox(col.transform, 12, false, "Header");
        UIFactory.LE(header.gameObject, minH: 52);
        var back = UIFactory.Button(header.transform, "←  Retour", UITheme.Carte, UITheme.TextePrincipal, 42, 18);
        UIFactory.Border(back.gameObject); UIFactory.LE(back.gameObject, prefW: 140, flexW: 0);
        back.onClick.AddListener(Close);
        var title = UIFactory.Text(header.transform, "Entreprises", 26, UITheme.TextePrincipal, true);
        UIFactory.LE(title.gameObject, flexW: 1);

        _content = MakeScroll(col.transform);
    }

    Transform MakeScroll(Transform parent)
    {
        var srGO = UIFactory.Rect("Scroll", parent);
        var sr = srGO.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 32;
        sr.movementType = ScrollRect.MovementType.Clamped;
        UIFactory.LE(srGO.gameObject, flexH: 1);

        var viewport = UIFactory.Rect("Viewport", srGO);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = UIFactory.VBox(viewport, 16, 2, 8, 2, 12, "Content");
        var crt = (RectTransform)content.transform;
        crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(.5f, 1);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        sr.viewport = viewport; sr.content = crt;
        return content.transform;
    }

    // ── Contenu (reconstruit à chaque ouverture / modif) ────────────────────────

    void Rebuild()
    {
        EntrepriseService.EnsureActiveRegistered();
        foreach (Transform c in _content) Destroy(c.gameObject);

        var body = UIFactory.Section(_content, "Mes entreprises", Vert, VertL);
        var actif = EntrepriseService.All().Find(EntrepriseService.EstActive);
        UIFactory.Text(body.transform,
            "Chaque entreprise a sa propre sauvegarde. Ouvrez-en une pour basculer dessus.",
            15, UITheme.TexteSecondaire);

        var liste = EntrepriseService.All();
        if (liste.Count == 0)
            UIFactory.Text(body.transform, "Aucune entreprise. Créez-en une ci-dessous.", 16, UITheme.TexteSecondaire);

        foreach (var e in liste)
        {
            bool active = EntrepriseService.EstActive(e);
            var row = UIFactory.HBox(body.transform, 8, false, "EntRow");
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = active ? VertL : UITheme.Fond; bg.sprite = UIFactory.Rounded(); bg.type = Image.Type.Sliced;
            UIFactory.LE(row.gameObject, minH: 56);
            var pad = UIFactory.VBox(row.transform, 1, 12, 10, 6, 6, "info");
            UIFactory.LE(pad.gameObject, flexW: 1);
            string nom = string.IsNullOrWhiteSpace(e.nom) ? "(sans nom)" : e.nom;
            UIFactory.Text(pad.transform, nom + (active ? "   • active" : ""), 18, UITheme.TextePrincipal, true);
            UIFactory.Text(pad.transform, e.racine ?? "—", 13, UITheme.TexteSecondaire);

            var e2 = e;
            if (!active)
            {
                var open = UIFactory.Button(row.transform, "Ouvrir", Vert, Color.white, 36, 16);
                UIFactory.LE(open.gameObject, prefW: 110, flexW: 0);
                // Bascule d'entreprise : ReloadFromDisk détruit les fiches ouvertes.
                open.onClick.AddListener(() => FermetureGuard.ConfirmerPerteSaisies(
                    "Changer d'entreprise", () => { EntrepriseService.Activer(e2.racine); Close(); }));
            }
            var oub = UIFactory.Button(row.transform, "Retirer", UITheme.Carte, UITheme.TexteSecondaire, 36, 15);
            UIFactory.Border(oub.gameObject); UIFactory.LE(oub.gameObject, prefW: 100, flexW: 0);
            oub.onClick.AddListener(() => ConfirmDialog.Instance?.Show(
                "Retirer de la liste ?",
                "Les données sur le disque ne sont pas supprimées — l'entreprise disparaît seulement de cette liste.",
                () => { EntrepriseService.Oublier(e2.racine); Rebuild(); }, "Retirer"));
        }

        // Actions
        var act = UIFactory.Section(_content, "Ajouter une entreprise", Ambre, AmbreL);
        var creer = UIFactory.Button(act.transform, "+  Créer une entreprise", AmbreL, Ambre, 44, 18);
        creer.onClick.AddListener(CreerFlux);
        var ouvrir = UIFactory.Button(act.transform, "Ouvrir un dossier existant", UITheme.Carte, UITheme.TextePrincipal, 42, 17);
        UIFactory.Border(ouvrir.gameObject);
        ouvrir.onClick.AddListener(() =>
        {
            if (SaveIO.PickFolder("Ouvrir une entreprise — choisir le dossier", out var dir))
            {
                FermetureGuard.ConfirmerPerteSaisies(
                    "Ouvrir une entreprise", () => { EntrepriseService.Ouvrir(dir); Close(); });
            }
        });
    }

    // Créer : demande le nom, puis le dossier, puis crée + bascule.
    void CreerFlux()
    {
        Modal("Nouvelle entreprise", body =>
        {
            UIFactory.Text(body.transform, "Nom de l'entreprise", 17, UITheme.TexteSecondaire);
            var f = UIFactory.Input(body.transform, "GROUPE CIPL");
            return () =>
            {
                string nom = f.text;
                if (SaveIO.PickFolder("Emplacement de la nouvelle entreprise — choisir le dossier", out var dir))
                {
                    // Création refusée (dossier déjà occupé) → on garde le panneau
                    // ouvert pour que l'utilisatrice choisisse un autre dossier.
                    FermetureGuard.ConfirmerPerteSaisies("Créer une entreprise",
                        () => { if (EntrepriseService.Creer(nom, dir)) Close(); });
                }
            };
        });
    }

    // ── Petit modal (nom) ───────────────────────────────────────────────────────

    void Modal(string titre, Func<VerticalLayoutGroup, Action> builder)
    {
        var scrim = UIFactory.Rect("EntScrim", transform.parent);
        UIFactory.Stretch(scrim);
        scrim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        scrim.SetAsLastSibling();

        var cardImg = UIFactory.Panel("Card", scrim, UITheme.Carte);
        UIFactory.Border(cardImg.gameObject);
        var card = (RectTransform)cardImg.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        card.sizeDelta = new Vector2(520, 100);
        var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10; v.padding = new RectOffset(18, 18, 16, 16);
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var vcsf = cardImg.gameObject.AddComponent<ContentSizeFitter>();
        vcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        vcsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        UIFactory.Text(v.transform, titre, 22, UITheme.TextePrincipal, true);
        var onValidate = builder(v);

        var actions = UIFactory.HBox(v.transform, 10, false, "Actions");
        UIFactory.LE(actions.gameObject, minH: 46, prefH: 46);
        var cancel = UIFactory.Button(actions.transform, "Annuler", UITheme.Carte, UITheme.TextePrincipal, 44, 18);
        UIFactory.Border(cancel.gameObject); UIFactory.LE(cancel.gameObject, flexW: 1);
        cancel.onClick.AddListener(() => Destroy(scrim.gameObject));
        var ok = UIFactory.Button(actions.transform, "Choisir le dossier…", UITheme.Primaire, Color.white, 44, 18);
        UIFactory.LE(ok.gameObject, flexW: 1);
        ok.onClick.AddListener(() => { Destroy(scrim.gameObject); onValidate?.Invoke(); });
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
