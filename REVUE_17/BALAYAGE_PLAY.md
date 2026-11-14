# Balayage de l'interface en Play — Revue 17 (03/11/2026)

Méthode : l'application tourne en Play sur SCI (JBC), écran 1920 × 1080. Chaque écran est ouvert par le code (clic simulé sur ses boutons, rien n'est enregistré ni envoyé), puis mesuré aux paliers de zoom 0,9 · 1 · 1,25 · 1,5 · 1,75 (le zoom d'origine est remis à la fin). Le détecteur ([audit_ui_play.cs](audit_ui_play.cs), à coller dans `script-execute` pendant le Play ; précautions en tête du fichier) liste :
- **DEBORDE** : texte plus large ou plus haut (de plus de 25 %) que sa zone, sans « … » (les textes coupés par « … » sont voulus et seulement comptés) ;
- **CHEVAUCHE** : deux éléments voisins, hors mise en page automatique, qui se recouvrent ;
- **HORS_ECRAN** : élément visible qui sort de la fenêtre (le contenu des zones qui défilent est exclu) ;
- **PETIT** : texte réduit sous 11 px par l'auto-taille.
Une capture est prise à chaque palier (`revue17_<écran>_<zoom>.png` dans le dossier de données de l'application) et regardée quand le détecteur signale quelque chose.

Écrans faits (03/11) : Accueil ; fiche du bâtiment « Immeuble BI2 » et sa fiche complète ; fiche de Locataire BJ6 ; les quatre formulaires de facture (loyer, régularisation, refacturation, révision du dépôt) ; Gestion du loyer (« Modalités » et « Réviser ») ; départ du locataire ; Réglages (six onglets) ; Compte ; Calcul rapide ; Consulter le PLU ; Historique des charges ; listes Travaux et Achats (vides sur cet immeuble) ; Photos. Ce qui tient : à 175 %, les sections s'empilent et défilent sans chevauchement ; aucun texte réduit sous 11 px ; Réglages, PLU, Photos et départ sans aucun défaut.

## Constats

### B-01 · Majeur · Au zoom 125 % et plus, le bouton « + » (nouveau bâtiment) sort de l'écran
- Où : objet de scène `Batiment Manager/TabBar` (pas de code) : ancré en haut à gauche avec une **largeur fixe de 1920** ; son `HorizontalLayoutGroup` ne commande pas la largeur des enfants (`childControlWidth = false`) : Accueil 160, Rechercher 160, `TabScrollView` 1250, « + » 60 (largeur posée par `DirectCloud.AfficherEnLigne:294`).
- Mesure : à 100 %, « + » est à x 1788-1848 ; à 125 %, la barre fait 2038 px pour un écran de 1920 et « + » est à x 1963-2038, hors de l'écran (derrière la croix « Quitter »). Même chose à 150 et 175 %.
- Correction proposée : ancrer `TabBar` en largeur (ancres x 0 → 1, décalages 0) et laisser la barre répartir la place : `childControlWidth = true`, `TabScrollView` flexible (flex 1, min ≈ 200), `LayoutElement` de « + » à 60 (min et préférée), puis retirer le `sizeDelta` posé à la main dans `DirectCloud`. Les onglets des bâtiments défilent déjà dans `TabScrollView`.
- Contrôle en Play : zoom 125 % puis 175 %, « + » visible à gauche de la croix, onglets toujours défilables.

### B-02 · Majeur · Montants au format anglais sur l'accueil, la fiche du bâtiment, la liste des locataires, la rentabilité, les achats et les travaux
- Où : une quarantaine d'affichages `$"{x:N0} €"` sans culture, donc à la culture du PC : `BatimentSummaryView.cs:124-193`, `BuildingCard.cs:35`, `BuildingRowUI.cs:26`, `LocataireRowUI.cs:73`, `LoyerSummaryUI.cs:356,376`, `RentabiliteGlobaleController.cs:150-156`, `RentabiliteRow.cs:30,41`, `AchatFormPanel.cs:162-165`, `AchatItemUI.cs:18-20`, `InvestissementListPanel.cs:256`, `TravauxFormPanel.cs:142-143`, `BatimentPrefab.cs:345` (`F2`), messages de `RevisionPanel.cs:700` et `PaliersPanel.cs:97,373` (`N2`).
- Mesure : sur ce PC, `CultureInfo.CurrentCulture` vaut **en-US** : l'accueil affiche « Loyers / an 87,419 € », « Total gagné +20,258,214 € », « Rendement net 73.6 % » ; la fiche du bâtiment « 31,082 € », la liste « 10,000 €/an ». Un lecteur français lit « 87,419 € » comme 87 euros. Les Créances, elles, sont bien au format français (« 7 570,06 € », `FacturePdfService.FrCulture`).
- Correction proposée : passer `FacturePdfService.FrCulture` à ces formats (`x.ToString("N0", FacturePdfService.FrCulture)`, ou un petit `Euros(x)` commun), pourcentages compris ; l'espace insécable (U+00A0) existe dans la police.
- Contrôle en Play : accueil et fiche du bâtiment : « 87 419 € », « 73,6 % ».

### B-03 · Majeur · Aperçu de la facture : le bouton « Ajuster » s'affiche « Aju / ste / r »
- Où : `FactureLoyerPanel.cs:368-374`, `FactureRegulPanel.cs:299-301`, `FactureRefacPanel.cs:294-296`, `FactureDepotPanel.cs:371-373` : la barre `ZoomBar` est un `HBox` placé dans le coin, **sans taille** ni `ContentSizeFitter` ; ses trois boutons (44 + 84 + 44) sont serrés dans la largeur par défaut de la barre : « Ajuster » n'a plus que 23 px pour un mot de 47.
- Mesure : vu sur la capture du loyer à tous les zooms (texte sur trois lignes).
- Correction proposée : `ContentSizeFitter` horizontal (`PreferredSize`) sur `zbar`, dans les quatre panneaux (ou une aide commune, le code est dupliqué quatre fois).
- Contrôle en Play : ouvrir un formulaire, « Ajuster » sur une ligne, à 100 % et 175 %.

### Écartés après vérification (03/11)
- Nom du locataire dans l'en-tête de sa fiche : il est raccourci par « … » (`overflowMode` Ellipsis), vu sur la capture à 150 % : pas de débordement.
- Boutons de facturation de la fiche du locataire à 175 % : signalés trop étroits (221 px) quand le formulaire du loyer était ouvert par-dessus ; fiche affichée seule, ils font 289 px pour 250 de texte : ils tiennent.
- « Départ du locataire » : 146 px de texte pour une zone de 140, mais le bouton fait 160 : le texte reste dans le bouton.
- Recouvrement « fiche du bâtiment / vue du locataire » à 150 % : la vue du locataire se pose par-dessus la fiche du bâtiment, comme voulu ; rien de visible sur la capture.

### B-07 · Mineur · Textes d'aide (placeholders) coupés dans les champs d'une ligne
- Où : les quatre formulaires (loyer, régularisation, refacturation, dépôt) : « Phrase de règlement (bas de facture, ex. « Valeur en votre aimable règlement ») » (coupé dès 100 %, sur deux lignes dans un champ de 26 px), « Texte / N° interne (optionnel, en rouge au-dessus du n°) » (coupé à 150 %) ; régularisation : « Provisions déjà versées (provision × périodes…) » (coupé à 150 %). C'est le seul défaut des formulaires de régularisation, refacturation et dépôt, de 100 à 175 %.
- Correction proposée : le libellé au-dessus du champ dit déjà tout : placeholder court (« Valeur en votre aimable règlement », « Optionnel »).

### B-08 · Mineur · Fiche du bâtiment : l'adresse sous le nom est dans une zone de 6 px de haut
- Où : `HeaderRow/ColonneInfos/RowTitre/InfoBlock/TxtAdresse` : zone 1620 × 6 pour un texte de 17 px ; il déborde et touche le nom du bâtiment (vu sur la capture à 100 %).

### B-09 · Mineur · Barres de défilement posées sur le contenu (4 à 5 px) à 125 % et plus
- Où : liste de la fiche du bâtiment (`Scroll view Batiment`), liste de la rentabilité (`RentabilitéListParent`).

### B-10 · Majeur · L'avertissement de surfaces bloque à chaque ouverture et à chaque fermeture d'un formulaire
- Où : `ConfirmDialog` (file d'attente sans dédoublonnage) ; vu après le rechargement du 03/11 : « Les surfaces saisies des locataires … dépassent celle du bâtiment … » deux fois, deux « OK » à cliquer ; puis de nouveau en ouvrant la fiche de Locataire BJ6, puis **à l'ouverture et à la fermeture** de la régularisation, de la refacturation et de la révision du dépôt (données d'essai de l'immeuble BI2 : 100 m² de bâtiment pour 200 m² de lots). Un client dont les surfaces ne tombent pas juste clique « OK » deux fois par facture.
- Correction proposée : dans `Erreur`, ne pas mettre en file un message identique à celui affiché ou déjà en attente (les `ensuite` restent tous exécutés) ; et ne montrer cet avertissement de surfaces qu'à l'enregistrement d'une surface (fiche du bâtiment ou du lot), pas à chaque ouverture.

### B-11 · Mineur · La pastille « SCI (JBC) · cloud » flotte par-dessus le contenu
- Où : `CloudPastille`, en bas à gauche : elle couvre le coin des cartes de bâtiments et, à 125-175 %, une partie de la liste « À traiter ». À décider : la garder flottante (avec un fond plus discret) ou la placer dans la barre du haut.

### B-12 · Mineur · Réglages › Facturation : les exemples du calendrier ne suivent pas la valeur tapée
- Où : `ReglagePanel.cs:670` et `:673` (textes fixes) : « Échéance le 1er décembre → envoi le 16 novembre » est juste pour 15 jours ; avec 20 jours (valeur de SCI (JBC), vue sur la capture à 175 %), l'envoi part le 11 novembre, mais l'exemple dit toujours le 16.
- Correction proposée : recalculer les deux phrases à chaque saisie (`onValueChanged` des champs `_dEnvoiAvant` et `_dImpayeApres`), à partir d'une échéance d'exemple au 1er décembre.

### B-13 · Mineur · Gestion du loyer : à 150 % et plus, le bas de la fenêtre sort de l'écran
- Où : `RevisionPanel` (fenêtre `Loyer`, volets « Modalités » et « Réviser ») : la fenêtre est bien plafonnée et son contenu défile, mais son cadre dépasse le bas de l'écran (67 px à 150 %, 10 px à 175 %) et les boutons « Modifier » et « Fermer », placés au bout du contenu, ne se voient qu'après avoir fait défiler jusqu'en bas (à 175 % : y -391 avant défilement, y 80 après).
- Correction proposée : sortir « Modifier » / « Fermer » du contenu qui défile (pied fixe, comme « Enregistrer » des Réglages) et plafonner la hauteur en tenant compte du zoom.

### B-14 · Mineur · Gestion du loyer : un carré gris vide sous « Provisions »
- Où : volet « Modalités » de Locataire BJ6 (bail sans provision) : sous le titre « Provisions », une petite case grise vide, sans libellé (vue sur la capture à 175 %). À masquer quand il n'y a aucune provision, ou à remplacer par « Aucune provision ».

### B-15 · Majeur · Fenêtre « Compte » : les trois boutons du bas se chevauchent, à tous les zooms
- Où : `EcransCloud.cs:783-788` (pied `Pied` : « Fermer », « Changer d'entreprise », « Se déconnecter ») : les boutons ne prennent pas la largeur de leur texte ; la rangée les serre (41, 118 et 100 px pour 59, 168 et 143 px de texte).
- Mesure : vu sur la capture à 175 % : « Fermer » touche « Changer d'entreprise », qui recouvre le début de « Se déconnecter », lui-même coupé (« e déconnecte »). Signalé à 100, 125, 150 et 175 %.
- Correction proposée : `UIFactory.LargeurDuTexte(…)` sur les trois boutons (l'aide existe et sert déjà dans `ReglagePanel`) ; la carte est assez large pour les trois.
- Contrôle en Play : Accueil › « Compte », à 100 % et 175 % : trois boutons lisibles, séparés.

### B-17 · Majeur · Historique des charges : les noms longs recouvrent le montant, le statut recouvre le locataire
- Où : `InvestissementListPanel` (lignes `Achat Item (Clone)` : `InfoLeft/TXT LABEL`, `InfoRight/txt Mensualité`) : zones de largeur fixe (214 et 146 px), sans retour à la ligne ni « … ».
- Mesure : à tous les zooms : « Entretien parties communes 2026 » (309 px pour 214) recouvre « 365 € » ; « Réparation porte d'entrée 2026 » colle au montant (« 2026120 € ») ; « En attente de paiement · 1 locataire(s) » (205 px pour 146) recouvre « Locataire BI2 » (vu sur la capture à 175 %). Un intitulé de charge réaliste (« Taxe d'enlèvement des ordures ménagères 2026 ») déborde toujours.
- Correction proposée : `overflowMode = Ellipsis` sur le nom et le statut, avec la zone de gauche flexible (`flexW 1`) et le montant à largeur fixe ; ou retour à la ligne du nom sur deux lignes.
- Contrôle en Play : Immeuble BI2 › « + Charges », à 100 % et 175 %.

### B-16 · Mineur · Calcul rapide : le libellé « Loyer annuel (€) » dépasse de 11 px
- Où : `Calcul Rapide/Saisie/Slot1/Label` : 131 px pour 120, sans retour à la ligne, à tous les zooms.

« Consulter le PLU » : rien à signaler de 100 à 175 %.

Réglages (six onglets : Entreprise, Facturation, Utilisateurs, Abonnement, Documents et affichage, Intégrations) : aucun débordement, chevauchement ni élément hors écran, de 100 à 175 %. Fenêtre de départ du locataire : rien à signaler de 100 à 175 %.

Incident du balayage (03/11) : en cherchant la croix de fermeture de « Gestion du loyer », le script a cliqué la croix rouge « Quitter » de l'application. La garde de fermeture a répondu « Modifications non enregistrées — Locataire BJ6 — Quitter maintenant perdrait ces saisies » ; « Annuler » a été choisi, rien n'a été perdu ni enregistré. La garde fonctionne.

### À vérifier
- À 150 % seulement, la fiche du bâtiment et la vue du locataire ont été signalées en recouvrement (1920 × 1441), sous le formulaire ouvert : à revoir en ouvrant la fiche du locataire à 150 %.
- Les petits symboles devant « 100 m² », « 3 lots », « Sans parking », « Occupé » sur la fiche du bâtiment sont des objets à part (le texte n'a aucun caractère manquant dans la police) : à regarder de près.

## Reste à faire
Avec le même détecteur, d'après l'« Inventaire des écrans » et les points V1 à V9 du rapport D10 :
1. Formulaire « Nouvelle charge » à 125, 150 et 175 % (`ChargePanel.OpenForm(bp, null, () => {})`, défaut D10-01 à confirmer), puis les formulaires de travaux et d'achat.
2. « Créer mon compte » à 150 et 175 % (`AfficherInscription`, par réflexion), Paliers sur un bail de 9 ans, Départ en mode cession.
3. Le filtre locataires des Créances ouvert, avec un nom de 50 caractères (D10-05).
4. La bande « Dépôt de garantie » de la fiche du locataire (bouton et pastille posés à la main).
5. Toute l'application en vue Game 1366 × 768 à 100 %, pour la lisibilité (D10-03).
6. Documents du bail, objectifs, rentabilité, mode « Modifier » des fiches.

Précautions apprises : ne jamais chercher un bouton « × » dans tout l'écran (c'est aussi la croix « Quitter » de l'application, `BtnQuitter`) ; fermer chaque fenêtre par son propre bouton, cherché dans la fenêtre ; remettre le zoom d'origine à la fin (`UIZoom.Facteur` est enregistré dans les préférences).

## Reprise du 09/11/2026 (Fable)

Même détecteur (classe `Audit` de `audit_ui_play.cs`, posée dans `.ai-game-dev/snippets/audit_ui.cs`, hors de git, avec deux parcours prêts : `Mesurer` une racine affichée, `Parcourir` des fenêtres par le libellé de leur bouton), SCI (JBC), 1920 × 1080, zooms 1 · 1,25 · 1,5 · 1,75, captures `revue17b_<écran>_<zoom>.png` dans le dossier de données ; plus l'accueil, le bâtiment, le locataire et les Réglages en 1366 × 768 à 100 et 125 % (`revue17b_1366_*.png`, taille de la vue Game changée puis remise). L'essai de SCI (JBC) avait expiré le 08/11 : prolongé au 09/12 par SQL pour mesurer les formulaires (en lecture seule, ils refusent de s'ouvrir).

Écrans faits : choix de l'entreprise ; accueil ; Compte ; Calcul rapide ; Consulter le PLU ; Réglages (six onglets) ; « Choisir l'abonnement » ; fiche du bâtiment « Immeuble BI2 » et sa fiche complète ; historique des charges, achats et travaux, et leurs trois formulaires (« Nouvelle charge », « Nouvel achat », « Nouveaux travaux ») ; Photos ; Lots ; fiche de « Locataire BJ6 » en lecture et en saisie (« Modifier » puis « Sauvegarder » à valeurs identiques) ; suivi 2026 et 2027 ; les quatre formulaires de facture ; Départ du locataire (simple et « Cession du bail ») ; Gestion du loyer (« Modalités », « Réviser ») ; Réviser le dépôt ; menu de la pastille du suivi ; « Paiement partiel… » ; nouvel objectif ; filtre « Tous les locataires » des Créances ; inscription et mot de passe (fenêtres ouvertes par leur fonction, rien envoyé). Rien n'a été enregistré ni envoyé, sauf un « Sauvegarder » de la fiche BJ6 à valeurs inchangées.

### Défauts du 03/11 revus
- Toujours présents : **B-01** (le « + » de la barre des bâtiments hors écran dès 125 %, même en 1366 × 768 ; à 175 % la croix « Quitter » recouvre le dernier onglet), **B-02** (montants au format anglais sur l'accueil : « 199,419 € », « 5,306,500 € », « 3.3 % », « 31,082 €/an », alors que les Créances sont en français), **B-03** (« Ajuster » sur trois lignes dans les quatre formulaires), **B-07** (textes d'aide coupés : phrase de règlement dès 100 %, « Texte / N° interne » et « Provisions déjà versées » à 150 %), **B-08** (adresse du bâtiment dans une zone de 6 px), **B-09** (barres de défilement sur le contenu dès 125 %), **B-10** (avertissement de surfaces à l'ouverture de SCI (JBC), puis de nouveau au retour à l'accueil après le gardien d'inactivité), **B-11** (pastille « SCI (JBC) · cloud » par-dessus le contenu), **B-13** (Gestion du loyer : le cadre de la fenêtre sort de l'écran à 150 % et plus, 144 px à 150 %), **B-15** (boutons de « Compte » : 41, 118 et 100 px pour 59, 168 et 143), **B-16** (« Loyer annuel (€) » : 131 px pour 120), **B-17** (noms des charges sur le montant).
- Non mesurables par le détecteur, à revoir à l'œil : B-12 (exemples du calendrier des Réglages), B-14 (carré gris sous « Provisions »).

### Nouveaux constats (09/11)

### B-18 · Majeur · Le formulaire « Nouvelle charge » sort de l'écran à 150 % et plus (D10-01 confirmé)
- Où : `ChargePanel` (`ChargeScrim/ChargeCard`). Mesure : à 150 %, la carte va de y −105 à 1 185 pour un écran de 1 080 ; la rangée `Actions` (« Annuler », « Enregistrer ») est à y −80..−14, sous le bord ; même chose à 175 %. À 100 et 125 % : rien.
- Correction proposée : plafonner la hauteur de la carte à celle de l'écran (en tenant compte du zoom) et faire défiler son corps, avec le pied (« Annuler », « Enregistrer ») fixe, comme les Réglages.
- Contrôle en Play : « + Charges » › « + Ajouter une charge » à 150 % : les deux boutons du bas visibles.

### B-19 · Mineur · Les menus flottants ne sont pas ramenés dans l'écran quand le zoom les pousse à droite
- Où : `UIFactory.PlacePopup` (menu de la pastille du suivi, `StatutScrim/StatutList` : à 125 % et plus, x 1 773..2 010 pour 1 920 ; à 100 % il tient) et la liste des menus déroulants (`UIDropdown`, `DDScrim/DDList` du filtre « Tous les locataires » des Créances : à 150 % et plus, x 1 846..2 161). Le placement ne borne que verticalement (« au-dessus s'il manque de place en bas »).
- Correction proposée : après le placement, ramener le rectangle dans l'écran sur les deux axes (décalage à gauche de ce qui dépasse), dans `PlacePopup` et dans l'ouverture de `UIDropdown`.
- Contrôle en Play : à 150 %, Créances › « Tous les locataires » : la liste entière visible ; suivi › pastille d'une ligne de droite : le menu visible.

### B-20 · Mineur · Le compteur de la pastille « À traiter » déborde de son rond
- Où : `A Traiter/Header/NbBadge/Nb` : « 12 » mesure 24 × 22 pour une zone de 18 × 20, à tous les zooms et en 1366 × 768 (visible sur la capture : les chiffres sortent du rond rouge). Un chiffre tient, deux débordent.
- Correction proposée : rond à largeur préférée (texte + marges) ou taille 11 pour deux chiffres ; `ContentSizeFitter` sur la pastille.

### B-21 · Mineur · Fiche du locataire à 175 % : trois boutons de facturation trop étroits
- Où : `FacturationActions/FactRow1` : « Régularisation des charges », « Refacturation d'une charge », « Révision du dépôt (facture) » ont 221 px pour 248 à 250 de texte à 175 % (le 03/11, la mesure avait été écartée parce que le formulaire du loyer était ouvert par-dessus ; ici la fiche est seule). Rien à 150 %.
- Correction proposée : retour à la ligne autorisé sur ces boutons (deux lignes à 175 %), ou une rangée qui passe sur deux lignes au-delà d'une largeur.

### B-22 · Mineur · Quelques pixels hors écran à 175 %
- Où : fenêtre des lots (`LotsScrim/LotsCard` : y −20..1 100), formulaire d'achat (`Achat from panel/Achat` : y −8..1 000), Gestion du loyer › « Réviser » (`Loyer/Content` : y −10). Sans conséquence visible sur les captures, mais les cadres touchent les bords.

### B-23 · Mineur · Texte d'aide « Réf. cadastrale (ex. AB 142) » coupé en mode saisie à 150 %
- Où : `Cadastral/InputField to modify/Text Area/Placeholder` : 252 px pour 191 à 150 % (fiche du locataire, « Modifier »). Correction : « Réf. cadastrale ».

### Écartés après vérification (09/11)
- `FacturationActions/FactRow1/Button/Text <> Rappel` (16 × 20 px à tous les zooms) : c'est la petite pastille « ! » posée au bord droit du bouton « Facturer le loyer », qui recouvre la zone du texte mais pas ses lettres (capture `revue17b_Menu_pastille_1.00.png`).
- `Content Prefab <> VueLocataire` à 150 % : la vue du locataire se pose sur la fiche du bâtiment, comme voulu (déjà écarté le 03/11).
- Menu de la pastille hors écran quand la ligne du suivi n'est pas à l'écran : artefact du clic simulé (l'utilisatrice fait défiler avant de cliquer).
- Propres à tous les zooms : Réglages (six onglets), « Choisir l'abonnement », inscription, mot de passe, PLU, fiche complète du bâtiment, photos, nouveaux travaux, départ (simple et cession), réviser le dépôt, « Paiement partiel… », nouvel objectif, menu de la pastille à 100 %.

### Non mesuré
- Paliers : aucun locataire de SCI (JBC) n'est au type « Paliers ». Documents du bail (« Ajouter le bail » ouvre une boîte de fichiers de Windows). Pappers (appel au serveur). Créances avec un nom de 50 caractères (D10-05) : aucun locataire de ce nom. Lisibilité en 1366 × 768 (D10-03) : le détecteur ne la mesure pas ; captures `revue17b_1366_*.png` à regarder.

### Précautions apprises (09/11)
- Le gardien d'inactivité (« Toujours là ? ») se déclenche pendant un long balayage : les clics simulés ne comptent pas comme une activité ; au bout du compte à rebours, les saisies sont annulées, la fiche libérée et l'application revient à l'accueil (l'avertissement de surfaces revient avec). Faire des passes courtes, ou bouger la souris entre deux.
- Une fenêtre peut s'ouvrir après le délai d'attente (départ : voile apparu après 1,5 s) ; la repérer par son nom (`DepartScrim`, `StatutScrim`, `VersementsScrim`, `ChargeScrim`, `LotsScrim`, `DDScrim`, `CloudVoile`, `Facture…Panel`) plutôt que par la différence des objets. Les formulaires de facture restent empilés s'ils ne sont pas refermés par « ← Retour ».
- En mode saisie de la fiche, il n'y a pas de bouton « Annuler » : la sortie est « Sauvegarder » (le bouton `Save` de la fiche, pas celui du bâtiment, qui répond « Enregistrez d'abord la fiche de … »).
- `VersementsPanel` est une classe statique : sa fenêtre est `VersementsScrim`.
