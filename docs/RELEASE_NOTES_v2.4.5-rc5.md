# PinteMod Control Center v2.4.5-rc5

Candidate de recette finale. Elle remplace la rc4 pour le test terrain unique décrit dans [la recette finale](RECETTE_FINALE_FR.md).

- Démarrer un seul serveur via le Worker ne bloque plus si d'autres profils locaux inactifs utilisent encore le même port par défaut.
- Le contrôle de ports uniques reste obligatoire quand plusieurs BOIII sont réellement lancés ensemble.
- Sans RCON lors du tout premier lancement, BOIII et les GSC PinteMod démarrent normalement ; Supervisor, Ban Service et GeoIP Bridge attendent la configuration RCON puis le redémarrage Worker.

Ce n'est pas la version stable publique. Ne publiez ni le fichier privé de secrets ni son mot de passe.
