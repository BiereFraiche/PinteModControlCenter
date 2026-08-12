# Audit différentiel post-RC2 — contrats runtime PinteMod

Date : 2026-08-12

## Baselines vérifiées

- Control Center : commit RC2 validé `90d4922cb663e4b8d923ecfb1681483d78db5126`.
- Branche de développement : `codex/post-rc2-runtime-contracts`.
- PinteModReal : commit audité en lecture seule `0b293b5371e4405805017bd3afff16cf28276043`.
- Producteur : `custom_scripts/ezz_admin_control_center_runtime.gsc`, bridge v0.1.2.
- Aucun fichier PinteModReal, GSC ou serveur n’a été modifié.

## Contrats confirmés dans PinteModReal

### Heartbeat global

- source : `boiii/scriptdata/pintemod/health/pintemod.json` ;
- schéma : 1 ;
- fréquence producteur : environ 5 secondes ;
- limite producteur : 4096 octets ;
- états fermés : `running`, `stopped`, `error` ;
- autorité temporelle : `session_gettime_and_file_mtime` ;
- `updated_at_utc` est volontairement vide avec le producteur actuel ;
- la fraîcheur est donc calculée depuis le `LastWriteTimeUtc` du fichier réellement ouvert.

### Snapshot runtime

- source : `boiii/scriptdata/pintemod/runtime/control_center_snapshot.json` ;
- schéma : 1 ;
- fréquence producteur : environ 2 secondes ;
- limite producteur : 32768 octets ;
- maximum 4 joueurs observables et 8 armes par joueur ;
- serveur : carte, manche, durée, Ranked, courant, Pack-a-Punch, joueurs connectés et maximum ;
- joueur : BOIII_XUID, pseudo informatif, client, présence, état de vie, Godmode, points, vie, arme équipée, munitions, inventaire et atouts connus ;
- aucun état Mute n’est produit et aucune valeur correspondante n’est inventée.

### Écriture producteur

`ezz_admin_storage::write_json_safe` écrit et vérifie un `.tmp`, sauvegarde l’ancien actif valide dans `.bak`, écrit puis vérifie l’actif et restaure/quarantaine en cas d’échec. Cette stratégie n’est pas décrite comme un remplacement atomique OS. Le lecteur Control Center complète la sûreté avec un handle read-only vérifié, une détection de modification pendant lecture, trois tentatives et le refus de `.tmp`/`.bak` comme sources actives.

## Intégration Control Center

- deux chemins explicitement ajoutés à la liste blanche locale ;
- lecteurs versionnés dédiés, bornés respectivement à 4096 et 32768 octets ;
- source active exigée sous la racine Local/LAN explicitement configurée ;
- session `current_session.json` conservée comme identité autoritaire ;
- cache mémoire invalidé lors d’un changement de session ;
- fichier futur, source périmée, schéma inconnu ou session/carte différente non autoritaires ;
- `Fresh <= 15 s`, `Stale <= 45 s`, `Expired > 45 s` ;
- heartbeat expiré : PinteMod inconnu, jamais automatiquement hors ligne ;
- `stopped` frais : hors ligne ; `error` frais : erreur ;
- overlay runtime appliqué uniquement sur une lecture locale réussie, fraîche et liée à la session/carte actives ;
- les métadonnées rôle/langue/pays sont fusionnées uniquement par BOIII_XUID ;
- les logs restent la source des événements et le repli des valeurs inférées ;
- Ranks, records, Easter Egg Records et Community Pause restent inchangés.

## Contrats encore absents

- `diagnostics/control_center_capabilities.json` ;
- contrat fermé ChangeMap/RestartMap avec preuve locale ;
- contrat générique sûr pour événements et boss par carte ;
- `remote/action_feedback.latest.json` unifié pour les mutations.

En conséquence, ChangeMap, RestartMap, TriggerEvent et SpawnBoss restent simulés. Le Control Center ne crée aucun de ces fichiers et ne modifie jamais PinteMod.
