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

    public string PublicMessage { get; } = message;
}

internal sealed class ReadOnlyJsonFileReader(
    LocalPinteModOptions options,
    Action<string>? afterReadBeforeVerification = null,
    Action<string>? afterMetadataBeforeRead = null)
{
    private const int MaximumFileSizeBytes = 1024 * 1024;
    private const int MaximumAttempts = 3;

    public Task<LocalJsonFileResult<T>> ReadAsync<T>(
        LocalPinteModFile file,
        Func<JsonElement, T> parser,
        CancellationToken cancellationToken = default)
        where T : class =>
        ReadAsync(file, parser, MaximumFileSizeBytes, cancellationToken);

    public Task<LocalJsonFileResult<T>> ReadAsync<T>(
        LocalPinteModFile file,
        Func<JsonElement, T> parser,
        int maximumFileSizeBytes,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (maximumFileSizeBytes <= 0 || maximumFileSizeBytes > MaximumFileSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFileSizeBytes));
        }

        return Task.Run(
            () => ReadOnWorkerAsync(file, parser, maximumFileSizeBytes, cancellationToken),
            cancellationToken);
    }

    private async Task<LocalJsonFileResult<T>> ReadOnWorkerAsync<T>(
        LocalPinteModFile file,
        Func<JsonElement, T> parser,
        int maximumFileSizeBytes,
        CancellationToken cancellationToken)
        where T : class
    {
        var path = options.ResolvePath(file);
        LocalJsonFileResult<T>? lastFailure = null;

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime? verifiedLastWriteTimeUtc = null;

            try
            {
                await using var stream = VerifiedReadOnlyFile.Open(
                    path,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var before = VerifiedReadOnlyFile.GetMetadata(stream);
                verifiedLastWriteTimeUtc = before.LastWriteTimeUtc;
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
                    using var memory = new MemoryStream((int)Math.Min(before.Length, maximumFileSizeBytes));
                    afterMetadataBeforeRead?.Invoke(path);
                    await CopyAtMostAsync(stream, memory, maximumFileSizeBytes + 1, cancellationToken);

                    if (memory.Length > maximumFileSizeBytes)
                    {
                        return Failure<T>(LocalReadStatus.Invalid, "Fichier anormalement volumineux.", before.LastWriteTimeUtc);
                    }

                    afterReadBeforeVerification?.Invoke(path);
                    var after = VerifiedReadOnlyFile.GetMetadata(stream);
                    verifiedLastWriteTimeUtc = after.LastWriteTimeUtc;
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
                            new DateTimeOffset(DateTime.SpecifyKind(after.LastWriteTimeUtc, DateTimeKind.Utc)),
                            "Lecture réussie.");
                    }
                }
            }
            catch (LocalJsonValidationException exception)
            {
                lastFailure = Failure<T>(exception.Status, exception.PublicMessage, verifiedLastWriteTimeUtc);
                if (exception.Status == LocalReadStatus.UnsupportedSchema)
                {
                    return lastFailure;
                }
            }
            catch (JsonException)
            {
                lastFailure = Failure<T>(LocalReadStatus.Invalid, "JSON incomplet ou invalide.", verifiedLastWriteTimeUtc);
            }
            catch (LocalFileAccessRefusedException)
            {
                return Failure<T>(LocalReadStatus.AccessDenied, "Source locale refusée.");
            }
            catch (FileNotFoundException)
            {
                return Failure<T>(LocalReadStatus.Missing, "Fichier absent.");
            }
            catch (DirectoryNotFoundException)
            {
                return Failure<T>(LocalReadStatus.Missing, "Fichier absent.");
            }
            catch (UnauthorizedAccessException)
            {
                return Failure<T>(LocalReadStatus.AccessDenied, "Accès refusé.", verifiedLastWriteTimeUtc);
            }
            catch (IOException)
            {
                lastFailure = Failure<T>(LocalReadStatus.IoError, "Lecture impossible.", verifiedLastWriteTimeUtc);
            }

            if (attempt < MaximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(35), cancellationToken);
            }
        }

        return lastFailure ?? Failure<T>(LocalReadStatus.IoError, "Lecture locale impossible.");
    }

    internal static async Task CopyAtMostAsync(
        Stream source,
        Stream destination,
        int maximumBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        var buffer = new byte[Math.Min(81_920, maximumBytes)];
        var copied = 0;
        while (copied < maximumBytes)
        {
            var requested = Math.Min(buffer.Length, maximumBytes - copied);
            var read = await source.ReadAsync(buffer.AsMemory(0, requested), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
        }
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

}
