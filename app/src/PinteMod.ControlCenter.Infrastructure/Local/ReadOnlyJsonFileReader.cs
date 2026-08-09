using System.Text.Json;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

internal sealed record LocalJsonFileResult<T>(
    T? Value,
    LocalReadStatus Status,
    DateTimeOffset? LastWriteTimeUtc,
    string Message)
    where T : class;

internal sealed class LocalJsonValidationException(LocalReadStatus status, string message)
    : Exception(message)
{
    public LocalReadStatus Status { get; } = status;
}

internal sealed class ReadOnlyJsonFileReader(LocalPinteModOptions options)
{
    private const int MaximumFileSizeBytes = 1024 * 1024;
    private const int MaximumAttempts = 3;

    public Task<LocalJsonFileResult<T>> ReadAsync<T>(
        LocalPinteModFile file,
        Func<JsonElement, T> parser,
        CancellationToken cancellationToken = default)
        where T : class =>
        Task.Run(() => ReadOnWorkerAsync(file, parser, cancellationToken), cancellationToken);

    private async Task<LocalJsonFileResult<T>> ReadOnWorkerAsync<T>(
        LocalPinteModFile file,
        Func<JsonElement, T> parser,
        CancellationToken cancellationToken)
        where T : class
    {
        var path = options.ResolvePath(file);
        LocalJsonFileResult<T>? lastFailure = null;

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!File.Exists(path))
                {
                    return Failure<T>(LocalReadStatus.Missing, "Fichier absent.");
                }

                var before = new FileInfo(path);
                before.Refresh();
                if (before.Length == 0)
                {
                    lastFailure = Failure<T>(LocalReadStatus.Empty, "Fichier vide.", before.LastWriteTimeUtc);
                }
                else if (before.Length > MaximumFileSizeBytes)
                {
                    return Failure<T>(LocalReadStatus.Invalid, "Fichier anormalement volumineux.", before.LastWriteTimeUtc);
                }
                else
                {
                    await using var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        4096,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    using var memory = new MemoryStream((int)Math.Min(before.Length, MaximumFileSizeBytes));
                    await stream.CopyToAsync(memory, cancellationToken);

                    if (memory.Length > MaximumFileSizeBytes)
                    {
                        return Failure<T>(LocalReadStatus.Invalid, "Fichier anormalement volumineux.", before.LastWriteTimeUtc);
                    }

                    var after = new FileInfo(path);
                    after.Refresh();
                    if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
                    {
                        lastFailure = Failure<T>(LocalReadStatus.Invalid, "Fichier modifié pendant la lecture.", after.LastWriteTimeUtc);
                    }
                    else
                    {
                        memory.Position = 0;
                        using var document = await JsonDocument.ParseAsync(memory, cancellationToken: cancellationToken);
                        var value = parser(document.RootElement);
                        return new LocalJsonFileResult<T>(
                            value,
                            LocalReadStatus.Success,
                            new DateTimeOffset(after.LastWriteTimeUtc, TimeSpan.Zero),
                            "Lecture réussie.");
                    }
                }
            }
            catch (LocalJsonValidationException exception)
            {
                lastFailure = Failure<T>(exception.Status, exception.Message, TryGetLastWriteTimeUtc(path));
                if (exception.Status == LocalReadStatus.UnsupportedSchema)
                {
                    return lastFailure;
                }
            }
            catch (JsonException exception)
            {
                lastFailure = Failure<T>(LocalReadStatus.Invalid, $"JSON incomplet ou invalide : {exception.Message}", TryGetLastWriteTimeUtc(path));
            }
            catch (UnauthorizedAccessException exception)
            {
                return Failure<T>(LocalReadStatus.AccessDenied, $"Accès refusé : {exception.Message}", TryGetLastWriteTimeUtc(path));
            }
            catch (IOException exception)
            {
                lastFailure = Failure<T>(LocalReadStatus.IoError, $"Lecture impossible : {exception.Message}", TryGetLastWriteTimeUtc(path));
            }

            if (attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(35), cancellationToken);
            }
        }

        return lastFailure ?? Failure<T>(LocalReadStatus.IoError, "Lecture locale impossible.");
    }

    private static LocalJsonFileResult<T> Failure<T>(
        LocalReadStatus status,
        string message,
        DateTime? lastWriteTimeUtc = null)
        where T : class =>
        new(
            null,
            status,
            lastWriteTimeUtc is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(lastWriteTimeUtc.Value, DateTimeKind.Utc)),
            message);

    private static DateTime? TryGetLastWriteTimeUtc(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
