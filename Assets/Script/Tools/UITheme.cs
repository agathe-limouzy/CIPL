using UnityEngine;

/// Palette centrale de l'application.
/// Modifier ici = toute l'UI suit (les composants lisent ces valeurs au chargement).
public static class UITheme
{
    public static readonly Color Fond            = Hex("#F1EFE8"); // fond général
    public static readonly Color Carte           = Hex("#FCFBF8"); // cartes / sections
    public static readonly Color CarteEntete     = Hex("#F1EFE8"); // en-tête de section
    public static readonly Color Primaire        = Hex("#0F6E56"); // vert profond — actions
    public static readonly Color PrimaireClair   = Hex("#E1F5EE"); // fonds/textes sur vert
    public static readonly Color Alerte          = Hex("#D85A30"); // terracotta — alertes
    public static readonly Color AlerteClair     = Hex("#FAECE7");
    public static readonly Color AlerteTexte     = Hex("#712B13");
    public static readonly Color Attention       = Hex("#EF9F27"); // ambre — bientôt dû
    public static readonly Color AttentionClair  = Hex("#FAEEDA");
    public static readonly Color AttentionTexte  = Hex("#633806");
    public static readonly Color TextePrincipal  = Hex("#2C2C2A");
    public static readonly Color TexteSecondaire = Hex("#5F5E5A");
    public static readonly Color Bordure         = Hex("#D3D1C7");
    // Texte d'exemple d'un champ vide (placeholder), en italique : il n'est jamais
    // enregistré, il doit donc se distinguer d'une valeur saisie au premier coup d'œil.
    public static readonly Color TexteExemple    = Hex("#A8A69E");

    // ── Tailles de texte ────────────────────────────────────────────────────────
    //
    // Six tailles, et seulement six. L'inventaire du 23/09 en a trouvé vingt,
    // dispersées entre 11 et 30 pt, mais elles se regroupaient déjà en six rôles :
    // les écarts de 1 ou 2 points relevaient de la dérive, pas d'une intention.
    //
    // La taille à l'écran se règle ailleurs, globalement, par UIZoom : ces
    // valeurs sont la base, pour un affichage à 100 %.
    //
    // Des constantes et non des champs : elles servent de valeur par défaut aux
    // paramètres de UIFactory, ce que C# n'accepte que pour des constantes.

    // Le classement d'un texte dans un rôle se décide sur ce qu'il PORTE, pas sur
    // la place qu'il occupe : une donnée qu'on lit reste une donnée, même logée
    // dans une ligne de tableau ou sous une vignette. La légende est réservée à
    // ce qu'on ne lit pas vraiment — une pastille d'état, une mention accessoire.

    public const float TailleLegende    = 12f;  // pastilles d'état, mentions accessoires
    public const float TailleSecondaire = 15f;  // données de tableau et de liste, sous-titres, menus
    public const float TailleLibelle    = 18f;  // titres de champ, noms sur les cartes, boutons
    public const float TailleValeur     = 20f;  // contenu des champs
    public const float TailleSection    = 24f;  // titres de section
    public const float TaillePage       = 26f;  // titres de page

    public static readonly float[] Tailles =
        { TailleLegende, TailleSecondaire, TailleLibelle, TailleValeur, TailleSection, TaillePage };

    /// Le rôle d'un texte, c'est-à-dire ce qu'il porte. C'est lui que le code
    /// désigne — jamais une taille : UIFactory.Text(..., UITheme.Role.Donnee, ...).
    ///
    /// Plusieurs rôles partagent une taille, mais chacun a son nom. Séparer deux
    /// rôles ou agrandir l'un d'eux ne demande qu'une ligne ici, et tous les textes
    /// de ce rôle suivent — dans tous les écrans à la fois.
    public static class Role
    {
        public const float Pastille   = TailleLegende;     // « Impayé », « Obligatoire »
        public const float Mention    = TailleLegende;     // « Filtrer : »

        public const float Donnee     = TailleSecondaire;  // cellule de tableau, ligne de liste, « Acquis 15/03/2018 · Cadastre … »
        public const float EnTete     = TailleSecondaire;  // « Échéance », « Montant TTC », libellé de tuile « Loyers / an »
        public const float SousTitre  = TailleSecondaire;  // « Paris · 4 lots »
        public const float Aide       = TailleSecondaire;  // paragraphe explicatif
        public const float Action     = TailleSecondaire;  // petit bouton dans une ligne ou une barre : « Modifier », filtres

        public const float Libelle    = TailleLibelle;     // « Nom du locataire : »
        public const float Bouton     = TailleLibelle;     // tous les boutons
        public const float Nom        = TailleLibelle;     // bâtiment ou locataire dans une carte, une liste

        public const float Valeur     = TailleValeur;      // contenu d'un champ

        public const float ChiffreCle = TailleSection;     // valeur d'une tuile d'indicateur
        public const float Section    = TailleSection;     // « Créances », « Informations générales »

        public const float Page       = TaillePage;        // « Fiche bâtiment », « Réglage »
    }

    /// La taille de l'échelle la plus proche. À égale distance, la plus petite :
    /// rattacher un texte à l'échelle ne doit jamais le faire grossir par surprise.
    public static float TailleProche(float taille)
    {
        float meilleure = Tailles[0];
        foreach (float t in Tailles)
            if (Mathf.Abs(t - taille) < Mathf.Abs(meilleure - taille)) meilleure = t;
        return meilleure;
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
