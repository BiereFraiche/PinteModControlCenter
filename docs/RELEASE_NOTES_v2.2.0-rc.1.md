# PinteMod Control Center v2.2.0-rc.1

Date : 2026-08-09.

Cette candidate est une copie octet pour octet du paquet MVP Preview 13 soumis à la revue globale indépendante.

## Validation

- revue globale : aucun blocage obligatoire ;
- Debug : 0 avertissement, 0 erreur, 285/285 tests réussis ;
- Release : 0 avertissement, 0 erreur, 285/285 tests réussis ;
- SHA-256 du paquet : `8ED173DEF5D67B14791433AAE1B60EBD136BA6F3963D972CE59E5D5D59D205F5` ;
- archive contrôlée : 466 fichiers, aucun secret, configuration opérateur, fichier serveur, donnée runtime ou chemin dangereux.

## Fonctionnalités principales

- Dashboard et navigation WPF sombre redimensionnable ;
- simulation sûre par défaut et source PinteMod Local/LAN explicitement configurée ;
- session, services, logs, Ranks, records et Easter Egg Records en lecture locale ;
- Live Console filtrée avec pause, recherche et copie neutralisée ;
- diagnostics RCON manuels à liste blanche ;
- Community Pause/Reprendre confirmés ;
- actions serveur et joueur typées, confirmées, ciblées par BOIII_XUID et sans retry automatique ;
- catalogue de cartes officiel/custom local ;
- verrou transversal après toute livraison UDP potentiellement incertaine.

## Limites volontaires

Le changement ou redémarrage de carte, les boss et les événements génériques restent simulés tant que PinteMod ne fournit pas de contrat stable, borné et compatible avec la carte active. Le snapshot d’inventaire joueur détaillé et le heartbeat global PinteMod font également partie des extensions futures documentées dans `PINTEMOD_REQUIREMENTS_NEXT.md`.

## Avant le tag stable

Une validation terrain groupée reste demandée pour les mutations serveur et joueur qui n’ont pas encore toutes été observées sur un serveur réel. Aucun essai ne doit être effectué pendant qu’une partie avec des joueurs pourrait être perturbée.
