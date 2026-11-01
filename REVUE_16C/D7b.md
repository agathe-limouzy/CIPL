# Domaine D7b — Révision des loyers, objectifs, travaux, achats, PLU, cartes

Revue 16c, lecture seule (31/10/2026). 20 défauts : 0 Critique, 7 Majeurs, 13 Mineurs.
Le calcul de révision lui-même est juste (loyer de départ × indice voulu / indice de référence, recherche exacte, repli signalé) ; les défauts majeurs viennent des bords : écran laissé ouvert après la révision, loyer révisé appliqué aux périodes d'avant, valeurs par défaut qui déclenchent une fausse alerte, reprise de facturation enregistrée sur les mauvais mois, et un résumé de rentabilité qui compte à vie les emprunts finis et les locataires partis.
Objectifs, PLU et cartes sont sains dans l'ensemble (mineurs seulement).

---

### D7b-01 · Majeur · Un second clic sur « Réviser » repousse la prochaine révision de deux ans
- Où : `Assets/Script/RevisionLoyer/RevisionPanel.cs:496` (bouton réactivé), `:584-606` (date + 1 an à chaque passage), `:655` (le panneau reste ouvert)
- Problème : après une révision réussie, le panneau reste ouvert, le bouton « Réviser » reste actif et le champ « Date de révision » affiche déjà la date + 1 an. Un second clic recalcule le même loyer (idempotent) mais pose `MoisDeRevision` à date + 2 ans et écrase `loyerAnnuelPrecedent` avec le loyer révisé. En mode CA, la garde `anneeCA != _caAnnee` ne fait qu'avertir : un troisième clic passe.
- Scénario : révision au 01/04/2026, clic → « prochaine révision 01/04/2027 ». L'utilisatrice n'est pas sûre que c'est enregistré, reclique → « prochaine révision 01/04/2028 », « Loyer précédent » = loyer actuel. En 2027 aucune alerte « Révision » : une année d'indexation est oubliée.
- Correction proposée : après une révision réussie, fermer le panneau et confirmer par un toast, comme `Initialiser()` (l. 475-477) et `PaliersPanel.Valider` le font déjà. À défaut, `btnReviser.interactable = false` après succès.
- Test : Play — réviser, cliquer une seconde fois, vérifier que la date de prochaine révision n'a pas bougé (ou que le panneau est fermé). EditMode possible si la logique de `Reviser()` est extraite dans `Loyers` (voir D7b-02).
- Confiance : haute (code lu de bout en bout ; aucune garde entre deux clics).

### D7b-02 · Majeur · Révision par indice : le nouveau loyer s'applique aussi aux périodes d'avant la date de révision
- Où : `Assets/Script/RevisionLoyer/RevisionPanel.cs:598-600` (écrit `loyerAnnuel` sans historique) contre `:588-596` (mode CA : `Loyers.EnregistrerAvenant(_loc, d.Date)` d'abord) ; `Assets/Script/RevisionLoyer/Loyers.cs:71-82` (`LoyerAnnuelA` : hors avenant, `loyerAnnuel` pour toute date)
- Problème : en mode indice, `LoyerAnnuelA(loc, date)` renvoie le `loyerAnnuel` courant pour toutes les dates, passées comprises. Toute facture pré-remplie après la révision (période en retard, « Corriger ») prend le nouveau loyer même pour les jours d'avant la date de révision. Le mode CA fait le contraire, et le commentaire l. 590-591 dit que c'est voulu : « L'ancien loyer reste dû jusqu'à la veille de la révision : les périodes passées (facture, rentabilité) gardent leur montant ».
- Scénario : bail du 15/02, trimestres facturés au 01/01, 01/04, 01/07, 01/10, révision au 15/02/2026 faite le 20/02. Le T1 2026 (émis en décembre à l'ancien loyer) devrait recevoir un complément du 15/02 au 31/03 ; « Corriger » pré-remplit tout le trimestre au nouveau loyer. Un loyer de janvier non encore facturé (retard) sortirait aussi au nouveau montant.
- Correction proposée : dans `Reviser()`, appeler `Loyers.EnregistrerAvenant(_loc, d.Date)` avant d'écrire le nouveau loyer, pour les deux modes (indice et CA). `MontantPeriode` proratise alors seul la période qui contient la date de révision, et `DepartLocataire.LoyerACorriger` peut signaler le complément. `debutConditionsISO` n'est lu que par `Loyers`, `RevisionPanel` et `PaliersPanel` (vérifié) : pas d'effet de bord.
- Test : EditMode, jumeau de `Une_revision_selon_le_CA_garde_l_ancien_loyer_avant_sa_date` pour le mode indice : loyer 12 000 au trimestre, révision à 12 600 au 15/02/2026 → `MontantPeriode(loc, 2026, 1)` = 3 000 × 45/90 + 3 150 × 45/90 = 3 075 ; `MontantPeriode(loc, 2025, 4)` = 3 000 ; `MontantPeriode(loc, 2026, 2)` = 3 150.
- Confiance : haute sur le mécanisme ; l'impact dépend d'une facture d'une période passée émise ou corrigée après la révision.

### D7b-03 · Majeur · Valeurs par défaut de l'indice : « Révision à faire » dès l'initialisation, puis révision proposée avec un trimestre antérieur à la référence
- Où : `Assets/Script/RevisionLoyer/RevisionPanel.cs:170` (date de révision pré-remplie avec `loc.MoisDeRevision`), `:447` (enregistrée telle quelle), `:187` (trimestre voulu par défaut = année précédente) ; `Assets/Script/Locataire/Locataire.cs:429` (`MoisDeRevision` vide → aujourd'hui) ; `Assets/Script/RevisionLoyer/LoyerSummaryUI.cs:409` (`EstRevisionDue` = `Now >= MoisDeRevision`)
- Problème : pour une fiche neuve, « Date de révision » vaut aujourd'hui. Si l'utilisatrice la laisse, `Initialiser()` enregistre aujourd'hui → badge « Révision à faire » et « À traiter » dès la sortie de l'écran, « En retard » le lendemain. En ouvrant « Réviser », le trimestre voulu proposé est celui de l'année précédente, c'est-à-dire antérieur au trimestre de référence d'un bail récent ; rien ne refuse un indice voulu plus ancien que l'indice de référence.
- Scénario : bail du 01/07/2026, référence 2026-T1 (publié), initialisation en octobre 2026. Retour à la fiche : « Révision à faire ». Clic « Réviser » : trimestre voulu pré-rempli 2025-T1, clic → loyer × ILC(2025-T1) / ILC(2026-T1) ≈ −3 %, date de révision poussée à octobre 2027. Le loyer baisse sans révision réelle.
- Correction proposée : (1) à l'ouverture, si `moisDeRevisionISO` est vide, pré-remplir avec début du bail + 1 an ; (2) dans `Reviser()`, refuser un trimestre voulu de rang inférieur au trimestre de référence (`InseeIndiceService.TryDecompose` des deux, `annee*4+trim`), message sous le champ comme pour un trimestre non publié.
- Test : EditMode sur une règle extraite (`Loyers.PremiereRevisionParDefaut(loc)` = début du bail + 1 an ; `InseeIndiceService.RangAvant(voulu, reference)` → vrai pour (2025-T1, 2026-T1), faux pour (2027-T1, 2026-T1)). Play : initialiser sans toucher la date, vérifier qu'aucun badge n'apparaît.
- Confiance : haute sur le code ; l'effet suppose que l'utilisatrice garde les valeurs proposées, ce que l'écran encourage.

### D7b-04 · Majeur · Reprise de facturation enregistrée sur les mois standard, relue avec les mois cochés
- Où : `Assets/Script/RevisionLoyer/RevisionPanel.cs:1569-1571` (`EcheancePeriode(_loc.periodiciteLoyer, per, y, jour)` : mois standard 1/4/7/10), `:1143` (options filtrées de même) ; `Assets/Script/Facturation/FacturationSuivi.cs:156-160` (clôture si échéance de ligne ≤ reprise), `:662-667` (`EcheanceLoyer` : mois COCHÉS), `:813-818`
- Problème : la date de reprise est calculée avec les mois d'échéance standard, mais les lignes du suivi ont leurs échéances sur les mois cochés. Dès que les mois cochés sont plus tardifs que les mois standard, la dernière période déclarée « déjà facturée » n'est pas clôturée.
- Scénario : trimestriel, mois cochés 3/6/9/12, reprise « 2e trimestre 2026 » → enregistré 2026-04-01. Lignes : T1 (échéance 01/03) clôturé, T2 (échéance 01/06) reste « À faire » puis « Impayé » avec alerte, alors qu'il a été facturé hors de l'app ; risque de double facturation.
- Correction proposée : dans `EcrireModalites`, calculer la reprise avec `FacturationSuivi.EcheanceLoyer(_loc, y, per)` (les mois cochés sont écrits juste avant, l. 1556-1557) ; même chose pour le filtre des options l. 1143.
- Test : EditMode — locataire trimestriel, `moisFacturationLoyer = {3,6,9,12}`, `repriseFacturationISO` posé par la même règle pour P2 2026 ; `FacturationSuivi.Lignes(loc, 2026)` : P1 et P2 « Cloture », P3 sans statut.
- Confiance : haute (les deux côtés lus).

### D7b-05 · Majeur · Résumé Rentabilité : les mensualités de tous les emprunts sont comptées à vie, même une fois le prêt remboursé
- Où : `Assets/Script/Travaux/RentabiliteGlobaleController.cs:108-117, 125-134` (`startDate` et `dureeMois` stockés), `:139-147` (`mensTotales` = somme de toutes les mensualités, sans regarder la date de fin) ; même règle dans `Assets/Script/Batiment/BatimentSummaryView.cs:646-661` (`ComposantesFinancieres`, hors domaine : tuiles KPI « Dépensé / an », « Cash flow », « Total gagné »)
- Problème : le résumé (« Mensualités totales », « Cash flow », « Rentable en X ans », statut « Effort d'épargne ») additionne les mensualités de chaque emprunt tant que l'achat ou les travaux existent. Le tableau annuel, lui, borne bien les mois actifs (`MoisActifsDansAnnee`). Les deux chiffres se contredisent à l'écran.
- Scénario : achat 2005 financé sur 15 ans (fini en 2020), loyers 60 000 €/an, mensualité 4 500 €. En 2026 le résumé affiche cash flow −54 000 €/an et « Effort d'épargne » ; le tableau montre un cumulé qui grimpe.
- Correction proposée : une règle unique `RentabiliteCalculator.MensualiteEnCours(montant, taux, dureeMois, dateDebut, aujourdhui)` qui renvoie 0 après la dernière échéance ; utilisée par le résumé et par `ComposantesFinancieres`.
- Test : EditMode — `MensualiteEnCours(100 000, 2, 120, 2010-01-01, 2026-01-01)` = 0 ; avec `aujourdhui` = 2015-01-01 = `Mensualite(100 000, 2, 120)`.
- Confiance : haute.

### D7b-06 · Majeur · Résumé Rentabilité : « Loyer annuel » additionne les loyers des locataires partis et archivés
- Où : `Assets/Script/Travaux/RentabiliteGlobaleController.cs:96` (`_bp.GetLoyerTotal()`) ; cause dans `Assets/Script/Batiment/BatimentPrefab.cs:66-72` (somme de `loyerAnnuel` sur toute `listLocataire`, qui contient les archivés : l. 247, 451) et `BatimentSummaryView.cs:641-644` (hors domaine, même somme)
- Problème : `loyerAnnuel` n'est jamais remis à zéro au départ (aucune écriture `loyerAnnuel = 0` dans le code, `Loyers.Actualiser` ne touche que les paliers). Un locataire parti ou archivé continue de compter dans « Loyer annuel », donc dans le cash flow, le rendement et le break-even du résumé, ainsi que dans les tuiles et la liste des bâtiments. Le tableau annuel, lui, s'arrête au départ (`FinFacturation`, `dateSortieISO`).
- Scénario : locataire A parti le 30/06/2026 (loyer 24 000), remplaçant B (24 000). Résumé : « Loyer annuel 48 000 € / an », rendement doublé ; le tableau 2027 montre 24 000.
- Correction proposée : `GetLoyerTotal()` ignore `loc.archive` et `loc.EstParti` (un seul endroit, utilisé par tous les écrans) ; même filtre dans `ComposantesFinancieres`.
- Test : EditMode — bâtiment avec deux locataires dont un `dateSortieISO` passé : `GetLoyerTotal()` = loyer du seul présent (ou test sur une règle statique `Loyers.LoyerTotalEnCours(liste, aujourdhui)` si `BatimentPrefab` est trop lourd à instancier).
- Confiance : haute sur le code ; hors de mon domaine strict pour la cause, mais le chiffre faux s'affiche dans Rentabilité.

### D7b-07 · Majeur · Nouvel achat / nouveaux travaux : la date n'est ni remise à zéro ni contrôlée
- Où : `Assets/Script/Achat/AchatFormPanel.cs:87-89` (ApplyDate seulement si une date existe), `:182` (`saveThedate()` écrit `SelectedDate` tel quel) ; `Assets/Script/Travaux/TravauxFormPanel.cs:77-79, 153` ; `Assets/Script/Calender/DateInputController.cs:127-134` (`saveThedate` ne valide rien)
- Problème : pour un nouvel élément, `dateAchat` est vide, donc `ApplyDate` n'est pas appelé : les champs et `SelectedDate` gardent la date du dernier formulaire ouvert (ou `01/01/0001` au premier usage). `Save()` enregistre cette valeur sans vérifier qu'une date complète a été saisie.
- Scénario : modifier l'achat de 2012, fermer, « + Ajouter des travaux » : les champs affichent déjà 2012 ; l'utilisatrice saisit le coût et enregistre → travaux de 2026 datés 2012, coût placé en 2012 dans le tableau annuel, « Total gagné » faussé. Au premier usage : « 0001-01-01 · comptant » dans la liste.
- Correction proposée : dans `ApplyToUI`, sans date → `datePicker.ApplyDate(DateTime.Today)` (comme le faisait l'ancien `TravauxController.ApplyData`) ; dans `Save()`, lire la date avec `datePicker.LireDate(out d)` et refuser (`ConfirmDialog.Erreur`) si elle est incomplète.
- Test : Play — ouvrir un achat daté, le fermer, ajouter des travaux, vérifier la date proposée (aujourd'hui) et le refus d'une date vide. EditMode possible sur `DateInputController.LireDate` si la saisie est extraite.
- Confiance : haute.

---

### D7b-08 · Mineur · Référence d'indexation enregistrée avec l'indice de repli → première année sur-indexée dans la rentabilité
- Où : `Assets/Script/RevisionLoyer/RevisionPanel.cs:436-461` (`TrouveAvecFallback`, puis `EnregistrerReference(…, obsRef.periode, obsRef.valeur, …)`), `:613-614` (à la révision, nouvelle référence datée du jour de révision) ; `Assets/Script/RevisionLoyer/LoyerHistoryService.cs:191-215`
- Problème : quand le trimestre de référence n'est pas encore publié, l'initialisation enregistre la période et la valeur de repli (année − 1) comme base du premier segment. À la première révision, la vraie base est enregistrée comme un nouveau segment daté de la révision ; le premier segment reste faux. `LoyerPourAnnee` applique alors pour la première année du bail indice(T) / indice(T − 1 an) : une année d'inflation en trop.
- Scénario : bail 01/11/2026, référence 2026-T3 initialisée le 05/11 (publié fin décembre) : base = valeur 2025-T3. Rentabilité 2026 : loyer × ILC(2026-T3)/ILC(2025-T3) ≈ +3 % alors que le loyer facturé est le loyer de départ.
- Correction proposée : à la révision, dater la référence comme à l'initialisation (début du bail ou avenant, l. 459-460) au lieu de `d` : `Segments` garde l'ordre d'ajout à date égale et le dernier segment l'emporte pour toutes les années.
- Test : EditMode — `EnregistrerReference` deux fois à la même date avec deux bases ; `LoyerPourAnnee(loc, anneeDuBail, obs)` utilise la seconde.
- Confiance : haute.

### D7b-09 · Mineur · Rentabilité en mode indice : loyer théorique, pas le loyer facturé
- Où : `Assets/Script/RevisionLoyer/LoyerHistoryService.cs:103-172`
- Problème : pour les paliers et « aucune révision », la rentabilité somme `MontantPeriode` (le loyer réellement facturable). Pour l'indice, elle recalcule un loyer théorique : même trimestre chaque année (celui de la référence) appliqué à l'année civile entière, prorata au mois (l. 146-153) alors que la facturation est au jour, et sans tenir compte d'une révision faite avec un autre trimestre, en retard, ou oubliée. Les chiffres « Rentrées » ne recoupent donc pas le suivi de facturation.
- Scénario : franchise jusqu'au 15/11 → novembre compté entier ; révision effectuée avec 2026-T1 au lieu de T2 → écart de quelques dixièmes de pour cent sur l'année.
- Correction proposée : avec D7b-02 en place (historique des loyers rempli à chaque révision), utiliser la somme de `MontantPeriode` pour toutes les années couvertes par l'historique, et ne garder la reconstruction INSEE que pour les années antérieures sans historique (anciennes fiches).
- Test : EditMode — locataire indice avec un avenant enregistré : `LoyerPourAnnee(loc, annee, null)` = somme des `MontantPeriode` de l'année.
- Confiance : haute sur l'écart, moyenne sur son importance (tableau indicatif).

### D7b-10 · Mineur · Saisie de date : l'année sur deux chiffres passe, et la date de révision est enregistrée aussitôt
- Où : `Assets/Script/Calender/DateInputController.cs:71-102` (`ValidateDate` : aucune borne d'année) contre `:106-114` (`LireDate` exige ≥ 1900) ; `Assets/Script/RevisionLoyer/RevisionPanel.cs:341-348` (`OnDateRevisionModifiee` → `_onSaved` à chaque champ quitté), `:584-586`
- Problème : taper « 27 » dans l'année donne `SelectedDate` = 14/05/0027, et le panneau l'enregistre sur le locataire sans confirmation. `EstRevisionDue` devient vrai, « À traiter » affiche « Révision · En retard », et une révision ferait une prochaine révision en 0028.
- Scénario : l'utilisatrice corrige la date de révision, tape « 27 », clique ailleurs : « Prochaine révision : 14/05/0027 », fiche en rouge.
- Correction proposée : appliquer dans `ValidateDate` la même borne que `LireDate` (année ≥ 1900) — une règle, un endroit.
- Test : EditMode (SaisieDateTests) — `ValidateDate` avec « 27 » ne pose pas `HasValidDate`.
- Confiance : haute.

### D7b-11 · Mineur · Récurrence mensuelle d'un objectif : le jour glisse (31 → 28 → 28…)
- Où : `Assets/Script/Objectif/Objective.cs:162-168` (`Suivante` : `AddMonths`), `:184` (l'occurrence suivante part de l'échéance précédente)
- Problème : chaque occurrence est calculée depuis la précédente ; « le 31 de chaque mois » devient le 28 après février et y reste.
- Scénario : loyer à encaisser le 31/01, récurrence mensuelle : 28/02, 28/03, 28/04…
- Correction proposée : dans `MarquerFait`, remonter `Precedentes` jusqu'à la première occurrence et reposer son jour (`Math.Min(jourOrigine, DaysInMonth)`) après `AddMonths`.
- Test : EditMode (ObjectifsTests) — chaîne 31/01 → fait → fait : la 3e échéance est le 31/03.
- Confiance : haute.

### D7b-12 · Mineur · Achats et travaux : montants parsés en `float` avant d'être stockés en `double`
- Où : `Assets/Script/Achat/AchatFormPanel.cs:163-178` (`float prix = Parse(...)` → `_data.prixAchat`), `Assets/Script/Travaux/TravauxFormPanel.cs:144-152` ; `Assets/Script/Tools/SaisieNumerique.cs:23-47` (`out float`)
- Problème : le modèle est passé en `double` « parce qu'un prix de 2 500 000,45 € était enregistré 2 500 000,50 € » (commentaire `AchatFinancement.cs:8-13`), mais le formulaire convertit la saisie en `float` avant de l'écrire : la perte annoncée corrigée est toujours là, dès la saisie.
- Scénario : saisir 2 500 000,45 → le formulaire rouvert affiche 2500000.5.
- Correction proposée : `SaisieNumerique.TryParse(string, out double)` (même normalisation, `double.TryParse`), la version `float` l'appelle ; les deux formulaires lisent en `double`.
- Test : EditMode (SaisieNumeriqueTests) — « 2 500 000,45 » → 2500000.45 exactement en `double`.
- Confiance : haute.

### D7b-13 · Mineur · INSEE : pas de délai d'attente, XML non protégé → « Réviser » bloqué sur « Récupération… »
- Où : `Assets/Script/RevisionLoyer/InseeIndiceService.cs:26-39` (aucun `req.timeout`), `:118-132` (`LoadXml` sans `try`)
- Problème : si l'INSEE ne répond pas, la coroutine attend sans fin ; si la réponse est un 200 non-XML (page de maintenance), `LoadXml` lève et tue la coroutine `Reviser()`. Dans les deux cas le bouton reste grisé et le statut « Récupération… » tant que le panneau n'est pas fermé puis rouvert.
- Scénario : INSEE en maintenance pendant une révision.
- Correction proposée : `req.timeout = 15` comme les autres services ; `try { ParseObservations } catch → onError("Réponse INSEE illisible")`.
- Test : Play avec réseau coupé : message d'erreur en moins de 20 s, bouton rendu.
- Confiance : haute.

### D7b-14 · Mineur · Géocodage : réponse non protégée et meilleur résultat pris sans seuil
- Où : `Assets/Script/Maps/GeoCodingService.cs:95-108` (`JsonUtility.FromJson` hors `try`, `coords[0]`/`[1]` sans contrôle de longueur, `properties.label` sans test de null), `:79` (`limit=1`, pas de `score`)
- Problème : une réponse inattendue lève dans la coroutine : `MapController.SearchAddress` meurt, le bouton de recherche reste grisé. Par ailleurs api-adresse renvoie toujours le meilleur candidat, même pour une adresse incomplète : la carte peut se poser ailleurs sans avertir. `CadastreService` (même API) protège déjà son parse (l. 130-131).
- Scénario : adresse « 6 route d'Agde » sans ville → carte sur une autre commune.
- Correction proposée : `try/catch` autour du parse → `onError` ; ignorer un résultat dont `properties.score` < 0,5 et dire « Adresse imprécise ».
- Test : Play — adresse incomplète : message au lieu d'une carte lointaine.
- Confiance : haute sur le parse, moyenne sur le seuil (valeur à régler).

### D7b-15 · Mineur · Carte : les textures Google ne sont jamais détruites
- Où : `Assets/Script/Maps/TileLoader.cs:66-67` (nouvelle `Texture2D` à chaque image), `:39-43` (l'ancienne `targetImage.texture` est remplacée, pas détruite)
- Problème : chaque zoom ou déplacement crée une texture 512×512 (≈ 1 Mo) ; Unity ne libère pas les textures créées par code. Une longue session de consultation laisse grossir la mémoire. Le cache des vignettes (`MapThumbnailService`) est voulu et borné par le nombre d'adresses ; ici rien ne borne.
- Scénario : 200 zooms/déplacements dans la journée → ~200 Mo.
- Correction proposée : dans `LoadMapAt`, `Destroy(targetImage.texture)` si elle n'est pas dans le cache des vignettes, avant de poser la nouvelle.
- Test : Play — Profiler mémoire après 20 zooms : stable.
- Confiance : haute.

### D7b-16 · Mineur · PLU : le panneau abandonne à 10 s, la requête dure jusqu'à 15 s
- Où : `Assets/Script/PLU/PLUOverlayPanel.cs:341-347` (`timeout = 10f`) ; `Assets/Script/PLU/PLUService.cs:44` (`req.timeout = 15`)
- Problème : à 10 s le panneau affiche « Délai dépassé — réessayez » ; la réponse qui arrive entre 10 et 15 s est perdue, et « Réessayer » lance une seconde requête en parallèle.
- Correction proposée : rendre `FetchZone` yieldable (renvoyer l'`IEnumerator`) et le `yield return` depuis le panneau, sans boucle d'attente ; un seul délai (15 s), dans le service.
- Test : Play avec réseau lent (ou `PLUService` simulé) : un seul message, cohérent.
- Confiance : haute.

### D7b-17 · Mineur · Formulaires achat/travaux : écouteurs empilés et durée d'emprunt héritée du formulaire précédent
- Où : `Assets/Script/Achat/AchatFormPanel.cs:117-123` (`AddListener` à chaque `Open` sans `RemoveAllListeners`), `:199` (`SetDuree` ne change rien si la durée n'est pas dans la liste) ; `Assets/Script/Travaux/TravauxFormPanel.cs:106-109, 164`
- Problème : après N ouvertures, chaque frappe recalcule N fois (sans effet visible). Plus gênant : pour un nouvel élément (`dureeMois` = 0) ou une durée hors liste, le menu garde la durée du dernier formulaire ouvert, et `Save()` l'enregistre.
- Scénario : modifier un achat sur 240 mois, puis ajouter des travaux à crédit sans toucher la durée → 240 mois enregistrés.
- Correction proposée : `RemoveAllListeners` avant chaque `AddListener` dans `WireListeners` ; `SetDuree` retombe sur une valeur par défaut (120) quand la durée n'est pas dans la liste.
- Test : Play — enchaîner les deux formulaires et lire la durée proposée.
- Confiance : haute.

### D7b-18 · Mineur · Code mort
- Où : `Assets/Script/Travaux/TravauxController.cs` (251 lignes entièrement commentées ; GUID du script référencé par aucun prefab ni scène, vérifié) ; `Assets/Script/Travaux/RentabiliteGlobaleController.cs:145` (`rendBrut` calculé, jamais affiché) ; `Assets/Script/Objectif/Objective.cs:10, 37` (`createdAt` : `DateTime`, non sérialisé par JsonUtility et jamais lu) ; `Assets/Script/Calender/DateInputController.cs:116-126` (`OnConfirm` avec `Debug.Log`, code commenté)
- Problème : du code qui ne sert plus, dont une ancienne logique de rentabilité (proratisation au mois, `GetCoutAchat`) qui pourrait resservir de modèle. `createdAt` donne l'impression d'une date de création conservée alors qu'elle est perdue à chaque sauvegarde.
- Correction proposée : supprimer le fichier et les champs/lignes cités (le commentaire de `SaisieNumerique.cs:7` cite `TravauxController` : à retirer aussi).
- Test : compilation ; la suite EditMode.
- Confiance : haute.

### D7b-19 · Mineur · Carte Loyer : le TTC est toujours HT × 1,20
- Où : `Assets/Script/RevisionLoyer/LoyerSummaryUI.cs:22` (`TVA = 1.2f`), `:112, 126-128, 140-149`
- Problème : la fiche affiche « Loyer annuel TTC » et « Loyer par période TTC » à 20 % pour tous les locataires, y compris un bail d'habitation (IRL, pas de TVA) ou un locataire marqué non assujetti (`Locataire.assujettiTva`, étape 18b). Le dépôt a déjà une base HT/TTC au choix (`depotSurTTC`).
- Scénario : bail d'habitation à 9 600 € HT → la fiche annonce 11 520 € TTC, jamais facturés.
- Correction proposée : afficher le TTC seulement quand le loyer est soumis à TVA (même règle que la facture de loyer), sinon « — » ou masquer la colonne.
- Test : Play — fiche IRL : pas de TTC affiché.
- Confiance : moyenne (l'option TVA du bailleur par bail n'est pas modélisée ; la règle à appliquer est à confirmer avec l'utilisatrice).

### D7b-20 · Mineur · L'ICC n'est pas proposé
- Où : `Assets/Script/Locataire/Locataire.cs:465-470` (`IndiceImmo` : ILC, IRL, ILAT) ; `Assets/Script/RevisionLoyer/InseeIndiceService.cs:10-16`
- Problème : un bail commercial ancien (avant la loi Pinel de 2014) peut encore être indexé sur l'ICC ; il ne peut pas être révisé dans l'app. Le cahier des charges de cette revue cite l'ICC.
- Correction proposée : ajouter `ICC` en fin d'enum (ordre figé) et sa série dans `Indices` (série trimestrielle de l'indice du coût de la construction, identifiant BDM à vérifier en ligne) ; `TrimestreInput` et `RevisionPanel` n'ont pas de liste figée à part les trois puces (`chipILC/IRL/ILAT`, à compléter ou à remplacer par le menu).
- Test : EditMode — `GetLabel(IndiceImmo.ICC)` ne lève pas ; Play — révision d'un bail ICC.
- Confiance : moyenne (besoin réel à confirmer ; identifiant de série non vérifiable sans réseau).

---

## Points vérifiés sans défaut

- **Formule de révision** (`RevisionPanel.Reviser`) : loyer de départ × indice voulu / indice de référence ; indice voulu en correspondance exacte (pas de repli), repli de la référence signalé à l'écran, indice de départ nul refusé, 29 février géré, trimestre futur bloqué avant tout appel réseau, résultat précédent effacé si le trimestre n'est pas publié. Mode CA : bornes indexées par le même ratio, CA ramené à l'année au prorata des jours, année du CA vérifiée.
- **Plafonnement légal** : aucun n'est appliqué. Le seul plafonnement récent de la clause d'indexation (ILC +3,5 % pour les PME, indices T3 2022 à T1 2024) est échu ; le lissage de 10 % de la loi Pinel vise le déplafonnement au renouvellement ou à la révision triennale, pas la clause d'échelle mobile. Rien à corriger ; à écrire dans la documentation.
- **Paliers** (`Loyers.Normaliser / PoserPalier / FinsPossibles / Couvrent`, `PaliersPanel`) : début calculé, fin la veille d'un début de période ou fin du bail, écran sur copie écrite à la validation, `Actualiser` au chargement (`BatimentManager.cs:383, 509`) et à la validation, premier palier dès le début du bail avec la franchise comprise. Couvert par les tests existants.
- **Montant d'une période** (`Loyers.MontantPeriode`) : au jour près, période pleine = exactement loyer / n, prorata du début de bail, de la franchise, du départ et d'un avenant ; franchise sur le loyer seul (provisions dues). Tests existants.
- **Culture, fuseau, ISO** : dates stockées `yyyy-MM-dd` et relues en culture invariante (`FacturationSuivi.TryEcheance`, `Objectifs.Date/Iso`, `RentabiliteGlobaleController.ParseDate`) ; indices écrits en invariant (`FormatIndice`, M4 réglé) et relus de même ; saisies numériques par `SaisieNumerique` (point et virgule) ; `DateTime.Today` local partout, pas d'UTC mélangé.
- **Verrou cloud Révision → Paliers / Initialiser → Indice** : la seconde réservation d'une fiche déjà tenue n'appelle pas le serveur et ajoute un second `Tenu` ; le gardien ne libère qu'une fois qu'aucun écran ne la tient (`EcritureCloud.cs:601`). Pas de libération prématurée.
- **Objectifs** : « À traiter » prend tous les objectifs non faits, du bâtiment puis de chaque locataire non archivé, sans doublon ; `MarquerFait` crée une seule occurrence suivante, `Rouvrir` la retire si elle n'a pas servi, `Precedentes` est protégé contre une boucle ; `Avancer` suit le cycle voulu ; l'état d'échéance est recalculé à l'affichage (limite `ponytail` connue). Les cartes « Automatique » suivent la même règle que « À traiter ».
- **Mensualité** (`RentabiliteCalculator.Mensualite`) : taux 0 et durée 0 gérés, calcul en `double` ; **tableau annuel** : mois d'emprunt actifs par année civile, apport l'année de départ, dates aberrantes bornées, départ du locataire pris en compte.
- **Cartes** : clé Google côté serveur (M1 réglé, « Hors cloud : pas de carte » est une décision), une seule demande par adresse affichée (`_adresseAffichee`, `_pendingAddress`), cache des vignettes par adresse, quota et refus Google traduits en clair (`TileLoader.Motif`) ; `_isLoading` ne peut rester bloqué que si l'appel serveur ne se termine jamais, or `CloudClient.EnvoyerBrut` pose un délai de 120 s et `UnityWebRequest` ne lève pas.
- **PLU et cadastre** : erreurs réseau et parse protégés dans `PLUService` et `CadastreService`, centroïde par moyenne des sommets (approximation acceptable pour un point dans la parcelle), liste des commerces annoncée « indicative » avec avertissement à l'écran.
- **Non vérifiable ici** : les identifiants de séries INSEE (ILC 001532540, IRL 001515334, ILAT 001617113) — pas de réseau pendant la revue ; les valeurs relues en Play jusqu'ici n'ont rien montré d'anormal.
