# État — PinteMod Control Center v2.4.0

Date : 2026-08-30

## Statut

- version : `2.4.0` ;
- type : release stable ;
- livraison : EXE Windows x64 autonome et dossier portable Windows x64 ;
- réseau : aucun port entrant ni serveur web ajouté.

## Correctif final

Sur une installation PinteMod déjà détectée, l’action **Vérifier / Réparer PinteMod** ne traite plus le payload complet. Elle cible exclusivement `boiii/tools/Verify_PinteMod_Installation.ps1` :

- le vérificateur stock v2.1.1 connu peut être mis à niveau ;
- un vérificateur inconnu reste refusé ;
- aucun module ou service existant, notamment `PinteMod_Ban_Service.ps1`, n’est modifié ;
- un backup local du vérificateur précédent est conservé avant remplacement.

L’inventaire PinteMod actuel de 35 modules est reconnu et l’absence de `hotfix.gsc` dans les distributions Ezz BOIII actuelles est attendue.

## Validation

- Build Debug et Release : 0 avertissement, 0 erreur ;
- Tests Debug et Release : 613/613 réussis ;
- auto-diagnostic hors serveur : PASS ;
- ZIP mono-EXE et ZIP dossier : audit confidentialité PASS ;
- liaison Agent fixe/portable et diagnostic RCON : validation humaine sur Server3.

La stabilité décrit les scénarios couverts et ne remplace pas une sauvegarde ni une validation prudente sur Server3 avant tout déploiement supplémentaire.
