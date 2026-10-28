// ════════════════════════════════════════════════════════════════════════════
// CIPL — étape 16a : fonction serveur « messagerie » (Supabase › Edge Functions).
// Appelée par CIPL avec le jeton de la personne connectée : « Verify JWT » reste ACTIVÉ.
// Les factures, relances et invitations partent de l'adresse du client (Gmail, Google Workspace,
// Outlook, Hotmail, Microsoft 365) sans mot de passe d'application : l'admin relie l'adresse une
// fois, dans la fenêtre de Google ou de Microsoft. L'autorisation (jeton de renouvellement) reste
// ici, dans la table messageries ; CIPL reçoit pour chaque envoi un jeton d'une heure, qui ne
// permet que d'envoyer des e-mails.
// Actions :
//   etat   → l'adresse reliée de l'entreprise, et les identifiants publics des applications ;
//   relier → (admin) échange le code reçu par CIPL contre l'autorisation, et la range ;
//   jeton  → (tous les membres) un jeton d'une heure ;
//   delier → (admin) retire l'autorisation (et la révoque chez Google).
// Secrets (Supabase › Edge Functions › Secrets) : GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET,
// MICROSOFT_CLIENT_ID. Droits demandés : les mêmes que dans CIPL (Messagerie.cs).
// ════════════════════════════════════════════════════════════════════════════
import { createClient } from "npm:@supabase/supabase-js@2";

const URL_PROJET = Deno.env.get("SUPABASE_URL")!;
const service = createClient(URL_PROJET, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!);
const GOOGLE_ID = Deno.env.get("GOOGLE_CLIENT_ID") ?? "";
const GOOGLE_SECRET = Deno.env.get("GOOGLE_CLIENT_SECRET") ?? "";
const MICROSOFT_ID = Deno.env.get("MICROSOFT_CLIENT_ID") ?? "";
const SCOPE_MICROSOFT = "offline_access openid email https://graph.microsoft.com/Mail.Send";
const ADRESSE_JETON: Record<string, string> = {
  google: "https://oauth2.googleapis.com/token",
  microsoft: "https://login.microsoftonline.com/common/oauth2/v2.0/token",
};

const json = (corps: unknown, status = 200) =>
  new Response(JSON.stringify(corps), { status, headers: { "Content-Type": "application/json" } });

/** L'adresse du compte, lue dans l'id_token reçu directement de Google ou de Microsoft. */
function adresse(idToken: string): string {
  let s = (idToken ?? "").split(".")[1] ?? "";
  s = s.replace(/-/g, "+").replace(/_/g, "/");
  s += "===".slice((s.length + 3) % 4);
  const p = JSON.parse(atob(s));
  return String(p.email ?? p.preferred_username ?? "").toLowerCase();
}

/** Une demande à Google ou à Microsoft : code → autorisation, ou autorisation → jeton. */
async function demander(fournisseur: string, champs: Record<string, string>) {
  const base: Record<string, string> = fournisseur === "google"
    ? { client_id: GOOGLE_ID, client_secret: GOOGLE_SECRET }
    : { client_id: MICROSOFT_ID, scope: SCOPE_MICROSOFT };
  const r = await fetch(ADRESSE_JETON[fournisseur], {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({ ...base, ...champs }),
  });
  const d = await r.json().catch(() => ({}));
  return { ok: r.ok, d };
}

Deno.serve(async (req) => {
  try {
    const { action, entreprise, fournisseur, code, verifier, redirect } = await req.json();
    const utilisateur = createClient(URL_PROJET, Deno.env.get("SUPABASE_ANON_KEY")!,
      { global: { headers: { Authorization: req.headers.get("Authorization") ?? "" } } });

    // Toute action : une entreprise à laquelle la personne a accès ; relier et delier : son admin.
    const { data: acces } = await utilisateur.rpc("mes_acces");
    const ids = (acces ?? []).map((a: unknown) => typeof a === "string" ? a : (a as Record<string, string>)?.mes_acces);
    if (!ids.includes(entreprise)) return json({ ok: false, refus: "acces" }, 403);
    if (action === "relier" || action === "delier") {
      const { data: admin } = await utilisateur.rpc("est_admin", { p_entreprise: entreprise });
      if (!admin) return json({ ok: false, refus: "admin" }, 403);
    }

    const { data: lien } = await service.from("messageries").select("*").eq("entreprise_id", entreprise).maybeSingle();

    if (action === "etat")
      return json({ ok: true, fournisseur: lien?.fournisseur ?? "", adresse: lien?.adresse ?? "", google: GOOGLE_ID, microsoft: MICROSOFT_ID });

    if (action === "relier") {
      if (fournisseur !== "google" && fournisseur !== "microsoft") return json({ ok: false, refus: "fournisseur" }, 400);
      if (!(fournisseur === "google" ? GOOGLE_ID && GOOGLE_SECRET : MICROSOFT_ID)) return json({ ok: false, refus: "config" });
      const { ok, d } = await demander(fournisseur,
        { grant_type: "authorization_code", code, code_verifier: verifier, redirect_uri: redirect });
      if (!ok || !d.refresh_token)
        return json({ ok: false, refus: "echange", erreur: d.error_description ?? d.error ?? "pas d'autorisation durable" });
      const a = adresse(d.id_token);
      const { error } = await service.from("messageries").upsert({
        entreprise_id: entreprise, fournisseur, adresse: a, refresh_token: d.refresh_token, maj_le: new Date().toISOString(),
      });
      if (error) throw error;
      return json({ ok: true, fournisseur, adresse: a });
    }

    if (action === "jeton") {
      if (!lien) return json({ ok: false, refus: "non_relie" });
      const { ok, d } = await demander(lien.fournisseur, { grant_type: "refresh_token", refresh_token: lien.refresh_token });
      if (!ok) return json({ ok: false, refus: d.error === "invalid_grant" ? "retire" : "jeton", erreur: d.error_description ?? d.error ?? "" });
      // Microsoft donne une nouvelle autorisation à chaque fois : on garde la dernière.
      if (d.refresh_token && d.refresh_token !== lien.refresh_token)
        await service.from("messageries").update({ refresh_token: d.refresh_token, maj_le: new Date().toISOString() })
          .eq("entreprise_id", entreprise);
      return json({ ok: true, fournisseur: lien.fournisseur, adresse: lien.adresse, access_token: d.access_token, expire_dans: d.expires_in ?? 3600 });
    }

    if (action === "delier") {
      if (lien?.fournisseur === "google")   // chez Google, l'autorisation est aussi retirée
        await fetch("https://oauth2.googleapis.com/revoke?token=" + encodeURIComponent(lien.refresh_token), { method: "POST" })
          .catch(() => {});
      await service.from("messageries").delete().eq("entreprise_id", entreprise);
      return json({ ok: true });
    }

    return json({ ok: false, refus: "action" }, 400);
  } catch (e) {
    return json({ ok: false, refus: "erreur", erreur: String((e as Error)?.message ?? e) }, 500);
  }
});
