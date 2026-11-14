using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// Revue 17, reprise du 09/11, lot B, groupe « stripe » : tests texte sur les fonctions serveur abonnement-stripe et
/// stripe-webhook (D3-13, D3-14, D3-18 + D3-07, D1-07, D1-12, D3-10 côté serveur). emails-abonnement.ts (D3-13, D3-10) est
/// couvert par Revue17Tests.D1_01 une fois le script lotB_emails.py appliqué.
public class LotBStripeTests
{
    static string Ts(string nom) => File.ReadAllText(Path.Combine("Serveur", "fonctions", nom));
    static string Abo => Ts("abonnement-stripe.ts");
    static string Webhook => Ts("stripe-webhook.ts");

    /// Le texte d'une action d'abonnement-stripe, de son `action === "x"` à l'action suivante.
    static string Action(string nom)
    {
        string ts = Abo;
        int i = ts.IndexOf($"action === \"{nom}\"", StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), nom + " absente d'abonnement-stripe.ts");
        int fin = ts.IndexOf("if (action === ", i + 1, StringComparison.Ordinal);
        return fin < 0 ? ts.Substring(i) : ts.Substring(i, fin - i);
    }

    /// Le corps d'une fonction Deno de stripe-webhook.ts, jusqu'à la fonction suivante.
    static string Fonction(string ts, string nom)
    {
        int i = ts.IndexOf("async function " + nom + "(", StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), nom + " absente");
        int fin = ts.IndexOf("\n/**", i, StringComparison.Ordinal);
        if (fin < 0) fin = ts.IndexOf("\nDeno.serve(", i, StringComparison.Ordinal);
        return ts.Substring(i, fin - i);
    }

    static string Ligne(string ts, string debut) => ts.Split('\n').Single(l => l.TrimStart().StartsWith(debut, StringComparison.Ordinal));

    // ── D3-13 : le prorata des licences et entreprises ajoutées est facturé tout de suite ──

    [Test]
    public void D3_13_ajuster_facture_le_prorata_tout_de_suite()
    {
        string ajuster = Action("ajuster");
        StringAssert.Contains("proration_behavior: \"always_invoice\"", ajuster, "facture et prélèvement immédiats, avoir immédiat");
        StringAssert.DoesNotContain("create_prorations", Abo, "attendait la facture suivante : un an sur l'annuel, jamais si résilié à l'échéance");
        StringAssert.Contains("await stripe.subscriptions.update(sub.id, { items, ...prorata });", ajuster, "un seul appel Stripe : une seule facture de prorata");
        StringAssert.DoesNotContain("subscriptionItems.", ajuster, "plus d'appel ligne par ligne (deux factures pour une entreprise ajoutée)");
        StringAssert.Contains("groupe.statut === \"expire\"", ajuster, "pas de facture de plus sur un abonnement impayé");
    }

    // ── D3-14 : la répartition des licences part en une métadonnée Stripe par entreprise ──

    [Test]
    public void D3_14_la_repartition_part_en_une_metadonnee_par_entreprise()
    {
        string choisir = Action("choisir");
        StringAssert.DoesNotContain("licences: JSON.stringify", Abo, "une seule valeur tenait 500 caractères, soit 12 entreprises");
        StringAssert.Contains("parEntreprise[\"l_\" + e.id] = l;", choisir);
        StringAssert.Contains("metadata: { groupe_id: g, ...parEntreprise }", choisir);
        string repartir = Fonction(Webhook, "repartir");
        StringAssert.Contains("cle.startsWith(\"l_\")", repartir);
        StringAssert.Contains(".eq(\"id\", cle.slice(2))", repartir);
        StringAssert.DoesNotContain("JSON.parse", repartir, "l'ancienne forme « licences » n'est plus lue : repartir ne sert qu'à un abonnement nouveau");
        Assert.That(("l_" + Guid.Empty).Length, Is.LessThanOrEqualTo(40), "une clé de métadonnée Stripe tient 40 caractères");
    }

    // ── D3-18 et D3-07 : un abonnement impayé reste connu ; son retour ne rejoue pas la répartition du paiement ──

    [Test]
    public void D3_18_un_abonnement_impaye_reste_connu_et_sa_repartition_n_est_pas_rejouee()
    {
        string ligne = Ligne(Fonction(Webhook, "appliquer"), "stripe_abonnement:");
        StringAssert.Contains("sub.status === \"canceled\" || sub.status === \"incomplete_expired\" ? null : sub.id", ligne);
        StringAssert.DoesNotContain("st === \"expire\" ? null", ligne, "« unpaid » vidait l'identifiant : son retour passait pour un abonnement nouveau");
        // D3-02 inchangé : la répartition s'applique une fois par abonnement nouveau en base (avant !== sub.id).
        StringAssert.Contains("if (r && avant !== sub.id && sub.status !== \"canceled\") await repartir(sub);", Webhook);
        // « unpaid » vaut toujours lecture seule (16c, D3-07).
        StringAssert.Contains("if (s === \"unpaid\" || s === \"canceled\" || s === \"incomplete_expired\" || s === \"paused\") return \"expire\";", Webhook);
        // Même paire que « choisir » (abonnement-stripe, D1-05 de la 16c) pour dire qu'un abonnement est terminé.
        StringAssert.Contains("s.status !== \"canceled\" && s.status !== \"incomplete_expired\"", Abo);
    }

    // ── D1-07 : une panne pendant la relecture de l'abonnement gardé n'est pas « abonnement absent » ──

    [Test]
    public void D1_07_une_panne_de_Stripe_ne_vaut_pas_abonnement_absent()
    {
        string appliquer = Fonction(Webhook, "appliquer");
        StringAssert.Contains("e?.code === \"resource_missing\"", appliquer, "seule l'absence vaut « terminé »");
        StringAssert.DoesNotContain(".catch(() => null)", appliquer, "toute erreur passait pour une absence : l'événement de l'ancien abonnement était appliqué");
        StringAssert.Contains("throw e;", appliquer, "une autre erreur remonte (500) : Stripe relivre l'événement");
    }

    // ── D1-12 : « choisir » exige un nombre entier de licences, comme « ajuster » ──

    [Test]
    public void D1_12_choisir_refuse_un_nombre_de_licences_non_entier()
    {
        const string garde = "|| l < 1) return json({ erreur: \"Nombre de licences invalide.\" });";
        StringAssert.Contains("if (!Number.isInteger(l) " + garde, Action("choisir"));
        StringAssert.Contains("if (!Number.isInteger(licences) || licences < 1) return json({ erreur: \"Nombre de licences invalide.\" });", Action("ajuster"), "même garde, même message");
    }

    // ── D3-10 (serveur) : la page de retour ne dit plus « Paiement enregistré » quand rien n'est prélevé ──

    [Test]
    public void D3_10_la_page_de_retour_dit_moyen_de_paiement_enregistre()
    {
        string paye = Ligne(Webhook, "paye:");
        StringAssert.Contains("Moyen de paiement enregistré.", paye);
        StringAssert.DoesNotContain("\"Paiement enregistré.", paye, "en essai, rien n'est prélevé avant la fin de l'essai");
    }
}
