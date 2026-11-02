# Revue complète du code — étape 16c (31/10/2026)

Décisions du 31/10 : 8 domaines relus chacun à part par un relecteur Fable, en lecture seule (le domaine 7 en deux) ; chaque défaut a une gravité, un endroit, un scénario, une correction et un test ; **tout est corrigé avant le passage en réel (17)**.

**136 défauts signalés, 11 trouvés par deux relecteurs (fusionnés) : 125 défauts — 7 critiques, 56 majeurs, 62 mineurs.**

Gravités : **critique** = perte ou fuite de données, faille, facture fausse ou illégale, argent faux ; **majeur** = bug qu'un client verra, ou faiblesse qui arrivera en usage normal ; **mineur** = lisibilité, code mort, petite incohérence.

Vérification : tous les critiques et les principaux majeurs ont été relus dans le code par la session principale (aucune fausse alerte) ; les autres sont revérifiés au moment de leur correction. Le détail de chaque défaut (endroit, scénario, correction, test) est dans le fichier de son domaine : [D1](D1.md) Serveur et sécurité, [D2](D2.md) Cloud et synchronisation, [D3](D3.md) Abonnement et comptes, [D4](D4.md) Facturation : émission et envoi, [D5](D5.md) Facturation : plateforme agréée, [D6](D6.md) Facturation : écrans et PDF, [D7a](D7a.md) Locataires, bâtiments, Pappers, [D7b](D7b.md) Révision des loyers, objectifs, travaux, achats, PLU, cartes, [D8](D8.md) Écrans généraux et sauvegarde.

## Ordre des corrections

1. **Critiques** (7), chacun avec son test.
2. **Majeurs**, par thème : serveur, sécurité et argent (droits, Stripe, secrets, installation chez un client) ; facturation ; données, cloud et écrans.
3. **Mineurs**.

Les corrections côté serveur (SQL, fonctions) demandent un script SQL à passer dans Supabase et un redéploiement, faits par l'utilisatrice.

## Critiques (7)

| N° | Domaine | Défaut | Aussi trouvé par | Corrigé |
|---|---|---|---|---|
| D1-01 | D1 | `documenter` n'encadre pas le chemin ; les PC des collègues l'écrivent tel quel |  | 31/10 (code et test ; parcours BC5) ; SQL refait le 01/11 (`chr(0)` refusé par Postgres) |
| D1-02 | D1 | La clé Pappers et la clé Google Maps sont dans l'adresse appelée ; un échec réseau la renvoie au client |  | 31/10 (code et test ; parcours BC) |
| D2-01 | D2 | « Transférer » (fiche bâtiment et locataire) supprime la fiche du cloud et l'écrit dans un dossier local | D8-01 | 31/10 (code et test ; parcours BC) |
| D4-01 | D4 | Deux factures différentes peuvent porter le même nom de fichier PDF : la première est écrasée et le suivi les fusionne |  | 31/10 (code et test ; parcours BC) |
| D5-01 | D5 | Correction hors plateforme : l'ancien JSON reste sur la ligne, un mauvais montant part plus tard |  | 31/10 (code et test ; parcours BC) |
| D5-02 | D5 | `Traiter` sans garde : la même facture peut partir deux fois, ou sa transmission se perdre |  | 31/10 (code et test ; parcours BC) |
| D6-01 | D6 | Répartition « Surface » : un lot à surface déduite compte pour 1 m² |  | 31/10 (code et test ; parcours BC) |

## Majeurs (56)

| N° | Domaine | Défaut | Aussi trouvé par | Corrigé |
|---|---|---|---|---|
| D1-03 | D1 | Le serveur laisse un « Utilisateur » modifier les RIB, le logo et les listes réservés à l'admin | D3-02 | 01/11 (code et test ; parcours BD5) |
| D1-04 | D1 | `creer_entreprise_import` reste ouverte à tout compte et contourne les règles d'`inscrire` et d'`ajouter_entreprise` |  | 01/11 (code et test) |
| D1-05 | D1 | `choisir` peut créer un second abonnement Stripe pour le même groupe | D3-04 | 01/11 (code et test) |
| D1-06 | D1 | `etat` échoue quand Super PDP ne répond pas : la SCI est vue « non reliée » et ses loyers ne sont jamais préparés pour la plateforme | D5-04 | 01/11 (code et test ; parcours BD4) |
| D1-07 | D1 | Renouvellement du jeton Super PDP non protégé contre deux appels simultanés | D5-09 | 01/11 (code et test) |
| D1-08 | D1 | `ajuster` écrit les licences en base avant Stripe et ne valide pas `licences` | D3-05 | 01/11 (code et test ; parcours BD6) |
| D1-09 | D1 | Clé Pennylane et mot de passe SMTP en clair sur le disque, alors que le jeton de session est chiffré | D6-12 | 01/11 (code et test ; parcours BD2) |
| D2-02 | D2 | Une cession de bail à date future n'est jamais appliquée sur le cloud | D7a-01 | 01/11 (code et test ; parcours BF1) |
| D2-03 | D2 | Le dossier d'un bâtiment part en corbeille avant la réponse du serveur : un refus fait disparaître ses documents chez tout le monde |  | 01/11 (code et test ; parcours BF2) |
| D2-04 | D2 | Un enregistrement refusé lancé par une tâche de fond (Pappers, factures en attente) ouvre un message et recharge tout : les saisies en cours de l'utilisateur sont perdues |  | 01/11 (code et test) |
| D2-05 | D2 | Un second message d'erreur remplace le premier : `_refusOuverts` et `_abandonEnCours` restent bloqués pour la session |  | 01/11 (code et test ; parcours BF3) |
| D2-06 | D2 | `Recharger` pendant un envoi de facture : le statut « Envoyé » n'est pas enregistré, la facture repart au prochain lancement |  | 01/11 (code et test) |
| D2-07 | D2 | Un enregistrement encore en file après un rechargement repart avec les versions neuves : il peut écraser une modification d'un collègue |  | 01/11 (code et test) |
| D3-01 | D3 | Une installation neuve n'a ni adresse ni clé du cloud : CIPL ne démarre pas chez un client |  | 01/11 (code et test ; parcours BD1) |
| D3-03 | D3 | « Changer d'entreprise » et « Se déconnecter » ferment une fiche en cours de modification sans prévenir |  | 01/11 (code et test ; parcours BD3) |
| D3-06 | D3 | `inscrire` accepte n'importe quel compte connecté : l'adresse e-mail n'est prouvée que côté client |  | 01/11 (code et test ; parcours BD7) |
| D3-07 | D3 | Stripe « unpaid » (relances épuisées) laisse le groupe modifiable sans payer |  | 01/11 (code et test) |
| D4-02 | D4 | Fermer le panneau pendant l'envoi tue la coroutine : l'e-mail part, rien n'est enregistré, et le panneau reste « envoi en cours » jusqu'au redémarrage |  | 01/11 (code et test ; parcours BC9) |
| D4-03 | D4 | Corriger une facture partie en repoussant son échéance promet un envoi différé qui n'aura jamais lieu, et l'échéance du suivi n'est pas mise à jour |  | 01/11 (code et test ; parcours BE2) |
| D4-04 | D4 | La séquence ne suit pas un numéro saisi à la main : désordre, puis doublon ; aucun contrôle d'unicité par locataire |  | 01/11 (code et test ; parcours BE3) |
| D4-05 | D4 | Une facture émise disparaît du suivi quand la planification change (dépôt révisé dans la fiche, périodicité modifiée) ; la clé du dépôt dépend d'une date qui bouge |  | 01/11 (code et test) |
| D4-06 | D4 | Mode Pennylane : l'envoi groupé au lancement propose d'expédier par e-mail des factures déjà déposées sur Pennylane |  | 01/11 (code et test) |
| D5-03 | D5 | La pastille du suivi n'apparaît jamais : `CopieDe` oublie les champs plateforme |  | 01/11 (code et test ; parcours BE4) |
| D5-05 | D5 | Après la bascule, une facture refusée par Super PDP reste « Envoyé » alors que le locataire n'a rien |  | 01/11 (code et test) |
| D5-06 | D5 | PDF absent sur ce PC → « refusee » définitif, partagé par le cloud |  | 01/11 (code et test) |
| D5-07 | D5 | « Encaissée » sans date ni montant : la TVA à l'encaissement peut tomber dans la mauvaise période |  | 01/11 (code et test ; parcours BE5) |
| D5-08 | D5 | Acheteur : le SIREN vient de la fiche, le nom et l'adresse du formulaire |  | 01/11 (code et test) |
| D5-10 | D5 | Carte Réglages sans issue quand le jeton est retiré ou refusé |  | 01/11 (code et test) |
| D5-11 | D5 | Changement d'entreprise pendant le rattrapage : les factures de la SCI A partent avec le jeton de la SCI B |  | 01/11 (code et test) |
| D5-12 | D5 | « Encaissée » déclaré sur un simple clic, sans confirmation, et irréversible |  | 01/11 (code et test ; parcours BE5) |
| D5-13 | D5 | Adresse électronique de l'acheteur : premier résultat de l'annuaire, pas le SIRET connu |  | 01/11 (code et test) |
| D6-02 | D6 | Date de facture et échéance reprises de la facture précédente du même type | D5-15 | 01/11 (code et test ; parcours BE1) |
| D6-03 | D6 | `{numero}` résolu avec le numéro proposé, pas celui de la facture (correction, réécriture) |  | 01/11 (code et test ; parcours BE3) |
| D6-04 | D6 | Loyer : la provision proposée est celle de la dernière facture, pas celle de la fiche |  | 01/11 (code et test ; parcours BE6) |
| D6-05 | D6 | Révision du dépôt : la base H.T./T.T.C. imprimée vient de la fiche, le montant de la case du panneau |  | 01/11 (code et test) |
| D6-06 | D6 | Variables de montant formatées en culture courante (« 48,894.58 € ») dans les textes et e-mails |  | 01/11 (code et test ; parcours BE7) |
| D6-07 | D6 | RIB ou entête supprimé dans Réglages : les factures retombent en silence sur le premier |  | 01/11 (code et test ; parcours BE8) |
| D6-08 | D6 | Révision du dépôt : si « Réviser » de la fiche est cliqué avant la facture, le complément vaut 0 |  | 01/11 (code et test ; parcours BE10) |
| D7a-02 | D7a | Les locataires partis ou archivés comptent encore dans le loyer total, la surface disponible, le nombre de lots et l'état du bâtiment | D7b-06 | 01/11 (code et test ; parcours BF4) |
| D7a-03 | D7a | Enregistrer la fiche bâtiment détruit sans prévenir une fiche locataire en cours de saisie |  | 01/11 (code et test ; parcours BF5) |
| D7a-04 | D7a | Supprimer une photo déplace le fichier avant l'accord du serveur : hors ligne ou fiche prise, la photo disparaît partout |  | 01/11 (code et test ; parcours BF6) |
| D7a-05 | D7a | Après une cession, le RIB, le téléphone, le code comptable et le choix TVA du cédant restent sur le cessionnaire |  | 01/11 (code et test) |
| D7a-06 | D7a | Hors création, les dates du bail sont enregistrées sans lecture ni contrôle |  | 01/11 (code et test ; parcours BF7) |
| D7a-07 | D7a | La liste des locataires affiche « 0 m² » pour tout lot dont la surface suit le bâtiment |  | 01/11 (code et test ; parcours BF8) |
| D7a-08 | D7a | Un locataire « Nouveau » jamais enregistré est persisté par n'importe quel autre enregistrement du bâtiment |  | 01/11 (code et test ; parcours BF9) |
| D7a-09 | D7a | Supprimer un locataire laisse son dossier en place : renommer ou céder vers ce nom est ensuite refusé, sans issue dans l'application |  | 01/11 (code et test ; parcours BF10) |
| D7b-01 | D7b | Un second clic sur « Réviser » repousse la prochaine révision de deux ans |  | 01/11 (code et test ; parcours BF11) |
| D7b-02 | D7b | Révision par indice : le nouveau loyer s'applique aussi aux périodes d'avant la date de révision |  | 01/11 (code et test) |
| D7b-03 | D7b | Valeurs par défaut de l'indice : « Révision à faire » dès l'initialisation, puis révision proposée avec un trimestre antérieur à la référence |  | 01/11 (code et test ; parcours BF12) |
| D7b-04 | D7b | Reprise de facturation enregistrée sur les mois standard, relue avec les mois cochés |  | 01/11 (code et test) |
| D7b-05 | D7b | Résumé Rentabilité : les mensualités de tous les emprunts sont comptées à vie, même une fois le prêt remboursé |  | 01/11 (code et test ; parcours BF13) |
| D7b-07 | D7b | Nouvel achat / nouveaux travaux : la date n'est ni remise à zéro ni contrôlée |  | 01/11 (code et test ; parcours BF14) |
| D8-02 | D8 | `GetSaveRoot` se replie sur la racine de « Mon entreprise » quand le dossier de l'entreprise cloud manque |  | 01/11 (code et test ; parcours BF15) |
| D8-03 | D8 | Fermer l'application pendant un enregistrement cloud en vol le perd sans prévenir |  | 01/11 (code et test ; parcours BF16 (build)) |
| D8-04 | D8 | Croix « Quitter » avec une fiche en édition : la confirmation est demandée deux fois (build) |  | 01/11 (code et test ; parcours BF16 (build)) |
| D8-05 | D8 | Les écrans de saisie (facture, achat, charge, révision, départ…) ne sont pas couverts par la garde de fermeture |  | 01/11 (code et test ; parcours BF17) |

## Mineurs (62)

| N° | Domaine | Défaut | Aussi trouvé par | Corrigé |
|---|---|---|---|---|
| D1-10 | D1 | Les révocations oublient `authenticated` : plusieurs fonctions « internes » restent appelables par tout compte |  | 02/11 (SQL et test ; parcours BH7) |
| D1-11 | D1 | Webhook : `repartir` rejoue la répartition initiale ; `appliquer` prend un événement d'un ancien abonnement pour le courant |  | 02/11 (code et test) |
| D1-12 | D1 | Une fonction serveur lente (> 15 s) passe CIPL « hors ligne » pendant 30 s |  | 02/11 (code et test ; pas de cas manuel) |
| D1-13 | D1 | `reserver_envoi` refusé rend un verrou que la même session tenait déjà pour autre chose |  | 02/11 (SQL et test) |
| D1-14 | D1 | Page de retour Super PDP : `state` consommé en deux temps, message d'erreur brut |  | 02/11 (code et test) |
| D1-15 | D1 | `pappers` : aucune limite par entreprise sur les jetons payés par CIPL |  | 02/11 (SQL, code et test ; 50 par jour) |
| D1-16 | D1 | Restes et incohérences |  | 02/11 (SQL, code et test) |
| D2-08 | D2 | Supprimer une liste de charges : vérification des verrous puis écriture de tous les bâtiments, sans verrou pris (écriture partielle possible) |  | 02/11 (code ; parcours BH1) |
| D2-09 | D2 | Changement d'entreprise pendant une file d'envoi : `Envoyer` et `EnvoyerApres` lisent `SessionCloud.Entreprise` après l'attente |  | 02/11 (code) |
| D2-10 | D2 | L'écho d'une suppression peut arriver avant la réponse du serveur : la fiche est relue et annoncée « mise à jour par un collègue » |  | 02/11 (code ; parcours BH2) |
| D2-11 | D2 | « Modifier » pendant qu'une relecture remplace la fiche : l'écran s'ouvre sur une fiche détruite |  | 02/11 (code) |
| D2-12 | D2 | Un fichier illisible pendant le balayage est vu « supprimé ici » et marqué supprimé au serveur |  | 02/11 (code) |
| D2-13 | D2 | « Retiré de l'entreprise » efface les copies hors ligne de toutes les entreprises |  | 02/11 (code ; parcours BH6) |
| D2-14 | D2 | Code mort et commentaires périmés de l'ancien mode local |  | 02/11 (code) |
| D2-15 | D2 | Message de refus « version » quand la fiche n'existe plus |  | 02/11 (code, SQL et test ; parcours BH8) |
| D3-08 | D3 | Deux admins qui invitent en même temps peuvent dépasser les licences |  | 02/11 (SQL et test) |
| D3-09 | D3 | Un compte sans entreprise ne peut pas saisir un code d'invitation, et la fenêtre trompe un compte existant |  | 02/11 (code ; parcours BH4 et BH5) |
| D3-10 | D3 | Après un échec d'ouverture, l'écran garde l'entreprise précédente sous l'identité de la nouvelle |  | 02/11 (code ; parcours BH3) |
| D3-11 | D3 | Le client Stripe garde l'adresse de l'admin qui a souscrit, même s'il est retiré du groupe |  | 02/11 (code et test ; parcours BH9 : portail, ajustement et souscription avec un client existant) |
| D3-12 | D3 | `Constater` ne fait rien si l'abonnement n'a pas pu être lu à l'ouverture |  | 02/11 (code et test) |
| D3-13 | D3 | Hygiène de la session : jeton jamais invalidé côté serveur, copie locale rendue sans vérifier le propriétaire du jeton |  | 02/11 (code et test) |
| D3-14 | D3 | Trois copies de `Q()` qui n'échappent ni tabulation ni retour à la ligne |  | 02/11 (code et test) |
| D3-15 | D3 | Code mort autour des licences et de la lecture seule |  | 02/11 (code) |
| D4-07 | D4 | `DoitPartir` applique le délai de 15 jours à tous les types, contrairement à `DateEnvoi` ; `PeutPartir` n'est utilisé que par les tests ; le diagnostic dit « échéance illisible » |  | |
| D4-08 | D4 | Régul regroupée : les parts par liste sont calculées sur `NbPeriodes` au lieu des périodes facturées, et ignorent la provision saisie | D6-11 | |
| D4-09 | D4 | Message obsolète « la case « Envoyer par email » est décochée (Options & envoi) » ; mode d'envoi par défaut = Pennylane pour une entreprise locale |  | |
| D4-10 | D4 | Le rappel d'impayé est adressé à l'e-mail de la fiche, pas au destinataire mémorisé sur la facture |  | |
| D4-11 | D4 | `FactureEnvoiAuto.Contexte` déduit HT = TTC ÷ 1,2 pour tous les types : `{loyer.ht}` / `{loyer.tva}` faux dans les e-mails d'un dépôt ou d'un décompte |  | |
| D4-12 | D4 | Correction élargie d'une facture regroupée : la ligne ajoutée reçoit un statut neuf alors que les autres gardent le leur |  | 01/11 (code et test ; parcours BE9) |
| D5-14 | D5 | Toast « Plateforme injoignable » pour tout échec, même « autorisation retirée » |  | |
| D5-16 | D5 | Montants à trois décimales ou négatifs : écart PDF/XML ou refus BR-27 |  | |
| D5-17 | D5 | Phrase de retard vide : le JSON dit la phrase légale, le PDF ne dit rien |  | |
| D5-18 | D5 | `plateformes.societe` jamais rempli |  | |
| D5-19 | D5 | N° de TVA de l'acheteur calculé, non vérifié |  | |
| D6-09 | D6 | Textes libres insérés dans le HTML sans échappement |  | |
| D6-10 | D6 | Code mort et paramètre inutilisé |  | |
| D7a-10 | D7a | Un refus du nom (doublon, dossier bloqué) repasse le champ nom en lecture seule et perd la saisie |  | |
| D7a-11 | D7a | BODACC : 100 annonces pour 40 sociétés, une société en procédure peut passer pour « SAINE » |  | |
| D7a-12 | D7a | SIRET avec espaces : bouton Pappers, tournée cloud et annuaire local ne le lisent pas pareil |  | |
| D7a-13 | D7a | Dates ISO relues en culture courante, contre la règle §4.2, et `EstParti` lit l'horloge |  | |
| D7a-14 | D7a | Départ : choisir « Non, pas d'état des lieux » garde le PDF joint, l'affichage se contredit |  | |
| D7a-15 | D7a | Archiver et Réactiver enregistrent deux fois |  | |
| D7a-16 | D7a | Un enregistrement voisin repasse en lecture seule le champ surface d'une fiche en saisie |  | |
| D7a-17 | D7a | Code mort et références d'éditeur ou de tests dans du code de jeu |  | |
| D7b-08 | D7b | Référence d'indexation enregistrée avec l'indice de repli → première année sur-indexée dans la rentabilité |  | |
| D7b-09 | D7b | Rentabilité en mode indice : loyer théorique, pas le loyer facturé |  | |
| D7b-10 | D7b | Saisie de date : l'année sur deux chiffres passe, et la date de révision est enregistrée aussitôt |  | |
| D7b-11 | D7b | Récurrence mensuelle d'un objectif : le jour glisse (31 → 28 → 28…) |  | |
| D7b-12 | D7b | Achats et travaux : montants parsés en `float` avant d'être stockés en `double` |  | |
| D7b-13 | D7b | INSEE : pas de délai d'attente, XML non protégé → « Réviser » bloqué sur « Récupération… » |  | |
| D7b-14 | D7b | Géocodage : réponse non protégée et meilleur résultat pris sans seuil |  | |
| D7b-15 | D7b | Carte : les textures Google ne sont jamais détruites |  | |
| D7b-16 | D7b | PLU : le panneau abandonne à 10 s, la requête dure jusqu'à 15 s |  | |
| D7b-17 | D7b | Formulaires achat/travaux : écouteurs empilés et durée d'emprunt héritée du formulaire précédent |  | |
| D7b-18 | D7b | Code mort |  | |
| D7b-19 | D7b | Carte Loyer : le TTC est toujours HT × 1,20 |  | |
| D7b-20 | D7b | L'ICC n'est pas proposé |  | |
| D8-06 | D8 | Code mort : le démarrage local et tout ce qu'il portait |  | |
| D8-07 | D8 | `MigrateData` copie et ne déplace pas : commentaire faux et copie complète laissée derrière (dont la clé API) |  | |
| D8-08 | D8 | Transfert : un dossier de destination est supprimé sans sauvegarde quand le bâtiment n'y existe pas |  | |
| D8-09 | D8 | Accueil : un rappel de facturation ou de révision ouvre la fiche, pas l'écran, alors que la même carte dans la fiche ouvre l'écran |  | |
| D8-10 | D8 | L'en-tête de l'accueil compte les locataires archivés |  | |

## Trouvés pendant le parcours BC (01/11)

| Défaut | Gravité | Corrigé |
|---|---|---|
| SQL du lot 1 : `chr(0)` dans `chemin_document_valide`, que Postgres refuse : plus aucun document enregistré (introduit le 31/10) | critique | 01/11 (SQL et test ; parcours BC5) |
| Enregistrer la fiche d'un locataire remet à 0 la surface saisie à la main, depuis le 22/09 (`TailleLotDisponible` rend la surface saisie, prise pour une proposition) | majeur | 01/11 (code et test ; parcours BC10) |
| Titre des PDF de facture : « cipl_facture_<code>.html » (page temporaire d'Edge), vu aussi par le locataire | mineur | 01/11 (code et test ; parcours BC7) |
| Le formulaire d'une charge ne montre pas ce que paie chaque locataire (demande) | amélioration | 01/11 (code et test ; parcours BC8) |
| Résumé Pappers de DANONE : finances 2024 et 2025 identiques (CA 1 030 000 000 €, résultat 592 000 000 €) : Pappers, ou lecture qui confond deux exercices | à vérifier | avec les majeurs « données » |

D4-02 (majeur) a été corrigé avant son lot : c'est lui qui a cassé BC4 (formulaire fermé pendant l'e-mail).

## Lot 2 : majeurs « serveur, sécurité et argent » (01/11)

Les 11 défauts D1-03 à D1-09, D3-01, D3-03, D3-06 et D3-07 sont codés et testés (`Revue16cTests`, tests texte sur le SQL et les fonctions, 590 tests verts). Décisions du 01/11 : **D3-01** l'adresse et la clé publique du projet sont compilées (`CloudDefaut`, générée depuis le fichier du PC sans passer par la conversation) et le fichier `cipl_app_secrets.dat` reste une surcharge ; **D1-09** secrets du cloud chiffrés par DPAPI sur Windows et par le trousseau sur Mac (hors cloud : inchangé). Serveur : `Serveur/etape16c2_supabase.sql` à exécuter ; `abonnement-stripe`, `stripe-webhook` et `facture-electronique` à redéployer. Parcours BD fait le 01/11 : BD1 à BD5 et BD7 bons ; BD6 à rejouer sur un groupe abonné (celui de SCI (JBC) est en essai, sans abonnement Stripe). Vérifié en réel (D3-06) : la session ouverte par le code porte `amr` = `otp` (`magiclink` et `email/signup` restent acceptés).

## Lot 3 : majeurs « facturation » (01/11)

Les 23 défauts D4-03 à D4-06, D4-12, D5-03, D5-05 à D5-08, D5-10 à D5-13, D6-02 à D6-08, D7b-02 et D7b-04 sont codés et testés (`Revue16cTests`, 602 tests verts). Décisions du 01/11 : **D5-07 / D5-12** « Encaissée » déclare le total TTC à la date du dernier versement saisi (sinon le jour du clic) ; une facture qui quitte « Payé » envoie un second 212 du même montant en négatif, daté du jour ; **D5-06** un PDF absent de ce PC est lu dans le cloud avant la transmission ; **D6-07** un RIB ou un entête utilisé par un locataire ne se supprime pas (message nommant les locataires) ; **D5-08** l'acheteur de la facture électronique est le SIRET du formulaire, comme le PDF. Décisions du 31/10 appliquées : D5-05 (retour en attente d'envoi après un refus post-bascule), D6-08 (ancien dépôt mémorisé par « Réviser »), D4-12 (refus d'élargir une facture payée). Nouveaux champs : `FactureEtat.encaisseeMontant/encaisseeDateISO`, `Locataire.depotPrecedent/depotPrecedentRevisionISO`. Serveur : `facture-electronique` à redéployer (212 avec montant et date, annuaire par SIRET). Parcours BE. À valider au bac à sable : la forme exacte de `details.reported_data` (type MEN) chez Super PDP.

### Demande faite pendant le parcours BE (01/11) : délais de facturation réglables

Le calendrier figé du code (loyer envoyé 15 j avant l'échéance, « Impayé » 15 j après, autres factures à +30 j) devient un réglage : Réglages › Facturation › Calendrier des factures pour l'entreprise (`ReglageData.envoiAvantJours / impayeApresJours / echeanceAutresJours`, admin), et sur la fiche du locataire (Gestion du loyer › Initialiser, « Délais des factures : ceux de l'entreprise » décochée → `Locataire.delaisPropres` et ses trois valeurs). Une seule règle, `FacturationSuivi.EnvoiAvant / ImpayeApres / EcheanceAutres(loc)`, suivie par `EtatDe`, `Lead`, `DateEnvoi` et `PeutPartir` (paramètre `loc` optionnel : sans lui, l'entreprise), les alertes, l'envoi automatique du lancement et les trois formulaires. Le jour d'échéance du loyer reste `jourDemandeLoyer`. Parcours BE11 ; tests `Delais_*`.

### Trouvés pendant le parcours BE (01/11)

- **BE5, Super PDP « Encaissée »** : la forme du détail est validée au bac à sable après trois refus lus par l'API : `details` est une **liste** de détails, et chaque bloc MEN de `reported_data` porte le montant TTC encaissé **pour un taux de TVA** et ce taux (`value_percent`, règle BR-FR-CDV-14). Le serveur relit la ventilation de la facture (`GET /v1.beta/invoices/{id}?format=en16931`, dans `en_invoice.vat_break_down`) et répartit le montant reçu au prorata (paiement partiel, annulation en négatif). Action de lecture `evenements` (diagnostic). `facture-electronique` redéployée le 01/11.
- **Refus affiché comme une panne** : un refus de Super PDP sur « Encaissée » donnait « Plateforme injoignable » sans motif nulle part. `FactureElectronique.Panne` journalise le motif et montre un refus avec son détail (`ConfirmDialog.Erreur`) ; `Traiter` rend la réponse en échec (`Reponse`) au lieu d'un texte.
- **Annulation d'encaissement invisible** : son message était recouvert par le résumé suivant. Les annulations sont comptées dans le résumé (« 1 encaissement annulé chez Super PDP »). Rappel : chez Super PDP, une annulation est un 212 à montant négatif, l'étiquette reste « Encaissée ».
- **Bouton du formulaire Loyer en correction** : il disait « Enregistrer pour l'envoi du 30/11 » alors que la correction partait tout de suite (D4-03). `FactureLoyerPanel.PeriodePartie()` : facture de la période déjà partie → « Sauvegarder et envoyer ».

## Lot 4 : majeurs « données, cloud et écrans » (01/11)

Les 22 défauts D2-02 à D2-07, D7a-02 à D7a-09, D7b-01, D7b-03, D7b-05, D7b-07 et D8-02 à D8-05 sont codés et testés (`Revue16cTests`, 615 tests verts). Décisions du 01/11 : **D7a-03** « Modifier » et « Sauvegarder » sur le bâtiment sont refusés tant qu'une fiche locataire est en saisie (message nommant le locataire) ; **D7b-01** après une révision réussie le panneau se ferme avec un message, comme « Initialiser ». Cloud : écritures de fond silencieuses (`silencieux`, D2-04), suppression d'un bâtiment entièrement différée à l'accord du serveur — rien ne quitte l'écran avant, refus hors ligne immédiat, même pour un locataire (D2-03 + retour du 01/11) ; dossier d'un locataire en corbeille après l'accord (D7a-09), messages d'erreur en file (D2-05), rechargement qui attend la fin d'un envoi et de la file (D2-06, D2-07, `Perime` revérifié au départ), cession datée appliquée à l'ouverture (D2-02, `AppliquerCessionsDues`), galerie sous réservation (D7a-04), pas de repli de racine (D8-02), fermeture qui attend l'écriture en vol (D8-03), une seule confirmation (D8-04), écrans tenus vus par la garde (D8-05). Données : `EnPlace` (D7a-02), dates du bail toujours contrôlées (D7a-06), surface effective dans la liste (D7a-07), « Nouveau » inscrit au premier enregistrement (D7a-08), cession sans RIB/téléphone/code/TVA du cédant (D7a-05), révision par défaut à bail + 1 an et trimestre voulu ≥ référence (D7b-03), prêt remboursé à 0 (D7b-05), date d'achat/travaux proposée et contrôlée (D7b-07).

### Demande faite pendant le lot 4 (01/11) : révision du dépôt corrigeable

Tant que la facture de révision du dépôt n'est pas partie, « Réviser » (carte Dépôt) reprend la révision en attente (même date, même ancien dépôt, mention « révision en attente de sa facture ») au lieu d'attendre un an : `LocataireFacturationFields.DateRevisionProposee`. Parcours BF18.

### Trouvés pendant le parcours BF (01/11)

- **Suppression** (BF2) : sur le cloud, rien ne quitte l'écran avant l'accord du serveur ; refus hors ligne / lecture seule avant tout, bâtiment et locataire (`DeleteBatiment(id, apresSucces)`, `DeleteLocataire` → `bool`).
- **Surface d'un lot** (BF8) : un champ vidé veut dire « suit de nouveau le bâtiment » (avant, seul « 0 » marchait).
- **Dépassement des surfaces** (BF9) : le message nomme les lots saisis et la surface du bâtiment, sinon on ne sait pas lequel corriger.
- **Dossier de l'entreprise introuvable** (BF15, grave) : la synchronisation des documents lisait un dossier absent (Drive fermé, clé débranchée, dossier renommé) comme « tous les fichiers supprimés ici » et aurait supprimé TOUS les documents de l'entreprise sur le cloud, puis chez les collègues. Arrivé pour de vrai le 01/11 à 18 h 55 sur SCI (JBC) (44 lignes marquées supprimées, fichiers du cloud intacts, copies locales intactes dans le dossier renommé) : la première garde ne vérifiait que l'existence du dossier, et l'émission d'une facture l'avait recréé vide. Gardes dans `DocumentsCloud.Passer` : dossier introuvable, ou aucun fichier local alors que des fichiers étaient connus (`SyncDocuments.DossierDeplace`) → passe suspendue ; fusible `SyncDocuments.SuppressionsSuspectes` : plus de 5 suppressions d'un coup et plus de la moitié des fichiers connus → refusées. Restauration : remettre le dossier, la passe renvoie les fichiers (`documenter` remet `supprime_le` à null).
- **Carte Dépôt** (BF18) : une révision en attente de sa facture se lit sur la carte (« 01/10/2026 en attente de sa facture · prochaine 01/10/2027 ») ; la carte ne montrait que la prochaine date et la fenêtre l'autre, on croyait à un décalage.

## Décisions de l'utilisatrice sur la liste (31/10)

- **D5-12 « Encaissée »** : déclaré tout de suite au clic « Payé » (comme le 29/10), mais **annulé si la facture quitte « Payé »** : CIPL envoie alors un « Encaissée » (fr:212) de montant **négatif** (cas d'usage officiel « encaissement partiel et annulation d'encaissement », Sovos cas 34 ; fiche FNFE « Avoirs, factures négatives »). Avec D5-07, chaque « Encaissée » porte le montant et la date du paiement (`details.reported_data`, type `MEN`). Format exact à valider au bac à sable.
- **D6-08 Révision du dépôt** : mémoriser le dépôt d'avant « Réviser », pour que la facture de révision soit juste dans les deux ordres.
- **D5-05 Refus de Super PDP après la bascule (1/9/2027)** : la ligne revient « En attente d'envoi », la fenêtre dit « corrigez-la depuis le suivi (Corriger) », et l'envoi automatique ne la repropose pas telle quelle.
- **D2-01 / D8-01 « Transférer » sur le cloud** : un **vrai transfert entre entreprises du cloud, maintenant** (fonction serveur). En attendant qu'il existe, le bouton est masqué sur le cloud (correction critique immédiate). Sa conception a été validée le 01/11 (CONCEPTION §16) : admin du groupe seulement, vers les entreprises du groupe où il a accès ; refus tant qu'une facture attend Super PDP (transmission ou « Encaissée ») ; factures transmises suivent le locataire, « Encaissée » déclaré au nom de SCI X (revu le 01/11, BG7 : une facture impayée ne bloque pas, l'émettrice doit déclarer son encaissement) ; homonymes fusionnés ; documents copiés puis marqués supprimés à l'origine. **Code fait le 01/11** (`TransfertCloud`, `transferer_fiches`, `plateformeOrigine`) ; parcours BG1 à BG8 validés le 01/11. **BG7 (01/11)** : la copie affichée du suivi (`FacturationSuivi.CopieDe`) oubliait `plateformeOrigine`, la pastille « transmise par » n'apparaissait jamais : corrigé, test sur tous les champs de `FactureEtat`.
- **D4-12 Facture déjà payée** : on ne peut plus lui ajouter de charge en la refaisant ; un message propose une nouvelle facture.
- D3-06 et D3-07 : corrections serveur sans décision (preuve de l'adresse par le code reçu ; Stripe « unpaid » = expiré) ; à noter pour l'étape 17 : réglage Stripe « annuler l'abonnement » après les relances.

## Trouvés pendant le parcours BH (02/11)

| Défaut | Gravité | Corrigé |
|---|---|---|
| `etape16c4` recréait `enregistrer_batiment` et `enregistrer_locataire` d'après l'étape 4, sans `maj_session` (étape 5) : chaque enregistrement passait pour celui d'un collègue, fiche reconstruite à chaque sauvegarde (vu en BH2 ; introduit et corrigé le même jour, avant tout commit) | majeur | 02/11 (SQL corrigé, test texte `maj_session = p_session` ×2) |
| `BatimentManager.Vider` détruisait les fiches sans redessiner l'accueil : un clic sur une carte restée affichée levait une `MissingReferenceException` (vu en BH3, après un échec de lecture ; même chemin au retrait d'une entreprise) | mineur | 02/11 (`GeneralMenuPanel.Instance?.Refresh()` dans `Vider`, retour à l'accueil sur l'échec de lecture) |
| Hors ligne, l'ouverture d'une entreprise enchaînait trois appels de 15 s (Réglages, abonnement, bâtiments) sans rien à l'écran : 45 s « gelées », le temps de recliquer partout, l'ancienne entreprise restant affichée et modifiable (vu en BH3, second essai) | mineur | 02/11 (écran vidé et voilé dès le clic (`EcransCloud.Attente`, « Ouverture de … »), sonde `Sonder(timeout: 5)` : message en 5 s au plus ; `DemarrageCloud.EchecLecture`) |
| Les copies hors ligne effacées laissaient leur `.bak` (écriture atomique) sur le PC, avec les mêmes données, y compris après « Se déconnecter » (vu en BH6) | mineur | 02/11 (`CopieLocale.Effacer` retire aussi le `.bak`) |
| Un enregistrement refusé (version, fiche supprimée) rechargeait toute l'entreprise au OK, et ne laissait aucune trace dans le journal (vu en BH8) | mineur | 02/11 (`DirectCloud.ARelire` : le seul bâtiment ; trace `enregistrement refusé`) |
