using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

/// Réglages › Abonnement, pour un admin sur le cloud. Étape 9c1 (maquette du 19/10) : état
/// (essai, actif, paiement refusé, expiré), entreprises du groupe avec « Ajouter une entreprise ».
/// Étape 9c2 (maquette du 22/10) : sans abonnement, « Choisir l'abonnement » (formule, licences,
/// HT / TVA / TTC) puis la page Stripe dans le navigateur ; avec un abonnement, « - » / « + » des
/// licences au prorata et « Paiement et factures » (portail Stripe).
public class SectionAbonnement
{
    readonly ReglagePanel _panel;
    readonly GameObject _section;
    readonly Transform _corps;
    int _lecture;   // la relecture la plus récente l'emporte

    // Choix en cours, avant le paiement ; attente du retour de Stripe (relecture toutes les 5 s).
    bool _annuel;
    int _licences;
    bool _enAttente;
    int _attente;

    public SectionAbonnement(ReglagePanel panel, Transform parent)
    {
        _panel = panel;
        var body = UIFactory.Section(parent, "Abonnement", UITheme.Primaire, UITheme.PrimaireClair);
        _corps = body.transform;
        _section = body.transform.parent.gameObject;
        _section.SetActive(false);
    }

    public async void Rafraichir()
    {
        var e = SessionCloud.Entreprise;
        bool voir = SessionCloud.Active && e != null && e.EstAdmin && !HorsLigne.Actif;
        _section.SetActive(voir);
        if (!voir) return;
        int n = ++_lecture;
        Vider();
        Texte("Lecture de l'abonnement…", UITheme.TexteSecondaire);
        string erreur = await AbonnementCloud.Lire(e);
        if (n != _lecture || _corps == null) return;
        var a = AbonnementCloud.Courant;
        if (a == null) { Vider(); Texte("Abonnement illisible : " + erreur, UITheme.Alerte); return; }
        Reconstruire();
    }

    void Vider() { foreach (Transform c in _corps) UnityEngine.Object.Destroy(c.gameObject); }

    /// Sans relire le serveur (choix de la formule, des licences).
    void Reconstruire()
    {
        var a = AbonnementCloud.Courant;
        if (a == null || _corps == null) return;
        Vider();
        var etat = AbonnementCloud.Etat;
        bool alerte = etat == RegleAbonnement.Etat.Expire || etat == RegleAbonnement.Etat.PaiementRefuse;
        var tete = UIFactory.HBox(_corps, 10, false, "Etat");
        UIFactory.LE(tete.gameObject, minH: 40);
        UIFactory.Text(tete.transform, "Votre abonnement", UITheme.Role.Libelle, UITheme.TextePrincipal, true);
        ObjectifsTableau.Pastille(tete.transform,
            RegleAbonnement.Pastille(etat, AbonnementCloud.JoursRestants, a.formule, a.resilie, AbonnementCloud.Echeance),
            alerte ? UITheme.AlerteClair : etat == RegleAbonnement.Etat.Essai ? UITheme.AttentionClair : UITheme.PrimaireClair,
            alerte ? UITheme.AlerteTexte : etat == RegleAbonnement.Etat.Essai ? UITheme.AttentionTexte : UITheme.Primaire,
            UITheme.Role.Donnee);
        UIFactory.LE(UIFactory.Rect("Espace", tete.transform).gameObject, flexW: 1);
        UIFactory.Text(_corps, RegleAbonnement.Explication(etat, AbonnementCloud.FinEssai, AbonnementCloud.Echeance, a.resilie),
                       UITheme.Role.Aide, UITheme.TexteSecondaire);

        if (a.abonne) Abonne(a);
        else if (_enAttente) Attente();
        else Choix(a, etat);
    }

    // ── Sans abonnement : choisir (maquette 1) ─────────────────────────────────

    void Choix(AbonnementCloud.Lu a, RegleAbonnement.Etat etat)
    {
        int min = RegleAbonnement.LicencesMin(a.licences_utilisees);
        _licences = Math.Max(_licences > 0 ? _licences : a.licences_max, min);
        UIFactory.Text(_corps, "Choisir l'abonnement", UITheme.Role.Libelle, UITheme.TextePrincipal, true);
        var formules = UIFactory.HBox(_corps, 8, false, "Formules");
        Puce(formules.transform, "Mensuel", !_annuel, () => { _annuel = false; Reconstruire(); });
        Puce(formules.transform, $"Annuel · {12 - RegleAbonnement.MoisParAn} mois offerts", _annuel, () => { _annuel = true; Reconstruire(); });

        int n = LigneEntreprises(a);
        LigneLicences(_licences, ecart => { _licences = Math.Max(min, _licences + ecart); Reconstruire(); },
                      $"{a.licences_utilisees} utilisée{S(a.licences_utilisees)} : {min} au minimum");
        int ht = RegleAbonnement.Total(n, _licences) * (_annuel ? RegleAbonnement.MoisParAn : 1);
        decimal ttc = RegleAbonnement.Ttc(ht);
        Ligne($"1re entreprise · {n - 1} suivante{S(n - 1)} · {_licences} licence{S(_licences)}", RegleAbonnement.Euros(ht) + " HT");
        Ligne("TVA 20 %", RegleAbonnement.Euros(ttc - ht));
        Ligne("Total", $"{RegleAbonnement.Euros(ttc)} TTC {(_annuel ? "/ an" : "/ mois")}", gras: true);

        var premier = RegleAbonnement.PremierPrelevement(etat, AbonnementCloud.FinEssai, AbonnementCloud.Aujourdhui);
        UIFactory.Text(_corps, "Carte ou prélèvement SEPA, sur la page sécurisée de Stripe. " +
                       RegleAbonnement.TextePrelevement(premier, AbonnementCloud.Aujourdhui), UITheme.Role.Aide, UITheme.TexteSecondaire);
        Bouton(UIFactory.HBox(_corps, 8, false, "Payer").transform, "Continuer vers le paiement", Payer, principal: true);
    }

    async void Payer()
    {
        string erreur = await AbonnementCloud.Payer(_annuel, _licences);
        if (erreur != null) { ConfirmDialog.Erreur("La page de paiement ne s'est pas ouverte : " + erreur); return; }
        _enAttente = true;
        Reconstruire();
        Attendre(++_attente);
    }

    /// La page Stripe est ouverte : l'abonnement arrive par le serveur (stripe-webhook). On relit
    /// toutes les 5 s, pendant 10 minutes au plus ; « Actualiser » relit tout de suite.
    async void Attendre(int n)
    {
        for (int i = 0; i < 120; i++)
        {
            await Task.Delay(5000);
            if (n != _attente || !_enAttente || _corps == null || SessionCloud.Entreprise == null) return;
            await AbonnementCloud.Lire(SessionCloud.Entreprise);
            if (n != _attente || _corps == null) return;
            if (AbonnementCloud.Courant?.abonne == true)
            {
                _enAttente = false;
                UndoToast.Instance?.ShowInfo("Abonnement enregistré.");
                Reconstruire();
                return;
            }
        }
    }

    // ── Page Stripe ouverte (maquette 2) ───────────────────────────────────────

    void Attente()
    {
        UIFactory.Text(_corps, "Paiement en cours dans votre navigateur…", UITheme.Role.Libelle, UITheme.TextePrincipal, true);
        UIFactory.Text(_corps, "Revenez ici une fois le paiement validé : l'abonnement se met à jour tout seul (quelques secondes).",
                       UITheme.Role.Aide, UITheme.TexteSecondaire);
        var row = UIFactory.HBox(_corps, 8, false, "Attente").transform;
        Bouton(row, "Actualiser", Rafraichir);
        Bouton(row, "Rouvrir la page de paiement", () => { if (AbonnementCloud.DerniereUrl != null) Application.OpenURL(AbonnementCloud.DerniereUrl); });
        Bouton(row, "Revenir au choix", () => { _enAttente = false; Reconstruire(); });
    }

    // ── Abonnement en cours (maquette 3) ───────────────────────────────────────

    void Abonne(AbonnementCloud.Lu a)
    {
        bool annuel = a.formule == "annuel";
        int n = LigneEntreprises(a);
        LigneLicences(a.licences_max, ecart => ChangerLicences(a, ecart), $"{a.licences_utilisees} utilisée{S(a.licences_utilisees)}");
        int ht = RegleAbonnement.Total(n, a.licences_max) * (annuel ? RegleAbonnement.MoisParAn : 1);
        Ligne("Total", $"{RegleAbonnement.Euros(ht)} HT · {RegleAbonnement.Euros(RegleAbonnement.Ttc(ht))} TTC {(annuel ? "/ an" : "/ mois")}", gras: true);
        var row = UIFactory.HBox(_corps, 10, false, "Portail").transform;
        Bouton(row, "Paiement et factures", AbonnementCloud.OuvrirPortail);
        UIFactory.Text(row, "carte, mandat SEPA, factures (portail Stripe)", UITheme.Role.Aide, UITheme.TexteSecondaire);
        UIFactory.LE(UIFactory.Rect("Espace", row).gameObject, flexW: 1);
        // Retour du 22/10 : résilier (ou reprendre) ici, sans passer par le portail.
        if (a.resilie) Bouton(row, "Reprendre l'abonnement", AbonnementCloud.ConfirmerReprise, principal: true);
        else BoutonAlerte(row, "Résilier l'abonnement", ConfirmerResiliation);
    }

    void ConfirmerResiliation() =>
        ConfirmDialog.Instance?.Show("Résilier l'abonnement ?", RegleAbonnement.TexteResiliation(AbonnementCloud.Echeance),
                                     () => Resilier(true), "Résilier");

    async void Resilier(bool resilier)
    {
        string erreur = await AbonnementCloud.Resilier(resilier);
        if (erreur != null) ConfirmDialog.Erreur((resilier ? "Résiliation impossible : " : "Reprise impossible : ") + erreur);
        else UndoToast.Instance?.ShowInfo(resilier ? "Abonnement résilié : il prend fin à l'échéance." : "Abonnement repris.");
        Rafraichir();
    }

    void ChangerLicences(AbonnementCloud.Lu a, int ecart)
    {
        int nouveau = a.licences_max + ecart, min = RegleAbonnement.LicencesMin(a.licences_utilisees);
        if (nouveau < min)
        {
            ConfirmDialog.Erreur($"{a.licences_utilisees} licence{S(a.licences_utilisees)} utilisée{S(a.licences_utilisees)} : " +
                                 "retirez quelqu'un (onglet Utilisateurs) avant de baisser le nombre de licences.");
            return;
        }
        ConfirmDialog.Instance?.Show(ecart > 0 ? "Ajouter une licence ?" : "Retirer une licence ?",
            RegleAbonnement.TexteLicences(ecart, a.formule == "annuel"), async () =>
            {
                string erreur = await AbonnementCloud.Ajuster(nouveau);
                if (erreur != null) ConfirmDialog.Erreur("Licences inchangées : " + erreur);
                else UndoToast.Instance?.ShowInfo($"Abonnement : {nouveau} licences.");
                Rafraichir();
            }, ecart > 0 ? "Ajouter" : "Retirer");
    }

    // ── Lignes communes ────────────────────────────────────────────────────────

    /// Entreprises du groupe, avec « Ajouter une entreprise ». Renvoie leur nombre.
    int LigneEntreprises(AbonnementCloud.Lu a)
    {
        var ents = a.entreprises ?? new UtilisateursCloud.Entreprise[0];
        Bouton(Ligne("Entreprises du groupe", string.Join(", ", ents.Select(x => x.nom))), "Ajouter une entreprise", OuvrirAjout, principal: true);
        return Math.Max(1, ents.Length);
    }

    void LigneLicences(int valeur, Action<int> changer, string note)
    {
        var row = Ligne("Licences", null);
        Bouton(row, "-", () => changer(-1));
        UIFactory.Text(row, valeur.ToString(), UITheme.Role.Donnee, UITheme.TextePrincipal, true);
        Bouton(row, "+", () => changer(+1));
        UIFactory.Text(row, note, UITheme.Role.Aide, UITheme.TexteSecondaire);
    }

    static string S(int n) => n > 1 ? "s" : "";

    // ── Ajouter une entreprise (maquette du 19/10) ─────────────────────────────

    /// Après un refus, la fenêtre se rouvre telle qu'elle était remplie (retour du 19/10).
    void OuvrirAjout() => OuvrirAjout("societe", "", "");

    void OuvrirAjout(string typeAvant, string siretAvant, string nomAvant)
    {
        UIDropdown type = null; TMP_InputField siret = null, nom = null;
        bool particulier = typeAvant == "particulier";
        bool abonne = AbonnementCloud.Courant?.abonne == true;
        bool annuel = AbonnementCloud.Courant?.formule == "annuel";
        _panel.Modal("Ajouter une entreprise", body =>
        {
            UIFactory.Text(body.transform, abonne
                ? $"Elle rejoint votre groupe : votre abonnement augmente de {RegleAbonnement.PrixEntrepriseSuivante * (annuel ? RegleAbonnement.MoisParAn : 1)} € HT " +
                  $"{(annuel ? "par an" : "par mois")}, au prorata dès aujourd'hui. Vous y avez accès en tant qu'admin."
                : $"Elle rejoint votre groupe : une seule facture, {RegleAbonnement.PrixEntrepriseSuivante} € au lieu de " +
                  $"{RegleAbonnement.PrixEntreprise} €. Vous y avez accès en tant qu'admin.", UITheme.Role.Aide, UITheme.TexteSecondaire);
            UIFactory.Text(body.transform, "Vous la gérez en tant que", UITheme.Role.Libelle, UITheme.TexteSecondaire);
            GameObject[] champsSiret = null;
            TMP_Text aideParticulier = null;
            type = UIDropdown.Create(body.transform, new List<string> { "Société", "Particulier" }, new List<string> { "societe", "particulier" },
                                     particulier ? 1 : 0, i =>
            {
                foreach (var go in champsSiret) go.SetActive(i == 0);
                aideParticulier.gameObject.SetActive(i == 1);
            }, 40);
            var libelleSiret = UIFactory.Text(body.transform, "SIRET (14 chiffres)", UITheme.Role.Libelle, UITheme.TexteSecondaire);
            siret = UIFactory.Input(body.transform, "123 456 789 00012");
            siret.text = siretAvant ?? "";
            champsSiret = new[] { libelleSiret.gameObject, siret.gameObject };
            foreach (var go in champsSiret) go.SetActive(!particulier);
            nom = _panel.ModalField(body, "Nom de l'entreprise", nomAvant, "SCI Rivoli");
            aideParticulier = UIFactory.Text(body.transform, "Laissé vide : « Patrimoine de » suivi de votre nom.", UITheme.Role.Aide, UITheme.TexteSecondaire);
            aideParticulier.gameObject.SetActive(particulier);
            return () => Ajouter(nom.text.Trim(), type.SelectedId, siret.text);
        }, "Ajouter");
    }

    async void Ajouter(string nom, string type, string siret)
    {
        Action reprendre = () => OuvrirAjout(type, siret, nom);
        if (type == "societe")
        {
            string motif = RegleAbonnement.VerifierSiret(siret) ?? (nom == "" ? "Saisissez la raison sociale." : null);
            if (motif != null) { ConfirmDialog.Erreur(motif, reprendre); return; }
        }
        bool abonne = AbonnementCloud.Courant?.abonne == true;
        var res = await AbonnementCloud.Ajouter(nom, type, siret);
        if (res?.ok != true) { ConfirmDialog.Erreur(AbonnementCloud.Motif(res), reprendre); return; }
        // Étape 9c2 : l'abonnement suit le nombre d'entreprises (ligne « entreprise supplémentaire »).
        string erreur = abonne ? await AbonnementCloud.Ajuster(0) : null;
        if (erreur != null) ConfirmDialog.Erreur($"« {res.nom} » est ajoutée, mais l'abonnement n'a pas été mis à jour : {erreur}");
        else UndoToast.Instance?.ShowInfo($"« {res.nom} » ajoutée : ouvrez-la par Compte › Changer d'entreprise.");
        Rafraichir();
    }

    // ── Mise en page ───────────────────────────────────────────────────────────

    /// Une ligne « libellé … valeur » précédée d'un filet ; valeur nulle : la ligne reçoit ses propres éléments.
    Transform Ligne(string libelle, string valeur, bool gras = false)
    {
        UIFactory.LE(UIFactory.Panel("Filet", _corps, UITheme.Bordure, false).gameObject, minH: 1, prefH: 1);
        var row = UIFactory.HBox(_corps, 10, false, "Ligne");
        UIFactory.LE(row.gameObject, minH: 40);
        var l = UIFactory.Text(row.transform, libelle, UITheme.Role.Donnee, UITheme.TexteSecondaire, gras);
        UIFactory.LE(l.gameObject, flexW: 1);
        if (valeur != null) UIFactory.Text(row.transform, valeur, UITheme.Role.Donnee, UITheme.TextePrincipal, gras);
        return row.transform;
    }

    void Texte(string texte, Color couleur) => UIFactory.Text(_corps, texte, UITheme.Role.Donnee, couleur);

    static void Bouton(Transform parent, string texte, Action clic, bool principal = false)
    {
        var b = principal ? UIFactory.Button(parent, texte, UITheme.Primaire, Color.white, 36, UITheme.Role.Action)
                          : UIFactory.Button(parent, texte, UITheme.Carte, UITheme.TextePrincipal, 36, UITheme.Role.Action, false);
        if (!principal) UIFactory.Border(b.gameObject);
        UIFactory.LargeurDuTexte(b);
        b.onClick.AddListener(() => clic());
    }

    static void BoutonAlerte(Transform parent, string texte, Action clic)
    {
        var b = UIFactory.Button(parent, texte, UITheme.AlerteClair, UITheme.AlerteTexte, 36, UITheme.Role.Action);
        UIFactory.LargeurDuTexte(b);
        b.onClick.AddListener(() => clic());
    }

    /// Choix de la formule : pleine couleur si retenue.
    static void Puce(Transform parent, string texte, bool choisi, Action clic)
    {
        var b = UIFactory.Button(parent, texte, choisi ? UITheme.Primaire : UITheme.Carte, choisi ? Color.white : UITheme.TextePrincipal,
                                 34, UITheme.Role.Action, choisi);
        if (!choisi) UIFactory.Border(b.gameObject);
        UIFactory.LargeurDuTexte(b);
        b.onClick.AddListener(() => clic());
    }
}
