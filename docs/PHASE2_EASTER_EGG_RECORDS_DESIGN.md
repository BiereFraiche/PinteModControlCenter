# Phase 2.3 — Easter Egg Records officiels read-only

## Périmètre

La Phase 2.3 lit uniquement les sources officielles v2 suivantes sous le `ServerRoot` explicitement fourni au mode hybride :

- `boiii/scriptdata/pintemod/easter_eggs_v2/profiles.json` — état des profils de carte, schéma 3 ;
- `boiii/scriptdata/pintemod/easter_eggs_v2/maps/*.json` — Top 5 officiels, schéma 2.

Les dossiers `candidates/`, `test/`, `backups/`, l’ancien arbre `easter_eggs/`, les `.tmp`, les `.bak` et tous les logs sont exclus. Un candidat n’est jamais présenté comme un record officiel.

## Autorité observée

Le module GSC validé déclare `easter_eggs_v2` comme racine active, `profiles.json` en schéma 3 et les cartes en schéma 2. Une écriture officielle n’est autorisée que pour une carte dont le statut est explicitement `OFFICIAL`. Le ciblage et les titulaires reposent sur `BOIII_XUID`.

Dans la copie de test désignée, `zm_tomb` est `OFFICIAL`, mais aucun fichier n’est présent dans `easter_eggs_v2/maps/`. Le fichier sous `candidates/maps/` n’est pas une donnée officielle. Le résultat réel attendu est donc zéro Easter Egg Record officiel, avec une source locale lisible et une explication claire — pas le record simulé historique.

## Architecture ciblée

- `IEasterEggRecordReader` expose une lecture asynchrone et annulable.
- `EasterEggRecordReader` valide le profil de carte puis les fichiers officiels directs.
- `EasterEggRecordsPathPolicy` confine les deux sources sous `ServerRoot` et refuse les liens/jonctions.
- `EasterEggRecordsOverlayDataProvider` enveloppe la baseline Phase 2.2 et remplace uniquement les Easter Egg Records simulés.
- `EasterEggRecordsSnapshot` porte séparément les records, la métadonnée de lecture, les compteurs et le nombre de profils officiels.

Les lecteurs Phase 2.1 et le fournisseur Phase 2.2 ne sont pas modifiés.

## Validation des données

Le profil doit être un objet JSON, utiliser le schéma 3, `identity_kind=BOIII_XUID` et `official_mode=per_map_validated_only`. Seules les cartes au statut exact `OFFICIAL` autorisent la lecture d’un record.

Chaque fichier carte doit :

- être un JSON direct de `maps/` dont le nom est un code carte sûr ;
- utiliser le schéma 2, `identity_kind=BOIII_XUID` et `mode=official` ;
- déclarer un code carte identique au nom de fichier ;
- correspondre à un profil `OFFICIAL`.

Chaque emplacement 1–4 joueurs / Top 1–5 est isolé. Une entrée invalide est ignorée sans supprimer les voisines valides. Les titulaires doivent fournir entre un et le nombre de joueurs de la catégorie en BOIII_XUID valides et uniques ; cette règle couvre les quêtes fixes 4P pouvant créditer seulement deux titulaires actifs.

## Tolérance et fraîcheur

Les records sont historiques : une lecture valide reste `Fresh` quelle que soit l’ancienneté du fichier, tandis que l’âge est affiché séparément. L’absence du dossier `maps/` ou l’absence de fichiers officiels est un catalogue valide vide si `profiles.json` est valide. Une erreur ultérieure conserve la dernière valeur valide en mémoire avec provenance `MemoryCache` et fraîcheur `Stale`.

Les fichiers sont lus avec partage lecture/écriture/suppression, longueur bornée, JSON complet requis et contrôle taille/date avant et après lecture afin de tolérer les remplacements atomiques et de refuser un fichier en cours de modification.

## Confidentialité et sécurité

Les XUID complets restent dans les modèles métier et le lecteur. Les ViewModels n’exposent que des formes abrégées. Aucun contenu runtime n’est ajouté au dépôt ou aux paquets de revue.

La phase n’ajoute aucun RCON, réseau, secret, port, processus, écriture PinteMod ou modification GSC.

## Tests prévus

- profil valide, absent, vide, tronqué, schéma incompatible et statut non officiel ;
- dossier de cartes absent ou vide accepté comme catalogue officiel vide ;
- fichier officiel valide, incohérent ou hors liste officielle ;
- isolation d’un emplacement invalide ;
- XUID invalide, dupliqué ou trop nombreux ;
- exclusion de `candidates/`, `test/`, `.tmp`, `.bak` et de l’ancien arbre ;
- repli mémoire après erreur ;
- confinement et refus des reparse points ;
- intégrité taille/date/SHA-256 avant et après lecture ;
- superposition limitée aux Easter Egg Records et retrait de la simulation en mode hybride ;
- absence de XUID complet dans les ViewModels et XAML.
