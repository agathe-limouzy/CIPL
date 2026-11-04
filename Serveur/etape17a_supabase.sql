-- Revue 17, lot 1 (03/11/2026) : les 7 critiques. À exécuter dans Supabase › SQL Editor, en une fois,
-- AVANT de lancer la nouvelle version de CIPL (elle envoie p_modele et p_reglages : sans ce script, ses écritures
-- seraient refusées pour paramètre inconnu). L'ancienne version continue de marcher avec ce script (paramètres par défaut).
--
-- D2-01 : un numéro de modèle des données (colonne « modele ») sur les bâtiments, les locataires et les Réglages ; une version
--         plus ancienne de CIPL ne peut plus réécrire une fiche enregistrée par une plus récente (refus « application ») ni la
--         transférer. Les fonctions d'écriture prennent un paramètre de plus (p_modele) : l'ancienne signature est supprimée
--         d'abord (sinon deux fonctions du même nom coexisteraient et l'appel serait ambigu), puis les droits sont reposés.
--         Fonctions recopiées de leur dernière définition (etape16c4, etape16c2, etape16c3).
-- D3-01 : ajouter_entreprise reçoit les Réglages de départ de la nouvelle entreprise (p_reglages), comme inscrire (etape10b).
--
-- Vérification :
--   select table_name from information_schema.columns where column_name = 'modele' and table_schema = 'public';  → 3 lignes
--   select proname, pg_get_function_identity_arguments(oid) from pg_proc
--    where proname in ('enregistrer_batiment', 'enregistrer_locataire', 'enregistrer_reglages', 'transferer_fiches', 'ajouter_entreprise');
--   → une seule ligne par fonction, avec p_modele (ou p_reglages pour ajouter_entreprise).

alter table public.batiments add column if not exists modele int not null default 0;
alter table public.locataires add column if not exists modele int not null default 0;
alter table public.reglages add column if not exists modele int not null default 0;

drop function if exists public.enregistrer_batiment (uuid, text, int, jsonb, text);
drop function if exists public.enregistrer_locataire (uuid, text, text, int, int, jsonb, text);
drop function if exists public.enregistrer_reglages (uuid, int, jsonb, text);
drop function if exists public.transferer_fiches (uuid, uuid, jsonb, jsonb, boolean, jsonb, text, jsonb, int);
drop function if exists public.ajouter_entreprise (uuid, text, text, text);

create or replace function public.enregistrer_batiment(p_entreprise uuid, p_id text, p_version int, p_data jsonb, p_session text, p_modele int default 0)
returns json language plpgsql security definer set search_path = public as $$
declare v public.verrous; nv int; nom text := public.mon_nom();
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  v := public.detenteur_autre(p_entreprise, 'batiment', p_id, p_session);
  if v.cible_id is not null then
    return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le);
  end if;
  if p_version = 0 then
    insert into public.batiments (entreprise_id, id, data, version, maj_par, maj_session, modele)
    values (p_entreprise, p_id, p_data, 1, nom, p_session, coalesce(p_modele, 0)) on conflict do nothing returning version into nv;
  else
    -- Revue 17 (D2-01) : une version plus ancienne de CIPL ne réécrit pas une fiche enregistrée par une plus récente
    -- (JsonUtility y effacerait les champs qu'elle ne connaît pas) : « mettez CIPL à jour ».
    if coalesce(p_modele, 0) < coalesce((select modele from public.batiments where entreprise_id = p_entreprise and id = p_id), 0) then
      return json_build_object('ok', false, 'refus', 'application');
    end if;
    update public.batiments set data = p_data, modele = greatest(modele, coalesce(p_modele, 0)), version = version + 1, maj_par = nom, maj_le = now(), maj_session = p_session
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
                                                        p_version int, p_data jsonb, p_session text, p_modele int default 0)
returns json language plpgsql security definer set search_path = public as $$
declare v public.verrous; nv int; nom text := public.mon_nom();
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  v := public.detenteur_autre(p_entreprise, 'locataire', p_id, p_session);
  if v.cible_id is not null then
    return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le);
  end if;
  if p_version = 0 then
    insert into public.locataires (entreprise_id, id, batiment_id, ordre, data, version, maj_par, maj_session, modele)
    values (p_entreprise, p_id, p_batiment, p_ordre, p_data, 1, nom, p_session, coalesce(p_modele, 0)) on conflict do nothing returning version into nv;
  else
    -- Revue 17 (D2-01) : une version plus ancienne de CIPL ne réécrit pas une fiche enregistrée par une plus récente
    -- (JsonUtility y effacerait les champs qu'elle ne connaît pas) : « mettez CIPL à jour ».
    if coalesce(p_modele, 0) < coalesce((select modele from public.locataires where entreprise_id = p_entreprise and id = p_id), 0) then
      return json_build_object('ok', false, 'refus', 'application');
    end if;
    update public.locataires set data = p_data, modele = greatest(modele, coalesce(p_modele, 0)), ordre = p_ordre, batiment_id = p_batiment,
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

create or replace function public.enregistrer_reglages(p_entreprise uuid, p_version int, p_data jsonb, p_session text, p_modele int default 0)
returns json language plpgsql security definer set search_path = public as $$
declare nv int; nom text := public.mon_nom(); avant jsonb;
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  if p_version = 0 then
    if not public.est_admin(p_entreprise) then return json_build_object('ok', false, 'refus', 'admin'); end if;
    insert into public.reglages (entreprise_id, data, version, maj_par, maj_session, modele)
    values (p_entreprise, p_data, 1, nom, p_session, coalesce(p_modele, 0)) on conflict do nothing returning version into nv;
  else
    -- Revue 17 (D2-01) : une version plus ancienne de CIPL ne réécrit pas une fiche enregistrée par une plus récente
    -- (JsonUtility y effacerait les champs qu'elle ne connaît pas) : « mettez CIPL à jour ».
    if coalesce(p_modele, 0) < coalesce((select modele from public.reglages where entreprise_id = p_entreprise), 0) then
      return json_build_object('ok', false, 'refus', 'application');
    end if;
    if not public.est_admin(p_entreprise) then
      select data into avant from public.reglages where entreprise_id = p_entreprise;
      if avant is not null and (
           coalesce(p_data->'ribs', '[]'::jsonb) is distinct from coalesce(avant->'ribs', '[]'::jsonb)
        or p_data->'logoPath' is distinct from avant->'logoPath'
        or p_data->'sansLogo' is distinct from avant->'sansLogo'
        or exists (select 1 from jsonb_array_elements(coalesce(avant->'listesCharges', '[]'::jsonb)) l
                    where not coalesce(p_data->'listesCharges', '[]'::jsonb) @> jsonb_build_array(jsonb_build_object('id', l->>'id'))))
      then return json_build_object('ok', false, 'refus', 'admin'); end if;
    end if;
    update public.reglages set data = p_data, modele = greatest(modele, coalesce(p_modele, 0)), version = version + 1, maj_par = nom, maj_session = p_session, maj_le = now()
     where entreprise_id = p_entreprise and version = p_version returning version into nv;
  end if;
  if nv is null then
    return json_build_object('ok', false, 'refus', 'version', 'nom',
      (select maj_par from public.reglages where entreprise_id = p_entreprise));
  end if;
  return json_build_object('ok', true, 'version', nv);
end $$;

create or replace function public.transferer_fiches(p_de uuid, p_vers uuid, p_batiment jsonb, p_locataires jsonb,
                                                    p_seul boolean, p_versions jsonb, p_session text,
                                                    p_reglages jsonb default null, p_reglages_version int default 0,
                                                    p_modele int default 0)
returns json language plpgsql security definer set search_path = public as $$
declare
  nom text := public.mon_nom();
  bid text := p_batiment->>'id';
  nom_bat text := lower(trim(coalesce(p_batiment->>'Name', '')));
  g_de uuid; g_vers uuid;
  v public.verrous;
  l jsonb; lid text; cible text; nv int;
  dest_id text;
  n_loc int := 0;
begin
  if p_de = p_vers then return json_build_object('ok', false, 'refus', 'meme'); end if;
  if not public.est_admin(p_de) or not public.est_admin(p_vers) then
    return json_build_object('ok', false, 'refus', 'admin');
  end if;
  select groupe_id into g_de from public.entreprises where id = p_de;
  select groupe_id into g_vers from public.entreprises where id = p_vers;
  if g_de is null or g_de is distinct from g_vers then return json_build_object('ok', false, 'refus', 'groupe'); end if;
  if not public.ecriture_permise(g_vers) then return json_build_object('ok', false, 'refus', 'expire'); end if;
  -- Revue 17 (D2-01) : une version plus ancienne de CIPL ne recopie pas des fiches enregistrées par une plus récente.
  if coalesce(p_modele, 0) < greatest(
       coalesce((select modele from public.batiments where entreprise_id = p_de and id = bid), 0),
       coalesce((select max(lo.modele) from public.locataires lo where lo.entreprise_id = p_de
                   and lo.id in (select x->>'id' from jsonb_array_elements(p_locataires) x)), 0)) then
    return json_build_object('ok', false, 'refus', 'application');
  end if;

  -- Verrous : le bâtiment (transfert entier) et chaque locataire transféré doivent être libres.
  if not p_seul then
    v := public.detenteur_autre(p_de, 'batiment', bid, p_session);
    if v.cible_id is not null then
      return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'cible', p_batiment->>'Name');
    end if;
  end if;
  for l in select * from jsonb_array_elements(p_locataires) loop
    lid := l->>'id';
    v := public.detenteur_autre(p_de, 'locataire', lid, p_session);
    if v.cible_id is not null then
      return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'cible', l->'data'->>'Name');
    end if;
  end loop;

  -- Versions : ce que le poste connaît doit être ce que le serveur tient (sinon : relire, recommencer).
  if not p_seul and (select version from public.batiments where entreprise_id = p_de and id = bid)
       is distinct from (p_versions->>('b:' || bid))::int then
    return json_build_object('ok', false, 'refus', 'version', 'cible', p_batiment->>'Name');
  end if;
  for l in select * from jsonb_array_elements(p_locataires) loop
    lid := l->>'id';
    if (select version from public.locataires where entreprise_id = p_de and id = lid)
         is distinct from (p_versions->>('l:' || lid))::int then
      return json_build_object('ok', false, 'refus', 'version', 'cible', l->'data'->>'Name');
    end if;
  end loop;

  -- Homonyme dans la destination (par nom, comme les dossiers) : sa version est gardée, puis mise à jour
  -- (transfert entier) en conservant SON identifiant : les charges du bâtiment y font référence.
  select id into dest_id from public.batiments
   where entreprise_id = p_vers and lower(trim(data->>'Name')) = nom_bat limit 1;
  if dest_id is not null then
    insert into public.transferts_corbeille (entreprise_id, type, cible_id, data, par)
      select p_vers, 'batiment', id, data, nom from public.batiments where entreprise_id = p_vers and id = dest_id;
    if not p_seul then
      update public.batiments
         set data = jsonb_set(p_batiment, '{id}', to_jsonb(dest_id)), modele = greatest(modele, coalesce(p_modele, 0)), version = version + 1,
             maj_par = nom, maj_le = now(), maj_session = p_session
       where entreprise_id = p_vers and id = dest_id;
    end if;
  else
    dest_id := bid;
    insert into public.batiments (entreprise_id, id, data, version, maj_par, maj_session, modele)
    values (p_vers, dest_id, p_batiment, 1, nom, p_session, coalesce(p_modele, 0));
  end if;

  -- Locataires : un homonyme de la destination est remplacé (gardé avant), les autres sont ajoutés.
  for l in select * from jsonb_array_elements(p_locataires) loop
    lid := l->>'id';
    cible := lower(trim(coalesce(l->'data'->>'Name', '')));
    insert into public.transferts_corbeille (entreprise_id, type, cible_id, data, par)
      select p_vers, 'locataire', id, data, nom from public.locataires
       where entreprise_id = p_vers and batiment_id = dest_id and lower(trim(data->>'Name')) = cible and id <> lid;
    delete from public.locataires
     where entreprise_id = p_vers and batiment_id = dest_id and lower(trim(data->>'Name')) = cible and id <> lid;
    insert into public.locataires (entreprise_id, id, batiment_id, ordre, data, version, maj_par, maj_session, modele)
    values (p_vers, lid, dest_id, coalesce((l->>'ordre')::int, 0), l->'data', 1, nom, p_session, coalesce(p_modele, 0))
    on conflict (entreprise_id, id) do update
      set batiment_id = excluded.batiment_id, ordre = excluded.ordre, data = excluded.data,
          modele = greatest(public.locataires.modele, excluded.modele),
          version = public.locataires.version + 1, maj_par = excluded.maj_par, maj_le = now(), maj_session = excluded.maj_session;
    delete from public.locataires where entreprise_id = p_de and id = lid;
    n_loc := n_loc + 1;
  end loop;
  if not p_seul then delete from public.batiments where entreprise_id = p_de and id = bid; end if;

  -- Réglages de la destination : les listes de charges créées pour le transfert (par nom).
  if p_reglages is not null then
    update public.reglages
       set data = p_reglages, modele = greatest(modele, coalesce(p_modele, 0)), version = version + 1, maj_par = nom, maj_session = p_session, maj_le = now()
     where entreprise_id = p_vers and version = p_reglages_version returning version into nv;
    if nv is null then
      if p_reglages_version = 0 and not exists (select 1 from public.reglages where entreprise_id = p_vers) then
        insert into public.reglages (entreprise_id, data, version, maj_par, maj_session, modele)
        values (p_vers, p_reglages, 1, nom, p_session, coalesce(p_modele, 0));
      else
        raise exception 'réglages de la destination modifiés entre-temps : recommencez';   -- annule la transaction
      end if;
    end if;
  end if;

  return json_build_object('ok', true, 'batiment', dest_id, 'locataires', n_loc);
end $$;

create or replace function public.ajouter_entreprise(p_entreprise uuid, p_nom text, p_type text, p_siret text,
                                                     p_reglages jsonb default null)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise);
        s text := nullif(regexp_replace(coalesce(p_siret, ''), '\s', '', 'g'), '');
        n text := trim(coalesce(p_nom, ''));
        st text;
        e uuid;
begin
  if p_type not in ('societe', 'particulier') then raise exception 'type inconnu'; end if;
  if not public.ecriture_permise(g) then return json_build_object('ok', false, 'refus', 'abonnement'); end if;
  if p_type = 'particulier' then
    s := null;
    if n = '' then n := 'Patrimoine de ' || public.mon_nom(); end if;
  else
    if s is null or s !~ '^[0-9]{14}$' then return json_build_object('ok', false, 'refus', 'siret'); end if;
    if exists (select 1 from public.entreprises where siret = s) then
      return json_build_object('ok', false, 'refus', 'deja_sur_cipl');
    end if;
    if n = '' then return json_build_object('ok', false, 'refus', 'nom'); end if;
  end if;
  select statut into st from public.groupes where id = g;
  insert into public.entreprises (groupe_id, nom, type, siret, licences_max)
  values (g, n, p_type, s, case when st = 'essai' then 3 else 1 end) returning id into e;
  insert into public.acces (entreprise_id, user_id) values (e, auth.uid());
  -- Revue 17 (D3-01) : ses Réglages de départ (tirés de son SIRET, comme à l'inscription), jamais ceux de CIPL : sans eux,
  -- le premier poste qui l'ouvrait mettait en commun les réglages d'usine (SIRET, TVA, siège et logo de CIPL).
  if p_reglages is not null then
    insert into public.reglages (entreprise_id, data, version, maj_par) values (e, p_reglages, 1, public.mon_nom());
  end if;
  return json_build_object('ok', true, 'id', e, 'nom', n);
end $$;

do $$ declare f text; begin
  foreach f in array array[
    'enregistrer_batiment(uuid, text, int, jsonb, text, int)',
    'enregistrer_locataire(uuid, text, text, int, int, jsonb, text, int)',
    'enregistrer_reglages(uuid, int, jsonb, text, int)',
    'transferer_fiches(uuid, uuid, jsonb, jsonb, boolean, jsonb, text, jsonb, int, int)',
    'ajouter_entreprise(uuid, text, text, text, jsonb)'] loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;

notify pgrst, 'reload schema';
