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

    public static float Montant(float ht, float taux) => Cents(ht * taux / 100f);
    public static float Ttc(float ht, float taux) => ht + Montant(ht, taux);
    /// Le HT d'un montant TTC mémorisé (suivi), au taux de l'époque.
    public static float Ht(float ttc, float taux) => taux <= 0f ? ttc : Cents(ttc / (1f + taux / 100f));
    /// « TVA 20% », « TVA 5,5% » : le libellé de la ligne du tableau.
    public static string Libelle(float taux) => "TVA " + taux.ToString("0.#", FacturePdfService.FrCulture) + "%";
    /// La mention imprimée sur une facture exonérée (Réglages › Textes fixes, sinon le texte d'usine).
    public static string Mention(ReglageData r = null) => FacturePdfService.Texte((r ?? ReglageService.Current)?.mentionTvaExoneration, ExonerationDefaut);
    static float Cents(float v) => Mathf.Round(v * 100f) / 100f;
}
