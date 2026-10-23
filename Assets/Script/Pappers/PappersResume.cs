using System;
using System.Linq;

/// Le résumé d'entreprise inséré dans le commentaire d'un locataire, et la façon de
/// l'y remplacer sans toucher au reste.
///
/// Cette logique vivait en méthodes privées de `LocatairePrefab`, donc accessible
/// seulement depuis la fiche ouverte. La mise à jour se faisant désormais au
/// lancement, pour tous les locataires et sans écran, elle devait en sortir — et elle
/// devient testable au passage : c'est du texte, la pire chose à vérifier à l'œil.
public static class PappersResume
{
    public const string Debut = "-- PAPPERS --";
    public const string Fin   = "-- FIN PAPPERS --";

    /// Remplace le bloc Pappers d'un commentaire, ou l'ajoute s'il n'y en a pas.
    /// Ce que l'utilisatrice a écrit autour est conservé, avant comme après.
    public static string Fusionner(string commentaire, string contenu)
    {
        string current = commentaire ?? "";
        int start = current.IndexOf(Debut, StringComparison.Ordinal);
        int end = current.IndexOf(Fin, StringComparison.Ordinal);
        string before, after = string.Empty;

        if (start >= 0 && end > start)
        {
            before = current.Substring(0, start).TrimEnd('\n', '\r', ' ');
            int apres = end + Fin.Length;
            if (apres < current.Length)
                after = current.Substring(apres).TrimStart('\n', '\r', ' ');
        }
        else before = current.TrimEnd('\n', '\r', ' ');

        string bloc = $"{Debut}\n{contenu}\n{Fin}";
        if (!string.IsNullOrEmpty(before) && !string.IsNullOrEmpty(after)) return $"{before}\n\n{bloc}\n\n{after}";
        if (!string.IsNullOrEmpty(before)) return $"{before}\n\n{bloc}";
        if (!string.IsNullOrEmpty(after)) return $"{bloc}\n\n{after}";
        return bloc;
    }

    /// Le résumé lisible d'une entreprise, tel qu'il s'affiche dans le commentaire.
    public static string Construire(AnnuaireEntreprise data)
    {
        if (data == null) return "";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{data.nom_complet}</b>");
        if (!string.IsNullOrEmpty(data.libelle_nature_juridique)) sb.AppendLine(data.libelle_nature_juridique);
        if (!string.IsNullOrEmpty(data.siren)) sb.AppendLine($"SIREN : {FormatSiren(data.siren)}");
        if (!string.IsNullOrEmpty(data.date_creation)) sb.AppendLine($"Créée le : {FormatDate(data.date_creation)}");
        if (!string.IsNullOrEmpty(data.libelle_tranche_effectif)) sb.AppendLine($"Effectif : {data.libelle_tranche_effectif}");
        sb.AppendLine();
        sb.AppendLine("── Statut ──");
        sb.AppendLine(data.etat_administratif == "A" ? "Actif" : "Cessé");
        sb.AppendLine();
        sb.AppendLine("── Activité ──");
        if (!string.IsNullOrEmpty(data.activite_principale))
            sb.AppendLine($"NAF {data.activite_principale} · {data.libelle_activite_principale}");
        if (data.siege != null)
        {
            sb.AppendLine();
            sb.AppendLine("── Siège ──");
            sb.AppendLine(data.siege.adresse);
            sb.AppendLine($"{data.siege.code_postal} {data.siege.commune}");
        }
        sb.AppendLine();
        sb.AppendLine("── Finances ──");
        sb.AppendLine("Consulter sur Pappers pour CA et résultat");
        return sb.ToString().TrimEnd();
    }

    // ── Étape 10 : « Ajouter une entreprise » (décisions du 23/10) ───────────────

    /// Peut-on ajouter cette société à CIPL ? Null si oui, sinon le motif. Annuaire injoignable
    /// (ni données ni « introuvable ») : null, la vérification n'a pas pu se faire.
    /// ponytail: vérifié par CIPL, pas par le serveur (annuaire public, sans clé à protéger).
    public static string Refus(string siret, AnnuaireEntreprise data, bool introuvable)
    {
        string chiffres = new string((siret ?? "").Where(char.IsDigit).ToArray());
        bool autreSociete = data != null && !string.IsNullOrEmpty(data.siren) && chiffres.Length >= 9
                            && data.siren != chiffres.Substring(0, 9);
        if (introuvable || autreSociete)
            return "Ce SIRET est introuvable dans l'annuaire des entreprises : vérifiez les chiffres.";
        if (data != null && data.etat_administratif == "C")
            return $"« {data.nom_complet} » est fermée (cessée) : elle ne peut pas être ajoutée.";
        return null;
    }

    /// La forme et le siège, pour vérifier d'un coup d'œil que c'est la bonne société :
    /// « Société civile immobilière · 12 RUE DE RIVOLI 75004 PARIS ».
    public static string Description(AnnuaireEntreprise data)
    {
        if (data == null) return "";
        string siege = data.siege == null ? null
                     : !string.IsNullOrWhiteSpace(data.siege.adresse) ? data.siege.adresse
                     : $"{data.siege.code_postal} {data.siege.commune}".Trim();
        return string.Join(" · ", new[] { data.libelle_nature_juridique, siege }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    /// Un SIRET utilisable pour l'interrogation : 9 chiffres au minimum.
    public static bool SiretExploitable(string siret)
        => !string.IsNullOrWhiteSpace(siret) && siret.Trim().Length >= 9;

    public static string FormatSiren(string s)
        => s?.Length == 9 ? $"{s.Substring(0, 3)} {s.Substring(3, 3)} {s.Substring(6, 3)}" : s;

    public static string FormatDate(string iso)
        => DateTime.TryParse(iso, out var dt) ? dt.ToString("dd/MM/yyyy") : iso;
}
