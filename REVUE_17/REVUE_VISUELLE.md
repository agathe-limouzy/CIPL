# Revue visuelle de l'interface — 16/11/2026

Demandée le 16/11 après le lot G : les lots F et G ont corrigé ce qui se mesure (texte coupé, fenêtre hors écran, contraste), pas la
**cohérence** entre les écrans. Cette revue compare les écrans entre eux. Rien n'est corrigé ici, sauf V-00 (un défaut du lot G).

## Méthode

- Play sur SCI (JBC), Immeuble BI2 et Locataire BI2, 1920 × 1080, taille d'affichage 100 %.
- Chaque écran est ouvert par son bouton, capturé, puis relevé par script : titre (taille, couleur, bandeau), carte (taille, fond, bord,
  marges), boutons (ordre, tailles, couleurs), tailles de texte. Outil : `.ai-game-dev/snippets/visuel.cs` (hors git).
- Les captures ont été regardées une à une. Les plus parlantes sont dans `REVUE_17/visuel/`.
- Aucune taille de texte hors de l'échelle du thème (12, 15, 18, 20, 24, 26) sur les écrans relevés.

Écrans faits (40 captures) : choix de l'entreprise ; accueil ; Calcul rapide ; Compte ; PLU ; fiche du bâtiment (résumé et fiche complète) ;
Photos ; Lots ; nouvel objectif ; Transférer ; historiques et formulaires des charges, achats et travaux ; fiche du locataire ; factures de
loyer, de régularisation et de refacturation ; Départ ; Gestion du loyer (« Modalités », « Réviser ») ; menu d'état du suivi ; Réglages
(six onglets) et « Nouveau RIB » ; message d'erreur, confirmation à deux boutons, message du bas.

Limites :
- Une seule taille d'affichage (100 %). Les autres tailles ont été mesurées aux lots F et G.
- Non vus : inscription et connexion, premiers pas, visite guidée, « Choisir l'abonnement », paliers de loyer, « Paiement partiel… »,
  révision du dépôt (la facture répond « aucune révision en attente »), documents du bail.
- « N factures sont à envoyer » n'est pas apparue pendant le balayage : relevée d'après le code et la capture d'Agathe.
- Regardées en image : 18 captures sur 40. Les autres (Lots, Photos, Transférer, Nouveau RIB, régularisation, refacturation, menu d'état,
  message du bas, quatre onglets des Réglages) ne sont connues que par leur relevé chiffré.
- Le réseau a coupé plusieurs fois pendant le balayage (CIPL « hors ligne », réservations de fiche expirées).

## Les fenêtres, côte à côte

| Fenêtre | Titre | Bandeau | Boutons du bas (de gauche à droite) | Fermeture |
|---|---|---|---|---|
| Choisir l'entreprise | 24 blanc | vert, 52 | Se déconnecter | — |
| Compte | 24 blanc | vert, 52 | Fermer · Changer d'entreprise · **Se déconnecter** | « Fermer » |
| Urbanisme · PLU | 24 blanc | violet, 52 | Rechercher | « × » en haut |
| Historique des achats / travaux / charges | 24 clair | bleu / ambre / violet, **76** | + Ajouter… (à gauche) | « × » en haut |
| Ajouter un achat / des travaux | 24 **vert** | **vert pâle**, 57 | **Sauvegarder** · Annuler (à droite) | « Annuler » |
| Nouvel objectif | 24 blanc | bleu nuit, 52 | Annuler · **Enregistrer** (à droite) | « Annuler » |
| Départ du locataire | 24 blanc | ambre, 52 | Fermer · **Enregistrer** (à droite) | « Fermer » |
| Gestion du loyer | 24 blanc | ambre, 54 | **Modifier** · Fermer (deux moitiés) | « Fermer » |
| Révision du loyer | 24 blanc | prune, 54 | **Réviser le loyer** · Fermer (deux moitiés) | « Fermer » |
| Nouvelle charge | **20** noir | **aucun** | Annuler · **Enregistrer** (deux moitiés) | « Annuler » |
| Lots | **20** noir | **aucun** | Annuler · **Enregistrer** (deux moitiés) | « Annuler » |
| Nouveau RIB (saisies des Réglages) | 24 noir | **aucun** | Annuler · **Valider** (deux moitiés) | « Annuler » |
| Transférer | 24 noir | **aucun** | Annuler (pleine largeur) | « Annuler » |
| N factures sont à envoyer | 24 noir | **aucun** | Plus tard · **Tout envoyer** (à droite) | « Plus tard » |
| Calcul rapide | 24 vert foncé | **aucun** (carte vert pâle) | — | **clic en dehors** |
| Message d'erreur / confirmation | 24 noir, centré | **aucun** | OK, ou Annuler · **action** | bouton |
| Photos | — | fond sombre | + Ajouter · Supprimer · Définir comme couverture | « × » en haut |

En gras dans la colonne des boutons : le bouton mis en avant (plein, coloré).

## Écarts

Gravité : **A** se voit tout de suite, **B** se voit en comparant, **C** détail.

### V-00 · Gestion du loyer : les boutons flottaient sous la carte (défaut du lot G)
- En sortant « Modifier » / « Fermer » du défilement (B-13, lot G), je les avais sortis du fond blanc : ils se posaient sur le voile
  sombre, sous la carte (captures `37_gestion_du_loyer_avant.png` et `38_revision_du_loyer_avant.png`). Ma vérification du lot G
  mesurait leur position, pas leur aspect.
- **Corrigé le 16/11** : la rangée reprend le fond blanc et le bas arrondi du corps (`RevisionPanel.EnvelopperDansScroll`) ; 984 tests
  verts ; vu en Play sur les deux volets (captures `f13_gestion_du_loyer_apres.png` et `f14_revision_du_loyer_apres.png`).

### Fenêtres : la structure

| N° | Gravité | Écart | Où |
|---|---|---|---|
| V-01 | A | Pas de bandeau de titre, alors que douze fenêtres en ont un | Nouvelle charge, Lots, Nouveau RIB et les autres saisies des Réglages, Transférer, N factures sont à envoyer, Calcul rapide |
| V-02 | B | Titre en 20 au lieu de 24 | Nouvelle charge, Lots |
| V-03 | B | Bandeaux de quatre hauteurs (52, 54, 57, 76) et de deux styles (plein avec texte blanc ; pâle avec texte vert) | 76 : les trois historiques ; pâle : Ajouter un achat, Ajouter des travaux |
| V-04 | B | Trois façons de fermer : bouton en bas, « × » en haut, clic en dehors sans aucun bouton | « × » : PLU, historiques, Photos ; clic en dehors : Calcul rapide |
| V-05 | A | Ordre des boutons du bas inversé : l'action d'abord, l'abandon ensuite | Ajouter un achat, Ajouter des travaux (« Sauvegarder · Annuler »), Gestion du loyer (« Modifier · Fermer »), Révision (« Réviser le loyer · Fermer ») ; ailleurs « Annuler · action » |
| V-06 | B | Quatre verbes pour la même action : Enregistrer, Sauvegarder, Valider, Modifier | Sauvegarder : achat, travaux, fiches ; Valider : saisies des Réglages ; Modifier : Gestion du loyer |
| V-07 | B | « Annuler » ou « Fermer » à côté d'« Enregistrer », selon la fenêtre | Fermer : Départ, Gestion du loyer ; Annuler : les autres |
| V-08 | B | Boutons du bas tantôt compacts à droite, tantôt en deux moitiés sur toute la largeur ; hauteurs 38, 40, 42 et 44 | compacts : Objectif, Départ, achat, travaux, factures à envoyer ; moitiés : charge, Lots, RIB, Gestion du loyer ; « Fermer » 38 à côté de « Modifier » 42 |
| V-09 | B | Le bouton mis en avant est « Se déconnecter » | Compte |
| V-10 | B | Bouton sur deux lignes | « + Ajouter une charge » (historique, 190 de large) ; « Continuer sans enregistrer » (confirmation) |
| V-11 | C | « CIPL version 1.0.6 » entre deux liens, et liens décalés de 10 px | Compte |
| V-12 | C | Voile de fond de trois opacités (0,42, 0,45, 0,5) | prefabs 0,42 ; fenêtres construites par code 0,45 ; Calcul rapide 0,5 |

### Champs et libellés

| N° | Gravité | Écart | Où |
|---|---|---|---|
| V-13 | A | Libellés écrits sans règle : « Nom du batiment: », « Cadastre : », « Adresse du Batiment : », « Taille du batiment: », « Loyer annuel Total: », « Etat du parking : », « Date du debut de bail : », « Lot Batiment : » (accents manquants, majuscules au milieu, espace avant les deux-points une fois sur deux, deux-points ou non) | fiche complète du bâtiment, fiche du locataire |
| V-14 | B | « Email » et « Siret », alors que le lot F a retenu « E-mail », et que les factures écrivent « SIRET » | fiche du locataire |
| V-15 | B | Titres de section : « General » sans accent ; « Objectif » (locataire) et « Objectifs & améliorations » (bâtiment) | fiche du locataire |
| V-16 | B | Quatre styles de libellé de champ : dans la case (fiches), petit gris au-dessus (factures, Réglages, Départ), 18 au-dessus (Nouvelle charge), gras gris (Gestion du loyer) ; dans « Ajouter un achat », seul « Date de l'achat: » est en gras | — |
| V-17 | B | Deux saisies de date : un champ « JJ/MM/AAAA » ou trois cases « JJ / MM / AAAA » | un champ : factures, charge ; trois cases : achat, Départ, Gestion du loyer, Révision |
| V-18 | B | Trois styles de menu déroulant : blanc bordé avec la lettre « v » ; gris avec chevron ; blanc avec chevron | « v » : factures, Créances, charge, Départ ; gris : Parking, Type de bail, Périodicité, Mode de financement ; chevron : « Tri : alertes » |
| V-19 | B | Deux styles de case à cocher : rond vert, ou petit carré gris | carré : « Provisions » (Gestion du loyer), « Frais auto (8 %) » (achat) ; dans Gestion du loyer les deux styles se suivent |
| V-20 | B | « Loyer annuel Total: 31082,05 € / an » sans séparateur de milliers | fiche complète du bâtiment |
| V-21 | B | « Clique sur « Générer facture » » : tutoiement | aperçu des formulaires de facture |
| V-22 | C | Unités minuscules à côté des valeurs (« m² », « ans », « ca », « % », « € ») | fiches, Gestion du loyer |
| V-23 | C | « Cout total du crédit » sans accent ; « Apport : » avec deux-points, « Mensualité » sans | Ajouter un achat |
| V-24 | C | Le texte du commentaire touche le bord de sa case | fiche du locataire |
| V-25 | C | Message de la carte avec une espace en tête et une coupe avant « : » (« (ex. ⏎ : 12 rue de la Paix ») | fiche complète du bâtiment |

### Accueil et fiches

| N° | Gravité | Écart | Où |
|---|---|---|---|
| V-26 | B | Les quatre boutons de l'en-tête n'ont pas le même style : « Consulter le PLU » violet, bordé, plus étroit ; « Compte » et « Réglages » bleu pâle ; « Calcul rapide » vert pâle | accueil |
| V-27 | B | Les boutons d'en-tête des fiches diffèrent : texte fin gris (bâtiment), texte gras espacé (locataire) ; le bandeau lui-même n'a pas les mêmes marges (4 px et 20 px) | fiche complète du bâtiment, fiche du locataire |
| V-28 | B | Colonne d'état des locataires pas alignée : pastille rose, texte nu, pastille grise, à trois positions | fiche du bâtiment, résumé |
| V-29 | C | Tuiles du haut de largeurs inégales | accueil, fiche du bâtiment |
| V-30 | C | Trois marges gauches dans la carte du haut (ligne « Acquis — · Terrain — … », photo, titre) | fiche du bâtiment, résumé |
| V-31 | C | Pictogrammes illisibles devant « 100 m² », « 3 lots », « Sans parking », « Occupé » | fiche du bâtiment, résumé |
| V-32 | C | 135 px vides sous les deux panneaux | fiche du bâtiment, résumé |
| V-33 | C | Dernière ligne du tableau coupée à mi-hauteur | Rentabilité, fiche complète |
| V-34 | C | Le message du bas se pose sur le contenu ; « 7 bâtiment(s) lu(s) » | accueil, fiches |
| V-35 | C | « Rien ici » très petit et pâle | colonnes vides des objectifs |
| V-36 | C | Noms coupés (« Locataire BJ6 (révision… ») alors que la colonne voisine est presque vide | « À traiter » de la fiche du bâtiment |
| V-37 | C | Champ « Rechercher… » sans bord à côté de « Tri » bordé | accueil, Bâtiments |
| V-38 | C | « rivoli » : l'adresse entière à la place de la ville (adresse sans virgule ni code postal) | accueil, carte du bâtiment |

## Ce qui est cohérent

- Les sections des pages (accueil, fiches, Réglages, formulaires de facture) : un bandeau pâle, un titre de 24, une couleur par famille.
- Les Réglages : libellés, champs, bouton « Enregistrer » en pied, d'un onglet à l'autre (deux onglets regardés sur six).
- Les formulaires de facture entre eux (d'après leurs relevés ; un seul regardé en image).
- Les fenêtres du cloud (Compte, choix de l'entreprise) et les fenêtres récentes construites par code (Objectif, Départ).

## Proposition : un seul modèle de fenêtre

À décider avant de corriger (ce sont des choix d'apparence) :
1. **Bandeau** : toutes les fenêtres en ont un, plein, 52 de haut, titre blanc de 24, à la couleur de leur famille (V-01, V-02, V-03).
2. **Bas de fenêtre** : l'abandon à gauche, l'action à droite ; boutons compacts à droite, 40 de haut (V-05, V-08).
3. **Mots** : « Enregistrer » partout ; « Annuler » quand on abandonne une saisie, « Fermer » quand il n'y a rien à perdre (V-06, V-07).
4. **Fermeture** : toujours un bouton en bas ; le « × » en haut reste pour les listes sans saisie (historiques, Photos, PLU) ; Calcul
   rapide reçoit un bouton (V-04).
5. **Champs** : un seul style de libellé au-dessus du champ, une seule saisie de date, un seul menu déroulant, une seule case à cocher
   (V-16 à V-19). C'est le plus gros chantier : il touche les prefabs.
6. **Libellés des fiches** : accents, minuscules, « : » précédé d'une espace, partout (V-13 à V-15).

## Corrections du 16/11 : les fenêtres

Décisions d'Agathe du 16/11 : bandeau sur toutes les fenêtres sauf les messages au centre ; boutons du bas compacts à droite, l'abandon
puis l'action, 40 de haut ; « Enregistrer » dans les fenêtres (« Sauvegarder » reste sur les fiches et sur « Sauvegarder et envoyer ») ;
« Annuler » à côté d'une action ; « × » pour le Calcul rapide ; « Fermer » mis en avant dans Compte.
Aides communes : `UIFactory.Bandeau` et `UIFactory.PiedFenetre` (test `LotGTests.Revue_visuelle_…`).

| N° | État | Ce qui est fait | Ce qui reste |
|---|---|---|---|
| V-01 | corrigé | bandeau sur Nouvelle charge (violet), Lots, saisies des Réglages, Transférer, Calcul rapide (vert), factures à envoyer (ambre) | les messages au centre n'en ont pas, exprès |
| V-02 | corrigé | titre blanc de 24 | — |
| V-03 | corrigé | historiques : 52 ; « Ajouter un achat » bleu plein, « Ajouter des travaux » ambre plein, 52, titre blanc | Gestion du loyer et Révision : 54 (objets de la scène) |
| V-04 | corrigé | « × » dans le bandeau du Calcul rapide (le clic en dehors marche toujours) | le « × » en haut reste pour les listes sans saisie |
| V-05 | corrigé | l'abandon avant l'action : achat, travaux, Gestion du loyer, Révision | — |
| V-06 | corrigé | « Enregistrer » : achat, travaux, saisies des Réglages ; Gestion du loyer garde « Initialiser / Réinitialiser / Modifier » | Paliers (« Valider ») : fenêtre non vue |
| V-07 | corrigé | « Annuler » : Départ, Gestion du loyer, Révision | Départ garde « Fermer » quand « Annuler le départ » est dans la rangée (deux « Annuler » côte à côte) : à confirmer |
| V-08 | corrigé | compacts à droite, 40 de haut : charge, Lots, saisies des Réglages, Transférer, Gestion du loyer, Révision ; 40 aussi pour achat, travaux, factures à envoyer | achat et travaux : boutons de 160 de large (prefab) ; leur « Annuler », comme celui de Gestion du loyer, est gris pâle sans bord, les autres sont blancs bordés |
| V-09 | corrigé | Compte : « Se déconnecter · Changer d'entreprise · **Fermer** » | — |
| V-10 | en partie | « + Ajouter… » des historiques : 240 de large, une ligne, à droite | « Continuer sans enregistrer » (confirmation) sur deux lignes |
| V-11, V-12 | à faire | — | version entre deux liens ; trois opacités de voile |

Vu en Play et regardé sur capture (`REVUE_17/visuel/f*.png`) : factures à envoyer (construite à vide pour l'essai), Calcul rapide, Compte,
Lots, Nouvelle charge, Ajouter un achat, Gestion du loyer, Révision du loyer, Nouveau RIB. Relevés seulement (chiffres conformes, image non
regardée) : Transférer, historiques, Ajouter des travaux, Départ. 985 tests EditMode verts.

Voir aussi les libellés et l'accueil, ci-dessous.

## Corrections du 16/11 : les libellés

Décisions d'Agathe du 16/11 : libellés des fiches **sans deux-points** (comme les formulaires et les Réglages) ; accents, majuscules et
mots plus justes (« Surface » pour une valeur en m², « Lot », « Début du bail », « Montant emprunté »).

| N° | État | Ce qui est fait |
|---|---|---|
| V-13 | corrigé | 45 libellés dans les prefabs `BatimentPrefab`, `LocatairePrefab`, `Achat from panel`, `TravauxFromPanel`, et « Bail » (`BailFileUI`). Bâtiment : Nom du bâtiment, Cadastre, Acquis le, Adresse du bâtiment, Surface du bâtiment, Surface du terrain, Loyer annuel total, État du parking. Locataire : Nom du locataire, Code comptable, Lot, Surface du lot, Téléphone, Adresse du locataire, Type de bail, Début du bail, Fin du bail. |
| V-14 | corrigé | « SIRET » et « E-mail » sur la fiche du locataire ; le message « renseignez « E-mail d'envoi » » du formulaire de loyer |
| V-15 | corrigé | sections « Général » et « Objectifs » (le texte seul : les objets gardent leur nom, que le code lit) |
| V-20 | corrigé | « 31 082,05 € / an » (`BatimentPrefab`, format N2) |
| V-21 | corrigé | « Cliquez sur « Générer facture » » dans les quatre formulaires |
| V-23 | corrigé | « Coût total du crédit », « Apport », « Taux d'intérêt », « Montant emprunté », « Description de l'achat » |

Vu en Play et regardé sur capture : fiche complète du bâtiment et fiche du locataire (`a03_batiment_fiche_complete_apres.png`, `l02_locataire_apres.png`) ;
plus aucun libellé ne finit par deux-points sur ces deux fiches. Non regardé après correction : les formulaires d'achat et de travaux
(libellés changés dans leur prefab), et le « Bail » de la ligne du document (corrigé après la capture). 985 tests EditMode verts.

Laissés : « Possible amelioration : » (ancien panneau d'objectifs, caché) ; « Siret : » dans le bas de page des factures (texte de
facture, pas un libellé d'écran) ; les phrases « Provision pour charges : 150,00 € / mois », où le deux-points sépare un nom de sa valeur
sur la même ligne ; V-22, V-24, V-25.

## Corrections du 16/11 : l'accueil et les fiches

Décision d'Agathe du 16/11 : les quatre boutons de l'en-tête de l'accueil en vert pâle.

| N° | État | Ce qui est fait ou pourquoi c'est laissé |
|---|---|---|
| V-26 | corrigé | « Consulter le PLU », « Compte », « Réglages », « Calcul rapide » : même fond vert pâle, même texte vert (`GeneralMenuPanel`) |
| V-27 | en partie | boutons d'en-tête de la fiche du bâtiment en gras, comme ceux du locataire (prefab) ; les marges du bandeau (4 et 20) restent |
| V-28 | retiré | ce n'était pas un défaut : la liste a deux colonnes d'étiquettes voulues (présence, puis révision), alignées d'une ligne à l'autre (retour BM7 du 04/11) |
| V-29 | corrigé | tuiles du haut à parts égales : 306 chacune, sur l'accueil et sur la fiche du bâtiment (`BatimentSummaryView.KpiTile`) |
| V-34 | en partie | « 7 bâtiments lus depuis le cloud. » ; le message se pose toujours sur le bas de l'écran |
| V-35 | corrigé | « Rien ici » en 15, gris foncé (`ObjectifsTableau.Note`) |
| V-36 | corrigé | « À traiter » de la fiche du bâtiment : la place de la colonne « Bâtiment » masquée revient au nom du locataire |
| V-37 | corrigé | champ « Rechercher… » des bâtiments : fond blanc et bord, comme « Tri » |
| V-30 à V-33 | à faire | trois marges gauches dans la carte du bâtiment, pictogrammes illisibles, 135 px vides sous les panneaux, dernière ligne du tableau de rentabilité coupée : objets de prefab, à mesurer un par un |
| V-38 | laissé | « rivoli » : son adresse n'a ni virgule ni code postal ; aucune règle sûre ne trouve la ville |

Vu en Play et regardé sur capture : accueil, fiche du bâtiment (résumé et fiche complète) : `a01_accueil_apres.png`,
`a02_batiment_resume_apres.png`, `a03_batiment_fiche_complete_apres.png`. 985 tests EditMode verts.

## Ce qui reste de la revue

- **Styles de champs** (V-16 à V-19) : un seul libellé, une seule saisie de date, un seul menu déroulant, une seule case à cocher. Presque
  tout est dans les prefabs : une séance à part.
- **Détails** : V-11, V-12 (Compte, voiles), V-22, V-24, V-25 (unités, commentaire, message de la carte), V-30 à V-33 (fiche du bâtiment),
  la fin de V-10 (« Continuer sans enregistrer » sur deux lignes) et de V-27 (marges du bandeau).
- **Non vus** : inscription et connexion, premiers pas, visite guidée, choix de l'abonnement, paliers, paiement partiel.

## Corrections du 16/11 : les détails restants

| N° | État | Ce qui est fait ou pourquoi c'est laissé |
|---|---|---|
| V-10 (fin) | corrigé | la carte d'une confirmation s'élargit (380 à 560) pour un libellé long : « Continuer sans enregistrer » sur une ligne (`ConfirmDialog.LargeurCarte`, testée) |
| V-11 | corrigé | Compte : « CIPL version » après les liens ; liens alignés sur les textes |
| V-12 | en partie | voile du Calcul rapide à 0,45 ; les prefabs restent à 0,42 (écart invisible à l'œil) |
| V-22 | en partie | surface du terrain en « m² » (elle disait « ca ») ; les unités restent petites à côté de la valeur : c'est un style, pas un défaut |
| V-24 | corrigé | commentaire : marge de 10 dans sa case (prefab `LocatairePrefab`) |
| V-25 | corrigé | messages de la carte sans espace en tête ; « (ex. : » ne se coupe plus avant le deux-points |
| V-30 | corrigé | ligne « Acquis — · Terrain — … » alignée sur la photo (x = 50 pour les deux) |
| V-27 (fin) | laissé | marges du bandeau d'en-tête (4 et 20) : les deux fiches n'ont pas le même conteneur |
| V-31 | laissé | pictogrammes des pastilles : à redessiner plus grands, pas une retouche de mise en page |
| V-32 | laissé | 135 px vides sous les panneaux du résumé : hauteurs calculées par le code, à revoir avec la mise en page de la fiche |
| V-33 | retiré | le tableau de rentabilité défile : une ligne à moitié visible en bas est normale |

Vu en Play : confirmation d'essai à libellé long (carte 560, une ligne ; retour à 380 pour un libellé court), ligne d'informations du
bâtiment (capture `d01_confirmation_et_batiment_apres.png`, regardée). Non regardés : Compte, commentaire du locataire, message de la
carte, unité du terrain. 985 tests EditMode verts (un test « texte » de `LotDCartesTests` adapté à l'écriture sans espace en tête).

Bilan des 38 écarts : 25 corrigés, 4 en partie (V-12, V-22, V-27, V-34), 2 retirés (V-28, V-33), 7 restants : les styles de champs
(V-16 à V-19, une séance à part) et trois détails laissés (V-31, V-32, V-38).

## Corrections du 16/11 : les styles de champs

Décisions du 16/11 : menus blancs bordés avec un chevron ; case ronde verte partout ; date en trois cases partout ; libellé gris
au-dessus du champ dans les fenêtres, jamais en gras (les fiches gardent le libellé dans la case).

| N° | État | Ce qui est fait |
|---|---|---|
| V-17 | corrigé | date en trois cases « JJ / MM / AAAA » dans les quatre formulaires de facture (« Date » et « Date d'échéance »), « Nouvelle charge » et la révision du dépôt : `ChampDate` (dans `UIFactory.cs`). Son `text` se lit et s'écrit « jj/mm/aaaa » comme l'ancien champ : les contrôles (`SaisieDate`) et les appelants ne changent pas. Une case vide parmi les trois : refus « date incomplète, remplissez le jour, le mois et l'année. » Une case pleine passe à la suivante |
| V-18 | corrigé | menus bâtis par code : un chevron dessiné (`UIFactory.Chevron`) à la place de la lettre « v ». Menus des prefabs (« Périodicité », trimestres, « État du parking », « Type de bail », « Mode de financement », « Durée », « Tri ») : fond blanc, bord `BordureChamp`, chevron gris ; désactivés (fiche en lecture) : fond crème `#F1EFE8` |
| V-19 | corrigé | `UIFactory.CaseRonde` donne aux deux cases carrées l'allure des autres : « Provisions pour charges » (le libellé est à côté de la case et la coche aussi ; le titre « Provisions » au-dessus est masqué ; le texte « Pas de provision pour charges » du lot G disparaît) et « Frais de notaire calculés (8 %) » (avant : « Frais auto (8 %) ») |
| V-16 | en partie | plus aucun libellé de champ en gras : « Gestion du loyer », « Révision du loyer » (titres du prefab en gris `#5F5E5A`, taille 18), achat et travaux (« Date de l'achat », « Date de début », « Mode de financement », « Durée ») ; dans « Gestion du loyer », les libellés bâtis par code passent de 15 à 18 comme ceux du prefab. Reste deux tailles : 18 dans les fenêtres (Réglages, charge, achat, travaux, loyer), 15 dans les quatre formulaires de facture et « Départ » |

Vu en Play sur SCI (JBC), captures regardées : accueil (`c01`), achat avec emprunt (`c02`), travaux, « Nouvelle charge » (`c03`),
« Gestion du loyer » haut et bas (`c04`), « Révision du loyer » (`c05`), révision du dépôt (`c06`), facture de loyer avec son aperçu
(`c07`), régularisation, refacturation, fiche du bâtiment en modification (`c08`), fiche du locataire en lecture (`c09`).
Vérifié par script en Play : refus d'une date et d'une échéance incomplètes dans le loyer, la régularisation et la refacturation ;
le début du numéro suit le mois de la date ; l'échéance proposée suit la date ; l'aperçu porte la date et l'échéance ; un clic sur
« Provisions pour charges » coche et décoche ; une case de date pleine passe à la suivante. Aucune facture émise.
Non vus à l'écran : le formulaire « Révision du dépôt (facture) » (aucune révision en attente sur le locataire d'essai ; même code
que les trois autres), le menu « Type de bail » en modification, le champ du montant à côté de « Provisions pour charges » (il ne
s'affiche que dans une entreprise sans liste de charges). Défaut trouvé et corrigé en route : un menu désactivé devenait gris
foncé (le bord transparaissait). 986 tests EditMode verts (un test neuf : `LotGTests`, la date en trois cases).

Bilan des 38 écarts : 28 corrigés, 5 en partie (V-12, V-16, V-22, V-27, V-34), 2 retirés (V-28, V-33), 3 laissés (V-31, V-32, V-38).

## Corrections du 16/11 : une seule taille de libellé

Décision du 16/11 : « 18 partout ». V-16 passe de « en partie » à « corrigé ».

Soixante libellés de champ bâtis par code passent de 15 (`Role.Donnee`) à 18 (`Role.Libelle`), dans dix fichiers : les quatre
formulaires de facture (« Nom », « Adresse », « SIRET », « Date », « Format du n° de facture », « Texte de présentation », « Aperçu »,
« Période facturée », « Loyer HT », provisions, « Taux de TVA », « Date d'échéance », « Phrase de règlement », « RIB CIPL », « Objet du
message », « Corps du message », et les libellés propres à chaque formulaire), « Mention TVA » (`MentionTva`), « Départ du locataire »,
la révision du dépôt, « Nouvel objectif », les paliers, et « Vous gérez des biens en tant que » de l'inscription. `ChampDate.Avec`
pose son libellé en 18 par défaut. Ce qui garde 15 : les lignes de tableau et de total (« Total HT », « TVA 20 % »), les aides, les
états, les mots dans une rangée (« Année : », « tous les »).

Vu en Play sur SCI (JBC), captures regardées : facture de loyer, haut et milieu (`e01`, `e02`), « Départ du locataire » (`e03`),
« Nouvel objectif » (`e04`), révision du dépôt. Aucun libellé ne déborde de sa colonne dans la facture de loyer (mesuré). Non vus à
l'écran : régularisation, refacturation et révision du dépôt (facture) en 18 (même code que le loyer), les paliers, l'inscription.
986 tests EditMode verts.

Reste noté, sans décision : dans « Départ du locataire », « Nouvel objectif » et « Paiement partiel », la date garde le bloc des
fiches (libellé dans la case), parce que ces fenêtres clonent le champ de la fiche du locataire.

Bilan des 38 écarts : 29 corrigés, 4 en partie (V-12, V-22, V-27, V-34), 2 retirés (V-28, V-33), 3 laissés (V-31, V-32, V-38).

## Corrections du 17/11 : la date des fenêtres

Décision : aligner. « Départ du locataire » (dernier jour, état des lieux), la cession du bail, « Nouvel objectif » (échéance) et
« Paiement partiel » (versement, remboursement) prennent la date en trois cases avec le libellé au-dessus (`ChampDate.Colonne`), à
la place du clone du champ de la fiche (libellé dans la case). Les deux copies du clonage disparaissent (`DepartPanel.ChampDate`,
`ObjectifPanel.ChampDate`). `ChampDate` reçoit `Vide()` et `LireDate()`, mêmes noms que le champ des fiches : les contrôles et leurs
messages ne changent pas. Seul écart : l'année sur deux chiffres est lue (« 27 » = 2027), comme dans les factures.

Vu en Play sur SCI (JBC), captures regardées : « Départ » (`g01`), « Nouvel objectif » (`g02`), « Paiement partiel » ouvert par le
menu de l'état (`g03`). Essayé par script, sans rien enregistrer : « Enregistrer » sans dernier jour → refus, fenêtre ouverte ; le
dernier jour saisi → « Dépôt à restituer avant le … » suit ; échéance d'objectif incomplète → refus ; « Ajouter » un versement avec
le mois vide → refus, aucun versement ajouté ; le locataire d'essai n'a ni date de sortie ni versement après les essais. Non vus à
l'écran : la cession du bail et le remboursement (même appel). 986 tests EditMode verts (le test de la date en trois cases couvre
`Vide`, `LireDate` et `Colonne`).

Restent au format des fiches, exprès : les dates des fiches elles-mêmes, et « Du » des paliers (une date calculée, affichée).

## Écrans vus le 17/11 sur un bâtiment d'essai

Pour voir les fenêtres restées hors de la revue, un bâtiment « Essai revue visuelle » est créé dans SCI (JBC) par les fonctions
de l'application (`BatimentManager.RestoreBatiment`, relu après `DemarrageCloud.Recharger`) : « Essai Dépôt » (dépôt révisé en
attente de sa facture, un loyer de septembre payé 100 € de trop) et « Essai Paliers » (deux paliers). Ni e-mail ni SIRET : rien ne
peut partir. Il reste en place, pour rejouer BZ11.

Vus, captures regardées : fiche du locataire en « Modifier » (`h01`, menu « Type de bail » blanc bordé), cession du bail (`h02`),
remboursement (`h03`), facture de révision du dépôt, haut, milieu et bas (`h04`, `h05`), régularisation et refacturation en 18,
« Gestion du loyer » d'un bail à paliers avec la case « Provisions pour charges » décochée (`h06`), « Paliers de loyer » (`h07`),
Réglages › Abonnement (`h08`), connexion, inscription, mot de passe oublié, invitation et code (`h09`, `h10`, affichés sans rien
envoyer puis détruits), une étape de la visite guidée (`h11`, « Vos RIB », quittée aussitôt).

| N° | État | Écart |
|---|---|---|
| V-39 | corrigé | après « Modifier » puis « Sauvegarder », les boutons de l'en-tête changeaient d'ordre : « Modifier · Supprimer · Transférer · Départ du locataire » au lieu de « Modifier · Départ du locataire · Transférer · Supprimer » (fiche du bâtiment : « Supprimer » avant « Transférer »). Cause : `SetSiblingIndex(index du voisin)` sur un bouton déjà placé avant ce voisin. `UIFactory.PlacerAvant`, testé |
| V-40 | corrigé | « Paliers de loyer » : deux boutons en pleine largeur « Fermer » / « Valider » → « Annuler » puis « Enregistrer les paliers », compacts à droite (décision du 17/11) |
| V-41 | corrigé | révision du dépôt (carte Dépôt) : « Fermer » / « Réviser » en pleine largeur → « Annuler » puis « Réviser » (ou « Initialiser »), compacts à droite |
| V-42 | noté | fiche du locataire en « Modifier » : les unités « ans » et « % » sont loin de leur valeur (à droite, plus bas) |
| V-43 | noté | fiche du locataire en « Modifier » : le champ blanc du commentaire est plus étroit que sa case |
| V-44 | noté | facture de révision du dépôt : la note rouge du bas cite un bouton « Révision dépôt de garantie » ; sur la fiche il s'appelle « Réviser » (carte Dépôt) |
| V-45 | noté | Réglages › Abonnement : les compteurs de licences « - 2 + » ne sont pas alignés d'une ligne à l'autre (ils suivent la longueur du texte) |
| V-46 | noté | inscription et mot de passe oublié : « Retour » à gauche, l'action à droite ; ailleurs « Annuler » puis l'action, à droite |

Vérifié en Play après correction, captures regardées : en-tête de « Essai Dépôt » identique après deux « Modifier » / « Sauvegarder » (`h13`), et celui du bâtiment d'essai aussi (« Modifier · Transférer · Supprimer ») ; « Paliers de loyer » : « Annuler » 82 × 40 et « Enregistrer les paliers » 222 × 40, à droite (`h07`) ; révision du dépôt : « Annuler » et « Réviser », à droite (`h12`). Données des deux fiches identiques avant et après. 987 tests EditMode verts (un test neuf : `LotGTests`, un bouton replacé avant son voisin garde sa place).

Toujours pas vus : la carte « Premiers pas » de l'accueil (il faut une entreprise neuve), la page de paiement de l'abonnement
(« Continuer vers le paiement » n'est pas cliqué), le champ du montant à côté de « Provisions pour charges » (seulement dans une
entreprise sans liste de charges).

Bilan : 46 écarts, 32 corrigés, 4 en partie (V-12, V-22, V-27, V-34), 2 retirés (V-28, V-33), 3 laissés (V-31, V-32, V-38), 5 notés
sans décision (V-42 à V-46).

## Corrections du 17/11 : les cinq écarts notés

| N° | État | Ce qui est fait |
|---|---|---|
| V-42 | corrigé | la cause n'était pas la place des unités : les champs de la fiche du locataire n'avaient pas de bord en « Modifier » (blanc cassé sur crème), alors que ceux de la fiche du bâtiment en ont un. Bord `#D3D1C7` posé sur les onze champs du prefab `LocatairePrefab` (les champs « Durée » et « Période ferme », clonés, suivent) : « ans » et « % » se lisent maintenant juste après leur case |
| V-43 | corrigé | le commentaire prend toute la largeur de sa case (le groupe vertical du prefab pilote la largeur de ses enfants) |
| V-44 | corrigé | la note du bas de la facture du dépôt dit « pour le changer : carte Dépôt, « Réviser » » (`FactureDepotPanel`) |
| V-45 | corrigé | Réglages › Abonnement : le nombre de licences et le texte « licences · N utilisées » ont une largeur fixe ; « - 2 + » tombe au même endroit sur chaque ligne (`SectionAbonnement.LignesEntreprises`) |
| V-46 | corrigé | inscription et mot de passe oublié : « Retour » à droite, juste avant l'action (`EcransCloud`) |

Vu en Play sur SCI (JBC), captures regardées : fiche « Essai Dépôt » en « Modifier » (`i01`), Abonnement (`i02`), inscription et mot
de passe oublié (`i03`, `i04`), bas de la facture de révision du dépôt (`i05`). 987 tests EditMode verts.

Bilan : 46 écarts, 37 corrigés, 4 en partie (V-12, V-22, V-27, V-34), 2 retirés (V-28, V-33), 3 laissés (V-31, V-32, V-38).

## Corrections du 17/11 : les trois détails laissés, et une société d'essai

| N° | État | Ce qui est fait |
|---|---|---|
| V-31 | corrigé | les pictogrammes des pastilles du résumé (« 100 m² », « 3 lots », « Sans parking », « Occupé ») passent de 11 à 16 px (prefab `BatimentPrefab` ; les images font 48 px) |
| V-32 | corrigé | la rangée « À traiter » / « Locataires » du résumé prend la hauteur qui reste dans la vue : les deux panneaux descendent jusqu'en bas (`BatimentSummaryView.RemplirLaVue`, `HauteurPourRemplir` testée). Quand le contenu dépasse déjà, rien ne change : la page défile |
| V-38 | corrigé | décision du 17/11 : quand la ville ne se devine pas (ni virgule ni code postal, et un numéro de rue ou plus de trois mots), la carte du bâtiment montre « N lots » seul ; même chose sans adresse (« 0 lot » au lieu de « — · 0 lot »). `BuildingRowUI.Ville` et `VilleEtLots`, testées ; dans le PLU, la commune estimée reste vide au lieu d'un tiret |
| V-47 | corrigé | carte « Premiers pas » : le bandeau et l'encadré « Prochaine étape » avaient des coins carrés ; arrondis comme le reste (`PremiersPas`) |

Société d'essai : « Essai revue visuelle (à supprimer) », ajoutée au groupe par Réglages › Abonnement › « Ajouter une entreprise »
(particulier, sans SIRET ; le groupe est en essai, rien n'est facturé), avec le bâtiment d'exemple « Exemple · Les Tilleuls ». Elle
a permis de voir : la fenêtre « Ajouter une entreprise » (`j03`), la carte « Premiers pas » (`j04`, après correction ; la carte est
masquée dans le compte : affichée en mémoire le temps de la capture, sans toucher au réglage), le montant à côté de « Provisions pour
charges » dans une entreprise sans liste de charges (`j05`). Vu aussi sur SCI (JBC) : l'accueil (`j01` : « rivoli » → « 1 lot »,
« pubelle » → « 0 lot ») et le résumé d'Immeuble BI2 (`j02` : pictogrammes lisibles, panneaux jusqu'en bas, sans défilement).
987 tests EditMode verts.

Toujours pas vue : la page de paiement de l'abonnement (« Continuer vers le paiement » n'est pas cliqué).

Bilan : 47 écarts, 41 corrigés, 4 en partie (V-12, V-22, V-27, V-34), 2 retirés (V-28, V-33). Plus rien de laissé.

## Corrections du 17/11 : les quatre écarts « en partie »

| N° | État | Ce qui est fait |
|---|---|---|
| V-12 | corrigé | le voile sombre des prefabs passe de 0,42 à 0,45, comme les fenêtres bâties par code (`Achat from panel`, `TravauxFromPanel`, `Investissement List Panel`, `Revision Loyer`, et la fenêtre de confirmation de la scène) |
| V-22 | corrigé | décision du 17/11 : les unités (« m² », « ans », « % », « € ») passent de 12 à 15 px, dans la source `Text & Input` et dans les champs propres aux prefabs (18 textes) ; aucune taille surchargée sur une instance |
| V-27 | corrigé | la fiche complète du bâtiment a les marges de la fiche du locataire : bandeau de x = 19 à 1893, à 59 du haut (locataire : 20 à 1882, à 60). Marges du contenu du prefab : 4 → 19 sur les côtés, 4 → 9 en haut |
| V-34 | accepté | décision du 17/11 : le message du bas reste en bas, au milieu. Il disparaît seul après 8 secondes ; le texte « (s) » était déjà corrigé |
| V-48 | corrigé | vu avec les nouvelles marges : le fond de la fiche complète du bâtiment était gris (un sprite gris d'Unity sur la couleur crème) ; sprite retiré, le fond est le crème de la page |

Petits restes repris le même jour : « Charges concernées (quote-part du locataire) » sans deux-points (régularisation) ; deux
commentaires du code citaient l'ancien nom de bouton « Révision dépôt de garantie ». Constat sans changement : le formulaire d'un
palier a déjà des boutons de 40 px (ma note disait 44) ; le texte d'un menu bâti par code reste en 18 et celui d'un menu des prefabs
en 20 (chacun suit les champs qui l'entourent).

Vu en Play sur SCI (JBC), captures regardées : fiche complète d'Immeuble BI2 (`k01`), fiche de « Locataire BI2 » (`k02`), « Ajouter un
achat » (voile mesuré à 0,45). 987 tests EditMode verts.

Bilan final : 48 écarts, 45 corrigés, 1 accepté par décision (V-34), 2 retirés (V-28, V-33). Seule la page de paiement de
l'abonnement n'a pas été vue.

Décision du 17/11 : dans « Départ du locataire », le bouton reste « Fermer » quand « Annuler le départ » est affiché (deux « Annuler »
côte à côte prêteraient à confusion) ; pas de croix dans les fenêtres de saisie, la croix blanche du bandeau reste réservée aux
fenêtres de consultation.
