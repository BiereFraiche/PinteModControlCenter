# Journal des changements

## 2.4.0 — stable publique

- l’absence confirmée de `hotfix.gsc` dans les distributions Ezz BOIII actuelles est désormais un état attendu du vérificateur, sans avertissement ni recommandation de téléchargement.
- la réparation d’une installation PinteMod déjà détectée cible uniquement le vérificateur stock connu ; aucun module ni service tel que `PinteMod_Ban_Service.ps1` n’est remplacé.

## 2.4.0 Integration Preview 4B1 Fix23

- rend accessible sur une installation PinteMod détectée la vérification/réparation first-party ; seul le vérificateur v2.1.1 stock connu peut être mis à jour, toute autre collision reste refusée.

## 2.4.0 Integration Preview 4B1 Fix22

- le format dossier inclut désormais un EXE Agent autonome vérifié ; l’activation locale et la synchronisation distante ne copient plus l’EXE léger du dossier sans ses DLL.

## 2.4.0 Integration Preview 4B1 Fix21

- le lancement manuel de réparation de l’Agent ne peut plus être bloqué par un ancien marqueur de mise à jour ; la tâche Windows automatique conserve sa protection normale durant une mise à jour.

## 2.4.0 Integration Preview 4B1 Fix20

- remplace l’erreur générique d’activation de l’Agent par un diagnostic local exploitable sans afficher de chemin privé, de secret ou de détail DPAPI ;
- confirme explicitement qu’aucun ordre BOIII n’est envoyé lorsqu’une préparation locale échoue.

## 2.4.0 Integration Preview 4B1 Fix19

- un ancien ordre d’arrêt ou marqueur de mise à jour ne peut plus faire quitter immédiatement un Agent nouvellement lancé ;

## 2.4.0 Integration Preview 4B1 Fix18

- ajoute l’argument `--software-rendering` pour forcer le rendu logiciel WPF sur les PC, GPU ou prises en main distante qui affichent une fenêtre blanche.

## 2.4.0 Integration Preview 4B1 Fix17

- accepte les retours RCON Community Pause v0.3 et v0.4 grâce aux marqueurs de contrat stables ;
- corrige le vérificateur embarqué : inventaires first-party 28 (base) et 35 (actuel) reconnus ;
- met à niveau uniquement le vérificateur v2.1.1 stock dont l’empreinte SHA-256 est connue ; toute variante modifiée reste refusée ;
- documente l’absence de `hotfix.gsc` comme avertissement BOIII optionnel, hors distribution PinteMod ;
- ajoute les tests de compatibilité Pause v0.4 et inventaire embarqué actuel.

Cette version reste une Preview jusqu’à la validation humaine sur Server3 et au test multi-PC de l’Agent.

## 2.4.0 Integration Preview 4B1 Fix16

- auto-diagnostic local exécutable sans serveur avec code de sortie exploitable par la CI ;
- vérification de la version produit, des assemblages, des six vues WPF et des payloads embarqués ;
- rapport texte anonymisé `SELF-TEST.txt`, inclus dans les empreintes de livraison ;
- bouton de lancement et copie du rapport depuis Paramètres ;
- aucun chargement de profil serveur, secret DPAPI, accès réseau ou transport RCON pendant le self-test ;
- CI et build local alignés sur 596 tests et les deux formats Windows x64.

Cette version reste une Preview jusqu’à la validation humaine sur Server3 et au test multi-PC de l’Agent.

## 2.4.0 Integration Preview 4B1 Fix15 — prerelease GitHub

- détection PinteMod first-party fondée sur des empreintes SHA-256 revues, jamais sur le seul nom d’un GSC ;
- capacités et transport de commandes maintenus fail-closed lorsqu’un fichier est inconnu ;
- cycle de vie désactivé lorsqu’aucun lanceur local n’est prouvé ;
- récupération de l’Agent Windows issue de Fix14 conservée ;
- versions produit, assembly et fichier alignées sur Fix15 ;
- production d’un EXE autonome et d’un dossier autonome Windows x64, avec ZIP du dossier ;
- documentation de contrôle depuis une VM via infrastructure distante existante, sans port entrant Control Center ;
- CI préparée pour tester et auditer les deux formats.

Cette version a été fusionnée sur `main` par la PR #7 puis publiée comme prerelease `v2.4.0-preview-integration.4b1.fix15`. Elle reste une Preview jusqu’à la validation humaine sur Server3 et au test multi-PC de l’Agent.

## 2.2.0 — stable publique

Première version stable publique du Control Center. Voir `docs/RELEASE_NOTES_v2.2.0.md`.
