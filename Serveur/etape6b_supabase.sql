-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 6b : rangement des documents choisi par l'admin (Dans CIPL / Drive partagé)
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape6).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §13 (Dossier des documents).
-- ════════════════════════════════════════════════════════════════════════════

alter table public.entreprises add column if not exists mode_documents text not null default 'cipl';
do $$ begin
  alter table public.entreprises add constraint entreprises_mode_documents check (mode_documents in ('cipl', 'drive'));
exception when duplicate_object then null; end $$;

-- Même liste qu'à l'étape 2, plus le mode de rangement (le type de retour change : on la recrée).
drop function if exists public.mes_entreprises();
create function public.mes_entreprises()
returns table (id uuid, nom text, type text, role text, nb_batiments bigint, mode_documents text)
language sql stable security definer set search_path = public as $$
  select e.id, e.nom, e.type, m.role,
         (select count(*) from public.batiments b where b.entreprise_id = e.id),
         e.mode_documents
    from public.acces a
    join public.entreprises e on e.id = a.entreprise_id
    join public.membres m on m.groupe_id = e.groupe_id and m.user_id = a.user_id
   where a.user_id = auth.uid()
   order by e.nom;
$$;

-- Admin seulement. Les collègues basculent à leur prochain démarrage de CIPL.
create or replace function public.definir_mode_documents(p_entreprise uuid, p_mode text)
returns json language plpgsql security definer set search_path = public as $$
begin
  if not public.est_admin(p_entreprise) then
    return json_build_object('ok', false, 'refus', 'admin');
  end if;
  if p_mode not in ('cipl', 'drive') then raise exception 'mode inconnu : %', p_mode; end if;
  update public.entreprises set mode_documents = p_mode where id = p_entreprise;
  return json_build_object('ok', true);
end $$;

revoke all on function public.mes_entreprises() from public, anon;
grant execute on function public.mes_entreprises() to authenticated;
revoke all on function public.definir_mode_documents(uuid, text) from public, anon;
grant execute on function public.definir_mode_documents(uuid, text) to authenticated;
