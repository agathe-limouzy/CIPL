using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Étape 9c1 : l'abonnement du groupe de l'entreprise ouverte, lu sur le serveur à l'ouverture et
/// à chaque passage dans Réglages. Expiré : lecture seule pour tous (refus au centre, bandeau sous
/// les onglets) ; le serveur refuse de toute façon l'écriture (etape9c1_supabase.sql).
public static class AbonnementCloud
{
    [Serializable]
    public class Lu
    {
        public string statut, fin_essai, aujourdhui, admin;
        public int licences_max, licences_utilisees;
        public UtilisateursCloud.Entreprise[] entreprises;
        public bool abonne, resilie;      // étape 9c2 : abonnement Stripe en cours, résilié à l'échéance
        public string formule, echeance;  // mensuel | annuel ; prochaine échéance (YYYY-MM-DD)
    }

    [Serializable] class PEntreprise { public string p_entreprise; }
    [Serializable] class PAjout { public string p_entreprise, p_nom, p_type, p_siret; }
    [Serializable] public class Ajout { public bool ok; public string refus, id, nom, erreur; }

    /// Null tant qu'il n'est pas lu (ou illisible) : rien n'est alors bloqué ici, le serveur décide.
    public static Lu Courant { get; private set; }

    static DateTime Jour(string iso) => (CloudClient.Date(iso) ?? DateTime.Today).Date;
    public static DateTime FinEssai => Jour(Courant?.fin_essai);
    public static RegleAbonnement.Etat Etat =>
        Courant == null ? RegleAbonnement.Etat.Actif : RegleAbonnement.Lire(Courant.statut, FinEssai, Jour(Courant.aujourdhui));
    public static int JoursRestants => Courant == null ? 0 : RegleAbonnement.JoursRestants(FinEssai, Jour(Courant.aujourdhui));
    public static DateTime Aujourdhui => Jour(Courant?.aujourdhui);
    public static DateTime? Echeance => string.IsNullOrEmpty(Courant?.echeance) ? (DateTime?)null : Jour(Courant.echeance);

    static bool Admin => SessionCloud.Entreprise?.EstAdmin == true;
    public static bool LectureSeule => SessionCloud.Active && SessionCloud.Entreprise != null && !RegleAbonnement.PeutEcrire(Etat);
    public static string Refus => RegleAbonnement.Refus(Admin, Courant?.admin);

    /// Null si lu, sinon le motif.
    public static async Task<string> Lire(EntrepriseCloud e)
    {
        Courant = null;
        if (e == null) return "Aucune entreprise ouverte.";
        var r = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "abonnement",
                                           new PEntreprise { p_entreprise = e.id });
        if (!r.Ok) return r.Erreur;
        try { Courant = JsonUtility.FromJson<Lu>(r.Corps); }
        catch (Exception ex) { return "Réponse illisible : " + ex.Message; }
        Bandeau.Assurer();
        return null;
    }

    /// Appelé par CloudClient à chaque refus : l'abonnement a expiré pendant la session (le serveur
    /// l'a dit en refusant une écriture). Le bandeau apparaît sans attendre la prochaine lecture.
    public static void Constater(string erreur)
    {
        if (Courant == null || erreur == null || !erreur.Contains("abonnement expiré")) return;
        Courant.statut = "expire";
        Bandeau.Assurer();
    }

    public static async Task<Ajout> Ajouter(string nom, string type, string siret)
    {
        var r = await CloudClient.Fonction(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "ajouter_entreprise",
            new PAjout { p_entreprise = SessionCloud.Entreprise?.id, p_nom = nom, p_type = type, p_siret = siret });
        if (!r.Ok) return new Ajout { refus = "erreur", erreur = r.Erreur };
        try { return JsonUtility.FromJson<Ajout>(r.Corps) ?? new Ajout { refus = "erreur", erreur = "Réponse illisible." }; }
        catch (Exception ex) { return new Ajout { refus = "erreur", erreur = ex.Message }; }
    }

    // ── Étape 9c2 : paiement avec Stripe (fonction serveur abonnement-stripe) ───

    [Serializable] class PStripe { public string action, entreprise, formule; public int licences; }
    [Serializable] class RStripe { public string url, erreur; public bool ok; }

    /// La dernière page Stripe ouverte (« Rouvrir la page de paiement »).
    public static string DerniereUrl { get; private set; }

    static async Task<(RStripe rep, string erreur)> Serveur(string action, string formule, int licences)
    {
        var r = await CloudClient.FonctionServeur(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "abonnement-stripe",
            JsonUtility.ToJson(new PStripe { action = action, entreprise = SessionCloud.Entreprise?.id, formule = formule, licences = licences }));
        RStripe rep = null;
        try { rep = JsonUtility.FromJson<RStripe>(r.Corps); } catch (Exception) { /* pas du JSON : r.Erreur dit pourquoi */ }
        if (!string.IsNullOrEmpty(rep?.erreur)) return (rep, rep.erreur);
        if (!r.Ok) return (rep, r.Erreur);
        // Ni adresse, ni « ok », ni erreur : ce n'est pas la fonction de CIPL qui a répondu (retour du
        // 22/10 : le modèle « Hello » de Supabase était resté déployé). Le début de la réponse le dit.
        if (rep == null || (string.IsNullOrEmpty(rep.url) && !rep.ok))
            return (rep, "réponse inattendue de la fonction abonnement-stripe : " + Extrait(r.Corps));
        return (rep, null);
    }

    static string Extrait(string corps) =>
        string.IsNullOrEmpty(corps) ? "(vide)" : corps.Length > 200 ? corps.Substring(0, 200) + "…" : corps;

    static async Task<string> OuvrirPage(string action, string formule, int licences)
    {
        var (rep, erreur) = await Serveur(action, formule, licences);
        if (erreur != null) return erreur;
        if (string.IsNullOrEmpty(rep?.url)) return "réponse du serveur sans adresse.";
        DerniereUrl = rep.url;
        Application.OpenURL(rep.url);
        return null;
    }

    /// Ouvre la page de paiement Stripe dans le navigateur. Null si elle s'est ouverte, sinon le motif.
    public static Task<string> Payer(bool annuel, int licences) => OuvrirPage("choisir", annuel ? "annuel" : "mensuel", licences);

    /// « Paiement et factures » : le portail client Stripe (moyen de paiement, factures, résiliation).
    public static Task<string> Portail() => OuvrirPage("portail", null, 0);

    public static async void OuvrirPortail()
    {
        string erreur = await Portail();
        if (erreur != null) ConfirmDialog.Erreur("« Paiement et factures » impossible à ouvrir : " + erreur);
    }

    /// Résilier à l'échéance, ou revenir dessus (retour du 22/10). Null si fait, sinon le motif.
    public static async Task<string> Resilier(bool resilier) => (await Serveur(resilier ? "resilier" : "reprendre", null, 0)).erreur;

    /// « Reprendre l'abonnement », du bandeau comme de l'onglet : d'abord une confirmation (retour du 22/10).
    public static void ConfirmerReprise() =>
        ConfirmDialog.Instance?.Show("Reprendre l'abonnement ?", RegleAbonnement.TexteReprise(Echeance), Reprendre, "Reprendre");

    /// La résiliation est annulée, puis tout est relu (bandeau, Réglages s'ils sont ouverts).
    static async void Reprendre()
    {
        string erreur = await Resilier(false);
        if (erreur != null) { ConfirmDialog.Erreur("Reprise impossible : " + erreur); return; }
        UndoToast.Instance?.ShowInfo("Abonnement repris.");
        await Lire(SessionCloud.Entreprise);
        if (ReglagePanel.Instance != null && ReglagePanel.Instance.isActiveAndEnabled) ReglagePanel.Instance.Rafraichir();
    }

    /// Licences (0 : inchangées) et nombre d'entreprises, au prorata. Null si fait, sinon le motif.
    public static async Task<string> Ajuster(int licences) => (await Serveur("ajuster", null, licences)).erreur;

    public static string Motif(Ajout a) =>
        a?.refus == "siret" ? "Le SIRET compte 14 chiffres."
      : a?.refus == "nom" ? "Saisissez la raison sociale."
      : a?.refus == "deja_sur_cipl" ? "Cette société est déjà sur CIPL. Si elle n'est pas dans votre groupe, demandez une invitation à son administrateur."
      : a?.refus == "abonnement" ? "Abonnement expiré : aucune entreprise ne peut être ajoutée."
      : "Ajout impossible : " + (a?.erreur ?? "réponse vide.");

    // ── Bandeau sous la barre d'onglets (maquette du 19/10) ────────────────────

    class Bandeau : MonoBehaviour
    {
        static Bandeau _instance;
        GameObject _vue;
        TMP_Text _texte;
        string _sorte;

        public static void Assurer()
        {
            if (_instance != null) return;
            var go = new GameObject("Abonnement");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Bandeau>();
        }

        void Update()
        {
            bool ouverte = SessionCloud.Active && SessionCloud.Entreprise != null;
            bool expire = LectureSeule;
            bool refus = ouverte && RegleAbonnement.BandeauRefus(Etat, Admin);   // étape 9c2
            bool resilie = ouverte && RegleAbonnement.BandeauResilie(Etat, Courant?.resilie == true, Echeance, Aujourdhui);   // 22/10
            bool essai = ouverte && RegleAbonnement.BandeauEssai(Etat, JoursRestants);
            // Résilié : information dès la résiliation, avertissement les 7 derniers jours (couleur différente).
            string niveau = resilie && RegleAbonnement.AlerteResilie(Echeance.Value, Aujourdhui) ? "alerte" : "";
            string sorte = expire ? "expire" + Admin : refus ? "refus" : resilie ? "resilie" + niveau + Admin : essai ? "essai" + Admin : null;   // l'admin seul a le bouton
            var barre = HorsLigne.BarreOnglets();
            if (sorte != _sorte || barre == null) { if (_vue != null) Destroy(_vue); _vue = null; _sorte = sorte; }
            if (sorte == null || barre == null) return;
            if (_vue == null) Construire(barre, sorte);
            _texte.text = expire ? RegleAbonnement.TexteExpire(Admin, Courant?.admin)
                        : refus ? RegleAbonnement.TexteRefusPaiement
                        : resilie ? RegleAbonnement.TexteResilie(Echeance.Value, Aujourdhui, Admin, Courant?.admin)
                        : RegleAbonnement.TexteEssai(JoursRestants, Admin, Courant?.admin);
        }

        void Construire(RectTransform barre, string sorte)
        {
            bool corail = sorte.StartsWith("expire");
            bool info = sorte.StartsWith("resilie") && !sorte.Contains("alerte");   // résilié, plus de 7 jours avant : simple information
            Color fond = corail ? UITheme.AlerteClair : info ? UITheme.PrimaireClair : UITheme.AttentionClair;
            Color encre = corail ? UITheme.AlerteTexte : info ? UITheme.Primaire : UITheme.AttentionTexte;
            string bouton = !Admin ? null : sorte == "refus" ? "Paiement et factures" : corail ? "Choisir l'abonnement"
                          : sorte.StartsWith("resilie") ? "Reprendre l'abonnement" : "Voir l'abonnement";
            _vue = HorsLigne.NouveauBandeau(barre, "BandeauAbonnement", fond, encre, bouton, Color.white, encre, out _texte, out var b);
            if (sorte == "refus") b?.onClick.AddListener(OuvrirPortail);
            else if (sorte.StartsWith("resilie")) b?.onClick.AddListener(ConfirmerReprise);
            else b?.onClick.AddListener(() => ReglagePanel.OuvrirOnglet(ReglagePanel.OAbonnement));
        }
    }
}
