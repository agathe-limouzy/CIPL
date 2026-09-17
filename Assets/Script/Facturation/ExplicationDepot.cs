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
    /// Construit le HTML. `nbTermes` = nombre de termes de loyer que représente le
    /// dépôt, `complement` = somme réclamée, `dateEffet` = prise d'effet du loyer révisé.
    public static string Html(Locataire loc, int nbTermes, float complement, DateTime dateEffet)
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
        sb.Append("<div class=\"expl-titre\"><u>&#10148; Dépôt de garantie :</u></div>");
        sb.Append("<p class=\"expl-p\">Je vous rappelle qu'il doit correspondre à "
                + $"{H(EnLettres(nbTermes))} terme{(nbTermes > 1 ? "s" : "")} de loyer {H(baseCalcul)}</p>");

        // Le sens de la dette suit le signe du complément. Seuil au demi-centime :
        // sans lui, un arrondi flottant ferait réclamer « 0,00 € ».
        if (complement > 0.005f)
            sb.Append($"<p class=\"expl-p\">Et ainsi, il ressort que vous nous devez : <b>{Euro(complement)}</b> "
                    + "somme que je vous demande de nous régler par retour de courrier.</p>");
        else if (complement < -0.005f)
            sb.Append($"<p class=\"expl-p\">Et ainsi, il ressort que nous vous devons : <b>{Euro(-complement)}</b> "
                    + "somme qui vous sera remboursée.</p>");
        else
            sb.Append("<p class=\"expl-p\">Le dépôt de garantie déjà versé correspond au montant requis : "
                    + "aucun ajustement n'est nécessaire.</p>");

        return sb.ToString();
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
