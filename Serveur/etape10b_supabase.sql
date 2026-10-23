-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 10b : licences PAR ENTREPRISE (décision du 23/10, remplace « une personne = une
-- licence pour tout le groupe » du 11/10). Chaque entreprise a son nombre de licences ; une
-- personne occupe une licence dans chacune de ses entreprises (une invitation en cours aussi).
-- Rôle et facture restent ceux du groupe ; sur la facture Stripe, une ligne « licence » dont la
-- quantité est le total des entreprises.
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape10). Relançable.
-- ════════════════════════════════════════════════════════════════════════════

-- Les licences utilisées dans une entreprise : les personnes qui y ont accès, plus les
-- invitations en cours qui la comprennent.
create or replace function public.licences_utilisees_entreprise(p_entreprise uuid)
returns int language sql stable security definer set search_path = public as $$
  select (select count(*) from public.acces where entreprise_id = p_entreprise)::int
       + (select count(*) from public.invitations
           where p_entreprise = any(entreprises) and acceptee_le is null and expire_le > now())::int;
$$;

-- 3 par défaut (l'essai : 3 licences par entreprise) ; une entreprise ajoutée à un groupe déjà
-- abonné commence à 1 (ajouter_entreprise ci-dessous).
alter table public.entreprises add column if not exists licences_max int;
-- ponytail: reprise de l'existant à 3 (ou plus s'il y a plus de monde) ; pour un groupe déjà
-- abonné, l'abonnement Stripe se recale au prochain « + » / « - » (action ajuster).
update public.entreprises e set licences_max = greatest(3, public.licences_utilisees_entreprise(e.id))
 where licences_max is null;
alter table public.entreprises alter column licences_max set default 3;
alter table public.entreprises alter column licences_max set not null;

-- Inviter : chaque entreprise cochée doit avoir une licence libre ; sinon le refus la nomme.
-- Réponses : {ok, id} ou {ok: false, refus: 'licences' (nom, utilisees, max) | 'deja_membre'
--   | 'email' | 'entreprises'}.
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

-- Entreprises d'un membre : chaque entreprise AJOUTÉE doit avoir une licence libre ; en retirer
-- une libère la licence du membre dans cette entreprise.
create or replace function public.changer_acces(p_entreprise uuid, p_user uuid, p_entreprises uuid[])
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise); x record;
begin
  if p_user = auth.uid() then return json_build_object('ok', false, 'refus', 'soi_meme'); end if;
  if coalesce(array_length(p_entreprises, 1), 0) = 0
     or exists (select 1 from unnest(p_entreprises) y where y not in (select id from public.entreprises where groupe_id = g)) then
    return json_build_object('ok', false, 'refus', 'entreprises');
  end if;
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

-- Les entreprises du groupe avec leurs licences (pour l'annuaire et l'abonnement).
create or replace function public.entreprises_licences(p_groupe uuid)
returns json language sql stable security definer set search_path = public as $$
  select coalesce(json_agg(json_build_object('id', e.id, 'nom', e.nom, 'licences_max', e.licences_max,
                                              'licences_utilisees', public.licences_utilisees_entreprise(e.id))
                           order by e.cree_le), '[]'::json)
    from public.entreprises e where e.groupe_id = p_groupe;
$$;

-- Réglages › Utilisateurs (admin) : comme en 9b, les entreprises portant leurs licences.
create or replace function public.annuaire(p_entreprise uuid)
returns json language plpgsql stable security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise);
begin
  return json_build_object(
    'licences_max', (select coalesce(sum(licences_max), 0) from public.entreprises where groupe_id = g),
    'licences_utilisees', public.licences_utilisees(g),
    'entreprises', public.entreprises_licences(g),
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

-- Réglages › Abonnement et bandeaux : comme en 9c2, les entreprises portant leurs licences ;
-- licences_max du groupe = le total des entreprises.
create or replace function public.abonnement(p_entreprise uuid)
returns json language plpgsql stable security definer set search_path = public as $$
declare g uuid;
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  select groupe_id into g from public.entreprises where id = p_entreprise;
  return (select json_build_object(
    'statut', gr.statut, 'fin_essai', gr.fin_essai, 'aujourdhui', current_date,
    'licences_max', (select coalesce(sum(licences_max), 0) from public.entreprises where groupe_id = g),
    'licences_utilisees', public.licences_utilisees(g),
    'entreprises', public.entreprises_licences(g),
    'admin', (select coalesce(nullif(m.nom_affiche, ''), u.email)
                from public.membres m join auth.users u on u.id = m.user_id
               where m.groupe_id = g and m.role = 'admin'
               order by (m.user_id = gr.titulaire) desc limit 1),
    'abonne', gr.stripe_abonnement is not null,
    'formule', gr.formule, 'echeance', gr.echeance, 'resilie', gr.resilie)
    from public.groupes gr where gr.id = g);
end $$;

-- Ajouter une entreprise (9c1) : 3 licences pendant l'essai, 1 (l'admin qui l'ajoute) une fois
-- abonné ; « + » dans Réglages › Abonnement pour en ajouter.
create or replace function public.ajouter_entreprise(p_entreprise uuid, p_nom text, p_type text, p_siret text)
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
  return json_build_object('ok', true, 'id', e, 'nom', n);
end $$;

do $$ declare f text; begin
  foreach f in array array['licences_utilisees_entreprise(uuid)', 'entreprises_licences(uuid)'] loop
    execute format('revoke all on function public.%s from public, anon', f);
  end loop;
  foreach f in array array[
    'inviter(uuid, text, text, uuid[], text)', 'changer_acces(uuid, uuid, uuid[])', 'annuaire(uuid)',
    'abonnement(uuid)', 'ajouter_entreprise(uuid, text, text, text)'] loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;
