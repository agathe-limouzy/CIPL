// ════════════════════════════════════════════════════════════════════════════
// CIPL — étape 16b : fonction serveur « cartes » (Supabase › Edge Functions).
// Appelée par CIPL avec le jeton de la personne connectée : « Verify JWT » reste ACTIVÉ.
// Le plan d'une adresse (fiche du bâtiment, vignettes de l'accueil ; satellite jusqu'au 13/11/2026), demandé à Google
// (Maps Static API) avec la clé de CIPL, qui ne quitte pas le serveur. Rien n'est gardé ici : les
// conditions de Google ne permettent pas de conserver les images ; CIPL les garde le temps de la
// session. Le plafond se règle dans la console Google Cloud (quota par jour, décision du 28/10) ; revue 17
// (D1-10) : et, par entreprise, QUOTA images par jour (table cartes_journal), réservées avant l'appel comme
// pour Pappers (D1-05) : un seul client qui boucle n'épuise plus les cartes de tous jusqu'au lendemain.
// Secret (Supabase › Edge Functions › Secrets) : GOOGLE_MAPS_KEY.
// ════════════════════════════════════════════════════════════════════════════
import { createClient } from "npm:@supabase/supabase-js@2";

const URL_PROJET = Deno.env.get("SUPABASE_URL")!;
const service = createClient(URL_PROJET, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!);
const CLE = Deno.env.get("GOOGLE_MAPS_KEY") ?? "";
const QUOTA = 500;   // images par jour (UTC) et par entreprise : quelques dizaines de bâtiments, plusieurs ouvertures par jour

const json = (corps: unknown, status = 200) =>
  new Response(JSON.stringify(corps), { status, headers: { "Content-Type": "application/json" } });

/** Un nombre dans ses bornes, sinon NaN (rien d'autre ne part chez Google). */
const nombre = (v: unknown, min: number, max: number) => {
  const n = Number(v);
  return Number.isFinite(n) && n >= min && n <= max ? n : NaN;
};

Deno.serve(async (req) => {
  try {
    const d = await req.json();
    const utilisateur = createClient(URL_PROJET, Deno.env.get("SUPABASE_ANON_KEY")!,
      { global: { headers: { Authorization: req.headers.get("Authorization") ?? "" } } });
    const { data: acces } = await utilisateur.rpc("mes_acces");
    const ids = (acces ?? []).map((a: unknown) => typeof a === "string" ? a : (a as Record<string, string>)?.mes_acces);
    if (!ids.includes(d.entreprise)) return json({ ok: false, refus: "acces" }, 403);
    // Jamais 502 à 504 : pour CIPL, ces codes veulent dire « serveur en panne », et il passerait hors ligne.
    if (!CLE) return json({ ok: false, refus: "config" });

    const lat = nombre(d.lat, -85, 85), lon = nombre(d.lon, -180, 180), zoom = nombre(d.zoom, 1, 21);
    const largeur = nombre(d.largeur, 1, 640), hauteur = nombre(d.hauteur, 1, 640);
    if ([lat, lon, zoom, largeur, hauteur].some(Number.isNaN)) return json({ ok: false, refus: "parametres" }, 400);

    const p = new URLSearchParams({
      center: `${lat},${lon}`, zoom: String(Math.round(zoom)), size: `${Math.round(largeur)}x${Math.round(hauteur)}`,
      // 13/11/2026 : plan routier. Google refuse désormais les types « satellite » et « hybrid » de Maps Static aux comptes de
      // l'Espace économique européen (403 « satellite and hybrid map types are not available for your account and region »).
      maptype: "roadmap", language: "fr", region: "FR", key: CLE,
    });
    const plat = nombre(d.epingle_lat, -85, 85), plon = nombre(d.epingle_lon, -180, 180);
    if (!Number.isNaN(plat) && !Number.isNaN(plon)) p.append("markers", `color:red|${plat},${plon}`);

    // Revue 17 (D1-10) : l'image est réservée AVANT l'appel, en une instruction SQL (prendre_lecture_cartes, rôle de service).
    const { data: prise, error: ePrise } = await service.rpc("prendre_lecture_cartes", { p_entreprise: d.entreprise, p_max: QUOTA });
    if (ePrise) throw ePrise;
    if (!prise) return json({ ok: false, refus: "quota", erreur: `limite du jour atteinte pour cette entreprise (${QUOTA} images) : réessayez demain` });

    const r = await fetch("https://maps.googleapis.com/maps/api/staticmap?" + p);
    if (!r.ok) return json({ ok: false, refus: "google", code: r.status, erreur: (await r.text()).slice(0, 300) }, 424);
    return new Response(await r.arrayBuffer(), { headers: { "Content-Type": r.headers.get("Content-Type") ?? "image/png" } });
  } catch (e) {
    // Revue 16c (D1-02) : le message d'une erreur réseau cite l'adresse appelée, qui contient la clé Google ;
    // il reste dans le journal du serveur, jamais dans la réponse.
    console.error("[cartes]", e);
    return json({ ok: false, refus: "erreur", erreur: "erreur interne du serveur" }, 500);
  }
});
