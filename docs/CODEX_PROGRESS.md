# Suivi Codex — PinteMod Control Center

## 2026-08-02 — Audit initial et socle applicatif

### Objectif de la passe

Auditer les références disponibles et poser le socle sécurisé du prototype WPF v2.2, intégralement simulé et read-only.

### Réalisé

- Lecture du handoff, des instructions workspace et d'`AGENTS.md`.
- Audit en lecture seule de l'archive PinteMod disponible, des quatre contrats JSON et de la référence graphique.
- Vérification des 73 sommes internes de l'archive : aucune divergence et aucun fichier manquant.
- Création de la solution et des projets Core, Infrastructure et Tests sous `app/`.
- Création des modèles indépendants de WPF, interfaces de fournisseurs, validation XUID et actions de simulation à liste blanche.
- Création d'un fournisseur de données simulées réalistes et d'un service garantissant `CommandSent = false`.
- Création des documents de suivi sous `docs/`.

### Fichiers créés ou modifiés

- `app/Directory.Build.props`
- `app/PinteMod.ControlCenter.sln`
- `app/src/PinteMod.ControlCenter.Core/**`
- `app/src/PinteMod.ControlCenter.Infrastructure/**`
- `app/tests/PinteMod.ControlCenter.Tests/**`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`
- `docs/UI_FEEDBACK.md`

### Fonctionnalités disponibles

- Contrat de snapshot Dashboard.
- Modèles serveur, services, joueurs, événements et records.
- Jeu de données simulé avec ciblage par XUID.
- Simulation d'actions joueur et serveur sans transport ni commande réelle.
- Validation stricte d'un BOIII_XUID hexadécimal de 16 caractères.

### Compilations

- Debug : non exécutée à ce stade — aucun SDK .NET n'est installé ou détectable dans l'environnement.
- Release : non exécutée à ce stade — même blocage.

### Tests

- Tests MSTest écrits pour les XUID, la cohérence du snapshot et l'absence de commande réelle.
- Exécution en attente du SDK .NET 8.

### Problèmes rencontrés

- `reference/PinteMod_v2.1.1_FINAL.zip` est absent. L'unique archive disponible est `reference/PinteMod_v2.1.1.zip`, SHA-256 `CA6B8FAF5D6569454C2D8D753D4E35CF4B5EEF18F25F9BCB707E09C4E3EE517D`.
- Aucun dépôt `.git` n'est présent dans le workspace, même si `.gitignore` exclut bien `server-sandbox/`.
- Aucun SDK .NET ni MSBuild n'est détecté dans les emplacements Windows usuels.

### Validation humaine nécessaire

- Confirmer que l'archive sans suffixe `_FINAL` est bien l'artefact final attendu.
- Examiner le prototype graphique et renseigner uniquement `docs/UI_FEEDBACK.md` si des ajustements sont souhaités.

### Captures

- Aucune à ce stade ; le prototype WPF est encore en cours de construction.

## 2026-08-02 — Prototype WPF complet

### Objectif de la passe

Livrer l'interface simulée des six sections et rendre toutes les interactions de prototype navigables et sûres.

### Réalisé

- Shell WPF sombre avec rail de navigation Dashboard/Joueurs/Serveur/Records/Logs/Paramètres.
- Dashboard avec cinq KPI, état de cinq services, événements, liste des joueurs et sélection par objet portant le XUID.
- Fiche joueur réutilisable avec identité, rôle, langue, pays, vie, points, présence et toutes les actions demandées marquées `Simulation`.
- Panneaux Armes & Atouts, Modération & Identité et Assistance.
- Panneau Serveur avec carte, redémarrage, manche, courant, Pack-a-Punch, musique, événements, boss, power-ups et diagnostics simulés.
- Vues Records, Logs filtrables et Paramètres locaux.
- Thème WPF natif sombre/bleu, couleurs sémantiques et conteneurs scrollables pour les tailles réduites.
- Contrôle XML statique de 15 fichiers XAML/projet : 0 erreur XML.
- Scan statique des API réseau/processus/écriture dans les sources : aucune occurrence.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter/PinteMod.ControlCenter.csproj`
- `app/src/PinteMod.ControlCenter/App.xaml` et `App.xaml.cs`
- `app/src/PinteMod.ControlCenter/MainWindow.xaml` et `MainWindow.xaml.cs`
- `app/src/PinteMod.ControlCenter/Themes/PinteModTheme.xaml`
- `app/src/PinteMod.ControlCenter/Converters/UiConverters.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/**`
- `app/src/PinteMod.ControlCenter/Controls/PlayerDetailsControl.xaml` et `.xaml.cs`
- `app/src/PinteMod.ControlCenter/Views/**`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Fonctionnalités disponibles

- Navigation fonctionnelle entre les six sections.
- Sélection joueur et simulations d'actions typées.
- Filtres et recherche du flux de logs simulé.
- Choix serveur prédéfinis sans saisie de commande brute.
- Préférences de prototype uniquement en mémoire.

### Compilations et tests

- Debug : en attente du SDK .NET 8.
- Release : en attente du SDK .NET 8.
- Tests : écrits, exécution en attente du SDK.

### Validation humaine et captures

- Le rendu WPF doit être lancé puis comparé à la référence après compilation.
- Aucune capture produite à cette étape intermédiaire.

## 2026-08-02 — Première livraison complète

### Objectif de la passe

Finaliser, compiler, tester et vérifier visuellement le prototype WPF read-only simulé.

### Réalisé

- Correction du démarrage WPF et validation réelle de l'ouverture de l'application.
- Ajustement responsive pour une fenêtre de 1060×840 et conservation du fonctionnement à partir de 900×640.
- Barre de titre sombre, contrôles de fenêtre et listes déroulantes cohérentes avec le thème.
- Contrôle visuel des vues Dashboard, Joueurs et Serveur.
- Trois captures PNG finales enregistrées.
- README de compilation, test et lancement ajouté sous `app/`.
- Audit détaillé de l'archive conservé dans `docs/PINTEMOD_ARCHIVE_AUDIT.md`.
- Application refermée après contrôle ; aucun processus PinteMod Control Center n'est laissé actif.

### Fonctionnalités disponibles

- navigation Dashboard/Joueurs/Serveur/Records/Logs/Paramètres ;
- KPI session, services, événements et joueurs ;
- sélection joueur par modèle contenant un XUID validé ;
- fiche joueur et toutes les actions demandées en simulation ;
- panneaux serveur, armes et atouts simulés ;
- records et Easter Egg Records simulés ;
- filtres et recherche de logs simulés ;
- paramètres en mémoire, sans lecture de secret ;
- redimensionnement, défilement vertical, clavier et souris.

### Résultats de compilation

- SDK utilisé pour la validation : .NET SDK 8.0.423, runtime Windows Desktop 8.0.29, installé uniquement dans un dossier temporaire.
- Debug : **SUCCÈS — 0 warning, 0 erreur**.
- Release : **SUCCÈS — 0 warning, 0 erreur**.
- Exécutable Release : `app/src/PinteMod.ControlCenter/bin/Release/net8.0-windows/PinteMod.ControlCenter.exe`.

### Résultats des tests

- Debug : **11/11 réussis, 0 échec, 0 ignoré**.
- Release : **11/11 réussis, 0 échec, 0 ignoré**.
- Couverture fonctionnelle de base : validation XUID, cohérence du snapshot, rejet du pseudo comme cible, actions joueur/serveur simulées et garantie `CommandSent = false`.

### Problèmes rencontrés et résolus

- SDK .NET absent : SDK officiel installé temporairement pour la validation.
- Accès sandbox à NuGet/bin/obj : restaurations et builds autorisés explicitement, limités au projet et aux caches temporaires.
- Binding WPF `Run.Text` initialement bidirectionnel : forcé en `OneWay`.
- Fenêtre initiale trop large pour l'écran de capture : dimensions et mesure horizontale ajustées.
- Contrôles ComboBox natifs trop clairs : template sombre ajouté.

### Éléments nécessitant une validation humaine

- confirmer que `reference/PinteMod_v2.1.1.zip` est bien l'archive FINAL malgré son nom différent ;
- valider l'orientation graphique à partir des captures ;
- déposer les éventuels retours graphiques dans `docs/UI_FEEDBACK.md`, sans modifier les autres sources à la main.

### Captures produites

- `app/artifacts/screenshots/dashboard.png`
- `app/artifacts/screenshots/joueurs.png`
- `app/artifacts/screenshots/serveur.png`

### Inventaire précis des fichiers créés ou modifiés

Les dossiers `bin/` et `obj/` sont des sorties générées et ne figurent pas dans cet inventaire source.

```text
app/artifacts/screenshots/dashboard.png
app/artifacts/screenshots/joueurs.png
app/artifacts/screenshots/serveur.png
app/Directory.Build.props
app/NuGet.Config
app/PinteMod.ControlCenter.sln
app/README.md
app/src/PinteMod.ControlCenter.Core/Contracts/IControlCenterDataProvider.cs
app/src/PinteMod.ControlCenter.Core/Contracts/ISimulationActionService.cs
app/src/PinteMod.ControlCenter.Core/Models/ControlCenterModels.cs
app/src/PinteMod.ControlCenter.Core/PinteMod.ControlCenter.Core.csproj
app/src/PinteMod.ControlCenter.Core/Security/XuidValidator.cs
app/src/PinteMod.ControlCenter.Core/Simulation/SimulationModels.cs
app/src/PinteMod.ControlCenter.Infrastructure/PinteMod.ControlCenter.Infrastructure.csproj
app/src/PinteMod.ControlCenter.Infrastructure/Simulation/SimulatedControlCenterDataProvider.cs
app/src/PinteMod.ControlCenter.Infrastructure/Simulation/SimulationActionService.cs
app/src/PinteMod.ControlCenter/App.xaml
app/src/PinteMod.ControlCenter/App.xaml.cs
app/src/PinteMod.ControlCenter/Controls/PlayerDetailsControl.xaml
app/src/PinteMod.ControlCenter/Controls/PlayerDetailsControl.xaml.cs
app/src/PinteMod.ControlCenter/Converters/UiConverters.cs
app/src/PinteMod.ControlCenter/MainWindow.xaml
app/src/PinteMod.ControlCenter/MainWindow.xaml.cs
app/src/PinteMod.ControlCenter/PinteMod.ControlCenter.csproj
app/src/PinteMod.ControlCenter/Themes/PinteModTheme.xaml
app/src/PinteMod.ControlCenter/ViewModels/Commands.cs
app/src/PinteMod.ControlCenter/ViewModels/DashboardViewModel.cs
app/src/PinteMod.ControlCenter/ViewModels/DisplayItemViewModels.cs
app/src/PinteMod.ControlCenter/ViewModels/LogsViewModel.cs
app/src/PinteMod.ControlCenter/ViewModels/ObservableObject.cs
app/src/PinteMod.ControlCenter/ViewModels/PageViewModel.cs
app/src/PinteMod.ControlCenter/ViewModels/PlayerActionsViewModelBase.cs
app/src/PinteMod.ControlCenter/ViewModels/PlayersViewModel.cs
app/src/PinteMod.ControlCenter/ViewModels/RecordsViewModel.cs
app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs
app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs
app/src/PinteMod.ControlCenter/ViewModels/ShellViewModel.cs
app/src/PinteMod.ControlCenter/Views/DashboardView.xaml
app/src/PinteMod.ControlCenter/Views/DashboardView.xaml.cs
app/src/PinteMod.ControlCenter/Views/LogsView.xaml
app/src/PinteMod.ControlCenter/Views/LogsView.xaml.cs
app/src/PinteMod.ControlCenter/Views/PlayersView.xaml
app/src/PinteMod.ControlCenter/Views/PlayersView.xaml.cs
app/src/PinteMod.ControlCenter/Views/RecordsView.xaml
app/src/PinteMod.ControlCenter/Views/RecordsView.xaml.cs
app/src/PinteMod.ControlCenter/Views/ServerView.xaml
app/src/PinteMod.ControlCenter/Views/ServerView.xaml.cs
app/src/PinteMod.ControlCenter/Views/SettingsView.xaml
app/src/PinteMod.ControlCenter/Views/SettingsView.xaml.cs
app/tests/PinteMod.ControlCenter.Tests/PinteMod.ControlCenter.Tests.csproj
app/tests/PinteMod.ControlCenter.Tests/SimulatedProviderTests.cs
app/tests/PinteMod.ControlCenter.Tests/SimulationActionServiceTests.cs
app/tests/PinteMod.ControlCenter.Tests/XuidValidatorTests.cs
docs/CODEX_PROGRESS.md
docs/DECISIONS.md
docs/PINTEMOD_ARCHIVE_AUDIT.md
docs/TODO.md
docs/UI_FEEDBACK.md
```

### Relais ChatGPT / Codex

- Faire appel à **ChatGPT** pour arbitrer la direction produit, commenter les captures et rédiger de nouveaux retours dans `UI_FEEDBACK.md`.
- Faire appel à **Codex** pour lire ces retours, proposer le plan ciblé, modifier les sources, compiler, tester et produire la livraison suivante.

## Passe ciblée de revue humaine — jalon d’implémentation

### Date

2026-08-02

### Objectif de la passe

Appliquer les corrections critiques et hautes de la revue ChatGPT sans changer l’architecture globale ni quitter le mode entièrement simulé.

### Réalisé à ce jalon

- Revue conservée fidèlement dans `docs/UI_FEEDBACK.md`.
- Snapshot partagé introduit par `IControlCenterSnapshotStore` et `CachedControlCenterSnapshotStore` : les six pages ne déclenchent plus six lectures du fournisseur.
- Sélection Dashboard/Joueurs partagée par BOIII_XUID via `PlayerSelectionState`, avec remplacement explicite si le joueur disparaît.
- Résultats d’actions structurés : action, cible d’affichage, XUID abrégé avec info-bulle complète, option, heure, statut et `CommandSent = false`.
- États `Warning`, `Offline`, `Error` et `Unknown` ajoutés, avec scénarios simulés healthy/warning/offline/server-stopped/empty.
- États Serveur et Records reliés au snapshot (`ServerRunning`, `RankedStatus`, carte).
- Paramètres non implémentés désactivés et marqués « À venir ».
- Commandes asynchrones protégées contre les exceptions ; messages affichables et réactivation garantie.
- Mise en page adaptative : sidebar compacte, grilles KPI responsives, empilement des panneaux, aucun `MinWidth=740` dans les pages.
- Thème sombre étendu à `WindowChrome`, ScrollBar, CheckBox, filtres Logs et focus clavier.

### Validation provisoire

- Compilation Debug : en attente après ce jalon.
- Compilation Release : en attente après ce jalon.
- Tests : extension en cours.
- Captures : en attente de la validation compilée aux trois tailles.

### Contraintes respectées

Aucun accès réseau, RCON, secret, processus serveur, lecteur local, écriture PinteMod, modification GSC, `reference/` ou `server-sandbox/`.

## Livraison finale de la passe ciblée

### Date

2026-08-02

### Objectif

Finaliser la seconde passe issue de la revue humaine, valider l’exécutable WPF aux trois tailles imposées et produire un lot de captures prêt pour la prochaine revue ChatGPT.

### Fonctionnalités disponibles

- Navigation fonctionnelle sur Dashboard, Joueurs, Serveur, Records, Logs et Paramètres.
- Layout réellement adaptatif à 1920×1080, 1060×840 et 900×640, sans défilement horizontal global.
- Sélection joueur partagée entre Dashboard et Joueurs par XUID ; Mason reste sélectionné lors du changement de page.
- Résultat structuré des actions Joueur et Serveur avec cible d’affichage, XUID, option, heure, statut et `CommandSent = false`.
- État serveur issu de `ServerRunning` et état Ranked/Unranked issu du snapshot.
- Filtre Logs sélectionné visuellement, recherche combinée et état vide.
- Paramètres non implémentés désactivés et marqués « À venir ».
- Scénarios déterministes Healthy, Warning, Offline/Error, ServerStopped et Empty.
- Snapshot partagé entre les six ViewModels et commandes asynchrones protégées.

### Compilations et tests finaux

- Debug : réussite, 0 warning, 0 erreur.
- Release : réussite, 0 warning, 0 erreur.
- Tests Debug : 26/26 réussis, 0 échec, 0 ignoré.
- Tests Release : 26/26 réussis, 0 échec, 0 ignoré.
- Rescan final : aucune API réseau/socket/processus/écriture/DPAPI/registre ; aucun ancien `MinWidth=740` ni état `EN LIGNE`/`RANKED` figé dans le XAML.
- `.gitignore` contient toujours `server-sandbox/`.

### Validation visuelle effectuée

Les six pages ont été ouvertes et inspectées à chacune des tailles suivantes :

- 1920×1080 ;
- 1060×840 ;
- 900×640.

À 900×640, la sidebar devient compacte, les KPI se répartissent sur plusieurs lignes et les panneaux à deux colonnes s’empilent. Le scrollbar vertical est sombre et aucun contenu n’impose de défilement horizontal global.

### Problèmes rencontrés et corrigés

- La première action simulée a révélé des bindings `Run.Text` implicitement incompatibles avec des propriétés en lecture seule. Les bindings de `Time`, `Status`, `StatusValue` et `CommandSent` sont maintenant explicitement `OneWay`.
- L’ouverture de Paramètres a révélé le même risque sur `CheckBox.IsChecked`. Les trois options en lecture seule utilisent maintenant explicitement `Mode=OneWay`.
- La gestion d’erreur a empêché toute fermeture brutale lors du premier défaut et a présenté un message visible.

### Captures produites

Toutes les captures sont sous `app/artifacts/screenshots/review-2/` :

- `dashboard-1060x840.png`
- `joueurs-1060x840.png`
- `serveur-1060x840.png`
- `records-1060x840.png`
- `logs-1060x840.png`
- `parametres-1060x840.png`
- `dashboard-1920x1080.png`
- `dashboard-900x640.png`
- `logs-empty-1060x840.png`
- `dashboard-warning-1060x840.png`
- `dashboard-offline-error-1060x840.png`

### Fichiers créés pendant cette passe

- `app/src/PinteMod.ControlCenter.Core/Contracts/IControlCenterSnapshotStore.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Simulation/CachedControlCenterSnapshotStore.cs`
- `app/src/PinteMod.ControlCenter/Controls/ResponsiveUniformGrid.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/PlayerSelectionState.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`
- les onze captures listées ci-dessus.

### Fichiers modifiés pendant cette passe

- `app/src/PinteMod.ControlCenter.Core/Models/ControlCenterModels.cs`
- `app/src/PinteMod.ControlCenter.Core/Simulation/SimulationModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Simulation/SimulatedControlCenterDataProvider.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Simulation/SimulationActionService.cs`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/Controls/PlayerDetailsControl.xaml`
- `app/src/PinteMod.ControlCenter/Converters/UiConverters.cs`
- `app/src/PinteMod.ControlCenter/MainWindow.xaml`
- `app/src/PinteMod.ControlCenter/Themes/PinteModTheme.xaml`
- `app/src/PinteMod.ControlCenter/ViewModels/Commands.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/DashboardViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/DisplayItemViewModels.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/LogsViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/PageViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/PlayerActionsViewModelBase.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/PlayersViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/RecordsViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ShellViewModel.cs`
- les six XAML sous `app/src/PinteMod.ControlCenter/Views/`.
- `app/tests/PinteMod.ControlCenter.Tests/PinteMod.ControlCenter.Tests.csproj`
- `app/tests/PinteMod.ControlCenter.Tests/SimulatedProviderTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/SimulationActionServiceTests.cs`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`
- `docs/UI_FEEDBACK.md`

### Validation humaine restante

Soumettre maintenant les captures `review-2` à ChatGPT pour arbitrer uniquement d’éventuels ajustements visuels. Revenir ensuite vers Codex avec les retours ajoutés à `docs/UI_FEEDBACK.md`.

## 2026-08-02 — Paquet autonome pour la revue ChatGPT

### Objectif de la passe

Préparer un livrable unique, téléchargeable et directement exploitable par ChatGPT pour la validation humaine de la seconde passe visuelle.

### Réalisé

- Création de `app/artifacts/chatgpt-review-2/` avec un guide de lecture et un prompt prêt à copier.
- Regroupement des onze captures `review-2`, dont les formats responsive et les scénarios Warning, Offline/Error et Logs vide.
- Ajout d'une copie de la référence graphique, des documents de contexte, des contrats et de toutes les sources utiles.
- Exclusion des dossiers `bin/`, `obj/`, des secrets, des données runtime, de `server-sandbox/` et de l'archive serveur.
- Création d'un manifeste SHA-256 et d'une archive ZIP autonome.
- Vérification que `docs/UI_FEEDBACK.md` est resté inchangé.
- Contrôle du ZIP : 83 fichiers, 11 captures, 59 fichiers de sources, 0 entrée interdite, 0 fichier requis manquant et 0 erreur de manifeste.

### Fichiers créés ou modifiés

- `app/artifacts/chatgpt-review-2/**`
- `app/artifacts/PinteMod-ControlCenter-ChatGPT-Review-2.zip`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Compilations et tests

- Aucun source applicatif n'a été modifié pendant cette passe documentaire.
- Les derniers résultats restent valides : Debug et Release avec 0 avertissement et 0 erreur ; 26/26 tests réussis dans les deux configurations.

### Validation humaine nécessaire

- Joindre `app/artifacts/PinteMod-ControlCenter-ChatGPT-Review-2.zip` à ChatGPT.
- Copier le contenu de `PROMPT_CHATGPT.md` dans la conversation de revue.
- Transmettre ensuite à Codex uniquement les retours validés, via `docs/UI_FEEDBACK.md`.

### Captures incluses

- Les onze fichiers du dossier `app/artifacts/screenshots/review-2/`, reproduits sans modification dans le paquet.

## 2026-08-02 — Clôture de la seconde passe visuelle

### Objectif de la passe

Appliquer l'unique correction prioritaire issue de la revue humaine finale : afficher `UNRANKED` en orange partout où le `RankedStatus` du snapshot est présenté.

### Réalisé

- Centralisation explicite de la clé de couleur dans `StatusBrushConverter.GetResourceKey`.
- Conservation de `RANKED` sur `SuccessBrush` vert.
- Passage de `UNRANKED` sur `WarningBrush` orange.
- Conservation de `DangerBrush` rouge pour les erreurs, états hors ligne, événements dangereux et simulations rejetées.
- Dashboard et Records continuent d'utiliser la valeur `RankedStatus` provenant du snapshot/ViewModel ; aucun état n'a été codé en dur dans les vues.
- Extension du test existant des records pour vérifier les clés de couleur des statuts Ranked et Unranked, sans changer le total de 26 tests.
- Nouvelle validation visuelle réelle à 1060×840 avec le scénario simulé Warning/Unranked.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter/Converters/UiConverters.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`
- `app/artifacts/screenshots/review-3/dashboard-unranked-orange-1060x840.jpg`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Fonctionnalités disponibles

- Sémantique visuelle cohérente : Ranked vert, Unranked orange, erreurs réelles rouges.
- Toutes les garanties de simulation restent inchangées, notamment `ISimulationActionService`, le ciblage par XUID et `CommandSent = false`.

### Résultats des compilations

- Debug : **SUCCÈS — 0 avertissement, 0 erreur**.
- Release : **SUCCÈS — 0 avertissement, 0 erreur**.

### Résultats des tests

- Debug : **26/26 réussis, 0 échec, 0 ignoré**.
- Release : **26/26 réussis, 0 échec, 0 ignoré**.
- Les tests couvrent désormais explicitement `RankedStatus.Ranked → SuccessBrush` et `RankedStatus.Unranked → WarningBrush`.

### Contrôles de sécurité

- Scan statique : 0 occurrence d'API réseau, socket, client HTTP, lancement de processus serveur, écriture de fichier applicative, DPAPI, pipe nommé ou registre.
- Les tests de la liste blanche confirment que `CommandSent` reste toujours faux.
- Aucun serveur BOIII, BAT, RCON, fichier PinteMod ou secret n'a été lancé, lu ou modifié.

### Problèmes rencontrés

- Le contrôle Windows a détecté une intervention utilisateur au moment de fermer la fenêtre et a cessé toute entrée automatique conformément à sa règle de sécurité.
- La fenêtre était déjà fermée mais le processus du prototype restait sans fenêtre ; son PID et son chemin exacts ont été vérifiés avant de l'arrêter. Aucun processus Control Center n'est laissé actif.

### Validation humaine nécessaire

- Aucune nouvelle passe graphique importante n'est demandée. La phase visuelle est validée, sauf régression ultérieure constatée.
- Les trois ajustements facultatifs de la revue finale n'ont pas été appliqués afin de conserver une correction strictement ciblée.

### Capture produite

- `app/artifacts/screenshots/review-3/dashboard-unranked-orange-1060x840.jpg`

## 2026-08-02 — Correction du déplacement de la fenêtre

### Objectif de la passe

Rendre la fenêtre principale réellement déplaçable par glisser-déposer depuis son bandeau supérieur personnalisé, sans modifier l'architecture ni les fonctions métier.

### Réalisé

- Remplacement de la zone de titre native inexistante (`CaptionHeight=0`) par une zone `WindowChrome` de 70 px sur toute la largeur.
- Suppression du gestionnaire manuel `DragMove`, qui ne couvrait qu'une partie du bandeau.
- Conservation des boutons Actualiser, Réduire, Agrandir et Fermer comme contrôles interactifs grâce à `IsHitTestVisibleInChrome=True`.
- Validation réelle du déplacement : origine de la fenêtre passée de `(430,96)` à `(560,186)` après glissement, soit `+130 px / +90 px`.
- Validation du bouton Fermer après déplacement ; aucun processus Control Center n'est resté actif.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter/MainWindow.xaml`
- `app/src/PinteMod.ControlCenter/MainWindow.xaml.cs`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Résultats des compilations

- Debug : **SUCCÈS — 0 avertissement, 0 erreur**.
- Release : **SUCCÈS — 0 avertissement, 0 erreur**.

### Résultats des tests

- Debug : **26/26 réussis, 0 échec, 0 ignoré**.
- Release : **26/26 réussis, 0 échec, 0 ignoré**.

### Contrôles complémentaires

- `CaptionHeight=70` présent et quatre boutons explicitement interactifs dans le chrome.
- Ancien gestionnaire `TitleBar_MouseLeftButtonDown` et appel `DragMove` supprimés.
- Scan statique : 0 occurrence d'API réseau, RCON, processus serveur ou écriture applicative interdite.

### Problèmes rencontrés

- Aucun problème restant. Le défaut de déplacement est reproduit, corrigé et validé sur l'exécutable Release.

### Validation humaine nécessaire

- Un essai manuel par l'utilisateur reste conseillé avant l'envoi final à ChatGPT, mais le déplacement a été confirmé automatiquement sur la fenêtre réelle.

### Captures

- Aucune nouvelle capture : la validation porte sur un mouvement de fenêtre et a été mesurée par ses coordonnées avant/après.

## 2026-08-02 — Paquet final pour ChatGPT

### Objectif de la passe

Fournir à l'utilisateur un unique ZIP à transmettre à ChatGPT après la correction orange de `UNRANKED` et la validation du déplacement de fenêtre.

### Réalisé

- Création de `app/artifacts/chatgpt-final-review/` avec un guide autonome et un prompt final prêt à copier.
- Regroupement des douze captures : onze captures `review-2` et la preuve finale de `UNRANKED` orange.
- Ajout de la référence graphique, du retour humain final, des documents de suivi, des contrats et des sources actuelles.
- Intégration des corrections `StatusBrushConverter` et `WindowChrome` dans la copie de sources destinée à la revue.
- Exclusion de `bin/`, `obj/`, `server-sandbox/`, des secrets, des données runtime et de l'archive serveur.
- Génération d'un manifeste SHA-256 et d'une archive finale vérifiée.

### Fichiers créés ou modifiés

- `app/artifacts/chatgpt-final-review/**`
- `app/artifacts/PinteMod-ControlCenter-ChatGPT-Final-Review.zip`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Compilations et tests

- Aucun source applicatif n'a été modifié pendant cette passe de conditionnement.
- Derniers résultats conservés : Debug et Release avec 0 avertissement et 0 erreur ; 26/26 tests réussis dans les deux configurations.

### Validation humaine nécessaire

- Transmettre le ZIP final à ChatGPT avec le contenu de `PROMPT_CHATGPT_FINAL.md`.
- Revenir vers Codex uniquement si ChatGPT identifie une régression bloquante concrète.

### Captures incluses

- Les douze captures des dossiers `review-2` et `review-3`, copiées sans modification.
## 2026-08-02 — Phase 2, sous-phase 1 : démarrage de l’implémentation

### Objectif de la passe

Introduire la lecture locale réelle, tolérante et strictement read-only de `current_session.json` et des quatre heartbeats autorisés, tout en conservant la simulation comme mode par défaut.

### État au démarrage

- Plan de conception validé par la revue humaine.
- Périmètre confirmé : manifeste de session, Supervisor, Ban Service, GeoIP Bridge et Live Console uniquement.
- Les états déclarés, états de lecture, fraîcheur, âge et provenance seront représentés séparément.
- Le mode hybride exigera explicitement `--data-mode=hybrid-local --server-root=<chemin absolu>` ; aucune installation ne sera recherchée automatiquement.
- `server-sandbox/` a uniquement été consulté en lecture pour confirmer le vocabulaire des champs `state`. Aucun fichier n’y a été exécuté, modifié ou sélectionné comme racine.

### Réalisé à ce stade

- Inspection du composition root, du snapshot partagé, des ViewModels, des vues et de la suite de tests existante.
- Confirmation qu’un fournisseur hybride peut enrichir le snapshot simulé sans modifier l’architecture en quatre projets.
- Définition des règles de synthèse : une donnée expirée devient inconnue, `Hors ligne` exige `stopped`, et `Erreur` exige un état explicite ou une erreur durable de lecture.

### Compilations et tests

- En attente de l’implémentation.

### Validation humaine

- Aucune validation supplémentaire requise avant le développement : l’autorisation explicite a été reçue.

## 2026-08-02 — Phase 2.1 : lecteurs locaux et snapshot hybride implémentés

### Objectif de la passe

Livrer l’intégration locale minimale validée sans étendre le périmètre au-delà du manifeste de session et des quatre heartbeats.

### Réalisé

- Ajout des modèles Core séparant état déclaré, lecture, fraîcheur, âge et provenance.
- Ajout de lecteurs JSON asynchrones et tolérants, exclusivement `FileAccess.Read`.
- Validation stricte et normalisation de `ServerRoot`, liste fermée de cinq chemins, rejet des racines de volume et des liens/jonctions.
- Ignorance systématique des `.tmp` et `.bak` comme sources actives.
- Cache mémoire de dernière valeur valide avec perte du vert dès que la lecture ou la fraîcheur n’est plus saine.
- Politique heartbeat : fraîche jusqu’à 15 s, retardée jusqu’à 45 s, expirée/inconnue au-delà ; `Hors ligne` exige `stopped`.
- Erreur durable de lecture uniquement après trois actualisations consécutives invalides.
- Fournisseur hybride superposé au snapshot simulé partagé ; seules carte/session/version déclarée et quatre cartes service sont remplacées.
- État global PinteMod neutre : « État inconnu — aucun heartbeat dédié ».
- Mode simulation par défaut et activation hybride testable par paire d’arguments obligatoire.
- Interface mise à jour pour afficher clairement le mode, les sources locales et les champs encore simulés.
- Documentation technique ajoutée dans `docs/PHASE2_LOCAL_READ_DESIGN.md` et instructions de lancement ajoutées au README.

### Fonctionnalités disponibles

- Lecture manuelle réelle de `current_session.json`.
- Lecture manuelle réelle des heartbeats Supervisor, Ban Service, GeoIP Bridge et Live Console.
- Affichage détaillé de la provenance et de la fraîcheur.
- Tolérance aux fichiers absents, partiels, vides, verrouillés ou périmés.
- Conservation read-only de la dernière donnée valide en mémoire.

### Compilations et tests intermédiaires

- Debug : **SUCCÈS — 0 avertissement, 0 erreur**.
- Tests Debug : **61/61 réussis, 0 échec, 0 ignoré**.
- Release : **SUCCÈS — 0 avertissement, 0 erreur**.
- Tests Release : **61/61 réussis, 0 échec, 0 ignoré**.

### Fichiers créés

- `app/src/PinteMod.ControlCenter.Core/Contracts/IClock.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/ISessionManifestReader.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/IServiceHeartbeatReader.cs`
- `app/src/PinteMod.ControlCenter.Core/Models/LocalReadModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/LocalPinteModOptions.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/ReadOnlyJsonFileReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/SystemClock.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/SessionManifestReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/HeartbeatFreshnessPolicy.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/ServiceHeartbeatReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/OfficialMapNameResolver.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/HybridControlCenterDataProvider.cs`
- `app/src/PinteMod.ControlCenter/Configuration/ApplicationStartupOptions.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ApplicationStartupOptionsTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/LocalReaderTestSupport.cs`
- `app/tests/PinteMod.ControlCenter.Tests/LocalSessionManifestReaderTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/LocalServiceHeartbeatReaderTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/HybridControlCenterDataProviderTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ReadOnlyGuaranteeTests.cs`
- `docs/PHASE2_LOCAL_READ_DESIGN.md`

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter.Core/Models/ControlCenterModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Simulation/SimulatedControlCenterDataProvider.cs`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/Converters/UiConverters.cs`
- `app/src/PinteMod.ControlCenter/MainWindow.xaml`
- `app/src/PinteMod.ControlCenter/ViewModels/DashboardViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/DisplayItemViewModels.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/RecordsViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ShellViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/DashboardView.xaml`
- `app/src/PinteMod.ControlCenter/Views/RecordsView.xaml`
- `app/src/PinteMod.ControlCenter/Views/SettingsView.xaml`
- `app/README.md`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Contrôles de sécurité finaux

- Test d’intégrité taille/date/SHA-256 avant et après lecture des cinq fichiers : réussi.
- Scan des sources de production : aucune API réseau, aucun client RCON, aucun lancement de processus et aucune API d’écriture de fichiers.
- `CommandSent` reste toujours `false` dans le service de simulation ; les tests historiques restent réussis.
- Aucun fichier `reference/`, `contracts/`, `server-sandbox/`, GSC ou `UI_FEEDBACK.md` n’a été modifié.
- Aucun serveur, BAT, outil PinteMod ou application WPF n’a été lancé pendant cette passe.

### Captures

- Aucune capture produite : l’application WPF n’a pas été lancée pendant cette sous-phase.

### Problèmes rencontrés

- Le SDK .NET 8 n’est pas exposé globalement ; réutilisation du SDK local 8.0.423 déjà présent dans le dossier temporaire de la phase 1.
- Les premières compilations sandboxées ne pouvaient pas remplacer les anciens fichiers générés. La compilation a été relancée avec l’autorisation limitée aux dossiers `app/bin` et `app/obj`.

### Validation humaine

- Le chemin réel `ServerRoot` n’a pas été utilisé pendant cette passe. Une validation sur une installation locale explicitement désignée reste à effectuer par l’utilisateur.
- Aucune capture n’a été produite à ce stade ; aucun exécutable WPF ou processus serveur n’a été lancé.

## 2026-08-02 — Validation humaine du Dashboard hybride local

### Objectif de la passe

Valider le comportement réel du Dashboard sur une copie de test explicitement désignée, sans toucher au serveur fonctionnel.

### Environnement validé

- `ServerRoot` de test : `<COPIE_DE_TEST>\UnrankedServer`.
- Le dossier contient `boiii` et les cinq JSON autorisés.
- Le serveur fonctionnel et `server-sandbox/` sont restés hors périmètre.

### Résultat visuel

- `MODE HYBRIDE LOCAL` affiché dans la sidebar et le bandeau Dashboard.
- Carte locale correctement lue : `Origins` / `zm_tomb`.
- Session locale correctement identifiée : `zm_tomb_s40_0`.
- Version déclarée `2.0.0` affichée sans être présentée comme une preuve de santé.
- PinteMod neutre et inconnu, faute de heartbeat dédié.
- Supervisor affiché `HORS LIGNE` uniquement parce que son état déclaré est explicitement `stopped`.
- Ban Service, GeoIP Bridge et Live Console déclarent `running`, mais restent `INCONNU` avec une fraîcheur `Expirée` car leurs fichiers ont environ 4 h 35 min.
- Lecture, fraîcheur, âge et provenance sont visibles séparément pour chaque service.
- Manche, durée, joueurs, Ranked, événements et records restent explicitement simulés.
- Le bouton Actualiser ne provoque aucune erreur ; l’absence de changement métier est normale sur une copie statique.

### Capture conservée

- `app/artifacts/screenshots/phase2/hybrid-local-validation-1060x840.png`
- SHA-256 : `F3C9712C95E0401A0787E619704237A8FC0204638D68B361545D34C0C4C36A9A`.

### Fichiers créés ou modifiés

- `app/artifacts/screenshots/phase2/hybrid-local-validation-1060x840.png`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Compilations et tests

- Aucun code applicatif modifié pendant cette validation.
- Derniers résultats conservés : Debug et Release avec 0 avertissement, 0 erreur et 61/61 tests réussis dans chaque configuration.

### Validation humaine restante

- La lecture hybride minimale est validée sur la copie de test.
- Une revue ChatGPT peut désormais vérifier la capture et clôturer la sous-phase 2.1.

## 2026-08-02 — Paquet ChatGPT de revue Phase 2.1

### Objectif de la passe

Fournir un ZIP autonome permettant à ChatGPT de vérifier la sécurité read-only, la sémantique des états, le snapshot hybride et la capture de validation réelle.

### Contenu préparé

- Guide `00_LIRE_EN_PREMIER.md`.
- Prompt final `PROMPT_CHATGPT_PHASE2_1.md` prêt à copier.
- Capture hybride locale validée.
- Preuves synthétiques des builds, 61 tests et contrôles de sécurité.
- Architecture Phase 2.1, audit, suivi et décisions.
- Contrats JSON publics du workspace.
- Sources actuelles Core, Infrastructure, WPF et Tests.
- Liste des fichiers et manifeste SHA-256 interne.

### Exclusions vérifiées

- Aucun `server-sandbox/`, `servtest`, runtime PinteMod ou donnée joueur.
- Aucun `reference/`, archive serveur, secret, BAT, EXE, DLL ou PDB.
- Aucun `bin/`, `obj/` ou ancien paquet de revue.

### Artefact produit

- `app/artifacts/PinteMod-ControlCenter-ChatGPT-Phase2.1-Review.zip`
- Taille finale : 2 047 765 octets.
- SHA-256 externe : `CEFB0780F56ECCF75328219F75077EB0B1BF4B5A13531B6395FF8A401C32CB44`.
- Contrôle ZIP : 95 entrées, 0 doublon, 0 entrée interdite.
- Manifeste interne : 92 fichiers vérifiés, 0 erreur de hash ; prompt et capture présents.

### Fichiers créés ou modifiés

- `app/artifacts/chatgpt-phase2.1-review/**`
- `app/artifacts/PinteMod-ControlCenter-ChatGPT-Phase2.1-Review.zip`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Compilations et tests

- Aucun code applicatif modifié pendant le conditionnement.
- Derniers résultats conservés : Debug et Release, 0 avertissement, 0 erreur, 61/61 tests réussis dans chaque configuration.

### Prochaine validation humaine

- Transmettre le ZIP à ChatGPT et copier le contenu de `PROMPT_CHATGPT_PHASE2_1.md`.
- Revenir vers Codex uniquement avec le verdict complet ou une correction bloquante précise.

## 2026-08-02 — Phase 2.1 validée et clôturée

### Verdict externe

- ChatGPT a validé la Phase 2.1 sans correction bloquante.
- La lecture locale hybride read-only est officiellement clôturée.

### Baseline figée

- Le code Phase 2.1 ne doit plus être modifié hors correction documentaire nécessaire ou régression bloquante démontrée.
- Simulation par défaut, activation hybride explicite, confinement de `ServerRoot`, cinq sources autorisées et sémantique des heartbeats deviennent la baseline de la suite.
- Les garanties sans RCON, réseau, secret, port, processus, écriture PinteMod ou modification GSC restent absolues.

### État de validation conservé

- Debug : 0 avertissement, 0 erreur, 61/61 tests réussis.
- Release : 0 avertissement, 0 erreur, 61/61 tests réussis.
- Validation humaine hybride sur copie de test : réussie.
- Capture et paquet ChatGPT Phase 2.1 : archivés sous `app/artifacts/`.

### Modifications de cette passe

- Documentation de suivi uniquement : `CODEX_PROGRESS.md`, `TODO.md` et `DECISIONS.md`.
- Aucun code, contrat, test, capture ou source PinteMod modifié.

### Suite

- Prochain plan recommandé à valider : intégration read-only structurée des Ranks et records de manches.

## 2026-08-02 — Démarrage Phase 2.2 et inventaire des schémas Ranks

### Objectif de la passe

Figer le périmètre et les champs autorisés avant d’implémenter la lecture locale read-only des profils Ranks v2 et des records de manches v4.

### Inventaire read-only réalisé

- Copie de test explicitement désignée consultée : `<COPIE_DE_TEST>\UnrankedServer`.
- Source profils active : `boiii/scriptdata/pintemod/ranks_v2/players/*.json`.
- Source records active : `boiii/scriptdata/pintemod/ranks_v2/maps/*.json`.
- 37 profils actifs et 1 fichier carte actif observés ; 36 sauvegardes et aucun temporaire observés, sans publier de valeur personnelle.
- Schéma profils confirmé en version 2 ; schéma records de carte confirmé en version 4.
- Convention confirmée dans la source GSC de référence : pseudos séparés par ` + ` et XUID séparés par `+`.

### Liste blanche retenue

- Profils : XUID validé, dernier pseudo d’affichage, nombre de sessions, temps total et meilleure manche.
- Records : carte, nom d’affichage, catégorie 1–4 joueurs, position 1–5, manche, durée, détenteurs, XUID validés et identifiant de match interne.
- Les champs techniques `key` et `identity_kind` ne seront ni exposés dans l’interface ni utilisés comme identifiants joueur.

### Garanties maintenues

- Aucune modification de la baseline Phase 2.1 pendant l’inventaire.
- Aucun fichier de la copie de test, de `server-sandbox/`, de `reference/`, de PinteMod ou GSC modifié.
- Aucun serveur, BAT, EXE, processus, RCON, réseau ou secret utilisé.
- Les fichiers `.tmp`, `.bak` et l’ancien dossier `ranks/` restent exclus des sources actives.

### Compilations et tests

- Aucun code modifié à ce jalon ; les résultats Phase 2.1 restent la baseline : 0 avertissement, 0 erreur et 61/61 tests en Debug et Release.

### Suite immédiate

- Implémenter les nouveaux contrats, lecteurs et fournisseur superposé sans réécrire les lecteurs Phase 2.1.
- Adapter uniquement la page Records et les informations de périmètre du mode hybride.

## 2026-08-02 — Livraison technique Phase 2.2

### Objectif de la passe

Livrer la lecture locale réelle, tolérante et strictement read-only des profils Ranks v2 et des records de manches v4, en conservant la Phase 2.1 comme baseline figée.

### Fonctionnalités disponibles

- Le mode simulation reste le mode par défaut et conserve ses profils/records déterministes.
- Le mode hybride explicite lit désormais les JSON actifs directement présents dans `ranks_v2/players` et `ranks_v2/maps`.
- La page Records affiche les profils locaux : pseudo, XUID abrégé, sessions, temps total et meilleure manche.
- Les top 5 de manches sont affichés par carte et catégorie 1–4 joueurs avec durée, détenteurs, XUID abrégés et provenance.
- État de lecture, fraîcheur, âge, provenance, fichiers/entrées ignorés et chemin logique sont visibles séparément.
- Les Easter Egg Records et le statut Ranked restent explicitement simulés.
- L’actualisation reste manuelle ; tous les réglages non implémentés restent désactivés et marqués « À venir ».
- Une source locale absente ne fait pas passer un record simulé pour une donnée locale ; seule la partie Easter Egg simulée est conservée.

### Architecture livrée

- Nouveaux contrats Core `IRankProfileReader` et `IRoundRecordReader`.
- Modèles WPF-indépendants `RankProfile`, `RoundRecord`, catalogues et `RankRecordsSnapshot`.
- Politique de chemins dédiée avec deux dossiers fixes, fichiers directs seulement et rejet des liens/jonctions existants.
- Lecteur JSON read-only dédié avec contrôle taille/date, trois tentatives et annulation.
- Deux lecteurs indépendants avec cache de dernière valeur valide marqué `MemoryCache` / `Stale`.
- `RankRecordsOverlayDataProvider` superposé à `HybridControlCenterDataProvider`, sans modification des lecteurs Phase 2.1.

### Validation sur la copie de test

- Racine explicitement fournie : `<COPIE_DE_TEST>\UnrankedServer`.
- Contrôle ponctuel exécuté avec les lecteurs compilés, puis retiré de la suite portable.
- 34 profils BOIII_XUID conformes sur 37 lus ; 3 fichiers portant un identifiant hexadécimal de 15 caractères sont ignorés et comptabilisés.
- Aucun remplissage automatique du caractère manquant et aucun affaiblissement de la validation XUID 16 caractères.
- Le fichier carte v4 fournit des records valides ; les entrées incomplètes éventuelles sont ignorées individuellement et comptabilisées.
- Taille, date de modification et SHA-256 de tous les JSON consultés identiques avant/après lecture.

### Compilations et tests finaux

- Debug : compilation réussie, 0 avertissement, 0 erreur.
- Debug : 75/75 tests réussis, 0 échec, 0 ignoré.
- Release : compilation réussie, 0 avertissement, 0 erreur.
- Release : 75/75 tests réussis, 0 échec, 0 ignoré.
- 10 fichiers XAML parsés avec succès.
- Scan des sources produit : aucune API d’écriture fichier, aucun réseau, aucun client RCON et aucun lancement de processus.
- `SimulationActionService` conserve `CommandSent = false` pour les simulations acceptées et rejetées.

### Fichiers créés

- `app/src/PinteMod.ControlCenter.Core/Contracts/IRankProfileReader.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/IRoundRecordReader.cs`
- `app/src/PinteMod.ControlCenter.Core/Models/RankRecordModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/RankRecordsPathPolicy.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/ReadOnlyRankJsonFileReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/RankProfileReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/RoundRecordReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/RankRecordsOverlayDataProvider.cs`
- `app/tests/PinteMod.ControlCenter.Tests/RankProfileReaderTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/RoundRecordReaderTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/RankRecordsOverlayDataProviderTests.cs`
- `docs/PHASE2_RANKS_RECORDS_DESIGN.md`

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter.Core/Models/ControlCenterModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Simulation/SimulatedControlCenterDataProvider.cs`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/DisplayItemViewModels.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/RecordsViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/RecordsView.xaml`
- `app/src/PinteMod.ControlCenter/Views/SettingsView.xaml`
- `app/tests/PinteMod.ControlCenter.Tests/LocalReaderTestSupport.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ReadOnlyGuaranteeTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`
- `app/README.md`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Problèmes rencontrés

- Une assertion MSTest attendait exactement `OperationCanceledException` alors que .NET renvoyait sa sous-classe valide `TaskCanceledException` ; seul le test a été assoupli pour accepter l’annulation coopérative.
- Trois identifiants historiques de la copie de test mesurent 15 caractères. Ils restent volontairement exclus pour ne pas inventer un XUID ni modifier la règle de sécurité des actions joueur.

### Validation humaine nécessaire

- Lancer l’application en mode hybride sur la copie de test, ouvrir Records et vérifier le rendu à taille normale et réduite.
- Confirmer visuellement les 34 profils lus, les 3 fichiers ignorés, les records de manches locaux et les badges simulés pour Ranked/Easter Egg.
- Produire une capture de la page Records si cette validation est concluante.

### Captures

- Aucune nouvelle capture produite pendant l’implémentation : aucun EXE WPF n’a été lancé par Codex.

## 2026-08-02 — Validation visuelle humaine Phase 2.2

### Objectif de la passe

Vérifier sur la copie de test l’affichage réel de la page Records, la provenance des données et la séparation entre données locales et simulées.

### Résultat

- Validation visuelle réussie sans correction bloquante.
- KPI visibles et cohérents : 34 profils Ranks, 7 records de manches, 1 Easter Egg simulé et statut Ranked simulé.
- Les 37 fichiers examinés et 3 profils ignorés correspondent au contrôle technique des identifiants 15 caractères.
- Les profils affichent pseudo, XUID abrégé, sessions, meilleure manche et temps total.
- Les records affichent classement, catégorie, manche, détenteurs, XUID abrégés, durée et provenance `Fichier local`.
- L’Easter Egg reste clairement distinct avec `Simulation` et `XUID non fourni — simulation`.
- Le badge `LECTURE LOCALE READ-ONLY`, les chemins logiques et les métadonnées de lecture sont visibles.
- Aucun chevauchement, troncature bloquante ou régression de navigation observé à 1908×1021.

### Captures archivées

- `app/artifacts/screenshots/phase2.2/records-overview-1908x1021.png`
  - SHA-256 : `7F93C752CA1FAFED34482AC4443D17F68BECB1333FD089D212889FDBBE5D0B59`
- `app/artifacts/screenshots/phase2.2/records-list-1908x1021.png`
  - SHA-256 : `41F5FD9A87CF4D70E25637D34E5DBE2F328FAAC68D8AF82E0C1AD5C91A50D127`

### Compilations et tests

- Aucun code modifié pendant la validation visuelle.
- Résultats conservés : Debug et Release, 0 avertissement, 0 erreur, 75/75 tests réussis dans chaque configuration.

### Suite

- Préparer le paquet autonome et le prompt ChatGPT Phase 2.2.

## 2026-08-02 — Paquet ChatGPT Phase 2.2

### Objectif de la passe

Fournir un ZIP autonome permettant à ChatGPT de contrôler le code read-only, la séparation des provenances, les tests et les captures validées de la Phase 2.2.

### Contenu préparé

- Guide `00_LIRE_EN_PREMIER.md`.
- Prompt final `PROMPT_CHATGPT_PHASE2_2.md` prêt à copier.
- Deux captures humaines de la page Records.
- Preuve synthétique des builds, 75 tests, contrôle d’intégrité et sécurité.
- Documents Phase 2.1/2.2, audit, décisions et suivi.
- Contrats JSON publics et référence graphique.
- Sources actuelles Core, Infrastructure, WPF et Tests, sans `bin/` ni `obj/`.
- Liste de fichiers et manifeste SHA-256 interne vérifié sans erreur.

### Exclusions vérifiées

- Aucun `server-sandbox/`, dossier `servtest`, JSON runtime ou BOIII_XUID réel complet.
- Aucun serveur, archive PinteMod, secret, BAT, EXE, DLL, PDB, `bin/`, `obj/` ou ancien paquet.
- `UI_FEEDBACK.md` n’est ni modifié ni inclus.

### Artefact produit

- `app/artifacts/PinteMod-ControlCenter-ChatGPT-Phase2.2-Review.zip`
- Taille : 2 179 409 octets.
- SHA-256 : `BDA45B9CA51F902365D0429E492491FB305B05FCB968B5B3B7D103926A76AA70`.
- Contrôle ZIP : 108 entrées, 0 doublon, 0 entrée interdite ; prompt et deux captures présents.
- Staging consultable : `app/artifacts/chatgpt-phase2.2-review/`.

### Compilations et tests

- Aucun code applicatif modifié pendant le conditionnement.
- Résultats conservés : Debug et Release, 0 avertissement, 0 erreur, 75/75 tests réussis dans chaque configuration.

### Prochaine validation humaine

- Transmettre le ZIP à ChatGPT et copier le contenu de `PROMPT_CHATGPT_PHASE2_2.md`.
- Revenir vers Codex avec le verdict complet ou chaque correction bloquante précise.

## 2026-08-02 — Corrections bloquantes de revue Phase 2.2

### Objectif de la passe

Appliquer uniquement les trois corrections bloquantes signalées : isolation stricte des entrées de record, absence de XUID complet dans les ViewModels/info-bulles et durées supérieures à 24 heures.

### Corrections réalisées

1. Chaque emplacement de record retourne désormais explicitement `Empty`, `Valid` ou `Invalid`. Un texte absent, trop long, de mauvais type, contenant un caractère de contrôle ou un XUID invalide incrémente uniquement `SlotsSkipped` et ne rejette plus le fichier carte.
2. `PlayerItemViewModel` n’expose plus `Model` ni `Xuid`, et `SimulationResultItemViewModel` n’expose plus `FullXuid`. Les ViewModels d’affichage copient uniquement les valeurs sûres et les XUID abrégés. Les trois info-bulles/bindings de XUID complet ont été supprimés.
3. La sélection BOIII_XUID complète reste dans `State/PlayerSelectionState` et dans une table privée du ViewModel d’actions ; elle n’est jamais bindable, tout en conservant le ciblage simulé par XUID.
4. `DurationDisplay` centralise les heures totales : `27:05:06`, `49:02:03` et `100:05:06` ne rebouclent plus à 24 heures.

### Tests de régression ajoutés

- texte de détenteur supérieur à 512 caractères isolé sans perte du record valide voisin ;
- inspection de toutes les propriétés publiques des ViewModels d’affichage avec un XUID sentinelle ;
- scan de tous les XAML interdisant `FullXuid` et `SelectedPlayer.Xuid` ;
- durées de 27 h, 49 h et 100 h vérifiées via convertisseur, record et profil.

### Compilations et tests

- Debug standard : compilation réussie, 0 avertissement, 0 erreur ; 79/79 tests réussis.
- Release : la première tentative standard a rencontré uniquement un verrou de fichier par la fenêtre Control Center encore ouverte (`PID 12540`). Aucun processus utilisateur n’a été arrêté.
- Release corrigée dans `app/artifacts/build/phase2.2-corrected/Release/` : compilation réussie, 0 avertissement, 0 erreur ; 79/79 tests réussis.
- 10 XAML valides.
- Scan produit : 0 API réseau/processus/écriture et 0 binding/info-bulle de XUID complet.
- Les lecteurs Phase 2.1 restent inchangés ; dates de modification antérieures à cette passe confirmées.

### Fichiers créés

- `app/src/PinteMod.ControlCenter/ViewModels/DurationDisplay.cs`
- `app/src/PinteMod.ControlCenter/State/PlayerSelectionState.cs`

### Fichier supprimé

- `app/src/PinteMod.ControlCenter/ViewModels/PlayerSelectionState.cs` — remplacé par un état interne non bindable, sans changement fonctionnel.

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter.Infrastructure/Local/RoundRecordReader.cs`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/Converters/UiConverters.cs`
- `app/src/PinteMod.ControlCenter/Controls/PlayerDetailsControl.xaml`
- `app/src/PinteMod.ControlCenter/ViewModels/DashboardViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/DisplayItemViewModels.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/PlayerActionsViewModelBase.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/PlayersViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/ServerView.xaml`
- `app/tests/PinteMod.ControlCenter.Tests/RoundRecordReaderTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`
- `docs/PHASE2_RANKS_RECORDS_DESIGN.md`

### Captures

- Les corrections ne modifient aucun élément visible dans les deux captures existantes : aucun XUID complet n’y figurait, toutes les durées capturées étaient inférieures à 24 h et la mise en page reste identique.
- Les captures validées sont donc conservées comme référence graphique dans le nouveau paquet, avec une preuve automatisée dédiée aux trois comportements corrigés.

### Sécurité

- Aucun RCON, réseau, secret, port, processus serveur, écriture PinteMod ou modification GSC ajouté.
- Aucun EXE applicatif ou serveur lancé par Codex.

### Paquet de revue corrigé

- Archive : `app/artifacts/PinteMod-ControlCenter-ChatGPT-Phase2.2-Corrections-Review.zip`.
- Taille : 2 184 227 octets.
- SHA-256 : `63CD0F2D21D6391539CBD6351DB1373B12C9593791FC59B83F5396A3993B993C`.
- Contrôle interne : 110 entrées, 0 doublon, 0 extension interdite, 0 dossier interdit.
- Manifeste interne : 107 fichiers vérifiés, 0 absence et 0 divergence d’empreinte.
- Contenu : sources actuelles sans sorties compilées, documents, contrats, preuves ciblées, prompt autonome et deux captures de référence.
- Captures : références visuelles Phase 2.2 inchangées, accompagnées d’une note explicite ; les trois corrections sont couvertes par les nouveaux tests automatisés.
- Validation humaine requise : transmettre cette archive à ChatGPT avec `PROMPT_CHATGPT_PHASE2_2_CORRECTIONS.md`, puis reporter son verdict à Codex.

## 2026-08-03 — Validation et clôture de la Phase 2.2

### Objectif de la passe

Consigner le verdict externe définitif après la correction des trois blocages de revue Phase 2.2.

### Verdict reçu

- **CORRECTIONS VALIDÉES**.
- Aucun blocage restant.
- Isolation des entrées de record invalides confirmée.
- Aucun XUID complet exposé par les ViewModels, info-bulles ou bindings XAML.
- Affichage des durées supérieures à 24 heures confirmé.
- Debug et Release confirmés à 0 avertissement, 0 erreur et 79/79 tests réussis.
- Les 10 fichiers XAML sont valides.
- Les lecteurs Phase 2.1 sont inchangés.
- Les garanties read-only, simulation et `CommandSent = false` restent respectées.

### Remarque non bloquante

- Le test de scan XAML suppose une sortie située sous l’arborescence de l’application. Cette fragilité ne concerne pas le produit ni la configuration de validation prévue ; aucune correction n’est requise pour clôturer la phase.

### Fichiers modifiés pendant cette passe

- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Clôture

- Phase 2.2 validée et clôturée sans condition restante.
- Aucun code, lecteur, XAML, test, GSC ou artefact de PinteMod modifié pendant cette passe documentaire.
- Aucune compilation supplémentaire nécessaire : le verdict confirme les preuves Debug et Release déjà produites.
- Aucune nouvelle capture requise.

## 2026-08-03 — Audit et lancement de la Phase 2.3

### Objectif de la passe

Identifier l’autorité locale réelle des Easter Egg Records et fixer un périmètre d’intégration strictement read-only avant implémentation.

### Sources confirmées

- Source de profils active : `boiii/scriptdata/pintemod/easter_eggs_v2/profiles.json`, schéma 3.
- Source de records officiels : `boiii/scriptdata/pintemod/easter_eggs_v2/maps/*.json`, schéma 2.
- Autorité d’identité : `BOIII_XUID`.
- Mode officiel : `per_map_validated_only` ; seuls les profils au statut exact `OFFICIAL` peuvent produire des records officiels.
- Copie de test : `zm_tomb` est déclaré `OFFICIAL`, mais aucun fichier officiel n’existe actuellement sous `maps/`.
- Le fichier observé sous `candidates/maps/` reste un candidat non officiel et sera exclu.

### Exclusions confirmées

- `candidates/`, `test/`, `backups/`, ancien arbre `easter_eggs/`, `.tmp`, `.bak` et logs.
- Aucun RCON, réseau, secret, processus, écriture PinteMod ou modification GSC.

### Intégrité de l’audit

- Les sources ont été consultées en lecture seule.
- Taille, date et SHA-256 des fichiers actifs contrôlés avant/après : 0 modification.

### Fichiers créés ou modifiés

- `docs/PHASE2_EASTER_EGG_RECORDS_DESIGN.md`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### État

- Audit terminé.
- Architecture ciblée documentée.
- Implémentation Phase 2.3 en cours.

## 2026-08-03 — Livraison technique de la Phase 2.3

### Objectif de la passe

Intégrer les Easter Egg Records officiels v2 dans le mode hybride local, sans modifier les baselines Phase 2.1/2.2 ni élargir le périmètre aux candidats ou aux logs.

### Fonctionnalités réalisées

- Lecture asynchrone et annulable de `easter_eggs_v2/profiles.json` schéma 3.
- Lecture des seuls JSON officiels directs de `easter_eggs_v2/maps/` schéma 2.
- Validation croisée : une carte doit être déclarée au statut exact `OFFICIAL`.
- Isolation par emplacement : une entrée invalide n’élimine pas les voisines valides.
- Prise en charge des quêtes fixes 4P créditant moins de quatre titulaires actifs.
- XUID complets conservés hors de la surface bindable et toujours abrégés dans l’interface.
- Repli sur la dernière valeur valide avec provenance mémoire et fraîcheur périmée après erreur.
- Superposition dédiée au-dessus de la Phase 2.2 ; suppression du record Easter Egg simulé en mode hybride.
- Affichage séparé de la source, de la provenance, des profils officiels, fichiers examinés et entrées ignorées.
- Un profil officiel valide sans fichier de carte produit un catalogue local vide valide.

### Validation sur la copie de test

- Profil : schéma 3, identité `BOIII_XUID`, mode `per_map_validated_only`.
- Profils `OFFICIAL` : 1 (`zm_tomb`).
- JSON officiels actifs sous `maps/` : 0.
- JSON candidat exclu : 1.
- JSON de test exclus : 6.
- Résultat attendu : 0 Easter Egg Record officiel.
- Contrôle taille/date/SHA-256 avant/après consultation : 0 modification.

### Compilations et tests

- Debug : compilation réussie, 0 avertissement, 0 erreur ; 92/92 tests réussis.
- Release : compilation réussie, 0 avertissement, 0 erreur ; 92/92 tests réussis.
- 10 fichiers XAML valides.
- 0 binding ou info-bulle vers un XUID complet.
- 0 API réseau, lancement de processus ou écriture de fichier dans les sources de production.
- Les deux occurrences de `RCON` restantes sont uniquement les libellés UI « aucun transport/client RCON ».
- `CommandSent = false` reste couvert par la suite complète.

### Problème rencontré

- La première compilation des nouveaux tests a signalé uniquement l’import `System.IO` manquant ; corrigé avant les validations finales.
- Le SDK global reste indisponible ; le SDK local .NET 8.0.423 déjà validé a été réutilisé.

### Fichiers créés

- `app/src/PinteMod.ControlCenter.Core/Contracts/IEasterEggRecordReader.cs`
- `app/src/PinteMod.ControlCenter.Core/Models/EasterEggRecordModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/EasterEggRecordsPathPolicy.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/ReadOnlyEasterEggJsonFileReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/EasterEggRecordReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/EasterEggRecordsOverlayDataProvider.cs`
- `app/tests/PinteMod.ControlCenter.Tests/EasterEggRecordReaderTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/EasterEggRecordsOverlayDataProviderTests.cs`
- `docs/PHASE2_EASTER_EGG_RECORDS_DESIGN.md`

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter.Core/Models/ControlCenterModels.cs`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/DisplayItemViewModels.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/RecordsViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/RecordsView.xaml`
- `app/tests/PinteMod.ControlCenter.Tests/LocalReaderTestSupport.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ReadOnlyGuaranteeTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`
- `app/README.md`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Baselines préservées

- Lecteurs Phase 2.1 inchangés.
- `RankProfileReader`, `RoundRecordReader` et `RankRecordsOverlayDataProvider` Phase 2.2 inchangés.
- Aucun GSC, contrat public, fichier `reference/`, `server-sandbox/` ou `UI_FEEDBACK.md` modifié.

### Validation humaine restante

- Lancer l’exécutable Release en mode hybride sur la copie de test explicitement désignée.
- Ouvrir Records et confirmer : Easter Egg Records = 0, source locale réussie, 1 profil officiel, 0 carte officielle examinée, aucun candidat affiché.
- Fournir une capture de la page Records avant préparation du paquet ChatGPT Phase 2.3.

### Captures

- Aucune capture produite par Codex : aucun exécutable WPF n’a été lancé.

## 2026-08-03 — Validation humaine locale de la Phase 2.3

### Objectif de la passe

Confirmer visuellement que la copie de test affiche uniquement l’autorité Easter Egg officielle v2 et ne promeut pas le candidat existant.

### Résultat validé

- Mode `MODE HYBRIDE LOCAL` visible.
- KPI Easter Egg Records : `0`.
- Libellé : `OFFICIELS LOCAUX · TOP 5`.
- Source Easter Egg : lecture réussie, fraîche, provenance fichier local.
- Message : profil officiel lu, aucun Easter Egg Record officiel enregistré.
- Compteurs : 1 profil officiel, 0 carte examinée, 0 fichier et 0 entrée ignorés.
- Aucun record Easter Egg candidat ou simulé dans la liste.
- Les 34 profils Ranks et 7 records de manches Phase 2.2 restent présents sans régression visible.

### Comparaison avant/après

- Avant Phase 2.3 : un Easter Egg Record simulé apparaissait en bas de la liste avec `XUID non fourni — simulation`.
- Après Phase 2.3 : ce record simulé a disparu et le KPI officiel local vaut zéro, conformément aux données officielles réellement disponibles.

### Captures enregistrées

- `app/artifacts/screenshots/phase2.3/records-phase2.3-official-empty-1900x1014.png`
  - 1900×1014, 140 773 octets.
  - SHA-256 : `547BCBB0A844E3175396966BEDFC50801E091F7A9DED880590A0F07DF9C92B99`.
- `app/artifacts/screenshots/phase2.3/records-phase2.2-simulated-before-1899x1021.png`
  - 1899×1021, 132 192 octets.
  - SHA-256 : `41F5FD9A87CF4D70E25637D34E5DBE2F328FAAC68D8AF82E0C1AD5C91A50D127`.

### Fichiers créés ou modifiés

- Deux captures sous `app/artifacts/screenshots/phase2.3/`.
- `docs/CODEX_PROGRESS.md`.
- `docs/TODO.md`.
- `docs/DECISIONS.md`.

### État

- Validation humaine locale réussie.
- Préparation du paquet ChatGPT Phase 2.3 autorisée.
- Aucun code, serveur, JSON runtime, GSC ou fichier `UI_FEEDBACK.md` modifié pendant cette validation.

## 2026-08-03 — Paquet ChatGPT Phase 2.3

### Objectif de la passe

Produire un paquet autonome et assaini permettant la revue externe de la Phase 2.3.

### Artefact produit

- Archive : `app/artifacts/PinteMod-ControlCenter-ChatGPT-Phase2.3-Review.zip`.
- Taille : 2 234 755 octets.
- SHA-256 : `013E5E103125181B8446740CA144352197F727A91DCB4C3B7195D3017DF9D32E`.
- Staging consultable : `app/artifacts/chatgpt-phase2.3-review/`.
- Prompt : `PROMPT_CHATGPT_PHASE2_3.md`.

### Contrôles du paquet

- 118 entrées ZIP.
- 0 doublon.
- 0 extension interdite.
- 0 dossier `bin`, `obj`, `artifacts` interne ou `server-sandbox`.
- 94 fichiers source actuels.
- 2 captures présentes.
- Manifeste : 115 fichiers vérifiés, 0 absence et 0 divergence SHA-256.
- `UI_FEEDBACK.md` non inclus et non modifié.

### Exclusions

- Aucun JSON runtime de la copie de test.
- Aucun log privé, secret, GSC, serveur, archive PinteMod, BAT, EXE, DLL ou PDB.
- Aucun XUID réel complet ; les seuls XUID complets des sources sont des valeurs fictives de test.

### Prochaine validation

- Transmettre le ZIP à ChatGPT.
- Copier intégralement le contenu de `PROMPT_CHATGPT_PHASE2_3.md`.
- Rapporter le verdict complet à Codex pour clôturer ou corriger la Phase 2.3.

## 2026-08-03 — Bloc A, passe d’implémentation consolidée en cours

### Objectif

Finaliser en une seule livraison la lecture locale hybride strictement read-only, sans créer de micro-phase ni modifier les baselines 2.1, 2.2 et 2.3.

### Réalisé à ce point de contrôle

- Ajout des modèles et contrats pour la vérification d’installation, l’état complémentaire du Ban Service, les métadonnées joueur et les logs structurés.
- Ajout d’une politique de chemins limitée à la session active et à neuf familles de logs autorisées.
- Ajout du filtrage central des XUID, IP, GUID, chemins, champs sensibles et caractères de contrôle.
- Lecture incrémentale bornée, cache mémoire, isolation des lignes malformées, changement de session et annulation.
- Agrégation au-dessus des providers Phase 2.1/2.2/2.3, sans les modifier.
- Actualisation hybride automatique toutes les deux secondes, mono-exécution et annulable.
- Intégration initiale Dashboard, Joueurs, Serveur, Records, Logs et Paramètres.

### Compilation intermédiaire

- Debug : réussie, 0 avertissement, 0 erreur.
- Release et tests : à exécuter après ajout de la couverture Bloc A.

### Fichiers

- Créations limitées à `app/src/` ; modifications de présentation limitées à `app/src/PinteMod.ControlCenter/`.
- Suivi mis à jour sous `docs/`.
- Aucun fichier `reference/`, `contracts/`, `server-sandbox/`, GSC ou `UI_FEEDBACK.md` modifié.

### Problème rencontré

- Le SDK global reste absent ; le SDK local officiel .NET 8.0.423 déjà utilisé par les phases validées a été réutilisé.
- Les sorties de compilation sont isolées sous `app/artifacts/block-a-build/`.

### Validation humaine

- En attente de la fin de la livraison globale : captures, preuve read-only et ZIP unique restent à produire.

## 2026-08-03 — Bloc A, livraison consolidée terminée

### Objectif de la passe

Finaliser en une seule livraison toute la lecture locale hybride read-only autorisée, intégrer les sources réellement disponibles dans les six écrans, préserver les baselines 2.1 à 2.3 et produire une revue globale unique.

### Réalisé

- Lecture de `installation_verification.json`, `service_status.json`, `roles.json` et des langues manuelles/automatiques lorsqu’ils existent.
- Lecture incrémentale de neuf familles de logs placées sur liste blanche, uniquement dans le dossier de la session active.
- Cache mémoire borné à 500 événements, lecture limitée à 2 Mio par log, attente des lignes partielles, isolation des lignes malformées, prise en charge des troncatures et remise à zéro sur changement de session.
- Filtrage central avant présentation des XUID, IP, GUID, chemins, champs sensibles et caractères de contrôle.
- Séparation explicite état de lecture, fraîcheur, âge et provenance ; une valeur issue du cache reste marquée périmée.
- Données non observables neutralisées : serveur BOIII, maximum de joueurs, points, vie et inventaire ne reprennent aucune valeur simulée en mode hybride.
- Agrégateur Bloc A superposé aux providers validés des Phases 2.1, 2.2 et 2.3.
- Actualisation automatique mono-exécution toutes les deux secondes en mode hybride, annulation à la fermeture et aucune I/O sur le thread UI.
- Intégration complète Dashboard, Joueurs, Serveur, Records, Logs et Paramètres.
- Documentation de l’absence réelle de snapshot stable pour armes, arme équipée, Pack-a-Punch, munitions et atouts ; aucun GSC modifié.

### Baselines préservées

- 15 fichiers de lecteurs/providers 2.1, 2.2 et 2.3 comparés à la source du paquet Phase 2.3 validé.
- Différences SHA-256 : 0.
- `LocalPinteModOptions` et `ReadOnlyJsonFileReader` Phase 2.1 restent strictement identiques ; le Bloc A possède sa propre politique de chemins et son propre lecteur JSON.

### Fonctionnalités disponibles

- Simulation inchangée et toujours active sans argument.
- Mode hybride uniquement avec `--data-mode=hybrid-local --server-root=<chemin absolu>`.
- Carte/session/services, Ranks, records de manches, Easter Egg Records officiels, diagnostics, métadonnées et événements locaux selon disponibilité réelle.
- Joueurs et présence dérivés uniquement de JOIN/ACTIVE/LEAVE ; XUID abrégé dans l’interface.
- Diagnostics d’installation sans chemin racine ni détails privés.
- Flux Logs filtré avec temps relatif de session, famille source et compteurs de tolérance.
- Actions joueur/serveur toujours simulées par `ISimulationActionService`, avec `CommandSent = false`.

### Résultats des compilations et tests

- Debug : compilation réussie, 0 avertissement, 0 erreur.
- Debug : 113/113 tests réussis, 0 échec, 0 test ignoré.
- Release : compilation réussie, 0 avertissement, 0 erreur.
- Release : 113/113 tests réussis, 0 échec, 0 test ignoré.
- 10/10 fichiers XAML valides.
- Scan de production : 0 API réseau, lancement de processus ou écriture de fichier/répertoire.
- 22/22 sources autorisées de la copie de test : taille, date UTC et SHA-256 identiques avant/après ; 0 modification.

### Fichiers créés

- `app/src/PinteMod.ControlCenter.Core/Contracts/IBanServiceStatusReader.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/IControlCenterSnapshotMonitor.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/IInstallationVerificationReader.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/ILocalPlayerMetadataReader.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/IStructuredLogReader.cs`
- `app/src/PinteMod.ControlCenter.Core/Models/BlockALocalModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/BanServiceStatusReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/BlockAControlCenterDataProvider.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/BlockALocalPathPolicy.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/HybridLocalSnapshotMonitor.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/InstallationVerificationReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/LocalPlayerMetadataReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/LogPrivacyFilter.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/ReadOnlyBlockAJsonFileReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/StructuredLogReader.cs`
- sept nouveaux fichiers de tests Bloc A sous `app/tests/PinteMod.ControlCenter.Tests/`.
- `docs/BLOCK_A_LOCAL_READ_FINAL_DESIGN.md`.
- documents et preuves du paquet sous `app/artifacts/chatgpt-block-a-review/`.

### Fichiers modifiés

- `app/README.md`.
- `app/src/PinteMod.ControlCenter.Core/Models/ControlCenterModels.cs`.
- composition root, chrome et ViewModels sous `app/src/PinteMod.ControlCenter/`.
- les six vues WPF et `Controls/PlayerDetailsControl.xaml`.
- `LocalReaderTestSupport.cs`, `ReadOnlyGuaranteeTests.cs` et `ViewModelTests.cs`.
- `docs/CODEX_PROGRESS.md`, `docs/TODO.md` et `docs/DECISIONS.md`.

La liste exacte, fichier par fichier, est fournie dans `app/artifacts/chatgpt-block-a-review/preuves/FICHIERS_CHANGES.txt`.

### Problèmes rencontrés

- Le SDK global .NET reste absent ; le SDK local officiel 8.0.423 déjà validé a été utilisé.
- Une première intégration interne élargissait deux fichiers Phase 2.1. Elle a été isolée avant livraison : les deux fichiers ont retrouvé exactement leur empreinte Phase 2.3.
- Un identifiant de session malveillant pouvait provoquer une exception de politique de chemin ; il produit désormais un état local `Invalid` sans sortir de `ServerRoot`, couvert par un test.

### Validation humaine nécessaire

- Transmettre l’unique ZIP Bloc A à ChatGPT avec le prompt inclus.
- Examiner les sept captures et confirmer l’absence de régression visuelle.
- Aucun autre contrôle intermédiaire ni micro-phase n’est demandé ; seul le verdict global du Bloc A reste à recevoir.

### Captures finales

- `app/artifacts/block-a-review/captures/dashboard-hybrid-local.png`
- `app/artifacts/block-a-review/captures/players-hybrid-local.png`
- `app/artifacts/block-a-review/captures/server-hybrid-local.png`
- `app/artifacts/block-a-review/captures/server-installation-checks.png`
- `app/artifacts/block-a-review/captures/records-hybrid-local.png`
- `app/artifacts/block-a-review/captures/logs-hybrid-local.png`
- `app/artifacts/block-a-review/captures/settings-hybrid-local.png`

La capture intermédiaire `logs-hybrid-local-pre-fix.png` est conservée hors paquet et n’est pas une preuve finale.

### Paquet de revue global

- Archive : `app/artifacts/PinteMod-ControlCenter-ChatGPT-Bloc-A-Review.zip`.
- Taille : 623 523 octets.
- SHA-256 : `EB1B3847A757B33A608D8FD1EBEFE2BEDF49B3205D0F92FF112491EA9781575E`.
- Staging consultable : `app/artifacts/chatgpt-block-a-review/`.
- Prompt à transmettre : `app/artifacts/chatgpt-block-a-review/PROMPT_CHATGPT_BLOCK_A.md`.
- 141 fichiers dans l’archive, 0 doublon, 0 fichier interdit.
- Manifeste : 140 empreintes vérifiées, 0 absence, 0 divergence.
- 116 fichiers applicatifs/source, 7 captures finales.
- Aucun runtime de la copie de test, log brut, secret, GSC, serveur, BAT, EXE, DLL, PDB, `server-sandbox/`, `reference/` ou `UI_FEEDBACK.md` inclus.

## 2026-08-03 — Corrections globales après revue du Bloc A

### Objectif

Appliquer en une seule livraison les sept blocages obligatoires de la revue globale, sans nouvelle micro-phase ni modification des baselines 2.1 à 2.3.

### Corrections réalisées

- Neutralisation ajoutée pour IPv6, chemins UNC et chemins Unix, en complément des XUID, IPv4, GUID et chemins Windows.
- Suppression des propriétés publiques `BlockALocalSnapshot`, `SnapshotDataContext` et `ServerState` des ViewModels Dashboard/Serveur ; libellés de session et sources de logs rendus génériques.
- Messages d’exception remplacés par des messages utilisateur constants dans Page, Shell et l’échec de démarrage.
- Racines et entrées JSON de forme inattendue transformées en état `Invalid` sans exception sortante.
- Cache des métadonnées joueur conservé en `MemoryCache` / `Stale` lorsqu’un `roles.json` invalide ne fournit aucune nouvelle valeur valide.
- Remplacements atomiques de logs détectés par date de création et empreinte du préfixe ; reconstruction complète de l’état de session.
- Moniteur exécuté hors contexte UI ; tâche conservée, annulée et attendue avant destruction des lecteurs.
- Quatre captures incohérentes remplacées avec le pied hybride correct.

### Fichiers applicatifs modifiés

- `app/src/PinteMod.ControlCenter.Infrastructure/Local/{BanServiceStatusReader,HybridLocalSnapshotMonitor,InstallationVerificationReader,LocalPlayerMetadataReader,LogPrivacyFilter,ReadOnlyBlockAJsonFileReader,StructuredLogReader}.cs`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/{DashboardViewModel,LogsViewModel,PageViewModel,ServerViewModel,ShellViewModel}.cs`
- `app/src/PinteMod.ControlCenter/Views/{DashboardView,ServerView}.xaml`
- tests correspondants sous `app/tests/PinteMod.ControlCenter.Tests/`, y compris `LocalReaderTestSupport.cs` et `ViewModelTests.cs`.

### Compilations et tests

- Tests ciblés : 46/46 réussis.
- Debug : 0 avertissement, 0 erreur ; 124/124 tests réussis.
- Release : 0 avertissement, 0 erreur ; 124/124 tests réussis.
- 10/10 XAML valides.
- Scan production : 0 API réseau, lancement de processus ou écriture.
- Baselines : 15 fichiers contrôlés, 0 différence.
- Copie de test : 22 sources contrôlées par taille/date/SHA-256, 0 différence.

### Captures remplacées

- `app/artifacts/block-a-review/captures/players-hybrid-local.png` — SHA-256 `415639F0AB9D2DAF8BC4C1246ECF529DD40A7870740B2F501A033EAF575608CB`.
- `app/artifacts/block-a-review/captures/records-hybrid-local.png` — SHA-256 `5EB28E0ABEA563517A25A42AF1A5476223C3CF5C3843FE500D962E4DF895175E`.
- `app/artifacts/block-a-review/captures/server-hybrid-local.png` — SHA-256 `31662367B9D57DB7891AB68FDB6EDDDA40C310BA46D62F6D0A1215C1EB6116BA`.
- `app/artifacts/block-a-review/captures/server-installation-checks.png` — SHA-256 `3CF54192435F49AC3B6F27CDF604880F50E0FA796CCA8331FACE1048769887E4`.

### Problèmes rencontrés

- Une première sortie ciblée pointait vers un nouveau dossier sans assets restaurés ; la validation a été relancée dans le dossier d’artefacts existant, sans téléchargement ni réseau.
- Les premiers tests ciblés ont détecté des accesseurs privés redondants et une différence `TaskCanceledException`/`OperationCanceledException` ; les deux ont été corrigés avant la suite finale.

### Validation humaine restante

- Vérifier uniquement les quatre captures remplacées et rendre le verdict final du Bloc A.
- Aucun blocage technique connu ne reste.

## 2026-08-09 — Audit de reprise orienté MVP

### Objectif

Reconstituer rapidement l’état réel du Control Center et définir la prochaine tranche utile, sans refonte ni nouvelle fonctionnalité applicative.

### État confirmé

- Les lecteurs validés des Phases 2.1, 2.2, 2.3 et du Bloc A sont présents et composés dans l’application.
- La simulation reste le mode par défaut ; le mode hybride exige toujours `--data-mode=hybrid-local --server-root=<chemin absolu>`.
- Le mode hybride actualise automatiquement le snapshot toutes les deux secondes et alimente les six pages.
- La dernière validation complète reste : Debug/Release, 0 avertissement, 0 erreur, 124/124 tests réussis.
- Aucun client RCON, transport UDP, stockage DPAPI ou configuration opérateur persistante n’existe encore dans les sources applicatives.

### Données réelles disponibles

- Session/carte, heartbeats, diagnostics d’installation, état Ban Service, profils Ranks, records de manches, Easter Egg Records officiels et logs structurés autorisés.
- Joueurs dérivés de `JOIN` / `ACTIVE` / `LEAVE` : pseudo, client, XUID interne avec affichage abrégé, présence inférée, rôle, langue, pays et état mute/ban lorsqu’un événement ou une métadonnée le fournit.
- Manche, durée et statut Unranked dérivés des logs lorsqu’une preuve explicite existe ; sinon ces valeurs restent inconnues.
- Points, vie, kills, downs, revives, inventaire, atouts, munitions, arme équipée et Pack-a-Punch ne disposent d’aucune source runtime stable intégrée.
- `feedback.latest.txt` et `pause.log` sont annoncés côté outils PinteMod, mais ne sont présents ni dans la copie de test auditée ni dans les lecteurs actuels ; leur format doit être confirmé avant intégration.

### Prochaine tranche recommandée

Ajouter une configuration opérateur read-only minimale pour choisir explicitement Local ou LAN et le chemin PinteMod, sans découverte automatique, sans secret et sans toucher aux lecteurs existants. Le RCON viendra ensuite dans un composant isolé, après validation de ce mode opérateur.

### Fichiers modifiés

- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Validation

- Aucun code applicatif modifié ; compilations, tests et captures non relancés pendant cet audit documentaire.
- Validation humaine future nécessaire : fournir un exemple neutralisé et le chemin attendu de `feedback.latest.txt` et `pause.log` lorsqu’ils seront disponibles.

## 2026-08-09 — Première tranche MVP : test opérateur Local/LAN

### Objectif

Permettre à l’opérateur de vérifier depuis Paramètres une racine PinteMod locale ou LAN, sans RCON, découverte automatique, secret ou écriture serveur.

### Réalisé

- Ajout du choix `LOCAL` / `LAN` et de la saisie explicite du chemin dans Paramètres.
- Ajout d’un bouton `TESTER LA SOURCE` qui contrôle `current_session.json` et les quatre heartbeats avec les lecteurs Phase 2.1 existants.
- Le mode LAN exige un chemin UNC ; le mode Local refuse les chemins UNC.
- Le test s’exécute hors du thread UI, affiche un résultat `PRÊT`, `PARTIEL`, `INCOMPLET` ou `REFUSÉ` et ne restitue ni chemin ni exception brute dans son diagnostic.
- Le fournisseur actif reste inchangé : cette tranche ne persiste pas encore la configuration et ne remplace pas l’activation explicite au démarrage.
- La carte Sécurité indique désormais correctement qu’aucun port entrant ni mécanisme de découverte réseau n’existe.

### Fichiers créés

- `app/src/PinteMod.ControlCenter.Core/Contracts/ILocalDataSourceProbe.cs`
- `app/src/PinteMod.ControlCenter.Core/Models/OperatorDataSourceModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/LocalDataSourceProbe.cs`
- `app/tests/PinteMod.ControlCenter.Tests/LocalDataSourceProbeTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/SettingsOperatorViewModelTests.cs`

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/SettingsView.xaml`
- `app/README.md`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Validation

- Tests ciblés : 6/6 réussis.
- Debug : 0 avertissement, 0 erreur ; 130/130 tests réussis.
- Release : 0 avertissement, 0 erreur ; 130/130 tests réussis.
- XAML : 10/10 fichiers valides.
- Scan applicatif : aucun client RCON, socket, lancement de processus ou écriture PinteMod ajouté.
- Test d’intégrité : les cinq fichiers lus par la sonde de régression conservent taille, date UTC et SHA-256.

### Reste à faire

- Validation visuelle manuelle de la nouvelle carte Paramètres.
- Activation du mode opérateur depuis l’interface et persistance de la configuration non sensible.
- `feedback.latest.txt` et `pause.log` restent en attente d’exemples neutralisés et de chemins confirmés.

## 2026-08-09 — Lot MVP opérateur jusqu’à validation RCON humaine

### Objectif

Avancer sans micro-phase jusqu’au premier point exigeant réellement un BOIII lancé et un secret saisi par l’opérateur.

### Fonctionnalités livrées

- Configuration Local/LAN persistante sans secret, activation au prochain démarrage et priorité conservée aux arguments explicites.
- Repli automatique en simulation si une source enregistrée devient inaccessible.
- Live Console avec filtres existants, filtre RCON, auto-scroll, pause/reprise de l’affichage et compteur d’événements en attente.
- Client RCON UDP compatible avec le protocole BOIII audité, sans lancement de PowerShell ni serveur.
- Secret propre au Control Center protégé par DPAPI `CurrentUser`, jamais bindé ou réaffiché et séparé du JSON de configuration.
- Liste blanche limitée à `ezzhealth full` et `ezzpausestatus` ; aucun texte libre et aucune commande automatique.
- Résultats RCON neutralisés et conservés dans un audit de session borné en mémoire pour la Live Console.
- Actions joueur et serveur toujours simulées via `ISimulationActionService` avec `CommandSent = false`.

### Fichiers principaux créés

- contrats et modèles opérateur/RCON sous `app/src/PinteMod.ControlCenter.Core/`.
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/JsonOperatorConfigurationStore.cs`.
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/{BoiiiUdpRconClient,InMemoryOperatorActivityStore,RconDiagnosticService}.cs`.
- `app/src/PinteMod.ControlCenter/Security/DpapiRconSecretStore.cs`.
- tests de configuration, DPAPI, UDP/RCON et Live Console sous `app/tests/PinteMod.ControlCenter.Tests/`.
- `docs/MVP_OPERATOR_VALIDATION_FR.md`.

### Fichiers principaux modifiés

- `app/src/PinteMod.ControlCenter/App.xaml.cs`.
- `app/src/PinteMod.ControlCenter/Configuration/ApplicationStartupOptions.cs`.
- `app/src/PinteMod.ControlCenter/ViewModels/{LogsViewModel,SettingsViewModel,ShellViewModel}.cs`.
- `app/src/PinteMod.ControlCenter/Views/{LogsView,SettingsView}.xaml` et leurs code-behind.
- `app/README.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md` et `docs/DECISIONS.md`.

### Validation automatisée

- Tests ciblés opérateur/DPAPI/RCON/Live Console : 24/24 réussis.
- Debug : 0 avertissement, 0 erreur ; 148/148 tests réussis.
- Release : 0 avertissement, 0 erreur ; 148/148 tests réussis.
- XAML : 10/10 fichiers valides.
- Protocole vérifié sur boucle UDP locale : préfixe, paquet, commande et retrait de `print` conformes.
- Scan des commandes réelles : exactement deux chaînes autorisées.
- Scan des écritures : uniquement configuration et secret sous le dossier applicatif local ; aucune écriture PinteMod et aucun lancement de processus.
- Aucun binding XAML vers un XUID complet ajouté ; réponses RCON filtrées pour XUID, IPv4/IPv6, GUID et chemins.

### Validation humaine indispensable

- Suivre `docs/MVP_OPERATOR_VALIDATION_FR.md` avec un serveur déjà lancé par l’opérateur.
- Vérifier visuellement Paramètres et Live Console.
- Valider `ezzhealth full`, puis `ezzpausestatus`, sur la même machine ; tester le LAN ensuite sans modifier automatiquement le pare-feu.
- Ne transmettre aucun secret, fichier DPAPI, XUID complet ou IP joueur.
- Aucun bouton gameplay réel ne doit être développé avant ce retour.

## 2026-08-09 — Correctif du crash de la page Paramètres

### Objectif

Corriger le crash reproductible lors de l’ouverture de Paramètres sans modifier le transport RCON ni le périmètre fonctionnel.

### Cause et correction

- L’événement Windows `.NET Runtime 1026` identifiait une liaison WPF bidirectionnelle implicite vers la propriété `RconResponse`, dont le setter est privé.
- La liaison du champ de réponse, déjà non éditable visuellement, est désormais explicitement `Mode=OneWay`.
- Un test WPF STA instancie la vraie vue Paramètres avec son ViewModel, exécute sa mise en page et vide la file du Dispatcher afin de détecter ce type d’erreur de liaison au runtime.

### Fichiers créés ou modifiés

- Modifié : `app/src/PinteMod.ControlCenter/Views/SettingsView.xaml`.
- Créé : `app/tests/PinteMod.ControlCenter.Tests/SettingsViewRuntimeTests.cs`.
- Modifiés : `docs/CODEX_PROGRESS.md`, `docs/TODO.md`, `docs/DECISIONS.md`.

### Validation

- Test ciblé Paramètres : 1/1 réussi.
- Debug : 0 avertissement, 0 erreur ; 149/149 tests réussis.
- Release : 0 avertissement, 0 erreur ; 149/149 tests réussis.
- Exécutable corrigé : `app/src/PinteMod.ControlCenter/bin/Release/net8.0-windows/PinteMod.ControlCenter.exe`.
- Aucun serveur, processus BOIII ou commande RCON lancé pendant le diagnostic et la correction.

### Validation humaine restante

- Relancer l’exécutable Release et confirmer que Paramètres s’ouvre sans fermeture de l’application.
- Reprendre ensuite la validation manuelle `ezzhealth full`, puis `ezzpausestatus`, uniquement avec un serveur déjà lancé par l’opérateur.

## 2026-08-09 — Lisibilité et accompagnement RCON dans Paramètres

### Objectif

Corriger les contrôles blanc sur blanc signalés sur la capture humaine et expliquer le test RCON sans supposer de connaissance technique.

### Réalisé

- Tous les boutons sans style explicite utilisent désormais le thème sombre commun, y compris les états désactivés.
- Le champ secret `PasswordBox` dispose d’un fond sombre, d’un texte lisible et d’un focus bleu cohérent avec les `TextBox`.
- La carte RCON est marquée `FACULTATIF` et explique en trois étapes : adresse/port, secret local protégé, test manuel.
- L’interface précise qu’une copie de fichiers serveur ne suffit pas : BOIII doit déjà être lancé par l’opérateur pour répondre.
- Les boutons utilisent des intitulés non techniques ; les commandes autorisées restent indiquées dans les info-bulles et en pied de carte.
- Une aide visible explique pourquoi la mémorisation peut être désactivée tant que la source PinteMod n’a pas été testée.

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter/Themes/PinteModTheme.xaml`.
- `app/src/PinteMod.ControlCenter/Views/SettingsView.xaml`.
- `app/tests/PinteMod.ControlCenter.Tests/SettingsViewRuntimeTests.cs`.
- `docs/CODEX_PROGRESS.md`, `docs/TODO.md`, `docs/DECISIONS.md`.

### Validation

- Tests ciblés Paramètres : 5/5 réussis.
- Test runtime : vérification du contraste du champ secret, des boutons et présence de l’aide RCON.
- Debug : 0 avertissement, 0 erreur ; 149/149 tests réussis.
- Release : 0 avertissement, 0 erreur ; 149/149 tests réussis.
- Aucun serveur BOIII, commande RCON ou fichier PinteMod utilisé ou modifié.

### Validation humaine restante

- Relancer l’exécutable Release et vérifier la lisibilité de Paramètres.
- La validation RCON peut être reportée : elle nécessite un vrai serveur déjà en cours d’exécution, pas la copie de test arrêtée.

### Retour humain

- Validation reçue le 2026-08-09 : ouverture de Paramètres sans crash, boutons et champs lisibles, parcours RCON compréhensible.
- Aucun diagnostic RCON lancé, le serveur réel accueillant actuellement des joueurs.
- Le test réel reste reporté à une période sans joueur afin de ne pas perturber la session.

## 2026-08-09 — Packaging autonome MVP Preview

### Objectif

Continuer l’avancement sans contacter le serveur actif et préparer un exécutable portable pour les futurs essais local/LAN.

### Réalisé

- Métadonnées produit fixées à `2.2.0`, version informative `2.2.0-mvp-preview`.
- Publication Windows x64 self-contained : aucun SDK ou runtime .NET requis sur le poste cible.
- Ajout d’un `LISEZ-MOI.txt` non technique dans le paquet.
- Mise à jour du guide de validation avec les nouveaux intitulés de Paramètres et l’interdiction de tester RCON en présence de joueurs.
- Archive : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-win-x64.zip`.
- Empreinte SHA-256 : `E944D936BE90CE517B24A434830E74FE711CCB4E5047925E5EE3C9CDA06F2650`.

### Contrôles du paquet

- 466 entrées ; taille compressée : 66,78 Mio.
- 0 chemin ZIP dangereux.
- 0 fichier de configuration opérateur, secret DPAPI, log, donnée serveur, PDB ou élément `server-sandbox`.
- Version de fichier de l’exécutable : `2.2.0.0`.
- Debug : 0 avertissement, 0 erreur ; 149/149 tests réussis.
- Release : 0 avertissement, 0 erreur ; 149/149 tests réussis.

### Fichiers créés ou modifiés

- Modifié : `app/src/PinteMod.ControlCenter/PinteMod.ControlCenter.csproj`.
- Créé : `app/packaging/LISEZ-MOI.txt`.
- Créés : le dossier publié `app/artifacts/mvp-preview-win-x64/`, le ZIP MVP Preview et son fichier SHA-256.
- Modifiés : `app/README.md`, `docs/MVP_OPERATOR_VALIDATION_FR.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md`, `docs/DECISIONS.md`.

### Validation humaine restante

- Aucun essai serveur n’a été lancé pendant cette passe.
- Tester ultérieurement le ZIP sur le portable, uniquement sans joueur pour la partie RCON.

### Retour humain et audit des prochaines commandes

- Le paquet portable a été extrait, lancé et validé humainement sur le PC de développement.
- L’archive stable v2.1.1 confirme que `points`, `ammo`, `godmode`, `ezzspawn`, `ezzrevive`, armes, atouts et modération acceptent `BOIII_XUID` comme sélecteur ; aucun ciblage par pseudo ne sera nécessaire dans le Control Center.
- Aucun contrat ou script Community Soft Pause v0.3 n’existe dans l’archive stable ni dans la copie de test actuelle. Le vrai bouton Pause serveur ne peut pas être branché sans la commande exacte de cette extension.

## 2026-08-09 — Observation Community Soft Pause v0.3 et MVP Preview 2

### Objectif

Exploiter le module Community Soft Pause réellement publié sur GitHub pour rendre son état et ses événements lisibles dans le Control Center, tout en laissant les commandes de modification verrouillées jusqu’à une validation RCON sur serveur vide.

### Sources vérifiées

- Dépôt public `BiereFraiche/PinteMod`, branche `main`, commit observé `7d5f33489d8635c460d3eb63bb04226c7aa3f326`.
- Module `boiii/custom_scripts/ezz_admin_pause_community_experimental.gsc`, Community Pause EXPERIMENTAL v0.3.
- Commandes confirmées : `ezzpausestatus`, `ezzpauseforce` et `ezzresume`.
- Feedback confirmé : `boiii/scriptdata/pintemod/remote/feedback.latest.txt`.
- Journal confirmé : `boiii/scriptdata/pintemod/logs/pause.log`.
- Événements confirmés : `PAUSE_START`, `PAUSE_END`, `PAUSE_COUNT`, `PAUSE_VOTE_START`, `RESUME_VOTE_START`, `VOTE_RESULT` et `STATUS`.

### Réalisé

- Ajout d’un lecteur texte strict, asynchrone, borné et read-only pour `feedback.latest.txt`.
- Séparation du statut, de l’état de lecture, de la fraîcheur, de l’âge et de la provenance.
- Un feedback vieux de plus de 45 secondes, invalide ou antérieur au manifeste courant ne prouve jamais l’état actuel : l’interface affiche `INCONNU`.
- Conservation d’une dernière valeur valide uniquement comme cache périmé, sans la conserver en vert.
- Ajout d’un lecteur incrémental borné de `pause.log`, démarrant en fin de fichier et redémarrant en fin de fichier après rotation/remplacement.
- Liste blanche limitée aux sept événements v0.3 confirmés ; XUID et champs inconnus exclus des détails.
- Ajout de la carte **Pause communautaire** dans la page Serveur avec état, version, reprise automatique, compteur, vote, fraîcheur et provenance.
- Fusion des nouveaux événements de pause dans la Live Console, catégorie `PAUSE`.
- Aucun bouton `ezzpauseforce` ou `ezzresume` activé : le serveur réel accueillait des joueurs et les diagnostics terrain restent reportés.

### Fichiers créés

- `app/src/PinteMod.ControlCenter.Core/Contracts/ICommunityPauseStatusReader.cs`.
- `app/src/PinteMod.ControlCenter.Core/Contracts/ICommunityPauseLogReader.cs`.
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/CommunityPauseStatusReader.cs`.
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/CommunityPauseLogReader.cs`.
- `app/tests/PinteMod.ControlCenter.Tests/CommunityPauseStatusReaderTests.cs`.
- `app/tests/PinteMod.ControlCenter.Tests/CommunityPauseLogReaderTests.cs`.
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-2-win-x64.zip`.
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-2-win-x64.zip.sha256`.
- dossier publié `app/artifacts/mvp-preview-2-win-x64/`.

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter.Core/Models/BlockALocalModels.cs`.
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/BlockALocalPathPolicy.cs`.
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/BlockAControlCenterDataProvider.cs`.
- `app/src/PinteMod.ControlCenter/App.xaml.cs`.
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`.
- `app/src/PinteMod.ControlCenter/Views/ServerView.xaml`.
- `app/tests/PinteMod.ControlCenter.Tests/LocalReaderTestSupport.cs`.
- `app/tests/PinteMod.ControlCenter.Tests/BlockAControlCenterDataProviderTests.cs`.
- `app/tests/PinteMod.ControlCenter.Tests/ReadOnlyGuaranteeTests.cs`.
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`.
- `app/README.md`, `docs/MVP_OPERATOR_VALIDATION_FR.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md` et `docs/DECISIONS.md`.

### Validation automatisée

- Tests ciblés Community Pause, ViewModel, agrégateur et intégrité : 11/11 réussis.
- Debug : 0 avertissement, 0 erreur ; 160/160 tests réussis.
- Release : 0 avertissement, 0 erreur ; 160/160 tests réussis.
- Test d’intégrité : taille, date et SHA-256 des deux sources inchangés après lecture.
- Tests de régression : fichier absent, `.tmp`/`.bak` ignorés, feedback périmé, réécriture invalide avec cache stale, ligne partielle, événement inconnu, rotation atomique, changement de session et absence de XUID dans les détails.
- Aucun serveur BOIII lancé ou contacté, aucune commande RCON envoyée, aucun secret lu, aucune écriture PinteMod, aucune modification GSC et aucune modification GitHub.

### Paquet portable

- Archive : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-2-win-x64.zip`.
- SHA-256 actuel après durcissement hors ligne : `0FB3C2BF223BB9FCDE24EF53180C32D9B1558366AB42FB9B0342C1E59D2C1148`.
- 466 entrées, 66,77 Mio, exécutable et guide présents.
- 0 chemin dangereux ; 0 PDB, configuration opérateur, secret DPAPI, log runtime, manifeste ou heartbeat embarqué.

### Validation humaine restante

- Aucune capture n’a été produite automatiquement pendant cette passe.
- Lorsque le serveur sera vide : exécuter `ezzhealth full`, puis `ezzpausestatus`, vérifier la carte **Pause communautaire** et l’événement `STATUS` dans la Live Console.
- Après ce retour seulement, ajouter `ezzpauseforce` et `ezzresume` derrière confirmation explicite. Le bouton **PAUSE AFFICHAGE** de Logs reste distinct et purement local.

### Préparation supplémentaire sans serveur

- Audit complet des déclarations `addcommand` du commit GitHub stable, sans exécution de GSC.
- Création de `docs/GITHUB_SERVER_COMMAND_CATALOG.md` avec les syntaxes vérifiées, le ciblage futur par BOIII_XUID, les niveaux de risque et l’ordre d’activation recommandé.
- Ajout des boutons visibles **Mettre en pause** et **Reprendre** dans Serveur. Ils ne possèdent aucune commande d’envoi et restent désactivés via `RealPauseControlsAvailable = false`.
- Ajout du filtre dédié `PAUSE` dans la Live Console et affichage explicite de la provenance `logs/pause.log`.
- Validation ciblée WPF/Serveur/Logs : 5/5 tests réussis.
- Le paquet Preview 2 a été republié avec ces ajouts, puis contrôlé à nouveau.
- Aucun nouveau texte de commande gameplay n’a été ajouté au service RCON : sa liste blanche reste exactement `ezzhealth full` et `ezzpausestatus`.
- La pause d’affichage de la Live Console existe déjà et n’agit jamais sur le serveur. `ezzpausestatus` reste uniquement un diagnostic d’état.

## 2026-08-09 — Validation sémantique RCON et garde-fous hors ligne

### Objectif

Continuer la préparation des futurs boutons sans contacter le serveur occupé et empêcher qu’une réponse RCON quelconque soit présentée comme une réussite.

### Réalisé

- Vérification en lecture seule du dépôt GitHub stable au commit `7d5f33489d8635c460d3eb63bb04226c7aa3f326` ; la branche observée n’a pas changé.
- Extraction des marqueurs stables réellement imprimés par `ezzhealth full` et `ezzpausestatus`.
- Ajout du statut `UnexpectedResponse` : une réponse non vide, tronquée, issue d’une commande inconnue ou ne correspondant pas au diagnostic n’est plus verte.
- Présentation de cet état en avertissement `RÉPONSE NON RECONNUE`, avec conservation de `Commande envoyée : Oui` lorsque le paquet a réellement été envoyé.
- Neutralisation inchangée de toute réponse avant l’interface et l’audit mémoire.
- Liste blanche RCON inchangée : seulement `ezzhealth full` et `ezzpausestatus`.
- Renforcement du service simulé : action non déclarée ou option contenant des caractères de contrôle refusée ; `CommandSent` reste toujours faux.
- Audit inventaire : l’état `pintemod_revive_*` est uniquement un cache GSC interne et non persistant de réanimation. Aucun snapshot local général d’inventaire joueur n’existe dans les sources auditées.

### Fichiers créés ou modifiés

- Créé : `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/RconDiagnosticResponseValidator.cs`.
- Modifiés : `app/src/PinteMod.ControlCenter.Core/Models/RconModels.cs`.
- Modifiés : `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/RconDiagnosticService.cs`, `InMemoryOperatorActivityStore.cs` et `Simulation/SimulationActionService.cs`.
- Modifié : `app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs`.
- Modifiés : `app/tests/PinteMod.ControlCenter.Tests/RconDiagnosticServiceTests.cs`, `SettingsOperatorViewModelTests.cs` et `SimulationActionServiceTests.cs`.
- Modifiés : `app/README.md`, `docs/GITHUB_SERVER_COMMAND_CATALOG.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md` et `docs/DECISIONS.md`.

### Validation finale

- Tests ciblés diagnostics, Paramètres et simulation : 19/19 réussis en Debug.
- Debug : 0 avertissement, 0 erreur ; 166/166 tests réussis.
- Release : 0 avertissement, 0 erreur ; 166/166 tests réussis.
- Paquet republié : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-2-win-x64.zip`.
- SHA-256 : `0FB3C2BF223BB9FCDE24EF53180C32D9B1558366AB42FB9B0342C1E59D2C1148`.
- Contrôle ZIP : 466 entrées, 66,77 Mio, 0 chemin dangereux et 0 secret, configuration, donnée serveur, log runtime ou PDB.
- Contrôle statique : les seuls textes de commande du service RCON sont toujours `ezzhealth full` et `ezzpausestatus`.
- Aucun serveur BOIII lancé ou contacté, aucune commande RCON envoyée, aucun secret lu et aucun fichier PinteMod/GSC/GitHub modifié.

### Validation humaine restante

- Lorsque le serveur sera vide : exécuter manuellement **Health complet**, puis **État de la pause** depuis Paramètres.
- Ce retour reste le seul verrou avant toute activation d’une première commande de partie ; les boutons Pause/Reprendre restent désactivés jusque-là.

### Retour terrain — réponse BOIII sans texte

- Premier essai réel effectué depuis le poste opérateur vers une adresse LAN et un port explicitement configurés, serveur vide et déjà lancé.
- Le Control Center a reçu un datagramme sans corps : `CommandSent = true`, statut historique `EmptyResponse`.
- Comparaison avec `PinteMod_Remote_RCON.ps1` au commit audité : ce comportement est explicitement traité comme « commande envoyée ; BOIII n’a retourné aucun texte ».
- Correction UI : statut `ENVOYÉ · SANS TEXTE` et consigne de vérifier la console du serveur, sans faux succès vert.
- Deux tests de régression ajoutés pour le service et le ViewModel Paramètres.
- La validation Health a ensuite été confirmée par la sortie console complète fournie par l’opérateur.

## 2026-08-09 — Diagnostics terrain validés et première commande réelle préparée

### Preuves humaines reçues

- `ezzhealth full` exécuté dans BOIII : `PASS=51 | WARNING=0 | ERROR=0`, zéro warning déclaré et tous les modules/services attendus connectés.
- `ezzpausestatus` exécuté : Community Pause EXPERIMENTAL v0.3, `Active: 0`, `Successful pauses: 0/2`, protections temporaires désactivées.
- La sortie confirme aussi le chargement de Community Pause v0.3, Operator Bridge v0.1 et Player State v0.1.
- L’erreur de rawfile et le hitch warning présents avant les diagnostics appartiennent au démarrage BOIII ; ils ne sont pas produits par le Control Center.

### Réalisé

- Ajout d’un service distinct limité à `ezzpauseforce` et `ezzresume` ; aucune chaîne de commande libre.
- Après chaque mutation demandée, envoi unique de `ezzpausestatus` afin de produire le feedback read-only autoritaire.
- Aucun retry automatique : timeout ou effet non observé impose une vérification humaine.
- Boutons réels activables uniquement avec source Community Pause live fraîche, endpoint explicite, secret DPAPI et confirmation Oui/Non.
- Pause indisponible si la partie est déjà pausée ou si un vote est actif ; Reprendre disponible uniquement pendant une pause observée.
- Retour visible séparant commande envoyée, état en cours, confirmation locale, résultat incertain et échec.
- Audit de l’action ajouté uniquement en mémoire dans la Live Console.
- Toutes les autres actions joueur/serveur restent simulées via `ISimulationActionService` et `CommandSent = false`.

### Fichiers créés ou modifiés

- Créés : `CommunityPauseCommandModels.cs`, `ICommunityPauseCommandService.cs`, `IOperatorConfirmationService.cs`.
- Créés : `CommunityPauseCommandService.cs`, `MessageBoxOperatorConfirmationService.cs`.
- Créés : `CommunityPauseCommandServiceTests.cs`, `ServerPauseCommandViewModelTests.cs`.
- Modifiés : `IOperatorActivityStore.cs`, `InMemoryOperatorActivityStore.cs`, `SettingsViewModel.cs`, `ServerViewModel.cs`, `ServerView.xaml`, `App.xaml.cs` et `ViewModelTests.cs`.
- Modifiés : `app/README.md`, `app/packaging/LISEZ-MOI.txt`, `docs/MVP_OPERATOR_VALIDATION_FR.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md` et `docs/DECISIONS.md`.

### Validation finale automatisée

- Tests ciblés RCON, confirmation, source fraîche et ViewModels : 24/24 réussis en Debug.
- Debug : 0 avertissement, 0 erreur ; 176/176 tests réussis.
- Release : 0 avertissement, 0 erreur ; 176/176 tests réussis.
- Paquet autonome : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-3-win-x64.zip`.
- SHA-256 : `7385B01E450581914653D7A490E0F2D72A56662EEADA15C4216FA4D72E425CBC`.
- Contrôle ZIP : 466 entrées, 66,78 Mio, aucun chemin dangereux, PDB, secret, configuration, log runtime ou donnée serveur.
- L’ancienne Preview 2 étant ouverte par l’opérateur, elle n’a pas été arrêtée ni écrasée ; Preview 3 a été publiée dans un dossier séparé.
- Aucun nouvel envoi au serveur n’a été effectué par Codex ; l’unique validation réelle provient des actions explicites de l’opérateur.

### Validation humaine restante

- Exécuter le prochain paquet avec une source live du portable, rejoindre seul la partie en restant vivant, puis tester Pause et Reprendre.
- Fournir une capture du panneau Serveur montrant `CONFIRMÉ PAR LE STATUT LOCAL` pour les deux actions.

## 2026-08-09 — Source LAN directe PinteModData

### Objectif

Permettre au Control Center de rester sur le PC fixe tout en observant le runtime réellement mis à jour sur le portable, sans partager toute l’installation serveur.

### Réalisé

- Identification du partage officiel recommandé : le dossier `boiii/scriptdata/pintemod`, exposé en lecture seule sous `PinteModData`.
- Correction du mode LAN : un chemin tel que `\\portable\\PinteModData` est désormais interprété directement comme racine de données.
- Le mode Local continue d’attendre la racine complète `UnrankedServer` ; les arguments historiques restent inchangés.
- Adaptation commune des cinq lecteurs Phase 2.1, des chemins Ranks/Records, Easter Eggs et du Bloc A.
- Confinement, interdiction des liens/jonctions et exclusions `.tmp`/`.bak` conservés.
- Aucune découverte réseau, aucun nouveau port et aucune écriture sur le partage.
- L’aide Paramètres recommande maintenant `\\portable\\PinteModData` plutôt qu’un partage du serveur complet.

### Fichiers modifiés ou créés

- Modifiés : `LocalPinteModOptions.cs`, `BlockALocalPathPolicy.cs`, `RankRecordsPathPolicy.cs`, `EasterEggRecordsPathPolicy.cs` et `LocalDataSourceProbe.cs`.
- Modifiés : `App.xaml.cs` et `SettingsViewModel.cs`.
- Créé : `PinteModDataRootLayoutTests.cs`.
- Modifiés : `app/README.md`, `app/packaging/LISEZ-MOI.txt`, `docs/MVP_OPERATOR_VALIDATION_FR.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md` et `docs/DECISIONS.md`.

### Validation finale automatisée

- Tests ciblés des lecteurs, confinement et racine directe : 37/37 réussis.
- Debug : 0 avertissement, 0 erreur ; 177/177 tests réussis.
- Release : 0 avertissement, 0 erreur ; 177/177 tests réussis.
- Paquet autonome : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-4-win-x64.zip`.
- Exécutable : `app/artifacts/mvp-preview-4-win-x64/PinteMod.ControlCenter.exe`.
- SHA-256 : `57264098C13A3E3FB315031D12349D4C2ED50023813A6C685923637FA9CAF7F2`.
- Contrôle ZIP : 466 entrées, 70 021 130 octets, aucun chemin dangereux, PDB, secret, configuration, log runtime ou donnée serveur.
- Aucun accès au partage réel, envoi RCON, lancement de processus serveur ou écriture PinteMod n’a été effectué automatiquement.

### Validation humaine restante

- Depuis le PC fixe, ouvrir le partage read-only `PinteModData` du portable, le tester en mode LAN dans Paramètres puis enregistrer la configuration.
- Avec l’opérateur seul, vivant et non à terre dans une partie, valider Pause puis Reprendre et vérifier le retour `CONFIRMÉ PAR LE STATUT LOCAL`.
- Capture recommandée : panneau Serveur après confirmation de Pause et de Reprendre.

### Validation LAN depuis le PC fixe

- Le partage UNC du portable a été testé strictement en lecture seule depuis le PC fixe : accessible.
- Présence confirmée sans lecture du contenu : `logs/current_session.json`, les quatre heartbeats, `ranks_v2` et `remote`.
- Aucun fichier distant n’a été créé, modifié ou supprimé ; aucune commande RCON ou serveur n’a été lancée.
- La capture opérateur a ensuite confirmé que le refus persistait avec `LAN` sélectionné ; l’analyse a identifié la garde de racine de volume UNC comme cause réelle, corrigée dans Preview 5 ci-dessous.

## 2026-08-09 — Correction de la racine UNC et Preview 5

### Objectif

Corriger le refus de `\\serveur\PinteModData` observé dans Preview 4 alors que le partage est accessible depuis le poste opérateur.

### Réalisé

- Cause identifiée : `Path.GetPathRoot` considère la racine d’un partage UNC comme la racine d’un volume ; la garde générale la refusait avant toute lecture.
- Une racine UNC est maintenant acceptée uniquement pour le layout LAN direct `PinteModDataRoot`.
- Les racines de lecteur local et l’utilisation d’une racine UNC comme `ServerRoot` restent refusées.
- Aucun changement des lecteurs, du RCON, des commandes gameplay ou des garanties read-only.
- Aucun identifiant ou mot de passe Windows n’est lu, utilisé, stocké ou ajouté aux livrables.

### Fichiers créés ou modifiés

- Modifiés : `LocalPinteModOptions.cs`, `PinteMod.ControlCenter.Infrastructure.csproj` et `PinteModDataRootLayoutTests.cs`.
- Modifiés : `app/README.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md` et `docs/DECISIONS.md`.
- Créés : `app/artifacts/mvp-preview-5-win-x64/`, le ZIP Preview 5 et son fichier SHA-256.

### Validation

- Tests ciblés LAN et sonde : 6/6 réussis.
- Debug : 0 avertissement, 0 erreur ; 178/178 tests réussis.
- Release : 0 avertissement, 0 erreur ; 178/178 tests réussis.
- Paquet : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-5-win-x64.zip`.
- Exécutable : `app/artifacts/mvp-preview-5-win-x64/PinteMod.ControlCenter.exe`.
- SHA-256 : `A0929F4BA26488D4869F17E64152418521122479CE6842EEDFF908E05A68A59A`.
- Contrôle ZIP : 466 entrées, aucun chemin dangereux, PDB, secret, configuration, log runtime ou donnée serveur.

### Validation humaine restante

- Fermer Preview 4, ouvrir Preview 5, sélectionner `LAN`, conserver le partage UNC déjà accessible et cliquer sur `TESTER LA SOURCE`.
- Enregistrer la source au prochain démarrage, relancer uniquement le Control Center et vérifier le passage en mode hybride.
- Changer séparément tout mot de passe Windows communiqué par inadvertance avant l’utilisation définitive ; ne jamais le transmettre au Control Center ou dans les documents du projet.

### Retour opérateur

- Preview 5 accepte désormais le partage LAN read-only du portable : validation humaine réussie.
- Le compte Windows dédié au partage est limité à la lecture et n’est utilisé ni stocké par le Control Center.
- Prochaine validation : confirmer le mode hybride après redémarrage, puis tester Pause/Reprendre avec l’opérateur seul en jeu.

## 2026-08-09 — Format Community Pause réel et Preview 6

### Objectif

Déverrouiller correctement les commandes Community Pause lorsque le serveur réel renvoie un statut local frais au format numérique.

### Diagnostic et correction

- Le fichier partagé `remote/feedback.latest.txt` était présent, complet et actualisé au moment de la commande `ezzpausestatus`.
- Le serveur réel écrit `Active: 0` ou `Active: 1` ; le lecteur Preview 5 n’acceptait que `false` ou `true`.
- Le parseur accepte maintenant strictement les quatre représentations booléennes `0`, `1`, `false` et `true`.
- Aucun changement du transport RCON, de la liste blanche, des confirmations ou des autres actions simulées.
- Aucun fichier distant n’a été modifié et aucune commande supplémentaire n’a été envoyée pendant le diagnostic.

### Fichiers créés ou modifiés

- Modifiés : `CommunityPauseStatusReader.cs` et `CommunityPauseStatusReaderTests.cs`.
- Modifiés : `app/README.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md` et `docs/DECISIONS.md`.
- Créés : `app/artifacts/mvp-preview-6-win-x64/`, le ZIP Preview 6 et son fichier SHA-256.

### Validation

- Tests ciblés Community Pause : 6/6 réussis.
- Debug : 0 avertissement, 0 erreur ; 180/180 tests réussis.
- Release : 0 avertissement, 0 erreur ; 180/180 tests réussis.
- Paquet : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-6-win-x64.zip`.
- Exécutable : `app/artifacts/mvp-preview-6-win-x64/PinteMod.ControlCenter.exe`.
- SHA-256 : `32936B110C08126A49A8D220DA01D4D6B019E42C02D12C7F5BFCDF5AA4472386`.
- Contrôle ZIP : 466 entrées, aucun chemin dangereux, PDB, secret, configuration, log runtime ou donnée serveur.

### Validation humaine restante

- Fermer Preview 5 puis ouvrir Preview 6 ; la configuration LAN et le secret DPAPI local restent réutilisés.
- Lancer `VÉRIFIER L’ÉTAT DE LA PAUSE`, puis confirmer que le panneau Serveur affiche un statut frais et active `METTRE EN PAUSE`.
- Avec l’opérateur seul et vivant en jeu, tester Pause puis Reprendre et vérifier `CONFIRMÉ PAR LE STATUT LOCAL`.

### Retour terrain Preview 6

- La mise en pause réelle a fonctionné après renouvellement manuel du statut avec un joueur connecté.
- Le retour local a confirmé `Active: 1`, une reprise automatique à 180 secondes, le God Mode temporaire, la protection spectateur et le blocage des nouveaux spawns IA.
- Reprendre est ensuite redevenu indisponible parce que le statut ponctuel avait dépassé la fenêtre fraîche de 15 secondes ; aucun échec de transport ni de commande Pause n’a été observé.

## 2026-08-09 — Actualisation Pause intégrée et Preview 7

### Objectif

Permettre à l’opérateur de renouveler explicitement le statut Community Pause depuis le panneau Serveur avant Pause ou Reprendre, sans polling RCON automatique.

### Réalisé

- Ajout du bouton `ACTUALISER LE STATUT` à côté de Pause/Reprendre.
- Le bouton réutilise exclusivement `IRconDiagnosticService` avec `RconDiagnosticCommand.PauseStatus` ; aucun texte libre ni nouvelle commande n’est accepté.
- Après l’envoi explicite, le ViewModel attend un fichier local plus récent, réussi et frais avant d’activer l’action correspondant à l’état observé.
- Une réponse UDP sans texte reste acceptable uniquement comme transport envoyé ; le succès UI exige toujours la preuve locale.
- Les mutations Pause/Reprendre, leurs confirmations et toutes les autres actions simulées restent inchangées.

### Fichiers créés ou modifiés

- Modifiés : `ServerViewModel.cs`, `ServerView.xaml`, `App.xaml.cs` et `ServerPauseCommandViewModelTests.cs`.
- Modifiés : `app/README.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md` et `docs/DECISIONS.md`.
- Créés : `app/artifacts/mvp-preview-7-win-x64/`, le ZIP Preview 7 et son fichier SHA-256.

### Validation

- Tests ciblés Community Pause et ViewModel Serveur : 11/11 réussis.
- Debug : 0 avertissement, 0 erreur ; 181/181 tests réussis.
- Release : 0 avertissement, 0 erreur ; 181/181 tests réussis.
- Paquet : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-7-win-x64.zip`.
- Exécutable : `app/artifacts/mvp-preview-7-win-x64/PinteMod.ControlCenter.exe`.
- SHA-256 : `1AAC3148869AE01221451969C919D1992F3E2E20F7E6A438A72ADAB1194EF567`.
- Contrôle ZIP : 466 entrées, aucun chemin dangereux, PDB, secret, configuration, log runtime ou donnée serveur.

### Validation humaine restante

- Fermer Preview 6 et ouvrir Preview 7 ; le serveur peut rester lancé.
- Dans Serveur, cliquer sur `ACTUALISER LE STATUT`, attendre `STATUT ACTUALISÉ`, puis cliquer sur Reprendre si la partie est encore pausée.
- Vérifier `CONFIRMÉ PAR LE STATUT LOCAL`. Si la reprise automatique a déjà eu lieu, le statut doit indiquer non pausée et aucune commande Reprendre n’est nécessaire.

### Validation humaine finale

- Preview 7 validée par l’opérateur sur le serveur réel.
- Mise en pause réelle : réussie et confirmée par le statut local frais.
- Actualisation manuelle du statut depuis le panneau Serveur : réussie.
- Reprise réelle : réussie et confirmée.
- Le parcours Community Pause/Reprendre est clôturé sans blocage connu.
- Toutes les autres actions joueur et serveur restent simulées jusqu’à décision explicite sur le prochain périmètre.

## 2026-08-09 — Paquet ChatGPT de revue globale Bloc B

### Objectif

Préparer une revue externe unique de la fondation des commandes locales sécurisées après validation terrain de Pause/Reprendre, avant toute extension de la liste blanche réelle.

### Réalisé

- Création d’un paquet autonome de revue contenant les sources Core/Infrastructure/WPF/Tests, les contrats, les décisions, l’audit, le catalogue des commandes et les preuves de validation.
- Ajout d’un prompt demandant explicitement les blocages de sécurité, de concurrence, de confidentialité, de restriction LAN et de confirmation locale.
- Remplacement de l’adresse LAN terrain utilisée comme exemple dans le test UNC par le nom fictif `serveur-test`.
- Inclusion de deux captures antérieures aux corrections, clairement identifiées ; la validation finale Pause/Reprendre reste consignée comme confirmation humaine sans capture inventée.
- Aucun secret, compte Windows, endpoint terrain, fichier runtime, log serveur, configuration opérateur, exécutable ou symbole de débogage inclus.

### Fichiers créés ou modifiés

- Modifié : `app/tests/PinteMod.ControlCenter.Tests/PinteModDataRootLayoutTests.cs` pour anonymiser l’exemple UNC.
- Créé : `app/artifacts/chatgpt-bloc-b-foundation-review/`.
- Créés dans ce dossier : `00_LIRE_EN_PREMIER.md`, `PROMPT_CHATGPT_BLOC_B_FONDATION.md`, preuves, contexte, contrats, sources, tests et `MANIFESTE_SHA256.txt`.
- Créés : `app/artifacts/PinteMod-ControlCenter-ChatGPT-Bloc-B-Foundation-Review.zip` et son fichier SHA-256.
- Modifiés : `docs/CODEX_PROGRESS.md` et `docs/TODO.md`.

### Validation

- Sources anonymisées : Debug 0 avertissement, 0 erreur, 181/181 tests réussis.
- Sources anonymisées : Release 0 avertissement, 0 erreur, 181/181 tests réussis.
- Manifeste interne : 173/173 empreintes conformes avant compression.
- ZIP : 176 entrées, 340 784 octets, aucun chemin dangereux ni fichier interdit.
- SHA-256 : `35A3FDCD2DB79340795B982C1A196E2BC04713CCEAC869BE45B422D3005B01BA`.

### Validation humaine restante

- Transmettre le ZIP à ChatGPT et copier le contenu de `PROMPT_CHATGPT_BLOC_B_FONDATION.md`.
- Rapporter le verdict complet à Codex ; aucune nouvelle commande réelle ne doit être activée avant traitement des éventuels blocages.

## 2026-08-09 — Corrections globales de la revue fondation Bloc B

### Objectif

Traiter en une seule livraison les cinq blocages de sécurité et de fiabilité relevés par la revue ChatGPT, sans ajouter de commande ni élargir le périmètre fonctionnel.

### Réalisé

- Confinement RCON centralisé : seules les IP numériques de boucle locale, privées ou link-local sont acceptées ; IP publiques, noms d’hôte et adresses non spécifiées sont refusés.
- Application de la même règle dans Paramètres, la persistance locale, les deux services RCON et le client UDP.
- Verrou de transport partagé entre diagnostics et Community Pause, complété par un coordinateur unique des opérations UI Serveur/Paramètres.
- Relecture du snapshot après confirmation humaine ; une mutation est annulée si la fraîcheur, le vote ou l’état ont changé.
- Invalidation immédiate de l’autorisation après tout envoi potentiellement effectué ; un ancien snapshot ne peut plus réactiver Pause/Reprendre.
- Déverrouillage uniquement après un statut local réussi, frais et strictement plus récent.
- Fermeture ordonnée : refus des nouvelles opérations, attente du moniteur et des opérations RCON/ViewModel, puis destruction des lecteurs.
- Aucune nouvelle commande : liste blanche inchangée à `ezzhealth full`, `ezzpausestatus`, `ezzpauseforce` et `ezzresume`.

### Fichiers créés

- `app/src/PinteMod.ControlCenter.Core/Contracts/IRconOperationGate.cs`
- `app/src/PinteMod.ControlCenter.Core/Security/RconEndpointValidator.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/RconOperationGate.cs`
- `app/src/PinteMod.ControlCenter/State/OperatorRconOperationCoordinator.cs`
- `app/tests/PinteMod.ControlCenter.Tests/RconEndpointValidatorTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/OperatorRconOperationCoordinatorTests.cs`

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter.Infrastructure/Local/JsonOperatorConfigurationStore.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/BoiiiUdpRconClient.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/RconDiagnosticService.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/CommunityPauseCommandService.cs`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/SettingsView.xaml`
- `app/tests/PinteMod.ControlCenter.Tests/BoiiiUdpRconClientTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/CommunityPauseCommandServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/OperatorConfigurationStoreTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/RconDiagnosticServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ServerPauseCommandViewModelTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/SettingsOperatorViewModelTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`
- `app/README.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md` et `docs/DECISIONS.md`

### Compilations et tests

- Debug : succès, 0 avertissement, 0 erreur ; 215/215 tests réussis, 0 échec, 0 ignoré.
- Release : succès, 0 avertissement, 0 erreur ; 215/215 tests réussis, 0 échec, 0 ignoré.
- Le test DPAPI exige le profil Windows courant ; son premier passage dans le bac à sable sans profil a échoué pour cette seule raison, puis les suites finales ont réussi avec le profil utilisateur chargé.
- Tests ciblés de la passe : 63/63 réussis avant la validation globale.

### Garanties et problèmes

- Aucun serveur BOIII/BAT/EXE lancé et aucune connexion au serveur réel pendant cette passe.
- Aucune lecture de secret existant, aucune modification PinteMod/GSC, aucun port entrant, serveur web ou découverte réseau.
- `ISimulationActionService` et `CommandSent = false` restent inchangés pour toutes les autres actions.
- Aucun blocage technique connu après la validation automatisée.

### Validation humaine restante

- Faire relire le paquet de corrections par ChatGPT et obtenir le verdict de clôture de la fondation Bloc B.
- Aucun nouvel essai terrain Pause/Reprendre n’est requis par cette passe sauf demande explicite de la revue finale.

### Captures

- Aucune nouvelle capture nécessaire : les corrections concernent le confinement, la concurrence, l’autorisation et l’arrêt, sans modification de mise en page.

### Paquet de contre-revue

- Dossier : `app/artifacts/chatgpt-bloc-b-foundation-corrections-review/`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-ChatGPT-Bloc-B-Foundation-Corrections-Review.zip`.
- SHA-256 : `088106F9FCA8C8B02F1354F4BEF9B15A5CC0447C8E45890C65A2320833F75686`.
- Contrôle : 177 entrées ZIP, 0 chemin dangereux, 0 binaire/symbole/log/configuration/secret interdit.
- Manifeste interne : 176/176 empreintes vérifiées avant compression.

## 2026-08-09 — Dernier verrou UDP de la fondation Bloc B

### Objectif

Corriger le seul blocage restant : une mutation UDP pouvait avoir été émise alors que le service retournait encore `CommandSent = false` si la réception levait une erreur après l’envoi.

### Réalisé

- La commande fermée est validée avant toute tentative de transport.
- Dès que le premier appel de transport de mutation commence, la livraison est considérée comme potentielle.
- Une `SocketException`, une erreur d’I/O ou une erreur d’argument pendant cet appel retourne donc un résultat incertain avec `CommandSent = true`.
- Le ViewModel applique alors le verrou existant et refuse Pause comme Reprendre jusqu’à un statut local strictement plus récent et frais.
- Un filet de sécurité ViewModel transforme aussi toute exception non normalisée après le début de l’opération en résultat incertain verrouillé.
- Aucun retry, nouvelle commande, modification GSC ou élargissement fonctionnel.

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/CommunityPauseCommandService.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`
- `app/tests/PinteMod.ControlCenter.Tests/CommunityPauseCommandServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ServerPauseCommandViewModelTests.cs`
- `app/README.md`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Validation

- Tests ciblés Community Pause/service/ViewModel : 17/17 réussis.
- Debug : succès, 0 avertissement, 0 erreur ; 218/218 tests réussis.
- Release : succès, 0 avertissement, 0 erreur ; 218/218 tests réussis.
- Aucun serveur BOIII/BAT/EXE lancé, aucune connexion au serveur réel et aucune commande RCON terrain pendant cette passe.

### Validation humaine restante

- Faire relire le paquet final par ChatGPT pour clôturer la fondation Bloc B.
- Aucun nouvel essai serveur n’est requis pour ce correctif de classification et de verrouillage.

### Captures

- Aucune : le correctif ne modifie pas la présentation.

### Paquet final de clôture

- Dossier : `app/artifacts/chatgpt-bloc-b-final-correction-review/`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-ChatGPT-Bloc-B-Final-Correction-Review.zip`.
- SHA-256 : `367BF20BDD03774DD46658E3F9DEA7DE784E2F62C983506EFE254829EA7D0D31`.
- Manifeste : 175/175 empreintes vérifiées ; ZIP de 176 entrées, sans chemin dangereux ni fichier interdit.

## 2026-08-09 — Clôture définitive de la fondation Bloc B

### Verdict humain

- Verdict ChatGPT : `FONDATION BLOC B VALIDÉE — aucune correction bloquante`.
- Le dernier défaut de classification UDP est confirmé corrigé du service jusqu’au ViewModel.
- Le verrou anti-répétition reste actif pour `DeliveryUnknown`, absence de confirmation, `TransportError` potentiellement envoyé et exception non normalisée.
- Le déverrouillage exige toujours une source locale réussie, fraîche et strictement plus récente.
- La liste blanche reste limitée à `ezzhealth full`, `ezzpausestatus`, `ezzpauseforce` et `ezzresume`.
- Toutes les autres actions restent simulées avec `CommandSent = false`.

### État validé

- Debug : 0 avertissement, 0 erreur, 218/218 tests réussis.
- Release : 0 avertissement, 0 erreur, 218/218 tests réussis.
- Aucun blocage connu dans la fondation Bloc B.
- Aucun nouveau test serveur réel requis pour cette clôture.

### Fichiers modifiés pour la clôture

- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`

### Suite autorisable

- La prochaine étape produit est le Bloc C — administration complète.
- Toute nouvelle commande réelle devra faire l’objet d’un périmètre explicite, d’une liste blanche typée, d’une analyse de risque et d’une validation humaine adaptée avant activation.

## 2026-08-09 — Bloc C, diagnostics serveur read-only et Preview 8

### Objectif de la passe

Commencer le Bloc C par le plus grand lot sûr ne nécessitant aucune mutation supplémentaire : diagnostics manuels de carte, courant, Pack-a-Punch, manche et joueurs, avec retour lisible et neutralisé.

### Réalisé

- Audit du catalogue stable PinteMod et vérification des signatures des commandes `ezzmap`, `ezzpowerstatus`, `ezzpapstatus`, `ezzround` et `ezzplayers`.
- Ajout de cinq valeurs typées au contrat de diagnostic et de cinq correspondances textuelles fermées dans l’infrastructure.
- Validation spécifique des marqueurs de réponse pour chaque commande ; aucune réception UDP arbitraire ne devient automatiquement un succès vert.
- Filtrage des XUID, IP, GUID et chemins avant exposition du retour aux ViewModels.
- Ajout des cinq boutons dans Paramètres et dans le panneau Diagnostics de Serveur.
- Affichage uniforme du statut, de `Commande envoyée : Oui/Non` et du texte neutralisé.
- Réutilisation du coordinateur RCON partagé : diagnostic, confirmation Pause/Reprendre et mutation restent sérialisés.
- Aucun diagnostic automatique, aucune nouvelle mutation, aucun texte de commande libre et aucun retry.
- Les actions joueur restent simulées : `ezzplayers` ne fournit pas de BOIII_XUID exploitable et ne peut pas autoriser un ciblage réel.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter.Core/Models/RconModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/RconDiagnosticResponseValidator.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/RconDiagnosticService.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/SettingsView.xaml`
- `app/src/PinteMod.ControlCenter/Views/ServerView.xaml`
- `app/tests/PinteMod.ControlCenter.Tests/RconDiagnosticServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/SettingsOperatorViewModelTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ServerPauseCommandViewModelTests.cs`
- `app/README.md`
- `app/packaging/LISEZ-MOI.txt`
- `docs/GITHUB_SERVER_COMMAND_CATALOG.md`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`
- `app/artifacts/mvp-preview-8-win-x64/`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-8-win-x64.zip`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-8-win-x64.zip.sha256`

### Fonctionnalités disponibles

- Paramètres : sept diagnostics RCON manuels au total.
- Serveur : cinq diagnostics Bloc C read-only directement accessibles à côté du rapport local.
- Carte, courant, Pack-a-Punch, manche et liste informative des joueurs peuvent être interrogés sans modifier la partie.
- Pause/Reprendre restent les seules mutations réelles.

### Compilation et tests

- Tests ciblés diagnostics/Paramètres/Serveur/WPF : 36/36 réussis.
- Debug : compilation réussie, 0 avertissement, 0 erreur ; 223/223 tests réussis.
- Release : compilation réussie, 0 avertissement, 0 erreur ; 223/223 tests réussis.
- Le premier passage isolé a refusé l’accès DPAPI du profil Windows ; la suite complète a été relancée dans le contexte utilisateur normal et a réussi intégralement.

### Problèmes rencontrés

- Le poste exposait uniquement le runtime .NET. Un SDK .NET 8.0.423 officiel a été installé dans un dossier temporaire, hors workspace, pour compiler et tester.
- La première publication contenait trois PDB générés ; ils ont été retirés avant la compression finale.
- Aucun serveur BOIII, BAT ou EXE serveur n’a été lancé et aucune commande RCON réelle n’a été envoyée pendant le développement.

### Validation humaine nécessaire

- Avec Preview 8 et le serveur déjà lancé, cliquer une fois sur Carte, Courant, Pack-a-Punch, Manche et Joueurs.
- Vérifier que chaque résultat est lisible, cohérent avec la console BOIII et n’affiche aucune donnée sensible complète.
- Cette validation est le point de contrôle requis avant d’autoriser la conception des prochaines mutations serveur.

### Captures

- Aucune capture automatique produite : aucune application ni serveur réel n’a été lancé pendant cette passe.
- Une capture humaine du panneau Diagnostics Serveur après les cinq essais est demandée.

### Paquet autonome

- Dossier : `app/artifacts/mvp-preview-8-win-x64/`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-8-win-x64.zip`.
- SHA-256 : `85054F58113080895CCF610A4DC1C8E5CAE552EAE2150AFD66E39BDCA31CB737`.
- Contrôle : 466 entrées, 0 chemin dangereux, 0 fichier interdit, 0 PDB ; exécutable et LISEZ-MOI présents.

## 2026-08-09 — État courant après publication Preview 12

- La livraison courante est la Preview 12 décrite dans l’entrée « Catalogue hybride, historique local, power-ups et Preview 12 » ; elle remplace les Preview 10 et 11 pour les prochains essais.
- Debug et Release : 0 avertissement, 0 erreur, 281/281 tests réussis dans chaque configuration.
- Exécutable : `app/artifacts/mvp-preview-12-win-x64/PinteMod.ControlCenter.exe`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-12-win-x64.zip`.
- SHA-256 : `E7A72DEB20F05EB3D4DA66362DB76C2CAAD0C60163E62F1A7A86733F03655813`.
- Aucun retour ChatGPT n’est nécessaire maintenant ; la prochaine intervention utile est une vérification visuelle locale, puis une validation terrain finale regroupée lorsque l’opérateur le souhaite.

## 2026-08-09 — Catalogue hybride, historique local, power-ups et Preview 12

### Objectif de la passe

Compléter les usages opérateur sûrs sans dépendre d’une ancienne rotation de serveur : gérer les cartes officielles et custom localement, exploiter les derniers contrats read-only stables, rendre le power-up joueur fonctionnel et remplacer le bouton Historique simulé par une consultation locale neutralisée.

### Réalisé

- Catalogue partagé entre Paramètres et Serveur avec quatre provenances : 14 cartes officielles, rotation collée explicitement, cartes manuelles locales et carte courante observée.
- Import strict d’une seule ligne active `set sv_maprotation`, avec remplacement de l’ancienne rotation, déduplication, limites de taille et refus des commentaires, commandes étrangères, plusieurs lignes et codes dangereux.
- Persistance atomique uniquement sous `%LOCALAPPDATA%\PinteMod\ControlCenter\map-catalog.json` ; aucune lecture automatique de `server_zm.cfg` et aucune écriture serveur.
- Ajout/suppression locale de cartes custom ; une suppression ne modifie jamais la rotation réelle.
- Diagnostics manuels read-only ajoutés : Audit carte (`ezzmapaudit full`), Événements (`ezzeventstatus`) et Power-ups (`ezzpowerups`), avec signatures de réponse fermées.
- Action Power-up joueur ajoutée avec neuf alias canoniques, ciblage exclusif par BOIII_XUID, revalidation après confirmation, zéro retry et verrou transversal.
- Historique de modération local chargé à la demande pour le joueur sélectionné : compteurs et dernier motif neutralisés uniquement ; XUID complet et chemin réel absents des objets d’affichage.
- Lecteur d’historique confiné à `moderation/history`, limité à 64 Kio, tolérant aux fichiers absents/partiels et ignorant `.tmp`/`.bak`.
- Changement/redémarrage de carte et mutations génériques boss/événements maintenus en simulation : aucun contrat générique sûr n’existe dans la référence stable auditée.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter.Core/Contracts/IMapCatalogService.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/IPlayerModerationHistoryReader.cs`
- `app/src/PinteMod.ControlCenter.Core/Models/MapCatalogModels.cs`
- `app/src/PinteMod.ControlCenter.Core/Models/PlayerAdministrationModels.cs`
- `app/src/PinteMod.ControlCenter.Core/Models/PlayerModerationHistoryModels.cs`
- `app/src/PinteMod.ControlCenter.Core/Models/RconModels.cs`
- `app/src/PinteMod.ControlCenter.Core/Security/MapCodeValidator.cs`
- `app/src/PinteMod.ControlCenter.Core/Simulation/SimulationModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/JsonMapCatalogService.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/LocalPlayerModerationHistoryReader.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/OfficialMapNameResolver.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/PlayerAdministrationCommandService.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/RconDiagnosticResponseValidator.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/RconDiagnosticService.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Simulation/SimulationActionService.cs`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/Controls/PlayerDetailsControl.xaml`
- `app/src/PinteMod.ControlCenter/State/MapCatalogState.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/DashboardViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/DisplayItemViewModels.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/PlayerActionsViewModelBase.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/PlayersViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/ServerView.xaml`
- `app/src/PinteMod.ControlCenter/Views/SettingsView.xaml`
- `app/tests/PinteMod.ControlCenter.Tests/LocalPlayerModerationHistoryReaderTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/MapCatalogServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/PlayerAdministrationCommandServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/PlayerAdministrationViewModelTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/RconDiagnosticServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`
- `app/README.md`
- `app/packaging/LISEZ-MOI.txt`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`
- `app/artifacts/mvp-preview-12-win-x64/`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-12-win-x64.zip`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-12-win-x64.zip.sha256`

### Compilation et tests

- Tests ciblés cartes, historique, administration joueur, diagnostics et WPF : 55/55 réussis.
- Debug : compilation réussie, 0 avertissement, 0 erreur ; 281/281 tests réussis avec le profil Windows requis par DPAPI.
- Release : compilation réussie, 0 avertissement, 0 erreur ; 281/281 tests réussis avec le profil Windows requis par DPAPI.
- Le test DPAPI échoue uniquement dans le bac à sable dépourvu de profil utilisateur ; la relance prévue sous le profil Windows a réussi dans les deux configurations.
- Aucun serveur BOIII, BAT ou EXE serveur n’a été lancé et aucune commande RCON réelle n’a été envoyée.

### Problèmes et validation humaine

- Aucun blocage de compilation ou de test.
- Vérifier visuellement dans Preview 12 le nouveau panneau Catalogue de cartes, le menu Serveur enrichi et le panneau Historique joueur.
- Le test terrain du power-up joueur peut être regroupé avec la validation finale des actions restantes ; il n’est pas nécessaire pour ouvrir et utiliser les fonctions read-only.
- Aucun nouveau retour ChatGPT n’est requis avant cette vérification visuelle. Les seules limites restantes sont volontaires : commandes de carte, boss et événements non activées faute de contrat générique sûr.

### Captures et paquet autonome

- Aucune capture automatique : l’exécutable n’a pas été lancé par Codex.
- Dossier : `app/artifacts/mvp-preview-12-win-x64/`.
- Exécutable : `app/artifacts/mvp-preview-12-win-x64/PinteMod.ControlCenter.exe`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-12-win-x64.zip`.
- SHA-256 : `E7A72DEB20F05EB3D4DA66362DB76C2CAAD0C60163E62F1A7A86733F03655813`.
- Contrôle : 466 entrées, 0 chemin dangereux, 0 fichier interdit, 0 fichier serveur, 0 PDB ; exécutable et LISEZ-MOI présents.

## 2026-08-09 — Bloc C, lot consolidé actions serveur/joueur et Preview 10

### Objectif de la passe

Avancer en un seul lot jusqu’au prochain véritable besoin de manipulation humaine : compléter les actions serveur stables, rendre fonctionnelles les actions joueur ciblées exclusivement par BOIII_XUID local, puis regrouper tous les essais terrain restants dans une validation finale unique.

### Réalisé

- Extension de la liste blanche serveur à la musique de carte, aux passages standard, à la conservation ou l’élimination des zombies et au délai permanent/normal des power-ups PinteMod.
- Aucune saisie de commande libre : chaque action serveur correspond à une valeur d’énumération et à une commande stable exacte.
- Nouvelle administration joueur réelle et typée : Revive, Respawn, Points bornés, Munitions, Godmode, Téléportation au viseur du joueur, armes, atouts, tous les atouts, Mute, Unmute, Kick, Ban à durée fermée, rôle Helper/Modérateur/Admin et retrait du rôle.
- Ciblage exclusif par BOIII_XUID provenant des événements `JOIN/ACTIVE/LEAVE` du `connections.log` de la session active. Le pseudo et le slot restent uniquement des informations d’affichage.
- Le XUID complet reste privé au ViewModel et au service ; aucun ViewModel public, binding XAML, résultat opérateur ou audit en mémoire ne l’expose.
- Après la confirmation humaine, le snapshot local est relu et le même XUID doit toujours être présent avant tout transport.
- Les armes, atouts, montants, durées de ban et rôles proviennent de listes fermées. Le rôle Owner, les alias arbitraires, les raisons libres et l’historique non implémenté sont refusés ou désactivés.
- Confirmation Oui/Non obligatoire, aucun retry et conservation prudente de `CommandSent = true` dès que l’émission UDP a pu commencer.
- Nouveau verrou de sûreté partagé entre Dashboard, Joueurs, Serveur et Community Pause. Une mutation potentielle bloque toutes les pages jusqu’au clic explicite de vérification.
- `ISimulationActionService` est conservé ; toute action qui reste simulée produit toujours `CommandSent = false`.
- Changement/redémarrage de carte, événements, boss, création de power-up et historique joueur restent simulés ou désactivés « À venir ».
- Aucun BOIII, BAT, EXE serveur ou transport RCON réel n’a été lancé pendant l’implémentation et les tests.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter.Core/Models/PlayerAdministrationModels.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/IPlayerAdministrationCommandService.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/IOperatorActivityStore.cs`
- `app/src/PinteMod.ControlCenter.Core/Models/ServerAdministrationModels.cs`
- `app/src/PinteMod.ControlCenter.Core/Simulation/SimulationModels.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/PlayerAdministrationCommandService.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/ServerAdministrationCommandService.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/InMemoryOperatorActivityStore.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Simulation/SimulationActionService.cs`
- `app/src/PinteMod.ControlCenter/State/OperatorMutationSafetyState.cs`
- `app/src/PinteMod.ControlCenter/App.xaml`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/Controls/PlayerDetailsControl.xaml`
- `app/src/PinteMod.ControlCenter/ViewModels/PlayerActionsViewModelBase.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/DashboardViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/PlayersViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/DisplayItemViewModels.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ShellViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/ServerView.xaml`
- `app/tests/PinteMod.ControlCenter.Tests/PlayerAdministrationCommandServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/PlayerAdministrationViewModelTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ServerAdministrationCommandServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ServerPauseCommandViewModelTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/RconDiagnosticServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`
- `app/README.md`
- `app/packaging/LISEZ-MOI.txt`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`
- `app/artifacts/mvp-preview-10-win-x64/`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-10-win-x64.zip`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-10-win-x64.zip.sha256`

### Fonctionnalités disponibles

- Les commandes réelles sont accessibles uniquement en mode hybride avec une source locale valide, une cible RCON privée explicite et un secret DPAPI présent.
- Les pages Dashboard et Joueurs utilisent la même sélection XUID, les mêmes confirmations, la même revalidation et le même verrou global.
- Les résultats ne revendiquent jamais un succès gameplay automatique lorsque BOIII répond sans texte ; l’opérateur doit vérifier la partie ou la console.
- La simulation reste le mode par défaut et toutes les actions non autorisées restent sans transport réel.

### Compilation et tests

- Tests ciblés administration joueur, administration serveur, coordination, confidentialité et WPF : 40/40 réussis lors de la dernière passe ciblée.
- Debug : compilation réussie, 0 avertissement, 0 erreur ; 252/252 tests réussis.
- Release : compilation réussie, 0 avertissement, 0 erreur ; 252/252 tests réussis.
- Publication autonome Release Windows x64 réussie, 0 avertissement et 0 erreur.

### Problèmes rencontrés

- Aucun blocage de compilation, de test ou de packaging.
- Le transport UDP ne fournit pas de preuve d’application gameplay ; la stratégie prudente de verrou et d’acquittement humain reste volontaire.

### Validation humaine nécessaire

- Une seule validation terrain finale est désormais demandée, sur serveur vide ou avec des joueurs consentants.
- Elle doit vérifier un échantillon représentatif des actions serveur et joueur, l’exactitude de la cible XUID, les confirmations, le verrou transversal et l’absence de répétition avant acquittement.
- Les actions destructrices de modération ne doivent être essayées que sur un compte de test dont la déconnexion ou le bannissement est accepté.
- Aucune revue intermédiaire n’est nécessaire sauf régression observée pendant cette validation.

### Captures

- Aucune capture automatique produite : Codex n’a lancé ni l’application publiée ni le serveur réel.
- Les captures utiles seront produites une seule fois pendant la validation terrain finale.

### Paquet autonome

- Dossier : `app/artifacts/mvp-preview-10-win-x64/`.
- Exécutable : `app/artifacts/mvp-preview-10-win-x64/PinteMod.ControlCenter.exe`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-10-win-x64.zip`.
- SHA-256 : `4277A42F2665EFF83CF44CC49E7EADBD342E26AE45C20EB73B6D8C1ED09EEA2F`.
- Contrôle : 466 entrées, 0 chemin dangereux, 0 fichier interdit, 0 PDB ; exécutable et LISEZ-MOI présents.

## 2026-08-09 — Validation terrain des diagnostics Bloc C

### Résultat humain

- Les cinq diagnostics read-only ont été déclenchés depuis Preview 8 et observés dans la console BOIII.
- `ezzmap` : carte `zm_zod`, courant global OFF, manche 1, un déclencheur Pack-a-Punch et profils de carte affichés.
- `ezzpowerstatus` : profil `Local Beast-mode switches`, courant global OFF.
- `ezzpapstatus` : carte Shadows of Evil, profil Ritual access, une machine attendue et zéro machine alimentée.
- `ezzround` : manche 1, zéro IA vivante, file de spawn vide et aucun respawn en attente.
- `ezzplayers` : aucun joueur connecté.
- Les cinq signatures terrain correspondent aux marqueurs stricts implémentés.
- Le retour UDP sans texte est confirmé comme comportement normal : la sortie détaillée est écrite dans la console BOIII et l’interface reste honnête avec `ENVOYÉ · SANS TEXTE`.

### Preuve

- Capture humaine fournie hors dépôt ; aucun chemin utilisateur n’est conservé.

### État

- Lot diagnostics read-only du Bloc C validé.
- Aucun correctif requis.
- Les actions joueur restent bloquées faute de source BOIII_XUID connectée fiable.

## 2026-08-09 — Bloc C, premières actions serveur confirmées et Preview 9

### Objectif de la passe

Rendre fonctionnelles les quatre actions serveur dont le contrat stable est vérifiable, sans créer de faux retour de succès lorsque BOIII imprime uniquement dans sa console.

### Réalisé

- Audit intégral des fonctions concernées dans l’archive stable : `ezznextround`, `ezzsetround`, `ezzpower` et `ezzpap`.
- Nouveau service RCON dédié à liste blanche fermée ; aucune chaîne de commande libre n’entre depuis l’interface.
- `ezzsetround` limité aux choix prédéfinis 2 à 255 et formaté en culture invariante.
- Confirmation Oui/Non obligatoire avant chaque mutation.
- Toute erreur après le début du premier appel UDP conserve `CommandSent = true` et interdit un nouvel envoi.
- Aucun retry automatique et aucun envoi de diagnostic automatique après mutation.
- Après toute émission potentielle, toutes les mutations serveur et Pause/Reprendre sont verrouillées.
- Le verrou ne peut être levé que par l’action explicite `J’AI VÉRIFIÉ LA CONSOLE`.
- Résultat présenté comme `ENVOYÉ · À VÉRIFIER`, jamais comme succès automatique.
- Audit opérateur en mémoire neutralisé pour ces quatre actions.
- Actions joueur toujours simulées et bloquées faute de BOIII_XUID live fiable.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter.Core/Models/ServerAdministrationModels.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/IServerAdministrationCommandService.cs`
- `app/src/PinteMod.ControlCenter.Core/Contracts/IOperatorActivityStore.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/ServerAdministrationCommandService.cs`
- `app/src/PinteMod.ControlCenter.Infrastructure/Rcon/InMemoryOperatorActivityStore.cs`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/ServerView.xaml`
- `app/tests/PinteMod.ControlCenter.Tests/ServerAdministrationCommandServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ServerPauseCommandViewModelTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/RconDiagnosticServiceTests.cs`
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`
- `app/README.md`
- `app/packaging/LISEZ-MOI.txt`
- `docs/GITHUB_SERVER_COMMAND_CATALOG.md`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`
- `app/artifacts/mvp-preview-9-win-x64/`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-9-win-x64.zip`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-9-win-x64.zip.sha256`

### Compilation et tests

- Tests ciblés administration serveur, diagnostics, ViewModel et WPF : 42/42 réussis.
- Debug : compilation réussie, 0 avertissement, 0 erreur ; 240/240 tests réussis.
- Release : compilation réussie, 0 avertissement, 0 erreur ; 240/240 tests réussis.
- Un test statique de composition attendait encore deux services partageant le verrou transport ; il a été ajusté à trois après ajout du service d’administration.
- Aucun serveur BOIII, BAT ou EXE serveur n’a été lancé et aucune commande RCON réelle n’a été envoyée pendant l’implémentation.

### Validation humaine nécessaire

- Serveur vide uniquement.
- Tester d’abord `ACTIVER LE COURANT`, vérifier la console, puis cliquer `J’AI VÉRIFIÉ LA CONSOLE`.
- Tester ensuite `ACTIVER PACK-A-PUNCH` de la même manière.
- Tester `TERMINER LA MANCHE`, vérifier le passage de 1 à 2, puis acquitter.
- Choisir ensuite une manche supérieure, par exemple 5, et tester `DÉFINIR LA MANCHE`.
- Vérifier qu’aucun second bouton réel n’est disponible avant l’acquittement manuel.

### Captures

- Aucune capture automatique : l’application et le serveur réel n’ont pas été lancés par Codex.
- Une capture du panneau d’action et une capture de la console après les quatre essais sont demandées.

### Paquet autonome

- Dossier : `app/artifacts/mvp-preview-9-win-x64/`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-9-win-x64.zip`.
- SHA-256 : `80676ECFC111B607BF8BF4553244031FB9563FAC10A589B1A31FA3E9E3CF0C66`.
- Contrôle : 466 entrées, 0 chemin dangereux, 0 fichier interdit, 0 PDB ; exécutable et LISEZ-MOI présents.

## 2026-08-09 — État courant après publication Preview 10

- La livraison courante est la Preview 10 décrite dans l’entrée consolidée ci-dessus ; elle remplace la Preview 9 pour les prochains essais.
- Debug : 0 avertissement, 0 erreur, 252/252 tests réussis.
- Release : 0 avertissement, 0 erreur, 252/252 tests réussis.
- Exécutable : `app/artifacts/mvp-preview-10-win-x64/PinteMod.ControlCenter.exe`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-10-win-x64.zip`.
- SHA-256 : `4277A42F2665EFF83CF44CC49E7EADBD342E26AE45C20EB73B6D8C1ED09EEA2F`.
- Prochaine étape : une seule validation terrain finale regroupant les actions serveur et joueur ; aucune micro-validation intermédiaire.

## 2026-08-09 — Catalogue complet des cartes serveur et Preview 11

### Objectif et résultat terrain reçu

- L’opérateur a confirmé le fonctionnement réel de Définir la manche, Activer le courant, Activer Pack-a-Punch et Terminer la manche ; les événements `GAMEPLAY_ACTION` attendus sont présents et la partie est correctement marquée Unranked.
- Le menu de carte ne contenait que cinq choix alors que la copie de test en expose davantage.

### Réalisé

- Lecture strictement limitée aux deux lignes `sv_maprotation` de `<COPIE_DE_TEST>\UnrankedServer\zone\server_zm.cfg` ; aucun autre réglage ou secret n’a été lu ou affiché.
- La rotation active contient Origins uniquement ; la ligne catalogue déclare 14 cartes officielles installées.
- Le sélecteur Serveur contient désormais les 14 codes exacts et leurs noms lisibles, dans l’ordre du catalogue.
- Le libellé précise que la liste est complète mais que Changer et Redémarrer restent simulés.
- Aucun lecteur runtime de `server_zm.cfg` n’a été ajouté : les sources locales autorisées et le partage LAN read-only restent inchangés.
- Régression automatisée ajoutée sur le nombre, l’ordre, l’unicité des codes et la présence des libellés.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/ServerView.xaml`
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`
- `app/README.md`
- `app/packaging/LISEZ-MOI.txt`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`
- `app/artifacts/mvp-preview-11-win-x64/`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-11-win-x64.zip`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-11-win-x64.zip.sha256`

### Compilation et tests

- Test ciblé du catalogue : 1/1 réussi.
- Debug : 0 avertissement, 0 erreur, 253/253 tests réussis sous le profil Windows requis par DPAPI.
- Release : 0 avertissement, 0 erreur, 253/253 tests réussis sous le profil Windows requis par DPAPI.
- L’échec initial isolé du test DPAPI dans le bac à sable provenait de l’absence de profil utilisateur chargé ; la relance prévue sous le profil Windows a réussi dans les deux configurations.
- Aucun serveur, BAT, EXE serveur ou transport RCON n’a été lancé par Codex.

### Validation humaine

- Vérifier simplement que les 14 cartes apparaissent dans le menu de Preview 11.
- Aucun changement de carte réel n’est demandé pendant cette passe.

### Captures

- Aucune capture automatique produite.

### Paquet autonome

- Dossier : `app/artifacts/mvp-preview-11-win-x64/`.
- Exécutable : `app/artifacts/mvp-preview-11-win-x64/PinteMod.ControlCenter.exe`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-11-win-x64.zip`.
- SHA-256 : `820D16A6AE8B6FF0CC655E99788261E35800BD1AA1756EA8A83F06C866948F93`.
- Contrôle : 466 entrées, 0 chemin dangereux, 0 fichier interdit, 0 PDB ; exécutable et LISEZ-MOI présents.

### Remplacement de cette livraison

La Preview 11 est désormais remplacée par la Preview 13 (`app/artifacts/mvp-preview-13-win-x64/PinteMod.ControlCenter.exe`), validée à 285/285 tests en Debug et Release. Utiliser uniquement la Preview 13 pour la prochaine vérification visuelle.

## 2026-08-09 — Stabilisation opérateur en lot et Preview 13

### Objectif et résultat

- Améliorer la transmission des preuves opérateur sans export automatique ni donnée brute.
- Ajouter dans un même lot la copie de la dernière réponse diagnostique depuis Paramètres et Serveur, ainsi que la copie du filtre visible de la Live Console.
- Clarifier que le power-up réel est une action joueur ciblée par BOIII_XUID, tandis que le power-up global de la page Serveur reste simulé.
- Aucun contrat RCON, lecteur local, commande gameplay ou architecture métier existante n’a été élargi.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter/Services/ITextClipboardService.cs`
- `app/src/PinteMod.ControlCenter/App.xaml.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/LogsViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`
- `app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs`
- `app/src/PinteMod.ControlCenter/Views/LogsView.xaml`
- `app/src/PinteMod.ControlCenter/Views/ServerView.xaml`
- `app/src/PinteMod.ControlCenter/Views/SettingsView.xaml`
- `app/tests/PinteMod.ControlCenter.Tests/ClipboardExportViewModelTests.cs`
- `app/README.md`
- `app/packaging/LISEZ-MOI.txt`
- `docs/CODEX_PROGRESS.md`
- `docs/TODO.md`
- `docs/DECISIONS.md`
- `app/artifacts/mvp-preview-13-win-x64/`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-13-win-x64.zip`
- `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-13-win-x64.zip.sha256`

### Validation

- Tests ciblés ViewModels, Live Console, vues WPF et presse-papiers injecté : 67/67 réussis.
- Debug : 0 avertissement, 0 erreur, 285/285 tests réussis.
- Release : 0 avertissement, 0 erreur, 285/285 tests réussis.
- Le presse-papiers Windows réel n’est pas utilisé par les tests ; les échecs sont simulés et restent non bloquants.
- Aucun serveur BOIII, BAT ou EXE serveur n’a été lancé et aucune commande RCON réelle n’a été envoyée.

### Paquet et prochaine validation humaine

- Exécutable : `app/artifacts/mvp-preview-13-win-x64/PinteMod.ControlCenter.exe`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-13-win-x64.zip`.
- SHA-256 : `8ED173DEF5D67B14791433AAE1B60EBD136BA6F3963D972CE59E5D5D59D205F5`.
- Contrôle : 466 entrées, aucun chemin dangereux, fichier interdit, fichier serveur ou PDB.
- À vérifier la semaine prochaine : ouverture des trois pages, copie d’une réponse diagnostique déjà présente et copie d’un filtre de Live Console. Aucun essai RCON n’est nécessaire pour vérifier les boutons de copie.
- ChatGPT n’est pas requis avant cette vérification ; préparer une revue externe uniquement après la validation terrain finale regroupée ou en cas de régression observée.

## 2026-08-09 — Préparation de la reprise et estimation de clôture

- Création de `docs/QUOTA_ESTIMATE.md` avec deux estimations distinctes : clôture de la V1 sur les contrats existants et Control Center étendu nécessitant de nouveaux contrats/snapshots PinteMod.
- Création de `docs/PROMPT_REPRISE_CODEX.md`, autonome et directement réutilisable après renouvellement du quota.
- Création de `docs/PROMPT_REVUE_CHATGPT.md`, centrée sur les seuls blocages de sécurité, confidentialité, robustesse, RCON et conditionnement du paquet.
- Décision consignée : les futures extensions GSC ne doivent pas être présentées comme de simples boutons WPF restant à terminer.
- Aucun code applicatif, serveur, secret, RCON ou paquet de la Preview 13 n’a été modifié pendant cette passe documentaire.
- Création de `docs/PINTEMOD_REQUIREMENTS_NEXT.md` : heartbeat global, snapshot runtime serveur/joueurs, capacités par carte, feedback de mutations et contrats sûrs pour les futures actions carte/boss/événements.

## 2026-08-09 — Revue globale V1 validée

### Verdict reçu

- Aucun blocage obligatoire détecté.
- Aucune correction de sécurité, confidentialité, RCON, confinement local, sérialisation ou packaging n’est requise avant clôture.
- La revue confirme les listes blanches fermées, le ciblage BOIII_XUID, DPAPI CurrentUser, la neutralisation des données, le confinement des lecteurs, l’absence d’écriture PinteMod et la sémantique conservatrice des mutations UDP.
- Le SHA-256 du paquet Preview 13 a été recalculé par la revue et correspond exactement à `8ED173DEF5D67B14791433AAE1B60EBD136BA6F3963D972CE59E5D5D59D205F5`.
- Conclusion externe explicite : la V1 peut être clôturée en conservant les fonctions sans contrat sûr en simulation.

### Décision de livraison

- Aucun code applicatif n’est modifié à la suite du verdict.
- Les octets exacts de la Preview 13 auditée sont promus en candidate `v2.2.0-rc.1`.
- La revue statique clôt le code V1 ; la dernière validation terrain groupée reste le jalon avant le tag stable `v2.2.0`.
- Les extensions nécessitant de nouveaux contrats PinteMod restent suivies séparément dans `docs/PINTEMOD_REQUIREMENTS_NEXT.md`.

### Compilation et tests de clôture

- SDK officiel .NET 8.0.423 installé durablement sur la machine de développement.
- Debug : compilation réussie, 0 avertissement, 0 erreur ; 285/285 tests réussis.
- Release : compilation réussie, 0 avertissement, 0 erreur ; 285/285 tests réussis.
- Aucun serveur BOIII, BAT, EXE serveur ou transport RCON n’a été lancé pendant cette validation.

### Paquet candidat

- Source auditée : `app/artifacts/PinteMod-ControlCenter-v2.2-MVP-Preview-13-win-x64.zip`.
- Candidate : `app/artifacts/PinteMod-ControlCenter-v2.2.0-rc.1-win-x64.zip`.
- SHA-256 commun : `8ED173DEF5D67B14791433AAE1B60EBD136BA6F3963D972CE59E5D5D59D205F5`.
- Identité binaire vérifiée : les deux archives ont exactement la même empreinte et la même taille de 70 067 814 octets.
- Contrôle de candidate : 466 entrées, 0 chemin dangereux, 0 nom interdit, exécutable et `LISEZ-MOI.txt` présents.
- Publication GitHub prévue sous le tag préversion `v2.2.0-rc.1`, avec l’archive et son fichier SHA-256 comme seuls assets binaires.

### Problèmes et validation humaine

- Le runtime .NET système ne contenait initialement aucun SDK ; l’installation officielle durable 8.0.423 a corrigé ce point sans modification du projet.
- Aucune capture supplémentaire n’est produite, puisque le code et le rendu sont strictement ceux du paquet déjà revu.
- La validation terrain groupée des mutations restantes demeure nécessaire avant le tag stable `v2.2.0` ; elle n’est pas remplacée par la revue statique.

## 2026-08-09 — Corrections bloquantes finales et candidate v2.2.0-rc.2

### Objectif de la passe

Traiter en une livraison unique les quatre blocages de la seconde revue Preview 13 : données/chemins sensibles dans le paquet, messages système exposables, fenêtre TOCTOU des points de réanalyse et faux négatif `CommandSent` des diagnostics RCON.

### Réalisé

- La RC1 a été immédiatement retirée de la publication active ; elle est historiquement documentée comme candidate retirée.
- Les XUID simulés et exemples ont été remplacés par quatre identifiants réservés fictifs. Les tests refusent tout littéral XUID non réservé dans les sources de production et les contrats.
- Le profil Release supprime les symboles, active le build déterministe et cartographie les chemins de source.
- Tous les lecteurs PinteMod concernés ouvrent désormais le fichier par un handle read-only, résolvent la cible réellement ouverte avant lecture et réutilisent ce même handle. Une divergence devient un refus local contrôlé ; la normalisation UNC explicite est conservée.
- Les messages publics de lecture utilisent des libellés fermés. Aucun `Exception.Message` système n’est présent dans les lecteurs publics.
- `RconDiagnosticService` résout d’abord sa liste blanche, puis considère l’envoi comme potentiel dès le début de l’appel de transport. Timeout, socket ou I/O après ce point conservent `CommandSent = true`, sans retry.
- Le workflow CI publie un Windows x64 Release et exécute le nouveau contrôle de paquet.
- Le ZIP RC2 a été créé puis audité avec sa racine de build et l’ancien identifiant interdits explicitement.

### Fichiers créés ou modifiés

- build/CI : `.github/workflows/ci.yml`, `app/Directory.Build.props` ;
- packaging : `app/packaging/LISEZ-MOI.txt`, `app/packaging/Test-PublishedPackage.ps1`, `app/src/PinteMod.ControlCenter/PinteMod.ControlCenter.csproj` ;
- confinement local : `VerifiedReadOnlyFile.cs`, `ReadOnlyJsonFileReader.cs`, `ReadOnlyRankJsonFileReader.cs`, `ReadOnlyEasterEggJsonFileReader.cs`, `ReadOnlyBlockAJsonFileReader.cs`, `StructuredLogReader.cs`, `CommunityPauseLogReader.cs`, `CommunityPauseStatusReader.cs`, `LocalPlayerModerationHistoryReader.cs`, `RankProfileReader.cs`, `RoundRecordReader.cs`, `EasterEggRecordReader.cs` ;
- RCON : `RconDiagnosticService.cs` ;
- simulation/contrats : `SimulatedControlCenterDataProvider.cs`, `contracts/command_request.example.json`, `contracts/command_result.example.json`, `contracts/players_state.example.json` ;
- tests : `FinalPrivacyRegressionTests.cs`, `VerifiedReadOnlyFileTests.cs`, `RconDiagnosticServiceTests.cs`, `SimulatedProviderTests.cs`, `ViewModelTests.cs`, `XuidValidatorTests.cs` ;
- documentation : `README.md`, `README_FR.md`, `app/README.md`, `docs/RELEASE_NOTES_v2.2.0-rc.1.md`, `docs/RELEASE_NOTES_v2.2.0-rc.2.md`, `docs/PROMPT_REVUE_CHATGPT.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md`, `docs/DECISIONS.md`.

### Fonctionnalités et garanties

- Fonctionnalités opérateur et listes blanches inchangées.
- Simulation par défaut, actions simulées et `CommandSent = false` inchangés.
- Aucun serveur BOIII, BAT, EXE serveur ou transport RCON réel lancé.
- Aucune écriture PinteMod, modification GSC, découverte réseau, port entrant ou secret lu.

### Compilation et tests

- Debug : 0 avertissement, 0 erreur, 292/292 tests réussis.
- Release : 0 avertissement, 0 erreur, 292/292 tests réussis.
- Une première relance a rencontré un ancien fichier Debug verrouillé par des processus .NET extérieurs ; la validation a été exécutée dans `app/artifacts/final-validation/` sans fermer le Control Center ni toucher à BOIII.
- L’unique échec initial était l’attente historique de l’ancien XUID simulé dans `ViewModelTests`; l’attente a été remplacée par l’identifiant réservé puis les deux suites complètes ont réussi.

### Publication et contrôle

- Dossier publié : `app/artifacts/v1-rc2-win-x64/`.
- Exécutable : `app/artifacts/v1-rc2-win-x64/PinteMod.ControlCenter.exe`.
- ZIP : `app/artifacts/PinteMod-ControlCenter-v2.2.0-rc.2-win-x64.zip`.
- Manifeste : `app/artifacts/PinteMod-ControlCenter-v2.2.0-rc.2-win-x64.zip.sha256`.
- SHA-256 : `2C30BB4BBB3F73DB15588D78518F94914FAB87B2EDA34364B9CEB8E8B5C58124`.
- Audit : 466 entrées ; aucun PDB, secret, configuration opérateur, fichier serveur/runtime, chemin ZIP dangereux, ancienne valeur XUID interdite ou racine privée de compilation.

### Problèmes, validation humaine et captures

- Aucun blocage technique connu ne reste dans les quatre corrections demandées.
- Validation humaine requise : transmettre exclusivement la RC2 et son manifeste à ChatGPT pour le verdict final de clôture ; ne plus transmettre ni utiliser la RC1.
- Aucune capture produite : aucun changement visuel n’a été effectué.

## 2026-08-12 — Intégration post-RC2 du heartbeat et du snapshot runtime PinteMod

### Objectif de la passe

Auditer PinteModReal sans le modifier, confirmer les contrats déjà produits par le bridge runtime v0.1.2, puis les consommer dans une branche Control Center créée exactement depuis la RC2 validée.

### Audit et traçabilité

- Base Control Center : `90d4922cb663e4b8d923ecfb1681483d78db5126`.
- Branche locale : `codex/post-rc2-runtime-contracts`.
- PinteModReal audité en lecture seule : `0b293b5371e4405805017bd3afff16cf28276043`.
- Producteur confirmé : `custom_scripts/ezz_admin_control_center_runtime.gsc`, v0.1.2.
- Heartbeat confirmé : `health/pintemod.json`, schéma 1, environ 5 secondes, 4096 octets.
- Runtime confirmé : `runtime/control_center_snapshot.json`, schéma 1, environ 2 secondes, 32768 octets, maximum 4 joueurs et 8 armes par joueur.
- Aucun fichier PinteModReal, serveur, GSC, tag ou asset RC2 n’a été modifié.

### Réalisé

- Ajout de deux chemins locaux explicitement autorisés et de deux lecteurs stricts versionnés.
- Acceptation de `updated_at_utc` vide conformément au contrat réel ; fraîcheur calculée depuis le LastWriteTimeUtc du handle vérifié.
- Cache borné et invalidé au changement de session ; refus des sessions/cartes différentes, fichiers futurs, `.tmp`, `.bak`, schémas et enums inconnus.
- Overlay appliqué uniquement à une source locale réussie, fraîche et cohérente avec `current_session.json`.
- État PinteMod réel : running frais sain, stopped frais hors ligne, error frais en erreur, expiré inconnu.
- Runtime réel : carte, manche, durée, Ranked, joueurs/max, courant et Pack-a-Punch.
- Joueurs réels : BOIII_XUID interne, pseudo informatif, client, vie, points, santé, Godmode, arme équipée, munitions, inventaire et atouts.
- Rôle/langue/pays enrichis uniquement par BOIII_XUID ; état Mute non inventé.
- Fiche joueur enrichie sans exposition du XUID complet.
- Les logs, Ranks, records, Easter Egg Records, Community Pause et listes blanches RCON existants sont préservés.
- ChangeMap, RestartMap, TriggerEvent et SpawnBoss restent simulés.

### Fichiers créés ou modifiés

- Core : nouveaux contrats heartbeat/runtime, modèles runtime, extensions non destructives de `ServerState`, `PlayerState` et `BlockALocalSnapshot`.
- Infrastructure : chemins whitelistés, lecteur JSON à limite par contrat, `RuntimeJsonContract`, `PinteModHeartbeatReader`, `ControlCenterRuntimeSnapshotReader` et `PinteModRuntimeOverlayDataProvider`.
- Présentation : composition root, Dashboard, Serveur, fiche joueur et états visuels runtime.
- Tests : fixtures locales, lecteurs heartbeat/runtime, overlay, présentation, autorisation joueur runtime et garantie read-only.
- Documentation : `app/README.md`, `docs/POST_RC2_PINTEMOD_RUNTIME_AUDIT.md`, `docs/PINTEMOD_REQUIREMENTS_NEXT.md`, `docs/TODO.md`, `docs/DECISIONS.md`, `docs/CODEX_PROGRESS.md`.

### Fonctionnalités disponibles

- Mode simulation toujours utilisé par défaut.
- Mode hybride toujours explicite, local ou LAN read-only.
- Le Dashboard n’affiche plus « aucun heartbeat dédié » lorsque le heartbeat PinteMod valide est présent.
- Les valeurs inférées depuis les logs cèdent la priorité au snapshot runtime frais de la session active.
- Les données runtime détaillées du joueur sont visibles sans modifier l’architecture générale ni les contrats RCON.

### Compilation et tests

- Tests ciblés runtime/UI/read-only : 125/125 réussis.
- Debug : 0 avertissement, 0 erreur, 344/344 tests réussis.
- Release : 0 avertissement, 0 erreur, 344/344 tests réussis.
- Aucun serveur BOIII, BAT, EXE serveur ou transport RCON n’a été lancé.
- Aucune écriture n’a été effectuée dans PinteModReal ou une racine PinteModData.

### Problèmes rencontrés

- Un avertissement nullable dans un nouveau test de présentation a été corrigé avant la validation finale.
- Aucun blocage technique restant dans le périmètre des deux contrats existants.

### Validation humaine

- Après extraction du paquet post-RC2, ouvrir une source Local/LAN read-only contenant les deux fichiers actifs.
- Vérifier visuellement l’état PinteMod, la manche/durée/Ranked et la fiche runtime d’un joueur.
- Aucun test RCON ni action serveur n’est nécessaire pour cette validation de lecture.

### Captures

- Aucune capture automatique produite ; une capture Dashboard et une capture fiche joueur pourront être réalisées pendant la validation humaine sur une source runtime réelle.

## 2026-08-12 — Correctifs terrain armes/PAP/diagnostics et audit UX post-RC2

### Objectif de la passe

Corriger en un seul lot le catalogue Give Weapon incomplet, ajouter le Pack-a-Punch de l’arme tenue, rendre les diagnostics utiles malgré les réponses RCON vides et vérifier systématiquement les fonctions PinteMod sûres encore absentes de l’interface.

### Réalisé

- Catalogue central Core de 19 armes standard/universelles et des spécialités officielles de chaque carte annoncées par PinteMod Weapons v0.5.2.
- Affichage des spécialités uniquement avec un runtime local frais, de session et carte cohérentes ; carte inconnue ou source périmée : aucun alias spécial.
- Nouvelle action réelle `ezzpapweapon <BOIII_XUID>` sans argument libre, confirmation, revalidation XUID, verrou transversal, zéro retry et acquittement manuel.
- Nouvelle action réelle de retrait ciblé d’atout `ezzremoveperk <BOIII_XUID> <alias>`, limitée aux neuf alias existants.
- PAP joueur désactivé lorsqu’aucune arme équipée n’est observable ou que l’état est explicitement amélioré ; PinteMod reste l’autorité de compatibilité.
- Fallback local structuré après une réponse RCON vide pour Carte, Courant, PAP de carte, Manche et Joueurs ; provenance annoncée, XUID complets neutralisés.
- Community Pause conserve son lecteur spécialisé. Health affiche au plus un résumé des services locaux et ne prétend jamais reproduire les 51 contrôles.
- Audit carte, événements et power-ups signalent explicitement la limite de transport lorsqu’aucun texte n’est renvoyé ; aucun résultat n’est inventé.
- Bouton Santé PinteMod ajouté aux diagnostics Serveur.
- Audit complet des boutons et commandes documenté dans `docs/UX_FEATURE_AUDIT.md`. Warn, AFK, commandes libres, toggle/clear perks, états musicaux libres, ChangeMap/RestartMap/Event/Boss réels n’ont pas été ajoutés.

### Fichiers créés ou modifiés

- Core : `PlayerWeaponCatalog.cs`, `PlayerAdministrationModels.cs`, `SimulationModels.cs`.
- Infrastructure : `PlayerAdministrationCommandService.cs`, `SimulationActionService.cs`.
- Présentation : `LocalDiagnosticFallback.cs`, `PlayerActionsViewModelBase.cs`, `DisplayItemViewModels.cs`, `ServerViewModel.cs`, `SettingsViewModel.cs`, `PlayerDetailsControl.xaml`, `ServerView.xaml`, `App.xaml.cs`.
- Tests : `PlayerWeaponCatalogTests.cs`, `PlayerWeaponActionsViewModelTests.cs`, `ServerDiagnosticFallbackTests.cs`, `PlayerAdministrationCommandServiceTests.cs`, `SimulationActionServiceTests.cs`.
- Documentation : `UX_FEATURE_AUDIT.md`, `CODEX_PROGRESS.md`, `TODO.md`, `DECISIONS.md`, `PINTEMOD_REQUIREMENTS_NEXT.md`, `app/README.md`.

### Fonctionnalités disponibles

- Give Weapon complet, fermé et contextuel à la carte.
- Pack-a-Punch de l’arme tenue et retrait ciblé d’un atout.
- Diagnostics Serveur lisibles sans consulter la console pour les cinq états couverts par le runtime frais.
- Santé PinteMod accessible dans Serveur, sans faux résultat `PASS=51`.
- Toutes les baselines post-RC2, listes blanches, simulations et protections opérateur restent actives.

### Compilation et tests

- Tests ciblés armes/PAP/diagnostics/UX : 70/70 réussis.
- Debug : 0 avertissement, 0 erreur, 377/377 tests réussis.
- Release : 0 avertissement, 0 erreur, 377/377 tests réussis.
- Les tests complets ont été exécutés séquentiellement avec le profil Windows afin de permettre au test DPAPI `CurrentUser` de fonctionner.

### Problèmes rencontrés

- Une première exécution parallèle Debug/Release dans le bac à sable a privé le test DPAPI du profil utilisateur Windows : 376 tests réussis et ce seul test en erreur d’environnement. Aucun code produit n’était en cause. Les deux relances séquentielles avec le profil Windows ont obtenu 377/377.
- Aucun blocage fonctionnel connu ne reste.

### Validation humaine nécessaire

- Give Weapon : plusieurs armes standard puis une spécialité de la carte active.
- PAP arme tenue : arme normale, arme déjà PAP et arme non compatible si disponible.
- Diagnostics : vérifier Carte, Courant, PAP, Manche et Joueurs avec réponse RCON vide ; le Control Center doit indiquer la provenance locale et ne plus imposer la lecture de la console.
- Vérifier que Santé ne présente jamais le résumé local comme le résultat complet `ezzhealth full`.

### Captures

- Aucune capture automatique : les données pertinentes exigent une source runtime terrain réelle. Captures à produire pendant la validation humaine minimale.

## 2026-08-12 — Mise en page responsive des actions Armes, Atouts et Power-ups

### Objectif de la passe

Corriger le décalage visuel provoqué par l’ajout de boutons dans la fiche joueur et rendre ce panneau durablement adaptable aux prochains ajouts.

### Réalisé

- Remplacement du flux unique `WrapPanel` par trois grilles responsives indépendantes : Arme, Atout et Power-up joueur.
- Suppression des largeurs fixes des trois sélecteurs ; chaque contrôle occupe désormais la cellule calculée selon la largeur disponible.
- Retour à la ligne centré pour les deux libellés longs Pack-a-Punch et Power-up.
- Ajout d’un test de régression vérifiant la séparation des groupes, l’absence de `WrapPanel` et de largeur fixe sur les sélecteurs.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter/Controls/PlayerDetailsControl.xaml`.
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`.
- `docs/CODEX_PROGRESS.md`, `docs/TODO.md`, `docs/DECISIONS.md`.

### Fonctionnalités disponibles

- À largeur réduite, chaque groupe passe automatiquement de plusieurs colonnes à une seule sans mélanger ses actions avec le groupe suivant.
- À grande largeur, les actions reprennent automatiquement trois colonnes pour les armes, quatre pour les atouts et deux pour les power-ups.
- Ajouter ultérieurement un bouton dans l’un des trois groupes provoquera un retour à la ligne local à ce groupe.

### Compilation et tests

- Test ciblé responsive : 1/1 réussi.
- Debug : 0 avertissement, 0 erreur, 378/378 tests réussis.
- Release : 0 avertissement, 0 erreur, 378/378 tests réussis.

### Problèmes rencontrés

- La première assertion du nouveau test confondait la largeur minimale responsive avec une ancienne largeur fixe de sélecteur ; l’assertion a été ciblée sur les propriétés `Width` des ComboBox avant la validation finale.
- Aucun blocage technique connu ne reste.

### Validation humaine nécessaire

- Vérifier visuellement la fiche joueur dans une fenêtre étroite puis large, en particulier les trois groupes Armes, Atouts et Power-ups.

### Captures

- Capture source du défaut fournie par l’opérateur : `<CAPTURE_OPÉRATEUR_LOCALE>/responsive-defaut.png`.
- Aucune nouvelle capture automatique produite ; la capture corrigée reste à réaliser pendant la validation humaine.

### Paquet testable

- Commit applicatif : `b57db3917693df2f73d895ba3ff0c1a6fb387829`.
- Archive autonome Windows x64 : `app/artifacts/post-rc2-responsive-b57db391-preview-win-x64/PinteMod-ControlCenter-post-RC2-responsive-preview-b57db391-win-x64.zip`.
- Audit du paquet : `PASS`, 466 entrées.
- SHA-256 : `BBE71D3C32FAA4F55D2839E4761C00E3EEEDDBB6DFB6CED1C833B9A7771A2A61`.

## 2026-08-12 — Paquet global de revue ChatGPT post-RC2

### Objectif de la passe

Regrouper en une seule preuve auditable les trois lots post-RC2 : overlay runtime, correctifs terrain armes/PAP/diagnostics et interface responsive.

### Réalisé

- Ajout d’un prompt ChatGPT spécifique au diff post-RC2, sans remettre en cause la baseline RC2 validée.
- Production de trois patches applicatifs ordonnés depuis `90d4922` jusqu’à `b57db391`.
- Production d’une archive de 229 entrées contenant uniquement la solution, les sources, tests, contrats et scripts de packaging nécessaires à la revue.
- Inclusion du paquet Windows x64 autonome déjà audité, de son manifeste, des rapports de tests et de la procédure terrain groupée.
- Création d’un manifeste SHA-256 interne couvrant chaque fichier puis d’un manifeste externe pour le ZIP global.

### Fichiers créés ou modifiés

- `docs/PROMPT_REVUE_CHATGPT_POST_RC2.md`.
- `docs/CODEX_PROGRESS.md`, `docs/TODO.md`, `docs/DECISIONS.md`.
- Artefacts ignorés sous `app/artifacts/post-rc2-global-review-0bcc4a5f/`.
- ZIP final : `app/artifacts/PinteMod-ControlCenter-post-RC2-global-review-0bcc4a5f.zip`.

### Compilation et tests

- Aucune source applicative modifiée après les validations finales.
- Debug conservé : 0 avertissement, 0 erreur, 378/378 tests réussis.
- Release conservé : 0 avertissement, 0 erreur, 378/378 tests réussis.
- Audit source : PASS, 229 entrées.
- Audit binaire : PASS, 466 entrées.
- Scan des chaînes privées connues : PASS.

### Problèmes rencontrés

- `app/README.md` contient historiquement un exemple de chemin absolu du workspace. Il a été exclu de l’archive des sources et des patches de revue ; le code, les projets, les tests et les contrats restent complets pour l’analyse.
- Aucun blocage connu ne reste dans la préparation de la revue.

### Validation humaine nécessaire

- Envoyer le ZIP global à ChatGPT et lui demander d’utiliser `PROMPT_CHATGPT_POST_RC2.md`.
- Après un verdict sans blocage, suivre une seule fois `VALIDATION_TERRAIN_GROUPEE.md`.

### Captures

- Aucune nouvelle capture requise pour la revue de code ; la correction responsive a déjà été confirmée par l’opérateur.

### Empreinte de livraison

- ZIP global : `PinteMod-ControlCenter-post-RC2-global-review-0bcc4a5f.zip`.
- SHA-256 : `38DA3EA2CFBBDAD66F39B6D210E11D297776C886C64447C89AF042EF8AF8C85B`.
- Contenu racine : 13 entrées, manifeste interne présent et aucun chemin ZIP dangereux.

## 2026-08-13 — Corrections de contre-revue du lecteur JSON partagé

### Objectif de la passe

Clore les deux blocages de la revue globale post-RC2 : lecture réellement bornée lorsqu’un fichier grossit et association des octets aux métadonnées du handle vérifié plutôt qu’au chemin remplaçable.

### Réalisé

- Ajout d’une lecture plafonnée à `maximumFileSizeBytes + 1` octets ; le dépassement est refusé avant callback, parsing ou nouvelle allocation.
- Taille initiale, taille finale et `LastWriteTimeUtc` lus par `GetFileInformationByHandle` sur le même handle déjà validé par `VerifiedReadOnlyFile`.
- Suppression de tout `FileInfo(path)` et `File.GetLastWriteTimeUtc(path)` du lecteur JSON partagé.
- Fichier absent traité par les exceptions contrôlées `FileNotFoundException` et `DirectoryNotFoundException`, sans contrôle préalable vulnérable au remplacement.
- Les erreurs de parsing et de validation utilisent uniquement la dernière date obtenue du handle vérifié.
- Ajout de trois régressions : copie plafonnée exacte, croissance après le contrôle initial et remplacement du chemin avec conservation des octets/date du handle original.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter.Infrastructure/Local/ReadOnlyJsonFileReader.cs`.
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/VerifiedReadOnlyFile.cs`.
- `app/tests/PinteMod.ControlCenter.Tests/VerifiedReadOnlyFileTests.cs`.
- `docs/CODEX_PROGRESS.md`, `docs/TODO.md`, `docs/DECISIONS.md`.

### Compilation et tests

- Tests ciblés handle/heartbeat/runtime : 51/51 réussis.
- Debug : 0 avertissement, 0 erreur, 381/381 tests réussis.
- Release : 0 avertissement, 0 erreur, 381/381 tests réussis.

### Garanties préservées

- Aucun changement RCON, commande, ViewModel, XAML, GSC, réseau ou écriture PinteMod.
- Limites heartbeat 4 Kio et runtime 32 Kio conservées, désormais appliquées pendant la consommation du flux.
- L’ouverture read-only vérifiée par handle et le support UNC explicite sont conservés.
- Aucun serveur BOIII, BAT, EXE serveur ou transport RCON lancé.

### Validation humaine nécessaire

- Aucune manipulation serveur : produire un paquet de contre-revue et faire vérifier uniquement ces deux corrections par ChatGPT.

### Captures

- Aucune capture nécessaire : le correctif concerne exclusivement la sûreté du lecteur local.

### Paquet de contre-revue

- Correctif applicatif : `0e4e09284ab8523dc1bb86ce4f162c1aae6ee0ac`.
- Binaire Windows x64 : audit PASS, 466 entrées, SHA-256 `FAB712771FABB95A548A862BE97C35B81593EA4D21174B52101C6786D52A5107`.
- Sources de contre-revue : audit PASS, 229 entrées.
- ZIP à transmettre : `app/artifacts/PinteMod-ControlCenter-post-RC2-handle-counter-review-0e4e0928.zip`.
- SHA-256 du ZIP : `5451C4217823527A7E5810A466FD8F4EBB678A3972C505F3EFAA17B990F3D410`.
- Contenu racine : 10 entrées, manifeste interne présent, aucun chemin dangereux ni chaîne privée connue détectée.

### Verdict de contre-revue

- Verdict humain transmis le 2026-08-13 : **VALIDÉ**.
- Les deux blocages du lecteur JSON partagé sont clôturés.
- Le lot post-RC2 est autorisé à passer à la validation terrain groupée.
- Aucun correctif de code ou nouveau packaging n’est requis avant cette validation.

## 2026-08-13 — Validation terrain groupée post-RC2

### Verdict humain

- Validation terrain transmise par l’opérateur : **VALIDÉE**.
- Les diagnostics locaux, actions joueur et retours opérateur du lot post-RC2 sont acceptés.
- La mise en page responsive avait déjà été validée humainement.
- Aucun bug ou correctif supplémentaire n’a été signalé.

### État final du lot

- Révision applicative candidate : `0e4e09284ab8523dc1bb86ce4f162c1aae6ee0ac`.
- Debug : 0 avertissement, 0 erreur, 381/381 tests réussis.
- Release : 0 avertissement, 0 erreur, 381/381 tests réussis.
- Binaire Windows x64 : audit PASS, 466 entrées.
- SHA-256 du binaire : `FAB712771FABB95A548A862BE97C35B81593EA4D21174B52101C6786D52A5107`.
- Revue ChatGPT et validation terrain : validées sans condition restante.
- Aucun blocage connu ne reste dans le périmètre post-RC2.

## 2026-08-13 — Stabilisation des sélecteurs et fondation multi-serveurs

### Objectif de la passe

Préparer la publication stable avec des menus déroulants qui ne remontent plus pendant l’actualisation automatique et plusieurs contextes serveur réellement isolés dans une même fenêtre.

### Réalisé

- Les catalogues Armes et Cartes ne sont plus vidés/recréés lorsque leur contenu est inchangé ; un menu ouvert conserve donc sa position et ses objets de sélection pendant les rafraîchissements.
- Ajout d’un catalogue local de profils serveurs, limité à huit profils et migré automatiquement depuis la configuration unique existante.
- Chaque profil possède sa configuration, son catalogue de cartes, son secret RCON DPAPI, son cache, son moniteur local, son coordinateur RCON et son verrou de mutation propres.
- Ajout d’une barre d’onglets serveurs, d’un bouton d’ajout et d’un retrait confirmé qui n’arrête ni ne modifie BOIII et conserve les fichiers locaux récupérables.
- Ajout du nom local de l’onglet dans Paramètres ; ce libellé ne prétend pas modifier le nom public BOIII.
- Simulation conservée par défaut pour tout nouveau profil ; aucune découverte automatique de serveur et aucun envoi RCON automatique.

### Fichiers créés ou modifiés

- `app/src/PinteMod.ControlCenter.Core/Contracts/IOperatorWorkspaceConfigurationStore.cs`.
- `app/src/PinteMod.ControlCenter.Core/Models/OperatorDataSourceModels.cs`.
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/JsonOperatorConfigurationStore.cs`.
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/JsonOperatorWorkspaceConfigurationStore.cs`.
- `app/src/PinteMod.ControlCenter.Infrastructure/Local/OperatorProfileStoragePaths.cs`.
- `app/src/PinteMod.ControlCenter/Composition/ServerRuntimeContext.cs`.
- `app/src/PinteMod.ControlCenter/ViewModels/ControlCenterWorkspaceViewModel.cs`.
- `app/src/PinteMod.ControlCenter/ViewModels/PlayerActionsViewModelBase.cs`.
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs`.
- `app/src/PinteMod.ControlCenter/ViewModels/SettingsViewModel.cs`.
- `app/src/PinteMod.ControlCenter/App.xaml.cs`, `MainWindow.xaml`, `Views/SettingsView.xaml`.
- Tests de configuration, workspace, armes, cartes et cycle de vie sous `app/tests/PinteMod.ControlCenter.Tests/`.
- `docs/PINTEMOD_REQUIREMENTS_NEXT.md`, `docs/CODEX_PROGRESS.md`, `docs/TODO.md`, `docs/DECISIONS.md`.

### Compilation et tests intermédiaires

- Build Debug : 0 avertissement, 0 erreur.
- Tests menus ciblés : 26/26 réussis.
- Tests configuration/workspace/ViewModels ciblés : 106/106 réussis.
- Debug : 0 avertissement, 0 erreur, 394/394 tests réussis.
- Release : 0 avertissement, 0 erreur, 394/394 tests réussis.
- Le test DPAPI a été exécuté sous le profil Windows réel ; l’échec initial de l’environnement isolé ne se reproduit pas dans ce contexte conforme au produit.
- Preview autonome Windows x64 : 466 entrées, audit packaging `PASS`.
- ZIP final du lot : `app/artifacts/PinteMod-ControlCenter-post-RC2-multiserver-preview-2-win-x64.zip`.
- SHA-256 : `B66FDB258C2ACB865D6385FDC8B04ADFFA7976D3CAD40D08258568E461DDE3DC`.
- Exécutable à vérifier : `app/artifacts/post-rc2-multiserver-preview-2-win-x64/PinteMod.ControlCenter.exe`.
- Aucun serveur, processus BOIII ou commande RCON n’a été lancé pendant cette passe.

### Point nécessitant une décision humaine

- Confirmation reçue le 2026-08-13 : le mot de passe demandé est bien `g_password`, utilisé par les joueurs pour rejoindre, et non le secret RCON.
- Le dépôt PinteModReal audité ne fournit aucun contrat fermé pour modifier le nom public BOIII ou `g_password`. Aucun `set` libre ou commande supposée n’est ajouté. La prochaine action appartient à PinteMod : définir des commandes fermées, la confidentialité du transport et un feedback structuré avant activation réelle dans le Control Center.
- Prompt préparé : `docs/PROMPT_PINTE_MOD_SERVER_IDENTITY_CONTRACT.md`.

### Captures

- Aucune capture automatique produite ; la barre multi-serveurs, le renommage, l’ajout/retrait et la stabilité des trois listes doivent être vérifiés dans la preview locale ci-dessus.

### Hors périmètre maintenu

- Modération réelle à deux comptes volontairement non validée.
- ChangeMap, RestartMap, TriggerEvent et SpawnBoss restent simulés faute de contrats PinteMod fermés et observables.
- Aucune publication, fusion, création de tag ou modification de release GitHub effectuée pendant cette clôture.

## 2026-08-14 — Intégration ciblée des contrats PinteModReal e279a59 (automatisée terminée)

### Objectif de la passe

Intégrer sur la branche post-RC2 les quatre contrats Control Center v1 validés côté PinteModReal, sans modifier la RC2, sans commande libre et sans activer Change Map, événement générique ou SET mot de passe joueur.

### Réalisé

- ajout des modèles Core `capabilities`, `action_feedback`, `map_transition` et `server_identity` ;
- ajout des quatre chemins locaux explicitement autorisés et lecture via `VerifiedReadOnlyFile`/`ReadOnlyJsonFileReader` ;
- tailles maximales : capabilities 16 Kio, autres contrats 4 Kio ;
- lecture asynchrone hors UI, cache mémoire non autoritaire, invalidation par session et contrôle carte/session ;
- schémas JSON v1 embarqués dans l’application ;
- ajout des quatre commandes fermées : Restart Map, Spawn Boss, Set Hostname et Clear Join Password ;
- revalidation après confirmation de la session, carte, capability, alias boss et cible BOIII_XUID ;
- corrélation du résultat par `request_id`, nouvelle session de transition et révision d’identité ;
- une transition lente reste « non confirmée » et ne devient jamais automatiquement un échec ;
- Change Map reste Simulation, SET password reste désactivé « À venir », événement générique reste Simulation ;
- aucun XUID complet ajouté à une propriété publique de ViewModel.

### Validation automatisée finale

- compilation WPF Debug : 0 avertissement, 0 erreur ;
- tests ciblés Debug : 27/27 réussis ;
- suite complète Debug : 0 avertissement, 0 erreur, 413/413 tests réussis ;
- suite complète Release : 0 avertissement, 0 erreur, 413/413 tests réussis ;
- 10/10 fichiers XAML valides et 4/4 schémas JSON valides ;
- aucune occurrence de `ezzccmap`, `ezzccevent` ou `ezzccsetjoinpassword` dans les sources de production ;
- aucun binding vers un XUID complet et aucune commande libre ajoutée ;
- garanties read-only couvertes par tests avant/après sur les quatre sources contractuelles ;
- validation terrain : non exécutée ;
- aucun serveur, BAT ou EXE BOIII lancé.

### Problèmes rencontrés

- plusieurs processus `dotnet` de compilation sont restés bloqués sans sortie ; seuls ces processus de build ont été arrêtés, sans toucher au Control Center ni à BOIII.

### Validation humaine future

- compilation GSC et installation de la branche PinteModReal candidate sur la copie de test ;
- validation groupée Restart/Boss/Hostname/Clear Password après réussite automatisée finale.

### Fichiers de preuve

- rapport : `docs/CONTROL_CENTER_CONTRACTS_INTEGRATION_REPORT.md` ;
- prompt de revue : `docs/PROMPT_CHATGPT_CONTROL_CENTER_CONTRACTS_REVIEW.md` ;
- `UI_FEEDBACK.md` n’a pas été modifié.

## 2026-08-14 — Contre-revue ciblée : transport RCON incertain

### Objectif

Corriger l’unique blocage de revue : poursuivre l’observation des contrats locaux après toute action contractuelle potentiellement envoyée, même lorsque la réponse UDP se termine par `DeliveryUnknown` ou `TransportError`.

### Réalisé

- `ExecuteServerAdministrationCoreAsync` lance désormais l’observation locale dès que `CommandSent = true`, sans dépendre du statut de transport ;
- une exception non normalisée pendant une action contractuelle suit également le chemin conservateur d’observation, sans nouvel envoi ;
- une preuve fraîche et corrélée peut confirmer Restart Map, Spawn Boss, Set Hostname ou Clear Join Password malgré une réponse UDP perdue ;
- en l’absence de preuve, le résultat reste exactement `ENVOYÉ · NON CONFIRMÉ`, avertissement et verrou anti-répétition ;
- aucun retry RCON et aucune commande supplémentaire ;
- le tooltip utilise désormais « mot de passe joueur » sans exposer le nom technique de la dvar.

### Tests et compilation

- tests ViewModel ciblés : 13/13 réussis ;
- Debug : 0 avertissement, 0 erreur, 418/418 tests réussis ;
- Release : 0 avertissement, 0 erreur, 418/418 tests réussis ;
- cinq régressions ajoutées : Restart avec preuve, Restart sans preuve, Boss, Hostname et Clear Password après transport incertain ;
- aucun serveur, BAT, EXE BOIII ou RCON réel lancé ;
- validation terrain toujours suspendue jusqu’au verdict de contre-revue.

### Verdict indépendant

- verdict ChatGPT reçu le 2026-08-14 : **VALIDÉ** ;
- aucun blocage obligatoire restant ;
- observation après `DeliveryUnknown`/`TransportError`, absence de retry et verrou sans preuve confirmés ;
- autorisation accordée pour l’unique validation terrain groupée, exclusivement sur la copie de test et après compilation GSC réussie.

### Candidate terrain préparée

- publication Windows x64 autonome, non exécutée ;
- révision embarquée : `e27e0b55bd0757f893310d78d1ff89df3bce94a7` ;
- version produit : `2.2.0-post-rc2-contracts.e27e0b5` ;
- exécutable : `app/artifacts/post-rc2-contracts-terrain-e27e0b5-win-x64/PinteMod.ControlCenter.exe` ;
- ZIP : `app/artifacts/PinteMod-ControlCenter-post-RC2-contracts-terrain-e27e0b5-win-x64.zip` ;
- audit packaging : PASS, 471 entrées, 0 PDB, 4 schémas contractuels ;
- SHA-256 : `C7B8933B27A8D9EBE0DFCDAA1F53C4D155BAAF4731F44B753E6B7C477CE6F92A`.

## 2026-08-14 — Préparation isolée de la copie Server3 de test

### État constaté

- l’ancienne copie `UnrankedServer` était obsolète ; elle a été remplacée humainement par une copie récente sous `<COPIE_SERVEUR_TEST>` ;
- comparaison avec PinteModReal `e279a59` : 32 GSC identiques, `ezz_admin_events.gsc` à mettre à jour, module contrats absent et `ezz_admin_music.gsc` spécifique à Server3 ;
- aucune modification du Server3 de production.

### Préparation effectuée

- sauvegarde récupérable : `<SAUVEGARDE_SERVEUR_TEST>` ;
- remplacement ciblé de `ezz_admin_events.gsc` par la candidate validée ;
- ajout de `ezz_admin_control_center_contracts.gsc` ;
- conservation de la version Server3 de `ezz_admin_music.gsc` ;
- `sv_lanonly=1` dans `server.cfg` et `server_zm.cfg` ;
- port de test isolé `27121` dans `Server.bat`, confirmé libre au moment de la préparation ;
- 5/5 imports du module contrats présents, 35 GSC au total ;
- aucun secret lu, aucun port entrant ajouté et aucun BAT, EXE, serveur ou RCON lancé.

### Validation humaine suivante

- lancer manuellement la copie avec son `Server.bat` ;
- confirmer la compilation GSC et la ligne `Control Center Contracts v0.1.1 loaded` ;
- arrêter immédiatement en cas d’`unresolved external` ou d’erreur GSC ;
- seulement après cette preuve, connecter la candidate Control Center à `127.0.0.1:27121` et effectuer la validation groupée.

### Résultat du premier démarrage

- confirmation humaine reçue : la copie de test a chargé sans erreur signalée ;
- le module `Control Center Contracts v0.1.1` est considéré comme compilé/chargé sur la copie isolée ;
- aucune mutation Control Center n’a encore été exécutée ;
- prochaine barrière : connexion locale `127.0.0.1:27121`, lecture des quatre contrats et capture avant toute action.

### Validation read-only du Control Center

- capture humaine : `<CAPTURE_OPÉRATEUR_LOCALE>/lecture-hybride.png` ;
- onglet `Serveur 2` en mode hybride local ;
- session locale fraîche, carte `zm_castle`, manche/runtime joueur observés ;
- heartbeat PinteMod frais et connecté ;
- Supervisor arrêté et autres services périmés/inconnus, comportement attendu avec le lancement isolé par `Server.bat` ;
- bannière conforme : seules Change Map, événements génériques et définition du mot de passe restent simulés ;
- aucune commande RCON ou mutation exécutée à ce stade.

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs` ;
- `app/src/PinteMod.ControlCenter/Views/ServerView.xaml` ;
- `app/tests/PinteMod.ControlCenter.Tests/ControlCenterContractViewModelTests.cs` ;
- documents de suivi et prompt de contre-revue.

## 2026-08-14 — Validation terrain du transport RCON local

### Objectif

- valider le canal RCON de la copie Server3 isolée avant les quatre mutations contractuelles groupées.

### Résultat

- profil actif : `Serveur 2`, racine locale `<COPIE_SERVEUR_TEST>` ;
- serveur BOIII observé en écoute UDP locale sur `27121` ;
- configuration opérateur corrigée sur `127.0.0.1:27121` ;
- secret DPAPI présent, sans lecture ni affichage de sa valeur ;
- `ezzhealth full` envoyé manuellement avec succès ;
- BOIII n'a pas transporté les 51 contrôles dans sa réponse UDP, comportement déjà prévu par le fallback ;
- preuve locale fraîche utilisée sans inventer la sortie console : `PinteMod : SAIN` ;
- aucun retry automatique, aucune mutation serveur et aucune écriture PinteMod effectués pendant ce diagnostic.

### Validation suivante

- exécuter en une seule passe terrain les quatre actions contractuelles autorisées : Restart Map, Spawn Boss, Set Hostname et Clear Join Password ;
- conserver Change Map, événements génériques et définition du mot de passe joueur en simulation.

## 2026-08-14 — Correctif terrain des types JSON contractuels BOIII

### Problème observé

- `server_identity.json` et `control_center_capabilities.json` étaient présents, frais et liés à la session active, mais l'interface les classait invalides ;
- BOIII sérialise les chaînes numériques et booléennes passées à `jsonset` sous forme de nombres et booléens JSON natifs ;
- les quatre schémas embarqués exigeaient à tort ces scalaires entre guillemets.

### Correction

- alignement strict du lecteur sur les types réellement émis : entiers JSON bornés et booléens JSON natifs ;
- alignement des quatre schémas Draft 2020-12 ;
- conservation des objets fermés, listes blanches, bornes, corrélations de session et refus des scalaires cités ;
- aucune modification du transport RCON, des commandes, des GSC de la copie de test ou de PinteMod en cours d'exécution.

### Validation

- tests ciblés lecteur/contrats : 8/8 réussis ;
- Debug : 0 avertissement, 0 erreur, 419/419 tests réussis ;
- Release : 0 avertissement, 0 erreur, 419/419 tests réussis ;
- test DPAPI rejoué avec succès dans le profil Windows normal ;
- validation terrain des quatre actions suspendue jusqu'au lancement de la nouvelle candidate.

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter.Infrastructure/Local/ControlCenterContractReader.cs` ;
- `app/contracts/control-center/v1/*.schema.json` ;
- `app/tests/PinteMod.ControlCenter.Tests/ControlCenterContractReaderTests.cs` ;
- `app/tests/PinteMod.ControlCenter.Tests/ControlCenterContractSchemaTests.cs` ;
- documents de suivi.

### Nouvelle candidate terrain

- commit : `3bf033ccc3371a054b757e4a5e86c726f255e9e6` ;
- version embarquée : `2.2.0-post-rc2-contracts.3bf033c` ;
- exécutable : `app/artifacts/post-rc2-contracts-terrain-3bf033c-win-x64/PinteMod.ControlCenter.exe` ;
- ZIP : `app/artifacts/PinteMod-ControlCenter-post-RC2-contracts-terrain-3bf033c-win-x64.zip` ;
- audit packaging : PASS, 471 entrées ;
- SHA-256 : `35EBBE5723D5CE20387A551D467078EC13E89E697F9DBDA7A9CFCE0FB607FFDB` ;
- révision et version vérifiées dans l'assembly publié.

## 2026-08-14 — Identité persistante, mot de passe loopback et barre de déplacement

### Objectif

- corriger le nom public perdu au redémarrage, rendre possible la définition sûre de `g_password` sur la machine serveur et séparer les onglets de la zone servant à déplacer la fenêtre.

### Réalisé

- ajout d’une barre de déplacement dédiée de 34 px au-dessus des onglets serveurs ; les onglets et boutons restent entièrement interactifs dans une seconde rangée ;
- contrat PinteMod v0.1.2 : le hostname public est persisté dans `pintemod/config/control_center_identity.json`, qui ne contient aucun secret, puis restauré au chargement ;
- ajout de `ezzccsetjoinpassword` avec alphabet fermé de 4 à 32 caractères, feedback corrélé et observation uniquement du booléen `join_password_enabled` ;
- le Control Center n’autorise cette mutation qu’avec une adresse RCON loopback ; le mode LAN ne peut ni afficher ni transmettre la valeur ;
- `PasswordBox` non bindé, effacé avant l’attente du résultat ; aucune valeur dans les modèles, l’activité opérateur, le feedback ou la configuration ;
- le mot de passe reste éphémère et n’est jamais persisté par PinteMod ou le Control Center ;
- copie `<COPIE_SERVEUR_TEST>` mise à jour sans lancement de serveur, avec sauvegarde `.pre-v0.1.2.bak` ; production inchangée.

### Fichiers et dépôts

- Control Center : Core, Infrastructure, WPF, schémas, tests et documents de suivi ;
- PinteModReal branche `codex/pintemod-contracts-3-7`, commit local `7da248d` ;
- `UI_FEEDBACK.md` inchangé.

### Validation automatique

- tests PinteModReal : 39/39 PASS ;
- Debug Control Center : 0 avertissement, 0 erreur, 438/438 tests ;
- Release Control Center : 0 avertissement, 0 erreur, 438/438 tests ;
- aucun serveur, BAT ou EXE BOIII lancé ; aucun retry ou commande RCON libre ajouté.

### Validation humaine requise

- démarrer manuellement la copie de test et confirmer `Control Center Contracts v0.1.2 loaded` ;
- vérifier le déplacement de fenêtre depuis la nouvelle barre ;
- tester le hostname, redémarrer le processus de test et confirmer sa restauration dans l’identité locale ;
- utiliser un mot de passe synthétique unique sur loopback, vérifier la connexion puis rechercher cette chaîne dans les sorties/logs avant toute autorisation de publication.

### Candidate terrain produite

- commit applicatif embarqué : `3d624fa3b09490d005b3cf65ad24ef081a8a7da5` ;
- version produit : `2.2.0-rc.2+3d624fa3b09490d005b3cf65ad24ef081a8a7da5` ;
- exécutable : `app/artifacts/post-rc2-identity-v012-3d624fa-win-x64/PinteMod.ControlCenter.exe` ;
- ZIP : `app/artifacts/PinteMod-ControlCenter-post-RC2-identity-v0.1.2-3d624fa-win-x64.zip` ;
- audit packaging : PASS, 471 entrées, aucun PDB ;
- SHA-256 : `C8D9B56E0307FAB614F75DD4D07D11FDA4114FBE078D8DB3D036AF494D42FB8A`.

## 2026-08-14 — Correctif terrain du nom public BOIII et codes couleur

### Diagnostic read-only

- `control_center_identity.json` contient bien le nom public `Test` ;
- `server_identity.json` confirme `public_hostname=Test`, `join_password_enabled=true`, révision 2 ;
- le feedback corrélé confirme `set_join_password / applied / success` ;
- le CFG de test déclare le nom public avec `live_steam_server_name`, et non `sv_hostname` ; les deux boutons avaient donc fonctionné côté contrat, mais le premier GSC modifiait la mauvaise autorité d’affichage BOIII.

### Correction

- contrat PinteMod v0.1.3 : `live_steam_server_name` devient l’autorité du nom public, avec mise à jour parallèle de `sv_hostname` pour compatibilité ;
- persistance/restauration du même nom public non sensible ;
- grammaire fermée étendue uniquement aux codes couleur BOIII `^0` à `^9` ; toute séquence `^` invalide reste refusée ;
- aide compacte des dix couleurs ajoutée sous le champ nom ;
- état mot de passe clarifié : actif pour les nouvelles connexions, jamais pour une session joueur déjà connectée.

### Validation

- PinteModReal : 39/39 PASS, commit local `b4d5b11` ;
- Control Center Debug : 0 avertissement, 0 erreur, 445/445 tests ;
- Control Center Release : 0 avertissement, 0 erreur, 445/445 tests ;
- GSC v0.1.3 préparé dans la copie `servtest\Server3` avec sauvegarde, sans arrêter ni relancer le processus BOIII en cours ; il prendra effet au prochain redémarrage manuel.

### Candidate v0.1.3

- commit applicatif : `7bdb22fbcc1a69b4768bb59afaf3bb72295f2004` ;
- exécutable : `app/artifacts/post-rc2-identity-v013-7bdb22f-win-x64/PinteMod.ControlCenter.exe` ;
- ZIP : `app/artifacts/PinteMod-ControlCenter-post-RC2-identity-v0.1.3-7bdb22f-win-x64.zip` ;
- audit packaging : PASS, 471 entrées ;
- SHA-256 : `E7513B783C900B451F58E8E01F4B34EF0355DADB0BC2B2FB2C80AF881A073F46`.

## 2026-08-14 — Correctif fonctionnel du mot de passe BOIII et détails repliables

### Objectif

- remplacer la fausse autorité `g_password` par le mécanisme réellement vérifié par Ezz BOIII et alléger les cartes Dashboard/Records sans supprimer d’information.

### Réalisé

- test humain négatif confirmé : un client vierge rejoint malgré `g_password` actif ;
- audit du code public Ezz BOIII : la connexion directe compare `net_password` via le hash publié dans `getInfo` ;
- contrat PinteMod candidat v0.1.4 corrigé pour définir, effacer et observer uniquement `net_password` ;
- tests de contrat renforcés pour interdire tout retour à `g_password` dans le module ;
- libellés UI corrigés en « mot de passe réseau BOIII » ;
- détails Déclaré/Lecture/Fraîcheur/Provenance repliés par défaut sous une flèche accessible ;
- XUID abrégé des profils Ranks replié de la même manière ;
- préfixe `M` supprimé devant la meilleure manche ;
- `UI_FEEDBACK.md` inchangé.

### Fichiers créés ou modifiés à ce stade

- WPF : thème, Dashboard, Records, Serveur et `ServerViewModel` ;
- tests Control Center : `ViewModelTests.cs` ;
- PinteModReal candidat : GSC contrats, test PowerShell et documentation du contrat ;
- suivi : `CODEX_PROGRESS.md`, `TODO.md`, `DECISIONS.md`.

### Validation intermédiaire

- tests contrats PinteModReal : 39/39 PASS ;
- tests WPF ciblés : 3/3 PASS ;
- Debug Control Center : 0 avertissement, 0 erreur, 447/447 tests ;
- Release Control Center : 0 avertissement, 0 erreur, 447/447 tests ;
- aucun serveur, BAT ou exécutable BOIII lancé ; aucune valeur sensible utilisée par le correctif, journalisée ou ajoutée au dépôt.

### Validation humaine restante

- après déploiement sur la copie de test et redémarrage manuel : vérifier `net_password` vide/refus, correct/accepté et incorrect/refusé ;
- rechercher une valeur synthétique unique dans les sorties et fichiers de test ;
- contrôler visuellement les cartes repliées sur Dashboard et Records.

### Livraison candidate

- commit PinteModReal : `fdb9a55` sur `codex/pintemod-contracts-3-7` ;
- commit Control Center : `e9be7ca` sur `codex/post-rc2-runtime-contracts` ;
- GSC v0.1.4 préparé dans `<COPIE_SERVEUR_TEST>\boiii\custom_scripts`, hash identique à la source validée ;
- exécutable : `app/artifacts/post-rc2-net-password-v014-e9be7ca-win-x64/PinteMod.ControlCenter.exe` ;
- ZIP : `app/artifacts/PinteMod-ControlCenter-post-RC2-net-password-v0.1.4-e9be7ca-win-x64.zip` ;
- audit packaging : PASS, 471 entrées ;
- SHA-256 : `F77687A7E35B1DCAB0C58BA6C453D37B7A5DEA34E8A19CB9484EE87CC8A150AC`.

### Ajustement visuel ciblé après retour humain

- capacités locales v0.1.4 vérifiées fraîches et cohérentes : Nom et Mot de passe sont autorisés côté serveur ;
- le bouton Nom reste volontairement inactif lorsque le champ contient déjà le nom observé ; une aide explicite l’indique désormais ;
- champs Nom et Mot de passe rendus visuellement actifs par un fond relevé et une bordure bleue ;
- `DÉTAILS` et `IDENTIFIANT` réduits à 8 px, opacité 72 %, flèche 9 px et accent seulement au survol/focus ;
- tests ciblés : 3/3 PASS ;
- Debug : 0 avertissement, 0 erreur, 447/447 tests ;
- Release : 0 avertissement, 0 erreur, 447/447 tests ;
- aucune modification supplémentaire de PinteMod ou de la copie serveur.
- commit applicatif de l’ajustement : `45599e5` ;
- exécutable actualisé : `app/artifacts/post-rc2-net-password-v014-ui-45599e5-win-x64/PinteMod.ControlCenter.exe` ;
- ZIP actualisé : `app/artifacts/PinteMod-ControlCenter-post-RC2-net-password-v0.1.4-ui-45599e5-win-x64.zip` ;
- audit packaging : PASS, 471 entrées ; SHA-256 `0F8E1547914B357318BBA47996505F3D47A51A8B8158A38EF67626E9AA0F5CFA`.

## 2026-08-14 — Compatibilité du lecteur avec le contrat Control Center v0.1.4

### Objectif

- corriger les boutons Nom et Mot de passe restant désactivés malgré un contrat PinteMod v0.1.4 frais et cohérent.

### Diagnostic et correction

- la copie Server3 publiait correctement `contract_module_version=0.1.4`, `set_hostname=true`, `set_join_password=true` et `join_password_transport=loopback_rcon_ephemeral` ;
- le lecteur applicatif exigeait encore exactement la version v0.1.3 et rejetait donc uniquement les capacités, tandis que l’identité v1 restait lisible ;
- le lecteur et le schéma embarqué acceptent désormais explicitement v0.1.3 et v0.1.4, conservent la version réellement observée et refusent toujours toute version inconnue ;
- le bandeau Serveur n’annonce plus statiquement « aucun transport RCON » : il reflète la configuration réelle et rappelle les fonctions qui restent simulées ;
- aucun secret, serveur, fichier PinteMod ou GSC n’a été lu ou modifié pendant ce correctif ; `UI_FEEDBACK.md` reste inchangé.

### Fichiers modifiés

- `app/src/PinteMod.ControlCenter.Infrastructure/Local/ControlCenterContractReader.cs` ;
- `app/contracts/control-center/v1/control_center_capabilities.schema.json` ;
- `app/src/PinteMod.ControlCenter/ViewModels/ServerViewModel.cs` ;
- `app/src/PinteMod.ControlCenter/Views/ServerView.xaml` ;
- `app/tests/PinteMod.ControlCenter.Tests/ControlCenterContractReaderTests.cs` ;
- `app/tests/PinteMod.ControlCenter.Tests/ViewModelTests.cs`.

### Validation

- tests ciblés : 105/105 réussis ;
- Debug : 0 avertissement, 0 erreur, 449/449 tests réussis ;
- Release : 0 avertissement, 0 erreur, 449/449 tests réussis ;
- commit applicatif local : `52ff04e` ;
- exécutable : `app/artifacts/post-rc2-contract-v014-52ff04e-win-x64/PinteMod.ControlCenter.exe` ;
- ZIP : `app/artifacts/PinteMod-ControlCenter-post-RC2-contract-v0.1.4-52ff04e-win-x64.zip` ;
- audit packaging : PASS, 471 entrées ;
- SHA-256 : `3698BE0A9B4E0458B82621249B69F403479C93886519869CD4BCDEAD40063B1B`.

### Validation humaine restante

- fermer l’ancienne candidate, lancer l’exécutable `52ff04e`, charger le profil Server3 puis vérifier que le nom différent active « APPLIQUER LE NOM » ;
- le bouton mot de passe ne doit être disponible que sur la machine serveur avec un endpoint RCON loopback (`127.0.0.1`).

### Amélioration de compatibilité avant validation finale

- `contract_module_version` n’est plus utilisé comme verrou de compatibilité fonctionnelle : il reste obligatoire, borné, au format sémantique `x.y.z` et affiché comme provenance ;
- la compatibilité réelle reste strictement contrôlée par `schema_version=1`, `command_contract_version=1`, les objets JSON fermés et chaque capacité booléenne explicite ;
- une future version de module conservant exactement ces contrats reste donc utilisable sans nouvelle version du Control Center ;
- toute forme, commande, propriété ou type incompatible reste refusé ;
- tests ciblés : 12/12 réussis ;
- Debug : 0 avertissement, 0 erreur, 450/450 tests réussis ;
- Release : 0 avertissement, 0 erreur, 450/450 tests réussis ;
- commit applicatif local : `b135e7a` ;
- exécutable final de cette passe : `app/artifacts/post-rc2-contract-compatible-b135e7a-win-x64/PinteMod.ControlCenter.exe` ;
- ZIP : `app/artifacts/PinteMod-ControlCenter-post-RC2-contract-compatible-b135e7a-win-x64.zip` ;
- audit packaging : PASS, 471 entrées ;
- SHA-256 : `728691D794A0BF8D2917458491A9240A109EC5754235AF498E2856B532606B60`.

## 2026-08-14 — Personnalisation par serveur et éditeur de nom BOIII coloré

### Objectif

- permettre une identité visuelle distincte pour chaque onglet serveur et rendre les codes couleur BOIII accessibles sans saisie manuelle obligatoire.

### Réalisé

- six palettes d’accent fermées : Bleu PinteMod, Cyan électrique, Indigo, Violet, Rose néon et Turquoise ;
- couleur enregistrée dans la configuration locale isolée de chaque profil serveur et appliquée automatiquement lors du changement d’onglet ;
- indicateur coloré discret ajouté dans chaque onglet ;
- ressources d’accent WPF converties en ressources dynamiques ; les couleurs sémantiques sain/avertissement/danger restent fixes ;
- bouton séparé `ENREGISTRER L’APPARENCE` : il persiste uniquement le nom de l’onglet et sa couleur, sans enregistrer une source ou une cible RCON non vérifiée ;
- éditeur de hostname BOIII dédié avec palette `^0` à `^9`, insertion à la position du curseur, application à une sélection et restauration de la couleur précédente ;
- aperçu coloré en direct dans le même bloc d’édition ;
- limite contractuelle de 64 caractères bruts conservée, codes couleur compris ;
- aucune nouvelle commande, aucun texte RCON libre et aucune modification PinteMod/GSC ;
- `UI_FEEDBACK.md` inchangé.

### Fichiers principaux créés ou modifiés

- Core : `BoiiiColorText.cs`, `OperatorDataSourceModels.cs` ;
- Infrastructure : `JsonOperatorConfigurationStore.cs` ;
- WPF : `AccentThemeService.cs`, `BoiiiHostnameEditor.xaml/.cs`, `App.xaml.cs`, workspace/settings ViewModels, MainWindow, thème et vues ;
- tests : parser couleur, configuration opérateur, Settings, workspace, XAML et rendu WPF.

### Validation

- tests ciblés : 113/113 réussis ;
- Debug : 0 avertissement, 0 erreur, 460/460 tests réussis ;
- Release : 0 avertissement, 0 erreur, 460/460 tests réussis ;
- commit applicatif local : `6358d94` ;
- exécutable : `app/artifacts/post-rc2-personalization-6358d94-win-x64/PinteMod.ControlCenter.exe` ;
- ZIP : `app/artifacts/PinteMod-ControlCenter-post-RC2-personalization-6358d94-win-x64.zip` ;
- audit packaging : PASS, 471 entrées ;
- SHA-256 : `7A459BE69CF0CBB6F3C36CCF337248DD8A3B840FCA46DEA7F92BE7CA7812A970` ;
- aucune capture produite automatiquement.

### Validation humaine restante

- attribuer deux couleurs différentes à deux onglets, enregistrer l’apparence, basculer entre eux puis redémarrer le Control Center ;
- saisir un nom avec plusieurs couleurs, tester la coloration d’une sélection et comparer l’aperçu indicatif au rendu BOIII ;
- contrôler la carte Serveur dans une fenêtre réduite avant publication stable.

### Verdict humain

- personnalisation par onglet, persistance des couleurs et éditeur de hostname multicolore validés le 2026-08-14 ;
- aucun correctif graphique supplémentaire demandé ;
- prochain et dernier verrou fonctionnel : validation terrain et confidentialité de `net_password`, puis gel du code et audit de publication stable.

## 2026-08-16 — Validation terrain finale de `net_password`

### Objectif

- vérifier sur la copie serveur de test que le mécanisme réellement utilisé par Ezz BOIII protège les connexions directes avant le gel de la candidate stable.

### Réalisé

- `net_password` absent côté client : connexion refusée ;
- `net_password` incorrect : connexion refusée ;
- `net_password` correct : connexion acceptée ;
- test effectué avec une valeur synthétique non communiquée et non ajoutée aux sources, contrats, journaux ou documents ;
- aucun serveur, fichier PinteMod, GSC ou secret n’a été modifié par Codex pendant cette validation humaine ;
- `UI_FEEDBACK.md` reste inchangé.

### État

- dernier verrou fonctionnel terrain levé ;
- code fonctionnel gelé ;
- métadonnée applicative passée de `2.2.0-rc.2` à `2.2.0` et mentions visuelles « Prototype » retirées ;
- Debug : 0 avertissement, 0 erreur et 460/460 tests réussis ;
- Release : 0 avertissement, 0 erreur et 460/460 tests réussis ;
- publication Windows x64 autonome terminée ;
- version embarquée : `2.2.0+8653210f3f90bf5a5f5140a35857aa9b7522c9aa` ;
- audit packaging : PASS, 471 entrées ZIP et aucun PDB ;
- SHA-256 : `C69E28110DE53DF4CCF93D9E46E87D2197D3BE6B815A6C43B35786F3F2CEE74D` ;
- paquet unique de revue ChatGPT : `app/artifacts/PinteMod-ControlCenter-v2.2.0-stable-review-8653210.zip` ;
- SHA-256 du paquet de revue : `9585BD18C066A6C081C2EA2502E1EA0A4E6987728E6B12F09363B75D636883A6` ;
- aucune publication GitHub effectuée.

### Fichiers créés ou modifiés à ce stade

- `app/src/PinteMod.ControlCenter/PinteMod.ControlCenter.csproj` ;
- `app/src/PinteMod.ControlCenter/MainWindow.xaml` ;
- `app/src/PinteMod.ControlCenter/Views/SettingsView.xaml` ;
- `app/README.md` ;
- `app/packaging/LISEZ-MOI.txt` ;
- `docs/CODEX_PROGRESS.md` ;
- `docs/TODO.md` ;
- `docs/DECISIONS.md`.
- `docs/FINAL_STABLE_VALIDATION.md` ;
- `docs/PROMPT_REVUE_CHATGPT_V2.2.0_STABLE.md`.

### Validation humaine restante

- transmettre le paquet de revue stable à ChatGPT avant toute publication GitHub ;
- aucune nouvelle manipulation serveur n’est requise à ce stade.

## 2026-08-16 — Corrections finales de métadonnées et neutralisation

### Objectif

- lever les deux blocages documentaires de la revue stable sans modifier le comportement applicatif.

### Réalisé

- `README.md`, `README_FR.md`, `app/README.md` et `LISEZ-MOI.txt` annoncent désormais sans ambiguïté la version stable `2.2.0` et 460/460 tests ;
- suppression des mentions de candidate/RC2 en attente dans les surfaces distribuées ;
- lien de téléchargement préparé pour le tag stable `v2.2.0` ;
- remplacement de l’adresse terrain du fixture par l’adresse de documentation réservée `198.51.100.42` ;
- neutralisation des chemins de captures, copie serveur et sauvegarde dans les documents de suivi ;
- exemple UNC remplacé par `\\serveur-exemple\PinteModData` ;
- aucun code métier, contrat, commande RCON, lecteur local ou GSC modifié ;
- `UI_FEEDBACK.md` reste inchangé.

### Validation

- tests ciblés : 12/12 réussis ;
- Debug : 0 avertissement, 0 erreur et 460/460 tests réussis ;
- Release : 0 avertissement, 0 erreur et 460/460 tests réussis ;
- scans ciblés : aucune ancienne mention RC2/292 tests dans les documents distribués, aucune adresse ou chemin terrain signalé restant dans le périmètre corrigé ;
- commit applicatif stable : `25e0e16b6883d77ea1e0ad91caa866aa78d25173` ;
- ProductVersion : `2.2.0+25e0e16b6883d77ea1e0ad91caa866aa78d25173` ;
- ZIP stable : `app/artifacts/PinteMod-ControlCenter-v2.2.0-win-x64.zip` ;
- audit packaging : PASS, 471 entrées et aucun PDB ;
- SHA-256 : `B3C0368DD662C2C04B41F04ECA4D9FBC19A19CE998C86CD5923B6EE793A080A0` ;
- paquet d’ultime contre-revue : `app/artifacts/PinteMod-ControlCenter-v2.2.0-final-review-25e0e16.zip` ;
- SHA-256 du paquet de contre-revue : `6E768490EB449D322D98439EFC6E58B9B42E3F48711A5C201CEF2DBF1AE1D30C` ;
- aucune publication GitHub effectuée.

## 2026-08-16 — Verdict final stable

- verdict ChatGPT : `VALIDÉ` ;
- blocages obligatoires : aucun ;
- commit applicatif autorisé : `25e0e16b6883d77ea1e0ad91caa866aa78d25173` ;
- ProductVersion confirmé : `2.2.0+25e0e16b6883d77ea1e0ad91caa866aa78d25173` ;
- ZIP stable confirmé : `PinteMod-ControlCenter-v2.2.0-win-x64.zip` ;
- SHA-256 confirmé : `B3C0368DD662C2C04B41F04ECA4D9FBC19A19CE998C86CD5923B6EE793A080A0` ;
- publication GitHub `v2.2.0` autorisée par la revue, mais non exécutée faute d’ordre opérateur explicite ;
- aucune nouvelle correction ni validation terrain demandée ;
- remarque facultative seulement : harmonisation future de quelques descriptions README concernant Restart Map/Boss.

## 2026-08-16 — Publication publique v2.2.0

### Objectif

- publier la version stable validée, rendre le dépôt Control Center public et présenter officiellement l’application depuis le dépôt PinteMod.

### Réalisé

- harmonisation des README Control Center français/anglais avec les fonctions réellement disponibles : multi-serveurs, personnalisation, Restart Map, boss compatibles, hostname public et mot de passe BOIII éphémère ;
- correction des passages historiques correspondants dans `app/README.md` ;
- ajout de notes de version stables réutilisables dans `docs/RELEASE_NOTES_v2.2.0.md` ;
- fusion de la branche stable dans `main` via la PR Control Center `#4`, merge commit `7f5e8f34163b282e4c98681da606b27c768798ea` ;
- dépôt `BiereFraiche/PinteModControlCenter` rendu public ;
- tag `v2.2.0` fixé sur le commit applicatif validé `25e0e16b6883d77ea1e0ad91caa866aa78d25173` ;
- release stable publique créée avec uniquement le ZIP Windows x64 et son fichier `.sha256` ;
- mise en avant bilingue du Control Center ajoutée aux README de `BiereFraiche/PinteMod` via la PR `#1`, fusionnée au commit `7587764d15d408fe44a5e67642b67a4df20b8722` ;
- aucun GSC, fichier serveur, contrat runtime, commande ou comportement applicatif modifié ;
- `UI_FEEDBACK.md` reste inchangé.

### Preuves publiques

- dépôt : `https://github.com/BiereFraiche/PinteModControlCenter` ;
- release : `https://github.com/BiereFraiche/PinteModControlCenter/releases/tag/v2.2.0` ;
- asset : `PinteMod-ControlCenter-v2.2.0-win-x64.zip`, 70 145 250 octets ;
- SHA-256 GitHub et local : `B3C0368DD662C2C04B41F04ECA4D9FBC19A19CE998C86CD5923B6EE793A080A0` ;
- fichier d’empreinte : 108 octets, téléversé séparément ;
- release non brouillon, non prerelease et marquée comme version stable actuelle ;
- README anglais et français vérifiés sur les branches `main` publiques des deux dépôts.

### Validation

- aucune recompilation ni réexécution fonctionnelle : cette passe modifie uniquement la documentation et la publication ;
- baseline inchangée : Debug et Release, 0 avertissement, 0 erreur, 460/460 tests ;
- paquet publié strictement identique au ZIP final déjà audité et validé ;
- audit précédent conservé : PASS, 471 entrées, aucun PDB, secret, configuration opérateur ou fichier runtime ;
- aucun blocage connu restant et aucune validation humaine supplémentaire demandée.

### Fichiers créés ou modifiés

- `README.md` ;
- `README_FR.md` ;
- `app/README.md` ;
- `docs/RELEASE_NOTES_v2.2.0.md` ;
- `docs/CODEX_PROGRESS.md` ;
- `docs/TODO.md` ;
- `docs/DECISIONS.md`.

### Captures

- la référence publique existante `design/pintemod-control-center-reference.png` est affichée dans les README ;
- aucune nouvelle capture nécessaire pour cette passe documentaire.

## 2026-08-16 — Présentation publique de la vision v2.3

### Objectif

- annoncer la suite du projet de façon attractive et transparente, sans présenter la roadmap comme une fonctionnalité déjà livrée.

### Réalisé

- lecture intégrale du cadrage « v2.3 Adaptive BOIII Core / Capability Engine » transmis par l’opérateur ;
- ajout d’un teaser français et anglais dans les README racine ;
- création de `docs/V2.3_VISION.md`, page publique bilingue présentant le Capability Engine, les niveaux Simulation/BOIII/GSC/Bridge/PinteMod, l’analyse GSC read-only et la roadmap progressive ;
- séparation explicite entre la stable v2.2.0 immuable et le futur chantier v2.3 ;
- maintien des garanties publiques : BOIII Zombies uniquement, aucun plugin, raw RCON, cloud, port entrant, découverte automatique ou modification de GSC tiers ;
- aucune modification de code, binaire, contrat, commande, package stable, tag ou release ;
- `UI_FEEDBACK.md` reste inchangé.

### Fichiers créés ou modifiés

- `README.md` ;
- `README_FR.md` ;
- `docs/V2.3_VISION.md` ;
- `docs/CODEX_PROGRESS.md` ;
- `docs/TODO.md` ;
- `docs/DECISIONS.md`.

### Validation

- passe exclusivement documentaire : aucune compilation ni suite de tests relancée ;
- baseline produit inchangée : Debug/Release, 0 avertissement, 0 erreur et 460/460 tests ;
- aucun blocage rencontré ;
- aucune validation terrain ou capture nécessaire.

## 2026-08-28 — Integration Preview 4B1 Fix15 préparée

### Objectif

- reprendre le candidat Fix14, corriger les écarts fail-closed et préparer une Preview testable et publiable sans la promouvoir en stable.

### Réalisé

- branche locale `codex/integration-preview-4b1-fix15` issue de la dernière référence `origin/main` ;
- import du candidat Fix14 sans toucher à `server-sandbox/` ;
- détection PinteMod durcie par empreintes SHA-256 first-party connues ;
- fichiers portant seulement un nom PinteMod classés comme tiers et maintenus sans transport de commandes ;
- capacité lifecycle indisponible sans lanceur prouvé ;
- version Fix15 et métadonnées Windows alignées ;
- workflow de build étendu à un EXE unique, un dossier autonome et son ZIP ;
- CI GitHub préparée pour les deux formats et leur audit ;
- documentation VM fondée sur une console/RDP/VPN existante, sans listener ou port applicatif ;
- chemins absolus de poste retirés des documents actifs concernés ;
- aucune opération GitHub distante effectuée.

### Validation automatisée

- Restore réussi ;
- builds Debug et Release : 0 avertissement, 0 erreur ;
- tests Debug et Release : 586/586 réussis ;
- EXE unique et dossier autonome publiés ;
- audits des ZIP mono-EXE et dossier réussis ;
- version fichier `2.4.0.15` et version produit Fix15 confirmées.

### Validation restante

- validation humaine Server3 ;
- scénario Agent bidirectionnel entre deux PC ;
- push, PR et prerelease uniquement sur ordre explicite.
