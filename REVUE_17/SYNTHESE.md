# Revue 17 — qualité du code, performances et interface, avant le passage en réel (02-03/11/2026)

Demande du 02/11 : « une grosse revue de la qualité du code, vérifier si tout est bon, s'il y a moyen que tout fonctionne plus vite, et une revue de l'interface : éléments, couleurs, cases à cocher, boutons, textes qui ne dépassent pas de leur zone, pas de superposition, tout visuel et clair, et le zoom qui fonctionne encore avec tous les menus ».

Méthode (décisions du 02/11) : comme la 16c, onze relecteurs en parallèle, un par domaine, en lecture seule, chaque défaut avec gravité, endroit, scénario, correction et test ; en plus, un domaine **Performances** (D9), un domaine **Interface** (D10) et un **balayage en Play** fait par la session principale ([BALAYAGE_PLAY](BALAYAGE_PLAY.md) : chaque écran ouvert par le code, textes qui débordent et éléments qui se chevauchent détectés automatiquement, captures aux zooms 0,9 à 1,75, sur SCI (JBC), rien enregistré ni envoyé). Les relecteurs ont été coupés trois fois par la limite d'utilisation et repris là où ils s'étaient arrêtés.

Gravités : **critique** = perte ou fuite de données, faille, facture fausse ou illégale, argent faux ; **majeur** = bug qu'un client verra, ou faiblesse qui arrivera en usage normal (D9 : lenteur sensible ; interface : texte coupé, chevauchement, écran qui ne tient pas au zoom) ; **mineur** = lisibilité, code mort, petite incohérence.

**État au 03/11 : les onze domaines sont rendus et le balayage en Play est fait sur l'essentiel des écrans. 173 défauts signalés, 10 trouvés deux fois (fusionnés) : 163 défauts — 7 critiques, 63 majeurs, 93 mineurs.**

Vérification : la session principale a relu dans le code les sept critiques (traçage complet par leurs relecteurs, confiance haute) et contrôlé elle-même les trois plus lourds : `FactureElectronique.Cle` prend le numéro de base, que `MarquerCorrige` ne change pas (D5-01) ; `ReglageData.basDePage` d'usine porte le SIRET et la TVA de CIPL (D3-01) ; `EcritureCloud._enAttente` n'est écrit nulle part et la garde de fermeture ne le lit pas (D8-01). Les majeurs sont revérifiés au moment de leur correction. Détail de chaque défaut dans le fichier de son domaine : [D1](D1.md) Serveur et sécurité, [D2](D2.md) Cloud et synchronisation, [D3](D3.md) Abonnement et comptes, [D4](D4.md) Facturation : émission et envoi, [D5](D5.md) Facturation : plateforme agréée et TVA, [D6](D6.md) Facturation : écrans et PDF, [D7a](D7a.md) Locataires, bâtiments, Pappers, [D7b](D7b.md) Révision, objectifs, travaux, achats, PLU, cartes, [D8](D8.md) Écrans généraux, outils, cycle de vie et couverture de tests, [D9](D9.md) Performances, [D10](D10.md) Interface (avec l'inventaire des écrans et les points « à vérifier en Play »), [BALAYAGE_PLAY](BALAYAGE_PLAY.md) (défauts B-xx).

## À trancher ou vérifier en priorité

- **D1, à vérifier (deviendrait critique)** : un compte créé d'avance au nom d'un futur invité. Si Supabase ne ferme pas les autres sessions quand le mot de passe change, celui qui a créé le compte garde l'accès à tout ce que l'invité rejoindra. Essai à deux postes décrit dans D1.
- **Passage en réel** : D1-02 / D3-11 (identifiants Stripe de test dans `groupes`) et D5-09 (factures du bac à sable Super PDP) demandent un nettoyage de la base, à ajouter à la liste de l'étape 17.
- **Avis de l'expert-comptable** : D6-03 (mentions obligatoires du PDF), D7b-11 (date d'effet d'une révision IRL faite en retard), D5-11 (signe d'« Encaissée » pour un avoir).

## Ordre des corrections

1. **Critiques** (7), chacun avec son test.
2. **Majeurs**, par thème : argent, abonnement et passage en réel ; facturation ; données, cloud et comptes ; performances (mesurer d'abord, protocole dans D9) ; interface.
3. **Mineurs**.

Les corrections côté serveur (SQL, fonctions) demandent un script SQL à passer dans Supabase et un redéploiement, faits par l'utilisatrice.

## Critiques (7)

| N° | Domaine | Défaut | Aussi trouvé par | Corrigé |
|---|---|---|---|---|
| D2-01 | D2 | Une version plus ancienne de CIPL (« Plus tard ») réécrit fiches et Réglages sans les champs ajoutés depuis : ils sont effacés sur le serveur (ex. `tauxTva`) | || 03/11 (code et test ; SQL etape17a ; parcours BL1) |
| D3-01 | D3 | « Ajouter une entreprise » donne à la nouvelle SCI les Réglages d'usine de CIPL : ses factures portent le SIRET, la TVA, le siège et le logo de CIPL | D6-02 || 03/11 (code et test ; SQL etape17a ; parcours BL2) |
| D5-01 | D5 | La correction d'une facture transmise ne part jamais à Super PDP (clé = numéro de l'originale), mais CIPL affiche « transmise » | || 03/11 (code et test ; parcours BL3) |
| D5-02 | D5 | Une correction devenue négative, ou la correction d'un avoir, part en avoir du nouveau montant : la plateforme additionne au lieu de remplacer | || 03/11 (décision : refus expliqué ; code et test ; parcours BL3) |
| D6-01 | D6 | Une charge « Tous » saisie avec un seul locataire n'a pas de répartition : chaque locataire arrivé ensuite la paie en entier | || 03/11 (code et test ; parcours BL4) |
| D6-03 | D6 | Mentions obligatoires absentes du PDF : escompte jamais, n° de TVA du vendeur absent pour un inscrit, pénalités et 40 € retirables par une case | || 03/11 (décision : case grisée pour un professionnel ; code et test ; parcours BL5) |
| D8-01 | D8 | Hors ligne, une modification « enregistrée au retour de la connexion » est perdue si l'on ferme CIPL avant ce retour, sans avertissement | || 03/11 (décision : avertissement à la fermeture ; code et test ; parcours BL6) |

## Majeurs (63)

| N° | Domaine | Défaut | Aussi trouvé par | Corrigé |
|---|---|---|---|---|
| D1-01 | D1 | Une entreprise ajoutée à un groupe abonné n'est facturée que si un second appel réussit ; rien ne rattrape l'écart | | |
| D1-02 | D1 | Passage en réel : les identifiants Stripe de test restent dans `groupes` (paiement et portail bloqués, ou abonnement gratuit) | D3-11 | |
| D1-03 | D1 | « Se déconnecter » ferme la session sur tous les PC de la personne ; le PC resté ouvert ne le dit jamais | D3-03 | |
| D1-04 | D1 | `creer_entreprise_import` ouvre un essai à un compte dont l'adresse n'est pas prouvée (D3-06 de la 16c contourné) | | |
| D2-02 | D2 | Le fusible des suppressions (BF15) bloque pour toujours le renommage ou la suppression d'une fiche qui porte plus de la moitié des documents | | |
| D2-03 | D2 | Un document créé juste avant la fermeture ne part au cloud qu'au prochain lancement de ce poste | | |
| D2-04 | D2 | « Changer d'entreprise » sans réseau arrête la synchronisation jusqu'au prochain lancement, sans rien montrer | | |
| D2-05 | D2 | Un refus sur une fiche arrête l'envoi des fiches suivantes du même bâtiment | | |
| D2-06 | D2 | La liste des documents est lue en une page plafonnée (1 000) : suppressions défaites, documents jamais reçus, renvois toutes les 20 s | D9-05 | |
| D3-02 | D3 | Le webhook peut perdre la répartition des licences choisie au paiement | | |
| D3-04 | D3 | Le titulaire d'un groupe retiré par un autre admin ne peut plus jamais « Créer mon compte » | | |
| D3-05 | D3 | « Ajouter une entreprise » annonce +28 € HT par mois, Stripe facture +43 € HT | | |
| D3-06 | D3 | Les factures Stripe de CIPL n'ont ni l'adresse du client ni son identifiant | | |
| D4-01 | D4 | Le destinataire n'est pas mémorisé sur la facture : après une cession, envoi différé et rappel partent au cessionnaire | | |
| D4-02 | D4 | « À faire » et « Automatique » dans le menu de la pastille dé-émettent une facture numérotée | | |
| D4-03 | D4 | Une facture payée corrigée à un autre montant reste « Payé » : l'écart n'est ni réclamé ni remboursé | | |
| D4-04 | D4 | Changement de périodicité en cours d'année : les loyers émis sont réétiquetés | | |
| D5-03 | D5 | TVA arrondie autrement sur le PDF et dans le XML : un centime d'écart à 10 % ou 5,5 % | | |
| D5-04 | D5 | Un refus définitif d'« Encaissée » bloque les factures du locataire et le rattrapage de tous les suivants | | |
| D5-05 | D5 | Après le 1/9/2027, une facture préparée sans facture électronique part quand même par e-mail seul | | |
| D5-06 | D5 | Corriger une facture déclarée « Encaissée » laisse l'encaissement de l'ancienne déclaré | | |
| D5-07 | D5 | Correction hors plateforme d'une régul ou refacturation transmise : rien ne prévient | | |
| D5-08 | D5 | Corriger une ancienne facture après un changement de taux : le taux d'aujourd'hui est proposé, l'ancien remis se fige | | |
| D5-09 | D5 | Passage en réel : les factures du bac à sable gardent leur identifiant fictif et leurs réservations | | |
| D5-10 | D5 | Régul et refacturation par la plateforme : l'e-mail est une facture, pas la « copie » annoncée | | |
| D5-11 | D5 | « Encaissée » d'un avoir part en montant négatif (signe d'annulation) | | |
| D6-04 | D6 | « Facturer le loyer » rouvre la dernière période facturée : « Sauvegarder et envoyer » envoie une correction | | |
| D6-05 | D6 | Une date ou une échéance illisible est remplacée sans rien dire | | |
| D6-06 | D6 | Le dépôt de garantie ignore la TVA du bail : calcul à 20 % et « soumis à la TVA » d'office | D7a-02 | |
| D6-07 | D6 | Les provisions des listes de charges restent figées sur la dernière facture (D6-04 de la 16c incomplet) | | |
| D6-08 | D6 | Variables du menu « / » vides ou imprimées telles quelles (`{echeance}`, `{depot.*}`, `{montant}`) | | |
| D6-09 | D6 | Supprimer une charge déjà facturée ne demande qu'une confirmation | | |
| D7a-01 | D7a | Départ repoussé ou annulé après un dernier loyer au prorata : la fin de période n'est jamais facturée | | |
| D7a-03 | D7a | Cession prévue : le loyer de la première période après la cession part au cédant | | |
| D7a-04 | D7a | Cession datée appliquée à l'ouverture sans verrou : dossier renommé avant l'accord du serveur | D2-08 | |
| D7a-05 | D7a | Un remplaçant pas encore arrivé compte en double (surface, lots, loyers) | | |
| D7a-06 | D7a | Date d'acquisition jamais saisie : bâtiment « acquis le 01/01/0001 », « Total gagné » multiplié par 2 025 | | |
| D7a-07 | D7a | Un refus Pappers arrête toute la tournée : les procédures BODACC suivantes ne sont plus signalées | | |
| D7a-08 | D7a | Photos en pleine résolution, chargées sur le fil principal et gardées sans limite | D9-06 | |
| D7a-09 | D7a | Supprimer un locataire qui a des factures efface son suivi pour de bon | | |
| D7b-01 | D7b | Révision : le trimestre proposé a un an de retard pour certains baux ; un trimestre égal à la référence est accepté sans effet | | |
| D7b-02 | D7b | Achat : « frais de notaire automatiques » coché à chaque ouverture ; modifier un achat remplace les vrais frais par 8 % | | |
| D8-02 | D8 | Une confirmation qui s'ouvre pendant un message d'erreur l'efface : sa suite ne s'exécute jamais | | |
| D8-03 | D8 | La fermeture ne voit pas un envoi de facture en cours : la facture peut repartir au lancement suivant | | |
| D8-04 | D8 | `SaisieNumerique` lit « 2.500.000 » comme 2 500 | | |
| D9-01 | D9 | À chaque ouverture d'entreprise, toutes les fiches de tous les locataires sont construites en une image (gel estimé 5 à 15 s) | | |
| D9-02 | D9 | `AppIconResizer` et `AppSectionColors` reparcourent toute l'interface deux fois par seconde | | |
| D9-03 | D9 | Accueil : « Créances » recrée une ligne d'objets par facture due à chaque retour | | |
| D9-04 | D9 | Un changement d'un collègue reconstruit tout le bâtiment ; après une coupure du direct, tous les bâtiments | | |
| D9-07 | D9 | Chaque PDF fige l'application (Edge démarré à froid, attendu sur le fil principal) | | |
| D9-08 | D9 | Ouverture d'une entreprise : neuf allers-retours réseau en série, données décodées 4 fois et encodées 6 fois | | |
| D9-09 | D9 | La mémoire ne redescend jamais (anciens bâtiments gardés, cartes non libérées) | | |
| D10-01 | D10 | Fenêtre « Nouvelle charge » plus haute que l'écran à 150 et 175 % : « Enregistrer » et « Annuler » disparaissent ; d'autres fenêtres sans hauteur maximale | | |
| D10-02 | D10 | Rien ne se fait au clavier : ni Tab entre les champs, ni Échap pour fermer ; Entrée dans trois fenêtres seulement | | |
| D10-03 | D10 | Zoom par défaut à 100 % quel que soit l'écran : sur un portable de 1366 px, pastilles à 8,5 px et données à 10,7 px | | |
| D10-05 | D10 | Menus déroulants : une option longue passe sur trois lignes et chevauche ses voisines | | |
| D10-06 | D10 | Formulaire de charge : date mal tapée remplacée sans message, nom vide accepté | | |
| B-01 | Interface | À 125 % et plus, le bouton « + » (nouveau bâtiment) sort de l'écran (barre d'onglets de largeur fixe) | | |
| B-02 | Interface | Montants au format anglais sur l'accueil, le bâtiment, les locataires, la rentabilité, le calcul rapide (« 87,419 € », « 73.6 % ») | D6-13, D8-07 (format), D10-04 | |
| B-03 | Interface | Aperçu de facture : « Ajuster » s'affiche « Aju / ste / r » (barre de zoom sans taille), dans les quatre formulaires | | |
| B-10 | Interface | L'avertissement de surfaces bloque à chaque ouverture et fermeture d'un formulaire, parfois deux fois | | |
| B-15 | Interface | Fenêtre « Compte » : les trois boutons du bas se chevauchent, à tous les zooms | | |
| B-17 | Interface | Historique des charges : les noms longs recouvrent le montant, le statut recouvre le locataire | | |

## Mineurs (93)

| N° | Domaine | Défaut | Corrigé |
|---|---|---|---|
| D1-05 | D1 | Quota Pappers contourné par des appels simultanés | |
| D1-06 | D1 | Transfert : fiches homonymes de la destination écrasées sans regarder leurs verrous | |
| D1-07 | D1 | Webhook : une panne pendant la vérification applique l'événement de l'ancien abonnement | |
| D1-08 | D1 | `detenteur_autre` appelable par tout compte | |
| D1-09 | D1 | Le fichier du logo reste modifiable par tout membre | |
| D1-10 | D1 | Restes : empreinte non contrôlée dans `documenter`, expiration du jeton à l'horloge du PC, pas de plafond par entreprise pour les cartes | |
| D2-07 | D2 | Synchronisation des documents suspendue : seulement au journal, sans issue | |
| D2-09 | D2 | Changement d'entreprise, retrait ou déconnexion pendant un envoi de fond : « Envoyé » non enregistré | |
| D2-10 | D2 | Réponse perdue après une écriture réussie : refus « modifié par » soi-même au retour | |
| D2-11 | D2 | Chez les collègues, un bâtiment supprimé ou renommé laisse ses dossiers vides | |
| D2-12 | D2 | Dossier des documents absent au démarrage (disque externe) : remplacé pour toujours sans le dire | |
| D2-13 | D2 | Réglages refusés pour un autre motif : la modification reste sur ce PC et sert à ses factures | |
| D2-14 | D2 | Transfert : documents choisis par un préfixe sensible à la casse | |
| D2-15 | D2 | Export et transfert ne filtrent pas les chemins des documents (D1-01 de la 16c) | |
| D3-07 | D3 | Relances épuisées : abonnement oublié en base alors qu'il vit chez Stripe | |
| D3-08 | D3 | « J'ai reçu une invitation » : l'adresse corrigée est ignorée | |
| D3-09 | D3 | 3 licences par entreprise proposées d'office, même pour une personne seule | |
| D3-10 | D3 | Textes faux ou périmés (« Réglage › Abonnement », « Paiement enregistré » sans paiement…) | |
| D3-12 | D3 | L'attente de paiement survit au changement de groupe | |
| D4-05 | D4 | Délais propres d'un locataire ignorés par le suivi, la pastille d'année, l'archivage et la plateforme | |
| D4-06 | D4 | Régul et refacturation regroupées : lignes secondaires d'une correction avec l'ancienne échéance et l'ancien RIB | |
| D4-07 | D4 | Fermeture forcée du panneau pendant l'envoi : coroutine tuée entre l'e-mail et l'enregistrement | |
| D4-08 | D4 | Le dépôt n'enregistre pas son taux de TVA sur la ligne : TVA à 0 € dans les e-mails | |
| D4-09 | D4 | `Preparer` appelé deux fois pour une régul et un dépôt, refus de la seconde non lu | |
| D4-10 | D4 | « Corriger » seulement pour le loyer ; « Refaire » ailleurs pour le même geste | |
| D5-12 | D5 | « Encaissée » rattrapé après une panne : daté du jour du nouvel essai | |
| D5-13 | D5 | Listes de provisions : le XML arrondit chaque ligne, le PDF le total | |
| D5-14 | D5 | Un avoir peut citer des loyers jamais partis ou refusés | |
| D5-15 | D5 | Avoir : le XML cite les factures corrigées, le PDF non | |
| D5-16 | D5 | Pastille « À renvoyer » pendant une transmission normale | |
| D5-17 | D5 | Taux de TVA libre, sans contrôle, aux trois niveaux | |
| D5-18 | D5 | Décompte de sortie avec retenues soumises : jamais en facture électronique, « sans TVA » affiché | |
| D5-19 | D5 | Code mort : la ligne « locataire non assujetti » ne s'affiche jamais | |
| D6-10 | D6 | Le numéro décidé reste en mémoire après un refus ou une annulation | |
| D6-11 | D6 | Loyer à total négatif : un « AVOIR » dit « SOMME À NOUS RÉGLER » | |
| D6-12 | D6 | Phrase « montant mensuel » non échappée dans le HTML | |
| D6-14 | D6 | Le menu « / » reste affiché à la fermeture du formulaire | |
| D6-15 | D6 | Sans RIB réglé, la facture part avec un relevé bancaire vide | |
| D6-16 | D6 | Parts par liste qui ignorent la provision saisie dans le formulaire | |
| D7a-10 | D7a | Accueil : « Lot vacant » et nombre de lots en mode liste ignorent `EnPlace` | |
| D7a-11 | D7a | Bouton Pappers : SIRET brut, mauvaise adresse avec des espaces | |
| D7a-12 | D7a | Après une cession, le bloc Pappers du cédant reste dans le commentaire du cessionnaire | |
| D7a-13 | D7a | Bail commencé un 29 février : échéances triennales un jour trop tôt | |
| D7a-14 | D7a | Nombre illisible enregistré 0 sans message (lot, surfaces, taux) | |
| D7a-15 | D7a | SIRET et IBAN du locataire enregistrés sans contrôle | |
| D7a-16 | D7a | Création abandonnée (« + Ajouter » sans enregistrer) restée en saisie | |
| D7b-03 | D7b | Correction 16c D7b-08 défaite : la première année reste sur-indexée | |
| D7b-04 | D7b | Correction 16c D7b-09 incomplète : première ligne d'historique sans début | |
| D7b-05 | D7b | « Adresse imprécise » avalée, repli Nominatim sans seuil ni pays | |
| D7b-06 | D7b | Révision : refus et erreurs dans le cadre de résultat, pas au centre | |
| D7b-07 | D7b | INSEE injoignable : rentabilité calculée sur le loyer actuel sans le dire | |
| D7b-08 | D7b | Achats et travaux : nombre illisible à 0, message pendant la frappe | |
| D7b-09 | D7b | Objectifs sur le cloud : refus arrivé après la saisie | |
| D7b-10 | D7b | `float.ToString()` à 7 chiffres : base réécrite arrondie, « 1.234568E+07 » | |
| D7b-11 | D7b | IRL : révision en retard appliquée depuis l'anniversaire (à confirmer) | |
| D7b-12 | D7b | PLU : panne réseau affichée « Commune introuvable » | |
| D7b-13 | D7b | Code mort restant (`rendNet`) | |
| D8-05 | D8 | L'en-tête de l'accueil compte encore les locataires partis | |
| D8-06 | D8 | Révision annoncée pour un locataire qui sera parti avant la date | |
| D8-07 | D8 | Calcul rapide : « Rentabilité nette » sans aucune charge (le format anglais est dans B-02) | |
| D8-08 | D8 | Année sur deux chiffres au-delà de 29 lue au XXe siècle | |
| D8-09 | D8 | « Annuler » la suppression d'un locataire sans effet après une relecture | |
| D8-10 | D8 | Onglets : deux règles de troncature (nom coupé à 20 caractères après un renommage, aussi D10-15), un booléen passé par un objet | |
| D8-11 | D8 | Mise à jour : installeur lancé sans contrôle, jamais effacé | |
| D8-12 | D8 | ConfirmDialog ne répond ni à Échap ni à Entrée | |
| D8-13 | D8 | Code mort et doublons ; décision de fermeture écrite deux fois | |
| D9-10 | D9 | Copie locale entière réencodée et réécrite après chaque enregistrement | |
| D9-11 | D9 | Liste des documents retéléchargée et état réécrit toutes les 20 s, même sans changement | |
| D9-12 | D9 | Verrous et présence : relevé toutes les 10 s qui fouille toutes les fiches | |
| D9-13 | D9 | Suivis de facturation cherchés dans toute la mémoire à chaque enregistrement | |
| D9-14 | D9 | Accueil : alertes calculées deux fois, recherche qui reconstruit tout à chaque touche | |
| D9-15 | D9 | Anciennes données locales relues en entier à chaque démarrage | |
| D9-16 | D9 | Icônes cherchées par nom parmi tous les sprites à chaque fiche | |
| D9-17 | D9 | Petits travaux répétés à chaque image | |
| D10-07 | D10 | Cases à cocher : seul le carré de 24 px réagit au clic, pas le libellé | |
| D10-08 | D10 | Boutons : survol presque invisible ; bouton crème désactivé qui ressemble à un bouton actif | |
| D10-09 | D10 | Palette du thème recopiée en dur : sept ambres et cinq rouges pour deux rôles | |
| D10-10 | D10 | Contrastes faibles (terracotta, texte d'exemple, bordure des champs, pastilles grises, blanc sur ambre) | |
| D10-11 | D10 | Titres de section construits par code : 22 pt, hors échelle, dans une bande de 42 sans « … » | |
| D10-12 | D10 | Deux refus affichés en confirmation à deux boutons au lieu de `ConfirmDialog.Erreur` | |
| D10-13 | D10 | Une même chose, plusieurs noms (« Email » / « E-mail », « Réglage » / « Réglages », « Sauvegarder » / « Enregistrer »…) | |
| D10-14 | D10 | Facture de loyer : le nom du locataire disparaît du titre à chaque ouverture | |
| D10-16 | D10 | Typographie : deux-points collés, accord oublié, point décimal, points de suspension | |
| D10-17 | D10 | Créances : largeur minimale annoncée (712) sans la rangée de filtres (environ 730) | |
| D10-18 | D10 | « Oui / Non » de l'état des lieux dessiné en cases à cocher | |
| B-07 | Interface | Textes d'aide (placeholders) coupés dans les champs d'une ligne des quatre formulaires | |
| B-08 | Interface | Fiche du bâtiment : adresse dans une zone de 6 px, qui touche le nom | |
| B-09 | Interface | Barres de défilement posées sur le contenu à 125 % et plus | |
| B-11 | Interface | La pastille « SCI (JBC) · cloud » flotte par-dessus le contenu (à décider) | |
| B-12 | Interface | Réglages : exemples du calendrier écrits pour 15 jours, faux avec une autre valeur | |
| B-13 | Interface | Gestion du loyer : bas de fenêtre hors écran à 150 % et plus, « Modifier » et « Fermer » au bout du défilement | |
| B-14 | Interface | Gestion du loyer : un carré gris vide sous « Provisions » | |
| B-16 | Interface | Calcul rapide : « Loyer annuel (€) » dépasse de 11 px | |
