// ════════════════════════════════════════════════════════════════════════════
// CIPL — étape 14 : fonction serveur « emails-abonnement » (Supabase › Edge Functions).
// Les e-mails de l'abonnement, aux admins du groupe, depuis le Gmail de CIPL (décisions et
// maquettes du 25/10) :
//   chaque matin (tâche planifiée d'etape14_supabase.sql, corps {}) : fin d'essai dans N jours
//   (J-7 à J-2), demain (J-1), essai terminé ;
//   à la demande de stripe-webhook ({groupe, sorte, date}) : résiliation enregistrée, abonnement terminé ;
//   chaque matin aussi (Revue 17, D1-01) : les quantités Stripe de chaque groupe abonné suivent ses entreprises et licences.
// Jamais deux fois le même : table envois_abonnement (groupe, sorte, date concernée).
// « Verify JWT » DÉSACTIVÉ : la tâche planifiée présente la clé du coffre (en-tête x-cipl-cron),
// stripe-webhook la clé service_role ; tout autre appel est refusé.
// Secrets (Supabase › Edge Functions › Secrets) : CIPL_GMAIL (l'adresse Gmail de CIPL) et
// CIPL_GMAIL_MOT_DE_PASSE (son mot de passe d'application, le même que pour les codes de Supabase), et
// STRIPE_SECRET_KEY (la même que pour abonnement-stripe ; absente, rien n'est aligné et les e-mails partent quand même).
// ════════════════════════════════════════════════════════════════════════════
import { createClient } from "npm:@supabase/supabase-js@2";
import { SMTPClient } from "https://deno.land/x/denomailer@1.6.0/mod.ts";
import Stripe from "npm:stripe@17";

const CLE_SERVICE = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const service = createClient(Deno.env.get("SUPABASE_URL")!, CLE_SERVICE);
const GMAIL = Deno.env.get("CIPL_GMAIL") ?? "";

// Revue 17 (D1-01) : la facturation d'une entreprise ajoutée ne dépend plus seulement du second appel fait par le PC
// (« ajuster ») : chaque matin, les lignes Stripe de chaque groupe abonné suivent ses entreprises et leurs licences, comme à
// la fin d'« ajuster » (abonnement-stripe.ts) : « licence » = somme des licences, « entreprise supplémentaire » = entreprises
// − 1, au prorata, seulement si une quantité diffère. Un écart dure au plus un jour, quel que soit le chemin de l'ajout
// (écran, import, appel direct).
const CLE_STRIPE = Deno.env.get("STRIPE_SECRET_KEY");
const stripe = CLE_STRIPE ? new Stripe(CLE_STRIPE, { httpClient: Stripe.createFetchHttpClient() }) : null;

async function alignerAbonnements(): Promise<Record<string, string>> {
  if (!stripe) return { stripe: "STRIPE_SECRET_KEY absente : rien aligné" };
  const bilan: Record<string, string> = {};
  const { data: groupes, error } = await service.from("groupes").select("id, stripe_abonnement").not("stripe_abonnement", "is", null);
  if (error) throw error;
  for (const g of groupes ?? []) {
    try {
      const { data: ents, error: e2 } = await service.from("entreprises").select("licences_max").eq("groupe_id", g.id);
      if (e2) throw e2;
      const n = Math.max(1, ents?.length ?? 1);
      const total = (ents ?? []).reduce((a, x) => a + (x.licences_max ?? 0), 0);
      const sub = await stripe.subscriptions.retrieve(g.stripe_abonnement);
      if (sub.status === "canceled" || sub.status === "incomplete_expired") { bilan[g.id] = "abonnement terminé"; continue; }
      const f = sub.items.data[0]?.price.recurring?.interval === "year" ? "annuel" : "mensuel";
      const ligne = (nom: string) => sub.items.data.find((i) => i.price.lookup_key === `cipl_${nom}_${f}`);
      const prorata = { proration_behavior: "create_prorations" as const };
      const faits: string[] = [];
      const l = ligne("licence");
      if (l && total > 0 && l.quantity !== total) {
        await stripe.subscriptionItems.update(l.id, { quantity: total, ...prorata });
        faits.push(`licences ${l.quantity} → ${total}`);
      }
      const suivantes = n - 1, s = ligne("entreprise_suivante");
      if (s && suivantes > 0 && s.quantity !== suivantes) {
        await stripe.subscriptionItems.update(s.id, { quantity: suivantes, ...prorata });
        faits.push(`entreprises supplémentaires ${s.quantity} → ${suivantes}`);
      } else if (s && suivantes <= 0) {
        await stripe.subscriptionItems.del(s.id, prorata);
        faits.push("entreprise supplémentaire retirée");
      } else if (!s && suivantes > 0) {
        // Le prix existe dès qu'un groupe en a eu besoin (créé par abonnement-stripe) ; sinon, l'écart attend « ajuster ».
        const p = (await stripe.prices.list({ lookup_keys: [`cipl_entreprise_suivante_${f}`], active: true, limit: 1 })).data[0];
        if (p) {
          await stripe.subscriptionItems.create({ subscription: sub.id, price: p.id, quantity: suivantes, ...prorata });
          faits.push(`entreprises supplémentaires 0 → ${suivantes}`);
        } else faits.push("prix « entreprise supplémentaire » absent : rien fait");
      }
      bilan[g.id] = faits.length ? faits.join(", ") : "à jour";
    } catch (e) {
      console.error("alignement", g.id, e);
      bilan[g.id] = "échec : " + ((e as Error)?.message ?? String(e));
    }
  }
  return bilan;
}

const json = (corps: unknown, status = 200) =>
  new Response(JSON.stringify(corps), { status, headers: { "Content-Type": "application/json" } });

const DATE = new Intl.DateTimeFormat("fr-FR", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });
const minuit = (iso: string) => Date.parse(`${iso}T00:00:00Z`);
const jour = (iso: string, decalage = 0) => DATE.format(new Date(minuit(iso) + decalage * 86400000));
const joursRestants = (iso: string) => Math.round((minuit(iso) - minuit(new Date().toISOString().slice(0, 10))) / 86400000);

/** « SCI (JBC) », « SCI (JBC) et SCI Rivoli », « A, B et C ». */
const liste = (noms: string[]) =>
  noms.length <= 1 ? (noms[0] ?? "votre groupe") : `${noms.slice(0, -1).join(", ")} et ${noms[noms.length - 1]}`;

const echapper = (s: string) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

/** Le texte en base64 (lignes de 76). Retour du 26/10 (« L'c3©quipe ») : le quoted-printable de
 *  denomailer 1.6.0 perd le « = » d'un accent à la dernière coupure (encoding.ts, `slice(lines * 74)`
 *  sans `offset`) ; on ne lui passe donc que des parties déjà encodées. */
const base64 = (s: string) => btoa(String.fromCharCode(...new TextEncoder().encode(s))).replace(/.{76}/g, "$&\r\n");

/** L'objet et les paragraphes de chaque e-mail (maquettes validées le 25/10). L'essai est
 *  modifiable jusqu'à sa date de fin incluse ; la lecture seule commence le lendemain. */
function message(sorte: string, ents: string, date: string): { objet: string; paragraphes: string[] } | null {
  switch (sorte) {
    case "essai_j7": return {
      objet: `Votre essai CIPL se termine dans ${joursRestants(date)} jours`,
      paragraphes: [
        `L'essai gratuit de CIPL pour ${ents} se termine le ${jour(date)}. Pour continuer à saisir et envoyer vos factures sans interruption, choisissez votre abonnement dans CIPL : Réglage › Abonnement (mensuel ou annuel, une licence par personne et par entreprise).`,
        `Sans abonnement, CIPL passera en lecture seule le ${jour(date, 1)} : vos données resteront consultables et exportables.`,
      ] };
    case "essai_j1": return {
      objet: "Votre essai CIPL se termine demain",
      paragraphes: [`L'essai gratuit de CIPL pour ${ents} se termine demain, le ${jour(date)}. Choisissez votre abonnement dans Réglage › Abonnement pour ne pas passer en lecture seule.`] };
    case "essai_fin": return {
      objet: "Votre essai CIPL est terminé",
      paragraphes: [`L'essai gratuit de CIPL pour ${ents} est terminé : CIPL est maintenant en lecture seule pour tout le groupe. Rien n'est perdu : choisissez votre abonnement dans Réglage › Abonnement, et la saisie et l'envoi des factures reprennent aussitôt.`] };
    case "resiliation": return {
      objet: "Votre résiliation CIPL est enregistrée",
      paragraphes: [
        `La résiliation de votre abonnement CIPL est enregistrée. Il reste actif jusqu'au ${jour(date)}, puis CIPL passera en lecture seule.`,
        `Vous avez changé d'avis ? « Reprendre l'abonnement » dans Réglage › Abonnement, jusqu'à cette date. Pensez à exporter vos données (Réglage › Documents).`,
      ] };
    case "abonnement_fin": return {
      objet: "Votre abonnement CIPL est terminé",
      paragraphes: [`Votre abonnement CIPL a pris fin le ${jour(date)} : CIPL est en lecture seule pour tout le groupe. Vos données restent consultables et exportables ; pour reprendre, choisissez un abonnement dans Réglage › Abonnement.`] };
    default: return null;
  }
}

/** Un e-mail à chaque admin du groupe : noté avant l'envoi, retiré s'il échoue (retenté au
 *  prochain passage). ponytail: si le 2e admin échoue, le 1er le recevra une seconde fois ;
 *  noter par destinataire si les groupes à plusieurs admins se plaignent de doublons. */
async function envoyer(smtp: SMTPClient, groupe: string, sorte: string, date: string): Promise<string> {
  if (!message(sorte, "", date)) throw new Error("Sorte d'e-mail inconnue : " + sorte);
  const { data: nouveau, error } = await service.rpc("noter_envoi_abonnement", { p_groupe: groupe, p_sorte: sorte, p_cle: date });
  if (error) throw error;
  if (!nouveau) return "déjà envoyé";
  try {
    const { data: d, error: e2 } = await service.rpc("destinataires_abonnement", { p_groupe: groupe });
    if (e2) throw e2;
    const m = message(sorte, liste(d.entreprises ?? []), date)!;
    for (const a of d.admins ?? []) {
      const lignes = [`Bonjour ${a.nom},`, ...m.paragraphes, "L'équipe CIPL"];
      await smtp.send({
        from: `CIPL <${GMAIL}>`, to: a.email, subject: m.objet,
        mimeContent: [
          { mimeType: 'text/plain; charset="utf-8"', transferEncoding: "base64", content: base64(lignes.join("\r\n\r\n")) },
          { mimeType: 'text/html; charset="utf-8"', transferEncoding: "base64", content: base64(lignes.map((l) => `<p>${echapper(l)}</p>`).join("\r\n")) },
        ],
      });
    }
    return `envoyé à ${(d.admins ?? []).length} admin(s)`;
  } catch (e) {
    await service.rpc("oublier_envoi_abonnement", { p_groupe: groupe, p_sorte: sorte, p_cle: date });
    throw e;
  }
}

Deno.serve(async (req) => {
  let autorise = req.headers.get("Authorization") === `Bearer ${CLE_SERVICE}`;
  const cron = req.headers.get("x-cipl-cron");
  if (!autorise && cron) autorise = (await service.rpc("cle_cron_valide", { p_cle: cron })).data === true;
  if (!autorise) return json({ erreur: "Refusé." }, 401);

  const corps = await req.json().catch(() => ({}));
  // Revue 17 (D1-01) : la tâche du matin (corps vide) aligne aussi les abonnements Stripe, avant les e-mails.
  let alignement: Record<string, string> | string = {};
  if (!corps.groupe) {
    try { alignement = await alignerAbonnements(); }
    catch (e) { console.error("alignement", e); alignement = "échec : " + ((e as Error)?.message ?? String(e)); }
  }
  if (!corps.groupe) console.log("alignement des abonnements", JSON.stringify(alignement));   // visible dans Logs, chaque matin
  let aEnvoyer: { groupe: string; sorte: string; date: string }[];
  if (corps.groupe) aEnvoyer = [{ groupe: corps.groupe, sorte: corps.sorte, date: corps.date }];
  else {
    const { data, error } = await service.rpc("emails_abonnement_du_jour", {});
    if (error) return json({ erreur: error.message }, 500);
    aEnvoyer = data ?? [];
  }
  if (aEnvoyer.length === 0) return json({ envoyes: 0, alignement });

  // Port 465 (TLS direct) : le 587 est réputé fermé depuis les fonctions Supabase.
  const smtp = new SMTPClient({
    connection: { hostname: "smtp.gmail.com", port: 465, tls: true,
                  auth: { username: GMAIL, password: Deno.env.get("CIPL_GMAIL_MOT_DE_PASSE") ?? "" } },
  });
  const bilan: Record<string, string> = {};
  try {
    for (const x of aEnvoyer) {
      const quoi = `${x.groupe} · ${x.sorte} · ${x.date}`;
      try { bilan[quoi] = await envoyer(smtp, x.groupe, x.sorte, x.date); }
      catch (e) { console.error(quoi, e); bilan[quoi] = "échec : " + ((e as Error)?.message ?? String(e)); }
    }
  } finally {
    await smtp.close();
  }
  return json({ ...bilan, alignement });
});
