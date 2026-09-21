using System;
using System.Collections.Generic;

/// Duplication des réglages de facture sur un locataire qui vient d'être créé.
///
/// La règle voulue est une CHAÎNE : le 1er locataire part des valeurs d'usine et se
/// règle ; le 2e est créé avec une copie des réglages du 1er, puis vit sa vie ; le 3e
/// copie ceux du 2e, et ainsi de suite. Chaque locataire POSSÈDE donc ses réglages —
/// les modifier n'affecte pas les précédents, et les précédents ne le modifient pas
/// rétroactivement.
///
/// C'est là toute la différence avec un réglage unique d'entreprise : la source n'est
/// pas « le dernier qui a enregistré », c'est « le dernier qui a été CRÉÉ ».
///
/// Les réglages seuls sont dupliqués — jamais une donnée de facture. Un RIB ou un
/// format de numéro repris, c'est une décision déjà prise ; une date, un numéro ou un
/// montant repris, c'est un document faux.
public static class HeritageFacture
{
    /// Réglages dupliqués. Tout ce qui n'est pas dans cette liste reste vide sur la
    /// nouvelle fiche, et sera donc recalculé depuis le locataire lui-même.
    static FactureInfo Reglages(FactureInfo src)
    {
        if (src == null) return null;

        return new FactureInfo
        {
            ribId          = src.ribId,
            enteteId       = src.enteteId,
            numeroFormat   = src.numeroFormat,
            tvaDebit       = src.tvaDebit,
            ajouterRetard  = src.ajouterRetard,
            ajouterMensuel = src.ajouterMensuel,
            envoiEmail     = src.envoiEmail,
            joindrePj      = src.joindrePj,

            // Formulations imprimées sur le document : de la rédaction, sans date ni
            // montant figé — les dupliquer ne peut pas produire de document faux, et
            // c'est tout l'intérêt (reformuler une fois, les suivants en héritent).
            texteTvaDebit  = src.texteTvaDebit,
            texteMensuel   = src.texteMensuel,

            depotRappel    = src.depotRappel,
            depotDu        = src.depotDu,
            depotRembourse = src.depotRembourse,
            depotEquilibre = src.depotEquilibre,

            // `saved` reste FAUX : les montants et les dates du nouveau locataire
            // doivent être recalculés depuis sa fiche, pas repris de l'autre.
            saved = false
        };
    }

    /// Le locataire créé le plus récemment parmi `existants`, `neuf` exclu.
    ///
    /// Une fiche sans date de création (antérieure à ce champ) compte comme la plus
    /// ancienne, mais reste éligible : à défaut de chronologie, la dernière rencontrée
    /// au parcours fait office de précédente — c'est mieux que repartir d'usine.
    public static Locataire Precedent(IEnumerable<Locataire> existants, Locataire neuf)
    {
        if (existants == null) return null;

        Locataire meilleur = null;
        DateTime meilleureDate = DateTime.MinValue;

        foreach (var l in existants)
        {
            if (l == null || ReferenceEquals(l, neuf)) continue;
            if (neuf != null && !string.IsNullOrEmpty(neuf.id) && l.id == neuf.id) continue;

            DateTime d = DateTime.TryParse(l.creationISO, out var parsed) ? parsed : DateTime.MinValue;

            // `>=` et non `>` : à dates égales — donc notamment entre fiches sans date —
            // c'est la dernière rencontrée qui gagne, soit l'ordre des listes.
            if (meilleur == null || d >= meilleureDate) { meilleur = l; meilleureDate = d; }
        }

        return meilleur;
    }

    /// Horodate `neuf` puis y duplique les réglages du locataire créé juste avant.
    /// Sans précédent (tout premier locataire de l'entreprise), la fiche reste vide et
    /// les panneaux appliquent leurs valeurs d'usine : c'est le « réglage initial ».
    public static void Appliquer(Locataire neuf, IEnumerable<Locataire> existants)
    {
        if (neuf == null) return;

        if (string.IsNullOrEmpty(neuf.creationISO))
            neuf.creationISO = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var src = Precedent(existants, neuf);
        if (src == null) return;

        neuf.factureLoyer = Reglages(src.factureLoyer);
        neuf.factureRegul = Reglages(src.factureRegul);
        neuf.factureRefac = Reglages(src.factureRefac);
        neuf.factureDepot = Reglages(src.factureDepot);
    }

    /// Tous les locataires de l'entreprise, tous bâtiments confondus — la chaîne ne
    /// s'arrête pas au bâtiment courant : le 1er locataire d'un bâtiment neuf doit
    /// reprendre les réglages du dernier locataire créé, **même dans un autre
    /// bâtiment**, sinon chaque nouveau bâtiment repartirait d'usine.
    ///
    /// `BatimentManager.Batiments` tient des copies que `SaveBatiment` remplace par
    /// l'objet vivant à chaque enregistrement. Ce n'est pas un problème ici : un
    /// réglage de facture n'existe qu'après un enregistrement (`FactureInfo` naît
    /// dans `SaveFromUI`), donc tout locataire réglé est à jour dans cette liste.
    public static List<Locataire> TousLesLocataires()
        => TousLesLocataires(BatimentManager.Instance != null ? BatimentManager.Instance.Batiments : null);

    /// Surcharge testable : la traversée des bâtiments est la partie qui fait
    /// fonctionner la chaîne d'un bâtiment à l'autre, elle mérite d'être vérifiée
    /// sans dépendre d'une scène ni de `BatimentManager.Instance`.
    public static List<Locataire> TousLesLocataires(IEnumerable<Batiment> batiments)
    {
        var tous = new List<Locataire>();
        if (batiments == null) return tous;

        foreach (var b in batiments)
        {
            if (b?.locataireDuBatiment == null) continue;
            foreach (var l in b.locataireDuBatiment)
                if (l != null) tous.Add(l);
        }
        return tous;
    }
}
