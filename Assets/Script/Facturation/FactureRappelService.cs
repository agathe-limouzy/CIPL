using System;
using System.Globalization;
using System.Text;
using UnityEngine;

/// Rappel d'échéance pour une facture impayée (diagramme : « option d'envoyer un
/// rappel » → « mail de rappel »). AUCUN envoi automatique : on ouvre un brouillon
/// dans la messagerie de l'utilisatrice (mailto:), elle valide l'envoi elle-même.
/// La date du rappel est mémorisée sur l'enregistrement stocké du suivi.
public static class FactureRappelService
{
    static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static void Demander(Locataire loc, FactureEtat l, Action onDone)
    {
        if (loc == null || l == null) return;

        string email    = (loc.emailLocataire ?? "").Trim();
        string echeance = DateTime.TryParse(l.echeanceISO, out var e) ? e.ToString("dd/MM/yyyy") : "—";
        string dejaTxt  = DateTime.TryParse(l.dernierRappelISO, out var dr)
            ? $"\n\nUn rappel a déjà été préparé le {dr:dd/MM/yyyy}." : "";

        string detail = string.IsNullOrEmpty(email)
            ? "Aucune adresse email n'est renseignée pour ce locataire (fiche → General). "
              + "Le rappel sera noté, mais aucun brouillon ne pourra s'ouvrir." + dejaTxt
            : $"Un brouillon d'email de rappel va s'ouvrir dans votre messagerie, à destination de "
              + $"{email}. Rien n'est envoyé automatiquement — vous validez l'envoi vous-même." + dejaTxt;

        void Confirmer()
        {
            if (!string.IsNullOrEmpty(email))
            {
                string sujet = $"Rappel — {l.libelle} — échéance dépassée";
                string corps = BuildCorps(l, echeance);
                Application.OpenURL($"mailto:{email}?subject={Esc(sujet)}&body={Esc(corps)}");
            }
            FacturationSuivi.MarquerRappel(loc, l.key);
            onDone?.Invoke();
            UndoToast.Instance?.ShowInfo(string.IsNullOrEmpty(email)
                ? "Rappel noté (aucun email : renseignez l'adresse du locataire)."
                : "Brouillon de rappel ouvert dans votre messagerie.");
        }

        if (ConfirmDialog.Instance != null)
            ConfirmDialog.Instance.Show("Rappel d'échéance", detail, Confirmer,
                string.IsNullOrEmpty(email) ? "Noter le rappel" : "Ouvrir le brouillon");
        else
            Confirmer();
    }

    static string BuildCorps(FactureEtat l, string echeance)
    {
        var R = ReglageService.Current;
        string signature = R != null && R.smtp != null && !string.IsNullOrWhiteSpace(R.smtp.fromName)
            ? R.smtp.fromName : "GROUPE CIPL";
        string montant = l.montant > 0f ? l.montant.ToString("#,##0.00", Fr) + " €" : "";

        var sb = new StringBuilder();
        sb.AppendLine("Bonjour,");
        sb.AppendLine();
        sb.Append($"Sauf erreur de notre part, la facture « {l.libelle} »");
        if (!string.IsNullOrEmpty(l.numero)) sb.Append($" (n° {l.numero})");
        if (!string.IsNullOrEmpty(montant)) sb.Append($", d'un montant de {montant},");
        sb.AppendLine($" échue le {echeance}, demeure impayée à ce jour.");
        sb.AppendLine();
        sb.AppendLine("Nous vous remercions de bien vouloir procéder à son règlement dans les meilleurs délais.");
        if (R != null && !string.IsNullOrWhiteSpace(R.phraseRetard))
        {
            sb.AppendLine();
            sb.AppendLine(R.phraseRetard);
        }
        sb.AppendLine();
        sb.AppendLine("Cordialement,");
        sb.Append(signature);
        return sb.ToString();
    }

    static string Esc(string s) => Uri.EscapeDataString(s ?? "");
}
