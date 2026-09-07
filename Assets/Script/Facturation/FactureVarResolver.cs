using System;
using System.Collections.Generic;

/// Contexte de facture pour la substitution des variables.
public struct FactureContext
{
    public DateTime date;
    public string periode;     // ex « avril 2026 » ou « T2 2026 »
    public string numero;      // n° de facture (renvoyé par Pennylane à la finalisation)
    public float loyerHT;      // total HT de la période (loyer + provision)
    public float tva;
    public float ttc;
}

/// Remplace les tokens {loc.*}, {bat.*}, {societe.*}, {date}, {loyer.*}… d'un
/// texte d'entête par les valeurs réelles (voir FactureVariables pour le catalogue).
public static class FactureVarResolver
{
    // Société émettrice (fixe — Groupe CIPL).
    public const string SocieteNom = "GROUPE CIPL";
    public const string SocieteSiret = "717 220 883 00044";
    public const string SocieteTva = "FR 28 717 220 883";

    public static string Resolve(string texte, Locataire loc, Batiment bat, FactureContext ctx)
    {
        if (string.IsNullOrEmpty(texte)) return "";
        foreach (var kv in BuildMap(loc, bat, ctx))
            texte = texte.Replace(kv.Key, kv.Value);
        return texte;
    }

    static Dictionary<string, string> BuildMap(Locataire loc, Batiment bat, FactureContext ctx)
    {
        var m = new Dictionary<string, string>();
        string periodicite = loc != null ? loc.periodiciteLoyer.ToString() : "";

        // ── Locataire ──
        m["{loc.nom}"]         = S(loc?.Name);
        m["{loc.adresse}"]     = S(loc?.adresseLocataire);
        m["{loc.siret}"]       = S(loc?.siretNumber);
        m["{loc.code}"]        = S(loc?.codeCompatable);
        m["{loc.email}"]       = S(loc?.emailLocataire);
        m["{loc.tel}"]         = S(loc?.telephoneLocataire);
        m["{loc.lot}"]         = loc != null ? loc.lotBatiment.ToString() : "";
        m["{loc.taille}"]      = loc != null ? $"{loc.tailleLot:0.##} m²" : "";
        m["{loc.bail}"]        = loc != null ? loc.typeDeBail.ToString() : "";
        m["{loc.debutBail}"]   = D(loc?.dateDebutBailISO);
        m["{loc.finBail}"]     = D(loc?.dateFinBailISO);
        m["{loc.premierBail}"] = D(loc?.dateDebutPremierBailISO);
        m["{loc.depot}"]       = loc != null ? $"{loc.depotDeGarantie:N2} €" : "";
        m["{loc.loyer}"]       = loc != null ? $"{loc.loyerAnnuel:N2} €" : "";
        m["{loc.periodicite}"] = periodicite;
        m["{loc.provision}"]   = loc != null && loc.provisionPourCharges
                                 ? $"{loc.provisionPourChargeValue:N2} €" : "—";
        m["{loc.taux}"]        = loc != null ? $"{loc.tauxDeRentabilité:0.##} %" : "";

        // ── Bâtiment ──
        m["{bat.nom}"]         = S(bat?.Name);
        m["{bat.adresse}"]     = S(bat?.adressBatiment);
        m["{bat.taille}"]      = bat != null ? $"{bat.tailleBatiment:0.##} m²" : "";
        m["{bat.terrain}"]     = bat != null ? $"{bat.tailleTerrain:0.##} ca" : "";
        m["{bat.cadastre}"]    = S(bat?.cadastral);
        m["{bat.acquisition}"] = D(bat?.dateAcquisitionISO);

        // ── Facture / contexte ──
        m["{date}"]            = ctx.date == default ? "" : ctx.date.ToString("dd/MM/yyyy");
        m["{periode}"]         = S(ctx.periode);
        m["{numero}"]          = S(ctx.numero);
        m["{loyer.ht}"]        = $"{ctx.loyerHT:N2} €";
        m["{loyer.tva}"]       = $"{ctx.tva:N2} €";
        m["{loyer.ttc}"]       = $"{ctx.ttc:N2} €";

        // ── Société ──
        m["{societe.nom}"]     = SocieteNom;
        m["{societe.siret}"]   = SocieteSiret;
        m["{societe.tva}"]     = SocieteTva;
        return m;
    }

    static string S(string s) => s ?? "";
    static string D(string iso) => DateTime.TryParse(iso, out var d) ? d.ToString("dd/MM/yyyy") : "";
}
