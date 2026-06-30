using System;
using System.Collections.Generic;

[Serializable]
public class PLUZoneInfo
{
    // ── Champs API IGN ────────────────────────────────────────────────────────
    public string typezone;   // U, AU, A, N
    public string libelle;    // UA, UB, UX, 1AU...
    public string libelong;   // Description longue
    public string urlfic;     // URL du PDF règlement
    public string nomfic;     // Nom du fichier PDF
    public string nomcom;     // Nom de la commune
    public string insee;      // Code INSEE
    public string datappro;   // Date d'approbation

    // ── Helpers zone ──────────────────────────────────────────────────────────

    public string TypeLabel => typezone switch
    {
        "U" => "Zone Urbaine — constructible",
        "AU" => "Zone À Urbaniser — sous conditions",
        "A" => "Zone Agricole — très limitée",
        "N" => "Zone Naturelle — protégée",
        _ => typezone ?? "Inconnue"
    };

    public string TypeColor => typezone switch
    {
        "U" => "#4CAF50",
        "AU" => "#FF9800",
        "A" => "#8BC34A",
        "N" => "#03A9F4",
        _ => "#9E9E9E"
    };

    public bool HasReglement => !string.IsNullOrEmpty(urlfic);

    // ── Heuristique commerce ──────────────────────────────────────────────────

    public List<CommerceEntry> CommercesIndicatifs => BuildCommerceList();

    private List<CommerceEntry> BuildCommerceList()
    {
        string z = typezone ?? "";
        string l = (libelle ?? "").ToUpper();

        // Zones non constructibles → tout interdit
        if (z == "A" || z == "N")
        {
            return new List<CommerceEntry>
            {
                new CommerceEntry("Commerce de détail",          CommerceStatus.Interdit),
                new CommerceEntry("Restauration",                CommerceStatus.Interdit),
                new CommerceEntry("Commerce de gros",            CommerceStatus.Interdit),
                new CommerceEntry("Bureau / Services",           CommerceStatus.Interdit),
                new CommerceEntry("Hébergement hôtelier",        CommerceStatus.Interdit),
                new CommerceEntry("Artisanat",                   CommerceStatus.Interdit),
            };
        }

        // Zone À Urbaniser
        if (z == "AU")
        {
            return new List<CommerceEntry>
            {
                new CommerceEntry("Commerce de détail",          CommerceStatus.SousConditions),
                new CommerceEntry("Restauration",                CommerceStatus.SousConditions),
                new CommerceEntry("Commerce de gros",            CommerceStatus.Interdit),
                new CommerceEntry("Bureau / Services",           CommerceStatus.SousConditions),
                new CommerceEntry("Hébergement hôtelier",        CommerceStatus.Interdit),
                new CommerceEntry("Artisanat",                   CommerceStatus.SousConditions),
            };
        }

        // Zone Urbaine — affinage par sous-zone
        if (z == "U")
        {
            // UA / UAa / UAb — cœur urbain mixte
            if (l.StartsWith("UA"))
                return new List<CommerceEntry>
                {
                    new CommerceEntry("Commerce de détail",      CommerceStatus.Autorise),
                    new CommerceEntry("Restauration",            CommerceStatus.Autorise),
                    new CommerceEntry("Commerce de gros",        CommerceStatus.SousConditions),
                    new CommerceEntry("Bureau / Services",       CommerceStatus.Autorise),
                    new CommerceEntry("Hébergement hôtelier",    CommerceStatus.Autorise),
                    new CommerceEntry("Artisanat",               CommerceStatus.Autorise),
                };

            // UB — urbain intermédiaire
            if (l.StartsWith("UB"))
                return new List<CommerceEntry>
                {
                    new CommerceEntry("Commerce de détail",      CommerceStatus.SousConditions),
                    new CommerceEntry("Restauration",            CommerceStatus.SousConditions),
                    new CommerceEntry("Commerce de gros",        CommerceStatus.Interdit),
                    new CommerceEntry("Bureau / Services",       CommerceStatus.SousConditions),
                    new CommerceEntry("Hébergement hôtelier",    CommerceStatus.SousConditions),
                    new CommerceEntry("Artisanat",               CommerceStatus.SousConditions),
                };

            // UC / UD — résidentiel
            if (l.StartsWith("UC") || l.StartsWith("UD"))
                return new List<CommerceEntry>
                {
                    new CommerceEntry("Commerce de détail",      CommerceStatus.Interdit),
                    new CommerceEntry("Restauration",            CommerceStatus.Interdit),
                    new CommerceEntry("Commerce de gros",        CommerceStatus.Interdit),
                    new CommerceEntry("Bureau / Services",       CommerceStatus.SousConditions),
                    new CommerceEntry("Hébergement hôtelier",    CommerceStatus.Interdit),
                    new CommerceEntry("Artisanat",               CommerceStatus.SousConditions),
                };

            // UX / UE / UI / UZ — zones d'activités économiques
            if (l.StartsWith("UX") || l.StartsWith("UE") ||
                l.StartsWith("UI") || l.StartsWith("UZ"))
                return new List<CommerceEntry>
                {
                    new CommerceEntry("Commerce de détail",      CommerceStatus.SousConditions),
                    new CommerceEntry("Restauration",            CommerceStatus.SousConditions),
                    new CommerceEntry("Commerce de gros",        CommerceStatus.Autorise),
                    new CommerceEntry("Bureau / Services",       CommerceStatus.Autorise),
                    new CommerceEntry("Hébergement hôtelier",    CommerceStatus.SousConditions),
                    new CommerceEntry("Artisanat",               CommerceStatus.Autorise),
                };

            // UT — touristique / loisirs
            if (l.StartsWith("UT"))
                return new List<CommerceEntry>
                {
                    new CommerceEntry("Commerce de détail",      CommerceStatus.SousConditions),
                    new CommerceEntry("Restauration",            CommerceStatus.Autorise),
                    new CommerceEntry("Commerce de gros",        CommerceStatus.Interdit),
                    new CommerceEntry("Bureau / Services",       CommerceStatus.SousConditions),
                    new CommerceEntry("Hébergement hôtelier",    CommerceStatus.Autorise),
                    new CommerceEntry("Artisanat",               CommerceStatus.SousConditions),
                };

            // U générique (sous-zone non reconnue)
            return new List<CommerceEntry>
            {
                new CommerceEntry("Commerce de détail",          CommerceStatus.SousConditions),
                new CommerceEntry("Restauration",                CommerceStatus.SousConditions),
                new CommerceEntry("Commerce de gros",            CommerceStatus.SousConditions),
                new CommerceEntry("Bureau / Services",           CommerceStatus.SousConditions),
                new CommerceEntry("Hébergement hôtelier",        CommerceStatus.SousConditions),
                new CommerceEntry("Artisanat",                   CommerceStatus.SousConditions),
            };
        }

        // Fallback
        return new List<CommerceEntry>
        {
            new CommerceEntry("Consulter le règlement officiel", CommerceStatus.SousConditions)
        };
    }
}

// ── Modèles annexes ───────────────────────────────────────────────────────────

public enum CommerceStatus { Autorise, SousConditions, Interdit }

public class CommerceEntry
{
    public string Label;
    public CommerceStatus Status;

    public CommerceEntry(string label, CommerceStatus status)
    {
        Label = label;
        Status = status;
    }

    public string StatusIcon => Status switch
    {
        CommerceStatus.Autorise => "✅",
        CommerceStatus.SousConditions => "⚠️",
        CommerceStatus.Interdit => "❌",
        _ => "?"
    };

    public string StatusLabel => Status switch
    {
        CommerceStatus.Autorise => "Autorisé",
        CommerceStatus.SousConditions => "Sous conditions",
        CommerceStatus.Interdit => "Interdit",
        _ => "Inconnu"
    };

    // Couleurs hex pour l'UI Unity
    public string StatusColor => Status switch
    {
        CommerceStatus.Autorise => "#4CAF50",
        CommerceStatus.SousConditions => "#FF9800",
        CommerceStatus.Interdit => "#F44336",
        _ => "#9E9E9E"
    };
}