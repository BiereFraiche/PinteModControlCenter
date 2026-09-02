using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class PlayerChatViewModel : PageViewModel
{
    private readonly IControlCenterSnapshotStore _snapshotStore;
    private readonly IPlayerChatLogReader? _chatReader;
    private readonly IActiveBanReader? _activeBanReader;
    private readonly IOperatorActivityStore? _operatorActivityStore;
    private readonly IPlayerChatHistoryStore _historyStore;
    private PlayerChatReadResult _lastRead = PlayerChatReadResult.Empty(LocalSourceMetadata.Simulation());
    private bool _historyLoaded;
    private bool _isHybridLocal;
    private string _statusMessage = "Historique local prêt.";
    private string _activeBanStatus = "Bans actifs non chargés.";

    public PlayerChatViewModel(
        IControlCenterSnapshotStore snapshotStore,
        IPlayerChatHistoryStore historyStore,
        IPlayerChatLogReader? chatReader = null,
        IActiveBanReader? activeBanReader = null,
        IOperatorActivityStore? operatorActivityStore = null)
        : base("Chat joueurs", "Messages, connexions et déconnexions observés · historique local par serveur")
    {
        _snapshotStore = snapshotStore;
        _historyStore = historyStore;
        _chatReader = chatReader;
        _activeBanReader = activeBanReader;
        _operatorActivityStore = operatorActivityStore;
        ClearChatCommand = new AsyncRelayCommand(
            ClearChatAsync,
            () => HasMessages,
            ReportError);
    }

    public ObservableCollection<PlayerChatItemViewModel> Messages { get; } = [];

    public ObservableCollection<PlayerChatItemViewModel> RecentMessages { get; } = [];

    public ObservableCollection<ActiveBanItemViewModel> ActiveBans { get; } = [];

    public AsyncRelayCommand ClearChatCommand { get; }

    public bool HasMessages => Messages.Count > 0;

    public bool HasRecentMessages => RecentMessages.Count > 0;

    public bool HasActiveBans => ActiveBans.Count > 0;

    public string ActiveBanStatus
    {
        get => _activeBanStatus;
        private set => SetProperty(ref _activeBanStatus, value);
    }

    public int MessageCount => Messages.Count;

    public string MessageCountLabel => $"{MessageCount} / {PlayerChatHistoryPolicy.MaximumMessages} messages conservés";

    public string CaptureStateLabel => _isHybridLocal && _chatReader is not null
        ? "CAPTURE LOCALE ACTIVE"
        : "CAPTURE INACTIVE";

    public string SourceSummary => _isHybridLocal
        ? $"Lecture {DisplayText.ReadStatus(_lastRead.Source.ReadStatus)} · " +
          $"{_lastRead.LinesIgnored} ignorée(s) · {_lastRead.MalformedLines} malformée(s) · historique persistant par profil"
        : "Historique local consultable · aucune lecture serveur en mode simulation";

    public string SourceLabel => _isHybridLocal
        ? "logs/sessions/<session-active>/chat/session.log"
        : "Stockage local Control Center uniquement";

    public string FreshnessSummary => _isHybridLocal
        ? $"Fraîcheur {DisplayText.Freshness(_lastRead.Source.Freshness)} · âge {DisplayText.FormatAge(_lastRead.Source.Age)}"
        : "Aucune source serveur lue";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearError();
        var snapshot = await _snapshotStore.GetSnapshotAsync(cancellationToken);
        _isHybridLocal = snapshot.DataContext.Mode == ControlCenterDataMode.HybridLocal;

        if (!_historyLoaded)
        {
            var stored = await _historyStore.LoadAsync(cancellationToken);
            ReplaceMessages(stored);
            _historyLoaded = true;
        }

        var collected = new List<PlayerChatMessage>();
        if (_isHybridLocal && _chatReader is not null)
        {
            _lastRead = await _chatReader.ReadAsync(
                snapshot.Server.SessionId,
                snapshot.Server.MapCode,
                cancellationToken);
            collected.AddRange(_lastRead.Messages);
        }
        else
        {
            _lastRead = PlayerChatReadResult.Empty(LocalSourceMetadata.Simulation());
        }

        if (_isHybridLocal)
        {
            collected.AddRange(CreateConnectionMessages(snapshot));
            collected.AddRange(CreateOperatorModerationMessages(snapshot));
            await RefreshActiveBansAsync(cancellationToken);
        }
        else
        {
            ActiveBans.Clear();
            ActiveBanStatus = "Liste des bans disponible lorsque le serveur local est sélectionné.";
        }

        if (collected.Count > 0)
        {
            var merged = await _historyStore.MergeAsync(collected, cancellationToken);
            ReplaceMessages(merged);
            StatusMessage = collected.Count == 1
                ? "1 nouvelle activité joueur capturée."
                : $"{collected.Count} nouvelles activités joueurs capturées.";
        }

        NotifyState();
    }

    private async Task ClearChatAsync()
    {
        await _historyStore.ClearAsync();
        Messages.Clear();
        RecentMessages.Clear();
        StatusMessage = "Historique chat local effacé · aucun log serveur n’a été modifié.";
        NotifyCollectionState();
    }

    private async Task RefreshActiveBansAsync(CancellationToken cancellationToken)
    {
        ActiveBans.Clear();
        if (_activeBanReader is null)
        {
            ActiveBanStatus = "Liste des bans non disponible pour ce profil.";
            return;
        }

        var result = await _activeBanReader.ReadAsync(cancellationToken);
        if (result.Value is { } snapshot && result.Metadata.ReadStatus == LocalReadStatus.Success)
        {
            foreach (var ban in snapshot.Bans)
            {
                ActiveBans.Add(new ActiveBanItemViewModel(ban));
            }

            ActiveBanStatus = snapshot.Bans.Count == 0
                ? "Aucun ban actif."
                : $"{snapshot.Bans.Count} ban(s) actif(s) · lecture locale.";
        }
        else
        {
            ActiveBanStatus = result.Metadata.ReadStatus == LocalReadStatus.Missing
                ? "Aucun fichier de bans actif pour le moment."
                : result.Metadata.Message;
        }

        OnPropertyChanged(nameof(HasActiveBans));
    }

    private void ReplaceMessages(IReadOnlyList<PlayerChatMessage> messages)
    {
        Messages.Clear();
        string? previousMapCode = null;
        foreach (var message in messages)
        {
            Messages.Add(new PlayerChatItemViewModel(
                message,
                previousMapCode is null || !string.Equals(
                    previousMapCode,
                    message.MapCode,
                    StringComparison.OrdinalIgnoreCase)));
            previousMapCode = message.MapCode;
        }

        ReplaceRecentMessages();
        NotifyCollectionState();
    }

    private static IReadOnlyList<PlayerChatMessage> CreateConnectionMessages(DashboardSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Server.SessionId) ||
            string.IsNullOrWhiteSpace(snapshot.Server.MapCode))
        {
            return [];
        }

        var mapLabel = OfficialMapCatalog.ResolveName(snapshot.Server.MapCode);
        return snapshot.Events
            .Where(item => string.Equals(item.Category, "JOUEURS", StringComparison.OrdinalIgnoreCase) &&
                           (string.Equals(item.Title, "Joueur connecté", StringComparison.Ordinal) ||
                            string.Equals(item.Title, "Joueur déconnecté", StringComparison.Ordinal)))
            .Select(item =>
            {
                var displayName = ExtractPlayerName(item.Details);
                var text = string.Equals(item.Title, "Joueur connecté", StringComparison.Ordinal)
                    ? "a rejoint le serveur."
                    : "a quitté le serveur.";
                var elapsed = item.SessionElapsed ?? snapshot.Server.SessionDuration;
                var afterEvent = snapshot.Server.SessionDuration - elapsed;
                var occurredAt = snapshot.Server.UpdatedAtUtc - (afterEvent > TimeSpan.Zero ? afterEvent : TimeSpan.Zero);
                var identity = $"{snapshot.Server.SessionId}\n{elapsed.Ticks}\n{item.Title}\n{displayName}";
                var eventId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32].ToLowerInvariant();
                return new PlayerChatMessage(
                    eventId,
                    occurredAt,
                    displayName,
                    text,
                    snapshot.Server.MapCode,
                    mapLabel);
            })
            .ToArray();
    }

    private IReadOnlyList<PlayerChatMessage> CreateOperatorModerationMessages(DashboardSnapshot snapshot)
    {
        if (_operatorActivityStore is null ||
            string.IsNullOrWhiteSpace(snapshot.Server.SessionId) ||
            string.IsNullOrWhiteSpace(snapshot.Server.MapCode))
        {
            return [];
        }

        var mapLabel = OfficialMapCatalog.ResolveName(snapshot.Server.MapCode);
        return _operatorActivityStore.GetSnapshot()
            .Where(item => string.Equals(item.Category, "RCON", StringComparison.Ordinal) &&
                           (string.Equals(item.Title, "Administration joueur · Kick", StringComparison.Ordinal) ||
                            string.Equals(item.Title, "Administration joueur · Ban", StringComparison.Ordinal)))
            .Select(item =>
            {
                var action = item.Title.EndsWith("Kick", StringComparison.Ordinal) ? "Kick" : "Ban";
                var identity = $"{snapshot.Server.SessionId}\n{item.OccurredAt:O}\n{action}";
                var eventId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32].ToLowerInvariant();
                return new PlayerChatMessage(
                    eventId,
                    item.OccurredAt,
                    "Control Center",
                    $"{action} demandé depuis le Control Center · vérification requise dans la partie.",
                    snapshot.Server.MapCode,
                    mapLabel);
            })
            .ToArray();
    }

    private static string ExtractPlayerName(string details)
    {
        const string prefix = "Joueur : ";
        var player = details
            .Split(" · ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(part => part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(player) ? "Joueur" : player[prefix.Length..].Trim();
    }

    private void ReplaceRecentMessages()
    {
        RecentMessages.Clear();
        foreach (var item in Messages.TakeLast(8))
        {
            RecentMessages.Add(item);
        }
    }

    private void NotifyCollectionState()
    {
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(HasRecentMessages));
        OnPropertyChanged(nameof(MessageCount));
        OnPropertyChanged(nameof(MessageCountLabel));
        OnPropertyChanged(nameof(HasActiveBans));
        ClearChatCommand.NotifyCanExecuteChanged();
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(CaptureStateLabel));
        OnPropertyChanged(nameof(SourceSummary));
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(FreshnessSummary));
        NotifyCollectionState();
    }
}

public sealed class ActiveBanItemViewModel(ActiveBan ban)
{
    public string DisplayName { get; } = ban.DisplayName;

    public string Duration { get; } = ban.Duration;

    public string ExpiresLabel { get; } = ban.ExpiresLabel;

    public string Reason { get; } = ban.Reason;
}

public sealed class PlayerChatItemViewModel(PlayerChatMessage message, bool showMapSeparator)
{
    public string Time { get; } = message.OccurredAtUtc.ToLocalTime().ToString("HH:mm:ss");

    public string DisplayName { get; } = message.DisplayName;

    public string Message { get; } = message.Message;

    public string MapLabel { get; } = message.MapLabel;

    public string MapSeparatorText => $"──────── {MapLabel} ────────";

    public bool ShowMapSeparator { get; } = showMapSeparator;
}
