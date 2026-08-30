using PinteMod.ControlCenter.Core.Models;

using System.Text.RegularExpressions;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class ServerInstallationAnalyzer
{
    private static readonly Regex SetPortRegex = new(
        @"^\s*set\s+""?(?:gameport|net_port)\s*=\s*""?(?<port>\d{1,5})""?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex NetPortRegex = new(
        @"\+set\s+net_port\s+""?(?<port>\d{1,5})""?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly string[] ReservedGenericBridgeNames =
    [
        "cc_bridge_00_main.gsc",
        "cc_bridge_storage.gsc",
        "cc_bridge_adapter_pintemod.gsc",
        "cc_bridge_contracts.gsc"
    ];

    public Task<ManagedServerAnalysis> AnalyzeAsync(string? serverRoot, CancellationToken cancellationToken = default) =>
        Task.Run(() => Analyze(serverRoot, cancellationToken), cancellationToken);

    public ManagedServerAnalysis Analyze(string? serverRoot, CancellationToken cancellationToken = default)
    {
        var root = serverRoot?.Trim() ?? string.Empty;
        if (root.Length == 0 || !Directory.Exists(root))
        {
            return Empty("Racine serveur absente ou inaccessible.", IsUnc(root));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var boiii = Path.Combine(root, "boiii");
        var boiiiDetected = Directory.Exists(boiii);
        if (!boiiiDetected)
        {
            return new ManagedServerAnalysis(
                true, false, false, false, false, false, IsUnc(root), 0, [],
                ManagedServerIntegrationKind.Unknown,
                "Dossier accessible, mais aucune racine BOIII (boiii/) détectée.");
        }

        var customScripts = Path.Combine(boiii, "custom_scripts");
        var main = Path.Combine(customScripts, "ezz_admin_01_main.gsc");
        var storage = Path.Combine(customScripts, "ezz_admin_storage.gsc");
        var runtime = Path.Combine(customScripts, "ezz_admin_control_center_runtime.gsc");
        var contracts = Path.Combine(customScripts, "ezz_admin_control_center_contracts.gsc");
        var mainTrusted = PinteModFirstPartyTrust.IsTrustedScript(main, "ezz_admin_01_main.gsc");
        var storageTrusted = PinteModFirstPartyTrust.IsTrustedScript(storage, "ezz_admin_storage.gsc");
        var pintemod = mainTrusted && storageTrusted;
        var runtimeDetected = PinteModFirstPartyTrust.IsTrustedRuntime(runtime);
        var bridge = PinteModFirstPartyTrust.IsTrustedBridge(contracts);
        var genericBridge = ReservedGenericBridgeNames.Any(name => File.Exists(Path.Combine(customScripts, name)));
        var gscFiles = Directory.Exists(customScripts)
            ? Directory.EnumerateFiles(customScripts, "*.gsc", SearchOption.TopDirectoryOnly)
                .Take(512)
                .ToArray()
            : Array.Empty<string>();
        var gscCount = gscFiles.Length;
        var knownFirstParty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (mainTrusted) knownFirstParty.Add("ezz_admin_01_main.gsc");
        if (storageTrusted) knownFirstParty.Add("ezz_admin_storage.gsc");
        if (runtimeDetected) knownFirstParty.Add("ezz_admin_control_center_runtime.gsc");
        if (bridge) knownFirstParty.Add("ezz_admin_control_center_contracts.gsc");
        foreach (var name in ReservedGenericBridgeNames)
        {
            knownFirstParty.Add(name);
        }

        var thirdPartyNames = gscFiles
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name) && !knownFirstParty.Contains(name!))
            .Take(64)
            .Select(name => name!)
            .ToArray();
        var thirdPartyDetected = !pintemod && !bridge && !genericBridge && thirdPartyNames.Length > 0;
        var thirdPartyPaths = gscFiles
            .Where(path => thirdPartyNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var thirdPartyAudit = thirdPartyDetected
            ? new ThirdPartyGscAuditor().Audit(thirdPartyPaths, cancellationToken)
            : ThirdPartyGscAudit.Empty;
        var launchers = FindLauncherCandidates(root, cancellationToken);
        var detectedPort = DetectServerPort(root, launchers, cancellationToken, out var detectedPortLauncher);
        var kind = pintemod
            ? ManagedServerIntegrationKind.PinteMod
            : bridge || genericBridge
                ? ManagedServerIntegrationKind.ControlCenterBridge
                : thirdPartyDetected
                    ? ManagedServerIntegrationKind.ThirdPartyScripts
                    : ManagedServerIntegrationKind.BoiiiNative;
        var integration = kind switch
        {
            ManagedServerIntegrationKind.PinteMod => bridge
                ? "PinteMod + Control Center Bridge"
                : "PinteMod",
            ManagedServerIntegrationKind.ControlCenterBridge => "BOIII + module de compatibilité",
            ManagedServerIntegrationKind.ThirdPartyScripts => "BOIII + scripts personnalisés",
            _ => "BOIII natif"
        };
        var summary = $"{integration} · {gscCount} GSC · " +
                      (IsUnc(root) ? "accès LAN/UNC" : "racine locale");

        var integrationProfile = BuildIntegrationProfile(
            kind,
            pintemod,
            runtimeDetected,
            bridge,
            genericBridge,
            launchers.Count > 0,
            thirdPartyAudit);

        return new ManagedServerAnalysis(
            true,
            true,
            pintemod,
            runtimeDetected,
            bridge,
            genericBridge,
            IsUnc(root),
            gscCount,
            launchers,
            kind,
            summary)
        {
            ThirdPartyGscDetected = thirdPartyDetected,
            ThirdPartyGscCount = thirdPartyNames.Length,
            ThirdPartyGscNames = thirdPartyNames,
            DetectedServerPort = detectedPort,
            DetectedServerPortLauncher = detectedPortLauncher,
            IntegrationProfile = integrationProfile
        };
    }

    private static ServerIntegrationProfile BuildIntegrationProfile(
        ManagedServerIntegrationKind kind,
        bool pinteMod,
        bool runtimeDetected,
        bool bridge,
        bool genericBridge,
        bool lifecycleAvailable,
        ThirdPartyGscAudit audit)
    {
        var capabilities = new List<IntegrationCapability>();
        void Add(
            IntegrationCapabilityKey key,
            IntegrationCapabilityAvailability availability,
            string evidence,
            string source) =>
            capabilities.Add(new IntegrationCapability(key, availability, evidence, source));

        Add(
            IntegrationCapabilityKey.ServerLifecycle,
            lifecycleAvailable
                ? IntegrationCapabilityAvailability.Available
                : IntegrationCapabilityAvailability.Unavailable,
            lifecycleAvailable
                ? "Racine BOIII et lanceur enregistrable détectés."
                : "Racine BOIII détectée, mais aucun lanceur .bat, .cmd ou .exe n’est prouvé.",
            lifecycleAvailable ? "BOIII" : "Fail-closed");

        if (pinteMod)
        {
            Add(IntegrationCapabilityKey.ServerInformation, IntegrationCapabilityAvailability.Available,
                runtimeDetected ? "Snapshot runtime PinteMod détecté." : "PinteMod détecté ; runtime Control Center installable.", "PinteMod");
            Add(IntegrationCapabilityKey.MapAndRound, IntegrationCapabilityAvailability.Available,
                "Carte/manche normalisées par le provider PinteMod.", "PinteMod");
            Add(IntegrationCapabilityKey.Players, IntegrationCapabilityAvailability.Available,
                "Snapshot joueurs PinteMod avec identité stable.", "PinteMod");
            Add(IntegrationCapabilityKey.Chat, IntegrationCapabilityAvailability.Available,
                "Journal chat PinteMod structuré/local.", "PinteMod");
            Add(IntegrationCapabilityKey.ServerCommands, IntegrationCapabilityAvailability.Available,
                "Commandes serveur fermées PinteMod uniquement.", "PinteMod RCON");
            Add(IntegrationCapabilityKey.PlayerCommands, IntegrationCapabilityAvailability.Available,
                "Commandes joueur fermées PinteMod uniquement.", "PinteMod RCON");
            Add(IntegrationCapabilityKey.PublicIdentity,
                bridge ? IntegrationCapabilityAvailability.Available : IntegrationCapabilityAvailability.Observed,
                bridge ? "Contrat identité Control Center détecté." : "PinteMod détecté ; Bridge requis pour identité structurée.",
                bridge ? "Bridge PinteMod" : "PinteMod");
            Add(IntegrationCapabilityKey.Ranks, IntegrationCapabilityAvailability.Available,
                "Ranks PinteMod v2 pris en charge.", "PinteMod");
            Add(IntegrationCapabilityKey.Records, IntegrationCapabilityAvailability.Available,
                "Records manches/EE PinteMod pris en charge.", "PinteMod");
            Add(IntegrationCapabilityKey.BossesAndEvents,
                bridge ? IntegrationCapabilityAvailability.Available : IntegrationCapabilityAvailability.Observed,
                bridge ? "Capacités boss/events déclarées map par map par le Bridge." : "Module PinteMod détecté ; contrat Bridge requis.",
                bridge ? "Bridge PinteMod" : "PinteMod");

            return new ServerIntegrationProfile(
                kind,
                bridge ? "PinteMod + Bridge" : "PinteMod",
                IntegrationCommandTransport.PinteModClosedRconV1,
                capabilities,
                audit);
        }

        if (genericBridge || kind == ManagedServerIntegrationKind.ControlCenterBridge)
        {
            Add(IntegrationCapabilityKey.ServerInformation, IntegrationCapabilityAvailability.Observed,
                "Module de compatibilité détecté ; données structurées à valider au runtime.", "Generic Bridge");
            Add(IntegrationCapabilityKey.MapAndRound, IntegrationCapabilityAvailability.Observed,
                "Bridge détecté ; contrat runtime requis avant activation.", "Generic Bridge");
            Add(IntegrationCapabilityKey.Players, IntegrationCapabilityAvailability.Observed,
                "Bridge détecté ; identité joueur structurée à prouver.", "Generic Bridge");
            Add(IntegrationCapabilityKey.Chat, IntegrationCapabilityAvailability.Observed,
                "Bridge détecté ; source chat à prouver.", "Generic Bridge");
            Add(IntegrationCapabilityKey.ServerCommands, IntegrationCapabilityAvailability.Unavailable,
                "Aucun adaptateur de commandes fermé v1 validé.", "Fail-closed");
            Add(IntegrationCapabilityKey.PlayerCommands, IntegrationCapabilityAvailability.Unavailable,
                "Aucun adaptateur joueur fermé v1 validé.", "Fail-closed");
            Add(IntegrationCapabilityKey.PublicIdentity, IntegrationCapabilityAvailability.Observed,
                "Module de compatibilité présent.", "Generic Bridge");
            Add(IntegrationCapabilityKey.Ranks, IntegrationCapabilityAvailability.Unavailable,
                "Aucun contrat ranks générique validé.", "Fail-closed");
            Add(IntegrationCapabilityKey.Records, IntegrationCapabilityAvailability.Unavailable,
                "Aucun contrat records générique validé.", "Fail-closed");
            Add(IntegrationCapabilityKey.BossesAndEvents, IntegrationCapabilityAvailability.Unavailable,
                "Aucun contrat events générique validé.", "Fail-closed");

            return new ServerIntegrationProfile(
                kind,
                "Generic Bridge (détection)",
                IntegrationCommandTransport.None,
                capabilities,
                audit);
        }

        if (kind == ManagedServerIntegrationKind.ThirdPartyScripts)
        {
            Add(IntegrationCapabilityKey.ServerInformation, IntegrationCapabilityAvailability.Observed,
                "BOIII est détecté ; aucune télémétrie tierce structurée n’est encore contractualisée.", "BOIII + audit GSC");
            Add(IntegrationCapabilityKey.MapAndRound, IntegrationCapabilityAvailability.Unavailable,
                "Aucune source carte/manche structurée prouvée.", "Fail-closed");
            Add(IntegrationCapabilityKey.Players,
                audit.ObservesPlayers ? IntegrationCapabilityAvailability.Observed : IntegrationCapabilityAvailability.Unavailable,
                audit.ObservesPlayers ? "Hooks joueurs observés dans les sources GSC ; lecture non activée sans adaptateur." : "Aucun hook joueur exploitable observé.",
                "Audit GSC read-only");
            Add(IntegrationCapabilityKey.Chat,
                audit.ObservesChat ? IntegrationCapabilityAvailability.Observed : IntegrationCapabilityAvailability.Unavailable,
                audit.ObservesChat ? "Hooks chat observés ; aucune conversion activée sans contrat." : "Aucune source chat exploitable observée.",
                "Audit GSC read-only");
            Add(IntegrationCapabilityKey.ServerCommands,
                audit.ObservedFamilies.Count > 0 ? IntegrationCapabilityAvailability.Observed : IntegrationCapabilityAvailability.Unavailable,
                audit.ObservedFamilies.Count > 0
                    ? $"Familles de commandes observées : {string.Join(", ", audit.ObservedFamilies)}. Exécution interdite sans adaptateur fermé."
                    : "Aucune commande tierce reconnue dans le catalogue fermé.",
                "Audit GSC read-only");
            Add(IntegrationCapabilityKey.PlayerCommands, IntegrationCapabilityAvailability.Unavailable,
                "Les commandes tierces ne sont jamais exécutées depuis une simple analyse de source.", "Fail-closed");
            Add(IntegrationCapabilityKey.PublicIdentity, IntegrationCapabilityAvailability.Unavailable,
                "Aucun contrat identité tiers validé.", "Fail-closed");
            Add(IntegrationCapabilityKey.Ranks, IntegrationCapabilityAvailability.Unavailable,
                "Aucun contrat ranks tiers validé.", "Fail-closed");
            Add(IntegrationCapabilityKey.Records, IntegrationCapabilityAvailability.Unavailable,
                "Aucun contrat records tiers validé.", "Fail-closed");
            Add(IntegrationCapabilityKey.BossesAndEvents, IntegrationCapabilityAvailability.Unavailable,
                "Aucun contrat boss/events tiers validé.", "Fail-closed");

            return new ServerIntegrationProfile(
                kind,
                "GSC tiers · audit read-only",
                IntegrationCommandTransport.None,
                capabilities,
                audit);
        }

        Add(IntegrationCapabilityKey.ServerInformation, IntegrationCapabilityAvailability.Observed,
            "Installation BOIII détectée ; télémétrie native structurée non activée dans cette Preview.", "BOIII natif");
        foreach (var key in new[]
                 {
                     IntegrationCapabilityKey.MapAndRound,
                     IntegrationCapabilityKey.Players,
                     IntegrationCapabilityKey.Chat,
                     IntegrationCapabilityKey.ServerCommands,
                     IntegrationCapabilityKey.PlayerCommands,
                     IntegrationCapabilityKey.PublicIdentity,
                     IntegrationCapabilityKey.Ranks,
                     IntegrationCapabilityKey.Records,
                     IntegrationCapabilityKey.BossesAndEvents
                 })
        {
            Add(key, IntegrationCapabilityAvailability.Unavailable,
                "Aucune capacité structurée prouvée pour BOIII natif dans cette Preview.", "Fail-closed");
        }

        return new ServerIntegrationProfile(
            kind,
            "BOIII natif",
            IntegrationCommandTransport.None,
            capabilities,
            audit);
    }

    private static IReadOnlyList<string> FindLauncherCandidates(string root, CancellationToken cancellationToken)
    {
        var preferred = new[]
        {
            "Server.bat",
            "server.bat",
            "Start_Server.bat",
            "Launch_Server.bat",
            "Launch_PinteMod_Server.bat"
        };
        var results = new List<string>();
        foreach (var name in preferred)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(Path.Combine(root, name)))
            {
                results.Add(name);
            }
        }

        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(path);
            if (!extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = Path.GetFileName(path);
            if (name.Length <= 96 && !results.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(name);
            }

            if (results.Count >= 32)
            {
                break;
            }
        }

        return results;
    }

    private static int? DetectServerPort(
        string root,
        IReadOnlyList<string> launchers,
        CancellationToken cancellationToken,
        out string launcherName)
    {
        launcherName = string.Empty;
        foreach (var launcher in launchers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(launcher);
            if (!extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = Path.Combine(root, launcher);
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > 1024 * 1024)
                {
                    continue;
                }

                var content = File.ReadAllText(path);
                var match = SetPortRegex.Match(content);
                if (!match.Success)
                {
                    match = NetPortRegex.Match(content);
                }

                if (match.Success &&
                    int.TryParse(match.Groups["port"].Value, out var port) &&
                    port is >= 1 and <= 65535)
                {
                    launcherName = launcher;
                    return port;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A launcher that cannot be read is simply not a proof of port.
            }
        }

        return null;
    }

    private static bool IsUnc(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.StartsWith("\\\\", StringComparison.Ordinal);

    private static ManagedServerAnalysis Empty(string summary, bool isUnc) =>
        new(false, false, false, false, false, false, isUnc, 0, [], ManagedServerIntegrationKind.Unknown, summary);
}
