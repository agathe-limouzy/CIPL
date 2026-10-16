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

    /// Pour les essais dans l'éditeur (script-execute) : CloudClient répond « sans réseau » sans
    /// rien envoyer, le build du même PC restant en ligne pour jouer le collègue.
    /// ponytail: interrupteur de test en mémoire, jamais enregistré.
    public static bool Simulee;

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
        EcritureCloud.RenvoyerEnAttente();   // ce qui n'avait pas pu partir, contrôlé par la version
        DirectCloud.ToutRelire();            // les changements des collègues pendant la coupure
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
            var barre = Barre();
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

        static RectTransform Barre()
        {
            var mm = BatimentManager.Instance != null ? BatimentManager.Instance.menuManager : null;
            return mm != null && mm.tabScrollRect != null ? (RectTransform)mm.tabScrollRect.transform.parent : null;
        }

        void Construire(RectTransform barre)
        {
            // Juste après la barre d'onglets, dans la pile verticale de BatimentManager : l'écran
            // descend de la hauteur du bandeau au lieu d'être caché dessous (retour du 16/10 : il
            // couvrait « Modifier »), et reprend sa place quand le bandeau disparaît. Les fenêtres
            // (ConfirmDialog…) restent devant.
            var fond = UIFactory.Panel("BandeauHorsLigne", barre.parent, new Color32(0xA3, 0x2D, 0x2D, 0xFF));
            fond.transform.SetSiblingIndex(barre.GetSiblingIndex() + 1);
            UIFactory.LE(fond.gameObject, minH: 44, prefH: 44, flexH: 0);
            var h = fond.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(20, 12, 4, 4); h.spacing = 16;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            _texte = UIFactory.Text(fond.transform, "", UITheme.Role.Donnee, Color.white, true);
            _texte.enableWordWrapping = false;
            UIFactory.LE(_texte.gameObject, flexW: 1);
            var b = UIFactory.Button(fond.transform, "Réessayer", Color.white, new Color32(0xA3, 0x2D, 0x2D, 0xFF), 34, UITheme.Role.Action, true);
            UIFactory.LargeurDuTexte(b);
            b.onClick.AddListener(() => { _prochainEssai = Time.realtimeSinceStartup + 30f; Essayer(dire: true); });
            _vue = fond.gameObject;
        }
    }
}
