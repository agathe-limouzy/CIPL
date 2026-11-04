using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// Étape 18b : une facture de loyer au modèle EN 16931 (le JSON « en_invoice » de Super PDP), que la plateforme
/// assemble avec notre PDF en Factur-X avant de la transmettre (fonction serveur « facture-electronique »).
/// Sans réseau, testé. Écrit à la main, et non par JsonUtility : un champ facultatif vide (« "" ») serait
/// refusé par la plateforme, il doit être absent.
public static class FactureEn16931
{
    public class Entree
    {
        public string numero, dateISO, echeanceISO;
        public string vendeurNom, vendeurSiren;
        public string acheteurNom, acheteurSiren, acheteurAdresse;
        public string acheteurSiret;   // 14 chiffres quand il est connu : l'adresse de facturation électronique de l'établissement (D5-13)
        public List<KeyValuePair<string, float>> lignes = new List<KeyValuePair<string, float>>();   // libellé, montant HT
        public float tauxTva = 20f;
        public string mention;   // phrase TVA imprimée (« TVA acquittée sur les débits… »), ou vide
        public string penalites; // phrase de retard des Réglages (note PMD), ou vide : la phrase légale d'usine
        public string escompte;  // phrase d'escompte des Réglages (note AAB, 04/11), ou vide : celle d'usine
        // Correction : la facture transmise qu'elle annule et remplace (voir Remplacee), ou vide.
        public string precedentNumero, precedentDateISO;
        // Avoir : les factures qu'il corrige (numéro tel que la plateforme le connaît, date), règle BR-FR-CO-05 (02/11).
        public List<KeyValuePair<string, string>> references = new List<KeyValuePair<string, string>>();
    }

    public const string AvoirSansReference = "un avoir électronique doit citer la facture qu'il corrige (règle BR-FR-CO-05).";

    /// Les lignes d'un loyer, telles que le PDF les imprime : le loyer, puis les provisions.
    public static List<KeyValuePair<string, float>> Lignes(FacturePdfService.Data d)
    {
        var l = new List<KeyValuePair<string, float>> { new KeyValuePair<string, float>(Libelle(d.ligneLabel, d.subtitle), d.totalPeriode) };
        if (d.lignesProvision != null) l.AddRange(d.lignesProvision.Where(p => Math.Abs(p.Value) > 0.004f));
        else if (Math.Abs(d.provision) > 0.004f) l.Add(new KeyValuePair<string, float>("Provision pour charges", d.provision));
        return l;
    }

    static string Libelle(string a, string b) => !string.IsNullOrWhiteSpace(a) ? a.Trim() : (b ?? "Loyer").Trim();

    /// Les lignes d'une régularisation (décision du 02/11, BI5) : les charges (quote-part), puis ce qui s'en déduit. La
    /// première est posée depuis le solde, pour que la somme redonne exactement le solde HT du PDF.
    public static List<KeyValuePair<string, float>> LignesRegul(FacturePdfService.RegulData d)
    {
        var l = new List<KeyValuePair<string, float>>
            { new KeyValuePair<string, float>(Libelle(null, d.subtitle) + " : charges (quote-part)", d.soldeHT + d.provisions + d.dejaRefacture) };
        if (Math.Abs(d.dejaRefacture) > 0.004f) l.Add(new KeyValuePair<string, float>("Charges déjà refacturées à part", -d.dejaRefacture));
        if (Math.Abs(d.provisions) > 0.004f) l.Add(new KeyValuePair<string, float>("Provisions déjà versées", -d.provisions));
        return l;
    }

    /// Mentions de retard (règle BR-FR-05) : la phrase des Réglages, sinon les phrases de repli, les mêmes dans le XML et sur le
    /// PDF (revue 16c, D5-17 : le PDF restait muet quand la phrase était vide).
    public const string PenalitesDefaut = "En cas de retard de paiement, des pénalités au taux égal à trois fois le taux d'intérêt légal sont exigibles.";
    public const string IndemniteDefaut = "Indemnité forfaitaire pour frais de recouvrement en cas de retard de paiement : 40 €.";
    public static string MentionsRetard(string saisie) => !string.IsNullOrWhiteSpace(saisie) ? saisie.Trim() : PenalitesDefaut + " " + IndemniteDefaut;

    /// Revue 17 (D6-03) : conditions d'escompte, obligatoires (art. L441-9 du Code de commerce) : le même texte au PDF et au XML.
    public const string SansEscompte = "Pas d'escompte pour paiement anticipé.";

    /// Demande du 04/11 : la tournure des Réglages (phraseEscompte), sinon la phrase d'usine : jamais absente.
    public static string Escompte(string saisie) => !string.IsNullOrWhiteSpace(saisie) ? saisie.Trim() : SansEscompte;

    /// Revue 17 (D5-01) : le numéro que la plateforme a reçu, premier champ du JSON écrit par Json (null si aucun).
    public static string NumeroDe(string json)
    {
        var m = Regex.Match(json ?? "", "^\\{\"number\":\"([^\"]*)\"");
        return m.Success && m.Groups[1].Value != "" ? m.Groups[1].Value : null;
    }

    public static string Json(Entree e)
    {
        var lignes = e.lignes.Select(x => (nom: x.Key, ht: Arrondi(x.Value))).ToList();
        decimal totalHT = lignes.Sum(x => x.ht);
        // Avoir (décision du 02/11, BI5) : un total négatif n'est pas une facture mais un avoir (381), aux montants positifs :
        // chaque ligne change de signe (les charges d'une régul qui rembourse passent en quantité −1), les totaux aussi.
        bool avoir = totalHT < 0m;
        if (avoir) { lignes = lignes.Select(x => (x.nom, -x.ht)).ToList(); totalHT = -totalHT; }
        decimal taux = Arrondi(e.tauxTva);
        decimal tva = Math.Round(totalHT * taux / 100m, 2, MidpointRounding.AwayFromZero);
        decimal ttc = totalHT + tva;

        // Correction d'une facture déjà transmise : facture rectificative (384), qui annule et remplace la
        // précédente en citant son numéro et sa date tels que la plateforme les a reçus (décision du 31/10). Sans
        // son type : le Factur-X EN 16931 le refuse (31/10, « Element 'ram:TypeCode' is marked as not used »).
        // Factures citées (BT-25, BT-26) : la précédente transmise d'une correction ; pour un avoir, aussi celles qu'il corrige
        // (BR-FR-CO-05 : le bac à sable a refusé, le 02/11, un avoir sans référence). Un avoir sans rien à citer ne part pas.
        var refs = new List<KeyValuePair<string, string>>();
        if (!string.IsNullOrEmpty(e.precedentNumero)) refs.Add(new KeyValuePair<string, string>(e.precedentNumero, e.precedentDateISO));
        if (avoir)
            foreach (var r in e.references ?? new List<KeyValuePair<string, string>>())
                if (!string.IsNullOrEmpty(r.Key) && refs.All(x => x.Key != r.Key)) refs.Add(r);
        if (avoir && refs.Count == 0) throw new InvalidOperationException(AvoirSansReference);
        bool rectificative = !avoir && refs.Count > 0;
        var j = new StringBuilder("{");
        Champ(j, "number", Numero(e.numero));
        Champ(j, "issue_date", e.dateISO);
        j.Append("\"type_code\":").Append(avoir ? "381" : rectificative ? "384" : "380").Append(',');
        if (refs.Count > 0)
        {
            j.Append("\"preceding_invoice_references\":[");
            for (int i = 0; i < refs.Count; i++)
            {
                j.Append(i > 0 ? ",{" : "{").Append("\"reference\":").Append(Q(refs[i].Key));
                if (!string.IsNullOrEmpty(refs[i].Value)) j.Append(",\"issue_date\":").Append(Q(refs[i].Value));
                j.Append('}');
            }
            j.Append("],");
        }
        Champ(j, "currency_code", "EUR");
        // BR-FR-CO-07 : l'échéance (BT-9) ne précède pas la date de facture (30/10 : loyer de février corrigé en
        // octobre). Échéance passée : pas de BT-9, la date du bail passe dans les conditions de paiement (BT-20),
        // comme sur le PDF (décision du 30/10).
        if (string.CompareOrdinal(e.echeanceISO ?? "", e.dateISO ?? "") >= 0) Champ(j, "payment_due_date", e.echeanceISO);
        else if (DateTime.TryParseExact(e.echeanceISO, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ech))
            Champ(j, "payment_terms", $"Loyer exigible le {(ech.Day == 1 ? "1er" : ech.Day.ToString())} {ech.ToString("MMMM yyyy", new CultureInfo("fr-FR"))}.");
        else Champ(j, "payment_terms", "Payable à réception.");
        // Cadre de facturation S1 : prestation de services (un loyer), facture à payer.
        j.Append("\"process_control\":{\"specification_identifier\":\"urn:cen.eu:en16931:2017\",\"business_process_type\":\"S1\"},");
        // Mentions obligatoires en France (règle BR-FR-05, constaté le 30/10) : pénalités de retard (PMD), indemnité
        // forfaitaire de recouvrement (PMT), escompte (AAB) ; puis la mention TVA imprimée.
        string pmd = !string.IsNullOrWhiteSpace(e.penalites) ? e.penalites.Trim() : PenalitesDefaut;
        j.Append("\"notes\":[");
        j.Append("{\"note\":").Append(Q(pmd)).Append(",\"subject_code\":\"PMD\"},");
        j.Append("{\"note\":").Append(Q(IndemniteDefaut)).Append(",\"subject_code\":\"PMT\"},");
        j.Append("{\"note\":").Append(Q(Escompte(e.escompte))).Append(",\"subject_code\":\"AAB\"}");
        if (!string.IsNullOrWhiteSpace(e.mention)) j.Append(",{\"note\":").Append(Q(e.mention)).Append('}');
        j.Append("],");
        j.Append("\"seller\":").Append(Partie(e.vendeurNom, e.vendeurSiren, null, vendeur: true)).Append(',');
        j.Append("\"buyer\":").Append(Partie(e.acheteurNom, e.acheteurSiren, e.acheteurAdresse, e.acheteurSiret)).Append(',');
        j.Append("\"totals\":{");
        Champ(j, "sum_invoice_lines_amount", M(totalHT));
        Champ(j, "total_without_vat", M(totalHT));
        // BT-110 : un objet montant + devise chez Super PDP (30/10 : un texte est refusé, « model.Amount » ; sans lui,
        // la règle BR-CO-15 « total TTC = total HT + total TVA » échoue).
        j.Append("\"total_vat_amount\":{\"value\":").Append(Q(M(tva))).Append(",\"currency_code\":\"EUR\"},");
        Champ(j, "total_with_vat", M(ttc));
        j.Append("\"amount_due_for_payment\":").Append(Q(M(ttc))).Append("},");
        j.Append("\"vat_break_down\":[{");
        Champ(j, "vat_category_taxable_amount", M(totalHT));
        Champ(j, "vat_category_tax_amount", M(tva));
        Champ(j, "vat_category_code", "S");
        j.Append("\"vat_category_rate\":").Append(Q(M(taux))).Append("}],");
        j.Append("\"lines\":[");
        for (int i = 0; i < lignes.Count; i++)
        {
            if (i > 0) j.Append(',');
            j.Append('{');
            Champ(j, "identifier", (i + 1).ToString(CultureInfo.InvariantCulture));
            // Revue 16c (D5-16, décision du 02/11) : une ligne négative (provision en déduction, avoir sur la période) s'écrit en
            // quantité −1 et prix positif : le prix unitaire ne peut pas être négatif (règle BR-27), la quantité si.
            bool negative = lignes[i].ht < 0m;
            Champ(j, "net_amount", M(lignes[i].ht));
            Champ(j, "invoiced_quantity", negative ? "-1" : "1");
            Champ(j, "invoiced_quantity_code", "C62");   // unité (UN/ECE 20)
            j.Append("\"price_details\":{\"item_net_price\":").Append(Q(M(Math.Abs(lignes[i].ht)))).Append("},");
            j.Append("\"vat_information\":{\"invoiced_item_vat_category_code\":\"S\",\"invoiced_item_vat_rate\":").Append(Q(M(taux))).Append("},");
            j.Append("\"item_information\":{\"name\":").Append(Q(lignes[i].nom)).Append("}}");
        }
        j.Append("]}");
        return j.ToString();
    }

    /// Vendeur ou acheteur : nom, SIREN (schéma 0002), n° de TVA, adresse, adresse de facturation électronique
    /// (schéma 0225 : le SIRET de l'établissement quand il est connu, sinon le SIREN ; l'annuaire de l'État relie
    /// l'un ou l'autre à sa plateforme — revue 16c, D5-13 : le serveur préfère l'entrée du SIRET).
    static string Partie(string nom, string siren, string adresse, string siret = null, bool vendeur = false)
    {
        var (ligne, cp, ville) = Adresse(adresse);
        var j = new StringBuilder("{");
        Champ(j, "name", nom);
        if (siren != null && siren.Length == 9)
        {
            j.Append("\"legal_registration_identifier\":{\"value\":").Append(Q(siren)).Append(",\"scheme\":\"0002\"},");
            if (vendeur) Champ(j, "vat_identifier", TvaIntra(siren));   // revue 16c (D5-19) : BT-48 facultatif ; l'acheteur n'a pas forcément de n° intracommunautaire
            string adresseElectronique = siret != null && siret.Length == 14 && siret.StartsWith(siren) ? siret : siren;
            j.Append("\"electronic_address\":{\"value\":").Append(Q(adresseElectronique)).Append(",\"scheme\":\"0225\"},");
        }
        j.Append("\"postal_address\":{");
        if (ligne != "") Champ(j, "address_line1", ligne);
        if (cp != "") Champ(j, "post_code", cp);
        if (ville != "") Champ(j, "city", ville);
        j.Append("\"country_code\":\"FR\"}}");
        return j.ToString();
    }

    /// « 12 rue de Rivoli 75004 Paris » → (« 12 rue de Rivoli », « 75004 », « Paris ») ; sans code postal : tout en ligne 1.
    public static (string ligne, string cp, string ville) Adresse(string brut)
    {
        string a = Regex.Replace(brut ?? "", @"[\r\n]+", ", ").Trim().Trim(',').Trim();
        var m = Regex.Match(a, @"^(.*?)[,\s]*\b(\d{5})\b[,\s]*(.*)$");
        if (!m.Success) return (a, "", "");
        return (m.Groups[1].Value.Trim().Trim(',').Trim(), m.Groups[2].Value, m.Groups[3].Value.Trim().Trim(',').Trim());
    }

    /// Le numéro de facture (BT-1) tel que la règle française BR-FR-02 l'accepte : lettres, chiffres et - + _ /
    /// seulement, 35 caractères au plus (30/10 : « 2026/10003 corrigée(1) » refusé → « 2026/10003-corrigee-1 »).
    public static string Numero(string n)
    {
        var sb = new StringBuilder();
        foreach (char c in (n ?? "").Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;   // accents
            sb.Append(c < 128 && (char.IsLetterOrDigit(c) || "-+_/".IndexOf(c) >= 0) ? c : '-');
        }
        string s = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        return s.Length > 35 ? s.Substring(0, 35) : s;
    }

    /// La facture qu'une correction de cette ligne annule et remplace : la dernière transmise, numéro et date tels
    /// que la plateforme les a reçus (le JSON gardé sur la ligne). Ligne transmise : elle-même. Pas encore
    /// transmise (rectificative refusée, à renvoyer, envoi différé) : la référence qu'elle portait déjà, sinon
    /// aucune (31/10 : un refus ne doit pas faire oublier la facture d'origine).
    public static (string numero, string dateISO) Remplacee(FactureEtat ligne)
    {
        string json = ligne?.plateformeDonnees;
        if (string.IsNullOrEmpty(json)) return (null, null);
        // Json écrit d'abord ses propres « number » et « issue_date », puis la référence.
        var m = !string.IsNullOrEmpty(ligne.plateformeId)
            ? Regex.Match(json, "\"number\":\"([^\"]*)\",\"issue_date\":\"([^\"]*)\"")
            : Regex.Match(json, "\"preceding_invoice_references\":\\[\\{\"reference\":\"([^\"]*)\"(?:,\"issue_date\":\"([^\"]*)\")?");
        if (!m.Success || m.Groups[1].Value == "") return (null, null);
        return (m.Groups[1].Value, m.Groups[2].Success && m.Groups[2].Value != "" ? m.Groups[2].Value : null);
    }

    /// N° de TVA intracommunautaire français, calculé d'après le SIREN : FR + clé + SIREN.
    public static string TvaIntra(string siren)
    {
        if (string.IsNullOrEmpty(siren) || siren.Length != 9 || !long.TryParse(siren, out long n)) return "";
        return "FR" + ((12 + 3 * (n % 97)) % 97).ToString("00", CultureInfo.InvariantCulture) + siren;
    }

    static decimal Arrondi(float x) => Math.Round((decimal)x, 2, MidpointRounding.AwayFromZero);
    static string M(decimal x) => x.ToString("0.00", CultureInfo.InvariantCulture);
    static void Champ(StringBuilder j, string nom, string valeur) => j.Append(Q(nom)).Append(':').Append(Q(valeur ?? "")).Append(',');

    static string Q(string s)
    {
        var b = new StringBuilder("\"");
        foreach (char c in s ?? "")
            switch (c)
            {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\n': b.Append("\\n"); break;
                case '\r': break;
                case '\t': b.Append("\\t"); break;
                default: if (c < 0x20) b.AppendFormat("\\u{0:x4}", (int)c); else b.Append(c); break;
            }
        return b.Append('"').ToString();
    }
}
