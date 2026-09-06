# Facturation CIPL × Pennylane — README de projet

> Fichier de référence vivant. **Mis à jour au fil de l'avancement** pour ne rien perdre entre les sessions.
> Dernière mise à jour : 2026-09-03.

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
