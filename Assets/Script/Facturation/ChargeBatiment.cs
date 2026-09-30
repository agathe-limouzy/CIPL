using System;
using System.Collections.Generic;

/// Une charge saisie au niveau du BÂTIMENT (onglet « Charges », à côté d'Achat /
/// Travaux). Sert de base à la refacturation et à la régularisation.
///
/// Trois états, et non deux — c'est la distinction qui manquait :
///   1. **à refacturer** : ni facturée ni payée, elle est proposée au choix ;
///   2. **en attente de paiement** : portée sur une facture émise, donc retirée du
///      choix pour ne pas être refacturée deux fois — mais PAS payée pour autant ;
///   3. **payée** : le virement est arrivé, et c'est l'utilisatrice qui le constate.
///
/// L'émission faisait auparavant passer la charge directement en « payé », ce qui
/// faisait dire au bâtiment qu'il avait encaissé une somme jamais reçue.
[Serializable]
public class ChargeBatiment
{
    public string id = Guid.NewGuid().ToString();
    public string nom;                 // ex. « Taxe foncière 2026 », « Entretien toiture »
    public float cout;                 // coût total de la charge (€)
    public string dateISO;             // date de la charge ("yyyy-MM-dd")
    public string pdfPath;             // PDF de la facture de charge (copié dans le dossier de sauvegarde)
    public string listeId;             // liste de charges (Réglages) ; vide = charges générales

    // Répartition entre locataires.
    public bool tousLocataires = true;                     // « Tous » ou sélection
    public List<string> locatairesConcernes = new List<string>(); // ids si pas « Tous »
    public string typeRatio = "Surface";                   // Surface | Égalité | Manuel
    public List<ChargeRatio> ratios = new List<ChargeRatio>(); // poids par locataire (part relative)

    // Réglée À LA MAIN (case « Payé » de la fiche charge) : plus proposée à personne.
    // L'encaissement d'une facture, lui, se note PAR LOCATAIRE dans `facturations`.
    public bool paye = false;

    // Ancien format : date de mise sur facture commune à tous les locataires. Une
    // charge partagée facturée à l'un disparaissait pour les autres, dont la part
    // était perdue. Lu une seule fois par `Reconstituer`, puis vidé.
    public string factureeISO;

    /// Une entrée par locataire à qui la charge a été facturée (refacturation ou
    /// régularisation). C'est ce qui la retire du choix de CE locataire seulement.
    public List<ChargeFacturation> facturations = new List<ChargeFacturation>();

    public ChargeFacturation FacturationDe(string locataireId)
        => facturations?.Find(f => f != null && f.locataireId == locataireId);

    public bool EstFactureePour(string locataireId) => FacturationDe(locataireId) != null;

    /// Encore à facturer à ce locataire : ni réglée à la main, ni déjà portée sur
    /// une de ses factures (payée ou non).
    public bool AFacturerPour(string locataireId) => !paye && !EstFactureePour(locataireId);

    public void MarquerFacturee(string locataireId, string dateISO)
    {
        if (EstFactureePour(locataireId)) return;   // première facture : sa date fait foi
        (facturations ??= new List<ChargeFacturation>())
            .Add(new ChargeFacturation { locataireId = locataireId, dateISO = dateISO });
    }

    public void MarquerPayee(string locataireId, bool payee)
    {
        var f = FacturationDe(locataireId);
        if (f != null) f.paye = payee;
    }

    /// Facturée à au moins un locataire.
    public bool EstFacturee => facturations != null && facturations.Count > 0;

    /// Réglée à la main, ou payée par tous ceux à qui elle a été facturée.
    public bool EstPayee => paye || (EstFacturee && facturations.TrueForAll(f => f.paye));

    /// Ce qu'on affiche à l'utilisatrice, en un seul endroit pour que les écrans ne
    /// puissent pas diverger.
    public string Etat => paye ? "Payé"
        : !EstFacturee ? "Impayé"
        : (EstPayee ? "Payé" : "En attente de paiement") + $" · {facturations.Count} locataire(s)";

    /// Ancien format → par locataire, d'après le suivi : une charge marquée facturée
    /// est attribuée aux locataires concernés qui ont une facture émise la couvrant
    /// (« refac-&lt;id&gt; », ou « regul-&lt;année&gt; »). Sans aucune ligne pour le dire,
    /// elle reste réglée pour tous — comme avant, pour ne rien refacturer au hasard.
    /// Renvoie vrai si le bâtiment a changé (à enregistrer).
    public static bool Reconstituer(Batiment bat)
    {
        bool change = false;
        if (bat?.charges == null) return false;
        foreach (var c in bat.charges)
        {
            if (c == null || string.IsNullOrEmpty(c.factureeISO)) continue;
            int annee = DateTime.TryParse(c.dateISO, out var d) ? d.Year : 0;

            foreach (var loc in bat.locataireDuBatiment ?? new List<Locataire>())
            {
                bool concerne = c.tousLocataires || (c.locatairesConcernes != null && c.locatairesConcernes.Contains(loc.id));
                if (!concerne || loc.facturesEtat == null) continue;
                var ligne = loc.facturesEtat.Find(x => x.key == "refac-" + c.id)
                         ?? loc.facturesEtat.Find(x => x.key == ListesCharges.Cle(ListesCharges.Effective(c.listeId), annee));
                if (ligne == null || !Emise(ligne.statut)) continue;
                c.MarquerFacturee(loc.id, c.factureeISO);
                c.MarquerPayee(loc.id, ligne.statut == "Paye");
            }

            // `paye` venait de l'encaissement de la facture : il vit maintenant par
            // locataire. Sans attribution possible, on garde l'ancien effet.
            c.paye = !c.EstFacturee;
            c.factureeISO = null;
            change = true;
        }
        return change;
    }

    static bool Emise(string statut)
        => statut == "Envoye" || statut == "AttenteEnvoi" || statut == "Impaye" || statut == "Paye";
}

/// Mise sur facture d'une charge pour UN locataire.
[Serializable]
public class ChargeFacturation
{
    public string locataireId;
    public string dateISO;   // "yyyy-MM-dd", date de la première facture émise
    public bool paye;        // le virement de ce locataire est arrivé
}

/// Poids de répartition d'une charge pour un locataire donné.
/// La part réelle = part / somme(parts) × coût.
[Serializable]
public class ChargeRatio
{
    public string locataireId;
    public float part;

    public ChargeRatio() { }
    public ChargeRatio(string locataireId, float part) { this.locataireId = locataireId; this.part = part; }
}
