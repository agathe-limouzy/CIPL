using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// L'échelle des tailles de texte.
///
/// L'inventaire du 23/09 a trouvé vingt tailles distinctes, de 11 à 30 pt, pour
/// six rôles seulement. Elles ont été rattachées à six constantes de UITheme.
/// Ces tests empêchent la dérive de revenir : une taille réécrite en dur dans
/// le code, et la vingt-et-unième valeur recommence à pousser.
public class EchelleTypoTests
{
    // ── L'échelle elle-même ────────────────────────────────────────────────────

    [Test]
    public void L_echelle_compte_six_tailles_croissantes()
    {
        Assert.That(UITheme.Tailles, Has.Length.EqualTo(6));
        Assert.That(UITheme.Tailles, Is.Ordered.Ascending);
    }

    [Test]
    public void Une_taille_de_l_echelle_se_rattache_a_elle_meme()
    {
        foreach (float t in UITheme.Tailles)
            Assert.That(UITheme.TailleProche(t), Is.EqualTo(t));
    }

    /// À égale distance, la plus petite : rattacher un texte à l'échelle ne doit
    /// jamais le faire grossir par surprise.
    [Test]
    public void A_egale_distance_on_choisit_la_plus_petite()
    {
        Assert.That(UITheme.TailleProche(13.5f), Is.EqualTo(UITheme.TailleLegende), "12 ou 15");
        Assert.That(UITheme.TailleProche(19f), Is.EqualTo(UITheme.TailleLibelle), "18 ou 20");
        Assert.That(UITheme.TailleProche(22f), Is.EqualTo(UITheme.TailleValeur), "20 ou 24");
    }

    [Test]
    public void Les_valeurs_hors_echelle_se_rattachent_au_plus_proche()
    {
        Assert.That(UITheme.TailleProche(11f), Is.EqualTo(UITheme.TailleLegende));
        Assert.That(UITheme.TailleProche(30f), Is.EqualTo(UITheme.TaillePage));
    }

    // ── Les rôles ──────────────────────────────────────────────────────────────

    /// Un rôle peut changer de taille, mais jamais sortir de l'échelle : sinon
    /// la vingt-et-unième valeur revient par la porte des rôles.
    [Test]
    public void Chaque_role_pointe_vers_une_taille_de_l_echelle()
    {
        var champs = typeof(UITheme.Role).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.That(champs, Is.Not.Empty);
        foreach (var c in champs)
            Assert.That(UITheme.Tailles, Does.Contain((float)c.GetValue(null)),
                $"le rôle {c.Name} vaut {c.GetValue(null)}, hors de l'échelle");
    }

    /// Le code désigne un rôle, jamais une taille : c'est ce qui permet de changer
    /// la taille d'un rôle partout d'un coup, et de relire un classement.
    [Test]
    public void Le_code_designe_un_role_jamais_une_taille()
    {
        string racine = Path.Combine(Application.dataPath, "Script");
        var fautifs = new List<string>();
        var motif = new Regex(@"UITheme\.Taille(Legende|Secondaire|Libelle|Valeur|Section|Page)\b");

        foreach (var f in Directory.GetFiles(racine, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(f) == "UITheme.cs") continue;   // l'échelle y est définie
            var lignes = File.ReadAllLines(f);
            for (int i = 0; i < lignes.Length; i++)
                if (motif.IsMatch(lignes[i]))
                    fautifs.Add($"{Path.GetFileName(f)}:{i + 1}");
        }

        Assert.That(fautifs, Is.Empty,
            "taille désignée directement — utiliser UITheme.Role.… :\n  " + string.Join("\n  ", fautifs));
    }

    // ── Le code ne réécrit plus de taille en dur ──────────────────────────────

    /// Le détecteur lui-même, sur des extraits : s'il ne voit rien ici, le test
    /// suivant ne prouverait rien en passant.
    [Test]
    public void Le_detecteur_trouve_une_taille_en_dur()
    {
        Assert.That(TaillesEnDur("UIFactory.Text(p, \"a\", 14, c);"), Has.Count.EqualTo(1), "Text");
        Assert.That(TaillesEnDur("t.fontSize = 16;"), Has.Count.EqualTo(1), "affectation");
        Assert.That(TaillesEnDur("UIFactory.Button(p, \"a\", bg, fg, 40, 18);"), Has.Count.EqualTo(1), "Button");
        Assert.That(TaillesEnDur("UIFactory.Input(p, \"a\", fontSize: 20);"), Has.Count.EqualTo(1), "argument nommé");
        Assert.That(TaillesEnDur("t.fontSizeMin = 18; t.fontSizeMax = 30;"), Has.Count.EqualTo(2), "bornes d'autosize");
    }

    /// Et il ne confond pas une taille avec autre chose : la hauteur d'un bouton,
    /// une composante de couleur, une constante de l'échelle.
    [Test]
    public void Le_detecteur_ignore_ce_qui_n_est_pas_une_taille()
    {
        Assert.That(TaillesEnDur("UIFactory.Text(p, \"a\", UITheme.TailleValeur, c);"), Is.Empty, "constante");
        Assert.That(TaillesEnDur("UIFactory.Button(p, \"a\", new Color(1, 1, 1, 0.9f), fg, 40);"), Is.Empty,
            "hauteur et couleur");
        Assert.That(TaillesEnDur("UIFactory.Text(p, $\"{n}, total\", size, c);"), Is.Empty, "variable");
    }

    [Test]
    public void Aucune_taille_de_police_n_est_ecrite_en_dur_dans_le_code()
    {
        string racine = Path.Combine(Application.dataPath, "Script");
        var fautifs = new List<string>();

        foreach (var f in Directory.GetFiles(racine, "*.cs", SearchOption.AllDirectories))
        {
            // L'échelle se définit forcément avec des nombres.
            if (Path.GetFileName(f) == "UITheme.cs") continue;
            foreach (var e in TaillesEnDur(File.ReadAllText(f)))
                fautifs.Add($"{Path.GetFileName(f)} : {e}");
        }

        Assert.That(fautifs, Is.Empty,
            "taille de police écrite en dur — utiliser UITheme.Role.… :\n  " + string.Join("\n  ", fautifs));
    }

    // ── Les prefabs ────────────────────────────────────────────────────────────
    //
    // Rattachés à l'échelle en trois lots (23–24/09), la scène au quatrième. Une
    // taille hors échelle posée dans l'inspecteur, ou une instance qui réécrit la
    // taille de sa source, et la dérive recommence — sans que le code y soit pour rien.

    [Test]
    public void Le_controle_des_prefabs_voit_une_taille_hors_echelle()
    {
        var go = new GameObject("Essai", typeof(RectTransform));
        try
        {
            var t = go.AddComponent<TMPro.TextMeshProUGUI>();
            t.fontSize = UITheme.Role.Donnee;
            Assert.That(HorsEchelle(go), Is.Empty, "taille de l'échelle");
            t.fontSize = 13f;
            Assert.That(HorsEchelle(go), Has.Count.EqualTo(1), "13 pt");
            t.fontSize = UITheme.Role.Donnee;
            t.enableAutoSizing = true; t.fontSizeMax = 27f;
            Assert.That(HorsEchelle(go), Has.Count.EqualTo(1), "autosize plafonné hors échelle");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Tout_texte_des_prefabs_est_sur_l_echelle()
    {
        var fautifs = new List<string>();
        foreach (var (nom, racine) in Prefabs())
            fautifs.AddRange(HorsEchelle(racine).Select(f => $"[{nom}] {f}"));
        Assert.That(fautifs, Is.Empty, "taille hors échelle — choisir le rôle du texte :\n  " + string.Join("\n  ", fautifs));
    }

    /// Une instance qui réécrit la taille masque sa source : on corrige le prefab
    /// source, rien ne bouge à l'écran, et on y perd une journée.
    [Test]
    public void Aucune_instance_ne_reecrit_la_taille_de_sa_source()
    {
        var fautifs = new List<string>();
        foreach (var (nom, racine) in Prefabs())
            foreach (var t in racine.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(t) == null) continue;
                if (new UnityEditor.SerializedObject(t).FindProperty("m_fontSize").prefabOverride)
                    fautifs.Add($"[{nom}] {Chemin(t.transform, racine.transform)} : {t.fontSize}");
            }
        Assert.That(fautifs, Is.Empty, "taille surchargée sur une instance — la porter à la source :\n  " + string.Join("\n  ", fautifs));
    }

    // ── La scène ───────────────────────────────────────────────────────────────
    //
    // Lue dans son fichier : l'ouvrir depuis un test remplacerait la scène ouverte
    // dans l'éditeur. Un texte TMP y écrit « m_fontSize: 14 » ; une instance de
    // prefab qui réécrit la taille, « propertyPath: m_fontSize ».

    [Test]
    public void Le_controle_de_la_scene_voit_taille_et_surcharge()
    {
        const string extrait =
            "--- !u!114 &1\nMonoBehaviour:\n  m_fontSize: 14\n  m_enableAutoSizing: 0\n" +
            "--- !u!114 &2\nMonoBehaviour:\n  m_fontSize: 15\n  m_enableAutoSizing: 1\n  m_fontSizeMax: 30\n" +
            "--- !u!114 &3\nMonoBehaviour:\n  m_fontSize: 20\n  m_enableAutoSizing: 0\n  m_fontSizeMax: 72\n" +
            "--- !u!1001 &4\nPrefabInstance:\n    - target: {fileID: 9}\n      propertyPath: m_fontSize\n      value: 72\n";
        Assert.That(HorsEchelleScene(extrait), Has.Count.EqualTo(3),
            "14 pt, autosize plafonné à 30, surcharge — mais pas le plafond d'un autosize éteint");
    }

    [Test]
    public void Tout_texte_de_la_scene_est_sur_l_echelle()
    {
        string scene = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes/SampleScene.unity"));
        var fautifs = HorsEchelleScene(scene);
        Assert.That(fautifs, Is.Empty, "SampleScene — choisir le rôle du texte :\n  " + string.Join("\n  ", fautifs));
    }

    static List<string> HorsEchelleScene(string yaml)
    {
        var fautifs = new List<string>();
        float Lire(Match m) => float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        foreach (var bloc in yaml.Split(new[] { "--- !u!" }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string id = bloc.Split('\n')[0].Trim();
            var taille = Regex.Match(bloc, @"\n  m_fontSize: ([\d.]+)");
            if (taille.Success && !UITheme.Tailles.Contains(Lire(taille)))
                fautifs.Add($"{id} : {taille.Groups[1].Value}");
            var max = Regex.Match(bloc, @"\n  m_fontSizeMax: ([\d.]+)");
            if (Regex.IsMatch(bloc, @"\n  m_enableAutoSizing: 1") && max.Success && !UITheme.Tailles.Contains(Lire(max)))
                fautifs.Add($"{id} : autosize jusqu'à {max.Groups[1].Value}");
            foreach (Match m in Regex.Matches(bloc, @"propertyPath: m_fontSize\r?\n\s*value: ([\d.]+)"))
                fautifs.Add($"{id} : surcharge d'instance à {m.Groups[1].Value} — la porter à la source");
        }
        return fautifs;
    }

    static IEnumerable<(string nom, GameObject racine)> Prefabs()
    {
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" }))
        {
            string chemin = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            yield return (Path.GetFileNameWithoutExtension(chemin), UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(chemin));
        }
    }

    /// Autosize toléré, à condition d'être plafonné à une taille de l'échelle :
    /// il ne sert qu'à rétrécir un montant trop long pour sa colonne.
    static List<string> HorsEchelle(GameObject racine)
    {
        var fautifs = new List<string>();
        foreach (var t in racine.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (!UITheme.Tailles.Contains(t.fontSize))
                fautifs.Add($"{Chemin(t.transform, racine.transform)} : {t.fontSize}");
            else if (t.enableAutoSizing && !UITheme.Tailles.Contains(t.fontSizeMax))
                fautifs.Add($"{Chemin(t.transform, racine.transform)} : autosize jusqu'à {t.fontSizeMax}");
        }
        return fautifs;
    }

    static string Chemin(Transform t, Transform racine)
    {
        string p = t.name;
        for (var h = t.parent; h != null && h != racine; h = h.parent) p = h.name + "/" + p;
        return p;
    }

    // ── Détection ──────────────────────────────────────────────────────────────

    static readonly Regex Nombre = new Regex(@"^(?:fontSize\s*:\s*)?\d+(?:\.\d+)?f?$");

    /// Les tailles littérales passées à UIFactory ou affectées à fontSize.
    /// Rang de l'argument de taille : Text(parent, texte, TAILLE), Button(parent,
    /// label, fond, texte, hauteur, TAILLE), Input(parent, invite, hauteur,
    /// multiligne, TAILLE) — ou n'importe où s'il est nommé « fontSize: ».
    static List<string> TaillesEnDur(string src)
    {
        var trouvees = new List<string>();

        foreach (var (fonction, rang) in new[] { ("Text", 2), ("Button", 5), ("Input", 4) })
        {
            foreach (Match m in Regex.Matches(src, @"UIFactory\." + fonction + @"\("))
            {
                var args = Arguments(src, m.Index + m.Length - 1);
                string cible = args.FirstOrDefault(a => a.StartsWith("fontSize"))
                            ?? (args.Count > rang ? args[rang] : null);
                if (cible != null && Nombre.IsMatch(cible))
                    trouvees.Add($"UIFactory.{fonction}(… {cible} …)");
            }
        }

        // Les bornes d'autosize aussi : un plafond à 30 affiche du 30, quelle que
        // soit la taille nominale (c'est ainsi que 27 et 30 étaient passés).
        foreach (Match m in Regex.Matches(src, @"\.(fontSize(?:Min|Max)?)\s*=\s*(\d+(?:\.\d+)?f?)\s*;"))
            trouvees.Add($"{m.Groups[1].Value} = {m.Groups[2].Value}");

        return trouvees;
    }

    /// Découpe les arguments d'un appel, en respectant parenthèses et chaînes :
    /// « new Color(1, 1, 1) » compte pour un seul argument.
    static List<string> Arguments(string src, int ouvrante)
    {
        var args = new List<string>();
        int prof = 0, debut = ouvrante + 1;
        bool chaine = false;

        for (int i = ouvrante; i < src.Length; i++)
        {
            char c = src[i];
            if (chaine)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') chaine = false;
                continue;
            }
            if (c == '"') { chaine = true; continue; }
            if (c == '(' || c == '[' || c == '{') { prof++; continue; }
            if (c == ')' || c == ']' || c == '}')
            {
                if (--prof == 0) { args.Add(src.Substring(debut, i - debut).Trim()); break; }
                continue;
            }
            if (c == ',' && prof == 1) { args.Add(src.Substring(debut, i - debut).Trim()); debut = i + 1; }
        }
        return args;
    }
}
