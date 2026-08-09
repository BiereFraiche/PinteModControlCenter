# Prompt de revue ChatGPT

```text
Effectue une revue globale et bloquante de PinteMod Control Center v2.2 à partir du ZIP de revue ou des sources que je joins.

Contexte validé :
- application locale Windows C#/.NET 8/WPF ;
- candidate corrigée v2.2.0-rc.2, remplaçant la RC1 retirée ;
- 292/292 tests réussis en Debug et Release, 0 avertissement et 0 erreur ;
- quatre blocages de la seconde revue traités : identifiants/chemins du paquet, messages d’exception publics, TOCTOU des points de réanalyse et `CommandSent` diagnostic conservateur ;
- lecture locale hybride seulement après configuration explicite ;
- aucune découverte automatique, aucun serveur web et aucun port entrant ;
- RCON uniquement sur action humaine explicite vers une IP numérique locale/privée autorisée ;
- secret protégé par DPAPI CurrentUser, jamais réaffiché ;
- ciblage joueur exclusivement par BOIII_XUID interne ;
- listes blanches fermées, confirmation avant mutation, aucun retry automatique ;
- résultat UDP incertain traité comme potentiellement envoyé et verrouillé ;
- les copies presse-papiers utilisent uniquement les textes déjà neutralisés ;
- le Control Center n’écrit directement dans aucun fichier PinteMod ;
- les écritures administratives Ban/Mute/Rôle sont effectuées par PinteMod uniquement après commande confirmée ;
- changement/redémarrage de carte, boss et événements génériques restent volontairement simulés faute de contrat stable sûr ;
- PinteMod reste « État inconnu — aucun heartbeat dédié » : sa version déclarée ne prouve pas sa santé runtime.

Vérifie en priorité :
1. absence de secret, XUID complet, IP, GUID ou chemin sensible dans les ViewModels, l’interface, le presse-papiers et les archives ;
2. confinement des lectures locales et absence de suivi de .tmp/.bak ou points de réanalyse ;
3. liste blanche RCON exacte et absence de commande libre ;
4. revalidation du BOIII_XUID après confirmation et avant envoi ;
5. sémantique conservatrice de CommandSent et verrou anti-répétition ;
6. sérialisation des opérations et arrêt propre ;
7. absence d’écriture PinteMod, de lancement de processus, de découverte réseau et de port entrant ;
8. robustesse des JSON/logs partiels, caches périmés et rotations ;
9. cohérence des états simulés, locaux, inconnus, périmés et hors ligne ;
10. validité XAML, navigation, responsivité et lisibilité ;
11. cohérence du catalogue de cartes hybride sans lecture automatique de server_zm.cfg ;
12. absence de données brutes dans les fonctions de copie ;
13. conformité du ZIP et de son manifeste SHA-256 ;
14. absence de XUID réel ou non réservé dans les données simulées/contrats et absence de chemin privé de compilation dans les assemblies applicatives ;
15. vérification de la cible réellement ouverte par handle avant lecture, avec refus contrôlé en cas d’écart ;
16. `CommandSent = true` pour toute erreur de diagnostic survenue après le début de l’appel de transport, sans retry.

Ne demande pas d’activer les fonctions volontairement simulées sans identifier d’abord un contrat sûr dans les sources stables. Ne transforme pas une préférence graphique facultative en blocage.

Réponds uniquement avec :
- Verdict final : VALIDÉ ou CORRECTIONS REQUISES ;
- Blocages obligatoires, avec fichier et ligne si possible ;
- Garanties confirmées ;
- Remarques facultatives ;
- Conclusion explicite indiquant si la V1 peut être clôturée.
```
