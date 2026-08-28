# PinteMod Control Center v2.4.0 — Integration Preview 4B1 Fix15

> Candidate Preview. Ne pas présenter comme stable avant validation humaine Server3 et Agent multi-PC.

## Principaux changements

- Manager multi-serveurs et Agent distant SMB/HMAC ;
- provider adaptatif pour PinteMod, BOIII natif et GSC tiers ;
- aucune donnée simulée injectée dans un profil serveur réel ;
- PinteMod first-party prouvé par SHA-256 avant activation du transport de commandes ;
- commandes tierces seulement observées par audit statique borné, jamais exécutées ;
- récupération automatique de l’Agent Windows ;
- icône originale multi-résolution intégrée ;
- EXE unique autonome et dossier autonome Windows x64 ;
- guide de déploiement VM sans port entrant Control Center.

## Sécurité

- aucun raw shell distant, PsExec, WinRM, serveur HTTP ou nouveau port ;
- secrets protégés par DPAPI et messages Agent signés HMAC ;
- aucune mutation activée à partir du seul nom d’un fichier GSC ;
- joueurs ciblés par identifiant stable, jamais uniquement par pseudo ;
- aucune donnée runtime ou configuration locale dans les distributions.

## Validation requise avant prerelease

- cycle automatisé local réussi : 0 avertissement/erreur, 586/586 tests Debug et Release, audits des deux paquets réussis ;
- test de l’interface et du redimensionnement ;
- Server3 avant Server1/2 ;
- mise à jour Agent bidirectionnelle, même version/SHA différent, aucun downgrade et récupération après arrêt.

Empreintes locales de préparation : voir `docs/STATUS_PREVIEW4B1_FIX15.md` et `app/artifacts/integration-preview4b1-fix15-win-x64/SHA256SUMS.txt`.
