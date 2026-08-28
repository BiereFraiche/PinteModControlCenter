using System.Collections.ObjectModel;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class PlayerChatViewModel : PageViewModel
{
    private readonly IControlCenterSnapshotStore _snapshotStore;
    private readonly IPlayerChatLogReader? _chatReader;
    private readonly IPlayerChatHistoryStore _historyStore;
    private PlayerChatReadResult _lastRead = PlayerChatReadResult.Empty(LocalSourceMetadata.Simulation());
    private bool _historyLoaded;
    private bool _isHybridLocal;
    private string _statusMessage = "Historique local prêt.";

    public PlayerChatViewModel(
        IControlCenterSnapshotStore snapshotStore,
        IPlayerChatHistoryStore historyStore,
        IPlayerChatLogReader? chatReader = null)
        : base("Chat joueurs", "Messages réellement écrits par les joueurs en game · historique local par serveur")
    {
        _snapshotStore = snapshotStore;
        _historyStore = historyStore;
        _chatReader = chatReader;
        ClearChatCommand = new AsyncRelayCommand(
            ClearChatAsync,
            () => HasMessages,
            ReportError);
    }

    public ObservableCollection<PlayerChatItemViewModel> Messages { get; } = [];

    public ObservableCollection<PlayerChatItemViewModel> RecentMessages { get; } = [];

    public AsyncRelayCommand ClearChatCommand { get; }

    public bool HasMessages => Messages.Count > 0;

    public bool HasRecentMessages => RecentMessages.Count > 0;

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

        if (_isHybridLocal && _chatReader is not null)
        {
            _lastRead = await _chatReader.ReadAsync(
                snapshot.Server.SessionId,
                snapshot.Server.MapCode,
                cancellationToken);
            if (_lastRead.Messages.Count > 0)
            {
                var merged = await _historyStore.MergeAsync(_lastRead.Messages, cancellationToken);
                ReplaceMessages(merged);
                StatusMessage = _lastRead.Messages.Count == 1
                    ? "1 nouveau message joueur capturé."
                    : $"{_lastRead.Messages.Count} nouveaux messages joueurs capturés.";
            }
        }
        else
        {
            _lastRead = PlayerChatReadResult.Empty(LocalSourceMetadata.Simulation());
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

public sealed class PlayerChatItemViewModel(PlayerChatMessage message, bool showMapSeparator)
{
    public string Time { get; } = message.OccurredAtUtc.ToLocalTime().ToString("HH:mm:ss");

    public string DisplayName { get; } = message.DisplayName;

    public string Message { get; } = message.Message;

    public string MapLabel { get; } = message.MapLabel;

    public string MapSeparatorText => $"──────── {MapLabel} ────────";

    public bool ShowMapSeparator { get; } = showMapSeparator;
}
