using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Étape 13b (maquette validée le 25/10, revue en 13b-2) : la carte « Premiers pas » en haut de
/// l'accueil. Chaque ligne se coche d'après les données ; la prochaine étape est mise en avant ;
/// « Commencer » ouvre le bon écran et, quand elle en a un, le guide pas à pas de la ligne (Bulles),
/// puis propose la suivante. « Masquer » vaut pour la personne (son compte, sur tous ses PC). L'admin
/// a 8 lignes ; un collègue, les 3 qu'il peut faire (bâtiment, locataire, facture).
public static class PremiersPas
{
    public enum Ligne { Logo, Rib, Envoi, Batiment, Locataire, Facture, Inviter, Abonnement }

    public struct Etat
    {
        public bool admin, logo, rib, envoi, facture, autrePersonne, abonne;
        public int batiments, locataires, joursEssai;
    }

    public static List<(Ligne ligne, string texte, string sous, bool fait)> Lignes(Etat e)
    {
        var l = new List<(Ligne, string, string, bool)>();
        void Ajouter(Ligne x, bool fait) => l.Add((x, Nom(x), x == Ligne.Abonnement ? (e.abonne ? "Abonné" : "Essai : " + Jours(e.joursEssai)) : Sous(x), fait));
        if (e.admin)
        {
            Ajouter(Ligne.Logo, e.logo);
            Ajouter(Ligne.Rib, e.rib);
            Ajouter(Ligne.Envoi, e.envoi);
        }
        Ajouter(Ligne.Batiment, e.batiments > 0);
        Ajouter(Ligne.Locataire, e.locataires > 0);
        Ajouter(Ligne.Facture, e.facture);
        if (e.admin)
        {
            Ajouter(Ligne.Inviter, e.autrePersonne);
            Ajouter(Ligne.Abonnement, e.abonne);
        }
        return l;
    }

    public static string Nom(Ligne x) => x switch
    {
        Ligne.Logo => "Votre logo",
        Ligne.Rib => "Vos RIB",
        Ligne.Envoi => "L'envoi des factures",
        Ligne.Batiment => "Un premier bâtiment",
        Ligne.Locataire => "Un locataire",
        Ligne.Facture => "Une première facture",
        Ligne.Inviter => "Inviter un collègue",
        _ => "Choisir l'abonnement",
    };

    static string Sous(Ligne x) => x switch
    {
        Ligne.Logo => "En haut de chaque facture · 1 min",
        Ligne.Rib => "Imprimés sur les factures · 2 min",
        Ligne.Envoi => "E-mail ou Pennylane · 3 min",
        Ligne.Batiment => "Nom et adresse · 2 min",
        Ligne.Locataire => "Bail, loyer, e-mail de facturation · 4 min",
        Ligne.Facture => "Aperçu puis émission · 2 min",
        _ => "Facultatif · 1 min",
    };

    /// Pourquoi faire la prochaine étape (dans le cadre « Prochaine étape »).
    public static string Phrase(Ligne x) => x switch
    {
        Ligne.Logo => "Votre logo s'imprime en haut de chaque facture. Le bas de page et le lieu d'émission sont déjà remplis d'après votre SIRET : vérifiez-les au passage.",
        Ligne.Rib => "Le compte où vos locataires paient : son IBAN et son BIC s'impriment sur chaque facture.",
        Ligne.Envoi => "Choisissez l'e-mail ou Pennylane : c'est par là que vos factures partiront.",
        Ligne.Batiment => "Chaque immeuble ou local a sa fiche et son onglet ; ses locataires s'y ajoutent.",
        Ligne.Locataire => "Son bail et son loyer servent à préparer chaque facture ; son e-mail, à la lui envoyer.",
        Ligne.Facture => "Vérifiez l'aperçu, puis émettez : la facture reçoit son numéro et son PDF. L'envoi se fait ensuite, quand vous le décidez.",
        Ligne.Inviter => "Un collègue travaille avec vous ? Il reçoit un code par e-mail et voit les mêmes bâtiments, en direct.",
        _ => "Mensuel ou annuel, une licence par personne et par entreprise. Le paiement se fait sur la page sécurisée de Stripe.",
    };

    static string Jours(int j) => j <= 0 ? "dernier jour" : j == 1 ? "1 jour restant" : $"{j} jours restants";

    public static string Titre(IList<(Ligne ligne, string texte, string sous, bool fait)> l) => $"Premiers pas · {l.Count(x => x.fait)} / {l.Count}";

    public static bool Visible(IList<(Ligne ligne, string texte, string sous, bool fait)> l, bool masquee) => !masquee && l.Any(x => !x.fait);

    /// La première ligne pas encore faite, dans l'ordre de la carte ; null quand tout est fait.
    public static Ligne? Prochaine(IList<(Ligne ligne, string texte, string sous, bool fait)> l)
    {
        foreach (var x in l) if (!x.fait) return x.ligne;
        return null;
    }

    /// L'état d'après les données de l'entreprise ouverte.
    public static Etat Lire()
    {
        var bats = (BatimentManager.Instance?.Batiments ?? new List<Batiment>()).Where(b => b != null && !BatimentExemple.Est(b)).ToList();
        var locs = Locataires();
        var r = ReglageService.Current;
        var e = SessionCloud.Entreprise;
        var ent = AbonnementCloud.Courant?.entreprises?.FirstOrDefault(x => x.id == e?.id);
        return new Etat
        {
            admin = e?.EstAdmin == true,
            logo = !string.IsNullOrEmpty(r?.logoPath),
            rib = r?.ribs?.Count > 0,
            envoi = r?.modeEnvoi == ModeEnvoi.Email ? EmailService.CeQuiManque() == null
                                                     : !string.IsNullOrEmpty(ReglageService.LireSecret("pennylane_api_key")),
            batiments = bats.Count,
            locataires = locs.Count,
            facture = locs.Any(l => l.facturesEtat?.Any(f => !string.IsNullOrEmpty(f.numero)) == true),
            autrePersonne = (ent?.licences_utilisees ?? 1) > 1,
            abonne = AbonnementCloud.Courant?.abonne == true,
            joursEssai = AbonnementCloud.JoursRestants,
        };
    }

    // ── La carte ───────────────────────────────────────────────────────────────

    static readonly Color Accent = Hex("#0F6E56"), Clair = Hex("#D6ECE3"), Encre = Hex("#085041"), Gris = Hex("#D3D1C7");
    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    static GameObject _carte;

    /// Construit (ou retire) la carte sous l'en-tête de l'accueil. Appelé par GeneralMenuPanel.Refresh,
    /// avant le calcul des hauteurs.
    public static void Afficher(RectTransform accueil)
    {
        if (_carte != null) { _carte.SetActive(false); UnityEngine.Object.Destroy(_carte); _carte = null; }   // inactive : hors des hauteurs
        if (accueil == null || !SessionCloud.Active) return;
        var lignes = Lignes(Lire());
        if (!Visible(lignes, SessionCloud.Retenu.cipl_premiers_pas_masques)) return;

        var fond = UIFactory.Panel("PremiersPas", accueil, UITheme.Carte);
        UIFactory.Border(fond.gameObject);
        _carte = fond.gameObject;
        var entete = accueil.Find("MenuHeader");
        _carte.transform.SetSiblingIndex(entete != null ? entete.GetSiblingIndex() + 1 : 0);
        var v = _carte.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(0, 0, 0, 10); v.spacing = 6;
        v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        var bandeau = UIFactory.HBox(_carte.transform, 8, false, "Bandeau");
        bandeau.padding = new RectOffset(14, 14, 8, 8);
        bandeau.gameObject.AddComponent<Image>().color = Clair;
        var titre = UIFactory.Text(bandeau.transform, Titre(lignes), UITheme.Role.Section, Accent, true);
        UIFactory.LE(titre.gameObject, flexW: 1);
        Lien(bandeau.transform, BatimentExemple.Trouver() == null ? "Essayer avec un exemple" : "Voir l'exemple", BatimentExemple.Ouvrir);
        Lien(bandeau.transform, "Masquer", () =>
        {
            SessionCloud.Retenir(SessionCloud.PremiersPasMasques);
            GeneralMenuPanel.Instance?.Refresh();
        });

        // Barre de progression.
        var barre = UIFactory.Panel("Progression", Marge("Marge"), Gris);
        UIFactory.LE(barre.gameObject, minH: 6, prefH: 6, flexW: 1);
        var plein = UIFactory.Panel("Fait", barre.transform, Accent);
        var prt = (RectTransform)plein.transform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = new Vector2((float)lignes.Count(x => x.fait) / lignes.Count, 1);
        prt.offsetMin = prt.offsetMax = Vector2.zero;

        // La prochaine étape, mise en avant (maquette 13b-2).
        var p = lignes.First(x => !x.fait);
        var cadre = UIFactory.HBox(Marge("Prochaine"), 12, false, "Cadre");
        cadre.padding = new RectOffset(12, 12, 8, 8);
        cadre.childAlignment = TextAnchor.MiddleLeft;
        cadre.gameObject.AddComponent<Image>().color = Clair;
        UIFactory.Border(cadre.gameObject, Accent);
        var col = UIFactory.VBox(cadre.transform, 2, name: "Texte");
        UIFactory.LE(col.gameObject, flexW: 1);
        int duree = p.sous.LastIndexOf("· ");   // la durée seule : le reste répète la ligne (vu en Play le 25/10)
        UIFactory.Text(col.transform, "Prochaine étape" + (duree < 0 ? "" : " · " + p.sous.Substring(duree + 2)), UITheme.Role.Aide, Accent);
        UIFactory.Text(col.transform, p.texte, UITheme.Role.Libelle, Encre, true);
        UIFactory.Text(col.transform, Phrase(p.ligne), UITheme.Role.Donnee, Accent);
        var go = UIFactory.Button(cadre.transform, "Commencer ›", Accent, Color.white, 36, UITheme.Role.Bouton);
        UIFactory.LargeurDuTexte(go);
        go.onClick.AddListener(() => Commencer(p.ligne));

        // Les lignes, sur deux colonnes.
        var colonnes = UIFactory.HBox(_carte.transform, 24, false, "Lignes");
        colonnes.padding = new RectOffset(14, 14, 0, 0);
        colonnes.childForceExpandWidth = true;
        int moitie = (lignes.Count + 1) / 2;
        for (int c = 0; c < 2; c++)
        {
            var colonne = UIFactory.VBox(colonnes.transform, 4, name: "Colonne");
            UIFactory.LE(colonne.gameObject, flexW: 1);
            foreach (var (ligne, texte, sous, fait) in lignes.Skip(c * moitie).Take(moitie))
            {
                var row = UIFactory.HBox(colonne.transform, 8, false, ligne.ToString());
                row.childAlignment = TextAnchor.MiddleLeft;
                var puce = UIFactory.Panel("Puce", row.transform, fait ? Accent : UITheme.Carte);
                UIFactory.LE(puce.gameObject, minW: 12, prefW: 12, minH: 12, prefH: 12);
                if (!fait) UIFactory.Border(puce.gameObject, Gris);
                var textes = UIFactory.VBox(row.transform, 0, name: "Textes");
                UIFactory.LE(textes.gameObject, flexW: 1);
                var t = UIFactory.Text(textes.transform, texte, UITheme.Role.Donnee, fait ? UITheme.TexteSecondaire : UITheme.TextePrincipal);
                if (fait) t.fontStyle = FontStyles.Strikethrough;
                else UIFactory.Text(textes.transform, sous, UITheme.Role.Aide, UITheme.TexteSecondaire);
                // Retour du 25/10 : une ligne faite garde son guide (« Refaire » : un RIB, un bâtiment… de plus).
                var l = ligne;
                Lien(row.transform, fait ? "Refaire" : "Commencer", () => Commencer(l));
            }
        }
    }

    static Transform Marge(string nom)
    {
        var m = UIFactory.HBox(_carte.transform, 0, false, nom);
        m.padding = new RectOffset(14, 14, 0, 0);
        return m.transform;
    }

    static void Lien(Transform parent, string texte, Action clic)
    {
        var b = UIFactory.Button(parent, texte, UITheme.Carte, UITheme.Primaire, 26, UITheme.Role.Aide, false);
        var t = b.GetComponentInChildren<TMP_Text>(true);
        t.fontStyle = FontStyles.Underline;
        b.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        UIFactory.LargeurDuTexte(b);
        b.onClick.AddListener(() => clic());
    }

    // ── Commencer : l'écran, puis le guide pas à pas ───────────────────────────

    /// « Commencer » de la visite : la première étape à faire.
    public static void Commencer()
    {
        var p = Prochaine(Lignes(Lire()));
        if (p != null) Commencer(p.Value);
    }

    public static void Commencer(Ligne l)
    {
        // 13b-3 : ce qui manque d'abord — pas de locataire sans bâtiment, pas de facture sans locataire.
        if (l == Ligne.Locataire && Fiches().Count == 0) l = Ligne.Batiment;
        if (l == Ligne.Facture && Locataires().Count == 0) l = Fiches().Count == 0 ? Ligne.Batiment : Ligne.Locataire;
        var avant = Maintenant();
        // Retour du 25/10 : la facture du tutoriel est émise sans être envoyée (pas d'e-mail réel, et
        // un e-mail mal réglé ne bloque pas) ; avant Aller, le bouton s'ouvre déjà sur « Émettre ».
        bool facture = l == Ligne.Facture;
        if (facture) FactureLoyerPanel.ModeSansEnvoi(true);
        if (!Aller(l)) { if (facture) FactureLoyerPanel.ModeSansEnvoi(false); return; }
        var guide = Guide(l, avant);
        if (guide == null) return;
        Bulles.Lancer(guide, Nom(l), bloquer: false, quitter: "Quitter le guide", fin: fini => { if (fini) Apres(l); },
                      ferme: facture ? () => FactureLoyerPanel.ModeSansEnvoi(false) : (Action)null);
    }

    /// L'état au départ d'un guide : il attend un nouveau logo, un RIB, un bâtiment, un locataire, une
    /// facture de plus (vu en Play le 25/10 : relancé avec un RIB déjà là, il passait tout).
    struct Avant { public string logo; public int ribs, batiments, locataires, factures; }

    static Avant Maintenant()
    {
        var r = ReglageService.Current;
        return new Avant
        {
            logo = r?.logoPath, ribs = r?.ribs?.Count ?? 0, batiments = BatimentManager.Instance?.Batiments?.Count ?? 0,
            locataires = LocatairesAffiches(), factures = FacturesEmises(),
        };
    }

    static List<Locataire> Locataires() =>
        (BatimentManager.Instance?.Batiments ?? new List<Batiment>()).Where(b => b?.locataireDuBatiment != null && !BatimentExemple.Est(b))
            .SelectMany(b => b.locataireDuBatiment).Where(x => x != null).ToList();

    /// Les locataires des fiches affichées : un locataire ajouté n'est dans la liste de BatimentManager
    /// qu'au premier « Sauvegarder » — la fiche travaille sur sa copie du bâtiment (vu en Play le 25/10 :
    /// le guide attendait un locataire qu'il ne voyait pas).
    static int LocatairesAffiches() => Fiches().Sum(f => f.listLocataire?.Count ?? 0);

    static int FacturesEmises() => Locataires().Sum(x => x.facturesEtat?.Count(f => !string.IsNullOrEmpty(f.numero)) ?? 0);

    /// Les fiches des vrais bâtiments : l'exemple ne compte pas dans les premiers pas (13b-3).
    static List<BatimentPrefab> Fiches() =>
        BatimentManager.Instance?.BatimentPrefab?.Where(p => p != null && !BatimentExemple.Est(p.getBatiment())).ToList() ?? new List<BatimentPrefab>();

    /// Le bâtiment ouvert, et le locataire ouvert dans sa vue des locataires.
    static BatimentPrefab Fiche()
    {
        var f = BatimentManager.Instance?.menuManager?.Actif as BatimentPrefab;
        return f != null ? f : null;
    }

    static LocatairePrefab Loc()
    {
        var f = Fiche();
        var l = f != null ? f.menulocataire?.Actif as LocatairePrefab : null;
        return l != null ? l : null;
    }

    static RectTransform Rt(Component c) => c != null ? (RectTransform)c.transform : null;
    static bool Ouvert(Component c) => c != null && c.gameObject.activeInHierarchy;
    static Transform FactureLoyer() => Ouvert(FactureLoyerPanel.Instance) ? FactureLoyerPanel.Instance.transform : null;

    /// Le bouton du bas de la facture : son libellé suit la date d'envoi (« Sauvegarder et envoyer »,
    /// « Enregistrer pour l'envoi du … »).
    static RectTransform BoutonEmettre()
    {
        var p = FactureLoyer();
        var b = p != null ? p.GetComponentsInChildren<Button>().LastOrDefault(x => x.transform.parent.name == "Right") : null;
        return b != null ? (RectTransform)b.transform : null;
    }

    public static bool AUnGuide(Ligne l) => Guide(l) != null;

    /// Le guide fini : la ligne relue (l'invitation et le paiement passent par le serveur), puis
    /// « Vos RIB sont prêts », et la suivante proposée.
    static async void Apres(Ligne l)
    {
        if ((l == Ligne.Inviter || l == Ligne.Abonnement) && SessionCloud.Entreprise != null)
            await AbonnementCloud.Lire(SessionCloud.Entreprise);
        var lignes = Lignes(Lire());
        if (!lignes.Any(x => x.ligne == l && x.fait)) return;   // terminé sans être fait : rien à fêter
        var p = Prochaine(lignes);
        string suite = p == null ? null : lignes.Where(x => x.ligne == p.Value).Select(x => $"{x.texte} ({x.sous})").First();
        int faites = lignes.Count(x => x.fait);
        var bravo = new Bulles.Etape
        {
            titre = Reussite(l),
            texte = p == null ? "Vos premiers pas sont terminés : CIPL est prêt."
                              : $"{faites} étape{(faites > 1 ? "s" : "")} sur {lignes.Count}. Prochaine étape : {suite}.",
            suivant = p == null ? "Terminer" : "Continuer ›",
        };
        Bulles.Lancer(new[] { bravo }, null, bloquer: true, quitter: p == null ? null : "Plus tard",
                      fin: continuer => { if (continuer && p != null) Commencer(p.Value); });
        GeneralMenuPanel.Instance?.Refresh();
    }

    static string Reussite(Ligne l) => l switch
    {
        Ligne.Logo => "Votre logo est en place",
        Ligne.Rib => "Vos RIB sont prêts",
        Ligne.Envoi => "L'envoi des factures est réglé",
        Ligne.Batiment => "Votre premier bâtiment est créé",
        Ligne.Locataire => "Votre locataire est prêt",
        Ligne.Facture => "Votre première facture est émise",
        Ligne.Inviter => "Invitation envoyée",
        Ligne.Abonnement => "Abonnement choisi",
        _ => "C'est fait",
    };

    static Transform Reglages =>
        ReglagePanel.Instance != null && ReglagePanel.Instance.gameObject.activeInHierarchy ? ReglagePanel.Instance.transform : null;

    /// Une étape « remplissez ce champ » (retours du 25/10) : pas de « Suivant » tant qu'il est vide,
    /// « Suivant » dès qu'il est rempli (même en cours de frappe) ; quitté rempli, la bulle avance seule.
    /// `deja` : la valeur est déjà enregistrée ailleurs (mot de passe, champ masqué vide).
    static Bulles.Etape Saisir(string titre, string texte, Func<TMP_InputField> champ, string sauf = null,
                               Func<RectTransform> cible = null, Func<bool> sauter = null, Func<bool> deja = null) =>
        new Bulles.Etape
        {
            titre = titre, texte = texte, cible = cible ?? (() => Rt(champ())), sauter = sauter,
            fait = () => Bulles.Rempli(champ(), sauf) || deja?.Invoke() == true,
            pret = () => Bulles.NonVide(champ(), sauf) || deja?.Invoke() == true,
            attente = "Remplissez le champ, puis « Suivant ».",
        };

    /// Le champ de saisie sous ce libellé, dans les Réglages.
    static TMP_InputField Saisie(string libelle)
    {
        var c = Bulles.Champ(libelle, Reglages);
        return c != null ? c.GetComponent<TMP_InputField>() : null;
    }

    static Bulles.Etape E(string titre, string texte, Func<RectTransform> cible, Func<bool> fait = null, string attente = null,
                          Func<bool> sauter = null, Func<string> manque = null, Func<bool> pret = null) =>
        new Bulles.Etape { titre = titre, texte = texte, cible = cible, fait = fait, attente = attente, sauter = sauter, manque = manque, pret = pret };

    // Retour du 25/10 : une action faite en avance (invitation envoyée, fiche sauvegardée depuis une étape
    // précédente) fait sauter les étapes qu'elle rend inutiles, au lieu de laisser le guide en arrière.
    static readonly Func<bool> Toujours = () => true;
    static bool BatSauve() { var f = Fiche(); return f != null && !f.EnEdition; }
    static bool LocSauve() { var l = Loc(); return l != null && !l.EnEdition; }

    /// Le guide pas à pas de chaque ligne : Réglages (13b-2), bâtiment, locataire, facture (13b-3).
    /// `a` : l'état au départ (Maintenant), vide pour AUnGuide.
    static List<Bulles.Etape> Guide(Ligne l, Avant a = default)
    {
        string logoAvant = a.logo;
        int ribsAvant = a.ribs;
        const string Pennylane = "Envoyer via Pennylane (e-facture Factur-X)";
        Func<bool> pennylane = () => Bulles.Coche(Pennylane, Reglages);
        Func<bool> sansSmtp = () => pennylane() || Messagerie.Relie(ReglageService.Current?.smtp) || ReglagePanel.Instance?.ZoneRelier != null;
        switch (l)
        {
            case Ligne.Logo: return new List<Bulles.Etape>
            {
                E("Choisir le logo", "Cliquez sur « Choisir un logo… » et prenez l'image de votre logo (PNG ou JPG). Elle s'imprime en haut de chaque facture.",
                  () => Bulles.Bouton("Choisir un logo…", Reglages),
                  () => !string.IsNullOrEmpty(ReglageService.Current?.logoPath) && ReglageService.Current.logoPath != logoAvant,
                  "La suite s'affiche dès que le logo est choisi."),
                E("Le bas de page et le lieu", "Ils ont été remplis d'après votre SIRET : vérifiez-les, corrigez si besoin, puis cliquez sur « Enregistrer » en bas de l'écran.",
                  () => Bulles.Section("Textes fixes", Reglages)),
            };
            case Ligne.Rib: return new List<Bulles.Etape>
            {
                E("Ajouter un RIB", "Cliquez sur « + Ajouter un RIB » : une fenêtre s'ouvre pour saisir le compte.",
                  () => Bulles.Bouton("+  Ajouter un RIB", Reglages), () => Bulles.Fenetre("Nouveau RIB") != null,
                  "La suite s'affiche quand la fenêtre est ouverte."),
                // Retour du 25/10 : une bulle pour toute la fenêtre, champ par champ ; « Valider » contrôle (RegleRib).
                E("Recopiez votre RIB", "Prenez le RIB du compte où vos locataires paient (papier, ou téléchargé depuis l'espace en ligne de votre banque) :\n"
                  + "· Nom : pour le retrouver dans la liste (par exemple « BNP Meudon »)\n"
                  + "· Titulaire : le nom du compte, tel qu'il figure sur le RIB\n"
                  + "· Domiciliation : l'agence qui tient le compte\n"
                  + "· RIB : banque, guichet, compte et clé ; laissé vide, il se remplit d'après l'IBAN\n"
                  + "· IBAN : 27 caractères commençant par FR\n"
                  + "· BIC : 8 ou 11 lettres et chiffres\n"
                  + "Tout s'imprime sur vos factures. Cliquez ensuite sur « Valider » : une saisie incomplète ou fausse est refusée, la zone en rouge.",
                  () => Bulles.Fenetre("Nouveau RIB"), () => (ReglageService.Current?.ribs?.Count ?? 0) > ribsAvant,
                  "La suite s'affiche dès que le RIB est validé."),
            };
            case Ligne.Envoi: return new List<Bulles.Etape>
            {
                E("Comment partent vos factures", "Case cochée : vos factures sont déposées dans Pennylane (il faudra sa clé). Case décochée : elles partent par e-mail, depuis votre messagerie. Regardez si elle est cochée, choisissez, puis « Suivant ».",
                  () => Bulles.Texte(Pennylane, Reglages)?.parent as RectTransform),
                E("La clé Pennylane", "Collez la clé d'API de votre société, créée dans Pennylane. Chaque société a la sienne.",
                  () => Bulles.Champ("Clé API Pennylane", Reglages), sauter: () => !pennylane()),
                // Retour du 25/10 : ces champs ne se passent plus vides (« Suivant » menait à « Enregistrer »,
                // qui attendait sans fin) ; « Suivant » apparaît dès qu'ils sont remplis (Saisir).
                // Étape 16a : l'adresse d'abord ; Gmail, Outlook, Hotmail… se relient, les autres en SMTP.
                Saisir("L'adresse d'expédition", "L'adresse qui envoie vos factures : vos locataires la voient et peuvent y répondre. Gmail, Outlook ou Hotmail : CIPL proposera de la relier, sans mot de passe.",
                  () => Saisie("Adresse d'expédition"), sauter: pennylane),
                E("Relier votre adresse", "Cliquez sur le bouton vert « Se connecter avec … » : la fenêtre de Google ou de Microsoft s'ouvre dans votre navigateur. Connectez-vous, autorisez CIPL, puis revenez ici.",
                  () => ReglagePanel.Instance?.ZoneRelier, () => Messagerie.Relie(ReglageService.Current?.smtp),
                  "La suite s'affiche dès que l'adresse est reliée.", sauter: () => pennylane() || ReglagePanel.Instance?.ZoneRelier == null),
                Saisir("Votre serveur d'envoi", "Rempli d'après votre adresse quand CIPL connaît votre messagerie ; sinon, celui de votre messagerie, avec le port 587.",
                  () => Saisie("Serveur SMTP"), sauter: sansSmtp),
                Saisir("Le mot de passe", "La plupart des messageries demandent un mot de passe d'application, créé dans la sécurité de votre compte, et non votre mot de passe habituel.",
                  () => Saisie("Mot de passe (d'application)"), sauter: sansSmtp,
                  deja: () => !string.IsNullOrWhiteSpace(ReglageService.GetSmtpPassword())),
                new Bulles.Etape
                {
                    titre = "Enregistrer", texte = "Cliquez sur « Enregistrer » en bas de l'écran.",
                    cible = () => Bulles.Bouton("Enregistrer", Reglages), fait = () => Lire().envoi,
                    attente = "La suite s'affiche dès que les réglages sont complets et enregistrés.",
                    manque = ManqueEnvoi,
                },
                E("Un e-mail de test", "Vérifiez avec « Envoyer un email de test » : il part vers votre adresse d'expédition, jamais vers un locataire.",
                  () => Bulles.Bouton("Envoyer un email de test", Reglages), sauter: pennylane),
            };
            case Ligne.Inviter: return new List<Bulles.Etape>
            {
                E("Inviter", "Un collègue travaille avec vous ? Cliquez sur « Inviter ».",
                  () => Bulles.Bouton("Inviter", Reglages), () => Bulles.Fenetre("Inviter un collègue") != null,
                  "La suite s'affiche quand la fenêtre est ouverte."),
                E("Son adresse et son rôle", "Il reçoit un code par e-mail pour créer son compte, valable 7 jours. « Utilisateur » ne touche ni à l'envoi des factures ni à l'abonnement ; « Admin » peut tout faire.",
                  () => Bulles.Fenetre("Inviter un collègue"), () => Bulles.Fenetre("Inviter un collègue") == null, pret: Toujours),
                E("Envoyer", "Cliquez sur « Envoyer l'invitation » : sa licence est réservée tout de suite.",
                  () => Bulles.Bouton("Envoyer l'invitation", Bulles.Fenetre("Inviter un collègue")), () => Bulles.Fenetre("Inviter un collègue") == null,
                  "La suite s'affiche quand l'invitation est partie.", sauter: () => Bulles.Fenetre("Inviter un collègue") == null),
            };
            case Ligne.Abonnement: return new List<Bulles.Etape>
            {
                E("Mensuel ou annuel", "Choisissez la formule : l'annuel offre des mois gratuits.",
                  () => Bulles.Champ("Choisir l'abonnement", Reglages)),
                E("Les licences", "Une licence par personne qui utilise CIPL, pour chaque entreprise. Ajustez avec « - » et « + » : le total se met à jour en dessous.",
                  () => Bulles.Texte("Entreprises du groupe", Reglages)?.parent as RectTransform),
                E("Le paiement", "Cliquez sur « Continuer vers le paiement » : la page sécurisée de Stripe s'ouvre dans votre navigateur (carte ou prélèvement SEPA). La date du premier prélèvement est indiquée juste au-dessus.",
                  () => Bulles.Bouton("Continuer vers le paiement", Reglages), () => AbonnementCloud.Courant?.abonne == true,
                  "La suite s'affiche dès que le paiement est validé."),
            };
            case Ligne.Batiment: return new List<Bulles.Etape>
            {
                E("Le « + »", "Cliquez sur « + », en haut à droite : un bâtiment « Nouveau » est créé et sa fiche s'ouvre, prête à remplir.",
                  () => BatimentManager.Instance?.menuManager?.addTabButton?.transform as RectTransform,
                  () => (BatimentManager.Instance?.Batiments?.Count ?? 0) > a.batiments, "La suite s'affiche quand la fiche est ouverte."),
                Saisir("Son nom", "Le nom de l'onglet et des dossiers de factures, par exemple « Rivoli » ou « 12 rue des Lilas ». Il doit être unique.",
                  () => Fiche()?.nameOfTheBuiding?.inputModify, "Nouveau"),
                Saisir("Son adresse", "L'adresse complète, puis « Chercher » : la carte se place dessus.",
                  () => Fiche()?.mapController?.addressInput, cible: () => Rt(Fiche()?.mapController?.addressInput)?.parent as RectTransform,
                  sauter: BatSauve),
                E("Sauvegarder", "La surface, le cadastre et la date d'achat peuvent attendre. Cliquez sur « Sauvegarder », en haut de la fiche.",
                  () => Rt(Fiche()?.save), BatSauve, "La suite s'affiche quand le bâtiment est enregistré.", sauter: BatSauve),
            };
            // Les quatre étapes du parcours de la fiche (ParcoursLocataire : général, bail, loyer, dépôt),
            // sans lesquelles la facturation reste bloquée.
            case Ligne.Locataire: return new List<Bulles.Etape>
            {
                E("Ajouter le locataire", "Dans « Locataires », cliquez sur « + Ajouter » : la fiche du locataire s'ouvre, prête à remplir.",
                  () => Rt(Fiche()?.summaryView?.btnAjouterLocataire), () => LocatairesAffiches() > a.locataires,
                  "La suite s'affiche quand la fiche est ouverte."),
                Saisir("Son nom", "Le nom du locataire, société ou personne. Pour une société, son SIRET juste en dessous, puis « Pappers », remplissent le nom et l'adresse.",
                  () => Loc()?.nameOfLocataire?.inputModify, "Nouveau"),
                E("Son e-mail", "L'adresse où partiront ses factures. Le téléphone et l'adresse postale, juste en dessous, sont facultatifs.",
                  () => Rt(Loc()?.emailLocataireTxt?.inputModify), LocSauve, pret: Toujours),
                E("Le bail", "Son type, puis les dates de début et de fin (JJ / MM / AAAA) : elles fixent les périodes facturées et les révisions.",
                  () => Loc() != null ? Loc().dateDebutBail.transform.parent.parent as RectTransform : null, LocSauve, sauter: LocSauve, pret: Toujours),
                E("Sauvegarder", "Cliquez sur « Sauvegarder », en haut de la fiche : le loyer se règle ensuite.",
                  () => Rt(Loc()?.save), LocSauve, "La suite s'affiche quand le locataire est enregistré.", sauter: LocSauve),
                E("Le loyer", "Dans « Loyer », cliquez sur « Initialiser » : le montant, la périodicité (mois ou trimestre), la TVA et la provision pour charges.",
                  () => Ouvert(RevisionPanel.Instance) ? null : Bulles.Bouton("Initialiser", Rt(Loc()?.loyerSummary)),
                  () => Loc()?.GetLocataire()?.LoyerInitialise == true, "La suite s'affiche quand le loyer est enregistré.",
                  manque: () => Loc()?.GetLocataire() is Locataire x ? ParcoursLocataire.Bloque(x, ParcoursLocataire.Etape.Loyer) : null),
                E("Le dépôt de garantie", "Dans « Dépôt de garantie », cliquez sur « Initialiser » : son montant, en périodes de loyer, et s'il est déjà demandé ou reçu.",
                  () => GameObject.Find("RevScrim") is GameObject s ? s.transform.Find("RevCard") as RectTransform : Bulles.Bouton("Initialiser", Loc()?.transform),
                  () => Loc()?.GetLocataire()?.DepotInitialise == true, "La suite s'affiche quand le dépôt est enregistré.",
                  manque: () => Loc()?.GetLocataire() is Locataire x ? ParcoursLocataire.Bloque(x, ParcoursLocataire.Etape.Depot) : null),
            };
            case Ligne.Facture: return new List<Bulles.Etape>
            {
                E("La période", "Le mois (ou le trimestre) facturé et l'année. Le loyer de la période se remplit d'après le bail : vérifiez-le juste en dessous.",
                  () => Bulles.Champ("Période facturée", FactureLoyer())),
                E("L'aperçu", "Cliquez sur « Générer facture » : l'aperçu s'affiche à droite, tel qu'il sera imprimé.",
                  () => Bulles.Bouton("Générer facture", FactureLoyer())),
                E("Émettre", "Cliquez sur « Émettre (sans envoyer) » : la facture reçoit son numéro et son PDF. Pendant ce guide, rien ne part (ni e-mail ni Pennylane) : elle attend dans le suivi de facturation, d'où vous l'enverrez. Ensuite, ce bouton redevient « Sauvegarder et envoyer ».",
                  BoutonEmettre, () => FacturesEmises() > a.factures, "La suite s'affiche quand la facture est émise."),
            };
            default: return null;
        }
    }

    /// Ce qui manque à l'envoi enregistré (retour du 25/10 : la bulle attendait sans dire quoi).
    static string ManqueEnvoi()
    {
        var r = ReglageService.Current;
        if (r == null) return null;
        if (r.modeEnvoi != ModeEnvoi.Email)
            return string.IsNullOrEmpty(ReglageService.LireSecret("pennylane_api_key"))
                ? "Il manque la clé Pennylane : collez-la plus haut et enregistrez, ou décochez « Envoyer via Pennylane » pour envoyer par e-mail."
                : null;
        return EmailService.CeQuiManque();
    }

    /// L'écran où la ligne se fait.
    /// Faux : l'écran du guide ne peut pas s'ouvrir (le message est déjà affiché).
    static bool Aller(Ligne l)
    {
        var bm = BatimentManager.Instance;
        var fiches = Fiches();
        // Bâtiment, locataire, facture : Réglages fermé, il cachait le « + » (vu en Play le 25/10 : le
        // guide de l'envoi finit dans Réglages, « Continuer » enchaîne sur le bâtiment).
        bool reglages = l == Ligne.Logo || l == Ligne.Rib || l == Ligne.Envoi || l == Ligne.Inviter || l == Ligne.Abonnement;
        if (!reglages && ReglagePanel.Instance != null) ReglagePanel.Instance.Close();
        switch (l)
        {
            case Ligne.Logo: ReglagePanel.OuvrirOnglet(ReglagePanel.OFacturation); break;
            case Ligne.Rib: ReglagePanel.OuvrirOnglet(ReglagePanel.OEntreprise); break;   // RIB dans Entreprise depuis le 27/10
            case Ligne.Envoi: ReglagePanel.OuvrirOnglet(ReglagePanel.OIntegrations); break;
            case Ligne.Inviter: ReglagePanel.OuvrirOnglet(ReglagePanel.OUtilisateurs); break;
            case Ligne.Abonnement: ReglagePanel.OuvrirOnglet(ReglagePanel.OAbonnement); break;
            // 13b-3 : plus de bâtiment créé d'office ; le guide montre le « + ».
            case Ligne.Batiment: bm?.menuManager?.OpenGeneralMenu(); break;
            case Ligne.Locataire:
                // Le résumé, où « + Ajouter » est dans « Locataires » (retour du 25/10 : la vue des
                // locataires d'un bâtiment qui n'en a pas est vide, sans bouton).
                bm.menuManager.OnSelect(fiches[0]);
                fiches[0].ShowSummary();
                break;
            case Ligne.Facture:
                // Un locataire complet (général, bail, loyer, dépôt) : sinon la facture est refusée.
                var locs = fiches.SelectMany(b => b.dictionnairelocataire.Where(kv => kv.Key != null && kv.Value != null).Select(kv => (b, kv.Key, kv.Value))).ToList();
                var pret = locs.FirstOrDefault(x => ParcoursLocataire.Prochaine(x.Item2) == ParcoursLocataire.Etape.Termine);
                var (bp, loc, lp) = pret.Item3 != null ? pret : locs.FirstOrDefault();
                if (lp == null) return false;
                bm.menuManager.OnSelect(bp);
                bp.ShowLocataireView();
                bp.menulocataire.OnSelect(lp);
                if (pret.Item3 == null) { ConfirmDialog.Erreur(ParcoursLocataire.FacturationBloquee(loc)); return false; }
                FactureLoyerPanel.OpenLoyer(lp);
                break;
        }
        return true;
    }
}
