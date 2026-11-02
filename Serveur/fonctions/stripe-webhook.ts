// ════════════════════════════════════════════════════════════════════════════
// CIPL — étape 9c2 : fonction serveur « stripe-webhook » (Supabase › Edge Functions).
// Stripe l'appelle à chaque changement d'abonnement : elle recopie l'état dans `groupes`
// (statut, licences, formule, échéance, résiliation). « Verify JWT » DÉSACTIVÉ : Stripe n'a pas
// de jeton Supabase, il SIGNE ses messages (secret STRIPE_WEBHOOK_SECRET, whsec_…).
// Elle sert aussi la page de retour du navigateur après le paiement (texte simple).
// Étape 14 : résiliation et fin d'abonnement → e-mail aux admins (fonction emails-abonnement).
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
 *  CIPL reste modifiable ; relances épuisées (« unpaid », réglage Stripe « marquer comme impayé » :
 *  plus aucune tentative, revue 16c D3-07) ou abonnement annulé : lecture seule. */
function statut(s: string): string {
  if (s === "past_due") return "refuse";
  if (s === "unpaid" || s === "canceled" || s === "incomplete_expired" || s === "paused") return "expire";
  return "actif";   // trialing, active, incomplete (prélèvement SEPA en cours de traitement)
}

const jourIso = (t: number | null | undefined) => t ? new Date(t * 1000).toISOString().slice(0, 10) : null;

async function appliquer(sub: Stripe.Subscription, avant: string | null): Promise<{ groupe: string; echeance: string | null } | null> {
  const g = sub.metadata?.groupe_id;
  if (!g) return null;   // abonnement créé hors de CIPL
  // Revue 16c (D1-11) : un événement d'un ANCIEN abonnement (relivré par Stripe après la création du nouveau) ne touche
  // pas au groupe, sauf si l'abonnement gardé est lui-même terminé chez Stripe.
  if (avant && avant !== sub.id) {
    const garde = await stripe.subscriptions.retrieve(avant).catch(() => null);
    if (garde && garde.status !== "canceled") { console.log("événement ignoré : abonnement", sub.id, "≠", avant); return null; }
  }
  const st = statut(sub.status);
  // L'échéance est sur l'abonnement, ou sur ses lignes selon la version de l'API Stripe.
  // deno-lint-ignore no-explicit-any
  const echeance = jourIso((sub as any).current_period_end ?? (sub.items.data[0] as any)?.current_period_end);
  const { error } = await service.from("groupes").update({
    statut: st,
    stripe_abonnement: st === "expire" ? null : sub.id,
    formule: sub.items.data[0]?.price.recurring?.interval === "year" ? "annuel" : "mensuel",
    echeance,
    resilie: sub.cancel_at_period_end,
  }).eq("id", g);
  if (error) throw error;   // réponse 500 : Stripe renverra l'événement
  return { groupe: g, echeance };
}

/** Étape 14 : l'e-mail « résiliation enregistrée » ou « abonnement terminé » aux admins
 *  (fonction emails-abonnement, qui ne l'envoie qu'une fois par date). ponytail: un échec est
 *  seulement journalisé, l'état du groupe est déjà à jour ; faire échouer l'événement (Stripe le
 *  renvoie pendant 3 jours) si des e-mails se perdent. */
async function prevenir(groupe: string, sorte: string, date: string | null) {
  if (!date) return;
  try {
    const r = await fetch(`${Deno.env.get("SUPABASE_URL")}/functions/v1/emails-abonnement`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")}` },
      body: JSON.stringify({ groupe, sorte, date }),
    });
    console.log("emails-abonnement", r.status, await r.text());
  } catch (e) {
    console.error("emails-abonnement", e);
  }
}

/** Licences par entreprise (décision du 23/10) : la répartition choisie avant le paiement
 *  (métadonnée « licences » posée par abonnement-stripe) s'applique à la création de
 *  l'abonnement, une seule fois ; ensuite, « + » / « - » dans CIPL (action ajuster). */
async function repartir(sub: Stripe.Subscription) {
  const g = sub.metadata?.groupe_id, r = sub.metadata?.licences;
  if (!g || !r) return;
  for (const [id, l] of Object.entries(JSON.parse(r) as Record<string, number>)) {
    const { error } = await service.from("entreprises").update({ licences_max: l }).eq("id", id).eq("groupe_id", g);
    if (error) throw error;
  }
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
    const gid = sub.metadata?.groupe_id;
    const avant = gid ? (await service.from("groupes").select("stripe_abonnement").eq("id", gid).maybeSingle()).data?.stripe_abonnement ?? null : null;
    // Revue 16c (D1-11) : la répartition initiale ne s'applique qu'au PREMIER « created » (un événement rejoué ne la refait pas).
    if (evt.type === "customer.subscription.created" && !avant) await repartir(sub);
    const r = await appliquer(sub, avant);
    // Sur l'état relu, pas sur le type d'événement : un « updated » arrivé après la fin ne
    // redonne pas « résiliation enregistrée ». Fin : le jour réel (paiements refusés : avant l'échéance).
    if (r && sub.status === "canceled") await prevenir(r.groupe, "abonnement_fin", jourIso(sub.ended_at) ?? r.echeance);
    else if (r && sub.cancel_at_period_end) await prevenir(r.groupe, "resiliation", r.echeance);
  }
  return new Response("ok");
});
