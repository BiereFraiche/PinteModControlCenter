# PinteMod Control Center

PinteMod Control Center is the simple Windows app for creating, starting and managing a **Call of Duty: Black Ops III Zombies** server with [PinteMod](https://github.com/BiereFraiche/PinteMod).

It is made for players who want a working server without having to edit configuration files by hand.

[![Version](https://img.shields.io/badge/stable-v2.4.5-168BFF)](https://github.com/BiereFraiche/PinteModControlCenter/releases/tag/v2.4.5)
![Windows](https://img.shields.io/badge/platform-Windows-0078D4)
![Tests](https://img.shields.io/badge/tests-629%20passing-24C875)

**[Download PinteMod Control Center v2.4.5](https://github.com/BiereFraiche/PinteModControlCenter/releases/download/v2.4.5/PinteMod.ControlCenter.exe)**

[French documentation](README_FR.md) · [Release notes](docs/RELEASE_NOTES_v2.4.5.md)

![PinteMod Control Center interface](design/pintemod-control-center-reference.png)

## What is it for?

The Control Center brings the important server tasks into one clear interface:

- install or repair PinteMod on a compatible BOIII/Ezz server folder;
- set up the first RCON password safely, or replace it when needed;
- start and stop each configured server from the same place;
- see the map, round, player count and PinteMod service health;
- access player information, records, logs and local chat history;
- manage several servers through separate tabs;
- use guided server and moderation actions only when they are supported and confirmed.

It stays local-first: no account, no cloud service and no new incoming network port.

## Start a server — step by step

You only need Windows, a BOIII/Ezz server folder and the Control Center.

### 1. Get a blank server base

If you need a ready-to-configure blank BOIII/Ezz server base, the community download is here:

**[Download the blank BOIII/Ezz server base from Mega](https://mega.nz/folder/MYsTGa6I#gJviuei7G6XuFicNy_L9BQ)**

Extract it somewhere simple, for example `E:\Games\BOIII\MyServer`. Use and obtain BOIII/Ezz only according to the applicable rights and licences.

### 2. Open the Control Center

Download the EXE above, place it wherever you like and launch it. No installation is required.

### 3. Add your server

Click **+ Server**, then select the server folder: it is the folder that directly contains `boiii` and your launcher, usually `Server.bat`.

Click **Analyse**. The Control Center tells you whether PinteMod is already installed or whether it can prepare the server.

### 4. Install PinteMod

For a blank server, click **Install PinteMod** and let the operation finish. Existing player data and third-party scripts are not overwritten by the normal repair flow.

### 5. Set your first RCON password

Before the first launch, the Control Center can ask for an RCON password. Choose one, keep it private, then confirm: the application writes the required server configuration for you.

You may also start without RCON, but the health check and administration controls will be unavailable until you configure it in **Settings**.

### 6. Start and check the server

Click **Start server**. Once BOIII is running, open the dashboard and use **PinteMod health check**. A healthy installation reports PinteMod, Supervisor, Ban Service and GeoIP Bridge as connected.

That is it: your server is ready to play.

## Why use it?

PinteMod Control Center is designed to make a multi-feature server approachable:

- a guided first setup instead of manual configuration edits;
- a dashboard that shows whether the server is actually healthy;
- multiple servers in one application, without mixing their settings;
- tools for players, records, chat, maps and logs in one place;
- safe-by-default controls: sensitive operations require a deliberate action and confirmation;
- optional LAN data access for a separate operator PC, without exposing a control port to the Internet.

## Common questions

### Does it include BOIII or Black Ops III?

No. The Control Center is an independent management application. It does not include the game, BOIII/Ezz executables, maps or proprietary game assets.

### Does it require administrator permissions?

No. If Windows asks for elevation when the server starts, that request comes from `boiii.exe` or its Windows compatibility settings. See the [BOIII elevation note](docs/SERVEUR_BOIII_DEMARRAGE_FR.md#préparer-une-base-saine).

### Can I use it on a second PC or VM?

Yes. The recommended approach is an existing secured hypervisor console or RDP/VPN setup. The Control Center itself does not expose a web server or an incoming remote-control port. See the [French VM guide](docs/DEPLOIEMENT_VM_FR.md).

### What is RCON?

RCON is the private password that lets the Control Center request supported BOIII checks and actions. Never share it. If you need to change it, use **Settings → Replace server RCON** while the server is stopped.

## Technical information

The current stable release is **v2.4.5**. The blank-server workflow has been field validated: PinteMod installation, first RCON or confirmed replacement, BOIII startup and health check.

| Check | Status |
|---|---|
| Debug build | PASS — 0 warnings, 0 errors |
| Release build | PASS — 0 warnings, 0 errors |
| Automated tests | PASS — 629/629 in both configurations |
| Windows packages | Standalone EXE, portable folder, ZIP files, SHA-256 and offline self-test |

The optional Remote Agent is never required for a local server. If it was explicitly enabled for LAN use, it can be fully disabled from the Manager.

### Useful guides

- [Blank server guide (French)](docs/SERVEUR_BOIII_DEMARRAGE_FR.md)
- [Public test guide (French)](docs/RECETTE_FINALE_FR.md)
- [VM deployment guide (French)](docs/DEPLOIEMENT_VM_FR.md)
- [PinteMod project](https://github.com/BiereFraiche/PinteMod)
- [Security policy](SECURITY.md)

Created and maintained by **BiereFraiche**, with development assistance from Codex and ChatGPT.
