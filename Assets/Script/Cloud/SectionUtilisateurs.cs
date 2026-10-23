using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Étape 9b : Réglages › Utilisateurs (maquette validée le 19/10), en tête de Réglages, pour un
/// admin sur le cloud. Membres et invitations en cours, rôle, entreprises accessibles, Retirer,
/// Inviter, avec le compte des licences (membres + invitations en cours).
public class SectionUtilisateurs
{
    static readonly Color Vert = new Color32(0x0F, 0x6E, 0x56, 0xFF), VertClair = new Color32(0xE1, 0xF5, 0xEE, 0xFF);
    static readonly Color EnLigneFond = new Color32(0xEA, 0xF3, 0xDE, 0xFF), EnLigneEncre = new Color32(0x27, 0x50, 0x0A, 0xFF);
    static readonly Color HorsFond = new Color32(0xF1, 0xEF, 0xE8, 0xFF), HorsEncre = new Color32(0x44, 0x44, 0x41, 0xFF);
    static readonly Color InviteFond = new Color32(0xFA, 0xEE, 0xDA, 0xFF), InviteEncre = new Color32(0x63, 0x38, 0x06, 0xFF);

    // Largeurs des colonnes : Nom, E-mail, Rôle, Entreprises (le reste), Statut, actions.
    const float LNom = 220, LEmail = 300, LRole = 170, LStatut = 220, LActions = 230;

    readonly ReglagePanel _panel;
    readonly GameObject _section;
    readonly Transform _corps;
    UtilisateursCloud.Annuaire _a;
    int _lecture;   // la relecture la plus récente l'emporte

    public SectionUtilisateurs(ReglagePanel panel, Transform parent)
    {
        _panel = panel;
        var body = UIFactory.Section(parent, "Utilisateurs", Vert, VertClair);
        _corps = body.transform;
        _section = body.transform.parent.gameObject;
        _section.SetActive(false);
    }

    /// À chaque ouverture de Réglages, et après chaque action : visible pour un admin en ligne.
    public async void Rafraichir()
    {
        var e = SessionCloud.Entreprise;
        bool voir = SessionCloud.Active && e != null && e.EstAdmin && !HorsLigne.Actif;
        _section.SetActive(voir);
        if (!voir) return;
        int n = ++_lecture;
        Vider();
        Texte(_corps, "Lecture des utilisateurs…", UITheme.TexteSecondaire);
        var (a, erreur) = await UtilisateursCloud.LireAnnuaire();
        if (n != _lecture || _corps == null) return;
        Vider();
        if (a == null) { Texte(_corps, "Liste des utilisateurs illisible : " + erreur, UITheme.Alerte); return; }
        _a = a;
        Construire();
    }

    void Vider() { foreach (Transform c in _corps) UnityEngine.Object.Destroy(c.gameObject); }

    void Construire()
    {
        var tete = UIFactory.HBox(_corps, 10, false, "Tete");
        UIFactory.LE(tete.gameObject, minH: 40);
        ObjectifsTableau.Pastille(tete.transform, UtilisateursCloud.Licences(_a.licences_utilisees, _a.licences_max), VertClair, Vert, UITheme.Role.Donnee);
        UIFactory.LE(UIFactory.Rect("Espace", tete.transform).gameObject, flexW: 1);
        Bouton(tete.transform, "Inviter", OuvrirInvitation, principal: true);

        var titres = Ligne(entete: true);
        Cellule(titres, "Nom", LNom, UITheme.TexteSecondaire, true);
        Cellule(titres, "E-mail", LEmail, UITheme.TexteSecondaire, true);
        Cellule(titres, "Rôle", LRole, UITheme.TexteSecondaire, true);
        Cellule(titres, "Entreprises", 0, UITheme.TexteSecondaire, true);
        Cellule(titres, "Statut", LStatut, UITheme.TexteSecondaire, true);
        Cellule(titres, "", LActions, UITheme.TexteSecondaire);

        foreach (var m in _a.membres ?? new UtilisateursCloud.Membre[0]) LigneMembre(m);
        foreach (var i in _a.invitations ?? new UtilisateursCloud.Invitation[0]) LigneInvitation(i);
    }

    void LigneMembre(UtilisateursCloud.Membre m)
    {
        var row = Ligne();
        string nom = string.IsNullOrWhiteSpace(m.nom) ? "—" : m.nom;
        Cellule(row, nom, LNom, UITheme.TextePrincipal);
        Cellule(row, m.email, LEmail, UITheme.TexteSecondaire);
        if (m.moi) Cellule(row, m.role == "admin" ? "Admin (vous)" : "Utilisateur (vous)", LRole, UITheme.TextePrincipal);
        else
        {
            var dd = UIDropdown.Create(row, new List<string> { "Admin", "Utilisateur" }, new List<string> { "admin", "utilisateur" },
                                       m.role == "admin" ? 0 : 1, i => ChangerRole(m, i == 0 ? "admin" : "utilisateur"), 34);
            UIFactory.LE(dd.gameObject, prefW: LRole, minW: LRole, flexW: 0);
        }

        var ents = UIFactory.HBox(row, 6, false, "Entreprises");
        UIFactory.LE(ents.gameObject, flexW: 1, minW: 160);
        var t = UIFactory.Text(ents.transform, NomsEntreprises(m.entreprises), UITheme.Role.Donnee, UITheme.TextePrincipal);
        UIFactory.LE(t.gameObject, flexW: 1);
        if (!m.moi && (_a.entreprises?.Length ?? 0) > 1) Bouton(ents.transform, "Modifier…", () => OuvrirAcces(m));

        var statut = UIFactory.HBox(row, 0, false, "Statut");
        UIFactory.LE(statut.gameObject, prefW: LStatut, minW: LStatut, flexW: 0);
        if (m.en_ligne) ObjectifsTableau.Pastille(statut.transform, "En ligne", EnLigneFond, EnLigneEncre, UITheme.Role.Donnee);
        else ObjectifsTableau.Pastille(statut.transform, "Hors ligne", HorsFond, HorsEncre, UITheme.Role.Donnee);

        var actions = UIFactory.HBox(row, 6, false, "Actions");
        UIFactory.LE(actions.gameObject, prefW: LActions, minW: LActions, flexW: 0);
        if (!m.moi) Bouton(actions.transform, "Retirer", () => ConfirmerRetrait(m), alerte: true);
    }

    void LigneInvitation(UtilisateursCloud.Invitation i)
    {
        var row = Ligne();
        Cellule(row, "—", LNom, UITheme.TexteSecondaire);
        Cellule(row, i.email, LEmail, UITheme.TexteSecondaire);
        Cellule(row, i.role == "admin" ? "Admin" : "Utilisateur", LRole, UITheme.TextePrincipal);
        Cellule(row, NomsEntreprises(i.entreprises), 0, UITheme.TextePrincipal);

        var statut = UIFactory.HBox(row, 0, false, "Statut");
        UIFactory.LE(statut.gameObject, prefW: LStatut, minW: LStatut, flexW: 0);
        var fin = CloudClient.Date(i.expire_le);
        int jours = fin.HasValue ? Math.Max(0, (int)Math.Ceiling((fin.Value - DateTime.Now).TotalDays)) : 0;
        ObjectifsTableau.Pastille(statut.transform, $"Invitation envoyée · {jours} j", InviteFond, InviteEncre, UITheme.Role.Donnee);

        var actions = UIFactory.HBox(row, 6, false, "Actions");
        UIFactory.LE(actions.gameObject, prefW: LActions, minW: LActions, flexW: 0);
        Bouton(actions.transform, "Renvoyer", () => Renvoyer(i));
        Bouton(actions.transform, "Annuler", () => AnnulerInvitation(i));
    }

    string NomsEntreprises(string[] ids)
    {
        var noms = (ids ?? new string[0]).Select(id => _a.entreprises?.FirstOrDefault(x => x.id == id)?.nom).Where(n => n != null).ToList();
        return noms.Count == 0 ? "—" : string.Join(", ", noms);
    }

    // ── Actions ────────────────────────────────────────────────────────────────

    /// Après un refus, la fenêtre se rouvre telle qu'elle était remplie (retour du 19/10).
    void OuvrirInvitation() => OuvrirInvitation("", "utilisateur", null);

    void OuvrirInvitation(string emailAvant, string roleAvant, string[] entreprisesAvant)
    {
        TMP_InputField email = null; UIDropdown role = null;
        var cases = new List<(string id, Toggle t)>();
        _panel.Modal("Inviter un collègue", body =>
        {
            UIFactory.Text(body.transform, "Il reçoit un e-mail avec un code, valable 7 jours. La licence est réservée dès maintenant.",
                           UITheme.Role.Aide, UITheme.TexteSecondaire);
            email = _panel.ModalField(body, "E-mail", emailAvant, "prenom@entreprise.fr");
            email.contentType = TMP_InputField.ContentType.EmailAddress;
            UIFactory.Text(body.transform, "Rôle", UITheme.Role.Libelle, UITheme.TexteSecondaire);
            role = UIDropdown.Create(body.transform, new List<string> { "Utilisateur", "Admin" }, new List<string> { "utilisateur", "admin" },
                                     roleAvant == "admin" ? 1 : 0, _ => { }, 40);
            UIFactory.Text(body.transform, "Entreprises accessibles", UITheme.Role.Libelle, UITheme.TexteSecondaire);
            foreach (var x in _a.entreprises ?? new UtilisateursCloud.Entreprise[0])
                cases.Add((x.id, UIFactory.Toggle(body.transform, x.nom,
                    entreprisesAvant != null ? entreprisesAvant.Contains(x.id) : x.id == SessionCloud.Entreprise?.id)));
            return () => Inviter(email.text.Trim(), role.SelectedId, cases.Where(c => c.t.isOn).Select(c => c.id).ToArray());
        }, "Envoyer l'invitation");
    }

    async void Inviter(string email, string role, string[] entreprises)
    {
        string code = UtilisateursCloud.NouveauCode();
        var res = await UtilisateursCloud.Inviter(email, role, entreprises, code);
        if (res?.ok != true) { ConfirmDialog.Erreur(UtilisateursCloud.Motif(res), () => OuvrirInvitation(email, role, entreprises)); return; }
        await EnvoyerCode(email, code);
        Rafraichir();
    }

    async void Renvoyer(UtilisateursCloud.Invitation i)
    {
        string code = UtilisateursCloud.NouveauCode();
        var res = await UtilisateursCloud.RenvoyerInvitation(i.id, code);
        if (res?.ok != true) { ConfirmDialog.Erreur(UtilisateursCloud.Motif(res)); Rafraichir(); return; }
        await EnvoyerCode(i.email, code);
        Rafraichir();
    }

    /// L'e-mail ne part pas (SMTP non réglé, refusé) : le code est donné à l'admin, à transmettre.
    static async System.Threading.Tasks.Task EnvoyerCode(string email, string code)
    {
        string nomEntreprise = SessionCloud.Entreprise?.nom ?? "l'entreprise";
        string refus = await UtilisateursCloud.EnvoyerEmail(email, nomEntreprise, code);
        if (refus == null) UndoToast.Instance?.ShowInfo($"Invitation envoyée à {email}.");
        else ConfirmDialog.Erreur($"L'invitation est créée, mais l'e-mail n'est pas parti ({refus}). " +
                                  $"Transmettez ce code à {email} : {code} (valable 7 jours).");
    }

    async void AnnulerInvitation(UtilisateursCloud.Invitation i)
    {
        var res = await UtilisateursCloud.AnnulerInvitation(i.id);
        if (res?.ok != true) ConfirmDialog.Erreur(UtilisateursCloud.Motif(res));
        else UndoToast.Instance?.ShowInfo($"Invitation de {i.email} annulée : la licence est libérée.");
        Rafraichir();
    }

    async void ChangerRole(UtilisateursCloud.Membre m, string role)
    {
        if (role == m.role) return;
        var res = await UtilisateursCloud.ChangerRole(m.user_id, role);
        if (res?.ok != true) ConfirmDialog.Erreur(UtilisateursCloud.Motif(res));
        else UndoToast.Instance?.ShowInfo($"{Nom(m)} est maintenant {(role == "admin" ? "admin" : "utilisateur")}.");
        Rafraichir();
    }

    void OuvrirAcces(UtilisateursCloud.Membre m)
    {
        var cases = new List<(string id, Toggle t)>();
        _panel.Modal($"Entreprises de {Nom(m)}", body =>
        {
            foreach (var x in _a.entreprises ?? new UtilisateursCloud.Entreprise[0])
                cases.Add((x.id, UIFactory.Toggle(body.transform, x.nom, (m.entreprises ?? new string[0]).Contains(x.id))));
            return () => ChangerAcces(m, cases.Where(c => c.t.isOn).Select(c => c.id).ToArray());
        });
    }

    async void ChangerAcces(UtilisateursCloud.Membre m, string[] entreprises)
    {
        var res = await UtilisateursCloud.ChangerAcces(m.user_id, entreprises);
        if (res?.ok != true) ConfirmDialog.Erreur(UtilisateursCloud.Motif(res));
        Rafraichir();
    }

    void ConfirmerRetrait(UtilisateursCloud.Membre m)
    {
        ConfirmDialog.Instance?.Show($"Retirer {Nom(m)} ?",
            $"{Nom(m)} perd l'accès à toutes les entreprises du groupe, et sa licence se libère. " +
            "Si CIPL est ouvert chez elle ou lui, il se ferme sur l'entreprise dans les 30 secondes.",
            async () =>
            {
                var res = await UtilisateursCloud.Retirer(m.user_id);
                if (res?.ok != true) ConfirmDialog.Erreur(UtilisateursCloud.Motif(res));
                else UndoToast.Instance?.ShowInfo($"{Nom(m)} a été retiré(e) : la licence est libérée.");
                Rafraichir();
            }, "Retirer");
    }

    static string Nom(UtilisateursCloud.Membre m) => string.IsNullOrWhiteSpace(m.nom) ? m.email : m.nom;

    // ── Mise en page ───────────────────────────────────────────────────────────

    /// En-tête sur bande grise ; chaque ligne suivante précédée d'un filet (retour du 19/10 :
    /// « on voit pas trop les lignes »).
    Transform Ligne(bool entete = false)
    {
        if (!entete) UIFactory.LE(UIFactory.Panel("Filet", _corps, UITheme.Bordure, false).gameObject, minH: 1, prefH: 1);
        var row = UIFactory.HBox(_corps, 10, false, "Ligne");
        UIFactory.LE(row.gameObject, minH: entete ? 36 : 48);
        row.padding = new RectOffset(10, 10, 0, 0);
        row.childAlignment = TextAnchor.MiddleLeft;
        if (entete) row.gameObject.AddComponent<Image>().color = UITheme.CarteEntete;
        return row.transform;
    }

    static void Cellule(Transform row, string texte, float largeur, Color couleur, bool gras = false)
    {
        var t = UIFactory.Text(row, texte ?? "", UITheme.Role.Donnee, couleur, gras);
        if (largeur > 0) UIFactory.LE(t.gameObject, prefW: largeur, minW: largeur, flexW: 0);
        else UIFactory.LE(t.gameObject, flexW: 1, minW: 160);
    }

    static void Texte(Transform parent, string texte, Color couleur) => UIFactory.Text(parent, texte, UITheme.Role.Donnee, couleur);

    static void Bouton(Transform parent, string texte, Action clic, bool principal = false, bool alerte = false)
    {
        var b = principal ? UIFactory.Button(parent, texte, Vert, Color.white, 36, UITheme.Role.Action)
              : alerte ? UIFactory.Button(parent, texte, UITheme.AlerteClair, UITheme.AlerteTexte, 34, UITheme.Role.Action)
              : UIFactory.Button(parent, texte, UITheme.Carte, UITheme.TextePrincipal, 34, UITheme.Role.Action, false);
        if (!principal && !alerte) UIFactory.Border(b.gameObject);
        UIFactory.LargeurDuTexte(b);
        b.onClick.AddListener(() => clic());
    }
}
