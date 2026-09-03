# PinteMod Control Center v2.4.9

## Server checks without false alerts

- Extra official PinteMod modules are now accepted by the installation check. Genuine duplicate files are still reported separately.
- `hotfix.gsc` is now correctly treated as an optional BOIII compatibility file: its absence is no longer a warning.

## Anti-AFK and moderation

- A new **Protection anti-AFK** card in **Settings** lets you enable it and choose its warning and spectator delays.
- AFK players are placed spectator without a death or equipment loss, then return with `.retour`.
- The **Chat joueurs** ban list now has a confirmed **Déban** action when RCON is configured.

## Verification

- 637 automated tests passed in Debug.
- This release is designed to be installed directly from v2.4.8 through **Vérifier GitHub → Mettre à jour**.
