using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// Rangée de colonnes qui passe en pile quand elles n'entrent plus côte à côte.
///
/// Posé sur une rangée (HorizontalLayoutGroup) dont le parent est un groupe
/// vertical. Quand la somme des largeurs minimum des colonnes dépasse la largeur
/// offerte — ce qui arrive aux zooms 150 et 175 % —, les colonnes après la
/// première descendent sous la rangée, dans le parent. Elles remontent quand la
/// place revient.
///
/// Ne marche que si chaque colonne dit la vérité sur sa largeur minimum : un
/// LayoutElement.minWidth plus petit que son contenu fait croire qu'elles tiennent,
/// et le contenu sort alors de sa colonne (« Créances » annonçait 300 pour 712).
[RequireComponent(typeof(HorizontalLayoutGroup))]
public class ColonnesAdaptatives : MonoBehaviour
{
    /// Après chaque changement de disposition : l'écran qui calcule ses hauteurs
    /// lui-même (menu général) doit le refaire.
    public event Action Change;

    readonly List<Transform> _descendues = new List<Transform>();
    float _largeur = -1f, _exigee = -1f;
    HorizontalLayoutGroup _groupe;

    // La largeur change avec le zoom ou la fenêtre. Pas dans
    // OnRectTransformDimensionsChange : il est appelé en plein calcul de layout, où
    // déplacer un enfant déclenche des erreurs de reconstruction.
    // 22/11 : aussi quand la largeur EXIGÉE change. Un Disposer appelé juste après avoir rempli une colonne (Refresh de l'accueil)
    // lit encore sa largeur minimum d'avant (elle n'est recalculée qu'à la mise en page, en fin d'image) : « À traiter » vidée
    // puis remplie à 175 % faisait remonter « Créances » à côté, hors de l'écran, sans jamais la redescendre.
    void LateUpdate()
    {
        float offerte = LargeurOfferte(), exigee = LargeurExigee();
        if (Mathf.Approximately(offerte, _largeur) && Mathf.Approximately(exigee, _exigee)) return;
        _largeur = offerte; _exigee = exigee;
        Disposer();
    }

    /// Largeur réellement offerte à la rangée : celle de la zone de défilement (ou
    /// de l'écran) qui la contient, moins les marges des groupes traversés.
    ///
    /// Pas celle du parent : un parent qui se dimensionne sur son contenu s'élargit
    /// avec la rangée, et elle croit alors toujours tenir. C'est ce qui arrivait à
    /// la fiche locataire à 175 % : 1168 exigées, parent élargi à 1188, écran à 1077.
    // ponytail: suppose des groupes verticaux jusqu'à la zone de défilement ; une
    // rangée logée dans une colonne voisine d'une autre demanderait de partager.
    public float LargeurOfferte()
    {
        float marges = 0f;
        for (var t = transform.parent as RectTransform; t != null; t = t.parent as RectTransform)
        {
            var sr = t.parent != null ? t.parent.GetComponent<ScrollRect>() : null;
            if ((sr != null && sr.viewport == t) || t.GetComponent<ScrollRect>() != null || t.GetComponent<Canvas>() != null)
                return t.rect.width - marges;
            var g = t.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (g != null) marges += g.padding.horizontal;
        }
        return 0f;
    }

    /// Largeur que la rangée exige pour garder ses colonnes côte à côte.
    public float LargeurExigee()
    {
        // Lue à chaque image (LateUpdate) : sans liste ni énumérateur.
        if (_groupe == null) _groupe = GetComponent<HorizontalLayoutGroup>();
        float somme = 0f; int n = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c.gameObject.activeSelf) { somme += LayoutUtility.GetMinWidth((RectTransform)c); n++; }
        }
        for (int i = 0; i < _descendues.Count; i++)
        {
            var c = _descendues[i];
            if (c != null && c.gameObject.activeSelf) { somme += LayoutUtility.GetMinWidth((RectTransform)c); n++; }
        }
        return somme + _groupe.spacing * Mathf.Max(0, n - 1) + _groupe.padding.horizontal;
    }

    public void Disposer()
    {
        var parent = transform.parent as RectTransform;
        if (parent == null) return;
        float offerte = LargeurOfferte();
        if (offerte <= 0f) return;   // pas encore mis en page

        bool empiler = LargeurExigee() > offerte;
        if (empiler == (_descendues.Count > 0)) return;

        if (empiler)
        {
            var aDescendre = new List<Transform>();
            foreach (Transform c in transform) if (c.gameObject.activeSelf) aDescendre.Add(c);
            if (aDescendre.Count < 2) return;                         // une seule colonne : rien à empiler
            aDescendre.RemoveAt(0);                                   // la première reste en place
            int index = transform.GetSiblingIndex();
            foreach (var c in aDescendre)
            {
                c.SetParent(parent, false);
                c.SetSiblingIndex(++index);
                _descendues.Add(c);
            }
        }
        else
        {
            foreach (var c in _descendues)
                if (c != null) { c.SetParent(transform, false); c.SetAsLastSibling(); }
            _descendues.Clear();
        }
        Change?.Invoke();
    }
}
