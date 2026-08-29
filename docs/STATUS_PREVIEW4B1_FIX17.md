# État — Integration Preview 4B1 Fix17

Date : 2026-08-29

## Positionnement

- stable publique : `v2.2.0` ;
- dernière prerelease GitHub : Fix15 ;
- candidat local : `2.4.0-preview-integration.4b1.fix17` ;
- statut : Preview, non publiée.

## Correctifs

- `ezzpausestatus` accepte Community Pause v0.3 et v0.4 avec bannière, module, état actif et compteur obligatoires ;
- le vérificateur PinteMod embarqué accepte uniquement les inventaires connus de 28 ou 35 modules ;
- lors d’une installation explicitement demandée, seul le vérificateur v2.1.1 stock à empreinte connue peut être mis à niveau ;
- l’absence de `hotfix.gsc` reste un avertissement BOIII optionnel, jamais un fichier fourni par PinteMod ;
- le paquet conserve l’exclusion de tout secret, profil opérateur, runtime ou port entrant.

## Validation locale

- Build Debug : 0 avertissement, 0 erreur ;
- Tests Debug : 599/599 réussis ;
- Build Release : 0 avertissement, 0 erreur ;
- Tests Release : 599/599 réussis ;
- auto-diagnostic Release sans serveur : code de sortie 0.

## Validation humaine restante

1. Installer ou lancer Fix17 sur Server3, jamais Server1/2 en premier.
2. Lancer `ezzhealth full`, puis `ezzpausestatus` sur serveur vide.
3. Vérifier que la réponse Pause v0.4 est reconnue et que le vérificateur ne signale plus 35 modules comme erreur après mise à jour du script.
4. Conserver l’avertissement `hotfix.gsc` comme information tant que la distribution BOIII ne l’exige pas.

GitHub reste inchangé tant qu’une publication explicite n’est pas demandée.
