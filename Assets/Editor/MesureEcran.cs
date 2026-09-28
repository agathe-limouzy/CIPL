using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Mesure l'écran réellement affiché, en Play.
///
/// Les écrans « menu général », « fiches résumé » et « facturation » sont
/// construits au runtime par code, pas depuis un prefab : aucune mesure hors
/// Play ne dit ce qu'ils valent. Cet outil se lance pendant le Play, sur
/// l'écran ouvert, et signale trois anomalies mesurées — pas supposées :
///
///  · du blanc : un conteneur nettement plus haut que la somme de ses enfants ;
///  · de l'invisible : un objet actif de hauteur nulle ;
///  · un texte qui réclame une ligne de plus à cause d'un retour à la ligne final.
public static class MesureEcran
{
    const string Sortie = "C:/Users/agath/AppData/Local/Temp/claude/ecran.txt";

    // Qualifié : le projet a déjà une classe nommée MenuItem.
    [UnityEditor.MenuItem("CIPL/Mesurer l'écran affiché")]
    public static void Mesurer()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== MESURE DE L'ECRAN AFFICHE ===");
        sb.AppendLine($"  Play : {Application.isPlaying}");

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Ecrire(sb.AppendLine("  aucun Canvas")); return; }

        int blancs = 0, invisibles = 0, textes = 0;
        var lignes = new System.Collections.Generic.List<string>();

        foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(false))
        {
            if (!rt.gameObject.activeInHierarchy) continue;

            string chemin = rt.name;
            for (var t = rt.parent; t != null && t != canvas.transform; t = t.parent)
                chemin = t.name + "/" + chemin;

            // Texte avec blanc final : TMP réclame alors une ligne de plus.
            var tmp = rt.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                string s = tmp.text ?? "";
                if (s.Trim().Length > 0 && s != s.TrimEnd('\n', '\r', ' ', '\t'))
                { textes++; lignes.Add($"  [TEXTE]      {chemin}"); }
            }

            // Objet actif mais sans hauteur : il occupe une place dans l'arbre
            // sans rien montrer.
            if (rt.rect.height < 0.5f && rt.childCount > 0)
            { invisibles++; lignes.Add($"  [INVISIBLE]  {chemin}"); continue; }

            // Blanc : le conteneur est nettement plus haut que ce qu'il contient.
            var lg = rt.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (lg is VerticalLayoutGroup v && rt.childCount > 0)
            {
                float somme = v.padding.top + v.padding.bottom;
                int n = 0;
                foreach (RectTransform e in rt)
                    if (e.gameObject.activeSelf) { somme += e.rect.height; n++; }
                if (n > 0) somme += v.spacing * (n - 1);

                float ecart = rt.rect.height - somme;
                if (ecart > 24f)
                { blancs++; lignes.Add($"  [BLANC {ecart,5:F0}px] {chemin}"); }
            }
        }

        sb.AppendLine($"  blancs : {blancs}   invisibles : {invisibles}   textes fautifs : {textes}");
        sb.AppendLine();
        foreach (var l in lignes.Take(60)) sb.AppendLine(l);
        if (lignes.Count > 60) sb.AppendLine($"  … et {lignes.Count - 60} autres");
        Ecrire(sb);
    }

    static void Ecrire(StringBuilder sb)
    {
        File.WriteAllText(Sortie, sb.ToString());
        Debug.Log($"[MesureEcran] écrit dans {Sortie}");
    }
}
