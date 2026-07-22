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

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
