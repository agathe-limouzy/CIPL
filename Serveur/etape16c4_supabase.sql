-- Étape 16c, suite (02/11) : mineurs de la revue 16c, lot 5 (serveur). À exécuter dans Supabase › SQL Editor.
--
-- D1-10 : les fonctions internes ne sont plus appelables par un compte connecté (« revoke … from authenticated » :
--         les privilèges par défaut de Supabase l'accordent à toute fonction nouvelle ; les fonctions SQL qui les
--         utilisent sont « security definer », elles n'ont pas besoin du droit de l'appelant). Le déclencheur
--         refuser_si_expire est laissé tel quel.
-- D1-13 : reserver_envoi refusé ne rend plus un verrou que la même session tenait déjà (saisie en cours).
-- D1-15 : journal des lectures Pappers payantes par entreprise et par jour (quota dans la fonction pappers).
-- D1-16 : supprimer_fiche contrôle p_type ; objets de l'essai technique (essai_*) retirés.
-- D2-15 : enregistrer_batiment / enregistrer_locataire répondent « supprimee » quand la fiche n'existe plus.
--         (Corrigé le 02/11 au soir : la première version de ce script les recréait sans maj_session, colonne
--         posée par l'étape 5 pour reconnaître ses propres écritures : à ré-exécuter si cette version a été jouée.)
-- D3-08 : inviter et changer_acces verrouillent les entreprises visées (for update) : deux administrateurs en
--         même temps ne dépassent plus les licences.

-- ── D1-10 : droits des fonctions ───────────────────────────────────────────────────────────────────────
do $$ declare f text; begin
  -- Internes : appelées par des fonctions « security definer » ou par le serveur (rôle service).
  foreach f in array array['ecriture_permise(uuid)', 'licences_utilisees(uuid)',
                           'licences_utilisees_entreprise(uuid)', 'entreprises_licences(uuid)']
  loop execute format('revoke all on function public.%s from public, anon, authenticated', f); end loop;
  -- Appelées par les règles d'accès (RLS) et par les fonctions serveur avec le jeton de la personne : connectés seulement.
  foreach f in array array['mes_acces()', 'est_admin(uuid)', 'groupe_admin(uuid)']
  loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;
-- Vérification : select grantee from information_schema.routine_privileges where routine_name = 'entreprises_licences';
-- → plus de ligne « authenticated ».

-- ── D1-13 : reserver_envoi ─────────────────────────────────────────────────────────────────────────────
create or replace function public.reserver_envoi(p_entreprise uuid, p_locataire text, p_facture text,
                                                 p_version int, p_session text)
returns json language plpgsql security definer set search_path = public as $$
declare v json; l public.locataires; st text; avant public.verrous;
begin
  -- Revue 16c (D1-13) : ma session tient peut-être déjà la fiche (saisie en cours) : un refus ne doit pas la libérer.
  select * into avant from public.verrous
   where entreprise_id = p_entreprise and cible_type = 'locataire' and cible_id = p_locataire
     and session_id = p_session and expire_le > now();
  v := public.prendre_verrou(p_entreprise, 'locataire', p_locataire, p_session, 'envoi des factures');   -- contrôle l'accès
  if not (v->>'obtenu')::boolean then
    return json_build_object('ok', false, 'refus', 'verrou', 'nom', v->>'nom', 'motif', v->>'motif', 'pris_le', v->>'pris_le');
  end if;
  select * into l from public.locataires where entreprise_id = p_entreprise and id = p_locataire;
  select f->>'statut' into st
    from jsonb_array_elements(coalesce(l.data->'facturesEtat', '[]'::jsonb)) f
   where f->>'key' = p_facture limit 1;
  if l.id is null or coalesce(st, '') <> 'AttenteEnvoi' or l.version <> p_version then
    if avant.cible_id is not null then
      update public.verrous set motif = avant.motif
       where entreprise_id = p_entreprise and cible_type = 'locataire' and cible_id = p_locataire and session_id = p_session;
    else
      perform public.liberer_verrou(p_entreprise, 'locataire', p_locataire, p_session);
    end if;
    return json_build_object('ok', false,
      'refus', case when coalesce(st, '') <> 'AttenteEnvoi' then 'envoyee' else 'version' end,
      'nom', l.maj_par, 'statut', st);
  end if;
  return json_build_object('ok', true);
end $$;

-- ── D1-16 : supprimer_fiche contrôle p_type ────────────────────────────────────────────────────────────
create or replace function public.supprimer_fiche(p_entreprise uuid, p_type text, p_id text, p_session text)
returns json language plpgsql security definer set search_path = public as $$
declare v public.verrous;
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  if p_type not in ('batiment', 'locataire') then raise exception 'type inconnu'; end if;   -- revue 16c, D1-16
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

-- ── D2-15 : la fiche n'existe plus ─────────────────────────────────────────────────────────────────────
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
    -- Revue 16c (D2-15) : supprimée entre-temps par un collègue (avant : « version » sans nom, fiche rechargée… vide).
    if not exists (select 1 from public.batiments where entreprise_id = p_entreprise and id = p_id) then
      return json_build_object('ok', false, 'refus', 'supprimee');
    end if;
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
    if not exists (select 1 from public.locataires where entreprise_id = p_entreprise and id = p_id) then
      return json_build_object('ok', false, 'refus', 'supprimee');   -- revue 16c, D2-15
    end if;
    return json_build_object('ok', false, 'refus', 'version', 'nom',
      (select maj_par from public.locataires where entreprise_id = p_entreprise and id = p_id));
  end if;
  return json_build_object('ok', true, 'version', nv);
end $$;

-- ── D3-08 : invitations et accès sérialisés par entreprise ─────────────────────────────────────────────
create or replace function public.inviter(p_entreprise uuid, p_email text, p_role text, p_entreprises uuid[], p_code_empreinte text)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise); em text := lower(trim(coalesce(p_email, ''))); nid uuid; x record;
begin
  if em = '' or position('@' in em) = 0 then return json_build_object('ok', false, 'refus', 'email'); end if;
  if p_role not in ('admin', 'utilisateur') then raise exception 'rôle inconnu'; end if;
  if exists (select 1 from public.membres m join auth.users u on u.id = m.user_id
              where m.groupe_id = g and lower(u.email) = em) then
    return json_build_object('ok', false, 'refus', 'deja_membre');
  end if;
  if coalesce(array_length(p_entreprises, 1), 0) = 0
     or exists (select 1 from unnest(p_entreprises) y where y not in (select id from public.entreprises where groupe_id = g)) then
    return json_build_object('ok', false, 'refus', 'entreprises');
  end if;
  -- Revue 16c (D3-08) : les entreprises visées sont verrouillées le temps de la transaction : deux invitations
  -- simultanées ne voient plus toutes deux la même licence libre.
  perform 1 from public.entreprises where id = any(p_entreprises) for update;
  -- Une seule invitation en cours par adresse : la nouvelle remplace l'ancienne (et ses licences).
  delete from public.invitations where groupe_id = g and lower(email) = em and acceptee_le is null;
  for x in select e.nom, e.licences_max, public.licences_utilisees_entreprise(e.id) as u
             from public.entreprises e where e.id = any(p_entreprises) order by e.nom loop
    if x.u >= x.licences_max then
      return json_build_object('ok', false, 'refus', 'licences', 'nom', x.nom, 'utilisees', x.u, 'max', x.licences_max);
    end if;
  end loop;
  insert into public.invitations (groupe_id, email, role, entreprises, code_empreinte, invite_par)
  values (g, em, p_role, p_entreprises, p_code_empreinte, public.mon_nom()) returning id into nid;
  return json_build_object('ok', true, 'id', nid);
end $$;

create or replace function public.changer_acces(p_entreprise uuid, p_user uuid, p_entreprises uuid[])
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise); x record;
begin
  if p_user = auth.uid() then return json_build_object('ok', false, 'refus', 'soi_meme'); end if;
  if coalesce(array_length(p_entreprises, 1), 0) = 0
     or exists (select 1 from unnest(p_entreprises) y where y not in (select id from public.entreprises where groupe_id = g)) then
    return json_build_object('ok', false, 'refus', 'entreprises');
  end if;
  perform 1 from public.entreprises where id = any(p_entreprises) for update;   -- revue 16c, D3-08
  for x in select e.nom, e.licences_max, public.licences_utilisees_entreprise(e.id) as u
             from public.entreprises e
            where e.id = any(p_entreprises)
              and not exists (select 1 from public.acces a where a.entreprise_id = e.id and a.user_id = p_user)
            order by e.nom loop
    if x.u >= x.licences_max then
      return json_build_object('ok', false, 'refus', 'licences', 'nom', x.nom, 'utilisees', x.u, 'max', x.licences_max);
    end if;
  end loop;
  delete from public.acces a using public.entreprises e
   where a.entreprise_id = e.id and e.groupe_id = g and a.user_id = p_user and not (a.entreprise_id = any(p_entreprises));
  insert into public.acces (entreprise_id, user_id) select y, p_user from unnest(p_entreprises) y on conflict do nothing;
  return json_build_object('ok', true);
end $$;

-- ── D1-15 : journal des lectures Pappers payantes ──────────────────────────────────────────────────────
create table if not exists public.pappers_journal (
  entreprise_id uuid not null references public.entreprises(id) on delete cascade,
  jour          date not null,
  n             int  not null default 0,
  primary key (entreprise_id, jour)
);
-- Aucune règle d'accès : seule la fonction pappers (rôle service) y lit et y écrit.
alter table public.pappers_journal enable row level security;
revoke all on public.pappers_journal from anon, authenticated;

-- ── D1-16 : objets de l'essai technique (étape 1), s'ils existent encore ───────────────────────────────
drop function if exists public.essai_prendre_verrou(text, text, text, text);
drop function if exists public.essai_liberer_verrou(text, text);
drop table if exists public.essai_verrous cascade;
drop table if exists public.essai_fiches cascade;
