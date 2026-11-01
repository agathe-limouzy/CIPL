-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 16c, lot 2 : défauts majeurs « serveur, sécurité et argent » de la revue (01/11/2026).
-- À exécuter dans Supabase › SQL Editor, après etape16c_supabase.sql. Relançable sans risque.
-- Détail des défauts : REVUE_16C/SYNTHESE.md (D1-03, D1-04, D3-06). Les fonctions serveur
-- abonnement-stripe, stripe-webhook et facture-electronique sont à redéployer en même temps.
-- ════════════════════════════════════════════════════════════════════════════

-- ── D1-03 : RIB, logo et suppression d'une liste de charges réservés à l'admin, par le serveur ──
-- Décision du 11/10, jusqu'ici appliquée par l'écran seulement : un membre « utilisateur » qui appelait
-- enregistrer_reglages directement changeait l'IBAN imprimé sur toutes les factures. Même corps qu'à
-- l'étape 9a, plus le contrôle : pour un non-admin, ribs, logoPath et sansLogo doivent être inchangés,
-- et aucune liste de charges ne doit disparaître (ajouter ou renommer reste permis).
create or replace function public.enregistrer_reglages(p_entreprise uuid, p_version int, p_data jsonb, p_session text)
returns json language plpgsql security definer set search_path = public as $$
declare nv int; nom text := public.mon_nom(); avant jsonb;
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  if p_version = 0 then
    if not public.est_admin(p_entreprise) then return json_build_object('ok', false, 'refus', 'admin'); end if;
    insert into public.reglages (entreprise_id, data, version, maj_par, maj_session)
    values (p_entreprise, p_data, 1, nom, p_session) on conflict do nothing returning version into nv;
  else
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
    update public.reglages set data = p_data, version = version + 1, maj_par = nom, maj_session = p_session, maj_le = now()
     where entreprise_id = p_entreprise and version = p_version returning version into nv;
  end if;
  if nv is null then
    return json_build_object('ok', false, 'refus', 'version', 'nom',
      (select maj_par from public.reglages where entreprise_id = p_entreprise));
  end if;
  return json_build_object('ok', true, 'version', nv);
end $$;

-- ── D1-04 : l'import d'une entreprise de ce PC suit les règles du groupe ─────
-- Fonction de l'étape 2, restée sans les règles d'inscrire et d'ajouter_entreprise : un compte dont le
-- groupe est expiré, ou un simple membre, créait une entreprise (3 licences, modifiable) hors de tout
-- abonnement, et l'import promouvait n'importe quel membre admin. Désormais : un membre non admin est
-- refusé ; un groupe existant doit pouvoir écrire (abonnement) et l'entreprise reçoit 3 licences en essai,
-- 1 sinon (comme ajouter_entreprise) ; sans groupe, il est créé comme avant.
create or replace function public.creer_entreprise_import(p_nom text)
returns uuid language plpgsql security definer set search_path = public as $$
declare g uuid; e uuid; r text; st text;
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
    insert into public.groupes (nom, titulaire) values (trim(p_nom), auth.uid()) returning id into g;
  end if;
  insert into public.membres (groupe_id, user_id, role) values (g, auth.uid(), 'admin') on conflict (groupe_id, user_id) do nothing;

  select statut into st from public.groupes where id = g;
  insert into public.entreprises (groupe_id, nom, licences_max)
  values (g, trim(p_nom), case when st = 'essai' then 3 else 1 end) returning id into e;
  insert into public.acces (entreprise_id, user_id) values (e, auth.uid());
  return e;
end $$;

-- ── D3-06 : « Créer mon compte » exige une adresse prouvée par le code reçu ──
-- « Confirm email » est décoché (décision 9b) : un compte créé directement par l'API (sans code) obtenait
-- une session, et inscrire lui ouvrait un groupe d'essai au nom de n'importe quelle adresse (e-mails de
-- l'étape 14 envoyés à des inconnus, adresse « squattée » pour son vrai propriétaire). Le jeton d'une
-- session dit comment elle a été ouverte (revendication amr) : otp pour le code saisi dans CIPL
-- (/auth/v1/verify, type email) ; magiclink et email/signup, autres formes d'un e-mail prouvé, sont
-- acceptées aussi ; password et oauth ne le sont pas. Même corps qu'à l'étape 13, plus ce contrôle.
create or replace function public.inscrire(p_type text, p_nom text, p_siret text, p_mon_nom text, p_reglages jsonb)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid; e uuid; deja text;
        s text := nullif(regexp_replace(coalesce(p_siret, ''), '\s', '', 'g'), '');
        n text := trim(coalesce(p_nom, ''));
        amr jsonb := coalesce(auth.jwt() -> 'amr', '[]'::jsonb);
begin
  if auth.uid() is null then raise exception 'non connecté'; end if;
  if not (amr @> '[{"method":"otp"}]'::jsonb or amr @> '[{"method":"magiclink"}]'::jsonb or amr @> '[{"method":"email/signup"}]'::jsonb) then
    raise exception 'adresse non prouvée par le code reçu';
  end if;
  if p_type not in ('societe', 'particulier') then raise exception 'type inconnu'; end if;

  select gr.nom into deja from public.groupes gr
   where gr.titulaire = auth.uid()
      or gr.id in (select groupe_id from public.membres where user_id = auth.uid())
   limit 1;
  if deja is not null then return json_build_object('ok', false, 'refus', 'deja_membre', 'nom', deja); end if;

  if p_type = 'particulier' then
    s := null;
  else
    if s is null or s !~ '^[0-9]{14}$' then return json_build_object('ok', false, 'refus', 'siret'); end if;
    if exists (select 1 from public.entreprises where siret = s) then
      return json_build_object('ok', false, 'refus', 'deja_sur_cipl');
    end if;
  end if;
  if n = '' then return json_build_object('ok', false, 'refus', 'nom'); end if;

  insert into public.groupes (nom, titulaire) values (n, auth.uid()) returning id into g;   -- statut essai, fin_essai J+14
  insert into public.membres (groupe_id, user_id, role, nom_affiche)
  values (g, auth.uid(), 'admin', nullif(trim(coalesce(p_mon_nom, '')), ''));
  insert into public.entreprises (groupe_id, nom, type, siret) values (g, n, p_type, s) returning id into e;   -- 3 licences
  insert into public.acces (entreprise_id, user_id) values (e, auth.uid());
  if p_reglages is not null then
    insert into public.reglages (entreprise_id, data, version, maj_par) values (e, p_reglages, 1, public.mon_nom());
  end if;
  return json_build_object('ok', true, 'id', e, 'nom', n);
end $$;
