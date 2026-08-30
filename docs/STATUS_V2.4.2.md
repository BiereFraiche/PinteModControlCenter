# État — PinteMod Control Center v2.4.2

Date : 2026-08-30

## Premier démarrage et RCON

- un serveur local PinteMod sans secret Worker démarre directement via le lanceur enregistré, typiquement `Server.bat` ;
- dès qu’un secret RCON est enregistré, le Control Center prépare automatiquement le secret Worker et retrouve son orchestration PinteMod au lancement suivant ;
- la page Paramètres propose **Utiliser ce secret comme premier RCON du serveur** : après confirmation, elle ajoute `rcon_password` au seul `.cfg` explicitement déclaré par `Server.bat` ;
- elle refuse toute initialisation si un `rcon_password` existe déjà, si le lanceur/configuration est ambigu ou si la racine BOIII n’est pas prouvée.

Le serveur doit être arrêté pendant l’initialisation. Le secret est ensuite protégé localement avec DPAPI et n’est jamais réaffiché ou inscrit dans les logs.
