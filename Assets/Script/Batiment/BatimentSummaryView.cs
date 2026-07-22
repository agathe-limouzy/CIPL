using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Vue résumé d'un bâtiment : carte d'identité (vignette, pilules, infos clés)
/// + liste des locataires + bande financière (loyers, cash flow, rendement).
public class BatimentSummaryView : MonoBehaviour
{
    [Header("Carte bâtiment")]
    public RawImage vignetteCarte;     // miroir de la carte Mapbox de la fiche
    public TMP_Text txtNom;
    public TMP_Text txtAdresse;
    public TMP_Text txtPiluleSurface;
    public TMP_Text txtPiluleLots;
    public TMP_Text txtPiluleParking;
    public TMP_Text txtAcquis;
    public TMP_Text txtTerrain;
    public TMP_Text txtTravaux;
    public TMP_Text txtObjectifs;
    public Button btnFicheComplete;

    [Header("Locataires")]
    public TMP_Text txtTitreLocataires;
    public Button btnAjouterLocataire;
    public Transform rowContainer;     // rempli par BatimentPrefab.RebuildLocataireRows

    [Header("Bande financière")]
    public TMP_Text txtFinances;
    public Button btnDetailsFinanciers;

    private BatimentPrefab _bp;

    public void Init(BatimentPrefab bp)
    {
        _bp = bp;

        btnFicheComplete.onClick.RemoveAllListeners();
        btnFicheComplete.onClick.AddListener(() => _bp.ShowFiche());

        if (btnDetailsFinanciers != null)
        {
            btnDetailsFinanciers.onClick.RemoveAllListeners();
            btnDetailsFinanciers.onClick.AddListener(() => _bp.ShowFiche());
        }

        if (btnAjouterLocataire != null)
        {
            btnAjouterLocataire.onClick.RemoveAllListeners();
            btnAjouterLocataire.onClick.AddListener(() =>
            {
                _bp.ShowLocataireView();
                var prefab = _bp.Addlocataire(true);
                _bp.menulocataire.CreateTab(prefab);
                _bp.menulocataire.OnSelect(prefab, true);
            });
        }

        // La vignette suit la carte de la fiche (même texture)
        if (_bp.mapController != null)
        {
            _bp.mapController.AdressUpdate.RemoveListener(RefreshVignette);
            _bp.mapController.AdressUpdate.AddListener(RefreshVignette);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_bp == null) return;
        var bat = _bp.getBatiment();
        if (bat == null) return;

        Set(txtNom, string.IsNullOrEmpty(bat.Name) ? "Sans nom" : bat.Name);
        Set(txtAdresse, string.IsNullOrEmpty(bat.adressBatiment) ? "Adresse non renseignée" : bat.adressBatiment);

        // Pilules
        Set(txtPiluleSurface, $"{bat.tailleBatiment:F0} m²");
        int nbLots = bat.locataireDuBatiment.Count;
        Set(txtPiluleLots, nbLots > 1 ? $"{nbLots} lots" : $"{nbLots} lot");
        Set(txtPiluleParking, LabelParking(bat.parkingEtat));

        // Grille d'infos
        Set(txtAcquis, DateAcquisition(bat));
        Set(txtTerrain, bat.tailleTerrain > 0 ? $"{bat.tailleTerrain:F0} m²" : "—");
        Set(txtTravaux, ResumeTravaux(bat));
        Set(txtObjectifs, ResumeObjectifs(bat));

        // Titre section locataires
        float loyerTotal = _bp.GetLoyerTotal();
        Set(txtTitreLocataires,
            $"Locataires   <size=70%><color=#5F5E5A>{nbLots} lot{(nbLots > 1 ? "s" : "")} · {loyerTotal:N0} € / an</color></size>");

        // Bande financière : loyers · cash flow · rendement
        CalculFinances(bat, loyerTotal, out float cashFlowMois, out float rendementNet, out bool aDesCredits);
        string signe = cashFlowMois >= 0 ? "+" : "";
        string couleurCF = cashFlowMois >= 0 ? "#0F6E56" : "#D85A30";
        string finances = $"Loyers  <b>{loyerTotal:N0} € / an</b>";
        if (aDesCredits || cashFlowMois != 0)
            finances += $"      Cash flow  <b><color={couleurCF}>{signe}{cashFlowMois:N0} € / mois</color></b>";
        if (rendementNet > 0)
            finances += $"      Rendement  <b>{rendementNet:F1} %</b>";
        Set(txtFinances, finances);

        RefreshVignette();
    }

    private void RefreshVignette()
    {
        if (vignetteCarte == null || _bp == null || _bp.mapController == null) return;
        var mapImg = _bp.mapController.mapImage;
        if (mapImg != null && mapImg.texture != null)
        {
            vignetteCarte.texture = mapImg.texture;
            vignetteCarte.color = Color.white;
        }
    }

    // ── Calculs ───────────────────────────────────────────────────────────────

    private static void CalculFinances(Batiment bat, float loyerAnnuel,
        out float cashFlowMois, out float rendementNet, out bool aDesCredits)
    {
        float investTotal = 0f;
        float mensualites = 0f;
        aDesCredits = false;

        if (bat.historiquesAchat != null)
            foreach (var a in bat.historiquesAchat)
            {
                investTotal += a.prixAchat + a.fraisNotaire + a.fraisAgence;
                if (a.emprunt && a.dureeMois > 0)
                {
                    mensualites += RentabiliteCalculator.Mensualite(
                        a.montantEmprunte, a.tauxInteretAnnuel, a.dureeMois);
                    aDesCredits = true;
                }
            }

        if (bat.travaux != null)
            foreach (var t in bat.travaux)
            {
                investTotal += t.coutTotal;
                if (t.emprunt && t.dureeMois > 0)
                {
                    mensualites += RentabiliteCalculator.Mensualite(
                        t.montantEmprunte, t.tauxInteretAnnuel, t.dureeMois);
                    aDesCredits = true;
                }
            }

        float cashFlowAnnuel = loyerAnnuel - mensualites * 12f;
        cashFlowMois = cashFlowAnnuel / 12f;
        rendementNet = investTotal > 0 ? cashFlowAnnuel / investTotal * 100f : 0f;
    }

    private static string DateAcquisition(Batiment bat)
    {
        if (bat.historiquesAchat == null || bat.historiquesAchat.Count == 0) return "—";
        DateTime plusAncienne = DateTime.MaxValue;
        foreach (var a in bat.historiquesAchat)
            if (DateTime.TryParse(a.dateAchat, out var d) && d < plusAncienne)
                plusAncienne = d;
        return plusAncienne == DateTime.MaxValue ? "—" : plusAncienne.ToString("dd/MM/yyyy");
    }

    private static string ResumeTravaux(Batiment bat)
    {
        if (bat.travaux == null || bat.travaux.Count == 0)
            return bat.travauxEnCours ? "En cours" : "Aucun";

        // Le plus récent avec description
        var dernier = bat.travaux[bat.travaux.Count - 1];
        string desc = string.IsNullOrEmpty(dernier.description) ? "Travaux" : dernier.description;
        if (desc.Length > 24) desc = desc.Substring(0, 24) + "…";

        if (DateTime.TryParse(dernier.dateDebutTravaux, out var debut) && dernier.dureeMois > 0)
        {
            var fin = debut.AddMonths(dernier.dureeMois);
            if (fin > DateTime.Now)
                return $"{desc} — fin {fin:MM/yyyy}";
        }
        return bat.travauxEnCours ? $"{desc} (en cours)" : $"{bat.travaux.Count} enregistrés";
    }

    private static string ResumeObjectifs(Batiment bat)
    {
        var items = bat.objectifs?.items;
        int aFaire = 0, obligatoires = 0;
        if (items != null)
            foreach (var o in items)
            {
                if (o.status == Objective.ObjectiveStatus.Fait) continue;
                if (o.status == Objective.ObjectiveStatus.Obligatoire) obligatoires++;
                else aFaire++;
            }

        if (aFaire == 0 && obligatoires == 0) return "Aucun en cours";
        string s = $"{aFaire} à faire";
        if (obligatoires > 0)
            s += $" · <color=#D85A30>{obligatoires} obligatoire{(obligatoires > 1 ? "s" : "")}</color>";
        return s;
    }

    private static string LabelParking(ParkingState p) => p switch
    {
        ParkingState.ParkingPrive => "Parking privé",
        ParkingState.ParkingEnCopropiete => "Parking copro",
        _ => "Sans parking"
    };

    private static void Set(TMP_Text t, string v) { if (t != null) t.text = v; }
}
