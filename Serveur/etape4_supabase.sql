-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 4a : enregistrement dans le cloud, contrôle de version, verrous
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape2).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §5 et §6.
-- ════════════════════════════════════════════════════════════════════════════

-- ── Verrous : une ligne par fiche ; la clé primaire garantit un seul détenteur ──
create table if not exists public.verrous (
  entreprise_id uuid not null references public.entreprises(id) on delete cascade,
  cible_type    text not null check (cible_type in ('batiment', 'locataire')),
  cible_id      text not null,
  session_id    text not null,
  user_id       uuid not null references auth.users(id) on delete cascade,
  nom           text not null,
  motif         text not null,
  pris_le       timestamptz not null default now(),
  expire_le     timestamptz not null,
  primary key (entreprise_id, cible_type, cible_id)
);
alter table public.verrous enable row level security;
drop policy if exists verrous_lecture on public.verrous;
create policy verrous_lecture on public.verrous for select to authenticated
  using (entreprise_id in (select public.mes_acces()));

-- Nom affiché à mes collègues (membres.nom_affiche), sinon mon e-mail.
create or replace function public.mon_nom()
returns text language sql stable security definer set search_path = public as $$
  select coalesce(
    (select nom_affiche from public.membres where user_id = auth.uid() and coalesce(nom_affiche, '') <> '' limit 1),
    (select email from auth.users where id = auth.uid()));
$$;

create or replace function public.mon_nom_saisi()
returns text language sql stable security definer set search_path = public as $$
  select nom_affiche from public.membres where user_id = auth.uid() and coalesce(nom_affiche, '') <> '' limit 1;
$$;

create or replace function public.definir_mon_nom(p_nom text)
returns void language sql security definer set search_path = public as $$
  update public.membres set nom_affiche = nullif(trim(p_nom), '') where user_id = auth.uid();
$$;

-- Qui détient la fiche, si ce n'est pas ma session et que le verrou est encore valable.
create or replace function public.detenteur_autre(p_entreprise uuid, p_type text, p_id text, p_session text)
returns public.verrous language sql stable security definer set search_path = public as $$
  select * from public.verrous
   where entreprise_id = p_entreprise and cible_type = p_type and cible_id = p_id
     and session_id <> p_session and expire_le >= now();
$$;

-- ── Prendre / renouveler / libérer un verrou (même logique que l'essai, corrigée) ──
create or replace function public.prendre_verrou(p_entreprise uuid, p_type text, p_id text, p_session text, p_motif text)
returns json language plpgsql security definer set search_path = public as $$
declare v public.verrous;
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  insert into public.verrous as e (entreprise_id, cible_type, cible_id, session_id, user_id, nom, motif, pris_le, expire_le)
  values (p_entreprise, p_type, p_id, p_session, auth.uid(), public.mon_nom(), p_motif, now(), now() + interval '2 minutes')
  on conflict (entreprise_id, cible_type, cible_id) do update
     set session_id = excluded.session_id, user_id = excluded.user_id, nom = excluded.nom, motif = excluded.motif,
         pris_le   = case when e.session_id = excluded.session_id and e.expire_le >= now() then e.pris_le else now() end,
         expire_le = excluded.expire_le
   where e.expire_le < now() or (e.session_id = excluded.session_id and e.user_id = auth.uid())
  returning * into v;
  if found then
    return json_build_object('obtenu', true, 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le, 'expire_le', v.expire_le);
  end if;
  select * into v from public.verrous where entreprise_id = p_entreprise and cible_type = p_type and cible_id = p_id;
  return json_build_object('obtenu', false, 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le, 'expire_le', v.expire_le);
end $$;

create or replace function public.liberer_verrou(p_entreprise uuid, p_type text, p_id text, p_session text)
returns void language sql security definer set search_path = public as $$
  delete from public.verrous
   where entreprise_id = p_entreprise and cible_type = p_type and cible_id = p_id
     and session_id = p_session and user_id = auth.uid();
$$;

-- ── Enregistrer : refus si la fiche est verrouillée par une autre session, ou si la
--    version présentée n'est plus la bonne (quelqu'un a enregistré entre-temps).
--    p_version = 0 : nouvelle fiche. Réponse : {ok, version, refus, nom, motif, pris_le}.
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
    insert into public.batiments (entreprise_id, id, data, version, maj_par)
    values (p_entreprise, p_id, p_data, 1, nom) on conflict do nothing returning version into nv;
  else
    update public.batiments set data = p_data, version = version + 1, maj_par = nom, maj_le = now()
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
    insert into public.locataires (entreprise_id, id, batiment_id, ordre, data, version, maj_par)
    values (p_entreprise, p_id, p_batiment, p_ordre, p_data, 1, nom) on conflict do nothing returning version into nv;
  else
    update public.locataires set data = p_data, ordre = p_ordre, batiment_id = p_batiment,
                                 version = version + 1, maj_par = nom, maj_le = now()
     where entreprise_id = p_entreprise and id = p_id and version = p_version returning version into nv;
  end if;
  if nv is null then
    return json_build_object('ok', false, 'refus', 'version', 'nom',
      (select maj_par from public.locataires where entreprise_id = p_entreprise and id = p_id));
  end if;
  return json_build_object('ok', true, 'version', nv);
end $$;

create or replace function public.supprimer_fiche(p_entreprise uuid, p_type text, p_id text, p_session text)
returns json language plpgsql security definer set search_path = public as $$
declare v public.verrous;
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  v := public.detenteur_autre(p_entreprise, p_type, p_id, p_session);
  if v.cible_id is not null then
    return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le);
  end if;
  if p_type = 'batiment' then
    delete from public.batiments where entreprise_id = p_entreprise and id = p_id;   -- ses locataires suivent (cascade)
  else
    delete from public.locataires where entreprise_id = p_entreprise and id = p_id;
  end if;
  delete from public.verrous where entreprise_id = p_entreprise and cible_type = p_type and cible_id = p_id;
  return json_build_object('ok', true);
end $$;

do $$ declare f text; begin
  foreach f in array array[
    'mon_nom()', 'mon_nom_saisi()', 'definir_mon_nom(text)', 'detenteur_autre(uuid, text, text, text)',
    'prendre_verrou(uuid, text, text, text, text)', 'liberer_verrou(uuid, text, text, text)',
    'enregistrer_batiment(uuid, text, int, jsonb, text)', 'enregistrer_locataire(uuid, text, text, int, int, jsonb, text)',
    'supprimer_fiche(uuid, text, text, text)']
  loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;
