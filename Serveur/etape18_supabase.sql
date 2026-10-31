-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 18a : chaque SCI reliée à sa plateforme agréée (Super PDP), une par entreprise.
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape16b).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §18.
-- ════════════════════════════════════════════════════════════════════════════

-- L'autorisation donnée par le gérant dans la fenêtre de Super PDP. Elle ne quitte jamais le serveur :
-- Super PDP change le jeton de renouvellement à chaque usage, un seul endroit doit donc le renouveler.
create table if not exists public.plateformes (
  entreprise_id uuid primary key references public.entreprises(id) on delete cascade,
  fournisseur   text not null default 'superpdp' check (fournisseur in ('superpdp')),
  societe       text not null default '',   -- l'identifiant de la SCI chez la plateforme
  statut        text not null default '',   -- vérification du gérant : verified, needs_review, failed, ou rien encore
  refresh_token text not null,
  access_token  text not null default '',
  expire_le     timestamptz not null default now(),
  maj_le        timestamptz not null default now()
);

-- Une demande d'activation en cours : le « state » de la fenêtre d'autorisation, à usage unique,
-- qui dit à quelle entreprise rattacher le retour de Super PDP.
create table if not exists public.plateformes_demandes (
  etat          text primary key,
  entreprise_id uuid not null references public.entreprises(id) on delete cascade,
  cree_le       timestamptz not null default now()
);

-- 30/10 : le code de vérification PKCE de la demande (OAuth 2.1, que suit Super PDP, l'impose).
alter table public.plateformes_demandes add column if not exists verifier text not null default '';

-- Aucune règle d'accès : seules les fonctions (service_role) y lisent et y écrivent.
alter table public.plateformes enable row level security;
alter table public.plateformes_demandes enable row level security;
revoke all on public.plateformes from anon, authenticated;
revoke all on public.plateformes_demandes from anon, authenticated;
