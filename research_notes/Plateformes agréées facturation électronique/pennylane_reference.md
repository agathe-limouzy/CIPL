# Pennylane comme plateforme agréée (PA) : référence pour la comparaison (état au 22 octobre 2026)

Point de vue : CIPL, logiciel tiers de gestion locative (bureau), génère des factures de loyer Factur-X pour plusieurs SCI. Il veut les émettre via l'API d'une PA et recevoir les factures fournisseurs.
Recherche faite le 22/10/2026. Les pages de la doc développeur (pennylane.readme.io) et les pages commerciales de Pennylane ne sont pas datées entrée par entrée. Une date de consultation au 22/10/2026 est donc implicite pour toutes les sources non datées. Aucune source utilisée n'est antérieure à 2025, sauf mention contraire.

## 1. Pennylane est-elle immatriculée PA par la DGFiP (date, statut) ?

### Takeaway
Oui. Pennylane fait partie de la première vague de PA confirmées par la DGFiP début décembre 2025. Sa propre page (août 2026) et des sites tiers parlent d'une immatriculation **définitive depuis décembre 2025** (11/12/2025 selon des blogs). Je n'ai pas pu lire la liste officielle sur impots.gouv.fr, et une page Pennylane plus ancienne ainsi qu'un comparatif tiers se contredisent sur le statut « définitif » ou « sous réserve ».

### Cited Findings
- Le comparatif de Pennylane (« Mise à jour le 7 août 2026 », données de comparaison collectées en avril 2026) affirme que Pennylane est « immatriculée plateforme agréée à titre définitif depuis décembre 2025 ». Il ajoute que Chorus Pro et l'Access Point PEPPOL sont « déjà opérationnels » et que la plateforme émet, reçoit et transmet les factures en Factur-X, UBL et CII. — [Pennylane, comparatif des PA (07/08/2026)](https://www.pennylane.com/fr/fiches-pratiques/facture-electronique/comparatif-des-plateformes-agreees)
- Compta-Online (article du 03/09/2025, modifié le 02/08/2026) : dans sa mise à jour du 6 décembre 2025, la DGFiP « a confirmé l'immatriculation des premières plateformes agréées » et cite « Dext, Pennylane et Yooz ». Au 16 janvier 2026, on compte « 101 plateformes immatriculées définitivement + 25 plateformes immatriculées sous réserve ». À l'été 2026, il y a « environ 140 » PA, dont environ 52 qui visent l'émission dès le 1er septembre 2026. L'article ne précise pas si Pennylane est en statut définitif ou sous réserve. — [Compta-Online (modifié 02/08/2026)](https://www.compta-online.com/plateformes-agreees-facturation-electronique-ao6026)
- Selon ma-facture-electronique.org (publié le 16/08/2026, mis à jour le 02/10/2026), Pennylane a obtenu une immatriculation « sous réserve » en 2024, puis définitive en décembre 2025. La liste DGFiP compterait 149 opérateurs immatriculés définitivement au 22/09/2026. **Le site est commercial** : liens Pennylane affiliés, mise en avant d'Indy. — [ma-facture-electronique.org, fiche Pennylane (02/10/2026)](https://ma-facture-electronique.org/plateforme-agreee/liste-officielle/pennylane/)
- **Contradiction** : la fiche pratique Pennylane « PDP » (« Mise à jour le 1 avril 2026 ») range Pennylane sous un titre « Les 107 Plateformes Agréées (PA) immatriculées sous réserve ». Elle indique une participation « aux tests DGFIP depuis décembre 2024 » mais ne donne ni numéro ni date d'immatriculation. — [Pennylane, fiche PDP (01/04/2026)](https://www.pennylane.com/fr/fiches-pratiques/facture-electronique/pdp)
- **Autre contradiction** : selon le résumé de recherche, au moins un comparatif tiers affirme qu'« aucune plateforme ne dispose d'un agrément définitif » et que Pennylane serait immatriculée « à titre provisoire le 11 décembre 2025 ». Je n'ai pas ouvert cette page, le comparatif exact n'est donc pas identifié. Candidats : [guide-plateforme-agreee.fr](https://guide-plateforme-agreee.fr/plateforme/pennylane/), [logicielcomptable.net](https://logicielcomptable.net/pennylane-plateforme-agreee-tarifs-et-statut-dgfip/), [artisansmart.fr](https://artisansmart.fr/blog/pennylane/).
- La liste officielle se trouve sur impots.gouv.fr (« Je consulte la liste des plateformes agréées », espace Professionnel). Elle comporte deux listes : les opérateurs qui remplissent toutes les conditions, tests d'interopérabilité compris, et ceux dont le dossier est complet mais qui attendent l'immatriculation définitive. La première publication date du 16 janvier 2026 (101 PA). — [impots.gouv.fr, actualité publication de la liste](https://www.impots.gouv.fr/actualite/facturation-electronique-publication-de-la-liste-des-plateformes-agreees) ; [impots.gouv.fr, page de la liste](https://www.impots.gouv.fr/node/27127)
- Sur le calendrier, Compta-Online précise : « Les plateformes encore immatriculées sous réserve disposent d'un délai pour finaliser leurs tests », faute de quoi « leur numéro d'immatriculation expire ». — [Compta-Online](https://www.compta-online.com/plateformes-agreees-facturation-electronique-ao6026)

### Inferences
- Faisceau cohérent : confirmation DGFiP dès le 06/12/2025, statut définitif revendiqué par Pennylane en août 2026, PEPPOL opérationnel. Pennylane est très probablement dans la liste « définitive ». Le titre « sous réserve » de la fiche d'avril 2026 ressemble à un titre générique de liste plutôt qu'à une déclaration sur Pennylane elle-même, mais ce n'est pas vérifié.
- Pour le comparatif, il faut considérer Pennylane comme une PA « établie », de la première vague, et non comme une candidate.

### Gaps
- Je n'ai pas pu lire l'entrée Pennylane de la liste officielle (raison sociale, SIREN, numéro d'immatriculation, statut, date de mise à jour) : la page impots.gouv.fr renvoyait un avis de maintenance ou un contenu tronqué. **À vérifier directement sur impots.gouv.fr avant toute affirmation ferme.**
- Numéro d'immatriculation PA de Pennylane : introuvable publiquement dans les sources consultées.

## 2. API publique Pennylane : dépôt Factur-X, émission via la PA, réception, authentification, sandbox, programme partenaires

### Takeaway
Oui, il existe une API publique documentée (**Company API v2**, `https://app.pennylane.com/api/external/v2`, doc sur pennylane.readme.io). Elle couvre tout le besoin de CIPL :
- import d'une facture client Factur-X ;
- envoi à la PA, soit en un appel avec `send_to_pa=true`, soit par un endpoint dédié `POST /customer_invoices/{id}/send_to_pa` ;
- suivi du statut de cycle de vie, par lecture des champs ou par webhook ;
- réception des factures fournisseurs (webhook `supplier_invoice.e_invoicing_received`) et réponse au fournisseur (contestation ou refus).

Côté authentification, il y a un token développeur **par société** (plan Essentiel ou supérieur) ou OAuth 2.0 pour les intégrateurs (app validée par l'équipe Partenariats). La sandbox se demande à l'équipe Partenariats.
Il existe une deuxième voie, distincte : le programme « Solution Compatible », via des API REST **AFNOR**, gratuit pour l'éditeur, avec sandbox.

### Cited Findings

**Documentation et versions**
- Doc développeur : pennylane.readme.io. « New capabilities are now added in API v2, while v1 is deprecated and will not receive new features. » — [Pennylane API, Supported use cases](https://pennylane.readme.io/docs/supported-use-cases)
- L'URL de base est `https://app.pennylane.com/api/external/v2`, la spécification OpenAPI est en « version 2.0 ». — [Référence, Import a customer e-invoice](https://pennylane.readme.io/reference/createcustomerinvoiceeinvoiceimport)
- Guide « 2026 API changes » : preview le 14/01/2026, sunset le 08/04/2026, cleanup le 01/07/2026.
  - Le scope `ledger` est remplacé par `journals`, `ledger_accounts`, `ledger_entries` et `file_attachments:all`.
  - Plusieurs listes passent en pagination par curseur.
  - `POST /ledger_attachments` est remplacé par `POST /file_attachments`.
  - Rien de spécifique à l'e-invoicing.
  — [Pennylane, 2026 API changes guide](https://pennylane.readme.io/docs/2026-api-changes-guide.md)

**(a) Déposer une facture Factur-X (PDF + XML embarqué)**
- `POST /api/external/v2/customer_invoices/e_invoices/imports`, scope `customer_invoices:all`, corps en `multipart/form-data`.
  - `file` (obligatoire) : « Factur-X PDF, UBL XML, or CII XML ». Le Factur-X PDF est le format recommandé en production. UBL et CII seuls sont en support **alpha** : le fichier n'est pas affiché, il n'y a pas d'aperçu et la facture n'est pas reconnue comme électronique dans Pennylane.
  - `invoice_options` (optionnel) : `customer_id`, et `invoice_lines[]` avec `e_invoice_line_id` (doit correspondre au BT-126 LineID du Factur-X), `ledger_account_id`, `product_id`.
  - `override_invoice_lines` (optionnel) : 1 à 500 lignes. Ignoré si le total ne réconcilie pas avec le Factur-X. Envoyer à la fois `override_invoice_lines` et `invoice_options.invoice_lines` renvoie 400.
  - Réponse 201 : seulement `{id, url}`. Erreurs 400, 401, 403, 404, 422.
  - En-tête `X-Company-Id` : requis seulement avec un token cabinet (firm).
  — [Référence, createCustomerInvoiceEInvoiceImport](https://pennylane.readme.io/reference/createcustomerinvoiceeinvoiceimport)
- Endpoints dédiés client et fournisseur. Le résumé de recherche date ce changement de mars 2026, mais la page elle-même n'est pas datée. L'endpoint global `POST /api/external/v2/e_invoices/imports` est **déprécié**. On ne peut plus émettre de nouveaux tokens avec le scope `e_invoices:all` : il faut passer à `customer_invoices:all` ou `supplier_invoices:all`. — [Changelog, dedicated e-invoice import endpoints](https://pennylane.readme.io/changelog/customer-supplier-invoices-dedicated-e-invoice-import-endpoints)
- Autre voie : `POST /api/external/v2/customer_invoices/imports` avec `convert_to_e_invoice=true` fait générer le Factur-X par Pennylane à partir d'un PDF simple. Erreurs 422 possibles :
  - numéro de facture au-delà de 35 caractères, ou contenant d'autres caractères que les alphanumériques et `-+_/` ;
  - ligne sans libellé ;
  - fichier qui n'est pas un PDF ;
  - client inéligible : « Customer is not a company or is a public administration », message « The customer is not eligible for e-invoicing ».
  — [Changelog, Factur-X conversion on customer invoice import](https://pennylane.readme.io/changelog/factur-x-conversion-on-customer-invoice-import.md)

**(b) Émettre réellement via le réseau (envoi à la PA)**
- **Option 1, en un appel** : `send_to_pa=true` à l'import. Selon le changelog, l'envoi « happens as soon as parsing completes, before Schematron validation ». Conséquence : « the invoice may be found invalid after it has already been submitted to the PA » et la PA la rejette alors. Il faut donc surveiller `e_invoicing.status` (par exemple `sent`, `rejected`) et `schematron_validation_status` (`pending`, `valid`, `invalid`). — [Changelog, Automatically send to PA](https://pennylane.readme.io/changelog/automatically-send-to-pa-on-customer-e-invoice-import)
- **Option 2, endpoint dédié** : `POST /api/external/v2/customer_invoices/{id}/send_to_pa`, scope `customer_invoices:all`, sans corps.
  - 204 : envoi en cours.
  - 409 : traitement de l'import pas encore terminé (« Retry after a few seconds »).
  - La page ne documente aucune erreur d'éligibilité spécifique.
  — [Référence, Send a customer e-invoice to PA](https://pennylane.readme.io/reference/sendtopacustomerinvoice)
- Endpoints annexes listés dans l'index de la doc : « List PA Registrations », « List eligible electronic billing addresses for a customer », création et mise à jour de clients. — [pennylane.readme.io/llms.txt](https://pennylane.readme.io/llms.txt)

**(c) Statuts et réception des factures fournisseurs**
- Webhooks :
  - `customer_invoice.e_invoicing_status_updated`, avec les statuts `accepted`, `approved`, `collected`, `in_dispute`, `partially_collected`, `refused`, `rejected`, `sent`, `submitted` ;
  - `supplier_invoice.e_invoicing_received`, déclenché quand une facture fournisseur arrive par e-invoicing ;
  - `dms_file.created`.
  Les scopes requis vont de `*:readonly` à `*:all`. — [Pennylane, List of webhook events](https://pennylane.readme.io/docs/list-of-events.md)
- Qui peut s'abonner aux webhooks : un token développeur de société, ce qui crée un abonnement limité à cette société, ou un token d'application OAuth (client credentials), ce qui crée un abonnement valable pour toutes les sociétés accessibles par l'app. Le scope `webhook_subscriptions` est requis. — [Pennylane, Subscriber types & company scoping](https://pennylane.readme.io/docs/authorization-new.md)
- Réponse au fournisseur : `PUT /api/external/v2/supplier_invoices/{id}/e_invoice_status`, scope `supplier_invoices:all`.
  - Statuts possibles : `disputed` (motif obligatoire), `refused` (motif obligatoire ; terminal, la facture est archivée), `approved` (lève la contestation).
  - Un statut `null` vaut « implicitly approved ».
  - Les transitions invalides renvoient 422.
  — [Référence, Update e-invoice status for a supplier invoice](https://pennylane.readme.io/reference/putsupplierinvoiceeinvoicestatus.md)
- Il existe aussi un endpoint d'import de facture fournisseur e-invoice (`POST /api/external/v2/supplier_invoices/e_invoices/imports`, scope `supplier_invoices:all`). — [Changelog, dedicated endpoints](https://pennylane.readme.io/changelog/customer-supplier-invoices-dedicated-e-invoice-import-endpoints)
- Le changelog mentionne aussi :
  - « Supplier invoices: expose and filter by electronic billing address » ;
  - « E-invoicing flow field and flow_id filter » ;
  - « Original Supplier Invoice File Retrieval ».
  Le champ `import_source` des factures fournisseurs ne concerne que les connecteurs de fichiers (Dropbox, Drive) : il ne permet pas d'identifier une facture reçue par la PA. — [Changelog, import_source](https://pennylane.readme.io/changelog/new-import_source-field-on-supplier-invoices-endpoints.md) ; [Changelog (index)](https://pennylane.readme.io/changelog)

**Authentification**
- Company API : token développeur ou OAuth 2.0, « Default for all accounts with an Essential plan or higher ».
- Firm API : token ou OAuth de cabinet, « Default for firms ».
- Firm Group API : « On request ».
— [Pennylane, Choose the Right API](https://pennylane.readme.io/docs/what-apis-are-available)
- Token développeur de société :
  - généré par un admin (rôles Executive, Internal Accountant, External Accountant) dans Settings > Connectivity > Developers ;
  - **plan Essential ou supérieur** ;
  - « A token is always linked to one company » ; pour plusieurs sociétés, « each company requires its own token » ;
  - expiration au choix : 1, 6, 12 mois ou illimitée ;
  - accès lecture seule ou lecture-écriture ;
  - le token n'est pas récupérable après création.
  — [Pennylane, Create a Company API Token](https://pennylane.readme.io/docs/generating-my-api-token)
- OAuth 2.0 (authorization code, avec refresh et révocation) : l'app doit être enregistrée auprès de l'**équipe Partenariats**. Après validation, l'intégrateur reçoit un Client ID et un Client Secret, qui ne sont pas récupérables ensuite. — [Pennylane, Implement OAuth 2.0](https://pennylane.readme.io/docs/oauth-20-walkthrough) ; [Getting started](https://pennylane.readme.io/docs/getting-started)
- Incohérence de la spec OpenAPI : le bloc `securitySchemes` ne liste que le scope `ledger`, alors que les endpoints e-invoice exigent `customer_invoices:all` ou `supplier_invoices:all`. — [Référence, send_to_pa](https://pennylane.readme.io/reference/sendtopacustomerinvoice)
- Rate limit v2 : « up to 25 requests every 5 seconds », par token. Il s'applique en production comme en sandbox. Un dépassement renvoie 429 avec les en-têtes `retry-after` et `ratelimit-*`. — [Pennylane, Rate Limiting in API v2](https://pennylane.readme.io/docs/rate-limiting-1)

**Sandbox et programmes partenaires**
- Sandbox intégrateur : il faut écrire à l'équipe Partenariats avec son nom et l'email à utiliser. Elle n'est pas en libre-service (selon le résumé de recherche de la doc Getting started). — [Pennylane, Getting started](https://pennylane.readme.io/docs/getting-started)
- **Programme « Solution Compatible »** (page sans date, © 2026 ; dernier jalon cité : « Février 2026 : participation au pilote DGFiP ») :
  - les éditeurs connectent leur logiciel à la PA Pennylane via des « API REST AFNOR » ;
  - formats acceptés : Factur-X, EDI, PDF signé, UBL, CII, avec conversion par Pennylane ;
  - « 0€ de coût : aucun frais de licence ou d'usage pour votre solution » ;
  - « +120 éditeurs » ;
  - sandbox et accompagnement dédié ;
  - intégration en 2 à 4 semaines en moyenne ;
  - marque grise seulement, pas de marque blanche ;
  - clause de non-sollicitation des clients de l'éditeur ;
  - « La PA est utilisable par des clients français uniquement » ;
  - candidature via un Typeform.
  La page **ne dit pas** si chaque client final doit avoir un abonnement Pennylane payant. — [Pennylane, Solutions Compatibles PA](https://www.pennylane.com/fr/facture-electronique-scpa)
- La fiche PDP Pennylane mentionne une « API ouverte pour faciliter les intégrations ». — [Pennylane, fiche PDP (01/04/2026)](https://www.pennylane.com/fr/fiches-pratiques/facture-electronique/pdp)

### Inferences
- Pour CIPL, le flux minimal est le suivant :
  1. Générer le Factur-X.
  2. L'importer via `POST /customer_invoices/e_invoices/imports` avec `invoice_options.customer_id`.
  3. Attendre `schematron_validation_status = valid`.
  4. Appeler `POST /customer_invoices/{id}/send_to_pa` (409 tant que le traitement n'est pas fini).
  5. Suivre le statut via le webhook `customer_invoice.e_invoicing_status_updated` ou par polling.
  Ce flux est plus sûr que `send_to_pa=true`, qui envoie avant la validation Schematron.
- Le client doit exister côté Pennylane (`customer_id`). CIPL devra donc créer ou synchroniser les locataires professionnels comme clients Pennylane.
- La règle « customer is not a company » de la conversion Factur-X laisse penser que les locataires particuliers ne passent pas par l'e-invoicing chez Pennylane. Les quittances aux particuliers relèvent du B2C et de l'e-reporting, hors e-facture. Ce point n'est confirmé que pour l'endpoint de conversion, pas pour l'import e-invoice.
- Un logiciel de bureau comme CIPL ne peut pas recevoir de webhooks sans serveur. Il lui faudra soit faire du polling, soit relayer les webhooks par un backend (CIPL a déjà Supabase).
- Il y a donc deux voies d'intégration :
  - **Company API** : 1 token par SCI, chaque SCI abonnée au plan Essentiel ou plus.
  - **API AFNOR « Solution Compatible »** : gratuite pour l'éditeur, normalisée, donc réutilisable avec d'autres PA. Le coût pour le client final n'est pas documenté.

### Gaps
- Dates exactes des entrées du changelog (send_to_pa, endpoints dédiés, webhooks) : les pages ne sont pas datées. « Mars 2026 » pour les endpoints dédiés ne vient que d'un résumé de recherche, non vérifié.
- Conditions précises d'éligibilité de `send_to_pa` (adresse de facturation électronique du client, mandat PA signé par la société) : non documentées.
- Conditions du programme partenaires OAuth (critères de validation, délais, contrat, frais) : non publiques, il faut contacter l'équipe Partenariats.
- Spécification exacte de l'« API REST AFNOR » proposée (norme XP Z12-013 ? version ?) : la page ne la nomme pas.
- Je n'ai pas consulté le contenu détaillé de l'endpoint « List PA Registrations » ni la gestion du mandat et de l'annuaire PPF (adresse de réception).

## 3. Tarifs pour une petite SCI (quelques dizaines de factures par mois)

### Takeaway
La SCI est rangée dans l'onglet « Indépendant » de la grille Pennylane. La facturation électronique (émission et réception via la PA) est **incluse dans tous les plans, sans frais par facture**, dans la limite de 1 200 factures par an. Mais l'**API n'est disponible qu'à partir du plan Essentiel, 24 € HT/mois** (Premium : 79 € HT/mois avec module comptable). L'offre gratuite est réservée aux micro-entreprises, n'a pas d'API documentée, et l'éligibilité d'une SCI n'est pas précisée.

### Cited Findings
- Grille « Indépendant (EI, SASU, EURL, SCI) », prix mensuels HT, 1 utilisateur, « jusqu'à 1200 factures/an » sur chaque plan :

  | Plan | Prix HT/mois | API |
  |---|---|---|
  | Starter | 7 € | non listée |
  | Basique | 14 € | non listée |
  | Essentiel | 24 € | « API et intégrations avec vos outils du quotidien » |
  | Premium | 79 € (avec « Module comptable ») | incluse |

  « Tous les plans sont conformes à la facturation électronique. » Pas de plan gratuit sur cette page, mais un essai de 15 jours sans engagement. Aucun frais par facture mentionné. — [Pennylane, tarifs](https://www.pennylane.com/fr/tarifs) ; [Pennylane, tarifs/small](https://www.pennylane.com/fr/tarifs/small)
- La doc API confirme : Company API « Default for all accounts with an Essential plan or higher ». — [Pennylane, Choose the Right API](https://pennylane.readme.io/docs/what-apis-are-available)
- La PA est « incluse gratuitement pour ses clients ». Pennylane a fait « le choix de proposer des services de facturation électronique gratuits ». — [Pennylane, fiche PDP (01/04/2026)](https://www.pennylane.com/fr/fiches-pratiques/facture-electronique/pdp)
- Le comparatif Pennylane indique, pour la PA : « Offre gratuite pour les micro-entreprise et à partir de 7€ / mois ». La PA est « incluse dans l'abonnement, sans surcoût ». — [Pennylane, comparatif PA (07/08/2026)](https://www.pennylane.com/fr/fiches-pratiques/facture-electronique/comparatif-des-plateformes-agreees)
- Offre gratuite :
  - « Offre réservée aux micro-entreprises » ;
  - émission et réception, « Transmission via Plateforme Agréée incluse », Factur-X généré ;
  - « 1 utilisateur et jusqu'à 1 200 factures/an (~100/mois) » ;
  - « Gratuit pour toujours pour les micro-entreprises » ;
  - l'adresse de réception est générée après signature d'un mandat en ligne.
  L'accès API n'est pas mentionné et « micro-entreprise » n'est pas définie (régime fiscal ou catégorie INSEE ?). La page n'est pas datée (© 2026). — [Pennylane, facture électronique gratuite](https://www.pennylane.com/fr/facture-electronique-gratuite)
- **Écart sur le mode de facturation** : un comparatif affilié (« Mis à jour le 13 août 2026 », tarifs « vérifiés en mars 2026 ») dit que 7 €/mois correspond à une facturation annuelle, le mensuel sans engagement étant « environ +20 % ». Il dit aussi que les offres sociétés sont « sur devis » et que l'API est absente du Basique mais présente dans l'Essentiel et le Premium. La page officielle n'affiche que des prix mensuels, sans préciser. — [comparatif-facture-electronique.fr (13/08/2026, affilié)](https://www.comparatif-facture-electronique.fr/pennylane-avis-test/)
- Un site tiers affilié confirme Starter 7 €, Basique 14 €, Essentiel 24 €, Premium 79 €, sans engagement minimum. Il cite aussi Premium 5 utilisateurs à 149 € ou 298 €, et des licences fournies par un cabinet comptable intégrées aux honoraires (« souvent 100 à 200 € par mois », jugé opaque). — [ma-facture-electronique.org (02/10/2026, affilié)](https://ma-facture-electronique.org/plateforme-agreee/liste-officielle/pennylane/)
- Le résumé de recherche mentionne une grille TPE (Basique 29 € HT pour 5 utilisateurs, Essentiel 49 € HT, 99 € HT pour 10 utilisateurs) qui se contredit d'une page à l'autre. **Non vérifié** : les pages fetchées n'affichaient pas les prix TPE. Les prix PME et ETI sont « personnalisés ». — [Pennylane, tarifs/medium](https://www.pennylane.com/fr/tarifs/medium) ; [Pennylane, pme/tarifs](https://www.pennylane.com/fr/pme/tarifs)

### Inferences
- Pour une SCI qui émet via l'API depuis CIPL, le plan minimal est **Essentiel, 24 € HT/mois** (environ 288 € HT/an), ou un peu moins en engagement annuel si le +20 % mensuel se confirme.
- La limite de 1 200 factures/an (environ 100/mois) couvre « quelques dizaines par mois ».
- Une SCI qui ne veut que la conformité, sans API (saisie dans l'interface Pennylane), peut se contenter de Starter à 7 €. Elle perd alors l'intégration avec CIPL.
- Pour l'éditeur, la voie « Solution Compatible » (API AFNOR) est annoncée à 0 €. Si elle n'exigeait pas d'abonnement Essentiel par client, ce serait la voie la moins chère, mais ce n'est pas documenté.

### Gaps
- Prix en facturation annuelle, et existence ou non de l'écart de +20 % : non confirmés sur la page officielle.
- Une SCI est-elle éligible à l'offre gratuite « micro-entreprises » ? Non précisé. Une SCI à l'IR n'est pas une micro-entreprise au sens fiscal du terme. À demander à Pennylane.
- L'offre gratuite donne-t-elle accès à l'API ? Non précisé, probablement non puisque l'API commence à Essentiel.
- Coût pour le client final d'une PA activée via une « Solution Compatible » : non publié.
- Frais en cas de dépassement des 1 200 factures/an : non publiés (le comparatif affilié évoque des « coûts supplémentaires à vérifier »).

## 4. Multi-sociétés : un abonnement et un token par SCI ?

### Takeaway
Oui. Chaque société (donc chaque SCI) a son propre espace et son propre abonnement, et un token développeur est lié à une seule société. Aucun tarif groupe ou multi-entités n'est publié. Le seul accès multi-sociétés natif est la **Firm API** (cabinets comptables, `X-Company-Id`) ou une **app OAuth** que chaque société autorise. Aucune des deux ne supprime l'abonnement par société.

### Cited Findings
- « A token is always linked to one company ». Pour plusieurs sociétés, « each company requires its own token ». — [Pennylane, Create a Company API Token](https://pennylane.readme.io/docs/generating-my-api-token)
- La Firm API (« Default for firms ») donne accès au portefeuille de sociétés clientes d'un cabinet. La Firm Group API est « On request ». — [Pennylane, Choose the Right API](https://pennylane.readme.io/docs/what-apis-are-available)
- `X-Company-Id` est « Required when using a firm token » et ignoré avec un token lié à une société. — [Référence, Import a customer e-invoice](https://pennylane.readme.io/reference/createcustomerinvoiceeinvoiceimport)
- Un token d'app OAuth crée des abonnements webhook « app-wide » qui couvrent toutes les sociétés accessibles par l'app. — [Pennylane, Subscriber types & company scoping](https://pennylane.readme.io/docs/authorization-new.md)
- Ma-facture-electronique.org (affilié, 02/10/2026) : chaque entité juridique a besoin de son propre abonnement, sans vue consolidée ni remise de volume pour les groupes. Exemple donné : une holding et 3 filiales, soit 4 espaces séparés. — [ma-facture-electronique.org](https://ma-facture-electronique.org/plateforme-agreee/liste-officielle/pennylane/)
- La page tarifs ne mentionne aucun tarif multi-sociétés ou groupe. Les prix PME et ETI sont « personnalisés ». — [Pennylane, tarifs](https://www.pennylane.com/fr/tarifs)
- Un comparatif affilié note « pas de gestion multi-entités avancée ». — [comparatif-facture-electronique.fr (13/08/2026)](https://www.comparatif-facture-electronique.fr/pennylane-avis-test/)

### Inferences
- Pour N SCI émettant via l'API, il faut N abonnements Essentiel (N × 24 € HT/mois) et N tokens à saisir et stocker dans CIPL. Une app OAuth éviterait la gestion manuelle des tokens (une autorisation par SCI) mais demande la validation de l'équipe Partenariats.
- Une Firm API n'est pertinente que si les SCI sont suivies par un cabinet comptable sur Pennylane. Ce n'est pas un levier tarifaire pour CIPL.

### Gaps
- Je n'ai trouvé aucune remise multi-entités officielle. Une offre « Custom » existe pour les TPE ou PME, mais ses conditions ne sont pas publiques.

## 5. Limites connues et plaintes sur l'API d'émission

### Takeaway
Peu de retours développeurs publics et détaillés. Les limites visibles viennent surtout de la doc elle-même :
- envoi `send_to_pa` avant la validation Schematron ;
- UBL et CII seulement en alpha ;
- incohérences de scopes dans la spec OpenAPI ;
- réponse d'import minimale ;
- sandbox et OAuth sur demande ;
- migration d'API forcée en 2026.
Une plainte ancienne sur l'absence de suivi du cycle de vie semble traitée, puisque des webhooks de statut existent désormais.

### Cited Findings
- `send_to_pa=true` envoie « before Schematron validation ». Conséquence : « the invoice may be found invalid after it has already been submitted to the PA ». — [Changelog, send to PA](https://pennylane.readme.io/changelog/automatically-send-to-pa-on-customer-e-invoice-import)
- UBL et CII seuls sont en « alpha » : pas d'affichage, et la facture n'est pas reconnue comme électronique dans Pennylane. La réponse 201 ne contient que `id` et `url`. Le champ `e_invoicing.status` est évoqué mais pas défini dans le schéma de réponse. Le rate limit n'est pas indiqué sur cette page. — [Référence, Import a customer e-invoice](https://pennylane.readme.io/reference/createcustomerinvoiceeinvoiceimport)
- Le scope `ledger` figure dans `securitySchemes` alors que les endpoints exigent `customer_invoices:all` ou `supplier_invoices:all`. Les exemples d'erreur 422 sont génériques (« Entry lines are not balanced »). Aucune erreur d'éligibilité n'est documentée pour `send_to_pa`. — [Référence, send_to_pa](https://pennylane.readme.io/reference/sendtopacustomerinvoice) ; [Référence, e_invoice_status fournisseur](https://pennylane.readme.io/reference/putsupplierinvoiceeinvoicestatus.md)
- L'endpoint global `e_invoices/imports` est déprécié et le scope `e_invoices:all` ne peut plus être émis : il faut migrer. — [Changelog, dedicated endpoints](https://pennylane.readme.io/changelog/customer-supplier-invoices-dedicated-e-invoice-import-endpoints)
- Les changements de l'API en 2026 sont cassants (scopes, pagination, réponses), avec un passage par défaut le 08/04/2026 et sans retour arrière possible après le 01/07/2026. — [Pennylane, 2026 API changes guide](https://pennylane.readme.io/docs/2026-api-changes-guide.md)
- Rate limit : 25 requêtes par 5 secondes et par token. — [Pennylane, Rate Limiting v2](https://pennylane.readme.io/docs/rate-limiting-1)
- Un développeur a posté sur le forum de la doc v1.0 qu'il voulait émettre depuis son ERP via la PA. Selon lui, l'endpoint e-invoicing ne renvoyait qu'une URL, sans webhooks ni suivi du cycle de vie (déposée, rejetée, acceptée). La page est aujourd'hui en **404**, sa date n'est pas vérifiable, et ce résumé ne vient que du moteur de recherche. — [forum readme v1.0 (404)](https://pennylane.readme.io/v1.0/discuss/696384938dff07efca60f20e). Depuis, le webhook `customer_invoice.e_invoicing_status_updated` existe. — [List of webhook events](https://pennylane.readme.io/docs/list-of-events.md)
- Limites citées par des comparatifs commerciaux, non vérifiées, à pondérer à cause de l'affiliation :
  - gestion multi-entités ;
  - plafond de factures de l'offre gratuite ;
  - support souvent relayé par le cabinet comptable ;
  - « Certaines intégrations externes limitées ».
  — [ma-facture-electronique.org (affilié)](https://ma-facture-electronique.org/plateforme-agreee/liste-officielle/pennylane/) ; [comparatif-facture-electronique.fr (affilié)](https://www.comparatif-facture-electronique.fr/pennylane-avis-test/)
- La PA n'est disponible qu'en marque grise et seulement pour des clients français. — [Pennylane, Solutions Compatibles PA](https://www.pennylane.com/fr/facture-electronique-scpa)

### Inferences
- Pour CIPL, les principaux risques d'intégration sont les suivants :
  1. **Validation asynchrone** : il faut gérer les états `pending`/`invalid` et le 409, plutôt que de faire confiance au 201.
  2. **Pas de webhook reçu directement par une app de bureau** : polling ou relais serveur.
  3. **Gestion de N tokens à expiration choisie**, sans possibilité de les relire.
  4. **API qui évolue vite** : deux dépréciations en 2026.
- Un avantage pour la comparaison : la doc est publique et riche (webhooks typés, statuts du cycle de vie conformes à la réforme : approved, in_dispute, refused, collected), et la PA est incluse sans frais par facture.

### Gaps
- Je n'ai trouvé aucun avis développeur détaillé et daté de 2026 sur l'émission via l'API Pennylane (GitHub, forums, Reddit). Les « retours négatifs » évoqués par les comparatifs ne sont pas sourcés.
- Pas de données publiques sur la fiabilité ou les délais de transmission PA vers PA ou PEPPOL.
