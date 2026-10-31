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
    public string echeance;    // « 01/02/2026 » (e-mail de notification, étape 18b)
    public float reste;        // reste à payer (rappel d'impayé)
}

/// Remplace les tokens {loc.*}, {bat.*}, {societe.*}, {date}, {loyer.*}… d'un
/// texte d'entête par les valeurs réelles (voir FactureVariables pour le catalogue).
public static class FactureVarResolver
{
    // Société émettrice quand aucune entreprise n'a de nom (Groupe CIPL, comme avant le multi-entreprise).
    public const string SocieteNom = "GROUPE CIPL";
    public const string SocieteSiret = "717 220 883 00044";
    public const string SocieteTva = "FR 28 717 220 883";

    /// L'entreprise ouverte (30/10 : une SCI par entreprise) : nom de Réglages › Entreprise, sinon celui du
    /// cloud ; SIRET et TVA (calculée d'après le SIREN) du cloud. Sans aucun nom, les constantes. Un SIRET
    /// inconnu reste vide : jamais celui de GROUPE CIPL sous le nom d'une autre société.
    public static (string nom, string siret, string tva) Societe(string nomReglages, EntrepriseCloud ent)
    {
        string nom = !string.IsNullOrWhiteSpace(nomReglages) ? nomReglages.Trim() : ent?.nom?.Trim();
        if (string.IsNullOrEmpty(nom)) return (SocieteNom, SocieteSiret, SocieteTva);
        string siret = string.IsNullOrWhiteSpace(ent?.siret) ? "" : RegleAbonnement.SiretLisible(ent.siret);
        string tva = FactureEn16931.TvaIntra(SocieteLocataire.Siren(ent?.siret));   // « FR28717220883 » ou vide
        return (nom, siret, tva == "" ? "" : $"FR {tva.Substring(2, 2)} {tva.Substring(4, 3)} {tva.Substring(7, 3)} {tva.Substring(10)}");
    }

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
        m["{loc.bail}"]        = loc != null ? Locataire.LibelleBail(loc.typeDeBail) : "";   // libellé, pas le nom interne
        m["{loc.debutBail}"]   = D(loc?.dateDebutBailISO);
        m["{loc.finBail}"]     = D(loc?.dateFinBailISO);
        m["{loc.premierBail}"] = D(loc?.dateDebutPremierBailISO);
        m["{loc.depot}"]       = loc != null ? $"{loc.depotDeGarantie:N2} €" : "";
        m["{loc.loyer}"]       = loc != null ? $"{loc.loyerAnnuel:N2} €" : "";
        m["{loc.periodicite}"] = periodicite;
        m["{loc.provision}"]   = loc != null && loc.provisionPourCharges
                                 ? $"{ListesCharges.ProvisionTotale(loc):N2} €" : "—";   // toutes listes
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
        m["{echeance}"]        = S(ctx.echeance);
        m["{loyer.ht}"]        = $"{ctx.loyerHT:N2} €";
        m["{loyer.tva}"]       = $"{ctx.tva:N2} €";
        m["{loyer.ttc}"]       = $"{ctx.ttc:N2} €";
        m["{reste}"]           = $"{ctx.reste:N2} €";
        m["{retard}"]          = S(ReglageService.Current?.phraseRetard);

        // ── Société ──
        var (nom, siret, tva) = Societe(ReglageService.Current?.entrepriseNom, SessionCloud.Entreprise);
        m["{societe.nom}"]     = nom;
        m["{societe.siret}"]   = siret;
        m["{societe.tva}"]     = tva;
        return m;
    }

    static string S(string s) => s ?? "";
    static string D(string iso) => DateTime.TryParse(iso, out var d) ? d.ToString("dd/MM/yyyy") : "";
}
