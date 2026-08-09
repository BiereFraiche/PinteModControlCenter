# Contributing

Thank you for helping improve PinteMod Control Center.

## Before changing code

Read:

1. `AGENTS.md`;
2. `docs/DECISIONS.md`;
3. `docs/TODO.md`;
4. `app/README.md`.

Preserve the separation between Core, Infrastructure, WPF presentation and tests. Domain models must remain independent from WPF.

## Security rules

- never commit a server copy, runtime data, logs, operator settings or secrets;
- never target a player only by display name;
- never turn free-form UI input into a server command;
- keep RCON actions typed and allowlisted;
- do not add automatic retries for mutations;
- do not add network discovery, an inbound port or a web server;
- do not enable a simulated action without an audited PinteMod contract and tests.

## Validation

Run both configurations before opening a pull request:

```powershell
dotnet restore .\app\PinteMod.ControlCenter.sln --configfile .\app\NuGet.Config
dotnet build .\app\PinteMod.ControlCenter.sln -c Debug --no-restore
dotnet test .\app\PinteMod.ControlCenter.sln -c Debug --no-build --no-restore
dotnet build .\app\PinteMod.ControlCenter.sln -c Release --no-restore
dotnet test .\app\PinteMod.ControlCenter.sln -c Release --no-build --no-restore
```

Document material architecture/security changes in `docs/DECISIONS.md` and update `docs/CODEX_PROGRESS.md` plus `docs/TODO.md`.
