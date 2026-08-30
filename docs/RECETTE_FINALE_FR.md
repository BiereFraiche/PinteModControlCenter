# Recette finale — PinteMod Control Center

Cette recette est exécutée une seule fois avant la prochaine publication finale. Elle ne demande aucun secret dans un rapport ou une capture.

## A. Serveur vierge local

1. Ouvrir le Control Center sur le PC qui héberge BOIII.
2. Choisir la racine du serveur, contenant directement `boiii`.
3. Vérifier que le bouton principal indique **PRÉPARER ET DÉMARRER**.
4. Cliquer ce bouton et confirmer.
5. Vérifier que BOIII démarre sans ouvrir manuellement un `.bat` et que PinteMod se charge dans la console.
6. Vérifier que la source PinteMod indique **PRÊT** après le lancement d’une partie.

## B. RCON et santé

1. Arrêter BOIII depuis le Control Center.
2. Dans Paramètres, initialiser le premier RCON avec un mot de passe inédit ; ne pas le partager ni le capturer. Sur PinteMod, le Control Center crée aussi la configuration locale privée et le secret Windows du bridge, sans afficher le mot de passe.
3. Redémarrer depuis le Control Center.
4. Vérifier la santé PinteMod. Une absence de texte renvoyé par BOIII doit rester distincte d’un état local PinteMod frais ; aucune action d’administration ne doit être considérée confirmée sans réponse RCON.

## C. Deux PC

1. Sur le PC serveur, autoriser une fois l’Agent local.
2. Sur le PC opérateur, enregistrer le partage réseau puis connecter le serveur.
3. Vérifier le démarrage, l’arrêt et l’actualisation depuis le PC opérateur.
4. Vérifier qu’aucun port entrant n’est créé par le Control Center et qu’aucun secret n’est affiché.

## D. VM et interface

1. Vérifier une fois les propriétés de compatibilité de `boiii.exe` : pas d’option **Exécuter en tant qu’administrateur** si un démarrage sans surveillance est souhaité.
2. Vérifier l’ouverture depuis la console VM ou RDP existant.
3. Vérifier le sélecteur 🇫🇷/🇬🇧 et la conservation du choix après redémarrage du Control Center.

## Critère de validation

La version finale est validée si A, B, C et D fonctionnent sans modification manuelle de scripts, sans écrasement de données, sans ouverture de port entrant et sans diffusion de secret.
