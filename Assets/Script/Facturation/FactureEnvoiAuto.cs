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
    public static bool DoitPartir(Locataire loc, FactureEtat rec, DateTime aujourdhui,
                                  Func<string, bool> pdfExiste, out string destinataire,
                                  out string pdfAbsolu)
    {
        destinataire = null; pdfAbsolu = null;
        if (loc == null || rec == null) return false;

        // Seul « en attente d'envoi » part : « Envoyé » l'est déjà, « Payé » aussi, et
        // une ligne sans statut n'a pas de facture générée à joindre.
        if (rec.statut != "AttenteEnvoi") return false;

        // La date d'envoi, c'est l'échéance moins le délai d'envoi — la même règle que
        // l'alerte URGENT. Une facture en retard part aussi : ne pas rattraper serait
        // le pire des deux mondes, ni rappel ni envoi.
        if (!FacturationSuivi.TryEcheance(rec.echeanceISO, out var ech)) return false;
        if (aujourdhui.Date < ech.AddDays(-FacturationSuivi.EnvoiAvantJours).Date) return false;

        pdfAbsolu = FacturationSuivi.CheminPdf(rec);
        if (string.IsNullOrEmpty(pdfAbsolu) || !(pdfExiste?.Invoke(pdfAbsolu) ?? false)) return false;

        destinataire = Destinataire(loc, rec.type);
        return !string.IsNullOrWhiteSpace(destinataire);
    }

    /// Destinataire retenu : celui mémorisé sur le réglage du type, sinon l'adresse de
    /// la fiche locataire — le même ordre que le panneau de facture.
    public static string Destinataire(Locataire loc, string type)
    {
        if (loc == null) return "";
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
                    if (DoitPartir(loc, rec, aujourdhui, File.Exists, out string dest, out string pdf)
                        // Régularisation regroupée : plusieurs lignes, UN document — il ne part qu'une fois.
                        && !res.Exists(x => x.loc == loc && x.pdfAbsolu == pdf))
                        res.Add(new Candidate { bp = bp, loc = loc, rec = rec,
                                                destinataire = dest, pdfAbsolu = pdf });
            }
        }
        return res.OrderBy(c => c.rec.echeanceISO).ToList();
    }

    // ── Proposition au lancement ────────────────────────────────────────────────

    /// À appeler une fois les bâtiments chargés. Ne fait rien s'il n'y a rien à
    /// envoyer, si l'email n'est pas configuré, ou si l'envoi a déjà été reporté
    /// aujourd'hui — relancer l'application dix fois ne doit pas poser dix questions.
    public static void ProposerAuDemarrage(IEnumerable<BatimentPrefab> bps)
    {
        string manque = EmailService.CeQuiManque();
        if (manque != null)
        {
            Debug.Log("[EnvoiAuto] Rien n'est proposé : " + manque);
            return;
        }
        if (ReporteAujourdhui())
        {
            Debug.Log("[EnvoiAuto] Envoi déjà reporté aujourd'hui — rien n'est reproposé.");
            return;
        }

        var liste = Selection(bps, DateTime.Today);
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
                    var envoi = FacturationSuivi.DateEnvoi(rec.type, rec.echeanceISO);
                    string pdf = FacturationSuivi.CheminPdf(rec);
                    string quoi = $"{loc.Name} / {rec.libelle}";
                    if (envoi.HasValue && DateTime.Today < envoi.Value)
                        raisons.Add($"{quoi} : à envoyer le {envoi.Value:dd/MM/yyyy}");
                    else if (string.IsNullOrEmpty(pdf) || !File.Exists(pdf))
                        raisons.Add($"{quoi} : PDF introuvable");
                    else if (string.IsNullOrWhiteSpace(Destinataire(loc, rec.type)))
                        raisons.Add($"{quoi} : aucune adresse email");
                    else
                        raisons.Add($"{quoi} : échéance illisible « {rec.echeanceISO} »");
                }
            }
        }
        return raisons.Count == 0
            ? "Aucune facture n'est en attente d'envoi."
            : "En attente — " + string.Join(" · ", raisons);
    }

    // Confort d'écran, propre au poste : il n'a rien à faire dans la sauvegarde métier.
    const string CleReport = "CIPL_EnvoiAutoReporte";

    static bool ReporteAujourdhui()
        => PlayerPrefs.GetString(CleReport, "") == DateTime.Today.ToString("yyyy-MM-dd");

    static void MarquerReporte()
    {
        PlayerPrefs.SetString(CleReport, DateTime.Today.ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();
    }

    // ── Écran ───────────────────────────────────────────────────────────────────

    List<Candidate> _liste;
    List<Toggle> _cases;
    Button _btnEnvoyer, _btnPlusTard;
    TMP_Text _etat;
    bool _envoiEnCours;

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

        UIFactory.Text(carte.transform,
            liste.Count == 1 ? "Une facture est à envoyer" : $"{liste.Count} factures sont à envoyer",
            UITheme.Role.Section, UITheme.TextePrincipal, true);
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
            UITheme.TextePrincipal, 44, UITheme.Role.Bouton, false);
        UIFactory.Border(_btnPlusTard.gameObject);
        UIFactory.LE(_btnPlusTard.gameObject, prefW: 150, flexW: 0);
        _btnPlusTard.onClick.AddListener(() => { MarquerReporte(); Destroy(gameObject); });

        _btnEnvoyer = UIFactory.Button(boutons.transform, "Tout envoyer", UITheme.Primaire,
            Color.white, 44, UITheme.Role.Bouton, true);
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
            $"{qui}\n<size=13>{c.rec.libelle} — {c.destinataire}</size>",
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
            + string.Join("\n", choisies.Take(8).Select(c => $"• {c.loc.Name} — {c.destinataire}"))
            + (choisies.Count > 8 ? $"\n• …et {choisies.Count - 8} autre(s)" : "");

        if (ConfirmDialog.Instance != null)
            ConfirmDialog.Instance.Show("Envoyer ces factures ?", detail,
                () => StartCoroutine(Envoyer(choisies)), "Tout envoyer");
        else
            _etat.text = "Confirmation indisponible : rien n'a été envoyé.";
    }

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
        foreach (var c in choisies)
        {
            _etat.text = $"Envoi {ok + echecs + 1}/{choisies.Count} — {c.loc.Name}…";

            var info = c.loc.FactureInfoDe(c.rec.type);
            var ctx = Contexte(c.loc, c.rec);
            var bat = c.bp != null ? c.bp.getBatiment() : null;
            string objet = FactureVarResolver.Resolve(
                FacturePdfService.Texte(info?.emailObjet, EmailService.ObjetDefaut), c.loc, bat, ctx);
            string corps = FactureVarResolver.Resolve(
                FacturePdfService.Texte(info?.emailCorps, EmailService.CorpsDefaut), c.loc, bat, ctx);

            var envoi = EmailService.Envoyer(c.destinataire, objet, corps, new[] { c.pdfAbsolu });
            while (!envoi.Termine) yield return null;

            if (envoi.Succes)
            {
                // Le PDF et le numéro existent déjà : on ne réémet rien, on constate le
                // départ. D'où la mise à jour du statut seul, sans passer par
                // FactureEmission — qui consommerait une nouvelle séquence.
                foreach (var r in FacturationSuivi.MemeFacture(c.loc, c.rec.key).DefaultIfEmpty(c.rec))
                {
                    r.statut = "Envoye";
                    r.dateEnvoiISO = DateTime.Today.ToString("yyyy-MM-dd");
                }
                if (c.bp != null) c.bp.SaveAfterModifyToDoListLocataire();
                ok++;
            }
            else { echecs++; derniereErreur = envoi.Erreur; }
        }

        _envoiEnCours = false;
        LocataireSuiviInline.RefreshTous();

        if (echecs == 0) UndoToast.Instance?.ShowInfo($"{ok} facture(s) envoyée(s).");
        else ConfirmDialog.Erreur($"{ok} envoyée(s), {echecs} en échec — {derniereErreur} "
              + "Les factures non parties restent en attente d'envoi.");
        Destroy(gameObject);
    }

    /// Contexte de résolution des variables du message. La facture étant déjà générée,
    /// ses valeurs se lisent sur la ligne de suivi — inutile de refaire les calculs du
    /// panneau. La TVA est déduite du TTC au taux normal, comme à l'émission.
    static FactureContext Contexte(Locataire loc, FactureEtat rec)
    {
        // Montant du DOCUMENT : une régularisation regroupée le répartit sur ses lignes.
        float ttc = FacturationSuivi.MemeFacture(loc, rec.key).DefaultIfEmpty(rec).Sum(r => r.montant);
        float ht = ttc / 1.2f;
        return new FactureContext
        {
            date = DateTime.Today,
            periode = rec.libelle ?? "",
            numero = rec.numero ?? "",
            loyerHT = ht,
            tva = ttc - ht,
            ttc = ttc
        };
    }
}
