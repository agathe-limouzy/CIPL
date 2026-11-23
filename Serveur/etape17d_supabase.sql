-- Décision du 22/11/2026 : l'essai gratuit comprend UNE entreprise et 2 utilisateurs ; pour en ajouter, il faut s'abonner.
-- À exécuter dans Supabase › SQL Editor, en une fois, APRÈS etape17c. Mêmes signatures qu'avant : la version de CIPL en
-- service continue de marcher (elle affiche « Ajout impossible » au lieu du message de l'essai, rien d'autre).
--
-- Avant : pendant l'essai, « Ajouter une entreprise » était libre et gratuit, sans plafond, avec 3 licences par entreprise.
--
-- 1. entreprises.licences_max : 2 par défaut (l'entreprise créée par « inscrire », qui ne donne pas de valeur).
-- 2. ajouter_entreprise : refus « essai » tant que le groupe est en essai. Recopiée d'etape17a (dernière définition).
-- 3. creer_entreprise_import : dans un groupe en essai qui a déjà une entreprise, refus ; la première entreprise d'un groupe
--    d'essai reçoit 2 licences. Recopiée d'etape17c (dernière définition).
--
-- Les groupes d'essai existants gardent leurs entreprises et leurs licences (comptes de test).
--
-- Vérification :
--   select pg_get_functiondef('public.ajouter_entreprise(uuid, text, text, text, jsonb)'::regprocedure) like '%''refus'', ''essai''%';  → true
--   select pg_get_functiondef('public.creer_entreprise_import(text)'::regprocedure) like '%une seule entreprise%';  → true
--   select column_default from information_schema.columns
--    where table_schema = 'public' and table_name = 'entreprises' and column_name = 'licences_max';  → 2

-- ── 1. Deux licences par défaut ─────────────────────────────────────────────────────────────────────────────────
alter table public.entreprises alter column licences_max set default 2;

-- ── 2. ajouter_entreprise (recopiée d'etape17a) ─────────────────────────────────────────────────────────────────
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
  -- Décision du 22/11 : l'essai comprend une seule entreprise (celle de l'inscription).
  select statut into st from public.groupes where id = g;
  if st = 'essai' then return json_build_object('ok', false, 'refus', 'essai'); end if;
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
  insert into public.entreprises (groupe_id, nom, type, siret, licences_max)
  values (g, n, p_type, s, 1) returning id into e;   -- hors essai : 1 licence (l'admin qui l'ajoute), comme avant
  insert into public.acces (entreprise_id, user_id) values (e, auth.uid());
  -- Revue 17 (D3-01) : ses Réglages de départ (tirés de son SIRET, comme à l'inscription), jamais ceux de CIPL : sans eux,
  -- le premier poste qui l'ouvrait mettait en commun les réglages d'usine (SIRET, TVA, siège et logo de CIPL).
  if p_reglages is not null then
    insert into public.reglages (entreprise_id, data, version, maj_par) values (e, p_reglages, 1, public.mon_nom());
  end if;
  return json_build_object('ok', true, 'id', e, 'nom', n);
end $$;

-- ── 3. creer_entreprise_import (recopiée d'etape17c) ────────────────────────────────────────────────────────────
create or replace function public.creer_entreprise_import(p_nom text)
returns uuid language plpgsql security definer set search_path = public as $$
declare g uuid; e uuid; r text; st text;
        amr jsonb := coalesce(auth.jwt() -> 'amr', '[]'::jsonb);
begin
  if auth.uid() is null then raise exception 'non connecté'; end if;
  if coalesce(trim(p_nom), '') = '' then raise exception 'nom vide'; end if;

  -- Revue 17 (D1-15) : membre de plusieurs groupes, la personne importe dans celui où elle est admin, le plus ancien
  -- (« limit 1 » sans ordre tirait un groupe au hasard : création ailleurs, ou refus « réservé à un administrateur »).
  -- membres n'a pas de date : celle du groupe (groupes.cree_le) sert d'ordre.
  select m.groupe_id, m.role into g, r from public.membres m join public.groupes gr on gr.id = m.groupe_id
   where m.user_id = auth.uid() order by (m.role = 'admin') desc, gr.cree_le limit 1;
  if g is null then
    select id into g from public.groupes where titulaire = auth.uid() order by cree_le limit 1;
    r := 'admin';
  end if;
  if g is not null then
    if r <> 'admin' then raise exception 'réservé à un administrateur du groupe'; end if;
    if not public.ecriture_permise(g) then raise exception 'abonnement expiré : lecture seule'; end if;
    -- Décision du 22/11 : l'essai comprend une seule entreprise.
    if (select statut from public.groupes where id = g) = 'essai'
       and exists (select 1 from public.entreprises where groupe_id = g) then
      raise exception 'l''essai gratuit comprend une seule entreprise : abonnez-vous pour en ajouter';
    end if;
  else
    -- Revue 17 (D1-04) : un groupe d'essai neuf, seulement pour une adresse prouvée par le code reçu (comme inscrire).
    if not (amr @> '[{"method":"otp"}]'::jsonb or amr @> '[{"method":"magiclink"}]'::jsonb or amr @> '[{"method":"email/signup"}]'::jsonb) then
      raise exception 'adresse non prouvée par le code reçu';
    end if;
    insert into public.groupes (nom, titulaire) values (trim(p_nom), auth.uid()) returning id into g;
  end if;
  insert into public.membres (groupe_id, user_id, role) values (g, auth.uid(), 'admin') on conflict (groupe_id, user_id) do nothing;

  select statut into st from public.groupes where id = g;
  insert into public.entreprises (groupe_id, nom, licences_max)
  values (g, trim(p_nom), case when st = 'essai' then 2 else 1 end) returning id into e;   -- 22/11 : 2 licences pendant l'essai
  insert into public.acces (entreprise_id, user_id) values (e, auth.uid());
  return e;
end $$;

-- ── 4. Droits (identiques à l'existant) ─────────────────────────────────────────────────────────────────────────
do $$ declare f text; begin
  foreach f in array array[
    'ajouter_entreprise(uuid, text, text, text, jsonb)',
    'creer_entreprise_import(text)'] loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;
