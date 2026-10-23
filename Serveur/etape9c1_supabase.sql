-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 9c1 : abonnement (essai, actif, expiré), lecture seule à l'expiration,
-- ajout d'une entreprise au groupe. Le paiement (Stripe) arrive à l'étape 9c2.
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape9b).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §13 (décisions du 11/10 et du 19/10).
--
-- ATTENTION : un groupe dont l'essai de 14 jours est fini passe aussitôt en lecture seule.
-- Pour garder le groupe de développement modifiable : voir PARCOURS_TEST_CIPL.md, parcours AG.
-- ════════════════════════════════════════════════════════════════════════════

-- Le groupe peut-il écrire ? Actif, paiement refusé (Stripe réessaie), ou essai pas encore fini.
create or replace function public.ecriture_permise(p_groupe uuid)
returns boolean language sql stable security definer set search_path = public as $$
  select coalesce((select statut in ('actif', 'refuse') or (statut = 'essai' and fin_essai >= current_date)
                     from public.groupes where id = p_groupe), false);
$$;

-- Toute écriture dans les données d'une entreprise d'un groupe expiré est refusée, quelle que
-- soit la fonction qui écrit (enregistrer_*, supprimer_fiche, documenter, reglages, secrets).
-- Verrous : seulement à la prise (le signal de vie et la libération passent toujours).
create or replace function public.refuser_si_expire()
returns trigger language plpgsql security definer set search_path = public as $$
declare g uuid;
begin
  if tg_op = 'DELETE' then
    select groupe_id into g from public.entreprises where id = old.entreprise_id;
  else
    select groupe_id into g from public.entreprises where id = new.entreprise_id;
  end if;
  -- g nul : l'entreprise est en train d'être supprimée (cascade) ; on laisse faire.
  if g is not null and not public.ecriture_permise(g) then
    raise exception 'abonnement expiré : lecture seule';
  end if;
  if tg_op = 'DELETE' then return old; end if;
  return new;
end $$;

do $$ declare t text; begin
  foreach t in array array['batiments', 'locataires', 'documents', 'reglages', 'secrets'] loop
    execute format('drop trigger if exists abonnement_expire on public.%I', t);
    execute format('create trigger abonnement_expire before insert or update or delete on public.%I '
                   'for each row execute function public.refuser_si_expire()', t);
  end loop;
  drop trigger if exists abonnement_expire on public.verrous;
  create trigger abonnement_expire before insert on public.verrous
    for each row execute function public.refuser_si_expire();
end $$;

-- Ce qu'affichent Réglages › Abonnement et les bandeaux (tout membre de l'entreprise) :
-- la date du serveur sert au compte des jours restants.
create or replace function public.abonnement(p_entreprise uuid)
returns json language plpgsql stable security definer set search_path = public as $$
declare g uuid;
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  select groupe_id into g from public.entreprises where id = p_entreprise;
  return (select json_build_object(
    'statut', gr.statut, 'fin_essai', gr.fin_essai, 'aujourdhui', current_date,
    'licences_max', gr.licences_max, 'licences_utilisees', public.licences_utilisees(g),
    'entreprises', coalesce((select json_agg(json_build_object('id', e.id, 'nom', e.nom) order by e.cree_le)
                               from public.entreprises e where e.groupe_id = g), '[]'::json),
    'admin', (select coalesce(nullif(m.nom_affiche, ''), u.email)
                from public.membres m join auth.users u on u.id = m.user_id
               where m.groupe_id = g and m.role = 'admin'
               order by (m.user_id = gr.titulaire) desc limit 1))
    from public.groupes gr where gr.id = g);
end $$;

-- Réglages › Abonnement › « Ajouter une entreprise » (admin) : même groupe, donc même facture et
-- remise ; l'admin y a accès. SIRET contrôlé sur sa forme (14 chiffres) et unique sur CIPL ;
-- la vérification Pappers viendra avec les fonctions serveur de l'étape 9c2.
-- Réponse : {ok, id, nom} ou {ok: false, refus: 'nom' | 'siret' | 'deja_sur_cipl' | 'abonnement'}.
create or replace function public.ajouter_entreprise(p_entreprise uuid, p_nom text, p_type text, p_siret text)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise);
        s text := nullif(regexp_replace(coalesce(p_siret, ''), '\s', '', 'g'), '');
        n text := trim(coalesce(p_nom, ''));
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
  insert into public.entreprises (groupe_id, nom, type, siret) values (g, n, p_type, s) returning id into e;
  insert into public.acces (entreprise_id, user_id) values (e, auth.uid());
  return json_build_object('ok', true, 'id', e, 'nom', n);
end $$;

do $$ declare f text; begin
  foreach f in array array[
    'ecriture_permise(uuid)', 'refuser_si_expire()', 'abonnement(uuid)', 'ajouter_entreprise(uuid, text, text, text)']
  loop
    execute format('revoke all on function public.%s from public, anon', f);
  end loop;
  grant execute on function public.abonnement(uuid) to authenticated;
  grant execute on function public.ajouter_entreprise(uuid, text, text, text) to authenticated;
end $$;
