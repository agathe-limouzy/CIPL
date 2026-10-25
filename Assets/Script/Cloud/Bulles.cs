using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Étape 13b-2 (maquettes validées le 25/10) : une série de bulles posées sur l'écran, une par étape,
/// la zone visée éclairée et le reste assombri. La visite guidée bloque les clics sous le voile ; un
/// guide des premiers pas laisse tout utilisable et avance de lui-même quand l'étape est faite.
public static class Bulles
{
    public class Etape
    {
        public string titre, texte;
        public string attente;              // guide : « La suite s'affiche dès que … »
        public Func<string> manque;         // guide : ce qui manque encore, relu tant qu'on attend (null : rien)
        public Func<bool> pret;             // guide : « Suivant » apparaît dès que c'est vrai (champ rempli, en cours de frappe)
        public Func<RectTransform> cible;   // null : pas de zone éclairée
        public Func<bool> fait;             // null : bouton « Suivant »
        public Func<bool> sauter;           // vrai à l'arrivée : étape passée
        public string suivant, quitter;     // textes propres à l'étape (sinon ceux de la série)
    }

    public const string Nom = "Bulles";
    public static bool EnCours => GameObject.Find(Nom) != null;

    public static void Arreter()
    {
        var g = GameObject.Find(Nom);
        if (g != null) UnityEngine.Object.Destroy(g);
    }

    /// `entete` (« Vos RIB ») : guide, numéroté « étape 2 sur 3 » ; null : visite, « 2 / 5 ».
    /// `fin(true)` : dernière étape faite ou validée ; `fin(false)` : quittée.
    /// `ferme` : appelé quand la série disparaît, de quelque façon que ce soit (finie, quittée,
    /// remplacée par une autre, Play arrêté).
    public static void Lancer(IList<Etape> etapes, string entete, bool bloquer, string quitter, Action<bool> fin, Action ferme = null)
    {
        Arreter();
        var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>()?.rootCanvas;
        if (canvas == null || etapes == null || etapes.Count == 0) return;
        var voile = UIFactory.Rect(Nom, canvas.transform);
        UIFactory.Stretch(voile);
        voile.SetAsLastSibling();
        var v = voile.gameObject.AddComponent<Vue>();
        v.etapes = etapes; v.entete = entete; v.bloquer = bloquer; v.quitter = quitter; v.fin = fin; v.ferme = ferme;
        v.Aller(0, +1);
    }

    /// Centre de la bulle (taille `taille`) en coordonnées de l'écran `ecran` : à droite de la zone s'il
    /// y a la place, sinon dessous, dessus, à gauche ; sans zone, au centre. Toujours dans l'écran.
    public static Vector2 Placer(Rect? zone, Vector2 taille, Rect ecran, float marge = 12)
    {
        float w = taille.x, h = taille.y, m = 8;
        if (zone == null) return ecran.center;
        var z = zone.Value;
        // Une zone de toute la largeur (bouton « Enregistrer » du bas) : la bulle à droite, où les
        // champs, alignés à gauche, ne sont pas cachés (vu en Play le 25/10).
        float cx = z.width > ecran.width / 2 ? ecran.xMax - m - w / 2
                 : Mathf.Clamp(z.xMin + w / 2, ecran.xMin + m + w / 2, ecran.xMax - m - w / 2);
        float cy = Mathf.Clamp(z.center.y, ecran.yMin + m + h / 2, ecran.yMax - m - h / 2);
        if (z.xMax + marge + w <= ecran.xMax - m) return new Vector2(z.xMax + marge + w / 2, cy);
        if (z.yMin - marge - h >= ecran.yMin + m) return new Vector2(cx, z.yMin - marge - h / 2);
        if (z.yMax + marge + h <= ecran.yMax - m) return new Vector2(cx, z.yMax + marge + h / 2);
        if (z.xMin - marge - w >= ecran.xMin + m) return new Vector2(z.xMin - marge - w / 2, cy);
        return new Vector2(cx, ecran.yMin + m + h / 2);
    }

    // ── Repères : une zone d'un écran construit par code, par son texte ──────────
    // `dans` : la racine où chercher (null : rien), pour ne pas prendre le même texte ailleurs
    // (la carte de l'accueil reste ouverte sous les Réglages). Les textes des bulles sont ignorés.

    public static RectTransform Texte(string s, Transform dans) =>
        dans == null ? null : dans.GetComponentsInChildren<TMP_Text>()
            .FirstOrDefault(t => t.text == s && t.GetComponentInParent<Vue>() == null)?.rectTransform;

    public static RectTransform Bouton(string s, Transform dans)
    {
        var t = Texte(s, dans);
        var b = t != null ? t.GetComponentInParent<Button>() : null;
        return b != null ? (RectTransform)b.transform : null;
    }

    /// L'élément qui suit `r` (le champ sous son libellé).
    public static RectTransform Suivant(RectTransform r)
    {
        if (r == null) return null;
        int i = r.GetSiblingIndex() + 1;
        return i < r.parent.childCount ? r.parent.GetChild(i) as RectTransform : null;
    }

    public static RectTransform Champ(string libelle, Transform dans) => Suivant(Texte(libelle, dans));

    /// Une section titrée de UIFactory.Section (titre › bande › section).
    public static RectTransform Section(string titre, Transform dans) => Texte(titre, dans)?.parent?.parent as RectTransform;

    /// La fenêtre de ReglagePanel.Modal ouverte sous ce titre.
    public static RectTransform Fenetre(string titre)
    {
        var scrim = GameObject.Find("ModalScrim");
        return scrim != null ? Texte(titre, scrim.transform)?.parent as RectTransform : null;
    }

    public static bool Coche(string libelle, Transform dans)
    {
        var t = Texte(libelle, dans);
        var c = t != null ? t.GetComponentInParent<Toggle>() : null;
        return c != null && c.isOn;
    }

    /// Un champ rempli et quitté (13b-3) : la bulle n'avance pas pendant la frappe. `sauf` : la
    /// valeur de départ (« Nouveau »), qui ne compte pas.
    public static bool Rempli(TMP_InputField f, string sauf = null) => NonVide(f, sauf) && !f.isFocused;

    public static bool NonVide(TMP_InputField f, string sauf = null) =>
        f != null && !string.IsNullOrWhiteSpace(f.text) && f.text.Trim() != sauf;

    public static string Saisi(RectTransform champ)
    {
        var f = champ != null ? champ.GetComponent<TMP_InputField>() : null;
        return f != null ? f.text : "";
    }

    // ── La série à l'écran ─────────────────────────────────────────────────────

    class Vue : MonoBehaviour
    {
        public IList<Etape> etapes; public string entete, quitter; public bool bloquer; public Action<bool> fin; public Action ferme;

        void OnDestroy() => ferme?.Invoke();
        int _n;
        float _prochain;
        Rect? _zone;
        RectTransform _cible, _bulle;
        TMP_Text _attente;
        bool _aEteFaux, _suivant;
        readonly List<GameObject> _ombre = new List<GameObject>();

        RectTransform Voile => (RectTransform)transform;
        bool Sautee(int i) => i != _n && etapes[i].sauter?.Invoke() == true;

        public void Aller(int depart, int sens)
        {
            int i = depart;
            while (i >= 0 && i < etapes.Count && etapes[i].sauter?.Invoke() == true) i += sens;
            if (i >= etapes.Count) { Finir(true); return; }
            if (i < 0) return;
            _n = i;
            _cible = null;
            // Retour du 25/10 : une étape déjà faite à l'arrivée (champ prérempli, « Précédent ») ne repart
            // pas seule — elle propose « Suivant » ; elle n'avance d'elle-même qu'après avoir été défaite.
            _aEteFaux = etapes[_n].fait?.Invoke() != true;
            Construire();
            Suivre(true);
        }

        void Finir(bool complet)
        {
            gameObject.name = Nom + " (fin)";   // une série lancée par `fin` ne doit pas le prendre pour elle
            Destroy(gameObject);
            fin?.Invoke(complet);
        }

        void Update()
        {
            if (Time.unscaledTime < _prochain) return;
            _prochain = Time.unscaledTime + 0.2f;
            // Au-dessus des fenêtres ouvertes entre-temps, mais pas d'un message (refus d'un RIB…).
            if (ConfirmDialog.Instance == null || !ConfirmDialog.Instance.gameObject.activeSelf) transform.SetAsLastSibling();
            var fait = etapes[_n].fait;
            if (fait != null)
            {
                if (!fait()) _aEteFaux = true;
                else if (_aEteFaux) { Aller(_n + 1, +1); return; }
            }
            // Retour du 25/10 : « Suivant » dès que le champ est rempli, sans devoir cliquer ailleurs.
            var pret = etapes[_n].pret;
            if (pret != null && _aEteFaux && pret() != _suivant) { Construire(); Suivre(true); return; }
            // Retour du 25/10 : la bulle qui attend dit ce qui manque (sans ça, « il se passe rien »).
            // Seulement les bulles qui attendent (vu en Play le 25/10 : relu aussi dans la visite, où il n'y a
            // pas de ligne d'attente → une erreur toutes les 0,2 s).
            string attente = _attente != null ? Attente(etapes[_n]) : null;
            if (_attente != null && _attente.text != attente)
            {
                _attente.text = attente;
                LayoutRebuilder.ForceRebuildLayoutImmediate(_bulle);
                Suivre(true);
                return;
            }
            Suivre(false);
        }

        /// La zone visée bouge (défilement, fenêtre ouverte, mise en page) : le voile la suit.
        void Suivre(bool force)
        {
            var c = etapes[_n].cible?.Invoke();
            if (c != null && !c.gameObject.activeInHierarchy) c = null;
            if (c != null && c != _cible) Defiler(c);
            _cible = c;
            Rect? z = c == null ? (Rect?)null : Zone(c);
            if (!force && Pareil(z, _zone)) return;
            _zone = z;
            Dessiner();
        }

        Rect Zone(RectTransform c)
        {
            var cv = GetComponentInParent<Canvas>().rootCanvas;
            var cam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;
            var coins = new Vector3[4];
            c.GetWorldCorners(coins);
            var pts = coins.Select(w =>
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Voile, RectTransformUtility.WorldToScreenPoint(cam, w), cam, out var p);
                return p;
            }).ToList();
            var r = Voile.rect;
            float x0 = Mathf.Max(r.xMin, pts.Min(p => p.x) - 6), x1 = Mathf.Min(r.xMax, pts.Max(p => p.x) + 6);
            float y0 = Mathf.Max(r.yMin, pts.Min(p => p.y) - 6), y1 = Mathf.Min(r.yMax, pts.Max(p => p.y) + 6);
            return Rect.MinMaxRect(x0, y0, Mathf.Max(x0, x1), Mathf.Max(y0, y1));
        }

        static bool Pareil(Rect? a, Rect? b) =>
            a == null ? b == null : b != null && Vector2.Distance(a.Value.min, b.Value.min) < 1 && Vector2.Distance(a.Value.max, b.Value.max) < 1;

        void Dessiner()
        {
            foreach (var g in _ombre) Destroy(g);
            _ombre.Clear();
            var r = Voile.rect;
            var sombre = new Color(0, 0, 0, 0.7f);   // 0,45 restait pâle à l'écran (vu en Play le 25/10)
            if (_zone == null)
            {
                if (bloquer) Bloc(r.xMin, r.yMin, r.xMax, r.yMax, sombre, true);
            }
            else
            {
                var z = _zone.Value;
                Bloc(r.xMin, z.yMax, r.xMax, r.yMax, sombre, bloquer);
                Bloc(r.xMin, r.yMin, r.xMax, z.yMin, sombre, bloquer);
                Bloc(r.xMin, z.yMin, z.xMin, z.yMax, sombre, bloquer);
                Bloc(z.xMax, z.yMin, r.xMax, z.yMax, sombre, bloquer);
                if (bloquer) Bloc(z.xMin, z.yMin, z.xMax, z.yMax, new Color(0, 0, 0, 0), true);
                Bloc(z.xMin, z.yMax - 2, z.xMax, z.yMax, Color.white, false);
                Bloc(z.xMin, z.yMin, z.xMax, z.yMin + 2, Color.white, false);
                Bloc(z.xMin, z.yMin, z.xMin + 2, z.yMax, Color.white, false);
                Bloc(z.xMax - 2, z.yMin, z.xMax, z.yMax, Color.white, false);
            }
            _bulle.SetAsLastSibling();
            float w = _bulle.rect.width, h = _bulle.rect.height;
            // Un guide sans zone (fenêtre fermée entre-temps) : la bulle en bas, sans rien cacher.
            _bulle.anchoredPosition = _zone == null && !bloquer
                ? new Vector2(r.center.x, r.yMin + 24 + h / 2)
                : Placer(_zone, new Vector2(w, h), r);
        }

        void Bloc(float xa, float ya, float xb, float yb, Color couleur, bool arrete)
        {
            var rt = UIFactory.Rect("Bloc", transform);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(Mathf.Max(0, xb - xa), Mathf.Max(0, yb - ya));
            rt.anchoredPosition = new Vector2((xa + xb) / 2, (ya + yb) / 2);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = couleur;
            img.raycastTarget = arrete;
            _ombre.Add(rt.gameObject);
        }

        void Construire()
        {
            if (_bulle != null) Destroy(_bulle.gameObject);
            var e = etapes[_n];
            var visibles = Enumerable.Range(0, etapes.Count).Where(i => !Sautee(i)).ToList();
            int rang = visibles.IndexOf(_n) + 1, total = visibles.Count;
            bool premiere = rang == 1, derniere = rang == total;

            var fond = UIFactory.Panel("Bulle", transform, UITheme.Carte);
            UIFactory.Border(fond.gameObject);
            _bulle = (RectTransform)fond.transform;
            _bulle.anchorMin = _bulle.anchorMax = new Vector2(.5f, .5f);
            _bulle.sizeDelta = new Vector2(460, 10);
            var v = fond.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 12, 12); v.spacing = 6;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            fond.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (entete != null) UIFactory.Text(fond.transform, $"{entete} · étape {rang} sur {total}", UITheme.Role.Aide, UITheme.Primaire);
            UIFactory.Text(fond.transform, e.titre, UITheme.Role.Libelle, UITheme.TextePrincipal, true);
            UIFactory.Text(fond.transform, Insecable(e.texte), UITheme.Role.Donnee, UITheme.TexteSecondaire);
            bool attend = e.fait != null && _aEteFaux;   // sinon : déjà fait, « Suivant »
            _attente = attend && e.attente != null ? UIFactory.Text(fond.transform, Attente(e), UITheme.Role.Aide, UITheme.Primaire) : null;

            var pied = UIFactory.HBox(fond.transform, 8, false, "Pied");
            pied.childAlignment = TextAnchor.MiddleLeft;
            if (entete == null && total > 1)
                UIFactory.Text(pied.transform, $"{rang} / {total}", UITheme.Role.Aide, UITheme.TexteSecondaire).enableWordWrapping = false;
            string q = e.quitter ?? quitter;
            if (q != null)
            {
                var lien = UIFactory.Button(pied.transform, q, UITheme.Carte, UITheme.Primaire, 30, UITheme.Role.Aide, false);
                lien.GetComponentInChildren<TMP_Text>(true).fontStyle = FontStyles.Underline;
                lien.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                UIFactory.LargeurDuTexte(lien);
                lien.onClick.AddListener(() => Finir(false));
            }
            UIFactory.LE(UIFactory.Rect("Espace", pied.transform).gameObject, flexW: 1);
            if (!premiere)
            {
                var prec = UIFactory.Button(pied.transform, "‹ Précédent", UITheme.Carte, UITheme.TextePrincipal, 34, UITheme.Role.Bouton);
                UIFactory.Border(prec.gameObject);
                UIFactory.LargeurDuTexte(prec);
                prec.onClick.AddListener(() => Aller(_n - 1, -1));
            }
            _suivant = !attend || e.pret?.Invoke() == true;
            if (_suivant)
            {
                var suiv = UIFactory.Button(pied.transform, e.suivant ?? (derniere ? "Terminer" : "Suivant ›"), UITheme.Primaire, Color.white, 34, UITheme.Role.Bouton);
                UIFactory.LargeurDuTexte(suiv);
                suiv.onClick.AddListener(() => Aller(_n + 1, +1));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bulle);
        }

        static string Attente(Etape e)
        {
            string m = e.manque?.Invoke();
            return Insecable(string.IsNullOrEmpty(m) ? e.attente : e.attente + "\n" + m);
        }

        /// « Compte » ne se coupe plus avant son guillemet fermant (vu en Play le 25/10).
        static string Insecable(string s) => s?.Replace("« ", "« ").Replace(" »", " »");

        /// La zone hors de la partie visible d'une liste qui défile : on la fait venir.
        static void Defiler(RectTransform c)
        {
            var sr = c.GetComponentInParent<ScrollRect>();
            if (sr == null || sr.content == null || !sr.vertical) return;
            Canvas.ForceUpdateCanvases();
            var vp = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
            var b = RectTransformUtility.CalculateRelativeRectTransformBounds(vp, c);
            var v = vp.rect;
            float dy = b.max.y > v.yMax - 20 ? b.max.y - v.yMax + 40 : b.min.y < v.yMin + 20 ? b.min.y - v.yMin - 40 : 0;
            if (dy == 0) return;
            sr.StopMovement();
            sr.content.anchoredPosition -= new Vector2(0, dy);
        }
    }
}
