# PinteMod Control Center v2.2.0-rc.2

Date : 2026-08-09.

Cette candidate remplace intégralement la RC1 retirée après une seconde revue plus stricte.

## Corrections finales

- identifiants simulés et contrats remplacés par des valeurs réservées manifestement fictives ;
- publication Release sans symboles ni chemin privé de compilation dans les assemblies applicatives ;
- messages publics des lecteurs fermés et génériques, sans `Exception.Message` système ;
- ouverture read-only par handle, vérification de la cible réellement ouverte avant toute lecture et refus contrôlé en cas d’écart ;
- diagnostic RCON conservateur : toute erreur après le début de l’appel de transport conserve `CommandSent = true` ;
- aucune nouvelle commande, fonction métier, écriture PinteMod, modification GSC ou tentative RCON réelle.

## Validation automatisée

- Debug : 0 avertissement, 0 erreur, 292/292 tests réussis ;
- Release : 0 avertissement, 0 erreur, 292/292 tests réussis ;
- publication autonome Windows x64 : réussie ;
- archive : 466 entrées ;
- aucun PDB, secret, configuration opérateur, fichier serveur, donnée runtime, chemin ZIP dangereux, ancien XUID interdit ou chemin privé de compilation ;
- SHA-256 : `2C30BB4BBB3F73DB15588D78518F94914FAB87B2EDA34364B9CEB8E8B5C58124`.

## Validation restante

La RC2 doit recevoir la revue indépendante de clôture. La validation terrain groupée des mutations qui ne sont pas encore toutes observées reste le jalon opérationnel avant le tag stable `v2.2.0`.
