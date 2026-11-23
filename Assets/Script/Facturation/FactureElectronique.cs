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
    [Serializable] class Demande { public string action, entreprise, cle, facture, pdf, id, montant, date; }
    [Serializable] public class Reponse
    {
        public bool ok, relie, configure, bac_a_sable;
        public bool indisponible;   // etat : Super PDP injoignable, dernier état connu (revue 16c, D1-06)
        public string refus, erreur, statut, url, id;
    }

    /// L'état n'a pas pu être lu au lancement, même au second essai (serveur de CIPL injoignable, jeton) : les loyers
    /// émis d'ici le prochain lancement partent par e-mail seul, et l'écran Facturer le dit (revue 16c, D5-04).
    /// Revue 17 (D5-05, lot 12) : après la bascule, ils ne partent plus du tout (RefusObligation, ARefaire) : le toast le dit.
    public static bool EtatInconnu { get; private set; }

    // L'état de la SCI ouverte, retenu à chaque lecture (démarrage, Réglages) : l'émission d'un loyer en a
    // besoin tout de suite, sans attendre le serveur.
    static string _entreprise;
    static bool _active, _relie;

    static bool Courante => SessionCloud.Active && SessionCloud.Entreprise != null && _entreprise == SessionCloud.Entreprise.id;

    /// La SCI ouverte est reliée à Super PDP et son gérant vérifié.
    public static bool Active => Courante && _active;

    /// Reliée, mais la vérification du gérant n'est pas finie : rien ne part par la plateforme (Super PDP refuse).
    public static bool EnVerification => Courante && _relie && !_active;

    /// Revue 17 (D4-16, lot 11) : l'état est lu et la SCI n'est PAS reliée (jamais, ou déliée dans Réglages) : rien ne partira par la
    /// plateforme tant qu'elle n'est pas reliée de nouveau. Faux tant que l'état n'est pas lu (lancement, hors ligne) : l'envoi groupé
    /// ne doit pas en déduire « e-mail de facture » alors que la transmission suivra (l'envoi est proposé avant Demarrer).
    public static bool Deliee => Courante && !_relie;

    public static async Task<Reponse> Etat()
    {
        var r = await Appeler(new Demande { action = "etat" });
        if (r.ok && SessionCloud.Entreprise != null)
        {
            _entreprise = SessionCloud.Entreprise.id;
            _relie = r.relie;
            _active = r.relie && r.statut == "verified";
            EtatInconnu = false;
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

    /// Une facture part-elle aussi par la plateforme ? (mode Pennylane retiré le 02/11) : SCI reliée et vérifiée, locataire avec
    /// son SIREN (02/11 : la case « Assujetti à la TVA » de la fiche est retirée ; une association non assujettie garde son SIRET
    /// vide ; sans SIREN, l'annuaire ne le trouve pas : règle BR-FR-12, 30/10 ; voir SansSiret), et de la TVA sur le DOCUMENT
    /// (`tva` : son montant de TVA, ou son taux ; BI8 (02/11) : un avoir a une TVA négative).
    /// Revue 17 (D5-08, lot 10) : c'est le taux du document qui compte, plus le bail du jour (Tva.Soumis) : la correction d'une
    /// facture transmise d'un bail devenu exonéré garde son taux et repart par la plateforme comme l'originale ; une facture
    /// sans TVA (bail exonéré, ou 0 tapé) n'y va pas.
    public static bool Concerne(Locataire loc, float tva, string siretFormulaire = null) =>
        Active && Math.Abs(tva) > 0.005f && SocieteLocataire.Siren(SiretAcheteur(loc, siretFormulaire)).Length == 9;

    /// Facture avec TVA qui devrait recevoir une facture électronique, mais sans SIRET : elle part par e-mail seulement, et le
    /// formulaire le signale (décision du 30/10). Revue 17 (D5-08, lot 10) : au taux du document, comme Concerne.
    public static bool SansSiret(Locataire loc, float tva, string siretFormulaire = null) =>
        Active && Math.Abs(tva) > 0.005f && SocieteLocataire.Siren(SiretAcheteur(loc, siretFormulaire)).Length != 9;

    /// Revue 17 (D5-05, lot 11, décision du 05/11) : depuis le 1er septembre 2027, une facture avec TVA (taux du document, lot 10) à un
    /// locataire qui a un SIREN DOIT partir par une plateforme agréée : la règle de Concerne sans l'état de la SCI. `depart` : le jour où elle
    /// part (loyer différé : sa date d'envoi ; un loyer préparé le 25/08/2027 pour le 16/09 est déjà concerné). L'essai en Play compte aussi.
    public static bool DoitPartirParPlateforme(float tva, string siretAcheteur, DateTime depart) =>
        (EssaiApresBascule || depart >= Bascule) && Math.Abs(tva) > 0.005f && SocieteLocataire.Siren(siretAcheteur).Length == 9;

    /// Revue 17 (D5-05 / D5-07, lot 11) : pourquoi la facture ne part pas par la plateforme, la même phrase pour la ligne « Envoi » et les
    /// refus au centre (avant : toutes les causes possibles d'un coup) ; null quand elle y part (Concerne).
    public static string RaisonHorsPlateforme(Locataire loc, float tva, string siretFormulaire) =>
        Concerne(loc, tva, siretFormulaire) ? null
        : Math.Abs(tva) <= 0.005f ? "la facture est sans TVA"
        : SocieteLocataire.Siren(SiretAcheteur(loc, siretFormulaire)).Length != 9 ? "le locataire n'a pas de SIRET (fiche ou formulaire)"
        : RaisonInactive;

    /// Pourquoi Super PDP n'est pas actif pour la SCI ouverte : la même phrase pour les formulaires (RaisonHorsPlateforme) et pour l'envoi
    /// au lancement (RefusLancement, décision du 10/11).
    public static string RaisonInactive =>
        EnVerification ? "Super PDP : vérification du gérant en cours, voir Réglages → Intégrations"
        : EtatInconnu ? "Super PDP : état non lu au lancement ; relancez CIPL"
        : Deliee ? "la SCI n'est pas reliée à Super PDP (Réglages → Intégrations → Facture électronique)"
        : "l'état de Super PDP n'est pas encore lu (démarrage, hors ligne) ; réessayez dans un instant, ou relancez CIPL";

    /// Revue 17 (D5-05, lot 11, décision du 05/11) : le refus, avant tout envoi, d'une facture qui doit partir par la plateforme
    /// (DoitPartirParPlateforme) alors qu'elle n'y part pas (`parPlateforme` faux : Concerne) : rien n'est émis ni envoyé, avec la raison.
    /// Avant, elle partait par e-mail seul (facture non conforme). Null : rien à refuser ici.
    public static string RefusObligation(Locataire loc, float tva, string siretFormulaire, string sujet, bool parPlateforme, DateTime depart) =>
        parPlateforme || !DoitPartirParPlateforme(tva, SiretAcheteur(loc, siretFormulaire), depart) ? null
        : $"« {sujet} » doit partir par la plateforme agréée (depuis le 1er septembre 2027, toute facture avec TVA à un locataire qui a un SIRET), "
          + $"et elle ne le peut pas en ce moment : {RaisonHorsPlateforme(loc, tva, siretFormulaire)}. Rien n'a été émis.";

    /// L'acheteur de la facture électronique est celui du PDF : le SIRET saisi dans le formulaire quand il y en a un
    /// (facture adressée à une holding…), sinon celui de la fiche (revue 16c, D5-08 ; décision du 01/11).
    public static string SiretAcheteur(Locataire loc, string siretFormulaire) =>
        string.IsNullOrWhiteSpace(siretFormulaire) ? (loc?.siretNumber ?? "") : siretFormulaire.Trim();

    /// Revue 17 (D5-05, lot 12) : la ligne a sa facture électronique et elle partira par la plateforme : JSON présent, ni périmée (refaite hors
    /// plateforme : ses anciens montants ne partent plus, 16c D5-01) ni refusée. Avant, l'envoi au lancement prenait « JSON présent » pour « par la
    /// plateforme » : une ligne périmée partait avec l'e-mail « copie » (sans rien après la bascule) et passait « Envoyé ».
    public static bool PartParPlateforme(FactureEtat r) =>
        r != null && !string.IsNullOrEmpty(r.plateformeDonnees) && r.plateformeEtat != "perimee" && r.plateformeEtat != "refusee";

    /// Revue 17 (D5-05, lot 12, décision du 05/11) : préparée SANS facture électronique (Super PDP pas actif à l'émission : état non lu, vérification
    /// en cours, SCI non reliée ; ou refaite hors plateforme) alors qu'au jour de son départ (`jour`) elle doit partir par la plateforme
    /// (DoitPartirParPlateforme) : elle ne part pas par e-mail seul ; elle est à refaire (« Refaire ») quand Super PDP est actif. La TVA est celle du
    /// document (montant au taux de la ligne : un total nul n'a pas de TVA, il n'est pas retenu puis refusé au « Refaire ») et le SIRET celui de
    /// l'acheteur de la ligne (FactureEnvoiAuto.SiretLigne), pas le bail ni la fiche du jour. Le décompte de sortie ne passe jamais par la plateforme (D5-18, à part).
    /// Une facture refusée par Super PDP n'est pas « à refaire » : elle a sa facture électronique, elle est à corriger depuis le suivi (16c D5-05,
    /// pastille « Refusée par la plateforme » ; DoitPartir l'écarte déjà) : exclue ici, sinon le lancement la nommait « préparée sans facture
    /// électronique » et le diagnostic perdait sa raison « refusée ».
    public static bool ARefaire(Locataire loc, FactureEtat r, DateTime jour) =>
        loc != null && r != null && r.type != "Depot" && r.plateformeEtat != "refusee" && !PartParPlateforme(r)
        && DoitPartirParPlateforme(FactureEnvoiAuto.Contexte(loc, r).tva, FactureEnvoiAuto.SiretLigne(loc, r), jour);

    /// L'état de la SCI ouverte a été lu (démarrage, Réglages) ; sinon l'envoi au lancement le relit avant de retenir une facture (EnvoyerUne).
    public static bool EtatLu => Courante;

    /// Décision du 10/11 (« Attendre Super PDP » ; D5-05, D5-26) : après la bascule, une facture qui a sa facture électronique
    /// (PartParPlateforme) ne part plus jamais en PDF par e-mail. Tant que Super PDP n'est pas actif (état pas lu, vérification du gérant en
    /// cours, SCI non reliée), l'envoi au lancement la laisse « en attente d'envoi » et le bilan dit pourquoi (ce texte) : la règle du refus
    /// des formulaires (RefusObligation). Avant : l'e-mail « copie » partait avec le PDF, puis la plateforme et sa notification (deux e-mails
    /// chez le locataire) ; une SCI connue comme déliée envoyait l'e-mail de facture seul (non conforme). Null : elle peut partir. Avant la
    /// bascule, rien ne change.
    public static string RefusLancement(Locataire loc, FactureEtat r) =>
        !NotificationSeule || !PartParPlateforme(r) || Active ? null
        : $"{loc?.Name} / {r.libelle} : depuis le 1er septembre 2027, cette facture doit partir par la plateforme, et elle ne le peut pas en ce moment : {RaisonInactive}.";

    /// Les 14 chiffres d'un SIRET (espaces et points enlevés), ou null s'il n'en a pas 14 (D5-13).
    public static string Siret14(string siret)
    {
        string chiffres = new string((siret ?? "").Where(char.IsDigit).ToArray());
        return chiffres.Length == 14 ? chiffres : null;
    }

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
        // `json` null : refaite hors plateforme (locataire plus concerné, état de Super PDP non lu au lancement…). Une
        // version jamais transmise ne doit plus partir, avec ses anciens montants (revue 16c, D5-01) : elle est
        // « périmée », gardée pour sa référence éventuelle à la facture d'origine (Remplacee). Une version transmise
        // ne bouge pas (FactureLoyerPanel prévient que la correction n'est pas allée à la plateforme).
        if (json == null)
        {
            if (string.IsNullOrEmpty(rec.plateformeId) && !string.IsNullOrEmpty(rec.plateformeDonnees))
            {
                rec.plateformeEtat = "perimee";
                rec.plateformeErreur = "";
            }
            return;
        }
        // Revue 17 (D5-06, lot 9, décision du 05/11) : la version remplacée est déjà déclarée « Encaissée » : son identifiant est gardé pour
        // annuler cet encaissement AVANT de transmettre la rectificative (Traiter), du même montant et à la même date ; la rectificative
        // recevra le sien une fois payée. Avant, l'identifiant et le drapeau étaient effacés : rien n'annulait, l'encaissement était compté
        // deux fois chez Super PDP. Montant inconnu (déclaré avant le 01/11, fiche relue avant le lot 7) : le total TTC de l'ancien JSON ;
        // date inconnue : aujourd'hui. Une annulation déjà en attente (facture repassée « Envoyé », D5-12) part de même, à sa date.
        // ponytail: une seconde correction, hors plateforme, pendant que l'annulation attend : l'annulation part, la rectificative périmée ne part
        // pas (ATransmettre), l'originale reste sans encaissement (limite connue) ; et une correction pendant un « Encaissée » en vol ne pose
        // pas l'annulation (voir Traiter).
        if (rec.encaisseeDeclaree && !string.IsNullOrEmpty(rec.plateformeId))
        {
            if (rec.encaisseeMontant <= 0.005f) rec.encaisseeMontant = FactureEn16931.TotalDe(rec.plateformeDonnees);
            if (string.IsNullOrEmpty(rec.encaisseeDateISO)) rec.encaisseeDateISO = DateTime.Today.ToString("yyyy-MM-dd");
            if (rec.encaisseeMontant > 0.005f) rec.encaisseeAAnnulerId = rec.plateformeId;
            else Debug.LogWarning($"[FactureElectronique] « {rec.libelle} » : montant de l'encaissement déclaré inconnu, rien à annuler.");
        }
        rec.plateformeDonnees = json;
        rec.plateformeId = "";
        rec.plateformeEtat = "";
        rec.plateformeErreur = "";
        rec.encaisseeDeclaree = false;
    }

    /// Régularisation et refacturation (décision du 02/11, BI5) : même départ que le loyer. `avant` = la ligne d'avant
    /// l'enregistrement (une correction transmise cite la facture remplacée) ; `e` porte l'acheteur et les lignes du PDF,
    /// le vendeur est posé ici. Hors plateforme : Preparer(ligne, null), comme le loyer.
    public static void Preparer(FactureEtat ligne, FactureEtat avant, bool parPlateforme, FactureEn16931.Entree e)
    {
        if (ligne == null) return;
        if (!parPlateforme) { Preparer(ligne, null); return; }
        e.vendeurNom = ReglageService.Current?.entrepriseNom;
        e.vendeurSiren = SocieteLocataire.Siren(SessionCloud.Entreprise?.siret);
        (e.precedentNumero, e.precedentDateISO) = FactureEn16931.Remplacee(avant);
        Preparer(ligne, FactureEn16931.Json(e));
    }

    /// Un avoir électronique cite les factures qu'il corrige (règle BR-FR-CO-05, refus du bac à sable le 02/11) : la version
    /// précédente de la même ligne quand il la corrige ; sinon la facture d'origine choisie d'un avoir sur refacturation
    /// (`origine` : son numéro) et les loyers de l'année (`annee`), dont les provisions couvrent les charges : régularisation,
    /// refacturation d'une charge négative ou loyer à provision négative qui remboursent. Une ligne transmise est citée telle
    /// que la plateforme la connaît (Remplacee), les autres par leur numéro et leur date d'envoi. Vide = rien à citer : le
    /// formulaire refuse l'avoir avant tout envoi.
    /// Revue 17 (D5-14, coutures) : seulement une facture PARTIE chez le locataire (envoyée, impayée ou payée) et que Super PDP n'a pas
    /// refusée. Avant : toute ligne qui avait un numéro, même « en attente d'envoi » (le locataire ne l'a pas reçue), bloquée « À faire »
    /// ou refusée par la plateforme. Une version précédente jamais partie n'est pas citée non plus (l'avoir se citait lui-même) : l'origine
    /// choisie et les loyers de l'année prennent le relais. Une ligne refaite hors plateforme (« perimee ») et partie par e-mail reste
    /// citée, par la référence que la plateforme connaît (Remplacee) ou par son numéro.
    public static List<KeyValuePair<string, string>> ReferencesAvoir(Locataire loc, FactureEtat avant, int annee = 0, string origine = null)
    {
        var res = new List<KeyValuePair<string, string>>();
        void Citer(FactureEtat r)
        {
            if (r == null || string.IsNullOrEmpty(r.numero) || r == avant && res.Count > 0) return;
            // D5-14 : jamais partie, ou refusée. Un loyer parti puis retiré du suivi (départ, D4-02 : `retireApresEnvoi`) reste citable :
            // c'est l'avoir qui le cite qui l'annule.
            if (!(Emise(FacturationSuivi.EtatDe(r, null)) || r.retireApresEnvoi) || r.plateformeEtat == "refusee") return;
            var (n, d) = FactureEn16931.Remplacee(r);
            if (res.Any(x => x.Key == (n ?? FactureEn16931.Numero(r.numero)))) return;
            res.Add(new KeyValuePair<string, string>(n ?? FactureEn16931.Numero(r.numero), d ?? r.dateEnvoiISO));
        }
        if (!string.IsNullOrEmpty(avant?.numero)) { Citer(avant); if (res.Count > 0) return res; }
        if (!string.IsNullOrEmpty(origine)) Citer(loc?.facturesEtat?.FirstOrDefault(x => x != null && x.numero == origine));
        if (annee > 0 && loc?.facturesEtat != null)
            foreach (var r in loc.facturesEtat.Where(x => x != null && x.type == "Loyer" && x != avant && (x.key ?? "").StartsWith($"loyer-{annee}-")))
                Citer(r);
        return res;
    }

    /// Revue 17 (D5-15, coutures) : les numéros des factures citées par un avoir, pour son PDF (« Avoir sur la facture n° … ») : ceux de son
    /// XML (ReferencesAvoir), dans le même ordre. Avant : le XML d'un avoir de loyer ou de régularisation citait, son PDF ne nommait rien.
    public static string NumerosCites(List<KeyValuePair<string, string>> references) =>
        string.Join(", ", (references ?? new List<KeyValuePair<string, string>>()).Select(x => x.Key));

    /// L'année d'une clé du suivi (« loyer-2026-P4 », « regul-2026-… ») ; 0 si elle n'en porte pas.
    public static int AnneeDeCle(string key)
    {
        var m = System.Text.RegularExpressions.Regex.Match(key ?? "", @"-(\d{4})(-|$)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    /// L'année d'une date ISO (celle de la charge refacturée), sinon l'année en cours.
    public static int Annee(string iso) => DateTime.TryParse(iso, out var d) ? d.Year : DateTime.Today.Year;

    /// Refus, avant tout envoi, d'un avoir qui n'a aucune facture à citer.
    public static string RefusAvoir(string sujet, string conseil) =>
        $"« {sujet} » a un total négatif : c'est un avoir, et {FactureEn16931.AvoirSansReference} {conseil} Rien n'a été émis.";

    /// Revue 17 (D5-02) : la correction d'une facture transmise ne part pas en avoir : un avoir (381) ne remplace rien, la
    /// plateforme et le locataire garderaient les deux documents. Refus aussi pour la correction d'un avoir transmis.
    /// La bonne méthode (avoir de la totalité puis nouvelle facture, ou différence seule) est à valider avec l'expert-comptable.
    /// Revue 17 (D5-20, lot 7) : une facture transmise par une autre entreprise (transfert : « transmise par SCI X », avec ou sans identifiant)
    /// ne se corrige pas d'ici non plus : la rectificative partirait au nom de la SCI ouverte en citant la facture de SCI X (document faux),
    /// et son « Encaissée » serait refusé à chaque lancement.
    /// Revue 17 (D5-07, lot 11, décision du 05/11) : `parPlateforme` faux (Concerne : SCI pas active, locataire plus concerné ; `raison` dit
    /// pourquoi) : la correction d'une facture transmise est refusée AVANT tout envoi, dans les trois formulaires ; sinon la plateforme garde
    /// l'ancienne version, le locataire reçoit la nouvelle, et « Encaissée » déclare le nouveau montant sur l'ancienne facture. Avant : régul et
    /// refac partaient par e-mail sans un mot, le loyer prévenait après l'e-mail. Une SCI non reliée ne corrige donc plus une facture transmise.
    public static string RefusCorrection(FactureEtat avant, float ttc, string sujet, bool parPlateforme = true, string raison = null) =>
        string.IsNullOrEmpty(avant?.plateformeId) ? null
        : RefusOrigine(avant, sujet) ?? (!parPlateforme
          ? $"« {sujet} » a été transmise à Super PDP : sa correction doit y partir aussi, et elle ne le peut pas en ce moment : " +
            $"{raison ?? "Super PDP n'est pas actif pour la SCI, ou le locataire n'est plus concerné"}. Rien n'a été émis. " +
            "Recommencez quand la ligne « Envoi » annonce Super PDP."
        : ttc < -0.004f || (avant.plateformeDonnees ?? "").Contains("\"type_code\":381")
          ? $"« {sujet} » a été transmise à Super PDP : sa correction ne peut pas partir en avoir (la plateforme garderait " +
            "les deux documents). Rien n'a été émis. Laissez-la telle quelle et émettez la différence à part (Refacturation, " +
            "« Avoir sur la facture n° … »)."
          : null);

    /// D5-20, décision du 17/11 (essai D5-N2 au bac à sable) : le refus d'une facture transmise par une AUTRE entreprise, seul. Il ne dépend
    /// ni du montant ni de l'état de Super PDP : lu dès le clic sur « Corriger » du suivi (le formulaire ne s'ouvre pas), et dans les trois
    /// formulaires avant le refus « Aucun RIB ». Null : jamais transmise, ou transmise par l'entreprise ouverte.
    public static string RefusOrigine(FactureEtat avant, string sujet) =>
        string.IsNullOrEmpty(avant?.plateformeId) || string.IsNullOrEmpty(avant.plateformeOrigine) ? null
        : $"« {sujet} » a été transmise à Super PDP par {avant.plateformeOrigine} : elle ne se corrige pas par la plateforme depuis cette " +
          "entreprise. Rien n'a été émis. Laissez-la telle quelle et émettez la différence à part (Refacturation, « Avoir sur la facture n° … »).";

    /// Revue 17 (D6-03) : un acheteur professionnel (SIREN connu) : pénalités de retard et indemnité de 40 € obligatoires
    /// (art. L441-9 du Code de commerce) ; la case « phrase de retard » est cochée et grisée, comme pour la plateforme.
    public static bool Professionnel(Locataire loc, string siretFormulaire) =>
        SocieteLocataire.Siren(SiretAcheteur(loc, siretFormulaire)).Length == 9;

    /// La ligne « Envoi : … » des formulaires de facture : ce qui va partir, et pourquoi pas la plateforme le cas échéant.
    /// `tva` : le taux (ou le montant de TVA) du document (Revue 17, D5-08, lot 10 : une facture sans TVA part par e-mail, même
    /// sur un bail soumis ; la correction taxée d'un bail devenu exonéré repart par la plateforme).
    /// `notification` : après la bascule, la facture part sans PDF (e-mail de notification). Revue 17 (D5-10, lot 13) : les trois
    /// formulaires le passent (avant, régul et refacturation gardaient la copie PDF par e-mail : limite du 02/11, levée).
    /// Revue 17 (D5-05, lot 11) : après la bascule, une facture qui doit partir par la plateforme et ne le peut pas est annoncée comme
    /// refusée (le formulaire la refuse : RefusObligation), au lieu d'« e-mail seulement » ; la raison est celle de RaisonHorsPlateforme
    /// (qui couvre aussi la SCI non reliée et l'état jamais lu : avant, « Mode global : Email direct (SMTP). »).
    /// Revue 17 (D5-19, coutures) : la ligne « locataire non assujetti à la TVA » est retirée : jamais atteinte depuis le retrait de la
    /// case « Assujetti » (02/11 : SCI active et TVA sur le document donnent toujours Concerne ou SansSiret), et son texte était faux.
    /// Coutures (reste du lot 11) : `depart` : le jour où la facture partira (un loyer préparé à l'avance : sa date d'envoi ; sinon
    /// aujourd'hui), le jour que juge la garde des formulaires (RefusObligation). En août 2027, un loyer à envoyer en septembre est annoncé
    /// comme il partira (notification seule, ou « impossible pour le moment »). Avant : jugé au jour même, la ligne annonçait encore
    /// l'e-mail et l'émission refusait.
    public static string LibelleEnvoi(Locataire loc, float tva, string siretFormulaire, bool notification = false, DateTime? depart = null) =>
        Math.Abs(tva) <= 0.005f ? (Tva.Soumis(loc) ? "Envoi : e-mail (facture sans TVA : pas de facture électronique)."
                                                   : "Envoi : e-mail (bail exonéré de TVA : pas de facture électronique).")   // 02/11
        : Concerne(loc, tva, siretFormulaire) ? (notification || depart >= Bascule
            ? "Envoi : plateforme agréée (Super PDP), puis un e-mail de notification au locataire. La phrase de retard y est obligatoire."
            : "Envoi : plateforme agréée (Super PDP) + copie PDF par e-mail. La phrase de retard y est obligatoire.")
        : SansSiret(loc, tva, siretFormulaire) ? "Envoi : e-mail seulement — ajoutez le SIRET du locataire pour la facture électronique (Super PDP)."
        : DoitPartirParPlateforme(tva, SiretAcheteur(loc, siretFormulaire), depart ?? DateTime.Today)
          ? $"Envoi : impossible pour le moment — depuis le 1er septembre 2027, cette facture doit partir par Super PDP : {RaisonHorsPlateforme(loc, tva, siretFormulaire)}."
        : $"Envoi : e-mail seulement ({RaisonHorsPlateforme(loc, tva, siretFormulaire)}).";

    /// Revue 17 (D5-10, lot 13) : dès la bascule, une facture qui part par la plateforme n'est plus envoyée en PDF : les trois formulaires
    /// (loyer, régularisation, refacturation) demandent « Transmettre ? » puis enregistrent sans e-mail (Finaliser) ; la notification part
    /// une fois Super PDP d'accord (Traiter → Notifier). Une transmission ne se rappelle pas : elle se confirme, comme un e-mail.
    /// `adresse` : l'e-mail du formulaire, celui qui sera prévenu (vide : le locataire ne le sera pas, la facture part quand même).
    public static void ConfirmerTransmission(string adresse, Action transmettre)
    {
        if (ConfirmDialog.Instance == null)
        {
            ConfirmDialog.Erreur("Confirmation indisponible : rien n'a été transmis. Le PDF est enregistré.");
            return;
        }
        adresse = (adresse ?? "").Trim();
        ConfirmDialog.Instance.Show("Transmettre la facture par la plateforme ?",
            "La facture part par Super PDP, sans PDF par e-mail. "
            + (adresse == "" ? "Aucune adresse e-mail : le locataire ne sera pas prévenu."
                             : $"Le locataire en sera prévenu par e-mail ({adresse}).")
            + "\n\nUne facture transmise ne peut pas être rappelée.",
            transmettre, "Transmettre");
    }

    // Revue 17 (D4-05, coutures) : ATransmettre, EnAttente et Pastille lisent l'état sans le locataire (EtatDe(r, null)) : ils demandent
    // « émise ? » ou « payée ? », et les délais propres ne décident que d'« Envoyé » ou « Impayé » ; Traiter, qui connaît le locataire, le passe.
    static bool Emise(FacturationSuivi.Etat e) =>
        e == FacturationSuivi.Etat.Envoye || e == FacturationSuivi.Etat.Impaye || e == FacturationSuivi.Etat.Paye;

    /// Ce qui part à Super PDP : une facture électronique émise, pas encore transmise, ni refusée ni périmée (refaite hors plateforme :
    /// ses anciens montants ne doivent plus partir, revue 16c D5-01). Revue 17 (D5-06, lot 9, relecture) : lue par EnAttente ET par le
    /// bloc « envoyer » de Traiter ; avant, celui-ci ne regardait que l'identifiant vide, et une version périmée ou refusée dont
    /// l'annulation attendait repartait avec le PDF de la correction suivante.
    public static bool ATransmettre(FactureEtat r) =>
        r != null && !string.IsNullOrEmpty(r.plateformeDonnees) && string.IsNullOrEmpty(r.plateformeId)
        && r.plateformeEtat != "refusee" && r.plateformeEtat != "perimee" && Emise(FacturationSuivi.EtatDe(r, null));

    /// Ce que la ligne attend de la plateforme : la transmettre, y déclarer « Encaissée », ou annuler un encaissement.
    public static bool EnAttente(FactureEtat r)
    {
        if (r == null) return false;
        // Transférée d'une autre entreprise : déclarée en son nom (BG7) ; sans son identifiant (marque d'avant) : lecture seule.
        if (!string.IsNullOrEmpty(r.plateformeOrigine) && string.IsNullOrEmpty(r.plateformeOrigineId)) return false;
        var e = FacturationSuivi.EtatDe(r, null);
        return ATransmettre(r)
            || (!string.IsNullOrEmpty(r.plateformeId) && !r.encaisseeDeclaree && e == FacturationSuivi.Etat.Paye)
            // Revue 16c (D5-12) : la facture a quitté « Payé » après la déclaration : l'annulation (212 négatif) attend.
            || (!string.IsNullOrEmpty(r.plateformeId) && r.encaisseeDeclaree && e != FacturationSuivi.Etat.Paye)
            // Revue 17 (D5-06, lot 9) : l'encaissement de la version remplacée reste à annuler, quel que soit l'état de la rectificative.
            || !string.IsNullOrEmpty(r.encaisseeAAnnulerId);
    }

    /// Ce qui part d'ici : les lignes de la SCI ouverte si elle est reliée, et les lignes transmises par une autre
    /// entreprise (déclarées en son nom, avec son jeton), même si la SCI ouverte ne l'est pas (BG7).
    /// Revue 17 (D5-04, lot 7) : pas une ligne dont la déclaration a été refusée pendant cette session (RefuseeCetteSession) : elle reste
    /// « en attente » (pastille, transfert bloqué), sans nouvel appel ni nouvelle fenêtre avant le prochain lancement. Lot 9 (relecture) : une
    /// annulation refusée (notée sous l'identifiant annulé) n'exclut pas la ligne : sa rectificative, ou la reprise d'une panne, part quand
    /// même dans la session ; seul l'appel refusé est sauté (Traiter).
    static bool ATraiter(FactureEtat r) => EnAttente(r) && !RefuseeCetteSession(r.plateformeId) && (Active || !string.IsNullOrEmpty(r.plateformeOrigineId));

    static string Noms(string a, string b) => string.IsNullOrEmpty(a) ? (b ?? "") : string.IsNullOrEmpty(b) || a.Contains(b) ? a : a + ", " + b;
    static string AuNomDe(string noms) => string.IsNullOrEmpty(noms) ? "" : $" (au nom de {noms})";

    /// Un refus NOMMÉ d'une déclaration (« Encaissée », ou son annulation : `quoi`), qui n'arrête ni le reste du locataire ni la tournée ;
    /// null pour une panne, qui arrête. « origine » : un 403 sur une ligne transmise par une autre entreprise, l'appelant n'en est pas membre
    /// (BG7). Revue 17 (D5-04, lot 7) : « refusee », un refus de Super PDP (une de ses règles, pas une panne), dit avec son détail (décision
    /// du 01/11) ; avant, il arrêtait le locataire (sa facture neuve ne partait plus) et toute la tournée du lancement. Dans les deux cas la
    /// ligne est notée pour la session : plus retentée avant le prochain lancement. `id` : l'identifiant appelé quand ce n'est pas celui de la
    /// ligne (lot 9 : la version remplacée dont l'encaissement s'annule).
    static Reponse RefusNomme(Locataire loc, FactureEtat r, Reponse rep, string quoi, string id = null)
    {
        var nomme = rep?.refus == "acces" && !string.IsNullOrEmpty(r.plateformeOrigineId)
            ? new Reponse { refus = "origine", erreur = $"« {r.libelle} » de {loc?.Name} a été transmise par {r.plateformeOrigine} : seul un membre de {r.plateformeOrigine} peut déclarer son encaissement." }
            : rep?.refus == "refusee"
            ? new Reponse { refus = "refusee", erreur = $"« {r.libelle} » de {loc?.Name} : {quoi}. Détail technique : {rep.erreur}" }
            : null;
        if (nomme == null) return null;
        NoterRefusEncaissement(id ?? r.plateformeId);
        Debug.LogWarning("[FactureElectronique] " + nomme.erreur);
        return nomme;
    }

    /// Revue 17 (D5-04, lot 7) : ce qui arrête une tournée (Demarrer, Rattraper) : une panne (plateforme ou serveur injoignable, jeton,
    /// autorisation retirée, entreprise changée, réponse sans motif…), dont le motif est dit et qui repart au prochain lancement. Un refus
    /// nommé (« refusee », « origine ») n'arrête rien ; rien (null) non plus.
    public static bool ArreteLaTournee(Reponse p) => p != null && p.refus != "refusee" && p.refus != "origine";

    /// Deux résultats d'un même traitement : une panne qui arrête prime ; deux refus nommés se disent ensemble (une seule fenêtre).
    public static Reponse Cumul(Reponse a, Reponse b) =>
        a == null ? b : b == null ? a : ArreteLaTournee(a) ? a : ArreteLaTournee(b) ? b
        : new Reponse { refus = a.refus, erreur = a.erreur + "\n" + b.erreur };

    /// Ce que déclare « Encaissée » : le total TTC de la facture (toutes ses lignes) et la date du dernier versement
    /// saisi, sinon aujourd'hui (revue 16c, D5-07 ; décision du 01/11 : pour la TVA sur les encaissements, c'est la
    /// date du versement qui fixe la période).
    /// Revue 17 (D5-12, coutures ; décision du 10/11) : sans versement saisi, le jour où la facture est passée « Payé » (payeLeISO, posé par
    /// FacturationSuivi.SetStatut), avant la date du jour. Avant : « Payé » cliqué le 30/09 hors ligne (ou Super PDP en panne) et déclaré
    /// le 01/10 donnait un encaissement d'octobre. Une ligne payée avant cette version n'a pas ce jour : la date du jour, comme avant.
    public static (float montant, string dateISO) Encaissement(Locataire loc, FactureEtat r)
    {
        var lignes = FacturationSuivi.MemeFacture(loc, r.key).DefaultIfEmpty(r).ToList();
        // Lot G (16/11) : somme en décimal, arrondie au centime. Avant : en float, trois lignes de 333,332 donnaient 999,996, enregistré tel
        // quel dans encaisseeMontant (le montant déclaré à Super PDP, lui, passait déjà par Montant() : « 1000.00 »).
        float montant = (float)Math.Round(lignes.Sum(x => (decimal)(double)x.montant), 2, MidpointRounding.AwayFromZero);
        // Retour BN17 : un remboursement (versement négatif) ne déplace pas la date d'une facture : celle du dernier paiement reçu.
        var verses = lignes.SelectMany(x => x.versements ?? new List<Versement>()).Where(v => montant < 0f || v.montant > 0f).ToList();
        string date = verses.Select(v => v.dateISO).Where(d => !string.IsNullOrEmpty(d)).OrderBy(d => d).LastOrDefault();
        // Décision du 11/11 : des versements qui ne couvrent pas le total (un acompte) ne datent pas l'encaissement : le reste est reçu
        // le jour du clic. Avant : acompte du 05/09, « Payé » cliqué le 30/09, « Encaissée » déclarée au 05/09.
        string paye = lignes.Select(x => x.payeLeISO).Where(d => !string.IsNullOrEmpty(d)).Max();
        if (!string.IsNullOrEmpty(paye) && (string.IsNullOrEmpty(date) || Math.Abs(verses.Sum(v => v.montant)) < Math.Abs(montant) - 0.005f)) date = paye;   // D5-12 : le jour du « Payé »
        // Revue 17 (D5-11, lot 8 ; décision du 09/11) : l'« Encaissée » d'un avoir part en POSITIF, comme l'avoir lui-même (type 381,
        // montants positifs). Chez Super PDP, un montant négatif veut dire « annulation d'un encaissement déclaré » : c'est ce que
        // l'annulation envoie (Traiter). Avant : −297,60 € pour un avoir de 297,60 €, et son annulation en positif. Le signe sert encore
        // au-dessus, pour dater un avoir par son dernier remboursement.
        return (Math.Abs(montant), string.IsNullOrEmpty(date) ? DateTime.Today.ToString("yyyy-MM-dd") : date);
    }

    static string Montant(float v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    /// La pastille de la ligne dans le suivi (null : pas concernée).
    public static string Pastille(FactureEtat r)
    {
        if (r == null) return null;
        if (!string.IsNullOrEmpty(r.plateformeOrigine))
            return r.encaisseeDeclaree ? $"Encaissée déclarée ({r.plateformeOrigine})" : "transmise par " + r.plateformeOrigine;
        if (r.encaisseeDeclaree) return "Encaissée déclarée";
        if (!string.IsNullOrEmpty(r.plateformeId)) return "Plateforme";
        if (r.plateformeEtat == "refusee") return "Refusée par la plateforme";
        if (r.plateformeEtat == "perimee") return null;   // refaite hors plateforme : elle n'y part pas
        // Revue 17 (D5-16, coutures) : « À renvoyer » seulement après un essai en panne (Traiter pose « a_renvoyer ») ; une facture pas
        // encore essayée est « À transmettre ». Avant : « À renvoyer » quelques secondes après chaque envoi, comme un échec.
        return !string.IsNullOrEmpty(r.plateformeDonnees) && Emise(FacturationSuivi.EtatDe(r, null))
            ? (r.plateformeEtat == "a_renvoyer" ? "À renvoyer" : "À transmettre") : null;
    }

    /// Après un envoi ou un paiement (fiche ouverte) : ce qui attend la plateforme pour ce locataire part
    /// maintenant ; enregistré s'il y a du changement, annoncé en bas.
    public static async void Rattraper(BatimentPrefab bp, Locataire loc)
    {
        if (bp == null || loc?.facturesEtat == null || !loc.facturesEtat.Any(ATraiter)) return;
        // Un traitement de ce locataire déjà en vol (« Payé » cliqué pendant la transmission…) : il ne repart pas
        // en double ; ce qu'il reste à faire est repris juste après (revue 16c, D5-02).
        if (!_enCours.Add(loc.id)) return;
        try
        {
            string batId = bp.getID();
            var (envoyees, encaissees, annulees, change, panne, auNomDe) = await Traiter(loc, bp);
            // Revue 17 (D5-04, lot 7) : un refus nommé n'empêche pas la seconde passe (ce qui est devenu à traiter pendant l'attente), et ne s'y perd pas.
            if (!ArreteLaTournee(panne) && loc.facturesEtat.Any(ATraiter))
            {
                var (a, b, d, c, p, n) = await Traiter(loc, bp);
                envoyees += a; encaissees += b; annulees += d; change |= c; panne = Cumul(panne, p); auNomDe = Noms(auNomDe, n);
            }
            if (change) Enregistrer(batId, loc);
            RafraichirSuivi(loc);
            var morceaux = new List<string>();
            if (envoyees > 0) morceaux.Add(envoyees == 1 ? "Facture transmise à Super PDP." : $"{envoyees} factures transmises à Super PDP.");
            if (encaissees > 0) morceaux.Add((encaissees == 1 ? "« Encaissée » déclaré à Super PDP" : $"{encaissees} « Encaissée » déclarés à Super PDP") + AuNomDe(auNomDe) + ".");
            if (annulees > 0) morceaux.Add((annulees == 1 ? "1 encaissement annulé chez Super PDP" : $"{annulees} encaissements annulés chez Super PDP")
                                           + AuNomDe(auNomDe) + (annulees == 1 ? " (facture plus payée ou remplacée)." : " (factures plus payées ou remplacées)."));
            if (panne != null) morceaux.Add(Panne(loc, panne));
            if (morceaux.Count > 0) UndoToast.Instance?.ShowInfo(string.Join(" ", morceaux));
        }
        catch (Exception e) { Debug.LogWarning("[FactureElectronique] " + e.Message); }
        finally { _enCours.Remove(loc.id); }
    }

    /// Un appel à la plateforme a échoué (01/11) : le motif est journalisé, et un REFUS (règle de Super PDP non respectée, pas membre de
    /// l'entreprise d'origine) est montré tel quel, nommé, au lieu de passer pour une panne : « injoignable » cachait le vrai message, et un
    /// refus ne se répare pas tout seul au prochain lancement. Revue 17 (D5-04, lot 7) : les refus d'un traitement (plusieurs lignes, ou toute
    /// la tournée du lancement) tiennent dans UNE fenêtre, et ils n'ont rien arrêté (RefusNomme les a déjà journalisés). Rend la phrase du résumé.
    static string Panne(Locataire loc, Reponse panne)
    {
        if (ArreteLaTournee(panne))
        {
            Debug.LogWarning($"[FactureElectronique] {loc?.Name} : {Motif(panne) ?? "pas de réponse"}");
            return Phrase(panne);
        }
        // Revue 17 (lot 7, relecture) : une ligne « transmise par SCI X » ne se corrige pas d'ici (D5-20) : la suite dépend du refus.
        string suite = panne.refus == "origine"
            ? "demandez à un membre de l'entreprise qui l'a transmise de déclarer l'encaissement, ou signalez-le."
            : "si un refus de Super PDP se répète, corrigez la facture depuis le suivi (Corriger), ou signalez-le.";
        ConfirmDialog.Erreur("Refus de la facture électronique :\n" + panne.erreur
                             + "\n\nLes autres factures ont continué. Nouvel essai au prochain lancement ; " + suite);
        return "Déclaration refusée (voir le message).";
    }

    /// Revue 16c (D5-14) : la phrase du résumé selon l'échec : une panne passagère annonce un nouvel essai ; un refus qui ne se
    /// réparera pas seul (autorisation retirée, SCI non reliée, SIRET absent…) dit quoi faire, au lieu d'« injoignable ».
    public static string Phrase(Reponse panne)
    {
        switch (panne?.refus)
        {
            case null: case "": case "indisponible": case "serveur":
                return "Plateforme injoignable : nouvel essai au prochain lancement.";
            default:
                return Motif(panne) ?? "Plateforme injoignable : nouvel essai au prochain lancement.";
        }
    }

    /// Locataires dont les factures sont en cours de transmission sur ce poste (un seul traitement à la fois).
    static readonly HashSet<string> _enCours = new HashSet<string>();

    // Retour BN9 (04/11) : juste après sa transmission, Super PDP n'a pas encore lu la facture (pas de ventilation TVA) et
    // refuse son « Encaissée » (« facture sans ventilation TVA ») ; une correction de facture payée déclenchait ce refus à
    // chaque fois. On attend quelques minutes : la tournée suivante, ou le prochain lancement, le déclare. Sans effet sur la
    // TVA : c'est la date du versement qui est déclarée.
    static readonly Dictionary<string, DateTime> _transmisLe = new Dictionary<string, DateTime>();
    public const double DelaiEncaissementMinutes = 5;
    public static void NoterTransmission(string plateformeId, DateTime quand) { if (!string.IsNullOrEmpty(plateformeId)) _transmisLe[plateformeId] = quand; }
    public static bool TransmiseALinstant(string plateformeId, DateTime maintenant)
        => !string.IsNullOrEmpty(plateformeId) && _transmisLe.TryGetValue(plateformeId, out var t) && (maintenant - t).TotalMinutes < DelaiEncaissementMinutes;

    // Revue 17 (D5-04, lot 7) : les déclarations (« Encaissée », annulation) refusées pendant cette session, par identifiant de facture chez
    // Super PDP : plus retentées avant le prochain lancement (chaque action sur le locataire relance Rattraper), les autres lignes et les autres
    // locataires continuent. Une facture corrigée reçoit un autre identifiant (Preparer) : elle n'est plus retenue. Un refus qui dure revient
    // à chaque lancement, et le locataire ne se transfère pas tant que la ligne attend (TransfertCloud.Bloque) : corriger la facture, ou
    // changer son état. Lot 9 : l'annulation de la version remplacée est notée sous l'identifiant annulé (encaisseeAAnnulerId) ; seul cet
    // appel est sauté, la ligne reste traitée.
    static readonly HashSet<string> _refusEncaissement = new HashSet<string>();
    public static void NoterRefusEncaissement(string plateformeId) { if (!string.IsNullOrEmpty(plateformeId)) _refusEncaissement.Add(plateformeId); }
    public static bool RefuseeCetteSession(string plateformeId) => !string.IsNullOrEmpty(plateformeId) && _refusEncaissement.Contains(plateformeId);

    /// Après l'attente de Super PDP, la fiche a pu être relue (collègue, temps réel, rechargement) : l'objet traité
    /// est alors orphelin et son enregistrement perdu, la facture repartait au lancement suivant (revue 16c, D5-02).
    /// Les résultats sont recopiés sur la fiche à jour, puis elle est enregistrée.
    static void Enregistrer(string batId, Locataire loc)
    {
        var bp = BatimentManager.Instance?.BatimentPrefab.Find(b => b != null && b.getID() == batId);
        var aJour = bp?.listLocataire.Find(l => l != null && l.id == loc.id);
        if (aJour == null) { Debug.LogWarning($"[FactureElectronique] {loc.Name} : fiche introuvable, résultat non enregistré."); return; }
        RecopierPlateforme(loc, aJour);
        bp.SaveAfterModifyToDoListLocataire(silencieux: true);   // revue 16c, D2-04 : un refus ne recharge pas tout
    }

    /// Les résultats de la plateforme d'un objet locataire sur un autre (même locataire relu), ligne par ligne, seulement
    /// si c'est la même facture électronique : une facture refaite ailleurs entre-temps garde la sienne. Tous les champs plateforme* et
    /// encaissee* suivent (un test par réflexion veille, lot 7), et le retour « en attente d'envoi » d'un refus.
    public static void RecopierPlateforme(Locataire de, Locataire vers)
    {
        if (de?.facturesEtat == null || vers?.facturesEtat == null || ReferenceEquals(de, vers)) return;
        foreach (var r in de.facturesEtat)
        {
            var n = vers.facturesEtat.Find(x => x.key == r.key);
            if (n == null || n.plateformeDonnees != r.plateformeDonnees) continue;
            n.plateformeId = r.plateformeId;
            n.plateformeEtat = r.plateformeEtat;
            n.plateformeErreur = r.plateformeErreur;
            n.plateformeOrigine = r.plateformeOrigine;
            n.plateformeOrigineId = r.plateformeOrigineId;
            n.encaisseeDeclaree = r.encaisseeDeclaree;
            // Revue 17 (D5-23, lot 7) : le montant et la date déclarés manquaient (ajoutés après cette recopie, 16c D5-07 / D5-12) : sur la fiche
            // enregistrée, l'annulation retombait sur un montant recalculé et la date déclarée était perdue.
            n.encaisseeMontant = r.encaisseeMontant;
            n.encaisseeDateISO = r.encaisseeDateISO;
            n.encaisseeAAnnulerId = r.encaisseeAAnnulerId;   // Revue 17 (D5-06, lot 9) : sinon la fiche relue renverrait l'annulation une seconde fois
            // Revue 17 (D5-27, lot 7) : après la bascule, un refus remet la facture « en attente d'envoi » (rien n'est parti), toutes ses lignes :
            // la fiche relue le reçoit aussi, sinon elle restait « Envoyé » et passait « Impayé ». Le statut n'est pas recopié dans les autres cas
            // (un collègue a pu le changer).
            if (r.plateformeEtat == "refusee" && r.statut == "AttenteEnvoi")
                foreach (var m in FacturationSuivi.MemeFacture(vers, n.key).DefaultIfEmpty(n)) m.statut = "AttenteEnvoi";
        }
    }

    /// Au démarrage du cloud (DemarrageCloud) : l'état de la SCI, puis tout ce qui attend (envois en panne,
    /// encaissements), sur les fiches que personne ne modifie.
    public static async void Demarrer(EntrepriseCloud e)
    {
        try
        {
            // L'état d'abord, tout de suite : l'écran Facturer en a besoin dès qu'on l'ouvre.
            if (SessionCloud.Entreprise != e || HorsLigne.Actif) return;
            var etat = await Etat();
            if (etat.ok && etat.relie && etat.indisponible)
                UndoToast.Instance?.ShowInfo("Super PDP ne répond pas : CIPL garde son dernier état connu, les factures partiront dès qu'il répond.");
            await Task.Delay(35000);   // le rattrapage après la relecture des verrous et la tournée de l'annuaire
            if (SessionCloud.Entreprise != e || HorsLigne.Actif || AbonnementCloud.LectureSeule) return;
            // Revue 16c (D1-06, D5-04) : un échec passager au lancement laissait la plateforme muette toute la session, sans un
            // mot, et les loyers du jour partaient sans facture électronique. Second essai, puis avertissement.
            if (!etat.ok && !(await Etat()).ok)
            {
                EtatInconnu = true;
                // Revue 17 (D5-05, lot 12) : après la bascule, ces factures ne partent plus par e-mail seul (refus au formulaire, retenue au lancement).
                UndoToast.Instance?.ShowInfo(NotificationSeule
                    ? "Super PDP : état non lu. Les factures avec TVA aux locataires qui ont un SIRET ne peuvent pas être émises d'ici le prochain lancement (depuis le 1er septembre 2027, elles doivent partir par la plateforme)."
                    : "Super PDP : état non lu. Les loyers émis d'ici le prochain lancement partiront par e-mail seul.");
            }
            var bm = BatimentManager.Instance;
            if (bm == null || !bm.BatimentPrefab.Any(b => (b?.listLocataire ?? new List<Locataire>()).Any(l => l?.facturesEtat != null && l.facturesEtat.Any(ATraiter)))) return;
            var libre = await EcritureCloud.LocatairesLibres();
            if (libre == null || SessionCloud.Entreprise != e) return;
            int envoyees = 0, encaissees = 0, annulees = 0;
            string noms = "";
            Reponse panne = null, refus = null; Locataire bloque = null;   // Revue 17 (D5-04, lot 7) : `refus` : les refus nommés de la tournée, dits ensemble à la fin
            foreach (var bp in bm.BatimentPrefab.ToList())
            {
                foreach (var loc in (bp?.listLocataire ?? new List<Locataire>()).ToList())
                {
                    if (loc?.facturesEtat == null || !libre(loc.id) || !loc.facturesEtat.Any(ATraiter)) continue;
                    if (!_enCours.Add(loc.id)) continue;   // déjà en cours (Rattraper) : pas de double envoi (D5-02)
                    try
                    {
                        string batId = bp.getID();
                        var (a, b, d, change, p, n) = await Traiter(loc, bp);
                        if (change) Enregistrer(batId, loc);
                        envoyees += a; encaissees += b; annulees += d; noms = Noms(noms, n);
                        if (ArreteLaTournee(p)) { panne = p; bloque = loc; } else refus = Cumul(refus, p);
                    }
                    finally { _enCours.Remove(loc.id); }
                    if (panne != null) break;   // une panne arrête la tournée ; un refus nommé (« origine » BG7, « Encaissée » refusé D5-04) n'arrête pas les autres
                }
                if (panne != null) break;
            }
            // Une panne arrête la tournée : son motif est dit, le reste repartira au prochain lancement ; les refus nommés tiennent dans une fenêtre.
            string fin = (panne != null ? " " + Panne(bloque, panne) : "") + (refus != null ? " " + Panne(null, refus) : "");
            if (envoyees + encaissees + annulees > 0 || fin != "")
                UndoToast.Instance?.ShowInfo($"Super PDP : {envoyees} facture(s) transmise(s), {encaissees} « Encaissée » déclaré(s), {annulees} annulé(s){AuNomDe(noms)}.{fin}");
        }
        catch (Exception ex) { Debug.LogWarning("[FactureElectronique] Démarrage : " + ex.Message); }
    }

    /// Les lignes d'un locataire qui attendent la plateforme, une par une. Plateforme injoignable (`panne`) :
    /// on s'arrête, la ligne garde son état et repartira au prochain lancement. Un refus nommé (`panne` « refusee » ou « origine »,
    /// cumulé) : rendu à la fin, le reste du locataire a continué (D5-04, lot 7).
    static async Task<(int envoyees, int encaissees, int annulees, bool change, Reponse panne, string auNomDe)> Traiter(Locataire loc, BatimentPrefab bp)
    {
        int envoyees = 0, encaissees = 0, annulees = 0;
        bool change = false;
        string auNomDe = "";           // les entreprises d'origine au nom desquelles on a déclaré (BG7)
        Reponse refus = null;          // les refus nommés (« pas membre de l'origine », « Encaissée » refusé : D5-04) : dits à la fin, sans arrêter le reste du locataire
        // Revue 16c (D5-11) : l'entreprise ouverte au départ ; si elle change pendant l'attente de Super PDP, on s'arrête
        // (sinon les factures de la SCI A partaient avec la session de la SCI B).
        var e = SessionCloud.Entreprise;
        var Changee = new Reponse { refus = "changee", erreur = "entreprise changée pendant le traitement" };
        foreach (var r in loc.facturesEtat.ToList())
        {
            if (!ATraiter(r)) continue;
            if (SessionCloud.Entreprise != e) return (envoyees, encaissees, annulees, change, Changee, auNomDe);
            // Revue 17 (D5-06, lot 9, décision du 05/11) : l'« Encaissée » de la version remplacée est annulé D'ABORD (212 négatif du même
            // montant, daté comme l'encaissement annulé : la période de TVA d'origine), puis la rectificative part ; elle recevra le sien une
            // fois payée (plus bas, après l'annulation : sinon son montant écraserait celui à annuler). Panne : la rectificative attend (même
            // ordre que BE5 : +1 000 puis −1 000, jamais sur une facture déjà remplacée). Refus de Super PDP : noté pour la session (D5-04)
            // sous l'identifiant annulé : l'appel n'est plus retenté avant le prochain lancement, la rectificative part quand même, tout de suite.
            // ponytail: une correction pendant un « Encaissée » en vol (Rattraper d'un « Payé » cliqué juste avant) pose le drapeau sur la
            // nouvelle version sans identifiant : rien ne s'annule ni ne se déclare ; quelques secondes, non traité.
            if (!string.IsNullOrEmpty(r.encaisseeAAnnulerId) && !RefuseeCetteSession(r.encaisseeAAnnulerId))
            {
                var rep = await Appeler(new Demande { action = "encaisser", entreprise = r.plateformeOrigineId, id = r.encaisseeAAnnulerId,
                                                      montant = Montant(-r.encaisseeMontant), date = r.encaisseeDateISO });
                if (rep.ok) { r.encaisseeAAnnulerId = ""; r.encaisseeMontant = 0f; r.encaisseeDateISO = ""; annulees++; change = true; auNomDe = Noms(auNomDe, r.plateformeOrigine); }
                else if (RefusNomme(loc, r, rep, "annulation de l'encaissement de la facture remplacée refusée", r.encaisseeAAnnulerId) is Reponse ro) refus = Cumul(refus, ro);
                else return (envoyees, encaissees, annulees, change, rep, auNomDe);
            }
            // Revue 17 (D5-06, lot 9, relecture) : la règle de l'attente, pas l'identifiant vide seul : une version périmée ou refusée dont
            // l'annulation vient de partir ne repart pas (ses montants, avec le PDF de la correction suivante : document faux, 16c D5-01).
            if (ATransmettre(r))
            {
                string cheminPdf = FacturationSuivi.CheminPdf(r, bp.getName(), loc.Name);
                byte[] pdf = null;
                try { pdf = File.ReadAllBytes(cheminPdf); } catch (Exception) { /* plus bas */ }
                // Revue 16c (D5-06) : pas encore recopié sur ce PC (émis sur un autre poste) : lu dans le cloud. Toujours
                // absent : la ligne reste à transmettre, elle n'est plus marquée « refusée » pour tout le monde.
                if (pdf == null) pdf = await DocumentsCloud.LireOctets(cheminPdf);
                if (pdf == null) { Debug.LogWarning($"[FactureElectronique] {loc.Name} · {r.libelle} : PDF introuvable ici et dans le cloud, transmission remise."); continue; }
                if (SessionCloud.Entreprise != e) return (envoyees, encaissees, annulees, change, Changee, auNomDe);
                var rep = await AppelerLong(new Demande { action = "envoyer", cle = Cle(loc, r), facture = r.plateformeDonnees, pdf = Convert.ToBase64String(pdf) });
                if (rep.ok)
                {
                    r.plateformeId = rep.id; r.plateformeEtat = "envoyee"; r.plateformeErreur = ""; envoyees++; change = true;
                    NoterTransmission(rep.id, DateTime.Now);   // retour BN9 : « Encaissée » attend que Super PDP l'ait lue
                    if (NotificationSeule) await Notifier(loc, r, bp.getBatiment());
                }
                else if (rep.refus == "refusee")
                {
                    r.plateformeEtat = "refusee"; r.plateformeErreur = rep.erreur; change = true;
                    // Revue 16c (D5-05, décision du 31/10) : après la bascule, rien n'est parti : la facture revient « en attente
                    // d'envoi » (toutes ses lignes), à corriger depuis le suivi ; l'envoi automatique ne la repropose pas telle quelle.
                    // Revue 17 (D5-10, lot 13) : vrai pour les trois types : régul et refac n'envoient plus de PDF après la bascule.
                    if (NotificationSeule)
                        foreach (var m in FacturationSuivi.MemeFacture(loc, r.key).DefaultIfEmpty(r)) m.statut = "AttenteEnvoi";
                    Debug.LogWarning($"[FactureElectronique] {loc.Name} · {r.libelle} : refusée par Super PDP : {rep.erreur}");
                    ConfirmDialog.Erreur($"Super PDP a refusé la facture « {r.libelle} » de {loc.Name}"
                                         + (NotificationSeule ? " : elle n'est pas partie, le locataire n'a rien reçu. Elle revient « en attente d'envoi » : refaites-la depuis le suivi (« Refaire »)."
                                                              : " (le PDF est bien parti par e-mail).")
                                         + "\nDétail technique : " + rep.erreur);
                }
                else
                {
                    if (r.plateformeEtat != "a_renvoyer") { r.plateformeEtat = "a_renvoyer"; change = true; }
                    return (envoyees, encaissees, annulees, change, rep, auNomDe);
                }
            }
            var etat = FacturationSuivi.EtatDe(r, loc);
            if (!string.IsNullOrEmpty(r.plateformeId) && !r.encaisseeDeclaree && etat == FacturationSuivi.Etat.Paye
                && string.IsNullOrEmpty(r.encaisseeAAnnulerId)   // Revue 17 (D5-06, lot 9) : pas avant l'annulation de l'ancien encaissement
                && !TransmiseALinstant(r.plateformeId, DateTime.Now)   // retour BN9
                && FacturationSuivi.ARembourser(loc, r.key) <= 0.005f)   // retour BN17 : pas avant le remboursement
            {
                var (montant, date) = Encaissement(loc, r);   // revue 16c, D5-07 : montant TTC et date du versement
                if (SessionCloud.Entreprise != e) return (envoyees, encaissees, annulees, change, Changee, auNomDe);
                var rep = await Appeler(new Demande { action = "encaisser", entreprise = r.plateformeOrigineId, id = r.plateformeId, montant = Montant(montant), date = date });
                if (!rep.ok) { if (RefusNomme(loc, r, rep, "« Encaissée » refusé") is Reponse ro) { refus = Cumul(refus, ro); continue; } return (envoyees, encaissees, annulees, change, rep, auNomDe); }
                r.encaisseeDeclaree = true; r.encaisseeMontant = montant; r.encaisseeDateISO = date; encaissees++; change = true;
                auNomDe = Noms(auNomDe, r.plateformeOrigine);
            }
            else if (!string.IsNullOrEmpty(r.plateformeId) && r.encaisseeDeclaree && etat != FacturationSuivi.Etat.Paye)
            {
                // Revue 16c (D5-12, décision du 31/10) : la facture a quitté « Payé » : l'encaissement déclaré est annulé par un
                // second 212 du même montant en négatif (cas d'usage officiel « annulation d'encaissement »), daté du jour.
                // Lot 8 (D5-11) : l'opposé de ce qui a été déclaré, quel que soit son signe : un avoir déclaré en négatif avant le
                // 17/11 s'annule encore en positif. Sans montant gardé (ligne ancienne) : celui d'aujourd'hui.
                float montant = Math.Abs(r.encaisseeMontant) > 0.005f ? r.encaisseeMontant : Encaissement(loc, r).montant;
                if (SessionCloud.Entreprise != e) return (envoyees, encaissees, annulees, change, Changee, auNomDe);
                var rep = await Appeler(new Demande { action = "encaisser", entreprise = r.plateformeOrigineId, id = r.plateformeId, montant = Montant(-montant), date = DateTime.Today.ToString("yyyy-MM-dd") });
                if (!rep.ok) { if (RefusNomme(loc, r, rep, "annulation de l'encaissement refusée") is Reponse ro) { refus = Cumul(refus, ro); continue; } return (envoyees, encaissees, annulees, change, rep, auNomDe); }
                r.encaisseeDeclaree = false; r.encaisseeMontant = 0f; r.encaisseeDateISO = ""; change = true;
                auNomDe = Noms(auNomDe, r.plateformeOrigine);
                annulees++;   // dit dans le résumé (un message à part était recouvert par le suivant)
            }
        }
        return (envoyees, encaissees, annulees, change, refus, auNomDe);
    }

    /// Dès la bascule : le locataire apprend par e-mail que sa facture est transmise (modèle de Réglages › Modèles d'e-mail), sans PDF.
    /// Un échec est dit, sans réessai : la facture, elle, est bien partie.
    /// Revue 17 (D5-10 / D5-26, lot 13) : pour toute ligne transmise, loyer, régularisation ou refacturation : leurs formulaires n'envoient
    /// alors aucun PDF (« Transmettre la facture par la plateforme ? », ConfirmerTransmission), la notification est le seul e-mail. Avant,
    /// régul et refac envoyaient l'e-mail de facture avec le PDF, puis cette notification : deux e-mails pour un document.
    static async Task Notifier(Locataire loc, FactureEtat r, Batiment bat)
    {
        // Revue 17 (D4-01, lot 6 ; D5-N5) : avec la ligne : le loyer d'un futur cessionnaire prévient le cessionnaire (l'adresse
        // gardée sur la facture, celle que le formulaire a promise), plus la fiche du cédant.
        string dest = FactureEnvoiAuto.Destinataire(loc, r.type, r);
        string echec = string.IsNullOrWhiteSpace(dest) ? "le locataire n'a pas d'adresse e-mail." : null;
        if (echec == null)
        {
            var ctx = FactureEnvoiAuto.Contexte(loc, r);
            var info = loc.FactureInfoDe(r.type);   // le texte propre à cette facture, sinon le modèle des Réglages
            // Revue 17 (D6-20, coutures ; décision du 09/11) : la notification d'une refacturation emporte les justificatifs de ses charges
            // (son PDF, parti par la plateforme, dit « Justificatif joint ») ; aucune pièce pour les autres types. Un justificatif
            // introuvable sur ce poste ne bloque rien : il est dit au centre (ici, un message du bas serait recouvert par le résumé de
            // la transmission).
            var pieces = FactureRefacPanel.PiecesDeLigne(loc, r, bat, null, out var absents);
            var envoi = EmailService.Envoyer(dest,
                FactureVarResolver.Resolve(EmailService.ObjetNotif(info?.notifObjet), loc, bat, ctx),
                FactureVarResolver.Resolve(EmailService.CorpsNotif(info?.notifCorps, ctx.ttc < -0.004f), loc, bat, ctx),   // lot 13 : un avoir n'est pas « à régler »
                pieces);
            while (!envoi.Termine) await Task.Delay(100);
            if (!envoi.Succes) echec = envoi.Erreur;
            else if (FactureRefacPanel.AbsentsDits(absents) is string manquants)
                ConfirmDialog.Erreur($"La facture « {r.libelle} » de {loc.Name} est transmise et le locataire est prévenu par e-mail. {manquants}");
        }
        if (echec != null)
            ConfirmDialog.Erreur($"La facture « {r.libelle} » de {loc.Name} est bien transmise par la plateforme, "
                                 + "mais l'e-mail qui prévient le locataire n'est pas parti : " + echec);
    }

    /// Identifiant de la facture chez Super PDP (36 caractères au plus) : un même envoi refait n'en crée pas deux.
    public static string Cle(Locataire loc, FactureEtat r)
    {
        string id = (loc?.id ?? "").Replace("-", "");
        // Revue 17 (D5-01) : le numéro que la plateforme reçoit (celui du JSON de la ligne) : une correction (« …-corrigee-1 »)
        // a sa propre clé ; avec le numéro de base, le serveur répondait « déjà transmise » et la rectificative ne partait jamais.
        string n = FactureEn16931.NumeroDe(r?.plateformeDonnees) ?? (string.IsNullOrEmpty(r?.numero) ? r?.key : r.numero);
        string id8 = id.Length > 8 ? id.Substring(0, 8) : id;
        string c = $"{id8}-{n}";
        if (c.Length <= 36) return c;
        // Revue 17 (D5-01 reste, D1-N4) : coupée à 36, deux rectificatives d'un numéro long partageaient la clé (la seconde « déjà
        // transmise », jamais partie). Au-delà : l'empreinte du numéro complet (27 hexadécimaux de son SHA-256), toujours 36.
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return $"{id8}-{BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(n ?? ""))).Replace("-", "").ToLowerInvariant().Substring(0, 27)}";
    }

    static void RafraichirSuivi(Locataire loc)
    {
        foreach (var fiche in UnityEngine.Object.FindObjectsOfType<LocatairePrefab>())
            if (fiche != null && fiche.GetLocataire()?.id == loc.id) LocataireSuiviInline.RefreshFor(fiche);   // relue : même id
    }

    // ── Le serveur ─────────────────────────────────────────────────────────────

    static async Task<Reponse> Appeler(Demande d)
    {
        if (!SessionCloud.Active || SessionCloud.Entreprise == null) return new Reponse { refus = "cloud" };
        if (string.IsNullOrEmpty(d.entreprise)) d.entreprise = SessionCloud.Entreprise.id;   // BG7 : l'origine d'une facture transférée, sinon la SCI ouverte
        var r = await CloudClient.FonctionServeur(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "facture-electronique",
                    JsonUtility.ToJson(d));
        return Lire(r.Ok, r.Corps, r.Erreur);
    }

    /// L'envoi d'une facture : deux appels à Super PDP (Factur-X, puis envoi) et un PDF, plus long que 15 s parfois.
    static async Task<Reponse> AppelerLong(Demande d)
    {
        if (!SessionCloud.Active || SessionCloud.Entreprise == null) return new Reponse { refus = "cloud" };
        if (string.IsNullOrEmpty(d.entreprise)) d.entreprise = SessionCloud.Entreprise.id;   // BG7 : l'origine d'une facture transférée, sinon la SCI ouverte
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
    /// Coutures (reste du lot 13) : « Active » dit la règle de Concerne, vraie avant comme après la bascule : toute facture avec TVA (loyer,
    /// régularisation, refacturation) à un locataire qui a un SIRET. Avant : « les factures de loyer aux locataires assujettis partent aussi ».
    public static string Statut(Reponse e) =>
          e == null || !e.relie ? null
        : e.statut == "verified" ? "Active : les factures avec TVA (loyer, régularisation, refacturation) des locataires qui ont un SIRET partent par Super PDP."
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
            case "origine": return r.erreur;   // BG7 : pas membre de l'entreprise qui a transmis la facture
            case "admin": return "Seul un administrateur peut activer ou couper la facture électronique.";
            case "config": return "La facture électronique n'est pas encore réglée sur le serveur (Supabase › Edge Functions › Secrets : SUPERPDP_CLIENT_ID et SUPERPDP_CLIENT_SECRET).";
            case "siren": return "Cette entreprise n'a pas de SIRET : renseignez-le d'abord (Réglages → Entreprise).";
            case "retire": return "L'autorisation de Super PDP a expiré ou a été retirée : « Se déconnecter », puis « Activer avec Super PDP » de nouveau.";
            case "jeton": return "Super PDP refuse de renouveler l'autorisation." + detail;
            case "non_relie": return "Cette SCI n'est pas reliée à Super PDP (Réglages → Intégrations → Facture électronique).";
            case "changee": return "Entreprise changée pendant le traitement : la suite partira au prochain lancement.";
            case "refusee": return "Super PDP a refusé la facture." + detail;
            case "indisponible": return "Super PDP est injoignable pour le moment." + detail;
            default: return "La fonction « facture-electronique » du serveur n'a pas pu répondre." + (detail == "" ? "\nDétail technique : " + r.refus : detail);
        }
    }
}
