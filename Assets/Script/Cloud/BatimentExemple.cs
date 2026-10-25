using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// Étape 13b-3 (maquette validée le 25/10) : un bâtiment d'exemple pour s'entraîner sans risque.
/// « Essayer avec un exemple » (carte Premiers pas) le crée, avec un locataire complet (bail, loyer,
/// dépôt), prêt à facturer. Rien n'en part : pas d'e-mail au locataire, facture de loyer émise sans
/// envoi, hors de l'envoi automatique. Il ne compte pas dans les premiers pas ; un bandeau le signale,
/// avec « Supprimer l'exemple ».
public static class BatimentExemple
{
    public const string Nom = "Exemple · Les Tilleuls";

    public static bool Est(Batiment b) => b != null && b.exemple;

    /// Le bâtiment d'exemple tel qu'il est créé (testé) : facturable dès ce mois, sans loyer en retard.
    public static Batiment Construire(DateTime aujourdhui)
    {
        var debut = new DateTime(aujourdhui.Year, aujourdhui.Month, 1);
        var l = new Locataire
        {
            Name = "Exemple Boulangerie", lotBatiment = 1, tailleLot = 120f,
            typeRevision = TypeRevision.Aucune, periodiciteLoyer = Periodicite.mensuel, jourDemandeLoyer = 1,
            loyerAnnuel = 14400f, loyerDepart = 14400f, depotDeGarantie = 3600f, depotInitialise = true,
            dureeBailAns = 9, creationISO = aujourdhui.ToString("yyyy-MM-ddTHH:mm:ss"),
        };
        l.DateDebutBail = debut;
        l.DateFinBail = debut.AddYears(9).AddDays(-1);
        l.dateDebutPremierBailISO = l.dateDebutBailISO;
        var b = new Batiment { Name = Nom, adressBatiment = "12 rue des Tilleuls, 92190 Meudon", tailleBatiment = 120f, exemple = true };
        b.locataireDuBatiment.Add(l);
        return b;
    }

    public static BatimentPrefab Trouver() =>
        BatimentManager.Instance?.BatimentPrefab?.FirstOrDefault(p => p != null && Est(p.getBatiment()));

    /// « Essayer avec un exemple » : le crée (ou rouvre celui qui existe) et l'ouvre.
    public static void Ouvrir()
    {
        var bm = BatimentManager.Instance;
        if (bm == null) return;
        var deja = Trouver();
        if (deja != null) { bm.menuManager.OnSelect(deja); deja.ShowSummary(); return; }
        bm.RestoreBatiment(JsonUtility.ToJson(Construire(DateTime.Now)));
        UndoToast.Instance?.ShowInfo("Bâtiment d'exemple créé : essayez tout, rien n'est envoyé ni déposé chez Pennylane.");
    }

    /// Le bandeau orange en tête du résumé (BatimentSummaryView.Refresh).
    public static void Bandeau(Transform contenu, BatimentPrefab bp)
    {
        if (contenu == null) return;
        var existant = contenu.Find("BandeauExemple");
        if (!Est(bp != null ? bp.getBatiment() : null))
        {
            if (existant != null) UnityEngine.Object.Destroy(existant.gameObject);
            return;
        }
        if (existant != null) return;
        var bande = UIFactory.HBox(contenu, 10, false, "BandeauExemple");
        bande.padding = new RectOffset(14, 14, 8, 8);
        bande.childAlignment = TextAnchor.MiddleLeft;
        bande.gameObject.AddComponent<Image>().color = Hex("#FAEEDA");
        bande.transform.SetSiblingIndex(0);
        var t = UIFactory.Text(bande.transform, "Bâtiment d'exemple : essayez tout, rien n'est envoyé ni déposé chez Pennylane, et il ne compte pas dans vos premiers pas.",
                               UITheme.Role.Donnee, Hex("#633806"));
        UIFactory.LE(t.gameObject, flexW: 1);
        var b = UIFactory.Button(bande.transform, "Supprimer l'exemple", UITheme.Carte, Hex("#854F0B"), 34, UITheme.Role.Action);
        UIFactory.Border(b.gameObject, Hex("#854F0B"));
        UIFactory.LargeurDuTexte(b);
        b.onClick.AddListener(() => Supprimer(bp));
    }

    static void Supprimer(BatimentPrefab bp)
    {
        var bat = bp != null ? bp.getBatiment() : null;
        if (bat == null || ConfirmDialog.Instance == null) return;
        ConfirmDialog.Instance.Show("Supprimer l'exemple ?", $"« {bat.Name} » et son locataire partent à la corbeille.", () =>
        {
            BatimentManager.Instance?.DeleteBatiment(bat.id);
            BatimentManager.Instance?.menuManager?.OpenGeneralMenu();
        }, "Supprimer");
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
