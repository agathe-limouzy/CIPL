# Facturation CIPL × Pennylane — README de projet

> Fichier de référence vivant. **Mis à jour au fil de l'avancement** pour ne rien perdre entre les sessions.
> Dernière mise à jour : 2026-09-11.

---

## 1. Objectif

Ajouter la **facturation** dans l'app CIPL (Unity, gestion locative) :
- créer les factures de loyer (PDF), en **e-facture (Factur-X)** et les **envoyer via une plateforme (PDP)** ;
- gérer la **régularisation de charges** et la **refacturation** de dépenses ;
- à terme, **détecter automatiquement les paiements** (virements/prélèvements).

Contexte : **30+ locataires**, mix particuliers / entreprises (baux commerciaux avec TVA).

## 2. Architecture retenue

**CIPL = cerveau métier** (calculs) · **Pennylane = moteur d'émission + banque**, piloté **par l'API** depuis CIPL.

```
CIPL (calcule loyer / régul / refac)
  │  API → crée + finalise la facture (ton template)
  ▼
Pennylane : PDF + Factur-X → envoi PDP → banque/paiement (GoCardless)
  │  ◄── API : CIPL relit le statut (émise → payée)
```

CIPL garde ce que Pennylane ne sait pas faire : **calcul régularisation** (prorata/lot), **refacturation** (lien dépense→locataire), et le **pilotage**.

## 3. Décisions actées

| Sujet | Décision |
|---|---|
| Plateforme | **Pennylane** (essai 15 j OK, clé API obtenue, connexion validée) |
| Génération de la facture | **Pennylane génère** (template custom sans quantité) ; CIPL pousse les données par API |
| Génération loyer | **En lot** (période) **+ cas par cas** |
| Émission (finalisation) | **Validation manuelle** : brouillon → vérif → « Émettre » |
| Organisation UI | **Hub « Facturation »** (menu général) **+ onglet « Factures »** par locataire |
| RIB | **Option B** : RIB **injecté dans un champ de la facture** (varie par locataire) |
| Paragraphe bail | **Variable par locataire**, stocké dans CIPL, injecté dans `pdf_invoice_free_text` |
| Numérotation | Faite **par Pennylane** à la finalisation ; CIPL **stocke le n° renvoyé** |

## 4. Accès & config technique

- **Clé API** : fichier `C:\Users\agath\Documents\CIPL\CIPL_Git\pennylane_api_key.txt`
  → **HORS du dépôt git** (le repo est `CIPL_Git\CIPL`, la clé est dans le parent) → safe.
  ⚠️ **Ne JAMAIS** mettre la clé dans le chat ni dans le repo.
- **Base API** : `https://app.pennylane.com/api/external/v2`
- **Auth** : en-tête `Authorization: Bearer <token>`
- **Société** : GROUPE CIPL (SIREN 717220883) · utilisatrice : Agathe Limouzy
- **Scopes du token** : `customers`, `customer_invoices`, `commercial_documents`, `billing_subscriptions` (GoCardless), `bank_accounts`, `webhook_subscriptions` → tout le nécessaire.
- **Plan requis** : Essential+ (doc Pennylane) ; la clé fonctionne donc le plan est OK.
- **Créer un token** : Pennylane → Paramètres → Connectivité → Développeurs → « Générer un token API » (V2, Read & write). Affiché **une seule fois**.

## 5. Endpoints & schémas API (vérifiés en test)

- **Test connexion** : `GET /me` → 200.
- **Créer un client entreprise** : `POST /company_customers`
  ```json
  {"name":"...","emails":["..."],"external_reference":"...","reg_no":"<SIREN 9 chiffres>",
   "billing_address":{"address":"...","postal_code":"...","city":"...","country_alpha2":"FR"}}
  ```
  ⚠️ champ **`country_alpha2`** (pas `country`). `reg_no` = **SIREN (9)**, tronque le SIRET (14).
- **Chercher un client** : `GET /customers?filter=[{"field":"external_reference","operator":"eq","value":"..."}]`
- **Créer une facture** : `POST /customer_invoices`
  ```json
  {"customer_id":123,"date":"AAAA-MM-JJ","deadline":"AAAA-MM-JJ",
   "invoice_lines":[{"label":"...","quantity":1,"unit":"forfait","raw_currency_unit_price":"4293.22","vat_rate":"FR_200"}],
   "draft":true,
   "pdf_invoice_subject":"Loyer avril 2025",
   "special_mention":"la TVA est payée sur les débits",
   "external_reference":"...",
   "customer_invoice_template_id": <optionnel>}
  ```
  - ⚠️ **montants en CHAÎNES** (`"4293.22"`).
  - `vat_rate` : `FR_200`=20 %, `FR_100`=10 %, `FR_055`=5,5 %, `exempt`=0 %.
  - `draft:true` = **brouillon** (rien émis) ; omettre / `false` = **finalise** (émet).
- **Paragraphe bail** : `pdf_invoice_free_text` = **JSON stringifié** (❗ pas un array brut → 400) :
  `"[{\"type\":\"paragraph\",\"children\":[{\"text\":\"...\"}]}]"` — se met bien via `PUT /customer_invoices/:id`.
- **Mettre à jour** : `PUT /customer_invoices/:id`.
- **Récupérer le PDF** : champ **`public_file_url`** dans la réponse (lien PDF public).
- **Finaliser (émettre)** : endpoint exact **à confirmer** (probable `.../finalize` ou `draft:false`) → génère PDF + Factur-X + PDP.
- **Envoyer par email** : `POST /customer_invoices/:id/send_by_email`.
- **GoCardless** : mandats par client (`mandates`), prélèvement pilotable par API.

## 6. Contenu d'une facture (inventaire complet — relevé sur factures Auto Evasion & Volteo)

- **Émetteur (fixe, Groupe CIPL)** : logo · lieu « St Marcel Paulel » · pied de page (SAS capital 1 500 000 € · SIRET 717 220 883 00044 · TVA FR 28 717 220 883 · APE 6820B · siège « La Louve » 6 route d'Agde 31590 St Marcel Paulel · tél 06.07.04.69.28 · contact@cipl.fr). → réglages société + template Pennylane.
- **Destinataire (locataire)** : nom, adresse, **SIRET** (ex. Volteo 49497269800034). → client Pennylane (`reg_no`).
- **Corps** : date · n° (`AAAA/NNNN`) · **paragraphe bail** (varie par locataire : dates, lot, surface, mensuel/trimestriel + « prestations de services » + « payable mensuellement/trimestriellement ») · titre période (« Loyer avril 2025 »).
- **Montants** : Total période · Provision charges · Total H.T. · TVA 20 % · Total T.T.C. · mention « **la TVA est payée sur les débits** » · « SOMME A NOUS REGLER LE … ».
- **RIB (VARIE par locataire)** : Titulaire Groupe CIPL · Banque · RIB · IBAN · BIC. (option B : dans un champ facture.)
- **Mentions légales** : pénalités de retard + indemnité 40 € (Art L441-6 C.com).

## 7. Ce que CIPL stockera

- **Sur le locataire** (profil facturation) : `pennylaneCustomerId`, `paragrapheBail`, `rib` (titulaire/banque/RIB/IBAN/BIC), `periodicite` (mensuel/trimestriel), `tva` (avec 20 % / sans), `siret`, `mentionSpeciale`.
  *(loyer HT + provision charges : déjà dans CIPL.)*
- **Modèle Facture** : `pennylaneInvoiceId`, `numero`, `type` (loyer/régul/refac/avoir), `statut` (brouillon/émise/envoyée/payée), montants, période, dates, `pdfUrl`, `locataireId`.

## 8. Organisation UI dans CIPL

- **Hub « Facturation »** (menu général) : **À facturer** (liste des loyers dus → « Générer les brouillons ») · **À valider** (brouillons → aperçu → « Émettre ») · **Suivi** (émises + statut, impayés en évidence) + accès Régularisation & Refacturation.
- **Onglet « Factures »** (fiche locataire) : paramètres de facturation + historique + PDF + boutons cas par cas.

## 9. Régularisation & refacturation (règles)

- **Répartition charges** : au **prorata surface** OU **montant par lot** (au choix, selon la charge).
- **Régularisation** : solde à **facturer** (facture) OU à **rembourser** (avoir) — gérer les deux.
- **Refacturation** : à l'euro ou avec **marge**, **TVA selon le cas** (régime à confirmer avec le comptable).

## 10. Plan / phases de développement

1. **Profil facturation** (fiche locataire) + **modèle Facture** dans CIPL.
2. **Hub Facturation** (squelette : À facturer / À valider / Suivi).
3. **Loyer en lot** (générer brouillons → valider → émettre via API).
4. **Régularisation** (charges réelles → répartition → facture/avoir).
5. **Refacturation** (depuis dépense reçue).
6. **Rapprochement des paiements** (GoCardless / banque).

## 11. Pièges / nuances découverts

- `pdf_invoice_free_text` = **JSON stringifié** (array brut → 400).
- `billing_address.country_alpha2` (pas `country`).
- `reg_no` = **SIREN (9)**, pas SIRET (14) — à vérifier si le SIRET complet est requis pour le Factur-X.
- **Arrondi TTC** : Pennylane 5 151,86 vs Excel 5 151,87 (1 ct) → réglable.
- **RIB ET paragraphe bail varient par locataire**.
- Le rendu PDF ne se convertit pas en image dans cet environnement (poppler absent) → pour voir un PDF, l'ouvrir directement ou le télécharger.

## 12. Tests réalisés (dans le vrai compte Pennylane — À NETTOYER)

- `GET /me` : OK (GROUPE CIPL).
- Client **TEST — Auto Evasion (CIPL)** : id `1457486979072` · brouillon facture id `28800305487872` (1 ligne loyer, HT 5097.30 / TVA 1019.46 / TTC 6116.76).
- Client **TEST — Volteo EBT (CIPL)** : id `1457548754944` (reg_no 494972698) · brouillon facture id `28802084667392` (loyer 4293.22, + sujet + special_mention + paragraphe bail). PDF récupéré.
- **Tests IMPORT (Voie 2) — 2026-09-05, réussis, NON émis** :
  - facture id `28999279636480` (import simple, statut `incomplete`, doc = `facture_volteo_test.pdf`) + `file_attachment` `81008558080` ;
  - facture id `28999504625664` (import **+ Factur-X**, statut `incomplete`, `factur_x:true`, `e_invoicing:null`) + `file_attachment` `81014890496`.
- 👉 **À supprimer dans Pennylane** quand terminé : les 2 clients TEST + leurs brouillons + les 2 factures import ci-dessus + les 2 file_attachments.

## 13. État en cours / à décider

- ✅ **Rendu PDF Pennylane validé par Agathe** (2026-09-04).
- ❌ **RIB en mention (option B) ABANDONNÉ** : Agathe n'aime pas le rendu texte. Elle veut le **bloc RIB natif** de Pennylane.
- ✅ **Solution RIB retenue : 1 template = 1 compte bancaire.** Découvertes API : la facture **n'a pas** de champ `bank_account` (RIB natif = compte des réglages société) ; les `bank_accounts` sont **vides** ; MAIS la facture a **`customer_invoice_template_id`** (choisi **par facture**). Donc → créer **un template par compte** (chacun affiche son RIB natif) et CIPL choisit le bon template par locataire. **3 comptes** chez Agathe → 3 templates, gérable.
  - Endpoint templates : `GET /customer_invoice_templates` → nécessite le scope **`customer_invoice_templates:readonly`** (à ajouter à la régénération du token), sinon donner les IDs à la main.
  - **Prérequis Agathe (dans Pennylane) :** ajouter les 3 comptes bancaires + créer 3 templates (RIB + mise en page sans quantité) + régénérer le token avec le scope templates.
  - **CIPL :** chaque locataire → `templateId` du bon compte → mis sur la facture à la création.
  - **Capacités API vérifiées** : `POST /bank_accounts` = **créable** par l'API (demande `name` + IBAN/BIC…) ; `POST /customer_invoice_templates` = **404** → templates **NON créables/modifiables** par l'API (à faire dans l'éditeur visuel Pennylane, lecture seule côté API).
  - ⚠️ **CORRECTION 2026-09-05 — Voie 2 (import) émet BIEN du Factur-X** (l'ancienne conclusion « import = compta seulement » était fausse). L'endpoint **`POST /customer_invoices/import`** avec **`convert_to_e_invoice: true`** convertit **de façon asynchrone** la facture importée en **Factur-X (PDF/A-3)** en **embarquant le XML structuré dans le PDF fourni**. Contraintes : fichier **PDF**, **toutes les lignes avec `label`**, `invoice_number` ≤ **35 car** (`[A-Za-z0-9-+_/]`). Réponse : champ `factur_x` (bool) + `schematron_validation_status`. Si le fichier n'est pas prêt → **HTTP 409**, réessayer.
- **➡️ Deux architectures viables (à trancher avec Agathe) :**
  - **Voie 1 — Create** : Pennylane **génère** le PDF depuis un **template**. RIB = template (créé **en UI**, 1 par compte). Présentation = style Pennylane. Paragraphe bail via `pdf_invoice_free_text`.
  - **Voie 2 — Import + `convert_to_e_invoice`** : **CIPL génère son propre PDF** (maquettes HTML déjà prêtes = présentation identique aux factures actuelles d'Agathe), l'upload via `/file_attachments`, puis `import` → Pennylane l'emballe en Factur-X + PDP. **RIB + paragraphe + logo + tout = 100 % CIPL, PLUS BESOIN de templates ni de multi-RIB dans Pennylane.** Numérotation gérée par CIPL.
  - **Reco : Voie 2** — colle au besoin « garder la présentation actuelle » ET règle le problème du RIB par locataire d'un coup (CIPL dessine le bon RIB). Le XML légal reste construit par Pennylane à partir des données structurées envoyées → conformité OK.
- ✅ **Pièces jointes / annexes (refacturation de charges) = OUI par l'API.** La facture expose une sous-ressource **`appendices`** (`.../customer_invoices/{id}/appendices`), **présente même sur une facture générée** (vérifié sur le brouillon de test). Flux : **`POST /file_attachments`** (PDF/PNG/JPEG…, max 100 Mo) → récupérer **`file_attachment_id`** → le **lier via `.../appendices`**. (Les noms `/attachments`, `/annexes`, `/documents` testés = renvoient le HTML de l'app = **pas** des endpoints API ; le bon nom est **`appendices`**.) Côté UI = onglet **« Emails et Annexes »**.
- ✅ **2026-09-05 — Voie 2 « plomberie » PROUVÉE (test réel, rien émis).** Chaîne validée : `POST /file_attachments` (multipart, → **201**, renvoie `file_attachment_id`) → `POST /customer_invoices/import` avec **`import_as_incomplete:true`** (→ **201**) → facture créée en statut **`incomplete`** (non finalisée/non émise) **avec le PDF de CIPL comme document** (`filename` = mon PDF). Aucune transmission, aucun envoi.
  - **Schéma ligne d'import (TOUS requis)** : `label, quantity, unit, raw_currency_unit_price, vat_rate, currency_amount (= TTC de la ligne), currency_tax`. **Règle** : `somme(invoice_lines.currency_amount)` doit = `currency_amount` total (TTC), sinon **422**. Totaux top-level : `currency_amount_before_tax` (HT), `currency_tax` (TVA), `currency_amount` (TTC).
  - ✅ **CONFIRMÉ 2026-09-05 (test réel, non émis)** : `import_as_incomplete:true` **+** `convert_to_e_invoice:true` → facture id `28999504625664` **reste `incomplete`** (NON finalisée, NON émise), **`factur_x:true`** (Factur-X généré, XML embarqué dans MON PDF), **`e_invoicing:null`** (aucune transmission PDP). Donc `convert` **ne finalise pas** et **ne transmet pas**. `schematron_validation_status` = `pending` (validation EN 16931 async → à revérifier ; « valid » = données structurées conformes). **➡️ Conclusion : Voie 2 = chemin retenu.**
- À confirmer : endpoint exact de **finalisation** (Voie 1) + **schéma POST `/appendices`** + suivi **Factur-X/PDP** dans l'API.
- Prochaine action dès que comptes + templates créés : construire **Phase 1** (profil facturation + modèle Facture, avec le champ template/compte par locataire).

## 13bis. Cahier des charges UI (parcours utilisateur validé avec Agathe, 2026-09-05)

Source : schéma de parcours fourni par Agathe. Trois modules qui se greffent sur les écrans existants (Menu, fiche Bâtiment, fiche Locataire).

### A. Réglage (socle)
- Clé API Pennylane.
- **Mode d'envoi (interrupteur GLOBAL)** : *Pennylane (e-facture Factur-X)* **ou** *Email direct*. En mode Email, CIPL envoie le PDF par email au locataire (adresse saisie dans la facture) via SMTP. Le bouton reste « Sauvegarder et envoyer » dans les deux cas ; seul le routage change.
- **SMTP** (mode Email) : serveur, port, adresse d'expédition. Cible = **Outlook / Microsoft 365 @cipl.fr** → `smtp.office365.com:587` (STARTTLS). ⚠️ M365 a souvent le **SMTP AUTH (Basic Auth) désactivé par l'admin** → à activer pour la boîte, ou mot de passe d'application (MFA), sinon OAuth2. Identifiants **hors repo, jamais dans le chat** (comme la clé API). Test d'envoi réel : d'abord vers Agathe elle-même.
- Rappel juridique : « Email direct » OK pour B2C / transition ; pour le **B2B** (locataires sociétés) la e-facture via PDP sera **obligatoire (2026-2027)** → repasser en mode Pennylane.
- **RIB** : liste + Ajouter/Modifier/Supprimer (popup « Voulez-vous supprimer ce RIB ? »). Chaque RIB a un **ID**.
- **Entêtes** (modèles de texte) : liste + Ajouter/Modifier/Supprimer (popup). Édition avec insertion de **variables** (infos locataire/bâtiment) ; bouton « Modifier » ↔ « Valider ». Chaque entête a un **ID**.
- Phrase de retard + indemnité ; Bas de page ; Emplacement de sauvegarde.
- « Charger une save » : explorateur de fichiers → recharge une société (tous bâtiments/locataires) ou propose de créer le dossier.
- RIB et entêtes **référencés par ID** depuis les locataires (pas de copie figée : modifier un RIB le met à jour partout où il est utilisé).

### B. Charges (onglet sur la fiche Bâtiment)
- Onglet « Charge » (à côté d'Achat / Travaux) → Historique des charges de l'année.
- Ajouter une charge : Nom, Coût, Locataire(s) concerné(s) (Tous / Loc…), Type de ratio (si ≠ Tous et > 1 locataire → répartition par **ratio modifiable**), PDF facture (gestionnaire de fichiers, remplaçable), Date.
- Range le PDF dans `<Batiment>/Charge`, mémorise la charge (statut **impayé**) pour régul/refac.

### C. Locataire → Facturation
- Fiche enrichie : Info générale + **RIB choisi (ribId)** + taux rentabilité ; Bail ; Commentaire ; Dépôt de garantie (+ restitution) ; Liste amélioration ; case **« en provision pour charge »**.
- **4 types de facture** (boutons) :
  - **Loyer** : toujours.
  - **Refacturation** : toujours.
  - **Régularisation** : seulement si **en provision pour charge**.
  - **Révision dépôt de garantie** : facture de la révision calculée dans le menu « Révision dépôt de garantie » (date, loyer prérempli non modifiable, TTC, nb de mois → Réviser → Ancien ↔ Nouveau dépôt).
  - Règle : provision OUI → Refacturation **+** Régularisation ; provision NON → **Refacturation seule**.
- Chaque bouton → panneau **« Information Facture »** pré-rempli, **mémorisé par locataire ET par type** (valeurs + états de cases persistants, modifiables) : Nom, Adresse, Email, SIRET, **RIB CIPL** (menu déroulant = un des RIB du Réglage), Date, Loyer/Charge, Provision, **Paragraphe** (entête selon locataire), **Modèle de texte** (menu déroulant), **TVA débit** (case), envoi email (case + adresse) → **Générer facture** (aperçu) → **Sauvegarder et envoyer**.
- Effets à l'enregistrement (nom de fichier + envoi Pennylane → Factur-X) :
  - Loyer → `Loyer-<locataire>-<mois>-<année>`.
  - Refacturation → `Refacturation-<NomCharge>-<locataire>-<mois>-<année>` (+ PJ facture si sélectionnée), la charge passe **impayé → payé**.
  - Régularisation → `RegularisationCharge-<locataire>-<année-1>` (total charges vs provisions + **tableau récap** + PJ), **toutes** les charges concernées passent **payé**.
  - Révision dépôt → `RegularisationDepotDeGarantie-<locataire>-<année>`.

### Arborescence de sauvegarde (fichiers)
```
<Société>/
  Batiment/
    <Batiment X>/
      Charge/                -> PDF des charges
      <Locataire Y>/
        Facture/             -> factures générées
  Sauvegarde/                -> back-ups + sauvegarde actuelle
```

### Modèle de données (esquisse)
- **Reglage** : apiKey ; **modeEnvoi (Pennylane | Email)** ; **smtp{host, port, fromEmail, cred (hors repo)}** ; ribs[{id, name, titulaire, domiciliation, rib, iban, bic}] ; entetes[{id, nom, texte+variables}] ; phraseRetard ; basDePage ; emplacementSauvegarde.
- **Locataire** : + ribId, enteteId, enProvisionPourCharge(bool), tauxRentabilite, factureState{ Loyer:{champs+cases}, Refacturation:{…}, Regularisation:{…}, DepotGarantie:{…} }.
- **Charge** (niveau bâtiment) : id, nom, cout, locatairesConcernes[], typeRatio, ratios{}, pdfPath, date, statut(impayé/payé).
- **Facture** : type, locataireId, ribId, enteteId, lignes, montants, pjPath?, nomFichier, **canalEnvoi (Pennylane | Email)**, statutEnvoi, statutPennylane, factur_x.

### Ordre de construction retenu
1. **Réglage** (RIB + entêtes + clé API) — socle.
2. **Modèle de données** + tranche **Loyer** bout-en-bout (Info Facture → Générer → aperçu → Sauvegarder+envoyer via Pennylane).
3. **Charges** (onglet bâtiment) + **Refacturation** + **Régularisation**.
4. **Dépôt de garantie** (révision + restitution).

## 14. Règles de sécurité

- Clé API **hors repo**, **jamais dans le chat**, **jamais commitée**.
- **Finalisation = émission légale d'une vraie facture** → **validation manuelle uniquement**, jamais sans accord explicite d'Agathe.
- Rester en **brouillon** (`draft:true`) pour tous les tests.

## 15. Liens

- Doc API Pennylane : https://pennylane.readme.io/
- Plan d'intégration (artifact) : https://claude.ai/code/artifact/1edb948f-582d-4b46-ae8d-463c0c476a09

## 16. État d'avancement (implémenté)

> Mis à jour 2026-09-11. Génération PDF **locale uniquement** (Edge headless) ; **aucune émission Pennylane** réelle pour l'instant (Voie 2 upload = à faire, avec accord explicite). Clé API dans `pennylane_api_key.txt` hors repo.

### 16.1 Génération de factures (PDF local, aperçu in-app)
`FacturePdfService` (HTML StreamingAssets + Edge `--print-to-pdf` / screenshot PNG). 4 types validés visuellement :
- **Loyer** (`FactureLoyerPanel`), **Régularisation** (`FactureRegulPanel`, détail charges page 2), **Refacturation** (`FactureRefacPanel`), **Révision dépôt** (`FactureDepotPanel`, sans TVA). Aperçu PDF zoomable (`FacturePreviewViewer`).
- **Période proposée par défaut = période EN COURS (2026-09-14)** : à l'ouverture de « Facturer le loyer » sans facture mémorisée, le menu « Période » proposait **toujours la 1re** période (`DefaultPeriodeIndex` renvoyait `1` sauf en mensuel) → on facturait le « 1er trimestre » par erreur. Corrigé : défaut = **période courante d'après la date du jour** (mensuel → mois ; trimestriel → `(mois-1)/3+1`, ex. sept → 3e trim ; bi-annuel → semestre ; annuel → 1). ⚠ L'**échéance** affichée pour une facture émise reste sa **date de paiement mémorisée** (date de facture + 30 j), distincte de la date calendaire de la période — d'où un « 1er trim » émis pouvant apparaître à une échéance d'octobre dans le Suivi.
- **Entête auto par type de facture (2026-09-15)** : à l'ouverture d'un panneau sans entête mémorisée (`FactureInfo.enteteId` vide), le menu « Entête » se **positionne automatiquement** sur l'entête correspondant au type — Loyer/Regul/Refac/Depot. `ReglageService.EnteteChoisi(stored, type)` = mémorisée si présente, sinon `EnteteDefautId(type)` qui prend la **1re entête dont le NOM contient un mot-clé du type** (loyer ; régularisation/charges ; refacturation ; dépôt/garantie), à défaut la 1re de la liste. Appliqué dans les 4 `Facture*Panel` (arg de sélection du `_enteteDD.SetOptions`). Une entête modifiée à la main reste mémorisée (`enteteId` sauvé) et l'emporte.
- **Option « montant mensuel à régler »** (2026-09-12, loyer non mensuel) : toggle dans « Options & envoi » (masqué si périodicité mensuelle, coché par défaut) → ajoute sous la mention TVA la ligne « Suite à votre demande, le montant mensuel à régler est de : X € » avec **X = TTC de la période ÷ nombre de mois de la période** (trim ÷3, semestre ÷6, an ÷12 ; `12 / NbPeriodes`). Placeholder `{{MENSUEL_ROW}}` du template, champs `Data.afficherMensuel/montantMensuel`, mémorisé `FactureInfo.ajouterMensuel`.

### 16.2 Suivi des états de facturation
`FactureEtat` + `FacturationSuivi` : états **À venir → À faire → En attente d'envoi → Envoyé → Impayé/Payé**. Alertes d'échéance : `FacturationAlertes`.
- **Machine à états alignée sur le diagramme de comportement (2026-09-12)** — voir §16.8. Transitions automatiques (calculées à la volée par `EtatDe`, aucun enregistrement pour À venir/À faire) :
  - **À venir → À faire** à `échéance − Lead(type)` : **Loyer = 22 j** (15 j avant l'envoi + 1 sem.), **Régul / Dépôt / Refac = 0** (à la date elle-même ; l'anticipation « à préparer » est portée par `FacturationAlertes`, pas par le suivi).
  - **En attente d'envoi** (`AttenteEnvoi`, pastille violette) : un **loyer préparé plus de 15 j avant l'échéance** passe dans cet état (au lieu d'« Envoyé ») ; `EtatDe` le fait basculer **« Envoyé » automatiquement à J‑15** (bascule d'**état** seulement — aucune émission réelle ; le PDF a déjà été généré en local). Choisi dans `MarquerEnvoye` selon `type=="Loyer"` + date. Compté hors créances tant qu'il est « en attente ».
  - **Envoyé → Impayé** à **`échéance + 15 j`** (`ImpayeApresEcheanceJours`, **et non plus `dateEnvoi + 15 j`**) si non réglé.
  - `DejaTraite` inclut `AttenteEnvoi` (l'alerte « à faire » s'éteint dès que le loyer est préparé). `Dues` (créances) = Envoyé + Impayé uniquement.
- **Génération des lignes de loyer** : **une ligne par PÉRIODE selon la périodicité** du loyer — `NbPeriodes` : mensuel→**12**, trimestriel→**4**, bi-annuel→2, annuel→1 (via `MoisEcheance`/`PeriodeLibelle`). Clé `loyer-{année}-P{période}`. **Décision utilisatrice 2026-09-11** : le Suivi suit la périodicité (changer trim→mensuel donne bien 12 lignes) — PAS les « mois facturés » `moisFacturationLoyer` (qui restent utilisés pour l'alerte « prochaine échéance » quand périodicité ≠ mensuel).
- **`Fusion`** rafraîchit le **libellé** (+ suffixe **« — corrigée(X) »** depuis `corrections`, voir §16.8) en conservant le **statut/envoi** stocké. **Échéance (correctif 2026-09-14)** : une facture **émise** (Envoyé/En attente d'envoi/Impayé/Payé) **garde SON échéance stockée** (celle imprimée sur la facture) → l'état (Impayé à échéance+15) est **cohérent entre le Suivi locataire et la colonne Créances** (qui lit le stocké). Avant, `Fusion` écrasait l'échéance par celle recalculée de la période → une facture pouvait apparaître « Envoyé » dans Créances et « Impayé » dans le Suivi. Seules les **lignes planifiées non émises** alignent leur échéance sur la période courante.
- Régul : ligne `regul-{année}` (échéance janvier N+1). Dépôt : ligne `depot-{année}` si `dateRevisionDepotISO` tombe dans l'année.
- Par locataire : `FacturationSuiviPanel` (pop-up) **et** `LocataireSuiviInline` (bandeau pleine largeur en bas de la fiche ; sélecteur d'année = **chips clonés des filtres d'améliorations** `objectivesManager.filterAllButton`, année active = ambre + texte blanc).
  - **Rafraîchissement auto après génération (2026-09-14)** : générer une facture (Loyer / Régul / Refac / Dépôt) ne mettait **pas** à jour le Suivi inline (il fallait rouvrir la fiche). Ajout de `LocataireSuiviInline.Refresh()` (reconstruit années + tableau sans refaire le décor) + `LocataireSuiviInline.RefreshFor(fiche)` (statique, cible le Suivi de la fiche via `Resources.FindObjectsOfTypeAll`), appelé à la fin de `SauvegarderEtEnvoyer` des 4 panneaux (branche normale **et** correction pour le loyer) juste après `SaveAfterModifyToDoListLocataire()`.
  - **Pastille d'année (2026-09-15)** : dans le sélecteur d'année du Suivi, une **petite pastille** (10 px, coin haut-droit du chip) marque toute année contenant une facturation qui demande une action — **rouge `#D85A30`** s'il y a un **Impayé**, **ambre `#A9741C`** s'il y a du **À faire** ou **En attente d'envoi**, rien sinon. Calcul par `YearAlerteColor(y)` (parcourt `FacturationSuivi.Lignes(_loc, y)` → `EtatDe`), pastille via `AjouteYearPastille` ; dans `LocataireSuiviInline` **et** `FacturationSuiviPanel`, reconstruit à chaque `RebuildYears`.
  - **Menu d'état — placement intelligent (2026-09-14)** : le menu déroulant de changement de statut (Payé/Impayé/Envoyé/À faire/Automatique) s'ouvrait toujours **vers le bas** → pour la **dernière ligne** il débordait sous le cadre. Helper réutilisable **`UIFactory.PlacePopup(popup, anchor, margin)`** : reconstruit le popup pour mesurer sa hauteur puis le place **sous l'ancre**, ou **au-dessus** s'il manque de place en bas (`c[0].y - h >= margin`). Utilisé par `OpenStatutMenu` de `LocataireSuiviInline` **et** `FacturationSuiviPanel` (les items sont ajoutés **avant** l'appel pour que la hauteur soit mesurée).
- Global (menu principal) : colonne **« Créances »** (`FacturationHomeSection`) — KPIs, groupé par locataire (repliable), filtres banque/locataire/état, pastilles banque. `FacturationGlobalPanel` (plein écran) existe mais **non branché** (jugé redondant).

### 16.3 Rappels d'échéance (comme la révision de loyer)
Pour **révision dépôt · régularisation charges · envoi loyer** (`FacturationAlertes` porte un `type` = Loyer/Regul/Depot) :
- **Pastilles dans la fiche** : badge **« Révision à faire »** + **date en rouge** sur la carte Dépôt (helper `RevisionDepotDue`) ; petite **pastille « ! » rouge** en haut-droite des boutons d'action Facturation (Facturer le loyer / Régularisation / Révision du dépôt) — pilotées par `RefreshAlertes`.
- **Lignes dans « À traiter »** (menu principal) : `HomeAlertCollector` ajoute une ligne par alerte de facturation (`HomeAlert.Kind.Facturation`, pastille ambre) — types courts « Facturer » / « Régul. » / « Rév. dépôt » (raccourcis pour tenir dans la pastille) — cliquable → ouvre la fiche du locataire.
- La zone d'alertes en haut de la carte Facturation (`_alertesBox`) liste toujours les échéances (URGENT/Attention).
- **« À traiter » par bâtiment (2026-09-14)** : la **fiche résumé** du bâtiment (`BatimentSummaryView`, `VueResume`) est en cours de refonte en **tableau de bord**. Étape 1 : carte code **« À traiter »** insérée après la carte identité, alimentée par `HomeAlertCollector.Collect(new[]{ bp })` (révisions loyer/dépôt · régul · impayés · fins de bail · objectifs obligatoires **de ce bâtiment**), rendu **identique au « À traiter » du menu** : **même fond de carte** (Image racine = **liseré gris-bleu `#5C6E85`** RoundedRect + enfant **`CardBg` crème inséré** `offsetMin=(8,0)`/`offsetMax=(0,0)`, `ignoreLayout`, rendu derrière → liseré de 8 px à gauche ; **VLG racine `padding=(28,16,10,12)` `spacing=4`** = mêmes marges que le menu → la **bande de titre « flotte »** (marge crème au-dessus/à droite, gouttière à gauche) au lieu d'un bandeau pleine largeur), réutilise le **même prefab de ligne** (`GeneralMenuPanel.Instance.alertRowPrefab` → `HomeAlertRowUI`), en-tête gris-bleu avec **icône `sec_target`** + **badge rouge** de compteur (pastille 26×20, comme `NbBadge`) ; **colonne « Bâtiment » masquée** (un seul bâtiment) ; **ligne d'en-têtes de colonnes reconstruite à l'identique du menu** (`ATraiterHeader`/`HeaderCol` : HBox padding `(4,8)` spacing 10, spacer 4 px, **Type** 170 gauche · **Description** flex · **Locataire** 170 droite, police 21 gris `TexteSecondaire`) ; + KPI (loyers/investi/rendement) **agrandis** (autosize borné 18–30). **Affichée « seulement si utile »** (choix utilisatrice) : au moins une alerte ET **≥2 locataires nommés** (`nommes >= 2`). À **0 ou 1 locataire** la carte est **toujours masquée** (doublon avec la fiche du locataire, ou rien à agréger) — la branche « alerte propre au bâtiment » a été retirée (2026-09-14) car elle laissait la carte apparaître avec 0 locataire.
  - **Correctif chevauchement KPI (2026-09-14)** : le HBox `RowTraiterLoc` a `childControlHeight=false` (pour préserver le CSF des deux cartes) → il ne mesurait pas leur hauteur et restait à ~100 px ; la carte « À traiter » (plus haute, ex. 402 px) débordait alors vers le haut (centrée dans une rangée trop courte) et **recouvrait la rangée KPI** au-dessus. `AjusteHauteurRangee()` (appelée en fin de `RefreshATraiter`) force `RowTraiterLoc.LayoutElement.preferredHeight` = **la plus haute des cartes actives** (`LayoutRebuilder.ForceRebuildLayoutImmediate` + `LayoutUtility.GetPreferredHeight`), donc la rangée fait la bonne taille et les cartes ne débordent plus.
  - **Carte Locataires — bande de titre + loyer non tronqué (2026-09-14)** : le titre « Locataires » reçoit une **bande de fond** comme « À traiter » mais en **vert** (famille identité `#D6ECE3`, icône verte, sprite arrondi repris de la bande À traiter) via `EnsureLocatairesBand()` (Image + LayoutElement 58 px posés sur `RowEntete`). Et dans `LocataireRowUI.Setup`, le **loyer** (`txtLoyer`) élargi à **128 px** + `overflowMode=Overflow` (avant : « 180 000 €/an » tronqué en « …/... » depuis le passage en fs 16).
- **Étape 2 — tuiles KPI à la DA (2026-09-14, maquette validée)** : la grille plate `InfoGrid` de `CarteBatiment` est **masquée** et remplacée par **4 tuiles KPI** construites en code (`EnsureKpiTiles`/`KpiTile`) — **Loyers/an** (vert), **Investi**, **Rendement net** (signé), **Cash flow/mois** (signé) — gros chiffres (autosize 18–27, gras) ; **tuiles rendues plus lisibles (2026-09-14)** : fond crème‑blanc net `UITheme.Carte` (avant `#FBF5E8` quasi invisible sur le beige de la page), **cadre ambre franc `#C9A96A`** (outline 1,5 px), **liseré ambre à gauche qui suit les coins arrondis** (root = `#BA7517` RoundedRect + enfant `CardBg` crème inséré `offsetMin=(4,0)`, `ignoreLayout` — même principe que la carte « À traiter », au lieu d'une barre carrée qui dépassait des coins), label ambre foncé `#8A5E14`, rangée à 82 px ; les infos secondaires (acquis · terrain · cadastre · travaux · objectifs) passent sur une **ligne compacte** ; la **bande financière** du bas (redondante avec la tuile Cash flow) est masquée. L'en-tête identité (vignette + nom + pastilles + « Fiche complète ») est conservé.
- **2 KPI ajoutés (2026-09-14)** : **Dépensé / an** (service de la dette annuel = `loyers − cash-flow annuel`, en terracotta) et **Total gagné** (cash-flow net **cumulé depuis l'acquisition** = `cashFlowAn × AnneesDepuisAcquisition`, vert si positif) — pour compléter la lecture « par an » (loyers/dépensé) et « sur la globalité » (investi/gagné). Rangée à 6 tuiles : Loyers/an · Dépensé/an · Investi · Total gagné · Rendement net · Cash flow/mois.
- **Sections agrandies (2026-09-14)** : la **carte de présentation** (`CarteBatiment`) est agrandie dans `EnlargeKpisOnce` — **vignette 165×118 → 235×150**, **nom fs 24 → 30**, adresse ≥ 17, et la **hauteur de carte** (pilotée par son `LayoutElement` fixe) passée à **215**. La **liste des locataires** (`LocataireRowUI.Setup`) est agrandie — **lignes 50 → 64 px**, **nom fs 19 → 23**, **avatar 34 → 46**, initiales 16, sous-titre 13, loyer 16.
- **Correctif régul en retard (2026-09-12)** : `FacturationAlertes` regarde la **dernière échéance de régul déjà arrivée** (`DerniereOccurrence` mois/jour ≤ aujourd'hui) et pas seulement la prochaine → une régularisation **échue et non traitée** déclenche bien URGENT (avant : `ProchaineDateAnnuelle` sautait au futur, aucune alerte). Une régul concerne les charges de l'année N‑1 (échéance mois/jour de N). **Pas de garde-fou sur le début du bail** : un garde-fou « année de charges ≥ début du bail » avait été essayé mais il **cachait** l'alerte pour les baux récents (2026) → retiré. L'échéance de régul du Suivi reste en N+1 (« Régularisation des charges N » due en N+1) — donc l'onglet de l'année N l'affiche « À venir », la régul en retard (charges N‑1) est dans l'onglet N‑1 en « À faire ». ⚠ Après recompilation, **relancer le Play** pour que l'alerte apparaisse.

### 16.4 UI menu principal
2 colonnes **À traiter** | **Créances** + **Bâtiments** ; en-têtes de section uniformisés. Voir mémoire `project_home_creances_layout`.

### 16.5 UI fiche locataire
Réorganisée (croquis utilisatrice) : **Gauche** = General · Loyer · Dépôt ; **Droite** = Bail · Commentaire · Objectif · **Facturation** (4 boutons en grille 2×2) ; **bandeau Suivi** pleine largeur en bas. RIB compact, Pappers dans la boîte Siret, Email/Tél sur une ligne, **Loyer** refait en compact (tuile annuelle HT/TTC + mini-tableau période côte à côte avec le récap « Modalités du loyer »), 11 types de bail. Voir mémoire `project_vue_locataire`.
- **Pop-up « Loyer » scindé en 2 volets (2026-09-12)** : l'ancienne modale « Révision du loyer » (`RevisionPanel`, GameObject `Canvas/Loyer`) mélangeait facturation + révision → séparée en **deux menus** ouverts par **deux boutons dans la bande de titre « Loyer »** de la fiche :
  - **« Modalités »** (bouton secondaire ambre clair `#F4E7CD`/`#A9741C`, clone de « Réviser ») → **gestion du loyer / facturation** : périodicité · jour de demande · mois facturés · provisions + montant · date de régularisation. Titre « Gestion du loyer », habillage ambre, bouton **Enregistrer**.
  - **« Réviser »** (inchangé) → **révision du loyer** : type d'indice · loyer de départ · trimestre de référence · dates · trimestre de révision · calcul + résultat.
  - **Trimestre de révision non publié (2026-09-14)** : l'indice de révision (`trimestreVoulu`, « Trimestre de révision ») exige une **correspondance EXACTE** (`InseeIndiceService.TrouveExact`, pas de fallback — contrairement à l'indice de **départ** qui recule jusqu'à 5 ans). Bug corrigé : si le trimestre voulu n'est pas encore publié par l'INSEE (ex. T4 2026 demandé mi‑2026), `RevisionPanel.Reviser` faisait `yield break` **sans effacer** le résultat précédent → l'écran gardait l'ancien « Nouvel indice » (trompeur, ex. 2025‑Q1). Désormais on **efface** Nouvel indice / Loyer révisé / Variation (→ « — ») et on **propose le dernier trimestre réellement publié** (`InseeIndiceService.DernierPublie`, tri année×4+trimestre).
    - **Avertissement inline + bouton verrouillé (2026-09-14)** : le message ne s'affiche plus en bas (statut) mais **directement sous le champ « Trimestre de révision »** (label `TrimWarning` créé en code sous `SectionRevision`, rouge `#A32D2D`, `EnsureTrimWarn`/`AfficheTrimWarn`/`MasqueTrimWarn`). Et le bouton **« Réviser » est désactivé** quand le trimestre n'est pas valide : (1) **synchrone** dès le changement de trimestre via `trimestreVoulu.OnTrimestreChanged` → `ValiderTrimestreVoulu` — un trimestre **pas encore commencé** (futur, `TrimestreDejaCommence` = début de trimestre > aujourd'hui, ex. T4 2026) bloque immédiatement sans appel INSEE ; (2) **après fetch** si le trimestre (passé mais pas encore publié) est introuvable. Le bouton se **réactive** quand l'utilisatrice choisit un trimestre déjà commencé.
    - **Débordement du statut corrigé (2026-09-14)** : le texte de statut (`Statut`, ex. « Indice des Loyers Commerciaux — loyer révisé : 7 900,54 € (prochaine révision …) ») avait `enableWordWrapping=false` → il **débordait du cadre** sur une seule ligne. Forcé à `enableWordWrapping=true` (+ `overflowMode=Overflow`) à l'ouverture ; la boîte `Information` (VLG `childControlWidth`+`forceExpand`) borne la largeur, le texte passe donc sur 2 lignes dans le cadre.
  - Implémentation : **un seul panneau réutilisé**, `RevisionPanel.Open(loc, onSaved, Volet)` (`Volet.Revision` / `Volet.Modalites`) montre/masque les blocs de `Content/Body` selon le volet (`ApplyVolet`). Le bouton « Enregistrer » est un **clone de `btnReviser`** dans la rangée `Indices` (`SaveModalites`). Le 2ᵉ bouton fiche = `LoyerSummaryUI.EnsureModalitesButton` (le badge « Révision à faire » est frère du bouton Réviser → non dupliqué). Récap de la carte Loyer renommé « Facturation du loyer » → **« Modalités du loyer »**.
- **Cartes Dépôt / Facturation / Suivi = CLONES de la coquille d'une section de scène** (« Autre »), pour un visuel **strictement identique** aux sections de scène (General/Bail…) : liseré coloré à gauche (image racine) + `CardBg` crème + bande de titre arrondie (« titre ») + icône. On recolore, retitre, masque le `Toggle`, vide le `Content` et le remplit. Helper `CloneSection`. L'originale « Autre » reste masquée (porte `depotDeGarantieTxt` pour la sauvegarde).
- **Carte Dépôt** : 2 **tuiles KPI de taille identique** (Montant du dépôt ⟷ Équivalent en périodes de loyer, chiffre 24 + descripteur inline via rich-text `<size>`), fond des tuiles plus clair que le bandeau ; **bouton « Réviser »** = clone du bouton « Réviser » de Loyer (même sprite/forme, largeur 110) posé en overlay dans le bandeau ; badge « Révision à faire ». La **modale « Révision du dépôt de garantie »** (`OpenRevisionPopup`) a la **même structure que la modale « Révision du loyer »** (2026-09-14) : card sans padding, **bandeau PLEIN pleine largeur** en tête (fond ambre `MoneyAccent`, sprite `RoundedTop` pour les coins arrondis en haut, icône `sec_coins` + titre en **blanc**), puis un corps crème encadré (`Body` VBox pad 18) avec le formulaire.
- **Hauteur des bandeaux** forcée à **58** (= « General ») ; piège : le `titre` de scène est dimensionné par son `ContentSizeFitter` (ne pas le désactiver → il s'effondre) → forcer via `LayoutElement.preferredHeight`. Piège coroutine : la fiche est construite **inactive** → pas de `StartCoroutine`, normalisations synchrones.

### 16.6 Colorimétrie « groupée par fonction » (app-wide, 2026-09-11)
3 familles seulement (remplace la variété 1-couleur-par-section) : 🟢 **vert** #0F6E56 (clair #D6ECE3) = identité/contrat (General, Bail, Informations) · 🟡 **ambre** #A9741C (clair bande #F4E7CD, tuiles #FBF5E8) = argent (Loyer, Dépôt, Facturation, Suivi, Rentabilité) · 🔵 **gris-bleu** #5C6E85 (clair #E7ECF2) = notes (Commentaire, Objectif).
- Fiche locataire : `LocataireFacturationFields.ApplyColorScheme` (recolore les sections de scène : liseré = image racine, bande = `titre`, icône + texte = accent) ; les sections clonées sont colorées à la construction.
- **App-wide** : composant **`AppSectionColors`** (Assets/Script/General/, posé sur le **Canvas racine**, scène sauvegardée) recolore par nom→famille toute carte de section (image racine + enfant `titre`) → couvre la **fiche bâtiment** (Informations, Rentabilité, Objectif). Table de noms extensible.
- **Menu principal** (choix utilisatrice) : **Bâtiments** 🟢 vert · **Créances** 🟡 ambre · **À traiter** 🔵 gris-bleu — dans `GeneralMenuPanel.NormalizeHeaders/StyleHeader` + `FacturationHomeSection` (frame/HeadBar/icône/titre en ambre). `NormalizeHeaders` recolore aussi le **liseré** (Image racine de la carte « À traiter ») en gris-bleu `#5C6E85` — il était resté en ambre dans la scène (2026-09-14).
- Éléments internes de Loyer (LoyerSummaryUI) repassés en ambre (tuile annuelle/tableau, labels, bouton Réviser).
- **Icônes de section** uniformisées à **34 px** app-wide via composant **`AppIconResizer`** (Canvas). Sprites `sec_*` chargés au runtime via `Resources.FindObjectsOfTypeAll<Sprite>()` (pas d'AssetDatabase en Play). Dépôt = `sec_coins`, Facturation = `sec_file`, Suivi = `sec_chart`.
- Voir mémoire `project_design_system`.

### 16.7 Reste à faire
Réglage (RIB + entêtes + clé) ; **Voie 2 Pennylane** (upload brouillon `import_as_incomplete:true`, avec accord explicite) ; détection paiements ; nettoyage des données de test Pennylane ; réglage du **jour de demande du loyer** (par défaut 0 → échéances au 1er du mois dans le Suivi).

### 16.8 Diagramme de comportement de la facturation (référence utilisatrice, 2026-09-12)
Machine à états cible fournie par l'utilisatrice (schéma). Depuis `Ouverture app → liste des états → État` :
- **En attente (À venir)** → selon **type** : *Loyer* → à faire à échéance‑22 j · *Régul* → à faire à la date de régul · *Dépôt* → à la date de révision (crée alerte « à traiter » + pastille, l'utilisateur fait la révision) · *Refacturation* → créée à la main → directement **Envoyé**.
- **À faire** → alerte « à traiter » + pastille sur le bouton dédié → l'utilisateur crée la facture → si **loyer ET aujourd'hui ≤ échéance‑15 j → En attente d'envoi**, sinon **Envoyé**.
- **En attente d'envoi** → à **échéance‑15 j** → **Envoyé** (auto).
- **Envoyé** → crée une ligne dans créances ; à **échéance+15 j** → **Impayé** ; l'utilisateur peut **refaire** la facture → nouvelle facture *corrigée(X)* (X = nb de corrections), lien PDF mis à jour dans le suivi.
- **Payé** → si **location non commerciale** → possibilité d'émettre la créance de loyer.
- **Impayé** → crée une ligne dans créances + **option « envoyer un rappel d'échéance »** dans le suivi → l'utilisateur clique → **mail de rappel**.

**Implémenté (16.2)** : les transitions d'états (À faire/En attente d'envoi/Envoyé/Impayé) + seuils.
- **Refaire → *corrigée(X)*** ✅ (loyer, 2026-09-12) : champ `FactureEtat.corrections` ; `FacturationSuivi.EstDejaEmise` détecte une facture déjà émise (Envoyé/Impayé + PDF) et `MarquerCorrige` incrémente le compteur, suffixe le libellé **« — corrigée(X) »** (réappliqué par `Fusion` depuis le compteur), met à jour le lien PDF, **conserve le numéro** et le statut. Dans le suivi, le bouton devient **« Corriger »** pour un loyer émis/impayé → `FactureLoyerPanel.OpenLoyer(fiche, ligne)` force la période/année de la ligne et **ne consomme pas de nouvelle séquence**. Le PDF porte « {numéro} corrigée(X) ». **Extensible** aux Régul/Refac/Dépôt (mêmes helpers, à câbler dans leurs `SauvegarderEtEnvoyer`).

- **Impayé → « rappel d'échéance »** ✅ (2026-09-12) : bouton **« Rappel »** sur les lignes Impayé des deux vues du suivi (`LocataireSuiviInline` + `FacturationSuiviPanel`, colonne Actions élargie à 280). `FactureRappelService.Demander` ouvre une **confirmation** (`ConfirmDialog`) puis un **brouillon email `mailto:`** pré-rempli (sujet + corps reprenant libellé/n°/montant/échéance + `ReglageData.phraseRetard` + signature `smtp.fromName`) vers `loc.emailLocataire` → **aucun envoi automatique**, l'utilisatrice valide dans sa messagerie (même principe que `ContactLink`). Date mémorisée : `FactureEtat.dernierRappelISO` via `FacturationSuivi.MarquerRappel` (sur l'enregistrement **stocké**, pas la copie `Fusion`).

- **Payé (bail non commercial) → quittance de loyer** ✅ (2026-09-12) : sur une ligne **loyer Payé** dont le bail n'est **pas commercial**, bouton **« Quittance »** (2 vues du suivi) → `FactureQuittanceService.Emettre` génère un **PDF de quittance de loyer** en local (nouveau template `StreamingAssets/quittance_template.html`, style maison, via `FacturePdfService.GenerateQuittancePdf`) puis l'ouvre. Classement commercial/non commercial : `Locataire.EstBailCommercial` (commercial = Bail9ans/Bail10ans/Commercial369/9Ferme/Dérogatoire ; le reste = non commercial → quittance). Montant = TTC payé ; période déduite du libellé ; désignation = bâtiment + lot. Aucune émission réelle.

**Reste à faire pour coller au diagramme** :
- *Corrigée(X)* : étendre Régul/Refac/Dépôt (helpers déjà prêts, aujourd'hui câblé loyer).
- Quittance : affiner si besoin (mention TVA, montant en lettres, adresse du bien) ; ajuster la liste des baux « commerciaux » si nécessaire.
- **Envoi automatique réel** à J‑15 : **non** — seule la bascule d'**état** est automatique ; l'émission réelle (PDF/Pennylane/email) reste **manuelle** (contrainte projet).

### 16.9 Parcours d'initialisation & reprise de passif (conception validée 2026-09-12 · Phases 1 & 2 IMPLÉMENTÉES)
Objectif : rendre la facturation utilisable sur un portefeuille **existant** sans générer un faux backlog de factures « à faire » pour tout l'historique. Choix utilisatrice : **multi-entreprise (sélecteur)** · reprise = **dernière période facturée** · historique **affiché en gris « Clôturé »**.

**Constat clé** : une entreprise = **une racine de sauvegarde** (déjà le cas — `<SaveRoot>/reglage.json`, `<SaveRoot>/pennylane_secrets.dat`, `<SaveRoot>/batiments/…`). Le multi-entreprise = surtout un **sélecteur au lancement** + un registre des entreprises connues, pas une refonte.

**Parcours cible :**
1. **Premier lancement / sélecteur d'entreprise** : liste des entreprises connues (registre `PlayerPrefs`/JSON global : `[{nom, cheminRacine}]`) + « Créer une entreprise » (nom → dossier → assistant Réglage : RIB, mentions, logo) + « Ouvrir un dossier existant ». Sélection → `SaveLocationService.SetSaveRoot(chemin)` → recharge Réglage + bâtiments. Mémoriser la dernière entreprise active. Point délicat : **rechargement à chaud** des bâtiments au changement d'entreprise (sinon redémarrage).
2. **Créer un bâtiment** : inchangé.
3. **Créer un locataire → « Nouveau bail » ou « Bail repris »** : repris → sélecteur **« Dernière période déjà facturée »** (année + période selon la périodicité).
4. **Impayés d'ouverture (optionnel)** : « des loyers déjà facturés mais impayés à reprendre ? » → crée directement ces lignes en état *Envoyé/Impayé* (vraies créances d'ouverture).

**Phase 1 — reprise/passif — IMPLÉMENTÉE (2026-09-12, testée) :**
- Nouveau champ `Locataire.repriseFacturationISO` = **échéance de la dernière période déjà facturée** (vide = nouveau bail, tout suivi).
- Nouvel état `FacturationSuivi.Etat.Cloture` (« Clôturé ») + libellé. `FacturationSuivi.Lignes` marque en fin de calcul toute ligne d'échéance **≤ reprise** et **sans statut actif** → statut transitoire `"Cloture"` ; `EtatDe` le rend `Cloture`. Les impayés d'ouverture (statut réel Envoyé/Impayé posé à la main sur une période **après** la reprise) restent normaux.
- `FacturationAlertes.Pour` : helper `AvantReprise(loc, date)` → **aucune alerte** (loyer / régul / dépôt) pour une échéance ≤ reprise (règle aussi proprement le faux « régul en retard » d'un bail repris).
- `Dues`/Créances : `Cloture` non compté (transitoire, jamais stocké).
- **UI Suivi** (`LocataireSuiviInline` + `FacturationSuiviPanel`) : lignes `Cloture` **grisées** (texte + fond), **non actionnables** (pastille statique, aucune action).
- **Saisie** : dans le menu **Modalités** (`RevisionPanel` volet Modalités), sélecteur `UIDropdown` **« Bail repris — dernière période déjà facturée »** (« Aucune » + 4 ans de périodes selon la périodicité). Helpers `FacturationSuivi.EcheancePeriode/LibellePeriode`. Sauvé dans `SaveModalites`. ⚠ Relancer le Play après recompilation.
- **Impayés d'ouverture** = les périodes déjà envoyées mais impayées se placent **après** la reprise et se marquent « Impayé » via la pastille normale (pas de flux séparé).

**Phase 2 — multi-entreprise — IMPLÉMENTÉE (2026-09-12, compile OK) :**
- Une entreprise = **une racine de sauvegarde propre** (`…/CIPL_Saves` : reglage.json + batiments/…). Registre **global** dans `PlayerPrefs` (clé `cipl_entreprises`) : `EntrepriseService` (`All`, `Register`, `Oublier`, `EnsureActiveRegistered`, `Activer`, `Creer`, `Ouvrir`).
- **Bascule à chaud** : `Activer/Creer/Ouvrir` → `SaveLocationService.UseRoot`/`SetSaveRoot` + `ReglageService.Load()` + `BatimentManager.ReloadFromDisk()` + `menuManager.OpenGeneralMenu()` (retour au home rafraîchi). Nouveau `SaveLocationService.UseRoot(finalRoot)` (active une racine finale connue sans ré-ajouter `CIPL_Saves`).
- **UI** : `EntreprisePanel` (plein écran, code) = liste des entreprises (nom + chemin, « active »), **Ouvrir** (bascule), **Retirer** (du registre seulement, données conservées), **Créer** (nom → dossier), **Ouvrir un dossier existant**. Bouton **« Entreprises »** ajouté au pied du menu Home par `EntrepriseBootstrap` (`RuntimeInitializeOnLoadMethod`, clone de « Sauvegardes », comme `FacturationBootstrap`), qui enregistre aussi l'entreprise active au 1er lancement.
- **Nom d'entreprise** : `ReglageData.entrepriseNom` (éditable dans le **Réglage**, section « Connexion & envoi ») ; la sauvegarde du Réglage met à jour le registre.
- Bouton **« Charger une save »** **retiré** (`GeneralMenuPanel.Start` masque `btnSauvegardes` — après les bootstraps qui le clonent) : remplacé par **Entreprises → « Ouvrir un dossier existant »**. **« Changer l'emplacement »** (Réglage → `ChangeLocation`) met à jour le registre (oublie l'ancienne racine, inscrit la nouvelle). `SaveIO.LoadSave` reste (inscrit l'active) mais n'est plus câblé à un bouton.
- Reste optionnel : **assistant** de premier lancement (auto-ouverture du sélecteur si registre vide — non fait, jugé fragile côté timing `AfterSceneLoad`).
