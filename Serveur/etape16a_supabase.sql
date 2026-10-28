-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 16a : l'adresse d'envoi reliée à Google ou à Microsoft, une par entreprise.
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape16).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §16a.
-- ════════════════════════════════════════════════════════════════════════════

-- L'autorisation donnée par l'admin dans la fenêtre de Google ou de Microsoft (jeton de
-- renouvellement). Elle ne quitte jamais le serveur : la fonction « messagerie » s'en sert pour
-- donner à CIPL un jeton d'une heure, qui ne permet que d'envoyer.
create table if not exists public.messageries (
  entreprise_id uuid primary key references public.entreprises(id) on delete cascade,
  fournisseur   text not null check (fournisseur in ('google', 'microsoft')),
  adresse       text not null,
  refresh_token text not null,
  maj_le        timestamptz not null default now()
);

-- Aucune règle d'accès : ni CIPL ni personne n'y lit directement, seule la fonction (service_role).
alter table public.messageries enable row level security;
revoke all on public.messageries from anon, authenticated;
