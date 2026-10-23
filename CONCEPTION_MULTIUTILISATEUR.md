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
| Liens des e-mails | **Lien qui ouvre CIPL** : `https://cipl.app/o/…` → page relais statique → `cipl://type?jeton=…`. Protocole enregistré dans `HKCU\Software\Classes\cipl` au premier lancement. Si CIPL est déjà ouvert, le lien est transmis à la fenêtre ouverte. La page relais est nécessaire parce que Gmail et Outlook web ne rendent pas cliquables les liens `cipl://` |
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
| 14 | E-mails de l'abonnement | M | 13 | Fin d'essai J-7 et J-1 (§13), paiement refusé, résiliation |
| 15 | Installation et mises à jour | L | — | Un invité doit pouvoir installer CIPL lui-même |
| 16 | Boutons « Tester » des intégrations | S | — | Repoussés en 9c1 ; utiles au support |
| 17 | Passage en réel | M | toutes | Réglages de production, juridique, RGPD |
| 18 | Plateforme agréée de facturation électronique | M | — | Obligations de 2027 (voir le rapport du 22/10) |

### 10. Pappers sur le serveur — **faite et validée le 23/10** (parcours AI)
- **Révisé le 23/10** : CIPL n'utilise pas l'API payante de Pappers mais l'annuaire public de l'État (sans clé). Décisions : vérification depuis CIPL, sans fonction serveur ; tournée des résumés d'entreprise une fois par jour, sur le premier poste qui ouvre l'entreprise (`prendre_tournee_annuaire`), fiches en cours de modification sautées ; « Ajouter une entreprise » pré-remplit le nom et affiche la forme et le siège, refuse une société fermée ou introuvable.
- **Contenu prévu au départ** : une fonction serveur `pappers` (clé `PAPPERS_API_KEY` dans les Secrets de Supabase, jamais sur les PC) : SIRET → raison sociale, adresse, forme, radiée ou non. « Ajouter une entreprise » pré-remplit la fenêtre et refuse une société radiée. La synchronisation Pappers des fiches (`PappersSync`), suspendue sur le cloud, passe par cette fonction.
- **Testable** : lecture de la réponse Pappers ; refus (radiée, introuvable) ; comparaison du corps envoyé avec la fonction (même test qu'en 9c2).
- **À décider** : la synchronisation des fiches se lance-t-elle seule (à l'ouverture, une fois par jour) ou à la demande de l'admin ? Qui paie les appels Pappers (inclus dans l'abonnement) ?

### 11. Export de toutes les données
- **Contenu** : « Exporter toutes les données » : bâtiments, locataires, Réglages (sans les secrets) en JSON, et tous les documents, dans un dossier choisi sur le PC. Possible en lecture seule (abonnement expiré, hors ligne sur la copie). `BackupService` disparaît du démarrage.
- **Testable** : le contenu et l'arborescence de l'export ; l'export puis l'import local redonnent les mêmes fiches.
- **À décider** : admin seulement (décision « Sauvegardes » du 09/10) ou tout le monde quand l'abonnement a expiré (§13) ? Dossier ou fichier ZIP unique ? L'import d'un export (pour changer de compte, ou revenir hors cloud) fait-il partie de l'étape ?

### 12. Mot de passe oublié
- **Contenu** : sur l'écran de connexion, « Mot de passe oublié » : l'adresse → un **code à 6 chiffres** par e-mail (code de récupération de Supabase, sans lien à ouvrir, comme les invitations), puis le code et le nouveau mot de passe dans CIPL.
- **Testable** : la vérification du formulaire (code, mot de passe ×2), sur le modèle de `VerifierRejoindre`.
- **À décider** : les e-mails de Supabase partent par défaut d'un service limité à quelques envois par heure ; pour la vente, un serveur d'envoi dédié (étape 17). Le modèle d'e-mail de Supabase est à personnaliser en français.

### 13. Inscription libre-service
- **Contenu** (§13) : « Créer mon compte » sur l'écran de connexion → Société (SIRET vérifié par l'étape 10, refus si radiée ou déjà sur CIPL : « demandez une invitation à son administrateur ») ou Particulier (nom, prénom, adresse ; « Patrimoine de … ») → compte admin, groupe en essai de 14 jours, première entreprise vide. Premier lancement : Se connecter / Créer mon compte / J'ai reçu une invitation.
- **Testable** : la vérification du formulaire ; le nom par défaut d'un particulier ; les refus.
- **À décider** : comment prouver l'adresse e-mail ? « Confirm email » est décoché depuis 9b (le code d'invitation prouve la boîte). Proposition : un code à 6 chiffres envoyé à l'inscription, comme pour le mot de passe oublié, plutôt qu'un lien. Une même personne peut-elle créer plusieurs groupes ?

### 14. E-mails de l'abonnement
- **Contenu** : fin d'essai à J-7 et J-1 (§13), puis expiration ; paiement refusé et reçus de paiement (Stripe sait les envoyer lui-même : à activer dans ses réglages) ; confirmation de résiliation. Une tâche planifiée sur le serveur (une fois par jour) choisit les groupes concernés.
- **Testable** : quel e-mail pour quel groupe, quel jour (règle pure, comme `RegleAbonnement`).
- **À décider** : par quel service partent les e-mails de CIPL lui-même (pas le SMTP des clients) ? À qui : l'admin seulement, ou tous les membres pour l'expiration ?

### 15. Installation et mises à jour
- **Contenu** : un installeur Windows ; l'enregistrement du protocole `cipl://` (déjà vérifié à l'étape 1) ; au lancement, la version la plus récente est lue sur le serveur et proposée au téléchargement ; un lien de téléchargement dans l'e-mail d'invitation (aujourd'hui : « demandez le lien à l'admin »).
- **Testable** : la comparaison des versions ; le choix « mise à jour obligatoire / proposée ».
- **À décider** : où héberger l'installeur (site cipl.fr ?) ; signature du code (certificat payant, sans lequel Windows affiche un avertissement à l'installation) ; mise à jour obligatoire quand le serveur change (nouvelle version du SQL) ?

### 16. Boutons « Tester » des intégrations
- **Contenu** : Réglages › Intégrations : un « Tester » par service, qui dit quel identifiant ne marche pas. Le test SMTP existe déjà (« Envoyer un email de test ») ; à ajouter : Pennylane (appel de lecture), Mapbox (une tuile).
- **Testable** : la traduction des erreurs en messages clairs.

### 17. Passage en réel
Surtout des réglages, mais à ne pas oublier :
- **Stripe** : vérification de l'entreprise, clé `sk_live_…`, nouvelle destination webhook en mode réel et son secret, portail client, « Annuler l'abonnement » après échec des relances (non modifiable en test), informations de l'entreprise sur les factures (SIREN, TVA intracommunautaire, mentions légales).
- **Supabase** : offre payante (l'offre gratuite **met le projet en pause** après une semaine sans activité, et n'a pas de sauvegarde quotidienne, promise par la décision « Sauvegardes » du 09/10) ; serveur d'envoi d'e-mails dédié ; modèles d'e-mails en français.
- **Juridique** : CGV et CGU, mentions légales, politique de confidentialité ; RGPD (contrat de sous-traitance avec Supabase et Stripe, registre des traitements ; données hébergées en UE : projet Supabase en Irlande).
- **Factures de CIPL lui-même** : à partir du 1/9/2027, les factures d'abonnement émises à des entreprises françaises devront passer par une plateforme agréée ; les factures Stripe ne le font pas (voir l'étape 18).

### 18. Plateforme agréée de facturation électronique
D'après le rapport `reports/Plateformes agréées facturation électronique.md` (22/10) :
- **Sans code, dès maintenant** : chaque SCI gérée doit avoir choisi une plateforme de réception (obligatoire depuis le 1/9/2026, amende après mise en demeure) ; un compte gratuit suffit.
- **Avant le 1/9/2027** : choisir la plateforme d'émission des loyers soumis à TVA (Pennylane, Super PDP ou B2Brouter), tester son API avec un vrai Factur-X, et l'intégrer à la place de Pennylane si elle est retenue ; déclarer le statut « encaissée ».
- **Pour CIPL lui-même** : émettre ses propres factures d'abonnement par une plateforme agréée.
- **À décider** : les questions aux fournisseurs listées à la fin du rapport.
