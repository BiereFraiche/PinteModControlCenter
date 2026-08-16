# Décisions — PinteMod Control Center

Dernière mise à jour : 2026-08-12

## ADR-001 — Architecture en quatre projets

**Décision.** Organiser la solution sous `app/` avec `PinteMod.ControlCenter.Core`, `PinteMod.ControlCenter.Infrastructure`, `PinteMod.ControlCenter` et `PinteMod.ControlCenter.Tests`.

**Raison.** Le domaine et les contrats restent indépendants de WPF. Les fournisseurs simulés pourront être remplacés par des lecteurs locaux sans modifier les vues.

**Alternative rejetée.** Une application WPF monolithique créerait un couplage direct entre affichage, données et future intégration.

## ADR-002 — Injection par constructeurs au composition root

**Décision.** Utiliser des interfaces et l'injection par constructeurs, assemblées dans `App.xaml.cs`, sans conteneur externe pendant la phase 1.

**Raison.** Cette stratégie satisfait l'inversion de dépendances, reste testable et évite une dépendance NuGet non indispensable.

**Alternative rejetée.** Un service locator global masquerait les dépendances des ViewModels.

## ADR-003 — Simulation structurelle sans transport

**Décision.** Toutes les actions passent par `ISimulationActionService`, avec une énumération fermée et un résultat portant explicitement `CommandSent = false`.

**Raison.** Aucun texte UI ne devient une commande. Il n'existe ni socket, ni client RCON, ni écriture PinteMod dans la solution.

**Alternative rejetée.** Simuler autour d'une future interface RCON augmenterait le risque de branchement accidentel pendant la phase prototype.

## ADR-004 — Ciblage joueur par XUID uniquement

**Décision.** Une action joueur exige un XUID hexadécimal validé sur 16 caractères. Le pseudo reste une propriété d'affichage.

**Raison.** Cette convention suit l'autorité BOIII_XUID de PinteMod et évite les ambiguïtés ou réattributions de pseudonyme.

## ADR-005 — Données simulées conformes aux contrats provisoires

**Décision.** Représenter carte, session, services, joueurs, événements et records avec des modèles Core proches des exemples de `contracts/`, sans les lire au runtime.

**Raison.** La phase 1 reste autonome et déterministe tout en préparant les futurs adaptateurs de snapshots versionnés.

## ADR-006 — Direction WPF sombre et responsive

**Décision.** Utiliser des ressources WPF natives, un rail latéral, des cartes KPI, des panneaux scrollables et une palette sombre bleu électrique. Vert, orange et rouge conservent leurs rôles sémantiques.

**Raison.** Ce choix reprend la référence validée sans dépendance graphique externe et reste utilisable au clavier et à la souris.

## ADR-007 — Dépendances de tests

**Décision.** Utiliser MSTest (`Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`) uniquement dans le projet de tests.

**Raison.** L'application et ses trois projets de production ne dépendent d'aucun paquet tiers. La suite reste exécutable avec `dotnet test` dès que le SDK .NET 8 est disponible.

## ADR-008 — Archive de référence non renommée

**Décision.** Auditer `reference/PinteMod_v2.1.1.zip` telle qu'elle existe et consigner l'écart avec le nom `_FINAL`, sans renommer ni modifier `reference/`.

**Raison.** Les règles interdisent de modifier les sources validées et exigent une provenance explicite.

## ADR-009 — Navigation MVVM par DataTemplates

**Décision.** Le shell expose un `PageViewModel` courant et WPF choisit la vue via des `DataTemplate` typés dans `App.xaml`.

**Raison.** La navigation reste testable sans code-behind métier ; les code-behind de vues contiennent seulement `InitializeComponent`.

## ADR-010 — Contrôles serveur prédéfinis

**Décision.** Les cartes, manches et options serveur sont choisies dans des listes prédéfinies. Aucun champ ne permet de saisir une commande brute.

**Raison.** Le prototype prépare la liste blanche future et évite qu'un texte d'interface soit confondu avec une commande validée.

## ADR-011 — Responsivité par mise en page scrollable

**Décision.** Utiliser des grilles à largeur minimale, des `UniformGrid`, des `WrapPanel` et des `ScrollViewer` horizontaux/verticaux.

**Raison.** La vue reste dense et stable à 1920×1080 tout en demeurant accessible dans une fenêtre réduite, sans bibliothèque de layout externe.

## ADR-012 — Chrome et contrôles entièrement thémés

**Décision.** Utiliser une barre de titre WPF sombre et des templates natifs personnalisés pour boutons, navigation, listes et ComboBox.

**Raison.** Les contrôles Windows clairs par défaut rompaient la direction sombre validée. Les templates restent locaux, sans bibliothèque graphique supplémentaire, et fonctionnent à la souris comme au clavier.

## ADR-013 — Snapshot partagé et rafraîchissement cohérent

**Décision.** Introduire `IControlCenterSnapshotStore` dans Core et un cache synchronisé dans Infrastructure. L’initialisation charge une fois le snapshot ; une actualisation remplace ce snapshot puis réinitialise toutes les pages.

**Raison.** Les futurs fichiers locaux ne doivent pas être lus six fois successivement et toutes les pages doivent présenter le même instant logique.

**Alternative rejetée.** Conserver un appel direct au fournisseur dans chaque ViewModel aurait créé des lectures redondantes et des états potentiellement divergents.

## ADR-014 — Sélection joueur partagée par XUID

**Décision.** Injecter un `PlayerSelectionState` léger dans Dashboard et Joueurs. Il ne contient que le XUID sélectionné ; chaque page résout son propre `PlayerItemViewModel` d’affichage.

**Raison.** La sélection traverse la navigation sans coupler les vues et sans transformer le pseudo en identifiant. Un XUID absent est remplacé par le premier joueur disponible, ou invalidé si la liste est vide.

## ADR-015 — Responsivité par grille adaptative

**Décision.** Cette décision remplace la partie « largeur minimale » d’ADR-011. Les pages ne fixent plus `MinWidth=740`. `ResponsiveUniformGrid` calcule le nombre de colonnes depuis la largeur disponible ; la sidebar passe à 82 px à 1100 px ou moins. Le défilement global reste vertical uniquement.

**Raison.** La combinaison précédente ne pouvait pas tenir à 900×640. Le contrôle WPF natif spécialisé conserve une interface dense à 1920×1080 et empile les panneaux à petite largeur.

## ADR-016 — Préférences non implémentées explicitement désactivées

**Décision.** Les options d’actualisation automatique, d’alertes sonores et de mode compact manuel restent fausses, désactivées et marquées « À venir ».

**Raison.** Aucun contrôle ne doit suggérer un comportement absent. La compacité automatique selon la largeur reste un comportement de layout distinct, sans préférence persistée.

## ADR-017 — Scénarios simulés sans sélecteur produit

**Décision.** Le fournisseur accepte des scénarios déterministes et l’application peut recevoir `--scenario=warning|offline|stopped|empty` pour les tests visuels. Aucun sélecteur n’est exposé dans l’interface produit.

**Raison.** Les états dégradés deviennent testables et capturables sans lecteur réel, réseau ou mutation de serveur.

## ADR-018 — Bindings d’affichage en lecture seule explicitement OneWay

**Décision.** Tous les bindings WPF placés sur des propriétés de ViewModel en lecture seule et associés à des propriétés de contrôle `TwoWay` par défaut sont déclarés explicitement `Mode=OneWay`. Cela couvre les `Run.Text` du résultat structuré et les `CheckBox.IsChecked` des préférences indisponibles.

**Raison.** La validation de l’exécutable a révélé que la valeur par défaut du contrôle peut tenter une écriture vers le ViewModel même lorsque l’intention est purement visuelle. Le mode explicite supprime l’erreur runtime tout en conservant des modèles immuables et testables.

## ADR-019 — Paquet de revue humaine autonome et sans sorties générées

**Décision.** Fournir à ChatGPT une archive dédiée contenant les captures, la référence graphique, le contexte, les contrats et les sources, mais excluant `bin/`, `obj/`, les données runtime, les secrets, `server-sandbox/` et l'archive serveur.

**Raison.** La revue visuelle doit être reproductible depuis un seul fichier, sans exposer d'élément inutile et sans confondre les sorties de compilation avec les sources à examiner.

**Alternative rejetée.** Transmettre seulement les captures priverait la revue du contexte nécessaire pour distinguer une limitation graphique d'une contrainte métier ou de sécurité déjà décidée.

## ADR-020 — Sémantique visuelle du statut Ranked

**Décision.** Mapper `RankedStatus.Ranked` vers `SuccessBrush` et `RankedStatus.Unranked` vers `WarningBrush` dans le convertisseur sémantique partagé. `DangerBrush` reste réservé aux erreurs, états hors ligne, événements dangereux et simulations rejetées.

**Raison.** Une session Unranked constitue un avertissement métier, pas une erreur réelle. Le mapping partagé garantit la même règle dans Dashboard, Records et tout futur affichage lié au snapshot.

**Alternative rejetée.** Fixer directement une couleur dans les XAML de Dashboard et Records dupliquerait la règle, contournerait le `RankedStatus` du ViewModel et risquerait de réintroduire un état visuel codé en dur.

## ADR-021 — Déplacement natif par WindowChrome

**Décision.** Définir une zone de titre `WindowChrome` native de 70 px sur toute la largeur de la fenêtre et déclarer les boutons Actualiser, Réduire, Agrandir et Fermer avec `IsHitTestVisibleInChrome=True`.

**Raison.** Windows gère ainsi nativement le glissement, la restauration depuis un état agrandi, le double-clic et l'accrochage de fenêtre. La zone de déplacement correspond visuellement à tout le bandeau supérieur, tandis que les boutons restent cliquables.

**Alternative rejetée.** Un appel manuel à `DragMove` attaché à une grille interne ne couvrait pas toute la barre et dépendait de la propagation des événements souris à travers ses contrôles enfants.

## ADR-022 — Le paquet final remplace le paquet Review 2

**Décision.** Produire `PinteMod-ControlCenter-ChatGPT-Final-Review.zip` depuis les sources courantes et demander d'utiliser ce fichier à la place de l'ancien paquet `Review-2`.

**Raison.** Le premier paquet de revue ne contient ni la correction sémantique finale de `UNRANKED`, ni la correction `WindowChrome`. Un artefact final unique évite que ChatGPT examine une version devenue obsolète.

**Alternative rejetée.** Envoyer séparément le paquet Review 2 et une petite archive corrective imposerait à la revue de reconstituer manuellement l'état actuel du projet.

## ADR-023 — Mode hybride local explicite et simulation par défaut

**Décision.** Le mode simulé reste actif sans argument. La lecture locale n’est activée que par la combinaison `--data-mode=hybrid-local --server-root=<chemin absolu>` ; le chemin est normalisé et validé, sans recherche automatique ni persistance.

**Raison.** L’application ne doit jamais se connecter implicitement à une installation PinteMod ni confondre une copie de référence avec une source active.

**Alternative rejetée.** Détecter automatiquement un dossier BOIII ou mémoriser le dernier chemin créerait un accès implicite et rendrait la provenance moins claire.

## ADR-024 — Lecture locale confinée et sans restauration

**Décision.** Les lecteurs n’ouvrent que cinq chemins relatifs prédéfinis sous la racine normalisée, avec partage de lecture tolérant. Les fichiers `.tmp` et `.bak` ne sont jamais utilisés comme données actives. Aucun lecteur ne crée, répare, restaure, renomme ou supprime un fichier.

**Raison.** Le périmètre read-only doit être vérifiable mécaniquement et résister aux chemins absolus, aux traversées de répertoire et aux écritures partielles.

## ADR-025 — Dimensions d’état indépendantes

**Décision.** Un service local expose séparément son état déclaré, l’état de la tentative de lecture, la fraîcheur, l’âge et la provenance. La synthèse visuelle ne devient `Hors ligne` que pour un état déclaré `stopped`; une donnée de plus de 45 secondes devient `Expirée` ou `Inconnue`, et une erreur de lecture ne devient rouge qu’après échec durable.

**Raison.** Un fichier ancien ou momentanément illisible ne prouve pas que le processus est arrêté. La séparation empêche de présenter une dernière valeur valide périmée comme saine.

## ADR-026 — Snapshot hybride à substitution minimale

**Décision.** Le fournisseur hybride part du snapshot simulé partagé et ne remplace que la carte, l’identifiant de session, la version déclarée du manifeste et les quatre services autorisés. Manche, durée, RankedStatus, serveur BOIII, joueurs, événements et records restent simulés et sont signalés comme tels. La carte PinteMod reste neutre avec « État inconnu — aucun heartbeat dédié ».

**Raison.** Cette stratégie livre une première intégration utile sans inventer des données absentes ni étendre le périmètre validé.

## ADR-027 — Fraîcheur différente pour manifeste et heartbeats

**Décision.** Les seuils 15/45 secondes s’appliquent exclusivement aux heartbeats. `current_session.json` est considéré comme un manifeste événementiel écrit au début de la session : son âge est affiché, mais une lecture valide ne devient pas expirée après 45 secondes. Si la lecture active échoue, la copie mémoire devient néanmoins retardée.

**Raison.** `started_gettime` n’est pas une date UTC et le manifeste n’est pas réécrit périodiquement. Lui appliquer une politique heartbeat rendrait toute session normale faussement périmée.

## ADR-028 — Actualisation locale manuelle dans la première sous-phase

**Décision.** La lecture locale s’effectue à l’initialisation et via le bouton Actualiser. L’option d’actualisation automatique reste désactivée et marquée « À venir ».

**Raison.** Cette première livraison valide la tolérance et la provenance des lecteurs avant d’ajouter un cycle périodique. Elle conserve aussi la décision graphique déjà validée sur les réglages non implémentés.

## ADR-029 — Validation réelle exclusivement sur une copie de test désignée

**Décision.** La validation humaine de la sous-phase 2.1 utilise uniquement une copie locale dédiée `<COPIE_DE_TEST>\UnrankedServer`, explicitement fournie comme copie parfaite de test. Le serveur fonctionnel n’est ni lu ni modifié, et `server-sandbox/` n’est pas utilisé comme source active.

**Raison.** Cette séparation permet de vérifier les formats et la sémantique d’affichage avec des données PinteMod réelles tout en conservant l’intégrité du serveur stable.

## ADR-030 — Paquet de revue Phase 2.1 autonome et sans runtime

**Décision.** La revue ChatGPT de la Phase 2.1 utilise un nouveau paquet dédié contenant la capture hybride, les preuves, les documents, les contrats publics et les sources actuelles. Il exclut les données de `servtest`, `server-sandbox/`, `reference/`, les secrets, les sorties compilées et les anciens paquets.

**Raison.** La revue doit pouvoir vérifier le code et la sémantique sans recevoir de données runtime, d’archive serveur ou d’artefact sensible. L’ancien paquet visuel ne contient pas l’implémentation de lecture locale et ne doit plus servir à cette validation.

## ADR-031 — Phase 2.1 figée comme baseline validée

**Décision.** Après validation humaine locale puis verdict ChatGPT sans correction bloquante, l’implémentation Phase 2.1 devient une baseline figée. Les prochaines sous-phases doivent s’ajouter par nouveaux lecteurs et fournisseurs superposés, sans réécrire les lecteurs de session et de heartbeats validés.

**Raison.** Le comportement read-only, la provenance, les seuils de fraîcheur et l’activation explicite ont été testés en Debug/Release et observés sur une copie réelle. Les préserver réduit le risque de régression lors de l’intégration des nouvelles sources.

## ADR-032 — Phase 2.2 superposée et liste blanche Ranks/records

**Décision.** Ajouter deux lecteurs indépendants pour `ranks_v2/players/*.json` (schéma 2) et `ranks_v2/maps/*.json` (schéma 4), puis un fournisseur superposé au fournisseur Phase 2.1. Les lecteurs de session et de heartbeats existants ne sont pas réécrits. Seuls les fichiers JSON actifs directement placés dans ces deux dossiers sont lus ; `.tmp`, `.bak`, sauvegardes, anciens `ranks/` et sous-dossiers sont ignorés.

**Décision de confidentialité.** La liste blanche profil contient le XUID validé, le pseudo d’affichage, les sessions, le temps total et la meilleure manche. Le XUID complet reste un identifiant métier interne et est abrégé dans l’interface. Les champs `key` et `identity_kind` ne sont pas exposés. Les records conservent uniquement les champs structurés nécessaires à leur validation et à leur affichage.

**Raison.** Cette composition préserve la baseline Phase 2.1, évite tout parseur de log fragile et limite l’exposition des données joueur à ce qui est nécessaire à la page Records.

**Alternative rejetée.** Modifier `HybridControlCenterDataProvider` ou utiliser automatiquement les sauvegardes aurait couplé la nouvelle sous-phase à une baseline validée et rendu la provenance active ambiguë.

## ADR-033 — XUID historique non conforme jamais complété automatiquement

**Décision.** Un profil Ranks n’est accepté que si son nom de fichier et son champ `xuid` correspondent et contiennent exactement 16 caractères hexadécimaux. Les identifiants historiques de 15 caractères observés dans la copie de test sont ignorés et comptabilisés, sans ajout automatique d’un zéro ni conversion.

**Raison.** Plusieurs complétions seraient techniquement possibles et aucune source auditée ne permet de prouver la valeur manquante. Inventer un identifiant compromettrait la règle de ciblage exclusif par BOIII_XUID et pourrait rattacher des statistiques au mauvais joueur.

**Alternative rejetée.** Compléter à gauche ou accepter 15 caractères uniquement dans le lecteur créerait une identité différente de celle validée par `XuidValidator` pour les futures actions.

## ADR-034 — Données historiques fraîches et repli sans faux local

**Décision.** Une lecture réussie de profils ou records historiques est `Fresh` quelle que soit l’ancienneté du fichier ; l’âge reste affiché séparément. En cas d’échec ultérieur, la dernière valeur valide devient `MemoryCache` / `Stale`. Sans valeur locale valide, les records de manches simulés ne sont pas conservés sous une apparence locale ; seuls les Easter Egg Records explicitement simulés restent visibles.

**Raison.** Les fichiers Ranks sont des agrégats persistants, pas des heartbeats. Les seuils 15/45 secondes seraient trompeurs, tandis qu’un repli silencieux vers des records simulés rendrait la provenance ambiguë.

## ADR-035 — Paquet de revue Phase 2.2 sans données runtime

**Décision.** Le paquet ChatGPT Phase 2.2 contient les sources, documents, preuves et deux captures validées, mais aucun JSON de `servtest`, serveur, `server-sandbox/`, archive PinteMod, secret ou sortie compilée. Les seules données joueur visibles sont les pseudos d’affichage et XUID abrégés déjà présents dans les captures fournies par l’utilisateur.

**Raison.** La revue doit pouvoir contrôler le comportement et le rendu sans recevoir les fichiers runtime complets ni des identifiants BOIII_XUID en clair.

## ADR-036 — Isolation totale par emplacement de record

**Décision.** Les métadonnées de carte restent validées au niveau du document, mais chaque combinaison catégorie/position est analysée par une fonction non levante retournant `Empty`, `Valid` ou `Invalid`. Toute anomalie d’un emplacement est comptabilisée dans `SlotsSkipped` sans invalider les autres emplacements.

**Raison.** Les fichiers persistants peuvent contenir une entrée ancienne ou partielle au milieu de données encore valides. La tolérance doit rester locale à l’unité de donnée défectueuse.

## ADR-037 — XUID complet hors surface bindable

**Décision.** Les ViewModels d’affichage ne conservent et n’exposent que des XUID abrégés. Le XUID complet requis par le ciblage reste dans les modèles Core, un état de sélection non bindable et une table privée du ViewModel d’actions. Aucune info-bulle ne rétablit la valeur complète.

**Raison.** Le ciblage futur exige l’identité complète, mais l’interface et ses propriétés publiques ne doivent pas la divulguer accidentellement.

## ADR-038 — Durées affichées en heures totales

**Décision.** Tous les affichages d’horloge utilisent un formateur partagé fondé sur `TotalHours`, puis minutes et secondes. Le format reste `mm:ss` sous une heure et devient `HH:mm:ss` avec un nombre d’heures non limité à deux chiffres.

**Raison.** Le format personnalisé `hh` représente seulement le composant horaire d’un jour et reboucle après 24 heures, ce qui faussait les profils et records de longue durée.

## ADR-039 — Phase 2.2 figée comme baseline validée

**Décision.** Après validation externe des trois corrections bloquantes, la Phase 2.2 est clôturée sans condition restante. Les lecteurs Ranks v2 et records de manches v4, leur superposition à la Phase 2.1, la confidentialité des XUID et le formatage des durées deviennent une baseline à préserver pour les sous-phases suivantes.

**Raison.** La revue finale confirme 79/79 tests en Debug et Release, 10 XAML valides, l’absence de binding vers un XUID complet, l’intégrité des lecteurs Phase 2.1 et le maintien des garanties read-only et simulation.

**Remarque non bloquante.** La dépendance du test de scan XAML à une sortie située sous l’arborescence applicative est acceptée pour la configuration actuelle. Elle pourra être durcie ultérieurement uniquement si une nouvelle organisation des sorties de test le nécessite.

## ADR-040 — Easter Egg Records limités à l’autorité officielle v2

**Décision.** La Phase 2.3 lit exclusivement `easter_eggs_v2/profiles.json` schéma 3 et les JSON directs de `easter_eggs_v2/maps/` schéma 2. Un fichier carte n’est accepté que si son profil est au statut exact `OFFICIAL`. Les candidats, tests, sauvegardes, logs et l’ancien arbre sont exclus.

**Raison.** Le module validé distingue explicitement les détections candidates des Top 5 officiels. Dans la copie de test, Origins possède un candidat mais aucun fichier officiel ; l’afficher comme record serait une élévation d’autorité incorrecte.

**Architecture.** Un nouveau lecteur et un nouveau fournisseur de superposition enveloppent la baseline Phase 2.2 sans modifier les lecteurs Phase 2.1 ni `RankRecordsOverlayDataProvider`. L’absence de fichier officiel après validation de `profiles.json` produit un catalogue local vide valide et retire le record simulé.

**Alternative rejetée.** Lire `candidates/maps`, les logs ou les fichiers `test/maps` aurait mélangé diagnostic, simulation et autorité officielle, et aurait rendu la provenance trompeuse.

## ADR-041 — Catégorie de quête distincte du nombre de titulaires actifs

**Décision.** Un emplacement Easter Egg 1–4P accepte entre un et le nombre de joueurs de la catégorie en XUID valides et uniques. Il n’exige pas systématiquement autant de titulaires que la catégorie. Chaque emplacement reste validé et isolé indépendamment.

**Raison.** Le module officiel prend explicitement en charge les quêtes fixes 4P pouvant créditer seulement deux titulaires ayant atteint le seuil de présence. Exiger quatre XUID rejetterait des records officiels légitimes.

## ADR-042 — Superposition Phase 2.3 sans faux repli simulé

**Décision.** `EasterEggRecordsOverlayDataProvider` enveloppe la baseline Phase 2.2 et retire toujours les Easter Egg Records simulés en mode hybride. Une lecture officielle valide mais vide affiche zéro record ; une lecture indisponible n’est jamais remplacée silencieusement par la simulation.

**Raison.** Conserver le record simulé lorsque la source officielle est vide ou défaillante rendrait sa provenance ambiguë et pourrait faire croire qu’un candidat ou un exemple est homologué.

## ADR-043 — Un catalogue officiel vide reste une donnée réelle valide

**Décision.** Lorsque `profiles.json` est valide mais qu’aucun JSON officiel n’existe sous `easter_eggs_v2/maps/`, l’interface affiche zéro Easter Egg Record avec provenance locale réussie. Elle ne montre ni le candidat, ni le record simulé historique, ni un faux état d’erreur.

**Raison.** La validation humaine sur la copie de test confirme précisément cette situation : Origins est autorisée par un profil `OFFICIAL`, mais aucun Top 5 officiel n’a encore été écrit. L’absence de record est une information réelle, pas une panne de lecture.

## ADR-044 — Paquet de revue Phase 2.3 sans données runtime

**Décision.** La revue externe utilise un paquet autonome contenant les sources, documents, contrats publics, preuves et deux captures avant/après. Il exclut tous les JSON de la copie de test, logs, secrets, GSC, archives serveur, sorties compilées et identifiants réels complets.

**Raison.** La correction d’autorité peut être vérifiée à partir du code, des tests et de la comparaison visuelle sans diffuser le candidat Easter Egg, les profils runtime ou d’autres données privées du serveur.

## ADR-045 — Le Bloc A superpose ses données aux baselines validées

**Décision.** Un provider Bloc A enveloppe les providers Phase 2.1, 2.2 et 2.3. Les lecteurs historiques restent inchangés et conservent l’autorité exclusive de leurs domaines.

**Raison.** Cette composition minimise le risque de régression et empêche les logs d’écraser la session, les heartbeats ou les records structurés.

## ADR-046 — Les logs actifs sont incrémentaux, bornés et filtrés avant présentation

**Décision.** Seuls neuf noms de logs du dossier de session active sont autorisés. Les archives, racines historiques, chats, commandes, menus et rapports libres ne sont jamais ouverts. Les lignes partielles attendent leur terminaison et les lignes invalides sont isolées.

**Raison.** Les logs sont des sources append-only utiles mais moins contractuelles que les JSON. Une liste blanche et des limites strictes donnent une dégradation sûre sans exposer de texte privé.

## ADR-047 — Une information non observable devient inconnue

**Décision.** En mode hybride, points, vie, inventaire, maximum de joueurs, processus BOIII et Ranked sans événement explicite ne reprennent pas les valeurs simulées. La présence provient uniquement de JOIN/LEAVE et toute valeur issue des logs est marquée comme inférée.

**Raison.** L’absence de snapshot réel ne doit jamais produire une donnée apparemment réelle.

## ADR-048 — Actualisation hybride automatique toutes les deux secondes

**Décision.** Un moniteur mono-exécution actualise le snapshot toutes les deux secondes uniquement en mode hybride. Il est annulé avec l’application ; le mode simulation et le bouton Actualiser restent inchangés.

**Raison.** Les logs append-only et changements de session doivent apparaître sans action manuelle, tout en garantissant qu’aucune lecture disque ne s’exécute sur le thread UI.

## ADR-049 — Politique de chemins Bloc A séparée de la baseline Phase 2.1

**Décision.** Conserver strictement `LocalPinteModOptions` et `ReadOnlyJsonFileReader` dans leur état validé Phase 2.3. Les nouvelles sources fixes et dynamiques du Bloc A utilisent `BlockALocalPathPolicy` et `ReadOnlyBlockAJsonFileReader`.

**Raison.** Les cinq sources Phase 2.1 ont déjà été validées. Une politique séparée permet d’ajouter les diagnostics, métadonnées et logs sans élargir l’énumération historique ni modifier le lecteur existant. La comparaison SHA-256 finale confirme zéro différence sur les quinze lecteurs et providers de baseline 2.1, 2.2 et 2.3 contrôlés.

**Alternative rejetée.** Ajouter les nouvelles familles à `LocalPinteModFile` et réutiliser le lecteur Phase 2.1 aurait modifié une baseline figée et mélangé chemins fixes, répertoires dynamiques et logs texte.

## ADR-050 — Une seule revue globale et aucun runtime dans le paquet Bloc A

**Décision.** Le Bloc A est livré dans une archive unique contenant les sources concernées, les documents, les preuves statiques et les captures finales. Aucun JSON ou log runtime, XUID complet réel, secret, serveur, GSC, binaire compilé ou ancien paquet n’y est inclus.

**Raison.** La revue peut vérifier l’architecture, la sécurité, les tests et l’interface sans diffuser les données de la copie de test. L’exécutable Release reste disponible séparément dans `app/artifacts/block-a-build/`.

## ADR-051 — Barrière de confidentialité avant toute surface bindable

**Décision.** Les ViewModels Dashboard et Serveur ne publient plus `BlockALocalSnapshot`, `SnapshotDataContext` ou `ServerState`. Ils exposent uniquement les scalaires nécessaires à l’affichage. Les libellés de session deviennent génériques et les erreurs utilisateur n’incorporent jamais `Exception.Message`. Le filtre commun couvre également IPv6, UNC et chemins Unix.

**Raison.** Un objet non directement lié par XAML reste accessible aux outils d’automatisation et d’inspection dès qu’il est public. La confidentialité doit donc être garantie à la frontière du ViewModel, et non seulement dans les bindings actuels.

## ADR-052 — Validation de forme JSON à deux niveaux

**Décision.** Chaque parseur Bloc A vérifie explicitement la nature objet de sa racine et de ses entrées structurées. Le lecteur JSON Bloc A transforme en plus toute `InvalidOperationException` de forme en résultat `Invalid`, sans interrompre le moniteur.

**Raison.** Un document peut être syntaxiquement valide tout en ayant une forme incompatible, par exemple `[]` ou une entrée scalaire. La défense locale améliore le diagnostic ; la défense générique garantit la continuité d’actualisation.

## ADR-053 — Rotation détectée par identité et reconstruction de session

**Décision.** Un curseur de log mémorise la date de création et l’empreinte SHA-256 des 256 premiers octets. Toute divergence, troncature ou recul de date reconstruit l’état de la session depuis les sources actives, même si le remplacement est plus grand et plus récent.

**Raison.** La taille et la date de dernière écriture seules ne distinguent pas un append normal d’un remplacement atomique. La reconstruction évite de conserver des joueurs ou événements issus de l’ancien fichier.

## ADR-054 — Moniteur hors UI et arrêt attendu

**Décision.** `HybridLocalSnapshotMonitor` démarre son worker via `Task.Run` et utilise `ConfigureAwait(false)`. L’application conserve la tâche, intercepte la fermeture, annule puis attend cette tâche avant de disposer les lecteurs et de fermer définitivement la fenêtre.

**Raison.** Une continuation capturée par WPF pouvait ramener des lectures sur le thread UI. Une tâche abandonnée pouvait aussi utiliser un lecteur déjà détruit pendant l’arrêt.

## ADR-055 — Cible MVP : poste opérateur local ou LAN explicite

**Décision.** Le MVP doit fonctionner sur la machine BOIII/PinteMod ou depuis un autre poste du même LAN. Le chemin de données, puis l’adresse et le port RCON, seront toujours fournis explicitement. Aucun serveur web, cloud, broadcast ou mécanisme de découverte ne sera ajouté.

**Architecture.** Les lecteurs read-only et les ViewModels validés sont conservés. La prochaine tranche ajoute seulement une configuration opérateur minimale autour de `ServerRoot`. Le transport RCON ultérieur restera isolé derrière une petite interface et ne remplacera pas les lecteurs locaux.

**Données absentes.** Aucun état courant fiable n’existe actuellement pour points, vie, kills, downs, revives, inventaire, atouts, munitions, arme équipée ou Pack-a-Punch. Ces valeurs restent inconnues. Un futur snapshot GSC read-only ciblé par BOIII_XUID ne sera envisagé que si aucune commande RCON existante ne fournit la donnée de façon fiable.

**Sources à confirmer.** `feedback.latest.txt` et `pause.log` ne seront pas intégrés tant que leur emplacement et leur format réels n’auront pas été fournis et validés. Aucune donnée ne sera inventée pour les remplacer.

## ADR-056 — Sonde opérateur limitée aux cinq sources Phase 2.1

**Décision.** Le premier contrôle Local/LAN de la page Paramètres teste uniquement `current_session.json` et les quatre heartbeats déjà validés. Le mode Local refuse les chemins UNC ; le mode LAN exige un UNC explicite. La sonde ne découvre aucune installation, n’active pas automatiquement le fournisseur et ne persiste encore aucune configuration.

**Sécurité.** La validation et les lectures s’exécutent hors du thread UI avec les lecteurs read-only existants. Les résultats ne contiennent ni racine complète, ni exception brute, ni donnée de fichier. Aucun fichier PinteMod n’est créé ou modifié.

**Raison.** Cette tranche rend le futur mode opérateur vérifiable sans élargir les lecteurs, introduire RCON ou modifier les baselines de données. L’activation et la persistance pourront être ajoutées après validation de ce contrôle minimal.

## ADR-057 — Configuration opérateur locale et priorité explicite

**Décision.** Les préférences non sensibles sont enregistrées dans `%LOCALAPPDATA%\PinteMod\ControlCenter\operator-settings.json`. Une source Local/LAN ne peut être activée depuis l’interface qu’après un test fournissant au moins une source lisible. Elle est appliquée au prochain démarrage ; les arguments de ligne de commande restent prioritaires. Une source enregistrée devenue inaccessible provoque un repli sûr en simulation.

**Sécurité.** Le JSON contient uniquement mode, chemin, activation, adresse RCON et port. Aucun secret n’y est accepté. Les écritures atomiques concernent exclusivement le dossier applicatif local et jamais `ServerRoot`.

## ADR-058 — Live Console avec pause d’affichage, pas de collecte

**Décision.** La page Logs conserve le lecteur incrémental validé et ajoute auto-scroll ainsi qu’une pause purement visuelle. Pendant la pause, les snapshots continuent d’être collectés et un compteur indique les événements en attente. À la reprise, le dernier snapshot remplace immédiatement la vue figée.

**Audit.** Les résultats RCON sont ajoutés à un magasin borné à 100 événements en mémoire, catégorie `RCON`, après passage par `LogPrivacyFilter`. Aucun audit RCON persistant n’est écrit à ce stade.

## ADR-059 — RCON BOIII minimal, manuel et en liste blanche

**Décision.** Le transport suit exactement le protocole observé dans l’outil PinteMod existant : UDP, quatre octets `0xFF`, puis `rcon <secret> <commande>` en UTF-8. Seuls `ezzhealth full` et `ezzpausestatus` sont accessibles par l’interface. Aucun texte de commande libre, envoi automatique, broadcast ou découverte n’existe.

**Secret.** Le Control Center crée son propre fichier `%LOCALAPPDATA%\PinteMod\ControlCenter\rcon.secret.dpapi` avec DPAPI `CurrentUser`. Il ne recherche, ne lit et ne réutilise jamais automatiquement le secret DPAPI des outils PowerShell. Le champ WPF est un `PasswordBox` non bindé et est vidé après enregistrement.

**Fiabilité.** Les envois sont sérialisés, limités à trois secondes et les réponses sont bornées puis neutralisées avant affichage. Les actions gameplay restent sur `ISimulationActionService` avec `CommandSent = false`. Aucune commande gameplay réelle ne sera ajoutée avant validation humaine des deux diagnostics.

## ADR-060 — Les sorties de diagnostic WPF utilisent des liaisons explicitement unidirectionnelles

**Décision.** Toute propriété de résultat dont le setter n’est pas public, notamment `RconResponse`, est liée aux contrôles d’affichage avec `Mode=OneWay`, même lorsque le contrôle est déjà marqué `IsReadOnly=True`.

**Raison.** `IsReadOnly` empêche la saisie utilisateur mais ne change pas le mode de liaison par défaut d’un `TextBox`. Une liaison `TwoWay` implicite vers un setter privé provoque une exception différée lors de la mise en page et peut fermer l’application.

**Régression.** Un test WPF STA construit et met en page `SettingsView` avec son vrai ViewModel pour intercepter les erreurs de liaison qui ne sont pas détectées par la compilation XAML.

## ADR-061 — RCON est présenté comme un parcours facultatif guidé

**Décision.** La carte Paramètres n’emploie plus RCON comme une consigne implicite. Elle distingue trois étapes visibles — adresse, secret, test — et précise qu’un serveur BOIII déjà lancé est indispensable. Une copie locale des fichiers peut alimenter les lecteurs read-only mais ne peut pas répondre en RCON.

**Thème.** Tout `Button` sans style explicite hérite du style sombre commun. `PasswordBox` reprend les couleurs, bordures et focus de `TextBox`. Un test runtime vérifie que le fond et le texte ne partagent pas la même couleur.

**Raison.** La sécurité ne suffit pas si l’opérateur ne comprend pas quand une action est nécessaire. Rendre la section facultative et séquentielle évite d’inciter au lancement d’un serveur ou à la manipulation inutile d’un secret.

## ADR-062 — Le paquet MVP Preview est autonome et sans état opérateur

**Décision.** La préversion Windows x64 est publiée en mode self-contained avec .NET 8 et porte la version produit `2.2.0-mvp-preview`. Elle est distribuée dans une archive ZIP accompagnée d’un guide non technique et d’une empreinte SHA-256.

**Exclusions.** Le paquet ne contient ni `operator-settings.json`, ni fichier DPAPI, ni log, ni donnée PinteMod, ni serveur, ni GSC, ni symbole de débogage. La configuration reste créée uniquement dans `%LOCALAPPDATA%` lors d’une action explicite de l’opérateur.

**Raison.** Ce format permet de préparer les essais même si le poste cible ne possède pas le SDK ou le runtime .NET, sans figer ni transférer l’identité ou les secrets d’un autre poste Windows.

## ADR-063 — Aucun bouton Soft Pause sans contrat v0.3 vérifiable

**Décision.** Ne pas inventer ni déduire le nom d’une commande de modification de partie. `ezzpausestatus` reste un diagnostic en lecture seule ; la pause d’affichage de la Live Console reste purement locale. Le bouton de vraie pause serveur attend le script ou la documentation Community Soft Pause v0.3 réellement déployé.

**Audit connexe.** Les commandes joueur de PinteMod v2.1.1 déclarent et résolvent déjà `BOIII_XUID` pour revive, respawn, points, munitions, godmode, armes, atouts et modération. Leur future intégration pourra donc respecter le ciblage exclusif par XUID sans modification GSC.

## ADR-064 — Le contrat Community Soft Pause v0.3 est vérifié, mais les mutations restent verrouillées

**Décision.** La source publique actuelle `BiereFraiche/PinteMod` confirme `ezzpausestatus`, `ezzpauseforce`, `ezzresume`, `boiii/scriptdata/pintemod/remote/feedback.latest.txt` et `boiii/scriptdata/pintemod/logs/pause.log`. Cette preuve lève l’incertitude d’ADR-063 sur le contrat, sans autoriser encore les deux commandes qui modifient la partie.

**Intégration.** Deux lecteurs dédiés observent le feedback structuré et suivent uniquement les nouveaux événements du journal global. Le statut doit être valide, âgé de 45 secondes au maximum et postérieur au manifeste de session ; sinon l’interface affiche `INCONNU`. Le journal global est pris en fin de fichier à l’ouverture et à chaque remplacement afin de ne pas rejouer une ancienne session. Les XUID et champs non autorisés sont exclus avant création des événements bindables.

**Sécurité.** Le Control Center ne crée et ne modifie aucun fichier PinteMod. La commande explicite `ezzpausestatus`, déjà présente dans la liste blanche diagnostic, provoque toutefois côté GSC l’écriture du feedback et d’un événement `STATUS`; cette propriété est documentée. `ezzpauseforce` et `ezzresume` ne seront ajoutées à la liste blanche qu’après validation humaine des deux diagnostics sur un serveur vide.

**Alternative rejetée.** Déduire l’état de pause depuis un ancien `pause.log`, conserver vert un feedback périmé ou activer immédiatement les commandes de mutation risquerait d’afficher une session précédente ou de perturber des joueurs connectés.

## ADR-065 — GitHub main devient la référence déployée complémentaire, sans remplacer la baseline stable

**Décision.** L’archive PinteMod v2.1.1 FINAL reste la baseline serveur validée. Pour les extensions postérieures réellement installées, notamment Community Soft Pause v0.3, le dépôt public `BiereFraiche/PinteMod` branche `main` peut être audité en lecture seule à condition de consigner le commit exact. Le commit de cette passe est `7d5f33489d8635c460d3eb63bb04226c7aa3f326`.

**Usage.** Le catalogue GitHub sert à préparer les listes blanches, validations et interfaces. Il n’autorise ni mise à jour automatique, ni exécution de script, ni commande RCON, ni modification du dépôt. Toute intégration runtime reste soumise aux validations locales et humaines du Control Center.

**Garde-fou UI.** Les contrôles Pause/Reprendre peuvent être visibles avant activation afin d’expliquer le parcours, mais ils n’ont aucun `ICommand` d’envoi et `RealPauseControlsAvailable` reste faux jusqu’au verdict serveur vide. La Live Console expose une catégorie `PAUSE` distincte afin d’éviter la confusion avec sa propre pause d’affichage.

## ADR-066 — Le transport reçu ne vaut pas validation fonctionnelle

**Décision.** Un datagramme RCON non vide ne produit plus automatiquement `Success`. Chaque diagnostic autorisé possède une signature minimale issue du commit GitHub audité : bannière et compteurs PASS/WARNING/ERROR pour Health ; bannière, version, état actif et compteur pour Community Pause. Une réponse ne contenant pas tous les marqueurs devient `UnexpectedResponse`, affichée comme avertissement après neutralisation.

**Raison.** BOIII peut répondre avec une commande inconnue, un message d’un autre module ou une sortie tronquée. Confondre réception réseau et réussite métier donnerait un faux vert à l’opérateur.

**Sécurité.** La liste blanche de transport reste exactement `ezzhealth full` et `ezzpausestatus`. Aucun nouveau texte de commande, envoi automatique ou action gameplay n’est ajouté. Les actions simulées rejettent aussi toute valeur d’énumération inconnue et toute option contenant des caractères de contrôle.

**Précision terrain.** Le premier essai réel a confirmé qu’un serveur BOIII peut répondre uniquement avec l’en-tête UDP pour une commande GSC, exactement comme le prévoit `PinteMod_Remote_RCON.ps1`. Ce cas devient `ENVOYÉ · SANS TEXTE`, distinct d’un timeout et d’un succès prouvé. La preuve fonctionnelle vient alors de la console BOIII ou, pour Community Pause, du feedback local frais.

## ADR-067 — L’instantané de réanimation n’est pas une source d’inventaire

**Décision.** Les champs en mémoire `pintemod_revive_*` observés dans `ezz_admin_commands.gsc` ne seront pas lus ni assimilés à un snapshot joueur. Ils sont internes au mécanisme de réanimation, incomplets, non persistés et sans contrat de lecture locale.

**Conséquence.** Armes, arme équipée, Pack-a-Punch, munitions et atouts restent inconnus dans le mode hybride. Un futur contrat GSC read-only dédié par BOIII_XUID reste nécessaire ; aucune modification GSC n’est autorisée avant sa conception et sa validation.

## ADR-068 — Première mutation limitée à Community Pause avec preuve après envoi

**Décision.** Après validation humaine de `ezzhealth full` (`PASS=51 | WARNING=0 | ERROR=0`) et `ezzpausestatus` (Community Pause v0.3 inactive), la première liste blanche de mutation contient uniquement `ezzpauseforce` et `ezzresume`. Toutes les autres actions joueur et serveur restent sur `ISimulationActionService` avec `CommandSent = false`.

**Barrières.** Un bouton réel n’est exécutable que si le statut Community Pause provient d’une source live, réussie et fraîche, si l’adresse/port sont valides, si le secret DPAPI est disponible et si l’opérateur répond Oui à une confirmation explicite. Pause est désactivée pendant un vote ou lorsque la partie est déjà pausée ; Reprendre est désactivé lorsque la partie ne l’est pas.

**Vérification.** Après la mutation explicitement demandée, le service envoie une seule interrogation `ezzpausestatus`. Le ViewModel attend ensuite un `feedback.latest.txt` plus récent confirmant l’état attendu. Il n’existe aucun retry automatique de la mutation. Un timeout ou un état inchangé produit `RÉSULTAT INCERTAIN` ou `ENVOYÉ · NON CONFIRMÉ` et demande une vérification humaine avant toute répétition.

**Raison.** Le datagramme BOIII peut ne contenir aucun texte et ne prouve donc pas l’effet gameplay. La source locale fraîche reste l’autorité de confirmation et empêche les doubles actions aveugles.

## ADR-069 — Le mode LAN utilise directement le partage PinteModData

**Décision.** Le mode Local conserve une racine `UnrankedServer` contenant `boiii`. Le mode LAN accepte exclusivement un chemin UNC explicite vers le dossier runtime `boiii/scriptdata/pintemod`, conventionnellement partagé en lecture seule sous le nom `PinteModData`. Aucun parcours ou détection de machine n’est ajouté.

**Architecture.** `LocalPinteModOptions` distingue `ServerRoot` et `PinteModDataRoot`, puis expose une racine de données commune aux lecteurs. Les politiques de chemins Ranks, Easter Eggs et Bloc A restent confinées sous la racine explicitement fournie ; les labels publics conservent les chemins logiques complets.

**Sécurité.** Le partage recommandé n’expose ni binaires BOIII, ni GSC, ni fichiers de configuration/secret. Le Control Center l’ouvre uniquement en lecture. SMB reste limité au réseau Windows privé et ne doit jamais être redirigé vers Internet.

**Alternative rejetée.** Partager tout `UnrankedServer` aurait permis de réutiliser l’ancien modèle de chemin, mais aurait inutilement rendu visibles les exécutables et configurations du serveur au poste opérateur.

## ADR-070 — Une racine de partage UNC est valide uniquement pour PinteModData

**Décision.** Windows considère `\\serveur\PinteModData` comme la racine d’un volume UNC. Cette racine est désormais acceptée exclusivement avec le layout `PinteModDataRoot` utilisé par le mode LAN. Une racine de lecteur local telle que `C:\` et une racine UNC utilisée comme `ServerRoot` restent refusées.

**Raison.** Le rejet général des racines de volume empêchait Preview 4 d’utiliser le partage pourtant accessible et explicitement autorisé. L’exception ciblée rétablit le scénario LAN sans élargir le confinement des lecteurs.

**Sécurité.** Le mode LAN exige toujours un UNC explicite, n’effectue aucune découverte, ne sort jamais de la racine fournie et n’obtient qu’un accès en lecture aux fichiers en liste blanche. Les identifiants Windows du partage ne sont ni lus, ni stockés, ni journalisés par l’application.

## ADR-071 — Le booléen Active de Community Pause accepte le format terrain 0/1

**Décision.** Le lecteur strict de `remote/feedback.latest.txt` accepte `Active: 0` et `Active: 1`, observés sur le serveur réel, en plus de `false` et `true` déjà couverts. Toute autre valeur reste invalide.

**Raison.** La commande `ezzpausestatus` a produit un fichier frais et complet avec `Active: 0`. Le rejet de ce booléen empêchait la source locale de devenir fraîche et maintenait les commandes Pause/Reprendre verrouillées malgré un transport et un partage fonctionnels.

**Portée.** Aucun autre champ, chemin, seuil de fraîcheur ou mécanisme de commande n’est modifié. La confirmation d’une mutation continue d’exiger un nouveau fichier local frais décrivant l’état attendu.

## ADR-072 — Le renouvellement du statut Pause reste explicite et proche des commandes

**Décision.** Le panneau Serveur expose `ACTUALISER LE STATUT`, qui envoie uniquement la commande de diagnostic déjà autorisée `ezzpausestatus`, puis attend un nouveau `feedback.latest.txt` local et frais. Aucune interrogation RCON périodique ou automatique n’est ajoutée.

**Raison.** Le statut terrain est volontairement considéré frais pendant 15 secondes. Une pause forcée peut durer 180 secondes : Reprendre devenait donc indisponible lorsque l’opérateur dépassait cette fenêtre. Le renouvellement manuel conserve la preuve fraîche sans obliger à naviguer jusqu’aux Paramètres.

**Sécurité.** Le bouton ne peut pas envoyer Pause ou Reprendre, n’accepte aucun texte libre, utilise l’adresse/port explicites et le secret DPAPI existant, et maintient la liste blanche RCON inchangée. Les mutations restent séparées et soumises à confirmation.

## ADR-073 — Confinement LAN et transaction opérateur RCON globale

**Décision.** Toute cible RCON doit être une adresse IP numérique appartenant à la boucle locale, à une plage privée IPv4, à une adresse link-local ou à une ULA IPv6. Les IP publiques, adresses non spécifiées et noms d’hôte sont refusés à chaque frontière : ViewModel, configuration persistée, service et transport.

**Sérialisation.** Un `IRconOperationGate` partagé protège l’ensemble des transports de diagnostic et de mutation. Un `OperatorRconOperationCoordinator` unique, injecté dans les ViewModels Serveur et Paramètres, sérialise en plus les parcours opérateur complets, y compris confirmation humaine et attente du feedback local.

**Autorisation de mutation.** Après la confirmation Oui/Non, le snapshot est relu avant tout envoi et doit encore autoriser exactement l’action demandée. Dès qu’une mutation a pu être envoyée, Pause et Reprendre restent verrouillés jusqu’à l’observation d’un statut local strictement plus récent, réussi et frais. Un ancien cache ne peut donc pas autoriser un second envoi.

**Arrêt.** La fermeture refuse les nouvelles opérations, annule le moniteur read-only, attend les opérations RCON/ViewModel déjà acceptées, puis seulement dispose les lecteurs et ferme la fenêtre. BOIII reste indépendant du cycle de vie du Control Center.

**Alternative rejetée.** Des sémaphores distincts par service ou la seule désactivation visuelle d’un bouton ne couvrent ni les actions lancées depuis deux pages, ni le délai de confirmation, ni un résultat réseau incertain.

## ADR-074 — Une tentative UDP de mutation vaut livraison potentielle

**Décision.** Après validation de la commande fermée et juste avant l’appel à `IRconClient.SendAsync`, `CommunityPauseCommandService` marque la mutation comme potentiellement envoyée. Une `SocketException`, une erreur d’I/O ou une erreur de transport survenant pendant cet appel produit donc `CommandSent = true`, même si le client n’a pas pu retourner normalement.

**Raison.** UDP ne fournit pas d’acquittement du datagramme. Une erreur pendant `ReceiveAsync` peut arriver après l’émission effective de `ezzpauseforce` ou `ezzresume`. Le service ne peut pas distinguer sans ambiguïté un échec avant émission d’un échec après émission ; le faux positif qui impose une actualisation manuelle est préférable au faux négatif qui autoriserait un double envoi.

**Défense complémentaire.** `ServerViewModel` invalide également l’autorisation si une exception non normalisée échappe au service après le début de l’opération. Pause et Reprendre restent alors verrouillés jusqu’à un statut local réussi, frais et strictement plus récent.

**Portée.** Aucun retry, aucune commande, aucun transport et aucun périmètre métier supplémentaire n’est ajouté.

**Validation.** La contre-revue finale a confirmé la chaîne complète service/ViewModel, les régressions de `SocketException`, le verrou sur ancien snapshot et la liste blanche inchangée. La fondation Bloc B est clôturée sans correction bloquante restante.

## ADR-075 — Le Bloc C commence par cinq diagnostics serveur manuels read-only

**Décision.** Étendre la liste blanche RCON aux seules commandes `ezzmap`, `ezzpowerstatus`, `ezzpapstatus`, `ezzround` et `ezzplayers`. Elles sont accessibles manuellement depuis Paramètres et Serveur, utilisent des valeurs d’énumération fermées, partagent le coordinateur RCON global et ne déclenchent aucun retry ni actualisation automatique.

**Validation des réponses.** Chaque commande possède ses marqueurs attendus. Une réponse vide reste `ENVOYÉ · SANS TEXTE`, une réponse non conforme reste `RÉPONSE NON RECONNUE`, et tout texte affichable traverse le filtre de confidentialité avant le ViewModel.

**Ciblage joueur.** La commande stable `ezzplayers` indique les joueurs connectés mais ne fournit pas de BOIII_XUID fiable. Son intégration reste donc purement informative. Les actions joueur réelles restent bloquées : aucun pseudonyme, slot ou XUID simulé ne peut devenir une cible de mutation.

**Alternative rejetée.** Activer immédiatement les commandes joueur à partir de la liste simulée ou d’un pseudo a été rejeté, car cela violerait le ciblage exclusif par BOIII_XUID et pourrait agir sur le mauvais joueur.

**Validation terrain.** Les cinq commandes ont été observées dans la console BOIII avec leurs signatures attendues. BOIII renvoie un datagramme sans texte au Control Center ; cette absence de corps ne doit donc jamais être transformée en résultat détaillé inventé. L’interface conserve `ENVOYÉ · SANS TEXTE` et oriente l’opérateur vers la console serveur.

## ADR-076 — Les mutations sans feedback local utilisent un verrou à acquittement humain

**Décision.** Autoriser uniquement `ezznextround`, `ezzsetround 2..255`, `ezzpower` et `ezzpap` dans un service dédié à liste blanche fermée. Chaque action exige une confirmation explicite, partage les deux verrous RCON existants, n’effectue aucun retry et traite toute erreur après le début du transport comme une livraison potentielle.

**Retour d’état.** BOIII imprime ces résultats dans sa console mais ne les renvoie pas dans le corps UDP et n’écrit aucun feedback local autoritaire. Le Control Center affiche donc `ENVOYÉ · À VÉRIFIER`, jamais un succès vert. Toutes les mutations, y compris Pause/Reprendre, restent verrouillées jusqu’au clic explicite `J’AI VÉRIFIÉ LA CONSOLE`.

**Bornes.** `ezzsetround` n’accepte que des valeurs prédéfinies comprises entre 2 et 255. Le GSC stable refuse lui-même toute cible inférieure ou égale à la manche actuelle ; aucun argument libre n’est construit depuis une saisie texte.

**Alternatives rejetées.** Une confirmation automatique fondée sur le datagramme vide a été rejetée comme mensongère. Un retry automatique a été rejeté car il pourrait appliquer deux fois une mutation effectivement reçue. Les actions joueur restent exclues faute de BOIII_XUID connecté fiable.

## ADR-077 — Les commandes serveur étendues restent fermées et soumises au même acquittement

**Décision.** La liste blanche d’administration serveur ajoute uniquement la lecture des contrats stables déjà présents dans PinteMod pour la musique de carte, les passages standard, la conservation ou l’élimination des zombies et le délai permanent ou normal des power-ups. Chaque variante correspond à une valeur d’énumération interne ; aucun texte de commande ou argument libre n’est accepté depuis l’interface.

**Sécurité.** Ces commandes utilisent le même endpoint privé explicite, le même secret DPAPI, le même verrou de transport, le même coordinateur opérateur et la même confirmation humaine que les quatre premières mutations serveur. Aucun retry n’est effectué. Dès que l’émission UDP a pu commencer, l’ensemble des mutations reste verrouillé jusqu’à l’acquittement manuel de la partie ou de la console.

**Retour d’état.** En l’absence de snapshot local autoritaire prouvant leur effet, l’interface conserve `ENVOYÉ · À VÉRIFIER`. Un datagramme vide n’est jamais interprété comme un succès gameplay.

**Validation.** Les essais sur serveur réel sont regroupés avec les autres actions dans une unique validation terrain finale afin d’éviter une succession de micro-validations perturbant la partie.

## ADR-078 — Les actions joueur réelles ciblent exclusivement un XUID issu de la session locale

**Décision.** L’audit du Bloc A a établi que `connections.log`, limité à la session active, fournit les transitions `JOIN`, `ACTIVE` et `LEAVE` avec BOIII_XUID. Cette source locale devient l’unique autorité de présence pour les actions joueur. Le XUID complet reste privé au ViewModel et au service de commande ; seuls sa forme abrégée et le pseudo d’affichage sont publics. `ezzplayers` reste informatif et n’est jamais utilisé pour cibler.

**Revalidation.** Après la confirmation humaine et immédiatement avant l’envoi, le snapshot partagé est relu. La source doit encore être locale, réussie et fraîche, et le même XUID doit encore être présent. Toute disparition, rotation de session ou source invalide annule la commande sans transport.

**Liste blanche.** Revive, Respawn, Points bornés, Munitions, Godmode, Téléportation au viseur du joueur, armes, atouts, tous les atouts, Mute, Unmute, Kick, Ban, rôle Helper/Modérateur/Admin et retrait du rôle utilisent des actions typées et des options prédéfinies. Le rôle Owner, les raisons libres, les alias arbitraires et toute construction de commande depuis une saisie texte sont exclus.

**Écritures administratives.** Le Control Center ne modifie aucun fichier PinteMod. Après confirmation, certaines commandes de modération ou de rôle demandent au GSC PinteMod d’effectuer son écriture administrative normale ; cette conséquence est annoncée explicitement dans l’interface.

**Verrou transversal.** Une émission joueur potentielle verrouille Dashboard, Joueurs, Serveur et Community Pause par un état de sûreté partagé. Le verrou est levé uniquement lorsque l’opérateur confirme avoir vérifié la partie ou la console. L’historique joueur reste désactivé « À venir » tant qu’une source locale neutralisée dédiée n’est pas intégrée.

**Alternative rejetée.** Cibler par pseudo, slot, liste simulée ou résultat de `ezzplayers` a été rejeté, car ces identifiants ne garantissent pas l’identité stable BOIII attendue.

## ADR-079 — Le catalogue de cartes est audité sans lecture runtime de server_zm.cfg

**Constat.** La rotation active de la copie de test contient uniquement `zm_tomb`, tandis que la ligne catalogue commentée du même `zone/server_zm.cfg` déclare les 14 cartes officielles installées : Shadows of Evil, Der Eisendrache, Zetsubou No Shima, Gorod Krovi, Revelations, Ascension, Kino der Toten, Moon, Nacht der Untoten, Origins, Shangri-La, Shi No Numa, The Giant et Verrückt.

**Décision.** Le sélecteur WPF reprend ces 14 codes exacts dans une liste blanche statique et testée. La carte courante continue de sélectionner automatiquement l’entrée correspondante lorsque son code provient du snapshot.

**Sécurité.** Le Control Center ne lit pas automatiquement `server_zm.cfg`, car ce fichier de configuration serveur se situe hors des sources locales read-only autorisées et peut contenir d’autres réglages sensibles. L’audit de cette passe a lu uniquement les lignes `sv_maprotation` de la copie de test explicitement indiquée, sans afficher ni conserver d’autre contenu.

**Portée.** Cette décision complète uniquement le menu. Les boutons Changer et Redémarrer restent simulés tant qu’un contrat de commande et une validation destructive dédiés ne sont pas autorisés.

## ADR-080 — Le catalogue de cartes est hybride, local et sans lecture automatique de configuration

**Décision.** Le catalogue combine quatre provenances explicites : les 14 cartes officielles connues, la rotation active collée volontairement par l’opérateur, les cartes custom ajoutées manuellement et la carte courante observée dans le snapshot. Les pages Paramètres et Serveur partagent immédiatement le même état de présentation.

**Import.** Seule une ligne active de forme `set sv_maprotation "..."` est acceptée. Le parseur refuse les commentaires, plusieurs lignes, commandes étrangères, tokens inconnus, codes contenant autre chose que lettres ASCII minuscules, chiffres ou `_`, ainsi que les listes hors limites. Une nouvelle importation remplace la rotation précédente sans supprimer les cartes conservées manuellement ou observées.

**Persistance.** Le catalogue est écrit uniquement sous `%LOCALAPPDATA%\PinteMod\ControlCenter\map-catalog.json` par remplacement atomique local. Supprimer une carte manuelle retire seulement ce marquage local et ne modifie ni PinteMod, ni `server_zm.cfg`, ni la rotation réelle du serveur.

**Alternative rejetée.** Rechercher ou lire automatiquement `zone/server_zm.cfg` est exclu : ce fichier se trouve hors du partage PinteModData autorisé et peut contenir des réglages sensibles. Un sélecteur fondé uniquement sur une ancienne copie serait également faux pour les serveurs custom.

## ADR-081 — Les contrats audités déterminent ce qui devient réel

**Décision.** Les diagnostics manuels ajoutent uniquement `ezzmapaudit full`, `ezzeventstatus` et `ezzpowerups`, avec marqueurs de réponse dédiés. L’action joueur Power-up utilise uniquement `ezzpowerup <BOIII_XUID> <alias>` et neuf alias canoniques fermés : `maxammo`, `instakill`, `doublepoints`, `firesale`, `carpenter`, `nuke`, `deathmachine`, `freeperk` et `shield`.

**Sécurité.** Le joueur est revalidé par BOIII_XUID après confirmation, aucun alias libre n’entre dans la commande, aucun retry n’existe et toute émission potentielle active le verrou transversal déjà validé.

**Limite volontaire.** Le changement de carte ne dispose pas d’une commande GSC générique sûre dans la référence stable. Les boss et événements exposent des contrats fortement dépendants de la carte ; la commande générique de boss auditée n’effectue pas elle-même le spawn attendu. Ces mutations restent donc simulées au lieu d’inventer une compatibilité.

## ADR-082 — L’historique de modération est une consultation locale ponctuelle

**Décision.** Le bouton Historique lit à la demande `boiii/scriptdata/pintemod/moderation/history/<BOIII_XUID>.json` pour le joueur sélectionné. Cette lecture n’utilise ni RCON, ni pseudo, ni slot, ni cache persistant et n’écrit aucun fichier.

**Confinement et confidentialité.** Le XUID doit être valide, le chemin résolu doit rester sous la racine PinteMod autorisée et aucun point de réanalyse existant n’est suivi. Seul le JSON actif de 64 Kio maximum est accepté ; `.tmp` et `.bak` sont ignorés. L’identité du fichier doit correspondre au XUID demandé, mais le résultat public ne contient jamais cet identifiant ni le chemin réel. Les derniers libellés traversent le filtre de confidentialité avant affichage.

**Comportement dégradé.** Un fichier absent produit un état neutre « Aucun historique local ». Un fichier vide, partiel, de schéma inconnu ou incohérent produit un état indisponible sans interrompre l’actualisation ni substituer une donnée simulée.

## ADR-083 — Les preuves opérateur copiées proviennent uniquement de l’affichage neutralisé

**Décision.** Paramètres et Serveur peuvent copier la dernière réponse diagnostique affichée. La Live Console peut copier uniquement les événements correspondant au filtre et à la recherche actifs. Aucun fichier n’est exporté et aucune lecture supplémentaire n’est déclenchée.

**Confidentialité.** La copie reçoit les mêmes chaînes neutralisées que l’interface ; elle n’accède ni aux paquets UDP bruts, ni au secret RCON, ni aux XUID internes, ni aux chemins sources. Les événements copiés conservent seulement heure, catégorie, titre et détail déjà filtrés.

**Fiabilité.** Le presse-papiers est une dépendance de présentation injectable. Son indisponibilité produit un message local et ne fait pas planter l’application. Les tests utilisent un faux presse-papiers et n’accèdent pas au presse-papiers Windows réel.

**Alternative rejetée.** Un export automatique de logs ou de diagnostics vers un fichier a été rejeté : il introduirait une nouvelle écriture, un emplacement à sécuriser et un risque de persistance de données opérateur inutile pour la V1.

## ADR-084 — La clôture V1 est séparée des futures extensions PinteMod

**Décision.** La V1 opérateur sera clôturée sur les contrats stables réellement disponibles dans PinteMod v2.1.1. Les fonctions qui exigent de nouveaux snapshots ou contrats GSC — inventaire joueur détaillé, changement de carte générique, boss et événements multi-cartes — constituent un futur lot d’extension PinteMod et ne conditionnent pas artificiellement la stabilité de la V1 actuelle.

**Raison.** Leur activation ne dépend pas d’un simple bouton WPF : elle exige une autorité runtime, un format versionné, une compatibilité par carte et des essais serveur dédiés. Les considérer comme du « code restant » sous-estimerait le risque et encouragerait l’invention de données ou de commandes.

**Méthode.** Les travaux futurs restent organisés en lots cohérents avec tests ciblés internes, une seule suite Debug/Release finale et une seule validation humaine regroupée. Les prompts de reprise et de revue sont conservés sous `docs/` afin de maintenir ces garanties entre les sessions.

## ADR-085 — La Preview 13 validée devient la candidate V1 sans recompilation comportementale

**Décision.** La revue globale indépendante conclut à zéro blocage obligatoire et autorise la clôture du code V1. Le paquet Preview 13 couvert par cette revue est donc promu octet pour octet sous le nom `v2.2.0-rc.1`, sans changement de code, de XAML, de contrat RCON ou de contenu binaire.

**Traçabilité.** L’empreinte SHA-256 recalculée par la revue correspond exactement à `8ED173DEF5D67B14791433AAE1B60EBD136BA6F3963D972CE59E5D5D59D205F5`. Renommer la copie de distribution ne change pas cette empreinte ; la Preview 13 reste la provenance auditée.

**Jalon stable.** La revue de code ne remplace pas l’observation terrain des mutations qui n’ont pas toutes été déclenchées sur un serveur réel. La candidate peut être distribuée pour cette validation groupée, mais le tag stable `v2.2.0` attend son résultat. Les fonctions sans contrat PinteMod sûr restent simulées et ne bloquent ni la candidate ni la V1.

## ADR-086 — La seconde revue stricte retire RC1 et impose une ouverture vérifiée par handle

**Décision.** Le verdict plus strict reçu après la première promotion prévaut pour la clôture. `v2.2.0-rc.1` est retirée et ne doit plus être distribuée. Une nouvelle `v2.2.0-rc.2` est compilée depuis les sources corrigées et doit recevoir sa propre revue finale.

**Confidentialité du paquet.** Les identifiants de simulation et contrats utilisent uniquement le préfixe réservé `000000000000000` avec un suffixe de test. Les builds Release sont déterministes, sans symboles, et cartographient les chemins de source. Le contrôle de paquet combine le scan des chaînes sources/contrats avec la recherche des valeurs explicitement interdites et des racines privées dans les assemblies applicatives ; les jetons techniques .NET ne sont pas interprétés comme des XUID.

**Messages publics.** Les erreurs de lecture renvoient uniquement des états et messages fermés (`AccessDenied`, `IoError`, `Invalid`, `Unavailable` ou équivalents existants). `Exception.Message` d’une exception système ne constitue jamais une donnée bindable. Les détails bruts ne sont pas ajoutés à un autre canal dans cette correction.

**Confinement atomique.** Les politiques de chemin restent la première barrière. Le fichier est ensuite ouvert une seule fois en lecture avec partage contrôlé ; `GetFinalPathNameByHandle` vérifie la cible réellement obtenue avant toute lecture et le même handle vérifié alimente le `FileStream`. Une cible différente, un refus de politique ou une forme invalide devient un état local contrôlé. Les chemins UNC explicitement autorisés sont normalisés sans découverte automatique.

**Diagnostic UDP.** Une commande de diagnostic inconnue est rejetée avant transport avec `CommandSent = false`. Dès que l’appel allowlisté à `IRconClient.SendAsync` va commencer, toute erreur de transport est traitée conservativement avec `CommandSent = true`, sans retry. Cette règle aligne les diagnostics sur les mutations déjà validées.

**Portée.** Aucun contrat RCON, commande, fonction métier, lecteur de source supplémentaire, écriture PinteMod, processus, port, découverte réseau ou GSC n’est ajouté.

## 2026-08-12 — Overlay runtime post-RC2 sans modification PinteMod

**Décision.** Le heartbeat global et le snapshot runtime ne sont pas recréés : ils existent dans PinteModReal via `ezz_admin_control_center_runtime.gsc` v0.1.2. Le Control Center ajoute deux lecteurs dédiés et un overlay final, placé après les providers déjà validés, afin que les logs ne puissent pas écraser une valeur runtime fraîche et autoritaire.

**Autorité.** `current_session.json` reste l’identité de session. Le heartbeat PinteMod prouve uniquement l’état global lorsqu’il est frais et lié à cette session. Le snapshot runtime remplace uniquement les champs qu’il fournit lorsqu’il est lu avec succès, frais et cohérent avec la session et la carte actives. Les logs restent le repli inféré et la source des événements.

**Temps.** `updated_at_utc` vide est conforme au producteur GSC. La fraîcheur repose sur le LastWriteTimeUtc du fichier réellement ouvert et vérifié : Fresh jusqu’à 15 secondes, Stale jusqu’à 45 secondes, Expired au-delà. Un heartbeat expiré devient Inconnu, jamais automatiquement Hors ligne.

**Cache et confinement.** Le cache est invalidé lors d’un changement de session. `.tmp` et `.bak` restent exclus. Les deux fichiers passent par `VerifiedReadOnlyFile`, le contrôle de taille, la détection de modification pendant lecture et trois tentatives. Aucun mécanisme d’écriture PinteMod n’est ajouté.

**Joueurs.** Le snapshot runtime devient la source de présence, client, vie, points et inventaire. Rôle, langue et pays sont enrichis uniquement par BOIII_XUID. L’état Mute n’étant pas autoritaire dans le contrat, il reste Inconnu. Les ViewModels ne reçoivent que le XUID abrégé.

**Alternatives rejetées.** Modifier PinteMod, recréer les mêmes fichiers sous un autre schéma, utiliser l’horodatage UTC vide comme erreur, réutiliser une ancienne session en cache ou activer ChangeMap/RestartMap/événements/boss sans contrat fermé ont été rejetés.

## ADR-088 — Le catalogue d’armes joueur est centralisé et filtré par le runtime

**Décision.** Les 19 alias standard/universels et tous les alias spéciaux annoncés par PinteMod Weapons v0.5.2 résident dans `PlayerWeaponCatalog`, dans Core. Le ViewModel et le service RCON utilisent cette même autorité fermée afin d’éviter deux listes divergentes.

**Contexte carte.** Les armes standard sont toujours visibles. Une arme spéciale n’est affichée que si le snapshot runtime est local, réussi, frais, de la session active et cohérent avec la carte. Une carte inconnue n’obtient aucune spécialité. Le service accepte seulement les alias canoniques fermés ; PinteMod vérifie ensuite leur disponibilité réelle sur la carte.

**Alternative rejetée.** Une saisie libre, un identifiant moteur, un synonyme technique ou une lecture de sortie console `ezzweapons` ne devient jamais une option de commande.

## ADR-089 — Le PAP de l’arme tenue et le retrait d’atout réutilisent la sûreté joueur existante

**Décision.** `ezzpapweapon <BOIII_XUID>` devient l’action typée `PackAPunchCurrentWeapon`. `ezzremoveperk <BOIII_XUID> <alias>` devient `RemovePerk` avec les neuf alias déjà bornés. Les deux passent par la confirmation, la revalidation XUID post-confirmation, le verrou transversal et l’acquittement manuel existants.

**État runtime.** L’interface désactive le PAP si aucune arme équipée n’est observable ou si son état est explicitement `upgraded`. Elle ne prétend pas connaître `can_upgrade_weapon` : PinteMod reste l’autorité finale et peut refuser proprement une arme incompatible.

**Alternative rejetée.** `ezzperktoggle` n’est pas exposé car une même action peut donner ou retirer selon un état devenu obsolète. `ezzclearperks` n’est pas ajouté : il est destructif, redondant et n’apporte pas assez de valeur quotidienne.

## ADR-090 — Une réponse RCON vide peut céder la présentation à une source locale autoritaire

**Décision.** Une réponse RCON normale reste prioritaire. Pour Carte, Courant, PAP de carte, Manche et Joueurs, une réponse vide peut afficher le runtime uniquement s’il est local, frais, de la session active et cohérent avec la carte. Le texte précise qu’il s’agit d’un état local autoritaire, jamais de la sortie console exacte.

**Confidentialité.** Le fallback Joueurs n’expose ni XUID complet, ni chemin, IP ou GUID. Les pseudos passent par le filtre de confidentialité. Community Pause conserve son feedback spécialisé. Health peut montrer un résumé des heartbeats frais, avec la mention explicite qu’il ne remplace pas les 51 contrôles de `ezzhealth full`.

**Absence de contrat.** Audit carte, événements et catalogue power-ups indiquent seulement que la commande a été exécutée mais que la sortie console n’a pas été transportée. Aucun scraping de console, lecture arbitraire de log, port ou transport supplémentaire n’est ajouté.

## ADR-091 — Les actions joueur extensibles sont regroupées dans des grilles responsives autonomes

**Décision.** La carte « Armes & Atouts » utilise trois `ResponsiveUniformGrid` indépendantes pour les armes, les atouts et les power-ups. Chaque groupe possède son propre nombre maximal de colonnes et revient automatiquement à la ligne selon la largeur réellement disponible. Les sélecteurs n’ont plus de largeur fixe.

**Extensibilité.** Un futur bouton est ajouté à la grille de sa famille. Il occupe une nouvelle cellule ou passe à la ligne dans ce seul groupe, sans modifier l’ordre visuel des sélecteurs et actions des autres familles.

**Lisibilité.** Les commandes longues utilisent un libellé centré avec retour à la ligne. Les marges restent identiques entre sélecteurs et boutons afin de conserver une hiérarchie stable dans les fenêtres petites comme en 1920×1080.

**Alternative rejetée.** Un unique `WrapPanel` mélange les familles selon la place restante et rend la disposition dépendante de la longueur des libellés. Ajouter des largeurs fixes supplémentaires aurait seulement déplacé le défaut vers d’autres dimensions de fenêtre.

## ADR-092 — La revue post-RC2 utilise une preuve globale et laisse la RC2 intacte

**Décision.** Le heartbeat et snapshot runtime, les correctifs terrain armes/PAP/diagnostics et le correctif responsive sont regroupés dans une seule revue post-RC2. La base `90d4922cb663e4b8d923ecfb1681483d78db5126` reste la RC2 validée ; aucun tag ni asset RC2 n’est remplacé.

**Preuves.** L’archive de revue contient les sources suivies exactes, le diff RC2 vers la tête de revue, la liste des commits, les résultats Debug/Release, la procédure terrain restante, le paquet Windows autonome déjà audité et un manifeste SHA-256 couvrant chaque élément.

**Périmètre.** La revue doit rechercher uniquement les régressions concrètes et les risques des ajouts post-RC2. ChangeMap, RestartMap, TriggerEvent et SpawnBoss restent simulés ; les futurs contrats PinteMod `capabilities` et `action_feedback` ne sont pas anticipés.

**Alternative rejetée.** Modifier ou republier la RC2 pour y intégrer ces extensions brouillerait la baseline déjà validée. Une série de micro-revues isolées masquerait les interactions entre lecteurs runtime, autorisations joueur, diagnostics et interface.

## ADR-093 — Limite et fraîcheur JSON proviennent du flux et du handle vérifié

**Décision.** `ReadOnlyJsonFileReader` ouvre d’abord la cible avec `VerifiedReadOnlyFile`, puis obtient longueur et `LastWriteTimeUtc` avec `GetFileInformationByHandle`. Ces métadonnées sont relues sur le même handle après consommation. Aucun `FileInfo` ou accès au chemin ne participe à la fraîcheur, à la vérification avant/après ou au résultat public d’une lecture ouverte.

**Borne mémoire.** Le flux est consommé par une boucle qui s’arrête à `maximumFileSizeBytes + 1`. La présence de cet octet supplémentaire suffit à classer la source comme anormalement volumineuse, avant parsing. La mémoire allouée ne peut donc pas croître jusqu’à l’EOF d’un producteur concurrent.

**Remplacement du chemin.** Si le chemin autorisé est remplacé après l’ouverture, les octets parsés et les métadonnées restent ceux du handle initial vérifié. Le fichier nouvellement placé au même chemin sera considéré uniquement lors d’une lecture ultérieure.

**Erreurs.** Après ouverture, les erreurs de contrat ou de JSON utilisent uniquement l’horodatage déjà acquis depuis le handle. Avant ouverture, un fichier ou dossier absent produit l’état `Missing` sans tentative de lire des métadonnées par le chemin.

**Alternative rejetée.** Contrôler la taille avec `FileInfo` puis utiliser `CopyToAsync` jusqu’à EOF laisse une fenêtre de croissance non bornée. Relire ensuite `FileInfo(path)` peut associer les octets de l’ancien handle à la date d’un fichier de remplacement.

## ADR-094 — Le lot post-RC2 validé passe à une unique validation terrain groupée

**Décision.** La contre-revue du lecteur JSON est validée sans blocage le 2026-08-13. La révision applicative `0e4e09284ab8523dc1bb86ce4f162c1aae6ee0ac` devient la candidate terrain du lot post-RC2.

**Étape suivante.** Une seule session terrain regroupe les fallbacks diagnostics, armes standard et spéciale, Pack-a-Punch de l’arme tenue, attribution/retrait d’atout et power-up joueur. Chaque mutation reste confirmée, sans retry, puis acquittée après vérification de la partie ou de la console.

**Limite.** La modération réelle à deux comptes reste volontairement hors de cette validation. ChangeMap, RestartMap, TriggerEvent et SpawnBoss restent simulés jusqu’à l’existence de contrats PinteMod fermés et observables.

## ADR-095 — La candidate post-RC2 est validée sur le terrain

**Décision.** La validation terrain groupée est déclarée réussie par l’opérateur le 2026-08-13. La révision applicative `0e4e09284ab8523dc1bb86ce4f162c1aae6ee0ac` ne présente plus de blocage connu dans le périmètre livré.

**Publication.** La clôture technique n’autorise pas implicitement une mutation GitHub. Toute fusion, création de tag, release stable ou remplacement d’asset exige un ordre explicite. La RC2 historique reste intacte.

**Extensions futures.** La modération à deux comptes et les fonctions sans contrat PinteMod stable ne sont pas transformées en dette bloquante de cette livraison. Elles feront l’objet de travaux distincts si les contrats nécessaires deviennent disponibles.

## ADR-096 — Les serveurs sont des contextes isolés, pas des vues partageant un singleton

**Décision.** La fenêtre peut accueillir jusqu’à huit onglets serveurs. Chaque onglet possède son propre `ShellViewModel`, snapshot store, moniteur local, sélection joueur, activité opérateur, verrou de mutation, coordinateur RCON, secret DPAPI et catalogue de cartes. Seuls la fenêtre et le presse-papiers Windows sont des ressources de présentation communes.

**Migration.** Le profil `primary` réutilise exactement les emplacements historiques `operator-settings.json`, `rcon.secret.dpapi` et `map-catalog.json`. Les profils supplémentaires résident sous un dossier local dédié et ne sont jamais créés à partir d’une découverte réseau ou d’une installation détectée. Un nouvel onglet démarre toujours en simulation jusqu’à activation explicite de sa source.

**Cycle de vie.** Tous les moniteurs peuvent observer leurs racines read-only en parallèle, mais chaque profil sérialise ses propres opérations RCON. À la fermeture ou au retrait d’un onglet, les nouvelles opérations sont refusées, le moniteur est annulé, les opérations acceptées sont attendues, puis les lecteurs sont détruits. Retirer un onglet ne supprime pas automatiquement sa configuration ou son secret protégé et ne touche jamais BOIII.

**Interface.** Le nom de l’onglet est un libellé local distinct du nom public du serveur. Les collections utilisées par les ComboBox ne sont remplacées que si leur contenu change, afin que l’actualisation automatique ne réinitialise pas leur défilement.

**Limite de contrat.** L’audit de PinteModReal n’a identifié aucune commande fermée pour changer le nom public BOIII ou le mot de passe de connexion joueur. Ces mutations ne seront pas construites à partir de commandes moteur supposées ou de texte libre. Elles attendent un contrat PinteMod validé, une validation stricte, une confirmation humaine et un feedback observable.

## ADR-097 — `g_password` est une donnée éphémère distincte du secret RCON

**Décision.** Le mot de passe serveur demandé par l’opérateur désigne la dvar de connexion joueur `g_password`. Il ne doit jamais être confondu avec le secret RCON DPAPI. Le futur écran utilisera un `PasswordBox` non bindé ; la valeur ne sera ni persistée, ni réaffichée, ni copiée, ni incluse dans l’activité opérateur. Une action séparée retirera le mot de passe sans demander de valeur vide dans un champ de commande libre.

**Transport.** Un encodage hexadécimal ou Base64 n’apporte aucune confidentialité. En l’absence de preuve d’un transport applicatif protégé côté PinteMod, la mutation sera réservée au loopback. Le mode LAN pourra continuer à observer et administrer les actions non sensibles, mais ne transmettra pas `g_password`.

**Contrat requis.** PinteMod doit fournir une commande dédiée à paramètres strictement bornés, sans journalisation de la valeur, ainsi qu’un feedback ne contenant que `join_password_enabled` et un résultat fermé. Le Control Center n’enverra jamais directement une commande libre `set g_password ...`.

## 2026-08-14 — Contrats Control Center v1 post-RC2

- Les quatre documents PinteModReal sont agrégés dans `BlockALocalSnapshot.ControlCenterContracts` afin que toutes les pages consomment le même snapshot partagé et qu’aucune lecture ne soit déclenchée depuis le thread UI.
- Un lecteur unique expose quatre résultats séparés avec métadonnées indépendantes. Il réutilise le lecteur JSON et le handle vérifié déjà audités, avec limites 16/4/4/4 Kio.
- Les schémas JSON validés au commit PinteModReal `e279a59` sont versionnés sous `app/contracts/control-center/v1` et copiés dans la publication.
- `supported` décrit uniquement la compatibilité PinteMod. Le catalogue de cartes installées reste inconnu et `change_map=false` ne peut jamais autoriser une mutation.
- Les alias boss publiés doivent aussi appartenir à la liste fermée connue du contrat v1 avant construction de la commande.
- Les commandes contractuelles réutilisent `IServerAdministrationCommandService`, le gate RCON, le coordinateur opérateur et le verrou humain existants. Aucun deuxième canal RCON n’est créé.
- Une action est revalidée après confirmation puis corrélée par `request_id`. Restart exige en plus une nouvelle session et un `map_transition` actif ; hostname/CLEAR exigent une révision d’identité strictement supérieure.
- Une absence de feedback ou une transition lente produit « envoyé, non confirmé » avec verrou anti-répétition, jamais un faux échec.
- SET `g_password`, `ezzccmap` et `ezzccevent` restent volontairement absents du constructeur de commandes.

## ADR-098 — Les contrats Control Center v1 sont des preuves locales corrélées, pas des promesses de capacité

**Décision.** Les quatre fichiers PinteModReal v1 sont lus uniquement sous la racine serveur explicitement configurée, via le lecteur borné et le handle déjà vérifié. Une donnée n’autorise une action que si sa provenance est locale, sa lecture réussie, sa fraîcheur valide et sa session/carte cohérente. Une valeur conservée en cache reste visible comme périmée mais ne peut jamais autoriser une mutation.

**Corrélation.** Chaque action réelle reçoit un `request_id` unique. Le résultat doit correspondre à l’action et au `request_id`, être postérieur au snapshot de départ et, selon le cas, présenter une séquence plus récente, une nouvelle session de transition ou une révision d’identité strictement croissante. `accepted` et une transition lente ne sont jamais assimilés à un succès.

**Périmètre fermé.** Seules `ezzccrestartmap`, `ezzccboss`, `ezzccsethostname` et `ezzccclearjoinpassword` sont ajoutées. `change_map=false` maintient Change Map en simulation ; `ezzccevent` et `ezzccsetjoinpassword` sont absentes. La valeur de `g_password` n’est jamais lue, transportée ou affichée.

**Alternative rejetée.** Déduire qu’une carte `supported` est installée, accepter un alias non publié, ou confirmer une action à partir de la seule réponse UDP contournerait l’autorité locale structurée et les garanties de revalidation déjà validées.

## ADR-099 — Une incertitude UDP n’interrompt jamais l’observation locale d’une action contractuelle

**Décision.** Pour les quatre actions Control Center v1, `CommandSent = true` suffit à démarrer la phase d’observation locale, quel que soit le statut final du transport (`SentAwaitingManualVerification`, `DeliveryUnknown` ou `TransportError`). Une exception non normalisée après le début possible du transport suit la même politique conservatrice.

**Confirmation.** Le transport ne prouve jamais à lui seul l’application. Seuls le feedback frais/corrélé et, selon l’action, la transition de session ou la révision d’identité peuvent produire « appliqué, confirmé localement ».

**Absence de preuve.** L’expiration de l’observation conserve « envoyé, non confirmé » et le verrou humain. Elle ne déclenche ni retry RCON, ni faux échec, ni déverrouillage automatique.

**Validation.** La contre-revue indépendante du 2026-08-14 valide cette politique sans blocage. Le lot peut passer à une seule validation terrain groupée sur la copie de test, uniquement après compilation GSC réussie. Cette autorisation ne concerne ni le serveur de production ni un serveur occupé.

## ADR-100 — La validation des contrats utilise une copie récente et isolée de Server3

**Décision.** La copie obsolète n’est pas utilisée pour conclure sur les contrats v1. Une copie récente est préparée sous `<COPIE_SERVEUR_TEST>`, avec sauvegarde externe récupérable, réseau limité au LAN et port distinct de la production.

**Déploiement minimal.** Seuls `ezz_admin_events.gsc` et le nouveau `ezz_admin_control_center_contracts.gsc` proviennent de la candidate `e279a59`. La variante `ezz_admin_music.gsc` présente sur Server3 est conservée afin de ne pas écraser une évolution terrain sans rapport avec les contrats.

**Barrière.** La copie ne doit être reliée au Control Center qu’après chargement GSC sans erreur. La préparation n’autorise aucune installation directe sur Server3 de production.

## ADR-101 — Un diagnostic RCON sans rapport textuel peut être complété par une preuve locale explicitement distincte

**Décision.** Une réponse BOIII vide ou ne transportant pas le rapport complet de `ezzhealth full` ne devient pas un faux rapport console. Le Control Center affiche séparément le résultat du transport et le résumé provenant d'une source locale fraîche et cohérente.

**Validation terrain.** Sur la copie isolée écoutant sur `127.0.0.1:27121`, le diagnostic a été envoyé et le fallback local a conclu uniquement `PinteMod : SAIN`. Cette preuve valide le canal RCON et le mécanisme de fallback, sans prétendre transporter les 51 contrôles de la console.

**Limite.** Cette validation n'autorise aucun retry automatique et ne confirme aucune mutation contractuelle. Les quatre actions réelles restent soumises à leur feedback local corrélé et à leur verrou humain.

## ADR-102 — Les contrats Control Center reflètent les types JSON natifs réellement produits par BOIII

**Décision.** Les champs numériques (`schema_version`, séquences, compteurs, révision et temps moteur) sont des entiers JSON bornés. Les indicateurs de capacité et `join_password_enabled` sont des booléens JSON natifs. Les variantes citées (`"1"`, `"true"`, `"false"`) sont refusées par le lecteur contractuel.

**Motif.** Le builtin BOIII `jsonset` sérialise ces valeurs sous leur type JSON natif même lorsque le GSC les construit initialement comme texte. Le terrain a démontré que les anciens schémas à chaînes ne décrivaient donc pas les fichiers effectivement produits.

**Sécurité.** Ce changement n'assouplit ni les objets fermés, ni les bornes, ni les listes blanches, ni la fraîcheur, ni la corrélation session/carte. Il ne change aucune commande RCON et ne transforme aucune source périmée en autorité.

**Synchronisation.** Les schémas embarqués du Control Center sont corrigés immédiatement. Les copies documentaires correspondantes de PinteModReal devront être synchronisées avant sa prochaine publication ; le générateur GSC actuel émet déjà les valeurs natives attendues.

## ADR-103 — Les onglets serveurs ne font pas partie de la zone de déplacement

**Décision.** Le chrome de fenêtre réserve une rangée supérieure de 34 px uniquement au déplacement et aux boutons Réduire/Agrandir/Fermer. Les onglets serveurs occupent une rangée distincte juste en dessous et restent marqués interactifs dans le chrome WPF.

**Motif.** Un onglet ne doit jamais entrer en concurrence avec le glisser de fenêtre. Cette séparation reste stable lorsque de nouveaux serveurs ou boutons sont ajoutés.

## ADR-104 — Le hostname est persistant mais `g_password` reste éphémère et loopback

**Hostname.** PinteMod persiste uniquement le nom public validé dans `pintemod/config/control_center_identity.json`, via son écriture JSON sûre, puis le restaure au prochain chargement. Ce fichier public ne contient aucun mot de passe. Le titre natif de la fenêtre BOIII peut rester figé ; `server_identity.json` et le navigateur de serveurs sont les observations pertinentes.

**Mot de passe joueur.** La valeur passe uniquement par une méthode dédiée, un `PasswordBox` non bindé et la commande fermée `ezzccsetjoinpassword`. Elle est limitée à 4–32 caractères ASCII, n’est jamais incluse dans un modèle public, un feedback, un snapshot, un fichier ou une activité opérateur, et n’est autorisée que si l’endpoint est loopback. Le mode LAN est refusé avant lecture du secret RCON et avant transport.

**Persistance.** `g_password` reste runtime uniquement. Un redémarrage complet recharge la configuration serveur déjà administrée hors Control Center. Persister cette valeur dans PinteMod, la configuration opérateur ou DPAPI a été rejeté afin de ne pas créer un second magasin de secrets.

**Gate terrain.** La fonctionnalité reste candidate jusqu’à un test avec valeur synthétique unique et recherche dans toutes les sorties et fichiers de la copie de test. Toute trace impose la désactivation de la capability. Aucun retry automatique et aucune commande libre ne sont autorisés.

**Candidate.** La première candidate intégrant cette décision embarque la révision `3d624fa3b09490d005b3cf65ad24ef081a8a7da5`. Son paquet autonome passe l’audit de publication ; cela ne remplace pas le gate de confidentialité terrain.

## ADR-105 — `live_steam_server_name` est l’autorité du nom public BOIII

**Constat terrain.** La copie Server3 configure le nom présenté aux joueurs avec `live_steam_server_name`. Modifier uniquement `sv_hostname` met à jour une dvar secondaire et le snapshot interne, mais ne constitue pas la mutation du nom public attendue.

**Décision.** Le contrat v0.1.3 observe, applique, persiste et restaure `live_steam_server_name`. `sv_hostname` reçoit la même valeur uniquement pour compatibilité. Aucun CFG n’est lu ou réécrit par le Control Center ou le GSC.

**Couleurs.** La grammaire accepte les couples BOIII `^0` à `^9`. Un caret isolé, `^x`, les séparateurs de commande et tout caractère hors alphabet fermé sont refusés avant transport. La limite de 64 caractères porte sur la chaîne brute, codes compris.

**Mot de passe.** Le booléen local `join_password_enabled=true` prouve que la dvar est active, mais son effet doit être vérifié sur une nouvelle connexion. Les joueurs déjà connectés ne sont ni expulsés ni redemandés automatiquement.

**Candidate.** La révision `7bdb22fbcc1a69b4768bb59afaf3bb72295f2004` consomme exclusivement le contrat v0.1.3 et refuse donc silencieusement de réactiver les contrôles sur un ancien GSC v0.1.2.

## ADR-106 — `net_password`, et non `g_password`, protège les connexions Ezz BOIII

**Constat terrain.** Un client totalement déconnecté et sans mot de passe configuré a pu rejoindre alors que `join_password_enabled=true` reflétait `g_password`. Le booléen prouvait donc uniquement qu’une dvar sans effet sur ce chemin était non vide.

**Autorité BOIII.** Le code public Ezz BOIII enregistre `net_password`, publie son hash dans `getInfo` et compare ce hash côté client avant la connexion. Le contrat PinteMod v0.1.4 définit, efface et observe exclusivement `net_password`. `g_password` n’est plus présenté comme une protection des connexions directes.

**Validation terrain du 2026-08-16.** Le comportement autoritaire est confirmé sur la copie de test : absence et valeur incorrecte refusées, valeur correcte acceptée. Cette validation lève le dernier verrou fonctionnel avant le gel et l’audit de la candidate stable, sans autoriser la persistance ou l’exposition de la valeur.

## ADR-107 — La candidate post-RC2 devient la version stable `2.2.0`

**Décision.** Après validation terrain de `net_password`, le code fonctionnel est gelé et l’`InformationalVersion` devient `2.2.0`. Les mentions « Prototype » et « Release Candidate 2 » sont retirées des surfaces distribuées afin que le binaire, l’interface, le README et le fichier de démarrage décrivent la même version.

**Portée.** Cette promotion ne crée aucune commande, aucun lecteur, aucun accès réseau supplémentaire et ne modifie aucun contrat PinteMod/GSC. La publication GitHub reste distincte et exige toujours un ordre explicite après la revue du ZIP stable.

**Confidentialité.** La valeur reste éphémère, transmise uniquement par la commande fermée et loopback, absente des snapshots, feedbacks, ViewModels, fichiers et journaux applicatifs. Seul `join_password_enabled` est publié. Le hash BOIII étant actuellement FNV1a-64 non salé, l’interface parle d’« isolation réseau BOIII » et recommande implicitement une valeur longue et aléatoire, sans revendiquer une protection cryptographique forte.

**Présentation.** Les diagnostics secondaires des services et les XUID déjà abrégés des profils Ranks sont repliés par défaut dans des contrôles accessibles. L’état principal reste visible. Les déclencheurs utilisent une typographie secondaire de 8 px et une flèche atténuée afin de ne pas concurrencer les données principales. Le numéro de meilleure manche est affiché sans préfixe `M`. Les champs actifs Nom et Mot de passe réseau utilisent un fond relevé et une bordure bleue ; le bouton Nom reste désactivé tant que la valeur saisie est identique au nom observé.

## ADR-107 — La compatibilité dépend des versions de schéma et de commandes

**Constat.** La structure publique de `control_center_capabilities.json` reste identique entre v0.1.3 et v0.1.4. La v0.1.4 corrige l’autorité interne du mot de passe réseau BOIII sans ajouter de champ ni de commande au contrat consommé par l’application. Exiger seulement v0.1.3 rendait les capacités v0.1.4 indisponibles alors que l’identité v1 restait lisible.

**Décision.** `schema_version=1` et `command_contract_version=1` sont les autorités de compatibilité. `contract_module_version` reste obligatoire, borné et validé au format sémantique `x.y.z`, mais sert uniquement d’information de provenance. Une version future du module est donc acceptée si elle conserve réellement le schéma et le contrat de commandes v1.

**Confinement.** Cette règle ne relâche ni les objets fermés, ni les propriétés autorisées, ni les types, bornes, sessions, cartes, fraîcheurs, capacités booléennes ou listes blanches. Toute évolution structurelle exige une nouvelle version de schéma ou de contrat et une adaptation explicite du Control Center.

**Présentation.** Le bandeau Serveur est dérivé de la présence réelle de l’infrastructure RCON configurée. Il ne doit plus annoncer statiquement une absence de transport lorsque des commandes réelles fermées sont disponibles. Les fonctions restant simulées sont toujours nommées explicitement.

## ADR-108 — L’accent visuel est isolé par profil serveur

**Décision.** Chaque profil choisit une clé dans une palette fermée de six accents. La clé est persistée dans son `operator-settings.json` local et la palette de l’onglet actif est appliquée via les ressources WPF dynamiques `AccentBrush`, `AccentBrightBrush` et `AccentSoftBrush`.

**Sémantique.** Les ressources `Success`, `Warning` et `Danger` ne sont jamais recolorées par ce mécanisme. Un thème utilisateur ne doit pas transformer une erreur en accent décoratif ni rendre ambigu un état de santé.

**Isolation.** L’action `ENREGISTRER L’APPARENCE` recharge la configuration enregistrée puis ne remplace que le nom local de l’onglet et sa clé d’accent. Les champs source, activation et RCON actuellement saisis ne sont pas propagés par cette action. Une couleur inconnue revient au Bleu PinteMod à la lecture et ne peut pas être enregistrée.

**Validation humaine.** Le changement d’accent par onglet et sa persistance ont été validés sans demande de correction supplémentaire.

## ADR-109 — Le hostname coloré conserve la chaîne BOIII autoritaire

**Décision.** L’éditeur conserve `RequestedHostname` sous la forme contractuelle existante contenant les codes `^0` à `^9`. La palette insère uniquement ces couples fermés au curseur. Lorsqu’une sélection est colorée, l’éditeur restaure ensuite la couleur active avant la sélection. L’aperçu est une interprétation visuelle indicative et ne devient jamais la valeur autoritaire.

**Sécurité.** La validation existante du hostname et la limite de 64 caractères bruts restent appliquées avant transport. L’éditeur refuse une insertion qui dépasserait cette limite. Aucun contenu riche, couleur arbitraire, commande libre ou nouveau chemin RCON n’est introduit.

**Validation humaine.** La palette, la coloration partielle et l’aperçu direct ont été validés. L’aperçu reste explicitement indicatif ; la chaîne BOIII encodée demeure l’autorité.

## ADR-110 — Les surfaces publiques décrivent uniquement la version stable

**Décision.** Une fois les validations fonctionnelles et terrain terminées, les README racine, le README applicatif et `LISEZ-MOI.txt` annoncent `v2.2.0` stable et la baseline actuelle de 460 tests. Les mentions de candidate RC2 ou de promotion encore en attente sont réservées à l’historique de développement et ne figurent plus dans les documents distribués comme état courant.

**Neutralisation.** Les fixtures utilisent des plages réservées à la documentation et les preuves historiques remplacent les chemins utilisateur, copies serveur, sauvegardes et partages LAN réels par des marqueurs génériques. Cette neutralisation ne modifie aucune règle de validation réseau ni aucun comportement produit.

## ADR-111 — La v2.2.0 est validée, la publication reste un acte explicite

**Décision.** La contre-revue finale autorise la publication du paquet `25e0e16` sans correction supplémentaire. Le code, les tests, la validation terrain et le packaging sont clôturés pour v2.2.0.

**Publication.** L’autorisation de revue ne déclenche pas automatiquement une mutation GitHub. La branche, le tag et la release ne sont créés ou modifiés qu’après un ordre explicite de l’opérateur. La remarque facultative Restart Map/Boss pourra être traitée séparément sans bloquer cette version.

## ADR-112 — La release stable pointe sur le commit applicatif audité

**Décision.** Le tag public `v2.2.0` cible exactement `25e0e16b6883d77ea1e0ad91caa866aa78d25173`, révision embarquée dans le binaire et autorisée par la revue finale. Les commits ultérieurs sur `main` ne modifient que les README, les preuves de revue et le suivi de publication.

**Assets.** La release publique contient uniquement `PinteMod-ControlCenter-v2.2.0-win-x64.zip` et son fichier `.sha256`. Les paquets de revue, anciennes RC, configurations opérateur, données runtime et preuves internes ne sont pas joints à la stable.

**Traçabilité.** Le digest déclaré par GitHub pour le ZIP doit être identique au SHA-256 local validé `B3C0368DD662C2C04B41F04ECA4D9FBC19A19CE998C86CD5923B6EE793A080A0`. Toute reconstruction future exige une nouvelle version ou une décision explicite ; le binaire stable publié n’est pas remplacé silencieusement.

## ADR-113 — PinteMod présente le Control Center sans coupler leurs releases

**Décision.** Les README anglais et français du dépôt public PinteMod présentent le Control Center comme application compagnon officielle et pointent vers sa release stable. Cette mise en avant est strictement documentaire : elle ne modifie aucun GSC, outil serveur, contrat ou contenu de la release PinteMod.

**Compatibilité.** La documentation précise que les actions réelles du Control Center ne deviennent disponibles que lorsque le runtime PinteMod installé publie des capabilities fraîches et compatibles. Une fonction absente ou non vérifiable reste désactivée ou simulée ; la promotion croisée ne devient pas une promesse artificielle de compatibilité.
