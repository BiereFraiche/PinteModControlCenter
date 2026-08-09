using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PinteMod.ControlCenter.Infrastructure.Local;

internal sealed class LocalFileAccessRefusedException()
    : IOException("La source locale réellement ouverte ne correspond pas au chemin autorisé.");

internal static class VerifiedReadOnlyFile
{
    private const int InitialPathBufferLength = 512;
    private const int MaximumPathBufferLength = 32_768;

    public static FileStream Open(string authorizedPath, FileOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedPath);
        var expectedPath = NormalizePath(Path.GetFullPath(authorizedPath));
        var handle = File.OpenHandle(
            authorizedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            options);

        try
        {
            var openedPath = NormalizePath(GetFinalPath(handle));
            EnsureOpenedPathMatches(expectedPath, openedPath);
            return new FileStream(
                handle,
                FileAccess.Read,
                4096,
                (options & FileOptions.Asynchronous) != 0);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static void EnsureOpenedPathMatches(string expectedPath, string openedPath)
    {
        if (!string.Equals(
                NormalizePath(expectedPath),
                NormalizePath(openedPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalFileAccessRefusedException();
        }
    }

    private static string GetFinalPath(SafeFileHandle handle)
    {
        var capacity = InitialPathBufferLength;
        while (capacity <= MaximumPathBufferLength)
        {
            var buffer = new StringBuilder(capacity);
            var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0)
            {
                throw new IOException("La cible du fichier local ouvert ne peut pas être vérifiée.");
            }

            if (length < buffer.Capacity)
            {
                return buffer.ToString();
            }

            capacity = checked((int)length + 1);
        }

        throw new IOException("Le chemin du fichier local ouvert dépasse la limite autorisée.");
    }

    private static string NormalizePath(string path)
    {
        const string extendedUncPrefix = @"\\?\UNC\";
        const string extendedPathPrefix = @"\\?\";

        var normalized = path.StartsWith(extendedUncPrefix, StringComparison.OrdinalIgnoreCase)
            ? @"\\" + path[extendedUncPrefix.Length..]
            : path.StartsWith(extendedPathPrefix, StringComparison.OrdinalIgnoreCase)
                ? path[extendedPathPrefix.Length..]
                : path;
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(normalized));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder filePath,
        uint filePathLength,
        uint flags);
}
