using System;
using System.Linq;

[Serializable]
public class AnnuaireResponse
{
    public AnnuaireEntreprise[] results;
    public int total_results;
}

[Serializable]
public class AnnuaireEntreprise
{
    public string nom_complet;
    public string siren;
    public string date_creation;
    public string libelle_nature_juridique;
    public string activite_principale;
    public string libelle_activite_principale;
    public string libelle_tranche_effectif;
    public string etat_administratif; // "A" = Actif, "C" =Cessé
    public AnnuaireSiege siege;
}

[Serializable]
public class AnnuaireSiege
{
    public string adresse;
    public string code_postal;
    public string commune;            // code INSEE de la commune (« 92048 »), pas son nom
    public string libelle_commune;    // « MEUDON »

    /// Le nom de la commune (retour du 25/10 : le lieu d'émission valait « 92048 »).
    public string Ville => !string.IsNullOrWhiteSpace(libelle_commune) ? libelle_commune.Trim()
                         : (commune ?? "").Trim().All(char.IsDigit) ? "" : commune.Trim();
}
