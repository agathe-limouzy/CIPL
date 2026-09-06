/// Catalogue des variables insérables dans les textes de facture (entêtes,
/// paragraphes…). Chaque variable a un token (inséré dans le texte), un libellé
/// lisible et un groupe. Sert au menu « slash » ET à la substitution réelle
/// lors de la génération de la facture (à venir).
public struct FactureVariable
{
    public string token;
    public string label;
    public string groupe;
    public FactureVariable(string token, string label, string groupe)
    { this.token = token; this.label = label; this.groupe = groupe; }
}

public static class FactureVariables
{
    public static readonly FactureVariable[] All =
    {
        // ── Locataire ──────────────────────────────────────────────
        new("{loc.nom}",         "Nom du locataire",        "Locataire"),
        new("{loc.adresse}",     "Adresse du locataire",    "Locataire"),
        new("{loc.siret}",       "SIRET locataire",         "Locataire"),
        new("{loc.code}",        "Code comptable",          "Locataire"),
        new("{loc.email}",       "Email locataire",         "Locataire"),
        new("{loc.tel}",         "Téléphone locataire",     "Locataire"),
        new("{loc.lot}",         "N° de lot",               "Locataire"),
        new("{loc.taille}",      "Surface du lot",          "Locataire"),
        new("{loc.bail}",        "Type de bail",            "Locataire"),
        new("{loc.debutBail}",   "Début du bail",           "Locataire"),
        new("{loc.finBail}",     "Fin du bail",             "Locataire"),
        new("{loc.premierBail}", "Date 1er bail",           "Locataire"),
        new("{loc.depot}",       "Dépôt de garantie",       "Locataire"),
        new("{loc.loyer}",       "Loyer annuel",            "Locataire"),
        new("{loc.periodicite}", "Périodicité du loyer",    "Locataire"),
        new("{loc.provision}",   "Provision pour charges",  "Locataire"),
        new("{loc.taux}",        "Taux de rentabilité",     "Locataire"),

        // ── Bâtiment ───────────────────────────────────────────────
        new("{bat.nom}",         "Nom du bâtiment",         "Bâtiment"),
        new("{bat.adresse}",     "Adresse du bâtiment",     "Bâtiment"),
        new("{bat.taille}",      "Surface du bâtiment",     "Bâtiment"),
        new("{bat.terrain}",     "Surface du terrain",      "Bâtiment"),
        new("{bat.cadastre}",    "Référence cadastrale",    "Bâtiment"),
        new("{bat.acquisition}", "Date d'acquisition",      "Bâtiment"),

        // ── Facture / contexte ─────────────────────────────────────
        new("{date}",            "Date du jour",            "Facture"),
        new("{periode}",         "Période facturée",        "Facture"),
        new("{numero}",          "N° de facture",           "Facture"),
        new("{loyer.ht}",        "Montant loyer HT",        "Facture"),
        new("{loyer.tva}",       "Montant TVA",             "Facture"),
        new("{loyer.ttc}",       "Montant TTC",             "Facture"),

        // ── Société émettrice ──────────────────────────────────────
        new("{societe.nom}",     "Nom de la société",       "Société"),
        new("{societe.siret}",   "SIRET société",           "Société"),
        new("{societe.tva}",     "N° TVA société",          "Société"),
    };
}
