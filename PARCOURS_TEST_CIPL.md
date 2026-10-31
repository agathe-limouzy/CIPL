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
  - une seule ligne « **Total** » 3 600,00 € — aucune ligne de TVA (changé le 02/10 : un dépôt n'est pas soumis à la TVA) ;
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
  → Total TTC **-600,00 €  — avoir (remboursement au locataire)**, phrase de règlement « SOMME QUI VOUS SERA REMBOURSÉE », et un menu « **Avoir sur la facture n°** » apparaît : il liste les factures déjà émises de Test Avenant (Jardin + Ménage n'y figure qu'une fois). Choisissez la refacturation de l'eau (J3).
  
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

> **Bâtiment supprimé le 06/10** après validation de L à P (dossier et JSON dans `corbeille_batiments/…_TEST Nouveautés/`). Pour rejouer ces sections, il faut le recréer ou le restaurer depuis la corbeille.

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
  - Générer → aperçu au **format refacturation**, une seule ligne « **Total** », **aucune ligne de TVA** (02/10). Le libellé garde « termes de loyer TTC ». Fermez sans émettre.
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
  - décochez Entretien (seul l'avoir reste) → Total TTC **-600,00 €  — avoir (remboursement au locataire)**, phrase « **SOMME QUI VOUS SERA REMBOURSÉE** » ;
  - le menu « **Avoir sur la facture n°** » apparaît. Il propose 09004 (Jardin +1, une seule fois), 09003 (Eau) et 08001 (Électricité), mais **pas l'avoir 08002**. Choisissez 09003 ;
  - **Générer facture** : « **AVOIR : n°** », juste dessous « **Avoir sur la facture n° 2026/09003** », « Avoir sur refacturation : Avoir eau 2025 », montants négatifs. Fermez sans émettre.
  - onglet Charges du bâtiment : rouvrez « Avoir eau 2025 », le coût affiche **-500** (et non vide). Fermez sans enregistrer.
- [ ] **L9 · Régul 2025 qui rembourse le locataire** (Régularisation des charges, année **2025**)
  - charges : « Entretien espaces verts 2025 » 1 200,00, « Avoir eau 2025 » -500,00, puis « **Eau 2025 · déjà refacturée** » 800,00 en gris. Électricité, Jardin et Ménage, refacturés sans la case « Déduite », n'apparaissent pas ;
  - Total des charges **1 500,00**, Provisions déjà versées **2 000,00** (500 × 4), « Charges déjà refacturées (réglées à part) » **800,00**, Solde HT **-1 300,00  — trop-perçu (avoir)** ;
  - **pas** de menu « Avoir sur la facture n° » sur la régul (retiré le 05/10 : une régul solde les provisions de l'année, elle ne corrige pas une facture précise ; il reste sur la refacturation) ;
  - **Générer facture** : « **AVOIR : n°** », sans ligne « Avoir sur la facture », « Montant à vous rembourser -1 300,00 », « SOMME QUI VOUS SERA REMBOURSÉE ». La page 2 liste l'eau « (déjà refacturée) » et la ligne « Charges déjà refacturées » sous les provisions. Fermez sans émettre.
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
- [ ] **L10 · Nettoyage** : supprimez le bâtiment **TEST Nouveautés** — **seulement après la section M**, qui s'en sert.

## M. Départ du locataire — « TEST Nouveautés » (refait le 02/10 : bouton et bandeau)

Le départ se fait maintenant par le bouton **« Départ du locataire »** de l'en-tête de la fiche, puis un **bandeau de six étapes** (Départ · Dernier loyer · État des lieux · Décompte de sortie · Régul de sortie · Archiver) jusqu'à l'archivage. « Modifier » (section Bail) ne saisit plus le départ : la section Bail l'affiche seulement. Toujours **sans rien émettre**.

C aujourd'hui (essais du 02/10) : bail au 01/01/2026, dernier jour déjà saisi au **15/09/2026**, délai **1** mois, dépôt 3 600 €, pas de provision, aucune facture.

- [ ] **M1 · Locataire sans départ** (fiche **A**) : dans l'en-tête, « **Départ du locataire** » juste avant « Transférer » ; pas de bandeau ; section Bail sans ligne de départ. Cliquez sur Modifier : le bouton disparaît pendant la saisie, et la section Bail n'a **plus** de case « Départ du locataire ». Annulez.
- [ ] **M2 · Bandeau de C** : en-tête « **Départ en cours** » ; sous l'en-tête, « **Départ de C · dernier jour le 15/09/2026** », six étapes : « Départ : fait » (vert), « 2 Dernier loyer » en **ambre** (étape en cours), « Régul de sortie : fait » (C n'a pas de provision), les autres en blanc. Consigne « Étape 2 — Dernier loyer : facturez la dernière période… », bouton « **Continuer** ».
- [ ] **M3 · Fenêtre « Départ du locataire »** (bouton « Départ en cours », ou étapes 1 et 3 du bandeau)
  - dernier jour **15/09/2026**, restitution **1** mois, « Dépôt à restituer avant le **15/10/2026** » (la date suit la saisie : mettez 2 → 15/11, remettez 1) ;
  - refus à « Enregistrer » : dernier jour vide → « saisissez une date » ; délai **30** → « entre 1 et 24 » ;
  - « État des lieux de sortie » : **Non, pas d'état des lieux** → le bloc date + PDF disparaît → **Enregistrer** : étape 3 « État des lieux : fait », section Bail « Départ le 15/09/2026 · pas d'état des lieux · dépôt à restituer avant le 15/10/2026 » ;
  - rouvrez, **Oui**, date **15/09/2026** → Enregistrer : l'étape 3 redevient à faire (« joignez le PDF… ») ; rouvrez → « **Joindre…** » un PDF → son nom s'affiche, ligne « **État des lieux :** nom.pdf » sous le bail, étape 3 ✓.
- [ ] **M4 · Continuer** → « Facturer le loyer » sur le **premier loyer 2026 encore à facturer** (C n'en a aucun : 1er trimestre). Choisissez le **3e trimestre 2026** = **2510.87** (77 jours sur 92). Fermez sans émettre. Le suivi 2026 : 4e trimestre « **Hors bail** ».
- [ ] **M5 · Liste et alertes** : résumé du bâtiment → C **en bas**, grisé, « **Parti le 15/09/2026** » ; « À traiter » → « **Rest. dépôt** » pour C (Attention) ; plus de « Fin de bail » ni de « Résiliation » pour C.
- [ ] **M6 · Décompte de sortie de C** (étape **4** du bandeau, cliquable)
  - carte **Facturation** : le 4e bouton s'appelle « **Décompte de sortie** », avec la pastille « ! » (restitution due avant le 15/10). Carte **Dépôt** : « Réviser » est **grisé**, plus de badge « Révision à faire » ;
  - suivi 2026 : une ligne « **Décompte de sortie (restitution du dépôt)** » à l'échéance **15/10/2026**, bouton « Générer » (il ouvre le même écran) ;
  - étape 4 du bandeau (ou bouton de la carte) → écran « **Décompte de sortie · C · …** ». À la place de la carte du dépôt : « Dépôt de garantie détenu » **3600.00**, « Aucune somme due » (C n'a pas de facture), totaux : Dépôt de garantie **-3 600,00 €**, Solde **-3 600,00 € — à rembourser au locataire (avoir)**. Pas de « Mention TVA » ;
  - « + Ajouter une retenue » : « Réparation porte », **500** → Solde **-3 100,00 €** ; **Générer facture** : une seule ligne « **Total** » -3 100,00 — **aucune information de TVA** (ni HT, ni TVA, ni mention) ;
  - cochez **TVA** sur la retenue → Retenues « 500,00 € HT (+ TVA 100,00 €) », Solde **-3 000,00 €**, le choix « Mention TVA » apparaît ; **Générer facture** : titre « **AVOIR** », sous-titre « Décompte de sortie — état des lieux du 15/09/2026 » (si la date est saisie), lignes « Dépôt de garantie restitué -3 600,00 », « Retenue — Réparation porte (HT, TVA 20 %) 500,00 », Total HT -3 100,00, « TVA 20 % (retenues soumises) » 100,00, TTC **-3 000,00**, « SOMME QUI VOUS SERA REMBOURSÉE ». « − » retire la ligne. Fermez **sans émettre**.
- [ ] **M7 · Annuler le départ** : « Départ en cours » → « **Annuler le départ** » → confirmation → le bandeau disparaît, le bouton redevient « Départ du locataire », la section Bail n'affiche plus de départ, C revient dans la liste normale. (Refusé si le décompte avait été émis.) Ressaisissez ensuite le départ au 15/09/2026 si vous continuez.
- [ ] **M8 · Sommes dues reprises** (facultatif, sur **F** qui a des factures) : « Départ du locataire » → dernier jour **30/09/2026**, « Non, pas d'état des lieux » → Enregistrer → étape 4 du bandeau → Décompte de sortie : les factures dues de F (Envoyé / Impayé — 08001 Électricité, l'avoir 08002 à -180, 09003, 09004 « Jardin et Ménage » en une ligne) sont **cochées** ; Solde = somme des cochées − **7 200** ; décocher une facture la retire du solde. Générer : une ligne par facture reprise, « Facture n° … — … ». Fermez sans émettre, puis « Annuler le départ » de F.
- *(L'archivage — étape 6, « Archiver », puis « Archivé · Réactiver » dans l'en-tête — demande des factures réellement émises et payées : il sera vérifié lors d'un vrai départ, ou plus tard sur ces données.)*

## N. Cas test complet — « H · Départ complet » (02/10)

Un locataire préparé pour passer **les six étapes du départ jusqu'à l'archivage**. Il est créé par script (Claude, Unity **hors Play**) dans TEST Nouveautés :
- loyer 12 000 €/an HT au trimestre, provisions 300 €/trim, dépôt **3 600 €**, bail depuis le 01/01/2025 ;
- **parti le 15/05/2025**, délai de restitution 2 mois → dépôt à rendre avant le **15/07/2025** (dépassé) ;
- 1er trimestre 2025 payé ; **2e trimestre 2025 émis en entier** (3 000 € HT au lieu de 3 000 × 45/91 = **1 483,52**), 3 960 € TTC, impayé ;
- régul 2025 due le **31/03/2026** (passée) ; une charge « **Eau 2025 (test H)** » de 1 200 €, pour H seul.

**Pour émettre** (corriger le loyer, décompte, régul), l'application exige l'email du locataire : mettez **votre propre adresse** dans la fiche H (Modifier → General). Les trois factures partiront vers vous. Sans email, faites tout jusqu'à l'aperçu (« Générer facture ») et sautez N5-N8 côté émission.

- [ ] **N0 · Préparation** : Unity hors Play → je lance le script de création → lancez le Play, DemoCIPL, TEST Nouveautés.
- [ ] **N1 · Liste du bâtiment** : H en bas, grisé, « **Parti le 15/05/2025** ».
- [ ] **N2 · Accueil** : « À traiter » montre pour H trois lignes — « **Rest. dépôt** » (rouge, URGENT), « **Régul.** » (rouge, URGENT), « **Facturer** » (loyer à corriger). Créances : loyer 2e trimestre 2025, **3 960,00 €**, Impayé.
- [ ] **N3 · Fiche H**
  - en-tête « **Départ en cours** » ; bandeau « Départ de H · Départ complet · dernier jour le 15/05/2025 » : « Départ : fait », « **2 Dernier loyer** » en ambre, étapes 3, 4, 5 à faire, « 6 Archiver » grisé ;
  - consigne : « Étape 2 — Dernier loyer : « Loyer 2e trimestre 2025 » a été émis en entier, corrigez-le au prorata. » ;
  - section Bail : « Départ le 15/05/2025 · état des lieux à faire · dépôt à restituer avant le 15/07/2025 » ;
  - carte Facturation : 3 alertes en tête (restitution URGENT, régul de sortie URGENT, loyer à corriger) ; 4e bouton « **Décompte de sortie** » avec pastille ; carte Dépôt : « Réviser » grisé.
- [ ] **N4 · Suivi** — 2025 : 1er trimestre Payé, 2e trimestre **Impayé 3 960,00**, 3e et 4e « Hors bail », « Régularisation des charges 2025 » À faire (31/03/2026), « **Décompte de sortie (restitution du dépôt)** » À faire (15/07/2025). 2026 : loyers « Hors bail », pas de régul 2026.
- [ ] **N5 · Étape 2, corriger le dernier loyer** *(1er essai le 05/10 : correct, mais les alertes de la carte Facturation restaient figées après l'émission — corrigé. H a été remis à l'état d'avant N5 le 05/10 à 16 h 24, cette fois avec le vrai PDF de la facture d'origine : dans le suivi, la ligne du 2e trimestre propose « PDF · **Corriger** · Rappel » ; « PDF » montre la facture d'origine à 3 960,00 €. Après la correction, l'alerte « montant à corriger » doit disparaître **tout de suite** de la carte Facturation, sans rouvrir la fiche)* : **Continuer** → « Facturer le loyer », 2e trimestre 2025 : loyer **1483.52**, provision 300.00 → TTC **2 140,22 €**. Générer facture, puis « Sauvegarder et envoyer ». Suivi : « Loyer 2e trimestre 2025 — corrigée(1) », **2 140,22** ; bandeau « Dernier loyer : fait » ; l'alerte « montant à corriger » disparaît.
- [ ] **N6 · Étape 3, état des lieux** : Continuer (ou l'étape 3) → fenêtre « Départ du locataire » → Oui, date **15/05/2025** → Enregistrer : l'étape 3 reste à faire, « joignez le PDF de l'état des lieux du 15/05/2025 » ; rouvrez → « Joindre… » un PDF → « État des lieux : fait », ligne « État des lieux : … » sous le bail.
- [ ] **N7 · Étape 4, décompte de sortie** : Continuer → dépôt détenu **3600.00**, sommes dues : « 2025/04802 · Loyer 2e trimestre 2025 — corrigée(1) · 2 140,22 € » **cochée** (3 960 si N5 n'a pas été émis) ; « + Ajouter une retenue » « Nettoyage », **200**, sans TVA → Solde **-1 259,78 € — à rembourser (avoir)**. Générer : « AVOIR », un seul « Total » -1 259,78, aucune TVA. Sauvegarder et envoyer (confirmation « Remboursement au locataire ») → le loyer du 2e trimestre passe **Payé** (soldé par le dépôt), « Décompte de sortie : fait », l'alerte de restitution disparaît. « Départ en cours » → « Annuler le départ » est maintenant **refusé** (décompte émis).
- [ ] **N8 · Étape 5, régul de sortie** : Continuer → régularisation 2025 : « Eau 2025 (test H) » **443,84** (1 200 × 135/365), provisions déjà versées **600,00** (300 × 2 trimestres facturés, et non 4), solde HT **-156,16 — trop-perçu (avoir)**, TVA -31,23, TTC **-187,39**. Générer, puis Sauvegarder et envoyer → « Régul de sortie : fait ».
- [ ] **N9 · Étape 6, archiver** : le bandeau dit « **Pas encore archivable** : avoir n° … « Décompte de sortie » à rembourser (1 259,78 €), puis à passer « Payé » · avoir n° … « Régularisation… » à rembourser (187,39 €)… » tant que les deux avoirs ne sont pas remboursés — dans le suivi, passez-les « **Payé** » (menu de la pastille). L'étape 6 devient ambre, le bouton dit « **Archiver** » → clic : retour au résumé, « « H · Départ complet » est archivé… », H n'est plus dans la liste ; la case « **Anciens locataires (1)** » le fait réapparaître (badge « Archivé ») ; plus aucune alerte pour H.
- [ ] **N10 · Réactiver** : ouvrez H depuis « Anciens locataires » → en-tête « **Archivé · Réactiver** » → confirmation → H revient dans la liste, bandeau complet (tout ✓, « Archiver » à refaire).
- [ ] **N11 · Nettoyage** : supprimez H (ou gardez-le pour la suite).

## O. Un locataire en remplace un autre — « TEST Nouveautés » (06/10)

Deux cas, en plus du départ sans successeur (§N). **N'émettez rien.** *(Validé en Play le 06/10.)*

- [ ] **O0 · La fenêtre** : fiche B → en-tête « **Départ du locataire** » (il n'y a plus de bouton « Cession du bail ») → bandeau ambre plein, coins arrondis en haut comme « Paliers » ou « Révision du dépôt », titre sur une ligne (« … » si trop long). En tête : menu déroulant « **Type de départ** » sur *Le locataire part, le lot reste libre* ; ouvert, il propose aussi *Il part, un nouveau locataire reprend le lot* et *Cession du bail*.
- [ ] **O1 · Cession du bail (B)** : dans « Type de départ », choisissez « **Cession du bail** » → les champs du départ disparaissent, le titre devient « Cession du bail — … », le bouton « **Enregistrer la cession** ». La fenêtre demande la date, le nouveau titulaire, SIRET, email, adresse de facturation ; revenir sur « Le locataire part » remet les champs du départ.
  - Nom vide ou « Nouveau » → refusé ; nom d'un autre locataire du bâtiment (ex. le nom exact de A) → refusé ; date avant le début du bail → refusé. Chaque refus s'affiche **au centre** (fenêtre « Attention », bouton « OK »), et non plus en bas de l'écran ; un enregistrement réussi reste annoncé en bas.
  - Date **01/07/2026**, nom « **B bis · Cessionnaire** », adresse « 1 rue du Test » → Enregistrer : l'onglet et la fiche prennent le nouveau nom ; sous les dates du bail : « **Cédé par** <ancien nom de B> **le 01/07/2026** » ; loyer, dépôt, suivi et factures **inchangés** (pas de prorata, pas de nouvelle facture).
  - Facturer le loyer → Générer l'aperçu : le destinataire est « B bis · Cessionnaire », 1 rue du Test. Fermez sans émettre.
  - Pour revenir en arrière : nouvelle cession vers l'ancien nom de B (une cession faite n'a pas d'annulation).
- [ ] **O1b · Cession à une date future (F)** : fiche F → « Départ du locataire » → « Cession du bail », date **01/12/2026**, nom « **F bis · Futur** » → Enregistrer la cession : message « Cession enregistrée : « F bis · Futur » reprendra le bail le 01/12/2026… » ; la fiche, l'onglet et les factures restent au nom de **F** ; sous les dates du bail : « **Cession prévue le 01/12/2026 à F bis · Futur** ».
  - Rouvrez « Départ du locataire » : le type est déjà « Cession du bail », les champs sont remplis ; « **Annuler la cession prévue** » → la ligne « Cession prévue » disparaît, F reste le locataire.
  - (Le changement le jour venu se fait au lancement de l'application : vérifié par le test `Une_cession_future_attend_son_jour`.)
- [ ] **O2 · Remplacement sans cession (A)** : fiche A → « Départ du locataire » → type « **Il part, un nouveau locataire reprend le lot** », dernier jour **30/06/2026**, « Non, pas d'état des lieux » → Enregistrer. (Rouvrir la fenêtre d'un départ déjà saisi ne propose plus la cession.)
  - A passe « Départ en cours » ; un **nouvel onglet** s'ouvre en création : même **lot** et même **surface** que A, début du bail **01/07/2026** ; message « Nouveau locataire du lot … : saisissez son nom et la fin de son bail… ».
  - Nom « **A2 · Remplaçant** », fin du bail au choix → Sauvegarder : le parcours de création continue (loyer, puis dépôt) comme pour tout nouveau locataire.
  - Suivi de A : 3e trimestre 2026 « **Hors bail** » ; de A2 : 2e trimestre 2026 « Hors bail », 3e trimestre facturable en entier.
  - Ensuite : supprimez A2, puis « Annuler le départ » de A.
- *(Le partage des charges entre l'ancien et le nouvel occupant d'un lot — chacun au prorata de ses jours, le lot compté une fois — est vérifié par les tests `Un_lot_qui_change_d_occupant…` et `Le_remplacant_reprend…` : les charges de TEST Nouveautés sont de 2025, réservées à F.)*

## P. Refacturation au prorata — « TEST Nouveautés » (06/10) *(validé en Play le 06/10)*

- [ ] **P1 · C, parti le 15/09/2026** : dans le bâtiment, ajoutez une charge « **Taxe test 2026** », **1 200 €**, datée du **01/10/2026**, pour **C seul**. Fiche C → carte Facturation → Refacturer : sous l'explication, la case « **Au prorata de sa présence dans l'année…** » est visible et **cochée** ; cochez la charge → montant **848.22** (1 200 × 258/365, du 01/01 au 15/09). Décochez la case → **1200.00** ; recochez → 848.22. Un locataire présent toute l'année (F) n'a pas la case. Fermez sans émettre, puis supprimez la charge.

## Q. Listes d'objectifs — défauts (06/10) — DemoCIPL, bâtiment « rivoli » *(validé en Play le 06/10)*

- [ ] **Q1 · Origine en texte** : sur la fiche du bâtiment rivoli, ajoutez l'objectif « **Test Q bâtiment** » ; sur la fiche d'un de ses locataires (ex. le premier de la liste), « **Test Q locataire** ». À l'accueil, ouvrez la vue de tous les objectifs (bouton Objectifs) : sous chaque objectif, l'origine se lit « **Bât. rivoli** » et « **Bât. rivoli · Loc. <nom>** », sans carré.
- [ ] **Q2 · Annuler sur la fiche** : fiche du locataire → supprimez « Test Q locataire » : il disparaît, toast en bas « **Objectif supprimé · Annuler** ». Cliquez Annuler : il revient **à la même place** ; changez d'onglet puis revenez : il est toujours là (enregistré).
- [ ] **Q3 · Annuler dans la vue de tous les objectifs** : supprimez « Test Q bâtiment » depuis cette vue → toast → Annuler : il revient dans la vue et sur la fiche du bâtiment. Supprimez-le à nouveau et laissez le toast se fermer (8 s) : il ne revient pas après un redémarrage.
- [ ] **Q4 · Locataire archivé** *(si un locataire archivé existe ; sinon à vérifier lors d'un vrai archivage)* : ses objectifs ne figurent ni dans « À traiter » ni dans la vue de tous les objectifs ; ils restent sur sa fiche et reviennent s'il est réactivé.
- [ ] **Q5 · Nettoyage** : supprimez « Test Q locataire ».

## R. Objectifs : importance, avancement, échéance, récurrence, documents (06/10) — DemoCIPL *(validé en Play le 06/10)*

*Dates calculées pour un essai le 06/10/2026 ; décalez-les d'autant un autre jour.*

**R0 · Données prêtes** : bâtiment **« TEST Objectifs »** (créé par script hors Play le 06/10, nouveau fichier `batiments/batiment_14ef4f0c-….json`, aucun autre bâtiment touché ; bail des locataires en 2027 pour que « À traiter » ne montre que les objectifs). **À supprimer après les essais** (comme un bâtiment normal). Contenu :
- bâtiment : 4 objectifs à l'**ancien format** (« Ancien · obligatoire / rappel / en cours / fait ») ; « Contrôle des extincteurs » (Obligatoire, **en retard de 5 j**, chaque an, 1 document) ; « Relancer le syndic » (Rappel, **dans 4 j**, prévenir 7) ; « Ravalement de façade » (échéance 30/06/2027) ; « Repeindre le hall » (sans échéance) ; « **Entretien chaudière** » mensuel : faits le 16/08 et le 14/09 (1 document chacun), en cours celui du **15/10** (dans 9 j) ;
- locataire **Dupont Objectifs** : « Faire signer l'avenant » (Obligatoire, dans 14 j), « Valider le devis peinture » (1 document) ;
- locataire **Ancien Archivé** (archivé) : « NE DOIT PAS APPARAÎTRE », en retard.

- [ ] **R0b · Lecture directe** : ouvrez TEST Objectifs. Ordre de la liste : Contrôle des extincteurs (liseré rouge, « En retard de 5 j · échéance 01/10/2026 · chaque an · 1 document »), Relancer le syndic (« Dans 4 j »), Entretien chaudière (« Dans 9 j · échéance 15/10/2026 · chaque mois »), Ancien · obligatoire, Ancien · rappel, puis les Normale : Ravalement (« Échéance 30/06/2027 »), puis les sans date. Filtre « Faits (3) » : les deux entretiens et « Ancien · fait ». Ouvrez l'entretien du 15/10 : « **Occurrences précédentes (2)** » ; « Ouvrir » sur celle d'août → son PDF s'ouvre avec « Ouvrir ». « À traiter » (accueil) : extincteurs en tête, syndic, entretien, l'avenant de Dupont ; **rien** de « Ancien Archivé ».
- [ ] **R0c · Clic dans « À traiter »** (retours du 06/10) : sur le résumé de TEST Objectifs comme à l'accueil, un clic sur un **objectif du bâtiment** ouvre la **fiche complète du bâtiment** avec la fenêtre de l'objectif par-dessus ; un clic sur « Faire signer l'avenant » ouvre la **fiche de Dupont Objectifs** avec sa fenêtre. Une modification s'y enregistre ; en fermant, on reste sur la fiche, l'objectif visible dans sa liste. Ouvrez ensuite la fiche de **Dupont Objectifs** : sa liste montre ses **2 objectifs** (elle était vide). Ajoutez-en un 3e depuis sa fiche : les 2 autres **restent** (avant ce correctif, ils étaient effacés). Une alerte de bail ou de facturation ouvre toujours la fiche du locataire concerné.
- [ ] **R1 · Anciens objectifs repris** : « Ancien · obligatoire » a le badge Obligatoire, « Ancien · rappel » Rappel, tous deux « À faire » ; « Ancien · en cours » : Normale, « En cours » ; « Ancien · fait » est dans « Faits ». Aucun n'a d'échéance. *(Ensuite, les étapes R2 à R10 peuvent se faire dans TEST Objectifs plutôt que rivoli.)*
- [ ] **R2 · Création** : fiche du bâtiment → « + Ajouter » ouvre la fenêtre « Nouvel objectif ». Objectif « **Test R extincteurs** », Importance **Obligatoire**, Échéance **01/10/2026**, Récurrence **Répéter tous les 1 ans** ; sous la récurrence, la fenêtre doit annoncer « … créé pour le **01/10/2027** ». Enregistrer. La ligne a un liseré **rouge**, « **En retard de 5 j · échéance 01/10/2026 · chaque an** », badge « Obligatoire », bouton « À faire ». Le filtre « En retard (1) » la montre seule.
- [ ] **R3 · Échéance proche, prévenir réglable** : « **Test R syndic** », Rappel, échéance **10/10/2026**, prévenir **7** → « **Dans 4 j** ». Rouvrez-le et mettez prévenir **2** : il passe « Échéance 10/10/2026 » (pas encore proche).
- [ ] **R4 · « À traiter »** (accueil et carte du bâtiment) : « Test R extincteurs · en retard de 5 j » en tête, pastille rouge, type « Obligatoire ». Avec prévenir 7, « Test R syndic · dans 4 j » remonte aussi. L'onglet de rivoli a son point d'alerte.
- [ ] **R5 · Fait → occurrence suivante** : sur « Test R extincteurs », cliquez le bouton d'avancement deux fois (À faire › En cours › Fait). Toast « Fait. Prochain « Test R extincteurs » le **01/10/2027** ». La liste montre un nouvel objectif « Échéance 01/10/2027 · chaque an », qui ne remonte plus dans « À traiter ». Filtre « Faits (1) » : l'ancien, barré, « **Fait le 06/10/2026** ».
- [ ] **R6 · Rouvrir** : dans « Faits », cliquez son bouton « Fait » : il repasse « À faire », en retard, et l'occurrence 2027 **disparaît**.
- [ ] **R7 · Modifier** : clic sur le texte d'un objectif → « Modifier l'objectif », champs remplis. Changez le texte et l'importance, enregistrez : la ligne suit. Fermez la fiche et rouvrez-la : c'est toujours là.
- [ ] **R8 · Documents** : dans la fenêtre, « + Joindre des documents… » → sélectionnez **2 fichiers** d'un coup (Ctrl + clic). Les deux sont listés, « Ouvrir » ouvre chacun d'eux. Recliquez pour en ajouter un 3e. « Retirer » en enlève un. Enregistrez : « … · 2 documents » sous le texte. Le dossier `Batiment/rivoli/Objectifs/` contient les copies (sur un objectif de locataire : `Batiment/rivoli/<locataire>/Objectifs/`).
- [ ] **R9 · Contrôles** : Répéter sans échéance → message au centre « Une répétition part de l'échéance… » ; texte vide → « Écrivez l'objectif. » ; date incomplète → message sur l'échéance.
- [ ] **R10 · Vue de tous les objectifs** (accueil, bouton Objectifs) : mêmes lignes et même ordre. Filtres Normale et En retard. Un clic sur une ligne ouvre la fenêtre ; une modification faite ici se retrouve sur la fiche, **même si la fiche était ouverte**, et survit à un « Sauvegarder » de la fiche.
- [ ] **R10b · Historique et clic sur la ligne** : cliquez **n'importe où sur la ligne** d'un objectif (pas seulement le texte) → la fenêtre s'ouvre ; ajoutez-y un document, enregistrez. Les boutons d'avancement et de suppression gardent leur propre action. Dans la vue de tous les objectifs, le filtre « **Faits (n)** » montre l'historique (le plus récent d'abord, « Fait le … ») ; un clic sur un objectif fait l'ouvre aussi, et on peut lui ajouter un document sans le rouvrir.
- [ ] **R10c · Occurrences précédentes** : objectif « **Test R entretien** », échéance **01/09/2026**, répéter **tous les 1 mois**, avec un document. Passez-le « Fait » : l'occurrence du 01/10/2026 apparaît. Passez-la « Fait » aussi : celle du 01/11/2026 apparaît. Ouvrez celle du 01/11 : en bas de la fenêtre, « **Occurrences précédentes (2)** » : « Fait le 06/10/2026 · échéance 01/10/2026 », puis « Fait le 06/10/2026 · échéance 01/09/2026 · 1 document ». « Ouvrir » sur la seconde ouvre sa fenêtre **par-dessus** (son document y est) ; « Annuler » ramène à la fenêtre du 01/11. Un objectif non récurrent n'a pas cette section.
- [ ] **R11 · Nettoyage** *(à faire après le §S, qui réutilise ces données)*

## S. Tableau des objectifs et filtres (06/10) — DemoCIPL, « TEST Objectifs » *(validé en Play le 08/10)*

- [x] **S1 · Le tableau** : fiche complète de TEST Objectifs, section Objectifs. En haut : « Importance » Toutes · Obligatoire · Rappel · Normale, puis « + Ajouter » à droite. Dessous, trois colonnes **À faire · En cours · Fait** avec leur nombre ; « À faire » affiche « 1 en retard ». Ordre de « À faire » : Contrôle des extincteurs (liseré rouge, étiquette rouge « En retard de 5 j », « récurrent », « 1 doc. »), Entretien chaudière (« Dans 9 j »), puis les obligatoires et rappels sans date, puis les normales. « Fait » : les 2 plus récents, puis « Voir tout l'historique (3) » → les 3 → « Réduire ».
- [x] **S2 · Filtre d'importance** : « Obligatoire » (pastille pleine) → seules les cartes obligatoires dans les trois colonnes, nombres à jour ; « Toutes » revient.
- [x] **S3 · Bouton « › »** : sur « Repeindre le hall », « › » → passe dans « En cours » ; « › » encore → « Fait », barré, « Fait le 06/10/2026 », bouton « Rouvrir » → revient dans « À faire ».
- [x] **S4 · Glisser** : faites glisser « Ravalement de façade » vers « En cours » : une copie suit le pointeur, la carte change de colonne au relâchement. Glissez « Contrôle des extincteurs » dans « Fait » : toast « Fait. Prochain … le 01/10/2027 », une nouvelle carte 2027 apparaît dans « À faire ». Relâchée hors des colonnes, une carte ne bouge pas. Un simple clic (sans glisser) ouvre toujours la fenêtre.
- [x] **S5 · Rappels automatiques** : sur une fiche de locataire qui a un rappel (ex. un locataire de rivoli avec un loyer à facturer ou une révision), « À faire » commence par des cartes beiges **« Automatique »** (ex. « Facturer le loyer · À faire »), non déplaçables ; un clic ouvre l'écran (facture du loyer, régul, dépôt, révision ; fin de bail : la fiche). Sur la fiche du **bâtiment**, ces cartes portent le nom du locataire. TEST Objectifs n'en a pas (baux en 2027) : rien d'automatique, c'est normal.
- [x] **S6 · Fiche locataire** : Dupont Objectifs → même tableau avec ses 2 objectifs.
- [x] **S6b · Défilement et place** (retour du 07/10) : fiche complète de **TEST Objectifs** (une dizaine de cartes dans « À faire ») : le tableau s'arrête à environ 5 cartes de haut et **défile en entier** à la molette (les trois colonnes ensemble). Sur la fiche de **Dupont** (peu de cartes), le tableau est réduit à son contenu et la molette fait défiler la fiche. Fiche locataire : plus de carte « Facturation » dans la colonne de droite ; ses alertes et ses **4 boutons sur une ligne** sont en tête du « Suivi de facturation », pastilles « ! » comprises ; chacun ouvre son écran comme avant (et « Révision du dépôt » devient « Décompte de sortie » après un départ).
- [x] **S6c · Mise en page et zoom** (retours du 07/10) : fiche de Dupont à 100 % : les trois colonnes ont la même largeur, une carte seule dans sa colonne garde sa hauteur, le liseré suit l'arrondi de la carte ; les deux grandes colonnes de la fiche ne bougent plus quand on déplace une carte ou change de filtre. Cartes sur deux lignes : « Obligatoire · Retard 5 j », puis « récurrent · 1 doc. · › ». Réglages → zoom **175 %** : les deux grandes colonnes s'empilent comme avant ; le tableau garde des colonnes lisibles et **défile en largeur** au lieu d'écraser les cartes. Revenez à 100 % : il reprend toute la largeur, sans défilement horizontal.
- [x] **S6d · Hauteurs et en-têtes** (retours du 07/10) : fiche de Dupont : les deux grandes colonnes se terminent au même niveau en bas (le Dépôt de garantie s'allonge si besoin). Dans les deux fiches (Dupont et fiche complète de TEST Objectifs) : cartes blanches, liseré fin et arrondi dans la carte ; faites défiler le tableau à la molette : les en-têtes « À faire · En cours · Fait » restent entiers au-dessus, seules les cartes défilent.
- [x] **S6e · Catégorie et menus au zoom** (07/10) : partout, « Normale » s'appelle désormais **« Tâche »** (badges, filtres, « À traiter ») et la fenêtre d'un objectif dit **« Catégorie »** (Tâche · Rappel · Obligatoire). Réglages → zoom 175 % : dans la fenêtre d'un objectif, les menus Catégorie, Avancement et Récurrence s'ouvrent juste sous leur champ (ils partaient en haut à droite) ; idem pour les menus des écrans de facture et le menu d'état du suivi. Revenez ensuite à 100 %.
- [x] **S6f · « À traiter » en tuiles** (07/10) : accueil, puis résumé de TEST Objectifs : au-dessus de la liste, 5 tuiles **Tout · En retard · À venir · Automatiques · Objectifs** avec leur nombre. Un clic filtre la liste (tuile active en couleur pleine) ; « Tout » revient. Le compteur rouge du titre garde le total. Dessous, pastilles **Catégorie : Toutes · Obligatoire · Rappel · Tâche** : « Obligatoire » ne garde que les objectifs obligatoires et les nombres des tuiles baissent ; « Rappel » garde les objectifs rappel **et** les rappels automatiques (facturation, bail, révision) ; combinable avec une tuile (ex. En retard + Rappel). Résumé : le bouton **« Anciens locataires (1) › »** remplace la case ronde ; un clic montre l'ancien locataire et devient « Masquer les anciens (1) ».
- [x] **S6g · Pastille « Automatique » du tableau** (08/10, *validé en Play le 08/10*) : fiche Dupont (TEST Objectifs), section Objectif : pastilles **Toutes · Obligatoire · Rappel · Tâche · Automatique**. « Automatique » → seules « Réviser le dépôt de garantie » et « Fin de bail » restent (À faire 2, autres colonnes « Rien ici »). « Obligatoire » ou « Tâche » → plus aucune carte automatique. « Rappel » → « Décider du renouvellement du bail » + les 2 automatiques. « Toutes » → tout revient. Même chose sur la fiche du bâtiment.
- [x] **S7 · Liste de tous les objectifs** (accueil, bouton Objectifs) : en haut, 5 **tuiles** Tous · À faire · En cours · En retard · Faits avec leur nombre ; dessous, « Importance » Toutes · Obligatoire · Rappel · Normale. Tuile « En retard » + « Obligatoire » → seul « Contrôle des extincteurs » ; les nombres des tuiles suivent l'importance choisie. Tuile « Faits » → l'historique, le plus récent d'abord. : supprimez les objectifs « Test R … » (toast « Annuler » disponible).

---

## T. Paiement partiel d'une facture (08/10) — DemoCIPL, rivoli · Starbucks Coffee *(validé en Play le 08/10)*

- [x] **T1 · Ouvrir** : fiche Starbucks Coffee, suivi de facturation 2026, « Loyer Août 2026 » (9 600,00 €, Impayé) → clic sur la pastille → **« Paiement partiel… »** (sous « Payé ») → fenêtre « Paiement — Loyer Août 2026 · 9 600,00 € », « Aucun versement pour l'instant », « Reste à payer : 9 600,00 € ».
- [x] **T2 · Verser une partie** : date du jour, montant `4 000` → « Ajouter » : la ligne apparaît, « Payé : 4 000,00 € · Reste à payer : 5 600,00 € ». « Enregistrer » → toast « reste 5 600,00 € à payer ». Dans le suivi, le montant devient « reste 5 600,00 € » puis « sur 9 600,00 € » en petit ; la pastille devient orange **« Partiel »** (dans Créances, la facture reste comptée dans « Impayé » et le rappel reste proposé).
- [x] **T3 · Créances (accueil)** : Starbucks Coffee — le loyer d'août compte **5 600,00 €**, et le total Impayé baisse de 4 000 €.
- [x] **T4 · Refus** : montant `6 000` (plus que le reste) → message au centre « Le versement dépasse le reste à payer (5 600,00 €). » ; montant vide ou `0` → refusé aussi.
- [x] **T5 · Solder** : nouveau versement de `5 600` → « Facture réglée » → « Enregistrer » → la facture passe **Payé**, montant 9 600,00 €, elle sort des Créances.
- [x] **T6 · Retirer** : sur cette facture payée, « Paiement partiel… » → « Retirer » le versement de 5 600 → « Enregistrer » → elle redevient **Partiel** avec « reste 5 600,00 € ».
- [x] **T7 · Partiel** : « Loyer 4e Trimestre 2026 » de Sephora (Envoyé, pas encore échu) → verser une partie → pastille orange **« Partiel »** dans le suivi et dans Créances.
- [x] **T8 · Annuler** : ajouter un versement puis « Annuler » → rien n'a changé.

**Résultat** : tout est coché ? Le module est validé en Play. Sinon, donnez-moi le numéro de l'étape et ce que vous avez vu (une capture aide beaucoup).

## U. Essai technique cloud (12/10) — connexion, verrou, temps réel, lien cipl:// *(hors Play, dans l'éditeur ; U7 dans un build)*

Rien ne touche aux vraies données : l'essai n'utilise que deux tables d'essai sur Supabase (`essai_fiches`, `essai_verrous`) et la fiche fictive « volteo ». Le journal de la fenêtre est aussi écrit dans la console Unity (`[EssaiCloud]`), je peux donc le relire moi-même.

*Validé le 12/10 avec deux vrais comptes : U0 à U6 (verrou refusé en 114 ms, changements reçus en 86 à 555 ms, reprise automatique après 2 min), puis U7 dans un build. **Étape 1 terminée.***

- [x] **U0 · Préparer Supabase (à faire par vous, une fois)** :
  1. Sur supabase.com, créez un compte, puis « New project » : nom `cipl-essai`, région **West EU (Paris)**, et un mot de passe de base de données (gardez-le pour vous).
  2. Project Settings › API Keys (ou bouton « Connect ») : copiez l'**adresse du projet** (`https://xxxx.supabase.co`) et la **Publishable key** (`sb_publishable_…`). Pas la « secret key ».
  3. SQL Editor › New query : collez tout `Serveur/essai_supabase.sql`, puis Run → « Success. No rows returned ».
  4. Authentication › Users › Add user › Create new user : deux comptes de test (par exemple `marie.essai@…` et `paul.essai@…`), un mot de passe chacun, et cochez **Auto Confirm User**.
- [x] **U1 · Ouvrir l'essai** : menu **CIPL › Essai cloud (étape 1)**. Collez l'adresse et la clé, puis « Enregistrer l'adresse et la clé » → journal « Adresse et clé enregistrées ».
- [x] **U2 · Connexion** : Session A, e-mail et mot de passe de Marie, puis « Se connecter » → « Session A : connectée (…), jeton valable jusqu'à HH:MM:SS » (environ 1 h plus tard). Même chose en Session B avec Paul. Avec un mauvais mot de passe → « connexion refusée — HTTP 400 Invalid login credentials ». « Renouveler le jeton » → nouvelle heure d'expiration.
- [x] **U3 · Verrou** : A « Prendre » → « verrou **OBTENU** sur Volteo ». B « Prendre » → « verrou **REFUSÉ** — En cours de modification par Marie Dupont (facturation) depuis 14 h 32 ». A « Libérer », puis B « Prendre » → obtenu.
- [x] **U4 · Libération seule après 2 min** *(validé le 12/10 dans les deux sens : Marie reprend la fiche abandonnée par Paul, puis Paul celle de Marie ; le signal de vie prolonge bien le verrou)* : B détient la fiche. Ne touchez plus à B : sous B, le compte à rebours descend de 2:00 à « expiré ». Alors A « Prendre » → **OBTENU**. Variante : B « Signal de vie » remet le compte à rebours à 2:00.
- [x] **U5 · Temps réel** : A « Écouter » → « abonnée aux changements de essai_fiches et essai_verrous ». B « Loyer + 100 € » → chez B « loyer 24 000 € → 24 100 € (version 2) », et chez A « **REÇOIT** : Volteo mis à jour par Paul Lefèvre — loyer 24 100 € (version 2) — reçu **N ms** après l'envoi ». On attend moins de 1 000 ms. Aussi : B « Prendre » → A reçoit « verrou de volteo — Paul Lefèvre (facturation) ». Et une modification faite à la main dans Supabase (Table Editor › essai_fiches › loyer) arrive aussi chez A.
- [x] **U6 · Contrôle de version** : B « Version périmée » → « écriture **REFUSÉE** — … Rien n'a été écrit », et A ne reçoit rien.
- [x] **U7 · Lien cipl:// (build)** *(validé le 12/10 avec `Build\CIPL Demo 2\CIPL_Test.exe` : protocole inscrit dans HKCU, lien reçu)* : File › Build Settings › Build, dans le dossier `Build`. Lancez `CIPL.exe` une fois (il déclare cipl:// pour votre session Windows ; n'utilisez pas « Tout envoyer » si la fenêtre des factures s'ouvre). Laissez-le ouvert, puis Windows + R : `cipl://invitation?jeton=Essai12345` → Entrée. Dans la fenêtre déjà ouverte, message en bas « Lien reçu : invitation (jeton Essai1…) », et **aucune 2e fenêtre** ne reste ouverte. Fermez CIPL, refaites Windows + R : CIPL s'ouvre et affiche le même message.
- [ ] **U8 · Nettoyage (facultatif)** : section NETTOYAGE en bas du script SQL, ou on garde les tables pour l'étape 2. Pour retirer cipl:// de Windows : `reg delete HKCU\Software\Classes\cipl /f`.

**Résultat** : notez le numéro de l'étape qui ne passe pas et le message du journal. Je peux aussi le relire directement dans la console Unity.

## V. Étape 2 : connexion, import et lecture depuis le cloud (12/10) *(validé en Play le 12/10, V1 à V8)*

Préalable : `Serveur/etape2_supabase.sql` passé dans Supabase (SQL Editor › Run). CIPL démarre maintenant **sur le cloud, en lecture seule** jusqu'à l'étape 4.

- [x] **V1 · Connexion** : lancer le Play → écran « CIPL — Connexion » à la place de l'accueil. E-mail, mot de passe, « Rester connecté(e) » coché, puis « Se connecter ». Un mauvais mot de passe donne le message au centre « E-mail ou mot de passe incorrect ».
- [x] **V2 · Import (cloud vide)** : écran « Bienvenue sur CIPL en ligne », avec les entreprises de ce PC (aucune cochée). Cocher **Démo CIPL** seulement (pas « Mon entreprise »), puis « Importer » → progression « bâtiment 1 / 4… », puis « Démo CIPL importée : 4 bâtiment(s) ». Le dossier de Démo CIPL reçoit le repère `CIPL_entreprise.json` ; rien d'autre n'est modifié sur le PC.
- [x] **V3 · Lecture** : CIPL ouvre directement Démo CIPL (seule entreprise), avec les 4 onglets rivoli, Parc Tertiaire Sud, Auterive et Le Hangar. Pastille en bas à gauche « Démo CIPL · cloud · lecture seule ». Plus de fenêtre « factures à envoyer » (suspendue en lecture seule).
- [x] **V4 · Lecture seule** : une fiche › Modifier › Enregistrer → message au centre « Lecture seule pendant la construction : vos modifications ne sont pas enregistrées. »
- [x] **V5 · Compte** : Accueil › « Compte » → e-mail, « Démo CIPL · Admin ». « Changer d'entreprise » → écran « Choisir l'entreprise ».
- [x] **V6 · Se déconnecter** : retour à l'écran de connexion.
- [x] **V7 · Connexion gardée** : arrêter puis relancer le Play (après s'être reconnecté avec la case cochée) → pas de mot de passe demandé.
- [x] **V8 · 2e compte** *(validé le 12/10 : agathe.limouzy@cipl.fr voit Démo CIPL)* : dans `etape2_supabase.sql`, bloc « AJOUTER UN MEMBRE » : mettre l'e-mail du 2e compte et **`Démo CIPL`** comme nom, retirer les `--`, puis Run. Se déconnecter dans CIPL, se connecter avec le 2e compte → il voit Démo CIPL (« Utilisateur »), sans écran d'import.

## W. Étape 4a : enregistrement sur le cloud et verrou des fiches (13/10) *(validé en Play le 13/10, W1 à W6)*

Préalable : `Serveur/etape4_supabase.sql` passé dans Supabase (SQL Editor › Run). Les modifications sont maintenant **enregistrées sur le cloud** ; plus de lecture seule.

- [x] **W1 · Votre nom** : première ouverture avec un compte sans nom → fenêtre « Votre nom », saisir, Valider → les 4 onglets, pastille « Démo CIPL · cloud ».
- [x] **W2 · Reconnexion** *(défaut du 13/10 : écran vide « 4 bâtiments · 0 locataire »)* : Accueil › Compte › Se déconnecter, se reconnecter avec l'autre compte (saisir son nom si demandé) → les 4 onglets et leurs locataires reviennent, l'accueil compte bien les locataires. Même chose avec Compte › Changer d'entreprise › Démo CIPL.
- [x] **W3 · Enregistrer** : Le Hangar › Décathlon › Modifier, changer un champ sans conséquence (le téléphone par exemple), Enregistrer → aucun message. Arrêter puis relancer le Play → la modification est toujours là.
- [x] **W4 · Deuxième poste** : reconstruire le build (il doit contenir le nouveau code), le lancer et s'y connecter avec le compte 2 → Démo CIPL.
- [x] **W5 · Fiche prise** : dans l'éditeur (compte 1), Décathlon › Modifier, sans enregistrer. Dans le build, sous 10 s, bandeau ambre en haut de la fiche Décathlon « En cours de modification par <nom> … — lecture seule » ; clic sur Modifier → message au centre avec le nom. Un autre locataire du Hangar reste modifiable.
- [x] **W6 · Fiche libérée** : dans l'éditeur, Enregistrer (ou Annuler) → sous 10 s le bandeau disparaît dans le build, et Modifier y fonctionne.

## X. Étape 4b : verrou des écrans, « Toujours là ? », « Forcer la libération » (13/10) *(validé en Play le 13/10, X1 à X6)*

Préalable : `Serveur/etape4b_supabase.sql` passé dans Supabase (SQL Editor › Run), et le build **reconstruit**. Éditeur = compte 1 (**admin** de Démo CIPL), build = compte 2 (**utilisateur**). Dans les panneaux de facture, **ne cliquez jamais « Générer »** : on teste seulement l'ouverture.

- [x] **X1 · Facturation** : éditeur, Le Hangar › Décathlon › « Facturer le loyer », panneau laissé ouvert. Build, sous 10 s : bandeau ambre sur Décathlon « … (facturation) … — lecture seule » ; Décathlon › « Facturer le loyer » → refus au centre avec le nom. Fermer le panneau dans l'éditeur → sous 10 s le bandeau disparaît dans le build.
- [x] **X2 · Régularisation = bâtiment + locataire** : éditeur, Décathlon › « Régularisation des charges », laissé ouvert. Build : Le Hangar › Modifier → refus « … (régularisation) … » ; « TEST Pennylane — Volteo » › « Régularisation des charges » → refus sur « Le Hangar » ; Volteo › « Facturer le loyer » → s'ouvre (autre locataire). Tout fermer.
- [x] **X3 · Charges** : éditeur, rivoli › Charges › « + Ajouter une charge », formulaire ouvert. Build : rivoli › Modifier → refus « … (charges) … ». Annuler le formulaire dans l'éditeur → sous 10 s Modifier passe dans le build.
- [x] **X4 · Deux écrans sur la même fiche** : éditeur, Décathlon › Modifier, puis « Facturer le loyer », puis fermer le panneau → dans le build le bandeau reste (la fiche est encore en saisie). Enregistrer dans l'éditeur → il disparaît.
- [x] **X5 · Toujours là ?** *(le délai est raccourci à 1 min pour l'essai : dis-moi quand le Play est lancé)* : éditeur, Décathlon › Modifier, puis ne plus toucher à CIPL. Après 1 min : fenêtre ambre « Toujours là ? » avec un compte à rebours qui part de 2:00. « Continuer » → la fiche reste en saisie. Recommencer et laisser le compte à rebours aller à 0 → message au centre, l'entreprise est relue, la fiche n'est plus en saisie et le bandeau disparaît dans le build.
- [x] **X6 · Forcer la libération** : build (compte 2), Décathlon › Modifier, changer le téléphone sans enregistrer. Éditeur (admin) : le bandeau de Décathlon a un bouton « Forcer la libération » → confirmer « Libérer » → message en bas « Décathlon est libérée ». Dans le build, sous 30 s : message au centre « <nom> (admin) a libéré « Décathlon »… », la fiche est relue **sans** le téléphone modifié. Dans le build (utilisateur), les bandeaux n'ont pas ce bouton.

## Y. Étape 5 : en direct — changements des collègues, présence (13/10) *(validé en Play le 14/10, Y1 à Y7)*

Préalable : `Serveur/etape5_supabase.sql` passé dans Supabase (SQL Editor › New query › Run), et le build **reconstruit**. Éditeur = compte 1, build = compte 2, tous deux sur Démo CIPL.

- [x] **Y1 · En ligne** *(validé le 14/10, après correction : les initiales restaient 2 min 40 après la fermeture du build)* : sous 30 s, au bout de la barre d'onglets de chaque fenêtre : « En ligne : » suivi des initiales de l'autre. Clic dessus → message en bas avec son nom. Fermer le build → les initiales disparaissent de l'éditeur.
- [x] **Y2 · Fiche affichée** : éditeur sur Le Hangar, onglet Décathlon. Build : Décathlon › Modifier, changer le téléphone, Enregistrer → dans l'éditeur, en quelques secondes : message en bas « « Le Hangar » mis à jour par <nom> », le nouveau téléphone s'affiche, et l'on reste sur l'onglet Décathlon.
- [x] **Y3 · Accueil** : éditeur sur l'accueil. Build : rivoli › Sephora › Modifier › changer le téléphone › Enregistrer → message dans l'éditeur, l'accueil se remet à jour.
- [x] **Y4 · Bâtiment non affiché** : éditeur sur Auterive. Build : modifier un locataire de rivoli → pas de message dans l'éditeur ; ouvrir rivoli → la modification y est.
- [x] **Y5 · J'y travaille** *(validé le 14/10 ; 1er essai faussé : le build avait enregistré avant le clic sur Modifier dans l'éditeur)* : éditeur : Le Hangar › Décathlon › Modifier, sans enregistrer. Build : Le Hangar › « insee » › Modifier, changer le téléphone, Enregistrer → éditeur : message « « Le Hangar » modifié par <nom> : affiché quand vous aurez fini », la fiche ne bouge pas. Enregistrer Décathlon dans l'éditeur → la fiche se met à jour (téléphone d'« insee » changé) et la saisie de Décathlon est bien gardée.
- [x] **Y6 · Prénom** *(revu le 14/10 : les onglets des locataires sont masqués dans CIPL)* : build : rivoli › Sephora › Modifier. Éditeur : l'onglet du bâtiment devient « rivoli · <prénom> » en ambre ; sur le résumé de rivoli, la ligne « Sephora · <prénom> » ; sur la fiche Sephora, le bandeau. Enregistrer dans le build → les prénoms disparaissent.
- [x] **Y7 · « En ligne » à sa place** *(retour du 14/10)* : le « + » de la barre est plus étroit, et « En ligne : (OL) » se tient entre lui et le bouton rouge, centré verticalement, lisible (vert clair).

## Z. Étape 6a : documents partagés, mode « Sur chaque PC » (14/10) *(validé en Play le 14/10, Z1 à Z4 ; le build tient lieu de 2e PC)*

Préalable : `Serveur/etape6_supabase.sql` passé dans Supabase (SQL Editor › New query › Run), et le build **reconstruit**. Éditeur = compte 1 (son dossier Démo CIPL actuel), build = compte 2 (il reçoit un dossier neuf). Les documents partagés sont ceux rangés dans `Batiment/` et `Logo/` ; un passage a lieu toutes les 20 s.

- [x] **Z1 · Envoi initial** : lancer le Play dans l'éditeur → dans les 20 s, message en bas « N document(s) de ce PC envoyé(s) au cloud » (une quarantaine pour Démo CIPL). Dans Supabase › Storage › documents : un dossier au numéro de l'entreprise.
- [x] **Z2 · Nouveau PC** : lancer le build (compte 2) → il crée `Documents\CIPL\Démo CIPL` et reçoit les documents : message « N document(s) reçu(s) de vos collègues ». Dans l'Explorateur, ce dossier contient `Batiment\rivoli\…` avec les factures.
- [x] **Z3 · Ajout** : éditeur, rivoli › Photos › ajouter une photo → dans le build, sous 20 s environ, « 1 document(s) reçu(s) », et la photo apparaît dans la galerie de rivoli.
- [x] **Z4 · Suppression** *(validé le 14/10)* : build, rivoli › Photos › supprimer cette photo → dans l'éditeur, sous 20 s environ, « 1 supprimé(s) par un collègue, rangé(s) dans la corbeille ». Le fichier est dans la corbeille **du dossier de l'éditeur** (pas `Documents\CIPL\Démo CIPL`, qui est celui du build) : `…\AppData\LocalLow\DefaultCompany\CIPL_Test\DemoCIPL\CIPL_Saves\corbeille_documents\<date>\Batiment\rivoli\Photos\`. Rien n'est effacé.

## AA. Étape 6b : Drive partagé et Réglages › Documents (14/10)

Préalable : `Serveur/etape6b_supabase.sql` passé dans Supabase (SQL Editor › New query › Run), et le build **reconstruit**. Éditeur = compte **admin** de Démo CIPL, build = compte 2 (utilisateur). Le Drive partagé de l'essai est ton OneDrive (`C:\Users\agath\OneDrive`) : OneDrive y recopiera les documents de Démo CIPL (environ 6 Mo).

- [x] **AA1 · Réglages › Documents** : éditeur, Réglages → la section « Documents » remplace « Emplacement de sauvegarde ». « Dans CIPL » en couleur ; « Sur cet ordinateur » : « Dossier de CIPL » et le chemin actuel.
- [x] **AA2 · Passer en Drive** : cliquer « Drive partagé » → choisir `C:\Users\agath\OneDrive` → « Ranger ici » → message « Documents rangés dans le Drive partagé (N recopié(s)) », l'entreprise est relue. Dans l'Explorateur, `OneDrive\Démo CIPL` contient `Batiment\…`, `reglage.json` et `CIPL_entreprise.json`. Réglages › Documents : « Drive partagé » en couleur, « Choisi à la main », le chemin OneDrive.
- [x] **AA3 · Trouvé tout seul** : lancer le build (compte 2) → message « Recherche des documents de Démo CIPL dans votre Drive… », puis, en quelques secondes et sans aucune question, « Documents de Démo CIPL trouvés dans votre Drive : <dossier choisi en AA2> ». *(Le 15/10, la recherche a pris 3 min 30 et c'est corrigé. Le build a déjà retenu le dossier, donc cette recherche ne se refait pas : AA4 la refait.)* Réglages › Documents dans le build : « Drive partagé », « Seul un administrateur peut changer le rangement », « Trouvé automatiquement ». Fermer le build.
- [x] **AA4 · Introuvable** : renommer `OneDrive\Démo CIPL\CIPL_entreprise.json` en `CIPL_entreprise.x.json`, relancer le build → fenêtre « Documents de Démo CIPL ». « Choisir le dossier… » → choisir `Documents\CIPL` → refus en rouge dans la fenêtre. Remettre le nom `CIPL_entreprise.json`, puis « Rechercher à nouveau » → trouvé, l'accueil s'ouvre. Fermer le build.
- [x] **AA5 · Continuer sans le dossier** : renommer de nouveau le repère, relancer le build → « Continuer sans le dossier » → choisir `Documents` → le build crée `Documents\Démo CIPL` et y reçoit les documents (« N document(s) reçu(s) »). Réglages › Documents : « Hors du Drive : ce PC reçoit lui-même les documents ». Remettre le nom du repère, puis Réglages › Documents › « Rechercher à nouveau » → « Trouvé automatiquement ». Fermer le build.
- [x] **AA6 · Retour dans CIPL** *(build fermé)* : éditeur, Réglages › Documents › « Dans CIPL » → confirmer → l'entreprise est relue depuis `Documents\CIPL\Démo CIPL` ; rien n'est effacé dans OneDrive.
- [x] **AA7 · Dossier précis sur ce PC** *(mode « Dans CIPL », build fermé)* :
  - Réglages › Documents › « Changer de dossier… » → choisir `C:\Users\agath\Documents`.
  - Attendu : message « Documents rangés dans C:\Users\agath\Documents\Démo CIPL (N recopié(s)) », et l'entreprise est relue.
  - Dans Réglages › Documents : la pastille « Choisi à la main » et ce chemin ; « Ouvrir le dossier » montre `Batiment\…` et `reglage.json`.
  - Relancer le Play : le même dossier est gardé.
  - « Changer de dossier… » → choisir `Documents\CIPL` → retour dans `Documents\CIPL\Démo CIPL`, pastille « Dossier de CIPL ».

## AB. Étape 7 : envoi automatique des factures, réservé facture par facture (15/10) *(validé en Play le 16/10, AB1 à AB4)*

Préalable : `Serveur/etape7_supabase.sql` passé dans Supabase (SQL Editor › New query › Run), et le build **reconstruit**. Éditeur = compte 1 (Agathe), build = compte 2 (Odile).

Une seule fois avant AB1 : dans le Play, Réglages › Connexion & envoi → ressaisir le mot de passe SMTP (et la clé Pennylane), puis relancer le Play.
- Sur le cloud, ces secrets restent désormais sur le PC, hors du dossier des documents (décision du 15/10).
- Le build, sur le même PC, les reprend tout seul.

**Les e-mails partent pour de vrai.** Dans la fenêtre « N factures sont à envoyer », décoche toutes les lignes sauf ta facture de test.

Une facture de test, c'est :
- une facture « en attente d'envoi » dont la date d'envoi est atteinte (échéance dans moins de 15 jours) ;
- sur un locataire de test dont l'adresse e-mail est la tienne.

Prépare-en trois, une par cas ci-dessous.

Si la fenêtre ne s'ouvre pas, la console (ou le Player.log du build) dit pourquoi, sur une ligne `[EnvoiAuto]`. Par exemple, « déjà reporté aujourd'hui » si « Plus tard » a été cliqué ce jour-là sur ce PC.

- [x] **AB1 · Envoi simple** *(validé le 16/10)* :
  - Lancer le Play → la fenêtre s'ouvre (elle ne s'ouvrait plus sur le cloud depuis l'étape 2).
  - Ne garder que la facture de test → « Tout envoyer » → confirmer.
  - Attendu : message en bas « 1 facture(s) envoyée(s) », l'e-mail arrive, et la ligne du suivi passe à « Envoyé ».
  - Relancer : elle n'est plus proposée.
- [x] **AB2 · Déjà envoyée par une collègue** *(validé le 16/10)* :
  - Lancer l'éditeur puis le build : les deux proposent la 2e facture de test.
    - Si le build s'ouvre sur ton compte (il reprend la connexion gardée de l'éditeur) : « Plus tard » → Compte → se déconnecter → se connecter avec le compte d'Odile.
    - La liste s'ouvre alors pour Odile : « Plus tard » ne vaut que pour la personne qui l'a cliqué (16/10).
  - Éditeur : l'envoyer seule.
  - Build : « Tout envoyer » sur cette même facture.
  - Attendu : fenêtre au centre « 0 facture(s) envoyée(s), 1 sautée(s) : • <locataire> — déjà envoyée par Agathe ». **Un seul e-mail reçu.**
- [x] **AB3 · Fiche en cours de modification** *(validé le 16/10)* :
  - 3e facture de test, préparée le 16/10 par script : « Sephora · rivoli — Loyer Octobre 2026 » (`loyer-2026-P10`), remise de « Envoyé » à « en attente d'envoi ». Elle part à ton adresse.
    - Pour la refaire à la main, sans rien envoyer : Réglages › Connexion & envoi en mode Pennylane ; rivoli › Sephora › « Révision du dépôt », « Date d'échéance » = aujourd'hui → « Sauvegarder et envoyer » (« Aucun email envoyé… ») ; puis remettre le mode e-mail.
  - Build : ouvrir le locataire de la 3e facture de test → « Modifier » (sans enregistrer).
  - Éditeur : relancer le Play → « Tout envoyer » sur cette facture.
  - Attendu : « … — en cours de modification par Odile : elle repartira au prochain lancement ». Rien n'est reçu.
  - Build : annuler la modification. Éditeur : relancer → la facture est proposée de nouveau → l'envoyer → elle part.
- [x] **AB4 · Elle part dès que la fiche est libre** (décision du 16/10) *(validé le 16/10)* :
  - Préparation : remettre une facture de test « en attente d'envoi » (demande-le-moi, comme pour AB3), puis reconstruire le build.
  - Build : ouvrir sa fiche → « Modifier », sans enregistrer.
  - Éditeur : lancer le Play → « Tout envoyer » sur cette facture.
  - Attendu : « … — en cours de modification par Odile : elle partira dès que la fiche sera libre ». Rien n'est reçu.
  - Build : annuler la modification, **sans fermer l'éditeur**.
  - Attendu dans l'éditeur, dans les 10 s environ : message en bas « Facture de Sephora envoyée : sa fiche est libre. », l'e-mail arrive, et la ligne passe « Envoyé ».

## AC. Étape 8a : coupure de la connexion pendant une session (16/10) *(validé en Play le 16/10, AC1 à AC5)*

Préalable : le build est reconstruit. La coupure est **simulée dans l'éditeur seulement**, par moi (script) : le build, sur le même PC, reste en ligne et joue Odile. Dis-moi « coupe » et « rétablis » au bon moment.

- [x] **AC1 · Bandeau et lecture seule** *(validé le 16/10)* :
  - Éditeur en Play, accueil chargé → « coupe ».
  - Attendu dans les 20 s environ : bandeau rouge sous les onglets « Hors ligne — lecture seule. Données à jour au 16/10 à … », avec « Réessayer ».
  - rivoli → « Modifier » → refus au centre « Hors ligne — lecture seule : rien ne peut être modifié tant que la connexion n'est pas revenue. »
  - « Réessayer » → message en bas « Toujours hors ligne. »
- [x] **AC2 · Retour** *(validé le 16/10)* : « rétablis » → dans les 30 s (ou tout de suite avec « Réessayer ») : le bandeau disparaît, message « Connexion revenue. »
- [x] **AC3 · Saisie commencée avant la coupure** *(validé le 16/10)* :
  - En ligne : rivoli → « Modifier » → changer la surface du terrain (noter l'ancienne valeur) → « coupe ».
  - « Enregistrer » → message en bas « Hors ligne : « rivoli » sera enregistré au retour de la connexion. »
  - « rétablis » → « Connexion revenue. », puis la modification est enregistrée : le build la montre.
  - Remettre l'ancienne valeur.
- [x] **AC4 · Une collègue a modifié la fiche pendant la coupure** *(validé le 16/10, sur la fiche de Sephora : le verrou suit la fiche ouverte, bâtiment OU locataire ; il expire 2 min après le dernier signal de l'éditeur, qui part toutes les 30 s)* :
  - En ligne : éditeur, rivoli → « Modifier » → « coupe ». Attendre **2 min et demie** : le verrou de l'éditeur expire.
  - Build : rivoli → « Modifier » → changer la surface du terrain → « Enregistrer ».
  - Éditeur : changer la même valeur autrement → « Enregistrer » (« … sera enregistré au retour… ») → « rétablis ».
  - Attendu : message au centre, rivoli modifié ou repris par Odile pendant la coupure, vos saisies ne sont pas enregistrées. **L'écran de l'éditeur reste visible derrière** jusqu'à « OK ». Puis la fiche se recharge avec la valeur d'Odile.
  - Remettre l'ancienne valeur.
- [x] **AC5 · Modifier le bâtiment ne fait pas sauter le verrou d'un locataire** (demande du 16/10, en ligne, sans coupure) *(validé le 16/10)* :
  - Build : rivoli › Sephora → « Modifier » (ne pas enregistrer).
  - Éditeur : la ligne de Sephora porte « · Odile ». rivoli (le bâtiment) → « Modifier » → accepté → changer la surface du terrain → « Enregistrer ».
  - Attendu : enregistré sans message ; le build montre la nouvelle surface. Sephora reste en modification dans le build, et l'éditeur affiche toujours « · Odile » sur sa ligne.
  - Éditeur : Sephora → « Modifier » → refus « En cours de modification par Odile ». Le verrou d'Odile tient toujours.
  - Build : annuler. Éditeur : remettre l'ancienne surface.

## AD. Étape 8b : démarrer sans connexion, sur la dernière copie (16/10) *(validé en Play le 19/10, AD1 à AD3)*

Préalable : un Play en ligne (accueil chargé), puis l'arrêter, pour que la copie soit écrite. La coupure au démarrage est **simulée dans l'éditeur seulement** : je la pose avant ton Play (dis-moi « coupe au démarrage »), et je l'enlève ensuite (« rétablis »).

- [x] **AD1 · Ouverture sur la copie** :
  - Lancer le Play (coupure posée).
  - Attendu : pas d'écran de connexion. L'accueil de Démo CIPL s'ouvre avec ses 4 bâtiments, et le message en bas « Hors ligne : Démo CIPL est affichée telle qu'au 16/10 à … ».
  - Le bandeau rouge sous les onglets donne la même heure.
  - rivoli → « Modifier » → refus au centre « Hors ligne — lecture seule… ».
- [x] **AD2 · Retour de la connexion** :
  - « rétablis » → dans les 30 s : « Connexion revenue. », puis « Démo CIPL : 4 bâtiment(s) lu(s) depuis le cloud. »
  - Le bandeau disparaît, et « Modifier » marche de nouveau.
- [x] **AD3 · Se déconnecter efface la copie** :
  - Compte → « Se déconnecter » → se reconnecter.
  - Je vérifie qu'aucune copie n'est restée sur le PC (`AppData\LocalLow\DefaultCompany\CIPL_Test\cipl_copie_*.json`).
  - Une nouvelle copie est écrite à l'ouverture.

## AE. Étape 9a : Réglages et secrets partagés par le serveur (19/10) *(validé en Play le 19/10, AE1 à AE4)*

Préalable : `Serveur/etape9a_supabase.sql` passé dans Supabase (SQL Editor › New query › Run), et le build **reconstruit**. Éditeur = compte admin (Agathe), build = compte d'Odile (utilisatrice).

- [x] **AE1 · Mise en commun par l'admin** :
  - Éditeur : lancer le Play → message en bas « Vos Réglages sont maintenant ceux de toute l'entreprise. »
  - Supabase › Table Editor : `reglages` a une ligne pour Démo CIPL, et `secrets` en a deux (clé Pennylane, mot de passe SMTP).
- [x] **AE2 · Reçus par une utilisatrice** :
  - Build (Odile) → Réglages → « Connexion & envoi » : seulement « Envoi des factures : par e-mail (…). Réglé par l'administrateur. », sans champ Pennylane ni SMTP.
  - Les entêtes, RIB et listes de charges sont ceux de l'éditeur.
- [x] **AE3 · Diffusion tout de suite** (à l'ouverture, les Réglages du serveur sont pris sans message : ils sont à jour) :
  - Build : Réglages fermé.
  - Éditeur : Réglages → changer le « Bas de page » → Enregistrer.
  - Attendu dans le build : message en bas « Réglages mis à jour par Agathe. » Rouvrir Réglages : le nouveau bas de page y est.
  - Refaire avec Réglages ouvert dans le build : aucun message tant qu'il reste ouvert ; à sa fermeture, « Réglages mis à jour par Agathe. »
- [x] **AE4 · Deux enregistrements en même temps** :
  - Éditeur et build : ouvrir Réglages.
  - Éditeur : changer le bas de page → Enregistrer.
  - Build : changer le bas de page autrement → Enregistrer.
  - Attendu dans le build : message au centre « Réglages modifiés entre-temps par Agathe : vos modifications ne sont pas enregistrées. Les Réglages sont rechargés. » ; après « OK », la fenêtre montre le bas de page d'Agathe.
  - Remettre l'ancien bas de page.

## AF. Étape 9b : utilisateurs, invitations, droits (19/10) *(validé en Play le 19/10, AF1 à AF5)*

Préalable :
- `Serveur/etape9b_supabase.sql` passé dans Supabase.
- Dans Supabase, Authentication › Sign In / Providers : « Allow new users to sign up » activé, et dans « Email », « Confirm email » décoché.
- Le build **reconstruit**.
- Éditeur = Agathe (admin), build = Odile (utilisatrice). Une adresse e-mail de test que tu lis, différente des deux comptes (par exemple un alias Gmail `agathelimouzy+test@gmail.com`).

- [x] **AF1 · La section Utilisateurs (admin)** :
  - Éditeur : Réglages → en tête, « Utilisateurs », avec la pastille « 2 / 3 licences » et le bouton « Inviter ».
  - Agathe : « Admin (vous) », sans « Retirer ». Odile : « Utilisateur » (menu), « Retirer », et son statut « En ligne » si le build est ouvert.
  - Build (Odile) : Réglages → pas de section Utilisateurs. Logo, RIB (ajout, modification, suppression) et suppression d'une liste de charges absents.
- [x] **AF2 · Inviter** :
  - Éditeur : « Inviter » → ton adresse de test, rôle Utilisateur, Démo CIPL coché → « Envoyer l'invitation ».
  - Attendu : message « Invitation envoyée à … ». La ligne « Invitation envoyée · 7 j » apparaît, et la pastille passe à « 3 / 3 licences ».
  - L'e-mail arrive, avec un code « XXXX-XXXX » et la marche à suivre.
  - « Inviter » une 4e adresse → refus « 3 licences sur 3 utilisées… ».
- [x] **AF3 · Rejoindre avec le code** :
  - Build : Compte → Se déconnecter → « J'ai reçu une invitation » → l'adresse de test, le code, un mot de passe ×2 → « Rejoindre ».
  - Attendu : « Bienvenue dans Démo CIPL. », puis l'entreprise s'ouvre avec le nouveau compte, qui demande « Votre nom ».
  - Éditeur : Réglages › Utilisateurs → la personne apparaît comme membre, l'invitation a disparu.
  - Un mauvais code → « Ce code ne correspond à aucune invitation… ».
- [x] **AF4 · Rôle et dernier admin** :
  - Éditeur : passer le nouveau membre en Admin → message « … est maintenant admin. »
  - Le repasser en Utilisateur.
  - Ton propre rôle n'est pas modifiable (« Admin (vous) »).
- [x] **AF5 · Retirer** :
  - Build connecté avec le nouveau compte et Démo CIPL ouverte.
  - Éditeur : « Retirer » sur ce compte → confirmer → « … a été retiré(e) : la licence est libérée. »
  - Attendu dans le build, dans les 30 s : message au centre « Vous n'avez plus accès à « Démo CIPL »… », puis le choix d'entreprise, vide.
  - Le build se reconnecte ensuite avec le compte d'Odile.

## AG. Étape 9c1 : onglets de Réglages, abonnement, lecture seule à l'expiration (19/10) *(validé en Play le 19/10, AG1 à AG7)*

Préalable :
- `Serveur/etape9c1_supabase.sql` passé dans Supabase.
- **Juste après**, pour que Démo CIPL reste modifiable pendant le développement (son essai de 14 jours court depuis sa création), dans SQL Editor :
  `update public.groupes set statut = 'actif' where id = (select groupe_id from public.entreprises where id = '1e8848f1-23a4-470e-89a9-afce3e9b2aa4');`
- Le build **reconstruit**. Éditeur = Agathe (admin), build = Odile (utilisatrice).
- Dans la suite, « passer en X » veut dire lancer cette ligne avec la valeur indiquée, puis rouvrir l'entreprise (relancer le Play, et Compte › Changer d'entreprise › Démo CIPL dans le build) :
  `update public.groupes set statut = 'essai', fin_essai = current_date + 5 where id = (select groupe_id from public.entreprises where id = '1e8848f1-23a4-470e-89a9-afce3e9b2aa4');`

- [x] **AG1 · Onglets (admin)** :
  - Éditeur : Réglages → six onglets : Entreprise, Facturation, Utilisateurs, Abonnement, Documents, Intégrations.
  - Entreprise : le nom de l'entreprise. Facturation : Logo, RIB, Entêtes, Listes de charges, Textes fixes. Documents : Documents, Taille de l'affichage, Cartes (token Mapbox). Intégrations : Pennylane, mode d'envoi, SMTP.
  - Pas de bouton « Enregistrer » sur Utilisateurs ni sur Abonnement. Chaque onglet s'ouvre en haut de sa page.
- [x] **AG2 · Onglets (utilisateur)** :
  - Build (Odile) : trois onglets seulement, Entreprise, Facturation, Documents.
  - Entreprise : le nom est grisé (non modifiable), avec « Envoi des factures : … Réglé par l'administrateur. »
- [x] **AG3 · Essai** : passer en `statut = 'essai', fin_essai = current_date + 5`.
  - Éditeur : bandeau ambre sous les onglets des bâtiments, « Essai gratuit : 5 jours restants. », avec « Voir l'abonnement ».
  - « Voir l'abonnement » → Réglages s'ouvre sur l'onglet Abonnement : pastille « Essai gratuit · 5 jours restants », « Jusqu'au … », puis Démo CIPL, Licences 3 (2 utilisées), 40 €, 0 €, 45 €, Total « 85 € / mois · ou 850 € / an ».
  - Build (Odile) : le même bandeau, « Essai gratuit : 5 jours restants. Voyez avec agathe Limouzy (admin). », **sans bouton** ; toujours pas d'onglet Abonnement dans ses Réglages (retour du 19/10 : elle est informée, sans rien pouvoir modifier).
  - Variante : `fin_essai = current_date + 10` → plus de bandeau, ni chez toi ni chez Odile (seulement les 7 derniers jours).
- [x] **AG4 · Expiré** : passer en `statut = 'essai', fin_essai = current_date - 1`.
  - Éditeur : bandeau corail « Abonnement expiré : CIPL est en lecture seule. » avec « Voir l'abonnement » ; l'onglet Abonnement affiche « Expiré ».
  - « Modifier » sur une fiche → message au centre « Abonnement expiré — lecture seule : rien ne peut être modifié. Voyez Réglages › Abonnement. »
  - Une action en un clic (par exemple marquer une facture payée dans le suivi) → même message ; au OK, la fiche est relue et la case revient comme avant.
  - Réglages › Facturation › changer un texte fixe → Enregistrer → refus « … Les Réglages ne sont pas enregistrés. » ; le texte revient aussitôt à l'ancienne valeur, et pas de message « Réglages enregistrés » *(défaut du 19/10 : la modification restait)*.
  - Pas de fenêtre « N factures sont à envoyer » au lancement.
  - Build (Odile) : bandeau « … Voyez avec agathe Limouzy (admin). », sans bouton ; « Modifier » → même refus, qui nomme l'admin.
- [x] **AG5 · Expiration pendant la session** : éditeur et build ouverts en essai (`current_date + 5`), puis, sans rien rouvrir, passer en `current_date - 1`.
  - Build : « Modifier » sur une fiche → refus du serveur (« … abonnement expiré … »), et le bandeau d'expiration apparaît aussitôt.
- [x] **AG6 · Ajouter une entreprise** : repasser en `statut = 'actif'`, puis, dans l'éditeur, Réglages › Abonnement › « Ajouter une entreprise ».
  - Société, SIRET « 123 » → « Le SIRET compte 14 chiffres. » ; SIRET `73282932000075` → « Ce SIRET n'existe pas : vérifiez les chiffres. »
  - Après chaque refus, au clic sur OK, la fenêtre se rouvre avec le type, le SIRET et le nom déjà saisis *(retour du 19/10)*.
  - Même chose pour Réglages › Utilisateurs › « Inviter » : adresse « abc » (sans @) → « … adresse e-mail … », puis au OK la fenêtre se rouvre avec « abc », le rôle et les entreprises cochées.
  - Société, SIRET `732 829 320 00074`, nom « SCI Test 9c » → message en bas « « SCI Test 9c » ajoutée : ouvrez-la par Compte › Changer d'entreprise. » ; l'offre montre les deux entreprises, « Entreprises suivantes (1 × 28 €) 28 € », Total 113 €.
  - Le même SIRET une deuxième fois → « Cette société est déjà sur CIPL… ».
  - Particulier, nom laissé vide → « Patrimoine de agathe Limouzy » ajoutée.
  - Compte › Changer d'entreprise : les nouvelles entreprises sont listées, et s'ouvrent vides.
  - Nettoyage, dans SQL Editor : `delete from public.entreprises where nom in ('SCI Test 9c', 'Patrimoine de agathe Limouzy');`
- [x] **AG7 · Remise en état** : `update public.groupes set statut = 'actif' where id = (select groupe_id from public.entreprises where id = '1e8848f1-23a4-470e-89a9-afce3e9b2aa4');`

## AH. Étape 9c2 : abonnement payé avec Stripe (22/10) *(validé en Play le 23/10, AH1 à AH7)*

**Mise en place, une fois** (tout en mode test : aucun vrai paiement) :
1. **Stripe** : crée un compte sur stripe.com et reste en **mode test** (interrupteur en haut du tableau de bord).
   - Paramètres › Informations sur l'entreprise : le nom et l'adresse qui figureront sur les factures.
   - Paramètres › Moyens de paiement : active « Prélèvement SEPA » (la carte l'est déjà).
   - Paramètres › Facturation › Portail client : coche « Mettre à jour les moyens de paiement », « Historique des factures » et « Annuler les abonnements » (à la fin de la période de facturation), puis **Enregistrer** (sans cela, « Paiement et factures » est refusé en mode test).
   - Paramètres › Facturation › Gérer les échecs de paiement : 3 relances. Le choix « Annuler l'abonnement » si toutes échouent n'est **modifiable qu'en mode réel** (grisé en test, constaté le 22/10) : à régler au passage en réel, sinon un groupe qui ne paie plus resterait en « paiement refusé », modifiable, au lieu d'expirer.
   - Développeurs › Clés API : copie la **clé secrète** (`sk_test_…`). Ne la colle nulle part ailleurs qu'à l'étape 2.
2. **Supabase** › Edge Functions › **Secrets** : ajoute `STRIPE_SECRET_KEY` = la clé `sk_test_…`.
3. **Supabase** › Edge Functions › **Deploy a new function › Via Editor** :
   - nom `abonnement-stripe` : colle tout `Serveur/fonctions/abonnement-stripe.ts` à la place du modèle → Deploy. Laisse « Verify JWT » **activé**.
   - nom `stripe-webhook` : colle tout `Serveur/fonctions/stripe-webhook.ts` → Deploy, puis dans ses réglages, **désactive « Verify JWT »** (Enforce JWT verification) → Save.
   - Vérifie les deux noms exacts (l'éditeur en propose un au hasard) et remplace tout le modèle « Hello » par le code. Test : ouvrir `<Project URL>/functions/v1/stripe-webhook?retour=paye` dans le navigateur doit afficher « Paiement enregistré… » ; un `401 Missing authorization header` veut dire que « Verify JWT » est encore actif (constaté le 22/10 : désactiver, enregistrer, puis redéployer).
4. **Stripe** › Développeurs › Webhooks › Ajouter une destination :
   - adresse : celle de ton projet Supabase (Project Settings › API › Project URL) suivie de `/functions/v1/stripe-webhook` ;
   - événements : `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted` ;
   - copie le **secret de signature** (`whsec_…`) → Supabase › Edge Functions › Secrets : `STRIPE_WEBHOOK_SECRET`.
   - Contrôle : Stripe › la destination › Événements envoyés doit montrer **200**. Un **400 « No signatures found matching… »** veut dire que le secret n'est pas le bon (constaté le 22/10) : recopier la clé de CETTE destination, préfixe `whsec_` compris et sans espace, recréer le secret, redéployer `stripe-webhook`, puis « Renvoyer » les événements en échec.
5. `Serveur/etape9c2_supabase.sql` passé dans SQL Editor.
6. Le build **reconstruit**. Démo CIPL en essai : `update public.groupes set statut = 'essai', fin_essai = current_date + 5 where id = (select groupe_id from public.entreprises where id = '1e8848f1-23a4-470e-89a9-afce3e9b2aa4');`

Valeurs de test publiées par Stripe, à saisir sur la page Stripe (date d'expiration future, code quelconque) :
- carte acceptée `4242 4242 4242 4242` ; carte enregistrée mais refusée au prélèvement `4000 0000 0000 0341` ;
- IBAN SEPA accepté `FR1420041010050500013M02606`.

- [x] **AH1 · Choisir l'abonnement** : éditeur, Réglages › Abonnement.
  - « Choisir l'abonnement » : Mensuel choisi ; Licences 3, « 2 utilisées : 2 au minimum » ; « - » descend à 2, pas en dessous ; « + » remonte.
  - Avec 3 licences : « 85 € HT », TVA 20 % « 17 € », Total « 102 € TTC / mois ». Annuel : « 850 € HT », « 170 € », « 1020 € TTC / an ».
  - « Premier prélèvement le … , à la fin de l'essai. » : le lendemain de la fin de l'essai.
- [x] **AH2 · Payer** : Mensuel, 3 licences → « Continuer vers le paiement ».
  - Le navigateur ouvre la page Stripe (mode test) : CIPL — entreprise, CIPL — licence utilisateur × 3, TVA 20 %, rien à payer aujourd'hui (essai jusqu'au …).
  - CIPL affiche « Paiement en cours dans votre navigateur… » avec Actualiser, Rouvrir la page de paiement, Revenir au choix.
  - Carte `4242…` → S'abonner → page de texte « Paiement enregistré… ».
  - Dans CIPL, en quelques secondes : « Abonnement enregistré. », pastille « Actif · mensuel », « Prochaine échéance le … », Total « 85 € HT · 102 € TTC / mois ». Le bandeau d'essai disparaît.
  - Vérification : `select statut, formule, echeance, licences_max, stripe_abonnement from public.groupes;` → actif, mensuel, la date, 3, `sub_…`.
  - Variante : « Annuler » sur la page Stripe → page « Paiement annulé : rien n'a été prélevé… » ; dans CIPL, « Revenir au choix ».
- [x] **AH3 · Licences** : « + » → « Ajouter 1 licence : +15 € HT par mois, au prorata dès aujourd'hui. » → Ajouter → « Abonnement : 4 licences. » (Stripe › Abonnements : licence × 4).
  - « - » deux fois → 2 ; encore « - » → refus au centre « 2 licences utilisées : retirez quelqu'un… ». Remonter à 3.
- [x] **AH4 · Ajouter une entreprise (abonné)** : la fenêtre dit « votre abonnement augmente de 28 € HT par mois, au prorata dès aujourd'hui ».
  - Société, SIRET `732 829 320 00074`, « SCI Test 9c2 » → ajoutée ; Total « 113 € HT · 135,60 € TTC / mois » ; Stripe : ligne « CIPL — entreprise supplémentaire » × 1.
  - Nettoyage : `delete from public.entreprises where nom = 'SCI Test 9c2';`, puis « + » et « - » sur les licences (l'abonnement Stripe se recale sur le nombre d'entreprises).
- [x] **AH5 · Résilier et Paiement et factures** *(résilier depuis CIPL : retour du 22/10 ; recoller `abonnement-stripe.ts` dans Supabase avant)* :
  - « Résilier l'abonnement » (bouton corail) → « Votre abonnement reste actif jusqu'au … Ensuite, CIPL passe en lecture seule… » → Résilier → « Abonnement résilié : il prend fin à l'échéance. », pastille « Actif jusqu'au … · résilié », « Résilié : CIPL passe en lecture seule le … ».
  - Résilié, la lecture seule ne commence qu'à l'échéance (le mois payé court jusque-là). Bandeaux pour tous *(décision du 22/10)* :
    - dès la résiliation, bandeau vert clair « Abonnement résilié : CIPL passera en lecture seule le … », avec « Reprendre l'abonnement » chez toi ; chez Odile « … Voyez avec agathe Limouzy (admin). », sans bouton ;
    - les 7 derniers jours, bandeau ambre « Abonnement résilié : 3 jours restants avant la lecture seule (le …) ». Pour le voir sans attendre : `update public.groupes set echeance = current_date + 3 where id = (select groupe_id from public.entreprises where id = '1e8848f1-23a4-470e-89a9-afce3e9b2aa4');` puis rouvrir Démo CIPL (Stripe remettra la vraie échéance au prochain événement) ;
    - « Reprendre l'abonnement » du bandeau → fenêtre « Reprendre l'abonnement ? Votre abonnement continue après le … : le prochain prélèvement aura lieu à cette date… » *(retour du 22/10 : rien ne se reprend sans confirmer)* ; Annuler ne change rien ; Reprendre → « Abonnement repris. », le bandeau disparaît.
  - Le bouton de l'onglet devient « Reprendre l'abonnement » → « Abonnement repris. », « Actif · mensuel » à nouveau.
  - « Paiement et factures » ouvre le portail Stripe (moyen de paiement, factures) ; la résiliation y reste possible aussi, avec le même résultat dans CIPL après Actualiser.
- [x] **AH6 · Paiement refusé** *(validé le 23/10, voie complète : `--payment-behavior=allow_incomplete` est nécessaire, sinon Stripe annule la modification quand le paiement échoue)* (abonnement en cours, **pas** résilié ; s'il l'est, le reprendre d'abord) :
  - Voie simple (ce que fait CIPL) : `update public.groupes set statut = 'refuse' where id = (select groupe_id from public.entreprises where id = '1e8848f1-23a4-470e-89a9-afce3e9b2aa4');` — c'est ce qu'écrit le webhook quand Stripe signale un paiement refusé.
  - Voie complète (vrai refus Stripe) : dans le portail, remplacer la carte par `4000 0000 0000 0341` ; puis Stripe › Workbench › Shell : `stripe subscriptions update sub_… --billing-cycle-anchor=now --proration-behavior=none --payment-behavior=allow_incomplete` (facturation immédiate, qui échoue ; l'identifiant complet se lit par `stripe subscriptions list --limit 1`). *(Corrigé le 22/10 : « mettre fin à l'essai » ne marche que pour un abonnement souscrit pendant l'essai.)*
  - Rouvrir Démo CIPL : chez toi, bandeau orange « Le dernier paiement a été refusé. Mettez à jour votre moyen de paiement. » avec « Paiement et factures » ; pastille « Paiement refusé » ; CIPL reste modifiable.
  - Build (Odile) : pas de bandeau.
  - Remise en état : voie simple, la même ligne avec `statut = 'actif'` ; voie complète, remettre la carte `4242…` dans le portail, puis Stripe › la facture ouverte › Relancer le paiement → « Actif · mensuel », le bandeau disparaît (à la réouverture).
- [x] **AH7 · Fin** *(validé le 23/10 ; dans Stripe, un abonnement impayé n'est pas dans l'onglet « Actifs » : effacer le filtre)* : Stripe › l'abonnement › Annuler immédiatement → rouvrir Démo CIPL : bandeau corail d'expiration avec « Choisir l'abonnement » (l'onglet Abonnement repropose le choix), lecture seule.
  - Remise en état pour le développement : la ligne d'AG7 (`statut = 'actif'`).

## AI. Étape 10 : annuaire des entreprises (23/10) *(validé en Play le 23/10, AI1 à AI6)*

CIPL n'utilise pas l'API payante de Pappers mais l'annuaire public de l'État (recherche-entreprises.api.gouv.fr, sans clé) : la vérification se fait depuis CIPL, sans fonction serveur (décision du 23/10).

Préalable : `Serveur/etape10_supabase.sql` passé dans SQL Editor ; Démo CIPL en « actif » (ligne d'AG7) ; le build reconstruit.

- [x] **AI1 · Ajouter une entreprise, SIRET trouvé** : Réglages › Abonnement › Ajouter une entreprise › Société.
  - Saisir un vrai SIRET d'une société active (par exemple celui de ta SAS, `717 220 883 00044`, celui du bas de page de tes factures) : dès le 14e chiffre, « Recherche dans l'annuaire des entreprises… », puis une ligne « <forme> · <siège> », et le nom se remplit tout seul (modifiable).
  - Effacer un chiffre : la ligne disparaît. Le remettre : elle revient.
  - Ajouter → ajoutée. Nettoyage : `delete from public.entreprises where siret = '71722088300044';` (puis « + » et « - » sur les licences si un abonnement Stripe est en cours).
- [x] **AI2 · SIRET introuvable** : `000 000 000 00000` (clé valable, mais aucune société) → ligne en rouge « Ce SIRET est introuvable dans l'annuaire des entreprises : vérifiez les chiffres. » ; Ajouter → refus au centre, puis la fenêtre se rouvre avec ce qui était saisi.
- [x] **AI3 · Société fermée** (si tu connais le SIRET d'une société fermée, sinon le test EditMode le couvre) : ligne en rouge « « … » est fermée (cessée) : elle ne peut pas être ajoutée. » ; Ajouter → refus.
- [x] **AI4 · Tournée du jour** : rouvrir Démo CIPL dans l'éditeur, attendre 30 s. *(Le premier poste du jour la fait : si le build a ouvert Démo CIPL avant, la tournée est dans son journal, `AppData\LocalLow\DefaultCompany\CIPL_Test\Player.log`, constaté le 23/10 ; l'éditeur écrit alors « Tournée de l'annuaire : déjà faite aujourd'hui ». Pour la refaire dans l'éditeur : la ligne `annuaire_le = null` d'AI5.)*
  - Editor.log (ou la console) : « [Cloud] Tournée de l'annuaire des entreprises pour « Démo CIPL » », puis « [PappersSync] N locataire(s) interrogé(s), M résumé(s) mis à jour ».
  - `select nom, annuaire_le from public.entreprises;` → la date du jour pour Démo CIPL.
  - Un locataire avec SIRET : son commentaire porte le bloc « -- PAPPERS -- » à jour.
- [x] **AI5 · Une seule fois par jour** *(validé le 23/10)* : rouvrir Démo CIPL (dans le build, ou relancer le Play) → plus de tournée ce jour-là (pas de ligne « Tournée » dans le journal). Pour la relancer : `update public.entreprises set annuaire_le = null where id = '1e8848f1-23a4-470e-89a9-afce3e9b2aa4';`.
- [x] **AI6 · Fiche en cours de modification sautée** : remettre `annuaire_le = null` ; dans le build (Odile), « Modifier » sur un locataire qui a un SIRET et rester en saisie ; puis ouvrir Démo CIPL dans l'éditeur et attendre 30 s → la tournée passe, sans aucun message ; le locataire d'Odile n'est pas touché (sa saisie reste intacte), les autres sont rafraîchis.

## AJ. Licences par entreprise (23/10) *(validé en Play le 23/10, AJ1 à AJ6)*

Décision du 23/10 : chaque entreprise a son nombre de licences (par exemple Démo CIPL 3, SCI Rivoli 1) ; une personne occupe une licence dans chacune de ses entreprises. Rôle et facture restent ceux du groupe ; sur la facture Stripe, une ligne « licence » = le total.

Préalable :
- `Serveur/etape10b_supabase.sql` passé (après etape10).
- **Recoller** `Serveur/fonctions/abonnement-stripe.ts` et `stripe-webhook.ts` dans Supabase (Code › Deploy) ; vérifier que « Verify JWT » reste désactivé sur `stripe-webhook`.
- Le build reconstruit. Démo CIPL en « actif » sans abonnement Stripe (ligne d'AG7).
- Une 2e entreprise, **après** etape10b : Réglages › Abonnement › Ajouter une entreprise › Particulier, nom vide → « Patrimoine de agathe Limouzy » (1 licence, celle de l'admin qui l'ajoute). Ajoutée avant etape10b, elle reçoit 3 licences à la reprise : `update public.entreprises set licences_max = 1 where nom like 'Patrimoine de%';`
- Signe que etape10b n'est pas passé (constaté le 23/10) : « 0 utilisée » partout dans Réglages › Abonnement.

- [x] **AJ1 · Compteurs** : Réglages › Utilisateurs → en tête « Démo CIPL · 2 / 3 licences » (vert) et « Patrimoine de agathe Limouzy · 1 / 1 licence · pleine » (ambre).
- [x] **AJ2 · Inviter** : « Démo CIPL · 1 licence libre » (cochable), « Patrimoine … · aucune licence libre » (grisée, non cochable). Inviter `agathelimouzy+test3@gmail.com` sur Démo CIPL → « Démo CIPL · 3 / 3 licences · pleine ». Annuler l'invitation → 2 / 3.
- [x] **AJ3 · Entreprises d'Odile** (« Modifier… ») : Démo CIPL cochée « · sa licence » ; Patrimoine grisée « · aucune licence libre » ; « Décocher une entreprise y libère la licence. »
- [x] **AJ4 · Choisir l'abonnement** (Réglages › Abonnement, sans abonnement Stripe) :
  - une ligne par entreprise : « Démo CIPL · 1re entreprise · 40 € » avec « - 3 + » (2 utilisées : pas en dessous de 2) et 45 € ; « Patrimoine … · -30 % · 28 € » avec « - 1 + » et 15 € ;
  - Total HT 128 €, TVA 25,60 €, Total 153,60 € TTC / mois ;
  - les « - » / « + » ici ne changent que le choix : l'onglet Utilisateurs garde les licences en vigueur jusqu'au paiement (la phrase sous le total le dit, retour du 23/10) ;
  - mettre Démo CIPL à 2 → 113 € HT ; Continuer vers le paiement → la page Stripe montre « licence × 3 » ; payer (`4242…`) ;
  - après « Abonnement enregistré. » : Démo CIPL 2 licences, Patrimoine 1 (la répartition s'applique au paiement) ; Utilisateurs : « Démo CIPL · 2 / 2 licences · pleine ».
- [x] **AJ5 · « + » / « - » par entreprise** (abonné) :
  - « + » sur Patrimoine → « Ajouter 1 licence à Patrimoine de agathe Limouzy : +15 € HT par mois, au prorata dès aujourd'hui. » → Ajouter → « Patrimoine de agathe Limouzy : 2 licences. » ; Stripe : licence × 4 ; Utilisateurs : « Patrimoine … · 1 / 2 licences ».
  - « - » sur Démo CIPL (2 utilisées, 2 licences) → refus au centre « Démo CIPL : 2 licences utilisées. Retirez quelqu'un de Démo CIPL… ».
- [x] **AJ6 · Nettoyage** : Stripe › annuler l'abonnement immédiatement ; `delete from public.entreprises where nom like 'Patrimoine de%';` ; ligne d'AG7 (`statut = 'actif'`) ; remettre Démo CIPL à 3 licences : `update public.entreprises set licences_max = 3 where id = '1e8848f1-23a4-470e-89a9-afce3e9b2aa4';`

## AK. Étape 11 : exporter toutes les données (23/10) *(validé en Play le 23/10, AK1 à AK6)*

Décisions du 23/10 : l'admin exporte toujours, les autres seulement quand l'abonnement a expiré ; l'export porte sur l'entreprise ouverte ; c'est un dossier rangé comme une entreprise CIPL sur le PC ; les documents absents de ce PC sont téléchargés depuis le cloud. Ni les mots de passe ni la clé Pennylane ne partent.

Préalable :
- Rien à passer dans Supabase.
- Le build **reconstruit** (pour AK5). Démo CIPL en « actif » (ligne d'AG7).
- Un dossier vide pour les essais, par exemple `C:\Users\agath\Documents\Exports CIPL`.
- **AL1 et AL4 faits avant** (rangement des documents, retour du 23/10).

- [x] **AK1 · La section** : éditeur (Agathe, admin), Réglages › Documents → en bas, « Exporter toutes les données » : « Une copie complète de Démo CIPL sur ce PC : bâtiments, locataires, factures, baux, photos, charges, Réglages (sans les mots de passe ni la clé Pennylane), rangés comme dans CIPL. », et le bouton « Exporter… ». Pas encore de « Dernier export » ni de « Ouvrir le dossier ».
- [x] **AK2 · Exporter** : « Exporter… » → fenêtre « Où ranger l'export ? » → choisir `Exports CIPL`.
  - Pendant l'export : « Export de Démo CIPL en cours… », puis « Documents : 12 / 48 » qui avance.
  - À la fin : message en bas « Export terminé : 4 bâtiments, 17 locataires, 48 documents. » (vos nombres) ; la section affiche « Dernier export : 23/10/2026 à 16 h 42 — … », le chemin du dossier, et « Ouvrir le dossier » à côté de « Exporter… ».
  - Annuler la fenêtre de choix du dossier → rien ne se passe.
- [x] **AK3 · Le contenu** (rangé comme en AL, retour du 23/10) : « Ouvrir le dossier » → l'Explorateur ouvre `Démo CIPL — export du 23-10-2026`, avec :
  - `Batiment\rivoli\_Infos du bâtiment\rivoli.json` : la fiche du bâtiment ; `Batiment\rivoli\Sephora\Sephora.json` : celle du locataire, à côté de `Bail\` et `Factures\Loyers\`… ; plus de dossier `batiments\` ;
  - `reglage.json` à la racine : les Réglages (textes fixes, listes de charges…) ; y chercher le mot de passe SMTP ou la clé Pennylane → absents ;
  - `Anciens documents\` et `Logo\` ;
  - pas de `pennylane_secrets.dat`, ni de corbeille, ni d'`apercu.png`.
  - Ouvrir une facture de l'export : elle s'ouvre.
- [x] **AK4 · Un deuxième export le même jour** : « Exporter… » → même dossier → `Démo CIPL — export du 23-10-2026 (2)` ; le premier est intact. « Dernier export » montre le nouveau.
  - Supprimer à la main les deux dossiers d'export, rouvrir Réglages › Documents → « Dernier export » reste, mais « Ouvrir le dossier » a disparu.
- [x] **AK5 · Les autres utilisateurs** : build (Odile), Réglages › Documents → **pas** de section « Exporter toutes les données ».
  - Passer en `statut = 'essai', fin_essai = current_date - 1` (ligne du préalable d'AG), rouvrir Démo CIPL dans le build (Compte › Changer d'entreprise) → bandeau corail d'expiration ; Réglages › Documents → la section est là ; « Exporter… » marche comme en AK2, même en lecture seule.
  - Dans l'éditeur, relancer le Play : la section reste visible pour l'admin.
  - Repasser en `statut = 'actif'` (ligne d'AG7).
- [x] **AK6 · Hors ligne** : éditeur en Play, Réglages fermé → dis-moi « coupe » → rouvrir Réglages › Documents → « Export indisponible hors ligne : il relit toutes les fiches sur le cloud. », sans « Exporter… » (« Ouvrir le dossier » reste si le dossier existe). « rétablis » → en rouvrant Réglages, « Exporter… » revient.

## AL. Rangement des documents (23/10, à faire avant AK) *(validé en Play le 23/10, AL1 à AL7)*

Retour du 23/10 sur l'export : dossiers à identifiant mêlés aux dossiers nommés, documents du bâtiment mêlés aux locataires. Décisions du 23/10 :
- dans chaque bâtiment, `_Infos du bâtiment\` (Factures de charges, Objectifs, Photos), à côté des dossiers des locataires ;
- dans chaque locataire, `Bail\`, `Objectifs\` et `Factures\`, avec six sous-dossiers : Avoirs, Dépôt de garantie, Loyers, Quittances, Refacturations, Régularisations ;
- les dossiers à identifiant (bâtiments disparus) vont dans `Anciens documents\`, à côté de `Batiment\` ;
- les aperçus (`apercu*.png`) ne sont plus des documents.

Le rangement se fait seul, à l'ouverture de l'entreprise, sur chaque PC ; le cloud est corrigé au premier passage. Les fiches ne sont pas réécrites : un chemin enregistré avant mène au nouvel emplacement.

Préalable :
- Sauvegarde déjà faite (88 fichiers) : `C:\Users\agath\Documents\CIPL\Sauvegardes avant rangement 2026-10-23` (éditeur et build).
- Le build **reconstruit avant de l'ouvrir** : un ancien build verrait les documents rangés comme supprimés par un collègue, les mettrait dans sa corbeille et les retéléchargerait (rien de perdu, mais du désordre).

- [x] **AL1 · Rangement à l'ouverture** (éditeur) : lancer le Play.
  - Console : « [Documents] Rangement : 41 document(s) déplacé(s) ou retiré(s) », puis « [Documents] Rangement au cloud : … chemin(s) corrigé(s) ».
  - Pas de message « … supprimé(s) par un collègue, rangé(s) dans la corbeille ».
  - `Test_Save2\Démo CIPL\Batiment\rivoli\Sephora\` : `Bail\` (AOUT 2025, Avril 2025), `Factures\Loyers\` (8 loyers, dont les 5 corrigées), `Factures\Régularisations\` (RegularisationdeCharge-Sephora-2025) ; plus de `Facture\`, plus d'`apercu*.png` nulle part.
  - Même chose pour Auterive (Orge&Houblon, Tereva), Le Hangar (TEST Pennylane — Volteo : Loyers et Régularisations), Starbucks Coffee (Loyers).
  - `Test_Save2\Démo CIPL\Anciens documents\` : trois dossiers à identifiant (Loyer-Sephora 1er et 3e trimestre, Boulangerie Paul, Optic 2000, Basic-Fit) ; il n'en reste aucun dans `Batiment\`.
  - Les dossiers qui ne contenaient que des aperçus ont disparu (Parc Tertiaire Sud, Boutique Orange, nouveau locataire) : ils reviendront avec leur premier document.
- [x] **AL2 · Les anciennes factures s'ouvrent** : rivoli › Sephora › suivi de facturation → ouvrir « Loyer 3e Trimestre 2026 » et la régularisation 2025 → les PDF s'ouvrent (depuis `Factures\Loyers\` et `Factures\Régularisations\`).
- [x] **AL3 · Les nouveaux documents** :
  - Sephora › Facturer le loyer › « Générer facture » (aperçu seul) → l'aperçu s'affiche, et aucun `apercu.png` n'apparaît dans le dossier de Sephora.
  - rivoli › ajouter une photo → `rivoli\_Infos du bâtiment\Photos\`.
  - rivoli › Charges › ajouter une charge avec un PDF → `rivoli\_Infos du bâtiment\Factures de charges\`.
  - Un objectif du bâtiment rivoli avec un document → `rivoli\_Infos du bâtiment\Objectifs\`.
  - Une quittance de Sephora (si un loyer est payé) → `Sephora\Factures\Quittances\`.
- [x] **AL4 · Le build** (Odile, reconstruit) : ouvrir Démo CIPL → `C:\Users\agath\Documents\CIPL\Démo CIPL\` rangé de la même façon. `Player.log` : « [Documents] Rangement : … » et aucun document mis en corbeille.
- [x] **AL5 · Un message avec un chemin** (défaut du 23/10 : texte superposé) : ouvrir une facture de Sephora dans le lecteur PDF et la laisser ouverte, puis renommer Sephora → refus au centre « Renommage impossible (Access to the path 'C:\Users\…\Batiment\rivoli…\Sephora' is denied.)… » écrit sur des lignes distinctes, sans rien de superposé (`\r` de `\rivoli` n'est plus un retour au début de la ligne). Fermer le PDF, renommer → passe.
- [x] **AL6 · Un long message en bas** (défaut du 23/10 : il sortait de la bande) : renommer un locataire dans l'éditeur, puis attendre la synchronisation dans le build → « 1 document(s) reçu(s) de vos collègues, 14 supprimé(s) par un collègue, rangé(s) dans la corbeille. » sur deux lignes, dans la bande, qui grandit vers le haut. Un message court garde une seule ligne.
- [x] **AL7 · Revenir à un ancien nom** (défaut du 23/10 : les documents partaient en corbeille partout) :
  - Remise en place d'abord : lancer le Play de l'éditeur → Console « [Documents] 15 envoyé(s)… » environ (Sephora, Starbucks Coffee, Volteo) ; `Test_Save2\Démo CIPL\Batiment\rivoli\Sephora\` garde ses 11 documents (2 baux, 8 loyers, 1 régularisation). Build (reconstruit) : il les reçoit, et `Volteo2\` part dans sa corbeille.
  - Puis : renommer rivoli en « rivoli2 », attendre 5 s, le renommer en « rivoli », attendre 5 s → dans l'éditeur, Sephora garde ses documents (rien dans `corbeille_documents\`) ; chez Odile, « PDF » sur « Loyer 3e Trimestre 2026 » de Sephora → il s'ouvre.
  - Renommer puis arrêter le Play aussitôt (3 s plus tard) → chez Odile, la facture s'ouvre sous le nouveau nom.

## AM. PDF de factures et de charges après un renommage (23/10) *(validé en Play le 23/10, AM1 à AM5)*

Défaut trouvé pendant le rangement (AL) : renommer un bâtiment ou un locataire, ou céder un bail, déplace son dossier, mais les fiches gardent l'ancien chemin des PDF de factures et de charges, qui ne s'ouvraient plus et ne partaient plus à l'envoi automatique. Décision du 23/10 : les fiches ne sont pas réécrites (sur le cloud, chaque locataire a son verrou) ; le PDF est cherché sous les noms actuels.

Préalable :
- AL fait (documents rangés).
- DemoCIPL, bâtiment « TEST Nouveautés » : un locataire avec au moins une facture émise (bouton « PDF » dans son suivi) et une charge du bâtiment avec un PDF joint. Noter les noms d'origine, remis en AM5.
- Pour AM4 : une facture de ce locataire « En attente d'envoi » dont la date d'envoi est atteinte, une adresse email de test sur la fiche, l'email configuré (Réglages), et « Plus tard » pas encore choisi aujourd'hui.
  - Constaté le 23/10 : dans Démo CIPL, Sephora n'a aucune facture en attente d'envoi (rien n'est proposé). La seule utilisable est « TEST Pennylane — Volteo » (Le Hangar), « Loyer Septembre 2026 », à qui il ne manque que l'adresse : faire AM4 avec elle (adresse `agathelimouzy@gmail.com` sur sa fiche, puis renommer Volteo ou Le Hangar). La Console dit pourquoi rien n'est proposé : « [EnvoiAuto] Aucune facture à envoyer aujourd'hui. En attente — … ».

- [x] **AM1 · Renommer le bâtiment** : fiche du bâtiment › Modifier › « TEST Nouveautés 2 » › Sauvegarder → dans l'Explorateur, `Batiment\TEST Nouveautés 2\` a remplacé l'ancien dossier.
  - Fiche du locataire › suivi de facturation › « PDF » sur la facture émise → le PDF s'ouvre (depuis `TEST Nouveautés 2\<locataire>\Factures\…`).
- [x] **AM2 · Renommer le locataire** : fiche du locataire › Modifier › ajouter « 2 » au nom › Sauvegarder → son dossier est renommé ; « PDF » sur la même facture → elle s'ouvre encore.
- [x] **AM3 · La charge** : bâtiment › Charges › ouvrir la charge qui a un PDF → son nom de fichier est affiché (pas « Aucun fichier ») ; « Enregistrer » sans rien changer → aucun `…_1.pdf` n'apparaît dans `TEST Nouveautés 2\_Infos du bâtiment\Factures de charges\`.
- [x] **AM4 · L'envoi automatique** : relancer le Play → la fenêtre « Une facture est à envoyer » propose la facture du locataire renommé (et non « [EnvoiAuto] … PDF introuvable » dans la console) ; « Tout envoyer » → l'email part avec le PDF joint, la ligne passe « Envoyé ».
- [x] **AM5 · Sur le cloud** (Démo CIPL sur le cloud) : dans le build (Odile), une fois la synchronisation passée, « PDF » sur la même facture → elle s'ouvre (lue sous le nouveau nom, pas de « introuvable »). Puis remettre les noms d'origine du bâtiment et du locataire, et vérifier qu'une facture s'ouvre toujours.

## AN. Étape 12 : mot de passe oublié (23/10) *(validé en Play le 23/10, AN1 à AN8)*

Décisions du 23/10 : sur l'écran de connexion, « Mot de passe oublié ? » ; l'adresse, un code par e-mail (code de Supabase, pas de lien), puis le code et le nouveau mot de passe dans la même fenêtre (maquette validée telle quelle) ; une fois changé, connecté(e) directement ; la même fenêtre dans le menu Compte, « Changer mon mot de passe ».

Préalable, dans Supabase (une fois) :
0. **Serveur d'envoi** (constaté le 23/10 : sans lui, Supabase n'autorise plus à modifier ses modèles d'e-mail, et le modèle par défaut envoie un lien, pas le code). Bouton « Set up SMTP » (ou Authentication › Emails › SMTP Settings) › Enable custom SMTP, avec le compte Gmail qui envoie déjà les factures :
   - Sender email `agathelimouzy@gmail.com`, Sender name `CIPL` ;
   - Host `smtp.gmail.com`, Port `587`, Username `agathelimouzy@gmail.com` ;
   - Password : un **mot de passe d'application** Gmail (Compte Google › Sécurité › Mots de passe des applications ; celui de CIPL convient, ou en créer un « Supabase »), saisi par toi seulement ;
   - Save. Pour la vente, un expéditeur à l'adresse de CIPL (étape 17).
1. **Authentication › Emails › Templates › « Reset Password »** :
   - Subject : `Votre code CIPL`
   - Body (remplacer tout) :
     ```html
     <h2>Votre code CIPL</h2>
     <p>Bonjour,</p>
     <p>Voici votre code pour choisir un nouveau mot de passe dans CIPL :</p>
     <p style="font-size:26px;font-weight:bold;letter-spacing:4px">{{ .Token }}</p>
     <p>Il est valable 1 heure. Saisissez-le dans CIPL, dans la fenêtre « Mot de passe oublié » ou « Changer mon mot de passe ».</p>
     <p>Si vous n'avez rien demandé, ignorez ce message : votre mot de passe ne change pas.</p>
     ```
   - Save. Sans ce modèle, l'e-mail contient un lien au lieu du code.
2. **Authentication › Sign In / Providers › Email** : vérifier « Email OTP Expiration » = 3600 (1 heure) et « Email OTP Length » = 6.
3. Limite : avec ce serveur, Supabase envoie à toute adresse, 30 e-mails par heure au plus (Authentication › Rate Limits) ; Gmail, environ 500 par jour. Tester avec `agathelimouzy@gmail.com` ; le parcours ci-dessous n'en demande que deux.
- Aucun SQL à passer. Le build reconstruit (pour AN7).

- [x] **AN1 · Le lien** : éditeur, Compte › Se déconnecter → écran de connexion, adresse `agathelimouzy@gmail.com` saisie → « Mot de passe oublié ? » sous « Rester connecté(e) » → fenêtre « CIPL — Mot de passe oublié », l'adresse reprise. « Retour » → l'écran de connexion, l'adresse toujours là.
- [x] **AN2 · L'adresse** : vider l'adresse ou écrire « agathe » → « Envoyer le code » → refus au centre « Saisissez l'adresse e-mail de votre compte. » ; au OK, la fenêtre est toujours là.
- [x] **AN3 · Le code** (1er e-mail) : `agathelimouzy@gmail.com` → « Envoyer le code » → « Si un compte existe pour agathelimouzy@gmail.com, un code vient de partir. Il est valable 1 heure. » ; l'adresse se grise ; apparaissent Code, Nouveau mot de passe, Confirmer, « Renvoyer le code » ; le bouton devient « Changer le mot de passe ». E-mail reçu : « Votre code CIPL », 6 chiffres.
  - « Renvoyer le code » aussitôt → « Un code vient déjà de partir : attendez une minute avant d'en demander un autre. » (aucun e-mail).
- [x] **AN4 · Les refus** (la fenêtre garde ce qui est saisi) :
  - code « 12345 » → « Saisissez le code à 6 chiffres reçu par e-mail. » ;
  - bon code, mot de passe « court » → « … au moins 8 caractères. » ; deux mots de passe différents → « … pas identiques. » ;
  - code `000000` et un bon mot de passe ×2 → « Ce code est incorrect ou a expiré : vérifiez-le, ou demandez-en un nouveau. »
- [x] **AN5 · Changer** : le bon code, puis ton **mot de passe actuel** ×2 → « Choisissez un mot de passe différent de l'ancien. » ; puis un nouveau ×2 → « Mot de passe changé. » en bas, et CIPL passe directement à l'ouverture de Démo CIPL (sans redemander de code, sans se reconnecter).
  - Compte › Se déconnecter → l'ancien mot de passe → « E-mail ou mot de passe incorrect. » ; le nouveau → connecté(e).
- [x] **AN6 · Inconnu** : « Mot de passe oublié ? » avec `personne@exemple.fr` → même message « Si un compte existe… » (CIPL ne dit pas qui a un compte) ; aucun e-mail. « Retour ».
- [x] **AN7 · Depuis le menu Compte** (2e e-mail) : connecté(e), Compte › « Changer mon mot de passe » → fenêtre « CIPL — Changer mon mot de passe » par-dessus l'application, l'adresse du compte grisée → « Envoyer le code » → code + nouveau mot de passe → « Mot de passe changé. » ; Démo CIPL reste ouverte, les fiches se modifient toujours.
  - Relancer le Play → la connexion gardée est reprise (pas d'écran de connexion).
  - Le build (Odile, autre compte) n'est pas touché. Un autre PC connecté au **même** compte devra se reconnecter : Supabase ferme les autres sessions d'un compte dont le mot de passe change.
- [x] **AN8 · Nettoyage** : remettre ton mot de passe habituel (AN7 avec un nouveau code, ou plus tard).

## AO. Étape 13 : créer mon compte (23/10) *(validé en Play le 25/10, AO1 à AO8)*

Décisions du 23/10 : maquette validée telle quelle (« Créer mon compte » sur l'écran de connexion ; Société ou Particulier ; e-mail et mot de passe ; le code reçu par e-mail dans la même fenêtre) ; l'adresse prouvée par un code à 6 chiffres ; **un seul groupe par adresse e-mail** (une adresse déjà membre d'un groupe, même invitée, n'en crée pas d'autre ; une autre adresse le peut, par exemple celle de son patrimoine personnel) ; les Réglages de la nouvelle entreprise pré-remplis avec ce qui est saisi (plus ceux de CIPL).

Préalable :
- `Serveur/etape13_supabase.sql` passé dans Supabase (après etape10b).
- Supabase › Authentication › Emails › Templates : **« Magic Link » et « Confirm signup »** (les deux : Supabase choisit selon que le compte existe déjà), avec le serveur d'envoi Gmail de l'étape 12 :
  - Subject : `Votre code CIPL`
  - Body (remplacer tout) :
    ```html
    <h2>Votre code CIPL</h2>
    <p>Bonjour,</p>
    <p>Voici votre code pour créer votre compte CIPL :</p>
    <p style="font-size:26px;font-weight:bold;letter-spacing:4px">{{ .Token }}</p>
    <p>Il est valable 1 heure. Saisissez-le dans CIPL, dans la fenêtre « Créer mon compte ».</p>
    <p>Si vous n'avez rien demandé, ignorez ce message.</p>
    ```
- Authentication › Sign In / Providers › Email : « Allow new users to sign up » activé ; « Confirm email » reste **décoché** (étape 9b).
- Adresses de test : `agathelimouzy+societe@gmail.com`, `agathelimouzy+particulier@gmail.com`, `agathelimouzy+doublon@gmail.com` (Gmail les livre dans ta boîte ; le « + » est ignoré). Le parcours envoie 4 codes.
- Éditeur ; dans CIPL, Compte › Se déconnecter avant chaque cas.

- [x] **AO1 · La porte d'entrée** : écran de connexion → trois boutons « J'ai reçu une invitation », « Créer mon compte », « Se connecter ». « Créer mon compte » → fenêtre « CIPL — Créer mon compte », « Société » choisi, « Essai gratuit de 14 jours, sans carte. ». « Retour » → la connexion, l'adresse saisie toujours là.
- [x] **AO2 · Société et annuaire** : SIRET `528 808 678 00023` (« SCI (JBC) », Meudon, active dans l'annuaire le 25/10 ; `732 829 320 00074`, donné d'abord, n'y existe pas) → au 14e chiffre, « Recherche dans l'annuaire… », puis la forme et le siège ; « Nom de l'entreprise » se remplit avec la raison sociale (modifiable).
  - SIRET « 123 » puis « Recevoir le code » → « Le SIRET compte 14 chiffres. » ; au OK, la fenêtre est toujours là.
- [x] **AO3 · Le formulaire** (la fenêtre garde tout ; retour du 25/10 : la zone en cause passe en rouge, bordure et libellé, et redevient normale dès qu'on la modifie — mots de passe différents : les deux champs ; SIRET, adresse e-mail, code : le champ concerné ; même chose dans la connexion, l'invitation et le mot de passe oublié) : « Votre nom » vide → « Saisissez votre nom : il est affiché à vos collègues. » ; adresse « agathe » → « Saisissez l'adresse e-mail de votre compte. » ; mots de passe différents → « … pas identiques. »
- [x] **AO4 · Le code** (1er e-mail) : SIRET d'AO2, ton nom, `agathelimouzy+societe@gmail.com`, un mot de passe ×2 → « Recevoir le code » → « Un code vient de partir à … Il est valable 1 heure. » ; l'adresse se grise ; « Code reçu par e-mail » et « Renvoyer le code » apparaissent ; le bouton devient « Créer mon compte ». E-mail reçu : « Votre code CIPL ».
  - Code `000000` → « Ce code est incorrect ou a expiré… ».
- [x] **AO5 · Le compte de la société** : le bon code → « Créer mon compte » → message en bas « Bienvenue sur CIPL : « … » est créée, essai gratuit de 14 jours. », et la nouvelle entreprise s'ouvre, vide, sans demander « Votre nom ».
  - Réglages › Abonnement : « Essai gratuit · 14 jours restants », l'entreprise avec 3 licences (1 utilisée).
  - Réglages › Facturation › Textes fixes : bas de page « SCI (JBC) - … - Siret : 528 808 678 00023 », puis « Siège social : 3 B RUE JEAN BRUNET 92190 MEUDON » ; lieu d'émission : « Meudon » (plus « St Marcel Paulel » ni le SIRET de CIPL).
  - Réglages › Facturation › Logo (retour du 25/10) : aperçu vide, « Aucun logo : cliquez sur « Choisir un logo… ». » ; le bouton s'appelle « Retirer le logo » (plus « Logo CIPL par défaut ») ; l'aperçu d'une facture n'a pas de logo en haut. Lieu d'émission : un nom de ville (« Meudon »), jamais un nombre (le code INSEE « 92048 » sortait avant la correction).
  - Réglages › Utilisateurs : toi seul(e), admin, avec ton nom.
  - Compte › Se déconnecter, puis se reconnecter avec cette adresse et ce mot de passe → la même entreprise.
- [x] **AO6 · Particulier** (2e e-mail) : « Créer mon compte » › « Particulier » → Prénom et Nom (« Jean », « Martin ») ; « Nom de l'entreprise » propose « Patrimoine de Jean Martin ».
  - Adresse « 4 rue des Lilas » → « Adresse : terminez par le code postal et la ville… » ; puis « 4 rue des Lilas, 31000 Toulouse ».
  - `agathelimouzy+particulier@gmail.com`, code → « Patrimoine de Jean Martin » s'ouvre. Réglages : bas de page « Jean Martin - 4 rue des Lilas, 31000 Toulouse », lieu d'émission « Toulouse ».
- [x] **AO7 · Un seul groupe par adresse** (retour du 25/10 : refus **avant** l'envoi du code ; il faut le SQL d'etape13 repassé, qui ajoute `adresse_deja_membre`) : « Créer mon compte » avec `agathelimouzy@gmail.com`, un SIRET libre, un mot de passe → « Recevoir le code » → aucun code envoyé ; refus au centre « Cette adresse e-mail a déjà un compte CIPL : connectez-vous avec… » (sans nommer le groupe), le champ « E-mail » en rouge. Puis « Retour » et « Se connecter » avec ton mot de passe habituel → Démo CIPL.
  - Ancien déroulé, gardé comme filet de sécurité si le serveur ne répond pas à la vérification (3e e-mail) : « Créer mon compte » avec `agathelimouzy@gmail.com` (déjà admin de Démo CIPL), un SIRET libre (`106 463 284 00013`, « SCI (OLTRA CAPITAL) ») et un mot de passe **différent** du tien → code → refus « Cette adresse e-mail a déjà un compte CIPL (« … ») : connectez-vous avec… » ; « Retour », puis « Se connecter » avec **ton mot de passe habituel** → Démo CIPL (il n'a pas changé).
- [x] **AO8 · Société déjà sur CIPL** (retour du 25/10 : refus **avant** le code ; SQL d'etape13 repassé, `siret_deja_sur_cipl`) : « Créer mon compte » › Société, le SIRET d'AO5, `agathelimouzy+doublon@gmail.com`, un mot de passe → « Recevoir le code » → aucun code envoyé ; « Cette société est déjà sur CIPL : demandez une invitation à son administrateur. », le champ « SIRET » en rouge.
- [ ] **AO9 · Nettoyage** (SQL Editor) :
  ```sql
  delete from public.groupes where titulaire in (select id from auth.users where email in
    ('agathelimouzy+societe@gmail.com', 'agathelimouzy+particulier@gmail.com', 'agathelimouzy+doublon@gmail.com'));
  delete from auth.users where email in
    ('agathelimouzy+societe@gmail.com', 'agathelimouzy+particulier@gmail.com', 'agathelimouzy+doublon@gmail.com');
  ```
  Puis, dans CIPL, se reconnecter avec ton compte habituel.

## AP. Étapes 13b et 13b-2 : premiers pas accompagnés (25/10) *(validé en Play le 25/10, AP1 à AP10 ; AP8 jusqu'à « Le paiement »)*

Décisions du 25/10 : la carte « Premiers pas » pour tout le monde (l'admin : 8 lignes ; un collègue : les 3 qu'il peut faire) ; « Masquer » et « visite déjà vue » retenus **pour la personne** (dans son compte, donc sur tous ses PC) ; la visite montrée une fois à chaque nouvelle personne. Retour du 25/10 (« plus explicatif, interactif, accompagné »), maquettes 13b-2 validées : la visite (5 bulles, « Précédent » et « Suivant ») finit par « C'est parti » qui lance le guide de la première étape ; la carte met la prochaine étape en avant et dit à quoi sert chaque ligne et combien de temps elle prend ; « Commencer » ouvre l'écran et, pour les lignes réglées dans Réglages (logo, RIB, envoi, inviter, abonnement), un guide pas à pas : la zone éclairée reste utilisable, la bulle avance d'elle-même quand l'étape est faite ; à la fin, « Vos RIB sont prêts » propose la suivante. Les guides bâtiment, locataire, facture et le bâtiment d'exemple viendront en 13b-3.

Préalable :
- Rien à passer sur le serveur (pas de SQL).
- Éditeur. Fais AP **avant** le nettoyage AO9 : on se sert du compte de la société d'AO5 (`agathelimouzy+societe@gmail.com`, « SCI (JBC) », vide).
- Ne clique jamais « Tout envoyer ».
- Pour revoir la carte masquée ou la visite automatique sur un compte (SQL Editor), puis relancer le Play :
  ```sql
  update auth.users set raw_user_meta_data = raw_user_meta_data - 'cipl_visite_vue' - 'cipl_premiers_pas_masques'
    where email = 'agathelimouzy+societe@gmail.com';
  ```

- **Repartir de zéro** (retour du 25/10 : des lignes cochées sans avoir été faites par le tutoriel) :
  1. Supabase › SQL Editor : le nettoyage d'AO9 (il efface les trois comptes de test, SCI (JBC) et tout ce qu'elle contient : bâtiments, Réglages, secrets).
  2. Dans l'Explorateur, renommer `C:\Users\agath\Documents\CIPL\SCI (JBC)` en `SCI (JBC) ancien` (sinon le logo et les PDF des essais repartiraient dans la nouvelle entreprise).
  3. Play → l'écran de connexion (le compte gardé n'existe plus) → « Créer mon compte » › Société, SIRET `528 808 678 00023`, `agathelimouzy+societe@gmail.com`, un mot de passe, le code reçu → la nouvelle SCI (JBC) s'ouvre : la visite démarre (AP1), carte « 0 / 8 », envoi par e-mail, serveur d'envoi vide.

- [x] **AP1 · La visite** : le SQL ci-dessus, puis Play avec `+societe` → SCI (JBC) s'ouvre ; une fois l'accueil seul à l'écran, l'écran s'assombrit et la bulle « Bienvenue dans CIPL » apparaît au centre, « 1 / 5 ». « Suivant › » → « Vos bâtiments », cadre blanc autour du « + » de la barre des onglets (« 2 / 5 ») ; « ‹ Précédent » revient à la bienvenue. Puis « Créances » (la section), « Réglages et compte » (les boutons de l'en-tête) et « C'est parti » avec « Plus tard » et « Commencer › ». Pendant la visite, rien ne se clique sous le voile ; aucun « Compte » coupé avant son « » ».
  - « Plus tard » → l'accueil, avec la carte (retour du 25/10 : juste après la création du compte, elle n'était apparue qu'après une relance ; si ça se reproduit, me le dire). Relancer le Play → la visite ne revient pas.
- [x] **AP2 · La carte** : sous l'en-tête, « Premiers pas · 0 / 8 » et « Masquer » ; la barre de progression ; le cadre vert « Prochaine étape · 1 min », « Votre logo », sa phrase (« Votre logo s'imprime en haut de chaque facture… ») et « Commencer › ». Puis les 8 lignes sur deux colonnes, chacune avec à quoi elle sert et sa durée (« Vos RIB — Imprimés sur les factures · 2 min »…, « Choisir l'abonnement — Essai : 14 jours restants ») et « Commencer » ; une ligne faite est barrée et garde « Refaire » (retour du 25/10), qui relance son guide (un logo, un RIB, un bâtiment… de plus).
- [x] **AP3 · Le guide du logo** : Compte › « Revoir la visite guidée » → jusqu'à « C'est parti » → « Commencer › » → Réglage › Facturation, bulle « Votre logo · étape 1 sur 2 » sur « Choisir un logo… », avec « La suite s'affiche dès que le logo est choisi. » et « Quitter le guide ». Choisir une image PNG → la bulle passe d'elle-même à « Le bas de page et le lieu » ; l'écran défile jusqu'à « Textes fixes », cadré de blanc. « Terminer » → bulle « Votre logo est en place · 1 étape sur 8. Prochaine étape : Vos RIB (Imprimés sur les factures · 2 min). », « Plus tard » et « Continuer › ».
- [x] **AP4 · Le guide des RIB** (retour du 25/10 : une bulle pour toute la fenêtre, et la saisie contrôlée) : si un RIB est déjà dans la liste (« (RIB sans nom) » de ton premier essai), le supprimer d'abord, pour que « Vos RIB » ne soit pas barré. « Continuer › » (ou « Commencer » de la ligne) → bulle sur « + Ajouter un RIB » ; le cliquer → la fenêtre « Nouveau RIB » s'ouvre et une seule bulle, à droite, liste les six champs et ce qu'il faut y mettre, avec « La suite s'affiche dès que le RIB est validé. »
  - Refus, sans rien enregistrer : laisser « Titulaire » vide, IBAN `FR76 3000 6000 0112 3456 7890 189`, « Valider » → au centre « Saisissez le titulaire du compte : il s'imprime sur les factures. » ; au OK, la fenêtre revient avec l'IBAN, le RIB rempli d'après l'IBAN (`30006 00001 12345678901 89`) et « Titulaire » en rouge (bordure et libellé), qui redevient normal dès qu'on y tape.
  - IBAN terminé par `188` au lieu de `189` → « Cet IBAN n'est pas valide… », IBAN en rouge. BIC `AGRI` → « Le BIC compte 8 ou 11 lettres et chiffres… ». RIB `30004 00747 00021009833 38` avec l'IBAN de test → « Le n° de RIB ne correspond pas à l'IBAN… », RIB et IBAN en rouge.
  - Puis le RIB de test en entier : nom `Banque test`, titulaire `SCI (JBC)`, domiciliation `Agence test Meudon`, RIB vide (il se remplit), IBAN `FR76 3000 6000 0112 3456 7890 189`, BIC `AGRIFRPP` → « Valider » → « Vos RIB sont prêts · 2 étapes sur 8. Prochaine étape : L'envoi des factures (E-mail ou Pennylane · 3 min). » → « Plus tard ».
  - « ← Retour » → l'accueil : « 2 / 8 », logo et RIB barrés, « Prochaine étape · 3 min — L'envoi des factures ».
- [x] **AP5 · Le guide de l'envoi (e-mail)** (retour du 25/10 : la bulle « Enregistrer » dit ce qui manque ; une entreprise créée par « Créer mon compte » démarre en e-mail, serveur vide) : « Commencer › » du cadre → Réglage › Intégrations, bulle sur la case « Envoyer via Pennylane » : « Case cochée : … Pennylane (il faudra sa clé). Case décochée : … par e-mail… ». Sur SCI (JBC), créée avant ce changement, la case est encore cochée : la décocher → « Suivant › » → « Votre serveur d'envoi » (`smtp.gmail.com`, pas `smtp.office365.com`) → « L'adresse d'expédition » (`agathelimouzy@gmail.com`) → « Le mot de passe » (ton mot de passe d'application Gmail, saisi par toi) → « Enregistrer ».
  - Retour du 25/10 : « Votre serveur d'envoi », « L'adresse d'expédition » et « Le mot de passe » n'ont plus de « Suivant » quand le champ est vide : « Suivant › » apparaît dès que le champ est rempli, même en cours de frappe (retour du 25/10) ; le quitter rempli fait aussi avancer la bulle. Sur « Enregistrer », la bulle se met à droite et ne cache plus les champs. Un champ déjà rempli (l'adresse d'expédition, ou en revenant par « ‹ Précédent ») ne repart plus seul : la bulle propose « Suivant » ; si on modifie le champ puis qu'on clique ailleurs, elle avance.
  - Avant d'avoir tout rempli, « Enregistrer » → la bulle reste et ajoute sous « La suite s'affiche… » ce qui manque (par exemple « Le mot de passe SMTP n'est pas enregistré… »). Complet et enregistré → « Un e-mail de test » ; le cliquer → « Envoyé à … » sous le bouton (s'il est refusé : « 535 Authentication unsuccessful » = serveur ou mot de passe faux) → « Terminer » → « L'envoi des factures est réglé ».
  - Variante, sans enregistrer : relancer le guide (ligne de la carte, si elle n'est pas encore barrée), cocher « Envoyer via Pennylane », « Suivant › » → « La clé Pennylane » (les étapes de l'e-mail sont sautées) → « Suivant › » → « Enregistrer » sans clé : la bulle ajoute « Il manque la clé Pennylane : collez-la plus haut et enregistrez, ou décochez « Envoyer via Pennylane »… ». Décocher, « Quitter le guide », « ← Retour » sans enregistrer.
  - Pour refaire ce guide une fois la ligne barrée : effacer le mot de passe dans Intégrations et « Enregistrer ».
- [x] **AP6 · Le guide de l'invitation** : « Commencer » de « Inviter un collègue » → Réglage › Utilisateurs, bulle sur « Inviter » → le cliquer → la bulle cadre la fenêtre « Inviter un collègue » → « Suivant › » → bulle sur « Envoyer l'invitation » ; adresse `agathelimouzy+doublon@gmail.com`, rôle Utilisateur, puis l'envoyer (garder le code pour AP7) → « Invitation envoyée », prochaine étape : « Un premier bâtiment ».
- [x] **AP6b · Le guide du locataire** (13b-3, 25/10 : les quatre étapes du parcours de la fiche — général, bail, loyer, dépôt — sans lesquelles la facturation reste bloquée) : sur la carte, « Commencer » d'« Un locataire » → le résumé du premier bâtiment (« Nouveau »), bulle « Un locataire · étape 1 sur 7 » et cadre blanc sur « + Ajouter ». Le cliquer → la fiche du locataire s'ouvre, la bulle passe sur le nom.
  - Nom (`Boulangerie Test`), puis cliquer ailleurs → « Son e-mail » (`agathelimouzy@gmail.com`) → « Suivant › » → « Le bail » (type, début `01/10/2026`, fin `30/09/2035`) → « Suivant › » → « Sauvegarder » sur le bouton de la fiche ; le cliquer → « Le loyer », cadre sur « Initialiser » dans « Loyer ».
  - Si le bail n'a pas été enregistré, la bulle ajoute « Étape 2 — Bail : saisissez les dates… » (le message du parcours).
  - « Initialiser » → la fenêtre du loyer s'ouvre, la bulle descend en bas sans cadre ; un loyer (`1 200` € par mois), « Enregistrer » → « Le dépôt de garantie », cadre sur « Initialiser » ; le cliquer → la fenêtre du dépôt est cadrée ; la remplir et la valider → « Votre locataire est prêt », prochaine étape : « Une première facture ».
- [x] **AP6c · Le guide de la facture** (retour du 25/10 : la facture du tutoriel est émise sans être envoyée) : « Continuer › » (ou « Commencer » d'« Une première facture ») → la facture de loyer du locataire s'ouvre, le bouton du bas dit « Émettre (sans envoyer) ». Bulle « La période » → « Suivant › » → « L'aperçu » sur « Générer facture » ; le cliquer → l'aperçu à droite → « Suivant › » → « Émettre » sur le bouton du bas ; le cliquer → aucune fenêtre d'envoi, message en bas « … Émise sans envoi (premiers pas) : elle attend dans le suivi de facturation. » → « Votre première facture est émise ».
  - Même avec un e-mail mal réglé (serveur faux), rien ne bloque : rien ne part.
  - Après le guide (« Terminer » ou « Quitter le guide »), rouvrir la facture d'un locataire : le bouton redit « Sauvegarder et envoyer » (ou « Enregistrer pour l'envoi du … »).
  - Dans le suivi de facturation du locataire, la facture est « en attente d'envoi » : elle s'envoie de là (jamais « Tout envoyer » pendant les tests).
  - Sans locataire complet : « Commencer » d'« Une première facture » ouvre sa fiche et affiche au centre « Facturation possible une fois le locataire complet… » (pas de guide). Sans locataire du tout : c'est le guide du locataire qui démarre ; sans bâtiment, celui du bâtiment.
- [x] **AP6d · Le guide du bâtiment** (à faire sur une autre entreprise vide, ou après avoir supprimé « Nouveau ») : « Commencer » d'« Un premier bâtiment » → l'accueil, cadre sur le « + » de la barre des onglets (plus de bâtiment créé d'office) ; le cliquer → la fiche « Nouveau » en modification, bulle sur le nom ; nom puis cliquer ailleurs → « Son adresse » (le champ et « Chercher ») ; adresse puis cliquer ailleurs → « Sauvegarder » ; le cliquer → « Votre premier bâtiment est créé ».
- [x] **AP7 · Un collègue** : Compte › Se déconnecter ; « J'ai reçu une invitation » avec `agathelimouzy+doublon@gmail.com`, le code et un mot de passe → SCI (JBC) s'ouvre et la visite démarre (nouvelle personne), jusqu'à « C'est parti ». La carte : « Premiers pas · 0 / 3 », « Prochaine étape · 2 min — Un premier bâtiment », et seulement bâtiment, locataire, facture. « Commencer › » → la création d'un bâtiment (pas encore de guide : étape 13b-3) ; ne rien enregistrer, revenir à l'accueil.
- [x] **AP8 · Le guide de l'abonnement** : se reconnecter avec `+societe` (pas de visite). « Commencer » de « Choisir l'abonnement » → Réglage › Abonnement : bulles « Mensuel ou annuel », « Les licences », puis « Le paiement » sur « Continuer vers le paiement » (« La suite s'affiche dès que le paiement est validé. »). Tu peux t'arrêter là (« Quitter le guide »), ou payer en mode test comme en AH (carte de test Stripe, saisie par toi) → « Abonnement choisi ».
  - Retour du 25/10 : s'arrêter sur « Le paiement » suffit (pas de carte à saisir) ; le passage à « Abonnement choisi » ne dépend que de l'abonnement enregistré par Stripe, déjà validé en AH.
- [x] **AP9 · Masquer, pour la personne** : « Masquer » → la carte disparaît. Relancer le Play → toujours absente ; Se déconnecter puis se reconnecter → toujours absente.
  - Retour du 25/10 : menu Compte → « Réafficher les premiers pas » (le lien n'y est que si la carte est masquée) → la carte revient, et reste là après une relance.
- [x] **AP10 · Ton compte habituel** : se reconnecter avec `agathelimouzy@gmail.com` → Démo CIPL. Si la proposition d'envoi des factures s'affiche, elle passe d'abord (« Plus tard ») ; puis la visite, une fois. La carte ne montre que ce qui reste à faire ; si tout est fait, ni carte ni bulle « C'est parti ».
- [ ] **AP11 · Nettoyage** : AO9 (`agathelimouzy+doublon@gmail.com` y est déjà ; le logo et les RIB de SCI (JBC) partent avec elle).

## AQ. Étape 13b-3 : le bâtiment d'exemple (25/10) *(validé en Play le 25/10, AQ1 à AQ4)*

Décisions (maquette 5 validée le 25/10) : « Essayer avec un exemple » sur la carte Premiers pas crée un bâtiment fictif, « Exemple · Les Tilleuls », avec un locataire complet (« Exemple Boulangerie », bail depuis le 1er de ce mois, 1 200 € HT par mois, dépôt initialisé, sans e-mail) ; rien n'en part (facture de loyer émise sans envoi, hors de l'envoi automatique) ; il ne compte pas dans les premiers pas ; un bandeau le signale, avec « Supprimer l'exemple ».

Préalable : Play avec `agathelimouzy+societe@gmail.com` (SCI (JBC)), pas ton compte habituel : l'exemple serait créé dans Démo CIPL.

- [x] **AQ1 · Créer l'exemple** : accueil, bandeau de la carte « Premiers pas » → « Essayer avec un exemple » → message en bas « Bâtiment d'exemple créé : essayez tout, rien n'est envoyé ni déposé chez Pennylane. » ; l'onglet « Exemple · Les Tilleuls » s'ouvre sur son résumé, avec en tête un bandeau orange « Bâtiment d'exemple : … » et « Supprimer l'exemple » ; dessous, la carte du bâtiment, les chiffres, puis « À traiter » et « Locataires » (« Exemple Boulangerie »), dans cet ordre.
- [x] **AQ2 · Il ne compte pas** : « Accueil » → la carte « Premiers pas » affiche le même compte qu'avant (l'exemple ne coche ni « Un premier bâtiment » ni « Un locataire ») ; le lien dit maintenant « Voir l'exemple » ; le cliquer → l'exemple se rouvre, sans en créer un second.
- [x] **AQ3 · Facturer l'exemple** : fiche d'« Exemple Boulangerie » → facture de loyer → le bouton du bas dit « Émettre (sans envoyer) » ; « Générer facture », puis ce bouton → pas de fenêtre d'envoi, message en bas se terminant par « Bâtiment d'exemple : rien n'est envoyé. » ; la facture est « en attente d'envoi » dans le suivi du locataire.
  - Relancer le Play → la fenêtre d'envoi automatique ne propose pas cette facture.
  - Une facture de dépôt, de régularisation ou de refacturation de l'exemple → refusée, « Aucune adresse email pour ce locataire… » : rien ne part.
- [x] **AQ4 · Supprimer l'exemple** : bandeau → « Supprimer l'exemple » → « Supprimer l'exemple ? » → « Supprimer » → l'onglet disparaît, retour à l'accueil ; le lien redit « Essayer avec un exemple ».

## AR. Étape 14 : e-mails de l'abonnement (25/10) *(validé le 26/10, AR1 à AR6 et AR8 ; AR7 le 27/10)*

Décisions du 25/10 : les e-mails partent du Gmail de CIPL (pour l'instant ; un service d'envoi, type Brevo, avant l'étape 17), **aux admins du groupe seulement**. CIPL en envoie 5 (maquettes validées telles quelles) : l'essai se termine dans N jours (dès 7 jours avant), demain (la veille), l'essai est terminé (le lendemain), la résiliation est enregistrée, l'abonnement est terminé. Les reçus, le paiement refusé et la carte qui expire sont envoyés par **Stripe** lui-même. Chaque e-mail ne part qu'une fois. Ceux de l'essai partent chaque matin à 9 h (10 h l'été), par une tâche planifiée du serveur ; ceux de l'abonnement, dès que Stripe prévient le serveur.

Préalable (une fois) :
1. **Supabase › Edge Functions › Secrets** : ajoute `CIPL_GMAIL` = `agathelimouzy@gmail.com`, et `CIPL_GMAIL_MOT_DE_PASSE` = un mot de passe d'application Gmail (celui de l'étape 12 convient), **saisi par toi seulement**.
2. **Edge Functions › Deploy a new function › Via Editor** : nom `emails-abonnement`, colle tout `Serveur/fonctions/emails-abonnement.ts` à la place du modèle → Deploy, puis dans ses réglages **désactive « Verify JWT »** (Enforce JWT verification) → Save. Comme `stripe-webhook` en AH : la tâche du matin n'a pas de jeton, elle présente une clé rangée dans le coffre du serveur.
3. **Recoller** `Serveur/fonctions/stripe-webhook.ts` (« Verify JWT » reste désactivé) et `abonnement-stripe.ts` (reste activé) : Code › Deploy.
4. **SQL Editor** : `Serveur/etape14_supabase.sql` → Run (après etape13). Vérifier la tâche : `select jobname, schedule from cron.job;` → `cipl-emails-abonnement`, `0 8 * * *` (8 h UTC).
5. **Stripe** : rien à faire maintenant. Constaté le 26/10 : ces réglages sont grisés en mode test (« modifiables en mode production ») ; ils passent à l'étape 17 (CONCEPTION §17) : https://dashboard.stripe.com/settings/emails › « Paiements réussis » ; https://dashboard.stripe.com/revenue_recovery/emails › échec des paiements par carte bancaire, échec des paiements par prélèvement bancaire, cartes arrivant à expiration. De toute façon, en mode test, Stripe n'écrit qu'aux adresses de l'équipe de ton compte Stripe.

Tout se fait dans le SQL Editor, sur SCI (JBC) (`agathelimouzy+societe@gmail.com`, toi seul(e) admin) : les e-mails arrivent dans ta boîte. Dans CIPL, seulement AR4.

- [x] **AR1 · L'aperçu, sans rien envoyer** :
  ```sql
  select fin_essai, statut from public.groupes
    where titulaire = (select id from auth.users where email = 'agathelimouzy+societe@gmail.com');
  ```
  → note la date (`2026-11-08` si le compte date du 25/10) ; statut `essai`. Puis :
  ```sql
  update public.groupes set fin_essai = current_date + 7
    where titulaire = (select id from auth.users where email = 'agathelimouzy+societe@gmail.com');
  select public.emails_abonnement_du_jour();
  ```
  → `[{"groupe" : "…", "sorte" : "essai_j7", "date" : "2026-11-02"}]` (aujourd'hui + 7 jours ; dates d'AR1 et AR2 pour un test le 26/10, à décaler un autre jour). Rien n'est parti. D'autres groupes de test peuvent apparaître si leur essai finit bientôt : ils recevront aussi leur e-mail, dans ta boîte. Pour voir ce que donnerait un autre jour : `select public.emails_abonnement_du_jour('2026-11-01');` → `essai_j1`.
- [x] **AR2 · « Se termine dans 7 jours »** : lancer tout de suite l'envoi du matin :
  ```sql
  select net.http_post(
    url := 'https://ehxskfjmiioesmipzsad.supabase.co/functions/v1/emails-abonnement',
    headers := jsonb_build_object('Content-Type', 'application/json',
      'x-cipl-cron', (select decrypted_secret from vault.decrypted_secrets where name = 'cipl_cron')),
    body := '{}'::jsonb);
  ```
  puis, quelques secondes après : `select status_code, content from net._http_response order by created desc limit 1;` → `200` et `{"… · essai_j7 · 2026-11-02":"envoyé à 1 admin(s)"}`.
  - E-mail reçu, expéditeur « CIPL » : « Votre essai CIPL se termine dans 7 jours » ; « Bonjour <ton nom>, L'essai gratuit de CIPL pour SCI (JBC) se termine le 2 novembre 2026… » ; « … passera en lecture seule le 3 novembre 2026… » ; signé « L'équipe CIPL ».
  - Retour du 26/10 : « L'équipe » arrivait en « L'c3©quipe » (défaut de la bibliothèque d'envoi, denomailer) → le texte part maintenant en base64. Recoller `Serveur/fonctions/emails-abonnement.ts` (Code › Deploy), puis, pour recevoir de nouveau l'e-mail : `delete from public.envois_abonnement where groupe_id = (select id from public.groupes where titulaire = (select id from auth.users where email = 'agathelimouzy+societe@gmail.com'));` et le `net.http_post` ci-dessus → tous les accents justes, « L'équipe CIPL » compris.
  - Relancer le même `net.http_post` → `{"envoyes":0}` : pas de second e-mail.
  - Dans `content`, `échec : … 535 …` : adresse ou mot de passe d'application faux dans les secrets (préalable 1). Un `401` : « Verify JWT » est encore actif (préalable 2 : désactiver, enregistrer, redéployer).
- [x] **AR3 · « Se termine demain »** : `update public.groupes set fin_essai = current_date + 1` (même `where` qu'en AR1), puis le `net.http_post` d'AR2 → « Votre essai CIPL se termine demain ».
- [x] **AR4 · « Essai terminé »** : `fin_essai = current_date - 1`, puis le `net.http_post` → « Votre essai CIPL est terminé ». Play avec `+societe` → SCI (JBC) en lecture seule (comme en AG).
- [x] **AR5 · Résiliation et fin d'abonnement** (sans carte : on demande l'e-mail comme le fait `stripe-webhook`) :
  ```sql
  select net.http_post(
    url := 'https://ehxskfjmiioesmipzsad.supabase.co/functions/v1/emails-abonnement',
    headers := jsonb_build_object('Content-Type', 'application/json',
      'x-cipl-cron', (select decrypted_secret from vault.decrypted_secrets where name = 'cipl_cron')),
    body := jsonb_build_object('groupe', (select id from public.groupes
              where titulaire = (select id from auth.users where email = 'agathelimouzy+societe@gmail.com')),
            'sorte', 'resiliation', 'date', '2026-11-25'));
  ```
  → « Votre résiliation CIPL est enregistrée » ; « Il reste actif jusqu'au 25 novembre 2026… ». La même commande avec `'abonnement_fin'` → « Votre abonnement CIPL est terminé » ; « … a pris fin le 25 novembre 2026… ».
  - Le vrai chemin, facultatif (il faut un abonnement de test payé, comme en AH) : Réglage › Abonnement › « Résilier » → l'e-mail de résiliation arrive aux admins ; « Reprendre l'abonnement », puis « Résilier » de nouveau → pas de second e-mail (même échéance).
- [x] **AR6 · Personne d'autre ne peut l'appeler** : dans PowerShell,
  ```powershell
  curl.exe -s -X POST https://ehxskfjmiioesmipzsad.supabase.co/functions/v1/emails-abonnement
  ```
  → `{"erreur":"Refusé."}`, aucun e-mail.
- [x] **AR7 · Le lendemain matin** (après 9 h) : `select status, start_time from cron.job_run_details order by start_time desc limit 3;` → `succeeded` ; aucun e-mail en double.
  - Constaté le 27/10 : tâche `succeeded` à 8 h 00 UTC ; `envois_abonnement` vide ce jour-là (aucun essai à moins de 7 jours de sa fin), donc rien envoyé et aucun doublon. Pour voir ce qu'un matin a envoyé : `select g.nom, e.sorte, e.cle, e.envoye_le from public.envois_abonnement e join public.groupes g on g.id = e.groupe_id where e.envoye_le >= current_date;` (sélectionner la requête puis « Run selected » : sans sélection, Supabase exécute tout l'onglet).
- [x] **AR8 · Remise en état** : `update public.groupes set fin_essai = '<la date notée en AR1>'` (même `where`) → SCI (JBC) redevient modifiable. Les e-mails de l'essai repartiront pour cette date, au bon moment (dès 7 jours avant).

## AS. Étape 15 : installation et mises à jour (26/10) *(validé le 26/10, AS1 à AS10 ; AS11 le jour de la sortie)*

Décisions du 26/10 : l'installeur sur **GitHub, dans un dépôt à part** (`CIPL-installation`, sans le code), **privé jusqu'à la sortie** et passé public ce jour-là ; CIPL **s'installe lui-même** quand une version plus récente existe ; mise à jour **obligatoire seulement si le serveur change** (une version minimale) ; **identité interne fixée** à `CIPL` (à ne plus jamais changer ; le nom commercial, pas encore choisi, ne changera que le nom visible) ; maquettes validées telles quelles ; le Mac viendra à l'étape 15b.

Préalable (une fois) :
1. **Inno Setup 6** (gratuit ; il fabrique l'installeur, sur ton PC seulement : les clients ne l'installent jamais) : https://jrsoftware.org/isdl.php → télécharger et installer avec les options par défaut. Ou, dans un terminal : `winget install JRSoftware.InnoSetup`.
2. **GitHub : rien pour l'instant.** Le dépôt `CIPL-installation` sera créé **privé** (maintenant ou plus tard) et passé public le jour de la sortie (AS11). Tant qu'il est privé, ses liens ne marchent pour personne, CIPL compris : les essais lisent un dossier du PC.
3. **Mode d'essai : déjà en place** (26/10). Le fichier `C:\Users\agath\AppData\LocalLow\CIPL\CIPL\cipl_mises_a_jour.txt` contient `C:\Users\agath\Documents\CIPL\CIPL_Git\Build\Installation` : la version installée y lit `version.json` et l'installeur, au lieu de GitHub. « Publier » une version pour les essais, c'est donc simplement la construire (AS2). L'éditeur ne lit jamais ce fichier.
4. **Unity : déjà fait** (26/10). Project Settings › Player : Company Name `CIPL`, Product Name `CIPL`, Version `1.0.0`. Ne plus jamais changer Company Name ni Product Name.
5. Les anciens builds (`Build\CIPL Demo 2\CIPL_Test.exe`…) gardent l'ancienne identité : ne plus s'en servir. Le « 2e PC » des parcours devient la version installée.
6. **Contrôle intelligent des applications** (Windows 11 ; activé sur ton PC, constaté le 26/10) : il bloque l'installeur tant qu'il n'est pas signé, sans bouton « Exécuter quand même ». Pour AS4 à AS7, le désactiver le temps des essais, **toi-même** : Sécurité Windows › Contrôle des applications et du navigateur › Paramètres du contrôle intelligent des applications › Désactivé. Retour du 26/10 : impossible à réactiver sur ce PC (« can't be turned on without reinstalling Windows »). C'est le problème connu de la mise à jour d'octobre KB5127648 (signalé par Microsoft le 16/10/2026) ; réessayer après une prochaine mise à jour de Windows. Le PC reste protégé par Microsoft Defender et SmartScreen.

- [x] **AS1 · Rien n'a bougé dans l'éditeur** : Play → ta connexion gardée est reprise, comme avant (le dossier caché et les réglages ont été recopiés vers la nouvelle identité). Compte → en bas « CIPL version 1.0.0 ». Pas de bandeau de mise à jour : jamais dans l'éditeur.
  - Si l'écran de connexion s'affiche quand même : reconnecte-toi une fois (la connexion a pu se renouveler pendant la recopie) ; tes dossiers et réglages sont bien là.
- [x] **AS2 · Construire l'installeur** : Play arrêté, menu Unity **CIPL › Construire l'installeur** → quelques minutes (build, puis compression) → l'Explorateur s'ouvre sur `CIPL_Git\Build\Installation` : `CIPL-installation.exe` (environ 40 Mo) et `version.json` (`"version": "1.0.0"`, `"minimale": "1.0.0"`). Console : « [Installeur] CIPL 1.0.0 prêt… ».
  - Sans Inno Setup : fenêtre « Inno Setup 6 est introuvable… ».
- [x] **AS3 · Installer comme un client** : double-cliquer `Build\Installation\CIPL-installation.exe`. (L'avertissement « Windows a protégé votre ordinateur » n'apparaît que pour un fichier téléchargé : il sera vu en AS11.)
  - L'installeur, en français : **aucune demande d'autorisation d'administrateur** ; case « Créer une icône sur le Bureau » ; « Installer » ; à la fin, « Exécuter CIPL » coché → « Terminer » → CIPL s'ouvre ; la fenêtre s'appelle « CIPL ».
  - Si l'écran de connexion s'affiche : se connecter avec `agathelimouzy+societe@gmail.com` (SCI (JBC)), pas avec ton compte habituel.
  - Compte → « CIPL version 1.0.0 », et pas de bandeau (le dossier d'essai contient la même version).
  - Menu Démarrer et Bureau : « CIPL ». Paramètres Windows › Applications : « CIPL ». Dossier : `C:\Users\agath\AppData\Local\Programs\CIPL`.
  - Windows + R : `cipl://invitation?jeton=Essai12345` → dans CIPL ouvert, message en bas « Lien reçu : invitation… » (comme en U7).
- [x] **AS4 · Une nouvelle version, proposée** : fermer CIPL. Unity : Project Settings › Player › **Version `1.0.1`** → CIPL › Construire l'installeur.
  - Retour du 26/10 : contrôle intelligent activé, CIPL se fermait et ne se rouvrait pas (Windows bloquait l'installeur, sans rien afficher). À refaire après le préalable 6, en **reconstruisant la 1.0.1** (elle contient désormais le bilan de mise à jour).
  - Lancer CIPL depuis le menu Démarrer (version 1.0.0) → une fois l'entreprise ouverte, sous les onglets, bandeau bleu « Nouvelle version de CIPL : 1.0.1 (vous avez la 1.0.0). », avec « Plus tard » et « Mettre à jour ».
  - « Plus tard » → le bandeau disparaît ; fermer et relancer CIPL → il revient.
  - « Mettre à jour » → « Téléchargement de la version 1.0.1… N % — ensuite, CIPL se ferme… » → « Mise à jour prête… » → CIPL se ferme, une petite fenêtre d'installation passe, CIPL se rouvre seul → Compte : « CIPL version 1.0.1 », plus de bandeau.
- [x] **AS5 · Une saisie en cours n'est pas perdue** : Unity : Version `1.0.2` → Construire. Dans CIPL installé (1.0.1) : ouvrir une fiche de SCI (JBC) et cliquer « Modifier », sans enregistrer → « Mettre à jour » sur le bandeau → après le téléchargement, la fenêtre « Modifications non enregistrées » → **Annuler** → CIPL reste ouvert, la fiche toujours en modification, le bandeau dit « Mise à jour prête : elle s'installe dès que CIPL se ferme… ». Enregistrer ou annuler la fiche, puis fermer CIPL → l'installation se fait, CIPL se rouvre en 1.0.2, avec en bas « CIPL est à jour : version 1.0.2. »
- [x] **AS6 · Mise à jour obligatoire** : Unity : Version `1.0.3` → Construire → dans `Build\Installation`, ouvrir `version.json` avec le Bloc-notes, remplacer `"minimale": "1.0.0"` par `"minimale": "1.0.3"`, enregistrer.
  - Lancer CIPL (1.0.2) → **par-dessus tout**, même l'écran de connexion : fenêtre « Mise à jour nécessaire », « Cette version de CIPL (1.0.2) ne fonctionne plus avec le serveur. Installez la version 1.0.3… », boutons « Quitter CIPL » et « Mettre à jour » ; rien d'autre ne se clique.
  - « Quitter CIPL » → CIPL se ferme ; relancer → la même fenêtre.
  - « Mettre à jour » → l'avancement s'affiche dans la fenêtre → CIPL se ferme, s'installe, se rouvre en 1.0.3, sans la fenêtre.
  - Retour du 26/10 (trait blanc au-dessus de « Rentabilité », en build) : dans la 1.0.3, ouvrir la fiche du bâtiment rivoli → plus de trait blanc en haut ; en faisant défiler la fiche, la poignée de la barre glisse au bord droit de l'écran.
- [x] **AS7 · GitHub injoignable** : renommer `cipl_mises_a_jour.txt` en `cipl_mises_a_jour.txt.off` (dans `AppData\LocalLow\CIPL\CIPL`) → lancer CIPL → ni bandeau ni fenêtre : le dépôt, privé ou absent, ne répond pas, comme sans connexion, et CIPL démarre normalement. Renommer de nouveau en `cipl_mises_a_jour.txt`.
- [x] **AS7b · Un échec est annoncé** (retour du 26/10, simulé : le contrôle intelligent ne se réactive plus sur ce PC) : CIPL fermé, le fichier `C:\Users\agath\AppData\LocalLow\CIPL\CIPL\cipl_mise_a_jour_en_cours.txt` contient `9.9.9` (créé le 26/10 ; sinon, le créer avec le Bloc-notes). Lancer CIPL depuis le menu Démarrer → message au centre « La mise à jour vers la version 9.9.9 n'a pas pu s'installer : votre version 1.0.3 fonctionne toujours… » ; le fichier a disparu. Relancer CIPL → plus de message. Le vrai cas (installeur bloqué, puis installeur **signé** qui passe, contrôle activé) se fera sur un PC où le contrôle est actif, une fois la signature obtenue.
- [x] **AS8 · L'e-mail d'invitation** : dans CIPL installé, SCI (JBC) › Réglages › Utilisateurs › « Inviter » `agathelimouzy+invite15@gmail.com` → e-mail reçu : « 1. Installez CIPL sur votre PC Windows : https://github.com/agathe-limouzy/CIPL-installation/releases/latest/download/CIPL-installation.exe », puis la phrase sur « Exécuter quand même » ; pas de ligne « Sur Mac ». Le lien ne marchera qu'une fois le dépôt public (AS11). Ensuite, annuler l'invitation (Réglages › Utilisateurs).
  - Retour du 26/10 : depuis la version installée, « L'invitation est créée, mais l'e-mail n'est pas parti (Envoi refusé par le serveur (GeneralFailure) : Message could not be sent.) ». Avec la 1.0.5, le message donne la cause : « SocketException : Could not resolve host 'stmp.gmail.com' » : faute de frappe dans le serveur d'envoi de SCI (JBC). Corriger dans Réglages › Intégrations : `smtp.gmail.com`, « Enregistrer », puis Réglages › Utilisateurs › « Renvoyer » sur `agathelimouzy+invite15@gmail.com` → e-mail reçu. Puis une facture de test par e-mail depuis la version installée (à `agathelimouzy@gmail.com`).
- [x] **AS9 · Désinstaller** : Paramètres Windows › Applications › CIPL › Désinstaller → les raccourcis et `AppData\Local\Programs\CIPL` disparaissent ; tes données (`Documents\CIPL`, connexion gardée) restent : réinstaller (AS3) → tout est là. Unity n'est pas touché.
- [x] **AS10 · Remise en ordre** : Project Settings › Player › Version : garder la dernière version construite (`1.0.5`) ; la prochaine partira de `1.0.6`. Une fois tout validé, tu peux supprimer l'ancien dossier `C:\Users\agath\AppData\LocalLow\DefaultCompany\CIPL_Test` (gardé comme sauvegarde de la recopie) et les anciens builds de `CIPL_Git\Build` (pas `Build\Installation`).
- [ ] **AS11 · Le jour de la sortie (étape 17)** : prérequis, l'installeur **signé** (CONCEPTION §15). GitHub, dépôt `CIPL-installation` (le créer s'il n'existe pas : « New repository », Private, « Add a README file ») › Settings › Danger Zone › « Change visibility » › **Public**. Puis Releases › « Create a new release » → tag de la version construite (par exemple `v1.0.6`), titre `CIPL 1.0.6`, glisser `CIPL-installation.exe` et `version.json` → **Publish release**.
  - Dans le navigateur : https://github.com/agathe-limouzy/CIPL-installation/releases/latest/download/version.json → le fichier ; https://github.com/agathe-limouzy/CIPL-installation/releases/latest/download/CIPL-installation.exe → téléchargé, puis à l'ouverture « Windows a protégé votre ordinateur » → « Informations complémentaires » → « Exécuter quand même ».
  - Sur ton PC, supprimer `cipl_mises_a_jour.txt` : ta version installée lit alors GitHub, comme les clients.

## AT. Étape 15b : version Mac (26/10) *(26/10 : pas de Mac disponible ; AT1 et AT2 se font sur le PC, AT3 à AT9 attendent un Mac)*

Décisions du 26/10 : PDF par **Google Chrome ou Microsoft Edge** installé sur le Mac ; connexion gardée dans le **trousseau de macOS** ; **liens `cipl://` retirés partout**, Windows compris (inutilisés : tout passe par des codes) ; distribution en **.zip**, mise à jour **par le lien** ; signature Apple (99 €/an) à l'étape 17. Identifiant de l'application Mac fixé : `fr.cipl.cipl`.

Préalable (une fois) :
1. **Unity Hub** › Installs › 2023.2.22f1 › ⚙ › Add modules › cocher **« Mac Build Support (Mono) »** › Install. Redémarrer Unity ensuite.
2. Sur le Mac : **Google Chrome** installé (pour les PDF), sauf pour AT6.
3. Pour copier le .zip sur le Mac : une clé USB, ou Google Drive (le fichier est trop gros pour un e-mail).

- [x] **AT1 · Windows n'a pas bougé** : Unity, Version `1.0.6` → CIPL › Construire l'installeur → dans CIPL installé, bandeau → « Mettre à jour » → CIPL 1.0.6.
  - Une facture : « Générer facture » → l'aperçu s'affiche (Edge, comme avant). Ouvrir un document (un bail ou un PDF du suivi) → il s'ouvre. Réglages › Documents › ouvrir le dossier → l'Explorateur s'ouvre.
  - Windows + R : `cipl://invitation?jeton=Essai12345` → Windows ne trouve plus d'application pour ce lien (la mise à jour a effacé l'ancienne déclaration).
- [ ] **AT2 · Construire la version Mac** : CIPL › **Construire la version Mac** → quelques minutes → l'Explorateur s'ouvre sur `Build\Installation\CIPL-mac.zip` ; `version.json` porte maintenant `"mac": "…/CIPL-mac.zip"`. Unity reste en Windows (File › Build Settings : Windows).
  - Sans le module : fenêtre « Le module « Mac Build Support (Mono) » n'est pas installé… ». Si la version de Unity n'a pas encore son installeur Windows : « Construisez d'abord l'installeur Windows de la version… ».
- [ ] **AT3 · L'ouvrir sur le Mac** : copier `CIPL-mac.zip` sur le Mac, double-cliquer → `CIPL.app` ; la glisser dans **Applications** ; l'ouvrir.
  - macOS bloque une application non signée : « CIPL ne peut pas être ouvert… » → Réglages Système › Confidentialité et sécurité › en bas, **« Ouvrir quand même »** (ou clic droit sur CIPL › Ouvrir).
  - Si macOS dit **« CIPL est endommagé »** : app Terminal, taper `xattr -cr /Applications/CIPL.app` (Entrée), puis `codesign --force --deep --sign - /Applications/CIPL.app` (Entrée), puis rouvrir CIPL. Me dire ce que macOS a affiché : la signature Apple (étape 17) devra l'éviter aux clients.
- [ ] **AT4 · Se connecter et le rester** : se connecter avec `agathelimouzy+societe@gmail.com`, « Rester connecté(e) » coché → SCI (JBC) s'ouvre ; quitter CIPL (Cmd + Q), le rouvrir → pas d'écran de connexion. App **Trousseaux d'accès** (ou **Mots de passe**) : une entrée « CIPL ». Compte › Se déconnecter → l'entrée disparaît ; rouvrir CIPL → écran de connexion.
- [ ] **AT5 · Les PDF** : une facture de SCI (JBC) → « Générer facture » → l'aperçu s'affiche (fait par Chrome). Une quittance → le PDF s'ouvre dans Aperçu.
- [ ] **AT6 · Sans Chrome ni Edge** (facultatif, sur un Mac qui n'a aucun des deux) : « Générer facture » → message au centre « Pour créer les PDF des factures sur Mac, CIPL a besoin de Google Chrome ou de Microsoft Edge (gratuits)… ».
- [ ] **AT7 · Documents et dossiers** : ouvrir un bail ou une photo → il s'ouvre (Aperçu) ; Réglages › Documents › ouvrir le dossier, ou l'export → le **Finder** s'ouvre sur le dossier ; « Choisir un logo… » → la fenêtre de choix de fichier du Mac.
- [ ] **AT8 · Mise à jour sur Mac** : Unity, Version `1.0.7` → Construire l'installeur, puis Construire la version Mac. Sur le Mac : copier `version.json` et `CIPL-mac.zip` (de `Build\Installation`) dans un dossier, par exemple `Documents/CIPL-maj`. Créer avec TextEdit (Format › Convertir au format texte) le fichier `~/Library/Application Support/CIPL/CIPL/cipl_mises_a_jour.txt` contenant le chemin de ce dossier (`/Users/<toi>/Documents/CIPL-maj`). Lancer CIPL (1.0.6) → bandeau « Nouvelle version de CIPL : 1.0.7 » → « Mettre à jour » → le .zip s'ouvre et le bandeau dit « La nouvelle version se télécharge… glissez CIPL dans Applications (« Remplacer ») » → le faire → rouvrir CIPL → Compte : « CIPL version 1.0.7 ». Ensuite, supprimer `cipl_mises_a_jour.txt` du Mac.
- [ ] **AT9 · Drive partagé** (facultatif, si OneDrive ou Google Drive est installé sur le Mac) : une entreprise en mode « Drive partagé » retrouve seule son dossier dans `~/Library/CloudStorage`.

## AU. Étape 16 : boutons « Tester » (26/10) *(validé le 27/10)*

Décisions du 26/10 : le test porte sur **ce qui est tapé**, sans enregistrer avant ; les **messages clairs servent partout** (factures, relances et invitations par e-mail, dépôts Pennylane, carte Mapbox), suivis du « Détail technique » ; maquette validée telle quelle.

Préalable : Unity › Play ; se connecter avec `agathelimouzy+societe@gmail.com` → SCI (JBC) ; ouvrir **Réglages**. Dans chaque cas, on **n'enregistre pas** : on revient avec « ← Retour » (si CIPL demande, abandonner les modifications).

- [x] **AU1 · Clé Pennylane valide** (si SCI (JBC) a une clé Pennylane ; sinon passer à AU2) : onglet **Intégrations** › à droite de la clé, **« Tester la clé »** → « Test en cours… » puis, en vert : « Clé valide : société « … » (SIREN …). Lecture des clients et des factures : autorisée. » Vérifier que c'est bien la société attendue.
- [x] **AU2 · Clé Pennylane fausse** : ajouter `x` à la fin de la clé (ou taper `essai` si le champ est vide) → « Tester la clé » → fenêtre au centre et texte rouge : « Pennylane — test de la clé : Clé refusée par Pennylane : recopiez-la en entier, ou créez-en une nouvelle (Pennylane → Paramètres → Connectivité → Développeurs). » puis « Détail technique : HTTP 401 … ». Vider le champ → « Tester la clé » → « Collez d'abord la clé API Pennylane. ». « ← Retour » sans enregistrer.
- [x] **AU3 · Jeton Mapbox** : onglet **Intégrations** › Cartes (Mapbox) › **« Tester le jeton »** (avant le 27/10 : Documents › Cartes) → en vert : « Jeton valide : Mapbox a renvoyé une carte. » Ajouter `x` à la fin du jeton → « Tester le jeton » → au centre : « Jeton refusé par Mapbox : recopiez-le en entier (il commence par pk.) depuis account.mapbox.com → Tokens. » puis le détail (HTTP 401). « ← Retour » sans enregistrer ; une fiche bâtiment affiche toujours sa carte.
- [x] **AU4 · Serveur d'e-mail mal écrit, sans enregistrer** : onglet **Intégrations** › Serveur SMTP : remplacer par `stmp.gmail.com` (**ne pas enregistrer**) → **« Envoyer un email de test »** → quelques secondes → au centre : « Serveur « stmp.gmail.com » introuvable : vérifiez l'orthographe (Gmail : smtp.gmail.com, Outlook : smtp.office365.com) et la connexion internet. » puis le détail. Remettre `smtp.gmail.com` → « Envoyer un email de test » → en vert « Envoyé à … » et l'e-mail « CIPL — test d'envoi » arrive à l'adresse d'expédition. « ← Retour », rouvrir Réglages › Intégrations : le serveur est `smtp.gmail.com` (rien n'a été enregistré).
- [x] **AU5 · Mot de passe refusé** : ajouter `x` à la fin du mot de passe d'application (dans le champ seulement) → « Envoyer un email de test » → au centre : « Identifiant ou mot de passe refusé. Gmail et Outlook demandent un mot de passe d'application (créé dans la sécurité du compte), pas le mot de passe habituel. » « ← Retour » sans enregistrer.
- [x] **AU6 · Sans internet** (facultatif) : couper le Wi-Fi → Intégrations › « Tester le jeton » → « Mapbox injoignable : vérifiez la connexion internet. » Rallumer le Wi-Fi.

## AV. Réglages réorganisés (27/10) *(validé le 27/10)*

Décisions du 27/10 : une carte par service dans **Intégrations**, visible par tous (Envoi des factures, Pennylane et E-mail réservés à l'admin ; **Cartes (Mapbox)** pour tout le monde, chacun sur son PC) ; **Entreprise** : nom, **SIRET** (modifiable par l'admin), RIB, export ; **Facturation** : logo, entêtes, listes de charges, textes fixes ; **Documents et affichage** : documents et taille de l'affichage. Le logo et les RIB restent réservés à l'admin (comme depuis le 11/10).

Préalable (une fois) : Supabase › SQL Editor › **New query** (un nouvel onglet, pas « Abonnement ») → coller tout `Serveur/etape16_supabase.sql` → Run → « Success. No rows returned ». Puis Unity › Play, `agathelimouzy+societe@gmail.com` → SCI (JBC), Réglages.

- [x] **AV1 · Les onglets** : Entreprise · Facturation · Utilisateurs · Abonnement · **Documents et affichage** · Intégrations.
  - **Entreprise** : « Nom de l'entreprise » ; « SIRET » avec le numéro en groupes (`123 456 789 00012`) et « Modifier » ; « RIB (comptes émetteurs) » ; « Exporter toutes les données ».
  - **Facturation** : Logo de la facture, Entêtes, Listes de charges, Textes fixes (plus de RIB).
  - **Documents et affichage** : Documents, Taille de l'affichage (plus de Cartes ni d'export).
  - **Intégrations** : Envoi des factures (la case Pennylane) ; Pennylane (clé + « Tester la clé ») ; E-mail (SMTP) (réglages + e-mail de test) ; Cartes (Mapbox) (token + « Tester le jeton »).
- [x] **AV2 · Changer le SIRET, refusé** : Entreprise › SIRET › « Modifier » → la fenêtre « Modifier le SIRET », le SIRET actuel et, dessous, la société trouvée dans l'annuaire. Taper `123` → « Enregistrer » → au centre « Le SIRET compte 14 chiffres. » → OK → la fenêtre se rouvre avec `123`. Taper le SIRET d'une autre entreprise déjà sur CIPL (celui de Démo CIPL, s'il en a un) → « Cette société est déjà sur CIPL… ». Annuler.
- [x] **AV3 · Changer le SIRET, accepté puis remis** : « Modifier » → un vrai SIRET d'une autre société en activité (par exemple celui d'un locataire société) → la ligne d'annuaire montre sa forme et son siège → « Enregistrer » → message en bas « SIRET enregistré. » et la ligne SIRET le montre. Puis **remettre le SIRET d'origine de SCI (JBC)** de la même façon. Le bas de page des factures n'a pas changé (Facturation › Textes fixes).
- [x] **AV4 · Les RIB guidés** : carte des premiers pas › « Vos RIB » › « Commencer » (ou « Refaire ») → Réglages s'ouvre sur **Entreprise**, la bulle montre « +  Ajouter un RIB ». Fermer la bulle sans rien ajouter.
- [x] **AV5 · Un utilisateur** (facultatif, avec un compte utilisateur de SCI (JBC)) : onglets Entreprise · Facturation · Documents et affichage · **Intégrations**. Entreprise : nom et SIRET sans « Modifier », RIB sans boutons. Intégrations : « Envoi des factures : … Réglé par l'administrateur. », puis seulement **Cartes (Mapbox)** (pas de Pennylane ni d'e-mail) ; « Tester le jeton » marche.
- [ ] *(sans objet le 27/10 : la requête était passée)* **AV6 · Sans la requête du serveur** (facultatif, si le préalable n'est pas fait) : la ligne SIRET dit « Non renseigné », et « Modifier » → « Enregistrer » → « Changement impossible : … » (rien n'est cassé).

## AW. Étape 16a : envoyer depuis l'adresse du client (28/10) — validé le 28/10

Décisions des 25 et 27/10 : Gmail et Google Workspace → « Se connecter avec Google » ; Outlook, Hotmail, Live et Microsoft 365 → « Se connecter avec Microsoft » (droit « envoyer » seulement, CIPL ne lit rien et ne voit aucun mot de passe) ; les autres messageries → SMTP, serveur rempli d'après l'adresse ; une adresse sur un domaine propre → le bouton de sa messagerie (lu dans ses serveurs de courrier) ; pas d'envoi de secours par CIPL. L'autorisation reste sur le serveur. Validations de Google et de Microsoft à l'étape 17 : d'ici là, écran « application non validée » et, chez Google en mode Test, **l'autorisation expire au bout de 7 jours** (il faut alors relier de nouveau).

Préalable (une fois) :
1. Google Cloud (fait le 28/10) : projet CIPL, API Gmail activée, Google Auth Platform (Externe, testeuse `agathelimouzy@gmail.com`, niveaux d'accès `openid`, `userinfo.email`, `gmail.send`), client « Application de bureau ».
2. Microsoft Entra (fait le 28/10) : compte Azure gratuit sur `agathelimouzy@gmail.com`, inscription « CIPL » multilocataire et comptes personnels, redirection « Client public/natif » `http://localhost`, flux de clients publics : Oui, autorisation déléguée Mail.Send.
3. Supabase › Edge Functions › Secrets : `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `MICROSOFT_CLIENT_ID` (fait le 28/10).
4. Supabase › SQL Editor › **New query** : tout `Serveur/etape16a_supabase.sql` → Run → « Success. No rows returned ».
5. Edge Functions › **Deploy a new function** › Via Editor : nom `messagerie`, coller tout `Serveur/fonctions/messagerie.ts` à la place du modèle → Deploy. « Verify JWT » reste **activé** (comme `abonnement-stripe`).

Puis Unity › Play, `agathelimouzy+societe@gmail.com` → SCI (JBC), Réglages › Intégrations › carte **E-mail**. Factures de test vers `agathelimouzy@gmail.com` seulement ; ne jamais cliquer « Tout envoyer ».

- [x] **AW1 · La carte reconnaît Gmail** : « Adresse d'expédition » `agathelimouzy@gmail.com` (cliquer ailleurs après l'avoir tapée) → « Cette adresse est chez Gmail : reliez-la avec Google (en vert), sans mot de passe à recopier. », bouton vert **« Se connecter avec Google »**, bouton gris « Se connecter avec Microsoft », et « Utiliser plutôt un serveur SMTP » (→ la zone SMTP, `smtp.gmail.com`, comme avant ; rouvrir Réglages pour revenir).
- [x] **AW2 · Relier Gmail** : « Se connecter avec Google » → le navigateur s'ouvre sur Google → choisir `agathelimouzy@gmail.com` → « Google n'a pas validé cette application » : **Paramètres avancés › Accéder à CIPL (non sécurisé)** (normal avant l'étape 17) → cocher « Envoyer des e-mails en votre nom » → Continuer → onglet « C'est fait » → revenir dans CIPL : message en bas « Adresse reliée à Google : agathelimouzy@gmail.com » ; la carte dit en vert « Reliée à Google : … », l'adresse n'est plus modifiable, le bouton devient « Se déconnecter ».
  - Si une fenêtre au centre s'affiche à la place : me dire son texte (« pas encore réglée sur le serveur » : un secret manque ; « La fonction « messagerie » … » : préalable 5).
- [x] **AW3 · E-mail de test par Google** : « Envoyer un email de test » → en vert « Envoyé à agathelimouzy@gmail.com » → l'e-mail « CIPL — test d'envoi » arrive, et figure dans Gmail › **Envoyés** (il est parti de ton compte, sans mot de passe d'application).
- [x] **AW4 · Une vraie facture** : une facture d'un locataire de test dont le destinataire est `agathelimouzy@gmail.com` → « Sauvegarder et envoyer » → reçue avec son PDF ; l'objet garde ses accents ; le suivi passe à « envoyée ».
- [x] **AW5 · Se déconnecter** : « Se déconnecter » → confirmer → « Adresse déliée… » ; la carte repropose « Se connecter avec Google ». Sur myaccount.google.com/permissions, CIPL n'a plus d'accès. Relier de nouveau (AW2) pour la suite.
- [x] **AW6 · Microsoft** : « Se déconnecter » de Google, puis « Adresse d'expédition » = ton adresse Outlook ou Hotmail de test → « Cette adresse est chez Outlook (ou Hotmail)… », **« Se connecter avec Microsoft »** → fenêtre de Microsoft → se connecter → « CIPL souhaite… (non vérifié) » → **Accepter** → « Reliée à Microsoft : … » → e-mail de test vers cette adresse → reçu, et présent dans **Éléments envoyés** de la boîte Outlook. Puis « Se déconnecter », remettre `agathelimouzy@gmail.com` et la relier à Google (AW2).
  - Adresse professionnelle Microsoft 365 (`@cipl.fr`, constaté le 28/10) : « Approbation administrateur requise » tant que CIPL n'a pas d'éditeur vérifié (étape 17), parce que le compte n'est pas administrateur du Microsoft 365. Ne pas cliquer « Connectez-vous avec ce compte » (CIPL relierait alors l'adresse de l'administrateur) : l'administrateur autorise CIPL une fois de son côté, puis on recommence avec sa propre adresse. Sinon, tester avec une adresse Outlook.com ou Hotmail.
- [x] **AW7 · Les autres messageries** (rien à relier, **ne pas enregistrer**) : taper `x@orange.fr` → zone SMTP avec « Messagerie Orange : le serveur est rempli. Il reste le mot de passe. » et `smtp.orange.fr` ; `contact@cipl.fr` → « Cette adresse est chez Microsoft 365 » (lu dans ses serveurs de courrier) ; `contact@exemple-inconnu.fr` → les deux boutons et « Utiliser plutôt un serveur SMTP » (→ zone SMTP sans serveur rempli ; voir AW10). « ← Retour » sans enregistrer, rouvrir : l'adresse reliée est toujours là.
- [x] **AW10 · Les deux boutons, et le mauvais choix** (compléments du 28/10) : « Se déconnecter » de Google. « Adresse d'expédition » `contact@exemple-inconnu.fr` → « CIPL ne reconnaît pas la messagerie de cette adresse : choisissez celle où vous recevez vos e-mails. », **« Se connecter avec Google »** et **« Se connecter avec Microsoft »** tous deux en vert (aucun n'est conseillé), et « Utiliser plutôt un serveur SMTP », sur une ligne. Puis remettre `agathelimouzy@gmail.com` → Google en vert, Microsoft en gris → bouton gris **« Se connecter avec Microsoft »** → dans la fenêtre, ton compte Microsoft `agathelimouzy@gmail.com` → Accepter → en bas « Adresse reliée à Microsoft : agathelimouzy@gmail.com. » → « Envoyer un email de test » → « Envoyé à agathelimouzy@gmail.com » et l'e-mail arrive (constaté le 28/10 : ton compte Microsoft créé sur Gmail envoie bien ; constaté : arrivé en **Spam**, expéditeur `agathelimouzy@gmail.com` — d'où l'avertissement d'AW12). Puis « Se déconnecter » et relier avec le bouton vert Google (AW2).
- [x] **AW11 · Refus de Microsoft 365** : « Adresse d'expédition » `agathe.limouzy@cipl.fr` → « Se connecter avec Microsoft » → « Approbation administrateur requise » → **« Revenir à l'application sans donner son accord »** (pas « Connectez-vous avec ce compte ») → au centre « Autorisation refusée dans la fenêtre de Microsoft : rien n'est relié. » suivi de la phrase sur l'administrateur de votre Microsoft 365 ; rien n'est relié. Si rien ne revient, CIPL dit au bout de 5 minutes que la fenêtre n'a pas répondu : me le dire. « ← Retour » sans enregistrer.
- [x] **AW12 · L'avertissement du bouton gris** (ajouté le 28/10 après AW10) : `agathelimouzy@gmail.com` non reliée → bouton gris « Se connecter avec Microsoft » → au centre « Relier avec Microsoft ? » : « Cette adresse est chez Gmail. Envoyés par Microsoft, ses e-mails risquent d'arriver dans les indésirables des destinataires (constaté à l'essai). Le bouton vert, Google, est conseillé. » → **Annuler** : rien ne s'ouvre. Puis `contact@exemple-inconnu.fr` → « Se connecter avec Microsoft » (vert) → la fenêtre de Microsoft s'ouvre tout de suite, sans avertissement → y cliquer « Annuler » → CIPL : « Autorisation refusée… », rien n'est relié. Remettre `agathelimouzy@gmail.com`, reliée à Google.
- [x] **AW8 · Le guide** (facultatif) : premiers pas › « L'envoi des factures » › Refaire → bulles « Comment partent vos factures », « L'adresse d'expédition », puis « Relier votre adresse » (adresse Gmail) — avec une adresse Orange, « Votre serveur d'envoi » et « Le mot de passe » à la place. Fermer la bulle sans enregistrer.
- [x] **AW9 · Le guide du logo** (correction du 28/10) : premiers pas › « Votre logo » › Refaire → Réglages s'ouvre sur **Facturation** (et non plus Entreprise), la bulle montre « Choisir un logo… ».

## AX. Étape 16b : cartes Google et fiche Pappers des locataires (28/10) — validé le 29/10 (AX8 le 30/10)

Décisions du 28/10 : les clés Google et Pappers sont **celles de CIPL**, sur le serveur ; les clients n'ont rien à régler. Pappers est relu une fois par mois et par société (fiche gardée sur le serveur), les procédures collectives viennent du BODACC (gratuit) ; le bloc société du commentaire gagne une ligne « État » ; une procédure ouverte crée l'objectif « Déclarer la créance au mandataire ». Le token Mapbox disparaît. Sur le cloud, CIPL n'utilise que les 9 premiers chiffres du SIRET (le SIREN).

Préalable (une fois, par toi) :
1. Google Cloud (projet CIPL) : compte de facturation associé ; **Maps Static API** activée ; une **clé API** restreinte à Maps Static API (pas de restriction d'application) ; quota « Requests per day » de Maps Static API à **330**.
2. Pappers : compte sur pappers.fr/api, clé d'API copiée. **L'« Offre 0 » ne donne aucun crédit** (constaté le 29/10 : « Vous n'avez plus assez de crédits ») : abonnement pris le 29/10.
3. Supabase › Edge Functions › Secrets : `GOOGLE_MAPS_KEY` et `PAPPERS_API_KEY`.
4. Supabase › SQL Editor › **New query** : tout `Serveur/etape16b_supabase.sql` → Run → « Success. No rows returned ».
5. Edge Functions › **Deploy a new function** › Via Editor : `pappers` (tout `Serveur/fonctions/pappers.ts`) puis `cartes` (tout `Serveur/fonctions/cartes.ts`). « Verify JWT » reste **activé** pour les deux.

Puis Unity › Play, `agathelimouzy+societe@gmail.com` → SCI (JBC).

- [x] **AX1 · La carte d'une fiche bâtiment** : ouvrir un bâtiment avec une adresse → carte **satellite avec les noms des rues** (Google, logo Google en bas), épingle rouge sur l'adresse, cadrage proche de l'ancien. Zoom + / − et glisser : la carte suit le doigt (avant, elle allait deux fois trop vite). Rouvrir la même fiche : la carte ne se recharge pas.
  - Si « Carte indisponible… » en bas : me dire le texte (« pas encore réglées sur le serveur » : préalable 3 ; « pas déployée » : préalable 5 ; « Google refuse la carte » : préalable 1).
- [x] **AX2 · Les vignettes de l'accueil** : retour à l'accueil → chaque bâtiment a sa petite carte satellite.
- [x] **AX3 · Plus de token Mapbox** : Réglages › Intégrations : Envoi des factures, Pennylane, E-mail, **plus de section « Cartes (Mapbox) »**. Avec un compte « Utilisateur » (pas admin) : plus d'onglet Intégrations du tout.
- [x] **AX4 · Le bloc société d'un locataire** (validé le 29/10) : un locataire de test avec le SIRET d'une vraie société (par exemple `55203253400646`) → « Modifier » → « Enregistrer » (sans rien changer d'autre) → quelques secondes plus tard, dans le commentaire, le bloc entre `-- PAPPERS --` et `-- FIN PAPPERS --` : nom en gras ; forme · capital ; SIREN · créée le ; **« État : SAINE »** (ou À SURVEILLER, avec la raison) ; Dirigeants ; Finances (3 années au plus) ; « Procédures collectives : Aucune » ; « Fiche Pappers du 28/10/2026 ». Ce que tu avais écrit autour du bloc est resté.
  - Si le bloc ne change pas : me copier les lignes `[PappersSync]` de la console (refus « config », « cle », « jetons »…), ou celles de Supabase › Edge Functions › pappers › Logs.
  - Si des lignes sont vides alors que pappers.fr les montre (dirigeants, finances, capital) : me le dire, c'est un nom de champ à corriger dans `pappers.ts`.
- [x] **AX5 · Une seule lecture payante** : enregistrer de nouveau la même fiche → le bloc ne bouge pas, et sur ton tableau de bord Pappers, le nombre de jetons utilisés n'a pas augmenté (la fiche vient du serveur pendant un mois).
- [x] **AX6 · Une procédure collective** : locataire de test avec un SIRET qui commence par `843965716` (METAGO Investissements, sauvegarde ouverte le 12/10/2026, BODACC du 28/10/2026), par exemple `84396571600017` (si la fiche refuse, les 9 chiffres seuls) → « Enregistrer » → bloc : **« État : EN DIFFICULTÉ — procédure de sauvegarde (jugement du 12/10/2026) »** et la ligne dans « Procédures collectives » ; onglet Objectifs du locataire : **« Déclarer la créance au mandataire : procédure de sauvegarde (jugement du 12/10/2026) »**, Obligatoire, échéance **28/12/2026** ; elle apparaît aussi dans « À traiter » de l'accueil.
- [x] **AX7 · L'objectif ne revient pas** : supprimer cet objectif → « Enregistrer » la fiche de nouveau → il ne revient pas. Puis remettre le SIRET d'avant et retirer ce locataire de test.
- [x] **AX9 · Le commentaire défile** (correction du 29/10, validé le 29/10) : un locataire au commentaire long (le bloc Pappers suffit) → la molette au-dessus du commentaire le fait défiler jusqu'au bout (`-- FIN PAPPERS --`) en quelques crans, comme une page, avec la barre à droite, qui se saisit aussi à la souris (glisser la poignée) ; la fiche s'arrête un peu avant la barre de la page ; « Modifier » : le texte entier se modifie comme avant.
- [x] **AX8 · La tournée du matin** (constaté dans le journal de Unity le 31/10 : « [PappersSync] Pappers et BODACC : 0 locataire(s) mis à jour, 1 en échec. », l'échec étant Sephora de SCI (JBC), au SIRET fictif du bac à sable Super PDP) (le lendemain, facultatif) : au premier lancement du jour, console `[PappersSync] Pappers et BODACC : … locataire(s) mis à jour` ; les fiches déjà à jour ne sont pas réécrites (pas de « … mis à jour par … » sur les autres PC).

## AY. Étape 18a : relier une SCI à sa plateforme agréée (Super PDP), en bac à sable (29/10) — validé le 30/10

Décisions du 29/10 : Super PDP, d'abord en bac à sable (factures fictives) ; CIPL paie Super PDP pour tous ses clients (une SCI ne donne aucun moyen de paiement) ; le gérant relie sa SCI une fois, dans la fenêtre de Super PDP (la loi demande sa signature et Super PDP vérifie son identité) ; ensuite tout part tout seul. L'envoi des loyers et « Encaissée » : étape 18b.

Préalable (une fois, par toi) :
1. Super PDP : ton compte éditeur (CIPL) ; une **application OAuth** nommée CIPL, avec l'adresse de retour exacte `https://ehxskfjmiioesmipzsad.supabase.co/functions/v1/facture-electronique-retour`.
2. Supabase › Edge Functions › Secrets : `SUPERPDP_CLIENT_ID`, `SUPERPDP_CLIENT_SECRET` (ceux de l'application) et `SUPERPDP_BAC_A_SABLE` = `1`.
3. Supabase › SQL Editor › **New query** : tout `Serveur/etape18_supabase.sql` → Run → « Success. No rows returned ».
4. Edge Functions › **Deploy a new function** › Via Editor : `facture-electronique` (tout `Serveur/fonctions/facture-electronique.ts`, « Verify JWT » **activé**) ; puis `facture-electronique-retour` (tout `Serveur/fonctions/facture-electronique-retour.ts`, « Verify JWT » **désactivé** : c'est Super PDP qui y revient, sans jeton de CIPL).

Puis Unity › Play, `agathelimouzy+societe@gmail.com` → SCI (JBC) (une entreprise avec un SIRET), Réglages › Intégrations.

- [x] **AY1 · La carte** : section « Facture électronique (plateforme agréée) » après Pennylane : « Non activée. », le texte « Obligatoire pour émettre les loyers soumis à TVA à partir du 1er septembre 2027… », le bouton vert **« Activer avec Super PDP »**, et en bas « Mode test (bac à sable)… ». Avec un compte « Utilisateur » : rien (réservé à l'admin).
  - « Non activée : le serveur n'est pas encore réglé » : préalable 2 ; une fenêtre au centre « La fonction « facture-electronique »… » : préalable 4.
- [x] **AY2 · Relier la SCI** : « Activer avec Super PDP » → le navigateur s'ouvre sur Super PDP, ton e-mail déjà rempli → créer ton accès ou te connecter → choisir une entreprise de test (Burger Queen ou Tricatel : en bac à sable, Super PDP ne connaît qu'elles) → accepter → autoriser CIPL → une page « C'est fait : la SCI est reliée à Super PDP… » → revenir dans CIPL : en bas « SCI reliée à Super PDP. », et la carte dit **« En attente de la vérification d'identité… »** (boutons « Actualiser » et « Se déconnecter ») ou, si le bac à sable vérifie tout de suite, **« Active : … »** en vert.
  - Si la page de retour dit autre chose (« Lien d'activation inconnu… », « Super PDP n'a pas donné l'autorisation… ») : me copier son texte. Si Super PDP refuse l'adresse de retour : préalable 1 (l'adresse doit être identique, caractère pour caractère).
- [x] **AY3 · La vérification** : si la carte attend, faire la vérification demandée par Super PDP (bac à sable : peut-être simulée), puis « Actualiser » → « Active : … ».
- [x] **AY4 · Fermer Réglages pendant l'activation** : « Activer avec Super PDP » puis fermer Réglages avant d'avoir fini dans le navigateur → finir chez Super PDP → rouvrir Réglages › Intégrations : la carte montre l'état relié.
- [x] **AY5 · Se déconnecter** : « Se déconnecter » → confirmer → « Facture électronique coupée pour cette SCI. » → la carte repropose « Activer avec Super PDP ». Puis relier de nouveau (AY2) pour l'étape 18b.

## AZ. Étape 18b : les loyers par la plateforme agréée, et « Encaissée » (30/10) — validé le 30/10

Décisions des 29 et 30/10 : un loyer à un locataire **assujetti à la TVA** part aussi par Super PDP (le PDF part toujours par e-mail) ; « Encaissée » (CIPL dit à la plateforme que le loyer est payé) part tout seul quand la facture est marquée payée ; Super PDP injoignable : la facture est marquée « à renvoyer » et CIPL réessaie au prochain lancement ; une pastille dans le suivi dit où en est chaque facture. Bac à sable : factures fictives, rien ne part vraiment chez le locataire.

Préalable :
1. Supabase › Edge Functions › `facture-electronique` : remplacer tout le code par le nouveau `Serveur/fonctions/facture-electronique.ts` → Deploy (« Verify JWT » activé).
2. SCI (JBC) reliée à Super PDP et **« Active »** (parcours AY) ; Réglages › Intégrations › Envoi des factures : **Pennylane décoché** (mode e-mail).
3. Un locataire de test de SCI (JBC) avec le SIRET **00000000200000** (Burger Queen : le bac à sable ne connaît que ses sociétés fictives, et SCI (JBC) y est Tricatel), et l'e-mail `agathelimouzy@gmail.com`.

- [x] **AZ1 · La case** : fiche du locataire de test → sous la rangée « Siret · Code comptable », la case **« Assujetti à la TVA : reçoit les factures électroniques… »** cochée (il a un SIRET), grisée ; « Modifier » → elle se coche et se décoche ; la décocher → « Enregistrer » → elle reste décochée ; la recocher → « Enregistrer ». Un locataire sans SIRET : décochée.
- [x] **AZ2 · L'écran Facturer** : Facturer le loyer de ce locataire → sous les boutons : **« Envoi : plateforme agréée (Super PDP) + copie PDF par e-mail. La phrase de retard y est obligatoire. »** ; la case **« Ajouter la phrase de retard / pénalités de paiement » est cochée et grisée** ; avec la case décochée : « Envoi : e-mail (locataire non assujetti à la TVA : pas de facture électronique). »
- [x] **AZ3 · Le loyer part par la plateforme** : « Sauvegarder et envoyer » → l'e-mail arrive comme avant ; en bas **« Facture transmise à Super PDP. »** ; dans le suivi, la ligne porte **« · Plateforme »** ; dans Super PDP (bac à sable), l'entreprise de test › **Factures de vente** : la facture y est, avec le même numéro.
  - Une fenêtre au centre « Super PDP a refusé la facture… » : me copier tout le « Détail technique » (c'est leur contrôle du format ; je corrige la facture envoyée). La ligne porte alors « · Refusée par la plateforme ».
  - « Plateforme injoignable : nouvel essai au prochain lancement » et « · À renvoyer » : relancer CIPL plus tard (AZ5).
- [x] **AZ4 · « Encaissée »** : sur cette ligne, état → **« Payé »** → en bas **« « Encaissée » déclaré à Super PDP. »** ; la ligne porte **« · Encaissée déclarée »** ; dans Super PDP, la facture affiche le paiement reçu.
- [x] **AZ5 · Au lancement** (si une ligne était « À renvoyer ») : relancer CIPL → au bout d'environ 35 s, en bas « Super PDP : 1 facture(s) transmise(s)… » ; la ligne passe à « · Plateforme ».
- [x] **AZ6 · Non assujetti** : décocher la case (AZ1), facturer et envoyer un loyer → seulement l'e-mail, aucune pastille dans le suivi. Recocher ensuite.
- [x] **AZ7 · Assujetti sans SIRET** : « Modifier » le locataire → vider le SIRET, garder « Assujetti » coché → « Enregistrer » → Facturer : sous les boutons, en orange, **« Envoi : e-mail seulement — ajoutez le SIRET du locataire pour la facture électronique (Super PDP). »** ; la case de la phrase de retard redevient libre. Remettre le SIRET ensuite (sans lui, Super PDP refuse la facture : règle BR-FR-12).

## BA. Variables société : le nom, le SIRET et la TVA de l'entreprise ouverte (30/10)

Avant : « GROUPE CIPL » dans l'objet et la signature de tous les e-mails de facture, et dans les entêtes qui utilisent `{societe.nom}`, même sur une autre SCI. Décisions du 30/10 : nom de Réglages › Entreprise (sinon celui du cloud) ; SIRET et TVA de l'entreprise ; sans SIRET connu, vides (jamais ceux de GROUPE CIPL) ; rien de réglé du tout : GROUPE CIPL comme avant.

Unity › Play, `agathelimouzy+societe@gmail.com` → SCI (JBC).

- [ ] **BA1 · L'entête** : Réglages › Facturation › « Entêtes (modèles de texte) » → une nouvelle entête « Essai société », écrite avec le menu « / » : `{societe.nom} · SIRET {societe.siret} · TVA {societe.tva}` → enregistrer → Facturer le loyer d'un locataire de SCI (JBC), menu « Entête » sur « Essai société » : l'aperçu affiche **« SCI (JBC) · SIRET … · TVA FR … »**, le SIRET étant celui affiché dans Réglages › Entreprise, en groupes de chiffres (format « 123 456 789 00012 ») et la TVA « FR xx xxx xxx xxx » ; plus de « GROUPE CIPL » ni de « 717 220 883 ».
- [ ] **BA2 · L'e-mail** : locataire de test (e-mail `agathelimouzy@gmail.com`) → « Sauvegarder et envoyer » un loyer → l'e-mail reçu a pour objet **« SCI (JBC) — facture 2026/… »** et finit par « Cordialement, SCI (JBC) ».
- [ ] **BA3 · Le nom des Réglages l'emporte** : Réglages › Entreprise, nom changé en « SCI (JBC) essai » → enregistrer → rouvrir Facturer : l'aperçu de l'entête dit « SCI (JBC) essai ». Remettre le nom ensuite.
- [ ] **BA4 · Une autre entreprise** : changer d'entreprise (une autre SCI avec un SIRET, l'entête de BA1 y étant recréée) → l'aperçu montre son nom, son SIRET et sa TVA, plus ceux de SCI (JBC). Une entreprise « particulier » : le nom seul, SIRET et TVA vides.
- [ ] **BA5 · Nettoyage** : supprimer l'entête « Essai société ».

## BB. Étape 18b (suite) : l'e-mail « copie », la notification et la bascule du 1er septembre 2027 (30/10) — validé le 31/10

Décisions du 30/10 : pour un locataire dont la facture part par Super PDP, l'e-mail avec le PDF est une **copie pour information** (la facture électronique fait foi) ; **à partir du 1er septembre 2027** (obligation d'émettre des PME), plus de PDF : la facture part par la plateforme, puis un **e-mail de notification** (modèle modifiable dans les Réglages) prévient le locataire, une fois la facture acceptée. Le PDF lui-même ne change pas (c'est la partie lisible du Factur-X).

Unity › Play, `agathelimouzy+societe@gmail.com` → SCI (JBC) ; Sephora avec le SIRET `00000000200000` et l'e-mail `agathelimouzy@gmail.com`.

- [x] **BB1 · La section** : Réglages › Facturation › **« Modèles d'e-mail »** (sous « Textes fixes », qui n'a plus le bloc de notification) : une phrase d'aide, puis **cinq** modèles pré-remplis, chacun avec son objet, son texte et « M'envoyer un essai » : **E-mail de facture (avec le PDF)**, **E-mail de facture — copie (facture transmise par la plateforme)**, **E-mail de notification — facture transmise par la plateforme**, **Rappel d'impayé**, **Invitation d'un collègue** (passée le 31/10 dans Réglages › Utilisateurs, voir BB10). Dans le texte de la notification, taper « / » : la liste propose « Date d'échéance », « Reste à payer (rappel) » et « Phrase de retard (Réglages) ». Changer un mot de l'e-mail de facture → « Enregistrer » → fermer et rouvrir les Réglages : le changement est gardé.
- [x] **BB2 · Les essais** : « M'envoyer un essai » sous chaque modèle → « Essai envoyé à … » ; cinq e-mails « [Essai] … » arrivent à l'adresse d'expédition (regarder les indésirables) : la facture (« 2026/10003 », « Loyer Février 2026 ») ; la copie (objet « … — copie pour information », « c'est elle qui fait foi ») ; la notification (« 1 000,00 € », « à régler le 01/02/2026 », **sans pièce jointe**) ; le rappel (« il reste 400,00 € à payer », puis ta phrase de retard) ; l'invitation (« Agathe (essai) vous invite à rejoindre SCI (JBC) », code « ESSAI-0000 », le lien Windows, pas de ligne Mac).
- [x] **BB3 · La copie (aujourd'hui)** : Facturer un loyer de Sephora pas encore émis → sous les boutons : « Envoi : plateforme agréée (Super PDP) + **copie PDF** par e-mail… » ; les champs « Objet du message » et « Corps du message » montrent **le modèle « E-mail de facture — copie »** des Réglages, et l'aide dit « La facture part aussi par la plateforme : cet e-mail en joint la copie PDF » ; changer un mot pour cette facture → « Sauvegarder et envoyer » : la confirmation montre le même objet → « Envoyer » → l'e-mail reçu est **exactement** le texte affiché, avec ton mot, le PDF joint ; la ligne porte « · Plateforme ». Un locataire non assujetti : l'« E-mail de facture » habituel.
- [x] **BB4 · Après la bascule (essai)** : me dire « essai bascule » pendant le Play : je fais comme si on était le 1er septembre 2027 (rien d'autre ne change, l'essai s'arrête avec le Play). Facturer le loyer suivant de Sephora → sous les boutons : « Envoi : plateforme agréée (Super PDP), **puis un e-mail de notification** au locataire… » ; **les champs de l'e-mail montrent la notification** (le modèle des Réglages) et l'aide dit « E-mail de notification, sans pièce jointe… » ; changer un mot de la notification **pour cette facture** → « Sauvegarder et envoyer » → fenêtre **« Transmettre la facture par la plateforme ? »** → « Transmettre » → en bas « Facture transmise à Super PDP. » ; **aucun e-mail avec PDF** ; la notification arrive **avec ton mot**, sans pièce jointe ; la ligne porte « · Plateforme ». (Validé le 31/10 avec le modèle des Réglages, avant cette version du formulaire.)
- [x] **BB5 · Correction d'une facture transmise** (validé le 31/10) (Play normal, puis avec « essai bascule ») : sur une facture de Sephora déjà transmise (pastille « · Plateforme »), « Corriger » → les champs de l'e-mail portent la mention « copie » (après la bascule : la notification) → changer le montant → « Sauvegarder et envoyer » → en bas **« Facture transmise à Super PDP. »** ; la ligne devient « … corrigée(1) · Plateforme » ; l'e-mail reçu est la copie (après la bascule : la notification). Dans Super PDP (Tricatel › factures de vente) : la nouvelle facture « …-corrigee-1 », **facture rectificative**, qui cite la facture d'origine. Si Super PDP refuse : me copier le détail.
- [x] **BB6 · Non assujetti** (même essai) : un locataire sans la case « Assujetti » → l'e-mail avec le PDF part comme avant, sans « copie » ni notification.
- [x] **BB7 · Le modèle suit** (Play normal) : Réglages › Modèles d'e-mail, changer l'objet de l'e-mail de facture (« Votre facture {numero} » par exemple) → « Enregistrer » → Facturer le loyer d'un locataire dont tu n'as jamais changé le texte : le nouvel objet y est. Un locataire dont tu as changé le texte garde le sien. Remettre le modèle ensuite.
- [x] **BB8 · Le rappel** : dans le suivi, sur une ligne impayée, « Rappel » → « Ouvrir le brouillon » : le brouillon de ta messagerie suit le modèle (période, n°, montant, échéance, reste à payer, phrase de retard, signé « SCI (JBC) »). Ne pas l'envoyer.
- [x] **BB9 · L'invitation** : Réglages › Utilisateurs → inviter `agathelimouzy+invit@gmail.com` → l'e-mail reçu suit le modèle « Invitation d'un collègue » (ton nom, SCI (JBC), le code, la date, le lien Windows). Retirer l'invitation ensuite.
- [x] **BB10 · L'invitation dans Utilisateurs** (31/10, validé) : Réglages › **Utilisateurs** : sous la liste, la section **« E-mail d'invitation »** (modèle, phrase des variables, « M'envoyer un essai ») et, en bas, le bouton **« Enregistrer »** ; changer un mot → « Enregistrer » → rouvrir : gardé ; « M'envoyer un essai » → l'e-mail avec ton mot. Réglages › Facturation › Modèles d'e-mail n'a plus que quatre modèles (facture, copie, notification, rappel).
