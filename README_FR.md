# PinteMod Control Center

PinteMod Control Center est l’application Windows simple pour créer, lancer et gérer un serveur **Call of Duty: Black Ops III Zombies** avec [PinteMod](https://github.com/BiereFraiche/PinteMod).

Elle est pensée pour les joueurs qui veulent un serveur fonctionnel sans devoir modifier des fichiers de configuration à la main.

[![Version](https://img.shields.io/badge/stable-v2.4.6-168BFF)](https://github.com/BiereFraiche/PinteModControlCenter/releases/tag/v2.4.6)
![Windows](https://img.shields.io/badge/plateforme-Windows-0078D4)
![Tests](https://img.shields.io/badge/tests-636%20réussis-24C875)

**[Télécharger PinteMod Control Center v2.4.6](https://github.com/BiereFraiche/PinteModControlCenter/releases/download/v2.4.6/PinteMod.ControlCenter.exe)**

[English documentation](README.md) · [Notes de version](docs/RELEASE_NOTES_v2.4.6.md)

![Interface de PinteMod Control Center](design/pintemod-control-center-reference.png)

## À quoi ça sert ?

Le Control Center rassemble les tâches importantes du serveur dans une seule interface claire :

- installer ou réparer PinteMod sur un dossier serveur BOIII/Ezz compatible ;
- définir le premier mot de passe RCON, ou le remplacer si besoin ;
- démarrer et arrêter chaque serveur depuis le même endroit ;
- voir la carte, la manche, le nombre de joueurs et l’état des services PinteMod ;
- actualiser automatiquement les données du serveur tant que l’application est ouverte ;
- modifier le port BOIII, le RCON et les messages publics récurrents dans Paramètres ;
- consulter les joueurs, records, logs et l’historique du chat local ;
- gérer plusieurs serveurs avec des onglets séparés ;
- utiliser des actions serveur et de modération guidées, seulement lorsqu’elles sont compatibles et confirmées.

L’application reste locale : pas de compte, pas de cloud et pas de nouveau port réseau entrant.

## Démarrer un serveur — pas à pas

Il faut simplement Windows, un dossier serveur BOIII/Ezz et le Control Center.

### 1. Récupérer une base serveur vierge

Si vous avez besoin d’une base BOIII/Ezz vierge prête à configurer, le téléchargement communautaire est ici :

**[Télécharger la base serveur BOIII/Ezz vierge sur Mega](https://mega.nz/folder/MYsTGa6I#gJviuei7G6XuFicNy_L9BQ)**

Décompressez-la dans un emplacement simple, par exemple `E:\Games\BOIII\MonServeur`. Utilisez et obtenez BOIII/Ezz uniquement conformément aux droits et licences applicables.

### 2. Ouvrir le Control Center

Téléchargez l’EXE ci-dessus, placez-le où vous le souhaitez puis lancez-le. Il n’y a rien à installer.

### 3. Ajouter votre serveur

Cliquez sur **+ Serveur**, puis sélectionnez le dossier du serveur : c’est le dossier qui contient directement `boiii` et votre lanceur, généralement `Server.bat`.

Cliquez sur **Analyser**. Le Control Center indique si PinteMod est déjà présent ou s’il peut préparer le serveur.

### 4. Installer PinteMod

Pour un serveur vierge, cliquez sur **Installer PinteMod** et laissez l’opération se terminer. Le flux normal de réparation ne remplace pas les données joueurs ni les scripts tiers existants.

### 5. Définir le premier RCON

Avant le premier démarrage, le Control Center peut demander un mot de passe RCON. Choisissez-en un, conservez-le précieusement puis confirmez : l’application écrit la configuration serveur nécessaire pour vous.

Il reste possible de démarrer sans RCON, mais le contrôle de santé et les actions d’administration seront indisponibles tant que vous ne l’aurez pas défini dans **Paramètres**.

### 6. Démarrer et vérifier le serveur

Cliquez sur **Démarrer le serveur**. Une fois BOIII lancé, ouvrez le dashboard puis utilisez **Contrôle de santé PinteMod**. Une installation saine indique PinteMod, Supervisor, Ban Service et GeoIP Bridge comme connectés.

C’est prêt : vous pouvez jouer sur votre serveur.

## Pourquoi l’utiliser ?

PinteMod Control Center rend un serveur riche en fonctionnalités plus accessible :

- premier paramétrage guidé, sans édition manuelle de fichiers ;
- dashboard qui montre si le serveur est réellement sain ;
- plusieurs serveurs dans une seule application, sans mélanger leurs réglages ;
- joueurs, records, chat, cartes et logs regroupés au même endroit ;
- contrôles sûrs par défaut : les opérations sensibles demandent une action volontaire et une confirmation ;
- accès LAN aux données en option pour un PC opérateur séparé, sans exposer de port de contrôle sur Internet.

## Questions fréquentes

### BOIII ou Black Ops III sont-ils inclus ?

Non. Le Control Center est une application de gestion indépendante. Il n’inclut pas le jeu, les exécutables BOIII/Ezz, les maps ni les ressources propriétaires du jeu.

### Est-ce qu’il faut les droits administrateur ?

Non. Si Windows demande une élévation au lancement du serveur, elle vient de `boiii.exe` ou de ses réglages de compatibilité Windows. Consultez la [note concernant l’élévation BOIII](docs/SERVEUR_BOIII_DEMARRAGE_FR.md#préparer-une-base-saine).

### Puis-je l’utiliser sur un autre PC ou dans une VM ?

Oui. La méthode recommandée reste une console d’hyperviseur existante ou RDP/VPN déjà sécurisé. Le Control Center lui-même n’ouvre aucun serveur web ni port de contrôle entrant. Voir le [guide VM](docs/DEPLOIEMENT_VM_FR.md).

### C’est quoi le RCON ?

Le RCON est le mot de passe privé qui permet au Control Center de demander les vérifications et actions BOIII compatibles. Ne le partagez jamais. Pour le modifier, utilisez **Paramètres → Remplacer le RCON du serveur** quand le serveur est arrêté.

## Informations techniques

La version stable actuelle est la **v2.4.6**. Le parcours serveur vierge a été validé sur le terrain : installation PinteMod, premier RCON ou remplacement confirmé, démarrage BOIII et contrôle de santé.

| Contrôle | État |
|---|---|
| Compilation Debug | PASS — 0 avertissement, 0 erreur |
| Compilation Release | PASS — 0 avertissement, 0 erreur |
| Tests automatisés | PASS — 636/636 en Debug |
| Paquets Windows | EXE autonome, dossier portable, ZIP, SHA-256 et auto-diagnostic hors ligne |

L’Agent distant optionnel n’est jamais nécessaire pour un serveur local. S’il a été activé volontairement pour le LAN, il peut être désactivé complètement depuis le Gestionnaire.

### Guides utiles

- [Guide serveur vierge](docs/SERVEUR_BOIII_DEMARRAGE_FR.md)
- [Guide de test public](docs/RECETTE_FINALE_FR.md)
- [Guide de déploiement VM](docs/DEPLOIEMENT_VM_FR.md)
- [Projet PinteMod](https://github.com/BiereFraiche/PinteMod)
- [Politique de sécurité](SECURITY.md)

Créé et maintenu par **BiereFraiche**, avec l’assistance de développement de Codex et ChatGPT.
