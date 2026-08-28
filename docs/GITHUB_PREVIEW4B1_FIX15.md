# Préparation GitHub — Integration Preview 4B1 Fix15

## Dépôts et rôles

- `BiereFraiche/PinteModControlCenter` : code source, CI, pull request et future release du Control Center ;
- `BiereFraiche/PinteMod` : code serveur PinteMod et métadonnées publiques actuellement consultées par le vérificateur de mises à jour.

Ne pas mélanger les tags ou releases des deux produits.

## Branche préparée

`codex/integration-preview-4b1-fix15`

La branche doit être poussée sans force uniquement après ordre explicite. La pull request cible `main` de `PinteModControlCenter`.

## Titre de PR proposé

`Preview 4B1 Fix15 : fail-closed SHA, Agent recovery et double packaging`

## Corps de PR proposé

### Résumé

- importe le candidat 4B1 Fix14 sur la dernière base publique du Control Center ;
- exige des empreintes first-party revues avant d’activer PinteMod et son transport fermé ;
- conserve l’audit GSC tiers en lecture seule ;
- ajoute les distributions mono-EXE et dossier autonome ;
- documente le déploiement VM sans port entrant applicatif ;
- prépare la CI et les audits de confidentialité des deux formats.

### Validation

- Debug et Release sans avertissement ;
- tests Debug et Release réussis ;
- un EXE unique ;
- un dossier autonome et son ZIP ;
- aucun secret, fichier runtime ou chemin privé dans les paquets.

### Limites

- Preview non stable ;
- aucune release stable ;
- validation humaine Server3 et scénario Agent multi-PC encore obligatoires ;
- aucun Generic Bridge installé automatiquement ;
- aucun port entrant ou service de contrôle distant.

## Publication éventuelle après validation humaine

- créer une prerelease, jamais une stable ;
- titre : `PinteMod Control Center v2.4.0 — Integration Preview 4B1 Fix15` ;
- joindre l’EXE unique, le ZIP du dossier et leurs SHA-256 ;
- joindre les notes `docs/RELEASE_NOTES_v2.4.0-preview-integration.4b1.fix15.md` ;
- joindre `SHA256SUMS.txt` ou publier une empreinte séparée pour chaque asset ;
- ne jamais remplacer silencieusement un asset portant le même nom.
