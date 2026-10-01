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
│  PennylaneClient (Pennylane)     │  AtomicFile · BackupService            │
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
| `Travaux/`, `Achat/` | Investissements, travaux, tableaux de rentabilité | `RentabiliteGlobaleController`, `TravauxController`, `InvestissementListPanel` |
| `Objectif/` | Objectifs (par bâtiment, locataire, et global) | `ObjectiveManager`, `GlobalObjectivesSection` |
| `Maps/` | Carte, géocodage, tuiles, vignettes | `MapController`, `GeoCodingService`, `MapThumbnailService` |
| `PLU/` | Urbanisme : zone PLU d'une adresse ou d'une parcelle | `PLUOverlayPanel`, `PLUService`, `CadastreService` |
| `Pappers/` | Résumé d'entreprise des locataires (annuaire de l'État) | `PappersSync`, `PapperService`, `PappersResume` |
| `Tools/` | Composants UI génériques et saisie | `UITheme`, `UIZoom`, `CollapsibleSection`, `ColonnesAdaptatives`, `SaisieNumerique`, `SaisieDate`, `UndoToast`, `ConfirmDialog` |
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

Les réglages d'entreprise sont à part (`ReglageData`, fichier `reglage.json`) : mode d'envoi, SMTP, RIB, entêtes, listes de charges, textes fixes, logo.

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
├─ pennylane_secrets.dat                  ← clé API Pennylane, mot de passe SMTP (clé=valeur)
├─ batiments/
│   └─ batiment_<id>.json                 ← un bâtiment et tous ses locataires
├─ Batiment/                              ← documents, rangés par NOM (lisible dans l'explorateur)
│   └─ <Nom du bâtiment>/
│       ├─ Photos/
│       ├─ Charge/                        ← PDF des factures de charges
│       └─ <Nom du locataire>/
│           ├─ Facture/                   ← PDF émis
│           └─ Bail/                      ← bail et avenants (copiés)
├─ Backups/<date_heure>/                  ← copie au démarrage, gardée 30 jours
├─ corbeille_batiments/, corbeille_photos/
└─ .migration_dossiers_v1                 ← marqueur de la migration GUID → noms
```

### 5.2 Les pièces

- **`SaveLocationService`** : racine active (`PlayerPrefs`), repli sur `persistentDataPath`. Une racine enregistrée mais introuvable (disque débranché) est **signalée**, pas remplacée en silence.
- **`DossiersDonnees`** : chemins de tous les documents, conversion d'un nom en nom de dossier valide (caractères interdits, noms réservés Windows, 60 caractères au plus), chemins **relatifs** stockés (`VersRelatif` / `VersAbsolu`). Les noms étant des dossiers, ils doivent être uniques : deux bâtiments homonymes sont refusés, et deux locataires homonymes seulement dans le même bâtiment.
- **`AtomicFile.WriteAllText`** : écriture dans un `.tmp` puis bascule par `File.Replace`, l'ancienne version passant en `.bak`. Renvoie `false` en cas d'échec : l'appelant doit le dire, jamais afficher « sauvegardé ».
- **`BatimentManager`** :
  - `LoadAll` charge chaque JSON isolément : un fichier tronqué est signalé sans bloquer les autres ;
  - `SaveBatiment` passe par `AtomicFile` ;
  - `SauvegardeDeFermeture` réécrit l'état en mémoire en quittant, et refuse si la racine a changé entre-temps.
- **`BackupService`** : copie datée au démarrage (JSON + `reglage.json`, **jamais les secrets**), purge après 30 jours.
- **`ReglageService`** : `Current` est chargé à la demande. Si `reglage.json` est illisible, l'écriture est **refusée** pour ne pas écraser l'original. Les secrets vivent dans deux fichiers `clé=valeur`, hors JSON, hors sauvegardes et hors dépôt Git :
  - `<racine>/pennylane_secrets.dat` pour ceux de l'entreprise (clé Pennylane, mot de passe SMTP) ;
  - `persistentDataPath/cipl_app_secrets.dat` pour ceux de l'application (jeton Mapbox), communs à toutes les entreprises.
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
   ▼
 Envoi selon ReglageData.modeEnvoi :
   ├─ EmailService (SMTP, confirmation obligatoire, rien sans destinataire explicite)
   └─ PennylaneClient.Deposer (facture INCOMPLÈTE + Factur-X ; ni finalisation ni send_to_pa)
   ▼
 FactureEmission.Enregistrer             ← APRÈS un PDF réussi : FacturationSuivi.MarquerEnvoye / MarquerCorrige
```

| Élément | Rôle |
|---|---|
| **`FacturationSuivi`** | Machine à états des factures. Lignes planifiées d'une année (`Lignes`), clés stables (`loyer-2026-P3`, `regul-2025[-<liste>]`, `depot-2026`, `depot-initial`, `refac-<charge>`). États : À venir → À faire → En attente d'envoi → Envoyé → Impayé / Payé. Les états **transitoires** (Clôturé, Franchise, Hors bail) ne sont jamais stockés. Seules les lignes « touchées » ont un `FactureEtat`. |
| **`FactureEmission`** | La règle « une facture émise ne consomme jamais un second numéro », en un seul endroit pour les quatre panneaux. |
| **`FacturationAlertes`** | Échéances (loyer à J-15, régul, dépôt) → pastilles de la fiche et « À traiter ». |
| **`ChargeBatiment`, `ListesCharges`, `ChargePanel`** | Charges du bâtiment, état **par locataire**, listes de charges (générale + spécifiques), quote-parts. |
| **`HeritageFacture`** | Un nouveau locataire copie les réglages de facture du précédent (chaîne). |
| **`FactureEnvoiAuto`** | Au lancement, liste les factures prêtes dont la date d'envoi est atteinte. Rien ne part sans relecture. |
| **`FacturationHomeSection`** | Colonne « Créances » de l'accueil. |
| **`LocataireSuiviInline`, `SuiviRowUI`** | Tableau de suivi en bas de la fiche (prefab `SuiviFactureRow`). |
| **`FactureQuittanceService`, `FactureRappelService`** | Quittance (bail non commercial payé) ; rappel d'impayé en brouillon `mailto:`. |
| **`ReglagePanel`, `ReglageService`, `ReglageData`** | Réglages d'entreprise et secrets. |
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
- `MapController`, `GeoCodingService`, `TileLoader`, `MapThumbnailService` : carte Mapbox et géocodage, avec la carte conservée tant que l'adresse ne change pas.
- `PLUOverlayPanel`, `PLUService`, `CadastreService` : zone PLU à partir d'une adresse ou d'une parcelle.

### 8.6 Résumé d'entreprise des locataires — `Pappers/`

`PappersSync` interroge l'annuaire des entreprises pour tous les locataires (au lancement, et à l'enregistrement d'une fiche). `PappersResume` ne remplace que le bloc délimité dans le commentaire du locataire.

---

## 9. Services externes

| Service | Usage | Code | Authentification |
|---|---|---|---|
| INSEE — `bdm.insee.fr` (SDMX) | Séries d'indices ILC / IRL / ILAT | `InseeIndiceService` | aucune |
| Pennylane — `app.pennylane.com/api/external/v2` | Dépôt des factures (incomplètes, Factur-X), clients par SIREN | `PennylaneClient` | clé API (secrets de l'entreprise) |
| SMTP (Gmail, M365, OVH…) | Envoi des factures par email | `EmailService` (`System.Net.Mail`, STARTTLS) | mot de passe (secrets de l'entreprise) |
| Microsoft Edge (local) | HTML → PDF / PNG en mode headless | `FacturePdfService` | — (Edge doit être installé) |
| Annuaire des entreprises — `recherche-entreprises.api.gouv.fr` | Résumé société par SIRET | `PapperService`, `PappersSync` | aucune |
| Mapbox — `api.mapbox.com` | Tuiles et vignettes de carte | `TileLoader`, `MapThumbnailService` | jeton (secrets de l'application) |
| Base Adresse Nationale — `api-adresse.data.gouv.fr`, puis Nominatim en repli | Adresse → coordonnées | `GeoCodingService`, `CadastreService` | aucune |
| IGN — `apicarto.ign.fr` (`/api/cadastre`, `/api/gpu`) | Parcelle cadastrale, zone PLU | `CadastreService`, `PLUService` | aucune |
| Géoportail de l'urbanisme | Lien ouvert dans le navigateur | `PLUOverlayPanel` | — |

Tous les appels réseau passent par `UnityWebRequest` dans des coroutines, sauf l'envoi d'email : il tourne sur un thread de fond, et l'UI attend son résultat en coroutine.

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
| **Pennylane** | Dépôt seulement. Finalisation et `send_to_pa` **volontairement non écrites** (en attente de Pennylane). Dépôt depuis Régul / Refac / Dépôt à faire. |
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
