# Security

PinteMod Control Center is a local Windows application. It does not create an incoming control port, web server, account or cloud service.

## Reporting a problem

Please use GitHub private vulnerability reporting when available. Do not publish a security issue before it has been reviewed.

Never include in an issue, screenshot or archive:

- an RCON password;
- DPAPI or `*.secret` files;
- a private IP address, Windows path, SMB share or credential;
- a full player XUID;
- PinteMod runtime data, logs or a personal server archive.

Use a test copy and synthetic names when explaining a problem.

## Current protections

- RCON is configured deliberately and stored with Windows DPAPI for the current user;
- the application only exposes supported, confirmed actions rather than free-form server commands;
- player actions use BOIII XUID internally, not a nickname alone;
- local data is read only from a server folder explicitly chosen by the operator;
- uncertain server actions are never retried automatically;
- closing the Control Center never stops BOIII.

The supported public version is the latest stable release: **v2.4.5**.
