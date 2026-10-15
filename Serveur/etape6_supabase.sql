-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 6a : documents partagés (factures, baux, photos, PDF de charges, logo)
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape5).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §8.
-- ════════════════════════════════════════════════════════════════════════════

-- ── Contenu : compartiment privé, un objet par contenu, nommé <entreprise_id>/<empreinte>.
--    (Le stockage refuse les noms accentués comme « Décathlon » ; et un renommage de bâtiment
--    ne renvoie ainsi aucun fichier.) Jamais modifié ni effacé par l'application.
insert into storage.buckets (id, name, public)
values ('documents', 'documents', false)
on conflict (id) do nothing;

drop policy if exists cipl_documents_lecture on storage.objects;
create policy cipl_documents_lecture on storage.objects for select to authenticated
  using (bucket_id = 'documents' and (storage.foldername(name))[1] in (select a::text from public.mes_acces() a));

drop policy if exists cipl_documents_depot on storage.objects;
create policy cipl_documents_depot on storage.objects for insert to authenticated
  with check (bucket_id = 'documents' and (storage.foldername(name))[1] in (select a::text from public.mes_acces() a));

-- ── Index : quel chemin du dossier de l'entreprise porte quel contenu ──────────
create table if not exists public.documents (
  entreprise_id uuid not null references public.entreprises(id) on delete cascade,
  chemin        text not null,              -- relatif au dossier de l'entreprise, séparateur « / »
  taille        bigint not null default 0,
  empreinte     text not null,              -- SHA-256 du contenu, en hexadécimal
  maj_par       text,
  maj_session   text,
  maj_le        timestamptz not null default now(),
  supprime_le   timestamptz,                -- rempli : supprimé (les autres le mettent en corbeille)
  primary key (entreprise_id, chemin)
);
alter table public.documents enable row level security;
drop policy if exists documents_lecture on public.documents;
create policy documents_lecture on public.documents for select to authenticated
  using (entreprise_id in (select public.mes_acces()));

create or replace function public.documenter(p_entreprise uuid, p_chemin text, p_taille bigint, p_empreinte text, p_session text)
returns void language plpgsql security definer set search_path = public as $$
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
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
  update public.documents
     set supprime_le = now(), maj_par = public.mon_nom(), maj_session = p_session, maj_le = now()
   where entreprise_id = p_entreprise and chemin = p_chemin and supprime_le is null;
end $$;

do $$ begin
  begin alter publication supabase_realtime add table public.documents; exception when duplicate_object then null; end;
end $$;

do $$ declare f text; begin
  foreach f in array array[
    'documenter(uuid, text, bigint, text, text)', 'supprimer_document(uuid, text, text)']
  loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;
