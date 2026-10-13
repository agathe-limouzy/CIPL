-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — essai technique (étape 1 de CONCEPTION_MULTIUTILISATEUR.md)
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run.
-- Crée deux tables d'essai (préfixe essai_) : rien à voir avec les vraies données.
-- Pour tout effacer après l'essai : voir la section NETTOYAGE en bas.
-- ════════════════════════════════════════════════════════════════════════════

-- ── Verrous : une ligne par fiche, la clé primaire garantit un seul détenteur ──
create table if not exists public.essai_verrous (
  cible       text primary key,                       -- ex. 'volteo'
  session_id  text not null,                          -- une session = une fenêtre CIPL
  user_id     uuid not null references auth.users(id) on delete cascade,
  nom         text not null,                          -- ex. 'Marie Dupont'
  motif       text not null,                          -- ex. 'facturation'
  pris_le     timestamptz not null default now(),
  expire_le   timestamptz not null
);
alter table public.essai_verrous enable row level security;
drop policy if exists essai_verrous_lecture on public.essai_verrous;
create policy essai_verrous_lecture on public.essai_verrous
  for select to authenticated using (true);
-- Aucune politique d'écriture : on n'écrit QUE par les deux fonctions ci-dessous.

-- Prendre le verrou, ou le renouveler (signal de vie) si on le détient déjà.
-- Un verrou expiré (plus de signal depuis 2 min) peut être repris par n'importe qui.
-- Deux appels simultanés : Postgres n'en laisse passer qu'un (clé primaire).
create or replace function public.essai_prendre_verrou(
  p_cible text, p_session text, p_nom text, p_motif text)
returns json language plpgsql security definer set search_path = public as $$
declare v public.essai_verrous;
begin
  if auth.uid() is null then raise exception 'non connecté'; end if;

  insert into public.essai_verrous as e (cible, session_id, user_id, nom, motif, pris_le, expire_le)
  values (p_cible, p_session, auth.uid(), p_nom, p_motif, now(), now() + interval '2 minutes')
  on conflict (cible) do update
     set session_id = excluded.session_id,
         user_id    = excluded.user_id,
         nom        = excluded.nom,
         motif      = excluded.motif,
         -- Même session ET verrou encore valide = signal de vie : on garde l'heure de départ.
         -- Sinon (autre session, ou verrou expiré puis repris) : la modification repart de maintenant.
         pris_le    = case when e.session_id = excluded.session_id and e.expire_le >= now()
                           then e.pris_le else now() end,
         expire_le  = excluded.expire_le
   where e.expire_le < now()
      or (e.session_id = excluded.session_id and e.user_id = auth.uid())
  returning * into v;

  if found then
    return json_build_object('obtenu', true, 'nom', v.nom, 'motif', v.motif,
                             'pris_le', v.pris_le, 'expire_le', v.expire_le);
  end if;

  select * into v from public.essai_verrous where cible = p_cible;
  return json_build_object('obtenu', false, 'nom', v.nom, 'motif', v.motif,
                           'pris_le', v.pris_le, 'expire_le', v.expire_le);
end $$;

create or replace function public.essai_liberer_verrou(p_cible text, p_session text)
returns void language sql security definer set search_path = public as $$
  delete from public.essai_verrous
   where cible = p_cible and session_id = p_session and user_id = auth.uid();
$$;

revoke all on function public.essai_prendre_verrou(text, text, text, text) from public, anon;
revoke all on function public.essai_liberer_verrou(text, text) from public, anon;
grant execute on function public.essai_prendre_verrou(text, text, text, text) to authenticated;
grant execute on function public.essai_liberer_verrou(text, text) to authenticated;

-- ── Une fiche d'essai, pour le temps réel et le contrôle de version ────────────
create table if not exists public.essai_fiches (
  id       text primary key,
  loyer    numeric not null,
  maj_par  text,
  version  int not null default 1,
  maj_le   timestamptz not null default now()
);
alter table public.essai_fiches enable row level security;
drop policy if exists essai_fiches_lecture on public.essai_fiches;
create policy essai_fiches_lecture on public.essai_fiches
  for select to authenticated using (true);
drop policy if exists essai_fiches_maj on public.essai_fiches;
create policy essai_fiches_maj on public.essai_fiches
  for update to authenticated using (true) with check (true);

insert into public.essai_fiches (id, loyer, maj_par) values ('volteo', 24000, 'création')
on conflict (id) do nothing;

-- ── Temps réel : Supabase diffuse les changements de ces deux tables ───────────
do $$ begin
  begin alter publication supabase_realtime add table public.essai_fiches;  exception when duplicate_object then null; end;
  begin alter publication supabase_realtime add table public.essai_verrous; exception when duplicate_object then null; end;
end $$;

-- ════════════════════════════════════════════════════════════════════════════
-- NETTOYAGE (après l'essai) : sélectionner ces lignes, retirer les « -- », Run.
-- drop function if exists public.essai_prendre_verrou(text, text, text, text);
-- drop function if exists public.essai_liberer_verrou(text, text);
-- drop table if exists public.essai_verrous;
-- drop table if exists public.essai_fiches;
-- ════════════════════════════════════════════════════════════════════════════
