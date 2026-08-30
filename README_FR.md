# PinteMod Control Center

> **Version stable actuelle : v2.4.4.** Le sélecteur 🇫🇷/🇬🇧 est mémorisé dans la barre de titre ; **Démarrer** comme **Lancer tous** peuvent lancer un premier serveur PinteMod local sans RCON préalable. La [candidate v2.4.5-rc4](https://github.com/BiereFraiche/PinteModControlCenter/releases/tag/v2.4.5-rc4) attend uniquement la recette terrain finale.

Documents utiles : [état v2.4.4](docs/STATUS_V2.4.4.md) · [recette finale](docs/RECETTE_FINALE_FR.md) · [test terrain](PREVIEW_INTEGRATION4B1_FIX17_TEST_FR.txt) · [déploiement VM](docs/DEPLOIEMENT_VM_FR.md).

Pour partir d’une base BOIII/Ezz obtenue légalement et installer PinteMod avec le Control Center, voir [Démarrer avec un serveur BOIII vierge](docs/SERVEUR_BOIII_DEMARRAGE_FR.md). Le dépôt ne redistribue pas de serveur BOIII ni d’archive de jeu.

[English documentation](README.md)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
![Windows](https://img.shields.io/badge/plateforme-Windows-0078D4)
![WPF](https://img.shields.io/badge/UI-WPF-0A84FF)
![Tests](https://img.shields.io/badge/tests-620%20r%C3%A9ussis-24C875)
[![Version](https://img.shields.io/badge/version-v2.4.4-168BFF)](https://github.com/BiereFraiche/PinteModControlCenter/releases/tag/v2.4.4)

PinteMod Control Center est une application Windows locale en **C# / .NET 8 / WPF** destinée à observer et administrer un serveur dédié **Call of Duty: Black Ops III Zombies** utilisant [PinteMod](https://github.com/BiereFraiche/PinteMod) avec BOIII/Ezz.

Créé et maintenu par **BiereFraiche**, avec l’assistance de développement de Codex et ChatGPT.

> **Version stable actuelle :** v2.4.4.
> La candidate **v2.4.5-rc4** termine Debug et Release avec **0 avertissement, 0 erreur et 620/620 tests réussis**. Elle reste une prépublication jusqu’à la recette BOIII réelle.
> Sans configuration explicite, l’application démarre toujours en simulation complète.

[Télécharger la stable v2.4.4](https://github.com/BiereFraiche/PinteModControlCenter/releases/tag/v2.4.4) · [Tester la candidate v2.4.5-rc4](https://github.com/BiereFraiche/PinteModControlCenter/releases/tag/v2.4.5-rc4)

## v2.4.4 — stable

Cette version consolide la génération 4B1 après validation des flux Agent, RCON et réparation PinteMod :

| Élément | État v2.4.4 |
|---|---|
| Version produit | `2.4.4` |
| Compilation | Debug et Release, 0 avertissement, 0 erreur |
| Tests automatisés | 616/616 réussis dans les deux configurations |
| Distribution | EXE autonome + dossier portable + ZIP + SHA-256 + rapport anonymisé |
| Validation terrain | Server3 et Agent fixe/portable validés ; réparation sans écrasement de modules personnalisés couverte par test |

La version ajoute notamment le Manager jusqu’à huit serveurs, l’adaptation BOIII/PinteMod/GSC tiers, l’historique de chat local, l’Agent SMB récupérable et le conditionnement en deux formats. Le lanceur de rendu logiciel traite les PC/prises en main distante incompatibles et, dans le format dossier, un Agent autonome identique au mono-EXE évite toute dépendance aux DLL du dossier utilisateur.

Pour reconstruire tous les livrables en une passe sous Windows :

```powershell
.\BUILD_MANAGER_PREVIEW.bat
```

Le Control Center peut être installé dans une VM Windows et affiché depuis un autre PC au moyen de la console de l’hyperviseur ou de RDP derrière un VPN/passerelle sécurisée. Il n’ajoute aucun serveur web ni port de contrôle distant. Voir le [guide de déploiement VM](docs/DEPLOIEMENT_VM_FR.md).

### Premier serveur en trois gestes

1. Choisir le dossier qui contient directement `boiii`.
2. Cliquer **PRÉPARER ET DÉMARRER** : PinteMod, le Bridge et l’Agent local sont préparés sans écraser les données joueurs ni les scripts tiers.
3. Définir le RCON plus tard dans **Paramètres** si les commandes d’administration sont nécessaires. Le premier démarrage BOIII n’est pas bloqué par cette étape.

### BOIII et demande administrateur

Le Control Center ne demande pas de droits administrateur. Si Windows affiche une demande UAC pour `boiii.exe`, elle vient de BOIII ou de sa configuration Windows. Pour un lancement local ou distant sans intervention, ouvrez une fois **Propriétés → Compatibilité** sur `boiii.exe` et décochez **Exécuter ce programme en tant qu’administrateur** (également dans les paramètres de tous les utilisateurs, si l’option est présente). Installez le serveur hors de `Program Files`, par exemple sous `E:\Games`.

![Direction graphique validée de PinteMod Control Center](design/pintemod-control-center-reference.png)

## Fonctionnalités principales

- Dashboard sombre et redimensionnable ;
- jusqu’à huit onglets serveurs isolés, chacun avec sa source, son contexte RCON et sa couleur d’accent ;
- carte, session, services, joueurs et événements ;
- Ranks, records de manches et Easter Egg Records officiels ;
- Live Console structurée avec filtres, recherche, pause, auto-scroll et copie neutralisée ;
- source locale ou partage LAN `PinteModData` explicitement configuré ;
- diagnostics RCON manuels à liste blanche ;
- observation et commandes confirmées Community Pause/Reprendre ;
- administration serveur : manche, courant, Pack-a-Punch, musique, passages, zombies et durée des power-ups ;
- redémarrage de carte, apparition de boss compatibles et nom public du serveur via contrats PinteMod fermés et observables ;
- mot de passe de connexion BOIII éphémère, limité au RCON loopback, jamais persisté ni affiché ;
- actions joueur par BOIII_XUID : assistance, armes, atouts, power-ups, téléportation, modération et rôles ;
- historique de modération local read-only ;
- catalogue de cartes officiel/custom local sans lecture automatique de `server_zm.cfg` ;
- confirmation humaine, verrou transversal et aucun retry automatique.

## Vision : Adaptive BOIII Core

La stable v2.2.0 est le socle, pas la ligne d’arrivée. Le prochain grand chantier vise à rendre le Control Center **utile avec BOIII seul**, puis progressivement plus riche lorsque des capacités fiables sont réellement disponibles :

- un **Capability Engine** pour que l’interface s’adapte aux preuves, à leur provenance et à leur fraîcheur, sans supposer qu’un seul package serveur fournit tout ;
- une progression de la Simulation vers BOIII natif, un catalogue GSC importé explicitement, un Bridge Control Center first-party optionnel, puis l’expérience PinteMod complète ;
- un **analyseur GSC read-only** conservateur qui découvre des candidats à examiner sans compiler, exécuter ou modifier les scripts tiers ;
- un futur Bridge versionné appartenant au Control Center, installé uniquement avec consentement explicite et jamais fusionné dans les GSC tiers ;
- une interface first-party adaptative, sans système de plugins, sans RCON libre, sans cloud et sans nouveau port entrant.

Il s’agit d’une roadmap progressive, pas d’une promesse de fonctionnalité déjà incluse dans la v2.2.0. La sécurité, la configuration explicite et les résultats vérifiables restent prioritaires.

**[Découvrir la vision publique v2.3 →](docs/V2.3_VISION.md)**

## Garanties de sécurité

- aucun serveur web, compte, cloud ou port entrant ;
- contrôle en VM possible via une console d’hyperviseur ou RDP derrière un VPN/passerelle existante, sans fonction de contrôle distant ajoutée à l’application ;
- aucune découverte réseau ;
- RCON uniquement après action explicite vers une adresse numérique locale/privée autorisée ;
- secret RCON protégé par DPAPI `CurrentUser`, jamais réaffiché ;
- ciblage réel exclusivement par BOIII_XUID interne ;
- aucune commande construite depuis un texte libre ;
- listes blanches fermées et confirmations pour les mutations ;
- toute livraison UDP incertaine verrouille les répétitions ;
- aucune écriture directe du Control Center dans les données PinteMod ;
- XUID complets, IP, GUID et chemins neutralisés avant affichage ou copie ;
- un arrêt du Control Center n’arrête jamais BOIII.

Les commandes Ban, Mute et Rôle peuvent demander à PinteMod d’effectuer sa persistance administrative normale après confirmation. Le Control Center ne modifie pas lui-même ces fichiers.

## Modes de données

### Simulation par défaut

Les données et actions sont simulées de manière réaliste. `CommandSent` reste toujours faux et aucun serveur n’est contacté.

### Mode hybride read-only

Le mode hybride remplace uniquement les champs réellement prouvés par les sources PinteMod autorisées. Il s’active depuis **Paramètres** ou avec :

```powershell
PinteMod.ControlCenter.exe --data-mode=hybrid-local --server-root="C:\Servers\UnrankedServer"
```

La racine doit être absolue, existante et explicitement choisie. Aucune installation n’est recherchée automatiquement.

Depuis un autre PC, partager en lecture seule uniquement :

```text
boiii/scriptdata/pintemod/
```

Ne partagez pas le dossier serveur complet et n’exposez jamais SMB ou RCON à Internet.

## Architecture

```text
app/
├── src/
│   ├── PinteMod.ControlCenter/                WPF, vues et ViewModels
│   ├── PinteMod.ControlCenter.Core/           modèles, contrats et validations
│   └── PinteMod.ControlCenter.Infrastructure/ simulation, lecteurs locaux et RCON
├── tests/PinteMod.ControlCenter.Tests/        tests MSTest
├── packaging/                                 documentation du paquet portable
└── PinteMod.ControlCenter.sln
```

Les modèles Core restent indépendants de WPF. Les lecteurs et services sont injectés par interfaces afin de conserver des ViewModels testables.

## Compiler et tester

Prérequis : Windows 10/11 et SDK .NET 8.

```powershell
dotnet restore .\app\PinteMod.ControlCenter.sln --configfile .\app\NuGet.Config
dotnet build .\app\PinteMod.ControlCenter.sln -c Debug --no-restore
dotnet test .\app\PinteMod.ControlCenter.sln -c Debug --no-build --no-restore
dotnet build .\app\PinteMod.ControlCenter.sln -c Release --no-restore
dotnet test .\app\PinteMod.ControlCenter.sln -c Release --no-build --no-restore
```

Lancer depuis les sources :

```powershell
dotnet run --project .\app\src\PinteMod.ControlCenter\PinteMod.ControlCenter.csproj -c Debug
```

Pour compiler, tester, publier et auditer automatiquement les deux formats Preview :

```powershell
.\BUILD_MANAGER_PREVIEW.bat
```

Les sorties sont placées dans `app/artifacts/release-v<version>-win-x64/` : EXE unique, dossier autonome, deux ZIP, `SHA256SUMS.txt` et `SELF-TEST.txt`.

Le même contrôle hors ligne peut être lancé depuis les deux formats :

```powershell
.\PinteMod.ControlCenter.exe --self-test --self-test-report="C:\Temp\PinteMod-self-test.txt"
```

Le code de sortie `0` et `RESULTAT=PASS` valident le paquet local. Le rapport ne contient ni profil serveur, ni secret, ni nom de machine/utilisateur, ni chemin privé.

Les commandes `dotnet` de compilation et de test ne lancent aucun serveur BOIII, BAT ou outil externe. Le script Preview appelle uniquement les outils locaux de compilation, de test, de compression et d’audit.

## Contrôles réels et limites volontaires

Les actions réelles ne sont proposées que lorsque leur ciblage et leur preuve locale sont connus. **Redémarrer la carte**, les alias de boss compatibles, le nom public du serveur et la suppression du mot de passe utilisent des contrats PinteMod fermés avec retour local corrélé. La définition du mot de passe de connexion BOIII est limitée à un endpoint RCON loopback explicitement configuré et sa valeur reste éphémère.

Le changement générique de carte et les événements génériques restent affichés en simulation faute de contrat suffisamment sûr et autoritaire. Une capacité absente, périmée ou incompatible n’autorise jamais une action réelle.

Les besoins futurs sont documentés dans [`docs/PINTEMOD_REQUIREMENTS_NEXT.md`](docs/PINTEMOD_REQUIREMENTS_NEXT.md) : heartbeat global, snapshot runtime serveur/joueurs, capacités par carte et feedback structuré des mutations.

## Documentation du dépôt

- [`app/README.md`](app/README.md) — documentation technique et opérateur complète ;
- [`docs/CODEX_PROGRESS.md`](docs/CODEX_PROGRESS.md) — état chronologique détaillé ;
- [`docs/DECISIONS.md`](docs/DECISIONS.md) — décisions d’architecture et de sécurité ;
- [`docs/TODO.md`](docs/TODO.md) — tâches restantes, bloquées et à valider ;
- [`contracts/`](contracts/) — contrats JSON de conception ;
- [`design/`](design/) — référence graphique validée ;
- [`reference/`](reference/) — référence publique PinteMod figée utilisée pour les audits.

Les builds, paquets portables, configurations locales, secrets DPAPI, copies serveur et données runtime sont exclus de Git.

## État de validation

v2.4.2 automatisée :

```text
Compilation Debug     PASS — 0 avertissement, 0 erreur
Compilation Release   PASS — 0 avertissement, 0 erreur
Tests Debug           PASS — 616/616
Tests Release         PASS — 616/616
Auto-diagnostic       PASS — RESULTAT=PASS, sans serveur ni réseau
Paquet mono-EXE       PASS — 1 EXE autonome, audit confidentialité réussi
Paquet dossier        PASS — archive et audit réussis
Validation terrain    PASS — Server3 et liaison Agent fixe/portable
```

Stable publique v2.2.0 :

```text
Compilation Debug     PASS — 0 avertissement, 0 erreur
Compilation Release   PASS — 0 avertissement, 0 erreur
Tests Debug           PASS — 460/460
Tests Release         PASS — 460/460
Contrôle du ZIP       PASS — aucun PDB, chemin privé de build, XUID interdit, secret ou fichier serveur
Contrôles terrain     PASS — lectures locales, diagnostics, actions confirmées et net_password
```

Le paquet stable est construit depuis un commit Git identifié, publié en archive Windows x64 autonome et contrôlé avant diffusion. Les mutations opérationnelles restent manuelles, confirmées et protégées par une sémantique de livraison conservatrice.

## Projet associé

PinteMod v2.1.1 et sa documentation serveur :

- <https://github.com/BiereFraiche/PinteMod>

PinteMod Control Center est une interface opérateur indépendante. Il n’inclut ni BOIII, ni Black Ops III, ni ressource propriétaire du jeu.

## Sécurité

Consultez [`SECURITY.md`](SECURITY.md). Ne publiez jamais dans une issue un mot de passe RCON, fichier DPAPI, XUID complet, IP privée, chemin serveur ou archive runtime.
