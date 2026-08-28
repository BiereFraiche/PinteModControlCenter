# État — Integration Preview 4B1 Fix16

Date : 2026-08-28

## Positionnement

- stable publique : `v2.2.0` ;
- Preview précédente fusionnée et publiée : Fix15, PR #7 et prerelease GitHub ;
- candidat de développement : `2.4.0-preview-integration.4b1.fix16` ;
- statut : Preview, non stable.

## Contenu

Fix16 ajoute une validation locale utile lorsqu’aucune VM ou installation BOIII de test n’est disponible :

- mode de démarrage `--self-test` avec code de sortie `0` ou `8` ;
- rapport texte anonymisé et borné ;
- contrôle de la version produit, des assemblages Core/Infrastructure/WPF et des six pages ;
- extraction temporaire de PinteMod et du Bridge embarqués, puis reconnaissance par les règles SHA-256 first-party ;
- suppression de la racine temporaire exacte après le contrôle ;
- bouton équivalent dans Paramètres avec copie du rapport ;
- exécution obligatoire dans le build local et la CI avant packaging.

Le chemin self-test s’arrête avant la création du Manager. Il ne charge donc aucun profil serveur, secret DPAPI, paramètre réseau, service Agent ou transport RCON.

## Validation automatisée

Résultat local Windows/.NET 8 du 2026-08-28 :

- Build Debug : 0 avertissement, 0 erreur ;
- Tests Debug : 596/596 réussis ;
- Build Release : 0 avertissement, 0 erreur ;
- Tests Release : 596/596 réussis ;
- tests ciblés Fix16 : 23/23 réussis ;
- auto-diagnostic dossier : `RESULTAT=PASS` ;
- auto-diagnostic mono-EXE : `RESULTAT=PASS` ;
- paquet mono-EXE : 1 fichier, audit confidentialité réussi ;
- paquet dossier : 465 fichiers, audit confidentialité réussi ;
- version Windows : `2.4.0.16` ;
- version produit : `2.4.0-preview-integration.4b1.fix16`.

## Empreintes des livrables locaux

- EXE : `DB0ABD4AF44A547D4F7407D7A2DA216925151AE8AF7C85ECD3B06E6D1DB42A5E` ;
- ZIP mono-EXE : `51F89CC849DAFD2AC6F1C060930D394EB1F64D9447A40DBFC588A80CD7CEE3D3` ;
- ZIP dossier : `3731000FE254E918029337AC8B566E1458D66ABA7D55DF888B24E1DE48BB2A8D` ;
- rapport `SELF-TEST.txt` : `C429ADA224AD87AC83B73053786CF7B81C1A7B4D64036296F389E31F624FBB69`.

Aucun résultat automatisé ne vaut validation humaine des opérations serveur.

## Validation humaine encore requise

1. Lisibilité du panneau Auto-diagnostic sur un poste ou une VM Windows disponible.
2. Server3 en premier : BOIII natif, PinteMod connu et GSC inconnu.
3. Démarrage/arrêt local fermé.
4. Agent SMB entre deux PC déjà appairés.
5. Convergence vers la version la plus récente, y compris version égale avec SHA différent.
6. Aucun downgrade.
7. Récupération automatique de l’Agent après arrêt et mise à jour.

Server1 et Server2 restent hors périmètre tant que Server3 n’est pas validé.
