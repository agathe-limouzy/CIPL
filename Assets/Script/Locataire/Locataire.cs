using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class Locataire : Data
{


    
    public string siretNumber;
    public string codeCompatable;
    public string emailLocataire;
    public string telephoneLocataire;
    public string adresseLocataire;
    public int lotBatiment;
    public float tailleLot;
    // Un nouveau locataire part en 3/6/9, le cas courant. Avant, il recevait la
    // première valeur de l'enum, « Bail à construction », sans que personne l'ait choisi.
    public BailType typeDeBail = BailType.BailCommercial369;
    // Durée du bail lui-même, en années (0 : non saisie, voir DureeBail()). La date de
    // fin en découle ; elle reste modifiable (bail repris, avenant).
    public int dureeBailAns;
    // Période ferme (bail commercial) : années, comprises dans la durée du bail,
    // pendant lesquelles le preneur ne peut pas donner congé. 0 = aucune.
    public int anneesFermes;
    public IndiceImmo indiceTypeImmo;
    public string indiceImmoAuDepart;
    public string indiceImmoActuel;
    public string dateDebutBailISO;
    public string dateFinBailISO;
    public string trimestreDeRevision;
    public float depotDeGarantie;
    public bool provisionPourCharges;
    public float provisionPourChargeValue;
    public float loyerAnnuel;
    public float loyerDepart;
    public float loyerAnnuelPrecedent;
    public float tauxDeRentabilité;
    public string moisDeRevisionISO;
    public string dernierRevision;
    public string commentaire;
    // Bail et avenants : NOMS de fichier, rangés dans DossiersDonnees.DossierBail (le
    // dossier du locataire). Un ancien bail peut encore porter un chemin complet.
    public string cheminBail;
    public List<string> avenants = new List<string>();
    public Periodicite periodiciteLoyer;

    // ── Facturation ────────────────────────────────────────────────────────────
    // RIB du locataire (ses coordonnées bancaires — prélèvement / référence).
    public string ribLocataireTitulaire;
    public string ribLocataireIban;
    public string ribLocataireBic;

    // Loyer : demande / périodicité de facturation / régularisation.
    public int jourDemandeLoyer;                    // jour du mois où le loyer est demandé (ex. 1 = le 1er)
    public List<int> moisFacturationLoyer = new List<int>(); // mois facturés (si trimestriel/bi-annuel), 1-12
    public string dateRegularisationChargeISO;      // date de régularisation des charges (événement annuel)
    // Une date et une provision par liste de charges SPÉCIFIQUE (Réglages). La liste
    // générale garde les deux champs historiques ci-dessus. Voir ListesCharges.
    public List<RegulListe> regulListes = new List<RegulListe>();
    // Listes de charges qui le concernent. Vide = toutes. Sinon ses ids ("" = la
    // générale) : une charge d'une autre liste ne lui est ni proposée, ni répartie.
    public List<string> listesConcernees = new List<string>();
    // provisionPourCharges (bool) + provisionPourChargeValue (float) existent déjà plus haut.

    // Reprise de facturation (bail repris / passif) : échéance de la DERNIÈRE période
    // déjà facturée hors app. Toute période d'échéance ≤ cette date = « Clôturé »
    // (non suivie, pas d'alerte). Vide = nouveau bail (tout est suivi).
    public string repriseFacturationISO;

    // Dépôt de garantie : date de révision (le montant = depotDeGarantie ci-dessus).
    public string dateRevisionDepotISO;
    // Base du calcul du dépôt : loyer TTC (locataire soumis à TVA) ou HT (sinon).
    public bool depotSurTTC = true;

    // Date de création de la fiche. Sert à retrouver le locataire créé juste avant,
    // dont les réglages de facture sont dupliqués sur le nouveau (voir
    // HeritageFacture). L'ordre dans les listes ne suffisait pas : les locataires
    // sont répartis entre plusieurs bâtiments, donc leur chronologie réelle n'est pas
    // reconstituable par simple parcours. Vide sur les fiches antérieures : elles
    // comptent alors comme les plus anciennes.
    public string creationISO;

    // Facturation : état mémorisé du menu « Information Facture » par type.
    public FactureInfo factureLoyer;
    public FactureInfo factureRegul;   // régularisation des charges
    public FactureInfo factureRefac;   // refacturation d'une charge
    public FactureInfo factureDepot;   // révision du dépôt de garantie (facture du complément)

    /// Le réglage de facture correspondant à un type du suivi (« Loyer », « Regul »,
    /// « Refac », « Depot »). Null si le type est inconnu ou le réglage jamais saisi.
    public FactureInfo FactureInfoDe(string type)
    {
        switch (type)
        {
            case "Loyer": return factureLoyer;
            case "Regul": return factureRegul;
            case "Refac": return factureRefac;
            case "Depot": return factureDepot;
            default: return null;
        }
    }

    // Séquence de numérotation des factures (unique par locataire, tous types).
    public int factureSeq = 1;
    // Suivi des états de facturation (lignes générées / envoyées / payées / forcées).
    public System.Collections.Generic.List<FactureEtat> facturesEtat = new System.Collections.Generic.List<FactureEtat>();

    // ── Historique d'indexation (pour la rentabilité année par année) ──────────
    // Date du tout premier bail, conservée à travers les renouvellements :
    // aucun loyer n'est perçu avant cette date (local vacant).
    public string dateDebutPremierBailISO;

    // Une entrée par « référence » d'indexation. Une nouvelle référence est
    // enregistrée quand le trimestre/indice/loyer de départ change (nouveau bail
    // ou renouvellement). Sert à reconstituer le vrai loyer de chaque année :
    // loyer(année) = loyerDepart × indice(année, trimestreRef) / indiceBaseValeur.
    public List<RevisionReference> historiqueReferences = new List<RevisionReference>();

    // Objectifs
    public ObjectiveList objectifs = new ObjectiveList();

    // Propriété DateTime pratique (non sérialisée)
    [NonSerialized]
    private DateTime _dateDebutBail;
    public DateTime DateDebutBail
    {
        get => DateTime.TryParse(dateDebutBailISO, out var d) ? d : DateTime.Today;
        set => dateDebutBailISO = value.ToString("yyyy-MM-dd");
    }

    [NonSerialized]
    private DateTime _dateFinBail;
    public DateTime DateFinBail
    {
        get => DateTime.TryParse(dateFinBailISO, out var d) ? d : DateTime.Today;
        set => dateFinBailISO = value.ToString("yyyy-MM-dd");
    }

    // ── Renouvellement de bail ────────────────────────────────────────────────
    // L'alerte s'ouvre 9 mois avant la fin : 3 mois avant la limite du congé (6 mois
    // avant la fin), comme pour les échéances triennales. Elle s'ouvrait à 6 mois,
    // le jour même où il était trop tard pour que le preneur donne congé.

    /// Vrai si le bail se termine dans moins de 9 mois (ou est déjà expiré).
    /// `jours` = nombre de jours avant la fin (négatif si le bail est expiré).
    /// Ne se déclenche que pour un locataire nommé avec une date de fin valide,
    /// afin de ne pas alerter sur les fiches vides (DateFinBail vaut Today par défaut).
    public static bool RenouvellementProche(Locataire loc, DateTime aujourdhui, out int jours)
    {
        jours = 0;
        if (loc == null || string.IsNullOrEmpty(loc.Name)) return false;
        if (!DateTime.TryParse(loc.dateFinBailISO, out var fin)) return false;
        jours = (fin - aujourdhui).Days;
        return aujourdhui >= fin.AddMonths(-(PREAVIS_CONGE_MOIS + ALERTE_AVANT_LIMITE_MOIS));
    }

    public static bool RenouvellementProche(Locataire loc, out int jours)
        => RenouvellementProche(loc, DateTime.Today, out jours);

    /// Bail commercial : le preneur peut encore donner congé pour la FIN du bail
    /// (préavis de 6 mois). `limite` = dernier jour pour le faire. Faux si le délai
    /// est passé ou si le bail n'est pas commercial.
    public static bool CongeFinDeBailPossible(Locataire loc, DateTime aujourdhui, out DateTime fin, out DateTime limite)
    {
        fin = limite = default;
        if (loc == null || !PeriodeFermePossible(loc.typeDeBail)) return false;
        if (!DateTime.TryParse(loc.dateFinBailISO, out fin)) return false;
        limite = fin.AddMonths(-PREAVIS_CONGE_MOIS);
        return aujourdhui <= limite;
    }

    /// Bail commercial expiré, en tacite prolongation : le preneur peut partir à tout
    /// moment, avec 6 mois de préavis, pour le dernier jour d'un trimestre civil
    /// (art. L145-9 du Code de commerce). Renvoie la sortie la plus proche possible.
    public static DateTime SortieTaciteAuPlusTot(DateTime aujourdhui)
    {
        var d = aujourdhui.AddMonths(PREAVIS_CONGE_MOIS);
        int mois = ((d.Month - 1) / 3 + 1) * 3;
        return new DateTime(d.Year, mois, DateTime.DaysInMonth(d.Year, mois));
    }

    /// Le texte de l'alerte de fin de bail, commun à la pastille de la fiche et à
    /// « À traiter » : les dates qui comptent, pas seulement « À renouveler ».
    public static string TexteFinDeBail(Locataire loc, DateTime aujourdhui)
    {
        RenouvellementProche(loc, aujourdhui, out int jours);
        bool commercial = PeriodeFermePossible(loc.typeDeBail);
        if (jours < 0)
            return commercial
                ? $"Bail expiré — congé possible à tout moment, sortie au plus tôt le {SortieTaciteAuPlusTot(aujourdhui):dd/MM/yyyy}"
                : "Bail expiré — à renouveler";
        if (CongeFinDeBailPossible(loc, aujourdhui, out var fin, out var limite))
            return $"À renouveler — fin le {fin:dd/MM/yyyy}, congé jusqu'au {limite:dd/MM/yyyy}";
        return DateTime.TryParse(loc.dateFinBailISO, out fin) ? $"À renouveler — fin le {fin:dd/MM/yyyy}" : "À renouveler";
    }

    // ── Résiliation triennale (bail commercial) ───────────────────────────────
    // Le preneur peut donner congé à chaque fin de période triennale (3, 6, 9… ans
    // après le début), avec un préavis de 6 mois — mais pas pendant la période ferme.
    // La fin du bail elle-même relève du renouvellement (RenouvellementProche).
    public const int PREAVIS_CONGE_MOIS = 6;
    // L'alerte s'ouvre 3 mois avant la date limite du congé et se ferme à cette date :
    // passé ce délai, le preneur ne peut plus partir à cette échéance.
    public const int ALERTE_AVANT_LIMITE_MOIS = 3;

    /// Dates où le preneur peut quitter les lieux : fins de périodes triennales,
    /// hors période ferme, avant la fin du bail (01/06/2020, 9 ans → 31/05/2023, 31/05/2026).
    public static List<DateTime> EcheancesTriennales(DateTime debut, int dureeAns, int fermes)
    {
        var echeances = new List<DateTime>();
        for (int k = 3; k < dureeAns; k += 3)
            if (k >= fermes) echeances.Add(FinDeBail(debut, k));
        return echeances;
    }

    /// Vrai si le preneur peut encore donner congé pour une échéance proche :
    /// `echeance` = date de sortie possible, `limite` = dernier jour pour donner congé.
    public static bool ResiliationProche(Locataire loc, DateTime aujourdhui, out DateTime echeance, out DateTime limite)
    {
        echeance = limite = default;
        if (loc == null || string.IsNullOrEmpty(loc.Name) || !PeriodeFermePossible(loc.typeDeBail)) return false;
        if (!DateTime.TryParse(loc.dateDebutBailISO, out var debut)) return false;
        foreach (var e in EcheancesTriennales(debut, loc.DureeBail(), loc.AnneesFermes()))
        {
            var l = e.AddMonths(-PREAVIS_CONGE_MOIS);
            if (aujourdhui > l) continue;                                          // délai passé
            if (aujourdhui < l.AddMonths(-ALERTE_AVANT_LIMITE_MOIS)) return false;   // trop tôt
            echeance = e; limite = l;
            return true;
        }
        return false;
    }

    public static bool ResiliationProche(Locataire loc, out DateTime echeance, out DateTime limite)
        => ResiliationProche(loc, DateTime.Today, out echeance, out limite);

    // Bail commercial (facture avec TVA) vs non commercial (loyer civil / professionnel /
    // autre → quittance de loyer possible quand le loyer est payé). Cf. diagramme facturation.
    public static bool EstBailCommercial(BailType t)
    {
        switch (t)
        {
            case BailType.Bail9ans:
            case BailType.Bail10ans:
            case BailType.BailCommercial369:
            case BailType.BailCommercialFerme:
            case BailType.BailDerogatoire:
                return true;
            default:
                return false;
        }
    }

    // ── Types, durées, années fermes ──────────────────────────────────────────

    /// Les types proposés dans la liste, dans cet ordre. La durée et la période ferme
    /// ne sont pas des types : « commercial 9 ans », « 10 ans » et « X ans ferme »
    /// sont tous des baux commerciaux (voir Normaliser), la durée et la période ferme
    /// se règlent à côté. Leurs anciennes valeurs restent lisibles dans les fichiers.
    public static readonly BailType[] TypesProposes =
    {
        BailType.BailCommercial369, BailType.BailDerogatoire,
        BailType.BailProfessionnel, BailType.BailCivil, BailType.ConventionOccupationPrecaire,
        BailType.BailEmphyteotique, BailType.BailAContruction, BailType.BailRehabilitation,
    };

    /// Le type tel qu'on le propose aujourd'hui : 9 ans, 10 ans et « ferme » → commercial.
    public static BailType Normaliser(BailType t)
        => t == BailType.Bail9ans || t == BailType.Bail10ans || t == BailType.BailCommercialFerme
            ? BailType.BailCommercial369 : t;

    /// La période ferme (le preneur ne peut pas donner congé) n'existe que pour le bail commercial.
    public static bool PeriodeFermePossible(BailType t) => Normaliser(t) == BailType.BailCommercial369;

    public static string LibelleBail(BailType t)
    {
        switch (Normaliser(t))
        {
            case BailType.BailCommercial369:            return "Bail commercial";
            case BailType.BailDerogatoire:              return "Bail dérogatoire (3 ans max)";
            case BailType.BailProfessionnel:            return "Bail professionnel (6 ans)";
            case BailType.BailCivil:                    return "Bail civil (droit commun)";
            case BailType.ConventionOccupationPrecaire: return "Convention d'occupation précaire";
            case BailType.BailEmphyteotique:            return "Bail emphytéotique";
            case BailType.BailAContruction:             return "Bail à construction";
            case BailType.BailRehabilitation:           return "Bail à réhabilitation";
            default:                                    return t.ToString();
        }
    }

    /// Durée par défaut, en années ; 0 = libre (civil, convention précaire).
    public static int DureeParDefaut(BailType t)
    {
        switch (t)
        {
            case BailType.Bail10ans:                  return 10;
            case BailType.Bail9ans:
            case BailType.BailCommercial369:
            case BailType.BailCommercialFerme:        return 9;
            case BailType.BailDerogatoire:            return 3;
            case BailType.BailProfessionnel:          return 6;
            case BailType.BailEmphyteotique:
            case BailType.BailAContruction:           return 18;
            case BailType.BailRehabilitation:         return 12;
            default:                                  return 0;
        }
    }

    /// Fin d'un bail de `ans` années : la veille de l'anniversaire (01/06/2020 → 31/05/2029).
    public static DateTime FinDeBail(DateTime debut, int ans) => debut.AddYears(ans).AddDays(-1);

    /// Durée du bail : celle saisie ; sinon déduite des dates quand elles couvrent
    /// un nombre entier d'années ; sinon celle du type. Un bail repris en cours
    /// (5,3 ans entre ses dates) retombe ainsi sur sa vraie durée, pas sur 5.
    public int DureeBail()
    {
        if (dureeBailAns > 0) return dureeBailAns;
        if (DateTime.TryParse(dateDebutBailISO, out var d) && DateTime.TryParse(dateFinBailISO, out var f))
            for (int n = 1; n <= 99; n++)
                if (Math.Abs((FinDeBail(d, n) - f).TotalDays) <= 3) return n;
        return DureeParDefaut(typeDeBail);
    }

    /// Années fermes : celles saisies ; l'ancien type « 9 ans ferme » en vaut 9.
    public int AnneesFermes()
        => anneesFermes > 0 ? anneesFermes : (typeDeBail == BailType.BailCommercialFerme ? 9 : 0);

    /// Message d'erreur, ou null si le bail tient debout. La période ferme est
    /// facultative (0 = aucune) ; c'est une portion de la durée, jamais plus.
    public static string VerifierBail(int dureeAns, int fermes)
    {
        if (dureeAns < 0 || dureeAns > 99) return "La durée du bail doit être comprise entre 1 et 99 ans.";
        if (fermes < 0) return "La période ferme ne peut pas être négative.";
        if (fermes > 0 && dureeAns > 0 && fermes > dureeAns)
            return $"Un bail de {dureeAns} ans ne peut pas avoir {fermes} ans fermes : la période ferme est comprise dans la durée du bail.";
        return null;
    }

    [NonSerialized]
    private DateTime _moisDeRevisionISO;
    public DateTime MoisDeRevision
    {
        get => DateTime.TryParse(moisDeRevisionISO, out var d) ? d : DateTime.Today;
        set => moisDeRevisionISO = value.ToString("yyyy-MM-dd");
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}


public enum BailType
{
    // Ordre figé : la valeur = l'index stocké. Ajouter les nouveaux À LA SUITE.
    // Renommer est sans risque (les fichiers stockent le numéro), réordonner non.
    BailAContruction,
    Bail9ans,            // n'est plus proposé : un 3/6/9 (voir Locataire.Normaliser)
    Bail10ans,           // idem, durée 10 ans
    BailCommercial369,
    BailCommercialFerme, // n'est plus proposé : un commercial avec période ferme (autrefois « 9 ans ferme »)
    BailDerogatoire,
    BailProfessionnel,
    BailEmphyteotique,
    BailRehabilitation,
    ConventionOccupationPrecaire,
    BailCivil
}

public enum IndiceImmo
{
    ILC,
    IRL,
    ILAT
}

public enum Periodicite
{
    mensuel,
    trimestriel,
    BiAnnuel,
    Annuel
}

public enum RevisionMode
{
    NouveauBail,
    BailEnCours
}

/// Une « référence » d'indexation : le loyer de base et l'indice de départ
/// valables à partir d'une date d'effet. Reconstitue le loyer d'une année via
/// loyer = loyerDepart × indice(année, trimestre de trimestreReference) / indiceBaseValeur.
[Serializable]
public class RevisionReference
{
    public string dateEffetISO;        // date de prise d'effet de cette référence
    public IndiceImmo indiceType;      // ILC / IRL / ILAT
    public string trimestreReference;  // période de l'indice de départ, ex "2020-T2"
    public float indiceBaseValeur;     // valeur de l'indice de départ
    public float loyerDepart;          // loyer annuel de base de cette référence

    public RevisionReference() { }

    public RevisionReference(string dateEffetISO, IndiceImmo indiceType,
        string trimestreReference, float indiceBaseValeur, float loyerDepart)
    {
        this.dateEffetISO = dateEffetISO;
        this.indiceType = indiceType;
        this.trimestreReference = trimestreReference;
        this.indiceBaseValeur = indiceBaseValeur;
        this.loyerDepart = loyerDepart;
    }
}
