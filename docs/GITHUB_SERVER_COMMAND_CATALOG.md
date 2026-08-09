# Catalogue des commandes serveur — référence GitHub stable

Date d’audit : 2026-08-09
Dépôt : `BiereFraiche/PinteMod`
Branche : `main`
Commit audité : `7d5f33489d8635c460d3eb63bb04226c7aa3f326`

## Portée

Ce document prépare les futurs boutons du Control Center. Il ne constitue pas une autorisation d’envoi. Aucun script du dépôt n’a été exécuté et aucun serveur n’a été contacté pendant l’audit.

Le transport de diagnostic manuel et les mutations autorisées utilisent des listes blanches fermées. Pause/Reprendre disposent d’un feedback local ; les actions Manche, Courant et Pack-a-Punch exigent une vérification manuelle de la console. Toutes les actions joueur et les autres actions serveur restent simulées avec `CommandSent = false`.

## Règles communes d’intégration

- Le Control Center conserve le BOIII_XUID complet uniquement dans le modèle métier interne.
- Une future commande joueur utilisera toujours le BOIII_XUID comme sélecteur, jamais le pseudonyme affiché.
- Le XUID doit être revalidé immédiatement avant l’envoi et ne doit jamais être affiché en entier dans l’interface ou les logs.
- Les alias de cartes, armes, atouts et power-ups proviendront de listes blanches fermées ; aucune saisie de commande brute ne sera proposée.
- Les nombres seront parsés et bornés avant construction de la commande.
- Les raisons de modération seront bornées, neutralisées et séparées des arguments structurels.
- Pause, reprise, changement de manche, kick, ban et rôle exigeront une confirmation explicite.
- Un résultat RCON ne prouvera pas à lui seul le nouvel état : l’interface attendra un feedback ou un événement local autoritaire lorsque celui-ci existe.

## Diagnostics et observations

| Fonction UI | Commande vérifiée | Effet | État Control Center |
|---|---|---|---|
| Santé complète | `ezzhealth full` | Diagnostic global | Autorisée manuellement |
| État Pause | `ezzpausestatus` | Affiche l’état et rafraîchit `feedback.latest.txt` / `pause.log` | Autorisée manuellement |
| Informations carte | `ezzmap` | Lecture console de la carte, du courant, de la manche et du PaP | Intégrée manuellement, read-only |
| État courant | `ezzpowerstatus` | Diagnostic du courant | Intégrée manuellement, read-only |
| État Pack-a-Punch | `ezzpapstatus` | Diagnostic du Pack-a-Punch | Intégrée manuellement, read-only |
| État manche | `ezzround` | Manche, IA vivantes et file de spawn | Intégrée manuellement, read-only |
| Joueurs | `ezzplayers` | Liste des joueurs connectés | Intégrée manuellement ; réponse neutralisée avant affichage |
| Historique joueur | `ezzhistory <BOIII_XUID>` | Historique de modération | Non intégrée ; réponse à neutraliser |

### Signatures de réponse retenues

Une réponse UDP non vide n’est pas suffisante pour déclarer un diagnostic réussi. Le Control Center valide désormais les marqueurs stables publiés par le commit audité :

- `ezzhealth full` : `[PinteMod Health]`, `PASS=`, `WARNING=` et `ERROR=` doivent tous être présents ;
- `ezzpausestatus` : `PINTEMOD COMMUNITY PAUSE`, `EXPERIMENTAL v0.3`, `Active:` et `Successful pauses:` doivent tous être présents.
- `ezzmap` : bannière Map Info, carte, déclencheurs Pack-a-Punch et profils courant/PaP ;
- `ezzpowerstatus` : profil et état explicite du courant ;
- `ezzpapstatus` : bannière Pack-a-Punch, carte, profil d’accès, déclencheurs et machines alimentées ;
- `ezzround` : manche courante et nombre d’IA vivantes ;
- `ezzplayers` : compteur de joueurs connectés ou absence explicite de joueur.

La commande `ezzplayers` stable ne fournit pas de BOIII_XUID exploitable pour cibler un joueur. Elle ne peut donc pas autoriser les actions joueur réelles : celles-ci restent verrouillées jusqu’à l’existence d’une source XUID locale fiable.

Un datagramme réduit à son en-tête reste distinct d’une absence de réponse et d’une réponse textuelle non reconnue. L’outil PowerShell de référence décrit ce cas comme une commande envoyée pour laquelle BOIII ne retourne aucun texte. Le Control Center l’affiche donc comme `ENVOYÉ · SANS TEXTE` et demande une vérification dans la console du serveur ; il ne le transforme jamais automatiquement en succès vert.

## Inventaire joueur courant

L’audit du commit ne révèle aucun fichier local persistant exposant l’inventaire courant complet par joueur : armes possédées, arme équipée, Pack-a-Punch, munitions et atouts.

`ezz_admin_commands.gsc` maintient bien en mémoire un instantané interne de réanimation avec arme courante, score et atouts classiques. Cet état est attaché à l’objet joueur, sert uniquement à restaurer une réanimation et n’est ni un snapshot général, ni écrit dans un fichier local consommable par le Control Center. Il ne doit donc pas être présenté comme une source disponible.

Le besoin reste celui d’un futur snapshot GSC strictement read-only, structuré et ciblé par BOIII_XUID. Aucune modification GSC n’est réalisée dans cette passe.

## Pause communautaire v0.3

| Action | Commande vérifiée | Conditions GSC principales | Risque |
|---|---|---|---|
| Mettre en pause | `ezzpauseforce` | Aucun vote actif, au moins un joueur actif, aucun joueur à terre, partie non déjà pausée | Moyen — modifie immédiatement la partie |
| Reprendre | `ezzresume` | Partie actuellement pausée | Moyen — modifie immédiatement la partie |
| Proposer une pause | `ezzvotepause <BOIII_XUID>` | Cible connectée et règles de vote v0.3 | Moyen |
| Proposer une reprise | `ezzvoteresume <BOIII_XUID>` | Cible connectée et règles de vote v0.3 | Moyen |

La pause est une soft pause limitée à 180 secondes. Elle bloque les joueurs et les nouvelles créations d’IA, mais ne fige pas les timers de scripts de carte ou d’Easter Egg.

## Actions joueur vérifiées

| Bouton prévu | Syntaxe future basée sur XUID | Source |
|---|---|---|
| Points | `points <BOIII_XUID> <montant>` | `ezz_admin_commands.gsc` |
| Maximum de points | `maxpoints <BOIII_XUID>` | `ezz_admin_commands.gsc` |
| Munitions | `ammo <BOIII_XUID>` | `ezz_admin_commands.gsc` |
| Godmode | `godmode <BOIII_XUID>` | `ezz_admin_commands.gsc` |
| Respawn | `ezzspawn <BOIII_XUID>` | `ezz_admin_commands.gsc` |
| Revive | `ezzrevive <BOIII_XUID>` | `ezz_admin_commands.gsc` |
| Arme | `ezzweapon <BOIII_XUID> <alias>` | `ezz_admin_weapons.gsc` |
| Atout | `ezzperk <BOIII_XUID> <alias>` | `ezz_admin_perks.gsc` |
| Tous les atouts | `ezzallperks <BOIII_XUID>` | `ezz_admin_perks.gsc` |
| Power-up | `ezzpowerup <BOIII_XUID> <alias>` | `ezz_admin_powerups.gsc` |
| Téléportation | `ezztp <BOIII_XUID_source> <BOIII_XUID_cible>` | `ezz_admin_navigation.gsc` |

Les commandes GSC acceptent également pseudo ou numéro client, mais ces variantes sont interdites au Control Center afin de conserver le ciblage exclusif par BOIII_XUID.

## Actions serveur vérifiées

| Bouton prévu | Commande vérifiée | Remarque |
|---|---|---|
| Terminer la manche | `ezznextround` | Intégrée avec confirmation et vérification console |
| Avancer de plusieurs manches | `ezzskiprounds <nombre>` | Cible maximale déclarée : 255 |
| Définir la manche | `ezzsetround <cible>` | Intégrée ; avance uniquement, cible Control Center 2 à 255 |
| Activer le courant | `ezzpower` | Intégrée ; peut laisser des objectifs propres à la carte incomplets |
| Activer Pack-a-Punch | `ezzpap` | Intégrée ; peut laisser un accès ou une quête propre à la carte incomplet |
| Déverrouiller les passages standard | `ezzunlock` | Ignore les portes de quête et nécessite un joueur connecté |
| Boss et événements | commandes `ezzspawn*` dédiées | Dépendance forte au profil de carte |
| Musique | `ezzmusicplayall` / `ezzmusicstopall` | À confirmer carte par carte |

Le dépôt ne fournit pas de commande GSC unique et sûre de changement direct de carte correspondant au bouton actuel. Cette action reste simulée ; aucune commande console brute ne sera inventée.

## Modération et rôles

| Bouton prévu | Commande vérifiée | Confirmation |
|---|---|---|
| Mute | `ezzmute <BOIII_XUID> [raison]` | Oui |
| Unmute | `ezzunmute <BOIII_XUID>` | Oui |
| Kick | `ezzkick <BOIII_XUID> [raison]` | Oui, dangereuse |
| Historique | `ezzhistory <BOIII_XUID>` | Lecture, réponse neutralisée |
| Ban | `ezzban <BOIII_XUID> [30m|2h|7d|4w|perm] [raison]` | Oui, dangereuse |
| Rôle | `ezzidsetrole <BOIII_XUID> <owner|admin|moderator|helper>` | Oui, dangereuse |
| Retrait de rôle | `ezzidremoverole <BOIII_XUID>` | Oui, dangereuse |

## Ordre d’activation recommandé

1. Valider manuellement `ezzhealth full` et `ezzpausestatus` sur un serveur vide.
2. Activer seulement `ezzpauseforce` et `ezzresume`, avec confirmation et observation du feedback local.
3. Valider le ciblage XUID avec une action réversible : `ammo` ou `ezzrevive` dans un scénario contrôlé.
4. Ajouter points, armes, atouts et power-ups avec listes blanches.
5. Ajouter les commandes serveur dépendantes de la carte.
6. Ajouter kick, ban et rôles en dernier, avec double confirmation et audit explicite.
