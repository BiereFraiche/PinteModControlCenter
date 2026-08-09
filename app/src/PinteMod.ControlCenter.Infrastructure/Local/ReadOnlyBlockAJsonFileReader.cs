using System.Text.Json;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

internal sealed class ReadOnlyBlockAJsonFileReader
{
    private const int MaximumFileSizeBytes = 1024 * 1024;
    private const int MaximumAttempts = 3;

    public Task<LocalJsonFileResult<T>> ReadAsync<T>(
        string path,
        Func<JsonElement, T> parser,
        CancellationToken cancellationToken = default)
        where T : class =>
        Task.Run(() => ReadWorkerAsync(path, parser, cancellationToken), cancellationToken);

    private static async Task<LocalJsonFileResult<T>> ReadWorkerAsync<T>(
        string path,
        Func<JsonElement, T> parser,
        CancellationToken cancellationToken)
        where T : class
    {
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
                    var after = new FileInfo(path);
                    after.Refresh();
                    if (memory.Length > MaximumFileSizeBytes ||
                        before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
                    {
                        lastFailure = Failure<T>(LocalReadStatus.Invalid, "Fichier modifié pendant la lecture.", after.LastWriteTimeUtc);
                    }
                    else
                    {
                        memory.Position = 0;
                        using var document = await JsonDocument.ParseAsync(memory, cancellationToken: cancellationToken);
                        return new(parser(document.RootElement), LocalReadStatus.Success,
                            new DateTimeOffset(after.LastWriteTimeUtc, TimeSpan.Zero), "Lecture réussie.");
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
            catch (InvalidOperationException)
            {
                lastFailure = Failure<T>(LocalReadStatus.Invalid, "Structure JSON inattendue.", TryGetLastWriteTimeUtc(path));
            }
            catch (UnauthorizedAccessException)
            {
                return Failure<T>(LocalReadStatus.AccessDenied, "Accès refusé.", TryGetLastWriteTimeUtc(path));
            }
            catch (IOException)
            {
                lastFailure = Failure<T>(LocalReadStatus.IoError, "Lecture impossible.", TryGetLastWriteTimeUtc(path));
            }

            if (attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(35), cancellationToken);
            }
        }

        return lastFailure ?? Failure<T>(LocalReadStatus.IoError, "Lecture locale impossible.");
    }

    private static LocalJsonFileResult<T> Failure<T>(LocalReadStatus status, string message, DateTime? lastWriteTimeUtc = null)
        where T : class =>
        new(null, status,
            lastWriteTimeUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(lastWriteTimeUtc.Value, DateTimeKind.Utc)),
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
