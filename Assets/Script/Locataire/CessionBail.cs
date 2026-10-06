using System;
using System.Collections.Generic;
using System.Linq;

/// Une cession du bail (décision du 06/10) : le cessionnaire reprend le bail tel quel —
/// loyer, dépôt, suivi continuent sur la même fiche ; cédant et cessionnaire s'arrangent
/// entre eux (dépôt, factures). On garde la trace de qui était là avant.
[Serializable]
public class CessionBail
{
    public string dateISO;
    public string ancienNom, ancienSiret, ancienAdresse, ancienEmail;
}

/// Cession à venir (retour du 06/10 : « il vaut mieux que le changement de locataire se
/// fasse le jour de la cession ») : la fiche garde le cédant jusqu'à cette date.
[Serializable]
public class CessionPrevue
{
    public string dateISO;
    public string nom, siret, adresse, email;
}

/// Règles de la cession du bail. Les factures déjà émises gardent l'ancien nom (leur PDF
/// est fait) ; les suivantes — régul de l'année comprise — partent au cessionnaire.
public static class Cessions
{
    /// La cession enregistrée pour plus tard, ou null (JsonUtility recrée un objet vide).
    public static CessionPrevue Prevue(Locataire loc)
        => loc?.cessionPrevue != null && FacturationSuivi.TryEcheance(loc.cessionPrevue.dateISO, out _) ? loc.cessionPrevue : null;

    /// Date passée ou du jour : la fiche change de titulaire tout de suite. Date à venir :
    /// la cession attend son jour (Effectuer, au lancement). Vrai si appliquée maintenant.
    public static bool Enregistrer(Locataire loc, DateTime date, string nom, string siret, string adresse, string email, DateTime today)
    {
        if (date.Date <= today.Date)
        {
            Appliquer(loc, date, nom, siret, adresse, email);
            loc.cessionPrevue = null;
            return true;
        }
        loc.cessionPrevue = new CessionPrevue
        {
            dateISO = date.ToString("yyyy-MM-dd"), nom = nom.Trim(), siret = (siret ?? "").Trim(),
            adresse = adresse ?? "", email = (email ?? "").Trim(),
        };
        return false;
    }

    /// La cession prévue dont le jour est arrivé, sinon null.
    public static CessionPrevue Due(Locataire loc, DateTime today)
    {
        var p = Prevue(loc);
        return p != null && FacturationSuivi.TryEcheance(p.dateISO, out var d) && d.Date <= today.Date ? p : null;
    }

    /// Applique la cession prévue si son jour est arrivé. Vrai si la fiche a changé.
    public static bool Effectuer(Locataire loc, DateTime today)
    {
        var p = Due(loc, today);
        if (p == null || !FacturationSuivi.TryEcheance(p.dateISO, out var d)) return false;
        Appliquer(loc, d, p.nom, p.siret, p.adresse, p.email);
        loc.cessionPrevue = null;
        return true;
    }

    /// Message de refus, ou null si la cession peut s'enregistrer.
    public static string Verifier(Locataire loc, DateTime date, string nouveauNom)
    {
        if (string.IsNullOrWhiteSpace(nouveauNom) || nouveauNom.Trim() == Data.NomParDefaut)
            return "Saisissez le nom du nouveau locataire (cessionnaire).";
        if (FacturationSuivi.TryEcheance(loc?.dateDebutBailISO, out var debut) && date.Date < debut.Date)
            return $"La cession ne peut pas précéder le début du bail ({debut:dd/MM/yyyy}).";
        if (DepartLocataire.Sortie(loc, out var sortie) && date.Date > sortie)
            return $"Le locataire part le {sortie:dd/MM/yyyy} : une cession après son départ n'a pas de sens.";
        return null;
    }

    /// Enregistre la cession : l'ancien titulaire va dans l'historique, la fiche prend
    /// les coordonnées du cessionnaire. Les destinataires mémorisés sur les factures sont
    /// effacés, sinon la prochaine facture partirait encore au nom du cédant.
    public static void Appliquer(Locataire loc, DateTime date, string nom, string siret, string adresse, string email)
    {
        (loc.cessions ??= new List<CessionBail>()).Add(new CessionBail
        {
            dateISO = date.ToString("yyyy-MM-dd"),
            ancienNom = loc.Name, ancienSiret = loc.siretNumber,
            ancienAdresse = loc.adresseLocataire, ancienEmail = loc.emailLocataire,
        });
        loc.Name = nom.Trim();
        loc.siretNumber = (siret ?? "").Trim();
        loc.adresseLocataire = adresse ?? "";
        loc.emailLocataire = (email ?? "").Trim();
        foreach (var f in new[] { loc.factureLoyer, loc.factureRegul, loc.factureRefac, loc.factureDepot })
        {
            if (f == null) continue;
            f.destNom = ""; f.destAdresse = ""; f.destSiret = ""; f.emailDest = "";
        }
    }

    /// « cession prévue le 01/11/2026 à SARL Y · cédé par SARL X le 01/06/2026 » (la plus
    /// récente d'abord), ou "".
    public static string Texte(Locataire loc)
    {
        var parts = new List<string>();
        var p = Prevue(loc);
        if (p != null && FacturationSuivi.TryEcheance(p.dateISO, out var dp))
            parts.Add($"cession prévue le {dp:dd/MM/yyyy} à {p.nom}");
        if (loc?.cessions != null)
            parts.AddRange(loc.cessions.OrderByDescending(c => c.dateISO)
                .Select(c => $"cédé par {c.ancienNom} le {(FacturationSuivi.TryEcheance(c.dateISO, out var d) ? d.ToString("dd/MM/yyyy") : "?")}"));
        return string.Join(" · ", parts);
    }
}
