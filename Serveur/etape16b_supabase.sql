-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 16b : les fiches Pappers des sociétés locataires, gardées sur le serveur.
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape16a).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §16b.
-- ════════════════════════════════════════════════════════════════════════════

-- Une ligne par société (SIREN), pour tout CIPL : la fonction « pappers » ne relit Pappers (un jeton
-- payant) que si la fiche a plus d'un mois (décision du 28/10). Données publiques, au format de CIPL
-- (SocieteLocataire.FicheSociete).
create table if not exists public.pappers_fiches (
  siren text primary key check (siren ~ '^[0-9]{9}$'),
  fiche jsonb not null,
  lu_le timestamptz not null default now()
);

-- Aucune règle d'accès : seule la fonction (service_role) y lit et y écrit.
alter table public.pappers_fiches enable row level security;
revoke all on public.pappers_fiches from anon, authenticated;
