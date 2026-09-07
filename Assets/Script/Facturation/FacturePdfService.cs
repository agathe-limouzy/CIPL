using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;

/// Génère le PDF d'une facture au visuel de la vraie facture CIPL :
/// remplit le template HTML (StreamingAssets/facture_template.html) puis le rend
/// en PDF via Microsoft Edge en mode headless (print-to-pdf). Local uniquement —
/// aucune émission Pennylane ici.
public static class FacturePdfService
{
    public static string TemplatePath =>
        Path.Combine(Application.streamingAssetsPath, "facture_template.html");

    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// Données à injecter dans le template.
    public class Data
    {
        public string clientNom, clientAdresseHtml, clientSiret;
        public string refInterne;   // texte libre (ex. « N° Interne Magasin 001048 »), optionnel
        public string dateStr, numero, subtitle, bodyHtml, sommeDate;
        public float totalPeriode, provision, totalHT, tva, ttc;
        public bool tvaDebit, retard;
        public string ribTitulaire, ribDomiciliation, ribNum, ribIban, ribBic;
        public string legal, foot1, foot2;
    }

    public static string BuildHtml(Data d)
    {
        string t = File.ReadAllText(TemplatePath);
        return t
            .Replace("{{LOGO_SRC}}", LogoDataUri())
            .Replace("{{CLIENT_NOM}}", H(d.clientNom))
            .Replace("{{CLIENT_ADRESSE}}", d.clientAdresseHtml ?? "")   // déjà en HTML (<br>)
            .Replace("{{CLIENT_SIRET}}", H(d.clientSiret))
            .Replace("{{REF_INTERNE}}", string.IsNullOrWhiteSpace(d.refInterne)
                ? "" : $"<div class=\"refint\">{H(d.refInterne)}</div>")
            .Replace("{{DATE}}", H(d.dateStr))
            .Replace("{{NUMERO}}", H(d.numero))
            .Replace("{{BODY}}", d.bodyHtml ?? "")                       // déjà en HTML (<p>)
            .Replace("{{SUBTITLE}}", H(d.subtitle))
            .Replace("{{TOTAL_PERIODE}}", Euro(d.totalPeriode))
            .Replace("{{PROVISION_ROW}}", d.provision > 0f
                ? $"<tr><td>Provision pour charges</td><td class=\"r\">{Euro(d.provision)}</td></tr>" : "")
            .Replace("{{TOTAL_HT}}", Euro(d.totalHT))
            .Replace("{{TVA}}", Euro(d.tva))
            .Replace("{{TTC}}", Euro(d.ttc))
            .Replace("{{TVA_DEBIT}}", d.tvaDebit
                ? "<div class=\"tva\">&nbsp;la TVA est pay&eacute;e sur les d&eacute;bits</div>" : "")
            .Replace("{{SOMME_DATE}}", H(d.sommeDate))
            .Replace("{{RIB_TITULAIRE}}", H(d.ribTitulaire))
            .Replace("{{RIB_DOMICILIATION}}", H(d.ribDomiciliation))
            .Replace("{{RIB_NUM}}", H(d.ribNum))
            .Replace("{{RIB_IBAN}}", H(d.ribIban))
            .Replace("{{RIB_BIC}}", H(d.ribBic))
            .Replace("{{LEGAL}}", d.retard ? H(d.legal) : "")
            .Replace("{{FOOT1}}", H(d.foot1))
            .Replace("{{FOOT2}}", H(d.foot2));
    }

    /// Écrit le PDF à `pdfPath`. Renvoie true si OK. Lance Edge en headless.
    public static bool GeneratePdf(Data d, string pdfPath, out string error)
    {
        error = null;
        string htmlPath = null, udd = null;
        try
        {
            if (!File.Exists(TemplatePath)) { error = "Template introuvable : " + TemplatePath; return false; }

            string html = BuildHtml(d);
            htmlPath = Path.Combine(Path.GetTempPath(), "cipl_facture_" + Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(htmlPath, html, new System.Text.UTF8Encoding(false));

            string edge = FindEdge();
            if (edge == null) { error = "Microsoft Edge introuvable (rendu PDF)."; return false; }

            Directory.CreateDirectory(Path.GetDirectoryName(pdfPath));
            // user-data-dir neuf : évite le cache de rendu d'Edge d'un appel à l'autre.
            udd = Path.Combine(Path.GetTempPath(), "cipl_edge_" + Guid.NewGuid().ToString("N"));

            var psi = new ProcessStartInfo
            {
                FileName = edge,
                Arguments =
                    $"--headless=new --disable-gpu --user-data-dir=\"{udd}\" " +
                    $"--print-to-pdf=\"{pdfPath}\" \"file:///{htmlPath.Replace("\\", "/")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (var proc = Process.Start(psi))
            {
                if (!proc.WaitForExit(30000)) { try { proc.Kill(); } catch { } error = "Edge : délai dépassé."; return false; }
            }

            if (!File.Exists(pdfPath)) { error = "Le PDF n'a pas été généré."; return false; }
            return true;
        }
        catch (Exception e) { error = e.Message; return false; }
        finally
        {
            try { if (htmlPath != null) File.Delete(htmlPath); } catch { }
            try { if (udd != null && Directory.Exists(udd)) Directory.Delete(udd, true); } catch { }
        }
    }

    /// Rend le même HTML en image PNG (capture d'écran Edge headless), pour l'aperçu
    /// dans l'application. `width`/`height` en px CSS (A4 ≈ 794×1123), `scale` = netteté.
    public static bool GeneratePreviewPng(Data d, string pngPath, out string error,
        int width = 794, int height = 1123, int scale = 2)
    {
        error = null;
        string htmlPath = null, udd = null;
        try
        {
            if (!File.Exists(TemplatePath)) { error = "Template introuvable : " + TemplatePath; return false; }

            string html = BuildHtml(d);
            htmlPath = Path.Combine(Path.GetTempPath(), "cipl_facture_" + Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(htmlPath, html, new System.Text.UTF8Encoding(false));

            string edge = FindEdge();
            if (edge == null) { error = "Microsoft Edge introuvable (rendu de l'aperçu)."; return false; }

            Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
            try { if (File.Exists(pngPath)) File.Delete(pngPath); } catch { }   // évite d'afficher l'ancien
            udd = Path.Combine(Path.GetTempPath(), "cipl_edge_" + Guid.NewGuid().ToString("N"));

            var psi = new ProcessStartInfo
            {
                FileName = edge,
                Arguments =
                    $"--headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor={scale} " +
                    $"--user-data-dir=\"{udd}\" --window-size={width},{height} " +
                    $"--screenshot=\"{pngPath}\" \"file:///{htmlPath.Replace("\\", "/")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (var proc = Process.Start(psi))
            {
                if (!proc.WaitForExit(30000)) { try { proc.Kill(); } catch { } error = "Edge : délai dépassé."; return false; }
            }

            if (!File.Exists(pngPath)) { error = "L'aperçu n'a pas été généré."; return false; }
            return true;
        }
        catch (Exception e) { error = e.Message; return false; }
        finally
        {
            try { if (htmlPath != null) File.Delete(htmlPath); } catch { }
            try { if (udd != null && Directory.Exists(udd)) Directory.Delete(udd, true); } catch { }
        }
    }

    // Logo à placer sur la facture : celui du Réglage s'il est défini, sinon le
    // logo CIPL par défaut (StreamingAssets/logo_cipl.png). Retourné en data-URI.
    static string LogoDataUri()
    {
        try
        {
            string custom = ReglageService.Current != null ? ReglageService.Current.logoPath : null;
            string path = (!string.IsNullOrEmpty(custom) && File.Exists(custom))
                ? custom : Path.Combine(Application.streamingAssetsPath, "logo_cipl.png");
            if (!File.Exists(path)) return "";
            byte[] bytes = File.ReadAllBytes(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string mime = ext == ".jpg" || ext == ".jpeg" ? "image/jpeg"
                : ext == ".gif" ? "image/gif" : "image/png";
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }
        catch { return ""; }
    }

    static string FindEdge()
    {
        string[] paths =
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        };
        foreach (var p in paths) if (File.Exists(p)) return p;
        return null;
    }

    // Montant au format français : « 4 565,50 € » avec espace fine insécable + &euro;.
    static string Euro(float v) =>
        v.ToString("#,##0.00", Fr).Replace(" ", "&#8239;").Replace("\u00A0", "&#8239;") + "&nbsp;&euro;";

    // Échappe le texte pour l'insertion HTML.
    static string H(string s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // Adresse multi-lignes → HTML (séparateurs \n ou virgules → <br>), échappé.
    public static string AdresseHtml(string adresse)
    {
        if (string.IsNullOrWhiteSpace(adresse)) return "";
        var lines = adresse.Replace("\r", "").Split('\n');
        var sb = new System.Text.StringBuilder();
        foreach (var l in lines)
        {
            var t = l.Trim();
            if (t.Length == 0) continue;
            sb.Append(H(t)).Append("<br>\n        ");
        }
        return sb.ToString();
    }

    // Texte d'entête résolu → paragraphes HTML.
    public static string BodyHtml(string texte)
    {
        if (string.IsNullOrWhiteSpace(texte)) return "";
        var lines = texte.Replace("\r", "").Split('\n');
        var sb = new System.Text.StringBuilder();
        foreach (var l in lines)
        {
            var t = l.Trim();
            if (t.Length == 0) continue;
            sb.Append("<p>").Append(H(t)).Append("</p>\n    ");
        }
        return sb.ToString();
    }

    public static CultureInfo FrCulture => Fr;
}
