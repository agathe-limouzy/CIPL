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
        Figer(loc);   // Revue 17 (D4-01, lot 6) : dès la saisie, même pour une cession à venir
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
        Figer(loc);   // Revue 17 (D4-01, lot 6) : avant tout changement de la fiche
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
        // code comptable. Revue 17 (D7a-12, lot D) : la TVA, elle, est celle du bail (décision du 02/11) : elle ne change pas.
        loc.ribLocataireTitulaire = loc.ribLocataireIban = loc.ribLocataireBic = "";
        loc.telephoneLocataire = ""; loc.codeCompatable = "";
        // Revue 17 (D7a-12, lot D) : le bloc Pappers du commentaire décrit la société du cédant (SIREN, état, dirigeants) : remplacé.
        // Celui du cessionnaire vient de son SIRET (PappersSync.Un après une cession immédiate, la tournée du lancement sinon).
        if ((loc.commentaire ?? "").Contains(PappersResume.Debut))
            loc.commentaire = PappersResume.Fusionner(loc.commentaire,
                $"Bail cédé le {date:dd/MM/yyyy} : la fiche société du cédant est retirée ; celle du nouveau titulaire suivra son SIRET.");
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
        var p = PrevueDe(loc, debutPeriode);
        if (p != null) return new[] { p.nom, p.siret, p.adresse, p.email };
        var c = FaiteDe(loc, debutPeriode);
        return c != null ? new[] { c.ancienNom, c.ancienSiret, c.ancienAdresse, c.ancienEmail } : null;
    }

    // La cession prévue qui commence au plus tard ce jour-là, sinon null.
    static CessionPrevue PrevueDe(Locataire loc, DateTime debutPeriode)
        => Prevue(loc) is CessionPrevue p && FacturationSuivi.TryEcheance(p.dateISO, out var dp) && dp.Date <= debutPeriode.Date ? p : null;

    // La première cession faite après ce jour-là (son cédant tenait le bail ce jour-là), sinon null.
    static CessionBail FaiteDe(Locataire loc, DateTime debutPeriode)
        => loc?.cessions?.Where(x => x != null && FacturationSuivi.TryEcheance(x.dateISO, out var dc) && debutPeriode.Date < dc.Date)
            .OrderBy(x => x.dateISO).FirstOrDefault();

    /// Revue 17 (D4-01, lot 6 ; D4-N3) : ce que le formulaire du loyer a tapé pour une période d'une cession prévue (nom, SIRET,
    /// adresse, e-mail) est recopié dans cette cession. Avant, l'e-mail tapé pour le cessionnaire n'était enregistré nulle part :
    /// l'envoi différé partait sans adresse, sans message. Relecture du lot 6 : une cession faite n'est pas réécrite (l'adresse
    /// tapée pour la correction d'un loyer du cédant reste sur sa ligne, destinataire, lue d'abord par AcheteurDe ; un nom retouché
    /// aurait détaché toutes ses autres factures de sa cession) ; un nom ou un e-mail laissé vide n'efface pas celui de la cession
    /// (sans nom elle n'existe plus, sans e-mail l'envoi différé est refusé après coup). Vrai si la cession prévue l'a reçu.
    public static bool Reporter(Locataire loc, DateTime debutPeriode, string nom, string siret, string adresse, string email)
    {
        var p = PrevueDe(loc, debutPeriode);
        if (p == null) return false;
        if (!string.IsNullOrWhiteSpace(nom)) p.nom = nom.Trim();
        p.siret = (siret ?? "").Trim(); p.adresse = adresse ?? "";
        if (!string.IsNullOrWhiteSpace(email)) p.email = email.Trim();
        return true;
    }

    /// Le titulaire d'une ligne de loyer (clé « loyer-{année}-P{période} ») ; null : la fiche, ou une ligne d'un autre type.
    public static string[] TitulaireLigne(Locataire loc, FactureEtat rec)
    {
        var parts = (rec?.key ?? "").Split('-');
        if (loc == null || rec?.type != "Loyer" || parts.Length < 3 || !int.TryParse(parts[1], out int annee)
            || !int.TryParse(parts[2].TrimStart('P', 'p'), out int periode)) return null;
        // Revue 17 (D4-15, lot 6) : émise sous une autre périodicité, la clé ne désigne pas cette période (D4-04) : la fiche, comme
        // DepartLocataire.PeriodeApresDepart. Les lignes d'avant le 04/11 n'ont pas de périodicité : rien à comparer.
        // Revue 17 (avenant de périodicité, lot 14) : la périodicité de SA période dans le plan de l'année, pas celle d'aujourd'hui.
        if (!string.IsNullOrEmpty(rec.periodicite) && rec.periodicite != FacturationSuivi.PeriodiciteDe(loc, annee, periode).ToString()) return null;
        FacturationSuivi.PeriodeBornes(loc, annee, periode, out var debut, out _);
        return TitulaireDe(loc, debut);
    }

    /// Revue 17 (D4-01, lot 6) : le nom du titulaire d'une ligne : celui de sa période pour un loyer (TitulaireLigne), sinon la fiche.
    public static string Titulaire(Locataire loc, FactureEtat rec) => TitulaireLigne(loc, rec)?[0] ?? loc?.Name ?? "";

    /// Revue 17 (D4-01, décision du 05/11, lot 6) : la facture est à un autre titulaire que celui de la fiche (un cédant, un futur
    /// cessionnaire) : son acheteur gardé n'est pas le nom de la fiche, mais celui d'une cession. Elle garde alors son adresse
    /// d'envoi ; une facture du titulaire de la fiche suit l'adresse actuelle. Faux sans acheteur (lignes d'avant la mise à jour).
    public static bool AutreTitulaire(Locataire loc, FactureEtat rec) => AcheteurDe(loc, rec) != null;

    /// Revue 17 (D4-01, lot 6) : l'acheteur d'une facture d'un autre titulaire (AutreTitulaire) : {nom, siret, adresse, email} de la
    /// cession, prévue ou faite, qui porte ce nom, l'adresse d'envoi gardée sur la ligne passant avant celle de la cession. Null
    /// pour une facture du titulaire de la fiche, sans acheteur, ou dont l'acheteur n'est le nom d'aucune cession (relecture du
    /// lot 6 : une fiche renommée sans cession, « SARL X » → « SAS X », gardait ses factures préparées sous l'ancien nom comme
    /// celles d'un cédant : retenues au lancement, rappel à l'adresse figée, quittance et formulaires sans adresse ; c'est la règle
    /// de la fiche qui vaut). Le rappel, la notification, l'envoi différé, la quittance et la correction d'une facture d'un cédant
    /// lisent ceci, et non le réglage du type ni la fiche, passés au cessionnaire.
    public static string[] AcheteurDe(Locataire loc, FactureEtat rec)
    {
        string nom = (rec?.acheteur ?? "").Trim();
        if (nom == "" || nom == (loc?.Name ?? "").Trim()) return null;
        var c = CessionDe(loc, nom);
        if (c == null) return null;
        string email = (rec.destinataire ?? "").Trim();
        if (email != "") c[3] = email;
        return c;
    }

    // {nom, siret, adresse, email} de la cession, prévue ou faite (la plus récente), qui porte ce nom ; null sinon.
    static string[] CessionDe(Locataire loc, string nom)
    {
        if (Prevue(loc) is CessionPrevue p && (p.nom ?? "").Trim() == nom) return new[] { p.nom, p.siret, p.adresse, p.email };
        var c = loc?.cessions?.Where(x => x != null && (x.ancienNom ?? "").Trim() == nom).OrderByDescending(x => x.dateISO).FirstOrDefault();
        return c != null ? new[] { c.ancienNom, c.ancienSiret, c.ancienAdresse, c.ancienEmail } : null;
    }

    /// Revue 17 (D4-01, décision du 05/11, lot 6) : un loyer préparé au nom d'un autre que le titulaire de sa période (préparé au
    /// nom du cédant avant la saisie de la cession) : retenu à l'envoi (FactureEnvoiAuto.DoitPartir), signalé dans le suivi ;
    /// « Refaire » le refait au même numéro au nom du titulaire. Faux sans acheteur (lignes d'avant : rien à comparer), et pour un
    /// acheteur qui n'est pas un nom du bail (fiche, cession prévue, cédants ; plan D4-01 (g)) : l'ancien nom d'une fiche renommée
    /// sans cession n'est pas « à refaire » (relecture du lot 6).
    public static bool HorsTitulaire(Locataire loc, FactureEtat rec)
    {
        string nom = (rec?.acheteur ?? "").Trim();
        return rec?.type == "Loyer" && nom != "" && nom != Titulaire(loc, rec).Trim()
            && (nom == (loc?.Name ?? "").Trim() || CessionDe(loc, nom) != null);
    }

    /// Revue 17 (D4-01, lot 6) : avant qu'une cession change la fiche (nom, e-mail, réglages des types), les lignes d'avant la mise
    /// à jour reçoivent ce qui leur manque, sur toute ligne qui porte un document : le nom de leur titulaire et l'adresse d'envoi
    /// du jour. Sans cela, le loyer préparé au nom du cédant partait chez le cessionnaire avec le PDF du cédant, et le rappel d'un
    /// impayé du cédant allait au cessionnaire. L'adresse est figée aussi sur un loyer pas encore parti (relecture du lot 6) : tant
    /// que la ligne reste celle du titulaire de la fiche, elle est ignorée (AutreTitulaire faux) ; le jour où il devient cédant,
    /// l'« Email d'envoi » du type, effacé par Appliquer, est là. Dès l'enregistrement (même d'une cession à venir) et à
    /// l'application : un loyer préparé entre les deux serait figé trop tard. Ne touche à rien de déjà rempli.
    static void Figer(Locataire loc)
    {
        foreach (var r in loc?.facturesEtat ?? new List<FactureEtat>())
        {
            if (r == null || string.IsNullOrEmpty(r.pdfPath)) continue;
            if (string.IsNullOrWhiteSpace(r.destinataire)) r.destinataire = FactureEnvoiAuto.Destinataire(loc, r.type, r);
            if (string.IsNullOrWhiteSpace(r.acheteur)) r.acheteur = Titulaire(loc, r);
        }
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
