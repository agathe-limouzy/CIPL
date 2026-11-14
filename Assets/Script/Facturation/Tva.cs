using System;
using UnityEngine;

/// TVA sur les loyers (chantier du 02/11, décisions du 02/11) : l'option du bailleur se règle par entreprise (Réglages ›
/// Facturation : soumis ou non, et le taux) ; chaque bail peut déroger (Gestion du loyer : comme l'entreprise, oui, non, et son
/// taux) ; chaque facture peut changer le taux (champ « Taux de TVA »). La régularisation et la refacturation suivent le
/// loyer. Un bail exonéré : total seul et mention d'exonération sur la facture, pas de facture électronique (les locations
/// exonérées de l'article 261 D sont hors de l'obligation), TTC = total sans TVA sur les cartes.
public static class Tva
{
    public const float TauxDefaut = 20f;
    public const string ExonerationDefaut = "TVA non applicable – article 261 D du CGI";

    /// Le loyer de ce bail est-il soumis à la TVA ? Le bail d'abord (« oui » / « non », ou son propre taux : un bail réglé à la
    /// main reste soumis quand l'entreprise ne l'est plus, BK4 du 02/11), sinon l'entreprise.
    public static bool Soumis(Locataire loc, ReglageData r = null)
    {
        r ??= ReglageService.Current;
        if (loc?.tvaLoyer == "oui") return true;
        if (loc?.tvaLoyer == "non") return false;
        return (loc != null && loc.tauxTvaLoyer > 0f) || (r?.loyersSoumisTva ?? true);
    }

    /// Taux en pour cent (0 = exonéré) : celui de la facture s'il est posé, sinon celui du bail, sinon celui de l'entreprise.
    public static float Taux(Locataire loc, FactureInfo f = null, ReglageData r = null)
    {
        r ??= ReglageService.Current;
        if (!Soumis(loc, r)) return 0f;
        if (f != null && f.tauxTva > 0f) return f.tauxTva;
        if (loc != null && loc.tauxTvaLoyer > 0f) return loc.tauxTvaLoyer;
        return r != null && r.tauxTva > 0f ? r.tauxTva : TauxDefaut;
    }

    /// Revue 17 (D5-08, lot 10) : la ligne visée est une facture PARTIE (envoyée, impayée ou payée : le test de
    /// FactureLoyerPanel.ProvisionProposee) : le formulaire la corrige, et ce qu'il propose vient d'elle.
    public static bool FacturePartie(FactureEtat ligne)
    {
        if (ligne == null) return false;
        var e = FacturationSuivi.EtatDe(ligne, null);   // sans locataire (D4-05) : « Envoyé » ou « Impayé », la réponse est la même
        // Finitions de la vague 3 (10/11) : un état « Payé », « Envoyé » ou « Impayé » posé à la main, sans PDF, n'est pas une facture :
        // l'émission en fera une facture neuve (FacturationSuivi.EstDejaEmise). Avant : son taux d'office (20 %) était proposé, même sur un bail exonéré.
        return (e == FacturationSuivi.Etat.Envoye || e == FacturationSuivi.Etat.Impaye || e == FacturationSuivi.Etat.Paye) && !string.IsNullOrEmpty(ligne.pdfPath);
    }

    /// Revue 17 (D5-08, lot 10 ; D5-N7) : le taux que propose un formulaire : pour la correction d'une facture partie, celui imprimé
    /// sur sa dernière version (ligne.tauxTva, 0 compris : une facture exonérée se corrige exonérée, et un bail devenu exonéré ne
    /// force plus 0) ; sinon Taux(loc, f). Avant : le taux du jour, sans le dire (rectificative à 10 % sur une période taxée à 20 %).
    public static float TauxPropose(Locataire loc, FactureInfo f, FactureEtat ligneCiblee, ReglageData r = null)
        => FacturePartie(ligneCiblee) ? ligneCiblee.tauxTva : Taux(loc, f, r);

    /// Revue 17 (D5-17, coutures) : les taux de TVA courants : 20, 10, 5,5 et 2,1 (métropole), 8,5 (taux normal des DOM) ; 0 = sans TVA.
    /// Un autre taux n'est pas refusé (Corse, cas particuliers) : il se confirme (ConfirmerTaux).
    public static bool TauxConnu(float t) => Array.Exists(new[] { 0f, 20f, 10f, 8.5f, 5.5f, 2.1f }, x => Mathf.Abs(x - t) < 0.001f);

    /// Le taux saisi est-il à confirmer ? Un taux inhabituel (« 2 » tapé pour « 20 »), sauf s'il est déjà en place (`enPlace` : le taux
    /// enregistré, ou celui que le formulaire proposait) : il a déjà été confirmé. Avant : tout nombre positif passait sans un mot.
    public static bool TauxAConfirmer(float taux, float enPlace) => !TauxConnu(taux) && Mathf.Abs(taux - enPlace) > 0.001f;

    /// La même règle aux trois niveaux (entreprise : Réglages ; bail : Gestion du loyer ; facture : loyer, régularisation, refacturation) :
    /// un taux à confirmer ouvre une confirmation, et `suite` s'exécute au « Confirmer ce taux » ; « Annuler » laisse l'écran ouvert avec
    /// la saisie, rien n'est enregistré ni émis. Sinon `suite` tout de suite.
    public static void ConfirmerTaux(float taux, float enPlace, Action suite)
    {
        if (!TauxAConfirmer(taux, enPlace) || ConfirmDialog.Instance == null) { suite(); return; }
        ConfirmDialog.Instance.Show("Taux de TVA inhabituel",
            $"Le taux de TVA saisi est de {taux.ToString("0.##", FacturePdfService.FrCulture)} %. Les taux courants sont 20, 10, 8,5, 5,5 et 2,1 %.\n\n"
            + "Confirmez ce taux, ou annulez pour le corriger.",
            suite, "Confirmer ce taux");
    }

    /// Revue 17 (D5-03) : la TVA se calcule en décimal, arrondie au centime « au plus loin », exactement comme le XML de la
    /// plateforme (FactureEn16931) : en float, un demi-centime s'arrondissait au pair (1 234,45 € à 10 % : 123,44 € sur le PDF,
    /// 123,45 € dans le XML).
    public static float Montant(float ht, float taux) => (float)Math.Round(Centimes(ht) * Centimes(taux) / 100m, 2, MidpointRounding.AwayFromZero);
    public static float Ttc(float ht, float taux) => ht + Montant(ht, taux);
    /// Le HT d'un montant TTC mémorisé (suivi), au taux de l'époque.
    public static float Ht(float ttc, float taux)
        => taux <= 0f ? ttc : (float)Math.Round(Centimes(ttc) / (1m + Centimes(taux) / 100m), 2, MidpointRounding.AwayFromZero);

    /// Revue 17 (D6-06) : la période de loyer qui sert de base au dépôt de garantie, la même sur la fiche et dans le formulaire :
    /// TTC au taux du bail si la case est cochée et le bail soumis, HT sinon (avant : × 1,2 en dur sur la fiche).
    public static float PeriodeDepot(Locataire loc, bool surTTC)
    {
        if (loc == null) return 0f;
        float ht = loc.loyerAnnuel / Mathf.Max(1, LoyerSummaryUI.NbPeriodes(loc.periodiciteLoyer));
        return surTTC && Soumis(loc) ? Ttc(ht, Taux(loc)) : ht;
    }
    /// « TVA 20% », « TVA 5,5% » : le libellé de la ligne du tableau.
    public static string Libelle(float taux) => "TVA " + taux.ToString("0.#", FacturePdfService.FrCulture) + "%";
    /// La mention imprimée sur une facture exonérée (Réglages › Textes fixes, sinon le texte d'usine).
    public static string Mention(ReglageData r = null) => FacturePdfService.Texte((r ?? ReglageService.Current)?.mentionTvaExoneration, ExonerationDefaut);
    /// Revue 17 (D5-03) : l'arrondi au centime de toute la facturation (PDF, panneaux, XML) : décimal, « au plus loin » ; la valeur
    /// exacte du float (double), pas ses 7 chiffres (un HT de 123 456,78 € perdait ses centimes en (decimal)float).
    public static decimal Centimes(float x) => Math.Round((decimal)(double)x, 2, MidpointRounding.AwayFromZero);
    public static float Cents(float v) => (float)Centimes(v);
}
