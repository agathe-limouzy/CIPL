-- ════════════════════════════════════════════════════════════════════════════
-- CIPL — étape 7 : réservation de l'envoi automatique des factures
-- À coller UNE fois dans Supabase › SQL Editor › New query › Run (après etape6b).
-- Relançable sans risque. Voir CONCEPTION_MULTIUTILISATEUR.md (étape 7).
-- ════════════════════════════════════════════════════════════════════════════

-- Avant d'envoyer une facture : le locataire est réservé (verrou « envoi des factures »), puis la
-- fiche du serveur est vérifiée : la facture attend toujours son envoi, et l'application connaît
-- la dernière version (son enregistrement du statut « Envoyé » passera donc). Sinon le verrou est
-- rendu et l'envoi refusé. Une facture ne peut ainsi partir qu'une fois, même depuis deux PC.
-- Réponse : {ok} ou {ok: false, refus: 'verrou' | 'envoyee' | 'version', nom, motif, pris_le, statut}.
create or replace function public.reserver_envoi(p_entreprise uuid, p_locataire text, p_facture text,
                                                 p_version int, p_session text)
returns json language plpgsql security definer set search_path = public as $$
declare v json; l public.locataires; st text;
begin
  v := public.prendre_verrou(p_entreprise, 'locataire', p_locataire, p_session, 'envoi des factures');   -- contrôle l'accès
  if not (v->>'obtenu')::boolean then
    return json_build_object('ok', false, 'refus', 'verrou', 'nom', v->>'nom', 'motif', v->>'motif', 'pris_le', v->>'pris_le');
  end if;
  select * into l from public.locataires where entreprise_id = p_entreprise and id = p_locataire;
  select f->>'statut' into st
    from jsonb_array_elements(coalesce(l.data->'facturesEtat', '[]'::jsonb)) f
   where f->>'key' = p_facture limit 1;
  if l.id is null or coalesce(st, '') <> 'AttenteEnvoi' or l.version <> p_version then
    perform public.liberer_verrou(p_entreprise, 'locataire', p_locataire, p_session);
    return json_build_object('ok', false,
      'refus', case when coalesce(st, '') <> 'AttenteEnvoi' then 'envoyee' else 'version' end,
      'nom', l.maj_par, 'statut', st);
  end if;
  return json_build_object('ok', true);
end $$;

revoke all on function public.reserver_envoi(uuid, text, text, int, text) from public, anon;
grant execute on function public.reserver_envoi(uuid, text, text, int, text) to authenticated;
