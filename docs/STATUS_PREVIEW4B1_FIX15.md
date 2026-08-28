# État — Integration Preview 4B1 Fix15

Date : 2026-08-28

## Positionnement

- stable publique : `v2.2.0` ;
- base Manager validée humainement : Onboarding Preview 4A7 Fix2 ;
- candidat de développement : `2.4.0-preview-integration.4b1.fix15` ;
- statut : prerelease GitHub publiée, non stable ;
- URL : `https://github.com/BiereFraiche/PinteModControlCenter/releases/tag/v2.4.0-preview-integration.4b1.fix15`.

## Contenu

Fix15 reprend Fix14 et ajoute le durcissement demandé après audit :

- PinteMod n’est reconnu comme first-party que si les fichiers cœur correspondent à des empreintes SHA-256 revues ;
- un GSC tiers reprenant un nom PinteMod reste en mode limité, sans transport de commandes ;
- la présence d’un lanceur est nécessaire pour annoncer la capacité de démarrage ;
- les deux distributions Windows x64 sont produites dans la même passe : EXE unique et dossier autonome ;
- l’accès à l’interface depuis un autre PC peut passer par une VM et une infrastructure RDP/VPN existante, sans service distant ajouté au produit.

## Validation automatisée

Le cycle obligatoire est : Restore, Build Debug, Tests Debug, Build Release, Tests Release, publication mono-EXE, publication dossier et audit des deux paquets.

Résultat local Windows/.NET 8 du 2026-08-28 :

- Build Debug : 0 avertissement, 0 erreur ;
- Tests Debug : 586/586 réussis ;
- Build Release : 0 avertissement, 0 erreur ;
- Tests Release : 586/586 réussis ;
- paquet mono-EXE : 1 fichier, audit confidentialité réussi ;
- paquet dossier : 465 fichiers, audit confidentialité réussi ;
- version Windows : `2.4.0.15` ;
- version produit : `2.4.0-preview-integration.4b1.fix15`.

Aucun résultat automatisé ne vaut validation humaine des opérations serveur.

## Empreintes des livrables locaux

- EXE : `3BE3D394316CABCD0855EA3FEF3A66F3ABD95387633BEECBADDECA9E08541463` ;
- ZIP mono-EXE : `8A751D334DB8BB6F3B3A20D1C1CDC73CE0EEF2BAC98126F72056923300354603` ;
- ZIP dossier : `84B4CAE95886A14367F9CD95BB22E2AAE0420A0D0B7C3AB2191CACF32D393E72`.

## Validation humaine encore requise

1. Interface et onboarding dans une VM ou sur un poste Windows de test.
2. Server3 en premier : BOIII natif, PinteMod connu et GSC inconnu.
3. Démarrage/arrêt local fermé.
4. Agent SMB entre deux PC déjà appairés.
5. Convergence vers la version la plus récente, y compris version égale avec SHA différent.
6. Aucun downgrade.
7. Récupération automatique de l’Agent après arrêt et mise à jour.

Server1 et Server2 restent hors périmètre tant que Server3 n’est pas validé.
