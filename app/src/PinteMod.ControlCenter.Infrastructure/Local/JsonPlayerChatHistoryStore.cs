using System.Text.Json;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class JsonPlayerChatHistoryStore : IPlayerChatHistoryStore, IDisposable
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumHistoryBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly string _historyPath;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonPlayerChatHistoryStore(string historyPath, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyPath);
        ArgumentNullException.ThrowIfNull(clock);
        _historyPath = Path.GetFullPath(historyPath);
        _clock = clock;
    }

    public async Task<IReadOnlyList<PlayerChatMessage>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await LoadDocumentCoreAsync(cancellationToken).ConfigureAwait(false)).Messages;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<PlayerChatMessage>> MergeAsync(
        IReadOnlyCollection<PlayerChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadDocumentCoreAsync(cancellationToken).ConfigureAwait(false);
            if (messages.Count == 0)
            {
                return document.Messages;
            }

            var byId = new Dictionary<string, PlayerChatMessage>(StringComparer.Ordinal);
            foreach (var message in document.Messages.Concat(messages))
            {
                var normalized = Normalize(message);
                if (normalized is null ||
                    (document.ClearedAtUtc is not null && normalized.OccurredAtUtc <= document.ClearedAtUtc.Value) ||
                    byId.ContainsKey(normalized.EventId))
                {
                    continue;
                }

                byId.Add(normalized.EventId, normalized);
            }

            var merged = byId.Values
                .OrderBy(message => message.OccurredAtUtc)
                .ThenBy(message => message.EventId, StringComparer.Ordinal)
                .TakeLast(PlayerChatHistoryPolicy.MaximumMessages)
                .ToArray();
            await SaveDocumentCoreAsync(
                new HistoryDocument(CurrentSchemaVersion, document.ClearedAtUtc, merged),
                cancellationToken).ConfigureAwait(false);
            return merged;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveDocumentCoreAsync(
                new HistoryDocument(CurrentSchemaVersion, _clock.UtcNow, []),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<HistoryDocument> LoadDocumentCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_historyPath))
        {
            return EmptyDocument();
        }

        try
        {
            var info = new FileInfo(_historyPath);
            if (info.Length is <= 0 or > MaximumHistoryBytes)
            {
                return EmptyDocument();
            }

            await using var stream = new FileStream(
                _historyPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var document = await JsonSerializer.DeserializeAsync<HistoryDocument>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            if (document is null || document.SchemaVersion != CurrentSchemaVersion || document.Messages is null)
            {
                return EmptyDocument();
            }

            var normalized = document.Messages
                .Select(Normalize)
                .Where(message => message is not null)
                .Cast<PlayerChatMessage>()
                .Where(message => document.ClearedAtUtc is null || message.OccurredAtUtc > document.ClearedAtUtc.Value)
                .GroupBy(message => message.EventId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(message => message.OccurredAtUtc)
                .ThenBy(message => message.EventId, StringComparer.Ordinal)
                .TakeLast(PlayerChatHistoryPolicy.MaximumMessages)
                .ToArray();
            return new HistoryDocument(CurrentSchemaVersion, document.ClearedAtUtc, normalized);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return EmptyDocument();
        }
    }

    private async Task SaveDocumentCoreAsync(
        HistoryDocument document,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_historyPath)
            ?? throw new InvalidOperationException("Dossier d’historique chat invalide.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _historyPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _historyPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static PlayerChatMessage? Normalize(PlayerChatMessage? message)
    {
        if (message is null ||
            message.EventId.Length != 32 ||
            !message.EventId.All(Uri.IsHexDigit) ||
            !MapCodeValidator.TryNormalize(message.MapCode, out var normalizedMapCode))
        {
            return null;
        }

        var displayName = LogPrivacyFilter.SafeChatPlayerName(message.DisplayName);
        var text = LogPrivacyFilter.SanitizeChatText(message.Message, 500);
        var mapLabel = LogPrivacyFilter.SanitizeChatText(message.MapLabel, 80);
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(mapLabel))
        {
            return null;
        }

        return new PlayerChatMessage(
            message.EventId.ToLowerInvariant(),
            message.OccurredAtUtc,
            displayName,
            text,
            normalizedMapCode,
            mapLabel);
    }

    private static HistoryDocument EmptyDocument() =>
        new(CurrentSchemaVersion, null, []);

    private sealed record HistoryDocument(
        int SchemaVersion,
        DateTimeOffset? ClearedAtUtc,
        IReadOnlyList<PlayerChatMessage> Messages);
}
