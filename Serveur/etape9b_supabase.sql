-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 9b : utilisateurs, invitations, rôles, entreprises accessibles
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape9a).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md (étape 9).
--
-- AUSSI, une fois : Supabase › Authentication › Sign In / Providers › Email : décocher
-- « Confirm email ». Le code d'invitation, envoyé à l'adresse invitée, prouve déjà la boîte
-- mail (décision du 19/10) ; sans cela, la création du compte attendrait un lien de confirmation.
-- ════════════════════════════════════════════════════════════════════════════

create extension if not exists pgcrypto with schema extensions;   -- digest (déjà présent sur Supabase)

-- Une invitation en cours réserve une licence (décision du 11/10). Le code n'est pas gardé,
-- seulement son empreinte (SHA-256 du code sans tirets, en majuscules).
create table if not exists public.invitations (
  id             uuid primary key default gen_random_uuid(),
  groupe_id      uuid not null references public.groupes(id) on delete cascade,
  email          text not null,
  role           text not null default 'utilisateur' check (role in ('admin', 'utilisateur')),
  entreprises    uuid[] not null,
  code_empreinte text not null,
  invite_par     text,
  cree_le        timestamptz not null default now(),
  expire_le      timestamptz not null default now() + interval '7 days',
  acceptee_le    timestamptz
);
alter table public.invitations enable row level security;   -- aucune lecture directe : tout passe par les fonctions

-- Le groupe d'une entreprise dont je suis admin ; sinon, refus.
create or replace function public.groupe_admin(p_entreprise uuid)
returns uuid language plpgsql stable security definer set search_path = public as $$
declare g uuid;
begin
  if not public.est_admin(p_entreprise) then raise exception 'admin seulement'; end if;
  select groupe_id into g from public.entreprises where id = p_entreprise;
  return g;
end $$;

-- Licences utilisées : les membres, plus les invitations en cours.
create or replace function public.licences_utilisees(p_groupe uuid)
returns int language sql stable security definer set search_path = public as $$
  select (select count(*) from public.membres where groupe_id = p_groupe)::int
       + (select count(*) from public.invitations
           where groupe_id = p_groupe and acceptee_le is null and expire_le > now())::int;
$$;

-- Tout ce qu'affiche Réglages › Utilisateurs (admin).
create or replace function public.annuaire(p_entreprise uuid)
returns json language plpgsql stable security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise);
begin
  return json_build_object(
    'licences_max', (select licences_max from public.groupes where id = g),
    'licences_utilisees', public.licences_utilisees(g),
    'entreprises', coalesce((select json_agg(json_build_object('id', e.id, 'nom', e.nom) order by e.nom)
                               from public.entreprises e where e.groupe_id = g), '[]'::json),
    'membres', coalesce((select json_agg(json_build_object(
        'user_id', m.user_id, 'nom', coalesce(m.nom_affiche, ''), 'email', u.email, 'role', m.role,
        'entreprises', coalesce((select json_agg(a.entreprise_id) from public.acces a
                                   join public.entreprises e on e.id = a.entreprise_id
                                  where a.user_id = m.user_id and e.groupe_id = g), '[]'::json),
        'en_ligne', exists (select 1 from public.presences p where p.user_id = m.user_id and p.vu_le > now() - interval '2 minutes'),
        'moi', m.user_id = auth.uid())
        order by m.role, coalesce(nullif(m.nom_affiche, ''), u.email))
        from public.membres m join auth.users u on u.id = m.user_id where m.groupe_id = g), '[]'::json),
    'invitations', coalesce((select json_agg(json_build_object(
        'id', i.id, 'email', i.email, 'role', i.role, 'entreprises', i.entreprises, 'expire_le', i.expire_le) order by i.cree_le)
        from public.invitations i where i.groupe_id = g and i.acceptee_le is null and i.expire_le > now()), '[]'::json));
end $$;

-- Réponses : {ok} ou {ok: false, refus: 'licences' (utilisees, max) | 'deja_membre' | 'email'
--   | 'entreprises' | 'dernier_admin' | 'soi_meme' | 'code' | 'expire'}.
create or replace function public.inviter(p_entreprise uuid, p_email text, p_role text, p_entreprises uuid[], p_code_empreinte text)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise); mx int; em text := lower(trim(coalesce(p_email, ''))); nid uuid;
begin
  if em = '' or position('@' in em) = 0 then return json_build_object('ok', false, 'refus', 'email'); end if;
  if p_role not in ('admin', 'utilisateur') then raise exception 'rôle inconnu'; end if;
  if exists (select 1 from public.membres m join auth.users u on u.id = m.user_id
              where m.groupe_id = g and lower(u.email) = em) then
    return json_build_object('ok', false, 'refus', 'deja_membre');
  end if;
  if coalesce(array_length(p_entreprises, 1), 0) = 0
     or exists (select 1 from unnest(p_entreprises) x where x not in (select id from public.entreprises where groupe_id = g)) then
    return json_build_object('ok', false, 'refus', 'entreprises');
  end if;
  -- Une seule invitation en cours par adresse : la nouvelle remplace l'ancienne (et sa licence).
  delete from public.invitations where groupe_id = g and lower(email) = em and acceptee_le is null;
  select licences_max into mx from public.groupes where id = g;
  if public.licences_utilisees(g) >= mx then
    return json_build_object('ok', false, 'refus', 'licences', 'utilisees', public.licences_utilisees(g), 'max', mx);
  end if;
  insert into public.invitations (groupe_id, email, role, entreprises, code_empreinte, invite_par)
  values (g, em, p_role, p_entreprises, p_code_empreinte, public.mon_nom()) returning id into nid;
  return json_build_object('ok', true, 'id', nid);
end $$;

create or replace function public.annuler_invitation(p_entreprise uuid, p_invitation uuid)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise);
begin
  delete from public.invitations where id = p_invitation and groupe_id = g and acceptee_le is null;
  return json_build_object('ok', true);
end $$;

-- Nouveau code, encore 7 jours.
create or replace function public.renvoyer_invitation(p_entreprise uuid, p_invitation uuid, p_code_empreinte text)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise);
begin
  update public.invitations set code_empreinte = p_code_empreinte, expire_le = now() + interval '7 days'
   where id = p_invitation and groupe_id = g and acceptee_le is null;
  if not found then return json_build_object('ok', false, 'refus', 'expire'); end if;
  return json_build_object('ok', true);
end $$;

-- L'invité, connecté avec l'adresse invitée, présente le code reçu.
create or replace function public.accepter_invitation(p_code text)
returns json language plpgsql security definer set search_path = public as $$
declare em text; i public.invitations; e uuid; noms text;
begin
  if auth.uid() is null then raise exception 'non connecté'; end if;
  select lower(email) into em from auth.users where id = auth.uid();
  select * into i from public.invitations
   where lower(email) = em and acceptee_le is null
     and code_empreinte = encode(extensions.digest(upper(regexp_replace(coalesce(p_code, ''), '[^A-Za-z0-9]', '', 'g')), 'sha256'), 'hex')
   order by cree_le desc limit 1;
  if i.id is null then return json_build_object('ok', false, 'refus', 'code'); end if;
  if i.expire_le < now() then return json_build_object('ok', false, 'refus', 'expire'); end if;
  insert into public.membres (groupe_id, user_id, role) values (i.groupe_id, auth.uid(), i.role)
    on conflict (groupe_id, user_id) do nothing;
  foreach e in array i.entreprises loop
    insert into public.acces (entreprise_id, user_id) values (e, auth.uid()) on conflict do nothing;
  end loop;
  update public.invitations set acceptee_le = now() where id = i.id;
  select string_agg(nom, ', ' order by nom) into noms from public.entreprises where id = any(i.entreprises);
  return json_build_object('ok', true, 'nom', noms);
end $$;

-- Rôle d'un membre. Le dernier admin ne peut pas être rétrogradé (décision du 11/10).
create or replace function public.changer_role(p_entreprise uuid, p_user uuid, p_role text)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise);
begin
  if p_role not in ('admin', 'utilisateur') then raise exception 'rôle inconnu'; end if;
  if p_user = auth.uid() then return json_build_object('ok', false, 'refus', 'soi_meme'); end if;
  if p_role = 'utilisateur' and exists (select 1 from public.membres where groupe_id = g and user_id = p_user and role = 'admin')
     and (select count(*) from public.membres where groupe_id = g and role = 'admin') <= 1 then
    return json_build_object('ok', false, 'refus', 'dernier_admin');
  end if;
  update public.membres set role = p_role where groupe_id = g and user_id = p_user;
  return json_build_object('ok', true);
end $$;

-- Entreprises du groupe accessibles à un membre (au moins une).
create or replace function public.changer_acces(p_entreprise uuid, p_user uuid, p_entreprises uuid[])
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise);
begin
  if p_user = auth.uid() then return json_build_object('ok', false, 'refus', 'soi_meme'); end if;
  if coalesce(array_length(p_entreprises, 1), 0) = 0
     or exists (select 1 from unnest(p_entreprises) x where x not in (select id from public.entreprises where groupe_id = g)) then
    return json_build_object('ok', false, 'refus', 'entreprises');
  end if;
  delete from public.acces a using public.entreprises e
   where a.entreprise_id = e.id and e.groupe_id = g and a.user_id = p_user and not (a.entreprise_id = any(p_entreprises));
  insert into public.acces (entreprise_id, user_id) select x, p_user from unnest(p_entreprises) x on conflict do nothing;
  return json_build_object('ok', true);
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
  return json_build_object('ok', true);
end $$;

do $$ declare f text; begin
  foreach f in array array[
    'groupe_admin(uuid)', 'licences_utilisees(uuid)', 'annuaire(uuid)',
    'inviter(uuid, text, text, uuid[], text)', 'annuler_invitation(uuid, uuid)', 'renvoyer_invitation(uuid, uuid, text)',
    'accepter_invitation(text)', 'changer_role(uuid, uuid, text)', 'changer_acces(uuid, uuid, uuid[])', 'retirer_membre(uuid, uuid)']
  loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;
