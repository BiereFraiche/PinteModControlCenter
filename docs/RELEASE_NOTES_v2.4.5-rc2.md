# PinteMod Control Center v2.4.5-rc2

Candidate de recette finale, destinée au test unique décrit dans [la recette finale](RECETTE_FINALE_FR.md). Ce n'est pas encore la version stable publique.

- Le bouton principal prépare PinteMod puis démarre le premier BOIII sans demander le RCON avant le démarrage initial.
- Le premier RCON PinteMod crée les fichiers locaux attendus par BOIII et le bridge GeoIP, avec secret protégé par Windows.
- Le port du serveur est repris depuis `Server.bat` quand il est déclaré ; sinon la valeur BOIII habituelle est conservée.
- Les paquets incluent le test autonome et leurs sommes SHA-256.

Ne publiez ni le fichier privé de secrets ni son mot de passe dans une capture, un dépôt ou un rapport.
