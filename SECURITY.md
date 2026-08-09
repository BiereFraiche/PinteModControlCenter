# Security policy

## Scope

PinteMod Control Center handles local runtime observations and optional BOIII RCON administration. Reports involving command construction, target identity, path confinement, secret handling, privacy filtering or uncertain UDP delivery are security-sensitive.

## Reporting

Please report security issues privately through GitHub's private vulnerability reporting/security-advisory feature when available. Do not disclose an exploitable issue in a public issue before it has been reviewed.

Never attach or paste:

- an RCON password or command packet containing it;
- `rcon.secret.dpapi` or any DPAPI-protected file;
- operator settings containing a real server path/address;
- full BOIII XUID values;
- private IP addresses, SMB credentials or share configuration;
- PinteMod runtime directories, logs or player registries;
- a production server archive.

Use synthetic identifiers and a dedicated test copy when preparing a reproduction.

## Supported baseline

Security fixes target the latest source on `main` and the latest documented operator preview. The current development baseline is v2.2 MVP Preview 13.

## Operational guarantees

- no inbound listener or automatic network discovery;
- explicit numeric private/local RCON targets only;
- DPAPI `CurrentUser` secret storage;
- closed command allowlists and strict option validation;
- BOIII_XUID targeting for real player actions;
- confirmation before mutations;
- no automatic retry after uncertain delivery;
- local readers confined to an explicit root;
- no direct writes into PinteMod runtime data.
