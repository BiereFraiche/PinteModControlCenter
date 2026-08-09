# Phase 2.1 — Lecture locale hybride read-only

Date : 2026-08-02

## Périmètre livré

La simulation reste le mode par défaut. Le mode hybride est activé uniquement avec :

```text
--data-mode=hybrid-local --server-root=<chemin absolu>
```

Cette sous-phase ne lit que `current_session.json` et les quatre heartbeats locaux. Elle ne lit aucun log de session, joueur, rank, record, Easter Egg Record ou inventaire. Elle ne contient ni RCON, réseau, secret, lancement de processus, écriture PinteMod ou modification GSC.

## Sources autorisées

Tous les chemins sont résolus sous le `ServerRoot` explicite :

| Source | Chemin relatif | Autorité utilisée |
|---|---|---|
| Session | `boiii/scriptdata/pintemod/logs/current_session.json` | Carte, session et version déclarée |
| Supervisor | `boiii/scriptdata/pintemod/health/supervisor.json` | État déclaré du Supervisor |
| Ban Service | `boiii/scriptdata/pintemod/health/ban_service.json` | État déclaré du Ban Service |
| GeoIP Bridge | `boiii/scriptdata/pintemod/health/geoip_bridge.json` | État déclaré du GeoIP Bridge |
| Live Console | `boiii/scriptdata/pintemod/health/live_console.json` | État déclaré de la Live Console |

Les chemins sont une liste fermée. Les chemins relatifs externes, racines de volume, liens/jonctions existants, `.tmp` et `.bak` sont rejetés. Aucune installation n’est recherchée automatiquement.

## Tolérance de lecture

- Lecture asynchrone démarrée sur un thread de travail.
- `FileAccess.Read` uniquement, avec `FileShare.ReadWrite | FileShare.Delete`.
- Taille maximale : 1 Mio par fichier.
- Trois tentatives courtes en cas de fichier vide, partiel, modifié pendant la lecture, verrouillé ou temporairement invalide.
- Validation explicite de `schema_version = 1`, des champs requis, de l’identité `tool` et des dates UTC.
- Conservation en mémoire de la dernière valeur valide, sans créer de fichier et sans restaurer de sauvegarde.
- Annulation coopérative propagée jusqu’au snapshot partagé.

## Dimensions d’état

Chaque service expose indépendamment :

1. l’état déclaré par le heartbeat ;
2. l’état de la tentative de lecture ;
3. la fraîcheur ;
4. l’âge ;
5. la provenance.

La synthèse graphique respecte les règles suivantes :

- jusqu’à 15 s : fraîche ;
- plus de 15 s et jusqu’à 45 s : retardée, orange ;
- plus de 45 s : expirée, synthèse inconnue et grise ;
- `Hors ligne` uniquement si le heartbeat déclare explicitement `stopped` ;
- `Erreur` si le heartbeat déclare une erreur, ou après trois actualisations consécutives en échec durable ;
- une valeur mémoire expirée affiche « Dernière donnée valide — périmée » et ne reste jamais verte.

Le manifeste `current_session.json` est un événement de début de session et non un heartbeat. Son âge est affiché, mais il n’expire pas automatiquement après 45 secondes. En cas d’échec après une lecture valide, sa provenance devient mémoire et sa fraîcheur devient retardée.

## Snapshot hybride

`HybridControlCenterDataProvider` part du snapshot simulé partagé et remplace uniquement :

- code et nom de carte ;
- identifiant de session ;
- version PinteMod déclarée ;
- Supervisor, Ban Service, GeoIP Bridge et Live Console.

Les autres propriétés restent simulées. Le Dashboard, la sidebar, Records et Paramètres l’indiquent explicitement. La version déclarée par `current_session.json` ne constitue pas une preuve de santé. La carte PinteMod reste neutre : « État inconnu — aucun heartbeat dédié ».

## Données différées

Les armes possédées, l’arme équipée, le Pack-a-Punch, les munitions et les atouts ne sont pas exposés dans les JSON ou logs stables audités. Leur intégration nécessitera ultérieurement un snapshot GSC read-only dédié, ciblé par BOIII_XUID. Aucun GSC n’est modifié dans cette passe.

## Vérifications automatisées

La suite vérifie notamment :

- mode simulé par défaut et paire d’arguments hybride obligatoire ;
- manifeste et heartbeats valides, absents, vides, partiels, incompatibles, verrouillés et datés dans le futur ;
- seuils exacts 15/45 secondes ;
- absence de passage automatique à Hors ligne ;
- cache mémoire périmé et erreur durable au troisième échec ;
- confinement de tous les chemins ;
- non-utilisation des `.tmp` et `.bak` ;
- invariance taille, date de modification et SHA-256 des cinq fichiers avant/après lecture ;
- substitution minimale du snapshot hybride.
