# Besoins PinteMod pour terminer les fonctions avancées du Control Center

Date : 2026-08-09.

Ces évolutions doivent être développées et validées sur une copie de test ou une branche dédiée. Ne jamais modifier directement le serveur de production.

## Priorité 1 — heartbeat global PinteMod

Créer un heartbeat dédié, par exemple :

`boiii/scriptdata/pintemod/health/pintemod.json`

Champs minimaux recommandés :

```json
{
  "schema_version": 1,
  "updated_at_utc": "2026-08-09T12:00:00Z",
  "declared_state": "running",
  "module_version": "2.1.1",
  "session_id": "identifiant-de-session",
  "last_error_code": null
}
```

Règles : écriture atomique `.tmp` puis remplacement, fréquence documentée, `running|stopped|error` fermé, aucun chemin, IP, secret ou texte d’exception libre. Cela permettra de remplacer « État inconnu — aucun heartbeat dédié » par un état réellement autoritaire.

## Priorité 2 — snapshot runtime serveur et joueurs

Créer un snapshot JSON read-only consommable, versionné et lié à la session active, par exemple :

`boiii/scriptdata/pintemod/runtime/control_center_snapshot.json`

Champs serveur utiles :

- `schema_version` ;
- `updated_at_utc` ;
- `session_id` ;
- `map_code` ;
- `round` ;
- `session_started_at_utc` ou durée autoritaire ;
- `ranked_status` ;
- `power_on` ;
- `pack_a_punch_state` ;
- `connected_players` et `max_players`.

Pour chaque joueur, ciblé par BOIII_XUID :

- `xuid` complet, réservé au traitement interne ;
- `display_name`, uniquement informatif ;
- `presence` et `life_state` ;
- `points` ;
- liste des armes par identifiant canonique ;
- arme équipée ;
- niveau/état Pack-a-Punch de chaque arme ;
- munitions chargeur et réserve ;
- liste des atouts par identifiant canonique ;
- éventuels états Godmode/Mute uniquement s’ils sont réellement autoritaires.

Règles : snapshot atomique, maximum raisonnable documenté, fréquence d’environ une à deux secondes ou écriture sur changement avec heartbeat, aucune donnée inventée, aucun ancien snapshot réutilisé après changement de `session_id`, aucun pseudo utilisé comme identité.

## Priorité 3 — catalogue de capacités et cartes sans lire server_zm.cfg

Créer un fichier neutralisé généré par PinteMod, par exemple :

`boiii/scriptdata/pintemod/diagnostics/control_center_capabilities.json`

Il devrait contenir :

- version de schéma et version PinteMod ;
- cartes installées connues ;
- rotation active si PinteMod peut la déterminer sans exposer la configuration complète ;
- carte active ;
- actions compatibles avec la carte active ;
- alias fermés des événements, boss et power-ups compatibles ;
- version des contrats de commande disponibles.

Ne jamais recopier le contenu complet de `server_zm.cfg`, les arguments de lancement, chemins, IP ou secrets.

## Priorité 4 — commandes carte sûres et accusé local

Fournir des contrats GSC fermés et documentés pour :

- changer vers une carte installée et explicitement autorisée ;
- redémarrer la carte active.

Le GSC doit valider le code de carte côté serveur à partir d’une liste autoritaire. Aucun texte libre ne doit devenir une commande moteur. Le changement de carte peut interrompre la réponse UDP : écrire un accusé local avant transition et un résultat de nouvelle session après chargement.

Le contrat doit préciser : commande exacte, paramètres autorisés, bornes, résultat console, fichier de feedback, comportement si carte inconnue, partie déjà en transition ou échec de chargement.

## Priorité 5 — événements et boss compatibles par carte

Éviter une commande générique qui prétend fonctionner partout. Préférer :

- alias internes fermés ;
- liste de capacités publiée pour la carte active ;
- refus GSC explicite si l’action n’est pas compatible ;
- paramètres bornés ;
- retour local structuré et versionné ;
- aucun nom d’entité, fonction GSC ou commande libre transmis depuis l’interface.

## Priorité 6 — feedback unifié des mutations

Pour supprimer la vérification manuelle de la console, ajouter un journal ou snapshot de résultats, par exemple :

`boiii/scriptdata/pintemod/remote/action_feedback.latest.json`

Champs recommandés :

- `schema_version` ;
- `session_id` ;
- identifiant de requête borné et validé ;
- action canonique ;
- cible BOIII_XUID éventuelle ;
- `accepted|applied|rejected|failed` ;
- code de résultat fermé ;
- horodatage UTC ;
- séquence monotone.

Le message affichable doit provenir d’un code fermé, pas d’un texte libre contenant potentiellement chemins, IP, commandes ou secrets. Aucun retry automatique ne doit être exigé côté Control Center.

## Livrables attendus côté PinteMod

- contrats JSON versionnés et exemples valides/invalides ;
- documentation des fréquences et autorités ;
- commandes exactes et listes blanches ;
- tests GSC ou procédure de validation reproductible ;
- preuve des écritures atomiques ;
- preuve de survie aux changements de carte/session ;
- archive ou commit de test séparé de la production ;
- aucune donnée runtime réelle, aucun secret et aucun compte opérateur dans les livrables.

## Ordre recommandé cette semaine

1. heartbeat global PinteMod ;
2. snapshot runtime serveur/joueurs ;
3. capabilities/cartes ;
4. feedback unifié ;
5. commandes carte ;
6. événements et boss.

Les deux premières priorités apportent le plus de valeur au Control Center et permettent de remplacer plusieurs états inconnus sans ajouter de commande destructive.
