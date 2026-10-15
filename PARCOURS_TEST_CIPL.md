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
