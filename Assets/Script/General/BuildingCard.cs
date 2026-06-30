using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingCard : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text txtNom;
    public TMP_Text txtAdresse;
    public TMP_Text txtNbLocataires;
    public TMP_Text txtLoyer;
    public Button btnOuvrir;

    [Header("Alerte révision")]
    public GameObject alertBadge;
    public TMP_Text txtAlerteBadge;
    public Image alertBadgeBg;

    private static readonly Color ColorEnRetard = new Color(0.95f, 0.20f, 0.20f);
    private static readonly Color ColorUrgent = new Color(0.95f, 0.55f, 0.10f);
    private static readonly Color ColorBientot = new Color(0.20f, 0.60f, 0.95f);

    private const int SEUIL_URGENT = 30;
    private const int SEUIL_BIENTOT = 90;

    public void Setup(BatimentPrefab batiment, Action<BatimentPrefab> onClick)
    {
        var data = batiment.GetBatimentData();
        int nbLoc = batiment.listLocataire.Count;

        if (txtNom != null)
            txtNom.text = string.IsNullOrEmpty(data.Name) ? "Bâtiment sans nom" : data.Name;

        if (txtAdresse != null)
            txtAdresse.text = string.IsNullOrEmpty(data.adressBatiment) ? "—" : data.adressBatiment;

        if (txtNbLocataires != null)
            txtNbLocataires.text = $"{nbLoc} locataire{(nbLoc > 1 ? "s" : "")}";

        if (txtLoyer != null)
            txtLoyer.text = $"{batiment.GetLoyerTotal():F0} € / an";

        btnOuvrir.onClick.RemoveAllListeners();
        btnOuvrir.onClick.AddListener(() => onClick?.Invoke(batiment));

        RefreshRevisionAlert(batiment.listLocataire);
    }

    private void RefreshRevisionAlert(List<Locataire> locataires)
    {
        if (alertBadge == null) return;

        var today = DateTime.Today;
        int retard = 0, urgent = 0, bientot = 0;
        int joursMin = int.MaxValue;

        foreach (var loc in locataires)
        {
            if (string.IsNullOrEmpty(loc.indiceImmoAuDepart) || loc.indiceImmoAuDepart == "—")
                continue;

            int jours = (loc.MoisDeRevision - today).Days;

            if (jours < 0) { retard++; joursMin = Math.Min(joursMin, jours); }
            else if (jours <= SEUIL_URGENT) { urgent++; joursMin = Math.Min(joursMin, jours); }
            else if (jours <= SEUIL_BIENTOT) { bientot++; joursMin = Math.Min(joursMin, jours); }
        }

        int total = retard + urgent + bientot;

        if (total == 0) { alertBadge.SetActive(false); return; }

        alertBadge.SetActive(true);

        if (retard > 0)
        {
            if (alertBadgeBg != null) alertBadgeBg.color = ColorEnRetard;
            txtAlerteBadge.text = retard == 1 ? "⚠ Révision en retard" : $"⚠ {retard} révisions en retard";
        }
        else if (urgent > 0)
        {
            if (alertBadgeBg != null) alertBadgeBg.color = ColorUrgent;
            txtAlerteBadge.text = urgent == 1 ? $"🔔 Révision dans {joursMin}j" : $"🔔 {urgent} révisions < {SEUIL_URGENT}j";
        }
        else
        {
            if (alertBadgeBg != null) alertBadgeBg.color = ColorBientot;
            txtAlerteBadge.text = bientot == 1 ? $"📅 Révision dans {joursMin}j" : $"📅 {bientot} révisions à venir";
        }
    }
}