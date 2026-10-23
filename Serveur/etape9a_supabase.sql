-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 9a : Réglages et secrets de l'entreprise partagés par le serveur
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape7).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md (étape 9).
-- ════════════════════════════════════════════════════════════════════════════

-- Les Réglages (le contenu de reglage.json), avec une version : deux personnes qui
-- enregistrent en même temps → la seconde est refusée (décision du 19/10).
create table if not exists public.reglages (
  entreprise_id uuid primary key references public.entreprises(id) on delete cascade,
  data          jsonb not null,
  version       int  not null default 1,
  maj_par       text,
  maj_session   text,
  maj_le        timestamptz not null default now()
);

-- Mot de passe SMTP, clé Pennylane : lus par l'application de chaque membre (l'envoi part de
-- son poste), modifiés par un admin seulement (definir_secret).
create table if not exists public.secrets (
  entreprise_id uuid not null references public.entreprises(id) on delete cascade,
  cle           text not null,
  valeur        text not null default '',
  maj_le        timestamptz not null default now(),
  primary key (entreprise_id, cle)
);

alter table public.reglages enable row level security;
alter table public.secrets  enable row level security;
drop policy if exists reglages_lecture on public.reglages;
create policy reglages_lecture on public.reglages for select to authenticated
  using (entreprise_id in (select public.mes_acces()));
drop policy if exists secrets_lecture on public.secrets;
create policy secrets_lecture on public.secrets for select to authenticated
  using (entreprise_id in (select public.mes_acces()));

-- Enregistrer les Réglages. p_version = 0 : première mise en commun, par un admin seulement
-- (les Réglages du premier admin deviennent ceux de l'entreprise, décision du 19/10).
-- Réponse : {ok, version} ou {ok: false, refus: 'version' | 'admin', nom}.
create or replace function public.enregistrer_reglages(p_entreprise uuid, p_version int, p_data jsonb, p_session text)
returns json language plpgsql security definer set search_path = public as $$
declare nv int; nom text := public.mon_nom();
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  if p_version = 0 then
    if not public.est_admin(p_entreprise) then return json_build_object('ok', false, 'refus', 'admin'); end if;
    insert into public.reglages (entreprise_id, data, version, maj_par, maj_session)
    values (p_entreprise, p_data, 1, nom, p_session) on conflict do nothing returning version into nv;
  else
    update public.reglages set data = p_data, version = version + 1, maj_par = nom, maj_session = p_session, maj_le = now()
     where entreprise_id = p_entreprise and version = p_version returning version into nv;
  end if;
  if nv is null then
    return json_build_object('ok', false, 'refus', 'version', 'nom',
      (select maj_par from public.reglages where entreprise_id = p_entreprise));
  end if;
  return json_build_object('ok', true, 'version', nv);
end $$;

create or replace function public.definir_secret(p_entreprise uuid, p_cle text, p_valeur text)
returns json language plpgsql security definer set search_path = public as $$
begin
  if not public.est_admin(p_entreprise) then return json_build_object('ok', false, 'refus', 'admin'); end if;
  insert into public.secrets as s (entreprise_id, cle, valeur) values (p_entreprise, p_cle, coalesce(p_valeur, ''))
  on conflict (entreprise_id, cle) do update set valeur = excluded.valeur, maj_le = now();
  return json_build_object('ok', true);
end $$;

-- Temps réel : un changement des Réglages ou d'un secret arrive tout de suite chez les autres.
do $$ begin
  begin alter publication supabase_realtime add table public.reglages; exception when duplicate_object then null; end;
  begin alter publication supabase_realtime add table public.secrets;  exception when duplicate_object then null; end;
end $$;

revoke all on function public.enregistrer_reglages(uuid, int, jsonb, text) from public, anon;
grant execute on function public.enregistrer_reglages(uuid, int, jsonb, text) to authenticated;
revoke all on function public.definir_secret(uuid, text, text) from public, anon;
grant execute on function public.definir_secret(uuid, text, text) to authenticated;
