using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// Étape 13 (décisions du 23/10) : « Créer mon compte ». Société (SIRET vérifié dans l'annuaire) ou
/// particulier ; l'adresse e-mail prouvée par un code ; un seul groupe par adresse ; essai de 14 jours ;
/// des Réglages de départ tirés de ce qui est saisi, à la place de ceux de CIPL.
public static class RegleInscription
{
    [Serializable] public class Resultat { public bool ok; public string refus, nom, id; }

    /// « Patrimoine de Jean Martin » : le nom proposé pour l'entreprise d'un particulier.
    public static string NomParticulier(string prenom, string nom)
    {
        string qui = $"{(prenom ?? "").Trim()} {(nom ?? "").Trim()}".Trim();
        return qui == "" ? "" : "Patrimoine de " + qui;
    }

    /// La ville en fin d'adresse, après le code postal (« 4 rue des Lilas, 31000 Toulouse »), ou null.
    public static string Ville(string adresse)
    {
        var m = Regex.Match(adresse ?? "", @"\b\d{5}\s+([^\d,][^,]*?)\s*$");
        return m.Success ? m.Groups[1].Value : null;
    }

    // Les zones du formulaire (avec celles de RegleMotDePasse), pour montrer en rouge celle d'un refus.
    public const string ChampSiret = "siret", ChampEntreprise = "entreprise", ChampPrenom = "prenom", ChampNom = "nom",
                        ChampAdresse = "adresse", ChampMonNom = "monNom";

    /// Ce qu'il manque au formulaire, ou null.
    public static string Refus(bool societe, string siret, string nomEntreprise, string prenom, string nom, string adresse,
                               string monNom, string email, string mdp, string confirmation) =>
        Refus(societe, siret, nomEntreprise, prenom, nom, adresse, monNom, email, mdp, confirmation, out _);

    /// `champs` : les zones en cause.
    public static string Refus(bool societe, string siret, string nomEntreprise, string prenom, string nom, string adresse,
                               string monNom, string email, string mdp, string confirmation, out string[] champs)
    {
        if (societe)
        {
            string s = RegleAbonnement.VerifierSiret(siret);
            if (s != null) { champs = new[] { ChampSiret }; return s; }
        }
        else
        {
            var vides = new[] { (ChampPrenom, prenom), (ChampNom, nom) }.Where(x => string.IsNullOrWhiteSpace(x.Item2)).Select(x => x.Item1).ToArray();
            if (vides.Length > 0) { champs = vides; return "Saisissez votre prénom et votre nom."; }
            if (Ville(adresse) == null)
            {
                champs = new[] { ChampAdresse };
                return "Adresse : terminez par le code postal et la ville, par exemple « 4 rue des Lilas, 31000 Toulouse ».";
            }
        }
        if (string.IsNullOrWhiteSpace(nomEntreprise))
        {
            champs = new[] { ChampEntreprise };
            return societe ? "Saisissez la raison sociale." : "Saisissez le nom de l'entreprise.";
        }
        if (societe && string.IsNullOrWhiteSpace(monNom)) { champs = new[] { ChampMonNom }; return "Saisissez votre nom : il est affiché à vos collègues."; }
        return RegleMotDePasse.RefusAdresse(email, out champs) ?? RegleMotDePasse.Refus(mdp, confirmation, out champs);
    }

    /// Zones en cause d'un refus du serveur.
    public static string[] Champs(Resultat r)
    {
        switch (r?.refus)
        {
            case "deja_membre": return new[] { RegleMotDePasse.ChampEmail };
            case "deja_sur_cipl": case "siret": return new[] { ChampSiret };
            case "nom": return new[] { ChampEntreprise };
            default: return new string[0];
        }
    }

    /// Les Réglages de départ (décision du 23/10) : le nom ; le bas de page des factures (raison sociale,
    /// forme, SIRET, siège ; ou nom et adresse du particulier) ; le lieu d'émission (la ville) ; le nom
    /// et l'adresse d'expédition des e-mails. `annuaire` : la fiche de la société, null si injoignable.
    public static ReglageData Reglages(bool societe, string nomEntreprise, string siret, AnnuaireEntreprise annuaire,
                                       string qui, string adresse, string email)
    {
        // Sans logo (retour du 25/10) : pas celui de CIPL sur les factures d'un client.
        var r = new ReglageData { entrepriseNom = (nomEntreprise ?? "").Trim(), sansLogo = true };
        // Retour du 25/10 : envoi par e-mail (Pennylane ne fait pas encore tout le chemin), et pas le
        // serveur d'envoi de CIPL : le guide « L'envoi des factures » le fait choisir.
        r.modeEnvoi = ModeEnvoi.Email;
        r.smtp.host = "";
        r.smtp.fromName = r.entrepriseNom;
        r.smtp.fromEmail = (email ?? "").Trim();
        if (societe)
        {
            string s = new string((siret ?? "").Where(char.IsDigit).ToArray());
            string lisible = RegleAbonnement.SiretLisible(s);
            r.basDePage = string.Join(" - ", new[] { annuaire?.nom_complet ?? r.entrepriseNom, annuaire?.libelle_nature_juridique, "Siret : " + lisible }
                                             .Where(x => !string.IsNullOrWhiteSpace(x)));
            if (!string.IsNullOrWhiteSpace(annuaire?.siege?.adresse)) r.basDePage += "\nSiège social : " + annuaire.siege.adresse.Trim();
            // ponytail: annuaire injoignable → le lieu par défaut reste (à corriger dans Réglages).
            string ville = annuaire?.siege?.Ville;
            if (!string.IsNullOrWhiteSpace(ville))
                r.lieuEmission = CultureInfo.GetCultureInfo("fr-FR").TextInfo.ToTitleCase(ville.ToLowerInvariant());
        }
        else
        {
            r.basDePage = $"{(qui ?? "").Trim()} - {(adresse ?? "").Trim()}";
            r.lieuEmission = Ville(adresse) ?? r.lieuEmission;
        }
        return r;
    }

    /// Paramètre de la fonction adresse_deja_membre (retour du 25/10 : refus avant l'envoi du code).
    public static string CorpsAdresse(string email) => "{\"p_email\":" + Q((email ?? "").Trim()) + "}";

    /// Paramètre de la fonction siret_deja_sur_cipl (retour du 25/10, AO8 : refus avant le code).
    public static string CorpsSiret(string siret) => "{\"p_siret\":" + Q(new string((siret ?? "").Where(char.IsDigit).ToArray())) + "}";

    /// Paramètres de la fonction inscrire ; `jsonReglages` est un objet JSON (ReglageData).
    public static string Corps(string type, string nom, string siret, string monNom, string jsonReglages) =>
        "{\"p_type\":" + Q(type) + ",\"p_nom\":" + Q(nom) + ",\"p_siret\":" + Q(siret) + ",\"p_mon_nom\":" + Q(monNom) +
        ",\"p_reglages\":" + jsonReglages + "}";

    /// Le refus du serveur, en clair.
    public static string Motif(Resultat r)
    {
        switch (r?.refus)
        {
            case "deja_membre":   // nom vide : vérifié avant le code, sans dire le groupe
                return $"Cette adresse e-mail a déjà un compte CIPL{(string.IsNullOrEmpty(r.nom) ? "" : $" (« {r.nom} »)")} : connectez-vous avec. " +
                       "Pour créer votre propre groupe, utilisez une autre adresse (par exemple celle de votre patrimoine personnel).";
            case "deja_sur_cipl": return "Cette société est déjà sur CIPL : demandez une invitation à son administrateur.";
            case "siret": return "Le SIRET compte 14 chiffres.";
            case "nom": return "Saisissez le nom de l'entreprise.";
            case null: return "Réponse du serveur illisible.";
            default: return "Refusé : " + r.refus;
        }
    }

    static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
