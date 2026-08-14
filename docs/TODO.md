# TODO — PinteMod Control Center

Dernière mise à jour : 2026-08-12

## Interface graphique

- **Terminé** — Construire le shell WPF sombre et la navigation des six sections.
- **Terminé** — Construire le Dashboard, la sélection joueur et les panneaux d'actions simulées.
- **Terminé** — Réaliser Serveur, Records, Logs et Paramètres.
- **Terminé** — Comparer le rendu final à `design/pintemod-control-center-reference.png` ; direction graphique validée par la revue humaine finale.
- **Terminé** — Produire les captures du Dashboard, de la fiche joueur et du panneau Serveur.
- **Terminé** — Vérifier l'ouverture, la navigation et la fermeture de l'application WPF.
- **Terminé** — Valider la seconde passe aux formats 1920×1080, 1060×840 et 900×640 sur les six pages.
- **Terminé** — Produire six nouvelles captures et les captures Warning / Offline-Error.
- **Terminé** — Faire relire les captures de `app/artifacts/screenshots/review-2/` par ChatGPT.
- **Terminé** — Préparer le paquet autonome `app/artifacts/PinteMod-ControlCenter-ChatGPT-Review-2.zip` et son prompt de revue.
- **Terminé** — Corriger la sémantique de `UNRANKED` : orange sur Dashboard et Records, avec capture de validation 1060×840.
- **Terminé** — Rendre la fenêtre déplaçable depuis tout le bandeau supérieur et préserver les quatre boutons interactifs du chrome.
- **Terminé** — Préparer `app/artifacts/PinteMod-ControlCenter-ChatGPT-Final-Review.zip` avec les dernières corrections et son prompt final.
- **Terminé** — Phase visuelle validée et clôturée par la revue humaine.
- **Terminé** — Phase 2.2 : page Records hybride validée visuellement avec 34 profils conformes, 3 fichiers ignorés et 7 records locaux.

## Architecture

- **Terminé** — Séparer Core, Infrastructure, Présentation WPF et Tests.
- **Terminé** — Définir les interfaces de données et d'actions simulées.
- **Terminé** — Finaliser les ViewModels testables et le composition root par injection constructeur.
- **Terminé** — Partager le snapshot entre les six pages et la sélection joueur par XUID.
- **Terminé** — Centraliser la capture d’erreur des commandes asynchrones et de l’initialisation.

## Données simulées

- **Terminé** — Fournir une session Origins réaliste, cinq services, quatre joueurs, événements et records.
- **Terminé** — Garantir que les actions restent des simulations sans transport.
- **Terminé** — Clôturer les ajustements obligatoires issus du retour visuel humain final.
- **Terminé** — Ajouter les scénarios Warning, Offline/Error, serveur arrêté, Unranked et jeu vide.

## Lecture locale future

- **Terminé** — Phase 2.1 : lecteur tolérant et read-only de `current_session.json`.
- **Terminé** — Phase 2.1 : lecteurs des heartbeats Supervisor, Ban Service, GeoIP Bridge et Live Console.
- **Terminé** — Séparer état déclaré, état de lecture, fraîcheur, âge et provenance.
- **Terminé** — Ajouter le fournisseur hybride explicite sans remplacer le mode simulé par défaut.
- **Terminé** — Mode hybride validé humainement sur une copie locale dédiée `<COPIE_DE_TEST>\UnrankedServer` ; serveur fonctionnel intact.
- **Terminé** — Phase 2.2 techniquement et visuellement validée, puis clôturée par verdict ChatGPT sans blocage restant.
- **Terminé** — Inventorier les schémas actifs sans exposer les valeurs personnelles : 37 profils v2, 1 carte v4, conventions de séparation confirmées.
- **Terminé** — Ajouter les lecteurs read-only des profils Ranks v2 et records de manches v4 avec cache, provenance, limites et exclusion des sources non actives.
- **Terminé** — Superposer les données Phase 2.2 sans modifier les lecteurs validés de session et heartbeats.
- **Terminé** — Phase 2.3 validée localement : 1 profil officiel, 0 fichier/record officiel et aucun candidat affiché.
- **Terminé** — Phase 2.3 clôturée sans condition restante après verdict externe.
- **Terminé** — Bloc A : logs locaux structurés de la session active, lecture incrémentale, filtrage et cache borné.
- **Terminé** — Auditer et consommer le heartbeat global et le snapshot runtime désormais fournis par PinteModReal.
- **Terminé** — Confirmer sur le commit GitHub stable qu’aucun snapshot local persistant ne fournit l’inventaire courant ; documenter l’instantané interne de réanimation comme non consommable.
- **Terminé** — Intégrer le snapshot GSC read-only d’inventaire par BOIII_XUID produit par le bridge runtime v0.1.2, sans modifier PinteMod.

## RCON diagnostics et commandes

- **Terminé** — Concevoir un transport UDP isolé pour une adresse et un port explicitement configurés, limité au poste serveur ou au LAN.
- **Terminé** — Protéger le secret propre au Control Center avec DPAPI `CurrentUser`, sans rechercher le secret PowerShell existant ni le réafficher.
- **Terminé** — Validation terrain de `ezzhealth full` : `PASS=51 | WARNING=0 | ERROR=0` sur le serveur réel vide.
- **Terminé** — Validation terrain de `ezzpausestatus` : Community Pause v0.3, inactive, compteur `0/2`.
- **Terminé** — Lever le verrou de la première commande gameplay après preuve console des deux diagnostics.
- **Terminé** — Valider sémantiquement les réponses des deux diagnostics : une réponse non vide mais incompatible ne peut plus être affichée comme réussie.
- **Terminé** — Conserver la liste blanche RCON strictement limitée à `ezzhealth full` et `ezzpausestatus`.
- **Terminé** — Refuser dans le service simulé toute valeur d’action hors liste blanche et toute option contenant des caractères de contrôle, avec `CommandSent = false`.
- **Terminé** — Revalider la présence du même BOIII_XUID après confirmation et juste avant toute action joueur réelle ; pseudo et slot ne servent jamais de clé.
- **Terminé** — L’interdiction RCON des Phases 2 et du Bloc A a été respectée jusqu’à leur clôture.

## Tests

- **Terminé** — Écrire les tests de validation XUID, du fournisseur simulé et des actions simulées.
- **Terminé** — SDK officiel .NET 8.0.423 installé durablement sur la machine pour les validations locales régulières.
- **Terminé** — Utiliser temporairement le SDK officiel .NET 8.0.423 pour la validation locale.
- **Terminé** — Ajouter les tests de ViewModels, navigation, sélection partagée, états métier, filtres et paramètres.
- **Terminé** — Exécuter Debug et Release sans warning ni erreur.
- **Terminé** — Exécuter 11 tests en Debug et Release, tous réussis.
- **Terminé** — Exécuter la suite étendue de 26 tests en Debug et Release, tous réussis.
- **Terminé** — Vérifier la syntaxe XML et l'absence d'API réseau/processus/écriture dans les sources applicatives.
- **Terminé** — Tester les fichiers valides, absents, vides, tronqués, verrouillés, périmés et les schémas incompatibles.
- **Terminé** — Vérifier le confinement sous ServerRoot et l’intégrité taille/date/hash avant et après lecture.
- **Terminé** — Vérifier que le fournisseur hybride ne remplace que les champs autorisés et laisse les autres données simulées.
- **Terminé** — Exécuter la suite étendue de phase 2.1 : 61/61 tests réussis en Debug et Release.
- **Terminé** — Exécuter la suite Phase 2.2 : 75/75 tests réussis en Debug et Release, 0 avertissement et 0 erreur.
- **Terminé** — Exécuter la suite Phase 2.3 : 92/92 tests réussis en Debug et Release, 0 avertissement et 0 erreur.
- **Terminé** — Valider ponctuellement la copie de test : compatibilité des lecteurs et intégrité taille/date/SHA-256 avant/après.
- **Terminé** — Valider le lot MVP opérateur en Debug et Release : 0 avertissement, 0 erreur, 148/148 tests dans chaque configuration.
- **Terminé** — Tester le protocole RCON sur boucle UDP locale sans serveur BOIII, ainsi que DPAPI, liste blanche, timeout et neutralisation.
- **Terminé** — Corriger le crash WPF de Paramètres en imposant une liaison `OneWay` sur `RconResponse` et ajouter un test runtime STA de la vue.
- **Terminé** — Revalider après correctif : 149/149 tests réussis en Debug et Release, 0 avertissement et 0 erreur.
- **Terminé** — Valider l’intégration Community Pause read-only et sa préparation UI : 160/160 tests réussis en Debug et Release, 0 avertissement et 0 erreur.
- **Terminé** — Validation humaine : la page Paramètres corrigée s’ouvre sans crash dans l’exécutable Release.
- **Terminé** — Corriger les boutons et le champ secret blanc sur blanc dans Paramètres et la Live Console.
- **Terminé** — Ajouter une aide RCON en trois étapes, facultative et compréhensible sans connaissance du protocole.
- **Terminé** — Validation humaine : contraste et compréhension de la carte RCON remaniée acceptés.
- **Terminé** — Diagnostics RCON réels Carte, Courant, Pack-a-Punch, Manche et Joueurs observés dans la console BOIII.
- **Terminé** — Vérifier sur le dépôt GitHub à jour le contrat Community Soft Pause v0.3 : `ezzpausestatus`, `ezzpauseforce`, `ezzresume`, `feedback.latest.txt` et `pause.log`.
- **Terminé** — Intégrer en lecture seule le statut Community Pause et les nouveaux événements de pause, sans exposer de XUID.
- **Terminé** — Revalider le lot après durcissement des diagnostics : Debug/Release sans avertissement ni erreur, 166/166 tests dans chaque configuration.
- **Terminé** — Valider l’activation sécurisée Pause/Reprendre : Debug/Release sans avertissement ni erreur, 176/176 tests dans chaque configuration.

## Documentation

- **Terminé** — Maintenir `CODEX_PROGRESS.md`, `TODO.md` et `DECISIONS.md` pour la première livraison.
- **Terminé** — Ajouter `app/README.md` avec les instructions de compilation et lancement.
- **Terminé** — Fournir l'inventaire final exact des fichiers.
- **Terminé** — Documenter l’architecture et les garanties de lecture locale dans `docs/PHASE2_LOCAL_READ_DESIGN.md`.
- **Terminé** — Produire `PinteMod-ControlCenter-ChatGPT-Phase2.1-Review.zip` et son prompt autonome.
- **Terminé** — Verdict ChatGPT obtenu : Phase 2.1 validée et clôturée sans correction bloquante.
- **Terminé** — Documenter la Phase 2.2 dans `docs/PHASE2_RANKS_RECORDS_DESIGN.md` et mettre à jour le README.
- **Terminé** — Documenter la Phase 2.3 dans `docs/PHASE2_EASTER_EGG_RECORDS_DESIGN.md` et mettre à jour le README.
- **Terminé** — Préparer `app/artifacts/PinteMod-ControlCenter-ChatGPT-Phase2.3-Review.zip` avec prompt, preuves et captures avant/après.
- **Terminé** — Préparer `app/artifacts/PinteMod-ControlCenter-ChatGPT-Phase2.2-Review.zip` et son prompt autonome.
- **Terminé** — Verdict ChatGPT obtenu : corrections validées et Phase 2.2 clôturée sans condition restante.
- **Terminé** — Corriger l’isolation de toute entrée de record invalide sans perdre les entrées valides du fichier carte.
- **Terminé** — Supprimer l’exposition et les info-bulles de XUID complet dans les ViewModels WPF.
- **Terminé** — Corriger les durées supérieures à 24 heures avec affichage des heures totales.
- **Terminé** — Exécuter les 79 tests de régression en Debug et Release, 0 avertissement et 0 erreur.
- **Terminé** — Générer le nouveau ZIP de revue Phase 2.2 après corrections bloquantes, avec manifeste SHA-256 vérifié.
- **Terminé** — Paquet corrigé relu par ChatGPT : 0 blocage restant, 79/79 tests confirmés en Debug et Release.
- **Terminé** — Documenter l’estimation de quota et distinguer la clôture V1 des futures extensions PinteMod dans `docs/QUOTA_ESTIMATE.md`.
- **Terminé** — Préparer les prompts autonomes de reprise Codex et de revue globale ChatGPT.
- **Terminé** — Formaliser dans `docs/PINTEMOD_REQUIREMENTS_NEXT.md` les sources et contrats PinteMod nécessaires aux extensions avancées.

## Intégration PinteMod

- **Terminé** — Auditer l'archive disponible sans extraction ni exécution.
- **À valider** — Confirmer que `reference/PinteMod_v2.1.1.zip` correspond bien à l'archive FINAL attendue.
- **Terminé** — Intégration locale read-only limitée au manifeste de session et aux quatre heartbeats.
- **Terminé** — Intégration strictement read-only des profils `ranks_v2/players` et records `ranks_v2/maps`, limitée aux JSON actifs directs.
- **Terminé** — Intégration strictement read-only des profils et Top 5 officiels `easter_eggs_v2`, sans candidats, tests, sauvegardes ni ancien format.
- **Terminé** — Le Bloc A a respecté l’interdiction de toute écriture, RCON, réseau, processus, secret ou modification GSC.
- **Terminé** — Confirmer depuis la source GitHub v0.3 le chemin et le format de `feedback.latest.txt` et `pause.log`, puis les intégrer sans supposer leur présence dans la copie de test.

## Bloc A — finalisation read-only consolidée

### Architecture

- **Terminé** — Ajouter les contrats, modèles, lecteurs et agrégateur sans modifier les baselines 2.1 à 2.3.
- **Terminé** — Ajouter l’actualisation hybride toutes les deux secondes avec annulation propre et sans I/O UI.

### Interface graphique

- **Terminé** — Finaliser les affichages de provenance, disponibilité, diagnostic, logs et joueurs inférés.
- **Terminé** — Revue humaine globale des sept captures hybrides du ZIP final, couverte par les validations Bloc A puis la revue globale V1.
- **Terminé** — Corriger les sept blocages de la première revue globale : confidentialité, formes JSON, cache métadonnées, rotation des logs, moniteur hors UI, arrêt attendu et captures cohérentes.
- **Terminé** — Transmettre les résultats et quatre captures remplacées pour le verdict final du Bloc A.

### Données locales

- **Terminé** — Limiter les logs à la session active et à la liste blanche approuvée.
- **Terminé** — Intégrer `installation_verification.json`, `service_status.json`, rôles et langues lorsqu’ils existent.
- **Terminé** — Maintenir points, vie, inventaire, serveur BOIII et Ranked à « inconnu » sans preuve explicite.

### Tests et documentation

- **Terminé** — Ajouter les régressions de confidentialité, fichiers partiels, changement de session, cache, confinement et read-only.
- **Terminé** — Compiler et tester Debug/Release : 0 avertissement, 0 erreur, 113/113 tests dans chaque configuration.
- **Terminé** — Produire sept captures hybrides finales et le ZIP global unique du Bloc A.
- **Terminé** — Relancer la validation après corrections : 0 avertissement, 0 erreur et 124/124 tests en Debug et Release.

### Blocs suivants

- **Terminé** — Mode opérateur Local/LAN : choix, test, persistance non sensible et activation au prochain démarrage sans ligne de commande.
- **Terminé** — Adapter le mode LAN au partage read-only officiel `PinteModData`, directement à la racine `boiii/scriptdata/pintemod`, sans partager les binaires ou secrets du serveur.
- **Terminé** — Live Console : actualisation, filtres, recherche, auto-scroll, pause/reprise et audit RCON neutralisé en mémoire.
- **Terminé** — Bloc B diagnostic : Health et statut Pause réels validés sur un serveur déjà lancé, sans lancement automatique.
- **Terminé** — Première commande réelle Community Pause/Reprendre : implémentée, sécurisée et validée avec l’opérateur seul en jeu.
- **Terminé** — Conserver en simulation ou « À venir » toutes les actions non autorisées ; seules les listes blanches explicitement auditées peuvent utiliser le transport réel.
- **Terminé** — Bloc C : actions serveur et joueur principales implémentées et revue globale V1 conclue sans blocage ; validation terrain globale conservée comme jalon opérationnel final.
- **Terminé** — Produire le paquet autonome Windows x64 `PinteMod-ControlCenter-v2.2-MVP-Preview-win-x64.zip`, sans secret, configuration ni donnée serveur.
- **Terminé** — Valider le packaging MVP Preview : 149/149 tests en Debug et Release, archive sûre et empreinte SHA-256 fournie.
- **Terminé** — Vérifier depuis le PC fixe que le partage read-only `PinteModData` du portable est accessible et contient la session, les quatre heartbeats, `ranks_v2` et `remote`.
- **Terminé** — Corriger le refus incorrect de la racine UNC du partage `PinteModData` et ajouter la régression dédiée.
- **Terminé** — Valider humainement Preview 5 en mode `LAN` avec le partage read-only `PinteModData` du portable.
- **Terminé** — Adapter le lecteur Community Pause au format booléen numérique réel `Active: 0/1`, sans assouplir les autres champs du contrat.
- **Terminé** — Valider la mise en pause réelle avec un joueur seul et confirmer localement `Active: 1` ainsi que les protections temporaires.
- **Terminé** — Ajouter une actualisation manuelle `ezzpausestatus` directement au panneau Serveur pour renouveler explicitement le statut avant Pause ou Reprendre.
- **Terminé** — Dans Preview 7, actualiser le statut puis valider Reprendre avec l’opérateur seul en jeu.
- **Terminé** — Valider humainement l’extraction et le lancement du paquet portable sur le PC de développement.
- **Terminé** — Confirmer dans l’archive stable que les commandes joueur prévues acceptent BOIII_XUID.
- **Terminé** — Boutons Community Soft Pause réels : Pause et Reprendre validés derrière source live fraîche, secret DPAPI, liste blanche et confirmation.
- **Terminé** — Publier `PinteMod-ControlCenter-v2.2-MVP-Preview-2-win-x64.zip` avec l’observation Community Pause read-only.
- **Terminé** — Republier Preview 2 après validation sémantique RCON ; archive contrôlée sans symboles, secret, configuration ni donnée serveur.
- **Terminé** — Publier Preview 3 avec Pause/Reprendre confirmés, sans remplacer la Preview 2 encore ouverte par l’opérateur.
- **Terminé** — Publier Preview 4 avec prise en charge directe du partage LAN read-only `PinteModData` ; 177/177 tests en Debug et Release, archive contrôlée et empreinte SHA-256 fournie.
- **Terminé** — Publier Preview 5 avec validation correcte d’une racine UNC de partage ; 178/178 tests en Debug et Release, archive contrôlée et empreinte SHA-256 fournie.
- **Terminé** — Publier Preview 6 avec le format Community Pause réel `0/1` ; 180/180 tests en Debug et Release, archive contrôlée et empreinte SHA-256 fournie.
- **Terminé** — Publier Preview 7 avec actualisation manuelle du statut dans le panneau Serveur ; 181/181 tests en Debug et Release, archive contrôlée et empreinte SHA-256 fournie.
- **Terminé** — Préparer le ZIP global de revue ChatGPT de la fondation Bloc B, anonymisé, avec sources, tests, preuves terrain, prompt et manifeste SHA-256.
- **Terminé** — Obtenir le premier verdict ChatGPT sur la fondation Bloc B : cinq blocages identifiés puis traités dans la passe globale de corrections.
- **Terminé** — Auditer le catalogue des commandes du commit GitHub stable et documenter syntaxe, XUID, risque et ordre d’activation.
- **Terminé** — Afficher les contrôles Pause/Reprendre verrouillés et ajouter le filtre Live Console `PAUSE`.

## Fondation Bloc B — corrections de revue globale

- **Terminé** — Refuser les IP publiques, noms d’hôte et adresses non spécifiées à toutes les frontières RCON ; conserver uniquement boucle locale et réseaux privés/link-local.
- **Terminé** — Relire et revalider le statut Community Pause après confirmation humaine et immédiatement avant la mutation.
- **Terminé** — Sérialiser globalement les diagnostics et mutations, au niveau transport comme au niveau des parcours UI.
- **Terminé** — Verrouiller Pause/Reprendre après tout envoi incertain ou non confirmé jusqu’à un statut local strictement plus récent et frais.
- **Terminé** — Attendre les opérations RCON/ViewModel acceptées avant de disposer les lecteurs pendant une fermeture normale.
- **Terminé** — Ajouter les régressions de confinement, concurrence, autorisation expirée, ancien snapshot, arrêt et configuration persistée.
- **Terminé** — Revalider Debug et Release : 0 avertissement, 0 erreur, 215/215 tests réussis dans chaque configuration.
- **Terminé** — Produire le ZIP de contre-revue Bloc B corrigé, avec sources, preuves, verdict précédent et manifeste SHA-256 vérifié.
- **Terminé** — Obtenir la première contre-revue ChatGPT : quatre blocages clôturés et un faux négatif UDP `CommandSent = false` identifié.
- **Terminé** — Traiter conservativement toute erreur pendant le premier appel UDP de mutation comme une livraison potentielle et ajouter le filet de sécurité ViewModel.
- **Terminé** — Ajouter les régressions `SocketException` sur la première mutation et exception non normalisée, puis revalider 218/218 tests.
- **Terminé** — Produire le paquet final de clôture Bloc B avec sources complètes, preuves et manifeste SHA-256 vérifié.
- **Terminé** — Verdict ChatGPT final obtenu : `FONDATION BLOC B VALIDÉE — aucune correction bloquante`.
- **Terminé** — Bloc C : seules les mutations dont le contrat, les bornes et le mode de vérification sont établis ont été activées.

## Bloc C — administration complète

### Diagnostics serveur read-only

- **Terminé** — Étendre la liste blanche typée aux diagnostics manuels `ezzmap`, `ezzpowerstatus`, `ezzpapstatus`, `ezzround` et `ezzplayers`.
- **Terminé** — Valider les signatures de réponse propres à chaque diagnostic et neutraliser les retours avant exposition aux ViewModels.
- **Terminé** — Exposer les cinq diagnostics dans Paramètres et Serveur avec état, réponse lisible et indicateur `Commande envoyée`.
- **Terminé** — Sérialiser ces diagnostics avec Pause/Reprendre via le coordinateur RCON partagé ; aucun envoi automatique ni retry.
- **Terminé** — Revalider Debug et Release : 0 avertissement, 0 erreur, 223/223 tests réussis dans chaque configuration.
- **Terminé** — Publier la Preview 8 autonome, sans PDB, secret, configuration, log, donnée serveur, GSC ou BAT.
- **Terminé** — Validation terrain des diagnostics Carte, Courant, Pack-a-Punch, Manche et Joueurs : les cinq sorties attendues ont été observées dans la console BOIII, sans modification de la partie.

### Actions joueur

- **Terminé** — Utiliser la présence JOIN/ACTIVE/LEAVE de `connections.log` dans la session active comme source locale des joueurs et BOIII_XUID ; ne jamais utiliser `ezzplayers`, le pseudo ou le slot comme clé d’action.
- **Terminé** — Relire le snapshot après confirmation et refuser l’envoi si la source locale ou le même BOIII_XUID n’est plus présent.
- **Terminé** — Implémenter Revive, Respawn, Points, Munitions, Godmode, Téléportation au viseur, armes, atouts et tous les atouts avec listes fermées.
- **Terminé** — Implémenter Mute, Unmute, Kick, Ban à durée fermée, rôle limité à Helper/Modérateur/Admin et retrait du rôle ; aucune saisie libre et rôle Owner exclu.
- **Terminé** — Partager le verrou de résultat incertain entre Dashboard, Joueurs, Serveur et Pause/Reprendre.
- **Terminé** — Ajouter l’historique de modération joueur local, strictement read-only, chargé par XUID interne et affiché sans XUID complet ni chemin réel.
- **Terminé** — Ajouter la création de power-up ciblée par BOIII_XUID avec neuf alias fermés, confirmation et verrou transversal.

### Actions serveur suivantes

- **Terminé** — Implémenter `ezznextround`, `ezzsetround 2..255`, `ezzpower` et `ezzpap` avec confirmation, zéro retry et verrou transversal après toute émission potentielle.
- **Terminé** — Ajouter l’acquittement explicite « J’AI VÉRIFIÉ LA CONSOLE » ; aucun succès automatique n’est affiché faute de feedback local autoritaire.
- **Terminé** — Vérifier la sérialisation avec diagnostics et Pause/Reprendre, le confinement réseau, les réponses neutralisées et `CommandSent` conservateur.
- **Terminé** — Compiler et tester Debug/Release : 0 avertissement, 0 erreur, 240/240 tests réussis dans chaque configuration.
- **Terminé** — Ajouter musique de carte, passages standard, garder/éliminer les zombies et délai permanent/normal des power-ups PinteMod avec confirmation et verrou manuel.
- **Terminé** — Revalider le lot étendu : Debug et Release, 0 avertissement, 0 erreur, 252/252 tests réussis dans chaque configuration.
- **Terminé** — Publier la Preview 10 autonome avec les actions serveur et joueur typées, sans PDB, secret, configuration, log, donnée serveur, GSC ou BAT.
- **Terminé** — Aligner le sélecteur sur les 14 cartes officielles déclarées par le catalogue `server_zm.cfg` de la copie de test, sans lire automatiquement ce fichier de configuration.
- **Terminé** — Remplacer le menu statique par un catalogue hybride : base officielle, rotation collée explicitement, cartes custom locales et carte courante observée.
- **Terminé** — Refuser toute lecture automatique de `server_zm.cfg`, toute ligne autre que `set sv_maprotation`, tout code de carte non sûr et toute écriture côté serveur.
- **Terminé** — Ajouter les diagnostics manuels read-only `ezzmapaudit full`, `ezzeventstatus` et `ezzpowerups` avec validation de réponse.
- **Terminé** — Publier et contrôler la Preview 11 : 253/253 tests Debug/Release et archive autonome sûre.
- **À valider** — Effectuer une seule validation terrain finale regroupant les actions serveur et joueur nécessaires, au lieu d’imposer une revue après chaque bouton.
- **À valider** — Vérifier visuellement le catalogue hybride, l’historique local et le nouveau bouton Power-up dans la Preview 13 ; le test serveur du power-up peut attendre la validation terrain finale.
- **Terminé** — Ajouter en un seul lot la copie locale des réponses diagnostics neutralisées et du filtre visible de la Live Console, avec gestion non bloquante d’un presse-papiers indisponible.
- **Terminé** — Preview 13 utilisée comme paquet de la revue globale finale ; la Preview 12 reste remplacée.
- **Bloqué** — Changement/redémarrage de carte : aucun contrat GSC générique sûr et stable n’a été identifié ; conserver les boutons simulés.
- **Bloqué** — Mutations événements et boss : les commandes auditées sont dépendantes des cartes et ne fournissent pas de contrat générique assez sûr ; conserver la simulation.

## Clôture V1 et publication

- **Terminé** — Revue indépendante globale de la source et du paquet Preview 13 : aucun blocage obligatoire, aucune correction de sécurité, confidentialité, RCON, confinement, sérialisation ou packaging requise.
- **Terminé** — La revue autorise explicitement la clôture du code V1 en conservant simulés changement/redémarrage de carte, boss et événements génériques.
- **Terminé** — Candidate `v2.2.0-rc.1` créée et contrôlée à partir des octets exacts de la Preview 13 auditée, puis préparée pour publication GitHub avec archive et SHA-256.
- **À valider** — Effectuer la validation terrain groupée restante avant de promouvoir la candidate vers le tag stable `v2.2.0`.

## Candidate V1 RC2 — corrections finales de la seconde revue

- **Terminé** — Retirer la RC1 de la publication active après le verdict plus strict et la remplacer par une RC2 recompilée.
- **Terminé** — Remplacer tous les XUID simulés/exemples par des identifiants réservés fictifs et ajouter le contrôle de régression des sources/contrats.
- **Terminé** — Désactiver les symboles Release, appliquer une cartographie déterministe des chemins et scanner les assemblies applicatives du paquet.
- **Terminé** — Fermer les messages publics des lecteurs : aucune exception système brute ne peut atteindre les métadonnées ou ViewModels.
- **Terminé** — Vérifier par handle la cible réellement ouverte avant toute lecture PinteMod, conserver le support UNC explicite et dégrader les refus sans crash.
- **Terminé** — Rendre `RconDiagnosticService` conservateur après le début du transport, sans retry ni élargissement de liste blanche.
- **Terminé** — Compiler et tester Debug/Release : 0 avertissement, 0 erreur, 292/292 tests dans chaque configuration.
- **Terminé** — Publier et auditer `PinteMod-ControlCenter-v2.2.0-rc.2-win-x64.zip` : 466 entrées, aucun fichier interdit, ancien XUID interdit ou chemin privé de compilation.
- **Terminé** — Soumettre uniquement la RC2 et son SHA-256 à la revue indépendante finale de clôture ; SHA et révision embarquée confirmés.
- **À valider** — Après verdict RC2 sans blocage, conserver la validation terrain groupée comme dernier jalon avant `v2.2.0` stable.

## Développement post-RC2 — runtime PinteMod existant

- **Terminé** — Créer la branche locale `codex/post-rc2-runtime-contracts` depuis le commit RC2 exact `90d4922…` sans toucher au tag ni aux assets validés.
- **Terminé** — Auditer en lecture seule PinteModReal `0b293b5…` et confirmer le bridge runtime v0.1.2.
- **Terminé** — Whitelister et lire strictement `health/pintemod.json` et `runtime/control_center_snapshot.json`.
- **Terminé** — Utiliser le LastWriteTimeUtc vérifié comme autorité de fraîcheur et accepter `updated_at_utc` vide.
- **Terminé** — Remplacer l’état synthétique PinteMod et les valeurs runtime inférées seulement avec une source fraîche de la session active.
- **Terminé** — Afficher vie, arme équipée, munitions, inventaire, atouts et Godmode sans exposer de XUID complet.
- **À faire** — Intégrer ultérieurement `control_center_capabilities.json`, seulement lorsqu’un contrat PinteMod stable existe.
- **Bloqué** — ChangeMap/RestartMap restent simulés faute de contrat fermé et de feedback local.
- **Bloqué** — TriggerEvent/SpawnBoss restent simulés faute de capacités génériques par carte.
- **À faire** — Intégrer ultérieurement le feedback unifié des mutations lorsqu’il sera produit par PinteMod.

## Correctifs terrain et audit UX post-RC2 — 2026-08-12

- **Terminé** — Centraliser les 19 armes standard/universelles et les catalogues spéciaux PinteMod Weapons v0.5.2 par carte.
- **Terminé** — Afficher les armes spéciales uniquement avec un runtime local frais, de session et carte cohérentes.
- **Terminé** — Ajouter le Pack-a-Punch de l’arme tenue par BOIII_XUID, sans option libre et avec le verrou opérateur existant.
- **Terminé** — Ajouter le retrait ciblé d’un atout depuis la même liste fermée que l’attribution.
- **Terminé** — Afficher un fallback local autoritaire pour Carte, Courant, PAP, Manche et Joueurs lorsque BOIII ne transporte pas le texte RCON.
- **Terminé** — Ajouter Santé PinteMod au panneau Serveur et limiter son fallback à un résumé local distinct de `ezzhealth full`.
- **Terminé** — Documenter dans `UX_FEATURE_AUDIT.md` les fonctions gardées, ajoutées, refusées et toujours simulées.
- **À valider** — Terrain : plusieurs armes standard, une arme spéciale de la carte, PAP arme normale/déjà PAP/non compatible et cinq fallbacks diagnostics.
- **À faire** — Futur contrat PinteMod capabilities/action feedback pour carte, événements, boss et diagnostics non structurés.

## Responsivité des actions joueur — 2026-08-12

- **Terminé** — Séparer Armes, Atouts et Power-ups en grilles responsives autonomes sans largeur fixe sur les sélecteurs.
- **Terminé** — Ajouter une régression XAML garantissant que de nouveaux boutons restent confinés à leur groupe responsive.
- **Terminé** — Validation humaine du panneau responsive dans la preview `b57db391`.
- **Terminé** — Produire le paquet global de revue ChatGPT du lot post-RC2, avec sources, patches, tests, binaire audité et manifestes SHA-256.
- **Terminé** — Revue globale post-RC2 effectuée : deux blocages ciblés identifiés dans le lecteur JSON partagé.
- **Terminé** — Plafonner la lecture à la limite contractuelle + 1 et refuser un fichier qui grossit avant parsing.
- **Terminé** — Utiliser la longueur et le LastWriteTimeUtc du même handle vérifié avant/après lecture, sans retour au chemin.
- **Terminé** — Revalider 381/381 tests en Debug et Release et produire le ZIP de contre-revue audité `0e4e0928`.
- **Terminé** — Contre-revue ChatGPT validée sans blocage le 2026-08-13 ; lot post-RC2 autorisé pour le terrain.
- **Terminé** — Validation terrain groupée réussie le 2026-08-13 : armes, PAP, retrait d’atout, power-up et fallbacks diagnostics acceptés.
- **À faire** — Sur ordre explicite uniquement, préparer la publication stable issue de la candidate `0e4e092` sans modifier la RC2 historique.

## Prépublication stable — profils serveurs et identité

- **Terminé** — Empêcher les listes Armes et Cartes inchangées d’être reconstruites à chaque actualisation automatique.
- **Terminé** — Ajouter plusieurs onglets serveurs avec configuration, lecture locale, secret DPAPI, catalogue et sécurité RCON isolés par profil.
- **Terminé** — Migrer la configuration unique existante vers le profil principal sans perdre le chemin, l’adresse RCON ou le secret déjà protégé.
- **Terminé** — Ajouter, retirer et renommer localement un onglet serveur sans modifier ni arrêter BOIII.
- **Terminé** — Exécuter la suite complète Debug/Release : 0 avertissement, 0 erreur et 394/394 tests dans chaque configuration.
- **Terminé** — Produire et auditer la preview autonome multi-serveurs : 466 entrées, audit packaging PASS.
- **À valider** — Vérifier visuellement la barre d’onglets, l’ajout/retrait, le renommage et la stabilité des listes Armes, Cartes et Catalogue pendant plusieurs actualisations.
- **Bloqué** — Modification du nom public BOIII : aucun contrat PinteMod fermé, validé et observable n’existe dans PinteModReal.
- **Terminé** — Confirmation humaine reçue : « mot de passe serveur » désigne bien `g_password`, demandé aux joueurs pour rejoindre, et non le secret RCON.
- **Bloqué** — Modification de `g_password` : attendre un contrat PinteMod typé, borné, sans journalisation de la valeur et avec une décision explicite sur la confidentialité du transport ; ne jamais utiliser une commande libre supposée.

## Intégration contrats PinteModReal e279a59 — 2026-08-14

### En cours

- Aucun travail automatisé restant dans ce lot.

### À valider

- [Interface graphique] Vérifier le rendu responsive de la nouvelle carte Identité et du sélecteur Boss.
- [Intégration PinteMod] Valider en une seule passe terrain Restart Map, Spawn Boss, Set Hostname et Clear Join Password.

### Terminé

- [Lecture locale] Quatre sources contractuelles bornées, confinées, asynchrones et mises en cache sans autorité fraîche artificielle.
- [Sécurité] `supported` n’est jamais transformé en `installed`; Change Map reste inactif.
- [RCON] Quatre nouvelles commandes fermées seulement, sans texte de commande libre ni retry automatique.
- [Confidentialité] `g_password` n’est jamais lu/affiché et le boss reste ciblé en interne par BOIII_XUID.
- [Tests] Debug et Release : 0 avertissement, 0 erreur et 413/413 tests réussis dans chaque configuration.
- [Tests] Scans de whitelist, confidentialité, XAML, schémas et garantie read-only terminés.
- [Documentation] Rapport et prompt de revue post-RC2 préparés.
- [Correctif revue] Observer les quatre actions contractuelles après `DeliveryUnknown`/`TransportError` lorsque `CommandSent = true`, sans retry.
- [Correctif revue] Conserver `ENVOYÉ · NON CONFIRMÉ` et le verrou humain lorsqu’aucune preuve locale corrélée n’arrive.
- [Tests] Contre-revue transport incertain : 418/418 tests Debug et Release, 0 avertissement, 0 erreur.
- [Revue] Contre-revue ciblée validée sans blocage ; validation terrain groupée autorisée.
- [Packaging] Candidate terrain Windows x64 autonome auditée : 471 entrées, SHA-256 `C7B8933B27A8D9EBE0DFCDAA1F53C4D155BAAF4731F44B753E6B7C477CE6F92A`.
- [Terrain] Copie Server3 de test sauvegardée et préparée en LAN-only sur le port 27121 avec les deux GSC candidats ; aucun serveur lancé.
- [Terrain] Compilation/chargement GSC confirmé humainement sans erreur signalée sur la copie Server3 isolée.
- [Terrain] Lecture hybride locale validée visuellement : session/runtime/PinteMod frais ; services auxiliaires non lancés correctement présentés comme arrêtés ou périmés.
- [Terrain] Transport RCON local validé sur la copie isolée à `127.0.0.1:27121` via le diagnostic `ezzhealth full` et son fallback local frais `PinteMod : SAIN`.
- [Terminé] Corriger l'écart terrain entre les scalaires JSON natifs produits par BOIII et les quatre schémas/lecteurs contractuels ; 419/419 tests Debug et Release.
- [À valider] Relancer la candidate terrain `3bf033c`, confirmer l'identité locale puis effectuer Restart Map, Spawn Boss, Set Hostname et Clear Join Password en une passe groupée.
- [À faire] Reporter les mêmes types JSON natifs dans les quatre schémas source de PinteModReal avant sa prochaine publication, sans modifier le GSC runtime déjà conforme.

## Contrat identité v0.1.2 et ergonomie fenêtre — 2026-08-14

- **Terminé** — Isoler les onglets serveurs sous une barre de déplacement dédiée et testée.
- **Terminé** — Persister uniquement le hostname public côté PinteMod, sans écrire le CFG serveur ni aucun secret.
- **Terminé** — Ajouter la définition de `g_password` par commande fermée, validation stricte et endpoint loopback uniquement.
- **Terminé** — Garder le mot de passe hors binding, snapshot, feedback, activité, configuration et journal applicatif.
- **Terminé** — Valider 39/39 tests PinteModReal et 438/438 tests Control Center en Debug et Release.
- **À valider** — Chargement terrain du GSC v0.1.2 sur la copie Server3 et rendu de la nouvelle barre de déplacement.
- **À valider** — Persistance du hostname après redémarrage complet du processus de test ; le titre de fenêtre BOIII n’est pas une autorité.
- **À valider** — Test de confidentialité avec un mot de passe synthétique unique sur loopback et recherche exhaustive de toute fuite.
- **Bloqué** — Publication de SET mot de passe tant que le test de fuite terrain n’est pas réussi.
- **Terminé** — Produire la candidate autonome `3d624fa`, audit packaging PASS (471 entrées), SHA-256 `C8D9B56E0307FAB614F75DD4D07D11FDA4114FBE078D8DB3D036AF494D42FB8A`.
- **Terminé** — Diagnostiquer par les preuves locales que SET mot de passe était appliqué et que le nom public BOIII dépend de `live_steam_server_name`.
- **Terminé** — Corriger le contrat v0.1.3, accepter uniquement `^0`–`^9` et valider 445/445 tests Debug/Release.
- **À valider** — Redémarrer manuellement la copie de test, confirmer `Control Center Contracts v0.1.3 loaded`, puis retester le nom coloré.
- **À valider** — Tester le mot de passe avec un client totalement déconnecté puis reconnecté ; un joueur déjà en session ne constitue pas une preuve.
- **Terminé** — Produire et auditer la candidate v0.1.3 `7bdb22f`, 471 entrées, SHA-256 `E7513B783C900B451F58E8E01F4B34EF0355DADB0BC2B2FB2C80AF881A073F46`.

## Correctif `net_password` et densité visuelle — 2026-08-14

- **Terminé** — Confirmer sur un client vierge que `g_password` n’empêche pas la connexion directe Ezz BOIII.
- **Terminé** — Identifier dans le code public BOIII l’autorité réelle `net_password` et corriger le contrat PinteMod v0.1.4 sans commande libre.
- **Terminé** — Conserver la valeur hors fichiers, snapshots, feedbacks, ViewModels et journaux ; loopback uniquement.
- **Terminé** — Replier les détails de lecture des services et les XUID abrégés des profils Ranks, avec révélation explicite et clavier/souris.
- **Terminé** — Afficher directement le numéro de meilleure manche sans préfixe `M`.
- **Terminé** — Exécuter les suites complètes Debug et Release : 0 avertissement, 0 erreur et 447/447 tests dans chaque configuration.
- **Terminé** — Préparer et auditer la candidate autonome `e9be7ca` liée au correctif PinteMod v0.1.4.
- **À valider** — Redémarrer manuellement la copie Server3, confirmer `Control Center Contracts v0.1.4 loaded`, puis tester refus sans `net_password` et acceptation avec la bonne valeur.
- **À valider** — Vérifier visuellement les deux flèches de détails sur Dashboard et Records en fenêtre grande et réduite.
- **Terminé** — Rendre `DÉTAILS` et `IDENTIFIANT` discrets (8 px, opacité réduite, petite flèche) et mieux distinguer les champs actifs Nom/Mot de passe.
- **Bloqué** — Publication stable tant que le test terrain `net_password` et le contrôle de confidentialité avec valeur synthétique ne sont pas terminés.
