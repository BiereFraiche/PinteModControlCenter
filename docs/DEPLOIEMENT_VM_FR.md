# Déployer PinteMod Control Center dans une VM Windows

## Principe retenu

Le Control Center s’exécute normalement dans une VM Windows x64. Un autre PC affiche et contrôle cette VM au moyen d’une fonction déjà fournie par l’hyperviseur ou l’infrastructure : console Hyper-V/VMware/Proxmox, Bureau à distance derrière un VPN ou une passerelle sécurisée.

Le Control Center n’héberge aucun serveur de contrôle distant, n’écoute sur aucun port et ne doit jamais être directement publié sur Internet.

## Format recommandé

- `single-exe` : pratique pour un transfert ponctuel et pour l’Agent ;
- `folder` : recommandé pour une VM administrée, un antivirus strict ou un outil de déploiement qui préfère des fichiers séparés ;
- ZIP du dossier : prêt à copier puis à extraire intégralement.

Les deux formats sont autonomes et ne nécessitent pas l’installation de .NET sur la VM.

## Accès depuis un PC extérieur au réseau local

Utiliser l’une des solutions déjà administrées suivantes :

1. console distante sécurisée de l’hyperviseur ;
2. VPN vers le réseau de la VM, puis RDP ;
3. passerelle RD Gateway ou solution d’accès zéro confiance gérée par l’administrateur.

Ne jamais exposer directement sur Internet :

- RDP `3389` ;
- SMB `445` ;
- le port RCON BOIII ;
- un partage serveur ou un répertoire PinteMod ;
- un éventuel port improvisé pour le Control Center.

## Réseau VM vers serveur BOIII

- utiliser un réseau privé, un VLAN d’administration ou un VPN ;
- autoriser uniquement les flux nécessaires vers les hôtes explicitement connus ;
- pour l’Agent distant, utiliser le partage SMB déjà prévu et limiter les ACL au compte de service/opérateur ;
- ne jamais rendre SMB accessible depuis Internet ;
- ne pas activer de découverte réseau large ;
- conserver le pairing HMAC séparé pour chaque profil serveur.

## Installation

1. Créer une VM Windows 10/11 ou Windows Server avec expérience de bureau, x64.
2. Appliquer les mises à jour Windows et activer la protection antimalware.
3. Copier soit l’EXE unique, soit tout le dossier portable.
4. Lancer l’application avec un compte Windows dédié à l’exploitation.
5. Ajouter uniquement Server3 pour la première validation.
6. Si l’Agent est utilisé, effectuer le pairing depuis ce compte et ce profil Windows.
7. Créer un instantané de VM après configuration, sans inclure de secret dans une image destinée au partage.

## DPAPI et déplacement

Les secrets RCON et Agent sont liés par DPAPI au compte Windows qui les a créés. Copier l’EXE ou le dossier vers une autre VM ne transfère pas ces secrets. Après migration vers un autre compte ou une autre VM, ressaisir le secret RCON et refaire le pairing.

## Contrôle avant ouverture à distance

- le Control Center fonctionne localement dans la VM ;
- les six pages restent utilisables à la résolution distante ;
- aucun processus n’écoute pour le compte du Control Center ;
- RDP ou la console distante exige une authentification forte ;
- le pare-feu n’accepte pas RDP/SMB/RCON depuis Internet ;
- Server3 est le seul serveur autorisé pendant la Preview ;
- aucune donnée runtime, configuration locale ou secret n’est placé dans le paquet de distribution.
