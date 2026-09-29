using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// Dépôt d'une facture sur Pennylane : NOTRE PDF + ses données, que Pennylane emballe
/// en Factur-X (`convert_to_e_invoice`). La mise en page reste celle de CIPL.
///
/// La facture est déposée INCOMPLÈTE (`import_as_incomplete`) : ni finalisée, ni
/// transmise à la plateforme. Finaliser et `send_to_pa` sont irréversibles ; ils ne
/// sont pas écrits tant que Pennylane n'a pas confirmé qu'une facture importée y est
/// éligible (FACTURATION_CIPL_PENNYLANE.md §5). En attendant, on finalise à la main
/// dans Pennylane.
///
/// La clé API vient du fichier secrets et n'apparaît dans aucun message ni log.
public static class PennylaneClient
{
    const string Base = "https://app.pennylane.com/api/external/v2";

    /// Résultat d'un dépôt. `Succes` n'est vrai que si Pennylane a créé la facture.
    public class Depot
    {
        public bool Succes;
        public string Erreur = "";
        public long FactureId;
        public bool FacturX;   // faux possible au retour : la conversion est asynchrone
    }

    // Corps JSON. Montants en CHAÎNES : l'API refuse les nombres.
    [Serializable] public class Ligne
    {
        public string label, unit = "forfait", raw_currency_unit_price, vat_rate, currency_amount, currency_tax;
        public int quantity = 1;
    }

    [Serializable] public class Import
    {
        public long file_attachment_id, customer_id;
        public string date, deadline, invoice_number;
        public string currency_amount_before_tax, currency_tax, currency_amount;
        public List<Ligne> invoice_lines = new List<Ligne>();
        public bool import_as_incomplete = true, convert_to_e_invoice = true;
    }

    [Serializable] public class Adresse { public string address, postal_code, city, country_alpha2 = "FR"; }
    // Trois formes, car JsonUtility écrit toujours un tableau, même vide : une fiche
    // sans email ne doit pas effacer celui saisi dans Pennylane.
    [Serializable] class Fiche { public string name; public Adresse billing_address; }
    [Serializable] class FicheEmail : Fiche { public string[] emails; }
    [Serializable] class FicheCreation : FicheEmail { public string reg_no; }
    [Serializable] class Id { public long id; }
    [Serializable] class Liste { public List<Id> items; }
    [Serializable] class Facture { public long id; public bool factur_x; }

    // ── Règles pures (testées sans réseau) ─────────────────────────────────────

    /// Lignes et totaux au centime. Pennylane refuse (422) si la somme des lignes TTC
    /// n'égale pas le total. La TVA est calculée sur le TOTAL HT, comme sur le PDF,
    /// et la ligne de provision absorbe l'écart d'arrondi entre les deux lignes.
    public static Import Montants(string libelleLoyer, float loyerHT, float provisionHT, bool avecTva)
    {
        decimal l = R2(loyerHT), p = R2(provisionHT), ht = l + p;
        decimal tva = avecTva ? R2(ht * 0.2m) : 0m;
        decimal tvaLoyer = p > 0 && avecTva ? R2(l * 0.2m) : tva;

        var imp = new Import
        {
            currency_amount_before_tax = S(ht), currency_tax = S(tva), currency_amount = S(ht + tva),
        };
        imp.invoice_lines.Add(NouvelleLigne(libelleLoyer, l, tvaLoyer, avecTva));
        if (p > 0) imp.invoice_lines.Add(NouvelleLigne("Provision pour charges", p, tva - tvaLoyer, avecTva));
        return imp;
    }

    /// SIREN (9 chiffres) tiré d'un SIRET saisi avec ou sans espaces. Null si absent.
    public static string Siren(string siret)
    {
        var chiffres = Regex.Replace(siret ?? "", @"\D", "");
        return chiffres.Length >= 9 ? chiffres.Substring(0, 9) : null;
    }

    /// Contrainte Pennylane sur `invoice_number` : 35 caractères, sans espace ni accent.
    public static bool NumeroValide(string numero)
        => Regex.IsMatch(numero ?? "", @"^[A-Za-z0-9\-+_/]{1,35}$");

    /// L'adresse du locataire est un texte libre (une ou plusieurs lignes) ; Pennylane
    /// veut rue / code postal / ville séparés. Le code postal (5 chiffres) fait la coupure.
    public static bool DecouperAdresse(string texte, out Adresse adresse)
    {
        adresse = null;
        var lignes = (texte ?? "").Replace("\r", "").Split('\n');
        string s = string.Join(", ", Array.FindAll(lignes, x => x.Trim().Length > 0)).Trim();
        var m = Regex.Match(s, @"^(?<rue>.+?)[,\s]+(?<cp>\d{5})\s+(?<ville>[^,]+?)(?:\s*,\s*France)?$",
                            RegexOptions.IgnoreCase);
        if (!m.Success) return false;
        adresse = new Adresse
        {
            address = m.Groups["rue"].Value.Trim(' ', ','),
            postal_code = m.Groups["cp"].Value,
            city = m.Groups["ville"].Value.Trim(),
        };
        return true;
    }

    /// Fiche client envoyée à Pennylane, tirée du destinataire de la facture (celui
    /// imprimé sur le PDF, donc aussi celui du Factur-X). `siren` non nul = création.
    public static object FicheClient(string nom, Adresse adresse, string email, string siren = null)
    {
        email = (email ?? "").Trim();
        if (siren != null)
            return new FicheCreation { name = nom, billing_address = adresse, reg_no = siren,
                                       emails = email.Length > 0 ? new[] { email } : new string[0] };
        return email.Length > 0
            ? new FicheEmail { name = nom, billing_address = adresse, emails = new[] { email } }
            : new Fiche { name = nom, billing_address = adresse };
    }

    static Ligne NouvelleLigne(string label, decimal ht, decimal tva, bool avecTva) => new Ligne
    {
        label = label, raw_currency_unit_price = S(ht), vat_rate = avecTva ? "FR_200" : "exempt",
        currency_amount = S(ht + tva), currency_tax = S(tva),
    };

    static decimal R2(float v) => Math.Round((decimal)v, 2, MidpointRounding.AwayFromZero);
    static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    static string S(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    // ── Dépôt ───────────────────────────────────────────────────────────────────

    /// Client (retrouvé par SIREN, sinon créé) → PDF → import incomplet + Factur-X.
    /// À lancer par `yield return` depuis une coroutine ; `res` porte l'issue.
    public static IEnumerator Deposer(FactureInfo f, FacturePdfService.Data d, string pdfPath, Depot res)
    {
        string cle = ReglageService.GetApiKey();
        string siren = Siren(f.destSiret);
        Adresse adresse = null;

        string manque =
              string.IsNullOrWhiteSpace(cle) ? "Clé API Pennylane absente (Réglages → Connexion & envoi)."
            : siren == null ? "SIRET manquant : la facture électronique concerne les entreprises. Pour un particulier, passe par l'email."
            : !NumeroValide(d.numero) ? $"Numéro « {d.numero} » refusé par Pennylane (35 caractères max : lettres, chiffres, - + _ /, sans espace). Une facture électronique déjà émise se corrige par un avoir."
            : !DecouperAdresse(f.destAdresse, out adresse) ? "Adresse du destinataire sans code postal + ville reconnaissables (ex. « 12 rue X, 31000 Toulouse »)."
            : string.IsNullOrEmpty(f.dateISO) || string.IsNullOrEmpty(f.dateEcheanceISO) ? "Date de facture ou d'échéance invalide."
            : null;
        if (manque != null) { Echec(res, manque); yield break; }

        byte[] pdf;
        try { pdf = File.ReadAllBytes(pdfPath); }
        catch (Exception e) { Echec(res, "PDF illisible : " + e.Message); yield break; }

        // 1. Client : CIPL fait foi. Un même SIREN = un seul client Pennylane (même
        // sur deux baux) : créé au premier dépôt, remis à jour à chaque dépôt suivant.
        long clientId = 0;
        string filtre = UnityWebRequest.EscapeURL($"[{{\"field\":\"reg_no\",\"operator\":\"eq\",\"value\":\"{siren}\"}}]");
        using (var q = Requete("GET", "/customers?filter=" + filtre, cle))
        {
            yield return q.SendWebRequest();
            if (q.result != UnityWebRequest.Result.Success) { Echec(res, Motif("recherche du client", q)); yield break; }
            var items = JsonUtility.FromJson<Liste>(q.downloadHandler.text)?.items;
            if (items != null && items.Count > 0) clientId = items[0].id;
        }

        if (clientId == 0)
        {
            using var c = Requete("POST", "/company_customers", cle, FicheClient(f.destNom, adresse, f.emailDest, siren));
            yield return c.SendWebRequest();
            if (c.result != UnityWebRequest.Result.Success) { Echec(res, Motif("création du client", c)); yield break; }
            clientId = JsonUtility.FromJson<Id>(c.downloadHandler.text).id;
        }
        else
        {
            // Pennylane construit le Factur-X depuis SA fiche client : une adresse
            // périmée y contredirait le PDF. En cas d'échec, on ne dépose pas.
            using var m = Requete("PUT", "/company_customers/" + clientId, cle, FicheClient(f.destNom, adresse, f.emailDest));
            yield return m.SendWebRequest();
            if (m.result != UnityWebRequest.Result.Success) { Echec(res, Motif("mise à jour du client", m)); yield break; }
        }

        // 2. Notre PDF.
        long fichierId;
        var form = new List<IMultipartFormSection>
            { new MultipartFormFileSection("file", pdf, Path.GetFileName(pdfPath), "application/pdf") };
        using (var up = UnityWebRequest.Post(Base + "/file_attachments", form))
        {
            up.SetRequestHeader("Authorization", "Bearer " + cle);
            yield return up.SendWebRequest();
            if (up.result != UnityWebRequest.Result.Success) { Echec(res, Motif("envoi du PDF", up)); yield break; }
            fichierId = JsonUtility.FromJson<Id>(up.downloadHandler.text).id;
        }

        // 3. Import incomplet + Factur-X. 409 = fichier pas encore prêt côté Pennylane :
        // on réessaie un peu (ou doublon, que le motif final dira).
        var corps = Montants(d.subtitle, d.totalPeriode, d.provision, d.tva > 0.005f);
        corps.file_attachment_id = fichierId;
        corps.customer_id = clientId;
        corps.date = f.dateISO;
        corps.deadline = f.dateEcheanceISO;
        corps.invoice_number = d.numero;

        for (int essai = 1; ; essai++)
        {
            using var imp = Requete("POST", "/customer_invoices/import", cle, corps);
            yield return imp.SendWebRequest();
            if (imp.responseCode == 409 && essai < 4) { yield return new WaitForSecondsRealtime(2f); continue; }
            if (imp.result != UnityWebRequest.Result.Success) { Echec(res, Motif("import de la facture", imp)); yield break; }

            var fac = JsonUtility.FromJson<Facture>(imp.downloadHandler.text);
            res.FactureId = fac.id;
            res.FacturX = fac.factur_x;
            res.Succes = true;
            yield break;
        }
    }

    static UnityWebRequest Requete(string methode, string chemin, string cle, object corps = null)
    {
        var r = new UnityWebRequest(Base + chemin, methode) { downloadHandler = new DownloadHandlerBuffer() };
        if (corps != null)
        {
            r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(corps)));
            r.SetRequestHeader("Content-Type", "application/json");
        }
        r.SetRequestHeader("Authorization", "Bearer " + cle);
        r.SetRequestHeader("Accept", "application/json");
        return r;
    }

    // Le corps de réponse dit POURQUOI (champ manquant, lignes déséquilibrées…).
    static string Motif(string etape, UnityWebRequest r)
    {
        string corps = r.downloadHandler?.text ?? "";
        if (corps.Length > 300) corps = corps.Substring(0, 300) + "…";
        return $"Pennylane — {etape} : HTTP {r.responseCode} {corps}".Trim();
    }

    static void Echec(Depot res, string motif)
    {
        res.Succes = false;
        res.Erreur = motif;
        Debug.LogError("[Pennylane] " + motif);
    }
}
