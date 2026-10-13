-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 2 : comptes, entreprises, bâtiments et locataires sur le cloud
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run.
-- Relançable sans risque (« if not exists », « create or replace »).
-- Voir CONCEPTION_MULTIUTILISATEUR.md §5 et §13.
-- ════════════════════════════════════════════════════════════════════════════

-- ── Groupe = ce qui paie (une facture), entreprises = les SCI ou patrimoines ──
create table if not exists public.groupes (
  id            uuid primary key default gen_random_uuid(),
  nom           text not null,
  titulaire     uuid references auth.users(id),
  licences_max  int  not null default 3,
  statut        text not null default 'essai',          -- essai | actif | refuse | expire
  fin_essai     date not null default (current_date + 14),
  cree_le       timestamptz not null default now()
);

create table if not exists public.entreprises (
  id        uuid primary key default gen_random_uuid(),
  groupe_id uuid not null references public.groupes(id) on delete cascade,
  nom       text not null,
  type      text not null default 'societe' check (type in ('societe', 'particulier')),
  siret     text,
  cree_le   timestamptz not null default now()
);

-- Une personne = une licence dans le groupe (quel que soit le nombre d'entreprises).
create table if not exists public.membres (
  groupe_id   uuid not null references public.groupes(id) on delete cascade,
  user_id     uuid not null references auth.users(id) on delete cascade,
  role        text not null default 'utilisateur' check (role in ('admin', 'utilisateur')),
  nom_affiche text,
  primary key (groupe_id, user_id)
);

-- À quelles entreprises du groupe chaque membre a accès.
create table if not exists public.acces (
  entreprise_id uuid not null references public.entreprises(id) on delete cascade,
  user_id       uuid not null references auth.users(id) on delete cascade,
  primary key (entreprise_id, user_id)
);

-- ── Les données de CIPL : le JSON actuel, coupé en deux niveaux ───────────────
create table if not exists public.batiments (
  entreprise_id uuid not null references public.entreprises(id) on delete cascade,
  id            text not null,                 -- identifiant CIPL du bâtiment
  data          jsonb not null,                -- le bâtiment SANS ses locataires
  version       int  not null default 1,
  maj_par       text,
  maj_le        timestamptz not null default now(),
  primary key (entreprise_id, id)
);

create table if not exists public.locataires (
  entreprise_id uuid not null,
  id            text not null,                 -- identifiant CIPL du locataire
  batiment_id   text not null,
  ordre         int  not null default 0,       -- ordre des onglets dans le bâtiment
  data          jsonb not null,
  version       int  not null default 1,
  maj_par       text,
  maj_le        timestamptz not null default now(),
  primary key (entreprise_id, id),
  foreign key (entreprise_id, batiment_id) references public.batiments(entreprise_id, id) on delete cascade
);

-- ── Qui a le droit de quoi ──────────────────────────────────────────────────
create or replace function public.mes_acces()
returns setof uuid language sql stable security definer set search_path = public as $$
  select entreprise_id from public.acces where user_id = auth.uid();
$$;

create or replace function public.est_admin(p_entreprise uuid)
returns boolean language sql stable security definer set search_path = public as $$
  select exists (
    select 1 from public.entreprises e
      join public.membres m on m.groupe_id = e.groupe_id
     where e.id = p_entreprise and m.user_id = auth.uid() and m.role = 'admin')
  and exists (select 1 from public.acces a where a.entreprise_id = p_entreprise and a.user_id = auth.uid());
$$;

alter table public.groupes     enable row level security;
alter table public.entreprises enable row level security;
alter table public.membres     enable row level security;
alter table public.acces       enable row level security;
alter table public.batiments   enable row level security;
alter table public.locataires  enable row level security;

drop policy if exists groupes_lecture on public.groupes;
create policy groupes_lecture on public.groupes for select to authenticated
  using (id in (select groupe_id from public.membres where user_id = auth.uid()));

drop policy if exists entreprises_lecture on public.entreprises;
create policy entreprises_lecture on public.entreprises for select to authenticated
  using (id in (select public.mes_acces()));

drop policy if exists membres_lecture on public.membres;
create policy membres_lecture on public.membres for select to authenticated
  using (user_id = auth.uid());

drop policy if exists acces_lecture on public.acces;
create policy acces_lecture on public.acces for select to authenticated
  using (user_id = auth.uid());

drop policy if exists batiments_lecture on public.batiments;
create policy batiments_lecture on public.batiments for select to authenticated
  using (entreprise_id in (select public.mes_acces()));
drop policy if exists batiments_import on public.batiments;
create policy batiments_import on public.batiments for insert to authenticated
  with check (public.est_admin(entreprise_id));

drop policy if exists locataires_lecture on public.locataires;
create policy locataires_lecture on public.locataires for select to authenticated
  using (entreprise_id in (select public.mes_acces()));
drop policy if exists locataires_import on public.locataires;
create policy locataires_import on public.locataires for insert to authenticated
  with check (public.est_admin(entreprise_id));
-- Pas encore de modification ni de suppression : CIPL est en lecture seule jusqu'à l'étape 4.

-- ── Fonctions appelées par CIPL ─────────────────────────────────────────────
-- Les entreprises auxquelles j'ai accès, avec mon rôle et le nombre de bâtiments.
create or replace function public.mes_entreprises()
returns table (id uuid, nom text, type text, role text, nb_batiments bigint)
language sql stable security definer set search_path = public as $$
  select e.id, e.nom, e.type, m.role,
         (select count(*) from public.batiments b where b.entreprise_id = e.id)
    from public.acces a
    join public.entreprises e on e.id = a.entreprise_id
    join public.membres m on m.groupe_id = e.groupe_id and m.user_id = a.user_id
   where a.user_id = auth.uid()
   order by e.nom;
$$;

-- Import d'une entreprise de ce PC : crée (si besoin) le groupe dont je suis titulaire,
-- puis l'entreprise, et me donne le rôle admin et l'accès. Renvoie l'id de l'entreprise.
create or replace function public.creer_entreprise_import(p_nom text)
returns uuid language plpgsql security definer set search_path = public as $$
declare g uuid; e uuid;
begin
  if auth.uid() is null then raise exception 'non connecté'; end if;
  if coalesce(trim(p_nom), '') = '' then raise exception 'nom vide'; end if;

  select id into g from public.groupes where titulaire = auth.uid() order by cree_le limit 1;
  if g is null then
    insert into public.groupes (nom, titulaire) values (trim(p_nom), auth.uid()) returning id into g;
  end if;
  insert into public.membres (groupe_id, user_id, role) values (g, auth.uid(), 'admin')
    on conflict (groupe_id, user_id) do update set role = 'admin';

  insert into public.entreprises (groupe_id, nom) values (g, trim(p_nom)) returning id into e;
  insert into public.acces (entreprise_id, user_id) values (e, auth.uid());
  return e;
end $$;

revoke all on function public.mes_entreprises() from public, anon;
revoke all on function public.creer_entreprise_import(text) from public, anon;
grant execute on function public.mes_entreprises() to authenticated;
grant execute on function public.creer_entreprise_import(text) to authenticated;

-- ════════════════════════════════════════════════════════════════════════════
-- AJOUTER UN MEMBRE (en attendant les invitations) — à lancer APRÈS l'import.
-- 1. Remplacer l'e-mail et le nom de l'entreprise ci-dessous.
-- 2. Sélectionner le bloc « do $$ … $$; » (sans les « -- ») et Run.
-- do $$ declare u uuid; e uuid; begin
--   select id into u from auth.users where email = 'deuxieme.compte@exemple.fr';
--   select id into e from public.entreprises where nom = 'Démo CIPL' order by cree_le limit 1;   -- nom exact, accents compris
--   if u is null or e is null then raise exception 'e-mail ou entreprise introuvable'; end if;
--   insert into public.membres (groupe_id, user_id, role)
--     select groupe_id, u, 'utilisateur' from public.entreprises where id = e
--     on conflict (groupe_id, user_id) do nothing;
--   insert into public.acces (entreprise_id, user_id) values (e, u) on conflict do nothing;
-- end $$;
-- ════════════════════════════════════════════════════════════════════════════
