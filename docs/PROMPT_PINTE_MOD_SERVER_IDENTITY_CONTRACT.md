# Prompt ChatGPT — contrat PinteMod pour nom public et `g_password`

La candidate post-RC2 du PinteMod Control Center est validée. Travaille uniquement sur une branche ou une copie de test de PinteModReal ; ne modifie jamais le serveur de production.

## Objectif

Concevoir, implémenter et tester deux contrats PinteMod fermés destinés au Control Center :

1. modifier le nom public BOIII (`sv_hostname`) ;
2. définir ou retirer le mot de passe de connexion joueur (`g_password`).

Le secret RCON est une donnée différente et ne doit jamais être modifié par ces commandes.

## Contraintes absolues

- aucune commande console ou dvar libre acceptée depuis l’interface ;
- aucun nom de dvar transmis par le client ;
- aucune concaténation non validée vers une commande moteur ;
- longueurs, alphabet, encodage et valeurs vides strictement définis ;
- aucun mot de passe dans les logs, la console, les heartbeats, snapshots, erreurs ou fichiers de feedback ;
- aucun mot de passe persistant ajouté à PinteMod ;
- aucun retry automatique requis ;
- aucun port, serveur web, processus ou accès distant supplémentaire ;
- préserver toutes les commandes et validations PinteMod existantes ;
- ne jamais toucher au serveur de production pendant le développement.

## Exigences pour `g_password`

- fournir deux actions distinctes : définir et retirer ;
- ne jamais interpréter une valeur vide comme une commande ambiguë ;
- refuser guillemets, retours de ligne, séparateurs et toute forme d’injection avant la couche moteur ;
- documenter la taille maximale réelle acceptée par BOIII ;
- ne jamais écrire ou renvoyer la valeur reçue ;
- produire seulement un booléen observable `join_password_enabled` ;
- analyser la confidentialité réelle du transport RCON. Un simple Base64/hex n’est pas une protection ; sans mécanisme démontré, déclarer officiellement la mutation compatible loopback uniquement.

## Exigences pour le nom public

- commande dédiée, sans nom de dvar fourni par le client ;
- texte normalisé avec longueur et alphabet documentés ;
- prise en charge des espaces seulement si l’encodage est non ambigu et sûr ;
- retour structuré contenant uniquement le nom effectivement appliqué après normalisation ;
- préciser si la modification est runtime seulement ou persistante après redémarrage.

## Feedback local demandé

Étendre le feedback unifié ou fournir une source versionnée contenant uniquement :

- `schema_version` ;
- `session_id` ;
- séquence monotone ;
- action canonique ;
- résultat fermé `accepted|applied|rejected|failed` ;
- code de résultat fermé ;
- nom public appliqué pour l’action hostname uniquement ;
- `join_password_enabled` pour l’action mot de passe ;
- jamais la valeur de `g_password`, même encodée ou hachée.

## Tests attendus

- valeur normale acceptée ;
- bornes minimales/maximales ;
- retrait explicite du mot de passe ;
- guillemets, CR/LF, séparateurs, Unicode inattendu et surcharge refusés ;
- aucune valeur sensible dans tous les fichiers/logs produits ;
- feedback lié à la bonne session et strictement plus récent ;
- survie changement de carte et redémarrage documentée ;
- tests sur copie locale uniquement, aucun joueur réel requis.

## Rapport final demandé

- commit/branche de test ;
- commandes exactes et grammaire fermée ;
- validation et bornes ;
- analyse de confidentialité du transport ;
- fichiers de feedback et exemples neutralisés ;
- résultats des tests ;
- points nécessitant une validation humaine sur serveur vide.

Ne modifie pas le Control Center dans cette passe. Ne publie rien sur la release stable.
