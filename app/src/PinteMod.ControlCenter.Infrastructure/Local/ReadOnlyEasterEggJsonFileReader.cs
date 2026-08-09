using System.Text.Json;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

internal sealed class ReadOnlyEasterEggJsonFileReader
{
    private const int MaximumAttempts = 3;

    public Task<LocalJsonFileResult<T>> ReadAsync<T>(
        string path,
        Func<string, string> pathValidator,
        int maximumFileSizeBytes,
        Func<JsonElement, T> parser,
        CancellationToken cancellationToken = default)
        where T : class =>
        Task.Run(
            () => ReadOnWorkerAsync(path, pathValidator, maximumFileSizeBytes, parser, cancellationToken),
            cancellationToken);

    private static async Task<LocalJsonFileResult<T>> ReadOnWorkerAsync<T>(
        string path,
        Func<string, string> pathValidator,
        int maximumFileSizeBytes,
        Func<JsonElement, T> parser,
        CancellationToken cancellationToken)
        where T : class
    {
        string authorizedPath;
        try
        {
            authorizedPath = pathValidator(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Failure<T>(LocalReadStatus.AccessDenied, "Source locale refusée.");
        }
        LocalJsonFileResult<T>? lastFailure = null;

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!File.Exists(authorizedPath))
                {
                    return Failure<T>(LocalReadStatus.Missing, "Fichier absent.");
                }

                var before = new FileInfo(authorizedPath);
                before.Refresh();
                if (before.Length == 0)
                {
                    lastFailure = Failure<T>(LocalReadStatus.Empty, "Fichier vide.", before.LastWriteTimeUtc);
                }
                else if (before.Length > maximumFileSizeBytes)
                {
                    return Failure<T>(LocalReadStatus.Invalid, "Fichier anormalement volumineux.", before.LastWriteTimeUtc);
                }
                else
                {
                    await using var stream = VerifiedReadOnlyFile.Open(
                        authorizedPath,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    using var memory = new MemoryStream((int)Math.Min(before.Length, maximumFileSizeBytes));
                    await stream.CopyToAsync(memory, cancellationToken);

                    if (memory.Length > maximumFileSizeBytes)
                    {
                        return Failure<T>(LocalReadStatus.Invalid, "Fichier anormalement volumineux.", before.LastWriteTimeUtc);
                    }

                    var after = new FileInfo(authorizedPath);
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
                lastFailure = Failure<T>(exception.Status, exception.PublicMessage, TryGetLastWriteTimeUtc(authorizedPath));
                if (exception.Status == LocalReadStatus.UnsupportedSchema)
                {
                    return lastFailure;
                }
            }
            catch (JsonException)
            {
                lastFailure = Failure<T>(LocalReadStatus.Invalid, "JSON incomplet ou invalide.", TryGetLastWriteTimeUtc(authorizedPath));
            }
            catch (LocalFileAccessRefusedException)
            {
                return Failure<T>(LocalReadStatus.AccessDenied, "Source locale refusée.");
            }
            catch (UnauthorizedAccessException)
            {
                return Failure<T>(LocalReadStatus.AccessDenied, "Accès refusé.", TryGetLastWriteTimeUtc(authorizedPath));
            }
            catch (IOException)
            {
                lastFailure = Failure<T>(LocalReadStatus.IoError, "Lecture impossible.", TryGetLastWriteTimeUtc(authorizedPath));
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
