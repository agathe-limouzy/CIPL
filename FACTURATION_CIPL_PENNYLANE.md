# Facturation CIPL × Pennylane — README de projet

> Fichier de référence vivant. **Mis à jour au fil de l'avancement** pour ne rien perdre entre les sessions.
> Dernière mise à jour : 2026-09-29 (dépôt Pennylane, lieu d'émission, charges par locataire, listes de charges, régul regroupée — voir §5, §13 « Charges : état par locataire… » et §16).

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
- **⚠️ Garder le visuel CIPL (relevé 2026-09-29, rien d'implémenté)** : `POST /customer_invoices` ci-dessus fait générer le PDF **par Pennylane, à son modèle** — pas le nôtre. Pour que le locataire reçoive le PDF CIPL : `POST /file_attachments` (notre PDF) puis `POST /customer_invoices/import` (`file_attachment_id`, `customer_id`, `date`, `deadline`, montants, `invoice_lines` ; somme des lignes = total, sinon 422). L'aide Pennylane mentionne une option `convert_to_e_invoice` : Pennylane génère le XML Factur-X et l'intègre au PDF importé. **Sans les données, un PDF importé reste un PDF simple, pas une facture électronique.** À confirmer avec Pennylane : transmission effective via la plateforme agréée, exigence PDF/A-3, e-reporting des paiements.
- **Mettre à jour** : `PUT /customer_invoices/:id`.
- **Récupérer le PDF** : champ **`public_file_url`** dans la réponse (lien PDF public).
- **Finaliser (émettre)** : `PUT /customer_invoices/:id/finalize` (brouillon → finalisée, plus modifiable). Non testé sur une facture **importée** `incomplete`.
- **Transmettre à la plateforme (PA)** — relevé doc 2026-09-29 : `POST /customer_invoices/:id/send_to_pa` (scope `customer_invoices:all`). Facture **finalisée** + « éligible e-invoicing » requises. 204 = soumise (asynchrone), 422 si déjà envoyée. Suivi : objet `e_invoicing` {`status`, `reason`} sur la facture (depuis le 09/03/2026) ou webhook `customer_invoice.e_invoicing_status_updated`. Pennylane est lui-même plateforme agréée.
  - Facture test import+Factur-X `28999504625664` relue le 2026-09-29 : toujours `incomplete`, `schematron_validation_status: pending` → normal : elle n'est jamais allée au bout (ni finalisée, ni envoyée). Pennylane ne documente pas si la validation se déclenche à la finalisation ou à l'envoi ; « pending » n'indique pas un problème de données.
  - **À confirmer avec Pennylane avant de coder** : une facture importée (`convert_to_e_invoice`) est-elle « éligible » à `send_to_pa` ?
- **IMPLÉMENTÉ 2026-09-29 — dépôt (sans émission), panneau Loyer uniquement** : `PennylaneClient.Deposer` (Assets/Script/Facturation/PennylaneClient.cs). En mode Pennylane, « Sauvegarder et envoyer » : client retrouvé par SIREN (`reg_no`) puis **remis à jour** (`PUT /company_customers/:id` : nom, adresse, email si renseigné — CIPL fait foi, rien à saisir dans Pennylane), sinon créé → `POST /file_attachments` (notre PDF) → `POST /customer_invoices/import` avec `import_as_incomplete:true` + `convert_to_e_invoice:true`. Le suivi n'est écrit qu'après un dépôt réussi et la ligne reste « en attente d'envoi » : **rien n'est émis**, finalisation et transmission se font à la main dans Pennylane. Refusé avant tout appel si : pas de clé, pas de SIRET (particulier → email), numéro invalide (ex. « corrigée(1) »), adresse sans code postal. TVA calculée sur le total HT comme sur le PDF ; la ligne de provision absorbe l'écart d'arrondi (tests : `PennylaneClientTests`).
  - Pas encore faits : finalize + `send_to_pa` (en attente de la réponse Pennylane), panneaux Régul / Refac / Dépôt, lecture du statut `e_invoicing`.
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
- **Locataire CIPL de test (2026-09-29)** : « TEST Pennylane — Volteo » dans l'entreprise **DemoCIPL**, bâtiment **Le Hangar** (SIRET Volteo → réutilise le client Pennylane TEST ci-dessus, trouvé par SIREN). À supprimer de CIPL après l'essai, avec la facture incomplète qu'il aura déposée.
- ✅ **Test de bout en bout depuis CIPL — 2026-09-29, RÉUSSI, non émis** : facture id `30794986868736`, n° `2026/2909001`, statut `incomplete`, `factur_x:true`, `e_invoicing:null`. Lignes et totaux exacts (HT 1 100 / TVA 220 / TTC 1 320), client TEST Volteo mis à jour (nom + adresse, email conservé). PDF = mise en page CIPL avec `factur-x.xml` embarqué, profil **EN 16931** ; vendeur = réglages société Pennylane, acheteur = fiche CIPL (SIREN inclus). ⚠️ NE PAS finaliser (SIREN du vrai Volteo). Échéance 01/09 < date 29/09 : normal ici (période de septembre déjà commencée).
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

### Arborescence de sauvegarde (fichiers) — IMPLÉMENTÉE 2026-09-16
```
<SaveRoot>/
  reglage.json               -> RIB, entêtes, nom d'entreprise (écriture atomique + .bak)
  pennylane_secrets.dat      -> clé API Pennylane, mot de passe SMTP (hors repo, hors backup)
  batiments/
    batiment_{id}.json       -> données (nommées par ID : stable aux renommages)
  Batiment/
    <Nom du bâtiment>/       -> nommé par le NOM, pas par l'ID
      Photos/                -> photos copiées ici, référencées en chemin RELATIF
      Charge/                -> PDF des charges
      <Nom du locataire>/
        Facture/             -> factures générées
  Backups/                   -> sauvegardes de démarrage + avant_migration_<date>
```
Le token Mapbox vit à part, au niveau application : `<persistentDataPath>/cipl_app_secrets.dat`.

**Nommage par nom** (`DossiersDonnees`) : les caractères interdits deviennent `-`, les points et espaces finaux sont retirés (Windows les supprime silencieusement), les noms réservés (`CON`, `COM1`…) sont préfixés, la longueur est bornée à 60 caractères pour rester sous la limite de 260 du chemin complet, et un nom vide devient « Sans nom ».

**Unicité imposée** : deux **bâtiments** ne peuvent pas porter le même nom (ils seraient au même niveau) ; deux **locataires** le peuvent s'ils sont dans des bâtiments différents, mais pas au sein du même bâtiment. La saisie est refusée et le champ revient à sa valeur précédente.

**Renommage** : `DossiersDonnees.RenommerBatiment` / `RenommerLocataire` déplacent le dossier et reportent les chemins de photos. Si le déplacement échoue — un PDF ouvert dans un lecteur suffit à verrouiller le dossier — le renommage est **annulé** plutôt qu'appliqué à moitié, sinon factures et photos resteraient orphelines sous l'ancien nom.

**Migration** (`MigrationDossiers`, au démarrage, une seule fois via le marqueur `.migration_dossiers_v1`) : convertit l'ancien format `Batiment/{idBatiment}/{idLocataire}/` et `Photos/{idBatiment}/`. Elle valide **tout** son plan avant de déplacer quoi que ce soit (collisions de noms, destinations occupées), sauvegarde dans `Backups/avant_migration_<date>`, et n'écrit son marqueur qu'en cas de réussite complète — un échec partiel est donc retenté au lancement suivant.

### Persistance des réglages — écriture atomique (2026-09-16)
`reglage.json` et `pennylane_secrets.dat` (RIB, entêtes, clé API, séquences) passent par `AtomicFile` (`Assets/Script/Save/AtomicFile.cs`) : écriture dans un `.tmp` puis bascule via `File.Replace`, qui archive la version précédente en `.bak`. Trois conséquences pour la facturation :
- `ReglageService.Save()` renvoie désormais un **`bool`** et journalise un échec au lieu de laisser croire à un enregistrement réussi.
- `ReglageService.Load()` distingue « fichier absent » (premier lancement, normal) de « fichier illisible » (anomalie). Sur fichier illisible il tente le `.bak`, et s'il n'y a rien de récupérable il **verrouille l'écriture** (`ChargementEchoue`) pour ne pas détruire l'original — auparavant un `catch` nu repartait d'un objet vide et le premier `Save()` effaçait définitivement RIB et entêtes.
- `WriteSecret` **abandonne** si les secrets existants sont illisibles : réécrire un dictionnaire incomplet aurait supprimé les autres clés (enregistrer le mot de passe SMTP effaçait la clé API Pennylane).

`reglage.json` est également inclus dans la sauvegarde de démarrage (`BackupService`), qui ne copiait auparavant que `batiments/`.

### Correctifs de revue de code (2026-09-16)
- **Avoirs — montants négatifs bloqués.** `FactureRegulPanel` (solde = charges − provisions) et `FactureDepotPanel` (complément = nouveau − ancien) pouvaient produire une facture à HT/TVA/TTC **négatifs**. Les totaux signalent désormais « trop-perçu (avoir) » / « remboursement (avoir) » et l'émission est **refusée** : un trop-perçu est un avoir, à établir hors facturation. *Si ce blocage gêne un cas réel, il peut être assoupli en simple confirmation.*
- **TVA/TTC cohérents.** TVA et TTC étaient dérivés indépendamment du solde (`solde*.2f` et `solde*1.2f`) : après arrondi d'affichage, HT + TVA ne redonnait pas toujours le TTC imprimé (écart d'un centime). Désormais HT est arrondi au centime, TVA en découle, et **TTC = HT + TVA**.
- **Échéances parsées en culture invariante.** `FacturationSuivi.TryEcheance` remplace `DateTime.TryParse` (culture courante) partout dans le fichier. Un échec de parse faisait retomber `EtatDe` sur « Envoyé » : la facture ne passait **jamais** Impayé et sortait des créances. `MarquerEnvoye` garantit en plus une échéance non vide sur toute ligne émise.
- **Token Mapbox sorti du dépôt.** Il était sérialisé en clair dans `Maps.prefab` (champ public de `TileLoader`) et commité depuis le commit initial. Il se saisit maintenant dans **Réglages → « Token Mapbox (cartes) »**. Il est rangé au niveau **application** — `mapbox_token` dans `<persistentDataPath>/cipl_app_secrets.dat` — et non dans le `pennylane_secrets.dat` de l'entreprise : c'est une clé de l'app, pas une donnée d'entreprise, sinon il faudrait la ressaisir à chaque bascule. La clé Pennylane et le mot de passe SMTP, eux, restent bien par entreprise. Le champ de l'inspecteur reste un repli de dépannage local. L'URL Mapbox n'est plus journalisée (elle contenait le token), et sans token configuré la requête n'est plus émise du tout : un message explicite remplace le 401 « Not Authorized » incompréhensible.
- **Point et virgule interchangeables dans toutes les saisies de montants (2026-09-16).** Les cinq panneaux (`ChargePanel`, `FactureLoyerPanel`, `FactureRegulPanel`, `FactureRefacPanel`, `FactureDepotPanel`) avaient chacun leur copie de `ParseF`, qui ne gérait que la virgule et **ignorait les espaces de milliers**. Elles délèguent maintenant toutes à `SaisieNumerique`, où « . » et « , » sont strictement équivalents : selon le clavier, le séparateur décimal est tapé au pavé numérique (point) ou à la frappe française (virgule), et les deux doivent donner le même montant. Quand plusieurs séparateurs sont présents, **seul le dernier est décimal** : `1.200,50`, `1,200.50`, `1 200,50` et `1 200.50` valent tous 1200,50 (les trois espaces — ordinaire, insécable, insécable étroit — sont retirés). Vérifié en exécution, 20 cas.
  **Conséquence à connaître** : un séparateur **unique** reste décimal des deux côtés, donc `1,200` = `1.200` = **1,2** et non 1200 — c'est le prix de la symétrie demandée, et c'est cohérent avec le fait que `0,500` doit valoir 0,5. Pour mille deux cents, taper `1200` ou `1 200`.
- **Facture de révision du dépôt : bloc explicatif du calcul (2026-09-17).** Un bloc s'imprime désormais **sous le titre** « Révision du dépôt de garantie », pour que le locataire puisse refaire le calcul sans nous appeler : titre « Base Loyer Annuel à compter du {date d'effet} », tableau **Loyer base / Indice base / Nouvel indice / Nouvelle Base Loyer** (trimestres en clair, « 3ème trimestre 2023 »), ligne « Soit par Mois », puis la règle du dépôt (« doit correspondre à *deux* termes de loyer H.T. ») et la somme. Implémenté dans `Facturation/ExplicationDepot.cs` — fonction **pure**, testée hors Play (16 tests) ; le template `facture_regul_template.html` reçoit le placeholder `{{EXPLICATION}}` et 4 styles ; `FactureDepotPanel.BuildData` le remplit via le champ `RegulData.explicationHtml`. Règles retenues :
  - **les données viennent de la fiche**, jamais d'une ressaisie : `loyerAnnuelPrecedent`, `loyerAnnuel`, `indiceImmoAuDepart`/`indiceImmoActuel` (lus par `LoyerHistoryService.IndiceValeur`/`IndicePeriode`, exposés pour l'occasion). Le bloc ne peut donc pas diverger du calcul réel ;
  - **sans révision de loyer enregistrée**, le tableau n'est pas imprimé (un locataire neuf n'a ni indice précédent ni loyer antérieur) — seule la partie dépôt apparaît ;
  - **le sens de la dette suit le signe** : complément positif → « vous nous devez … à régler par retour de courrier » ; négatif → « **nous vous devons** … somme qui vous sera remboursée », montant affiché en positif ; nul, ou sous le demi-centime pour absorber les arrondis `float` → « aucun ajustement n'est nécessaire » ;
  - H.T. ou T.T.C. suit `depotSurTTC`, le nombre de termes s'écrit en lettres jusqu'à douze, et le 1er du mois s'écrit « 1er ».
- **Complément négatif : le document est désormais ÉMIS (2026-09-17).** La garde d'avoir du dépôt (finding M6) refusait l'émission quand le nouveau dépôt était inférieur à l'ancien. Décision de l'utilisatrice : **on envoie notre propre document dans ce cas aussi**. La garde bloquante devient donc une **confirmation** — « ce document constate X € dus AU locataire, et non réclamés ; il consommera un numéro comme une facture » — puis l'émission suit son cours normal (numéro, séquence, suivi, PDF). `SauvegarderEtEnvoyer` est scindé en garde + `Emettre(d)` pour pouvoir reprendre après la confirmation, comme pour les autres flux confirmés de l'app. Le libellé du total suit le sens : **« Montant à vous rembourser »** au lieu de « Complément à régler ».
  - **Ce que cela implique comptablement** : un document constatant une somme due au locataire est un **avoir**, pas une facture. Il consomme un numéro de la même séquence, ce qui est cohérent avec la numérotation continue — mais à signaler à l'expert-comptable en même temps que **H1**, puisque les deux touchent la même séquence.
  - **Même traitement pour la régularisation de charges (2026-09-17)** : quand les provisions dépassent les charges réelles, le document est également émis après confirmation, au lieu d'être refusé.
- **Le sens de la somme se lit sur tout le document (2026-09-17).** Corriger le seul texte explicatif ne suffisait pas : la **phrase imprimée en gras sous les totaux** — « SOMME À NOUS RÉGLER LE … » — restait fausse sur un remboursement, alors que c'est la ligne la plus lue du document. Deux helpers partagés, dans `FactureEmission` :
  - `PhraseSomme(phrase, solde)` → « SOMME QUI VOUS SERA REMBOURSÉE » quand le solde est négatif. **Seule la phrase automatique est remplacée** : un texte saisi à la main par l'utilisatrice est respecté ;
  - `LibelleSolde(solde, libellePositif)` → « Montant à vous rembourser » au lieu de « Complément à régler » (dépôt) ou « Solde H.T. » (régularisation).
  
  Les deux panneaux (dépôt et régularisation) passent par ces helpers, donc le document ne peut plus se contredire d'une ligne à l'autre. Couvert par 4 tests (`FactureEmissionTests`), dont le cas « un arrondi au centime n'inverse rien » et « une phrase écrite à la main est respectée ».
- **La phrase proposée à l'écran était inerte à l'ouverture (2026-09-17).** `PhraseSomme` protège le **document imprimé**, mais le champ « Phrase de règlement » propose, lui, son défaut via `DefaultSomme()`. Ce défaut a bien été rendu sensible au signe du solde — sauf qu'il était calculé **trop tôt** : `LoadIntoUI` appelle `_autoSomme = DefaultSomme()` *avant* de remplir `_provisions` (régularisation) / `_ancien` (dépôt) et le sélecteur d'année. Le solde valait donc toujours 0 à l'ouverture, et sur un remboursement l'écran affichait « SOMME À NOUS RÉGLER LE … » pendant que le PDF imprimait « SOMME QUI VOUS SERA REMBOURSÉE ». Aucun chemin ne le recalculait ensuite, sauf à retoucher la date d'échéance à la main. Correctif : `RefreshSommeDefault()` est appelé **en fin de `RefreshTotaux()`** dans les deux panneaux — c'est le point de passage unique de tout ce qui change le solde (saisie des provisions ou de l'ancien dépôt, nombre de périodes, changement d'année, chargement de la fiche). La règle « une phrase saisie à la main est respectée » reste portée par la comparaison à `_autoSomme`, donc inchangée. **À valider en Play** : c'est du câblage d'UI, non couvert par les tests EditMode.
- **L'écran suit l'ordre du document imprimé — les 4 panneaux (2026-09-17).** Le RIB était le premier champ du formulaire alors qu'il s'imprime en dernier, et le « N° interne » venait après le numéro alors qu'il s'imprime au-dessus : le formulaire ne racontait pas le document. Les cartes suivent désormais les trois zones du PDF — **En-tête du document** (Date · Texte / N° interne · Format du n° + N° de facture) · **Contenu** (texte de présentation + aperçu) · **la carte propre au type** · **Règlement** (Date d'échéance · Phrase de règlement · RIB CIPL · case pénalités de retard) — encadrées par Destinataire et Options & envoi. Dans chaque carte, **le variable précède le stable** : date et montants changent à chaque facture, RIB et format de numéro une fois par an. L'échéance, la phrase qu'elle alimente et le RIB étant devenus voisins, la mention « (défaut de la phrase de règlement) » a été retirée du libellé de l'échéance — le lien se voit.
  - **Six cartes identiques dans les quatre panneaux**, seule la quatrième change : « Loyer facturé » · « Régularisation des charges » · « Charge à refacturer » · « Dépôt de garantie ». Dans le panneau Loyer, **« Période facturée » quitte l'en-tête pour rejoindre les montants** : elle s'imprime dans le corps du document et c'est elle qui détermine le loyer de la ligne. La case « pénalités de retard » quitte Options & envoi pour le pied de Règlement, dans les quatre.
  - **Nommage des couleurs aligné au passage** : `CoContenu` (taupe) et `CoReglement` (ambre) désignent le même rôle, donc la même teinte, dans les quatre fichiers. Le piège corrigé : `CoAmbre` valait `#854F0B` (ambre) dans Loyer mais `#7A5AA6` (**violet**) dans Refacturation — un même nom pour deux couleurs, dans des fichiers qu'on modifie toujours ensemble. Il s'appelle désormais `CoCharge` côté Refacturation.
  - **Aucune logique touchée**, aucun champ ajouté ni retiré : déplacement d'appels dans `Build()`. Compilation propre et **88 tests EditMode verts**. Le rendu lui-même se valide en Play — aucun test ne couvre la construction d'UI.
- **Protocole d'émission mutualisé — `FactureEmission` (2026-09-16).** Les quatre panneaux recopiaient la même séquence : `EstDejaEmise` → suffixe `corrigée(X)` sur le numéro ET sur le nom de fichier → génération → `MarquerCorrige` ou `MarquerEnvoye` + `factureSeq + 1`. Quatre copies, d'où H2 (garde sur un seul panneau) puis H2-bis (deux états sur quatre). La règle **« une facture déjà émise ne consomme jamais un second numéro »** vit maintenant dans un seul fichier. Deux points d'entrée, dans cet ordre impératif : `Preparer(loc, key, numeroPropose)` **avant** la génération (il fournit le numéro à imprimer et le suffixe du fichier, donc le PDF d'origine n'est pas écrasé), `Enregistrer(...)` **après** une génération réussie (sinon le suivi annoncerait un PDF inexistant). Vérifié : plus aucun panneau ne touche `EstDejaEmise`, `MarquerCorrige` ni `factureSeq`. Chaque panneau conserve son message propre via un paramètre optionnel. Couvert par `FactureEmissionTests` (7 tests).
- **Le réglage survit à l'émission (2026-09-21).** Vérifié plutôt que supposé : `SauvegarderEtEnvoyer` appelle `SaveFromUI()` puis persiste par `SaveAfterModifyToDoListLocataire()`, et les quatre panneaux **réécrivent tout ce qu'ils relisent** — relevé champ par champ entre `LoadIntoUI` et `SaveFromUI`. Le réglage reste donc sur la fiche **du locataire**, et la facture suivante le retrouve sans rien re-saisir. Seule exception, volontaire : `Enregistrer` remet `numeroId` à vide pour que la facture suivante propose le numéro suivant. Rien n'empêchait ça de régresser : `Enregistrer` étant le seul point qui modifie la `FactureInfo` après l'émission, un test y vérifie maintenant que RIB, entête, format, cases et textes en ressortent intacts.
- **Chemins des PDF stockés en relatif (2026-09-16).** `FactureEtat.pdfPath` et `ChargeBatiment.pdfPath` conservaient un chemin **absolu** (`C:\…\CIPL_Saves\Batiment\Immeuble Rivoli\Dupont\Facture\Loyer-…pdf`). Conséquence : changer d'emplacement de sauvegarde, migrer l'entreprise, ouvrir la sauvegarde depuis une autre machine ou **renommer un bâtiment ou un locataire** cassait le lien de toutes les factures déjà émises — le bouton « PDF » disparaissait des deux vues du suivi (`File.Exists` faux) alors que les fichiers avaient bien suivi le déplacement. `MarquerEnvoye` et `MarquerCorrige` passent maintenant par `DossiersDonnees.VersRelatif` ; la lecture se fait par le nouveau `FacturationSuivi.CheminPdf(ligne)` (`VersAbsolu`), qui **renvoie tels quels les enregistrements absolus antérieurs** — aucun n'est perdu. Attention pour toute évolution : ne plus jamais ouvrir `pdfPath` brut.
- **Quittances et PDF de charges rangés au bon endroit (2026-09-16).** La quittance partait dans `Batiment/<GUID bâtiment>/<GUID locataire>/Facture` et les justificatifs de charge dans `Batiment/<GUID bâtiment>/Charge` — deux arborescences GUID en parallèle de l'arborescence nommée, invisibles pour qui ouvre le dossier lisible d'un bâtiment, et non déplacées par un renommage. Les deux passent désormais par `DossiersDonnees.DossierFactures(nom, nom)` et `DossierCharges(nom)` — cette dernière existait depuis la standardisation de l'arborescence et n'avait jamais été branchée. Les PDF déjà copiés dans les anciens dossiers GUID ne sont **pas** migrés automatiquement.
- **Lancement d'Edge : arguments passés un par un (2026-09-16).** `FacturePdfService.RunEdge` assemblait sa ligne de commande par interpolation, en posant lui-même les guillemets autour de chemins venant des noms de bâtiment et de locataire et du dossier de sauvegarde. Ce n'était pas exploitable sous Windows (`UseShellExecute = false`, donc aucun shell ; et NTFS interdit le guillemet dans un chemin), mais la sûreté reposait sur une garantie du **système de fichiers** plutôt que du code. `ProcessStartInfo.ArgumentList` (vérifié disponible dans le Mono d'Unity) reçoit désormais un argument par entrée — c'est .NET qui cite. `ScreenshotArgs` renvoie un `string[]`, les trois appels `--print-to-pdf` aussi ; aucune signature publique ne change. Vérifié : PDF réellement produit par Edge dans un chemin contenant espaces, `&`, `%`, apostrophe, `#` et parenthèses.
- **Migration d'entreprise non destructive.** `SaveLocationService.MigrateData` refuse désormais de migrer vers un dossier déjà peuplé (il écrasait le `reglage.json` d'une autre entreprise), copie **tous** les fichiers — dont `pennylane_secrets.dat` et les photos, auparavant laissés derrière — et renvoie un `bool`. En cas d'échec, l'ancienne racine n'est plus retirée de la liste des entreprises.

### Textes de facture : réglables dans le panneau, et avec variables (2026-09-21)

**Ce qui se règle où.** La frontière, posée par l'utilisatrice : **restent dans Réglages** les textes imprimés à l'identique sur tous les documents — phrase de retard, bas de page — ainsi que les RIB et les entêtes ; **vivent sur la facture** les textes propres à un document. Cas intermédiaire depuis le 2026-09-29 : les **mentions TVA** ont leur phrase de base dans Réglages et peuvent être remplacées facture par facture.

| Texte | Où on le modifie | Case pour le mettre ou non |
|---|---|---|
| Mention TVA (débits / encaissements) | **base** : Réglages → *Textes fixes* ; **remplacement** pour une facture : les 4 panneaux, carte du type | menu : *Aucune mention* / débits / encaissements (2026-09-29) |
| « Suite à votre demande, le montant mensuel… » | panneau Loyer, carte *Loyer facturé* | oui, existante |
| Phrase de règlement | les 4 panneaux, carte *Règlement* | — (toujours imprimée) |
| Les 4 phrases de l'explication du dépôt | panneau Dépôt, carte *Dépôt de garantie* | choix par le signe du solde |

**La mention « TVA payée sur les débits » est disponible sur les quatre types** (2026-09-21). Elle était impossible sur le dépôt, pour une raison qui n'était pas un choix : `TotauxBlock` testait `d.tvaDebit && !d.masquerTva`, et le dépôt masque les lignes TVA/T.T.C. La condition liait donc la **mention** au **tableau**. Elle ne teste plus que `d.tvaDebit` — `masquerTva` gouverne les lignes, la case gouverne la mention. Le tableau du dépôt reste sans TVA ni T.T.C. : c'est la mention qui devient disponible, pas la TVA.

**Sa formulation aussi est modifiable, sur les quatre** (2026-09-21). Elle était codée en dur à deux endroits : `BuildHtml` (Loyer, Refacturation) et `TotauxBlock` (Régularisation, Dépôt). `RegulData` porte maintenant `texteTvaDebit` comme `Data`, et les deux passent par `FacturePdfService.Texte(...)` — donc un champ vide retombe sur le texte d'usine et un document antérieur sort inchangé. Les quatre panneaux ont le même câblage : case, texte pré-rempli, menu « / », variables résolues, valeur sauvegardée sur le locataire et relue à l'ouverture.

**Pourquoi la mention ne dépend pas du calcul** : « la TVA est payée sur les débits » est une **mention souvent obligatoire sur la facture**, indépendante du fait qu'une TVA soit calculée ou affichée. La lier à la présence des lignes TVA était donc une erreur de raisonnement, pas seulement une gêne. Sur le dépôt, elle est **cochée par défaut** et c'est voulu : le document peut la porter sans qu'aucune TVA n'y figure.

**Mention TVA : un choix, une base dans les Réglages, un remplacement par facture** (2026-09-29). La case « Ajouter la mention « TVA payée sur les débits » » est remplacée, dans les quatre panneaux, par un menu *Mention TVA* : **Aucune mention** · **TVA payée sur les débits** · **TVA payée sur les encaissements**. Le taux (20 %) n'en dépend pas — seule la phrase change.
- **Base** : Réglages → *Textes fixes* porte une phrase par mention (`ReglageData.mentionTvaDebits`, `mentionTvaEncaissements` ; d'usine « la TVA est payée sur les débits / encaissements »). Un `reglage.json` antérieur n'a pas ces clés → texte d'usine ; une base vidée y retombe aussi.
- **Remplacement** : sous le menu, la phrase est pré-remplie avec la base et reste modifiable. **Seul un vrai remplacement est gardé sur la facture** (`FactureInfo.texteTvaDebit`, vide = base) : une facture non retouchée suit donc toute modification ultérieure de la base.
- **Changer de mention** remet la base de la nouvelle mention : garder « …sur les débits » sous « encaissements » imprimerait le contraire du choix. *Aucune mention* masque la phrase.
- **Stockage du choix** : `tvaDebit` garde son sens historique « mention imprimée » ; nouveau `tvaEncaissements` dit laquelle. Une facture antérieure (sans cette clé) sort à l'identique : case cochée → débits, décochée → aucune.
- **Factures existantes** : l'ancien panneau enregistrait la phrase pré-remplie telle quelle, donc toutes portent le texte d'usine. Ce texte n'est **jamais** traité comme un remplacement — sans quoi elles resteraient figées sur l'ancienne formulation, sourdes aux Réglages. Limite connue : une facture ne peut pas imposer le texte d'usine quand la base a changé (un drapeau « remplacée » le permettrait si le besoin vient).
- **Pennylane** (rien d'émis) : `special_mention` recevra la phrase effectivement imprimée (`MentionTva.PhraseFacture`), vide pour *Aucune mention*.
- Code : `MentionTva` (choix, base, remplacement, construction du menu commune aux quatre panneaux). Couvert par `MentionTvaTests` (10 tests : factures et réglages antérieurs, aller-retour JSON des trois choix, base vidée, base suivie / remplacement conservé, texte d'usine non compté comme remplacement, héritage du type).

**Autres mentions TVA, non proposées** : exonération (« Exonération de TVA, art. 261 D du CGI » — location nue sans option), franchise (« TVA non applicable, art. 293 B du CGI »), autoliquidation. Toutes vont avec une facture **sans TVA** ; or le taux est figé à 20 % : les proposer imprimerait une exonération sous une ligne « TVA 20 % ». À ajouter avec un taux par locataire, pas avant. Seule la mention **débits** est légalement exigée quand l'option est prise (CGI, art. 242 nonies A) ; « encaissements » est le régime par défaut d'un loyer, la mentionner est permis mais pas requis.

**Les cases ont rejoint la ligne qu'elles commandent.** « TVA sur les débits » et « montant mensuel » étaient rangées dans *Options & envoi*, à l'autre bout du formulaire : on ne pouvait pas deviner où décocher « Suite à votre demande… ». *Options & envoi* ne garde que l'envoi. Règle qui vaut pour la suite : **ce qui commande une ligne du document vit dans la carte où cette ligne s'imprime.**

**Restent figés, volontairement** : les libellés du tableau (`Total H.T.`, `TVA 20%`, `Total T.T.C.`, `Euros`) et le bloc RIB. Ce sont des étiquettes comptables, pas de la rédaction — les rendre libres permettrait d'annoncer « TVA 10 % » sur un document qui en calcule 20.

**Variables dans ces textes.** Tous les champs libres de facture sont branchés sur le menu « / » (`SlashAutocomplete`) **et** passent par `FactureVarResolver`, résolu dans `BuildData()` de chaque panneau — là où l'entête l'était déjà. L'ordre compte pour la régularisation et le dépôt : la résolution vient **après** `FactureEmission.PhraseSomme`, qui peut substituer sa propre phrase selon le signe du solde.

Le piège à ne jamais rouvrir : **brancher un champ au menu sans le résoudre**. L'utilisatrice insère `{loc.nom}` en confiance et le jeton s'imprime tel quel sur un document envoyé au client. `FactureVariablesTests` le verrouille — il parcourt le catalogue et échoue si une variable proposée par le menu ressort inchangée. Les deux groupes résolus ailleurs (`{depot.*}` par `ExplicationDepot`, `{montant}` par `FacturePdfService`) sont exclus explicitement, et un test vérifie que leur groupe l'annonce.

Le champ affiche toujours le **modèle**, jamais le texte résolu : c'est ce qui permet à `RefreshSommeDefault` de comparer la saisie à `_autoSomme` pour savoir si la phrase a été personnalisée.

### Cycle de vie d'une charge : trois états, pas deux (2026-09-21)

L'émission faisait passer les charges directement en « payé ». Le bâtiment affirmait donc avoir encaissé une somme jamais reçue.

```
Impayé  ──(facture émise)──>  En attente de paiement  ──(facture marquée « Payé »)──>  Payé
                                       ▲                                                │
                                       └────────(facture remise en Impayé / Envoyé)─────┘
```

`ChargeBatiment.factureeISO` (date de mise sur facture, `yyyy-MM-dd`) porte l'état intermédiaire ; `paye` garde son sens — le virement est arrivé. `EstFacturee`, `EnAttentePaiement` et `Etat` évitent que les écrans divergent sur la lecture de ces deux champs.

- **Sélection** : les panneaux Régularisation et Refacturation excluent `paye || EstFacturee`. Une charge déjà facturée n'est plus proposée, payée ou non — c'est ce qui empêche la double facturation.
- **`factureeISO` n'est jamais effacée**, même si la facture repasse en impayé : elle a bien été émise.
- **Affichage** : trois états dans la liste des charges, l'intermédiaire en ambre.

**La bascule en « payé » est automatique** : marquer une régularisation ou une refacturation « Payé » dans le suivi encaisse ses charges ; l'inverse les remet en attente, ce qui permet de corriger une validation faite par erreur. La règle vit dans **`FacturationSuivi.SetStatut`**, point de passage unique des deux vues du suivi — la dupliquer chez elles aurait reproduit H2, où une garde n'existait que dans un panneau sur quatre. `SetStatut` reçoit pour cela le `Batiment` en paramètre optionnel (les charges y vivent) ; sans lui, la répercussion n'a simplement pas lieu.

Le lien facture → charges : la clé de suivi porte l'id de la charge en refacturation (`refac-<id>`), l'année en régularisation (`regul-<année>`, charges de cette année concernant ce locataire). Loyer et Dépôt n'ont aucune charge liée.

*Migration non faite* : les charges marquées « payées » par l'ancien comportement le restent. Impossible de distinguer celles que l'application a marquées à tort de celles réellement encaissées — à corriger à la main si besoin.

### Un seul réglage décide du mode d'envoi (2026-09-21)

Chaque panneau portait une case « Envoyer par email (au lieu de Pennylane) », **décochée par défaut**, qui doublait le mode d'envoi global des Réglages. Deux réglages pour une seule décision, dont l'un ignorait l'autre : le mode global pouvait être sur « Email » sans qu'aucun mail ne parte, et rien ne l'expliquait.

La case et le champ `FactureInfo.envoiEmail` sont supprimés. **`ReglageData.modeEnvoi` décide seul.** Les panneaux gardent ce qui leur appartient : adresse du destinataire, objet et corps du message. Quand le mode est Pennylane, le message de fin le dit et renvoie vers Réglages → Connexion & envoi.

### CRITIQUE — le réglage des factures n'était jamais enregistré (2026-09-21)

**Symptôme** : case « Envoyer par email » cochée, et pourtant aucun email. Plus largement : l'impression de devoir tout re-régler à chaque fois.

**Cause** : `[Serializable]` n'était **pas sur `FactureInfo`**. L'attribut avait glissé sur `FactureNumerotation` — une classe `static`, où il ne sert à rien — parce que celle-ci avait été insérée entre le commentaire de `FactureInfo` et son attribut :

```csharp
/// État mémorisé du menu « Information Facture »…   ← commentaire de FactureInfo
[Serializable]                                       ← son attribut
/// Formats de numérotation…                         ← insertion
public static class FactureNumerotation              ← qui capte l'attribut
```

Syntaxiquement valide : **aucun avertissement du compilateur**.

**Portée** : `JsonUtility` ignorait les quatre champs `factureLoyer / factureRegul / factureRefac / factureDepot` du locataire. Constaté sur le fichier de sauvegarde réel — il contenait `factureSeq` et `facturesEtat`, et **aucune** de ces quatre clés. Tout le réglage (RIB, entête, format de numéro, cases, textes, adresse d'envoi) tenait en mémoire puis disparaissait dès que les données repassaient par le JSON, ce que font les prefabs en permanence. Aucune erreur, aucune trace.

**Correctif** : attribut remis sur `FactureInfo`, avec un commentaire expliquant pourquoi il est vital et comment il avait dérivé. Couvert par `FactureInfoSerialisationTests` : un aller-retour JSON doit rendre les réglages intacts, et les quatre clés doivent apparaître dans le texte produit — c'était exactement le symptôme.

**Leçon** : un attribut mal placé ne casse rien de visible. Ni la compilation, ni les tests d'alors, ni l'usage immédiat — seule la relecture du fichier révélait la perte. La sérialisation d'un modèle mérite son test, au même titre qu'une règle métier.

### L'échéance du loyer suit le jour de demande (2026-09-22)

Le panneau Loyer proposait une échéance à **date de facture + 30 jours**. Le suivi, lui, calcule l'échéance d'une période avec `loc.jourDemandeLoyer` — « le loyer est demandé le X ». La facture imprimée et sa ligne de suivi portaient donc deux dates différentes, et la phrase « SOMME À NOUS RÉGLER LE … », qui découle de l'échéance, annonçait une date arbitraire.

La règle est désormais unique : `FacturationSuivi.EcheanceLoyer(loc, année, période)` — le jour de demande posé sur le mois d'échéance de la période, borné à la longueur réelle du mois (« le 31 » en février tombe le 28, ou le 29 en année bissextile). Le suivi et le panneau l'appellent tous les deux. L'échéance proposée se recalcule au changement de période ou d'année, tant qu'elle n'a pas été saisie à la main ; une échéance mémorisée sur une facture déjà réglée est respectée.

Les trois autres panneaux (Régularisation, Refacturation, Dépôt) gardent le défaut à +30 jours : ils ne facturent pas une période récurrente, il n'y a pas de « jour de demande » à suivre.

### La ligne cliquée dans le suivi ouvre la bonne période (2026-09-22)

Constaté à l'usage : cliquer « Refaire » sur le loyer de **mars** ouvrait le panneau sur **avril**. Le paramètre transmis au panneau s'appelait `correctionTarget` et n'était passé que par « Corriger » — « Générer » et « Refaire » envoyaient `null`, laissant le panneau retomber sur la dernière période mémorisée (`FactureInfo.moisPeriode`). On croyait éditer une facture, on en éditait une autre.

Le paramètre est renommé **`ligneCiblee`** : il désigne la ligne cliquée dans **tous** les cas, et sert uniquement à ouvrir la bonne période. Ce que devient la facture — nouvelle, remplacée ou corrigée — se décide ailleurs, dans `FactureEmission.Preparer`, d'après son statut réel. Le nom trompeur était la cause du bug : il décrivait un cas particulier, donc on remettait naturellement `null` pour les autres boutons.

Aligné sur les trois types qui ont une cible :

| Type | Clé de suivi | Ce que la ligne impose |
|---|---|---|
| **Loyer** | `loyer-{année}-P{n}` | la période **et** l'année ; l'échéance et la date d'envoi se recalculent |
| **Régularisation** | `regul-{année}` | l'année régularisée |
| **Refacturation** | `refac-{idCharge}` | la charge refacturée |
| **Dépôt** | `depot-{année}` | rien : l'année vient de la date de révision de la fiche, le panneau n'a pas de sélecteur |

**Piège traité** : les listes de choix **écartent ce qui est déjà facturé** — `YearsAvailable()` saute les charges `EstFacturee`, `ImpayeesCharges()` ne garde que les impayées. La cible d'une facture qu'on veut refaire en avait donc précisément disparu, et le panneau serait retombé sur une autre année ou une autre charge. Elle est réinsérée dans la liste quand elle manque ; si la charge a été supprimée, la cible est abandonnée plutôt que d'ouvrir la mauvaise.

**Second piège** : l'identifiant d'une charge est un GUID, il contient des tirets. La clé se découpe donc par **préfixe**, jamais par `Split('-')` — qui n'aurait rendu que le premier fragment, donc une charge introuvable. Vérifié par mutation : le test échoue avec `Expected "3f2a1b4c-8d90-…", But was "3f2a1b4c"`.

### Corriger une facture jamais partie la REMPLACE (2026-09-22)

Corollaire du point précédent : puisqu'une facture peut désormais attendre son jour d'envoi, on peut vouloir la retoucher entre-temps. Or « Corriger » produisait une **rectificative** — numéro « 2026/09001 corrigée(1) », fichier `-corrigee1.pdf`, libellé suffixé — alors que **personne n'avait reçu la version d'origine**. Une facture rectificative sans facture émise est un document faux, et le suivi se retrouvait avec deux PDF pour une seule facture.

Le protocole distingue maintenant **trois** cas au lieu de deux :

| Cas | Numéro | Fichier | Séquence | Statut |
|---|---|---|---|---|
| **Neuve** | proposé | nom normal | +1 | selon envoi réel |
| **Réécriture** — préparée, jamais partie | **le même** | **le même, écrasé** | **inchangée** | reste « en attente d'envoi » |
| **Correction** — réellement partie | « … corrigée(X) » | `-corrigeeX.pdf` | inchangée | conservé |

La réécriture remplace donc la version en attente : nouveau montant, nouveau PDF, même numéro — et c'est **elle** qui partira à la date prévue. La séquence n'avance pas : le numéro avait déjà été consommé à la première génération, l'avancer une seconde fois creuserait un trou dans la numérotation.

La garde H2‑bis est intacte : une facture en attente d'envoi compte toujours comme **émise**, donc un second clic ne prend jamais un numéro neuf. Ce qui change n'est pas le numéro, c'est ce qu'on fait du document — remplacer au lieu de rectifier.

*Vérifié par mutation* : en supprimant la détection de réécriture, deux tests tombent (« corrigée(1) » réapparaît) ; en laissant la séquence avancer, un troisième signale `Expected: 2, But was: 3`.

### Le panneau Loyer n'envoie plus en avance (2026-09-22)

Constaté à l'usage : une échéance au 8 octobre, un clic sur « Sauvegarder et envoyer » le 22 septembre — et la facture partait **sur-le-champ**, alors que sa date d'envoi était le 23. Le bouton expédiait sans consulter le calendrier : celui-ci n'alimentait que les rappels et l'envoi groupé. Préparer une facture un mois à l'avance l'envoyait donc un mois à l'avance, sans retour possible.

**Le calendrier fait loi** (décision de l'utilisatrice, 22/09) : avant la date d'envoi, le bouton change de libellé — « **Enregistrer pour l'envoi du 23/09** » — et un panneau confirme le jour exact du départ avant d'enregistrer. La facture est générée, numérotée, et reste « en attente d'envoi » ; elle sera proposée au lancement le jour venu.

Règle partagée : `FacturationSuivi.DateEnvoi(type, échéance)` — échéance − 15 j pour le **loyer seul**. Une régularisation, une refacturation ou une révision de dépôt s'envoient quand on les fait : elles n'ont pas de date d'envoi, donc rien ne les retient. `PeutPartir(type, échéance, jour)` en dérive, et une facture **en retard** part toujours.

Pour envoyer malgré tout avant la date, il reste à avancer l'échéance, ou à passer par « Corriger » depuis le suivi le jour de l'envoi.

### Envoi groupé proposé au lancement (2026-09-22)

Corollaire direct du point précédent : maintenant qu'une facture reste « en attente d'envoi » tant qu'elle n'est pas partie, l'application sait exactement ce qui doit partir. `FactureEnvoiAuto` le propose au démarrage, après le chargement des bâtiments (`BatimentManager.Start`).

**Ce qui est proposé** — une ligne est retenue si elle réunit les cinq conditions : statut « en attente d'envoi » · date d'envoi atteinte (échéance − 15 j, la même règle que l'alerte URGENT) · PDF présent sur le disque · destinataire connu (`FactureInfo.emailDest`, sinon l'adresse de la fiche) · échéance lisible. Les **retards sont rattrapés** : l'application doit être ouverte pour que quoi que ce soit parte, ne pas rattraper donnerait le pire des deux mondes — ni rappel, ni envoi.

**Ce qui n'est pas fait** : *rien ne part tout seul*. Un envoi ne se rappelle pas, donc la fenêtre liste les factures (locataire · bâtiment · libellé · destinataire · montant), chaque ligne se décoche, et « Plus tard » est toujours disponible. Une seconde confirmation récapitule le nombre exact et les destinataires avant le départ. C'est le choix de l'utilisatrice (22/09) face à un envoi réellement automatique : le travail manuel disparaît, la relecture non.

**Pendant l'envoi** : en série, une facture après l'autre. Chaque succès marque la ligne « Envoyé » et **sauvegarde immédiatement** — si l'application s'arrête au milieu, ce qui est parti reste marqué parti. Un échec laisse la ligne en attente, elle repassera au lancement suivant ; le bilan indique le nombre d'envois et la dernière erreur.

Le statut est mis à jour **directement** (`statut = "Envoye"`), sans repasser par `FactureEmission` : le PDF existe et le numéro est déjà consommé, il n'y a rien à réémettre — y passer prendrait une nouvelle séquence.

**Garde-fous** : ne demande rien si l'email n'est pas configuré (`EmailService.CeQuiManque`), ni si l'envoi a été reporté le jour même — relancer l'application dix fois ne doit pas poser dix questions (mémorisé en `PlayerPrefs`, c'est un confort d'écran propre au poste, pas une donnée métier).

**Limites connues** : l'application doit être lancée pour que l'envoi soit proposé · seul l'email existe (aucun appel à l'API Pennylane dans le code) · l'adresse `@cipl.fr` reste bloquée côté Microsoft 365, l'envoi part donc du compte configuré.

### « Envoyé » veut dire envoyé (2026-09-22)

Le statut d'une facture se déduisait de la **date**, pas du fait : une facture préparée à plus de 15 j de l'échéance était « En attente d'envoi », et `EtatDe` la basculait ensuite toute seule en « Envoyé » à J‑15. Le suivi annonçait donc des envois qui n'avaient jamais eu lieu — y compris quand l'envoi par email n'était même pas activé, cas où le panneau affichait pourtant « rien n'a été émis ».

Désormais le statut suit le fait : `FacturationSuivi.MarquerEnvoye(..., envoyeReellement)` et `FactureEmission.Enregistrer(..., envoyeReellement)` reçoivent l'information des quatre panneaux, qui la connaissent déjà — ils distinguent depuis septembre le chemin « PDF seul » du chemin « envoi réussi » (`EnvoyerPuisFinaliser` n'enregistre qu'après acquittement du serveur).

| Situation | Statut |
|---|---|
| PDF généré, envoi non demandé ou email non configuré | **En attente d'envoi** |
| PDF généré, envoi email accepté par le serveur | **Envoyé** |
| Envoi échoué | rien n'est enregistré (inchangé) : même numéro, même PDF au prochain essai |
| Facture partie autrement (Pennylane, courrier) | l'utilisatrice force « Envoyé » par la pastille du suivi |

Trois conséquences, toutes voulues :

- **« En attente d'envoi » ne se périme plus.** L'état ne devient « Envoyé » que par un envoi réel ou un forçage manuel.
- **Une facture jamais envoyée ne devient jamais « Impayée ».** On ne peut pas reprocher un impayé à qui n'a pas reçu sa facture.
- **L'alerte reste allumée tant que la facture n'est pas partie** : `DejaTraite` ne compte plus « En attente d'envoi » comme traité — c'est même le moment où le rappel est le plus utile. À ne pas confondre avec `EstDejaEmise` (garde anti-double-numéro), pour qui « en attente d'envoi » compte toujours comme émise : le PDF existe et le numéro est consommé. Deux questions différentes, deux réponses différentes — H2‑bis reste couvert par ses tests.

**Correctif du 22/09 au soir** — le message affiché se contredisait : après un envoi réussi, le bandeau annonçait « Rien n'a été envoyé » suivi de « et envoyée à … ». Le message par défaut de `Enregistrer` était renvoyé à l'identique dans les deux cas. Le comportement, lui, était correct — mail parti, ligne marquée « Envoyé » ; seul le texte mentait, ce qui est presque pire sur un envoi irréversible. Le message dépend désormais de `envoyeReellement`, et trois tests le verrouillent (dont « ne doit pas contenir *rien n'a été envoyé* quand l'envoi a réussi »).

Au passage, le calendrier (15 j d'envoi, +1 semaine de préparation) était écrit **trois fois** : dans `FacturationSuivi`, dans `FacturationAlertes`, et une troisième fois en dur dans `Lead("Loyer") = 22`. Une seule source désormais : `EnvoiAvantJours` et `RappelAvantEnvoiJours`, dont `Lead` est la somme.

### Calendrier du loyer : une seule règle, du rappel à l'impayé (2026-09-22)

Règle confirmée par l'utilisatrice, et désormais portée par un seul calcul :

| Moment | Ce qui se passe | Constante |
|---|---|---|
| **J‑22** (15 j + 1 semaine) | la ligne passe « À faire » et l'alerte « Loyer à préparer : envoi attendu avant le … » s'affiche | `Lead("Loyer") = 22`, `LoyerAttentionLead = 7` |
| **J‑15** | alerte URGENT « à envoyer avant le … » — c'est la date d'envoi par mail ou Pennylane ; une facture préparée d'avance passe de « En attente d'envoi » à « Envoyé » | `LoyerEnvoiAvant` / `EnvoiAvantJours = 15` |
| **J** | échéance = le « loyer demandé le X » (`jourDemandeLoyer`) | — |
| **J+15** | « Impayé » si non réglé | `ImpayeApresEcheanceJours = 15` |

Le mécanisme existait déjà ; deux défauts l'empêchaient de fonctionner dans des cas réels, tous deux nés de la **même règle écrite à deux endroits** (le suivi et les alertes) :

- **Aucun rappel quand le jour de demande n'était pas renseigné.** `ProchaineEcheanceLoyer` abandonnait (`return null`), alors que le suivi plaçait l'échéance au 1er du mois : des loyers à échéance s'affichaient dans le tableau sans qu'aucune alerte ne les accompagne — précisément sur les fiches au réglage incomplet, les plus exposées à l'oubli.
- **Les mois de facturation cochés étaient ignorés par le suivi.** `moisFacturationLoyer` est saisi dans la révision de loyer (cases des 12 mois) et affiché dans le résumé, mais `FacturationSuivi` imposait le calendrier standard : janvier/avril/juillet/octobre pour un trimestriel. Sur un bail facturé en février/mai/août/novembre, l'alerte annonçait le 05/02 et le tableau le 05/01.

Les mois cochés commandent désormais les échéances (`FacturationSuivi.MoisEcheance(loc, période)`, triés, dédoublonnés, bornés à 1‑12), et `PeriodeIndex(loc, mois)` en est l'inverse exact — sans quoi la clé `loyer-{année}-P{n}` des alertes ne désignerait plus la ligne du suivi. Les alertes, la période proposée à l'ouverture du panneau Loyer et la période de reprise passent toutes par ce calcul. **Sans objet en mensuel** : les douze mois sont facturés de toute façon.

*Décision prise avec l'utilisatrice le 22/09* : les mois cochés font foi. Conséquence assumée — sur les baux non mensuels, l'échéance des loyers **non encore facturés** se déplace vers les mois choisis ; les factures déjà émises conservent l'échéance enregistrée sur leur PDF.

### Le suivi de facturation se met à jour sans quitter l'application (2026-09-22)

Sur une fiche fraîchement créée, le tableau de suivi restait vide jusqu'au redémarrage. Deux causes :

- la section est construite **avant** que le locataire entre dans `listLocataire` : le bandeau se masquait, et rien ne le rallumait — le locataire n'était jamais relu ;
- régler le **loyer** (`RevisionPanel`) ou le **dépôt** (pop-up de révision) ne rafraîchissait rien : seuls les panneaux de facture appelaient `RefreshFor`, alors que ces deux réglages décident du contenu du suivi (montants, et la ligne « Révision du dépôt de garantie » n'apparaît que si `depotDeGarantie > 0` avec une date de révision).

`LocataireSuiviInline.RefreshTous()` est maintenant appelée depuis `BatimentPrefab.SaveAfterModifyToDoListLocataire()`, point de passage unique des 18 chemins qui modifient un locataire — donc tout enregistrement met le suivi à jour, quel que soit l'écran d'où il vient.

### Héritage du réglage de facture : une chaîne de locataire en locataire (2026-09-18)

**Le problème** : créer un second locataire obligeait à re-régler les quatre types de facture un par un — RIB, modèle d'entête, format du numéro, cases à cocher — alors que ces choix ne dépendent pas du locataire. `FactureInfo` n'étant créé qu'au premier enregistrement (`_loc.factureX ?? new FactureInfo()`), un locataire neuf repartait systématiquement des valeurs d'usine.

**La règle retenue est une chaîne, pas un réglage global.** Le 1er locataire part des valeurs d'usine et se règle ; le 2e est **créé avec une copie** des réglages du 1er, puis vit sa vie ; le 3e copie ceux du **2e**, et ainsi de suite. Chaque locataire **possède** ses réglages : les modifier n'affecte pas les précédents, et les précédents ne le modifient pas rétroactivement.

Deux points sur lesquels cette règle se distingue d'un modèle d'entreprise unique — et c'est exactement ce qui a été demandé :

- la source est le dernier locataire **créé**, pas le dernier **enregistré**. Retoucher la facture du 1er alors que le 3e existe déjà ne change rien à ce qu'héritera le 4e : c'est le 3e qui sert de modèle ;
- la copie est **faite à la création**, pas résolue à l'affichage. Les valeurs sont posées sur la fiche du nouveau locataire, donc visibles et modifiables immédiatement, et elles ne bougent plus quand la source change.

| | |
|---|---|
| `HeritageFacture.Appliquer(neuf, existants)` | Horodate la fiche puis y duplique les réglages du locataire créé juste avant. Appelé depuis `BatimentPrefab.Addlocataire`, **avant** la construction de la fiche — le panneau lit ces valeurs à son ouverture. |
| `HeritageFacture.Precedent(existants, neuf)` | Le locataire créé le plus récemment, `neuf` exclu. |
| `HeritageFacture.TousLesLocataires()` | Tous les locataires de l'entreprise, **tous bâtiments confondus** (surcharge testable prenant la liste des bâtiments). |

**La chaîne traverse les bâtiments, et c'est essentiel** : créer un bâtiment neuf ne remet pas les réglages à zéro. Son 1er locataire copie le dernier locataire créé **où qu'il soit**, y compris dans un autre bâtiment — sinon chaque nouveau bâtiment obligerait à tout re-saisir, alors que RIB, entête et numérotation sont communs à l'entreprise. L'ordre des bâtiments ne prime jamais sur la date de création : c'est bien le dernier créé qui sert de modèle, pas le dernier bâtiment de la liste.

Une question qu'on se pose légitimement en lisant ce code : `BatimentManager.Batiments` tient des **copies**, que `SaveBatiment` remplace par l'objet vivant à chaque enregistrement (« les prefabs travaillent sur des copies (clone JSON) »). Les réglages hérités pourraient-ils être périmés ? Non : un `FactureInfo` ne naît que dans `SaveFromUI`, qui se termine par `SaveAfterModifyToDoListLocataire()` → `SaveBatiment(batiment)`. Tout locataire ayant des réglages a donc été enregistré, donc son bâtiment est à jour dans la liste. Vérifié en traçant les deux appels.

**`Locataire.creationISO`** (nouveau champ, `yyyy-MM-dd HH:mm:ss`) porte la chronologie. L'ordre des listes ne suffisait pas : les locataires sont répartis entre plusieurs bâtiments, donc leur chronologie réelle n'est pas reconstituable par simple parcours. Une fiche antérieure sans date compte comme la plus ancienne mais **reste éligible** comme source — sinon une sauvegarde existante repartirait d'usine à chaque nouveau locataire.

**Ce qui se duplique** : `ribId` · `enteteId` · `numeroFormat` · `tvaDebit` · `tvaEncaissements` · `ajouterRetard` · `ajouterMensuel` · `envoiEmail` · `joindrePj`.

**Ce qui ne se duplique jamais** — le point à tenir : dates, numéro et `numeroId`, montants, destinataire (nom/adresse/SIRET), email, objet, période, `chargeId`, `refInterne`, `sommePhrase`. Et `saved` reste **faux**, sans quoi les montants ne seraient pas recalculés depuis la fiche du nouveau locataire. Dupliquer un réglage reprend une décision déjà prise ; dupliquer une donnée produirait un **document faux** — une facture datée du locataire d'à côté, ou un numéro en doublon.

**Limite assumée** : la copie a lieu au moment de la création. Créer le 2e locataire *avant* d'avoir réglé le 1er ne duplique donc rien — il faut régler le 1er puis créer le 2e. C'est l'ordre de travail naturel, mais il conditionne le résultat.

Couvert par `HeritageFactureTests` (15 tests, sans scène — la liste est passée en paramètre) : la chaîne T1 → T2 → T3, le fait que modifier le 2e n'altère pas le 1er, la priorité de la date de création sur l'ordre des listes, **le 1er locataire d'un bâtiment neuf qui copie celui d'un autre bâtiment** et la chronologie respectée à travers les bâtiments dans les deux sens, la garde « aucune donnée de facture n'est dupliquée » champ par champ, et les fiches antérieures sans date.

### Explication du dépôt : les phrases se règlent sur la facture (2026-09-17, corrigé le 21/09)

Les phrases du bloc explicatif de la facture de révision du dépôt étaient **écrites en dur** dans `ExplicationDepot` : « il ressort que vous nous devez : X », « nous vous devons : X », le rappel des N termes. Impossible d'en changer la formulation ni la casse.

**Elles vivent sur la FACTURE** (`FactureInfo.depotRappel` / `depotDu` / `depotRembourse` / `depotEquilibre`) et se modifient dans le **panneau Dépôt, carte « Dépôt de garantie »**.

*Première version, corrigée le 21/09* : elles avaient été mises dans `reglage.json`, donc dans Réglages → « Textes fixes ». Mauvais endroit — ce sont des textes du **document**, pas de l'entreprise, et un texte unique ne permettait pas qu'une facture ait sa propre formulation. La règle qui en ressort, et qui vaut pour la suite : **restent dans les Réglages les seuls textes imprimés à l'identique sur les quatre types** (phrase de retard, bas de page) ; tout ce qui est propre à un type descend dans le panneau de ce type.

Les textes d'usine sont des constantes de `ExplicationDepot` (`RappelDefaut`, `DuDefaut`, `RembourseDefaut`, `EquilibreDefaut`), à côté du code qui les imprime. `ExplicationDepot.Ou(texteFacture, defaut)` porte la règle « celui de la facture s'il est renseigné, sinon celui d'usine » en un seul endroit — une phrase vide, ou une facture antérieure qui n'a aucun de ces champs, sort donc exactement comme avant. Le panneau **pré-remplit** les quatre champs avec le texte d'usine : l'utilisatrice voit toujours la phrase réellement imprimée plutôt qu'un champ vide dont il faudrait deviner l'effet.

Les quatre phrases **suivent la chaîne d'héritage** (`HeritageFacture`) : reformuler une fois, et les locataires créés ensuite en héritent.

**Ce qui reste dans le code, délibérément** : le **choix** de la phrase selon le signe de la dette (seuil au demi-centime), la mise en page (`<p class="expl-p">`) et le tableau d'indexation. C'est de la logique de document, pas de la rédaction — la rendre réglable n'apporterait rien et exposerait le sens du document à une faute de manipulation.

**Variables disponibles** : `{depot.termes}` (« deux termes », nombre accordé) · `{depot.nb}` (« deux ») · `{depot.base}` (H.T./T.T.C.) · `{depot.montant}` (**toujours positif** : la phrase porte déjà le sens, réimprimer un signe moins ferait lire « nous vous devons ‑281,80 € »), plus tous les `{loc.*}` / `{bat.*}` du résolveur commun — l'autocomplétion « / » les propose, groupe « Explication dépôt ». `ExplicationDepot.Html` reçoit pour cela un paramètre optionnel `Batiment bat = null`, donc aucun appel existant n'est cassé.

**Le texte est inséré tel quel dans le HTML**, ce qui permet le `<b>` du montant présent dans le texte d'usine. Conséquence à connaître : un `<` isolé dans une phrase casserait la mise en page du PDF. Les **valeurs substituées**, elles, sont échappées.

Les textes d'usine sont identiques mot pour mot à l'ancien code : à réglages neufs, le document imprimé est inchangé.

**Piège disparu avec le déplacement** : tant que les phrases vivaient dans les réglages, `ExplicationDepotTests` lisait le `reglage.json` **réel** de la machine — ses 14 assertions seraient passées au rouge dès la première reformulation, sans qu'aucune régression n'ait eu lieu. Il avait donc fallu un `SetUp`/`TearDown` d'isolation ; les phrases étant maintenant posées sur un `FactureInfo` local, cette isolation n'a plus lieu d'être et a été retirée. 5 tests couvrent le sujet : le cas demandé (« vous nous devez » en minuscules), deux factures qui gardent chacune sa formulation, la substitution des variables, le montant toujours positif sur un remboursement, et la non-régression d'une facture sans ces champs.

### Envoi par email — SMTP (2026-09-21)

**`EmailService`** est le premier code du projet capable de faire partir quelque chose vers l'extérieur. Deux garanties portées par le service, pas par ses appelants : **rien ne part sans destinataire explicite** (aucune adresse par défaut, aucun repli sur la fiche), et **`Succes` n'est vrai que si le serveur a accepté**. L'envoi tourne sur un thread de fond (un serveur injoignable gèlerait l'éditeur 30 s), le mot de passe ne figure dans aucun message ni journal, et une pièce jointe introuvable **fait échouer l'envoi** plutôt que d'expédier une facture sans sa facture. Depuis le 21/09, tout échec est aussi journalisé en console : il n'existait que dans un message éphémère.

**N'importe quelle messagerie convient**, port 587 (STARTTLS) : Gmail, Outlook/M365, OVH, Free, Orange. Deux points appris à l'usage :
- l'**identifiant** peut différer de l'adresse d'expédition (alias, compte OVH) — d'où le champ dédié, vide = on prend l'adresse d'expédition ;
- le **port 465** (SSL implicite) n'est pas géré par `System.Net.Mail` et est refusé avec un message explicite, plutôt que d'échouer par expiration de délai.

**Les réglages SMTP restent visibles dans les deux modes d'envoi** : les relances d'impayé partent par email même quand les factures passent par Pennylane. Les conditionner au mode les rendait inconfigurables — corrigé le 21/09.

**Envoi réel d'une facture** (les **quatre** panneaux, câblage identique) : PDF généré → si la case « envoyer par email » est cochée, refus immédiat si destinataire ou réglages manquants, **confirmation affichant destinataire, objet et pièce jointe**, envoi, puis **`FactureEmission.Enregistrer` seulement en cas de succès**. Un échec garde le PDF, ne marque rien, et laisse le numéro disponible pour un nouvel essai — c'est la règle H3 appliquée à l'envoi : le suivi ne doit jamais affirmer plus qu'il ne sait. Objet et corps sont réglables, avec variables et menu « / », et suivent la chaîne d'héritage.

Deux gardes : un double clic ne peut pas produire deux envois, et **sans boîte de confirmation disponible, rien ne part**. Quand la case est décochée, le message de fin le dit explicitement — le bouton s'appelant « Sauvegarder et envoyer », le silence était trompeur.

**État côté CIPL** : le locataire Microsoft 365 interdit les mots de passe d'application et n'a pas SMTP AUTH activé — `@cipl.fr` reste inutilisable sans intervention d'un administrateur. Validé avec Gmail en attendant ; le code ne dépend d'aucun fournisseur.

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

### Charges : état par locataire, listes de charges, régul regroupée (2026-09-29)
Règles centralisées dans `ListesCharges.cs` et `ChargeBatiment.cs` — ne pas les recopier dans les panneaux.
- **État par locataire** : `ChargeBatiment.facturations[]` {locataireId, dateISO, paye}. Refacturer/régulariser une charge ne la retire QUE pour ce locataire (avant : charge partagée perdue pour les autres). `paye` de la charge = « réglée pour tous » (manuel). Ancien `factureeISO` reconstitué au chargement d'après le suivi (`ChargeBatiment.Reconstituer`).
- **Listes de charges** (Réglages → `listesCharges`) : générale = id "" (toujours là, **renommable mais pas supprimable** : `ReglageData.nomListeGenerale`, vide = « Charges générales »). Charge → `listeId`. Locataire → `regulListes` {listeId, dateISO, provision} + `listesConcernees` (vide = toutes). Règle unique « la charge concerne ce locataire » : `ListesCharges.Concerne` ; quote-part : `ListesCharges.QuotePart` (ne répartit qu'entre locataires concernés).
- **Clés de suivi** : générale `regul-2025` (inchangée), spécifique `regul-2025-<id>`. Une ligne de régul par liste datée ; une régul émise reste au suivi même si sa liste n'est plus planifiée.
- **Loyer** : une ligne de provision par liste (PDF + Pennylane), libellé « Provision pour charges — <liste> » ; inchangé sans liste spécifique.
- **Régul regroupée** : une seule facture pour les listes cochées, rangée sous une ligne, les autres lignes portent le même numéro/PDF et leur part du montant. **Même PDF = même facture** (`FacturationSuivi.MemeFacture`) : statut commun, envoi auto unique. Garde-fou `FactureRegulPanel.Regroupement` : jamais deux factures existantes mélangées, jamais une correction qui oublie une de ses listes.
- **Transfert entre entreprises** : les listes sont retrouvées par nom dans la destination (ou créées), tous les ids réécrits (charges, locataires, factures mémorisées, clés de suivi).
- **Retirer une liste à un locataire** (Gestion du loyer) : refusé s'il y doit encore une charge (non régularisée ou non payée) ; avertissement si toutes sont payées ; direct s'il n'en a aucune. Ne comptent que ses charges : désigné/tous, quote-part > 0, datées depuis le début de son bail (`ListesCharges.ChargesDeListe`).
- **Supprimer une liste** (Réglages) : refusé tant qu'un locataire **à provision** y doit une charge (`ListesCharges.Debiteurs`) ; sinon avertissement, puis ses charges **basculent dans la générale** — elles restent dans l'historique avec leur état par locataire ; dates/provisions de la liste retirées ; un locataire limité à elle l'est à la générale (`ListesCharges.BasculerVersGenerale`).

## 14. Règles de sécurité

- Clé API **hors repo**, **jamais dans le chat**, **jamais commitée**.
- **Finalisation = émission légale d'une vraie facture** → **validation manuelle uniquement**, jamais sans accord explicite d'Agathe.
- Rester en **brouillon** (`draft:true`) pour tous les tests.

## 15. Liens

- Doc API Pennylane : https://pennylane.readme.io/
- Plan d'intégration (artifact) : https://claude.ai/code/artifact/1edb948f-582d-4b46-ae8d-463c0c476a09

## 16. État d'avancement (implémenté)

> Mis à jour 2026-09-29. Génération PDF locale (Edge headless). **Pennylane (Voie 2) : dépôt implémenté pour le Loyer** (`PennylaneClient`, import incomplet + Factur-X, testé de bout en bout le 29/09, §5) — **finalisation et transmission (`send_to_pa`) NON écrites**, en attente de la réponse Pennylane sur l'éligibilité des factures importées. Clé API : secrets de l'entreprise (Réglages), jamais dans le dépôt.

### 16.1 Génération de factures (PDF local, aperçu in-app)
`FacturePdfService` (HTML StreamingAssets + Edge `--print-to-pdf` / screenshot PNG). 4 types validés visuellement :
- **Loyer** (`FactureLoyerPanel`), **Régularisation** (`FactureRegulPanel`, détail charges page 2), **Refacturation** (`FactureRefacPanel`), **Révision dépôt** (`FactureDepotPanel`, sans TVA). Aperçu PDF zoomable (`FacturePreviewViewer`).
- **Période proposée par défaut = période EN COURS (2026-09-14)** : à l'ouverture de « Facturer le loyer » sans facture mémorisée, le menu « Période » proposait **toujours la 1re** période (`DefaultPeriodeIndex` renvoyait `1` sauf en mensuel) → on facturait le « 1er trimestre » par erreur. Corrigé : défaut = **période courante d'après la date du jour** (mensuel → mois ; trimestriel → `(mois-1)/3+1`, ex. sept → 3e trim ; bi-annuel → semestre ; annuel → 1). ⚠ L'**échéance** affichée pour une facture émise reste sa **date de paiement mémorisée** (date de facture + 30 j), distincte de la date calendaire de la période — d'où un « 1er trim » émis pouvant apparaître à une échéance d'octobre dans le Suivi.
- **Entête auto par type de facture (2026-09-15)** : à l'ouverture d'un panneau sans entête mémorisée (`FactureInfo.enteteId` vide), le menu « Entête » se **positionne automatiquement** sur l'entête correspondant au type — Loyer/Regul/Refac/Depot. `ReglageService.EnteteChoisi(stored, type)` = mémorisée si présente, sinon `EnteteDefautId(type)` qui prend la **1re entête dont le NOM contient un mot-clé du type** (loyer ; régularisation/charges ; refacturation ; dépôt/garantie), à défaut la 1re de la liste. Appliqué dans les 4 `Facture*Panel` (arg de sélection du `_enteteDD.SetOptions`). Une entête modifiée à la main reste mémorisée (`enteteId` sauvé) et l'emporte.
- **Lieu d'émission (2026-09-29)** : la ville devant la date (« St Marcel Paulel, le … ») n'est plus écrite dans les gabarits (`{{LIEU}}`) : Réglages → Textes fixes → « Lieu d'émission » (`ReglageData.lieuEmission`), réglage d'entreprise seul, vide = « St Marcel Paulel ». Facture, régul et quittance. Tests `LieuEmissionTests`.
- **Provisions par liste sur le loyer (2026-09-29)** : avec des listes de charges spécifiques, une ligne de provision par liste (« Provision pour charges — <liste> ») sur le panneau, le PDF et le dépôt Pennylane ; inchangé sans liste. Voir §13 « Charges : état par locataire… ».
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
- Régul : ligne `regul-{année}` (échéance N+1 à la date de régul) pour les charges générales, **plus une ligne `regul-{année}-<id>` par liste de charges spécifique datée** (2026-09-29). Une régul regroupant plusieurs listes = une facture portée par plusieurs lignes (même PDF → même statut). Dépôt : ligne `depot-{année}` si `dateRevisionDepotISO` tombe dans l'année.
- Par locataire : `LocataireSuiviInline` (bandeau pleine largeur en bas de la fiche ; la vue pop-up `FacturationSuiviPanel` a été **supprimée le 28/09** ; sélecteur d'année = **chips clonés des filtres d'améliorations** `objectivesManager.filterAllButton`, année active = ambre + texte blanc).
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
- **Tuiles KPI globales sur le menu principal (2026-09-15)** : la même rangée de 6 tuiles (Loyers/an · Dépensé/an · Investi · Total gagné · Rendement net · Cash flow/mois) est ajoutée **en haut du menu général** (`GeneralMenuPanel.EnsureKpiGlobaux`/`RefreshKpiGlobaux`, insérée en sibling 1 du `Panel`, appelée dans `Refresh`), **agrégée sur tous les bâtiments**. Mutualisation : `BatimentSummaryView.KpiTile` passé **public static** (même visuel) + `BatimentSummaryView.ComposantesFinancieres(bat, out loyers, mensualitésAn, chargesAn, investi, gagné)` (public static) utilisé par la fiche **et** le menu ; le rendement global = `(Σloyers − Σcharges) / Σinvesti` (agrégation des numérateurs/dénominateurs, pas moyenne des %). `CalculFinances` refactorisé pour s'appuyer dessus.
- **Rendement net = net de charges (2026-09-15)** : la tuile **« Rendement net »** ne se base **plus sur le financement** (mensualités) mais sur les **charges d'exploitation** — `rendement net = (loyers annuels − charges bâtiment de l'année en cours) / coût de revient × 100` (`CalculFinances`, `chargesAn` = Σ `bat.charges.cout` dont `dateISO.Year == année courante`). Cohérent avec le Calcul rapide (`QuickCalcInline`) où **« Rentabilité »** = loyer/prix (brut) → la tuile en est la version **nette de charges**. Le financement reste porté par **Dépensé/an** (mensualités×12) et **Cash flow/mois** (loyers − mensualités×12). NB : vrai « net‑net » fiscal non calculable (dépend du taux d'imposition) — ici net des charges bâtiment saisies.
- **Sections agrandies (2026-09-14)** : la **carte de présentation** (`CarteBatiment`) est agrandie dans `EnlargeKpisOnce` — **vignette 165×118 → 235×150**, **nom fs 24 → 30**, adresse ≥ 17, et la **hauteur de carte** (pilotée par son `LayoutElement` fixe) passée à **215**. La **liste des locataires** (`LocataireRowUI.Setup`) est agrandie — **lignes 50 → 64 px**, **nom fs 19 → 23**, **avatar 34 → 46**, initiales 16, sous-titre 13, loyer 16.
- **Correctif régul en retard (2026-09-12)** : `FacturationAlertes` regarde la **dernière échéance de régul déjà arrivée** (`DerniereOccurrence` mois/jour ≤ aujourd'hui) et pas seulement la prochaine → une régularisation **échue et non traitée** déclenche bien URGENT (avant : `ProchaineDateAnnuelle` sautait au futur, aucune alerte). Une régul concerne les charges de l'année N‑1 (échéance mois/jour de N). **Garde-fou sur le bail rétabli le 30/09** (demande de l'utilisatrice : un bail de 2026 affichait des régul depuis 2023) : pas de régul — ni ligne de suivi, ni alerte — pour des charges d'une année où le locataire n'était pas dans les lieux, du **premier** bail (`dateDebutPremierBailISO`, conservé aux renouvellements) au départ (`Loyers.AnneeDansLeBail`). *Historique :* **Pas de garde-fou sur le début du bail** : un garde-fou « année de charges ≥ début du bail » avait été essayé mais il **cachait** l'alerte pour les baux récents (2026) → retiré. L'échéance de régul du Suivi reste en N+1 (« Régularisation des charges N » due en N+1) — donc l'onglet de l'année N l'affiche « À venir », la régul en retard (charges N‑1) est dans l'onglet N‑1 en « À faire ». ⚠ Après recompilation, **relancer le Play** pour que l'alerte apparaisse.

### 16.4 UI menu principal
2 colonnes **À traiter** | **Créances** + **Bâtiments** ; en-têtes de section uniformisés. Voir mémoire `project_home_creances_layout`.

### 16.5 UI fiche locataire
Réorganisée (croquis utilisatrice) : **Gauche** = General · Loyer · Dépôt ; **Droite** = Bail · Commentaire · Objectif · **Facturation** (4 boutons en grille 2×2) ; **bandeau Suivi** pleine largeur en bas. RIB compact, Pappers dans la boîte Siret, Email/Tél sur une ligne, **Loyer** refait en compact (tuile annuelle HT/TTC + mini-tableau période côte à côte avec le récap « Modalités du loyer »), 11 types de bail. Voir mémoire `project_vue_locataire`.
- **Pop-up « Loyer » scindé en 2 volets (2026-09-12)** : l'ancienne modale « Révision du loyer » (`RevisionPanel`, GameObject `Canvas/Loyer`) mélangeait facturation + révision → séparée en **deux menus** ouverts par **deux boutons dans la bande de titre « Loyer »** de la fiche :
  - **« Modalités »** (bouton secondaire ambre clair `#F4E7CD`/`#A9741C`, clone de « Réviser ») → **gestion du loyer / facturation** : périodicité · jour de demande · mois facturés · provisions + montant · date de régularisation. Titre « Gestion du loyer », habillage ambre, bouton **Enregistrer**.
  - **« Réviser »** (inchangé) → **révision du loyer** : type d'indice · loyer de départ · trimestre de référence · dates · trimestre de révision · calcul + résultat.
  - **Listes de charges dans « Modalités » (2026-09-29)** : dès qu'une liste spécifique existe (Réglages) et que « Provisions » est coché : une case par liste (« Listes de charges concernées », au moins une), puis pour chaque liste cochée — **générale comprise** — un bloc date de régularisation (même saisie JJ/MM/AAAA que le reste de l'app, clone du bloc « Date de révision ») + provision par période. Le montant à côté de la case et l'ancienne date de régul sont alors masqués (doublon). Provisions décochées → tout le bloc listes disparaît, et le locataire relève de toutes les charges.
  - **Case « Provisions » à 0 px (corrigé 2026-09-29)** : depuis le commit « add zoom », les groupes du prefab `Revision Loyer` n'étirent plus leurs enfants → la case n'avait plus de hauteur et la provision était impossible à activer. `LayoutElement` 28 px posé dans le prefab ; règle verrouillée pour tous les prefabs par `PrefabLayoutTests.Toute_case_a_cocher_dimensionnee_par_un_groupe_a_une_hauteur`.
  - **Trimestre de révision non publié (2026-09-14)** : l'indice de révision (`trimestreVoulu`, « Trimestre de révision ») exige une **correspondance EXACTE** (`InseeIndiceService.TrouveExact`, pas de fallback — contrairement à l'indice de **départ** qui recule jusqu'à 5 ans). Bug corrigé : si le trimestre voulu n'est pas encore publié par l'INSEE (ex. T4 2026 demandé mi‑2026), `RevisionPanel.Reviser` faisait `yield break` **sans effacer** le résultat précédent → l'écran gardait l'ancien « Nouvel indice » (trompeur, ex. 2025‑Q1). Désormais on **efface** Nouvel indice / Loyer révisé / Variation (→ « — ») et on **propose le dernier trimestre réellement publié** (`InseeIndiceService.DernierPublie`, tri année×4+trimestre).
    - **Avertissement inline + bouton verrouillé (2026-09-14)** : le message ne s'affiche plus en bas (statut) mais **directement sous le champ « Trimestre de révision »** (label `TrimWarning` créé en code sous `SectionRevision`, rouge `#A32D2D`, `EnsureTrimWarn`/`AfficheTrimWarn`/`MasqueTrimWarn`). Et le bouton **« Réviser » est désactivé** quand le trimestre n'est pas valide : (1) **synchrone** dès le changement de trimestre via `trimestreVoulu.OnTrimestreChanged` → `ValiderTrimestreVoulu` — un trimestre **pas encore commencé** (futur, `TrimestreDejaCommence` = début de trimestre > aujourd'hui, ex. T4 2026) bloque immédiatement sans appel INSEE ; (2) **après fetch** si le trimestre (passé mais pas encore publié) est introuvable. Le bouton se **réactive** quand l'utilisatrice choisit un trimestre déjà commencé.
    - **Débordement du statut corrigé (2026-09-14)** : le texte de statut (`Statut`, ex. « Indice des Loyers Commerciaux — loyer révisé : 7 900,54 € (prochaine révision …) ») avait `enableWordWrapping=false` → il **débordait du cadre** sur une seule ligne. Forcé à `enableWordWrapping=true` (+ `overflowMode=Overflow`) à l'ouverture ; la boîte `Information` (VLG `childControlWidth`+`forceExpand`) borne la largeur, le texte passe donc sur 2 lignes dans le cadre.
  - **Refonte des révisions (2026-09-30, schéma utilisatrice)** — remplace la bascule Initialiser/Réviser et le volet « Modalités » :
    - **Écran « Initialiser »** (volet `Initialisation`, bouton fiche « Initialiser » puis « Modalités ») : loyer de départ, périodicité, jour, mois, provisions et listes, **révision oui/non + type (indice / paliers)**, **franchise** (« facturation à partir du »), reprise. Un seul bouton dont le libellé suit l'état : *Initialiser* (jamais fait) · *Réinitialiser* (loyer de départ, révision oui/non ou type modifié) · *Modifier* (modalités seules). (Ré)initialiser vide l'indice et repart du loyer de départ (`historiqueReferences` conservé), puis : **aucune** → fiche ; **indice** → volet `Indice` en initialisation ; **paliers** → `PaliersPanel`.
    - **Indice** (volet `Indice`, bouton « Réviser ») : initialisation (type, trimestre de référence, date de révision) tant que `IndiceInitialise` est faux, puis révision (inchangée). La bascule manuelle du prefab est masquée ; l'indice n'écrit plus les modalités.
    - **Paliers** (`PaliersPanel`, bouton « Paliers ») : tableau trié, palier en cours surligné, ajout / modification / suppression. Le 1er palier démarre au **début du bail et contient la franchise** (« 4 mois, dont 3 mois payés », décision du 30/09) ; les suivants démarrent le lendemain du précédent (calculé). Un palier **finit la veille d'un début de période de facturation, ou à la fin du bail** — la fin se choisit dans une liste des seules dates valides (`Loyers.FinsPossibles`) (décision : jamais de palier en milieu de période). Modifier une fin décale le suivant ; « Valider » grisé tant que les paliers ne couvrent pas tout le bail.
    - Données (`Locataire`) : `typeRevision` (Indice = 0 pour les anciennes fiches), `paliers` {debutISO, finISO, loyer annuel HT}, `debutFacturationISO` (franchise). `LoyerInitialise` n'est pas stocké (indice connu / paliers couvrant le bail / aucune) : une fin de bail prolongée repasse la fiche « Loyer à initialiser ». Règles communes dans **`Loyers`** (`MontantPeriode`, `Couvrent`, `PoserPalier`, `Actualiser`…). `loyerAnnuel` suit le palier du jour (au chargement et à la validation).
    - Effets : alertes de révision **pour l'indice seulement** ; périodes en franchise grisées **« Franchise »** et périodes antérieures au premier bail **« Hors bail »** dans le suivi (statuts transitoires, comme « Clôturé ») ; pas d'alerte loyer pour elles ; **montant pré-rempli de la facture = loyer de la période** (`Loyers.MontantPeriode`, recalculé au changement de période, plus le montant de la dernière facture) ; périodes partielles proratisées au jour (`MontantPeriode` compte chaque jour au loyer en vigueur ce jour-là) ; rentabilité : somme des périodes (paliers / aucune), franchise et départ pris en compte (indice). Tests `RevisionLoyerTests`.
    - **Début, fin, avenant (décisions du 30/09)** : prorata au jour quand la facturation commence en cours de période (début du bail, **fin de franchise**) ou s'arrête (**départ du locataire**, saisi dans la **section Bail de la fiche** : case « Départ du locataire » + « dernier jour de location », `LocataireBailFields` ; les périodes suivantes passent « Hors bail »). **La fin du bail seule n'arrête rien** : sans départ saisi, le loyer continue en entier (tacite prolongation, au dernier palier), avec l'alerte « Bail expiré » existante. **Avenant** (renégociation, y compris en tacite prolongation) : case « Avenant : nouvelles conditions à compter du » proposée avec « Réinitialiser » → `Loyers.EnregistrerAvenant` range le loyer en vigueur jusqu'à la veille dans `historiqueLoyers` et pose `debutConditionsISO` ; la période qui contient l'avenant est proratisée entre l'ancien et le nouveau loyer, les paliers repartent de la date de l'avenant. Sans date d'avenant, réinitialiser corrige tout le bail (comme avant). Un locataire parti n'a plus d'alerte de révision.
  - **Parcours de création du locataire (2026-09-30)** : Général → Bail → Loyer → Dépôt, dans cet ordre (`ParcoursLocataire`, état déduit de la fiche, rien de stocké). Bandeau d'étapes en tête de fiche (`ParcoursLocataireUI`, faite / en cours / à venir + « Continuer ») tant que le locataire est incomplet — pour toute fiche nommée, pas seulement à la création. À la création, « Sauvegarder » exige le nom puis des dates de bail valides (un bail jamais saisi partait au 01/01/0001), puis l'écran « Initialiser le loyer » s'ouvre seul ; une fois le loyer initialisé (aucune / indice / paliers validés), l'initialisation du dépôt s'ouvre seule. Verrous : pas de loyer avant le bail enregistré, pas de dépôt avant le loyer, **aucune facture avant la fin du parcours** (les 4 boutons de la carte Facturation et « Générer / Refaire / Corriger » du suivi, `ParcoursLocataire.FacturationBloquee`). Vérifié sur DemoCIPL le 30/09 : tous les vrais locataires sont complets, seul le locataire de test « TEST Pennylane — Volteo » (loyer non initialisé) est bloqué. Le nom par défaut « Nouveau » (`Data.NomParDefaut`) ne compte pas comme un nom.
  - **Dépôt — initialisation (2026-09-30)** : bouton de la carte « Initialiser » tant que `DepotInitialise` est faux (anciennes fiches : dépôt > 0 = initialisé). Même modale : nombre de périodes, TVA, date de la prochaine révision, **« Dépôt demandé et reçu ? »** → *reçu* : rien ; *demandé, pas reçu* : créance « Envoyé » `depot-initial` au montant du dépôt, échéance au début du bail ; *pas encore demandé* : ligne `depot-initial` « À faire » → « Générer » ouvre `FactureDepotPanel` en mode initial : facture au **format d'une refacturation** (gabarit facture simple, une ligne « Dépôt de garantie — N termes de loyer », TVA 0 libellée « dépôt de garantie non soumis »), titre « Dépôt de garantie », PDF `DepotGarantie-…` ; pas de bloc d'explication (réservé à la révision).
  - Implémentation : **un seul panneau réutilisé**, `RevisionPanel.Open(loc, onSaved, Volet)` (`Volet.Initialisation` / `Volet.Indice`) montre/masque les blocs de `Content/Body` selon le volet (`ApplyVolet`). Le bouton « Enregistrer » est un **clone de `btnReviser`** dans la rangée `Indices` (`SaveModalites`). Le 2ᵉ bouton fiche = `LoyerSummaryUI.EnsureModalitesButton` (le badge « Révision à faire » est frère du bouton Réviser → non dupliqué). Récap de la carte Loyer renommé « Facturation du loyer » → **« Modalités du loyer »**.
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

### 16.7 Reste à faire (mis à jour 2026-09-29)
- **Pennylane** : ~~dépôt Voie 2~~ fait pour le Loyer (29/09). Reste : **finalisation + `send_to_pa`** (attend la réponse Pennylane : une facture importée est-elle « éligible » ?) ; dépôt depuis **Régul / Refac / Dépôt** ; lecture du statut `e_invoicing` ; l'envoi auto au lancement passe encore par l'email même en mode Pennylane ; **nettoyer les données de test** (§12 : clients/factures TEST, locataire « TEST Pennylane — Volteo » dans DemoCIPL, entreprise « Test transfert »).
- **Multi-SCI** : 1 abonnement + 1 token Pennylane par société — à confirmer avec Pennylane avant de vendre l'app à un client multi-sociétés.
- **Listes de charges** : tour complet en Play (Réglages → charge → Modalités → loyer → régul regroupée → suivi → paiement).
- Détection des paiements ; réglage du **jour de demande du loyer** (par défaut 0 → échéances au 1er du mois dans le Suivi).

### 16.8 Diagramme de comportement de la facturation (référence utilisatrice, 2026-09-12)
Machine à états cible fournie par l'utilisatrice (schéma). Depuis `Ouverture app → liste des états → État` :
- **En attente (À venir)** → selon **type** : *Loyer* → à faire à échéance‑22 j · *Régul* → à faire à la date de régul · *Dépôt* → à la date de révision (crée alerte « à traiter » + pastille, l'utilisateur fait la révision) · *Refacturation* → créée à la main → directement **Envoyé**.
- **À faire** → alerte « à traiter » + pastille sur le bouton dédié → l'utilisateur crée la facture → si **loyer ET aujourd'hui ≤ échéance‑15 j → En attente d'envoi**, sinon **Envoyé**.
- **En attente d'envoi** → à **échéance‑15 j** → **Envoyé** (auto).
- **Envoyé** → crée une ligne dans créances ; à **échéance+15 j** → **Impayé** ; l'utilisateur peut **refaire** la facture → nouvelle facture *corrigée(X)* (X = nb de corrections), lien PDF mis à jour dans le suivi.
- **Payé** → si **location non commerciale** → possibilité d'émettre la créance de loyer.
- **Impayé** → crée une ligne dans créances + **option « envoyer un rappel d'échéance »** dans le suivi → l'utilisateur clique → **mail de rappel**.

**Implémenté (16.2)** : les transitions d'états (À faire/En attente d'envoi/Envoyé/Impayé) + seuils.
- **Refaire → *corrigée(X)*** ✅ (loyer, 2026-09-12) : champ `FactureEtat.corrections` ; `FacturationSuivi.EstDejaEmise` détecte une facture déjà émise (Envoyé/Impayé + PDF) et `MarquerCorrige` incrémente le compteur, suffixe le libellé **« — corrigée(X) »** (réappliqué par `Fusion` depuis le compteur), met à jour le lien PDF, **conserve le numéro** et le statut. Dans le suivi, le bouton devient **« Corriger »** pour un loyer émis/impayé → `FactureLoyerPanel.OpenLoyer(fiche, ligne)` force la période/année de la ligne et **ne consomme pas de nouvelle séquence**. Le PDF porte « {numéro} corrigée(X) ». **Étendu aux Régul / Refac / Dépôt (2026-09-16)** : les trois `SauvegarderEtEnvoyer` calculent désormais leur clé de suivi **avant** la génération du PDF (`regul-{année}`, `refac-{id charge}`, `depot-{année de révision}`), passent par `EstDejaEmise` puis `MarquerCorrige`, et sortent sans consommer de séquence. Le nom de fichier reçoit le suffixe `-corrigeeX`, donc le PDF d'origine n'est plus écrasé. Avant ce correctif, seul le loyer était protégé : sur les trois autres types, un second clic consommait un numéro **et** écrasait la facture déjà émise, qui restait pourtant référencée dans le suivi.

  **Complété le 2026-09-16 (vérifié en exécution, pas seulement compilé)** : `EstDejaEmise` ne testait que **Envoyé/Impayé**, alors que la même notion est portée par **quatre** états dans `DejaTraite`. Deux cas passaient donc au travers, reproduits par un test exécuté dans l'éditeur :
  - **loyer préparé plus de 15 j avant l'échéance** — statut `AttenteEnvoi`, c'est-à-dire le cas **normal** du panneau Loyer : `EstDejaEmise` renvoyait `false` malgré un PDF déjà généré → un second clic consommait une nouvelle séquence **et** écrasait ce PDF ;
  - **facture marquée « Payé » puis re-générée** — repartait sur un numéro neuf, donc **deux numéros pour une seule période**.

  `EstDejaEmise` teste désormais `AttenteEnvoi | Envoyé | Impayé | Payé` (+ PDF non vide) : un paiement ne « dé-émet » pas une facture, un numéro consommé le reste. En contrepartie, `MarquerCorrige` **ne force plus** `AttenteEnvoi → Envoye` : corriger un loyer avant sa date d'envoi afficherait sinon un envoi qui n'a pas eu lieu (la bascule à J‑15 reste assurée par `EtatDe`), et une facture payée reste payée. Seule une ligne **sans statut** devient « Envoyé ». Non-régressions vérifiées : ligne planifiée et ligne sans PDF ne sont toujours pas vues comme des émissions.

- **Impayé → « rappel d'échéance »** ✅ (2026-09-12) : bouton **« Rappel »** sur les lignes Impayé des deux vues du suivi (`LocataireSuiviInline` + `FacturationSuiviPanel`, colonne Actions élargie à 280). `FactureRappelService.Demander` ouvre une **confirmation** (`ConfirmDialog`) puis un **brouillon email `mailto:`** pré-rempli (sujet + corps reprenant libellé/n°/montant/échéance + `ReglageData.phraseRetard` + signature `smtp.fromName`) vers `loc.emailLocataire` → **aucun envoi automatique**, l'utilisatrice valide dans sa messagerie (même principe que `ContactLink`). Date mémorisée : `FactureEtat.dernierRappelISO` via `FacturationSuivi.MarquerRappel` (sur l'enregistrement **stocké**, pas la copie `Fusion`).

- **Payé (bail non commercial) → quittance de loyer** ✅ (2026-09-12) : sur une ligne **loyer Payé** dont le bail n'est **pas commercial**, bouton **« Quittance »** (2 vues du suivi) → `FactureQuittanceService.Emettre` génère un **PDF de quittance de loyer** en local (nouveau template `StreamingAssets/quittance_template.html`, style maison, via `FacturePdfService.GenerateQuittancePdf`) puis l'ouvre. Classement commercial/non commercial : `Locataire.EstBailCommercial` (commercial = Bail9ans/Bail10ans/Commercial369/CommercialFerme/Dérogatoire ; le reste = non commercial → quittance). *28/09 : la liste ne propose plus « 9 ans », « 10 ans » ni « 9 ans ferme » — ce sont des baux commerciaux ; la durée et la période ferme (`dureeBailAns`, `anneesFermes`) se règlent à côté du type. Ces anciennes valeurs restent commerciales si elles figurent dans un fichier (`BailCommercial9Ferme` seulement renommé `BailCommercialFerme`, même numéro stocké). Classement inchangé pour tous les baux existants.* Montant = TTC payé ; période déduite du libellé ; désignation = bâtiment + lot. Aucune émission réelle.

  **Garde ajoutée le 21/09** : le bouton n'apparaît que si la facture est **réellement émise** — numéro **et** PDF non vides. L'état « Payé » peut être forcé à la main sur une ligne jamais émise ; sans ce contrôle on éditait une quittance, donc un reçu de paiement, pour une facture qui n'existe pas. La garde existait dans `LocataireSuiviInline` et **manquait** dans `FacturationSuiviPanel` : les deux vues construisaient la ligne chacune de leur côté et avaient divergé. Trouvé en passant cette ligne en prefab commun (`SuiviFactureRow`), qui a mis les deux copies côte à côte.

  **Affichage, 24/09** : les boutons d'action du suivi (PDF · Corriger/Refaire · Générer · Rappel · Quittance) avaient une largeur fixe de 74 px, calibrée pour du 13 pt ; passés au rôle Action (15 pt), « Générer » ou « Quittance » se coupaient sur deux lignes. Leur largeur suit désormais le libellé (`UIFactory.LargeurDuTexte`, dans les deux vues) ; le pire cas, PDF + Corriger + Quittance, occupe 221 px sur les 280 de la colonne. Les en-têtes « État » et « Actions » prennent la taille des autres en-têtes. Aucun changement de comportement.

  **Clic sur une facture de « Créances » (menu général), 28/09** : ouvre la fiche du locataire et la fait défiler jusqu'à sa section « Suivi de facturation », sur l'année d'échéance de la facture (`LocataireSuiviInline.MontrerPour`, même règle d'année que le suivi : `FacturationSuivi.TryEcheance`). Avant, la vue plein écran `FacturationSuiviPanel` s'ouvrait par-dessus la fiche ; devenue inutile, elle a été supprimée. Aucun changement sur les factures elles-mêmes.

  **Variable `{loc.bail}` des textes de facture, 28/09** : elle insérait le nom interne du type (« BailCommercial369 ») ; elle insère désormais son libellé (« Bail commercial (3/6/9) »), via `Locataire.LibelleBail`.

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
