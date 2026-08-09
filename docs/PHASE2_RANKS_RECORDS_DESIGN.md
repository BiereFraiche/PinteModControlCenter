# Phase 2.2 — Profils Ranks et records de manches read-only

Date : 2026-08-02

## Périmètre livré

La Phase 2.2 s’ajoute à la baseline Phase 2.1 sans modifier ses lecteurs. Elle est active uniquement en mode hybride explicite :

```text
--data-mode=hybrid-local --server-root=<chemin absolu>
```

Elle lit uniquement :

- `boiii/scriptdata/pintemod/ranks_v2/players/*.json` — profils schéma 2 ;
- `boiii/scriptdata/pintemod/ranks_v2/maps/*.json` — records de manches schéma 4.

Les Easter Egg Records, logs, inventaires joueur, RCON, réseau, secrets, processus et écritures restent hors périmètre.

## Composition

`RankRecordsOverlayDataProvider` enveloppe le fournisseur Phase 2.1. Il lance les lectures profils et records en parallèle de la collecte du snapshot existant, puis remplace seulement :

- le catalogue de profils Ranks ;
- les records de manches standards.

Carte, session et services conservent exactement l’autorité Phase 2.1. Manche, durée, Ranked, serveur BOIII, joueurs et événements restent simulés. Les Easter Egg Records simulés sont conservés et explicitement étiquetés comme tels.

## Lecteurs séparés

- `IRankProfileReader` / `RankProfileReader` : profils individuels.
- `IRoundRecordReader` / `RoundRecordReader` : top 5 de chaque carte et catégorie 1–4 joueurs.
- `RankRecordsPathPolicy` : confinement des deux dossiers et exclusion des sources non actives.
- `ReadOnlyRankJsonFileReader` : ouverture read-only tolérante, contrôle taille/date avant et après lecture, trois tentatives courtes.

Toutes les opérations de répertoire et de fichier sont démarrées sur un thread de travail. L’annulation est propagée et aucun accès fichier n’est effectué par les ViewModels.

## Liste blanche et confidentialité

### Profils schéma 2

Champs utilisés :

- `schema_version` ;
- `xuid` ;
- `last_name`, avec repli sur `name` ;
- `sessions` ;
- `total_seconds` ;
- `best_overall_round`.

Le nom du fichier doit être un BOIII_XUID hexadécimal de 16 caractères et correspondre au champ `xuid`. `key` et `identity_kind` ne sont pas exposés. Le XUID complet reste dans le modèle Core pour l’identité ; les ViewModels WPF n’exposent que sa forme abrégée et aucune info-bulle ne rétablit la valeur complète.

### Records de carte schéma 4

Champs utilisés :

- `schema_version`, `map`, `display` ;
- `round_{1-4}p_{1-5}` ;
- `seconds_{1-4}p_{1-5}` ;
- `holders_{1-4}p_{1-5}` ;
- `holder_xuids_{1-4}p_{1-5}` ;
- `match_id_{1-4}p_{1-5}`.

Le code carte doit correspondre au nom du fichier. Une entrée active exige une manche et une durée positives, un détenteur, un identifiant de match et exactement autant de XUID valides que la catégorie. Une entrée invalide est ignorée sans supprimer les autres entrées valides de la carte.

Après la revue bloquante, cette garantie est renforcée par un résultat explicite `Empty` / `Valid` / `Invalid` pour chaque emplacement. Les champs d’un emplacement sont validés sans lever d’exception au niveau du document.

## Confinement et limites

- aucune recherche automatique d’installation ;
- racine absolue existante validée par `LocalPinteModOptions` ;
- dossiers fixes sous `ServerRoot` ;
- fichiers directs seulement, sans récursion ;
- liens et jonctions existants rejetés ;
- `.tmp`, `.bak`, sauvegardes et ancien `ranks/` ignorés ;
- maximum 1 000 profils, 64 Kio par profil ;
- maximum 100 cartes, 1 Mio par carte ;
- `FileAccess.Read` avec `FileShare.ReadWrite | FileShare.Delete` ;
- aucune restauration automatique ni création de fichier.

## Tolérance et provenance

Une lecture complète ou partielle contenant au moins un document valide produit un catalogue local et compte séparément les fichiers/entrées ignorés. Si la source entière devient indisponible après une lecture valide, la dernière valeur mémoire est conservée avec :

- provenance `MemoryCache` ;
- fraîcheur `Stale` ;
- message « Dernière donnée valide — lecture actuelle indisponible ».

Un catalogue historique lu correctement reste `Fresh` au sens de l’intégrité de lecture ; son âge de fichier est affiché séparément. Il ne reçoit pas les seuils 15/45 secondes réservés aux heartbeats.

## Interface

La page Records présente :

- nombre et cartes des profils Ranks ;
- pseudo, XUID abrégé, sessions, temps total et meilleure manche ;
- records de manches avec carte, position top 5, catégorie, manche, détenteurs, XUID abrégés, durée et provenance ;
- état de lecture, fraîcheur, âge, provenance et chemin logique de chaque source ;
- Easter Egg Records et statut Ranked toujours marqués simulés.

L’actualisation reste exclusivement manuelle. Les réglages « À venir » restent désactivés.

## Vérifications automatisées

La suite couvre notamment :

- schémas 2/4 valides et incompatibles ;
- XUID invalide ou incohérent avec le nom de fichier ;
- carte incohérente avec le nom de fichier ;
- fichiers absents, partiels et cache mémoire retardé ;
- entrées de record invalides isolées ;
- exclusion `.tmp`, `.bak`, ancien `ranks/` et sous-dossiers ;
- annulation coopérative ;
- superposition limitée aux profils/records et conservation explicite des Easter Egg Records simulés ;
- XUID abrégé côté ViewModel ;
- invariance taille, date et SHA-256 avant/après lecture.
