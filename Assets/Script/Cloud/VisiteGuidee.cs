using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// Étape 13b (maquette validée le 25/10, revue en 13b-2) : une courte visite de l'accueil, montrée une
/// fois à chaque personne (retenu dans son compte), relançable par Compte › « Revoir la visite guidée ».
/// Elle se termine par « C'est parti » : « Commencer » lance le guide de la première étape à faire.
public static class VisiteGuidee
{
    /// Les bulles, dans l'ordre (testées) ; une zone absente de l'écran est sautée, et « C'est parti »
    /// aussi quand il n'y a plus rien à faire.
    public static readonly (string titre, string texte)[] Etapes =
    {
        ("Bienvenue dans CIPL", "CIPL gère vos bâtiments, vos locataires et leurs factures, à plusieurs et en direct. Voici l'accueil en quelques bulles."),
        ("Vos bâtiments", "Chaque immeuble ou local a son onglet, créé avec ce « + ». Dans sa fiche, vous ajoutez ses locataires, leur bail et leur loyer."),
        ("Créances", "Ce que vos locataires vous doivent. Une facture émise apparaît ici jusqu'à son paiement ; passée l'échéance, elle passe en rouge dans « Impayé ». « À traiter », à côté, rappelle les loyers à facturer, les révisions et les fins de bail."),
        ("Réglages et compte", "« Réglage » : votre logo, vos RIB, l'envoi des factures, vos collègues et l'abonnement. « Compte » : changer d'entreprise ou de mot de passe, revoir cette visite, vous déconnecter."),
        ("C'est parti", "Je vous accompagne pas à pas jusqu'à votre première facture. Comptez une quinzaine de minutes ; vous pouvez vous arrêter à tout moment : la carte « Premiers pas » de l'accueil garde votre avancement."),
    };

    /// Au premier lancement de la personne, une fois l'accueil seul à l'écran (proposition d'envoi
    /// des factures, message… fermés). Au bout d'une minute d'attente : la prochaine fois.
    public static async void LancerSiNouvelle()
    {
        if (!SessionCloud.Active || SessionCloud.Retenu.cipl_visite_vue) return;
        for (int i = 0; i < 120 && Occupe(); i++) await Task.Delay(500);
        if (!Occupe()) Lancer();
    }

    static bool Occupe() =>
        GeneralMenuPanel.Instance == null || !GeneralMenuPanel.Instance.gameObject.activeInHierarchy
        || UnityEngine.Object.FindAnyObjectByType<FactureEnvoiAuto>() != null
        || (ConfirmDialog.Instance != null && ConfirmDialog.Instance.gameObject.activeSelf)
        || GameObject.Find("CloudVoile") != null || Bulles.EnCours;

    public static void Lancer()
    {
        var cibles = new Func<RectTransform>[]
        {
            null,
            () => BatimentManager.Instance?.menuManager?.addTabButton?.transform as RectTransform,   // le « + » (celui de l'accueil est caché)
            () => GeneralMenuPanel.Instance?.Cible("RowTraiterCalcul/Section Creances"),
            () => GameObject.Find("BtnReglage")?.transform.parent as RectTransform,                  // Réglage, Compte…
            null,
        };
        var etapes = Etapes.Select((x, i) => new Bulles.Etape { titre = x.titre, texte = x.texte, cible = cibles[i] }).ToList();
        for (int i = 1; i <= 3; i++)
        {
            var c = cibles[i];
            etapes[i].sauter = () => c()?.gameObject.activeInHierarchy != true;
        }
        var partir = etapes[4];
        partir.sauter = () => PremiersPas.Prochaine(PremiersPas.Lignes(PremiersPas.Lire())) == null;
        partir.suivant = "Commencer ›";
        partir.quitter = "Plus tard";
        Bulles.Lancer(etapes, null, bloquer: true, quitter: "Passer la visite", fin: commencer =>
        {
            SessionCloud.Retenir(SessionCloud.VisiteVue);
            if (commencer) PremiersPas.Commencer();
        });
    }
}
