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
  - **Franchise de loyer (les provisions restent dues)** cochée, date **01/04/2026** ;
  - ouvrez le menu **« Bail repris »** sans rien choisir. Il doit proposer exactement : Aucune, 4e trimestre 2026, 3e trimestre 2026, 2e trimestre 2026, 1er trimestre 2026. Donc pas de mois, rien de futur, rien avant le bail. Laissez « Aucune ».
  
  Cliquez sur **Initialiser**.
  → L'écran se ferme et **« Paliers de loyer · Test Paliers »** s'ouvre, avec :
  - « Loyer de départ : 12 000,00 € / an HT » ;
  - « Bail : du 01/01/2026 au 31/12/2034 (9 ans) » ;
  - « Franchise : 3 mois — loyer facturé à partir du 01/04/2026 — comprise dans le premier palier (les provisions restent dues). » ;
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
  - **Loyer 1er trimestre 2026** grisé, avec la pastille « **Franchise** » et aucune action (pas de provision : rien à facturer — voir D3) ;
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
  - récapitulatif « **Prochain palier** : 01/10/2027 (13 000 €/an) » et « **Loyer facturé à partir du** : 01/04/2026 » ;
  - boutons « **Modalités** » et « **Paliers** ».
- [ ] **D2** Cliquez sur **Facturer le loyer** et choisissez la période et l'année à chaque fois :

  | Période | Loyer HT attendu | Pourquoi |
  |---|---|---|
  | 3e trimestre 2026 | **3000.00** | palier 1 |
  | 4e trimestre 2027 | **3250.00** | palier 2, à partir du 01/10/2027 |
  | 3e trimestre 2027 | **3000.00** | encore palier 1 |
  | 1er trimestre 2026 | **0.00** | franchise |

  Fermez sans émettre.
- [ ] **D3** Franchise et provisions : la franchise porte sur le loyer, pas sur les provisions.
  Cliquez sur **Modalités**, cochez la provision pour charges, saisissez **300**, puis **Modifier**.
  → Dans le suivi 2026, le **1er trimestre 2026 n'est plus grisé** : il est « À faire ».
  
  Facturer le loyer, 1er trimestre 2026 → loyer **0.00**, provision **300.00**. **Générer l'aperçu** : la première ligne du tableau est « **Loyer — franchise** · 0,00 € », puis la provision. Fermez sans émettre.
  
  Vous pouvez ensuite retirer la provision (Modalités → décocher → Modifier) : la ligne redevient grisée « Franchise ».

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

## J. Charge refacturée et régularisation (Test Avenant)

*À faire en mode d'envoi Pennylane : en mode Email, une refacturation n'est enregistrée qu'une fois le mail parti.*

- [ ] **J1** Test Avenant → **Modalités** → cochez la provision pour charges, **250** par trimestre (soit 1 000 € sur l'année) → Modifier.
- [ ] **J2** Bâtiment TEST Parcours → onglet **Charges** → ajoutez deux charges datées de **2026**, qui concernent **seulement Test Avenant** :
  - « Entretien » **1 200 €** ;
  - « Eau » **800 €**.
- [ ] **J3** Fiche Test Avenant → **Refacturation d'une charge**.
  → La carte « Charges à refacturer » liste les charges avec une case et un montant HT chacune ; aucune n'est cochée.
  
  Cochez **« Eau »** (800,00).
  → Une case « **Déduite des provisions** » apparaît (le locataire a une provision). **Cochez-la** → **Sauvegarder et envoyer**.
  → En rouvrant la refacturation, l'eau n'est plus proposée.
- [ ] **J4** **Régularisation des charges**, année **2026** :
  - charges : « Entretien » 1 200,00 €, puis « Eau · déjà refacturée » 800,00 € en gris ;
  - Total des charges **2 000,00 €**, Provisions déjà versées **1 000,00 €**, « Charges déjà refacturées (réglées à part) » **800,00 €** ;
  - **Solde HT 200,00 €** (1 200 − 1 000 : l'eau, déjà payée, ne change rien).
  
  **Générer facture** : la ligne « Charges déjà refacturées » apparaît sous les provisions, en page 1 et en page 2.
  
  Fermez sans émettre.
- [ ] **J5** Contre-épreuve : ajoutez une charge « Nettoyage » **300 €** (2026, Test Avenant), refacturez-la **sans cocher** « Déduite des provisions ».
  → Dans la régularisation 2026, le Nettoyage **n'apparaît pas** : il reste hors des provisions. Le solde reste **200,00 €**.
- [ ] **J6** Plusieurs charges sur une facture : ajoutez « Jardin » **200 €** et « Ménage » **100 €** (2026, Test Avenant). Refacturation → cochez **les deux** → **Générer facture**.
  → Le tableau a **deux lignes** (Jardin 200,00 · Ménage 100,00), Total HT 300,00, TTC 360,00 ; titre « Refacturation : Jardin, Ménage ».
  
  **Sauvegarder et envoyer**.
  → Le suivi 2026 montre **deux lignes** « Refacturation : Jardin » (240,00) et « Refacturation : Ménage » (120,00), avec le **même numéro**. Passez l'une en « Payé » → **les deux** passent « Payé ».
- [ ] **J7** Rouvrez la refacturation depuis la ligne « Jardin » du suivi.
  → Jardin **et** Ménage sont cochés (c'est la même facture). Décochez Ménage → Sauvegarder → refus : « Cette facture couvre aussi : Refacturation : Ménage… ». Fermez.
- [ ] **J8** Avoir : ajoutez une charge « Avoir eau » à **-500** (2026, Test Avenant). Rouvrez-la dans l'onglet Charges : le coût affiche bien **-500** (et non vide).
  
  Refacturation → cochez « Avoir eau » (-500,00).
  → Total TTC **-600,00 €  ⚠ avoir**, phrase de règlement « SOMME QUI VOUS SERA REMBOURSÉE », et un menu « **Avoir sur la facture n°** » apparaît : il liste les factures déjà émises de Test Avenant (Jardin + Ménage n'y figure qu'une fois). Choisissez la refacturation de l'eau (J3).
  
  **Générer facture**.
  → Titre **« AVOIR : n° »**, juste dessous « **Avoir sur la facture n° …** » (celle choisie), « Avoir sur refacturation : Avoir eau », montants négatifs.
  
  Sauvegarder.
  → Ligne « Avoir : Avoir eau » (-600,00) dans le suivi ; elle restera « Envoyé » et ne passera **jamais « Impayé »** (rien à réclamer). Marquez-la « Payé » une fois le remboursement fait.

## K. Nettoyage

- [ ] **K1** Supprimez **un locataire** de TEST Parcours.
  → Retour au **résumé du bâtiment**, avec le locataire retiré de la liste.
  
  Cliquez sur « Annuler » dans le message.
  → Retour sur la **fiche du locataire restauré**. Supprimez-le à nouveau.
- [ ] **K2** Supprimez le bâtiment **TEST Parcours**.
  → L'application affiche le **bâtiment voisin** (celui de l'onglet précédent, sinon le suivant), jamais un écran vide. S'il n'y avait plus aucun bâtiment, elle reviendrait à l'**accueil**.

## L. Raccourci : bâtiment « TEST Nouveautés » déjà préparé (DemoCIPL)

Préparé le 01/10. Ce bâtiment remplace la saisie des sections A à J : on va directement aux vérifications.
- **6 locataires** (A à F) et **7 charges 2025**, toutes réservées à F.
- F a déjà **4 factures émises**, avec leurs PDF :
  - 2026/08001 « Électricité » (360 €, échue : Impayé) ;
  - 2026/08002 « Avoir électricité » (-180 €, sur la 08001) ;
  - 2026/09003 « Eau », **déduite des provisions** (960 €) ;
  - 2026/09004 « Jardin + Ménage », sur une même facture (240 + 120 €).

**Règle unique** : **n'émettez rien**. Tout se vérifie à l'écran et avec **« Générer facture »** (l'aperçu), en gardant le mode d'envoi Email. Ces locataires n'ont pas d'email : un « Sauvegarder et envoyer » serait refusé de toute façon. Fermez toujours « Facturer le loyer » sans émettre.

- [ ] **L1 · A · Paliers + franchise**
  - carte Loyer : boutons « Modalités » et **« Paliers »**, « Prochain palier : 01/10/2027 (13 000 €/an) », « **Loyer facturé à partir du** : 01/04/2026 » ;
  - Paliers : deux lignes, la première « 01/01/2026 → 30/09/2027 · 1 an 9 mois, **dont 1 an 6 mois payés** » ;
  - suivi 2026 : le 1er trimestre est **grisé « Franchise »** (pas de provision : rien à facturer) ;
  - Facturer le loyer : 2e trimestre 2026 = **3000.00**, 4e trimestre 2027 = **3250.00**.
- [ ] **L2 · B · Franchise + provisions** (franchise jusqu'au 30/04/2026, provision 300 par trimestre)
  - suivi 2026 : le 1er trimestre **n'est pas grisé** (les provisions restent dues) ;
  - Facturer le loyer, 1er trimestre 2026 : loyer **0.00**, provision **300.00**. Générer l'aperçu : la ligne s'intitule « **Loyer — franchise** · 0,00 € » ;
  - 2e trimestre 2026 : loyer **2010.99**, au jour : 61 jours facturés sur 91 (mai-juin). Provision **300.00**.
- [ ] **L3 · C · Avenant + départ** (12 000 € puis 15 000 € à partir du 15/02/2027, départ le 14/08/2027)
  - section Bail : « Départ du locataire » affiché, dernier jour **14/08/2027** ;
  - Facturer le loyer : 4e trimestre 2026 = **3000.00**, 1er trimestre 2027 = **3375.00**, 3e trimestre 2027 = **1834.24** ;
  - suivi 2027 : 4e trimestre **« Hors bail »**.
- [ ] **L4 · D · Indice à initialiser** *(Internet)*
  - bandeau « **3 Loyer** », facturation refusée ;
  - carte Loyer → **Initialiser** → type « Par indice » → Initialiser → « Initialisation de l'indice » : ILC, **2025-T2**, révision **01/01/2027** → Initialiser ;
  - la fenêtre du dépôt s'ouvre : **3** périodes, « **Demandé, pas encore reçu** » → ligne « Dépôt de garantie » **10 800,00 €** en **Impayé**, aussi dans les Créances de l'accueil.
- [ ] **L5 · E · Dépôt à initialiser**
  - bandeau « **4 Dépôt de garantie** » ;
  - carte Dépôt → Initialiser : **1** période, « **Pas encore demandé** » → ligne « Dépôt de garantie » **2 700,00 €** « À faire » ;
  - Générer → aperçu au **format refacturation** (« TVA — dépôt de garantie non soumis »). Fermez sans émettre.
- [ ] **L6 · F · Suivi des refacturations déjà émises** (suivi, année **2026**)
  - « Refacturation : Électricité… » 360,00 en **Impayé** rouge, avec un bouton « Rappel » : facture échue depuis plus de 15 jours ;
  - « **Avoir : Avoir électricité 2025** » -180,00, également échu, mais **« Envoyé »**, sans bouton « Rappel » : un avoir ne passe jamais « Impayé » ;
  - **une seule ligne** pour la facture 2026/09004 : « **Refacturation : Jardin 2025 et Ménage 2025** » **360,00**. Avec trop de charges pour la colonne, ce serait « Refacturation : 3 charges » ;
  - bouton « PDF » sur chaque ligne :
    - 08002 s'intitule « **AVOIR : 2026/08002** », avec « Avoir sur la facture n° 2026/08001 » ;
    - 09004 a deux lignes, Jardin et Ménage.
- [ ] **L7 · Correction d'une facture à plusieurs charges**
  - « Refaire » sur la ligne « Jardin 2025 et Ménage 2025 » → l'écran s'ouvre avec Jardin **et** Ménage cochés (montants 200,00 et 100,00) ;
  - décochez Ménage → « Sauvegarder et envoyer » → **refusé** : « Cette facture couvre aussi 1 autre(s) charge(s) : garde-les toutes cochées pour la corriger. » Fermez.
- [ ] **L8 · Nouvelle refacturation et avoir** (Refacturation d'une charge, sans rien émettre)
  - la liste ne propose plus que les charges **non refacturées** : Entretien espaces verts 2025 et Avoir eau 2025 (**-500,00**). Aucune n'est cochée ;
  - cochez **Entretien** → la case « **Déduite des provisions** » apparaît (F a une provision) ;
  - cochez aussi **Avoir eau 2025** → **Générer facture** : deux lignes (1 200,00 et **-500,00**, la ligne négative est bien imprimée), Total HT 700,00. C'est une facture : titre « FACTURE » ;
  - décochez Entretien (seul l'avoir reste) → Total TTC **-600,00 €  ⚠ avoir**, phrase « **SOMME QUI VOUS SERA REMBOURSÉE** » ;
  - le menu « **Avoir sur la facture n°** » apparaît. Il propose 09004 (Jardin +1, une seule fois), 09003 (Eau) et 08001 (Électricité), mais **pas l'avoir 08002**. Choisissez 09003 ;
  - **Générer facture** : « **AVOIR : n°** », juste dessous « **Avoir sur la facture n° 2026/09003** », « Avoir sur refacturation : Avoir eau 2025 », montants négatifs. Fermez sans émettre.
  - onglet Charges du bâtiment : rouvrez « Avoir eau 2025 », le coût affiche **-500** (et non vide). Fermez sans enregistrer.
- [ ] **L9 · Régul 2025 qui rembourse le locataire** (Régularisation des charges, année **2025**)
  - charges : « Entretien espaces verts 2025 » 1 200,00, « Avoir eau 2025 » -500,00, puis « **Eau 2025 · déjà refacturée** » 800,00 en gris. Électricité, Jardin et Ménage, refacturés sans la case « Déduite », n'apparaissent pas ;
  - Total des charges **1 500,00**, Provisions déjà versées **2 000,00** (500 × 4), « Charges déjà refacturées (réglées à part) » **800,00**, Solde HT **-1 300,00  ⚠ trop-perçu (avoir)** ;
  - le menu « Avoir sur la facture n° » apparaît : choisissez 08001 ;
  - **Générer facture** : « **AVOIR : n°** », « Avoir sur la facture n° 2026/08001 », « Montant à vous rembourser -1 300,00 », « SOMME QUI VOUS SERA REMBOURSÉE ». La page 2 liste l'eau « (déjà refacturée) » et la ligne « Charges déjà refacturées » sous les provisions. Fermez sans émettre.
- [ ] **L11 · G · Loyer selon le CA** *(Internet)* — bail depuis le 01/01/2025, loyer de départ 12 000 €, 8 % du CA HT entre 10 000 et 20 000 €, CA 2025 déjà déclaré : 150 000 €
  - bandeau « **3 Loyer** ». Carte Loyer → **Initialiser** : « Révision du loyer » cochée, type « **Selon le chiffre d'affaires** » (3e choix du menu, après indice et paliers) → **Initialiser** ;
  - l'écran « **Initialisation du loyer selon le CA** » s'ouvre. Sous « Loyer de départ », au même format : **Loyer minimum 10000 €**, **Loyer maximum 20000 €**, **Part du chiffre d'affaires HT 8 %**. Puis type d'indice, trimestre de référence et date de révision ;
  - mettez le maximum à **5000** → Initialiser → **refusé** (« … un maximum au moins égal au minimum »). Remettez **20000** ;
  - ILC, trimestre de référence **2024-T4**, date de révision **01/01/2026** → **Initialiser**. Retour à la fiche ; le bandeau disparaît (le dépôt est déjà fait) ;
  - carte Loyer : « **Loyer selon le CA : 8 % · min 10 000 € · max 20 000 €** », « Prochaine révision : 01/01/2026 » en orange (révision due), bouton **Réviser** ;
  - **Réviser** → « **Révision du loyer selon le CA** » :
    - loyer minimum et loyer maximum **grisés** (bornes en vigueur, révisées par l'indice) ; la **part du CA reste modifiable** (blanche) : la changer, par exemple à 10, fait calculer avec 10 % et l'enregistre pour les révisions suivantes ;
    - dans « Cette révision », « **Dernière révision** » grisé et **vide** (jamais révisé) à côté du « Trimestre de révision » (2025-T4) ; après une révision, il affichera son trimestre et son indice (ex. « 2025-T4 · 13x,xx ») — même chose sur l'écran de révision par indice ;
    - dessous, deux champs côte à côte : « **CA précédent** » grisé et **vide** (aucun CA avant 2025), et « **Nouveau CA HT (2025)** » pré-rempli à **150000**. Après une première révision, l'année suivante affichera « CA précédent (2025) : 150 000,00 € » ;
    - le cadre de résultat ne montre que des tirets.
  - remplacez le CA par **400000** → **Calculer la révision** → cadre de résultat, sans comparaison d'indices :
    - en tête : **Nouveau loyer annuel** ;
    - puis CA HT 2025 400 000,00 €, 8 % du CA 32 000,00 €, nouveau loyer minimum, nouveau loyer maximum (bornes × la hausse de l'ILC) ;
    - « Loyer retenu : **le maximum** (% du CA au-dessus) », ancien loyer 12 000,00 €, prochaine révision 01/01/2027 ;
    - en bas, une petite ligne « Bornes indexées sur l'ILC : … → … , +x % » ;
    - les champs min et max grisés affichent maintenant les nouvelles bornes.
  - remettez la date de révision au **01/01/2026**, saisissez **100000** → « Loyer retenu : **le minimum** (% du CA en dessous) » (8 000 € est sous le minimum) ;
  - recommencez avec **150000** (date remise au 01/01/2026) → loyer **12 000,00 €** (entre les bornes). « Prochaine révision » passe au 01/01/2027, et le champ demande alors le CA 2026 ;
  - Facturer le loyer : 4e trimestre 2025 = **3000.00** (loyer de départ, avant la révision), 1er trimestre 2026 = **3000.00** (12 000 / 4, après la révision). Fermez sans émettre.
- [ ] **L10 · Nettoyage** : supprimez le bâtiment **TEST Nouveautés**.

---

**Résultat** : tout est coché ? Le module est validé en Play. Sinon, donnez-moi le numéro de l'étape et ce que vous avez vu (une capture aide beaucoup).
