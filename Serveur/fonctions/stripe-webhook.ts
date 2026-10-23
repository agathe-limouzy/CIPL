// ════════════════════════════════════════════════════════════════════════════
// CIPL — étape 9c2 : fonction serveur « stripe-webhook » (Supabase › Edge Functions).
// Stripe l'appelle à chaque changement d'abonnement : elle recopie l'état dans `groupes`
// (statut, licences, formule, échéance, résiliation). « Verify JWT » DÉSACTIVÉ : Stripe n'a pas
// de jeton Supabase, il SIGNE ses messages (secret STRIPE_WEBHOOK_SECRET, whsec_…).
// Elle sert aussi la page de retour du navigateur après le paiement (texte simple).
// Secrets : STRIPE_SECRET_KEY et STRIPE_WEBHOOK_SECRET (Supabase › Edge Functions › Secrets).
// ════════════════════════════════════════════════════════════════════════════
import Stripe from "npm:stripe@17";
import { createClient } from "npm:@supabase/supabase-js@2";

const stripe = new Stripe(Deno.env.get("STRIPE_SECRET_KEY")!, { httpClient: Stripe.createFetchHttpClient() });
const service = createClient(Deno.env.get("SUPABASE_URL")!, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!);
const signature = Stripe.createSubtleCryptoProvider();

// Supabase n'affiche que du texte brut sur son adresse par défaut (pas de page HTML).
const RETOURS: Record<string, string> = {
  paye: "Paiement enregistré. Vous pouvez fermer cette page et revenir dans CIPL : l'abonnement s'y met à jour tout seul.",
  annule: "Paiement annulé : rien n'a été prélevé. Vous pouvez fermer cette page et revenir dans CIPL.",
  portail: "Vous pouvez fermer cette page et revenir dans CIPL.",
};

/** Le statut CIPL (groupes.statut) d'un abonnement Stripe. Paiement refusé : Stripe relance,
 *  CIPL reste modifiable ; toutes les relances échouées, Stripe annule : lecture seule. */
function statut(s: string): string {
  if (s === "past_due" || s === "unpaid") return "refuse";
  if (s === "canceled" || s === "incomplete_expired" || s === "paused") return "expire";
  return "actif";   // trialing, active, incomplete (prélèvement SEPA en cours de traitement)
}

async function appliquer(sub: Stripe.Subscription) {
  const g = sub.metadata?.groupe_id;
  if (!g) return;   // abonnement créé hors de CIPL
  const st = statut(sub.status);
  const licence = sub.items.data.find((i) => i.price.lookup_key?.startsWith("cipl_licence_"));
  // L'échéance est sur l'abonnement, ou sur ses lignes selon la version de l'API Stripe.
  // deno-lint-ignore no-explicit-any
  const fin: number | undefined = (sub as any).current_period_end ?? (sub.items.data[0] as any)?.current_period_end;
  const { error } = await service.from("groupes").update({
    statut: st,
    stripe_abonnement: st === "expire" ? null : sub.id,
    licences_max: licence?.quantity ?? undefined,
    formule: sub.items.data[0]?.price.recurring?.interval === "year" ? "annuel" : "mensuel",
    echeance: fin ? new Date(fin * 1000).toISOString().slice(0, 10) : null,
    resilie: sub.cancel_at_period_end,
  }).eq("id", g);
  if (error) throw error;   // réponse 500 : Stripe renverra l'événement
}

Deno.serve(async (req) => {
  if (req.method === "GET") {
    const r = new URL(req.url).searchParams.get("retour") ?? "";
    return new Response(RETOURS[r] ?? RETOURS.portail, { headers: { "Content-Type": "text/plain; charset=utf-8" } });
  }

  const corps = await req.text();
  let evt: Stripe.Event;
  try {
    evt = await stripe.webhooks.constructEventAsync(corps, req.headers.get("Stripe-Signature") ?? "",
                                                    Deno.env.get("STRIPE_WEBHOOK_SECRET")!, undefined, signature);
  } catch (e) {
    return new Response("Signature refusée : " + (e as Error).message, { status: 400 });
  }

  if (evt.type.startsWith("customer.subscription.")) {
    // Relu chez Stripe plutôt que pris dans l'événement : les événements peuvent arriver dans
    // le désordre, l'état relu est toujours le dernier.
    const sub = await stripe.subscriptions.retrieve((evt.data.object as Stripe.Subscription).id);
    await appliquer(sub);
  }
  return new Response("ok");
});
