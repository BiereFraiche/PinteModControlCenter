using System.Globalization;
using System.IO;
using System.Security.Principal;

namespace PinteMod.ControlCenter.Services;

/// <summary>
/// Installs a closed, first-party Windows Task Scheduler recovery trigger for the
/// per-user SMB Agent. The task can only start the fixed Agent executable with
/// the fixed --remote-agent argument; it never accepts a remote path/command.
/// </summary>
internal static class RemoteAgentRecoveryTaskService
{
    private const string TaskName = "PinteMod Control Center Agent Recovery";
    private const int TaskCreateOrUpdate = 6;
    private const int TaskLogonInteractiveToken = 3;
    private const int TaskTriggerDaily = 2;
    private const int TaskTriggerLogon = 9;
    private const int TaskActionExec = 0;
    private const int TaskInstancesIgnoreNew = 2;
    private static readonly TimeSpan UpdateMarkerMaximumAge = TimeSpan.FromMinutes(3);

    internal static bool EnsureInstalled(string agentExecutable, out string diagnostic)
    {
        diagnostic = string.Empty;
        try
        {
            var expected = NormalizeAgentExecutable(agentExecutable);
            var schedulerType = Type.GetTypeFromProgID("Schedule.Service");
            if (schedulerType is null)
            {
                diagnostic = "Planificateur de tâches Windows indisponible.";
                return false;
            }

            var serviceInstance = Activator.CreateInstance(schedulerType);
            if (serviceInstance is null)
            {
                diagnostic = "Planificateur de tâches Windows indisponible.";
                return false;
            }
            dynamic service = serviceInstance;

            service.Connect();
            dynamic root = service.GetFolder("\\");
            dynamic definition = service.NewTask(0);
            definition.RegistrationInfo.Description =
                "Relance uniquement l'Agent SMB first-party PinteMod Control Center s'il s'est arrêté.";
            definition.Settings.Enabled = true;
            definition.Settings.StartWhenAvailable = true;
            definition.Settings.AllowDemandStart = true;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.MultipleInstances = TaskInstancesIgnoreNew;
            definition.Settings.ExecutionTimeLimit = "PT0S";
            definition.Settings.Hidden = true;

            using var identity = WindowsIdentity.GetCurrent();
            var currentUser = identity.Name;
            definition.Principal.UserId = currentUser;
            definition.Principal.LogonType = TaskLogonInteractiveToken;
            definition.Principal.RunLevel = 0;

            dynamic logon = definition.Triggers.Create(TaskTriggerLogon);
            logon.Enabled = true;
            logon.UserId = currentUser;

            dynamic daily = definition.Triggers.Create(TaskTriggerDaily);
            daily.Enabled = true;
            daily.StartBoundary = DateTime.Now.AddMinutes(1).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            daily.DaysInterval = 1;
            daily.Repetition.Interval = "PT1M";
            daily.Repetition.Duration = "P1D";
            daily.Repetition.StopAtDurationEnd = false;

            dynamic action = definition.Actions.Create(TaskActionExec);
            action.Path = expected;
            action.Arguments = "--remote-agent";
            action.WorkingDirectory = RemoteAgentConfigurationStore.GetAgentHome();

            root.RegisterTaskDefinition(
                TaskName,
                definition,
                TaskCreateOrUpdate,
                currentUser,
                null,
                TaskLogonInteractiveToken,
                null);

            diagnostic = "Auto-récupération Agent active (ouverture de session + contrôle chaque minute).";
            return true;
        }
        catch (Exception exception)
        {
            diagnostic = "Auto-récupération Agent non installée : " + exception.Message;
            return false;
        }
    }

    internal static bool IsInstalledFor(string agentExecutable)
    {
        try
        {
            var expected = NormalizeAgentExecutable(agentExecutable);
            var schedulerType = Type.GetTypeFromProgID("Schedule.Service");
            if (schedulerType is null) return false;
            var serviceInstance = Activator.CreateInstance(schedulerType);
            if (serviceInstance is null) return false;
            dynamic service = serviceInstance;
            service.Connect();
            dynamic root = service.GetFolder("\\");
            dynamic task = root.GetTask(TaskName);
            dynamic definition = task.Definition;
            if (definition.Actions.Count < 1) return false;
            dynamic action = definition.Actions.Item(1);
            var path = action.Path as string;
            var arguments = action.Arguments as string;
            if (string.IsNullOrWhiteSpace(path)) return false;
            return string.Equals(Path.GetFullPath(path), expected, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(arguments?.Trim(), "--remote-agent", StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static bool Remove(out string diagnostic)
    {
        diagnostic = string.Empty;
        try
        {
            var schedulerType = Type.GetTypeFromProgID("Schedule.Service");
            if (schedulerType is null)
            {
                diagnostic = "Planificateur de tâches Windows indisponible.";
                return false;
            }

            var serviceInstance = Activator.CreateInstance(schedulerType);
            if (serviceInstance is null)
            {
                diagnostic = "Planificateur de tâches Windows indisponible.";
                return false;
            }

            dynamic service = serviceInstance;
            service.Connect();
            dynamic root = service.GetFolder("\\");
            root.DeleteTask(TaskName, 0);
            diagnostic = "Auto-récupération Agent supprimée.";
            return true;
        }
        catch (Exception)
        {
            // A missing task is already an acceptable disabled state.
            diagnostic = "Auto-récupération Agent déjà absente ou non supprimable.";
            return true;
        }
    }

    internal static bool ShouldSuppressAgentStartForUpdate()
    {
        var marker = RemoteAgentConfigurationStore.GetUpdateInProgressPath();
        try
        {
            if (!File.Exists(marker)) return false;
            var lastWrite = new DateTimeOffset(File.GetLastWriteTimeUtc(marker), TimeSpan.Zero);
            if (IsFreshUpdateMarker(DateTimeOffset.UtcNow, lastWrite)) return true;
            File.Delete(marker);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // If a fresh marker cannot be inspected safely, fail closed for this
            // one recovery attempt. Task Scheduler will retry one minute later.
            return true;
        }
    }


    internal static bool IsFreshUpdateMarker(DateTimeOffset nowUtc, DateTimeOffset markerUpdatedAtUtc)
    {
        var age = nowUtc - markerUpdatedAtUtc;
        return age >= TimeSpan.Zero && age <= UpdateMarkerMaximumAge;
    }

    internal static void MarkUpdateInProgress()
    {
        Directory.CreateDirectory(RemoteAgentConfigurationStore.GetAgentHome());
        File.WriteAllText(
            RemoteAgentConfigurationStore.GetUpdateInProgressPath(),
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    }

    internal static void ClearUpdateInProgress()
    {
        try
        {
            var marker = RemoteAgentConfigurationStore.GetUpdateInProgressPath();
            if (File.Exists(marker)) File.Delete(marker);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string NormalizeAgentExecutable(string agentExecutable)
    {
        if (string.IsNullOrWhiteSpace(agentExecutable))
        {
            throw new ArgumentException("Chemin Agent vide.", nameof(agentExecutable));
        }

        var full = Path.GetFullPath(agentExecutable.Trim());
        var expected = Path.GetFullPath(RemoteAgentConfigurationStore.GetExecutablePath());
        if (!string.Equals(full, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Le plan de récupération ne peut cibler que l'EXE Agent first-party géré.", nameof(agentExecutable));
        }
        return full;
    }
}
