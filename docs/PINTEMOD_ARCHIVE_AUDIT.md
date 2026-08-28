# Audit de l'archive PinteMod v2.1.1

Date : 2026-08-02

## Provenance

Le handoff historique annonçait `reference/PinteMod_v2.1.1_FINAL.zip`. L’archive disponible et auditée est :

```text
reference\PinteMod_v2.1.1.zip
```

- taille compressée : 281 410 octets ;
- SHA-256 : `CA6B8FAF5D6569454C2D8D753D4E35CF4B5EEF18F25F9BCB707E09C4E3EE517D` ;
- 79 entrées, dont 74 fichiers ;
- taille totale décompressée : 1 216 207 octets ;
- les 73 fichiers couverts par `SHA256SUMS.txt` correspondent tous ;
- divergence d'intégrité : 0 ;
- fichier attendu manquant dans le manifeste : 0.

L'archive n'a été ni extraite, ni modifiée, ni exécutée.

## Inventaire technique

- 28 modules GSC runtime, plus un exemple de profil de carte hors runtime ;
- 7 scripts PowerShell ;
- 10 lanceurs/outils BAT ;
- 3 exemples JSON ;
- documentation française et anglaise, manifestes, validation statique et procédure réelle ;
- aucun `scriptdata`, log runtime, profil joueur, record, base de bans, secret local ou `hotfix.gsc` distribué.

## Éléments utiles au Control Center

- `current_session.json` est la source de manifeste locale existante ;
- les heartbeats de Supervisor, Ban Service, GeoIP Bridge et Live Console sont sans secret ;
- la Live Console existante est strictement read-only ;
- l'identité stable est `BOIII_XUID` et les noms sont des métadonnées d'affichage ;
- le stockage protégé utilise une stratégie `.tmp` / validation / `.bak` / restauration ;
- Ranked/Unranked est sous l'autorité des GSC et protège Ranks/Records ;
- les cartes et capacités sont centralisées dans un registre conservateur ;
- aucun `control/state.json` ni `control/last_command_result.json` n'existe encore dans l'archive ; ces snapshots restent des contrats futurs.

## Contrats externes audités

Les quatre exemples de `contracts/` définissent une première version de schéma pour :

- état serveur ;
- état joueurs ;
- requête de commande en mode `simulation` ;
- résultat `simulated` indiquant qu'aucune commande n'a été envoyée.

Le prototype reprend les mêmes concepts via des modèles Core, sans lire ces fichiers au runtime.

## Contraintes conservées

- aucune source PinteMod modifiée ;
- aucun accès à la copie `server-sandbox/` ;
- aucun secret RCON/DPAPI lu ou exposé ;
- aucun port, serveur web ou accès distant ;
- aucun client RCON dans la phase 1 ;
- futures actions structurées par liste blanche et ciblage XUID ;
- modération réelle à deux comptes toujours non validée et non déclarée conforme.

## Conclusion

L'archive disponible est cohérente avec la base stable v2.1.1 décrite par le handoff et ses sommes internes sont intactes. La seule réserve de provenance est son nom sans suffixe `_FINAL`, qui nécessite une confirmation humaine avant toute future phase d'intégration réelle.
