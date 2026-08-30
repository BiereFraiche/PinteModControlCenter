# PinteMod Control Center v2.4.2

## Premier lancement sans blocage

Le premier démarrage d’un serveur PinteMod local ne requiert plus un secret RCON préexistant : le Control Center lance `Server.bat` directement. Après configuration RCON, il bascule automatiquement vers le Worker PinteMod au prochain lancement.

## Initialisation RCON protégée

Avec confirmation explicite et serveur arrêté, le premier mot de passe saisi peut créer la ligne `rcon_password` dans le fichier `.cfg` déclaré par `Server.bat`. Toute configuration RCON existante est refusée et n’est jamais écrasée. Le même secret est protégé par DPAPI pour le compte Windows courant.
