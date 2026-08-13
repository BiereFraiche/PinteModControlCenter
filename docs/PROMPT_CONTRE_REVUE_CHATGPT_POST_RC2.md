# Prompt de contre-revue ciblée post-RC2

```text
Effectue uniquement la contre-revue des deux blocages du verdict précédent, à partir du ZIP joint.

BASE

- Paquet précédemment revu : révision applicative b57db3917693df2f73d895ba3ff0c1a6fb387829.
- Correctif applicatif : 0e4e09284ab8523dc1bb86ce4f162c1aae6ee0ac.
- Les seuls fichiers de production modifiés sont :
  - app/src/PinteMod.ControlCenter.Infrastructure/Local/ReadOnlyJsonFileReader.cs ;
  - app/src/PinteMod.ControlCenter.Infrastructure/Local/VerifiedReadOnlyFile.cs.
- Tests modifiés :
  - app/tests/PinteMod.ControlCenter.Tests/VerifiedReadOnlyFileTests.cs.
- Debug : 0 avertissement, 0 erreur, 381/381 tests réussis.
- Release : 0 avertissement, 0 erreur, 381/381 tests réussis.
- Paquet Windows x64 autonome : 466 entrées, audit PASS.

BLOCAGES À VÉRIFIER

1. La lecture doit consommer au maximum maximumFileSizeBytes + 1 octets, même si le fichier grossit après le contrôle initial. La détection du dépassement doit survenir avant parsing et sans CopyToAsync jusqu’à EOF.
2. Longueur et LastWriteTimeUtc utilisés avant/après lecture et pour la fraîcheur doivent provenir du même handle réellement ouvert et vérifié par VerifiedReadOnlyFile. Un remplacement du chemin après ouverture ne doit pas associer les octets de l’ancien handle aux métadonnées du nouveau fichier.

TESTS À VÉRIFIER

- une copie depuis une source plus longue s’arrête exactement à la borne ;
- un fichier agrandi après la lecture des métadonnées initiales est plafonné puis refusé ;
- si le chemin est remplacé après l’ouverture, le résultat conserve les octets et la date du handle original ;
- les suites heartbeat/runtime et la régression complète restent vertes.

NON-RÉGRESSION

- aucun retour à FileInfo(path), File.GetLastWriteTimeUtc(path) ou File.Exists(path) dans ReadOnlyJsonFileReader ;
- états Missing, AccessDenied, Invalid et IoError restent contrôlés et sans message système brut ;
- limites 4 Kio heartbeat et 32 Kio runtime inchangées ;
- aucun changement RCON, réseau, GSC, ViewModel, XAML ou écriture PinteMod ;
- paquet sans PDB, secret, configuration opérateur, donnée runtime ou chemin privé.

Ne relance pas une revue générale et ne demande aucune validation terrain pour ce correctif de lecteur.

Réponds uniquement avec :

- Verdict final : VALIDÉ ou CORRECTIONS REQUISES ;
- Blocages obligatoires restants ;
- Garanties confirmées ;
- Résultats de preuves vérifiés ;
- Conclusion explicite : LOT POST-RC2 AUTORISÉ ou NON AUTORISÉ à passer à la validation terrain groupée.
```
