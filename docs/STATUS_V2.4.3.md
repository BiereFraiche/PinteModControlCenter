# État — PinteMod Control Center v2.4.3

Date : 2026-08-30

## Parcours simplifié

- **Démarrer** et **Lancer tous** peuvent préparer un premier serveur local sans secret RCON préalable ;
- le test de source PinteMod reste le contrôle principal pour confirmer qu’un serveur fonctionne ;
- un silence RCON ne masque plus cet état local : il est signalé séparément comme un RCON à vérifier, sans action automatique ni répétition de commande.

Le Control Center ne demande jamais une élévation Windows. Une demande UAC provient du lanceur BOIII choisi, de l’exécutable qu’il appelle ou de la stratégie de la machine/VM.
