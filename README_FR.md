# PinteMod Control Center v2.2

[English documentation](README.md)

PinteMod Control Center est une application Windows locale en **C# / .NET 8 / WPF** destinée à observer et administrer un serveur dédié **Call of Duty: Black Ops III Zombies** utilisant [PinteMod](https://github.com/BiereFraiche/PinteMod) avec BOIII/Ezz.

Créé et maintenu par **BiereFraiche**, avec l’assistance de développement de Codex et ChatGPT.

> **Version stable actuelle :** v2.2.0.
> Debug et Release : **0 avertissement, 0 erreur et 460/460 tests réussis**.
> Sans configuration explicite, l’application démarre toujours en simulation complète.

[Télécharger v2.2.0](https://github.com/BiereFraiche/PinteModControlCenter/releases/tag/v2.2.0)

![Direction graphique validée de PinteMod Control Center](design/pintemod-control-center-reference.png)

## Fonctionnalités principales

- Dashboard sombre et redimensionnable ;
- carte, session, services, joueurs et événements ;
- Ranks, records de manches et Easter Egg Records officiels ;
- Live Console structurée avec filtres, recherche, pause, auto-scroll et copie neutralisée ;
- source locale ou partage LAN `PinteModData` explicitement configuré ;
- diagnostics RCON manuels à liste blanche ;
- observation et commandes confirmées Community Pause/Reprendre ;
- administration serveur : manche, courant, Pack-a-Punch, musique, passages, zombies et durée des power-ups ;
- actions joueur par BOIII_XUID : assistance, armes, atouts, power-ups, téléportation, modération et rôles ;
- historique de modération local read-only ;
- catalogue de cartes officiel/custom local sans lecture automatique de `server_zm.cfg` ;
- confirmation humaine, verrou transversal et aucun retry automatique.

## Garanties de sécurité

- aucun serveur web, compte, cloud ou port entrant ;
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

Ces commandes ne lancent aucun serveur BOIII, BAT ou outil externe.

## Limites volontaires

Le changement/redémarrage de carte et les commandes génériques de boss/événements restent affichés en simulation. PinteMod v2.1.1 ne fournit pas encore de contrat générique suffisamment sûr et autoritaire pour les activer sans risque.

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
