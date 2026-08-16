# Audit UX et fonctions réellement exploitables — post-RC2

Date : 2026-08-12

Base Control Center : `c1c0d5d840846b37cd298275a73d0b86791dbbef`

PinteModReal audité en lecture seule : `0b293b5371e4405805017bd3afff16cf28276043`

## Conclusion

L’interface exposait déjà la majorité des actions PinteMod utiles qui disposent d’un contrat fermé. Trois actions seulement justifiaient une évolution : le catalogue complet de `Give Weapon`, le Pack-a-Punch de l’arme tenue et le retrait ciblé d’un atout. Un bouton Santé PinteMod a aussi été ajouté au panneau de diagnostics Serveur. Aucun autre bouton métier n’est activé par cet audit.

## Matrice joueur

| Fonction | PinteMod | Contrat CC | UI avant | Cible | Risque | Décision |
|---|---|---|---|---|---|---|
| Revive | `ezzrevive` | Réel fermé | Présente | XUID | Moyen | GARDER |
| Respawn | `ezzspawn` | Réel fermé | Présente | XUID | Élevé | GARDER |
| Points | `points` | Réel, -999999..999999 hors zéro | Présente | XUID | Moyen | GARDER |
| Munitions | `ammo` | Réel fermé | Présente | XUID | Moyen | GARDER |
| Godmode | `godmode` | Réel fermé | Présente | XUID | Élevé | GARDER |
| Donner une arme | `ezzweapon` | Réel, alias fermé | Présente mais catalogue incomplet | XUID | Élevé | ÉTENDRE LE CATALOGUE |
| PAP arme tenue | `ezzpapweapon` | Réel sans option libre | Absente | XUID | Élevé | AJOUTER |
| Donner un atout | `ezzperk` | Réel, 9 alias fermés | Présente | XUID | Moyen | GARDER |
| Retirer un atout | `ezzremoveperk` | Réel, mêmes 9 alias | Absente | XUID | Moyen | AJOUTER |
| Basculer un atout | `ezzperktoggle` | Existe | Absente | XUID | Moyen | NE PAS AJOUTER : résultat ambigu |
| Tous les atouts | `ezzallperks` | Réel fermé | Présente | XUID | Élevé | GARDER |
| Retirer tous les atouts | `ezzclearperks` | Existe | Absent | XUID | Élevé | NE PAS AJOUTER : mutation globale redondante |
| Donner un power-up | `ezzpowerup` | Réel, 9 alias fermés | Présente | XUID | Élevé | GARDER |
| Téléportation | `ezztp` | Réel fermé | Présente | XUID | Élevé | GARDER |
| Mute / Unmute | `ezzmute`, `ezzunmute` | Réels fermés | Présents | XUID | Élevé | GARDER |
| Kick | `ezzkick` | Réel fermé | Présent | XUID | Destructif | GARDER |
| Ban | `ezzban` | Réel, durées fermées | Présent | XUID | Destructif | GARDER |
| Rôle / retrait | `ezzidsetrole`, `ezzidremoverole` | Réels, rôles bornés | Présents | XUID | Élevé | GARDER |
| Historique | fichier local de modération | Read-only | Présent | XUID | Faible | GARDER |
| Warn / AFK | Commandes historiques diverses | Exclu | Absent | — | Inutile/ambigu | NE PAS AJOUTER |

## Matrice serveur et partie

| Fonction | PinteMod | Contrat CC | UI | Risque | Décision |
|---|---|---|---|---|---|
| Manche suivante | `ezznextround` | Réel fermé | Présente | Élevé | GARDER |
| Définir manche | `ezzsetround 2..255` | Réel borné | Présente | Élevé | GARDER |
| Activer courant | `ezzpower` | Réel fermé | Présente | Élevé | GARDER |
| Ouvrir PAP de la carte | `ezzpap` | Réel fermé | Présente | Élevé | GARDER avec libellé distinct du PAP joueur |
| Musique de carte | `ezzmusicplayall` | Réel, sélection par défaut fermée | Présente | Moyen | GARDER |
| Arrêter musique | `ezzmusicstopall` | Réel fermé | Présente | Faible | GARDER |
| Choisir un état musical | argument map-specific | Pas de catalogue local autoritaire | Absent | Moyen | NE PAS AJOUTER |
| Ouvrir passages standard | `ezzunlock` | Réel fermé | Présente | Élevé | GARDER |
| Garder un zombie | `ezzlastzombie` | Réel fermé | Présente | Moyen | GARDER |
| Éliminer les zombies | `ezzkillzombies` | Réel fermé | Présente | Destructif | GARDER |
| Power-ups permanents / délai normal | `ezzfreezepowerups on/off` | Réel booléen fermé | Présents | Moyen | GARDER |
| Community Pause | `ezzpauseforce`, `ezzresume` | Réel + feedback local | Présente | Élevé | GARDER |
| Changer / redémarrer carte | commandes proches mais contrat de transition absent | Simulé | Présents comme simulation | Destructif | RESTER SIMULÉ |
| Événement / boss | commandes dépendantes de la carte | Simulé | Présents comme simulation | Élevé | RESTER SIMULÉ |
| Power-up global | pas de contrat global sûr | Simulé | Présent comme simulation | Élevé | RESTER SIMULÉ |

## Diagnostics

| Diagnostic | RCON fermé | Source locale de repli | Décision |
|---|---|---|---|
| Santé PinteMod | `ezzhealth full` | Heartbeats structurés, résumé partiel seulement | AJOUTER au panneau Serveur ; ne jamais inventer les 51 contrôles |
| Carte | `ezzmap` | Session + runtime frais cohérent | GARDER avec fallback structuré |
| Courant | `ezzpowerstatus` | Runtime frais | GARDER avec fallback structuré |
| PAP de la carte | `ezzpapstatus` | Runtime frais | GARDER avec fallback structuré |
| Manche | `ezzround` | Runtime frais | GARDER avec fallback structuré |
| Joueurs | `ezzplayers` | Runtime frais, affichage neutralisé | GARDER avec fallback structuré |
| Pause | `ezzpausestatus` | `remote/feedback.latest.txt` | GARDER le lecteur spécialisé |
| Audit carte | `ezzmapaudit full` | Aucune | GARDER, signaler la sortie non transportée |
| Événements | `ezzeventstatus` | Aucune | GARDER, signaler la sortie non transportée |
| Catalogue power-ups | `ezzpowerups` | Aucune | GARDER, signaler la sortie non transportée |

Une réponse RCON textuelle reconnue reste prioritaire. Le repli local n’est utilisé qu’après une réponse vide. Il indique toujours sa provenance et ne prétend jamais reproduire la console BOIII.

## Catalogue d’armes

- 19 alias standard/universels canoniques sont toujours proposés.
- Les alias spéciaux sont ajoutés uniquement pour le `map_code` d’un runtime local frais, de la bonne session et de la bonne carte.
- Une carte inconnue ou une source non fraîche ne reçoit aucun alias spécial.
- Les synonymes techniques et identifiants moteur sont refusés.
- Le service possède une seule liste blanche centrale partagée avec la présentation ; PinteMod reste l’autorité finale de disponibilité sur la carte.

## Fonctions volontairement non ajoutées

- Warn, warnings, clearwarnings, AFK et back ;
- console RCON libre, commande ou argument moteur libre ;
- `ezzperktoggle` et `ezzclearperks` ;
- choix libre d’un état musical ;
- commandes de debug, tests GSC, gestion brute du stockage ;
- changement/restart de carte, événements et boss réels ;
- macros ou actions « tout faire ».

## Candidats pour futurs contrats PinteMod

1. **Capabilities par carte** : catalogue versionné des cartes, événements, boss, power-ups et états musicaux compatibles.
2. **Feedback unifié des mutations** : résultat local structuré par identifiant de requête pour supprimer l’acquittement console manuel.
3. **Transition de carte** : commande fermée, validation serveur, accusé avant transition et confirmation après nouvelle session.
4. **Diagnostics structurés complémentaires** : audit carte, événements et catalogue power-ups afin de ne plus dépendre d’une sortie console non transportée.

## Principes UX conservés

- Le bouton global Actualiser relit les sources locales ; aucun diagnostic RCON n’est périodique.
- Les libellés distinguent « ACTIVER PACK-A-PUNCH » de la carte et « PACK-A-PUNCH ARME TENUE » du joueur.
- Le PAP joueur est désactivé si aucune arme équipée n’est observable ou si elle est clairement déjà améliorée.
- Toute mutation réelle conserve confirmation, revalidation XUID, sérialisation, zéro retry et acquittement manuel.
