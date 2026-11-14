-- Revue 17, lot B (09/11/2026) : serveur, groupe « sql ». À exécuter UNE fois dans Supabase › SQL Editor, avant le passage
-- en réel. Mêmes signatures qu'avant : la version de CIPL en service continue de marcher, rien à faire dans l'application.
-- Relançable sans risque. Chaque fonction est recopiée de sa DERNIÈRE définition (etape16c, etape17a, etape17b, etape9b,
-- etape16c4) et n'y change que ce que la fiche demande (REVUE_17/D1.md, REVUE_17/D3.md).
-- Lot D (11/11/2026), sections 8 à 12 : même fichier, mêmes règles (REVUE_17/D2.md, REVUE_17/D1.md). Une seule fonction
-- nouvelle, enregistrer_fiches : tant que ce script n'est pas passé, CIPL ne la trouve pas (HTTP 404) et enregistre comme
-- avant, une fiche après l'autre.
--
-- Sections :
--   1. D1-08 : detenteur_autre n'est plus appelable par un compte connecté (revoke … from authenticated).
--   2. D1-09, D1-10 : documenter et supprimer_document réservent Logo/ à l'admin (refus « admin seulement ») ;
--      documenter refuse une empreinte qui n'est pas 64 hexadécimaux (« empreinte refusée »).
--   3. D1-13 : enregistrer_reglages contrôle la version AVANT les champs réservés à l'admin : une copie périmée
--      reçoit « version » (rechargement), plus « admin ».
--   4. D3-15, D1-14 : le dernier admin se compte par entreprise (retirer_membre, changer_role, changer_acces : refus
--      « dernier_admin » avec « nom » de l'entreprise) ; changer_acces exige que la personne visée soit membre du
--      groupe (refus « membre »).
--   5. D1-15 : creer_entreprise_import choisit le groupe où la personne est admin, le plus ancien (plus de « limit 1 »
--      sans ordre).
--   6. Droits du lot B (identiques à l'existant).
--   7. D1-05, D1-10 : prendre_lecture_pappers, cartes_journal et prendre_lecture_cartes (nouvelles ; rôle de service ; fonctions Deno pappers et cartes à redéployer ensuite).
--   8. D2-16 : supprimer_fiche refuse (« verrou ») de supprimer un bâtiment dont un locataire est tenu par une autre
--      session, et efface les verrous des locataires supprimés avec lui.
--   9. D2-10 : le refus « version » d'enregistrer_batiment et d'enregistrer_locataire rend aussi « version » (celle de la
--      ligne au serveur) et « maj_session » (la session qui l'a écrite).
--  10. D1-06 : transferer_fiches refuse (« verrou ») si le bâtiment ou un locataire homonyme de la destination est tenu
--      par une autre session.
--  11. D2-18 : enregistrer_fiches (nouvelle) : toutes les fiches d'un enregistrement en une transaction, tout ou rien.
--  12. Droits du lot D, puis rechargement du schéma (un seul notify, à la fin du fichier).
--
-- Vérification :
--   select grantee from information_schema.routine_privileges where routine_name = 'detenteur_autre';  → plus d'« authenticated »
--   select pg_get_functiondef('public.documenter(uuid, text, bigint, text, text)'::regprocedure) like '%empreinte refusée%';  → true
--   select pg_get_functiondef('public.supprimer_document(uuid, text, text)'::regprocedure) like '%admin seulement%';  → true
--   select pg_get_functiondef('public.enregistrer_reglages(uuid, int, jsonb, text, int)'::regprocedure) like '%is distinct from p_version%';  → true
--   select pg_get_functiondef('public.changer_acces(uuid, uuid, uuid[])'::regprocedure) like '%''refus'', ''membre''%';  → true
--   select pg_get_functiondef('public.creer_entreprise_import(text)'::regprocedure) like '%order by (m.role%';  → true
--   select pg_get_functiondef('public.supprimer_fiche(uuid, text, text, text)'::regprocedure) like '%lo.batiment_id = p_id%';  → true
--   select pg_get_functiondef('public.enregistrer_batiment(uuid, text, int, jsonb, text, int)'::regprocedure) like '%''maj_session'',%';  → true
--   select pg_get_functiondef('public.enregistrer_locataire(uuid, text, text, int, int, jsonb, text, int)'::regprocedure) like '%''maj_session'',%';  → true
--   select pg_get_functiondef('public.transferer_fiches(uuid, uuid, jsonb, jsonb, boolean, jsonb, text, jsonb, int, int)'::regprocedure) like '%detenteur_autre(p_vers%';  → true
--   select proname, pg_get_function_identity_arguments(oid) from pg_proc where proname = 'enregistrer_fiches';
--   → une ligne : p_entreprise uuid, p_ops jsonb, p_session text, p_modele integer
--   select grantee from information_schema.routine_privileges where routine_name = 'enregistrer_fiches';
--   → « authenticated » (avec postgres et service_role), jamais « anon » ni « PUBLIC »

-- ── 1. D1-08 : detenteur_autre, fonction interne ──────────────────────────────────────────────────────
-- Accordée à « authenticated » par l'étape 4 et absente des révocations de l'étape 16c4 : tout compte connecté lisait la
-- ligne de verrou d'une fiche de n'importe quelle entreprise (qui la tient, avec son e-mail). CIPL ne l'appelle pas : seules
-- les fonctions « security definer » (enregistrer_*, supprimer_fiche, transferer_fiches) s'en servent, sans ce droit.
revoke all on function public.detenteur_autre(uuid, text, text, text) from public, anon, authenticated;

-- ── 2. D1-09, D1-10 : documents (recopiées d'etape16c) ────────────────────────────────────────────────
create or replace function public.documenter(p_entreprise uuid, p_chemin text, p_taille bigint, p_empreinte text, p_session text)
returns void language plpgsql security definer set search_path = public as $$
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  if not public.chemin_document_valide(p_chemin) then raise exception 'chemin refusé'; end if;
  -- Revue 17 (D1-09) : le logo des factures est réservé à l'admin, le fichier comme le réglage logoPath : un membre
  -- « utilisateur » ne le remplace plus pour tous les postes de l'entreprise.
  if p_chemin like 'Logo/%' and not public.est_admin(p_entreprise) then raise exception 'admin seulement'; end if;
  -- Revue 17 (D1-10) : l'empreinte sert d'adresse de stockage et de nom de fichier temporaire sur chaque poste :
  -- 64 hexadécimaux (SHA-256), rien d'autre.
  if coalesce(p_empreinte, '') !~ '^[0-9a-f]{64}$' then raise exception 'empreinte refusée'; end if;
  insert into public.documents as d (entreprise_id, chemin, taille, empreinte, maj_par, maj_session, maj_le, supprime_le)
  values (p_entreprise, p_chemin, p_taille, p_empreinte, public.mon_nom(), p_session, now(), null)
  on conflict (entreprise_id, chemin) do update
     set taille = excluded.taille, empreinte = excluded.empreinte, maj_par = excluded.maj_par,
         maj_session = excluded.maj_session, maj_le = now(), supprime_le = null;
end $$;

create or replace function public.supprimer_document(p_entreprise uuid, p_chemin text, p_session text)
returns void language plpgsql security definer set search_path = public as $$
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  if not public.chemin_document_valide(p_chemin) then raise exception 'chemin refusé'; end if;
  -- Revue 17 (D1-09) : le logo ne se supprime pas non plus depuis un poste « utilisateur ».
  if p_chemin like 'Logo/%' and not public.est_admin(p_entreprise) then raise exception 'admin seulement'; end if;
  update public.documents
     set supprime_le = now(), maj_par = public.mon_nom(), maj_session = p_session, maj_le = now()
   where entreprise_id = p_entreprise and chemin = p_chemin and supprime_le is null;
end $$;

-- ── 3. D1-13 : enregistrer_reglages (recopiée d'etape17a) ─────────────────────────────────────────────
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
    -- Revue 17 (D1-13) : la version d'abord. Une copie périmée (RIB, logo ou liste de charges changés par l'admin pendant
    -- que la fenêtre Réglages était ouverte ailleurs) reçoit « version », donc un rechargement, et non « admin ».
    if (select version from public.reglages where entreprise_id = p_entreprise) is distinct from p_version then
      return json_build_object('ok', false, 'refus', 'version', 'nom',
        (select maj_par from public.reglages where entreprise_id = p_entreprise));
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

-- ── 4. D3-15, D1-14 : membres et accès ────────────────────────────────────────────────────────────────
-- Les droits d'admin s'exercent entreprise par entreprise (est_admin = rôle admin ET ligne acces) ; la garde « dernier
-- admin » comptait les admins du groupe sans regarder leurs accès : une SCI pouvait rester sans personne pour l'ouvrir.
-- La même requête dans les trois fonctions : une entreprise du groupe où p_user a accès (et la perd) et où aucun autre
-- admin du groupe n'a accès → refus « dernier_admin » avec son nom.

-- Retirer un membre : sa licence se libère, il perd l'accès à toutes les entreprises du groupe. (Recopiée d'etape17b.)
create or replace function public.retirer_membre(p_entreprise uuid, p_user uuid)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise); n text;
begin
  if p_user = auth.uid() then return json_build_object('ok', false, 'refus', 'soi_meme'); end if;
  if exists (select 1 from public.membres where groupe_id = g and user_id = p_user and role = 'admin')
     and (select count(*) from public.membres where groupe_id = g and role = 'admin') <= 1 then
    return json_build_object('ok', false, 'refus', 'dernier_admin');
  end if;
  -- Revue 17 (D3-15) : le dernier admin se compte par entreprise, pas par groupe.
  if exists (select 1 from public.membres where groupe_id = g and user_id = p_user and role = 'admin') then
    select e.nom into n from public.entreprises e
     where e.groupe_id = g
       and exists (select 1 from public.acces a where a.entreprise_id = e.id and a.user_id = p_user)
       and not exists (select 1 from public.acces a join public.membres m on m.groupe_id = g and m.user_id = a.user_id
                        where a.entreprise_id = e.id and m.role = 'admin' and a.user_id <> p_user)
     order by e.nom limit 1;
    if n is not null then return json_build_object('ok', false, 'refus', 'dernier_admin', 'nom', n); end if;
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

-- Rôle d'un membre. Le dernier admin ne peut pas être rétrogradé (décision du 11/10). (Recopiée d'etape9b.)
create or replace function public.changer_role(p_entreprise uuid, p_user uuid, p_role text)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise); n text;
begin
  if p_role not in ('admin', 'utilisateur') then raise exception 'rôle inconnu'; end if;
  if p_user = auth.uid() then return json_build_object('ok', false, 'refus', 'soi_meme'); end if;
  if p_role = 'utilisateur' and exists (select 1 from public.membres where groupe_id = g and user_id = p_user and role = 'admin')
     and (select count(*) from public.membres where groupe_id = g and role = 'admin') <= 1 then
    return json_build_object('ok', false, 'refus', 'dernier_admin');
  end if;
  -- Revue 17 (D3-15) : rétrogradé, il n'est plus admin nulle part : chaque entreprise où il a accès garde un autre admin.
  if p_role = 'utilisateur' and exists (select 1 from public.membres where groupe_id = g and user_id = p_user and role = 'admin') then
    select e.nom into n from public.entreprises e
     where e.groupe_id = g
       and exists (select 1 from public.acces a where a.entreprise_id = e.id and a.user_id = p_user)
       and not exists (select 1 from public.acces a join public.membres m on m.groupe_id = g and m.user_id = a.user_id
                        where a.entreprise_id = e.id and m.role = 'admin' and a.user_id <> p_user)
     order by e.nom limit 1;
    if n is not null then return json_build_object('ok', false, 'refus', 'dernier_admin', 'nom', n); end if;
  end if;
  update public.membres set role = p_role where groupe_id = g and user_id = p_user;
  return json_build_object('ok', true);
end $$;

-- Entreprises du groupe accessibles à un membre (au moins une). (Recopiée d'etape16c4.)
create or replace function public.changer_acces(p_entreprise uuid, p_user uuid, p_entreprises uuid[])
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise); x record; n text;
begin
  if p_user = auth.uid() then return json_build_object('ok', false, 'refus', 'soi_meme'); end if;
  -- Revue 17 (D1-14) : la personne visée doit être membre du groupe ; sinon l'accès posé n'apparaissait dans aucun
  -- annuaire (membres join auth.users), consommait une licence et ne pouvait pas être retiré depuis l'écran.
  if not exists (select 1 from public.membres where groupe_id = g and user_id = p_user) then
    return json_build_object('ok', false, 'refus', 'membre');
  end if;
  if coalesce(array_length(p_entreprises, 1), 0) = 0
     or exists (select 1 from unnest(p_entreprises) y where y not in (select id from public.entreprises where groupe_id = g)) then
    return json_build_object('ok', false, 'refus', 'entreprises');
  end if;
  -- Revue 17 (D3-15) : un admin qui perd l'accès à une entreprise ne la laisse pas sans administrateur y ayant accès.
  if exists (select 1 from public.membres where groupe_id = g and user_id = p_user and role = 'admin') then
    select e.nom into n from public.entreprises e
     where e.groupe_id = g and not (e.id = any(p_entreprises))
       and exists (select 1 from public.acces a where a.entreprise_id = e.id and a.user_id = p_user)
       and not exists (select 1 from public.acces a join public.membres m on m.groupe_id = g and m.user_id = a.user_id
                        where a.entreprise_id = e.id and m.role = 'admin' and a.user_id <> p_user)
     order by e.nom limit 1;
    if n is not null then return json_build_object('ok', false, 'refus', 'dernier_admin', 'nom', n); end if;
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

-- ── 5. D1-15 : creer_entreprise_import (recopiée d'etape17b) ──────────────────────────────────────────
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

-- ── 6. Droits (identiques à l'existant) ───────────────────────────────────────────────────────────────
do $$ declare f text; begin
  foreach f in array array[
    'documenter(uuid, text, bigint, text, text)', 'supprimer_document(uuid, text, text)',
    'enregistrer_reglages(uuid, int, jsonb, text, int)',
    'retirer_membre(uuid, uuid)', 'changer_role(uuid, uuid, text)', 'changer_acces(uuid, uuid, uuid[])',
    'creer_entreprise_import(text)'] loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;

-- ── 7. D1-05, D1-10 : quotas Pappers et cartes (nouvelles fonctions, rôle de service) ─────────────────────
-- D1-05 : la lecture Pappers est réservée AVANT l'appel payant, en une seule instruction. Avant (pappers.ts) : lire n,
-- appeler Pappers, écrire « n + 1 » : des appels lancés ensemble lisaient tous le même n, passaient tous et payaient
-- tous. Ici : compteur du jour + 1 si et seulement s'il est sous p_max, en un insert … on conflict do update (verrou de
-- ligne : les appels simultanés passent un par un). Vrai si la lecture est prise, faux sinon. Rôle de service seulement.
create or replace function public.prendre_lecture_pappers(p_entreprise uuid, p_max int)
returns boolean language plpgsql security definer set search_path = public as $$
declare prise boolean;
begin
  insert into public.pappers_journal (entreprise_id, jour, n) values (p_entreprise, current_date, 1)
  on conflict (entreprise_id, jour) do update set n = pappers_journal.n + 1 where pappers_journal.n < p_max
  returning true into prise;
  return coalesce(prise, false);
end $$;
revoke all on function public.prendre_lecture_pappers(uuid, int) from public, anon, authenticated;
grant execute on function public.prendre_lecture_pappers(uuid, int) to service_role;

-- D1-10 (reste « cartes ») : même réservation pour les images Google, par jour et par entreprise. Avant : aucun plafond
-- par entreprise, le quota Google (console) est commun à tous les clients.
create table if not exists public.cartes_journal (
  entreprise_id uuid not null references public.entreprises(id) on delete cascade,
  jour          date not null,
  n             int  not null default 0,
  primary key (entreprise_id, jour)
);
-- Aucune règle d'accès : seule la fonction cartes (rôle service) y écrit, par prendre_lecture_cartes.
alter table public.cartes_journal enable row level security;
revoke all on public.cartes_journal from anon, authenticated;

create or replace function public.prendre_lecture_cartes(p_entreprise uuid, p_max int)
returns boolean language plpgsql security definer set search_path = public as $$
declare prise boolean;
begin
  insert into public.cartes_journal (entreprise_id, jour, n) values (p_entreprise, current_date, 1)
  on conflict (entreprise_id, jour) do update set n = cartes_journal.n + 1 where cartes_journal.n < p_max
  returning true into prise;
  return coalesce(prise, false);
end $$;
revoke all on function public.prendre_lecture_cartes(uuid, int) from public, anon, authenticated;
grant execute on function public.prendre_lecture_cartes(uuid, int) to service_role;

-- ══ Lot D (11/11/2026) : données et cloud (REVUE_17/D2.md, REVUE_17/D1.md) ══════════════════════════════════════════
-- Fonctions recopiées de leur DERNIÈRE définition : supprimer_fiche d'etape16c4 ; enregistrer_batiment,
-- enregistrer_locataire et transferer_fiches d'etape17a (aucune étape plus récente ne les touche). Signatures inchangées.

-- ── 8. D2-16 : supprimer un bâtiment regarde aussi les verrous de ses locataires (recopiée d'etape16c4) ────────────
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
    -- Revue 17 (D2-16, lot D) : les locataires partent avec le bâtiment (cascade) : un collègue en saisie sur l'un d'eux
    -- perdait tout au clic « Sauvegarder » (refus « supprimee »). Même règle que detenteur_autre (autre session, verrou
    -- non expiré) et même refus que pour le bâtiment ; le verrou le plus ancien est nommé.
    select vl.* into v from public.verrous vl
      join public.locataires lo on lo.entreprise_id = vl.entreprise_id and lo.id = vl.cible_id
     where vl.entreprise_id = p_entreprise and vl.cible_type = 'locataire' and lo.batiment_id = p_id
       and vl.session_id <> p_session and vl.expire_le >= now()
     order by vl.pris_le limit 1;
    if v.cible_id is not null then
      return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le);
    end if;
    -- Les verrous de ses locataires (les miens, ou expirés) : effacés AVANT la suppression, tant que la jointure les
    -- trouve ; après, ils resteraient sans fiche.
    delete from public.verrous vl using public.locataires lo
     where vl.entreprise_id = p_entreprise and vl.cible_type = 'locataire'
       and lo.entreprise_id = vl.entreprise_id and lo.id = vl.cible_id and lo.batiment_id = p_id;
    delete from public.batiments where entreprise_id = p_entreprise and id = p_id;   -- ses locataires suivent (cascade)
  else
    delete from public.locataires where entreprise_id = p_entreprise and id = p_id;
  end if;
  delete from public.verrous where entreprise_id = p_entreprise and cible_type = p_type and cible_id = p_id;
  return json_build_object('ok', true);
end $$;

-- ── 9. D2-10 : le refus « version » dit la version et la session de la ligne (recopiées d'etape17a) ────────────────
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
    -- Revue 17 (D2-10, lot D) : le refus dit aussi la version actuelle de la ligne et la session qui l'a écrite : si
    -- c'est la mienne (réponse perdue après une écriture réussie), CIPL reprend cette version et réessaie une fois.
    return json_build_object('ok', false, 'refus', 'version', 'nom',
      (select maj_par from public.batiments where entreprise_id = p_entreprise and id = p_id),
      'version', (select version from public.batiments where entreprise_id = p_entreprise and id = p_id),
      'maj_session', (select maj_session from public.batiments where entreprise_id = p_entreprise and id = p_id));
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
    -- Revue 17 (D2-10, lot D) : le refus dit aussi la version actuelle de la ligne et la session qui l'a écrite : si
    -- c'est la mienne (réponse perdue après une écriture réussie), CIPL reprend cette version et réessaie une fois.
    return json_build_object('ok', false, 'refus', 'version', 'nom',
      (select maj_par from public.locataires where entreprise_id = p_entreprise and id = p_id),
      'version', (select version from public.locataires where entreprise_id = p_entreprise and id = p_id),
      'maj_session', (select maj_session from public.locataires where entreprise_id = p_entreprise and id = p_id));
  end if;
  return json_build_object('ok', true, 'version', nv);
end $$;

-- ── 10. D1-06 : le transfert regarde aussi les verrous de la destination (recopiée d'etape17a) ─────────────────────
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
  hid text;   -- Revue 17 (D1-06, lot D) : un locataire homonyme de la destination
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
  -- Revue 17 (D1-06, lot D) : les homonymes de la destination vont être remplacés : eux aussi doivent être libres (un
  -- collègue en saisie dans la destination perdait tout). Contrôlé ici, AVANT la première écriture : un « return »
  -- n'annule rien. Même refus que pour l'origine.
  if dest_id is not null then
    if not p_seul then
      v := public.detenteur_autre(p_vers, 'batiment', dest_id, p_session);
      if v.cible_id is not null then
        return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'cible', p_batiment->>'Name');
      end if;
    end if;
    for l in select * from jsonb_array_elements(p_locataires) loop
      lid := l->>'id';
      cible := lower(trim(coalesce(l->'data'->>'Name', '')));
      for hid in select id from public.locataires
                  where entreprise_id = p_vers and batiment_id = dest_id and lower(trim(data->>'Name')) = cible and id <> lid loop
        v := public.detenteur_autre(p_vers, 'locataire', hid, p_session);
        if v.cible_id is not null then
          return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'cible', l->'data'->>'Name');
        end if;
      end loop;
    end loop;
  end if;
  -- Revue 17 (D1-06, lot D) : un locataire de la destination qui porte le MÊME identifiant (même sauvegarde importée dans
  -- les deux entreprises) est remplacé par « on conflict … do update », homonyme ou non : lui aussi doit être libre.
  for l in select * from jsonb_array_elements(p_locataires) loop
    v := public.detenteur_autre(p_vers, 'locataire', l->>'id', p_session);
    if v.cible_id is not null then
      return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'cible', l->'data'->>'Name');
    end if;
  end loop;
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

-- ── 11. D2-18 : enregistrer_fiches (nouvelle) ──────────────────────────────────────────────────────────────────────
-- Toutes les fiches d'un enregistrement (le bâtiment, ses locataires, les locataires retirés) en UNE transaction. Avant :
-- un appel par fiche ; un refus sur la deuxième laissait la première écrite (régularisation : charges « facturées » au
-- bâtiment, sans ligne de facture chez le locataire).
-- p_ops : tableau JSON, dans l'ordre d'application. Chaque élément :
--   {"type":"batiment"|"locataire"|"supprimer_locataire","id":"…","batiment":"…","ordre":N,"version":N,"data":{…}}
--   (« batiment » et « ordre » pour un locataire seulement ; ni « version » ni « data » pour une suppression).
-- Aucun contrôle n'est recopié : chaque fiche passe par sa fonction unitaire (accès, verrou d'une autre session, version,
-- modèle, fiche supprimée). Au premier refus, une exception à nous (RF001) annule tout ce que le bloc a écrit (point de
-- reprise de PL/pgSQL ; les variables gardent leur valeur) et le refus est rendu tel quel, avec « type » et « id » de la
-- fiche refusée. Toute autre exception (« accès refusé », abonnement expiré, type inconnu) remonte et annule l'appel.
-- Succès : {"ok":true,"versions":[{"type":"…","id":"…","version":N}, …]}, une entrée par fiche, dans l'ordre (version 0
-- pour une suppression) ; tableau vide : {"ok":true,"versions":[]}.
-- Refus (RIEN n'est écrit) : {"ok":false,"refus":"verrou"|"version"|"application"|"supprimee","type":"…","id":"…", …}.
create or replace function public.enregistrer_fiches(p_entreprise uuid, p_ops jsonb, p_session text, p_modele int default 0)
returns json language plpgsql security definer set search_path = public as $$
declare op jsonb; r json; refus jsonb; versions jsonb := '[]'::jsonb;
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  begin
    for op in select e.value from jsonb_array_elements(coalesce(p_ops, '[]'::jsonb)) with ordinality as e(value, n) order by e.n loop
      if op->>'type' = 'batiment' then
        r := public.enregistrer_batiment(p_entreprise, op->>'id', (op->>'version')::int, op->'data', p_session, p_modele);
      elsif op->>'type' = 'locataire' then
        r := public.enregistrer_locataire(p_entreprise, op->>'id', op->>'batiment', coalesce((op->>'ordre')::int, 0),
                                          (op->>'version')::int, op->'data', p_session, p_modele);
      elsif op->>'type' = 'supprimer_locataire' then
        r := public.supprimer_fiche(p_entreprise, 'locataire', op->>'id', p_session);
      else
        raise exception 'type inconnu';
      end if;
      if coalesce((r->>'ok')::boolean, false) is not true then
        refus := r::jsonb || jsonb_build_object('type', op->>'type', 'id', op->>'id');
        raise exception 'refus' using errcode = 'RF001';
      end if;
      versions := versions || jsonb_build_object('type', op->>'type', 'id', op->>'id', 'version', coalesce((r->>'version')::int, 0));
    end loop;
  exception when sqlstate 'RF001' then
    return refus::json;
  end;
  return json_build_object('ok', true, 'versions', versions);
end $$;

-- ── 12. Droits du lot D (identiques à l'existant ; enregistrer_fiches : comme enregistrer_batiment) ────────────────
do $$ declare f text; begin
  foreach f in array array[
    'supprimer_fiche(uuid, text, text, text)',
    'enregistrer_batiment(uuid, text, int, jsonb, text, int)',
    'enregistrer_locataire(uuid, text, text, int, int, jsonb, text, int)',
    'transferer_fiches(uuid, uuid, jsonb, jsonb, boolean, jsonb, text, jsonb, int, int)',
    'enregistrer_fiches(uuid, jsonb, text, int)'] loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;

notify pgrst, 'reload schema';

-- Lot D : fait le 11/11/2026 (sections 8 à 12)
