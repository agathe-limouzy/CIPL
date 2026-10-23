using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// Étape 9b : les utilisateurs de l'entreprise (décisions du 11/10 et du 19/10). L'admin invite
/// par e-mail avec un code (parti du SMTP de l'entreprise), change le rôle et les entreprises
/// accessibles, retire quelqu'un. Une licence compte chaque membre et chaque invitation en cours.
public static class UtilisateursCloud
{
    /// Licences par entreprise (décision du 23/10) : celles qu'elle a, celles qu'elle utilise
    /// (personnes qui y ont accès + invitations en cours qui la comprennent).
    [Serializable] public class Entreprise { public string id, nom; public int licences_max, licences_utilisees; }
    [Serializable] public class Membre { public string user_id, nom, email, role; public string[] entreprises; public bool en_ligne, moi; }
    [Serializable] public class Invitation { public string id, email, role, expire_le; public string[] entreprises; }
    [Serializable] public class Annuaire { public int licences_max, licences_utilisees; public Entreprise[] entreprises; public Membre[] membres; public Invitation[] invitations; }
    [Serializable] public class Resultat { public bool ok; public string refus, id, nom, erreur; public int utilisees, max; }

    // ── Sans réseau (testé en EditMode) ────────────────────────────────────────

    /// Sans 0, O, 1 ni I : un code qui se lit au téléphone sans erreur.
    const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// « K7F2-9QXD » : 8 caractères tirés au hasard (32⁸ possibilités).
    public static string NouveauCode()
    {
        var octets = new byte[8];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(octets);
        var sb = new StringBuilder();
        for (int i = 0; i < 8; i++)
        {
            if (i == 4) sb.Append('-');
            sb.Append(Alphabet[octets[i] % Alphabet.Length]);
        }
        return sb.ToString();
    }

    /// Comme le serveur : majuscules, sans tiret ni espace.
    public static string Normaliser(string code) =>
        new string((code ?? "").ToUpperInvariant().Where(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')).ToArray());

    /// Ce que garde le serveur (jamais le code lui-même).
    public static string Empreinte(string code)
    {
        using (var sha = SHA256.Create())
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(Normaliser(code))).Select(o => o.ToString("x2")));
    }

    public static string Licences(int utilisees, int max) => $"{utilisees} / {max} licence{(max > 1 ? "s" : "")}";

    /// « Démo CIPL · 2 / 3 licences », « SCI Rivoli · 1 / 1 licence · pleine ».
    public static string PastilleLicences(Entreprise e) =>
        $"{e.nom} · {Licences(e.licences_utilisees, e.licences_max)}" + (Libres(e) <= 0 ? " · pleine" : "");

    public static int Libres(Entreprise e) => e.licences_max - e.licences_utilisees;

    /// Case d'une entreprise dans « Inviter » ou « Entreprises de … » : ses licences libres.
    public static string CaseEntreprise(Entreprise e, bool dejaDedans) =>
        dejaDedans ? $"{e.nom} · sa licence"
      : Libres(e) <= 0 ? $"{e.nom} · aucune licence libre"
      : $"{e.nom} · {Libres(e)} licence{(Libres(e) > 1 ? "s" : "")} libre{(Libres(e) > 1 ? "s" : "")}";

    /// « J'ai reçu une invitation » : null si le formulaire est complet, sinon ce qu'il manque.
    public static string VerifierRejoindre(string email, string code, string mdp, string confirmation)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@")) return "Saisissez l'adresse e-mail qui a reçu l'invitation.";
        if (Normaliser(code).Length != 8) return "Le code d'invitation a 8 caractères (par exemple K7F2-9QXD).";
        if ((mdp ?? "").Length < 8) return "Le mot de passe doit faire au moins 8 caractères.";
        if (mdp != confirmation) return "Les deux mots de passe ne sont pas identiques.";
        return null;
    }

    /// Le refus du serveur, en clair.
    public static string Motif(Resultat r)
    {
        switch (r?.refus)
        {
            case "licences": return string.IsNullOrEmpty(r.nom)
                ? $"{r.utilisees} licences sur {r.max} utilisées. Retirez quelqu'un ou augmentez l'abonnement."
                : $"{r.nom} : {r.utilisees} licence{(r.utilisees > 1 ? "s" : "")} sur {r.max} utilisée{(r.utilisees > 1 ? "s" : "")}. " +
                  $"Ajoutez une licence à {r.nom} (Réglages › Abonnement) ou retirez-y quelqu'un.";
            case "deja_membre": return "Cette personne fait déjà partie de l'entreprise.";
            case "email": return "Cette adresse e-mail n'est pas valide.";
            case "entreprises": return "Choisissez au moins une entreprise.";
            case "dernier_admin": return "Il faut au moins un administrateur.";
            case "soi_meme": return "Vous ne pouvez pas changer votre propre accès : demandez à un autre administrateur.";
            case "code": return "Ce code ne correspond à aucune invitation pour cette adresse e-mail.";
            case "expire": return "Cette invitation a expiré : demandez-en une nouvelle à l'administrateur.";
            case null: return "Réponse du serveur illisible.";
            default: return string.IsNullOrEmpty(r.erreur) ? "Refusé : " + r.refus : r.erreur;
        }
    }

    public static string ObjetEmail(string entreprise) => $"Invitation à rejoindre {entreprise} sur CIPL";

    public static string CorpsEmail(string qui, string entreprise, string code, DateTime expire) =>
        "Bonjour,\n\n" +
        $"{qui} vous invite à rejoindre {entreprise} sur CIPL, l'application de gestion locative.\n\n" +
        $"Votre code d'invitation : {code}\n" +
        $"Il est valable jusqu'au {expire:dd'/'MM'/'yyyy}.\n\n" +
        "Pour rejoindre l'entreprise :\n" +
        $"1. Installez CIPL (demandez le lien d'installation à {qui}).\n" +
        "2. À l'écran de connexion, choisissez « J'ai reçu une invitation ».\n" +
        "3. Saisissez cette adresse e-mail et le code ci-dessus, puis choisissez votre mot de passe.\n\n" +
        "À bientôt sur CIPL.";

    // ── Serveur ────────────────────────────────────────────────────────────────

    // Une classe par signature SQL, au champ près : JsonUtility envoie un texte nul comme "",
    // et un paramètre en trop rend la fonction introuvable (404 ; Retirer, 19/10).
    [Serializable] class PEntreprise { public string p_entreprise; }
    [Serializable] class PInviter { public string p_entreprise, p_email, p_role, p_code_empreinte; public string[] p_entreprises; }
    [Serializable] class PInvitation { public string p_entreprise, p_invitation; }
    [Serializable] class PRenvoi { public string p_entreprise, p_invitation, p_code_empreinte; }
    [Serializable] class PMembre { public string p_entreprise, p_user; }
    [Serializable] class PRole { public string p_entreprise, p_user, p_role; }
    [Serializable] class PAcces { public string p_entreprise, p_user; public string[] p_entreprises; }
    [Serializable] class PCode { public string p_code; }

    static string Ent => SessionCloud.Entreprise?.id;

    public static async Task<(Annuaire annuaire, string erreur)> LireAnnuaire()
    {
        var r = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "annuaire",
                                           new PEntreprise { p_entreprise = Ent });
        if (!r.Ok) return (null, r.Erreur);
        try { return (JsonUtility.FromJson<Annuaire>(r.Corps), null); }
        catch (Exception e) { return (null, "Réponse illisible : " + e.Message); }
    }

    /// Les tests le remplacent pour lire ce qui part au serveur, sans réseau.
    public static Func<string, object, Task<Resultat>> Appeler = AppelerServeur;

    static async Task<Resultat> AppelerServeur(string fonction, object parametres)
    {
        try
        {
            var r = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), fonction, parametres);
            if (!r.Ok) return new Resultat { refus = "erreur", erreur = r.Erreur };
            return JsonUtility.FromJson<Resultat>(r.Corps) ?? new Resultat { refus = "erreur", erreur = "Réponse illisible." };
        }
        catch (Exception e) { return new Resultat { refus = "erreur", erreur = e.Message }; }
    }

    public static Task<Resultat> Inviter(string email, string role, string[] entreprises, string code) =>
        Appeler("inviter", new PInviter { p_entreprise = Ent, p_email = email, p_role = role, p_entreprises = entreprises, p_code_empreinte = Empreinte(code) });

    public static Task<Resultat> AnnulerInvitation(string id) =>
        Appeler("annuler_invitation", new PInvitation { p_entreprise = Ent, p_invitation = id });

    public static Task<Resultat> RenvoyerInvitation(string id, string code) =>
        Appeler("renvoyer_invitation", new PRenvoi { p_entreprise = Ent, p_invitation = id, p_code_empreinte = Empreinte(code) });

    public static Task<Resultat> ChangerRole(string user, string role) =>
        Appeler("changer_role", new PRole { p_entreprise = Ent, p_user = user, p_role = role });

    public static Task<Resultat> ChangerAcces(string user, string[] entreprises) =>
        Appeler("changer_acces", new PAcces { p_entreprise = Ent, p_user = user, p_entreprises = entreprises });

    public static Task<Resultat> Retirer(string user) =>
        Appeler("retirer_membre", new PMembre { p_entreprise = Ent, p_user = user });

    public static Task<Resultat> Accepter(string code) => Appeler("accepter_invitation", new PCode { p_code = code });

    /// L'e-mail d'invitation part du SMTP de l'entreprise. Null s'il est parti, sinon le motif.
    public static async Task<string> EnvoyerEmail(string email, string entreprise, string code)
    {
        string manque = EmailService.CeQuiManque();
        if (manque != null) return manque;
        string qui = NomAdmin() ?? RegleVerrou.Prenom(SessionCloud.Session?.user?.email ?? "");
        var envoi = EmailService.Envoyer(email, ObjetEmail(entreprise), CorpsEmail(qui, entreprise, code, DateTime.Today.AddDays(7)));
        while (!envoi.Termine) await Task.Yield();
        return envoi.Succes ? null : envoi.Erreur;
    }

    /// Nom de l'admin pour l'e-mail : son nom d'expédition s'il est réglé.
    static string NomAdmin()
    {
        string n = ReglageService.Current?.smtp?.fromName;
        return string.IsNullOrWhiteSpace(n) ? null : n.Trim();
    }
}
