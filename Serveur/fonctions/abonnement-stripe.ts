// ════════════════════════════════════════════════════════════════════════════
// CIPL — étape 9c2 : fonction serveur « abonnement-stripe » (Supabase › Edge Functions).
// Appelée par CIPL avec le jeton de l'admin connecté : « Verify JWT » reste ACTIVÉ.
// Actions :
//   choisir  → page de paiement Stripe (carte ou SEPA), premier prélèvement à la fin de l'essai ;
//   portail  → « Paiement et factures » (portail client Stripe : moyen de paiement, factures, résiliation) ;
//   ajuster  → nombre de licences, et d'entreprises après « Ajouter une entreprise », au prorata ;
//   resilier / reprendre → résiliation à l'échéance, et retour en arrière jusque-là.
// Secret à ranger dans Supabase › Edge Functions › Secrets : STRIPE_SECRET_KEY (sk_test_… en test).
// La clé ne quitte jamais le serveur : CIPL ne voit ni elle, ni aucun numéro de carte.
// ════════════════════════════════════════════════════════════════════════════
import Stripe from "npm:stripe@17";
import { createClient } from "npm:@supabase/supabase-js@2";

const stripe = new Stripe(Deno.env.get("STRIPE_SECRET_KEY")!, { httpClient: Stripe.createFetchHttpClient() });
const URL_PROJET = Deno.env.get("SUPABASE_URL")!;
const service = createClient(URL_PROJET, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!);
const RETOUR = `${URL_PROJET}/functions/v1/stripe-webhook?retour=`;

// Prix HT par mois, en centimes (décisions du 19/10 et du 22/10) ; l'annuel vaut 10 mois.
// Mêmes valeurs que RegleAbonnement côté CIPL. TVA 20 % en plus.
// ponytail: un prix est créé dans Stripe à sa première utilisation ; pour le changer, archiver
// l'ancien dans Stripe (il disparaît des recherches) : le nouveau est créé au prochain paiement.
const PRIX: Record<string, number> = { entreprise: 4000, entreprise_suivante: 2800, licence: 1500 };
const NOMS: Record<string, string> = {
  entreprise: "CIPL — entreprise",
  entreprise_suivante: "CIPL — entreprise supplémentaire (-30 %)",
  licence: "CIPL — licence utilisateur",
};

const json = (corps: unknown, status = 200) =>
  new Response(JSON.stringify(corps), { status, headers: { "Content-Type": "application/json" } });

/** Le prix Stripe d'une ligne, retrouvé par sa clé (cipl_<ligne>_<formule>), sinon créé. */
async function prix(ligne: string, formule: string): Promise<string> {
  const cle = `cipl_${ligne}_${formule}`;
  const trouves = await stripe.prices.list({ lookup_keys: [cle], active: true, limit: 1 });
  if (trouves.data.length) return trouves.data[0].id;
  const p = await stripe.prices.create({
    currency: "eur",
    unit_amount: PRIX[ligne] * (formule === "annuel" ? 10 : 1),
    recurring: { interval: formule === "annuel" ? "year" : "month" },
    lookup_key: cle,
    product_data: { name: NOMS[ligne] },
  });
  return p.id;
}

/** La TVA française de 20 %, ajoutée au prix HT (créée à la première utilisation). */
async function tva(): Promise<string> {
  const taux = await stripe.taxRates.list({ active: true, limit: 100 });
  const t = taux.data.find((x) => x.display_name === "TVA" && x.percentage === 20 && !x.inclusive);
  if (t) return t.id;
  const cree = await stripe.taxRates.create({
    display_name: "TVA", description: "TVA France 20 %", percentage: 20, inclusive: false, country: "FR", jurisdiction: "FR",
  });
  return cree.id;
}

Deno.serve(async (req) => {
  try {
    // `cible` : l'entreprise dont on change les licences (ajuster) ; `repartition` : les licences
    // de chaque entreprise au choix de l'abonnement (licences par entreprise, décision du 23/10).
    const { action, entreprise, formule, licences, cible, repartition } = await req.json();
    const jeton = req.headers.get("Authorization") ?? "";

    // Seul un admin de l'entreprise passe : la même règle que dans CIPL (groupe_admin).
    const utilisateur = createClient(URL_PROJET, Deno.env.get("SUPABASE_ANON_KEY")!, { global: { headers: { Authorization: jeton } } });
    const { data: g, error: refus } = await utilisateur.rpc("groupe_admin", { p_entreprise: entreprise });
    if (refus || !g) return json({ erreur: "Réservé à l'administrateur." }, 403);

    const { data: groupe, error: e1 } = await service.from("groupes").select("*").eq("id", g).single();
    if (e1) throw e1;
    const { data: ents, error: e2 } = await service.from("entreprises").select("id, nom, licences_max").eq("groupe_id", g).order("cree_le");
    if (e2) throw e2;
    const n = Math.max(1, ents?.length ?? 1);
    const utilisees = async (id: string): Promise<number> =>
      (await service.rpc("licences_utilisees_entreprise", { p_entreprise: id })).data ?? 0;

    if (action === "choisir") {
      if (groupe.stripe_abonnement) return json({ erreur: "Un abonnement est déjà en cours : voyez « Paiement et factures »." });
      const f = formule === "annuel" ? "annuel" : "mensuel";

      // Licences de chaque entreprise : au moins celles qu'elle utilise. Elles ne s'appliquent
      // qu'au paiement (stripe-webhook, à la création de l'abonnement) : un paiement abandonné ne
      // change rien.
      const choix = new Map<string, number>((repartition ?? []).map((r: { entreprise: string; licences: number }) => [r.entreprise, r.licences]));
      const parEntreprise: Record<string, number> = {};
      for (const e of ents ?? []) {
        const l = Number(choix.get(e.id) ?? e.licences_max), u = await utilisees(e.id);
        if (!(l >= Math.max(1, u))) return json({ erreur: `${e.nom} : ${u} licences sont utilisées, il en faut au moins autant.` });
        parEntreprise[e.id] = l;
      }
      const total = Object.values(parEntreprise).reduce((a, b) => a + b, 0);

      let client = groupe.stripe_client;
      if (!client) {
        const { data: moi } = await utilisateur.auth.getUser(jeton.replace(/^Bearer\s+/i, ""));
        // Étape 14 : les reçus, paiements refusés et cartes qui expirent sont envoyés par Stripe, en français.
        client = (await stripe.customers.create({ email: moi.user?.email, name: groupe.nom, preferred_locales: ["fr"], metadata: { groupe_id: g } })).id;
        const { error } = await service.from("groupes").update({ stripe_client: client }).eq("id", g);
        if (error) throw error;
      }

      const lignes = [{ price: await prix("entreprise", f), quantity: 1 }];
      if (n > 1) lignes.push({ price: await prix("entreprise_suivante", f), quantity: n - 1 });
      lignes.push({ price: await prix("licence", f), quantity: total });

      // Premier prélèvement le lendemain de la fin de l'essai (décision du 22/10). Stripe exige au
      // moins 48 h d'avance : plus près de la fin, le paiement part tout de suite.
      const lendemain = Date.parse(`${groupe.fin_essai}T00:00:00Z`) / 1000 + 86400;
      const essai = groupe.statut === "essai" && lendemain > Date.now() / 1000 + 48 * 3600;

      const session = await stripe.checkout.sessions.create({
        mode: "subscription",
        customer: client,
        client_reference_id: g,
        locale: "fr",
        payment_method_types: ["card", "sepa_debit"],
        payment_method_collection: "always",
        line_items: lignes,
        subscription_data: {
          // ponytail: une valeur de métadonnée Stripe tient 500 caractères, soit une dizaine
          // d'entreprises ; au-delà, garder la répartition sur le serveur en attendant le paiement.
          metadata: { groupe_id: g, licences: JSON.stringify(parEntreprise) },
          default_tax_rates: [await tva()],
          ...(essai ? { trial_end: lendemain } : {}),
        },
        success_url: RETOUR + "paye",
        cancel_url: RETOUR + "annule",
      });
      return json({ url: session.url });
    }

    if (action === "portail") {
      if (!groupe.stripe_client) return json({ erreur: "Aucun abonnement : choisissez-en un d'abord." });
      const s = await stripe.billingPortal.sessions.create({ customer: groupe.stripe_client, return_url: RETOUR + "portail", locale: "fr" });
      return json({ url: s.url });
    }

    if (action === "ajuster") {
      if (!groupe.stripe_abonnement) return json({ erreur: "Aucun abonnement en cours." });
      const sub = await stripe.subscriptions.retrieve(groupe.stripe_abonnement);
      const f = sub.items.data[0]?.price.recurring?.interval === "year" ? "annuel" : "mensuel";
      const ligne = (nom: string) => sub.items.data.find((i) => i.price.lookup_key === `cipl_${nom}_${f}`);
      const prorata = { proration_behavior: "create_prorations" as const };

      // Licences de l'entreprise `cible` (0 : inchangées) ; jamais moins que celles qu'elle
      // utilise. La ligne « licence » de Stripe suit le total des entreprises.
      if (licences && cible) {
        const e = (ents ?? []).find((x) => x.id === cible);
        if (!e) return json({ erreur: "Cette entreprise n'est pas dans le groupe." });
        const u = await utilisees(cible);
        if (licences < Math.max(1, u)) return json({ erreur: `${e.nom} : ${u} licences sont utilisées, retirez quelqu'un avant de baisser le nombre de licences.` });
        const { error } = await service.from("entreprises").update({ licences_max: licences }).eq("id", cible);
        if (error) throw error;
        e.licences_max = licences;
      }
      const total = (ents ?? []).reduce((a, x) => a + x.licences_max, 0);
      const l = ligne("licence");
      if (!l) throw new Error("Ligne « licence » absente de l'abonnement.");
      if (l.quantity !== total) await stripe.subscriptionItems.update(l.id, { quantity: total, ...prorata });
      const { error: e3 } = await service.from("groupes").update({ licences_max: total }).eq("id", g);
      if (e3) throw e3;

      // Entreprises : la ligne « supplémentaire » suit le nombre d'entreprises du groupe.
      const suivantes = n - 1, s = ligne("entreprise_suivante");
      if (s && suivantes > 0 && s.quantity !== suivantes) await stripe.subscriptionItems.update(s.id, { quantity: suivantes, ...prorata });
      else if (s && suivantes <= 0) await stripe.subscriptionItems.del(s.id, prorata);
      else if (!s && suivantes > 0) {
        await stripe.subscriptionItems.create({ subscription: sub.id, price: await prix("entreprise_suivante", f), quantity: suivantes, ...prorata });
      }
      return json({ ok: true });
    }

    // Résilier depuis CIPL (retour du 22/10) : effectif à l'échéance (décision du 11/10), et
    // réversible jusque-là (« Reprendre l'abonnement »).
    if (action === "resilier" || action === "reprendre") {
      if (!groupe.stripe_abonnement) return json({ erreur: "Aucun abonnement en cours." });
      const sub = await stripe.subscriptions.update(groupe.stripe_abonnement, { cancel_at_period_end: action === "resilier" });
      const { error } = await service.from("groupes").update({ resilie: sub.cancel_at_period_end }).eq("id", g);
      if (error) throw error;
      return json({ ok: true });
    }

    return json({ erreur: "Action inconnue." }, 400);
  } catch (e) {
    console.error(e);
    return json({ erreur: (e as Error)?.message ?? String(e) }, 500);
  }
});
