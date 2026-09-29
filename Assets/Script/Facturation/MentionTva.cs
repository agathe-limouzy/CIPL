using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Mention TVA imprimée sous les totaux : aucune, « payée sur les débits » ou
/// « payée sur les encaissements ». Le taux (20 %) n'en dépend pas.
///
/// Deux niveaux de texte : la phrase de BASE de chaque mention vit dans Réglages →
/// Textes fixes (`ReglageData.mentionTva*`) ; une facture peut la remplacer, et seul
/// ce remplacement est gardé sur elle (`FactureInfo.texteTvaDebit`, vide = phrase de
/// base). Modifier la base dans les Réglages change donc toutes les factures qui ne
/// l'ont pas remplacée.
///
/// Choix stocké sur `FactureInfo` : `tvaDebit` garde son sens historique « mention
/// imprimée », `tvaEncaissements` dit laquelle. Une facture antérieure n'a pas ce
/// second champ → faux → débits (ou aucune si sa case était décochée).
public static class MentionTva
{
    // Indices du menu, dans l'ordre de `Libelles`.
    public const int Aucune = 0, Debits = 1, Encaissements = 2;

    public static readonly List<string> Libelles = new List<string>
        { "Aucune mention", "TVA payée sur les débits", "TVA payée sur les encaissements" };

    public static int Lire(FactureInfo f)
    {
        if (f == null) return Debits;   // réglage d'usine
        if (!f.tvaDebit) return Aucune;
        return f.tvaEncaissements ? Encaissements : Debits;
    }

    public static void Ecrire(FactureInfo f, int choix)
    {
        f.tvaDebit = choix != Aucune;
        f.tvaEncaissements = choix == Encaissements;
    }

    /// Texte d'usine : celui des Réglages neufs, et le filet s'il y a été vidé.
    public static string Defaut(int choix)
        => choix == Encaissements ? FacturePdfService.TvaEncaissementsDefaut : FacturePdfService.TvaDebitDefaut;

    /// Phrase de base (Réglages → Textes fixes) de la mention choisie.
    public static string Base(ReglageData r, int choix)
        => FacturePdfService.Texte(choix == Encaissements ? r?.mentionTvaEncaissements : r?.mentionTvaDebits,
                                   Defaut(choix));

    /// Vrai si `texte` remplace la phrase de base sur cette facture.
    ///
    /// Le texte d'usine n'en est jamais un : l'ancien panneau enregistrait la phrase
    /// pré-remplie telle quelle, sans que personne l'ait choisie. La compter comme un
    /// remplacement figerait toutes ces factures sur l'ancienne formulation, sourdes
    /// aux Réglages.
    /// ponytail: une facture ne peut donc pas imposer le texte d'usine quand la base
    /// a changé ; ajouter un vrai drapeau « remplacée » sur FactureInfo si le besoin vient.
    public static bool Surcharge(ReglageData r, int choix, string texte)
        => !string.IsNullOrWhiteSpace(texte) && texte != Base(r, choix) && texte != Defaut(choix);

    /// Phrase effectivement imprimée pour cette facture (avant résolution des variables).
    public static string PhraseFacture(ReglageData r, FactureInfo f)
    {
        int choix = Lire(f);
        return Surcharge(r, choix, f?.texteTvaDebit) ? f.texteTvaDebit : Base(r, choix);
    }

    // ── Panneaux ────────────────────────────────────────────────────────────────

    static ReglageData R => ReglageService.Current;

    /// Libellé + menu + phrase modifiable, dans `parent`. Changer de mention remet la
    /// phrase de base correspondante : garder « …sur les débits » sous le choix
    /// « encaissements » imprimerait le contraire de ce qui vient d'être choisi.
    public static UIDropdown Creer(Transform parent, out TMP_InputField phrase)
    {
        UIFactory.Text(parent, "Mention TVA", UITheme.Role.Donnee, UITheme.TexteSecondaire);
        TMP_InputField p = null;
        var menu = UIDropdown.Create(parent, Libelles, null, Debits, choix =>
        {
            p.text = Base(R, choix);
            p.transform.parent.gameObject.SetActive(choix != Aucune);
        });

        // Phrase et aide ensemble, masquées ensemble quand rien ne s'imprime.
        var bloc = UIFactory.VBox(parent, 4, name: "PhraseTva");
        p = UIFactory.Input(bloc.transform, FacturePdfService.TvaDebitDefaut, 46, true);
        SlashAutocomplete.Attach(p);
        UIFactory.Text(bloc.transform,
            "Phrase de base modifiable dans Réglages → Textes fixes. La changer ici ne vaut que pour cette facture.",
            UITheme.Role.Aide, UITheme.TexteSecondaire);
        phrase = p;
        return menu;
    }

    /// Pré-remplie avec la phrase réellement imprimée — la base ou le remplacement —
    /// plutôt qu'un champ vide dont il faudrait deviner l'effet.
    public static void Charger(UIDropdown menu, TMP_InputField phrase, FactureInfo f)
    {
        int choix = Lire(f);
        menu.SetIndex(choix, false);
        phrase.text = PhraseFacture(R, f);
        phrase.transform.parent.gameObject.SetActive(choix != Aucune);
    }

    /// Seul un vrai remplacement est gardé : une phrase laissée à la base reste vide
    /// sur la facture, et suivra donc les Réglages.
    public static void Enregistrer(UIDropdown menu, TMP_InputField phrase, FactureInfo f)
    {
        Ecrire(f, menu.Index);
        f.texteTvaDebit = Surcharge(R, menu.Index, phrase.text) ? phrase.text : "";
    }

    public static bool Imprimee(UIDropdown menu) => menu.Index != Aucune;

    /// Phrase à imprimer, avant résolution des variables. Un champ vidé retombe sur la
    /// base de LA mention choisie — le filet de `FacturePdfService`, lui, ne connaît
    /// que le texte d'usine des débits.
    public static string Phrase(UIDropdown menu, TMP_InputField phrase)
        => FacturePdfService.Texte(phrase.text, Base(R, menu.Index));
}
