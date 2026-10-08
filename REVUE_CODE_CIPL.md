# Revue de code — application CIPL

**Date** : 16 septembre 2026 · **Périmètre** : 107 fichiers C#, ~20 920 lignes, UI comprise · **Branche** : `Facturation`

Chaque finding porte un statut :

- **CONFIRMÉ** — chemin de code tracé, scénario de défaillance vérifié.
- **PLAUSIBLE** — risque réel, mais dépend d'un cas d'usage non tranché.
- **ÉCARTÉ** — vérifié comme faux positif (listé quand on pourrait croire le contraire).

---

## Passation — à lire en premier (mise à jour 21/09/2026)

Ce document est le **suivi de la revue de code**. Tout ce qui suit a été écrit et compilé ; ce qui a été *réellement exécuté* est listé plus bas, et la distinction compte.

**Architecture du code** (couches, modèle de données, fichiers sur disque, démarrage, modules, services externes, conventions, « où modifier quoi ») : voir `ARCHITECTURE_CIPL.md` (30/09).

### Session du 30/09 — refonte des révisions de loyer et initialisation du dépôt

D'après le schéma de l'utilisatrice ; détail dans `FACTURATION_CIPL_PENNYLANE.md` §16.5 (« Refonte des révisions », « Dépôt — initialisation »).
- **Écran « Initialiser »** (ex-« Modalités ») avec type de révision (indice / paliers / aucune) et franchise ; bouton Initialiser / Réinitialiser / Modifier selon l'état. **Indice** : initialiser puis réviser (la bascule manuelle disparaît). **Paliers** : nouvel écran `PaliersPanel` (couverture complète du bail exigée, palier = lendemain du précédent, fin = veille d'un début de période ou fin du bail).
- **Logique commune `Loyers`** (nouveau) : montant d'une période (palier en vigueur, franchise, prorata de la seule 1re période partielle), paliers, loyer courant actualisé au chargement. Le test « indice initialisé », recopié 7 fois, devient `Locataire.IndiceInitialise / LoyerInitialise / RevisionIndiceSuivie`.
- **Changement de comportement assumé** : le loyer pré-rempli sur la facture est celui de la période choisie, plus celui de la dernière facture (il restait à l'ancien montant après une révision).
- **Suivi** : lignes grisées « Franchise » et « Hors bail » (avant le premier bail — elles apparaissaient « À faire » pour un bail commencé en cours d'année — et après le départ du locataire).
- **Prorata au jour** (décisions de l'utilisatrice) : début du bail, fin de franchise, départ du locataire (nouvelle date « dernier jour de location »), avenant en cours de période (`historiqueLoyers` + `debutConditionsISO`). La fin du bail seule n'arrête pas la facturation (tacite prolongation).
- **Dépôt** : initialisation (demandé et reçu / demandé non reçu → créance / pas demandé → facture « Dépôt de garantie » depuis le suivi, clé `depot-initial`).
- **Parcours de création du locataire** : Général → Bail → Loyer → Dépôt, bandeau d'étapes en tête de fiche, écrans enchaînés à la création, loyer et dépôt verrouillés tant que l'étape précédente manque, facturation (boutons de la carte et du suivi) bloquée tant que le parcours n'est pas fini. Défaut corrigé au passage : un nouveau locataire enregistré sans dates de bail recevait un bail au 01/01/0001 (date par défaut du contrôle de saisie).
- Tests `RevisionLoyerTests` + `InputAndTextTests` — **351 tests EditMode verts** (suite officielle du 01/10). Pas encore vu en Play : **parcours de test à suivre dans `PARCOURS_TEST_CIPL.md`**.
- **Défaut trouvé en Play dès l'étape 0.2, corrigé** : impossible de saisir le nom d'un **nouveau bâtiment** (ni d'un nouveau locataire). Le correctif du 22/09 (« valeur affichée deux fois ») a posé l'état de repos lecture seule dans `InputAndText.Awake` ; or Awake ne tourne qu'à la première activation, et une fiche neuve est construite inactive : il passait après `Modify()` et remettait les champs en lecture. Awake respecte désormais un état déjà choisi (`_etatPose`). Tests `InputAndTextTests`.
- **Retour de l'utilisatrice en Play** : le suivi d'un nouveau locataire se remplissait de loyers « À faire » avant toute configuration. Désormais `FacturationSuivi.LignesAffichees` ne planifie rien tant que le parcours n'est pas fini (seules restent les factures réellement touchées), et `FacturationAlertes.Pour` se tait. `Lignes` est inchangé (règles et tests). Au passage : la carte Dépôt d'une fiche neuve affichait « Réviser » (état non chargé à la création). Le bandeau du parcours suit maintenant la saisie (nom tapé → étape Bail, dates valides → « Sauvegarder »).
- **Départ du locataire déplacé dans la section Bail de la fiche** (choix de l'utilisatrice : c'est un événement du bail) — `LocataireBailFields`, rangée « Départ du locataire » sous les dates, visible en lecture seulement si un départ est saisi ; retiré de l'écran du loyer. **Menu « Bail repris »** : il proposait les périodes de la périodicité ENREGISTRÉE (des mois pour un nouveau locataire alors qu'on venait de choisir « trimestriel »), des périodes futures et d'avant le bail ; il suit désormais la périodicité choisie à l'écran, ne propose que les périodes échues ou dont la facture est partie (J-22) et postérieures au début du bail, et garde une reprise déjà enregistrée hors fenêtre.
- **Régularisations liées au bail** (retour : un bail de 2026 affichait des régul depuis 2023 et une alerte URGENT pour les charges 2024) : `Loyers.AnneeDansLeBail` — pas de ligne de régul ni d'alerte pour des charges d'une année hors bail (du premier bail au départ). Un garde-fou semblable avait été retiré le 12/09 parce qu'il cachait l'alerte des baux récents ; il repose désormais sur le PREMIER bail, ce qui règle le cas des renouvellements.
- **Facture du dépôt initial au format d'une refacturation** (demande de l'utilisatrice, et non plus celui d'une révision) : `FactureDepotPanel` produit en mode initial une `FacturePdfService.Data` (gabarit facture simple) — une ligne « Dépôt de garantie — N termes de loyer », TVA 0 avec le libellé « TVA — dépôt de garantie non soumis » (nouveau champ `Data.tvaLibelle`, `{{TVA_LIBELLE}}` du gabarit, « TVA 20% » par défaut). Le protocole d'émission est commun aux deux formats (`Document` : numéro, correction, envoi, suivi). Aperçu généré et vérifié. Le bloc d'explication est masqué en mode initial.
- **Le 1er palier part du début du bail et contient la franchise** (choix de l'utilisatrice : « 01/09 → 31/12 · 4 mois, dont 3 mois payés ») : `Loyers.DebutPaliers` = début du bail (ou avenant), plus la fin de franchise ; les durées affichent la part payée (`Loyers.DureeLibelle`) ; une fin de palier entièrement en franchise n'est pas proposée ; changer la franchise ne demande plus de réinitialiser.
- **Formulaire des paliers refait** (retour : « zone en bordel ») : trois colonnes (loyer · « Du » au format des dates de l'app · « Au »), boutons à droite. « Au » est une **liste des seules fins valides** (`Loyers.FinsPossibles` : veille d'un début de période, ou fin du bail), avec la durée ; en modification, elle s'arrête avant la fin du palier suivant. **`UIDropdown` défile désormais** (hauteur bornée, ouverture vers le haut s'il manque de place, choix courant visible) : les longues listes sortaient de l'écran, y compris celle de la reprise.
- **Navigation après suppression** (retour de l'utilisatrice : écran vide) : supprimer un locataire revient au résumé du bâtiment, et « Annuler » ramène sur sa fiche (`BatimentPrefab.DeleteLocataire` / `RestoreLocataire`). Supprimer un bâtiment montre l'onglet voisin (précédent, sinon suivant), et l'accueil s'il n'en reste aucun (`MenuManager.RemoveTabAndBuilding` prenait toujours le premier onglet, ou rien).
- Deux défauts trouvés en écrivant ce parcours, corrigés : le bandeau du parcours restait caché à la création (la fiche est construite avant que `Addlocataire` n'ajoute le locataire à la liste, `GetLocataire()` renvoyait null) ; la facture du dépôt initial reprenait le nombre de périodes mémorisé de la dernière facture de révision.
- **Fin de session 30/09** : parcours E, F, G relus avec l'utilisatrice, jugés bons. Les 3 derniers changements (palier dès le début du bail, format du dépôt initial, régul liées au bail) ont été vérifiés par la suite officielle le 01/10 : 351/351.
- **01/10 — la franchise ne porte que sur le loyer** (retour de l'utilisatrice) : un locataire qui verse des provisions garde ses périodes de franchise à facturer (loyer 0,00, provisions dues) ; sans provision, elles restent grisées « Franchise ». `Loyers.PeriodeFacturable` / `AppelleProvisions`, `FacturationSuivi.Lignes` ; Pennylane ne reçoit pas de ligne de loyer à 0 € (`PennylaneClient.Montants`). Libellés « Loyer facturé à partir du » ; sur le PDF, la ligne du loyer nul s'intitule « Loyer — franchise » (`Loyers.EnFranchise`, `FactureLoyerPanel.LigneLoyer`). **Prorata au jour confirmé** (un essai au mois a été annulé à la demande de l'utilisatrice : le jour est plus juste) — un mois de franchise sur un trimestre = 59/90 du loyer (1 966,67 pour 3 000), verrouillé par un test. **354 tests verts.** Parcours : étape D3.
- **01/10 — refacturation « déduite des provisions »** (demande de l'utilisatrice) : case sur la refacturation (si le locataire a une provision pour la liste de la charge), mémorisée par locataire (`ChargeFacturation.deduitProvisions`). Cochée : la charge figure sur la régul, « (déjà refacturée) », comptée dans le total et déduite comme déjà réglée — solde inchangé ; décochée : hors des provisions, absente de la régul (comme avant). `FactureRegulPanel.ChargesRefacturees`, `RegulData.dejaRefacture`. Aperçu PDF vérifié. **355 tests verts.** Parcours : section J.
- **01/10 — refacturation de plusieurs charges sur une facture** (demande de l'utilisatrice) : cases + montant HT par charge, une ligne par charge sur le PDF ; dans le suivi une ligne `refac-<id>` par charge, même numéro et même PDF (mécanique de la régul regroupée). Garde-fou de regroupement extrait dans `FacturationSuivi.Regroupable`, partagé avec la régul (messages inchangés). `FactureInfo.chargeId` supprimé. Aperçu PDF vérifié. **356 tests verts.** Parcours : J6-J7.
- **01/10 — avoir sur une charge** (vérification demandée par l'utilisatrice) : 5 défauts trouvés sur le chemin d'une charge négative, corrigés — coût négatif relu vide puis remis à 0 (`ChargePanel`) ; ligne négative omise du PDF mais comptée au total (`LignesProvision`) ; titre « FACTURE » et « SOMME À NOUS RÉGLER » sur un remboursement (→ « AVOIR », phrase de remboursement) ; ligne négative passée « Impayé » avec bouton Rappel (`EtatDe` : jamais pour un montant négatif, régul comprise) ; montant négatif ignoré par `MarquerCorrige` et la reprise des parts. Puis, à sa demande : régul et révision du dépôt qui remboursent intitulées « AVOIR » ; menu « Avoir sur la facture n° » (refacturation et régul, factures émises du locataire : `FacturationSuivi.FacturesOrigine`), imprimé sous le titre de l'avoir seulement. Aperçus PDF vérifiés. **358 tests verts.** Parcours : J8.
- **01/10 — une facture = une ligne** (retour en Play, étape L6) : une refacturation de plusieurs charges n'affiche plus qu'une ligne au suivi et une créance à l'accueil, au total, libellée « Refacturation : A et B » ou « … : N charges » si trop long. Fusion à l'affichage (`Lignes`, `Dues`), stockage par charge inchangé. **358 tests verts.**
- **01/10 (fin) — 4e type de révision : loyer selon le chiffre d'affaires** (demande de l'utilisatrice) : % du CA HT de l'année civile écoulée, borné par un minimum et un maximum indexés INSEE ; 1re année = loyer de départ ; s'applique jusqu'à la révision suivante (pas de facture de régularisation rétroactive — décision de l'utilisatrice) ; CA d'une année incomplète ramené à l'année. `TypeRevision.ChiffreAffaires`, `Loyers.LoyerSelonCA` / `FractionPresence` / `CA` / `PoserCA`, même logique que les deux autres types (schéma mis à jour) : l'écran général choisit le type, puis « Initialisation du loyer selon le CA » (indice + min, max, %) et « Révision du loyer selon le CA » (bornes en vigueur, montant du CA, cadre de résultat). Retour en Play : min / max / % affichés comme le loyer de départ (blocs clonés), résultat recentré sur le CA, les bornes et le loyer retenu (indices réduits à une ligne) ; fenêtre élargie à 900 px pour la révision (volet Indice ; Initialiser garde 560) avec les champs courts côte à côte (`LignesCompactes` : loyer de départ | min | max | %, puis trimestre de référence | date de révision ; une colonne de résultat à droite a été essayée puis abandonnée : vide et déséquilibrée), résultat en pleine largeur sur deux colonnes (calcul | décision) ; CA précédent (grisé, vide sinon) à côté du nouveau CA, qui reste dans son champ après le calcul — recalculer après avancée de la date demande de vérifier le montant ; part du CA modifiable à la révision (min / max restent grisés) ; « Dernière révision » (trimestre + indice précédents) en référence à côté du trimestre de révision, indice comme CA. **361 tests verts.** **361 tests verts.** Locataire de test **G** dans TEST Nouveautés, parcours §L11. **Validé en Play par l'utilisatrice** (après les retours d'affichage ci-dessous).
- **01/10 — validé en Play par l'utilisatrice** : parcours §L (L1 à L9) sur « TEST Nouveautés » — paliers, franchise et provisions, avenant et départ, parcours de création, dépôt initial, refacturation multi-charges, déduction des provisions, avoirs, régul qui rembourse.
- **01/10 — données de test** : bâtiment **« TEST Nouveautés »** créé dans **DemoCIPL** (fichier `batiments/batiment_1ce28839-….json`, écrit par script éditeur, aucun autre bâtiment touché) : 6 locataires A-F (paliers + franchise, franchise + provisions, avenant + départ, indice à initialiser, dépôt à initialiser, charges et avoirs) et 7 charges 2025 de F, dont 4 factures déjà émises par script (PDF réels, suivi « Envoyé ») : 2026/08001 électricité (échue → Impayé), 08002 avoir électricité (échu, reste Envoyé), 09003 eau déduite des provisions, 09004 jardin + ménage. Mode à suivre : `PARCOURS_TEST_CIPL.md` §L — tout se vérifie sans rien émettre, en gardant le mode Email de DemoCIPL. **À supprimer après les essais.**
- **01/10 (soir) — gestion du départ d'un locataire, cadrée avec l'utilisatrice** (décisions chiffrées dans `FACTURATION_CIPL_PENNYLANE.md` §16.5 « Départ du locataire ») : décompte de sortie unique (retenues + créances − dépôt, avoir si négatif), délai de restitution réglable (2 mois), régul de sortie à la date habituelle avec charges au prorata de présence **(arrivée comprise — change les régul non émises des locataires arrivés en cours d'année)**, état des lieux (date + PDF), dernière facture « à corriger », fiche grisée puis archivée, 4 alertes.
  - **Lot 1 fait — règles et tests** : `DepartLocataire` (nouveau), `ListesCharges.QuotePartAuProrata`, `Loyers.PeriodesFacturees`, `FacturationSuivi.DuesDe` (extrait de `Dues`), `FactureEtat.loyerHT`, champs `Locataire` (état des lieux, délai, archive), alertes Départ / Restitution / Archiver et « montant à corriger », plus de révision du dépôt après le départ ni d'alerte pour une fiche archivée. Régul : quote-part au prorata, provisions × périodes facturées, provision mémorisée valable pour son année seulement (elle suit l'année choisie). **372 tests verts** (`DepartLocataireTests`, 11).
  - **Lot 2 fait — la fiche** (compilé, 372 verts, **pas encore vu en Play** : parcours §M) : section Bail, rangée « État des lieux de sortie · Restitution du dépôt (mois) » sous le départ (`LocataireBailFields`, rangées factorisées dans `Rangee` / `CloneDate`) ; PDF de l'état des lieux comme ligne de documents du bail (`BailFileUI.LigneDocument`, bouton « + État des lieux » si départ saisi) ; liste du bâtiment : badge « Départ le / Parti le / Archivé », ligne grisée, partis en bas, archivés masqués sauf case « Anciens locataires (n) » (`BatimentPrefab.RebuildLocataireRows`) ; `BatimentPrefab.Archiver` (+ `MenuManager.SetTabVisible` : onglet masqué, montré le temps d'une consultation) ; bouton « Archiver / Réactiver » dans la rangée de sortie quand `PretAArchiver`. Départ saisi → plus d'alerte « Fin de bail » ni « Résiliation » (`Locataire.RenouvellementProche` / `ResiliationProche`) ; archivé → plus d'objectifs dans « À traiter » ni sur l'onglet du bâtiment.
  - **02/10 — §M vu en Play (M1-M4)** : un délai non saisi garde 2 mois (l'alerte n'arrive qu'à J-15 : comportement voulu) ; avec 1 mois, « Rest. dépôt » apparaît. Retour : « je ne vois pas où émettre la restitution » → lot 3.
  - **Lot 3 — décompte de sortie** (choix de l'utilisatrice : carte Facturation + suivi, mode « sortie » de l'écran du dépôt) : `FactureDepotPanel` mode sortie (carte « Décompte de sortie » à la place de celle du dépôt : dépôt détenu, retenues libellé / HT / TVA / −, sommes dues à cocher, totaux) ; PDF au format simple (dépôt restitué en négatif, une ligne par retenue et par facture reprise, AVOIR si négatif) ; à l'émission, les factures reprises passent « Payé » ; correction = mêmes retenues et mêmes factures (`FactureInfo.depotDetenu / retenuesSortie / creancesReprises`, `DepartLocataire.CreancesProposees(loc, dejaReprises)`). `DepartLocataire.Calculer` remplace `Solde`. Suivi : ligne `depot-sortie` à la date limite de restitution ; révision du dépôt plus planifiée après le départ. Carte Facturation : 4e bouton « Décompte de sortie » (pastille aussi sur l'alerte de restitution) ; carte Dépôt : « Réviser » grisé, plus de badge. Parcours §M6-M7. Limite (`ponytail:`) : une créance décochée lors d'une correction reste « Payé ». **374 tests verts**, aperçu PDF généré et vérifié.
  - **02/10 — décompte sans TVA** (demande : « enlève toute info de TVA si je ne mets pas TVA sur les sommes à déduire ») : `FacturePdfService.Data.masquerTva` → une seule ligne « Total », ni HT, ni TVA, ni mention ; gabarit simple : `{{TOTAUX}}` (sortie identique pour les autres factures). Choix « Mention TVA » masqué à l'écran sans retenue soumise. **375 tests verts**, aperçu vérifié.
  - **02/10 — dépôt initial sans ligne de TVA** (« on n'a pas vraiment le droit de faire un dépôt de garantie avec la TVA ») : un seul « Total » (`masquerTva`), plus de « TVA — dépôt de garantie non soumis · 0,00 ». Décisions de l'utilisatrice : l'option « loyer TTC » reste la base du calcul, le libellé « … termes de loyer TTC » et l'explication de la révision (« … de loyer T.T.C. ») restent (« on est obligé de mettre TTC vu qu'on a les provisions pour charges dans le dépôt ») ; la mention TVA reste proposée sur les factures de dépôt. `masquerTva` ne masque donc que les totaux ; seul le décompte sans retenue soumise retire aussi la mention. 375 verts.
  - **02/10 — parcours de départ guidé** (retour : « il faut un bouton spécifique pour le départ… réfléchir au parcours utilisateur pour que ce soit simple » ; maquettes montrées puis validées) : bouton d'en-tête « Départ du locataire / Départ en cours / Archivé · Réactiver », fenêtre `DepartPanel` (dernier jour, délai, état des lieux oui/non + PDF, annuler le départ), bandeau de six étapes `ParcoursDepartUI` (règles testées : `DepartLocataire.StatutDe / Prochaine / Consigne / LigneDernierLoyer / LigneRegulSortie / PeutAnnuler`, `Locataire.sansEtatDesLieux`). Saisie retirée de la section Bail (ligne en lecture `LocataireBailFields.TexteDepart` ; le bail n'efface plus le départ à l'enregistrement), bouton « + État des lieux » remplacé par `BailFileUI.JoindreEtatDesLieux()`. Le bandeau se rafraîchit après une facture (`LocataireSuiviInline.RefreshFor`) et un changement d'état du suivi ; `LocatairePrefab.ApresDepart()` enregistre et réaffiche. **Écrit sans compiler** : le serveur MCP Unity était déconnecté — compiler et lancer les tests dès qu'il revient (tests ajoutés : 2). Parcours §M refait (M1-M8).
  - **02/10 — cas test complet « H · Départ complet »** (demande : « fais-moi un cas test et un todo pour tester ») : parcours **§N** (N0-N11, montants attendus calculés : loyer corrigé 1 483,52 / 2 140,22 TTC, décompte -1 259,78, régul 443,84 − 600 = -156,16 HT / -187,39 TTC). H se crée par script éditeur hors Play (clone de A, réglages de facture remis à zéro, sauvegarde `.bak` du JSON avant écriture) — **créé le 05/10** (sauvegarde `batiment_1ce28839-….json.avant-H-20261005-110733.bak` à côté du fichier ; relu : parcours terminé, T2 dû 1 483,52, étapes Départ faite / Dernier loyer, État des lieux, Décompte, Régul à faire / Archiver en attente, 3 alertes : régul de sortie et restitution URGENT, loyer à corriger). Parcours de départ compilé le 05/10 : **377 tests verts**.
  - **05/10 — §N en Play, N5 vu** : correction du 2e trimestre 2025 juste (corrigée(1), 2 140,22, `loyerHT` 1 483,52, étape 2 faite, alerte « à corriger » éteinte dans les calculs). **Défaut trouvé** (antérieur au départ, vaut pour toute facture) : les alertes et pastilles de la carte Facturation restaient figées après une émission ou un changement d'état du suivi, jusqu'à la réouverture de la fiche. Corrigé : `LocatairePrefab.RafraichirAlertesFacturation()`, appelé par `LocataireSuiviInline.RefreshFor` (fin d'émission des 4 panneaux) et par le menu d'état du suivi — compilé le 05/10, **377 verts**.
  - **05/10 — §N vérifié par simulation** (copie en mémoire de H relue sur le disque, mêmes règles, rien écrit) : N6 état des lieux (date seule → à faire, + PDF → fait) · N7 décompte -1 259,78 sans TVA, T2 soldé « Payé », alerte de restitution éteinte, annulation refusée · N8 régul 443,84 − 600 = -156,16 HT, -187,39 TTC · N9 deux avoirs dus → « En attente : archiver », payés → Archiver, archivé → 0 alerte · N10 réactivé → Archiver à refaire. Reste l'essai à l'écran de N6 à N11 (H est enregistré après N5).
  - **05/10 (après-midi) — retours de §N** : H remis à l'état d'avant N5 par script (avec le vrai PDF de la facture d'origine, pour « Corriger »), N5 refaite en Play : juste. Fenêtre « Départ du locataire » : les deux dates sont des clones du champ date de la fiche (JJ / MM / AAAA, `DepartPanel.ChampDate`) et le libellé dit « Date de l'état des lieux ». Les « ⚠ » affichés (absents de la police : un carré à l'écran) remplacés par du texte dans 6 écrans (régul, dépôt, refacturation, travaux, réglages, objectifs). Menu « Avoir sur la facture n° » **retiré de la régul** (décision de l'utilisatrice : une régul solde les provisions de l'année, elle ne corrige pas une facture ; ajouté le 01/10 par symétrie, sans cas concret) — gardé sur la refacturation ; `RegulData.factureOrigine` supprimé. **377 verts.**
  - **05/10 (fin) — §N validé en Play par l'utilisatrice** (« j'ai tout testé, c'est bon ») : départ de H jusqu'à l'archivage et la réactivation. Derniers retours appliqués : le bandeau dit **pourquoi** l'archivage attend (`DepartLocataire.RaisonsNonArchivable` : facture n° … non payée, avoir à rembourser puis à passer « Payé », loyer à facturer / corriger, décompte ou régul à faire ; seule source de `PretAArchiver`, `LoyersFactures` supprimé ; test `Le_bandeau_dit_pourquoi_on_ne_peut_pas_encore_archiver`) ; le « ✓ » (absent de la police, un carré à l'écran) remplacé par « : fait » dans les deux bandeaux et par « Fait » sur les objectifs. **378 tests verts** (05/10, 18 h 40).
  - **06/10 — un locataire en remplace un autre** (demande : « 2.1 en cas de cession… on change juste le nom et l'adresse de facturation ; 2.2 pas de cession mais tout au prorata » ; choix : bouton « Cession du bail », dépôt tel quel, lot compté une fois, remplaçant depuis le départ). Trois lots :
    - **Lot 1 — défaut corrigé** : `ListesCharges.QuotePart` comptait l'ancien et le nouvel occupant d'un même lot au dénominateur (le lot payait deux parts). Parts additionnées par lot. Test `Un_lot_qui_change_d_occupant_compte_une_fois_dans_le_partage` (A 600 · B 221,92 · C 378,08). 379 verts.
    - **Lot 2 — cession** : `CessionBail.cs` (`CessionBail`, `Cessions.Verifier / Appliquer / Texte`), `Locataire.cessions`, bouton d'en-tête « Cession du bail » + `CessionPanel` (dans `DepartLocataireUI.cs`) : nom unique dans le bâtiment, dossier renommé, destinataires mémorisés des factures vidés ; « Cédé par … le … » sous le bail. Test `Une_cession_garde_le_bail_et_trace_l_ancien_titulaire`. 380 verts.
    - **Lot 3 — remplaçant** : case « Un nouveau locataire reprend le lot » dans `DepartPanel` → `DepartPanel.CreerRemplacant` (même enchaînement que le « + » du bâtiment : `Addlocataire`, `CreateTab`, `OnSelect`) puis `DepartLocataire.PreparerRemplacant` (lot, surface, listes, bail au lendemain du départ, part de l'ancien sur les charges de l'année d'arrivée et suivantes). Test `Le_remplacant_reprend_le_lot_et_la_part_de_l_ancien`. Défaut vu en relisant : une fiche de remplaçant supprimée laissait sa part dans la charge, comptée au dénominateur → `QuotePart` ignore la part d'un locataire absent du bâtiment (vaut aussi pour tout locataire supprimé avant). **381 tests verts** (06/10).
    - **Retour en Play (06/10)** : les bandeaux des deux fenêtres n'avaient pas l'allure des autres modales (pastille arrondie, titre long qui débordait) → `DepartPanel.Entete`, même construction que `PaliersPanel` (sprite `RoundedTop`, titre en « … »). Et « mets le bouton cession dans le départ, c'est la même origine, un locataire part » → bouton d'en-tête « Cession du bail » supprimé ; la fenêtre « Départ du locataire » commence par « Que se passe-t-il ? » (lot libre / remplaçant / cession — la cession seulement sans départ saisi) ; la case remplaçant devient l'un des trois choix ; la cession n'est plus une fenêtre mais un bloc (`CessionPanel.Bloc`). Puis « mets-moi un dropdown plutôt pour le type de départ » : les trois boutons radio deviennent un menu déroulant « Type de départ » (`UIDropdown`, celui des écrans de facturation). Parcours §O mis à jour.
    - **Remplaçant vu en Play (06/10)** : la nouvelle fiche s'ouvrait vide (« ça m'ouvre un nouveau locataire tout juste ») — en création, `InitializeLocataire` ne lit pas les données, donc le lot et le début du bail repris ne s'affichaient pas (l'en-tête disait pourtant « Lot 1 ») et « Sauvegarder » les aurait perdus. Corrigé : en création, lot, surface et début du bail déjà connus sont affichés (sans effet sur un locataire neuf : lot 0, pas de date). 382 verts. Données : A remis sans départ (comme « Annuler le départ ») et fiche « Nouveau » du remplaçant retirée (ni facture, ni dossier, ni part de charge) par script hors Play ; sauvegarde `….json.avant-reset-A-remplacant-20261006-123045.bak`. B s'appelle « Dupont » (cession du 01/10/2026 faite en Play par l'utilisatrice, laissée).
    - **Cession datée dans le futur** (retour du 06/10 : « j'ai mis une date de cession après aujourd'hui mais ça override directement l'ancien locataire, il vaut mieux que le changement de locataire se fasse le jour de la cession ») : `Cessions.Enregistrer` applique tout de suite une date passée ou du jour, sinon garde `Locataire.cessionPrevue` ; `BatimentManager.EffectuerCession` l'applique au chargement à partir de son jour (renommage du dossier ; reportée si nom pris ou dossier bloqué) ; fenêtre : cession prévue retrouvée, modifiable, « Annuler la cession prévue » ; départ refusé avant une cession prévue. Test `Une_cession_future_attend_son_jour`. Limite (`ponytail`) : vérifiée au lancement seulement. **382 tests verts.** Données : les deux cessions faites sur B en Play avec l'ancien code (B → « O new locataire » → « O ») annulées par script hors Play — nom, SIRET, email, adresse d'origine, historique vidé ; ni dossier ni facture concernés ; sauvegarde `….json.avant-annulation-cession-B-20261006-114836.bak`.
    - **Erreurs au centre** (retour du 06/10 : « pour les panels qui indiquent un changement de statut ou informatifs c'est bien, mais pour un panel qui remonte une erreur il le faut plus voyant, centré ») : `ConfirmDialog.Erreur(message)` — la fenêtre de confirmation de la scène, titre « Attention », un seul bouton « OK » (`Show` réaffiche « Annuler » ensuite) ; repli sur le message du bas si la fenêtre manque. **Règle** : erreur, refus ou échec → `ConfirmDialog.Erreur` ; information, confirmation, « en cours » → `UndoToast.ShowInfo`. Appliquée aux ~60 erreurs de l'application (4 panneaux de facture : aperçu, PDF, email manquant, envoi échoué, refus ; départ, cession ; fiche : nom, bail, Siret, renommage ; parcours bloqué ; loyer : franchise, avenant ; bâtiment ; réglages ; sauvegarde, transfert ; saisie numérique). Restent en bas, volontairement : « Un envoi est déjà en cours », les consignes du parcours de création, et trois avertissements automatiques (carte Mapbox, emplacement de sauvegarde au démarrage, rappel sans email) qui ne doivent pas bloquer l'écran.
  - **06/10 (fin) — §O validé en Play par l'utilisatrice** (« j'ai tout testé, c'est bon ») : type de départ en menu déroulant, cession immédiate et à date future (annulation comprise), remplaçant sur le même lot, erreurs au centre. **382 tests verts.**
  - **06/10 — case « au prorata » de la refacturation** (décidée le 01/10) : `FactureRefacPanel._prorata`, visible si `ListesCharges.PresencePartielle` (année de la charge incomplète pour le locataire), cochée par défaut ; montants = `QuotePartAuProrata` ou `QuotePart` ; changer la case recalcule les lignes, une facture rouverte garde ses montants émis à l'ouverture. Test `La_refacturation_propose_le_prorata_si_le_locataire_n_a_pas_ete_la_toute_l_annee`. Prorata par cas confirmé par l'utilisatrice : (1) départ seul → régul et refac au prorata ; (2.1) cession → aucun ; (2.2) remplaçant → prorata, refac au choix. **383 tests verts.** Parcours P1 (C relu sur le disque : 848,22 attendu) — **validé en Play par l'utilisatrice le 06/10**.
  - **06/10 (clôture) — bâtiment « TEST Nouveautés » supprimé** (demande de l'utilisatrice), hors Play, comme `DeleteBatiment` : JSON retiré de `batiments/`, dossier du bâtiment déplacé dans `corbeille_batiments/a7d61d8d…_TEST Nouveautés/` avec une copie du JSON et les 5 `.bak` de test dans `_donnees/` (restaurable à la main). Les sauvegardes automatiques `Backups/` le contiennent encore. Restent dans DemoCIPL : rivoli, Parc Tertiaire Sud, Auterive, Le Hangar. Les sections L à P du parcours de test portaient sur ce bâtiment.
  - **Chantier « départ du locataire » terminé** (06/10) : trois cas validés en Play, 383 tests verts. **Reste** : le commit de l'utilisatrice (nouveaux fichiers non suivis : `CessionBail.cs`, `DepartLocataire.cs`, `DepartLocataireUI.cs`, `DepartLocataireTests.cs` et leurs `.meta`, `.gitattributes`, `.graphifyignore`). Prochaine session : listes de tâches / améliorations.
  - Piège d'outil : après création d'une classe de test, `tests-run` filtré par classe la dit introuvable (liste de tests en cache) ; la suite complète la trouve.

### Session du 06/10 (après-midi) — listes d'objectifs (module Objectifs / « À traiter »)

Sujet choisi par l'utilisatrice : le module de l'app. Diagnostic présenté (4 sujets) ; **seul le sujet 1, les défauts, est retenu** pour l'instant. Restent proposés, non décidés : modifier le texte d'un objectif et revoir / rouvrir les objectifs « Fait » ; échéance datée (« Rappel » n'a pas de date) ; séparer l'importance (Obligatoire) de l'avancement (À faire / En cours / Fait). Aujourd'hui, un clic fait passer un objectif « Obligatoire » à « En cours » et lui fait perdre son caractère obligatoire.
- **Origine en texte** dans la vue de tous les objectifs : « Bât. rivoli · Loc. Dupont » (choix de l'utilisatrice), au lieu de « 🏢 … › 👤 … ». Vérifié par script : LiberationSans et son fallback n'ont pas ces emojis (carrés à l'écran).
- **Locataire archivé** : ses objectifs sont masqués de la vue de tous les objectifs, comme dans « À traiter ».
- **Suppression annulable** (fiche et vue de tous les objectifs) : toast « Objectif supprimé · Annuler », qui remet l'objectif à sa place et réenregistre. La vue de tous les objectifs supprime désormais dans la liste d'origine de l'objectif, au lieu de le retirer de toutes les listes du bâtiment.
- Règles : `Objectifs.Origine` / `Objectifs.Supprimer` (`Objective.cs`) ; tests `ObjectifsTests` (2). **385 tests verts.** Parcours §Q **validé en Play par l'utilisatrice le 06/10**.
- **Puis tout le reste, plus les documents** (demande : « On attaque tout + la possibilité de lier des docs à une tâche » ; maquettes validées). Décisions :
  - les trois importances sont gardées (Normale / Rappel / Obligatoire) ; toute tâche peut avoir une échéance et une récurrence dès la création ;
  - « me prévenir N jours avant » est réglable par objectif (15 par défaut) ;
  - « À traiter » montre tous les objectifs non faits, triés : en retard, échéance proche, puis Obligatoire, Rappel, Normale ; à rang égal, par date ;
  - récurrence libre (tous les N jours / semaines / mois / ans). « Fait » crée un **nouvel** objectif à l'**échéance prévue + intervalle** (calendrier fixe, même fait en avance) ; l'ancien reste « Fait le … » avec ses documents ;
  - documents **copiés** dans `<bâtiment>[/<locataire>]/Objectifs/`, seul le nom est stocké ; plusieurs par objectif, sélection multiple dans l'explorateur.
  - Modèle (`Objective.cs`) : `importance`, `avancement`, `echeanceISO`, `prevenirJours`, `repeterTous` / `repeterUnite`, `faitLeISO`, `suivanteId`, `documents`, `version`. L'ancien `status` n'est plus lu qu'à la reprise : `Objectifs.Migrer` au chargement (`BatimentManager.LoadAll`, en mémoire, écrit au prochain enregistrement ; Obligatoire / Rappel → même importance, « À faire »).
  - Règles `Objectifs` : `Etat` / `TexteEcheance` / `Detail`, `Suivante`, `MarquerFait` / `Rouvrir` (rouvrir retire l'occurrence suivante si elle n'a pas servi) / `Avancer`, `Rang` / `Comparer`, `Verifier`.
  - Écrans : fenêtre `ObjectifPanel` (création et modification, Supprimer). `ObjectiveItem` : badge d'importance, ligne de détail, bouton d'avancement, clic sur le texte. `ObjectivesManager` : filtres Tous · Obligatoire · Rappel · Normale · En retard · Faits, la ligne de saisie est masquée. « À traiter » (`HomeAlert`) avec tri stable. Point d'alerte de l'onglet aussi pour un objectif en retard.
  - **Défaut corrigé** : la vue de tous les objectifs modifiait `BatimentManager.Batiments`, alors que chaque fiche travaille sur sa copie (clone JSON) ; une modification faite là pouvait être écrasée au prochain enregistrement de la fiche. Elle passe désormais par les fiches (`BatimentPrefab`), comme « À traiter », et rafraîchit la liste de la fiche.
  - Limites (`ponytail`) : un document joint puis « Annuler » reste copié sur le disque ; l'état d'échéance est recalculé à l'affichage (pas à minuit si l'application reste ouverte).
  - Tests `ObjectifsTests` (+7). **392 tests verts** (06/10). Pas encore vu en Play : parcours **§R**.
  - Demande suivante (« visualiser l'historique… quand ça passe en fait ils ne sont pas supprimés ; si on clique sur un objectif ça l'ouvre et on peut le modifier ») : déjà en place sur la fiche (filtre « Faits », clic sur le texte). Ajouté : toute la ligne est cliquable (bouton sur la racine de `ObjectiveItem`) ; filtre « Faits (n) » dans la vue de tous les objectifs (clone de « Tous »), du plus récent au plus ancien. Test `Un_objectif_fait_reste_dans_l_historique`. **393 tests verts.** Parcours R10b.
  - **Occurrences précédentes** (demande de l'utilisatrice) : section en bas de la fenêtre d'un objectif récurrent, la plus récente d'abord (« Fait le … · échéance … · n documents » + « Ouvrir », qui ouvre la fenêtre de cette occurrence par-dessus, sans perdre la saisie en cours). `Objectifs.Precedentes` remonte la chaîne des `suivanteId` (protégée contre une boucle) ; `Objectifs.TexteOccurrence`. Test `Les_occurrences_precedentes_se_retrouvent`. **394 tests verts.** Parcours R10c.
  - **Données de test** (demande de l'utilisatrice, choix : nouveau bâtiment) : **« TEST Objectifs »** dans DemoCIPL, créé par script éditeur hors Play — nouveau fichier `batiments/batiment_14ef4f0c-2891-449e-9d83-abcd122e6a87.json` (aucun autre fichier modifié), modèle = copie de TEST Nouveautés dans la corbeille (lue seulement), charges / factures / photos vidées, 2 locataires (« Dupont Objectifs », « Ancien Archivé » archivé), bail en 2027 (pas d'alerte de facturation). Objectifs : 4 à l'ancien format (version 0), retard, proche, lointain, sans date, chaîne mensuelle « Entretien chaudière » (2 faits + 1 en cours). 4 PDF minimaux dans `Batiment/TEST Objectifs/Objectifs/` et `…/Dupont Objectifs/Objectifs/`. Relu par script : tout conforme. Parcours R0-R0b. **À supprimer après les essais.**
  - **Retour en Play (06/10), deux défauts antérieurs à cette session** :
    - **la fiche locataire ne chargeait jamais ses objectifs** : `LocatairePrefab.InitializeLocataire` n'appelait pas `LoadObjectives` (la fiche bâtiment, si). Sa liste était vide, et `updateListObjectif` la recopiait sur le locataire : **ajouter un objectif depuis une fiche locataire effaçait les autres**. Corrigé, la liste de la fiche est celle du locataire ;
    - **un clic dans la carte « À traiter » du résumé ouvrait toujours la vue locataire** (`BatimentSummaryView`, `_ => ShowLocataireView()`), même pour un objectif du bâtiment. Désormais, à l'accueil comme dans le résumé, un objectif ouvre **sa fenêtre** (`ObjectifPanel.OuvrirAlerte`, `HomeAlert.objectif`) ; les autres alertes ouvrent la fiche du locataire concerné, sinon celle du bâtiment.
    - Enregistrement commun : `ObjectifPanel.EnregistrerFiche`, aussi utilisé par la vue de tous les objectifs. Parcours R0c.
    - Précision de l'utilisatrice : « ça doit ouvrir le bâtiment ou le locataire dédié et ouvrir la page sur l'objectif dédié ». `ObjectifPanel.OuvrirAlerte(a)` sélectionne l'onglet du bâtiment, ouvre la fiche du locataire (sinon la fiche complète du bâtiment), puis la fenêtre de l'objectif par-dessus ; l'accueil se masque d'abord. **394 tests verts.**
  - **06/10 (fin) — §R validé en Play par l'utilisatrice** (« j'ai testé R, c'est bon ») : reprise des anciens objectifs, échéance et « me prévenir », récurrence et occurrences précédentes, historique (« Faits »), documents multiples, clic → fiche dédiée + fenêtre, fiche locataire chargée. Chantier objectifs terminé. **Reste** : le commit de l'utilisatrice ; supprimer le bâtiment « TEST Objectifs » quand elle le souhaite.
  - **06/10 (soir) — présentation des filtres repensée** (demande : « mettre en avant les différents types de filtres : à faire, en cours, en retard / normal, rappel, obligatoire / fini »). Maquettes successives : pastilles sur deux lignes ou une ligne, puis tableau croisé (jugé « pas clair du tout »), puis tableau en colonnes type Kanban (retenu pour les fiches). Décisions :
    - **fiches** : tableau À faire · En cours · Fait (`ObjectifsTableau`, construit dans la section existante ; anciens filtres et liste masqués). Filtre d'importance en pastilles. Cartes : liseré et badge d'importance, étiquette retard / proche, « récurrent », « n doc. ». Déplacement au **glisser-déposer** (`CarteGlissable` : fantôme sous le pointeur, colonne sous le relâchement) **et** au bouton « › » (« Rouvrir » dans « Fait »). « Fait » : les 2 derniers, puis « Voir tout l'historique ». « En retard » est un sous-ensemble, pas une colonne ;
    - **rappels automatiques** (bail, révision, facturation) **en tête de « À faire »**, en cartes « Automatique » non déplaçables. Un clic ouvre l'écran concerné (`RappelsAuto.Ouvrir` : facture du loyer, régul, dépôt / restitution, départ, révision ; fin de bail : la fiche). `HomeAlert.facturation` porte le type. La fiche du bâtiment montre ceux de tous ses locataires, avec leur nom. « À traiter » (accueil, résumé) reste une liste qui mélange tout ;
    - **liste de tous les objectifs** : 5 tuiles d'avancement (Tous · À faire · En cours · En retard · Faits) avec leur nombre, et pastilles d'importance dessous ; les deux se combinent.
    - Règles : `Objectifs.FiltreAvancement` / `Garde(o, avancement, importance)`, `Colonne`, `Deplacer`. `ObjectivesManager.Filtre` / `Garde` supprimés. Tests `Les_filtres_avancement_et_importance_se_combinent` et `Le_tableau_range_et_deplace_les_cartes`. **396 tests verts.** Pas encore vu en Play : parcours **§S**.
    - **07/10 — retour visuel en Play** (« c'est visuellement pas bon ») : pastilles (nombre des colonnes, importance, échéance) en grosses bulles, texte collé en haut. Cause : le sprite arrondi d'`UIFactory.Rounded` (9-slice, coins de 24) impose sa taille minimale (48 px) via l'`ILayoutElement` de l'Image, au-dessus de celle du texte. Correctif : `pixelsPerUnitMultiplier` (pastilles 4 → coins de 6 ; bouton « › » 3 ; pastilles d'importance 2,5 ; cartes, colonnes et tuiles 2), texte centré dans la pastille. **Insuffisant** (2e capture : pastilles toujours en blocs) : le multiplicateur ne change que le dessin ; la taille minimale vient de `Image.preferredWidth` (bordure du sprite / pixelsPerUnit, sans le multiplicateur). Correctif réel : le fond de la pastille est un enfant étiré en `ignoreLayout`, la pastille prend la taille de son texte. Rapprochement de la maquette : bouton « › » de 26 px, libellé « Importance » retiré, pastilles d'importance à la hauteur de « + Ajouter » (34). Garde dans `RefreshList` si les colonnes ont été perdues par un rechargement du code en cours de jeu (NullReferenceException vue le 07/10, compilation lancée par erreur pendant le Play). **396 verts.**
    - **07/10 — retour suivant** (« que se passe-t-il si on a plusieurs objectifs ? il faut un scroll rect ; on peut enlever le sous-menu facturation pour mettre les boutons sur le suivi et étendre objectif » ; maquette validée ; « le défilement doit être général sur objectif, pas colonne par colonne » ; hauteur fixe ~5 cartes) : le tableau est dans **un** `ScrollRect` (`TableauDefilement`), hauteur plafonnée à 360 et réduite au contenu, mesurée à l'image suivante (`AjusterHauteur`) ; défilement interne coupé quand tout tient, pour que la molette fasse défiler la fiche. **Carte « Facturation » supprimée** de la fiche locataire : `LocataireFacturationFields` construit le suivi d'abord, puis `FacturationActions` (alertes + 4 boutons sur une ligne) en tête du corps du suivi ; `RefreshAlertes`, pastilles et libellé « Décompte de sortie » inchangés.
    - **07/10 — « la tâche en cours doit rester de la même longueur que les autres »** : une carte seule dans sa colonne s'étirait sur toute la hauteur. Cause : `childForceExpandHeight` du groupe horizontal de la carte (nécessaire pour que le liseré suive le texte) donne à la carte une hauteur flexible de 1, que la colonne remplit. Correctif : `LayoutElement.flexibleHeight = 0` sur la carte (`ObjectifsTableau.Squelette`, cartes d'objectif et « Automatique »).
    - **07/10 — liseré et largeurs** (« la barre de couleur dépasse de la tâche ; les colonnes sont flexibles, certaines ne sont pas égales, et ça fait bouger les 2 grandes colonnes ») : `Mask` sur la carte (le liseré rectangulaire suit l'arrondi) ; colonnes du tableau en largeur préférée 0 (parts égales, quel que soit le texte). Mesure en Play : les deux colonnes de la fiche locataire se partageaient la largeur d'après leur largeur préférée (Loyer 764 contre Objectif 487 → 1072 / 796 px), donc toute section qui changeait de largeur déplaçait la séparation ; `ReorganizeColumns` leur donne maintenant une largeur préférée de 0 : la séparation ne bouge plus avec le contenu. **Pas exactement moitié / moitié** (annoncé à tort) : Unity part du minimum de chaque colonne (504 / 267) et ajoute la même part de place libre. Le minimum n'est volontairement pas forcé : `ColonnesAdaptatives` (empilement des colonnes aux zooms 150-175 %) ne fonctionne que si chaque colonne annonce son vrai minimum.
    - **07/10 — zoom** (« il ne faut pas que les colonnes fixes cassent la feature zoom ») : `UIZoom` agit sur la résolution de référence, donc toutes les tailles en unités (360 de haut, 26 du « › ») suivent ensemble ; le risque est la largeur (1097 unités à 175 %). Les cartes passent sur deux lignes (importance + échéance ; récurrence · documents · « › ») ; le détail d'une carte « Automatique » devient un texte qui passe à la ligne ; colonnes du tableau à 180 de largeur minimale (`ObjectifsTableau.LARGEUR_MIN_COLONNE`), et en dessous le tableau **défile en largeur** au lieu d'écraser les cartes (`AjusterHauteur`, recalculé quand la largeur change via `LateUpdate`). Test `ObjectifsTableauTests` : les cartes les plus chargées tiennent dans une colonne minimale. Premier passage : « Obligatoire · En retard de 15 j » mesurait 180 pour 144 disponibles → étiquette courte sur la carte (« Retard 15 j » ; la ligne de détail et « À traiter » gardent la forme longue) et colonnes à 190. **397 tests verts.**
    - **07/10 — retours en Play** (« la fiche résumé s'est agrandie ; en zoom 175 regarde objectif dépasse ») :
      - **175 %** : les deux grandes colonnes restaient côte à côte et le tableau était coupé : la section Objectif annonçait toujours ~270 de minimum alors que son tableau en exige ~590 (3 × 190 + espacements). La zone du tableau annonce désormais ce vrai minimum (`LayoutElement.minWidth`), `ColonnesAdaptatives` empile donc les colonnes à 175 %. À 100 % la colonne de droite devient un peu plus large que la gauche (~980 / ~860 sur 1920). Le défilement horizontal reste en secours. Constaté au passage, antérieur : à 175 %, le texte « Mois facturés » de la carte Loyer chevauchait les mois. **Corrigé à la demande de l'utilisatrice** (`LoyerSummaryUI.Row`) : le libellé garde sa largeur (sans retour à la ligne), la valeur prend le reste et passe à la ligne, alignée en haut à droite ; le bloc « Modalités du loyer » annonce son vrai minimum (libellé le plus long + 8 + 110 + marges, au lieu de 200) pour que `ColonnesAdaptatives` empile à temps.
      - **Incident (07/10, 18 h 12)** : ce correctif, écrit pendant un Play, a été compilé au Play suivant **sans passer par les tests** (entrer en Play compile les scripts modifiés). Il mesurait les libellés (`GetPreferredValues`) à la construction ; or la fiche locataire est construite inactive, le texte n'a pas encore sa police → `NullReferenceException` → les 5 bâtiments de DemoCIPL « illisibles » au chargement (accueil vide, « 5 bâtiments · 0 locataire »). Données intactes (fichiers seulement ignorés, rien réécrit). Correctif : mesure à l'image qui suit l'affichage de la fiche (`OnEnable` → `MesurerLibelles`), seulement si la police est prête, sous `try` (un défaut d'affichage ne bloque plus rien). Seules les lignes visibles sont mesurées (franchise, départ, CA, régul peuvent être masquées), à chaque affichage et après chaque rafraîchissement.
      - **Vérifié en Play (lancé par l'assistant, 18 h 17-18 h 20, rien modifié ; zoom remis à 100 %)** : 5 bâtiments chargés, aucune erreur. Fiche de Dupont à 100 % : colonnes 869 / 999, tableau en 3 colonnes de 312, cartes de 76, plafond 360 ; bloc Loyer : minimum mesuré 277 (au lieu de 200). À 175 % : les deux grandes colonnes s'empilent (1057 chacune), tableau 1013 de large en 3 colonnes de 331, rien de coupé. **397 tests verts.**
      - **« Regarde l'objectif du bâtiment »** : dans la fiche complète du bâtiment, le tableau ne faisait que 100 de haut (le haut d'une carte visible). La section `ObjectivesPanel` du prefab bâtiment ne pilote pas la hauteur de ses enfants (`childControlHeight = false`, contrairement au prefab locataire) : le tableau gardait la taille par défaut d'un objet créé par code. `Construire` règle désormais `childControlHeight = true` / `childForceExpandHeight = false` (seuls filtres et tableau y sont actifs). Test `Le_tableau_a_sa_hauteur_dans_la_fiche_batiment_et_la_fiche_locataire` (construit le tableau dans les deux prefabs). **398 tests verts.** Vérifié en Play par l'assistant : 1847 × 360, défilement actif, cartes entières.
      - **« L'objectif du bâtiment n'a pas le même visuel que celui du locataire »** (cartes grises) : reproduit par le parcours résumé → « Fiche complète ». Cause : le `Mask` posé sur chaque carte (pour arrondir le liseré) s'imbriquait dans le `Mask` à stencil de la page du bâtiment (la fiche locataire n'en a pas) → cartes mal dessinées. Le liseré devient une fine barre arrondie **dans** la carte, en retrait des coins, hors mise en page ; plus de `Mask`. Au passage, dans les deux fiches, le contenu du tableau démarrait décalé de 28 (en-têtes de colonnes rognés) : il est remis en haut à l'affichage de la fiche et au changement de filtre, pas au déplacement d'une carte. **398 verts** ; vérifié en Play (captures des deux fiches identiques, contenu à 0).
      - **Toujours gris chez l'utilisatrice** (pas reproduit par l'assistant par code) : inspecté en direct dans son Play. `Image.color` blanc mais `CanvasRenderer` à (0,784 ; 0,784 ; 0,784 ; 0,5), la teinte « désactivé » par défaut d'un `Button`. Ajouter un `Button` applique aussitôt la teinte de son état (OnEnable) ; si un `CanvasGroup` parent est non cliquable à cet instant (fiche du bâtiment en transition), la carte naît « désactivée », et la transition étant ensuite coupée (`None`), le gris restait. Correctif : `ObjectifsTableau.Cliquable` (bouton sans effet, teinte du rendu remise à blanc), utilisé par les cartes, les cartes « Automatique », les lignes `ObjectiveItem` et les tuiles de la liste globale. Test `Une_carte_creee_dans_un_groupe_non_cliquable_reste_blanche` (sans correctif, la même construction dans l'éditeur donne exactement (0,784 ; 0,784 ; 0,784 ; 0,502) : le test protège bien). **399 tests verts.** Confirmé en Play par l'utilisatrice (« c'est bon »).
      - **Fiche locataire, deux colonnes de même hauteur** (demande : « je veux que les deux colonnes aient la même longueur » → même hauteur) : `ReorganizeColumns` met `childForceExpandHeight` sur la rangée et donne la place restante à la dernière section de chaque colonne (`EtirerDerniere` : Dépôt à gauche, Objectif à droite ; les autres sections en `flexibleHeight` 0). Vérifié en Play : colonnes de 1031, bas alignés, Dépôt allongé de 195 à 230, aucun blanc ajouté. 399 verts. Remarque laissée à l'utilisatrice : la molette au-dessus du tableau fait défiler le tableau au lieu de la page (en-têtes de colonnes rognés) ; en-têtes fixes proposés.
      - **En-têtes fixes** (« oui mets les en-têtes fixes ») : rangée `EnTetesColonnes` (3 cases `ObjectifsTableau.CreerEnTete`, mêmes largeurs que les colonnes) juste au-dessus de `TableauDefilement`, hors défilement ; `RefreshList` y écrit les en-têtes (nom, nombre, « n en retard ») ; texte centré verticalement. Vérifié en Play : tableau défilé de 120, en-têtes entiers. Test des deux fiches complété (rangée présente, juste au-dessus du défilement, 3 cases). Limite (`ponytail`) : dans le cas de secours où le tableau défile en largeur (colonnes sous leur minimum), les en-têtes ne suivent pas le défilement horizontal. **399 verts.**
      - **Menus déroulants au zoom ≠ 100 %** (« le dropdown est un peu dans les choux », vu à 175 % dans la fenêtre d'un objectif) : défaut **antérieur, commun à tous les `UIDropdown`** et au menu d'état du suivi. `UIFactory.PlacePopup` posait les coins « monde » du champ (pixels d'écran) comme position du menu (unités d'interface) ; à 175 % (canevas à l'échelle 1,75, mesuré en direct : champ en 449 ; 640, menu posé en 786 ; 1120), le menu partait en haut à droite. Correctif : coins convertis dans le repère du parent du menu (`InverseTransformPoint`). Test `UIZoomTests.Un_menu_deroulant_s_ouvre_sous_son_champ_au_zoom_175`. **400 verts.**
      - **Vocabulaire** (« importance n'est pas le bon terme… mettre Tâche, Obligatoire et Rappel » ; terme choisi : « Catégorie ») : libellés affichés seulement — « Catégorie » (fenêtre de l'objectif, liste de tous les objectifs) et « Tâche » au lieu de « Normale » (badges, filtres, « À traiter »). Valeur interne et données inchangées (`Objective.Importance.Normale`, stockée en entier).
      - **« À traiter » refait** (menu principal et résumé du bâtiment ; visuel choisi : les tuiles) : rangée `TuilesATraiter` (Tout · En retard · À venir · Automatiques · Objectifs, nombre + libellé, tuile active en couleur pleine) au-dessus de la liste ; règle `ATraiterFiltre.Garde` (objectif : son échéance ; rappel automatique : priorité 0 = en retard, sinon à venir). Accueil : tuiles insérées avant le `ColHeader` de la scène (`GeneralMenuPanel.BuildAlertes`) ; résumé : en tête de la carte (`BatimentSummaryView`). Le compteur rouge reste le total. Test `Les_tuiles_a_traiter_classent_objectifs_et_rappels`.
        Sous les tuiles (08/10, demandé sur la maquette « Tous les objectifs ») : pastilles **Catégorie** Toutes · Obligatoire · Rappel · Tâche, combinées aux tuiles (les nombres des tuiles suivent la catégorie). Un rappel automatique compte comme « Rappel ». Filtre unique : `TuilesATraiter.Garde(a, auj)`.
      - **Tableau des fiches : pastille « Automatique »** (08/10) : Toutes · Obligatoire · Rappel · Tâche · Automatique. Les cartes automatiques suivent la même règle qu'« À traiter » (`ATraiterFiltre.Garde`) : visibles avec Toutes et Rappel, cachées par Obligatoire et Tâche ; « Automatique » ne montre qu'elles (`ObjectivesManager._seulementAuto`). Validé en Play le 08/10 (S6g).
      - **« Anciens locataires »** (résumé du bâtiment) : la case ronde du `Toggle` débordait sur le bord de la carte → bouton pastille (« Anciens locataires (n) › » clair, « Masquer les anciens (n) » foncé une fois ouvert ; ▸ / ▾ absents de la police).
      - Compilé, **401 tests EditMode verts** (08/10).
    - **Paiement partiel d'une facture** (08/10, maquettes validées : versements datés + état « Partiel ») — détail dans FACTURATION §16.2. Modèle : `FactureEtat.versements` (`Versement` : `dateISO`, `montant`), sur la ligne porteuse (`FacturationSuivi.Porteuse`). Règles testées dans `FacturationSuivi` : `Solde`, `EtatAffiche` (« Partiel » = affichage dès qu'une facture envoyée ou impayée a un versement — retour du 08/10 ; `EtatDe` inchangé), `StatutApresVersements`, `VerifierVersement` ; `DuesDe` renvoie le **reste** (Créances, décompte de sortie, archivage). Écran : `VersementsPanel` (nouveau), entrée « Paiement partiel… » du menu d'état (`LocataireSuiviInline`), montant « reste X / sur Y » sur deux lignes (une seule ligne : 209 px pour une colonne de 140), pastille Partiel aussi dans Créances, rappel d'échéance qui réclame le reste. `DepartPanel.Entete` passe `internal` (réutilisé). 5 tests, **406 verts**. Vérifié en Play : fenêtre ouverte sur le loyer d'août de Starbucks Coffee (rivoli), saisie « 4 000,50 » lue, reste recalculé, « Annuler » n'écrit rien. Retours du 08/10 : pastille « Partiel » aussi pour une partielle échue (l'état réel reste Impayé) ; « Retirer » devenu un vrai bouton (fond rouge pâle + bordure : en texte seul on ne le voyait pas). Parcours T **validé en Play le 08/10**, 406 tests verts.
      - **Résumé du bâtiment** : la carte « À traiter » affichait toutes les alertes sans limite (16 avec les objectifs de TEST Objectifs), et agrandissait tout le résumé. En-têtes fixes, puis lignes dans un défilement plafonné à 300 (~8 lignes), réduit au contenu sinon, défilement coupé quand tout tient (`BatimentSummaryView.AjusterATraiter`).

### Où en est le chantier au 29/09 (soir) — **310 tests EditMode verts**

**Session du 29/09 (après la mention TVA)** — détail des règles dans `FACTURATION_CIPL_PENNYLANE.md` (§5, §13 « Charges : état par locataire… », §16) :
- **Pennylane, dépôt (Voie 2)** : `PennylaneClient` — client retrouvé par SIREN puis mis à jour (sinon créé), notre PDF, import incomplet + Factur-X. Branché sur le Loyer en mode Pennylane. **Testé de bout en bout** (facture incomplète, Factur-X EN 16931, rien de transmis). Finalisation / `send_to_pa` **non écrits** (attente Pennylane).
- **Transfert entre entreprises** (`TransfertEntreprise` + bouton « Transférer » des fiches bâtiment / locataire) : fusion par nom, dossiers copiés, origine en corbeille, listes de charges retrouvées par nom. **Validé en Play** (aller-retour DemoCIPL ↔ Test transfert).
- **Lieu d'émission** réglable (Textes fixes).
- **Défaut majeur corrigé — l'état facturée/payée d'une charge était commun à tous les locataires** : régulariser ou refacturer une charge partagée pour un locataire la retirait de la régularisation des autres (quote-part perdue ; constaté sur la taxe foncière 2025 de rivoli). Désormais par locataire (`ChargeBatiment.facturations`), ancien format reconstitué au chargement d'après le suivi.
- **Correction d'une régularisation** : reprend les charges qu'elle couvre (sortait à 0 €).
- **Listes de charges** (Réglages) : date + provision par liste et par locataire, choix des listes par locataire (au moins une), provisions par liste sur le loyer, régularisation d'une / plusieurs / toutes les listes en **une seule facture**.
- **Revue de fin de session — 4 défauts trouvés et corrigés** : régul regroupée pouvant refacturer des charges déjà facturées (garde `FactureRegulPanel.Regroupement`) · régul émise disparaissant du suivi si sa liste n'est plus datée · choix de listes perdu au transfert · régul vide possible sans liste datée.
- **Case « Provisions » à 0 px** (régression du commit « add zoom ») : corrigée dans le prefab ; nouveau test sur toutes les cases à cocher de tous les prefabs.
- **Titres des 9 tuiles Rentabilité disparus** (même famille) : tuiles fixées à 54 px pour 55,5 px de contenu → les titres, en « … », étaient masqués **en entier** par TMP. Tuiles passées en hauteur minimale 54 px (`BatimentPrefab`) ; test `Un_groupe_de_hauteur_fixe_contient_ses_textes_a_points_de_suspension` sur tous les prefabs. Piège noté : sur un prefab non instancié, TMP annonce une hauteur préférée de 0 — le test mesure donc avec `GetPreferredValues`.

**Vu en Play le 29/09** : dépôt Pennylane · transfert · case Provisions + listes dans « Gestion du loyer » (en cours). **Pas encore vu** : panneau Loyer avec plusieurs provisions · régularisation à cases · fiche charge avec choix de liste · section Réglages « Listes de charges ».

**Mention TVA (29/09)** : la case « TVA payée sur les débits » des 4 panneaux devient un menu *Aucune mention / débits / encaissements*. Les phrases de base sont dans Réglages → *Textes fixes*, et une facture peut les remplacer pour elle seule ; seul le remplacement est gardé sur la facture. Code commun : `MentionTva` ; 10 tests (`MentionTvaTests`). Détail et limite connue dans `FACTURATION_CIPL_PENNYLANE.md`. **Validé en Play.**

Le détail de la facturation est dans `FACTURATION_CIPL_PENNYLANE.md` ; voici l'essentiel pour reprendre.

**Fait et validé en Play** : réorganisation des 4 panneaux de facture selon l'ordre du document imprimé · héritage en chaîne des réglages d'un locataire au suivant (T1 → T2 → T3, à travers les bâtiments) · textes du document réglables sur la facture, avec variables et menu « / » · **envoi réel par email**, branché sur les 4 panneaux.

**Le défaut majeur de la soirée — voir C4** : `FactureInfo` n'était pas `[Serializable]`, donc **aucun réglage de facture n'a jamais été écrit sur le disque**. Corrigé et verrouillé par des tests. Conséquence pratique : tout réglage saisi avant le 21/09 est perdu et doit être re-saisi une fois.

**Envoi par email — état réel** : fonctionne, validé avec **Gmail**. L'adresse `@cipl.fr` est **bloquée** : le locataire Microsoft 365 de l'entreprise interdit les mots de passe d'application et n'a pas SMTP AUTH activé, et le compte de l'utilisatrice n'est pas administrateur. Il faut l'admin du locataire, ou un compte OVH (le SPF de `cipl.fr` l'autorise déjà).

**À reprendre** : le tour en Play des panneaux Régularisation / Refacturation / Dépôt (seul le Loyer a été vu de bout en bout) · le rendu de la ligne de suivi passée en prefab (`SuiviFactureRow`, fait le 21/09 au soir — reste à voir à l'écran) · **H1**, la numérotation, toujours en attente de l'expert-comptable.

**Piège d'outillage** : le lanceur de tests par MCP se bloque si Unity est en **mode Play** ou si une scène est **non enregistrée** — il reste alors verrouillé jusqu'à une recompilation. Sortir du Play et enregistrer la scène avant de lancer.

### Décisions prises, et leur motif

| Décision | Motif |
|---|---|
| **H1 (numérotation des factures) laissé de côté** | Deux locataires facturés le même mois peuvent obtenir le même numéro. Le choix entre séquence unique globale et identifiant stable par locataire engage la conformité (art. 242 nonies A du CGI) → **en attente de l'expert-comptable**. Ne pas trancher seul. |
| **Dossiers nommés par nom seul**, pas nom + identifiant | Lisibilité maximale dans l'explorateur, au prix d'une **unicité imposée** : deux bâtiments homonymes sont refusés, deux locataires homonymes sont autorisés **s'ils sont dans des bâtiments différents**. |
| **`double` limité aux gros montants** | Mesuré : `float` est exact au centime sous 1 M€. Convertir loyers et charges aurait touché toute la facturation pour un gain nul. |
| **Émission bloquée sur montant négatif** | Un trop-perçu est un avoir, pas une facture. Volontairement strict — **peut être assoupli en simple confirmation** si un cas réel se trouve bloqué. |
| **Migration automatique au démarrage** | Valide tout son plan avant de bouger quoi que ce soit, sauvegarde préalable, marqueur écrit seulement en cas de réussite complète. |

### Vérifié en exécution réelle, versus seulement compilé

C'est le point le plus important de cette passation. Un `SaveAll()` livré « compilé » s'est révélé cassé au premier lancement (itération d'une `List<T>` pendant sa modification) : **compiler ne prouve rien**.

**Testé pour de vrai** : écriture atomique (`.tmp` → `.bak`, récupération d'un JSON tronqué) · nommage des dossiers (15 cas, pièges Windows) · renommage et refus de collision · homonymes entre bâtiments · aller-retour `double` sur disque · `SaveAll` · comportement du `??` · culture décimale · **(16/09, seconde session)** logique de correction / double émission (11 cas) · refus de migration vers un dossier peuplé (12 cas) · saisie de montants invalides.

**Compilé mais jamais exécuté** : gardes d'avoir sur régularisation et dépôt (le refus est écrit dans les deux `SauvegarderEtEnvoyer`, mais il passe par l'UI et n'a jamais été déclenché) · avertissement de fermeture (`FermetureGuard`, dépend de `wantsToQuit`) · parcours UI complet des 3 panneaux de facture — la logique sous-jacente, elle, est désormais testée.

### Ce que le passage en exécution a trouvé (16/09, seconde session)

« Compiler ne prouve rien » s'est vérifié une seconde fois : exécuter H2 a montré que le correctif **fermait le mauvais périmètre** — il protégeait les états Envoyé/Impayé, en laissant dehors le cas le plus courant du panneau Loyer. Détail en **H2-bis** dans le tableau des corrections.

### C4 — le réglage des factures n'était jamais enregistré (21/09/2026)

**CONFIRMÉ sur le fichier de sauvegarde réel** · `Facturation/FactureInfo.cs`

`[Serializable]` n'était pas sur `FactureInfo` : l'attribut avait glissé sur `FactureNumerotation`, une classe **`static`** où il ne sert à rien, parce qu'elle avait été insérée entre le commentaire de `FactureInfo` et son attribut. Syntaxiquement valide — **aucun avertissement**.

`JsonUtility` ignorait donc les quatre champs `factureLoyer / factureRegul / factureRefac / factureDepot`. Vérifié sur `batiment_*.json` : `factureSeq` et `facturesEtat` présents, **aucune** des quatre clés. RIB, entête, format de numéro, cases, textes, adresse d'envoi — tout tenait en mémoire puis disparaissait dès que les données repassaient par le JSON, ce que font les prefabs en permanence.

Trouvé par l'usage, pas par la relecture : « j'ai coché envoyer par email et je n'ai rien reçu ». La trace d'appel montrait la branche « pas d'envoi demandé », donc une case lue à `false` alors qu'elle avait été cochée — c'est en cherchant pourquoi que le fichier a livré la réponse.

Correctif : attribut remis sur la classe, commentaire expliquant pourquoi il est vital. `FactureInfoSerialisationTests` verrouille les deux faces : les réglages survivent à un aller-retour JSON, et les quatre clés apparaissent dans le texte produit.

*Même famille que C1/C2/C3 : une perte de données silencieuse. Elle rejoint aussi la leçon de `SaveAll` — compiler ne prouve rien, et ici même les 115 tests d'alors ne prouvaient rien, aucun ne relisant un modèle depuis le disque.*

### Le résumé d'entreprise se met à jour tout seul (22/09/2026)

Le bouton « résumé société » de la fiche locataire interrogeait l'annuaire des entreprises (`recherche-entreprises.api.gouv.fr` — pas Pappers, malgré le nom des champs ; le bouton « Pappers », lui, ouvre simplement le site et reste en place). L'information n'était donc à jour que là où quelqu'un avait pensé à cliquer : jamais sur les fiches qu'on ouvre rarement, précisément celles qu'on n'a pas en tête.

Le bouton disparaît. La mise à jour se déclenche **au lancement pour tous les locataires**, et **à l'enregistrement d'une fiche** pour celui qu'on vient de modifier — le SIRET vient peut-être d'y être saisi ou corrigé, attendre le prochain démarrage pour en voir l'effet serait déroutant. Les deux chemins passent par le même appel (`PappersSync.Interroger`), donc par la même règle.

L'écriture devenant massive et invisible, trois garde-fous :

- **Les notes de l'utilisatrice survivent.** Seul le bloc délimité par `-- PAPPERS --` / `-- FIN PAPPERS --` est remplacé ; ce qui l'entoure est conservé, et le bloc ne se duplique pas à chaque lancement.
- **Rien n'est écrit si rien n'a changé.** Un démarrage sans nouveauté chez l'annuaire ne réécrit aucun fichier de sauvegarde.
- **Un échec n'arrête pas les autres.** SIRET inconnu, réseau coupé : avertissement nommant le locataire, et la boucle continue.

Au démarrage, les appels s'enchaînent **en série, espacés de 0,4 s**, en coroutine : l'API publique n'est pas martelée et l'écran reste utilisable. Seuls les SIRET d'au moins 9 chiffres sont interrogés.

La logique du résumé a quitté `LocatairePrefab` — elle y était **privée**, donc inutilisable sans fiche ouverte, ce qui interdisait toute mise à jour en masse. Elle vit dans `PappersResume`, et devient testable : 9 tests, dont celui qui compte, « un bloc existant est remplacé sans toucher aux notes qui l'entourent ».

### Lenteurs d'interface : mesurées, puis corrigées (22/09/2026, soir)

Deux lenteurs signalées à l'usage. Dans les deux cas, **ce qui paraissait lent ne l'était pas** — sans chronomètre, j'aurais optimisé le mauvais endroit.

#### « Modifier » puis « Sauvegarder » : 2 866 ms → 128 ms

| Poste | Avant | Après |
|---|---|---|
| `InitializeLocataire` | 1 604 ms | **10 ms** |
| `sections.SetOpen` | 599 ms | **1 ms** |
| `SaveAfterModify` | 659 ms | **117 ms** |
| **Total** | **2 866 ms** | **128 ms** |

**Le coupable principal : `LayoutRebuilder.ForceRebuildLayoutImmediate`.** `InputAndText` recalculait le layout *sur place, à chaque niveau de la hiérarchie*, pour **chacun des seize champs** que la fiche réinitialise. `CollapsibleSection` faisait de même, d'où les 599 ms des sections. Les deux passent par `MarkLayoutForRebuild` : Unity regroupe les demandes et reconstruit **une fois en fin d'image**. Le rendu est identique — ce qui changeait, c'était le nombre de fois qu'on le calculait.

**Second coupable, le mien** : `LocataireSuiviInline.RefreshTous`, ajouté le matin même, reconstruisait les **8** tableaux de suivi alors qu'**un seul** est affiché (356 ms), et deux fois pour celui-là — une fois à l'enregistrement du bâtiment, une fois à la réinitialisation de la fiche. La reconstruction est désormais **différée** : `Refresh()` marque, `LateUpdate` reconstruit une seule fois, et seulement si le suivi est visible.

*Garde posée au passage* : un suivi masqué ne reçoit plus `LateUpdate`, donc il ne se rallumerait jamais — exactement le défaut corrigé le matin, qui revenait par la fenêtre. `Refresh()` réactive l'objet dès que le locataire existe.

Les 117 ms restantes sont l'écriture du JSON sur disque : incompressible et légitime.

#### Ouvrir un bâtiment ou un locataire : c'était le réseau

Mesures : basculement d'onglet **0 ms**, `ShowFiche` **19 ms**, `OnEnable` sous 2 ms. Le code n'était pas en cause. La console, elle, montrait quatre géocodages et trois téléchargements de tuile Mapbox **pour une seule adresse inchangée**.

- **`MapController.SetAdress` lançait la recherche deux fois** : elle posait `_pendingAddress` — que `Update()` déclenche dès que l'objet devient actif — *et* démarrait la coroutine dans la foulée. Le commentaire « passe l'adresse directement, pas de DelayedSearch » raconte l'histoire : l'appel direct a été ajouté sans retirer le mécanisme existant.
- **Aucune carte n'était réutilisée** : rouvrir une fiche relançait géocodage et téléchargement pour une adresse déjà affichée. La carte est conservée tant que l'adresse ne change pas (`_adresseAffichee`).

**Reste possible** : les vignettes du menu d'accueil (`MapThumbnailService`) géocodent les quatre bâtiments à chaque retour à l'accueil — non signalé, même remède si besoin. Et **19 autres `ForceRebuildLayoutImmediate`** subsistent (Achat, Travaux, PLU, résumé de bâtiment, objectifs) : ils n'ont pas été touchés faute d'être sur un chemin mesuré.

**À vérifier en Play** : ces `ForceRebuildLayoutImmediate` avaient probablement été posés pour résoudre un vrai problème d'affichage (les `ContentSizeFitter` imbriqués en produisent). Contrôler la hauteur des champs, le défilement des fiches et l'ouverture des sections repliables. En cas de souci, revenir à un rebuild forcé **une seule fois** en fin de série, et non par champ.

## Nettoyage des prefabs — préparer le réglage de taille de police (23/09/2026)

**But demandé** : pouvoir un jour agrandir ou réduire la police depuis un réglage. Impossible tant que
les prefabs ne laissent pas la hauteur remonter : si un maillon de la chaîne ne mesure pas ses enfants,
un texte plus grand déborde au lieu de pousser son parent.

**Ce qui bloquait, mesuré et non supposé** — trois causes distinctes, chacune trouvée en mesurant :

1. `AddComponent<VerticalLayoutGroup>()` laisse `childForceExpandHeight` à **`true`** par défaut. Un
   groupe dans cet état annonce `flexibleHeight = 1`, et cette réclamation **remonte toute la chaîne** :
   un `Row` invisible au fond d'une section gonflait le panneau entier. C'est l'origine de la quasi-
   totalité des blancs fantômes.
2. `childControlHeight = false` : le groupe se fie à la taille *courante* de ses enfants, jamais à leur
   taille *souhaitée*. Un texte agrandi ne pousse rien — c'est le blocage de fond pour le réglage de police.
3. **120 textes** finissaient par `\n`. TMP implémente `ILayoutElement` : un retour à la ligne final
   réclame une ligne de plus. Un `« General\n »` coûtait la hauteur d'une ligne, quatre-vingt-une fois.

**La quatrième cause, trouvée en cassant l'écran deux fois de suite** : un `ScrollRect` n'a pas de
hauteur préférée — il est dimensionné par son parent, jamais par son contenu. Le conteneur qui
l'enveloppe annonce donc **0**, et le groupe au-dessus doit lui donner sa hauteur autrement. Deux
mécanismes le permettent : `childForceExpandHeight`, ou un `LayoutElement` sur le maillon. En retirant
l'expansion **partout**, j'ai supprimé le seul qui était en place à deux endroits :

| Groupe | Effet | Correctif |
|---|---|---|
| `Content Prefab` → `Information Batiment` | tombé à 10 px (son seul padding), Viewport à −7 | `ctrlH=false` sur le maillon **et** `expH=true` restauré |
| `VueLocataire` → `ContentLocataire` | tombé à **0** ; le panneau vide recouvrait toute la fiche | `expH=true` restauré |

**Le second s'est caché au premier correctif** : son `ScrollRect` n'est pas l'enfant *direct* du groupe,
et mon premier test ne regardait que les enfants directs. D'où la leçon la plus utile du lot —
`childForceExpandHeight` n'est pas toujours un défaut. Il gonfle les panneaux quand il est posé par
inadvertance (le défaut n° 1), mais il est parfois **le mécanisme voulu** qui remplit un conteneur de
scroll. Les deux usages se ressemblent dans l'inspecteur ; seule la présence d'un `ScrollRect` en
descendance les distingue.

**Un prefab est resté volontairement hors de la migration** : `Investissement List Panel`, le panneau
d'historique (achats, travaux, charges). Ses trois groupes `invest`, `Body`, `Content` sont revenus à
`ctrlH=0, expH=1`, leur état d'origine, parce que la migration rendait les panneaux nettement plus
petits à l'écran. La raison est structurelle : **son contenu est construit au runtime**, donc le prefab
vide mesure 0 et aucune mesure hors Play ne renseigne sur le rendu réel. Le nettoyage des textes et des
overrides y reste acquis ; seul le layout est en attente. À reprendre avec l'écran sous les yeux, en
même temps que les `ContentSizeFitter` — il en empile trois (`invest`, `Body`, `Content`), ce qui est
probablement la vraie cause.

**Les trois règles sont désormais des tests** — `Assets/Editor/Tests/PrefabLayoutTests.cs`, exécutés
sur les 26 prefabs : tout conteneur de scroll peut recevoir une hauteur (en remontant la chaîne de
parents, `Template` de dropdown écartés car placés à la main) · aucun texte ne finit par un retour à
la ligne · aucun override `m_text` ne répète sa source. Vérifiés par mutation : remettre le défaut fait
échouer le premier test, avec le chemin complet du coupable dans le message.

**Recette appliquée, dans cet ordre** (validée sur Général, Loyer, Révision Loyer) :

1. `childControlHeight = true` — la chaîne mesure ;
2. `childForceExpandHeight = false` sur les lignes — elles cessent de réclamer de la place ;
3. retirer le `ContentSizeFitter` de la section — le parent fixe déjà la hauteur ;
4. aligner le plancher `minHeight` sur le contenu réel.

**L'ordre n'est pas négociable** : retirer les fitters en premier a écrasé toute la fiche Général
(« bon c'est tout écrasé », annulé par `git checkout`). Mon hypothèse — « les LayoutGroups fourniront
la hauteur » — était fausse, précisément parce qu'ils étaient encore en `childControlHeight = false`
et ne mesuraient rien. **Rendre la chaîne mesurable d'abord, retirer les fitters ensuite.**

**Overrides créés par la passe elle-même — le piège central de ce chantier.** Écrire sur une instance
de prefab imbriqué y grave un override, même quand la valeur finit identique à la source. Le script de
migration parcourait chaque prefab *y compris les instances qu'il contient*, et a donc gravé partout.

- **Textes** : **65** overrides `m_text` accumulés — 51 posés par le nettoyage, 14 antérieurs. Tous
  révoqués ; les 30 libellés réellement propres à leur instance (`Dépôt de garantie :`, `Mensualité`,
  `Siret :`) conservés.
- **Layout** : **27** overrides `m_Child*` posés dans `BatimentPrefab`, `LocatairePrefab` et
  `Achat from panel`. Trois d'entre eux — `invest`, `Body`, `Content` du panneau d'historique —
  **ont rendu la restauration du prefab source sans effet** : l'instance gardait sa propre copie.
  Symptôme : les historiques restaient petits alors que le prefab source était revenu à l'identique.
  Total des overrides de layout ramené de **58 à 51**, sous le niveau d'origine.

  Trois **racines d'instance** portaient le même défaut : `Investissement List Panel`,
  `Achat from panel` et `TravauxFromPanel`, toutes à `ctrlH=1` sur l'instance contre `0` sur leur
  source. Les deux dernières ont été trouvées **par le test**, pas à l'œil — et avant que les panneaux
  Achat et Travaux ne soient ouverts, donc avant que le défaut ne se voie.

  **Reste deux overrides, laissés exprès** : une instance de `Text & Input` dans `LocatairePrefab`
  (`ctrlH=1, expH=0`, divergents de leur source). Les appliquer au source toucherait **tous** les
  champs de l'application ; les révoquer pourrait ramener un blanc dans une section déjà validée.
  Aucune mesure ne tranche aujourd'hui — à reprendre avec le lot des `ContentSizeFitter`.

**La leçon** : une valeur gravée à deux endroits finit par diverger, et c'est exactement le reproche
central de cette revue. Corriger le prefab source ne suffit jamais tant que l'instance porte un
override — et un script qui parcourt « tous les prefabs » en crée sans le dire.

**État au 23/09** : audit des 26 prefabs, **plus aucun groupe à traiter**. Ne subsistent que les cas
légitimes — la racine d'un prefab plein écran, et le `Content` direct d'un `Viewport` de `ScrollRect`.
Un piège à noter pour la prochaine passe : ma garde « sous un Viewport » excluait le **sous-arbre
entier**, donc le script avait d'abord corrigé **0 prefab** dans `BatimentPrefab`, où tout vit sous
`Scroll view/Viewport/Content/…`. Elle n'exclut plus que l'enfant **direct** du Viewport.

### Les `ContentSizeFitter` : 173 → 61 (23/09, après-midi)

Le décompte de 113 était partiel : l'audit complet en trouve **173** au fit vertical actif, dont 137
imbriqués sous un autre. La première tentative de les retirer avait écrasé la fiche Général ; celle-ci
a tenu, grâce à une méthode et non à un raisonnement.

**La méthode — simuler avant d'appliquer.** Pour chaque prefab : l'instancier sous le Canvas, mesurer
la hauteur de *tous* ses nœuds, retirer les fitters sur cette copie jetable, remesurer, comparer. Le
retrait n'est appliqué au prefab que si l'écart est **nul**. C'est exactement ce qui manquait la
première fois, où j'avais déduit que « les LayoutGroups fourniront la hauteur » sans jamais le vérifier.

| Prefab | Fitters retirés | Objets mesurés | Écart |
|---|---|---|---|
| `TravauxFromPanel` | 15 | 106 | 0 |
| `Achat from panel` | 17 | 127 | 0 |
| `Revision Loyer` | 14 | 177 | 0 |
| `LocatairePrefab` | 22 | 336 | 0 |
| `BatimentPrefab` | 12 | 580 | 14 → **3 fitters conservés** |

**Trois fitters de `BatimentPrefab` font un vrai travail** et restent en place : `HeaderFiche/BoutonList`
(36 → 32 px sans lui), `Objectif/titre` (48 → 38) et `Rentabilité/Content/Bouton` (34 → 48). La
simulation les a isolés ; aucun coup d'œil ne les aurait distingués des autres.

**Critère de retrait** : le parent contrôle déjà la hauteur (`childControlHeight`) *et* l'objet sait
annoncer la sienne sans le fitter (un `LayoutGroup` interne, un texte, un `LayoutElement`). Sans la
seconde condition, l'objet tombe à zéro — c'est ce qui avait écrasé Général.

**Ne jamais toucher aux instances.** Les retraits n'ont porté que sur les **prefabs sources** ; les
fitters appartenant à un sous-prefab sont ignorés (`IsPartOfPrefabInstance`). Vérifié après coup :
les blocs `m_AddedComponents` / `m_RemovedComponents` de `BatimentPrefab` (8/8) et `LocatairePrefab`
(10/10) sont **identiques à l'origine** — aucun override créé. C'est la leçon des trois pièges du matin.

**Les 61 restants** sont pour l'essentiel légitimes : racines de prefab, enveloppes de `ScrollRect`
(46 au départ, à ne pas toucher — un scroll n'a pas de hauteur de contenu), et les 3 actifs ci-dessus.

### Ma prémisse était fausse — ce n'étaient pas les fitters

Tout ce chantier partait d'une idée : les `ContentSizeFitter` empêchaient de régler la taille de police.
Une fois les 112 retirés, la vérification — grossir tous les textes de 25 % et regarder qui suit :

| Prefab | Conteneurs qui grandissent | Restés figés |
|---|---|---|
| `LocatairePrefab` | 167 | 144 |
| `TravauxFromPanel` | **0** | 89 |
| `Revision Loyer` | 25 | 147 |

Ce qui fige la hauteur, ce sont **140 `LayoutElement` à hauteur écrite en dur** (29 dans Travaux,
35 dans Révision, 76 dans Locataire) et une centaine d'éléments positionnés par ancrage plutôt que
par layout. Les fitters n'y étaient pour rien.

Le travail n'est pas perdu — blancs fantômes, 120 textes parasites, 65 overrides et trois régressions
d'affichage corrigés — mais il ne débloquait pas ce qu'on croyait. **Leçon** : une idée de départ non
mesurée survit à des heures de travail sans qu'aucune étape ne la remette en cause.

### Largeur et défilement : ce que le zoom a révélé (23/09)

Le zoom n'a rien cassé — il a montré des défauts qui attendaient. Sur un écran de 1920 px, un
conteneur large de 1920 px tombe juste **par coïncidence**. Dès que le Canvas rétrécit (à 125 %, il
passe à 1536), tout ce qui portait une largeur en dur déborde.

**La cause était unique et à la racine** : `Batiment Manager`, le conteneur de tous les écrans, portait
un `ContentSizeFitter` sur les **deux** axes. Sa taille venait donc de son contenu, jamais de l'écran —
exactement à l'envers de ce qu'il faut. Trois corrections, toutes mesurées à zéro écart immédiat :

| Objet | Changement | Effet |
|---|---|---|
| `Batiment Manager` | `horizontalFit → Unconstrained`, `childControlWidth → true` | 13 largeurs en dur (560, 524, 500, 496, 490…) remplacées par « la largeur que donne le parent » |
| `TabBar` | `horizontalFit → Unconstrained` | gardait 1730 px sur un écran de 1536 |
| `General Panel` | `ScrollArea` + `Viewport`, `Panel` devenu contenu défilant | 1211 px de contenu pour 1033 visibles à 125 % |

**Un LayoutGroup écrase les ancres de ses enfants directs** : on ne peut pas étirer soi-même un enfant
de groupe, c'est au groupe de lui donner sa largeur (`childControlWidth`). Une tentative d'étirement
manuel a mis `ContentPanel` à **0 px** — le parent attendait la taille de l'enfant, l'enfant celle du
parent.

**La greffe du défilement a échoué une première fois**, toutes les sections superposées. La cause :
`offsetMin`/`offsetMax` posés puis écrasés par `anchoredPosition = 0`, avec le pivot changé au passage.
Les trois se contredisaient. La configuration correcte d'un contenu de `ScrollRect` est
`anchorMin (0,1)` / `anchorMax (1,1)` / `pivot (0.5,1)`, puis **`sizeDelta` et `anchoredPosition`
seulement** — jamais les offsets en plus.

**Ce qui a fait la différence la seconde fois** : la simulation **en Play**. Les écrans se construisent
au runtime et les deux panneaux sont actifs simultanément dans l'éditeur, donc aucune mesure hors Play
n'est fiable. Monter la structure en mémoire pendant le Play, mesurer chaque section avant/après, puis
n'appliquer qu'à zéro écart — et l'utilisatrice voit le résultat immédiatement, sans rien risquer
puisque le Play ne persiste pas.

**Pourquoi le zoom global et pas une taille de police seule** : la structure est indépendante du
facteur. Le viewport prend toujours la hauteur de l'écran, le contenu toujours celle de ses sections ;
le défilement est leur différence. À 200 % comme à 90 %, rien à régler.

### Les tailles de texte : une échelle, des rôles (23–24/09)

**Constat** : 20 tailles distinctes (11 à 30 pt) dans 833 textes de prefabs et 309 appels de code, et
**745 surcharges de `fontSize`** sur des instances de prefab — un tiers de toutes les surcharges du
projet. Une même taille servait à des choses sans rapport (20 pt : un titre de section, un bouton, le
contenu d'un champ), et une même chose avait plusieurs tailles (titres de section à 20, 22 et 24).

**La règle** — tout vit dans `UITheme` :

- **Six tailles** : 12 · 15 · 18 · 20 · 24 · 26 (`TailleLegende` … `TaillePage`).
- **Quinze rôles** (`UITheme.Role`), chacun pointant vers une taille : Pastille, Mention, Donnée,
  En-tête, Sous-titre, Aide, Action, Libellé, Bouton, Nom, Valeur, Chiffre clé, Section, Page.
- **Le code désigne un rôle, jamais une taille** : `UIFactory.Text(…, UITheme.Role.Donnee, …)`.
  Changer la taille d'un rôle change tous ses textes, dans tous les écrans, en une ligne.
- **Un texte se classe selon ce qu'il porte, pas selon sa place** : une donnée qu'on lit reste une
  donnée, même dans une ligne de tableau ou sous une vignette. La légende est réservée à ce qu'on ne
  lit pas vraiment — pastille d'état, mention accessoire. C'est faute de cette règle que les lignes du
  tableau des créances avaient été rangées en légende (12 pt) et jugées trop petites. Même erreur,
  refaite à la migration puis corrigée le 24/09 sur la fiche résumé : la ligne « Acquis · Terrain ·
  Cadastre… » (→ Donnée), les libellés des tuiles de chiffres « Loyers / an » (→ En-tête, ici comme
  dans les tuiles des créances), les pilules « 450 m² · 4 lots · Sans parking » (→ Donnée, largeur
  libérée) et l'avertissement rouge du trimestre de révision (→ Aide). Les vraies pastilles (compteur,
  type d'alerte, « ! », « Révision à faire ») ont quitté Mention pour Pastille, à taille égale.
- **Un `Button` n'est pas toujours un bouton** : une pastille d'état cliquable, un élément de menu
  déroulant, un petit bouton logé dans une ligne (≤ 36 px) ont chacun leur rôle — sinon une trentaine
  de petits boutons seraient passés à 18 pt et auraient débordé.

**Code migré** : 265 tailles en dur → 6 constantes, puis 267 références → rôles, dans 21 fichiers.
34 textes ont changé de taille, tous listés et relus avant application.

**Garde-fous** (`EchelleTypoTests`) : aucune taille littérale dans le code · aucune `UITheme.Taille…`
hors de `UITheme` · chaque rôle pointe vers une taille de l'échelle · le détecteur lui-même est testé
sur des extraits (il trouve une taille glissée, il ne confond pas la hauteur d'un bouton ni une
composante de couleur avec une taille).

**Prefabs (24/09, trois lots)** : les **665 textes des 26 prefabs** sont sur l'échelle, et plus
aucune instance ne réécrit la taille de sa source (745 surcharges au départ). Chaque taille a été
portée au prefab source, jamais sur l'instance. Lot 3 : historiques et formulaires achat/travaux,
objectifs, PLU, révision de loyer, lignes (alertes, locataires, bâtiments, suivi de facturation,
rentabilité). Au passage :

- Des libellés avaient une **hauteur figée à 15 px** (révision de loyer, PLU, résumé achat/travaux) :
  le texte débordait déjà de 3 à 4 px. Hauteurs libérées, le layout suit la taille du texte.
- Le résumé achat/travaux tient en trois colonnes d'environ 130 px : libellés en En-tête, montants en
  Valeur avec un autosize **plafonné à 20** (il ne sert qu'à rétrécir un montant trop long). C'est la
  seule forme d'autosize admise dans les prefabs.
- **`AppSectionColors` réimposait 22 pt** (hors échelle) à tous les titres de section, toutes les
  0,5 s : les titres passés à 24 au lot 2 redescendaient à 22 en Play. Il applique désormais
  `UITheme.Role.Section`. Le test du code ne l'avait pas vu : la taille passait par une constante
  nommée, pas par un littéral.
- Le tableau « À traiter » avait ses lignes à 19–20 pt, celui des créances, sur le même écran, à 15.
  Les deux sont des données : même rôle. Leurs en-têtes (scène, et `HeaderCol` de la fiche résumé)
  passent en En-tête.
- Les petits boutons du suivi de facturation avaient une **largeur fixe (74 px) calibrée pour 13 pt** :
  à 15, « Générer » se coupait en deux. Leur largeur suit désormais le libellé
  (`UIFactory.LargeurDuTexte`, prefab et vue plein écran). Règle à retenir : un bouton ne reçoit pas de
  largeur fixe, sinon changer la taille d'un rôle recasse l'écran.

**Garde-fous prefabs** (`EchelleTypoTests`) : tout texte de prefab est sur l'échelle, autosize compris
(son plafond aussi) · aucune instance ne surcharge `m_fontSize` · le contrôle est lui-même testé sur
un texte à 13 pt et un autosize plafonné à 27.

**Scène (lot 4, 24/09)** : 77 textes propres à `SampleScene` ramenés à leur rôle — calculatrice,
sauvegarde, galerie photo, dialogue de confirmation, message d'annulation, barre d'onglets, menu
général (titre « Menu général » en Page, tuiles, titres de section, calcul rapide). Six hauteurs figées
libérées (libellés du calcul rapide, son titre, bandeau d'en-tête du menu — minimum 62 conservé).
Une **surcharge d'instance à 72 pt** traînait sur le « Vide » des objectifs globaux : masquée tant que
la source était en autosize, elle aurait affiché « Vide » en 72 une fois l'autosize retiré. Retirée.
Les 168 textes de la scène sont sur l'échelle ; un test le vérifie en lisant le fichier `.unity`
(l'ouvrir depuis un test remplacerait la scène ouverte dans l'éditeur).

**La calculatrice `Canvas/Calcul` a été supprimée (24/09)**, avec son script
`Prix/CalculPrixRentabilite.cs` : aucun bouton ne l'ouvrait (seul son propre bouton de fermeture la
référençait), le calcul rapide du menu l'avait remplacée. Vérifié avant suppression : aucune référence
depuis le reste de la scène, aucun prefab ni script n'utilisait sa classe ou ses méthodes. 147 objets
de moins dans la scène.

**Tuiles de chiffres de la fiche résumé** : leur autosize montait à 27 et 30, hors échelle. Plafonné
au rôle Chiffre clé (24), plancher Libellé (18), sur décision de l'utilisatrice (« aligne-les, à la
limite on grossira tout » — par le zoom ou par les rôles, pas au cas par cas). Le test du code
attrape désormais aussi les bornes d'autosize écrites en dur (`fontSizeMin/Max = 30`).

### Le réglage de taille : `UIZoom` (23/09)

Plutôt que de rendre proportionnelles 140 hauteurs en dur — plusieurs heures, et un texte devenu plus
grand que son champ déborde — le réglage agit sur **toute** l'interface d'un coup.

`Assets/Script/Tools/UIZoom.cs`. Le `CanvasScaler` est en `ScaleWithScreenSize`, mode où `scaleFactor`
est **ignoré** ; le levier est la résolution de référence : la diviser par 1,25 revient à dessiner
l'interface comme si l'écran était plus petit, donc à tout grossir de 25 %. Paliers 90 / 100 / 110 /
125 / 150 / 175 % (150 et 175 ajoutés le 25/09 à la demande de l'utilisatrice), bornés à [0,8 – 1,75] —
à 175 %, un écran de 1920 px n'offre que 1097 unités de large —, persistés dans `PlayerPrefs` (`CIPL_ZoomUI`), appliqués dans
`BatimentManager.Start()` avant la construction des écrans. Section « Taille de l'affichage » en tête
du panneau Réglage.

La référence d'origine est lue **une seule fois** (`_origineLue`) : la relire à chaque application la
ferait dériver, puisqu'on écrit dessus. Cinq tests (`UIZoomTests`) verrouillent le sens du calcul —
on divise pour agrandir, ce qui s'inverse sans rien casser d'autre — et le retour à 100 % sur une
valeur absurde (0, négative, `NaN`). Vérifiés par mutation.

**À 150 et 175 %, « Créances » sortait de l'écran (25/09).** La carte annonçait une largeur minimum
de 300 alors que son tableau en exige 712 (mesuré en Play) : le menu la serrait à côté de « À traiter »
(610) dans une rangée qui n'offre que 1065 unités à 175 %. Corrigé en deux temps :
`FacturationHomeSection.LargeurMin` calcule le vrai minimum à partir des constantes de colonnes, et
`GeneralMenuPanel.DisposerColonnes` place « Créances » **sous** « À traiter » quand les deux n'entrent
plus côte à côte (1334 exigées : côte à côte jusqu'à 125 %, empilées à 150 et 175 %). La disposition
est revue quand la largeur change (`LateUpdate`, pas `OnRectTransformDimensionsChange` : déplacer un
enfant en plein calcul de layout déclenche des erreurs de reconstruction). Leçon générale : **un
`minWidth` doit dire la vérité sur le contenu**, sinon un groupe de layout ne peut pas arbitrer.

**Même défaut sur les fiches, même remède, un seul composant (25/09).** À 175 %, « Informations
générales » débordait sous « Rentabilité » (colonne annoncée à 520, adresse + carte en exigent 642) et
les tuiles de rentabilité se chevauchaient (annoncées à 80, leurs montants en demandent jusqu'à 208).
La logique du menu est devenue un composant, **`Tools/ColonnesAdaptatives`**, posé sur toute rangée de
colonnes : il empile les colonnes quand la somme de leurs minimums dépasse la largeur offerte, et les
remet côte à côte quand la place revient. Posé sur la rangée du menu (par code, après le
réaménagement), sur `TopRow` de la fiche bâtiment et sur `RowColonnes` de la fiche locataire.

Les minimums menteurs ont été retirés (`minWidth` des colonnes à -1 : le contenu dit le sien), les
tuiles de rentabilité portent 210 et leurs titres ne passent plus à la ligne (« … » plutôt que
chevaucher le montant). Fiche bâtiment : côte à côte jusqu'à 125 % (tuiles de 211), empilée à 150 et
175 %. **`ColonnesAdaptativesTests`** : au zoom maximal, aucune colonne ne contient de rangée plus large
qu'elle ; à 100 %, rien n'est empilé. Le premier passage a trouvé 2 px de débordement dans la fiche
locataire (colonnes annoncées à 400) — corrigé de la même façon. Limite : le test ne voit que ce que
le prefab contient ; les sections construites en Play (facturation, suivi) restent à regarder à l'écran.

**Défilement du résumé de la fiche bâtiment (28/09).** À 175 %, la vue résumé dépassait en bas sans
pouvoir défiler. Même structure que le menu général : `VueResume` porte le `ScrollRect` (vertical,
Clamped, sensibilité 30), ses cartes vivent sous `VueResume/Viewport/Content`, qui reprend le groupe
vertical d'origine (marges 18, espacement 14) et se dimensionne sur son contenu. `VueResume` reste
l'objet que `BatimentPrefab` active et désactive — rien ne change pour lui. Dans
`BatimentSummaryView`, les cinq endroits qui créaient ou cherchaient une carte sous `transform` passent
par une seule propriété, `Contenu` : c'est la leçon des créances disparues du menu (cinq chemins écrits
en dur, vidés en silence le jour où la structure a changé).

**Section « Bâtiments » du menu (28/09).** À 150 %, la zone des cartes sortait de la section. Cause :
une hauteur de section **fixe** (380, bornée à 260 au zoom) plus petite que son contenu (410), avec une
zone de cartes fixe (300) qui ne pouvait pas céder. Effet de bord ancien : même à 100 %, la barre de tri
était écrasée à 4 px — le « Tri » qui « passait derrière les cartes » (contourné à l'époque par un
Canvas trié, qui reste en place mais ne soigne plus rien). Désormais, `LayoutHeights` calcule la
hauteur **pleine** d'après le contenu (`HauteurContenu`), seule la zone des cartes peut se serrer (jusqu'à
une rangée, 180), et la section n'est serrée que si cela évite le défilement de la page à 100 % ; au
zoom, la page défile de toute façon et la section garde sa hauteur pleine.

**« À traiter » et « Locataires » désalignés dans le résumé (28/09).** La rangée qui les porte ne
contrôlait pas les hauteurs ; chaque carte se dimensionnait par son propre `ContentSizeFitter`. Placées
par leur sommet puis redimensionnées autour de leur centre (pivot 0,5), elles ne tombaient jamais au
même endroit. `AjusteHauteurRangee` recalculait la hauteur de la rangée à la main (reconstruction
forcée) pour compenser. Désormais la rangée contrôle et étire les hauteurs : même sommet, même hauteur
(celle de la plus grande carte), et la liste des locataires prend la place gagnée. Les deux
`ContentSizeFitter` et `AjusteHauteurRangee` sont supprimés.

**Textes d'exemple des champs vides (28/09).** 42 « Enter text... » en anglais, et sept couleurs de
placeholder différentes — dont certaines aussi foncées qu'une valeur saisie (« JJ / MM / AAAA » opaque) :
rien ne signalait qu'un texte ne serait pas enregistré. Une seule couleur, `UITheme.TexteExemple`
(`#A8A69E`), en italique, et « Saisir… » par défaut, appliqués aux prefabs sources et à la scène ; 29
couleurs surchargées sur des instances retirées ; `UIFactory.Input` suit la même règle. 90 champs
vérifiés, un seul style. `TexteExempleTests` le verrouille (prefabs : couleur, italique, pas
d'« Enter text... » ; scène : pas d'« Enter text... »).

**Saisie des dates (28/09).** `DateInputController` passe au champ suivant dès qu'un champ est complet
(« 14 » → mois, « 05 » → année), seulement pendant la frappe (`isFocused`) : `ApplyDate` remplit les
champs par code et ne doit pas déplacer le curseur. Non couvert par un test EditMode (le focus exige
le Play). Dans la révision de loyer, les trois champs s'étalaient sur toute la largeur (143 px entre
eux) : le groupe `DateInput` répartissait l'espace libre (`childForceExpandWidth`). Corrigé dans le
prefab « Calendar » et dans la **copie non reliée** que porte « Revision Loyer ». Écart de 20 px
partout désormais.

**Date de la révision rattachée à « Calendar » (28/09).** L'instance de « Calendar » dans « Revision
Loyer » avait son `DateInput` d'origine **retiré** et remplacé par une **copie ajoutée**, vers laquelle
pointaient les quatre références du `DateInputController`. Désormais : copie supprimée, `DateInput`
d'origine restauré, références revenues à la source. Restaurer l'objet a fait ressurgir 55 anciennes
retouches qu'il portait en sommeil (tailles 20×35 au lieu de 52×44, tailles de texte, couleurs) —
`EchelleTypoTests` les a signalées ; retirées, ainsi que 36 retouches de la scène sur le même objet et
89 surcharges de scène sans cible (elles visaient la copie ou des objets disparus). Seules adaptations
gardées, voulues pour cet écran : titre au-dessus des champs (groupe vertical au lieu d'horizontal),
hauteur de ligne 44, pas de `ContentSizeFitter` sur `DateInput` (le groupe vertical fixe la largeur).
`RevisionPanel` ne passe que par les références du composant : rien à changer côté code.

**Clic sur une créance → suivi de la fiche (28/09).** `FacturationHomeSection.OpenDetail` n'ouvre plus
la vue plein écran : `LocataireSuiviInline.MontrerPour(fiche, année)` attend une image (la sélection
de la fiche remet l'année courante), pose l'année d'échéance de la facture, puis fait défiler la fiche
jusqu'au haut de la section. Conséquence : `FacturationSuiviPanel` n'avait plus aucun appelant —
**supprimé** (≈ 400 lignes, avec l'accord de l'utilisatrice), après vérification qu'aucune scène,
aucun prefab ni aucun script n'y faisait référence. C'était la dernière copie en code de la ligne de
suivi : elle n'a plus qu'une construction, le prefab `SuiviFactureRow`.

**Section Bail retravaillée (28/09).** Inventaire des données réelles d'abord (9 locataires) : 7 en
3/6/9, 1 en « 9 ans ferme » (qui dure 10 ans), 1 en « Bail à construction » sans aucune date — la
valeur 0 de l'enum, reçue par défaut sans que personne la choisisse.
- **Rangée [type · durée · période ferme]** (précision de l'utilisatrice : « le type, à côté la
  durée pré-remplie mais modifiable qui définit le temps total du bail ; la période ferme est un
  temps à l'intérieur de cette durée »). La durée et la période ferme ne sont donc **pas des types**.
- **Liste** : bail commercial · dérogatoire (3 ans max, ex-« précaire », qui prêtait à confusion avec
  la convention d'occupation précaire) · professionnel · civil · convention d'occupation précaire ·
  emphytéotique · à construction · à réhabilitation. « Commercial 9 ans », « 10 ans » et « 9 ans
  ferme » ne sont plus proposés : ce sont des baux commerciaux (`Locataire.Normaliser`). Libellé
  « Bail commercial » sans « (3/6/9) », faux dès qu'il y a une période ferme. L'ordre de l'enum est
  intact (les fichiers stockent le numéro) ; `BailCommercial9Ferme` est seulement renommé
  `BailCommercialFerme`. La liste déroulante ne suit plus l'enum : `Locataire.TypesProposes`.
- **Durée** (`dureeBailAns`) et **période ferme** (`anneesFermes`, facultative, bail commercial
  seulement) : champs clonés d'un champ natif (`LocataireFacturationFields.Clone`, désormais partagé).
  Sur les fichiers existants, la durée se déduit des dates quand elles couvrent un nombre entier
  d'années, sinon celle du type ; l'ancien « 9 ans ferme » garde 9 ans fermes. Enregistrement refusé
  si la période ferme dépasse la durée.
- **Date de fin automatique** : début + durée − 1 jour (01/06/2020 → 31/05/2029), recalculée quand
  on change le début, la durée ou le type — jamais au simple affichage d'une fiche. Elle reste
  modifiable.
- **Nouveau locataire** : commercial 3/6/9 par défaut (initialiseur de champ ; un fichier existant
  garde son type, testé). Le locataire « Nouveau » actuel reste en « Bail à construction » tant que
  l'utilisatrice ne le change pas.
- **Au passage** : la variable `{loc.bail}` des textes de facture insérait le nom interne
  (« BailCommercial369 ») ; elle insère le libellé.
- `LocataireBailFields` porte la logique ; `BailTests` (9 tests) couvre fin, période ferme, durée
  déduite, anciens types, numéros stockés, défaut, lecture d'un fichier existant et clonage de champ.
- **`Clone` ne trouvait le titre et l'unité qu'au premier niveau**, sous les noms « title » et
  « quantité ». Cloné depuis « Taille Batiment » (titre au nom du champ, unité un niveau plus bas),
  le champ gardait « Taille Batiment : … m² ». `Clone` cherche désormais l'unité dans toute la
  profondeur et prend, à défaut de « title », le premier texte direct qui n'est ni la valeur ni l'unité.

**Bail et avenants rangés dans le dossier du locataire (28/09).** Le bail joint était copié dans
`Documents/<identifiant du locataire>/` à la racine des données, et son chemin **absolu** enregistré
(cassé au premier changement d'emplacement des données). Désormais : copie dans
`Batiment/<bâtiment>/<locataire>/Bail/` (`DossiersDonnees.DossierBail`, à côté de `Facture/`), et
seul le **nom de fichier** est enregistré — un renommage déplace le dossier du locataire, les
documents suivent. Un ancien chemin complet reste lisible (`CheminDocumentBail`). Copie **sans
écraser** (`CopierSansEcraser` : « bail.pdf » pris → « bail (2).pdf » ; un fichier déjà rangé n'est
pas recopié). **Avenants** : `Locataire.avenants` (noms de fichier), bouton « + Avenant » sur la
ligne du bail, une ligne « Avenant n » par document (Ouvrir · Retirer). « Retirer » détache sans
supprimer le fichier. Aucun locataire n'avait encore de bail joint : rien à migrer. 3 tests.
Boutons selon l'état (demande de l'utilisatrice) : sans bail, seulement « Ajouter le bail » ; avec
un bail, « Modifier » (remplace le fichier) · « Ouvrir » · ✕ · « + Avenant ». Le bouton dont le
libellé change suit la largeur de son texte (`UIFactory.LargeurDuTexte`) ; « Bail : » et
« Avenant n : » ont la même largeur, les noms de fichier s'alignent.

**Alerte de résiliation triennale (28/09).** À côté de « Fin de bail » : le preneur d'un bail
commercial peut donner congé à chaque fin de période triennale (3, 6, 9… ans après le début),
préavis de 6 mois, jamais pendant la période ferme ; la fin du bail reste au renouvellement
(`Locataire.EcheancesTriennales`, `ResiliationProche`). L'alerte s'ouvre 3 mois avant la date limite
du congé et se ferme à cette date (après, le preneur ne peut plus partir à cette échéance). Affichée
dans « À traiter » (menu et résumé : type « Résiliation », « Congé possible jusqu'au … (sortie le …) »),
par le point rouge d'onglet, et par la pastille de la section Bail (le renouvellement, plus urgent,
garde la priorité). 2 tests, dates figées. La pastille porte la date limite : « Résiliation possible —
congé jusqu'au … (sortie le …) » ; elle avait une largeur fixe de 140 px qui coupait déjà « Résiliation
possible » — libérée, elle suit son texte (le bandeau contrôle les largeurs).

**Fin de bail : congé et tacite prolongation (28/09).** Question de l'utilisatrice : « au moment du
renouvellement, peut-on avoir la résiliation en même temps ? » — oui (art. L145-9 C. com., à faire
valider par son conseil) : pour un bail commercial, le preneur peut donner congé pour la **fin** du bail
avec 6 mois de préavis ; et une fois le bail expiré sans action, en **tacite prolongation**, il peut
partir à tout moment, préavis de 6 mois, pour le dernier jour d'un trimestre civil. L'alerte de
renouvellement s'ouvrait **6 mois** avant la fin — le jour même où il était trop tard pour ce congé ;
elle s'ouvre désormais **9 mois** avant (même règle que les échéances triennales). Un seul texte,
`Locataire.TexteFinDeBail`, pour la pastille et « À traiter » : « À renouveler — fin le …, congé
jusqu'au … », puis « À renouveler — fin le … » une fois le délai passé, puis « Bail expiré — congé
possible à tout moment, sortie au plus tôt le … » (`SortieTaciteAuPlusTot`). Hors bail commercial,
textes inchangés dans leur principe. 4 tests, dates figées.

### Prochaines étapes (mise à jour 22/09/2026)

Rien ne bloque : tout ce qui suit est écrit, compilé et couvert par **244 tests EditMode verts**. Ce qui reste se range en trois tas.

#### A. À voir en Play — le seul vrai reste

Aucun de ces points n'est douteux dans le code ; ils demandent l'écran. Par ordre d'intérêt :

1. **Le cycle complet d'un loyer** (22/09, le plus neuf) : préparer une facture en avance → bouton « Enregistrer pour l'envoi du JJ/MM » + panneau annonçant le jour · la ligne reste **ambre** et ne bascule jamais « Envoyé » toute seule · la corriger avant sa date → elle **remplace** la précédente, garde son numéro, perd le « corrigée(X) » · au lancement le jour dit → le récapitulatif d'envoi apparaît, « Tout envoyer » part et marque « Envoyé », « Plus tard » ne redemande pas le même jour.
2. **La ligne cliquée ouvre la bonne période** (22/09) : « Générer » / « Refaire » sur le loyer d'octobre → panneau sur octobre, échéance au jour de demande · idem sur une régularisation d'une année donnée et une refacturation d'une charge donnée.
3. **Surfaces** (22/09) : un bâtiment à deux locataires — le lot non défini se réduit quand l'autre prend sa part, la surface du bâtiment ne bouge plus seule, dépassement signalé une fois.
4. **Le suivi se remplit sans quitter l'application** (22/09) : créer un locataire, régler loyer et dépôt → le tableau apparaît immédiatement.
5. **Plus aucun champ n'affiche sa valeur en double** (22/09) : vérifier « Taille Batiment » avec plusieurs locataires, et les écrans Achat / Travaux qui avaient le même défaut latent.
6. **Le rendu du prefab `SuiviFactureRow`** (21/09) : alignement en-tête / lignes, libellé long tronqué, année sans facture, période clôturée sans action — comparer avec la vue plein écran, restée en code.
7. **L'envoi email sur Régularisation, Refacturation et Dépôt** — seul le Loyer a été vu de bout en bout. Tester surtout **l'échec** (mot de passe faux) : PDF présent, ligne non marquée, charges non payées, second essai qui reprend le même numéro.
8. **Le cycle de vie des charges** (21/09) : régularisées → « en attente de paiement » (ambre), hors du choix ; facture « Payé » → vert ; retour « Impayé » → attente **sans** redevenir sélectionnables.
9. **Après les optimisations du 22/09 au soir** : vérifier que l'affichage n'a pas souffert du passage à `MarkLayoutForRebuild` — hauteur des champs, défilement des fiches, sections repliables — et que rouvrir deux fois le même bâtiment ne relance plus ni géocodage ni téléchargement de carte (la console ne doit afficher `[GeoCoding]` et `[Mapbox]` qu'à la première ouverture).
10. **Après le nettoyage des prefabs du 23/09** : fiche bâtiment **vue le 23/09, correcte**. Restent
    les panneaux **Achat**, **Travaux**, **Objectifs** et le **Calendrier**. Chercher l'inverse du
    défaut réparé : non plus des blancs, mais un contenu **tassé ou tronqué** — une ligne trop courte,
    un texte coupé, un bouton écrasé.
11. **Reliquat des sessions 17–21/09** : gardes d'avoir (régul/dépôt) · `FermetureGuard` · ordre des cartes dans les trois panneaux non encore vus · phrase de règlement au signe du solde dès l'ouverture · textes de facture réglables et menu « / » (aucun `{jeton}` ne doit ressortir sur le PDF) · phrases de l'explication du dépôt · héritage en chaîne des réglages (T1 → T2 → T3, puis nouveau bâtiment).

#### B. À coder — court

1. ~~Convertir `FacturationSuiviPanel` au prefab `SuiviFactureRow`~~ — **sans objet** : la vue plein écran a été supprimée le 28/09 (plus aucun appelant depuis que le clic sur une créance mène au suivi de la fiche). La ligne de suivi n'a plus qu'une construction, le prefab.
2. **Puis `FacturationHomeSection`** (48 appels `UIFactory`, écran d'accueil). Ne **pas** convertir `ReglagePanel` ni les 4 panneaux de facture : formulaires construits une seule fois, arbitrage inchangé.
3. **Les 113 `ContentSizeFitter` imbriqués**, puis le réglage de taille de police lui-même. C'est la
   suite directe du 23/09 et la seule qui reste avant le réglage. **Une section à la fois, vérifiée à
   l'écran avant la suivante** — le lot a déjà échoué une fois. Chaque fitter retiré doit d'abord avoir
   un parent qui mesure (`childControlHeight = true`), sinon la section s'effondre au lieu de s'ajuster.

#### C. En attente d'une décision extérieure

1. **H1 — numérotation des factures**, en attente de l'expert-comptable. Deux locataires facturés le même mois peuvent obtenir le même numéro ; le choix entre séquence globale et identifiant stable par locataire engage la conformité (art. 242 nonies A du CGI). Ne pas trancher seul.
2. **Pennylane** : le **dépôt** existe (Loyer, facture incomplète + Factur-X, 29/09). La **finalisation et la transmission** attendent la réponse de Pennylane (une facture importée est-elle éligible à `send_to_pa` ?). Tout ce qui est « envoi » réel passe encore par l'email.
3. **Adresse `@cipl.fr`** : bloquée côté Microsoft 365 (pas de mot de passe d'application, SMTP AUTH désactivé, compte non administrateur). Il faut l'administrateur du locataire, ou un compte OVH — le SPF de `cipl.fr` l'autorise déjà.

#### Traité et clos

~~Prefab `SuiviFactureRow`~~ (21/09) · ~~six défauts trouvés par l'usage~~ (22/09) · ~~envoi groupé au lancement~~ (22/09) · ~~reliquat mineur~~ (16/09).

### Garde-fous permanents

Travailler dans `CIPL_Git\CIPL` (jamais `CIPL_Test`, copie obsolète) · **ne jamais commiter** (l'utilisatrice s'en charge) · secrets hors dépôt · toute modification de facturation répercutée dans `FACTURATION_CIPL_PENNYLANE.md` · aucune émission Pennylane réelle sans accord explicite (le code ne fait que **déposer** des factures incomplètes ; finaliser / transmettre n'existe pas) · **ne jamais finaliser une facture de test** portant un vrai SIREN (ex. Volteo) : elle partirait chez la vraie société.

---

## Statut des corrections (16/09/2026)

| Finding | Statut |
|---|---|
| C1 — perte des RIB / entêtes | **Corrigé** — écriture atomique, repli `.bak`, écriture verrouillée si lecture impossible, `reglage.json` dans les backups |
| C2 — app qui ne démarre plus | **Corrigé** — `try/catch` par fichier dans `LoadAll`, fichiers illisibles listés |
| C3 — « sauvegardé » alors que non | **Corrigé** — `AtomicFile` + `SaveBatiment` renvoie `bool` |
| H2 — double émission Régul/Dépôt/Refac | **Corrigé** — `EstDejaEmise` + `MarquerCorrige` câblés sur les trois panneaux |
| H2-bis — la garde ne couvrait pas les 4 états « émise » | **Corrigé et testé en exécution (16/09)** — voir ci-dessous |
| H4 — bâtiment jamais écrit sur disque | **Corrigé** — appel de sauvegarde réactivé dans `AddBatiment` |
| Bonus (non listé initialement) | **Corrigé** — `WriteSecret` pouvait effacer la clé API en enregistrant le mot de passe SMTP |
| H3 — impayé jamais détecté | **Corrigé** — `TryEcheance` en culture invariante partout, échéance garantie à l'émission, anomalie journalisée |
| M1 — token Mapbox commité | **Corrigé** — purgé du prefab, saisi dans Réglages, stocké dans les secrets ; URL plus journalisée ; `.gitignore` complété |
| M2 — `catch` silencieux | **Corrigé** — les 3 qui masquaient une vraie défaillance (secrets, photo, PDF périmé) ; les 3 autres sont du best-effort légitime |
| M3 — saisie invalide → 0 muet | **Corrigé** — `SaisieNumerique` mutualisé (3 copies supprimées), tolère « 1 200 » / « 3,5 % », avertit sinon |
| M3-bis — « . » et « , » ne faisaient pas la même chose | **Corrigé et testé en exécution (16/09)** — voir ci-dessous |
| M4 — culture des indices | **Corrigé** — écriture forcée en culture invariante (`FormatIndice`) |
| M5 — migration destructive | **Corrigé** — refus si destination non vide, copie de **tous** les fichiers (secrets, photos), retour `bool` honoré |
| M6 — montants négatifs / TVA | **Corrigé** — émission refusée sur avoir, TTC = HT + TVA arrondis au centime |
| L2 — code mort | **Corrigé** — `LoyerRevisionController.cs` supprimé après vérification (0 référence, GUID absent des scènes) |
| L3 — `Debug.Log` | **Traité** — la seule fuite réelle (URL Mapbox avec token) est corrigée ; les 30 restants sont diagnostiques et sans secret |
| L1 — pattern `??` | **Écarté** — prouvé non-bug par test en éditeur |
| Bonus (non listés initialement) | **Corrigés** — `WriteSecret` effaçait la clé API ; l'URL Mapbox fuitait le token dans la console |
| H1 — doublons de numéro de facture | **En attente** — arbitrage avec l'expert-comptable |

Compilation vérifiée après chaque lot (`CSF=False`) et écriture atomique testée en conditions réelles (création, remplacement, `.bak`, absence de `.tmp` résiduel, récupération d'un JSON tronqué). Rien n'est commité.

### H2-bis — la garde anti-double-émission ne couvrait pas le cas le plus courant (16/09/2026)

**CONFIRMÉ en exécution** · `Facturation/FacturationSuivi.cs:227` (`EstDejaEmise`), `:238` (`MarquerCorrige`)

Le correctif H2 avait câblé `EstDejaEmise` sur les quatre panneaux, mais la fonction elle-même testait `e == Etat.Envoye || e == Etat.Impaye` — deux états sur les quatre que `DejaTraite`, dans le même fichier, considère comme « traitée ». Deux trous, reproduits par un test exécuté dans l'éditeur (et non déduits de la lecture) :

| Cas | Ce qui se passait |
|---|---|
| **Loyer préparé plus de 15 j avant l'échéance** (statut `AttenteEnvoi`) — le cas **normal** du panneau Loyer | `EstDejaEmise` = `false` malgré un PDF déjà généré → second clic : nouvelle séquence consommée **et** PDF d'origine écrasé |
| **Facture marquée « Payé » puis re-générée** | Numéro neuf → **deux numéros pour une seule période** |

Correctif : les quatre états `AttenteEnvoi | Envoyé | Impayé | Payé` valent « émise » (+ PDF non vide). Un paiement ne « dé-émet » pas une facture ; un numéro consommé le reste.

Conséquence assumée sur `MarquerCorrige` : il ne force plus `AttenteEnvoi → Envoye`. Corriger un loyer avant sa date d'envoi affichait sinon un envoi qui n'a pas eu lieu — la bascule à J‑15 reste assurée par `EtatDe`, qui dérive l'état. Une facture payée reste payée ; seule une ligne **sans** statut devient « Envoyé ».

**Vérifié** (11 assertions) : détection en `AttenteEnvoi` et en `Payé` · statut d'attente et paiement conservés après correction · numéro conservé · séquence non consommée · non-régressions (ligne planifiée, ligne sans PDF, cas nominal Envoyé).

**Vérifié également en exécution** — refus de migration vers un dossier peuplé (`SaveLocationService.MigrateData`, 12 assertions) : dossier vide → migration complète, **y compris** secrets `.dat` et photos en sous-dossier ; dossier contenant déjà une entreprise → refus, message explicite, `reglage.json` de la destination **intact** et **aucune copie partielle** avant le refus (la pré-analyse précède toute écriture) ; même dossier écrit avec une casse différente → sans effet, pas d'auto-copie.

### M3-bis — point et virgule ne faisaient pas la même chose (16/09/2026)

**CONFIRMÉ en exécution** · `Tools/SaisieNumerique.cs` · 11 sites de saisie routés

M3 avait mutualisé **trois** copies de `Parse`. Une recherche exhaustive des parseurs de saisie en a révélé **onze**, dont huit hors du périmètre initial, avec trois comportements différents pour la même frappe :

| Site | Avant |
|---|---|
| `PrefabBatLoc.SaveCorrectlyFloat` (helper de sauvegarde des fiches), `CalculPrixRentabilite.CheckValueOnString` | **La virgule n'était pas gérée du tout** : « 1200,50 » → avertissement + **0** |
| `ParseF`/`ParseFloat` des 5 panneaux de facturation, `QuickCalcInline`, `RevisionPanel` (×3) | Virgule gérée, **espaces de milliers non** : « 1 200 » → 0 |
| `SaisieNumerique` | Virgule et espaces gérés, séparateurs mixtes non |

Les onze passent désormais par `SaisieNumerique`, où **« . » et « , » sont strictement équivalents** — le séparateur décimal se tape au pavé numérique (point) ou à la frappe française (virgule), et les deux doivent donner le même nombre. Avec plusieurs séparateurs, **seul le dernier est décimal** : `1.200,50`, `1,200.50`, `1 200,50`, `1 200.50` valent tous 1200,50.

**Vérifié** (20 assertions) : 9 paires point/virgule strictement égales · 6 formes mixtes · non-régressions (champ vide = 0, « abc » rejeté, séparateur seul rejeté).

Deux points conservés délibérément :
- **un séparateur unique reste décimal** : `1,200` = `1.200` = **1,2**, pas 1200. C'est le prix de la symétrie demandée — et le seul choix cohérent, puisque `0,500` doit valoir 0,5. Pour mille deux cents : `1200` ou `1 200`.
- **`TryParseLoyer` (loyer de départ) refuse toujours un champ vide** : `SaisieNumerique` traite le vide comme un 0 légitime, ce qui n'a pas de sens pour un champ obligatoire.

Limite inchangée, déjà connue (M6) : au-delà de ~7 chiffres significatifs, `float` ne tient plus le centime — `1.234.567,89` est relu à 1234567,9. Le type, pas la saisie.

### Revue globale de fin — 5 bugs trouvés et corrigés (16/09/2026)

Revue de tout `Assets/Script` par angles de risque (parsing, indexation, division, accumulation de listeners, caches statiques, chemins de fichiers, suppression/renommage), puis lecture de chaque site suspect. **Tous corrigés et vérifiés en exécution.**

Le fil rouge : quatre des cinq portent sur **la même faille**. L'arborescence a été standardisée (dossiers nommés par nom, chemins relatifs) et `DossiersDonnees` écrit comme point de passage unique — mais trois sites construisaient encore leurs chemins à la main, et un quatrième ne nettoyait pas ce que la convention laisse derrière.

| # | Défaut | Correctif |
|---|---|---|
| G1 | **`pdfPath` stocké en absolu** (`FacturationSuivi.cs:214`). Changer d'emplacement, migrer l'entreprise ou renommer un bâtiment/locataire cassait le lien de **toutes** les factures émises : le bouton « PDF » disparaissait du suivi alors que les fichiers étaient bien là. Même défaut sur `ChargeBatiment.pdfPath`. | Stocké via `VersRelatif` dans `MarquerEnvoye`/`MarquerCorrige` ; nouveau `FacturationSuivi.CheminPdf(ligne)` pour la lecture, câblé dans les deux vues du suivi. Les anciens enregistrements absolus restent lisibles (`VersAbsolu` les renvoie tels quels). |
| G2 | **Cache photo statique jamais vidé** (`PhotoService.cs:195`). Indexé par chemin relatif : deux entreprises ayant un bâtiment de même nom avec un `facade.jpg` partagent la clé — après bascule, la fiche affichait **la photo de l'autre entreprise**, sans aucun signe d'erreur. | `PhotoService.ViderCache()`, appelé dans `ReloadFromDisk` et à la suppression d'un bâtiment. |
| G3 | **Quittances écrites dans `Batiment/<GUID>/<GUID>/Facture`** (`FactureQuittanceService.cs:35`) : introuvables à côté des factures du locataire. | `DossiersDonnees.DossierFactures(nom, nom)`. |
| G4 | **PDF de charges dans `Batiment/<GUID>/Charge`** (`ChargePanel.cs:342`). `DossiersDonnees.DossierCharges` existait pour ça et n'était appelée **nulle part** — un oubli, pas un choix. | `DossierCharges(nom)` + chemin stocké en relatif. |
| G5 | **Dossier fantôme après suppression** (`BatimentManager.cs:140`) : le dossier restait en place, et renommer plus tard un bâtiment vers ce nom échouait sur « un dossier X existe déjà », alors qu'aucun bâtiment de ce nom n'existait — cause invisible depuis l'app. | Le dossier est **déplacé** vers `<racine>/corbeille_batiments/<guid>_<nom>` et revient si l'utilisatrice annule. **Jamais purgé automatiquement** : il contient des factures, donc des pièces comptables. |

**Vérifié en exécution** : 10 assertions sur les chemins (relatif à l'écriture, absolu à la lecture, ancien format toujours résolu, correction également relativisée, dossiers nommés) · 3 sur le cache (cache actif, vidage effectif, rechargement) · 10 sur la corbeille bâtiment (déplacement, collision de renommage levée, photos et factures conservées puis restaurées, corbeille vidée, bâtiment sans dossier sans exception).

Un défaut de mon propre correctif a été trouvé par l'exécution, pas par la relecture : `ViderCache` appelait `Object.Destroy`, refusé hors mode Play (« Destroy may not be called from edit mode »). Corrigé en `DestroyImmediate` hors Play. *Même leçon que `SaveAll` : compiler ne prouve rien.*

**Piège évité au passage** : `pdfPath` devenant relatif, la comparaison de `ChargePanel:328` (`_fPdf != _edit.pdfPath`) serait devenue toujours vraie et aurait **recopié le PDF de charge à chaque enregistrement**. Les deux termes sont désormais comparés en absolu.

**Reste non migré** : les enregistrements existants gardent leurs chemins absolus (ils restent lisibles, mais ne suivront pas un futur déplacement tant que la facture n'est pas ré-émise) et les PDF déjà copiés dans les anciens dossiers `Batiment/<GUID>/` n'ont pas été déplacés. Une migration au démarrage serait possible ; elle n'a pas été faite ici, c'est une décision à prendre.

**Vérifié et sain** (fausses pistes écartées une par une) : gardes de `Substring`/`Split` (`TryDecompose`, `Initiales`, SIRET, autocomplétion) · accumulation de listeners (cibles recréées à chaque construction, `_manualEditable` vidé, `ObjectiveManager` dans `Start`) · divisions (`NbPeriodes` ne renvoie jamais 0) · les appelants de `Renommer*` honorent le `false` et reviennent en arrière · `SaveAll`/`SauvegardeDeFermeture` itèrent sur une copie.

### La phrase de règlement proposée à l'écran était inerte à l'ouverture (17/09/2026)

**CONFIRMÉ par lecture du chemin d'appel** · `FactureRegulPanel.cs`, `FactureDepotPanel.cs` — `DefaultSomme` / `LoadIntoUI`

Trouvé en reprenant le chantier, dans une modification **écrite mais pas encore commitée** : `DefaultSomme()` avait été rendue sensible au signe du solde, pour que le champ « Phrase de règlement » ne propose plus « SOMME À NOUS RÉGLER » quand le PDF, lui, imprimera « SOMME QUI VOUS SERA REMBOURSÉE » (`FactureEmission.PhraseSomme`). L'intention était juste, le câblage non : `LoadIntoUI` appelle `_autoSomme = DefaultSomme()` **avant** de remplir `_provisions` / `_ancien` et le sélecteur d'année. Le solde lu valait donc toujours 0.

Conséquence : à l'ouverture d'un panneau en situation de remboursement, **l'écran continuait d'afficher exactement la phrase que le correctif visait à supprimer**. Rien ne la recalculait ensuite — ni la saisie des provisions, ni le changement d'année ; seule une retouche manuelle de la date d'échéance, via le seul listener câblé sur `RefreshSommeDefault`, faisait apparaître la bonne phrase.

Correctif : `RefreshSommeDefault()` appelé **en fin de `RefreshTotaux()`** dans les deux panneaux. C'est le point de passage unique de tout ce qui change le solde — provisions, ancien dépôt, nombre de périodes, année, chargement de la fiche — donc une seule ligne par panneau plutôt qu'un listener sur chaque champ. La règle « une phrase saisie à la main est respectée » reste portée par la comparaison à `_autoSomme` : inchangée.

*La leçon n'est pas celle de `SaveAll` (« compiler ne prouve rien ») mais sa voisine : **un correctif peut être exact et n'être jamais atteint**. Ici il fallait suivre l'ordre de `LoadIntoUI`, pas relire `DefaultSomme`.* À vérifier en Play, comme tout ce qui touche à l'UI.

### Réorganiser les champs des 4 panneaux de facture — fait (17/09/2026)

**Principe : l'écran suit l'ordre du document imprimé.** Aujourd'hui le RIB est le premier champ alors qu'il s'imprime en dernier, et le « N° interne » vient après le numéro alors qu'il s'imprime au-dessus — d'où l'impression de désordre. Trois sections, correspondant aux trois zones du PDF :

| Section | Champs |
|---|---|
| **1. En-tête du document** | Date · Texte / N° interne · Format du n° + N° de facture |
| **2. Contenu** | Texte de présentation (+ aperçu collé dessous) · puis les champs propres au type (charges de l'année, ancien/nouveau dépôt, période de loyer…) |
| **3. Règlement** (pied) | Date d'échéance · Phrase de règlement · RIB CIPL · case pénalités de retard |

Gain principal : l'échéance, la phrase qu'elle alimente et le RIB deviennent voisins — la mention « (défaut de la phrase de règlement) » devient inutile, le lien se voit.

**Deux règles à tenir** : le **même ordre dans les quatre panneaux** (seule la section 2 varie selon le type) ; et dans chaque section, **le variable avant le stable** (date et montants changent à chaque facture, RIB et format de numéro une fois par an).

**Coût** : trois `UIFactory.Section` par panneau et un déplacement d'appels dans les méthodes de construction. Aucune logique touchée, aucun champ ajouté ni retiré — les 88 tests restent valides. À faire **panneau par panneau**, en commençant par la régularisation, avec validation visuelle en Play avant de propager aux trois autres.

**Fait sur `FactureRegulPanel` (17/09).** Un écart assumé avec la spec, décidé avec l'utilisatrice : les champs propres au type **ne fusionnent pas** dans la carte « Contenu », ils gardent leur carte et leur couleur juste en dessous — sinon « Montants » et « Dépôt de garantie » perdaient leur titre et la carte devenait très longue. Six cartes, donc, dans cet ordre :

| Carte | Champs |
|---|---|
| Destinataire (vert) | Nom · Adresse · SIRET — inchangée |
| **En-tête du document** (bleu) | Date · Texte / N° interne · Format du n° · ligne N° de facture |
| **Contenu** (taupe) | Texte de présentation · aperçu variables remplacées |
| Régularisation des charges (violet) | Année · charges · provisions · totaux — contenu inchangé |
| **Règlement** (ambre) | Date d'échéance · Phrase de règlement · RIB CIPL · case pénalités de retard |
| Options & envoi (vert) | TVA sur les débits · mode d'envoi · email · note « rien n'est émis » |

Libellé raccourci au passage : « Date d'échéance (défaut de la phrase de règlement) » → **« Date d'échéance »**. La parenthèse ne servait qu'à compenser l'éloignement des deux champs ; ils sont maintenant voisins.

Le vrai risque n'était pas le rendu mais **l'ordre de construction** : `RefreshEntetePreview` déréférence `_entetePreview` sans garde, et `BuildContext` lit `_provisions`, `_date` et `_anneeDD`. Vérifié avant de déplacer quoi que ce soit — tous sont créés avant les champs qui les déclenchent, et ni `UIDropdown.Create` ni `UIFactory.Toggle` n'invoquent leur callback à la création (sinon le code d'avant planterait déjà, `_chargesBox` étant affecté après `_anneeDD`). `_echeance` passant désormais **après** `_provisions`, sa lecture dans `DefaultSomme` a été gardée (`_echeance != null ? … : ""`), sur le modèle de `RefreshNumero`.

**Vérifié** : compilation propre (seule subsiste l'erreur Burst connue, venant des packages inutilisés) et **88/88 tests EditMode verts** — aucune logique n'ayant été touchée, un rouge aurait signalé un déplacement mal fait. **Reste à valider en Play** : c'est de la construction d'UI, aucun test ne la couvre.

**Propagé aux trois autres panneaux le 17/09, après validation du rendu de la régularisation.** Les quatre ont maintenant **exactement les six mêmes cartes dans le même ordre** ; seule la quatrième change de titre et de couleur :

| Panneau | Carte du type |
|---|---|
| Loyer | **Loyer facturé** (violet) — « Période facturée » l'a rejointe, voir ci-dessous |
| Régularisation | Régularisation des charges (violet) |
| Refacturation | Charge à refacturer (violet clair) |
| Dépôt | Dépôt de garantie (bleu canard) |

Deux points au-delà du simple déplacement :

- **« Période facturée » quitte l'en-tête du panneau Loyer** pour rejoindre les montants. Elle était dans la carte « Facture » avec le numéro, alors qu'elle s'imprime dans le **corps** du document — et c'est elle qui détermine le loyer de la ligne, donc elle appartient au même bloc que lui. La carte « Montants » est renommée « Loyer facturé » en conséquence : « Montants » serait devenu faux.
- **Les noms de couleurs mentaient d'un fichier à l'autre.** `CoAmbre` valait `#854F0B` (ambre) dans Loyer et `#7A5AA6` (**violet**) dans Refacturation : un même nom, deux couleurs, dans quatre fichiers qu'on modifie toujours ensemble — exactement le genre de piège qui fait qu'on colle la mauvaise teinte sans s'en apercevoir. `CoContenu` (taupe) et `CoReglement` (ambre) portent désormais le même rôle et la même valeur dans les quatre ; le violet des charges s'appelle `CoCharge`.

La garde `_echeance != null` de `DefaultSomme` a été posée dans les quatre, pas seulement là où elle est nécessaire aujourd'hui : le champ passe désormais **après** les montants dans les quatre panneaux, et la prochaine réorganisation ne doit pas avoir à y repenser.

**Vérifié après propagation** : plus aucune trace de l'ancienne carte « Facture » ni de couleur orpheline (`CoTaupe`/`CoAmbre` ne subsistent que dans un commentaire d'explication), compilation **sans aucune erreur** — pas même Burst cette fois — et **88/88 tests EditMode verts**.

### Six défauts trouvés par l'usage, et une évolution (22/09/2026)

Signalés en utilisant l'application, tous **CONFIRMÉS** par lecture du chemin de code. Trois ont la même signature que les défauts déjà rencontrés ici : ce n'est pas le calcul qui est faux, c'est qu'il n'est jamais atteint, ou qu'il porte sur la mauvaise donnée.

#### 1. Un champ affichait sa valeur DEUX fois — `InputAndText.cs`

Capture à l'appui : « Taille Batiment : 1250 [1250] m² », le texte et le champ de saisie côte à côte.

Cause, vérifiée en lisant l'état sérialisé des prefabs : **`textSaved` et `inputModify` sont actifs tous les deux dans les prefabs** — les 14 champs du bâtiment, les 11 du locataire, sans exception. L'affichage correct ne tenait qu'au fait qu'un `Modify()` ou un `ApplySave()` passe et masque l'un des deux. Tout champ qu'aucun de ces appels n'atteint montrait donc la valeur en double — et `Modify()` **sautait** justement `tailleBatimentText` dès qu'il y avait plus d'un locataire.

Correctif à la racine : `Awake()` pose l'état de repos (lecture seule) une fois pour toutes. Un seul endroit, tous les champs de l'application — y compris ceux des écrans Achat et Travaux, qui avaient le même défaut latent.

#### 2. L'échéance du loyer ignorait le jour où le loyer est demandé — `FactureLoyerPanel.cs`

Le panneau proposait « date de facture **+ 30 jours** », alors que le suivi calcule l'échéance d'une période avec `loc.jourDemandeLoyer` (« le X du mois »). La facture et sa propre ligne de suivi n'avaient donc pas la même échéance — et la phrase de règlement imprimée sur le PDF, qui découle de l'échéance, annonçait une date qui ne correspondait à rien.

La règle vivait déjà dans `FacturationSuivi.Lignes` : elle est extraite en `FacturationSuivi.EcheanceLoyer(loc, année, période)`, appelée par le suivi **et** par le panneau. Une seule règle, donc plus de divergence possible.

*Piège au passage, le même que la phrase de règlement du 17/09* : l'échéance était posée **avant** le sélecteur de période dans `LoadIntoUI`. Calculée là, elle n'aurait jamais vu la période choisie. Elle est donc posée après, et se recalcule à chaque changement de période ou d'année — tant qu'elle n'a pas été saisie à la main.

#### 3. La surface du bâtiment gonflait à chaque locataire — `BatimentPrefab.cs`

La surface du bâtiment était **dérivée** : dès deux locataires, `tailleBatiment = somme des lots`. Ajouter un locataire de 1500 m² à un bâtiment de 1250 m² en faisait un bâtiment de 2750 m². Pire, le lot du **premier** locataire n'était pas saisissable (il recopiait la surface du bâtiment) : impossible de le réduire pour faire de la place au second, alors que c'est exactement ce que la situation demandait.

Règle retenue — **le bâtiment est la donnée source** : sa surface est celle qu'on saisit, et elle ne bouge plus jamais toute seule. Chaque lot se saisit librement, pré-rempli avec ce qui reste. Un lot jamais saisi (`tailleLot <= 0`) prend le disponible — partagé à parts égales s'il y en a plusieurs — donc il **se réduit** quand un autre locataire prend sa part. Si les lots saisis dépassent le bâtiment, la saisie est respectée et l'écart est signalé une fois (pas à chaque enregistrement).

Aucun champ ajouté, donc **aucune migration** : la convention « `tailleLot <= 0` = pas encore défini » laisse les fiches existantes, qui ont toutes une valeur > 0, comme des surfaces fermes.

#### 4. Le suivi de facturation restait vide jusqu'au redémarrage — `LocataireSuiviInline.cs`

Deux causes, cumulées :

- **La sortie se faisait avant `Build()`.** À la création d'une fiche, la section de suivi est construite *avant* que le locataire entre dans `listLocataire` : `GetLocataire()` renvoyait `null`, le bandeau se masquait — et rien ne le rallumait, `Refresh()` sortant lui aussi sur `_loc == null` sans jamais relire. Pire, la coquille (`extBody`/`extTitre`) était alors perdue : l'appel suivant arrivant sans elle, le décor interne se serait reconstruit **par-dessus** la section de la fiche.
- **Régler le loyer ou le dépôt ne rafraîchissait rien.** Ces deux réglages passent par leurs propres pop-ups (`RevisionPanel`, révision du dépôt), pas par un panneau de facture — or seuls les panneaux de facture appelaient `RefreshFor`. Le tableau restait donc tel qu'il était avant le réglage.

Correctifs : `Refresh()` relit le locataire à chaque passage et rallume le bandeau ; `Setup` ne remplace la coquille que si on lui en fournit une ; et `LocataireSuiviInline.RefreshTous()` est appelée depuis `BatimentPrefab.SaveAfterModifyToDoListLocataire()` — le point de passage unique des **18** chemins qui modifient un locataire, plutôt qu'une garde répétée chez chacun.

#### 5. Le calendrier du loyer était écrit deux fois — `FacturationAlertes.cs`

Vérification faite en confirmant la règle voulue (rappel à J‑22, envoi à J‑15, échéance au jour de demande) : elle était **déjà implémentée**, avec les bons délais. Mais la même règle vivait à deux endroits — le suivi et les alertes — et les deux copies avaient divergé, exactement comme la ligne de tableau la veille.

- **Aucun rappel si le jour de demande n'est pas renseigné** : l'alerte abandonnait (`return null`) là où le suivi plaçait l'échéance au 1er. Des loyers à échéance s'affichaient sans le moindre rappel — sur les fiches au réglage incomplet, donc les plus exposées.
- **Les mois de facturation cochés étaient ignorés par le suivi** : sur un bail trimestriel facturé en février, l'alerte annonçait le 05/02 et le tableau le 05/01.

Les alertes passent désormais par `FacturationSuivi.EcheanceLoyer`, et les mois cochés commandent les échéances (décision de l'utilisatrice, 22/09) — de même que `PeriodeIndex`, son inverse, sans quoi la clé `loyer-{année}-P{n}` ne désignerait plus la bonne ligne. Détail dans `FACTURATION_CIPL_PENNYLANE.md`.

#### 6. Le suivi annonçait des envois qui n'avaient pas eu lieu — `FacturationSuivi.cs`

Constaté en documentant le point 5 : le statut se déduisait de la **date**, pas du fait. Une facture préparée à plus de 15 j de l'échéance était « En attente d'envoi », puis `EtatDe` la basculait toute seule en « Envoyé » à J‑15 — même si l'envoi par email n'était pas activé, cas où le panneau affichait pourtant « rien n'a été émis ». Le suivi en disait donc plus qu'il ne savait, le défaut exact de H3.

Le statut suit désormais le fait : les quatre panneaux savent déjà s'ils viennent du chemin « PDF seul » ou « envoi réussi » (`EnvoyerPuisFinaliser` n'enregistre qu'après acquittement du serveur) ; cette information est simplement transmise jusqu'à `MarquerEnvoye`. Une facture non partie reste « En attente d'envoi », ne devient jamais « Impayée » — on ne reproche pas un impayé à qui n'a rien reçu — et **garde son alerte allumée**, `DejaTraite` ne la comptant plus comme traitée. `EstDejaEmise`, elle, continue de la compter comme émise : le PDF existe, le numéro est consommé, H2‑bis reste couvert.

Le calendrier (15 j + 1 semaine) était écrit **trois fois**, dont un `22` en dur dans `Lead`. Une seule source désormais.

#### 7. Nouveau — envoi groupé proposé au lancement (`FactureEnvoiAuto.cs`)

Demandé après le point 6, et rendu possible par lui : puisqu'une facture reste « en attente d'envoi » tant qu'elle n'est pas partie, l'application sait exactement ce qui doit partir. Au démarrage, elle propose la liste des factures dont la date d'envoi (J‑15) est atteinte, **retards compris**.

*Rien ne part tout seul.* Un envoi ne se rappelle pas : la fenêtre montre locataire, destinataire et montant, chaque ligne se décoche, « Plus tard » reste disponible, et une seconde confirmation récapitule avant le départ. C'est l'arbitrage de l'utilisatrice face à un envoi réellement automatique — le travail manuel disparaît, la relecture non.

L'envoi se fait en série, chaque succès étant sauvegardé immédiatement : une interruption au milieu laisse marqué parti ce qui est parti. Un échec laisse la ligne en attente pour le lancement suivant. Le statut est mis à jour directement, sans repasser par `FactureEmission` — le numéro est déjà consommé, y repasser en prendrait un second.

La règle de sélection est **séparée de l'écran** et l'existence du PDF lui est injectée : elle se teste sans disque et sans mode Play (10 assertions). Vérifiée par mutation — en retirant la garde de date puis celle de statut, trois tests échouent (« une facture déjà envoyée repart », « une facture non due part trop tôt »).

#### Vérification

**207 tests EditMode verts** (126 + 62 + 3 + 5 + 2 + 9). Les nouveaux (`SurfacesEtEcheanceTests`) couvrent la règle des surfaces avec le scénario exact rapporté et le calcul d'échéance (jour borné à la longueur du mois — « le 31 » en février tombe le 28, ou le 29 en année bissextile — périodicités trimestrielle, semestrielle, annuelle), plus le fait que les lignes du suivi passent bien par la règle extraite.

**Les tests mordent** : vérifié par mutation. En remettant l'ancien calcul de surface, trois tests échouent avec le bon message (`Expected: 750, But was: 1250` — le bug rapporté, exactement), puis repassent au vert après restauration.

**Reste à voir en Play** : le champ de taille qui n'affiche plus qu'une valeur · la répartition des surfaces sur un bâtiment à deux locataires · l'échéance proposée à l'ouverture du panneau Loyer et au changement de période · le suivi de facturation rempli **sans quitter l'application**, juste après avoir créé un locataire et réglé son loyer et son dépôt.

### Ligne de suivi en prefab — fait (21/09/2026, soir)

Spécifié le 17/09, réalisé ici. Les deux vues du suivi construisaient **la même ligne** en code, chacune de son côté — c'est ce qui a permis au défaut d'alignement (`childForceExpandWidth`) d'exister en double, et, on le découvre au passage, à la garde « Quittance » de diverger (voir plus bas).

**Livré** : `Assets/Prefab/SuiviFactureRow.prefab` + `Facturation/SuiviRowUI.cs`, et `LocataireSuiviInline` converti. `FacturationSuiviPanel` reste **en code**, comme prévu : c'est le filet de comparaison des deux rendus en Play.

Le prefab a été construit **par script** (Roslyn, via MCP) en réutilisant `UIFactory` plutôt qu'en re-saisissant couleurs et largeurs à la main : le rendu est identique par construction, pas par recopie.

**Trois choses que la spécification du 17/09 n'avait pas vues** — toutes trouvées en faisant, pas en relisant :

| Surprise | Conséquence |
|---|---|
| **`UIFactory.Rounded()` génère son sprite au runtime** (`UIFactory.cs:11`) : ce n'est pas un asset, la référence ne survit pas à la sérialisation du prefab | Les coins arrondis auraient disparu, silencieusement. `SuiviRowUI` repose le sprite sur chaque `Image` à l'instanciation — donc le prefab **ne supprime pas** tout le code de style, contrairement à ce qu'annonçait la spec |
| **Aucune des deux vues ne peut recevoir de référence d'inspecteur** : `LocataireSuiviInline` est un `AddComponent` à la volée (`LocataireFacturationFields.cs:290`), `FacturationSuiviPanel` un `new GameObject` | La spec supposait un câblage direct, impossible. Le prefab transite par `LocatairePrefab` (`suiviRowPrefab`), que les deux vues ont déjà en main via `_fiche`. Pas de dossier `Resources/` créé : le projet n'en a aucun et n'appelle jamais `Resources.Load` |
| **L'en-tête n'est pas une ligne comme les autres** (ni pastille, ni actions) | La laisser en code aurait fait *réapparaître* le défaut visé : deux constructions pour un même alignement. Elle passe par le même prefab via `SetupEntete`, où les colonnes État et Actions deviennent du texte nu. Idem pour la ligne « Aucune facture » (`SetupVide`) |

**Verrouillé par 9 tests** (`Assets/Editor/Tests/SuiviRowPrefabTests.cs`) — c'est le point que cette revue redoutait, « des références sérialisées fragiles, **invisibles aux tests** ». Ils le rendent visible sans passer par le Play : les 11 références du prefab, `childForceExpandWidth == false` (le défaut historique, désormais figé), les largeurs de colonnes comparées aux constantes de `SuiviRowUI`, le câblage de `suiviRowPrefab` sur la fiche relu **depuis le disque**, puis le comportement de `Setup` / `SetupEntete` / `SetupVide` (nombre d'actions activées, pastille non cliquable sur période clôturée, cellules masquées puis rétablies, callback rattaché à la bonne ligne).

**Les tests mordent-ils vraiment ?** Vérifié par mutation : en retirant une seule ligne de `SetupVide`, la suite échoue avec le bon message (`montant masqué — Expected: False, But was: True`), puis repasse au vert après restauration. *Un test vert ne prouve rien s'il n'atteint pas le code — même leçon que « compiler ne prouve rien ».*

**Piège d'outillage découvert ici** : les tests salissaient `Assets/TextMesh Pro/.../LiberationSans SDF - Fallback.asset` (+114 lignes) à **chaque exécution**. Un libellé accentué, un « € » ou un tiret cadratin remis à TMP peuple l'atlas dynamique de la police de repli, qui est un asset du dépôt. Les libellés de test sont donc en ASCII, avec un commentaire qui interdit de les « corriger » — les assertions ne comparent que la chaîne transmise, jamais le rendu. L'asset a été remis à son état commité.

**Reste à voir en Play** : alignement en-tête / lignes, libellé long tronqué en ellipse, année sans facture, menu d'état, les boutons d'action dans chaque cas (`PDF` + `Corriger`/`Refaire`, `Rappel`, `Quittance`), période clôturée sans action — puis comparer avec la vue plein écran restée en code.

**Ensuite** : convertir `FacturationSuiviPanel` au même prefab une fois les deux rendus comparés, puis `FacturationHomeSection`. Ne **pas** convertir `ReglagePanel` ni les 4 panneaux de facture : formulaires construits une seule fois, aucune répétition — arbitrage inchangé.

### La garde « Quittance » manquait dans la vue plein écran (21/09/2026)

**CONFIRMÉ par comparaison des deux copies** · `Facturation/FacturationSuiviPanel.cs`

Trouvé en convertissant la ligne en prefab, donc en mettant les deux constructions côte à côte. `LocataireSuiviInline` vérifiait `reellementEmise` (numéro **et** PDF non vides) avant de proposer « Quittance » ; `FacturationSuiviPanel` ne le vérifiait pas.

L'état « Payé » peut être forcé à la main sur une ligne **jamais émise** : dans la vue plein écran, on pouvait donc éditer une quittance — un reçu de paiement — pour une facture qui n'existe pas. Correctif : même garde des deux côtés.

*C'est le second défaut imputable à cette duplication, après l'alignement. Le motif de passer la ligne en prefab n'était donc pas cosmétique : deux copies d'un même écran divergent, et la divergence se paie en pièces comptables fausses.*

### Nettoyage du projet (16/09/2026)

Méthode : rien n'a été supprimé sur une impression. Pour chaque fichier, le GUID de son `.meta` a été cherché dans les 37 scènes/prefabs/assets **et** son nom dans les 111 scripts ; seuls les candidats à **zéro référence des deux côtés** ont été retenus, puis vérifiés un par un. Tout ce qui a été supprimé est suivi par git, donc récupérable par `git checkout`.

**Supprimé** — 5 scripts (756 lignes), 13 méthodes (85 lignes), 2 prefabs, 1 image :

| Type | Éléments |
|---|---|
| Scripts | `PLU/PLUPanelUI.cs` · `Maps/TextureScale.cs` · `Tools/NestedScrollRect.cs` · `Maps/PineSpriteGenerator.cs` · `Facturation/FacturationGlobalPanel.cs` (353 l., voir ci-dessous) |
| Prefabs | `Building Card.prefab` (ancienne version — c'est `BuildingCardItem.prefab` qui porte le composant `BuildingCard` en scène) · `Toggle Possiblity .prefab` |
| Image | `UI/sec_arrow.png` |
| Méthodes | `BatimentManager.LoadBatiment`/`GetSaveFolder` · `BatimentPrefab.GetBatimentData` · `DateInputController.LoadSavedDate` · `MapController.getLat`/`getLon` · `TileLoader.HidePin` · `TrimestreInput.CannotModify`/`GetPreviousYear` · `SaveIO.LoadSave` · `ScrollAutoResize.ScrollToBottom` · `RentabiliteCalculator.CoutInterets` · `TravauxController.GetSaveData` |

**Faux positifs écartés — à ne jamais supprimer sur la foi d'une recherche de références :**

| Élément | Pourquoi il n'a aucune référence | |
|---|---|---|
| `FacturationBootstrap`, `EntrepriseBootstrap` | `[RuntimeInitializeOnLoadMethod]` : **Unity les exécute sans que personne ne les cite**. Les supprimer aurait cassé l'initialisation au démarrage. | gardés |
| `papperData.cs` | Le fichier ne porte pas le nom de ses types : il déclare `AnnuaireEntreprise` et `AnnuaireResponse`, bien utilisés par la recherche SIRET. | gardé |
| `StreamingAssets/logo_cipl.png` | StreamingAssets se charge **par chemin**, pas par GUID — c'est le logo imprimé sur les factures. | gardé |
| `InputFieldMinHeight.CalculateLayoutInput*` | Implémentations de `ILayoutElement`, appelées par le moteur de layout. | gardées |
| `ScrollAutoResize.OnTransformChildrenChanged` | Message Unity. | gardé |

**Refactorisé** — deux duplications supprimées, sans changement de comportement :
- `Sanitize` existait en **5 exemplaires** (4 identiques + la variante de la quittance) → `DossiersDonnees.NomFichier`, à côté de `NomDossier`. La quittance garde sa spécificité (espaces en tirets, repli « Quittance ») mais s'appuie sur la base commune.
- Les listes de formats de numérotation étaient **dupliquées dans les 4 panneaux** de facture, avec le risque qu'elles divergent → `FactureNumerotation.Labels`/`.Ids`, source unique.

**Vérifié** : compilation propre, puis 9 assertions en exécution (neutralisation des caractères interdits, `null` toléré, nom valide inchangé, formats de numérotation, survie des bootstraps, des types Pappers et de `BuildingCard`, `CheminPdf` et `Mensualite` toujours opérationnels).

**`FacturationGlobalPanel.cs` — supprimé sur décision de l'utilisatrice (16/09)**. Vue globale des créances (filtres bâtiment/banque/locataire/état + tableau des factures dues), jamais branchée à aucune scène ni à aucun code. La colonne « Créances » du menu est assurée par `FacturationHomeSection`, qui reste en place. Le fichier étant suivi par git, il est récupérable par `git checkout` si le besoin d'une vue globale filtrable revient — c'est la référence à reprendre plutôt qu'à réécrire.

**Laissé en l'état** : `Col(string)` dupliqué en 4 exemplaires d'une ligne (gain nul, risque non nul).

**Audit de la scène — rien à nettoyer.** `SampleScene` : 643 GameObjects, **0 script manquant** (aucune référence cassée), **0 coquille vide** (objet sans composant ni enfant). Les **29 objets désactivés ne sont pas du déchet** : ce sont les `Template` des dropdowns — Unity les désactive par conception — et les écrans masqués activés par code (`Calcul`, `Savegarde`, `Panel PLU/GroupeCadastre`, `Objectif`). Les 4 « noms dupliqués sous un même parent » sont des éléments répétés légitimes (chips de modes du calcul rapide, colonnes d'en-tête). Les suppressions de fichiers n'ont créé **aucun orphelin en cascade** (scan relancé après coup).

**Reste le plus gros gisement, non traité : les packages.** `Packages/manifest.json` déclare `com.unity.feature.2d`, qui tire 2D Animation, Aseprite, PSD Importer, SpriteShape, PixelPerfect, Tilemap Extras, **Burst**, Collections et Mathematics — **aucun fichier du projet ne les référence**, et c'est Burst qui produit les erreurs de compilation observées dans la console. S'y ajoutent `com.unity.timeline`, `com.unity.visualscripting` (son unique `using`, dans `BatimentPrefab.cs`, était inutile et a été retiré) et une quinzaine de modules sans emploi pour une application de gestion en UI 2D (`ai`, `cloth`, `particlesystem`, `physics`, `physics2d`, `terrain`, `vehicles`, `vr`, `xr`, `wind`, `umbra`, `video`…).

Non fait volontairement : retirer un package touche la résolution de dépendances et la compilation de **tout** le projet. Le gain est du confort (temps de compilation, poids du build), pas une correction — le rapport risque/bénéfice ne justifie pas de le faire sans validation. À traiter par petits lots, avec une compilation vérifiée entre chaque.

### Protocole d'émission mutualisé — `FactureEmission` (16/09/2026)

**Le problème de fond, pas ses symptômes.** Les quatre panneaux de facture recopiaient la même séquence d'émission : détecter une facture déjà émise, suffixer le numéro et le nom de fichier, générer, puis marquer le suivi et avancer la séquence. Quatre copies, donc quatre occasions d'oublier — et c'est exactement ce qui s'est produit : **H2** (garde présente sur un seul panneau), **H2-bis** (deux états sur quatre), **G1** (`pdfPath` absolu répété), **G3/G4** (chemins construits à la main).

`Facturation/FactureEmission.cs` porte désormais cette règle, en deux temps dont l'ordre n'est pas négociable :

| | |
|---|---|
| `Preparer(loc, key, numeroPropose)` | **Avant** la génération. Décide correction ou première émission, renvoie le numéro à imprimer et le suffixe de nom de fichier (`-corrigee2`) — donc le PDF d'origine n'est jamais écrasé. |
| `Enregistrer(...)` | **Après** une génération réussie. En correction : conserve numéro et statut, n'avance pas la séquence. En première émission : marque la ligne envoyée, consomme le numéro, oublie l'ID saisi. |

`RibNom(ribId)` remplace au passage quatre copies de trois lignes.

**Vérifié** : plus aucun panneau n'appelle `EstDejaEmise`, `MarquerCorrige` ou ne touche `factureSeq` directement — la recherche ne renvoie rien. Chacun se contente de deux appels au service.

**Ce qui n'a pas changé** : chaque panneau garde son libellé propre (« charges passées en payé », « le montant du dépôt n'a pas été modifié ») via un paramètre optionnel — la mutualisation ne devait pas appauvrir les messages, c'est le genre de régression invisible qui passe les tests et se voit à l'usage.

**Six tests ajoutés** (`FactureEmissionTests`) : première émission qui consomme un numéro · seconde émission traitée en correction sans rien consommer · corrections successives numérotées avec une seule séquence consommée en tout · loyer préparé en avance également protégé · messages.

**Second lot — les helpers partagés (16/09)** : `TryDate` existait en **quatre copies strictement identiques** et `NumeroPrefixe` aussi (la seule différence entre elles était un commentaire — vérifié, il n'y avait pas de divergence réelle, mais rien ne l'empêchait).

- `TryDate` → `Tools/SaisieDate.cs`, pendant de `SaisieNumerique` : formats explicites (`dd/MM/yyyy`, `d/M/yyyy`, `dd/MM/yy`, `d/M/yy`) en culture invariante. Utilisable par les autres formulaires qui parsent des dates à la main.
- `NumeroPrefixe` → `FactureNumerotation.Prefixe`, à côté des formats `AN`/`AMN`/`AJMN` qu'il consomme. **C'est là que se jouera H1** : l'arbitrage sur la numérotation (séquence unique globale ou identifiant stable de locataire) ne touchera plus qu'un seul endroit.

Les noms locaux sont conservés en délégation d'une ligne : **aucun site d'appel n'a été modifié**, donc aucun risque de rupture. Vérifié : plus aucun `DateTime.TryParseExact` ni `switch (fmt)` dans les panneaux. 12 tests ajoutés (`SaisieDateTests`), dont le cas `03/04/2026` qui doit toujours être le 3 avril.

Les quatre panneaux passent de 2 850 à **2 770 lignes** — le gain en volume est modeste, l'important est ailleurs : les règles qui produisaient des bugs (émission, numérotation, dates) n'ont plus qu'une seule implémentation.

**Prochaine étape possible** : ce qui reste dupliqué est de la **construction d'UI** (`LoadIntoUI`, `SaveFromUI`, `ShowPreview`, `ZoomBtn`, `RefreshEntetePreview`…), pas de la règle métier. L'extraire demanderait une classe de base MonoBehaviour et présente un risque bien supérieur pour un gain surtout cosmétique. À ne faire que si ces panneaux doivent réellement évoluer.

### Tests automatisés — 66 tests EditMode (16/09/2026)

Jusqu'ici, **chaque vérification était refaite à la main**, par scripts jetables. Ces scripts sont désormais des tests que n'importe qui relance en moins de 2 secondes.

**Comment les lancer** : `Window → General → Test Runner → EditMode → Run All`. Aucun mode Play requis.

| Fichier | Couvre |
|---|---|
| `Assets/Editor/Tests/SaisieNumeriqueTests.cs` | M3 / M3-bis — symétrie point/virgule (8 paires), séparateurs mixtes, espaces insécables, saisie invalide refusée, champ vide |
| `Assets/Editor/Tests/FacturationSuiviTests.cs` | H2 / H2-bis / H3 / G1 — les 4 états « émise », numéro et séquence préservés à la correction, statut conservé, impayé à échéance+15, échéance en culture invariante, `pdfPath` relatif et ancien format absolu |
| `Assets/Editor/Tests/DonneesDisqueTests.cs` | M5 / G1 / G2 / G4 — nommage des fichiers, aller-retour relatif↔absolu, migration acceptée puis refusée sans rien écraser, corbeille photos (suppression → annulation → purge), vidage du cache |
| `Assets/Editor/Tests/InfraTest.cs` | L'infrastructure elle-même |

**Où vivent les tests, et pourquoi là.** Le code de CIPL est dans `Assembly-CSharp` (aucun `.asmdef`), et un assembly défini par `.asmdef` **ne peut pas** référencer `Assembly-CSharp`. Les tests sont donc sous `Assets/Editor/`, compilés dans `Assembly-CSharp-Editor`, qui lui y a accès — et exclus des builds. Vérifié empiriquement avant d'écrire la suite.

**Isolation des données — le point le plus important.** `DonneesDisqueTests` écrit sur le disque. Son `SetUp` mémorise la racine réelle puis bascule `SaveLocationService` sur `%TEMP%/cipl_tests_<guid>/CIPL_Saves` ; son `TearDown` **restaure la racine réelle en première instruction**, avant même de nettoyer. Vérifié après exécution : racine rendue, 3 bâtiments intacts. Toute évolution de ces tests doit préserver cette garantie.

*Deux tests ont échoué au premier lancement, et les deux fois c'était le test qui avait tort, pas le code : un chemin de PDF placé hors racine (que `VersRelatif` laisse donc absolu, à juste titre) et un `Debug.LogError` volontaire non déclaré par `LogAssert.Expect`.*

**Non couvert** : la corbeille des bâtiments (`DeleteBatiment`/`RestoreBatiment` exigent une scène et `BatimentManager.Instance` — vérifiée par réflexion en session, pas figée en test) · `FermetureGuard` avec fiche réellement en édition · les gardes d'avoir et les parcours UI des panneaux. Ce sont les mêmes points que la liste « à passer en Play ».

### Seconde passe — findings non numérotés (16/09/2026)

Le tableau ci-dessus ne couvrait que les findings **numérotés**. L'exploration en avait remonté d'autres, jamais promus en findings, donc passés à travers. Traités depuis :

| Sujet | Statut |
|---|---|
| Division par l'indice de départ sans garde → `NaN` écrit dans `loyerAnnuel` | **Corrigé** — indice ≤ 0 rejeté avant calcul |
| Repli silencieux jusqu'à 5 ans sur l'indice de départ | **Corrigé** — avertissement affiché quand la base diffère du trimestre demandé |
| `new DateTime(d.Year+1,…)` levant un 29 février **après** l'écriture du loyer | **Corrigé** — date calculée avant écriture, jour borné au mois |
| `Split('-')[1]` sur un trimestre au format ancien → modale impossible à ouvrir | **Corrigé** — `InseeIndiceService.TryDecompose` aux deux sites |
| `TrimestreInput` empilant ses listeners à chaque ouverture | **Corrigé** — `RemoveAllListeners` avant câblage |
| Restauration écrivant dans l'entreprise B un bâtiment supprimé dans A | **Corrigé** — racine capturée à la suppression et vérifiée à l'annulation |
| Créer une entreprise sur un dossier déjà occupé → adoption des données existantes | **Corrigé** — détection + refus, renvoi vers « Ouvrir » |
| `GetSaveRoot` retombant muettement sur le dossier par défaut | **Corrigé** — alerte (une seule fois par racine manquante) |
| Quittance émise sur une facture jamais générée | **Corrigé** — bouton conditionné à un numéro + un PDF réels |
| Erreur réseau INSEE avalée → tableau plat sans indicateur | **Corrigé** — journalisée explicitement |
| `ParseDate` en culture courante + repli muet sur aujourd'hui | **Corrigé** — culture invariante + avertissement |
| Dépôt très en retard : `AddYears(1)` retombait dans le passé | **Corrigé** — avance jusqu'à repasser dans le futur |

**Écartés après vérification** (l'exploration se trompait) :
- *« Revalider le dépôt repousse la révision de 2 ans »* — non reproductible : la garde `Today < dr` bloque une seconde révision, et calculer depuis l'échéance plutôt que depuis aujourd'hui est correct (sinon l'anniversaire dérive).
- *« `MoisDeRevision` retombe sur aujourd'hui »* — les 5 sites de calcul d'échéance sont tous protégés par le test « bail initialisé », et passer à `MaxValue` afficherait 31/12/9999 dans le sélecteur de date.
- *« Virgule française mal gérée dans les formulaires d'achat »* — déjà traitée par `Replace(',', '.')` ; le vrai défaut était l'espace des milliers.

### Fermeture de l'application (16/09/2026)

Deux mécanismes complémentaires, parce qu'aucun des deux ne suffit seul :

1. **Sauvegarde de fermeture** — `BatimentManager.OnApplicationQuit` réécrit l'état en mémoire via l'écriture atomique. Elle **annule** l'opération si l'emplacement de sauvegarde a changé depuis le chargement (disque débranché, bascule d'entreprise) plutôt que de dupliquer les données ailleurs, et compte les échecs d'écriture. Ce qu'elle protège réellement : une modification validée dont l'écriture disque avait échoué (fichier verrouillé par un antivirus, lecteur réseau absent).

2. **Avertissement « modifications non enregistrées »** — `FermetureGuard` liste les fiches encore en édition et fait annuler la fermeture par `Application.wantsToQuit` (fenêtre fermée en build) ; `QuitButton` passe par le même contrôle, ce qui couvre l'éditeur, où `wantsToQuit` ne se déclenche pas.

Le point important : la sauvegarde de fermeture **ne peut pas** capturer une fiche en cours de saisie. Reverser l'UI dans les données passe par `BatimentPrefab.SaveBatiment()`, qui bascule aussi des boutons, ré-initialise la fiche et appelle `ShowFiche()` — l'exécuter pendant la destruction de l'application manipulerait l'interface en pleine fermeture, et l'appeler sur une fiche jamais ouverte écraserait des données réelles avec des champs vides. D'où l'avertissement plutôt qu'une sauvegarde silencieuse.

L'état « en édition » n'est pas stocké dans un nouveau booléen : `PrefabBatLoc.EnEdition` lit la visibilité du bouton Enregistrer, que `Modify()` affiche et `SaveBatiment()` masque. Impossible de le désynchroniser.

**Restent ouverts**, volontairement non traités parce que le correctif dépasse la revue : photos en chemins absolus (nécessite une migration des JSON existants) · montants en `float` plutôt que `decimal` (l'arrondi est traité, pas le type) · alerte de régularisation non acquittable sur un locataire neuf.

*(Les trois autres — `ReloadFromDisk`, arguments Edge, suppression de photo — ont été traités le 16/09, voir « Reliquat mineur » ci-dessous.)*

### Reliquat mineur — traité et vérifié en exécution (16/09/2026)

**1. `ReloadFromDisk` détruisait les saisies en cours sans prévenir** · `Batiment/BatimentManager.cs:92`

La méthode détruit tous les prefabs de bâtiment puis relit le disque. Sept points d'entrée y mènent : changement d'emplacement (`SaveLocationMenu.Appliquer`, `.RemettreDefaut`, `ReglagePanel` → `SaveIO.ChangeLocation`, `SaveIO.ResetToDefault`) et bascule d'entreprise (`EntreprisePanel` → `Activer`, `Ouvrir`, `Creer`). Une fiche ouverte en édition partait avec son prefab.

Le mécanisme de détection existait déjà — `FermetureGuard.NomsEnEdition()`, écrit pour la fermeture — il n'était simplement appelé nulle part ailleurs. Ajout de `FermetureGuard.ConfirmerPerteSaisies(action, suite)`, câblé sur les sept entrées.

Deux points de conception, tous deux vérifiés :
- **la confirmation précède l'opération**, jamais le rechargement : quand `ReloadFromDisk` est atteint dans `SaveLocationMenu`, les fichiers ont **déjà** été déplacés, et renoncer là laisserait le disque et l'affichage désaccordés. `Appliquer` a donc été scindé en `Appliquer` (garde) + `AppliquerConfirme` (travail) ;
- **la garde est posée dans l'UI, pas dans les services** : envelopper `EntrepriseService.Creer` aurait fait répondre son `bool` avant l'utilisatrice.
- Choix retenu : **prévenir et laisser choisir** (`ConfirmDialog`, bouton « Continuer sans enregistrer »), pas bloquer — bloquer piégerait quelqu'un qui veut justement abandonner ses modifications.

**Vérifié** (5 assertions) : action exécutée directement quand rien n'est en édition (la garde ne gêne pas le cas courant) · repli sans dialogue disponible · action nulle sans exception · texte de fermeture inchangé pour `QuitButton`.

**2. Arguments Edge assemblés à la main** · `Facturation/FacturePdfService.cs`

La ligne de commande était construite par interpolation, avec des guillemets posés à la main autour de chemins issus de noms de bâtiment, de locataire et du dossier choisi par l'utilisatrice. Ce n'était pas exploitable sous Windows (`UseShellExecute = false`, donc pas de shell ; et NTFS interdit le guillemet dans un chemin), mais la sûreté reposait alors sur une garantie du **système de fichiers**, pas du code.

`ProcessStartInfo.ArgumentList` — vérifié disponible dans le Mono d'Unity — reçoit désormais un argument par entrée ; c'est .NET qui assemble et cite.

**Vérifié** : un PDF de 69 Ko réellement produit par Edge dans `…/cipl edge & test 100% d'essai …/facture d'essai #1 (piégée).pdf` — espaces, `&`, `%`, apostrophe, `#`, parenthèses.

**3. Suppression de photo sans annulation** · `Batiment/PhotoService.cs`, `PhotoGalleryController.cs:92`

`File.Delete` immédiat, sans confirmation ni corbeille — alors que toute autre suppression de l'app (achat, travaux, charge, bâtiment, locataire) passe par `ConfirmDialog` **puis** `UndoToast` avec restauration. C'était la seule action définitive au premier clic.

Le fichier est maintenant **déplacé** vers `<racine>/corbeille_photos/<guid>_<nom>` ; `Supprimer` renvoie un jeton (`PhotoSupprimee` : chemin, index, couverture, emplacement en corbeille) que `Restaurer` consomme pour tout remettre en place, **à la position d'origine** dans la liste. `PurgerCorbeille()` (7 jours) est appelée au démarrage à côté du backup — sans elle, les photos supprimées s'accumuleraient dans la sauvegarde et seraient recopiées à chaque migration d'entreprise.

**Vérifié** (15 assertions) sur de vrais fichiers : retrait de la liste, du cache et de la couverture · contenu intact en corbeille puis après aller-retour · position et couverture rétablies · rien laissé en corbeille après restauration (déplacement, pas copie) · purge qui supprime un fichier de 30 jours et conserve celui du jour · photo dont le fichier manque déjà, sans exception.

Un cas traité au passage : si une photo ajoutée entre-temps a repris le nom libéré, la restauration ne l'écrase pas — elle revient sous un nom libre, et c'est ce chemin-là qui est réinscrit.

> **Rectificatif** : le comptage initial de « 20 `catch` silencieux » reposait sur une heuristique trop large. Le dépôt en contient **6** réellement vides, dont 3 seulement masquaient une défaillance ; les 3 autres (`proc.Kill`, suppression de fichiers temporaires) sont du best-effort légitime et ont été laissés tels quels.

## Synthèse

| Gravité | Nombre | Nature |
|---|---|---|
| CRITICAL | 3 | Perte de données irréversible, app qui ne démarre plus |
| HIGH | 4 | Numérotation de facture non conforme, impayés non détectés |
| MEDIUM | 6 | Secret commité, échecs silencieux, risques latents |
| LOW | 3 | Style, code mort, logs résiduels |

**Point rassurant d'emblée** : aucun appel à l'API Pennylane n'existe dans le code. La recherche `UnityWebRequest|POST|customer_invoices|draft` sur les 27 fichiers de `Facturation` ne renvoie rien. **Aucun chemin ne peut émettre une vraie facture aujourd'hui.** La règle « jamais d'émission réelle sans consentement » n'est donc pas menaçable en l'état.

Le risque n'est pas là où on l'attendait : il est dans la **persistance** (on peut perdre définitivement ses réglages) et dans la **numérotation des factures** (doublons, non conforme).

---

## CRITICAL

### C1 — Perte définitive des RIB, entêtes et séquences de facturation

**CONFIRMÉ** · `Facturation/ReglageService.cs:38-41`, `:44-49` · `Save/BackupService.cs:17`

Trois défauts se combinent en une chaîne de destruction :

1. `Save()` (ligne 47) fait un `File.WriteAllText` direct, **non atomique** et **sans `try`**. Une coupure pendant l'écriture laisse un `reglage.json` tronqué.
2. `Load()` (lignes 38-41) avale toute exception avec un `catch` nu et repart d'un `new ReglageData()` **vide**, sans le moindre avertissement.
3. Le premier `Save()` suivant écrase alors le fichier encore récupérable par des réglages vides.

**Aggravant décisif** : `BackupService.RunStartupBackup` ne sauvegarde que `<saveRoot>/batiments` (ligne 17). `reglage.json` n'a **aucune copie de secours**. RIB, entêtes, nom d'entreprise et séquences de facturation sont perdus sans recours.

**Scénario** : coupure de courant pendant l'enregistrement d'un RIB → au redémarrage, l'écran Réglages est vide → le moindre clic sur « Enregistrer » scelle la perte.

**Correctif** : écriture atomique (`.tmp` + `File.Replace` avec fichier `.bak`), distinguer « fichier absent » (normal) de « fichier illisible » (anomalie à signaler sans écraser), et inclure `reglage.json` dans le backup de démarrage.

### C2 — Un seul JSON corrompu empêche l'application de démarrer

**CONFIRMÉ** · `Batiment/BatimentManager.cs:151-164`, `:32-39`

`LoadAll` lit et désérialise chaque fichier **sans `try` par fichier** (lignes 153-154). Un JSON tronqué fait remonter l'exception hors de `LoadAll`, donc hors de `Start()` (ligne 35) — et `menuManager.OpenGeneralMenu()` (ligne 38) **n'est jamais atteint**.

Ce n'est donc pas seulement « les bâtiments suivants ne se chargent pas » : l'app reste bloquée sur un écran vide, sans message. Et la cause la plus probable d'un JSON tronqué est précisément l'écriture non atomique de C3.

**Correctif** : envelopper chaque fichier dans son propre `try/catch`, continuer la boucle, et présenter à la fin la liste des fichiers illisibles plutôt que d'échouer en silence.

### C3 — Écritures non atomiques : l'UI annonce « sauvegardé » alors que rien ne l'est

**CONFIRMÉ** · `Batiment/BatimentManager.cs:112-125`, `Facturation/ReglageService.cs:44-49`

`SaveBatiment` met à jour la liste mémoire (lignes 117-119) **avant** d'écrire (ligne 123), et n'a aucun `try/catch`. Disque plein, fichier verrouillé par OneDrive ou l'antivirus, lecteur réseau déconnecté → l'exception part d'un handler de clic Unity, l'écriture n'a pas eu lieu, mais l'état mémoire et l'affichage indiquent un succès.

**Correctif** : écriture atomique, `try/catch` explicite, et retour visuel d'échec.

---

## HIGH

### H1 — Deux locataires peuvent recevoir le même numéro de facture

**CONFIRMÉ** · `Locataire/Locataire.cs:66`, `Facturation/FactureDepotPanel.cs:313-315`, `:323-325`

Le numéro se compose de `NumeroPrefixe(format, date) + numeroId` où :

- le préfixe est **purement calendaire** — `{Année}/`, `{Année}/{Mois}` ou `{Année}/{Jour}{Mois}` (lignes 323-325), sans aucun identifiant de locataire ;
- `numeroId` est pré-rempli avec `factureSeq`, qui est **par locataire** (`Locataire.cs:66`, le commentaire le dit explicitement).

Deux locataires différents facturés le même mois, tous deux à `factureSeq = 1`, obtiennent **`2026/09001`** l'un comme l'autre. Aucun contrôle d'unicité n'existe nulle part.

En France, la numérotation des factures doit reposer sur une séquence **continue et unique** (art. 242 nonies A du CGI). C'est un sujet de conformité, pas de confort.

Le design est en outre contradictoire : le champ affiche « ID locataire (ex. 001) » — ce qui suggère un identifiant **stable** par locataire — alors qu'il est pré-rempli avec une séquence **incrémentée** à chaque émission (ligne 434).

**Correctif** : trancher entre les deux modèles. Soit une séquence unique globale à l'entreprise, soit préfixe calendaire + identifiant stable de locataire + séquence par locataire. Puis ajouter un contrôle d'unicité au moment de l'émission.

### H2 — Régularisation, dépôt et refacturation : double émission non protégée

**CONFIRMÉ** · `Facturation/FacturationSuivi.cs:189`, `Facturation/FactureLoyerPanel.cs:340`

`EstDejaEmise` existe et gère proprement la ré-émission comme une « correction » (même numéro, aucune séquence consommée). Elle n'est appelée **que** dans `FactureLoyerPanel`.

`FactureRegulPanel`, `FactureDepotPanel` et `FactureRefacPanel` n'ont aucune garde équivalente : un second clic sur « Sauvegarder et envoyer » consomme un nouveau numéro de séquence. Si le nom de fichier PDF est déterministe, la première facture est également écrasée sur le disque tout en restant référencée dans le suivi (*PLAUSIBLE* — non vérifié).

**Correctif** : appliquer le même passage par `EstDejaEmise` dans les trois panneaux.

### H3 — Une facture sans échéance n'est jamais détectée comme impayée

**CONFIRMÉ** · `Facturation/FacturationSuivi.cs:125`, `:137`, `:139`

`EtatDe` fait `hasEch = DateTime.TryParse(f.echeanceISO, out var ech)` (ligne 125). Si l'échéance est vide ou illisible, `hasEch = false`, le test d'impayé (ligne 137) est **sauté**, et la fonction retombe sur `return Etat.Envoye` (ligne 139).

Conséquence : la ligne reste « Envoyé » indéfiniment, ne bascule jamais en Impayé, et disparaît des créances. Le défaut de fond est que `hasEch == false` est traité comme « tout va bien » au lieu de « anomalie à signaler ».

**Correctif** : traiter l'absence d'échéance comme un état distinct et visible, pas comme un équivalent de « envoyé, rien à signaler ».

### H4 — Un bâtiment nouvellement créé n'est jamais écrit sur le disque

**CONFIRMÉ** · `Batiment/BatimentManager.cs:64`

Dans `AddBatiment`, l'appel `SaveBatiment(data.getBatiment())` est **commenté**. Le bâtiment n'existe qu'en mémoire jusqu'à une sauvegarde explicite ultérieure. Créer un bâtiment puis fermer l'app le fait disparaître.

Aucun `OnApplicationQuit` n'existe dans tout `Assets/Script` (*PLAUSIBLE*, relevé en exploration) : il n'y a pas de filet au moment de la fermeture.

---

## MEDIUM

### M1 — Token Mapbox actif commité dans le dépôt

**CONFIRMÉ** · `Assets/Prefab/Maps.prefab:340` · `Assets/Script/Maps/TileLoader.cs:9`

`mapboxAccessToken` est un champ `public`, donc sérialisé dans le prefab avec sa vraie valeur. Il est présent depuis le commit initial `165650a`, donc dans tout l'historique.

Le dépôt étant **privé**, la gravité reste MEDIUM : pas d'urgence de rotation, mais le token est lisible par quiconque y a accès, et le passage en public le publierait rétroactivement via l'historique.

Le projet dispose déjà du bon mécanisme (`ReglageService.ReadSecret`/`WriteSecret` → `<SaveRoot>/pennylane_secrets.dat`, hors repo). Par ailleurs `.gitignore` ne contient **aucune** règle `secret`, `key` ou `.dat`.

### M2 — Vingt blocs `catch` silencieux

**CONFIRMÉ** (comptage) · notamment `Batiment/PhotoService.cs`, `Facturation/FacturePdfService.cs`, `Facturation/ReglageService.cs:112`, `:128`

Sur `ReglageService` (écriture des secrets) et `FacturePdfService` (génération de PDF), une erreur avalée fait croire à une opération réussie. Une clé API illisible renvoie `""` sans diagnostic.

### M3 — Saisie numérique invalide convertie en zéro, sans signal

**CONFIRMÉ** · `Achat/AchatFormPanel.cs:194-200`

`Parse` ignore le booléen de `TryParse` et renvoie `0` en cas d'échec. La virgule française est bien gérée (`Replace(',', '.')` ligne 196), mais une saisie comme « 1 200 » (espace de milliers) échoue → taux ou montant à 0 → mensualité calculée **sans intérêts**, affichée comme valide.

### M4 — Risque latent de culture décimale sur les indices

**MEDIUM, non actif** · `RevisionLoyer/RevisionPanel.cs:330`, `:409` vs `RevisionLoyer/LoyerHistoryService.cs:193`

L'écriture utilise l'interpolation `$"{valeur:F2}"` (culture courante) alors que la relecture impose `CultureInfo.InvariantCulture`. **Vérifié empiriquement** : Unity tourne en culture `en-US` (séparateur `.`), l'aller-retour `125.50` → `125.5` fonctionne. Le défaut **n'est pas actif aujourd'hui**.

Il le deviendrait si la culture courante passait en `fr-FR` : tous les indices seraient relus à `0`, et le tableau de rentabilité indexée retomberait silencieusement sur un loyer plat — c'est-à-dire exactement ce que la fonctionnalité est censée remplacer, sans aucun signe visible.

**Correctif** : forcer `CultureInfo.InvariantCulture` à l'écriture, pour aligner sur la lecture.

### M5 — Migration d'entreprise pouvant écraser les réglages d'une autre

**PLAUSIBLE** · `Save/SaveLocationService.cs:74-79`

`MigrateData` utilise `File.Copy(..., overwrite: true)`. Migrer vers un dossier contenant déjà une entreprise écraserait son `reglage.json` (même nom) sans confirmation. La copie ne portant que sur `*.json`, les photos et le fichier de secrets resteraient en arrière.

Non vérifié dans le détail — à confirmer avant correction.

### M6 — Montants en `float` et absence de garde de signe

**PLAUSIBLE** · `Facturation/FactureRegulPanel.cs:360-361`, `FactureDepotPanel.cs:286`

Relevé en exploration, non vérifié ligne à ligne : soldes de régularisation et compléments de dépôt calculés sans garde de signe (facture à TTC négatif possible si les provisions dépassent les charges), TVA et TTC calculés indépendamment en `float` (HT + TVA ≠ TTC après arrondi à l'affichage).

`float` offre ~7 chiffres significatifs : au-delà de ~100 000 €, la précision au centime n'est plus garantie. Pour de la facturation, `decimal` est le type approprié.

---

## LOW

### L1 — `GetComponent<T>() ?? AddComponent<T>()` : 41 occurrences — **ÉCARTÉ comme bug**

**ÉCARTÉ** · 20 fichiers

Ce pattern a été testé empiriquement dans l'éditeur plutôt que jugé sur la théorie :

| Cas | `GetComponent` renvoie | Le `??` se déclenche |
|---|---|---|
| Objet frais, composant absent | vrai `null` | **oui — fonctionne** |
| Composant détruit, `GetComponent` refait | vrai `null` | **oui — fonctionne** |
| Référence morte conservée | faux-null | **non — renvoie l'objet mort** |

`GetComponent<T>()` renvoie toujours un **vrai** `null`, jamais un faux-null. Le piège du faux-null ne concerne que les **références stockées** vers un objet détruit — ce que ce pattern ne fait pas.

Les 41 sites sont donc **corrects**. Ils restent signalés par les analyseurs Unity UNT0023/UNT0008, d'où le classement LOW pour cohérence de style, mais **aucune réécriture de masse n'est justifiée**.

*Note d'honnêteté : une revue précédente avait classé ce pattern en HIGH et invoqué un `MissingComponentException` sur `TriDropdown` comme preuve. C'était erroné. La cause réelle de ce crash reste non identifiée.*

### L2 — Code mort contenant l'ancienne logique fautive

**PLAUSIBLE** · `RevisionLoyer/LoyerRevisionController.cs` (439 lignes)

D'après l'exploration, son GUID n'est référencé par aucune scène ni prefab. Le fichier contient l'ancienne logique de révision, celle qui choisissait l'indice par le trimestre de départ en ignorant le trimestre demandé. Le laisser en place expose à ce qu'il serve un jour de modèle. À supprimer après vérification du non-référencement.

### L3 — 42 `Debug.Log` résiduels

Aucun `TODO`/`FIXME` dans le projet, aucun `FindObjectOfType` dans un `Update()` : de ce côté, le code est sain.

---

## Écartés après vérification

Ces points ont été examinés et ne sont **pas** des défauts, mais méritent d'être listés car on pourrait légitimement les croire problématiques :

- **Virgule française dans les formulaires d'achat** — gérée par `Replace(',', '.')` (`AchatFormPanel.cs:196`).
- **Aller-retour des indices INSEE** — fonctionne, Unity étant en culture `en-US` (voir M4 pour le risque latent).
- **Collision d'identifiants** — tous les modèles utilisent `Guid.NewGuid()`.
- **Perte de champs par `JsonUtility`** — aucun `Dictionary` sérialisé ni polymorphisme dans les modèles.
- **`RentabiliteCalculator.Mensualite`** — les cas `durée <= 0` et `taux <= 0` sont correctement traités, pas de division par zéro.
- **Purge des backups** — `Directory.Delete(recursive)` ne s'applique qu'aux dossiers dont le suffixe correspond exactement au format de date attendu.

---

## Par où commencer

Classé par rapport valeur/risque, pas par gravité brute :

1. **C2** — un `try/catch` par fichier dans `LoadAll`. Quelques lignes, supprime le scénario « l'app ne démarre plus ».
2. **C1 + C3** — écriture atomique mutualisée (`.tmp` + `File.Replace` + `.bak`) pour `SaveBatiment` et `ReglageService.Save`, plus `reglage.json` dans le backup. Un seul utilitaire couvre les trois.
3. **H4** — décommenter la sauvegarde dans `AddBatiment`, une ligne.
4. **H2** — étendre `EstDejaEmise` aux trois panneaux, le mécanisme existe déjà.
5. **H1** — décision de conception à prendre avec toi avant tout code : séquence globale, ou identifiant stable de locataire.
6. **M1** — déplacer le token Mapbox vers le fichier de secrets et compléter `.gitignore`.

**H1 est le seul point qui demande un arbitrage métier de ta part** ; tous les autres ont un correctif technique évident.
