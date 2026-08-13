# Rapport d’intégration — contrats PinteMod Control Center v1

Date : 2026-08-14  
Base : branche post-RC2 `codex/post-rc2-runtime-contracts`  
Source contractuelle : PinteModReal contre-revue `e279a59`

## Périmètre intégré

- lecture locale bornée et confinée de `control_center_capabilities.json` ;
- lecture locale bornée et confinée de `action_feedback.latest.json` ;
- lecture locale bornée et confinée de `map_transition.json` ;
- lecture locale bornée et confinée de `server_identity.json` ;
- actions fermées `ezzccrestartmap`, `ezzccboss`, `ezzccsethostname` et `ezzccclearjoinpassword` ;
- corrélation par session, `request_id`, séquence, fraîcheur, transition et révision d’identité ;
- intégration UI du redémarrage, des boss publiés, du hostname public et de l’état booléen du mot de passe joueur.

## Garanties conservées

- simulation par défaut et configuration locale/LAN explicite seulement ;
- aucune découverte automatique, commande libre, relance automatique ou écoute réseau ;
- aucune lecture ni exposition de `g_password` ;
- SET mot de passe désactivé ; Change Map et événements génériques simulés ;
- ciblage boss interne par BOIII_XUID, revalidé après confirmation, affichage abrégé uniquement ;
- données `.tmp` ignorées et `.bak` jamais promues ;
- cache périmé non autoritaire ; lecture hors thread UI ; arrêt propre conservé ;
- aucun fichier PinteMod/GSC modifié et aucun serveur ou RCON lancé pendant l’intégration.

## Résultats automatisés

- Debug : 0 avertissement, 0 erreur, 413/413 tests réussis ;
- Release : 0 avertissement, 0 erreur, 413/413 tests réussis ;
- XAML : 10/10 valides ;
- schémas JSON : 4/4 valides ;
- commandes interdites en production : 0 ;
- bindings vers un XUID complet : 0 ;
- lancement de processus ou écoute entrante : 0.

## Validation humaine restante

Une seule passe groupée, après compilation GSC réussie sur la copie de test :

1. Restart Map avec transition et nouvelle session corrélées ;
2. Spawn Boss avec alias publié et joueur encore connecté ;
3. Set Hostname avec révision d’identité croissante ;
4. Clear Join Password avec révision croissante et état désactivé ;
5. vérification visuelle responsive de la carte Identité et du sélecteur Boss.

Aucune validation ne doit être effectuée sur un serveur occupé.
