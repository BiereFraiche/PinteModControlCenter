using System.IO;

namespace PinteMod.ControlCenter.Services;

/// <summary>
/// A folder publish has a lightweight apphost EXE plus DLL dependencies. The
/// companion is the verified single-file executable shipped beside it and is
/// therefore the only safe source for a background Agent copy.
/// </summary>
internal static class RemoteAgentExecutableSourceResolver
{
    internal const string CompanionFileName = "PinteMod.ControlCenter.Agent.exe";

    internal static string Resolve(string controlCenterExecutable)
    {
        if (string.IsNullOrWhiteSpace(controlCenterExecutable)) return string.Empty;

        try
        {
            var source = Path.GetFullPath(controlCenterExecutable);
            var directory = Path.GetDirectoryName(source);
            if (string.IsNullOrWhiteSpace(directory)) return source;

            var companion = Path.Combine(directory, CompanionFileName);
            return File.Exists(companion) ? companion : source;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return controlCenterExecutable;
        }
    }
}
