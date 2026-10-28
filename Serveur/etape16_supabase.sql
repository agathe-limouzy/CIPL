-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — Réglages réorganisés (27/10) : le SIRET de l'entreprise dans l'onglet Entreprise,
-- modifiable par l'admin. À coller UNE fois dans Supabase › SQL Editor › New query › Run
-- (après etape13). Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §16.
-- ════════════════════════════════════════════════════════════════════════════

-- Même liste qu'à l'étape 6b, plus le SIRET (le type de retour change : on la recrée).
-- Les versions de CIPL déjà installées ignorent ce champ de plus.
drop function if exists public.mes_entreprises();
create function public.mes_entreprises()
returns table (id uuid, nom text, type text, role text, nb_batiments bigint, mode_documents text, siret text)
language sql stable security definer set search_path = public as $$
  select e.id, e.nom, e.type, m.role,
         (select count(*) from public.batiments b where b.entreprise_id = e.id),
         e.mode_documents, e.siret
    from public.acces a
    join public.entreprises e on e.id = a.entreprise_id
    join public.membres m on m.groupe_id = e.groupe_id and m.user_id = a.user_id
   where a.user_id = auth.uid()
   order by e.nom;
$$;

-- Admin seulement. Mêmes règles qu'à l'ajout d'une entreprise : 14 chiffres, pas déjà sur CIPL
-- (l'annuaire est interrogé par CIPL avant l'appel). Un particulier n'a pas de SIRET.
-- Réponse : {ok, siret} ou {ok: false, refus: 'siret' | 'deja_sur_cipl' | 'particulier' | 'abonnement'}.
create or replace function public.changer_siret(p_entreprise uuid, p_siret text)
returns json language plpgsql security definer set search_path = public as $$
declare g uuid := public.groupe_admin(p_entreprise);
        s text := nullif(regexp_replace(coalesce(p_siret, ''), '\s', '', 'g'), '');
begin
  if not public.ecriture_permise(g) then return json_build_object('ok', false, 'refus', 'abonnement'); end if;
  if (select type from public.entreprises where id = p_entreprise) = 'particulier' then
    return json_build_object('ok', false, 'refus', 'particulier');
  end if;
  if s is null or s !~ '^[0-9]{14}$' then return json_build_object('ok', false, 'refus', 'siret'); end if;
  if exists (select 1 from public.entreprises where siret = s and id <> p_entreprise) then
    return json_build_object('ok', false, 'refus', 'deja_sur_cipl');
  end if;
  update public.entreprises set siret = s where id = p_entreprise;
  return json_build_object('ok', true, 'siret', s);
end $$;

revoke all on function public.mes_entreprises() from public, anon;
grant execute on function public.mes_entreprises() to authenticated;
revoke all on function public.changer_siret(uuid, text) from public, anon;
grant execute on function public.changer_siret(uuid, text) to authenticated;
