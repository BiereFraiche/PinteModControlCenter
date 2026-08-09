# Prompt de reprise Codex

```text
Nous reprenons PinteMod Control Center v2.2 dans le workspace local :
E:\Dev\PinteMod-ControlCenter

Commence par lire intégralement, dans cet ordre :
1. AGENTS.md
2. HANDOFF_PinteMod_ControlCenter_v2.2.md
3. docs/CODEX_PROGRESS.md
4. docs/TODO.md
5. docs/DECISIONS.md
6. docs/QUOTA_ESTIMATE.md
7. app/README.md
8. docs/UI_FEEDBACK.md, uniquement pour détecter d’éventuels nouveaux retours humains

Baseline obligatoire :
- Preview 13 ;
- exécutable : app/artifacts/mvp-preview-13-win-x64/PinteMod.ControlCenter.exe ;
- Debug et Release : 0 avertissement, 0 erreur, 285/285 tests réussis ;
- SHA-256 du ZIP : 8ED173DEF5D67B14791433AAE1B60EBD136BA6F3963D972CE59E5D5D59D205F5.

Contraintes absolues :
- ne modifie pas reference/ ni server-sandbox/ ;
- ne lance aucun BAT, EXE serveur ou processus BOIII ;
- n’envoie aucune commande RCON réelle sans demande explicite de l’opérateur et confirmation que le serveur peut être modifié ;
- ne lis, n’affiche et ne commit aucun secret RCON/DPAPI ;
- ne modifie pas GitHub sans demande explicite ;
- ne cible jamais un joueur par pseudo ou slot : BOIII_XUID uniquement ;
- conserve les listes blanches, confirmations, absence de retry et verrous de résultat incertain ;
- conserve ISimulationActionService et CommandSent = false pour les actions simulées ;
- aucun serveur web, port entrant ou découverte réseau ;
- ne modifie pas UI_FEEDBACK.md.

Méthode demandée : intégration en lot + tests complets, pas d’ajout fonction par fonction.

À la reprise :
1. vérifie l’état des fichiers et lis les nouveaux retours humains éventuels ;
2. présente en cinq lignes maximum le lot cohérent que tu vas réaliser ;
3. poursuis directement sans micro-validation, sauf véritable risque ou besoin d’action humaine ;
4. utilise d’abord les tests ciblés pendant le développement ;
5. exécute une seule suite complète Debug et Release à la fin ;
6. mets à jour CODEX_PROGRESS.md, TODO.md et DECISIONS.md ;
7. produis un seul paquet autonome final contrôlé, sans PDB, secret, configuration opérateur, donnée serveur, GSC, BAT ou log.

Priorité de reprise :
- traiter d’abord les bugs ou retours visuels de Preview 13 ;
- vérifier les boutons de copie neutralisée, le catalogue hybride, l’historique local et le power-up joueur ;
- finaliser la V1 sur les contrats stables existants ;
- ne pas activer changement/redémarrage de carte, boss ou événements génériques tant qu’un contrat GSC sûr n’est pas établi ;
- ne pas inventer l’inventaire joueur : documenter le futur snapshot GSC read-only si nécessaire.

Quand une validation humaine devient nécessaire, regroupe-la en une seule liste simple et indique clairement si ChatGPT doit être appelé ou non.
```
