# PinteMod Control Center v2.2

Application Windows en C# / .NET 8 / WPF pour observer et administrer une session PinteMod depuis la machine serveur ou un poste du même LAN. Le mode simulé reste le comportement initial. Les données PinteMod sont lues en read-only ; les diagnostics et actions RCON autorisés sont exclusivement manuels et confirmés selon leur niveau de risque.

## Sécurité de cette version

- aucun serveur web, port entrant, broadcast ou découverte réseau ;
- un client UDP sortant isolé, limité à des listes blanches typées de diagnostics et d’actions RCON ;
- le test LAN utilise uniquement un chemin UNC fourni explicitement par l’opérateur ;
- aucune écriture directe du Control Center dans PinteMod ; les actions de modération et de rôle confirmées demandent à PinteMod d’appliquer ses propres écritures administratives validées ;
- aucune recherche automatique d’installation ;
- aucun accès automatique à `server-sandbox/` ;
- le secret RCON du Control Center est saisi explicitement, protégé par DPAPI pour l’utilisateur Windows courant et jamais réaffiché ;
- le secret DPAPI existant des outils PowerShell n’est jamais recherché ni réutilisé automatiquement ;
- actions joueur ciblées par XUID validé, jamais par pseudo ;
- en simulation, chaque action retourne toujours `CommandSent = false` ; en mode hybride, seules les actions explicitement listées peuvent atteindre le transport après confirmation.
- les lecteurs hybrides utilisent uniquement `FileAccess.Read` et une liste blanche de chemins confinés sous `ServerRoot` ;
- les fichiers `.tmp` et `.bak` ne deviennent jamais des sources actives.
- les logs sont filtrés avant présentation : XUID, IP, GUID, chemins et champs sensibles ne sont jamais affichés en clair.

## Prérequis

- Windows 10 ou 11 ;
- SDK [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) avec le runtime Windows Desktop.

## Compiler

Depuis `E:\Dev\PinteMod-ControlCenter\app` :

```powershell
dotnet restore .\PinteMod.ControlCenter.sln
dotnet build .\PinteMod.ControlCenter.sln -c Debug
dotnet build .\PinteMod.ControlCenter.sln -c Release
```

## Tester

```powershell
dotnet test .\PinteMod.ControlCenter.sln -c Debug --no-build
dotnet test .\PinteMod.ControlCenter.sln -c Release --no-build
```

## Lancer

Après une compilation Debug :

```powershell
dotnet run --project .\src\PinteMod.ControlCenter\PinteMod.ControlCenter.csproj -c Debug
```

Ou lancer directement :

```text
app\src\PinteMod.ControlCenter\bin\Debug\net8.0-windows\PinteMod.ControlCenter.exe
app\src\PinteMod.ControlCenter\bin\Release\net8.0-windows\PinteMod.ControlCenter.exe
```

Sans argument, l’application reste entièrement simulée.

## Paquet portable Windows x64

La candidate autonome corrigée et prête pour la revue finale est disponible ici :

```text
app/artifacts/PinteMod-ControlCenter-v2.2.0-rc.2-win-x64.zip
```

Il contient le runtime .NET 8 Windows nécessaire : aucun SDK n’est requis sur le poste cible. Décompresser entièrement l’archive puis lancer `PinteMod.ControlCenter.exe`. Le fichier `LISEZ-MOI.txt` rappelle le démarrage, les garanties read-only et le report volontaire des essais RCON à une période sans joueur.

Le paquet ne contient aucune configuration opérateur, secret DPAPI, donnée PinteMod, log, XUID réel, fichier serveur, GSC, PDB ou chemin privé de compilation. La RC2 remplace la RC1 retirée et ajoute les correctifs finaux de confidentialité, ouverture read-only par handle vérifié et sémantique RCON conservatrice. Les diagnostics RCON, Community Pause et principales actions serveur ont été validés sur le serveur réel. Les autres actions serveur et joueur sont protégées par confirmation et verrou manuel ; leurs essais terrain restent regroupés dans une seule validation finale avant le tag stable.

### Test de source Local/LAN

La page **Paramètres** permet de sélectionner **Local** ou **LAN**, de saisir une racine PinteMod explicite et de tester en lecture seule `current_session.json` ainsi que les quatre heartbeats. Le mode Local attend le dossier `UnrankedServer` contenant `boiii`. Le mode LAN attend uniquement le partage read-only du dossier `boiii\scriptdata\pintemod`, par exemple `\\portable\PinteModData`. Le test s’exécute hors du thread UI, ne recherche aucun serveur et ne modifie aucun fichier PinteMod.

Après un test lisible, l’opérateur peut enregistrer cette source pour le prochain démarrage. La configuration non sensible est stockée sous `%LOCALAPPDATA%\PinteMod\ControlCenter\operator-settings.json`. Si la source enregistrée est inaccessible, l’application revient en simulation avec un avertissement. Les arguments ci-dessous restent prioritaires sur la configuration enregistrée.

### Mode hybride local

Le mode hybride exige toujours les deux arguments suivants :

```powershell
dotnet run --project .\src\PinteMod.ControlCenter\PinteMod.ControlCenter.csproj -c Debug -- --data-mode=hybrid-local --server-root="C:\Chemin\Vers\UnrankedServer"
```

Ou avec l’exécutable compilé :

```powershell
.\src\PinteMod.ControlCenter\bin\Release\net8.0-windows\PinteMod.ControlCenter.exe --data-mode=hybrid-local --server-root="C:\Chemin\Vers\UnrankedServer"
```

`ServerRoot` doit être un chemin absolu existant vers le dossier contenant `boiii`. Il n’est ni recherché ni enregistré automatiquement. `server-sandbox/` n’est jamais choisi implicitement.

Le mode hybride lit uniquement :

- `boiii/scriptdata/pintemod/logs/current_session.json` ;
- `boiii/scriptdata/pintemod/health/supervisor.json` ;
- `boiii/scriptdata/pintemod/health/ban_service.json` ;
- `boiii/scriptdata/pintemod/health/geoip_bridge.json` ;
- `boiii/scriptdata/pintemod/health/live_console.json`.
- `boiii/scriptdata/pintemod/ranks_v2/players/*.json` ;
- `boiii/scriptdata/pintemod/ranks_v2/maps/*.json` ;
- `boiii/scriptdata/pintemod/easter_eggs_v2/profiles.json` ;
- `boiii/scriptdata/pintemod/easter_eggs_v2/maps/*.json`.
- `boiii/scriptdata/pintemod/diagnostics/installation_verification.json` ;
- `boiii/scriptdata/pintemod/bans/service_status.json` ;
- `boiii/scriptdata/pintemod/identity/roles.json` ;
- `boiii/scriptdata/pintemod/localization/manual/*.json` et `localization/auto/*.json` ;
- `boiii/scriptdata/pintemod/remote/feedback.latest.txt` pour l’état Community Pause v0.3 ;
- `boiii/scriptdata/pintemod/logs/pause.log` pour les nouveaux événements de pause ;
- dans le dossier de la session active seulement : `connections.log`, `community.log`, `ranks.log`, `easter_eggs.log`, `identity.log`, `moderation.log`, `localization.log`, `storage.log` et `validation.log`.

Carte, identifiant de session, version déclarée, quatre services, profils Ranks, records de manches, Easter Egg Records officiels, diagnostics disponibles, métadonnées joueur autorisées et événements structurés deviennent locaux. La manche, la durée, l’état Ranked/Unranked, la présence et les joueurs ne deviennent disponibles que lorsqu’un événement structuré explicite les prouve. Points, vie, inventaire, maximum de joueurs et état du processus BOIII restent inconnus. L’état global PinteMod reste « État inconnu — aucun heartbeat dédié ».

Seuls les fichiers `.json` actifs directement présents dans `ranks_v2/players` et `ranks_v2/maps` sont lus. L’ancien dossier `ranks/`, les sous-dossiers, `.tmp`, `.bak` et sauvegardes ne sont jamais utilisés comme sources actives. Les XUID complets servent uniquement d’identifiants métier internes ; la page Records les abrège.

Pour les Easter Egg Records, seuls `profiles.json` schéma 3 et les JSON officiels directs de `easter_eggs_v2/maps/` schéma 2 sont autorisés. Les cartes doivent être déclarées `OFFICIAL`. Les dossiers `candidates/`, `test/`, `backups/`, l’ancien arbre `easter_eggs/`, les logs, `.tmp` et `.bak` sont toujours exclus. Une source officielle valide sans fichier de record affiche zéro record local et ne réintroduit aucune simulation.

En mode hybride, les lectures sont exécutées hors du thread UI et actualisées automatiquement toutes les deux secondes par une boucle mono-exécution annulée à la fermeture. Le bouton **Actualiser** reste disponible. En simulation, aucun lecteur local ni moniteur n’est créé. Les préférences non implémentées restent désactivées et marquées « À venir ».

Les logs sont lus incrémentalement, avec une limite de 2 Mio par fichier et 500 événements en mémoire. Une ligne finale partielle attend son saut de ligne ; une ligne malformée est isolée sans masquer les lignes valides. Un changement d’identifiant de session vide les curseurs et le cache de session. Les données structurées conservent séparément état de lecture, fraîcheur, âge et provenance ; une dernière valeur mémoire n’est jamais présentée comme fraîche.

Le statut Community Pause n’est considéré comme actuel que si `feedback.latest.txt` est valide, âgé de 15 secondes au maximum et postérieur au manifeste de la session active. Entre 15 et 45 secondes, la donnée est conservée comme périmée mais ne peut autoriser aucune commande. La page Serveur affiche alors l’état, le délai de reprise automatique, le compteur et le vote actif. `pause.log` est suivi depuis la fin présente à l’ouverture : l’historique global n’est pas rejoué comme s’il appartenait à la session courante, les XUID sont exclus des détails et seuls les événements `PAUSE_*`, `RESUME_VOTE_START`, `VOTE_RESULT` et `STATUS` sont acceptés.

La Live Console propose un filtre `PAUSE` dédié. Les boutons **Mettre en pause** et **Reprendre** restent désactivés tant que la source Community Pause live n’est pas fraîche ou que la configuration RCON est incomplète. Les autres mutations serveur et joueur autorisées sont décrites plus bas et exigent leur propre confirmation ainsi qu’une vérification manuelle. Le catalogue de préparation est disponible dans `docs/GITHUB_SERVER_COMMAND_CATALOG.md` à la racine du workspace.

La page Logs fonctionne comme Live Console : actualisation automatique, filtres, recherche, auto-scroll et pause/reprise de l’affichage. La pause ne suspend pas la collecte ; le nombre de nouveaux événements en attente est affiché. Les résultats RCON sont ajoutés à un audit de session en mémoire, après neutralisation des XUID, IP, GUID et chemins. Le filtre visible peut être copié dans le presse-papiers pour une revue humaine ; seules les lignes déjà neutralisées sont utilisées. Les pages Serveur et Paramètres permettent de copier de la même manière la dernière réponse diagnostique affichée, jamais le secret RCON.

## Diagnostics RCON

La page **Paramètres** accepte uniquement une adresse IP numérique explicite et le port UDP BOIII. La cible doit être la boucle locale ou appartenir à une plage privée/link-local IPv4 ou IPv6 ; les IP publiques et noms d’hôte sont refusés par le ViewModel, la persistance, les services et le transport. Le secret est enregistré séparément sous `%LOCALAPPDATA%\PinteMod\ControlCenter\rcon.secret.dpapi` avec DPAPI `CurrentUser`. Il n’est présent ni dans le JSON de configuration, ni dans un ViewModel, ni dans les logs.

Dix diagnostics seulement sont autorisés :

- **Health complet** → `ezzhealth full` ;
- **État de la pause** → `ezzpausestatus` ;
- **Carte** → `ezzmap` ;
- **Courant** → `ezzpowerstatus` ;
- **Pack-a-Punch** → `ezzpapstatus` ;
- **Manche** → `ezzround` ;
- **Joueurs connectés** → `ezzplayers`.
- **Audit carte complet** → `ezzmapaudit full` ;
- **État des événements** → `ezzeventstatus` ;
- **Catalogue des power-ups** → `ezzpowerups`.

Les cinq diagnostics du Bloc C sont manuels, read-only, sérialisés avec les autres opérations RCON et leur réponse est neutralisée avant affichage. Ils ne mettent pas automatiquement à jour les snapshots locaux. La sortie stable de `ezzplayers` ne fournit pas de BOIII_XUID et reste informative. Le ciblage réel utilise exclusivement les BOIII_XUID lus dans `connections.log` pour la session active ; le joueur et la source sont relus après confirmation et avant tout envoi.

Le module Community Soft Pause v0.3 écrit lui-même son retour dans `feedback.latest.txt` et ajoute un événement `STATUS` à `pause.log` lorsque `ezzpausestatus` est demandé. Le Control Center ne crée ni ne modifie ces fichiers : il les ouvre uniquement en lecture. Les diagnostics ont été validés humainement avec `PASS=51 | WARNING=0 | ERROR=0` et un statut Pause v0.3 cohérent.

`ezzpauseforce` et `ezzresume` restent les seules mutations disposant d’une confirmation locale automatique. Chaque clic exige une confirmation Oui/Non, puis le statut local est relu et revalidé juste avant l’envoi. Une seule lecture `ezzpausestatus` suit la mutation. Dès que l’appel UDP de mutation commence, toute erreur de transport est considérée comme une livraison potentielle : l’ancien snapshot ne peut jamais autoriser une répétition. L’interface ne confirme la réussite que si un feedback local plus récent expose l’état attendu. Une source locale périmée, un vote actif, une configuration incomplète ou un résultat incertain maintient les contrôles verrouillés jusqu’à l’observation d’un statut local nouveau et frais ; aucun retry automatique n’existe.

La page Serveur expose une liste fermée d’actions confirmées : **Terminer/Définir la manche**, **Courant**, **Pack-a-Punch**, **musique de carte**, **passages standard**, **garder un zombie**, **éliminer les zombies** et **délai des power-ups PinteMod**. Elles ne font aucun retry et restent non confirmées après l’envoi, car BOIII imprime leur résultat uniquement dans sa console. Toutes les mutations restent alors verrouillées jusqu’à ce que l’opérateur vérifie la console et clique sur **J’ai vérifié la console**.

Le catalogue de cartes combine les 14 cartes officielles connues, la ligne active `set sv_maprotation "..."` que l’opérateur peut coller explicitement, les cartes custom ajoutées localement et toute carte courante observée. `server_zm.cfg` n’est jamais recherché ni lu automatiquement. L’import accepte uniquement une ligne de rotation stricte et le catalogue est conservé sous `%LOCALAPPDATA%\PinteMod\ControlCenter\map-catalog.json`, sans écriture serveur. Changement/redémarrage de carte, événements et boss restent simulés tant qu’aucun contrat générique sûr n’est disponible.

Les pages Dashboard et Joueurs rendent réelles, uniquement pour une présence locale revalidée par BOIII_XUID, les actions **Revive**, **Respawn**, **Points**, **Munitions**, **Godmode**, **Téléportation au viseur**, **Arme**, **Atout**, **Tous les atouts**, **Power-up**, **Mute**, **Unmute**, **Kick**, **Ban**, **Rôle** et **Retrait du rôle**. Les montants, armes, atouts, power-ups, durées et rôles proviennent de listes fermées ; aucun pseudo, slot, texte de commande ou motif libre n’est utilisé. `owner` n’est jamais attribuable depuis l’interface. Après tout envoi potentiel, Dashboard, Joueurs, Serveur et Pause/Reprendre partagent un verrou global jusqu’à vérification humaine.

L’historique de modération du joueur sélectionné peut être chargé en lecture seule depuis `boiii/scriptdata/pintemod/moderation/history/<BOIII_XUID>.json`. Le chemin reste interne, le XUID complet n’est jamais exposé, seuls les compteurs et les derniers libellés neutralisés sont affichés, et `.tmp`/`.bak` ne sont jamais utilisés comme source active.

Le paquet BOIII utilise le préfixe `FF FF FF FF`, puis `rcon <secret> <commande>` en UTF-8. Le délai est limité à trois secondes. Un verrou partagé sérialise les transports des quatre services et un coordinateur unique sérialise les parcours UI complets, confirmation et vérification comprises. À la fermeture, les nouvelles opérations sont refusées et l’application attend la fin des opérations déjà acceptées avant de disposer ses lecteurs. Aucune commande n’est exécutée au démarrage ou pendant l’actualisation automatique. Les actions non autorisées restent gérées par `ISimulationActionService` avec `CommandSent = false` ou désactivées avec le libellé « À venir ».

BOIII peut accuser réception d’une commande GSC avec un datagramme sans texte, comportement également pris en charge par l’outil PowerShell de référence. Le Control Center affiche alors **Envoyé · sans texte** et demande de vérifier la console du serveur ; ce statut ne devient pas vert automatiquement. Lorsqu’un texte est renvoyé, **Health complet** doit contenir la bannière PinteMod et les trois compteurs `PASS`, `WARNING` et `ERROR`, tandis que **État de la pause** doit contenir la bannière Community Pause v0.3, l’état actif et le compteur de pauses réussies. Toute autre réponse est affichée comme **Réponse non reconnue**, après neutralisation, et ne déverrouille aucune action.

La copie locale et le commit GitHub audité ne fournissent pas de snapshot persistant stable pour les armes, l’arme équipée, le Pack-a-Punch, les munitions, les atouts, les points ou la vie. L’instantané interne utilisé par le mécanisme de réanimation GSC n’est ni complet, ni écrit dans un fichier consommable. Ces valeurs ne sont donc pas inventées. Un futur snapshot GSC read-only ciblé par BOIII_XUID sera nécessaire, hors Bloc A.

## Organisation

```text
app/
├── src/
│   ├── PinteMod.ControlCenter/                # WPF, vues et ViewModels
│   ├── PinteMod.ControlCenter.Core/           # modèles, contrats, validation XUID
│   └── PinteMod.ControlCenter.Infrastructure/ # simulation et lecteurs locaux read-only
├── tests/PinteMod.ControlCenter.Tests/        # MSTest
├── artifacts/screenshots/                     # captures de la livraison
└── PinteMod.ControlCenter.sln
```

L'injection de dépendances est réalisée par constructeurs dans le composition root `App.xaml.cs`. Les futurs lecteurs locaux pourront remplacer `IControlCenterDataProvider` sans coupler le domaine à WPF.
