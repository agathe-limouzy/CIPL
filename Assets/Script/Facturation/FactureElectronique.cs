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

    /// Une facture de loyer part-elle aussi par la plateforme ? Mode e-mail (Pennylane a son propre chemin),
    /// SCI reliée et vérifiée, locataire assujetti à la TVA et avec son SIREN (sans lui, l'annuaire ne le trouve
    /// pas : règle BR-FR-12, 30/10 ; voir SansSiret), TVA sur la facture.
    public static bool Concerne(Locataire loc, float tva, string siretFormulaire = null) =>
        Active && ReglageService.Current?.modeEnvoi == ModeEnvoi.Email && Locataire.AssujettiTva(loc)
        && SocieteLocataire.Siren(SiretAcheteur(loc, siretFormulaire)).Length == 9 && tva > 0.005f;

    /// Locataire qui devrait recevoir une facture électronique, mais sans SIRET : sa facture part par e-mail
    /// seulement, et le formulaire le signale (décision du 30/10).
    public static bool SansSiret(Locataire loc, string siretFormulaire = null) =>
        Active && ReglageService.Current?.modeEnvoi == ModeEnvoi.Email && Locataire.AssujettiTva(loc)
        && SocieteLocataire.Siren(SiretAcheteur(loc, siretFormulaire)).Length != 9;

    /// L'acheteur de la facture électronique est celui du PDF : le SIRET saisi dans le formulaire quand il y en a un
    /// (facture adressée à une holding…), sinon celui de la fiche (revue 16c, D5-08 ; décision du 01/11).
    public static string SiretAcheteur(Locataire loc, string siretFormulaire) =>
        string.IsNullOrWhiteSpace(siretFormulaire) ? (loc?.siretNumber ?? "") : siretFormulaire.Trim();

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
        // Transférée d'une autre entreprise : déclarée en son nom (BG7) ; sans son identifiant (marque d'avant) : lecture seule.
        if (!string.IsNullOrEmpty(r.plateformeOrigine) && string.IsNullOrEmpty(r.plateformeOrigineId)) return false;
        var e = FacturationSuivi.EtatDe(r);
        return (!string.IsNullOrEmpty(r.plateformeDonnees) && string.IsNullOrEmpty(r.plateformeId)
                && r.plateformeEtat != "refusee" && r.plateformeEtat != "perimee" && Emise(e))
            || (!string.IsNullOrEmpty(r.plateformeId) && !r.encaisseeDeclaree && e == FacturationSuivi.Etat.Paye)
            // Revue 16c (D5-12) : la facture a quitté « Payé » après la déclaration : l'annulation (212 négatif) attend.
            || (!string.IsNullOrEmpty(r.plateformeId) && r.encaisseeDeclaree && e != FacturationSuivi.Etat.Paye);
    }

    /// Ce qui part d'ici : les lignes de la SCI ouverte si elle est reliée, et les lignes transmises par une autre
    /// entreprise (déclarées en son nom, avec son jeton), même si la SCI ouverte ne l'est pas (BG7).
    static bool ATraiter(FactureEtat r) => EnAttente(r) && (Active || !string.IsNullOrEmpty(r.plateformeOrigineId));

    static string Noms(string a, string b) => string.IsNullOrEmpty(a) ? (b ?? "") : string.IsNullOrEmpty(b) || a.Contains(b) ? a : a + ", " + b;
    static string AuNomDe(string noms) => string.IsNullOrEmpty(noms) ? "" : $" (au nom de {noms})";

    /// Un 403 sur une ligne transmise par une autre entreprise : l'appelant n'en est pas membre (BG7). Le refus nommé, sinon null.
    static Reponse RefusOrigine(Locataire loc, FactureEtat r, Reponse rep) =>
        rep?.refus == "acces" && !string.IsNullOrEmpty(r.plateformeOrigineId)
            ? new Reponse { refus = "origine", erreur = $"« {r.libelle} » de {loc?.Name} a été transmise par {r.plateformeOrigine} : seul un membre de {r.plateformeOrigine} peut déclarer son encaissement." }
            : null;

    /// Ce que déclare « Encaissée » : le total TTC de la facture (toutes ses lignes) et la date du dernier versement
    /// saisi, sinon aujourd'hui (revue 16c, D5-07 ; décision du 01/11 : pour la TVA sur les encaissements, c'est la
    /// date du versement qui fixe la période).
    public static (float montant, string dateISO) Encaissement(Locataire loc, FactureEtat r)
    {
        var lignes = FacturationSuivi.MemeFacture(loc, r.key).DefaultIfEmpty(r).ToList();
        float montant = lignes.Sum(x => x.montant);
        string date = lignes.SelectMany(x => x.versements ?? new List<Versement>()).Select(v => v.dateISO)
            .Where(d => !string.IsNullOrEmpty(d)).OrderBy(d => d).LastOrDefault();
        return (montant, string.IsNullOrEmpty(date) ? DateTime.Today.ToString("yyyy-MM-dd") : date);
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
        return !string.IsNullOrEmpty(r.plateformeDonnees) && Emise(FacturationSuivi.EtatDe(r)) ? "À renvoyer" : null;
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
            if (panne == null && loc.facturesEtat.Any(ATraiter))
            {
                var (a, b, d, c, p, n) = await Traiter(loc, bp);
                envoyees += a; encaissees += b; annulees += d; change |= c; panne = p; auNomDe = Noms(auNomDe, n);
            }
            if (change) Enregistrer(batId, loc);
            RafraichirSuivi(loc);
            var morceaux = new List<string>();
            if (envoyees > 0) morceaux.Add(envoyees == 1 ? "Facture transmise à Super PDP." : $"{envoyees} factures transmises à Super PDP.");
            if (encaissees > 0) morceaux.Add((encaissees == 1 ? "« Encaissée » déclaré à Super PDP" : $"{encaissees} « Encaissée » déclarés à Super PDP") + AuNomDe(auNomDe) + ".");
            if (annulees > 0) morceaux.Add((annulees == 1 ? "1 encaissement annulé chez Super PDP" : $"{annulees} encaissements annulés chez Super PDP")
                                           + AuNomDe(auNomDe) + (annulees == 1 ? " (facture plus payée)." : " (factures plus payées)."));
            if (panne != null) morceaux.Add(Panne(loc, panne));
            if (morceaux.Count > 0) UndoToast.Instance?.ShowInfo(string.Join(" ", morceaux));
        }
        catch (Exception e) { Debug.LogWarning("[FactureElectronique] " + e.Message); }
        finally { _enCours.Remove(loc.id); }
    }

    /// Un appel à la plateforme a échoué (01/11) : le motif est journalisé, et un REFUS de Super PDP (règle non respectée)
    /// est montré tel quel au lieu de passer pour une panne : « injoignable » cachait le vrai message, et un refus ne se
    /// répare pas tout seul au prochain lancement. Rend la phrase du résumé.
    static string Panne(Locataire loc, Reponse panne)
    {
        string motif = Motif(panne) ?? "pas de réponse";
        Debug.LogWarning($"[FactureElectronique] {loc?.Name} : {motif}");
        if (panne.refus == "origine") { ConfirmDialog.Erreur(panne.erreur); return "Déclaration impossible d'ici (voir le message)."; }
        if (panne.refus != "refusee") return "Plateforme injoignable : nouvel essai au prochain lancement.";
        ConfirmDialog.Erreur($"Super PDP a refusé une déclaration pour {loc?.Name}.\nDétail technique : {panne.erreur}\n\n"
                             + "Nouvel essai au prochain lancement ; si le refus se répète, signalez-le.");
        return "Déclaration refusée par Super PDP (voir le message).";
    }

    /// Locataires dont les factures sont en cours de transmission sur ce poste (un seul traitement à la fois).
    static readonly HashSet<string> _enCours = new HashSet<string>();

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
    /// si c'est la même facture électronique : une facture refaite ailleurs entre-temps garde la sienne.
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
            n.encaisseeDeclaree = r.encaisseeDeclaree;
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
                UndoToast.Instance?.ShowInfo("Super PDP : état non lu. Les loyers émis d'ici le prochain lancement partiront par e-mail seul.");
            }
            var bm = BatimentManager.Instance;
            if (bm == null || !bm.BatimentPrefab.Any(b => (b?.listLocataire ?? new List<Locataire>()).Any(l => l?.facturesEtat != null && l.facturesEtat.Any(ATraiter)))) return;
            var libre = await EcritureCloud.LocatairesLibres();
            if (libre == null || SessionCloud.Entreprise != e) return;
            int envoyees = 0, encaissees = 0, annulees = 0;
            string noms = "";
            Reponse panne = null; Locataire bloque = null;
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
                        if (p != null) { panne = p; bloque = loc; }
                    }
                    finally { _enCours.Remove(loc.id); }
                    if (panne != null && panne.refus != "origine") break;   // « pas membre de l'origine » n'arrête pas les autres (BG7)
                }
                if (panne != null && panne.refus != "origine") break;
            }
            // Un locataire bloqué (refus, panne) arrête la tournée : son motif est dit, le reste repartira au prochain lancement.
            string fin = panne != null ? " " + Panne(bloque, panne) : "";
            if (envoyees + encaissees + annulees > 0 || fin != "")
                UndoToast.Instance?.ShowInfo($"Super PDP : {envoyees} facture(s) transmise(s), {encaissees} « Encaissée » déclaré(s), {annulees} annulé(s){AuNomDe(noms)}.{fin}");
        }
        catch (Exception ex) { Debug.LogWarning("[FactureElectronique] Démarrage : " + ex.Message); }
    }

    /// Les lignes d'un locataire qui attendent la plateforme, une par une. Plateforme injoignable (`panne`) :
    /// on s'arrête, la ligne garde son état et repartira au prochain lancement.
    static async Task<(int envoyees, int encaissees, int annulees, bool change, Reponse panne, string auNomDe)> Traiter(Locataire loc, BatimentPrefab bp)
    {
        int envoyees = 0, encaissees = 0, annulees = 0;
        bool change = false;
        string auNomDe = "";           // les entreprises d'origine au nom desquelles on a déclaré (BG7)
        Reponse refusOrigine = null;   // « pas membre de l'origine » : dit à la fin, sans arrêter le reste du locataire
        // Revue 16c (D5-11) : l'entreprise ouverte au départ ; si elle change pendant l'attente de Super PDP, on s'arrête
        // (sinon les factures de la SCI A partaient avec la session de la SCI B).
        var e = SessionCloud.Entreprise;
        var Changee = new Reponse { refus = "changee", erreur = "entreprise changée pendant le traitement" };
        foreach (var r in loc.facturesEtat.ToList())
        {
            if (!ATraiter(r)) continue;
            if (SessionCloud.Entreprise != e) return (envoyees, encaissees, annulees, change, Changee, auNomDe);
            if (string.IsNullOrEmpty(r.plateformeId))
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
                    if (NotificationSeule) await Notifier(loc, r, bp.getBatiment());
                }
                else if (rep.refus == "refusee")
                {
                    r.plateformeEtat = "refusee"; r.plateformeErreur = rep.erreur; change = true;
                    // Revue 16c (D5-05, décision du 31/10) : après la bascule, rien n'est parti : la facture revient « en attente
                    // d'envoi » (toutes ses lignes), à corriger depuis le suivi ; l'envoi automatique ne la repropose pas telle quelle.
                    if (NotificationSeule)
                        foreach (var m in FacturationSuivi.MemeFacture(loc, r.key).DefaultIfEmpty(r)) m.statut = "AttenteEnvoi";
                    Debug.LogWarning($"[FactureElectronique] {loc.Name} · {r.libelle} : refusée par Super PDP : {rep.erreur}");
                    ConfirmDialog.Erreur($"Super PDP a refusé la facture « {r.libelle} » de {loc.Name}"
                                         + (NotificationSeule ? " : elle n'est pas partie, le locataire n'a rien reçu. Elle revient « en attente d'envoi » : corrigez-la depuis le suivi (Corriger)."
                                                              : " (le PDF est bien parti par e-mail).")
                                         + "\nDétail technique : " + rep.erreur);
                }
                else
                {
                    if (r.plateformeEtat != "a_renvoyer") { r.plateformeEtat = "a_renvoyer"; change = true; }
                    return (envoyees, encaissees, annulees, change, rep, auNomDe);
                }
            }
            var etat = FacturationSuivi.EtatDe(r);
            if (!string.IsNullOrEmpty(r.plateformeId) && !r.encaisseeDeclaree && etat == FacturationSuivi.Etat.Paye)
            {
                var (montant, date) = Encaissement(loc, r);   // revue 16c, D5-07 : montant TTC et date du versement
                if (SessionCloud.Entreprise != e) return (envoyees, encaissees, annulees, change, Changee, auNomDe);
                var rep = await Appeler(new Demande { action = "encaisser", entreprise = r.plateformeOrigineId, id = r.plateformeId, montant = Montant(montant), date = date });
                if (!rep.ok) { if (RefusOrigine(loc, r, rep) is Reponse ro) { refusOrigine = ro; continue; } return (envoyees, encaissees, annulees, change, rep, auNomDe); }
                r.encaisseeDeclaree = true; r.encaisseeMontant = montant; r.encaisseeDateISO = date; encaissees++; change = true;
                auNomDe = Noms(auNomDe, r.plateformeOrigine);
            }
            else if (!string.IsNullOrEmpty(r.plateformeId) && r.encaisseeDeclaree && etat != FacturationSuivi.Etat.Paye)
            {
                // Revue 16c (D5-12, décision du 31/10) : la facture a quitté « Payé » : l'encaissement déclaré est annulé par un
                // second 212 du même montant en négatif (cas d'usage officiel « annulation d'encaissement »), daté du jour.
                float montant = r.encaisseeMontant > 0f ? r.encaisseeMontant : Encaissement(loc, r).montant;
                if (SessionCloud.Entreprise != e) return (envoyees, encaissees, annulees, change, Changee, auNomDe);
                var rep = await Appeler(new Demande { action = "encaisser", entreprise = r.plateformeOrigineId, id = r.plateformeId, montant = Montant(-montant), date = DateTime.Today.ToString("yyyy-MM-dd") });
                if (!rep.ok) { if (RefusOrigine(loc, r, rep) is Reponse ro) { refusOrigine = ro; continue; } return (envoyees, encaissees, annulees, change, rep, auNomDe); }
                r.encaisseeDeclaree = false; r.encaisseeMontant = 0f; r.encaisseeDateISO = ""; change = true;
                auNomDe = Noms(auNomDe, r.plateformeOrigine);
                annulees++;   // dit dans le résumé (un message à part était recouvert par le suivant)
            }
        }
        return (envoyees, encaissees, annulees, change, refusOrigine, auNomDe);
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
