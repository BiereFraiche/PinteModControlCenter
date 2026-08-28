using System.Diagnostics;
using System.Text;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

/// <summary>
/// Stops one explicitly selected local BOIII server and only PinteMod first-party
/// helper processes tied to that same server/profile. No free-form command is accepted.
/// </summary>
public sealed class ManagedServerStopService
{
    public async Task<ServerLaunchResult> StopAsync(
        string profileId,
        string serverRoot,
        int serverPort,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return new ServerLaunchResult(false, "Identifiant serveur manquant.");
        }

        if (string.IsNullOrWhiteSpace(serverRoot) || serverRoot.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return new ServerLaunchResult(false, "L’arrêt local exige une racine serveur locale.");
        }

        if (serverPort is < 1 or > 65535)
        {
            return new ServerLaunchResult(false, "Port BOIII invalide.");
        }

        string root;
        try
        {
            root = Path.GetFullPath(serverRoot.Trim()).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new ServerLaunchResult(false, "Racine BOIII locale invalide.");
        }

        if (!Directory.Exists(Path.Combine(root, "boiii")))
        {
            return new ServerLaunchResult(false, "Racine BOIII locale invalide.");
        }

        var safeProfileId = new string(profileId
            .Where(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.')
            .Take(48)
            .ToArray());
        if (safeProfileId.Length == 0)
        {
            return new ServerLaunchResult(false, "Identifiant serveur invalide.");
        }

        var script = BuildStopScript();
        var scriptPath = Path.Combine(Path.GetTempPath(), $"pintemod-cc-stop-{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ServerLaunchResult(false, "Windows n’a pas pu préparer l’arrêt contrôlé du serveur.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-OutputFormat");
        startInfo.ArgumentList.Add("Text");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.Environment["PINTE_CC_SERVER_ROOT"] = root;
        startInfo.Environment["PINTE_CC_PROFILE_ID"] = safeProfileId;
        startInfo.Environment["PINTE_CC_SERVER_PORT"] = serverPort.ToString(System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            Process? startedProcess;
            try
            {
                startedProcess = Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return new ServerLaunchResult(false, "Windows n’a pas pu démarrer l’arrêt contrôlé du serveur.");
            }

            using var process = startedProcess;
            if (process is null)
            {
                return new ServerLaunchResult(false, "Windows n’a pas pu démarrer l’arrêt contrôlé du serveur.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = (await outputTask.ConfigureAwait(false)).Trim();
            var error = (await errorTask.ConfigureAwait(false)).Trim();

            if (process.ExitCode == 0)
            {
                return new ServerLaunchResult(true, GetSuccessMessage(output));
            }

            var detail = NormalizePowerShellError(error.Length > 0 ? error : output);
            if (detail.Length > 280) detail = detail[..280];
            return new ServerLaunchResult(
                false,
                detail.Length > 0
                    ? "Arrêt refusé : " + detail
                    : "Arrêt refusé : BOIII n’a pas pu être identifié de façon sûre pour ce profil.");
        }
        finally
        {
            try { File.Delete(scriptPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static string BuildStopScript() => """
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding
$root = [IO.Path]::GetFullPath($env:PINTE_CC_SERVER_ROOT).TrimEnd([IO.Path]::DirectorySeparatorChar)
$port = [int]$env:PINTE_CC_SERVER_PORT

function Test-RootCommandLine([string]$commandLine) {
    if ([string]::IsNullOrWhiteSpace($commandLine)) { return $false }
    return $commandLine.IndexOf($root, [StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Test-FirstPartyHelper([string]$commandLine) {
    if ([string]::IsNullOrWhiteSpace($commandLine)) { return $false }
    $names = @(
        "PinteMod_MultiServer_Worker.ps1",
        "PinteMod_Server_Launcher.ps1",
        "PinteMod_Launch_SingleInstance.ps1",
        "PinteMod_Ban_Service.multi.ps1",
        "PinteMod_Ban_Service.ps1",
        "PinteMod_GeoIP_Bridge.multi.ps1",
        "PinteMod_GeoIP_Bridge.ps1",
        "PinteMod_LiveConsole.ps1",
        "PinteMod_Remote_RCON.ps1",
        "PinteMod_Remote_Tools_Launcher.ps1"
    )
    foreach ($name in $names) {
        if ($commandLine.IndexOf($name, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true }
    }
    return $false
}

# Stop only first-party PowerShell helpers whose command line belongs to this server root.
$helpers = @(Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue | Where-Object {
    if ([int]$_.ProcessId -eq $PID) { return $false }
    $cmd = [string]$_.CommandLine
    (Test-RootCommandLine $cmd) -and (Test-FirstPartyHelper $cmd)
})
foreach ($item in $helpers) {
    Stop-Process -Id ([int]$item.ProcessId) -Force -ErrorAction SilentlyContinue
}
if ($helpers.Count -gt 0) { Start-Sleep -Milliseconds 250 }

# Resolve the owner PID from the explicitly configured BOIII UDP port.
if ($null -eq (Get-Command Get-NetUDPEndpoint -ErrorAction SilentlyContinue)) {
    Write-Error "Impossible de vérifier de façon sûre le propriétaire du port BOIII demandé."
    exit 6
}
$owners = @(Get-NetUDPEndpoint -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)

$boiiiStopped = $false
foreach ($owner in $owners) {
    $pidValue = [int]$owner
    if ($pidValue -le 0) { continue }
    $boiii = Get-CimInstance Win32_Process -Filter ("ProcessId=" + $pidValue) -ErrorAction SilentlyContinue
    if ($null -eq $boiii) { continue }
    if ([string]$boiii.Name -ine "boiii.exe") { continue }

    $exe = [string]$boiii.ExecutablePath
    if ([string]::IsNullOrWhiteSpace($exe)) { continue }
    try { $fullExe = [IO.Path]::GetFullPath($exe) } catch { continue }
    $rootPrefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $fullExe.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { continue }

    Stop-Process -Id $pidValue -Force -ErrorAction Stop
    $boiiiStopped = $true
}

foreach ($relative in @(
    "boiii\scriptdata\pintemod\health\supervisor.json",
    "boiii\scriptdata\pintemod\health\pintemod.json"
)) {
    Remove-Item -LiteralPath (Join-Path $root $relative) -Force -ErrorAction SilentlyContinue
}

if ($boiiiStopped) {
    Write-Output "STOPPED"
    exit 0
}

# If no endpoint remains, the final requested state is already reached.
$stillOwned = @((Get-NetUDPEndpoint -LocalPort $port -ErrorAction SilentlyContinue)).Count -gt 0
if (-not $stillOwned) {
    Write-Output "ALREADY_STOPPED"
    exit 0
}

Write-Error "Le port demandé est utilisé, mais aucun boiii.exe correspondant à cette racine n'a été identifié."
exit 7
""";

    internal static string GetSuccessMessage(string output) => output switch
    {
        "STOPPED" => "Serveur BOIII arrêté. Les services PinteMod de ce profil ont également été arrêtés.",
        "ALREADY_STOPPED" => "Serveur déjà arrêté. Les services PinteMod résiduels ont été nettoyés.",
        _ => "Serveur arrêté."
    };

    private static string NormalizePowerShellError(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
        if (!normalized.Contains("#< CLIXML", StringComparison.OrdinalIgnoreCase)) return normalized;

        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, "<[^>]+>", " ");
        normalized = System.Net.WebUtility.HtmlDecode(normalized)
            .Replace("_x000D_", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("_x000A_", " ", StringComparison.OrdinalIgnoreCase);
        return string.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
