# Prompt de revue globale post-RC2

```text
Effectue une revue globale et bloquante du lot post-RC2 de PinteMod Control Center v2.2 à partir de l’archive de revue jointe.

BASE ET TRAÇABILITÉ

- RC2 validée et intouchée : 90d4922cb663e4b8d923ecfb1681483d78db5126.
- Branche de travail séparée : codex/post-rc2-runtime-contracts.
- Commit applicatif responsive : b57db3917693df2f73d895ba3ff0c1a6fb387829.
- La documentation de revue peut se trouver dans un commit ultérieur ne modifiant aucun code applicatif.
- Le diff complet RC2 → tête de revue et les sources exactes sont inclus dans l’archive.
- Debug : 0 avertissement, 0 erreur, 378/378 tests réussis.
- Release : 0 avertissement, 0 erreur, 378/378 tests réussis.
- Paquet Windows x64 autonome : audit packaging PASS, 466 entrées.
- Aucun fichier PinteModReal, serveur, GSC, tag ou asset RC2 n’a été modifié.

NOUVEAUTÉS À REVOIR

1. Lecture locale read-only du heartbeat global PinteMod :
   boiii/scriptdata/pintemod/health/pintemod.json.
2. Lecture locale read-only du snapshot runtime :
   boiii/scriptdata/pintemod/runtime/control_center_snapshot.json.
3. Overlay runtime pour carte, manche, durée, Ranked, joueurs, courant, Pack-a-Punch et inventaire joueur.
4. Catalogue fermé des armes standard et spéciales par carte.
5. Deux mutations joueur supplémentaires :
   - ezzpapweapon <BOIII_XUID> ;
   - ezzremoveperk <BOIII_XUID> <alias fermé>.
6. Fallback local structuré pour Carte, Courant, PAP, Manche et Joueurs lorsque BOIII répond sans texte.
7. Mise en page responsive séparant Armes, Atouts et Power-ups.

GARANTIES À PRÉSERVER

- simulation par défaut ; mode hybride seulement après choix explicite Local/LAN ;
- aucune découverte automatique d’installation ou de partage ;
- aucune lecture de secret, aucun port entrant, serveur web, processus ou retry automatique ;
- aucun texte libre transformé en commande RCON ;
- ciblage joueur exclusivement par BOIII_XUID interne revalidé après confirmation ;
- aucun XUID complet, chemin privé, IP ou GUID exposé dans l’interface ou le presse-papiers ;
- CommandSent conservateur dès qu’une émission UDP est possible ;
- verrou transversal après résultat incertain et acquittement humain obligatoire ;
- aucune écriture directe du Control Center dans PinteMod ;
- aucun fichier .tmp ou .bak utilisé comme source active ;
- confinement par racine explicite et ouverture read-only vérifiée par handle ;
- ChangeMap, RestartMap, TriggerEvent et SpawnBoss restent simulés faute de contrat stable ;
- les données absentes ou périmées restent inconnues, jamais inventées.

VÉRIFICATIONS PRIORITAIRES

1. Vérifier que heartbeat et snapshot runtime sont confinés, bornés, tolérants aux fichiers partiels et liés à la session active.
2. Vérifier que updated_at_utc vide est traité selon le contrat réel sans contourner la fraîcheur fondée sur le LastWriteTimeUtc du handle vérifié.
3. Vérifier l’invalidation du cache au changement de session et le refus d’une carte/session incohérente, future ou périmée.
4. Vérifier la priorité de l’overlay runtime frais sur les valeurs inférées depuis les logs, sans masquer une panne de lecture.
5. Vérifier que le BOIII_XUID complet reste interne et que pseudo, slot ou résultat ezzplayers ne deviennent jamais une identité d’action.
6. Vérifier que les armes spéciales ne sont proposées qu’avec une carte runtime fraîche et cohérente ; aucun alias moteur ou texte libre ne doit être accepté.
7. Vérifier les commandes ezzpapweapon et ezzremoveperk : listes fermées, confirmation, revalidation, sérialisation, zéro retry et verrou global.
8. Vérifier que les fallbacks diagnostics utilisent uniquement un snapshot local réussi, frais et cohérent, indiquent leur provenance et n’inventent jamais la sortie console.
9. Vérifier que le fallback Joueurs et les nouveaux ViewModels n’exposent aucun XUID complet ni donnée sensible.
10. Vérifier que les fonctions sans nouveau contrat PinteMod restent simulées.
11. Vérifier le XAML responsive à petite et grande largeur, sans transformer une préférence graphique facultative en blocage.
12. Vérifier l’archive binaire et son SHA-256 : aucun PDB, secret, configuration opérateur, donnée runtime, vrai XUID ou chemin privé de compilation.
13. Vérifier qu’aucun élargissement réseau, lancement BOIII/BAT/EXE serveur, écriture PinteMod ou modification GSC n’apparaît.

Ne demande pas d’activer les changements de carte, événements ou boss tant qu’un contrat PinteMod fermé, versionné et observable n’existe pas. Ne requalifie pas les risques déjà couverts par la RC2 sans identifier une régression concrète introduite par le diff post-RC2.

Réponds uniquement avec :

- Verdict final : VALIDÉ ou CORRECTIONS REQUISES ;
- Blocages obligatoires, avec fichier et ligne si possible ;
- Garanties confirmées ;
- Remarques facultatives ;
- Conclusion explicite indiquant si le lot post-RC2 peut passer à la validation terrain groupée.
```
