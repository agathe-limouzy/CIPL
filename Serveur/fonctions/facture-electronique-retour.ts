// ════════════════════════════════════════════════════════════════════════════
// CIPL — étape 18a : fonction serveur « facture-electronique-retour » (Supabase › Edge Functions).
// « Verify JWT » DÉSACTIVÉ : c'est la page où la fenêtre de Super PDP renvoie le navigateur du
// gérant, sans jeton de CIPL. Ce qui la protège : le « state », créé par « facture-electronique »
// pour une entreprise, à usage unique, valable une heure, et le secret de l'application, que seul
// le serveur connaît (le code reçu ne vaut rien sans lui).
// Adresse à déclarer dans l'application OAuth de Super PDP :
//   https://<projet>.supabase.co/functions/v1/facture-electronique-retour
// Secrets : SUPERPDP_CLIENT_ID, SUPERPDP_CLIENT_SECRET.
// ════════════════════════════════════════════════════════════════════════════
import { createClient } from "npm:@supabase/supabase-js@2";

const URL_PROJET = Deno.env.get("SUPABASE_URL")!;
const service = createClient(URL_PROJET, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!);
const ID = Deno.env.get("SUPERPDP_CLIENT_ID") ?? "";
const SECRET = Deno.env.get("SUPERPDP_CLIENT_SECRET") ?? "";
const API = "https://api.superpdp.tech";
const RETOUR = `${URL_PROJET}/functions/v1/facture-electronique-retour`;

// Texte simple : Supabase n'affiche pas de page HTML depuis ses fonctions.
const page = (texte: string, status = 200) =>
  new Response(texte, { status, headers: { "Content-Type": "text/plain; charset=utf-8" } });

Deno.serve(async (req) => {
  try {
    const q = new URL(req.url).searchParams;
    const etat = q.get("state") ?? "", code = q.get("code") ?? "";
    if (q.get("error"))
      return page(`Activation annulée dans Super PDP (${q.get("error")}${q.get("error_description") ? " : " + q.get("error_description") : ""}). `
        + "Rien n'est relié : revenez dans CIPL pour recommencer.");

    const { data: demande } = await service.from("plateformes_demandes").select("*").eq("etat", etat).maybeSingle();
    if (!demande || !code) return page("Lien d'activation inconnu ou déjà utilisé : recommencez « Activer » depuis CIPL.", 400);
    await service.from("plateformes_demandes").delete().eq("etat", etat);
    if (Date.now() - new Date(demande.cree_le).getTime() > 3600_000)
      return page("Lien d'activation expiré (plus d'une heure) : recommencez « Activer » depuis CIPL.", 400);

    const r = await fetch(`${API}/oauth2/token`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({ grant_type: "authorization_code", code, redirect_uri: RETOUR, client_id: ID, client_secret: SECRET,
                                  code_verifier: demande.verifier ?? "" }),
    });
    const d = await r.json().catch(() => ({}));
    if (!r.ok || !d.refresh_token)
      return page(`Super PDP n'a pas donné l'autorisation : ${d.error_description ?? d.error ?? `HTTP ${r.status}`}. Recommencez « Activer » depuis CIPL.`, 400);

    const s = await fetch(`${API}/v1.beta/oauth2_sessions/me`, { headers: { Authorization: `Bearer ${d.access_token}` } })
      .then((x) => x.json()).catch(() => ({}));
    const { error } = await service.from("plateformes").upsert({
      entreprise_id: demande.entreprise_id, fournisseur: "superpdp",
      societe: String(s.company_id ?? s.company?.id ?? ""), statut: String(s.company_verification_status ?? ""),
      refresh_token: d.refresh_token, access_token: d.access_token,
      expire_le: new Date(Date.now() + (d.expires_in ?? 3600) * 1000).toISOString(), maj_le: new Date().toISOString(),
    });
    if (error) throw error;
    return page("C'est fait : la SCI est reliée à Super PDP. Vous pouvez fermer cet onglet et revenir dans CIPL.");
  } catch (e) {
    return page("Erreur du serveur de CIPL : " + String((e as Error)?.message ?? e), 500);
  }
});
