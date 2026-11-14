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
        new("{echeance}",        "Date d'échéance",         "Facture"),
        // Revue 17 (D6-25, coutures) : ces trois jetons valent les totaux du DOCUMENT (loyer + provisions, ou le solde), pas le
        // loyer seul : les libellés le disent (avant : « Montant loyer HT ») ; les jetons restent, les textes déjà écrits aussi.
        new("{loyer.ht}",        "Total HT de la facture",  "Facture"),
        new("{loyer.tva}",       "TVA de la facture",       "Facture"),
        new("{loyer.ttc}",       "Total TTC de la facture", "Facture"),
        // Revue 17 (D6-08) : remplacé par sa vraie valeur dans le seul rappel d'impayé (FactureRappelService) ; ailleurs « 0,00 € ».
        new("{reste}",           "Reste à payer (rappel)",  "Rappel d'impayé"),
        new("{retard}",          "Phrase de retard (Réglages)", "Facture"),

        // ── Société émettrice ──────────────────────────────────────
        new("{societe.nom}",     "Nom de la société",       "Société"),
        new("{societe.siret}",   "SIRET société",           "Société"),
        new("{societe.tva}",     "N° TVA société",          "Société"),

        // ── Ligne du montant mensuel (facture de loyer) ────────────
        // Résolu par FacturePdfService, donc uniquement dans le texte de cette
        // ligne-là. Ailleurs il resterait tel quel : le menu ne le propose que là (GroupesLimites).
        new("{montant}",         "Montant mensuel calculé",  "Ligne montant mensuel"),

        // ── Explication du dépôt ───────────────────────────────────
        // Résolues par ExplicationDepot, donc UNIQUEMENT dans les phrases
        // d'explication du dépôt (formulaire de la facture de dépôt). Ailleurs elles
        // resteraient telles quelles : le menu ne les propose que là (GroupesLimites).
        new("{depot.termes}",    "Nb de termes (« deux termes »)", "Explication dépôt"),
        new("{depot.nb}",        "Nb en lettres (« deux »)",       "Explication dépôt"),
        new("{depot.base}",      "Base de calcul (H.T./T.T.C.)",   "Explication dépôt"),
        new("{depot.montant}",   "Montant du dépôt (positif)",     "Explication dépôt"),
    };

    /// Revue 17 (D6-08) : groupes remplacés dans un seul texte. Le menu « / » ne les propose que dans le champ qui les nomme
    /// (SlashAutocomplete.Attach) ; avant, il les proposait partout, et ils partaient tels quels ou à 0 hors de leur texte.
    public static readonly string[] GroupesLimites = { "Ligne montant mensuel", "Explication dépôt", "Rappel d'impayé" };
}
