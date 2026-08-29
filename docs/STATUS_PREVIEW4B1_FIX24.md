# État — Integration Preview 4B1 Fix24

Date : 2026-08-30

## Positionnement

- stable publique : `v2.2.0` ;
- dernière prerelease GitHub : Fix15 ;
- candidat de publication : `2.4.0-preview-integration.4b1.fix24` ;
- statut : prerelease validée, prête à publier.

## Correctifs inclus

- Community Pause v0.3/v0.4 et inventaires first-party PinteMod de 28 ou 35 modules reconnus sans assouplir le fail-closed ;
- rendu logiciel WPF disponible pour les GPU et prises en main distante qui affichent une fenêtre blanche ;
- Agent SMB récupérable, avec réparation locale des anciens marqueurs de mise à jour ;
- dossier portable avec Agent autonome identique au mono-EXE, pour une activation fiable sans DLL manquante ;
- réparation first-party accessible pour le vérificateur PinteMod stock connu ; les collisions inconnues restent refusées ;
- absence confirmée de `hotfix.gsc` dans les distributions Ezz BOIII actuelles reconnue comme normale, sans avertissement.

## Validation

- Build Debug et Release : 0 avertissement, 0 erreur ;
- Tests Debug et Release : 611/611 réussis ;
- auto-diagnostic sans serveur : PASS ;
- ZIP mono-EXE et ZIP dossier audités, sans secrets, runtime, chemins privés ou ports entrants ;
- Server3 : santé PinteMod et commande RCON validées ;
- Agent : activation portable, auto-récupération Windows et liaison avec le PC fixe validées.

## Publication

La prerelease doit inclure les deux ZIP Windows x64, `SHA256SUMS.txt` et `SELF-TEST.txt`. La stable publique reste v2.2.0.
