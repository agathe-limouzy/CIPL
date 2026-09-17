using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

/// Quittance de loyer (diagramme : « Payé → location non commerciale → possibilité
/// d'émettre la créance/quittance de loyer »). Génère un PDF de quittance en local
/// (même infrastructure que les factures) puis l'ouvre. Aucune émission réelle.
public static class FactureQuittanceService
{
    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static void Emettre(LocatairePrefab fiche, Locataire loc, FactureEtat l)
    {
        if (fiche == null || loc == null || l == null) return;

        var R = ReglageService.Current;
        var foot = (R?.basDePage ?? "").Replace("\r", "").Split('\n');
        string periode = PeriodeDepuisLibelle(l.libelle);
        string montant = l.montant > 0f ? l.montant.ToString("#,##0.00", Fr) + " €" : "—";

        var d = new FacturePdfService.QuittanceData
        {
            clientNom = loc.Name,
            clientAdresseHtml = FacturePdfService.AdresseHtml(loc.adresseLocataire),
            dateStr = DateTime.Today.ToString("d MMMM yyyy", FacturePdfService.FrCulture),
            periode = string.IsNullOrEmpty(periode) ? "—" : periode,
            designation = Designation(fiche, loc),
            montantStr = montant,
            foot1 = foot.Length > 0 ? foot[0] : "",
            foot2 = foot.Length > 1 ? foot[1] : "",
        };

        // Meme dossier que les factures du locataire. La quittance partait dans
        // Batiment/<GUID>/<GUID>/Facture : introuvable a cote des factures.
        string dir = DossiersDonnees.DossierFactures(
            fiche.batimentPrefabOrigin.getName(), loc.Name);
        string pdf = Path.Combine(dir, Sanitize($"Quittance-{loc.Name}-{periode}") + ".pdf");

        if (!FacturePdfService.GenerateQuittancePdf(d, pdf, out string err))
        {
            UndoToast.Instance?.ShowInfo("Échec de la quittance : " + err);
            return;
        }
        Application.OpenURL("file:///" + pdf.Replace("\\", "/"));
        UndoToast.Instance?.ShowInfo("Quittance de loyer générée (PDF).");
    }

    // « Loyer Janvier 2026 — corrigée(2) » → « Janvier 2026 ».
    static string PeriodeDepuisLibelle(string lib)
    {
        string s = lib ?? "";
        int i = s.IndexOf(" — corrigée(", StringComparison.Ordinal);
        if (i >= 0) s = s.Substring(0, i);
        if (s.StartsWith("Loyer ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(6);
        return s.Trim();
    }

    static string Designation(LocatairePrefab fiche, Locataire loc)
    {
        string bat = fiche.batimentPrefabOrigin != null ? fiche.batimentPrefabOrigin.getName() : "";
        string lot = loc.lotBatiment > 0 ? $"lot {loc.lotBatiment}" : "";
        return string.Join(" — ", new[] { bat, lot }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    // Comme DossiersDonnees.NomFichier, avec en plus les espaces en tirets et un
    // repli : le nom du fichier de quittance ne doit jamais etre vide.
    static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Quittance";
        return DossiersDonnees.NomFichier(s).Replace(' ', '-');
    }
}
