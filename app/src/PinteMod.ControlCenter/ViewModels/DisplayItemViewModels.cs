using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;
using PinteMod.ControlCenter.Core.Simulation;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class PlayerItemViewModel
{
    public PlayerItemViewModel(PlayerState player)
    {
        ClientNumber = player.ClientNumber;
        ClientLabel = $"CLIENT {player.ClientNumber}";
        ShortXuid = XuidValidator.Abbreviate(player.Xuid);
        DisplayName = player.DisplayName;
        Initials = string.Concat(
            player.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(part => char.ToUpperInvariant(part[0])));
        Role = player.Role switch
        {
            "owner" => "Owner",
            "admin" => "Administrateur",
            "moderator" => "Modérateur",
            "helper" => "Helper",
            "user" => "Utilisateur",
            _ => "Inconnu"
        };
        Language = string.Equals(player.Language, "unknown", StringComparison.OrdinalIgnoreCase)
            ? "INCONNUE"
            : player.Language.ToUpperInvariant();
        CountryCode = player.CountryCode is "--" or "unknown"
            ? "INCONNU"
            : player.CountryCode.ToUpperInvariant();
        LifeState = player.LifeState;
        LifeStateText = player.LifeStateAvailable ? DisplayText.LifeState(player.LifeState) : "NON DISPONIBLE";
        Points = player.PointsAvailable
            ? player.Points.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))
            : "NON DISPONIBLE";
        Presence = player.Presence;
        PresenceText = player.PresenceAvailable ? DurationDisplay.Format(player.Presence) : "NON DISPONIBLE";
        Provenance = DisplayText.Provenance(player.Provenance);
        ModerationStatus = player.IsBanned
            ? "BANNI"
            : player.IsMuted
                ? "MUTE"
                : "OK";
        IsMuted = player.IsMuted;
    }

    public int ClientNumber { get; }

    public string ClientLabel { get; }

    public string ShortXuid { get; }

    public string DisplayName { get; }

    public string Initials { get; }

    public string Role { get; }

    public string Language { get; }

    public string CountryCode { get; }

    public PlayerLifeState LifeState { get; }

    public string LifeStateText { get; }

    public string Points { get; }

    public TimeSpan Presence { get; }

    public string PresenceText { get; }

    public string Provenance { get; }

    public string ModerationStatus { get; }

    public bool IsMuted { get; }
}

public sealed class ServiceItemViewModel(ServiceStatus service)
{
    public string Name => service.Name;

    public string Description => service.Description;

    public ServiceHealth Health => service.Health;

    public ServiceDeclaredState DeclaredState => service.DeclaredState;

    public LocalReadStatus ReadStatus => service.Source.ReadStatus;

    public DataFreshness Freshness => service.Source.Freshness;

    public DataProvenance Provenance => service.Source.Provenance;

    public string DeclaredStateText => DisplayText.DeclaredState(DeclaredState);

    public string ReadStatusText => DisplayText.ReadStatus(ReadStatus);

    public string FreshnessText => DisplayText.Freshness(Freshness);

    public string AgeText => DisplayText.FormatAge(service.Source.Age);

    public string ProvenanceText => DisplayText.Provenance(Provenance);

    public string SourceLabel => service.Source.SourceLabel;

    public string? CacheNotice => Provenance == DataProvenance.MemoryCache
        ? service.Source.Message
        : null;
}

internal static class DisplayText
{
    public static string LifeState(PlayerLifeState state) => state switch
    {
        PlayerLifeState.Alive => "EN VIE",
        PlayerLifeState.Downed => "À TERRE",
        PlayerLifeState.Spectator => "SPECTATEUR",
        _ => "INCONNU"
    };

    public static string DeclaredState(ServiceDeclaredState state) => state switch
    {
        ServiceDeclaredState.Running => "En cours",
        ServiceDeclaredState.Monitoring => "Supervision",
        ServiceDeclaredState.Connected => "Connecté",
        ServiceDeclaredState.Active => "Actif",
        ServiceDeclaredState.Paused => "En pause",
        ServiceDeclaredState.Configured => "Configuré",
        ServiceDeclaredState.Stopped => "Arrêté",
        ServiceDeclaredState.Error => "Erreur",
        _ => "Inconnu"
    };

    public static string ReadStatus(LocalReadStatus status) => status switch
    {
        LocalReadStatus.NotAttempted => "Non tentée",
        LocalReadStatus.Success => "Réussie",
        LocalReadStatus.Missing => "Fichier absent",
        LocalReadStatus.Empty => "Fichier vide",
        LocalReadStatus.Invalid => "Invalide",
        LocalReadStatus.UnsupportedSchema => "Schéma incompatible",
        LocalReadStatus.AccessDenied => "Accès refusé",
        LocalReadStatus.IoError => "Erreur de lecture",
        _ => "Inconnue"
    };

    public static string Freshness(DataFreshness freshness) => freshness switch
    {
        DataFreshness.Fresh => "Fraîche",
        DataFreshness.Stale => "Retardée",
        DataFreshness.Expired => "Expirée",
        _ => "Inconnue"
    };

    public static string Provenance(DataProvenance provenance) => provenance switch
    {
        DataProvenance.LocalFile => "Fichier local",
        DataProvenance.MemoryCache => "Mémoire — dernière valeur valide",
        DataProvenance.Simulation => "Simulation",
        _ => "Aucune source dédiée"
    };

    public static string FormatAge(TimeSpan? age)
    {
        if (age is null)
        {
            return "—";
        }

        if (age.Value.TotalHours >= 1)
        {
            return $"{(int)age.Value.TotalHours} h {age.Value.Minutes:D2} min";
        }

        if (age.Value.TotalMinutes >= 1)
        {
            return $"{(int)age.Value.TotalMinutes} min {age.Value.Seconds:D2} s";
        }

        return $"{Math.Max(0, (int)age.Value.TotalSeconds)} s";
    }
}

public sealed class EventItemViewModel(LiveEvent item)
{
    public string Time => item.SessionElapsed is null
        ? item.OccurredAt.ToLocalTime().ToString("HH:mm:ss")
        : $"T+{DurationDisplay.Format(item.SessionElapsed.Value)}";

    public string Category => item.Category;

    public string Title => item.Title;

    public string Details => item.Details;

    public EventSeverity Severity => item.Severity;

    public string SourceLabel => item.SourceLabel;
}

public sealed class InstallationCheckItemViewModel(InstallationVerificationCheck item)
{
    public string Name { get; } = item.Name;

    public string Status { get; } = item.Status;

    public string Recommendation { get; } = string.IsNullOrWhiteSpace(item.Recommendation)
        ? "Aucune recommandation."
        : item.Recommendation;

    public ServiceHealth Health { get; } = item.Status switch
    {
        "PASS" => ServiceHealth.Healthy,
        "WARNING" => ServiceHealth.Warning,
        "ERROR" => ServiceHealth.Error,
        _ => ServiceHealth.Unknown
    };
}

public sealed class RecordItemViewModel
{
    public RecordItemViewModel(RecordEntry record)
    {
        MapCode = record.MapCode;
        MapName = record.MapName;
        Category = record.IsEasterEgg
            ? $"EASTER EGG · {record.PlayerCount} JOUEUR{(record.PlayerCount > 1 ? "S" : string.Empty)}"
            : $"{record.PlayerCount} JOUEUR{(record.PlayerCount > 1 ? "S" : string.Empty)}";
        Result = record.IsEasterEgg ? "QUÊTE TERMINÉE" : $"MANCHE {record.Round}";
        Duration = DurationDisplay.Format(record.Duration);
        Holder = record.Holder;
        HolderXuids = record.HolderXuids.Count == 0
            ? "XUID non fourni — simulation"
            : string.Join(" + ", record.HolderXuids.Select(XuidValidator.Abbreviate));
        Position = record.IsEasterEgg && record.Position > 0
            ? $"TOP #{record.Position}"
            : record.IsEasterEgg
                ? "QUÊTE"
            : record.Position > 0
                ? $"TOP #{record.Position}"
                : "CLASSEMENT SIMULÉ";
        Provenance = DisplayText.Provenance(record.Provenance);
        IsEasterEgg = record.IsEasterEgg;
    }

    public string MapCode { get; }

    public string MapName { get; }

    public string Category { get; }

    public string Result { get; }

    public string Duration { get; }

    public string Holder { get; }

    public string HolderXuids { get; }

    public string Position { get; }

    public string Provenance { get; }

    public bool IsEasterEgg { get; }
}

public sealed class RankProfileItemViewModel
{
    public RankProfileItemViewModel(RankProfile profile)
    {
        DisplayName = profile.DisplayName;
        ShortXuid = XuidValidator.Abbreviate(profile.Xuid);
        Sessions = profile.Sessions;
        BestOverallRound = profile.BestOverallRound;
        TotalPlayTime = DurationDisplay.Format(profile.TotalPlayTime);
    }

    public string DisplayName { get; }

    public string ShortXuid { get; }

    public int Sessions { get; }

    public int BestOverallRound { get; }

    public string TotalPlayTime { get; }
}

public sealed record SelectionOption(string Key, string Label)
{
    public override string ToString() => Label;
}

public sealed class MapCatalogItemViewModel(MapCatalogEntry entry)
{
    public string Code { get; } = entry.Code;

    public string DisplayName { get; } = entry.DisplayName;

    public bool IsManual { get; } = entry.IsManual;

    public string Sources { get; } = string.Join(" · ", new[]
    {
        entry.IsOfficial ? "OFFICIELLE" : null,
        entry.IsInServerRotation ? "ROTATION" : null,
        entry.IsManual ? "MANUELLE" : null,
        entry.IsObserved ? "OBSERVÉE" : null
    }.Where(source => source is not null));

    public string DisplayLabel => $"{DisplayName} · {Code} · {Sources}";
}

public sealed class FilterOptionViewModel(string key) : ObservableObject
{
    private bool _isSelected;

    public string Key { get; } = key;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class SimulationResultItemViewModel
{
    public SimulationResultItemViewModel(SimulationResult result, string targetDisplay)
    {
        Action = result.Action switch
        {
            SimulationAction.RevivePlayer => "Revive",
            SimulationAction.RespawnPlayer => "Respawn",
            SimulationAction.GrantPoints => "Points",
            SimulationAction.RefillAmmo => "Munitions",
            SimulationAction.GiveWeapon => "Arme",
            SimulationAction.GivePerk => "Atout",
            SimulationAction.GiveAllPerks => "Tous les atouts",
            SimulationAction.GivePowerUpPlayer => "Power-up",
            SimulationAction.TeleportPlayer => "Téléportation",
            SimulationAction.ToggleGodmode => "Godmode",
            SimulationAction.MutePlayer => "Mute",
            SimulationAction.UnmutePlayer => "Unmute",
            SimulationAction.KickPlayer => "Kick",
            SimulationAction.BanPlayer => "Ban",
            SimulationAction.ChangeRole => "Rôle",
            SimulationAction.RemoveRole => "Retrait du rôle",
            SimulationAction.ViewHistory => "Historique",
            SimulationAction.ChangeMap => "Changer la carte",
            SimulationAction.RestartMap => "Redémarrer la carte",
            SimulationAction.SetRound => "Définir la manche",
            SimulationAction.TogglePower => "Courant",
            SimulationAction.EnablePackAPunch => "Pack-a-Punch",
            SimulationAction.PlayMusic => "Musique",
            SimulationAction.TriggerEvent => "Événement",
            SimulationAction.SpawnBoss => "Boss",
            SimulationAction.SpawnPowerUp => "Power-up",
            SimulationAction.RunDiagnostics => "Diagnostics",
            _ => result.Action.ToString()
        };
        TargetDisplay = targetDisplay;
        ShortXuid = result.TargetXuid is null
            ? "—"
            : XuidValidator.Abbreviate(result.TargetXuid);
        Option = string.IsNullOrWhiteSpace(result.OptionKey) ? "—" : result.OptionKey;
        Time = result.CompletedAtUtc.ToLocalTime().ToString("HH:mm:ss");
        Status = result.Status.ToString();
        CommandSent = result.CommandSent.ToString().ToLowerInvariant();
        StatusValue = result.Status;
    }

    public string Action { get; }

    public string TargetDisplay { get; }

    public string ShortXuid { get; }

    public string Option { get; }

    public string Time { get; }

    public string Status { get; }

    public string CommandSent { get; }

    public SimulationStatus StatusValue { get; }
}
