-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 10 : tournée de l'annuaire des entreprises, une fois par jour.
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape9c2).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §14 (décisions du 23/10).
--
-- CIPL interroge l'annuaire public de l'État (recherche-entreprises.api.gouv.fr, sans clé) pour
-- le résumé d'entreprise des locataires. Sur le cloud, la tournée qui rafraîchit tous les
-- locataires ne doit tourner qu'une fois par jour et par entreprise, sur un seul poste : le
-- premier qui ouvre l'entreprise dans la journée. Le serveur le note ici.
-- ════════════════════════════════════════════════════════════════════════════

alter table public.entreprises add column if not exists annuaire_le date;   -- dernière tournée

-- Vrai pour le premier poste du jour (la date est posée dans la même instruction : deux postes
-- qui ouvrent en même temps ne peuvent pas l'obtenir tous les deux), faux ensuite.
create or replace function public.prendre_tournee_annuaire(p_entreprise uuid)
returns boolean language plpgsql security definer set search_path = public as $$
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  update public.entreprises set annuaire_le = current_date
   where id = p_entreprise and (annuaire_le is null or annuaire_le < current_date);
  return found;
end $$;

revoke all on function public.prendre_tournee_annuaire(uuid) from public, anon;
grant execute on function public.prendre_tournee_annuaire(uuid) to authenticated;
