# AGENTS.md — PinteMod Control Center

## Mission
Développer une application Windows WPF/.NET 8 locale pour observer puis administrer un serveur BOIII Zombies utilisant PinteMod.

## Langue
- Répondre en français.
- Textes UI français par défaut.
- Préparer l'internationalisation future sans la rendre bloquante.

## Source de vérité
- `reference/PinteMod_v2.1.1_FINAL.zip`
- `HANDOFF_PinteMod_ControlCenter_v2.2.md`
- Les fichiers de `server-sandbox/` sont uniquement une copie locale de test.
- Ne jamais considérer des données runtime comme faisant partie du produit public.

## Interdictions
- Ne jamais modifier le serveur de production.
- Ne jamais committer `server-sandbox/`.
- Ne jamais lire, afficher ou committer les secrets RCON/DPAPI.
- Ne jamais exposer un port réseau.
- Ne jamais ajouter d'accès distant dans la v2.2 initiale.
- Ne jamais envoyer de commande réelle pendant la phase prototype simulé.
- Ne jamais cibler un joueur uniquement par pseudo.
- Ne jamais modifier GitHub sans ordre explicite.

## Phase actuelle
Prototype graphique read-only avec données simulées.

## Première livraison attendue
1. Solution .NET 8 compilable.
2. Application WPF exécutable.
3. Dashboard sombre conforme à la référence visuelle.
4. Navigation Dashboard/Joueurs/Serveur/Records/Logs/Paramètres.
5. Données simulées injectées via services/interfaces.
6. Aucun couplage direct à RCON.
7. Tests unitaires de base.
8. README de compilation et lancement.

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
