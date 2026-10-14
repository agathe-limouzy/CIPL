-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 5 : en direct (changements des collègues, présence)
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape4b).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §11 (étape 5).
-- ════════════════════════════════════════════════════════════════════════════

-- Qui a enregistré, à la session près : chaque application ignore l'écho de ses
-- propres enregistrements (sinon elle relirait la fiche qu'elle vient d'écrire).
alter table public.batiments  add column if not exists maj_session text;
alter table public.locataires add column if not exists maj_session text;

-- Mêmes fonctions qu'à l'étape 4a, plus maj_session.
create or replace function public.enregistrer_batiment(p_entreprise uuid, p_id text, p_version int, p_data jsonb, p_session text)
returns json language plpgsql security definer set search_path = public as $$
declare v public.verrous; nv int; nom text := public.mon_nom();
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  v := public.detenteur_autre(p_entreprise, 'batiment', p_id, p_session);
  if v.cible_id is not null then
    return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le);
  end if;
  if p_version = 0 then
    insert into public.batiments (entreprise_id, id, data, version, maj_par, maj_session)
    values (p_entreprise, p_id, p_data, 1, nom, p_session) on conflict do nothing returning version into nv;
  else
    update public.batiments set data = p_data, version = version + 1, maj_par = nom, maj_le = now(), maj_session = p_session
     where entreprise_id = p_entreprise and id = p_id and version = p_version returning version into nv;
  end if;
  if nv is null then
    return json_build_object('ok', false, 'refus', 'version', 'nom',
      (select maj_par from public.batiments where entreprise_id = p_entreprise and id = p_id));
  end if;
  return json_build_object('ok', true, 'version', nv);
end $$;

create or replace function public.enregistrer_locataire(p_entreprise uuid, p_id text, p_batiment text, p_ordre int,
                                                        p_version int, p_data jsonb, p_session text)
returns json language plpgsql security definer set search_path = public as $$
declare v public.verrous; nv int; nom text := public.mon_nom();
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  v := public.detenteur_autre(p_entreprise, 'locataire', p_id, p_session);
  if v.cible_id is not null then
    return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le);
  end if;
  if p_version = 0 then
    insert into public.locataires (entreprise_id, id, batiment_id, ordre, data, version, maj_par, maj_session)
    values (p_entreprise, p_id, p_batiment, p_ordre, p_data, 1, nom, p_session) on conflict do nothing returning version into nv;
  else
    update public.locataires set data = p_data, ordre = p_ordre, batiment_id = p_batiment,
                                 version = version + 1, maj_par = nom, maj_le = now(), maj_session = p_session
     where entreprise_id = p_entreprise and id = p_id and version = p_version returning version into nv;
  end if;
  if nv is null then
    return json_build_object('ok', false, 'refus', 'version', 'nom',
      (select maj_par from public.locataires where entreprise_id = p_entreprise and id = p_id));
  end if;
  return json_build_object('ok', true, 'version', nv);
end $$;

-- ── Présence : une ligne par session ouverte, signal toutes les 30 s ──────────
-- « En ligne » = signal de moins de 2 min (décidé côté application).
create table if not exists public.presences (
  entreprise_id uuid not null references public.entreprises(id) on delete cascade,
  session_id    text not null,
  user_id       uuid not null references auth.users(id) on delete cascade,
  nom           text not null,
  vu_le         timestamptz not null default now(),
  primary key (entreprise_id, session_id)
);
alter table public.presences enable row level security;
drop policy if exists presences_lecture on public.presences;
create policy presences_lecture on public.presences for select to authenticated
  using (entreprise_id in (select public.mes_acces()));

create or replace function public.signaler_presence(p_entreprise uuid, p_session text)
returns void language plpgsql security definer set search_path = public as $$
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  insert into public.presences as p (entreprise_id, session_id, user_id, nom, vu_le)
  values (p_entreprise, p_session, auth.uid(), public.mon_nom(), now())
  on conflict (entreprise_id, session_id) do update set vu_le = now(), nom = excluded.nom
   where p.user_id = auth.uid();
  delete from public.presences where vu_le < now() - interval '1 day';   -- sessions plantées
end $$;

create or replace function public.quitter_presence(p_entreprise uuid, p_session text)
returns void language sql security definer set search_path = public as $$
  delete from public.presences
   where entreprise_id = p_entreprise and session_id = p_session and user_id = auth.uid();
$$;

-- ── Temps réel : les changements de ces tables sont diffusés aux membres ─────
do $$ begin
  begin alter publication supabase_realtime add table public.batiments;  exception when duplicate_object then null; end;
  begin alter publication supabase_realtime add table public.locataires; exception when duplicate_object then null; end;
  begin alter publication supabase_realtime add table public.verrous;    exception when duplicate_object then null; end;
  begin alter publication supabase_realtime add table public.presences;  exception when duplicate_object then null; end;
end $$;

do $$ declare f text; begin
  foreach f in array array[
    'enregistrer_batiment(uuid, text, int, jsonb, text)', 'enregistrer_locataire(uuid, text, text, int, int, jsonb, text)',
    'signaler_presence(uuid, text)', 'quitter_presence(uuid, text)']
  loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;
