# Publication GitHub — Integration Preview 4B1 Fix15

Statut au 2026-08-28 : PR #7 fusionnée et prerelease publiée sur
`v2.4.0-preview-integration.4b1.fix15`.

## Dépôts et rôles

- `BiereFraiche/PinteModControlCenter` : code source, CI, pull request et release du Control Center ;
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

## Publication réalisée

- prerelease, jamais stable ;
- titre : `PinteMod Control Center v2.4.0 — Integration Preview 4B1 Fix15` ;
- tag sur `c0c0c660d28fc373cdff4f6cc1196929815a96c5` ;
- assets : EXE unique, ZIP mono-EXE, ZIP dossier et `SHA256SUMS.txt` ;
- quatre digests GitHub vérifiés identiques aux fichiers locaux ;
- URL : <https://github.com/BiereFraiche/PinteModControlCenter/releases/tag/v2.4.0-preview-integration.4b1.fix15>.
