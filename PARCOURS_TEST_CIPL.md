# Parcours de test — révisions, paliers, dépôt, parcours locataire (mis à jour le 30/09/2026)

À suivre dans l'ordre, en Play. Chaque étape donne **l'action**, puis **ce qui doit se passer** (→). Cochez au fur et à mesure. Si une étape ne donne pas le résultat attendu, notez son numéro et ce que vous voyez, et arrêtez-vous là : les étapes suivantes en dépendent souvent.

Les montants attendus supposent que **nous sommes le 30/09/2026**.

---

## 0. Préparation

- [ ] **0.1** Lancez le Play dans l'entreprise **DemoCIPL**.
- [ ] **0.2** Supprimez les bâtiments de test des essais précédents (ex. « nouveau bati »).
  → Après chaque suppression, l'application affiche le **bâtiment voisin**, jamais un écran vide.
- [ ] **0.3** Créez un bâtiment **« TEST Parcours »** (bouton « Nouveau bâtiment » de l'accueil).
  → **Le nom, les surfaces et le cadastre sont modifiables** (ils ne l'étaient plus).
  
  Surface 1 000 m², n'importe quelle adresse, puis Sauvegarder.

---

## A. Parcours de création — locataire « Test Paliers »

- [ ] **A1** Dans TEST Parcours, ajoutez un locataire.
  → La fiche s'ouvre en modification :
  - le **nom est modifiable** ;
  - un bandeau gris-bleu « Nouveau locataire — à remplir dans l'ordre » affiche **1 Général** en ambre, puis 2 Bail, 3 Loyer et 4 Dépôt de garantie en gris, sans bouton « Continuer » ;
  - la carte Dépôt affiche « **Initialiser** » ;
  - le **suivi de facturation est vide**, avec le message « Le suivi de facturation apparaîtra une fois le locataire complet… », et aucune pastille sur les années ;
  - la section Bail propose, sous les dates, une case « **Départ du locataire** » (décochée).
- [ ] **A2** Sans rien remplir, cliquez sur **Sauvegarder**.
  → Message : « Étape 1 — Général : saisissez au moins le nom du locataire, puis enregistrez. » Rien n'est enregistré.
- [ ] **A3** Toujours sans enregistrer, cliquez sur **Initialiser** (carte Loyer), puis sur **Initialiser** (carte Dépôt), puis sur **Facturer le loyer**.
  → Les trois sont refusés et rappellent l'étape 1. Le troisième message commence par « Facturation possible une fois le locataire complet. »
- [ ] **A4** Tapez le nom **Test Paliers**.
  → **Dès la première lettre**, le bandeau passe à ✓ Général et **2 Bail** en ambre.
  
  Sauvegardez sans dates de bail.
  → Message : « Étape 2 — Bail : saisissez les dates de début et de fin du bail, puis enregistrez. »
- [ ] **A5** Bail commercial, début **01/01/2026**, durée **9**. La fin doit se remplir seule avec **31/12/2034**.
  → Avant même d'enregistrer, le bandeau affiche ✓ Général, ✓ Bail et **3 Loyer**, avec la consigne « Cliquez sur « Sauvegarder », puis : Étape 3 — Loyer… ».
  
  Cliquez sur Sauvegarder.
  → L'écran **« Initialisation du loyer »** s'ouvre tout seul, avec un bouton **Initialiser**.

## B. Loyer à paliers avec franchise (suite de Test Paliers)

- [ ] **B1** Dans l'écran d'initialisation :
  - vérifiez qu'il n'y a **pas** de case « Départ du locataire » (elle est dans la section Bail) ;
  - loyer de départ **12000**, périodicité **trimestriel**, pas de provision ;
  - **Révision du loyer** cochée, type **Par paliers** ;
  - **Franchise de loyer** cochée, date **01/04/2026** ;
  - ouvrez le menu **« Bail repris »** sans rien choisir. Il doit proposer exactement : Aucune, 4e trimestre 2026, 3e trimestre 2026, 2e trimestre 2026, 1er trimestre 2026. Donc pas de mois, rien de futur, rien avant le bail. Laissez « Aucune ».
  
  Cliquez sur **Initialiser**.
  → L'écran se ferme et **« Paliers de loyer · Test Paliers »** s'ouvre, avec :
  - « Loyer de départ : 12 000,00 € / an HT » ;
  - « Bail : du 01/01/2026 au 31/12/2034 (9 ans) » ;
  - « Franchise : 3 mois — facturation à partir du 01/04/2026 — comprise dans le premier palier, sans loyer. » ;
  - le formulaire **Palier 1** ouvert, en trois colonnes alignées :
    - « Loyer annuel HT » : 12000 ;
    - « **Du (début du bail)** » : 01 / 01 / 2026, affiché comme les autres dates de l'app — le 1er palier contient la franchise ;
    - « **Au (fin du palier)** » : une liste, positionnée sur « 31/12/2034 · fin du bail (9 ans, dont 8 ans 9 mois payés) » ;
  - les boutons Annuler / Ajouter à droite, de taille normale ;
  - **Valider** grisé.
- [ ] **B2** Ouvrez la liste « Au ».
  → Elle **défile** et ne propose que des fins de trimestre, chacune avec la durée du palier et la part payée :
  - la première est « 30/06/2026 · 6 mois, dont 3 mois payés ». Le 31/03/2026 n'est pas proposé : ce palier serait entièrement en franchise ;
  - aucune date en milieu de trimestre (pas de 15/02/2027, pas de 31/08) ;
  - la dernière est « 31/12/2034 · fin du bail… ».
- [ ] **B3** Choisissez **« 31/03/2027 · 1 an 3 mois, dont 1 an payé »**, puis Ajouter.
  → Le tableau affiche « Palier 1 ● · 12 000,00 € · 01/01/2026 · 31/03/2027 · 1 an 3 mois, dont 1 an payé ». La ligne est **surlignée en vert**, car c'est le palier en cours. Message : « Les paliers s'arrêtent le 31/03/2027 : le bail court jusqu'au 31/12/2034. » Valider reste grisé.
- [ ] **B4** Cliquez sur **+ Ajouter un palier**.
  → « Palier 2 », « Du » 01 / 04 / 2027 (calculé), « Au » sur la fin du bail.
  
  Loyer **13000**, puis Ajouter.
  → Message vert « Les paliers couvrent tout le bail. » **Valider devient cliquable.**
- [ ] **B5** Cliquez sur **Modifier** sur le palier 1, choisissez Au **30/09/2027**, puis Enregistrer.
  → Le palier 2 commence maintenant le **01/10/2027**.
- [ ] **B6** Cliquez sur **Modifier** sur le palier 1 et ouvrez la liste « Au ».
  → Elle s'arrête **avant la fin du palier 2** : on ne peut pas écraser le palier suivant. Cliquez sur Annuler.
- [ ] **B7** Cliquez sur **Valider**.
  → Message « Paliers enregistrés — loyer actuel : 12 000,00 € / an ». Le bandeau passe à **4 Dépôt de garantie**, et la fenêtre **« Initialisation du dépôt de garantie »** s'ouvre toute seule. Le suivi est toujours vide.

## C. Dépôt initial « pas encore demandé »

- [ ] **C1** Dans la fenêtre du dépôt :
  - la date de la prochaine révision est pré-remplie au **01/01/2027** ;
  - la case TVA est cochée ;
  - le loyer par période affiche « 3600.00 € / période TTC ».
  
  Saisissez 1 période.
  → « Dépôt de garantie : 3600.00 € ».
  
  Choisissez **« Pas encore demandé »**, puis cliquez sur **Initialiser**.
  → Message « « Test Paliers » est prêt : général, bail, loyer et dépôt sont renseignés. » **Le bandeau disparaît.** La carte Dépôt affiche 3 600,00 € et un bouton « Réviser ».
- [ ] **C2** Le suivi de facturation 2026 **se remplit** :
  - **Loyer 1er trimestre 2026** grisé, avec la pastille « **Franchise** » et aucune action ;
  - les 2e, 3e et 4e trimestres normaux (« À faire » : rien n'a été facturé) ;
  - une ligne **« Dépôt de garantie » 3 600,00 € « À faire »** ;
  - si une provision pour charges est réglée : aucune régularisation avant 2026, et **aucune pastille sur 2023, 2024 et 2025** ; pas d'alerte « Régularisation des charges » pour les charges d'avant le bail.
- [ ] **C3** Cliquez sur **Générer** sur la ligne « Dépôt de garantie ».
  → Le panneau s'appelle « **Dépôt de garantie** · Test Paliers ». Il affiche « Dépôt déjà versé : **0,00 €** », **1** période, 3 600,00 €.
  
  Le bloc « Explication imprimée sur le document » n'apparaît pas : il ne sert qu'à la révision.
  
  Cliquez sur **Générer l'aperçu**.
  → Le document a le **même format qu'une facture de refacturation** :
  - « FACTURE : n° », titre **« Dépôt de garantie »** ;
  - un tableau avec la ligne « Dépôt de garantie — 1 terme de loyer TTC · 3 600,00 € » ;
  - Total H.T. 3 600,00 €, « TVA — dépôt de garantie non soumis · 0,00 € », Total T.T.C. 3 600,00 € ;
  - puis la phrase de règlement et le RIB.
  
  **Fermez sans émettre.**

## D. Montant du loyer sur la facture (paliers)

- [ ] **D1** Carte Loyer :
  - loyer annuel HT **12 000,00 €** ;
  - récapitulatif « **Prochain palier** : 01/10/2027 (13 000 €/an) » et « **Facturation à partir du** : 01/04/2026 » ;
  - boutons « **Modalités** » et « **Paliers** ».
- [ ] **D2** Cliquez sur **Facturer le loyer** et choisissez la période et l'année à chaque fois :

  | Période | Loyer HT attendu | Pourquoi |
  |---|---|---|
  | 3e trimestre 2026 | **3000.00** | palier 1 |
  | 4e trimestre 2027 | **3250.00** | palier 2, à partir du 01/10/2027 |
  | 3e trimestre 2027 | **3000.00** | encore palier 1 |
  | 1er trimestre 2026 | **0.00** | franchise |

  Fermez sans émettre.

## E. Bail sans révision, puis avenant — locataire « Test Avenant »

- [ ] **E1** Ajoutez un locataire **Test Avenant** : bail 01/01/2026, 9 ans, puis Sauvegarder.
  → L'écran du loyer s'ouvre. Saisissez 12000, trimestriel, **décochez « Révision du loyer »**, puis Initialiser.
  → La fenêtre du dépôt s'ouvre. Saisissez 3 périodes, « Demandé et reçu », puis Initialiser.
  → « est prêt ».
  
  Vérifiez ensuite :
  - la carte Loyer n'a **pas** de bouton « Réviser », et le récapitulatif affiche « Révision : Aucune » ;
  - sur le résumé du bâtiment, la ligne de Test Avenant affiche « Sans révision ».
- [ ] **E2** Cliquez sur **Modalités**.
  → Le bouton affiche « **Modifier** ».
  
  Changez le loyer de départ en **15000**.
  → Le bouton devient « **Réinitialiser** » et une case « Avenant : le nouveau loyer s'applique en cours de bail » apparaît.
  
  Cochez-la, date **15/02/2027**, puis Réinitialiser.
  → Message « Loyer initialisé, sans révision. »
- [ ] **E3** Facturer le loyer :

  | Période | Loyer HT attendu | Pourquoi |
  |---|---|---|
  | 4e trimestre 2026 | **3000.00** | ancien loyer |
  | 1er trimestre 2027 | **3375.00** | 45 jours à 12 000, puis 45 jours à 15 000 |
  | 2e trimestre 2027 | **3750.00** | nouveau loyer |

  *Note : la carte Loyer affiche déjà 15 000 € ; c'est le loyer des nouvelles conditions.*

## F. Départ du locataire et tacite prolongation (Test Avenant)

- [ ] **F1** Cliquez sur **Modifier** (fiche). Dans la section **Bail**, sous les dates, cochez **Départ du locataire** et saisissez le dernier jour de location **14/08/2027**. Sauvegarder.
  → La section Bail affiche « Dernier jour de location 14/08/2027 », et la carte Loyer « Départ du locataire : 14/08/2027 ».
- [ ] **F2** Facturer le loyer :

  | Période | Loyer HT attendu | Pourquoi |
  |---|---|---|
  | 3e trimestre 2027 | **1834.24** | 45 jours sur 92 |
  | 4e trimestre 2027 | **0.00** | après le départ |

- [ ] **F3** Suivi, année 2027 → la ligne « Loyer 4e trimestre 2027 » est grisée, pastille « **Hors bail** ».
- [ ] **F4** Modifier (fiche) → décochez le départ dans la section Bail, puis Sauvegarder.
  → La ligne du départ disparaît de la section Bail.
  
  Facturez ensuite le 1er trimestre **2035** (après la fin du bail).
  → **3750.00** : sans départ, le loyer continue (tacite prolongation).

## G. Révision par indice — locataire « Test Indice » *(connexion Internet nécessaire)*

- [ ] **G1** Ajoutez un locataire **Test Indice** : bail 01/01/2026, 9 ans, puis Sauvegarder. Dans l'écran du loyer : 12000, trimestriel, révision cochée, type **Par indice**, puis Initialiser.
  → Le même panneau devient « **Initialisation de l'indice** », avec :
  - les puces ILC / IRL / ILAT ;
  - le loyer de départ grisé ;
  - le trimestre de référence et la date de révision ;
  - un bouton **Initialiser** (plus de bascule Initialiser / Réviser).
- [ ] **G2** ILC, trimestre de référence **2025-T2**, date de révision **01/01/2027**, puis Initialiser.
  → Message « Indice initialisé — indice 2025-T2 : … ». Le panneau se ferme, puis la fenêtre du dépôt s'ouvre.
- [ ] **G3** Dépôt : 3 périodes, **« Demandé, pas encore reçu »**, puis Initialiser.
  → Le suivi affiche une ligne « Dépôt de garantie » **10 800,00 €** en **Impayé** (échéance au 01/01/2026, dépassée de plus de 15 jours). Sur l'accueil, la colonne **Créances** la montre aussi.
- [ ] **G4** Carte Loyer → boutons « Modalités » et « **Réviser** », récapitulatif « Prochaine révision : 01/01/2027 ».

## H. Réinitialisation abandonnée (Test Indice)

- [ ] **H1** Modalités → type **Par paliers** → « Réinitialiser ». L'écran des paliers s'ouvre ; cliquez sur **Fermer sans valider**.
  → Le bandeau réapparaît sur « **3 Loyer** », avec un bouton « Continuer ». La carte Loyer affiche « Initialiser ». « Facturer le loyer » est refusé (étape 3). Le suivi ne montre plus les loyers à venir, mais **garde la ligne du dépôt** (déjà demandée).
- [ ] **H2** Cliquez sur **Continuer**.
  → L'écran d'initialisation du loyer s'ouvre. Fermez-le.

## I. Les vrais locataires ne sont pas touchés

- [ ] **I1** Ouvrez **Sephora** (rivoli).
  → Pas de bandeau ; boutons « Modalités » et « Réviser » ; suivi complet.
  
  Cliquez sur Modalités.
  → Le bouton affiche « **Modifier** », il n'y a pas de case Avenant, et « Bail repris » propose des périodes cohérentes avec sa périodicité. Fermez **sans rien changer**.
  
  « Facturer le loyer » s'ouvre normalement ; fermez sans émettre.
- [ ] **I2** Ouvrez **TEST Pennylane — Volteo** (Le Hangar).
  → Bandeau « 3 Loyer », facturation refusée, et le suivi ne montre que ses **2 factures déjà émises** (attendu : ce locataire de test n'a jamais eu de loyer initialisé).
- [ ] **I3** Accueil → « À traiter » affiche toujours les révisions et échéances des vrais locataires.

## J. Nettoyage

- [ ] **J1** Supprimez **un locataire** de TEST Parcours.
  → Retour au **résumé du bâtiment**, avec le locataire retiré de la liste.
  
  Cliquez sur « Annuler » dans le message.
  → Retour sur la **fiche du locataire restauré**. Supprimez-le à nouveau.
- [ ] **J2** Supprimez le bâtiment **TEST Parcours**.
  → L'application affiche le **bâtiment voisin** (celui de l'onglet précédent, sinon le suivant), jamais un écran vide. S'il n'y avait plus aucun bâtiment, elle reviendrait à l'**accueil**.

---

**Résultat** : tout est coché ? Le module est validé en Play. Sinon, donnez-moi le numéro de l'étape et ce que vous avez vu (une capture aide beaucoup).
