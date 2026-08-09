# Validation humaine — MVP opérateur Local/LAN et RCON diagnostic

Date de préparation : 2026-08-09

## Limites de cette validation

- Ne lancer aucun BAT ou outil PowerShell depuis le Control Center.
- Ne modifier manuellement aucun GSC ou fichier PinteMod. Le diagnostic `ezzpausestatus` demande au module v0.3 de rafraîchir `feedback.latest.txt` et `pause.log` ; le Control Center les lit ensuite sans écriture.
- Ne tester que `ezzhealth full` et `ezzpausestatus`.
- Ne transmettre et ne capturer aucun secret RCON, XUID complet ou fichier DPAPI.
- En cas d’échec LAN, ne modifier ni pare-feu ni configuration serveur avant analyse.
- Les diagnostics RCON Health et Pause sont validés. Pour tester la vraie pause, rester seul en jeu, vivant et non à terre ; ne jamais effectuer cet essai avec d’autres joueurs.
- Depuis le PC fixe, choisir LAN et saisir le partage read-only du portable sous la forme `\\adresse-du-portable\PinteModData` ; ne pas partager toute l’installation BOIII.

## 1. Source locale read-only

1. Lancer l’exécutable Release du Control Center.
2. Ouvrir **Paramètres**.
3. Choisir **LOCAL**.
4. Saisir le chemin de votre copie de test, par exemple `<COPIE_DE_TEST>\UnrankedServer`.
5. Cliquer sur **TESTER LA SOURCE**.
6. Attendre `PRÊT` ou `PARTIEL`, sans erreur ni gel de fenêtre.
7. Cocher **Activer cette source au prochain démarrage**, puis enregistrer.
8. Fermer et rouvrir manuellement le Control Center : le bandeau doit indiquer le mode hybride sans argument de lancement.

## 2. Live Console

1. Ouvrir **Logs**.
2. Vérifier l’arrivée automatique des événements disponibles.
3. Cliquer sur **PAUSE AFFICHAGE** : les lignes visibles doivent rester fixes et le compteur de nouveaux événements doit pouvoir augmenter.
4. Cliquer sur **REPRENDRE** : les événements en attente doivent apparaître.
5. Désactiver puis réactiver **Auto-scroll**.

## 3. RCON sur serveur déjà lancé

Cette étape exige une instance BOIII/PinteMod déjà démarrée par l’opérateur. Codex ne lance pas le serveur.

1. Dans **Paramètres**, saisir l’adresse explicite : `127.0.0.1` sur la même machine, ou l’adresse LAN prévue sur un autre poste.
2. Saisir le port UDP correspondant au `net_port` BOIII.
3. Mémoriser l’adresse et le port si souhaité.
4. Saisir manuellement le secret RCON dans le champ masqué, puis cliquer sur **ENREGISTRER LE SECRET**.
5. Vérifier que le champ est vidé et que le secret n’est jamais réaffiché.
6. Cliquer sur **VÉRIFIER LA SANTÉ PINTE MOD**. Résultat attendu : `RÉUSSI`, `Commande envoyée : Oui` et retour PinteMod contenant le bilan Health.
7. Cliquer sur **VÉRIFIER L’ÉTAT DE LA PAUSE**. Résultat attendu : `RÉUSSI` et état de la pause.
8. Ouvrir **Logs**, choisir le filtre **RCON** et vérifier la présence des deux diagnostics neutralisés.
9. Ouvrir **Serveur** : la carte **Pause communautaire** doit afficher un état récent et non « Inconnu ».
10. Revenir dans **Logs** et vérifier qu’un événement **Statut Community Pause actualisé** est visible dans la catégorie **PAUSE**.

## 4. Retour à transmettre

Fournir uniquement :

- résultat du test Local : `PRÊT`, `PARTIEL` ou erreur affichée ;
- confirmation du redémarrage automatique en mode hybride ;
- résultat Health et Pause sans copier de secret ni identifiant complet ;
- capture de Paramètres avec adresse masquée si nécessaire ;
- capture du filtre RCON dans la Live Console ;
- comportement de pause/reprise et auto-scroll.

Après cette validation, la prochaine étape sera une unique commande gameplay choisie et testée de bout en bout. Aucun autre bouton réel ne doit être activé avant ce verdict.
