using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Retour de BW17 (14/11) : des adresses proposées sous le champ, au fil de la frappe, comme Google Maps (api-adresse,
/// autocomplete). Un clic choisit l'adresse et lance la recherche ; Entrée cherche le texte tapé, sans rien choisir ; Échap ferme.
/// Modèle : SlashAutocomplete (liste sous le canevas racine, placée par UIFactory.PlacePopup).
public class AdresseAutocomplete : MonoBehaviour
{
    public const int LongueurMin = 4;   // en dessous, trop de réponses au hasard

    TMP_InputField _input;
    RectTransform _popup, _inputRT;
    Action<string> _choisie;
    Func<(double lat, double lon)?> _pres;   // la zone à privilégier (retour de BW17)
    Func<string> _ville;                     // sans zone : la ville du lieu d'émission (décision du 14/11)
    string _attente;      // texte à chercher quand la frappe se calme
    float _quand;         // 0,3 s après la dernière touche
    int _generation;      // une réponse arrivée après une nouvelle frappe est ignorée

    public static AdresseAutocomplete Attach(TMP_InputField champ, Action<string> choisie, Func<(double lat, double lon)?> pres = null,
                                             Func<string> ville = null)
    {
        if (champ == null) return null;
        var go = new GameObject("AdresseAutocomplete", typeof(RectTransform));
        go.transform.SetParent(champ.transform, false);
        var a = go.AddComponent<AdresseAutocomplete>();
        a._input = champ; a._choisie = choisie; a._pres = pres; a._ville = ville; a._inputRT = (RectTransform)champ.transform;
        a.Construire();
        champ.onValueChanged.AddListener(a.SurFrappe);
        champ.onSubmit.AddListener(_ => { a._generation++; a._attente = null; a.Cacher(); });
        champ.onEndEdit.AddListener(_ => { if (a.isActiveAndEnabled) a.StartCoroutine(a.CacherBientot()); else a.Cacher(); });
        return a;
    }

    /// Le texte tapé mérite-t-il des suggestions ? Pur : testé (LotDCartesTests).
    public static bool AChercher(string texte) => (texte ?? "").Trim().Length >= LongueurMin;

    void Construire()
    {
        var canvas = _input.GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.rootCanvas.transform : _input.transform;
        var img = UIFactory.Panel("AdressePopup", parent, UITheme.Carte);
        UIFactory.Border(img.gameObject);
        _popup = (RectTransform)img.transform;
        _popup.anchorMin = _popup.anchorMax = Vector2.zero;
        _popup.pivot = new Vector2(0f, 1f);
        var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(4, 4, 4, 4); v.spacing = 2;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        img.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        img.gameObject.SetActive(false);
    }

    void SurFrappe(string texte)
    {
        _generation++;
        // Un texte posé par le code (ouverture de la fiche, suggestion choisie) ne cherche rien : seulement la frappe.
        if (!_input.isFocused || !AChercher(texte)) { _attente = null; Cacher(); return; }
        _attente = texte.Trim();
        _quand = Time.unscaledTime + 0.3f;
    }

    void Update()
    {
        if (_attente == null || Time.unscaledTime < _quand) return;
        string q = _attente; _attente = null;
        int gen = _generation;
        StartCoroutine(GeoCodingService.Suggestions(q, _pres?.Invoke(), _ville?.Invoke(), liste =>
        {
            if (this != null && gen == _generation && _input != null && _input.isFocused) Afficher(liste);
        }));
    }

    void Afficher(List<string> adresses)
    {
        if (adresses == null || adresses.Count == 0) { Cacher(); return; }
        // Masquées avant d'être détruites (fin d'image) : PlacePopup mesure la liste tout de suite, sans les anciennes lignes.
        foreach (Transform c in _popup) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        foreach (var adresse in adresses)
        {
            var b = UIFactory.Button(_popup, adresse, UITheme.Carte, UITheme.TextePrincipal, 30, UITheme.Role.Donnee, false);
            var t = b.GetComponentInChildren<TMP_Text>();
            if (t != null) t.alignment = TextAlignmentOptions.Left;
            string choix = adresse;
            b.onClick.AddListener(() => Choisir(choix));
        }
        _popup.gameObject.SetActive(true);
        _popup.SetAsLastSibling();
        _popup.sizeDelta = new Vector2(Mathf.Max(_inputRT.rect.width, 320f), _popup.sizeDelta.y);
        UIFactory.PlacePopup(_popup, _inputRT);
    }

    void Choisir(string adresse)
    {
        _generation++;
        _attente = null;
        _input.SetTextWithoutNotify(adresse);
        Cacher();
        _choisie?.Invoke(adresse);
    }

    // Le clic sur une suggestion fait perdre le focus au champ AVANT d'arriver au bouton : on attend un peu avant de cacher.
    IEnumerator CacherBientot()
    {
        yield return new WaitForSecondsRealtime(0.4f);
        if (_input == null || !_input.isFocused) Cacher();
    }

    void Cacher() { if (_popup != null) _popup.gameObject.SetActive(false); }

    void OnDisable() => Cacher();   // le champ se cache (fiche en lecture) : la liste aussi
    void OnDestroy() { if (_popup != null) Destroy(_popup.gameObject); }

    void OnGUI()
    {
        if (_popup == null || !_popup.gameObject.activeSelf) return;
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Cacher(); e.Use(); }
    }
}
