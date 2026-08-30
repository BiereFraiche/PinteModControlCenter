# Démarrer avec un serveur BOIII vierge

Le Control Center peut installer PinteMod sur une racine BOIII/Ezz existante et reconnue. Il ne fournit pas Black Ops III, BOIII/Ezz, les exécutables du jeu, les maps ni les fichiers Steam : obtenez-les et utilisez-les uniquement selon leurs licences et conditions applicables.

## Préparer une base saine

La racine choisie doit contenir `boiii/` et votre lanceur, par exemple `Server.bat`. Avant de la partager ou de l’archiver, retirez systématiquement :

- `.pintemod-controlcenter/remote-agent/` ;
- `identities/`, `boiii_players/`, les journaux `*.log` et les dumps ;
- réglages Steam locaux et tout fichier contenant un secret, un identifiant ou une donnée de session.

Ne publiez pas une archive qui contient une installation de jeu complète ou des fichiers runtime issus d’un PC personnel. Le dépôt fournit le Control Center et sa documentation, pas un miroir de serveur BOIII.

## Installer PinteMod depuis le Control Center

1. Arrêtez le serveur.
2. Dans le Control Center, créez un profil local puis sélectionnez la racine BOIII.
3. Lancez **Analyser**.
4. Sur une base sans PinteMod et sans scripts tiers, utilisez **Installer PinteMod**.
5. Redémarrez BOIII, puis lancez le diagnostic PinteMod et conservez son rapport.

Sur une installation PinteMod déjà présente, **Vérifier / Réparer PinteMod** ne modifie que le vérificateur stock connu. Les modules et services existants, notamment `PinteMod_Ban_Service.ps1`, ne sont pas remplacés.

## Licence PinteMod

PinteMod v2.1.1 est distribué sous GPLv3. Le Control Center peut embarquer l’installateur PinteMod, mais toute personne qui reçoit une copie doit aussi pouvoir obtenir le code source correspondant et la licence GPLv3. Le projet PinteMod ne peut donc pas être rendu privé tout en étant distribué à des utilisateurs via le Control Center, sauf à remettre ce code source et la licence à chaque destinataire par un autre moyen conforme.

Ce guide décrit l’intégration technique et ne remplace pas une vérification des licences BOIII/Ezz, du jeu et des dépendances que vous distribuez.
