# État — PinteMod Control Center v2.4.1

Date : 2026-08-30

## Correctif de gestion des serveurs

- le Gestionnaire propose **Actualiser les serveurs connus** : il relit les racines BOIII locales et UNC déjà enregistrées, sans appairage, transport RCON, lancement ni arrêt ;
- l’analyse lit la déclaration `GamePort`/`net_port` de `Server.bat` ou d’un lanceur `.cmd` et applique ce port à un profil encore sur l’ancien défaut `27017` ;
- `Server.bat` est le lanceur à sélectionner pour une installation BOIII classique ;
- la carte **Connexion opérateur** est explicitement réservée au partage `PinteModData`. Une racine BOIII complète doit être ajoutée dans le Gestionnaire.

## Validation

- compilation Debug et Release : 0 avertissement, 0 erreur ;
- tests Debug et Release : 614/614 réussis ;
- le test ajouté couvre `set GamePort=27021` dans `Server.bat` ;
- aucune commande BOIII/RCON ni opération distante n’est effectuée pendant une actualisation de serveurs.
