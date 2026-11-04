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
        // Revue 16c (D7a-05) : ce qui est propre au cédant ne passe pas au cessionnaire : coordonnées bancaires, téléphone,
        // code comptable, et le choix TVA fait à la main (la case suit alors le nouveau SIRET).
        loc.ribLocataireTitulaire = loc.ribLocataireIban = loc.ribLocataireBic = "";
        loc.telephoneLocataire = ""; loc.codeCompatable = "";
        foreach (var f in new[] { loc.factureLoyer, loc.factureRegul, loc.factureRefac, loc.factureDepot })
        {
            if (f == null) continue;
            f.destNom = ""; f.destAdresse = ""; f.destSiret = ""; f.emailDest = "";
        }
    }

    /// Revue 17 (D7a-03) : le titulaire du bail au premier jour d'une période, quand ce n'est pas celui de la fiche : le
    /// cessionnaire d'une cession prévue qui commence au plus tard ce jour-là (le loyer de décembre préparé en novembre lui
    /// revient), ou le cédant d'une cession faite après ce jour-là (le loyer de novembre corrigé après la cession). {nom,
    /// siret, adresse, email}, ou null : la fiche.
    public static string[] TitulaireDe(Locataire loc, DateTime debutPeriode)
    {
        var p = Prevue(loc);
        if (p != null && FacturationSuivi.TryEcheance(p.dateISO, out var dp) && dp.Date <= debutPeriode.Date)
            return new[] { p.nom, p.siret, p.adresse, p.email };
        var c = loc?.cessions?.Where(x => x != null && FacturationSuivi.TryEcheance(x.dateISO, out var dc) && debutPeriode.Date < dc.Date)
            .OrderBy(x => x.dateISO).FirstOrDefault();
        return c != null ? new[] { c.ancienNom, c.ancienSiret, c.ancienAdresse, c.ancienEmail } : null;
    }

    /// Le titulaire d'une ligne de loyer (clé « loyer-{année}-P{période} ») ; null : la fiche, ou une ligne d'un autre type.
    public static string[] TitulaireLigne(Locataire loc, FactureEtat rec)
    {
        var parts = (rec?.key ?? "").Split('-');
        if (loc == null || rec?.type != "Loyer" || parts.Length < 3 || !int.TryParse(parts[1], out int annee)
            || !int.TryParse(parts[2].TrimStart('P', 'p'), out int periode)) return null;
        FacturationSuivi.PeriodeBornes(loc, annee, periode, out var debut, out _);
        return TitulaireDe(loc, debut);
    }

    /// Retour BN12 (décision du 04/11) : « Cession le 01/12/2026 » pour la liste des locataires, ou null sans cession prévue.
    public static string Etiquette(Locataire loc)
        => Prevue(loc) is CessionPrevue p && FacturationSuivi.TryEcheance(p.dateISO, out var d) ? $"Cession le {d:dd/MM/yyyy}" : null;

    /// Retour BN15 (décision du 04/11) : « Cession le 01/12/2026 → SARL Y » pour la pastille de l'en-tête de la fiche, ou null.
    public static string TexteEntete(Locataire loc)
        => Prevue(loc) is CessionPrevue p && FacturationSuivi.TryEcheance(p.dateISO, out var d) ? $"Cession le {d:dd/MM/yyyy} → {p.nom}" : null;

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
