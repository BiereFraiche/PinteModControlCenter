PINTE MOD CONTROL CENTER v2.4.0 - ONBOARDING PREVIEW 4A2
========================================================

OBJECTIF
Rendre le premier lancement compréhensible sans connaissance de BOIII, GSC, RCON, Worker, SMB ou Bridge.

NOUVEAU PARCOURS SIMPLE
- CONFIGURER UN SERVEUR SUR CE PC
- AJOUTER UN SERVEUR DU RESEAU
- choix du dossier serveur
- analyse automatique BOIII / PinteMod / module de compatibilite / scripts tiers
- recommandation principale adaptee au resultat
- matrice FONCTIONS DISPONIBLES
- fonctions non prouvees marquees indisponibles au lieu d'etre supposees

MODE SIMPLE / MODE AVANCE
Le mode Simple est le defaut et masque les details RCON, lanceur, Agent SMB et maintenance.
Le mode Avance revele ces sections sous forme de panneaux repliables.
Le choix est memorise dans le workspace local.

SERVEUR BOIII VIDE
Le bouton INSTALLER ET PREPARER PINTE MOD installe de facon fail-closed :
- payload PinteMod courant ;
- Bridge Control Center v0.3.1 ;
- allowlist Change Map vide par defaut ;
- Agent local lorsque le serveur est sur ce PC et qu'un lanceur valide est detecte.
Aucun fichier existant different n'est ecrase.

PINTEMOD DEJA PRESENT
Le serveur est adopte tel quel. Les donnees existantes restent en place.

SCRIPTS TIERS
La Preview 4A inventorie les GSC personnalises sans les modifier.
Elle n'execute jamais une commande decouverte dans un GSC tiers.
Les pages/fonctions qui exigent une source structuree restent grisees lorsque le profil reel n'en fournit pas.
La conversion/adaptation generique des donnees et commandes est la prochaine etape 4B.

RESEAU
Un chemin UNC utilisant directement une IP locale remplit automatiquement l'adresse de controle.
Sinon l'interface Simple affiche un champ "ADRESSE LOCALE DU PC SERVEUR".
L'auto-update Agent ajoute en 3M est conserve : apres mise a jour du Control Center principal, utilisez METTRE A JOUR sur l'Agent distant.

SECURITE
- aucun scan silencieux global du PC ou du reseau ;
- aucun GSC tiers modifie ;
- aucune commande libre inventee ;
- aucune IP, XUID, mot de passe ou chemin production embarque ;
- collision first-party inconnue = refus ;
- Change Map ferme par defaut dans la preparation simple.
