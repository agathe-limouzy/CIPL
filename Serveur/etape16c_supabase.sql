-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 16c : corrections de la revue complète du code (31/10/2026), côté serveur.
-- À exécuter dans Supabase › SQL Editor, après les étapes précédentes. Relançable sans risque.
-- Détail des défauts : REVUE_16C/SYNTHESE.md.
-- ════════════════════════════════════════════════════════════════════════════

-- ── D1-01 : le chemin d'un document est contrôlé ────────────────────────────
-- Chaque PC de l'entreprise écrit les documents de la table `documents` dans son dossier, au `chemin` donné.
-- Un chemin complet ou contenant « .. » sortait de ce dossier. Seuls les dossiers partagés (Batiment/, Logo/,
-- « Anciens documents/ », comme SyncDocuments.Partage côté CIPL), sans segment « . » ou « .. », ni « \ » ni « : ».
-- Pas de test du caractère nul : un texte Postgres ne peut pas en contenir, et le fabriquer (chr de 0) lève « null
-- character not permitted » à chaque appel (constaté le 01/11 : plus aucun document enregistré).
create or replace function public.chemin_document_valide(p_chemin text)
returns boolean language sql immutable set search_path = public as $$
  select p_chemin is not null
     and p_chemin ~ '^(Batiment|Logo|Anciens documents)/'
     and p_chemin !~ '(^|/)\.{1,2}(/|$)'
     and p_chemin !~ '//'
     and p_chemin !~ '[\\:]'
$$;
revoke all on function public.chemin_document_valide(text) from public, anon, authenticated;

create or replace function public.documenter(p_entreprise uuid, p_chemin text, p_taille bigint, p_empreinte text, p_session text)
returns void language plpgsql security definer set search_path = public as $$
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  if not public.chemin_document_valide(p_chemin) then raise exception 'chemin refusé'; end if;
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
  update public.documents
     set supprime_le = now(), maj_par = public.mon_nom(), maj_session = p_session, maj_le = now()
   where entreprise_id = p_entreprise and chemin = p_chemin and supprime_le is null;
end $$;

-- ── D5-02 : une facture n'est transmise qu'une fois à la plateforme ─────────
-- Réservée par la fonction « facture-electronique » avant l'envoi (la première réservation gagne) ; garde
-- l'identifiant de Super PDP : un second envoi de la même facture (autre poste, réessai) reçoit cet identifiant
-- au lieu de la transmettre une deuxième fois. Serveur seul (aucune règle RLS).
create table if not exists public.plateformes_envois (
  entreprise_id uuid        not null references public.entreprises(id) on delete cascade,
  cle           text        not null,
  facture_id    text,
  cree_le       timestamptz not null default now(),
  primary key (entreprise_id, cle)
);
alter table public.plateformes_envois enable row level security;
