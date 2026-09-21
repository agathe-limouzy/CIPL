using System;
using System.Globalization;

/// Bloc explicatif imprimé sur la facture de révision du dépôt de garantie, juste
/// sous le titre : d'où vient le nouveau loyer (indice de référence → nouvel indice)
/// et comment on en déduit le dépôt.
///
/// Le locataire doit pouvoir refaire le calcul lui-même sans nous appeler : c'est
/// tout l'objet de ce bloc. Les données viennent de la dernière révision de loyer
/// enregistrée sur la fiche — rien n'est recalculé ici.
public static class ExplicationDepot
{
    // ── Textes d'usine ──────────────────────────────────────────────────────────
    //
    // Ils vivent ici, à côté du code qui les imprime, et non dans les réglages : ce
    // sont des textes de la FACTURE DE DÉPÔT, pas de l'entreprise. Chaque facture
    // porte les siens (`FactureInfo.depot*`) ; ceux-ci servent de point de départ et
    // de filet — une phrase laissée vide retombe dessus, donc une facture antérieure
    // sort exactement comme avant.

    public const string RappelDefaut =
        "Je vous rappelle qu'il doit correspondre à {depot.termes} de loyer {depot.base}";

    public const string DuDefaut =
        "Et ainsi, il ressort que vous nous devez : <b>{depot.montant}</b> " +
        "somme que je vous demande de nous régler par retour de courrier.";

    public const string RembourseDefaut =
        "Et ainsi, il ressort que nous vous devons : <b>{depot.montant}</b> " +
        "somme qui vous sera remboursée.";

    public const string EquilibreDefaut =
        "Le dépôt de garantie déjà versé correspond au montant requis : " +
        "aucun ajustement n'est nécessaire.";

    /// Texte retenu : celui de la facture s'il est renseigné, sinon celui d'usine.
    public static string Ou(string texteFacture, string defaut)
        => string.IsNullOrWhiteSpace(texteFacture) ? defaut : texteFacture;

    /// Construit le HTML. `nbTermes` = nombre de termes de loyer que représente le
    /// dépôt, `complement` = somme réclamée, `dateEffet` = prise d'effet du loyer révisé.
    /// `bat` et `depot` sont optionnels : le premier ne sert qu'à résoudre les variables
    /// {bat.*}, le second porte les phrases propres à cette facture. Les appels
    /// antérieurs continuent donc de fonctionner sans eux.
    public static string Html(Locataire loc, int nbTermes, float complement, DateTime dateEffet,
                             Batiment bat = null, FactureInfo depot = null)
    {
        if (loc == null) return "";

        string baseCalcul = loc.depotSurTTC ? "T.T.C." : "H.T.";
        var sb = new System.Text.StringBuilder();

        // ── Partie loyer : uniquement si une révision a réellement eu lieu ──────
        // Sans révision, `loyerAnnuelPrecedent` vaut 0 et l'indice actuel « — » :
        // un tableau d'indexation n'aurait alors aucun sens.
        float ancien = loc.loyerAnnuelPrecedent;
        float nouveau = loc.loyerAnnuel;
        float indiceBase = Valeur(loc.indiceImmoAuDepart);
        float indiceNouv = Valeur(loc.indiceImmoActuel);

        bool revisionConnue = ancien > 0f && nouveau > 0f && indiceBase > 0f && indiceNouv > 0f;

        if (revisionConnue)
        {
            sb.Append($"<div class=\"expl-titre\"><u>Base Loyer Annuel à compter du {H(Date(dateEffet))}</u></div>");
            sb.Append("<table class=\"recap expl-table\">");
            sb.Append("<tr><th>Loyer base</th><th>Indice base</th>"
                    + "<th>Nouvel indice</th><th>Nouvelle Base Loyer</th></tr>");
            sb.Append($"<tr><td></td><td>{H(Periode(loc.indiceImmoAuDepart, loc.trimestreDeRevision))}</td>"
                    + $"<td>{H(Periode(loc.indiceImmoActuel, loc.trimestreDeRevision))}</td><td></td></tr>");
            sb.Append($"<tr><td class=\"r\">{Euro(ancien)}</td>"
                    + $"<td class=\"r\">{Nombre(indiceBase)}</td>"
                    + $"<td class=\"r\">{Nombre(indiceNouv)}</td>"
                    + $"<td class=\"r\"><b>{Euro(nouveau)}</b></td></tr>");
            sb.Append("</table>");
            sb.Append("<div class=\"expl-mois\"><u>Soit par Mois :</u>&nbsp;&nbsp;&nbsp;"
                    + $"<b>{Euro(nouveau / 12f)}</b></div>");
        }

        // ── Partie dépôt : toujours affichée, c'est l'objet de la facture ───────
        //
        // La FORMULATION vient de la facture (elle était écrite en dur, donc
        // impossible à retoucher) ; le CHOIX de la phrase reste ici, parce qu'il
        // dépend du signe de la dette — c'est de la logique de document, pas de la
        // rédaction. La mise en page (`<p class="expl-p">`) reste ici aussi.
        sb.Append("<div class=\"expl-titre\"><u>&#10148; Dépôt de garantie :</u></div>");
        sb.Append($"<p class=\"expl-p\">{Phrase(Ou(depot?.depotRappel, RappelDefaut), loc, bat, nbTermes, complement, baseCalcul)}</p>");

        // Le sens de la dette suit le signe du complément. Seuil au demi-centime :
        // sans lui, un arrondi flottant ferait réclamer « 0,00 € ».
        string dette = complement > 0.005f  ? Ou(depot?.depotDu, DuDefaut)
                     : complement < -0.005f ? Ou(depot?.depotRembourse, RembourseDefaut)
                                            : Ou(depot?.depotEquilibre, EquilibreDefaut);
        sb.Append($"<p class=\"expl-p\">{Phrase(dette, loc, bat, nbTermes, complement, baseCalcul)}</p>");

        return sb.ToString();
    }

    /// Résout une phrase réglable : d'abord les variables générales ({loc.*}, {bat.*}…)
    /// par le résolveur commun, puis les variables propres au dépôt.
    ///
    /// La phrase est insérée TELLE QUELLE dans le HTML — c'est ce qui permet d'y
    /// mettre du `<b>`, comme le fait le texte par défaut sur le montant. Ce sont
    /// donc les VALEURS substituées qui sont échappées, pas le modèle.
    static string Phrase(string modele, Locataire loc, Batiment bat,
                         int nbTermes, float complement, string baseCalcul)
    {
        if (string.IsNullOrWhiteSpace(modele)) return "";

        string texte = FactureVarResolver.Resolve(modele, loc, bat, default);

        return texte
            .Replace("{depot.termes}",  H($"{EnLettres(nbTermes)} terme{(nbTermes > 1 ? "s" : "")}"))
            .Replace("{depot.nb}",      H(EnLettres(nbTermes)))
            .Replace("{depot.base}",    H(baseCalcul))
            .Replace("{depot.montant}", Euro(Math.Abs(complement)));
    }

    // ── Mise en forme ───────────────────────────────────────────────────────────

    /// "2023-T3" → "3ème trimestre 2023". Renvoie la chaîne telle quelle si illisible :
    /// mieux vaut une période brute qu'une case vide sur un document envoyé au client.
    public static string TrimestreEnClair(string periode)
    {
        if (!InseeIndiceService.TryDecompose(periode, out int annee, out int trim))
            return periode ?? "";
        string rang = trim == 1 ? "1er" : $"{trim}ème";
        return $"{rang} trimestre {annee}";
    }

    /// 1..12 en toutes lettres, comme sur les courriers ; au-delà, le chiffre.
    public static string EnLettres(int n)
    {
        switch (n)
        {
            case 1:  return "un";
            case 2:  return "deux";
            case 3:  return "trois";
            case 4:  return "quatre";
            case 5:  return "cinq";
            case 6:  return "six";
            case 7:  return "sept";
            case 8:  return "huit";
            case 9:  return "neuf";
            case 10: return "dix";
            case 11: return "onze";
            case 12: return "douze";
            default: return n.ToString(CultureInfo.InvariantCulture);
        }
    }

    static string Periode(string indiceStocke, string fallback)
        => TrimestreEnClair(LoyerHistoryService.IndicePeriode(indiceStocke, fallback));

    static float Valeur(string indiceStocke) => LoyerHistoryService.IndiceValeur(indiceStocke);

    static string Euro(float v) => v.ToString("N2", FacturePdfService.FrCulture) + " €";
    static string Nombre(float v) => v.ToString("N2", FacturePdfService.FrCulture);
    /// « 1er juillet 2025 », pas « 1 juillet 2025 » : c'est un document envoyé au client.
    static string Date(DateTime d)
        => (d.Day == 1 ? "1er" : d.Day.ToString(CultureInfo.InvariantCulture))
           + d.ToString(" MMMM yyyy", FacturePdfService.FrCulture);

    static string H(string s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
