-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 9c2 : abonnement payé avec Stripe (carte ou prélèvement SEPA).
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape9c1).
-- Relançable sans risque. Les deux fonctions serveur sont dans Serveur/fonctions/ ;
-- la mise en place complète (Stripe, Supabase) est dans PARCOURS_TEST_CIPL.md, parcours AH.
-- ════════════════════════════════════════════════════════════════════════════

-- Ce que Stripe dit du groupe, recopié par la fonction stripe-webhook à chaque changement.
alter table public.groupes add column if not exists stripe_client     text;     -- client Stripe (cus_…)
alter table public.groupes add column if not exists stripe_abonnement text;     -- abonnement en cours (sub_…), nul s'il n'y en a pas
alter table public.groupes add column if not exists formule           text;     -- mensuel | annuel
alter table public.groupes add column if not exists echeance          date;     -- prochaine échéance (ou fin, si résilié)
alter table public.groupes add column if not exists resilie           boolean not null default false;   -- prend fin à l'échéance

-- Étape 9c1, complétée : la formule, l'échéance, la résiliation, et s'il y a un abonnement Stripe.
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
               order by (m.user_id = gr.titulaire) desc limit 1),
    'abonne', gr.stripe_abonnement is not null,
    'formule', gr.formule, 'echeance', gr.echeance, 'resilie', gr.resilie)
    from public.groupes gr where gr.id = g);
end $$;

revoke all on function public.abonnement(uuid) from public, anon;
grant execute on function public.abonnement(uuid) to authenticated;
