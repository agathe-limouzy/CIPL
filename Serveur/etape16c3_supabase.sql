-- Étape 16c, suite (01/11) : transfert d'un bâtiment ou d'un seul locataire vers une autre entreprise du
-- même groupe (CONCEPTION §16, décisions du 01/11). À exécuter dans Supabase › SQL Editor.
--
-- Les fiches changent d'entreprise en UNE transaction (transferer_fiches) ; la version de destination d'un
-- homonyme est gardée dans transferts_corbeille avant d'être fusionnée. Les documents sont copiés par le
-- poste de l'administrateur AVANT cet appel (doublons sans danger : contenu adressé par empreinte) et marqués
-- supprimés à l'origine APRÈS. Qui : un administrateur des deux entreprises ; refus si une fiche est tenue
-- par un autre, si une version a bougé, ou si le groupe ne peut plus écrire.

create table if not exists public.transferts_corbeille (
  id            bigserial primary key,
  entreprise_id uuid not null references public.entreprises(id) on delete cascade,
  type          text not null check (type in ('batiment', 'locataire')),
  cible_id      text not null,
  data          jsonb not null,
  le            timestamptz not null default now(),
  par           text
);
alter table public.transferts_corbeille enable row level security;
drop policy if exists "transferts_corbeille : admins" on public.transferts_corbeille;
create policy "transferts_corbeille : admins" on public.transferts_corbeille
  for select using (public.est_admin(entreprise_id));

create or replace function public.transferer_fiches(p_de uuid, p_vers uuid, p_batiment jsonb, p_locataires jsonb,
                                                    p_seul boolean, p_versions jsonb, p_session text,
                                                    p_reglages jsonb default null, p_reglages_version int default 0)
returns json language plpgsql security definer set search_path = public as $$
declare
  nom text := public.mon_nom();
  bid text := p_batiment->>'id';
  nom_bat text := lower(trim(coalesce(p_batiment->>'Name', '')));
  g_de uuid; g_vers uuid;
  v public.verrous;
  l jsonb; lid text; cible text; nv int;
  dest_id text;
  n_loc int := 0;
begin
  if p_de = p_vers then return json_build_object('ok', false, 'refus', 'meme'); end if;
  if not public.est_admin(p_de) or not public.est_admin(p_vers) then
    return json_build_object('ok', false, 'refus', 'admin');
  end if;
  select groupe_id into g_de from public.entreprises where id = p_de;
  select groupe_id into g_vers from public.entreprises where id = p_vers;
  if g_de is null or g_de is distinct from g_vers then return json_build_object('ok', false, 'refus', 'groupe'); end if;
  if not public.ecriture_permise(g_vers) then return json_build_object('ok', false, 'refus', 'expire'); end if;

  -- Verrous : le bâtiment (transfert entier) et chaque locataire transféré doivent être libres.
  if not p_seul then
    v := public.detenteur_autre(p_de, 'batiment', bid, p_session);
    if v.cible_id is not null then
      return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'cible', p_batiment->>'Name');
    end if;
  end if;
  for l in select * from jsonb_array_elements(p_locataires) loop
    lid := l->>'id';
    v := public.detenteur_autre(p_de, 'locataire', lid, p_session);
    if v.cible_id is not null then
      return json_build_object('ok', false, 'refus', 'verrou', 'nom', v.nom, 'cible', l->'data'->>'Name');
    end if;
  end loop;

  -- Versions : ce que le poste connaît doit être ce que le serveur tient (sinon : relire, recommencer).
  if not p_seul and (select version from public.batiments where entreprise_id = p_de and id = bid)
       is distinct from (p_versions->>('b:' || bid))::int then
    return json_build_object('ok', false, 'refus', 'version', 'cible', p_batiment->>'Name');
  end if;
  for l in select * from jsonb_array_elements(p_locataires) loop
    lid := l->>'id';
    if (select version from public.locataires where entreprise_id = p_de and id = lid)
         is distinct from (p_versions->>('l:' || lid))::int then
      return json_build_object('ok', false, 'refus', 'version', 'cible', l->'data'->>'Name');
    end if;
  end loop;

  -- Homonyme dans la destination (par nom, comme les dossiers) : sa version est gardée, puis mise à jour
  -- (transfert entier) en conservant SON identifiant : les charges du bâtiment y font référence.
  select id into dest_id from public.batiments
   where entreprise_id = p_vers and lower(trim(data->>'Name')) = nom_bat limit 1;
  if dest_id is not null then
    insert into public.transferts_corbeille (entreprise_id, type, cible_id, data, par)
      select p_vers, 'batiment', id, data, nom from public.batiments where entreprise_id = p_vers and id = dest_id;
    if not p_seul then
      update public.batiments
         set data = jsonb_set(p_batiment, '{id}', to_jsonb(dest_id)), version = version + 1,
             maj_par = nom, maj_le = now(), maj_session = p_session
       where entreprise_id = p_vers and id = dest_id;
    end if;
  else
    dest_id := bid;
    insert into public.batiments (entreprise_id, id, data, version, maj_par, maj_session)
    values (p_vers, dest_id, p_batiment, 1, nom, p_session);
  end if;

  -- Locataires : un homonyme de la destination est remplacé (gardé avant), les autres sont ajoutés.
  for l in select * from jsonb_array_elements(p_locataires) loop
    lid := l->>'id';
    cible := lower(trim(coalesce(l->'data'->>'Name', '')));
    insert into public.transferts_corbeille (entreprise_id, type, cible_id, data, par)
      select p_vers, 'locataire', id, data, nom from public.locataires
       where entreprise_id = p_vers and batiment_id = dest_id and lower(trim(data->>'Name')) = cible and id <> lid;
    delete from public.locataires
     where entreprise_id = p_vers and batiment_id = dest_id and lower(trim(data->>'Name')) = cible and id <> lid;
    insert into public.locataires (entreprise_id, id, batiment_id, ordre, data, version, maj_par, maj_session)
    values (p_vers, lid, dest_id, coalesce((l->>'ordre')::int, 0), l->'data', 1, nom, p_session)
    on conflict (entreprise_id, id) do update
      set batiment_id = excluded.batiment_id, ordre = excluded.ordre, data = excluded.data,
          version = public.locataires.version + 1, maj_par = excluded.maj_par, maj_le = now(), maj_session = excluded.maj_session;
    delete from public.locataires where entreprise_id = p_de and id = lid;
    n_loc := n_loc + 1;
  end loop;
  if not p_seul then delete from public.batiments where entreprise_id = p_de and id = bid; end if;

  -- Réglages de la destination : les listes de charges créées pour le transfert (par nom).
  if p_reglages is not null then
    update public.reglages
       set data = p_reglages, version = version + 1, maj_par = nom, maj_session = p_session, maj_le = now()
     where entreprise_id = p_vers and version = p_reglages_version returning version into nv;
    if nv is null then
      if p_reglages_version = 0 and not exists (select 1 from public.reglages where entreprise_id = p_vers) then
        insert into public.reglages (entreprise_id, data, version, maj_par, maj_session)
        values (p_vers, p_reglages, 1, nom, p_session);
      else
        raise exception 'réglages de la destination modifiés entre-temps : recommencez';   -- annule la transaction
      end if;
    end if;
  end if;

  return json_build_object('ok', true, 'batiment', dest_id, 'locataires', n_loc);
end $$;
revoke all on function public.transferer_fiches(uuid, uuid, jsonb, jsonb, boolean, jsonb, text, jsonb, int) from public, anon;
grant execute on function public.transferer_fiches(uuid, uuid, jsonb, jsonb, boolean, jsonb, text, jsonb, int) to authenticated;
