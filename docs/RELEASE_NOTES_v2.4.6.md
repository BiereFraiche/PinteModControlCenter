# PinteMod Control Center v2.4.6

## Simpler live status

- Local PinteMod servers are now labelled **Serveur local** instead of the technical hybrid-mode wording.
- The dashboard uses a clear **Mise à jour automatique** label for live PinteMod data.
- The automatic refresh now tolerates the brief file replacement that can occur while a player joins or leaves. It keeps the last displayed data and retries two seconds later instead of stopping.

## More control from Settings

- Configure BOIII server port, RCON and recurring public chat tips from the Control Center.
- Public tips can be enabled, disabled, added, removed and scheduled without manually editing PinteMod files.

## Verification

- 636 automated tests passed in Debug.
- The release package includes the offline self-test and SHA-256 checksums.
