# Journal des changements

## Non publié — 2.4.0 Integration Preview 4B1 Fix15

- détection PinteMod first-party fondée sur des empreintes SHA-256 revues, jamais sur le seul nom d’un GSC ;
- capacités et transport de commandes maintenus fail-closed lorsqu’un fichier est inconnu ;
- cycle de vie désactivé lorsqu’aucun lanceur local n’est prouvé ;
- récupération de l’Agent Windows issue de Fix14 conservée ;
- versions produit, assembly et fichier alignées sur Fix15 ;
- production d’un EXE autonome et d’un dossier autonome Windows x64, avec ZIP du dossier ;
- documentation de contrôle depuis une VM via infrastructure distante existante, sans port entrant Control Center ;
- CI préparée pour tester et auditer les deux formats.

Cette version reste une Preview jusqu’à la validation humaine sur Server3 et au test multi-PC de l’Agent.

## 2.2.0 — stable publique

Première version stable publique du Control Center. Voir `docs/RELEASE_NOTES_v2.2.0.md`.
