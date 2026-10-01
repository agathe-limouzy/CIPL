using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class InputAndText : MonoBehaviour
{


    public TMP_Text textSaved;
    public TMP_InputField inputModify;

    private ScrollAutoResize[] _scrollAutoResizes;
    private TMP_Text _quantite;

    // Vrai dès qu'un appel a choisi l'affichage (Modify, ShowSaveElement, ApplySave).
    // Awake ne tourne qu'à la PREMIÈRE activation de l'objet : sur une fiche construite
    // inactive (nouveau bâtiment, nouveau locataire), il passait APRÈS Modify() et
    // remettait tous les champs en lecture seule — le bouton « Sauvegarder » était là,
    // mais impossible de saisir le nom (constaté le 30/09).
    private bool _etatPose;


    private void Awake()
    {
        EnsureInit();
        if (_etatPose) return;   // un appel a déjà choisi : ne pas l'écraser
        // Dans les prefabs, le texte ET le champ de saisie sont actifs tous les deux
        // (vérifié : les 14 champs du bâtiment, les 11 du locataire). L'affichage
        // correct ne tenait donc qu'à un appel de Modify()/ApplySave() quelque part —
        // et tout champ qu'aucun de ces appels n'atteint montrait la valeur DEUX fois,
        // une en texte et une en saisie. Cas vécu : « Taille Batiment », que Modify()
        // sautait dès qu'il y avait plus d'un locataire.
        // L'état de repos est donc posé ici, une fois pour toutes : lecture seule.
        if (inputModify != null) inputModify.gameObject.SetActive(false);
        if (textSaved != null) textSaved.gameObject.SetActive(true);
    }

    // Awake ne tourne pas si l'objet est instancié sous un parent inactif :
    // on initialise à la demande.
    private void EnsureInit()
    {
        if (_scrollAutoResizes == null)
            _scrollAutoResizes = GetComponentsInParent<ScrollAutoResize>(true);
        if (_quantite == null)
        {
            var q = transform.Find("quantité");
            if (q != null) _quantite = q.GetComponent<TMP_Text>();
        }
    }

    // Masque l'unité (« quantité ») si elle est vide ou déjà contenue dans la valeur
    // (évite les doublons « 6 789 € € »). Sinon la garde (%, m²…).
    private void UpdateUnit()
    {
        if (_quantite == null) return;
        string unit = _quantite.text != null ? _quantite.text.Trim() : "";
        string val = textSaved != null ? textSaved.text : "";
        bool show = unit.Length > 0 && (val == null || !val.Contains(unit));
        _quantite.gameObject.SetActive(show);
    }

    public void Start()
    {
        inputModify.onValueChanged.AddListener(_ => CopyValue());
    }

    public void Modify()
    {
        EnsureInit();
        _etatPose = true;
        inputModify.gameObject.SetActive(true);
        textSaved.gameObject.SetActive(false);

        ForceRebuildLayout();
        foreach (var sar in _scrollAutoResizes)
            sar.SetDirty();
    }

    // Marque la hiérarchie jusqu'au ScrollRect pour reconstruction.
    //
    // MarkLayoutForRebuild et non ForceRebuildLayoutImmediate : Unity regroupe les
    // demandes et reconstruit UNE fois en fin d'image, au lieu de tout recalculer sur
    // place à chaque appel. Le rendu final est le même — ce qui changeait, c'était le
    // nombre de fois qu'on le calculait.
    //
    // Mesuré sur « Modifier + Sauvegarder » : la fiche réinitialise seize champs, et
    // chacun forçait un rebuild complet par niveau de hiérarchie. Environ 1,5 s de
    // layout pour un écran qui ne change qu'une fois.
    private void ForceRebuildLayout()
    {
        Transform t = transform;
        while (t != null)
        {
            var rt = t.GetComponent<RectTransform>();
            if (rt != null)
                LayoutRebuilder.MarkLayoutForRebuild(rt);

            if (t.GetComponent<ScrollRect>() != null) break;
            t = t.parent;
        }
    }


    public void ShowSaveElement()
    {
        EnsureInit();
        _etatPose = true;
        inputModify.gameObject.SetActive(false);
        textSaved.gameObject.SetActive(true);
        ForceRebuildLayout();
        foreach (var sar in _scrollAutoResizes)
            sar.SetDirty();

    }

    public void ApplySave(string text)
    {
       ApplyValue(text);
        ShowSaveElement();
    }

    public string GetNewSave()
    {

        ShowSaveElement();
        return GetValue();
    }
    public string GetValue()
    {
       
       CopyValue();
        return inputModify.text;
    }

    private  void CopyValue()
    {
        textSaved.text = inputModify.text;
        EnsureInit();
        UpdateUnit();
    }

    public void ApplyValue(string text)
    {
        inputModify.text = text;
        textSaved.text = text;
        EnsureInit();
        UpdateUnit();
    }

    public void SetPlaceholder(string text)
    {
        var ph = inputModify.placeholder?.GetComponent<TMP_Text>();
        textSaved.text = text;
        if (ph != null) ph.text = text;
    }

}
