using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Récapitulatif d'envoi proposé au lancement de l'application : les factures déjà
/// générées, dont la date d'envoi est atteinte, et que rien n'a encore expédiées.
///
/// Un envoi ne se rappelle pas. Rien ne part donc sans que la liste ait été montrée :
/// destinataire, montant et pièce jointe sont affichés, chaque ligne peut être écartée,
/// et « Plus tard » reste toujours disponible. C'est la contrepartie du confort — le
/// travail manuel disparaît, la relecture non.
///
/// L'application doit être ouverte pour que quoi que ce soit parte : les factures en
/// retard sont donc rattrapées, pas seulement celles du jour.
public class FactureEnvoiAuto : MonoBehaviour
{
    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// Une facture prête à partir.
    public struct Candidate
    {
        public BatimentPrefab bp;
        public Locataire loc;
        public FactureEtat rec;
        public string destinataire;
        public string pdfAbsolu;
    }

    // ── Sélection (sans UI : c'est la règle, elle se teste) ─────────────────────

    /// Une ligne part-elle ? Elle doit être **en attente d'envoi** (générée, jamais
    /// expédiée), avoir atteint sa date d'envoi, porter un PDF lisible et un
    /// destinataire. `pdfExiste` est injecté pour que la règle se teste sans disque.
    public static bool DoitPartir(string nomBatiment, Locataire loc, FactureEtat rec, DateTime aujourdhui,
                                  Func<string, bool> pdfExiste, out string destinataire,
                                  out string pdfAbsolu)
    {
        destinataire = null; pdfAbsolu = null;
        if (loc == null || rec == null) return false;

        // Seul « en attente d'envoi » part : « Envoyé » l'est déjà, « Payé » aussi, et
        // une ligne sans statut n'a pas de facture générée à joindre.
        if (rec.statut != "AttenteEnvoi") return false;
        // Revue 16c (D5-05) : refusée par la plateforme : à corriger depuis le suivi d'abord, pas à renvoyer telle quelle.
        if (rec.plateformeEtat == "refusee") return false;
        // Revue 17 (D5-05, lot 12, décision du 05/11) : préparée sans facture électronique alors qu'elle doit partir par la plateforme le jour
        // de son départ : retenue, nommée au centre à chaque lancement (ProposerAuDemarrage), à refaire quand Super PDP est actif. Avant, elle
        // partait par e-mail seul avec le PDF (facture non conforme depuis le 1/9/2027), sans un mot.
        if (FactureElectronique.ARefaire(loc, rec, aujourdhui)) return false;
        // Revue 17 (D4-01, décision du 05/11, lot 6) : préparé au nom d'un autre que le titulaire de sa période (cession saisie
        // après la préparation) : retenu ; le suivi le signale, « Refaire » le refait au même numéro au nom du titulaire.
        if (Cessions.HorsTitulaire(loc, rec)) return false;

        // La date d'envoi, c'est l'échéance moins le délai d'envoi — la même règle que
        // l'alerte URGENT. Une facture en retard part aussi : ne pas rattraper serait
        // le pire des deux mondes, ni rappel ni envoi.
        // Revue 16c (D4-07) : la règle écrite et testée (PeutPartir) : seul le loyer attend sa date d'envoi ; une régularisation,
        // une refacturation ou un dépôt partent dès qu'ils sont prêts. Une échéance illisible retient toujours (elle serait fausse).
        if (!FacturationSuivi.TryEcheance(rec.echeanceISO, out _)) return false;
        if (!FacturationSuivi.PeutPartir(rec.type, rec.echeanceISO, aujourdhui, loc)) return false;

        pdfAbsolu = FacturationSuivi.CheminPdf(rec, nomBatiment, loc.Name);
        if (string.IsNullOrEmpty(pdfAbsolu) || !(pdfExiste?.Invoke(pdfAbsolu) ?? false)) return false;

        destinataire = Destinataire(loc, rec.type, rec);
        // Revue 17 (D5-21, lot 12) : après la bascule, une facture qui part par la plateforme seule (plus de PDF par e-mail) n'a pas besoin
        // d'adresse : rien ne part d'ici (EnvoyerUne), et Notifier dit que le locataire n'a pas été prévenu. Avant, un loyer préparé pour un
        // locataire assujetti sans e-mail dormait « en attente d'envoi » pour toujours, puis passait « Impayé ».
        return ParPlateformeSeule(rec) || !string.IsNullOrWhiteSpace(destinataire);
    }

    /// Revue 17 (D5-21, lot 12) : la facture part par la plateforme seule : plus de PDF par e-mail dès la bascule, l'e-mail n'est qu'une notification.
    public static bool ParPlateformeSeule(FactureEtat rec) => FactureElectronique.PartParPlateforme(rec) && FactureElectronique.NotificationSeule;

    /// Revue 17 (D5-05, lot 12) : le SIRET de l'acheteur d'une ligne, relu comme son adresse (Destinataire) : celui de la cession de son titulaire
    /// quand ce n'est pas la fiche (vide : aucun, jamais celui de la fiche, comme FactureLoyerPanel.SiretPourPlateforme), sinon celui mémorisé sur le
    /// réglage du type (destSiret : l'acheteur du PDF), sinon la fiche — l'ordre du formulaire. Lu par FactureElectronique.ARefaire.
    public static string SiretLigne(Locataire loc, FactureEtat rec)
    {
        if (loc == null) return "";
        var t = Cessions.AcheteurDe(loc, rec) ?? Cessions.TitulaireLigne(loc, rec);
        if (t != null) return (t[1] ?? "").Trim();
        var info = loc.FactureInfoDe(rec?.type);
        return !string.IsNullOrWhiteSpace(info?.destSiret) ? info.destSiret.Trim() : (loc.siretNumber ?? "").Trim();
    }

    /// Destinataire retenu : celui mémorisé sur le réglage du type, sinon l'adresse de
    /// la fiche locataire — le même ordre que le panneau de facture.
    /// `rec` (Revue 17, D7a-03) : un loyer dont le titulaire n'est pas celui de la fiche (cession prévue ou faite) part à ce
    /// titulaire.
    public static string Destinataire(Locataire loc, string type, FactureEtat rec = null)
    {
        if (loc == null) return "";
        // Revue 17 (D4-01, décision du 05/11, lot 6) : la facture d'un autre titulaire que celui de la fiche (cédant, futur
        // cessionnaire) garde l'adresse pour laquelle elle a été préparée (rec.destinataire, sinon celle de sa cession) ; celle du
        // titulaire de la fiche suit l'adresse actuelle, ci-dessous. Lu aussi par le rappel et la notification de la plateforme.
        if (Cessions.AcheteurDe(loc, rec) is string[] acheteur) return (acheteur[3] ?? "").Trim();
        var titulaire = Cessions.TitulaireLigne(loc, rec);
        if (titulaire != null) return (titulaire[3] ?? "").Trim();
        var info = loc.FactureInfoDe(type);
        string memorise = info != null ? (info.emailDest ?? "").Trim() : "";
        return !string.IsNullOrEmpty(memorise) ? memorise : (loc.emailLocataire ?? "").Trim();
    }

    /// Toutes les factures prêtes à partir, tous bâtiments confondus.
    public static List<Candidate> Selection(IEnumerable<BatimentPrefab> bps, DateTime aujourdhui)
    {
        var res = new List<Candidate>();
        if (bps == null) return res;
        foreach (var bp in bps)
        {
            if (bp == null || bp.listLocataire == null) continue;
            foreach (var loc in bp.listLocataire)
            {
                if (loc?.facturesEtat == null) continue;
                foreach (var rec in loc.facturesEtat)
                    if (DoitPartir(bp.getName(), loc, rec, aujourdhui, File.Exists, out string dest, out string pdf)
                        // Régularisation regroupée : plusieurs lignes, UN document — il ne part qu'une fois.
                        && !res.Exists(x => x.loc == loc && x.pdfAbsolu == pdf))
                        res.Add(new Candidate { bp = bp, loc = loc, rec = rec,
                                                destinataire = dest, pdfAbsolu = pdf });
            }
        }
        return res.OrderBy(c => c.rec.echeanceISO).ToList();
    }

    // ── Étape 7 : réservation (cloud) ───────────────────────────────────────────

    /// Pourquoi une facture n'est pas partie : le serveur a refusé de la réserver.
    public static string MotifSaut(EcritureCloud.ResultatEnvoi r)
    {
        const string plusTard = " : elle repartira au prochain lancement";
        string qui = string.IsNullOrEmpty(r?.nom) ? "un collègue" : RegleVerrou.Prenom(r.nom);
        switch (r?.refus)
        {
            case "verrou": return r.motif == EcritureCloud.MotifEnvoi ? $"envoi en cours par {qui}"
                                                                       : $"en cours de modification par {qui} : elle partira dès que la fiche sera libre";
            case "envoyee": return r.statut == "Envoye" ? $"déjà envoyée par {qui}" : $"n'est plus à envoyer (modifiée par {qui})";
            case "version": return $"modifiée entre-temps par {qui}" + plusTard;
            default: return $"réservation impossible ({r?.motif})" + plusTard;
        }
    }

    /// Sautée parce que sa fiche est en cours de modification : son envoi, déjà confirmé, attend
    /// que la fiche soit libre (décision du 16/10). En cours d'envoi chez un collègue : il s'en charge.
    public static bool AttendFiche(EcritureCloud.ResultatEnvoi r)
        => r?.refus == "verrou" && r.motif != EcritureCloud.MotifEnvoi;

    /// Bilan de l'envoi, ou null si tout est parti (le message du bas suffit alors).
    /// Revue 17 (D6-20, coutures, reprise) : `notes` = ce qu'il faut dire des factures parties quand même (un justificatif introuvable,
    /// non joint) : il y a quelque chose à refaire, donc un bilan au centre même quand tout est parti. Avant : un message du bas, qui disparaît seul.
    public static string Bilan(int ok, int echecs, string erreur, IList<string> sautees, string notes = "")
    {
        notes = (notes ?? "").Trim();
        if (echecs == 0 && sautees.Count == 0 && notes == "") return null;
        string tete = $"{ok} facture(s) envoyée(s)"
            + (sautees.Count > 0 ? $", {sautees.Count} sautée(s)" : "")
            + (echecs > 0 ? $", {echecs} en échec" : "")
            + (sautees.Count > 0 ? " :" : ".");
        return tete + string.Concat(sautees.Select(s => "\n• " + s))
            + (echecs > 0 ? $"\n\n{erreur} Les factures non parties restent en attente d'envoi." : "")
            + (notes != "" ? "\n\n" + notes : "");
    }

    /// La même facture sur les fiches affichées maintenant : une relecture (changement d'un
    /// collègue) a pu remplacer les objets depuis l'ouverture de la fenêtre. Le destinataire est
    /// relu sur la fiche à jour (il a pu changer pendant l'attente). Null si elle n'y est plus.
    public static Candidate? Retrouver(Candidate c, IEnumerable<BatimentPrefab> bps)
    {
        if (bps == null || c.loc == null || c.rec == null) return null;
        foreach (var bp in bps)
        {
            if (bp == null || bp.listLocataire == null) continue;
            var loc = bp.listLocataire.FirstOrDefault(l => l != null && l.id == c.loc.id);
            var rec = loc?.facturesEtat?.FirstOrDefault(r => r.key == c.rec.key);
            if (rec == null) continue;
            string dest = Destinataire(loc, rec.type, rec);
            return new Candidate { bp = bp, loc = loc, rec = rec, pdfAbsolu = c.pdfAbsolu,
                                   destinataire = string.IsNullOrWhiteSpace(dest) ? c.destinataire : dest };
        }
        return null;
    }

    // ── Proposition au lancement ────────────────────────────────────────────────

    /// À appeler une fois les bâtiments chargés. Ne fait rien s'il n'y a rien à
    /// envoyer, si l'email n'est pas configuré (sauf les factures qui partent par la plateforme seule, D5-21, lot 12),
    /// ou si l'envoi a déjà été reporté aujourd'hui — relancer l'application dix fois ne doit pas poser dix questions.
    /// Les factures retenues parce qu'elles doivent partir par la plateforme (D5-05) sont nommées au centre dans tous les cas.
    public static void ProposerAuDemarrage(IEnumerable<BatimentPrefab> bps)
    {
        // Étape 13b-3 : rien ne part d'un bâtiment d'exemple.
        bps = (bps ?? Enumerable.Empty<BatimentPrefab>()).Where(bp => bp != null && !BatimentExemple.Est(bp.getBatiment())).ToList();
        // Revue 17 (D5-05, lot 12, décision du 05/11) : les factures préparées sans facture électronique qui doivent partir par la plateforme sont
        // nommées au centre à chaque lancement, quel que soit le retour (lecture seule, e-mail non réglé, « Plus tard », rien d'autre à envoyer) :
        // le journal ne se voit pas. Le message vient EN DERNIER (finally), après la fenêtre d'envoi : affiché avant elle, il passait sous son voile
        // (même Canvas, SetAsLastSibling) et la confirmation « Tout envoyer » (ConfirmDialog.Show) l'effaçait (D8-02) : le jour où d'autres loyers
        // partent, il n'était pas vu.
        var retenues = Retenues(bps, DateTime.Today);
        try
        {
            if (AbonnementCloud.LectureSeule)   // étape 9c1 : envoyer change le statut des factures
            {
                Debug.Log("[EnvoiAuto] Rien n'est proposé : abonnement expiré, lecture seule.");
                return;
            }
            string manque = EmailService.CeQuiManque();
            if (ReporteAujourdhui())
            {
                Debug.Log("[EnvoiAuto] Envoi déjà reporté aujourd'hui — rien n'est reproposé.");
                return;
            }

            var liste = Selection(bps, DateTime.Today);
            // Revue 17 (D5-21, lot 12) : sans e-mail réglé, seules les factures qui partent par la plateforme seule sont proposées (rien ne part d'ici
            // pour elles ; la notification manquante est dite par Notifier). Avant, cette porte retenait tout, y compris ce que la plateforme devait envoyer.
            if (manque != null)
            {
                liste = liste.Where(c => ParPlateformeSeule(c.rec)).ToList();
                if (liste.Count == 0) { Debug.Log("[EnvoiAuto] Rien n'est proposé : " + manque); return; }
            }
            if (liste.Count == 0)
            {
                // Sans ce compte rendu, une fenêtre absente est indiscernable d'une panne :
                // on ne sait pas si rien n'est dû, ou si quelque chose bloque.
                Debug.Log("[EnvoiAuto] Aucune facture à envoyer aujourd'hui. " + Diagnostic(bps));
                return;
            }

            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("FactureEnvoiAuto", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            go.AddComponent<FactureEnvoiAuto>().Construire(liste);
        }
        finally { if (retenues.Count > 0) ConfirmDialog.Erreur(MessageRetenues(retenues)); }
    }

    /// Pourquoi rien n'est proposé : pour chaque facture en attente d'envoi, ce qui la
    /// retient. Une facture prête mais sans destinataire ou sans PDF dormirait sinon
    /// sans rien dire — et le plus souvent, sa date d'envoi n'est simplement pas
    /// encore arrivée.
    static string Diagnostic(IEnumerable<BatimentPrefab> bps)
    {
        var raisons = new List<string>();
        if (bps == null) return "Aucun bâtiment chargé.";
        foreach (var bp in bps)
        {
            if (bp?.listLocataire == null) continue;
            foreach (var loc in bp.listLocataire)
            {
                if (loc?.facturesEtat == null) continue;
                foreach (var rec in loc.facturesEtat)
                {
                    if (rec.statut != "AttenteEnvoi") continue;
                    var envoi = FacturationSuivi.DateEnvoi(rec.type, rec.echeanceISO, loc);
                    string pdf = FacturationSuivi.CheminPdf(rec, bp.getName(), loc.Name);
                    string quoi = $"{loc.Name} / {rec.libelle}";
                    if (envoi.HasValue && DateTime.Today < envoi.Value)
                        raisons.Add($"{quoi} : à envoyer le {envoi.Value:dd/MM/yyyy}");
                    else if (string.IsNullOrEmpty(pdf) || !File.Exists(pdf))
                        raisons.Add($"{quoi} : PDF introuvable");
                    else if (!ParPlateformeSeule(rec) && string.IsNullOrWhiteSpace(Destinataire(loc, rec.type, rec)))   // Revue 17 (D5-21, lot 12) : par la plateforme seule, pas besoin d'adresse
                        raisons.Add($"{quoi} : aucune adresse email");
                    else if (!FacturationSuivi.TryEcheance(rec.echeanceISO, out _))
                        raisons.Add($"{quoi} : échéance illisible « {rec.echeanceISO} »");
                    else if (Cessions.HorsTitulaire(loc, rec))   // Revue 17 (D4-01, lot 6) : avant « refusée par la plateforme »
                        raisons.Add($"{quoi} : préparée au nom de {rec.acheteur}, la période est à {Cessions.Titulaire(loc, rec)} : à refaire (« Refaire », même numéro)");
                    else if (FactureElectronique.ARefaire(loc, rec, DateTime.Today))   // Revue 17 (D5-05, lot 12)
                        raisons.Add($"{quoi} : à refaire (« Refaire ») quand Super PDP est actif : depuis le 1/9/2027 elle doit partir par la plateforme");
                    else
                        raisons.Add($"{quoi} : prête, mais refusée par la plateforme (à corriger depuis le suivi)");   // D4-07
                }
            }
        }
        return raisons.Count == 0
            ? "Aucune facture n'est en attente d'envoi."
            : "En attente — " + string.Join(" · ", raisons);
    }

    /// Revue 17 (D5-05, lot 12, décision du 05/11) : une ligne en attente d'envoi retenue parce qu'elle doit partir par la plateforme et n'a pas de
    /// facture électronique (FactureElectronique.ARefaire), jugée au jour de son départ : sa date d'envoi si elle n'est pas atteinte, sinon aujourd'hui
    /// (la règle du formulaire) ; nommée dès sa préparation, pour la refaire à temps.
    public static bool Retenue(Locataire loc, FactureEtat rec, DateTime aujourdhui)
    {
        if (loc == null || rec?.statut != "AttenteEnvoi") return false;
        var envoi = FacturationSuivi.DateEnvoi(rec.type, rec.echeanceISO, loc);
        return FactureElectronique.ARefaire(loc, rec, envoi > aujourdhui ? envoi.Value : aujourdhui);
    }

    /// « Locataire / libellé » des lignes retenues (Retenue), tous bâtiments confondus ; un document regroupé compte une fois.
    public static List<string> Retenues(IEnumerable<BatimentPrefab> bps, DateTime aujourdhui)
    {
        var res = new List<string>();
        foreach (var bp in bps ?? Enumerable.Empty<BatimentPrefab>())
            foreach (var loc in bp?.listLocataire ?? new List<Locataire>())
                foreach (var rec in loc?.facturesEtat ?? new List<FactureEtat>())
                    if (Retenue(loc, rec, aujourdhui)) res.Add($"{loc.Name} / {rec.libelle}");
        return res.Distinct().ToList();
    }

    /// Le message du lancement qui nomme les lignes retenues (ConfirmDialog.Erreur : il doit se voir), et dit quoi faire.
    public static string MessageRetenues(IList<string> noms) =>
        (noms.Count == 1 ? "Une facture ne partira pas à sa date :" : $"{noms.Count} factures ne partiront pas à leur date :")
        + string.Concat(noms.Select(n => "\n• " + n))
        + "\n\nDepuis le 1er septembre 2027, une facture avec TVA à un locataire qui a un SIRET doit partir par la plateforme agréée (Super PDP). "
        + "Celles-ci ont été préparées sans facture électronique (Super PDP n'était pas actif) : elles ne partent pas par e-mail seul. "
        + "Refaites-les depuis le suivi (« Refaire ») quand Super PDP est actif ; d'ici là, ce message revient à chaque lancement.";

    /// Revue 17 (D5-05, lot 12) : la ligne retenue au moment de l'envoi (refaite hors plateforme pendant que la fenêtre était ouverte) : le bilan le dit.
    const string TexteARefaire = "Depuis le 1er septembre 2027, une facture avec TVA à un locataire qui a un SIRET doit partir par la plateforme : "
        + "préparée sans facture électronique, elle ne part pas par e-mail seul. Refaites-la (« Refaire ») quand Super PDP est actif.";

    // Confort d'écran, propre au poste : il n'a rien à faire dans la sauvegarde métier.
    const string CleReport = "CIPL_EnvoiAutoReporte";

    /// Sur le cloud, « Plus tard » ne vaut que pour qui l'a dit, dans cette entreprise : un
    /// collègue qui ouvre CIPL sur le même PC voit encore la liste (retour du 16/10).
    public static string CleReportDe(string utilisateur, string entreprise)
        => string.IsNullOrEmpty(utilisateur) ? CleReport : $"{CleReport}_{utilisateur}_{entreprise}";

    static string CleReportIci => CleReportDe(SessionCloud.Active ? SessionCloud.Session?.user?.id : null,
                                              SessionCloud.Entreprise?.id);

    static bool ReporteAujourdhui()
        => PlayerPrefs.GetString(CleReportIci, "") == DateTime.Today.ToString("yyyy-MM-dd");

    static void MarquerReporte()
    {
        PlayerPrefs.SetString(CleReportIci, DateTime.Today.ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();
    }

    // ── Écran ───────────────────────────────────────────────────────────────────

    List<Candidate> _liste;
    List<Toggle> _cases;
    Button _btnEnvoyer, _btnPlusTard;
    TMP_Text _etat;
    bool _envoiEnCours;
    /// Revue 17 (D8-03) : l'envoi groupé est en cours, hors cloud aussi (lu par la garde de fermeture ; le panneau disparaît à la fin).
    public static bool EnCours => FindFirstObjectByType<FactureEnvoiAuto>() is FactureEnvoiAuto p && p._envoiEnCours;

    void Construire(List<Candidate> liste)
    {
        _liste = liste;
        UIFactory.Stretch(GetComponent<RectTransform>());
        gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        transform.SetAsLastSibling();

        var carte = UIFactory.Panel("Carte", transform, UITheme.Carte);
        UIFactory.Border(carte.gameObject);
        var crt = (RectTransform)carte.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(.5f, .5f);
        crt.pivot = new Vector2(.5f, .5f);
        crt.sizeDelta = new Vector2(860, 560);
        var v = carte.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(20, 20, 18, 18); v.spacing = 12;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        // Revue visuelle du 16/11 (V-01 ; relevé par Agathe) : le bandeau des autres fenêtres, à la couleur de l'argent.
        UIFactory.Bandeau(crt, liste.Count == 1 ? "Une facture est à envoyer" : $"{liste.Count} factures sont à envoyer", UITheme.Hex("#854F0B"));
        UIFactory.Text(carte.transform,
            "Leur date d'envoi est atteinte et elles n'ont pas encore été expédiées. "
            + "Décochez celles que vous ne voulez pas envoyer maintenant.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);

        var scroll = Defilement(carte.transform);
        _cases = new List<Toggle>();
        foreach (var c in liste) _cases.Add(Ligne(scroll, c));

        _etat = UIFactory.Text(carte.transform, "", UITheme.Role.Donnee, UITheme.TexteSecondaire);

        var boutons = UIFactory.HBox(carte.transform, 10, false, "Boutons");
        UIFactory.LE(boutons.gameObject, minH: 46);
        boutons.childAlignment = TextAnchor.MiddleRight;
        var spacer = UIFactory.Rect("Spacer", boutons.transform);
        UIFactory.LE(spacer.gameObject, flexW: 1);

        _btnPlusTard = UIFactory.Button(boutons.transform, "Plus tard", UITheme.Carte,
            UITheme.TextePrincipal, 40, UITheme.Role.Bouton, false);
        UIFactory.Border(_btnPlusTard.gameObject);
        UIFactory.LE(_btnPlusTard.gameObject, prefW: 150, flexW: 0);
        _btnPlusTard.onClick.AddListener(() => { MarquerReporte(); Destroy(gameObject); });

        _btnEnvoyer = UIFactory.Button(boutons.transform, "Tout envoyer", UITheme.Primaire,
            Color.white, 40, UITheme.Role.Bouton, true);
        UIFactory.LE(_btnEnvoyer.gameObject, prefW: 200, flexW: 0);
        _btnEnvoyer.onClick.AddListener(Confirmer);
    }

    Toggle Ligne(Transform parent, Candidate c)
    {
        var row = UIFactory.HBox(parent, 10, false, "Ligne");
        UIFactory.LE(row.gameObject, minH: 44);
        row.padding = new RectOffset(8, 8, 4, 4);

        var t = UIFactory.Toggle(row.transform, "", true);
        UIFactory.LE(t.gameObject, prefW: 34, minW: 34, flexW: 0);

        string bat = c.bp != null ? c.bp.getName() : "";
        string qui = string.IsNullOrEmpty(bat) ? c.loc.Name : $"{c.loc.Name} · {bat}";
        var txt = UIFactory.Text(row.transform,
            $"{qui}\n<size=13>{c.rec.libelle} — {Vers(c)}</size>",
            UITheme.Role.Aide, UITheme.TextePrincipal);
        UIFactory.LE(txt.gameObject, flexW: 1, minW: 260);

        float total = Contexte(c.loc, c.rec).ttc;   // tout le document, même regroupé
        var montant = UIFactory.Text(row.transform,
            total > 0f ? total.ToString("#,##0.00", Fr) + " €" : "—",
            UITheme.Role.Aide, UITheme.TextePrincipal, true, TextAlignmentOptions.Right);
        UIFactory.LE(montant.gameObject, prefW: 130, minW: 130, flexW: 0);
        return t;
    }

    Transform Defilement(Transform parent)
    {
        var srGO = UIFactory.Rect("Scroll", parent);
        var sr = srGO.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 28;
        sr.movementType = ScrollRect.MovementType.Clamped;
        UIFactory.LE(srGO.gameObject, flexH: 1, minH: 240);
        var viewport = UIFactory.Rect("Viewport", srGO);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        // 30/10 : une surface transparente capte la molette aussi dans les vides entre les sections (sinon elle se perdait sur le fond).
        if (viewport.GetComponent<Graphic>() == null) viewport.gameObject.AddComponent<Image>().color = Color.clear;
        var vb = UIFactory.VBox(viewport, 4, 0, 0, 0, 0, "Content");
        var crt = (RectTransform)vb.transform;
        crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(.5f, 1);
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        var csf = vb.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = viewport; sr.content = crt;
        return vb.transform;
    }

    // ── Envoi ───────────────────────────────────────────────────────────────────

    void Confirmer()
    {
        if (_envoiEnCours) return;
        var choisies = Choisies();
        if (choisies.Count == 0) { _etat.text = "Aucune facture sélectionnée."; return; }

        // Un email ne se rappelle pas : dernière confirmation, avec le compte exact.
        string detail = $"{choisies.Count} facture(s) vont partir immédiatement et ne pourront "
            + "pas être rappelées.\n\n"
            + string.Join("\n", choisies.Take(8).Select(c => $"• {c.loc.Name} — {Vers(c)}"))
            + (choisies.Count > 8 ? $"\n• …et {choisies.Count - 8} autre(s)" : "");

        if (ConfirmDialog.Instance != null)
            ConfirmDialog.Instance.Show("Envoyer ces factures ?", detail,
                () => StartCoroutine(Envoyer(choisies)), "Tout envoyer");
        else
            _etat.text = "Confirmation indisponible : rien n'a été envoyé.";
    }

    /// Revue 17 (D5-21, lot 12) : à qui ça part, à l'écran : l'adresse, ou la plateforme seule quand le locataire n'a pas d'e-mail.
    static string Vers(Candidate c) =>
        string.IsNullOrWhiteSpace(c.destinataire) ? "par Super PDP seulement (pas d'adresse e-mail : le locataire ne sera pas prévenu)" : c.destinataire;

    List<Candidate> Choisies()
    {
        var res = new List<Candidate>();
        for (int i = 0; i < _liste.Count && i < _cases.Count; i++)
            if (_cases[i] != null && _cases[i].isOn) res.Add(_liste[i]);
        return res;
    }

    /// Envoi en série. Chaque succès est enregistré et sauvegardé immédiatement : si
    /// l'application s'arrête au milieu, ce qui est parti reste marqué parti.
    /// Un échec laisse la ligne en attente — elle repassera au prochain lancement.
    IEnumerator Envoyer(List<Candidate> choisies)
    {
        _envoiEnCours = true;
        _btnEnvoyer.interactable = false;
        _btnPlusTard.interactable = false;

        int ok = 0, echecs = 0;
        string derniereErreur = "";
        var sautees = new List<string>();
        string notes = "";   // Revue 17 (D6-20) : justificatifs introuvables, dits après le compte des factures parties (un locataire par ligne)
        foreach (var choisie in choisies)
        {
            _etat.text = $"Envoi {ok + echecs + sautees.Count + 1}/{choisies.Count} — {choisie.loc.Name}…";
            var issue = new Issue();
            yield return EnvoyerUne(choisie, issue, this);
            if (issue.Partie && issue.Note != null) notes += $"\n{choisie.loc.Name} : {issue.Note}";
            if (issue.Partie) ok++;
            else if (issue.Refus != null)
            {
                sautees.Add($"{choisie.loc.Name} — {MotifSaut(issue.Refus)}");
                if (AttendFiche(issue.Refus)) Attente.Ajouter(choisie);
            }
            else { echecs++; derniereErreur = issue.Erreur; }
        }

        _envoiEnCours = false;
        LocataireSuiviInline.RefreshTous();

        string bilan = Bilan(ok, echecs, derniereErreur, sautees, notes);
        if (bilan == null) UndoToast.Instance?.ShowInfo($"{ok} facture(s) envoyée(s).");
        else ConfirmDialog.Erreur(bilan);
        Destroy(gameObject);
    }

    /// Ce qu'est devenue une facture : partie, sautée (refus du serveur) ou en échec d'envoi.
    /// `Note` (Revue 17, D6-20) : ce qu'il faut dire d'une facture partie quand même (un justificatif introuvable, non joint).
    class Issue { public bool Partie; public EcritureCloud.ResultatEnvoi Refus; public string Erreur, Note; }

    /// Une facture : réservée d'abord (étape 7 ; déjà envoyée par un collègue, en cours d'envoi
    /// chez lui, ou fiche prise : sautée), puis envoyée, puis son statut enregistré. Le verrou
    /// tient jusqu'à l'enregistrement, et tombe si `hote` disparaît en route.
    static IEnumerator EnvoyerUne(Candidate choisie, Issue issue, MonoBehaviour hote)
    {
        bool tenu = true;
        var reservation = EcritureCloud.ReserverEnvoi(choisie.loc, choisie.rec.key,
            () => (tenu && hote != null) || EcritureCloud.EnregistrementEnCours);
        while (!reservation.IsCompleted) yield return null;
        if (!reservation.Result.ok) { issue.Refus = reservation.Result; yield break; }
        // La fiche a pu être relue depuis l'ouverture de la fenêtre : on part de l'objet à
        // jour, que le verrou garde désormais tel quel.
        var c = (SessionCloud.Active ? Retrouver(choisie, BatimentManager.Instance?.BatimentPrefab) : null) ?? choisie;
        // Revue 17 (D5-05, lot 12) : la ligne relue (ou celle de la file d'attente, confirmée plus tôt) a pu être refaite hors plateforme
        // depuis la fenêtre : le contrôle est refait avant tout envoi ; elle reste en attente, le bilan le dit. Le verrou est rendu.
        if (FactureElectronique.ARefaire(c.loc, c.rec, DateTime.Today)) { issue.Erreur = TexteARefaire; tenu = false; yield break; }
        // Décision du 10/11 (« Attendre Super PDP » ; D5-05, D5-26) : après la bascule, une facture qui a sa facture électronique ne part pas en PDF
        // par e-mail tant que Super PDP n'est pas actif. L'état pas encore lu (clic avant la lecture du lancement, lecture ratée) est relu ici ;
        // toujours pas actif : elle reste en attente d'envoi, le bilan dit pourquoi, elle est reproposée au lancement suivant. Avant la bascule,
        // rien ne change (la « copie » part avec le PDF, plus bas).
        if (FactureElectronique.RefusLancement(c.loc, c.rec) != null && !FactureElectronique.EtatLu)
        {
            var lecture = FactureElectronique.Etat();
            while (!lecture.IsCompleted) yield return null;
        }
        if (FactureElectronique.RefusLancement(c.loc, c.rec) is string inactif) { issue.Erreur = inactif; tenu = false; yield break; }

        // Étape 18b : une facture qui part aussi par la plateforme. Avant la bascule (1/9/2027), le PDF part
        // marqué « copie » ; après, plus de PDF : la plateforme l'envoie, puis la notification (Traiter). SCI
        // plus reliée (ou pas encore relue), avant la bascule : le PDF part quand même, le locataire ne reste pas sans rien (après la bascule : retenue plus haut).
        // Revue 17 (D4-16, lot 11) : SCI connue comme déliée (état lu, non reliée) : rien ne sera transmis, le PDF part avec l'e-mail de
        // facture ordinaire, plus la « copie » qui annonce une transmission. Pas Active : faux tant que l'état n'est pas lu (l'envoi est
        // proposé avant Demarrer), et la transmission suivrait l'e-mail de facture : deux factures chez le locataire.
        // Revue 17 (D5-05, lot 12) : « par la plateforme » = sa facture électronique partira (PartParPlateforme) ; une ligne périmée (refaite
        // hors plateforme) part avec l'e-mail de facture ordinaire et le PDF, plus la « copie » qui annonçait une transmission qui n'avait pas lieu.
        bool plateforme = FactureElectronique.PartParPlateforme(c.rec) && !FactureElectronique.Deliee;
        EmailService.Envoi envoi = null;
        if (!(plateforme && FactureElectronique.NotificationSeule && FactureElectronique.Active))
        {
            var info = c.loc.FactureInfoDe(c.rec.type);
            var ctx = Contexte(c.loc, c.rec);
            var bat = c.bp != null ? c.bp.getBatiment() : null;
            // Par la plateforme : l'e-mail « copie » (modèle des Réglages, ou le texte de cette facture).
            string objet = FactureVarResolver.Resolve(plateforme ? EmailService.ObjetCopie(info?.copieObjet)
                                                                 : EmailService.ObjetFacture(info?.emailObjet), c.loc, bat, ctx);
            string corps = FactureVarResolver.Resolve(plateforme ? EmailService.CorpsCopie(info?.copieCorps)
                                                                 : EmailService.CorpsFacture(info?.emailCorps), c.loc, bat, ctx);

            // Revue 17 (D6-20, coutures ; décision du 09/11) : une refacturation part avec les justificatifs de ses charges, comme depuis
            // son formulaire ; un justificatif introuvable sur ce poste ne bloque pas l'envoi, il est dit avec le bilan (issue.Note).
            var pieces = FactureRefacPanel.PiecesDeLigne(c.loc, c.rec, bat, c.pdfAbsolu, out var absents);
            issue.Note = FactureRefacPanel.AbsentsDits(absents);
            envoi = EmailService.Envoyer(c.destinataire, objet, corps, pieces);
            while (!envoi.Termine) yield return null;
        }

        if (envoi == null || envoi.Succes)
        {
            // Revue 16c (D2-06) : la fiche a pu être rechargée pendant l'envoi (refus de fond, abandon) : le statut se
            // pose sur la fiche à jour, sinon la facture restait « en attente » et repartait au lancement suivant.
            if (SessionCloud.Active && c.bp == null) c = Retrouver(choisie, BatimentManager.Instance?.BatimentPrefab) ?? c;
            // Le PDF et le numéro existent déjà : on ne réémet rien, on constate le
            // départ. D'où la mise à jour du statut seul, sans passer par
            // FactureEmission — qui consommerait une nouvelle séquence.
            foreach (var r in FacturationSuivi.MemeFacture(c.loc, c.rec.key).DefaultIfEmpty(c.rec))
            {
                r.statut = "Envoye";
                r.dateEnvoiISO = DateTime.Today.ToString("yyyy-MM-dd");
                // Revue 17 (D4-01, lot 6) : l'adresse à laquelle le PDF est réellement parti, lignes d'avant comprises. Par la
                // plateforme seule, rien n'est parti d'ici : la ligne garde l'adresse posée à l'émission (Notifier la relit).
                if (envoi != null) r.destinataire = c.destinataire;
            }
            if (c.bp != null) c.bp.SaveAfterModifyToDoListLocataire();
            FactureElectronique.Rattraper(c.bp, c.loc);   // étape 18b : la facture électronique part aussi
            issue.Partie = true;
        }
        else issue.Erreur = envoi?.Erreur;
        tenu = false;   // le verrou est rendu une fois le statut enregistré
    }

    /// Factures confirmées mais sautées parce que leur fiche était en cours de modification
    /// (décision du 16/10) : elles partent seules dès que la fiche est libre, tant que CIPL reste
    /// ouvert sur la même entreprise. Sinon, elles sont reproposées au prochain lancement.
    class Attente : MonoBehaviour
    {
        static Attente _instance;
        readonly List<Candidate> _liste = new List<Candidate>();
        string _entreprise;
        bool _enCours;
        float _prochain;

        public static void Ajouter(Candidate c)
        {
            if (_instance == null)
            {
                var go = new GameObject("FacturesEnAttenteDeFiche");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Attente>();
            }
            if (_instance._entreprise != SessionCloud.Entreprise?.id) _instance._liste.Clear();
            _instance._entreprise = SessionCloud.Entreprise?.id;
            _instance._liste.Add(c);
        }

        void Update()
        {
            if (_enCours || _liste.Count == 0 || Time.realtimeSinceStartup < _prochain) return;
            _prochain = Time.realtimeSinceStartup + 5f;
            if (!SessionCloud.Active || SessionCloud.Entreprise?.id != _entreprise) { _liste.Clear(); return; }
            // Détenteur d'après le dernier relevé des verrous (10 s, ou tout de suite par le direct).
            int i = _liste.FindIndex(c => EcritureCloud.Detenteur(false, c.loc.id) == null);
            if (i >= 0) StartCoroutine(Envoyer(i));
        }

        IEnumerator Envoyer(int i)
        {
            _enCours = true;
            var c = _liste[i];
            var issue = new Issue();
            yield return EnvoyerUne(c, issue, this);
            _enCours = false;
            if (issue.Refus != null && AttendFiche(issue.Refus)) yield break;   // reprise entre-temps : on attend encore
            _liste.RemoveAt(i);
            LocataireSuiviInline.RefreshTous();
            // Revue 17 (D6-20, coutures, reprise) : partie sans un justificatif : à lire et à refaire, donc au centre (comme le bilan).
            if (issue.Partie && issue.Note != null) ConfirmDialog.Erreur($"Facture de {c.loc.Name} envoyée : sa fiche est libre. {issue.Note}");
            else if (issue.Partie) UndoToast.Instance?.ShowInfo($"Facture de {c.loc.Name} envoyée : sa fiche est libre.");
            else if (issue.Refus != null) UndoToast.Instance?.ShowInfo($"Facture de {c.loc.Name} : {MotifSaut(issue.Refus)}.");
            else ConfirmDialog.Erreur($"Facture de {c.loc.Name} non envoyée : {issue.Erreur} Elle reste en attente d'envoi.");
        }
    }

    /// Contexte de résolution des variables du message. La facture étant déjà générée,
    /// ses valeurs se lisent sur la ligne de suivi — inutile de refaire les calculs du
    /// panneau. La TVA est déduite du TTC au taux normal, comme à l'émission.
    public static FactureContext Contexte(Locataire loc, FactureEtat rec)
    {
        // Montant du DOCUMENT : une régularisation regroupée le répartit sur ses lignes.
        float ttc = FacturationSuivi.MemeFacture(loc, rec.key).DefaultIfEmpty(rec).Sum(r => r.montant);
        // Revue 17 (D4-08, coutures) : le décompte de sortie a la TVA de ses retenues soumises, au taux gardé sur sa ligne (les retenues
        // sont celles enregistrées avec lui : loc.factureDepot) ; avant : 0 € dans le rappel. Dépôt et révision : pas de TVA.
        float tvaDepot = rec.key == DepartLocataire.CleDecompte ? DepartLocataire.Calculer(0f, loc?.factureDepot?.retenuesSortie, null, rec.tauxTva).tva : 0f;
        float ht = rec.type == "Depot" ? ttc - tvaDepot : Tva.Ht(ttc, rec.tauxTva);   // revue 16c (D4-11) : un dépôt n'a pas de TVA ; 02/11 : au taux de l'époque (0 = exonéré)
        return new FactureContext
        {
            date = DateTime.Today,
            periode = rec.libelle ?? "",
            numero = rec.numero ?? "",
            loyerHT = ht,
            tva = ttc - ht,
            ttc = ttc,
            echeance = FacturationSuivi.TryEcheance(rec.echeanceISO, out var ech) ? ech.ToString("dd/MM/yyyy") : "",
            periodicite = rec.periodicite   // Revue 17 (lot 15) : celle gardée sur le loyer émis ({loc.periodicite}) ; vide : celle de la fiche
        };
    }
}
