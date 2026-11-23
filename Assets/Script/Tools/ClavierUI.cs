using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Revue 17 (D10-02 / D8-12, lot F ; décision du 16/11) : le clavier. Tab et Maj+Tab passent au champ suivant ou précédent de la
/// fenêtre du dessus, dans l'ordre de lecture. Dans un message (ConfirmDialog) : Échap = « Annuler » (ou « OK » d'une erreur) ;
/// Entrée = « OK » d'une erreur seulement, jamais « Confirmer » (suppression, envoi). Avant : aucune touche, sauf dans la connexion.
/// BY4 (décision du 19/11, remplace « Échap ne ferme pas les formulaires ») : dans la fenêtre du dessus, Échap = son « Annuler »
/// (« Fermer », « ← Retour »), après « Abandonner la saisie ? » si un champ de texte y a changé ; Entrée, hors d'un champ en saisie
/// = son « Enregistrer » (« Sauvegarder », « Valider »), jamais un bouton qui envoie.
public class ClavierUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Installer()
    {
        var go = new GameObject("ClavierUI");
        DontDestroyOnLoad(go);
        go.AddComponent<ClavierUI>();
    }

    // Suivi des saisies (BY4) : le champ en main, son texte quand il l'a prise et à l'image d'avant (TMP remet l'ancien texte sur
    // Échap, peut-être avant nous dans l'image), les fenêtres dont un champ a changé.
    // ponytail: seuls les champs de texte comptent ; une case ou un menu changé ne déclenche pas la question.
    TMP_InputField _champ;
    string _texteAuDebut, _dernierTexte;
    bool _focusAvant;
    readonly HashSet<Transform> _modifiees = new HashSet<Transform>();

    void Update()
    {
        SuivreSaisie();
        var message = ConfirmDialog.Instance;
        if (message != null && message.gameObject.activeInHierarchy)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) message.Touche(false);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) message.Touche(true);
        }
        else if (Input.GetKeyDown(KeyCode.Tab)) Tabuler(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        else if (Input.GetKeyDown(KeyCode.Escape)) Echap();
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Entree();
        if (_champ != null) { _dernierTexte = _champ.text; _focusAvant = _champ.isFocused; }
        else _focusAvant = false;
    }

    void SuivreSaisie()
    {
        var es = EventSystem.current;
        var choisi = es != null ? es.currentSelectedGameObject : null;
        var champ = choisi != null ? choisi.GetComponent<TMP_InputField>() : null;
        // BY19 (19/11) : un bouton cliqué restait « choisi » et chaque Entrée le recliquait (« + Ajouter une charge » ouvrait une
        // fenêtre par appui). Seuls les champs gardent la main ; les menus TMP_Dropdown ouverts gardent la leur (leurs options).
        if (choisi != null && champ == null && !es.alreadySelecting && choisi.GetComponentInParent<TMP_Dropdown>() == null)
        { es.SetSelectedGameObject(null); choisi = null; }
        if (champ != _champ)
        {
            if (ChampModifie()) _modifiees.Add(Fenetre(_champ.transform));
            _champ = champ;
            _texteAuDebut = _dernierTexte = champ != null ? champ.text : null;
        }
        _modifiees.RemoveWhere(t => t == null || !t.gameObject.activeInHierarchy);   // fenêtre fermée : sa saisie est oubliée
    }

    bool ChampModifie() => _champ != null && _dernierTexte != _texteAuDebut;

    void Echap()
    {
        var fenetre = FenetreDuDessus();
        var annuler = fenetre != null ? Bouton(fenetre, EstAnnuler) : null;
        if (annuler == null) return;   // liste déroulante ouverte, ou fenêtre sans « Annuler » : rien
        bool modifiee = _modifiees.Contains(fenetre) || (ChampModifie() && Fenetre(_champ.transform) == fenetre);
        if (!modifiee || ConfirmDialog.Instance == null) { annuler.onClick.Invoke(); return; }
        if (ChampModifie()) StartCoroutine(Remettre(_champ, _dernierTexte));
        ConfirmDialog.Instance.Show("Abandonner la saisie ?", "Ce qui a été tapé dans cette fenêtre sera perdu.",
            () => { if (annuler != null) annuler.onClick.Invoke(); }, "Abandonner", "Continuer la saisie");
    }

    /// TMP a remis l'ancien texte du champ sur Échap : « Continuer la saisie » retrouve ce qui était tapé.
    static System.Collections.IEnumerator Remettre(TMP_InputField champ, string texte)
    {
        yield return null;
        if (champ != null && champ.text != texte) champ.text = texte;
    }

    void Entree()
    {
        var es = EventSystem.current;
        var choisi = es != null ? es.currentSelectedGameObject : null;
        // Un champ en saisie garde sa touche (nouvelle ligne, fin de saisie) ; un bouton choisi reçoit l'Entrée de Unity.
        if (_focusAvant || (choisi != null && choisi.activeInHierarchy && choisi.GetComponent<TMP_InputField>() == null)) return;
        var fenetre = FenetreDuDessus();
        if (fenetre == null) return;
        var boutons = fenetre.GetComponentsInChildren<Button>(false).Where(b => b.IsInteractable() && EstEnregistrer(Libelle(b))).ToList();
        if (boutons.Count == 1) boutons[0].onClick.Invoke();   // deux candidats : on ne devine pas
    }

    /// La fenêtre ouverte au-dessus de tout : le dernier enfant actif du canevas racine qui a un bouton, hors message, petit message
    /// du bas (son « Annuler » rétablit une suppression) et écran principal (null).
    static Transform FenetreDuDessus()
    {
        foreach (var canvas in FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).OrderByDescending(c => c.sortingOrder))
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                var t = canvas.transform.GetChild(i);
                if (!t.gameObject.activeInHierarchy || t.GetComponent<ConfirmDialog>() != null || t.GetComponent<UndoToast>() != null) continue;
                if (!t.GetComponentsInChildren<Button>(false).Any(b => b.IsInteractable())) continue;
                return i == 0 || t.GetComponentInChildren<BatimentManager>(true) != null ? Interne(null) : t;
            }
        return null;
    }

    /// BZ7 (22/11) : « Ajouter un achat » et « Ajouter des travaux » sont des fenêtres rangées DANS la fiche du bâtiment (prefab), pas
    /// sous le canevas : celle qui contient `t`, ou celle qui est ouverte (t null) ; null sinon.
    static Transform Interne(Transform t)
    {
        // Sans « ?? » : dans l'éditeur, un composant absent n'est pas un vrai null.
        Component achat = t != null ? t.GetComponentInParent<AchatFormPanel>() : FindObjectOfType<AchatFormPanel>();
        if (achat != null) return achat.transform;
        Component travaux = t != null ? t.GetComponentInParent<TravauxFormPanel>() : FindObjectOfType<TravauxFormPanel>();
        return travaux != null ? travaux.transform : null;
    }

    static Button Bouton(Transform fenetre, System.Func<string, bool> regle)
        => fenetre.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.IsInteractable() && regle(Libelle(b)));

    static string Libelle(Button b)
    {
        var t = b.GetComponentInChildren<TMP_Text>(false);
        return t != null ? t.text : "";
    }

    /// Pures, testées (BY4) : le bouton qu'Échap déclenche, et celui d'Entrée (jamais un envoi).
    public static bool EstAnnuler(string libelle)
    {
        var l = (libelle ?? "").Replace("←", "").Trim().ToLowerInvariant();
        return l == "annuler" || l == "fermer" || l == "retour" || l == "fermer sans valider";
    }

    public static bool EstEnregistrer(string libelle)
    {
        var l = (libelle ?? "").Trim().ToLowerInvariant();
        // « Modifier » tout court (Gestion du loyer, Modalités) ; pas « Modifier… » d'une liste, ni « Réinitialiser » (il remet le loyer à zéro).
        return (l.StartsWith("enregistrer") || l.StartsWith("sauvegarder") || l.StartsWith("valider") || l == "modifier")
               && !l.Contains("envoi") && !l.Contains("envoy");
    }

    static void Tabuler(bool arriere)
    {
        var es = EventSystem.current;
        var choisi = es != null ? es.currentSelectedGameObject : null;
        var champ = choisi != null ? choisi.GetComponent<TMP_InputField>() : null;
        // Dans un champ à plusieurs lignes (corps d'un e-mail), Tab reste une tabulation.
        if (champ == null || champ.lineType != TMP_InputField.LineType.SingleLine) return;

        var champs = Fenetre(champ.transform).GetComponentsInChildren<TMP_InputField>(false)
            .Where(c => c.isActiveAndEnabled && c.IsInteractable() && !c.readOnly).ToList();
        int i = champs.IndexOf(champ);
        if (i < 0 || champs.Count < 2) return;
        var coins = new Vector3[4];
        var centres = new List<Vector2>();
        float hauteur = float.MaxValue;
        foreach (var c in champs)
        {
            ((RectTransform)c.transform).GetWorldCorners(coins);
            centres.Add((coins[0] + coins[2]) / 2f);
            hauteur = Mathf.Min(hauteur, coins[1].y - coins[0].y);
        }
        var ordre = Ordre(centres, hauteur * 0.4f);
        int rang = ordre.IndexOf(i);
        var suivant = champs[ordre[(rang + (arriere ? -1 : 1) + ordre.Count) % ordre.Count]];
        Montrer((RectTransform)suivant.transform);
        suivant.Select();
        suivant.ActivateInputField();
        suivant.MoveTextEnd(false);
    }

    /// L'ordre de lecture : de haut en bas, puis de gauche à droite ; deux champs sont sur la même ligne quand leurs centres
    /// sont à moins de `tolerance` l'un de l'autre en hauteur. Rend les indices de `centres`, dans cet ordre. Pure, testée.
    public static List<int> Ordre(IList<Vector2> centres, float tolerance)
    {
        var parHauteur = Enumerable.Range(0, centres.Count).OrderByDescending(i => centres[i].y).ToList();
        var ordre = new List<int>();
        for (int debut = 0; debut < parHauteur.Count;)
        {
            int fin = debut + 1;
            while (fin < parHauteur.Count && centres[parHauteur[debut]].y - centres[parHauteur[fin]].y <= tolerance) fin++;
            ordre.AddRange(parHauteur.GetRange(debut, fin - debut).OrderBy(i => centres[i].x));
            debut = fin;
        }
        return ordre;
    }

    /// La fenêtre du champ : l'enfant du canevas racine qui le contient (une fenêtre ouverte par-dessus est un autre enfant).
    static Transform Fenetre(Transform t)
    {
        var interne = Interne(t);
        if (interne != null) return interne;
        var canvas = t.GetComponentInParent<Canvas>();
        var racine = canvas != null ? canvas.rootCanvas.transform : null;
        while (t.parent != null && t.parent != racine) t = t.parent;
        return t;
    }

    /// Le champ qui prend la main est amené dans la zone visible de sa liste qui défile.
    static void Montrer(RectTransform champ)
    {
        var defil = champ.GetComponentInParent<ScrollRect>();
        if (defil == null || !defil.vertical || defil.content == null) return;
        var vue = defil.viewport != null ? defil.viewport : (RectTransform)defil.transform;
        var c = new Vector3[4]; champ.GetWorldCorners(c);
        var v = new Vector3[4]; vue.GetWorldCorners(v);
        float marge = (c[1].y - c[0].y) * 0.5f, d = 0f;
        if (c[0].y < v[0].y) d = v[0].y - c[0].y + marge;          // sous la zone : le contenu monte
        else if (c[1].y > v[1].y) d = v[1].y - c[1].y - marge;     // au-dessus : il descend
        if (Mathf.Approximately(d, 0f)) return;
        var p = defil.content.position; p.y += d; defil.content.position = p;
    }
}
