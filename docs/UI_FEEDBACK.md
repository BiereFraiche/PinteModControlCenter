# Retours UI

Ce fichier est réservé aux retours graphiques transmis depuis la conversation ChatGPT. Codex ne doit pas y inventer ni y ajouter de retours de sa propre initiative.

## Revue humaine transmise le 2026-08-02

La direction graphique générale est validée. La seconde passe doit rester ciblée, sans réécriture de l’application ni changement de son architecture globale.

### Règles absolues transmises

- Aucun RCON réel, secret, accès réseau, lancement ou arrêt réel du serveur.
- Aucune écriture dans PinteMod et aucune modification GSC.
- Conserver Core / Infrastructure / WPF / Tests, `ISimulationActionService` et `CommandSent = false`.
- Le ciblage joueur reste exclusivement fondé sur BOIII_XUID ; le pseudo reste un nom d’affichage.

### Priorité critique

- Rendre l’interface réellement adaptative à 1920×1080, 1060×840 et 900×640 : sidebar compacte si nécessaire, suppression des `MinWidth` incompatibles, KPI multilignes, grilles deux colonnes verticales à petite largeur et aucun défilement horizontal global.
- Supprimer les états métier codés en dur : l’état serveur vient de `ServerRunning`, Ranked/Unranked et la carte des records viennent du snapshot/ViewModel ; distinguer le mode simulation de l’état serveur simulé.
- Désactiver clairement les options Paramètres sans effet avec « À venir », ou leur donner un comportement simulé réel et testable. Ne laisser aucune option cochée sans mécanisme associé.

### Priorité haute

- Partager la sélection joueur entre Dashboard et Joueurs via un état MVVM injecté. Conserver la sélection par XUID et l’invalider ou la remplacer explicitement si le joueur disparaît.
- Afficher après une action un résultat structuré : action, cible affichée, XUID avec accès au complet, option, heure, statut et `CommandSent = false`. Au minimum, fournir le XUID complet en info-bulle.
- Thèmer ScrollBar, CheckBox et focus ; supprimer la bordure claire supérieure, avec `WindowChrome` si nécessaire et sans bibliothèque externe.
- Utiliser le bleu pour les indicateurs de simulation. Réserver le vert aux états sains. Employer « Interface locale active » ou « Prototype actif » dans le pied de page.
- Donner un état sélectionné distinct au filtre Logs par un mécanisme MVVM et afficher un état vide sans résultat.
- Retirer « Simulation » de chaque libellé de bouton tout en conservant un badge de section « ACTIONS SIMULÉES » et le résultat `CommandSent=false`.

### Priorité moyenne

- Porter les informations secondaires importantes à 10–11 px et le contenu principal à 12 px ou plus ; réserver `TextMuted` aux informations réellement secondaires.
- Ajouter des scénarios/test data : Warning, Offline/Error, serveur arrêté, Unranked, aucun joueur, joueur disparu, aucun record et recherche Logs vide. Ajouter Error/Unknown au modèle si nécessaire.
- Sécuriser `AsyncRelayCommand` : capturer l’exception, exposer un message affichable, garantir la réactivation et éviter toute fermeture brutale ; protéger également l’initialisation de l’application.
- Préparer un snapshot partagé afin d’éviter six lectures futures identiques, sans ajouter de lecteur de fichiers.

### Tests demandés

- Page initiale et navigation vers les six pages.
- Sélection partagée et disparition du joueur sélectionné.
- Action désactivée sans joueur ; action utilisant le XUID et jamais le pseudo.
- Filtre Logs actif, filtre + recherche et résultat vide.
- Ranked/Unranked issu du snapshot et état serveur issu de `ServerRunning`.
- `CommandSent` toujours faux.
- Options Paramètres désactivées ou réellement simulées.

### Validation finale demandée

- Debug et Release : zéro warning, zéro erreur ; tests dans les deux configurations.
- Rescan sans API réseau, RCON, processus ni écriture.
- Vérification des six pages aux trois tailles demandées et six nouvelles captures.
- Au moins une capture Warning et une capture Offline/Error.
- Mise à jour de `CODEX_PROGRESS.md`, `TODO.md` et `DECISIONS.md`.
- Livrer uniquement les fichiers réellement modifiés ou un patch ZIP ciblé.
