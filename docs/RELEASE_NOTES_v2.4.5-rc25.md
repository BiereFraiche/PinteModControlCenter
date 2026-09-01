# PinteMod Control Center v2.4.5-rc25

- Corrige la fermeture de l’interface : elle demande l’arrêt immédiat des services locaux, attend au maximum cinq secondes, puis se ferme afin de ne plus rester bloquée dans le Gestionnaire des tâches.
- Respecte le RCON déjà déclaré par BOIII ; une action séparée permet de le remplacer après confirmation, sans jamais afficher l’ancienne valeur.
- Un déploiement PinteMod normal ne crée plus `.pintemod-controlcenter` et ne réactive plus l’Agent distant lors de l’ouverture ou de l’enregistrement d’un serveur.
