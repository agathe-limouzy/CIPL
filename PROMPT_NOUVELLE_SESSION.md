Contexte : application CIPL (gestion locative), Unity 2023.2, C#, uGUI/TMP, branche Facturation. Réponds en français.

À lire d'abord :
- REVUE_CODE_CIPL.md : la passation en tête. Dernier état au 09/10 (section « Session du 06/10 » jusqu'à « Fin de session (09/10) ») : module Objectifs / « À traiter » refait et paiement partiel des factures, tout validé en Play, 406 tests EditMode verts ;
- ARCHITECTURE_CIPL.md ;
- FACTURATION_CIPL_PENNYLANE.md §16 (§16.2 pour le suivi des factures et le paiement partiel) ;
- PARCOURS_TEST_CIPL.md (sections Q à T pour le dernier travail) ;
- vérifie l'état git (le commit de la session précédente est peut-être fait, peut-être pas).
Le graphe du code est dans graphify-out/ (`python -m graphify affected "X"` pour trouver les appelants ; graphify n'est pas dans le PATH, passe par `python -m`).

Objectif de la session : [À COMPLÉTER]
Commence par me demander ce que j'entends par là et ma liste, avec AskUserQuestion. Ensuite, propose un ordre de priorité. Pour chaque sujet, mets-toi d'accord avec moi sur les décisions (AskUserQuestion, avec des exemples chiffrés ou concrets, et un aperçu quand c'est visuel) AVANT de coder. Pour tout parcours ou écran nouveau, montre-moi des maquettes d'abord.

Méthode (à respecter) :
- Règles métier dans des classes statiques testables, avec un test NUnit EditMode à chaque fois.
- Unity passe par le MCP ai-game-developer : assets-refresh, tests-run, script-execute, console-get-logs. Si le serveur ne répond pas (ECONNREFUSED, session expirée), dis-le-moi pour que je reconnecte le plugin.
- En Play, on ne peut ni compiler ni lancer les tests : vérifie EditorApplication.isPlaying et demande-moi d'arrêter le Play. Entrer en Play compile les .cs en attente : dès que j'arrête le Play, compile et teste AVANT que je le relance.
- Si tests-run reste bloqué, efface (hors Play) les clés SessionState MCP_PendingTestRun* et Unity_MCP_TestRunner_*.
- Captures d'écran en Play via ScreenCapture ; la vue Game est réduite (0,55x à 0,7x), mesure avant de corriger un « trop petit ». Ne mesure jamais un texte TMP sur un objet inactif (GetPreferredValues : police nulle, NullReferenceException).
- Le zoom des Réglages (jusqu'à 175 %) ne doit rien casser : vérifie les largeurs minimales et l'ouverture des menus à 175 %.
- Ne modifie jamais un fichier de données (batiment_*.json) pendant le Play : la fermeture du Play le réécrit. Pour vérifier en Play sans rien écrire, ne modifie pas non plus les données en mémoire (la fermeture les sauvegarde). Hors Play, fais une sauvegarde .bak avant toute écriture par script.
- Au lancement, une fenêtre « N factures sont à envoyer » peut s'ouvrir : ne clique jamais « Tout envoyer » (vrais e-mails).
- Le hook GateGuard exige des faits (appelants, API, données, ma consigne mot pour mot) avant la première modification de chaque fichier. Quand il bloque une modification dans un lot, vérifie lesquelles sont passées et refais seulement celle qui a été bloquée.
- Mets à jour la doc à chaque changement : FACTURATION §16 (si ça touche la facturation), la passation de la REVUE, le guide PARCOURS_TEST_CIPL.md (avec un cas de test pour moi).
- Je fais les commits moi-même.

Conventions d'interface déjà en place :
- Erreur, refus ou échec → ConfirmDialog.Erreur(message), au centre. Information ou changement de statut → UndoToast.Instance?.ShowInfo, en bas.
- La police TMP (LiberationSans) n'a ni ⚠ ✓ 🏢 👤 ▸ ▾ ▼ ► ⌄ (ils s'affichent en carré) : écris du texte à la place ; « › » existe.
- Champs date : DepartPanel.ChampDate (clone de fiche.dateFinBail, saisie JJ / MM / AAAA). Libellé + saisie : DepartPanel.Champ. Montants saisis : SaisieNumerique.TryParse (virgule ou point, espaces de milliers).
- Menus déroulants : UIDropdown.
- Modales construites en code : bandeau plein, sprite RoundedTop, via DepartPanel.Entete (exemples : DepartPanel, VersementsPanel, ObjectifPanel).
- Rendre cliquable un objet construit en code : ObjectifsTableau.Cliquable (sinon un Button ajouté sous un CanvasGroup non interactif garde une teinte grise). Pastille arrondie : ObjectifsTableau.Pastille (fond en enfant ignoreLayout : une Image 9-slice impose sinon une taille minimale).
- Données de test : DemoCIPL (4 bâtiments : rivoli, Parc Tertiaire Sud, Auterive, Le Hangar). Les bâtiments de test supprimés (« TEST Nouveautés », « TEST Objectifs ») sont dans CIPL_Saves/corbeille_batiments/. Si un test a besoin de données dédiées, crée un bâtiment par script hors Play, puis supprime-le de la même façon (dossier et JSON rangés dans la corbeille).
