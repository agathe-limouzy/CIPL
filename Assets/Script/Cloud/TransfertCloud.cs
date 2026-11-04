using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// Transfert d'un bâtiment, ou d'un seul locataire, vers une autre entreprise du groupe, sur le cloud
/// (CONCEPTION §16, décisions du 01/11). Un administrateur des deux entreprises. Ordre de sécurité : les documents
/// sont copiés d'abord (des copies en plus ne gênent pas : contenu adressé par empreinte), les fiches changent
/// d'entreprise en une transaction (transferer_fiches), l'origine est marquée ensuite. Rien n'est effacé sans copie.
public static class TransfertCloud
{
    [Serializable] public class Resultat { public bool ok; public string refus, nom, cible, batiment; public int locataires; }
    [Serializable] class LigneDoc { public string chemin, empreinte; public long taille; }
    [Serializable] class LignesDoc { public LigneDoc[] items; }

    public class Apercu
    {
        public bool batimentExistant;
        public int locatairesAjoutes, locatairesRemplaces, documents;
        public string erreur;
    }

    /// Ce qui empêche le transfert tant que Super PDP a du travail en attente (décision du 01/11) : une facture à
    /// transmettre, payée mais pas encore déclarée « Encaissée », ou dont l'encaissement reste à annuler. Le message,
    /// ou null. Une facture envoyée ou impayée ne bloque pas (« Impayé » ne se déclare pas à la plateforme) ; une facture
    /// transmise par une autre entreprise suit la même règle (déclarée en son nom, BG7), sauf marque sans identifiant (lecture seule).
    public static string Bloque(IEnumerable<Locataire> locs)
    {
        foreach (var l in locs ?? Enumerable.Empty<Locataire>())
            foreach (var f in l?.facturesEtat ?? new List<FactureEtat>())
            {
                if (!FactureElectronique.EnAttente(f)) continue;
                return string.IsNullOrEmpty(f.plateformeId)
                    ? $"« {f.libelle} » de {l.Name} attend sa transmission à Super PDP."
                    : $"« {f.libelle} » de {l.Name} est payée mais pas encore déclarée « Encaissée » chez Super PDP (ou son encaissement reste à annuler).";
            }
        return null;
    }

    /// Les documents vivants du bâtiment (ou du seul locataire), tels que le serveur les connaît.
    public static async Task<List<(string chemin, string empreinte)>> Documents(Batiment bat, Locataire seul)
    {
        var res = new List<(string chemin, string empreinte)>();
        var e = SessionCloud.Entreprise;
        if (e == null || bat == null) return res;
        string prefixe = "Batiment/" + DossiersDonnees.NomDossier(bat.Name) + "/"
                         + (seul != null ? DossiersDonnees.NomDossier(seul.Name) + "/" : "");
        var r = await CloudClient.Table("GET", SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "documents",
                                        $"entreprise_id=eq.{e.id}&supprime_le=is.null&select=chemin,empreinte,taille");
        if (!r.Ok) return res;
        var lignes = JsonUtility.FromJson<LignesDoc>("{\"items\":" + r.Corps + "}")?.items ?? new LigneDoc[0];
        foreach (var l in lignes)
            if (l?.chemin != null && l.chemin.StartsWith(prefixe, StringComparison.Ordinal)) res.Add((l.chemin, l.empreinte));
        return res;
    }

    /// Ce que ferait le transfert, sans rien écrire : homonyme, locataires ajoutés ou remplacés, documents.
    public static async Task<Apercu> Preparer(Batiment bat, Locataire seul, EntrepriseCloud dest)
    {
        var a = new Apercu();
        var (bats, erreur) = await EcritureCloud.Consulter(dest.id);   // sans toucher au cache de l'origine
        if (bats == null) { a.erreur = $"lecture de {dest.nom} impossible : {erreur}"; return a; }
        var cible = bats.FirstOrDefault(b => b != null && DossiersDonnees.MemeDossier(b.Name, bat.Name));
        a.batimentExistant = cible != null;
        foreach (var l in seul != null ? new List<Locataire> { seul } : (bat.locataireDuBatiment ?? new List<Locataire>()))
        {
            if (l == null) continue;
            if (cible != null && (cible.locataireDuBatiment ?? new List<Locataire>()).Exists(d => d != null && DossiersDonnees.MemeDossier(d.Name, l.Name)))
                a.locatairesRemplaces++;
            else a.locatairesAjoutes++;
        }
        a.documents = (await Documents(bat, seul)).Count;
        return a;
    }

    /// Le transfert lui-même. Rend null, ou le motif de l'échec (rien n'a alors été retiré de l'origine).
    public static async Task<string> Executer(Batiment bat, Locataire seul, EntrepriseCloud dest, Action<string> progression)
    {
        var origine = SessionCloud.Entreprise;
        if (!SessionCloud.Active || origine == null || bat == null || dest == null) return "connexion au cloud requise.";
        if (HorsLigne.Actif) return HorsLigne.Refus;
        // 1. L'état affiché est celui qui part : la file des enregistrements doit être vide, la fiche à jour.
        try { await EcritureCloud.Attendre(); } catch (Exception) { /* déjà signalé */ }
        if (EcritureCloud.Perime(bat)) return "la fiche a été relue entre-temps : rouvrez-la et recommencez.";

        // 2. Une copie, réécrite avec les listes de charges de la destination ; les factures transmises par ici sont
        //    marquées au nom de l'origine (celles qui reviennent chez qui les a transmises redeviennent ordinaires).
        var copie = JsonUtility.FromJson<Batiment>(JsonUtility.ToJson(bat));
        copie.locataireDuBatiment = (copie.locataireDuBatiment ?? new List<Locataire>()).Where(l => l != null).ToList();
        if (seul != null) copie.locataireDuBatiment = copie.locataireDuBatiment.Where(l => l.id == seul.id).ToList();
        var (regDest, regVersion, errReg) = await ReglagesCloud.LireDe(dest.id);
        if (errReg != null) return errReg;
        var regAEnvoyer = TransfertEntreprise.ReconcilierListes(copie, ReglageService.Current, regDest ?? new ReglageData());
        foreach (var l in copie.locataireDuBatiment)
            foreach (var f in l.facturesEtat ?? new List<FactureEtat>()) MarquerOrigine(f, origine, dest);

        // 3. Les documents d'abord.
        var docs = await Documents(bat, seul);
        int i = 0;
        foreach (var d in docs)
        {
            progression?.Invoke($"Transfert : document {++i} / {docs.Count}…");
            string err = await DocumentsCloud.CopierVers(dest.id, d.chemin, d.empreinte);
            if (err != null) return $"document « {d.chemin} » : {err}.";
        }

        // 4. Les fiches, en une transaction.
        progression?.Invoke("Transfert : fiches…");
        var locs = copie.locataireDuBatiment;
        copie.locataireDuBatiment = new List<Locataire>();   // la table garde le bâtiment sans ses locataires
        var versions = new List<string>();
        if (seul == null) versions.Add("\"b:" + bat.id + "\":" + EcritureCloud.Version(true, bat.id));
        versions.AddRange(locs.Select(l => "\"l:" + l.id + "\":" + EcritureCloud.Version(false, l.id)));
        string locsJson = "[" + string.Join(",", locs.Select((l, k) => "{\"id\":" + Q(l.id) + ",\"ordre\":" + k + ",\"data\":" + JsonUtility.ToJson(l) + "}")) + "]";
        string corps = "{\"p_de\":" + Q(origine.id) + ",\"p_vers\":" + Q(dest.id) + ",\"p_batiment\":" + JsonUtility.ToJson(copie)
            + ",\"p_locataires\":" + locsJson + ",\"p_seul\":" + (seul != null ? "true" : "false")
            + ",\"p_versions\":{" + string.Join(",", versions) + "},\"p_session\":" + Q(EcritureCloud.SessionId)
            + (regAEnvoyer != null ? ",\"p_reglages\":" + JsonUtility.ToJson(regAEnvoyer) + ",\"p_reglages_version\":" + regVersion : "")
            + ",\"p_modele\":" + DonneesCloud.Modele + "}";   // Revue 17 (D2-01)
        var r = await CloudClient.FonctionJson(SessionCloud.Url, SessionCloud.Cle, await SessionCloud.Jeton(), "transferer_fiches", corps);
        if (!r.Ok) return "serveur : " + r.Erreur + ".";
        Resultat res = null;
        try { res = JsonUtility.FromJson<Resultat>(r.Corps); } catch (Exception) { /* réponse illisible */ }
        if (res == null || !res.ok) return Motif(res, dest);

        // 5. L'origine ne les a plus : ses documents sont marqués supprimés (chaque poste range sa copie en corbeille).
        progression?.Invoke("Transfert : origine…");
        foreach (var d in docs) await DocumentsCloud.MarquerSupprime(origine.id, d.chemin);
        return null;
    }

    /// Une facture transmise par ici part au nom de l'origine (« Encaissée » se déclarera avec son jeton, BG7) ; celle qui
    /// revient chez l'entreprise qui l'a transmise redevient ordinaire ; une marque sans identifiant (transfert d'avant)
    /// est gardée telle quelle (lecture seule) jusqu'à ce retour.
    public static void MarquerOrigine(FactureEtat f, EntrepriseCloud origine, EntrepriseCloud dest)
    {
        if (f == null || string.IsNullOrEmpty(f.plateformeId) || origine == null || dest == null) return;
        bool retour = !string.IsNullOrEmpty(f.plateformeOrigineId) ? f.plateformeOrigineId == dest.id
                    : !string.IsNullOrEmpty(f.plateformeOrigine) && f.plateformeOrigine == dest.nom;
        if (retour) { f.plateformeOrigine = ""; f.plateformeOrigineId = ""; }
        else if (string.IsNullOrEmpty(f.plateformeOrigine)) { f.plateformeOrigine = origine.nom; f.plateformeOrigineId = origine.id; }
    }

    static string Motif(Resultat r, EntrepriseCloud dest)
    {
        switch (r?.refus)
        {
            case "admin": return "réservé à un administrateur des deux entreprises.";
            case "groupe": return $"{dest.nom} n'est pas dans le même groupe.";
            case "expire": return "l'abonnement du groupe ne permet plus d'écrire.";
            case "verrou": return $"« {r.cible} » : en cours de modification par {RegleVerrou.Prenom(r.nom)}. Réessayez quand la fiche est libre.";
            case "version": return $"« {r.cible} » a été modifié entre-temps : rouvrez la fiche et recommencez.";
            case "application": return "une version plus récente de CIPL a enregistré ces fiches : mettez CIPL à jour avant de les transférer.";   // Revue 17 (D2-01)
            case "meme": return "c'est la même entreprise.";
            default: return "réponse inattendue du serveur" + (string.IsNullOrEmpty(r?.refus) ? "" : $" ({r.refus})") + ".";
        }
    }

    static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
