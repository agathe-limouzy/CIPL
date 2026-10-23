using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Étape 8 : hors ligne (décisions du 09/10 et du 16/10). Chaque réponse du serveur dit si la
/// connexion est là (CloudClient → Constater). Sans elle : lecture seule, bandeau rouge sous les
/// onglets, nouvel essai toutes les 30 s ou sur « Réessayer ». Au retour, ce qui n'avait pas pu
/// partir repart, contrôlé par la version, et les fiches changées entre-temps sont relues.
public static class HorsLigne
{
    // ── Sans réseau (testé en EditMode) ────────────────────────────────────────

    /// Le serveur n'a pas répondu (coupure, délai dépassé) ou il est indisponible (502 à 504).
    /// Un refus (4xx) prouve au contraire que la connexion est là.
    public static bool Panne(bool ok, long code) => !ok && (code == 0 || (code >= 502 && code <= 504));

    public static string Texte(DateTime aJour) =>
        $"Hors ligne — lecture seule. Données à jour au {aJour:dd'/'MM} à {aJour.Hour} h {aJour:mm}";

    public const string Refus = "Hors ligne — lecture seule : rien ne peut être modifié tant que la connexion n'est pas revenue.";

    // ── État ───────────────────────────────────────────────────────────────────

    public static bool Actif { get; private set; }

    /// Dernière réponse du serveur : les données affichées datent au plus de là.
    public static DateTime AJourLe { get; private set; } = DateTime.Now;

    /// Étape 8b : démarré sans connexion, sur la copie locale ; au retour, tout est relu.
    public static bool DepuisCopie { get; private set; }

    public static void SurCopie(DateTime aJour)
    {
        DepuisCopie = true;
        AJourLe = aJour;
    }

    /// Pour les essais dans l'éditeur (script-execute) : CloudClient répond « sans réseau » sans
    /// rien envoyer, le build du même PC restant en ligne pour jouer le collègue.
    /// ponytail: interrupteur de test en mémoire, jamais enregistré.
    public static bool Simulee;

#if UNITY_EDITOR
    /// Essai du démarrage sans connexion (8b) : un Play repart de zéro, la coupure simulée se pose
    /// donc avant (EditorPrefs « CIPL_HorsLigneAuDemarrage », par script-execute). Éditeur seulement.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SimulerAuDemarrage() => Simulee = UnityEditor.EditorPrefs.GetBool("CIPL_HorsLigneAuDemarrage", false);
#endif

    /// Appelé par CloudClient après chaque appel.
    public static void Constater(bool ok, long code)
    {
        if (Panne(ok, code)) { if (!Actif) Perdue(); }
        else
        {
            AJourLe = DateTime.Now;
            if (Actif) Retrouvee();
        }
    }

    static void Perdue()
    {
        Actif = true;
        Debug.Log("[HorsLigne] Connexion perdue : lecture seule");
        Bandeau.Assurer();
    }

    static void Retrouvee()
    {
        Actif = false;
        Debug.Log("[HorsLigne] Connexion revenue");
        if (SessionCloud.Entreprise == null) return;
        UndoToast.Instance?.ShowInfo("Connexion revenue.");
        if (DepuisCopie)   // étape 8b : rien n'a pu être modifié sur la copie ; on relit tout
        {
            DepuisCopie = false;
            DemarrageCloud.ReprendreEnLigne();
            return;
        }
        EcritureCloud.RenvoyerEnAttente();   // ce qui n'avait pas pu partir, contrôlé par la version
        DirectCloud.ToutRelire();            // les changements des collègues pendant la coupure
        ReglagesCloud.ARelire(null);         // étape 9a : Réglages et secrets aussi
    }

    /// « Réessayer », ou toutes les 30 s : un appel léger, constaté comme les autres.
    public static async void Essayer(bool dire)
    {
        if (!SessionCloud.Configure) return;
        await CloudClient.Sonder(SessionCloud.Url, SessionCloud.Cle);
        if (Actif && dire) UndoToast.Instance?.ShowInfo("Toujours hors ligne.");
    }

    // ── Bandeau rouge sous la barre d'onglets (maquette 5, décision du 16/10) ──

    class Bandeau : MonoBehaviour
    {
        static Bandeau _instance;
        GameObject _vue;
        TMP_Text _texte;
        float _prochainEssai;

        public static void Assurer()
        {
            if (_instance == null)
            {
                var go = new GameObject("HorsLigne");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Bandeau>();
            }
            _instance._prochainEssai = Time.realtimeSinceStartup + 30f;
        }

        void Update()
        {
            bool montrer = Actif && SessionCloud.Entreprise != null;
            var barre = BarreOnglets();
            if (!montrer || barre == null) { if (_vue != null) _vue.SetActive(false); return; }
            if (_vue == null) Construire(barre);
            _vue.SetActive(true);
            _texte.text = Texte(AJourLe);

            if (Time.realtimeSinceStartup >= _prochainEssai)
            {
                _prochainEssai = Time.realtimeSinceStartup + 30f;
                Essayer(dire: false);
            }
        }

        void Construire(RectTransform barre)
        {
            var rouge = new Color32(0xA3, 0x2D, 0x2D, 0xFF);
            _vue = NouveauBandeau(barre, "BandeauHorsLigne", rouge, Color.white, "Réessayer", Color.white, rouge, out _texte, out var b);
            b.onClick.AddListener(() => { _prochainEssai = Time.realtimeSinceStartup + 30f; Essayer(dire: true); });
        }
    }

    /// La barre d'onglets des bâtiments, sous laquelle se glissent les bandeaux (null avant l'accueil).
    public static RectTransform BarreOnglets()
    {
        var mm = BatimentManager.Instance != null ? BatimentManager.Instance.menuManager : null;
        return mm != null && mm.tabScrollRect != null ? (RectTransform)mm.tabScrollRect.transform.parent : null;
    }

    /// Un bandeau juste après la barre d'onglets, dans la pile verticale de BatimentManager : l'écran
    /// descend de sa hauteur au lieu d'être caché dessous (retour du 16/10 : il couvrait « Modifier »),
    /// et reprend sa place quand le bandeau disparaît. Les fenêtres (ConfirmDialog…) restent devant.
    /// Sans libellé de bouton, pas de bouton (`b` nul). Sert aussi à l'abonnement (étape 9c1).
    public static GameObject NouveauBandeau(RectTransform barre, string nom, Color fond, Color encre, string bouton,
                                            Color boutonFond, Color boutonEncre, out TMP_Text texte, out Button b)
    {
        var img = UIFactory.Panel(nom, barre.parent, fond);
        img.transform.SetSiblingIndex(barre.GetSiblingIndex() + 1);
        UIFactory.LE(img.gameObject, minH: 44, prefH: 44, flexH: 0);
        var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(20, 12, 4, 4); h.spacing = 16;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        texte = UIFactory.Text(img.transform, "", UITheme.Role.Donnee, encre, true);
        texte.enableWordWrapping = false;
        UIFactory.LE(texte.gameObject, flexW: 1);
        b = null;
        if (bouton != null)
        {
            b = UIFactory.Button(img.transform, bouton, boutonFond, boutonEncre, 34, UITheme.Role.Action, true);
            UIFactory.LargeurDuTexte(b);
        }
        return img.gameObject;
    }
}
