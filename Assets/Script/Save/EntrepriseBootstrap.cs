using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Amorce le multi-entreprise sans toucher la scène : enregistre l'entreprise
/// active (1er lancement) et ajoute un bouton « Entreprises » au pied du menu Home
/// (clone du bouton Sauvegardes), câblé sur EntreprisePanel.OpenPanel.
public static class EntrepriseBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        EntrepriseService.EnsureActiveRegistered();

        var gmp = GeneralMenuPanel.Instance;
        if (gmp == null || gmp.btnSauvegardes == null) return;
        if (GameObject.Find("BtnEntreprises") != null) return;

        var src = gmp.btnSauvegardes;
        var clone = Object.Instantiate(src.gameObject, src.transform.parent);
        clone.name = "BtnEntreprises";
        clone.transform.SetSiblingIndex(src.transform.GetSiblingIndex());   // avant « Charger une save »

        var btn = clone.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(EntreprisePanel.OpenPanel);

        var label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Entreprises";
    }
}
