// Balayage de l'interface en Play (Revue 17, 03/11/2026) — à coller dans l'outil script-execute (mode « snippet »),
// application en Play. Fichier hors de Assets : Unity ne le compile pas.
//
// Ce que fait le snippet en bas du fichier : ouvre un écran par un clic simulé sur un bouton (libellé exact), puis, pour
// chaque zoom, attend que l'interface se recalcule, mesure et prend une capture ; remet le zoom d'origine ; referme la
// fenêtre par SON bouton (« Retour », « Fermer », « Annuler », « × » cherchés DANS la fenêtre ouverte).
//
// Précautions :
// - ne jamais chercher « × » dans tout l'écran : c'est aussi la croix « Quitter » de l'application (BtnQuitter) ;
// - UIZoom.Facteur est enregistré dans les préférences : le remettre à sa valeur d'origine (bloc finally) ;
// - un message « Modifications non enregistrées » se ferme par « Annuler » (BtnAnnuler), jamais par « Quitter sans enregistrer » ;
// - ouvrir une fenêtre réserve la fiche sur le cloud : toujours la refermer par son propre bouton.
// `await Task.Delay` fonctionne dans script-execute et reste sur le fil principal (les images avancent pendant l'attente).
//
// Détection : DEBORDE (texte plus large, ou plus haut de plus de 25 %, que sa zone, hors « … » voulus et hors valeur d'un champ
// de saisie, qui défile) ; CHEVAUCHE (deux voisins hors mise en page automatique qui se recouvrent) ; HORS_ECRAN (hors de la
// fenêtre, contenu des zones masquées exclu) ; PETIT (auto-taille sous 11 px). Captures : revue17_<nom>_<zoom>.png dans
// Application.persistentDataPath.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class Audit
{
    public static Dictionary<string, string> Detail = new Dictionary<string, string>();
    public static Dictionary<string, List<string>> Zooms = new Dictionary<string, List<string>>();
    public static string[] Ignorer = { "Scrollbar Vertical", "Batiment Manager /TabBar" };   // défauts déjà connus (B-01, B-09)
    public static string Prefixe = "";

    public static string Run(Transform racine, float z)
    {
        Canvas.ForceUpdateCanvases();
        int nT = 0, nD = 0, nE = 0, nC = 0, nH = 0;
        var canvas = racine.GetComponentInParent<Canvas>().rootCanvas;
        Rect ecr = RectMonde(canvas.GetComponent<RectTransform>());
        foreach (var t in racine.GetComponentsInChildren<TMP_Text>(false))
        {
            if (!t.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(t.text)) continue;
            var inp = t.GetComponentInParent<TMP_InputField>();
            if (inp != null && inp.textComponent == t) continue;
            var rt = t.rectTransform;
            if (rt.rect.width <= 1f || rt.rect.height <= 1f) continue;
            nT++;
            t.ForceMeshUpdate();
            bool large = !t.enableWordWrapping && !t.enableAutoSizing && t.preferredWidth > rt.rect.width + 2f;
            bool haut = !t.enableAutoSizing && t.preferredHeight > rt.rect.height * 1.25f + 2f;
            if (!(large || haut)) { if (t.enableAutoSizing && t.fontSize < 11f) { nD++; Add("PETIT | " + Chemin(t.transform) + " | « " + Court(t.text) + " »", $"taille {t.fontSize:0.#}", z); } continue; }
            if (t.overflowMode == TextOverflowModes.Ellipsis || t.overflowMode == TextOverflowModes.Truncate) { nE++; continue; }
            nD++; Add("DEBORDE | " + Chemin(t.transform) + " | « " + Court(t.text) + " »", $"rect {rt.rect.width:0}x{rt.rect.height:0} préféré {t.preferredWidth:0}x{t.preferredHeight:0} wrap={t.enableWordWrapping}", z);
        }
        foreach (var parent in racine.GetComponentsInChildren<RectTransform>(false))
        {
            if (parent == canvas.transform || parent.GetComponent<LayoutGroup>() != null || parent.childCount < 2) continue;
            var fr = new List<(RectTransform rt, Rect r)>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i) as RectTransform;
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                var b = Bornes(c);
                if (b.HasValue) fr.Add((c, b.Value));
            }
            for (int i = 0; i < fr.Count; i++)
                for (int j = i + 1; j < fr.Count; j++)
                {
                    var x = Inter(fr[i].r, fr[j].r);
                    if (x.width > 3f && x.height > 3f && !Contient(fr[i].r, fr[j].r) && !Contient(fr[j].r, fr[i].r))
                    { nC++; Add("CHEVAUCHE | " + Chemin(fr[i].rt) + " <> " + fr[j].rt.name, $"commun {x.width:0}x{x.height:0}", z); }
                }
        }
        foreach (var g in racine.GetComponentsInChildren<Graphic>(false))
        {
            if (!g.gameObject.activeInHierarchy || g.GetComponentInParent<RectMask2D>() != null || g.GetComponentInParent<Mask>() != null) continue;
            var r = RectMonde(g.rectTransform);
            if (r.width <= 1f || r.height <= 1f) continue;
            if (r.xMin < ecr.xMin - 1f || r.xMax > ecr.xMax + 1f || r.yMin < ecr.yMin - 1f || r.yMax > ecr.yMax + 1f)
            { nH++; Add("HORS_ECRAN | " + Chemin(g.transform), $"x {r.xMin:0}..{r.xMax:0} y {r.yMin:0}..{r.yMax:0}", z); }
        }
        return $"{Prefixe} zoom {z:0.##} : textes={nT} débordements={nD} ellipses={nE} chevauchements={nC} hors_écran={nH}";
    }

    static void Add(string cle, string det, float z)
    {
        if (Ignorer.Any(i => cle.Contains(i))) return;
        cle = Prefixe + " | " + cle;
        if (!Detail.ContainsKey(cle)) { Detail[cle] = det + " (au zoom " + z.ToString("0.##") + ")"; Zooms[cle] = new List<string>(); }
        var zs = z.ToString("0.##");
        if (!Zooms[cle].Contains(zs)) Zooms[cle].Add(zs);
    }
    static Rect RectMonde(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
    static Rect? Bornes(RectTransform racine)
    {
        Rect? res = null;
        foreach (var g in racine.GetComponentsInChildren<Graphic>(false))
        {
            if (!g.gameObject.activeInHierarchy || !g.enabled || g.color.a < 0.05f) continue;
            if (g is TMP_Text tx && string.IsNullOrWhiteSpace(tx.text)) continue;
            var r = RectMonde(g.rectTransform);
            if (r.width <= 1f || r.height <= 1f) continue;
            res = res.HasValue ? Rect.MinMaxRect(Mathf.Min(res.Value.xMin, r.xMin), Mathf.Min(res.Value.yMin, r.yMin), Mathf.Max(res.Value.xMax, r.xMax), Mathf.Max(res.Value.yMax, r.yMax)) : r;
        }
        return res;
    }
    static Rect Inter(Rect a, Rect b) { float x1 = Mathf.Max(a.xMin, b.xMin), y1 = Mathf.Max(a.yMin, b.yMin), x2 = Mathf.Min(a.xMax, b.xMax), y2 = Mathf.Min(a.yMax, b.yMax); return x2 > x1 && y2 > y1 ? Rect.MinMaxRect(x1, y1, x2, y2) : Rect.zero; }
    static bool Contient(Rect a, Rect b) => a.xMin <= b.xMin + 1f && a.yMin <= b.yMin + 1f && a.xMax >= b.xMax - 1f && a.yMax >= b.yMax - 1f;
    static string Chemin(Transform t) { var p = new List<string>(); for (var x = t; x != null && p.Count < 4; x = x.parent) p.Add(x.name); p.Reverse(); return string.Join("/", p); }
    static string Court(string s) { s = s.Replace("\n", " ").Replace("\r", ""); return s.Length > 40 ? s.Substring(0, 40) + "…" : s; }

    /// Bouton actif dont un texte vérifie `test`, cherché dans `dans` seulement (jamais la croix Quitter).
    public static Button Bouton(System.Func<string, bool> test, Transform dans)
        => dans.GetComponentsInChildren<Button>(false)
            .Where(x => x.isActiveAndEnabled && x.interactable && x.name != "BtnQuitter")
            .FirstOrDefault(x => x.GetComponentsInChildren<TMP_Text>(false).Any(t => test(t.text.Trim())));

    /// Ferme un message à « OK » ; un message à deux boutons est annulé (BtnAnnuler) et signalé.
    public static string Dialogue()
    {
        var d = ConfirmDialog.Instance;
        if (d == null || !d.gameObject.activeSelf) return "";
        var msg = string.Join(" / ", d.GetComponentsInChildren<TMP_Text>(false).Select(t => t.text.Replace("\n", " ")).Where(s => s.Length > 20));
        var ok = d.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.GetComponentInChildren<TMP_Text>()?.text.Trim() == "OK");
        if (ok != null) { ok.onClick.Invoke(); return msg.Length > 60 ? msg.Substring(0, 60) + "…" : msg; }
        d.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "BtnAnnuler")?.onClick.Invoke();
        return "ANNULÉ : " + (msg.Length > 70 ? msg.Substring(0, 70) : msg);
    }
}

// ── Snippet : adapter la liste des libellés de boutons à ouvrir (ici, des fenêtres de la fiche du bâtiment) ──
var canvasRoot = GameObject.Find("Canvas").transform;
var actions = new[] { "+ Charges", "Photos" };
float origine = UIZoom.Facteur;
var sb = new StringBuilder();
try
{
    foreach (var a in actions)
    {
        var avant = new HashSet<GameObject>(Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Where(r => r.gameObject.activeInHierarchy).Select(r => r.gameObject));
        var b = Audit.Bouton(s => s == a, canvasRoot);
        if (b == null) { sb.AppendLine(a + " : bouton introuvable"); continue; }
        b.onClick.Invoke();
        await Task.Delay(1500);
        var d = Audit.Dialogue(); if (d != "") sb.AppendLine(a + " : message « " + d + " »");
        var nouveaux = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Where(r => r.gameObject.activeInHierarchy && !avant.Contains(r.gameObject)).Select(r => r.gameObject).ToList();
        var tete = nouveaux.Where(g => g.transform.parent == null || !nouveaux.Contains(g.transform.parent.gameObject)).OrderByDescending(g => g.GetComponentsInChildren<RectTransform>().Length).FirstOrDefault();
        Transform panneau = tete != null ? tete.transform : canvasRoot;
        Audit.Prefixe = a;
        foreach (var z in new[] { 1f, 1.25f, 1.5f, 1.75f })
        {
            UIZoom.Facteur = z;
            await Task.Delay(450);
            sb.AppendLine(Audit.Run(panneau, z));
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath, $"revue17_{a.Replace("+ ", "").Split(' ')[0]}_{z:0.00}.png"));
            await Task.Delay(250);
        }
        UIZoom.Facteur = origine;
        await Task.Delay(300);
        var f = panneau != canvasRoot ? Audit.Bouton(s => s.Contains("Retour") || s == "Fermer" || s == "Annuler" || s == "×", panneau) : null;
        f?.onClick.Invoke();
        await Task.Delay(800);
        var d2 = Audit.Dialogue(); if (d2 != "") sb.AppendLine(a + " (fermeture) : message « " + d2 + " »");
        sb.AppendLine(a + " : fenêtre " + panneau.name + ", fermée par « " + (f != null ? f.name : "rien : à fermer à la main") + " »");
    }
}
finally { UIZoom.Facteur = origine; }
foreach (var k in Audit.Detail.Keys.OrderBy(k => k).Take(40))
    sb.AppendLine(k + " | " + Audit.Detail[k] + " | zooms " + string.Join(" ", Audit.Zooms[k]));
"zoom remis à " + UIZoom.Facteur + "\n" + sb
