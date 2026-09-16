# Revue de code — application CIPL

**Date** : 16 septembre 2026 · **Périmètre** : 107 fichiers C#, ~20 920 lignes, UI comprise · **Branche** : `Facturation`

Chaque finding porte un statut :

- **CONFIRMÉ** — chemin de code tracé, scénario de défaillance vérifié.
- **PLAUSIBLE** — risque réel, mais dépend d'un cas d'usage non tranché.
- **ÉCARTÉ** — vérifié comme faux positif (listé quand on pourrait croire le contraire).

---

## Passation — à lire en premier (16/09/2026)

Ce document est le **suivi de la revue de code**. Tout ce qui suit a été écrit et compilé ; ce qui a été *réellement exécuté* est listé plus bas, et la distinction compte.

### Décisions prises, et leur motif

| Décision | Motif |
|---|---|
| **H1 (numérotation des factures) laissé de côté** | Deux locataires facturés le même mois peuvent obtenir le même numéro. Le choix entre séquence unique globale et identifiant stable par locataire engage la conformité (art. 242 nonies A du CGI) → **en attente de l'expert-comptable**. Ne pas trancher seul. |
| **Dossiers nommés par nom seul**, pas nom + identifiant | Lisibilité maximale dans l'explorateur, au prix d'une **unicité imposée** : deux bâtiments homonymes sont refusés, deux locataires homonymes sont autorisés **s'ils sont dans des bâtiments différents**. |
| **`double` limité aux gros montants** | Mesuré : `float` est exact au centime sous 1 M€. Convertir loyers et charges aurait touché toute la facturation pour un gain nul. |
| **Émission bloquée sur montant négatif** | Un trop-perçu est un avoir, pas une facture. Volontairement strict — **peut être assoupli en simple confirmation** si un cas réel se trouve bloqué. |
| **Migration automatique au démarrage** | Valide tout son plan avant de bouger quoi que ce soit, sauvegarde préalable, marqueur écrit seulement en cas de réussite complète. |

### Vérifié en exécution réelle, versus seulement compilé

C'est le point le plus important de cette passation. Un `SaveAll()` livré « compilé » s'est révélé cassé au premier lancement (itération d'une `List<T>` pendant sa modification) : **compiler ne prouve rien**.

**Testé pour de vrai** : écriture atomique (`.tmp` → `.bak`, récupération d'un JSON tronqué) · nommage des dossiers (15 cas, pièges Windows) · renommage et refus de collision · homonymes entre bâtiments · aller-retour `double` sur disque · `SaveAll` · comportement du `??` · culture décimale · **(16/09, seconde session)** logique de correction / double émission (11 cas) · refus de migration vers un dossier peuplé (12 cas) · saisie de montants invalides.

**Compilé mais jamais exécuté** : gardes d'avoir sur régularisation et dépôt (le refus est écrit dans les deux `SauvegarderEtEnvoyer`, mais il passe par l'UI et n'a jamais été déclenché) · avertissement de fermeture (`FermetureGuard`, dépend de `wantsToQuit`) · parcours UI complet des 3 panneaux de facture — la logique sous-jacente, elle, est désormais testée.

### Ce que le passage en exécution a trouvé (16/09, seconde session)

« Compiler ne prouve rien » s'est vérifié une seconde fois : exécuter H2 a montré que le correctif **fermait le mauvais périmètre** — il protégeait les états Envoyé/Impayé, en laissant dehors le cas le plus courant du panneau Loyer. Détail en **H2-bis** dans le tableau des corrections.

### Prochaines étapes

1. Reste à passer en Play : gardes d'avoir (régul/dépôt) · `FermetureGuard` · parcours UI des panneaux.
2. H1, après avis de l'expert-comptable.
3. ~~Reliquat mineur~~ — **traité le 16/09**, voir la section « Reliquat mineur » plus bas : garde sur `ReloadFromDisk` · arguments Edge · annulation de suppression de photo.

### Garde-fous permanents

Travailler dans `CIPL_Git\CIPL` (jamais `CIPL_Test`, copie obsolète) · **ne jamais commiter** (l'utilisatrice s'en charge) · secrets hors dépôt · toute modification de facturation répercutée dans `FACTURATION_CIPL_PENNYLANE.md` · aucune émission Pennylane réelle sans accord explicite (aujourd'hui aucun appel à l'API n'existe dans le code).

---

## Statut des corrections (16/09/2026)

| Finding | Statut |
|---|---|
| C1 — perte des RIB / entêtes | **Corrigé** — écriture atomique, repli `.bak`, écriture verrouillée si lecture impossible, `reglage.json` dans les backups |
| C2 — app qui ne démarre plus | **Corrigé** — `try/catch` par fichier dans `LoadAll`, fichiers illisibles listés |
| C3 — « sauvegardé » alors que non | **Corrigé** — `AtomicFile` + `SaveBatiment` renvoie `bool` |
| H2 — double émission Régul/Dépôt/Refac | **Corrigé** — `EstDejaEmise` + `MarquerCorrige` câblés sur les trois panneaux |
| H2-bis — la garde ne couvrait pas les 4 états « émise » | **Corrigé et testé en exécution (16/09)** — voir ci-dessous |
| H4 — bâtiment jamais écrit sur disque | **Corrigé** — appel de sauvegarde réactivé dans `AddBatiment` |
| Bonus (non listé initialement) | **Corrigé** — `WriteSecret` pouvait effacer la clé API en enregistrant le mot de passe SMTP |
| H3 — impayé jamais détecté | **Corrigé** — `TryEcheance` en culture invariante partout, échéance garantie à l'émission, anomalie journalisée |
| M1 — token Mapbox commité | **Corrigé** — purgé du prefab, saisi dans Réglages, stocké dans les secrets ; URL plus journalisée ; `.gitignore` complété |
| M2 — `catch` silencieux | **Corrigé** — les 3 qui masquaient une vraie défaillance (secrets, photo, PDF périmé) ; les 3 autres sont du best-effort légitime |
| M3 — saisie invalide → 0 muet | **Corrigé** — `SaisieNumerique` mutualisé (3 copies supprimées), tolère « 1 200 » / « 3,5 % », avertit sinon |
| M3-bis — « . » et « , » ne faisaient pas la même chose | **Corrigé et testé en exécution (16/09)** — voir ci-dessous |
| M4 — culture des indices | **Corrigé** — écriture forcée en culture invariante (`FormatIndice`) |
| M5 — migration destructive | **Corrigé** — refus si destination non vide, copie de **tous** les fichiers (secrets, photos), retour `bool` honoré |
| M6 — montants négatifs / TVA | **Corrigé** — émission refusée sur avoir, TTC = HT + TVA arrondis au centime |
| L2 — code mort | **Corrigé** — `LoyerRevisionController.cs` supprimé après vérification (0 référence, GUID absent des scènes) |
| L3 — `Debug.Log` | **Traité** — la seule fuite réelle (URL Mapbox avec token) est corrigée ; les 30 restants sont diagnostiques et sans secret |
| L1 — pattern `??` | **Écarté** — prouvé non-bug par test en éditeur |
| Bonus (non listés initialement) | **Corrigés** — `WriteSecret` effaçait la clé API ; l'URL Mapbox fuitait le token dans la console |
| H1 — doublons de numéro de facture | **En attente** — arbitrage avec l'expert-comptable |

Compilation vérifiée après chaque lot (`CSF=False`) et écriture atomique testée en conditions réelles (création, remplacement, `.bak`, absence de `.tmp` résiduel, récupération d'un JSON tronqué). Rien n'est commité.

### H2-bis — la garde anti-double-émission ne couvrait pas le cas le plus courant (16/09/2026)

**CONFIRMÉ en exécution** · `Facturation/FacturationSuivi.cs:227` (`EstDejaEmise`), `:238` (`MarquerCorrige`)

Le correctif H2 avait câblé `EstDejaEmise` sur les quatre panneaux, mais la fonction elle-même testait `e == Etat.Envoye || e == Etat.Impaye` — deux états sur les quatre que `DejaTraite`, dans le même fichier, considère comme « traitée ». Deux trous, reproduits par un test exécuté dans l'éditeur (et non déduits de la lecture) :

| Cas | Ce qui se passait |
|---|---|
| **Loyer préparé plus de 15 j avant l'échéance** (statut `AttenteEnvoi`) — le cas **normal** du panneau Loyer | `EstDejaEmise` = `false` malgré un PDF déjà généré → second clic : nouvelle séquence consommée **et** PDF d'origine écrasé |
| **Facture marquée « Payé » puis re-générée** | Numéro neuf → **deux numéros pour une seule période** |

Correctif : les quatre états `AttenteEnvoi | Envoyé | Impayé | Payé` valent « émise » (+ PDF non vide). Un paiement ne « dé-émet » pas une facture ; un numéro consommé le reste.

Conséquence assumée sur `MarquerCorrige` : il ne force plus `AttenteEnvoi → Envoye`. Corriger un loyer avant sa date d'envoi affichait sinon un envoi qui n'a pas eu lieu — la bascule à J‑15 reste assurée par `EtatDe`, qui dérive l'état. Une facture payée reste payée ; seule une ligne **sans** statut devient « Envoyé ».

**Vérifié** (11 assertions) : détection en `AttenteEnvoi` et en `Payé` · statut d'attente et paiement conservés après correction · numéro conservé · séquence non consommée · non-régressions (ligne planifiée, ligne sans PDF, cas nominal Envoyé).

**Vérifié également en exécution** — refus de migration vers un dossier peuplé (`SaveLocationService.MigrateData`, 12 assertions) : dossier vide → migration complète, **y compris** secrets `.dat` et photos en sous-dossier ; dossier contenant déjà une entreprise → refus, message explicite, `reglage.json` de la destination **intact** et **aucune copie partielle** avant le refus (la pré-analyse précède toute écriture) ; même dossier écrit avec une casse différente → sans effet, pas d'auto-copie.

### M3-bis — point et virgule ne faisaient pas la même chose (16/09/2026)

**CONFIRMÉ en exécution** · `Tools/SaisieNumerique.cs` · 11 sites de saisie routés

M3 avait mutualisé **trois** copies de `Parse`. Une recherche exhaustive des parseurs de saisie en a révélé **onze**, dont huit hors du périmètre initial, avec trois comportements différents pour la même frappe :

| Site | Avant |
|---|---|
| `PrefabBatLoc.SaveCorrectlyFloat` (helper de sauvegarde des fiches), `CalculPrixRentabilite.CheckValueOnString` | **La virgule n'était pas gérée du tout** : « 1200,50 » → avertissement + **0** |
| `ParseF`/`ParseFloat` des 5 panneaux de facturation, `QuickCalcInline`, `RevisionPanel` (×3) | Virgule gérée, **espaces de milliers non** : « 1 200 » → 0 |
| `SaisieNumerique` | Virgule et espaces gérés, séparateurs mixtes non |

Les onze passent désormais par `SaisieNumerique`, où **« . » et « , » sont strictement équivalents** — le séparateur décimal se tape au pavé numérique (point) ou à la frappe française (virgule), et les deux doivent donner le même nombre. Avec plusieurs séparateurs, **seul le dernier est décimal** : `1.200,50`, `1,200.50`, `1 200,50`, `1 200.50` valent tous 1200,50.

**Vérifié** (20 assertions) : 9 paires point/virgule strictement égales · 6 formes mixtes · non-régressions (champ vide = 0, « abc » rejeté, séparateur seul rejeté).

Deux points conservés délibérément :
- **un séparateur unique reste décimal** : `1,200` = `1.200` = **1,2**, pas 1200. C'est le prix de la symétrie demandée — et le seul choix cohérent, puisque `0,500` doit valoir 0,5. Pour mille deux cents : `1200` ou `1 200`.
- **`TryParseLoyer` (loyer de départ) refuse toujours un champ vide** : `SaisieNumerique` traite le vide comme un 0 légitime, ce qui n'a pas de sens pour un champ obligatoire.

Limite inchangée, déjà connue (M6) : au-delà de ~7 chiffres significatifs, `float` ne tient plus le centime — `1.234.567,89` est relu à 1234567,9. Le type, pas la saisie.

### Revue globale de fin — 5 bugs trouvés et corrigés (16/09/2026)

Revue de tout `Assets/Script` par angles de risque (parsing, indexation, division, accumulation de listeners, caches statiques, chemins de fichiers, suppression/renommage), puis lecture de chaque site suspect. **Tous corrigés et vérifiés en exécution.**

Le fil rouge : quatre des cinq portent sur **la même faille**. L'arborescence a été standardisée (dossiers nommés par nom, chemins relatifs) et `DossiersDonnees` écrit comme point de passage unique — mais trois sites construisaient encore leurs chemins à la main, et un quatrième ne nettoyait pas ce que la convention laisse derrière.

| # | Défaut | Correctif |
|---|---|---|
| G1 | **`pdfPath` stocké en absolu** (`FacturationSuivi.cs:214`). Changer d'emplacement, migrer l'entreprise ou renommer un bâtiment/locataire cassait le lien de **toutes** les factures émises : le bouton « PDF » disparaissait du suivi alors que les fichiers étaient bien là. Même défaut sur `ChargeBatiment.pdfPath`. | Stocké via `VersRelatif` dans `MarquerEnvoye`/`MarquerCorrige` ; nouveau `FacturationSuivi.CheminPdf(ligne)` pour la lecture, câblé dans les deux vues du suivi. Les anciens enregistrements absolus restent lisibles (`VersAbsolu` les renvoie tels quels). |
| G2 | **Cache photo statique jamais vidé** (`PhotoService.cs:195`). Indexé par chemin relatif : deux entreprises ayant un bâtiment de même nom avec un `facade.jpg` partagent la clé — après bascule, la fiche affichait **la photo de l'autre entreprise**, sans aucun signe d'erreur. | `PhotoService.ViderCache()`, appelé dans `ReloadFromDisk` et à la suppression d'un bâtiment. |
| G3 | **Quittances écrites dans `Batiment/<GUID>/<GUID>/Facture`** (`FactureQuittanceService.cs:35`) : introuvables à côté des factures du locataire. | `DossiersDonnees.DossierFactures(nom, nom)`. |
| G4 | **PDF de charges dans `Batiment/<GUID>/Charge`** (`ChargePanel.cs:342`). `DossiersDonnees.DossierCharges` existait pour ça et n'était appelée **nulle part** — un oubli, pas un choix. | `DossierCharges(nom)` + chemin stocké en relatif. |
| G5 | **Dossier fantôme après suppression** (`BatimentManager.cs:140`) : le dossier restait en place, et renommer plus tard un bâtiment vers ce nom échouait sur « un dossier X existe déjà », alors qu'aucun bâtiment de ce nom n'existait — cause invisible depuis l'app. | Le dossier est **déplacé** vers `<racine>/corbeille_batiments/<guid>_<nom>` et revient si l'utilisatrice annule. **Jamais purgé automatiquement** : il contient des factures, donc des pièces comptables. |

**Vérifié en exécution** : 10 assertions sur les chemins (relatif à l'écriture, absolu à la lecture, ancien format toujours résolu, correction également relativisée, dossiers nommés) · 3 sur le cache (cache actif, vidage effectif, rechargement) · 10 sur la corbeille bâtiment (déplacement, collision de renommage levée, photos et factures conservées puis restaurées, corbeille vidée, bâtiment sans dossier sans exception).

Un défaut de mon propre correctif a été trouvé par l'exécution, pas par la relecture : `ViderCache` appelait `Object.Destroy`, refusé hors mode Play (« Destroy may not be called from edit mode »). Corrigé en `DestroyImmediate` hors Play. *Même leçon que `SaveAll` : compiler ne prouve rien.*

**Piège évité au passage** : `pdfPath` devenant relatif, la comparaison de `ChargePanel:328` (`_fPdf != _edit.pdfPath`) serait devenue toujours vraie et aurait **recopié le PDF de charge à chaque enregistrement**. Les deux termes sont désormais comparés en absolu.

**Reste non migré** : les enregistrements existants gardent leurs chemins absolus (ils restent lisibles, mais ne suivront pas un futur déplacement tant que la facture n'est pas ré-émise) et les PDF déjà copiés dans les anciens dossiers `Batiment/<GUID>/` n'ont pas été déplacés. Une migration au démarrage serait possible ; elle n'a pas été faite ici, c'est une décision à prendre.

**Vérifié et sain** (fausses pistes écartées une par une) : gardes de `Substring`/`Split` (`TryDecompose`, `Initiales`, SIRET, autocomplétion) · accumulation de listeners (cibles recréées à chaque construction, `_manualEditable` vidé, `ObjectiveManager` dans `Start`) · divisions (`NbPeriodes` ne renvoie jamais 0) · les appelants de `Renommer*` honorent le `false` et reviennent en arrière · `SaveAll`/`SauvegardeDeFermeture` itèrent sur une copie.

### Seconde passe — findings non numérotés (16/09/2026)

Le tableau ci-dessus ne couvrait que les findings **numérotés**. L'exploration en avait remonté d'autres, jamais promus en findings, donc passés à travers. Traités depuis :

| Sujet | Statut |
|---|---|
| Division par l'indice de départ sans garde → `NaN` écrit dans `loyerAnnuel` | **Corrigé** — indice ≤ 0 rejeté avant calcul |
| Repli silencieux jusqu'à 5 ans sur l'indice de départ | **Corrigé** — avertissement affiché quand la base diffère du trimestre demandé |
| `new DateTime(d.Year+1,…)` levant un 29 février **après** l'écriture du loyer | **Corrigé** — date calculée avant écriture, jour borné au mois |
| `Split('-')[1]` sur un trimestre au format ancien → modale impossible à ouvrir | **Corrigé** — `InseeIndiceService.TryDecompose` aux deux sites |
| `TrimestreInput` empilant ses listeners à chaque ouverture | **Corrigé** — `RemoveAllListeners` avant câblage |
| Restauration écrivant dans l'entreprise B un bâtiment supprimé dans A | **Corrigé** — racine capturée à la suppression et vérifiée à l'annulation |
| Créer une entreprise sur un dossier déjà occupé → adoption des données existantes | **Corrigé** — détection + refus, renvoi vers « Ouvrir » |
| `GetSaveRoot` retombant muettement sur le dossier par défaut | **Corrigé** — alerte (une seule fois par racine manquante) |
| Quittance émise sur une facture jamais générée | **Corrigé** — bouton conditionné à un numéro + un PDF réels |
| Erreur réseau INSEE avalée → tableau plat sans indicateur | **Corrigé** — journalisée explicitement |
| `ParseDate` en culture courante + repli muet sur aujourd'hui | **Corrigé** — culture invariante + avertissement |
| Dépôt très en retard : `AddYears(1)` retombait dans le passé | **Corrigé** — avance jusqu'à repasser dans le futur |

**Écartés après vérification** (l'exploration se trompait) :
- *« Revalider le dépôt repousse la révision de 2 ans »* — non reproductible : la garde `Today < dr` bloque une seconde révision, et calculer depuis l'échéance plutôt que depuis aujourd'hui est correct (sinon l'anniversaire dérive).
- *« `MoisDeRevision` retombe sur aujourd'hui »* — les 5 sites de calcul d'échéance sont tous protégés par le test « bail initialisé », et passer à `MaxValue` afficherait 31/12/9999 dans le sélecteur de date.
- *« Virgule française mal gérée dans les formulaires d'achat »* — déjà traitée par `Replace(',', '.')` ; le vrai défaut était l'espace des milliers.

### Fermeture de l'application (16/09/2026)

Deux mécanismes complémentaires, parce qu'aucun des deux ne suffit seul :

1. **Sauvegarde de fermeture** — `BatimentManager.OnApplicationQuit` réécrit l'état en mémoire via l'écriture atomique. Elle **annule** l'opération si l'emplacement de sauvegarde a changé depuis le chargement (disque débranché, bascule d'entreprise) plutôt que de dupliquer les données ailleurs, et compte les échecs d'écriture. Ce qu'elle protège réellement : une modification validée dont l'écriture disque avait échoué (fichier verrouillé par un antivirus, lecteur réseau absent).

2. **Avertissement « modifications non enregistrées »** — `FermetureGuard` liste les fiches encore en édition et fait annuler la fermeture par `Application.wantsToQuit` (fenêtre fermée en build) ; `QuitButton` passe par le même contrôle, ce qui couvre l'éditeur, où `wantsToQuit` ne se déclenche pas.

Le point important : la sauvegarde de fermeture **ne peut pas** capturer une fiche en cours de saisie. Reverser l'UI dans les données passe par `BatimentPrefab.SaveBatiment()`, qui bascule aussi des boutons, ré-initialise la fiche et appelle `ShowFiche()` — l'exécuter pendant la destruction de l'application manipulerait l'interface en pleine fermeture, et l'appeler sur une fiche jamais ouverte écraserait des données réelles avec des champs vides. D'où l'avertissement plutôt qu'une sauvegarde silencieuse.

L'état « en édition » n'est pas stocké dans un nouveau booléen : `PrefabBatLoc.EnEdition` lit la visibilité du bouton Enregistrer, que `Modify()` affiche et `SaveBatiment()` masque. Impossible de le désynchroniser.

**Restent ouverts**, volontairement non traités parce que le correctif dépasse la revue : photos en chemins absolus (nécessite une migration des JSON existants) · montants en `float` plutôt que `decimal` (l'arrondi est traité, pas le type) · alerte de régularisation non acquittable sur un locataire neuf.

*(Les trois autres — `ReloadFromDisk`, arguments Edge, suppression de photo — ont été traités le 16/09, voir « Reliquat mineur » ci-dessous.)*

### Reliquat mineur — traité et vérifié en exécution (16/09/2026)

**1. `ReloadFromDisk` détruisait les saisies en cours sans prévenir** · `Batiment/BatimentManager.cs:92`

La méthode détruit tous les prefabs de bâtiment puis relit le disque. Sept points d'entrée y mènent : changement d'emplacement (`SaveLocationMenu.Appliquer`, `.RemettreDefaut`, `ReglagePanel` → `SaveIO.ChangeLocation`, `SaveIO.ResetToDefault`) et bascule d'entreprise (`EntreprisePanel` → `Activer`, `Ouvrir`, `Creer`). Une fiche ouverte en édition partait avec son prefab.

Le mécanisme de détection existait déjà — `FermetureGuard.NomsEnEdition()`, écrit pour la fermeture — il n'était simplement appelé nulle part ailleurs. Ajout de `FermetureGuard.ConfirmerPerteSaisies(action, suite)`, câblé sur les sept entrées.

Deux points de conception, tous deux vérifiés :
- **la confirmation précède l'opération**, jamais le rechargement : quand `ReloadFromDisk` est atteint dans `SaveLocationMenu`, les fichiers ont **déjà** été déplacés, et renoncer là laisserait le disque et l'affichage désaccordés. `Appliquer` a donc été scindé en `Appliquer` (garde) + `AppliquerConfirme` (travail) ;
- **la garde est posée dans l'UI, pas dans les services** : envelopper `EntrepriseService.Creer` aurait fait répondre son `bool` avant l'utilisatrice.
- Choix retenu : **prévenir et laisser choisir** (`ConfirmDialog`, bouton « Continuer sans enregistrer »), pas bloquer — bloquer piégerait quelqu'un qui veut justement abandonner ses modifications.

**Vérifié** (5 assertions) : action exécutée directement quand rien n'est en édition (la garde ne gêne pas le cas courant) · repli sans dialogue disponible · action nulle sans exception · texte de fermeture inchangé pour `QuitButton`.

**2. Arguments Edge assemblés à la main** · `Facturation/FacturePdfService.cs`

La ligne de commande était construite par interpolation, avec des guillemets posés à la main autour de chemins issus de noms de bâtiment, de locataire et du dossier choisi par l'utilisatrice. Ce n'était pas exploitable sous Windows (`UseShellExecute = false`, donc pas de shell ; et NTFS interdit le guillemet dans un chemin), mais la sûreté reposait alors sur une garantie du **système de fichiers**, pas du code.

`ProcessStartInfo.ArgumentList` — vérifié disponible dans le Mono d'Unity — reçoit désormais un argument par entrée ; c'est .NET qui assemble et cite.

**Vérifié** : un PDF de 69 Ko réellement produit par Edge dans `…/cipl edge & test 100% d'essai …/facture d'essai #1 (piégée).pdf` — espaces, `&`, `%`, apostrophe, `#`, parenthèses.

**3. Suppression de photo sans annulation** · `Batiment/PhotoService.cs`, `PhotoGalleryController.cs:92`

`File.Delete` immédiat, sans confirmation ni corbeille — alors que toute autre suppression de l'app (achat, travaux, charge, bâtiment, locataire) passe par `ConfirmDialog` **puis** `UndoToast` avec restauration. C'était la seule action définitive au premier clic.

Le fichier est maintenant **déplacé** vers `<racine>/corbeille_photos/<guid>_<nom>` ; `Supprimer` renvoie un jeton (`PhotoSupprimee` : chemin, index, couverture, emplacement en corbeille) que `Restaurer` consomme pour tout remettre en place, **à la position d'origine** dans la liste. `PurgerCorbeille()` (7 jours) est appelée au démarrage à côté du backup — sans elle, les photos supprimées s'accumuleraient dans la sauvegarde et seraient recopiées à chaque migration d'entreprise.

**Vérifié** (15 assertions) sur de vrais fichiers : retrait de la liste, du cache et de la couverture · contenu intact en corbeille puis après aller-retour · position et couverture rétablies · rien laissé en corbeille après restauration (déplacement, pas copie) · purge qui supprime un fichier de 30 jours et conserve celui du jour · photo dont le fichier manque déjà, sans exception.

Un cas traité au passage : si une photo ajoutée entre-temps a repris le nom libéré, la restauration ne l'écrase pas — elle revient sous un nom libre, et c'est ce chemin-là qui est réinscrit.

> **Rectificatif** : le comptage initial de « 20 `catch` silencieux » reposait sur une heuristique trop large. Le dépôt en contient **6** réellement vides, dont 3 seulement masquaient une défaillance ; les 3 autres (`proc.Kill`, suppression de fichiers temporaires) sont du best-effort légitime et ont été laissés tels quels.

## Synthèse

| Gravité | Nombre | Nature |
|---|---|---|
| CRITICAL | 3 | Perte de données irréversible, app qui ne démarre plus |
| HIGH | 4 | Numérotation de facture non conforme, impayés non détectés |
| MEDIUM | 6 | Secret commité, échecs silencieux, risques latents |
| LOW | 3 | Style, code mort, logs résiduels |

**Point rassurant d'emblée** : aucun appel à l'API Pennylane n'existe dans le code. La recherche `UnityWebRequest|POST|customer_invoices|draft` sur les 27 fichiers de `Facturation` ne renvoie rien. **Aucun chemin ne peut émettre une vraie facture aujourd'hui.** La règle « jamais d'émission réelle sans consentement » n'est donc pas menaçable en l'état.

Le risque n'est pas là où on l'attendait : il est dans la **persistance** (on peut perdre définitivement ses réglages) et dans la **numérotation des factures** (doublons, non conforme).

---

## CRITICAL

### C1 — Perte définitive des RIB, entêtes et séquences de facturation

**CONFIRMÉ** · `Facturation/ReglageService.cs:38-41`, `:44-49` · `Save/BackupService.cs:17`

Trois défauts se combinent en une chaîne de destruction :

1. `Save()` (ligne 47) fait un `File.WriteAllText` direct, **non atomique** et **sans `try`**. Une coupure pendant l'écriture laisse un `reglage.json` tronqué.
2. `Load()` (lignes 38-41) avale toute exception avec un `catch` nu et repart d'un `new ReglageData()` **vide**, sans le moindre avertissement.
3. Le premier `Save()` suivant écrase alors le fichier encore récupérable par des réglages vides.

**Aggravant décisif** : `BackupService.RunStartupBackup` ne sauvegarde que `<saveRoot>/batiments` (ligne 17). `reglage.json` n'a **aucune copie de secours**. RIB, entêtes, nom d'entreprise et séquences de facturation sont perdus sans recours.

**Scénario** : coupure de courant pendant l'enregistrement d'un RIB → au redémarrage, l'écran Réglages est vide → le moindre clic sur « Enregistrer » scelle la perte.

**Correctif** : écriture atomique (`.tmp` + `File.Replace` avec fichier `.bak`), distinguer « fichier absent » (normal) de « fichier illisible » (anomalie à signaler sans écraser), et inclure `reglage.json` dans le backup de démarrage.

### C2 — Un seul JSON corrompu empêche l'application de démarrer

**CONFIRMÉ** · `Batiment/BatimentManager.cs:151-164`, `:32-39`

`LoadAll` lit et désérialise chaque fichier **sans `try` par fichier** (lignes 153-154). Un JSON tronqué fait remonter l'exception hors de `LoadAll`, donc hors de `Start()` (ligne 35) — et `menuManager.OpenGeneralMenu()` (ligne 38) **n'est jamais atteint**.

Ce n'est donc pas seulement « les bâtiments suivants ne se chargent pas » : l'app reste bloquée sur un écran vide, sans message. Et la cause la plus probable d'un JSON tronqué est précisément l'écriture non atomique de C3.

**Correctif** : envelopper chaque fichier dans son propre `try/catch`, continuer la boucle, et présenter à la fin la liste des fichiers illisibles plutôt que d'échouer en silence.

### C3 — Écritures non atomiques : l'UI annonce « sauvegardé » alors que rien ne l'est

**CONFIRMÉ** · `Batiment/BatimentManager.cs:112-125`, `Facturation/ReglageService.cs:44-49`

`SaveBatiment` met à jour la liste mémoire (lignes 117-119) **avant** d'écrire (ligne 123), et n'a aucun `try/catch`. Disque plein, fichier verrouillé par OneDrive ou l'antivirus, lecteur réseau déconnecté → l'exception part d'un handler de clic Unity, l'écriture n'a pas eu lieu, mais l'état mémoire et l'affichage indiquent un succès.

**Correctif** : écriture atomique, `try/catch` explicite, et retour visuel d'échec.

---

## HIGH

### H1 — Deux locataires peuvent recevoir le même numéro de facture

**CONFIRMÉ** · `Locataire/Locataire.cs:66`, `Facturation/FactureDepotPanel.cs:313-315`, `:323-325`

Le numéro se compose de `NumeroPrefixe(format, date) + numeroId` où :

- le préfixe est **purement calendaire** — `{Année}/`, `{Année}/{Mois}` ou `{Année}/{Jour}{Mois}` (lignes 323-325), sans aucun identifiant de locataire ;
- `numeroId` est pré-rempli avec `factureSeq`, qui est **par locataire** (`Locataire.cs:66`, le commentaire le dit explicitement).

Deux locataires différents facturés le même mois, tous deux à `factureSeq = 1`, obtiennent **`2026/09001`** l'un comme l'autre. Aucun contrôle d'unicité n'existe nulle part.

En France, la numérotation des factures doit reposer sur une séquence **continue et unique** (art. 242 nonies A du CGI). C'est un sujet de conformité, pas de confort.

Le design est en outre contradictoire : le champ affiche « ID locataire (ex. 001) » — ce qui suggère un identifiant **stable** par locataire — alors qu'il est pré-rempli avec une séquence **incrémentée** à chaque émission (ligne 434).

**Correctif** : trancher entre les deux modèles. Soit une séquence unique globale à l'entreprise, soit préfixe calendaire + identifiant stable de locataire + séquence par locataire. Puis ajouter un contrôle d'unicité au moment de l'émission.

### H2 — Régularisation, dépôt et refacturation : double émission non protégée

**CONFIRMÉ** · `Facturation/FacturationSuivi.cs:189`, `Facturation/FactureLoyerPanel.cs:340`

`EstDejaEmise` existe et gère proprement la ré-émission comme une « correction » (même numéro, aucune séquence consommée). Elle n'est appelée **que** dans `FactureLoyerPanel`.

`FactureRegulPanel`, `FactureDepotPanel` et `FactureRefacPanel` n'ont aucune garde équivalente : un second clic sur « Sauvegarder et envoyer » consomme un nouveau numéro de séquence. Si le nom de fichier PDF est déterministe, la première facture est également écrasée sur le disque tout en restant référencée dans le suivi (*PLAUSIBLE* — non vérifié).

**Correctif** : appliquer le même passage par `EstDejaEmise` dans les trois panneaux.

### H3 — Une facture sans échéance n'est jamais détectée comme impayée

**CONFIRMÉ** · `Facturation/FacturationSuivi.cs:125`, `:137`, `:139`

`EtatDe` fait `hasEch = DateTime.TryParse(f.echeanceISO, out var ech)` (ligne 125). Si l'échéance est vide ou illisible, `hasEch = false`, le test d'impayé (ligne 137) est **sauté**, et la fonction retombe sur `return Etat.Envoye` (ligne 139).

Conséquence : la ligne reste « Envoyé » indéfiniment, ne bascule jamais en Impayé, et disparaît des créances. Le défaut de fond est que `hasEch == false` est traité comme « tout va bien » au lieu de « anomalie à signaler ».

**Correctif** : traiter l'absence d'échéance comme un état distinct et visible, pas comme un équivalent de « envoyé, rien à signaler ».

### H4 — Un bâtiment nouvellement créé n'est jamais écrit sur le disque

**CONFIRMÉ** · `Batiment/BatimentManager.cs:64`

Dans `AddBatiment`, l'appel `SaveBatiment(data.getBatiment())` est **commenté**. Le bâtiment n'existe qu'en mémoire jusqu'à une sauvegarde explicite ultérieure. Créer un bâtiment puis fermer l'app le fait disparaître.

Aucun `OnApplicationQuit` n'existe dans tout `Assets/Script` (*PLAUSIBLE*, relevé en exploration) : il n'y a pas de filet au moment de la fermeture.

---

## MEDIUM

### M1 — Token Mapbox actif commité dans le dépôt

**CONFIRMÉ** · `Assets/Prefab/Maps.prefab:340` · `Assets/Script/Maps/TileLoader.cs:9`

`mapboxAccessToken` est un champ `public`, donc sérialisé dans le prefab avec sa vraie valeur. Il est présent depuis le commit initial `165650a`, donc dans tout l'historique.

Le dépôt étant **privé**, la gravité reste MEDIUM : pas d'urgence de rotation, mais le token est lisible par quiconque y a accès, et le passage en public le publierait rétroactivement via l'historique.

Le projet dispose déjà du bon mécanisme (`ReglageService.ReadSecret`/`WriteSecret` → `<SaveRoot>/pennylane_secrets.dat`, hors repo). Par ailleurs `.gitignore` ne contient **aucune** règle `secret`, `key` ou `.dat`.

### M2 — Vingt blocs `catch` silencieux

**CONFIRMÉ** (comptage) · notamment `Batiment/PhotoService.cs`, `Facturation/FacturePdfService.cs`, `Facturation/ReglageService.cs:112`, `:128`

Sur `ReglageService` (écriture des secrets) et `FacturePdfService` (génération de PDF), une erreur avalée fait croire à une opération réussie. Une clé API illisible renvoie `""` sans diagnostic.

### M3 — Saisie numérique invalide convertie en zéro, sans signal

**CONFIRMÉ** · `Achat/AchatFormPanel.cs:194-200`

`Parse` ignore le booléen de `TryParse` et renvoie `0` en cas d'échec. La virgule française est bien gérée (`Replace(',', '.')` ligne 196), mais une saisie comme « 1 200 » (espace de milliers) échoue → taux ou montant à 0 → mensualité calculée **sans intérêts**, affichée comme valide.

### M4 — Risque latent de culture décimale sur les indices

**MEDIUM, non actif** · `RevisionLoyer/RevisionPanel.cs:330`, `:409` vs `RevisionLoyer/LoyerHistoryService.cs:193`

L'écriture utilise l'interpolation `$"{valeur:F2}"` (culture courante) alors que la relecture impose `CultureInfo.InvariantCulture`. **Vérifié empiriquement** : Unity tourne en culture `en-US` (séparateur `.`), l'aller-retour `125.50` → `125.5` fonctionne. Le défaut **n'est pas actif aujourd'hui**.

Il le deviendrait si la culture courante passait en `fr-FR` : tous les indices seraient relus à `0`, et le tableau de rentabilité indexée retomberait silencieusement sur un loyer plat — c'est-à-dire exactement ce que la fonctionnalité est censée remplacer, sans aucun signe visible.

**Correctif** : forcer `CultureInfo.InvariantCulture` à l'écriture, pour aligner sur la lecture.

### M5 — Migration d'entreprise pouvant écraser les réglages d'une autre

**PLAUSIBLE** · `Save/SaveLocationService.cs:74-79`

`MigrateData` utilise `File.Copy(..., overwrite: true)`. Migrer vers un dossier contenant déjà une entreprise écraserait son `reglage.json` (même nom) sans confirmation. La copie ne portant que sur `*.json`, les photos et le fichier de secrets resteraient en arrière.

Non vérifié dans le détail — à confirmer avant correction.

### M6 — Montants en `float` et absence de garde de signe

**PLAUSIBLE** · `Facturation/FactureRegulPanel.cs:360-361`, `FactureDepotPanel.cs:286`

Relevé en exploration, non vérifié ligne à ligne : soldes de régularisation et compléments de dépôt calculés sans garde de signe (facture à TTC négatif possible si les provisions dépassent les charges), TVA et TTC calculés indépendamment en `float` (HT + TVA ≠ TTC après arrondi à l'affichage).

`float` offre ~7 chiffres significatifs : au-delà de ~100 000 €, la précision au centime n'est plus garantie. Pour de la facturation, `decimal` est le type approprié.

---

## LOW

### L1 — `GetComponent<T>() ?? AddComponent<T>()` : 41 occurrences — **ÉCARTÉ comme bug**

**ÉCARTÉ** · 20 fichiers

Ce pattern a été testé empiriquement dans l'éditeur plutôt que jugé sur la théorie :

| Cas | `GetComponent` renvoie | Le `??` se déclenche |
|---|---|---|
| Objet frais, composant absent | vrai `null` | **oui — fonctionne** |
| Composant détruit, `GetComponent` refait | vrai `null` | **oui — fonctionne** |
| Référence morte conservée | faux-null | **non — renvoie l'objet mort** |

`GetComponent<T>()` renvoie toujours un **vrai** `null`, jamais un faux-null. Le piège du faux-null ne concerne que les **références stockées** vers un objet détruit — ce que ce pattern ne fait pas.

Les 41 sites sont donc **corrects**. Ils restent signalés par les analyseurs Unity UNT0023/UNT0008, d'où le classement LOW pour cohérence de style, mais **aucune réécriture de masse n'est justifiée**.

*Note d'honnêteté : une revue précédente avait classé ce pattern en HIGH et invoqué un `MissingComponentException` sur `TriDropdown` comme preuve. C'était erroné. La cause réelle de ce crash reste non identifiée.*

### L2 — Code mort contenant l'ancienne logique fautive

**PLAUSIBLE** · `RevisionLoyer/LoyerRevisionController.cs` (439 lignes)

D'après l'exploration, son GUID n'est référencé par aucune scène ni prefab. Le fichier contient l'ancienne logique de révision, celle qui choisissait l'indice par le trimestre de départ en ignorant le trimestre demandé. Le laisser en place expose à ce qu'il serve un jour de modèle. À supprimer après vérification du non-référencement.

### L3 — 42 `Debug.Log` résiduels

Aucun `TODO`/`FIXME` dans le projet, aucun `FindObjectOfType` dans un `Update()` : de ce côté, le code est sain.

---

## Écartés après vérification

Ces points ont été examinés et ne sont **pas** des défauts, mais méritent d'être listés car on pourrait légitimement les croire problématiques :

- **Virgule française dans les formulaires d'achat** — gérée par `Replace(',', '.')` (`AchatFormPanel.cs:196`).
- **Aller-retour des indices INSEE** — fonctionne, Unity étant en culture `en-US` (voir M4 pour le risque latent).
- **Collision d'identifiants** — tous les modèles utilisent `Guid.NewGuid()`.
- **Perte de champs par `JsonUtility`** — aucun `Dictionary` sérialisé ni polymorphisme dans les modèles.
- **`RentabiliteCalculator.Mensualite`** — les cas `durée <= 0` et `taux <= 0` sont correctement traités, pas de division par zéro.
- **Purge des backups** — `Directory.Delete(recursive)` ne s'applique qu'aux dossiers dont le suffixe correspond exactement au format de date attendu.

---

## Par où commencer

Classé par rapport valeur/risque, pas par gravité brute :

1. **C2** — un `try/catch` par fichier dans `LoadAll`. Quelques lignes, supprime le scénario « l'app ne démarre plus ».
2. **C1 + C3** — écriture atomique mutualisée (`.tmp` + `File.Replace` + `.bak`) pour `SaveBatiment` et `ReglageService.Save`, plus `reglage.json` dans le backup. Un seul utilitaire couvre les trois.
3. **H4** — décommenter la sauvegarde dans `AddBatiment`, une ligne.
4. **H2** — étendre `EstDejaEmise` aux trois panneaux, le mécanisme existe déjà.
5. **H1** — décision de conception à prendre avec toi avant tout code : séquence globale, ou identifiant stable de locataire.
6. **M1** — déplacer le token Mapbox vers le fichier de secrets et compléter `.gitignore`.

**H1 est le seul point qui demande un arbitrage métier de ta part** ; tous les autres ont un correctif technique évident.
