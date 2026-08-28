# Préparation du workspace — Windows

## Dossiers recommandés

```text
<racine-du-dépôt>\
<racine-du-dépôt>\app\
<racine-du-dépôt>\reference\
<racine-du-dépôt>\server-sandbox\UnrankedServer\
<racine-du-dépôt>\samples\
```

## Copie du serveur

Une copie complète peut être utile pour les tests d'intégration, mais :
- elle ne doit jamais être le serveur de production ;
- elle ne doit jamais être ajoutée à Git ;
- les secrets et données joueurs doivent être retirés avant tout partage ;
- Codex doit ouvrir `<racine-du-dépôt>`, jamais le dossier de production.

Le prototype UI n'a pas besoin des binaires BOIII ni des assets du jeu.

## À placer dans `reference/`

- `PinteMod_v2.1.1.zip`

## À placer dans `design/`

- captures et maquettes validées

## À placer dans `samples/`

Uniquement des copies nettoyées :
- logs
- JSON
- sortie RCON
- configuration serveur

## Installation Codex

Dans l'application de bureau Codex :
1. ajouter le dossier `<racine-du-dépôt>`;
2. vérifier que le projet local sélectionné est le bon;
3. lancer la nouvelle tâche avec le prompt fourni;
4. examiner chaque diff avant application;
5. ne jamais autoriser une commande visant le serveur de production.
