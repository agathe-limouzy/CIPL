# CIPL — Architecture du code

*Document technique, établi le 30/09/2026 sur la branche `Facturation`.*

À lire avec :

- `REVUE_CODE_CIPL.md` : suivi de la revue de code, passation, défauts corrigés.
- `FACTURATION_CIPL_PENNYLANE.md` : règles métier de la facturation et historique des décisions.

Ce document décrit **comment le code est organisé et pourquoi**. Il ne répète pas les règles métier détaillées : il indique où elles vivent.

---

## 1. En bref

| | |
|---|---|
| **Application** | Gestion d'un patrimoine immobilier : bâtiments, locataires, baux, loyers, révisions, facturation, rentabilité, urbanisme (PLU). |
| **Technologie** | Unity **2023.2.22f1**, uGUI + TextMesh Pro, C#. Application de bureau **Windows** (génération des PDF par Microsoft Edge). |
| **Taille** | 124 scripts (≈ 27 800 lignes) dans `Assets/Script`, 28 fichiers de tests (345 tests EditMode) dans `Assets/Editor/Tests`. |
| **Scène** | Une seule : `Assets/Scenes/SampleScene.unity`. |
| **Assemblies** | Aucune `.asmdef` : tout le code est dans `Assembly-CSharp`, les tests dans `Assembly-CSharp-Editor`. |
| **Données** | Fichiers JSON locaux, **un par bâtiment**, dans une racine de sauvegarde **par entreprise**. Pas de base de données, pas de serveur. |
| **Paquets notables** | `com.unity.test-framework` (tests) ; `com.ivanmurzak.unity.mcp` (pilotage de l'éditeur par Claude, pour le développement seulement) ; StandaloneFileBrowser (sélecteurs de fichiers natifs). |

---

## 2. Vue d'ensemble

Le code se lit en cinq couches. La règle qui les relie est simple : **l'interface appelle les règles, les règles lisent et écrivent les modèles, la persistance écrit les modèles sur le disque.** Les règles métier ne connaissent pas l'interface, ce qui les rend testables sans scène.

```
┌───────────────────────────────────────────────────────────────────────────┐
│ INTERFACE  (MonoBehaviour)                                                │
│  Scène + prefabs : MenuManager, GeneralMenuPanel, BatimentPrefab,         │
│  LocatairePrefab, RevisionPanel, LoyerSummaryUI…                          │
│  Panneaux construits par code : Facture*Panel, ReglagePanel,              │
│  EntreprisePanel, PaliersPanel, LocataireFacturationFields…               │
│  Boîte à outils : UIFactory, UITheme, UIDropdown, UIZoom, UndoToast…      │
├───────────────────────────────────────────────────────────────────────────┤
│ RÈGLES MÉTIER  (classes statiques, sans UI, testées)                      │
│  Loyers · LoyerHistoryService · FacturationSuivi · FacturationAlertes ·   │
│  FactureEmission · ListesCharges · HeritageFacture · MentionTva ·         │
│  ParcoursLocataire · HomeAlertCollector · Locataire (règles du bail)…     │
├──────────────────────────────────┬────────────────────────────────────────┤
│ SERVICES EXTERNES                │ PERSISTANCE                            │
│  InseeIndiceService (INSEE)      │  BatimentManager (charge / sauve)      │
│  mode Pennylane retiré 02/11     │  AtomicFile · BackupService            │
│  EmailService (SMTP)             │  SaveLocationService · DossiersDonnees │
│  FacturePdfService (Edge)        │  ReglageService (+ secrets)            │
│  PapperService (annuaire État)   │  EntrepriseService · MigrationDossiers │
│  Maps / PLU / Cadastre (IGN…)    │  TransfertEntreprise                   │
├──────────────────────────────────┴────────────────────────────────────────┤
│ MODÈLES  ([Serializable], JsonUtility)                                    │
│  Data → Batiment, Locataire · ChargeBatiment · FactureInfo · FactureEtat ·│
│  ReglageData · PalierLoyer · RevisionReference · Objective…               │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Arborescence

`Assets/` contient `Script/` (le code), `Editor/` (tests et outil de mesure), `Prefab/` (26 prefabs), `Scenes/`, `StreamingAssets/` (gabarits HTML des factures, logo), `Plugins/NuGet` (DLL du plugin MCP) et `StandaloneFileBrowser/`.

| Dossier de `Assets/Script` | Rôle | Fichiers clés |
|---|---|---|
| *(racine)* | Bases communes | `Data` (id + nom), `PrefabBatLoc` (base des fiches), `InputAndText` (champ lecture/saisie) |
| `Batiment/` | Bâtiment : modèle, fiche, vue résumé, photos, chargement et sauvegarde | `Batiment`, `BatimentManager`, `BatimentPrefab`, `BatimentSummaryView`, `PhotoService` |
| `Locataire/` | Locataire : modèle et règles du bail, fiche, sections, parcours de création | `Locataire`, `LocatairePrefab`, `LocataireBailFields`, `LocataireFacturationFields`, `ParcoursLocataireUI`, `BailFileUI` |
| `RevisionLoyer/` | Loyer dans le temps : initialisation, indice, paliers, franchise, avenant, historique | `Loyers`, `RevisionPanel`, `PaliersPanel`, `LoyerSummaryUI`, `LoyerHistoryService`, `InseeIndiceService` |
| `Facturation/` | Tout le module facture (34 fichiers) : charges, suivi, émission, PDF, envoi, réglages, et la boîte à outils UI | voir §8.3 |
| `General/` | Accueil, alertes « À traiter », couleurs et icônes de toute l'app, garde de fermeture | `GeneralMenuPanel`, `HomeAlert` (+ `HomeAlertCollector`), `AppSectionColors`, `FermetureGuard` |
| `Menu/` | Barre d'onglets (bâtiments, locataires) | `MenuManager`, `MenuItem` |
| `Save/` | Emplacement des données, dossiers, écriture sûre, sauvegardes, entreprises, migration, transfert | `SaveLocationService`, `DossiersDonnees`, `AtomicFile`, `BackupService`, `EntrepriseService`, `TransfertEntreprise` |
| `Cloud/` | Multi-utilisateur (Supabase), branché au démarrage : connexion, choix de l'entreprise et import (étapes 2-3), enregistrement par fiche avec version et verrous (étape 4), en direct et présence (étape 5), documents partagés (étape 6), réservation de l'envoi des factures (étape 7), hors ligne (étape 8), Réglages partagés, utilisateurs et abonnement (étape 9), export de toutes les données (étape 11), mot de passe oublié (étape 12), inscription libre-service (étape 13), mise à jour de l'application installée (étape 15 : `version.json` lu sur GitHub au lancement, bandeau ou fenêtre obligatoire). Côté serveur : `Serveur/*.sql`, passés dans l'ordre (essai, etape2, etape4, etape4b, etape5, etape6, etape6b, etape7, etape9a, etape9b, etape9c1, etape9c2, etape10, etape10b, etape13, etape14), et les fonctions serveur `Serveur/fonctions/*.ts` (Supabase Edge Functions) : Stripe (`abonnement-stripe`, `stripe-webhook`) et les e-mails de l'abonnement (`emails-abonnement`, étape 14, appelée chaque matin par la tâche `pg_cron` d'etape14 et par `stripe-webhook`). Voir `CONCEPTION_MULTIUTILISATEUR.md` | `CloudClient`, `SessionCloud`, `DemarrageCloud`, `DonneesCloud`, `ImportLocal`, `EcritureCloud`, `DirectCloud`, `TempsReel`, `EcransCloud`, `RegleVerrou`, `Dpapi`, `Trousseau`, `DocumentsCloud`, `DossierEntreprise`, `HorsLigne`, `CopieLocale`, `ReglagesCloud`, `UtilisateursCloud`, `SectionUtilisateurs`, `RegleAbonnement`, `AbonnementCloud`, `SectionAbonnement`, `RegleExport`, `ExportCloud`, `SectionExport`, `RegleMotDePasse`, `RegleInscription`, `PremiersPas`, `VisiteGuidee`, `Bulles`, `BatimentExemple`, `RegleVersion`, `MiseAJour` |
| `Travaux/`, `Achat/` | Investissements, travaux, tableaux de rentabilité | `RentabiliteGlobaleController`, `TravauxController`, `InvestissementListPanel` |
| `Objectif/` | Objectifs (par bâtiment, locataire, et global) | `ObjectiveManager`, `GlobalObjectivesSection` |
| `Maps/` | Carte, géocodage, tuiles, vignettes | `MapController`, `GeoCodingService`, `MapThumbnailService` |
| `PLU/` | Urbanisme : zone PLU d'une adresse ou d'une parcelle | `PLUOverlayPanel`, `PLUService`, `CadastreService` |
| `Pappers/` | Résumé d'entreprise des locataires : sur le cloud, fiche Pappers par le serveur et procédures du BODACC (étape 16b) ; hors cloud, annuaire de l'État | `PappersSync`, `SocieteLocataire`, `PapperService`, `PappersResume` |
| `Tools/` | Composants UI génériques et saisie ; ce qui diffère entre Windows et Mac (étape 15b) | `Plateforme`, `UITheme`, `UIZoom`, `CollapsibleSection`, `ColonnesAdaptatives`, `SaisieNumerique`, `SaisieDate`, `UndoToast`, `ConfirmDialog` |
| `Calender/` | Saisie de date JJ/MM/AAAA | `DateInputController` |

`UIFactory` et `UIDropdown` sont rangés dans `Facturation/` pour des raisons historiques (le module les a créés), mais toute l'app s'en sert.

---

## 4. Modèle de données

### 4.1 Un fichier par bâtiment

Tout ce qui concerne un bâtiment — ses locataires compris — est sérialisé dans **un seul JSON** par `JsonUtility` :

```
Batiment : Data                       (id, Name)
├─ adresse, surfaces, parking, cadastre, date d'acquisition, photos
├─ historiquesAchat : List<AchatFinancement>
├─ travaux          : List<TravauxFinancement>
├─ charges          : List<ChargeBatiment>          ← charges à refacturer / régulariser
│     └─ facturations : List<ChargeFacturation>    ← état PAR locataire
├─ objectifs        : ObjectiveList
└─ locataireDuBatiment : List<Locataire>
      Locataire : Data
      ├─ identité, contact, SIRET, lot, surface, RIB
      ├─ bail : type, durée, période ferme, dates, fichiers (bail + avenants)
      ├─ loyer : loyerDepart, loyerAnnuel, périodicité, jour de demande, mois facturés
      │   ├─ typeRevision (Indice | Paliers | Aucune)
      │   ├─ indice : type, trimestre, indice de départ/actuel, date de révision
      │   ├─ paliers : List<PalierLoyer>          {debutISO, finISO, loyer}
      │   ├─ franchise (debutFacturationISO), avenant (debutConditionsISO + historiqueLoyers)
      │   ├─ départ (dateSortieISO), reprise de facturation (repriseFacturationISO)
      │   └─ historiqueReferences : List<RevisionReference>   ← rentabilité indexée
      ├─ charges : provision, date de régul, listes de charges (regulListes, listesConcernees)
      ├─ dépôt : montant, base TTC/HT, date de révision, depotInitialise
      ├─ factureLoyer / factureRegul / factureRefac / factureDepot : FactureInfo
      │                                           ← réglages mémorisés de chaque panneau
      ├─ factureSeq                                ← numérotation (voir H1, en attente)
      └─ facturesEtat : List<FactureEtat>          ← lignes du suivi « touchées »
```

Les réglages d'entreprise sont à part (`ReglageData`, fichier `reglage.json`) : mode d'envoi, SMTP, RIB, entêtes, listes de charges, textes fixes, logo, délais de facturation (01/11 ; un locataire peut avoir les siens).

### 4.2 Règles de sérialisation — à respecter

Chacune de ces règles a coûté un défaut réel (voir `REVUE_CODE_CIPL.md`).

- **`[Serializable]` sur chaque classe imbriquée.** Sans lui, `JsonUtility` ignore le champ **sans erreur ni trace**. C'est le défaut C4 : aucun réglage de facture n'a été enregistré pendant des semaines. Chaque nouvelle classe reçoit un test d'aller-retour JSON (`FactureInfoSerialisationTests`, `RevisionLoyerTests`).
- **Dates en chaînes ISO `yyyy-MM-dd`**, relues en culture **invariante** (`FacturationSuivi.TryEcheance`). Une lecture en culture courante échouait selon la machine.
- **Nombres décimaux en culture invariante** à l'écriture (ex. `FormatIndice`). La saisie passe par `SaisieNumerique`, qui accepte virgule et point.
- **Ordre des enums figé** (la valeur stockée est l'index). On ajoute à la fin, on ne réordonne jamais (`BailType`, `TypeRevision`, `FacturationSuivi.Etat`).
- **Tout nouveau champ doit avoir une valeur par défaut compatible** avec les anciens fichiers. Exemple : `typeRevision = Indice` (0), car toutes les fiches antérieures révisaient par indice.
- **Préférer un état déduit à un booléen stocké.** `LoyerInitialise`, `DepotInitialise`, `ParcoursLocataire.Prochaine`, `PrefabBatLoc.EnEdition` et les états « À venir / À faire » du suivi sont **calculés**. Un booléen dupliqué finit par se désynchroniser.
- **Montants en `float`.** Exact au centime sous 1 M€ (mesuré) ; `double` seulement pour les gros cumuls. Arrondi au centime aux points de calcul (`Math.Round(…, 2)`).

---

## 5. Persistance : les fichiers sur le disque

### 5.1 Arborescence d'une entreprise

```
<racine>/CIPL_Saves/                      ← une racine par entreprise
├─ reglage.json                           ← ReglageData (aucun secret)
├─ pennylane_secrets.dat                  ← mot de passe SMTP (clé=valeur ; nom de fichier historique)
├─ batiments/
│   └─ batiment_<id>.json                 ← un bâtiment et tous ses locataires
├─ Batiment/                              ← documents, rangés par NOM (lisible dans l'explorateur)
│   └─ <Nom du bâtiment>/
│       ├─ _Infos du bâtiment/            ← rangement du 23/10
│       │   ├─ Factures de charges/       ← PDF des factures de charges
│       │   ├─ Objectifs/
│       │   └─ Photos/
│       └─ <Nom du locataire>/
│           ├─ Bail/                      ← bail et avenants (copiés)
│           ├─ Factures/                  ← PDF émis : Avoirs, Dépôt de garantie, Loyers,
│           │                               Quittances, Refacturations, Régularisations
│           └─ Objectifs/
├─ Anciens documents/                     ← dossiers à identifiant d'avant le nommage par nom
├─ Backups/<date_heure>/                  ← copie au démarrage, gardée 30 jours
├─ corbeille_batiments/, corbeille_photos/ (01/11 : aussi le dossier d'un locataire supprimé, restauré par « Annuler »)
└─ .migration_dossiers_v1                 ← marqueur de la migration GUID → noms
```

### 5.2 Les pièces

- **`SaveLocationService`** : racine active (`PlayerPrefs`), repli sur `persistentDataPath`. Une racine enregistrée mais introuvable (disque débranché) est **signalée**, pas remplacée en silence.
- **`DossiersDonnees`** : chemins de tous les documents, conversion d'un nom en nom de dossier valide (caractères interdits, noms réservés Windows, 60 caractères au plus), chemins **relatifs** stockés (`VersRelatif` / `VersAbsolu`). `Nouveau` dit où va un document rangé avant le 23/10 : `VersAbsolu` s'en sert pour les chemins déjà enregistrés, `DocumentsCloud.RangerIci` pour déplacer les fichiers à l'ouverture. Les noms étant des dossiers, ils doivent être uniques : deux bâtiments homonymes sont refusés, et deux locataires homonymes seulement dans le même bâtiment.
- **`AtomicFile.WriteAllText`** : écriture dans un `.tmp` puis bascule par `File.Replace`, l'ancienne version passant en `.bak`. Renvoie `false` en cas d'échec : l'appelant doit le dire, jamais afficher « sauvegardé ».
- **`BatimentManager`** :
  - `LoadAll` charge chaque JSON isolément : un fichier tronqué est signalé sans bloquer les autres ;
  - `SaveBatiment` passe par `AtomicFile` ;
  - `SauvegardeDeFermeture` réécrit l'état en mémoire en quittant, et refuse si la racine a changé entre-temps.
- **`BackupService`** : copie datée au démarrage (JSON + `reglage.json`, **jamais les secrets**), purge après 30 jours.
- **`ReglageService`** : `Current` est chargé à la demande. Si `reglage.json` est illisible, l'écriture est **refusée** pour ne pas écraser l'original. Les secrets vivent dans deux fichiers `clé=valeur`, hors JSON, hors sauvegardes et hors dépôt Git :
  - `<racine>/pennylane_secrets.dat` pour ceux de l'entreprise (mot de passe SMTP ; nom de fichier historique) ;
  - `persistentDataPath/cipl_app_secrets.dat` pour ceux de l'application (adresse et clé du cloud), communs à toutes les entreprises. Le jeton Mapbox n'y est plus lu depuis l'étape 16b. Revue 16c (01/11) : le projet de CIPL est **compilé** (`Cloud/CloudDefaut.cs`, D3-01) et ce fichier n'est plus qu'une surcharge (projet de test) ; sur le cloud, les secrets de l'entreprise sont dans `persistentDataPath/cipl_secrets_<entreprise>.dat`, **chiffrés** (DPAPI sur Windows, trousseau sur Mac : `ReglageService.LireTexte`/`EcrireTexte`, D1-09).
- **`EntrepriseService`** : registre global des entreprises dans `PlayerPrefs` (`cipl_entreprises`). Changer d'entreprise, c'est changer de racine, puis `ReglageService.Load()` et `BatimentManager.ReloadFromDisk()` **à chaud**.
- **`MigrationDossiers`** : passage des dossiers nommés par GUID à des dossiers nommés par nom. Le plan est validé en entier avant tout déplacement, une sauvegarde est faite d'abord, et le marqueur n'est écrit qu'en cas de réussite complète.
- **`TransfertEntreprise`** : déplace un bâtiment ou un locataire vers une autre entreprise, **sur le disque uniquement** (racines explicites). Fusion par nom, listes de charges retrouvées par nom, tous les identifiants réécrits.

---

## 6. Cycle de vie de l'application

### 6.1 Démarrage

1. **Amorces `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`** : elles ajoutent des fonctions sans toucher la scène.
   - `FacturationBootstrap` : bouton « Réglage » sur l'accueil (clone d'un bouton existant).
   - `EntrepriseBootstrap` : bouton « Entreprises », et inscription de l'entreprise active.
   - `FermetureGuard` : intercepte la fermeture (`Application.wantsToQuit`).
2. **`BatimentManager.Start`**, dans cet ordre :
   1. `UIZoom.Appliquer()` : la taille d'interface, avant toute construction d'écran.
   2. `BackupService.RunStartupBackup()` puis `PhotoService.PurgerCorbeille()`.
   3. `LoadAll()` : pour chaque bâtiment,
      - `ChargeBatiment.Reconstituer` (ancien format de charges) ;
      - `Loyers.Actualiser` (loyer courant = palier du jour) ;
      - création de la fiche (`SpawnPrefabInPanel`) et de son onglet.
   4. `MigrationDossiers.Migrer(...)`.
   5. `menuManager.OpenGeneralMenu()` : l'accueil.
   6. `FactureEnvoiAuto.ProposerAuDemarrage(...)` : récapitulatif des factures prêtes à partir.
   7. `PappersSync.Tout(...)` : mise à jour des résumés d'entreprise, en coroutine, espacée de 0,4 s.

### 6.2 Navigation

`MenuManager` gère la barre d'onglets : l'accueil (`GeneralMenuPanel`), puis un onglet par bâtiment (`BatimentPrefab`). Chaque fiche bâtiment a sa propre barre d'onglets de locataires (`menulocataire`) et deux vues : **résumé** (`BatimentSummaryView`) et **fiche complète**.

### 6.3 Modifier puis enregistrer une fiche

Les deux fiches héritent de **`PrefabBatLoc`** (boutons Modifier / Sauvegarder / Supprimer). Le déroulé est le même :

```
InitializeBatiment / InitializeLocataire(data, needToModify)
   └─ la fiche bâtiment travaille sur un CLONE JSON du Batiment
Modify()        → champs InputAndText en saisie, bouton Sauvegarder visible (= EnEdition)
Save…()         → vérifications (bail, unicité du nom, parcours) → champs reversés dans le modèle
   └─ BatimentManager.SaveBatiment(batiment)
        ├─ remplace l'entrée de la liste en mémoire (le clone devient la référence)
        └─ AtomicFile → batiments/batiment_<id>.json
```

Les écrans secondaires (loyer, dépôt, factures, charges) écrivent **directement dans le modèle du locataire**, puis appellent `batimentPrefabOrigin.SaveAfterModifyToDoListLocataire()`. Cette méthode enregistre le bâtiment et rafraîchit la surface, le loyer total, la liste des locataires, l'alerte d'onglet et le suivi.

### 6.4 Fermeture

`FermetureGuard` prévient si une fiche est encore en saisie (il n'enregistre pas à la place de l'utilisatrice). `BatimentManager.SauvegardeDeFermeture` réécrit l'état en mémoire.

---

## 7. L'interface : principes de construction

- **Deux façons de faire un écran.**
  - Les écrans de la scène et des prefabs : fiches, `RevisionPanel`, menus.
  - Les écrans construits **100 % par code** avec `UIFactory` : panneaux de facture, réglages, entreprises, paliers, bandeaux. Ces derniers sont en général des singletons **créés au premier appel** (`FactureLoyerPanel.OpenLoyer`, `ReglagePanel.OpenPanel`, `FactureDepotPanel.OpenDepot`…) ou des modales jetables (fond grisé + carte, détruites à la fermeture : `PaliersPanel`, modale du dépôt).
- **Même rendu que la scène : on clone.** Pour qu'un bloc ajouté par code ressemble exactement à l'existant, on **instancie un élément natif** et on le retitre :
  - `LocataireFacturationFields.CloneSection` pour les cartes Dépôt / Facturation / Suivi ;
  - `LocataireFacturationFields.Clone` pour les champs ;
  - clone du bloc « Date de révision » pour toute saisie de date ;
  - clone du bouton « Réviser » pour « Modalités ».
- **Thème centralisé.** `UITheme` porte les couleurs et les **rôles de texte** (`UITheme.Role.Donnee`, `.Libelle`, `.Aide`…) : le code désigne un rôle, jamais une taille. `AppSectionColors` recolore toutes les cartes de section par nom, selon trois familles : vert (identité), ambre (argent), gris-bleu (notes). `AppIconResizer` uniformise les icônes.
- **Taille d'interface.** `UIZoom` agit sur la résolution de référence du `CanvasScaler`, pas sur la police : tout grossit ensemble, car 140 hauteurs sont écrites en dur. `ColonnesAdaptatives` empile les colonnes quand elles n'entrent plus.
- **Retours à l'utilisatrice.** `UndoToast` (message, éventuellement avec « Annuler ») et `ConfirmDialog` (toute action irréversible).
- **Pièges de mise en page connus.**
  - `childForceExpandHeight = false` sur les groupes : sinon le `flexibleHeight` remonte et étire les cartes.
  - Pas de `LayoutRebuilder.ForceRebuildLayoutImmediate` par champ : utiliser `MarkLayoutForRebuild`.
  - Une fiche construite **inactive** ne peut pas lancer de coroutine.
  - Des tests (`PrefabLayoutTests`) vérifient les cases à cocher et les groupes de hauteur fixe de tous les prefabs.

---

## 8. Modules métier

### 8.1 Loyer et révision — `RevisionLoyer/`

| Élément | Rôle |
|---|---|
| **`Loyers`** (statique) | Loyer dans le temps, en un seul endroit : `LoyerAnnuelA(date)` (historique d'avenant → palier → loyer courant), `MontantPeriode` (**au jour près** : prorata du début de bail, de la franchise, du départ, d'un avenant), paliers (`PoserPalier`, `Normaliser`, `Couvrent`), `EnregistrerAvenant`, `Actualiser`. |
| **`RevisionPanel`** (scène, `Canvas/Loyer`) | Deux volets. **Initialisation** : loyer de départ, périodicité, provisions et listes, type de révision, avenant, franchise, reprise (le départ du locataire se saisit dans la section Bail de la fiche) ; bouton *Initialiser / Réinitialiser / Modifier*. **Indice** : initialiser, puis réviser (appel INSEE). |
| **`PaliersPanel`** (code) | Tableau des paliers : couverture complète du bail exigée, palier suivant le lendemain du précédent, fin = veille d'un début de période ou fin du bail. |
| **`LoyerSummaryUI`** | Carte Loyer de la fiche : chiffres, récapitulatif, boutons « Initialiser / Modalités » et « Réviser / Paliers ». |
| **`InseeIndiceService`** | Séries INSEE (ILC, IRL, ILAT) : recherche avec repli (indice de départ) ou stricte (indice de révision). |
| **`LoyerHistoryService`** | Loyer réel année par année pour la rentabilité (références d'indexation, somme des périodes). |

Principe : **un palier commence toujours au 1er jour d'une période de facturation**, et la fin du bail seule n'arrête pas la facturation (tacite prolongation). Détail dans `FACTURATION_CIPL_PENNYLANE.md` §16.5.

### 8.2 Locataire et parcours de création — `Locataire/`

- **`Locataire`** : le modèle, et les règles du bail en méthodes statiques (`EcheancesTriennales`, `RenouvellementProche`, `ResiliationProche`, `DureeBail`, `VerifierBail`, `EstBailCommercial`…) ; propriétés dérivées `LoyerInitialise`, `DepotInitialise`, `RevisionIndiceSuivie`.
- **`LocatairePrefab`** : la fiche. Elle délègue à des composants ajoutés par code :
  - `LocataireBailFields` (type, durée, période ferme, fin calculée, départ du locataire) ;
  - `LocataireFacturationFields` (RIB, cartes Dépôt / Facturation / Suivi, modale du dépôt) ;
  - `ParcoursLocataireUI` (bandeau d'étapes) ;
  - `BailFileUI` (bail et avenants).
- **`ParcoursLocataire`** : **Général → Bail → Loyer → Dépôt**, état déduit de la fiche. Il verrouille le loyer avant le bail, le dépôt avant le loyer et **toute facturation avant la fin du parcours**. À la création, les écrans s'enchaînent seuls.

### 8.3 Facturation — `Facturation/`

Le chemin d'une facture :

```
 Panneau (FactureLoyerPanel / FactureRegulPanel / FactureRefacPanel / FactureDepotPanel)
   │  pré-remplissage : FactureInfo mémorisé + Loyers.MontantPeriode + ListesCharges
   │  textes : entête (ReglageData) → FactureVarResolver ({loc.*}, {bat.*}…), MentionTva
   ▼
 FactureEmission.Preparer(loc, clé)      ← décide : nouvelle facture / remplacement / correction
   ▼
 FacturePdfService                       ← gabarit HTML (StreamingAssets) → Edge headless → PDF + aperçu PNG
                                            (titre du PDF = nom du fichier, AvecTitre, 01/11)
   ▼
 Envoi selon ReglageData.modeEnvoi :
   ├─ EmailService (SMTP, confirmation obligatoire, rien sans destinataire explicite ; texte : celui de la
   │    facture, sinon le modèle de Réglages › Modèles d'e-mail, EmailService.Modele, 31/10)
   └─ (dépôt Pennylane retiré le 02/11 : la plateforme agréée prend le relais, `FactureElectronique`)
   ▼
 FactureEmission.Enregistrer             ← APRÈS un PDF réussi : FacturationSuivi.MarquerEnvoye / MarquerCorrige
```

| Élément | Rôle |
|---|---|
| **`FacturationSuivi`** | Machine à états des factures. Lignes planifiées d'une année (`Lignes`), clés stables (`loyer-2026-P3`, `regul-2025[-<liste>]`, `depot-2026`, `depot-initial`, `refac-<charge>`). États : À venir → À faire → En attente d'envoi → Envoyé → Impayé / Payé. Les états **transitoires** (Clôturé, Franchise, Hors bail) ne sont jamais stockés. Seules les lignes « touchées » ont un `FactureEtat`. Une ligne émise garde son libellé (le document fait foi) et sa périodicité (`FactureEtat.periodicite`, Revue 17 D4-04). |
| **`FactureEmission`** | La règle « une facture émise ne consomme jamais un second numéro », en un seul endroit pour les quatre panneaux. Et « Retour » refusé pendant un envoi (`RefuseFermeture`, D4-02) : fermer le panneau arrêtait l'envoi entre l'e-mail et l'enregistrement. Le numéro est décidé avant de construire le document (`Preparer` puis `NumeroDocument()`, D6-03), un numéro déjà pris est refusé (`Decision.Refus`, D4-04) et la séquence suit un ID saisi à la main (`NumeroSaisi`). |
| **`FacturationAlertes`** | Échéances (loyer à J-15, régul, dépôt) → pastilles de la fiche et « À traiter ». |
| **`ChargeBatiment`, `ListesCharges`, `ChargePanel`** | Charges du bâtiment, état **par locataire**, listes de charges (générale + spécifiques), quote-parts ; le formulaire montre la part de chacun en euros (`ChargePanel.Ratios`, même calcul que `ListesCharges.QuotePart`, 01/11). |
| **`HeritageFacture`** | Un nouveau locataire copie les réglages de facture du précédent (chaîne). |
| **`FactureEnvoiAuto`** | Au lancement, liste les factures prêtes dont la date d'envoi est atteinte. Rien ne part sans relecture. |
| **`FacturationHomeSection`** | Colonne « Créances » de l'accueil. |
| **`LocataireSuiviInline`, `SuiviRowUI`** | Tableau de suivi en bas de la fiche (prefab `SuiviFactureRow`). |
| **`FactureQuittanceService`, `FactureRappelService`** | Quittance (bail non commercial payé) ; rappel d'impayé en brouillon `mailto:`. |
| **`ReglagePanel`, `ReglageService`, `ReglageData`, `RegleRib`** | Réglages d'entreprise et secrets ; `RegleRib` contrôle un RIB à « Valider » (clé de l'IBAN, BIC, n° de RIB). |
| **`ExplicationDepot`** | Bloc explicatif imprimé sur la facture de dépôt. |

### 8.4 Accueil et alertes — `General/`

`GeneralMenuPanel` compose l'accueil :
- les indicateurs globaux (tuiles partagées avec `BatimentSummaryView.KpiTile`) ;
- **« À traiter »**, alimenté par `HomeAlertCollector.Collect` : fins de bail, résiliations triennales, révisions d'indice, facturation, objectifs ;
- **« Créances »** ;
- la liste des bâtiments ;
- le calcul rapide et les objectifs globaux.

La même collecte d'alertes alimente la carte « À traiter » du résumé de bâtiment.

### 8.5 Bâtiment, rentabilité, carte, PLU

- `BatimentSummaryView` : carte d'identité, tuiles d'indicateurs, liste des locataires.
- `RentabiliteGlobaleController`, `TravauxController`, `RentabiliteCalculator` : tableaux de rentabilité (loyers réels par année via `LoyerHistoryService`).
- `MapController`, `GeoCodingService`, `TileLoader`, `MapThumbnailService` : carte satellite et géocodage, avec la carte conservée tant que l'adresse ne change pas. Étape 16b : l'image vient de Google (Maps Static API) par la fonction serveur `cartes` (`TileLoader.Image`, sur le cloud seulement) ; les zooms des prefabs restent ceux de Mapbox, Google en prend un de plus (`TileLoader.DecalageZoom`).
- `PLUOverlayPanel`, `PLUService`, `CadastreService` : zone PLU à partir d'une adresse ou d'une parcelle.

### 8.6 Résumé d'entreprise des locataires — `Pappers/`

`PappersSync` interroge l'annuaire des entreprises pour tous les locataires (au lancement, et à l'enregistrement d'une fiche). `PappersResume` ne remplace que le bloc délimité dans le commentaire du locataire.

Sur le cloud (étape 16b), la tournée du jour (`DemarrageCloud.TourneeAnnuaire`) et l'enregistrement d'une fiche passent par `PappersSync.ToutCloud` / `Appliquer` : le BODACC d'abord (40 sociétés par appel ; s'il ne répond pas, rien n'est écrit ce jour-là), puis la fiche de chaque locataire par la fonction serveur `pappers`, qui ne relit Pappers qu'une fois par mois et par SIREN pour tout CIPL (table `pappers_fiches`). `SocieteLocataire` (testé) construit le bloc (capital, dirigeants, finances, procédures, ligne « État » : Fermée, En difficulté, À surveiller, Saine) et crée les objectifs « Déclarer la créance au mandataire » (procédure ouverte publiée il y a moins de 2 mois, échéance à 2 mois) et « Vérifier le bail » (société fermée), une seule fois chacun (`Locataire.signalementsSociete`).

---

## 9. Services externes

| Service | Usage | Code | Authentification |
|---|---|---|---|
| INSEE — `bdm.insee.fr` (SDMX) | Séries d'indices ILC / IRL / ILAT | `InseeIndiceService` | aucune |
| Pennylane | **Retiré le 02/11** (décision : CIPL passe par Super PDP) ; `PennylaneClient` supprimé | — | — |
| SMTP (OVH, Orange, Free, iCloud…) | Envoi des factures par email quand l'adresse n'est pas reliée | `EmailService` (`System.Net.Mail`, STARTTLS) | mot de passe (secrets de l'entreprise) |
| API Gmail — `gmail.googleapis.com` / Microsoft Graph — `graph.microsoft.com` (étape 16a) | Envoi depuis une adresse Gmail, Google Workspace, Outlook, Hotmail, Microsoft 365 reliée par « Se connecter avec … » | `Messagerie` (message MIME pour Gmail, `sendMail` pour Graph) | jeton d'une heure donné par la fonction serveur `messagerie`, qui garde l'autorisation (table `messageries`) |
| DNS de Google — `dns.google` | Serveurs de courrier (MX) d'un domaine propre, pour proposer le bon bouton ou le bon serveur SMTP | `Messagerie.DevinerEnLigne` | aucune |
| Microsoft Edge (local) | HTML → PDF / PNG en mode headless | `FacturePdfService` | — (Edge doit être installé) |
| Annuaire des entreprises — `recherche-entreprises.api.gouv.fr` | Résumé société par SIRET | `PapperService`, `PappersSync` | aucune |
| Google Maps Static API — `maps.googleapis.com` (étape 16b, à la place de Mapbox) | Carte satellite de la fiche bâtiment et vignettes de l'accueil | `TileLoader.Image`, `MapThumbnailService`, par la fonction serveur `cartes` | clé de CIPL `GOOGLE_MAPS_KEY` (Secrets de Supabase), plafond par jour dans la console Google |
| Pappers — `api.pappers.fr` (étape 16b) | Fiche des sociétés locataires (capital, dirigeants, finances, fermeture) | `PappersSync` (cloud), par la fonction serveur `pappers` | clé de CIPL `PAPPERS_API_KEY` (Secrets de Supabase) ; une lecture par société et par mois |
| Super PDP — `api.superpdp.tech` (étape 18a, plateforme agréée) | Relier chaque SCI (fenêtre d'autorisation, vérification du gérant) ; 18b : transmettre les loyers (et, depuis le 02/11, régularisations et refacturations) des locataires assujettis avec un SIRET (`FactureEn16931` → Factur-X assemblé par Super PDP, UBL si le destinataire refuse le Factur-X ; adresse de l'acheteur lue dans l'annuaire ; en bac à sable, vendeur et acheteur repris de la facture d'essai `generate_test_invoice`) et déclarer « Encaissée » (fr:212, un bloc MEN par taux de TVA, ventilation relue chez Super PDP ; annulation = montant négatif) ; une seule transmission par facture (réservation `plateformes_envois`, revue 16c) ; e-mail au locataire : copie du PDF, puis dès le 1/9/2027 (`FactureElectronique.Bascule`) une simple notification après l'acceptation (modèle `ReglageData.notifObjet` / `notifCorps`) | `FactureElectronique` (`Rattraper` après l'émission ou un paiement, `Demarrer` au lancement), par les fonctions serveur `facture-electronique` et `facture-electronique-retour` | application OAuth de CIPL (`SUPERPDP_CLIENT_ID`, `SUPERPDP_CLIENT_SECRET`) ; une autorisation par SCI, gardée et renouvelée sur le serveur (table `plateformes`) |
| BODACC — `bodacc-datadila.opendatasoft.com` (étape 16b) | Procédures collectives des sociétés locataires | `PappersSync.Bodacc`, `SocieteLocataire.LireBodacc` | aucune |
| Base Adresse Nationale — `api-adresse.data.gouv.fr`, puis Nominatim en repli | Adresse → coordonnées | `GeoCodingService`, `CadastreService` | aucune |
| IGN — `apicarto.ign.fr` (`/api/cadastre`, `/api/gpu`) | Parcelle cadastrale, zone PLU | `CadastreService`, `PLUService` | aucune |
| Géoportail de l'urbanisme | Lien ouvert dans le navigateur | `PLUOverlayPanel` | — |

Tous les appels réseau passent par `UnityWebRequest` dans des coroutines, sauf l'envoi d'email : il tourne sur un thread de fond, et l'UI attend son résultat en coroutine.

**Réglages (réorganisés le 27/10)** : onglets Entreprise (nom, SIRET modifiable par l'admin via `changer_siret`, RIB, export), Facturation (logo, entêtes, listes de charges, textes fixes), Utilisateurs et Abonnement (admin), Documents et affichage, Intégrations (une section par service, pour l'admin : Envoi des factures, Pennylane, E-mail ; la section Cartes (Mapbox) a disparu à l'étape 16b, et avec elle l'onglet pour les autres). `ReglagePanel.Onglets` dit qui voit quoi.

**Boutons « Tester » et refus en clair (étape 16)** : les trois services qui demandent un identifiant ont un bouton « Tester » dans Réglages, à droite de leur champ (`ReglagePanel.ChampTeste`, résultat en vert ou en rouge dessous, échec aussi au centre). Le test porte sur ce qui est tapé, sans enregistrer. Pennylane : `PennylaneClient.Tester` (`GET /me`, qui nomme la société de la clé quels que soient ses droits, puis lecture d'un client et d'une facture). Mapbox : `TileLoader.Tester` (une carte de 1 × 1 pixel), retiré à l'étape 16b avec le token. SMTP : l'e-mail de test, avec `EmailService.Envoyer(…, reglage, motDePasse)`. Les refus sont traduits par `EmailService.Clair` (serveur introuvable, identifiant refusé, SMTP désactivé chez Microsoft 365, pas de réponse sur le port), `PennylaneClient.Clair` et `TileLoader.Clair` (codes HTTP 0, 401, 403, 429, 5xx), aussi pour les vrais envois : la phrase claire d'abord, puis « Détail technique : … ». Les autres services n'ont pas d'identifiant, donc rien à tester.

---

## 10. Tests et outillage

- **Tests EditMode (NUnit)** dans `Assets/Editor/Tests` : **345 tests, tous verts** au 30/09. Ils couvrent surtout les règles métier (statiques, sans scène) :
  - suivi et émission de factures ;
  - listes de charges ;
  - bail ;
  - révision et paliers (`RevisionLoyerTests`) ;
  - héritage des réglages de facture ;
  - transfert d'entreprise ;
  - données disque.
  
  Certains tests chargent les **prefabs** pour vérifier des invariants de mise en page (`PrefabLayoutTests`, `SuiviRowPrefabTests`, `DimensionsTests`, `EchelleTypoTests`).
- **Lancer les tests** : Unity → *Window › General › Test Runner › EditMode*, ou par l'outil MCP `tests-run`. **Piège** : lancé pendant le mode Play, le lanceur MCP reste verrouillé. Il faut sortir du Play, enregistrer la scène, et effacer au besoin les clés `SessionState` du plugin (procédure dans la mémoire du projet).
- **Pratique** : une règle métier nouvelle ou corrigée laisse **au moins un test** qui échouerait si la règle cassait. Et « compiler ne prouve rien » : une modification n'est considérée comme validée qu'après exécution (tests, ou vérification en Play).
- `Editor/MesureEcran.cs` : mesure en Play des écrans construits par code (menu général, fiches résumé, facturation), qu'aucun test sur prefab ne peut mesurer.
- `Editor/ConstruireInstalleur.cs` (étape 15) : menu **CIPL › Construire l'installeur** : build Windows, `version.json` et installeur Inno Setup (`Installeur/CIPL.iss`, à la racine du dépôt) dans `Build/Installation`, à côté du dépôt. La version se règle dans Project Settings › Player › Version ; la version minimale exigée par le serveur est `ConstruireInstalleur.VersionMinimale`. Avant la sortie (dépôt GitHub privé), la version installée lit `version.json` et l'installeur dans le dossier indiqué par `cipl_mises_a_jour.txt` (dossier caché de CIPL, `MiseAJour.FichierEssai`). Étape 15b : menu **CIPL › Construire la version Mac** (build macOS depuis Windows, module « Mac Build Support (Mono) » requis ; `CIPL-mac.zip` qui garde le droit d'exécution ; lien Mac ajouté au `version.json` de la même version) ; identifiant de l'application `fr.cipl.cipl`, à ne plus changer. Identité interne (société et produit `CIPL` / `CIPL`, donc `AppData\LocalLow\CIPL\CIPL`) : fixée le 26/10, à ne plus changer.

---

## 11. Conventions de code

- **Français partout** (types, méthodes, champs, commentaires, messages). Les commentaires expliquent **pourquoi**, souvent avec l'histoire du défaut qui a motivé le code : ne pas les supprimer sans raison.
- **Une règle, un seul endroit.** Quand une règle était recopiée dans plusieurs écrans, elle a fini fausse dans l'un d'eux (H2, H2-bis, la garde « Quittance »…). Les règles vivent dans des classes statiques (`FactureEmission`, `Loyers`, `FacturationSuivi`, `ListesCharges`, `ParcoursLocataire`), et les écrans les appellent.
- **Aucun échec silencieux.** Pas de `catch {}` vide sur un chemin de données. Une écriture ratée se signale (`AtomicFile` renvoie `false`), un fichier illisible est listé, une donnée incohérente est tracée par `Debug.LogWarning`.
- **Rien d'irréversible sans confirmation** : envoi d'email, suppression, avoir, finalisation Pennylane (non écrite volontairement).
- **Valeurs par défaut sûres** pour les anciens fichiers (voir §4.2), et **états déduits** plutôt que stockés.
- **UI par code : nommer les objets créés** (`"FranchiseBlock"`, `"ParcoursLocataire"`…) pour les retrouver dans la hiérarchie et dans les tests.

---

## 12. Points d'attention et dette connue

| Sujet | Situation |
|---|---|
| **Numérotation des factures (H1)** | Deux locataires peuvent obtenir le même numéro le même mois. En attente de l'expert-comptable (conformité art. 242 nonies A du CGI). Point unique à modifier : `FactureNumerotation.Prefixe` + `factureSeq`. |
| **Pennylane** | Mode retiré le 02/11 ; plus rien à faire. |
| **Dépendance à Windows / Edge** | La génération PDF cherche `msedge.exe` aux emplacements standards. |
| **Grosses classes UI** | `RevisionPanel` (≈ 1 300 lignes), `FactureLoyerPanel` (≈ 1 200), `FactureRegulPanel` (≈ 1 050), `LocataireFacturationFields` (≈ 900). À découper si elles continuent de grossir ; la logique métier en est déjà sortie. |
| **Une seule assembly** | Pas d'`.asmdef` : toute modification recompile tout. Acceptable à cette taille. |
| **API Unity obsolètes** | Avertissements `FindObjectOfType` et `TMP_Text.enableWordWrapping`, sans effet aujourd'hui. |
| **Mises en page forcées** | Une vingtaine de `ForceRebuildLayoutImmediate` subsistent hors des chemins mesurés (Achat, Travaux, PLU, résumé, objectifs). |
| **Données de test Pennylane** | Clients et factures TEST, locataire « TEST Pennylane — Volteo » dans DemoCIPL, entreprise « Test transfert » : à nettoyer. |

---

## 13. Où modifier quoi

| Je veux… | Aller dans |
|---|---|
| Changer le calcul du loyer d'une période, les paliers, la franchise, l'avenant | `RevisionLoyer/Loyers.cs` |
| Modifier l'écran « Initialiser le loyer » ou la révision par indice | `RevisionLoyer/RevisionPanel.cs` (+ prefab `Revision Loyer`) |
| Changer les états, clés ou échéances du suivi de facturation | `Facturation/FacturationSuivi.cs` |
| Changer une alerte d'échéance | `Facturation/FacturationAlertes.cs`, `General/HomeAlert.cs` |
| Modifier le contenu d'une facture imprimée | `StreamingAssets/*_template.html` + `Facturation/FacturePdfService.cs` |
| Ajouter une variable de texte `{…}` | `Facturation/FactureVariables.cs` (catalogue) + `FactureVarResolver.cs` |
| Changer la numérotation | `Facturation/FactureInfo.cs` (`FactureNumerotation`) — voir H1 |
| Ajouter un champ au locataire | `Locataire/Locataire.cs` (valeur par défaut compatible, test JSON), fiche `LocatairePrefab.cs` |
| Ajouter une étape ou un verrou au parcours de création | `Locataire/ParcoursLocataireUI.cs` |
| Changer un chemin de dossier sur le disque | `Save/DossiersDonnees.cs` (+ migration si des données existent) |
| Changer une couleur, une taille de texte | `Tools/UITheme.cs`, `General/AppSectionColors.cs` |
| Ajouter un réglage d'entreprise | `Facturation/ReglageData.cs` + `ReglagePanel.cs` (jamais de secret dans le JSON) |

## Transfert entre entreprises du cloud (01/11)

- **Client** : `TransfertCloud` (règle `Bloque` : refus tant que Super PDP a du travail en attente ; `Preparer` : aperçu ; `Executer` : documents copiés, fonction SQL, origine marquée), `TransfertEntrepriseUI` (bouton « Transférer » réservé à un admin sur le cloud, choix de l'entreprise du groupe, confirmation, rechargement). `FactureEtat.plateformeOrigine` / `plateformeOrigineId` : facture transmise par une autre entreprise (« transmise par SCI X ») ; rien n'y est renvoyé, mais « Encaissée » se déclare au nom de SCI X (`FactureElectronique.ATraiter`, `Demande.entreprise` = l'origine) ; `TransfertCloud.MarquerOrigine` efface la marque au retour chez l'origine.
- **Serveur** (`Serveur/etape16c3_supabase.sql`) : fonction `transferer_fiches(p_de, p_vers, p_batiment, p_locataires, p_seul, p_versions, p_session, p_reglages, p_reglages_version)` — une transaction ; table `transferts_corbeille` (version de destination d'un homonyme, lisible par les admins). Les documents gardent leur chemin ; l'objet `documents/<entreprise>/<empreinte>` est copié sous le préfixe de la destination.

## Mineurs du lot 5, partie client (02/11)

- `HorsLigne.Panne(ok, code, fonctionServeur)` et `Constater(…, fonctionServeur)` ; `CloudClient.Envoyer(…, fonction: true)` pour `FonctionServeur`. `CloudClient.Q` : l'échappement JSON unique (les `Q` privés de `EcritureCloud`, `ReglagesCloud`, `RegleInscription` y renvoient).
- `EcritureCloud.Envoyer(avant, b, silencieux, e)` et `ReglagesCloud.EnvoyerApres(avant, e)` : l'entreprise capturée à la mise en file ; `ModifierSiLibre` vérifie la fiche avant d'ouvrir ; `MotifRefus` connaît `supprimee` ; `SiAucunBatimentPris` n'a plus d'appelant (ReglagePanel passe par `Reserver` + `Attendre`).
- `CopieLocale.Effacer(entrepriseId = null)` ; `DemarrageCloud.Exclu` n'efface que la copie de l'entreprise quittée ; `Ouvrir` vide l'écran sur un échec de lecture.
- `AbonnementCloud.Constater` crée l'état « expire » si rien n'a été lu ; `AbonnementCloud.Oublier()` pour les tests ; champs `licences_max`/`licences_utilisees` retirés de `Lu` et `Annuaire` (gardés sur `Entreprise`).
- `EcransCloud.AfficherCode(canvas, fait)` : fenêtre « Code d'invitation » ; bouton dans `Choisir` (liste vide) et lien dans `MenuCompte`.
- Retirés : `BatimentManager.DemarrageLocal`, `SaveAll`, `SauvegardeDeFermeture`, `OnApplicationQuit`, `_racineChargement`, `SessionCloud.LectureSeule` et sa branche dans `SaveBatiment`.
- **Serveur (02/11)** : `Serveur/etape16c4_supabase.sql` (droits, `reserver_envoi`, `supprimer_fiche`, `enregistrer_*` → refus `supprimee`, `inviter`/`changer_acces` → `for update`, table `pappers_journal (entreprise_id, jour, n)`, objets `essai_*` retirés). `stripe-webhook.ts` : `appliquer(sub, avant)` ; `facture-electronique-retour.ts` : `delete().select()` ; `pappers.ts` : `QUOTA`, refus `quota` ; `messagerie.ts` : garde `id_token` ; `abonnement-stripe.ts` : `contact()` → `customers.update`. Client : `CloudClient.Deconnecter` (POST /auth/v1/logout), `SessionCloud.CleUtilisateur` (PlayerPrefs), `PappersSync.QuotaAtteint`.

## Mineurs du lot 6, facturation (02/11)

- `FactureEnvoiAuto.DoitPartir` applique `FacturationSuivi.PeutPartir` (seul le loyer a une date d'envoi) ; `Contexte` ne met pas de TVA sur un dépôt. `FactureRappelService.Adresse(loc, ligne)` = `FactureEnvoiAuto.Destinataire`.
- `FactureRegulPanel.MontantsParListe(listes, chargesDe, provisionDe, periodesFacturees, ttcTotal)` (statique, testée) ; `SaveFromUI()` sans paramètre. `FactureEmission.SansEmail` (message commun des trois panneaux) ; `ReglageData.modeEnvoi` = Email par défaut.
- `FactureEn16931` : `PenalitesDefaut`, `IndemniteDefaut`, `MentionsRetard(saisie)` ; ligne négative = quantité −1 et prix positif ; total négatif = avoir (381) aux montants positifs, lignes inversées (BI5, 02/11) ; `LignesRegul` (régul : charges, déjà refacturé, provisions) ; `Partie(…, vendeur: true)` seul porte `vat_identifier`. `FacturePdfService` : `{{LEGAL}}` = `MentionsRetard`, mention TVA échappée.
- `FactureLoyerPanel` : `Cents`, `BuildData` au centime, refus d'un total ≤ 0 pour la plateforme ; `ShowApercu`/`Modal` retirés.
- `FactureVarResolver.Resolve(texte, loc, bat, ctx, valeur)` : chaque valeur passe par `valeur` (échappement) ; `ExplicationDepot.Phrase` l'utilise avec `H`. `FactureElectronique.Phrase(Reponse)` : la phrase du résumé. Lot 7 (02/11) : `Locataire.EstPartiLe(date)` ; `IndiceImmo.ICC` (série 000008630) ; `LoyerHistoryService.HistoriqueCouvre` ; `Objectifs.Suivante(…, jourOrigine)` ; `SaisieNumerique.TryParse(out double)` et `ParseOuAvertirD` ; `MapThumbnailService.EstEnCache` ; `PLUService.FetchZone` public ; `SaveLocationService.AncienneRacineDeplacee` ; `TransfertEntreprise.CopierFichiers(…, racineDest)` ; TVA (02/11) : `Tva` (règle entreprise › bail › facture), `ReglageData.loyersSoumisTva/tauxTva/mentionTvaExoneration`, `Locataire.tvaLoyer/tauxTvaLoyer`, `FactureInfo.tauxTva`, `FactureEtat.tauxTva`, `TauxFacture()` dans les trois panneaux ; `Locataire.assujettiTva` retiré le 02/11 (`FactureElectronique.Concerne`/`SansSiret` = `Tva.Soumis` + SIREN) ; `UIFactory.MontrerAvecLibelle`, `LigneMontant` ; Revue 17 lot 1 (03/11) : `DonneesCloud.Modele` envoyé en `p_modele` (Serveur/etape17a_supabase.sql, colonne `modele`), `FactureEn16931.NumeroDe` / `SansEscompte`, `FactureElectronique.RefusCorrection` / `Professionnel`, `RegleInscription.ReglagesAjout`, `ListesCharges.PayeursSansPart` / `PartsEffectives` (retour BL4), `ChargeBatiment.ponctuelle` et `Loyers.PresentLe` (période d'une charge, 04/11, modèle des données 2), `ListesCharges.LocauxVides` et `ChargePanel.DetailDuPartage` (locaux vides au propriétaire, montants au prorata, retour BL8), `LotsBatiment` et `LotsPanel` (lots déclarés dans le bâtiment, 04/11, modèle des données 3), `ReglageData.phraseEscompte` et `FactureEn16931.Escompte` (phrase d'escompte réglable, 04/11, modèle 4), `FermetureGuard.Decision` / `Avertissement`, `EcritureCloud.NomsEnAttente` ; mode local d'avant le cloud supprimé (`BackupService`, `MigrationDossiers`, `SaveLocationMenu`, `EntreprisePanel`, `EntrepriseService.Activer/Creer/Ouvrir`). Revue 17, lot 2a (04/11) : `Tva.Centimes` / `Cents` / `PeriodeDepot` (D5-03, D6-06), `RegleAbonnement.TexteAjoutEntreprise` (D3-05), `BatimentSummaryView.AcquisitionSaisie` (D7a-06), `LotsBatiment.Occupes` (D7a-05), `FacturationSuivi.ApresCorrection` / `TropPercu` (D4-03), `FactureEmission.Preparer(…, periodicite)` (D4-04), `Cessions.TitulaireDe` / `TitulaireLigne` (D7a-03) ; retours BN : `SaisieNumerique.Montant` / `CaractereMontant` (champ de montant, virgule acceptée), `RevisionPanel.MoisStandard` / `RefusMois`, `Cessions.Etiquette` / `TexteEntete`, `FactureElectronique.TransmiseALinstant` (« Encaissée » attend la lecture de Super PDP), `FacturationSuivi.ARembourser` / `EnregistrerRemboursement` et `Etat.ARembourser` (trop-perçu, avoir : « À rembourser »), `VersementsPanel.Rembourser`, `FactureLoyerPanel.SiretPourPlateforme`.
- Serveur : `facture-electronique-retour.ts` n'écrit plus `plateformes.societe` (colonne gardée).

## Revue 17, thème 2c, lots 1 à 5 (06/11)

- `FacturationSuivi.Partie(loc, key)` : facture partie (envoyée, impayée, payée), sans « À faire » ni « Automatique » ; `SetStatut` refuse de la dé-émettre, sauf `DepartLocataire.PeriodeApresDepart` ; `DepartLocataire.ConfirmationRetrait(loc, ligne)` : texte de la fenêtre « Retirer ce loyer du suivi ? ».
- `FactureEmission.Preparer` : une ligne « remise » (numéro et PDF, statut vide) se refait sous son numéro (`Reecriture`) ; `FactureEmission.SeraUneCorrection(loc, key)` : titre « (correction) » et « Corriger et envoyer ».
- `FactureLoyerPanel.PeriodeAOuvrir(loc, auj)` : la période à envoyer (parcourt 1..`NbPeriodes` elle-même : à faire suivre à l'avenant de périodicité, lots 14-15) ; `HomeAlert.cleFacture` : la période dont parle une alerte de loyer, ouverte par `RappelsAuto.Ouvrir`.
- `SaisieDate.Refus(saisie, quoi)`, `SaisieDate.RefusFacture(date, echeance)` (avant `SaveFromUI` dans `GenererApercu` et `SauvegarderEtEnvoyer` des quatre formulaires) ; `SaisieDate.TryParse` lit tirets et points, années à deux chiffres jusqu'en 2099 ; `ChargePanel.Refus(nom, cout, date)`.
- `ChargeBatiment.RefusSuppression(charge, locataires)` : refus au centre dans `InvestissementListPanel`, avant la confirmation.
- `FactureVariables.GroupesLimites` et `SlashAutocomplete.Proposees(query, groupesEnPlus)` : variables proposées par champ ; `SlashAutocomplete.OnDisable` ferme le menu ; `ExplicationDepot.Html` reçoit le contexte de la facture.
- `FactureEmission.EstPhraseUsine(phrase)`, `PhraseAMemoriser(phrase)` : la phrase de règlement d'usine n'est pas mémorisée par les quatre formulaires.
- Retours de BP1 (06/11), bouton « Auto » du suivi : `FacturationSuivi.AvecBoutonAuto(loc, ligne)` (ligne pas encore partie ni grisée), `AutoAllume(rec)` (statut vide ou « AttenteEnvoi »), `StatutApresBoutonAuto(loc, rec)` (allumé → « AFaire » ; éteint → `StatutAutomatique`) ; `SuiviRowUI.Setup(…, bool? auto, Action onAuto)` : bouton créé par code au premier besoin (`ObjectifsTableau.Pastille` + `Button`), caché dans `SetupEntete` et `SetupVide` ; `LocataireSuiviInline.BasculerAuto`, `AppliquerStatut` ; le menu de la pastille ne garde que les faits, plus « Retirer du suivi… » (exception du départ).
- `FactureEmission.NumeroRepris(loc, key, periodicite)` : le numéro que l'émission garde (correction ou réécriture, sans refus), sinon null ; `FactureEmission.AfficherNumero(aide, prefixe, id, repris, prefixeNeuf, seq, aideRepris, ref idGarde)` : la ligne « N° de facture » des quatre formulaires ; `SeraUneCorrection(loc, key, periodicite)` ne compte pas une émission refusée ; `FactureLoyerPanel.CleLoyer()`. Le champ ID efface `_numeroEmis` (aperçu après une émission abandonnée).
- `FacturationSuivi.StatutAutomatique(loc, rec)` : le statut quand on rallume le bouton « Auto » : « AttenteEnvoi » pour une facture préparée puis bloquée « À faire », avec numéro et PDF, si `FactureToujoursJuste(loc, rec)` ; sinon "" (état d'après les dates). Relecture du 07/11 : `DepartLocataire.Retirable(loc, ligne)` (menu « Retirer du suivi… »), `ARetirer` publique, `LoyerACorriger` compte un loyer retirable ; `FacturationSuivi.AFacture(loc, key, out rec)` (lu par `Regroupable`, `FactureRegulPanel.ChargesDeRegul`, `FactureRefacPanel.MontantPropose`) ; une facture « AFaire » avec numéro et PDF reste dans `Lignes` ; l'alerte d'un loyer à retirer est de type `Depart` avec sa clé : `RappelsAuto.Ouvrir` et l'étape « Dernier loyer » montrent le suivi (`LocataireSuiviInline.MontrerPour`) ; `AvecBoutonAuto` exclut une ligne au statut vide qui garde numéro et PDF ; `SetStatut` refuse aussi « AttenteEnvoi » sur une facture partie.

## Revue 17, reprise du 09/11, lot A (09/11)

- `FacturePdfService.QuittanceData.societe` et gabarit `{{SOCIETE}}` (quittance ; alt des logos vidé) ; `FacturePdfService.LieuLe()` et gabarits `{{LIEU_LE}}` (« Toulouse, le » ou « Le ») ; `LieuDefaut` ne sert plus qu'aux Réglages d'avant ; `RegleInscription.Reglages` pose `lieuEmission` vide sans ville connue.
- `FactureDepotPanel.AncienPropose(loc, f, dejaFacturee)` (lu par LoadIntoUI avec `FacturationSuivi.AFacture`), `FactureDepotPanel.RefusOuverture(loc, ligne, aujourdhui)` (en tête d'`OpenDepot`).
- `FermetureGuard.EnvoiEnCours`, `QuitterApresAttente(avant, apres, erreurAffichee)`, `Bouton(verbe, enEdition)`, `Demander(quitter)`, `Decision(…, envoiEnCours)`, `Avertissement(enEdition, enAttente, action, envoiEnCours)` ; `EcritureCloud.QuandTermine` retirée (boucle privée de la garde sur `EcritureCloud.Attendre`) ; `EcritureCloud.NomsEnAttente` ignore les fiches périmées ; `EcritureCloud.MonNom()` ; `FactureEnvoiAuto.EnCours`.
- `DepartLocataire.RefusSuppression(Locataire)` et `RefusSuppression(Batiment)` ; `FactureElectronique.Cle` (empreinte SHA-256 au-delà de 36).

## Revue 17, reprise du 09/11, lot B (09/11)

- Session : `CloudClient.Deconnecter` (scope=local), `CloudClient.Session.ExpireLe` (= maintenant + expires_in, posé par `LireSession`), `SessionCloud.RefusDefinitif(code)`, `SessionCloud.Fermee` (message au centre puis `DemarrageCloud.SeDeconnecter`), `Restaurer` tolérant au 429 ; `CloudClient.FicheEssai` / `Fiches` / `LireFiches` retirés, `EssaiCloudWindow` réduite.
- Abonnement et comptes : `AbonnementCloud.Lire` (état gardé sur échec), `RegleAbonnement.LicencesProposees(utilisees)`, `SectionAbonnement._entreprise` (choix et attente par entreprise, fin après 10 minutes), `UtilisateursCloud.EnvoyerEmail` (invitant = `EcritureCloud.MonNom`), `UtilisateursCloud.Motif` (« dernier_admin » nommé, « membre », « deja_membre »), `PremiersPas.Etat.expire`, `EcransCloud.AfficherInvitation` (session oubliée si l'adresse change), `ReglagesCloud` (refus « admin » traduit, relecture), `SyncDocuments.ReserveAdmin` (DocumentsCloud), `TileLoader.Motif` (« quota »), `RegleInscription.Reglages` (loyersSoumisTva = societe), `ReglagePanel.OpenRibForm` (exemples neutres).
- Serveur : `Serveur/etape17c_supabase.sql` (sections 1 à 7 : detenteur_autre, documenter, supprimer_document, enregistrer_reglages, retirer_membre, changer_role, changer_acces, creer_entreprise_import, prendre_lecture_pappers, cartes_journal, prendre_lecture_cartes ; repère « Lot D : à compléter ») ; fonctions Deno abonnement-stripe (always_invoice, métadonnées l_<entreprise>, Number.isInteger, impayé refusé), stripe-webhook (repartir par clés l_, canceled / incomplete_expired, resource_missing, page de retour), emails-abonnement (always_invoice, textes), facture-electronique (réservation rendue, identifiant contrôlé), pappers et cartes (réservation avant l'appel, QUOTA).
- Tests : `LotBStripeTests`, `LotBPlateformeTests`, `LotBSqlTests` (dont la comparaison de chaque fonction recréée à sa dernière définition), `LotBSessionTests`, `LotBEcransTests` ; `CloudAbonnementTests` (PSiret), `CloudInscriptionTests`, `CloudPremiersPasTests`, `CloudTests` et `Revue17Tests.D1_01` ajustés.

## Revue 17, lot C (facturation), vague 1 : lots 6, 6b, 7 et 9 (09/11)

- Lot 6 (modèle 8) : `FactureEtat.destinataire`, `acheteur` ; `FacturationSuivi.PoserAcheteur(loc, key, acheteur, destinataire)` ; `FactureEnvoiAuto.Destinataire(loc, type, rec)`, `DoitPartir` (HorsTitulaire), `Diagnostic` ; `CessionBail.cs` : `Cessions.AcheteurDe`, `AutreTitulaire`, `HorsTitulaire`, `Figer`, `Reporter` (cession prévue seulement), `CessionDe` / `PrevueDe` / `FaiteDe` (privées), `TitulaireLigne` (rec.periodicite) ; `FactureRappelService.Adresse(loc, rec)` ; `FactureElectronique.Notifier` ; `FactureQuittanceService.Client(loc, l)` ; les quatre `Finaliser(…, dest)` ; `FactureLoyerPanel.RefreshTitulaire`, refus de l'envoi différé sans adresse ; `_acheteurAutre` des trois autres formulaires ; `LocataireSuiviInline.Libelle` (annotation « à refaire »).
- Lot 6b (modèle 8) : `FactureEtat.retireApresEnvoi`, `factureCitee` ; `FactureEmission.RefusRetire(FactureEtat)` (lu par `Preparer` et `LocataireSuiviInline.OuvrirGeneration`) ; `FacturationSuivi.SetStatut` (pose / lève le marqueur), `Verse(loc, key)`, `PoserFactureCitee(loc, key, numeroCite)`, `ARembourser` et `DuesDe` (facture citée) ; `FactureRefacPanel.Finaliser` ; `DepartLocataire.ConfirmationRetrait`.
- Lot 7 : `FactureElectronique.ArreteLaTournee(Reponse)`, `Cumul(a, b)`, `RefusNomme(loc, r, rep, quoi, id)`, `NoterRefusEncaissement`, `RefuseeCetteSession`, `Panne` (suite selon le refus), `RefusCorrection` (plateformeOrigine), `RecopierPlateforme` (tous les champs plateforme* / encaissee*, retour « AttenteEnvoi »), `ATraiter`, `Demarrer`, `Rattraper`, `Traiter` (refus cumulés).
- Lot 9 (modèle 9) : `FactureEtat.encaisseeAAnnulerId` ; `FactureEn16931.TotalDe(json)` ; `FactureElectronique.Preparer` (pose l'annulation), `ATransmettre(FactureEtat)`, `EnAttente`, `Traiter` (annulation en tête, avant « envoyer » ; l'« Encaissée » attend).
- Tests : `Assets/Editor/Tests/LotCTests.cs` (lots 6, 6b, 7, 9), `Revue17Tests.D2_01` (empreinte 9:edb166c9b1f5), `Revue17Tests.D4_02_le_rappel_de_l_avoir…` ajusté.

## Revue 17, lot C (facturation), vague 2 : lots 10 à 13 et décisions du 10/11 (10/11)

- Lot 10 : `Tva.FacturePartie(ligne)`, `Tva.TauxPropose(loc, f, ligneCiblee, r)` ; dans `FactureLoyerPanel`, `FactureRegulPanel`, `FactureRefacPanel` : `Corrigee()`, `ChargerTva(f)`, `TauxFacture()`, `RefreshTauxDefault()`, `RefreshModeEnvoi()` ; `SaveFromUI` n'écrit le taux (et, décision du 10/11, la mention TVA) que si `Corrigee()` est nul ; `FactureElectronique.Concerne(loc, tva, siret)`, `SansSiret`, `LibelleEnvoi(loc, tva, siret, notification)` au taux du document.
- Lot 11 : `FactureElectronique.Deliee`, `DoitPartirParPlateforme(tva, siretAcheteur, depart)`, `RaisonHorsPlateforme(loc, tva, siretFormulaire)`, `RefusCorrection(avant, ttc, sujet, parPlateforme, raison)`, `RefusObligation(loc, tva, siretFormulaire, sujet, parPlateforme, depart)` ; formulaires : `Finaliser` et `EnvoyerPuisFinaliser` reçoivent `parPlateforme`, comme `PreparerPourEnvoi` du loyer.
- Lot 12 : `FactureElectronique.PartParPlateforme(FactureEtat)`, `ARefaire(loc, r, jour)` ; `FactureEnvoiAuto.SiretLigne`, `ParPlateformeSeule`, `Retenue`, `Retenues`, `MessageRetenues`, `Vers` ; `ProposerAuDemarrage` nomme les retenues dans un `finally` ; `DemarrageCloud.ReprendreEnLigne` → `FactureElectronique.Demarrer(e)`.
- Lot 13 : `EmailService.Modeles`, `Garder`, `TexteEnvoi`, `Aide`, `CorpsNotif(propre, avoir)` ; `FactureElectronique.ConfirmerTransmission(adresse, transmettre)` ; les trois panneaux : `RefreshPlateforme(bool chargement)` (écouteurs `_siret` et `_tauxTva`, appelé en dernier par `LoadIntoUI`), champs `_modeNotif`, `_modeCopie`, `_chargement` ; `Traiter` notifie toute ligne transmise après la bascule.
- Décisions du 10/11 : `FacturationSuivi.ApresCorrection` (date du versement reporté : `encaisseeDateISO`, sinon `FactureElectronique.Encaissement`) ; `FactureElectronique.RaisonInactive`, `EtatLu`, `RefusLancement(loc, r)`, lus par `FactureEnvoiAuto.EnvoyerUne` avant tout e-mail.
- Tests : `Assets/Editor/Tests/LotCTests.cs` (sections « Lot 10 » à « Lot 13 », `D5_06_le_reglement_reporte_garde_la_date_de_l_encaissement_d_origine`, deux tests `Decision_10_11_…`) ; `TvaTests.La_plateforme_suit_le_bail_et_le_siret` et `Revue17Tests.Numero_repris_grise_et_non_modifiable_dans_les_quatre_formulaires` ajustés ; 776 tests ; modèle 9 inchangé.

## Revue 17, lot C (facturation), vague 3 : lots 14 à 16, avenant de périodicité (10/11, modèle 10)

- Lot 14 : `Locataire.historiquePeriodicites` (liste de `PeriodiciteAnterieure`, classe dans `Locataire.cs` à côté de `PalierLoyer`) ; `DonneesCloud.Modele` = 10. `FacturationSuivi`, bloc « Plan des périodes de loyer d'une année » : struct `PeriodeLoyer` (`debut`, `fin`, `debutNaturel`, `finNaturelle`, `periodicite`, `numero`, `Coupee`, `Mois`, `Part`), `Plan(loc, année)`, `Periode`, `PeriodiciteDe`, `LibellePeriode(loc, année, rang)`, `PeriodeIndex(loc, année, mois)`, `MoisFactures(PeriodeLoyer)`. Toute fonction (loc, année, période) passe par le plan ; sans historique, c'est le calcul d'avant. `Loyers.EnregistrerAvenantPeriodicite(loc, effet)` ; `Loyers.MontantPeriode` au prorata des mois pour une période coupée ; `LoyerHistoryService.LoyerPourAnnee`, `FacturationAlertes`, `FactureLoyerPanel.PeriodeAOuvrir`, `DepartLocataire.PeriodeApresDepart`, `Cessions.TitulaireLigne` lisent le plan.
- Lot 15 : `Loyers`, bloc « Avenant de périodicité : la date d'effet » : `FactureJusquAu`, `DateAvenantRequise`, `PremiereDateAvenant`, `RefusAvenantPeriodicite` (règles pures). `RevisionPanel` : `MoisEffectifs` et `CalendrierChange` (statiques), `AvenantPeriodiciteDemande`, `RefreshAvenantPeriodicite` (bloc, proposition, aide), `JourSaisi`, `RepriseChoisie` ; `RefreshRepriseOptions` lit `FacturationSuivi.Plan` ; `ValiderInitialisation` refuse, puis appelle `Loyers.EnregistrerAvenantPeriodicite` avant `EcrireModalites`. `FactureLoyerPanel` : `PeriodeOptions(loc, année)`, `LibellePeriode(loc, année, période)`, `MoisFactures(loc, année, période)`, `PeriodeGardee` (statiques publiques) ; `PoserPeriodes`, `RefreshPeriodes`, `PeriodiciteChoisie`, `RefreshMensuel` ; `_anneeOptions`. `FactureVarResolver` : `FactureContext.periodicite`, remplie aussi par `FactureEnvoiAuto.Contexte`.
- Lot 16 : `Loyers`, section « Provisions pour charges et périodicité » : `ProvisionConvertie`, `ProvisionPeriode`, `ProvisionsAppelees`. `RevisionPanel` : `ConvertirProvisions`, `SuivreProvision`, `TexteProvision`, `_provAuto` (vidé à l'ouverture, avant de poser la périodicité). `FactureLoyerPanel` : `DefaultProvision(listeId)`, `RefreshProvisionDefault` (dans `RefreshEcheanceDefault`), `_autoProvisions` ; `ProvisionSaisie` et `LignesProvision` au centime par ligne. `FactureRegulPanel` : `ProvisionsAuto` lit `Loyers.ProvisionsAppelees` ; `PartProvision` (statique, publique) ; la forme statique de `MontantsParListe` garde sa signature et calcule le TTC par `Tva.Ttc`. `TransfertEntreprise.ReconcilierListes` : une ligne de plus (provisions imprimées).
- Finitions : `Loyers.PremiereDateAvenant(loc, jour, avenantLoyer, repriseISO)` (nouveau paramètre, transmis à `Loyers.FactureJusquAu`) ; `RevisionPanel.PeriodeReprise(loc, reprise)` (statique, publique : l'année et le rang, rang 0 si la date n'est dans aucun plan) et la constante `RepriseGardee` (option « Reprise enregistrée (…) » du menu) ; `FactureVarResolver.ProvisionFacturee(loc, periodicite)` (privée, sert `{loc.provision}`, ne convertit que si `Locataire.historiquePeriodicites` n'est pas vide) ; `Tva.FacturePartie` exige un PDF sur la ligne ; retirées de `FacturationSuivi`, sans appelant : EcheancePeriode et l'ancienne forme de LibellePeriode (périodicité, période, année) ; `Loyers.PeriodesFacturees` reste, lue par les tests seulement.
- Tests : `Assets/Editor/Tests/AvenantPeriodiciteTests.cs` (33 tests : scénarios chiffrés du plan, règles de date, provisions) ; `TransfertEntrepriseTests.Le_choix_des_listes_du_locataire_suit_le_transfert` complété ; `Revue17Tests.Numero_repris_grise_et_non_modifiable_dans_les_quatre_formulaires` suit `PeriodiciteChoisie` ; empreinte du modèle dans `Revue17Tests.D2_01_un_champ_ajoute_fait_monter_le_numero_de_modele`. 809 tests EditMode verts.

## Revue 17, lot C (facturation), « coutures » (10/11, modèle 11)

- Émission (`FactureEmission`) : `EnvoisEnCours`, `NoterEnvoi(ref enCours, debut)` et `FichePerdue(bat, dest)` (D4-07 : tout formulaire qui envoie passe par `NoterEnvoi`, ferme par `FermerDeForce()` dans sa réservation, et lit `FichePerdue` dans `EnvoyerPuisFinaliser`, après le cas de l'échec et avant `Finaliser`) ; `EnregistrerLiee` (les autres lignes d'une même facture, D4-06), `Decision.NumeroBase`, `RefusTotalNul`, `RefusSansRib`, `PhraseSomme`, `NommerBouton` (bouton « Corriger et envoyer » de la régularisation, de la refacturation et du dépôt, décision du 11/11). Ordre dans le loyer : `Preparer`, document, refus (total nul, RIB, correction, obligation, avoir), PDF, puis premiers pas, envoi différé, transmission, e-mail. `FactureRegulPanel.Emettre(d, key, emission)` et `FactureDepotPanel.Emettre(d, key, emission)` : la décision de `Preparer` est prise une seule fois, dans `SauvegarderEtEnvoyer`.
- Suivi (`FacturationSuivi`) : `EtatDe(f, loc)`, `loc` obligatoire (`null` seulement quand le locataire est inconnu ou quand la question est « émise ? » ou « payée ? ») ; `MarquerCorrige` écrit toujours le montant, 0 compris ; `RienDu` (une facture de CIPL à 0 € ne passe pas « Impayé ») ; `SetStatut` pose et vide `FactureEtat.payeLeISO`.
- Cloud : `EcritureCloud.EnvoiEnCours` = envoi groupé du lancement OU envoi d'un formulaire ; `EcritureCloud.Occupe` vrai pendant l'envoi d'un formulaire ; `VersementsPanel.Ouvrir` et `Rembourser` réservent le locataire et son bâtiment (`EcritureCloud.Reserver`, fenêtre ouverte après l'aller-retour du verrou) ; `FactureDepotPanel.OpenDepot` réserve le bâtiment aussi.
- Plateforme et TVA : `FactureElectronique.ReferencesAvoir` (factures parties et non refusées seulement), `NumerosCites`, `Encaissement` (lit `payeLeISO`), `LibelleEnvoi` (jour de départ, `FactureLoyerPanel.DepartSaisi`) ; `FactureDepotPanel.LibelleEnvoi` ; `Tva.TauxConnu`, `TauxAConfirmer`, `ConfirmerTaux` (Réglages, Gestion du loyer : `RevisionPanel.ConfirmerTauxPuisValider`, formulaires) ; `MentionTva.Lire(f, defaut)`, `ParDefaut(r)`, `Charger(menu, phrase, f, defaut)` ; `ReglageData.mentionTvaParDefaut`, menu dans `ReglagePanel.BuildTva`.
- Refacturation : `FactureRefacPanel.Justificatifs`, `PiecesJointes`, `PiecesDeLigne`, `AbsentsDits` (pièces jointes, lues aussi par `FactureEnvoiAuto.EnvoyerUne` et `FactureElectronique.Notifier`) ; `FactureEnvoiAuto.Bilan(ok, echecs, erreur, sautees, notes)` : avec une note, le bilan passe au centre.
- Autour : `ConfirmDialog.Show` (une erreur recouverte par une confirmation revient après elle, D8-02) ; `SocieteLocataire.Siren` (9 ou 14 chiffres exactement) ; `SlashAutocomplete.Position` (`UIFactory.PlacePopup`) ; `FactureRegulPanel.TitreDetail` ; `FacturePdfService.RegulData.factureOrigine` ; `Loyers.PeriodesFacturees` retirée (la régularisation lit `Loyers.ProvisionsAppelees`).
- Tests : `Assets/Editor/Tests/CouturesTests.cs` (36 tests), `Assets/Editor/Tests/CouturesAutourTests.cs` (10 tests) ; empreinte du modèle « 11:973caba90b7c » ; 855 tests EditMode verts.

## Revue 17, lot D (données et cloud) (11/11, modèle 11 inchangé)

- Écriture (`EcritureCloud`) : `Envoyer` → `Transmettre` (un appel `enregistrer_fiches`, repli `TransmettreUneParUne` sur 404) ; règles pures `Differences` (ordre réel, sans renumérotation), `CorpsEnregistrerTous`, `LireResultat`, `Avancer`, `OpRefusee`, `Reessayer`, `SansRefuses`, `RetenirRefusDeFond`, `TenuParAutre` ; `Oublier`, `NoterSuppression` / `RetirerSuppression`, `AJour(b)` (à lire après `Attendre`) ; `Lire` dédouble les lignes ; le gardien garde un verrou tant qu'un enregistrement est en vol ; `RegleVerrou.ResteTenu`.
- Direct (`DirectCloud`, `TempsReel`) : délai de connexion de 10 s, `AbonnementRefuse`, `ARelancer`.
- Documents (`DocumentsCloud`, `SyncDocuments`) : `RetraitVoulu`, `Terminer`, `IssueAProposer`, action « Indexer », `SuppressionsAControler`, `SousRetrait`, `PrefixeRetrait` ; `CloudClient.TableTout`, `CompterElements`, `FusionnerPages` ; `TransfertCloud.DuDossier` ; `DossiersDonnees` (destination vide retirée, `Bientot` après une copie) ; `FacturePdfService` et `PhotoService` demandent un passage.
- Démarrage (`DemarrageCloud`) : `FinDesEnvois`, `DossierEntreprise.DossierLibre` et `DecisionLocal`, `ImportLocal.Repere.local`, `EcransCloud.DossierLocalIntrouvable` ; `ReglageService.EffacerCopie`, `ReglagesCloud.Motif` ; `FermetureGuard.QuitterApresDocuments`, `DernierePasse` ; `RegleVersion.Empreinte`, `InstalleurValide`, `TexteFenetre`.
- Départ et bail : `DepartLocataire.Remplacant`, `RefusRemplacant`, `DepartPanel.TypesProposes`, `BailFileUI.EtatLigne` et `Reserve`.
- Fiches : `BatimentManager.CessionsAAppliquer`, `RefusAjout`, `BatimentPrefab.CreationAbandonnee`, `PrefabBatLoc.RefusNombre`, `LocatairePrefab.RefusSiretIban`, `PappersSync.ArreteLaTournee`, `MessageRefus`, `SiretChange`.
- Révision : `InseeIndiceService.TrimestreSuivant`, `RefusTrimestre`, `MessageEchec` ; `LoyerHistoryService.Manquants` ; `Loyers.PaliersDesalignesApres` ; `RevisionPanel.Nombre`, `LibellesPeriodicite`.
- Cartes, PLU, achats, objectifs : `MapController.Deplacer` et `Adresse`, `TileLoader.ImagePosee`, `GeoCodingService.Decision`, `CadastreService.MessageCommune`, `SaisieNumerique.OuNaN` et `RefusIllisible`, `AchatItemUI.DateLisible`, `ObjectifPanel.Ouvrir` (réserve la fiche).
- Tests : `Assets/Editor/Tests/LotDSqlTests.cs`, `LotDEcritureTests.cs`, `LotDDocumentsTests.cs`, `LotDDemarrageTests.cs`, `LotDDepartTests.cs`, `LotDFichesTests.cs`, `LotDRevisionTests.cs`, `LotDCartesTests.cs` (104 tests de plus) ; 959 tests EditMode verts.
