# PinteMod Control Center v2.4.5-rc3

Candidate de recette finale. Elle remplace la rc2 pour le test terrain unique décrit dans [la recette finale](RECETTE_FINALE_FR.md).

- Le premier RCON PinteMod s'arrête proprement avant toute modification si Windows ne peut pas protéger le secret local du bridge.
- En cas d'échec lors de la finalisation, les nouveaux fichiers privés temporaires sont retirés et la configuration GeoIP précédente est restaurée.
- Le parcours guidé, les deux formats portables, les sommes SHA-256 et l'auto-diagnostic restent identiques.

Ce n'est pas la version stable publique. Ne publiez ni le fichier privé de secrets ni son mot de passe.
