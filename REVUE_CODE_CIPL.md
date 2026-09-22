# Revue de code — application CIPL

**Date** : 16 septembre 2026 · **Périmètre** : 107 fichiers C#, ~20 920 lignes, UI comprise · **Branche** : `Facturation`

Chaque finding porte un statut :

- **CONFIRMÉ** — chemin de code tracé, scénario de défaillance vérifié.
- **PLAUSIBLE** — risque réel, mais dépend d'un cas d'usage non tranché.
- **ÉCARTÉ** — vérifié comme faux positif (listé quand on pourrait croire le contraire).

---

## Passation — à lire en premier (mise à jour 21/09/2026)

Ce document est le **suivi de la revue de code**. Tout ce qui suit a été écrit et compilé ; ce qui a été *réellement exécuté* est listé plus bas, et la distinction compte.

### Où en est le chantier au 22/09 — **156 tests EditMode verts**

Le détail de la facturation est dans `FACTURATION_CIPL_PENNYLANE.md` ; voici l'essentiel pour reprendre.

**Fait et validé en Play** : réorganisation des 4 panneaux de facture selon l'ordre du document imprimé · héritage en chaîne des réglages d'un locataire au suivant (T1 → T2 → T3, à travers les bâtiments) · textes du document réglables sur la facture, avec variables et menu « / » · **envoi réel par email**, branché sur les 4 panneaux.

**Le défaut majeur de la soirée — voir C4** : `FactureInfo` n'était pas `[Serializable]`, donc **aucun réglage de facture n'a jamais été écrit sur le disque**. Corrigé et verrouillé par des tests. Conséquence pratique : tout réglage saisi avant le 21/09 est perdu et doit être re-saisi une fois.

**Envoi par email — état réel** : fonctionne, validé avec **Gmail**. L'adresse `@cipl.fr` est **bloquée** : le locataire Microsoft 365 de l'entreprise interdit les mots de passe d'application et n'a pas SMTP AUTH activé, et le compte de l'utilisatrice n'est pas administrateur. Il faut l'admin du locataire, ou un compte OVH (le SPF de `cipl.fr` l'autorise déjà).

**À reprendre** : le tour en Play des panneaux Régularisation / Refacturation / Dépôt (seul le Loyer a été vu de bout en bout) · le rendu de la ligne de suivi passée en prefab (`SuiviFactureRow`, fait le 21/09 au soir — reste à voir à l'écran) · **H1**, la numérotation, toujours en attente de l'expert-comptable.

**Piège d'outillage** : le lanceur de tests par MCP se bloque si Unity est en **mode Play** ou si une scène est **non enregistrée** — il reste alors verrouillé jusqu'à une recompilation. Sortir du Play et enregistrer la scène avant de lancer.

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

### C4 — le réglage des factures n'était jamais enregistré (21/09/2026)

**CONFIRMÉ sur le fichier de sauvegarde réel** · `Facturation/FactureInfo.cs`

`[Serializable]` n'était pas sur `FactureInfo` : l'attribut avait glissé sur `FactureNumerotation`, une classe **`static`** où il ne sert à rien, parce qu'elle avait été insérée entre le commentaire de `FactureInfo` et son attribut. Syntaxiquement valide — **aucun avertissement**.

`JsonUtility` ignorait donc les quatre champs `factureLoyer / factureRegul / factureRefac / factureDepot`. Vérifié sur `batiment_*.json` : `factureSeq` et `facturesEtat` présents, **aucune** des quatre clés. RIB, entête, format de numéro, cases, textes, adresse d'envoi — tout tenait en mémoire puis disparaissait dès que les données repassaient par le JSON, ce que font les prefabs en permanence.

Trouvé par l'usage, pas par la relecture : « j'ai coché envoyer par email et je n'ai rien reçu ». La trace d'appel montrait la branche « pas d'envoi demandé », donc une case lue à `false` alors qu'elle avait été cochée — c'est en cherchant pourquoi que le fichier a livré la réponse.

Correctif : attribut remis sur la classe, commentaire expliquant pourquoi il est vital. `FactureInfoSerialisationTests` verrouille les deux faces : les réglages survivent à un aller-retour JSON, et les quatre clés apparaissent dans le texte produit.

*Même famille que C1/C2/C3 : une perte de données silencieuse. Elle rejoint aussi la leçon de `SaveAll` — compiler ne prouve rien, et ici même les 115 tests d'alors ne prouvaient rien, aucun ne relisant un modèle depuis le disque.*

### Prochaines étapes

1. Reste à passer en Play : gardes d'avoir (régul/dépôt) · `FermetureGuard` · parcours UI des panneaux · **le nouvel ordre des cartes, maintenant dans les quatre panneaux** et **la phrase de règlement au signe du solde, dès l'ouverture** (17/09, voir les deux sections dédiées). Le rendu de la régularisation est validé ; les trois autres ne le sont pas encore.
2. À passer en Play également (21/09) : les **textes de facture réglables dans le panneau** — « TVA sur les débits » et « montant mensuel » ont quitté *Options & envoi* pour la carte *Loyer facturé*, sous la ligne qu'ils commandent — et le **menu « / » sur tous les champs libres**, avec les variables réellement résolues. Vérifier qu'aucun `{jeton}` ne ressort sur le PDF ni sur l'aperçu texte.
3. À passer en Play également : les **phrases de l'explication du dépôt** (17/09, déplacées le 21/09 des Réglages vers la carte « Dépôt de garantie » du panneau Dépôt — les Réglages ne gardent que la phrase de retard et le bas de page, les seuls textes imprimés à l'identique sur les quatre types) et **l'héritage en chaîne des réglages de facture** (18/09) — régler le 1er locataire, créer le 2e et vérifier qu'il arrive déjà réglé, le retoucher, créer le 3e et vérifier qu'il suit le **2e**. Puis créer un **nouveau bâtiment** et vérifier que son 1er locataire reprend le dernier créé de l'autre bâtiment. La logique est couverte par 19 tests ; l'aller-retour par l'écran ne l'est pas.
   *Première version écartée le 18/09* : un modèle unique au niveau de l'entreprise, écrasé à chaque enregistrement. Il donnait le bon résultat sur l'enchaînement simple, mais pas la règle voulue — la source doit être le dernier locataire **créé**, et la copie appartenir à sa fiche. Machinerie retirée en totalité (`ModeleFacture`, `ReglageService.Modele`/`MemoriserModele`, les replis dans les quatre panneaux) : une seule règle vit dans le code.
4. À passer en Play (21/09, soir) : **le cycle de vie des charges** — régulariser des charges, vérifier qu'elles passent « en attente de paiement » (ambre) et non « payé », qu'elles disparaissent du choix, puis marquer la facture « Payé » dans le suivi et vérifier qu'elles passent au vert ; la repasser en « Impayé » et vérifier le retour en attente **sans** qu'elles redeviennent sélectionnables.
5. À passer en Play également : **l'envoi email sur Régularisation, Refacturation et Dépôt** — seul le Loyer a été vu de bout en bout. Tester surtout **l'échec** (mot de passe faux) : PDF présent, ligne non marquée, charges non payées, et un second essai qui reprend le même numéro.
6. ~~Prefab `SuiviFactureRow`~~ — **fait le 21/09 au soir** (voir la section dédiée). Reste le rendu à voir en Play, et la conversion de la seconde vue après comparaison.
7. H1, après avis de l'expert-comptable.
8. ~~Reliquat mineur~~ — **traité le 16/09**, voir la section « Reliquat mineur » plus bas : garde sur `ReloadFromDisk` · arguments Edge · annulation de suppression de photo.

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

### La phrase de règlement proposée à l'écran était inerte à l'ouverture (17/09/2026)

**CONFIRMÉ par lecture du chemin d'appel** · `FactureRegulPanel.cs`, `FactureDepotPanel.cs` — `DefaultSomme` / `LoadIntoUI`

Trouvé en reprenant le chantier, dans une modification **écrite mais pas encore commitée** : `DefaultSomme()` avait été rendue sensible au signe du solde, pour que le champ « Phrase de règlement » ne propose plus « SOMME À NOUS RÉGLER » quand le PDF, lui, imprimera « SOMME QUI VOUS SERA REMBOURSÉE » (`FactureEmission.PhraseSomme`). L'intention était juste, le câblage non : `LoadIntoUI` appelle `_autoSomme = DefaultSomme()` **avant** de remplir `_provisions` / `_ancien` et le sélecteur d'année. Le solde lu valait donc toujours 0.

Conséquence : à l'ouverture d'un panneau en situation de remboursement, **l'écran continuait d'afficher exactement la phrase que le correctif visait à supprimer**. Rien ne la recalculait ensuite — ni la saisie des provisions, ni le changement d'année ; seule une retouche manuelle de la date d'échéance, via le seul listener câblé sur `RefreshSommeDefault`, faisait apparaître la bonne phrase.

Correctif : `RefreshSommeDefault()` appelé **en fin de `RefreshTotaux()`** dans les deux panneaux. C'est le point de passage unique de tout ce qui change le solde — provisions, ancien dépôt, nombre de périodes, année, chargement de la fiche — donc une seule ligne par panneau plutôt qu'un listener sur chaque champ. La règle « une phrase saisie à la main est respectée » reste portée par la comparaison à `_autoSomme` : inchangée.

*La leçon n'est pas celle de `SaveAll` (« compiler ne prouve rien ») mais sa voisine : **un correctif peut être exact et n'être jamais atteint**. Ici il fallait suivre l'ordre de `LoadIntoUI`, pas relire `DefaultSomme`.* À vérifier en Play, comme tout ce qui touche à l'UI.

### Réorganiser les champs des 4 panneaux de facture — fait (17/09/2026)

**Principe : l'écran suit l'ordre du document imprimé.** Aujourd'hui le RIB est le premier champ alors qu'il s'imprime en dernier, et le « N° interne » vient après le numéro alors qu'il s'imprime au-dessus — d'où l'impression de désordre. Trois sections, correspondant aux trois zones du PDF :

| Section | Champs |
|---|---|
| **1. En-tête du document** | Date · Texte / N° interne · Format du n° + N° de facture |
| **2. Contenu** | Texte de présentation (+ aperçu collé dessous) · puis les champs propres au type (charges de l'année, ancien/nouveau dépôt, période de loyer…) |
| **3. Règlement** (pied) | Date d'échéance · Phrase de règlement · RIB CIPL · case pénalités de retard |

Gain principal : l'échéance, la phrase qu'elle alimente et le RIB deviennent voisins — la mention « (défaut de la phrase de règlement) » devient inutile, le lien se voit.

**Deux règles à tenir** : le **même ordre dans les quatre panneaux** (seule la section 2 varie selon le type) ; et dans chaque section, **le variable avant le stable** (date et montants changent à chaque facture, RIB et format de numéro une fois par an).

**Coût** : trois `UIFactory.Section` par panneau et un déplacement d'appels dans les méthodes de construction. Aucune logique touchée, aucun champ ajouté ni retiré — les 88 tests restent valides. À faire **panneau par panneau**, en commençant par la régularisation, avec validation visuelle en Play avant de propager aux trois autres.

**Fait sur `FactureRegulPanel` (17/09).** Un écart assumé avec la spec, décidé avec l'utilisatrice : les champs propres au type **ne fusionnent pas** dans la carte « Contenu », ils gardent leur carte et leur couleur juste en dessous — sinon « Montants » et « Dépôt de garantie » perdaient leur titre et la carte devenait très longue. Six cartes, donc, dans cet ordre :

| Carte | Champs |
|---|---|
| Destinataire (vert) | Nom · Adresse · SIRET — inchangée |
| **En-tête du document** (bleu) | Date · Texte / N° interne · Format du n° · ligne N° de facture |
| **Contenu** (taupe) | Texte de présentation · aperçu variables remplacées |
| Régularisation des charges (violet) | Année · charges · provisions · totaux — contenu inchangé |
| **Règlement** (ambre) | Date d'échéance · Phrase de règlement · RIB CIPL · case pénalités de retard |
| Options & envoi (vert) | TVA sur les débits · mode d'envoi · email · note « rien n'est émis » |

Libellé raccourci au passage : « Date d'échéance (défaut de la phrase de règlement) » → **« Date d'échéance »**. La parenthèse ne servait qu'à compenser l'éloignement des deux champs ; ils sont maintenant voisins.

Le vrai risque n'était pas le rendu mais **l'ordre de construction** : `RefreshEntetePreview` déréférence `_entetePreview` sans garde, et `BuildContext` lit `_provisions`, `_date` et `_anneeDD`. Vérifié avant de déplacer quoi que ce soit — tous sont créés avant les champs qui les déclenchent, et ni `UIDropdown.Create` ni `UIFactory.Toggle` n'invoquent leur callback à la création (sinon le code d'avant planterait déjà, `_chargesBox` étant affecté après `_anneeDD`). `_echeance` passant désormais **après** `_provisions`, sa lecture dans `DefaultSomme` a été gardée (`_echeance != null ? … : ""`), sur le modèle de `RefreshNumero`.

**Vérifié** : compilation propre (seule subsiste l'erreur Burst connue, venant des packages inutilisés) et **88/88 tests EditMode verts** — aucune logique n'ayant été touchée, un rouge aurait signalé un déplacement mal fait. **Reste à valider en Play** : c'est de la construction d'UI, aucun test ne la couvre.

**Propagé aux trois autres panneaux le 17/09, après validation du rendu de la régularisation.** Les quatre ont maintenant **exactement les six mêmes cartes dans le même ordre** ; seule la quatrième change de titre et de couleur :

| Panneau | Carte du type |
|---|---|
| Loyer | **Loyer facturé** (violet) — « Période facturée » l'a rejointe, voir ci-dessous |
| Régularisation | Régularisation des charges (violet) |
| Refacturation | Charge à refacturer (violet clair) |
| Dépôt | Dépôt de garantie (bleu canard) |

Deux points au-delà du simple déplacement :

- **« Période facturée » quitte l'en-tête du panneau Loyer** pour rejoindre les montants. Elle était dans la carte « Facture » avec le numéro, alors qu'elle s'imprime dans le **corps** du document — et c'est elle qui détermine le loyer de la ligne, donc elle appartient au même bloc que lui. La carte « Montants » est renommée « Loyer facturé » en conséquence : « Montants » serait devenu faux.
- **Les noms de couleurs mentaient d'un fichier à l'autre.** `CoAmbre` valait `#854F0B` (ambre) dans Loyer et `#7A5AA6` (**violet**) dans Refacturation : un même nom, deux couleurs, dans quatre fichiers qu'on modifie toujours ensemble — exactement le genre de piège qui fait qu'on colle la mauvaise teinte sans s'en apercevoir. `CoContenu` (taupe) et `CoReglement` (ambre) portent désormais le même rôle et la même valeur dans les quatre ; le violet des charges s'appelle `CoCharge`.

La garde `_echeance != null` de `DefaultSomme` a été posée dans les quatre, pas seulement là où elle est nécessaire aujourd'hui : le champ passe désormais **après** les montants dans les quatre panneaux, et la prochaine réorganisation ne doit pas avoir à y repenser.

**Vérifié après propagation** : plus aucune trace de l'ancienne carte « Facture » ni de couleur orpheline (`CoTaupe`/`CoAmbre` ne subsistent que dans un commentaire d'explication), compilation **sans aucune erreur** — pas même Burst cette fois — et **88/88 tests EditMode verts**.

### Six défauts trouvés par l'usage, et une évolution (22/09/2026)

Signalés en utilisant l'application, tous **CONFIRMÉS** par lecture du chemin de code. Trois ont la même signature que les défauts déjà rencontrés ici : ce n'est pas le calcul qui est faux, c'est qu'il n'est jamais atteint, ou qu'il porte sur la mauvaise donnée.

#### 1. Un champ affichait sa valeur DEUX fois — `InputAndText.cs`

Capture à l'appui : « Taille Batiment : 1250 [1250] m² », le texte et le champ de saisie côte à côte.

Cause, vérifiée en lisant l'état sérialisé des prefabs : **`textSaved` et `inputModify` sont actifs tous les deux dans les prefabs** — les 14 champs du bâtiment, les 11 du locataire, sans exception. L'affichage correct ne tenait qu'au fait qu'un `Modify()` ou un `ApplySave()` passe et masque l'un des deux. Tout champ qu'aucun de ces appels n'atteint montrait donc la valeur en double — et `Modify()` **sautait** justement `tailleBatimentText` dès qu'il y avait plus d'un locataire.

Correctif à la racine : `Awake()` pose l'état de repos (lecture seule) une fois pour toutes. Un seul endroit, tous les champs de l'application — y compris ceux des écrans Achat et Travaux, qui avaient le même défaut latent.

#### 2. L'échéance du loyer ignorait le jour où le loyer est demandé — `FactureLoyerPanel.cs`

Le panneau proposait « date de facture **+ 30 jours** », alors que le suivi calcule l'échéance d'une période avec `loc.jourDemandeLoyer` (« le X du mois »). La facture et sa propre ligne de suivi n'avaient donc pas la même échéance — et la phrase de règlement imprimée sur le PDF, qui découle de l'échéance, annonçait une date qui ne correspondait à rien.

La règle vivait déjà dans `FacturationSuivi.Lignes` : elle est extraite en `FacturationSuivi.EcheanceLoyer(loc, année, période)`, appelée par le suivi **et** par le panneau. Une seule règle, donc plus de divergence possible.

*Piège au passage, le même que la phrase de règlement du 17/09* : l'échéance était posée **avant** le sélecteur de période dans `LoadIntoUI`. Calculée là, elle n'aurait jamais vu la période choisie. Elle est donc posée après, et se recalcule à chaque changement de période ou d'année — tant qu'elle n'a pas été saisie à la main.

#### 3. La surface du bâtiment gonflait à chaque locataire — `BatimentPrefab.cs`

La surface du bâtiment était **dérivée** : dès deux locataires, `tailleBatiment = somme des lots`. Ajouter un locataire de 1500 m² à un bâtiment de 1250 m² en faisait un bâtiment de 2750 m². Pire, le lot du **premier** locataire n'était pas saisissable (il recopiait la surface du bâtiment) : impossible de le réduire pour faire de la place au second, alors que c'est exactement ce que la situation demandait.

Règle retenue — **le bâtiment est la donnée source** : sa surface est celle qu'on saisit, et elle ne bouge plus jamais toute seule. Chaque lot se saisit librement, pré-rempli avec ce qui reste. Un lot jamais saisi (`tailleLot <= 0`) prend le disponible — partagé à parts égales s'il y en a plusieurs — donc il **se réduit** quand un autre locataire prend sa part. Si les lots saisis dépassent le bâtiment, la saisie est respectée et l'écart est signalé une fois (pas à chaque enregistrement).

Aucun champ ajouté, donc **aucune migration** : la convention « `tailleLot <= 0` = pas encore défini » laisse les fiches existantes, qui ont toutes une valeur > 0, comme des surfaces fermes.

#### 4. Le suivi de facturation restait vide jusqu'au redémarrage — `LocataireSuiviInline.cs`

Deux causes, cumulées :

- **La sortie se faisait avant `Build()`.** À la création d'une fiche, la section de suivi est construite *avant* que le locataire entre dans `listLocataire` : `GetLocataire()` renvoyait `null`, le bandeau se masquait — et rien ne le rallumait, `Refresh()` sortant lui aussi sur `_loc == null` sans jamais relire. Pire, la coquille (`extBody`/`extTitre`) était alors perdue : l'appel suivant arrivant sans elle, le décor interne se serait reconstruit **par-dessus** la section de la fiche.
- **Régler le loyer ou le dépôt ne rafraîchissait rien.** Ces deux réglages passent par leurs propres pop-ups (`RevisionPanel`, révision du dépôt), pas par un panneau de facture — or seuls les panneaux de facture appelaient `RefreshFor`. Le tableau restait donc tel qu'il était avant le réglage.

Correctifs : `Refresh()` relit le locataire à chaque passage et rallume le bandeau ; `Setup` ne remplace la coquille que si on lui en fournit une ; et `LocataireSuiviInline.RefreshTous()` est appelée depuis `BatimentPrefab.SaveAfterModifyToDoListLocataire()` — le point de passage unique des **18** chemins qui modifient un locataire, plutôt qu'une garde répétée chez chacun.

#### 5. Le calendrier du loyer était écrit deux fois — `FacturationAlertes.cs`

Vérification faite en confirmant la règle voulue (rappel à J‑22, envoi à J‑15, échéance au jour de demande) : elle était **déjà implémentée**, avec les bons délais. Mais la même règle vivait à deux endroits — le suivi et les alertes — et les deux copies avaient divergé, exactement comme la ligne de tableau la veille.

- **Aucun rappel si le jour de demande n'est pas renseigné** : l'alerte abandonnait (`return null`) là où le suivi plaçait l'échéance au 1er. Des loyers à échéance s'affichaient sans le moindre rappel — sur les fiches au réglage incomplet, donc les plus exposées.
- **Les mois de facturation cochés étaient ignorés par le suivi** : sur un bail trimestriel facturé en février, l'alerte annonçait le 05/02 et le tableau le 05/01.

Les alertes passent désormais par `FacturationSuivi.EcheanceLoyer`, et les mois cochés commandent les échéances (décision de l'utilisatrice, 22/09) — de même que `PeriodeIndex`, son inverse, sans quoi la clé `loyer-{année}-P{n}` ne désignerait plus la bonne ligne. Détail dans `FACTURATION_CIPL_PENNYLANE.md`.

#### 6. Le suivi annonçait des envois qui n'avaient pas eu lieu — `FacturationSuivi.cs`

Constaté en documentant le point 5 : le statut se déduisait de la **date**, pas du fait. Une facture préparée à plus de 15 j de l'échéance était « En attente d'envoi », puis `EtatDe` la basculait toute seule en « Envoyé » à J‑15 — même si l'envoi par email n'était pas activé, cas où le panneau affichait pourtant « rien n'a été émis ». Le suivi en disait donc plus qu'il ne savait, le défaut exact de H3.

Le statut suit désormais le fait : les quatre panneaux savent déjà s'ils viennent du chemin « PDF seul » ou « envoi réussi » (`EnvoyerPuisFinaliser` n'enregistre qu'après acquittement du serveur) ; cette information est simplement transmise jusqu'à `MarquerEnvoye`. Une facture non partie reste « En attente d'envoi », ne devient jamais « Impayée » — on ne reproche pas un impayé à qui n'a rien reçu — et **garde son alerte allumée**, `DejaTraite` ne la comptant plus comme traitée. `EstDejaEmise`, elle, continue de la compter comme émise : le PDF existe, le numéro est consommé, H2‑bis reste couvert.

Le calendrier (15 j + 1 semaine) était écrit **trois fois**, dont un `22` en dur dans `Lead`. Une seule source désormais.

#### 7. Nouveau — envoi groupé proposé au lancement (`FactureEnvoiAuto.cs`)

Demandé après le point 6, et rendu possible par lui : puisqu'une facture reste « en attente d'envoi » tant qu'elle n'est pas partie, l'application sait exactement ce qui doit partir. Au démarrage, elle propose la liste des factures dont la date d'envoi (J‑15) est atteinte, **retards compris**.

*Rien ne part tout seul.* Un envoi ne se rappelle pas : la fenêtre montre locataire, destinataire et montant, chaque ligne se décoche, « Plus tard » reste disponible, et une seconde confirmation récapitule avant le départ. C'est l'arbitrage de l'utilisatrice face à un envoi réellement automatique — le travail manuel disparaît, la relecture non.

L'envoi se fait en série, chaque succès étant sauvegardé immédiatement : une interruption au milieu laisse marqué parti ce qui est parti. Un échec laisse la ligne en attente pour le lancement suivant. Le statut est mis à jour directement, sans repasser par `FactureEmission` — le numéro est déjà consommé, y repasser en prendrait un second.

La règle de sélection est **séparée de l'écran** et l'existence du PDF lui est injectée : elle se teste sans disque et sans mode Play (10 assertions). Vérifiée par mutation — en retirant la garde de date puis celle de statut, trois tests échouent (« une facture déjà envoyée repart », « une facture non due part trop tôt »).

#### Vérification

**156 tests EditMode verts** (126 + 30). Les nouveaux (`SurfacesEtEcheanceTests`) couvrent la règle des surfaces avec le scénario exact rapporté et le calcul d'échéance (jour borné à la longueur du mois — « le 31 » en février tombe le 28, ou le 29 en année bissextile — périodicités trimestrielle, semestrielle, annuelle), plus le fait que les lignes du suivi passent bien par la règle extraite.

**Les tests mordent** : vérifié par mutation. En remettant l'ancien calcul de surface, trois tests échouent avec le bon message (`Expected: 750, But was: 1250` — le bug rapporté, exactement), puis repassent au vert après restauration.

**Reste à voir en Play** : le champ de taille qui n'affiche plus qu'une valeur · la répartition des surfaces sur un bâtiment à deux locataires · l'échéance proposée à l'ouverture du panneau Loyer et au changement de période · le suivi de facturation rempli **sans quitter l'application**, juste après avoir créé un locataire et réglé son loyer et son dépôt.

### Ligne de suivi en prefab — fait (21/09/2026, soir)

Spécifié le 17/09, réalisé ici. Les deux vues du suivi construisaient **la même ligne** en code, chacune de son côté — c'est ce qui a permis au défaut d'alignement (`childForceExpandWidth`) d'exister en double, et, on le découvre au passage, à la garde « Quittance » de diverger (voir plus bas).

**Livré** : `Assets/Prefab/SuiviFactureRow.prefab` + `Facturation/SuiviRowUI.cs`, et `LocataireSuiviInline` converti. `FacturationSuiviPanel` reste **en code**, comme prévu : c'est le filet de comparaison des deux rendus en Play.

Le prefab a été construit **par script** (Roslyn, via MCP) en réutilisant `UIFactory` plutôt qu'en re-saisissant couleurs et largeurs à la main : le rendu est identique par construction, pas par recopie.

**Trois choses que la spécification du 17/09 n'avait pas vues** — toutes trouvées en faisant, pas en relisant :

| Surprise | Conséquence |
|---|---|
| **`UIFactory.Rounded()` génère son sprite au runtime** (`UIFactory.cs:11`) : ce n'est pas un asset, la référence ne survit pas à la sérialisation du prefab | Les coins arrondis auraient disparu, silencieusement. `SuiviRowUI` repose le sprite sur chaque `Image` à l'instanciation — donc le prefab **ne supprime pas** tout le code de style, contrairement à ce qu'annonçait la spec |
| **Aucune des deux vues ne peut recevoir de référence d'inspecteur** : `LocataireSuiviInline` est un `AddComponent` à la volée (`LocataireFacturationFields.cs:290`), `FacturationSuiviPanel` un `new GameObject` | La spec supposait un câblage direct, impossible. Le prefab transite par `LocatairePrefab` (`suiviRowPrefab`), que les deux vues ont déjà en main via `_fiche`. Pas de dossier `Resources/` créé : le projet n'en a aucun et n'appelle jamais `Resources.Load` |
| **L'en-tête n'est pas une ligne comme les autres** (ni pastille, ni actions) | La laisser en code aurait fait *réapparaître* le défaut visé : deux constructions pour un même alignement. Elle passe par le même prefab via `SetupEntete`, où les colonnes État et Actions deviennent du texte nu. Idem pour la ligne « Aucune facture » (`SetupVide`) |

**Verrouillé par 9 tests** (`Assets/Editor/Tests/SuiviRowPrefabTests.cs`) — c'est le point que cette revue redoutait, « des références sérialisées fragiles, **invisibles aux tests** ». Ils le rendent visible sans passer par le Play : les 11 références du prefab, `childForceExpandWidth == false` (le défaut historique, désormais figé), les largeurs de colonnes comparées aux constantes de `SuiviRowUI`, le câblage de `suiviRowPrefab` sur la fiche relu **depuis le disque**, puis le comportement de `Setup` / `SetupEntete` / `SetupVide` (nombre d'actions activées, pastille non cliquable sur période clôturée, cellules masquées puis rétablies, callback rattaché à la bonne ligne).

**Les tests mordent-ils vraiment ?** Vérifié par mutation : en retirant une seule ligne de `SetupVide`, la suite échoue avec le bon message (`montant masqué — Expected: False, But was: True`), puis repasse au vert après restauration. *Un test vert ne prouve rien s'il n'atteint pas le code — même leçon que « compiler ne prouve rien ».*

**Piège d'outillage découvert ici** : les tests salissaient `Assets/TextMesh Pro/.../LiberationSans SDF - Fallback.asset` (+114 lignes) à **chaque exécution**. Un libellé accentué, un « € » ou un tiret cadratin remis à TMP peuple l'atlas dynamique de la police de repli, qui est un asset du dépôt. Les libellés de test sont donc en ASCII, avec un commentaire qui interdit de les « corriger » — les assertions ne comparent que la chaîne transmise, jamais le rendu. L'asset a été remis à son état commité.

**Reste à voir en Play** : alignement en-tête / lignes, libellé long tronqué en ellipse, année sans facture, menu d'état, les boutons d'action dans chaque cas (`PDF` + `Corriger`/`Refaire`, `Rappel`, `Quittance`), période clôturée sans action — puis comparer avec la vue plein écran restée en code.

**Ensuite** : convertir `FacturationSuiviPanel` au même prefab une fois les deux rendus comparés, puis `FacturationHomeSection`. Ne **pas** convertir `ReglagePanel` ni les 4 panneaux de facture : formulaires construits une seule fois, aucune répétition — arbitrage inchangé.

### La garde « Quittance » manquait dans la vue plein écran (21/09/2026)

**CONFIRMÉ par comparaison des deux copies** · `Facturation/FacturationSuiviPanel.cs`

Trouvé en convertissant la ligne en prefab, donc en mettant les deux constructions côte à côte. `LocataireSuiviInline` vérifiait `reellementEmise` (numéro **et** PDF non vides) avant de proposer « Quittance » ; `FacturationSuiviPanel` ne le vérifiait pas.

L'état « Payé » peut être forcé à la main sur une ligne **jamais émise** : dans la vue plein écran, on pouvait donc éditer une quittance — un reçu de paiement — pour une facture qui n'existe pas. Correctif : même garde des deux côtés.

*C'est le second défaut imputable à cette duplication, après l'alignement. Le motif de passer la ligne en prefab n'était donc pas cosmétique : deux copies d'un même écran divergent, et la divergence se paie en pièces comptables fausses.*

### Nettoyage du projet (16/09/2026)

Méthode : rien n'a été supprimé sur une impression. Pour chaque fichier, le GUID de son `.meta` a été cherché dans les 37 scènes/prefabs/assets **et** son nom dans les 111 scripts ; seuls les candidats à **zéro référence des deux côtés** ont été retenus, puis vérifiés un par un. Tout ce qui a été supprimé est suivi par git, donc récupérable par `git checkout`.

**Supprimé** — 5 scripts (756 lignes), 13 méthodes (85 lignes), 2 prefabs, 1 image :

| Type | Éléments |
|---|---|
| Scripts | `PLU/PLUPanelUI.cs` · `Maps/TextureScale.cs` · `Tools/NestedScrollRect.cs` · `Maps/PineSpriteGenerator.cs` · `Facturation/FacturationGlobalPanel.cs` (353 l., voir ci-dessous) |
| Prefabs | `Building Card.prefab` (ancienne version — c'est `BuildingCardItem.prefab` qui porte le composant `BuildingCard` en scène) · `Toggle Possiblity .prefab` |
| Image | `UI/sec_arrow.png` |
| Méthodes | `BatimentManager.LoadBatiment`/`GetSaveFolder` · `BatimentPrefab.GetBatimentData` · `DateInputController.LoadSavedDate` · `MapController.getLat`/`getLon` · `TileLoader.HidePin` · `TrimestreInput.CannotModify`/`GetPreviousYear` · `SaveIO.LoadSave` · `ScrollAutoResize.ScrollToBottom` · `RentabiliteCalculator.CoutInterets` · `TravauxController.GetSaveData` |

**Faux positifs écartés — à ne jamais supprimer sur la foi d'une recherche de références :**

| Élément | Pourquoi il n'a aucune référence | |
|---|---|---|
| `FacturationBootstrap`, `EntrepriseBootstrap` | `[RuntimeInitializeOnLoadMethod]` : **Unity les exécute sans que personne ne les cite**. Les supprimer aurait cassé l'initialisation au démarrage. | gardés |
| `papperData.cs` | Le fichier ne porte pas le nom de ses types : il déclare `AnnuaireEntreprise` et `AnnuaireResponse`, bien utilisés par la recherche SIRET. | gardé |
| `StreamingAssets/logo_cipl.png` | StreamingAssets se charge **par chemin**, pas par GUID — c'est le logo imprimé sur les factures. | gardé |
| `InputFieldMinHeight.CalculateLayoutInput*` | Implémentations de `ILayoutElement`, appelées par le moteur de layout. | gardées |
| `ScrollAutoResize.OnTransformChildrenChanged` | Message Unity. | gardé |

**Refactorisé** — deux duplications supprimées, sans changement de comportement :
- `Sanitize` existait en **5 exemplaires** (4 identiques + la variante de la quittance) → `DossiersDonnees.NomFichier`, à côté de `NomDossier`. La quittance garde sa spécificité (espaces en tirets, repli « Quittance ») mais s'appuie sur la base commune.
- Les listes de formats de numérotation étaient **dupliquées dans les 4 panneaux** de facture, avec le risque qu'elles divergent → `FactureNumerotation.Labels`/`.Ids`, source unique.

**Vérifié** : compilation propre, puis 9 assertions en exécution (neutralisation des caractères interdits, `null` toléré, nom valide inchangé, formats de numérotation, survie des bootstraps, des types Pappers et de `BuildingCard`, `CheminPdf` et `Mensualite` toujours opérationnels).

**`FacturationGlobalPanel.cs` — supprimé sur décision de l'utilisatrice (16/09)**. Vue globale des créances (filtres bâtiment/banque/locataire/état + tableau des factures dues), jamais branchée à aucune scène ni à aucun code. La colonne « Créances » du menu est assurée par `FacturationHomeSection`, qui reste en place. Le fichier étant suivi par git, il est récupérable par `git checkout` si le besoin d'une vue globale filtrable revient — c'est la référence à reprendre plutôt qu'à réécrire.

**Laissé en l'état** : `Col(string)` dupliqué en 4 exemplaires d'une ligne (gain nul, risque non nul).

**Audit de la scène — rien à nettoyer.** `SampleScene` : 643 GameObjects, **0 script manquant** (aucune référence cassée), **0 coquille vide** (objet sans composant ni enfant). Les **29 objets désactivés ne sont pas du déchet** : ce sont les `Template` des dropdowns — Unity les désactive par conception — et les écrans masqués activés par code (`Calcul`, `Savegarde`, `Panel PLU/GroupeCadastre`, `Objectif`). Les 4 « noms dupliqués sous un même parent » sont des éléments répétés légitimes (chips de modes du calcul rapide, colonnes d'en-tête). Les suppressions de fichiers n'ont créé **aucun orphelin en cascade** (scan relancé après coup).

**Reste le plus gros gisement, non traité : les packages.** `Packages/manifest.json` déclare `com.unity.feature.2d`, qui tire 2D Animation, Aseprite, PSD Importer, SpriteShape, PixelPerfect, Tilemap Extras, **Burst**, Collections et Mathematics — **aucun fichier du projet ne les référence**, et c'est Burst qui produit les erreurs de compilation observées dans la console. S'y ajoutent `com.unity.timeline`, `com.unity.visualscripting` (son unique `using`, dans `BatimentPrefab.cs`, était inutile et a été retiré) et une quinzaine de modules sans emploi pour une application de gestion en UI 2D (`ai`, `cloth`, `particlesystem`, `physics`, `physics2d`, `terrain`, `vehicles`, `vr`, `xr`, `wind`, `umbra`, `video`…).

Non fait volontairement : retirer un package touche la résolution de dépendances et la compilation de **tout** le projet. Le gain est du confort (temps de compilation, poids du build), pas une correction — le rapport risque/bénéfice ne justifie pas de le faire sans validation. À traiter par petits lots, avec une compilation vérifiée entre chaque.

### Protocole d'émission mutualisé — `FactureEmission` (16/09/2026)

**Le problème de fond, pas ses symptômes.** Les quatre panneaux de facture recopiaient la même séquence d'émission : détecter une facture déjà émise, suffixer le numéro et le nom de fichier, générer, puis marquer le suivi et avancer la séquence. Quatre copies, donc quatre occasions d'oublier — et c'est exactement ce qui s'est produit : **H2** (garde présente sur un seul panneau), **H2-bis** (deux états sur quatre), **G1** (`pdfPath` absolu répété), **G3/G4** (chemins construits à la main).

`Facturation/FactureEmission.cs` porte désormais cette règle, en deux temps dont l'ordre n'est pas négociable :

| | |
|---|---|
| `Preparer(loc, key, numeroPropose)` | **Avant** la génération. Décide correction ou première émission, renvoie le numéro à imprimer et le suffixe de nom de fichier (`-corrigee2`) — donc le PDF d'origine n'est jamais écrasé. |
| `Enregistrer(...)` | **Après** une génération réussie. En correction : conserve numéro et statut, n'avance pas la séquence. En première émission : marque la ligne envoyée, consomme le numéro, oublie l'ID saisi. |

`RibNom(ribId)` remplace au passage quatre copies de trois lignes.

**Vérifié** : plus aucun panneau n'appelle `EstDejaEmise`, `MarquerCorrige` ou ne touche `factureSeq` directement — la recherche ne renvoie rien. Chacun se contente de deux appels au service.

**Ce qui n'a pas changé** : chaque panneau garde son libellé propre (« charges passées en payé », « le montant du dépôt n'a pas été modifié ») via un paramètre optionnel — la mutualisation ne devait pas appauvrir les messages, c'est le genre de régression invisible qui passe les tests et se voit à l'usage.

**Six tests ajoutés** (`FactureEmissionTests`) : première émission qui consomme un numéro · seconde émission traitée en correction sans rien consommer · corrections successives numérotées avec une seule séquence consommée en tout · loyer préparé en avance également protégé · messages.

**Second lot — les helpers partagés (16/09)** : `TryDate` existait en **quatre copies strictement identiques** et `NumeroPrefixe` aussi (la seule différence entre elles était un commentaire — vérifié, il n'y avait pas de divergence réelle, mais rien ne l'empêchait).

- `TryDate` → `Tools/SaisieDate.cs`, pendant de `SaisieNumerique` : formats explicites (`dd/MM/yyyy`, `d/M/yyyy`, `dd/MM/yy`, `d/M/yy`) en culture invariante. Utilisable par les autres formulaires qui parsent des dates à la main.
- `NumeroPrefixe` → `FactureNumerotation.Prefixe`, à côté des formats `AN`/`AMN`/`AJMN` qu'il consomme. **C'est là que se jouera H1** : l'arbitrage sur la numérotation (séquence unique globale ou identifiant stable de locataire) ne touchera plus qu'un seul endroit.

Les noms locaux sont conservés en délégation d'une ligne : **aucun site d'appel n'a été modifié**, donc aucun risque de rupture. Vérifié : plus aucun `DateTime.TryParseExact` ni `switch (fmt)` dans les panneaux. 12 tests ajoutés (`SaisieDateTests`), dont le cas `03/04/2026` qui doit toujours être le 3 avril.

Les quatre panneaux passent de 2 850 à **2 770 lignes** — le gain en volume est modeste, l'important est ailleurs : les règles qui produisaient des bugs (émission, numérotation, dates) n'ont plus qu'une seule implémentation.

**Prochaine étape possible** : ce qui reste dupliqué est de la **construction d'UI** (`LoadIntoUI`, `SaveFromUI`, `ShowPreview`, `ZoomBtn`, `RefreshEntetePreview`…), pas de la règle métier. L'extraire demanderait une classe de base MonoBehaviour et présente un risque bien supérieur pour un gain surtout cosmétique. À ne faire que si ces panneaux doivent réellement évoluer.

### Tests automatisés — 66 tests EditMode (16/09/2026)

Jusqu'ici, **chaque vérification était refaite à la main**, par scripts jetables. Ces scripts sont désormais des tests que n'importe qui relance en moins de 2 secondes.

**Comment les lancer** : `Window → General → Test Runner → EditMode → Run All`. Aucun mode Play requis.

| Fichier | Couvre |
|---|---|
| `Assets/Editor/Tests/SaisieNumeriqueTests.cs` | M3 / M3-bis — symétrie point/virgule (8 paires), séparateurs mixtes, espaces insécables, saisie invalide refusée, champ vide |
| `Assets/Editor/Tests/FacturationSuiviTests.cs` | H2 / H2-bis / H3 / G1 — les 4 états « émise », numéro et séquence préservés à la correction, statut conservé, impayé à échéance+15, échéance en culture invariante, `pdfPath` relatif et ancien format absolu |
| `Assets/Editor/Tests/DonneesDisqueTests.cs` | M5 / G1 / G2 / G4 — nommage des fichiers, aller-retour relatif↔absolu, migration acceptée puis refusée sans rien écraser, corbeille photos (suppression → annulation → purge), vidage du cache |
| `Assets/Editor/Tests/InfraTest.cs` | L'infrastructure elle-même |

**Où vivent les tests, et pourquoi là.** Le code de CIPL est dans `Assembly-CSharp` (aucun `.asmdef`), et un assembly défini par `.asmdef` **ne peut pas** référencer `Assembly-CSharp`. Les tests sont donc sous `Assets/Editor/`, compilés dans `Assembly-CSharp-Editor`, qui lui y a accès — et exclus des builds. Vérifié empiriquement avant d'écrire la suite.

**Isolation des données — le point le plus important.** `DonneesDisqueTests` écrit sur le disque. Son `SetUp` mémorise la racine réelle puis bascule `SaveLocationService` sur `%TEMP%/cipl_tests_<guid>/CIPL_Saves` ; son `TearDown` **restaure la racine réelle en première instruction**, avant même de nettoyer. Vérifié après exécution : racine rendue, 3 bâtiments intacts. Toute évolution de ces tests doit préserver cette garantie.

*Deux tests ont échoué au premier lancement, et les deux fois c'était le test qui avait tort, pas le code : un chemin de PDF placé hors racine (que `VersRelatif` laisse donc absolu, à juste titre) et un `Debug.LogError` volontaire non déclaré par `LogAssert.Expect`.*

**Non couvert** : la corbeille des bâtiments (`DeleteBatiment`/`RestoreBatiment` exigent une scène et `BatimentManager.Instance` — vérifiée par réflexion en session, pas figée en test) · `FermetureGuard` avec fiche réellement en édition · les gardes d'avoir et les parcours UI des panneaux. Ce sont les mêmes points que la liste « à passer en Play ».

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
