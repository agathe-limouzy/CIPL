  -- ════════════════════════════════════════════════════════════════════════════
  -- CIPL — étape 13 : inscription libre-service (décisions du 23/10)
  -- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape10b).
  -- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §14 et le parcours AO.
  -- ════════════════════════════════════════════════════════════════════════════
  
  -- « Créer mon compte », une fois l'adresse prouvée par le code de l'e-mail (la session est celle du
  -- compte) : groupe en essai de 14 jours, l'entreprise (3 licences), l'admin, ses Réglages de départ.
  -- Un seul groupe par adresse e-mail : une adresse déjà membre d'un groupe (titulaire ou invitée)
  -- n'en crée pas d'autre ; une autre adresse le peut (décision du 23/10).
  -- Réponse : {ok, id, nom} ou {ok: false, refus: 'deja_membre' (nom du groupe) | 'siret' | 'deja_sur_cipl' | 'nom'}.
  create or replace function public.inscrire(p_type text, p_nom text, p_siret text, p_mon_nom text, p_reglages jsonb)
  returns json language plpgsql security definer set search_path = public as $$
  declare g uuid; e uuid; deja text;
          s text := nullif(regexp_replace(coalesce(p_siret, ''), '\s', '', 'g'), '');
          n text := trim(coalesce(p_nom, ''));
  begin
    if auth.uid() is null then raise exception 'non connecté'; end if;
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
  
  revoke all on function public.inscrire(text, text, text, text, jsonb) from public, anon;
  grant execute on function public.inscrire(text, text, text, text, jsonb) to authenticated;
  
  -- Retour du 25/10 : « Créer mon compte » refuse une adresse déjà membre d'un groupe AVANT d'envoyer
  -- le code. Appelée sans être connecté : elle ne dit que oui ou non, jamais le groupe. (La création de
  -- compte de Supabase dit déjà si une adresse existe.) `inscrire` garde son propre contrôle.
  create or replace function public.adresse_deja_membre(p_email text)
  returns boolean language sql stable security definer set search_path = public as $$
    select exists (
      select 1 from auth.users u
       where lower(u.email) = lower(trim(coalesce(p_email, '')))
         and (exists (select 1 from public.membres m where m.user_id = u.id)
              or exists (select 1 from public.groupes g where g.titulaire = u.id)));
  $$;
  
  revoke all on function public.adresse_deja_membre(text) from public;
  grant execute on function public.adresse_deja_membre(text) to anon, authenticated;
  
  -- Retour du 25/10 (AO8) : de même, une société déjà sur CIPL est refusée avant l'envoi du code.
  -- Oui ou non seulement ; `inscrire` garde son propre contrôle.
  create or replace function public.siret_deja_sur_cipl(p_siret text)
  returns boolean language sql stable security definer set search_path = public as $$
    select exists (select 1 from public.entreprises
                    where siret = nullif(regexp_replace(coalesce(p_siret, ''), '\s', '', 'g'), ''));
  $$;
  
  revoke all on function public.siret_deja_sur_cipl(text) from public;
  grant execute on function public.siret_deja_sur_cipl(text) to anon, authenticated;
