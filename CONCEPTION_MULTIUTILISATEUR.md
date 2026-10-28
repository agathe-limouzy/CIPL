# Conception — mode multi-utilisateur de CIPL

> Document de conception (09/10/2026). **Rien n'est codé.** Toutes les décisions ci-dessous ont été validées avec l'utilisatrice pendant la session du 09/10.
> À lire avec `ARCHITECTURE_CIPL.md` (§5 Persistance, §6 Cycle de vie).

---

## 1. Le besoin

CIPL sera vendu à des entreprises. Dans une entreprise, plusieurs personnes travaillent en même temps, comme sur un document Drive :

- **chaque personne a son identifiant**, rattaché à l'entreprise ;
- **chacun voit en direct** les modifications des autres ;
- **une fiche (bâtiment ou locataire) en cours de modification est verrouillée** pour les autres, jusqu'à ce que la personne ait fini ;
- **les documents** (factures PDF, baux, photos…) créés par l'un se retrouvent chez les autres.

## 2. Ce qui empêche le multi-utilisateur aujourd'hui

| Constat (code actuel) | Conséquence à plusieurs |
|---|---|
| Pas de serveur. Un JSON local par bâtiment, **locataires inclus** (`batiments/batiment_<id>.json`) | Rien n'est partagé entre deux PC |
| On enregistre toujours **le bâtiment entier** : `BatimentManager.SaveBatiment`, et `BatimentPrefab.SaveAfterModifyToDoListLocataire` qui est appelé par une vingtaine d'endroits | Paul enregistre Volteo, Marie enregistre Atelier Nord dans la même minute : la dernière écriture écrase l'autre |
| `SauvegardeDeFermeture` (`OnApplicationQuit`) réécrit **tout** ce qui est en mémoire | En fermant, Paul écraserait tout ce que Marie a fait depuis son démarrage à lui |
| `LoadAll` écrit au chargement (`ChargeBatiment.Reconstituer`, `EffectuerCession`, `BatimentManager.cs:364`) | Chaque PC qui démarre réécrit les mêmes bâtiments |
| Les documents sont dans un dossier local propre à chaque PC (`DossiersDonnees`) | Marie n'a pas la facture que Paul vient d'émettre |
| Les entreprises sont une liste locale (`EntrepriseService`, PlayerPrefs) | Pas de notion de compte ni de membres |

---

## 3. Décisions validées (09/10)

| Sujet | Décision |
|---|---|
| Hébergement | **Cloud hébergé par nous (SaaS)**, service type Supabase : base Postgres, comptes, temps réel, présence, stockage de fichiers. Hébergement en UE. Internet est nécessaire pour modifier |
| Comptes | Connexion par e-mail et mot de passe. Un compte peut être membre de plusieurs entreprises (par exemple un comptable) |
| Rôles | **Admin** : inviter et retirer des personnes, Réglages (SMTP, Pennylane, RIB, mode des documents), forcer la libération d'un verrou, exporter les données. **Utilisateur** : tout le reste |
| Licences | Abonnement **par entreprise et par utilisateur** : un prix de base, plus un nombre de places (« 3 / 5 licences »). L'admin ne peut pas inviter au-delà. Précisé le 11/10 : un **groupe** paie pour toutes ses entreprises, avec -30 % sur chaque entreprise après la première, et une personne compte pour une seule licence dans tout le groupe (voir §13) |
| Ce qui verrouille | Le clic sur **« Modifier »**, le **mode facturation** (panneaux de facture), les **sous-menus Dépôt et Loyer**, et **l'ajout d'une charge, de travaux ou d'un coût** |
| Portée du verrou | **La seule fiche concernée** : le bâtiment OU un locataire. Marie modifie Volteo, Paul peut modifier Atelier Nord ou la fiche du bâtiment rivoli |
| Pendant le verrou | Les autres voient la fiche **en lecture seule**, avec un bandeau « En cours de modification par Marie Dupont (facturation) depuis 14 h 32 ». Les boutons Modifier, Facturation et Loyer sont grisés |
| Libération | À l'enregistrement ou à l'annulation. **Plantage ou fermeture** : le verrou tombe seul 2 min après la perte du signal. **Inactivité de 15 min** : une fenêtre « Toujours là ? » s'ouvre avec un compte à rebours de 2 min, puis les saisies non enregistrées sont annulées et la fiche libérée. **Un admin** peut forcer la libération |
| Actions en un clic (payé ou impayé, rappel, versement, objectif, photo, bail ajouté, archiver) | Pas de verrou durable. **Refus si la fiche est verrouillée par quelqu'un d'autre** (« Volteo est en cours de modification par Marie »), sinon écriture immédiate avec contrôle de version |
| Mise à jour en direct | La fiche affichée se rafraîchit seule, avec un message en bas : « rivoli mis à jour par Marie » (`UndoToast.ShowInfo`) |
| Refacturation d'une charge | La charge verrouille le bâtiment. Si un locataire concerné est verrouillé, la refacturation est refusée avec `ConfirmDialog.Erreur` : « Volteo est en cours de modification par Paul, réessayez quand il aura fini ». Rien n'est écrit à moitié |
| Envoi automatique des factures | **Réservation par facture**. La fenêtre « N factures sont à envoyer » s'ouvre chez chacun, mais la première personne qui envoie fait passer chaque facture à « envoi en cours par Marie », et ces factures disparaissent de la liste chez les autres |
| Hors ligne | **Lecture seule** sur la dernière copie reçue, avec un bandeau rouge « Hors ligne — lecture seule. Données à jour au 09/10 à 14 h 41 » et un bouton « Réessayer » |
| Reprise après une coupure | Si la fiche a été modifiée par quelqu'un d'autre entre-temps : refus « Marie a modifié Volteo pendant votre déconnexion. Vos saisies n'ont pas été enregistrées. » Les saisies restent à l'écran pour être recopiées, puis la fiche se recharge. Si personne n'y a touché, l'enregistrement passe normalement |
| Secrets (clé Pennylane, mot de passe SMTP) | Transmis à l'application de chaque membre, car l'envoi part du poste. Visibles et modifiables **par l'admin seulement** |
| Sauvegardes | Sauvegarde quotidienne sur le serveur, plus un bouton admin **« Exporter toutes les données »** (JSON et documents, dans un dossier local). Le `BackupService` du démarrage disparaît |
| Documents partagés | **Tous** : ceux que CIPL génère (factures, avoirs…) et ceux déposés dans l'application (baux, avenants, photos, PDF des charges, logo) |
| Référence des documents | Le **stockage cloud** de CIPL. Chaque PC en garde en plus une copie dans le dossier de l'entreprise |
| Copie locale des documents | Mode choisi par l'admin. **« Drive partagé »** : CIPL écrit le fichier seulement chez la personne qui le crée, et OneDrive, Google Drive ou SharePoint le recopie chez les autres. **« Sur chaque PC »** : chaque application télécharge elle-même les nouveaux documents |
| Dossier sur le PC | **Corrigé le 12/10** : personne ne choisit de dossier au premier lancement. En mode Drive, CIPL **retrouve seul** le dossier partagé sur chaque ordinateur grâce à un fichier repère ; en mode « Dans CIPL », il crée `Documents\CIPL\SCI Rivoli` sans rien demander (voir §13). Le chemin trouvé est mémorisé par poste, comme aujourd'hui (`SaveLocationService`) |
| Suppression d'un document | Chez les autres, le fichier est rangé dans la corbeille locale, jamais effacé |
| Fichier déposé à la main dans l'Explorateur | **Non partagé** : seul ce qui passe par l'application l'est |

---

## 4. Architecture cible

```
┌─────────────── PC de Paul ───────────────┐      ┌──────────── SERVICE CLOUD (UE) ────────────┐
│ CIPL (Unity)                             │      │  Comptes (e-mail + mot de passe)           │
│  Interface ─ Règles métier (inchangées)  │ REST │  Postgres : entreprises, membres,          │
│  CloudClient  ── lecture / écriture  ────┼─────►│    batiments, locataires, verrous,         │
│  Temps réel   ◄── changements, présence ─┼◄─────┤    documents, reglages, secrets            │
│  Cache local (persistentDataPath)        │  WS  │  Règles d'accès : membre de l'entreprise   │
│   └ dernière copie, pour la lecture      │      │  Stockage de fichiers : documents          │
│     seule hors ligne                     │      │  Sauvegardes quotidiennes                  │
│  Dossier de l'entreprise (choisi par PC) │      └────────────────────────────────────────────┘
│   └ documents seulement                  │
└──────────────────────────────────────────┘
```

- **Les règles métier ne changent pas** (Loyers, FacturationSuivi, FactureEmission…). Seule la couche Persistance change : `BatimentManager.SaveBatiment` n'écrit plus dans un fichier, il envoie au serveur.
- **Le cache local** (copie des JSON) va dans `persistentDataPath`, **jamais dans le dossier de l'entreprise** : en mode Drive, il serait synchronisé par le Drive et entrerait en conflit.
- **La génération des PDF** (Microsoft Edge) et **l'envoi SMTP et Pennylane** restent sur le poste. Le serveur ne fait que stocker et coordonner.

---

## 5. Modèle de données sur le serveur

Principe : on garde le JSON actuel (`JsonUtility`) comme contenu, mais **on le coupe en deux niveaux**. Le bâtiment est stocké sans ses locataires, et chaque locataire a sa propre ligne. C'est ce qui permet de verrouiller et d'enregistrer un locataire sans toucher aux autres.

| Table | Colonnes principales | Remarque |
|---|---|---|
| `entreprises` | id, nom, licences_max, mode_documents (`drive` / `chaque_pc`) | |
| `membres` | user_id, entreprise_id, role (`admin` / `utilisateur`), nom_affiche, statut (`invite` / `actif`) | Compter les membres actifs et invités permet d'appliquer `licences_max` |
| `batiments` | id, entreprise_id, data (JSON **sans** `locataireDuBatiment`), version, maj_par, maj_le | |
| `locataires` | id, batiment_id, entreprise_id, data (JSON d'un `Locataire`), version, maj_par, maj_le | `factureSeq` et `facturesEtat` sont dedans |
| `reglages` | entreprise_id, data (`ReglageData`), version | |
| `secrets` | entreprise_id, cle, valeur | Lus par l'application de chaque membre pour envoyer. Affichés et modifiables seulement par l'admin |
| `verrous` | cible_type (`batiment` / `locataire`), cible_id (**clé unique**), session_id, user_id, motif (« modification », « facturation », « loyer »…), pris_le, expire_le | Une seule ligne par fiche : c'est la base qui garantit qu'il n'y a qu'un seul détenteur |
| ~~`envois_factures`~~ | — | **Abandonnée le 15/10 (étape 7)** : `reserver_envoi` prend le verrou du locataire et vérifie le statut de la facture dans sa fiche. Le statut reste à un seul endroit |
| `documents` | entreprise_id, chemin (relatif), taille, empreinte (SHA-256), auteur, cree_le, supprime_le | `chemin` est le même que celui stocké par `DossiersDonnees.VersRelatif` |

**Règles d'accès** : on ne lit et on n'écrit que les lignes de son entreprise. Les tables `membres`, `secrets` et `entreprises` ne sont modifiables que par un admin.

**Version (verrouillage optimiste)** : chaque enregistrement envoie la version lue (`... WHERE id = X AND version = 7`). Si la ligne a changé entre-temps, l'écriture est refusée. Ce filet de sécurité attrape tout ce que les verrous ne couvrent pas, dont la reprise après une coupure.

**Numérotation des factures** : `factureSeq` est **par locataire** (`FactureEmission.cs:107`). L'émission se fait sous le verrou du locataire et l'enregistrement est contrôlé par la version, donc deux numéros identiques sont impossibles. Une fonction serveur dédiée n'est pas nécessaire (cela simplifie la décision « numéro attribué par le serveur »).

---

## 6. Règles du verrou

### 6.1 Cycle de vie

```
Clic « Modifier » (ou Facturation, Loyer, Dépôt, ajout de charge, travaux ou coût)
   └─ Acquérir(cible) : on insère la ligne dans `verrous`
        ├─ ligne libre, ou expirée (expire_le < maintenant) → acquis, l'écran s'ouvre en saisie
        └─ détenue par quelqu'un d'autre → pas d'ouverture, bandeau « En cours de modification par X »
Pendant la saisie : signal de vie toutes les 30 s (expire_le = maintenant + 2 min)
Enregistrer / Annuler / fermer le panneau → Libérer(cible)
Plantage, coupure réseau, PC fermé → plus de signal, le verrou expire à 2 min
Aucune saisie ni clic pendant 15 min → fenêtre « Toujours là ? » (2 min)
   ├─ « Continuer » → on repart pour 15 min
   └─ « Annuler et libérer », ou fin du compte à rebours → saisies annulées, Libérer
Admin : « Forcer la libération » → la ligne est supprimée ; chez le détenteur, ses saisies
   sont annulées et un message s'affiche (ConfirmDialog.Erreur)
```

Le même compte ouvert sur **deux PC** : ce sont deux sessions distinctes (`session_id`). Le verrou est attaché à la session, pas seulement à la personne.

### 6.2 Règles testables (classe statique future `RegleVerrou`, tests NUnit EditMode)

| Fonction | Exemple |
|---|---|
| `PeutAcquerir(verrou, maSession, maintenant)` | Libre → oui. Détenu par ma session → oui (on renouvelle). Détenu par Paul, expire à 14:35, il est 14:34 → non. Il est 14:36 → oui (expiré) |
| `Expire(verrou, maintenant)` | expire_le 14:35:00, maintenant 14:35:01 → oui |
| `EtatInactivite(derniereActivite, maintenant)` | 14 min 59 s → actif. 15 min → avertissement, reste 2:00. 17 min → libérer |
| `PeutAgirEnUnClic(verrou, maSession, maintenant)` | Fiche libre ou à moi → oui. Prise par Marie → non, avec le message « Volteo est en cours de modification par Marie » |
| `CiblesRefacturation(charge)` | Taxe foncière de rivoli refacturée à Volteo et Atelier Nord → [bâtiment rivoli, Volteo, Atelier Nord] |
| `Libelle(verrou)` | « En cours de modification par Marie Dupont (facturation) depuis 14 h 32 » |

### 6.3 Quel écran verrouille quoi

Tous les appelants de `SaveBatiment` et `SaveAfterModifyToDoListLocataire` (relevés le 09/10) :

| Écran / code | Ce qu'il écrit | Verrou |
|---|---|---|
| `BatimentPrefab.Modify` puis `SaveBatiment` (fiche bâtiment) | Champs du bâtiment | **Bâtiment** |
| `InvestissementListPanel` (achats, coûts), `TravauxFormPanel` | Achats et travaux du bâtiment | **Bâtiment** |
| `ChargePanel` (ajout ou modification d'une charge) | `charges` du bâtiment | **Bâtiment** |
| `FactureRefacPanel`, `FactureRegulPanel` | Charges du bâtiment **et** `facturesEtat` des locataires | **Bâtiment + locataires concernés** (refus si l'un est pris) |
| `LocatairePrefab.Modify` puis Save (fiche locataire) | Le locataire | **Locataire** |
| `FactureLoyerPanel`, `FactureDepotPanel` | Le locataire (facture, `factureSeq`) | **Locataire** |
| `RevisionPanel`, `PaliersPanel` (sous-menu Loyer), `LocataireFacturationFields` | Le locataire | **Locataire** |
| `DepartPanel`, `CessionPanel`, `ParcoursDepartUI` (`ApresDepart`) | Le locataire | **Locataire** |
| `BatimentPrefab` : ajouter ou supprimer un locataire | La liste des locataires | **Bâtiment** (création ou suppression d'une ligne `locataires`) |
| `TransfertEntrepriseUI` | Déplacement vers une autre entreprise | Bâtiment ou locataire, puis opération serveur |
| `ReglagePanel` (supprimer une liste de charges, ligne 531) | **Tous les bâtiments** | Admin, refusé si l'un des bâtiments est verrouillé |
| **En un clic** : `LocataireSuiviInline` (payé, impayé, rappel), `VersementsPanel`, `ObjectifPanel`, `LocatairePrefab.updateListObjectif`, `BatimentPrefab.updateListObjectif`, `PhotoGalleryController`, `BailFileUI`, `BatimentPrefab.Archiver` | Le locataire ou le bâtiment | **Aucun verrou durable**, refusé si la fiche est prise (`PeutAgirEnUnClic`) |

Exemple d'action en un clic : Paul marque payé le loyer d'octobre de Volteo (2 000 €) pendant que personne n'a la fiche. C'est accepté, et chez Marie la pastille passe à « Payé » avec le message « Volteo mis à jour par Paul ».

---

## 7. Écritures en arrière-plan à revoir

| Code | Aujourd'hui | Demain |
|---|---|---|
| `BatimentManager.SauvegardeDeFermeture` | Réécrit tout à la fermeture | **Supprimé** : chaque action est déjà enregistrée sur le serveur. `FermetureGuard` continue de prévenir si une saisie est en cours, et libère les verrous |
| `LoadAll` : `Reconstituer`, `EffectuerCession` | Écrivent au chargement | Faits **une seule fois**, à l'import des données (étape 3), ou comme une action en un clic (refusée si la fiche est prise, avec contrôle de version). `Loyers.Actualiser` reste en mémoire seulement (état déduit) |
| `FactureEnvoiAuto` (ligne 346) | Statut « Envoyé » puis enregistrement | **Fait le 15/10 (étape 7)** : chaque facture est réservée juste avant son envoi (`reserver_envoi` : verrou du locataire, statut toujours « en attente », version à jour). Celles déjà envoyées, en cours d'envoi ailleurs ou dont la fiche est prise sont sautées et listées dans le bilan. Il n'envoie que des factures déjà émises (PDF et numéro existants), la réservation suffit donc |
| `PappersSync` | Écrit le résumé d'entreprise de chaque locataire | Action en un clic : si la fiche est prise, il passe et réessaie au prochain démarrage |
| `BackupService` | Copie datée au démarrage | Remplacé par la sauvegarde quotidienne du serveur et par l'export de l'admin |
| `EntrepriseService` | Liste locale (PlayerPrefs) | « Les entreprises dont je suis membre », lues après la connexion |
| `TransfertEntreprise` | Déplacement sur le disque | Opération serveur (changer `entreprise_id`, déplacer les documents) |

---

## 8. Documents

1. **La clé ne change pas** : un document est identifié par son chemin relatif actuel (`DossiersDonnees.VersRelatif`, par exemple `Batiment/rivoli/Volteo/Facture/F-2026-10-012.pdf`). Ce chemin sert aussi de clé dans le stockage cloud.
2. **Création** (facture émise, bail ajouté, photo, PDF de charge, logo) : écriture locale comme aujourd'hui, **puis** envoi au cloud, **puis** une ligne dans `documents`. Le temps réel prévient les autres postes.
3. **Mode « Sur chaque PC »** : à l'événement, et au démarrage pour rattraper ce qui a été manqué, le poste compare `documents` à son dossier (présence et empreinte) et télécharge ce qui manque.
4. **Mode « Drive partagé »** : le poste ne télécharge rien, c'est le Drive qui recopie. Si Marie ouvre depuis l'application un fichier pas encore arrivé, il est lu depuis le cloud vers un fichier temporaire.
5. **Suppression** : `supprime_le` est rempli. Chez les autres, le fichier local est rangé dans la corbeille (`corbeille_photos/`, etc.), comme le fait déjà `PhotoService` chez la personne qui supprime.
6. **Rien n'est surveillé dans l'Explorateur** : un fichier déposé à la main reste local.

Règles testables (classe statique future `SyncDocuments`) :

| Fonction | Exemple |
|---|---|
| `ATelecharger(listeServeur, fichiersLocaux, mode)` | « chaque_pc » : la facture F-012 est sur le serveur mais absente du PC → à télécharger. Mode « drive » → rien |
| `AMettreEnCorbeille(listeServeur, fichiersLocaux)` | Photo supprimée par Paul et encore présente chez Marie → corbeille |
| `EstAJour(local, serveur)` | Même taille, empreinte différente → à retélécharger |

---

## 9. Parcours

1. **Premier lancement** : écran de connexion (maquette 1). Si on n'a pas encore de compte : « Créer mon entreprise » (le créateur devient admin) ou « J'ai reçu une invitation ».
2. **Choix de l'entreprise** : s'il y en a plusieurs, liste des entreprises dont on est membre (remplace l'actuel `EntreprisePanel`).
3. **Dossier des documents** : en mode Drive, CIPL retrouve seul le dossier partagé grâce au repère `CIPL_entreprise.json`, et ne pose une question que s'il ne le trouve pas. En mode « Dans CIPL », il crée le dossier sans rien demander (§13).
4. **Inviter un collègue** (admin) : Réglages › Utilisateurs › « Inviter » → e-mail, rôle. Refusé si les licences sont toutes prises : « 5 licences sur 5 utilisées. Retirez quelqu'un ou augmentez l'abonnement ».
5. **Import des données existantes** (une seule fois, par l'admin) : « Importer les données de ce PC » → envoi des `batiment_*.json` (découpés en bâtiments et locataires), de `reglage.json`, des secrets et de tous les documents, avec une barre de progression. Une sauvegarde locale complète est faite avant.
6. **Perte de connexion** : bandeau rouge (maquette 5), tout passe en lecture seule. Les saisies en cours restent à l'écran. Au retour de la connexion, l'enregistrement est contrôlé par la version (voir « Reprise après une coupure », §3).
7. **Export** (admin) : Réglages › « Exporter toutes les données » → dossier choisi, avec l'arborescence actuelle (`batiments/`, `Batiment/…`, `reglage.json`), **sans les secrets**.

---

## 10. Maquettes

Maquettes 1 à 5 **validées le 09/10** (montrées en session). Celle de Réglages › Documents a été montrée en aperçu dans une question et choisie.

1. **Connexion** : modale à bandeau bleu « CIPL — Connexion », E-mail, Mot de passe, « Rester connecté sur ce poste », bouton « Se connecter », lien « Mot de passe oublié ».
2. **Fiche verrouillée par une autre personne** :
   - en haut à droite de la barre d'onglets : « En ligne : » suivi des initiales en pastilles (MD, PL) ;
   - sur l'onglet du locataire : pastille ambre au prénom (« Volteo [Marie] »). La police n'a pas de cadenas, on écrit le prénom avec `ObjectifsTableau.Pastille` ;
   - sous les onglets : bandeau ambre « En cours de modification par Marie Dupont (facturation) depuis 14 h 32 — lecture seule », et à droite « Forcer la libération » (admin seulement) ;
   - boutons Modifier, Facturation et Loyer grisés, avec « Disponible dès que Marie aura fini ».
3. **Inactivité** : modale centrée, bandeau ambre « Toujours là ? ». Texte : « Vous modifiez Volteo sans activité depuis 15 min. Les autres ne peuvent pas modifier cette fiche. Sans réponse dans 1:45, vos saisies non enregistrées seront annulées et la fiche libérée. » Boutons [Annuler et libérer] [Continuer].
4. **Réglages › Utilisateurs** (admin) : bandeau « Utilisateurs — SCI Rivoli (3 / 5 licences) », bouton « Inviter ». Tableau Nom · E-mail · Rôle (menu `UIDropdown`) · Statut (En ligne / Invitation envoyée / Hors ligne) · « Retirer ».
5. **Hors ligne** : bandeau rouge en haut de tous les écrans, « Hors ligne — lecture seule. Données à jour au 09/10 à 14 h 41 », avec « Réessayer ».
6. **Réglages › Documents** (refait le 12/10) : bloc « Rangement des documents » (choix de l'admin, en lecture pour les autres), puis bloc « Sur cet ordinateur » (état trouvé ou introuvable, chemin, Rechercher à nouveau, Choisir à la main…). Voir §13 et la page 5 du `.drawio`.

À vérifier pour chaque écran au zoom 175 % (largeurs minimales, ouverture des menus). Les modales sont construites avec `DepartPanel.Entete`.

---

## 11. Découpage en étapes

Chaque étape doit pouvoir être livrée et testée seule. Les règles métier vont dans des classes statiques avec des tests NUnit EditMode.

| # | Étape | Contenu | Règles testées | Fichiers touchés (principaux) |
|---|---|---|---|---|
| 1 | **Essai technique** — **terminé le 12/10** | Supabase (Paris) : connexion, verrou (refus en 114 ms, reprise après 2 min), temps réel (changements reçus en 86 à 555 ms), contrôle de version, lien cipl:// dans un build. Détail : REVUE (session du 12/10), parcours U | `RegleVerrou`, `LienCipl`, lecture des réponses (`CloudTests`) | `Cloud/` : `CloudClient`, `TempsReel`, `LienCipl`, `LienCiplRecepteur`, `RegleVerrou` ; `Serveur/essai_supabase.sql` ; fenêtre CIPL › Essai cloud |
| 2 | Connexion et lecture seule — **fait le 12/10** (avec l'étape 3) | Connexion (jeton gardé chiffré DPAPI), choix de l'entreprise, bâtiments lus sur le serveur, lecture seule, menu Compte. Pas encore de cache hors ligne (étape 8) | `DonneesCloud.Decouper/Recomposer`, `ImportLocal.Statut`, `Dpapi` (`CloudDonneesTests`) | `Cloud/` : `SessionCloud`, `DonneesCloud`, `EcransCloud`, `DemarrageCloud`, `Dpapi` ; `BatimentManager` (Start, `Vider`, `Installer`, lecture seule) ; `Serveur/etape2_supabase.sql` |
| 3 | Import des données — **fait le 12/10** (bâtiments et locataires ; les documents restent sur le PC jusqu'à l'étape 6) | Proposé si le cloud est vide ou si un import est à reprendre, même nom, repère `CIPL_entreprise.json` (refus d'un 2e import) | `ImportLocal.Statut` | `Cloud/ImportLocal` |
| 4 | Écriture et verrous — **terminée le 13/10**. **4a** (parcours W) : enregistrement des fiches changées avec contrôle de version, verrou au clic sur « Modifier », bandeau chez les autres, nom saisi. **4b** (parcours X) : verrous de Facturation, Loyer, Dépôt, Départ, charges, achats et travaux ; régularisation et refacturation réservent bâtiment + locataire ; « Toujours là ? » ; « Forcer la libération » ; une annulation relit toute l'entreprise | Enregistrement par fiche (bâtiment OU locataire) avec version, verrous et bandeau, actions en un clic, suppression de `SauvegardeDeFermeture`, inactivité | `RegleVerrou`, `EcritureCloud.Differences/Lire/MotifRefus` (`CloudEcritureTests`) | `Cloud/EcritureCloud`, `Serveur/etape4_supabase.sql`, `BatimentManager` (`SaveBatiment`), boutons « Modifier » de `BatimentPrefab` et `LocatairePrefab` ; en 4b : les écrans du §6.3, `FermetureGuard` |
| 5 | Temps réel — **terminé le 14/10** (parcours Y) : fiche du bâtiment relue et reconstruite (attend si j'y travaille), message pour le bâtiment affiché ou l'accueil, « En ligne » dans la barre d'onglets, prénom sur l'onglet du bâtiment et sur la ligne du locataire (les onglets des locataires sont masqués) | Rafraîchissement des fiches, message « mis à jour par », pastilles « En ligne » | `DirectCloud.Concerne`, `RegleVerrou.EnLigne/Prenom/Initiales`, `EcritureCloud.Adopter` | `Cloud/DirectCloud`, `TempsReel`, `Serveur/etape5_supabase.sql`, `BatimentManager` (`Remplacer`, `Retirer`), `MenuManager`, `MenuItem`, `BatimentPrefab` |
| 6 | Documents — **6a faite le 14/10** (parcours Z) : synchronisation de `Batiment/` et `Logo/` (décision du 14/10 : tout ce qui y est rangé est partagé), contenu stocké sous son empreinte, corbeille, dossier `Documents\CIPL\<entreprise>` sur un nouveau PC. **6b faite le 15/10** (parcours AA) : mode choisi par l'admin sur le serveur, Réglages › Documents, recherche du repère dans OneDrive / Google Drive (tous les Drives ensemble, niveau par niveau), fenêtre « Documents de … » (« Continuer sans le dossier » demande où le créer, décision du 14/10), Réglages dans le Drive, lecture depuis le cloud d'un document pas encore recopié, et « Changer de dossier… » en mode « Dans CIPL » (décision du 15/10) | Envoi au cloud, deux modes de copie locale, corbeille, Réglages › Documents | `SyncDocuments` | `DossiersDonnees`, `PhotoService`, `BailFileUI`, émission des factures, `ChargePanel`, `ReglagePanel` |
| 7 | Factures — **faite le 16/10** (parcours AB) : fenêtre « N factures sont à envoyer » chez chacun, à l'ouverture de l'entreprise (décision du 15/10) ; réservation par `reserver_envoi` juste avant chaque envoi ; factures prises ailleurs sautées, et dites dans le bilan (décision du 15/10) ; une facture sautée parce que sa fiche était en cours de modification part seule dès que la fiche est libre (décision du 16/10) ; « Plus tard » propre à chaque personne ; secrets (SMTP, Pennylane) rangés sur le PC, hors du dossier des documents (décision du 15/10) | Réservation de l'envoi automatique | `MotifSaut`, `Bilan`, `Retrouver` | `FactureEnvoiAuto`, `EcritureCloud`, `DemarrageCloud` |
| 8 | Hors ligne — **8a faite le 16/10** (parcours AC) : coupure pendant une session, bandeau rouge sous les onglets (décision du 16/10), lecture seule, saisies commencées avant la coupure renvoyées au retour et contrôlées par la version, retour tout seul toutes les 30 s ou sur « Réessayer » (décision du 16/10). **8b faite le 19/10** (parcours AD) : démarrage sans connexion sur la dernière copie (`persistentDataPath`, entreprise ouverte en dernier), effacée à la déconnexion ; au retour, tout est relu | Bandeau, lecture seule, reprise contrôlée par la version | `HorsLigne.Panne`, `HorsLigne.Texte` | `CloudClient`, `EcritureCloud`, `DirectCloud`, `ConfirmDialog` |
| 9 | Admin — découpée le 19/10 en 9a (Réglages et secrets partagés : **faite le 19/10**, parcours AE), 9b (utilisateurs, invitations par code envoyé par e-mail, droits : **faite le 19/10**, parcours AF), 9c1 (onglets de Réglages, abonnement sans paiement, lecture seule à l'expiration, « Ajouter une entreprise » : **faite le 19/10**, parcours AG), 9c2 (Stripe : paiement par carte ou SEPA, portail client, licences au prorata, TVA 20 % en plus des prix HT, résiliation depuis CIPL : **faite le 22/10, validée le 23/10**, parcours AH). Pappers sur le serveur et l'export deviennent les étapes 10 et 11 (§14, découpage du 23/10) | Utilisateurs, invitations, licences, libération forcée, export | Comptage des licences ; `ReglagesCloud.PeutEnvoyer` | `ReglagePanel` (nouvel onglet), `BackupService` (devient l'export), `ReglageService`, `ReglagesCloud` |

---

## 12. Risques et questions ouvertes

- **Coût et choix du fournisseur** : comparer Supabase (UE, Postgres) avec les autres services à l'étape 1. Hébergement **en UE obligatoire** (RGPD : données de locataires, RIB).
- **Bibliothèque C#** : un client C# non officiel existe pour Supabase, mais sa compatibilité avec Unity 2023.2 n'est pas vérifiée. À défaut, REST par `UnityWebRequest` et WebSocket (`System.Net.WebSockets`) écrits à la main.
- **Secrets sur les postes** : la clé Pennylane et le mot de passe SMTP arrivent sur le poste de chaque membre (cachés dans l'interface, mais présents en mémoire). Si un client exige mieux, on pourra plus tard déplacer l'envoi sur le serveur. Pour rappel : un abonnement et un jeton Pennylane par société (SCI).
- **Volume du stockage** : les photos sont les plus lourdes. Prévoir une limite par entreprise dans l'offre.
- **Délai de recopie du Drive** : couvert par la lecture depuis le cloud (§8.4).
- **Tests** : le serveur n'est pas testable en EditMode. Les règles (`RegleVerrou`, `SyncDocuments`, découpage JSON) le sont. Les vérifications à plusieurs se feront avec deux instances de l'application (un build et l'éditeur).
- **Liens cipl://** : certains antivirus ou politiques d'entreprise bloquent les protocoles personnalisés. La page relais affiche alors « Ouvrir CIPL » et « Télécharger CIPL », et on peut toujours revenir à la page de connexion.

---

## 13. Comptes, abonnement et paramètres (décisions du 11/10)

Les flows complets (technique et écrans) sont dans **`FLOW_MULTIUTILISATEUR.drawio`**, en 7 pages. Ils suivent le style du diagramme IfreightOS de l'utilisatrice :
- page 1 : technique, connexion et compte ;
- page 2 : technique, paramètres et abonnement ;
- page 3 : technique, travail à plusieurs ;
- page 4 : écrans, connexion et compte ;
- page 5 : écrans, paramètres et abonnement ;
- page 6 : écrans, travail à plusieurs ;
- page 7 : intégration dans le code CIPL (ordre du démarrage, nouvelles classes, classes modifiées, serveur et Stripe).

| Sujet | Décision |
|---|---|
| Inscription | **Libre-service** : « Créer mon compte » → **« Vous gérez des biens en tant que : Société / Particulier »** (corrigé le 12/10). **Société** : SIRET vérifié sur le serveur (Pappers, la clé ne quitte pas le serveur) → raison sociale et adresse pré-remplies. Refus si le SIRET est introuvable, si la société est radiée, ou si l'entreprise est déjà sur CIPL (« demandez une invitation à son administrateur »). **Particulier** : nom, prénom et adresse (entête des factures), sans SIRET ni Pappers. Dans les deux cas : compte de l'admin, puis e-mail de confirmation |
| Vocabulaire | On garde « **entreprise** » partout dans l'application. Celle d'un particulier s'appelle par défaut « Patrimoine de Jean Martin » (modifiable). Un particulier peut ajouter ses SCI dans le même groupe, avec la remise |
| Essai | **14 jours, sans carte**, jusqu'à 3 utilisateurs, tout inclus. Bandeau « Essai gratuit : N jours restants », e-mails à J-7 et J-1, puis lecture seule tant qu'aucune offre n'est choisie |
| Collaborateurs | **Invitation seulement** (Réglages › Utilisateurs › Inviter : e-mail, rôle, entreprises accessibles). Statut « Invitation envoyée », lien valable 7 jours, licence réservée dès l'invitation. Renvoyer ou annuler (ce qui libère la licence) |
| Liens des e-mails | *Abandonné : codes à 6 chiffres partout (étapes 9b, 12, 13), liens `cipl://` retirés le 26/10 (étape 15b).* **Lien qui ouvre CIPL** : `https://cipl.app/o/…` → page relais statique → `cipl://type?jeton=…`. Protocole enregistré dans `HKCU\Software\Classes\cipl` au premier lancement. Si CIPL est déjà ouvert, le lien est transmis à la fenêtre ouverte. La page relais est nécessaire parce que Gmail et Outlook web ne rendent pas cliquables les liens `cipl://` |
| Paiement | **Stripe**, dans l'application : « Choisissez votre abonnement », puis « Information bancaire » (page Stripe : carte ou prélèvement SEPA). Les webhooks Stripe mettent le statut à jour. « Paiement et factures » ouvre le portail client Stripe. CIPL ne voit jamais les numéros de carte |
| Offre | **Une seule offre**, tout inclus, mensuelle ou annuelle (2 mois offerts, à confirmer). Prix = 1re entreprise X € + chaque entreprise en plus X € **-30 %** (pourcentage fixe) + licences × Y € (prix à fixer) |
| Groupe | Un **groupe** = un titulaire, un moyen de paiement, **une facture** pour toutes ses entreprises. On ajoute une entreprise par Réglages › Abonnement › « Ajouter une entreprise » (SIRET), et la remise s'applique seule |
| Licences dans le groupe | ~~**Une personne = une licence** pour tout le groupe~~. **Remplacé le 23/10 : licences par entreprise.** Chaque entreprise a son nombre de licences (exemple : Démo CIPL 3, SCI Rivoli 1) ; une personne occupe une licence dans chacune de ses entreprises ; une entreprise pleine ne peut pas être cochée. Rôle et facture restent ceux du groupe (une ligne « licence » = le total). 3 licences par entreprise pendant l'essai ; une entreprise ajoutée à un groupe abonné commence à 1. Parcours AJ |
| Abonnement expiré | **Lecture seule + export** pour tous. L'admin voit « Choisissez votre abonnement », les autres « voyez avec Marie Dupont (admin) ». Paiement refusé : bandeau orange pour l'admin, Stripe relance 3 fois, puis expiration. Résiliation : effective à la date d'échéance |
| Réglages | **Onglets** : Entreprise, Facturation (les sections actuelles), Utilisateurs, Abonnement, Documents, Intégrations (Pennylane, serveur mail, Pappers, chacun avec un bouton « Tester » qui dit quel identifiant ne fonctionne pas). Un utilisateur ne voit que Entreprise (lecture), Facturation et Documents (dossier sur ce PC) |
| Menu compte | Initiales en haut à droite : Mon profil, Changer d'entreprise (avec `FermetureGuard` si une saisie est en cours), Se déconnecter (libère les verrous et efface le jeton du PC). Le bouton « Entreprises » de l'accueil disparaît |
| Droits sur Facturation | Tous modifient les entêtes et les textes fixes, et peuvent ajouter une liste de charges. **Admin seulement** : RIB, logo, suppression d'une liste de charges (elle touche tous les bâtiments) |
| Dernier admin | On ne peut ni le retirer ni le rétrograder : « Il faut au moins un administrateur » |
| Dossier des documents (12/10) | **Drive partagé** : quand l'admin choisit le dossier partagé, CIPL y dépose un repère `CIPL_entreprise.json` (identifiant et nom de l'entreprise). Sur chaque autre ordinateur, CIPL cherche ce repère dans les Drive synchronisés (variables `OneDrive` et `OneDriveCommercial`, comptes OneDrive du registre, lecteur Google Drive). S'il le trouve : aucune question, juste le message « Documents de SCI Rivoli trouvés dans votre OneDrive ». Sinon : écran « Documents de SCI Rivoli » avec l'aide « ouvrez ce Drive dans votre navigateur et cliquez sur Synchroniser », et les boutons Rechercher à nouveau, Choisir le dossier… (refusé si le repère n'est pas celui de l'entreprise) et Continuer sans le dossier (documents lus depuis le cloud). **Dans CIPL** : `Documents\CIPL\SCI Rivoli` est créé sans question |
| Réglages › Documents (12/10) | Deux blocs. « **Rangement des documents** » : choix de l'admin (Drive partagé ou Dans CIPL) et « Changer le dossier partagé… », en lecture seule pour les autres. « **Sur cet ordinateur** » : l'état (trouvé automatiquement ou introuvable), le chemin, la phrase « Chaque ordinateur range le Drive à sa façon : CIPL retrouve ce dossier tout seul », et les boutons Rechercher à nouveau et Choisir à la main… |
| Import en mode Drive | Les documents déjà dans le Drive restent en place : ils sont seulement envoyés au cloud comme copie de référence |

**Modèle serveur, compléments au §5** :
- `groupes` (titulaire, client et abonnement Stripe, offre, licences, statut, fin d'essai, échéance) ;
- `entreprises.groupe_id` ;
- `membres` (personne × groupe = une licence, rôle) ;
- `acces` (membre × entreprise).

**Règles testables à ajouter** :
- `RegleAbonnement` : état, droit d'écrire, jours d'essai restants, total avec la remise, licences libres. Exemple : 3 SCI, 5 licences, X = 40, Y = 15 → 40 + 2 × 28 + 75 = 171 € par mois ;
- `RegleDroits` : onglets, RIB, logo, suppression d'une liste de charges, dernier admin ;
- `RegleMotDePasse` : 8 caractères minimum, deux saisies identiques ;
- `LienCipl` : analyse de `cipl://type?jeton=…` ;
- `DossierEntreprise` : liste des racines Drive, recherche du repère, validation d'un dossier choisi à la main. Exemple : chez Paul, le repère de SCI Rivoli est dans `C:\Users\paul\OneDrive - SCI Rivoli\CIPL` → trouvé. Un dossier dont le repère porte un autre identifiant → « Ce dossier n'est pas celui de SCI Rivoli ».

**Découpage (§11)** : l'étape 2 comprend désormais l'inscription, les liens `cipl://` et le menu compte. L'étape 9 comprend l'abonnement Stripe, le groupe, la remise et les onglets de Réglages. L'étape 1 (essai technique) doit aussi vérifier l'enregistrement du protocole `cipl://` et la transmission d'un lien à une fenêtre CIPL déjà ouverte.

---

## 14. Avant la vente : suite du découpage (proposée le 23/10, à valider)

Les étapes 1 à 9c2 sont faites et validées. Restent les deux étapes prévues (Pappers sur le serveur, export), puis ce qu'il faut pour vendre : aujourd'hui, un nouveau client ne peut entrer que par une invitation ou par l'import depuis un PC déjà installé (l'inscription prévue au §13 n'a pas été faite à l'étape 2), personne ne peut retrouver un mot de passe oublié, et l'application ne s'installe ni ne se met à jour seule.

Ordre proposé : chaque étape ne dépend que des précédentes. Taille : S (une séance), M (deux ou trois), L (davantage).

| N° | Étape | Taille | Dépend de | Pourquoi à cette place |
|---|---|---|---|---|
| 10 | Pappers sur le serveur | S | 9c2 (fonctions serveur) | L'inscription d'une société en a besoin |
| 11 | Export de toutes les données (ex-9d) | M | — | Promis aux abonnés expirés (§13) ; remplace la sauvegarde du démarrage |
| 12 | Mot de passe oublié | S | — | Manque à tous les utilisateurs actuels |
| 13 | Inscription libre-service | M | 10, 12 | Sans elle, aucun nouveau client ne peut commencer seul |
| 13b | Premiers pas (tuto) | M | 13 | Ajoutée le 24/10 : un nouveau client arrive dans une application vide, sans savoir par où commencer |
| 14 | E-mails de l'abonnement | M | 13 | Fin d'essai J-7 et J-1 (§13), paiement refusé, résiliation |
| 15 | Installation et mises à jour | L | — | Un invité doit pouvoir installer CIPL lui-même |
| 15b | Version Mac | M | 15 | Ajoutée le 26/10 : des clients sur Mac ; PDF, connexion gardée, `cipl://` et dossiers à adapter (voir 15b) |
| 16 | Boutons « Tester » des intégrations | S | — | Repoussés en 9c1 ; utiles au support |
| 16a | Envoi des factures depuis l'adresse du client (« Se connecter avec Google / Microsoft ») | M | 9a (secrets) | Ajoutée le 25/10 : le réglage SMTP (serveur, mot de passe d'application) bloquait les premiers pas ; un nouveau client doit pouvoir facturer depuis sa messagerie sans ce réglage |
| 16b | API payantes : Google Maps et Pappers | M | 9c2 (fonctions serveur) | Ajoutée le 24/10 : cartes Google, surveillance et fiche complète des locataires, clés de CIPL sur le serveur |
| 18 | Plateforme agréée de facturation électronique | M | — | Obligations de 2027 (rapport du 22/10). **Avancée avant 16c et 17 le 24/10** : il faut savoir comment chaque société de chaque client est ouverte sur la plateforme avant de vendre |
| 16c | Revue complète du code (Fable) | M | toutes sauf 17 | Ajoutée le 24/10 : relire tout le code, plateforme comprise, avant d'ouvrir à de vrais clients |
| 17 | Passage en réel | M | toutes | Réglages de production, juridique, RGPD |

### 10. Pappers sur le serveur — **faite et validée le 23/10** (parcours AI)
- **Révisé le 23/10** : CIPL n'utilise pas l'API payante de Pappers mais l'annuaire public de l'État (sans clé). Décisions : vérification depuis CIPL, sans fonction serveur ; tournée des résumés d'entreprise une fois par jour, sur le premier poste qui ouvre l'entreprise (`prendre_tournee_annuaire`), fiches en cours de modification sautées ; « Ajouter une entreprise » pré-remplit le nom et affiche la forme et le siège, refuse une société fermée ou introuvable.
- **Contenu prévu au départ** : une fonction serveur `pappers` (clé `PAPPERS_API_KEY` dans les Secrets de Supabase, jamais sur les PC) : SIRET → raison sociale, adresse, forme, radiée ou non. « Ajouter une entreprise » pré-remplit la fenêtre et refuse une société radiée. La synchronisation Pappers des fiches (`PappersSync`), suspendue sur le cloud, passe par cette fonction.
- **Testable** : lecture de la réponse Pappers ; refus (radiée, introuvable) ; comparaison du corps envoyé avec la fonction (même test qu'en 9c2).
- **À décider** : la synchronisation des fiches se lance-t-elle seule (à l'ouverture, une fois par jour) ou à la demande de l'admin ? Qui paie les appels Pappers (inclus dans l'abonnement) ?

### 11. Export de toutes les données — **faite et validée le 23/10** (parcours AK, rangement AL)
- **Décisions du 23/10** : l'admin exporte toujours ; les autres seulement quand l'abonnement a expiré. L'export porte sur l'entreprise ouverte. C'est un dossier (pas un ZIP) rangé comme une entreprise CIPL sur le PC : `batiments/`, `reglage.json`, `Batiment/`, `Logo/`. Les documents absents de ce PC sont téléchargés depuis le cloud. L'import d'un export n'est pas dans l'étape. Hors ligne, pas d'export : il relit les fiches sur le cloud. `BackupService` ne tournait déjà plus sur le cloud depuis l'étape 2.
- **Fait** : `RegleExport` (testée), `ExportCloud`, `SectionExport` (Réglages › Documents).
- **Rangement des documents (retour du 23/10)** : dans chaque bâtiment, `_Infos du bâtiment/` (Factures de charges, Objectifs, Photos) à côté des dossiers des locataires ; chez le locataire, `Factures/` en six sous-dossiers (Avoirs, Dépôt de garantie, Loyers, Quittances, Refacturations, Régularisations) ; les dossiers à identifiant dans `Anciens documents/` ; les aperçus ne sont plus des documents. Chaque PC range ses fichiers à l'ouverture (`DocumentsCloud.RangerIci`) ; la table `documents` est corrigée au premier passage (`SyncDocuments.Redresser`) ; les chemins enregistrés dans les fiches ne sont pas réécrits, `DossiersDonnees.VersAbsolu` les mène au nouvel emplacement. L'export met chaque fiche dans son dossier. Le chemin d'exemple du §8 devient `Batiment/rivoli/Volteo/Factures/Loyers/…`.
- **Synchronisation, règle revue le 23/10** : « jamais synchronisé ici, le cloud fait foi » ne vaut plus qu'au premier passage d'un dossier (import, dossier neuf). Après, un fichier remis à un chemin supprimé au cloud repart (renommage revenu à un ancien nom : rivoli → rivoli2 → rivoli mettait les documents en corbeille chez tout le monde). Un renommage demande un passage dans 2 s.
- **Contenu prévu au départ** : « Exporter toutes les données » : bâtiments, locataires, Réglages (sans les secrets) en JSON, et tous les documents, dans un dossier choisi sur le PC. Possible en lecture seule (abonnement expiré, hors ligne sur la copie). `BackupService` disparaît du démarrage.
- **Testable** : le contenu et l'arborescence de l'export ; l'export puis l'import local redonnent les mêmes fiches.
- **À décider** : admin seulement (décision « Sauvegardes » du 09/10) ou tout le monde quand l'abonnement a expiré (§13) ? Dossier ou fichier ZIP unique ? L'import d'un export (pour changer de compte, ou revenir hors cloud) fait-il partie de l'étape ?

### 12. Mot de passe oublié — **faite et validée le 23/10** (parcours AN)
- **Décisions du 23/10** : maquette validée telle quelle (lien sur l'écran de connexion ; l'adresse, puis le code et le nouveau mot de passe dans la même fenêtre) ; connecté(e) directement une fois changé ; la même fenêtre dans le menu Compte, « Changer mon mot de passe ». Modèle « Reset Password » de Supabase en français avec `{{ .Token }}`. Fait : `RegleMotDePasse` (testée), `SessionCloud.DemanderCode` / `ChangerMotDePasse`, `EcransCloud.AfficherMotDePasse`.
- **Contenu prévu** : sur l'écran de connexion, « Mot de passe oublié » : l'adresse → un **code à 6 chiffres** par e-mail (code de récupération de Supabase, sans lien à ouvrir, comme les invitations), puis le code et le nouveau mot de passe dans CIPL.
- **Testable** : la vérification du formulaire (code, mot de passe ×2), sur le modèle de `VerifierRejoindre`.
- **À décider** : les e-mails de Supabase partent par défaut d'un service limité à quelques envois par heure ; pour la vente, un serveur d'envoi dédié (étape 17). Le modèle d'e-mail de Supabase est à personnaliser en français.

### 13. Inscription libre-service — **faite le 23/10, validée le 25/10** (parcours AO)
- **Décisions du 23/10** : maquette validée telle quelle ; adresse prouvée par un code à 6 chiffres (modèles « Magic Link » et « Confirm signup » de Supabase, `{{ .Token }}`), « Confirm email » restant décoché ; **un seul groupe par adresse e-mail** (une adresse déjà membre d'un groupe, même invitée, n'en crée pas d'autre ; une autre adresse le peut) ; Réglages de départ tirés de ce qui est saisi (bas de page, lieu d'émission, expéditeur) à la place de ceux de CIPL. Fait : `Serveur/etape13_supabase.sql` (`inscrire`), `RegleInscription` (testée), `SessionCloud.Inscrire`, `EcransCloud.AfficherInscription`.
- **Contenu prévu** (§13) : « Créer mon compte » sur l'écran de connexion → Société (SIRET vérifié par l'étape 10, refus si radiée ou déjà sur CIPL : « demandez une invitation à son administrateur ») ou Particulier (nom, prénom, adresse ; « Patrimoine de … ») → compte admin, groupe en essai de 14 jours, première entreprise vide. Premier lancement : Se connecter / Créer mon compte / J'ai reçu une invitation.
- **Testable** : la vérification du formulaire ; le nom par défaut d'un particulier ; les refus.
- **À décider** : comment prouver l'adresse e-mail ? « Confirm email » est décoché depuis 9b (le code d'invitation prouve la boîte). Proposition : un code à 6 chiffres envoyé à l'inscription, comme pour le mot de passe oublié, plutôt qu'un lien. Une même personne peut-elle créer plusieurs groupes ?

### 13b. Premiers pas (tuto) — ajoutée le 24/10, **faite et validée le 25/10** (parcours AP)
- **Décisions du 25/10** : maquettes validées telles quelles. La carte pour **tout le monde** : l'admin a les 8 lignes ; un collègue, les 3 qu'il peut faire (bâtiment, locataire, facture). « Masquer » vaut **pour la personne** (retenu dans son compte Supabase, `user_metadata`, donc sur tous ses PC). La visite est montrée **une fois à chaque nouvelle personne** (même mémoire), relançable par Compte › « Revoir la visite guidée ». Une carte dont tout est fait disparaît d'elle-même.
- **Fait** : `PremiersPas` (règle testée, carte sous l'en-tête de l'accueil, « Y aller »), `VisiteGuidee` (6 bulles : À traiter, Vos bâtiments, Créances, Les onglets, Réglages, Compte ; zone absente sautée), `SessionCloud.Retenu` / `Retenir`. Pas de SQL.
- **Retour du 25/10, 13b-2 et 13b-3** : « le tuto doit être plus explicatif, interactif, et vos premiers pas plus accompagnés ». Décisions (maquettes validées) : la visite (5 bulles, « Précédent » et « Suivant ») finit par « C'est parti », qui lance le guide de la première étape ; la carte met la prochaine étape en avant et dit à quoi sert chaque ligne et combien de temps elle prend ; chaque ligne a un guide pas à pas (la zone éclairée reste utilisable, la bulle avance quand l'étape est faite), puis « Vos RIB sont prêts » propose la suivante ; un bâtiment d'exemple (fictif, à supprimer ensuite, rien envoyé ni déposé chez Pennylane, hors du décompte).
  - **13b-2 — faite et validée le 25/10** (parcours AP) : `Bulles` (les bulles, les repères par texte), la visite et la carte revues, les guides des lignes réglées dans Réglages (logo, RIB, envoi, inviter, abonnement). Retours du 25/10 pendant le test : une bulle pour toute la fenêtre du RIB et la saisie contrôlée (`RegleRib`) ; la bulle qui attend dit ce qui manque ; **une entreprise créée par « Créer mon compte » démarre en envoi par e-mail**, serveur d'envoi vide (décision du 25/10).
  - **13b-3 — guides faits et validés le 25/10 (parcours AP6b à AP6d) ; bâtiment d'exemple fait et validé le 25/10 (parcours AQ).** L'étape 13b est terminée. Le guide du locataire suit les quatre étapes du parcours de la fiche (général, bail, loyer, dépôt), sans lesquelles la facturation est refusée. Contenu prévu : les guides bâtiment (« + », nom, adresse, « Sauvegarder »), locataire (« + Ajouter », nom ou SIRET, e-mail, dates du bail, loyer) et facture (aperçu, « Émettre ») ; le bâtiment d'exemple (« Essayer avec un exemple » sur la carte, bandeau « Supprimer l'exemple »).
- **Parcours d'un nouveau client** : recevoir le lien → installer (15) → « Créer mon compte » (13) → application vide → régler l'entreprise → saisir bâtiments et locataires → première facture → inviter → choisir l'abonnement avant la fin de l'essai (bandeau des 7 derniers jours, e-mails de l'étape 14).
- **Décisions du 24/10** :
  - **Les deux formes de tuto** : une carte « Premiers pas » sur l'accueil, et une courte visite guidée des écrans principaux au premier lancement.
  - **Pas d'import** depuis un tableur pour l'instant : saisie à la main, guidée par la liste (import si des clients le demandent).
  - **Téléchargement** : un lien pour commencer (envoyé à la demande et dans l'e-mail d'invitation) ; le site web plus tard (voir 15).
- **Contenu proposé** (à valider avec les maquettes) :
  - Carte « Premiers pas » : 1. votre entreprise (logo, bas de page, lieu d'émission), 2. vos RIB, 3. l'envoi des factures (e-mail ou Pennylane, avec le « Tester » de l'étape 16), 4. un premier bâtiment, 5. un locataire (bail, loyer), 6. une première facture (aperçu), 7. inviter un collègue, 8. choisir l'abonnement. Chaque ligne se coche seule d'après les données, « Y aller » ouvre le bon écran ; masquable ; pour l'admin (un utilisateur invité voit une liste réduite ?).
  - Visite guidée : quelques bulles au premier lancement (accueil, onglets des bâtiments, fiche, suivi de facturation, Réglages, Compte), « Passer » à tout moment, relançable depuis le menu Compte.
- **Testable** : quelles étapes sont faites d'après les données (règle pure) ; l'ordre et le masquage ; la visite montrée une seule fois par personne.
- **À décider** : le contenu exact de la liste et de la visite (maquettes) ; ce que voit un utilisateur invité ; « Masquer » vaut-il pour ce PC ou pour la personne ?

### 14. E-mails de l'abonnement — **faite le 25/10, validée les 26 et 27/10** (parcours AR ; AR7 : la tâche du matin a tourné seule, sans doublon)
- **Décisions du 25/10** : les e-mails partent du **Gmail de CIPL** pour l'instant (un service d'envoi, type Brevo, avant l'étape 17) ; **aux admins du groupe seulement** ; **Stripe** envoie lui-même les reçus, le paiement refusé et la carte qui expire (à activer dans ses réglages ; client Stripe créé en français) ; CIPL en envoie 5, maquettes validées telles quelles : l'essai se termine dans N jours (dès J-7), demain (J-1), l'essai est terminé (le lendemain), la résiliation est enregistrée, l'abonnement est terminé.
- **Fait** : `Serveur/etape14_supabase.sql` (table `envois_abonnement` : jamais deux fois le même e-mail pour la même date ; `emails_abonnement_du_jour(p_jour)`, la règle « quel e-mail pour quel groupe, quel jour », en SQL plutôt qu'en C# puisque c'est le serveur qui envoie, essayable à une autre date ; tâche `pg_cron` chaque matin à 8 h UTC, avec une clé rangée dans le coffre Vault ; fonctions réservées au serveur) ; fonction serveur `emails-abonnement` (SMTP Gmail port 465, secrets `CIPL_GMAIL` et `CIPL_GMAIL_MOT_DE_PASSE`) ; `stripe-webhook` la prévient d'une résiliation (`cancel_at_period_end`) ou d'une fin (`canceled`, date réelle de fin). Tests : droits des fonctions, paramètres des appels, un texte pour chaque e-mail.
- **Limites** : un groupe à plusieurs admins dont un envoi échoue au milieu renvoie l'e-mail aux premiers au passage suivant ; un e-mail de résiliation ou de fin qui échoue n'est que journalisé (pas renvoyé) ; en mode test, Stripe n'écrit qu'aux adresses de l'équipe Stripe.
- **Contenu prévu** : fin d'essai à J-7 et J-1 (§13), puis expiration ; paiement refusé et reçus de paiement (Stripe sait les envoyer lui-même : à activer dans ses réglages) ; confirmation de résiliation. Une tâche planifiée sur le serveur (une fois par jour) choisit les groupes concernés.
- **Testable** : quel e-mail pour quel groupe, quel jour (règle pure, comme `RegleAbonnement`).
- **À décider** : par quel service partent les e-mails de CIPL lui-même (pas le SMTP des clients) ? À qui : l'admin seulement, ou tous les membres pour l'expiration ?

### 15. Installation et mises à jour — **faite et validée le 26/10** (parcours AS1 à AS10 ; AS11, la publication, le jour de la sortie)
- **Décisions du 26/10** : l'installeur sur **GitHub, dans un dépôt public à part** (`agathe-limouzy/CIPL-installation`, sans le code ; gratuit, sans limite de téléchargements, lien fixe vers la dernière version), **privé jusqu'à la sortie** et passé public ce jour-là (2e décision du 26/10 ; avant, ses liens ne marchent pour personne : les essais lisent un dossier du PC indiqué par `cipl_mises_a_jour.txt`, dans le dossier caché de CIPL, `MiseAJour.FichierEssai`) ; CIPL **s'installe lui-même** (« Mettre à jour » : téléchargement, fermeture, installation sans question, réouverture) ; mise à jour **obligatoire seulement si le serveur change** (une version minimale ; sinon « Plus tard ») ; **identité interne fixée** à `CIPL` / `CIPL` (Player Settings : société et produit ; dossier `AppData\LocalLow\CIPL\CIPL` et réglages `HKCU\Software\CIPL\CIPL`), à ne plus jamais changer : le nom commercial n'est pas encore choisi, il changera seulement le nom visible ; maquettes validées telles quelles (bandeau, téléchargement, fenêtre obligatoire, version dans le menu Compte, lien dans l'e-mail d'invitation) ; le Mac en étape 15b.
- **Fait** : `RegleVersion` (testée : comparaison, proposée ou obligatoire, `version.json`) ; `MiseAJour` (lecture de `version.json` au lancement d'une version installée, jamais dans l'éditeur ; bandeau sous les onglets ; fenêtre bloquante `EcransCloud.MiseAJourObligatoire`, devant tout ; l'installeur est lancé sur `Application.quitting`, donc après la garde de fermeture) ; menu Compte « CIPL version … » ; e-mail d'invitation avec le lien (et le Mac quand `version.json` en donne un) ; `Installeur/CIPL.iss` (Inno Setup 6 : sans droits d'administrateur, `%LOCALAPPDATA%\Programs\CIPL`, raccourcis, `cipl://`, désinstalleur, réouverture après une mise à jour) ; menu Unity **CIPL › Construire l'installeur** (`Assets/Editor/ConstruireInstalleur.cs` : build, `version.json`, installeur dans `Build/Installation`, hors du dépôt). Version 1.0.0. Sur le PC de développement, le dossier caché et les réglages ont été recopiés une fois vers la nouvelle identité (l'ancien dossier `DefaultCompany\CIPL_Test` reste, comme sauvegarde).
- **Version minimale** : `ConstruireInstalleur.VersionMinimale`, à monter quand un nouveau SQL rend les anciennes versions incompatibles (elle part dans `version.json`).
- **Quand le nom commercial sera choisi** : rechercher-remplacer « CIPL » dans les textes (≈ 70 dans le code, plus les e-mails du serveur, les modèles Supabase et les produits Stripe), `#define Nom` de `CIPL.iss` ; le titre de la fenêtre suit le produit (`CIPL`) : l'écrire depuis CIPL (`SetWindowText`) plutôt que de changer le produit.
- **Retour du 26/10 (AS4) : la signature devient obligatoire avant la vente.** Le **Contrôle intelligent des applications** de Windows 11 (activé sur le PC de développement, et sur beaucoup de PC neufs) bloque l'installeur non signé, sans recours pour l'utilisateur (pas de « Exécuter quand même ») : la 1.0.0 était passée, la mise à jour 1.0.1 a été bloquée (journal Code Integrity, événements 3033/3077). Ajout : un bilan au lancement suivant (`MiseAJour.Bilan` : échec au centre, réussite en bas). Options relevées le 26/10, à confirmer chez les fournisseurs :
  - **Azure Artifact Signing** (ex-Trusted Signing, Microsoft) : environ 9,99 $/mois (5 000 signatures) ; ouvert aux **organisations** de l'Union européenne (nom légal vérifié), pas aux particuliers en France ; signature intégrée au build ;
  - **certificat OV** d'une autorité reconnue (Certum, Sectigo, SSL.com…) : environ 110 à 400 $/an, clé sur jeton matériel ou en ligne ; depuis le 1er mars 2026, 460 jours au plus (renouvellement chaque année) ;
  - **26/10 : le contrôle, désactivé pour les essais, ne se réactive plus sur le PC de développement** (problème connu de la mise à jour Windows KB5127648, signalé par Microsoft le 16/10/2026). L'échec annoncé (AS7b) est donc testé par simulation ; l'essai de l'installeur signé, contrôle activé, se fera sur un autre PC Windows 11 récent, ou sur celui-ci après le correctif de Microsoft ;
  - **26/10 : la structure qui vendra CIPL n'est pas encore créée** : la signature attend sa création (choisir alors entre le service de Microsoft, si c'est une société, et un certificat OV) ; d'ici là, les essais se font le contrôle désactivé par l'utilisatrice ;
  - Microsoft dit qu'une application signée par une autorité de son programme racine passe le contrôle quand il ne la connaît pas ; des éditeurs signalent qu'un logiciel signé mais tout neuf peut encore être bloqué : à vérifier sur le PC de développement, contrôle activé (AS7b).
- **Reste à l'étape 17** : passer le dépôt `CIPL-installation` en public, y publier la première release (installeur et `version.json`), supprimer `cipl_mises_a_jour.txt` du PC de développement ; signer l'installeur et le désinstalleur (directive `SignTool` de `CIPL.iss`), voir ci-dessus ; un dépôt au nom de CIPL plutôt que du compte personnel (GitHub redirige après un transfert).
- **Contenu prévu** : un installeur Windows ; l'enregistrement du protocole `cipl://` (déjà vérifié à l'étape 1) ; au lancement, la version la plus récente est lue sur le serveur et proposée au téléchargement ; un lien de téléchargement dans l'e-mail d'invitation (aujourd'hui : « demandez le lien à l'admin »).
- **Testable** : la comparaison des versions ; le choix « mise à jour obligatoire / proposée ».
- **Décision du 24/10** : d'abord un **lien** de téléchargement (envoyé à la demande, et dans l'e-mail d'invitation), le site web ensuite (un site simple peut être hébergé gratuitement ; seul le nom de domaine se paie, `cipl.fr` semble déjà acquis).
- **À décider** : où héberger le fichier de l'installeur (stockage Supabase limité en taille de fichier sur l'offre gratuite ; GitHub Releases d'un dépôt public ; un Drive partagé) ; signature du code (certificat payant, sans lequel Windows affiche un avertissement à l'installation) ; mise à jour obligatoire quand le serveur change (nouvelle version du SQL) ?

### 15b. Version Mac — ajoutée le 26/10, **faite le 26/10, à valider sur un Mac** (parcours AT ; pas de Mac disponible le 26/10 : AT3 à AT9 en attente, la version Mac ne doit pas être proposée avant)
- **Pourquoi** : « pense bien à mettre l'option pour Mac » (26/10). Décision : Windows d'abord (étape 15), le Mac ensuite ; un Mac est disponible pour tester.
- **Décisions du 26/10** : PDF par **Google Chrome ou Microsoft Edge** installé sur le Mac (sinon un message clair) ; connexion gardée dans le **trousseau de macOS** ; **liens `cipl://` retirés partout**, Windows compris (« si c'est plus utile autant les enlever pour tout » : invitations, mot de passe et inscription passent par des codes) ; distribution en **.zip**, mise à jour **par le lien** (le bandeau ouvre le téléchargement, on remplace l'application dans Applications) ; signature Apple (compte développeur, 99 €/an) à l'étape 17.
- **Fait** : `Plateforme` (Mac ou Windows, adresse d'un fichier — Windows inchangée, Mac échappée —, ouvrir un dossier dans l'Explorateur ou le Finder) ; `FacturePdfService.Navigateurs` (Chrome ou Edge, dans `/Applications` ou `~/Applications`) ; `Trousseau` (commande `security`, service « CIPL ») branché dans `SessionCloud` ; `MiseAJour` sur Mac (rien tant que `version.json` n'a pas de lien Mac ; « Mettre à jour » ouvre le .zip ; « Quitter CIPL » toujours possible dans la fenêtre obligatoire) ; `DossierEntreprise` cherche aussi `~/Library/CloudStorage` ; menu Unity **CIPL › Construire la version Mac** (`ConstruireInstalleur.ConstruireMac` : build macOS, `CIPL-mac.zip` qui garde le droit d'exécution — droits Unix et « fait sous Unix » écrits dans le zip —, lien Mac ajouté au `version.json` de la même version) ; identifiant de l'application fixé à `fr.cipl.cipl` ; `LienCipl` et `LienCiplRecepteur` supprimés, l'installeur Windows efface l'ancienne déclaration de `cipl://`.
- **Inconnu jusqu'à l'essai sur le Mac** : une application construite sous Windows n'est pas signée ; macOS peut la dire « endommagée » (parcours AT3 : `xattr -cr` puis `codesign --sign -`). Pour les clients, il faudra la signer et la faire vérifier par Apple (étape 17).
- **Déjà prêt (étape 15)** : `version.json` a un champ `mac` (lien, vide tant que la version Mac n'est pas publiée) ; l'e-mail d'invitation ajoute « Sur Mac : … » dès qu'il est rempli ; la fenêtre de choix de fichiers (StandaloneFileBrowser) marche sur Mac.
- **À adapter** (aujourd'hui Windows seulement) :
  - **PDF des factures** (`FacturePdfService.FindEdge`) : Edge cherché dans `C:\Program Files…` ; sur Mac, Edge ou Chrome dans `/Applications` (mêmes options en ligne de commande), sinon aucune facture : le point le plus important ;
  - **connexion gardée** (`Dpapi`, `SessionCloud`) : chiffrement Windows ; sur Mac, le trousseau (commande `security`) ;
  - **liens `cipl://`** (`LienCipl`, `LienCiplRecepteur`) : registre Windows ; sur Mac, schéma déclaré dans les réglages du build (Info.plist) et réception par `Application.deepLinkActivated` ;
  - **« Ouvrir le dossier »** (`SaveIO`, `SaveLocationMenu`) : `explorer.exe` ; sur Mac, le Finder (`open`) ;
  - **installation et mise à jour** : l'application dans un `.zip` ; « Mettre à jour » ouvre le lien de téléchargement (se remplacer soi-même est plus délicat sur Mac).
- **Prérequis** : module « Mac Build Support (Mono) » dans Unity Hub (le build Mac se fait depuis Windows) ; un compte développeur Apple (99 € par an) pour signer et faire vérifier l'application par Apple, sans quoi macOS la bloque (chaque client devrait passer par Réglages › Confidentialité et sécurité › « Ouvrir quand même ») : à décider avant la vente (étape 17).

### 16. Boutons « Tester » des intégrations — faite le 26/10, validée le 27/10 (parcours AU)
- **Contenu** : Réglages › Intégrations : un « Tester » par service, qui dit quel identifiant ne marche pas. Le test SMTP existe déjà (« Envoyer un email de test ») ; à ajouter : Pennylane (appel de lecture), Mapbox (une tuile).
- **Testable** : la traduction des erreurs en messages clairs.
- **Décisions du 26/10** (maquette validée telle quelle) : le test porte sur **ce qui est tapé**, sans enregistrer avant (l'e-mail de test aussi) ; les messages clairs servent **partout** (factures et relances par e-mail, invitations, dépôts Pennylane, carte Mapbox), suivis du détail technique.
- **Fait** : « Tester la clé » à droite de la clé Pennylane (nom et SIREN de la société de la clé, lecture des clients et des factures vérifiée ; l'écriture ne se teste pas sans rien créer) ; « Tester le jeton » à droite du token Mapbox (une carte de 1 × 1 pixel) ; l'e-mail de test lit les champs. Résultat en vert ou en rouge sous le champ, un échec aussi au centre. Les autres services (adresses, cadastre, INSEE, annuaire des entreprises) n'ont pas d'identifiant.
- **Réglages réorganisés (27/10, validés, parcours AV)** : Intégrations a une carte par service et devient visible par tous — Envoi des factures, Pennylane, E-mail (SMTP) réservés à l'admin ; Cartes (Mapbox) pour tout le monde (token propre à chaque PC, jusqu'à 16b). Entreprise : nom, **SIRET modifiable par l'admin** (vérifié dans l'annuaire, refusé s'il est déjà sur CIPL ; `Serveur/etape16_supabase.sql`), RIB, export. Facturation : logo, entêtes, listes de charges, textes fixes. « Documents » devient « Documents et affichage ».

### 16a. Envoi des factures depuis l'adresse du client, sans mot de passe d'application — ajoutée le 25/10, faite et validée le 28/10 (parcours AW)
- **Décisions du 27/10** (maquette de la carte E-mail validée) : Google **et** Microsoft maintenant, testés en mode test (validations à l'étape 17) ; une adresse sur le domaine du client reçoit le bouton de sa messagerie (lu dans ses serveurs de courrier, MX) ; **pas d'envoi de secours par CIPL** (l'idée d'une adresse de CIPL avait été écartée le 25/10). Raison de faire Microsoft tout de suite : Outlook.com et Hotmail refusent déjà l'envoi SMTP par mot de passe (refus 5.7.139), et Microsoft 365 le coupe par défaut fin décembre 2026.
- **Décision du 28/10** (pendant le parcours AW, précisée le même jour) : la carte propose **toujours les deux boutons**, Google et Microsoft, plus le SMTP ; celui qui est conseillé d'après l'adresse est en vert, l'autre en gris ; les deux sont en vert quand la messagerie ne se devine pas (filtre anti-spam devant, petit hébergeur). On ne cherche pas si l'adresse est un compte Google ou Microsoft (aucun moyen officiel). Constaté le 28/10 (AW10) : `agathelimouzy@gmail.com`, reliée par son compte Microsoft, envoie bien, mais l'e-mail de test est arrivé dans les **indésirables** (expéditeur `agathelimouzy@gmail.com`). Le bouton vert reste donc la messagerie qui héberge l'adresse, et le bouton gris demande confirmation (« Relier avec Microsoft ? … risquent d'arriver dans les indésirables… ») ; pas d'avertissement quand la messagerie ne se devine pas. Un compte vraiment sans boîte est signalé à l'e-mail de test (« Microsoft n'a pas de boîte e-mail pour cette adresse… »). Un refus dans la fenêtre de Microsoft rappelle l'« Approbation administrateur requise » des comptes Microsoft 365 qui ne sont pas administrateurs (constaté avec `@cipl.fr`).
- **Fait** : `Messagerie` (détection d'après le domaine ou les MX, fenêtre de connexion avec PKCE et retour sur 127.0.0.1 / localhost, envoi par l'API Gmail — message MIME — ou par Microsoft Graph `sendMail`, refus traduits) ; fonction serveur `messagerie` (etat, relier, jeton, delier) qui garde l'autorisation dans la table `messageries` (`etape16a_supabase.sql`) et ne donne à CIPL qu'un jeton d'une heure ; `SmtpConfig.fournisseur` ; `EmailService` passe par Google ou Microsoft quand l'adresse est reliée (factures, relances, invitations, e-mail de test) ; Réglages › Intégrations › E-mail : « Se connecter avec Google / Microsoft », « Utiliser plutôt un serveur SMTP », SMTP rempli d'après l'adresse ; guide « L'envoi des factures » refait (adresse d'abord).
- **Pourquoi** : pendant les premiers pas (parcours AP), le réglage SMTP a bloqué plusieurs fois : serveur mal orthographié, serveur de CIPL resté par défaut, mot de passe d'application à créer chez Gmail ou Outlook. Un nouveau client doit pouvoir envoyer ses factures sans ce réglage.
- **Décision du 25/10 (précisée le même jour)** : les factures, relances et invitations partent **de l'adresse du client** (sa messagerie Gmail, Outlook ou autre), mais il n'a plus à régler de serveur ni à créer de mot de passe d'application. (Première idée, remplacée : les envoyer depuis une adresse de CIPL au nom du client.)
- **Contenu prévu** (Réglages › Intégrations, et guide « L'envoi des factures ») :
  - Gmail et Google Workspace : « Se connecter avec Google » → la fenêtre de Google, le client autorise CIPL à envoyer en son nom (API Gmail, droit « envoyer » seulement) ;
  - Outlook, Hotmail, Microsoft 365 : « Se connecter avec Microsoft » (Microsoft Graph, droit « envoyer » seulement) ;
  - autre messagerie (OVH, Orange, Free, iCloud…) : le réglage SMTP actuel, avec le serveur et le port remplis d'après l'adresse ;
  - CIPL ne voit jamais le mot de passe : il garde une autorisation (jeton) par entreprise, comme les autres secrets (étape 9a) ; l'envoi reste sur le poste, le PDF en pièce jointe.
- **Testable** : le choix du mode d'après l'adresse (gmail.com → Google, outlook.com / hotmail.com → Microsoft, sinon SMTP et son serveur) ; le message construit pour chaque API ; la traduction des refus (autorisation retirée, quota).
- **À décider** : Google doit valider l'application CIPL avant l'ouverture au public (politique de confidentialité, domaine, courte démonstration ; jusque-là, 100 testeurs) ; Microsoft : l'application à déclarer, et certaines entreprises demandent l'accord de leur service informatique ; Google Workspace ou Microsoft 365 sur un domaine du client (contact@sci-jbc.fr) : quel bouton proposer, d'après le serveur de messagerie du domaine ; garder, en secours, un envoi par CIPL (« Répondre à » le client) pour qui ne veut rien relier ?

### 16b. API payantes : Google Maps et Pappers — ajoutée le 24/10
- **Aujourd'hui** : images satellites par Mapbox, clé rangée sur chaque PC (Réglages › Intégrations › Cartes depuis le 27/10) ; adresses géocodées par `api-adresse.data.gouv.fr` (gratuit, gardé) ; sociétés lues dans l'annuaire public de l'État (gratuit, gardé pour le SIRET), pas dans l'API payante de Pappers.
- **Décisions du 24/10** :
  - **Cartes : passer à Google Maps** (images satellites, Static Maps), à la place de Mapbox.
  - **Clés de CIPL, sur le serveur** : les clés Google et Pappers restent dans les Secrets de Supabase et ne vont jamais sur les PC (décision du 12/10, « la clé ne quitte pas le serveur ») ; le coût est inclus dans l'abonnement. Le réglage « token Mapbox » disparaît.
  - **Pappers : surveiller les locataires** (alerte quand un locataire entre en procédure collective — redressement, liquidation — ou est radié : risque d'impayé) et **fiche complète** (dirigeants, capital, chiffre d'affaires, résultat) sur la fiche du locataire.
- **Contenu proposé** : deux fonctions serveur (Edge Functions), `cartes` et `pappers`, appelées avec le jeton de l'utilisateur (abonnement valable) ; une image de carte gardée sur le serveur une fois obtenue (une adresse = un appel payant, pas un par PC ni par ouverture) ; la tournée quotidienne de l'étape 10 interroge Pappers pour les locataires sociétés et dépose une alerte dans « À traiter ».
- **Testable** : lecture des réponses Google et Pappers ; la règle d'alerte (procédure ouverte, radiation, déjà signalée) ; le corps envoyé aux fonctions (comparé au TypeScript, comme en 9c2).
- **À décider** : la formule Pappers (crédits à l'appel ou abonnement de surveillance) et le plafond de dépense ; ce que montre la fiche quand Pappers ne répond pas ; le budget mensuel Google (environ 10 000 images gratuites par mois, payant au-delà).

### 18. Plateforme agréée de facturation électronique — **avancée avant 16c et 17 le 24/10**
D'après le rapport `reports/Plateformes agréées facturation électronique.md` (22/10) :
- **Sans code, dès maintenant** : chaque SCI gérée doit avoir choisi une plateforme de réception (obligatoire depuis le 1/9/2026, amende après mise en demeure) ; un compte gratuit suffit.
- **Avant le 1/9/2027** : choisir la plateforme d'émission des loyers soumis à TVA (Pennylane, Super PDP ou B2Brouter), tester son API avec un vrai Factur-X, et l'intégrer à la place de Pennylane si elle est retenue ; déclarer le statut « encaissée ».
- **Pour CIPL lui-même** : émettre ses propres factures d'abonnement par une plateforme agréée.
- **À décider** : les questions aux fournisseurs listées à la fin du rapport.
- **Ouvrir les sociétés des clients sur la plateforme** (question du 24/10, à régler avant la vente) : chaque SCI soumise à TVA d'un client doit pouvoir émettre par la plateforme retenue. Aujourd'hui chez Pennylane : une société = son dossier, son abonnement et son jeton d'API (Essentiel au minimum) ; l'accès par OAuth est réservé aux partenaires d'intégration. À trancher :
  - qui ouvre les sociétés : le client lui-même (CIPL le guide pas à pas et il colle son jeton dans Réglages › Intégrations), ou CIPL pour lui (programme partenaire Pennylane avec OAuth, ou plateforme qui gère plusieurs sociétés sous un compte CIPL, comme Super PDP ou B2Brouter) ;
  - qui paie : le client directement à la plateforme, ou CIPL qui refacture dans l'abonnement ;
  - les SCI sans TVA (location nue d'habitation, local professionnel sans option) : PDF par e-mail, sans plateforme ; seule la réception reste obligatoire pour toutes.
- **Recherche du 25/10** (`reports/Ouverture sociétés clientes plateforme agréée.md`) — recommandation, **à confirmer par les réponses des fournisseurs** :
  - **Qui ouvre** : le gérant, une fois par SCI soumise à TVA, par un parcours lancé depuis CIPL et pré-rempli (vérification d'identité sur mobile, accord d'annuaire). L'arrêté du 27/07/2026 demande la signature du représentant légal pour désigner la plateforme de réception ; aucune plateforme ne laisse un éditeur tout faire seul, sauf l'offre éditeur de B2Brouter (eDocSync, sur devis).
  - **Qui paie** : CIPL. Chez Super PDP, les SCI sont rattachées à l'abonnement de l'éditeur.
  - **Quelle plateforme** : **Super PDP** en principale (0,01 € HT par facture, 2 € HT de vérification d'identité par SCI une fois, minimum 10 € HT/an pour tout CIPL ; Factur-X déposé tel quel ; statut « Encaissée » par une route dédiée ; bac à sable). Exemple : 5 SCI dont 3 soumises à TVA, 10 loyers par mois chacune → 3,60 € HT/an, contre 330 € chez B2Brouter et 864 € chez Pennylane (abonnement Essentiel par SCI). **B2Brouter** en secours.
  - **Côté CIPL** : un jeton par SCI (autorisation OAuth, renouvelé à chaque usage), gardé sur le serveur ; pas encore de notification automatique chez Super PDP : CIPL interroge la plateforme pour les factures reçues et les statuts.
  - **Règles** : pas de mandat de facturation tant que le gérant déclenche l'envoi depuis CIPL ; émission et réception peuvent passer par deux plateformes différentes (une SCI qui reçoit déjà chez sa banque n'a rien à changer) ; le statut « Encaissée » (date, montant payé par taux de TVA) part de la plateforme d'émission, pour les factures émises à partir du 1/9/2027.
  - **Réserves** : Super PDP est une petite équipe, sans engagement de disponibilité ni notifications automatiques à ce jour, API encore en bêta ; liste officielle relue le 26/10 (impots.gouv.fr, liste du 13/10/2026, 155 plateformes) : Super PDP y figure, numéro d'immatriculation délivré le 22/12/2025 (Pennylane et Iopole le 11/12/2025, B2Brouter le 12/12/2025 ; le nom du fichier, « liste_pa_attente_rapport_audit », évoque un rapport d'audit attendu, pour toutes ces plateformes à la fois) ; sources contradictoires sur l'inscription à l'annuaire des SCI sans TVA (à trancher avec l'expert-comptable ou le service des impôts).
- **Offre, 26/10** : la facture électronique **incluse dans l'abonnement CIPL**, pour un seul prix (« pour le client c'est quand même mieux d'avoir tout sur un même abonnement ») ; Pennylane n'y entre pas (environ 24 € HT par mois et par SCI ; gratuit seulement pour les micro-entreprises). Décision : **demander un partenariat à Pennylane** (programme « Solution Compatible » : 0 € pour l'éditeur, conversion des formats par Pennylane, coût pour le client non publié), **mais si Super PDP fait tout ce pour quoi CIPL a pris Pennylane, partir sur Super PDP**. Vérifié le 26/10 :
  - **correction du rapport du 24/10** : CIPL ne fabrique **pas** de Factur-X (aucun code ; c'est Pennylane qui convertit notre PDF, `convert_to_e_invoice`). Super PDP en fabrique un : `POST /v1.beta/invoices/convert` reçoit notre PDF et les données de la facture (modèle EN 16931 en JSON) et rend un Factur-X, puis `POST /v1.beta/invoices` l'envoie ; `POST /v1.beta/validation_reports` contrôle avant ; `POST /v1.beta/invoice_events` porte « Encaissée » (`fr:212`, montant) ; `GET /v1.beta/invoices?direction=…` liste les factures reçues ([OpenAPI](https://api.superpdp.tech/openapi/superpdp.json)) ;
  - ce que CIPL demandait à Pennylane : Factur-X et envoi à la plateforme (Super PDP : oui, et « Encaissée » documenté, ce que Pennylane ne documente pas) ; fiche client du locataire (inutile : l'acheteur est dans la facture) ; banque et prélèvements GoCardless (jamais branchés : CIPL suit les paiements lui-même) ; dépôt dans la comptabilité (Super PDP n'en a pas : le connecteur Pennylane reste **en option** pour les SCI qui y tiennent leur comptabilité).
- **Piste pour plus tard (26/10), non planifiée : suivre le compte bancaire.** Pas un prélèvement SEPA (encaisser) mais une **lecture du compte** de la SCI (« savoir si X ou Y a envoyé l'argent correctement ») : agrégation bancaire DSP2, avec l'accord du client dans son application bancaire (à renouveler tous les 180 jours), par un prestataire agréé par l'ACPR (Powens, Bridge, Tink, Salt Edge, Enable Banking… ; prix sur devis ; l'offre gratuite de GoCardless/Nordigen ne prend plus de nouveaux clients). CIPL rapprocherait les virements reçus des loyers attendus, et pourrait alors porter « Encaissée » chez Super PDP sans saisie.
- **Avant de coder** : demander un devis à B2Brouter (eDocSync) et à Iopole (le gérant n'ouvre qu'un lien) ; poser à Super PDP les questions du rapport (les factures reçues sont-elles payantes, un seul parcours peut-il couvrir plusieurs SCI du même gérant, date des notifications automatiques). Puis décision, et maquette du parcours dans CIPL (Réglages › Intégrations, liste « Premiers pas »).

### 16c. Revue complète du code (Fable) — ajoutée le 24/10
- **Décision du 24/10** : une grosse revue avec le modèle Fable, **juste avant le passage en réel** (17).
- **Contenu proposé** : tout le code, par domaine, chaque domaine relu à part : cloud et synchronisation (verrous, écriture par fiche, documents, hors ligne) ; facturation (numérotation, émission, envoi, Pennylane) ; données et documents (sauvegarde, rangement, renommages, export) ; abonnement et comptes (Stripe, inscription, mot de passe) ; écrans. Puis la liste des défauts classés par gravité, validée avec toi, et les corrections, chacune avec son test.
- **À décider** : la liste des domaines et l'ordre ; ce qui bloque le passage en réel (gravité à partir de laquelle on corrige avant).

### 17. Passage en réel
Surtout des réglages, mais à ne pas oublier :
- **Stripe** : vérification de l'entreprise, clé `sk_live_…`, nouvelle destination webhook en mode réel et son secret, portail client, « Annuler l'abonnement » après échec des relances (non modifiable en test), e-mails clients de l'étape 14 (constaté le 26/10 : grisés en mode test) — https://dashboard.stripe.com/settings/emails « Paiements réussis », https://dashboard.stripe.com/revenue_recovery/emails échec des paiements par carte **et** par prélèvement bancaire, cartes arrivant à expiration —, informations de l'entreprise sur les factures (SIREN, TVA intracommunautaire, mentions légales).
- **Google et Microsoft (étape 16a, noté le 28/10)** : Google : passer l'application de « Test » à « En production » et la faire valider (nom commercial, site et politique de confidentialité sur un domaine vérifié, courte vidéo de « Se connecter avec Google » ; `gmail.send` est un droit « sensible », sans audit de sécurité). Jusque-là : 100 utilisateurs au plus, écran « application non validée », et en mode Test l'autorisation expire au bout de 7 jours. Microsoft : « vérification de l'éditeur » (programme partenaires de Microsoft, au nom de la société commerciale), sans laquelle beaucoup d'entreprises sous Microsoft 365 bloquent CIPL (constaté le 28/10 avec `@cipl.fr` : « Approbation administrateur requise » pour un compte qui n'est pas administrateur) ; l'application est déclarée dans l'annuaire Azure du compte agathelimouzy@gmail.com, à redéclarer sous la société si besoin (nouvel identifiant dans les Secrets). Le nom commercial remplace « CIPL » dans les deux.
- **Supabase** : offre payante (l'offre gratuite **met le projet en pause** après une semaine sans activité, et n'a pas de sauvegarde quotidienne, promise par la décision « Sauvegardes » du 09/10) ; serveur d'envoi d'e-mails dédié ; modèles d'e-mails en français.
- **Juridique** : CGV et CGU, mentions légales, politique de confidentialité ; RGPD (contrat de sous-traitance avec Supabase et Stripe, registre des traitements ; données hébergées en UE : projet Supabase en Irlande).
- **Factures de CIPL lui-même** : à partir du 1/9/2027, les factures d'abonnement émises à des entreprises françaises devront passer par une plateforme agréée ; les factures Stripe ne le font pas (voir l'étape 18).
