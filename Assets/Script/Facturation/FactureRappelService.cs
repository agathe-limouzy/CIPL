using System;
using UnityEngine;

/// Rappel d'échéance pour une facture impayée (diagramme : « option d'envoyer un
/// rappel » → « mail de rappel »). AUCUN envoi automatique : on ouvre un brouillon
/// dans la messagerie de l'utilisatrice (mailto:), elle valide l'envoi elle-même.
/// La date du rappel est mémorisée sur l'enregistrement stocké du suivi.
public static class FactureRappelService
{
    public static void Demander(Locataire loc, FactureEtat l, Action onDone)
    {
        if (loc == null || l == null) return;

        string email    = (loc.emailLocataire ?? "").Trim();
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
                var (sujet, corps) = Texte(loc, l);
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

    /// Le brouillon, d'après le modèle de Réglages › Facturation › Modèles d'e-mail (décision du 31/10).
    /// Paiement partiel (08/10) : le rappel réclame le reste, pas le total ({reste}).
    public static (string sujet, string corps) Texte(Locataire loc, FactureEtat l)
    {
        var R = ReglageService.Current;
        var ctx = FactureEnvoiAuto.Contexte(loc, l);
        FacturationSuivi.Solde(loc, l.key, out float total, out float paye);
        if (total <= 0f) total = l.montant;
        ctx.ttc = total;
        ctx.reste = Math.Max(0f, total - paye);
        return (FactureVarResolver.Resolve(FacturePdfService.Texte(R?.rappelObjet, EmailService.RappelObjetDefaut), loc, null, ctx),
                FactureVarResolver.Resolve(FacturePdfService.Texte(R?.rappelCorps, EmailService.RappelCorpsDefaut), loc, null, ctx));
    }

    static string Esc(string s) => Uri.EscapeDataString(s ?? "");
}
