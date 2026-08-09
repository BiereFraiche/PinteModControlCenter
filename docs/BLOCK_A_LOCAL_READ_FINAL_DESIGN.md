# Bloc A — conception finale de la lecture locale read-only

Date : 2026-08-03

## Périmètre livré

Le Bloc A finalise en une seule livraison la lecture locale hybride. Le mode simulation reste le mode par défaut. Le mode hybride n’est activé qu’avec :

```text
--data-mode=hybrid-local --server-root=<chemin absolu existant>
```

Il n’existe aucune découverte automatique, mémorisation de chemin, communication réseau, commande RCON, lecture de secret, écriture PinteMod, modification GSC ou lancement de processus.

## Sources locales réellement disponibles

L’inventaire a été confirmé sur la copie de test explicitement désignée. Les chemins ci-dessous sont relatifs à `ServerRoot` et ne sont jamais recherchés ailleurs.

| Source | Disponibilité constatée | Format et autorité | Mise à jour attendue |
|---|---|---|---|
| `boiii/scriptdata/pintemod/logs/current_session.json` | présente | JSON schéma 1 ; autorité de la session active, de la carte et de la version déclarée | événementielle au début de session |
| `boiii/scriptdata/pintemod/health/{supervisor,ban_service,geoip_bridge,live_console}.json` | présents | JSON heartbeat schéma 1 ; autorité de l’état déclaré de chaque service | périodique ; fraîcheur 15 s, expiration au-delà de 45 s |
| `boiii/scriptdata/pintemod/ranks_v2/players/*.json` | présents | JSON profils schéma 2 ; autorité Ranks validée en Phase 2.2 | événementielle/persistante |
| `boiii/scriptdata/pintemod/ranks_v2/maps/*.json` | présents | JSON records de manches schéma 4 ; autorité validée en Phase 2.2 | événementielle/persistante |
| `boiii/scriptdata/pintemod/easter_eggs_v2/profiles.json` | présent | JSON profils officiels schéma 3 ; autorité validée en Phase 2.3 | événementielle/persistante |
| `boiii/scriptdata/pintemod/easter_eggs_v2/maps/*.json` | aucun record officiel constaté | JSON records officiels schéma 2 ; catalogue vide valide | événementielle/persistante |
| `boiii/scriptdata/pintemod/diagnostics/installation_verification.json` | présent | JSON schéma 1 ; résultat déclaré du dernier outil de vérification, pas un heartbeat | uniquement lors d’une vérification |
| `boiii/scriptdata/pintemod/bans/service_status.json` | présent | JSON schéma 1 ; complément déclaré du Ban Service | périodique, fréquence non contractuelle |
| `boiii/scriptdata/pintemod/identity/roles.json` | présent | JSON schéma 1 ; association XUID/rôle et pseudo d’affichage | événementielle |
| `boiii/scriptdata/pintemod/localization/{manual,auto}/*.json` | dossiers disponibles ; fichiers optionnels | JSON unitaire ; langue déclarée par BOIII_XUID | événementielle |
| `boiii/scriptdata/pintemod/logs/sessions/<session_id>/*.log` | présents pour les sessions observées | texte append-only structuré ; source secondaire, jamais supérieure aux JSON spécialisés | au fil des événements |

Dans la session inspectée, les familles `community.log`, `connections.log`, `easter_eggs.log`, `identity.log` et `ranks.log` sont présentes. Les familles `moderation.log`, `localization.log`, `storage.log` et `validation.log` sont autorisées mais optionnelles.

Aucun snapshot runtime stable n’expose l’inventaire courant d’un joueur : armes possédées, arme équipée, Pack-a-Punch, munitions et atouts. Aucun snapshot stable n’a été retenu pour les points ou la vie. Ces données restent inconnues. Leur future intégration nécessitera un contrat GSC read-only dédié et ciblé par BOIII_XUID ; aucun GSC n’est modifié dans le Bloc A.

## Architecture consolidée

- Les providers validés des Phases 2.1, 2.2 et 2.3 restent la baseline et sont enveloppés par `BlockAControlCenterDataProvider`.
- `IInstallationVerificationReader`, `IBanServiceStatusReader`, `ILocalPlayerMetadataReader` et `IStructuredLogReader` séparent les sources et restent indépendants de WPF.
- `BlockALocalPathPolicy` constitue la liste blanche propre au Bloc A et confine chaque chemin sous le `ServerRoot` normalisé de la Phase 2.1.
- `ReadOnlyBlockAJsonFileReader` ouvre les JSON avec `FileAccess.Read`, partage tolérant, trois tentatives courtes et une limite de 1 Mio.
- `StructuredLogReader` lit au maximum 2 Mio par fichier et conserve au maximum 500 événements. Les curseurs, joueurs et événements sont remis à zéro lors d’un changement de session ou d’un remplacement détecté par identité de création et empreinte du préfixe.
- `HybridLocalSnapshotMonitor` exécute explicitement sa boucle mono-exécution sur le pool de threads, toutes les deux secondes, puis publie le snapshot sur le Dispatcher uniquement après la fin des I/O. Sa tâche est conservée, annulée et attendue avant destruction des lecteurs.
- Les six pages consomment le même `IControlCenterSnapshotStore`. Aucune page ne lit le disque.

## Autorité et valeurs inconnues

Les JSON spécialisés conservent leur autorité sur la session, les services, les Ranks et les records. Les logs ne les écrasent pas. Ils peuvent uniquement fournir des observations runtime explicites : JOIN/ACTIVE/LEAVE, manche, horloge de session, état `MATCH_UNRANKED`, rôle, langue, pays et état de modération lorsqu’un événement autorisé le déclare.

L’absence d’un événement n’est pas interprétée. En particulier :

- `Ranked` n’est jamais déduit de l’absence de `MATCH_UNRANKED` ;
- l’état du processus BOIII, le maximum de joueurs, les points, la vie et l’inventaire restent inconnus ;
- la carte globale PinteMod reste neutre : « État inconnu — aucun heartbeat dédié » ;
- `module_version` reste une version déclarée et non une preuve de santé.

## Tolérance et cache

- `.tmp`, `.bak`, archives, sous-dossiers non autorisés et anciens formats sont ignorés.
- Un fichier JSON absent, vide, partiel, trop volumineux, de schéma inconnu ou illisible produit un état distinct ; aucune sauvegarde n’est promue automatiquement.
- Les lecteurs JSON conservent leur dernière valeur valide en mémoire. Elle est alors marquée `MemoryCache`, `Stale` et « Dernière donnée valide — lecture actuelle indisponible ».
- Une ligne finale de log sans saut de ligne n’est pas consommée avant d’être complète.
- Une ligne de log malformée est comptée et isolée ; les autres lignes valides restent disponibles.
- Une troncature/rotation remet le curseur du fichier concerné à zéro.
- Une session absente ou un identifiant de session non autorisé produit une source absente/invalide sans sortir de `ServerRoot`.

## Confidentialité

- Le XUID complet reste réservé aux modèles Core et au rapprochement interne. Aucun ViewModel de Dashboard ou Serveur n’expose le snapshot Bloc A, le contexte contenant `ServerRoot` ou le modèle serveur brut ; les surfaces bindables utilisent uniquement des valeurs d’affichage.
- `LogPrivacyFilter` neutralise XUID, adresses IPv4/IPv6, GUID, chemins avec lecteur, UNC et Unix, ainsi que les champs sensibles avant toute construction de texte affichable.
- Les identifiants de session et les messages d’exception ne sont jamais restitués tels quels dans l’interface.
- Seuls des noms d’événements et champs publics placés sur liste blanche peuvent atteindre l’interface.
- Les chats, commandes, menus, rapports libres, contenus de bannissement et fichiers de secret ne sont jamais ouverts.
- Le rapport d’installation n’affiche ni `root` ni `details` ; seules les synthèses, noms de contrôles, statuts et recommandations filtrées sont présentés.

## Interface

- Dashboard : provenance locale, valeurs runtime disponibles ou `INCONNU`, services et événements structurés.
- Joueurs : présence inférée, pseudo filtré, XUID abrégé, rôle/langue/pays disponibles ; vie, points et inventaire inconnus.
- Serveur : diagnostics d’installation read-only, état complémentaire du Ban Service et valeurs serveur non observables neutralisées.
- Records : baselines 2.2/2.3 conservées et état Ranked provenant exclusivement du snapshot.
- Logs : flux local filtré, famille source, temps relatif de session, compteurs de lignes ignorées/malformées.
- Paramètres : mode explicite, racine masquée, fréquence d’actualisation et garanties read-only ; réglages non implémentés désactivés et « À venir ».

## Validation attendue

- compilations Debug et Release : zéro avertissement, zéro erreur ;
- suite complète dans les deux configurations ;
- tests de fichiers absents, partiels, invalides, tronqués et périmés ;
- tests de cache, changement de session, annulation et prévention des exécutions concurrentes ;
- tests de filtrage des données sensibles et absence de XUID complet bindable ;
- empreintes taille/date/SHA-256 avant et après lecture de toutes les sources autorisées ;
- scan statique sans réseau, processus, RCON effectif ni écriture ;
- validation visuelle des six pages en mode hybride sur la copie de test ;
- une seule revue globale à partir du ZIP final du Bloc A.
