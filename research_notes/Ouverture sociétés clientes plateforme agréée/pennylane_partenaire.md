# Pennylane : la voie partenaire pour un éditeur qui sert de nombreuses sociétés clientes (état au 24 octobre 2026)

Point de vue : CIPL, logiciel Windows de gestion locative vendu par abonnement. Il dépose déjà le Factur-X d'une SCI dans Pennylane par l'API, avec un token « Company » que le client colle dans les réglages. L'envoi à la PA n'est pas encore codé.
Recherche faite le 24/10/2026, en complément de `research_notes/Plateformes agréées facturation électronique/pennylane_reference.md` (22/10/2026). Ce qui y est déjà établi n'est pas repris, sauf pour le recouper : import Factur-X, `send_to_pa`, webhooks, tarifs 7/14/24/79 €, un token par société, programme « Solution Compatible ».
Les pages pennylane.readme.io et les pages commerciales Pennylane (© 2026) ne sont pas datées : elles ont été consultées le 24/10/2026. Les articles du centre d'aide affichent « Mis à jour hier », soit environ le 23/10/2026. **Toute source antérieure à 2026 est signalée.**

## 1. Programme partenaires et OAuth : conditions, coût, délais, candidature ; ce qu'une app partenaire permet de plus qu'un token par société

### Takeaway
Pennylane a **deux voies partenaires utiles à CIPL**. Aucune ne publie de conditions ni de délai d'approbation. **Aucune n'affiche de frais pour l'éditeur.** Une seule publie un chiffre de coût : le programme PA, à 0 €.
- **Partenaire technologique** : une app OAuth est développée, certifiée, puis publiée sur la marketplace. L'OAuth est obligatoire pour une intégration partenaire en production.
- **Solution Compatible PA** : API REST AFNOR, 0 € pour l'éditeur, 2 à 4 semaines d'intégration.

Face au token par société, l'app OAuth apporte trois choses : le client donne son consentement en un clic au lieu de copier-coller un token, des webhooks valables pour toutes les sociétés connectées, et la visibilité marketplace. Elle **n'enlève aucune exigence d'abonnement documentée** (voir Q3).

### Cited Findings
**Pages partenaires**
- La page « Partenaires » liste cinq parcours :
  - **Intégrateur**, pour les projets et migrations ;
  - **Affilié**, via Affilae ;
  - **Prescripteur** ;
  - **éditeurs ERP ou de facturation qui veulent « proposer Pennylane comme PA »** : « Contactez notre équipe partenariats », lien vers `/fr/facture-electronique-scpa` ;
  - **connecteur API** : « contactez notre équipe partenariat connectivité », lien vers `/fr/partenaires/partenaire-technologique`.

  La page ne donne aucune condition, aucun coût, aucun délai et aucune commission. — [Pennylane, Partenaires (© 2026)](https://www.pennylane.com/fr/partenaires)
- La page « Partenaire technologique » s'adresse aux éditeurs ComptaTech et FinTech. On y rejoint « une sélection de plus de 300 éditeurs reconnus ». Le parcours a trois étapes :
  1. le développement, avec un accompagnement et la documentation ;
  2. la **certification** : l'équipe vérifie que l'app « respecte nos standards de qualité » ;
  3. la **publication** sur la marketplace, accessible à toutes les entreprises et à tous les cabinets.

  La page n'indique ni coût, ni délai, ni critère d'éligibilité, ni contrat. — [Pennylane, Partenaire technologique (© 2026)](https://www.pennylane.com/fr/partenaires/partenaire-technologique)
- Programme « Solution Compatible PA » :
  - candidature par formulaire (« Devenez partenaire gratuitement »), puis 4 étapes : candidature, intégration via les « API REST AFNOR », tests en sandbox avec une équipe connectivité, mise en production et kit marketing ;
  - « Entre 2 et 4 semaines en moyenne » ;
  - « 0€ de coût : aucun frais de licence ou d'usage pour votre solution » ;
  - « +120 éditeurs » ;
  - clause de non-sollicitation des clients de l'éditeur dans le contrat ;
  - marque grise seulement : le portail peut prendre les couleurs de l'éditeur, mais Pennylane reste identifiée comme PA ;
  - clients français uniquement.

  Jalons affichés : PEPPOL depuis 2023, immatriculation en septembre 2024, immatriculation définitive en décembre 2025, pilote DGFiP en février 2026. — [Pennylane, Solutions Compatibles PA (© 2026)](https://www.pennylane.com/fr/facture-electronique-scpa)

**Ce que dit la documentation sur l'OAuth**
- Pour utiliser l'OAuth, « register your app with Pennylane by contacting our Partnerships team ». Après validation, l'éditeur reçoit un Client ID et un Client Secret, qui ne sont pas récupérables. En cas de perte, il faut créer une nouvelle app, ce qui « may take time ».
  - Jetons : l'access token expire après 24 h. Le refresh token est valable 90 jours et chaque usage l'invalide (rotation). Le code d'autorisation sert une seule fois et expire après 5 min.
  - Consentement : « The user is prompted to select one of their available companies or firms and grant access ». Un token de société est « Granted for a single company ». Un token de cabinet est « Granted at the firm level ».
  - La page ne dit rien d'un coût, d'un délai de validation ou d'une exigence de plan. — [Pennylane API, Implement OAuth 2.0](https://pennylane.readme.io/docs/oauth-20-walkthrough.md)
- « Partner integrations must use OAuth 2.0 for authentication. » « OAuth 2.0 is required for production integrations published on the Marketplace. » En sandbox, un token Company suffit pour tester. — [Pennylane API, Import a Customer Invoice via API](https://pennylane.readme.io/docs/customer-invoicing)
- Le centre d'aide ajoute qu'au-delà d'un certain volume, « Une authentification OAuth 2.0 est alors nécessaire ». Pour développer une intégration avec un outil tiers, on soumet un formulaire sur la page partenaires, et l'OAuth se demande une fois la candidature validée. Une sandbox ne coûte rien de plus (« Créer une sandbox n'occasionne pas de surcoût »), mais elle hérite de l'abonnement de l'espace principal et il faut le plan Essentiel pour y générer des tokens. — [Centre d'aide Pennylane, Utiliser les API publiques (màj ~23/10/2026)](https://help.pennylane.com/fr/articles/18770-utiliser-les-api-publiques-pennylane)
- Rappel des notes du 22/10 : un token d'app OAuth (client credentials) crée des abonnements webhook « app-wide », qui couvrent toutes les sociétés accessibles par l'app. Un token développeur ne couvre que sa société. — [Pennylane API, Subscriber types & company scoping](https://pennylane.readme.io/docs/authorization-new.md)

**Sources tierces, non vérifiées**
- Le programme intégrateurs et revendeurs date de janvier 2025 (**antérieur à 2026**). Il vise l'accompagnement des migrations, pas les éditeurs de connecteurs. — [Pennylane, lancement du programme intégrateurs](https://www.pennylane.com/fr/blog/pennylane-news/lancement-programme-integrateurs)
- Une adresse `integrations@pennylane.com` pour l'enregistrement, une sandbox fournie par Pennylane et une limite d'environ 60 requêtes par minute sur certains endpoints sont citées par des sites tiers d'intégrateurs. **Je ne les ai pas trouvées dans une source officielle.** — [Apideck, How to integrate with the Pennylane API](https://www.apideck.com/blog/how-to-integrate-with-the-pennylane-api) ; [tout-pour-la-gestion.com](https://blog.tout-pour-la-gestion.com/api-pennylane-guide-complet-2026-automatiser-votre-comptabilite-e-commerce-caisse-negoce/)

### Inferences
**Ce que l'app OAuth change par rapport au token collé**
- Le client clique sur « Connecter Pennylane » dans CIPL et choisit sa société. Il n'a plus à générer, copier ni renouveler un token.
- Il faut autant d'autorisations que de SCI, puisque le consentement porte sur une société à la fois. Exception : un cabinet qui autorise au niveau firm couvre d'un coup toutes ses sociétés.
- Un seul abonnement webhook couvre toutes les SCI, mais il faut un serveur pour le recevoir. CIPL a Supabase.

**Les contraintes qui viennent avec**
- Il faut faire tourner les refresh tokens au moins une fois tous les 90 jours.
- Il faut garder le Client Secret hors du poste client. En pratique, l'échange de code et le refresh passent donc par le backend CIPL. C'est une bonne pratique OAuth générale, pas une exigence écrite de Pennylane.
- Le token développeur actuel, lui, peut être illimité : c'est plus simple pour une app de bureau, mais plus fragile côté sécurité.

**Deux candidatures distinctes**
- La voie « Solution Compatible », via l'API AFNOR et la marque grise, et la voie « Partenaire technologique », via l'API v2 en OAuth et la marketplace, passent par des équipes différentes (« partenariats » et « partenariat connectivité »). CIPL devra probablement candidater à celle qui correspond à son usage, ou aux deux.

### Gaps
- Critères d'acceptation, délai de validation d'une app OAuth, contrat et éventuels frais ou commissions du programme technologique : **non publiés**.
- Spécification exacte de l'« API REST AFNOR » de Pennylane (XP Z12-013 ? version ? URL de doc) : introuvable publiquement.
- Le « Partenaire technologique » impose-t-il une taille minimale ou un nombre de clients ? Non dit.

## 2. Un partenaire peut-il créer ou rattacher la société Pennylane de son client par API, ou chaque client doit-il ouvrir lui-même chaque société ?

### Takeaway
**Non, rien de documenté ne le permet à un éditeur.**
- Ni la Company API ni la Firm API ne créent de société.
- La seule API qui crée des sociétés est la **Firm Group API**, réservée aux groupes de cabinets comptables et ouverte sur demande.
- L'inscription PA (consentement, mandat, annuaire) se fait dans Pennylane. L'API ne permet que de **lire** son statut (`GET /pa_registrations`).

Chaque SCI doit donc avoir sa propre société Pennylane, ouverte par le client ou par son cabinet, qui peut inviter en masse. CIPL s'y rattache ensuite par token ou par OAuth.

### Cited Findings
**Création de sociétés**
- Firm Group API : « Creating companies and users in the firm context ». Elle vise les « Accounting firms (advanced use cases) » et n'est accessible qu'« On request », via le support ou l'Account Manager. La Company API couvre « Companies and integration partners ». La Firm API couvre « Accounting firms and integration partners », avec l'accès au portefeuille et à la liste des sociétés. — [Pennylane API, Choose the Right API](https://pennylane.readme.io/docs/what-apis-are-available)
- Le centre d'aide précise que la Firm API permet de lister les sociétés clientes et de lire leurs dossiers, mais pas d'en créer. La Firm Group API sert aux groupes de cabinets pour les traitements de masse et les migrations, sur demande. Elle a un endpoint de churn, qui retire une société de Pennylane, « limité à 50 appels par heure, par groupe de cabinets ». — [Centre d'aide Pennylane, Utiliser les API publiques](https://help.pennylane.com/fr/articles/18770-utiliser-les-api-publiques-pennylane)
- L'index de la documentation (llms.txt) ne contient aucun endpoint ni guide « create company » ou « create dossier » pour la Company API ou la Firm API. — [pennylane.readme.io/llms.txt](https://pennylane.readme.io/llms.txt)
- Un token Firm agit sur « any company your firm manages », en passant `X-Company-Id`. Une société qui n'est pas gérée par le cabinet renvoie 404. — [Pennylane API, Create a Firm API Token](https://pennylane.readme.io/docs/firm-api.md)

**Inscription PA : en lecture seule par l'API**
- `GET /api/external/v2/pa_registrations` demande le scope `pa_registrations:readonly`. Sa description : « Use this to determine whether the company has completed PA onboarding ».
- Champs de réponse :
  - `emission_activated` (booléen) ;
  - pour chaque élément : `siren`, `siret` (null = niveau SIREN), `exchange_direction` (`emission`, `reception` ou `emission_and_reception`) et `status`.
- Valeurs de `status` :
  - `pending` : consentement donné, inscription soumise à l'annuaire ;
  - `activated` ;
  - `draft` ;
  - `conflict` : adresse revendiquée ailleurs ;
  - `ineligible` ;
  - `lost` : adresse partie chez une autre PA, par portabilité ;
  - `provisioned` et `archived` : dépréciées.
- **Aucun endpoint de création** et aucun champ de mandat ou d'adresse. — [Pennylane API, List PA Registrations](https://pennylane.readme.io/reference/getparegistrations.md)

**Côté cabinet et côté éditeur**
- Pennylane permet aux cabinets d'« inscrire vos clients dans l'annuaire en quelques clics » et d'« Invite[r] en masse ou individuellement vos clients à utiliser la Plateforme Agréée Pennylane ». Le parcours comprend le « recueil et signature de leur mandat », sans dire qui signe ni comment. Le cabinet suit la conformité depuis un tableau de bord et peut relancer. — [Pennylane, PA pour experts-comptables (© 2026)](https://www.pennylane.com/fr/expert-comptable/pdp)
- Un expert-comptable peut finaliser l'inscription à la place du client. La formule « Collaboratif » est celle que le cabinet ouvre généralement pour son client. — [digitiz.fr, Pennylane facturation électronique (13/10/2026)](https://digitiz.fr/pennylane-facturation-electronique/)
- Solution Compatible : les clients peuvent « activer la PA Pennylane directement depuis votre solution » une fois l'intégration en production. La page ne dit pas comment le compte Pennylane du client est créé, ni qui signe le mandat. — [Pennylane, Solutions Compatibles PA](https://www.pennylane.com/fr/facture-electronique-scpa)
- La page de l'offre gratuite précise que l'adresse de réception est générée après la signature d'un mandat en ligne (rappel des notes du 22/10). — [Pennylane, facture électronique gratuite](https://www.pennylane.com/fr/facture-electronique-gratuite)

### Inferences
**Parcours réaliste pour un client CIPL qui a 3 SCI**
1. Il crée 3 sociétés Pennylane, ou son cabinet les crée ou l'y invite.
2. Il signe le mandat PA dans chacune.
3. Il connecte chacune à CIPL, par token ou par OAuth.
4. CIPL contrôle `pa_registrations` (`activated`, `emission_activated`) avant d'autoriser l'envoi.

**Pourquoi la Firm Group API n'est pas une piste**
- Elle s'adresse à des groupes de cabinets comptables et dépend d'une décision commerciale de Pennylane. Rien n'indique qu'un éditeur de gestion locative puisse l'obtenir.

**La seule piste de création « depuis CIPL »**
- C'est le parcours « activer la PA depuis votre solution » du programme Solution Compatible. Son mécanisme (redirection vers un tunnel Pennylane ? compte créé par Pennylane ?) n'est pas documenté et doit être demandé.

### Gaps
- Le parcours d'activation « depuis votre solution » du programme Solution Compatible crée-t-il un compte ou une société Pennylane complète, ou un simple espace PA ? Qui signe le mandat ? Non documenté.
- L'utilisateur Pennylane d'un client peut-il ajouter plusieurs sociétés (SCI) sous un même login, et comment ? Non trouvé dans les sources consultées.

## 3. Abonnement par société : plan minimal pour l'API et pour l'e-invoicing, prix HT, remises multi-sociétés, réception seule gratuite

### Takeaway
**Oui, chaque société a son abonnement.**
- La facturation électronique, en émission comme en réception, est incluse dans tous les plans, y compris Starter à 7 €.
- L'API et la marketplace commencent au plan **Essentiel, 24 € HT/mois** pour 1 utilisateur.
- Starter impose d'ouvrir un Compte Pro Pennylane.
- L'offre gratuite est réservée aux micro-entrepreneurs sans TVA, donc pas aux SCI.

Je n'ai trouvé **aucune remise multi-sociétés publique** ni d'offre « réception seule » gratuite pour une SCI. Les seules facturations groupées sont côté cabinet : le plan Collaboratif est « généralement payé par le cabinet », et une offre à 4 €/dossier a été annoncée en 2025, sans confirmation en 2026.

### Cited Findings
**Plans et prix**
- Centre d'aide, liste des plans pour entreprises :
  - **Collaboratif** : « disponible uniquement pour les entreprises invitées par leur cabinet », « généralement payé par le cabinet ». Factures dans un format « compatible avec la facturation électronique ».
  - **Starter** : « L'ouverture d'un Compte Pro Pennylane est obligatoire dans les 30 jours qui suivent la souscription au plan Starter », faute de quoi le compte bascule en Basique à 14 € HT/mois.
  - **Basique**.
  - **Essentiel** : signature électronique, notes de frais, prévisionnel et **accès API/Marketplace**.
  - **Premium**.

  L'API et les intégrations tierces sont « disponibles à partir du plan Essentiel », et l'« API Entreprise est accessible avec un abonnement Essentiel et supérieur ». Les prix varient selon le palier d'utilisateurs (1, 5 ou 10+). La page ne dit rien du multi-sociétés ni d'une réception seule. — [Centre d'aide Pennylane, Les abonnements pour les entreprises (màj ~23/10/2026)](https://help.pennylane.com/fr/articles/185068-les-abonnements-pennylane-pour-les-entreprises) ; [Centre d'aide, Utiliser les API publiques](https://help.pennylane.com/fr/articles/18770-utiliser-les-api-publiques-pennylane)
- Selon digitiz.fr (13/10/2026, prix vérifiés le 12/10/2026), en € HT par mois :

  | Plan | 1 utilisateur | 2 utilisateurs | Contenu ajouté |
  |---|---|---|---|
  | Gratuit | 0 | — | « micro-entrepreneurs sans TVA » ; pas d'accès comptable, pas de synchro bancaire, pas de contestation |
  | Starter | 7 | — | exige le Compte Pro |
  | Basique | 14 | 29 | — |
  | Essentiel | 24 | 49 | — |
  | Premium | 79 | 149 | — |

  E-facturation incluse dans tous les plans jusqu'à 1 200 factures par an (2 400 avec 2 utilisateurs) : « vous ne payez jamais de supplément pour la facturation électronique ». La page ne cite aucune offre multi-sociétés et aucune réception seule. — [digitiz.fr (13/10/2026)](https://digitiz.fr/pennylane-facturation-electronique/)
- La Company API est « Default for all accounts with an Essential plan or higher ». La page ne dit pas si une app OAuth partenaire peut accéder à une société en Starter ou en Basique. — [Pennylane API, Choose the Right API](https://pennylane.readme.io/docs/what-apis-are-available)

**Côté cabinet**
- La PA « est gratuite pour les cabinets qui tiennent la comptabilité de leurs clients sur Pennylane ». Les clients ont des offres TPE-PME « à partir de 14€/mois ». — [Pennylane, PA pour experts-comptables](https://www.pennylane.com/fr/expert-comptable/pdp)
- **Source antérieure à 2026** (keynote ComptaTech, 26/06/2025) :
  - pour les cabinets qui utilisent d'autres logiciels comptables : « C'est gratuit jusqu'au 1er septembre 2026, puis 4 euros par mois et par dossier », mais « vous n'aurez pas la synchronisation retour des statuts de paiement » ;
  - « finie la limite de 5 factures par mois sur le plan collaboratif » à partir de septembre 2025.

  Je n'ai trouvé **aucune confirmation 2026** de ce tarif de 4 €/dossier. — [Pennylane, keynote ComptaTech 2025 (26/06/2025)](https://www.pennylane.com/fr/blog/contenu-expert-comptable/keynote-comptatech-2025)

**Solution Compatible et sources tierces**
- La page Solution Compatible ne donne aucun prix pour le client final. Elle laisse entendre que l'éditeur peut proposer la PA « offerte/incluse ». — [Pennylane, Solutions Compatibles PA](https://www.pennylane.com/fr/facture-electronique-scpa)
- Selon une synthèse de comparateurs tiers, il n'y a pas de tarif public pour les cabinets, et les entreprises invitées par leur cabinet basculent sur Collaboratif, le plus souvent payé par le cabinet. — [comparateur-efacturation.fr, avis Pennylane 2026](https://comparateur-efacturation.fr/blog/pennylane-avis-tarifs-test-complet-2026) ; [stackindep.fr, prix Pennylane 2026](https://stackindep.fr/facturation/pennylane-prix)

### Inferences
**Voie API v2, celle utilisée aujourd'hui par CIPL**
- Il faut au moins Essentiel par SCI, soit 24 € HT/mois et 288 € HT/an (déjà établi le 22/10).
- Le centre d'aide range l'« accès API/Marketplace » dans Essentiel. Une app OAuth publiée sur la marketplace demanderait donc très probablement aussi Essentiel pour chaque société connectée. Ce n'est pas écrit explicitement.

**Une SCI sans API**
- Elle pourrait se mettre en conformité pour 7 € HT/mois (Starter, avec Compte Pro obligatoire) ou 14 € (Basique), et saisir ou importer ses factures à la main. Elle perdrait alors l'automatisation avec CIPL.

**La seule piste moins chère**
- C'est la voie Solution Compatible (API AFNOR), si elle n'exige pas l'Essentiel. C'est la question n° 1 à poser à Pennylane.

**SCI suivies par un cabinet**
- Si ce cabinet est sur Pennylane, la SCI peut être en Collaboratif payé par le cabinet. Mais Collaboratif n'inclut pas l'API. Pour CIPL, il faudrait alors passer par le token Firm du cabinet, ce qui suppose la coopération de ce cabinet.

### Gaps
- Prix de Collaboratif et du palier « 4 €/dossier » en 2026 : non publiés.
- Remise sur volume pour un éditeur qui amène N sociétés, ou facturation consolidée à l'éditeur : rien trouvé.
- Une SCI à l'IR est-elle éligible au Gratuit ? Probablement non, puisque l'offre est réservée aux « micro-entrepreneurs sans TVA ». Non confirmé par Pennylane.
- Le parcours Solution Compatible exige-t-il un abonnement Pennylane par client final, et lequel ? Non documenté.

## 4. Émission par l'API : envoi à la PA, statuts du cycle de vie, déclaration du statut « encaissée »

### Takeaway
L'envoi à la PA est **documenté et disponible**, sans mention bêta : `POST /customer_invoices/{id}/send_to_pa` ou `send_to_pa=true` à l'import (déjà établi le 22/10). La facture lit son cycle de vie dans `e_invoicing.status`, qui comprend `collected` et `partially_collected`, et dans `lifecycle_events` (bêta).

Côté « encaissée », **aucun endpoint ne pose un statut e-invoicing sur une facture client**. Il existe `PUT /customer_invoices/{id}/mark_as_paid` et le lettrage de transactions bancaires. Selon une source tierce, Pennylane met le statut à jour seul quand il retrouve le paiement. **Le lien entre `mark_as_paid` et l'envoi du statut 212 « Encaissée » à la PA n'est documenté nulle part.**

### Cited Findings
**Paiement**
- `PUT /api/external/v2/customer_invoices/{id}/mark_as_paid` demande le scope `customer_invoices:all`, sans corps. Il renvoie 204 « Customer invoice marked as paid », ou 400/401/403/404/422. Sa description : « Mark a customer invoice as paid. No automatic reconciliation will be done once the invoice is marked as paid ». La page ne mentionne ni l'e-invoicing, ni la PA, ni `collected`, ni le statut 212, ni l'e-reporting. — [Pennylane API, Mark a customer invoice as paid](https://pennylane.readme.io/reference/markaspaidcustomerinvoice.md)
- L'index de la documentation liste aussi « Match a transaction to a customer invoice », « List payments for a customer invoice » et le guide « Automate Payment Matching ». La seule mise à jour de statut e-invoicing documentée concerne les factures **fournisseurs**. Il n'y a **aucune page sur l'e-reporting**. — [pennylane.readme.io/llms.txt](https://pennylane.readme.io/llms.txt)
- Le guide « Payment vs Matched Transaction » distingue deux cas :
  - le **paiement** passe par un moyen Pennylane (Qonto, lien de paiement, GoCardless, Stripe) et le lettrage est automatique ;
  - la **transaction lettrée** correspond à un paiement extérieur, lettré dans l'app ou par l'API.

  Le guide ne parle pas de l'e-invoicing. — [Pennylane API, Payment vs Matched Transaction](https://pennylane.readme.io/docs/what-is-the-difference-between-a-payment-and-a-matched-transaction.md)

**Cycle de vie dans l'objet facture**
- Schéma de la facture client :
  - `e_invoicing`, « E-invoicing lifecycle information managed by the PA », présent uniquement pour les factures envoyées en e-invoicing ;
  - `status` : `accepted`, `approved`, `collected`, `in_dispute`, `partially_collected`, `refused`, `rejected`, `sent`, `submitted` ;
  - `reason` ;
  - `flow.id` : « The AFNOR flow associated with this e-invoice » ;
  - `lifecycle_events` (**bêta**) : types `accepted`, `available_to_buyer`, `emitted_by_seller_platform`, `in_dispute`, `partially_collected`, `received_by_buyer_platform`, `refused`, `rejected`, `sent`, `submitted`. **`collected` en est absent**, ce qui est incohérent avec `status`.
  - Champs de paiement : `paid` (booléen), `status` de premier niveau qui inclut `paid` et `partially_paid`, et `remaining_amount_with_tax`.
  - Aucun texte n'explique ce qui fait passer `e_invoicing.status` à `collected`. — [Pennylane API, Get a customer invoice](https://pennylane.readme.io/reference/getcustomerinvoice.md)

**Qui pose « Encaissée » et comment**
- Fiche Pennylane « cycle de vie » (« Mise à jour le 13 avril 2026 ») : « Encaissée » est posée par **l'entreprise**. C'est l'un des statuts obligatoires, avec Déposée, Rejetée et Refusée, qui « sont obligatoirement transmis par votre PA via l'administration via le PPF ». La fiche ne décrit pas le mécanisme dans Pennylane. — [Pennylane, cycle de vie (13/04/2026)](https://www.pennylane.com/fr/fiches-pratiques/facture-electronique/cycle-de-vie)
- Selon digitiz.fr (13/10/2026) : « Si Pennylane retrouve le paiement tout seul, par exemple sur son compte pro, le statut se met à jour sans rien faire ». Pour un chèque ou un virement non retrouvé, l'utilisateur marque la facture payée à la main. — [digitiz.fr (13/10/2026)](https://digitiz.fr/pennylane-facturation-electronique/)
- **Source antérieure à 2026** (26/06/2025) : Pennylane « maintient votre lettrage comptable comme référence », et ce lettrage remonte vers l'outil de gestion et la PDP. L'offre à 4 €/dossier pour les cabinets sur d'autres outils exclut « la synchronisation retour des statuts de paiement ». — [Pennylane, keynote ComptaTech 2025](https://www.pennylane.com/fr/blog/contenu-expert-comptable/keynote-comptatech-2025)

**Rappels des notes du 22/10 et du changelog**
- Le webhook `customer_invoice.e_invoicing_status_updated` porte les mêmes 9 statuts. `send_to_pa=true` envoie « before Schematron validation ». L'endpoint dédié renvoie 409 tant que le traitement n'est pas terminé. — [Pennylane API, List of webhook events](https://pennylane.readme.io/docs/list-of-events.md) ; [Changelog, send to PA](https://pennylane.readme.io/changelog/automatically-send-to-pa-on-customer-e-invoice-import)
- Le changelog n'affiche **aucune date** par entrée. On ne peut donc pas dater la mise en service de `send_to_pa`, de `schematron_validation_status` ni des endpoints clients et adresses de facturation électronique. — [Pennylane API, Changelog](https://pennylane.readme.io/changelog)

### Inferences
**Flux pour CIPL**
1. Importer le Factur-X.
2. Attendre `schematron_validation_status = valid`.
3. Appeler `send_to_pa` (et réessayer tant qu'il renvoie 409).
4. Suivre `e_invoicing.status` par polling depuis le poste, ou par webhook via Supabase.
5. À l'encaissement du loyer, appeler `mark_as_paid` ou lettrer une transaction.
6. Vérifier ensuite que `e_invoicing.status` passe à `collected`.

**Rapport entre `mark_as_paid` et le statut « encaissée »**
- Que l'étape 5 déclenche le statut 212 vers la PA est **plausible**. Le statut existe dans l'enum, une source tierce décrit une mise à jour automatique au paiement, et la keynote 2025 parle de remontée du lettrage. **Mais ce n'est pas documenté.** À tester en sandbox, puis à faire confirmer par Pennylane.

**Correspondance avec la norme**
- Les 9 valeurs Pennylane sont des libellés propriétaires. Leur correspondance avec les codes AFNOR (200 Déposée, 210 Refusée, 212 Encaissée, 213 Rejetée…) n'est pas publiée. Hypothèse probable : `submitted` = déposée, `collected` = encaissée.

**Paiements partiels**
- `partially_collected` existe. Un loyer payé en plusieurs fois aurait donc une représentation. Le montant encaissé par taux de TVA, que demande le statut 212, n'a aucun champ visible.

### Gaps
- Déclencheur exact de `collected` et de `partially_collected` (`mark_as_paid` ? lettrage seulement ? Compte Pro seulement ?) et montant transmis : non documentés.
- E-reporting des paiements (pour les opérations hors e-facture) par l'API : aucun endpoint trouvé.
- Correspondance des statuts Pennylane avec les codes de cycle de vie AFNOR/DGFiP : non publiée.
- Date de disponibilité de `send_to_pa` en production : non datée dans le changelog. Les pages ne portent toutefois aucun libellé bêta, sauf `lifecycle_events`.

## 5. Offres Pennylane pour les éditeurs et les cabinets qui gèrent de nombreuses sociétés (cabinet, partenaire, marque blanche)

### Takeaway
Il n'existe **ni marque blanche, ni offre « éditeur multi-sociétés » à prix groupé**. Pennylane propose :
- pour les éditeurs, la **Solution Compatible PA** (marque grise, 0 € pour l'éditeur, API AFNOR) et le **Partenaire technologique** (OAuth et marketplace) ;
- pour les cabinets, la **Firm API**, la **Firm Group API** sur demande, le plan **Collaboratif payé par le cabinet**, l'invitation en masse des clients à la PA, et une PA gratuite pour les cabinets dont la comptabilité est sur Pennylane.

Les leviers multi-sociétés de Pennylane sont pensés pour les **cabinets comptables**, pas pour les éditeurs verticaux.

### Cited Findings
**Pour les éditeurs**
- Solution Compatible : marque grise uniquement, portail aux couleurs de l'éditeur, « 0€ », +120 éditeurs, clause de non-sollicitation. — [Pennylane, Solutions Compatibles PA](https://www.pennylane.com/fr/facture-electronique-scpa)
- Partenaire technologique : plus de 300 éditeurs, certification puis marketplace, co-marketing (« Collaborez avec nos équipes commerciales et soyez visibles sur nos réseaux »). — [Pennylane, Partenaire technologique](https://www.pennylane.com/fr/partenaires/partenaire-technologique)

**Pour les cabinets**
- PA gratuite pour les cabinets qui tiennent la comptabilité de leurs clients sur Pennylane, avec invitation des clients en masse ou à l'unité et suivi de la conformité. — [Pennylane, PA pour experts-comptables](https://www.pennylane.com/fr/expert-comptable/pdp)
- Firm API : « Default for firms », portefeuille de sociétés. Firm Group API : création de sociétés et d'utilisateurs, « On request ». — [Pennylane API, Choose the Right API](https://pennylane.readme.io/docs/what-apis-are-available)
- Collaboratif : réservé aux entreprises invitées par un cabinet, « généralement payé par le cabinet », sans API. — [Centre d'aide Pennylane, abonnements entreprises](https://help.pennylane.com/fr/articles/185068-les-abonnements-pennylane-pour-les-entreprises)
- Pennylane revendique 7 500 cabinets et entre 900 000 et 1 500 000 entreprises. Les chiffres varient d'une section à l'autre de la même page. — [Pennylane, Partenaires](https://www.pennylane.com/fr/partenaires) ; [Partenaire technologique](https://www.pennylane.com/fr/partenaires/partenaire-technologique)
- **Antérieur à 2026** (06/2025) : PA à 4 €/mois et par dossier pour les cabinets sous d'autres logiciels comptables, gratuite jusqu'au 01/09/2026, sans retour des statuts de paiement. Programme Compte Pro : 270 cabinets inscrits. — [Pennylane, keynote ComptaTech 2025](https://www.pennylane.com/fr/blog/contenu-expert-comptable/keynote-comptatech-2025)

### Inferences
- **CIPL n'est pas un cabinet comptable.** La Firm API ne s'applique que si les SCI sont tenues par un cabinet sur Pennylane, et elle passe par le token de ce cabinet.
- Pour CIPL, il reste deux options :
  1. **Solution Compatible**, la plus adaptée sur le papier : un éditeur qui fait émettre ses clients via la PA Pennylane, en marque grise, sans frais pour l'éditeur. Son modèle d'abonnement pour le client final est inconnu.
  2. **Partenaire technologique avec OAuth**, si CIPL reste sur l'API v2 actuelle. Il faut alors l'Essentiel par SCI.
- L'offre à 4 €/dossier de 2025 suggère que Pennylane sait vendre une « PA seule » par dossier. Si elle existe encore en 2026, c'est l'ordre de grandeur à demander pour un éditeur : 48 €/an par SCI, contre 288 € avec Essentiel. **Rien ne confirme qu'elle soit ouverte aux éditeurs.**

**Questions à poser à l'équipe Partenariats de Pennylane**
1. La Solution Compatible exige-t-elle un abonnement Pennylane par SCI cliente ? Lequel, et à quel prix ?
2. Comment se passe l'activation « depuis votre solution » : création du compte, signature du mandat, inscription à l'annuaire ?
3. Une app OAuth peut-elle accéder à une société en Starter ou en Basique ?
4. Délai et critères de validation d'une app OAuth ?
5. `mark_as_paid` ou un lettrage déclenche-t-il le statut 212 « Encaissée » vers la PA ? Avec quel montant ?
6. Quelle norme et quelle version suit l'« API REST AFNOR » (XP Z12-013) ?
7. Existe-t-il un tarif de volume ou une facturation consolidée pour un éditeur ?

### Gaps
- Aucune trace publique d'une offre de **marque blanche** : la page Solution Compatible l'exclut explicitement. Aucune trace non plus d'un tarif éditeur par volume.
- Le statut 2026 de l'offre PA à 4 €/dossier (annoncée le 26/06/2025) : non confirmé.
- Statut d'immatriculation sur la liste officielle impots.gouv.fr : non revérifié ici. Voir `pennylane_reference.md` Q1, avec des sources contradictoires sur « définitive » ou « sous réserve ». La page Solution Compatible affiche « décembre 2025 : immatriculation définitive ».
