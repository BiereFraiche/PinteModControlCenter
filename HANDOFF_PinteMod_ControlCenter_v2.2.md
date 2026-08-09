# HANDOFF — PinteMod Control Center v2.2

## 1. Source de vérité

La base serveur stable et unique est **PinteMod v2.1.1 FINAL**.

Validations réelles déjà obtenues :
- `ezzhealth full` : `PASS=51 | WARNING=0 | ERROR=0`
- suite GSC globale : `88/88 PASS | failed=0 | skipped=0`
- identité stable BOIII_XUID validée
- stockage protégé validé
- GeoIP, Ban Service, Supervisor et Live Console connectés
- Live Console anti-spam validée
- 14 profils officiels de cartes déclarés
- profil EE Origins officiel validé

La modération réelle à deux comptes est volontairement différée. Ne jamais la déclarer validée tant que cette session n'a pas été effectuée.

## 2. Objectif produit

Créer **PinteMod Control Center**, une application Windows locale, moderne et graphique pour administrer un serveur dédié BOIII Zombies utilisant PinteMod.

Première cible :
- Windows uniquement
- application locale sur la machine du serveur
- aucun accès distant
- aucun compte utilisateur
- aucun serveur web
- aucun port entrant ajouté
- RCON uniquement via `127.0.0.1`
- le secret RCON local existant ne doit jamais être affiché ni copié dans le dépôt
- les joueurs sont identifiés et ciblés par BOIII_XUID
- le pseudo reste uniquement un nom d'affichage
- les GSC restent l'autorité finale
- la Live Console PowerShell reste une solution de secours

Technologie recommandée :
- C#
- .NET 8
- WPF
- architecture testable avec séparation UI / domaine / infrastructure

## 3. Stratégie de développement obligatoire

### Phase 1 — Prototype graphique totalement simulé

Aucune lecture ni écriture sur le serveur réel.

Créer une application exécutable contenant :
- Dashboard
- carte actuelle
- code de carte
- manche
- durée de session
- nombre de joueurs
- état Ranked / Unranked
- état Supervisor / Ban Service / GeoIP / PinteMod
- flux d'événements simulés
- liste de joueurs simulés
- fiche joueur
- panneaux Atouts, Armes et Administration serveur
- thème sombre bleu PinteMod
- redimensionnement propre pour 1920×1080 et fenêtres plus petites

Aucun bouton ne doit envoyer de commande durant cette phase.

### Phase 2 — Lecture locale réelle

Lire uniquement les fichiers locaux PinteMod :
- manifeste de session
- logs de session
- heartbeats
- installation verification
- futurs snapshots `control/state.json` et `control/players.json`

Aucune commande réelle avant validation de toute la lecture.

### Phase 3 — Première commande locale contrôlée

Valider d'abord une commande sans effet gameplay :
- `ezzhealth full`

Puis une seule action simple :
- munitions sur un joueur connecté, ciblé par XUID

Ajouter progressivement les actions uniquement après validation réelle de l'étape précédente.

### Phase 4 — Administration complète

Prévoir :
- revive
- respawn
- points
- munitions
- armes
- atouts
- tous les atouts
- téléportation
- godmode
- courant
- Pack-a-Punch
- manche
- changement de carte
- musique
- événements
- power-ups
- modération
- rôles
- historique
- Ranks et Records
- Easter Egg Records
- diagnostics et exports

## 4. Principes de sécurité et fiabilité

- Ne jamais travailler directement dans le serveur de production.
- Ne jamais lancer automatiquement le serveur de production depuis les tests.
- Ne jamais committer de secret RCON, DPAPI, IP, GUID, logs privés, profils, bans ou records.
- Ne jamais construire une commande en concaténant du texte utilisateur non validé.
- Utiliser une liste blanche d'actions et d'arguments.
- Revalider le XUID et le numéro client juste avant chaque action.
- Refuser une action si le joueur s'est déconnecté ou si le slot a été réattribué.
- Les actions destructrices nécessitent une confirmation.
- Le serveur/GSC décide toujours si l'action est autorisée.
- Journaliser localement : date, action, XUID cible abrégé, commande structurée, résultat.
- Un crash du Control Center ne doit jamais arrêter BOIII.
- Le Control Center doit survivre à un changement de carte et à un redémarrage du serveur.

## 5. Direction graphique validée

Style :
- fond très sombre
- accents bleu nuit / bleu électrique
- vert réservé aux états sains
- orange pour les avertissements
- rouge uniquement pour erreurs et actions dangereuses
- cartes KPI sobres
- typographie moderne et lisible
- animations très discrètes
- interface professionnelle, pas une console PowerShell déguisée

Navigation principale :
- Dashboard
- Joueurs
- Serveur
- Records
- Logs
- Paramètres

Dashboard :
- carte et manche visibles en permanence
- joueurs connectés
- durée de session
- Ranked / Unranked
- état des services
- événements en direct
- actions rapides non destructrices

Fiche joueur :
- pseudo
- XUID abrégé
- client
- rôle
- langue
- pays
- vie / down / spectateur
- points
- présence
- boutons d'action organisés par catégories

## 6. Architecture locale recommandée

```text
PinteMod-ControlCenter/
├── app/
│   ├── src/
│   │   ├── PinteMod.ControlCenter/
│   │   ├── PinteMod.ControlCenter.Core/
│   │   └── PinteMod.ControlCenter.Infrastructure/
│   ├── tests/
│   └── PinteMod.ControlCenter.sln
├── reference/
│   └── PinteMod_v2.1.1_FINAL.zip
├── server-sandbox/
│   └── UnrankedServer/
├── samples/
├── contracts/
├── design/
├── docs/
├── AGENTS.md
└── .gitignore
```

`server-sandbox/` est une copie locale de test et doit être totalement exclue de Git.

## 7. Informations serveur utiles

Le code PinteMod public suffit pour comprendre :
- modules GSC
- commandes
- rôles
- ciblage XUID
- stockage
- logs
- outils Windows
- RCON
- heartbeats
- documentation

Pour l'intégration réelle, ajouter des exemples nettoyés :
- `Server.bat`
- `zone/server_zm.cfg`
- `current_session.json`
- heartbeats des quatre services
- `installation_verification.json`
- logs d'un démarrage
- logs join/leave
- logs d'un changement de manche
- logs Ranks/record
- sortie RCON `status`

Supprimer ou remplacer avant partage :
- mots de passe
- secrets DPAPI
- IP publiques et privées
- GUID
- XUID de joueurs tiers
- chemins personnels non nécessaires
- données de bans
- profils
- records réels
- messages Chat privés

## 8. Contrats locaux à construire

Snapshots cibles :
- `boiii/scriptdata/pintemod/control/state.json`
- `boiii/scriptdata/pintemod/control/players.json`
- `boiii/scriptdata/pintemod/control/last_command_result.json`

Ils doivent être :
- locaux
- sans secret
- écrits de manière robuste
- versionnés par schéma
- tolérants aux fichiers absents ou momentanément incomplets
- conçus pour éviter de reconstruire tout l'état à partir des logs

## 9. Règles de version et livraison

- Ne pas modifier PinteMod v2.1.1 validé pendant la phase UI simulée.
- Toute modification GSC future doit être isolée, documentée et testée.
- N'envoyer que les fichiers réellement modifiés.
- Utiliser un ZIP lorsqu'il y a plusieurs fichiers.
- Toujours indiquer la base utilisée.
- Toujours lister les tests statiques et les tests réels restant à faire.
- Ne jamais modifier GitHub sans demande explicite.
