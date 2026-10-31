using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// Étape 18 (décisions des 26, 29 et 30/10) : chaque SCI soumise à TVA émet ses factures par une plateforme
/// agréée, Super PDP. 18a : le gérant relie la SCI une fois (Réglages → Intégrations → Facture électronique),
/// dans la fenêtre de Super PDP qui vérifie son identité ; l'autorisation reste sur le serveur (fonction
/// « facture-electronique »), et CIPL paie Super PDP pour tous ses clients. 18b : les loyers des locataires
/// assujettis partent aussi par la plateforme (le PDF part par e-mail, marqué « copie » ; dès le 1/9/2027,
/// plus de PDF, un e-mail de notification une fois la facture acceptée), et « Encaissée » (fr:212)
/// part tout seul quand la facture est payée. Plateforme injoignable : réessai au prochain lancement.
/// Sur le cloud seulement.
public static class FactureElectronique
{
    [Serializable] class Demande { public string action, entreprise, cle, facture, pdf, id; }
    [Serializable] public class Reponse
    {
        public bool ok, relie, configure, bac_a_sable;
        public string refus, erreur, statut, url, id;
    }

    // L'état de la SCI ouverte, retenu à chaque lecture (démarrage, Réglages) : l'émission d'un loyer en a
    // besoin tout de suite, sans attendre le serveur.
    static string _entreprise;
    static bool _active, _relie;

    static bool Courante => SessionCloud.Active && SessionCloud.Entreprise != null && _entreprise == SessionCloud.Entreprise.id;

    /// La SCI ouverte est reliée à Super PDP et son gérant vérifié.
    public static bool Active => Courante && _active;

    /// Reliée, mais la vérification du gérant n'est pas finie : rien ne part par la plateforme (Super PDP refuse).
    public static bool EnVerification => Courante && _relie && !_active;

    public static async Task<Reponse> Etat()
    {
        var r = await Appeler(new Demande { action = "etat" });
        if (r.ok && SessionCloud.Entreprise != null)
        {
            _entreprise = SessionCloud.Entreprise.id;
            _relie = r.relie;
            _active = r.relie && r.statut == "verified";
        }
        return r;
    }

    public static async Task<string> Delier()
    {
        var r = await Appeler(new Demande { action = "delier" });
        if (r.ok) _active = _relie = false;
        return r.ok ? null : Motif(r);
    }

    /// Ouvre la fenêtre de Super PDP, puis attend que le serveur ait reçu l'autorisation, 10 minutes au plus.
    /// `abandon` : Réglages fermé entre-temps (le lien se fera quand même, visible à la réouverture).
    public static async Task<(Reponse etat, string refus)> Activer(Func<bool> abandon)
    {
        var a = await Appeler(new Demande { action = "activer" });
        if (!a.ok) return (null, Motif(a));
        Application.OpenURL(a.url);
        var fin = DateTime.Now.AddMinutes(10);
        while (DateTime.Now < fin)
        {
            await Task.Delay(4000);
            if (abandon()) return (null, null);
            var e = await Etat();
            if (e.ok && e.relie) return (e, null);
        }
        return (null, "La fenêtre de Super PDP n'a pas répondu en 10 minutes : recommencez « Activer avec Super PDP ».");
    }

    // ── Étape 18b : les loyers et « Encaissée » ────────────────────────────────

    /// Une facture de loyer part-elle aussi par la plateforme ? Mode e-mail (Pennylane a son propre chemin),
    /// SCI reliée et vérifiée, locataire assujetti à la TVA et avec son SIREN (sans lui, l'annuaire ne le trouve
    /// pas : règle BR-FR-12, 30/10 ; voir SansSiret), TVA sur la facture.
    public static bool Concerne(Locataire loc, float tva) =>
        Active && ReglageService.Current?.modeEnvoi == ModeEnvoi.Email && Locataire.AssujettiTva(loc)
        && SocieteLocataire.Siren(loc.siretNumber).Length == 9 && tva > 0.005f;

    /// Locataire qui devrait recevoir une facture électronique, mais sans SIRET : sa facture part par e-mail
    /// seulement, et le formulaire le signale (décision du 30/10).
    public static bool SansSiret(Locataire loc) =>
        Active && ReglageService.Current?.modeEnvoi == ModeEnvoi.Email && Locataire.AssujettiTva(loc)
        && SocieteLocataire.Siren(loc.siretNumber).Length != 9;

    /// Le 1er septembre 2027, les PME (presque toutes les SCI) doivent émettre leurs factures par une plateforme :
    /// dès ce jour, un loyer qui part par Super PDP n'est plus envoyé en PDF, le locataire reçoit un e-mail de
    /// notification une fois la facture acceptée (Notifier). Avant : l'e-mail « copie » avec le PDF (modèle de
    /// Réglages › Modèles d'e-mail, EmailService.ObjetCopie). Décisions des 30 et 31/10.
    public static readonly DateTime Bascule = new DateTime(2027, 9, 1);

    /// Pour un essai en Play seulement, réglé par script (jamais par l'écran) : fait comme si la bascule était passée.
    public static bool EssaiApresBascule;

    public static bool NotificationSeule => EssaiApresBascule || DateTime.Today >= Bascule;

    /// À l'émission (FactureLoyerPanel) : la facture au format EN 16931 est gardée sur la ligne du suivi, elle
    /// partira dès que la ligne est « Envoyé » (tout de suite, ou le jour de l'envoi différé). La correction
    /// d'une facture déjà transmise repart aussi, comme facture rectificative (384) qui annule et remplace la
    /// précédente (décision du 31/10 : une facture transmise ne se corrige pas par un simple PDF).
    public static void Preparer(FactureEtat rec, string json)
    {
        if (rec == null) return;
        rec.plateformeDonnees = json;
        rec.plateformeId = "";
        rec.plateformeEtat = "";
        rec.plateformeErreur = "";
        rec.encaisseeDeclaree = false;
    }

    static bool Emise(FacturationSuivi.Etat e) =>
        e == FacturationSuivi.Etat.Envoye || e == FacturationSuivi.Etat.Impaye || e == FacturationSuivi.Etat.Paye;

    /// Ce que la ligne attend de la plateforme : la transmettre, ou y déclarer « Encaissée ».
    public static bool EnAttente(FactureEtat r)
    {
        if (r == null) return false;
        var e = FacturationSuivi.EtatDe(r);
        return (!string.IsNullOrEmpty(r.plateformeDonnees) && string.IsNullOrEmpty(r.plateformeId) && r.plateformeEtat != "refusee" && Emise(e))
            || (!string.IsNullOrEmpty(r.plateformeId) && !r.encaisseeDeclaree && e == FacturationSuivi.Etat.Paye);
    }

    /// La pastille de la ligne dans le suivi (null : pas concernée).
    public static string Pastille(FactureEtat r)
    {
        if (r == null) return null;
        if (r.encaisseeDeclaree) return "Encaissée déclarée";
        if (!string.IsNullOrEmpty(r.plateformeId)) return "Plateforme";
        if (r.plateformeEtat == "refusee") return "Refusée par la plateforme";
        return !string.IsNullOrEmpty(r.plateformeDonnees) && Emise(FacturationSuivi.EtatDe(r)) ? "À renvoyer" : null;
    }

    /// Après un envoi ou un paiement (fiche ouverte) : ce qui attend la plateforme pour ce locataire part
    /// maintenant ; enregistré s'il y a du changement, annoncé en bas.
    public static async void Rattraper(BatimentPrefab bp, Locataire loc)
    {
        try
        {
            if (!Active || bp == null || loc?.facturesEtat == null || !loc.facturesEtat.Any(EnAttente)) return;
            var (envoyees, encaissees, change, panne) = await Traiter(loc, bp);
            if (change && bp != null) bp.SaveAfterModifyToDoListLocataire();
            RafraichirSuivi(loc);
            var morceaux = new List<string>();
            if (envoyees > 0) morceaux.Add(envoyees == 1 ? "Facture transmise à Super PDP." : $"{envoyees} factures transmises à Super PDP.");
            if (encaissees > 0) morceaux.Add(encaissees == 1 ? "« Encaissée » déclaré à Super PDP." : $"{encaissees} « Encaissée » déclarés à Super PDP.");
            if (panne != null) morceaux.Add("Plateforme injoignable : nouvel essai au prochain lancement.");
            if (morceaux.Count > 0) UndoToast.Instance?.ShowInfo(string.Join(" ", morceaux));
        }
        catch (Exception e) { Debug.LogWarning("[FactureElectronique] " + e.Message); }
    }

    /// Au démarrage du cloud (DemarrageCloud) : l'état de la SCI, puis tout ce qui attend (envois en panne,
    /// encaissements), sur les fiches que personne ne modifie.
    public static async void Demarrer(EntrepriseCloud e)
    {
        try
        {
            // L'état d'abord, tout de suite : l'écran Facturer en a besoin dès qu'on l'ouvre.
            if (SessionCloud.Entreprise != e || HorsLigne.Actif) return;
            await Etat();
            await Task.Delay(35000);   // le rattrapage après la relecture des verrous et la tournée de l'annuaire
            if (SessionCloud.Entreprise != e || HorsLigne.Actif || AbonnementCloud.LectureSeule) return;
            var bm = BatimentManager.Instance;
            if (!Active || bm == null) return;
            var libre = await EcritureCloud.LocatairesLibres();
            if (libre == null || SessionCloud.Entreprise != e) return;
            int envoyees = 0, encaissees = 0;
            string panne = null;
            foreach (var bp in bm.BatimentPrefab.ToList())
            {
                foreach (var loc in (bp?.listLocataire ?? new List<Locataire>()).ToList())
                {
                    if (loc?.facturesEtat == null || !libre(loc.id) || !loc.facturesEtat.Any(EnAttente)) continue;
                    var (a, b, change, p) = await Traiter(loc, bp);
                    if (change && bp != null) bp.SaveAfterModifyToDoListLocataire();
                    envoyees += a; encaissees += b;
                    if ((panne = p) != null) break;
                }
                if (panne != null) { Debug.LogWarning("[FactureElectronique] " + panne); break; }
            }
            if (envoyees + encaissees > 0)
                UndoToast.Instance?.ShowInfo($"Super PDP : {envoyees} facture(s) transmise(s), {encaissees} « Encaissée » déclaré(s).");
        }
        catch (Exception ex) { Debug.LogWarning("[FactureElectronique] Démarrage : " + ex.Message); }
    }

    /// Les lignes d'un locataire qui attendent la plateforme, une par une. Plateforme injoignable (`panne`) :
    /// on s'arrête, la ligne garde son état et repartira au prochain lancement.
    static async Task<(int envoyees, int encaissees, bool change, string panne)> Traiter(Locataire loc, BatimentPrefab bp)
    {
        int envoyees = 0, encaissees = 0;
        bool change = false;
        foreach (var r in loc.facturesEtat.ToList())
        {
            if (!EnAttente(r)) continue;
            if (string.IsNullOrEmpty(r.plateformeId))
            {
                byte[] pdf = null;
                try { pdf = File.ReadAllBytes(FacturationSuivi.CheminPdf(r, bp.getName(), loc.Name)); } catch (Exception) { /* plus bas */ }
                if (pdf == null) { r.plateformeEtat = "refusee"; r.plateformeErreur = "PDF introuvable sur ce PC."; change = true; continue; }
                var rep = await AppelerLong(new Demande { action = "envoyer", cle = Cle(loc, r), facture = r.plateformeDonnees, pdf = Convert.ToBase64String(pdf) });
                if (rep.ok)
                {
                    r.plateformeId = rep.id; r.plateformeEtat = "envoyee"; r.plateformeErreur = ""; envoyees++; change = true;
                    if (NotificationSeule) await Notifier(loc, r, bp.getBatiment());
                }
                else if (rep.refus == "refusee")
                {
                    r.plateformeEtat = "refusee"; r.plateformeErreur = rep.erreur; change = true;
                    Debug.LogWarning($"[FactureElectronique] {loc.Name} · {r.libelle} : refusée par Super PDP : {rep.erreur}");
                    ConfirmDialog.Erreur($"Super PDP a refusé la facture « {r.libelle} » de {loc.Name}"
                                         + (NotificationSeule ? " : elle n'est pas partie, le locataire n'a rien reçu." : " (le PDF est bien parti par e-mail).")
                                         + "\nDétail technique : " + rep.erreur);
                }
                else
                {
                    if (r.plateformeEtat != "a_renvoyer") { r.plateformeEtat = "a_renvoyer"; change = true; }
                    return (envoyees, encaissees, change, Motif(rep));
                }
            }
            if (!string.IsNullOrEmpty(r.plateformeId) && !r.encaisseeDeclaree && FacturationSuivi.EtatDe(r) == FacturationSuivi.Etat.Paye)
            {
                var rep = await Appeler(new Demande { action = "encaisser", id = r.plateformeId });
                if (!rep.ok) return (envoyees, encaissees, change, Motif(rep));
                r.encaisseeDeclaree = true; encaissees++; change = true;
            }
        }
        return (envoyees, encaissees, change, null);
    }

    /// Dès la bascule : le locataire apprend par e-mail que sa facture est transmise (modèle de Réglages › Textes
    /// fixes), sans PDF. Un échec est dit, sans réessai : la facture, elle, est bien partie.
    static async Task Notifier(Locataire loc, FactureEtat r, Batiment bat)
    {
        string dest = FactureEnvoiAuto.Destinataire(loc, r.type);
        string echec = string.IsNullOrWhiteSpace(dest) ? "le locataire n'a pas d'adresse e-mail." : null;
        if (echec == null)
        {
            var ctx = FactureEnvoiAuto.Contexte(loc, r);
            var info = loc.FactureInfoDe(r.type);   // le texte propre à cette facture, sinon le modèle des Réglages
            var envoi = EmailService.Envoyer(dest,
                FactureVarResolver.Resolve(EmailService.ObjetNotif(info?.notifObjet), loc, bat, ctx),
                FactureVarResolver.Resolve(EmailService.CorpsNotif(info?.notifCorps), loc, bat, ctx));
            while (!envoi.Termine) await Task.Delay(100);
            if (!envoi.Succes) echec = envoi.Erreur;
        }
        if (echec != null)
            ConfirmDialog.Erreur($"La facture « {r.libelle} » de {loc.Name} est bien transmise par la plateforme, "
                                 + "mais l'e-mail qui prévient le locataire n'est pas parti : " + echec);
    }

    /// Identifiant de la facture chez Super PDP (36 caractères au plus) : un même envoi refait n'en crée pas deux.
    public static string Cle(Locataire loc, FactureEtat r)
    {
        string id = (loc?.id ?? "").Replace("-", "");
        string c = $"{(id.Length > 8 ? id.Substring(0, 8) : id)}-{(string.IsNullOrEmpty(r?.numero) ? r?.key : r.numero)}";
        return c.Length > 36 ? c.Substring(0, 36) : c;
    }

    static void RafraichirSuivi(Locataire loc)
    {
        foreach (var fiche in UnityEngine.Object.FindObjectsOfType<LocatairePrefab>())
            if (fiche != null && fiche.GetLocataire() == loc) LocataireSuiviInline.RefreshFor(fiche);
    }

    // ── Le serveur ─────────────────────────────────────────────────────────────

    static async Task<Reponse> Appeler(Demande d)
    {
        if (!SessionCloud.Active || SessionCloud.Entreprise == null) return new Reponse { refus = "cloud" };
        d.entreprise = SessionCloud.Entreprise.id;
        var r = await CloudClient.FonctionServeur(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "facture-electronique",
                    JsonUtility.ToJson(d));
        return Lire(r.Ok, r.Corps, r.Erreur);
    }

    /// L'envoi d'une facture : deux appels à Super PDP (Factur-X, puis envoi) et un PDF, plus long que 15 s parfois.
    static async Task<Reponse> AppelerLong(Demande d)
    {
        if (!SessionCloud.Active || SessionCloud.Entreprise == null) return new Reponse { refus = "cloud" };
        d.entreprise = SessionCloud.Entreprise.id;
        var (r, octets) = await CloudClient.FonctionBrute(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "facture-electronique",
                              JsonUtility.ToJson(d));
        return Lire(r.Ok, r.Ok ? (octets == null ? "" : Encoding.UTF8.GetString(octets)) : r.Corps, r.Erreur);
    }

    static Reponse Lire(bool ok, string corps, string erreur)
    {
        Reponse rep = null;
        try { rep = JsonUtility.FromJson<Reponse>(corps); } catch (Exception) { /* pas du JSON : `erreur` dit pourquoi */ }
        if (rep != null && (rep.ok || !string.IsNullOrEmpty(rep.refus))) return rep;
        return new Reponse { refus = "serveur", erreur = ok ? "réponse inattendue" : erreur };
    }

    /// La ligne d'état de la carte ; null quand la SCI n'est pas reliée.
    public static string Statut(Reponse e) =>
          e == null || !e.relie ? null
        : e.statut == "verified" ? "Active : les factures de loyer aux locataires assujettis partent aussi par Super PDP."
        : e.statut == "needs_review" ? "En vérification manuelle chez Super PDP (nom différent au registre, ou gérant qui est une société) : leur support s'en occupe."
        : e.statut == "failed" ? "Vérification d'identité refusée par Super PDP : « Se déconnecter », puis recommencez « Activer avec Super PDP »."
        : "En attente de la vérification d'identité du gérant chez Super PDP (sur son téléphone) ; « Actualiser » ensuite.";

    /// Null si la fonction a répondu oui, sinon le motif pour l'écran.
    public static string Motif(Reponse r)
    {
        string detail = string.IsNullOrEmpty(r?.erreur) ? "" : "\nDétail technique : " + r.erreur;
        switch (r?.refus)
        {
            case null: case "": return r == null ? "Pas de réponse du serveur de CIPL." : null;
            case "cloud": return "La facture électronique demande d'être connecté(e) au cloud de CIPL.";
            case "acces": return "Cette entreprise ne vous est pas ouverte.";
            case "admin": return "Seul un administrateur peut activer ou couper la facture électronique.";
            case "config": return "La facture électronique n'est pas encore réglée sur le serveur (Supabase › Edge Functions › Secrets : SUPERPDP_CLIENT_ID et SUPERPDP_CLIENT_SECRET).";
            case "siren": return "Cette entreprise n'a pas de SIRET : renseignez-le d'abord (Réglages → Entreprise).";
            case "retire": return "L'autorisation de Super PDP a expiré ou a été retirée : « Se déconnecter », puis « Activer avec Super PDP » de nouveau.";
            case "jeton": return "Super PDP refuse de renouveler l'autorisation." + detail;
            case "non_relie": return "Cette SCI n'est pas reliée à Super PDP (Réglages → Intégrations → Facture électronique).";
            case "refusee": return "Super PDP a refusé la facture." + detail;
            case "indisponible": return "Super PDP est injoignable pour le moment." + detail;
            default: return "La fonction « facture-electronique » du serveur n'a pas pu répondre." + (detail == "" ? "\nDétail technique : " + r.refus : detail);
        }
    }
}
