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

    public static string RegulTemplatePath =>
        Path.Combine(Application.streamingAssetsPath, "facture_regul_template.html");

    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// Données à injecter dans le template.
    public class Data
    {
        public string clientNom, clientAdresseHtml, clientSiret;
        public string refInterne;   // texte libre (ex. « N° Interne Magasin 001048 »), optionnel
        public string ligneLabel;   // libellé de la 1re ligne du tableau (« Total de la période », nom de charge…)
        public string dateStr, numero, subtitle, bodyHtml, sommePhrase;
        public float totalPeriode, provision, totalHT, tva, ttc;
        public bool tvaDebit, retard;
        public bool afficherMensuel;   // ligne « montant mensuel à régler »
        public float montantMensuel;   // TTC de la période ÷ nombre de mois de la période
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
            .Replace("{{LIGNE_LABEL}}", H(string.IsNullOrEmpty(d.ligneLabel) ? "Total de la période" : d.ligneLabel))
            .Replace("{{TOTAL_PERIODE}}", Euro(d.totalPeriode))
            .Replace("{{PROVISION_ROW}}", d.provision > 0f
                ? $"<tr><td>Provision pour charges</td><td class=\"r\">{Euro(d.provision)}</td></tr>" : "")
            .Replace("{{TOTAL_HT}}", Euro(d.totalHT))
            .Replace("{{TVA}}", Euro(d.tva))
            .Replace("{{TTC}}", Euro(d.ttc))
            .Replace("{{TVA_DEBIT}}", d.tvaDebit
                ? "<div class=\"tva\">&nbsp;la TVA est pay&eacute;e sur les d&eacute;bits</div>" : "")
            .Replace("{{MENSUEL_ROW}}", d.afficherMensuel && d.montantMensuel > 0f
                ? $"<div class=\"mensuel\">Suite &agrave; votre demande, le montant mensuel &agrave; r&eacute;gler est de:<b>{Euro(d.montantMensuel)}</b></div>"
                : "")
            .Replace("{{SOMME_PHRASE}}", H(d.sommePhrase))
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
        if (!File.Exists(TemplatePath)) { error = "Template introuvable : " + TemplatePath; return false; }
        return RunEdge(BuildHtml(d), new[] { "--print-to-pdf=" + pdfPath }, pdfPath, out error);
    }

    // ── Quittance de loyer (bail non commercial, loyer payé) ────────────────────

    public static string QuittanceTemplatePath =>
        Path.Combine(Application.streamingAssetsPath, "quittance_template.html");

    public class QuittanceData
    {
        public string clientNom, clientAdresseHtml, dateStr, periode, designation, montantStr, foot1, foot2;
    }

    public static string BuildQuittanceHtml(QuittanceData d)
    {
        string t = File.ReadAllText(QuittanceTemplatePath);
        return t
            .Replace("{{LOGO_SRC}}", LogoDataUri())
            .Replace("{{CLIENT_NOM}}", H(d.clientNom))
            .Replace("{{CLIENT_ADRESSE}}", d.clientAdresseHtml ?? "")
            .Replace("{{DATE}}", H(d.dateStr))
            .Replace("{{PERIODE}}", H(d.periode))
            .Replace("{{DESIGNATION}}", string.IsNullOrWhiteSpace(d.designation)
                ? "" : $"<div class=\"desig\">Bien lou&eacute; : {H(d.designation)}</div>")
            .Replace("{{MONTANT}}", H(d.montantStr))
            .Replace("{{FOOT1}}", H(d.foot1))
            .Replace("{{FOOT2}}", H(d.foot2));
    }

    public static bool GenerateQuittancePdf(QuittanceData d, string pdfPath, out string error)
    {
        if (!File.Exists(QuittanceTemplatePath))
        { error = "Template quittance introuvable : " + QuittanceTemplatePath; return false; }
        return RunEdge(BuildQuittanceHtml(d), new[] { "--print-to-pdf=" + pdfPath }, pdfPath, out error);
    }

    // Arguments Edge pour une capture d'écran (aperçu image). Un argument par entrée :
    // c'est .NET qui assemble et cite la ligne de commande (voir RunEdge).
    static string[] ScreenshotArgs(string pngPath, int width, int height, int scale) => new[]
    {
        "--hide-scrollbars",
        $"--force-device-scale-factor={scale}",
        $"--window-size={width},{height}",
        "--screenshot=" + pngPath
    };

    // Lance Edge headless sur `html` avec `outputArgs` (print-to-pdf ou screenshot) et
    // vérifie que `outPath` a bien été produit. Factorisé pour loyer ET régularisation.
    static bool RunEdge(string html, string[] outputArgs, string outPath, out string error)
    {
        error = null;
        string htmlPath = null, udd = null;
        try
        {
            string edge = FindEdge();
            if (edge == null) { error = "Microsoft Edge introuvable (rendu de la facture)."; return false; }

            htmlPath = Path.Combine(Path.GetTempPath(), "cipl_facture_" + Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(htmlPath, html, new System.Text.UTF8Encoding(false));

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            // Purge de l'ancien PDF avant régénération. Un échec silencieux laissait
            // le fichier PÉRIMÉ en place, présenté ensuite comme la nouvelle facture.
            try { if (File.Exists(outPath)) File.Delete(outPath); }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[FacturePdfService] Ancien PDF non supprimé ({outPath}) : {e.Message} — " +
                                           "le fichier affiché risque d'être périmé.");
            }
            // user-data-dir neuf : évite le cache de rendu d'Edge d'un appel à l'autre.
            udd = Path.Combine(Path.GetTempPath(), "cipl_edge_" + Guid.NewGuid().ToString("N"));

            // Arguments passés UN PAR UN plutôt qu'en une ligne de commande assemblée à
            // la main : c'est .NET qui se charge de la citation. Poser les guillemets
            // soi-même autour de chemins venant de noms de bâtiment, de locataire et du
            // dossier de sauvegarde choisi par l'utilisatrice, c'est se reposer sur le
            // fait que Windows interdit le guillemet dans un chemin — vrai sur NTFS,
            // faux ailleurs, et de toute façon pas une garantie à faire porter au nommage.
            var psi = new ProcessStartInfo
            {
                FileName = edge,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--headless=new");
            psi.ArgumentList.Add("--disable-gpu");
            psi.ArgumentList.Add("--user-data-dir=" + udd);
            foreach (string a in outputArgs) psi.ArgumentList.Add(a);
            psi.ArgumentList.Add("file:///" + htmlPath.Replace("\\", "/"));
            using (var proc = Process.Start(psi))
            {
                if (!proc.WaitForExit(30000)) { try { proc.Kill(); } catch { } error = "Edge : délai dépassé."; return false; }
            }

            if (!File.Exists(outPath)) { error = "Le fichier n'a pas été généré."; return false; }
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
        if (!File.Exists(TemplatePath)) { error = "Template introuvable : " + TemplatePath; return false; }
        return RunEdge(BuildHtml(d), ScreenshotArgs(pngPath, width, height, scale), pngPath, out error);
    }

    // ── Régularisation des charges ─────────────────────────────────────────────

    /// Une ligne du récapitulatif de charges (quote-part du locataire).
    public class RegulLigne
    {
        public string nom, dateStr, pj;   // pj = nom du justificatif (ou vide)
        public float coutTotal, quotePart;
    }

    /// Données d'une facture de régularisation des charges.
    public class RegulData
    {
        public string clientNom, clientAdresseHtml, clientSiret, refInterne;
        public string dateStr, numero, subtitle, bodyHtml, sommePhrase;
        public System.Collections.Generic.List<RegulLigne> charges = new System.Collections.Generic.List<RegulLigne>();
        public float totalCharges, provisions, soldeHT, tva, ttc;
        // Libellés des 3 lignes de totaux (défauts = régularisation ; réutilisé pour le dépôt).
        public string labelTotal, labelProvisions, labelSolde;
        public bool masquerTva;   // true = pas de ligne TVA/TTC (ex. révision du dépôt de garantie)
        // Page 2 (détail) : titre = adresse du bâtiment, colonne quote-part = nom locataire.
        public string detailTitre, locataireNom;
        public float surfaceImmeuble, totalARepartir;
        public bool tvaDebit, retard;
        public string ribTitulaire, ribDomiciliation, ribNum, ribIban, ribBic;
        public string legal, foot1, foot2;
    }

    public static string BuildRegulHtml(RegulData d)
    {
        string t = File.ReadAllText(RegulTemplatePath);
        return t
            .Replace("{{LOGO_SRC}}", LogoDataUri())
            .Replace("{{CLIENT_NOM}}", H(d.clientNom))
            .Replace("{{CLIENT_ADRESSE}}", d.clientAdresseHtml ?? "")
            .Replace("{{CLIENT_SIRET}}", H(d.clientSiret))
            .Replace("{{REF_INTERNE}}", string.IsNullOrWhiteSpace(d.refInterne)
                ? "" : $"<div class=\"refint\">{H(d.refInterne)}</div>")
            .Replace("{{DATE}}", H(d.dateStr))
            .Replace("{{NUMERO}}", H(d.numero))
            .Replace("{{BODY}}", d.bodyHtml ?? "")
            .Replace("{{SUBTITLE}}", H(d.subtitle))
            .Replace("{{TOTAUX_BLOCK}}", TotauxBlock(d))
            .Replace("{{DETAIL_PAGE}}", DetailPage(d))
            .Replace("{{SOMME_PHRASE}}", H(d.sommePhrase))
            .Replace("{{RIB_TITULAIRE}}", H(d.ribTitulaire))
            .Replace("{{RIB_DOMICILIATION}}", H(d.ribDomiciliation))
            .Replace("{{RIB_NUM}}", H(d.ribNum))
            .Replace("{{RIB_IBAN}}", H(d.ribIban))
            .Replace("{{RIB_BIC}}", H(d.ribBic))
            .Replace("{{LEGAL}}", d.retard ? H(d.legal) : "")
            .Replace("{{FOOT1}}", H(d.foot1))
            .Replace("{{FOOT2}}", H(d.foot2));
    }

    // Page 1 : uniquement les totaux (le détail des charges part en page 2).
    static string TotauxBlock(RegulData d)
    {
        string lt = string.IsNullOrEmpty(d.labelTotal) ? "Total des charges (quote-part)" : d.labelTotal;
        string lp = string.IsNullOrEmpty(d.labelProvisions) ? "Provisions déjà versées" : d.labelProvisions;
        string ls = string.IsNullOrEmpty(d.labelSolde) ? "Solde H.T." : d.labelSolde;

        var sb = new System.Text.StringBuilder();
        sb.Append("<table class=\"totaux\">")
          .Append("<tr><td>").Append(H(lt)).Append("</td><td class=\"r\">").Append(Euro(d.totalCharges)).Append("</td></tr>")
          .Append("<tr><td>").Append(H(lp)).Append("</td><td class=\"r\">").Append(Euro(d.provisions)).Append("</td></tr>");
        if (d.masquerTva)
        {
            sb.Append("<tr class=\"ttc\"><td>").Append(H(ls)).Append("</td><td class=\"r\">").Append(Euro(d.soldeHT)).Append("</td></tr>");
        }
        else
        {
            sb.Append("<tr class=\"solde\"><td>").Append(H(ls)).Append("</td><td class=\"r\">").Append(Euro(d.soldeHT)).Append("</td></tr>")
              .Append("<tr><td>TVA 20%</td><td class=\"r\">").Append(Euro(d.tva)).Append("</td></tr>")
              .Append("<tr class=\"ttc\"><td>Total T.T.C.</td><td class=\"r\">").Append(Euro(d.ttc)).Append("</td></tr>");
        }
        sb.Append("</table>");
        if (d.tvaDebit && !d.masquerTva)
            sb.Append("<div class=\"tva\">&nbsp;la TVA est pay&eacute;e sur les d&eacute;bits</div>");
        return sb.ToString();
    }

    // Page 2 : récapitulatif détaillé des charges (adresse bâtiment + surface +
    // Date + Total à répartir + quote-part du locataire + Total/Provision/Solde).
    static string DetailPage(RegulData d)
    {
        if (d.charges == null || d.charges.Count == 0) return "";
        string loc = string.IsNullOrWhiteSpace(d.locataireNom) ? "Locataire" : H(d.locataireNom);
        var sb = new System.Text.StringBuilder();
        sb.Append("<div class=\"page detail\">");
        sb.Append("<div class=\"detail-title\">").Append(H(d.detailTitre)).Append("</div>");
        sb.Append("<table class=\"detail\">");
        sb.Append("<tr><th></th><th>Date</th><th class=\"r\">Total &agrave; r&eacute;partir</th><th class=\"r\">")
          .Append(loc).Append("</th></tr>");
        sb.Append("<tr><td class=\"name\">Surface immeuble m&sup2;</td><td></td><td></td><td class=\"r\">")
          .Append(d.surfaceImmeuble > 0 ? d.surfaceImmeuble.ToString("0.##", Fr) : "").Append("</td></tr>");
        sb.Append("<tr><td colspan=\"4\">&nbsp;</td></tr>");
        foreach (var l in d.charges)
            sb.Append("<tr><td class=\"name\">").Append(H(l.nom)).Append("</td><td>").Append(H(l.dateStr))
              .Append("</td><td class=\"r\">").Append(Euro(l.coutTotal))
              .Append("</td><td class=\"r\">").Append(Euro(l.quotePart)).Append("</td></tr>");
        sb.Append("<tr><td colspan=\"4\">&nbsp;</td></tr>");
        sb.Append("<tr class=\"tot\"><td>Total Charges</td><td></td><td class=\"r\">").Append(Euro(d.totalARepartir))
          .Append("</td><td class=\"r\">").Append(Euro(d.totalCharges)).Append("</td></tr>");
        sb.Append("<tr><td class=\"name\">Provision pour charges &agrave; d&eacute;duire</td><td></td><td></td><td class=\"r\">")
          .Append(Euro(d.provisions)).Append("</td></tr>");
        sb.Append("<tr class=\"tot\"><td>Solde H.T.</td><td></td><td></td><td class=\"r\">").Append(Euro(d.soldeHT)).Append("</td></tr>");
        sb.Append("</table></div>");
        return sb.ToString();
    }

    public static bool GenerateRegulPdf(RegulData d, string pdfPath, out string error)
    {
        if (!File.Exists(RegulTemplatePath)) { error = "Template régul. introuvable : " + RegulTemplatePath; return false; }
        return RunEdge(BuildRegulHtml(d), new[] { "--print-to-pdf=" + pdfPath }, pdfPath, out error);
    }

    public static bool GenerateRegulPreviewPng(RegulData d, string pngPath, out string error,
        int width = 794, int height = 1123, int scale = 2)
    {
        if (!File.Exists(RegulTemplatePath)) { error = "Template régul. introuvable : " + RegulTemplatePath; return false; }
        return RunEdge(BuildRegulHtml(d), ScreenshotArgs(pngPath, width, height, scale), pngPath, out error);
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
