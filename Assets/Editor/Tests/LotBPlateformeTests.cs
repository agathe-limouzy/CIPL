using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// Revue 17, lot B (09/11/2026), groupe « plateforme » : D1-11 et D5-24. Pas de Deno dans l'éditeur : tests texte
/// sur Serveur/fonctions/facture-electronique.ts (même façon que Revue17Tests et Revue16cTests).
public class LotBPlateformeTests
{
    static string Serveur(params string[] chemin) =>
        File.ReadAllText(Path.Combine(new[] { Application.dataPath, "..", "Serveur" }.Concat(chemin).ToArray()));

    // ── D1-11 : une exception après la réservation plateformes_envois la rend (plus de 10 min de faux « en cours ») ──

    [Test]
    public void D1_11_une_exception_apres_la_reservation_rend_la_reservation()
    {
        string ts = Serveur("fonctions", "facture-electronique.ts");
        int serve = ts.IndexOf("Deno.serve(", StringComparison.Ordinal);
        int declaration = ts.IndexOf("let liberer", StringComparison.Ordinal);
        Assert.That(declaration, Is.GreaterThan(serve).And.LessThan(ts.IndexOf("try {", serve, StringComparison.Ordinal)),
            "liberer est déclaré avant le try général, pour que son catch le voie");
        int reservation = ts.IndexOf("liberer = () => service.from(\"plateformes_envois\").delete()", StringComparison.Ordinal);
        Assert.That(reservation, Is.GreaterThan(ts.IndexOf("ignoreDuplicates: true", StringComparison.Ordinal)), "défini juste après la réservation (16c, D5-02)");
        StringAssert.Contains("if (!s?.ok) { await liberer(); return await refus(s); }", ts, "un refus de Super PDP la rend toujours (16c)");
        // Le catch général : le seul passage de toute exception de l'envoi (JSON.parse, fe[partie][champ], atob dans envoi).
        int general = ts.LastIndexOf("catch (e) {", StringComparison.Ordinal);
        Assert.That(general, Is.GreaterThan(ts.IndexOf("JSON.parse(String(facture", StringComparison.Ordinal)), "le catch général suit l'envoi");
        string rattrape = ts.Substring(general);
        StringAssert.Contains("if (liberer) await liberer();", rattrape, "D1-11 : la réservation est rendue");
        Assert.That(rattrape.IndexOf("await liberer()", StringComparison.Ordinal), Is.LessThan(rattrape.IndexOf("refus: \"erreur\"", StringComparison.Ordinal)),
            "rendue avant la réponse « erreur » (500)");
    }

    // ── D5-24 : l'identifiant de facture est contrôlé (chiffres) et encodé avant d'entrer dans une adresse Super PDP ──

    [Test]
    public void D5_24_l_identifiant_de_facture_est_controle_par_le_serveur()
    {
        string ts = Serveur("fonctions", "facture-electronique.ts");
        int controle = ts.IndexOf("(action === \"evenements\" || action === \"encaisser\") && !/^\\d+$/.test(idFacture)", StringComparison.Ordinal);
        Assert.That(controle, Is.GreaterThan(0).And.LessThan(ts.IndexOf("action === \"evenements\") {", StringComparison.Ordinal)),
            "le contrôle précède « evenements » et « encaisser »");
        StringAssert.Contains("refus: \"refusee\", erreur: \"identifiant de facture invalide\"", ts);
        // Les deux adresses et le corps de l'événement fr:212 : plus d'identifiant brut.
        StringAssert.Contains("invoices/${encodeURIComponent(idFacture)}?expand[]=events", ts);
        StringAssert.Contains("invoices/${encodeURIComponent(id)}?format=en16931", ts);
        StringAssert.Contains("invoice_id: Number(idFacture)", ts);
        StringAssert.Contains("ventilation(auth, idFacture)", ts);
        StringAssert.DoesNotContain("Number(id) || id", ts);
        StringAssert.DoesNotContain("invoices/${id}", ts);
        // Le motif lu dans le fichier, face au scénario de la fiche (règle ECMAScript, comme Deno).
        var motif = new Regex(Regex.Match(ts, @"/(\^\\d\+\$)/\.test\(").Groups[1].Value, RegexOptions.ECMAScript);
        Assert.That(motif.IsMatch("12345"), Is.True);
        foreach (var faux in new[] { "", "1?expand[]=events&x=/../oauth2_sessions/me", "42/../french_directory/entries", "12 ", "pdp-1" })
            Assert.That(motif.IsMatch(faux), Is.False, faux);
    }
}
