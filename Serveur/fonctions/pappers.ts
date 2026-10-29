// ════════════════════════════════════════════════════════════════════════════
// CIPL — étape 16b : fonction serveur « pappers » (Supabase › Edge Functions).
// Appelée par CIPL avec le jeton de la personne connectée : « Verify JWT » reste ACTIVÉ.
// La fiche d'une société locataire (capital, dirigeants, finances, fermeture), lue chez Pappers au
// plus une fois par mois et par société pour tout CIPL (table pappers_fiches) : un jeton Pappers par
// lecture, payé par CIPL. Les procédures collectives ne viennent pas d'ici : CIPL les lit chaque jour
// au BODACC, gratuitement (décisions du 28/10, CONCEPTION §16b).
// La réponse de Pappers est traduite ICI dans le format de CIPL (SocieteLocataire.FicheSociete) :
// si Pappers nomme un champ autrement, on corrige ce fichier sans refaire CIPL.
// Secret (Supabase › Edge Functions › Secrets) : PAPPERS_API_KEY.
// ════════════════════════════════════════════════════════════════════════════
import { createClient } from "npm:@supabase/supabase-js@2";

const URL_PROJET = Deno.env.get("SUPABASE_URL")!;
const service = createClient(URL_PROJET, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!);
const CLE = Deno.env.get("PAPPERS_API_KEY") ?? "";
const UN_MOIS = 30 * 24 * 3600 * 1000;

const json = (corps: unknown, status = 200) =>
  new Response(JSON.stringify(corps), { status, headers: { "Content-Type": "application/json" } });

// Montants et dates en texte : CIPL lit le JSON avec JsonUtility, qui ne connaît pas null.
const texte = (v: unknown) => v === null || v === undefined ? "" : String(v);

// deno-lint-ignore no-explicit-any
type Brut = Record<string, any>;

/** La fiche de Pappers dans le format de CIPL. */
function fiche(p: Brut) {
  const dirigeants = (p.representants ?? p.dirigeants ?? [])
    .filter((r: Brut) => r && r.actuel !== false)
    .map((r: Brut) => ({
      nom: texte(r.nom_complet ?? r.denomination ?? [r.prenom, r.nom].filter(Boolean).join(" ")),
      qualite: texte(r.qualite),
    }));
  const finances = (p.finances ?? [])
    .map((f: Brut) => ({ annee: Number(f.annee ?? 0), ca: texte(f.chiffre_affaires), resultat: texte(f.resultat) }))
    .filter((f: Brut) => f.annee > 0)
    .sort((a: Brut, b: Brut) => b.annee - a.annee)
    .slice(0, 3);
  return {
    siren: texte(p.siren),
    nom: texte(p.nom_entreprise ?? p.denomination),
    forme: texte(p.forme_juridique),
    capital: texte(p.capital),
    creee_le: texte(p.date_creation),
    fermee: p.entreprise_cessee === true || texte(p.statut_rcs).toLowerCase().startsWith("radi"),
    fermee_le: texte(p.date_cessation ?? p.date_radiation_rcs),
    dirigeants,
    finances,
  };
}

Deno.serve(async (req) => {
  try {
    const { entreprise, siren } = await req.json();
    const utilisateur = createClient(URL_PROJET, Deno.env.get("SUPABASE_ANON_KEY")!,
      { global: { headers: { Authorization: req.headers.get("Authorization") ?? "" } } });

    // Une entreprise à laquelle la personne a accès, et un abonnement valable : les jetons sont payés par CIPL.
    const { data: acces } = await utilisateur.rpc("mes_acces");
    const ids = (acces ?? []).map((a: unknown) => typeof a === "string" ? a : (a as Record<string, string>)?.mes_acces);
    if (!ids.includes(entreprise)) return json({ ok: false, refus: "acces" }, 403);
    const { data: e } = await service.from("entreprises").select("groupe_id").eq("id", entreprise).maybeSingle();
    const { data: permis } = await service.rpc("ecriture_permise", { p_groupe: e?.groupe_id });
    if (!permis) return json({ ok: false, refus: "abonnement" }, 403);

    const s = texte(siren).replace(/\D/g, "").slice(0, 9);
    if (s.length !== 9) return json({ ok: false, refus: "siren" }, 400);

    const { data: garde } = await service.from("pappers_fiches").select("fiche, lu_le").eq("siren", s).maybeSingle();
    if (garde && Date.now() - new Date(garde.lu_le).getTime() < UN_MOIS)
      return json({ ok: true, fiche: garde.fiche, lu_le: garde.lu_le });

    if (!CLE) return json({ ok: false, refus: "config" });
    const r = await fetch(`https://api.pappers.fr/v2/entreprise?api_token=${encodeURIComponent(CLE)}&siren=${s}`);
    if (r.status === 404) return json({ ok: false, refus: "introuvable" });
    if (!r.ok) {
      // Pappers ne répond pas (ou plus de jetons) : la fiche d'avant vaut mieux que rien.
      if (garde) return json({ ok: true, fiche: garde.fiche, lu_le: garde.lu_le });
      // Constaté le 29/10 : sans crédit, Pappers répond 401 « Vous n'avez plus assez de crédits… ».
      const corps = (await r.text()).slice(0, 200);
      const refus = /cr[ée]dit/i.test(corps) || r.status === 402 || r.status === 429 ? "jetons"
        : r.status === 401 || r.status === 403 ? "cle" : "pappers";
      return json({ ok: false, refus, erreur: `HTTP ${r.status} ${corps}` });
    }

    const f = fiche(await r.json());
    const lu_le = new Date().toISOString();
    const { error } = await service.from("pappers_fiches").upsert({ siren: s, fiche: f, lu_le });
    if (error) throw error;
    return json({ ok: true, fiche: f, lu_le });
  } catch (e) {
    return json({ ok: false, refus: "erreur", erreur: String((e as Error)?.message ?? e) }, 500);
  }
});
