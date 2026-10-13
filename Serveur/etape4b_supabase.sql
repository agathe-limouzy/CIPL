-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 4b : signal de vie contrôlé et « Forcer la libération » (admin)
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape4).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md §6.1.
-- ════════════════════════════════════════════════════════════════════════════

-- Signal de vie (toutes les 30 s) : prolonge le verrou SEULEMENT s'il est encore à ma
-- session. Sinon on dit qui l'a (libération forcée par un admin, ou reprise par un collègue
-- après 2 min sans signal) : l'application annule alors les saisies. Plus de ligne du tout :
-- personne ne l'a prise, on la reprend.
create or replace function public.renouveler_verrou(p_entreprise uuid, p_type text, p_id text, p_session text, p_motif text)
returns json language plpgsql security definer set search_path = public as $$
declare v public.verrous;
begin
  if p_entreprise not in (select public.mes_acces()) then raise exception 'accès refusé'; end if;
  update public.verrous set expire_le = now() + interval '2 minutes'
   where entreprise_id = p_entreprise and cible_type = p_type and cible_id = p_id
     and session_id = p_session and user_id = auth.uid()
  returning * into v;
  if found then
    return json_build_object('obtenu', true, 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le, 'expire_le', v.expire_le);
  end if;
  select * into v from public.verrous where entreprise_id = p_entreprise and cible_type = p_type and cible_id = p_id;
  if not found then
    return public.prendre_verrou(p_entreprise, p_type, p_id, p_session, p_motif);
  end if;
  return json_build_object('obtenu', false, 'nom', v.nom, 'motif', v.motif, 'pris_le', v.pris_le, 'expire_le', v.expire_le);
end $$;

-- Admin : la ligne passe au nom de l'admin, motif « libération forcée », déjà expirée.
-- N'importe qui peut donc reprendre la fiche, et le détenteur l'apprend à son prochain
-- signal de vie (≤ 30 s) par renouveler_verrou.
create or replace function public.forcer_liberation(p_entreprise uuid, p_type text, p_id text)
returns json language plpgsql security definer set search_path = public as $$
begin
  if not public.est_admin(p_entreprise) then
    return json_build_object('ok', false, 'refus', 'admin');
  end if;
  update public.verrous
     set session_id = 'force', user_id = auth.uid(), nom = public.mon_nom(), motif = 'libération forcée',
         pris_le = now(), expire_le = now() - interval '1 second'
   where entreprise_id = p_entreprise and cible_type = p_type and cible_id = p_id;
  return json_build_object('ok', true);
end $$;

do $$ declare f text; begin
  foreach f in array array[
    'renouveler_verrou(uuid, text, text, text, text)', 'forcer_liberation(uuid, text, text)']
  loop
    execute format('revoke all on function public.%s from public, anon', f);
    execute format('grant execute on function public.%s to authenticated', f);
  end loop;
end $$;
