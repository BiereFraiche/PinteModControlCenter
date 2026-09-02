# PinteMod Control Center v2.4.7

## Real-session test release

- Adds the first PinteMod anti-AFK flow: after 10 minutes without movement, view change or action, a player is moved to spectator after a two-minute warning.
- The player can return with `.retour` or `.back`. The system saves and restores score, weapons, ammunition and perks, without intentionally adding a death.
- Player chat history and the dashboard now keep join and leave events alongside chat messages, including activity recorded while the Control Center was closed.
- The **Chat joueurs** page now shows active bans and records the kick/ban requests sent from the Control Center in its local activity history.
- Removes the unnecessary automatic-update wording from the dashboard.

## What to test

- Join a real session, stay inactive until the warning and spectator transition, then use `.retour`.
- Check that your score, weapons, ammunition and perks are unchanged after returning.
- Repeat after a map change if possible, then report any difference.

## Verification

- 637 automated tests passed and the portable Windows package was rebuilt for this release.
- This feature is intentionally released for real-session validation; the game-session behaviour above is the priority test.
