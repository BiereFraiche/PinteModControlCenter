# PinteMod Control Center v2.2.0

Première version stable publique de l’application Windows compagnon de [PinteMod](https://github.com/BiereFraiche/PinteMod).

## Points forts

- Dashboard WPF sombre, redimensionnable et utilisable jusqu’à huit serveurs isolés.
- Lecture locale ou LAN read-only : session, services, joueurs, Ranks, records, Easter Egg Records et logs structurés.
- Live Console avec filtres, recherche, pause d’affichage, auto-scroll et copie neutralisée.
- Diagnostics RCON manuels et actions serveur/joueur provenant exclusivement de listes blanches fermées.
- Ciblage joueur par BOIII_XUID interne, confirmation humaine et revalidation avant mutation.
- Redémarrage de carte, boss compatibles, nom public et mot de passe de connexion disponibles uniquement lorsque leurs contrats sûrs sont observables.
- Thème et couleur d’accent propres à chaque serveur, plus éditeur de nom public avec aperçu des couleurs BOIII.

## Installation

1. Télécharger `PinteMod-ControlCenter-v2.2.0-win-x64.zip`.
2. Vérifier facultativement son empreinte avec le fichier `.sha256` fourni.
3. Extraire entièrement l’archive dans un dossier normal.
4. Lancer `PinteMod.ControlCenter.exe`.

Le paquet est autonome pour Windows x64 : le SDK .NET n’est pas requis pour l’utiliser.

## Sécurité et comportement initial

- L’application démarre en simulation tant qu’aucune source n’est explicitement activée.
- Aucune installation n’est recherchée automatiquement.
- Aucun serveur web, port entrant, broadcast ou lancement de BOIII/BAT/EXE.
- Le secret RCON est protégé avec DPAPI pour le compte Windows courant et n’est jamais réaffiché.
- Aucune commande libre et aucun retry automatique après une livraison UDP potentiellement effectuée.
- Le Control Center n’écrit pas directement dans les données runtime PinteMod.
- Le changement générique de carte et les événements génériques restent simulés faute de contrat autoritaire.

## Validation

```text
Compilation Debug     PASS — 0 avertissement, 0 erreur
Compilation Release   PASS — 0 avertissement, 0 erreur
Tests Debug           PASS — 460/460
Tests Release         PASS — 460/460
Audit du ZIP          PASS — 471 entrées, aucun PDB, secret ou fichier runtime
Validation terrain    PASS — lectures, diagnostics, actions confirmées et net_password
```

Commit applicatif embarqué : `25e0e16b6883d77ea1e0ad91caa866aa78d25173`

SHA-256 : `B3C0368DD662C2C04B41F04ECA4D9FBC19A19CE998C86CD5923B6EE793A080A0`

Documentation complète : [README français](https://github.com/BiereFraiche/PinteModControlCenter/blob/main/README_FR.md) · [README anglais](https://github.com/BiereFraiche/PinteModControlCenter/blob/main/README.md) · [Sécurité](https://github.com/BiereFraiche/PinteModControlCenter/blob/main/SECURITY.md)
