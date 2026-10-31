// ════════════════════════════════════════════════════════════════════════════
// CIPL — étape 18a : fonction serveur « facture-electronique » (Supabase › Edge Functions).
// Appelée par CIPL avec le jeton de la personne connectée : « Verify JWT » reste ACTIVÉ.
// Chaque SCI soumise à TVA émet ses factures par une plateforme agréée : Super PDP (décisions des
// 26 et 29/10). Le gérant relie la SCI une fois, dans la fenêtre de Super PDP (vérification de son
// identité) ; l'autorisation reste ici (table plateformes), et c'est ici qu'elle est renouvelée :
// Super PDP change le jeton de renouvellement à chaque usage. CIPL paie Super PDP pour tous.
// Actions :
//   etat    → la SCI est-elle reliée, et où en est la vérification du gérant ;
//   activer → (admin) l'adresse de la fenêtre de Super PDP, pré-remplie (e-mail, SIREN) ;
//   delier  → (admin) oublie l'autorisation ;
//   envoyer → (18b) un loyer : notre PDF + la facture au modèle EN 16931 → Factur-X (UBL si le destinataire
//             ne l'accepte pas) → envoi ; rend son id ;
//   encaisser → (18b) le statut « Encaissée » (fr:212) d'une facture envoyée.
// Le retour de la fenêtre arrive dans la fonction « facture-electronique-retour ».
// Secrets : SUPERPDP_CLIENT_ID, SUPERPDP_CLIENT_SECRET ; SUPERPDP_BAC_A_SABLE = 1 pour les essais.
// ════════════════════════════════════════════════════════════════════════════
import { createClient } from "npm:@supabase/supabase-js@2";

const URL_PROJET = Deno.env.get("SUPABASE_URL")!;
const service = createClient(URL_PROJET, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!);
const ID = Deno.env.get("SUPERPDP_CLIENT_ID") ?? "";
const SECRET = Deno.env.get("SUPERPDP_CLIENT_SECRET") ?? "";
const BAC_A_SABLE = (Deno.env.get("SUPERPDP_BAC_A_SABLE") ?? "") === "1";
const API = "https://api.superpdp.tech";
const RETOUR = `${URL_PROJET}/functions/v1/facture-electronique-retour`;

const json = (corps: unknown, status = 200) =>
  new Response(JSON.stringify(corps), { status, headers: { "Content-Type": "application/json" } });

const b64url = (o: Uint8Array) => btoa(String.fromCharCode(...o)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

/** Un refus de Super PDP. « refusee » : la facture elle-même (4xx), à corriger, pas à renvoyer telle quelle ;
 *  « indisponible » : la plateforme (panne, gérant pas encore vérifié), CIPL réessaiera au prochain lancement.
 *  Toujours répondu en 200 à CIPL : 502 à 504 le feraient passer hors ligne. */
async function refus(r: Response | null) {
  if (!r) return json({ ok: false, refus: "indisponible", erreur: "Super PDP injoignable" });
  // Le détail de Super PDP, sans ses simples avertissements (« still status warning ») qui masquaient la vraie règle.
  let texte = await r.text().catch(() => "");
  try {
    const m = JSON.parse(texte)?.message;
    if (typeof m === "string") texte = m.split("\n").filter((l) => !l.includes("still status warning")).join("\n") || m;
  } catch { /* texte brut */ }
  texte = texte.slice(0, 1000);
  const motif = r.status === 401 ? "jeton" : r.status >= 500 || r.status === 429 || r.status === 403 ? "indisponible" : "refusee";
  return json({ ok: false, refus: motif, erreur: `HTTP ${r.status} ${texte}` });
}

// deno-lint-ignore no-explicit-any
type Ligne = Record<string, any>;

/** Un jeton d'accès valable : celui gardé, ou un nouveau (et le nouveau jeton de renouvellement est gardé). */
async function jeton(lien: Ligne): Promise<{ jeton?: string; refus?: string; erreur?: string }> {
  if (lien.access_token && new Date(lien.expire_le).getTime() - Date.now() > 60_000) return { jeton: lien.access_token };
  const r = await fetch(`${API}/oauth2/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({ grant_type: "refresh_token", refresh_token: lien.refresh_token, client_id: ID, client_secret: SECRET }),
  });
  const d = await r.json().catch(() => ({}));
  if (!r.ok || !d.access_token) return { refus: d.error === "invalid_grant" ? "retire" : "jeton", erreur: d.error_description ?? d.error ?? `HTTP ${r.status}` };
  // ponytail: deux appels au même instant renouvelleraient deux fois (le second serait refusé) ; un verrou si ça arrive.
  await service.from("plateformes").update({
    access_token: d.access_token, refresh_token: d.refresh_token ?? lien.refresh_token,
    expire_le: new Date(Date.now() + (d.expires_in ?? 3600) * 1000).toISOString(), maj_le: new Date().toISOString(),
  }).eq("entreprise_id", lien.entreprise_id);
  return { jeton: d.access_token };
}

Deno.serve(async (req) => {
  try {
    const { action, entreprise, cle, facture, pdf, id } = await req.json();
    const utilisateur = createClient(URL_PROJET, Deno.env.get("SUPABASE_ANON_KEY")!,
      { global: { headers: { Authorization: req.headers.get("Authorization") ?? "" } } });

    const { data: acces } = await utilisateur.rpc("mes_acces");
    const ids = (acces ?? []).map((a: unknown) => typeof a === "string" ? a : (a as Record<string, string>)?.mes_acces);
    if (!ids.includes(entreprise)) return json({ ok: false, refus: "acces" }, 403);
    if (action === "activer" || action === "delier") {
      const { data: admin } = await utilisateur.rpc("est_admin", { p_entreprise: entreprise });
      if (!admin) return json({ ok: false, refus: "admin" }, 403);
    }

    const { data: lien } = await service.from("plateformes").select("*").eq("entreprise_id", entreprise).maybeSingle();

    if (action === "etat") {
      if (!lien) return json({ ok: true, relie: false, configure: ID !== "" && SECRET !== "", bac_a_sable: BAC_A_SABLE });
      // Où en est la vérification du gérant (tant qu'elle n'est pas « verified », Super PDP refuse le reste).
      const j = await jeton(lien);
      if (!j.jeton) return json({ ok: false, refus: j.refus, erreur: j.erreur });
      const r = await fetch(`${API}/v1.beta/oauth2_sessions/me`, { headers: { Authorization: `Bearer ${j.jeton}` } });
      const s = await r.json().catch(() => ({}));
      const statut = String(s.company_verification_status ?? lien.statut ?? "");
      if (statut !== lien.statut)
        await service.from("plateformes").update({ statut, maj_le: new Date().toISOString() }).eq("entreprise_id", entreprise);
      return json({ ok: true, relie: true, statut, bac_a_sable: BAC_A_SABLE });
    }

    if (action === "activer") {
      if (!ID || !SECRET) return json({ ok: false, refus: "config" });
      const { data: e } = await service.from("entreprises").select("siret").eq("id", entreprise).maybeSingle();
      const siren = String(e?.siret ?? "").replace(/\D/g, "").slice(0, 9);
      if (siren.length !== 9) return json({ ok: false, refus: "siren" });
      const { data: u } = await utilisateur.auth.getUser();
      const etat = crypto.randomUUID();
      // PKCE (S256) : OAuth 2.1 l'impose ; le code de vérification reste sur le serveur jusqu'au retour.
      const verifier = b64url(crypto.getRandomValues(new Uint8Array(32)));
      const defi = b64url(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(verifier))));
      await service.from("plateformes_demandes").delete().lt("cree_le", new Date(Date.now() - 3600_000).toISOString());
      const { error } = await service.from("plateformes_demandes").insert({ etat, entreprise_id: entreprise, verifier });
      if (error) throw error;
      const p = new URLSearchParams({
        response_type: "code", client_id: ID, redirect_uri: RETOUR, state: etat,
        code_challenge: defi, code_challenge_method: "S256",
      });
      // Le SIREN pré-remplit la SCI. Pas en bac à sable : Super PDP n'y connaît que ses entreprises fictives
      // (constaté le 30/10 : « No company found with these superpdp_company_number_scheme and … number »).
      if (!BAC_A_SABLE) { p.set("superpdp_company_number", siren); p.set("superpdp_company_number_scheme", "fr_siren"); }
      if (u?.user?.email) p.set("login_hint", u.user.email);
      return json({ ok: true, url: `${API}/oauth2/authorize?${p}` });
    }

    if (action === "delier") {
      // L'autorisation est aussi retirée chez Super PDP (RFC 7009 : le jeton de renouvellement et ses jetons d'accès).
      if (lien)
        await fetch(`${API}/oauth2/revoke`, {
          method: "POST",
          headers: { "Content-Type": "application/x-www-form-urlencoded" },
          body: new URLSearchParams({ token: lien.refresh_token, token_type_hint: "refresh_token", client_id: ID, client_secret: SECRET }),
        }).catch(() => {});
      await service.from("plateformes").delete().eq("entreprise_id", entreprise);
      return json({ ok: true });
    }

    // Étape 18b : un loyer (notre PDF + la facture au modèle EN 16931 → Factur-X → envoi), puis « Encaissée ».
    if (action === "envoyer" || action === "encaisser") {
      if (!lien) return json({ ok: false, refus: "non_relie" });
      const j = await jeton(lien);
      if (!j.jeton) return json({ ok: false, refus: j.refus, erreur: j.erreur });
      const auth = { Authorization: `Bearer ${j.jeton}` };

      if (action === "encaisser") {
        // fr:212 sans détail : le montant est le total de la facture (documentation Super PDP).
        const r = await fetch(`${API}/v1.beta/invoice_events`, {
          method: "POST", headers: { ...auth, "Content-Type": "application/json" },
          body: JSON.stringify({ invoice_id: Number(id) || id, status_code: "fr:212" }),
        }).catch(() => null);
        return r?.ok ? json({ ok: true }) : await refus(r);
      }

      const fe = JSON.parse(String(facture ?? "{}"));
      if (BAC_A_SABLE) {
        // Bac à sable : vendeur et acheteur y sont des entreprises fictives. Super PDP refuse un vendeur autre que
        // l'entreprise de la session, et un destinataire absent de son annuaire (constaté le 30/10). Leurs
        // identifiants sont repris de la facture d'essai que Super PDP prépare pour la société (l'acheteur y est une
        // autre entreprise fictive). En production, la SCI est reliée sous son propre SIREN : rien n'est changé.
        const essai = await fetch(`${API}/v1.beta/invoices/generate_test_invoice?format=en16931`, { headers: auth })
          .then((r) => r.ok ? r.json() : null).catch(() => null);
        for (const partie of ["seller", "buyer"])
          for (const champ of ["legal_registration_identifier", "vat_identifier", "electronic_address"])
            if (essai?.[partie]?.[champ]) fe[partie][champ] = essai[partie][champ];
      }
      // Production : l'adresse de l'acheteur (BT-49) est celle que l'annuaire donne pour son SIREN, le SIREN
      // lui-même s'il y est. Absent de l'annuaire : le SIREN, et Super PDP refusera.
      const sirenAcheteur = String(fe.buyer?.legal_registration_identifier?.value ?? "");
      if (!BAC_A_SABLE && sirenAcheteur) {
        const a = await fetch(`${API}/v1.beta/french_directory/entries?number=${encodeURIComponent(sirenAcheteur)}`, { headers: auth })
          .then((r) => r.json()).catch(() => null);
        const actives: string[] = (a?.data ?? []).filter((x: Ligne) => x.is_active).map((x: Ligne) => String(x.identifier))
          .filter((x: string) => x.startsWith("0225:") && !x.includes("replyto"));
        const choix = actives.find((x) => x === `0225:${sirenAcheteur}`) ?? actives[0];
        if (choix) fe.buyer.electronic_address = { value: choix.slice(5), scheme: "0225" };
      }
      // La facture convertie (Factur-X : notre PDF avec la facture jointe ; UBL : la facture seule), puis envoyée.
      // Rend la réponse de la conversion si elle échoue, sinon celle de l'envoi.
      const envoi = async (format: "factur-x" | "ubl") => {
        let corps: BodyInit = JSON.stringify(fe);
        const entetes: Record<string, string> = { ...auth, "Content-Type": "application/json" };
        if (format === "factur-x") {
          const fd = new FormData();
          fd.append("invoice", new Blob([JSON.stringify(fe)], { type: "application/json" }), "facture.json");
          fd.append("pdf", new Blob([Uint8Array.from(atob(String(pdf ?? "")), (c) => c.charCodeAt(0))], { type: "application/pdf" }), "facture.pdf");
          corps = fd;
          delete entetes["Content-Type"];
        }
        const c = await fetch(`${API}/v1.beta/invoices/convert?from=en16931&to=${format}`, { method: "POST", headers: entetes, body: corps })
          .catch(() => null);
        if (!c?.ok) return c;
        // ponytail: un même external_id renvoyé après une réponse perdue sera sans doute refusé (doublon) ; à vérifier au bac à sable.
        return await fetch(`${API}/v1.beta/invoices?external_id=${encodeURIComponent(String(cle ?? "").slice(0, 36))}`, {
          method: "POST", headers: { ...auth, "Content-Type": format === "ubl" ? "application/xml" : "application/pdf" },
          body: new Uint8Array(await c.arrayBuffer()),
        }).catch(() => null);
      };
      // Factur-X d'abord. Si le destinataire ne l'accepte pas (pré-contrôle de Super PDP : « does not accept this
      // document », constaté en bac à sable le 30/10), la même facture part en UBL ; le PDF part toujours par e-mail.
      let s = await envoi("factur-x");
      if (s && !s.ok && (await s.clone().text()).includes("does not accept this document")) s = await envoi("ubl");
      if (!s?.ok) return await refus(s);
      const f = await s.json().catch(() => ({}));
      return json({ ok: true, id: String(f.id ?? "") });
    }

    return json({ ok: false, refus: "action" }, 400);
  } catch (e) {
    return json({ ok: false, refus: "erreur", erreur: String((e as Error)?.message ?? e) }, 500);
  }
});
