# Revue 17 — thème 2c (facturation) : plan de réalisation

Analyse en lecture seule du 05/11/2026 (5 lecteurs, 5 vérificateurs sceptiques, 1 synthèse), décisions de l'utilisatrice du 05/11. Référence pour coder lot par lot : les lieux `fichier:ligne` datent du 05/11, à relire avant chaque lot.

## État (06/11)

- **Lots 1 à 5 faits le 06/11** (687 tests, parcours BP1 à BP6). En plus : la phrase de règlement d'usine n'est plus mémorisée (`FactureEmission.EstPhraseUsine`), trouvé en vérifiant D6-08 ; refus « n'existe pas » pour une date bien formée mais impossible ; refus de suppression d'une charge qui renvoie à « Refaire ».
- **Retours de BP1 (06/11)**, décisions de l'utilisatrice : (1) badge « Auto » : « la pastille garde l'état calculé et un petit badge « Auto » s'ajoute à côté, seulement sur une facture remise en « Automatique » (numéro et PDF gardés) » ; (2) numéro repris : « la ligne « N° de facture » montre le numéro qui sera imprimé, non modifiable, et l'aperçu aussi ; le numéro suivant ne s'affiche que pour une facture neuve » ; règle : « tant que la facture n'a pas été envoyée par mail ou sur la plateforme on peut réutiliser le numéro » ; (3) « Automatique » sur une facture « En attente d'envoi » : « rien, elle reste en attente » ; (4) facture préparée puis mise « À faire » à la main, puis « Automatique » : « elle repart en attente » (avec son PDF, tel quel ; une ligne déjà en automatique qui garde un numéro ne change pas, car ce peut être un loyer parti puis retiré après un départ). (5) « Automatique » est un mode, pas un état : « on retire le bouton automatique dans la liste et tu mets un bouton à activer/désactiver automatique à côté » ; éteint = bloquée « À faire » ; « À faire » quitte aussi le menu (c'est le bouton éteint) ; le badge « Auto » disparaît. Faits le 06/11 (692 tests, parcours BP1, BP7, BP8). Relecture du 07/11 (deux relecteurs), corrigés : « Retirer du suivi… » pour un loyer préparé ou bloqué après le départ ; pas de bouton « Auto » sur une ligne retirée puis départ annulé ; rallumer ne repropose qu'une facture toujours juste (FactureToujoursJuste) ; facture bloquée visible et protégée (AFacture) ; alerte et étape « Dernier loyer » vers le suivi ; garde de SetStatut ; messages (694 tests). Reste (point (a)) : « Refaire » sur une ligne retirée puis départ annulé reprend son numéro et remplace le PDF reçu. À faire valider : le texte d'aide des trois autres formulaires (« N° de facture — repris : cette facture a déjà ce numéro », le loyer garde « … cette période … ») et le gris du numéro repris (TexteExemple, contraste faible). Laissés : sur un numéro repris, l'ID tapé avant n'est pas enregistré (la séquence revient à la réouverture) ; une sélection refusée en régularisation ou en refacturation affiche un numéro neuf, le refus venant au clic, comme avant.
- **Reprise de la revue par Fable (09/11)** : les lots 6 à 16 restent valables ; la synthèse (REVUE_17/SYNTHESE.md, bloc « Reprise du 09/11 », lot C) y rattache les nouveaux défauts de facturation : lot 6 + D4-N3 (régression D7a-03 : e-mail du cessionnaire) + D4-N10 + D4-N14 ; lot 7 + D5-N2 + D5-N9/D5-V2 (RecopierPlateforme : encaisseeMontant, encaisseeDateISO) ; lot 9 + D4-N2/D5-N1 (refus temporaire en attendant) ; lot 10 + D5-N7 ; lot 11 + D4-N15 ; lot 12 + D5-N4 ; lot 13 + D5-V1 ; lot 16 + D6-07 (moitié régularisation) + D5-N8 ; puis un lot « coutures » sans champ (réservations manquantes du décompte et des versements, total nul, pièces jointes, états des lignes secondaires). D6-N13 reste « laissé ».
- **Décisions du 09/11 (reprise)** : le lot 9 (D5-06) passe juste après le lot 7, sans garde temporaire ; D5-N13 (réglage « Mention TVA par défaut », modèle +1), D6-N9 (dépôt sans mention), D6-N3 / D4-N9 (refus du total nul : loyer avant toute branche, régularisation, refacturation ; jamais le dépôt), D6-N4 (justificatifs joints, envoi différé compris) et D4-05 / D4-06 (majeurs) rejoignent le lot C « coutures » ; D6-N13 reste « laissé ». Le lot A (critiques, dont D6-18 : `FactureDepotPanel.AncienPropose` et `RefusOuverture`) est corrigé le 09/11.
- **Vague 1 du lot C faite le 09/11** (lots 6, 6b, 7, 9 ; 757 tests ; parcours BS1 à BS26) : modèle des données 9 (`DonneesCloud.Modele`). Lot 8 sauté (bac à sable Super PDP et expert-comptable) ; **fait le 17/11**, voir « Lot 8 » plus bas. Lot 9 : tranché le 10/11, le versement reporté par `ApresCorrection` porte la date de l'encaissement d'origine (fait le 10/11) ; essais du bac à sable : (d) et l'ordre le 15/11 ; (a), (b) et le parcours complet (c) **faits le 17/11 dans Démo CIPL, sans défaut**, voir « Avant le lot 9 ».
- **Vague 2 du lot C faite le 10/11** (lots 10 à 13 ; 776 tests ; parcours BT1 à BT43) : lot 10, la correction garde le taux imprimé et la plateforme suit le taux du document ; lot 11, refus avant l'envoi dans les trois formulaires quand Super PDP est indisponible, et e-mail de facture ordinaire pour une SCI déliée (D4-16) ; lot 12, facture sans facture électronique retenue et nommée au lancement après le 1/9/2027, plateforme seule sans adresse (D5-21) ; lot 13, régularisation et refacturation suivent la règle du loyer (D5-10, D5-26). Modèle des données inchangé (9), rien côté serveur. **Décisions du 10/11** : (1) versement reporté à la date de l'encaissement d'origine (lot 9 ; correction au même montant comprise, confirmé le 10/11 ; « Payé » reste un seul clic daté du jour du clic, « Paiement partiel… » pour une autre date de réception) ; (2) « Attendre Super PDP » : après la bascule, une facture qui a sa facture électronique ne part plus en PDF au lancement tant que Super PDP n'est pas actif (`FactureElectronique.RefusLancement`) ; (3) la mention TVA choisie pendant une correction ne vaut que pour elle. Restes rangés aux « coutures » : refus du total nul avant le PDF et avant la branche « premiers pas » (D6-19), PDF « corrigée(X) » écrit avant un refus, ligne « Envoi » d'un loyer préparé à l'avance (elle juge le jour, la garde juge le départ), D8-02 en général, mention « à refaire » dans le libellé du suivi, texte de la carte des Réglages (`FactureElectronique.Statut`). Suite : vague 3 (faite le 10/11, puce suivante), puis « coutures ».
- **Vague 3 du lot C faite le 10/11** (lots 14 à 16, avenant de périodicité ; modèle des données 10 ; 809 tests ; parcours BU1 à BU51) : lot 14, `Locataire.historiquePeriodicites` et `FacturationSuivi.Plan` (le rang dans l'année reste le « n » de la clé, période coupée au prorata des mois) ; lot 15, date d'effet dans Gestion du loyer (« Nouvelle périodicité à compter du », `Loyers.RefusAvenantPeriodicite`) et formulaire du loyer sur le plan (`FactureLoyerPanel.PeriodeOptions`, `PeriodiciteChoisie`) ; lot 16, provisions converties d'office (`Loyers.ProvisionConvertie`), régularisation sur les provisions réellement appelées (`Loyers.ProvisionsAppelees` : D6-07 reste, D6-N5), parts par liste (D6-16), arrondis (D5-13 ← D5-N8). Relecture d'ensemble : rien de bloquant, les points mineurs repris le jour même (finitions). Restes rangés aux « coutures » : `Loyers.PeriodesFacturees` n'est plus lue que par les tests (à retirer avec eux) ; le récap de la fiche « depuis le … » (facultatif dans le plan) ; `{loc.provision}` d'un vieux loyer hors plan sur une fiche qui a aussi un avenant daté (ramenée à la périodicité gardée sur la ligne) ; à voir en Play : la place et le retour à la ligne du bloc « Nouvelle périodicité à compter du » dans la fenêtre de Gestion du loyer, et l'absence de toast de conversion à l'ouverture sur un locataire d'une autre périodicité que le précédent. Suite : lot « coutures », lot 8 (D5-11, après le bac à sable et l'expert-comptable), essais BS19 au bac à sable, puis lots D à G.
- **« Coutures » du lot C faites le 10/11** (trois lots en série sur les formulaires, un lot en parallèle autour, chacun relu par un sceptique et repris, puis une relecture d'ensemble ; modèle des données 11 ; 855 tests ; parcours BV1 à BV25) : D4-05 à D4-12 et D4-14, D5-12, D5-14 à D5-19, D5-22, D5-25, D6-11, D6-12, D6-15, D6-19, D6-20, D6-22 à D6-27, D8-02, les restes des vagues 1 à 3 et les deux points « hors du plan » (une facture de 0 € ne passe plus « Impayé » ; l'aperçu de l'entête suit l'échéance). Détail : FACTURATION §16, bloc « coutures ». Décisions du 11/11 faites : un acompte ne date pas le « Payé » d'un clic (D5-12) ; sans RIB, le guide « Une première facture » commence par « Vos RIB » (D6-15) ; bouton « Corriger et envoyer » dans la régularisation, la refacturation et le dépôt (D4-10). Restent ouverts : D5-18 (décompte de sortie avec TVA : expert-comptable), la date citée par un avoir pour un loyer sans facture électronique (D5-14), et les choix pris sans décision écrite (liste dans FACTURATION §16). Suite : lot 8 (D5-11, après le bac à sable et l'expert-comptable), essais BS19 au bac à sable, puis lots D à G.
- **Tranché le 09/11 (relecture du lot 1), fait au lot 6b** : (a) un loyer retiré après envoi porte `FactureEtat.retireApresEnvoi` (modèle 8) : « Refaire » et toute émission sur cette période sont refusés au centre tant qu'il est posé ; il tombe quand la pastille redonne un état ; (b) l'avoir cite sa facture (`FactureEtat.factureCitee`, posé par `FactureRefacPanel.Finaliser`) et « À rembourser » ne compte que ce qui a été versé sur elle (`FacturationSuivi.ARembourser`, `Verse`) : l'avoir d'un loyer jamais payé reste « Envoyé » et n'empêche plus d'archiver. Pas de migration des lignes d'avant.
- Hors du plan, relevé en route : `FacturePdfService.cs:107` (D6-12) ; l'aperçu de l'entête ne suit pas un changement d'échéance quand il contient `{echeance}` (mineur, lot 5) ; une facture de 0 € passe « Impayé » après son échéance (`FacturationSuivi.EtatDe` : `f.montant >= 0f`), vu en Play le 06/11 sur « Loyer Octobre 2026 » de Locataire BI2 (données des essais BI) : rien n'est dû, elle devrait rester « Envoyé » (mineur, lot 3).

## Décisions de l'utilisatrice (05/11)

- **D4-01 (adresse d'envoi)** : Actuelle, sauf autre titulaire : chaque facture garde le nom de son acheteur et l'adresse d'envoi ; facture du locataire de la fiche → son adresse actuelle (une adresse corrigée vaut aussi pour une facture déjà préparée) ; facture d'un cédant ou d'un futur cessionnaire → l'adresse gardée sur la facture. Modèle des données +1.
- **D4-01 (loyer préparé au nom du cédant)** : Le retenir et le signaler : plus proposé à l'envoi ; ligne du suivi « préparé au nom de SARL X, la période est à SARL Y : à refaire » ; « Refaire » le refait au même numéro au nom de SARL Y.
- **D4-02 (menu de la pastille)** : « À faire » et « Automatique » retirés d'une facture partie (envoyée, impayée, payée) ; « Payé » par erreur se défait par « Envoyé » ; exception : un loyer envoyé pour une période entièrement après un départ saisi ensuite garde « Automatique », avec un rappel d'envoyer un avoir ; facture seulement préparée : « Automatique » reste.
- **D6-04 (période ouverte par « Facturer le loyer »)** : La période à envoyer : la première, à partir de celle en cours, dont le loyer n'est pas parti ; l'alerte ouvre la période dont elle parle ; une période déjà partie s'annonce (titre « (correction) », bouton « Corriger et envoyer »).
- **D5-05 / D5-07 (Super PDP indisponible)** : Refuser avant l'envoi, au centre, avec la raison ; après le 1/9/2027 un loyer préparé sans facture électronique ne part pas à sa date et est signalé à chaque lancement ; une SCI non reliée ne corrige plus une facture déjà transmise.
- **D5-06 (encaissement de la facture remplacée)** : Annulé tout seul, à sa date : avant de transmettre la rectificative, « Encaissée » négatif du même montant sur l'ancienne facture, daté comme l'encaissement annulé ; la rectificative reçoit le sien une fois payée. Modèle +1. Essais au bac à sable d'abord.
- **D5-11 (« Encaissée » d'un avoir)** : En positif (+297,60 € pour un avoir de 297,60 €), après un essai au bac à sable ; codé seulement si accepté, puis à confirmer par l'expert-comptable.
- **Avenant de périodicité (date d'effet)** : Le jour de demande du loyer (« Loyer demandé le 5 du mois » → le 5), de n'importe quel mois après la dernière période facturée ; périodes d'avant à l'ancienne périodicité ; période coupée au prorata des mois ; mois déjà facturés jamais reproposés ; provisions converties d'office (modifiables).

## Corrections sans décision (règles déjà prises ; à contester au besoin)

- **D5-04** : Un refus de Super PDP sur « Encaissée » ou sur son annulation n'arrête plus le reste : FactureElectronique.Traiter (:494-516) note la facture pour la session et passe à la ligne suivante, Demarrer (:426-431) et Rattraper (:300-303) ne s'arrêtent plus que sur une panne (aide ArreteLaTournee, fausse pour « refusee », « origine » et null), et les refus d'une tournée sont dits ensemble dans une seule fenêtre au centre, avec nouvel essai au prochain lancement : c'est la décision du 01/11 appliquée, sans nouveau choix.
- **D5-08** : Le taux d'une correction est celui de la période facturée : la correction d'une facture partie propose le taux imprimé sur cette facture, 0 compris (Tva.TauxPropose, lu par ChargerTva et TauxFacture des trois formulaires : FactureLoyerPanel.cs:1321-1342, FactureRegulPanel.cs:1156-1177, FactureRefacPanel.cs:969-990), modifiable même vers ou depuis 0, sans devenir le taux des factures suivantes du type. C'est le taux du document, et non le bail du jour (Tva.Soumis), qui décide du passage par Super PDP, pour que la correction d'une facture transmise d'un bail devenu exonéré reparte comme l'originale.
- **D5-10** : Régularisation et refacturation suivent la règle du loyer (décisions des 30 et 31/10, limite connue du 02/11 levée) : avant le 1/9/2027 leur e-mail est la « copie pour information », après il n'y a plus de PDF mais « Transmettre la facture par la plateforme ? » puis la notification, avec une seule règle pour choisir et garder le modèle d'e-mail (EmailService). Dans les trois formulaires, un SIRET tapé met aussitôt à jour la ligne « Envoi », la case retard et le mode de l'e-mail, et la notification par défaut ne dit plus « à régler le … » pour un avoir.
- **D6-05** : « 01-12-2026 » et « 1.12.2026 » sont lus comme le 01/12/2026 ; une date vide ou illisible (date de la facture, échéance, date d'une charge) est refusée au centre avant toute émission, aperçu compris (SaisieDate.Refus en tête de GenererApercu et SauvegarderEtEnvoyer des quatre formulaires, et à « Enregistrer » d'une charge avec le nom vide de D10-06 et un coût illisible), le formulaire restant ouvert avec la saisie. Un coût laissé vide reste 0 € et une charge sans locataire coché reste permise, comme aujourd'hui.
- **D6-08** : {echeance} est remplie dans les quatre formulaires (BuildContext), comme à l'envoi au lancement ; le menu « / » ne propose une variable que là où elle est remplacée ({montant} dans la phrase du montant mensuel, {depot.*} dans l'explication du dépôt, {reste} dans le modèle du rappel), et l'explication du dépôt reçoit le contexte de la facture ({numero}, {date}, {echeance}).
- **D6-09** : Une charge portée sur une facture d'un locataire du bâtiment (archivés compris) ne se supprime plus : refus au centre qui nomme les locataires et dit quoi faire (mettre son coût à 0, puis corriger la facture depuis le suivi), comme pour un RIB ou un entête utilisé (décision du 01/11). Au passage, la correction d'une régularisation garde une charge « Réglée pour tous » qu'elle couvrait déjà (FactureRegulPanel.cs:506), comme le fait la refacturation.
- **AVENANT-PERIODICITE-PROVISION** : Quand la périodicité change dans Gestion du loyer, la provision générale et celle de chaque liste sont converties d'office d'après la valeur enregistrée (100 €/mois → 300 €/trimestre), sauf champ retouché, et restent modifiables ; une période de l'ancienne périodicité appelle la provision ramenée à sa périodicité, une période coupée appelle la provision entière comme une arrivée ou un départ aujourd'hui. La régularisation additionne les provisions réellement appelées, période par période (montant imprimé sur le loyer quand il est connu, sinon celui de la fiche ramené), au lieu de provision × nombre de périodes.

## Déjà corrigé en partie

Aucun défaut du thème 2c n'est entièrement corrigé par le lot 1 (03/11) ni par les thèmes 2a et 2b (04-05/11) ; seules des parties le sont :, D4-01, en partie : l'envoi au lancement d'un loyer suit le titulaire de la période après une cession (D7a-03, 04/11 ; FactureEnvoiAuto.cs:72-80, CessionBail.cs:109-127). Restent : rappel, e-mail de la plateforme, régularisation, refacturation, dépôt, adresse de facturation du cédant et loyer préparé avant la saisie de la cession., D5-04, en partie : ses déclencheurs sont réduits (« Encaissée » attend 5 minutes après la transmission, retour BN9, FactureElectronique.cs:349-357 ; pas avant le remboursement, BN17, :496). Le blocage lui-même est intact (:500-501, :511-512, :429-431)., AVENANT-PERIODICITE, en partie : D4-04 (04/11) garde le libellé des loyers émis (FacturationSuivi.cs:210-213) et refuse de corriger une facture d'une autre périodicité (FactureEmission.cs:93-100). Le changement daté reste à faire.

## Lots, dans l'ordre

### Lot 1 · Pastille : une facture partie ne se dé-émet plus (D4-02)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Décision D4-02. Avant les lots 14-15 (la règle de date de l'avenant lit le numéro, le PDF et l'état stocké que ce lot protège).
- Fichiers :
  - `Assets/Script/Facturation/LocataireSuiviInline.cs:436-456, 486-489 (menu ; confirmation de l'exception du départ)`
  - `Assets/Script/Facturation/FacturationSuivi.cs:584-604 (SetStatut ; aide « facture partie » commune au menu)`
  - `Assets/Script/Facturation/FactureEmission.cs:61-100 (Preparer : ligne remise = même numéro, sans le refus de périodicité)`
  - `Assets/Script/Locataire/DepartLocataire.cs:164-174 (période entièrement après le départ)`
  - `Assets/Editor/Tests/Revue17Tests.cs`

### Lot 2 · « Facturer le loyer » ouvre la période à envoyer (D6-04)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Décision D6-04. Avec D10-14 (même titre, garder le nom du locataire). PeriodeAOuvrir lit les lignes planifiées (FacturationSuivi.Lignes) : l'avenant (lots 14-15) la fera suivre sans retouche.
- Fichiers :
  - `Assets/Script/Facturation/FactureLoyerPanel.cs:137-148, 709-713, 866-870, 1058-1066, 1206-1226`
  - `Assets/Script/Facturation/FactureEmission.cs:59-71 (aide commune « sera une correction »)`
  - `Assets/Script/Facturation/FacturationAlertes.cs:44-58, 124-129, 150-161 (clé de l'alerte, non enregistrée)`
  - `Assets/Script/General/HomeAlert.cs:26, 177-180, 312-326`
  - `Assets/Editor/Tests/Revue17Tests.cs`

### Lot 3 · Dates illisibles refusées dans les formulaires et la fiche d'une charge (D6-05)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Aucune. Même geste conseillé : D10-06 (nom vide d'une charge, même garde) et D8-08 (années à deux chiffres, même SaisieDate).
- Fichiers :
  - `Assets/Script/Tools/SaisieDate.cs:13-20`
  - `Assets/Script/Facturation/FactureLoyerPanel.cs:445-462`
  - `Assets/Script/Facturation/FactureRegulPanel.cs:786-803`
  - `Assets/Script/Facturation/FactureRefacPanel.cs:658-672`
  - `Assets/Script/Facturation/FactureDepotPanel.cs:804-815`
  - `Assets/Script/Facturation/ChargePanel.cs:195-199, 488-492, 580-583`
  - `Assets/Script/Locataire/LocataireFacturationFields.cs:59-62`
  - `Assets/Editor/Tests/SaisieDateTests.cs`
  - `Assets/Editor/Tests/Revue17Tests.cs`

### Lot 4 · Une charge déjà facturée ne se supprime plus (D6-09)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Aucune.
- Fichiers :
  - `Assets/Script/Facturation/ChargeBatiment.cs:46-71 (RefusSuppression)`
  - `Assets/Script/Achat/InvestissementListPanel.cs:285-298`
  - `Assets/Script/Facturation/FactureRegulPanel.cs:506`
  - `Assets/Editor/Tests/Revue17Tests.cs`

### Lot 5 · Variables du menu « / » : échéance remplie, menu filtré par champ (D6-08)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Après le lot 3 (échéance lisible garantie à l'émission). Mêmes fichiers : D6-12 et D6-14 (mineurs) peuvent être faits au passage.
- Fichiers :
  - `Assets/Script/Facturation/FactureLoyerPanel.cs:1017-1031`
  - `Assets/Script/Facturation/FactureRegulPanel.cs:684-700`
  - `Assets/Script/Facturation/FactureRefacPanel.cs:590-604`
  - `Assets/Script/Facturation/FactureDepotPanel.cs:625-636, 662`
  - `Assets/Script/Facturation/SlashAutocomplete.cs:87-119`
  - `Assets/Script/Facturation/ExplicationDepot.cs:112-118`
  - `Assets/Script/Facturation/ReglagePanel.cs:1130-1175 (BlocModele)`
  - `Assets/Script/Facturation/FactureVariables.cs:53-73 (commentaires)`
  - `Assets/Editor/Tests/FactureVariablesTests.cs:118-131`
  - `Assets/Editor/Tests/Revue17Tests.cs`

### Lot 6 · Destinataire et acheteur gardés sur la facture (D4-01)
- Serveur : aucun (les champs voyagent dans le JSON de la fiche ; le serveur compare déjà le numéro de modèle, etape17a)
- Nouveau champ persisté : oui
- Dépendances : Les deux décisions D4-01. Avant les lots 11 à 13, qui lisent le destinataire et l'acheteur (envoi au lancement, e-mail de la plateforme). Modèle 7 → 8.
- Fichiers :
  - `Assets/Script/Facturation/FactureEtat.cs (destinataire, acheteur)`
  - `Assets/Script/Facturation/FacturationSuivi.cs:222-239 (CopieDe)`
  - `Assets/Script/Facturation/FactureEnvoiAuto.cs:40-80, 203-235, 445-474`
  - `Assets/Script/Locataire/CessionBail.cs:34-48, 82-127`
  - `Assets/Script/Facturation/FactureRappelService.cs:46`
  - `Assets/Script/Facturation/FactureElectronique.cs:523-525`
  - `Assets/Script/Facturation/FactureLoyerPanel.cs:631-682, 1153-1170`
  - `Assets/Script/Facturation/FactureRegulPanel.cs:318, 362, 938-979`
  - `Assets/Script/Facturation/FactureRefacPanel.cs:312, 360, 778-819`
  - `Assets/Script/Facturation/FactureDepotPanel.cs:393, 455, 934-946`
  - `Assets/Script/Facturation/LocataireSuiviInline.cs:523-529`
  - `Assets/Script/Cloud/DonneesCloud.cs:17`
  - `Assets/Editor/Tests/Revue17Tests.cs:57`

### Lot 7 · Un refus d'« Encaissée » ne bloque plus le locataire ni la tournée (D5-04)
- Serveur : aucun (facultatif : la fonction facture-electronique rendrait « pas encore lue » comme refus passager, ts:202 ; alors déployer CIPL avant la fonction ; redéploiement par l'utilisatrice)
- Nouveau champ persisté : non
- Dépendances : Avant les lots 8 et 9 (leurs déclarations peuvent être refusées).
- Fichiers :
  - `Assets/Script/Facturation/FactureElectronique.cs:290-331, 389-439, 443-518`
  - `Assets/Editor/Tests/Revue17Tests.cs`
  - `(facultatif) Serveur/fonctions/facture-electronique.ts:200-202 et Assets/Editor/Tests/FactureElectroniqueTests.cs:281`

### Lot 8 · Signe de l'« Encaissée » d'un avoir (D5-11)
- **Fait le 17/11** (988 tests EditMode verts) : `Encaissement` rend la valeur absolue du montant ; l'annulation (`Traiter`) part à l'opposé de ce qui a été déclaré, quel que soit son signe. Test `Revue17Tests.D5_11_encaissee_d_un_avoir_en_positif` ; `LotGTests` adapté (l'arrondi d'un avoir donne +500). Résultat de l'essai au bac à sable de Super PDP dans Démo CIPL le 17/11 (bâtiment « Essai lot 8 », locataire « Essai Lot 8 », SIRET fictif 00000000200000) : refacturation 2026/11001 de 120,00 € (type 380, identifiant 2161064) puis avoir 2026/11002 de −120,00 € (type 381, identifiant 2161131), tous deux « Déposée (validée) » ; avoir passé « Payé » : événement fr:212 « Encaissée » de +120,00 accepté ; avoir repassé « Envoyé » : fr:212 de −120,00 accepté (« 1 encaissement annulé »). Relevé avant de coder, dans les données de SCI (JBC) : le 04/11, deux avoirs de Locataire BI2 avaient été déclarés en négatif (−297,60 et −60,00) et acceptés : le bac à sable accepte donc les deux signes ; le positif est retenu pour son sens (le négatif veut dire « annulation »). Reste : l'avis de l'expert-comptable. SCI (JBC) ne pouvait pas servir : Super PDP répondait « 403 User account is being validated ».
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Décision D5-11 et essai au bac à sable AVANT de coder. Après le lot 7. À tester avec le parcours « À rembourser » (avoir remboursé, BN19).
- Fichiers :
  - `Assets/Script/Facturation/FactureElectronique.cs:262-271`
  - `Assets/Editor/Tests/Revue17Tests.cs`

### Lot 9 · Annulation de l'« Encaissée » de la facture remplacée (D5-06)
- Serveur : aucun (l'action « encaisser » accepte déjà un montant négatif et une date, parcours BE5)
- Nouveau champ persisté : oui
- Dépendances : Décision D5-06. Après les lots 7 et 8 ; essais (a), (b) et (d) au bac à sable avant de fixer l'ordre « annulation d'abord ». Modèle +1 (9 après le lot 6). À tester avec le parcours « À rembourser » (correction à la baisse, « Remboursé… »). Le point « À vérifier » du transfert (plateformeOrigineId, D5.md) est à régler avec.
- Fichiers :
  - `Assets/Script/Facturation/FactureEtat.cs (encaisseeAAnnulerId)`
  - `Assets/Script/Facturation/FactureElectronique.cs:125-146, 233-244, 307-311, 373-385, 453-516`
  - `Assets/Script/Facturation/FacturationSuivi.cs:222-239, 512-516`
  - `Assets/Script/Cloud/DonneesCloud.cs:17`
  - `Assets/Editor/Tests/Revue17Tests.cs`

### Lot 10 · Correction au taux de la facture corrigée (D5-08)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Avant le lot 11 : la correction d'une facture transmise doit suivre le taux de la facture, sinon la garde du lot 11 refuserait pour une fausse raison.
- Fichiers :
  - `Assets/Script/Facturation/Tva.cs:25-32 (TauxPropose)`
  - `Assets/Script/Facturation/FactureLoyerPanel.cs:260-266, 709-713, 1278, 1321-1342`
  - `Assets/Script/Facturation/FactureRegulPanel.cs:996-1001, 1066-1067, 1156-1177`
  - `Assets/Script/Facturation/FactureRefacPanel.cs:431-433, 836-841, 881-882, 969-990`
  - `Assets/Script/Facturation/FactureElectronique.cs:88-96, 218-227`
  - `Assets/Editor/Tests/TvaTests.cs`

### Lot 11 · Super PDP indisponible : refus avant l'envoi dans les trois formulaires (D5-07, D5-05)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Décision D5-05/D5-07. Après les lots 6 et 10.
- Fichiers :
  - `Assets/Script/Facturation/FactureElectronique.cs:88-90, 199-208, 218-227 (DoitPartirParPlateforme, RefusCorrection avec parPlateforme)`
  - `Assets/Script/Facturation/FactureLoyerPanel.cs:491-536, 656-691`
  - `Assets/Script/Facturation/FactureRegulPanel.cs:849-894, 957-991`
  - `Assets/Script/Facturation/FactureRefacPanel.cs:680-733, 800-831`
  - `Assets/Editor/Tests/Revue17Tests.cs`

### Lot 12 · Envoi au lancement après le 1/9/2027 : facture sans facture électronique retenue (D5-05)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Décision D5-05/D5-07. Après les lots 6 (DoitPartir, acheteur de la ligne) et 11 (même règle DoitPartirParPlateforme).
- Fichiers :
  - `Assets/Script/Facturation/FactureEnvoiAuto.cs:40-66, 162-235, 440-447, 486-530`
  - `Assets/Script/Facturation/FactureElectronique.cs:402-406 (PartParPlateforme, ARefaire, toast)`
  - `Assets/Script/Cloud/DemarrageCloud.cs:251-260`
  - `Assets/Editor/Tests/EnvoiAutoTests.cs`

### Lot 13 · Régularisation et refacturation : copie puis notification comme le loyer, SIRET tapé suivi (D5-10)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Après les lots 6 (Notifier lit la ligne) et 11 (mêmes lignes des formulaires).
- Fichiers :
  - `Assets/Script/Facturation/EmailService.cs:243-270`
  - `Assets/Script/Facturation/FactureRegulPanel.cs:257-259, 356-364, 890-894, 1070-1071`
  - `Assets/Script/Facturation/FactureRefacPanel.cs:354-362, 729-733, 885-886`
  - `Assets/Script/Facturation/FactureLoyerPanel.cs:214, 908-936, 1285-1300`
  - `Assets/Script/Facturation/FactureElectronique.cs:215-227, 479-485`
  - `Assets/Script/Facturation/ReglagePanel.cs:1133-1141`
  - `FACTURATION_CIPL_PENNYLANE.md (limite connue du 02/11)`
  - `Assets/Editor/Tests/Revue17Tests.cs`

### Lot 14 · Avenant de périodicité : historique daté et plan des périodes (AVENANT-PERIODICITE)
- Serveur : aucun (ne jamais renommer une clé de loyer existante : reserver_envoi la cherche telle quelle, Serveur/etape16c4_supabase.sql:47-49)
- Nouveau champ persisté : oui
- Dépendances : Décision de l'avenant. Après les lots 1 et 2. Modèle +1 (10 après les lots 6 et 9).
- Fichiers :
  - `Assets/Script/Locataire/Locataire.cs:47-101 (historiquePeriodicites)`
  - `Assets/Script/Cloud/DonneesCloud.cs:17`
  - `Assets/Script/Facturation/FacturationSuivi.cs:98-112, 765-803, 833-845, 876-943 (Plan)`
  - `Assets/Script/RevisionLoyer/Loyers.cs:118-170, 299-344`
  - `Assets/Script/RevisionLoyer/LoyerHistoryService.cs:112-118`
  - `Assets/Script/Facturation/FacturationAlertes.cs:44-49, 150-161`
  - `Assets/Script/Facturation/FactureLoyerPanel.cs (PeriodeAOuvrir, lot 2 : elle parcourt 1..NbPeriodes de la périodicité actuelle sans lire FacturationSuivi.Lignes ; à faire suivre, avec FacturationAlertes.ProchaineEcheanceLoyer)`
  - `Assets/Script/Facturation/FactureEmission.cs:90-100 (message)`
  - `Assets/Editor/Tests/AvenantPeriodiciteTests.cs (nouveau)`
  - `Assets/Editor/Tests/Revue17Tests.cs:57`

### Lot 15 · Avenant : formulaire du loyer et date d'effet dans Gestion du loyer (AVENANT-PERIODICITE, AVENANT-PERIODICITE-DATE)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Après le lot 14.
- Fichiers :
  - `Assets/Script/Facturation/FactureLoyerPanel.cs:137-163, 260-266, 404-405, 464-470, 678, 867-870, 892-920, 1036-1122, 1220-1226`
  - `Assets/Script/RevisionLoyer/RevisionPanel.cs:973-975, 1276-1311, 1582-1602, 1646-1659, 1699-1757`
  - `Assets/Script/Facturation/FactureVarResolver.cs:57, 76`
  - `Assets/Editor/Tests/AvenantPeriodiciteTests.cs`

### Lot 16 · Avenant : provisions converties et régularisation de l'année du changement (AVENANT-PERIODICITE-PROVISION)
- Serveur : aucun
- Nouveau champ persisté : non
- Dépendances : Après le lot 14. Avec D6-16 (parts par liste qui ignorent la provision saisie, même endroit).
- Fichiers :
  - `Assets/Script/RevisionLoyer/RevisionPanel.cs:1259-1265, 1703-1705, 1802-1815`
  - `Assets/Script/RevisionLoyer/Loyers.cs (ProvisionConvertie, ProvisionsAppelees)`
  - `Assets/Script/Facturation/FactureRegulPanel.cs:633-634, 1008-1010`
  - `Assets/Script/Facturation/FactureLoyerPanel.cs:892-895, 974`
  - `Assets/Editor/Tests/AvenantPeriodiciteTests.cs`

## Remarques de la synthèse

Regroupement des questions : D4-01 donne deux questions (règle de l'adresse ; loyer préparé au nom du cédant). D5-05 et D5-07 sont le même choix (facture qui doit passer par Super PDP et ne le peut pas). Les sous-questions des vérifications sont posées dans les options : rappel au cédant, lignes préparées avant la mise à jour, sortie d'un loyer envoyé après un départ, « Payé » cliqué par erreur, message à chaque lancement, autre plateforme agréée, sous-choix de D5-06, prorata au mois de l'avenant.
Classés sans décision, parce qu'ils appliquent une décision déjà prise ou une règle (défaut retenu, à contester au besoin) : D5-04 (01/11), D5-10 (30/10), D6-09 (refus comme pour le RIB et l'entête, 01/11), D5-08 (taux de la période facturée), D6-05, D6-08, provisions de l'avenant. Restent possibles plus tard : l'option « marquer la ligne Encaissée refusée » de D5-04, et le contrôle « aucun {…} dans ce qui part » de D6-08.
Choix technique de l'avenant, pris sans question car il ne change rien à l'écran : le repère des loyers reste « loyer-{année}-P{n} », avec n = rang dans l'année (le 3e trimestre 2026 devient P7 après six mois). Les deux autres repères ajoutent un second format de clé à lire partout.

RISQUES TRANSVERSES
1. Modèle des données : il monte trois fois (lots 6, 9 et 14 : 7 → 8 → 9 → 10). À chaque fois :
- recopier l'empreinte de Revue17Tests.D2_01 (:57) ;
- recopier les nouveaux champs de FactureEtat dans FacturationSuivi.CopieDe (:222-239). Le test par réflexion Suivi_la_copie_affichee_porte_tous_les_champs le vérifie, sans retouche ;
- pour encaisseeAAnnulerId, encaisseeMontant et encaisseeDateISO, les recopier aussi dans FactureElectronique.RecopierPlateforme (:373-385).
Un poste pas à jour voit ses écritures refusées par le serveur (etape17a) : mettre à jour tous les postes.
2. Formulaires loyer, régularisation et refacturation : huit lots les touchent (2, 3, 5, 6, 10, 11, 13, 15), aux mêmes endroits (SauvegarderEtEnvoyer/Emettre, Finaliser, ChargerTva, SaveFromUI). Faire les lots dans l'ordre et relire le code avant chacun.
3. Tests texte à garder :
- Revue16cTests.cs:437 (« if (!correction && dateEnvoi.HasValue »), :216-217 (« if (Math.Abs(d.ttc) <= 0.004f) »), :203-204 (LibelleEnvoi dans les trois formulaires), :346 (exactement trois « DatesIllisibles() is string refusDate » : enrichir la fonction au lieu d'ajouter un appel), :575 (« FacturationSuivi.EcheanceLoyer(_loc, y, per) ») ;
- Revue17Tests.cs:586 et :619 (condition « Encaissée »), :667 (« FactureElectronique.Professionnel(_loc, SiretSaisi()) ») ;
- TvaTests : « Active && Tva.Soumis(loc) », à faire suivre si Concerne change ;
- SurfacesEtEcheanceTests.cs:135 : garder PeriodeIndex(loc, mois) ;
- signature statique de MontantsParListe (Revue16cTests.cs:963-966, TvaTests.cs:46) ;
- ensembles statiques de session : prendre des identifiants propres aux tests (« 7 » est déjà pris par Revue16cTests.cs:501).
4. Nouvelles confirmations : l'exception du départ de D4-02, et « Transmettre ? » qui suit « Remboursement au locataire » pour une régularisation en avoir. D8-02 (une confirmation efface un message d'erreur) n'est toujours pas corrigé.
5. Taux du document : Concerne, SansSiret, LibelleEnvoi et la garde D5-05 doivent lire le taux du document (lot 10), pas Tva.Soumis(loc) du jour. Sinon, la correction taxée d'un bail devenu exonéré est refusée avec une fausse raison.
6. D5-04 : un refus qui dure revient à chaque lancement et empêche le transfert du locataire (TransfertCloud passe par EnAttente). Seule sortie : changer le statut de la facture, ou la corriger.

PRÉCISIONS DE RÉALISATION, RETENUES DES VÉRIFICATIONS
D4-01 :
- poser destinataire et acheteur dans les quatre Finaliser. En envoi immédiat, garder l'adresse réellement envoyée (dest), jamais le champ relu après l'envoi. EnvoyerUne la réécrit au départ réel, sauf dans la branche notification seule ;
- figer l'adresse des factures parties, et le nom des lignes émises sans acheteur, au début de Cessions.Enregistrer et de Cessions.Appliquer, avant tout changement de la fiche. L'analyse ne le faisait que dans Appliquer : c'est trop tard pour un loyer préparé avant la saisie d'une cession prévue ;
- la correction d'une facture d'un cédant reprend son titulaire (la cession dont ancienNom = acheteur) et l'adresse de la ligne, dans les quatre formulaires ;
- Diagnostic : « à refaire » passe avant « refusée par la plateforme » ;
- la quittance n'est pas touchée.
D4-02 :
- une seule aide « facture partie », lue par le menu et par SetStatut ;
- une ligne « remise » (numéro et PDF, statut vide) n'est pas soumise au refus de périodicité de D4-04.
D6-04 :
- le titre « (correction) » et le bouton « Corriger et envoyer » suivent la règle de Preparer (EstDejaEmise, et pas « en attente d'envoi »), pas PeriodePartie ;
- poser le titre avant la garde « _btnSauver == null ».
D5-06 :
- l'« Encaissée » de la rectificative attend que l'annulation soit passée. Sinon, son montant écrase celui à annuler : −880 € au lieu de −1 000 € ;
- montant inconnu : prendre le total TTC de l'ancien JSON ;
- course rare (correction pendant un « Encaissée » en cours) : la noter dans un commentaire ponytail:.
D5-05 :
- DemarrageCloud.ReprendreEnLigne (:251-260) ne relit pas l'état de Super PDP : il faut le relire, sinon tout est refusé pendant toute la session après un démarrage hors ligne ;
- refaire le contrôle après Retrouver (EnvoyerUne) et pour la file Attente ;
- ProposerAuDemarrage nomme les lignes retenues avant ses retours anticipés ;
- dans le loyer, le refus passe avant l'envoi différé (FactureLoyerPanel.cs:505-536).
Avenant :
- montrer le bloc de date quand les mois effectifs changent, pas la liste enregistrée (CocherMoisStandard la remplit à l'ouverture) ;
- refuser avant EnregistrerAvenant ;
- reconstruire les options de période seulement pour une année à 4 chiffres ;
- AppliquerLigneCiblee, la provision et la ligne « mensuel » du formulaire lisent le plan ;
- prorata au mois : l'année fait alors exactement le loyer annuel. Au jour (règle du 01/10), elle ferait 12 010,99 € pour 12 000 € ;
- une reprise de facturation compte comme facturée ;
- préremplir depuis l'avenant de loyer au 1er du mois suivant.

BAC À SABLE SUPER PDP (en Play, par script ; factures de test vers agathelimouzy@gmail.com seulement)
Avant le lot 8 (D5-11) :
- lire les événements de l'avoir de BN19, ou de la régularisation corrigée(4) de BI8 (action « evenements » ; elle n'a pas d'appelant dans CIPL : lire la réponse brute, comme pour BE5) ;
- le −297,60 € a-t-il été accepté ? Sinon, le rejouer sur Locataire BI2 ;
- puis, avec le changement : +297,60 € accepté, et « Envoyé » → −297,60 € accepté.
- **Fait le 17/11 dans Démo CIPL, avec un avoir de 120,00 € : +120,00 accepté, puis −120,00 accepté.**
Avant le lot 9 (D5-06) :
- (a) événements de l'originale et de la rectificative de juillet de sephora : 1 000 € et 880 € ? Un événement de remplacement ?
- (b) un 212 de −1 000 € sur l'originale déjà remplacée : accepté ou refusé ? La réponse dit si l'annulation doit partir avant la rectificative ;
- (d) un 212 négatif daté dans le passé : accepté ?
- puis (c) le parcours complet avec le code.
- **Fait le 17/11 dans Démo CIPL** (locataire « Essai Lot 8 », « Loyer Septembre 2026 », n° 2026/11003 ; code inchangé) : (a) originale 2161585 : fr:212 +600,00 puis fr:212 −600,00, tous deux datés du 05/09 ; rectificative 2161750 (type 384) : fr:212 +480,00 daté du 05/09 ; pas d'événement de remplacement sur l'originale. (b) accepté : +10,00 puis −10,00 sur l'originale déjà remplacée ; l'ordre « annulation d'abord » n'est donc pas exigé, il est gardé (annulation 2 s avant la rectificative). (d) accepté. (c) sans défaut : « À rembourser » 120,00 €, « Remboursé… » → « Payé » → « Encaissée » 480,00 € après le délai de 5 minutes.
- **D5-N2 (= D5-20), essayé le 17/11 dans le compte de Démo CIPL** (code inchangé) : entreprise « Essai D5-N2 (à supprimer) » ajoutée au groupe, « Essai Lot 8 » transféré seul. Dans la nouvelle entreprise : lignes « transmise par Démo CIPL », correction refusée au centre avant tout envoi (ligne inchangée) ; « Envoyé » puis « Payé » : annulation −480,00 et « Encaissée » +480,00 acceptées par le bac à sable, au nom de Démo CIPL ; transfert retour : marques effacées. Deux remarques, corrigées le 17/11 après décision d'Agathe (refus dès le clic sur « Corriger » ; « Envoyé » et « Impayé » refusés au menu ; cas BZ14 et BZ15) : (1) dans une entreprise sans RIB, le refus « Aucun RIB n'est enregistré » passe avant le refus de l'origine (`RefusSansRib` avant `RefusCorrection`, dans les formulaires) : on ajoute un RIB, puis on apprend que la correction est impossible ; pour l'essai, un RIB d'essai sans numéro a été posé par script dans l'entreprise d'essai ; (2) une facture « Payé » avec des versements, repassée « Envoyé » par le menu, garde ses versements : le suivi dit « Partiel · reste — » et propose « Rappel ».
Lot 7 (D5-04) : déclencheur sûr = corriger une facture payée au même montant, puis relancer CIPL dans les 5 minutes (la tournée part après 35 s). Attendu : une seule fenêtre, et les autres locataires continuent.
Lots 11 à 13 : régler EssaiApresBascule par script. Vérifier le refus avant l'envoi, le loyer préparé sans facture électronique retenu au lancement, et la régularisation et la refacturation par « Transmettre ? » puis notification.

RESTE INCERTAIN
Questions pour l'expert-comptable :
- signe de l'« Encaissée » d'un avoir (D5-11, déjà inscrit) ;
- à ajouter : le trop-perçu rendu est déclaré en net (880 € à la date du paiement), au lieu de +1 000 € puis −120 € ;
- une rectificative (384) efface-t-elle l'encaissement de la facture qu'elle remplace (D5-06) ?
- comment corriger une facture transmise vers un total négatif, ou vers un autre acheteur (SIRET faux) (D5-02) ?
D4-02 : l'exception du départ retire du suivi une facture envoyée. L'avoir est à faire à la main (Refacturation, « Avoir sur la facture n° … »).
Super PDP reporte sur l'e-reporting suivant un « Encaissée » déclaré après la clôture du mois : dater l'annulation du jour d'origine ne garantit pas la période.
D4-01 :
- {loc.nom} et {loc.email} des modèles d'e-mail personnalisés lisent toujours la fiche actuelle ;
- pour les factures d'avant, l'adresse figée est celle du réglage le jour de la cession, pas forcément celle de l'envoi réel ;
- l'adresse d'envoi gardée n'est affichée nulle part (pas prévu).
Avenant :
- les fiches de test changées sans date (BN11) restent incohérentes. Ce sont des sociétés de test, écartées au passage en réel ;
- une fin de palier qui ne tombe plus sur un début de période sera refusée par PoserPalier si on la retouche (Loyers.cs:209).
Hors thème 2c, même famille :
- le décompte de sortie prend le taux du jour (D4-08) et ne passe jamais par Super PDP (D5-18) ;
- la case retard du dépôt ne suit pas le SIRET tapé (D6-03).

## Analyse détaillée par défaut (analyse, puis vérification sceptique : la vérification fait foi)

### D4-01 · Le destinataire n'est pas gardé sur la facture : après une cession, le rappel, la notification de la plateforme et l'envoi différé recalculent l'adresse au lieu de lire celle de la facture
- État au 05/11 : partiellement corrigé · effort M · serveur : aucun. Le champ voyage dans le JSON de la fiche, et les fonctions d'écriture d'etape17a comparent déjà le numéro de modèle : pas de SQL ni de fonction edge à changer. · nouveau champ : oui
- Lieux :
  - Assets/Script/Facturation/FactureEtat.cs:8-56 : aucun champ « envoyée à ». Seule la date est gardée (dateEnvoiISO, :17)
  - Assets/Script/Facturation/FactureEnvoiAuto.cs:72-80 : Destinataire choisit dans cet ordre : titulaire de la période (loyer seulement, D7a-03), puis emailDest du TYPE, puis la fiche. Appelé par DoitPartir (:64), Retrouver (:150) et Diagnostic (:223). L'adresse obtenue est affichée dans la liste (:327) et dans la confirmation (:372)
  - Assets/Script/Facturation/FactureEnvoiAuto.cs:458 et :470-474 : EnvoyerUne envoie à c.destinataire, puis n'écrit que statut et dateEnvoiISO. L'adresse utilisée n'est gardée nulle part
  - Assets/Script/Facturation/FactureRappelService.cs:14 et :46 : Adresse appelle FactureEnvoiAuto.Destinataire(loc, l?.type) SANS la ligne. Ni la ligne ni la cession ne sont lues
  - Assets/Script/Facturation/FactureElectronique.cs:472 et :523-525 : Notifier appelle Destinataire(loc, r.type) SANS la ligne
  - Assets/Script/Locataire/CessionBail.cs:82-103 : Appliquer remplace emailLocataire (:93) et efface emailDest des quatre réglages (:98-102). CessionBail.cs:109-127 (TitulaireDe, TitulaireLigne) : loyer seulement (:123). Rend l'e-mail de la fiche du cédant (ancienEmail), pas son adresse de facturation mémorisée
  - Assets/Script/Facturation/FactureLoyerPanel.cs:872-875 et :1153-1170 : l'adresse proposée suit le titulaire de la période. À :1282, une adresse tapée n'est pas gardée quand l'acheteur vient d'une cession. Finaliser (:656-682) n'enregistre aucune adresse sur la ligne
  - Assets/Script/Facturation/FactureRegulPanel.cs:362, :879, :938-979, :1069 ; FactureRefacPanel.cs:360, :718, :778-819, :884 ; FactureDepotPanel.cs:455, :877, :934-946, :1010 : l'adresse du formulaire sert à l'envoi, puis n'est gardée que sur le réglage du type (emailDest)
  - Assets/Script/Facturation/FacturationSuivi.cs:222-239 : CopieDe. La ligne passée au rappel depuis le suivi (LocataireSuiviInline.cs:391) est cette copie
  - Assets/Script/Facturation/FactureQuittanceService.cs:25-26 : la quittance prend le nom et l'adresse de la fiche actuelle
  - Assets/Script/Facturation/FactureVarResolver.cs:60-64 : {loc.nom} et {loc.email} sont lus sur la fiche actuelle
  - Assets/Script/Cloud/DonneesCloud.cs:17 (Modele = 7) et Assets/Editor/Tests/Revue17Tests.cs:57 (empreinte « 7:1d9bc7661d3e »)
- Problème : L'adresse d'envoi n'est pas gardée sur la facture. Chaque lecteur la recalcule au moment où il en a besoin, à partir du réglage du type et de la fiche, et la cession réécrit les deux. D7a-03 a corrigé l'envoi au lancement pour le loyer (l'adresse suit le titulaire de la période). Restent cinq trous : (1) le brouillon de rappel et l'e-mail de notification de la plateforme ne passent pas la ligne ; (2) la régularisation, la refacturation et le dépôt ne suivent pas la cession ; (3) l'adresse de facturation propre au cédant (compta@…) est effacée par la cession et n'est plus retrouvée, TitulaireDe rendant l'e-mail de sa fiche ; (4) dans le formulaire du loyer, une adresse tapée pour une période de cession n'est gardée nulle part, et l'envoi différé repart sur l'adresse du titulaire ; (5) un loyer préparé au nom du cédant AVANT la saisie de la cession part chez le cessionnaire avec le PDF du cédant (limite connue de D7a-03). Enfin, on ne peut jamais dire à quelle adresse une facture est réellement partie.
- Scénario : Cession de SARL X à SARL Y saisie le 28/10/2026, avec effet le jour même. (1) Le loyer de septembre de SARL X (n° 2026/09002, parti à compta@sarl-x.fr) est impayé. Le 05/11, « Rappel » ouvre un brouillon « à destination de contact@sarl-y.fr ». (2) Le loyer de novembre avait été préparé le 25/10 au nom de SARL X (en attente, départ le 31/10). Le 31/10, l'envoi au lancement propose « SARL Y, Loyer Novembre 2026, contact@sarl-y.fr », avec le PDF au nom de SARL X. (3) Cas inverse, après la bascule plateforme (1/9/2027) : cession prévue au 01/12. Le loyer de décembre est préparé au nom de SARL Y et transmis le 16/11, mais l'e-mail qui prévient le locataire part chez SARL X, car la fiche n'a pas encore changé. (4) En cas de contestation, CIPL ne peut pas dire à quelle adresse une facture est partie.
- Correction proposée : Garder l'adresse SUR LA FACTURE.
(a) FactureEtat : nouveau champ `public string destinataire;` (adresse de CETTE facture : posée à l'émission, réécrite au départ réel, vide sur les lignes d'avant). Le recopier dans FacturationSuivi.CopieDe (:222-239). Le test Suivi_la_copie_affichee_porte_tous_les_champs le vérifie.
(b) Le poser à l'émission, dans les quatre Finaliser, sur toutes les lignes de la facture, comme tauxTva : `foreach (var l in FacturationSuivi.MemeFacture(_loc, key)) l.destinataire = (_emailEnvoi.text ?? "").Trim();`. Endroits : FactureLoyerPanel.cs, bloc :676-682 (couvre les quatre chemins : sans envoi, plateforme, envoi différé, envoi immédiat) ; FactureRegulPanel.cs:979 ; FactureRefacPanel.cs:819 ; FactureDepotPanel.cs, après :946. Pas dans FactureEmission.Enregistrer : les lignes secondaires d'une régul ou d'une refacturation sont écrites après lui. Une correction passe par le même Finaliser : la ligne garde l'adresse de la dernière version.
(c) Au départ réel, dans FactureEnvoiAuto.EnvoyerUne (:470-474) : `r.destinataire = adresse`, où adresse est la valeur passée à EmailService.Envoyer (:458), lue avant l'envoi. C'est le journal « à qui c'est parti », y compris pour les lignes d'avant.
(d) Lecture : FactureEnvoiAuto.Destinataire (:72-80) rend d'abord `rec.destinataire` s'il est rempli. Sinon il garde l'ordre actuel (titulaire de la période, emailDest, fiche) pour les lignes d'avant. DoitPartir, Retrouver et Diagnostic suivent sans autre changement. FactureRappelService.Adresse (:46) passe la ligne : `Destinataire(loc, l?.type, l)`. FactureElectronique.Notifier (:525) aussi : `Destinataire(loc, r.type, r)`.
(e) Lignes d'avant : au tout début de Cessions.Appliquer (CessionBail.cs:82), avant d'ajouter la cession et de changer la fiche, figer l'adresse des factures déjà émises qui n'en ont pas : `foreach (var r in loc.facturesEtat ?? new List<FactureEtat>()) if (string.IsNullOrWhiteSpace(r.destinataire) && FacturationSuivi.EstDejaEmise(loc, r.key, out _)) r.destinataire = FactureEnvoiAuto.Destinataire(loc, r.type, r);`. Cela garde l'adresse compta@ du cédant avant qu'elle soit effacée. Appliquer ne touche pas aux lignes autrement.
(f) DonneesCloud.Modele passe de 7 à 8, et recopier la nouvelle empreinte dans Revue17Tests.D2_01.
(g) Cas né de D7a-03, selon la décision (option A) : second champ `acheteur` (nom imprimé = _nom.text, posé au même endroit). Une aide `Cessions.HorsTitulaire(loc, rec)` est vraie pour un loyer dont l'acheteur n'est pas le titulaire de la période mais est l'un des noms du bail (fiche, cession prévue, cédants). Un nom tapé à la main n'est donc pas visé. Avec elle, DoitPartir rend faux, Diagnostic dit pourquoi, et LocataireSuiviInline.Libelle (:523-529) annote la ligne, comme « modalités … refaire ? ». « Refaire » la refait au même numéro au nom du cessionnaire, grâce à RefreshTitulaire.
(h) Au passage, la quittance (FactureQuittanceService.cs:25-26) prend le nom et l'adresse de `Cessions.TitulaireLigne(loc, l)` quand il y en a un (aide existante).
Conséquence voulue : changer l'adresse sur la fiche ne change plus celle d'une facture déjà préparée. La liste du lancement l'affiche, et « Refaire » permet de la changer.
Limite laissée : un modèle d'e-mail personnalisé qui utilise {loc.nom} ou {loc.email} lit toujours la fiche actuelle (FactureVarResolver.cs:60-64).
- Décision posée : Chaque facture gardera l'adresse (et le nom) pour laquelle elle a été préparée. Il reste un cas né de la cession : un loyer déjà préparé, en attente d'envoi, au nom de l'ancien locataire, pour une période qui revient au nouveau parce que la cession a été saisie après la préparation. Que doit faire l'envoi au lancement ?
  - (recommandée) A. Le retenir et le signaler : Il n'est plus proposé à l'envoi. Dans le suivi, la ligne indique « préparé au nom de SARL X, le bail est à SARL Y pour cette période : à refaire ». « Refaire » le refait au même numéro au nom de SARL Y. Rien de faux ne part. Il faut garder aussi sur la facture le nom de l'acheteur.
  - B. L'envoyer tel quel à l'ancien locataire, et prévenir à la saisie de la cession : Il part à l'adresse gardée, celle de SARL X, qui va avec le PDF. Au moment de la cession, un message liste les loyers déjà préparés à refaire pour SARL Y. Pas de second champ. Mais si le message est oublié, SARL X reçoit le loyer d'un mois qui n'est plus le sien.
  - C. L'envoyer au nouveau locataire, comme aujourd'hui : Le PDF au nom de SARL X part chez SARL Y. Déconseillé : le document et l'adresse ne correspondent pas.
- Test : EditMode, Revue17Tests :
(1) D4_01_l_adresse_de_la_facture_survit_a_une_cession : ligne Loyer « AttenteEnvoi » avec destinataire = compta@x.fr. Après Cessions.Enregistrer(loc, aujourd'hui, "SARL Y", …, "y@y.fr", aujourd'hui), FactureEnvoiAuto.Destinataire(loc, "Loyer", rec) vaut compta@x.fr, et FactureRappelService.Adresse(loc, rec) aussi.
(2) D4_01_une_ligne_d_avant_garde_son_adresse_a_la_cession : régul « Envoye » sans destinataire, factureRegul.emailDest = charges@x.fr. Après la cession, rec.destinataire vaut charges@x.fr (figée par Appliquer) et Adresse rend charges@x.fr.
(3) Option A, D4_01_un_loyer_prepare_au_nom_du_cedant_avant_la_cession_ne_part_pas : ligne loyer-2026-P12 « AttenteEnvoi », acheteur « SARL X », échéance 01/12/2026, PDF présent. DoitPartir("Test", loc, rec, 16/11/2026, _ => true, …) est vrai. Après une cession prévue au 01/12 vers SARL Y, il est faux. Avec acheteur = « SARL Y » (refaite), il redevient vrai.
Tests existants à faire suivre : Revue16cTests.Suivi_la_copie_affichee_porte_tous_les_champs (CopieDe) et Revue17Tests.D2_01_un_champ_ajoute_fait_monter_le_numero_de_modele (« 8:… »). Les tests D7a_03 (Revue17Tests.cs:498-500), D4-10 de la 16c (Revue16cTests.cs:983-985) et EnvoiAutoTests :193-207 restent vrais : leurs lignes n'ont pas de destinataire.
En Play : vérifier l'adresse affichée dans la liste du lancement puis répondre « Plus tard ». Aucune facture de test ne part ailleurs qu'à agathelimouzy@gmail.com.
- Interactions : D7a-03 : TitulaireDe et TitulaireLigne restent le repli des lignes d'avant. Le fait que _acheteurCession ne garde pas f.emailDest (FactureLoyerPanel.cs:1282) devient sans effet sur l'envoi, puisque la ligne garde l'adresse. Le cas né de D7a-03 est la décision ci-dessus.
D4-10 de la 16c : Adresse garde son repli.
D2-01 : modèle 7 vers 8, et nouvelle empreinte.
D7a-04 : une cession prévue appliquée au lancement passe par Appliquer, qui fige les adresses.
D4-02 (option 1) : une facture préparée, remise en automatique puis refaite, réécrit destinataire et acheteur dans Finaliser.
Retour BN9 et D5 : Notifier lit la ligne ; l'adresse annoncée dans la confirmation « Transmettre » (FactureLoyerPanel.cs:544-548) est celle qui sera utilisée.
D4-04 et D6-07 : même schéma (champ posé dans Finaliser, recopié par CopieDe).
D8-03 : l'adresse s'écrit dans le même enregistrement que le statut « Envoyé ».
Limites : variables {loc.nom}/{loc.email} des modèles d'e-mail personnalisés ; les lignes « en attente » préparées avant la mise à jour gardent le comportement actuel (pas d'acheteur à comparer).
- **Vérification : à corriger**
  - Corrections : Lieux : tous vérifiés dans le code actuel, et les numéros de ligne sont justes : FactureEnvoiAuto.cs:64, 72-80, 150, 223, 327, 372, 458, 470-474 ; FactureRappelService.cs:14 et 46 ; FactureElectronique.cs:472 et 525 ; CessionBail.cs:82-103 et 109-127 ; FactureLoyerPanel.cs:872-875, 1153-1170, 1282 et 656-682 ; Regul :362, :879, :938-979, :1069 ; Refac :360, :718, :778-819, :884 ; Dépôt :455, :877, :934-946, :1010 ; FacturationSuivi.cs:222-239 ; DonneesCloud.cs:17 ; Revue17Tests.cs:57.
Le problème est réel aujourd'hui : le rappel et Notifier appellent Destinataire sans la ligne. « Partiellement corrigé » est exact : D7a-03 ne couvre que l'envoi au lancement d'un loyer.

Ce qu'il faut corriger dans la proposition :

1) (e) fige aussi les loyers « en attente d'envoi », parce que EstDejaEmise compte AttenteEnvoi (FacturationSuivi.cs:450-452). Cas concret : un loyer préparé AVANT la mise à jour, au nom de SARL X, pour une période qui passe à SARL Y. Il reçoit l'adresse de X. Comme il n'a pas d'`acheteur`, HorsTitulaire est faux, donc il part chez X pour un mois qui n'est plus le sien. C'est le contraire de la limite annoncée (« gardent le comportement actuel »). Deux façons de corriger :
- dans (e), poser aussi `acheteur` sur ces lignes, avec la règle de RefreshTitulaire (`Cessions.TitulaireLigne(loc, r)?[0] ?? loc.Name`), calculé avant le changement de fiche ;
- ou ne figer que les factures parties (DejaTraite), et garder la limite telle qu'elle est écrite.

2) Une correction faite après la cession ne lit pas la ligne :
- Loyer : RefreshTitulaire (FactureLoyerPanel.cs:1157-1161, appelé par AppliquerLigneCiblee via :1134) propose l'e-mail de fiche du cédant (ancienEmail). Le Finaliser de (b) écrase alors l'adresse compta@ figée par (e).
- Régul, refac, dépôt : le nom et l'e-mail viennent du réglage du type, vidé par la cession, sinon de la fiche (FactureRegulPanel.cs:318 et :362, FactureRefacPanel.cs:312 et :360, FactureDepotPanel.cs:393 et :455). La correction d'une facture de SARL X part donc chez SARL Y, à son nom.
Le trou (2) n'est corrigé que pour le rappel. À ajouter : pré-remplir l'e-mail (et le nom avec (g)) depuis la ligne ciblée quand ils existent. Sinon, l'écrire comme limite.

3) (b), envoi immédiat : garder `dest`, c'est-à-dire l'adresse réellement passée à EmailService.Envoyer (FactureLoyerPanel.cs:638 ; même chose dans les trois autres EnvoyerPuisFinaliser). Ne pas relire `_emailEnvoi.text` dans Finaliser après l'envoi : le champ reste modifiable pendant l'envoi.

4) (c), branche « notification seule » (FactureEnvoiAuto.cs:447) : il n'y a pas d'appel à EmailService.Envoyer. Préciser qu'on garde l'adresse posée à l'émission, celle que Notifier utilisera.

5) Formulation du trou (2) : une NOUVELLE régul, refac ou facture de dépôt émise après la cession part au cessionnaire, et c'est la règle écrite (CessionBail.cs:24-25). Ce n'est pas un défaut. Le trou ne concerne que les factures déjà émises : rappel, notification, correction.

6) (h), la quittance : hors sujet. Elle n'existe que hors bail commercial (LocataireSuiviInline.cs:403), où une cession est rare. À retirer, ou à faire lire `acheteur` de (g) plutôt que TitulaireLigne.

7) Tests :
- le test (2) doit donner un pdfPath à la régul, sinon EstDejaEmise est faux (FacturationSuivi.cs:452) et rien n'est figé ;
- Revue16cTests.Suivi_la_copie_affichee_porte_tous_les_champs n'est pas à modifier : il parcourt les champs string par réflexion (Revue16cTests.cs:833-852) et échoue seulement si CopieDe oublie le champ.

Le reste tient : le modèle passe de 7 à 8 avec la nouvelle empreinte. Aucun SQL : la réservation (etape16c4_supabase.sql:47) ne lit que `statut`. Les tests D7a_03 (Revue17Tests.cs:498-500), D4-10 de la 16c (Revue16cTests.cs:983-985) et EnvoiAutoTests:193-207 restent vrais.
  - Risques : - Changement de comportement pour tous les locataires, pas seulement en cas de cession. Avec (d), un locataire qui change d'adresse de facturation (sur la fiche ou dans le formulaire) garde l'ancienne pour un loyer déjà préparé et pour tous ses rappels. Le rappel reste un brouillon modifiable (FactureRappelService.cs:29). L'envoi différé, lui, ne l'est pas.
- (e) n'est qu'une approximation pour les lignes d'avant : l'adresse figée est celle du réglage du type le jour de la cession, pas forcément celle de l'envoi réel. Le « journal » n'est fiable que pour les factures émises après la mise à jour.
- Modèle 8 : tant qu'un poste n'est pas mis à jour, ses enregistrements sont refusés par le serveur (etape17a). À annoncer.
- (g) : sans branche dédiée, Diagnostic (FactureEnvoiAuto.cs:219-228) retombe sur « refusée par la plateforme ». La branche HorsTitulaire doit passer avant.
  - Décision manquante relevée : 1) Une règle générale est posée comme « conséquence voulue » sans avoir été demandée : une facture garde-t-elle l'adresse de sa préparation quand l'adresse du locataire change, hors cession ? Deux options :
- A. Oui, partout (envoi différé et rappel) ; « Refaire » pour la changer.
- B. L'adresse gardée sert de journal et de repli après une cession ; sinon l'envoi et le rappel suivent l'adresse actuelle du type.

2) À qui va le rappel d'une facture du cédant restée impayée après la cession ? Au cédant (débiteur nommé sur la facture), ou au cessionnaire ? La note du 06/10 (« cédant et cessionnaire s'arrangent entre eux (dépôt, factures) », CessionBail.cs:5-7) laisse un doute.

3) Avec l'option A de la cession : les loyers en attente préparés avant la mise à jour, qui n'ont pas d'acheteur, sont-ils retenus eux aussi (acheteur déduit au moment de la cession), ou partent-ils comme aujourd'hui ?

### D4-02 · « À faire » et « Automatique » du menu de la pastille dé-émettent une facture numérotée : la refaire prend un second numéro et la première sort des créances
- État au 05/11 : présent · effort S · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Facturation/LocataireSuiviInline.cs:486-489 : « Impayé », « Envoyé », « À faire » et « Automatique » sont ajoutés sans condition. Le menu s'ouvre sur toute ligne non grisée (:352)
  - Assets/Script/Facturation/LocataireSuiviInline.cs:436-456 : `set` appelle SetStatut. Seule garde : « Payé » est refusé tant qu'il reste à rembourser (BN17, :439-445)
  - Assets/Script/Facturation/LocataireSuiviInline.cs:369-382 : PDF présent et état « À faire » : le bouton devient « Refaire »
  - Assets/Script/Facturation/FacturationSuivi.cs:584-604 : SetStatut n'a aucune garde sur le numéro ni le PDF, et écrit le statut sur toutes les lignes de la facture
  - Assets/Script/Facturation/FacturationSuivi.cs:273-275 et :290-295 : un statut vide ou « AFaire » donne À faire ou À venir
  - Assets/Script/Facturation/FacturationSuivi.cs:445-453 : EstDejaEmise dépend de l'ÉTAT, pas du numéro
  - Assets/Script/Facturation/FacturationSuivi.cs:731-745 (DuesDe : Envoyé et Impayé seulement) et :757-762 (DejaTraite)
  - Assets/Script/Facturation/FactureEmission.cs:61-78 : Preparer traite une facture non émise comme neuve et prend le numéro proposé. À :142-156, Enregistrer appelle MarquerEnvoye puis avance la séquence de 1
  - Assets/Script/Facturation/FacturationSuivi.cs:398-412 : MarquerEnvoye remplace numéro, PDF et date, et remet corrections à 0
  - Assets/Script/Facturation/FactureLoyerPanel.cs:477 et FactureRegulPanel.cs:865 : le nom du fichier n'a pas de numéro, donc le nouveau PDF écrase l'ancien. Refacturation et dépôt mettent le numéro dans le nom (FactureRefacPanel.cs:704, FactureDepotPanel.cs:862) : l'ancien PDF reste orphelin
  - Assets/Script/Facturation/FactureLoyerPanel.cs:662-664 : une facture transmise puis refaite cite l'ancienne (FactureEn16931.Remplacee) et repart en rectificative sous un autre numéro
  - Assets/Script/Facturation/FacturationAlertes.cs:44-49 : l'alerte du loyer ne revient que pour la prochaine échéance
- Problème : Le menu propose « À faire » et « Automatique » sur toute ligne, même une facture envoyée, impayée ou payée. Une fois le statut effacé, la facture n'est plus « émise » pour le code, car EstDejaEmise lit l'état. Elle sort des créances (DuesDe) et des rappels, et le bouton devient « Refaire ». « Refaire » passe par Preparer comme pour une facture neuve : nouveau numéro, séquence +1, et le numéro, le PDF et le compteur de corrections de la ligne sont remplacés. Pour le loyer et la régul, le fichier porte le même nom : le PDF de la facture réellement envoyée est écrasé, pas « orphelin » comme le dit la fiche, et la copie du cloud suit (DocumentsCloud.cs:82, « modifié ici : il l'emporte »). On obtient deux factures numérotées pour une période, et la première n'est plus suivie nulle part. Si elle était transmise à Super PDP, la plateforme la garde comme due, et la nouvelle part en rectificative sous un autre numéro. Sur une facture seulement préparée, le même geste annule l'envoi différé, mais la refaire prend aussi un numéro neuf : l'ancien, jamais envoyé, laisse un trou dans la numérotation. Nuance sur la fiche : l'alerte URGENT ne revient que si cette facture est la prochaine échéance.
- Scénario : Loyer d'octobre n° 2026/10003 (1 200 €), envoyé le 23/09, échéance le 08/10. Le 10/10, l'utilisatrice clique « Payé » par erreur, puis « Automatique » pour annuler. La ligne affiche « À faire », Créances ne réclame plus les 1 200 €, plus aucun rappel n'est possible, et le bouton dit « Refaire ». Elle clique « Refaire » puis « Sauvegarder et envoyer ». Le locataire reçoit « 2026/10004 — Loyer Octobre 2026 » alors qu'il a déjà 2026/10003. La séquence est à 5. Le fichier « Loyer-…-Loyer Octobre 2026.pdf » contient désormais 2026/10004 : l'original de 2026/10003 a disparu.
- Correction proposée : Une facture partie ne se dé-émet pas, et une facture préparée garde son numéro.
(1) Une petite aide `FacturationSuivi.Partie(loc, key)` = DejaTraite (statut Envoyé, Impayé ou Payé) + numéro + PDF. C'est le même test que « Quittance » (:401). Elle est lue par le menu et par SetStatut, pour qu'ils ne divergent pas.
(2) LocataireSuiviInline.OpenStatutMenu (:488-489) : n'ajouter « À faire » et « Automatique » que si `!FacturationSuivi.Partie(_loc, ligne.key)`.
(3) FacturationSuivi.SetStatut (:584) : un statut vide ou « AFaire » sur une facture partie déclenche un Debug.LogWarning et un retour, sans rien changer. C'est la garde commune à tous les appelants ; VersementsPanel (:117), le dépôt (:957) et EnregistrerRemboursement n'écrivent que « Payé » ou « Envoyé ».
(4) FactureEmission.Preparer (:61-71) : une ligne qui a encore un numéro et un PDF mais un statut vide ou « AFaire » (préparée puis remise en automatique) est traitée comme jamais partie : `bool remise = !dejaEmise && !string.IsNullOrEmpty(existante?.numero) && !string.IsNullOrEmpty(existante?.pdfPath); dejaEmise |= remise; bool jamaisPartie = dejaEmise && (existante.statut == "AttenteEnvoi" || remise);`. Elle reprend le même numéro et le même fichier, sans avancer la séquence (Enregistrer :150).
Pour défaire un « Payé » cliqué par erreur : « Envoyé », déjà dans le menu. Il annule aussi une « Encaissée » déjà déclarée (D5-12). Aucun nouveau champ.
- Décision posée : Dans le menu de la pastille d'une facture, « À faire » et « Automatique » effacent son état : si elle a déjà un numéro, la refaire en prend un deuxième. Que faire de ces deux choix sur une facture qui porte un numéro ?
  - (recommandée) 1. Les retirer pour une facture partie, les garder pour une facture préparée : Facture envoyée, impayée ou payée (y compris partielle ou à rembourser) : les deux choix disparaissent. Un « Payé » cliqué par erreur se défait par « Envoyé ». Une facture fausse se corrige (« Corriger » ou « Refaire ») ou s'annule par un avoir. Facture seulement préparée (en attente d'envoi) : « Automatique » reste, pour qu'elle ne parte pas, par exemple si le départ a été saisi après. Si la période est refaite, elle reprend son numéro et son fichier.
  - 2. Les retirer pour toute facture numérotée : Même chose, mais une facture préparée ne peut plus être annulée : elle se remplace par « Refaire », au même numéro. Inconvénient : un loyer préparé pour une période qui n'est plus due reste « à envoyer », est reproposé à chaque lancement, et empêche d'archiver le locataire.
  - 3. Les remplacer par « Annuler par un avoir… » : Sur une facture partie, le menu ouvre le formulaire de l'avoir qui l'annule, déjà rempli. Plus complet, mais plus long à faire (formulaires à adapter). Peut s'ajouter plus tard à l'option 1.
- Test : EditMode, Revue17Tests (ou FacturationSuiviTests et FactureEmissionTests) :
(1) D4_02_une_facture_partie_ne_redevient_pas_a_faire : ligne Loyer « Envoye », n° 2026/10003, pdfPath rempli. Après SetStatut(loc, l, "") puis SetStatut(loc, l, "AFaire"), le statut reste « Envoye » et DuesDe garde la facture. Preparer(loc, key, "2026/10004").NumeroFacture vaut « 2026/10003 corrigée(1) ».
(2) D4_02_une_facture_preparee_remise_en_automatique_garde_son_numero : ligne « AttenteEnvoi », n° 2026/11004, PDF. Après SetStatut(""), le statut est vide. Preparer(loc, key, "2026/11009") donne Reecriture vrai, Correction faux, NumeroFacture = 2026/11004. Après Enregistrer, factureSeq n'a pas bougé.
(3) D4_02_un_paye_force_sans_facture_revient_en_automatique : ligne « Paye » sans numéro ni PDF. SetStatut("") donne bien un statut vide.
En Play : ouvrir le menu d'une ligne envoyée (pas de « À faire » ni d'« Automatique ») et d'une ligne en attente (présents), sans rien envoyer.
- Interactions : D4-03 et BN17 : la garde empêche d'effacer un trop-perçu « À rembourser » ou un « Partiel » ; « Payé » reste refusé tant qu'il reste à rembourser (:439-445).
D5-12 : « Envoyé » reste le retour arrière et déclenche l'annulation d'« Encaissée ».
D5-01 et D5-02 : une facture transmise ne peut plus être dé-émise puis refaite en rectificative sous un autre numéro.
D4-04 : le refus de périodicité de Preparer s'applique aussi à une facture préparée remise en automatique.
D7a-01 et DepartLocataire.LoyerACorriger : un loyer préparé pour une période après le départ reste annulable (option 1) et repasse « Hors bail » sans bloquer l'archivage.
D4-01 : une facture remise puis refaite réécrit son destinataire.
Limite : une facture partie déjà remise en « Automatique » avant la correction (statut vide, numéro et PDF encore sur la ligne) serait refaite sous son numéro, sans « corrigée ». Cas rare, données d'avant seulement.
- **Vérification : à corriger**
  - Corrections : Problème et lieux confirmés dans le code actuel : LocataireSuiviInline.cs:352, 380-382, 436-456 et 486-489 ; FacturationSuivi.cs:398-412, 445-453, 584-604, 731-745 et 757-762 ; FactureEmission.cs:61-78 et 142-156 ; FactureLoyerPanel.cs:662-664 ; DocumentsCloud.cs:82. Noms de fichiers : sans numéro pour le loyer et la régul (FactureLoyerPanel.cs:477, FactureRegulPanel.cs:865), avec numéro pour la refacturation et le dépôt (FactureRefacPanel.cs:704, FactureDepotPanel.cs:862). Les lots 1, 2a et 2b n'y ont rien changé.

Ce qu'il faut corriger :

1) Citation : le statut vide ou « AFaire » est traité à FacturationSuivi.cs:290 et :293-295. Les lignes :273-275 concernent « AttenteEnvoi » sans PDF.

2) Nuance sur les alertes : seule l'alerte du loyer ne revient que pour la prochaine échéance (FacturationAlertes.cs:44-49). Celles de la régul et du dépôt reviennent bien (:80-85 et :95-101).

3) Impasse que l'analyse n'a pas vue : un loyer ENVOYÉ pour une période entièrement après un départ saisi plus tard.
- LoyerACorriger reste vrai tant que la ligne est émise (DepartLocataire.cs:164-174). Il ne regarde ni « Payé » ni un avoir.
- « Corriger » à 0 € est refusé (« total nul », FactureLoyerPanel.cs:520-524).
- « S'annule par un avoir » n'existe pas pour un loyer : l'avoir de la Refacturation porte sur des charges (FactureRefacPanel.cs:544-545).
Aujourd'hui, la seule sortie est « Automatique » : la ligne repasse « Hors bail » (FacturationSuivi.cs:104-110). Avec l'option 1 ou 2, l'archivage reste bloqué pour toujours (DepartLocataire.cs:131) et l'étape « Dernier loyer » reste à faire (:204). La correction doit traiter ce cas.

4) Défaire par « Envoyé » un « Payé » cliqué par erreur ne marche pas pour une facture seulement préparée. Partie (statut + n° + PDF) ne sait pas qu'elle n'est jamais partie, donc « Automatique » disparaît. « Envoyé » la retire de l'envoi différé (DoitPartir exige AttenteEnvoi, FactureEnvoiAuto.cs:49) et affiche un envoi qui n'a pas eu lieu.

5) (4) combiné à D4-04 : une facture préparée puis annulée par « Automatique » sous l'ancienne périodicité redevient « émise » pour Preparer. La période de même numéro dans la nouvelle périodicité est alors refusée (FactureEmission.cs:93-100), sans moyen de lever ce refus. Ne pas appliquer le refus D4-04 à une ligne « remise » : elle n'a jamais été envoyée.

Le reste tient :
- aucun autre appelant de SetStatut n'écrit « » ni « AFaire » : VersementsPanel.cs:117 (via StatutApresVersements), FactureDepotPanel.cs:957 et FacturationSuivi.cs:563 n'écrivent que « Payé » ou « Envoyé » ;
- aucun test existant ne casse : aucun n'appelle SetStatut("") ni Preparer sur une ligne numérotée sans statut ;
- pas de nouveau champ, rien côté serveur ;
- les tests proposés (1) à (3) sont justes.
  - Risques : - La règle « AttenteEnvoi = jamais partie », que (4) étend, est déjà fausse dans un cas. Une rectificative refusée par Super PDP après la bascule repasse « AttenteEnvoi » (FactureElectronique.cs:480), alors que la facture de base est bien partie. La refaire la « réécrit » sous le numéro de base, sans « corrigée », et remet corrections à 0 (FacturationSuivi.cs:412). C'est hors D4-02, à signaler à part.
- Une régul regroupée « remise » qui n'est pas la première liste cochée est vue comme neuve par Regroupable (FacturationSuivi.cs:621-632) : elle prend un numéro neuf. Cas rare avec l'option 1, car une régul n'est « en attente » qu'après un refus de la plateforme.
- Données d'avant : une facture partie déjà remise en « Automatique » serait refaite sous son numéro, sans « corrigée ». Cette limite est déjà notée dans l'analyse.
  - Décision manquante relevée : La question posée est la bonne, mais l'option 1 ferme deux cas sans le dire. Il faut les ajouter à la question.

1) Comment annuler une facture partie qui n'est plus due (départ saisi après l'envoi du loyer suivant), puisque « Automatique » disparaît ? Options :
- a. Faire l'option 3 (« Annuler par un avoir ») tout de suite, au moins pour ce cas.
- b. Garder « Automatique », avec un avertissement, sur un loyer que le départ rend entièrement non dû.
- c. Accepter une correction à 0 € qui annule le loyer. À valider avec l'expert-comptable : la plateforme refuse un total nul (BI8).

2) Comment défaire un « Payé » mis par erreur sur une facture préparée mais pas encore partie ? Options :
- a. Ajouter « En attente d'envoi » au menu d'une ligne numérotée : elle repartira à sa date.
- b. Laisser seulement « Envoyé » : elle ne partira plus, il faudra l'envoyer à la main.

### D5-04 · Un refus définitif d'« Encaissée » bloque les autres factures du locataire et le rattrapage des locataires suivants
- État au 05/11 : présent · effort S · serveur : aucun (facultatif : fonction edge Serveur/fonctions/facture-electronique.ts:202, « facture sans ventilation » rendue en refus passager ; redéploiement par l'utilisatrice) · nouveau champ : non
- Lieux :
  - Assets/Script/Facturation/FactureElectronique.cs:500-501 — « encaisser » refusé : `return` avec le refus, les lignes suivantes du locataire ne sont pas traitées
  - Assets/Script/Facturation/FactureElectronique.cs:511-512 — même chose pour l'annulation (212 négatif)
  - Assets/Script/Facturation/FactureElectronique.cs:453 — lignes prises dans l'ordre du suivi : la vieille facture passe avant la neuve
  - Assets/Script/Facturation/FactureElectronique.cs:474-486 — à comparer : un refus de TRANSMISSION est noté sur la ligne et le traitement continue
  - Assets/Script/Facturation/FactureElectronique.cs:426-431 — Demarrer : `break` sur tout refus sauf « origine », la tournée des locataires suivants s'arrête
  - Assets/Script/Facturation/FactureElectronique.cs:299-312 et :322-331 — Rattraper puis Panne : fenêtre « Super PDP a refusé une déclaration… Nouvel essai au prochain lancement »
  - Assets/Script/Facturation/FactureElectronique.cs:349-357 — TransmiseALinstant (retour BN9) : 5 minutes d'attente, gardées en mémoire seulement
  - Assets/Script/Facturation/FactureEnvoiAuto.cs:445-447 et :476 — après la bascule, pas d'e-mail : la facture neuve ne part que par Rattraper
  - Serveur/fonctions/facture-electronique.ts:45 — tout 4xx sauf 401, 403 et 429 = « refusee »
  - Serveur/fonctions/facture-electronique.ts:200-202 — GET de la facture en échec (même facture inconnue) = « indisponible » ; facture pas encore lue par Super PDP = « refusee » (« facture sans ventilation TVA »)
- Problème : Dans `Traiter`, un refus de Super PDP sur « Encaissée » ou sur son annulation fait `return` : les autres lignes du locataire attendent (y compris une facture neuve à transmettre) et `Demarrer` arrête la tournée de tous les locataires suivants. Rien n'est noté sur la ligne ; elle repasse en tête à chaque lancement ET à chaque action sur ce locataire (Payé, versement, remboursement, envoi : tous appellent `Rattraper`), avec la même fenêtre. Un refus de transmission, lui, est noté sur la ligne et n'arrête rien. Depuis la revue : `TransmiseALinstant` (BN9) évite le déclencheur le plus fréquent (« facture sans ventilation TVA » juste après la transmission d'une correction), mais seulement sur le même poste et dans la même session ; la garde `ARembourser` (BN17) retarde l'« Encaissée » d'un trop-perçu ou d'un avoir jusqu'au remboursement ; le blocage lui-même n'a pas changé. Le serveur appelle aussi « refus » un état passager (facture pas encore lue).
- Scénario : Après le 1/9/2027 : loyer T1 de Dupont « Payé » ; Super PDP refuse son « Encaissée » (par exemple un avoir déclaré en négatif, voir D5-11, ou une facture refusée entre-temps par le locataire). Le 15/10, le loyer T4 de Dupont part par la plateforme, sans PDF : `Rattraper` bute sur T1 et s'arrête, T4 n'est jamais transmise (pastille « À renvoyer »), le locataire n'a rien reçu et passe « Impayé » après le délai. Au lancement suivant, si Dupont est en tête, aucun autre locataire n'est rattrapé (factures à renvoyer, « Encaissée » des autres). Déjà possible aujourd'hui : facture corrigée sur le poste A, puis « Payé » sur le poste B (ou CIPL relancé) dans les minutes qui suivent → « facture sans ventilation TVA » → tout s'arrête pour la session, fenêtre d'erreur.
- Correction proposée : Option recommandée (ne plus bloquer, réessayer au lancement suivant), dans FactureElectronique.cs : 1) à côté de `_transmisLe` (349-357), un `HashSet<string>` statique des identifiants dont l'« Encaissée » a été refusé pendant la session, avec `NoterRefusEncaissement(id)` et `RefuseeCetteSession(id)` (testables comme `TransmiseALinstant`). 2) `Traiter`, branche « Encaissée » (494-504) et branche annulation (505-516) : sauter une ligne dont le `plateformeId` est dans l'ensemble ; si `rep.refus` vaut « refusee » : noter l'identifiant, garder un refus nommé pour le résumé (« « libellé » de locataire : détail », comme `RefusOrigine`) et `continue` au lieu de `return` ; facultatif : `r.plateformeErreur = rep.erreur` (champ existant, pour le diagnostic). Les pannes générales gardent le `return`. 3) Fonction pure `ArreteLaTournee(Reponse p)` : vraie si `p` n'est ni « origine » ni « refusee » ; `Demarrer` l'utilise aux lignes 429 et 431. 4) `Panne` (328-330) : nommer la facture et dire que les autres factures continuent, nouvel essai au prochain lancement. Appelants inchangés : `Rattraper` (FactureLoyerPanel.cs:706, FactureRegulPanel.cs:994, FactureRefacPanel.cs:834, LocataireSuiviInline.cs:451, VersementsPanel.cs:120 et :187, FactureEnvoiAuto.cs:476) et `Demarrer` (DemarrageCloud). Facultatif serveur : facture-electronique.ts:202 rendre un refus passager (par ex. « pas_lue ») que `Traiter` saute sans message. Si l'option « marquer la ligne » est choisie : `plateformeEtat` = « encaissement_refuse » (champ existant, pas de nouveau modèle), `EnAttente` l'ignore, `Pastille` affiche « Encaissée refusée », `FacturationSuivi.SetStatut` (584-604) le remet à « envoyee » pour relancer ; il faut alors d'abord classer « sans ventilation » en passager côté serveur, sinon une facture simplement pas encore lue resterait marquée.
- Décision posée : Quand Super PDP refuse « Encaissée » sur une facture (un refus de ses règles, pas une panne), que doit faire CIPL ?
  - (recommandée) Continuer, réessayer au prochain lancement : Les autres factures du locataire et les autres locataires continuent. La déclaration refusée n'est plus retentée pendant la session ; elle l'est à chaque lancement, avec le message au centre (motif de Super PDP) tant que le refus dure, comme décidé le 01/11 pour les refus. Une panne de Super PDP arrête toujours la tournée. C'est le plus petit changement, et un refus passager (facture pas encore lue) se répare tout seul.
  - Continuer et marquer la ligne « Encaissée refusée » : Message une seule fois ; la ligne porte « Encaissée refusée » dans le suivi ; plus d'essai tout seul : remettre « Payé » (ou corriger la facture) relance la déclaration. Moins de messages, mais un encaissement non déclaré peut être oublié, et il faut d'abord apprendre au serveur à ne pas appeler « refus » une facture simplement pas encore lue.
  - Ne rien changer : Tout s'arrête au premier refus : factures neuves du locataire non transmises, autres locataires non rattrapés, fenêtre à chaque lancement et à chaque action sur ce locataire.
- Test : EditMode, `Revue17Tests.D5_04_un_refus_d_encaissee_n_arrete_ni_le_locataire_ni_la_tournee` : `ArreteLaTournee` vrai pour « indisponible », « serveur », « jeton », « retire », « changee » et une réponse sans motif, faux pour « refusee » et « origine » ; `NoterRefusEncaissement` sur « 7 » puis `RefuseeCetteSession` vrai pour « 7 », faux pour « 8 » ; test texte sur FactureElectronique.cs : `Demarrer` appelle `ArreteLaTournee(` et `Traiter` appelle `NoterRefusEncaissement(` au lieu de rendre la main. Bac à sable : trouver un « Encaissée » que Super PDP refuse (montant supérieur au total ? avoir en négatif, D5-11 ?), le provoquer sur un locataire de test placé en tête, avec une facture neuve d'un autre locataire à transmettre → une seule fenêtre, l'autre facture part ; relancer CIPL → nouvel essai, même fenêtre.
- Interactions : D5-11 : un « Encaissée » négatif d'avoir, s'il est refusé, déclenche exactement ce blocage : vérifier D5-11 au bac à sable avant la bascule. D5-06 : l'annulation automatique proposée passe par `Traiter` ; son refus ne doit pas retenir la rectificative. D5-09 : un identifiant inconnu fait échouer le GET → « indisponible » → arrêt à chaque lancement (cas fermé par la décision D5-09 : sociétés de test écartées au passage en réel). BN9 (`TransmiseALinstant`) : réduit le déclencheur « sans ventilation », en mémoire seulement. BN17 : pas d'« Encaissée » avant « Remboursé… ». D5-16 : la facture retenue s'affiche « À renvoyer ». `TransfertCloud.Bloque` (TransfertCloud.cs:28-39) refuse le transfert du locataire tant qu'une ligne attend : normal. Décision du 01/11 (refus montré avec son détail, nouvel essai au prochain lancement) : gardée par l'option recommandée.
- **Vérification : à corriger**
  - Corrections : Le diagnostic est confirmé. Tous les lieux cités existent dans le code actuel : FactureElectronique.cs:453, 474-486, 494-504 (dont 500-501), 505-516 (dont 511-512), 426-431, 299-317, 322-331 et 349-357 ; FactureEnvoiAuto.cs:445-447 et :476 ; facture-electronique.ts:45 et :200-202. Les appelants de Rattraper sont exacts : FactureLoyerPanel:706, FactureRegulPanel:994, FactureRefacPanel:834, LocataireSuiviInline:451, VersementsPanel:120 et :187, FactureEnvoiAuto:476. Demarrer est appelé depuis DemarrageCloud.cs:117. Le blocage n'a pas changé : BN9 et BN17 n'ont fait que réduire ce qui le déclenche.

La correction recommandée est faisable, mais il faut la compléter :
1) Demarrer (FactureElectronique.cs:413, :426, :434) ne garde que la DERNIÈRE réponse non nulle (`panne = p; bloque = loc`). Si un refus n'arrête plus la tournée, le refus du locataire A est écrasé par celui de B, ou caché par une panne plus loin (seul « injoignable » s'affiche). Comme l'identifiant de A est alors dans l'ensemble de la session, ce refus n'est plus montré avant le prochain lancement. À faire : garder la liste des refus nommés et les afficher tous dans UNE fenêtre ; la panne qui arrête garde sa phrase.
2) Traiter ne rend qu'une seule Reponse (ligne 518) : un refusOrigine et un refus d'« Encaissée » du même locataire s'écrasent. Il faut les réunir.
3) Rattraper (lignes 300-303) ne lance la seconde passe que si `panne == null`, puis `panne = p` écrase le résultat. Avec un refus nommé : lancer la seconde passe si `!ArreteLaTournee(panne)` et écrire `panne = p ?? panne`, sinon le refus est perdu.
4) ArreteLaTournee(null) doit rendre faux ; le test doit le vérifier.
5) Tests :
- prendre un identifiant propre au test (par exemple « test-refus-1 », comme Revue17Tests.cs:582). L'ensemble est statique, et « 7 » est déjà l'identifiant utilisé par Revue16cTests.cs:501.
- si la condition des lignes 494-496 change, garder telles quelles les sous-chaînes testées par Revue17Tests.cs:586 et :619. `<= 0.005f)` doit rester en fin de condition.
6) Option serveur facultative (« pas_lue » à ts:202) :
- ajouter son cas dans Motif (FactureElectronique.cs:597-617) et dans Phrase (:335-344), sinon le message devient « La fonction … n'a pas pu répondre » ;
- l'ajouter à la liste de FactureElectroniqueTests.cs:281 ;
- ArreteLaTournee ne doit pas s'arrêter sur « pas_lue » ;
- déployer CIPL AVANT la fonction : un CIPL pas à jour prendrait « pas_lue » pour une panne et arrêterait la tournée.
7) Précisions sur le scénario :
- la tournée saute TOUS les locataires qui suivent Dupont (dans l'ordre bâtiments puis locataires), pas seulement « si Dupont est en tête » ;
- sur le poste B, seul le Rattraper en cours s'arrête, et il est repris à l'action suivante ; « tout s'arrête pour la session » ne vaut que pour la tournée du lancement ;
- un loyer T1 n'est pas un avoir : l'exemple D5-11 vise une autre ligne.
8) Bac à sable : il existe un déclencheur fiable. Corriger une facture payée au même montant, puis relancer CIPL dans les 5 minutes. Demarrer repart après 35 s (ligne 398) et obtient « facture sans ventilation TVA ».
  - Risques : Avec l'option 1, un refus qui dure (une règle de Super PDP non respectée) revient à chaque lancement, sans fin. Pendant ce temps, le locataire ne peut plus être transféré : TransfertCloud.cs:28-39 passe par EnAttente. Les seules sorties sont de changer le statut de la facture ou de la corriger.

Le message de FactureElectronique.cs:328 dit « Super PDP a refusé une déclaration ». Or « facture sans ventilation TVA » est produit par le serveur de CIPL (ts:202), pas par Super PDP.

Un identifiant inconnu, en revanche, donne « indisponible » (ts:201). C'est traité comme une panne, et cela arrête toujours la tournée ; le cas est fermé par la décision D5-09.

Une ligne sautée reste « en attente » toute la session : chaque action sur ce locataire relance Traiter, mais sans appel réseau pour cette ligne. C'est sans gravité.
  - Décision manquante relevée : La question posée couvre le choix principal. Trois compléments :
- ajouter à l'option 1 : « un refus qui ne se répare pas reviendra à chaque lancement et empêche le transfert du locataire » ;
- demander aussi l'option serveur : une facture « pas encore lue » serait réessayée sans fenêtre, au lieu d'afficher un refus ;
- faire valider un petit choix d'affichage : plusieurs refus dans une même tournée donnent une seule fenêtre qui les liste.

### D5-06 · Corriger une facture déjà déclarée « Encaissée » laisse l'encaissement de l'ancienne déclaré
- État au 05/11 : présent · effort M · serveur : aucun (l'action « encaisser » accepte déjà un montant négatif et une date : validé au bac à sable le 01/11, parcours BE5) · nouveau champ : oui
- Lieux :
  - Assets/Script/Facturation/FactureElectronique.cs:141-145 — Preparer : plateformeId vidé, encaisseeDeclaree remis à faux (encaisseeMontant et encaisseeDateISO gardés)
  - Assets/Script/Facturation/FactureElectronique.cs:151-159 — Preparer de la régularisation et de la refacturation, qui appelle le précédent
  - Assets/Script/Facturation/FactureElectronique.cs:233-244 — EnAttente : l'annulation (243) n'attend que si la ligne garde son identifiant et quitte « Payé »
  - Assets/Script/Facturation/FactureElectronique.cs:493-516 — Traiter : « Encaissée » de la nouvelle version (494-504), annulation par 212 négatif (505-516)
  - Assets/Script/Facturation/FactureElectronique.cs:373-385 — RecopierPlateforme : ne recopie ni encaisseeMontant ni encaisseeDateISO
  - Assets/Script/Facturation/FactureLoyerPanel.cs:142-147 — une facture « Payé » s'ouvre en correction
  - Assets/Script/Facturation/FactureLoyerPanel.cs:656-706 — Finaliser : Enregistrer (667), ApresCorrection (670), Preparer (693), Rattraper (706)
  - Assets/Script/Facturation/FactureRegulPanel.cs:957-994 (Preparer à 982) et Assets/Script/Facturation/FactureRefacPanel.cs:800-834 (Preparer à 822)
  - Assets/Script/Facturation/LocataireSuiviInline.cs:380-382 — « Refaire » sur une ligne « Payé » ouvre la correction
  - Assets/Script/Facturation/FacturationSuivi.cs:500-528 — ApresCorrection : payée puis corrigée → « Envoyé » (Partiel ou À rembourser) ; versement ajouté daté du jour (514)
  - Assets/Script/Facturation/FactureEmission.cs:59-112 — lieu du refus proposé par la revue
- Problème : Quand une correction part par la plateforme, `FactureElectronique.Preparer` efface l'identifiant de l'ancienne facture et le drapeau « Encaissée déclarée ». Le seul moyen d'annuler un encaissement (212 négatif, revue 16c D5-12) a besoin des deux : il ne part donc jamais. Depuis la correction de D5-01 (03/11), la rectificative a sa propre clé et son propre identifiant : l'ancienne garde son « Encaissée », la nouvelle reçoit le sien quand elle est payée (cinq minutes après sa transmission, BN9 ; après « Remboursé… » pour une baisse, BN17). Reproduit le 04/11 au bac à sable : juillet 2026 de sephora, 1 000 € sur l'originale et 880 € sur la rectificative. Aussi pour une correction de forme (même montant : la facture reste « Payé ») et à la hausse. Une annulation en attente (facture repassée « Envoyé » hors ligne) est aussi perdue si l'on corrige avant qu'elle parte. Ce que dit le dépôt : une correction transmise part en « facture rectificative (384), qui annule et remplace la précédente en citant son numéro et sa date » (FACTURATION_CIPL_PENNYLANE.md:623, décision du 31/10 ; commentaires FactureElectronique.cs:121-124, FactureEn16931.cs:86-88 et :213-216 ; D5-02 : un avoir 381, lui, ne remplace rien). Rien sur l'« Encaissée » de la facture remplacée : le statut porte « le numéro et la date de la facture » (reports/Ouverture sociétés clientes plateforme agréée.md:203) ; chez Super PDP « pas de machine à états », chaque facture garde sa liste d'événements (research_notes/Ouverture sociétés clientes plateforme agréée/super_pdp.md:103), et une facture déjà encaissée à son émission doit recevoir son 212 « juste après sa création » (même fichier, :105) : rien n'annule de lui-même le 212 de l'originale, ce que BN9 a constaté. Comment l'administration compte un encaissement sur une facture remplacée n'est écrit nulle part. L'obligation « Encaissée » ne vise que les factures émises à partir du 1/9/2027 (reports, :203 ; research_notes/…/regles_mandat_annuaire.md:185) : à corriger avant la bascule. FACTURATION_CIPL_PENNYLANE.md:627 et REVUE_CODE_CIPL.md:773 rangent D5-06 au thème 2c.
- Scénario : Loyer de juillet 2026, 1 000 € TTC, « Payé » le 03/07, « Encaissée » 1 000 € déclaré. Le 04/11, « Refaire » → loyer HT −100 € → 880 € TTC : la rectificative part (nouvel identifiant), la ligne est « À rembourser » 120 € ; « Remboursé… » 120 € → « Payé » → « Encaissée » 880 € sur la rectificative. Chez Super PDP : 1 000 € + 880 € = 1 880 € déclarés pour 880 € gardés. Correction de forme au même montant : 2 000 € pour 1 000 €. À la hausse (1 200 €) : 1 000 € + 1 200 € pour 1 200 €. Dates : si la facture était « Payé » à la main, ApresCorrection ajoute un versement daté du jour de la correction (FacturationSuivi.cs:514) : l'« Encaissée » de la rectificative tombe dans la période de la correction, celui de l'originale reste dans celle du paiement.
- Correction proposée : Recommandé : annulation automatique, avant la transmission de la rectificative. 1) FactureEtat.cs (après la ligne 55) : `public string encaisseeAAnnulerId;` (identifiant chez Super PDP de la version remplacée dont l'« Encaissée », décrit par encaisseeMontant et encaisseeDateISO, reste à annuler). 2) `FactureElectronique.Preparer(rec, json)`, avant la ligne 141 : si `rec.encaisseeDeclaree` et `rec.plateformeId` non vide, `rec.encaisseeAAnnulerId = rec.plateformeId` (encaisseeMontant et encaisseeDateISO ne sont déjà pas remis à zéro). C'est le seul point de passage des trois panneaux qui transmettent (loyer 693, régularisation 982 et refacturation 822 via 151-159) ; le dépôt ne passe pas par la plateforme ; `Preparer(rec, null)` (correction hors plateforme) ne change rien, l'ancienne version reste celle de la plateforme. 3) `EnAttente` (233-244) : ajouter « ou encaisseeAAnnulerId non vide ». 4) `Traiter`, en tête du corps de boucle (après 456), AVANT la transmission : appel « encaisser » sur `r.encaisseeAAnnulerId`, montant = opposé de `r.encaisseeMontant`, date = `r.encaisseeDateISO` (option 1) ou le jour (option 2), entreprise = `r.plateformeOrigineId` ; réussite → vider encaisseeAAnnulerId, encaisseeMontant, encaisseeDateISO, `annulees++` ; panne → `return` (la rectificative attend : même ordre que le parcours BE5 validé le 01/11, +1 000 puis −1 000, et Super PDP n'a jamais à annuler sur une facture déjà remplacée) ; refus → traité comme D5-04, la rectificative part quand même ; ne jamais envoyer 0 € (ligne déclarée avant le 01/11 sans encaisseeMontant). 5) `RecopierPlateforme` (380-383) : recopier aussi encaisseeAAnnulerId, encaisseeMontant et encaisseeDateISO (sinon une fiche relue pendant l'appel garde l'annulation et la renvoie une seconde fois). 6) `FacturationSuivi.CopieDe` (222-239) : le nouveau champ. 7) `DonneesCloud.Modele` 7 → 8 (commentaire « 8 : FactureEtat.encaisseeAAnnulerId (Revue 17, D5-06) ») et l'empreinte de `Revue17Tests.D2_01` (« 8:… »). 8) Résumé de `Rattraper` (310-311) : « (facture plus payée ou remplacée) ». 9) Lié, pour les dates : `ApresCorrection` (514) peut dater le versement ajouté de `porteuse.encaisseeDateISO` quand il existe. Écartés : le refus de la revue (voir la décision) ; un état « à annuler » sans nouveau champ, en gardant l'ancien identifiant dans plateformeId (Remplacee, Cle, Pastille et RefusCorrection lisent plateformeId : fragile) ; une annulation synchrone dans les trois panneaux (attente réseau à l'émission, et annulation faite pour une correction finalement abandonnée) ; déclarer seulement la différence (un « Encaissée » vaut pour UNE facture). Limite à noter en commentaire `ponytail:` : une seconde correction, hors plateforme, pendant que l'annulation attend, laisse l'originale sans encaissement.
- Décision posée : Quand on corrige une facture payée et déjà déclarée « Encaissée » chez Super PDP, que doit devenir l'encaissement déclaré sur l'ancienne facture ?
  - (recommandée) Annulé tout seul, à la date du paiement d'origine : CIPL garde l'identifiant de l'ancienne facture et, avant de transmettre la rectificative, y envoie l'annulation (« Encaissée » négatif du même montant), datée du jour de l'encaissement annulé : la période de TVA d'origine retombe juste. La rectificative reçoit son propre « Encaissée » quand elle est payée (tout de suite si elle reste « Payé », après « Remboursé… » pour une baisse). Rien de plus à faire pour vous. Un champ de plus sur la facture (modèle 8). À vérifier au bac à sable : Super PDP accepte un « Encaissée » négatif daté dans le passé.
  - Annulé tout seul, à la date du jour : Pareil, mais l'annulation est datée du jour de la correction, comme l'annulation actuelle quand une facture quitte « Payé ». Si la correction tombe dans une autre période de TVA, la période d'origine garde les deux encaissements, et celle de la correction un encaissement négatif.
  - Correction refusée tant que l'encaissement est déclaré (proposition de la revue) : Message au centre : repasser d'abord la facture « Envoyé » (CIPL annule l'encaissement), attendre l'annulation, corriger, puis ressaisir le paiement. Aucun nouveau champ, mais plusieurs gestes, Super PDP doit répondre avant de pouvoir corriger, et une facture « Payé » sans versement repassée « Envoyé » devient « Impayé » : corrigée, elle reste due en entier et le trop-perçu n'est plus calculé (BN17).
  - Attendre l'expert-comptable : Ne rien changer tant qu'on ne sait pas si une rectificative (384) efface d'elle-même l'encaissement de la facture qu'elle remplace ; en attendant, deux encaissements déclarés (1 000 € + 880 € pour juillet de sephora).
- Test : EditMode, `Revue17Tests.D5_06_corriger_une_facture_encaissee_annule_l_ancien_encaissement` : ligne « Paye », plateformeId « 1672792 », encaisseeDeclaree vrai, encaisseeMontant 1000, encaisseeDateISO « 2026-07-03 » → `Preparer(ligne, json de la rectificative)` → encaisseeAAnnulerId « 1672792 », plateformeId vide, encaisseeDeclaree faux, montant et date gardés, `EnAttente` vrai ; ligne jamais déclarée → encaisseeAAnnulerId vide ; `Preparer(ligne déclarée, null)` → rien ne change ; test texte : dans FactureElectronique.cs, l'appel sur `r.encaisseeAAnnulerId` vient avant `action = ` « envoyer », et `RecopierPlateforme` recopie le champ. `Revue16cTests.Suivi_la_copie_affichee_porte_tous_les_champs` veille sur CopieDe ; `D2_01` à mettre à « 8:… ». Bac à sable : (a) lire les événements (action « evenements » de la fonction) de l'originale et de la rectificative de juillet de sephora : 1 000 et 880 ? l'originale a-t-elle reçu un événement de remplacement ? (b) essayer un 212 de −1 000 sur cette originale déjà remplacée : accepté ou refusé (dit si l'ordre « annulation d'abord » est indispensable) ; (c) avec la correction : loyer payé et déclaré → « Refaire » au même montant, puis à la baisse → originale +1 000 puis −1 000, rectificative +880 après « Remboursé… » ; (d) un 212 négatif daté dans le passé est-il accepté ?
- Interactions : D5-01 (corrigé le 03/11) : avant, les deux « Encaissée » tombaient sur le même identifiant ; depuis, deux factures distinctes. D5-04 : à livrer avant ou avec, l'annulation ajoutée peut être refusée. D4-03 / BN17 : ApresCorrection rouvre la facture (Partiel, À rembourser) en gardant « Payé » et ses versements comme base : c'est ce qui écarte le refus de la revue. BN9 : la rectificative est déclarée cinq minutes après sa transmission ; l'annulation peut partir tout de suite. D5-02 : correction d'un avoir transmis ou vers un total négatif refusée : D5-06 ne touche que des factures positives. D5-07 : correction hors plateforme, l'ancienne version reste celle de la plateforme et le chemin d'annulation actuel suffit. D5-12 (mineur, payeLeISO) et le versement daté du jour par ApresCorrection : dates de l'« Encaissée » de la rectificative ; la décision du 01/11 (un seul « Encaissée », total TTC, date du dernier paiement) rend déjà les périodes approximatives pour un paiement en plusieurs fois ou un remboursement (question pour l'expert-comptable, avec D5-11). Transfert (BG7) : Preparer ne remet pas plateformeOrigineId, l'annulation de l'ancienne part bien au nom de l'origine, mais la rectificative émise dans la nouvelle entreprise déclarerait ensuite au nom de l'origine (point « À vérifier » de D5.md, à traiter à part). TransfertCloud.Bloque refuse le transfert tant que l'annulation attend (via EnAttente). D5-09 : pas de migration, les données actuelles sont des tests écartés au passage en réel. Modèle 8 : empreinte D2_01 et CopieDe.
- **Vérification : à corriger**
  - Corrections : Le diagnostic est confirmé. Les lieux sont exacts : FactureElectronique.cs:141-145, 151-159, 233-244, 373-385 et 493-516 ; FactureLoyerPanel.cs:142-147 et 656-706 ; FactureRegulPanel.cs:957-994 ; FactureRefacPanel.cs:800-834 ; LocataireSuiviInline.cs:380-382 ; FacturationSuivi.cs:500-528 ; FactureEmission.cs:59-112. Le défaut a été reproduit au bac à sable le 04/11 (REVUE_CODE_CIPL.md:773, FACTURATION_CIPL_PENNYLANE.md:627).

`Preparer(rec, json)` est bien le seul point de passage des trois panneaux. `avant` et `ligne` sont le même objet, car MarquerCorrige modifie la ligne sur place : à la ligne 141, plateformeId et encaisseeDeclaree sont donc encore les anciens. L'ancrage proposé fonctionne.

Points à corriger dans la proposition :
1) Conflit de champs. encaisseeMontant et encaisseeDateISO décrivent à la fois l'annulation en attente et le futur « Encaissée » de la rectificative. Avec la règle « refus : la rectificative part quand même » et le saut de session de D5-04 :
- la rectificative part ;
- son « Encaissée » (ligne 502) écrase le montant et la date ;
- au lancement suivant, l'annulation part sur l'ancienne facture avec le montant de la nouvelle (−880 au lieu de −1 000 pour juillet de sephora).
Correction : ajouter `&& string.IsNullOrEmpty(r.encaisseeAAnnulerId)` à la branche « Encaissée » (lignes 494-496), AVANT la clause ARembourser (pour garder les sous-chaînes testées à Revue17Tests.cs:586 et :619). La rectificative est transmise, mais son « Encaissée » attend l'annulation.
2) « Ne jamais envoyer 0 € » n'a pas de règle. Ce cas ne concerne pas seulement les lignes d'avant le 01/11 : aujourd'hui, RecopierPlateforme (380-383) ne recopie ni le montant ni la date. Une fiche relue pendant l'appel est donc enregistrée avec encaisseeDeclaree vrai et encaisseeMontant à 0. Proposition : dans Preparer, avant d'écraser plateformeDonnees (ligne 141), si encaisseeMontant ≤ 0, reprendre le total TTC de l'ancien JSON (`total_with_vat`, FactureEn16931.cs:140) ; une date vide prend la date du jour.
3) L'option 1 promet trop. « La période d'origine retombe juste » n'est pas acquis :
- les données sont attendues au plus tard 10 jours après la fin du mois (reports/Ouverture…:203) ;
- Super PDP reporte sur l'e-reporting suivant ce qui est déclaré après la clôture (super_pdp.md:105, version 1.37.0).
De plus, pour une facture marquée « Payé » à la main, sans versement, et corrigée au même montant, ApresCorrection n'ajoute rien (FacturationSuivi.cs:505). L'« Encaissée » de la rectificative prend alors la date du jour où il part (FactureElectronique.cs:270) : l'encaissement glisse quand même dans la période de la correction. Le point 9 ne couvre que le cas d'un montant changé (lien avec D5-12, payeLeISO). Il faut l'écrire dans l'option.
4) Ordre « annulation d'abord ». Si la rectificative est ensuite REFUSÉE (lignes 474-486), l'originale reste la facture valable chez Super PDP, mais sans encaissement, jusqu'à une nouvelle correction. L'ordre doit être tranché par l'essai (b) au bac à sable, pas fixé d'avance.
5) Le test proposé ne prouve pas la nouvelle clause d'EnAttente : juste après Preparer, EnAttente est déjà vrai à cause de la transmission à faire (lignes 239-240). Ajouter un cas avec plateformeEtat à « refusee » (ou « perimee ») et encaisseeAAnnulerId rempli : EnAttente doit être vrai.

Ce qui est déjà juste dans l'analyse :
- CopieDe (FacturationSuivi.cs:222-239), surveillé par Revue16cTests.cs:829 ;
- Modele 7 → 8 (DonneesCloud.cs:17) et l'empreinte « 7:1d9bc7661d3e » de Revue17Tests.cs:57 ;
- aucun SQL à changer : le serveur ne lit que key et statut dans facturesEtat (etape16c4_supabase.sql:48) ;
- les tests existants restent valides (Revue16cTests.cs:63, 81, 501, 772 ; FactureElectroniqueTests.cs:190-211) ;
- TransfertCloud.Bloque dira « attend sa transmission » (plateformeId vide), ce qui est juste.
  - Risques : Course déjà présente, aggravée par le nouveau champ. Si l'on corrige une ligne pendant qu'un « encaisser » est en cours sur elle (Rattraper d'un « Payé » cliqué juste avant) :
- au retour, la ligne 502 remet encaisseeDeclaree à vrai sur la nouvelle version, qui n'a pas d'identifiant ;
- encaisseeAAnnulerId n'a pas été posé, car le drapeau était encore faux au moment de Preparer ;
- l'ancien « Encaissée » ne s'annule jamais, et la rectificative ne se déclare jamais.
C'est rare (quelques secondes) ; à noter comme limite dans un commentaire `ponytail:`.

Point 9 : changer la date du versement ajouté se voit dans VersementsPanel.

Transfert (D5.md:171) : la rectificative d'une facture transférée part au nom de B, mais son « Encaissée » serait déclaré au nom de A, car plateformeOrigineId reste sur la ligne. À traiter avant ou avec ce défaut.

L'action « evenements » n'a aucun appelant dans CIPL, et Reponse n'a pas de champ pour elle. L'essai (a) se fait donc par script, en lisant le corps brut de la réponse, comme pour BE5.
  - Décision manquante relevée : Quatre choix sont tranchés sans question à l'utilisatrice :
(a) Si l'annulation de l'ancien encaissement est refusée : la rectificative part-elle quand même, et son « Encaissée » attend-il l'annulation ?
(b) Si l'ancien montant est inconnu : reprendre le total de l'ancienne facture, ou ne rien annuler et prévenir ?
(c) Point 9 : dater le versement ajouté par ApresCorrection du jour du paiement d'origine. C'est un changement visible, à demander.
(d) Avec l'option 1, l'annulation actuelle d'une facture qui quitte « Payé » (ligne 511) reste datée du jour. Cela fait deux règles de date différentes : il faut le dire, ou aligner les deux.

### D5-11 · « Encaissée » d'un avoir part en montant négatif, le signe qui veut dire « annulation »
- État au 05/11 : présent · effort S · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Facturation/FactureElectronique.cs:262-271 — Encaissement : montant = somme des lignes (négative pour un avoir) ; pour un avoir, date = dernier remboursement
  - Assets/Script/Facturation/FactureElectronique.cs:494-502 — déclaration envoyée avec ce montant signé (−297.60)
  - Assets/Script/Facturation/FactureElectronique.cs:509-511 — annulation : un encaisseeMontant négatif n'est pas retenu (test > 0), repli sur Encaissement, puis montant opposé : +297.60 pour un avoir
  - Serveur/fonctions/facture-electronique.ts:190-211 — montant réparti par taux avec son signe ; commentaire 191-193 : « un montant négatif annule un encaissement déclaré »
  - Assets/Script/Facturation/FactureEn16931.cs:78-81 et :101 — l'avoir part en type 381, montants positifs
  - Assets/Script/Facturation/FacturationSuivi.cs:533-541 et :553-564 — BN17 : avoir « À rembourser », « Payé » après « Remboursé… »
  - Assets/Script/Facturation/LocataireSuiviInline.cs:436-445 — « Payé » refusé tant qu'il reste à rembourser
- Problème : L'avoir est transmis en type 381 aux montants positifs, mais son « Encaissée » part à −297,60 €. Pour Super PDP, un « Encaissée » négatif est l'annulation d'un encaissement déclaré (décision du 31/10 : Sovos cas 34, fiche FNFE « Avoirs, factures négatives », REVUE_16C/SYNTHESE.md:208 ; validé au parcours BE5) ; sur un avoir qui n'a jamais eu d'encaissement, le sens est inconnu (accepté ? refusé, ce qui bloquerait tout via D5-04 ? compté comme une baisse ou une hausse de TVA ?). Et l'annulation d'un avoir repassé « Envoyé » part en positif. Ce que dit le dépôt : « l'avoir marqué « Payé » déclare « Encaissée » comme une facture » (FACTURATION_CIPL_PENNYLANE.md:623, 02/11), sans parler du signe ; le parcours BI8 qui devait le montrer n'est pas validé (PARCOURS_TEST_CIPL.md:1501) et BN19 ne dit rien de Super PDP ; Super PDP écrit qu'un fr:212 sans détails prend « le total de la facture » (research_notes/Ouverture sociétés clientes plateforme agréée/super_pdp.md:104), donc un montant positif pour un avoir transmis en positif (déduit). Depuis BN17 (04/11) : l'« Encaissée » d'un avoir ne part qu'après « Remboursé… » et prend la date du dernier remboursement ; le signe n'a pas changé.
- Scénario : Régularisation 2026 de Locataire BI2 transmise en avoir de 297,60 € TTC (381, montants positifs). Le 12/11, « Remboursé… » 297,60 € → « Payé » → « Encaissée » de −297,60 € daté du 12/11 sur l'avoir. Si l'avoir repasse « Envoyé » : annulation de +297,60 €. Si Super PDP refuse le négatif : D5-04, le locataire et la tournée sont bloqués à chaque lancement.
- Correction proposée : Option recommandée : `FactureElectronique.Encaissement` (262-271), garder le calcul de la date tel quel (il se sert du signe pour prendre les remboursements d'un avoir) et rendre la valeur absolue du montant : `return (Math.Abs(montant), date)`. L'annulation (509-511) relit alors un encaisseeMontant positif et part en négatif, comme pour une facture ; l'annulation automatique de D5-06 aussi. Seuls les avoirs changent : une facture 380 ou une rectificative 384 n'est jamais négative (D5-02). Appelants : `Traiter` (498 et 509) ; tests existants Revue16cTests.cs:507 et Revue17Tests.cs:607 (factures positives, inchangés). Serveur inchangé. Si l'option « aucun « Encaissée » pour un avoir » est choisie : l'exclure dans `EnAttente` et `Traiter` en lisant le type 381 dans `plateformeDonnees` (même test que `RefusCorrection`, ligne 204), sinon la ligne resterait « en attente » pour toujours et bloquerait son transfert.
- Décision posée : Un avoir transmis par Super PDP est remboursé au locataire (« Remboursé… », puis « Payé ») : quel montant CIPL doit-il déclarer en « Encaissée » ?
  - (recommandée) Le montant de l'avoir, en positif : 297,60 € pour un avoir de 297,60 € : le même signe que l'avoir transmis (montants positifs, type 381) et que ce que Super PDP prend lui-même quand on ne lui donne pas de montant (« le total de la facture »). Une annulation éventuelle part en négatif, comme pour une facture. À confirmer au bac à sable avant de coder, puis par l'expert-comptable.
  - Le montant en négatif (comme aujourd'hui) : −297,60 € : un remboursement vu comme un encaissement négatif. Mais chez Super PDP le négatif veut dire « annulation d'un encaissement » (décision du 31/10), jamais déclaré sur cet avoir ; et l'annulation d'un avoir part alors en positif.
  - Aucun « Encaissée » pour un avoir : La ligne passe « Payé » sans déclaration ; la baisse de TVA due au remboursement est faite à la main par l'expert-comptable. Pas de risque de mauvais signe, mais la déclaration pré-remplie ne voit pas le remboursement.
- Test : EditMode, `Revue17Tests.D5_11_encaissee_d_un_avoir_en_positif` : avoir « Paye » de −297,60 avec un remboursement de −297,60 le 2026-11-12 → `Encaissement` = (297,60 ; « 2026-11-12 ») ; facture de 880 réglée 1 000 le 2026-07-03 puis remboursée de 120 → (880 ; « 2026-07-03 »), inchangé (BN17). Bac à sable, AVANT de coder : si l'avoir de BN19 ou la régularisation corrigée(4) de BI8 a été transmis puis remboursé, lire ses événements (action « evenements ») : le −297,60 a-t-il été accepté ? Sinon rejouer BI8/BN19 sur Locataire BI2 (SIRET de test) avec le code actuel : accepté ou refusé, et ce que montre l'écran de Super PDP. Puis, avec le changement : +297,60 accepté, et « Envoyé » → −297,60 accepté. Facultatif : un fr:212 sans détails sur l'avoir, pour voir le montant et le signe que Super PDP retient lui-même. Ce que l'administration en fait ne se voit pas au bac à sable (l'étiquette reste « Encaissée » même après une annulation, BE5) : question à l'expert-comptable, déjà inscrite dans SYNTHESE.md:17.
- Interactions : D5-04 : un refus du négatif bloquerait le locataire et la tournée : raison de tester D5-11 tôt. D5-06 : l'annulation automatique relit encaisseeMontant, toujours positif avec la correction ; D5-02 refuse déjà la correction d'un avoir transmis, donc pas de cas D5-06 sur un avoir. BN17 / BN19 : l'avoir passe « Payé » seulement par « Remboursé… » ; l'« Encaissée » est daté du dernier remboursement. Même question pour le trop-perçu rendu (BN17) : aujourd'hui déclaré en net (880 € à la date du paiement) au lieu de +1 000 au paiement et −120 au remboursement ; à poser dans la même question à l'expert-comptable. L'« Encaissée » n'est obligatoire que pour les factures émises à partir du 1/9/2027 : le choix doit être fait et validé avant la bascule.
- **Vérification : confirmé**
  - Corrections : Les lieux sont exacts dans le code actuel :
- FactureElectronique.cs:262-271 : la somme est signée ; pour un avoir, la date est celle du dernier versement, quel que soit son signe (ligne 267) ;
- :494-502 : « -297.60 » est envoyé ;
- :509-511 : un encaisseeMontant négatif n'est pas retenu, le code se replie sur Encaissement, et « 297.60 » est envoyé ;
- facture-electronique.ts:190-211 : commentaire aux lignes 191-193, répartition signée aux lignes 203-210 ;
- FactureEn16931.cs:78-81 et :101 : l'avoir part en 381, aux montants positifs ;
- FacturationSuivi.cs:533-541 et :553-565 ; LocataireSuiviInline.cs:436-445.

Les documents cités sont exacts :
- FACTURATION_CIPL_PENNYLANE.md:623 ne dit rien du signe ;
- BI8 n'est pas validé (PARCOURS_TEST_CIPL.md:1501) ;
- BN19 (:1567) ne dit rien de Super PDP ;
- research_notes/…/super_pdp.md:104 et REVUE_16C/SYNTHESE.md:208 disent bien ce que l'analyse leur fait dire.

La correction suffit : `Math.Abs` dans le seul return de Encaissement. Il n'y a que deux appelants (lignes 498 et 509). Les tests Revue16cTests.cs:507 (1 200) et Revue17Tests.cs:607 (880) portent sur des montants positifs et ne changent pas, et aucun test n'attend un montant négatif. Ni le serveur ni le modèle ne changent. Seul ajustement à faire dans l'analyse : « seuls les avoirs changent » n'est vrai que presque (voir les risques).
  - Risques : (1) RefusCorrection ne s'applique que si la correction part par la plateforme (FactureLoyerPanel.cs:527, FactureRegulPanel.cs:854, FactureRefacPanel.cs:692). Une facture 380 transmise, puis corrigée HORS plateforme à un total négatif, verrait donc aussi le signe de son « Encaissée » changer. C'est faux avant comme après, puisque la plateforme garde la 380 d'origine ; cela relève de D5-07.
(2) Les avoirs déjà déclarés en négatif au bac à sable seraient « annulés » dans le mauvais sens après le changement (repli de la ligne 509 : on renverrait −297,60 au lieu de +297,60). Sans effet après le passage en réel (D5-09).
(3) Si Super PDP refuse tout 212 sur un 381, quel que soit le signe, l'option « positif » bloque elle aussi le locataire par D5-04. Il faut donc faire l'essai au bac à sable avant de coder, comme prévu.
  - Décision manquante relevée : La question est bien posée. Elle est déjà inscrite pour l'expert-comptable (REVUE_17/SYNTHESE.md:17). Il faut y joindre explicitement la question voisine du trop-perçu, qui n'est inscrite nulle part. Aujourd'hui, il est déclaré en net : 880 € à la date du paiement (Revue17Tests.cs:607-609), au lieu de +1 000 € au paiement puis −120 € au remboursement.

### D5-05 · Après le 1/9/2027, une facture préparée sans facture électronique part quand même par e-mail seul
- État au 05/11 : présent · effort M · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Facturation/FactureEnvoiAuto.cs:40-66 (DoitPartir : rien sur la plateforme ; seule une ligne « refusee » est écartée, l.50-51)
  - Assets/Script/Facturation/FactureEnvoiAuto.cs:442-460 (EnvoyerUne : `plateforme = !IsNullOrEmpty(plateformeDonnees)` l.445 ; sans JSON, e-mail de facture avec le PDF ; une ligne « perimee » est prise pour une facture de la plateforme)
  - Assets/Script/Facturation/FactureEnvoiAuto.cs:162-197 et :203-235 (ProposerAuDemarrage : le diagnostic ne va qu'au journal, l.188 ; Diagnostic : le dernier else, l.227-228, suppose « refusée par la plateforme »)
  - Assets/Script/Facturation/FactureLoyerPanel.cs:491-510 (premiers pas, puis envoi différé décidé avant tout contrôle : l.505-510 passent avant le refus du total nul l.520-524 et de l'avoir sans référence l.530-536)
  - Assets/Script/Facturation/FactureLoyerPanel.cs:516 et :555-595 (hors plateforme : envoi par e-mail, même après la bascule) ; :687-703 (Finaliser : Concerne faux → Preparer(ligne, null), la ligne n'a pas de JSON)
  - Assets/Script/Facturation/FactureRegulPanel.cs:849-894 et FactureRefacPanel.cs:680-733 (envoi immédiat par e-mail hors plateforme, même après la bascule)
  - Assets/Script/Facturation/FactureElectronique.cs:88-90 (Concerne exige Active), :114-119 (Bascule, NotificationSeule), :125-146 (Preparer(null) : « perimee », JSON gardé), :218-227 (LibelleEnvoi : « e-mail seulement … relancez CIPL »), :402-406 (EtatInconnu après 35 s et un second essai ; toast « partiront par e-mail seul »)
  - Assets/Script/Facturation/FacturationAlertes.cs:48-54 (seul signe visible d'une ligne restée en attente : « URGENT — Loyer … à envoyer avant le … », sans la raison)
- Problème : La décision « plateforme ou e-mail » n'est prise qu'une fois, à l'émission, par Concerne, qui exige que Super PDP soit actif à cet instant. L'envoi différé (FactureEnvoiAuto) ne revérifie rien : une ligne sans facture électronique part avec l'e-mail de facture et le PDF, même après le 1/9/2027, alors que l'émission électronique est obligatoire pour une facture avec TVA à un locataire qui a un SIREN. Rien ne la rattrape ni ne la signale (le diagnostic n'est écrit que dans le journal). Les formulaires, eux aussi, envoient par e-mail seul après la bascule quand Super PDP n'est pas actif (état non lu, vérification en cours, SCI non reliée). Trouvé en relisant le même chemin : (1) une ligne refaite hors plateforme garde son ancien JSON marqué « perimee » ; EnvoyerUne la croit « par la plateforme » : avant la bascule elle part avec l'e-mail « copie » qui annonce une transmission qui n'aura pas lieu ; après la bascule, ni PDF ni transmission (EnAttente écarte « perimee »), et la ligne passe pourtant « Envoyé ». (2) Dans le loyer, la branche de l'envoi différé (l.505-510) passe avant le refus du total nul et de l'avoir sans référence : un loyer à 0 € ou un avoir sans facture à citer, préparé à l'avance, est enregistré et partira.
- Scénario : 20/09/2027 : au lancement, le serveur de CIPL ne répond pas deux fois ; le toast dit « Super PDP : état non lu. Les loyers émis d'ici le prochain lancement partiront par e-mail seul. » Le loyer de novembre de Dupont (bail soumis, SIRET) est préparé ce jour-là pour l'envoi du 17/10 : pas de facture électronique sur la ligne. Le 17/10, « Tout envoyer » : PDF et e-mail de facture, aucune facture électronique, aucun message (facture non conforme, amende de 15 €). Variante : le même loyer préparé le 01/09 (Super PDP actif, JSON sur la ligne) puis refait (« Refaire ») le 20/09 : l'ancien JSON devient « perimee » ; le 17/10, CIPL n'envoie pas le PDF (notification attendue), ne transmet rien et marque « Envoyé » : Dupont ne reçoit rien et passe « Impayé » quinze jours après l'échéance.
- Correction proposée : Une règle pure, deux gardes, un ordre corrigé. 1) FactureElectronique, à côté de Concerne : `DoitPartirParPlateforme(float tva, string siretAcheteur, DateTime depart)` = (EssaiApresBascule ou depart ≥ Bascule) et TVA non nulle et SIREN à 9 chiffres (la règle de Concerne sans l'état de la SCI) ; `PartParPlateforme(FactureEtat r)` = JSON présent et état ni « perimee » ni « refusee » ; `ARefaire(Locataire loc, FactureEtat r, DateTime jour)` = type ≠ « Depot », pas PartParPlateforme, et DoitPartirParPlateforme(r.tauxTva, SIRET de la ligne, jour). La TVA est celle de la ligne (tauxTva, taux à l'émission), pas le bail d'aujourd'hui ; le SIRET est relu comme FactureEnvoiAuto.Destinataire : titulaire de la période (Cessions.TitulaireLigne, avec la règle de FactureLoyerPanel.SiretPourPlateforme), sinon destSiret du type, sinon la fiche. 2) FactureEnvoiAuto : DoitPartir, après le test « refusee » (l.51) : `if (FactureElectronique.ARefaire(loc, rec, aujourdhui)) return false;` ; EnvoyerUne l.445 : `bool plateforme = FactureElectronique.PartParPlateforme(c.rec);` (une ligne « perimee » part alors avec l'e-mail de facture et le PDF) ; Diagnostic : la raison « à refaire (Refaire) quand Super PDP est actif : depuis le 1/9/2027 elle doit partir par la plateforme » avant le dernier else ; ProposerAuDemarrage : les lignes retenues sont nommées dans un ConfirmDialog.Erreur (le journal ne se voit pas). 3) Formulaires, avant tout envoi : Loyer, SauvegarderEtEnvoyer : après la branche premiers pas / bâtiment d'exemple (l.497), calculer parPlateforme (remonté de la l.516), puis faire passer AVANT l'envoi différé (l.505) le refus du total nul (l.520), celui de l'avoir sans référence (l.530) et le nouveau : `if (!parPlateforme && FactureElectronique.DoitPartirParPlateforme(d.tva, FactureElectronique.SiretAcheteur(_loc, SiretSaisi()), differe ? dateEnvoi.Value : DateTime.Today))` → ConfirmDialog.Erreur, rien n'est émis (la date de départ compte : un loyer préparé le 25/08/2027 pour un envoi le 16/09 est déjà concerné). Régularisation (Emettre, vers l.853) et Refacturation (vers l.691) : même test avec DateTime.Today. Les premiers pas et le bâtiment d'exemple restent libres (DoitPartir retient la ligne ensuite). 4) Textes après la bascule : LibelleEnvoi (l.218-227) et le toast de la l.405 disent que la facture ne pourra pas partir tant que Super PDP n'est pas actif, au lieu de « e-mail seul » (garder « : EtatInconnu ? », testé par Revue16cTests.D1_06). Sans nouveau champ.
- Décision posée : À partir du 1er septembre 2027, une facture avec TVA à un locataire qui a un SIRET doit partir par Super PDP. Si Super PDP n'est pas actif pour la SCI au moment de l'émettre ou de l'envoyer (état pas lu au lancement, vérification du gérant en cours, SCI pas reliée), que doit faire CIPL ?
  - (recommandée) Refuser l'e-mail seul : Une telle facture ne part jamais par e-mail seul. Au formulaire : message au centre, rien n'est émis, avec la raison (relancer CIPL ; vérification du gérant en cours ; relier la SCI dans Réglages → Intégrations). Une facture déjà préparée sans facture électronique n'est pas envoyée au jour dit : elle est nommée au lancement, à refaire (« Refaire ») quand Super PDP est actif.
  - Refuser seulement si la SCI est reliée : Même refus quand l'état n'est pas lu ou que la vérification est en cours ; une SCI jamais reliée à Super PDP continue d'envoyer par e-mail seul, avec l'avertissement de la ligne « Envoi » (facture non conforme, à ses risques).
  - Demander confirmation : Au formulaire et dans l'envoi automatique : « Cette facture devrait partir par la plateforme. L'envoyer quand même par e-mail seul (non conforme depuis le 1/9/2027) ? » ; Oui : elle part comme aujourd'hui.
- Test : EditMode, EnvoiAutoTests.Apres_la_bascule_une_facture_sans_facture_electronique_ne_part_pas_par_e_mail : locataire tvaLoyer = "oui", siretNumber = "55203253400646", e-mail ; ligne Loyer « AttenteEnvoi », PDF, tauxTva = 20, échéance = le jour du test, sans JSON : DoitPartir(…, new DateTime(2027, 9, 16), …) faux ; la même au 16/08/2027 (échéance du 16/08) vrai ; avec JSON vrai ; JSON et plateformeEtat = "perimee" faux ; type « Depot » vrai ; tauxTva = 0 vrai ; locataire sans SIRET vrai (dates fixes : le test ne vieillit pas). Revue17Tests.D5_05_… : DoitPartirParPlateforme(240f, "55203253400646", 01/09/2027) vrai, au 31/08/2027 faux, TVA 0 faux, SIRET vide faux ; PartParPlateforme faux pour « perimee » ; tests texte : les trois formulaires appellent DoitPartirParPlateforme( ; FactureEnvoiAuto contient PartParPlateforme(c.rec) ; dans FactureLoyerPanel, « if (Math.Abs(d.ttc) <= 0.004f) » vient avant « PreparerPourEnvoi(d, key ». Parcours ensuite avec EssaiApresBascule réglé par script, e-mails de test vers agathelimouzy@gmail.com.
- Interactions : D5-07 (même endroit des formulaires : après la bascule, une correction hors plateforme tombe d'abord sur cette garde) ; D5-10 (choix de l'e-mail dans EnvoyerUne et les formulaires : PartParPlateforme rend la « copie » juste) ; revue 16c D5-05 (« refusee » déjà écartée par DoitPartir, la nouvelle règle se place à côté) ; D5-04 (une ligne partie sans PDF après la bascule peut rester bloquée derrière un « Encaissée » refusé) ; D4-01 et D7a-03 (la ligne ne garde pas son acheteur : SIRET relu comme Destinataire) ; D8-02 (le message du lancement ne doit pas être effacé par la confirmation « Tout envoyer ») ; tests texte existants à garder : « Active && Tva.Soumis(loc) » (TvaTests), « : EtatInconnu ? » et « Math.Abs(tva) > 0.005f » (Revue16cTests) ; DemarrageCloud.cs:115-117 ouvre la fenêtre d'envoi avant la lecture de l'état (un clic dans la seconde enverrait une « copie » après la bascule ; ARefaire ne dépend pas de l'état, il n'est pas touché).
- **Vérification : à corriger**
  - Corrections : Lieux et problème vérifiés dans le code actuel. Les lots 1, 2a et 2b n'ont rien corrigé ici.
- FactureEnvoiAuto.cs:40-66 : DoitPartir ne regarde pas la plateforme.
- FactureEnvoiAuto.cs:445 : `plateforme` = « JSON présent », donc une ligne « perimee » compte comme une ligne plateforme ; envoi aux l.447-458.
- FactureElectronique.cs:88-90 (Concerne exige Active) et :132-139 (Preparer(null) marque « perimee » et garde le JSON).
- FactureElectronique.cs:239-240 : EnAttente écarte « perimee ». FactureElectronique.cs:284 : pas de pastille pour elle.
- FactureLoyerPanel.cs:505-510 (envoi différé) passe avant :520-524 (total nul) et :530-536 (avoir sans référence).
Les deux trouvailles du collègue sont exactes. Une ligne « perimee » part avant la bascule avec l'e-mail « copie » et rien n'est transmis ; après la bascule, ni e-mail ni transmission, et la ligne passe « Envoyé ». Un loyer à 0 € ou un avoir sans référence préparé à l'avance est enregistré.
À reprendre dans la correction :
1) DoitPartirParPlateforme n'est pas « la règle de Concerne sans l'état de la SCI » : il lui manque Tva.Soumis(loc) (FactureElectronique.cs:89). Aujourd'hui cela revient au même, car TauxFacture rend 0 pour un bail exonéré. Avec D5-08, ce n'est plus vrai : la correction taxée d'un bail devenu exonéré serait refusée avec « Super PDP pas actif », même quand il l'est. Pour la garde des formulaires, prendre Tva.Soumis(loc) && SIREN && TVA ≠ 0 && date : c'est ce que dit la fiche (D5.md:44). Garder le taux de la ligne seulement pour ARefaire.
2) Tests existants oubliés :
- Revue16cTests.cs:437 exige le texte « if (!correction && dateEnvoi.HasValue ». Une variable `differe` le casse : garder ce texte ou mettre le test à jour.
- Revue16cTests.cs:216-217 : « if (Math.Abs(d.ttc) <= 0.004f) » doit rester, « parPlateforme && Math.Abs(d.ttc) » doit rester absent. Déplacer ces lignes telles quelles.
3) ProposerAuDemarrage sort avant tout calcul en lecture seule, sans e-mail réglé et après le « Plus tard » du jour (FactureEnvoiAuto.cs:164-179). Si le message doit toujours paraître, calculer et nommer les lignes retenues avant ces retours.
4) LibelleEnvoi : la phrase « après la bascule » doit tester NotificationSeule dans la fonction, car régul et refac l'appellent sans le paramètre (FactureRegulPanel.cs:360, FactureRefacPanel.cs:358). Elle doit aussi couvrir la dernière branche « Mode global : Email direct (SMTP). » (l.227), qui est celle d'un état jamais lu.
5) Garde peu coûteuse : EnvoyerUne repart de la ligne relue (l.440, Retrouver), et la file Attente (l.486-530) ne repasse pas par DoitPartir. Refaire le test ARefaire après Retrouver.
Pas de nouveau champ ni rien côté serveur : exact.
  - Risques : - Reprise en ligne après un démarrage hors ligne : DemarrageCloud.ReprendreEnLigne (DemarrageCloud.cs:251-260) ne lit jamais l'état de Super PDP, et Demarrer sortait déjà hors ligne (FactureElectronique.cs:394). Active et EtatInconnu restent faux toute la session : les loyers partent par e-mail seul sans explication. Avec l'option 1, ils seront refusés après la bascule : le message doit nommer ce cas, ou ReprendreEnLigne doit relire l'état.
- Lignes qui ONT leur facture électronique : FactureEnvoiAuto.cs:447 envoie la « copie » avec le PDF dès que Active est faux. Or la fenêtre d'envoi s'ouvre avant la lecture de l'état (DemarrageCloud.cs:115, puis 117). Après la bascule, un « Tout envoyer » rapide ou un état non lu fait partir le PDF, contre la décision des 30-31/10. La proposition laisse ce cas tel quel.
- Décompte de sortie (type « Depot ») : il peut porter de la TVA sur les retenues (FactureDepotPanel.cs:754-758) et ne passe jamais par la plateforme. ARefaire l'exclut : il restera « e-mail seul » après la bascule. À suivre à part.
- Un loyer à 0 € préparé avant le correctif sera retenu par ARefaire, puis refusé au « Refaire » (total nul). Il sera nommé à chaque lancement sans issue simple.
- La garde du formulaire juge la date d'envoi, la ligne « Envoi » juge la date du jour. En août 2027, pour un loyer qui part en septembre, la ligne annoncera encore l'e-mail alors que l'émission est refusée.
  - Décision manquante relevée : La question et les options sont justes, et l'option 1 suit l'obligation. Trois points sont à ajouter :
1) Une facture qui A sa facture électronique mais part pendant que Super PDP n'est pas actif (état pas encore lu à l'ouverture de la fenêtre, état non lu, reprise après hors ligne). Aujourd'hui : « copie » avec PDF, puis transmission plus tard (FactureEnvoiAuto.cs:444-447). Avec l'option 1, faut-il la retenir (« sautée, elle repartira ») ou garder ce comportement ?
2) Dire dans l'option 1 qu'une SCI qui émet par une autre plateforme agréée que Super PDP ne pourra plus envoyer ces factures depuis CIPL.
3) Le message du lancement revient-il à chaque lancement, même après « Plus tard » ?

### D5-07 · Correction hors plateforme d'une régularisation ou d'une refacturation transmise : rien ne prévient (le loyer prévient, mais après l'envoi)
- État au 05/11 : présent · effort S · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Facturation/FactureElectronique.cs:151-159 (Preparer(ligne, avant, parPlateforme, e) : hors plateforme, Preparer(ligne, null) sans un mot)
  - Assets/Script/Facturation/FactureElectronique.cs:132-140 (une ligne transmise ne bouge pas : elle garde plateformeId) et :494-502 (« Encaissée » : montant de la ligne corrigée, identifiant de l'ancienne facture)
  - Assets/Script/Facturation/FactureRegulPanel.cs:957 et :982-991 (Finaliser) ; FactureRefacPanel.cs:800 et :822-831
  - Assets/Script/Facturation/FactureRegulPanel.cs:853-854 et FactureRefacPanel.cs:691-692 (RefusCorrection n'est lu que si Concerne)
  - Assets/Script/Facturation/FactureElectronique.cs:202-208 (RefusCorrection, D5-02)
  - Assets/Script/Facturation/FactureLoyerPanel.cs:526-527 (RefusCorrection seulement si parPlateforme) ; :662-664 et :687-691 (le loyer prévient au centre, mais dans Finaliser : l'e-mail est déjà parti)
- Problème : Quand la correction d'une facture déjà transmise à Super PDP ne peut pas partir par la plateforme (état non lu au lancement, vérification en cours, SCI déliée, SIRET retiré du formulaire, bail devenu exonéré), la régularisation et la refacturation partent par e-mail sans rien dire : la plateforme garde l'ancienne version, le locataire reçoit la nouvelle en PDF, et la ligne garde l'identifiant de l'ancienne, si bien que « Encaissée » déclarera ensuite le nouveau montant sur l'ancienne facture. Le loyer prévient, mais une fois l'e-mail parti (le message est dans Finaliser) : l'écart est déjà créé, et « Refaites Corriger » fabrique une deuxième correction.
- Scénario : Refacturation « Taxe foncière 2026 » de Dupont transmise (1 500 € HT, 1 800 € TTC). Un matin, l'état de Super PDP n'est pas lu ; « Refaire » à 1 350 € HT : l'e-mail part avec le PDF « corrigée(1) », aucun message. Super PDP garde 1 500 € HT ; au paiement, « Encaissée » déclare 1 620 € sur la facture de 1 800 €. Le même geste sur un loyer affiche « … cette correction n'y part pas … » après l'envoi.
- Correction proposée : Option A (recommandée) : refuser avant tout envoi, dans la règle déjà partagée par les trois formulaires. `RefusCorrection(FactureEtat avant, float ttc, string sujet, bool parPlateforme = true)` (FactureElectronique.cs:202) : ligne transmise (plateformeId) et !parPlateforme → « « {sujet} » a été transmise à Super PDP : sa correction doit y partir aussi, et elle ne le peut pas en ce moment (Super PDP pas actif pour la SCI : état non lu au lancement, vérification en cours ; ou locataire plus concerné : SIRET, bail exonéré). Rien n'a été émis. Recommencez quand la ligne « Envoi » annonce la plateforme. » ; sinon le refus D5-02 inchangé. Appelants : Loyer l.526-527, Régularisation l.853-854, Refacturation l.691-692 : passer parPlateforme (calculé une fois : la régul et la refac appellent Concerne deux fois) et tester `refusCorrection != null` seul. Supprimer le message d'après coup du loyer (l.688-691) et `dejaTransmise` (l.664). La valeur par défaut laisse Revue17Tests.D5_02 valable. Option B (celle de la fiche) : texte du loyer déplacé dans FactureElectronique.CorrectionHorsPlateforme(sujet), affiché (ConfirmDialog.Erreur) par Preparer(ligne, avant, parPlateforme, e) quand !parPlateforme et avant.plateformeId ; le loyer l'appelle aussi.
- Décision posée : On corrige une régularisation, une refacturation ou un loyer déjà transmis à Super PDP, mais la correction ne peut pas partir par la plateforme en ce moment (état de Super PDP non lu, vérification en cours, SIRET retiré, bail devenu exonéré). Que doit faire CIPL ?
  - (recommandée) Refuser avant l'envoi : Les trois formulaires, loyer compris, refusent au centre : rien n'est émis ni envoyé, la plateforme et le locataire gardent la même version. On recommence quand la ligne « Envoi » annonce Super PDP.
  - Prévenir après l'envoi : Comme le loyer aujourd'hui : la correction part par e-mail, puis un message au centre dit que la plateforme garde l'ancienne version et qu'il faudra refaire la correction. D'ici là, « Encaissée » déclarera le nouveau montant sur l'ancienne facture.
  - Demander confirmation : Avant l'envoi : « La plateforme gardera l'ancienne version. Envoyer quand même cette correction par e-mail ? » ; Oui : comme l'option précédente.
- Test : EditMode, Revue17Tests.D5_07_une_correction_transmise_ne_part_pas_hors_plateforme : ligne { plateformeId = "pdp-1", plateformeDonnees = JSON d'une facture 380 } : RefusCorrection(l, 1620f, "Refacturation", parPlateforme: false) contient « Rien n'a été émis » ; parPlateforme: true → null ; ligne jamais transmise, hors plateforme → null ; tests texte : aucun des trois formulaires ne contient plus « && refusCorrection != null », et FactureLoyerPanel ne contient plus « cette correction n'y part pas ». (Option B : fonction pure PrevenirHorsPlateforme(avant, parPlateforme), comme la fiche.) Parcours : état de Super PDP simulé non lu, « Refaire » d'une refacturation transmise → refus au centre, rien d'envoyé.
- Interactions : D5-02 (même fonction, même place dans les formulaires) ; D5-01 (corrigé le 03/11 : la correction a sa propre clé, elle part bien quand la plateforme est active) ; D5-05 (après la bascule, la garde D5-05 passe avant et refuse aussi) ; D5-08 (le taux de l'époque gardé sur un bail devenu exonéré rend Concerne faux : la correction d'une facture transmise de ce bail est refusée ; à lever plus tard si le cas existe, en faisant suivre la facture plutôt que le bail) ; D5-06 (encaissement de l'ancienne) ; D4-03/BN17 (rien n'est émis : ApresCorrection n'est pas appelée) ; D4-10 (« Corriger » ou « Refaire » selon le type : le message dit « recommencez »).
- **Vérification : à corriger**
  - Corrections : Lieux et problème vérifiés :
- Régularisation : FactureRegulPanel.cs:853-854 (le refus n'est lu que si Concerne) et :982-991.
- Refacturation : FactureRefacPanel.cs:691-692 et :822-831.
- Preparer est muet hors plateforme (FactureElectronique.cs:151-154 et :132-139).
- Le loyer prévient dans Finaliser (FactureLoyerPanel.cs:687-691), appelé après l'e-mail (l.650).
- « Encaissée » part avec le nouveau montant sur l'ancien identifiant (FactureElectronique.cs:494-502).
L'option A est faisable pour les trois formulaires. Le paramètre par défaut garde Revue17Tests.D5_02 valable (Revue17Tests.cs:117-128). `dejaTransmise` ne sert qu'aux l.664 et 688, et aucun test ne lit « dejaTransmise » ni « n'y part pas ».
À reprendre :
1) Le refus est une impasse pour les causes durables : bail devenu exonéré (Concerne exige Tva.Soumis, FactureElectronique.cs:89), SCI déliée pour de bon avant la bascule, SIRET retiré parce qu'il était faux. « Recommencez quand la ligne Envoi annonce la plateforme » n'arrivera jamais : la facture transmise ne pourra plus être corrigée. Le collègue le renvoie à plus tard (« à lever plus tard si le cas existe ») : il faut le poser dans la question.
2) Message : donner la vraie raison, avec la même chaîne que LibelleEnvoi (FactureElectronique.cs:218-227), plutôt que toutes les causes possibles. Partager le texte avec la garde D5-05, qui passe avant après la bascule.
3) Calculer parPlateforme une seule fois et le passer jusqu'à Finaliser. Aujourd'hui Finaliser rappelle Concerne après l'e-mail (FactureRegulPanel.cs:983, FactureRefacPanel.cs:823, FactureLoyerPanel.cs:687). Si l'état change pendant la confirmation (Délier, lecture de l'état au lancement), le refus et la préparation ne voient pas le même état.
  - Risques : - Avec D5-08 (taux de l'époque gardé en correction), toute correction taxée d'une facture transmise d'un bail devenu exonéré sera refusée : Concerne lit le bail du jour, pas la facture.
- Premiers pas et bâtiment d'exemple appellent Finaliser avant RefusCorrection (FactureLoyerPanel.cs:491-496). Une fois le message retiré de Finaliser (l.688-691), ce chemin n'a plus aucune garde (cas rare).
- Dans le loyer, le PDF « corrigée(X) » est déjà écrit quand le refus tombe (l.480 avant l.526), comme pour D5-02 aujourd'hui. Aucun numéro n'est consommé, mais le fichier reste.
  - Décision manquante relevée : La question cite « SIRET retiré, bail devenu exonéré », mais l'option recommandée n'en dit pas la conséquence. Il faut une option qui distingue deux cas :
- causes passagères (état non lu, vérification en cours) : refus, on recommence plus tard ;
- causes durables (bail exonéré, SCI déliée, SIRET retiré) : au choix, un refus définitif dit clairement (la facture transmise ne se corrige plus dans CIPL), un envoi par e-mail après confirmation (la plateforme garde l'ancienne version), ou la méthode à demander à l'expert-comptable (avoir de la totalité puis nouvelle facture, déjà en suspens depuis D5-02).

### D5-08 · Corriger une ancienne facture après un changement de taux : le taux d'aujourd'hui est proposé, l'ancien remis se fige
- État au 05/11 : présent · effort M · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1332-1342 (ChargerTva : Tva.Taux(_loc, f), pas le taux de la ligne corrigée ; champ caché si le bail est devenu exonéré) et :904 (appel)
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1321-1325 (TauxFacture : bail non soumis → 0) ; :1328-1329 et :1278 (TauxAMemoriser : un taux différent du bail est gardé sur la facture du type, règle BK3)
  - Assets/Script/Facturation/FactureLoyerPanel.cs:709-713 (après une correction, le formulaire reste ouvert, _ligneCiblee remis à null)
  - Assets/Script/Facturation/FactureRegulPanel.cs:1156-1177 (mêmes fonctions), :352, :1067
  - Assets/Script/Facturation/FactureRefacPanel.cs:969-990, :350, :882 ; :431-433 (MontantPropose : HT relu au taux de l'époque, TTC recalculé au taux du jour)
  - Assets/Script/Facturation/Tva.cs:25-32 (Taux : facture du type › bail › entreprise)
  - À comparer : FactureLoyerPanel.cs:726-732 (ProvisionProposee : la correction garde la provision imprimée) ; taux de l'époque écrit sur la ligne : FactureLoyerPanel.cs:675, FactureRegulPanel.cs:979, FactureRefacPanel.cs:819
- Problème : La ligne du suivi garde le taux imprimé (FactureEtat.tauxTva), mais aucun des trois formulaires ne le lit pour une correction : il propose le taux du jour (facture du type, sinon bail, sinon entreprise), ou 0 si le bail est devenu exonéré, sans le dire. Si l'utilisatrice retape l'ancien taux, TauxAMemoriser le garde sur la facture du type : il s'impose ensuite aux factures neuves du locataire.
- Scénario : Bail de Dupont à 20 %, passé à 10 % au 1er juillet (Gestion du loyer). En septembre, « Corriger » le loyer du 1er trimestre (montant mal saisi) : le formulaire affiche 10 % → PDF et rectificative à 10 %, TVA divisée par deux sur une période taxée à 20 %. Si elle remet 20 % : le loyer du 4e trimestre s'ouvre à 20 % au lieu de 10 %. Refacturation : « Refaire » une taxe foncière refacturée à 20 % : le HT proposé est juste, le TTC est recalculé à 10 %. Bail devenu exonéré : la correction part sans TVA, avec la mention d'exonération.
- Correction proposée : Une règle pure à côté de Tva.Taux (Tva.cs) : `public static float TauxPropose(Locataire loc, FactureInfo f, FactureEtat ligneCiblee)` : ligne cliquée partie (EtatDe = Envoyé, Impayé ou Payé, le test de ProvisionProposee) → ligneCiblee.tauxTva (0 compris : une facture exonérée se corrige exonérée) ; sinon Taux(loc, f). Dans les trois formulaires (_ligneCiblee existe dans les trois) : ChargerTva montre le champ et la mention TVA si TauxPropose > 0 (au lieu de Tva.Soumis) et y met ce taux ; TauxFacture rend le taux tapé, sinon TauxPropose (plus de 0 forcé pour la correction d'une facture taxée d'un bail devenu exonéré) ; SaveFromUI n'écrit `f.tauxTva = TauxAMemoriser()` que hors correction (loyer l.1278, régul l.1067, refac l.882). Loyer : après une correction (Finaliser, l.709-713), rappeler ChargerTva(_loc.factureLoyer), sinon la facture suivante faite sans fermer le formulaire garderait le taux de l'époque et le mémoriserait. Le XML (tauxTva = TauxFacture()), MontantsParListe (régul l.1010) et PartsTtc (refac l.796) suivent sans autre changement. Limite (comme la provision) : le taux suit la ligne cliquée, pas une période choisie ensuite dans le menu. Option B : garder Taux(loc, f) dans le champ et afficher un message au centre à l'ouverture quand il diffère de ligneCiblee.tauxTva ; le garde-fou de SaveFromUI reste.
- Décision posée : On corrige une facture déjà partie, et le taux de TVA du bail a changé depuis (ou le bail est devenu exonéré). Quel taux le formulaire doit-il proposer, et le taux tapé pendant la correction doit-il devenir celui des factures suivantes ?
  - (recommandée) Le taux de la facture corrigée : Le formulaire de correction propose le taux imprimé sur la facture d'origine (même si le bail a changé de taux ou est devenu exonéré), modifiable ; ce qu'on tape pendant une correction ne vaut que pour elle et ne devient pas le taux des factures suivantes du type.
  - Le taux du jour, avec un avertissement : Le formulaire propose le taux actuel du bail ; s'il diffère de celui de la facture corrigée, un message au centre le signale à l'ouverture ; ce qu'on tape pendant une correction ne vaut que pour elle.
- Test : EditMode, TvaTests.Une_correction_garde_le_taux_de_l_epoque : locataire tvaLoyer = "oui", tauxTvaLoyer = 10 ; ligne { statut = "Envoye", tauxTva = 20, pdfPath, échéance } → Tva.TauxPropose(loc, null, ligne) = 20 ; sans ligne → 10 ; ligne « AttenteEnvoi » (jamais partie) → 10 ; tvaLoyer = "non" → 20 pour la ligne partie, 0 sans ligne ; ligne partie à tauxTva = 0 sur un bail soumis → 0. Test texte : les trois formulaires appellent Tva.TauxPropose( ; garder « TauxFacture() » et « UIFactory.MontrerAvecLibelle(_tauxTva.gameObject » (TvaTests existants).
- Interactions : D6-07 et revue 16c D6-04 (même idée pour la provision, ProvisionProposee) ; BK3/BK4 (le taux tapé reste mémorisé pour le type, mais plus depuis une correction) ; D5-07 (un bail devenu exonéré rend Concerne faux : la correction avec TVA d'une facture transmise est refusée ou part hors plateforme selon la décision D5-07) ; D5-03 (Tva.Montant en décimal, inchangé) ; D5-17 (taux libre) ; D4-04 (la même ligne garde déjà sa périodicité) ; décompte de sortie : FactureDepotPanel.cs:506, :523 et :756 prennent aussi le taux du jour, et sa ligne n'a pas son taux (D4-08) : même défaut, plus rare, hors de ces trois formulaires.
- **Vérification : à corriger**
  - Corrections : Lieux et problème vérifiés :
- ChargerTva lit Tva.Taux(_loc, f) : FactureLoyerPanel.cs:1332-1342, FactureRegulPanel.cs:1167-1177, FactureRefacPanel.cs:980-990.
- TauxFacture rend 0 si le bail est exonéré.
- TauxAMemoriser : l.1278 (loyer), 1067 (régul), 882 (refac).
- MontantPropose de la refac : l.431-433.
FactureEtat.tauxTva vaut 20 sur les anciennes lignes (FactureEtat.cs:19, TvaTests.cs:39) : pas de nouveau champ, pas de modèle à monter. EtatDe ne rend jamais ARembourser (FacturationSuivi.cs:243-296 ; « À rembourser » n'est qu'un affichage, l.349) : le test « partie » de ProvisionProposee convient.
À reprendre :
1) Avec « 0 compris » et le champ caché quand TauxPropose = 0, une facture exonérée ne peut plus être corrigée avec TVA, contrairement au « modifiable » de l'option 1. Et TauxFacture ignore un 0 tapé (« t > 0f », FactureLoyerPanel.cs:1324, FactureRegulPanel.cs:1159, FactureRefacPanel.cs:972) : une facture taxée ne peut pas devenir exonérée en correction. En correction, montrer le champ même à 0 et accepter 0, ou le trancher (voir décision).
2) Concerne, SansSiret et LibelleEnvoi lisent toujours Tva.Soumis(loc) (FactureElectronique.cs:89, :95, :219). Une correction taxée d'un bail devenu exonéré affichera « bail exonéré : pas de facture électronique » et partira par e-mail seul. Elle sera refusée par D5-07 (option A) si l'originale a été transmise, et par la garde D5-05 après la bascule, avec une raison fausse. À aligner selon la décision.
3) Régul et refac ne remettent pas _ligneCiblee à null après une correction, et le formulaire reste ouvert (FactureRegulPanel.cs:996-1001, FactureRefacPanel.cs:836-841). Une facture neuve faite ensuite garderait le taux de l'époque. Il faut le même rappel que pour le loyer : ChargerTva, et _ligneCiblee = null.
4) Dans SaveFromUI, « hors correction » doit se juger sur _ligneCiblee : SaveFromUI tourne avant FactureEmission.Preparer (FactureLoyerPanel.cs:462, puis 470).
5) Option B : un avertissement à l'ouverture est une information, donc un toast en bas selon la règle de l'appli, pas un message au centre.
6) Option 1 : « facture d'origine » est inexact. ligne.tauxTva est réécrit à chaque correction (l.675) : c'est le taux de la dernière version.
  - Risques : - Loyer : après « Corriger », un changement de période dans le menu (FactureLoyerPanel.cs:260-262) ne recharge ni la provision ni le taux. La facture NEUVE de l'autre période part alors au taux de l'ancienne ligne : la « limite » annoncée produit une facture neuve fausse. Plus sûr : lire la ligne de la période affichée (clé loyer-{année}-P{p}) et recalculer à chaque changement de période.
- En correction taxée d'un bail exonéré, MentionTva.Enregistrer (l.1277, 1066, 881) mémorise la mention TVA sur la facture du type.
- Le décompte de sortie (FactureDepotPanel.cs:506, :523, :756) garde le même défaut, comme le note le collègue.
  - Décision manquante relevée : La question est utile, mais deux points manquent :
1) Pendant une correction, peut-on passer d'exonéré à taxé et inversement ? Aujourd'hui seul le réglage du bail le permet ; avec la proposition, ni l'un ni l'autre.
2) La correction avec TVA d'une facture d'un bail devenu exonéré part-elle par la plateforme comme l'originale (Concerne suivrait alors la facture en correction) ou par e-mail seul (donc refusée par D5-07 et D5-05 si l'originale a été transmise) ? Le collègue a renvoyé ce point à plus tard sans le poser.

### D5-10 · Régularisation et refacturation par la plateforme : l'e-mail est une facture, pas la « copie » annoncée ; loyer dont le SIRET est tapé dans le formulaire
- État au 05/11 : présent · effort M · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Facturation/FactureRegulPanel.cs:356-364 (ligne « Envoi » : « + copie PDF par e-mail », mais champs de l'e-mail = ObjetFacture/CorpsFacture), :890-894 (envoi : modèles de facture), :1070-1071 (enregistrés comme e-mail de facture)
  - Assets/Script/Facturation/FactureRefacPanel.cs:354-362, :729-733, :885-886 (même chose)
  - Assets/Script/Facturation/FactureElectronique.cs:215-227 (LibelleEnvoi ; régularisation et refacturation l'appellent sans `notification`)
  - Assets/Script/Facturation/FactureLoyerPanel.cs:214 (_siret sans écouteur), :908-936 (plateforme, case retard, copie ou notification décidées au chargement), :1146-1151 (RefreshModeEnvoi : la ligne « Envoi » seulement, au changement de période), :516 et :573-577 (à l'envoi, seul le cas « copie affichée, plus de plateforme » est traité)
  - Assets/Script/Facturation/FactureElectronique.cs:523-540 (Notifier ; l.525 Destinataire(loc, r.type) sans la ligne)
  - Assets/Script/Facturation/ReglagePanel.cs:1133-1141 et EmailService.cs:251-270 (copie et notification décrites « pour un loyer »)
- Problème : Décisions du 30/10 : quand une facture part par Super PDP, le PDF envoyé par e-mail est une « copie pour information » (la facture électronique fait foi) ; dès le 1/9/2027, plus de PDF, une notification. BI5 (02/11) a fait partir la régularisation et la refacturation par la plateforme, mais leur e-mail est resté l'e-mail de facture : la ligne « Envoi » annonce une copie, le locataire reçoit deux originaux du même numéro (double saisie, double paiement). Après la bascule, elles enverraient encore le PDF (limite connue du 02/11). Loyer : si la fiche n'a pas de SIRET et que l'utilisatrice le tape dans le formulaire, rien ne suit (ligne « Envoi » orange « e-mail seulement », case retard ni forcée pour la plateforme ni grisée pour un professionnel, D6-03) ; à l'envoi, la facture part par la plateforme avec l'e-mail de facture normal. Trouvé en relisant : Notifier appelle Destinataire sans la ligne : après la bascule, la notification d'un loyer d'une période après une cession prévue part au cédant alors que la facture électronique va au cessionnaire.
- Scénario : Régularisation 2026 de Dupont (bail soumis, SIRET, SCI active) : la ligne « Envoi » dit « plateforme agréée (Super PDP) + copie PDF par e-mail » ; l'e-mail part avec l'objet « SCI X — facture 2026/11001 » et « Veuillez trouver ci-joint la facture… », puis la même facture arrive par sa plateforme : son comptable saisit les deux. Loyer de Martin (fiche sans SIRET) : SIRET tapé dans le formulaire, « Sauvegarder et envoyer » → e-mail de facture et transmission ; si la case retard était décochée, le PDF n'a pas la phrase de retard alors que le XML porte les pénalités.
- Correction proposée : Une règle pour les trois formulaires, dans EmailService : `Modeles(FactureInfo f, bool parPlateforme, bool notification)` → (objet, corps) de la facture (emailObjet/emailCorps), de la copie (copieObjet/copieCorps) ou de la notification (notifObjet/notifCorps), et son pendant `Garder(...)` pour l'enregistrement (la règle de FactureLoyerPanel.cs:1285-1300). Ces champs existent déjà dans FactureInfo pour chaque type : pas de nouveau champ. Régularisation et Refacturation : au chargement, _modeCopie/_modeNotif comme le loyer (Concerne(_loc, 1f, _siret.text), NotificationSeule), champs et aide de l'e-mail par Modeles ; à l'envoi (Regul l.890-894, Refac l.729-733), parPlateforme = Concerne(_loc, d.tva, _siret.text) : si le mode affiché n'est pas le vrai, le modèle du vrai mode (la règle copieFausse du loyer, dans les deux sens) ; SaveFromUI par Garder. Option A : après la bascule, la branche « Transmettre la facture par la plateforme ? » du loyer (l.537-553) dans les deux formulaires, avant leurs contrôles d'adresse e-mail (Finaliser(…, true) sans e-mail ; Traiter → Notifier fait le reste), et LibelleEnvoi(_loc, _siret.text, NotificationSeule). Les trois formulaires : `_siret.onValueChanged` → une méthode qui refait la partie « plateforme » (ligne « Envoi », case retard forcée pour la plateforme ou un professionnel, mode de l'e-mail ; les champs passent au modèle du nouveau mode s'ils n'ont pas été retouchés) ; au chargement, SIRET posé par SetTextWithoutNotify et cette méthode appelée en dernier. Notifier (FactureElectronique.cs:525) : Destinataire(loc, r.type, r). Textes : ReglagePanel.cs:1133-1141 (« un loyer » → « un loyer, une régularisation ou une refacturation »), commentaires d'EmailService (l.251-262), de FactureInfo (l.67-74) et de LibelleEnvoi (l.215-217), « Limite connue » de FACTURATION_CIPL_PENNYLANE.md.
- Décision posée : Une régularisation ou une refacturation qui part par Super PDP : avant le 1/9/2027, son e-mail devient la « copie pour information » avec le PDF, comme le loyer (décision du 30/10). Et à partir du 1/9/2027 ?
  - (recommandée) Comme le loyer : Plus de PDF par e-mail : « Transmettre la facture par la plateforme ? », puis l'e-mail de notification une fois la facture acceptée par Super PDP. Les trois formulaires suivent la même règle, et elle suit le SIRET tapé dans le formulaire.
  - La copie, avant comme après : L'e-mail « copie pour information » avec le PDF continue après le 1/9/2027 ; la notification reste propre au loyer (la limite connue du 02/11 demeure). Moins de travail.
- Test : EditMode, Revue17Tests.D5_10_la_copie_pour_tous : EmailService.Modeles(new FactureInfo { copieObjet = "Ma copie" }, true, false).objet = « Ma copie » ; Modeles(null, false, false).objet = EmailService.ObjetFacture() ; Modeles(null, true, false).objet = ObjetCopie() ; Modeles(null, true, true).objet = ObjetNotif() (comparer aux fonctions : les Réglages du poste peuvent avoir changé les modèles) ; tests texte : FactureRegulPanel et FactureRefacPanel contiennent EmailService.Modeles( ; les trois formulaires contiennent _siret.onValueChanged.AddListener( et toujours FactureElectronique.LibelleEnvoi( (Revue16cTests.D1_06) ; FactureElectronique contient Destinataire(loc, r.type, r). Parcours : régularisation d'un locataire soumis, e-mail vers agathelimouzy@gmail.com → « copie pour information » ; loyer d'une fiche sans SIRET, SIRET tapé → la ligne « Envoi » et la case retard changent aussitôt.
- Interactions : D5-05 (même flux d'envoi ; avec l'option A, après la bascule, la régularisation et la refacturation passent par la confirmation « Transmettre ») ; D5-07 (mêmes lignes des formulaires) ; D6-03 (la case retard d'un professionnel suit aussi le SIRET tapé) ; D7a-03/BN12 (SiretSaisi du loyer ; Notifier sans la ligne envoyait la notification au cédant) ; FactureEnvoiAuto.EnvoyerUne (l.453-456) lit déjà copieObjet du type de la ligne : rien à y changer ; NotifCorpsDefaut (EmailService.cs:265-270) dit « à régler le … » : faux pour l'avoir d'une régularisation après la bascule (option A) ; parcours BI8 et FACTURATION_CIPL_PENNYLANE.md à mettre à jour.
- **Vérification : à corriger**
  - Corrections : Lieux et problème vérifiés :
- Régularisation : FactureRegulPanel.cs:356-364, :890-894, :1070-1071.
- Refacturation : FactureRefacPanel.cs:354-362, :729-733, :885-886.
- Loyer : _siret sans écouteur (l.214), mode décidé au chargement (l.908-936), seul copieFausse traité à l'envoi (l.573-577).
- Notifier sans la ligne : FactureElectronique.cs:525.
Les champs copie et notif existent dans FactureInfo (FactureInfo.cs:67-74) et HeritageFacture les recopie déjà (HeritageFacture.cs:46-49) : pas de nouveau champ.
À reprendre :
1) L'option B décrit mal le code : la notification n'est pas propre au loyer. Traiter appelle Notifier pour toute ligne transmise après la bascule, sans regarder le type (FactureElectronique.cs:472). Après la bascule, une régul ou une refac part donc aujourd'hui avec l'e-mail de facture et le PDF, PUIS la notification. Avec l'option B, ce serait la copie puis la notification. Pour tenir l'option B : `if (NotificationSeule && r.type == "Loyer")`.
2) Même cause : après la bascule, un refus de Super PDP remet la ligne « en attente d'envoi » avec « elle n'est pas partie, le locataire n'a rien reçu » (FactureElectronique.cs:479-485). C'est faux pour une régul ou une refac tant qu'elles partent avec le PDF.
3) La phrase « à régler le … » de NotifCorpsDefaut (EmailService.cs:267) part déjà aujourd'hui pour un avoir après la bascule (tous types, loyer à provision négative compris), pas seulement avec l'option A.
4) FactureRappelService.Adresse (FactureRappelService.cs:46) oublie aussi la ligne : à corriger avec Notifier, ou à renvoyer à D4-01.
5) Régul et refac n'ont pas de champ _aideEmail : leur aide est un texte fixe (FactureRegulPanel.cs:257-259, même chose dans la refac). Le garder dans un champ pour le changer selon le mode.
6) Quand l'écouteur change le mode, un texte retouché reste à l'écran, mais SaveFromUI l'écrit dans la paire du nouveau mode (FactureLoyerPanel.cs:1286-1300). Un e-mail de facture personnalisé deviendrait la « copie » du type. Garder le texte retouché dans la paire de l'ancien mode avant de basculer.
7) Tests à garder : Revue17Tests.cs:667 exige « FactureElectronique.Professionnel(_loc, SiretSaisi()) » dans le loyer ; Revue16cTests.cs:203-204 exige LibelleEnvoi dans les trois formulaires.
  - Risques : - Formulaire de dépôt : même défaut pour la case retard d'un professionnel (D6-03). FactureDepotPanel.cs:452 ne lit le SIRET qu'au chargement, et la proposition ne vise que trois formulaires.
- Loyer : RefreshTitulaire réécrit _siret.text (FactureLoyerPanel.cs:1161) au milieu de LoadIntoUI (l.875), avant ChargerTva et la case retard (l.904-911). L'écouteur se déclenchera trop tôt : il ne doit rien faire tant que le chargement n'est pas fini, puis la méthode est appelée en dernier.
- Option A : une régul en avoir passe déjà par la confirmation « Remboursement au locataire » (FactureRegulPanel.cs:816-825). « Transmettre ? » ajoute une deuxième confirmation à la suite (sujet de D8-02).
  - Décision manquante relevée : La question est utile, mais l'option B doit être réécrite avec ce qu'elle implique vraiment : soit limiter la notification au loyer (une condition dans Traiter), soit accepter la copie PDF puis la notification. Dans les deux cas, adapter le message du refus après la bascule. Le reste applique des décisions déjà prises (30/10, 01/11, D7a-03) : copie avant la bascule, SIRET tapé suivi aussitôt, notification au titulaire de la période. Pas d'autre question à poser.

### D6-04 · « Facturer le loyer » (bouton de la fiche, alerte de l'accueil, carte « Automatique ») rouvre la dernière période enregistrée : « Sauvegarder et envoyer » envoie alors une correction de cette période au lieu de la facture à envoyer
- État au 05/11 : présent · effort M · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Facturation/FactureLoyerPanel.cs:866-870 — LoadIntoUI : période et année = factureLoyer.moisPeriode / anneePeriode (la dernière enregistrée), sinon DefaultPeriodeIndex
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1305-1307 — SaveFromUI : la période est mémorisée à chaque « Générer facture » et « Sauvegarder »
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1058-1066 — DefaultPeriodeIndex : la période en cours, même si elle est déjà partie
  - Assets/Script/Facturation/FactureLoyerPanel.cs:137-148 — AppliquerLigneCiblee : « (correction) » seulement quand une ligne du suivi est passée ; sinon titre sans « (correction) » (et sans le nom du locataire, D10-14)
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1206-1226 — RefreshBoutonSauver / PeriodePartie : une période déjà partie affiche « Sauvegarder et envoyer », comme une facture neuve
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1102-1122 — DefaultEcheance : l'échéance affichée est celle de la facture partie
  - Assets/Script/Facturation/FactureLoyerPanel.cs:464-474 et 503-510 — clé = période mémorisée ; Preparer décide « correction » ; une correction n'attend pas la date d'envoi, elle part tout de suite (et en rectificative à Super PDP si la SCI y est)
  - Assets/Script/Facturation/FactureLoyerPanel.cs:709-713 — après une correction, le titre repasse sans « (correction) »
  - Assets/Script/Locataire/LocataireFacturationFields.cs:335 — bouton « Facturer le loyer » : OpenLoyer(_fiche) sans ligne
  - Assets/Script/General/HomeAlert.cs:177-180 — RappelsAuto.Ouvrir : OpenLoyer(lp) sans ligne ; appelé par l'accueil (Assets/Script/General/GeneralMenuPanel.cs:637-640) et par les cartes « Automatique » du tableau d'objectifs (Assets/Script/Objectif/ObjectifsTableau.cs:171)
  - Assets/Script/General/HomeAlert.cs:26 et 312-326 — HomeAlert ne garde que le type d'alerte, pas la ligne
  - Assets/Script/Facturation/FacturationAlertes.cs:16-21, 44-58 et 124-129 — l'alerte calcule la période (loyer-{année}-P{p}) ou la ligne à corriger (l.key) mais ne la transmet pas
  - Assets/Script/Cloud/PremiersPas.cs:549 — « Une première facture » : OpenLoyer(lp) sans ligne
  - Chemins corrects (ligne passée) : Assets/Script/Facturation/LocataireSuiviInline.cs:508 et Assets/Script/Locataire/DepartLocataireUI.cs:108
  - Voisins, moins graves : Assets/Script/Facturation/FactureRegulPanel.cs:393-394 (l'année mémorisée n'est reprise que si elle a encore des charges à facturer : la régul émise est alors corrigée, élargie, sans « (correction) ») ; Assets/Script/Facturation/FactureDepotPanel.cs:62-71 et 387 (révision : même clé tant que la date de révision n'a pas bougé, titre sans « (correction) ») ; refacturation non concernée (aucune charge cochée sans ligne, FactureRefacPanel.cs:389)
- Problème : Sans ligne du suivi, le formulaire du loyer reprend la période de la dernière facture enregistrée, donc en général une période déjà partie. Rien ne le dit : le titre n'affiche « (correction) » que si l'on vient d'une ligne du suivi, et le bouton dit « Sauvegarder et envoyer » comme pour une facture neuve (libellé choisi en BE12, qui ne distingue pas la correction). FactureEmission.Preparer voit la période « Envoyé » et fabrique une version « corrigée(1) », qui part tout de suite. L'alerte connaît pourtant la bonne période, mais elle ne la transmet pas. Même absence de signal quand on choisit à la main une période déjà partie dans le menu « Période ».
- Scénario : Loyer mensuel, échéance le 1er, envoi 15 jours avant. Novembre est envoyé le 17/10 (période mémorisée : 11). Le 16/11, l'accueil affiche « URGENT — Loyer : échéance le 01/12, à envoyer avant le 16/11 ». Clic : le formulaire s'ouvre sur « Novembre 2026 », échéance 01/11, titre « Information Facture — Loyer », bouton « Sauvegarder et envoyer ». Clic puis « Envoyer » : le locataire reçoit le loyer de novembre « corrigée(1) » (seul indice : « -corrigee1 » dans le nom de la pièce jointe) ; si la SCI est sur Super PDP, une rectificative de novembre part aussi à la plateforme ; décembre n'est pas facturé et l'alerte reste allumée. Même chose par le bouton « Facturer le loyer » de la fiche ou une carte « Automatique ». L'alerte « Loyer Mars 2026 : montant à corriger (départ …) » ouvre elle aussi la dernière période enregistrée, pas mars.
- Correction proposée : 1) FactureLoyerPanel : aide statique PeriodeAOuvrir(Locataire loc, DateTime auj) qui remplace DefaultPeriodeIndex (1058-1066) : à partir de la période en cours (dernier mois d'échéance atteint), la première période facturable (Loyers.PeriodeFacturable, et pas avant la date de reprise) dont le loyer n'est pas parti (!FacturationSuivi.DejaTraite : une facture « En attente d'envoi » reste proposée, comme l'alerte), cette année puis la suivante ; à défaut, la période en cours. 2) LoadIntoUI (867-870) : période et année = PeriodeAOuvrir ; la ligne cliquée garde la priorité (AppliquerLigneCiblee passe après). SaveFromUI continue d'écrire moisPeriode / anneePeriode (la clé de la ligne 464 en dépend). 3) Le signal à un seul endroit : RefreshBoutonSauver (1206-1217, déjà appelé à chaque changement de période, d'année ou d'échéance) pose aussi le titre « Information Facture — Loyer (correction) · Nom » si PeriodePartie(), sinon sans « (correction) » mais avec le nom (règle D10-14), et le bouton « Corriger et envoyer » (« Émettre (sans envoyer) » inchangé). Retirer le titre d'AppliquerLigneCiblee (145-147) et de Finaliser (712) ; appeler RefreshBoutonSauver à la fin de LoadIntoUI, d'AppliquerLigneCiblee et de Finaliser (un champ dont le texte ne change pas ne déclenche pas onValueChanged). 4) Alerte : champ non enregistré FacturationAlertes.Alerte.cle posé aux lignes 53-57 (« loyer-{E.Year}-P{pe} ») et 128-129 (l.key) ; HomeAlert.cleFacture recopié dans HomeAlertCollector (HomeAlert.cs:325) ; RappelsAuto.Ouvrir (HomeAlert.cs:180) : OpenLoyer(lp, la ligne stockée de cette clé, sinon new FactureEtat { key = cle }). Appelants sans ligne (LocataireFacturationFields.cs:335, PremiersPas.cs:549) : inchangés, ils profitent de 1 à 3. Le même signal « (correction) » pourrait s'étendre plus tard à la régul et au dépôt (FactureEmission.EstDejaEmise sur la clé) : non indispensable.
- Décision posée : Quand on clique « Facturer le loyer » (bouton de la fiche ou alerte de l'accueil), quelle période le formulaire doit-il ouvrir ?
  - (recommandée) La période à envoyer : La première période, à partir de celle en cours, dont le loyer n'est pas encore parti (novembre parti : décembre). L'alerte ouvre la période dont elle parle. Si la période affichée est déjà partie, le titre dit « (correction) » et le bouton « Corriger et envoyer ».
  - Le rattrapage d'abord : La plus ancienne période « À faire » de l'année (un mois oublié passe avant), sinon la suivante, avec le même signal « (correction) ». Inconvénient : un locataire saisi en cours d'année sans date de reprise rouvre janvier à chaque clic.
  - Comme aujourd'hui, mais dit : Garder la dernière période enregistrée et seulement afficher « (correction) » et « Corriger et envoyer ». Il faut encore choisir la bonne période à la main.
- Test : EditMode Revue17Tests.D6_04_facturer_le_loyer_ouvre_la_periode_a_envoyer_pas_la_derniere_emise : locataire mensuel (jour de demande 1, bail 2025), ligne loyer-2026-P11 « Envoye » → PeriodeAOuvrir(loc, 16/11/2026) = (2026, 12) ; sans cette ligne → (2026, 11) ; P12 aussi « Envoye » → (2027, 1) ; trimestriel avec T4 « Envoye » → (2027, 1). Et FacturationAlertes.Pour(loc, 16/11/2026) (locataire complet, comme dans DepartLocataireTests) : l'alerte Loyer porte cle = « loyer-2026-P12 ». Play : l'alerte de l'accueil ouvre décembre sans « (correction) » ; choisir novembre à la main affiche « (correction) » et « Corriger et envoyer ».
- Interactions : D10-14 (même titre : à corriger ensemble, en gardant le nom). D6-07 (les provisions suivent _ligneCiblee : avec la ligne passée par l'alerte « montant à corriger », la correction reprend les provisions de CETTE facture ; changer la période à la main ne relit pas les provisions : limite existante). D7a-03 (RefreshTitulaire suit déjà la période : rien à faire). D4-04 (une clé prise par l'ancienne périodicité est « traitée », donc sautée ; Preparer la refuse de toute façon ; avenant daté en 2c). D4-03 / BN17 (« À rembourser » garde le statut « Envoye » : compté comme parti, donc « (correction) »). D5-02 et D5-06 (une correction involontaire part aussi en rectificative à Super PDP et laisse l'« Encaissée » de l'ancienne : raison de plus de corriger). D6-05 (le libellé du bouton dépend aussi de l'échéance lue). D4-01 (destinataire par facture) : sans lien direct.
- **Vérification : à corriger**
  - Corrections : Le constat est confirmé dans le code actuel et les numéros de ligne cités sont justes. Lieux relus : FactureLoyerPanel.cs:867-870 (période et année mémorisées), 1305-1307 (mémorisées à chaque Générer et Sauvegarder), 142-147 (« (correction) » seulement avec une ligne passée), 1206-1226 (bouton), 464-474 et 505-506 (Preparer décide la correction, qui part sans attendre la date d'envoi). Appelants sans ligne : LocataireFacturationFields.cs:335, HomeAlert.cs:180 (accueil GeneralMenuPanel.cs:639, cartes ObjectifsTableau.cs:171), PremiersPas.cs:549. HomeAlert ne garde que le type (HomeAlert.cs:26, 325). Les lots 1, 2a et 2b n'y ont rien changé.

Cinq points à corriger :

1) Le critère du signal. Ne pas prendre PeriodePartie() (statut brut, FactureLoyerPanel.cs:1220-1226). Prendre la règle de Preparer : FacturationSuivi.EstDejaEmise(loc, key, out r) && r.statut != "AttenteEnvoi" (FactureEmission.cs:61-71). EstDejaEmise exige un PDF (FacturationSuivi.cs:445-453). Cas qui trompe : une période forcée « Envoyé » ou « Payé » par le menu de la pastille, sans PDF (facture partie hors CIPL). Le titre dirait « (correction) » et le bouton « Corriger et envoyer », alors que Preparer émet une facture neuve, avec un nouveau numéro. MarquerEnvoye réécrit alors « Payé » en « Envoyé » (FacturationSuivi.cs:431). Le mieux : une seule aide, utilisée par le titre et le bouton (ex. FactureEmission.SeraUneCorrection).

2) Le repli « à défaut, la période en cours ». Pour un locataire parti, plus aucune période n'est facturable (FinFacturation = date de sortie, Loyers.cs:56-57 ; PeriodeFacturable faux, Loyers.cs:136-142). Le formulaire ouvrirait donc une période APRÈS le départ : loyer 0 € (MontantPeriode, Loyers.cs:118-131) mais provision de la fiche. FactureLoyerPanel ne refuse rien de tel : « Sauvegarder et envoyer » facturerait des provisions après la sortie. Repli proposé : la dernière période facturable jusqu'à aujourd'hui (le dernier loyer, affiché « (correction) » s'il est parti), sinon la période mémorisée (le comportement actuel).

3) Le scénario est à rectifier, le défaut reste réel.
- L'accueil n'affiche pas le message de l'alerte : sa ligne dit « Facturer » / « À faire » (HomeAlert.cs:304, 316). Le texte « URGENT — Loyer : échéance le 01/12… » s'affiche sur la fiche, juste au-dessus du bouton « Facturer le loyer » (LocataireFacturationFields.cs:391-410).
- « -corrigee1 » n'est pas le seul indice. Avec le modèle d'usine, l'objet montré dans la confirmation est « {societe.nom} — facture {numero} », donc « … corrigée(1) » (EmailService.cs:243 ; _numeroEmis posé ligne 472, contexte lignes 569-575). L'aperçu de droite, rendu avant la confirmation (486-488), imprime aussi « corrigée(1) ».
- Mais rien ne prévient avant le clic : le défaut tient.

4) Une nuance à écrire dans l'option 1. PeriodeAOuvrir part de la période en cours ; l'alerte part de la prochaine échéance à venir (FacturationAlertes.cs:150-161). Exemple : le 16/11, novembre pas encore parti. Le bouton ouvre novembre (en retard, et sans alerte), alors que l'alerte affichée juste au-dessus parle de décembre. C'est défendable (novembre est dû), mais il faut le dire.

5) Ajouter deux cas au test : P11 forcée « Payé » sans PDF (pas de « (correction) ») ; locataire parti le 15/10/2026, le 16/11 → (2026, 10).

Le reste est faisable :
- MoisEcheance, Loyers.PeriodeFacturable et DejaTraite sont publics.
- Le champ ajouté sur FacturationAlertes.Alerte et HomeAlert n'est pas enregistré : il est hors de l'empreinte D2_01 (Revue17Tests.cs:56 ne couvre que Batiment, Locataire et ReglageData). Pas de changement de modèle.
- Tous les HomeAlert viennent de HomeAlertCollector.Collect (accueil, résumé, ObjectiveManager.cs:242).
- UIDropdown.SetOptions ne notifie pas (UIDropdown.cs:47-53) : l'appel explicite de RefreshBoutonSauver est bien nécessaire. Poser le titre avant la garde « _btnSauver == null » de cette méthode.
- Le guide PremiersPas trouve le bouton par son parent « Right » (PremiersPas.cs:297-302), pas par son libellé : il ne casse pas. Il cesse même de rester bloqué quand la période mémorisée était déjà émise (FacturesEmises, PremiersPas.cs:271).
- Aucun test existant ne casse : Revue16cTests.cs:437 et 546-549 ne sont pas touchés. Rien côté serveur.
  - Risques : - Locataire parti : une période hors bail s'ouvre avec la provision de la fiche (voir point 2).
- Après une première émission réellement envoyée, le titre passe à « (correction) » et le bouton à « Corriger et envoyer ». C'est juste (un second clic corrigerait), mais c'est un changement visible à annoncer.
- Après un changement de périodicité en cours d'année (D4-04), la clé du trimestre en cours peut être prise par un mois « Envoyé ». Ce trimestre est alors sauté (comme par l'alerte), et il reste non facturable jusqu'au thème 2c.
- Choisir à la main une période déjà partie ne relit pas les provisions de sa ligne (limite D6-07). Le signal « (correction) » prévient, mais la provision proposée reste celle de la fiche.
  - Décision manquante relevée : - Le repli n'est pas posé : que faire quand aucune période n'est à envoyer (locataire parti, ou tout parti jusqu'à l'an prochain) ? Proposer « la dernière période facturable » (recommandé) ou « la période mémorisée, comme aujourd'hui ».
- L'option 1 doit aussi dire que le bouton de la fiche ouvre la période en cours non partie, même quand l'alerte affichée au-dessus parle de la suivante.

### D6-05 · Une date ou une échéance illisible est remplacée sans rien dire (quatre formulaires de facture et fiche d'une charge) : facture datée et due le jour même, loyer envoyé tout de suite, charge rangée dans une autre année
- État au 05/11 : présent · effort S · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Tools/SaisieDate.cs:13 et 18-20 — formats lus : jj/mm/aaaa, j/m/aaaa, jj/mm/aa ; « 01-12-2026 », « 1.12.2026 », « 1er décembre 2026 » sont illisibles
  - Assets/Script/Facturation/FactureLoyerPanel.cs:445-447 et 457-462 — « Générer facture » et « Sauvegarder et envoyer » : aucun contrôle avant SaveFromUI
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1271-1272 — date et échéance illisibles enregistrées vides
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1021 — date illisible → aujourd'hui (PDF, Factur-X ligne 695) ; 1241 — préfixe du numéro au mois du jour
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1092-1096 — phrase « SOMME À NOUS RÉGLER LE » à la date du jour
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1198-1201 et 505-510 — échéance illisible : plus de date d'envoi, le bouton redevient « Sauvegarder et envoyer » et le loyer part tout de suite
  - Assets/Script/Facturation/FactureLoyerPanel.cs:668 — échéance vide transmise au suivi ; 695 → Assets/Script/Facturation/FactureEn16931.cs:117-120 (« Payable à réception »)
  - Assets/Script/Facturation/FactureRegulPanel.cs:786-788 et 796-803 (aucun contrôle), 1060-1061 (vide enregistré), 690 (date → aujourd'hui), 660, 646, 960 et 974 (échéance vide au suivi), 985
  - Assets/Script/Facturation/FactureRefacPanel.cs:658-660 et 667-672 (aucun contrôle), 875-876, 594, 566, 552, 685 et 704 (nom du PDF au mois du jour), 803, 814, 825
  - Assets/Script/Facturation/FactureDepotPanel.cs:804-806 et 813-815 (aucun contrôle), 1001-1002, 628, 600, 586, 849 (année du nom du PDF), 940
  - Assets/Script/Facturation/FacturationSuivi.cs:389-396 — MarquerEnvoye : échéance illisible → date du jour, simple avertissement au journal
  - Assets/Script/Facturation/ChargePanel.cs:195-199 — « Enregistrer » ferme la fenêtre sans contrôle ; 488-492 — coût illisible → 0, date illisible → ancienne date (aujourd'hui pour une nouvelle charge) ; 352 — parts affichées à l'ancienne date ; 580-583 — TryParseFr, copie de SaisieDate
  - Assets/Script/Facturation/ChargePanel.cs:124 — le coût est déjà filtré à la frappe (BN2) : seuls « - » ou une virgule seule passent encore à 0
  - Précédent à suivre : Assets/Script/RevisionLoyer/RevisionPanel.cs:1553-1567 (DatesIllisibles, BJ4)
- Problème : Aucun des quatre formulaires de facture ne refuse une date qu'il ne sait pas lire : la date de la facture devient celle du jour, l'échéance est enregistrée vide puis remplacée par la date du jour dans le suivi. Pour le loyer, une échéance illisible fait aussi disparaître la date d'envoi : la facture part immédiatement. La fiche d'une charge se ferme sans contrôle et garde l'ancienne date (ou celle du jour). La règle du projet (refus au centre, formulaire gardé avec la saisie), appliquée le 02/11 à la Gestion du loyer (BJ4), ne l'est pas ici.
- Scénario : Le 03/11, loyer de décembre, échéance tapée « 01-12-2026 ». Le bouton passe de « Enregistrer pour l'envoi du 16/11 » à « Sauvegarder et envoyer » ; la facture part tout de suite au lieu du 16/11. Le PDF dit « SOMME À NOUS RÉGLER LE 3 novembre 2026 », la facture électronique « Payable à réception ». Le suivi enregistre l'échéance du 03/11 : « Impayé » le 18/11 et bouton « Rappel ». Autre cas : le 05/01/2027, charge « Eau » datée « 31.12.2026 » : enregistrée au 05/01/2027 sans message ; elle sort de la régularisation 2026, entre dans celle de 2027 et dans les charges 2027 du rendement net.
- Correction proposée : 1) SaisieDate : ajouter public static string Refus(string saisie, string quoi) → null si la date se lit, sinon « {quoi} « … » illisible : tapez-la sous la forme jj/mm/aaaa. » (vide : « {quoi} : obligatoire. »). Selon la décision, TryParse lit aussi « - » et « . » (remplacés par « / » avant TryParseExact ; « 2026-12-31 » reste refusé, comme le veut SaisieDateTests). 2) Les quatre formulaires, en tête de GenererApercu et de SauvegarderEtEnvoyer, avant SaveFromUI : var r = SaisieDate.Refus(_date.text, "Date de la facture") ?? SaisieDate.Refus(_echeance.text, "Date d'échéance"); if (r != null) { ConfirmDialog.Erreur(r); return; } — FactureLoyerPanel.cs:445 et 457, FactureRegulPanel.cs:786 et 796 (avant Regroupement), FactureRefacPanel.cs:658 et 667, FactureDepotPanel.cs:804 et 813. Le formulaire reste ouvert avec la saisie. 3) ChargePanel, clic « Enregistrer » (195-199), avant SaveForm : nom vide (D10-06), date vide ou illisible (SaisieDate.Refus), coût illisible (!SaisieNumerique.TryParse) → ConfirmDialog.Erreur puis return (la fenêtre de la charge reste ouverte) ; remplacer TryParseFr (580-583) par SaisieDate.TryParse. 4) Garder le repli de MarquerEnvoye (FacturationSuivi.cs:391-396) comme dernier filet pour d'anciennes données. Ne pas refuser une charge sans locataire coché, contrairement à la fiche D6-05 : elle compte dans le rendement net (Assets/Script/Batiment/BatimentSummaryView.cs:670-677), c'est une charge du propriétaire.
- Décision posée : Quand une date est tapée autrement que jj/mm/aaaa (« 01-12-2026 », « 1.12.2026 ») dans un formulaire de facture ou la fiche d'une charge, que doit faire CIPL ?
  - (recommandée) Lire tirets et points, refuser le reste : « 01-12-2026 » et « 1.12.2026 » sont lus comme le 01/12/2026 ; une date vide ou encore illisible (« 1er décembre ») est refusée au centre, le formulaire reste ouvert avec ce qui a été tapé.
  - N'accepter que jj/mm/aaaa : Tout autre format est refusé au centre, formulaire ouvert. Plus strict, donc plus de refus.
  - Remplacer et prévenir : Garder le remplacement par la date du jour, mais l'annoncer par un message en bas. Une facture peut encore partir avec une date fausse.
- Test : EditMode SaisieDateTests : Une_date_avec_tirets_ou_points_est_lue (« 01-12-2026 », « 1.12.2026 » → 01/12/2026 ; « 2026-12-31 » toujours refusé par le test existant) et Le_refus_nomme_la_date_illisible (Refus("01/12/2026", "Date d'échéance") = null ; Refus("1er décembre 2026", …) et Refus("", …) non nuls). Revue17Tests.D6_05 (test texte, comme D2_01) : GenererApercu et SauvegarderEtEnvoyer des quatre panneaux appellent SaisieDate.Refus. Play : fiche d'une charge avec « 31.13.2026 » → message au centre, fenêtre toujours ouverte, aucune charge ajoutée.
- Interactions : D10-06 (même garde dans ChargePanel, avec le nom vide : un seul correctif). D8-08 (années à deux chiffres lues en 19xx, même SaisieDate.TryParse : à régler dans le même geste, TwoDigitYearMax 2099 ou quatre chiffres). D6-15 (refus sans RIB : même endroit, une seule garde « avant émission » dans les quatre formulaires). D6-04 (le libellé du bouton du loyer dépend de l'échéance lue). D6-08 ({echeance} remplie avec la même lecture). D4-03 de la 16c (une correction part tout de suite : inchangé). FactureEnvoiAuto (une échéance illisible retient l'envoi : ne concernera plus que d'anciennes lignes). D8-12 / D10-02 (Entrée et Échap sur le message). D6-10 (le refus arrive avant que _numeroEmis soit posé : rien à remettre).
- **Vérification : confirmé**
  - Corrections : Tous les lieux cités existent et le problème est réel aujourd'hui.
- FactureLoyerPanel.cs : 445-447, 457-462, 505-506, 668, 695, 1021, 1092-1096, 1198-1201, 1241, 1271-1272.
- FactureEn16931.cs:117-120 : une échéance vide donne « Payable à réception. ».
- FactureRegulPanel.cs : 646, 660, 690, 786-788, 796-803, 960, 974, 985, 1060-1061.
- FactureRefacPanel.cs : 552, 566, 594, 658-660, 667-672, 685, 704, 803, 814, 825, 875-876.
- FactureDepotPanel.cs : 586, 600, 628, 804-806, 813-815, 849, 940, 1001-1002.
- FacturationSuivi.cs:391-396.
- ChargePanel.cs : 91 (une nouvelle charge est datée du jour), 195-199, 352, 488-492, 580-583.
Les lots n'ont corrigé que le filtre du coût (BN2, ChargePanel.cs:124 ; « - », « , » ou « . » seuls passent encore à 0).

Petits compléments :
- a) FactureDepotPanel.cs:69 (CleSuivi) : sans date de révision, l'année de la clé « depot-AAAA » vient de la date de la facture, ou d'aujourd'hui si elle est illisible. La garde, placée avant SaveFromUI et Preparer, la couvre : il suffit de la citer.
- b) Avec l'option 1, la copie TryParseFr de LocataireFacturationFields.cs:59-62 (fenêtre Dépôt de la fiche) resterait stricte. La remplacer aussi par SaisieDate.TryParse. Là, rien n'est remplacé en silence : le bouton est grisé avec « Date de révision invalide » (826-836).
- c) Le test texte « les panneaux appellent SaisieDate.Refus » doit vérifier les DEUX méthodes de chaque panneau (au moins deux appels, ou une aide par panneau appelée deux fois). Sinon une seule garde suffit à le faire passer.
- d) SaisieNumerique.TryParse("") renvoie vrai, avec 0 (SaisieNumerique.cs:27). La garde « coût illisible » laisse donc passer une charge au coût vide, à 0 €.

Faisabilité vérifiée :
- ConfirmDialog.Erreur s'affiche au-dessus de la fenêtre de la charge (ConfirmDialog.cs:80, SetAsLastSibling). Précédent : LotsPanel.cs:177, « la fenêtre reste ouverte, avec la saisie ».
- Après « - » remplacé par « / », « 2026-12-31 » reste refusé : aucun format ne commence par l'année. SaisieDateTests.cs:21 tient.
- Aucun test existant ne casse : Revue16cTests.cs:437 et 557-558 sont intacts ; Revue17Tests.cs:204-206 lit d'autres lignes de ChargePanel.
- Rien côté serveur, aucun champ enregistré.
  - Risques : - Une charge déjà enregistrée avec une date vide ou illisible s'ouvrira avec un champ date vide : « Enregistrer » sera refusé tant qu'aucune date n'est tapée. C'est un changement visible.
- D8-08 touche le même fichier : si TwoDigitYearMax n'est pas réglé dans le même geste, « 01-12-30 » sera lu 1930 sans refus.
- Refuser aussi « Générer facture » (l'aperçu) est plus strict qu'aujourd'hui, où l'aperçu passait avec la date du jour. C'est cohérent avec BJ4.
- Les anciennes lignes à échéance vide gardent le repli de MarquerEnvoye (date du jour) : c'est voulu.
  - Décision manquante relevée : La question est bien posée. Deux points à soumettre aussi à l'utilisatrice, sans les trancher seul :
- La fiche D6-05 proposait de refuser une charge sans locataire coché ; le collègue l'écarte (c'est une charge du propriétaire, comptée dans le rendement net, BatimentSummaryView.cs:670-677). Ce statu quo est défendable, mais il faut lui dire.
- Un coût laissé vide doit-il être refusé, ou enregistré à 0 € comme aujourd'hui ?

### D6-08 · Variables du menu « / » vides ou imprimées telles quelles : {echeance} vide dans tout ce qui part des formulaires ; {montant}, {depot.*} et {reste} proposés partout mais remplacés dans une seule phrase
- État au 05/11 : présent · effort M · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1017-1031, FactureRegulPanel.cs:684-700, FactureRefacPanel.cs:590-604, FactureDepotPanel.cs:625-636 — BuildContext ne pose pas echeance
  - Résolus avec ce contexte : e-mails (FactureLoyerPanel.cs:569-577, FactureRegulPanel.cs:890-894, FactureRefacPanel.cs:729-733, FactureDepotPanel.cs:888-892), entête (FactureLoyerPanel.cs:406, FactureRegulPanel.cs:710, FactureRefacPanel.cs:615, FactureDepotPanel.cs:645, 696, 735), phrase de règlement, mention TVA, phrase du montant mensuel
  - Assets/Script/Facturation/FactureVarResolver.cs:93 — {echeance} = ctx.echeance, donc vide
  - Remplie ailleurs : Assets/Script/Facturation/FactureEnvoiAuto.cs:536-551 (envoi au lancement ; aussi la notification, FactureElectronique.cs:529, et le rappel, FactureRappelService.cs:53) ; Assets/Script/Facturation/ReglagePanel.cs:1178-1183 (essai)
  - Assets/Script/Facturation/FactureVariables.cs:53 ({reste}), 61-64 ({montant}), 66-73 ({depot.*}) — commentaires « le groupe le dit dans le menu »
  - Assets/Script/Facturation/SlashAutocomplete.cs:87-119 — tout le catalogue dans chaque champ ; 107 — le menu montre libellé et jeton, pas le groupe
  - Les 21 champs avec le menu : FactureLoyerPanel.cs:295, 308, 327, 330 ; FactureRegulPanel.cs:240, 253, 256 ; FactureRefacPanel.cs:237, 249, 252 ; FactureDepotPanel.cs:262, 266, 270, 274, 312, 323, 326 ; MentionTva.cs:85 ; ReglagePanel.cs:920, 1102, 1105, 1167
  - Seuls endroits qui remplacent : Assets/Script/Facturation/FacturePdfService.cs:107 ({montant}, ligne mensuelle du loyer) ; Assets/Script/Facturation/ExplicationDepot.cs:114-118 ({depot.*}, quatre phrases de l'explication du dépôt) ; Assets/Script/Facturation/FactureRappelService.cs:57 ({reste} ; ailleurs il vaut « 0,00 € »)
  - Assets/Script/Facturation/ExplicationDepot.cs:112 — l'explication du dépôt résout avec un contexte vide : {date}, {numero}, {periode}, {echeance} sortent vides et {loyer.*} « 0,00 € »
  - Assets/Editor/Tests/FactureVariablesTests.cs:118-131 — le test suppose que le groupe est montré dans le menu
  - Gabarits HTML (Assets/StreamingAssets/facture_template.html, facture_regul_template.html) : aucun jeton « {…} », rien à y changer
- Problème : Un même modèle donne deux résultats : l'essai des Réglages, l'envoi au lancement, la notification et le rappel posent l'échéance ; les quatre formulaires ne la posent pas. Et le menu « / » propose dans tous les champs « Montant mensuel calculé », « Montant du dépôt (positif) », « Reste à payer (rappel) », alors qu'ils ne sont remplacés que dans une seule phrase : ailleurs le jeton part tel quel (ou « 0,00 € » pour {reste}). Le commentaire promet que le menu affiche le groupe : il ne l'affiche pas. À l'inverse, les variables « Facture » proposées dans l'explication du dépôt y sortent vides. La fenêtre de confirmation ne montre que l'objet de l'e-mail : un corps faux passe inaperçu.
- Scénario : Modèle « E-mail de facture » : « … à régler avant le {echeance}. ». « M'envoyer un essai » montre « avant le 01/02/2026 ». Le loyer de décembre envoyé depuis le formulaire dit « à régler avant le . » ; le même loyer préparé d'avance et parti au lancement porte la date. E-mail d'une facture de dépôt avec « Montant du dépôt (positif) » choisi dans le menu : « {depot.montant} » part tel quel. Phrase de règlement « Merci de régler {montant} avant le {echeance} » : le PDF imprime « Merci de régler {montant} avant le ». Explication du dépôt « … suite à la facture {numero} du {date} » : « suite à la facture  du ».
- Correction proposée : 1) Les quatre BuildContext : echeance = TryDate(_echeance.text, out var e) ? e.ToString("dd/MM/yyyy") : "" (même format que FactureEnvoiAuto.Contexte). 2) Menu filtré par champ : SlashAutocomplete.Attach(TMP_InputField champ, params string[] groupesEnPlus) ; les groupes à portée limitée (« Ligne montant mensuel », « Explication dépôt », et un groupe « Rappel d'impayé » pour {reste}, FactureVariables.cs:53) ne sont proposés que si le champ les nomme ; le filtre de ShowFiltered extrait en static Proposees(string recherche, string[] enPlus) pour le tester. Appelants à changer : FactureLoyerPanel.cs:295 (+ « Ligne montant mensuel »), FactureDepotPanel.cs:262, 266, 270, 274 (+ « Explication dépôt »), ReglagePanel.cs:1142-1145 et 1160-1167 (BlocModele : + « Rappel d'impayé » pour le seul modèle du rappel). Les autres appels restent tels quels. 3) ExplicationDepot.Html(…, FactureContext ctx = default) → Phrase → FactureVarResolver.Resolve(modele, loc, bat, ctx, H) ; FactureDepotPanel.cs:662 passe le ctx déjà construit ligne 642 (les appels des tests restent valides). 4) Corriger les commentaires de FactureVariables.cs (61-63, 66-69) et le test FactureVariablesTests.cs:118-131. Limite : un texte déjà enregistré qui contient {montant} ou {depot.*} hors de sa phrase continue de partir tel quel (option C). Au passage, mêmes fichiers : D6-14 (void OnDisable() => Hide(); dans SlashAutocomplete) et D6-12 (échappement de la phrase mensuelle, FacturePdfService.cs:107).
- Décision posée : Le menu « / » propose partout des variables qui ne marchent que dans une phrase précise ({montant}, {depot.montant}, {reste}…). Que doit-il faire ?
  - (recommandée) Ne proposer que ce qui est remplacé : {montant} seulement dans la phrase du montant mensuel, {depot.*} seulement dans les quatre phrases de l'explication du dépôt, {reste} seulement dans le modèle du rappel. En plus : {echeance} remplie dans les quatre formulaires et les variables « Facture » remplies dans l'explication du dépôt.
  - Tout proposer, mais dire où ça marche : Le menu affiche le groupe à côté de chaque variable (« Explication du dépôt seulement »). Moins de code, mais l'erreur reste possible.
  - Filtrer, plus un contrôle avant l'envoi : Comme la première option, et l'émission est refusée au centre si un jeton « {…} » reste dans le PDF ou l'e-mail (anciens textes, faute de frappe comme {echance}). Un peu plus de code.
- Test : EditMode Revue17Tests.D6_08_l_echeance_est_dans_le_contexte_des_quatre_formulaires (test texte avec Src, comme D2_01 : le BuildContext de chacun des quatre panneaux contient « echeance = ») ; Revue17Tests.D6_08_le_menu_ne_propose_dans_un_champ_que_ce_qui_y_est_remplace : chaque jeton de SlashAutocomplete.Proposees("") est remplacé par FactureVarResolver (sortie différente du jeton) et n'est ni {montant}, ni {depot.*}, ni {reste} ; Proposees("", "Ligne montant mensuel") contient {montant} ; ExplicationDepot.Html(…, ctx) avec la phrase « {numero} {echeance} {depot.montant} » ne laisse aucun « { ». Mettre à jour FactureVariablesTests.Les_variables_a_portee_limitee_sont_annoncees_comme_telles.
- Interactions : D6-05 (l'échéance est garantie lisible à l'émission). D6-12 et D6-14 (mêmes fichiers, à faire dans le même passage). {periode} n'a pas la même valeur selon le chemin : formulaire « Novembre 2026 » ou « charges 2026 », envoi au lancement « Loyer Novembre 2026 » (FactureEnvoiAuto.cs:544, libellé du suivi) : même cause (deux contextes), à aligner si l'on veut le même texte partout. {bat.*} vides dans le rappel et dans l'essai (bat = null : FactureRappelService.cs:58-59, ReglagePanel.cs:1178). D6-03 (bas de page non résolu, hors menu) : sans lien.
- **Vérification : à corriger**
  - Corrections : Le constat est exact dans le code actuel.
- Les quatre BuildContext ne posent pas l'échéance : FactureLoyerPanel.cs:1017-1031, FactureRegulPanel.cs:684-700, FactureRefacPanel.cs:590-604, FactureDepotPanel.cs:625-636. L'envoi au lancement et l'essai la posent (FactureEnvoiAuto.cs:549, ReglagePanel.cs:1182).
- Le menu « / » propose tout le catalogue et n'affiche que le libellé et le jeton, jamais le groupe (SlashAutocomplete.cs:91-93, 107).
- {montant} n'est remplacé qu'en FacturePdfService.cs:107, {depot.*} qu'en ExplicationDepot.cs:114-118, et {reste} ne reçoit sa vraie valeur qu'en FactureRappelService.cs:57.
- ExplicationDepot.cs:112 résout avec un contexte vide (default).
- Les gabarits HTML ne contiennent aucun jeton.
- Rien de cela n'a été corrigé par les lots 1, 2a ou 2b.

À corriger dans la proposition :

1) Le test texte « le BuildContext contient « echeance = » » ne prouve rien. Chaque panneau contient déjà « _echeance = Labeled(… » (FactureLoyerPanel.cs:305, FactureRegulPanel.cs:237, FactureRefacPanel.cs:234, FactureDepotPanel.cs:309) : il passerait sans correction. Chercher une chaîne propre au correctif (ex. « echeance = TryDate(_echeance.text »), ou mieux, extraire une petite aide statique testée en comportement.

2) Il y a 22 appels SlashAutocomplete.Attach, pas 21. ReglagePanel.cs:1167 en pose deux par modèle, pour les quatre modèles avec variables (1130-1145). Il faut donc donner un paramètre de groupes à BlocModele (1160-1175), et seul l'appel du rappel (1142) passe « Rappel d'impayé ».

3) Changer le groupe de {reste} ne casse pas FactureVariablesTests. {reste} est résolu, donc Chaque_variable_proposee_par_le_menu_est_reellement_resolue passe. Le test 118-131 ne parcourt que GroupesResolusAilleurs. Il suffit de mettre à jour les commentaires, comme proposé.

Le reste est faisable :
- Un paramètre « params string[] » garde compatibles les appels existants.
- ExplicationDepot.Html(…, FactureContext ctx = default) en 8e paramètre : les 19 appels des tests compilent toujours (ExplicationDepotTests, Revue16cTests.cs:555-556 et 1040).
- FactureDepotPanel.cs:662 dispose déjà du contexte construit ligne 642, avec le bon numéro : _numeroEmis est posé ligne 819, avant DocumentAEmettre (820).
- Aucun champ enregistré, rien côté serveur.
  - Risques : - {periode} change selon le chemin : « Novembre 2026 » au formulaire, rec.libelle au lancement (« Loyer Novembre 2026 — corrigée(1) », FactureEnvoiAuto.cs:544). Un modèle « Loyer {periode} » donnerait « Loyer Loyer Novembre 2026 » au lancement.
- Les textes déjà enregistrés qui contiennent {montant} ou {depot.*} hors de leur phrase partent toujours tels quels (option 1). Seule l'option 3 les attraperait.
- {bat.*} restent vides dans le rappel et dans l'essai (bat = null, FactureRappelService.cs:58-59), mais le menu du modèle du rappel continuera de les proposer.
  - Décision manquante relevée : Aucune : la question est posée, avec ses options. Préciser seulement, dans l'option 1, que {reste} disparaît aussi du menu des e-mails de facture, où il valait « 0,00 € ».

### D6-09 · Supprimer une charge déjà facturée ne demande qu'une confirmation : la refacturation ne se corrige plus et une régularisation corrigée perd la charge
- État au 05/11 : présent · effort S · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Achat/InvestissementListPanel.cs:276-280 — bouton « Supprimer » de chaque charge (Historique des charges)
  - Assets/Script/Achat/InvestissementListPanel.cs:285-298 — DemanderSuppressionCharge : confirmation simple, charges.RemoveAll, toast « Annuler » ; les facturations de la charge ne sont pas lues
  - Assets/Script/Facturation/ChargeBatiment.cs:46-55 et 71 — facturations par locataire, EstFactureePour, EstFacturee : l'information existe
  - Assets/Script/Facturation/FactureRefacPanel.cs:415-427 — ChargesDeLaFacture : « charge supprimée : ignorée »
  - Assets/Script/Facturation/FactureRefacPanel.cs:451-466 — Regroupement : la ligne refac-<id> orpheline bloque la correction (« Cette facture couvre aussi 1 autre(s) charge(s) », impossible à cocher) ; avec une seule charge : « Coche au moins une charge à refacturer. »
  - Assets/Script/Facturation/FactureRegulPanel.cs:498-515 — ChargesDeRegul relit les charges du bâtiment : la correction perd la quote-part ; 1008-1010 (parts par liste) ; 521-536 (la ligne « déjà refacturée » disparaît)
  - Assets/Script/Facturation/FacturationSuivi.cs:142-154 — la ligne refac reste au suivi et en Créances ; 680-684 — « Payé » sur cette refacturation ne touche plus aucune charge
  - Chemin voisin : Assets/Script/Facturation/ChargePanel.cs:180 (« Réglée pour tous » cochable sur une charge facturée) + FactureRegulPanel.cs:506 (c.paye écarte aussi une charge couverte : la correction de la régul la perd)
  - Chemin voisin : Assets/Script/Facturation/ChargePanel.cs:488-501 — date, liste et locataires d'une charge facturée restent modifiables : la correction suit (voulu pour corriger un montant), mais une autre année ou une autre liste la fait sortir de sa régul
  - Précédents de refus : Assets/Script/Facturation/ReglagePanel.cs:797-807 et 816-826 (RIB ou entête utilisé : refus qui nomme les locataires, décision du 01/11) ; 977-993 (liste de charges encore due : refus)
- Problème : La suppression ne regarde pas si la charge est portée sur une facture. Les lignes du suivi restent (montant, PDF, Créances), mais tout ce qui reconstruit la facture à partir des charges la perd : la correction d'une refacturation devient impossible, celle d'une régularisation sort sans la quote-part. Si la régul était payée, la correction à la baisse fait même apparaître un faux trop-perçu « À rembourser » (BN17).
- Scénario : Refacturation « Jardin 2026 » de 480 € envoyée à Dupont, en attente de paiement. L'utilisatrice supprime « Jardin 2026 » dans l'Historique des charges (une seule confirmation). Créances réclame toujours 480 €, mais « Corriger » ouvre un formulaire sans charge : « Coche au moins une charge à refacturer. » ; si la facture portait « Jardin » et « Ménage » : « Cette facture couvre aussi 1 autre(s) charge(s) », impossible à cocher. Régularisation 2026 émise et payée, corrigée plus tard pour une provision : elle ressort sans la quote-part de la charge supprimée, « corrigée(1) » au mauvais solde, avec un faux « À rembourser ».
- Correction proposée : Option recommandée : ChargeBatiment.cs, ajouter public static string RefusSuppression(ChargeBatiment c, IEnumerable<Locataire> locataires) → null si aucun locataire encore présent dans le bâtiment n'a c.EstFactureePour(l.id) (une facturation d'un locataire supprimé ne bloque pas), sinon « « Jardin 2026 » est sur une facture déjà émise (Dupont) : elle ne peut pas être supprimée, cette facture ne pourrait plus être corrigée. Elle reste dans l'historique ; pour changer ce qui a été facturé, corrigez la facture depuis le suivi. ». InvestissementListPanel.DemanderSuppressionCharge (285), avant la confirmation : var refus = ChargeBatiment.RefusSuppression(ch, _batiment.listLocataire); if (refus != null) { ConfirmDialog.Erreur(refus); return; } ; sinon la confirmation et le toast « Annuler » actuels. Seul appelant : InvestissementListPanel.cs:279 (aucune autre suppression de charge dans le code). Chemin voisin, même lot, une ligne : FactureRegulPanel.cs:506 if (c == null || c.paye) continue; → if (c == null) continue; (AFacturerPour écarte déjà une charge réglée non couverte : seule une charge couverte par la régul émise revient dans sa correction).
- Décision posée : Une charge déjà portée sur une facture (refacturation ou régularisation) peut-elle encore être supprimée de l'historique des charges ?
  - (recommandée) Non, refus au centre : Le message nomme les locataires concernés, comme pour un RIB ou un entête utilisé. La charge reste dans l'historique ; une charge jamais facturée se supprime comme aujourd'hui (confirmation, puis « Annuler »).
  - Non tant que ce n'est pas payé : Comme pour une liste de charges : refus tant qu'une facture qui la porte n'est pas payée, suppression permise ensuite. Risque : corriger plus tard une régul payée donne un faux « À rembourser ».
  - Oui, après un avertissement : Une confirmation qui nomme les factures et dit qu'elles ne pourront plus être corrigées correctement. La perte reste possible.
  - La masquer au lieu de la supprimer : Elle disparaît de l'historique et des nouvelles factures, mais les corrections la retrouvent. Demande un nouveau champ enregistré (modèle des données 8) et plus de code.
- Test : EditMode Revue17Tests.D6_09_une_charge_facturee_ne_se_supprime_pas : charge facturée (MarquerFacturee) à « Dupont » présent dans la liste → refus qui contient « Dupont » ; charge non facturée → null ; charge facturée à un locataire absent du bâtiment → null. Et Revue17Tests.D6_09_une_charge_reglee_pour_tous_reste_dans_la_correction_de_sa_regul : régul 2026 émise pour A (ligne avec pdfPath, statut « Envoye »), charge 2026 facturée à A puis paye = true → FactureRegulPanel.ChargesDeRegul(bat, A, 2026) la contient. (L'option « masquer » imposerait aussi de monter DonneesCloud.Modele et de recopier l'empreinte de D2_01.)
- Interactions : D7a-09 (supprimer un locataire qui a des factures efface son suivi ; ses facturations restent sur les charges : d'où le filtre sur les locataires présents). D10-12 (un refus passe par ConfirmDialog.Erreur, pas par une confirmation à deux boutons ; le refus de suppression d'une liste, ReglagePanel.cs:986-991, est à aligner). D4-03 / BN17 (faux trop-perçu après correction). D4-12 (Regroupement : une facture payée ne reçoit plus de charge). D5-02 et D5-06 (rectificative à Super PDP). D6-01 / BL4 / BL8 (parts) : sans effet. À vérifier, hors D6-09 : la suppression ne réserve pas le bâtiment sur le cloud, contrairement à l'ouverture du formulaire de charge (ChargePanel.cs:78).
- **Vérification : à corriger**
  - Corrections : Le constat est exact dans le code actuel.
- InvestissementListPanel.cs:276-280 et 285-298 : une confirmation simple, puis RemoveAll et le toast « Annuler ». C'est la seule suppression de charge du code (le seul RemoveAll sur les charges est InvestissementListPanel.cs:290).
- ChargeBatiment.cs:46-55 et 71 : l'information existe. Les « facturations » ne sont jamais retirées : aucun code ne les vide.
- Refacturation : FactureRefacPanel.cs:415-427 ignore la charge supprimée ; 451-466 renvoie « Cette facture couvre aussi 1 autre(s) charge(s) » ou « Coche au moins une charge ».
- Régularisation : FactureRegulPanel.cs:498-515 et 1008-1010 relisent les charges du bâtiment.
- Suivi : FacturationSuivi.cs:142-154 et 680-684.
- Le faux « À rembourser » est plausible : ApresCorrection, FacturationSuivi.cs:508-519.
- Les lots n'y ont rien changé.

La proposition principale est faisable et complète : ChargeBatiment.RefusSuppression, appelée avant la confirmation, refus au centre. Un seul appelant (InvestissementListPanel.cs:279), aucun test existant touché, rien côté serveur ni modèle.

À corriger :

1) Le changement de FactureRegulPanel.cs:506 (retirer « c.paye ») n'est pas une simple ligne : il change le sens de la case « Réglée pour tous (plus proposée à la refacturation ni à la régularisation) » (ChargePanel.cs:180) pour une charge déjà portée par une régul émise.
- Aujourd'hui, la correction de la régul la retire, comme le promet le libellé. Après le changement, elle la garde.
- C'est cohérent avec la refacturation, qui la remet déjà dans sa correction sans regarder « paye » (FactureRefacPanel.cs:389-391).
- Aucun test ne casse : ChargesParLocataireTests et ListesChargesTests n'ont pas ce cas.
- Mais c'est une décision de comportement : la poser, ou la remplacer par « case grisée dès que la charge est facturée (EstFacturee) », dans la logique du refus de suppression.

2) Citation : le refus pour un entête utilisé est ReglagePanel.cs:896-904. Les lignes 816-826 sont l'aide LocatairesUtilisant, qui lit les FactureInfo et non les facturations des charges.

3) Le message dit « corrigez la facture depuis le suivi ». Pour une charge saisie par erreur, la seule voie sera de mettre son coût à 0 (ou son montant HT dans la correction de la refacturation), puis de corriger la facture. Le message doit le dire.
  - Risques : - listLocataire contient aussi les locataires archivés (BatimentPrefab.cs:41). Une charge facturée à un ancien locataire archivé ne pourra donc plus jamais être supprimée.
- Les facturations ne sont jamais retirées, même si la ligne du suivi est remise « à faire » (SetStatut ""). Une charge peut rester non supprimable alors qu'aucune facture ne la porte plus (cas rare).
- Restent ouverts, hors de ce correctif :
  - modifier l'année, la liste ou les locataires d'une charge facturée la fait sortir de sa régul (ChargePanel.cs:488-501) ;
  - la suppression ne réserve pas le bâtiment sur le cloud, alors que le formulaire de charge le fait (ChargePanel.cs:78).
  - Décision manquante relevée : - La question posée ne couvre pas le changement de FactureRegulPanel.cs:506. À demander : une charge « Réglée pour tous » déjà portée par une régul émise reste-t-elle dans la correction de cette régul (comme en refacturation), ou la case doit-elle être grisée dès que la charge est facturée ?
- L'option recommandée doit aussi dire que les locataires archivés comptent : une telle charge reste à vie dans l'historique.

### AVENANT-PERIODICITE · Avenant de périodicité daté (thème 2c) : changer de périodicité en cours de bail sans que les loyers se marchent dessus
- État au 05/11 : présent · effort L · serveur : aucun (le format des clés ne change pas ; quelle que soit la conception, ne jamais renommer une clé existante : reserver_envoi, Serveur/etape16c4_supabase.sql:47-49, la cherche telle qu'elle est dans la fiche) · nouveau champ : oui
- Lieux :
  - Assets/Script/Facturation/FacturationSuivi.cs:98-112 — Lignes : une ligne par numéro de période de la périodicité ACTUELLE, clé « loyer-{année}-P{n} », pour chaque année affichée
  - Assets/Script/Facturation/FacturationSuivi.cs:196-220 — Fusion : la ligne planifiée P{n} reprend la ligne stockée de même clé, quelle que soit sa périodicité (D4-04 : garde son libellé et son échéance)
  - Assets/Script/Facturation/FacturationSuivi.cs:156-169 — lignes émises non planifiées (P5 à P12 d'un ancien mensuel) ajoutées à part ; 171-177 reprise (par date)
  - Assets/Script/Facturation/FacturationSuivi.cs:784-803 et 876-917 — EcheanceLoyer, PeriodeBornes, MoisEcheance, MoisChoisis : périodicité et mois cochés actuels, sans date
  - Assets/Script/Facturation/FacturationSuivi.cs:765-774 et 897-906 — PeriodeIndex (mois → numéro), même périodicité actuelle
  - Assets/Script/Facturation/FacturationSuivi.cs:833-845 — EcheanceAttendue : relit la clé et recalcule l'échéance dans la périodicité actuelle
  - Assets/Script/Facturation/FacturationSuivi.cs:919-943 — PeriodeLibelle, EcheancePeriode, LibellePeriode
  - Assets/Script/Facturation/FactureLoyerPanel.cs:464, 470, 678 — clé construite avec le numéro choisi ; Preparer et ligne.periodicite = périodicité actuelle
  - Assets/Script/Facturation/FactureLoyerPanel.cs:137-163 — AppliquerLigneCiblee : relit « P{n} » et le place dans les options de la périodicité actuelle
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1036-1087 — PeriodeOptions, DefaultPeriodeIndex, PeriodeLibelle (mêmes options quelle que soit l'année) ; 260-266 écouteurs période et année
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1102-1122 et 1220-1226 — DefaultEcheance et PeriodePartie relisent « loyer-{année}-P{n} »
  - Assets/Script/Facturation/FactureLoyerPanel.cs:1153-1186 — RefreshTitulaire, DefaultLoyer, PeriodeChoisie, LigneLoyer (bornes et montant par numéro)
  - Assets/Script/Facturation/FactureLoyerPanel.cs:404-405, 867-870, 913-920, 1305-1308 — montant mensuel, période mémorisée (factureLoyer.moisPeriode), ligne « mensuel »
  - Assets/Script/Facturation/FactureEmission.cs:59 et 90-100 — refus D4-04 (même numéro, autre périodicité) et son conseil « au 1er janvier »
  - Assets/Script/Facturation/FacturationAlertes.cs:44-49 et 150-161 — prochaine échéance et DejaTraite(« loyer-{année}-P{n} ») dans la périodicité actuelle
  - Assets/Script/Locataire/DepartLocataire.cs:127-134, 164-174, 346-360, 363-369 — RaisonsNonArchivable, LoyerACorriger, PeutAnnuler, MontantSansDepart, LirePeriode
  - Assets/Script/Locataire/CessionBail.cs:120-127 et Assets/Script/Facturation/FactureEnvoiAuto.cs:72-80 — TitulaireLigne lit la clé puis PeriodeBornes actuelles ; Destinataire de l'envoi au lancement
  - Assets/Script/Facturation/FactureElectronique.cs:180 et 186-190 — ReferencesAvoir (« loyer-{année}- ») et AnneeDeCle : indépendants de ce qui suit l'année
  - Assets/Script/RevisionLoyer/Loyers.cs:118-170 — MontantPeriode (n = périodicité actuelle), PeriodeFacturable, PeriodesFacturees, EnFranchise, EstDebutPeriode (paliers : 209, 234, 256)
  - Assets/Script/RevisionLoyer/Loyers.cs:84-95 et 299-344 — avenant de loyer existant (historiqueLoyers + debutConditionsISO, prorata au jour) : le modèle à suivre
  - Assets/Script/RevisionLoyer/LoyerHistoryService.cs:112-118 — loyer d'une année = somme de ses périodes (périodicité actuelle)
  - Assets/Script/RevisionLoyer/RevisionPanel.cs:1582-1589, 1659, 1699-1701, 1740-1741 — Gestion du loyer : périodicité et mois cochés écrits par « Modifier », sans date ; 1646-1657 et 973-975 : bloc d'avenant de loyer (seulement pour « Réinitialiser »)
  - Assets/Script/RevisionLoyer/RevisionPanel.cs:1276-1311 et 1743-1757 — reprise de facturation : options « {année}-P{n} » dans la périodicité à l'écran, enregistrée comme une date
  - Assets/Script/Facturation/FactureRegulPanel.cs:633-634 et 1008-1010 — provisions de la régul = provision de la fiche × PeriodesFacturees (périodicité actuelle)
  - Assets/Script/Facturation/LocataireSuiviInline.cs:221 et 259-272 — années affichées (−3 à +1) et pastille d'année, toutes planifiées avec la périodicité actuelle
  - Assets/Script/Locataire/Locataire.cs:47, 60, 96-101 ; Assets/Script/Facturation/FactureEtat.cs:24-25 ; Assets/Script/Facturation/FactureInfo.cs:109-110 — périodicité, mois cochés, historique du loyer ; periodicite sur la ligne (D4-04) ; période mémorisée
  - Serveur/etape16c4_supabase.sql:47-49 — reserver_envoi cherche la ligne par sa clé dans la fiche (f->>'key') : une clé existante ne doit jamais être renommée
  - Assets/Editor/Tests/Revue17Tests.cs:54-59 et 471-484 ; SurfacesEtEcheanceTests.cs:124-136 ; RevisionLoyerTests.cs:263, 280 ; DepartLocataireTests.cs:292-296 ; FacturationSuiviTests.cs:293-331 ; FactureElectroniqueTests.cs:151-168 — tests qui lisent ces clés et numéros
- Problème : La périodicité n'a pas de date : « Modifier » dans Gestion du loyer remplace la périodicité (et les mois cochés) pour tout le bail, passé compris, et toutes les années affichées sont replanifiées avec elle. Or un loyer est repéré par « loyer-{année}-P{numéro} », numéro compté dans la périodicité actuelle : après un changement, le même numéro désigne deux périodes. D4-04 (04/11) n'a protégé que les factures déjà émises (libellé gardé, correction refusée si la périodicité stockée diffère). Restent, dans le code actuel : (1) mensuel → trimestriel en cours d'année : les numéros 1 à 4 sont pris par janvier à avril ; juillet-décembre n'ont aucune ligne, ne se facturent pas (refus D4-04), et l'alerte « Loyer à préparer » se tait, car DejaTraite(« P3 ») voit mars déjà envoyé ; (2) trimestriel → mensuel, même au 1er janvier comme le conseille le refus D4-04 : chaque année affichée replanifiée en mensuel montre mai à décembre « À faire » alors que les trimestres sont facturés, pastille d'année ambre, et un départ dans une de ces années bloque l'archivage (« Loyer Mai … à facturer ») ; (3) une facture de l'ancienne périodicité ne se corrige plus (le formulaire l'ouvre comme « 3e Trimestre » : refus ; sans periodicite sur la ligne, émise avant le 04/11, c'est la rectificative fausse d'origine) ; (4) un départ compare mars (mensuel) au montant du 3e trimestre : « à corriger » à tort, « Annuler le départ » refusé ; (5) faux « (modalités : 05/10 — refaire ?) » sur un loyer de novembre préparé en mensuel, titulaire d'une cession lu sur de mauvaises bornes ; (6) la régul compte provision de la fiche × périodes de la périodicité actuelle.
- Scénario : Bail à 12 000 € HT par an, mensuel, loyers de janvier à juin 2026 émis ; avenant : trimestriel à compter du 1er juillet 2026. Aujourd'hui (« Modifier », sans date) : le suivi 2026 montre janvier à juin et plus rien ; « Facturer le loyer » › « 3e Trimestre » → refus au centre (P3 = mars) ; aucune alerte ne rappelle juillet ni octobre. Sens inverse : trimestriel depuis 2020, mensuel au 1er janvier 2027 → 2023 à 2026 affichent chacune « Mai … Décembre · À faire ». Avec la conception recommandée : on saisit « à compter du 01/07/2026 » ; 2026 = janvier à juin (P1 à P6, inchangés), puis « 3e trimestre 2026 » (P7) et « 4e trimestre 2026 » (P8) à 3 000 € HT ; 2025 et avant restent mensuels, 2027 est trimestriel. Avec « à compter du 01/05/2026 » (trimestres janvier, avril, juillet, octobre) : avril reste mensuel, puis « 2e trimestre 2026 (mai à juin) » à 2 010,99 € HT (3 000 × 61/91 jours), échéance au jour de demande de mai, puis 3e et 4e trimestres. Trimestriel → mensuel au 01/04/2026 : « 1er trimestre » (P1) puis avril à décembre (P2 à P10) ; février et mars ne sont jamais proposés.
- Correction proposée : Les trois conceptions ont le MÊME écran ; elles ne diffèrent que par le repère des loyers. Base commune (indispensable : sans dates, aucune ne sait planifier les mois d'une périodicité plus courte ni les années d'avant) :
• Modèle : Locataire.historiquePeriodicites = liste de PeriodiciteAnterieure { finISO, periodicite, mois } (périodicité et mois cochés valables jusqu'à finISO inclus), comme historiqueLoyers pour le loyer ; vide = la périodicité actuelle depuis toujours (rien ne change pour les fiches sans avenant). DonneesCloud.Modele 7 → 8, empreinte de Revue17Tests.D2_01 recopiée. Rien sur FactureEtat (CopieDe inchangé).
• Gestion du loyer (RevisionPanel) : quand la périodicité ou les mois cochés changent et qu'un loyer a déjà été touché, un bloc « Nouvelle périodicité à compter du » (même bloc que l'avenant de loyer, BlocCaseDate, 973-975), prérempli au lendemain de la dernière période facturée ; refus au centre (ConfirmDialog.Erreur, fenêtre ouverte) selon AVENANT-PERIODICITE-DATE ; puis Loyers.EnregistrerAvenantPeriodicite(loc, effet) AVANT EcrireModalites (1659), comme EnregistrerAvenant (recoupe un historique antidaté, ignore une tranche vide). Si l'avenant de loyer est coché, sa date préremplit celle-ci.
• FacturationSuivi.Plan(loc, année) : les périodes de l'année. Historique vide : exactement le calcul d'aujourd'hui (n périodes, mêmes bornes, mêmes mois cochés, même cas des mois incomplets). Sinon : pour chaque tranche [du, au] et sa périodicité, ses périodes naturelles (débutant en N−1 ou N) qui croisent la tranche, recoupées à la tranche, gardées si leur début recoupé tombe dans l'année, triées par début. Chaque période : début et fin facturés, début et fin naturels, périodicité, son numéro dans SA périodicité (libellé). Période recoupée : « Loyer 2e trimestre 2026 (mai à juin) », échéance au jour de demande de son premier mois. Pas de cache (12 périodes au plus).
• Prorata : Loyers.MontantPeriode compte les jours recoupés, divise par le nombre de périodes de SA périodicité et par la durée naturelle : la période à cheval est au prorata au jour, comme une franchise, un départ ou un avenant de loyer. Une période partielle appelle la provision entière (règle actuelle, la régul corrige).
• Mois déjà couverts : jamais proposés (la date ne peut pas passer avant une période déjà facturée) ; les années d'avant sont planifiées dans leur périodicité.

A — Historique daté, plan par tranches, repère inchangé (RECOMMANDÉE). « loyer-{année}-P{n} » avec n = rang dans Plan(loc, année). Année d'une seule périodicité : rangs identiques à aujourd'hui, donc toutes les clés existantes gardent leur sens ; année du changement : l'ancienne périodicité garde 1..k, la nouvelle continue (3e trimestre = P7). Les fonctions (loc, année, période) gardent leur signature et lisent le plan : EcheanceLoyer, PeriodeBornes (hors plan : calcul actuel, pour les vieilles clés), PeriodeIndex(loc, année, mois) (année ajoutée ; appelants FacturationAlertes:45, RevisionPanel:1300), MoisEcheance(loc, p) remplacé par le plan (appelants PeriodeBornes, Loyers.EstDebutPeriode, FactureLoyerPanel.DefaultPeriodeIndex), Lignes (boucle sur le plan, libellé du plan), Loyers.MontantPeriode / PeriodesFacturees / EstDebutPeriode, LoyerHistoryService:115, FacturationAlertes:150-161 (échéance et rang tirés du plan). FactureLoyerPanel : options de « Période facturée » = plan de l'année saisie, reconstruites quand l'année change (_annee, 266) ; libellé, montant mensuel (mois de la période), Preparer et ligne.periodicite = périodicité de la période. Lecteurs inchangés, justes dès que le plan est lu : DepartLocataire.LirePeriode / LoyerACorriger / PeutAnnuler, Cessions.TitulaireLigne → FactureEnvoiAuto.Destinataire, EcheanceAttendue, AppliquerLigneCiblee, FactureElectronique.AnneeDeCle / ReferencesAvoir. Régul : provisions imprimées (AVENANT-PERIODICITE-PROVISION). FactureEmission.Preparer : refus D4-04 gardé comme filet, conseil « changez-la avec sa date d'effet (Gestion du loyer) » au lieu de « au 1er janvier ». Reprise : options d'une année d'avant le changement lues sur le plan (ou l'option porte directement sa date d'échéance). Migration : aucune ; fiches de test déjà changées sans date (BN11) : remettre l'ancienne périodicité puis refaire le changement daté. Risques : le rang dépend de l'historique, d'où la règle de date (aucune date avant une période touchée, donc aucun rang émis ne bouge) ; un lecteur oublié qui recalcule avec loc.periodiciteLoyer (Loyers:127/148/166, LoyerHistoryService:115, FacturationAlertes:152, FactureLoyerPanel:404/1061) ; une ancienne version (« Plus tard ») ignore l'historique (affichage d'aujourd'hui), ses écritures sont refusées (modèle 8). Fichiers : Locataire, DonneesCloud, FacturationSuivi, Loyers, LoyerHistoryService, FacturationAlertes, FactureLoyerPanel, RevisionPanel, FactureRegulPanel, FactureEmission (message), LoyerSummaryUI (récap « depuis le … », facultatif), tests. Effort L, le plus petit des trois.

B — Repère par mois de début pour les nouvelles lignes (« loyer-2026-M07 »). Même base, même écran. Le repère dit le mois et ne bouge pas si l'historique change. Mais les clés « P{n} » existantes ne se renomment pas sans risque (reserver_envoi les cherche telles quelles côté serveur ; il faudrait réécrire toutes les fiches au lancement, verrou par verrou ; une ancienne version les lirait mal) : deux formats pour toujours. En plus de A : 5 constructeurs (FacturationSuivi:102, FactureLoyerPanel:464/1115/1224, FacturationAlertes:49), 4 lecteurs (FactureLoyerPanel:150-156, FacturationSuivi:838-841, DepartLocataire:363-369, CessionBail:122-124) et la recherche d'une période parmi les lignes stockées (Fusion, DejaTraite, EstDejaEmise, PeriodePartie, DefaultEcheance : une vingtaine de « x.key == key ») qui doit reconnaître l'ancien format ; factureLoyer.moisPeriode devient un mois. Risque : une comparaison oubliée rate une vieille ligne (double numéro, ligne « À faire » en double). Modèle : historique (8). Migration : aucune. Effort L, le plus gros.

C — Lettre de périodicité l'année du changement (« loyer-2026-T3 »). Même base, même écran. P{n} = période n de la périodicité en vigueur au 1er janvier de l'année (aujourd'hui pour toutes les années sans changement) ; l'autre périodicité de l'année porte sa lettre (M, T, S, A) et son numéro naturel. Aucune clé existante ne change ; deux formats seulement les années de changement. Mais une quinzaine de fonctions qui prennent un numéro de période (EcheanceLoyer, PeriodeBornes, MontantPeriode, EnFranchise, PeriodeFacturable, LirePeriode, TitulaireLigne, le formulaire…) doivent aussi prendre la lettre, et le sens de « P » dépend de l'historique (une date déplacée par-dessus le 1er janvier le change). Modèle : historique (8). Migration : aucune. Effort L.

Recommandation : A, la seule qui ne touche ni au format des clés ni aux signatures, avec la même protection ; B et C n'apportent qu'un repère plus lisible, au prix de deux formats.
- Décision posée : Changement de périodicité en cours de bail (avenant daté) : l'écran sera le même dans les trois cas (date d'effet dans Gestion du loyer, périodes d'avant gardées, période à cheval au prorata, mois déjà facturés jamais reproposés). Quelle façon de repérer les loyers retenir ?
  - (recommandée) A — Repère inchangé, numéros dans l'ordre de l'année : Les loyers existants gardent leur repère ; l'année du changement, les nouvelles périodes prennent la suite (le 3e trimestre devient la 7e période de 2026, invisible à l'écran). Aucune reprise des données, rien côté serveur. Le plus petit chantier (L).
  - B — Repère par mois de début (« M07 ») pour les nouvelles factures : Repère lisible et stable même si une date change plus tard ; mais les anciens repères restent : deux formats à lire partout, risque d'oubli. Le plus gros chantier.
  - C — Lettre de la périodicité l'année du changement (« T3 ») : Numéros naturels (T3 = 3e trimestre), deux formats seulement les années de changement ; toutes les fonctions qui prennent un numéro de période doivent aussi prendre la lettre (L).
- Test : EditMode, nouveau fichier AvenantPeriodiciteTests : Mensuel_puis_trimestriel_au_1er_juillet (historique { fin 30/06/2026, mensuel }, actuel trimestriel → Plan(2026) = janvier…juin puis 3e et 4e trimestres (8 périodes), Plan(2025) = 12, Plan(2027) = 4 ; Lignes(2026) garde « loyer-2026-P3 » = mars émis et propose « loyer-2026-P7 » = 3e trimestre ; Preparer(loc, « loyer-2026-P7 », …, « trimestriel »).Refus nul ; MontantPeriode(loc, 2026, 7) = 3 000) ; Trimestriel_puis_mensuel_ne_propose_pas_les_mois_couverts (historique { fin 31/12/2026, trimestriel } → Lignes(2025) : 4 loyers, aucun « Mai 2025 ») ; La_periode_a_cheval_est_au_prorata (au 01/05/2026 : période 5 = 2e trimestre du 01/05 au 30/06, 2 010,99 €, échéance en mai) ; L_alerte_suit_le_trimestre (mars et avril payés, 20/09/2026 → alerte pour l'échéance d'octobre, DejaTraite sur P8 faux) ; Le_depart_compare_mars_a_mars (LoyerACorriger faux) ; sans historique, SurfacesEtEcheanceTests, RevisionLoyerTests, DepartLocataireTests et Revue17Tests.D4_04 restent verts sans retouche ; Revue17Tests.D2_01 : « 8:… ». Ensuite en Play, un parcours BP (locataire de test, factures à agathelimouzy@gmail.com).
- Interactions : D4-04 (fait) : FactureEtat.periodicite et le refus de Preparer restent comme filet pour les lignes d'avant ; leur conseil « au 1er janvier » est faux pour trimestriel → mensuel et change ; BN11 à réécrire (« Corriger » mars redevient possible après un changement daté). D6-04 (à faire) : « Facturer le loyer » doit ouvrir la prochaine période à facturer → la chercher dans Plan ; faire après ou avec. D7a-01 (fait) : LoyerACorriger et PeutAnnuler deviennent justes dès que MontantPeriode lit le plan. D7a-03 / D4-01 : TitulaireLigne lit les bornes du plan ; la limite D4-01 (facture préparée avant la saisie de la cession) ne change pas. D4-02 (à faire) : la règle de date lit numéro, PDF et état stocké, pas EtatDe. D6-07 (fait) : provisions imprimées, base de la régul de l'année du changement ; D6-16 (à faire) au même endroit. D5-01 : la clé Super PDP vient du numéro, pas de la clé du suivi. D2-01 : modèle 8 et empreinte. Paliers : un palier finit la veille d'un début de période du plan (EstDebutPeriode). Avenant de loyer : date indépendante, préremplit celle de la périodicité. Règle d'interface : refus au centre, fenêtre rouverte avec la saisie.
- **Vérification : à corriger**
  - Corrections : Le diagnostic tient. J'ai relu tous les lieux cités dans le code actuel et les numéros de ligne sont justes : FacturationSuivi.cs:98-112, 156-177, 196-220, 765-774, 784-803, 833-845, 876-917, 919-943 ; FactureLoyerPanel.cs:137-163, 260-266, 404-405, 464, 470, 678, 867-870, 913-920, 1036-1087, 1102-1122, 1153-1186, 1220-1226, 1305-1308 ; FactureEmission.cs:59 et 90-100 ; FacturationAlertes.cs:44-49 et 150-161 ; DepartLocataire.cs:127-134, 164-174, 346-369 ; CessionBail.cs:120-127 ; FactureEnvoiAuto.cs:72-80 ; Loyers.cs:84-170 et 299-344 ; LoyerHistoryService.cs:112-118 ; RevisionPanel.cs:973-975, 1276-1311, 1582-1602, 1646-1659, 1699-1757 ; FactureRegulPanel.cs:633-634 et 1008-1010 ; LocataireSuiviInline.cs:221 et 259-272 ; etape16c4_supabase.sql:47-49, qui est la dernière définition de reserver_envoi. DonneesCloud.cs:17 donne bien Modele = 7. Les six symptômes se vérifient. Les lots 1, 2a et 2b n'ont rien corrigé au-delà de D4-04.

Ce qu'il faut corriger dans l'analyse :

1) Scénario « 3e Trimestre → refus au centre ». Le refus n'existe que si la facture de mars porte sa périodicité (FactureEmission.cs:93), c'est-à-dire si elle a été émise depuis le 04/11. Les vrais loyers de janvier à juin 2026 ont été émis avant : sans refus, on obtient la fausse rectificative de mars. Le point (3) du problème le dit, le scénario non.

2) Un symptôme manque : la ligne orpheline.
- « Corriger » sur « Loyer Mai 2026 » (clé P5) passe "5" à des options numérotées de 1 à 4 (FactureLoyerPanel.cs:155-156).
- UIDropdown.SetOptions retombe alors sur l'index 0 (UIDropdown.cs:51-52), donc « 1er Trimestre », c'est-à-dire P1 = janvier.
- Résultat : soit un refus qui cite janvier, soit, si la ligne n'a pas de périodicité, la correction de janvier.
- Même chute à l'ouverture du formulaire quand factureLoyer.moisPeriode vaut 11 (867-869).

3) AppliquerLigneCiblee est classé parmi les « lecteurs inchangés » : c'est faux. La ligne 155 appelle PeriodeOptions(_loc.periodiciteLoyer) ; elle doit prendre les options du plan de l'année lue dans la clé.

4) « Aucune signature ne change » et « tests verts sans retouche » sont faux.
- Passer à PeriodeIndex(loc, année, mois) casse SurfacesEtEcheanceTests.cs:135, qui appelle PeriodeIndex(loc, 8). Il faut garder la surcharge (loc, mois) ou retoucher le test.
- Si l'option de reprise porte directement sa date, Revue16cTests.cs:575 casse : ce test cherche le texte « FacturationSuivi.EcheanceLoyer(_loc, y, per) » dans RevisionPanel.

5) Un lecteur est oublié : la provision du formulaire Loyer (FactureLoyerPanel.cs:892-895 et 974).
- ListesCharges.Provision donne la provision par période de la périodicité ACTUELLE.
- Elle est chargée une seule fois et ne suit pas le changement de période (les écouteurs 262/266 ne la rafraîchissent pas).
- Exemple : avenant saisi en avril pour le 01/07, mai et juin restent mensuels. Facturés ensuite, ils recevraient 300 € par mois si la provision a été convertie.
- Correction : prendre la provision de la fiche ramenée à la périodicité de la période (× NbPeriodes(actuelle) / NbPeriodes(de la période)), ou garder les provisions par tranche dans PeriodiciteAnterieure. Et la rafraîchir avec la période.
- Plus mineurs : la case « montant mensuel » s'affiche et s'imprime selon la périodicité actuelle (405, 914, 920) ; la variable {loc.periodicite} imprime la périodicité actuelle (FactureVarResolver.cs:57 et 76).

6) Les options reconstruites quand l'année change (_annee, 264-266) se reconstruiraient à chaque frappe. Pendant la saisie de l'année (« 2 », « 20 », « 202 »), la période choisie retombe sur l'index 0. Ne reconstruire que pour une année à 4 chiffres, et garder la période par son mois de début.

7) La migration proposée (« remettre l'ancienne périodicité puis refaire le changement daté ») ne marche pas.
- Remettre l'ancienne périodicité est déjà un changement sur une fiche dont des loyers sont touchés : le bloc de date apparaît.
- La règle lit alors mars (P3) dans le plan trimestriel, donc comme le 3e trimestre : première date possible le 01/10.
- BN11 demandait de remettre le mensuel juste après le test (PARCOURS_TEST_CIPL.md:1559).
- Pour une vraie fiche déjà changée sans date, le problème (2) reste, et l'interface ne permet pas de reconstruire l'historique. Il faut le dire.
  - Risques : Période à cheval comptée au jour. L'année du changement ne fait plus le loyer annuel :
- mensuel → trimestriel au 01/05 : 4 000 + 2 010,99 + 6 000 = 12 010,99 € au lieu de 12 000 € ;
- trimestriel → mensuel au 01/05 : 11 989,01 €.
LoyerHistoryService.LoyerPourAnnee (rentabilité) affichera cet écart.

Paliers existants. Après mensuel → trimestriel, une fin de palier qui ne tombe pas la veille d'un début de trimestre est refusée par PoserPalier (Loyers.cs:209) dès qu'on retouche ce palier. Le montant reste juste (calcul au jour), mais la règle « jamais à cheval sur deux paliers » (Loyers.cs:9-10) ne tient plus.

Lecteurs de loc.periodiciteLoyer à reprendre en plus de la liste de risques : FactureLoyerPanel.cs:155, 405, 470, 678, 867-868, 914, 920, 1079 ; RevisionPanel.cs:1304 ; FactureVarResolver.cs:57. Tva.cs:48, LocataireFacturationFields.cs:613/711 et LoyerSummaryUI.cs:108 restent sur la périodicité actuelle (dépôt, récap) : c'est voulu.

Le bloc des lignes émises non planifiées (FacturationSuivi.cs:156-169) reste le seul filet pour les fiches déjà changées sans date.

Serveur : rien à faire, confirmé. Les clés ne changent pas, et le serveur compare seulement le numéro de modèle (etape17a_supabase.sql:76).
  - Décision manquante relevée : 1) Le choix A, B ou C ne change rien à l'écran (l'analyse le dit elle-même). Ce n'est pas une décision de comportement : A peut être tranché sans la poser. Les vraies décisions de comportement, elles, ont été tranchées sans être posées : les trois points suivants.

2) Période à cheval : prorata au jour (règle du 01/10, 2 010,99 €) ou au mois (2 000 €) ? Le calcul au mois est exact ici, puisque la date d'effet tombe toujours le 1er d'un mois.

3) Une option plus simple n'est pas proposée.
- Limiter la date d'effet au début d'une période commune aux deux périodicités (par exemple le 01/07 pour mensuel ↔ trimestriel). Plus de période recoupée, plus de libellé « (mai à juin) », plus de prorata ; une combinaison sans début commun est refusée avec explication.
- Ou bien une périodicité par année civile, changée seulement au 1er janvier. Cela corrige le problème (2) et le conseil de D4-04, pour un effort M. Un avenant en cours d'année attendrait le 1er janvier.

4) Fiches déjà changées sans date : les laisser telles quelles, ou prévoir un moyen de les réparer ?

### AVENANT-PERIODICITE-DATE · Avenant de périodicité : quand demander la date d'effet, et que faire d'une date dans une période déjà facturée
- État au 05/11 : présent · effort S · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/RevisionLoyer/RevisionPanel.cs:1582-1589 — ChangementStructurant : la périodicité n'est pas structurante, « Modifier » l'écrit sans date
  - Assets/Script/RevisionLoyer/RevisionPanel.cs:1593-1602 et 973-975 — bloc de date d'avenant (BlocCaseDate), montré seulement pour « Réinitialiser »
  - Assets/Script/RevisionLoyer/RevisionPanel.cs:1646-1657 — validation de l'avenant de loyer (après le début du bail, refus au centre) : le modèle
  - Assets/Script/RevisionLoyer/RevisionPanel.cs:1699-1701 et 1740-1741 — périodicité et mois cochés écrits
  - Assets/Script/RevisionLoyer/Loyers.cs:303-344 — EnregistrerAvenant : appelé avant d'écrire, recoupe un historique antidaté
  - Assets/Script/Facturation/FacturationSuivi.cs:445-453 et 584-604 — EstDejaEmise dépend de l'état ; SetStatut pose un état à la main sans facture (« Payé » réglé hors CIPL)
- Problème : Aujourd'hui il n'y a pas de date. Avec l'historique daté, deux choix de comportement restent : (1) quand le champ de date apparaît ; (2) une date qui tombe dans une période déjà facturée (ou marquée à la main). Soit on la refuse et on propose la fin de cette période ; soit on l'accepte et la facture à cheval devient « à corriger » au prorata, les mois qu'elle couvre ne devant pas être facturés avant. Dans la conception A, la règle protège aussi les repères : une date ne passe jamais avant une ligne touchée, donc aucun rang déjà utilisé ne change (une ligne « Payé » posée à la main sans facture compte : sinon le 2e trimestre recoupé reprendrait « Loyer Mai 2026 · Payé »).
- Scénario : Trimestriel (janvier, avril, juillet, octobre), 2e trimestre 2026 émis le 15/03 et payé. Le 20/04, avenant : mensuel à compter du 01/05/2026. (a) Refus au centre « « Loyer 2e Trimestre 2026 » est déjà facturé jusqu'au 30/06/2026 : choisissez le 1er juillet 2026 ou après », la fenêtre reste ouverte avec la saisie. (b) Accepté : la ligne du 2e trimestre affiche « à corriger : avenant du 01/05 » (1 000 € HT attendus pour avril au lieu de 3 000), mai et juin grisés « couverts par le 2e trimestre » jusqu'à la correction, puis « À faire ».
- Correction proposée : (a) Loyers.RefusAvenantPeriodicite(loc, effet) → message ou null : effet le 1er d'un mois ; après le début du bail ; aucune ligne de loyer touchée (numéro et PDF, ou état stocké non vide — pas EtatDe, que D4-02 peut défaire) dont la période, lue dans le plan d'AVANT le changement, finit à effet ou après. Loyers.PremiereDateAvenant(loc) = lendemain de la fin de la dernière de ces périodes (prérempli). RevisionPanel : bloc « Nouvelle périodicité à compter du » visible quand la périodicité ou les mois cochés diffèrent de ceux enregistrés et qu'une ligne de loyer est touchée ; sinon le changement vaut pour tout le bail, comme aujourd'hui (rien à protéger). Refus par ConfirmDialog.Erreur, fenêtre ouverte. (b) en plus : dans Lignes, une période de la nouvelle tranche couverte par une facture émise de l'ancienne prend un statut transitoire grisé « Couvert » (comme Clôturé, EstGrisee) tant que cette facture n'est pas corrigée au prorata ; LoyerACorriger étendu à l'avenant (loyerHT ≠ MontantPeriode recoupé) ; alerte et bandeau « à corriger ».
- Décision posée : Avenant de périodicité : quand faut-il demander la date d'effet, et que faire si elle tombe dans une période déjà facturée ?
  - (recommandée) Date demandée dès qu'un loyer a été facturé, refusée dans une période déjà facturée : Préremplie au lendemain de la dernière période facturée ; une date plus tôt est refusée au centre avec la première date possible. La période à cheval n'est au prorata que si elle n'est pas encore facturée. Simple, sans risque de double facturation.
  - Date acceptée dans une période facturée, qui passe « à corriger » : La facture à cheval se corrige au prorata, comme pour un départ ; les mois qu'elle couvre restent grisés jusque-là. Plus juste pour un avenant rétroactif, mais plus de code et un écran plus chargé.
  - Date toujours demandée dès que le loyer est initialisé, avec la règle du refus : Même sans aucune facture (création d'un locataire, reprise) : une saisie de plus, inutile tant qu'aucun loyer n'est parti.
- Test : EditMode, AvenantPeriodiciteTests : Avenant_refuse_dans_une_periode_deja_facturee (2e trimestre émis, effet 01/05/2026 → message avec « 30/06/2026 » et « 1er juillet » ; 01/07/2026 → null ; 15/07/2026 → refus « 1er du mois ») ; La_date_proposee_suit_la_derniere_periode_facturee (PremiereDateAvenant = 01/07/2026) ; Sans_loyer_touche_pas_de_date (aide DateAvenantRequise faux) ; Un_paye_pose_a_la_main_compte (ligne mai « Paye » sans numéro → refus au 01/05) ; Une_ligne_de_emise_compte_encore (numéro et PDF, statut vide → refus).
- Interactions : Partie de AVENANT-PERIODICITE (garantit, en conception A, qu'aucun rang déjà utilisé ne bouge). D4-02 (à faire) : la règle lit numéro, PDF et état stocké. D7a-01 (fait) : l'option « à corriger » réutilise LoyerACorriger. Règle d'interface : refus au centre, formulaire rouvert avec la saisie.
- **Vérification : à corriger**
  - Corrections : Les lieux cités sont confirmés : RevisionPanel.cs:1582-1589 (la périodicité n'est pas structurante), 1593-1602 et 973-975 (bloc d'avenant seulement pour « Réinitialiser »), 1646-1657, 1699-1701, 1740-1741 ; Loyers.cs:303-344 ; FacturationSuivi.cs:445-453 et 584-604. Aujourd'hui, aucune date n'est demandée. Les options proposées sont bien posées.

Ce qu'il faut corriger :

1) Le scénario (b) contredit le prorata au jour retenu. Avril seul, dans le 2e trimestre, vaut 3 000 × 30/91 = 989,01 € HT, pas 1 000 €.

2) « Périodicité ou mois cochés différents de ceux enregistrés » : il faut comparer les mois EFFECTIFS, pas la liste moisFacturationLoyer telle qu'elle est stockée. En mensuel, les mois sont ignorés (FacturationSuivi.cs:889).
- À l'ouverture, CocherMoisStandard (RevisionPanel.cs:166-170) coche janvier, avril, juillet et octobre sur une fiche trimestrielle qui n'a aucun mois enregistré.
- Avec une comparaison naïve, le bloc apparaîtrait, et pourrait refuser, à n'importe quel « Modifier » (jour de demande, délais…).

3) « Période lue dans le plan d'AVANT le changement » se trompe pour une ligne émise sous une autre périodicité (changement sans date fait avant cette version). Mars, P3 dans un plan trimestriel, est lu comme le 3e trimestre : première date au 01/10 au lieu du 01/04. Quand la ligne porte sa périodicité (D4-04), lire la fin de sa période sur la ligne elle-même (périodicité + échéance).

4) Ordre des contrôles. RefusAvenantPeriodicite doit passer AVANT Loyers.EnregistrerAvenant (RevisionPanel.cs:1656), qui écrit déjà l'historique des loyers. Sinon, un refus laisse la fiche à moitié écrite, contre la règle notée en 1641 (« à décider AVANT d'écrire quoi que ce soit »).

5) Reprise de facturation. Les périodes jusqu'à repriseFacturationISO (FacturationSuivi.cs:171-177) ont été facturées hors de CIPL, dans l'ancienne périodicité. « Rien à protéger » est donc faux pour une fiche reprise sans aucune ligne touchée : les compter comme facturées, ou dire pourquoi non.

6) Préremplissage depuis l'avenant de loyer. Sa date peut tomber en milieu de mois (seule condition : après le début du bail, RevisionPanel.cs:1649-1652), ce qui serait refusé (« le 1er du mois »). Préremplir au 1er du mois suivant, ou avec PremiereDateAvenant si elle est plus tardive.
  - Risques : Un loyer préparé d'avance (« en attente d'envoi », par exemple janvier 2027) repousse la première date possible. Le message de refus doit dire de le remplacer ou de le retirer d'abord.

La date et la provision sont liées. Avec une date saisie d'avance, les mois de l'ancienne tranche restent à facturer : la conversion d'office de la provision (voir PROVISION) les ferait appeler au nouveau montant.

Le test Revue16cTests.cs:346 compte exactement 3 occurrences de « DatesIllisibles() is string refusDate ». Pour la nouvelle date, enrichir la fonction DatesIllisibles plutôt qu'ajouter un 4e appel de cette forme.

Aucun champ nouveau, aucun effet serveur : confirmé.
  - Décision manquante relevée : Les trois options sont bien posées. Trois questions manquent :
- La date d'effet est-elle limitée au début d'une période commune aux deux périodicités ? C'est l'option plus simple, voir AVENANT-PERIODICITE.
- Une fiche avec reprise de facturation, sans aucun loyer facturé dans CIPL, compte-t-elle comme déjà facturée jusqu'à la reprise ?
- Une date d'effet après le départ saisi est-elle refusée ?

### AVENANT-PERIODICITE-PROVISION · Provision pour charges « par période » au changement de périodicité, et régul de l'année du changement
- État au 05/11 : présent · effort M · serveur : aucun · nouveau champ : non
- Lieux :
  - Assets/Script/Facturation/ListesCharges.cs:22 et 253-257 — provision « par période de loyer » (générale et par liste)
  - Assets/Script/RevisionLoyer/RevisionPanel.cs:1703-1705 et 1259-1265 — provisions écrites telles quelles ; 1802-1815 OnPeriodiciteChanged ne les touche pas
  - Assets/Script/Facturation/FactureRegulPanel.cs:633-634 — ProvisionsAuto = provision de la fiche × Loyers.PeriodesFacturees
  - Assets/Script/Facturation/FactureRegulPanel.cs:1008-1010 — MontantsParListe, même produit par liste
  - Assets/Script/RevisionLoyer/Loyers.cs:147-148 — PeriodesFacturees dans la périodicité actuelle
  - Assets/Script/Facturation/FactureEtat.cs:21-27 et FactureLoyerPanel.cs:676-682 — provisions imprimées sur chaque loyer (D6-07)
- Problème : La provision se saisit par période. Changer de périodicité sans la changer appelle 100 € par trimestre au lieu de 300 € : un tiers des provisions de l'année, découvert seulement à la régul. Et la régul propose provision de la fiche × nombre de périodes de la périodicité actuelle : faux pour une année à deux périodicités (et déjà faux aujourd'hui pour une provision modifiée en cours d'année). Avec la conception A, PeriodesFacturees compte 8 périodes en 2026 : 300 × 8 = 2 400 € au lieu de 1 200 €.
- Scénario : Provision 100 € par mois, mensuel → trimestriel au 01/07/2026. Provision laissée à 100 : 6 × 100 + 2 × 100 = 800 € appelés au lieu de 1 200. Convertie à 300 : 6 × 100 + 2 × 300 = 1 200 € appelés, mais la régul 2026 propose 300 × 8 = 2 400 € (plan A), ou 300 × 4 aujourd'hui, juste par hasard.
- Correction proposée : (1) RevisionPanel.OnPeriodiciteChanged (option recommandée) : quand la périodicité change à l'écran, le champ de provision générale (provisionValue) et chaque provision de liste (_listesChamps) sont multipliés par NbPeriodes(ancienne) / NbPeriodes(nouvelle), arrondis au centime (Tva.Cents) ; aide statique testable Loyers.ProvisionConvertie(montant, de, vers). (2) Régul, quelle que soit l'option : FactureRegulPanel.ProvisionsAuto et MontantsParListe prennent, pour chaque période du plan de l'année, la provision imprimée sur son loyer émis (provisionsConnues : provision pour la générale, provisionsListes par liste), sinon la provision de la fiche ; aide Loyers.ProvisionsAppelees(loc, année, listeId). Règle actuelle gardée : une période partielle (à cheval, arrivée, départ) appelle la provision entière, la régul corrige.
- Décision posée : La provision pour charges se saisit « par période » (par mois, par trimestre…). Quand on change de périodicité dans Gestion du loyer, que doit faire son champ ?
  - (recommandée) Convertie d'office, visible et modifiable : 100 € par mois deviennent 300 € par trimestre dans le champ (et pour chaque liste de charges), avant « Modifier » ; le montant reste corrigeable.
  - Gardée, avec un message au centre : Le montant reste ; un message rappelle qu'il est par période et doit être ajusté.
  - Gardée sans rien dire (comme aujourd'hui) : Risque d'appeler trois fois moins (ou plus) de provisions, vu seulement à la régul.
- Test : EditMode : Changer_de_periodicite_convertit_la_provision (ProvisionConvertie(100, mensuel, trimestriel) = 300 ; (300, trimestriel, mensuel) = 100 ; (100, mensuel, BiAnnuel) = 600) ; La_regul_de_l_annee_du_changement_compte_les_provisions_appelees (6 loyers mensuels à provision 100 et 2 trimestriels à 300, provisionsConnues → ProvisionsAppelees(loc, 2026, liste générale) = 1 200 ; une période sans ligne émise → la fiche).
- Interactions : AVENANT-PERIODICITE (le plan compte des périodes de deux périodicités). D6-07 (fait) : provisions imprimées sur la ligne. D6-16 (à faire) : parts par liste, même endroit (MontantsParListe), à faire ensemble. Règle 16c (D4-08, D6-11) « périodes facturées » gardée. Utile aussi sans avenant : une provision changée en cours d'année est aujourd'hui recomptée sur toute l'année.
- **Vérification : à corriger**
  - Corrections : Les lieux cités sont confirmés : ListesCharges.cs:22 et 253-257 (provision par période) ; RevisionPanel.cs:1703-1705, 1259-1265 et 1802-1815 (OnPeriodiciteChanged ne touche pas la provision) ; FactureRegulPanel.cs:633-634 et 1008-1010 ; Loyers.cs:147-148 ; FactureEtat.cs:21-27 ; FactureLoyerPanel.cs:676-682. Les calculs de l'analyse sont justes (800, 1 200 et 2 400 €).

Ce qu'il faut corriger :

1) Le repli « sinon la provision de la fiche » est faux pour une période de l'autre périodicité, puisque la fiche est convertie au trimestre. Et les provisions imprimées n'existent que depuis le 04/11 (D6-07, SYNTHESE.md:72) : les vrais loyers de janvier à juin 2026 ne les ont pas. Résultat : ProvisionsAppelees = 8 × 300 = 2 400 €, le défaut même qu'on veut corriger. Le repli doit être la provision de la fiche ramenée à la périodicité de la période (× NbPeriodes(actuelle) / NbPeriodes(de la période)), ou des provisions gardées par tranche dans PeriodiciteAnterieure.

2) La conversion d'office au moment de « Modifier » est fausse pour les mois de l'ancienne tranche encore à facturer quand la date d'effet est saisie d'avance. Exemple : effet au 01/07, mai et juin restent mensuels et recevraient 300 € par mois (FactureLoyerPanel.cs:892-895 et 974). Il faut la même règle par période dans le formulaire Loyer.

3) Calcul de la conversion.
- Partir de la valeur enregistrée × NbPeriodes(enregistrée) / NbPeriodes(choisie), pas du champ affiché. Sinon les allers-retours dérivent : 100 € trimestriel → 33,33 € → 99,99 €.
- Ne pas convertir un champ retouché à la main.
- OnPeriodiciteChanged se déclenche aussi à l'ouverture : la valeur est posée ligne 144 alors que l'ancien écouteur n'est retiré qu'en 174. C'est sans effet aujourd'hui (les champs sont rechargés en 165 et 224), mais il ne faut pas que la conversion en dépende.

4) Tests. La forme statique MontantsParListe(…, periodesFacturees, …) est appelée par Revue16cTests.cs:963-966 et TvaTests.cs:46. Il faut garder sa signature (passer les provisions appelées avec 1 période) ou retoucher ces tests. DepartLocataireTests.cs:292-296 reste valable tant que PeriodesFacturees existe.
  - Risques : « Utile aussi sans avenant » ne vaut que pour les loyers émis depuis le 04/11 : la régularisation 2026, faite en 2027, retombera sur la fiche pour janvier à octobre.

D6-16 (provision saisie ignorée par MontantsParListe) touche le même endroit : à faire ensemble.

Le récap de la fiche (« … € / trimestre », LoyerSummaryUI.cs:119-120) suivra la conversion.

Pas de nouveau champ si l'on prend le rapport des périodicités ; si l'on garde les provisions par tranche, elles entrent dans PeriodiciteAnterieure, donc dans la montée au modèle 8. Aucun effet serveur.
  - Décision manquante relevée : La question sur la conversion est bien posée. Deux choix manquent :
- La conversion vaut-elle à la date d'effet (une provision par tranche) ou dès « Modifier » ? Dès « Modifier », les mois restants de l'ancienne périodicité sont appelés au nouveau montant.
- Pour une période recoupée (2e trimestre de mai à juin), appeler la provision entière du trimestre (règle actuelle, la régularisation corrige), ou au prorata (200 € au lieu de 300 €) ?

