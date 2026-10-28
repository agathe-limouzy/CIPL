using System;
using System.Globalization;
using System.Linq;

/// Étape 9c1 : l'abonnement d'un groupe et ce qu'il permet (CONCEPTION §13, décisions du 11/10 et
/// du 19/10). Essai de 14 jours ; expiré : lecture seule pour tout le groupe ; paiement refusé :
/// toujours modifiable pendant que Stripe réessaie.
public static class RegleAbonnement
{
    public enum Etat { Essai, Actif, PaiementRefuse, Expire }

    // ponytail: prix de l'exemple du §13, en dur pour l'estimation ; Stripe fera foi à l'étape 9c2.
    public const int PrixEntreprise = 40, PrixEntrepriseSuivante = 28, PrixLicence = 15, MoisParAn = 10;

    /// Même règle que le serveur (ecriture_permise), avec la date du serveur.
    public static Etat Lire(string statut, DateTime finEssai, DateTime aujourdhui) =>
        statut == "actif" ? Etat.Actif
      : statut == "refuse" ? Etat.PaiementRefuse
      : statut == "essai" && finEssai.Date >= aujourdhui.Date ? Etat.Essai
      : Etat.Expire;

    public static bool PeutEcrire(Etat e) => e != Etat.Expire;

    /// Jours d'essai après aujourd'hui : 0 le dernier jour.
    public static int JoursRestants(DateTime finEssai, DateTime aujourdhui) => Math.Max(0, (finEssai.Date - aujourdhui.Date).Days);

    static string Restant(int jours) => jours == 0 ? "dernier jour" : jours == 1 ? "1 jour restant" : $"{jours} jours restants";

    /// Bandeau sous les onglets : les 7 derniers jours de l'essai (décision du 19/10), pour tous
    /// (retour du 19/10 : Odile doit le voir aussi) ; le bouton vers l'abonnement reste à l'admin.
    public static bool BandeauEssai(Etat e, int jours) => e == Etat.Essai && jours <= 7;

    public static string TexteEssai(int jours, bool admin, string nomAdmin) =>
        "Essai gratuit : " + Restant(jours) + "." + (admin ? "" : $" Voyez avec {Admin(nomAdmin)}.");

    /// Étape 9c2 : la formule, et la fin d'un abonnement résilié (il court jusqu'à l'échéance).
    public static string Pastille(Etat e, int jours, string formule = null, bool resilie = false, DateTime? echeance = null) =>
        e == Etat.Essai ? "Essai gratuit · " + Restant(jours)
      : e == Etat.Actif && resilie && echeance.HasValue ? $"Actif jusqu'au {echeance.Value:dd/MM/yyyy} · résilié"
      : e == Etat.Actif ? (string.IsNullOrEmpty(formule) ? "Actif" : "Actif · " + formule)
      : e == Etat.PaiementRefuse ? "Paiement refusé"
      : "Expiré";

    public static string Explication(Etat e, DateTime finEssai, DateTime? echeance = null, bool resilie = false) =>
        e == Etat.Essai ? $"Jusqu'au {finEssai:dd/MM/yyyy}. Ensuite, CIPL passe en lecture seule pour tout le groupe tant qu'aucune offre n'est choisie."
      : e == Etat.Actif && echeance.HasValue ? (resilie ? $"Résilié : CIPL passe en lecture seule le {echeance.Value:dd/MM/yyyy}."
                                                        : $"Prochaine échéance le {echeance.Value:dd/MM/yyyy}.")
      : e == Etat.Actif ? "Votre abonnement est en cours."
      : e == Etat.PaiementRefuse ? "Le dernier paiement a été refusé. CIPL reste modifiable pendant les nouvelles tentatives : mettez à jour votre moyen de paiement dans « Paiement et factures »."
      : "CIPL est en lecture seule pour tout le groupe tant qu'aucune offre n'est choisie.";

    // ── Étape 9c2 : paiement avec Stripe (décisions du 22/10) ──────────────────

    /// Prix HT, TVA 20 % en plus.
    public static decimal Ttc(int ht) => Math.Round(ht * 1.2m, 2);

    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// « 102 € », « 135,60 € ».
    public static string Euros(decimal montant) => (montant % 1 == 0 ? montant.ToString("0", Fr) : montant.ToString("0.00", Fr)) + " €";

    /// Jamais moins de licences que de personnes (membres et invitations en cours), ni moins d'une.
    public static int LicencesMin(int utilisees) => Math.Max(1, utilisees);

    /// Premier prélèvement : le lendemain de la fin de l'essai, si Stripe a ses 48 h d'avance
    /// (3 jours d'écart en dates) ; sinon aujourd'hui. Même règle que la fonction abonnement-stripe.
    public static DateTime PremierPrelevement(Etat e, DateTime finEssai, DateTime aujourdhui)
    {
        var lendemain = finEssai.Date.AddDays(1);
        return e == Etat.Essai && (lendemain - aujourdhui.Date).Days >= 3 ? lendemain : aujourdhui.Date;
    }

    public static string TextePrelevement(DateTime premier, DateTime aujourdhui) =>
        premier.Date > aujourdhui.Date ? $"Premier prélèvement le {premier:dd/MM/yyyy}, à la fin de l'essai." : "Premier prélèvement aujourd'hui.";

    /// Confirmation de « - » / « + » sur un abonnement en cours : prorata immédiat.
    /// Licences par entreprise (décision du 23/10) : `entreprise` nomme celle qui change.
    public static string TexteLicences(int ecart, bool annuel, string entreprise = null)
    {
        int n = Math.Abs(ecart), prix = PrixLicence * (annuel ? MoisParAn : 1) * n;
        string quoi = n + " licence" + (n > 1 ? "s" : ""), periode = annuel ? "par an" : "par mois";
        string ou = string.IsNullOrEmpty(entreprise) ? "" : (ecart > 0 ? " à " : " de ") + entreprise;
        return ecart > 0 ? $"Ajouter {quoi}{ou} : +{prix} € HT {periode}, au prorata dès aujourd'hui."
                         : $"Retirer {quoi}{ou} : -{prix} € HT {periode}, avec un avoir au prorata.";
    }

    /// Confirmation de « Résilier l'abonnement » (retour du 22/10) : effectif à l'échéance.
    public static string TexteResiliation(DateTime? echeance) =>
        (echeance.HasValue ? $"Votre abonnement reste actif jusqu'au {echeance.Value:dd/MM/yyyy}." : "Votre abonnement reste actif jusqu'à la fin de la période en cours.") +
        " Ensuite, CIPL passe en lecture seule pour tout le groupe. Vous pourrez le reprendre d'ici là.";

    /// Confirmation de « Reprendre l'abonnement » (retour du 22/10 : rien ne se reprend sans elle).
    public static string TexteReprise(DateTime? echeance) =>
        (echeance.HasValue ? $"Votre abonnement continue après le {echeance.Value:dd/MM/yyyy} : le prochain prélèvement aura lieu à cette date"
                           : "Votre abonnement continue : le prochain prélèvement aura lieu à la fin de la période en cours")
        + ", et CIPL reste modifiable.";

    /// Abonnement résilié (décision du 22/10) : un bandeau pour tous dès la résiliation, qui devient
    /// un avertissement les 7 derniers jours avant la lecture seule.
    public static bool BandeauResilie(Etat e, bool resilie, DateTime? echeance, DateTime aujourdhui) =>
        e == Etat.Actif && resilie && echeance.HasValue && echeance.Value.Date >= aujourdhui.Date;

    public static bool AlerteResilie(DateTime echeance, DateTime aujourdhui) => (echeance.Date - aujourdhui.Date).Days <= 7;

    public static string TexteResilie(DateTime echeance, DateTime aujourdhui, bool admin, string nomAdmin) =>
        (AlerteResilie(echeance, aujourdhui)
            ? $"Abonnement résilié : {Restant((echeance.Date - aujourdhui.Date).Days)} avant la lecture seule (le {echeance:dd/MM/yyyy})."
            : $"Abonnement résilié : CIPL passera en lecture seule le {echeance:dd/MM/yyyy}.")
        + (admin ? "" : $" Voyez avec {Admin(nomAdmin)}.");

    /// Bandeau orange de paiement refusé : à l'admin seulement (décision du 11/10).
    public static bool BandeauRefus(Etat e, bool admin) => admin && e == Etat.PaiementRefuse;

    public const string TexteRefusPaiement = "Le dernier paiement a été refusé. Mettez à jour votre moyen de paiement.";

    static string Admin(string nomAdmin) => string.IsNullOrWhiteSpace(nomAdmin) ? "votre administrateur" : nomAdmin + " (admin)";

    /// Bandeau d'expiration : l'admin a le bouton vers l'abonnement, les autres le nom de l'admin.
    public static string TexteExpire(bool admin, string nomAdmin) =>
        "Abonnement expiré : CIPL est en lecture seule." + (admin ? "" : $" Voyez avec {Admin(nomAdmin)}.");

    /// Refus d'une écriture, au centre (comme hors ligne).
    public static string Refus(bool admin, string nomAdmin) =>
        "Abonnement expiré — lecture seule : rien ne peut être modifié. " +
        (admin ? "Voyez Réglages › Abonnement." : $"Voyez avec {Admin(nomAdmin)}.");

    /// Prix par mois : 1re entreprise, chacune des suivantes -30 %, chaque licence.
    /// Exemple du §13 : 3 SCI, 5 licences → 40 + 2 × 28 + 5 × 15 = 171 €.
    public static int Total(int entreprises, int licences) =>
        entreprises <= 0 ? 0 : PrixEntreprise + PrixEntrepriseSuivante * (entreprises - 1) + PrixLicence * licences;

    /// Null si le SIRET est plausible (14 chiffres, clé de Luhn ; La Poste a sa règle), sinon le motif.
    public static string VerifierSiret(string siret)
    {
        string s = (siret ?? "").Replace(" ", "").Trim();
        if (s.Length != 14 || !s.All(c => c >= '0' && c <= '9')) return "Le SIRET compte 14 chiffres.";
        int somme = 0;
        for (int i = 0; i < 14; i++)
        {
            int d = s[13 - i] - '0';
            if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
            somme += d;
        }
        bool laPoste = s.StartsWith("356000000") && s.Sum(c => c - '0') % 5 == 0;
        return somme % 10 == 0 || laPoste ? null : "Ce SIRET n'existe pas : vérifiez les chiffres.";
    }

    /// « 123 456 789 00012 » ; tel quel s'il n'a pas 14 chiffres.
    public static string SiretLisible(string siret)
    {
        string s = new string((siret ?? "").Where(char.IsDigit).ToArray());
        return s.Length == 14 ? $"{s.Substring(0, 3)} {s.Substring(3, 3)} {s.Substring(6, 3)} {s.Substring(9)}" : (siret ?? "").Trim();
    }

    /// La ligne SIRET de Réglages › Entreprise (27/10).
    public static string LigneSiret(string type, string siret) =>
          type == "particulier" ? "Particulier : pas de SIRET"
        : string.IsNullOrWhiteSpace(siret) ? "Non renseigné"
        : SiretLisible(siret);
}
