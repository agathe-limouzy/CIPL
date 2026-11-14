-- Revue 17, lot 2b (04/11/2026) : abonnement et comptes. À exécuter dans Supabase › SQL Editor, en une fois.
-- Mêmes signatures qu'avant : la version de CIPL en service continue de marcher, rien à faire dans l'application.
--
-- D1-04 : creer_entreprise_import, quand le compte n'a pas encore de groupe, exige une adresse prouvée par le code reçu,
--         comme inscrire (D3-06 de la 16c) : un compte créé directement par l'API au nom de n'importe quelle adresse ne
--         s'ouvre plus de groupe d'essai (ni n'empêche le vrai propriétaire de l'adresse de « Créer mon compte »).
--         L'import dans un groupe existant, par son admin, ne change pas. Recopiée d'etape16c2 (dernière définition).
-- D3-04 : retirer_membre : retirer le titulaire d'un groupe passe le titre à l'admin qui le retire ; avant, l'ancien
--         titulaire restait « déjà dans un groupe » pour toujours (« Créer mon compte » refusé, accès à aucune entreprise).
--         Recopiée d'etape9b (dernière définition).
--
-- Vérification :
--   select pg_get_functiondef('public.creer_entreprise_import(text)'::regprocedure) like '%adresse non prouvée%';  → true
--   select pg_get_functiondef('public.retirer_membre(uuid, uuid)'::regprocedure) like '%set titulaire = auth.uid()%';  → true

create or replace function public.creer_entreprise_import(p_nom text)
returns uuid language plpgsql security definer set search_path = public as $$
declare g uuid; e uuid; r text; st text;
        amr jsonb := coalesce(auth.jwt() -> 'amr', '[]'::jsonb);
begin
  if auth.uid() is null then raise exception 'non connecté'; end if;
  if coalesce(trim(p_nom), '') = '' then raise exception 'nom vide'; end if;

  select m.groupe_id, m.role into g, r from public.membres m where m.user_id = auth.uid() limit 1;
  if g is null then
    select id into g from public.groupes where titulaire = auth.uid() order by cree_le limit 1;
    r := 'admin';
  end if;
  if g is not null then
    if r <> 'admin' then raise exception 'réservé à un administrateur du groupe'; end if;
    if not public.ecriture_permise(g) then raise exception 'abonnement expiré : lecture seule'; end if;
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
  values (g, trim(p_nom), case when st = 'essai' then 3 else 1 end) returning id into e;
  insert into public.acces (entreprise_id, user_id) values (e, auth.uid());
  return e;
end $$;

-- Retirer un membre : sa licence se libère, il perd l'accès à toutes les entreprises du groupe.
create or replace function public.retirer_membre(p_entreprise uuid, p_user uuid)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise);
begin
  if p_user = auth.uid() then return json_build_object('ok', false, 'refus', 'soi_meme'); end if;
  if exists (select 1 from public.membres where groupe_id = g and user_id = p_user and role = 'admin')
     and (select count(*) from public.membres where groupe_id = g and role = 'admin') <= 1 then
    return json_build_object('ok', false, 'refus', 'dernier_admin');
  end if;
  delete from public.verrous v using public.entreprises e where v.entreprise_id = e.id and e.groupe_id = g and v.user_id = p_user;
  delete from public.presences p using public.entreprises e where p.entreprise_id = e.id and e.groupe_id = g and p.user_id = p_user;
  delete from public.acces a using public.entreprises e where a.entreprise_id = e.id and e.groupe_id = g and a.user_id = p_user;
  delete from public.membres where groupe_id = g and user_id = p_user;
  -- Revue 17 (D3-04) : le titulaire retiré ne garde pas le groupe : l'admin qui le retire en devient titulaire (abonnement(),
  -- creer_entreprise_import suivent) ; l'ancien titulaire peut de nouveau « Créer mon compte » avec son adresse.
  update public.groupes set titulaire = auth.uid() where id = g and titulaire = p_user;
  return json_build_object('ok', true);
end $$;

do $$ declare f text; begin
  foreach f in array array['creer_entreprise_import(text)', 'retirer_membre(uuid, uuid)'] loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;

notify pgrst, 'reload schema';
