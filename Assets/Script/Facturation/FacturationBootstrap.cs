using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Amorce le module Facturation sans toucher la scène : à chaque lancement,
/// ajoute un bouton « Réglage » sur le menu Home (clone du bouton Sauvegardes),
/// câblé sur ReglagePanel.OpenPanel (qui crée le panneau à la volée au 1er clic).
public static class FacturationBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var gmp = GeneralMenuPanel.Instance;
        if (gmp == null || gmp.btnSauvegardes == null) return;
        if (GameObject.Find("BtnReglage") != null) return;

        var src = gmp.btnSauvegardes;
        var clone = Object.Instantiate(src.gameObject, src.transform.parent);
        clone.name = "BtnReglage";
        // La source peut être déjà masquée par EntrepriseBootstrap : l'ordre des deux amorces
        // n'est pas garanti, et une copie d'un bouton masqué naît masquée (retour du 15/10).
        clone.SetActive(true);
        clone.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);

        var btn = clone.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(ReglagePanel.OpenPanel);

        var label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Réglage";
    }
}
