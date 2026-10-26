-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 14 : e-mails de l'abonnement (décisions et maquettes du 25/10).
-- Aux admins du groupe seulement, depuis le Gmail de CIPL, par la fonction serveur
-- « emails-abonnement » :
--   chaque matin (tâche planifiée ci-dessous) : fin d'essai dans N jours (de J-7 à J-2), demain
--   (J-1), essai terminé (jusqu'à 7 jours après, pour rattraper un matin manqué) ;
--   à la demande de stripe-webhook : résiliation enregistrée, abonnement terminé.
-- Les reçus, le paiement refusé et la carte qui expire sont envoyés par Stripe lui-même.
-- Après etape13. À relancer sans risque (tout est « si absent » ou remplacé).
-- Prérequis : rien à copier — la clé de la tâche est créée ici, dans le coffre (Vault).
-- ════════════════════════════════════════════════════════════════════════════

create extension if not exists pg_cron;
create extension if not exists pg_net with schema extensions;

-- Ce qui est parti : jamais deux fois le même e-mail. `cle` : la date concernée (fin d'essai,
-- échéance) — un essai prolongé ou une autre échéance redonnent droit à l'e-mail.
create table if not exists public.envois_abonnement (
  groupe_id uuid not null references public.groupes(id) on delete cascade,
  sorte     text not null,   -- essai_j7 | essai_j1 | essai_fin | resiliation | abonnement_fin
  cle       text not null,
  envoye_le timestamptz not null default now(),
  primary key (groupe_id, sorte, cle)
);
alter table public.envois_abonnement enable row level security;   -- aucune règle : le serveur seul

-- Les e-mails du jour : [{groupe, sorte, date}]. `p_jour` permet de simuler une autre date
-- (parcours AR) ; rien n'est envoyé ni noté ici.
create or replace function public.emails_abonnement_du_jour(p_jour date default current_date)
returns json language sql stable security definer set search_path = public as $$
  select coalesce(json_agg(json_build_object('groupe', x.groupe, 'sorte', x.sorte, 'date', x.cle) order by x.groupe), '[]'::json)
  from (
    select g.id as groupe, g.fin_essai::text as cle,
           case when g.fin_essai - p_jour between 2 and 7 then 'essai_j7'
                when g.fin_essai - p_jour = 1 then 'essai_j1'
                when g.fin_essai < p_jour and g.fin_essai >= p_jour - 7 then 'essai_fin' end as sorte
    from public.groupes g
    where g.statut = 'essai'
  ) x
  where x.sorte is not null
    and not exists (select 1 from public.envois_abonnement e
                    where e.groupe_id = x.groupe and e.sorte = x.sorte and e.cle = x.cle);
$$;

-- Les destinataires (admins : adresse et nom affiché) et les entreprises du groupe.
create or replace function public.destinataires_abonnement(p_groupe uuid)
returns json language sql stable security definer set search_path = public as $$
  select json_build_object(
    'admins', coalesce((select json_agg(json_build_object(
                          'email', u.email, 'nom', coalesce(nullif(m.nom_affiche, ''), split_part(u.email, '@', 1))))
                        from public.membres m join auth.users u on u.id = m.user_id
                        where m.groupe_id = p_groupe and m.role = 'admin'), '[]'::json),
    'entreprises', coalesce((select json_agg(e.nom order by e.cree_le)
                             from public.entreprises e where e.groupe_id = p_groupe), '[]'::json));
$$;

-- Noté avant l'envoi (vrai : à envoyer ; faux : déjà parti), retiré si l'envoi échoue.
create or replace function public.noter_envoi_abonnement(p_groupe uuid, p_sorte text, p_cle text)
returns boolean language plpgsql security definer set search_path = public as $$
begin
  insert into public.envois_abonnement (groupe_id, sorte, cle) values (p_groupe, p_sorte, p_cle)
  on conflict do nothing;
  return found;
end $$;

create or replace function public.oublier_envoi_abonnement(p_groupe uuid, p_sorte text, p_cle text)
returns void language sql security definer set search_path = public as $$
  delete from public.envois_abonnement where groupe_id = p_groupe and sorte = p_sorte and cle = p_cle;
$$;

-- La clé de la tâche planifiée : créée une fois dans le coffre, jamais affichée ni copiée.
do $$ begin
  if not exists (select 1 from vault.secrets where name = 'cipl_cron') then
    perform vault.create_secret(encode(extensions.gen_random_bytes(32), 'hex'), 'cipl_cron',
                                'Clé de la tâche planifiée des e-mails de l''abonnement (étape 14)');
  end if;
end $$;

create or replace function public.cle_cron_valide(p_cle text)
returns boolean language sql stable security definer set search_path = public as $$
  select exists (select 1 from vault.decrypted_secrets where name = 'cipl_cron' and decrypted_secret = p_cle);
$$;

-- Le serveur seul (fonctions avec la clé service_role) : ni CIPL, ni un visiteur.
revoke all on function public.emails_abonnement_du_jour(date) from public, anon, authenticated;
revoke all on function public.destinataires_abonnement(uuid) from public, anon, authenticated;
revoke all on function public.noter_envoi_abonnement(uuid, text, text) from public, anon, authenticated;
revoke all on function public.oublier_envoi_abonnement(uuid, text, text) from public, anon, authenticated;
revoke all on function public.cle_cron_valide(text) from public, anon, authenticated;
grant execute on function public.emails_abonnement_du_jour(date) to service_role;
grant execute on function public.destinataires_abonnement(uuid) to service_role;
grant execute on function public.noter_envoi_abonnement(uuid, text, text) to service_role;
grant execute on function public.oublier_envoi_abonnement(uuid, text, text) to service_role;
grant execute on function public.cle_cron_valide(text) to service_role;

-- Chaque matin à 8 h UTC (9 h à Paris en hiver, 10 h en été) : appel de la fonction.
select cron.unschedule(jobid) from cron.job where jobname = 'cipl-emails-abonnement';
select cron.schedule('cipl-emails-abonnement', '0 8 * * *', $cron$
  select net.http_post(
    url := 'https://ehxskfjmiioesmipzsad.supabase.co/functions/v1/emails-abonnement',
    headers := jsonb_build_object('Content-Type', 'application/json',
      'x-cipl-cron', (select decrypted_secret from vault.decrypted_secrets where name = 'cipl_cron')),
    body := '{}'::jsonb);
$cron$);
