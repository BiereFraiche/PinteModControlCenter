# AGENTS.md — PinteMod Control Center

## Mission
Développer une application Windows WPF/.NET 8 locale pour observer puis administrer un serveur BOIII Zombies utilisant PinteMod.

## Langue
- Répondre en français.
- Textes UI français par défaut.
- Préparer l'internationalisation future sans la rendre bloquante.

## Source de vérité
- `reference/PinteMod_v2.1.1.zip`
- `HANDOFF_PinteMod_ControlCenter_v2.2.md`
- `docs/STATUS_PREVIEW4B1_FIX16.md`
- Les fichiers de `server-sandbox/` sont uniquement une copie locale de test.
- Ne jamais considérer des données runtime comme faisant partie du produit public.

## Interdictions
- Ne jamais modifier le serveur de production.
- Ne jamais committer `server-sandbox/`.
- Ne jamais lire, afficher ou committer les secrets RCON/DPAPI.
- Ne jamais exposer un port réseau.
- Ne jamais transformer le Control Center en serveur distant ni ouvrir de port entrant.
- Pour une VM, utiliser uniquement une console d’hyperviseur ou un accès RDP/VPN déjà sécurisé hors de l’application.
- Ne jamais envoyer de commande réelle pendant les builds, tests automatisés ou audits.
- Ne jamais cibler un joueur uniquement par pseudo.
- Ne jamais modifier GitHub sans ordre explicite.

## Phase actuelle
Integration Preview 4B1 Fix16. La stable publique reste v2.2.0 et la base humaine de repli 4A7 Fix2.

## Livraison Preview attendue
1. Builds Debug et Release sans avertissement.
2. Tests Debug et Release réussis.
3. EXE autonome et dossier autonome Windows x64.
4. Capabilities fail-closed avec preuve first-party par SHA-256.
5. Aucun secret, chemin privé ou donnée runtime dans les paquets.
6. Validation humaine sur Server3 avant Server1/2.
7. Auto-diagnostic sans serveur avec rapport anonymisé et code de sortie CI.

## Architecture
Séparer :
- Domain/Core
- Infrastructure
- Presentation/WPF
- Tests

Utiliser l'injection de dépendances et des interfaces permettant de remplacer les données simulées par les sources locales réelles.

## Design
- sombre, moderne, bleu PinteMod
- lisibilité prioritaire
- vert sain, orange warning, rouge danger
- pas de surcharge visuelle
- responsive pour 1920×1080 et taille réduite
- contrôles clavier et souris
- confirmations pour futures actions dangereuses

## Qualité
Avant chaque livraison :
- compiler en Debug et Release
- exécuter les tests
- vérifier les warnings
- lister les fichiers modifiés
- expliquer les décisions importantes
