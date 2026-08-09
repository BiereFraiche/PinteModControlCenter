using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Simulation;

public enum SimulationScenario
{
    Healthy,
    Warning,
    Offline,
    ServerStopped,
    Empty
}

public sealed class SimulatedControlCenterDataProvider(
    SimulationScenario scenario = SimulationScenario.Healthy) : IControlCenterDataProvider
{
    private static readonly DateTimeOffset SnapshotTime =
        new(2026, 8, 2, 16, 22, 18, TimeSpan.Zero);

    public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateSnapshot(scenario));
    }

    public static DashboardSnapshot CreateSnapshot(SimulationScenario selectedScenario)
    {
        IReadOnlyList<PlayerState> players = selectedScenario is SimulationScenario.Empty or SimulationScenario.ServerStopped
            ? Array.Empty<PlayerState>()
            : CreatePlayers();

        var server = new ServerState(
            "2.1.1",
            selectedScenario != SimulationScenario.ServerStopped,
            "zm_tomb",
            "Origins",
            selectedScenario == SimulationScenario.ServerStopped ? 0 : 12,
            players.Count,
            18,
            selectedScenario is SimulationScenario.Healthy or SimulationScenario.Empty
                ? RankedStatus.Ranked
                : RankedStatus.Unranked,
            selectedScenario == SimulationScenario.ServerStopped ? TimeSpan.Zero : TimeSpan.FromSeconds(2_538),
            SnapshotTime);

        IReadOnlyList<RecordEntry> records = selectedScenario == SimulationScenario.Empty
            ? Array.Empty<RecordEntry>()
            : CreateRecords();
        IReadOnlyList<LiveEvent> events = selectedScenario == SimulationScenario.Empty
            ? Array.Empty<LiveEvent>()
            : CreateEvents(selectedScenario);

        return new DashboardSnapshot(
            server,
            CreateServices(selectedScenario),
            players,
            events,
            records)
        {
            RankRecords = CreateRankRecordsSnapshot(players, records)
        };
    }

    private static IReadOnlyList<PlayerState> CreatePlayers() =>
    [
        new(0, "9cf34426f668fb8b", "BiereFraiche", "owner", "fr", "LOCAL",
            PlayerLifeState.Alive, 12_500, TimeSpan.FromSeconds(2_538), false, false),
        new(1, "1111111111111111", "Léonie", "admin", "fr", "FR",
            PlayerLifeState.Alive, 8_900, TimeSpan.FromSeconds(1_862), false, false),
        new(2, "2222222222222222", "Mason", "user", "en", "GB",
            PlayerLifeState.Downed, 3_200, TimeSpan.FromSeconds(932), false, false),
        new(3, "3333333333333333", "Nox", "helper", "es", "ES",
            PlayerLifeState.Spectator, 1_250, TimeSpan.FromSeconds(530), true, false)
    ];

    private static IReadOnlyList<ServiceStatus> CreateServices(SimulationScenario selectedScenario)
    {
        var supervisor = selectedScenario == SimulationScenario.Warning
            ? ServiceHealth.Warning
            : selectedScenario == SimulationScenario.ServerStopped
                ? ServiceHealth.Unknown
                : ServiceHealth.Healthy;
        var banService = selectedScenario == SimulationScenario.Offline
            ? ServiceHealth.Offline
            : selectedScenario == SimulationScenario.ServerStopped
                ? ServiceHealth.Unknown
                : ServiceHealth.Healthy;
        var geoIp = selectedScenario == SimulationScenario.Offline
            ? ServiceHealth.Error
            : selectedScenario == SimulationScenario.ServerStopped
                ? ServiceHealth.Unknown
                : ServiceHealth.Healthy;

        return
        [
            new("PinteMod", selectedScenario == SimulationScenario.ServerStopped ? "État non disponible" : "28 modules chargés",
                selectedScenario == SimulationScenario.ServerStopped ? ServiceHealth.Unknown : ServiceHealth.Healthy, SnapshotTime)
            {
                DeclaredState = selectedScenario == SimulationScenario.ServerStopped
                    ? ServiceDeclaredState.Unknown
                    : ServiceDeclaredState.Running
            },
            new("Supervisor", supervisor == ServiceHealth.Warning ? "Heartbeat retardé" : "Supervision locale",
                supervisor, SnapshotTime.AddSeconds(-2))
            {
                DeclaredState = supervisor == ServiceHealth.Warning
                    ? ServiceDeclaredState.Paused
                    : selectedScenario == SimulationScenario.ServerStopped
                        ? ServiceDeclaredState.Unknown
                        : ServiceDeclaredState.Monitoring
            },
            new("Ban Service", banService == ServiceHealth.Offline ? "Heartbeat absent" : "Heartbeat reçu",
                banService, SnapshotTime.AddSeconds(-4))
            {
                DeclaredState = banService == ServiceHealth.Offline
                    ? ServiceDeclaredState.Stopped
                    : selectedScenario == SimulationScenario.ServerStopped
                        ? ServiceDeclaredState.Unknown
                        : ServiceDeclaredState.Running
            },
            new("GeoIP Bridge", geoIp == ServiceHealth.Error ? "Erreur simulée" : "Bridge local connecté",
                geoIp, SnapshotTime.AddSeconds(-5))
            {
                DeclaredState = geoIp == ServiceHealth.Error
                    ? ServiceDeclaredState.Error
                    : selectedScenario == SimulationScenario.ServerStopped
                        ? ServiceDeclaredState.Unknown
                        : ServiceDeclaredState.Connected
            },
            new("Live Console", selectedScenario == SimulationScenario.ServerStopped ? "Donnée inconnue" : "Lecture seule active",
                selectedScenario == SimulationScenario.ServerStopped ? ServiceHealth.Unknown : ServiceHealth.Healthy,
                SnapshotTime.AddSeconds(-3))
            {
                DeclaredState = selectedScenario == SimulationScenario.ServerStopped
                    ? ServiceDeclaredState.Unknown
                    : ServiceDeclaredState.Running
            }
        ];
    }

    private static IReadOnlyList<LiveEvent> CreateEvents(SimulationScenario selectedScenario)
    {
        var events = new List<LiveEvent>
        {
            new(SnapshotTime.AddSeconds(-8), "JOIN", "Joueur connecté",
                "Nox a rejoint la session sur le client 3.", EventSeverity.Success),
            new(SnapshotTime.AddSeconds(-19), "IDENTITÉ", "Identité attachée",
                "BOIII_XUID validé pour Mason.", EventSeverity.Information),
            new(SnapshotTime.AddSeconds(-31), "LANGUE", "Langue détectée",
                "Léonie utilise le français (FR).", EventSeverity.Information),
            new(SnapshotTime.AddMinutes(-2), "RECORD", "Candidat de record",
                "La présence de l’équipe reste éligible à 100 %.", EventSeverity.Success),
            new(SnapshotTime.AddMinutes(-4), "SYSTÈME", "Diagnostic sain",
                "ezzhealth simulé : 51 contrôles conformes.", EventSeverity.Success),
            new(SnapshotTime.AddMinutes(-6), "PRÉSENCE", "État joueur mis à jour",
                "Nox est passé en mode spectateur.", EventSeverity.Warning)
        };

        if (selectedScenario == SimulationScenario.Warning)
        {
            events.Insert(0, new LiveEvent(SnapshotTime, "SYSTÈME", "Heartbeat retardé",
                "Supervisor dépasse le délai simulé attendu.", EventSeverity.Warning));
        }
        else if (selectedScenario == SimulationScenario.Offline)
        {
            events.Insert(0, new LiveEvent(SnapshotTime, "SYSTÈME", "Service indisponible",
                "Ban Service est hors ligne et GeoIP Bridge signale une erreur simulée.", EventSeverity.Danger));
        }
        else if (selectedScenario == SimulationScenario.ServerStopped)
        {
            events.Clear();
            events.Add(new LiveEvent(SnapshotTime, "SYSTÈME", "Serveur arrêté",
                "Le snapshot simulé indique que BOIII n’est pas en cours d’exécution.", EventSeverity.Danger));
        }

        return events;
    }

    private static IReadOnlyList<RecordEntry> CreateRecords() =>
    [
        new("zm_tomb", "Origins", 4, 82, TimeSpan.FromHours(3.42), "Team Braise", false),
        new("zm_castle", "Der Eisendrache", 2, 74, TimeSpan.FromHours(2.95), "Léonie & Mason", false),
        new("zm_tomb", "Origins — Easter Egg", 4, 1, TimeSpan.FromMinutes(52.3), "Team Braise", true),
        new("zm_zod", "Shadows of Evil", 1, 61, TimeSpan.FromHours(2.1), "BiereFraiche", false)
    ];

    private static RankRecordsSnapshot CreateRankRecordsSnapshot(
        IReadOnlyList<PlayerState> players,
        IReadOnlyList<RecordEntry> records) =>
        new(
            players.Select((player, index) => new RankProfile(
                    player.Xuid,
                    player.DisplayName,
                    Math.Max(1, 18 - index * 3),
                    player.Presence + TimeSpan.FromHours(20 - index * 4),
                    Math.Max(12, 82 - index * 11)))
                .ToArray(),
            records.Where(record => !record.IsEasterEgg)
                .Select((record, index) => new RoundRecord(
                    record.MapCode,
                    record.MapName,
                    record.PlayerCount,
                    index + 1,
                    record.Round,
                    record.Duration,
                    record.Holder,
                    [],
                    $"simulation-{index + 1}"))
                .ToArray(),
            LocalSourceMetadata.Simulation("Profils Ranks simulés"),
            LocalSourceMetadata.Simulation("Records de manches simulés"),
            0,
            0,
            0,
            0,
            0);
}
