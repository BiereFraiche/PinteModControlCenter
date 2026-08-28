# PinteMod Control Center v2.4.0 — Integration Preview 4B1 Fix16

> Candidate Preview. Ne pas présenter comme stable avant validation humaine Server3 et Agent multi-PC.

## Principaux changements

- auto-diagnostic local exécutable sans serveur depuis l’interface ou `--self-test` ;
- rapport anonymisé avec statut global et contrôles individuels ;
- validation des métadonnées produit, assemblages, six pages WPF et ressources embarquées ;
- contrôle réel des payloads PinteMod/Bridge dans une racine temporaire jetable ;
- génération automatique de `SELF-TEST.txt` pendant le build et dans GitHub Actions ;
- EXE unique autonome et dossier autonome Windows x64 conservés.

## Garanties du self-test

- aucun profil serveur ou paramètre opérateur chargé ;
- aucun secret DPAPI lu ou écrit ;
- aucun accès réseau, Agent, BOIII ou RCON ;
- aucune commande serveur ;
- aucun nom de machine/utilisateur, chemin privé ou détail d’exception dans le rapport.

## Validation

- builds Debug et Release : 0 avertissement, 0 erreur ;
- tests Debug et Release : 596/596 réussis ;
- contrôle du paquet et audits des deux distributions intégrés au build final ;
- validation terrain Server3 et Agent multi-PC toujours reportée.

Les empreintes finales sont consignées dans `docs/STATUS_PREVIEW4B1_FIX16.md` et `app/artifacts/integration-preview4b1-fix16-win-x64/SHA256SUMS.txt`.
